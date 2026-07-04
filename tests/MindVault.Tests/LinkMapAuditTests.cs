using MindVault.Cli;
using MindVault.Core;
using MindVault.Mcp;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MindVault.Tests;

/// <summary>
/// Link intelligence, map notes and audit evals: suggestions carry reasons and skip archived
/// notes, broken links and orphans are found, map rebuilds never destroy human text, audits
/// report alias collisions and nested YAML, and link apply never duplicates.
/// </summary>
public sealed class LinkMapAuditTests
{
    private static TempVault Vault() => new(fixture: "OrganisationVault");

    private static (int Code, string Stdout) RunCli(TempVault tv, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(args.Concat(["--vault", tv.Root]).ToArray(), stdout, stderr, _ => null, tv.Root);
        return (code, stdout.ToString());
    }

    // ---------- link suggestions ----------

    [Fact]
    public void SuggestionsIncludeDecisionToTaskRelationshipWithReason()
    {
        using var tv = Vault();
        var suggestions = tv.Ctx.LinkIntel.SuggestForNote("Random SQLite Decision");

        var task = Assert.Single(suggestions,
            s => s.ToPath == "01_Projects/Task - Add SQLite index tests.md");
        Assert.Contains("decision-to-task relationship", task.Reason);
        Assert.Contains(suggestions, s => s.ToPath == "06_Agent_Memory/Risk - Sync conflicts.md");
    }

    [Fact]
    public void SuggestionsExcludeArchivedNotesAndAlreadyLinkedPairs()
    {
        using var tv = Vault();
        var suggestions = tv.Ctx.LinkIntel.SuggestForNote("Random SQLite Decision");

        Assert.DoesNotContain(suggestions, s => s.ToPath.StartsWith("99_Archive/"));
        Assert.DoesNotContain(suggestions, s => s.To.Contains("Old SQLite cleanup"));
        // The decision already links to the hub — never re-suggested.
        Assert.DoesNotContain(suggestions, s => s.ToPath == "01_Projects/OrgProj.md");
    }

    [Fact]
    public void LinkApplyAvoidsDuplicateLinks()
    {
        using var tv = Vault();
        var first = tv.Ctx.Writer.LinkNotes("Random SQLite Decision", "Task - Add SQLite index tests");
        Assert.True(first.Changed);
        var second = tv.Ctx.Writer.LinkNotes("Random SQLite Decision", "Task - Add SQLite index tests");
        Assert.False(second.Changed);

        var text = tv.ReadNote("00_Inbox/Random SQLite Decision.md");
        Assert.Single(Regex.Matches(text, Regex.Escape("[[Task - Add SQLite index tests]]")));
    }

    [Fact]
    public void LinksCliSubcommandsRoundTrip()
    {
        using var tv = Vault();
        var (code, stdout) = RunCli(tv, "links", "suggest", "--note", "Random SQLite Decision", "--json");
        Assert.Equal(0, code);
        using (var doc = JsonDocument.Parse(stdout))
            Assert.True(doc.RootElement.GetProperty("count").GetInt32() >= 1);

        var (applyCode, applyOut) = RunCli(tv, "links", "apply",
            "--note", "Random SQLite Decision", "--to", "Task - Add SQLite index tests", "--json");
        Assert.Equal(0, applyCode);
        using (var doc = JsonDocument.Parse(applyOut))
            Assert.True(doc.RootElement.GetProperty("changed").GetBoolean());
    }

    // ---------- broken links + orphans ----------

    [Fact]
    public void BrokenLinkDetectorFindsMissingTargets()
    {
        using var tv = Vault();
        var (rows, truncated) = tv.Ctx.LinkIntel.BrokenLinks();
        Assert.False(truncated);
        Assert.Contains(rows,
            r => r.FromPath == "03_Resources/Broken research.md" && r.Target == "Nonexistent Target");
    }

    [Fact]
    public void OrphanDetectorFindsUnlinkedManagedNotesButNotThoughts()
    {
        using var tv = Vault();
        var (rows, _) = tv.Ctx.LinkIntel.Orphans();
        Assert.Contains(rows, r => r.Path == "06_Agent_Memory/Orphan memory.md");
        // Raw thoughts are expected to be unlinked — not orphans.
        Assert.DoesNotContain(rows, r => r.Path == "00_Inbox/Great idea.md");
        // Linked notes are not orphans.
        Assert.DoesNotContain(rows, r => r.Path == "01_Projects/Task - Add SQLite index tests.md");
        Assert.DoesNotContain(rows, r => r.Path == "01_Projects/OrgProj.md");
    }

