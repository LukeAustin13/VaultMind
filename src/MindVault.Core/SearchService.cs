using System.Text.RegularExpressions;

namespace MindVault.Core;

public sealed partial class SearchService(VaultContext ctx)
{
    public const int DefaultLimit = 10;
    public const int MaxLimit = 100;
    public const int DefaultSnippetChars = 240;
    public const int MaxSnippetChars = 1000;

    /// <summary>
    /// Ranked search. FTS5 supplies candidates with title-weighted bm25 (title x4);
    /// a deterministic rescoring pass then applies: exact-title x2.0, title-contains-all-terms
    /// x1.5, updated&lt;=14d x1.25, updated&lt;=60d x1.1, archived x0.25 (archived results are
    /// excluded entirely unless <paramref name="includeArchived"/>). When a project filter
    /// yields nothing, the search falls back to the whole vault and marks results with
    /// scope "global-fallback". Ties break by path for stable output.
    /// </summary>
    public List<SearchResult> Search(string query, string? type = null, string? project = null,
        string? tag = null, string? status = null, int limit = DefaultLimit,
        string? updatedAfter = null, string? updatedBefore = null,
        bool includeArchived = false, bool explain = false, int snippetChars = DefaultSnippetChars)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new MindVaultException("Search query must not be empty.");
        ValidateDate(updatedAfter, "--updated-after");
        ValidateDate(updatedBefore, "--updated-before");
        ctx.Scanner.EnsureFresh();

        limit = Math.Clamp(limit, 1, MaxLimit);
        snippetChars = Math.Clamp(snippetChars, 0, MaxSnippetChars);
        var candidateLimit = Math.Min(Math.Max(limit * 4, 20), 100);
        var archiveFolder = ctx.Config.DefaultArchiveFolder;

        var (candidates, match) = ctx.Db.SearchCandidates(query.Trim(), Clean(type), Clean(project),
            Clean(tag), Clean(status), Clean(updatedAfter), Clean(updatedBefore),
            includeArchived, archiveFolder, candidateLimit);

        string? scope = null;
        if (candidates.Count == 0 && Clean(project) is not null)
        {
            (candidates, match) = ctx.Db.SearchCandidates(query.Trim(), Clean(type), null,
                Clean(tag), Clean(status), Clean(updatedAfter), Clean(updatedBefore),
                includeArchived, archiveFolder, candidateLimit);
            if (candidates.Count > 0) scope = "global-fallback";
        }

        // Query-level ranking inputs are computed once, not once per candidate.
        var terms = Tokenize(query);
        var queryNorm = SlugHelper.NormalizeWiki(query.Trim().Trim('"'));

        var scored = candidates
            .Select(c => Score(c, terms, queryNorm, archiveFolder, explain))
            .OrderByDescending(s => s.Relevance)
            .ThenBy(s => s.Candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        // Snippets are generated only for the page that survives ranking — snippet() re-reads
        // and tokenizes note bodies, so running it for the whole candidate pool wastes work.
        // snippetChars=0 skips them entirely for a refs-only, cheapest-possible result.
        var snippets = snippetChars == 0
            ? new Dictionary<long, string>()
            : ctx.Db.GetSnippets(match, scored.Select(s => s.Candidate.Id).ToList());

        // Feedback annotates but never re-ranks: FTS relevance stays reproducible, the agent
        // just learns it is about to read a note the user marked hidden/noisy before paying
        // for the read.
        var fb = ctx.Feedback.LoadAll();
        string? CautionFor(string path)
        {
            var stem = SlugHelper.NormalizeWiki(Path.GetFileNameWithoutExtension(path));
            if (!fb.TryGetValue(stem, out var state)) return null;
            if (state.Hidden) return "hidden by feedback — skip unless the user asks";
            if (state.Score < 0) return $"negative feedback (score {state.Score}) — likely low value";
            return null;
        }

        return scored.Select(s => new SearchResult(
                s.Candidate.Title, s.Candidate.Path, s.Candidate.Type, s.Candidate.Project,
                s.Candidate.Status,
                Truncate(snippets.GetValueOrDefault(s.Candidate.Id, ""), snippetChars),
                Math.Round(s.Relevance, 4),
                Section: FindMatchedSection(s.Candidate.Id, snippets.GetValueOrDefault(s.Candidate.Id, "")),
                Scope: scope,
                Why: explain ? s.Why : null,
                Caution: CautionFor(s.Candidate.Path)))
            .ToList();
    }

