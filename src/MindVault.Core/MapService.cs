using System.Text;
using System.Text.RegularExpressions;

namespace MindVault.Core;

/// <summary>
/// One entry per project hub: whether the hub already carries a generated map block, plus any
/// remaining legacy <c>09_Maps/</c> file (flagged, never auto-deleted). Path points at the hub
/// note for project rows, or at the legacy file for legacy rows.
/// </summary>
public sealed record MapListEntry(
    string Title, string Path, string? Project, string? Updated, bool HasMapBlock, bool IsLegacy);

public sealed record MapResult(string Path, string? SnapshotPath, string Message, IReadOnlyList<string> Warnings);

/// <summary>
/// The generated project map now lives INSIDE the project hub note (the `type: project` note),
/// appended at the end of its body between the `mindvault-generated` markers. It is an agent
/// route + health view in one read: start-here guidance, decisions/tasks/risks/mistakes,
/// do-not-repeat rules, work areas, recent sessions, and needs-review/orphans/broken-links/
/// unsummarized health sections with an organisation score line. Goal and non-negotiables are
/// deliberately NOT duplicated here — they sit on the same page above the block, so copying them
/// would only rot. Everything between the markers is MindVault's to rewrite; anything a human
/// writes outside the markers is preserved verbatim on rebuild. Summary blocks use distinct
/// (`mindvault-summary`) markers, so a hub can carry both blocks at once.
///
/// The old separate `09_Maps/{Title} Map.md` note and the `09_Maps` folder are retired; Create
/// and Rebuild migrate any such legacy file (archived when it holds no human text, otherwise
/// left with a warning). Files are never deleted.
/// </summary>
public sealed partial class MapService(VaultContext ctx)
{
    public const string MarkerStart = "<!-- mindvault-generated:start -->";
    public const string MarkerEnd = "<!-- mindvault-generated:end -->";
    private const int SectionLimit = 10;
    private const int HealthLimit = 5;

    /// <summary>
    /// Refusal text when a hub's map markers cannot be located unambiguously — a stray literal
    /// marker in prose/a code fence, a duplicated block, or an end-before-start pairing. Naming the
    /// counts and telling the human exactly how to fix it (make each literal appear once) is what
    /// stops us from silently deleting or duplicating their text.
    /// </summary>
    public static string AmbiguityMessage(string projectTitle, int startCount, int endCount) =>
        $"Cannot safely locate the map block on {projectTitle}: the marker strings appear more than once " +
        $"or are malformed (found {startCount} '{MarkerStart}' and {endCount} '{MarkerEnd}'). Edit the note " +
        "so each literal marker string appears exactly once around the real block — if you mention a marker " +
        "in prose or a code fence, break it up (for example put a zero-width space or spaces inside it) so it " +
        "is no longer the exact marker string. No changes were made.";

    public MapResult Create(string project)
    {
        var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
        var body = ReadHubBody(proj)
                   ?? throw new MindVaultException($"Could not read the project hub for {proj.Title}.");

        var located = GeneratedBlocks.Locate(body, MarkerStart, MarkerEnd);
        if (located.Kind == GeneratedBlocks.BlockKind.Ambiguous)
            throw new MindVaultException(AmbiguityMessage(proj.Title, located.StartCount, located.EndCount));
        if (located.Kind == GeneratedBlocks.BlockKind.Single)
            throw new MindVaultException(
                $"{proj.Title} already has a map block. Run 'map rebuild --project \"{proj.Title}\"' to refresh it.");

        var block = BuildGeneratedBlock(proj);
        var newBody = body.TrimEnd('\n') + $"\n\n{MarkerStart}\n{block}\n{MarkerEnd}\n";
        var result = ctx.Writer.ReplaceBody(proj.Path, newBody);

        var warnings = new List<string>();
        var migration = MigrateLegacy(proj, warnings);
        var message = $"Added a map block to {result.Path}" +
                      (migration is { Length: > 0 } ? $"; {migration}" : "");
        return new MapResult(result.Path, result.SnapshotPath, message, warnings);
    }

