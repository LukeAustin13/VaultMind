namespace MindVault.Core;

public sealed record SessionStartResult(
    ContextPack Pack, string LogNotePath, bool LogNoteCreated, string? Task);

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

    /// <summary>Mid-session note — use sparingly; the handoff entry at `end` is the one that matters.</summary>
    public WriteResult Log(string project, string summary)
    {
        RequireSummary(summary);
        var proj = ctx.Writer.FindProject(project);
        var (logPath, _) = EnsureLogNote(proj);
        var entry = $"#### {DateTime.Now:yyyy-MM-dd HH:mm} — {summary.Trim()}";
        return ctx.Writer.AppendToSection(logPath, "Sessions", entry, createSection: true);
    }

    /// <summary>Writes the concise handoff block a future session (or person) resumes from.</summary>
    public WriteResult End(string project, string summary, string? tests = null, string? followUps = null)
    {
        RequireSummary(summary);
        var proj = ctx.Writer.FindProject(project);
        var (logPath, _) = EnsureLogNote(proj);
        var entry =
            $"### {DateTime.Now:yyyy-MM-dd HH:mm} — {summary.Trim()}\n\n" +
            $"- Tests: {Fallback(tests, "not recorded")}\n" +
            $"- Follow-ups: {Fallback(followUps, "none")}";
        return ctx.Writer.AppendToSection(logPath, "Sessions", entry, createSection: true);
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
