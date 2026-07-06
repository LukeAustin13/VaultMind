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
        "mindvault_capture_thought", "mindvault_promote_note", "mindvault_organize_vault",
        "mindvault_create_map", "mindvault_rebuild_map", "mindvault_list_maps",
        "mindvault_suggest_links", "mindvault_find_broken_links", "mindvault_find_orphans",
        "mindvault_audit_frontmatter", "mindvault_audit_aliases",
        "mindvault_build_context_capsule", "mindvault_get_work_context", "mindvault_recall",
        "mindvault_record_feedback", "mindvault_brain_ops", "mindvault_checkpoint_session",
        "mindvault_recent_sessions", "mindvault_list_inbox", "mindvault_add_mistake",
        "mindvault_list_mistakes", "mindvault_resolve_mistake",
        "mindvault_get_project_map", "mindvault_build_route_card", "mindvault_build_read_plan",
        "mindvault_token_audit", "mindvault_generate_summaries", "mindvault_organisation_score",
        "mindvault_build_graph", "mindvault_explain_relationships",
        "mindvault_find_low_value_notes", "mindvault_compile_brain",
    ];

    private static readonly string[] CoreTools =
    [
        "mindvault_detect_project", "mindvault_status", "mindvault_start_session",
        "mindvault_checkpoint_session", "mindvault_end_session", "mindvault_recall",
        "mindvault_search", "mindvault_read_note", "mindvault_get_work_context",
        "mindvault_build_route_card", "mindvault_build_context_capsule", "mindvault_capture_thought",
        "mindvault_check_draft", "mindvault_create_decision", "mindvault_create_task",
        "mindvault_add_mistake", "mindvault_append_to_note", "mindvault_update_frontmatter",
        "mindvault_link_notes", "mindvault_record_feedback",
    ];

    private StdioClientTransport CreateTransport(string? toolProfile = null) =>
        new(new StdioClientTransportOptions
        {
            Name = "mindvault-under-test",
            Command = "dotnet",
            Arguments = [Path.Combine(AppContext.BaseDirectory, "MindVault.Mcp.dll"), "--vault", _tv.Root],
            EnvironmentVariables = toolProfile is null
                ? null
                : new Dictionary<string, string?> { ["MINDVAULT_TOOL_PROFILE"] = toolProfile },
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
    public async Task CoreProfileExposesExactlyTheTwentyCoreTools()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(
            CreateTransport(toolProfile: "core"), cancellationToken: cts.Token);

        var tools = await client.ListToolsAsync(cancellationToken: cts.Token);
        var names = tools.Select(t => t.Name).OrderBy(n => n).ToArray();
        Assert.Equal(CoreTools.OrderBy(n => n).ToArray(), names);
    }

    [Fact]
    public async Task CoreProfileToolsStillExecute()
    {
        // The core tools are registered per-request; prove one round-trips end to end.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(
            CreateTransport(toolProfile: "core"), cancellationToken: cts.Token);

        var status = await client.CallToolAsync("mindvault_status",
            new Dictionary<string, object?>(), cancellationToken: cts.Token);
        var statusText = Assert.IsType<TextContentBlock>(Assert.Single(status.Content)).Text;
        Assert.Contains("vaultName", statusText);
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

    [Fact]
    public async Task CapsuleFormatReturnsExactlyOneShape()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: cts.Token);

        var json = await client.CallToolAsync("mindvault_build_context_capsule",
            new Dictionary<string, object?> { ["project"] = "Alpha" }, cancellationToken: cts.Token);
        var jsonText = Assert.IsType<TextContentBlock>(Assert.Single(json.Content)).Text;
        Assert.Contains("\"capsule\"", jsonText);
        Assert.DoesNotContain("\"markdown\"", jsonText);

        var md = await client.CallToolAsync("mindvault_build_context_capsule",
            new Dictionary<string, object?> { ["project"] = "Alpha", ["format"] = "markdown" },
            cancellationToken: cts.Token);
        var mdText = Assert.IsType<TextContentBlock>(Assert.Single(md.Content)).Text;
        Assert.Contains("\"markdown\"", mdText);
        Assert.DoesNotContain("\"capsule\"", mdText);
    }

    [Fact]
    public async Task CapsuleOmitsSourcePathsByDefault()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var client = await McpClient.CreateAsync(CreateTransport(), cancellationToken: cts.Token);

        var defaultCall = await client.CallToolAsync("mindvault_build_context_capsule",
            new Dictionary<string, object?> { ["project"] = "Alpha" }, cancellationToken: cts.Token);
        var defaultText = Assert.IsType<TextContentBlock>(Assert.Single(defaultCall.Content)).Text;
        Assert.Contains("\"sourcePaths\":[]", defaultText.Replace(" ", ""));

        var withSources = await client.CallToolAsync("mindvault_build_context_capsule",
            new Dictionary<string, object?> { ["project"] = "Alpha", ["includeSources"] = true },
            cancellationToken: cts.Token);
        var withText = Assert.IsType<TextContentBlock>(Assert.Single(withSources.Content)).Text;
        Assert.Contains("01_Projects/Alpha.md", withText);
    }

    public void Dispose() => _tv.Dispose();
}
