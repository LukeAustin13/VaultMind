using MindVault.Core;

namespace MindVault.Tests;

/// <summary>
/// Light smoke test for the shared coordination lock (F-A / F-C): many parallel reads/scans on a
/// single shared VaultContext must not throw or corrupt the index.
/// </summary>
public sealed class ConcurrencyTests : IDisposable
{
    private readonly TempVault _tv = new();

    [Fact]
    public void ParallelEnsureFreshAndSearchDoNotThrow()
    {
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 64, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i =>
        {
            try
            {
                _tv.Ctx.Scanner.EnsureFresh();
                _ = _tv.Ctx.Search.Search("alpha");
                _ = _tv.Ctx.Search.List(null, null, null, null, 10);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        Assert.Empty(exceptions);
        Assert.True(_tv.Ctx.Db.CountNotes() > 0);
    }

    public void Dispose() => _tv.Dispose();
}
