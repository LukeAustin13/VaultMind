using MindVault.Core;
using MindVault.Mcp;

namespace MindVault.Tests.AgentWorkflowEvals;

/// <summary>
/// Agent-behaviour evals: the tool surface and skills must make good agent behaviour the
/// path of least resistance — compact outputs, refs not dumps, search-before-create,
/// no unsafe operations, honest handoffs. See docs/AGENT_EVALS.md.
/// </summary>
public sealed partial class AgentEvalTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] UnsafeNames =
        ["write_file", "delete_file", "run_shell", "raw_sql", "raw_filesystem_access"];

    // ---------- output bounds ----------

    [Fact]
    public void ContextPackStaysCompactEnoughForAgentUse()
    {
        using var tv = new TempVault();
        var pack = tv.Ctx.Packs.Get("Alpha", "harden the sqlite search");
        Assert.True(Json.Serialize(pack).Length < 6_000, "pack JSON must stay well under agent-context scale");
        Assert.True(ContextPackService.ToMarkdown(pack).Length < 5_000);
    }

    [Fact]
    public void ProjectContextCarriesRefsNotFullNoteBodies()
    {
        using var tv = new TempVault();
        var bigBlock = string.Join(" ", Enumerable.Repeat("longbodyfiller", 400)); // ~6 KB
        tv.Ctx.Writer.AppendToSection("Decision - Use SQLite", "Context", bigBlock, createSection: true);

        var context = Json.Serialize(tv.Ctx.Projects.Get("Alpha", detailLevel: "deep"));
        Assert.DoesNotContain(bigBlock, context);
        var pack = Json.Serialize(tv.Ctx.Packs.Get("Alpha"));
        Assert.DoesNotContain(bigBlock, pack);
    }

    [Fact]
    public void McpSearchOutputIsBounded()
    {
        using var tv = new TempVault();
        var tools = new MindVaultTools(tv.Ctx);
        var text = tools.Search("v1 OR sqlite OR docs OR alpha", limit: 10_000);
        Assert.True(text.Length < 30_000);
        Assert.DoesNotContain("\"count\":101", text); // limit clamps at 100
    }

    [Fact]
    public void McpReadNoteTruncatesHugeBodies()
    {
        using var tv = new TempVault();
        var body = string.Join(" ", Enumerable.Repeat("filler", 15_000)); // ~100 KB
        tv.Ctx.Writer.CreateNoteFile("03_Resources/Huge.md",
            $"---\ntype: research\nstatus: draft\ncreated: 2026-01-01\nupdated: 2026-01-01\ntags: [big]\n---\n\n# Huge\n\n{body}\n");

        var tools = new MindVaultTools(tv.Ctx);
        var text = tools.ReadNote("Huge");
        Assert.Contains("[truncated]", text);
        Assert.True(text.Length < 80_000);
    }

    [Fact]
    public void McpDiagnosticsLeakNoPathsOrSecrets()
    {
        using var tv = new TempVault();
        var tools = new MindVaultTools(tv.Ctx);
        foreach (var output in new[] { tools.Health(), tools.Diagnostics(), tools.Status() })
        {
            Assert.DoesNotContain(tv.Root, output);
            Assert.DoesNotContain(tv.Root.Replace('\\', '/'), output);
            Assert.DoesNotContain("MINDVAULT_MCP_AUTH_TOKEN", output);
        }
        Assert.Contains("\"transport\":\"unknown\"", tools.Diagnostics()); // constructed without runtime info
    }

    // ---------- unsafe-operation hard fail ----------

    [Fact]
    public void NoUnsafeToolNamesAnywhereInSkillsOrMcpSurface()
    {
        var files = Directory.GetFiles(Path.Combine(RepoRoot, "skills"), "SKILL.md", SearchOption.AllDirectories)
            .Append(Path.Combine(RepoRoot, "src", "MindVault.Mcp", "MindVaultTools.cs"))
            .ToList();
        Assert.True(files.Count >= 9);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var name in UnsafeNames)
                Assert.False(content.Contains(name, StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetFileName(file)} mentions unsafe operation '{name}'");
        }
    }

    // ---------- skill content contracts ----------

    private static IEnumerable<string> SkillFiles() =>
        Directory.GetFiles(Path.Combine(RepoRoot, "skills"), "SKILL.md", SearchOption.AllDirectories);

    [Fact]
    public void EverySkillHasTheFiveRequiredSections()
    {
        var required = new[]
        {
            "## Trigger conditions", "## Required workflow", "## Do not",
            "## Efficiency rules", "## Safety rules",
        };
        var files = SkillFiles().ToList();
        Assert.Equal(8, files.Count);
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            foreach (var section in required)
                Assert.True(content.Contains(section),
                    $"{Path.GetFileName(Path.GetDirectoryName(file))} is missing '{section}'");
        }
    }

    [Fact]
    public void CreatingSkillsRequireDraftChecksBeforeCreating()
    {
        foreach (var skill in new[] { "mindvault-decision-capture", "mindvault-task-sync", "mindvault-review-memory" })
        {
            var content = File.ReadAllText(Path.Combine(RepoRoot, "skills", skill, "SKILL.md"));
            Assert.Contains("mindvault_check_draft", content);
            Assert.Contains("before", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SkillsForbidVaultDumpingAndRawWritesAndShell()
    {
        foreach (var file in SkillFiles())
        {
            var content = File.ReadAllText(file);
            // Every skill pins usage to the safe MCP tools and explicitly rules out shell access.
            Assert.Contains("mindvault_*", content);
            Assert.Contains("shell", content, StringComparison.OrdinalIgnoreCase);
            // No skill may include executable shell instructions.
            foreach (var fence in new[] { "```bash", "```sh", "```powershell", "```bat", "```cmd" })
                Assert.DoesNotContain(fence, content, StringComparison.OrdinalIgnoreCase);
        }
        // The context-loading skills carry the explicit no-dump rule.
        foreach (var skill in new[] { "mindvault-project-context" })
        {
            var content = File.ReadAllText(Path.Combine(RepoRoot, "skills", skill, "SKILL.md"));
            Assert.Contains("whole vault", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------- durable-record quality ----------

    [Fact]
    public void SessionHandoffIsConciseAndStructured()
    {
        using var tv = new TempVault();
        var start = tv.Ctx.Sessions.Start("Alpha", "eval run");
        tv.Ctx.Sessions.End("Alpha", "hardened the search ranking", "dotnet test green (200)", "watch bm25 weights");

        var log = tv.ReadNote(start.LogNotePath);
        const string followUps = "- Follow-ups: watch bm25 weights";
        var summaryAt = log.IndexOf("hardened the search ranking", StringComparison.Ordinal);
        Assert.True(summaryAt > 0, "handoff summary must be in the log note");
        var blockStart = log.LastIndexOf("### ", summaryAt, StringComparison.Ordinal);
        var blockEnd = log.IndexOf(followUps, StringComparison.Ordinal) + followUps.Length;
        var handoff = log[blockStart..blockEnd];
        Assert.True(handoff.Length < 300, $"handoff must stay concise, was {handoff.Length} chars");
        Assert.Contains("- Tests: dotnet test green (200)", handoff);
    }

    [Fact]
    public void DecisionCaptureIncludesReversalConditions()
    {
        Assert.Contains("## Reversal Conditions", NoteTemplates.Decision("T", "Alpha", "Alpha", "2026-07-04"));
        var skill = File.ReadAllText(Path.Combine(RepoRoot, "skills", "mindvault-decision-capture", "SKILL.md"));
        Assert.Contains("Reversal Conditions", skill);
        using var tv = new TempVault();
        var created = tv.Ctx.Writer.CreateDecision("Alpha", "Reversal check");
        Assert.Contains("## Reversal Conditions", tv.ReadNote(created.Note.Path));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MindVault.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root not found.");
    }
}
