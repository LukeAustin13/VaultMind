using System.ComponentModel;
using MindVault.Core;
using ModelContextProtocol.Server;

namespace MindVault.Mcp;

/// <summary>
/// The safe MCP tool surface. Every tool goes through MindVault.Core services: paths are
/// vault-guarded, mutations are snapshotted first, and outputs are compact JSON. No raw
/// file, shell or SQL access is exposed.
/// </summary>
[McpServerToolType]
public sealed class MindVaultTools(VaultContext ctx, McpRuntimeInfo? runtime = null)
{
    private const int MaxBodyChars = 60_000;
    private const int MaxIssues = 100;

    [McpServerTool(Name = "mindvault_status", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Get MindVault status: vault name, whether the index exists, note count, whether a rescan is pending and last scan time.")]
    public string Status() => Safe(() =>
    {
        var state = ctx.State.Load();
        var indexExists = ctx.IndexExists;
        var needsRescan = indexExists && ctx.Db.NeedsRescan;
        return new
        {
            vaultName = string.IsNullOrWhiteSpace(ctx.Config.VaultName)
                ? Path.GetFileName(ctx.VaultRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                : ctx.Config.VaultName,
            indexExists,
            rescanPending = needsRescan,
            noteCount = indexExists ? ctx.Db.CountNotes() : (int?)null,
            lastScanUtc = state?.LastScanUtc,
        };
    });

    [McpServerTool(Name = "mindvault_search", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Ranked full-text search across vault notes (title-weighted, recency-boosted; archived notes excluded by default). Returns title, path, type, project, status, matched section and a snippet per hit. When a project filter finds nothing the search falls back vault-wide and marks results with scope 'global-fallback'.")]
    public string Search(
        [Description("Search query (FTS5 syntax; plain words work fine)")] string query,
        [Description("Filter by note type, e.g. decision, task, project")] string? type = null,
        [Description("Preferred project scope (falls back vault-wide if empty)")] string? project = null,
        [Description("Filter by tag")] string? tag = null,
        [Description("Filter by status, e.g. open, done")] string? status = null,
        [Description("Max results (default 10, max 100)")] int limit = 10,
        [Description("Only notes updated on/after this date (yyyy-MM-dd)")] string? updatedAfter = null,
        [Description("Only notes updated on/before this date (yyyy-MM-dd)")] string? updatedBefore = null,
        [Description("Include archived notes (heavily deprioritised)")] bool includeArchived = false,
        [Description("Include per-result ranking factors for retrieval debugging")] bool explain = false) =>
        Safe(() =>
        {
            var results = ctx.Search.Search(query, type, project, tag, status, limit,
                updatedAfter, updatedBefore, includeArchived, explain);
            return new { count = results.Count, results };
        });

    [McpServerTool(Name = "mindvault_read_note", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Read one note by reference (relative path, title, filename, slug or [[wiki link]]). Ambiguous references return the candidate list instead of guessing.")]
    public string ReadNote(
        [Description("Note reference: path, title, filename or [[wiki link]]")] string noteRef) =>
        Safe(() =>
        {
            var note = ctx.Resolver.Resolve(noteRef);
            var abs = ctx.Resolver.AbsolutePathOf(note);
            var parsed = NoteParser.Parse(File.ReadAllText(abs), note.Path);
            var body = parsed.Body.Length > MaxBodyChars
                ? parsed.Body[..MaxBodyChars] + "\n… [truncated]"
                : parsed.Body;
            return new
            {
                path = note.Path,
                title = note.Title,
                type = note.Type,
                status = note.Status,
                project = note.Project,
                frontmatter = parsed.FrontmatterEntries.ToDictionary(
                    e => e.Key, e => e.IsList ? (object)e.Items : e.Scalar ?? ""),
                body,
                backlinks = ctx.Db.GetBacklinkPaths(
                    SlugHelper.NormalizeWiki(note.Title), SlugHelper.NormalizeWiki(note.Stem), note.Id),
            };
        });

    [McpServerTool(Name = "mindvault_list_notes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List indexed notes, optionally filtered by type, project, status or tag. Sorted by most recently updated.")]
    public string ListNotes(
        [Description("Filter by note type")] string? type = null,
        [Description("Filter by project name")] string? project = null,
        [Description("Filter by status")] string? status = null,
        [Description("Filter by tag")] string? tag = null,
        [Description("Max results (default 20)")] int limit = 20) =>
        Safe(() =>
        {
            var notes = ctx.Search.List(type, project, status, tag, limit);
            return new
            {
                count = notes.Count,
                notes = notes.Select(n => new { n.Path, n.Title, n.Type, n.Status, n.Project, n.Updated }),
            };
        });

    [McpServerTool(Name = "mindvault_create_project", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a new project note in 01_Projects with valid frontmatter and the standard section skeleton. Refuses names that already resolve to an existing project (including via alias) unless allowDuplicate is true.")]
    public string CreateProject(
        [Description("Project name (also becomes the file name)")] string name,
        [Description("Create even if a very similar project exists (default false)")] bool allowDuplicate = false) =>
        Safe(() => Created(ctx.Writer.CreateProject(name, allowDuplicate)));

    [McpServerTool(Name = "mindvault_create_decision", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a decision note in 04_Decisions, linked to an existing project (aliases resolve). Fails if the project note does not exist; refuses near-duplicate titles unless allowDuplicate is true — update or supersede the existing decision instead.")]
    public string CreateDecision(
        [Description("Existing project name (alias or repo name also works)")] string project,
        [Description("Decision title")] string title,
        [Description("Create even if a very similar decision exists (default false)")] bool allowDuplicate = false) =>
        Safe(() => Created(ctx.Writer.CreateDecision(project, title, allowDuplicate)));

    [McpServerTool(Name = "mindvault_create_task", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a task note linked to an existing project (aliases resolve). Fails if the project note does not exist; refuses near-duplicate titles unless allowDuplicate is true — update the existing task instead.")]
    public string CreateTask(
        [Description("Existing project name (alias or repo name also works)")] string project,
        [Description("Task title")] string title,
        [Description("Create even if a very similar task exists (default false)")] bool allowDuplicate = false) =>
        Safe(() => Created(ctx.Writer.CreateTask(project, title, allowDuplicate)));

    [McpServerTool(Name = "mindvault_append_to_note", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Append content under an existing heading of a note. Snapshots the note first. Errors if the heading is missing unless createSection is true. Pass dryRun to preview without writing.")]
    public string AppendToNote(
        [Description("Note reference: path, title, filename or [[wiki link]]")] string noteRef,
        [Description("Heading text to append under (without # markers)")] string section,
        [Description("Markdown content to append")] string content,
        [Description("Create the section at the end of the note when missing")] bool createSection = false,
        [Description("Preview only — report what would happen, change nothing")] bool dryRun = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.AppendToSection(noteRef, section, content, createSection, dryRun);
            return new { dryRun, path = result.Path, message = result.Message, snapshot = result.SnapshotPath };
        });

    [McpServerTool(Name = "mindvault_update_frontmatter", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set one flat frontmatter key on a note (e.g. status). Nested YAML values are rejected. For tags/links pass a comma-separated list. Snapshots first. Pass dryRun to preview the old -> new value without writing.")]
    public string UpdateFrontmatter(
        [Description("Note reference")] string noteRef,
        [Description("Frontmatter key, e.g. status")] string key,
        [Description("New scalar value (comma-separated list for tags/links)")] string value,
        [Description("Preview only — report what would happen, change nothing")] bool dryRun = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.UpdateFrontmatter(noteRef, key, value, dryRun);
            return new { dryRun, path = result.Path, message = result.Message, snapshot = result.SnapshotPath };
        });

    [McpServerTool(Name = "mindvault_link_notes", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Add a [[wiki link]] to the source note's frontmatter links list pointing at the target note. Snapshots first; no-op if already linked.")]
    public string LinkNotes(
        [Description("Source note reference")] string fromRef,
        [Description("Target note reference")] string toRef) =>
        Safe(() =>
        {
            var result = ctx.Writer.LinkNotes(fromRef, toRef);
            return new { path = result.Path, changed = result.Changed, message = result.Message };
        });

    [McpServerTool(Name = "mindvault_archive_note", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Archive a note instead of deleting: snapshot, set status archived, move to 99_Archive, reindex. There is no delete tool. Pass dryRun to preview the move without changing anything.")]
    public string ArchiveNote(
        [Description("Note reference")] string noteRef,
        [Description("Preview only — report what would happen, change nothing")] bool dryRun = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.Archive(noteRef, dryRun);
            return new { dryRun, from = result.FromPath, to = result.ToPath, snapshot = result.SnapshotPath, warnings = result.Warnings };
        });

    [McpServerTool(Name = "mindvault_detect_project", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Map a repository/folder name (or any shorthand) to a vault project using exact titles, declared aliases, repoNames frontmatter and separator-insensitive comparison. Returns the match with a confidence tier, or candidates when ambiguous/uncertain — it never guesses. Call this first when starting work in a repo.")]
    public string DetectProject(
        [Description("Repo folder name, project shorthand or alias, e.g. 'mind-vault'")] string name) =>
        Safe(() =>
        {
            var d = ctx.ProjectDetect.Detect(name);
            return new
            {
                input = name.Trim(),
                project = d.Project?.Title,
                path = d.Project?.Path,
                confidence = d.Confidence,
                matchedVia = d.MatchedVia,
                candidates = d.Candidates,
            };
        });

    [McpServerTool(Name = "mindvault_find_related", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Related notes for one note, each with a reason: outgoing wiki links, backlinks, active same-project memory, and same-type notes with similar titles (possible duplicates/follow-ups). Compact and deterministic; use it to find the tasks/risks/reviews around a decision without multiple searches.")]
    public string FindRelated(
        [Description("Note reference: path, title, filename or [[wiki link]]")] string noteRef,
        [Description("Max related notes (default 12, max 50)")] int limit = RelatedNotesService.DefaultLimit) =>
        Safe(() =>
        {
            var result = ctx.Related.Get(noteRef, limit);
            return new { note = result.Title, path = result.Path, count = result.Related.Count, related = result.Related };
        });

    [McpServerTool(Name = "mindvault_validate_vault", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Validate the vault: folders, frontmatter schema, nested YAML, duplicate titles, broken wiki links, statuses, project references, stale tasks, superseded-decision contradictions and environment problems. Returns severity counts plus the top issues.")]
    public string ValidateVault() => Safe(() =>
    {
        var report = ctx.Validator.Validate();
        return new
        {
            criticals = report.CriticalCount,
            warnings = report.WarningCount,
            infos = report.InfoCount,
            elapsedMs = report.ElapsedMs,
            truncated = report.Issues.Count > MaxIssues,
            issues = report.Issues.Take(MaxIssues),
        };
    });

    [McpServerTool(Name = "mindvault_get_project_context", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Compact project bundle: goal, non-negotiables, active/blocked tasks, decisions in force, risks, constraints, recent implementation logs, recommended next reads and warnings (stale/contradictory/duplicate). Use this first to understand a project.")]
    public string GetProjectContext(
        [Description("Project name")] string project,
        [Description("Max items per list (default 10, max 50)")] int limit = 10,
        [Description("Detail level: brief, standard or deep")] string detailLevel = "standard") =>
        Safe(() => ctx.Projects.Get(project, limit, detailLevel));

    [McpServerTool(Name = "mindvault_get_context_pack", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Generated briefing pack for starting work on a project: summary, goal, non-negotiables, relevant architecture/decisions, active tasks, risks, constraints, suggested next reads and a do-not-forget list. Pass the task description to surface task-relevant notes first. Compact by design — it carries refs, not full notes.")]
    public string GetContextPack(
        [Description("Project name")] string project,
        [Description("What you are about to work on (optional; improves relevance)")] string? task = null,
        [Description("Output: json (default) or markdown")] string output = "json") =>
        Safe(() =>
        {
            var pack = ctx.Packs.Get(project, task);
            return output.Trim().ToLowerInvariant() switch
            {
                "json" => pack,
                "markdown" => new { markdown = ContextPackService.ToMarkdown(pack) },
                _ => throw new MindVaultException($"Unknown output format '{output}'. Use json or markdown."),
            };
        });

    [McpServerTool(Name = "mindvault_check_draft", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Quality-check a note idea BEFORE creating it: duplicate/near-duplicate detection, missing project, vague titles, and supersede suggestions for conflicting decisions. Blockers mean the create would fail or duplicate; warnings are advisory. Always call this before creating durable notes.")]
    public string CheckDraft(
        [Description("Note type, e.g. decision, task, risk")] string type,
        [Description("Project name (required for decisions and tasks)")] string? project = null,
        [Description("Proposed title")] string title = "") =>
        Safe(() =>
        {
            var result = ctx.Drafts.CheckDraft(type, project, title);
            return new
            {
                ok = result.Ok, blockers = result.Blockers, warnings = result.Warnings,
                suggestions = result.Suggestions, relatedPaths = result.RelatedPaths,
                likelyDuplicatePaths = result.LikelyDuplicatePaths,
            };
        });

    [McpServerTool(Name = "mindvault_supersede_decision", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Replace one decision with another: the old decision gets status 'superseded' and a superseded_by link, the new one gets a supersedes link. Both notes are snapshotted first; if the second write fails the first rolls back.")]
    public string SupersedeDecision(
        [Description("Reference of the decision being replaced")] string oldRef,
        [Description("Reference of the decision that replaces it")] string newRef) =>
        Safe(() =>
        {
            var result = ctx.Writer.SupersedeDecision(oldRef, newRef);
            return new
            {
                old = result.OldPath, @new = result.NewPath,
                oldSnapshot = result.OldSnapshot, newSnapshot = result.NewSnapshot,
            };
        });

    [McpServerTool(Name = "mindvault_start_session", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Start a coding session on a project: returns the context pack (goal, constraints, tasks, decisions, warnings) and ensures the project's implementation-log note exists. Call this before coding; call mindvault_end_session when done.")]
    public string StartSession(
        [Description("Project name")] string project,
        [Description("What this session will work on (optional; improves pack relevance)")] string? task = null) =>
        Safe(() =>
        {
            var result = ctx.Sessions.Start(project, task);
            return new
            {
                logNote = result.LogNotePath, logNoteCreated = result.LogNoteCreated,
                task = result.Task, pack = result.Pack,
            };
        });

    [McpServerTool(Name = "mindvault_end_session", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("End a coding session by writing one concise handoff entry to the project's implementation-log note: summary, tests/builds run, follow-ups. Keep it short — this is a handoff, not a transcript.")]
    public string EndSession(
        [Description("Project name")] string project,
        [Description("One-line summary of what was accomplished")] string summary,
        [Description("Tests/builds run and their result, e.g. 'dotnet test green (145)'")] string? tests = null,
        [Description("Remaining risks or follow-ups (comma-separated), or omit for none")] string? followUps = null) =>
        Safe(() =>
        {
            var result = ctx.Sessions.End(project, summary, tests, followUps);
            return new { path = result.Path, snapshot = result.SnapshotPath, message = result.Message };
        });

    [McpServerTool(Name = "mindvault_health", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Fast health check with a verdict (good/warning/critical): vault configured/writable, index exists/stale, note count, last scan and app version. Compact; never returns secrets, environment variables or host paths.")]
    public string Health() => Safe(() =>
    {
        var health = BuildHealth();
        var verdict = !health.VaultWritable ? "critical"
            : !health.IndexExists || health.IndexStale ? "warning"
            : "good";
        return new
        {
            ok = health.VaultWritable && health.IndexExists && !health.IndexStale,
            verdict,
            version = MindVaultVersion.Current,
            vaultConfigured = true, // this server refuses to start without a resolvable vault
            health.VaultWritable,
            health.IndexExists,
            health.IndexStale,
            health.NoteCount,
            health.LastScanUtc,
        };
    });

    [McpServerTool(Name = "mindvault_diagnostics", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Deeper diagnostics: health plus transport, index schema version, a validation summary (severity counts) and warnings. Runs a scan+validation, so it is slower than mindvault_health. Never returns secrets, environment variables or host paths.")]
    public string Diagnostics() => Safe(() =>
    {
        var health = BuildHealth();
        var report = ctx.Validator.Validate();
        var warnings = new List<string>();
        if (!health.VaultWritable) warnings.Add("Vault folder is not writable — all mutations will fail.");
        if (health.IndexStale) warnings.Add("Index is stale — run mindvault_rebuild_index or any search to refresh it.");
        if (ctx.IndexExists && ctx.Db.UserVersion != IndexDatabase.CurrentSchemaVersion)
            warnings.Add($"Index schema is v{ctx.Db.UserVersion} but this build expects v{IndexDatabase.CurrentSchemaVersion} — rebuild the index.");
        if (report.CriticalCount > 0)
            warnings.Add($"Validation found {report.CriticalCount} critical issue(s) — run mindvault_validate_vault for details.");
        return new
        {
            ok = health.VaultWritable && report.CriticalCount == 0,
            version = MindVaultVersion.Current,
            transport = runtime?.Transport ?? "unknown",
            indexSchemaVersion = ctx.IndexExists ? ctx.Db.UserVersion : 0,
            expectedSchemaVersion = IndexDatabase.CurrentSchemaVersion,
            health.VaultWritable,
            health.IndexExists,
            health.IndexStale,
            health.NoteCount,
            health.LastScanUtc,
            validation = new
            {
                criticals = report.CriticalCount,
                warnings = report.WarningCount,
                infos = report.InfoCount,
                elapsedMs = report.ElapsedMs,
            },
            warnings,
        };
    });

    private sealed record HealthSnapshot(
        bool VaultWritable, bool IndexExists, bool IndexStale, int? NoteCount, DateTime? LastScanUtc);

    private HealthSnapshot BuildHealth()
    {
        var state = ctx.State.Load();
        var indexExists = ctx.IndexExists;
        var last = state?.LastScanUtc;
        var ttl = ctx.Config.ScanStalenessSeconds;
        var stale = !indexExists ||
                    ctx.Db.NeedsRescan ||
                    (ttl > 0 && (last is null || (DateTime.UtcNow - last.Value).TotalSeconds > ttl));
        return new HealthSnapshot(
            ProbeWritable(ctx.VaultRoot),
            indexExists,
            stale,
            indexExists ? ctx.Db.CountNotes() : null,
            last);
    }

    private static bool ProbeWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".mindvault-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [McpServerTool(Name = "mindvault_rebuild_index", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Clear and rebuild the SQLite index from the Markdown files (the files are canonical; the index is disposable cache).")]
    public string RebuildIndex() => Safe(() =>
    {
        var result = ctx.Scanner.Scan(full: true);
        return new { result.Added, result.Updated, result.Removed, result.Unchanged, result.Errors };
    });

    private static object Created(CreateNoteResult result) =>
        new
        {
            path = result.Note.Path, title = result.Note.Title,
            type = result.Note.Type, status = result.Note.Status,
            warnings = result.Warnings,
        };

    private static string Safe(Func<object> action)
    {
        try
        {
            return Json.Serialize(action());
        }
        catch (AmbiguousNoteRefException ex)
        {
            return Json.Serialize(new { error = ex.Message, code = ex.Code, candidates = ex.Candidates });
        }
        catch (DuplicateSuspectedException ex)
        {
            return Json.Serialize(new
            {
                created = false, reason = "possible_duplicate",
                error = ex.Message, code = ex.Code, candidates = ex.Candidates,
            });
        }
        catch (MindVaultException ex)
        {
            return Json.Serialize(new { error = ex.Message, code = ex.Code });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Log full detail server-side; return a sanitized message that never leaks stack
            // traces or absolute host paths (ex.Message often embeds them).
            Console.Error.WriteLine($"MindVault internal error ({ex.GetType().Name}): {ex}");
            return Json.Serialize(new
            {
                error = "MindVault encountered an internal error handling this request.",
                code = ErrorCodes.Unexpected,
                errorType = ex.GetType().Name,
            });
        }
    }
}
