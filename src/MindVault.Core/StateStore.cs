using System.Text.Json;

namespace MindVault.Core;

public sealed class VaultState
{
    public DateTime? LastScanUtc { get; set; }
    public int NoteCount { get; set; }
}

/// <summary>
/// Reads and writes .mindvault/state.json (operational metadata only, never canonical).
/// Loads are mtime-cached: EnsureFresh consults the state before every query, and in a
/// long-lived MCP server that would otherwise mean read+deserialize per call. A single
/// stat detects external updates (another process scanning), so freshness semantics keep
/// working across processes.
/// </summary>
public sealed class StateStore(string mindVaultDir)
{
    private readonly object _sync = new();
    private VaultState? _cached;
    private DateTime _cachedMtimeUtc;

    private string FilePath => Path.Combine(mindVaultDir, "state.json");

    public VaultState? Load()
    {
        lock (_sync)
        {
            if (!File.Exists(FilePath))
            {
                _cached = null;
                return null;
            }
            var mtime = File.GetLastWriteTimeUtc(FilePath);
            if (_cached is not null && mtime == _cachedMtimeUtc) return _cached;
            try
            {
                _cached = JsonSerializer.Deserialize<VaultState>(File.ReadAllText(FilePath), Json.Options);
                _cachedMtimeUtc = mtime;
                return _cached;
            }
            catch (JsonException)
            {
                _cached = null;
                return null;
            }
        }
    }

    public void Save(VaultState state)
    {
        lock (_sync)
        {
            Directory.CreateDirectory(mindVaultDir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(state, Json.Options));
            _cached = state;
            _cachedMtimeUtc = File.GetLastWriteTimeUtc(FilePath);
        }
    }
}
