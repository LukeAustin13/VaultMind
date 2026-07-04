namespace MindVault.Core;

public sealed record ContextItem(string Title, string Path, string? Status, string? Updated);

public sealed record ContextRead(string Path, string Reason);

public sealed record ProjectContextResult(
    string Project,
    ContextItem ProjectNote,
    string? CurrentGoal,
    IReadOnlyList<string> NonNegotiables,
    IReadOnlyList<ContextItem> ActiveTasks,
    IReadOnlyList<ContextItem> BlockedTasks,
    IReadOnlyList<ContextItem> RecentDecisions,
    IReadOnlyList<ContextItem> OpenRisks,
    IReadOnlyList<ContextItem> Constraints,
    IReadOnlyList<string> RecentImplementationLogs,
    IReadOnlyList<ContextItem> RelevantArchitecture,
    IReadOnlyList<string> KnownUnknowns,
    IReadOnlyList<ContextRead> RecommendedNextReads,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ContextItem> RecentNotes,
    string Confidence = "exact",
    string? ResolvedVia = null);

/// <summary>
/// Compact per-project bundle so an agent can load project state without dumping the vault.
/// Detail levels: brief (goal + tasks + decisions + warnings), standard (everything, default
/// limits), deep (everything, doubled limits and a longer goal excerpt).
/// </summary>
public sealed class ProjectContextService(VaultContext ctx)
{
    public const int StaleTaskDays = 60;

    public ProjectContextResult Get(string project, int limit = 10, string detailLevel = "standard")
    {
        if (string.IsNullOrWhiteSpace(project))
            throw new MindVaultException("Project name must not be empty.");
        detailLevel = (detailLevel ?? "standard").Trim().ToLowerInvariant();
        if (detailLevel is not ("brief" or "standard" or "deep"))
            throw new MindVaultException($"Unknown detail level '{detailLevel}'. Use brief, standard or deep.");
        ctx.Scanner.EnsureFresh();

        // Alias/repo-name tolerant resolution: unique exact/high match resolves, ambiguity
        // throws with candidates, no match throws with known projects and near misses.
        var detection = ctx.ProjectDetect.Detect(project.Trim());
        var (proj, matchedVia) = detection.Project is not null
            ? (detection.Project, detection.MatchedVia!)
            : ctx.ProjectDetect.ResolveOrThrow(project.Trim()); // throws the right error

        var brief = detailLevel == "brief";
        limit = Math.Clamp(brief ? Math.Min(limit, 3) : detailLevel == "deep" ? limit * 2 : limit, 1, 50);
        // Notes may reference the project by title, stem or a declared alias.
        var names = ctx.ProjectDetect.QueryNamesFor(proj);

        // One read of the project note body feeds goal / non-negotiables / open questions.
        var body = ReadBody(proj);
        var goal = SectionExtractor.GetSectionText(body, "Goal", detailLevel == "deep" ? 800 : 300);
        var nonNegotiables = brief ? [] : SectionExtractor.GetBullets(body, "Non-Negotiables", limit);
        var knownUnknowns = brief ? [] : SectionExtractor.GetBullets(body, "Open Questions", limit);

        var activeTasks = Items(ctx.Db.Query(type: "task", projectNames: names, statusIn: ["open", "active"], limit: limit));
        var blockedTasks = Items(ctx.Db.Query(type: "task", projectNames: names, statusIn: ["blocked"], limit: limit));
        var decisions = Items(ctx.Db.Query(type: "decision", projectNames: names,
            statusIn: ["accepted", "draft", "active", "open"], limit: limit));
        var risks = Items(ctx.Db.Query(type: "risk", projectNames: names, statusIn: ["open", "active", "blocked"], limit: limit));
        var constraints = brief ? [] : Items(ctx.Db.Query(type: "constraint", projectNames: names, statusNot: "archived", limit: limit));
        var architecture = brief ? [] : Items(ctx.Db.Query(type: "architecture", projectNames: names, statusNot: "archived", limit: limit));
        var recentNotes = brief ? [] : Items(ctx.Db.Query(projectNames: names, excludeId: proj.Id, limit: limit));
        var logs = brief ? [] : RecentLogs(proj, body);

        return new ProjectContextResult(
            proj.Title,
            Item(proj),
            goal,
            nonNegotiables,
            activeTasks,
            blockedTasks,
            decisions,
            risks,
            constraints,
            logs,
            architecture,
            knownUnknowns,
            NextReads(proj, activeTasks, blockedTasks, decisions, risks, architecture),
            BuildWarnings(proj, goal, activeTasks, blockedTasks, names, project.Trim(), matchedVia),
            recentNotes,
            detection.Confidence,
            matchedVia);
    }

