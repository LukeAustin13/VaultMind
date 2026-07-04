using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Doctor/status diagnostics, version identity and stable error codes.</summary>
public sealed class ConfigDiagnosticsTests
{
    [Fact]
    public void DoctorReportsEnvironmentAndVersions()
    {
        using var tv = new TempVault();
        var r = tv.Ctx.Doctor.Run(_ => null);
        Assert.Equal(MindVaultVersion.Current, r.AppVersion);
        Assert.True(r.VaultExists);
        Assert.True(r.VaultWritable);
        Assert.True(r.SnapshotWritable);
        Assert.Equal(IndexDatabase.CurrentSchemaVersion, r.IndexSchemaVersion);
        Assert.Equal(IndexDatabase.CurrentSchemaVersion, r.ExpectedSchemaVersion);
        Assert.False(r.LocalConfigFound); // TempVault configures via --vault
        Assert.False(string.IsNullOrWhiteSpace(r.User));
        Assert.DoesNotContain(r.Warnings, w => w.Contains("placeholder"));
    }

    [Fact]
    public void PlaceholderVaultPathsAreDetected()
    {
        Assert.True(DoctorService.LooksLikePlaceholderPath(@"C:\Path\To\Your\ObsidianVault"));
        Assert.True(DoctorService.LooksLikePlaceholderPath("/home/user/path/to/vault"));
        Assert.True(DoctorService.LooksLikePlaceholderPath(@"D:\vaults\CHANGEME"));
        Assert.False(DoctorService.LooksLikePlaceholderPath(@"C:\Users\Luke\Documents\MyVault"));
    }

    [Fact]
    public void McpEnvIsReportedAsPresenceOnlyNeverTheTokenValue()
    {
        using var tv = new TempVault();
        var r = tv.Ctx.Doctor.Run(key => key switch
        {
            McpEnv.AuthToken => "super-secret-token-value",
            McpEnv.Transport => "http",
            McpEnv.Port => "7777",
            _ => null,
        });
        Assert.True(r.McpEnvironment.AuthTokenSet);
        Assert.Equal("http", r.McpEnvironment.Transport);
        Assert.DoesNotContain("super-secret-token-value", Json.Serialize(r));
    }

    [Fact]
    public void AnonymousMcpEnvProducesAWarning()
    {
        using var tv = new TempVault();
        var r = tv.Ctx.Doctor.Run(key => key == McpEnv.AllowAnonymous ? "true" : null);
        Assert.True(r.McpEnvironment.AllowAnonymous);
        Assert.Contains(r.Warnings, w => w.Contains("anonymous", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void VersionCommandPrintsAppAndSchemaVersion()
    {
        var stdout = new StringWriter();
        Assert.Equal(0, MindVault.Cli.CliRunner.Run(["--version"], stdout, new StringWriter(), _ => null, Path.GetTempPath()));
        Assert.Contains(MindVaultVersion.Current, stdout.ToString());

        stdout = new StringWriter();
        Assert.Equal(0, MindVault.Cli.CliRunner.Run(["version", "--json"], stdout, new StringWriter(), _ => null, Path.GetTempPath()));
        Assert.Contains($"\"version\":\"{MindVaultVersion.Current}\"", stdout.ToString());
        Assert.Contains($"\"indexSchemaVersion\":{IndexDatabase.CurrentSchemaVersion}", stdout.ToString());
    }

    [Fact]
    public void StatusJsonCarriesVersionAndPlaceholderFlag()
    {
        using var tv = new TempVault();
        var stdout = new StringWriter();
        var exit = MindVault.Cli.CliRunner.Run(["status", "--vault", tv.Root, "--json"],
            stdout, new StringWriter(), _ => null, Path.GetTempPath());
        Assert.Equal(0, exit);
        Assert.Contains($"\"version\":\"{MindVaultVersion.Current}\"", stdout.ToString());
        Assert.Contains("\"vaultPathLooksLikePlaceholder\":false", stdout.ToString());
    }

    [Fact]
    public void JsonErrorsCarryStableCodes()
    {
        using var tv = new TempVault();
        var stdout = new StringWriter();
        var exit = MindVault.Cli.CliRunner.Run(["read", "No Such Note Anywhere", "--vault", tv.Root, "--json"],
            stdout, new StringWriter(), _ => null, Path.GetTempPath());
        Assert.Equal(2, exit);
        Assert.Contains("\"code\":\"NOTE_NOT_FOUND\"", stdout.ToString());

        stdout = new StringWriter();
        exit = MindVault.Cli.CliRunner.Run(["read", "Duplicate Note", "--vault", tv.Root, "--json"],
            stdout, new StringWriter(), _ => null, Path.GetTempPath());
        Assert.Equal(3, exit);
        Assert.Contains("\"code\":\"NOTE_REF_AMBIGUOUS\"", stdout.ToString());

        stdout = new StringWriter();
        exit = MindVault.Cli.CliRunner.Run(["status", "--vault", Path.Combine(tv.Root, "no-such-dir"), "--json"],
            stdout, new StringWriter(), _ => null, Path.GetTempPath());
        Assert.Equal(2, exit);
        Assert.Contains("\"code\":\"VAULT_NOT_FOUND\"", stdout.ToString());
    }

    [Fact]
    public void QuietSuppressesMutationChatterButNotResults()
    {
        using var tv = new TempVault();
        var stdout = new StringWriter();
        var exit = MindVault.Cli.CliRunner.Run(
            ["create", "project", "Quiet Project", "--vault", tv.Root, "--quiet"],
            stdout, new StringWriter(), _ => null, Path.GetTempPath());
        Assert.Equal(0, exit);
        Assert.Equal("", stdout.ToString());

        stdout = new StringWriter();
        exit = MindVault.Cli.CliRunner.Run(["search", "SQLite", "--vault", tv.Root, "--quiet"],
            stdout, new StringWriter(), _ => null, Path.GetTempPath());
        Assert.Equal(0, exit);
        Assert.Contains("Decision", stdout.ToString()); // query results still print
    }

    [Fact]
    public void VerboseWritesTimingToStderrOnly()
    {
        using var tv = new TempVault();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = MindVault.Cli.CliRunner.Run(["list", "--vault", tv.Root, "--verbose"],
            stdout, stderr, _ => null, Path.GetTempPath());
        Assert.Equal(0, exit);
        Assert.Contains("[verbose] list completed in", stderr.ToString());
        Assert.DoesNotContain("[verbose]", stdout.ToString());
    }
}
