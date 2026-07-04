using MindVault.Core;

namespace MindVault.Tests;

/// <summary>The synthetic vault generator must be safe, deterministic and messy on purpose.</summary>
public sealed class FixtureGeneratorTests
{
    [Fact]
    public void RefusesNonEmptyDirectories()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "real-data.md"), "# precious");
        try
        {
            Assert.Throws<MindVaultException>(() => FixtureVaultGenerator.Generate(dir, 2, 10));
            Assert.True(File.Exists(Path.Combine(dir, "real-data.md")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void GeneratesAScannableVaultWithTheAdvertisedMessiness()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var fixedDay = new DateTime(2026, 7, 1);
            var stats = FixtureVaultGenerator.Generate(dir, 3, 40, seed: 42, today: fixedDay);
            Assert.Equal(3, stats.Projects);
            Assert.True(stats.Tasks > 0 && stats.Decisions > 0 && stats.Risks > 0);
            Assert.True(stats.SupersededDecisions > 0, "must contain superseded decision chains");
            Assert.True(stats.BrokenLinks > 0, "must contain broken links");
            Assert.True(stats.ArchivedNotes > 0, "must contain archived notes");
            Assert.True(stats.StaleTasks > 0, "must contain stale tasks");

            using var ctx = TempVault.CreateContextFor(dir);
            var scan = ctx.Scanner.Scan();
            Assert.Empty(scan.Errors);
            Assert.Equal(stats.TotalNotes, ctx.Db.CountNotes());

            // No invalid YAML: generated mess must be realistic, not corrupt.
            var report = ctx.Validator.Validate();
            Assert.DoesNotContain(report.Issues, i => i.Code is "invalid-yaml" or "nested-yaml");
            Assert.Contains(report.Issues, i => i.Code == "broken-link");
            Assert.Contains(report.Issues, i => i.Code == "stale-task");

            // Deterministic: same seed + same day → identical vault.
            var dir2 = dir + "-b";
            var stats2 = FixtureVaultGenerator.Generate(dir2, 3, 40, seed: 42, today: fixedDay);
            Assert.Equal(stats, stats2);
            var a = File.ReadAllText(Path.Combine(dir, "01_Projects", "Genproj 01.md"));
            var b = File.ReadAllText(Path.Combine(dir2, "01_Projects", "Genproj 01.md"));
            Assert.Equal(a, b);
            Directory.Delete(dir2, recursive: true);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
