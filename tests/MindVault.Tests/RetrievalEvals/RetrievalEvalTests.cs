using MindVault.Core;

namespace MindVault.Tests.RetrievalEvals;

/// <summary>
/// Retrieval evals: not "does search return something" but "does it return the RIGHT thing,
/// in the right order". Each case encodes a behaviour an agent depends on; a ranking
/// regression fails the build. See docs/RETRIEVAL_EVALS.md.
/// </summary>
public sealed class RetrievalEvalTests
{
    private static void WriteNote(TempVault tv, string path, string type, string status,
        string? project, string title, string body, string updated = "2026-01-01")
    {
        var projectLine = project is null ? "" : $"project: {project}\n";
        File.WriteAllText(tv.Abs(path), $"""
            ---
            type: {type}
            status: {status}
            {projectLine}created: 2026-01-01
            updated: {updated}
            tags: [eval]
            ---

            # {title}

            {body}
            """.Replace("\r\n", "\n"));
    }

    // 1. Exact decision title ranks first, even against a body-spam competitor.
    [Fact]
    public void ExactDecisionTitleRanksFirst()
    {
        using var tv = new TempVault();
        WriteNote(tv, "03_Resources/Spam.md", "research", "draft", null, "SQLite trivia collection",
            string.Join(" ", Enumerable.Repeat("use SQLite everywhere always", 30)));
        tv.Ctx.Scanner.Scan();

        var results = tv.Ctx.Search.Search("Use SQLite");
        Assert.Equal("04_Decisions/Decision - Use SQLite.md", results[0].Path);
    }

    // 2. A project-scoped task beats an unrelated global note for a project query.
    [Fact]
    public void ProjectScopedSearchPrefersProjectNotes()
    {
        using var tv = new TempVault();
        WriteNote(tv, "03_Resources/Global Shipping.md", "research", "draft", null,
            "Shipping industry notes", "ship ship ship shipping vessels at sea");
        tv.Ctx.Scanner.Scan();

        var results = tv.Ctx.Search.Search("ship", project: "Alpha");
        Assert.NotEmpty(results);
        Assert.Equal("01_Projects/Task - Ship v1.md", results[0].Path);
        Assert.DoesNotContain(results, r => r.Path == "03_Resources/Global Shipping.md");
        Assert.Null(results[0].Scope); // real project hits, not a fallback
    }

    // 2b. When the project has nothing, fall back vault-wide and SAY SO.
    [Fact]
    public void EmptyProjectScopeFallsBackVisibly()
    {
        using var tv = new TempVault();
        var results = tv.Ctx.Search.Search("cheatsheet", project: "Alpha");
        Assert.NotEmpty(results);
        Assert.Equal("global-fallback", results[0].Scope);
    }

    // 3. Archived notes stay out unless explicitly included.
    [Fact]
    public void ArchivedNotesExcludedUnlessRequested()
    {
        using var tv = new TempVault();
        WriteNote(tv, "99_Archive/Old Plan.md", "task", "archived", "Alpha",
            "Old evacuation plan", "zebrafish protocol details live here");
        tv.Ctx.Scanner.Scan();

        Assert.DoesNotContain(tv.Ctx.Search.Search("zebrafish"), r => r.Path == "99_Archive/Old Plan.md");
        var included = tv.Ctx.Search.Search("zebrafish", includeArchived: true);
        Assert.Contains(included, r => r.Path == "99_Archive/Old Plan.md");
    }

    // 4. A superseded decision never outranks its accepted replacement.
    [Fact]
    public void SupersededDecisionRanksBelowItsReplacement()
    {
        using var tv = new TempVault();
        WriteNote(tv, "04_Decisions/Decision - Cache in Redis.md", "decision", "superseded", "Alpha",
            "Cache in Redis", "Cache the session data in Redis. Superseded by the in-process cache decision.");
        WriteNote(tv, "04_Decisions/Decision - Cache in process.md", "decision", "accepted", "Alpha",
            "Cache in process", "Cache the session data in process memory instead of Redis.");
        tv.Ctx.Scanner.Scan();

        var results = tv.Ctx.Search.Search("cache session data");
        var accepted = results.FindIndex(r => r.Path.EndsWith("Cache in process.md"));
        var superseded = results.FindIndex(r => r.Path.EndsWith("Cache in Redis.md"));
        Assert.True(accepted >= 0 && superseded >= 0, "both decisions must be findable");
        Assert.True(accepted < superseded,
            $"accepted at {accepted} must outrank superseded at {superseded}");
    }

