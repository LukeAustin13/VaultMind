using System.Text;

namespace MindVault.Core;

public sealed record CapsuleItem(string Title, string Path, string? Status, string? Updated, string Reason);

public sealed record ContextCapsule(
    string Project,
    string Confidence,
    string? ResolvedVia,
    string Mode,
    string? CurrentGoal,
    IReadOnlyList<string> NonNegotiables,
    IReadOnlyList<CapsuleItem> ActiveDecisions,
    IReadOnlyList<string> SupersededDecisionWarnings,
    IReadOnlyList<CapsuleItem> OpenTasks,
    IReadOnlyList<CapsuleItem> BlockedTasks,
    IReadOnlyList<CapsuleItem> OpenRisks,
    IReadOnlyList<CapsuleItem> Constraints,
    IReadOnlyList<string> RecentImplementationLogs,
    IReadOnlyList<CapsuleItem> KnownMistakes,
    IReadOnlyList<string> DoNotRepeat,
    IReadOnlyList<ContextRead> SuggestedReads,
    IReadOnlyList<string> OpenQuestions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> SourcePaths);

/// <summary>Either a capsule, or the candidate list when project identity is ambiguous — never a guess.</summary>
public sealed record CapsuleOutcome(ContextCapsule? Capsule, IReadOnlyList<ProjectCandidate> Candidates);

/// <summary>
/// Mode-aware, char-budgeted context capsules: everything an agent should hold in its head
/// before working, as refs + reasons, never note bodies. Built entirely from the existing
/// deterministic services (project context, decisions, mistakes) plus feedback signals
/// (hidden excluded, pinned/useful boosted). Trimming under the budget removes items from
/// the mode's lowest-priority sections first, so what survives is what the mode cares about.
/// </summary>
public sealed class CapsuleService(VaultContext ctx)
{
    public const int DefaultBudget = 8000;

    public static readonly IReadOnlyList<string> Modes =
        ["coding", "debugging", "review", "planning", "handoff", "release", "architecture"];