    public MapResult Rebuild(string project)
    {
        var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
        var body = ReadHubBody(proj)
                   ?? throw new MindVaultException($"Could not read the project hub for {proj.Title}.");

        var warnings = new List<string>();
        var located = GeneratedBlocks.Locate(body, MarkerStart, MarkerEnd);

        // Ambiguous markers: refuse. Writing NOTHING (no snapshot) is the only safe move — guessing
        // the span from first-occurrence positions silently deletes or duplicates human text.
        if (located.Kind == GeneratedBlocks.BlockKind.Ambiguous)
        {
            warnings.Add(AmbiguityMessage(proj.Title, located.StartCount, located.EndCount));
            return new MapResult(proj.Path, null,
                $"Map block on {proj.Title} was not rebuilt — ambiguous markers.", warnings);
        }

        var block = BuildGeneratedBlock(proj);

        if (located.Kind == GeneratedBlocks.BlockKind.Single)
        {
            var start = located.Start;
            var end = located.End;
            // Idempotency: if the existing block equals the new one once the volatile computed
            // lines are ignored (see StripVolatile), write nothing — no snapshot, no churn.
            var existing = body[(start + MarkerStart.Length)..end].Trim('\n');
            if (StripVolatile(existing) == StripVolatile(block))
            {
                var msg = "map block unchanged — nothing written";
                var m = MigrateLegacy(proj, warnings);
                if (m is { Length: > 0 }) msg += $"; {m}";
                return new MapResult(proj.Path, null, msg, warnings);
            }
            // Only the generated block is replaced; human text before and after survives.
            var newBody = body[..(start + MarkerStart.Length)] + "\n" + block + "\n" + body[end..];
            var result = ctx.Writer.ReplaceBody(proj.Path, newBody);
            var migration = MigrateLegacy(proj, warnings);
            var message = $"Rebuilt the map block in {result.Path}" +
                          (migration is { Length: > 0 } ? $"; {migration}" : "");
            return new MapResult(result.Path, result.SnapshotPath, message, warnings);
        }
        else // None
        {
            warnings.Add("Map-block markers were missing — a fresh map block was appended; " +
                         "existing text was left untouched.");
            var newBody = body.TrimEnd('\n') + $"\n\n{MarkerStart}\n{block}\n{MarkerEnd}\n";
            var result = ctx.Writer.ReplaceBody(proj.Path, newBody);
            var migration = MigrateLegacy(proj, warnings);
            var message = $"Rebuilt the map block in {result.Path}" +
                          (migration is { Length: > 0 } ? $"; {migration}" : "");
            return new MapResult(result.Path, result.SnapshotPath, message, warnings);
        }
    }

    public List<MapListEntry> List()
    {
        ctx.Scanner.EnsureFresh();
        var entries = new List<MapListEntry>();

        foreach (var proj in ctx.Db.GetAllNotes()
                     .Where(n => string.Equals(n.Type, "project", StringComparison.OrdinalIgnoreCase))
                     .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase))
        {
            var hasBlock = false;
            try
            {
                var raw = File.ReadAllText(ctx.Resolver.AbsolutePathOf(proj));
                hasBlock = raw.Contains(MarkerStart, StringComparison.Ordinal);
            }
            catch (IOException) { /* presence is best-effort */ }
            entries.Add(new MapListEntry(proj.Title, proj.Path, proj.Title, proj.Updated, hasBlock, false));
        }

        // Any surviving legacy map files under 09_Maps are surfaced as legacy so a human can
        // migrate their text and remove them; they are never touched automatically here.
        foreach (var n in ctx.Db.GetAllNotes()
                     .Where(n => n.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase))
        {
            entries.Add(new MapListEntry(n.Title, n.Path, n.Project, n.Updated, false, true));
        }

