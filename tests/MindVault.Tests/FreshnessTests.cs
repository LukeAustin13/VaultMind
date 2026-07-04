using Microsoft.Data.Sqlite;
using MindVault.Core;

namespace MindVault.Tests;

public sealed class FreshnessTests
{
    [Fact]
    public void StaleIndexIsRefreshedIncrementallyOnQuery()
    {
        using var tv = new TempVault(init: false);
        File.WriteAllText(tv.Abs("00_Inbox/Fresh Note.md"), "# Fresh Note\n\nttlfreshterm\n");
        // Age the last scan past the default 60s staleness window.
        tv.Ctx.State.Save(new VaultState { LastScanUtc = DateTime.UtcNow.AddHours(-1), NoteCount = 14 });

        var hits = tv.Ctx.Search.Search("ttlfreshterm");
        Assert.Single(hits);
        Assert.Equal("00_Inbox/Fresh Note.md", hits[0].Path);
    }

    [Fact]
    public void FreshIndexIsNotRescannedOnQuery()
    {
        using var tv = new TempVault(init: false);
        File.WriteAllText(tv.Abs("00_Inbox/Fresh Note.md"), "# Fresh Note\n\nttlfreshterm\n");
        // Last scan is current, so the new file must not be picked up by a query.
        Assert.Empty(tv.Ctx.Search.Search("ttlfreshterm"));
    }

    [Fact]
    public void ZeroStalenessDisablesAutoRefresh()
    {
        using var tv = new TempVault(init: false);
        using var ctx = new VaultContext(new LoadedConfig(
            new MindVaultConfig { VaultPath = tv.Root, ScanStalenessSeconds = 0 }, "test", null));
        File.WriteAllText(tv.Abs("00_Inbox/Fresh Note.md"), "# Fresh Note\n\nttlfreshterm\n");
        ctx.State.Save(new VaultState { LastScanUtc = DateTime.UtcNow.AddHours(-1), NoteCount = 14 });

        Assert.Empty(ctx.Search.Search("ttlfreshterm"));
    }

    [Fact]
    public void OutdatedSchemaVersionTriggersResetAndRescan()
    {
        using var tv = new TempVault(init: false);
        var indexFile = tv.Ctx.IndexFile;
        tv.Ctx.Dispose(); // release the connection so we can tamper with the file

        using (var conn = new SqliteConnection($"Data Source={indexFile};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA user_version = 1;";
            cmd.ExecuteNonQuery();
        }

        using var ctx = TempVault.CreateContextFor(tv.Root);
        Assert.True(ctx.Db.NeedsRescan);
        // A query must transparently repopulate the reset index.
        Assert.NotEmpty(ctx.Search.Search("SQLite"));
        Assert.Equal(14, ctx.Db.CountNotes());
        Assert.False(ctx.Db.NeedsRescan);
    }

    [Fact]
    public void PorterStemmingMatchesWordVariants()
    {
        using var tv = new TempVault(init: false);
        // Fixture text says "full text search"; a stemmed query variant must still match.
        var hits = tv.Ctx.Search.Search("searching");
        Assert.Contains(hits, h => h.Path == "01_Projects/Alpha.md");
    }
}
