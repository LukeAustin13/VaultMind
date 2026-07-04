namespace MindVault.Core;

public sealed record NoteTokenRow(string Title, string Path, string? Type, int EstimatedTokens);

public sealed record TokenAuditReport(
    string? Project, int NoteCount,
    int TotalEstimatedTokens, int ManagedEstimatedTokens, int ActiveEstimatedTokens,
    int ArchivedEstimatedTokens, int CapsuleEstimatedTokens, int RouteReadFirstEstimatedTokens,
    int LargeNoteCount, int LargeWithSummaryCount, int EstimatedTokenWaste,
    IReadOnlyList<NoteTokenRow> LargestNotes,
    IReadOnlyList<NoteTokenRow> NotesWithoutSummaries,
    IReadOnlyList<NoteTokenRow> NotesLikelyTooLarge,
    IReadOnlyList<string> TokenWasteWarnings,
    IReadOnlyList<string> RecommendedFixes);

/// <summary>
/// Where do the tokens go? Deterministic accounting over file sizes (ceil(bytes/4)):
/// totals by tier, the largest notes, large notes that lack generated summaries (agents
/// must read them raw), notes that should probably be split, and what a capsule costs
/// versus a route card's read-first list. Waste is an estimate and says so.
/// </summary>
public sealed class TokenAuditService(VaultContext ctx)
{
    public const int TooLargeTokens = 2000;
    private const int TopN = 10;

    public TokenAuditReport Run(string? project = null)
    {
        ctx.Scanner.EnsureFresh();
        string[]? names = null;
        NoteSummary? proj = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project!);
            names = ctx.ProjectDetect.QueryNamesFor(proj);
        }

        var states = ctx.Db.GetFileStates();
        var archive = ctx.Config.DefaultArchiveFolder;
        var notes = ctx.Db.GetAllNotes()
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Where(n => names is null ||
                        (n.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase)) ||
                        names.Contains(n.Title, StringComparer.OrdinalIgnoreCase))
            .ToList();

        int TokensOf(NoteSummary n) =>
            states.TryGetValue(n.Path, out var s) ? TokenEstimator.EstimateBytes(s.Size) : 0;
        bool IsArchived(NoteSummary n) =>
            n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase);
        bool IsActive(NoteSummary n) =>
            !IsArchived(n) &&
            !string.Equals(n.Status, "superseded", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(n.Status, "rejected", StringComparison.OrdinalIgnoreCase);

        var total = notes.Sum(TokensOf);
        var managed = notes.Where(n => NoteTypes.IsManaged(n.Type)).Sum(TokensOf);
        var active = notes.Where(IsActive).Sum(TokensOf);
        var archived = notes.Where(IsArchived).Sum(TokensOf);

        var largest = notes.OrderByDescending(TokensOf)
            .ThenBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .Take(TopN)
            .Select(n => new NoteTokenRow(n.Title, n.Path, n.Type, TokensOf(n)))
            .ToList();

        // Summary presence is checked only on notes already known to be large.
        var withoutSummaries = new List<NoteTokenRow>();
        var largeCount = 0;
        var largeWithSummary = 0;
        foreach (var n in notes.Where(n => IsActive(n) &&
                     !string.Equals(n.Type, "map", StringComparison.OrdinalIgnoreCase) &&
                     !n.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase) &&
                     states.TryGetValue(n.Path, out var s) && s.Size >= SummaryService.LargeBodyChars)
                     .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase))
        {
            largeCount++;
            string raw;
            try { raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, n.Path)); }
            catch (IOException) { continue; }
            if (SummaryService.HasSummaryBlock(raw)) largeWithSummary++;
            else withoutSummaries.Add(new NoteTokenRow(n.Title, n.Path, n.Type, TokensOf(n)));
        }

        var tooLarge = notes.Where(n => IsActive(n) && TokensOf(n) >= TooLargeTokens)
            .OrderByDescending(TokensOf)
            .Select(n => new NoteTokenRow(n.Title, n.Path, n.Type, TokensOf(n)))
            .ToList();

        var capsuleTokens = 0;
        var routeTokens = 0;
        if (proj is not null)
        {
            var capsule = ctx.Capsules.Build(proj.Title).Capsule;
            if (capsule is not null)
                capsuleTokens = TokenEstimator.Estimate(CapsuleService.ToMarkdown(capsule));
            var card = ctx.Routes.Build(proj.Title).Card;
            if (card is not null)
                routeTokens = card.ReadFirst.Sum(r => r.EstimatedTokens);
        }

        var unsummarizedTokens = withoutSummaries.Sum(r => r.EstimatedTokens);
        var oversizeExcess = tooLarge.Sum(r => r.EstimatedTokens - TooLargeTokens);
        var waste = unsummarizedTokens + oversizeExcess;

        var warnings = new List<string>();
        if (withoutSummaries.Count > 0)
        {
            warnings.Add($"{withoutSummaries.Count} large note(s) have no generated summary — agents " +
                         $"must read ~{unsummarizedTokens} tokens raw where summaries would serve " +
                         $"~{withoutSummaries.Count * 60} tokens.");
        }
        if (tooLarge.Count > 0)
        {
            warnings.Add($"{tooLarge.Count} note(s) exceed ~{TooLargeTokens} tokens each — " +
                         "splitting them would let reads stay scoped.");
        }
        if (total > 0 && archived * 100 / Math.Max(1, total) >= 30)
        {
            warnings.Add($"archived notes hold {archived * 100 / total}% of vault tokens — healthy " +
                         "(they are excluded from routes) but prune snapshots if disk matters.");
        }
        if (capsuleTokens > 0 && routeTokens > 0 && capsuleTokens > routeTokens)
        {
            warnings.Add($"a default capsule costs ~{capsuleTokens} tokens vs ~{routeTokens} for the " +
                         "route card's read-first list — prefer the route for narrow tasks.");
        }

        var fixes = new List<string>();
        if (withoutSummaries.Count > 0)
            fixes.Add($"run `mindvault summarize{(proj is not null ? $" --project \"{proj.Title}\"" : "")} --apply` " +
                      $"({withoutSummaries.Count} note(s))");
        if (tooLarge.Count > 0)
            fixes.Add($"split or archive the {tooLarge.Count} oversized note(s), starting with {tooLarge[0].Path}");
        if (fixes.Count == 0)
            fixes.Add("nothing urgent — token posture is healthy");

        return new TokenAuditReport(proj?.Title, notes.Count,
            total, managed, active, archived, capsuleTokens, routeTokens,
            largeCount, largeWithSummary, waste,
            largest, withoutSummaries.Take(TopN).ToList(), tooLarge.Take(TopN).ToList(),
            warnings, fixes);
    }
}
