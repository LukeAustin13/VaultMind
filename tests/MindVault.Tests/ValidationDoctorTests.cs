using MindVault.Core;

namespace MindVault.Tests;

public sealed class ValidationDoctorTests
{
    [Fact]
    public void ValidateReportsAllFixtureProblems()
    {
        using var tv = new TempVault();
        var report = tv.Ctx.Validator.Validate();
        var codes = report.Issues.Select(i => i.Code).ToHashSet();

        Assert.Contains("invalid-yaml", codes);          // 00_Inbox/Bad Yaml.md
        Assert.Contains("nested-yaml", codes);           // 00_Inbox/Nested.md
        Assert.Contains("duplicate-title", codes);       // Duplicate Note x2
        Assert.Contains("broken-link", codes);           // [[Ghost Note]]
        Assert.Contains("missing-project-note", codes);  // project: Phantom
        Assert.Contains("invalid-status", codes);        // status: bogus
        Assert.Contains("outside-structure", codes);     // task in 00_Inbox
        Assert.True(report.ErrorCount >= 5);
        Assert.True(report.WarningCount >= 2);
    }

    [Fact]
    public void ValidateFlagsMissingFoldersWhenNotInitialized()
    {
        using var tv = new TempVault(useFixture: false, init: false, scan: false);
        var report = tv.Ctx.Validator.Validate();
        Assert.Contains(report.Issues, i => i.Code == "missing-folder" && i.Message.Contains("01_Projects"));
    }

    [Fact]
    public void CleanInitializedVaultHasNoErrors()
    {
        using var tv = new TempVault(useFixture: false);
        tv.Ctx.Writer.CreateProject("Solo");
        tv.Ctx.Writer.CreateTask("Solo", "First step");
        var report = tv.Ctx.Validator.Validate();
        Assert.Equal(0, report.ErrorCount);
        Assert.Equal(0, report.WarningCount);
    }

    [Fact]
    public void DoctorReportsSystemHealth()
    {
        using var tv = new TempVault();
        var report = tv.Ctx.Doctor.Run();
        Assert.Equal(tv.Root, report.VaultPath);
        Assert.Equal("cli --vault", report.ConfigSource);
        Assert.True(report.IndexExists);
        Assert.Equal(24, report.NoteCount); // 14 fixture notes + 10 templates
        Assert.True(report.BrokenLinkCount >= 1);
        Assert.True(report.DuplicateTitleCount >= 1);
        Assert.NotNull(report.LastScanUtc);
        Assert.Contains("disabled", report.WatcherStatus);
    }
}
