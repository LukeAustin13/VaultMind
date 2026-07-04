using MindVault.Core;

namespace MindVault.Tests;

public sealed class ContextAndPackTests : IDisposable
{
    private readonly TempVault _tv = new(init: false);

    [Fact]
    public void ContextIncludesGoalAndSplitsBlockedTasks()
    {
        File.WriteAllText(_tv.Abs("01_Projects/Task - Stuck.md"),
            $"---\ntype: task\nstatus: blocked\ncreated: 2026-06-01\nupdated: {DateTime.Now:yyyy-MM-dd}\nproject: Alpha\ntags:\n  - task\nlinks:\n  - \"[[Alpha]]\"\n---\n\n# Task: Stuck\n\n## Description\n\nWaiting on upstream.\n");
        _tv.Ctx.Scanner.Scan();

        var c = _tv.Ctx.Projects.Get("Alpha");
        Assert.Contains("full text search", c.CurrentGoal);
        Assert.Contains(c.ActiveTasks, t => t.Path == "01_Projects/Task - Ship v1.md");
        Assert.DoesNotContain(c.ActiveTasks, t => t.Path == "01_Projects/Task - Stuck.md");
        Assert.Contains(c.BlockedTasks, t => t.Path == "01_Projects/Task - Stuck.md");
        Assert.NotEmpty(c.RecommendedNextReads);
        Assert.Equal("01_Projects/Alpha.md", c.RecommendedNextReads[0].Path);
    }

    [Fact]
    public void ContextWarnsAboutStaleTasksAndSupersededMismatch()
    {
        File.WriteAllText(_tv.Abs("04_Decisions/Decision - Old Way.md"),
            "---\ntype: decision\nstatus: accepted\ncreated: 2026-01-01\nupdated: 2026-01-01\nproject: Alpha\ntags:\n  - decision\nsuperseded_by:\n  - \"[[Decision - Use SQLite]]\"\n---\n\n# Decision: Old Way\n\n## Decision\n\nThe old way.\n");
        var ancient = DateTime.Today.AddDays(-120).ToString("yyyy-MM-dd");
        File.WriteAllText(_tv.Abs("01_Projects/Task - Ancient.md"),
            $"---\ntype: task\nstatus: open\ncreated: {ancient}\nupdated: {ancient}\nproject: Alpha\ntags:\n  - task\nlinks:\n  - \"[[Alpha]]\"\n---\n\n# Task: Ancient\n\n## Description\n\nForgotten work.\n");
        _tv.Ctx.Scanner.Scan();

        var c = _tv.Ctx.Projects.Get("Alpha");
        Assert.Contains(c.Warnings, w => w.Contains("Stale") && w.Contains("Task - Ancient"));
        Assert.Contains(c.Warnings, w => w.Contains("superseded_by") && w.Contains("Decision - Old Way"));
    }

    [Fact]
    public void DetailLevelsChangeShape()
    {
        var brief = _tv.Ctx.Projects.Get("Alpha", detailLevel: "brief");
        Assert.Empty(brief.Constraints);
        Assert.Empty(brief.RecentNotes);
        Assert.NotNull(brief.CurrentGoal);
        Assert.True(brief.ActiveTasks.Count <= 3);

        var standard = _tv.Ctx.Projects.Get("Alpha");
        Assert.NotEmpty(standard.Constraints);

        Assert.Throws<MindVaultException>(() => _tv.Ctx.Projects.Get("Alpha", detailLevel: "verbose"));
    }

    [Fact]
    public void ContextPackBuildsMarkdownAndJson()
    {
        var pack = _tv.Ctx.Packs.Get("Alpha");
        Assert.Equal("Alpha", pack.Project);
        Assert.Contains(pack.ActiveTasks, t => t.Path == "01_Projects/Task - Ship v1.md");
        Assert.Contains(pack.DoNotForget, d => d.Contains("Constraint"));
        Assert.NotEmpty(pack.SuggestedNextReads);

        var markdown = ContextPackService.ToMarkdown(pack);
        Assert.Contains("# Context pack: Alpha", markdown);
        Assert.Contains("## Read next", markdown);
        Assert.Contains("01_Projects/Alpha.md", markdown);
        // Compact: a fixture-sized pack must stay well under any dump threshold.
        Assert.True(markdown.Length < 4000, $"pack markdown too large: {markdown.Length} chars");
    }

    [Fact]
    public void TaskAwarePackSurfacesRelevantNotesFirst()
    {
        var pack = _tv.Ctx.Packs.Get("Alpha", task: "improve the SQLite search index");
        Assert.Equal("improve the SQLite search index", pack.TaskFocus);
        Assert.Contains(pack.TaskRelevantNotes, n => n.Path == "04_Decisions/Decision - Use SQLite.md");
        Assert.Equal("04_Decisions/Decision - Use SQLite.md", pack.RelevantDecisions[0].Path);
    }

    [Fact]
    public void PackJsonStaysCompact()
    {
        var pack = _tv.Ctx.Packs.Get("Alpha", task: "search work");
        var json = Json.Serialize(pack);
        Assert.True(json.Length < 8000, $"pack json too large: {json.Length} chars");
        Assert.DoesNotContain("## Goal\n\nBuild the alpha release", json); // refs, not full note bodies
    }

    public void Dispose() => _tv.Dispose();
}
