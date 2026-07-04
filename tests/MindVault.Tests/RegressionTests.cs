using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Focused regressions for the code-review fixes (F-B, F-D, F-L, F-K).</summary>
public sealed class RegressionTests
{
    // ---------- F-B: frontmatter keys that need quoting survive a round-trip ----------

    [Fact]
    public void FrontmatterKeyNeedingQuotingSurvivesRoundTrip()
    {
        var source = "---\n\"#tagkey\": important-value\ntype: project\n---\n\n# N\n";
        var parsed = NoteParser.Parse(source, "n.md");
        Assert.Null(parsed.ParseError);
        Assert.Contains(parsed.FrontmatterEntries, e => e.Key == "#tagkey");

        // Emulate an Archive/UpdateFrontmatter-style rewrite: mutate one key, re-serialize, re-parse.
        var fm = new Frontmatter();
        foreach (var e in parsed.FrontmatterEntries) fm.Entries.Add(e);
        fm.SetScalar("status", "archived");

        var rebuilt = FrontmatterCodec.BuildDocument(fm, "\n# N\n");
        var reparsed = NoteParser.Parse(rebuilt, "n.md");

        Assert.Null(reparsed.ParseError);
        var entry = Assert.Single(reparsed.FrontmatterEntries, e => e.Key == "#tagkey");
        Assert.Equal("important-value", entry.Scalar);
    }

    [Fact]
    public void FrontmatterKeyWithColonSurvivesRoundTrip()
    {
        var parsed = NoteParser.Parse("---\n\"weird: key\": v1\n---\n\n# N\n", "n.md");
        Assert.Null(parsed.ParseError);

        var rebuilt = FrontmatterCodec.BuildDocument(parsed.FrontmatterEntries.ToFrontmatter(), "\n# N\n");
        var reparsed = NoteParser.Parse(rebuilt, "n.md");

        Assert.Null(reparsed.ParseError);
        var entry = Assert.Single(reparsed.FrontmatterEntries, e => e.Key == "weird: key");
        Assert.Equal("v1", entry.Scalar);
    }

    // ---------- F-D: carriage returns ----------

    [Fact]
    public void MidValueCarriageReturnRoundTripsWithoutParseError()
    {
        var fm = new Frontmatter();
        fm.SetScalar("note", "before\rafter");
        var doc = FrontmatterCodec.BuildDocument(fm, "\n# N\n");
        var parsed = NoteParser.Parse(doc, "n.md");
        Assert.Null(parsed.ParseError);
    }

    [Fact]
    public void CarriageReturnOnlyNoteSurfacesFrontmatter()
    {
        var note = NoteParser.Parse("---\rtype: idea\rstatus: open\r---\r\r# Body\r", "n.md");
        Assert.True(note.HasFrontmatter);
        Assert.Null(note.ParseError);
        Assert.Equal("idea", note.Type);
        Assert.Equal("open", note.Status);
    }

    [Fact]
    public void ComputeBodyHashMatchesParsedBodyHash()
    {
        const string raw = "---\ntype: idea\r\n---\r\n\r\n# Body\rextra\n";
        Assert.Equal(NoteParser.Parse(raw, "n.md").BodyHash, NoteParser.ComputeBodyHash(raw));
    }

    // ---------- F-L: anchor-only wiki link is not a tag ----------

    [Fact]
    public void AnchorOnlyWikiLinkDoesNotProduceTag()
    {
        var note = NoteParser.Parse("# N\n\n[[#Overview]]\n", "n.md");
        Assert.DoesNotContain("Overview", note.Tags);
    }

    [Fact]
    public void NormalInlineTagsStillMatch()
    {
        var note = NoteParser.Parse("# N\n\n#alpha and text #beta\n", "n.md");
        Assert.Contains("alpha", note.Tags);
        Assert.Contains("beta", note.Tags);
    }
}

internal static class FrontmatterTestExtensions
{
    public static Frontmatter ToFrontmatter(this List<FrontmatterEntry> entries)
    {
        var fm = new Frontmatter();
        foreach (var e in entries) fm.Entries.Add(e);
        return fm;
    }
}
