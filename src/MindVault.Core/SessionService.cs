namespace MindVault.Core;

public sealed record SessionEntry(string Kind, string Heading, string LogPath);

/// <summary>A decision to create as part of end_session. content is optional body text.</summary>
public sealed record CloseDecision(string Title, string? Content = null);

/// <summary>A mistake to record as part of end_session.</summary>
public sealed record CloseMistake(string Title, string? Lesson = null, string? Prevention = null);

/// <summary>
/// A task change at end_session: pass <see cref="Ref"/> + <see cref="Status"/> to update an
/// existing task's status, OR <see cref="Title"/> to create a new task. Exactly one shape.
/// </summary>
public sealed record CloseTask(string? Ref = null, string? Status = null, string? Title = null);

/// <summary>The structured batch to process after the handoff is written.</summary>
public sealed record SessionCloseItems(
    IReadOnlyList<CloseDecision>? Decisions = null,
    IReadOnlyList<CloseMistake>? Mistakes = null,
    IReadOnlyList<CloseTask>? Tasks = null);

/// <summary>Per-item outcome. outcome ∈ created | updated | skipped_duplicate | blocked | error.</summary>
public sealed record CloseItemResult(string Kind, string Title, string Outcome, string? Path, string? Message);

/// <summary>The full end_session result: the handoff write plus any batched item results.</summary>
public sealed record SessionCloseResult(
    bool DryRun, string Path, string? SnapshotPath, string Message,
    IReadOnlyList<string>? RiskWarnings, IReadOnlyList<CloseItemResult> Items);

