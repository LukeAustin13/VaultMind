using System.Text.Json;

namespace MindVault.Core;

public sealed record BriefItem(string Title, string Path, string? Status, string Reason);

public sealed record BriefRead(string Path, int EstimatedTokens, string Reason);

public sealed record BriefDoNotRead(string Path, string Reason);

public sealed record BriefDelta(
    int Decisions, int Tasks, int Risks, int Mistakes, int Sessions, int Reviews, int Notes,
    IReadOnlyList<RecallItem> Items,
    IReadOnlyList<string> Warnings);

/// <summary>
/// The one-call session brief: everything an agent needs to start work on a project, composed
/// once and budgeted. Each fact appears exactly ONCE — decisions/tasks/risks/constraints as
/// the facts, readFirst/doNotRead as the notes to read, delta as what changed since the last
/// handoff. No section repeats another's items.
/// </summary>
public sealed record SessionBriefResult(
    string Project,
    string Confidence,
    string? ResolvedVia,
    string? Goal,
    IReadOnlyList<string> NonNegotiables,
    IReadOnlyList<BriefItem> DecisionsInForce,
    IReadOnlyList<string> DoNotRepeat,
    IReadOnlyList<BriefItem> KnownMistakes,
    IReadOnlyList<BriefItem> OpenTasks,
    IReadOnlyList<BriefItem> BlockedTasks,
    IReadOnlyList<BriefItem> OpenRisks,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<BriefRead> ReadFirst,
    IReadOnlyList<BriefDoNotRead> DoNotRead,
    BriefDelta? DeltaSinceLastHandoff,
    IReadOnlyList<string> Warnings,
    string LogNote,
    bool LogNoteCreated,
    string? Task);

/// <summary>
/// Composes the <see cref="SessionBriefResult"/> for start_session. Calls
/// <see cref="ProjectContextService.Get"/> exactly once and shares that result with the route
/// internals so the brief never triggers 2-3 identical project queries. Trims to a char budget
/// by dropping the least-load-bearing content first.
/// </summary>
public sealed class BriefService(VaultContext ctx)
{
    public const int DefaultBudget = 6000;

    private const int MaxDecisions = 8;
    private const int MaxDoNotRepeat = 5;
    private const int MaxTasks = 8;
    private const int MaxRisks = 5;
    private const int MaxReadFirst = 5;
    private const int MaxDoNotRead = 8;
    private const int MaxDeltaItems = 10;
    private const int MaxGoalChars = 300;

