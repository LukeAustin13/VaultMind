using MindVault.Core;

namespace MindVault.Tests;

public sealed class SearchTests : IDisposable
{
    private readonly TempVault _tv = new(init: false);

    [Fact]
    public void FtsFindsContentAcrossNotes()
    {
        var results = _tv.Ctx.Search.Search("full text search");
        Assert.Contains(results, r => r.Path == "03_Resources/SQLite Cheatsheet.md");
        Assert.Contains(results, r => r.Path == "01_Projects/Alpha.md");
    }

    [Fact]
    public void ResultsIncludeSnippetAndMetadata()
    {
        var results = _tv.Ctx.Search.Search("bm25 ranking");
        var hit = Assert.Single(results);
        Assert.Equal("SQLite Cheatsheet", hit.Title);
        Assert.Equal("research", hit.Type);
        Assert.Contains("**bm25**", hit.Snippet);
    }

    [Fact]
    public void TypeFilterNarrowsResults()
    {
        var results = _tv.Ctx.Search.Search("SQLite", type: "decision");
        Assert.NotEmpty(results);
        Assert.All(results, r => Assert.Equal("decision", r.Type));
    }

    [Fact]
    public void ProjectFilterNarrowsResults()
    {
        var results = _tv.Ctx.Search.Search("SQLite", project: "Alpha");
        var hit = Assert.Single(results);
        Assert.Equal("04_Decisions/Decision - Use SQLite.md", hit.Path);
    }

    [Fact]
    public void TagFilterNarrowsResults()
    {
        Assert.NotEmpty(_tv.Ctx.Search.Search("ranking", tag: "sqlite"));
        Assert.Empty(_tv.Ctx.Search.Search("ranking", tag: "no-such-tag"));
    }

    [Fact]
    public void StatusFilterNarrowsResults()
    {
        var results = _tv.Ctx.Search.Search("docs", status: "done");
        Assert.All(results, r => Assert.Equal("done", r.Status));
    }

    [Fact]
    public void LimitIsRespected()
    {
        var results = _tv.Ctx.Search.Search("the", limit: 2);
        Assert.True(results.Count <= 2);
    }

    [Fact]
    public void InvalidFtsSyntaxFallsBackToPhraseSearch()
    {
        // Unbalanced quote is invalid FTS5 syntax; it must not throw.
        var results = _tv.Ctx.Search.Search("\"full text");
        Assert.NotNull(results);
    }

    [Fact]
    public void EmptyQueryIsRejected()
    {
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Search.Search("   "));
    }

    public void Dispose() => _tv.Dispose();
}
