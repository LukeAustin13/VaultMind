namespace MindVault.Core;

public sealed record GraphEdge(
    string FromPath, string ToPath, string Type, string Reason, double Confidence, string Source);

public sealed record GraphBuildReport(
    int NoteCount, int EdgeCount, IReadOnlyDictionary<string, int> EdgesByType,
    string? SidecarPath, bool Written);

public sealed record GraphExplanation(
    bool Found, IReadOnlyList<GraphEdge> Path, string Explanation);

/// <summary>
/// Typed relationship graph derived deterministically from what already exists: explicit
/// wiki links typed by their endpoint note types (a task linking a decision IS
/// task_tracks_decision — no new Markdown syntax), frontmatter project membership, decision
/// supersession, and exact normalized-title collisions (duplicates). Edges carry reasons
/// and confidence. The graph never duplicates plain link data: it interprets it.
/// `relationships`/`explain` compute live so they are never stale; `build` also writes the
/// operational sidecar `.mindvault/link-graph.jsonl` (disposable, like the index).
/// Archived notes are excluded except for historical supersession edges.
/// </summary>
public sealed class LinkGraphService(VaultContext ctx)
{
    public const int MaxResults = 200;

    public string SidecarPath => Path.Combine(ctx.MindVaultDir, "link-graph.jsonl");

    public GraphBuildReport Build(string? project = null, bool write = true)
    {
        ctx.Scanner.EnsureFresh();
        var names = ResolveNames(project);
        var edges = BuildEdges(names);
        if (write)
        {
            Directory.CreateDirectory(ctx.MindVaultDir);
            File.WriteAllLines(SidecarPath, edges.Select(e => Json.Serialize(e)));
        }
        var byType = edges.GroupBy(e => e.Type, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return new GraphBuildReport(ctx.Db.GetAllNotes().Count, edges.Count, byType,
            write ? SidecarPath : null, write);
    }

    public List<GraphEdge> RelationshipsFor(string noteRef, int limit = 50)
    {
        limit = Math.Clamp(limit, 1, MaxResults);
        ctx.Scanner.EnsureFresh();
        var note = ctx.Resolver.Resolve(noteRef);
        return BuildEdges(null)
            .Where(e => e.FromPath.Equals(note.Path, StringComparison.OrdinalIgnoreCase) ||
                        e.ToPath.Equals(note.Path, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
    }

    public GraphExplanation Explain(string fromRef, string toRef)
    {
        ctx.Scanner.EnsureFresh();
        var a = ctx.Resolver.Resolve(fromRef);
        var b = ctx.Resolver.Resolve(toRef);
        var edges = BuildEdges(null);

        bool Touches(GraphEdge e, string p1, string p2) =>
            (e.FromPath.Equals(p1, StringComparison.OrdinalIgnoreCase) &&
             e.ToPath.Equals(p2, StringComparison.OrdinalIgnoreCase)) ||
            (e.FromPath.Equals(p2, StringComparison.OrdinalIgnoreCase) &&
             e.ToPath.Equals(p1, StringComparison.OrdinalIgnoreCase));

        var direct = edges.Where(e => Touches(e, a.Path, b.Path)).ToList();
        if (direct.Count > 0)
        {
            var best = direct.OrderByDescending(e => e.Confidence).First();
            return new GraphExplanation(true, [best],
                $"'{a.Title}' and '{b.Title}' are directly related: {best.Type} — {best.Reason} " +
                $"(confidence {best.Confidence:0.0#}).");
        }

        // Two hops: the strongest shared neighbour wins; ties break on path order.
        var ofA = edges.Where(e => TouchesNote(e, a.Path)).ToList();
        var ofB = edges.Where(e => TouchesNote(e, b.Path)).ToList();
        var best2 = (
            from ea in ofA
            let mid = OtherEnd(ea, a.Path)
            from eb in ofB
            where OtherEnd(eb, b.Path).Equals(mid, StringComparison.OrdinalIgnoreCase)
            orderby ea.Confidence + eb.Confidence descending, mid
            select (ea, eb, mid)).ToList();
        if (best2.Count > 0)
        {
            var (ea, eb, mid) = best2[0];
            var midStem = Path.GetFileNameWithoutExtension(mid);
            return new GraphExplanation(true, [ea, eb],
                $"'{a.Title}' relates to '{b.Title}' via '{midStem}': " +
                $"{ea.Type} ({ea.Reason}), then {eb.Type} ({eb.Reason}).");
        }
        return new GraphExplanation(false, [],
            $"No relationship between '{a.Title}' and '{b.Title}' within two hops. " +
            "If they belong together, add an explicit link (mindvault link).");
    }

    private static bool TouchesNote(GraphEdge e, string path) =>
        e.FromPath.Equals(path, StringComparison.OrdinalIgnoreCase) ||
        e.ToPath.Equals(path, StringComparison.OrdinalIgnoreCase);

    private static string OtherEnd(GraphEdge e, string path) =>
        e.FromPath.Equals(path, StringComparison.OrdinalIgnoreCase) ? e.ToPath : e.FromPath;

    private string[]? ResolveNames(string? project)
    {
        if (string.IsNullOrWhiteSpace(project)) return null;
        var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project!);
        return ctx.ProjectDetect.QueryNamesFor(proj);
    }

    private List<GraphEdge> BuildEdges(string[]? projectNames)
    {
        var notes = ctx.Db.GetAllNotes();
        var archive = ctx.Config.DefaultArchiveFolder;

        bool Ineligible(NoteSummary n) =>
            n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase) ||
            n.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Type, "map", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase);

        bool Archived(NoteSummary n) =>
            n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase);

