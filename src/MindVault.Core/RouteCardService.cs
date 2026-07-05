using System.Text;

namespace MindVault.Core;

public sealed record RouteNote(
    string Title, string Path, string? Type, string? Status, string Reason,
    int EstimatedTokens, string? SummarySnippet);

public sealed record RouteCard(
    string Project, string Confidence, string RoutePurpose,
    IReadOnlyList<RouteNote> ReadFirst,
    IReadOnlyList<RouteNote> ReadIfNeeded,
    IReadOnlyList<RouteNote> DoNotRead,
    IReadOnlyList<string> ActiveConstraints,
    IReadOnlyList<RouteNote> RelevantDecisions,
    IReadOnlyList<RouteNote> RelevantMistakes,
    IReadOnlyList<RouteNote> OpenRisks,
    IReadOnlyList<RouteNote> ActiveTasks,
    IReadOnlyList<string> SuggestedNextToolCalls,
    int TokenBudget, int EstimatedTokenSavings,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> SourcePaths);

public sealed record RouteCardOutcome(RouteCard? Card, IReadOnlyList<ProjectCandidate> Candidates);

/// <summary>
/// An agent navigation brief: the 3–5 notes to read first (with reasons, token estimates
/// and summary snippets), what can wait, what NOT to read and why, the constraints and
/// do-not-repeat rules in force, and the next tool calls — all under a token budget.
/// Deterministic: seeds come from work-context (goal/file/query) or the project's own
/// hub/map/session trail; low-value notes are excluded from every read list and surfaced
/// as doNotRead. Ambiguous projects return candidates, never a guess.
/// </summary>
public sealed class RouteCardService(VaultContext ctx)
{
    public const int DefaultTokenBudget = 4000;
    public const int DefaultReadFirst = 5;
    private const int DoNotReadCap = 8;

