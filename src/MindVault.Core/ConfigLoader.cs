using System.Text.Json;

namespace MindVault.Core;

public static class ConfigLoader
{
    public const string EnvVar = "MINDVAULT_VAULT_PATH";
    public const string LocalConfigFileName = "mindvault.config.local.json";

    public const string SetupHelp =
        "No vault path configured.\n" +
        "Configure MindVault one of these ways (highest priority first):\n" +
        "  1. Pass --vault \"C:\\Path\\To\\Vault\" on the command line\n" +
        "  2. Set the MINDVAULT_VAULT_PATH environment variable\n" +
        "  3. Copy config/mindvault.config.example.json to config/mindvault.config.local.json\n" +
        "     and set \"vaultPath\" to your local Obsidian vault folder";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads configuration with priority: CLI --vault, MINDVAULT_VAULT_PATH env var,
    /// config/mindvault.config.local.json (searched upward from <paramref name="startDirectory"/>
    /// and from the application base directory). The example config is never loaded.
    /// </summary>
    public static LoadedConfig Load(string? cliVaultPath = null, Func<string, string?>? getEnv = null, string? startDirectory = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var configFilePath = FindLocalConfig(startDirectory);
        var config = configFilePath is null ? new MindVaultConfig() : ReadConfigFile(configFilePath);

        string vaultPath;
        string source;
        if (!string.IsNullOrWhiteSpace(cliVaultPath))
        {
            vaultPath = Path.GetFullPath(cliVaultPath);
            source = "cli --vault";
        }
        else if (getEnv(EnvVar) is { } env && !string.IsNullOrWhiteSpace(env))
        {
            vaultPath = Path.GetFullPath(env);
            source = $"env {EnvVar}";
        }
        else if (!string.IsNullOrWhiteSpace(config.VaultPath))
        {
            // A relative vaultPath in the config file is resolved against the repo root
            // (the directory that contains the config/ folder).
            vaultPath = Path.IsPathRooted(config.VaultPath)
                ? Path.GetFullPath(config.VaultPath)
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(configFilePath!))!, config.VaultPath));
            source = configFilePath!;
        }
        else
        {
            throw new MindVaultConfigException(SetupHelp);
        }

        return new LoadedConfig(config with { VaultPath = vaultPath }, source, configFilePath);
    }

    private static MindVaultConfig ReadConfigFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MindVaultConfig>(text, JsonOptions)
                   ?? throw new MindVaultConfigException($"Config file is empty or not a JSON object: {path}",
                       ErrorCodes.ConfigInvalid);
        }
        catch (JsonException ex)
        {
            throw new MindVaultConfigException($"Config file is not valid JSON: {path}\n{ex.Message}",
                ErrorCodes.ConfigInvalid);
        }
    }

    private static string? FindLocalConfig(string? startDirectory)
    {
        // An explicit start directory is the only search root (keeps tests hermetic);
        // otherwise search from the working directory and the executable location.
        var starts = new List<string>();
        if (!string.IsNullOrWhiteSpace(startDirectory)) starts.Add(startDirectory);
        else
        {
            starts.Add(Environment.CurrentDirectory);
            starts.Add(AppContext.BaseDirectory);
        }

        foreach (var start in starts)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(start));
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "config", LocalConfigFileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
        }
        return null;
    }
}
