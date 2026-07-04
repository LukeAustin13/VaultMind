using System.Text;

namespace MindVault.Core;

public sealed record MapListEntry(string Title, string Path, string? Project, string? Updated);

public sealed record MapResult(string Path, string? SnapshotPath, string Message, IReadOnlyList<string> Warnings);

/// <summary>
/// Map-of-content notes: one generated, compact navigation hub per project in 09_Maps.
/// v2 blocks are agent route + health in one read: start-here guidance, goal,
/// non-negotiables, decisions/tasks/risks/mistakes, do-not-repeat rules, work areas,
/// recent sessions, needs-review/orphans/broken-links/unsummarized health sections and an
/// organisation score line. Everything between the generated markers is MindVault's to
/// rewrite; anything a human writes outside the markers is preserved verbatim on rebuild.
/// Maps carry `type: map`, deliberately not a managed type — generated artifacts, not memory.
/// </summary>
public sealed class MapService(VaultContext ctx)
{
    public const string MarkerStart = "<!-- mindvault-generated:start -->";
    public const string MarkerEnd = "<!-- mindvault-generated:end -->";
    private const int SectionLimit = 10;
    private const int HealthLimit = 5;

    public MapResult Create(string project)
    {
        var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
        var rel = MapPathFor(proj);
        if (ctx.Db.FindByPath(rel) is not null ||
            File.Exists(PathGuard.ResolveNotePath(ctx.VaultRoot, rel)))
        {
            throw new MindVaultException(
                $"Map already exists: {rel}. Run 'map rebuild --project \"{proj.Title}\"' to refresh it.");
        }

        var today = DateTime.Now.ToString("yyyy-MM-dd");
        var content = $"""
            ---
            type: map
            status: active
            created: {today}
            updated: {today}
            project: {proj.Title}
            tags:
              - map
            links:
              - "[[{proj.Stem}]]"
            ---

            # {proj.Title} Map

            {MarkerStart}
            {BuildGeneratedBlock(proj)}
            {MarkerEnd}

            """;
        var note = ctx.Writer.CreateNoteFile(rel, content);
        return new MapResult(note.Path, null, $"Created {note.Path}", []);
    }

    public MapResult Rebuild(string project)
    {
        var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
        var rel = MapPathFor(proj);
        var abs = PathGuard.ResolveNotePath(ctx.VaultRoot, rel);
        if (!File.Exists(abs))
            throw new MindVaultException(
                $"No map found at {rel}. Run 'map create --project \"{proj.Title}\"' first.");

        var raw = File.ReadAllText(abs).Replace("\r\n", "\n");
        FrontmatterCodec.TryExtract(raw, out _, out var body);

        var warnings = new List<string>();
        var block = BuildGeneratedBlock(proj);
        string newBody;
        var start = body.IndexOf(MarkerStart, StringComparison.Ordinal);
        var end = body.IndexOf(MarkerEnd, StringComparison.Ordinal);
        if (start >= 0 && end > start)
        {
            // Only the generated block is replaced; human text before and after survives.
            newBody = body[..(start + MarkerStart.Length)] + "\n" + block + "\n" + body[end..];
        }
        else
        {
            warnings.Add("Generated-block markers were missing — a fresh generated block was " +
                         "appended; existing text was left untouched.");
            newBody = body.TrimEnd('\n') + $"\n\n{MarkerStart}\n{block}\n{MarkerEnd}\n";
        }

        var result = ctx.Writer.ReplaceBody(rel, newBody);
        return new MapResult(result.Path, result.SnapshotPath,
            $"Rebuilt the generated block in {result.Path}", warnings);
    }