        return entries;
    }

    internal static string LegacyMapPathFor(NoteSummary proj) =>
        $"09_Maps/{SlugHelper.SanitizeFileName(proj.Title)} Map.md";

    /// <summary>
    /// Retires the project's legacy 09_Maps file if one exists. With no human text outside the
    /// generated markers → archive it (snapshot-first, same mechanism as archive_note). With
    /// human text → leave it untouched and warn, naming the file. Files are never deleted.
    /// Returns a short message fragment for the caller, or null when there was nothing to do.
    /// </summary>
    private string? MigrateLegacy(NoteSummary proj, List<string> warnings)
    {
        var rel = LegacyMapPathFor(proj);
        var abs = PathGuard.ResolveNotePath(ctx.VaultRoot, rel);
        if (!File.Exists(abs)) return null;

        string humanText;
        try
        {
            var raw = File.ReadAllText(abs).Replace("\r\n", "\n");
            FrontmatterCodec.TryExtract(raw, out _, out var body);
            humanText = ExtractHumanText(body);
        }
        catch (IOException)
        {
            warnings.Add($"Could not read legacy map file {rel} to migrate it — leave it in place.");
            return null;
        }

        if (humanText.Length == 0)
        {
            try
            {
                ctx.Writer.Archive(rel);
                return "legacy map migrated and archived";
            }
            catch (MindVaultException ex)
            {
                warnings.Add($"Could not archive legacy map file {rel} ({ex.Message}) — leave it in place.");
                return null;
            }
        }

        warnings.Add($"Legacy map file {rel} still holds human-written text outside the generated " +
                     "markers — move that text onto the hub manually; the file was left untouched.");
        return null;
    }

    /// <summary>Human text of a legacy map body: everything outside the generated markers, minus
    /// the H1 line and blank lines. Frontmatter is already stripped by the caller.</summary>
    private static string ExtractHumanText(string body)
    {
        var outside = GeneratedBlocks.StripAll(body);
        var kept = outside.Split('\n')
            .Select(l => l.TrimEnd())
            .Where(l => l.Trim().Length > 0)
            .Where(l => !l.TrimStart().StartsWith("# ", StringComparison.Ordinal));
        return string.Join("\n", kept).Trim();
    }

    private string BuildGeneratedBlock(NoteSummary proj)
    {
        var names = ctx.ProjectDetect.QueryNamesFor(proj);
        var sb = new StringBuilder();

        sb.AppendLine("## Start Here");
        sb.AppendLine();
        sb.AppendLine($"- Agent route: `mindvault route --project \"{proj.Title}\"` " +
                      "(token-budgeted read-first list)");
        sb.AppendLine($"- Session brief: `mindvault capsule --project \"{proj.Title}\" --mode coding`");

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
                     .Where(n => states.TryGetValue(n.Path, out var s) && s.ContentSize >= SummaryService.LargeBodyChars)
                     .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase))
        {
            string raw;
            try { raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, n.Path)); }
            catch (IOException) { continue; }
            if (SummaryService.HasSummaryBlock(raw)) continue;
            result.Add((n.Path, TokenEstimator.EstimateBytes(states[n.Path].ContentSize)));
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

    /// <summary>
    /// Drops the volatile computed lines so two blocks that differ only by their rebuild time or a
    /// drifted organisation score compare equal (mirrors <see cref="SummaryService"/>'s date
    /// handling). The score self-references the block's own byte size (it feeds token accounting),
    /// so it can shift by a point purely because the block was materialised — that is not a real
    /// change and must not force an endless rewrite loop.
    /// </summary>
    private static string StripVolatile(string block) =>
        string.Join("\n", block.Split('\n')
            .Where(l => !l.Contains("`mindvault map rebuild", StringComparison.Ordinal))
            .Where(l => !ScoreLinePattern().IsMatch(l)));

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

    /// <summary>The "NN/100 — …" organisation-score line under the Organisation Score heading.</summary>
    [GeneratedRegex(@"^\d{1,3}/100\b")]
    private static partial Regex ScoreLinePattern();
}
