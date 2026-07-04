using MindVault.Core;

namespace MindVault.Tests;

public sealed class NoteParserTests
{
    [Fact]
    public void ParsesFlatFrontmatter()
    {
        var note = NoteParser.Parse("""
            ---
            type: task
            status: open
            created: 2026-01-01
            updated: 2026-01-02
            project: Alpha
            tags:
              - task
              - urgent
            links:
              - "[[Alpha]]"
            ---

            # Task: Demo
            """, "01_Projects/Task - Demo.md");

        Assert.True(note.HasFrontmatter);
        Assert.Null(note.ParseError);
        Assert.Equal("task", note.Type);
        Assert.Equal("open", note.Status);
        Assert.Equal("Alpha", note.Project);
        Assert.Equal("2026-01-01", note.Created);
        Assert.Equal("2026-01-02", note.Updated);
        Assert.Equal(["task", "urgent"], note.Tags);
        Assert.Contains(note.Links, l => l.TargetNorm == "alpha");
        Assert.Equal("Task: Demo", note.Title);
        Assert.Equal("Task - Demo", note.Stem);
    }

    [Fact]
    public void InvalidYamlIsReportedNotThrown()
    {
        var note = NoteParser.Parse("---\ntype: memory\nstatus: [unclosed\n---\n\n# X\n", "00_Inbox/X.md");
        Assert.NotNull(note.ParseError);
        Assert.StartsWith("yaml-invalid", note.ParseError);
    }

    [Fact]
    public void NestedYamlIsRejectedButFlatKeysSurvive()
    {
        var note = NoteParser.Parse("""
            ---
            type: memory
            status: open
            meta:
              nested: true
            ---

            # X
            """, "00_Inbox/X.md");
        Assert.StartsWith("yaml-nested", note.ParseError);
        Assert.Equal("memory", note.Type);
        Assert.Equal("open", note.Status);
    }

    [Fact]
    public void ExtractsHeadingsWithLevels()
    {
        var note = NoteParser.Parse("# Top\n\ntext\n\n## Second\n\n### Third ##\n", "n.md");
        Assert.Equal(3, note.Headings.Count);
        Assert.Equal((1, "Top"), (note.Headings[0].Level, note.Headings[0].Text));
        Assert.Equal((2, "Second"), (note.Headings[1].Level, note.Headings[1].Text));
        Assert.Equal((3, "Third"), (note.Headings[2].Level, note.Headings[2].Text));
    }

    [Fact]
    public void HeadingsInsideCodeFencesAreIgnored()
    {
        var note = NoteParser.Parse("# Real\n\n```\n# not a heading\n```\n", "n.md");
        Assert.Single(note.Headings);
        Assert.Equal("Real", note.Headings[0].Text);
    }

    [Fact]
    public void WikiLinksSupportAliasAndSection()
    {
        var note = NoteParser.Parse("# N\n\nSee [[Target|the target]] and [[Other#Section]].\n", "n.md");
        Assert.Equal(2, note.Links.Count);
        Assert.Contains(note.Links, l => l.Target == "Target");
        Assert.Contains(note.Links, l => l.Target == "Other");
    }

    [Fact]
    public void LinksAndTagsInsideCodeBlocksAreMasked()
    {
        var note = NoteParser.Parse(
            "# N\n\nReal #realtag and [[Real Link]].\n\n```sql\nSELECT 1; -- #notatag [[Not A Link]]\n```\n",
            "n.md");
        Assert.Contains("realtag", note.Tags);
        Assert.DoesNotContain("notatag", note.Tags);
        Assert.Single(note.Links);
        Assert.Equal("Real Link", note.Links[0].Target);
    }

    [Fact]
    public void FrontmatterAndInlineTagsAreMerged()
    {
        var note = NoteParser.Parse("---\ntags:\n  - alpha\n---\n\n# N\n\nBody #beta #alpha\n", "n.md");
        Assert.Equal(["alpha", "beta"], note.Tags);
    }

    [Fact]
    public void TitleFallsBackToFilenameStem()
    {
        var note = NoteParser.Parse("no headings here\n", "03_Resources/Plain Note.md");
        Assert.Equal("Plain Note", note.Title);
    }
}