    private sealed record Scored(SearchCandidate Candidate, double Relevance, List<string> Why);

    private static Scored Score(SearchCandidate c, List<string> terms, string queryNorm,
        string archiveFolder, bool explain)
    {
        // bm25 is negative-better; flip to positive-better relevance.
        var relevance = Math.Max(-c.Bm25, 0.001);
        var why = new List<string>();
        if (explain) why.Add($"bm25(title x4)={c.Bm25:0.###}");

        var titleNorm = SlugHelper.NormalizeWiki(c.Title);

        if (titleNorm == queryNorm)
        {
            relevance *= 2.0;
            if (explain) why.Add("exact-title x2.0");
        }
        else if (terms.Count > 0 && terms.All(t => titleNorm.Contains(t, StringComparison.Ordinal)))
        {
            relevance *= 1.5;
            if (explain) why.Add("title-contains-all-terms x1.5");
        }

        if (c.Updated is not null &&
            DateTime.TryParseExact(c.Updated, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var updated))
        {
            var age = (DateTime.Today - updated).TotalDays;
            if (age <= 14) { relevance *= 1.25; if (explain) why.Add("updated<=14d x1.25"); }
            else if (age <= 60) { relevance *= 1.1; if (explain) why.Add("updated<=60d x1.1"); }
        }

        if (string.Equals(c.Status, "archived", StringComparison.OrdinalIgnoreCase) ||
            c.Path.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase))
        {
            relevance *= 0.25;
            if (explain) why.Add("archived x0.25");
        }
        else if (c.Status is not null &&
                 (c.Status.Equals("superseded", StringComparison.OrdinalIgnoreCase) ||
                  c.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase)))
        {
            // Replaced/rejected decisions stay findable but must not outrank what's in force.
            relevance *= 0.5;
            if (explain) why.Add("inactive-status x0.5");
        }

        return new Scored(c, relevance, why);
    }

    /// <summary>Heading of the section containing the first highlighted snippet term, if determinable.</summary>
    private string? FindMatchedSection(long noteId, string snippet)
    {
        var highlight = HighlightPattern().Match(snippet);
        if (!highlight.Success) return null;
        var body = ctx.Db.GetFtsBody(noteId);
        if (body is null) return null;
        var at = body.IndexOf(highlight.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;
        var line = body.AsSpan(0, at).Count('\n');
        var section = ctx.Db.GetHeadings(noteId).LastOrDefault(h => h.Line <= line);
        return section?.Text;
    }

    public List<NoteSummary> List(string? type = null, string? project = null,
        string? status = null, string? tag = null, int limit = 20)
    {
        ctx.Scanner.EnsureFresh();
        return ctx.Db.Query(
            type: Clean(type),
            projectNames: Clean(project) is { } p ? [p] : null,
            statusIn: Clean(status) is { } s ? [s] : null,
            tag: Clean(tag),
            limit: Math.Clamp(limit, 1, 500));
    }

    /// <summary>Caps a snippet at <paramref name="maxChars"/>, ellipsising if trimmed. 0 yields empty.</summary>
    private static string Truncate(string snippet, int maxChars)
    {
        if (maxChars <= 0 || snippet.Length == 0) return maxChars <= 0 ? "" : snippet;
        return snippet.Length <= maxChars ? snippet : snippet[..maxChars].TrimEnd() + " …";
    }

    internal static List<string> Tokenize(string text) =>
        TokenPattern().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 1)
            .Distinct()
            .ToList();

    private static void ValidateDate(string? value, string optionName)
    {
        if (value is not null && !string.IsNullOrWhiteSpace(value) &&
            !DateTime.TryParseExact(value.Trim(), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _))
        {
            throw new MindVaultException($"{optionName} must be a date in yyyy-MM-dd format, got '{value}'.");
        }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex HighlightPattern();

    [GeneratedRegex(@"[a-z0-9]+")]
    private static partial Regex TokenPattern();
}
