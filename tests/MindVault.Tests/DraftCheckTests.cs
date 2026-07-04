using MindVault.Core;

namespace MindVault.Tests;

public sealed class DraftCheckTests : IDisposable
{
    private readonly TempVault _tv = new();

    [Fact]
    public void ExactDuplicateIsABlocker()
    {
        var result = _tv.Ctx.Drafts.CheckDraft("decision", "Alpha", "Use SQLite");
        Assert.False(result.Ok);
        Assert.Contains(result.Blockers, b => b.Contains("already exists"));
        Assert.Contains("04_Decisions/Decision - Use SQLite.md", result.RelatedPaths);
    }

    [Fact]
    public void NearDuplicateTitleIsAWarning()
    {
        var result = _tv.Ctx.Drafts.CheckDraft("task", "Alpha", "Ship the v1");
        Assert.True(result.Ok); // advisory, not blocking
        Assert.Contains(result.Warnings, w => w.Contains("Task: Ship v1"));
    }

    [Fact]
    public void MissingProjectBlocksProjectScopedTypes()
    {
        var noProject = _tv.Ctx.Drafts.CheckDraft("task", null, "Do something concrete");
        Assert.False(noProject.Ok);
        Assert.Contains(noProject.Blockers, b => b.Contains("must belong to a project"));

        var unknownProject = _tv.Ctx.Drafts.CheckDraft("decision", "Nonexistent", "Pick a database");
        Assert.False(unknownProject.Ok);
        Assert.Contains(unknownProject.Blockers, b => b.Contains("Project not found"));
    }

    [Fact]
    public void VagueTitlesAreFlagged()
    {
        var result = _tv.Ctx.Drafts.CheckDraft("task", "Alpha", "fix stuff");
        Assert.Contains(result.Warnings, w => w.Contains("too vague"));
    }

    [Fact]
    public void RelatedDecisionSuggestsSupersede()
    {
        var result = _tv.Ctx.Drafts.CheckDraft("decision", "Alpha", "Use SQLite differently");
        Assert.Contains(result.Suggestions, s => s.Contains("supersede"));
        Assert.Contains("04_Decisions/Decision - Use SQLite.md", result.RelatedPaths);
    }

    [Fact]
    public void InvalidTypeIsABlocker()
    {
        var result = _tv.Ctx.Drafts.CheckDraft("banana", null, "X");
        Assert.False(result.Ok);
        Assert.Contains(result.Blockers, b => b.Contains("not a managed note type"));
    }

    [Fact]
    public void CheckNoteFlagsEmptySectionsAndBrokenLinks()
    {
        // Fixture task "Ship v1" has acceptance criteria; make a task with an empty one.
        var created = _tv.Ctx.Writer.CreateTask("Alpha", "Empty criteria demo").Note;
        var result = _tv.Ctx.Drafts.CheckNote(created.Path);
        Assert.Contains(result.Warnings, w => w.Contains("Acceptance Criteria"));

        var orphan = _tv.Ctx.Drafts.CheckNote("00_Inbox/Orphan Task.md");
        Assert.Contains(orphan.Warnings, w => w.Contains("[[Ghost Note]]"));
        Assert.Contains(orphan.Warnings, w => w.Contains("Invalid status"));
    }

    [Fact]
    public void CreateResultsCarryDraftWarnings()
    {
        var result = _tv.Ctx.Writer.CreateTask("Alpha", "Ship the v1");
        Assert.Contains(result.Warnings, w => w.Contains("Task: Ship v1")); // near-duplicate surfaced
    }

    public void Dispose() => _tv.Dispose();
}
