using MindVault.Core;

namespace MindVault.Tests;

/// <summary>
/// Repo-to-project binding: aliases, repoNames, condensed matching and confidence tiers.
/// Detection must resolve confidently or return candidates — never guess.
/// </summary>
public sealed class ProjectDetectTests : IDisposable
{
    private readonly TempVault _tv = new();

    public ProjectDetectTests()
    {
        File.WriteAllText(_tv.Abs("01_Projects/Gamma.md"), """
            ---
            type: project
            status: active
            created: 2026-01-10
            updated: 2026-06-20
            tags:
              - project
            aliases:
              - gam
              - Gamma Project
            repoNames:
              - gamma-svc
            links: []
            ---

            # Gamma

            ## Goal

            Ship the gamma service.
            """);
        File.WriteAllText(_tv.Abs("01_Projects/Task - Gamma rollout.md"), """
            ---
            type: task
            status: open
            project: Gamma Project
            created: 2026-06-20
            updated: 2026-06-21
            tags:
              - task
            links: []
            ---

            # Task: Gamma rollout

            ## Acceptance Criteria

            - rolled out
            """);
        _tv.Ctx.Scanner.Scan();
    }

    [Fact]
    public void ExactTitleIsExactConfidence()
    {
        var d = _tv.Ctx.ProjectDetect.Detect("Alpha");
        Assert.Equal("Alpha", d.Project?.Title);
        Assert.Equal("exact", d.Confidence);
    }

    [Fact]
    public void AliasResolvesWithHighConfidence()
    {
        var d = _tv.Ctx.ProjectDetect.Detect("gam");
        Assert.Equal("Gamma", d.Project?.Title);
        Assert.Equal("high", d.Confidence);
        Assert.Equal("alias", d.MatchedVia);
    }

    [Fact]
    public void RepoNameResolves()
    {
        var d = _tv.Ctx.ProjectDetect.Detect("gamma-svc");
        Assert.Equal("Gamma", d.Project?.Title);
        Assert.Equal("repo-name", d.MatchedVia);
    }

    [Fact]
    public void CondensedComparisonBridgesSeparatorStyles()
    {
        // Underscore repo folder vs dash-declared repo name.
        var d = _tv.Ctx.ProjectDetect.Detect("gamma_svc");
        Assert.Equal("Gamma", d.Project?.Title);
        Assert.Equal("condensed-name", d.MatchedVia);

        // Dashed folder name vs plain project title.
        var alpha = _tv.Ctx.ProjectDetect.Detect("al-pha");
        Assert.Equal("Alpha", alpha.Project?.Title);
    }

    [Fact]
    public void FuzzyOverlapOnlySuggestsNeverResolves()
    {
        var d = _tv.Ctx.ProjectDetect.Detect("Alpha Backend");
        Assert.Null(d.Project);
        Assert.Equal("low", d.Confidence);
        Assert.Contains(d.Candidates, c => c.Title == "Alpha");
    }

    [Fact]
    public void SharedAliasIsAmbiguousWithCandidates()
    {
        File.WriteAllText(_tv.Abs("01_Projects/Delta.md"), """
            ---
            type: project
            status: active
            created: 2026-01-10
            updated: 2026-06-20
            tags:
              - project
            aliases:
              - gam
            links: []
            ---

            # Delta
            """);
        _tv.Ctx.Scanner.Scan();

        var d = _tv.Ctx.ProjectDetect.Detect("gam");
        Assert.Null(d.Project);
        Assert.True(d.Ambiguous);
        Assert.Equal(2, d.Candidates.Count);
    }

    [Fact]
    public void UnknownNameListsKnownProjects()
    {
        var ex = Assert.Throws<MindVaultException>(() => _tv.Ctx.ProjectDetect.ResolveOrThrow("Zeppelin"));
        Assert.Contains("Known projects", ex.Message);
    }

    [Fact]
    public void ProjectContextResolvesAliasAndSaysSo()
    {
        var context = _tv.Ctx.Projects.Get("gam");
        Assert.Equal("Gamma", context.Project);
        Assert.Equal("high", context.Confidence);
        Assert.Equal("alias", context.ResolvedVia);
        Assert.Contains(context.Warnings, w => w.Contains("Resolved 'gam'"));
    }

    [Fact]
    public void ContextIncludesNotesFiledUnderAnAlias()
    {
        // The task's `project:` field says "Gamma Project" (an alias), not "Gamma".
        var context = _tv.Ctx.Projects.Get("Gamma");
        Assert.Contains(context.ActiveTasks, t => t.Title.Contains("Gamma rollout"));
    }

    [Fact]
    public void CreateViaRepoNameBindsToTheRealProjectInsteadOfFailing()
    {
        var result = _tv.Ctx.Writer.CreateDecision("gamma-svc", "Use message queue");
        Assert.Equal("Gamma", result.Note.Project);
    }

    [Fact]
    public void CreatingAProjectThatMatchesAnAliasIsRefused()
    {
        var ex = Assert.Throws<DuplicateSuspectedException>(() =>
            _tv.Ctx.Writer.CreateProject("Gamma Project"));
        Assert.Contains(ex.Candidates, c => c.Contains("Gamma.md"));
    }

    [Fact]
    public void DetectionIsDeterministic()
    {
        var a = _tv.Ctx.ProjectDetect.Detect("Alpha Backend");
        var b = _tv.Ctx.ProjectDetect.Detect("Alpha Backend");
        Assert.Equal(a.Candidates.Select(c => c.Path), b.Candidates.Select(c => c.Path));
    }

    public void Dispose() => _tv.Dispose();
}
