namespace MindVault.Core;

/// <summary>
/// Deterministic note-placement rules used by `organize` and promotion: which folder each
/// note type belongs in, which locations already count as correctly placed, and why.
/// Deliberately boring — no taxonomy engine, no config, at most one shallow subfolder level.
///
/// Design decision (docs/ORGANISATION.md): per-project subfolders are OFF. The existing
/// vault convention keeps tasks flat in 01_Projects; predictable and shallow beats clever
/// and nested. Work items (task/bug/feature) live with projects; agent memory that is not
/// work (mistakes, risks, constraints, meetings) gets type-named subfolders under
/// 06_Agent_Memory so that folder stays navigable instead of becoming a graveyard.
///
/// This is organize-time intelligence only — the `outside-structure` validation contract
/// (<see cref="VaultStructure.ExpectedFolder"/>) is unchanged, so existing vaults gain no
/// new validation warnings from this policy.
/// </summary>
public static class PlacementPolicy
{
    public static string? PreferredFolder(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "project" => "01_Projects",
        "task" => "01_Projects",
        "bug" => "01_Projects",
        "feature" => "01_Projects",
        "decision" => "04_Decisions",
        "prompt" => "05_Prompts",
        "research" => "03_Resources",
        "architecture" => "03_Resources/Architecture",
        "memory" => "06_Agent_Memory",
        "meeting" => "06_Agent_Memory/Meetings",
        "mistake" => "06_Agent_Memory/Mistakes",
        "constraint" => "06_Agent_Memory/Constraints",
        "risk" => "06_Agent_Memory/Risks",
        "review" => "07_Reviews",
        "thought" => "00_Inbox",
        "map" => "09_Maps",
        _ => null,
    };

    /// <summary>
    /// Folder prefixes that already count as correctly placed for a type. Wider than the
    /// preferred folder where humans legitimately organise their own way (anything under
    /// 03_Resources for research/architecture, any 06_Agent_Memory subfolder for memory).
    /// </summary>
    public static IReadOnlyList<string> AcceptableRoots(string? type) => type?.Trim().ToLowerInvariant() switch
    {
        "project" or "task" or "bug" or "feature" => ["01_Projects"],
        "decision" => ["04_Decisions"],
        "prompt" => ["05_Prompts"],
        "research" or "architecture" => ["03_Resources"],
        "memory" => ["06_Agent_Memory"],
        "meeting" => ["06_Agent_Memory/Meetings"],
        "mistake" => ["06_Agent_Memory/Mistakes"],
        "constraint" => ["06_Agent_Memory/Constraints"],
        "risk" => ["06_Agent_Memory/Risks"],
        "review" => ["07_Reviews"],
        "thought" => ["00_Inbox", "06_Agent_Memory/Inbox"],
        "map" => ["09_Maps"],
        _ => [],
    };

    /// <summary>True when the note's current location is fine for its type (archive always is).</summary>
    public static bool IsAcceptablePath(string notePath, string? type, string archiveFolder)
    {
        if (notePath.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase)) return true;
        var roots = AcceptableRoots(type);
        if (roots.Count == 0) return true; // no opinion for unknown types — never propose a move
        return roots.Any(r => notePath.StartsWith(r + "/", StringComparison.OrdinalIgnoreCase));
    }
}
