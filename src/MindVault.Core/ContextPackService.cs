using System.Text;

namespace MindVault.Core;

public sealed record ContextPack(
    string Project,
    string? Status,
    string? CurrentGoal,
    IReadOnlyList<string> NonNegotiables,
    IReadOnlyList<ContextItem> RelevantArchitecture,
    IReadOnlyList<ContextItem> RelevantDecisions,
    IReadOnlyList<ContextItem> ActiveTasks,
    IReadOnlyList<ContextItem> OpenRisks,
    IReadOnlyList<ContextItem> Constraints,
    IReadOnlyList<ContextRead> SuggestedNextReads,
    IReadOnlyList<string> DoNotForget,
    IReadOnlyList<string> Warnings,
    string? TaskFocus,
    IReadOnlyList<ContextItem> TaskRelevantNotes);

/// <summary>
/// A context pack is a generated, compact briefing assembled from existing vault notes —
/// never a new canonical store. When a task description is given, notes matching it are
/// surfaced first so the agent starts with what matters for this piece of work.
/// </summary>
public sealed class ContextPackService(VaultContext ctx)
{
    public ContextPack Get(string project, string? task = null, int limit = 8)
    {
        limit = Math.Clamp(limit, 1, 25);
        var context = ctx.Projects.Get(project, limit);

        var taskRelevant = new List<ContextItem>();
        var decisions = context.RecentDecisions.ToList();
        var architecture = context.RelevantArchitecture.ToList();
        if (!string.IsNullOrWhiteSpace(task))
        {
            // OR-join the task terms: any-term relevance, ranked — implicit FTS AND would
            // demand every word of the task description appear in one note.
            var terms = string.Join(" OR ", SearchService.Tokenize(task));
            if (terms.Length > 0)
            {
                var hits = ctx.Search.Search(terms, project: context.Project, limit: 5);
                taskRelevant = hits
                    .Where(h => !string.Equals(h.Path, context.ProjectNote.Path, StringComparison.OrdinalIgnoreCase))
                    .Select(h => new ContextItem(h.Title, h.Path, h.Status, null))
                    .ToList();
                var relevantPaths = new HashSet<string>(taskRelevant.Select(t => t.Path), StringComparer.OrdinalIgnoreCase);
                decisions = Reorder(decisions, relevantPaths);
                architecture = Reorder(architecture, relevantPaths);
            }
        }

        var doNotForget = new List<string>();
        doNotForget.AddRange(context.NonNegotiables.Take(5));
        doNotForget.AddRange(context.Constraints.Take(3).Select(c => $"Constraint: {c.Title}"));
        doNotForget.AddRange(context.OpenRisks.Take(2).Select(r => $"Open risk: {r.Title}"));

        return new ContextPack(
            context.Project,
            context.ProjectNote.Status,
            context.CurrentGoal,
            context.NonNegotiables,
            architecture,
            decisions,
            context.ActiveTasks.Concat(context.BlockedTasks).Take(limit).ToList(),
            context.OpenRisks,
            context.Constraints,
            context.RecommendedNextReads,
            doNotForget.Take(8).ToList(),
            context.Warnings,
            string.IsNullOrWhiteSpace(task) ? null : task.Trim(),
            taskRelevant);
    }

    public static string ToMarkdown(ContextPack pack)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Context pack: {pack.Project}{(pack.Status is null ? "" : $" ({pack.Status})")}");
        if (pack.TaskFocus is not null) sb.AppendLine($"\n**Task focus:** {pack.TaskFocus}");
        if (pack.CurrentGoal is not null) sb.AppendLine($"\n## Goal\n\n{pack.CurrentGoal}");
        AppendList(sb, "Non-negotiables", pack.NonNegotiables.Select(n => n));
        AppendItems(sb, "Task-relevant notes", pack.TaskRelevantNotes);
        AppendItems(sb, "Architecture", pack.RelevantArchitecture);
        AppendItems(sb, "Decisions in force", pack.RelevantDecisions);
        AppendItems(sb, "Active tasks", pack.ActiveTasks);
        AppendItems(sb, "Open risks", pack.OpenRisks);
        AppendItems(sb, "Constraints", pack.Constraints);
        if (pack.SuggestedNextReads.Count > 0)
        {
            sb.AppendLine("\n## Read next");
            foreach (var read in pack.SuggestedNextReads)
                sb.AppendLine($"- `{read.Path}` — {read.Reason}");
        }
        AppendList(sb, "Do not forget", pack.DoNotForget);
        AppendList(sb, "Warnings", pack.Warnings);
        return sb.ToString().TrimEnd() + "\n";
    }

    private static void AppendItems(StringBuilder sb, string heading, IReadOnlyList<ContextItem> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine($"\n## {heading}");
        foreach (var item in items)
            sb.AppendLine($"- {item.Title}{(item.Status is null ? "" : $" [{item.Status}]")} (`{item.Path}`)");
    }

    private static void AppendList(StringBuilder sb, string heading, IEnumerable<string> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;
        sb.AppendLine($"\n## {heading}");
        foreach (var item in list) sb.AppendLine($"- {item}");
    }

    private static List<ContextItem> Reorder(List<ContextItem> items, HashSet<string> preferredPaths) =>
        items.OrderByDescending(i => preferredPaths.Contains(i.Path)).ToList();
}