    private string ReadBody(NoteSummary proj)
    {
        // Section extraction only needs the raw body text — a full NoteParser.Parse would run
        // Markdig, link/tag extraction and SHA-256 for nothing.
        var abs = ctx.Resolver.AbsolutePathOf(proj);
        var content = File.ReadAllText(abs)
            .TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n");
        FrontmatterCodec.TryExtract(content, out _, out var body);
        return body;
    }

    private List<string> RecentLogs(NoteSummary proj, string projectBody)
    {
        var entries = new List<string>();
        var logNote = ctx.Db.FindByStem($"Log - {proj.Stem}").FirstOrDefault();
        if (logNote is not null)
        {
            var logBody = ctx.Db.GetFtsBody(logNote.Id);
            if (logBody is not null)
                entries.AddRange(SectionExtractor.GetSubheadings(logBody, "Sessions", 3));
        }
        entries.AddRange(SectionExtractor.GetSubheadings(projectBody, "Active Work", 3));
        return entries.OrderByDescending(e => e, StringComparer.OrdinalIgnoreCase).Take(3).ToList();
    }

    private static List<ContextRead> NextReads(NoteSummary proj, IReadOnlyList<ContextItem> active,
        IReadOnlyList<ContextItem> blocked, IReadOnlyList<ContextItem> decisions,
        IReadOnlyList<ContextItem> risks, IReadOnlyList<ContextItem> architecture)
    {
        var reads = new List<ContextRead> { new(proj.Path, "project note — goals, constraints, active work") };
        if (blocked.Count > 0) reads.Add(new(blocked[0].Path, "most recent blocked task — unblock or route around it"));
        if (active.Count > 0) reads.Add(new(active[0].Path, "most recently updated active task"));
        if (decisions.Count > 0) reads.Add(new(decisions[0].Path, "most recent decision — do not contradict it"));
        if (risks.Count > 0) reads.Add(new(risks[0].Path, "top open risk"));
        if (architecture.Count > 0) reads.Add(new(architecture[0].Path, "architecture note for this project"));
        return reads
            .GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(5)
            .ToList();
    }

    private List<string> BuildWarnings(NoteSummary proj, string? goal,
        IReadOnlyList<ContextItem> activeTasks, IReadOnlyList<ContextItem> blockedTasks, string[] names,
        string requestedName, string matchedVia)
    {
        var warnings = new List<string>();

        if (matchedVia is not ("title" or "stem") &&
            !string.Equals(requestedName, proj.Title, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Resolved '{requestedName}' to project '{proj.Title}' via {matchedVia} — " +
                         "verify this is the right project before writing to it.");
        }

        if (goal is null)
            warnings.Add($"Project note has no Goal content — agents will lack direction ({proj.Path}).");

        var titleMatches = ctx.Db.FindByTitle(proj.Title)
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (titleMatches.Count > 1)
            warnings.Add($"Duplicate notes share the title '{proj.Title}': {string.Join(" | ", titleMatches.Select(m => m.Path))}.");

        foreach (var task in activeTasks.Concat(blockedTasks))
        {
            if (task.Updated is not null &&
                DateTime.TryParseExact(task.Updated, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var updated) &&
                (DateTime.Today - updated).TotalDays > StaleTaskDays)
            {
                warnings.Add($"Stale {task.Status} task (untouched {(int)(DateTime.Today - updated).TotalDays}d): {task.Path}.");
            }
        }

        // A decision that has been superseded but still carries an active status is a contradiction.
        var supersededBy = ctx.Db.GetFrontmatterValues("superseded_by");
        var projectDecisionPaths = ctx.Db.Query(type: "decision", projectNames: names, limit: 100)
            .Where(d => !string.Equals(d.Status, "superseded", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(d => d.Id, d => d.Path);
        foreach (var row in supersededBy.Where(r => projectDecisionPaths.ContainsKey(r.NoteId)))
            warnings.Add($"Decision is marked superseded_by but its status is not 'superseded': {row.NotePath}.");

        // Broken wiki links in the project note itself. Only that note's links are loaded;
        // the name set is (title, stem) pairs, not full note rows.
        var projLinks = ctx.Db.GetLinksFor(proj.Id);
        if (projLinks.Count > 0)
        {
            var known = new HashSet<string>();
            foreach (var (title, stem) in ctx.Db.GetAllTitleStems())
            {
                known.Add(SlugHelper.NormalizeWiki(title));
                known.Add(SlugHelper.NormalizeWiki(stem));
            }
            foreach (var link in projLinks.Where(l => !known.Contains(l.TargetNorm)))
                warnings.Add($"Project note links to a missing note: [[{link.Target}]].");
        }

        return warnings;
    }

    private static ContextItem Item(NoteSummary n) => new(n.Title, n.Path, n.Status, n.Updated);

    private static List<ContextItem> Items(IEnumerable<NoteSummary> notes) =>
        notes.Select(Item).ToList();
}
