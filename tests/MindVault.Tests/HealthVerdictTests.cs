using MindVault.Cli;
using MindVault.Core;
using MindVault.Mcp;

namespace MindVault.Tests;

/// <summary>One verdict to branch on: good / warning / critical.</summary>
public sealed class HealthVerdictTests
{
    [Fact]
    public void MessyFixtureVaultIsWarningNotCritical()
    {
        using var tv = new TempVault();
        var r = tv.Ctx.Doctor.Run(_ => null);
        Assert.Equal("warning", r.Verdict);
        Assert.True(r.ValidationCriticalCount > 0);
        Assert.Contains(r.VerdictReasons!, reason => reason.Contains("validation"));
    }

    [Fact]
    public void CleanVaultIsGood()
    {
        using var tv = new TempVault(useFixture: false);
        var r = tv.Ctx.Doctor.Run(_ => null);
        Assert.Equal("good", r.Verdict);
        Assert.Empty(r.VerdictReasons!);
    }

    [Fact]
    public void PlaceholderLookingVaultPathIsCritical()
    {
        var root = Path.Combine(Path.GetTempPath(), "mindvault-tests",
            Guid.NewGuid().ToString("N"), "YourObsidianVault");
        Directory.CreateDirectory(root);
        try
        {
            VaultStructure.EnsureStructure(root);
            using var ctx = TempVault.CreateContextFor(root);
            var r = ctx.Doctor.Run(_ => null);
            Assert.Equal("critical", r.Verdict);
            Assert.Contains(r.VerdictReasons!, reason => reason.Contains("placeholder"));
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(root)!, recursive: true); }
            catch (IOException) { /* best effort */ }
        }
    }

    [Fact]
    public void CliDoctorLeadsWithTheVerdict()
    {
        using var tv = new TempVault();
        var stdout = new StringWriter();
        var code = CliRunner.Run(["doctor", "--vault", tv.Root], stdout, new StringWriter(), _ => null, tv.Root);
        Assert.Equal(0, code);
        Assert.Contains("health:", stdout.ToString());
        Assert.Contains("WARNING", stdout.ToString());
    }

    [Fact]
    public void McpHealthCarriesAVerdict()
    {
        using var tv = new TempVault();
        var tools = new MindVaultTools(tv.Ctx);
        var json = tools.Health();
        Assert.Contains("\"verdict\":\"good\"", json);
    }

    [Fact]
    public void McpCreateDuplicateReturnsStructuredRefusal()
    {
        using var tv = new TempVault();
        var tools = new MindVaultTools(tv.Ctx);
        var json = tools.CreateTask("Alpha", "Ship the v1");
        Assert.Contains("\"created\":false", json);
        Assert.Contains("possible_duplicate", json);
        Assert.Contains(ErrorCodes.DuplicateSuspected, json);
        // Override creates it.
        var created = tools.CreateTask("Alpha", "Ship the v1", allowDuplicate: true);
        Assert.Contains("01_Projects/Task - Ship the v1.md", created);
    }
}
