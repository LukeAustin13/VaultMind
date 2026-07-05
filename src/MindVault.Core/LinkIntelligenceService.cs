namespace MindVault.Core;

public sealed record LinkSuggestion(
    string From, string FromPath, string To, string ToPath, string Reason, string Confidence);

public sealed record BrokenLinkRow(string FromPath, string Target);

public sealed record OrphanNoteRow(string Title, string Path, string? Type, string? Status, string? Project);

/// <summary>
/// Deterministic link intelligence: reason-tagged link suggestions (never auto-applied —
/// applying is `link` / mindvault_link_notes), broken wiki-link detection, and orphan
/// detection for managed notes. Suggestions score concrete signals — type relationships
/// (decision↔task, risk↔task, …) within a project, shared specific tags, shared title
/// tokens, body mentions — and anything with only one weak signal is dropped, so generic
/// word overlap alone never produces a suggestion.
/// </summary>
public sealed class LinkIntelligenceService(VaultContext ctx)
{
    public const int MaxResults = 100;

    private static readonly Dictionary<string, string[]> RelatedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["decision"] = ["task", "risk"],
        ["task"] = ["decision", "risk", "mistake", "review"],
        ["risk"] = ["task", "decision", "review"],
        ["mistake"] = ["task"],
        ["review"] = ["task", "risk"],
        ["architecture"] = ["decision"],
        ["bug"] = ["task", "decision"],
    };

    /// <summary>Tags that mirror the type system carry no linking signal.</summary>
    private static readonly HashSet<string> GenericTags =
        new(NoteTypes.Managed.Concat(["log", "map"]), StringComparer.OrdinalIgnoreCase);

    private sealed class PoolEntry(NoteSummary note)
    {
        public NoteSummary Note { get; } = note;
        public List<string> Reasons { get; } = [];
        public int Score { get; private set; }

        public void Add(string reason, int score)
        {
            if (Reasons.Contains(reason)) return;
            Reasons.Add(reason);
            Score += score;
        }
    }

    public List<LinkSuggestion> SuggestForNote(string noteRef, int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 25);
        ctx.Scanner.EnsureFresh();
        var note = ctx.Resolver.Resolve(noteRef);
        var pool = new Dictionary<string, PoolEntry>(StringComparer.OrdinalIgnoreCase);
        var fb = ctx.Feedback.LoadAll();

        // Existing connections (either direction) are never re-suggested.
        var connectedNorms = ctx.Db.GetLinksFor(note.Id)
            .Select(l => l.TargetNorm).ToHashSet(StringComparer.Ordinal);
        var backlinkPaths = ctx.Db.GetBacklinkPaths(
                SlugHelper.NormalizeWiki(note.Title), SlugHelper.NormalizeWiki(note.Stem), note.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        bool Excluded(NoteSummary c) =>
            c.Id == note.Id
            || c.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase)
            || c.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase) // legacy shield: un-migrated map files

            || c.Path.StartsWith(ctx.Config.DefaultArchiveFolder + "/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Status, "archived", StringComparison.OrdinalIgnoreCase)
            || string.Equals(c.Type, "thought", StringComparison.OrdinalIgnoreCase) // raw thoughts are not link targets
            || FeedbackService.For(c, fb).Hidden
            || connectedNorms.Contains(SlugHelper.NormalizeWiki(c.Title))
            || connectedNorms.Contains(SlugHelper.NormalizeWiki(c.Stem))
            || backlinkPaths.Contains(c.Path);

        void AddSignal(NoteSummary c, string reason, int score)
        {
            if (Excluded(c)) return;
            if (!pool.TryGetValue(c.Path, out var entry)) pool[c.Path] = entry = new PoolEntry(c);
            entry.Add(reason, score);
        }

        // 1+2. Same project: type relationships (strongest deterministic signal) + siblings.
        if (note.Project is { Length: > 0 } projName)
        {
            var detection = ctx.ProjectDetect.Detect(projName);
            var names = detection.Project is not null
                ? ctx.ProjectDetect.QueryNamesFor(detection.Project)
                : [projName];

            if (note.Type is not null && RelatedTypes.TryGetValue(note.Type, out var relTypes))
            {
                foreach (var rt in relTypes)
                    foreach (var c in ctx.Db.Query(type: rt, projectNames: names, statusNot: "archived",
                                 excludeId: note.Id, limit: 30))
                        AddSignal(c, $"{note.Type}-to-{rt} relationship", 2);
            }

            foreach (var c in ctx.Db.Query(projectNames: names, statusNot: "archived",
                         excludeId: note.Id, limit: 40))
                AddSignal(c, "same project", 1);

            if (detection.Project is not null)
                AddSignal(detection.Project, "project hub", 2);
        }

        // 3. Shared specific tags.
        foreach (var tag in ctx.Db.GetTagsFor(note.Id).Where(t => !GenericTags.Contains(t)).Take(6))
            foreach (var c in ctx.Db.Query(tag: tag, statusNot: "archived", excludeId: note.Id, limit: 30))
                AddSignal(c, $"shared tag '{tag}'", 1);

        // 4. Same-type notes whose titles genuinely overlap (2+ shared tokens).
        var myTokens = SearchService.Tokenize(note.Title).ToHashSet(StringComparer.Ordinal);
        if (note.Type is not null && myTokens.Count > 0)
        {
            foreach (var c in ctx.Db.Query(type: note.Type, statusNot: "archived",
                         excludeId: note.Id, limit: 100))
            {
                var shared = myTokens.Intersect(SearchService.Tokenize(c.Title))
                    .OrderBy(t => t, StringComparer.Ordinal).ToList();
                if (shared.Count >= 2)
                    AddSignal(c, $"same type, shared title tokens: {string.Join(", ", shared)}", 2);
            }
        }

        // Cross-signal boosts over the assembled pool: title-token overlap and body mentions.
        foreach (var entry in pool.Values.ToList())
        {
            var shared = myTokens.Intersect(SearchService.Tokenize(entry.Note.Title))
                .OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (shared.Count >= 2)
                entry.Add($"shared title tokens: {string.Join(", ", shared)}", 1);
        }
        string? bodyLower = null;
        try { bodyLower = File.ReadAllText(ctx.Resolver.AbsolutePathOf(note)).ToLowerInvariant(); }
        catch (IOException) { /* body signal is optional */ }
        if (bodyLower is not null)
        {
            foreach (var entry in pool.Values.ToList())
            {
                if (entry.Note.Title.Length >= 8 &&
                    bodyLower.Contains(entry.Note.Title.ToLowerInvariant()))
                {
                    entry.Add("mentioned in this note's body", 2);
                }
            }
        }

        return pool.Values
            .Where(e => e.Score >= 2) // one weak signal alone is noise, not a suggestion
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Note.Path, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(e => new LinkSuggestion(note.Title, note.Path, e.Note.Title, e.Note.Path,
                string.Join("; ", e.Reasons), e.Score >= 4 ? "high" : "medium"))
            .ToList();
    }

    /// <summary>Suggestions across a project: the hub plus its most recent notes as seeds.</summary>
    public List<LinkSuggestion> SuggestForProject(string project, int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
        var names = ctx.ProjectDetect.QueryNamesFor(proj);
        var seeds = new List<NoteSummary> { proj };
        seeds.AddRange(ctx.Db.Query(projectNames: names, statusNot: "archived", limit: 10));

        var seenPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<LinkSuggestion>();
        foreach (var seed in seeds.DistinctBy(s => s.Path, StringComparer.OrdinalIgnoreCase))
        {
            if (seed.Type is "thought" or "map") continue;
            foreach (var s in SuggestForNote(seed.Path, 5))
            {
                var key = string.CompareOrdinal(s.FromPath, s.ToPath) <= 0
                    ? $"{s.FromPath}|{s.ToPath}"
                    : $"{s.ToPath}|{s.FromPath}";
                if (!seenPairs.Add(key)) continue;
                result.Add(s);
                if (result.Count >= limit) return result;
            }
        }
        return result;
    }

    /// <summary>Wiki links whose target matches no note title or stem. Template-authored links are skipped.</summary>
    public (List<BrokenLinkRow> Rows, bool Truncated) BrokenLinks()
    {
        ctx.Scanner.EnsureFresh();
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (title, stem) in ctx.Db.GetAllTitleStems())
        {
            known.Add(SlugHelper.NormalizeWiki(title));
            known.Add(SlugHelper.NormalizeWiki(stem));
        }
        var rows = ctx.Db.GetAllLinks()
            .Where(l => !known.Contains(l.TargetNorm))
            .Where(l => !l.NotePath.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Select(l => new BrokenLinkRow(l.NotePath, l.Target))
            .Distinct()
            .OrderBy(r => r.FromPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Target, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return (rows.Take(MaxResults).ToList(), rows.Count > MaxResults);
    }

    /// <summary>
    /// Managed, non-archived notes with no links in either direction. Thoughts are excluded —
    /// raw inbox captures are expected to be unlinked until promoted.
    /// </summary>
    public (List<OrphanNoteRow> Rows, bool Truncated) Orphans(string[]? projectNames = null)
    {
        ctx.Scanner.EnsureFresh();
        var links = ctx.Db.GetAllLinks()
            .Where(l => !l.NotePath.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var outgoing = links.Select(l => l.NoteId).ToHashSet();
        var targets = links.Select(l => l.TargetNorm).ToHashSet(StringComparer.Ordinal);
        var archive = ctx.Config.DefaultArchiveFolder;

        var rows = ctx.Db.GetAllNotes()
            .Where(n => NoteTypes.IsManaged(n.Type))
            .Where(n => !string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase))
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Where(n => !n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase))
            .Where(n => !string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase))
            .Where(n => projectNames is null ||
                        (n.Project is { Length: > 0 } p && projectNames.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .Where(n => !outgoing.Contains(n.Id))
            .Where(n => !targets.Contains(SlugHelper.NormalizeWiki(n.Title)) &&
                        !targets.Contains(SlugHelper.NormalizeWiki(n.Stem)))
            .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .Select(n => new OrphanNoteRow(n.Title, n.Path, n.Type, n.Status, n.Project))
            .ToList();
        return (rows.Take(MaxResults).ToList(), rows.Count > MaxResults);
    }
}
