using MindVault.Core;

namespace MindVault.Tests;

public sealed class TemplateExclusionTests : IDisposable
{
    private readonly TempVault _tv = new(); // init: templates exist and are indexed

    [Fact]
    public void TemplateTitlesDoNotResolveAsNotes()
    {
        // 08_Templates/Project.md is titled "Project Name" but must not shadow real notes.
        Assert.Throws<NoteNotFoundException>(() => _tv.Ctx.Resolver.Resolve("Project Name"));
    }

    [Fact]
    public void TemplatesAreStillReachableByExactPath()
    {
        Assert.Equal("08_Templates/Project.md",
            _tv.Ctx.Resolver.Resolve("08_Templates/Project.md").Path);
    }

    [Fact]
    public void TemplateProjectNoteCannotBeUsedAsAProject()
    {
        var ex = Assert.Throws<MindVaultException>(() => _tv.Ctx.Writer.CreateTask("Project Name", "X"));
        Assert.Contains("Project not found", ex.Message);
        Assert.Throws<MindVaultException>(() => _tv.Ctx.Projects.Get("Project Name"));
    }

    [Fact]
    public void RealNoteWithTemplateLikeNameStillResolves()
    {
        _tv.Ctx.Writer.CreateProject("Project Name");
        Assert.Equal("01_Projects/Project Name.md", _tv.Ctx.Resolver.Resolve("Project Name").Path);
        Assert.Equal("01_Projects/Project Name.md", _tv.Ctx.Writer.FindProject("Project Name").Path);
    }

    public void Dispose() => _tv.Dispose();
}
