using MindVault.Core;

namespace MindVault.Tests;

/// <summary>
/// The safety contract in one suite: every mutation snapshots BEFORE changing content,
/// partial failures roll back, unsafe inputs are rejected, and the index never lies after
/// a mutation. If any of these fail, MindVault cannot be trusted with a real vault.
/// </summary>
public sealed class MutationTortureTests
{
    [Fact]
    public void AppendSnapshotsTheExactPreMutationContent()
    {
        using var tv = new TempVault();
        var before = tv.ReadNote("01_Projects/Alpha.md");
        var result = tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "torture entry");
        Assert.True(File.Exists(result.SnapshotPath));
        Assert.Equal(before, File.ReadAllText(result.SnapshotPath!));
        Assert.NotEqual(before, tv.ReadNote("01_Projects/Alpha.md"));
    }

    [Fact]
    public void FrontmatterUpdateSnapshotsTheExactPreMutationContent()
    {
        using var tv = new TempVault();
        var before = tv.ReadNote("01_Projects/Task - Ship v1.md");
        var result = tv.Ctx.Writer.UpdateFrontmatter("Task - Ship v1", "status", "active");
        Assert.Equal(before, File.ReadAllText(result.SnapshotPath!));
    }

    [Fact]
    public void ArchiveSnapshotsBeforeMoving()
    {
        using var tv = new TempVault();
        var before = tv.ReadNote("01_Projects/Task - Write docs.md");
        var result = tv.Ctx.Writer.Archive("Task - Write docs");
        Assert.Equal(before, File.ReadAllText(result.SnapshotPath));
        Assert.False(File.Exists(tv.Abs("01_Projects/Task - Write docs.md")));
        Assert.True(File.Exists(tv.Abs(result.ToPath)));
    }

    [Fact]
    public void SupersedeSnapshotsBothNotesBeforeTouchingEither()
    {
        using var tv = new TempVault();
        tv.Ctx.Writer.CreateDecision("Alpha", "Use Postgres for search");
        var oldBefore = tv.ReadNote("04_Decisions/Decision - Use SQLite.md");
        var newBefore = tv.ReadNote("04_Decisions/Decision - Use Postgres for search.md");

        var result = tv.Ctx.Writer.SupersedeDecision("Decision - Use SQLite", "Decision - Use Postgres for search");
        Assert.Equal(oldBefore, File.ReadAllText(result.OldSnapshot));
        Assert.Equal(newBefore, File.ReadAllText(result.NewSnapshot));
        Assert.Contains("status: superseded", tv.ReadNote("04_Decisions/Decision - Use SQLite.md"));
    }

    [Fact]
    public void SupersedeRollsBackTheOldNoteWhenTheNewWriteFails()
    {
        using var tv = new TempVault();
        // A decision whose type is indexed (flat keys survive) but whose YAML contains a
        // nested structure, so the edit-parse throws AFTER the old note was already written.
        File.WriteAllText(tv.Abs("04_Decisions/Decision - Broken Target.md"), """
            ---
            type: decision
            status: accepted
            created: 2026-01-01
            updated: 2026-01-01
            tags: [decision]
            nested:
              a: b
            ---

            # Broken Target
            """.Replace("\r\n", "\n"));
        tv.Ctx.Scanner.Scan();
        var oldBefore = tv.ReadNote("04_Decisions/Decision - Use SQLite.md");

        var ex = Assert.Throws<MindVaultException>(() =>
            tv.Ctx.Writer.SupersedeDecision("Decision - Use SQLite", "Decision - Broken Target"));
        Assert.Contains("rolled back", ex.Message);
        Assert.Equal(oldBefore, tv.ReadNote("04_Decisions/Decision - Use SQLite.md"));
        Assert.DoesNotContain("superseded",
            tv.Ctx.Db.FindByPath("04_Decisions/Decision - Use SQLite.md")!.Status ?? "");
    }

    [Fact]
    public void WritesOutsideTheVaultAreRejected()
    {
        using var tv = new TempVault();
        Assert.Throws<UnsafePathException>(() =>
            tv.Ctx.Writer.CreateNoteFile("../evil.md", "# Evil"));
        Assert.Throws<UnsafePathException>(() =>
            tv.Ctx.Writer.CreateNoteFile(Path.Combine(Path.GetTempPath(), "evil.md"), "# Evil"));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(tv.Root)!, "evil.md")));
    }

    [Fact]
    public void PathTraversalIsRejectedWithTheStableCode()
    {
        using var tv = new TempVault();
        var ex = Assert.Throws<UnsafePathException>(() =>
            PathGuard.ResolveNotePath(tv.Root, "..\\..\\outside.md"));
        Assert.Equal(ErrorCodes.PathTraversalRejected, ex.Code);
        Assert.Throws<UnsafePathException>(() =>
            PathGuard.ResolveNotePath(tv.Root, ".mindvault/sneaky.md"));
    }

    [Fact]
    public void AmbiguousNoteRefsAreRejectedNotGuessed()
    {
        using var tv = new TempVault();
        var ex = Assert.Throws<AmbiguousNoteRefException>(() =>
            tv.Ctx.Writer.AppendToSection("Duplicate Note", "Anything", "x", createSection: true));
        Assert.Equal(2, ex.Candidates.Count);
        Assert.Equal(ErrorCodes.NoteRefAmbiguous, ex.Code);
    }

    [Fact]
    public void NearDuplicateTitleCreationWarns()
    {
        using var tv = new TempVault();
        var result = tv.Ctx.Writer.CreateTask("Alpha", "Ship the v1");
        Assert.Contains(result.Warnings, w => w.Contains("Ship v1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InvalidYamlNotesRefuseFrontmatterEdits()
    {
        using var tv = new TempVault();
        var ex = Assert.Throws<MindVaultException>(() =>
            tv.Ctx.Writer.UpdateFrontmatter("Bad Yaml", "status", "active"));
        Assert.Equal(ErrorCodes.InvalidFrontmatter, ex.Code);
        Assert.Contains("Fix the note in Obsidian", ex.Message);
    }

    [Fact]
    public void NestedYamlValuesAreRejected()
    {
        using var tv = new TempVault();
        var ex = Assert.Throws<MindVaultException>(() =>
            tv.Ctx.Writer.UpdateFrontmatter("Alpha", "meta", "{a: b}"));
        Assert.Equal(ErrorCodes.InvalidFrontmatter, ex.Code);
        Assert.Throws<MindVaultException>(() =>
            tv.Ctx.Writer.UpdateFrontmatter("Nested", "status", "active"));
    }

    [Fact]
    public void TempFilesAreNeverIndexed()
    {
        using var tv = new TempVault();
        File.WriteAllText(tv.Abs("01_Projects/Ghost.md.mindvault-tmp"), "---\ntype: task\n---\n# Ghost");
        tv.Ctx.Scanner.Scan();
        Assert.Null(tv.Ctx.Db.FindByStem("Ghost.md").FirstOrDefault());
        Assert.DoesNotContain(tv.Ctx.Db.GetAllNotes(), n => n.Path.Contains("mindvault-tmp"));
    }

    [Fact]
    public void FailedWritesNameTheProblemAndTheOptions()
    {
        using var tv = new TempVault();
        var ex = Assert.Throws<MindVaultException>(() =>
            tv.Ctx.Writer.AppendToSection("Alpha", "No Such Section", "content"));
        Assert.Contains("No Such Section", ex.Message);
        Assert.Contains("Available headings", ex.Message);
        Assert.Contains("--create-section", ex.Message);
    }

    [Fact]
    public void IndexStaysConsistentThroughMutations()
    {
        using var tv = new TempVault();
        var archive = tv.Ctx.Writer.Archive("Task - Write docs");
        Assert.Null(tv.Ctx.Db.FindByPath("01_Projects/Task - Write docs.md"));
        Assert.NotNull(tv.Ctx.Db.FindByPath(archive.ToPath));

        tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "uniquetorturemarker42");
        var hits = tv.Ctx.Search.Search("uniquetorturemarker42");
        Assert.Contains(hits, h => h.Path == "01_Projects/Alpha.md");

        var verify = tv.Ctx.IndexCheck.Verify();
        Assert.True(verify.Ok, string.Join(" | ", verify.Issues.Select(i => $"{i.Code}:{i.Path}")));
    }

    [Fact]
    public void RestoreIsItselfSnapshottedAndReversible()
    {
        using var tv = new TempVault();
        var original = tv.ReadNote("01_Projects/Alpha.md");
        tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "mutation to be undone");
        var restore = tv.Ctx.Writer.RestoreFromSnapshot("Alpha");
        Assert.Equal(original, tv.ReadNote("01_Projects/Alpha.md"));
        Assert.True(File.Exists(restore.PreRestoreSnapshot));
        Assert.Contains("mutation to be undone", File.ReadAllText(restore.PreRestoreSnapshot));
    }
}
