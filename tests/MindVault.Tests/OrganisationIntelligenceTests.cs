using MindVault.Core;
using MindVault.Mcp;

namespace MindVault.Tests;

/// <summary>
/// The 15 organisation-intelligence evals from the token-compression pass, run against the
/// deliberately messy TokenEfficientVault fixture (archived/superseded/rejected notes,
/// orphans, a broken link, unsummarized large notes, waiting thoughts, a noisy memory).
/// </summary>
public sealed class OrganisationIntelligenceTests
{
    private static TempVault Vault() => new(fixture: "TokenEfficientVault");

    // 1. Route card selects a bounded read-first list.
    [Fact]
    public void RouteCardReadFirstIsAtMostFiveNotesWithReasons()
    {
        using var tv = Vault();
        var card = tv.Ctx.Routes.Build("Tokenproj").Card;
        Assert.NotNull(card);
        Assert.InRange(card!.ReadFirst.Count, 1, 5);
        Assert.All(card.ReadFirst, n =>
        {
            Assert.False(string.IsNullOrWhiteSpace(n.Reason));
            Assert.True(n.EstimatedTokens > 0);
        });
    }

    // 2. Archived and superseded notes never enter the read lists by default.
    [Fact]
    public void RouteCardExcludesArchivedAndSupersededByDefault()
    {
        using var tv = Vault();
        var card = tv.Ctx.Routes.Build("Tokenproj").Card!;
        var readPaths = card.ReadFirst.Concat(card.ReadIfNeeded).Select(n => n.Path).ToList();
        Assert.DoesNotContain(readPaths, p => p.StartsWith("99_Archive/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("04_Decisions/Decision - Use nested config.md", readPaths);
        Assert.DoesNotContain("04_Decisions/Decision - Cache everything.md", readPaths);
    }

    // 3. Do-not-read guidance is explicit and reasoned.
    [Fact]
    public void RouteCardListsDoNotReadNotesWithReasons()
    {
        using var tv = Vault();
        var card = tv.Ctx.Routes.Build("Tokenproj").Card!;
        Assert.NotEmpty(card.DoNotRead);
        Assert.All(card.DoNotRead, n => Assert.False(string.IsNullOrWhiteSpace(n.Reason)));
        Assert.Contains(card.DoNotRead, n => n.Path.StartsWith("99_Archive/", StringComparison.OrdinalIgnoreCase));
    }

    // 4. Read plans are strict: ordered, bounded, with stop conditions.
    [Fact]
    public void ReadPlanHasStopConditionsAndBoundedOrderedReads()
    {
        using var tv = Vault();
        var plan = tv.Ctx.ReadPlans.Build("Tokenproj", goal: "improve config validation").Plan;
        Assert.NotNull(plan);
        Assert.NotEmpty(plan!.StopWhen);
        Assert.InRange(plan.Steps.Count, 1, 5);
        Assert.Equal(Enumerable.Range(1, plan.Steps.Count), plan.Steps.Select(s => s.Order));
        Assert.NotEmpty(plan.DoNotRead);
        Assert.All(plan.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.ExpectedUse)));
    }

    // 5a. Route snippets prefer the generated summary line over raw body text.
    [Fact]
    public void RouteUsesTheGeneratedSummaryLineAsSnippetWhenAvailable()
    {
        using var tv = Vault();
        var card = tv.Ctx.Routes.Build("Tokenproj", goal: "storage layout on disk").Card!;
        var storage = card.ReadFirst.Concat(card.ReadIfNeeded)
            .FirstOrDefault(n => n.Path.EndsWith("Architecture - Storage layout.md", StringComparison.Ordinal));
        Assert.NotNull(storage);
        Assert.Equal("How config data is laid out on disk.", storage!.SummarySnippet);
    }

    // 5b. Capsules fall back to the hub's generated summary when there is no Goal section.
    [Fact]
    public void CapsuleFallsBackToHubSummaryWhenGoalSectionIsMissing()
    {
        using var tv = Vault();
        var capsule = tv.Ctx.Capsules.Build("Sideproj").Capsule;
        Assert.NotNull(capsule);
        Assert.Contains("Side experiment for config tooling spikes.", capsule!.CurrentGoal);
    }

