using MindVault.Core;

namespace MindVault.Tests;

public sealed class RestorePruneTests : IDisposable
{
    private readonly TempVault _tv = new();

    [Fact]
    public void RestoreBringsBackThePreMutationContent()
    {
        var original = _tv.ReadNote("01_Projects/Alpha.md");
        _tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "mistaken content zzzmistake");
        Assert.NotEqual(original, _tv.ReadNote("01_Projects/Alpha.md"));

        var result = _tv.Ctx.Writer.RestoreFromSnapshot("Alpha");

        Assert.Equal(original, _tv.ReadNote("01_Projects/Alpha.md"));
        Assert.True(File.Exists(result.PreRestoreSnapshot)); // the mistake is itself recoverable
        Assert.Contains("zzzmistake", File.ReadAllText(result.PreRestoreSnapshot));
        Assert.Empty(_tv.Ctx.Search.Search("zzzmistake")); // index reflects the restore
    }

    [Fact]
    public void RestoreCanUseAnExplicitSnapshot()
    {
        _tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "first edit");
        _tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "second edit");
        var snapshots = _tv.Ctx.Snapshots.ListSnapshots("Alpha");
        Assert.Equal(2, snapshots.Count);

        // The oldest snapshot is the original note (taken before the first edit).
        _tv.Ctx.Writer.RestoreFromSnapshot("Alpha", snapshots[^1]);
        var content = _tv.ReadNote("01_Projects/Alpha.md");
        Assert.DoesNotContain("first edit", content);
        Assert.DoesNotContain("second edit", content);
    }

    [Fact]
    public void RestoreRejectsSourcesOutsideTheSnapshotFolder()
    {
        Assert.Throws<MindVaultException>(() =>
            _tv.Ctx.Writer.RestoreFromSnapshot("Alpha", _tv.Abs("01_Projects/Task - Ship v1.md")));
    }

    [Fact]
    public void RestoreWithoutSnapshotsFailsClearly()
    {
        var ex = Assert.Throws<MindVaultException>(() => _tv.Ctx.Writer.RestoreFromSnapshot("Scratch"));
        Assert.Contains("No snapshots", ex.Message);
    }

    [Fact]
    public void PruneRemovesOnlySnapshotsPastRetention()
    {
        var oldDir = Path.Combine(_tv.Ctx.SnapshotDir, "2020-01-01");
        Directory.CreateDirectory(oldDir);
        File.WriteAllText(Path.Combine(oldDir, "20200101-000000000-Ancient.md"), "# ancient\n");
        _tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "keeps a fresh snapshot around");

        var removed = _tv.Ctx.Snapshots.Prune(30);

        Assert.Equal(1, removed);
        Assert.False(Directory.Exists(oldDir));
        Assert.NotEmpty(_tv.Ctx.Snapshots.ListSnapshots("Alpha"));
    }

    [Fact]
    public void PruneRejectsZeroRetention()
    {
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Snapshots.Prune(0));
    }

    [Fact]
    public void ListSnapshotsMatchesExactNoteOnly()
    {
        _tv.Ctx.Writer.AppendToSection("Task - Ship v1", "Notes", "x");
        Assert.Empty(_tv.Ctx.Snapshots.ListSnapshots("Alpha"));
        Assert.Single(_tv.Ctx.Snapshots.ListSnapshots("Task - Ship v1"));
    }

    [Fact]
    public void ListSnapshotsDoesNotMatchDashPrefixedSiblingNote()
    {
        // Craft snapshot files for "Foo" and the unrelated sibling "Foo-bar".
        var day = Path.Combine(_tv.Ctx.SnapshotDir, "2026-07-04");
        Directory.CreateDirectory(day);
        File.WriteAllText(Path.Combine(day, "20260703-120000000-Foo.md"), "# foo\n");
        File.WriteAllText(Path.Combine(day, "20260704-120000000-Foo-bar.md"), "# foobar\n");

        var forFoo = _tv.Ctx.Snapshots.ListSnapshots("Foo");
        Assert.Single(forFoo);
        Assert.DoesNotContain(forFoo, f => Path.GetFileName(f).Contains("Foo-bar"));
    }

    [Fact]
    public void ListSnapshotsOrdersSameMillisecondDedupNewestFirst()
    {
        var day = Path.Combine(_tv.Ctx.SnapshotDir, "2026-07-04");
        Directory.CreateDirectory(day);
        // Same 18-char stamp; "-1" was written second (newer) than the suffix-less one.
        File.WriteAllText(Path.Combine(day, "20260704-120000000-Note.md"), "older\n");
        File.WriteAllText(Path.Combine(day, "20260704-120000000-Note-1.md"), "newer\n");

        var snapshots = _tv.Ctx.Snapshots.ListSnapshots("Note");
        Assert.Equal(2, snapshots.Count);
        Assert.EndsWith("Note-1.md", snapshots[0]);
    }

    public void Dispose() => _tv.Dispose();
}
