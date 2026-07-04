namespace MindVault.Core;

public sealed record DraftCheckResult(
    bool Ok,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Suggestions,
    IReadOnlyList<string> RelatedPaths,
    IReadOnlyList<string> LikelyDuplicatePaths)
{
    public DraftCheckResult(bool ok, IReadOnlyList<string> blockers, IReadOnlyList<string> warnings,
        IReadOnlyList<string> suggestions, IReadOnlyList<string> relatedPaths)
        : this(ok, blockers, warnings, suggestions, relatedPaths, []) { }
}

/// <summary>
/// Advisory quality gate for memory writes. Blockers are things the create would reject
/// anyway (missing project, exact duplicate); everything else is a warning or suggestion —
/// the caller decides. Deterministic only: token overlap, no fuzzy magic.
/// </summary>
public sealed class DraftCheckService(VaultContext ctx)
{
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "of", "for", "to", "in", "on", "at", "with", "and", "or", "is", "it", "this", "that",
    };

    private static readonly HashSet<string> GenericWords = new(StringComparer.Ordinal)
    {
        "fix", "improve", "update", "stuff", "things", "misc", "various", "cleanup", "general", "better", "some",
    };

    public DraftCheckResult CheckDraft(string type, string? project, string title)
    {
        var blockers = new List<string>();
        var warnings = new List<string>();
        var suggestions = new List<string>();
        var related = new List<string>();
        var likelyDuplicates = new List<string>();

        type = (type ?? "").Trim().ToLowerInvariant();
        if (!NoteTypes.IsManaged(type))
            blockers.Add($"'{type}' is not a managed note type. Use one of: {string.Join(", ", NoteTypes.Managed)}.");
        if (string.IsNullOrWhiteSpace(title))
            blockers.Add("Title must not be empty.");
        if (blockers.Count > 0)
            return new DraftCheckResult(false, blockers, warnings, suggestions, related);

        ctx.Scanner.EnsureFresh();
        title = title.Trim();

        NoteSummary? proj = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var detection = ctx.ProjectDetect.Detect(project.Trim());
            if (detection.Project is not null) proj = detection.Project;
            else if (detection.Ambiguous)
                blockers.Add($"Project '{project}' is ambiguous: " +
                             $"{string.Join(" | ", detection.Candidates.Select(c => c.Path))}.");
            else if (type is "decision" or "task")
                blockers.Add($"Project not found: '{project}'. Create it first with mindvault_create_project.");
            else
                warnings.Add($"Project '{project}' has no project note; the reference will be flagged by validate.");
        }
        else if (type is "decision" or "task")
        {
            blockers.Add($"A {type} must belong to a project. Pass the project name.");
        }

        // Exact collisions with what the create would produce.
        string clean;
        try
        {
            clean = SlugHelper.SanitizeFileName(title);
        }
        catch (MindVaultException ex)
        {
            blockers.Add(ex.Message);
            return new DraftCheckResult(false, blockers, warnings, suggestions, related);
        }
        var stem = type switch
        {
            "decision" => $"Decision - {clean}",
            "task" => $"Task - {clean}",
            _ => clean,
        };
        foreach (var existing in ctx.Db.FindByStem(stem)
                     .Concat(ctx.Db.FindByTitle(title))
                     .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
                     .DistinctBy(n => n.Path))
        {
            blockers.Add($"A note with this name already exists: {existing.Path}. Update it instead of duplicating.");
            related.Add(existing.Path);
            likelyDuplicates.Add(existing.Path);
        }

        // A project whose name collides with another project's alias/repo name would split
        // that project's memory in two.
        if (type == "project" && blockers.Count == 0)
        {
            var detection = ctx.ProjectDetect.Detect(title);
            List<ProjectCandidate> hits = detection.Project is not null
                ? [new ProjectCandidate(detection.Project.Title, detection.Project.Path,
                    detection.Project.Status, detection.MatchedVia!)]
                : detection.Ambiguous ? detection.Candidates.ToList() : [];
            foreach (var hit in hits)
            {
                warnings.Add($"'{title}' already resolves to project '{hit.Title}' via {hit.MatchedVia} " +
                             $"({hit.Path}) — creating it would split that project's memory.");
                related.Add(hit.Path);
                likelyDuplicates.Add(hit.Path);
            }
        }

        // Near-duplicates and possible conflicts among same-type notes (project-scoped when known).
        var titleTokens = Tokens(title);
        var candidates = ctx.Db.Query(type: type,
            projectNames: proj is null ? null : [proj.Title, proj.Stem], limit: 200);
        foreach (var candidate in candidates.Where(c =>
                     !c.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase)))
        {
            var overlap = Similarity(titleTokens, Tokens(candidate.Title));
            if (overlap >= 0.6)
            {
                warnings.Add($"Very similar existing {type}: '{candidate.Title}' ({candidate.Path}).");
                related.Add(candidate.Path);
                likelyDuplicates.Add(candidate.Path);
            }
            else if (type == "decision" && overlap >= 0.34)
            {
                suggestions.Add($"Possibly related decision: '{candidate.Title}' ({candidate.Path}) — " +
                                "if the new decision replaces it, use the supersede operation.");
                related.Add(candidate.Path);
            }
        }

        if (type is "task" or "decision")
        {
            var meaningful = titleTokens.Where(t => !GenericWords.Contains(t)).ToList();
            if (titleTokens.Count < 2 || meaningful.Count == 0)
                warnings.Add($"Title '{title}' is too vague to be actionable later. Name the concrete outcome.");
        }
        if (type == "decision")
            suggestions.Add("Fill Reversal Conditions — a decision without them is hard to revisit safely.");
        if (type == "task")
            suggestions.Add("Fill Acceptance Criteria so 'done' is checkable.");

        return new DraftCheckResult(blockers.Count == 0, blockers, warnings, suggestions,
            related.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList(),
            likelyDuplicates.Distinct(StringComparer.OrdinalIgnoreCase).Take(10).ToList());
    }

    /// <summary>Quality check of an existing note: schema, links, and type-specific gaps.</summary>
    public DraftCheckResult CheckNote(string noteRef)
    {
        var note = ctx.Resolver.Resolve(noteRef);
        var abs = ctx.Resolver.AbsolutePathOf(note);
        var parsed = NoteParser.Parse(File.ReadAllText(abs), note.Path);

        var blockers = new List<string>();
        var warnings = new List<string>();
        var suggestions = new List<string>();
        var related = new List<string>();

        if (parsed.ParseError is not null)
            blockers.Add($"Frontmatter problem: {parsed.ParseError}. Fix it before further edits.");

        if (NoteTypes.IsManaged(parsed.Type))
        {
            var keys = parsed.FrontmatterEntries.Select(e => e.Key.ToLowerInvariant()).ToHashSet();
            foreach (var key in NoteTypes.RequiredFrontmatterKeys.Where(k => !keys.Contains(k)))
                warnings.Add($"Missing required frontmatter key '{key}'.");
            if (parsed.Status is not null && !NoteTypes.IsValidStatus(parsed.Status))
                warnings.Add($"Invalid status '{parsed.Status}'. Allowed: {string.Join(", ", NoteTypes.Statuses)}.");

            if (parsed.Type == "decision" && IsSectionEmpty(parsed.Body, "Reversal Conditions"))
                warnings.Add("Reversal Conditions section is empty.");
            if (parsed.Type == "task" && IsSectionEmpty(parsed.Body, "Acceptance Criteria"))
                warnings.Add("Acceptance Criteria section is empty.");

            if (parsed.Updated is not null && parsed.Status is "open" or "active" &&
                DateTime.TryParseExact(parsed.Updated, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var updated) &&
                (DateTime.Today - updated).TotalDays > ProjectContextService.StaleTaskDays)
            {
                warnings.Add($"Note is '{parsed.Status}' but untouched for {(int)(DateTime.Today - updated).TotalDays} days — update or close it.");
            }
        }

        var known = new HashSet<string>();
        foreach (var n in ctx.Db.GetAllNotes())
        {
            known.Add(SlugHelper.NormalizeWiki(n.Title));
            known.Add(SlugHelper.NormalizeWiki(n.Stem));
        }
        foreach (var link in parsed.Links.Where(l => !known.Contains(l.TargetNorm)))
            warnings.Add($"Wiki link target not found: [[{link.Target}]].");

        var duplicates = ctx.Db.FindByTitle(note.Title)
            .Where(n => n.Id != note.Id && !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var dup in duplicates)
        {
            warnings.Add($"Another note shares this title: {dup.Path} (breaks reference resolution).");
            related.Add(dup.Path);
        }

        return new DraftCheckResult(blockers.Count == 0, blockers, warnings, suggestions, related);
    }

    private static bool IsSectionEmpty(string body, string heading) =>
        SectionExtractor.GetSectionText(body, heading) is null &&
        NoteParser.ExtractHeadings(body).Any(h => string.Equals(h.Text, heading, StringComparison.OrdinalIgnoreCase));

    private static HashSet<string> Tokens(string text) =>
        SearchService.Tokenize(text).Where(t => !Stopwords.Contains(t)).ToHashSet(StringComparer.Ordinal);

    private static double Similarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Intersect(b).Count();
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
}
