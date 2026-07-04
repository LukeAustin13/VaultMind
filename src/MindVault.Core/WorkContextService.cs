using System.Text.RegularExpressions;

namespace MindVault.Core;

public sealed record WorkContextItem(string Title, string Path, string? Type, string? Status, string Reason);

public sealed record WorkContextResult(
    string Project, string InputKind, string Input,
    IReadOnlyList<WorkContextItem> Decisions,
    IReadOnlyList<WorkContextItem> Tasks,
    IReadOnlyList<WorkContextItem> Risks,
    IReadOnlyList<WorkContextItem> Mistakes,
    IReadOnlyList<WorkContextItem> Reviews,
    IReadOnlyList<WorkContextItem> Logs,
    IReadOnlyList<ContextRead> SuggestedReads,
    IReadOnlyList<string> Warnings);

/// <summary>
/// "I am working on X — what memory touches it?" Seeds come from exactly one input — a
/// source file (token-matched via FTS), a free-text query, or an existing note (graph
/// expansion) — then deterministic boosts apply: same project, actionable status, pinned
/// and positive feedback. Hidden, archived, superseded, template, map and thought notes
/// never appear. Every result carries its reasons.
/// </summary>
public sealed partial class WorkContextService(VaultContext ctx)
{
    private sealed class Entry(NoteSummary note)
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

    public WorkContextResult Get(string project, string? currentFile = null, string? query = null,
        string? noteRef = null, int limit = 12)
    {
        limit = Math.Clamp(limit, 1, 30);
        var given = new[] { currentFile, query, noteRef }.Count(s => !string.IsNullOrWhiteSpace(s));
        if (given != 1)
            throw new MindVaultException("Pass exactly one of: current-file, query, or note.");

        var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
        var names = ctx.ProjectDetect.QueryNamesFor(proj);
        var fb = ctx.Feedback.LoadAll();
        var warnings = new List<string>();
        var pool = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var archive = ctx.Config.DefaultArchiveFolder;

        bool Excluded(NoteSummary n) =>
            n.Id == proj.Id
            || n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase)
            || n.Path.StartsWith("09_Maps/", StringComparison.OrdinalIgnoreCase)
            || n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase)
            || n.Status is not null && (n.Status.Equals("archived", StringComparison.OrdinalIgnoreCase) ||
                                        n.Status.Equals("superseded", StringComparison.OrdinalIgnoreCase))
            || string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n.Type, "map", StringComparison.OrdinalIgnoreCase)
            || FeedbackService.For(n, fb).Hidden;

        void Add(NoteSummary? n, string reason, int score)
        {
            if (n is null || Excluded(n)) return;
            if (!pool.TryGetValue(n.Path, out var entry)) pool[n.Path] = entry = new Entry(n);
            entry.Add(reason, score);
        }

        string inputKind, input;
        if (!string.IsNullOrWhiteSpace(currentFile))
        {
            inputKind = "current-file";
            input = currentFile!.Trim();
            var fileName = Path.GetFileName(input.Replace('\\', '/'));
            var tokens = FileTokens(input);
            if (tokens.Count == 0)
                throw new MindVaultException($"Could not extract search tokens from '{input}'.");
            foreach (var hit in ctx.Search.Search(string.Join(" OR ", tokens), project: proj.Title, limit: 25))
            {
                if (hit.Scope == "global-fallback" && warnings.Count == 0)
                    warnings.Add("no in-project matches for the current file — results are vault-wide");
                Add(ctx.Db.FindByPath(hit.Path), $"matches the current file ({fileName})", 2);
            }
        }
        else if (!string.IsNullOrWhiteSpace(query))
        {
            inputKind = "query";
            input = query!.Trim();
            // OR-join descriptive queries (FTS is implicit-AND); same trick as context packs.
            var terms = SearchService.Tokenize(input);
            var fts = terms.Count > 1 ? string.Join(" OR ", terms) : input;
            foreach (var hit in ctx.Search.Search(fts, project: proj.Title, limit: 25))
            {
                if (hit.Scope == "global-fallback" && warnings.Count == 0)
                    warnings.Add("no in-project matches for the query — results are vault-wide");
                Add(ctx.Db.FindByPath(hit.Path), "matches the query", 2);
            }
        }
        else
        {
            inputKind = "note";
            input = noteRef!.Trim();
            var note = ctx.Resolver.Resolve(input);
            foreach (var r in ctx.Related.Get(note.Path, 20).Related)
                Add(ctx.Db.FindByPath(r.Path), r.Reason, 2);
        }

        // Deterministic boosts over the pooled seeds.
        foreach (var e in pool.Values)
        {
            if (e.Note.Project is { Length: > 0 } p && names.Contains(p, StringComparer.OrdinalIgnoreCase))
                e.Add("same project", 1);
            var status = e.Note.Status?.ToLowerInvariant();
            if (status is "active" or "accepted" or "open" or "blocked")
                e.Add($"status {status}", 1);
            var f = FeedbackService.For(e.Note, fb);
            if (f.Pinned) e.Add("pinned", 3);
            if (f.Score > 0) e.Add("marked useful", f.Score);
            else if (f.Score < 0) e.Add("negative feedback", f.Score);
        }

        var ranked = pool.Values
            .Where(e => e.Score > 0)
            .OrderByDescending(e => e.Score)
            .ThenBy(e => e.Note.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<WorkContextItem> Group(Func<string?, bool> typeMatch) =>
            ranked.Where(e => typeMatch(e.Note.Type?.ToLowerInvariant()))
                .Take(Math.Min(limit, 8))
                .Select(e => new WorkContextItem(e.Note.Title, e.Note.Path, e.Note.Type, e.Note.Status,
                    string.Join("; ", e.Reasons)))
                .ToList();

        return new WorkContextResult(
            proj.Title, inputKind, input,
            Group(t => t == "decision"),
            Group(t => t is "task" or "bug" or "feature"),
            Group(t => t == "risk"),
            Group(t => t == "mistake"),
            Group(t => t == "review"),
            Group(t => t == "memory"),
            ranked.Take(6).Select(e => new ContextRead(e.Note.Path, string.Join("; ", e.Reasons))).ToList(),
            warnings);
    }

    /// <summary>Search tokens for a source-file path: full stem, camelCase parts, parent folder parts.</summary>
    internal static List<string> FileTokens(string filePath)
    {
        var norm = filePath.Replace('\\', '/');
        var stem = Path.GetFileNameWithoutExtension(norm);
        var parent = Path.GetFileName(Path.GetDirectoryName(norm) ?? "") ?? "";
        var raw = new List<string>();
        foreach (var source in new[] { stem, parent })
            raw.AddRange(TokenPattern().Matches(source).Select(m => m.Value));
        if (stem.All(char.IsLetterOrDigit)) raw.Insert(0, stem);
        return raw.Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 2)
            .Distinct()
            .Take(8)
            .ToList();
    }

    [GeneratedRegex(@"[A-Z][a-z]+|[A-Z]{2,}(?![a-z])|[a-z]{3,}|\d{2,}")]
    private static partial Regex TokenPattern();
}
