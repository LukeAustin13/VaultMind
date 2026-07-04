using MindVault.Core;

namespace MindVault.Tests;

public sealed class ScanIndexTests
{
    private const int FixtureNoteCount = 14;

    [Fact]
    public void ScanIndexesAllFixtureNotes()
    {
        using var tv = new TempVault(init: false, scan: false);
        var result = tv.Ctx.Scanner.Scan();
        Assert.Equal(FixtureNoteCount, result.Added);
        Assert.Equal(FixtureNoteCount, tv.Ctx.Db.CountNotes());
        Assert.Empty(result.Errors);
        Assert.NotNull(tv.Ctx.State.Load()?.LastScanUtc);
    }

    [Fact]
    public void SecondScanIsIncrementalNoop()
    {
        using var tv = new TempVault(init: false);
        var result = tv.Ctx.Scanner.Scan();
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Removed);
        Assert.Equal(FixtureNoteCount, result.Unchanged);
    }

    [Fact]
    public void ModifiedFileIsReindexed()
    {
        using var tv = new TempVault(init: false);
        File.AppendAllText(tv.Abs("01_Projects/Alpha.md"), "\nzzzuniqueterm\n");
        var result = tv.Ctx.Scanner.Scan();
        Assert.Equal(1, result.Updated);
        var hits = tv.Ctx.Search.Search("zzzuniqueterm");
        Assert.Single(hits);
        Assert.Equal("01_Projects/Alpha.md", hits[0].Path);
    }

    [Fact]
    public void DeletedFileIsRemovedFromIndex()
    {
        using var tv = new TempVault(init: false);
        File.Delete(tv.Abs("02_Areas/Duplicate Note.md"));
        var result = tv.Ctx.Scanner.Scan();
        Assert.Equal(1, result.Removed);
        Assert.Null(tv.Ctx.Db.FindByPath("02_Areas/Duplicate Note.md"));
    }

    [Fact]
    public void OperationalAndBuildFoldersAreSkipped()
    {
        using var tv = new TempVault(init: false, scan: false);
        Directory.CreateDirectory(tv.Abs(".obsidian"));
        Directory.CreateDirectory(tv.Abs("bin"));
        File.WriteAllText(tv.Abs(".obsidian/workspace.md"), "# hidden\n");
        File.WriteAllText(tv.Abs("bin/build-output.md"), "# hidden\n");
        tv.Ctx.Scanner.Scan();
        Assert.Equal(FixtureNoteCount, tv.Ctx.Db.CountNotes());
        Assert.Empty(tv.Ctx.Db.FindByStem("workspace"));
    }

    [Fact]
    public void RebuildIndexRecreatesEverything()
    {
        using var tv = new TempVault(init: false);
        var rebuild = tv.Ctx.Scanner.Scan(full: true);
        Assert.Equal(FixtureNoteCount, rebuild.Added);
        Assert.Equal(FixtureNoteCount, tv.Ctx.Db.CountNotes());
        Assert.Single(tv.Ctx.Search.Search("bm25 ranking"));
    }

    [Fact]
    public void SameSizeMtimePreservingEditIsMissedByDefaultButCaughtWithContentHash()
    {
        using var tv = new TempVault(init: false);
        var abs = tv.Abs("01_Projects/Alpha.md");
        var original = File.ReadAllText(abs);
        var mtime = File.GetLastWriteTimeUtc(abs);
        // "alpha release" -> "gamma release": same byte length, so file size is unchanged.
        var edited = original.Replace("alpha release", "gamma release");
        Assert.NotEqual(original, edited);
        Assert.Equal(original.Length, edited.Length);
        File.WriteAllText(abs, edited);
        File.SetLastWriteTimeUtc(abs, mtime);

        // Default fast path: mtime+size unchanged, so the edit is not detected.
        Assert.Equal(0, tv.Ctx.Scanner.Scan().Updated);

        // With content-hash verification the same edit is picked up.
        using var verifying = new VaultContext(new LoadedConfig(
            new MindVaultConfig { VaultPath = tv.Root, VerifyContentHash = true }, "test", null));
        Assert.Equal(1, verifying.Scanner.Scan().Updated);
    }

    [Fact]
    public void IndexStoresTagsLinksAndHeadings()
    {
        using var tv = new TempVault(init: false);
        var links = tv.Ctx.Db.GetAllLinks();
        Assert.Contains(links, l => l.NotePath == "01_Projects/Task - Ship v1.md" && l.TargetNorm == "alpha");
        var backlinks = tv.Ctx.Db.GetBacklinkPaths("alpha", "alpha", selfId: -1);
        Assert.Contains("01_Projects/Task - Ship v1.md", backlinks);
        Assert.Contains("04_Decisions/Decision - Use SQLite.md", backlinks);
    }
}
