namespace MindVault.Core;

public sealed record SessionStartResult(
    ContextPack Pack, string LogNotePath, bool LogNoteCreated, string? Task);

public sealed record SessionEntry(string Kind, string Heading, string LogPath);

/// <summary>
/// Lightweight coding-session lifecycle. No daemon, no state: `start` returns a context pack
/// (and makes sure the project's implementation-log note exists), `log` and `end` append
/// structured entries to that note's Sessions section through the normal safe-write pipeline.
/// </summary>
public sealed class SessionService(VaultContext ctx)
{
    public SessionStartResult Start(string project, string? task = null)
    {
        var proj = ctx.Writer.FindProject(project);
        var (logPath, created) = EnsureLogNote(proj);
        var pack = ctx.Packs.Get(proj.Title, task);
        return new SessionStartResult(pack, logPath, created, string.IsNullOrWhiteSpace(task) ? null : task.Trim());
    }

    /// <summary>Mid-session checkpoint — use sparingly; the handoff entry at `end` is the one that matters.</summary>
    public WriteResult Log(string project, string summary, bool dryRun = false, bool allowRiskyContent = false)
    {
        RequireSummary(summary);
        var proj = ctx.Writer.FindProject(project);
        var (logPath, _) = EnsureLogNote(proj);
        var entry = $"#### {DateTime.Now:yyyy-MM-dd HH:mm} — {summary.Trim()}";
        return ctx.Writer.AppendToSection(logPath, "Sessions", entry, createSection: true,
            dryRun: dryRun, allowRiskyContent: allowRiskyContent);
    }

    /// <summary>Writes the concise handoff block a future session (or person) resumes from.</summary>
    public WriteResult End(string project, string summary, string? tests = null, string? followUps = null,
        bool dryRun = false, bool allowRiskyContent = false)
    {
        RequireSummary(summary);
        var proj = ctx.Writer.FindProject(project);
        var (logPath, _) = EnsureLogNote(proj);
        var entry =
            $"### {DateTime.Now:yyyy-MM-dd HH:mm} — {summary.Trim()}\n\n" +
            $"- Tests: {Fallback(tests, "not recorded")}\n" +
            $"- Follow-ups: {Fallback(followUps, "none")}";
        return ctx.Writer.AppendToSection(logPath, "Sessions", entry, createSection: true,
            dryRun: dryRun, allowRiskyContent: allowRiskyContent);
    }

    /// <summary>
    /// The most recent session entries from the project's log note, newest first: handoffs
    /// (### entries) and checkpoints (#### entries). Entry headings start with a timestamp,
    /// so ordinal-descending text order is chronological.
    /// </summary>
    public List<SessionEntry> Recent(string project, int limit = 5)
    {
        limit = Math.Clamp(limit, 1, 20);
        var proj = ctx.Writer.FindProject(project);
        var log = ctx.Db.FindByStem($"Log - {proj.Stem}")
            .FirstOrDefault(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase));
        if (log is null) return [];

        var raw = File.ReadAllText(ctx.Resolver.AbsolutePathOf(log)).Replace("\r\n", "\n");
        FrontmatterCodec.TryExtract(raw, out _, out var body);
        var headings = NoteParser.ExtractHeadings(body);
        var sessions = headings.FirstOrDefault(h =>
            h.Level == 2 && string.Equals(h.Text, "Sessions", StringComparison.OrdinalIgnoreCase));
        if (sessions is null) return [];
        var end = headings
            .Where(h => h.Line > sessions.Line && h.Level <= 2)
            .OrderBy(h => h.Line)
            .FirstOrDefault()?.Line ?? int.MaxValue;

        return headings
            .Where(h => h.Line > sessions.Line && h.Line < end && h.Level is 3 or 4)
            .OrderByDescending(h => h.Text, StringComparer.Ordinal)
            .Take(limit)
            .Select(h => new SessionEntry(h.Level == 3 ? "handoff" : "checkpoint", h.Text, log.Path))
            .ToList();
    }

    private (string Path, bool Created) EnsureLogNote(NoteSummary proj)
    {
        var existing = ctx.Db.FindByStem($"Log - {proj.Stem}")
            .FirstOrDefault(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase));
        if (existing is not null) return (existing.Path, false);

        var note = ctx.Writer.CreateNoteFile(
            $"06_Agent_Memory/Log - {proj.Stem}.md",
            NoteTemplates.ImplementationLog(proj.Title, proj.Stem, DateTime.Now.ToString("yyyy-MM-dd")));
        return (note.Path, true);
    }

    private static void RequireSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
            throw new MindVaultException("A session entry needs a one-line summary.");
    }

    private static string Fallback(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().Replace("\n", " ");
}
