using System.Globalization;

namespace MindVault.Core;

public sealed record LowValueNote(
    string Title, string Path, string? Type, string? Status, IReadOnlyList<string> Reasons);

public sealed record LowValueReport(
    string? Project, int Scanned, IReadOnlyList<LowValueNote> Notes, bool Truncated);

/// <summary>
/// The do-not-read list: notes an agent should skip by default, each with explicit
/// reasons. Nothing here is ever deleted or moved — this is guidance that route cards,
/// read plans, the organisation score and search cautions consume so tokens are not
/// spent on archived, superseded, hidden, stale or unsummarized-oversized memory.
/// </summary>
public sealed class LowValueService(VaultContext ctx)
{
    public const int MaxResults = 200;
    public const int StaleLogDays = 90;

    public LowValueReport Find(string? project = null)
    {
        ctx.Scanner.EnsureFresh();
        string[]? names = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project!);
            names = ctx.ProjectDetect.QueryNamesFor(proj);
        }

        var fb = ctx.Feedback.LoadAll();
        var states = ctx.Db.GetFileStates();
        var archive = ctx.Config.DefaultArchiveFolder;
        var orphanPaths = ctx.LinkIntel.Orphans(names).Rows
            .Select(r => r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // How many distinct projects claim each normalized name (ambiguity detection).
        var projectClaims = new Dictionary<string, int>(StringComparer.Ordinal);
        var projects = ctx.Db.GetAllNotes()
            .Where(n => string.Equals(n.Type, "project", StringComparison.OrdinalIgnoreCase) &&
                        !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var aliasRows = ctx.ProjectDetect.LoadAliases();
        foreach (var p in projects)
        {
            var claimed = new HashSet<string>(StringComparer.Ordinal)
            {
                SlugHelper.NormalizeWiki(p.Title),
                SlugHelper.NormalizeWiki(p.Stem),
            };
            if (aliasRows.TryGetValue(p.Id, out var extra))
                foreach (var name in extra.Aliases.Concat(extra.RepoNames))
                    claimed.Add(SlugHelper.NormalizeWiki(name));
            foreach (var norm in claimed)
                projectClaims[norm] = projectClaims.GetValueOrDefault(norm) + 1;
        }

        var today = DateTime.Today;
        var scanned = 0;
        var rows = new List<LowValueNote>();
        foreach (var n in ctx.Db.GetAllNotes().OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(n.Type, "map", StringComparison.OrdinalIgnoreCase)) continue;
            if (names is not null &&
                !(n.Project is { Length: > 0 } np && names.Contains(np, StringComparer.OrdinalIgnoreCase)) &&
                !names.Contains(n.Title, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }
            scanned++;

            var reasons = new List<string>();
            var isArchived = n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase);
            if (isArchived) reasons.Add("archived");
            if (string.Equals(n.Status, "superseded", StringComparison.OrdinalIgnoreCase))
                reasons.Add("superseded");
            if (string.Equals(n.Type, "decision", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(n.Status, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("rejected decision");
            }

            var f = FeedbackService.For(n, fb);
            if (f.Hidden) reasons.Add("hidden by feedback");
            else if (f.Score < 0) reasons.Add($"negative feedback (score {f.Score})");

            if (string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase))
                reasons.Add("raw thought — unpromoted, low confidence");

            if (orphanPaths.Contains(n.Path)) reasons.Add("unlinked orphan");

            if (NoteTypes.IsManaged(n.Type) &&
                n.Type?.ToLowerInvariant() is "decision" or "task" or "bug" or "feature"
                    or "risk" or "constraint" or "review" or "mistake" or "architecture")
            {
                if (string.IsNullOrWhiteSpace(n.Project))
                {
                    reasons.Add("no project assigned");
                }
                else
                {
                    var claims = projectClaims.GetValueOrDefault(SlugHelper.NormalizeWiki(n.Project));
                    if (claims == 0) reasons.Add($"project '{n.Project}' does not resolve");
                    else if (claims > 1) reasons.Add($"ambiguous project reference '{n.Project}'");
                }
            }

            if (n.Title.StartsWith("Log - ", StringComparison.OrdinalIgnoreCase) &&
                TryDate(n.Updated ?? n.Created) is { } d && (today - d).TotalDays > StaleLogDays)
            {
                reasons.Add($"stale implementation log (last updated {d:yyyy-MM-dd})");
            }

            if (!isArchived &&
                states.TryGetValue(n.Path, out var state) && state.Size >= SummaryService.LargeBodyChars)
            {
                string raw;
                try { raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, n.Path)); }
                catch (IOException) { raw = ""; }
                if (raw.Length > 0 && !SummaryService.HasSummaryBlock(raw))
                {
                    reasons.Add($"large (~{TokenEstimator.EstimateBytes(state.Size)} tokens) with no summary" +
                                " — run summarize before reading it raw");
                }
            }

            if (reasons.Count > 0)
                rows.Add(new LowValueNote(n.Title, n.Path, n.Type, n.Status, reasons));
        }

        return new LowValueReport(names?[0] ?? project, scanned,
            rows.Take(MaxResults).ToList(), rows.Count > MaxResults);
    }

    private static DateTime? TryDate(string? s) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d) ? d : null;
}
