using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Dry-run mutations must change NOTHING: no file bytes, no moves, no snapshots.</summary>
public sealed class DryRunTests : IDisposable
{
    private readonly TempVault _tv = new();

    private int SnapshotFileCount() =>
        Directory.Exists(_tv.Ctx.SnapshotDir)
            ? Directory.GetFiles(_tv.Ctx.SnapshotDir, "*", SearchOption.AllDirectories).Length
            : 0;

    [Fact]
    public void ArchiveDryRunPreviewsWithoutMoving()
    {
        var before = _tv.ReadNote("01_Projects/Task - Write docs.md");
        var snapshots = SnapshotFileCount();

        var result = _tv.Ctx.Writer.Archive("Task - Write docs", dryRun: true);

        Assert.Equal("01_Projects/Task - Write docs.md", result.FromPath);
        Assert.Equal("99_Archive/Task - Write docs.md", result.ToPath);
        Assert.True(File.Exists(_tv.Abs("01_Projects/Task - Write docs.md")));
        Assert.False(File.Exists(_tv.Abs("99_Archive/Task - Write docs.md")));
        Assert.Equal(before, _tv.ReadNote("01_Projects/Task - Write docs.md"));
        Assert.Equal(snapshots, SnapshotFileCount());
        Assert.Contains(result.Warnings, w => w.Contains("[dry-run]"));
    }

    [Fact]
    public void UpdateFrontmatterDryRunShowsOldAndNewValue()
    {
        var before = _tv.ReadNote("01_Projects/Task - Ship v1.md");
        var snapshots = SnapshotFileCount();

        var result = _tv.Ctx.Writer.UpdateFrontmatter("Task - Ship v1", "status", "done", dryRun: true);

        Assert.False(result.Changed);
        Assert.Null(result.SnapshotPath);
        Assert.Contains("-> 'done'", result.Message);
        Assert.Equal(before, _tv.ReadNote("01_Projects/Task - Ship v1.md"));
        Assert.Equal(snapshots, SnapshotFileCount());
    }

    [Fact]
    public void AppendDryRunLeavesTheNoteAlone()
    {
        var before = _tv.ReadNote("01_Projects/Alpha.md");

        var result = _tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "new content", dryRun: true);

        Assert.False(result.Changed);
        Assert.Contains("Would append", result.Message);
        Assert.Equal(before, _tv.ReadNote("01_Projects/Alpha.md"));
    }

    [Fact]
    public void AppendDryRunStillReportsMissingSections()
    {
        var ex = Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.AppendToSection("Alpha", "No Such Heading", "x", dryRun: true));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void DryRunDoesNotTouchTheIndex()
    {
        _tv.Ctx.Writer.Archive("Task - Write docs", dryRun: true);
        // The note is still indexed at its original path and still resolvable.
        Assert.Equal("01_Projects/Task - Write docs.md", _tv.Ctx.Resolver.Resolve("Task - Write docs").Path);
    }

    public void Dispose() => _tv.Dispose();
}
