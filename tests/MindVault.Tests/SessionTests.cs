using MindVault.Core;

namespace MindVault.Tests;

public sealed class SessionTests : IDisposable
{
    private readonly TempVault _tv = new();

    [Fact]
    public void StartReturnsBriefAndCreatesLogNoteOnce()
    {
        var first = _tv.Ctx.Sessions.StartBrief("Alpha", task: "harden search");
        Assert.True(first.LogNoteCreated);
        Assert.Equal("06_Agent_Memory/Log - Alpha.md", first.LogNote);
        Assert.True(File.Exists(_tv.Abs(first.LogNote)));
        Assert.Equal("Alpha", first.Project);
        Assert.Equal("harden search", first.Task);

        var second = _tv.Ctx.Sessions.StartBrief("Alpha");
        Assert.False(second.LogNoteCreated);
    }

    [Fact]
    public void EndWritesStructuredHandoffEntry()
    {
        _tv.Ctx.Sessions.StartBrief("Alpha");
        var result = _tv.Ctx.Sessions.End("Alpha",
            "Shipped weighted search", tests: "dotnet test green (145)", followUps: "tune recency boost");

        var content = _tv.ReadNote("06_Agent_Memory/Log - Alpha.md").Replace("\r\n", "\n");
        Assert.Contains("## Sessions", content);
        Assert.Contains("— Shipped weighted search", content);
        Assert.Contains("- Tests: dotnet test green (145)", content);
        Assert.Contains("- Follow-ups: tune recency boost", content);
        Assert.True(File.Exists(result.SnapshotPath)); // handoff writes go through the snapshot pipeline
    }

    [Fact]
    public void EndWithoutTestsRecordsThatHonestly()
    {
        _tv.Ctx.Sessions.End("Alpha", "Quick fix");
        var content = _tv.ReadNote("06_Agent_Memory/Log - Alpha.md");
        Assert.Contains("- Tests: not recorded", content);
        Assert.Contains("- Follow-ups: none", content);
    }

    [Fact]
    public void SessionEntriesFeedProjectContextLogs()
    {
        _tv.Ctx.Sessions.End("Alpha", "Implemented context packs", tests: "green");
        var context = _tv.Ctx.Projects.Get("Alpha");
        Assert.Contains(context.RecentImplementationLogs, l => l.Contains("Implemented context packs"));
    }

    [Fact]
    public void SessionRequiresSummaryAndKnownProject()
    {
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Sessions.End("Alpha", "  "));
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Sessions.StartBrief("NoSuchProject"));
    }

    public void Dispose() => _tv.Dispose();
}
