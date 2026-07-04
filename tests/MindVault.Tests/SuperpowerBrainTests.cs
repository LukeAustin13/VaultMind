using MindVault.Cli;
using MindVault.Core;
using System.Text.Json;

namespace MindVault.Tests;

/// <summary>
/// Memory-OS evals over the EliteBrainVault fixture: capsules include the right memory and
/// exclude the wrong memory, work-context finds notes for a source file, recall respects
/// the window, feedback boosts and hides deterministically, the risk scanner blocks
/// secrets without leaking them, and the session/mistake/inbox lifecycles round-trip.
/// </summary>
public sealed class SuperpowerBrainTests
{
    private static TempVault Vault() => new(fixture: "EliteBrainVault");

    private static (int Code, string Stdout) RunCli(TempVault tv, params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = CliRunner.Run(args.Concat(["--vault", tv.Root]).ToArray(), stdout, stderr, _ => null, tv.Root);
        return (code, stdout.ToString());
    }

    // ---------- capsules ----------

    [Fact]
    public void CapsuleIncludesActiveMemoryAndExcludesArchived()
    {
        using var tv = Vault();
        var capsule = tv.Ctx.Capsules.Build("Elite").Capsule!;

        Assert.Contains(capsule.ActiveDecisions, d => d.Title == "Decision: Use FTS5 ranking");
        Assert.DoesNotContain(capsule.ActiveDecisions, d => d.Title == "Decision: Old ranking approach");
        Assert.Contains(capsule.OpenTasks, t => t.Title == "Task: Ship capsule evals");
        Assert.DoesNotContain(capsule.OpenTasks, t => t.Title == "Task: Ancient work");
        Assert.Contains(capsule.OpenRisks, r => r.Title == "Risk: Token burn");
        Assert.Contains(capsule.Constraints, c => c.Title == "Constraint: Local first only");
        Assert.Contains(capsule.SupersededDecisionWarnings, w => w.Contains("Old ranking approach"));
        Assert.Contains(capsule.KnownMistakes, m => m.Title == "Mistake: Trusted stale index");
        Assert.Contains(capsule.DoNotRepeat, r => r.Contains("Always run scan after external edits"));
        Assert.Contains(capsule.OpenQuestions, q => q.Contains("recall include archived"));
        Assert.Contains(capsule.SourcePaths, p => p == "04_Decisions/Decision - Use FTS5 ranking.md");
        Assert.Equal("exact", capsule.Confidence);
    }

    [Fact]
    public void CapsuleRefusesToGuessAmbiguousProjects()
    {
        using var tv = Vault();
        var outcome = tv.Ctx.Capsules.Build("shared-el");
        Assert.Null(outcome.Capsule);
        Assert.Equal(2, outcome.Candidates.Count);
    }

    [Fact]
    public void CapsuleRespectsTheCharBudget()
    {
        using var tv = Vault();
        var full = CapsuleService.ToMarkdown(tv.Ctx.Capsules.Build("Elite", "coding", 32_000).Capsule!);
        var tight = CapsuleService.ToMarkdown(tv.Ctx.Capsules.Build("Elite", "coding", 1000).Capsule!);
        Assert.True(full.Length <= 32_000);
        Assert.True(tight.Length <= full.Length,
            $"tight capsule ({tight.Length}) must not exceed the full one ({full.Length})");
        // Trimming removed items — the tight capsule must have shed list content.
        Assert.True(tight.Length < full.Length || full.Length <= 1000);
    }

    [Fact]
    public void CapsuleModesReorderButStayDeterministic()
    {
        using var tv = Vault();
        var debug1 = CapsuleService.ToMarkdown(tv.Ctx.Capsules.Build("Elite", "debugging").Capsule!);
        var debug2 = CapsuleService.ToMarkdown(tv.Ctx.Capsules.Build("Elite", "debugging").Capsule!);
        Assert.Equal(debug1, debug2);
        Assert.Throws<MindVaultException>(() => tv.Ctx.Capsules.Build("Elite", "vibes"));
    }