    // 5. An architecture query surfaces the architecture note first.
    [Fact]
    public void ArchitectureQuerySurfacesArchitectureNotes()
    {
        using var tv = new TempVault();
        WriteNote(tv, "02_Areas/Architecture - Sync pipeline.md", "architecture", "active", "Alpha",
            "Architecture - Sync pipeline", "Watcher -> queue -> sync pipeline -> SQLite store.");
        tv.Ctx.Scanner.Scan();

        var results = tv.Ctx.Search.Search("sync pipeline architecture");
        Assert.Equal("02_Areas/Architecture - Sync pipeline.md", results[0].Path);
    }

    // 6. A vague query returns bounded, non-empty candidates — not a dump, not nothing.
    [Fact]
    public void VagueQueryReturnsBoundedUsefulCandidates()
    {
        using var tv = new TempVault();
        var results = tv.Ctx.Search.Search("docs OR ship OR sqlite", limit: 5);
        Assert.InRange(results.Count, 1, 5);
    }

    // 7. A task-aware context pack surfaces the relevant decision.
    [Fact]
    public void ContextPackSurfacesTaskRelevantDecisions()
    {
        using var tv = new TempVault();
        var pack = tv.Ctx.Packs.Get("Alpha", "improve the sqlite search quality");
        Assert.Contains(pack.TaskRelevantNotes, n => n.Path == "04_Decisions/Decision - Use SQLite.md");
        Assert.Contains(pack.RelevantDecisions, d => d.Path == "04_Decisions/Decision - Use SQLite.md");
    }

    // 8. Broken links surface as validation warnings with the source path.
    [Fact]
    public void BrokenLinksAppearInValidationWarnings()
    {
        using var tv = new TempVault();
        WriteNote(tv, "03_Resources/Broken Linker.md", "research", "draft", null,
            "Broken Linker", "See [[Absolutely Missing Note]] for details.");
        tv.Ctx.Scanner.Scan();

        var report = tv.Ctx.Validator.Validate();
        Assert.Contains(report.Issues, i =>
            i.Code == "broken-link" && i.Severity == IssueSeverity.Warning &&
            i.Path == "03_Resources/Broken Linker.md" && i.Message.Contains("Absolutely Missing Note"));
    }

    // 9. Duplicate titles are flagged, and ambiguous refs refuse to guess.
    [Fact]
    public void DuplicateTitlesProduceAmbiguityProtection()
    {
        using var tv = new TempVault();
        var report = tv.Ctx.Validator.Validate();
        Assert.Contains(report.Issues, i => i.Code == "duplicate-title" && i.Message.Contains("Duplicate Note"));
        Assert.Throws<AmbiguousNoteRefException>(() => tv.Ctx.Resolver.Resolve("Duplicate Note"));
    }

    // 10. Stale open tasks surface in validation AND in project-context warnings.
    [Fact]
    public void StaleTasksSurfaceInValidationAndContextWarnings()
    {
        using var tv = new TempVault();
        WriteNote(tv, "01_Projects/Task - Forgotten chore.md", "task", "open", "Alpha",
            "Forgotten chore", "This was captured and then abandoned.",
            updated: DateTime.Today.AddDays(-120).ToString("yyyy-MM-dd"));
        tv.Ctx.Scanner.Scan();

        var report = tv.Ctx.Validator.Validate();
        Assert.Contains(report.Issues, i => i.Code == "stale-task" && i.Path == "01_Projects/Task - Forgotten chore.md");

        var context = tv.Ctx.Projects.Get("Alpha");
        Assert.Contains(context.Warnings, w => w.Contains("Forgotten chore") || w.Contains("stale", StringComparison.OrdinalIgnoreCase));
    }
}
