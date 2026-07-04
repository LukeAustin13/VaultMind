namespace MindVault.Core;

/// <summary>Small shared read-only queries used by the CLI, MCP tools and brain-ops rollup.</summary>
public static class BrainQueries
{
    /// <summary>Unpromoted drafts: thought/untyped notes in the inboxes, newest first.</summary>
    public static List<NoteSummary> Inbox(VaultContext ctx, string? project = null)
    {
        string[]? names = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project.Trim());
            names = ctx.ProjectDetect.QueryNamesFor(proj);
        }
        return ctx.Db.GetAllNotes()
            .Where(n => n.Path.StartsWith("00_Inbox/", StringComparison.OrdinalIgnoreCase) ||
                        n.Path.StartsWith("06_Agent_Memory/Inbox/", StringComparison.OrdinalIgnoreCase))
            .Where(n => n.Type is null || string.Equals(n.Type, "thought", StringComparison.OrdinalIgnoreCase))
            .Where(n => !string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase))
            .Where(n => names is null || n.Project is null ||
                        names.Contains(n.Project, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(n => n.Updated ?? n.Created ?? "", StringComparer.Ordinal)
            .ThenBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Mistake-ledger notes; active lessons only unless includeResolved.</summary>
    public static List<NoteSummary> Mistakes(VaultContext ctx, string? project = null,
        bool includeResolved = false)
    {
        string[]? names = null;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project.Trim());
            names = ctx.ProjectDetect.QueryNamesFor(proj);
        }
        var rows = includeResolved
            ? ctx.Db.Query(type: "mistake", projectNames: names, statusNot: "archived", limit: 200)
            : ctx.Db.Query(type: "mistake", projectNames: names, statusIn: ["active", "open"], limit: 200);
        return rows
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