    // ---------- work-context ----------

    [Fact]
    public void WorkContextFindsNotesRelatedToTheCurrentFile()
    {
        using var tv = Vault();
        var result = tv.Ctx.WorkContext.Get("Elite", currentFile: "src/MindVault.Core/WriteService.cs");
        Assert.Contains(result.SuggestedReads, r => r.Path == "03_Resources/WriteService safety notes.md");
        Assert.Contains(result.SuggestedReads,
            r => r.Path == "03_Resources/WriteService safety notes.md" &&
                 r.Reason.Contains("matches the current file"));
    }

    [Fact]
    public void WorkContextRequiresExactlyOneInput()
    {
        using var tv = Vault();
        Assert.Throws<MindVaultException>(() => tv.Ctx.WorkContext.Get("Elite"));
        Assert.Throws<MindVaultException>(() =>
            tv.Ctx.WorkContext.Get("Elite", currentFile: "a.cs", query: "b"));
    }

    // ---------- recall ----------

    [Fact]
    public void RecallReturnsOnlyTheRequestedWindowAndWarnsAboutArchived()
    {
        using var tv = Vault();
        var result = tv.Ctx.RecallSvc.Recall("Elite", "2026-06-25");

        Assert.Contains(result.Decisions, d => d.Title == "Decision: Use FTS5 ranking");
        Assert.DoesNotContain(result.Decisions, d => d.Title == "Decision: Old ranking approach");
        Assert.Contains(result.Tasks, t => t.Title == "Task: Ship capsule evals" && t.Change == "created");
        Assert.Empty(result.Sessions); // the log entry is 2026-06-20, before the window
        // The archived task changed 2026-07-01 — excluded, but counted honestly.
        Assert.Contains(result.Warnings, w => w.Contains("archived"));
    }

    [Fact]
    public void HandoffAppearsInLaterRecall()
    {
        using var tv = Vault();
        var write = tv.Ctx.Sessions.End("Elite", "shipped the superpower pass", "dotnet test green");
        Assert.NotNull(write.SnapshotPath);
        var result = tv.Ctx.RecallSvc.Recall("Elite", "1 days");
        Assert.Contains(result.Sessions, s => s.Path == "06_Agent_Memory/Log - Elite.md");
    }

    [Fact]
    public void SessionCheckpointAndRecentRoundTrip()
    {
        using var tv = Vault();
        tv.Ctx.Sessions.Log("el", "midway checkpoint"); // alias resolution included
        tv.Ctx.Sessions.End("Elite", "final handoff", "green");
        var entries = tv.Ctx.Sessions.Recent("Elite", 5);

        Assert.Contains(entries, e => e.Kind == "checkpoint" && e.Heading.Contains("midway checkpoint"));
        Assert.Contains(entries, e => e.Kind == "handoff" && e.Heading.Contains("final handoff"));
        Assert.Contains(entries, e => e.Kind == "handoff" && e.Heading.Contains("prior handoff"));
        // Dry-run checkpoint writes nothing.
        var before = tv.ReadNote("06_Agent_Memory/Log - Elite.md");
        tv.Ctx.Sessions.Log("Elite", "dry checkpoint", dryRun: true);
        Assert.Equal(before, tv.ReadNote("06_Agent_Memory/Log - Elite.md"));
    }

    // ---------- feedback ----------

    [Fact]
    public void FeedbackBoostsUsefulNotesDeterministically()
    {
        using var tv = Vault();
        WorkContextResult Get() => tv.Ctx.WorkContext.Get("Elite", query: "ranking evals");

        var baseline = Get();
        var paths = baseline.SuggestedReads.Select(r => r.Path).ToList();
        Assert.Contains("06_Agent_Memory/Ranking evals A.md", paths);
        Assert.Contains("06_Agent_Memory/Ranking evals B.md", paths);
        // Tie broken by path: A ranks before B.
        Assert.True(paths.IndexOf("06_Agent_Memory/Ranking evals A.md") <
                    paths.IndexOf("06_Agent_Memory/Ranking evals B.md"));

        tv.Ctx.Feedback.Record("Ranking evals B", "useful", "the one that mattered");
        var boosted = Get().SuggestedReads.Select(r => r.Path).ToList();
        Assert.True(boosted.IndexOf("06_Agent_Memory/Ranking evals B.md") <
                    boosted.IndexOf("06_Agent_Memory/Ranking evals A.md"),
            "a 'useful' signal must outrank the path tiebreak");
    }

