namespace MindVault.Core;

public sealed record BrainOpsReport(
    string Health,
    IReadOnlyList<string> HealthReasons,
    string VaultPath,
    string ConfigSource,
    int NoteCount,
    int ManagedNoteCount,
    string? LastScanUtc,
    int? IndexAgeMinutes,
    bool RescanPending,
    int BrokenLinkCount,
    int OrphanCount,
    int DuplicateTitleCount,
    int AliasCollisionCount,
    double ArchivedRatio,
    int InboxDraftCount,
    int OpenRiskCount,
    int ActiveMistakeCount,
    int FeedbackEntryCount,
    int McpToolCount,
    string SkillsPack,
    string? LatestSession,
    IReadOnlyList<string> RecommendedFixes);

/// <summary>
/// One-call brain state: the doctor verdict plus every operational count that says whether
/// the memory is healthy, connected and current — with concrete recommended fixes. Counts
/// only; no vault content is included.
/// </summary>
public sealed class OpsService(VaultContext ctx)
{
    public BrainOpsReport Run()
    {
        var doctor = ctx.Doctor.Run();
        var notes = ctx.Db.GetAllNotes();
        var content = notes
            .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var managed = content.Count(n => NoteTypes.IsManaged(n.Type));
        var archiveFolder = ctx.Config.DefaultArchiveFolder;
        var archived = content.Count(n =>
            string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase) ||
            n.Path.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase));
        var archivedRatio = content.Count == 0 ? 0 : Math.Round((double)archived / content.Count, 2);

        var orphans = ctx.LinkIntel.Orphans().Rows.Count;
        var aliasCollisions = ctx.Audits.AuditAliases().Findings
            .Count(f => f.Code.StartsWith("alias-collision", StringComparison.Ordinal));
        var inbox = BrainQueries.Inbox(ctx).Count;
        var openRisks = ctx.Db.Query(type: "risk", statusIn: ["open", "active", "blocked"], limit: 500)
            .Count(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase));
        var activeMistakes = BrainQueries.Mistakes(ctx).Count;
        var feedback = ctx.Feedback.EntryCount();
        var rescanPending = ctx.IndexExists && ctx.Db.NeedsRescan;
        int? ageMinutes = doctor.LastScanUtc is { } scan
            ? (int)Math.Max((DateTime.UtcNow - scan).TotalMinutes, 0)
            : null;

        var latestSession = content
            .Where(n => string.Equals(n.Type, "memory", StringComparison.OrdinalIgnoreCase) &&
                        n.Stem.StartsWith("Log - ", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(n => n.Updated ?? n.Created ?? "", StringComparer.Ordinal)
            .Select(n => $"{n.Stem} (updated {n.Updated ?? n.Created ?? "unknown"})")
            .FirstOrDefault();

        var fixes = new List<string>();
        if (doctor.Verdict == "critical")
            fixes.Add("health is CRITICAL — run 'doctor' and fix its reasons before writing anything");
        if (rescanPending) fixes.Add("index schema changed — run 'scan' to repopulate");
        if (doctor.BrokenLinkCount > 0)
            fixes.Add($"{doctor.BrokenLinkCount} broken link(s) — run 'links broken' and repair");
        if (orphans > 0)
            fixes.Add($"{orphans} orphan note(s) — run 'links orphans', then 'links suggest' to connect them");
        if (doctor.DuplicateTitleCount > 0)
            fixes.Add($"{doctor.DuplicateTitleCount} duplicate title(s) — run 'validate' and merge/rename");
        if (aliasCollisions > 0)
            fixes.Add($"{aliasCollisions} alias collision(s) — run 'aliases audit'");
        if (inbox > 5)
            fixes.Add($"{inbox} unpromoted inbox draft(s) — review with 'inbox list', then promote or reject");
        if (fixes.Count == 0) fixes.Add("nothing urgent — the brain is in good shape");

        return new BrainOpsReport(
            doctor.Verdict, doctor.VerdictReasons ?? [], doctor.VaultPath, doctor.ConfigSource,
            doctor.NoteCount, managed,
            doctor.LastScanUtc?.ToString("yyyy-MM-dd HH:mm:ss") is { } ts ? ts + "Z" : null,
            ageMinutes, rescanPending,
            doctor.BrokenLinkCount, orphans, doctor.DuplicateTitleCount, aliasCollisions,
            archivedRatio, inbox, openRisks, activeMistakes, feedback,
            MindVaultVersion.McpToolCount,
            "ships in the repo's skills/ folder (installed per client — see skills/README.md)",
            latestSession, fixes);
    }
}
