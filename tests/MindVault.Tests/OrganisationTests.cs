using MindVault.Cli;
using MindVault.Core;
using System.Text.Json;

namespace MindVault.Tests;

/// <summary>
/// Organisation evals: organize proposes correct moves with reasons, dry-run never mutates,
/// apply is snapshot-first, uncertainty lands in needs-review, and promotion turns thoughts
/// into durable memory without losing a byte of content.
/// </summary>
public sealed class OrganisationTests
{
    private static TempVault Vault() => new(fixture: "OrganisationVault");

    private static (int Code, string Stdout) RunCli(TempVault tv, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(args.Concat(["--vault", tv.Root]).ToArray(), stdout, stderr, _ => null, tv.Root);
        return (code, stdout.ToString());
    }

    // ---------- organize ----------

    [Fact]
    public void OrganizeDryRunProposesCorrectMovesWithReasons()
    {
        using var tv = Vault();
        var report = tv.Ctx.Organizer.Plan();

        Assert.True(report.DryRun);
        var decision = Assert.Single(report.Proposals,
            p => p.CurrentPath == "00_Inbox/Random SQLite Decision.md");
        Assert.Equal("04_Decisions/Decision - Use SQLite FTS5.md", decision.ProposedPath);
        Assert.Contains("type=decision", decision.Reason);
        Assert.Contains("project=OrgProj", decision.Reason);
        Assert.Equal("high", decision.Confidence);

        var risk = Assert.Single(report.Proposals,
            p => p.CurrentPath == "06_Agent_Memory/Risk - Sync conflicts.md");
        Assert.Equal("06_Agent_Memory/Risks/Risk - Sync conflicts.md", risk.ProposedPath);
    }

