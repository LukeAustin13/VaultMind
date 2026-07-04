using System.Text.Json;

namespace MindVault.Core;

public sealed record DecisionInfo(
    string Title, string Path, string? Status, string? Updated,
    IReadOnlyList<string> Supersedes, IReadOnlyList<string> SupersededBy, IReadOnlyList<string> Related);

public sealed record DecisionNode(string Path, string Title, string? Status);

public sealed record DecisionEdge(string From, string To, string Kind);

public sealed record DecisionGraphResult(IReadOnlyList<DecisionNode> Nodes, IReadOnlyList<DecisionEdge> Edges);

/// <summary>
/// Decision lifecycle queries over the flat frontmatter relations
/// (supersedes / superseded_by / related — lists of wiki links).
/// </summary>
public sealed class DecisionService(VaultContext ctx)
{
    private static readonly string[] InactiveStatuses = ["superseded", "rejected", "archived", "cancelled"];

    /// <summary>Decisions, most recent first. Superseded/rejected/archived/cancelled hidden unless includeAll.</summary>
    public List<DecisionInfo> List(string? project = null, bool includeAll = false)
    {
        ctx.Scanner.EnsureFresh();
        string[]? names = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var proj = ctx.Writer.FindProject(project);
            names = string.Equals(proj.Title, proj.Stem, StringComparison.OrdinalIgnoreCase)
                ? [proj.Title] : [proj.Title, proj.Stem];
        }

        var decisions = ctx.Db.Query(type: "decision", projectNames: names, limit: 200)
            .Where(d => !d.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .Where(d => includeAll || !InactiveStatuses.Contains(d.Status ?? "", StringComparer.OrdinalIgnoreCase))
            .ToList();

        var relations = LoadRelations();
        return decisions.Select(d => new DecisionInfo(
                d.Title, d.Path, d.Status, d.Updated,
                relations.GetValueOrDefault((d.Id, "supersedes"), []),
                relations.GetValueOrDefault((d.Id, "superseded_by"), []),
                relations.GetValueOrDefault((d.Id, "related"), [])))
            .ToList();
    }

    /// <summary>Nodes (all decisions incl. superseded) plus supersedes/related edges.</summary>
    public DecisionGraphResult Graph(string? project = null)
    {
        var decisions = List(project, includeAll: true);
        var byRef = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var d in decisions)
        {
            byRef[SlugHelper.NormalizeWiki(d.Title)] = d.Path;
            byRef[SlugHelper.NormalizeWiki(System.IO.Path.GetFileNameWithoutExtension(d.Path))] = d.Path;
        }

        var edges = new List<DecisionEdge>();
        foreach (var d in decisions)
        {
            foreach (var target in d.Supersedes)
                if (ResolveRef(byRef, target) is { } to)
                    edges.Add(new DecisionEdge(d.Path, to, "supersedes"));
            foreach (var target in d.SupersededBy)
                if (ResolveRef(byRef, target) is { } from)
                    edges.Add(new DecisionEdge(from, d.Path, "supersedes"));
            foreach (var target in d.Related)
                if (ResolveRef(byRef, target) is { } to)
                    edges.Add(new DecisionEdge(d.Path, to, "related"));
        }

        return new DecisionGraphResult(
            decisions.Select(d => new DecisionNode(d.Path, d.Title, d.Status)).ToList(),
            edges.DistinctBy(e => (e.From, e.To, e.Kind)).ToList());
    }

    private static string? ResolveRef(Dictionary<string, string> byRef, string rawTarget)
    {
        var inner = rawTarget.Trim();
        if (inner.StartsWith("[[") && inner.EndsWith("]]")) inner = inner[2..^2];
        inner = inner.Split('|')[0].Split('#')[0].Trim();
        return byRef.GetValueOrDefault(SlugHelper.NormalizeWiki(inner));
    }

    /// <summary>(noteId, key) → raw relation targets, parsed from the indexed frontmatter values.</summary>
    private Dictionary<(long, string), List<string>> LoadRelations()
    {
        var result = new Dictionary<(long, string), List<string>>();
        foreach (var key in new[] { "supersedes", "superseded_by", "related" })
        {
            foreach (var row in ctx.Db.GetFrontmatterValues(key))
                result[(row.NoteId, key)] = ParseRefList(row.Value);
        }
        return result;
    }

    private static List<string> ParseRefList(string stored)
    {
        if (stored.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(stored) ?? [];
            }
            catch (JsonException)
            {
                return [stored];
            }
        }
        return string.IsNullOrWhiteSpace(stored) ? [] : [stored];
    }
}