    // 6. Budgets are enforced, never silently ignored.
    [Fact]
    public void TokenBudgetIsEnforcedOnRouteCards()
    {
        using var tv = Vault();
        var tight = tv.Ctx.Routes.Build("Tokenproj", budget: new ContextBudget(MaxNotes: 2)).Card!;
        Assert.True(tight.ReadFirst.Count <= 2);

        var tiny = tv.Ctx.Routes.Build("Tokenproj",
            budget: new ContextBudget(MaxEstimatedTokens: 120)).Card!;
        Assert.Equal(120, tiny.TokenBudget);
        Assert.True(tiny.ReadFirst.Count == 1 || tiny.ReadFirst.Sum(n => n.EstimatedTokens) <= 120,
            "over-budget notes must be demoted to readIfNeeded");
    }

    // 7. The score sees missing summaries.
    [Fact]
    public void OrganisationScoreDetectsMissingSummaries()
    {
        using var tv = Vault();
        var score = tv.Ctx.OrgScore.Run("Tokenproj");
        var cat = score.Categories.Single(c => c.Name == "summaryCoverage");
        Assert.True(cat.Score < 100, cat.Evidence);
        Assert.Contains("large note", cat.Evidence);
    }

    // 8. The score sees orphans.
    [Fact]
    public void OrganisationScoreDetectsOrphanNotes()
    {
        using var tv = Vault();
        var score = tv.Ctx.OrgScore.Run("Tokenproj");
        var cat = score.Categories.Single(c => c.Name == "orphanRisk");
        Assert.True(cat.Score < 100, cat.Evidence);
        Assert.Contains("orphan", cat.Evidence);
    }

    // 9. The score puts a number on token waste.
    [Fact]
    public void OrganisationScoreEstimatesTokenWaste()
    {
        using var tv = Vault();
        var score = tv.Ctx.OrgScore.Run("Tokenproj");
        Assert.True(score.EstimatedTokenWaste > 0);
        Assert.True(score.EstimatedTokenSavingsIfFixed > 0);
    }

    // 10. Summary generation touches only the generated block.
    [Fact]
    public void SummaryGenerationPreservesHumanTextAndIsIdempotent()
    {
        using var tv = Vault();
        const string path = "03_Resources/Architecture/Architecture - Config pipeline.md";
        var abs = Path.Combine(tv.Root, path);
        Assert.DoesNotContain(SummaryService.MarkerStart, File.ReadAllText(abs));

        var report = tv.Ctx.Summaries.ForNote(path, apply: true);
        Assert.Equal(1, report.Applied);

        var after = File.ReadAllText(abs);
        Assert.StartsWith("---", after);
        Assert.Contains(SummaryService.MarkerStart, after);
        Assert.Contains("The config pipeline loads flat files", after);
        Assert.Contains("## Validation Stage", after);
        Assert.Contains("## Appendix - Failure Modes", after);

        var again = tv.Ctx.Summaries.ForNote(path, apply: true);
        Assert.Equal(0, again.Applied); // unchanged block is never rewritten
    }

    // 11. Map v2 rebuild: new sections appear, human text survives.
    [Fact]
    public void MapV2RebuildPreservesHumanTextAndAddsAgentSections()
    {
        using var tv = Vault();
        var result = tv.Ctx.Maps.Rebuild("Tokenproj");
        var raw = File.ReadAllText(Path.Combine(tv.Root, result.Path));
        Assert.Contains("Humans wrote this paragraph and it must survive every rebuild.", raw);
        Assert.DoesNotContain("stale block — rebuild pending", raw);
        Assert.Contains("## Start Here", raw);
        Assert.Contains("## Do Not Repeat", raw);
        Assert.Contains("## Organisation Score", raw);
        Assert.Contains("## Large Notes Missing Summaries", raw);
        Assert.Contains("Always run map rebuild instead of editing generated blocks", raw);
    }

    // 12. The typed graph explains decision↔task (and the rest of the family).
    [Fact]
    public void TypedGraphExplainsDecisionTaskRelationship()
    {
        using var tv = Vault();
        var explanation = tv.Ctx.Graph.Explain("Decision - Use flat config", "Task - Add config validation");
        Assert.True(explanation.Found, explanation.Explanation);
        Assert.Contains(explanation.Path, e => e.Type == "task_tracks_decision");

        var build = tv.Ctx.Graph.Build("Tokenproj");
        Assert.True(File.Exists(build.SidecarPath));
        Assert.Contains("task_tracks_decision", build.EdgesByType.Keys);
        Assert.Contains("mistake_prevented_by", build.EdgesByType.Keys);
        Assert.Contains("risk_mitigated_by", build.EdgesByType.Keys);
        Assert.Contains("supersedes", build.EdgesByType.Keys);
        Assert.Contains("belongs_to_project", build.EdgesByType.Keys);
    }

