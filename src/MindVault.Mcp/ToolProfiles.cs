using System.ComponentModel;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace MindVault.Mcp;

/// <summary>
/// Tool-profile registration. <see cref="MindVaultTools"/> stays the single source of truth
/// for all 55 [McpServerTool] declarations. The "full" profile reflects the whole class; the
/// "core" profile registers only the curated <see cref="CoreToolNames"/> subset — same method
/// bodies, same schemas, just fewer tools advertised to keep per-session token cost down.
/// </summary>
public static class ToolProfiles
{
    /// <summary>
    /// The token-lean core surface: the tools an agent needs for the orient → work → hand-off
    /// loop. Subset of the full 55; guarded by a test against MindVaultVersion.McpCoreToolCount.
    /// </summary>
    public static readonly IReadOnlyList<string> CoreToolNames =
    [
        "mindvault_detect_project", "mindvault_status", "mindvault_start_session",
        "mindvault_checkpoint_session", "mindvault_end_session", "mindvault_recall",
        "mindvault_search", "mindvault_read_note", "mindvault_get_work_context",
        "mindvault_build_route_card", "mindvault_build_context_capsule", "mindvault_capture_thought",
        "mindvault_check_draft", "mindvault_create_decision", "mindvault_create_task",
        "mindvault_add_mistake", "mindvault_append_to_note", "mindvault_update_frontmatter",
        "mindvault_link_notes", "mindvault_record_feedback",
    ];

    /// <summary>
    /// Builds the McpServerTool instances for the core profile by reflecting the [McpServerTool]
    /// methods on <see cref="MindVaultTools"/> and keeping only the core names. The target is
    /// resolved per request from the request's service provider, exactly like WithTools&lt;T&gt;.
    /// </summary>
    public static IReadOnlyList<McpServerTool> BuildCoreTools()
    {
        var wanted = new HashSet<string>(CoreToolNames, StringComparer.Ordinal);
        var tools = new List<McpServerTool>();
        foreach (var method in typeof(MindVaultTools).GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            var attr = method.GetCustomAttribute<McpServerToolAttribute>();
            if (attr?.Name is not { } name || !wanted.Contains(name)) continue;

            tools.Add(McpServerTool.Create(
                method,
                static request => ActivatorUtilities.CreateInstance<MindVaultTools>(request.Services!),
                new McpServerToolCreateOptions
                {
                    Name = name,
                    Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description,
                }));
        }

        if (tools.Count != wanted.Count)
        {
            var found = tools.Select(t => t.ProtocolTool.Name).ToHashSet(StringComparer.Ordinal);
            var missing = wanted.Except(found);
            throw new InvalidOperationException(
                "Core tool profile could not resolve every declared tool. Missing: " +
                string.Join(", ", missing));
        }
        return tools;
    }
}
