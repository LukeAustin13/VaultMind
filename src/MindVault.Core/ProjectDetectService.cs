namespace MindVault.Core;

public sealed record ProjectCandidate(string Title, string Path, string? Status, string MatchedVia);

/// <summary>
/// Outcome of project detection. <see cref="Project"/> is set only for a unique match at a
/// confidence tier that is safe to act on ("exact" or "high"); "low" tiers and ambiguous
/// names populate <see cref="Candidates"/> instead of guessing.
/// </summary>
public sealed record ProjectDetection(
    NoteSummary? Project,
    string Confidence,
    string? MatchedVia,
    IReadOnlyList<ProjectCandidate> Candidates)
{
    public bool Ambiguous => Confidence == "ambiguous";
}

/// <summary>
/// Maps a free-form name — a repo folder, a user's shorthand, an alias — to a vault project
/// note. Deterministic tiers, evaluated in order; the first tier with matches wins:
///   exact:  title/stem equality (case/whitespace-insensitive)
///   high:   `aliases:` entry, `repoNames:` entry, condensed comparison
///           ("mind-vault" / "Mind_Vault" / "MindVault" all condense to "mindvault")
///   low:    title token overlap — suggestions only, never auto-resolved
/// Aliases and repo names live in project-note frontmatter (see docs/VAULT_SCHEMA.md);
/// no index schema change is involved.
/// </summary>
public sealed class ProjectDetectService(VaultContext ctx)
{
    public ProjectDetection Detect(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new MindVaultException("Project name must not be empty.");
        ctx.Scanner.EnsureFresh();

        var input = name.Trim();
        var inputNorm = SlugHelper.NormalizeWiki(input);
        var inputCondensed = Condense(input);

        var projects = ctx.Db.Query(type: "project", limit: 1000)
            .Where(p => !p.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var aliases = LoadAliases();

        // Tier 1 — exact title/stem (whitespace/case-insensitive).
        var exact = projects.Where(p =>
            SlugHelper.NormalizeWiki(p.Title) == inputNorm ||
            SlugHelper.NormalizeWiki(p.Stem) == inputNorm).ToList();
        if (exact.Count > 0) return Resolve(exact, "exact", "title");

        // Tier 2 — declared aliases.
        var byAlias = projects.Where(p =>
            aliases.TryGetValue(p.Id, out var a) &&
            a.Aliases.Any(x => SlugHelper.NormalizeWiki(x) == inputNorm)).ToList();
        if (byAlias.Count > 0) return Resolve(byAlias, "high", "alias");

        // Tier 3 — declared repo names.
        var byRepo = projects.Where(p =>
            aliases.TryGetValue(p.Id, out var a) &&
            a.RepoNames.Any(x => SlugHelper.NormalizeWiki(x) == inputNorm)).ToList();
        if (byRepo.Count > 0) return Resolve(byRepo, "high", "repo-name");

        // Tier 4 — condensed comparison across title/stem/aliases/repo names, so repo
        // separators ("mind-vault") match project casing ("MindVault") without config.
        if (inputCondensed.Length > 0)
        {
            var condensed = projects.Where(p =>
                Condense(p.Title) == inputCondensed ||
                Condense(p.Stem) == inputCondensed ||
                (aliases.TryGetValue(p.Id, out var a) &&
                 a.Aliases.Concat(a.RepoNames).Any(x => Condense(x) == inputCondensed))).ToList();
            if (condensed.Count > 0) return Resolve(condensed, "high", "condensed-name");
        }

        // Tier 5 — token overlap. Suggestion-quality only: candidates, never a match.
        var inputTokens = SearchService.Tokenize(input).ToHashSet(StringComparer.Ordinal);
        var fuzzy = projects
            .Select(p => (Note: p, Overlap: Jaccard(inputTokens,
                SearchService.Tokenize(p.Title).ToHashSet(StringComparer.Ordinal))))
            .Where(x => x.Overlap >= 0.34)
            .OrderByDescending(x => x.Overlap)
            .ThenBy(x => x.Note.Path, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        return new ProjectDetection(null, fuzzy.Count > 0 ? "low" : "none", null,
            fuzzy.Select(x => new ProjectCandidate(x.Note.Title, x.Note.Path, x.Note.Status, "token-overlap")).ToList());
    }

    /// <summary>
    /// Detection that must end in exactly one project: unique exact/high match returns it,
    /// anything ambiguous throws with candidates, no match throws with known projects and
    /// near-miss suggestions. Shared by creates and context so alias resolution can never
    /// silently pick different projects in different code paths.
    /// </summary>
    public (NoteSummary Project, string MatchedVia) ResolveOrThrow(string name)
    {
        var detection = Detect(name);
        if (detection.Project is not null)
            return (detection.Project, detection.MatchedVia!);
        if (detection.Ambiguous)
            throw new AmbiguousNoteRefException(name, detection.Candidates.Select(c => c.Path).ToList());

        var known = ctx.Db.Query(type: "project", limit: 500)
            .Where(p => !p.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Title).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).Take(10).ToList();
        var suggestions = detection.Candidates.Count > 0
            ? $" Closest: {string.Join(" | ", detection.Candidates.Select(c => c.Title))}."
            : "";
        throw new MindVaultException(known.Count == 0
            ? $"Project not found: '{name}'. The vault has no project notes yet."
            : $"Project not found: '{name}'. Known projects: {string.Join(", ", known)}.{suggestions}");
    }

    /// <summary>Names that scope a `project:` frontmatter query: title, stem and declared aliases.</summary>
    public string[] QueryNamesFor(NoteSummary project)
    {
        var names = new List<string> { project.Title };
        if (!string.Equals(project.Title, project.Stem, StringComparison.OrdinalIgnoreCase))
            names.Add(project.Stem);
        if (LoadAliases().TryGetValue(project.Id, out var a))
            names.AddRange(a.Aliases);
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static ProjectDetection Resolve(List<NoteSummary> matches, string confidence, string via)
    {
        var candidates = matches
            .Select(m => new ProjectCandidate(m.Title, m.Path, m.Status, via))
            .ToList();
        return matches.Count == 1
            ? new ProjectDetection(matches[0], confidence, via, candidates)
            : new ProjectDetection(null, "ambiguous", via, candidates);
    }

    private Dictionary<long, (List<string> Aliases, List<string> RepoNames)> LoadAliases()
    {
        var map = new Dictionary<long, (List<string>, List<string>)>();
        foreach (var (noteId, key, value) in ctx.Db.GetProjectAliasRows())
        {
            if (!map.TryGetValue(noteId, out var entry))
                map[noteId] = entry = ([], []);
            (key == "aliases" ? entry.Item1 : entry.Item2).AddRange(Json.ReadStringList(value));
        }
        return map;
    }

    private static string Condense(string value) =>
        new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Intersect(b).Count();
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
}