    public List<MapListEntry> List()
    {
        ctx.Scanner.EnsureFresh();
        return ctx.Db.GetAllNotes()
            .Where(n => n.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
            .Select(n => new MapListEntry(n.Title, n.Path, n.Project, n.Updated))
            .ToList();
    }

    internal static string MapPathFor(NoteSummary proj) =>
        $"09_Maps/{SlugHelper.SanitizeFileName(proj.Title)} Map.md";

    private string BuildGeneratedBlock(NoteSummary proj)
    {
        var names = ctx.ProjectDetect.QueryNamesFor(proj);
        var hubBody = ReadHubBody(proj);
        var sb = new StringBuilder();

        sb.AppendLine("## Start Here");
        sb.AppendLine();
        sb.AppendLine($"- Hub: [[{proj.Stem}]] — goal and non-negotiables");
        sb.AppendLine($"- Agent route: `mindvault route --project \"{proj.Title}\"` " +
                      "(token-budgeted read-first list)");
        sb.AppendLine($"- Session brief: `mindvault capsule --project \"{proj.Title}\" --mode coding`");

        sb.AppendLine();
        sb.AppendLine("## Current Goal");
        sb.AppendLine();
        sb.AppendLine((hubBody is null ? null : SectionExtractor.GetSectionText(hubBody, "Goal", 300))
                      ?? "_(no Goal section content on the project note)_");

        var nonNeg = hubBody is null ? [] : SectionExtractor.GetBullets(hubBody, "Non-Negotiables");
        sb.AppendLine();
        sb.AppendLine("## Non-Negotiables");
        sb.AppendLine();
        if (nonNeg.Count == 0) sb.AppendLine("_(none recorded on the hub)_");
        else foreach (var n in nonNeg) sb.AppendLine($"- {n}");

        Section(sb, "Key Decisions",
            ctx.Db.Query(type: "decision", projectNames: names, statusNot: "archived", limit: SectionLimit));
        Section(sb, "Active Tasks",
            ctx.Db.Query(type: "task", projectNames: names, statusIn: ["open", "active", "blocked"], limit: SectionLimit));
        Section(sb, "Open Risks",
            ctx.Db.Query(type: "risk", projectNames: names, statusIn: ["open", "active", "blocked"], limit: SectionLimit));
        Section(sb, "Known Mistakes",
            ctx.Db.Query(type: "mistake", projectNames: names, statusNot: "archived", limit: SectionLimit));

        sb.AppendLine();
        sb.AppendLine("## Do Not Repeat");
        sb.AppendLine();
        var rules = PreventionRules(names);
        if (rules.Count == 0) sb.AppendLine("_(no prevention rules recorded)_");
        else foreach (var r in rules) sb.AppendLine($"- {r}");

        Section(sb, "Constraints",
            ctx.Db.Query(type: "constraint", projectNames: names, statusNot: "archived", limit: SectionLimit));

        sb.AppendLine();
        sb.AppendLine("## Work Areas");
        sb.AppendLine();
        var scoped = ctx.Db.GetAllNotes()
            .Where(n => n.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var areas = scoped.GroupBy(n => n.Path.Split('/')[0], StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (areas.Count == 0) sb.AppendLine("_(none)_");
        else foreach (var a in areas) sb.AppendLine($"- {a.Key} — {a.Count()} note(s)");

        sb.AppendLine();
        sb.AppendLine("## Recent Sessions");
        sb.AppendLine();
        var logPath = $"06_Agent_Memory/Log - {proj.Stem}.md";
        var sessions = ctx.Db.FindByPath(logPath) is null
            ? []
            : ctx.Sessions.Recent(proj.Title, 3);
        if (sessions.Count == 0) sb.AppendLine("_(no sessions logged)_");
        else foreach (var s in sessions) sb.AppendLine($"- {s.Kind}: {s.Heading}");

        Section(sb, "Recent Implementation Logs",
            ctx.Db.Query(type: "memory", projectNames: names, tag: "log", statusNot: "archived", limit: 5));
        Section(sb, "Reviews",
            ctx.Db.Query(type: "review", projectNames: names, statusNot: "archived", limit: 5));
        Section(sb, "Related Prompts",
            ctx.Db.Query(type: "prompt", projectNames: names, statusNot: "archived", limit: 5));

        // Health: what needs a human, kept short — details live in the dedicated commands.
        sb.AppendLine();
        sb.AppendLine("## Needs Review");
        sb.AppendLine();
        var review = ctx.Organizer.Plan(proj.Title).NeedsReview.Take(HealthLimit).ToList();
        if (review.Count == 0) sb.AppendLine("_(none)_");
        else foreach (var r in review) sb.AppendLine($"- {r.Path} — {r.Reason}");

        sb.AppendLine();
        sb.AppendLine("## Orphans");
        sb.AppendLine();
        var orphans = ctx.LinkIntel.Orphans(names).Rows.Take(HealthLimit).ToList();
        if (orphans.Count == 0) sb.AppendLine("_(none)_");
        else foreach (var o in orphans) sb.AppendLine($"- [[{StemOf(o.Path)}]] — unlinked {o.Type}");

        sb.AppendLine();
        sb.AppendLine("## Broken Links");
        sb.AppendLine();
        var scopedPaths = scoped.Select(n => n.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        scopedPaths.Add(proj.Path);
        var broken = ctx.LinkIntel.BrokenLinks().Rows
            .Where(b => scopedPaths.Contains(b.FromPath))
            .Take(HealthLimit)
            .ToList();
        if (broken.Count == 0) sb.AppendLine("_(none)_");
        else foreach (var b in broken) sb.AppendLine($"- {b.FromPath} → [[{b.Target}]]");

        sb.AppendLine();
        sb.AppendLine("## Large Notes Missing Summaries");
        sb.AppendLine();
        var unsummarized = LargeUnsummarized(scoped, proj);
        if (unsummarized.Count == 0) sb.AppendLine("_(none)_");
        else foreach (var (path, tokens) in unsummarized)
            sb.AppendLine($"- [[{StemOf(path)}]] — ~{tokens} tokens, no summary block");

        sb.AppendLine();
        sb.AppendLine("## Organisation Score");
        sb.AppendLine();
        var score = ctx.OrgScore.Run(proj.Title);
        sb.AppendLine($"{score.OverallScore}/100" + (score.Weaknesses.Count == 0
            ? " — no weak categories"
            : $" — weakest: {score.Weaknesses[0]}"));

        sb.AppendLine();
        sb.AppendLine("## Last Rebuilt");
        sb.AppendLine();
        sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm} — `mindvault map rebuild --project \"{proj.Title}\"`");

        return sb.ToString().Replace("\r\n", "\n").TrimEnd('\n');
    }

    /// <summary>Prevention rules from active mistakes — the same rules capsules carry.</summary>
    private List<string> PreventionRules(string[] names)
    {
        var rules = new List<string>();
        foreach (var m in ctx.Db.Query(type: "mistake", projectNames: names,
                     statusIn: ["active", "open"], limit: HealthLimit))
        {
            string? rule = null;
            try
            {
                var raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, m.Path))
                    .Replace("\r\n", "\n");
                FrontmatterCodec.TryExtract(raw, out _, out var body);
                rule = SectionExtractor.GetSectionText(body, "Prevention Task", 160)
                       ?? SectionExtractor.GetSectionText(body, "How To Avoid It", 160);
            }
            catch (IOException) { /* rule is best-effort */ }
            rules.Add($"{rule ?? m.Title} ([[{StemOf(m.Path)}]])");
        }
        return rules;
    }

    private List<(string Path, int Tokens)> LargeUnsummarized(List<NoteSummary> scoped, NoteSummary proj)
    {
        var states = ctx.Db.GetFileStates();
        var result = new List<(string, int)>();
        foreach (var n in scoped.Concat([proj])
                     .Where(n => NoteTypes.IsManaged(n.Type))
                     .Where(n => !string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase))
                     .Where(n => states.TryGetValue(n.Path, out var s) && s.Size >= SummaryService.LargeBodyChars)
                     .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase))
        {
            string raw;
            try { raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, n.Path)); }
            catch (IOException) { continue; }
            if (SummaryService.HasSummaryBlock(raw)) continue;
            result.Add((n.Path, TokenEstimator.EstimateBytes(states[n.Path].Size)));
            if (result.Count >= HealthLimit) break;
        }
        return result;
    }

    private static void Section(StringBuilder sb, string heading, List<NoteSummary> items)
    {
        sb.AppendLine();
        sb.AppendLine($"## {heading}");
        sb.AppendLine();
        if (items.Count == 0)
        {
            sb.AppendLine("_(none)_");
            return;
        }
        foreach (var n in items)
        {
            var status = n.Status is { Length: > 0 } s ? $" — {s}" : "";
            var updated = n.Updated is { Length: > 0 } u ? $" ({u})" : "";
            sb.AppendLine($"- [[{n.Stem}]]{status}{updated}");
        }
    }

    private string? ReadHubBody(NoteSummary proj)
    {
        try
        {
            var raw = File.ReadAllText(ctx.Resolver.AbsolutePathOf(proj)).Replace("\r\n", "\n");
            FrontmatterCodec.TryExtract(raw, out _, out var body);
            return body;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string StemOf(string path) =>
        Path.GetFileNameWithoutExtension(path);
}
