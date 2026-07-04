namespace MindVault.Core;

public sealed record IndexIssue(string Code, string Message, string? Path = null);

public sealed record IndexStatusReport(
    string IndexPath, bool IndexExists, long IndexSizeBytes, int SchemaVersion,
    int ExpectedSchemaVersion, int NoteCount, int FtsRowCount, DateTime? LastScanUtc,
    bool RescanPending);

public sealed record IndexVerifyReport(bool Ok, IReadOnlyList<IndexIssue> Issues, long ElapsedMs)
{
    /// <summary>The recovery is always the same: the index is a disposable cache.</summary>
    public string? Recommendation =>
        Ok ? null : "Run 'mindvault index rebuild' — the Markdown files are canonical and the index is fully rebuildable.";
}

/// <summary>
/// Detects index drift without rebuilding: indexed notes that vanished from disk, on-disk
/// notes the index missed, FTS row mismatches, stale file states and schema mismatches.
/// Everything it reports is fixed by a rebuild; nothing it reports can lose data.
/// </summary>
public sealed class IndexVerifier(VaultContext ctx)
{
    public IndexStatusReport Status()
    {
        var exists = ctx.IndexExists;
        var state = ctx.State.Load();
        return new IndexStatusReport(
            ctx.IndexFile,
            exists,
            exists ? new FileInfo(ctx.IndexFile).Length : 0,
            exists ? ctx.Db.UserVersion : 0,
            IndexDatabase.CurrentSchemaVersion,
            exists ? ctx.Db.CountNotes() : 0,
            exists ? ctx.Db.CountFtsRows() : 0,
            state?.LastScanUtc,
            exists && ctx.Db.NeedsRescan);
    }

    public IndexVerifyReport Verify()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var issues = new List<IndexIssue>();

        if (!ctx.IndexExists)
        {
            issues.Add(new(ErrorCodes.IndexStale, "Index does not exist yet — run 'scan' to build it."));
            stopwatch.Stop();
            return new IndexVerifyReport(false, issues, stopwatch.ElapsedMilliseconds);
        }

        lock (ctx.Sync)
        {
            var db = ctx.Db;
            if (db.UserVersion != IndexDatabase.CurrentSchemaVersion)
                issues.Add(new("schema-version-mismatch",
                    $"Index schema is v{db.UserVersion}, this build expects v{IndexDatabase.CurrentSchemaVersion}."));

            var indexed = db.GetFileStates();
            var onDisk = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var abs in VaultFiles.EnumerateMarkdown(ctx.VaultRoot))
                onDisk[PathGuard.ToRelative(ctx.VaultRoot, abs)] = new FileInfo(abs);

            foreach (var (path, state) in indexed)
            {
                if (!onDisk.TryGetValue(path, out var file))
                {
                    issues.Add(new("deleted-file-indexed",
                        "Indexed note no longer exists on disk", path));
                    continue;
                }
                if (file.LastWriteTimeUtc.Ticks != state.ModifiedTicks || file.Length != state.Size)
                    issues.Add(new("stale-file-state",
                        "File changed on disk since it was indexed", path));

                var firstSegment = path.Split('/', 2)[0];
                if (VaultFiles.IsSkippedFolder(firstSegment) || VaultFiles.IsConflictFile(Path.GetFileName(path)))
                    issues.Add(new("bad-path-indexed",
                        "Indexed path is in an operational folder or is a conflict file", path));
            }

            foreach (var path in onDisk.Keys.Where(p => !indexed.ContainsKey(p)))
                issues.Add(new("file-not-indexed", "Note on disk is missing from the index", path));

            var noteCount = db.CountNotes();
            var ftsCount = db.CountFtsRows();
            if (noteCount != ftsCount)
                issues.Add(new("fts-count-mismatch",
                    $"notes table has {noteCount} rows but the FTS table has {ftsCount} — search coverage is wrong."));
        }

        stopwatch.Stop();
        return new IndexVerifyReport(issues.Count == 0, issues, stopwatch.ElapsedMilliseconds);
    }
}