    public SessionBriefResult Compose(string project, string? task, bool logNoteCreated, string logNote,
        int maxChars = DefaultBudget)
    {
        maxChars = Math.Clamp(maxChars, 1000, 32_000);

        var detection = ctx.ProjectDetect.Detect(project);
        var (proj, matchedVia) = detection.Project is not null
            ? (detection.Project, detection.MatchedVia!)
            : ctx.ProjectDetect.ResolveOrThrow(project); // throws with candidates / not-found
        var confidence = detection.Project is not null ? detection.Confidence : "exact";

        // The single project-context query, shared with the route internals below.
        var pc = ctx.Projects.Get(proj.Title, limit: MaxTasks);

        var warnings = new List<string>(pc.Warnings);

        var goal = pc.CurrentGoal;
        if (goal is { Length: > MaxGoalChars })
            goal = goal[..MaxGoalChars].TrimEnd() + " …";

        var nonNegotiables = pc.NonNegotiables.ToList();
        var constraints = pc.Constraints.Select(c => c.Title)
            .Concat(pc.NonNegotiables)
            .Where(c => !nonNegotiables.Contains(c, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Except(nonNegotiables, StringComparer.OrdinalIgnoreCase)
            .Take(MaxRisks)
            .ToList();

        var decisions = pc.RecentDecisions.Take(MaxDecisions)
            .Select(d => new BriefItem(d.Title, d.Path, d.Status, "decision in force"))
            .ToList();

        var mistakeNotes = BrainQueries.Mistakes(ctx, proj.Title).Take(MaxDecisions).ToList();
        var doNotRepeat = CapsuleService.DoNotRepeatRules(ctx, mistakeNotes, MaxDoNotRepeat);
        var knownMistakes = mistakeNotes.Take(MaxDoNotRepeat)
            .Select(m => new BriefItem(m.Title, m.Path, m.Status, "active lesson"))
            .ToList();

        var openTasks = pc.ActiveTasks.Take(MaxTasks)
            .Select(t => new BriefItem(t.Title, t.Path, t.Status, "open/active task")).ToList();
        var blockedTasks = pc.BlockedTasks.Take(MaxTasks)
            .Select(t => new BriefItem(t.Title, t.Path, t.Status, "blocked task")).ToList();
        var openRisks = pc.OpenRisks.Take(MaxRisks)
            .Select(r => new BriefItem(r.Title, r.Path, r.Status, "open risk")).ToList();

        // readFirst / doNotRead come from the route internals, seeded with the SAME pc so no
        // second project query runs. Paths that already appear as facts are not repeated here —
        // readFirst is about where to look, the fact lists are about what is true.
        var factPaths = decisions.Select(d => d.Path)
            .Concat(openTasks.Select(t => t.Path))
            .Concat(blockedTasks.Select(t => t.Path))
            .Concat(openRisks.Select(r => r.Path))
            .Concat(knownMistakes.Select(m => m.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var route = ctx.Routes.Build(proj.Title,
            budget: new ContextBudget(MaxNotes: MaxReadFirst),
            sharedContext: pc).Card;
        var readFirst = new List<BriefRead>();
        var doNotRead = new List<BriefDoNotRead>();
        if (route is not null)
        {
            // A note already surfaced as a fact (decision/task/risk/mistake) is not repeated in
            // readFirst — the agent knows it is true; readFirst is only for notes it must open.
            readFirst = route.ReadFirst
                .Where(n => !factPaths.Contains(n.Path))
                .Select(n => new BriefRead(n.Path, n.EstimatedTokens, n.Reason))
                .Take(MaxReadFirst)
                .ToList();
            doNotRead = route.DoNotRead
                .Select(n => new BriefDoNotRead(n.Path, n.Reason))
                .Take(MaxDoNotRead)
                .ToList();
            foreach (var w in route.Warnings.Where(w => !warnings.Contains(w)))
                warnings.Add(w);
        }

        var delta = BuildDelta(proj.Title, logNote, warnings);

        var brief = new SessionBriefResult(
            proj.Title, confidence, matchedVia, goal, nonNegotiables, decisions, doNotRepeat,
            knownMistakes, openTasks, blockedTasks, openRisks, constraints, readFirst, doNotRead,
            delta, warnings, logNote, logNoteCreated, task);

        return TrimToBudget(brief, maxChars);
    }

    /// <summary>Delta since the project's last handoff: counts per group plus up to 10 changed items.</summary>
    private BriefDelta? BuildDelta(string project, string logNote, List<string> warnings)
    {
        var at = ctx.Sessions.MostRecentHandoffAt(project);
        if (at is null)
        {
            warnings.Add("No prior handoff for this project — deltaSinceLastHandoff is null " +
                         "(this is likely the first session).");
            return null;
        }

        var recall = ctx.RecallSvc.Recall(project, since: "last-handoff");

        // The project's own session log always changed when the reference handoff was written —
        // counting it would inflate every same-day delta, so it is excluded from all groups.
        List<RecallItem> Filter(IReadOnlyList<RecallItem> group) =>
            group.Where(i => !string.Equals(i.Path, logNote, StringComparison.OrdinalIgnoreCase)).ToList();
        var decisions = Filter(recall.Decisions);
        var tasks = Filter(recall.Tasks);
        var risks = Filter(recall.Risks);
        var mistakes = Filter(recall.Mistakes);
        var sessions = Filter(recall.Sessions);
        var reviews = Filter(recall.Reviews);
        var notes = Filter(recall.Notes);

        var items = decisions
            .Concat(tasks).Concat(risks).Concat(mistakes)
            .Concat(sessions).Concat(reviews).Concat(notes)
            .OrderByDescending(i => i.Date, StringComparer.Ordinal)
            .Take(MaxDeltaItems)
            .ToList();
        // Recall's own warnings (e.g. group truncation) belong on the brief, not swallowed.
        foreach (var w in recall.Warnings.Where(w => !warnings.Contains(w)))
            warnings.Add(w);

        return new BriefDelta(
            decisions.Count, tasks.Count, risks.Count, mistakes.Count,
            sessions.Count, reviews.Count, notes.Count,
            items, []);
    }

    /// <summary>
    /// Trims the brief under the char budget by dropping the least load-bearing content first:
    /// knownMistakes, then doNotRead reasons, then delta items (counts kept), then openRisks,
    /// then readFirst snippets (here: the read reasons). Facts an agent must not miss — goal,
    /// decisions, tasks, do-not-repeat rules, constraints — are kept longest.
    /// </summary>
    private static SessionBriefResult TrimToBudget(SessionBriefResult brief, int maxChars)
    {
        var current = brief;
        for (var step = 0; step < 8 && Measure(current) > maxChars; step++)
        {
            current = step switch
            {
                0 => current with { KnownMistakes = [] },
                1 => current with
                {
                    DoNotRead = current.DoNotRead.Select(d => d with { Reason = "" }).ToList(),
                },
                2 => current with
                {
                    DeltaSinceLastHandoff = current.DeltaSinceLastHandoff is { } d
                        ? d with { Items = [] }
                        : null,
                },
                3 => current with { OpenRisks = [] },
                4 => current with
                {
                    ReadFirst = current.ReadFirst.Select(r => r with { Reason = "" }).ToList(),
                },
                5 => current with { DoNotRead = [] },
                6 => current with { ReadFirst = [] },
                _ => current with { Constraints = [] },
            };
        }
        return current;
    }

    private static readonly JsonSerializerOptions MeasureOptions = new() { WriteIndented = false };

    private static int Measure(SessionBriefResult brief) =>
        JsonSerializer.Serialize(brief, MeasureOptions).Length;

    /// <summary>Compact human rendering for the CLI: the sections a person scans before coding.</summary>
    public static string ToText(SessionBriefResult b)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Session Brief — {b.Project}");
        if (b.Task is { Length: > 0 }) sb.AppendLine($"task: {b.Task}");
        sb.AppendLine($"log note: {b.LogNote}{(b.LogNoteCreated ? " (created)" : "")}");
        if (b.Goal is { Length: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"Goal: {b.Goal}");
        }

        void Items(string title, IReadOnlyList<BriefItem> items)
        {
            if (items.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine($"{title}:");
            foreach (var i in items)
                sb.AppendLine($"  - {i.Title}{(i.Status is null ? "" : $" [{i.Status}]")} ({i.Path})");
        }
        void Lines(string title, IReadOnlyList<string> lines)
        {
            if (lines.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine($"{title}:");
            foreach (var l in lines) sb.AppendLine($"  - {l}");
        }

        Lines("Non-negotiables", b.NonNegotiables);
        Items("Decisions in force", b.DecisionsInForce);
        Lines("Do not repeat", b.DoNotRepeat);
        Items("Open tasks", b.OpenTasks);
        Items("Blocked tasks", b.BlockedTasks);
        Items("Open risks", b.OpenRisks);
        Lines("Constraints", b.Constraints);

        if (b.ReadFirst.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Read first:");
            foreach (var r in b.ReadFirst)
                sb.AppendLine($"  - {r.Path} (~{r.EstimatedTokens} tokens){(r.Reason.Length > 0 ? $" — {r.Reason}" : "")}");
        }

        sb.AppendLine();
        if (b.DeltaSinceLastHandoff is { } d)
        {
            sb.AppendLine("Since last handoff: " +
                $"{d.Decisions} decision(s), {d.Tasks} task(s), {d.Risks} risk(s), {d.Mistakes} mistake(s), " +
                $"{d.Sessions} session(s), {d.Reviews} review(s), {d.Notes} note(s) changed");
            foreach (var i in d.Items)
                sb.AppendLine($"  - [{i.Change}] {i.Title} ({i.Path})");
        }
        else
        {
            sb.AppendLine("Since last handoff: (no prior handoff)");
        }

        Lines("Warnings", b.Warnings);
        return sb.ToString().Replace("\r\n", "\n");
    }
}