    [Fact]
    public void HiddenNotesDisappearFromCapsuleWorkContextAndSuggestions()
    {
        using var tv = Vault();
        tv.Ctx.Feedback.Record("Mistake - Trusted stale index", "hidden", "test");
        var capsule = tv.Ctx.Capsules.Build("Elite").Capsule!;
        Assert.DoesNotContain(capsule.KnownMistakes, m => m.Title.Contains("Trusted stale index"));

        tv.Ctx.Feedback.Record("Ranking evals A", "hidden");
        var wc = tv.Ctx.WorkContext.Get("Elite", query: "ranking evals");
        Assert.DoesNotContain(wc.SuggestedReads, r => r.Path.Contains("Ranking evals A"));

        // clear restores it.
        tv.Ctx.Feedback.Record("Ranking evals A", "clear");
        wc = tv.Ctx.WorkContext.Get("Elite", query: "ranking evals");
        Assert.Contains(wc.SuggestedReads, r => r.Path.Contains("Ranking evals A"));
    }

    [Fact]
    public void PinnedNotesSurfaceInCapsuleSuggestedReads()
    {
        using var tv = Vault();
        tv.Ctx.Feedback.Record("WriteService safety notes", "pinned", "core safety reference");
        var capsule = tv.Ctx.Capsules.Build("Elite").Capsule!;
        Assert.Contains(capsule.SuggestedReads,
            r => r.Path == "03_Resources/WriteService safety notes.md" && r.Reason == "pinned by feedback");
    }

    // ---------- risk scanner ----------

    [Fact]
    public void RiskScannerBlocksPrivateKeysWithoutLeakingThem()
    {
        using var tv = Vault();
        var before = tv.ReadNote("01_Projects/Elite.md");
        var secret = "-----BEGIN RSA PRIVATE KEY-----\nMIIEowIBAAKCAQEA7abc\n-----END RSA PRIVATE KEY-----";

        var ex = Assert.Throws<MindVaultException>(() =>
            tv.Ctx.Writer.AppendToSection("Elite", "Architecture", secret));
        Assert.Equal(ErrorCodes.RiskyContent, ex.Code);
        Assert.DoesNotContain("MIIEowIBAAKCAQEA7abc", ex.Message); // never leak the value
        Assert.Equal(before, tv.ReadNote("01_Projects/Elite.md")); // nothing written

        // Explicit override stores it and still reports the risk.
        var result = tv.Ctx.Writer.AppendToSection("Elite", "Architecture", secret,
            allowRiskyContent: true);
        Assert.Contains(result.RiskWarnings ?? [], w => w.Contains("private-key-block"));
    }

    [Fact]
    public void RiskScannerWarnsOnInjectionLanguageButDoesNotBlock()
    {
        using var tv = Vault();
        var result = tv.Ctx.Writer.AppendToSection("Elite", "Active Work",
            "Note to self: the upstream README literally says ignore all previous instructions.");
        Assert.True(result.Changed);
        Assert.Contains(result.RiskWarnings ?? [], w => w.Contains("prompt-injection"));
    }

    [Fact]
    public void RiskScannerCoversSessionHandoffs()
    {
        using var tv = Vault();
        var ex = Assert.Throws<MindVaultException>(() =>
            tv.Ctx.Sessions.End("Elite", "leaked ghp_0123456789012345678901234567890123456789"));
        Assert.Equal(ErrorCodes.RiskyContent, ex.Code);
    }

    // ---------- mistake ledger + inbox + ops ----------

