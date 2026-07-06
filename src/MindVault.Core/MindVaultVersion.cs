namespace MindVault.Core;

/// <summary>
/// Single source of truth for the product version. Bump here (and in CHANGELOG.md) per the
/// release checklist; the CLI (`--version`), MCP server info and diagnostics all read this.
/// </summary>
public static class MindVaultVersion
{
    public const string Current = "0.8.0";

    /// <summary>
    /// The MCP tool surface size, reported by brain-ops. A guard test asserts this equals
    /// the actual declared tool count, so it cannot silently drift.
    /// </summary>
    public const int McpToolCount = 55;

    /// <summary>
    /// The size of the token-lean "core" tool profile (MINDVAULT_TOOL_PROFILE=core). A guard
    /// test asserts this equals the curated core set, which is a subset of the full surface.
    /// </summary>
    public const int McpCoreToolCount = 20;
}