    // 13. Hidden feedback → low-value with a reason, excluded from read lists, listed as do-not-read.
    [Fact]
    public void HiddenNotesAreLowValueAndExcludedFromRoutes()
    {
        using var tv = Vault();
        tv.Ctx.Feedback.Record("Memory - Random musings", "hidden", "keeps polluting retrieval");

        var low = tv.Ctx.LowValue.Find("Tokenproj");
        var row = low.Notes.Single(n => n.Path.EndsWith("Memory - Random musings.md", StringComparison.Ordinal));
        Assert.Contains("hidden by feedback", row.Reasons);

        var card = tv.Ctx.Routes.Build("Tokenproj").Card!;
        Assert.DoesNotContain(card.ReadFirst.Concat(card.ReadIfNeeded),
            n => n.Path.EndsWith("Memory - Random musings.md", StringComparison.Ordinal));
        Assert.Contains(card.DoNotRead,
            n => n.Path.EndsWith("Memory - Random musings.md", StringComparison.Ordinal));
    }

    // 14. Useful feedback lifts a note into the route with the reason attached.
    [Fact]
    public void UsefulFeedbackBoostsANoteIntoTheRoute()
    {
        using var tv = Vault();
        tv.Ctx.Feedback.Record("Risk - Config drift", "useful", "governs this area");
        var card = tv.Ctx.Routes.Build("Tokenproj", goal: "config validation schema").Card!;
        var risk = card.ReadFirst.Concat(card.ReadIfNeeded)
            .FirstOrDefault(n => n.Path.EndsWith("Risk - Config drift.md", StringComparison.Ordinal));
        Assert.NotNull(risk);
        Assert.Contains("marked useful", risk!.Reason);
    }

    // 15. The skills pack teaches agents to stop reading.
    [Fact]
    public void SkillsInstructAgentsToStopReadingWhenContextIsEnough()
    {
        var root = FindRepoRoot();
        foreach (var skill in new[] { "mindvault-route-card", "mindvault-read-plan" })
        {
            var content = File.ReadAllText(Path.Combine(root, "skills", skill, "SKILL.md"));
            Assert.Contains("stop", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("do not", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    // Guard: compile is dry-run by default — nothing on disk changes.
    [Fact]
    public void CompileDryRunWritesNothing()
    {
        using var tv = Vault();
        var report = tv.Ctx.Compiler.Compile("Tokenproj", apply: false);
        Assert.True(report.DryRun);
        Assert.False(File.Exists(Path.Combine(tv.Root, ".mindvault", "link-graph.jsonl")));
        var mapRaw = File.ReadAllText(Path.Combine(tv.Root, "09_Maps", "Tokenproj Map.md"));
        Assert.Contains("stale block — rebuild pending", mapRaw); // map untouched
        Assert.True(report.OverallScore is > 0 and <= 100);
    }

    // Guard: search annotates feedback-hidden hits but never re-ranks or drops them.
    [Fact]
    public void SearchAnnotatesHiddenNotesWithACautionInsteadOfHidingThem()
    {
        using var tv = Vault();
        tv.Ctx.Feedback.Record("Memory - Random musings", "hidden", "noise");
        var hit = tv.Ctx.Search.Search("musings")
            .FirstOrDefault(r => r.Path.EndsWith("Memory - Random musings.md", StringComparison.Ordinal));
        Assert.NotNull(hit); // still findable — search stays honest
        Assert.Contains("hidden by feedback", hit!.Caution);
    }

    // Guard: section-scoped MCP reads return just the asked-for section.
    [Fact]
    public void McpReadNoteCanScopeToOneSection()
    {
        using var tv = Vault();
        var tools = new MindVaultTools(tv.Ctx);
        var text = tools.ReadNote("Tokenproj", section: "Goal");
        Assert.Contains("Ship the config pipeline v2", text);
        Assert.DoesNotContain("Config errors fail fast at startup", text); // other sections stay out
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "skills")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found from test base dir");
    }
}