    /// <summary>Section keys in render/keep priority per mode (earlier = kept longest).</summary>
    private static readonly Dictionary<string, string[]> ModeOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["coding"] = ["goal", "nonNegotiables", "activeDecisions", "openTasks", "blockedTasks", "constraints", "doNotRepeat", "knownMistakes", "openRisks", "recentLogs", "openQuestions"],
        ["debugging"] = ["goal", "knownMistakes", "doNotRepeat", "recentLogs", "openRisks", "activeDecisions", "constraints", "openTasks", "blockedTasks", "openQuestions", "nonNegotiables"],
        ["review"] = ["activeDecisions", "constraints", "nonNegotiables", "openRisks", "knownMistakes", "doNotRepeat", "goal", "openTasks", "blockedTasks", "recentLogs", "openQuestions"],
        ["planning"] = ["goal", "openQuestions", "openTasks", "blockedTasks", "openRisks", "activeDecisions", "constraints", "nonNegotiables", "knownMistakes", "doNotRepeat", "recentLogs"],
        ["handoff"] = ["recentLogs", "openTasks", "blockedTasks", "openRisks", "goal", "activeDecisions", "knownMistakes", "doNotRepeat", "constraints", "openQuestions", "nonNegotiables"],
        ["release"] = ["openRisks", "constraints", "blockedTasks", "activeDecisions", "knownMistakes", "doNotRepeat", "goal", "openTasks", "recentLogs", "openQuestions", "nonNegotiables"],
        ["architecture"] = ["activeDecisions", "constraints", "nonNegotiables", "goal", "openRisks", "knownMistakes", "doNotRepeat", "openQuestions", "openTasks", "blockedTasks", "recentLogs"],
    };

    public CapsuleOutcome Build(string project, string mode = "coding", int maxChars = DefaultBudget)
    {
        mode = (mode ?? "coding").Trim().ToLowerInvariant();
        if (!Modes.Contains(mode))
            throw new MindVaultException($"Unknown capsule mode '{mode}'. Modes: {string.Join(", ", Modes)}.");
        maxChars = Math.Clamp(maxChars, 1000, 32_000);

        var detection = ctx.ProjectDetect.Detect(project);
        if (detection.Project is null)
        {
            if (detection.Candidates.Count > 0) return new CapsuleOutcome(null, detection.Candidates);
            ctx.ProjectDetect.ResolveOrThrow(project); // throws the helpful not-found error
        }
        var proj = detection.Project!;
        var context = ctx.Projects.Get(proj.Title, limit: 10);
        var names = ctx.ProjectDetect.QueryNamesFor(proj);
        var fb = ctx.Feedback.LoadAll();

        FeedbackState FbFor(string path) =>
            fb.TryGetValue(SlugHelper.NormalizeWiki(System.IO.Path.GetFileNameWithoutExtension(path)), out var s)
                ? s
                : FeedbackState.None;

        List<CapsuleItem> Items(IEnumerable<ContextItem> src, string reason) =>
            src.Where(i => !FbFor(i.Path).Hidden)
                .OrderByDescending(i => FbFor(i.Path).Pinned)
                .ThenByDescending(i => FbFor(i.Path).Score)
                .Select(i => new CapsuleItem(i.Title, i.Path, i.Status, i.Updated,
                    FbFor(i.Path).Pinned ? $"{reason}; pinned" : reason))
                .ToList();

        var activeDecisions = ctx.Decisions.List(proj.Title)
            .Where(d => !FbFor(d.Path).Hidden)
            .OrderByDescending(d => FbFor(d.Path).Pinned)
            .Select(d => new CapsuleItem(d.Title, d.Path, d.Status, d.Updated,
                FbFor(d.Path).Pinned ? "decision in force; pinned" : "decision in force"))
            .Take(10).ToList();

        var supersededWarnings = ctx.Decisions.List(proj.Title, includeAll: true)
            .Where(d => string.Equals(d.Status, "superseded", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select(d => $"'{d.Title}' is superseded" +
                         (d.SupersededBy.Count > 0 ? $" by {string.Join(", ", d.SupersededBy)}" : "") +
                         $" — do not follow it ({d.Path})")
            .ToList();

        var mistakes = BrainQueries.Mistakes(ctx, proj.Title)
            .Where(m => !FbFor(m.Path).Hidden).Take(8).ToList();
        var knownMistakes = mistakes
            .Select(m => new CapsuleItem(m.Title, m.Path, m.Status, m.Updated, "active lesson"))
            .ToList();
        var doNotRepeat = new List<string>();
        foreach (var m in mistakes.Take(5))
        {
            string? prevention = null;
            try
            {
                var raw = File.ReadAllText(ctx.Resolver.AbsolutePathOf(m)).Replace("\r\n", "\n");
                FrontmatterCodec.TryExtract(raw, out _, out var body);
                // The prevention rule IS the do-not-repeat rule; the lesson text is fallback.
                prevention = SectionExtractor.GetSectionText(body, "Prevention Task", 200)
                             ?? SectionExtractor.GetSectionText(body, "How To Avoid It", 200);
            }
            catch (IOException) { /* the title alone still carries the lesson */ }
            var label = m.Title.StartsWith("Mistake:", StringComparison.OrdinalIgnoreCase)
                ? m.Title["Mistake:".Length..].Trim()
                : m.Title;
            doNotRepeat.Add(prevention is null ? label : $"{label}: {prevention}");
        }

        var openTasks = Items(context.ActiveTasks, "open/active task");
        var blockedTasks = Items(context.BlockedTasks, "blocked task");
        var openRisks = Items(context.OpenRisks, "open risk");
        var constraints = Items(context.Constraints, "constraint");
        var nonNegotiables = context.NonNegotiables.ToList();
        var openQuestions = context.KnownUnknowns.ToList();
        var recentLogs = context.RecentImplementationLogs.ToList();

        var pinnedReads = ctx.Db.Query(projectNames: names, statusNot: "archived", limit: 100)
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Where(n => FbFor(n.Path).Pinned)
            .Take(3)
            .Select(n => new ContextRead(n.Path, "pinned by feedback"));
        var suggestedReads = pinnedReads
            .Concat(context.RecommendedNextReads.Where(r => !FbFor(r.Path).Hidden))
            .DistinctBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .Take(6).ToList();

        // The hub's Goal section wins; a generated summary line is the honest fallback.
        var goal = context.CurrentGoal;
        if (goal is null)
        {
            try
            {
                var hubRaw = File.ReadAllText(ctx.Resolver.AbsolutePathOf(proj)).Replace("\r\n", "\n");
                FrontmatterCodec.TryExtract(hubRaw, out _, out var hubBody);
                if (SummaryService.ExtractSummaryLine(hubBody) is { } line)
                    goal = $"{line} (from the hub's generated summary — no Goal section)";
            }
            catch (IOException) { /* goal stays null */ }
        }

        // Budget: drop items from the mode's lowest-priority sections until the markdown fits.
        var order = ModeOrder[mode];
        var trimmable = new Dictionary<string, System.Collections.IList>(StringComparer.Ordinal)
        {
            ["nonNegotiables"] = nonNegotiables,
            ["activeDecisions"] = activeDecisions,
            ["openTasks"] = openTasks,
            ["blockedTasks"] = blockedTasks,
            ["openRisks"] = openRisks,
            ["constraints"] = constraints,
            ["recentLogs"] = recentLogs,
            ["knownMistakes"] = knownMistakes,
            ["doNotRepeat"] = doNotRepeat,
            ["openQuestions"] = openQuestions,
        };

        ContextCapsule Snapshot() => new(proj.Title, detection.Confidence, detection.MatchedVia, mode,
            goal, nonNegotiables, activeDecisions, supersededWarnings, openTasks, blockedTasks,
            openRisks, constraints, recentLogs, knownMistakes, doNotRepeat, suggestedReads,
            openQuestions, context.Warnings,
            SourcePathsOf(proj, activeDecisions, openTasks, blockedTasks, openRisks, constraints, knownMistakes));

        var capsule = Snapshot();
        for (var guard = 0; guard < 300 && ToMarkdown(capsule).Length > maxChars; guard++)
        {
            var trimmed = false;
            foreach (var key in order.Reverse())
            {
                if (trimmable.TryGetValue(key, out var list) && list.Count > 0)
                {
                    list.RemoveAt(list.Count - 1);
                    trimmed = true;
                    break;
                }
            }
            if (!trimmed)
            {
                if (goal is { Length: > 400 }) goal = goal[..400].TrimEnd() + " …";
                capsule = Snapshot();
                break;
            }
            capsule = Snapshot();
        }
        return new CapsuleOutcome(capsule, []);
    }

    private static IReadOnlyList<string> SourcePathsOf(NoteSummary proj, params IEnumerable<CapsuleItem>[] groups) =>
        groups.SelectMany(g => g).Select(i => i.Path)
            .Prepend(proj.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string ToMarkdown(ContextCapsule c)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Context Capsule — {c.Project} [{c.Mode}]");
        sb.AppendLine($"confidence: {c.Confidence}" + (c.ResolvedVia is null ? "" : $" (via {c.ResolvedVia})"));

        foreach (var key in ModeOrder[c.Mode])
        {
            switch (key)
            {
                case "goal" when c.CurrentGoal is { Length: > 0 }:
                    Section(sb, "Current Goal"); sb.AppendLine(c.CurrentGoal); break;
                case "nonNegotiables": Bullets(sb, "Non-Negotiables", c.NonNegotiables); break;
                case "activeDecisions": ItemLines(sb, "Decisions In Force", c.ActiveDecisions); break;
                case "openTasks": ItemLines(sb, "Open Tasks", c.OpenTasks); break;
                case "blockedTasks": ItemLines(sb, "Blocked Tasks", c.BlockedTasks); break;
                case "openRisks": ItemLines(sb, "Open Risks", c.OpenRisks); break;
                case "constraints": ItemLines(sb, "Constraints", c.Constraints); break;
                case "recentLogs": Bullets(sb, "Recent Implementation Logs", c.RecentImplementationLogs); break;
                case "knownMistakes": ItemLines(sb, "Known Mistakes", c.KnownMistakes); break;
                case "doNotRepeat": Bullets(sb, "Do Not Repeat", c.DoNotRepeat); break;
                case "openQuestions": Bullets(sb, "Open Questions", c.OpenQuestions); break;
            }
        }
        Bullets(sb, "Superseded — Do Not Follow", c.SupersededDecisionWarnings);
        Bullets(sb, "Warnings", c.Warnings);
        if (c.SuggestedReads.Count > 0)
        {
            Section(sb, "Suggested Reads");
            foreach (var r in c.SuggestedReads) sb.AppendLine($"- {r.Path} — {r.Reason}");
        }
        if (c.SourcePaths.Count > 0)
        {
            Section(sb, "Sources");
            foreach (var p in c.SourcePaths) sb.AppendLine($"- {p}");
        }
        return sb.ToString().Replace("\r\n", "\n");
    }

    private static void Section(StringBuilder sb, string title)
    {
        sb.AppendLine();
        sb.AppendLine($"## {title}");
    }

    private static void Bullets(StringBuilder sb, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        Section(sb, title);
        foreach (var i in items) sb.AppendLine($"- {i}");
    }

    private static void ItemLines(StringBuilder sb, string title, IReadOnlyList<CapsuleItem> items)
    {
        if (items.Count == 0) return;
        Section(sb, title);
        foreach (var i in items)
            sb.AppendLine($"- {i.Title}{(i.Status is null ? "" : $" [{i.Status}]")} — {i.Reason} ({i.Path})");
    }
}
