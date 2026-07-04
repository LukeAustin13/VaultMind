using MindVault.Core;

namespace MindVault.Tests;

public sealed class WriteServiceTests : IDisposable
{
    private readonly TempVault _tv = new();
    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    // ---------- create ----------

    [Fact]
    public void CreateProjectWritesValidNote()
    {
        var note = _tv.Ctx.Writer.CreateProject("Beta").Note;
        Assert.Equal("01_Projects/Beta.md", note.Path);
        Assert.Equal("project", note.Type);
        Assert.Equal("active", note.Status);
        var content = _tv.ReadNote(note.Path);
        Assert.Contains($"created: {Today}", content);
        Assert.Contains("## Next Actions", content);
        Assert.Equal(note.Path, _tv.Ctx.Resolver.Resolve("Beta").Path);
    }

    [Fact]
    public void CreateProjectRejectsDuplicates()
    {
        _tv.Ctx.Writer.CreateProject("Beta");
        var ex = Assert.Throws<DuplicateSuspectedException>(() => _tv.Ctx.Writer.CreateProject("Beta"));
        Assert.Equal(ErrorCodes.DuplicateSuspected, ex.Code);
        // Even with the override, the exact file collision still refuses — no silent overwrite.
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Writer.CreateProject("Beta", allowDuplicate: true));
    }

    [Fact]
    public void CreateProjectWithTraversalNameStaysInsideVault()
    {
        var note = _tv.Ctx.Writer.CreateProject(@"..\..\evil").Note;
        Assert.StartsWith("01_Projects/", note.Path);
        Assert.True(File.Exists(_tv.Abs(note.Path)));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(_tv.Root)!, "evil.md")));
    }

    [Fact]
    public void CreateProjectWithReservedDeviceNameProducesReadableNote()
    {
        var note = _tv.Ctx.Writer.CreateProject("CON").Note;
        Assert.Equal("01_Projects/_CON.md", note.Path);
        Assert.True(File.Exists(_tv.Abs(note.Path)));
        Assert.Equal(note.Path, _tv.Ctx.Resolver.Resolve("_CON").Path);
    }

    [Fact]
    public void CreateDecisionRequiresExistingProject()
    {
        var ex = Assert.Throws<MindVaultException>(() => _tv.Ctx.Writer.CreateDecision("Nope", "Anything"));
        Assert.Contains("Project not found", ex.Message);
    }

    [Fact]
    public void CreateDecisionLinksToProject()
    {
        var note = _tv.Ctx.Writer.CreateDecision("Alpha", "Adopt Markdig").Note;
        Assert.Equal("04_Decisions/Decision - Adopt Markdig.md", note.Path);
        Assert.Equal("Alpha", note.Project);
        var content = _tv.ReadNote(note.Path);
        Assert.Contains("[[Alpha]]", content);
        Assert.Contains("# Decision: Adopt Markdig", content);
    }

    [Fact]
    public void CreateTaskLinksToProject()
    {
        var note = _tv.Ctx.Writer.CreateTask("Alpha", "Refactor parser").Note;
        Assert.Equal("01_Projects/Task - Refactor parser.md", note.Path);
        Assert.Equal("task", note.Type);
        Assert.Equal("open", note.Status);
        Assert.Contains("priority: medium", _tv.ReadNote(note.Path));
    }

    // ---------- append ----------

    [Fact]
    public void AppendAddsContentInsideTheRightSection()
    {
        var result = _tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "Also support MCP agents.");
        var content = _tv.ReadNote("01_Projects/Alpha.md");
        var goal = content.IndexOf("full text search over vault notes.", StringComparison.Ordinal);
        var added = content.IndexOf("Also support MCP agents.", StringComparison.Ordinal);
        var nextHeading = content.IndexOf("## Non-Negotiables", StringComparison.Ordinal);
        Assert.True(goal >= 0 && added > goal && added < nextHeading,
            $"expected insertion between existing goal text and next heading:\n{content}");
        Assert.Contains($"updated: {Today}", content);
        Assert.True(File.Exists(result.SnapshotPath));
    }

    [Fact]
    public void AppendToLastSectionWorks()
    {
        _tv.Ctx.Writer.AppendToSection("Alpha", "Next Actions", "- write more tests");
        var content = _tv.ReadNote("01_Projects/Alpha.md");
        Assert.Contains("- Ship v1\n\n- write more tests", content.Replace("\r\n", "\n"));
    }

    [Fact]
    public void AppendToMissingSectionFailsWithHeadingList()
    {
        var ex = Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.AppendToSection("Alpha", "No Such Section", "x"));
        Assert.Contains("'Goal'", ex.Message);
    }

    [Fact]
    public void AppendCanCreateMissingSectionOnRequest()
    {
        _tv.Ctx.Writer.AppendToSection("Scratch", "Log", "first entry", createSection: true);
        var content = _tv.ReadNote("00_Inbox/Scratch.md");
        Assert.Contains("## Log", content);
        Assert.Contains("first entry", content);
    }

    [Fact]
    public void AppendPreservesCrlfLineEndings()
    {
        var path = _tv.Abs("00_Inbox/Crlf.md");
        File.WriteAllText(path, "# Crlf\r\n\r\n## Log\r\n");
        _tv.Ctx.Scanner.Scan();
        _tv.Ctx.Writer.AppendToSection("Crlf", "Log", "entry");
        var raw = File.ReadAllText(path);
        Assert.Contains("\r\n", raw);
        Assert.DoesNotContain("\n", raw.Replace("\r\n", ""));
        Assert.Contains("entry", raw);
    }

    [Fact]
    public void AppendSnapshotsBeforeMutation()
    {
        var original = _tv.ReadNote("01_Projects/Task - Ship v1.md");
        var result = _tv.Ctx.Writer.AppendToSection("Task - Ship v1", "Notes", "remember the docs");
        Assert.True(File.Exists(result.SnapshotPath));
        Assert.Equal(original, File.ReadAllText(result.SnapshotPath!));
        Assert.StartsWith(_tv.Ctx.SnapshotDir, result.SnapshotPath!);
    }

    // ---------- frontmatter ----------

    [Fact]
    public void UpdateFrontmatterChangesOneKeyAndBumpsUpdated()
    {
        _tv.Ctx.Writer.UpdateFrontmatter("Task - Ship v1", "status", "done");
        var content = _tv.ReadNote("01_Projects/Task - Ship v1.md");
        Assert.Contains("status: done", content);
        Assert.Contains($"updated: {Today}", content);
        Assert.Contains("priority: high", content); // untouched keys survive
        Assert.Equal("done", _tv.Ctx.Db.FindByPath("01_Projects/Task - Ship v1.md")!.Status);
    }

    [Fact]
    public void UpdateFrontmatterRejectsNestedValues()
    {
        Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.UpdateFrontmatter("Alpha", "meta", "{a: b}"));
        Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.UpdateFrontmatter("Alpha", "meta", "[1, 2]"));
        Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.UpdateFrontmatter("Alpha", "meta", "line1\nline2"));
    }

    [Fact]
    public void UpdateFrontmatterSplitsTagsIntoList()
    {
        _tv.Ctx.Writer.UpdateFrontmatter("Alpha", "tags", "project, alpha, focus");
        var content = _tv.ReadNote("01_Projects/Alpha.md").Replace("\r\n", "\n");
        Assert.Contains("tags:\n  - project\n  - alpha\n  - focus\n", content);
    }

    [Fact]
    public void UpdateFrontmatterRefusesNotesWithBrokenYaml()
    {
        var ex = Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.UpdateFrontmatter("Bad Yaml", "status", "open"));
        Assert.Contains("not valid flat YAML", ex.Message);
    }

    // ---------- link ----------

    [Fact]
    public void LinkAddsWikiLinkAndCreatesFrontmatterWhenMissing()
    {
        var result = _tv.Ctx.Writer.LinkNotes("Scratch", "Alpha");
        Assert.True(result.Changed);
        var content = _tv.ReadNote("00_Inbox/Scratch.md").Replace("\r\n", "\n");
        Assert.StartsWith("---\n", content);
        Assert.Contains("[[Alpha]]", content);
        Assert.Contains("# Scratch", content); // body preserved
    }

    [Fact]
    public void LinkingTwiceIsANoop()
    {
        _tv.Ctx.Writer.LinkNotes("Scratch", "Alpha");
        var before = _tv.ReadNote("00_Inbox/Scratch.md");
        var second = _tv.Ctx.Writer.LinkNotes("Scratch", "Alpha");
        Assert.False(second.Changed);
        Assert.Equal(before, _tv.ReadNote("00_Inbox/Scratch.md"));
    }

    [Fact]
    public void LinkingANoteToItselfIsRejected()
    {
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Writer.LinkNotes("Alpha", "[[Alpha]]"));
    }

    // ---------- archive ----------

    [Fact]
    public void ArchiveSnapshotsMarksAndMovesInsteadOfDeleting()
    {
        var result = _tv.Ctx.Writer.Archive("Task - Write docs");
        Assert.Equal("01_Projects/Task - Write docs.md", result.FromPath);
        Assert.Equal("99_Archive/Task - Write docs.md", result.ToPath);
        Assert.False(File.Exists(_tv.Abs(result.FromPath)));
        Assert.True(File.Exists(_tv.Abs(result.ToPath)));
        Assert.True(File.Exists(result.SnapshotPath));
        Assert.Contains("status: archived", _tv.ReadNote(result.ToPath));
        Assert.Null(_tv.Ctx.Db.FindByPath(result.FromPath));
        Assert.NotNull(_tv.Ctx.Db.FindByPath(result.ToPath));
    }

    [Fact]
    public void ArchivingAnArchivedNoteIsRejected()
    {
        _tv.Ctx.Writer.Archive("Task - Write docs");
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Writer.Archive("Task - Write docs"));
    }

    // ---------- path guard ----------

    [Theory]
    [InlineData("../outside.md")]
    [InlineData(@"..\outside.md")]
    [InlineData(@"01_Projects\..\..\outside.md")]
    [InlineData(@"C:\Windows\outside.md")]
    public void PathGuardRejectsEscapes(string path)
    {
        Assert.Throws<UnsafePathException>(() => PathGuard.ResolveInsideVault(_tv.Root, path));
    }

    [Fact]
    public void PathGuardRejectsWritesIntoOperationalFolders()
    {
        Assert.Throws<UnsafePathException>(() => PathGuard.ResolveNotePath(_tv.Root, ".mindvault/hack.md"));
        Assert.Throws<UnsafePathException>(() => PathGuard.ResolveNotePath(_tv.Root, ".obsidian/hack.md"));
    }

    [Fact]
    public void PathGuardAllowsNormalNotePaths()
    {
        var abs = PathGuard.ResolveInsideVault(_tv.Root, "01_Projects/Alpha.md");
        Assert.StartsWith(_tv.Root, abs);
    }

    public void Dispose() => _tv.Dispose();
}
