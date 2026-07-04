namespace MindVault.Core;

/// <summary>
/// Single source of truth for the product version. Bump here (and in CHANGELOG.md) per the
/// release checklist; the CLI (`--version`), MCP server info and diagnostics all read this.
/// </summary>
public static class MindVaultVersion
{
    public const string Current = "0.3.0";
}
