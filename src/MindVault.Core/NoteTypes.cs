namespace MindVault.Core;

public static class NoteTypes
{
    public static readonly IReadOnlyList<string> Managed =
    [
        "project", "decision", "task", "bug", "feature", "architecture",
        "prompt", "research", "review", "meeting", "memory", "constraint", "risk",
    ];

    public static readonly IReadOnlyList<string> Statuses =
    [
        "active", "open", "draft", "accepted", "rejected", "superseded",
        "done", "archived", "blocked", "cancelled",
    ];

    public static readonly IReadOnlyList<string> RequiredFrontmatterKeys =
        ["type", "status", "created", "updated", "tags"];

    public static bool IsManaged(string? type) =>
        type is not null && Managed.Contains(type.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsValidStatus(string? status) =>
        status is not null && Statuses.Contains(status.Trim(), StringComparer.OrdinalIgnoreCase);
}
