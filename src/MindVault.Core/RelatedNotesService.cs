namespace MindVault.Core;

public sealed record RelatedNote(string Title, string Path, string? Type, string? Status, string Reason);

public sealed record RelatedNotesResult(
    string Title, string Path, string? Type, string? Project,
    IReadOnlyList<RelatedNote> Related);

/// <summary>
/// Practical graph intelligence without a graph database: for one note, the outgoing wiki
/// links, the backlinks, active same-project memory, and same-type notes with overlapping
/// titles — each tagged with the reason it appears. Deterministic ordering, deduplicated
/// (first reason wins), bounded output.
/// </summary>
public sealed class RelatedNotesService(VaultContext ctx)
{
    public const int DefaultLimit = 12;

    public RelatedNotesResult Get(string noteRef, int limit = DefaultLimit)
    {
        limit = Math.Clamp(limit, 1, 50);
        var note = ctx.Resolver.Resolve(noteRef);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { note.Path };
        var related = new List<RelatedNote>();
        var perGroup = Math.Max(limit / 3, 3);
        var fb = ctx.Feedback.LoadAll();
        bool Visible(NoteSummary n) => !FeedbackService.For(n, fb).Hidden;

        // 1. Outgoing wiki links (frontmatter + body), resolved to indexed notes.
        foreach (var link in ctx.Db.GetLinksFor(note.Id))
        {
            var target = ctx.Db.FindByTitle(link.Target).Concat(ctx.Db.FindByStem(link.Target))
                .FirstOrDefault(n => !IsTemplate(n) && Visible(n));
            if (target is not null && seen.Add(target.Path))
                related.Add(Item(target, $"linked from this note ([[{link.Target}]])"));
        }

        // 2. Backlinks: notes that point here.
        foreach (var path in ctx.Db.GetBacklinkPaths(
                     SlugHelper.NormalizeWiki(note.Title), SlugHelper.NormalizeWiki(note.Stem), note.Id))
        {
            if (related.Count(r => r.Reason.StartsWith("links to")) >= perGroup) break;
            var source = ctx.Db.FindByPath(path);
            if (source is not null && !IsTemplate(source) && Visible(source) && seen.Add(source.Path))
                related.Add(Item(source, "links to this note"));
        }

        // 3. Active memory in the same project (most recently updated first).
        if (note.Project is { Length: > 0 })
        {
            var names = ctx.Db.FindProjects(note.Project) is [var proj, ..]
                ? ctx.ProjectDetect.QueryNamesFor(proj)
                : [note.Project];
            foreach (var sibling in ctx.Db.Query(projectNames: names, statusNot: "archived",
                         excludeId: note.Id, limit: perGroup * 2))
            {
                if (related.Count(r => r.Reason == "same project, active") >= perGroup) break;
                if (!IsTemplate(sibling) && Visible(sibling) && seen.Add(sibling.Path))
                    related.Add(Item(sibling, "same project, active"));
            }
        }

        // 4. Same-type notes with overlapping titles (possible duplicates or follow-ups).
        var titleTokens = Tokens(note.Title);
        if (note.Type is not null && titleTokens.Count > 0)
        {
            var similar = ctx.Db.Query(type: note.Type, excludeId: note.Id, limit: 200)
                .Where(c => !IsTemplate(c) && Visible(c))
                .Select(c => (Note: c, Overlap: Jaccard(titleTokens, Tokens(c.Title))))
                .Where(x => x.Overlap >= 0.3)
                .OrderByDescending(x => x.Overlap)
                .ThenBy(x => x.Note.Path, StringComparer.OrdinalIgnoreCase)
                .Take(perGroup);
            foreach (var (candidate, _) in similar)
            {
                if (seen.Add(candidate.Path))
                    related.Add(Item(candidate, $"same type ({note.Type}), similar title"));
            }
        }

        return new RelatedNotesResult(note.Title, note.Path, note.Type, note.Project,
            related.Take(limit).ToList());
    }

    private static RelatedNote Item(NoteSummary n, string reason) =>
        new(n.Title, n.Path, n.Type, n.Status, reason);

    private static bool IsTemplate(NoteSummary note) =>
        note.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> Tokens(string text) =>
        SearchService.Tokenize(text).ToHashSet(StringComparer.Ordinal);

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Intersect(b).Count();
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
}
