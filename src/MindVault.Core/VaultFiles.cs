namespace MindVault.Core;

/// <summary>Enumerates the Markdown files that belong to the vault, skipping operational folders.</summary>
public static class VaultFiles
{
    private static readonly HashSet<string> SkippedFolderNames =
        new(["node_modules", "bin", "obj"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// True when a folder must not be indexed: any dot-folder (.obsidian, .trash, .git,
    /// .mindvault, ...) or a known build/dependency folder.
    /// </summary>
    public static bool IsSkippedFolder(string folderName) =>
        folderName.StartsWith('.') || SkippedFolderNames.Contains(folderName);

    /// <summary>
    /// True for sync-conflict copies that must never be indexed as real notes:
    /// Syncthing ("Note.sync-conflict-20260704-123456-ABC.md") and Dropbox-style
    /// ("Note (someone's conflicted copy 2026-07-04).md"). Validation surfaces them
    /// so the human resolves the conflict in Obsidian; MindVault only ignores them.
    /// </summary>
    public static bool IsConflictFile(string fileName) =>
        fileName.Contains(".sync-conflict-", StringComparison.OrdinalIgnoreCase) ||
        fileName.Contains("conflicted copy", StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<string> EnumerateMarkdown(string vaultRoot)
    {
        foreach (var file in EnumerateAllMarkdown(vaultRoot))
        {
            if (!IsConflictFile(Path.GetFileName(file)))
                yield return file;
        }
    }

    /// <summary>
    /// Same as <see cref="EnumerateMarkdown"/> but yields FileInfo objects whose metadata was
    /// populated by the directory enumeration itself — the scanner needs size+mtime for every
    /// file, and this avoids a second stat() per note.
    /// </summary>
    public static IEnumerable<FileInfo> EnumerateMarkdownFiles(string vaultRoot)
    {
        var pending = new Stack<DirectoryInfo>();
        pending.Push(new DirectoryInfo(Path.GetFullPath(vaultRoot)));
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var sub in dir.EnumerateDirectories())
            {
                if (!IsSkippedFolder(sub.Name))
                    pending.Push(sub);
            }
            foreach (var file in dir.EnumerateFiles("*.md"))
            {
                if (!IsConflictFile(file.Name))
                    yield return file;
            }
        }
    }

    /// <summary>Conflict copies present in the vault (indexable folders only) — for diagnostics.</summary>
    public static IEnumerable<string> EnumerateConflictMarkdown(string vaultRoot)
    {
        foreach (var file in EnumerateAllMarkdown(vaultRoot))
        {
            if (IsConflictFile(Path.GetFileName(file)))
                yield return file;
        }
    }

    private static IEnumerable<string> EnumerateAllMarkdown(string vaultRoot)
    {
        var pending = new Stack<string>();
        pending.Push(Path.GetFullPath(vaultRoot));
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (!IsSkippedFolder(Path.GetFileName(sub)))
                    pending.Push(sub);
            }
            foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
                yield return file;
        }
    }
}
