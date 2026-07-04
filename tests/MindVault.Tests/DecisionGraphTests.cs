using MindVault.Core;

namespace MindVault.Tests;

public sealed class DecisionGraphTests : IDisposable
{
    private readonly TempVault _tv = new();

    [Fact]
    public void SupersedeUpdatesBothNotesSafely()
    {
        var newDecision = _tv.Ctx.Writer.CreateDecision("Alpha", "Use SQLite v2").Note;
        var result = _tv.Ctx.Writer.SupersedeDecision("Decision - Use SQLite", newDecision.Path);

        Assert.Equal("04_Decisions/Decision - Use SQLite.md", result.OldPath);
        Assert.True(File.Exists(result.OldSnapshot));
        Assert.True(File.Exists(result.NewSnapshot));

        var oldContent = _tv.ReadNote(result.OldPath).Replace("\r\n", "\n");
        Assert.Contains("status: superseded", oldContent);
        Assert.Contains("superseded_by:", oldContent);
        Assert.Contains("[[Decision - Use SQLite v2]]", oldContent);

        var newContent = _tv.ReadNote(result.NewPath).Replace("\r\n", "\n");
        Assert.Contains("supersedes:", newContent);
        Assert.Contains("[[Decision - Use SQLite]]", newContent);

        // Both reindexed: the old decision now carries the superseded status in the index.
        Assert.Equal("superseded", _tv.Ctx.Db.FindByPath(result.OldPath)!.Status);
    }

    [Fact]
    public void SupersededDecisionsAreHiddenFromListByDefault()
    {
        var newDecision = _tv.Ctx.Writer.CreateDecision("Alpha", "Use SQLite v2").Note;
        _tv.Ctx.Writer.SupersedeDecision("Decision - Use SQLite", newDecision.Path);

        var active = _tv.Ctx.Decisions.List("Alpha");
        Assert.DoesNotContain(active, d => d.Path == "04_Decisions/Decision - Use SQLite.md");
        Assert.Contains(active, d => d.Path == newDecision.Path);

        var all = _tv.Ctx.Decisions.List("Alpha", includeAll: true);
        Assert.Contains(all, d => d.Path == "04_Decisions/Decision - Use SQLite.md");
    }

    [Fact]
    public void GraphContainsSupersedeEdge()
    {
        var newDecision = _tv.Ctx.Writer.CreateDecision("Alpha", "Use SQLite v2").Note;
        _tv.Ctx.Writer.SupersedeDecision("Decision - Use SQLite", newDecision.Path);

        var graph = _tv.Ctx.Decisions.Graph("Alpha");
        Assert.Contains(graph.Nodes, n => n.Path == "04_Decisions/Decision - Use SQLite.md");
        Assert.Contains(graph.Edges, e =>
            e.Kind == "supersedes" &&
            e.From == newDecision.Path &&
            e.To == "04_Decisions/Decision - Use SQLite.md");
    }

    [Fact]
    public void SupersedeRejectsNonDecisionsAndSelf()
    {
        Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.SupersedeDecision("Decision - Use SQLite", "Decision - Use SQLite"));
        Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.SupersedeDecision("Alpha", "Decision - Use SQLite"));
    }

    [Fact]
    public void ValidateFlagsSupersededStatusMismatch()
    {
        File.WriteAllText(_tv.Abs("04_Decisions/Decision - Contradiction.md"),
            "---\ntype: decision\nstatus: accepted\ncreated: 2026-01-01\nupdated: 2026-01-01\nproject: Alpha\ntags:\n  - decision\nsuperseded_by:\n  - \"[[Decision - Use SQLite]]\"\n---\n\n# Decision: Contradiction\n");
        var report = _tv.Ctx.Validator.Validate();
        Assert.Contains(report.Issues, i =>
            i.Code == "superseded-status-mismatch" && i.Path == "04_Decisions/Decision - Contradiction.md");
    }

    public void Dispose() => _tv.Dispose();
}
