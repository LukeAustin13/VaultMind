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
        var errors = new List<string>();
        var verifyHash = ctx.Config.VerifyContentHash;

        foreach (var abs in VaultFiles.EnumerateMarkdown(ctx.VaultRoot))
        {
            ct.ThrowIfCancellationRequested();
            var rel = PathGuard.ToRelative(ctx.VaultRoot, abs);
            seen.Add(rel);
            var info = new FileInfo(abs);
            if (known.TryGetValue(rel, out var state) &&
                state.ModifiedTicks == info.LastWriteTimeUtc.Ticks && state.Size == info.Length)
            {
                if (!verifyHash)
                {
                    unchanged++;
                    continue;
                }
                try
                {
                    if (NoteParser.ComputeBodyHash(File.ReadAllText(abs)) == state.BodyHash)
                    {
                        unchanged++;
                        continue;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{rel}: {ex.Message}");
                    continue;
                }
            }
            try
            {
                db.UpsertNote(NoteParser.ParseFile(ctx.VaultRoot, abs));
                if (known.ContainsKey(rel)) updated++;
                else added++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"{rel}: {ex.Message}");
            }
        }

        var removed = 0;
        foreach (var path in known.Keys.Where(p => !seen.Contains(p)))
        {
            db.DeleteNoteByPath(path);
            removed++;
        }

        ctx.State.Save(new VaultState { LastScanUtc = DateTime.UtcNow, NoteCount = db.CountNotes() });
        return new ScanResult(added, updated, removed, unchanged, errors);
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