    // ---------- maps ----------

    [Fact]
    public void MapRebuildRefreshesGeneratedBlockAndPreservesHumanText()
    {
        using var tv = Vault();
        var result = tv.Ctx.Maps.Rebuild("OrgProj");
        Assert.Empty(result.Warnings);
        Assert.NotNull(result.SnapshotPath);

        var text = tv.ReadNote("09_Maps/OrgProj Map.md");
        Assert.Contains("HUMAN-LINE-KEEP-ME", text);
        Assert.Contains("Keep me too.", text);
        Assert.DoesNotContain("stale generated content", text);
        Assert.Contains("[[Task - Add SQLite index tests]]", text);
        Assert.Contains("[[Random SQLite Decision]]", text);
        Assert.Contains("## Last Rebuilt", text);

        // Rebuilding again must not multiply marker blocks or lose the human text.
        tv.Ctx.Maps.Rebuild("OrgProj");
        text = tv.ReadNote("09_Maps/OrgProj Map.md");
        Assert.Single(Regex.Matches(text, Regex.Escape(MapService.MarkerStart)));
        Assert.Contains("HUMAN-LINE-KEEP-ME", text);
    }

    [Fact]
    public void MapCreateWorksOncePerProjectAndListsIt()
    {
        using var tv = Vault();
        var created = tv.Ctx.Maps.Create("AliasTwinA");
        Assert.Equal("09_Maps/AliasTwinA Map.md", created.Path);
        Assert.Throws<MindVaultException>(() => tv.Ctx.Maps.Create("AliasTwinA"));

        var maps = tv.Ctx.Maps.List();
        Assert.Contains(maps, m => m.Path == "09_Maps/AliasTwinA Map.md");
        Assert.Contains(maps, m => m.Path == "09_Maps/OrgProj Map.md");

        var (code, stdout) = RunCli(tv, "map", "list", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("count").GetInt32() >= 2);
    }

    // ---------- audits ----------

    [Fact]
    public void AliasAuditFindsCrossProjectCollisions()
    {
        using var tv = Vault();
        var report = tv.Ctx.Audits.AuditAliases();
        var collision = Assert.Single(report.Findings, f => f.Code == "alias-collision");
        Assert.Contains("AliasTwinA", collision.Issue);
        Assert.Contains("AliasTwinB", collision.Issue);
        Assert.Equal("critical", collision.Severity);
    }

    [Fact]
    public void FrontmatterAuditReportsNestedYamlAndUnresolvableProjects()
    {
        using var tv = Vault();
        var report = tv.Ctx.Audits.AuditFrontmatter();
        Assert.Contains(report.Findings,
            f => f.Code == "nested-yaml" && f.Path == "03_Resources/Nested config.md");
        Assert.Contains(report.Findings,
            f => f.Code == "project-unresolved" && f.Path == "00_Inbox/Misfiled ghost task.md");

        var (code, stdout) = RunCli(tv, "frontmatter", "audit", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("criticals").GetInt32() >= 2);
    }

    // ---------- MCP surface behaviour ----------

    [Fact]
    public void McpOrganizeIsDryRunByDefaultAndCaptureThoughtUsesAgentInbox()
    {
        using var tv = Vault();
        var tools = new MindVaultTools(tv.Ctx);

        using (var doc = JsonDocument.Parse(tools.OrganizeVault()))
        {
            Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
        }
        Assert.True(File.Exists(tv.Abs("00_Inbox/Random SQLite Decision.md")),
            "MCP organize without apply must not move notes");

        using (var doc = JsonDocument.Parse(tools.CaptureThought("Agent hunch", "might matter")))
        {
            Assert.Equal("06_Agent_Memory/Inbox/Agent hunch.md",
                doc.RootElement.GetProperty("path").GetString());
        }
        Assert.Contains("type: thought", tv.ReadNote("06_Agent_Memory/Inbox/Agent hunch.md"));
    }
}