        var byNorm = new Dictionary<string, NoteSummary>(StringComparer.Ordinal);
        foreach (var n in notes.Where(n => !Ineligible(n)))
        {
            byNorm.TryAdd(SlugHelper.NormalizeWiki(n.Title), n);
            byNorm.TryAdd(SlugHelper.NormalizeWiki(n.Stem), n);
        }
        var byPath = notes.ToDictionary(n => n.Path, n => n, StringComparer.OrdinalIgnoreCase);

        // Project lookup: titles, stems and declared aliases → project note.
        var projects = notes.Where(n => string.Equals(n.Type, "project", StringComparison.OrdinalIgnoreCase) &&
                                        !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var projByNorm = new Dictionary<string, NoteSummary>(StringComparer.Ordinal);
        foreach (var p in projects)
        {
            projByNorm.TryAdd(SlugHelper.NormalizeWiki(p.Title), p);
            projByNorm.TryAdd(SlugHelper.NormalizeWiki(p.Stem), p);
        }
        foreach (var (id, (aliases, repoNames)) in ctx.ProjectDetect.LoadAliases())
        {
            var proj = projects.FirstOrDefault(p => p.Id == id);
            if (proj is null) continue;
            foreach (var a in aliases.Concat(repoNames))
                projByNorm.TryAdd(SlugHelper.NormalizeWiki(a), proj);
        }

        var edges = new Dictionary<string, GraphEdge>(StringComparer.OrdinalIgnoreCase);
        void Add(GraphEdge e)
        {
            var key = $"{e.FromPath}|{e.ToPath}|{e.Type}";
            if (!edges.TryGetValue(key, out var existing) || e.Confidence > existing.Confidence)
                edges[key] = e;
        }

        // 1. Frontmatter project membership.
        foreach (var n in notes)
        {
            if (Ineligible(n) || Archived(n)) continue;
            if (string.Equals(n.Type, "project", StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Project is not { Length: > 0 } pName) continue;
            if (!projByNorm.TryGetValue(SlugHelper.NormalizeWiki(pName), out var proj)) continue;
            Add(new GraphEdge(n.Path, proj.Path, "belongs_to_project",
                $"frontmatter project: {pName}", 1.0, "frontmatter"));
        }

        // 2. Explicit wiki links, typed by their endpoints.
        foreach (var link in ctx.Db.GetAllLinks())
        {
            if (!byPath.TryGetValue(link.NotePath, out var from) || Ineligible(from)) continue;
            if (!byNorm.TryGetValue(link.TargetNorm, out var to) || to.Id == from.Id) continue;
            var classified = Classify(from, to);
            if (classified is null) continue;
            var (edgeFrom, edgeTo, type, reason, confidence) = classified.Value;
            if (type != "supersedes" && (Archived(edgeFrom) || Archived(edgeTo))) continue;
            Add(new GraphEdge(edgeFrom.Path, edgeTo.Path, type, reason, confidence, "explicit-link"));
        }

        // 3. Exact normalized-title collisions → duplicate suspicion.
        foreach (var group in notes
                     .Where(n => !Ineligible(n) && !Archived(n) && NoteTypes.IsManaged(n.Type))
                     .GroupBy(n => SlugHelper.NormalizeWiki(n.Title), StringComparer.Ordinal)
                     .Where(g => g.Count() > 1))
        {
            var members = group.OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase).ToList();
            for (var i = 1; i < members.Count; i++)
            {
                Add(new GraphEdge(members[0].Path, members[i].Path, "duplicates",
                    "identical normalized titles", 0.6, "title-collision"));
            }
        }

        var result = edges.Values
            .Where(e => projectNames is null || InProject(e, byPath, projectNames))
            .OrderBy(e => e.FromPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.ToPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Type, StringComparer.Ordinal)
            .ToList();
        return result;
    }

