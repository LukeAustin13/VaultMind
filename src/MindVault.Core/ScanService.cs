namespace MindVault.Core;

public sealed record ScanResult(int Added, int Updated, int Removed, int Unchanged, IReadOnlyList<string> Errors);

public sealed class ScanService(VaultContext ctx)
{
    /// <summary>
    /// Incremental scan: only files whose modified time or size changed are re-parsed.
    /// With <paramref name="full"/> the index is cleared and everything is re-read.
    /// Change detection is mtime+size by default; a same-size, mtime-preserving edit (e.g. a
    /// git restore) is only detected when <c>verifyContentHash</c> is enabled in config.
    /// </summary>
    public ScanResult Scan(bool full = false, CancellationToken ct = default)
    {
        lock (ctx.Sync)
        {
            return ScanCore(full, ct);
        }
    }

    private ScanResult ScanCore(bool full, CancellationToken ct)
    {
        var db = ctx.Db;
        if (full) db.Clear();

        var known = db.GetFileStates();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int added = 0, updated = 0, unchanged = 0;
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        var verifyHash = ctx.Config.VerifyContentHash;

        // Cheap metadata pass first (single stat per file via the enumeration), so only files
        // that actually need reading/parsing reach the expensive stage.
        var toProcess = new List<(string Abs, string Rel, bool NeedsHashCheck, FileState? State)>();
        foreach (var file in VaultFiles.EnumerateMarkdownFiles(ctx.VaultRoot))
        {
            ct.ThrowIfCancellationRequested();
            var rel = PathGuard.ToRelative(ctx.VaultRoot, file.FullName);
            seen.Add(rel);
            if (known.TryGetValue(rel, out var state) &&
                state.ModifiedTicks == file.LastWriteTimeUtc.Ticks && state.Size == file.Length)
            {
                if (!verifyHash) { unchanged++; continue; }
                toProcess.Add((file.FullName, rel, true, state));
            }
            else
            {
                toProcess.Add((file.FullName, rel, false, null));
            }
        }

        // Parse in parallel (Markdig + YAML + SHA-256 dominate cold scans; a Pi has 4 cores to
        // use), upsert into ONE bulk transaction (one commit instead of one fsync per note).
        // Per-note DB writes stay atomic: UpsertNote holds the DB lock for the whole note.
        var removedPaths = known.Keys.Where(p => !seen.Contains(p)).ToList();
        if (toProcess.Count > 0 || removedPaths.Count > 0)
        {
            using var bulk = db.BeginBulk();
            var options = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 8),
            };
            Parallel.ForEach(toProcess, options, item =>
            {
                try
                {
                    if (item.NeedsHashCheck &&
                        NoteParser.ComputeBodyHash(File.ReadAllText(item.Abs)) == item.State!.BodyHash)
                    {
                        Interlocked.Increment(ref unchanged);
                        return;
                    }
                    db.UpsertNote(NoteParser.ParseFile(ctx.VaultRoot, item.Abs));
                    if (known.ContainsKey(item.Rel)) Interlocked.Increment(ref updated);
                    else Interlocked.Increment(ref added);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Enqueue($"{item.Rel}: {ex.Message}");
                }
            });

            foreach (var path in removedPaths)
                db.DeleteNoteByPath(path);
        }

        ctx.State.Save(new VaultState { LastScanUtc = DateTime.UtcNow, NoteCount = db.CountNotes() });
        return new ScanResult(added, updated, removedPaths.Count, unchanged, [.. errors]);
    }

    /// <summary>
    /// Makes the index usable before a query: builds it if missing, repopulates it after a
    /// schema reset, and runs an incremental refresh when the last scan is older than the
    /// configured staleness window (0 disables auto-refresh). Never a full re-read: staleness
    /// refresh only re-parses files whose modified time or size changed (unless
    /// <c>verifyContentHash</c> is enabled — see <see cref="Scan"/>).
    /// </summary>
    public void EnsureFresh()
    {
        lock (ctx.Sync)
        {
            if (!ctx.IndexExists)
            {
                ScanCore(full: false, CancellationToken.None);
                return;
            }
            var db = ctx.Db;
            if (db.NeedsRescan)
            {
                ScanCore(full: false, CancellationToken.None);
                db.MarkRescanned();
                return;
            }
            var ttl = ctx.Config.ScanStalenessSeconds;
            if (ttl <= 0) return;
            var last = ctx.State.Load()?.LastScanUtc;
            if (last is null || (DateTime.UtcNow - last.Value).TotalSeconds > ttl)
                ScanCore(full: false, CancellationToken.None);
        }
    }

    /// <summary>Re-parses and re-indexes a single file (used after every write).</summary>
    public NoteSummary IndexFile(string absolutePath)
    {
        var parsed = NoteParser.ParseFile(ctx.VaultRoot, absolutePath);
        ctx.Db.UpsertNote(parsed);
        return ctx.Db.FindByPath(parsed.RelativePath)
               ?? throw new MindVaultException($"Failed to index {parsed.RelativePath}.");
    }

    public void RemoveFromIndex(string relativePath) => ctx.Db.DeleteNoteByPath(relativePath);
}