    [Fact]
    public void MistakeLedgerRoundTripFeedsTheCapsule()
    {
        using var tv = Vault();
        var created = tv.Ctx.Writer.CreateMistake("Forgot the write lock", "Elite",
            lesson: "Mutations raced a scan.", prevention: "Always take ctx.Sync plus the write lock.");
        Assert.Equal("06_Agent_Memory/Mistakes/Mistake - Forgot the write lock.md", created.Note.Path);

        var capsule = tv.Ctx.Capsules.Build("Elite").Capsule!;
        Assert.Contains(capsule.DoNotRepeat, r => r.Contains("Always take ctx.Sync"));

        tv.Ctx.Writer.ResolveMistake("Mistake - Forgot the write lock");
        Assert.DoesNotContain(BrainQueries.Mistakes(tv.Ctx, "Elite"),
            m => m.Title.Contains("Forgot the write lock"));
        Assert.Contains(BrainQueries.Mistakes(tv.Ctx, "Elite", includeResolved: true),
            m => m.Title.Contains("Forgot the write lock"));
    }

    [Fact]
    public void InboxLifecycleRoundTripsThroughTheCli()
    {
        using var tv = Vault();
        var (addCode, _) = RunCli(tv, "inbox", "add", "--title", "Maybe cache aliases",
            "--content", "unproven idea", "--project", "Elite");
        Assert.Equal(0, addCode);
        Assert.Contains(BrainQueries.Inbox(tv.Ctx), n => n.Path == "00_Inbox/Maybe cache aliases.md");

        var (promoteCode, promoteOut) = RunCli(tv, "inbox", "promote", "Maybe cache aliases",
            "--to", "memory", "--json");
        Assert.Equal(0, promoteCode);
        using (var doc = JsonDocument.Parse(promoteOut))
            Assert.Equal("06_Agent_Memory/Maybe cache aliases.md", doc.RootElement.GetProperty("to").GetString());

        RunCli(tv, "inbox", "add", "--title", "Dead idea");
        var (rejectCode, _) = RunCli(tv, "inbox", "reject", "Dead idea");
        Assert.Equal(0, rejectCode);
        Assert.DoesNotContain(BrainQueries.Inbox(tv.Ctx), n => n.Title.Contains("Dead idea"));
    }

    [Fact]
    public void BrainOpsReportsHonestCountsAndPinnedToolCount()
    {
        using var tv = Vault();
        var ops = tv.Ctx.Ops.Run();
        Assert.Equal(MindVaultVersion.McpToolCount, ops.McpToolCount);
        Assert.True(ops.NoteCount > 0);
        Assert.True(ops.ActiveMistakeCount >= 1);
        Assert.NotNull(ops.LatestSession);
        Assert.NotEmpty(ops.RecommendedFixes);

        var (code, stdout) = RunCli(tv, "ops", "--json");
        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout);
        Assert.Equal(MindVaultVersion.McpToolCount,
            doc.RootElement.GetProperty("ops").GetProperty("mcpToolCount").GetInt32());
    }

    [Fact]
    public void CapsuleAndRecallCliRoundTrip()
    {
        using var tv = Vault();
        var (code, stdout) = RunCli(tv, "capsule", "--project", "elite-brain", "--mode", "coding", "--json");
        Assert.Equal(0, code);
        using (var doc = JsonDocument.Parse(stdout))
        {
            var capsule = doc.RootElement.GetProperty("capsule");
            Assert.Equal("Elite", capsule.GetProperty("project").GetString());
            Assert.Equal("high", capsule.GetProperty("confidence").GetString()); // via repoName
        }

        var (recallCode, recallOut) = RunCli(tv, "recall", "--project", "Elite",
            "--since", "2026-06-25", "--json");
        Assert.Equal(0, recallCode);
        using var recallDoc = JsonDocument.Parse(recallOut);
        Assert.True(recallDoc.RootElement.GetProperty("recall").GetProperty("decisions").GetArrayLength() >= 1);
    }
}
