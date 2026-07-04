using MindVault.Core;

namespace MindVault.Tests;

/// <summary>Copies the fixture vault into a unique temp folder and wires up a VaultContext.</summary>
public sealed class TempVault : IDisposable
{
    public string Root { get; }
    public VaultContext Ctx { get; private set; }

    public TempVault(bool useFixture = true, bool init = true, bool scan = true, string fixture = "SampleVault")
    {
        Root = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        if (useFixture) CopyDirectory(FixtureDirFor(fixture), Root);
        if (init) VaultStructure.EnsureStructure(Root);
        Ctx = CreateContext();
        if (scan) Ctx.Scanner.Scan();
    }

    public static string FixtureDir => FixtureDirFor("SampleVault");

    public static string FixtureDirFor(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", name);

    public VaultContext CreateContext() => CreateContextFor(Root);

    public static VaultContext CreateContextFor(string root) => VaultContext.Create(root, _ => null, root);

    public string Abs(string relativePath) =>
        Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public string ReadNote(string relativePath) => File.ReadAllText(Abs(relativePath));

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
    }

    public void Dispose()
    {
        Ctx.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException) { /* best effort; it's in %TEMP% */ }
        catch (UnauthorizedAccessException) { /* best effort */ }
    }
}
