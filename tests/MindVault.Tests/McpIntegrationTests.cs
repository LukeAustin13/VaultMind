using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MindVault.Tests;

/// <summary>
/// End-to-end test: launches the real MCP server over stdio (the MindVault.Mcp.dll copied
/// into the test output by the project reference) and talks to it with the official client.
/// </summary>
public sealed class McpIntegrationTests : IDisposable
{
    private readonly TempVault _tv = new();

    private static readonly string[] ExpectedTools =
    [
        "mindvault_status", "mindvault_search", "mindvault_read_note", "mindvault_list_notes",
        "mindvault_create_project", "mindvault_create_decision", "mindvault_create_task",
        "mindvault_append_to_note", "mindvault_update_frontmatter", "mindvault_link_notes",
        "mindvault_archive_note", "mindvault_validate_vault", "mindvault_get_project_context",
        "mindvault_rebuild_index", "mindvault_get_context_pack", "mindvault_check_draft",
        "mindvault_supersede_decision", "mindvault_start_session", "mindvault_end_session",
        "mindvault_health", "mindvault_diagnostics",
        "mindvault_detect_project", "mindvault_find_related",
    ];

    private StdioClientTransport CreateTransport() => new(new StdioClientTransportOptions
    {
        Name = "mindvault-under-test",
        Command = "dotnet",
        Arguments = [Path.Combine(AppContext.BaseDirectory, "MindVault.Mcp.dll"), "--vault", _tv.Root],
    });

    [Fact]
    public async Task ServerExposesExactlyTheSafeToolSurface()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        var names = tools.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedTools.OrderBy(n => n).ToArray(), names);
    }

    [Fact]
    public async Task ToolCallsRoundTripThroughTheRealServer()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: cts.Token);

        var status = await client.CallToolAsync("mindvault_status",
            new Dictionary<string, object?>(), cancellationToken: cts.Token);
        var statusText = Assert.IsType<TextContentBlock>(Assert.Single(status.Content)).Text;
        Assert.Contains("vaultName", statusText);
        Assert.Contains("indexExists", statusText);
        Assert.DoesNotContain("vaultPath", statusText);

        var context = await client.CallToolAsync("mindvault_get_project_context",
            new Dictionary<string, object?> { ["project"] = "Alpha" }, cancellationToken: cts.Token);
        var contextText = Assert.IsType<TextContentBlock>(Assert.Single(context.Content)).Text;
        Assert.Contains("01_Projects/Alpha.md", contextText);
        Assert.Contains("Task - Ship v1", contextText);
    }

    public void Dispose() => _tv.Dispose();
}
