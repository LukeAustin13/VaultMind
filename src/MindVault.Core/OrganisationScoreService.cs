using System.Globalization;

namespace MindVault.Core;

public sealed record ScoreCategory(string Name, int Score, string Evidence);

public sealed record OrganisationScoreReport(
    string? Project, int OverallScore,
    IReadOnlyList<ScoreCategory> Categories,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> RecommendedFixes,
    int EstimatedTokenWaste, int EstimatedTokenSavingsIfFixed);

/// <summary>
/// Eleven explainable 0–100 heuristics that tie vault structure to agent token cost.
/// Every category carries the evidence for its number; no false precision is claimed —
/// the point is that the weaknesses list is directly actionable, and fixing it makes
/// route cards and capsules measurably cheaper.
/// </summary>
public sealed class OrganisationScoreService(VaultContext ctx)
{
    public const int StaleDays = 90;
    public const int RecentSessionDays = 14;

    public OrganisationScoreReport Run(string? project = null)
    {
        ctx.Scanner.EnsureFresh();
        string[]? names = null;
        NoteSummary? proj = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project!);
            names = ctx.ProjectDetect.QueryNamesFor(proj);
        }

        var archive = ctx.Config.DefaultArchiveFolder;
        var all = ctx.Db.GetAllNotes()
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var scoped = all
            .Where(n => names is null ||
                        (n.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase)) ||
                        names.Contains(n.Title, StringComparer.OrdinalIgnoreCase))
            .ToList();

        bool IsArchived(NoteSummary n) =>
            n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase);
        var managed = scoped.Where(n => NoteTypes.IsManaged(n.Type) && !IsArchived(n)).ToList();

        var organize = ctx.Organizer.Plan(proj?.Title);
        var fm = ctx.Audits.AuditFrontmatter(proj?.Title);
        var orphans = ctx.LinkIntel.Orphans(names).Rows;
        var broken = ctx.LinkIntel.BrokenLinks().Rows;
        if (names is not null)
        {
            var scopedPaths = scoped.Select(n => n.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            broken = broken.Where(b => scopedPaths.Contains(b.FromPath)).ToList();
        }
        var ta = ctx.TokenAudit.Run(proj?.Title);

        var categories = new List<ScoreCategory>();
        void Cat(string name, int score, string evidence) =>
            categories.Add(new ScoreCategory(name, Math.Clamp(score, 0, 100), evidence));

        // folderPlacement
        var misplaced = organize.Proposals.Count;
        Cat("folderPlacement",
            100 - (managed.Count == 0 ? 0 : misplaced * 100 / managed.Count) - organize.NeedsReview.Count * 2,
            $"{misplaced} of {managed.Count} managed notes are misplaced; {organize.NeedsReview.Count} need review");

        // frontmatterQuality
        var criticals = fm.Findings.Count(f => f.Severity == "critical");
        var warns = fm.Findings.Count(f => f.Severity == "warning");
        Cat("frontmatterQuality", 100 - criticals * 15 - warns * 3,
            $"{criticals} critical and {warns} warning frontmatter finding(s) across {fm.NotesChecked} notes");

        // linkCoverage
        var linkable = managed.Count(n => !string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase));
        Cat("linkCoverage", 100 - (linkable == 0 ? 0 : orphans.Count * 100 / linkable),
            $"{orphans.Count} of {linkable} linkable managed notes have no links in either direction");

        // mapCoverage — the map now lives on the hub, so "mapped" means the hub carries a map block.
        var projects = scoped.Where(n => string.Equals(n.Type, "project", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (proj is not null && projects.Count == 0) projects = [proj];
        var mapped = projects.Count(HubHasMapBlock);
        Cat("mapCoverage", projects.Count == 0 ? 100 : mapped * 100 / projects.Count,
            $"{mapped} of {projects.Count} project hub(s) carry a map block");

        // summaryCoverage
        Cat("summaryCoverage",
            ta.LargeNoteCount == 0 ? 100 : ta.LargeWithSummaryCount * 100 / ta.LargeNoteCount,
            $"{ta.LargeWithSummaryCount} of {ta.LargeNoteCount} large note(s) carry a generated summary");

        // duplicateRisk
        var dupClusters = managed.GroupBy(n => SlugHelper.NormalizeWiki(n.Title), StringComparer.Ordinal)
            .Count(g => g.Count() > 1);
        Cat("duplicateRisk", 100 - dupClusters * 20,
            $"{dupClusters} exact-title duplicate cluster(s) among managed notes");

        // orphanRisk
        Cat("orphanRisk", 100 - orphans.Count * 8,
            $"{orphans.Count} orphan(s); {broken.Count} broken link(s) from scoped notes");

        // staleMemoryRisk — only work-tracking types go stale; old decisions are fine.
        var staleable = managed.Where(n => n.Type?.ToLowerInvariant() is "task" or "bug" or "feature" or "memory")
            .Where(n => n.Status?.ToLowerInvariant() is not ("done" or "cancelled" or "superseded"))
            .ToList();
        var stale = staleable.Count(n =>
            TryDate(n.Updated ?? n.Created) is { } d && (DateTime.Today - d).TotalDays > StaleDays);
        Cat("staleMemoryRisk", 100 - (staleable.Count == 0 ? 0 : stale * 100 / staleable.Count),
            $"{stale} of {staleable.Count} open work item(s)/log(s) untouched for {StaleDays}+ days");

        // thoughtPromotionHygiene
        var thoughts = scoped.Where(n => string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase) &&
                                         !IsArchived(n)).ToList();
        var strayThoughts = thoughts.Count(n =>
            !n.Path.StartsWith("00_Inbox/", StringComparison.OrdinalIgnoreCase) &&
            !n.Path.StartsWith("06_Agent_Memory/Inbox/", StringComparison.OrdinalIgnoreCase));
        var oldThoughts = thoughts.Count(n =>
            TryDate(n.Created) is { } d && (DateTime.Today - d).TotalDays > 14);
        Cat("thoughtPromotionHygiene", 100 - strayThoughts * 25 - oldThoughts * 5,
            $"{strayThoughts} thought(s) outside the inbox; {oldThoughts} older than 14 days awaiting promote/reject");

        // tokenEfficiency
        Cat("tokenEfficiency",
            100 - (ta.ActiveEstimatedTokens == 0 ? 0 : ta.EstimatedTokenWaste * 100 / Math.Max(1, ta.ActiveEstimatedTokens)),
            $"~{ta.EstimatedTokenWaste} of ~{ta.ActiveEstimatedTokens} active tokens are waste " +
            "(unsummarized large notes + oversized notes)");

        // agentReadiness
        var readiness = new List<(bool Ok, string Label)>
        {
            (projects.Count > 0 && mapped == projects.Count, "hub carries a map block"),
            (broken.Count == 0, "no broken links"),
            (scoped.Any(n => string.Equals(n.Type, "mistake", StringComparison.OrdinalIgnoreCase)),
                "mistake ledger in use"),
            (RecentSession(scoped), $"session log updated within {RecentSessionDays} days"),
        };
        if (proj is not null)
        {
            var pc = ctx.Projects.Get(proj.Title, 3, "brief");
            readiness.Add((pc.CurrentGoal is { Length: > 0 }, "hub has a Goal section"));
        }
        var failing = readiness.Where(r => !r.Ok).Select(r => r.Label).ToList();
        Cat("agentReadiness", readiness.Count(r => r.Ok) * 100 / readiness.Count,
            failing.Count == 0 ? "map, links, ledger, sessions and goal all in place"
                               : "missing: " + string.Join(", ", failing));

        var overall = (int)Math.Round(categories.Average(c => c.Score));
        var strengths = categories.Where(c => c.Score >= 90).Select(c => c.Name).ToList();
        var weaknesses = categories.Where(c => c.Score < 70)
            .OrderBy(c => c.Score)
            .Select(c => $"{c.Name} ({c.Score}): {c.Evidence}")
            .ToList();

        var fixes = new List<string>();
        if (mapped < projects.Count) fixes.Add("create/rebuild project maps (`mindvault map create|rebuild`)");
        if (ta.LargeNoteCount > ta.LargeWithSummaryCount)
            fixes.Add($"generate summaries for {ta.LargeNoteCount - ta.LargeWithSummaryCount} large note(s) (`mindvault summarize --apply`)");
        if (misplaced > 0) fixes.Add($"review {misplaced} placement proposal(s) (`mindvault organize`)");
        if (orphans.Count > 0) fixes.Add($"link {orphans.Count} orphan(s) (`mindvault links suggest`)");
        if (strayThoughts + oldThoughts > 0) fixes.Add("promote or reject waiting thoughts (`mindvault inbox list`)");
        if (dupClusters > 0) fixes.Add($"merge or rename {dupClusters} duplicate title cluster(s)");
        if (fixes.Count == 0) fixes.Add("nothing urgent — the brain is well organised");

        // Savings if fixed: summaries replace raw reads (~60 tokens each) and the route
        // becomes the entry point instead of a full capsule where one exists.
        var unsummarized = ta.NotesWithoutSummaries.Sum(r => r.EstimatedTokens);
        var savings = Math.Max(0, unsummarized - ta.NotesWithoutSummaries.Count * 60)
                      + Math.Max(0, ta.CapsuleEstimatedTokens - ta.RouteReadFirstEstimatedTokens);

        return new OrganisationScoreReport(proj?.Title, overall, categories, strengths,
            weaknesses, fixes, ta.EstimatedTokenWaste, savings);
    }

    private bool HubHasMapBlock(NoteSummary proj)
    {
        try
        {
            return File.ReadAllText(ctx.Resolver.AbsolutePathOf(proj))
                .Contains(MapService.MarkerStart, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private bool RecentSession(List<NoteSummary> scoped) =>
        scoped.Any(n => n.Path.StartsWith("06_Agent_Memory/Log - ", StringComparison.OrdinalIgnoreCase) &&
                        TryDate(n.Updated) is { } d &&
                        (DateTime.Today - d).TotalDays <= RecentSessionDays);

    private static DateTime? TryDate(string? s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;
}