/// <summary>
/// Lightweight coding-session lifecycle. No daemon, no state: `start` composes the budgeted
/// session brief (and makes sure the project's implementation-log note exists), `log` and `end`
/// append structured entries to that note's Sessions section through the safe-write pipeline.
/// </summary>
public sealed class SessionService(VaultContext ctx)
{
    /// <summary>
    /// The one-call session brief behind CLI `session start` and MCP start_session: ensures the
    /// log note exists, then composes a budgeted <see cref="SessionBriefResult"/> (goal,
    /// decisions, tasks, risks, read-first, delta since last handoff, ...).
    /// </summary>
    public SessionBriefResult StartBrief(string project, string? task = null, int maxChars = BriefService.DefaultBudget)
    {
        var proj = ctx.Writer.FindProject(project);
        var (logPath, created) = EnsureLogNote(proj);
        return ctx.Briefs.Compose(proj.Title, string.IsNullOrWhiteSpace(task) ? null : task.Trim(),
            created, logPath, maxChars);
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
    /// Ends a session AND processes a structured batch of durable-memory changes in one call.
    /// The handoff entry is written FIRST so a bad batch item can never lose the handoff; each
    /// item then runs through the SAME gates as its standalone tool (duplicate, risky-content,
    /// project resolution). One failing item never aborts the others or the handoff. dryRun
    /// previews everything and writes nothing.
    /// </summary>
    public SessionCloseResult Close(string project, string summary, string? tests, string? followUps,
        SessionCloseItems? items, bool dryRun = false, bool allowRiskyContent = false)
    {
        var proj = ctx.Writer.FindProject(project);
        var handoff = End(proj.Title, summary, tests, followUps, dryRun, allowRiskyContent);

        var results = new List<CloseItemResult>();
        if (items is not null)
        {
            foreach (var d in items.Decisions ?? [])
                results.Add(ProcessDecision(proj.Title, d, dryRun, allowRiskyContent));
            foreach (var m in items.Mistakes ?? [])
                results.Add(ProcessMistake(proj.Title, m, dryRun, allowRiskyContent));
            foreach (var t in items.Tasks ?? [])
                results.Add(ProcessTask(proj.Title, t, dryRun, allowRiskyContent));
        }

        return new SessionCloseResult(dryRun, handoff.Path, handoff.SnapshotPath, handoff.Message,
            handoff.RiskWarnings, results);
    }

    private CloseItemResult ProcessDecision(string project, CloseDecision d, bool dryRun, bool allowRisky)
    {
        var title = (d.Title ?? "").Trim();
        if (title.Length == 0)
            return new CloseItemResult("decision", "", "error", null, "A decision needs a title.");
        return RunGated("decision", title, () =>
        {
            // Gate the body BEFORE creating so a risky decision is blocked without leaving a
            // half-written note; the create + append then run through the normal pipeline.
            if (!string.IsNullOrWhiteSpace(d.Content))
                ContentRiskScanner.Gate(d.Content, allowRisky);
            if (dryRun)
            {
                ThrowIfDuplicate("decision", project, title);
                return ("created", (string?)null, $"[dry-run] Would create decision '{title}' in {project}.");
            }
            var created = ctx.Writer.CreateDecision(project, title);
            if (!string.IsNullOrWhiteSpace(d.Content))
                ctx.Writer.AppendToSection(created.Note.Path, "Decision", d.Content!.Trim(),
                    createSection: true, allowRiskyContent: allowRisky);
            return ("created", created.Note.Path, $"Created decision '{created.Note.Title}'.");
        });
    }

    private CloseItemResult ProcessMistake(string project, CloseMistake m, bool dryRun, bool allowRisky)
    {
        var title = (m.Title ?? "").Trim();
        if (title.Length == 0)
            return new CloseItemResult("mistake", "", "error", null, "A mistake needs a title.");
        return RunGated("mistake", title, () =>
        {
            if (dryRun)
            {
                ThrowIfDuplicate("mistake", project, title);
                return ("created", (string?)null, $"[dry-run] Would record mistake '{title}'.");
            }
            var created = ctx.Writer.CreateMistake(title, project, m.Lesson, m.Prevention,
                allowDuplicate: false, allowRiskyContent: allowRisky);
            return ("created", created.Note.Path, $"Recorded mistake '{created.Note.Title}'.");
        });
    }

    private CloseItemResult ProcessTask(string project, CloseTask t, bool dryRun, bool allowRisky)
    {
        var hasRef = !string.IsNullOrWhiteSpace(t.Ref);
        var hasTitle = !string.IsNullOrWhiteSpace(t.Title);
        if (hasRef == hasTitle)
            return new CloseItemResult("task", t.Ref ?? t.Title ?? "", "error", null,
                "A task item needs either {ref, status} to update or {title} to create — not both, not neither.");

        if (hasRef)
        {
            var status = (t.Status ?? "").Trim();
            if (status.Length == 0)
                return new CloseItemResult("task", t.Ref!, "error", null,
                    "Updating a task needs a status (e.g. done, blocked).");
            try
            {
                if (dryRun)
                {
                    var note = ctx.Resolver.Resolve(t.Ref!);
                    return new CloseItemResult("task", note.Title, "updated", note.Path,
                        $"[dry-run] Would set status: {status} on {note.Path}.");
                }
                var r = ctx.Writer.UpdateFrontmatter(t.Ref!, "status", status, allowRiskyContent: allowRisky);
                return new CloseItemResult("task", Path.GetFileNameWithoutExtension(r.Path), "updated",
                    r.Path, r.Message);
            }
            catch (MindVaultException ex)
            {
                return new CloseItemResult("task", t.Ref!, "error", null, ex.Message);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One bad item must never abort the rest of the batch, whatever it throws.
                return new CloseItemResult("task", t.Ref!, "error", null, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        var title = t.Title!.Trim();
        return RunGated("task", title, () =>
        {
            if (dryRun)
            {
                ThrowIfDuplicate("task", project, title);
                return ("created", (string?)null, $"[dry-run] Would create task '{title}' in {project}.");
            }
            var created = ctx.Writer.CreateTask(project, title);
            return ("created", created.Note.Path, $"Created task '{created.Note.Title}'.");
        });
    }

    /// <summary>
    /// The duplicate gate the real create runs (via WriteService), applied to dry-run previews
    /// too — a duplicate must preview as skipped_duplicate, never as created.
    /// </summary>
    private void ThrowIfDuplicate(string type, string? project, string title)
    {
        var clean = SlugHelper.SanitizeFileName(title);
        var check = ctx.Drafts.CheckDraft(type, project, clean);
        if (check.LikelyDuplicatePaths.Count > 0)
            throw new DuplicateSuspectedException(type, clean, check.LikelyDuplicatePaths);
    }

    /// <summary>
    /// Runs a create through its gates, mapping exceptions to per-item outcomes so a duplicate,
    /// a risky-content block or even an unexpected failure is reported on the item, never
    /// thrown out of the batch (cancellation excepted).
    /// </summary>
    private static CloseItemResult RunGated(string kind, string title,
        Func<(string Outcome, string? Path, string Message)> action)
    {
        try
        {
            var (outcome, path, message) = action();
            return new CloseItemResult(kind, title, outcome, path, message);
        }
        catch (DuplicateSuspectedException ex)
        {
            return new CloseItemResult(kind, title, "skipped_duplicate", null, ex.Message);
        }
        catch (MindVaultException ex)
        {
            var outcome = ex.Code == ErrorCodes.RiskyContent ? "blocked" : "error";
            return new CloseItemResult(kind, title, outcome, null, ex.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One bad item must never abort the rest of the batch, whatever it throws.
            return new CloseItemResult(kind, title, "error", null, $"{ex.GetType().Name}: {ex.Message}");
        }
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

    /// <summary>
    /// Timestamp of the project's most recent handoff (### heading) in its session log, or null
    /// when there is no log or no handoff yet. Shared by recent-sessions ordering and by
    /// handoff-relative recall so the "### yyyy-MM-dd HH:mm" parse lives in exactly one place.
    /// </summary>
    public DateTime? MostRecentHandoffAt(string project)
    {
        var proj = ctx.Writer.FindProject(project);
        var log = ctx.Db.FindByStem($"Log - {proj.Stem}")
            .FirstOrDefault(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase));
        if (log is null) return null;

        var raw = File.ReadAllText(ctx.Resolver.AbsolutePathOf(log)).Replace("\r\n", "\n");
        FrontmatterCodec.TryExtract(raw, out _, out var body);
        var headings = NoteParser.ExtractHeadings(body);
        var sessions = headings.FirstOrDefault(h =>
            h.Level == 2 && string.Equals(h.Text, "Sessions", StringComparison.OrdinalIgnoreCase));
        if (sessions is null) return null;
        var end = headings
            .Where(h => h.Line > sessions.Line && h.Level <= 2)
            .OrderBy(h => h.Line)
            .FirstOrDefault()?.Line ?? int.MaxValue;

        return headings
            .Where(h => h.Line > sessions.Line && h.Line < end && h.Level == 3)
            .Select(h => ParseHandoffTimestamp(h.Text))
            .Where(d => d is not null)
            .OrderByDescending(d => d!.Value)
            .FirstOrDefault();
    }

    /// <summary>Parses the leading "yyyy-MM-dd HH:mm" of a handoff heading (the text after "### ").</summary>
    internal static DateTime? ParseHandoffTimestamp(string headingText)
    {
        var text = headingText.Trim();
        if (text.Length < 16) return null;
        return DateTime.TryParseExact(text[..16], "yyyy-MM-dd HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var dt)
            ? dt
            : null;
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
