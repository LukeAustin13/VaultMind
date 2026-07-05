using System.Text;

namespace MindVault.Core;

public sealed record SummaryProposal(
    string Title, string Path, bool HadBlock, bool NeedsReview, string Summary);

public sealed record SummarizeReport(
    bool DryRun, int NotesConsidered, IReadOnlyList<SummaryProposal> Proposals,
    int Applied, IReadOnlyList<string> Warnings);

/// <summary>
/// Deterministic extractive summaries in a dedicated generated block
/// (`mindvault-summary` markers, distinct from map `mindvault-generated` markers so both
/// can coexist). No LLM, no invention: summary = the note's own first paragraph, key
/// points = its own headings/bullets, agentUse = a fixed phrase per note type. Rebuilds
/// splice only the block; human text is untouched. Project-wide runs are dry-run by
/// default and snapshot-first on apply (via ReplaceBody).
/// </summary>
public sealed class SummaryService(VaultContext ctx)
{
    public const string MarkerStart = "<!-- mindvault-summary:start -->";
    public const string MarkerEnd = "<!-- mindvault-summary:end -->";

    /// <summary>Bodies at or above this raw size (~600 tokens) earn a generated summary.</summary>
    public const int LargeBodyChars = 2400;
    private const int SummaryMaxChars = 200;
    private const int MaxKeyPoints = 5;
    private const int MaxProposals = 100;

    public static bool HasSummaryBlock(string rawOrBody) =>
        rawOrBody.Contains(MarkerStart, StringComparison.Ordinal);

