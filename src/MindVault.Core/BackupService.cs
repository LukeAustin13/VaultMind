using System.IO.Compression;

namespace MindVault.Core;

public sealed record BackupResult(string ZipPath, int FileCount);

/// <summary>Zips every Markdown file in the vault into .mindvault/backups (operational folders excluded).</summary>
public sealed class BackupService(VaultContext ctx)
{
    public BackupResult Run()
    {
        Directory.CreateDirectory(ctx.BackupDir);
        var zipPath = Path.Combine(ctx.BackupDir, $"vault-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        var suffix = 1;
        while (File.Exists(zipPath))
            zipPath = Path.Combine(ctx.BackupDir, $"vault-backup-{DateTime.Now:yyyyMMdd-HHmmss}-{suffix++}.zip");

        var count = 0;
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            foreach (var abs in VaultFiles.EnumerateMarkdown(ctx.VaultRoot))
            {
                zip.CreateEntryFromFile(abs, PathGuard.ToRelative(ctx.VaultRoot, abs));
                count++;
            }
        }
        return new BackupResult(zipPath, count);
    }
}
