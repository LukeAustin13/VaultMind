namespace MindVault.Core;

public sealed record ReadPlanStep(
    int Order, string Action, string? Note, string Reason, string ExpectedUse);

public sealed record ReadPlan(
    string Project, string? Goal, int MaxReads,
    IReadOnlyList<ReadPlanStep> Steps,
    IReadOnlyList<string> StopWhen,
    IReadOnlyList<RouteNote> DoNotRead,
    string? FallbackSearch,
    IReadOnlyList<string> Warnings);

public sealed record ReadPlanOutcome(ReadPlan? Plan, IReadOnlyList<ProjectCandidate> Candidates);

/// <summary>
/// A strict, ordered tool-call plan (the route card is the briefing; this is the
/// itinerary): at most five reads, maps and hubs before raw notes, an explicit stop
/// condition, do-not-read guidance, and a narrowed search as the only sanctioned
/// fallback. Built on the route card so both always agree.
/// </summary>
public sealed class ReadPlanService(VaultContext ctx)
{
    public const int DefaultMaxReads = 5;

    public static readonly string[] StopConditions =
    [
        "the current goal and its constraints are clear",
        "active risks and do-not-repeat rules are known",
        "you can state the next concrete change without another read",
    ];

    public ReadPlanOutcome Build(string project, string? goal = null, string? currentFile = null,
        int maxReads = DefaultMaxReads)
    {
        maxReads = Math.Clamp(maxReads, 1, DefaultMaxReads);
        var outcome = ctx.Routes.Build(project, goal, currentFile,
            budget: new ContextBudget(MaxNotes: maxReads));
        if (outcome.Card is null)
            return new ReadPlanOutcome(null, outcome.Candidates);
        var card = outcome.Card;

        var steps = new List<ReadPlanStep>();
        foreach (var n in card.ReadFirst.Take(maxReads))
        {
            steps.Add(new ReadPlanStep(steps.Count + 1, "read_note", n.Path, n.Reason,
                ExpectedUse(n.Type)));
        }
        if (!string.IsNullOrWhiteSpace(currentFile) && steps.Count < maxReads)
        {
            steps.Add(new ReadPlanStep(steps.Count + 1, "get_work_context", null,
                $"memory touching {currentFile!.Trim()}",
                "confirm no decision, constraint or mistake governs this edit"));
        }

        var fallback = card.SuggestedNextToolCalls
            .FirstOrDefault(c => c.StartsWith("mindvault_search", StringComparison.Ordinal));

        return new ReadPlanOutcome(new ReadPlan(
            card.Project, goal, maxReads, steps, StopConditions,
            card.DoNotRead, fallback, card.Warnings), []);
    }

    private static string ExpectedUse(string? type) => type?.ToLowerInvariant() switch
    {
        "map" => "orient: decisions, risks and do-not-repeat rules in one read",
        "project" => "know the goal and non-negotiables",
        "decision" => "know what is already decided before changing anything",
        "mistake" => "avoid repeating a recorded mistake",
        "memory" => "see where the last session stopped",
        "task" or "bug" or "feature" => "understand the active work's scope",
        "risk" => "know what could break",
        "constraint" => "know the hard limits on this work",
        "architecture" => "understand how the pieces fit before structural changes",
        _ => "background context for the goal",
    };
}
