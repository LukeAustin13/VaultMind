using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Obsidian realities: alias/heading/block links, conflict copies, .canvas, spaces.</summary>
public sealed class ObsidianCompatTests
{
    [Fact]
    public void AliasHeadingAndBlockLinksAllResolveToTheTargetNote()
    {
        using var tv = new TempVault();
        File.WriteAllText(tv.Abs("03_Resources/Linker.md"), """
            ---
            type: research
            status: draft
            created: 2026-01-01
            updated: 2026-01-01
            tags: [test]
            ---

            # Linker

            Alias link: [[Alpha|the project]]
            Heading link: [[Decision - Use SQLite#Context]]
            Block link: [[Task - Ship v1#^abc123]]
            """.Replace("\r\n", "\n"));
        tv.Ctx.Scanner.Scan();

        var report = tv.Ctx.Validator.Validate();
        var brokenFromLinker = report.Issues
            .Where(i => i.Code == "broken-link" && i.Path == "03_Resources/Linker.md").ToList();
        Assert.Empty(brokenFromLinker);

        var alpha = tv.Ctx.Resolver.Resolve("Alpha");
        var backlinks = tv.Ctx.Db.GetBacklinkPaths(
            SlugHelper.NormalizeWiki(alpha.Title), SlugHelper.NormalizeWiki(alpha.Stem), alpha.Id);
        Assert.Contains("03_Resources/Linker.md", backlinks);
    }

    [Fact]
    public void WikiLinkRefsWithAliasOrHeadingResolveViaTheResolver()
    {
        using var tv = new TempVault();
        Assert.Equal("01_Projects/Alpha.md", tv.Ctx.Resolver.Resolve("[[Alpha|whatever alias]]").Path);
        Assert.Equal("04_Decisions/Decision - Use SQLite.md",
            tv.Ctx.Resolver.Resolve("[[Decision - Use SQLite#Context]]").Path);
    }

    [Fact]
    public void FileNamesWithSpacesWorkEndToEnd()
    {
        using var tv = new TempVault();
        var note = tv.Ctx.Resolver.Resolve("Task - Ship v1");
        Assert.Equal("01_Projects/Task - Ship v1.md", note.Path);
        var hits = tv.Ctx.Search.Search("Ship v1");
        Assert.Contains(hits, h => h.Path == note.Path);
    }

    [Fact]
    public void SyncConflictCopiesAreNeverIndexedButAreReported()
    {
        using var tv = new TempVault();
        var syncthing = "01_Projects/Task - Ship v1.sync-conflict-20260704-123456-ABCDEF.md";
        var dropbox = "03_Resources/Notes (John's conflicted copy 2026-07-04).md";
        File.WriteAllText(tv.Abs(syncthing), "# Conflict copy syncthingmarker\n");
        File.WriteAllText(tv.Abs(dropbox), "# Conflict copy dropboxmarker\n");
        tv.Ctx.Scanner.Scan();

        Assert.Null(tv.Ctx.Db.FindByPath(syncthing));
        Assert.Null(tv.Ctx.Db.FindByPath(dropbox));
        Assert.Empty(tv.Ctx.Search.Search("syncthingmarker"));

        var report = tv.Ctx.Validator.Validate();
        var conflictIssues = report.Issues.Where(i => i.Code == "sync-conflict-file").ToList();
        Assert.Equal(2, conflictIssues.Count);
        Assert.All(conflictIssues, i => Assert.Equal(IssueSeverity.Warning, i.Severity));
    }

    [Fact]
    public void PreviouslyIndexedConflictFileIsDroppedOnNextScan()
    {
        using var tv = new TempVault();
        // Simulate an index that contains a conflict file (e.g. created by an older version).
        var parsed = NoteParser.Parse("# Old conflict\n", "00_Inbox/Old.sync-conflict-20260101-000000-XYZ.md");
        tv.Ctx.Db.UpsertNote(parsed);
        Assert.NotNull(tv.Ctx.Db.FindByPath("00_Inbox/Old.sync-conflict-20260101-000000-XYZ.md"));

        tv.Ctx.Scanner.Scan();
        Assert.Null(tv.Ctx.Db.FindByPath("00_Inbox/Old.sync-conflict-20260101-000000-XYZ.md"));
    }

    [Fact]
    public void CanvasAndOtherNonMarkdownFilesAreIgnored()
    {
        using var tv = new TempVault();
        var before = tv.Ctx.Db.CountNotes();
        File.WriteAllText(tv.Abs("03_Resources/Board.canvas"), "{\"nodes\":[]}");
        File.WriteAllText(tv.Abs("03_Resources/image.png"), "not really a png");
        tv.Ctx.Scanner.Scan();
        Assert.Equal(before, tv.Ctx.Db.CountNotes());
    }

    [Fact]
    public void FrontmatterAndInlineTagsBothIndex()
    {
        using var tv = new TempVault();
        File.WriteAllText(tv.Abs("03_Resources/Tagged.md"), """
            ---
            type: research
            status: draft
            created: 2026-01-01
            updated: 2026-01-01
            tags: [fromfrontmatter]
            ---

            # Tagged

            Body mentions #frominline here.
            """.Replace("\r\n", "\n"));
        tv.Ctx.Scanner.Scan();

        Assert.Contains(tv.Ctx.Search.List(tag: "fromfrontmatter"), n => n.Path == "03_Resources/Tagged.md");
        Assert.Contains(tv.Ctx.Search.List(tag: "frominline"), n => n.Path == "03_Resources/Tagged.md");
    }
}