    /// <summary>The one-line `summary:` text from a note's generated block, or null. Returns null
    /// on ambiguous markers rather than reading from a guessed span (this feeds read-only snippets,
    /// so refusing is safe — the block just contributes no snippet).</summary>
    public static string? ExtractSummaryLine(string body)
    {
        var located = GeneratedBlocks.Locate(body, MarkerStart, MarkerEnd);
        if (located.Kind != GeneratedBlocks.BlockKind.Single) return null;
        var start = located.Start;
        var end = located.End;
        foreach (var line in body[(start + MarkerStart.Length)..end].Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("summary:", StringComparison.OrdinalIgnoreCase))
            {
                var text = t["summary:".Length..].Trim();
                return text.Length > 0 ? text : null;
            }
        }
        return null;
    }

    public SummarizeReport ForNote(string noteRef, bool apply = false)
    {
        var note = ctx.Resolver.Resolve(noteRef);
        var warnings = new List<string>();
        var proposal = Propose(note, out var newBody, out var changed, out var skipWarning);
        if (skipWarning is not null)
            throw new MindVaultException(skipWarning);
        if (proposal is null)
            throw new MindVaultException(
                $"'{note.Title}' is a {note.Type ?? "untyped"} note — summaries apply to managed notes, not maps or templates.");

        var applied = 0;
        if (apply && changed)
        {
            ctx.Writer.ReplaceBody(note.Path, newBody!);
            applied = 1;
        }
        else if (apply)
        {
            warnings.Add($"{note.Path}: summary block already up to date — nothing written.");
        }
        return new SummarizeReport(!apply, 1, [proposal], applied, warnings);
    }

    public SummarizeReport ForProject(string? project, bool apply = false)
    {
        ctx.Scanner.EnsureFresh();
        string[]? names = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project!);
            names = ctx.ProjectDetect.QueryNamesFor(proj);
        }

        var states = ctx.Db.GetFileStates();
        var archive = ctx.Config.DefaultArchiveFolder;
        var candidates = ctx.Db.GetAllNotes()
            .Where(n => NoteTypes.IsManaged(n.Type))
            .Where(n => !string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase))
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Where(n => !n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase))
            .Where(n => n.Status is null ||
                        !(n.Status.Equals("archived", StringComparison.OrdinalIgnoreCase) ||
                          n.Status.Equals("superseded", StringComparison.OrdinalIgnoreCase)))
            .Where(n => names is null ||
                        (n.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .Where(n => states.TryGetValue(n.Path, out var s) && s.ContentSize >= LargeBodyChars)
            .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var warnings = new List<string>();
        if (candidates.Count > MaxProposals)
        {
            warnings.Add($"showing {MaxProposals} of {candidates.Count} candidate notes — re-run to continue.");
            candidates = candidates.Take(MaxProposals).ToList();
        }

        var proposals = new List<SummaryProposal>();
        var applied = 0;
        var unchanged = 0;
        foreach (var note in candidates)
        {
            var proposal = Propose(note, out var newBody, out var changed, out var skipWarning);
            if (skipWarning is not null) { warnings.Add(skipWarning); continue; }
            if (proposal is null) continue;
            proposals.Add(proposal);
            if (!changed) { unchanged++; continue; }
            if (apply)
            {
                ctx.Writer.ReplaceBody(note.Path, newBody!);
                applied++;
            }
        }
        if (unchanged > 0)
            warnings.Add($"{unchanged} note(s) already have an up-to-date summary block.");
        return new SummarizeReport(!apply, candidates.Count, proposals, applied, warnings);
    }

    /// <summary>
    /// Refusal text when a note's summary markers cannot be located unambiguously — a stray literal
    /// marker in prose/a code fence, a duplicated block, or an end-before-start pairing. Naming the
    /// counts and the fix keeps us from silently deleting or duplicating the human's text.
    /// </summary>
    internal static string AmbiguityMessage(string path, int startCount, int endCount) =>
        $"Cannot safely locate the summary block in {path}: the marker strings appear more than once " +
        $"or are malformed (found {startCount} '{MarkerStart}' and {endCount} '{MarkerEnd}'). Edit the note " +
        "so each literal marker string appears exactly once around the real block — if you mention a marker " +
        "in prose or a code fence, break it up so it is no longer the exact marker string. No changes were made.";

    /// <summary>Builds the proposal for one note. Null for maps/templates. `changed` is
    /// false when the existing block matches the regenerated one (date line ignored).
    /// `skipWarning` is set (and the proposal is null) when the summary markers are ambiguous —
    /// the note is skipped rather than spliced, so we never guess which text to overwrite.</summary>
    private SummaryProposal? Propose(NoteSummary note, out string? newBody, out bool changed, out string? skipWarning)
    {
        newBody = null;
        changed = false;
        skipWarning = null;
        // legacy shields: un-migrated 09_Maps files / type: map notes are generated artifacts,
        // never summarized (project hubs, by contrast, remain summarizable — see Propose above).
        if (string.Equals(note.Type, "map", StringComparison.OrdinalIgnoreCase) ||
            note.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase) ||
            note.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, note.Path)).Replace("\r\n", "\n");
        FrontmatterCodec.TryExtract(raw, out _, out var body);

        var located = GeneratedBlocks.Locate(body, MarkerStart, MarkerEnd);
        if (located.Kind == GeneratedBlocks.BlockKind.Ambiguous)
        {
            skipWarning = AmbiguityMessage(note.Path, located.StartCount, located.EndCount);
            return null;
        }
        var hadBlock = located.Kind == GeneratedBlocks.BlockKind.Single;
        // The summary is derived from human text only: strip BOTH generated regions (the map
        // block on a project hub as well as any existing summary block). Splice still targets the
        // full body so the map block is preserved in place.
        var bodyWithout = GeneratedBlocks.StripAll(body);
        var (block, needsReview, summaryLine) = BuildBlock(note, bodyWithout);

        if (hadBlock && StripVolatile(ExistingBlock(body, located)) == StripVolatile(block))
        {
            return new SummaryProposal(note.Title, note.Path, hadBlock, needsReview, summaryLine);
        }

        newBody = Splice(body, block, located);
        changed = true;
        return new SummaryProposal(note.Title, note.Path, hadBlock, needsReview, summaryLine);
    }

    private static (string Block, bool NeedsReview, string SummaryLine) BuildBlock(
        NoteSummary note, string bodyWithoutBlock)
    {
        var summary = FirstParagraph(bodyWithoutBlock);
        var fallback = summary is null;
        summary ??= $"{note.Type ?? "note"}: {note.Title}.";

        var keyPoints = NoteParser.ExtractHeadings(bodyWithoutBlock)
            .Where(h => h.Level is 2 or 3)
            .Select(h => h.Text)
            .Take(MaxKeyPoints)
            .ToList();
        if (keyPoints.Count < 2)
        {
            keyPoints.AddRange(bodyWithoutBlock.Split('\n')
                .Select(l => l.TrimStart())
                .Where(l => l.StartsWith("- ") || l.StartsWith("* "))
                .Select(l => l[2..].Trim())
                .Where(l => l.Length > 0 && !keyPoints.Contains(l))
                .Take(MaxKeyPoints - keyPoints.Count));
        }

        var needsReview = fallback || keyPoints.Count == 0 || bodyWithoutBlock.Trim().Length < 200;

        var sb = new StringBuilder();
        sb.Append(MarkerStart).Append('\n');
        sb.Append("summary: ").Append(summary).Append('\n');
        sb.Append("agentUse: ").Append(AgentUse(note.Type, note.Status)).Append('\n');
        if (keyPoints.Count > 0)
        {
            sb.Append("keyPoints:\n");
            foreach (var p in keyPoints) sb.Append("- ").Append(p).Append('\n');
        }
        if (needsReview) sb.Append("needsReview: true\n");
        sb.Append("source: generated from headings/frontmatter/body\n");
        sb.Append("updated: ").Append(DateTime.Now.ToString("yyyy-MM-dd")).Append('\n');
        sb.Append(MarkerEnd);
        return (sb.ToString(), needsReview, summary);
    }

    /// <summary>Fixed per-type phrase — why an agent would spend tokens on this note.</summary>
    private static string AgentUse(string? type, string? status) => type?.ToLowerInvariant() switch
    {
        "project" => "Project hub — goal, non-negotiables and open questions live here.",
        "decision" when string.Equals(status, "superseded", StringComparison.OrdinalIgnoreCase)
            => "Superseded decision — historical context only; do not treat as in force.",
        "decision" => "Decision in force — check before contradicting it.",
        "task" or "bug" or "feature" => "Tracks active work — read for scope and acceptance criteria.",
        "risk" => "Open risk — check before changing related behaviour.",
        "mistake" => "Do-not-repeat rule — read the prevention before similar work.",
        "constraint" => "Non-negotiable constraint on this project.",
        "architecture" => "How the system fits together — read before structural changes.",
        "review" => "Recorded review findings — check before re-reviewing the same area.",
        "memory" => "Implementation log — historical context, usually skimmable.",
        "meeting" => "Meeting record — decisions made here should exist as decision notes.",
        "research" => "Collected research — background, not binding.",
        "prompt" => "Reusable prompt text.",
        _ => "Reference note.",
    };

    /// <summary>First real paragraph: consecutive plain-text lines, capped and word-safe.</summary>
    private static string? FirstParagraph(string body)
    {
        var lines = body.Split('\n');
        var collected = new List<string>();
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (t.Length == 0)
            {
                if (collected.Count > 0) break;
                continue;
            }
            if (t.StartsWith('#') || t.StartsWith('-') || t.StartsWith('*') || t.StartsWith('>') ||
                t.StartsWith('|') || t.StartsWith('!') || t.StartsWith("<!--", StringComparison.Ordinal) ||
                t.StartsWith("```", StringComparison.Ordinal))
            {
                if (collected.Count > 0) break;
                continue;
            }
            collected.Add(t);
        }
        if (collected.Count == 0) return null;
        var text = string.Join(" ", collected);
        if (text.Length <= SummaryMaxChars) return text;
        var cut = text.LastIndexOf(' ', SummaryMaxChars);
        return text[..(cut > 40 ? cut : SummaryMaxChars)].TrimEnd() + " …";
    }

    private static string ExistingBlock(string body, GeneratedBlocks.BlockLocation located) =>
        located.Kind == GeneratedBlocks.BlockKind.Single
            ? body[located.Start..(located.End + MarkerEnd.Length)]
            : "";

    private static string StripVolatile(string block) =>
        string.Join("\n", block.Split('\n').Where(l => !l.TrimStart().StartsWith("updated:", StringComparison.Ordinal)));

    /// <summary>Replaces the existing block, or inserts one after the H1 (or at the top). The
    /// caller passes the already-classified location; on Single we splice the found span, on None
    /// we insert after the H1 — byte-identical to the prior behaviour for well-formed notes.</summary>
    private static string Splice(string body, string block, GeneratedBlocks.BlockLocation located)
    {
        if (located.Kind == GeneratedBlocks.BlockKind.Single)
            return body[..located.Start] + block + body[(located.End + MarkerEnd.Length)..];

        var lines = body.Split('\n').ToList();
        var insertAt = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            if (lines[i].StartsWith("# ", StringComparison.Ordinal)) insertAt = i + 1;
            break;
        }
        lines.Insert(insertAt, "");
        lines.Insert(insertAt + 1, block);
        return string.Join("\n", lines);
    }
}
