using MindVault.Core;

namespace MindVault.Tests;

public sealed class ProjectContextTests : IDisposable
{
    private readonly TempVault _tv = new(init: false);

    [Fact]
    public void ReturnsCompactProjectBundle()
    {
        var ctx = _tv.Ctx.Projects.Get("Alpha");

        Assert.Equal("Alpha", ctx.Project);
        Assert.Equal("01_Projects/Alpha.md", ctx.ProjectNote.Path);
        Assert.Equal("active", ctx.ProjectNote.Status);

        Assert.Contains(ctx.ActiveTasks, t => t.Path == "01_Projects/Task - Ship v1.md");
        Assert.DoesNotContain(ctx.ActiveTasks, t => t.Path == "01_Projects/Task - Write docs.md"); // done
        Assert.Contains(ctx.RecentDecisions, d => d.Path == "04_Decisions/Decision - Use SQLite.md");
        Assert.Contains(ctx.OpenRisks, r => r.Path == "01_Projects/Risk - Data loss.md");
        Assert.Contains(ctx.Constraints, c => c.Path == "02_Areas/Constraint - Local only.md");
        Assert.NotEmpty(ctx.RecentNotes);
        Assert.DoesNotContain(ctx.RecentNotes, n => n.Path == ctx.ProjectNote.Path);
    }

    [Fact]
    public void ProjectLookupIsCaseInsensitive()
    {
        Assert.Equal("Alpha", _tv.Ctx.Projects.Get("aLpHa").Project);
    }

    [Fact]
    public void LimitCapsEveryList()
    {
        var ctx = _tv.Ctx.Projects.Get("Alpha", limit: 1);
        Assert.True(ctx.ActiveTasks.Count <= 1);
        Assert.True(ctx.RecentNotes.Count <= 1);
    }

    [Fact]
    public void UnknownProjectErrorListsKnownProjects()
    {
        var ex = Assert.Throws<MindVaultException>(() => _tv.Ctx.Projects.Get("Zeta"));
        Assert.Contains("Alpha", ex.Message);
    }

    public void Dispose() => _tv.Dispose();
}
