using System.Text.RegularExpressions;
using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Cross-cutting guarantees: atomic writes, safe skills, Docker files intact.</summary>
public sealed partial class HardeningGuardTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    /// <summary>The complete safe MCP tool surface; skills may reference these and nothing else.</summary>
    private static readonly HashSet<string> SafeTools = new(StringComparer.Ordinal)
    {
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
    };

    [Fact]
    public void BrainOpsToolCountConstantMatchesTheRealSurface() =>
        Assert.Equal(MindVaultVersion.McpToolCount, SafeTools.Count);

    [Fact]
    public void MutationsLeaveNoTempFilesBehind()
    {
        using var tv = new TempVault();
        tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "atomic check");
        tv.Ctx.Writer.UpdateFrontmatter("Task - Ship v1", "status", "active");
        tv.Ctx.Writer.CreateProject("Atomic");
        Assert.Empty(Directory.GetFiles(tv.Root, "*.mindvault-tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void ValidationProbesDoNotLeaveFilesBehind()
    {
        using var tv = new TempVault();
        tv.Ctx.Validator.Validate();
        Assert.Empty(Directory.GetFiles(tv.Root, ".mindvault-probe-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void SkillsReferenceOnlySafeMcpTools()
    {
        var skillFiles = Directory.GetFiles(Path.Combine(RepoRoot, "skills"), "SKILL.md", SearchOption.AllDirectories);
        Assert.True(skillFiles.Length >= 6, "expected the skills pack to be present");
        foreach (var file in skillFiles)
        {
            var content = File.ReadAllText(file);
            foreach (Match match in ToolNamePattern().Matches(content))
                Assert.True(SafeTools.Contains(match.Value),
                    $"{Path.GetFileName(Path.GetDirectoryName(file))} references unknown tool '{match.Value}'");
            foreach (var forbidden in new[] { "write_file", "delete_file", "run_shell", "raw_sql", "raw_filesystem_access" })
                Assert.DoesNotContain(forbidden, content);
        }
    }

    [Fact]
    public void DockerFilesExistAndStaySane()
    {
        var dockerfile = File.ReadAllText(Path.Combine(RepoRoot, "Dockerfile"));
        Assert.Contains("mcr.microsoft.com/dotnet/sdk", dockerfile);
        Assert.Contains("mcr.microsoft.com/dotnet/aspnet", dockerfile);
        Assert.Contains("ENTRYPOINT", dockerfile);

        var compose = File.ReadAllText(Path.Combine(RepoRoot, "docker-compose.example.yml"));
        Assert.Contains("127.0.0.1:7777:7777", compose); // safe default binding
        Assert.Contains("MINDVAULT_VAULT_PATH: /vault", compose);
        // No ACTIVE bare all-interfaces binding (comments warning against it are fine).
        var activeLines = compose.Split('\n').Where(l => !l.TrimStart().StartsWith('#'));
        Assert.DoesNotContain(activeLines, l => l.Contains("\"7777:7777\""));

        Assert.True(File.Exists(Path.Combine(RepoRoot, ".dockerignore")));
    }

    [Fact]
    public void McpToolCountMatchesTheDocumentedSurface()
    {
        var tools = File.ReadAllText(Path.Combine(RepoRoot, "src", "MindVault.Mcp", "MindVaultTools.cs"));
        var declared = Regex.Matches(tools, "McpServerTool\\(Name = \"(mindvault_[a-z_]+)\"")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(SafeTools, declared);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MindVault.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found from test base directory.");
    }

    // Lookbehind excludes the `mcp__mindvault__mindvault_*` client-prefix explanation text.
    [GeneratedRegex(@"(?<!\w)mindvault_[a-z_]*[a-z]")]
    private static partial Regex ToolNamePattern();
}
