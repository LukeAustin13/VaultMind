namespace MindVault.Mcp;

/// <summary>How the server was started — surfaced by the diagnostics tool.</summary>
public sealed record McpRuntimeInfo(string Transport);
