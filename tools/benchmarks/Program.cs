using System.Diagnostics;
using MindVault.Core;

// MindVault benchmark harness. Generates a synthetic vault per size and measures the
// operations an agent actually pays for. Prints a Markdown table (paste into
// docs/PERFORMANCE_RESULTS.md). Usage:
//   dotnet run -c Release --project tools/benchmarks -- [--sizes 100,1000,10000] [--keep]
var sizes = new List<int> { 100, 1000, 10_000 };
var keep = false;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--sizes" when i + 1 < args.Length:
            sizes = args[++i].Split(',').Select(int.Parse).ToList();
            break;
        case "--keep":
            keep = true;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument '{args[i]}'. Usage: --sizes 100,1000 [--keep]");
            return 2;
    }
}

Console.WriteLine($"MindVault benchmarks (v{MindVaultVersion.Current}, {Environment.OSVersion}, " +
                  $".NET {Environment.Version}, {Environment.ProcessorCount} cores)");
Console.WriteLine();

foreach (var size in sizes)
{
    var projects = Math.Max(2, size / 100);
    var notesPerProject = Math.Max(5, size / projects);
    var root = Path.Combine(Path.GetTempPath(), "mindvault-bench", $"vault-{size}-{Guid.NewGuid():N}");

    Console.WriteLine($"## {size} notes ({projects} projects x {notesPerProject} notes)");
    var stats = FixtureVaultGenerator.Generate(root, projects, notesPerProject);
    Console.WriteLine($"generated {stats.TotalNotes} notes at {root}");

    var results = new List<(string Metric, string Value)>();
    using (var ctx = VaultContext.Create(root, _ => null, root))
    {
        // 1. cold scan (index does not exist yet)
        var sw = Stopwatch.StartNew();
        var cold = ctx.Scanner.Scan();
        sw.Stop();
        results.Add(("cold scan", $"{sw.ElapsedMilliseconds} ms ({cold.Added} notes)"));

        // 2. incremental scan (nothing changed)
        results.Add(("incremental scan", Med(() => ctx.Scanner.Scan(), 5)));

        // 3. search
        var queries = new[]
        {
            "cache layer", "auth service retry", "Decision SQLite", "harden sync engine", "telemetry",
        };
        results.Add(("search (ranked)", Med(() =>
        {
            foreach (var q in queries) ctx.Search.Search(q, limit: 10);
        }, 5, perOp: queries.Length)));

        // 4. project context
        results.Add(("project context", Med(() => ctx.Projects.Get("Genproj 01"), 5)));

        // 5. context pack (task-aware)
        results.Add(("context pack", Med(() => ctx.Packs.Get("Genproj 01", "harden the cache layer"), 5)));

        // 6. draft check
        results.Add(("draft check", Med(() => ctx.Drafts.CheckDraft("task", "Genproj 01", "Refactor cache layer again"), 5)));

        // 7/8. session start + end (mutations: snapshot + write lock + append)
        results.Add(("session start", Once(() => ctx.Sessions.Start("Genproj 01", "benchmark run"))));
        results.Add(("session end", Once(() => ctx.Sessions.End("Genproj 01", "benchmark handoff", "bench only", null))));

        // 9. validation
        var vsw = Stopwatch.StartNew();
        var report = ctx.Validator.Validate();
        vsw.Stop();
        results.Add(("validate", $"{vsw.ElapsedMilliseconds} ms " +
                                 $"({report.CriticalCount}c/{report.WarningCount}w/{report.InfoCount}i)"));

        // 10. index size (main DB + WAL: after a bulk-transaction scan the data may still
        // live in the -wal file until SQLite checkpoints it)
        var indexBytes = new[] { ctx.IndexFile, ctx.IndexFile + "-wal", ctx.IndexFile + "-shm" }
            .Where(File.Exists)
            .Sum(f => new FileInfo(f).Length);
        results.Add(("index size (db+wal)", $"{indexBytes / 1024:N0} KB"));

        // 11. memory
        results.Add(("managed heap", $"{GC.GetTotalMemory(forceFullCollection: true) / (1024 * 1024)} MB"));
        results.Add(("process working set", $"{Environment.WorkingSet / (1024 * 1024)} MB"));
    }

    Console.WriteLine();
    Console.WriteLine("| metric | result |");
    Console.WriteLine("| --- | --- |");
    foreach (var (metric, value) in results)
        Console.WriteLine($"| {metric} | {value} |");
    Console.WriteLine();

    if (!keep)
    {
        try { Directory.Delete(root, recursive: true); }
        catch (IOException) { Console.Error.WriteLine($"could not delete {root}"); }
    }
}

return 0;

// Median wall time of `runs` executions; perOp divides each run to report per-operation cost.
static string Med(Action action, int runs, int perOp = 1)
{
    var times = new List<double>();
    for (var i = 0; i < runs; i++)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        times.Add(sw.Elapsed.TotalMilliseconds / perOp);
    }
    times.Sort();
    return $"{times[times.Count / 2]:0.0} ms (median of {runs})";
}

static string Once(Action action)
{
    var sw = Stopwatch.StartNew();
    action();
    sw.Stop();
    return $"{sw.Elapsed.TotalMilliseconds:0.0} ms";
}