    private static bool InProject(GraphEdge e, Dictionary<string, NoteSummary> byPath, string[] names)
    {
        bool Match(string path) =>
            byPath.TryGetValue(path, out var n) &&
            ((n.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase)) ||
             names.Contains(n.Title, StringComparer.OrdinalIgnoreCase));
        return Match(e.FromPath) || Match(e.ToPath);
    }

    /// <summary>
    /// Types an explicit link by its endpoint note types. Returns the canonical direction
    /// (e.g. the edge is always mistake→task for mistake_prevented_by, whichever side held
    /// the wiki link). Null means the pair carries no relationship worth an edge.
    /// </summary>
    private static (NoteSummary From, NoteSummary To, string Type, string Reason, double Confidence)?
        Classify(NoteSummary a, NoteSummary b)
    {
        var ta = a.Type?.ToLowerInvariant() ?? "";
        var tb = b.Type?.ToLowerInvariant() ?? "";

        (NoteSummary, NoteSummary, string, string, double)? Directed(
            string fromType, string toType, string type, string reason, double conf)
        {
            if (ta == fromType && tb == toType) return (a, b, type, reason, conf);
            if (tb == fromType && ta == toType) return (b, a, type, reason, conf);
            return null;
        }

        // Supersession first: it is the one relationship that may touch archived notes.
        if (ta == "decision" && tb == "decision")
        {
            var aSuper = string.Equals(a.Status, "superseded", StringComparison.OrdinalIgnoreCase);
            var bSuper = string.Equals(b.Status, "superseded", StringComparison.OrdinalIgnoreCase);
            if (aSuper != bSuper)
            {
                var newer = aSuper ? b : a;
                var older = aSuper ? a : b;
                return (newer, older, "supersedes",
                    "an active decision links a superseded one", 0.9);
            }
            return (a, b, "related_to", "explicit link between decisions", 0.7);
        }

        if (ta == "project" || tb == "project")
        {
            var (note, proj) = ta == "project" ? (b, a) : (a, b);
            return (note, proj, "belongs_to_project", "explicit link to the project hub", 0.9);
        }

        var hit = Directed("task", "decision", "task_tracks_decision",
                      "the task tracks the decision it links", 0.9)
                  ?? Directed("mistake", "task", "mistake_prevented_by",
                      "the linked task is the prevention for this mistake", 0.9)
                  ?? Directed("risk", "task", "risk_mitigated_by",
                      "the linked task mitigates this risk", 0.9)
                  ?? Directed("risk", "decision", "risk_mitigated_by",
                      "the linked decision addresses this risk", 0.8)
                  ?? Directed("mistake", "bug", "caused_by",
                      "this mistake traces back to the linked bug", 0.8)
                  ?? Directed("mistake", "decision", "caused_by",
                      "the linked decision contributed to this mistake", 0.6)
                  ?? Directed("architecture", "decision", "implements",
                      "the architecture implements the linked decision", 0.8);
        if (hit is { } h) return h;

        if (ta == "review" || tb == "review")
        {
            var (review, subject) = ta == "review" ? (a, b) : (b, a);
            return (review, subject, "review_finding_for",
                "review findings apply to the linked note", 0.8);
        }

        if (ta == "task" && tb == "task")
        {
            var aBlocked = string.Equals(a.Status, "blocked", StringComparison.OrdinalIgnoreCase);
            var bBlocked = string.Equals(b.Status, "blocked", StringComparison.OrdinalIgnoreCase);
            if (aBlocked != bBlocked)
            {
                var blocked = aBlocked ? a : b;
                var blocker = aBlocked ? b : a;
                return (blocker, blocked, "blocks",
                    "a blocked task links this task as its blocker", 0.5);
            }
            return (a, b, "related_to", "explicit link between tasks", 0.7);
        }

        if (ta == tb && ta.Length > 0)
            return (a, b, "related_to", $"explicit link between {ta} notes", 0.7);
        return (a, b, "references", "explicit wiki link", 0.7);
    }
}
