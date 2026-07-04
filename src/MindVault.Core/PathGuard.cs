namespace MindVault.Core;

/// <summary>Central gate for turning note paths into absolute paths that are guaranteed to stay inside the vault.</summary>
public static class PathGuard
{
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Resolves <paramref name="path"/> (relative to the vault root, or absolute) and throws
    /// <see cref="UnsafePathException"/> if the result escapes the vault.
    /// </summary>
    public static string ResolveInsideVault(string vaultRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new UnsafePathException("Empty path.");

        var full = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(vaultRoot, path));

        var root = Path.GetFullPath(vaultRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.Equals(full, root, PathComparison) &&
            !full.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
        {
            throw new UnsafePathException($"Path escapes the vault: {path}");
        }
        return full;
    }

    /// <summary>
    /// Same as <see cref="ResolveInsideVault"/> but additionally rejects any path whose first
    /// segment is a skipped folder (dot-folders like .mindvault/.obsidian/.git/.trash and
    /// build/dependency folders), so only files the scanner would index are reachable.
    /// </summary>
    public static string ResolveNotePath(string vaultRoot, string path)
    {
        var full = ResolveInsideVault(vaultRoot, path);
        var rel = ToRelative(vaultRoot, full);
        var firstSegment = rel.Split('/', 2)[0];
        if (VaultFiles.IsSkippedFolder(firstSegment))
            throw new UnsafePathException($"Notes cannot be written into the operational folder: {rel}");
        return full;
    }

    /// <summary>Vault-relative path with forward slashes (the canonical index form).</summary>
    public static string ToRelative(string vaultRoot, string fullPath) =>
        Path.GetRelativePath(vaultRoot, fullPath).Replace('\\', '/');
}
