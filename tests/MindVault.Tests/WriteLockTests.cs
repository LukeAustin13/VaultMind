using System.Text.Json;
using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Cross-process write lock: fresh foreign locks block clearly, stale locks never brick the vault.</summary>
public sealed class WriteLockTests
{
    private static string LockPath(TempVault tv) => Path.Combine(tv.Root, ".mindvault", "write.lock");

    private static void WriteForeignLock(TempVault tv, DateTime createdUtc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LockPath(tv))!);
        File.WriteAllText(LockPath(tv), JsonSerializer.Serialize(
            new { pid = 99999, machine = "other-box", createdUtc }, Json.Options));
    }

    [Fact]
    public void FreshForeignLockFailsWritesWithWriteLocked()
    {
        using var tv = new TempVault();
        WriteForeignLock(tv, DateTime.UtcNow);

        var ex = Assert.Throws<WriteLockedException>(() =>
            tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "should not land"));
        Assert.Equal(ErrorCodes.WriteLocked, ex.Code);
        Assert.Contains("pid 99999", ex.Message);
        Assert.Contains("delete the lock file", ex.Message);
        Assert.DoesNotContain("should not land", tv.ReadNote("01_Projects/Alpha.md"));
    }

    [Fact]
    public void ReadsAndSearchesIgnoreTheLock()
    {
        using var tv = new TempVault();
        WriteForeignLock(tv, DateTime.UtcNow);
        Assert.NotEmpty(tv.Ctx.Search.Search("SQLite"));
        Assert.NotNull(tv.Ctx.Resolver.Resolve("Alpha"));
        Assert.True(tv.Ctx.Db.CountNotes() > 0);
    }

    [Fact]
    public void StaleLockIsTakenOverAndTheWriteSucceeds()
    {
        using var tv = new TempVault();
        WriteForeignLock(tv, DateTime.UtcNow.AddHours(-2)); // default stale window is 600s

        var result = tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "landed after takeover");
        Assert.Contains("landed after takeover", tv.ReadNote("01_Projects/Alpha.md"));
        Assert.NotNull(result.SnapshotPath);
        Assert.False(File.Exists(LockPath(tv)), "lock must be released after the mutation");
    }

    [Fact]
    public void UnparseableStaleLockFileDoesNotBrickTheVault()
    {
        using var tv = new TempVault();
        Directory.CreateDirectory(Path.GetDirectoryName(LockPath(tv))!);
        File.WriteAllText(LockPath(tv), "not json at all");
        File.SetLastWriteTimeUtc(LockPath(tv), DateTime.UtcNow.AddHours(-1)); // mtime fallback

        tv.Ctx.Writer.AppendToSection("Alpha", "Goal", "recovered");
        Assert.Contains("recovered", tv.ReadNote("01_Projects/Alpha.md"));
    }

    [Fact]
    public void LockIsHeldDuringAndReleasedAfterEveryMutation()
    {
        using var tv = new TempVault();
        tv.Ctx.Writer.CreateProject("Locked Check");
        Assert.False(File.Exists(LockPath(tv)));
        tv.Ctx.Writer.UpdateFrontmatter("Task - Ship v1", "status", "active");
        Assert.False(File.Exists(LockPath(tv)));
    }

    [Fact]
    public void AcquireIsReentrantWithinTheProcess()
    {
        using var tv = new TempVault();
        lock (tv.Ctx.Sync)
        {
            using (tv.Ctx.WriteLock.Acquire())
            using (tv.Ctx.WriteLock.Acquire())
            {
                Assert.True(File.Exists(LockPath(tv)));
            }
        }
        Assert.False(File.Exists(LockPath(tv)));
    }

    [Fact]
    public void SessionLifecycleWorksUnderTheLockRegime()
    {
        using var tv = new TempVault();
        var start = tv.Ctx.Sessions.StartBrief("Alpha", "lock regime check");
        Assert.True(start.LogNoteCreated);
        tv.Ctx.Sessions.End("Alpha", "done", "green", null);
        Assert.False(File.Exists(LockPath(tv)));
        Assert.Contains("- Tests: green", tv.ReadNote(start.LogNote));
    }
}
