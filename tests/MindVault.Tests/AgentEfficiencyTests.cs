using MindVault.Core;

namespace MindVault.Tests;

/// <summary>
/// v0.8.0 agent-efficiency levers: the one-call session brief, handoff-relative recall, batched
/// session close, and the capsule/search payload-slimming options.
/// </summary>
public sealed class AgentEfficiencyTests : IDisposable
{
    private readonly TempVault _tv = new();

    // ---------- Lever 3: the session brief ----------

    [Fact]
    public void StartBriefCarriesGoalDecisionsTasksAndConstraints()
    {
        var brief = _tv.Ctx.Sessions.StartBrief("Alpha");

        Assert.Equal("Alpha", brief.Project);
        Assert.NotNull(brief.Goal);
        Assert.Contains("full text search", brief.Goal!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(brief.DecisionsInForce, d => d.Title.Contains("SQLite"));
        Assert.Contains(brief.OpenTasks, t => t.Title.Contains("Ship v1"));
        Assert.True(File.Exists(_tv.Abs(brief.LogNote)));
    }

    [Fact]
    public void StartBriefEnsuresTheLogNoteOnce()
    {
        var first = _tv.Ctx.Sessions.StartBrief("Alpha");
        Assert.True(first.LogNoteCreated);
        var second = _tv.Ctx.Sessions.StartBrief("Alpha");
        Assert.False(second.LogNoteCreated);
    }

    [Fact]
    public void StartBriefEchoesTheTask()
    {
        var brief = _tv.Ctx.Sessions.StartBrief("Alpha", task: "harden search");
        Assert.Equal("harden search", brief.Task);
    }

    [Fact]
    public void BriefQueriesProjectContextExactlyOnce()
    {
        var before = _tv.Ctx.Projects.GetCallCount;
        _ = _tv.Ctx.Briefs.Compose("Alpha", task: null, logNoteCreated: false,
            logNote: "06_Agent_Memory/Log - Alpha.md");
        // One shared query feeds the facts AND the route internals; not 2-3 identical queries.
        Assert.Equal(before + 1, _tv.Ctx.Projects.GetCallCount);
    }

    [Fact]
    public void BriefWarnsAndNullsDeltaWhenNoPriorHandoff()
    {
        var brief = _tv.Ctx.Sessions.StartBrief("Alpha");
        Assert.Null(brief.DeltaSinceLastHandoff);
        Assert.Contains(brief.Warnings, w => w.Contains("No prior handoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BriefReportsDeltaAfterAHandoff()
    {
        // Write a handoff (which appends a ### entry), then a change, then read the brief.
        _tv.Ctx.Sessions.End("Alpha", "first pass done", tests: "green");
        _tv.Ctx.Writer.UpdateFrontmatter("Task - Ship v1", "status", "active");

        var brief = _tv.Ctx.Sessions.StartBrief("Alpha");
        Assert.NotNull(brief.DeltaSinceLastHandoff);
        var delta = brief.DeltaSinceLastHandoff!;

        // The real change is counted and itemised...
        Assert.True(delta.Tasks >= 1, "the task status change since the handoff must be counted");
        Assert.Contains(delta.Items, i => i.Path == "01_Projects/Task - Ship v1.md");

        // ...but the session log carrying the reference handoff is not a "change" — counting it
        // would inflate every same-day delta by at least one.
        Assert.DoesNotContain(delta.Items,
            i => string.Equals(i.Path, brief.LogNote, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, delta.Sessions);
    }

    [Fact]
    public void BriefTrimmingHonoursTheCharBudget()
    {
        var tight = _tv.Ctx.Sessions.StartBrief("Alpha", maxChars: 1000);
        var json = Json.Serialize(tight);
        // A tiny budget clamps to 1000 and trims low-priority sections; goal/decisions survive.
        Assert.True(json.Length <= 4000, $"brief JSON was {json.Length} chars under a 1000 budget");
        Assert.NotNull(tight.Goal);
    }

    [Fact]
    public void BriefTrimDropsKnownMistakesBeforeDecisions()
    {
        // Force known mistakes to exist, then squeeze: mistakes go before decisions per priority.
        _tv.Ctx.Writer.CreateMistake("Trusted mtime after restore", "Alpha",
            lesson: "mtime lied", prevention: "hash instead");
        var squeezed = _tv.Ctx.Sessions.StartBrief("Alpha", maxChars: 1000);
        Assert.Empty(squeezed.KnownMistakes);
    }

    [Fact]
    public void BriefFactsAppearOnlyOnceAcrossSections()
    {
        var brief = _tv.Ctx.Sessions.StartBrief("Alpha");
        // A note that is a fact (task/decision) is not also duplicated into readFirst.
        var factPaths = brief.DecisionsInForce.Select(d => d.Path)
            .Concat(brief.OpenTasks.Select(t => t.Path))
            .Concat(brief.BlockedTasks.Select(t => t.Path))
            .Concat(brief.OpenRisks.Select(r => r.Path))
            .Concat(brief.KnownMistakes.Select(m => m.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(brief.ReadFirst, r => Assert.DoesNotContain(r.Path, factPaths));
    }

    // ---------- Lever 4: handoff-relative recall ----------

    [Fact]
    public void RecallLastHandoffFallsBackWithAWarningWhenNoHandoff()
    {
        var result = _tv.Ctx.RecallSvc.Recall("Alpha", since: "last-handoff");
        Assert.Contains(result.Warnings, w => w.Contains("No prior handoff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecallLastHandoffUsesTheHandoffTimestampWhenPresent()
    {
        _tv.Ctx.Sessions.End("Alpha", "checkpoint", tests: "green");
        var result = _tv.Ctx.RecallSvc.Recall("Alpha", since: "last-handoff");
        Assert.DoesNotContain(result.Warnings, w => w.Contains("No prior handoff", StringComparison.OrdinalIgnoreCase));
        // The window opens at the handoff date (today), not the 7-day default.
        Assert.Contains(DateTime.Today.ToString("yyyy-MM-dd"), result.Window);
    }

    [Fact]
    public void RecallLastHandoffRequiresAProject()
    {
        Assert.Throws<MindVaultException>(() => _tv.Ctx.RecallSvc.Recall(project: null, since: "last-handoff"));
    }

    // ---------- Lever 6: batched session close ----------

    [Fact]
    public void EndSessionBatchCreatesItemsAndWritesTheHandoff()
    {
        var items = new SessionCloseItems(
            Decisions: [new CloseDecision("Adopt cursor pagination", "keeps memory flat")],
            Mistakes: [new CloseMistake("Assumed sorted input", Prevention: "sort first")],
            Tasks: [new CloseTask(Title: "Add pagination tests")]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "shipped pagination", "green", null, items);

        Assert.Equal(3, result.Items.Count);
        Assert.All(result.Items, i => Assert.Equal("created", i.Outcome));
        // The handoff itself was written regardless of the batch.
        var log = _tv.ReadNote("06_Agent_Memory/Log - Alpha.md");
        Assert.Contains("— shipped pagination", log);
    }

    [Fact]
    public void EndSessionBatchUpdatesTaskStatus()
    {
        var items = new SessionCloseItems(
            Tasks: [new CloseTask(Ref: "Task - Ship v1", Status: "done")]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "closed ship task", null, null, items);

        var taskResult = Assert.Single(result.Items);
        Assert.Equal("updated", taskResult.Outcome);
        Assert.Contains("status: done", _tv.ReadNote("01_Projects/Task - Ship v1.md"));
    }

    [Fact]
    public void EndSessionBatchReportsDuplicatesWithoutAbortingTheRest()
    {
        _tv.Ctx.Writer.CreateDecision("Alpha", "Use SQLite for the index"); // near-dup of the fixture decision
        var items = new SessionCloseItems(
            Decisions:
            [
                new CloseDecision("Use SQLite for the index"),   // should be flagged duplicate
                new CloseDecision("Adopt structured logging"),   // should still be created
            ]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "mixed batch", null, null, items);

        Assert.Contains(result.Items, i => i.Outcome == "skipped_duplicate");
        Assert.Contains(result.Items, i => i.Outcome == "created" && i.Title.Contains("structured logging"));
        // One bad item never loses the handoff.
        Assert.Contains("— mixed batch", _tv.ReadNote("06_Agent_Memory/Log - Alpha.md"));
    }

    [Fact]
    public void EndSessionBatchBlocksRiskyContentPerItem()
    {
        var items = new SessionCloseItems(
            Decisions: [new CloseDecision("Store token", "aws key AKIAIOSFODNN7EXAMPLE inline")]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "risky batch", null, null, items);

        var decision = Assert.Single(result.Items);
        Assert.Equal("blocked", decision.Outcome);
        // Handoff still written; a blocked item does not abort it.
        Assert.Contains("— risky batch", _tv.ReadNote("06_Agent_Memory/Log - Alpha.md"));
    }

    [Fact]
    public void EndSessionBatchDryRunWritesNothing()
    {
        var items = new SessionCloseItems(
            Decisions: [new CloseDecision("Would-be decision")],
            Tasks: [new CloseTask(Title: "Would-be task")]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "preview", null, null, items, dryRun: true);

        Assert.True(result.DryRun);
        Assert.All(result.Items, i => Assert.Equal("created", i.Outcome)); // "would create"
        Assert.All(result.Items, i => Assert.Null(i.Path));
        // No durable notes were actually created.
        Assert.False(File.Exists(_tv.Abs("04_Decisions/Decision - Would-be decision.md")));
        Assert.False(File.Exists(_tv.Abs("01_Projects/Task - Would-be task.md")));
    }

    [Fact]
    public void EndSessionBatchTaskRequiresACleanShape()
    {
        var items = new SessionCloseItems(
            Tasks: [new CloseTask(Ref: "Task - Ship v1", Status: null, Title: "also a title")]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "bad task", null, null, items);
        Assert.Equal("error", Assert.Single(result.Items).Outcome);
    }

    [Fact]
    public void EndSessionBatchDryRunPreviewsDuplicatesAsSkipped()
    {
        // A dry-run preview must run the same duplicate gate as the real create, so what
        // previews as "created" is what a real run would actually create.
        _tv.Ctx.Writer.CreateDecision("Alpha", "Adopt cursor pagination");
        var items = new SessionCloseItems(
            Decisions:
            [
                new CloseDecision("Adopt cursor pagination"), // duplicate of the note above
                new CloseDecision("Adopt structured logging"),
            ]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "dup preview", null, null, items, dryRun: true);

        Assert.Equal("skipped_duplicate", result.Items[0].Outcome);
        Assert.Equal("created", result.Items[1].Outcome);
        Assert.False(File.Exists(_tv.Abs("04_Decisions/Decision - Adopt structured logging.md")));
    }

    [Fact]
    public void EndSessionBatchSurvivesAnUnexpectedExceptionInOneItem()
    {
        // Lock the task file so the status update throws IOException — not a MindVaultException.
        using var fileLock = new FileStream(_tv.Abs("01_Projects/Task - Ship v1.md"),
            FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var items = new SessionCloseItems(
            Tasks:
            [
                new CloseTask(Ref: "Task - Ship v1", Status: "done"), // fails with IOException
                new CloseTask(Title: "Recovery task"),                // must still be processed
            ]);
        var result = _tv.Ctx.Sessions.Close("Alpha", "resilient batch", null, null, items);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("error", result.Items[0].Outcome);
        Assert.Equal("created", result.Items[1].Outcome);
        Assert.Contains("— resilient batch", _tv.ReadNote("06_Agent_Memory/Log - Alpha.md"));
    }

    // ---------- Lever 5: payload slimming ----------

    [Fact]
    public void SearchSnippetCharsZeroOmitsSnippets()
    {
        var withSnippets = _tv.Ctx.Search.Search("search", project: "Alpha");
        Assert.Contains(withSnippets, r => r.Snippet.Length > 0);

        var refsOnly = _tv.Ctx.Search.Search("search", project: "Alpha", snippetChars: 0);
        Assert.All(refsOnly, r => Assert.Equal("", r.Snippet));
    }

    [Fact]
    public void SearchSnippetCharsCapsLength()
    {
        var capped = _tv.Ctx.Search.Search("search", project: "Alpha", snippetChars: 10);
        Assert.All(capped, r => Assert.True(r.Snippet.Length <= 12)); // 10 chars + " …" ellipsis
    }

    [Fact]
    public void RouteCardMarkdownRendersSourcesWhenPresent()
    {
        var card = _tv.Ctx.Routes.Build("Alpha").Card!;
        Assert.NotEmpty(card.SourcePaths);
        var withSources = RouteCardService.ToMarkdown(card);
        Assert.Contains("## Sources", withSources);
        Assert.Contains(card.SourcePaths[0], withSources);

        // The includeSources=false tool path empties SourcePaths; markdown then omits the section.
        var withoutSources = RouteCardService.ToMarkdown(card with { SourcePaths = [] });
        Assert.DoesNotContain("## Sources", withoutSources);
    }

    public void Dispose() => _tv.Dispose();
}
