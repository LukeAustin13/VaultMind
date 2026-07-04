using System.Text.RegularExpressions;

namespace MindVault.Core;

/// <summary>Copies a note into .mindvault/snapshots/YYYY-MM-DD/ before any mutation.</summary>
public sealed partial class SnapshotService(VaultContext ctx)
{
    public string Snapshot(string absoluteNotePath)
    {
        if (!File.Exists(absoluteNotePath))
            throw new MindVaultException($"Cannot snapshot missing file: {absoluteNotePath}",
                ErrorCodes.SnapshotFailed);

        var now = DateTime.Now;
        var dir = Path.Combine(ctx.SnapshotDir, now.ToString("yyyy-MM-dd"));
        Directory.CreateDirectory(dir);

        var safeName = SlugHelper.SanitizeFileName(Path.GetFileNameWithoutExtension(absoluteNotePath));
        var stamp = now.ToString("yyyyMMdd-HHmmssfff");
        var target = Path.Combine(dir, $"{stamp}-{safeName}.md");
        var suffix = 1;
        while (File.Exists(target))
            target = Path.Combine(dir, $"{stamp}-{safeName}-{suffix++}.md");

        File.Copy(absoluteNotePath, target);
        return target;
    }

    /// <summary>
    /// All snapshots of a note (matched by its sanitized file name), newest first.
    /// Snapshot names are "{yyyyMMdd-HHmmssfff}-{name}[-{n}].md".
    /// </summary>
    public List<string> ListSnapshots(string noteStem)
    {
        if (!Directory.Exists(ctx.SnapshotDir)) return [];
        var safeName = SlugHelper.SanitizeFileName(noteStem);
        // Match only "{name}" (exact) or "{name}-{n}" (numeric same-millisecond dedup suffix), so
        // a sibling note like "Foo-bar" is never treated as a snapshot of "Foo".
        var suffixPattern = new Regex($@"^{Regex.Escape(safeName)}-(\d+)$", RegexOptions.IgnoreCase);
        var matches = new List<(string File, string Stamp, int Dedup)>();
        foreach (var dir in Directory.EnumerateDirectories(ctx.SnapshotDir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
            {
                var stem = Path.GetFileNameWithoutExtension(file);
                if (stem.Length < 20 || stem[8] != '-' || stem[18] != '-') continue;
                var name = stem[19..];
                if (string.Equals(name, safeName, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add((file, stem[..18], 0));
                }
                else if (suffixPattern.Match(name) is { Success: true } m)
                {
                    matches.Add((file, stem[..18], int.Parse(m.Groups[1].Value)));
                }
            }
        }
        return matches
            .OrderByDescending(x => x.Stamp, StringComparer.Ordinal)
            .ThenByDescending(x => x.Dedup)
            .Select(x => x.File)
            .ToList();
    }

    /// <summary>Deletes snapshot day-folders older than the retention window. Returns files removed.</summary>
    public int Prune(int retentionDays)
    {
        if (retentionDays < 1)
            throw new MindVaultException("Snapshot retention must be at least 1 day.");
        if (!Directory.Exists(ctx.SnapshotDir)) return 0;

        var cutoff = DateTime.Today.AddDays(-retentionDays);
        var removed = 0;
        foreach (var dir in Directory.EnumerateDirectories(ctx.SnapshotDir))
        {
            if (!DateTime.TryParseExact(Path.GetFileName(dir), "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var day) || day >= cutoff)
            {
                continue;
            }
            removed += Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(dir, recursive: true);
        }
        return removed;
    }
}