    public RouteCardOutcome Build(string project, string? goal = null, string? currentFile = null,
        string? query = null, ContextBudget? budget = null)
    {
        ctx.Scanner.EnsureFresh();
        var detection = ctx.ProjectDetect.Detect(project);
        if (detection.Project is null)
        {
            if (detection.Candidates.Count > 0)
                return new RouteCardOutcome(null, detection.Candidates);
            ctx.ProjectDetect.ResolveOrThrow(project); // throws the helpful not-found error
        }
        var proj = detection.Project!;

        budget ??= ContextBudget.Default;
        var maxFirst = Math.Clamp(budget.MaxNotes ?? DefaultReadFirst, 1, 10);
        var tokenBudget = budget.MaxEstimatedTokens
                          ?? (budget.MaxChars is int mc ? (mc + 3) / 4 : DefaultTokenBudget);

        var states = ctx.Db.GetFileStates();
        var warnings = new List<string>();

        int TokensOf(string path) =>
            states.TryGetValue(path, out var s) ? TokenEstimator.EstimateBytes(s.Size) : 0;

        // Do-not-read guidance. Only HARD reasons exclude a note from the read lists —
        // hygiene flags (large/no-summary, orphan, missing project) are advice, not a veto,
        // or an unsummarized hub would vanish from its own route.
        var lowValue = ctx.LowValue.Find(proj.Title);
        string[] hardReasons =
            ["archived", "superseded", "rejected decision", "hidden by feedback",
             "negative feedback", "raw thought"];
        var lowSet = lowValue.Notes
            .Where(n => n.Reasons.Any(r => hardReasons.Any(h => r.StartsWith(h, StringComparison.Ordinal))))
            .Select(n => n.Path)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var doNotRead = lowValue.Notes
            .OrderByDescending(n => n.Reasons.Count)
            .ThenBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .Take(DoNotReadCap)
            .Select(n => new RouteNote(n.Title, n.Path, n.Type, n.Status,
                string.Join("; ", n.Reasons), TokensOf(n.Path), null))
            .ToList();
        if (lowValue.Truncated)
            warnings.Add("low-value list truncated — run `mindvault low-value` for the full picture");

        var pc = ctx.Projects.Get(proj.Title, 5);

        // Seed from exactly one input when given; otherwise the project's own trail.
        WorkContextResult? wc = null;
        string purpose;
        if (!string.IsNullOrWhiteSpace(currentFile))
        {
            wc = ctx.WorkContext.Get(proj.Title, currentFile: currentFile);
            purpose = $"edit {currentFile!.Trim()} without violating recorded memory";
        }
        else if (!string.IsNullOrWhiteSpace(goal ?? query))
        {
            wc = ctx.WorkContext.Get(proj.Title, query: (goal ?? query)!.Trim());
            purpose = goal is not null ? $"achieve: {goal.Trim()}" : $"answer: {query!.Trim()}";
        }
        else
        {
            purpose = $"general orientation on {proj.Title}";
        }
        if (wc is not null) warnings.AddRange(wc.Warnings);

        // Candidate stream in priority order; low-value paths never enter a read list.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<RouteNote>();
        void Offer(string? path, string reason)
        {
            if (path is null || lowSet.Contains(path) || !seen.Add(path)) return;
            var n = ctx.Db.FindByPath(path);
            if (n is null) return;
            candidates.Add(new RouteNote(n.Title, n.Path, n.Type, n.Status, reason,
                TokensOf(n.Path), null));
        }

        // The map block lives on the hub now, so the hub is the single orientation read.
        var hubHasMap = false;
        try
        {
            hubHasMap = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, proj.Path))
                .Contains(MapService.MarkerStart, StringComparison.Ordinal);
        }
        catch (IOException) { /* presence is best-effort */ }
        Offer(proj.Path, hubHasMap
            ? "project hub — map block orients everything (goal, decisions, risks, health) in one read"
            : "project hub — goal and non-negotiables");
        if (!hubHasMap)
            warnings.Add($"no map block on the {proj.Title} hub — `mindvault map create` would give agents a cheaper entry point");

        if (wc is not null)
        {
            foreach (var r in wc.SuggestedReads) Offer(r.Path, r.Reason);
        }
        else
        {
            Offer($"06_Agent_Memory/Log - {proj.Stem}.md", "where the last session stopped");
            foreach (var d in pc.RecentDecisions) Offer(d.Path, "recent decision in force");
            foreach (var t in pc.ActiveTasks) Offer(t.Path, "active task");
        }

        var readFirst = candidates.Take(maxFirst).ToList();
        var readIfNeeded = candidates.Skip(maxFirst).Take(5).ToList();

        // Budget enforcement: overflow moves down, never silently disappears.
        while (readFirst.Count > 1 && readFirst.Sum(n => n.EstimatedTokens) > tokenBudget)
        {
            var demoted = readFirst[^1];
            readFirst.RemoveAt(readFirst.Count - 1);
            readIfNeeded.Insert(0, demoted);
        }
        readFirst = WithSnippets(readFirst, budget.MaxSnippetChars);
        readIfNeeded = WithSnippets(readIfNeeded, budget.MaxSnippetChars);

        // Typed context: from the seeded work-context when there is one, else the hub view.
        List<RouteNote> FromWc(IReadOnlyList<WorkContextItem> items, int cap = 5) =>
            items.Take(cap).Select(i => new RouteNote(i.Title, i.Path, i.Type, i.Status,
                i.Reason, TokensOf(i.Path), null)).ToList();
        List<RouteNote> FromPc(IEnumerable<ContextItem> items, string? type, string reason, int cap = 5) =>
            items.Take(cap).Select(i => new RouteNote(i.Title, i.Path, type, i.Status,
                reason, TokensOf(i.Path), null)).ToList();

        var decisions = wc is not null && wc.Decisions.Count > 0
            ? FromWc(wc.Decisions)
            : FromPc(pc.RecentDecisions, "decision", "decision in force");
        var mistakes = wc is not null && wc.Mistakes.Count > 0
            ? FromWc(wc.Mistakes)
            : BrainQueries.Mistakes(ctx, proj.Title).Take(5)
                .Select(m => new RouteNote(m.Title, m.Path, m.Type, m.Status,
                    "recorded mistake — do not repeat", TokensOf(m.Path), null)).ToList();
        var risks = wc is not null && wc.Risks.Count > 0
            ? FromWc(wc.Risks)
            : FromPc(pc.OpenRisks, "risk", "open risk");
        var tasks = wc is not null && wc.Tasks.Count > 0
            ? FromWc(wc.Tasks)
            : FromPc(pc.ActiveTasks.Concat(pc.BlockedTasks), "task", "active task");

        var constraints = pc.NonNegotiables
            .Concat(pc.Constraints.Select(c => c.Title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var toolCalls = new List<string>();
        if (readFirst.Count > 0)
            toolCalls.Add($"mindvault_read_note {{\"noteRef\": \"{readFirst[0].Path}\"}}");
        if (wc is null)
            toolCalls.Add($"mindvault_build_context_capsule {{\"project\": \"{proj.Title}\", \"mode\": \"coding\"}}");
        var searchTerms = SearchService.Tokenize(goal ?? query ?? currentFile ?? "").Take(3).ToList();
        if (searchTerms.Count > 0)
        {
            toolCalls.Add($"mindvault_search {{\"query\": \"{string.Join(" ", searchTerms)}\", " +
                          $"\"project\": \"{proj.Title}\"}} — only if the reads above leave the goal unclear");
        }

        var readFirstTokens = readFirst.Sum(n => n.EstimatedTokens);
        var baseline = readFirst.Concat(readIfNeeded).Concat(doNotRead)
            .Concat(decisions).Concat(mistakes).Concat(risks).Concat(tasks)
            .DistinctBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .Sum(n => n.EstimatedTokens);
        var savings = Math.Max(0, baseline - readFirstTokens);

        var sourcePaths = readFirst.Concat(readIfNeeded)
            .Concat(decisions).Concat(mistakes).Concat(risks).Concat(tasks)
            .Select(n => n.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var card = new RouteCard(proj.Title, detection.Confidence, purpose,
            readFirst, readIfNeeded, doNotRead, constraints,
            decisions, mistakes, risks, tasks, toolCalls,
            tokenBudget, savings, warnings, sourcePaths);
        return new RouteCardOutcome(card, []);
    }

    /// <summary>Snippets only for notes the agent is told to read — a few file reads, not a sweep.
    /// A generated summary line wins; otherwise the first plain-text line of the body.</summary>
    private List<RouteNote> WithSnippets(List<RouteNote> notes, int maxSnippetChars)
    {
        var result = new List<RouteNote>(notes.Count);
        foreach (var n in notes)
        {
            string? snippet = null;
            try
            {
                var raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, n.Path))
                    .Replace("\r\n", "\n");
                FrontmatterCodec.TryExtract(raw, out _, out var body);
                snippet = SummaryService.ExtractSummaryLine(body) ?? FirstTextLine(body);
                if (snippet is { Length: > 0 } && snippet.Length > maxSnippetChars)
                    snippet = snippet[..maxSnippetChars].TrimEnd() + " …";
            }
            catch (IOException) { /* snippet is best-effort */ }
            result.Add(n with { SummarySnippet = snippet });
        }
        return result;
    }

    private static string? FirstTextLine(string body) =>
        body.Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(t => t.Length > 0 && !t.StartsWith('#') && !t.StartsWith('-') &&
                                 !t.StartsWith('*') && !t.StartsWith('>') && !t.StartsWith('|') &&
                                 !t.StartsWith("<!--", StringComparison.Ordinal) &&
                                 !t.StartsWith("```", StringComparison.Ordinal) &&
                                 !t.StartsWith("[[", StringComparison.Ordinal));

    public static string ToMarkdown(RouteCard card)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Route Card — {card.Project}");
        sb.AppendLine();
        sb.AppendLine($"purpose: {card.RoutePurpose}");
        sb.AppendLine($"confidence: {card.Confidence} · token budget: {card.TokenBudget} · " +
                      $"estimated savings vs reading every candidate: ~{card.EstimatedTokenSavings} tokens");

        void Notes(string heading, IReadOnlyList<RouteNote> notes)
        {
            if (notes.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine($"## {heading}");
            sb.AppendLine();
            foreach (var n in notes)
            {
                sb.AppendLine($"- {n.Title} ({n.Path}) — {n.Reason} [~{n.EstimatedTokens} tokens]");
                if (n.SummarySnippet is { Length: > 0 } s) sb.AppendLine($"  > {s}");
            }
        }
        void Lines(string heading, IReadOnlyList<string> lines)
        {
            if (lines.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine($"## {heading}");
            sb.AppendLine();
            foreach (var l in lines) sb.AppendLine($"- {l}");
        }

        Notes("Read First", card.ReadFirst);
        Notes("Read If Needed", card.ReadIfNeeded);
        Notes("Do Not Read", card.DoNotRead);
        Lines("Active Constraints", card.ActiveConstraints);
        Notes("Relevant Decisions", card.RelevantDecisions);
        Notes("Do Not Repeat (Mistakes)", card.RelevantMistakes);
        Notes("Open Risks", card.OpenRisks);
        Notes("Active Tasks", card.ActiveTasks);
        Lines("Suggested Next Tool Calls", card.SuggestedNextToolCalls);
        Lines("Warnings", card.Warnings);
        return sb.ToString().Replace("\r\n", "\n");
    }
}
