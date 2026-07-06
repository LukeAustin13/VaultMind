using System.Text.Json;
using MindVault.Cli;

namespace MindVault.Tests;

public sealed class CliTests : IDisposable
{
    private readonly TempVault _tv = new();

    private (int Code, string Stdout, string Stderr) RunCli(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var argv = args.Concat(["--vault", _tv.Root]).ToArray();
        var code = CliRunner.Run(argv, stdout, stderr, _ => null, _tv.Root);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void StatusCommandWorks()
    {
        var (code, stdout, _) = RunCli("status");
        Assert.Equal(0, code);
        Assert.Contains(_tv.Root, stdout);
        Assert.Contains("26 notes", stdout);
    }

    [Fact]
    public void StatusJsonIsParseable()
    {
        var (code, stdout, _) = RunCli("status", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(26, doc.RootElement.GetProperty("noteCount").GetInt32());
    }

    [Fact]
    public void StatusFailsClearlyWithoutAnyConfiguration()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(["status"], stdout, stderr, _ => null, emptyDir);
        Assert.Equal(2, code);
        Assert.Contains("No vault path configured", stderr.ToString());
        Directory.Delete(emptyDir, recursive: true);
    }

    [Fact]
    public void ScanSearchReadFlow()
    {
        Assert.Equal(0, RunCli("scan").Code);

        var (code, stdout, _) = RunCli("search", "SQLite", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("count").GetInt32() > 0);

        var read = RunCli("read", "Alpha");
        Assert.Equal(0, read.Code);
        Assert.Contains("# Alpha", read.Stdout);
    }

    [Fact]
    public void CreateAppendUpdateArchiveFlow()
    {
        Assert.Equal(0, RunCli("create", "project", "Gamma").Code);
        Assert.Equal(0, RunCli("create", "task", "--project", "Gamma", "--title", "Try it").Code);
        Assert.Equal(0, RunCli("create", "decision", "--project", "Gamma", "--title", "Go live").Code);
        Assert.Equal(0, RunCli("append", "--note", "Task - Try it", "--section", "Status Notes", "--content", "hello world").Code);
        Assert.Equal(0, RunCli("update-frontmatter", "--note", "Task - Try it", "--key", "status", "--value", "done").Code);

        var archive = RunCli("archive", "Task - Try it");
        Assert.Equal(0, archive.Code);
        Assert.True(File.Exists(_tv.Abs("99_Archive/Task - Try it.md")));
        Assert.Contains("status: archived", _tv.ReadNote("99_Archive/Task - Try it.md"));
    }

    [Fact]
    public void AppendFromContentFile()
    {
        var contentFile = Path.Combine(_tv.Root, "input.txt");
        File.WriteAllText(contentFile, "- from a file");
        var (code, _, _) = RunCli("append", "--note", "Alpha", "--section", "Next Actions", "--content-file", contentFile);
        Assert.Equal(0, code);
        Assert.Contains("- from a file", _tv.ReadNote("01_Projects/Alpha.md"));
    }

    [Fact]
    public void ListFiltersByTypeAsJson()
    {
        var (code, stdout, _) = RunCli("list", "--type", "decision", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        foreach (var note in doc.RootElement.GetProperty("notes").EnumerateArray())
            Assert.Equal("decision", note.GetProperty("type").GetString());
    }

    [Fact]
    public void LinkCommandWorks()
    {
        var (code, stdout, _) = RunCli("link", "--from", "Scratch", "--to", "Alpha");
        Assert.Equal(0, code);
        Assert.Contains("Linked", stdout);
    }

    [Fact]
    public void SessionStartPrintsTheBrief()
    {
        var (code, stdout, _) = RunCli("session", "start", "--project", "Alpha", "--task", "cli check");
        Assert.Equal(0, code);
        Assert.Contains("Session Brief — Alpha", stdout);
        Assert.Contains("Goal:", stdout);
        Assert.Contains("Decisions in force:", stdout);
        Assert.Contains("Read first:", stdout);
        Assert.Contains("Since last handoff:", stdout);
        Assert.True(File.Exists(_tv.Abs("06_Agent_Memory/Log - Alpha.md")));
    }

    [Fact]
    public void SessionStartJsonSerializesTheBrief()
    {
        // With a prior handoff the brief carries a non-null delta (null values are omitted).
        Assert.Equal(0, RunCli("session", "end", "--project", "Alpha", "--summary", "first pass").Code);

        var (code, stdout, _) = RunCli("session", "start", "--project", "Alpha", "--max-chars", "2000", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        var brief = doc.RootElement.GetProperty("brief");
        Assert.Equal("Alpha", brief.GetProperty("project").GetString());
        Assert.True(brief.TryGetProperty("decisionsInForce", out _));
        Assert.True(brief.TryGetProperty("deltaSinceLastHandoff", out _));
    }

    [Fact]
    public void ValidateExitsNonZeroOnFixtureErrors()
    {
        var (code, stdout, _) = RunCli("validate");
        Assert.Equal(1, code);
        Assert.Contains("critical", stdout);
    }

    [Fact]
    public void DoctorRuns()
    {
        var (code, stdout, _) = RunCli("doctor");
        Assert.Equal(0, code);
        Assert.Contains("broken links", stdout);
    }

    [Fact]
    public void AmbiguousReferenceReturnsExitCode3()
    {
        var (code, _, stderr) = RunCli("read", "Duplicate Note");
        Assert.Equal(3, code);
        Assert.Contains("ambiguous", stderr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BackupWritesZipIntoMindvaultFolder()
    {
        var (code, stdout, _) = RunCli("backup");
        Assert.Equal(0, code);
        Assert.Contains("Backup written", stdout);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(_tv.Root, ".mindvault", "backups"), "*.zip"));
    }

    [Fact]
    public void RebuildIndexCommandWorks()
    {
        var (code, stdout, _) = RunCli("rebuild-index");
        Assert.Equal(0, code);
        Assert.Contains("rebuild:", stdout);
    }

    [Fact]
    public void UnknownCommandReturns2()
    {
        var (code, _, stderr) = RunCli("frobnicate");
        Assert.Equal(2, code);
        Assert.Contains("Unknown command", stderr);
    }

    [Fact]
    public void UnknownCommandWithJsonEmitsParseableError()
    {
        var (code, stdout, _) = RunCli("frobnicate", "--json");
        Assert.Equal(2, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Contains("frobnicate", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void ValueOptionDoesNotSwallowFollowingFlag()
    {
        // `--limit --json` must not consume --json as the limit value.
        var (code, _, stderr) = RunCli("list", "--limit", "--json");
        Assert.Equal(2, code);
        Assert.Contains("--limit requires a value", stderr);
    }

    [Fact]
    public void StatusJsonReportsRescanPending()
    {
        var (code, stdout, _) = RunCli("status", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.False(doc.RootElement.GetProperty("rescanPending").GetBoolean());
    }

    [Fact]
    public void ProjectContextCommandEmitsJson()
    {
        var (code, stdout, _) = RunCli("project-context", "Alpha");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal("Alpha", doc.RootElement.GetProperty("project").GetString());
        Assert.Equal("01_Projects/Alpha.md",
            doc.RootElement.GetProperty("projectNote").GetProperty("path").GetString());
    }

    public void Dispose() => _tv.Dispose();
}
