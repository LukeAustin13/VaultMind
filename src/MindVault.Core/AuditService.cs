namespace MindVault.Core;

public sealed record AuditFinding(string Severity, string Code, string? Path, string Issue, string? Proposal);

public sealed record AuditReport(int NotesChecked, IReadOnlyList<AuditFinding> Findings, bool Truncated);

/// <summary>
/// Read-only quality audits with proposals, never auto-fixes: frontmatter completeness and
/// consistency, and alias hygiene across project notes. Every finding says what is wrong
/// and what command would fix it — the human (or agent, with approval) applies changes.
/// </summary>
public sealed class AuditService(VaultContext ctx)
{
    public const int MaxFindings = 100;

    private static readonly string[] ProjectScopedTypes =
        ["decision", "task", "risk", "constraint", "architecture", "review"];

    public AuditReport AuditFrontmatter(string? project = null)
    {
        ctx.Scanner.EnsureFresh();
        var findings = new List<AuditFinding>();
        var archive = ctx.Config.DefaultArchiveFolder;

        string[]? names = null;
        long projId = -1;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project.Trim());
            names = ctx.ProjectDetect.QueryNamesFor(proj);
            projId = proj.Id;
        }

        var notes = ctx.Db.GetAllNotes()
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Where(n => !n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase))
            .Where(n => !string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase))
            .Where(n => names is null || n.Id == projId ||
                        (n.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var keyPresence = ctx.Db.GetFrontmatterKeyPresence(NoteTypes.RequiredFrontmatterKeys.ToList());
        var outgoingByNote = ctx.Db.GetAllLinks().ToLookup(l => l.NoteId);
        var aliasRows = ctx.ProjectDetect.LoadAliases();
        var detections = new Dictionary<string, ProjectDetection>(StringComparer.OrdinalIgnoreCase);

        ProjectDetection DetectCached(string name)
        {
            if (!detections.TryGetValue(name, out var d)) detections[name] = d = ctx.ProjectDetect.Detect(name);
            return d;
        }

        foreach (var note in notes)
        {
            if (note.ParseError is not null)
            {
                var code = note.ParseError.StartsWith("yaml-nested") ? "nested-yaml" : "invalid-yaml";
                findings.Add(new("critical", code, note.Path, note.ParseError,
                    "fix the YAML in Obsidian — frontmatter must stay a flat mapping"));
                continue;
            }

            var managed = NoteTypes.IsManaged(note.Type);
            if (managed)
            {
                foreach (var key in NoteTypes.RequiredFrontmatterKeys.Where(k => !keyPresence[k].Contains(note.Id)))
                    findings.Add(new("critical", "missing-frontmatter", note.Path,
                        $"managed note ({note.Type}) is missing '{key}'",
                        $"add '{key}:' via update-frontmatter"));
                if (keyPresence["status"].Contains(note.Id) && !NoteTypes.IsValidStatus(note.Status ?? ""))
                    findings.Add(new("critical", "invalid-status", note.Path,
                        $"invalid status '{note.Status}'",
                        $"use one of: {string.Join(", ", NoteTypes.Statuses)}"));
            }

            var projectScoped = note.Type is not null &&
                                ProjectScopedTypes.Contains(note.Type, StringComparer.OrdinalIgnoreCase);
            if (projectScoped && string.IsNullOrWhiteSpace(note.Project))
            {
                findings.Add(new("warning", "missing-project", note.Path,
                    $"{note.Type} note has no project:",
                    "set project: '<name>' via update-frontmatter"));
            }

            if (note.Project is { Length: > 0 } projName)
            {
                var d = DetectCached(projName);
                if (d.Project is null)
                {
                    findings.Add(new(d.Ambiguous ? "warning" : "critical", "project-unresolved", note.Path,
                        d.Ambiguous
                            ? $"project '{projName}' matches more than one project note"
                            : $"project '{projName}' does not resolve to any project note",
                        d.Ambiguous
                            ? "make the project: value unambiguous"
                            : "create the project note or fix project:"));
                }
                else
                {
                    if (!string.Equals(projName.Trim(), d.Project.Title, StringComparison.OrdinalIgnoreCase))
                        findings.Add(new("info", "project-name-inconsistent", note.Path,
                            $"project: '{projName}' resolves to '{d.Project.Title}' via {d.MatchedVia}",
                            $"normalize it: update-frontmatter --key project --value \"{d.Project.Title}\""));

                    var linkWorthy = projectScoped ||
                                     string.Equals(note.Type, "memory", StringComparison.OrdinalIgnoreCase);
                    if (managed && linkWorthy && note.Id != d.Project.Id)
                    {
                        var hubTitleNorm = SlugHelper.NormalizeWiki(d.Project.Title);
                        var hubStemNorm = SlugHelper.NormalizeWiki(d.Project.Stem);
                        if (!outgoingByNote[note.Id].Any(l =>
                                l.TargetNorm == hubTitleNorm || l.TargetNorm == hubStemNorm))
                        {
                            findings.Add(new("info", "unlinked-to-project", note.Path,
                                $"carries project: '{d.Project.Title}' but does not link to the hub",
                                $"link it: link --from \"{note.Stem}\" --to \"{d.Project.Stem}\""));
                        }
                    }
                }
            }

            if (string.Equals(note.Type, "project", StringComparison.OrdinalIgnoreCase))
            {
                var has = aliasRows.TryGetValue(note.Id, out var entry);
                if (!has || entry.Aliases.Count == 0)
                    findings.Add(new("info", "missing-aliases", note.Path,
                        "project note declares no aliases:",
                        "add aliases: so shorthand names resolve to this project"));
                if (!has || entry.RepoNames.Count == 0)
                    findings.Add(new("info", "missing-reponames", note.Path,
                        "project note declares no repoNames:",
                        "add repoNames: so repo folder names resolve to this project"));
            }
        }

        return Finish(notes.Count, findings);
    }

    public AuditReport AuditAliases()
    {
        ctx.Scanner.EnsureFresh();
        var findings = new List<AuditFinding>();
        var projects = ctx.Db.Query(type: "project", limit: 1000)
            .Where(p => !p.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var aliases = ctx.ProjectDetect.LoadAliases();

        // Every name a project answers to (normalized), with owner and kind.
        var claims = new Dictionary<string, List<(NoteSummary Project, string Kind, string Raw)>>(StringComparer.Ordinal);
        void Claim(string raw, NoteSummary p, string kind)
        {
            var norm = SlugHelper.NormalizeWiki(raw);
            if (norm.Length == 0) return;
            if (!claims.TryGetValue(norm, out var list)) claims[norm] = list = [];
            list.Add((p, kind, raw));
        }

        foreach (var p in projects)
        {
            Claim(p.Title, p, "title");
            if (!string.Equals(p.Title, p.Stem, StringComparison.OrdinalIgnoreCase))
                Claim(p.Stem, p, "title");

            if (aliases.TryGetValue(p.Id, out var entry))
            {
                foreach (var a in entry.Aliases) Claim(a, p, "alias");
                foreach (var r in entry.RepoNames) Claim(r, p, "repoName");

                foreach (var dup in entry.Aliases.Concat(entry.RepoNames)
                             .GroupBy(SlugHelper.NormalizeWiki).Where(g => g.Count() > 1))
                {
                    findings.Add(new("warning", "duplicate-alias", p.Path,
                        $"'{dup.First()}' is declared more than once on this project",
                        "remove the duplicate entry"));
                }
                foreach (var a in entry.Aliases.Where(a =>
                             SlugHelper.NormalizeWiki(a) == SlugHelper.NormalizeWiki(p.Title) ||
                             SlugHelper.NormalizeWiki(a) == SlugHelper.NormalizeWiki(p.Stem)))
                {
                    findings.Add(new("info", "redundant-alias", p.Path,
                        $"alias '{a}' just repeats the project's own name",
                        "remove it — titles already resolve"));
                }
            }
            else
            {
                findings.Add(new("info", "missing-aliases", p.Path,
                    "project declares no aliases: or repoNames:",
                    "add them so repo folders and shorthand resolve to this project"));
            }
        }

        // Cross-project collisions: a name claimed by 2+ projects makes detection refuse to
        // guess. Pure duplicate titles are validation's domain (duplicate-title) — skipped.
        var collidedPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, owners) in claims.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            var distinct = owners.DistinctBy(o => o.Project.Id).ToList();
            if (distinct.Count < 2 || owners.All(o => o.Kind == "title")) continue;
            foreach (var pair in PairKeys(distinct.Select(o => o.Project.Id))) collidedPairs.Add(pair);
            findings.Add(new("critical", "alias-collision", null,
                $"'{owners[0].Raw}' is claimed by " +
                string.Join(" and ", distinct.Select(o => $"{o.Project.Title} ({o.Kind} '{o.Raw}')")),
                "keep the name on exactly one project — detection refuses to guess between them"));
        }

        // Condensed collisions (tier-4 matching: separators/case stripped) that plain
        // normalization missed.
        var condensed = new Dictionary<string, List<(NoteSummary Project, string Raw)>>(StringComparer.Ordinal);
        foreach (var (_, owners) in claims)
        {
            foreach (var o in owners)
            {
                var key = ProjectDetectService.Condense(o.Raw);
                if (key.Length == 0) continue;
                if (!condensed.TryGetValue(key, out var list)) condensed[key] = list = [];
                list.Add((o.Project, o.Raw));
            }
        }
        foreach (var (key, owners) in condensed.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            var distinct = owners.DistinctBy(o => o.Project.Id).ToList();
            if (distinct.Count < 2) continue;
            if (PairKeys(distinct.Select(o => o.Project.Id)).All(collidedPairs.Contains)) continue;
            findings.Add(new("warning", "alias-collision-condensed", null,
                $"{string.Join(" and ", distinct.Select(o => $"'{o.Raw}' ({o.Project.Title})"))} " +
                $"condense to the same key '{key}' — repo-style names will be ambiguous",
                "rename one of them so condensed matching stays unambiguous"));
        }

        return Finish(projects.Count, findings);
    }

    private static IEnumerable<string> PairKeys(IEnumerable<long> ids)
    {
        var sorted = ids.Distinct().OrderBy(i => i).ToList();
        for (var i = 0; i < sorted.Count; i++)
            for (var j = i + 1; j < sorted.Count; j++)
                yield return $"{sorted[i]}|{sorted[j]}";
    }

    private static AuditReport Finish(int notesChecked, List<AuditFinding> findings)
    {
        var ordered = findings
            .OrderBy(f => SeverityRank(f.Severity))
            .ThenBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new AuditReport(notesChecked, ordered.Take(MaxFindings).ToList(), ordered.Count > MaxFindings);
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "critical" => 0,
        "warning" => 1,
        _ => 2,
    };
}
