using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Index drift detection: the verifier must catch every way the cache can lie.</summary>
public sealed class IndexVerifyTests
{
    [Fact]
    public void StatusReportsSchemaCountsAndSize()
    {
        using var tv = new TempVault();
        var s = tv.Ctx.IndexCheck.Status();
        Assert.True(s.IndexExists);
        Assert.True(s.IndexSizeBytes > 0);
        Assert.Equal(IndexDatabase.CurrentSchemaVersion, s.SchemaVersion);
        Assert.Equal(IndexDatabase.CurrentSchemaVersion, s.ExpectedSchemaVersion);
        Assert.Equal(26, s.NoteCount);
        Assert.Equal(s.NoteCount, s.FtsRowCount);
        Assert.False(s.RescanPending);
    }

    [Fact]
    public void CleanVaultVerifiesOk()
    {
        using var tv = new TempVault();
        var report = tv.Ctx.IndexCheck.Verify();
        Assert.True(report.Ok, string.Join(" | ", report.Issues.Select(i => i.Code)));
        Assert.Null(report.Recommendation);
    }

    [Fact]
    public void DetectsDeletedFileStillIndexed()
    {
        using var tv = new TempVault();
        File.Delete(tv.Abs("03_Resources/SQLite Cheatsheet.md"));
        var report = tv.Ctx.IndexCheck.Verify();
        Assert.False(report.Ok);
        Assert.Contains(report.Issues, i => i.Code == "deleted-file-indexed"
                                            && i.Path == "03_Resources/SQLite Cheatsheet.md");
        Assert.Contains("index rebuild", report.Recommendation);
    }

    [Fact]
    public void DetectsFileOnDiskMissingFromIndex()
    {
        using var tv = new TempVault();
        File.WriteAllText(tv.Abs("00_Inbox/Unindexed.md"), "# Unindexed\n");
        var report = tv.Ctx.IndexCheck.Verify();
        Assert.Contains(report.Issues, i => i.Code == "file-not-indexed" && i.Path == "00_Inbox/Unindexed.md");
    }

    [Fact]
    public void DetectsStaleFileState()
    {
        using var tv = new TempVault();
        var abs = tv.Abs("01_Projects/Alpha.md");
        File.AppendAllText(abs, "\nchanged behind the index's back\n");
        var report = tv.Ctx.IndexCheck.Verify();
        Assert.Contains(report.Issues, i => i.Code == "stale-file-state" && i.Path == "01_Projects/Alpha.md");
    }

    [Fact]
    public void RebuildClearsEveryDriftIssue()
    {
        using var tv = new TempVault();
        File.Delete(tv.Abs("03_Resources/SQLite Cheatsheet.md"));
        File.WriteAllText(tv.Abs("00_Inbox/Unindexed.md"), "# Unindexed\n");
        Assert.False(tv.Ctx.IndexCheck.Verify().Ok);

        tv.Ctx.Scanner.Scan(full: true);
        var after = tv.Ctx.IndexCheck.Verify();
        Assert.True(after.Ok, string.Join(" | ", after.Issues.Select(i => $"{i.Code}:{i.Path}")));
    }

    [Fact]
    public void CliIndexCommandsReportAndRepair()
    {
        using var tv = new TempVault();
        var stdout = new StringWriter();
        var exit = CliRun(["index", "status", "--vault", tv.Root, "--json"], stdout);
        Assert.Equal(0, exit);
        Assert.Contains($"\"schemaVersion\":{IndexDatabase.CurrentSchemaVersion}", stdout.ToString());

        File.Delete(tv.Abs("03_Resources/SQLite Cheatsheet.md"));
        stdout = new StringWriter();
        exit = CliRun(["index", "verify", "--vault", tv.Root, "--json"], stdout);
        Assert.Equal(1, exit);
        Assert.Contains("deleted-file-indexed", stdout.ToString());

        stdout = new StringWriter();
        exit = CliRun(["index", "rebuild", "--vault", tv.Root], stdout);
        Assert.Equal(0, exit);

        stdout = new StringWriter();
        exit = CliRun(["index", "verify", "--vault", tv.Root], stdout);
        Assert.Equal(0, exit);
        Assert.Contains("index verify: ok", stdout.ToString());
    }

    private static int CliRun(string[] args, StringWriter stdout) =>
        MindVault.Cli.CliRunner.Run(args, stdout, new StringWriter(), _ => null,
            Path.GetTempPath());
}
