using System.Text.Json;

namespace MindVault.Core;

public sealed class VaultState
{
    public DateTime? LastScanUtc { get; set; }
    public int NoteCount { get; set; }
}

/// <summary>Reads and writes .mindvault/state.json (operational metadata only, never canonical).</summary>
public sealed class StateStore(string mindVaultDir)
{
    private string FilePath => Path.Combine(mindVaultDir, "state.json");

    public VaultState? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try
        {
            return JsonSerializer.Deserialize<VaultState>(File.ReadAllText(FilePath), Json.Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void Save(VaultState state)
    {
        Directory.CreateDirectory(mindVaultDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(state, Json.Options));
    }
}
