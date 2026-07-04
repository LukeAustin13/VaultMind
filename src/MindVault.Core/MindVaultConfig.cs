namespace MindVault.Core;

public sealed record MindVaultConfig
{
    public string? VaultPath { get; init; }
    public string? VaultName { get; init; }
    public string IndexPath { get; init; } = ".mindvault/index.sqlite";
    public string SnapshotPath { get; init; } = ".mindvault/snapshots";
    public bool EnableWatcher { get; init; }
    public string DefaultArchiveFolder { get; init; } = "99_Archive";

    /// <summary>
    /// Query commands run an incremental rescan when the index is older than this.
    /// 0 disables auto-refresh (only `scan` and MindVault's own writes update the index).
    /// </summary>
    public int ScanStalenessSeconds { get; init; } = 60;

    /// <summary>
    /// When true, a file whose modified time and size match the index is additionally verified
    /// by re-hashing its content, so a same-size mtime-preserving edit (e.g. a git restore) is
    /// still detected. Off by default because hashing every candidate is expensive on low-power
    /// hardware (Raspberry Pi target); the default fast path stays mtime+size only.
    /// </summary>
    public bool VerifyContentHash { get; init; }

    /// <summary>Default retention used by the `prune` command for snapshot files.</summary>
    public int SnapshotRetentionDays { get; init; } = 30;

    /// <summary>
    /// A .mindvault/write.lock older than this is treated as abandoned (crashed holder) and
    /// taken over. Normal mutations hold the lock for well under a second.
    /// </summary>
    public int WriteLockStaleSeconds { get; init; } = 600;
}

/// <summary>A resolved configuration plus where the vault path came from.</summary>
public sealed record LoadedConfig(MindVaultConfig Config, string VaultPathSource, string? ConfigFilePath);
