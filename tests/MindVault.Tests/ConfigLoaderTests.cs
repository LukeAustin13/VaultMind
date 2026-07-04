using MindVault.Core;

namespace MindVault.Tests;

public sealed class ConfigLoaderTests : IDisposable
{
    private readonly string _repoDir;

    public ConfigLoaderTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_repoDir, "config"));
    }

    private string WriteLocalConfig(string vaultPath)
    {
        var path = Path.Combine(_repoDir, "config", ConfigLoader.LocalConfigFileName);
        File.WriteAllText(path, $$"""{ "vaultPath": {{System.Text.Json.JsonSerializer.Serialize(vaultPath)}} }""");
        return path;
    }

    [Fact]
    public void CliArgumentWinsOverEnvAndFile()
    {
        WriteLocalConfig(@"C:\FromFile");
        var loaded = ConfigLoader.Load(@"C:\FromCli", _ => @"C:\FromEnv", _repoDir);
        Assert.Equal(@"C:\FromCli", loaded.Config.VaultPath);
        Assert.Equal("cli --vault", loaded.VaultPathSource);
    }

    [Fact]
    public void EnvVarBeatsConfigFile()
    {
        WriteLocalConfig(@"C:\FromFile");
        var loaded = ConfigLoader.Load(null, name => name == ConfigLoader.EnvVar ? @"C:\FromEnv" : null, _repoDir);
        Assert.Equal(@"C:\FromEnv", loaded.Config.VaultPath);
        Assert.Contains(ConfigLoader.EnvVar, loaded.VaultPathSource);
    }

    [Fact]
    public void ConfigFileUsedWhenNoOverrides()
    {
        var configPath = WriteLocalConfig(@"C:\FromFile");
        var loaded = ConfigLoader.Load(null, _ => null, _repoDir);
        Assert.Equal(@"C:\FromFile", loaded.Config.VaultPath);
        Assert.Equal(configPath, loaded.VaultPathSource);
    }

    [Fact]
    public void RelativeVaultPathResolvesAgainstRepoRoot()
    {
        WriteLocalConfig("MyVault");
        var loaded = ConfigLoader.Load(null, _ => null, _repoDir);
        Assert.Equal(Path.Combine(_repoDir, "MyVault"), loaded.Config.VaultPath);
    }

    [Fact]
    public void ThrowsClearErrorWhenNothingConfigured()
    {
        var ex = Assert.Throws<MindVaultConfigException>(() => ConfigLoader.Load(null, _ => null, _repoDir));
        Assert.Contains("mindvault.config.local.json", ex.Message);
        Assert.Contains(ConfigLoader.EnvVar, ex.Message);
    }

    [Fact]
    public void ExampleConfigIsNeverLoadedImplicitly()
    {
        File.WriteAllText(Path.Combine(_repoDir, "config", "mindvault.config.example.json"),
            """{ "vaultPath": "C:\\FromExample" }""");
        Assert.Throws<MindVaultConfigException>(() => ConfigLoader.Load(null, _ => null, _repoDir));
    }

    [Fact]
    public void ConfigFileIsFoundFromNestedDirectory()
    {
        WriteLocalConfig(@"C:\FromFile");
        var nested = Path.Combine(_repoDir, "src", "deep");
        Directory.CreateDirectory(nested);
        var loaded = ConfigLoader.Load(null, _ => null, nested);
        Assert.Equal(@"C:\FromFile", loaded.Config.VaultPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_repoDir, recursive: true); } catch (IOException) { }
    }
}
