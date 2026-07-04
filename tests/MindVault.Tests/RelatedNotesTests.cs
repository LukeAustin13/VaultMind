using MindVault.Cli;
using MindVault.Core;

namespace MindVault.Tests;

public sealed class RelatedNotesTests : IDisposable
{
    private readonly TempVault _tv = new();

    [Fact]
    public void DecisionSurfacesBacklinksAndProjectMemory()
    {
        var result = _tv.Ctx.Related.Get("Decision - Use SQLite");
        Assert.Equal("04_Decisions/Decision - Use SQLite.md", result.Path);
        // Alpha's project note links to this decision.
        Assert.Contains(result.Related, r => r.Path == "01_Projects/Alpha.md");
        // Active memory of the same project rides along.
        Assert.Contains(result.Related, r => r.Reason == "same project, active");
        // Every entry explains why it is here.
        Assert.All(result.Related, r => Assert.False(string.IsNullOrWhiteSpace(r.Reason)));
    }

    [Fact]
    public void RelatedNeverIncludesTheNoteItselfAndDeduplicates()
    {
        var result = _tv.Ctx.Related.Get("Decision - Use SQLite");
        Assert.DoesNotContain(result.Related, r => r.Path == result.Path);
        Assert.Equal(result.Related.Count, result.Related.Select(r => r.Path).Distinct().Count());
    }

    [Fact]
    public void LimitIsRespected()
    {
        var result = _tv.Ctx.Related.Get("Alpha", limit: 3);
        Assert.True(result.Related.Count <= 3);
    }

    [Fact]
    public void OutputIsDeterministic()
    {
        var a = _tv.Ctx.Related.Get("Alpha");
        var b = _tv.Ctx.Related.Get("Alpha");
        Assert.Equal(a.Related.Select(r => r.Path), b.Related.Select(r => r.Path));
    }

    [Fact]
    public void CliRelatedCommandWorks()
    {
        var stdout = new StringWriter();
        var code = CliRunner.Run(["related", "Decision - Use SQLite", "--vault", _tv.Root],
            stdout, new StringWriter(), _ => null, _tv.Root);
        Assert.Equal(0, code);
        Assert.Contains("01_Projects/Alpha.md", stdout.ToString());
    }

    [Fact]
    public void CliDetectProjectCommandWorks()
    {
        var stdout = new StringWriter();
        var code = CliRunner.Run(["detect-project", "al-pha", "--json", "--vault", _tv.Root],
            stdout, new StringWriter(), _ => null, _tv.Root);
        Assert.Equal(0, code);
        Assert.Contains("\"project\":\"Alpha\"", stdout.ToString());
    }

    public void Dispose() => _tv.Dispose();
}
