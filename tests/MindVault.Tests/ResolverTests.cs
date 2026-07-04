using MindVault.Core;

namespace MindVault.Tests;

public sealed class ResolverTests : IDisposable
{
    private readonly TempVault _tv = new(init: false);

    [Theory]
    [InlineData("01_Projects/Alpha.md")]
    [InlineData("01_Projects/Alpha")]
    [InlineData(@"01_Projects\Alpha.md")]
    [InlineData("Alpha")]
    [InlineData("aLpHa")]
    [InlineData("[[Alpha]]")]
    [InlineData("[[Alpha|the alpha project]]")]
    [InlineData("[[Alpha#Goal]]")]
    [InlineData("alpha")]
    public void ResolvesProjectNoteByAnyReferenceForm(string noteRef)
    {
        Assert.Equal("01_Projects/Alpha.md", _tv.Ctx.Resolver.Resolve(noteRef).Path);
    }

    [Fact]
    public void ResolvesByFilenameStem()
    {
        Assert.Equal("04_Decisions/Decision - Use SQLite.md",
            _tv.Ctx.Resolver.Resolve("Decision - Use SQLite").Path);
    }

    [Fact]
    public void ResolvesBySlugOfTitle()
    {
        Assert.Equal("04_Decisions/Decision - Use SQLite.md",
            _tv.Ctx.Resolver.Resolve("decision-use-sqlite").Path);
    }

    [Fact]
    public void AmbiguousReferenceListsCandidatesInsteadOfGuessing()
    {
        var ex = Assert.Throws<AmbiguousNoteRefException>(() => _tv.Ctx.Resolver.Resolve("Duplicate Note"));
        Assert.Equal(2, ex.Candidates.Count);
        Assert.Contains("02_Areas/Duplicate Note.md", ex.Candidates);
        Assert.Contains("03_Resources/Duplicate Note.md", ex.Candidates);
    }

    [Fact]
    public void UnknownReferenceThrowsNotFound()
    {
        Assert.Throws<NoteNotFoundException>(() => _tv.Ctx.Resolver.Resolve("No Such Note Anywhere"));
    }

    [Fact]
    public void TraversalReferenceCannotEscapeVault()
    {
        // A real file one level above the vault must not be reachable through the resolver.
        var outside = Path.Combine(Path.GetDirectoryName(_tv.Root)!, "outside-secret.md");
        File.WriteAllText(outside, "# secret\n");
        try
        {
            Assert.Throws<NoteNotFoundException>(() => _tv.Ctx.Resolver.Resolve("../outside-secret.md"));
            Assert.Throws<NoteNotFoundException>(() => _tv.Ctx.Resolver.Resolve(@"..\outside-secret.md"));
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void OperationalFolderNotesAreNotResolvableEvenWhenTheyExist()
    {
        // A real .md file inside an operational folder must not be readable/mutable via the resolver.
        var snapDir = Path.Combine(_tv.Root, ".mindvault", "snapshots", "2026-07-04");
        Directory.CreateDirectory(snapDir);
        var snap = Path.Combine(snapDir, "20260704-101010123-alpha.md");
        File.WriteAllText(snap, "# snapshot\n");

        var obsidianDir = Path.Combine(_tv.Root, ".obsidian");
        Directory.CreateDirectory(obsidianDir);
        File.WriteAllText(Path.Combine(obsidianDir, "x.md"), "# config\n");

        Assert.Throws<NoteNotFoundException>(() =>
            _tv.Ctx.Resolver.Resolve(".mindvault/snapshots/2026-07-04/20260704-101010123-alpha.md"));
        Assert.Throws<NoteNotFoundException>(() => _tv.Ctx.Resolver.Resolve(".obsidian/x.md"));
    }

    [Fact]
    public void AllPunctuationTitleDoesNotCauseCrossNoteSlugResolution()
    {
        // A note whose H1 is all punctuation slugs to "". An all-punctuation reference must not
        // resolve to it via the empty-slug lookup.
        File.WriteAllText(Path.Combine(_tv.Root, "03_Resources", "Dots.md"), "# ...\n");
        _tv.Ctx.Scanner.Scan();
        Assert.Throws<NoteNotFoundException>(() => _tv.Ctx.Resolver.Resolve("!!!"));
    }

    public void Dispose() => _tv.Dispose();
}
