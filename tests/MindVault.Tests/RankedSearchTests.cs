using MindVault.Core;

namespace MindVault.Tests;

public sealed class RankedSearchTests : IDisposable
{
    private readonly TempVault _tv = new(init: false);

    [Fact]
    public void ExactTitleMatchOutranksBodyMatches()
    {
        // "alpha" appears in several bodies; the note titled "Alpha" must win.
        var results = _tv.Ctx.Search.Search("alpha");
        Assert.Equal("01_Projects/Alpha.md", results[0].Path);
    }

    [Fact]
    public void ArchivedNotesAreExcludedByDefaultAndDeprioritisedWhenIncluded()
    {
        Directory.CreateDirectory(_tv.Abs("99_Archive"));
        File.WriteAllText(_tv.Abs("99_Archive/Old Plan.md"),
            "---\ntype: memory\nstatus: archived\ncreated: 2025-01-01\nupdated: 2025-01-01\ntags:\n  - memory\n---\n\n# Old Plan\n\nzzarchterm old direction\n");
        File.WriteAllText(_tv.Abs("00_Inbox/New Plan.md"),
            $"---\ntype: memory\nstatus: active\ncreated: 2026-07-01\nupdated: {DateTime.Now:yyyy-MM-dd}\ntags:\n  - memory\n---\n\n# New Plan\n\nzzarchterm new direction\n");
        _tv.Ctx.Scanner.Scan();

        var normal = _tv.Ctx.Search.Search("zzarchterm");
        Assert.Single(normal);
        Assert.Equal("00_Inbox/New Plan.md", normal[0].Path);

        var withArchived = _tv.Ctx.Search.Search("zzarchterm", includeArchived: true);
        Assert.Equal(2, withArchived.Count);
        Assert.Equal("00_Inbox/New Plan.md", withArchived[0].Path); // archived ranks below
    }

    [Fact]
    public void UpdatedDateFiltersNarrowResults()
    {
        var recent = _tv.Ctx.Search.Search("SQLite", updatedAfter: "2026-03-01");
        Assert.All(recent, r => Assert.True(string.CompareOrdinal("2026-03-01", "2026-03-01") <= 0));
        Assert.DoesNotContain(recent, r => r.Path == "04_Decisions/Decision - Use SQLite.md"); // updated 2026-02-01

        var old = _tv.Ctx.Search.Search("SQLite", updatedBefore: "2026-02-15");
        Assert.Contains(old, r => r.Path == "04_Decisions/Decision - Use SQLite.md");

        Assert.Throws<MindVaultException>(() => _tv.Ctx.Search.Search("x", updatedAfter: "not-a-date"));
    }

    [Fact]
    public void ProjectScopeFallsBackGloballyWhenEmpty()
    {
        // "bm25" only exists in the cheatsheet, which has no project.
        var results = _tv.Ctx.Search.Search("bm25", project: "Alpha");
        Assert.NotEmpty(results);
        Assert.Equal("global-fallback", results[0].Scope);
        Assert.Contains(results, r => r.Path == "03_Resources/SQLite Cheatsheet.md");

        // A query that matches inside the project stays scoped (no fallback marker).
        var scoped = _tv.Ctx.Search.Search("SQLite", project: "Alpha");
        Assert.All(scoped, r => Assert.Null(r.Scope));
    }

    [Fact]
    public void ExplainReportsRankingFactors()
    {
        var results = _tv.Ctx.Search.Search("alpha", explain: true);
        Assert.NotNull(results[0].Why);
        Assert.Contains(results[0].Why!, w => w.StartsWith("bm25"));
        Assert.Contains(results[0].Why!, w => w.Contains("exact-title"));

        var noExplain = _tv.Ctx.Search.Search("alpha");
        Assert.Null(noExplain[0].Why);
    }

    [Fact]
    public void MatchedSectionIsReported()
    {
        var results = _tv.Ctx.Search.Search("rebuildable cache");
        var hit = results.First(r => r.Path == "04_Decisions/Decision - Use SQLite.md");
        Assert.Equal("Context", hit.Section);
    }

    public void Dispose() => _tv.Dispose();
}