    [Fact]
    public void OrganizeDryRunDoesNotMutateAnything()
    {
        using var tv = Vault();
        var before = File.ReadAllBytes(tv.Abs("00_Inbox/Random SQLite Decision.md"));
        var filesBefore = Directory.GetFiles(tv.Root, "*.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".mindvault")).OrderBy(f => f).ToArray();
        var snapshotsBefore = Directory.Exists(tv.Ctx.SnapshotDir)
            ? Directory.GetFiles(tv.Ctx.SnapshotDir, "*", SearchOption.AllDirectories).Length
            : 0;

        tv.Ctx.Organizer.Plan();

        var filesAfter = Directory.GetFiles(tv.Root, "*.md", SearchOption.AllDirectories)
            .Where(f => !f.Contains(".mindvault")).OrderBy(f => f).ToArray();
        Assert.Equal(filesBefore, filesAfter);
        Assert.Equal(before, File.ReadAllBytes(tv.Abs("00_Inbox/Random SQLite Decision.md")));
        var snapshotsAfter = Directory.Exists(tv.Ctx.SnapshotDir)
            ? Directory.GetFiles(tv.Ctx.SnapshotDir, "*", SearchOption.AllDirectories).Length
            : 0;
        Assert.Equal(snapshotsBefore, snapshotsAfter);
    }

    [Fact]
    public void OrganizeApplySnapshotsAndMovesSafeNotes()
    {
        using var tv = Vault();
        var originalBytes = File.ReadAllBytes(tv.Abs("00_Inbox/Random SQLite Decision.md"));
        var report = tv.Ctx.Organizer.Apply();

        Assert.False(report.DryRun);
        var moved = Assert.Single(report.Applied,
            m => m.FromPath == "00_Inbox/Random SQLite Decision.md");
        Assert.Equal("04_Decisions/Decision - Use SQLite FTS5.md", moved.ToPath);
        Assert.True(File.Exists(moved.SnapshotPath), "apply must snapshot before moving");
        Assert.False(File.Exists(tv.Abs("00_Inbox/Random SQLite Decision.md")));
        Assert.True(File.Exists(tv.Abs(moved.ToPath)));
        // A move never rewrites content.
        Assert.Equal(originalBytes, File.ReadAllBytes(tv.Abs(moved.ToPath)));
        Assert.NotNull(tv.Ctx.Db.FindByPath(moved.ToPath));
        Assert.Null(tv.Ctx.Db.FindByPath("00_Inbox/Random SQLite Decision.md"));
    }

    [Fact]
    public void AmbiguousOrUntypedNotesGoToNeedsReviewAndAreNeverMoved()
    {
        using var tv = Vault();
        var report = tv.Ctx.Organizer.Apply();

        Assert.Contains(report.NeedsReview, r => r.Path == "04_Decisions/Unlabeled.md");
        Assert.Contains(report.NeedsReview,
            r => r.Path == "00_Inbox/Misfiled ghost task.md" && r.Reason.Contains("GhostProj"));
        Assert.Contains(report.NeedsReview, r => r.Path == "03_Resources/Nested config.md");
        Assert.DoesNotContain(report.Proposals, p => p.CurrentPath == "04_Decisions/Unlabeled.md");
        Assert.DoesNotContain(report.Proposals, p => p.CurrentPath == "00_Inbox/Misfiled ghost task.md");
        Assert.True(File.Exists(tv.Abs("04_Decisions/Unlabeled.md")));
        Assert.True(File.Exists(tv.Abs("00_Inbox/Misfiled ghost task.md")));
    }

    [Fact]
    public void OrganizeNeverTouchesArchivedNotesOrCreatesDeepFolders()
    {
        using var tv = Vault();
        var report = tv.Ctx.Organizer.Apply();

        Assert.DoesNotContain(report.Proposals, p => p.CurrentPath.StartsWith("99_Archive/"));
        Assert.True(File.Exists(tv.Abs("99_Archive/Task - Old SQLite cleanup.md")));
        // Shallow and predictable: at most folder/subfolder/file.
        foreach (var m in report.Applied)
            Assert.True(m.ToPath.Split('/').Length <= 3, $"too deep: {m.ToPath}");
        foreach (var p in report.Proposals)
            Assert.True(p.ProposedPath.Split('/').Length <= 3, $"too deep: {p.ProposedPath}");
    }

    [Fact]
    public void OrganizeCliJsonRoundTrips()
    {
        using var tv = Vault();
        var (code, stdout) = RunCli(tv, "organize", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("dryRun").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("proposals").GetArrayLength() >= 2);
        Assert.True(doc.RootElement.GetProperty("needsReview").GetArrayLength() >= 3);
        // Dry-run through the CLI must not move anything either.
        Assert.True(File.Exists(tv.Abs("00_Inbox/Random SQLite Decision.md")));
    }

    // ---------- capture + promote ----------

    [Fact]
    public void CaptureThoughtLandsInTheInboxWithValidFrontmatter()
    {
        using var tv = Vault();
        var (code, _) = RunCli(tv, "create", "thought", "Quick capture", "--content", "raw text here");
        Assert.Equal(0, code);
        var text = tv.ReadNote("00_Inbox/Quick capture.md");
        Assert.Contains("type: thought", text);
        Assert.Contains("status: draft", text);
        Assert.Contains("raw text here", text);
    }

    [Fact]
    public void PromoteToDecisionRequiresAProject()
    {
        using var tv = Vault();
        var ex = Assert.Throws<MindVaultException>(
            () => tv.Ctx.Writer.PromoteNote("Great idea", "decision"));
        Assert.Contains("requires a project", ex.Message);
        // Nothing changed.
        Assert.Contains("type: thought", tv.ReadNote("00_Inbox/Great idea.md"));
    }

    [Fact]
    public void PromoteToDecisionSetsFrontmatterLinksProjectAndMovesTheNote()
    {
        using var tv = Vault();
        // Resolving through the alias proves promotion shares project detection.
        var r = tv.Ctx.Writer.PromoteNote("Great idea", "decision", project: "op");

        Assert.Equal("00_Inbox/Great idea.md", r.FromPath);
        Assert.Equal("04_Decisions/Great idea.md", r.ToPath);
        Assert.Equal("decision", r.Type);
        Assert.Equal("accepted", r.Status);
        Assert.True(File.Exists(r.SnapshotPath));

        var text = tv.ReadNote(r.ToPath);
        Assert.Contains("type: decision", text);
        Assert.Contains("status: accepted", text);
        Assert.Contains("project: OrgProj", text);
        Assert.Contains("[[OrgProj]]", text);
        Assert.Contains("# Decision: Adopt WAL checkpoints", text);
        Assert.Contains(r.Suggestions, s => s.Contains("Reasoning"));
    }

    [Fact]
    public void PromoteToMemoryPreservesContentByteForByte()
    {
        using var tv = Vault();
        var r = tv.Ctx.Writer.PromoteNote("Great idea", "memory");

        Assert.Equal("06_Agent_Memory/Great idea.md", r.ToPath);
        var text = tv.ReadNote(r.ToPath).Replace("\r\n", "\n");
        Assert.Contains("type: memory", text);
        Assert.Contains("# Memory: Adopt WAL checkpoints", text);
        Assert.Contains(
            "## Thought\n\nUNIQUE-CONTENT-LINE-42 must survive promotion byte for byte.\n\n## Why It Might Matter\n\n## Promote When",
            text);
    }

    [Fact]
    public void PromoteDoesNotFlagTheNoteItselfAsADuplicate()
    {
        using var tv = Vault();
        // capture names the file after the title, so stem == promoted title — the exact
        // self-collision the duplicate gate must ignore.
        tv.Ctx.Writer.CaptureThought("Try WAL checkpoints", "smoke regression");
        var r = tv.Ctx.Writer.PromoteNote("Try WAL checkpoints", "memory");
        Assert.Equal("06_Agent_Memory/Try WAL checkpoints.md", r.ToPath);
        Assert.Contains("# Memory: Try WAL checkpoints", tv.ReadNote(r.ToPath));
    }

    [Fact]
    public void PromoteRefusesDurableNotesAndArchivedNotes()
    {
        using var tv = Vault();
        var durable = Assert.Throws<MindVaultException>(
            () => tv.Ctx.Writer.PromoteNote("Task - Add SQLite index tests", "memory"));
        Assert.Contains("already a durable", durable.Message);

        var archived = Assert.Throws<MindVaultException>(
            () => tv.Ctx.Writer.PromoteNote("Task - Old SQLite cleanup", "memory"));
        Assert.Contains("Archived notes cannot be promoted", archived.Message);
    }

    [Fact]
    public void PromoteCliJsonRoundTrips()
    {
        using var tv = Vault();
        var (code, stdout) = RunCli(tv, "promote", "Great idea", "--to", "memory", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("06_Agent_Memory/Great idea.md", doc.RootElement.GetProperty("to").GetString());
    }
}
