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
    [Description("Read one note by reference (relative path, title, filename, slug or [[wiki link]]). Ambiguous references return the candidate list instead of guessing. Token-saving options: pass 'section' to get just one heading's content, or 'maxChars' to cap the body — prefer these over full reads.")]
    public string ReadNote(
        [Description("Note reference: path, title, filename or [[wiki link]]")] string noteRef,
        [Description("Return only this heading's content (e.g. 'Goal') instead of the whole body")] string? section = null,
        [Description("Cap the returned body at this many chars (0 = default cap)")] int maxChars = 0) =>
        Safe(() =>
        {
            var note = ctx.Resolver.Resolve(noteRef);
            var abs = ctx.Resolver.AbsolutePathOf(note);
            var parsed = NoteParser.Parse(File.ReadAllText(abs), note.Path);
            var cap = maxChars > 0 ? Math.Min(maxChars, MaxBodyChars) : MaxBodyChars;
            string body;
            if (!string.IsNullOrWhiteSpace(section))
            {
                body = SectionExtractor.GetSectionText(parsed.Body, section!, cap)
                       ?? $"[section '{section}' not found — headings: " +
                          string.Join(", ", parsed.Headings.Select(h => h.Text)) + "]";
            }
            else
            {
                body = parsed.Body.Length > cap
                    ? parsed.Body[..cap] + "\n… [truncated] (pass maxChars or section to scope the read)"
                    : parsed.Body;
            }
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
        [Description("Preview only — report what would happen, change nothing")] bool dryRun = false,
        [Description("Store even if the content gate flags secrets (default false)")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.AppendToSection(noteRef, section, content, createSection, dryRun, allowRiskyContent);
            return new { dryRun, path = result.Path, message = result.Message, snapshot = result.SnapshotPath, riskWarnings = result.RiskWarnings };
        });

    [McpServerTool(Name = "mindvault_update_frontmatter", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set one flat frontmatter key on a note (e.g. status). Nested YAML values are rejected. For tags/links pass a comma-separated list. Snapshots first. Pass dryRun to preview the old -> new value without writing.")]
    public string UpdateFrontmatter(
        [Description("Note reference")] string noteRef,
        [Description("Frontmatter key, e.g. status")] string key,
        [Description("New scalar value (comma-separated list for tags/links)")] string value,
        [Description("Preview only — report what would happen, change nothing")] bool dryRun = false,
        [Description("Store even if the content gate flags secrets (default false)")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.UpdateFrontmatter(noteRef, key, value, dryRun, allowRiskyContent);
            return new { dryRun, path = result.Path, message = result.Message, snapshot = result.SnapshotPath, riskWarnings = result.RiskWarnings };
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

    [McpServerTool(Name = "mindvault_capture_thought", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Capture a raw, uncertain idea as a thought note in 06_Agent_Memory/Inbox — NOT durable memory. Use this instead of creating decisions/memories you are not sure about; promote it later with mindvault_promote_note once confirmed. This IS the inbox-add tool; list drafts with mindvault_list_inbox, reject one with mindvault_archive_note.")]
    public string CaptureThought(
        [Description("Short thought title")] string title,
        [Description("Optional body text for the Thought section")] string? content = null,
        [Description("Project to tag the thought with (alias/repo name works)")] string? project = null,
        [Description("Store even if the content gate flags secrets (default false)")] bool allowRiskyContent = false) =>
        Safe(() => Created(ctx.Writer.CaptureThought(title, content, agentInbox: true, project, allowRiskyContent)));

    [McpServerTool(Name = "mindvault_build_context_capsule", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Build a compact, char-budgeted context capsule for a project: goal, non-negotiables, decisions in force, open/blocked tasks, risks, constraints, known mistakes with do-not-repeat rules, superseded-decision warnings, open questions, suggested reads and source paths. Modes reshape priority: coding, debugging, review, planning, handoff, release, architecture. Feedback-aware (hidden excluded, pinned boosted). If the project name is ambiguous, candidates come back instead of a guess.")]
    public string BuildContextCapsule(
        [Description("Project name (alias or repo name also works)")] string project,
        [Description("coding | debugging | review | planning | handoff | release | architecture")] string mode = "coding",
        [Description("Character budget for the capsule (default 8000)")] int maxChars = CapsuleService.DefaultBudget) =>
        Safe(() =>
        {
            var outcome = ctx.Capsules.Build(project, mode, maxChars);
            return outcome.Capsule is null
                ? (object)new { ambiguous = true, candidates = outcome.Candidates }
                : new { ambiguous = false, capsule = outcome.Capsule, markdown = CapsuleService.ToMarkdown(outcome.Capsule) };
        });

    [McpServerTool(Name = "mindvault_get_work_context", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Memory related to what you are working on RIGHT NOW. Pass exactly one of: currentFile (source path — token-matched), query (free text), or note (graph expansion). Returns decisions/tasks/risks/mistakes/reviews/logs each with the reasons it matched, plus suggested reads. Use before risky edits. Feedback-aware; archived/superseded/hidden never appear.")]
    public string GetWorkContext(
        [Description("Project name (alias or repo name also works)")] string project,
        [Description("Source file path you are editing, e.g. src/App/WriteService.cs")] string? currentFile = null,
        [Description("Free-text query describing the work")] string? query = null,
        [Description("Note reference to expand from")] string? note = null,
        [Description("Max results per group (default 12)")] int limit = 12) =>
        Safe(() => new { workContext = ctx.WorkContext.Get(project, currentFile, query, note, limit) });

    [McpServerTool(Name = "mindvault_recall", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("What changed in a time window: decisions/tasks/risks/mistakes/session logs/reviews/notes created or updated since a date ('7 days' or yyyy-MM-dd), or on this day in earlier years. Frontmatter dates first, file mtime fallback; archived excluded (counted in warnings). Use when continuing work after a gap.")]
    public string Recall(
        [Description("Project name (optional — omit for vault-wide)")] string? project = null,
        [Description("Window: '7 days' or a yyyy-MM-dd date (default 7 days)")] string? since = null,
        [Description("Anniversaries: same month/day in earlier years")] bool onThisDay = false) =>
        Safe(() => new { recall = ctx.RecallSvc.Recall(project, since, onThisDay) });

    [McpServerTool(Name = "mindvault_record_feedback", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Record deterministic retrieval feedback for a note: pinned (always surface), hidden (never surface), useful, noisy, outdated, wrong, or clear (reset). Stored in a sidecar (.mindvault/feedback.jsonl) — vault Markdown is untouched. Feedback shapes capsules, work-context, related notes and link suggestions.")]
    public string RecordFeedback(
        [Description("Note reference")] string note,
        [Description("pinned | hidden | useful | noisy | outdated | wrong | clear")] string signal,
        [Description("Short reason (recommended — future you will ask why)")] string? reason = null) =>
        Safe(() =>
        {
            var entry = ctx.Feedback.Record(note, signal, reason);
            return new { path = entry.Path, signal = entry.Signal, reason = entry.Reason };
        });

    [McpServerTool(Name = "mindvault_brain_ops", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("One-call brain state: health verdict, note/managed counts, index age, broken links, orphans, duplicate suspects, alias collisions, archived ratio, inbox drafts, open risks, active mistakes, feedback volume, latest session and recommended fixes. Counts only — no vault content.")]
    public string BrainOps() =>
        Safe(() => new { ops = ctx.Ops.Run() });

    [McpServerTool(Name = "mindvault_checkpoint_session", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Append a one-line mid-session checkpoint to the project's session log. Use sparingly — the handoff at mindvault_end_session is the entry that matters. Supports dryRun.")]
    public string CheckpointSession(
        [Description("Project name (alias or repo name also works)")] string project,
        [Description("One-line checkpoint summary")] string summary,
        [Description("Preview only — change nothing")] bool dryRun = false,
        [Description("Store even if the content gate flags secrets (default false)")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var r = ctx.Sessions.Log(project, summary, dryRun, allowRiskyContent);
            return new { dryRun, path = r.Path, snapshot = r.SnapshotPath, message = r.Message, riskWarnings = r.RiskWarnings };
        });

    [McpServerTool(Name = "mindvault_recent_sessions", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The latest handoff (###) and checkpoint (####) entries from the project's session log, newest first — read this when resuming work to see where the last session stopped.")]
    public string RecentSessions(
        [Description("Project name (alias or repo name also works)")] string project,
        [Description("Max entries (default 5)")] int limit = 5) =>
        Safe(() =>
        {
            var entries = ctx.Sessions.Recent(project, limit);
            return new { count = entries.Count, entries };
        });

    [McpServerTool(Name = "mindvault_list_inbox", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Unpromoted thought drafts in 00_Inbox and 06_Agent_Memory/Inbox, newest first. Promote a confirmed one with mindvault_promote_note; reject a dead one with mindvault_archive_note.")]
    public string ListInbox(
        [Description("Limit to one project (alias or repo name also works)")] string? project = null) =>
        Safe(() =>
        {
            var rows = BrainQueries.Inbox(ctx, project);
            return new
            {
                count = rows.Count,
                drafts = rows.Select(n => new { n.Title, n.Path, n.Project, n.Updated }),
            };
        });

    [McpServerTool(Name = "mindvault_add_mistake", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Record a durable lesson in the mistake ledger (06_Agent_Memory/Mistakes): what to never repeat, with the lesson and a prevention rule. Refuses near-duplicates; capsules surface active mistakes as do-not-repeat rules. Record one when a mistake has repeat-prevention value — not for routine bugs.")]
    public string AddMistake(
        [Description("Short mistake title, e.g. 'Trusted mtime after git restore'")] string title,
        [Description("Project name (optional; alias or repo name works)")] string? project = null,
        [Description("The lesson — why it happened and what it taught")] string? lesson = null,
        [Description("The prevention rule a future agent must follow")] string? prevention = null,
        [Description("Create even if a very similar mistake exists (default false)")] bool allowDuplicate = false,
        [Description("Store even if the content gate flags secrets (default false)")] bool allowRiskyContent = false) =>
        Safe(() => Created(ctx.Writer.CreateMistake(title, project, lesson, prevention, allowDuplicate, allowRiskyContent)));

    [McpServerTool(Name = "mindvault_list_mistakes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Active lessons from the mistake ledger (status active/open), newest first — the do-not-repeat list. Pass includeResolved for the full history.")]
    public string ListMistakes(
        [Description("Limit to one project (alias or repo name also works)")] string? project = null,
        [Description("Include resolved (status done) lessons too")] bool includeResolved = false) =>
        Safe(() =>
        {
            var rows = BrainQueries.Mistakes(ctx, project, includeResolved);
            return new
            {
                count = rows.Count,
                mistakes = rows.Select(n => new { n.Title, n.Path, n.Status, n.Project, n.Updated }),
            };
        });

    [McpServerTool(Name = "mindvault_resolve_mistake", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Mark a mistake's lesson as no longer active (status: done). It stays in the ledger for history; it just stops appearing in capsules and do-not-repeat lists.")]
    public string ResolveMistake(
        [Description("Note reference of the mistake")] string note) =>
        Safe(() =>
        {
            var r = ctx.Writer.ResolveMistake(note);
            return new { path = r.Path, snapshot = r.SnapshotPath, message = r.Message };
        });

    [McpServerTool(Name = "mindvault_promote_note", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Promote a thought (or untyped note) into durable memory: decision, memory, task, risk or mistake. Validates required fields, runs the duplicate gate, snapshots, rewrites frontmatter, links the project and moves the note to its correct folder. Body content is preserved verbatim; the file name never changes. Never guesses a project — pass one when the note has none.")]
    public string PromoteNote(
        [Description("Note reference of the thought/untyped note")] string noteRef,
        [Description("Target type: decision, memory, task, risk or mistake")] string to,
        [Description("Project name (required for decision/task/risk when the note has no project:)")] string? project = null,
        [Description("Promote even if a very similar note exists (default false)")] bool allowDuplicate = false) =>
        Safe(() =>
        {
            var r = ctx.Writer.PromoteNote(noteRef, to, project, allowDuplicate);
            return new
            {
                from = r.FromPath, to = r.ToPath, type = r.Type, status = r.Status,
                snapshot = r.SnapshotPath, warnings = r.Warnings, suggestions = r.Suggestions,
            };
        });

    [McpServerTool(Name = "mindvault_organize_vault", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Propose placement moves for misfiled notes with a reason per move (type=..., status=..., project=...). Dry-run by default — NOTHING moves unless apply is true, and apply only executes the safe high-confidence proposals (snapshot-first, atomic). Archived notes and templates are never touched; uncertain notes are returned under needsReview instead of moved.")]
    public string OrganizeVault(
        [Description("Limit to one project (alias or repo name also works)")] string? project = null,
        [Description("Execute the proposed moves (default false = dry-run)")] bool apply = false) =>
        Safe(() =>
        {
            var r = apply ? ctx.Organizer.Apply(project) : ctx.Organizer.Plan(project);
            return new
            {
                dryRun = r.DryRun, proposals = r.Proposals, needsReview = r.NeedsReview,
                warnings = r.Warnings, applied = r.Applied,
            };
        });

    [McpServerTool(Name = "mindvault_create_map", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a generated map-of-content note for a project in 09_Maps: hub link, current goal, key decisions, active tasks, open risks, mistakes, constraints, recent logs, reviews and orphans — a compact navigation layer for humans and agents. Fails if the map already exists (use mindvault_rebuild_map).")]
    public string CreateMap(
        [Description("Project name (alias or repo name also works)")] string project) =>
        Safe(() =>
        {
            var r = ctx.Maps.Create(project);
            return new { path = r.Path, message = r.Message, warnings = r.Warnings };
        });

    [McpServerTool(Name = "mindvault_rebuild_map", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Refresh a project map's generated block from current vault state (v2: start-here, agent route pointer, goal, non-negotiables, decisions/tasks/risks/mistakes, do-not-repeat rules, work areas, sessions, needs-review/orphans/broken-links health and an organisation score). Only the content between the mindvault-generated markers is rewritten — human text outside them is preserved verbatim. Snapshots first.")]
    public string RebuildMap(
        [Description("Project name (alias or repo name also works)")] string project) =>
        Safe(() =>
        {
            var r = ctx.Maps.Rebuild(project);
            return new { path = r.Path, snapshot = r.SnapshotPath, message = r.Message, warnings = r.Warnings };
        });

    [McpServerTool(Name = "mindvault_list_maps", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List the map-of-content notes in 09_Maps with their project and last update. Read a map for a compact project overview instead of listing the whole vault.")]
    public string ListMaps() =>
        Safe(() =>
        {
            var maps = ctx.Maps.List();
            return new { count = maps.Count, maps };
        });

    [McpServerTool(Name = "mindvault_get_project_map", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Read a project's map note — the cheapest orientation read: goal, decisions, risks, do-not-repeat rules and health in one payload. Prefer this over multiple searches when starting on a project.")]
    public string GetProjectMap(
        [Description("Project name (alias or repo name also works)")] string project) =>
        Safe(() =>
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
            var maps = ctx.Maps.List();
            var map = maps.FirstOrDefault(m =>
                string.Equals(m.Project, proj.Title, StringComparison.OrdinalIgnoreCase));
            if (map is null)
                throw new MindVaultException(
                    $"No map for {proj.Title}. Run mindvault_create_map first.");
            var raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, map.Path))
                .Replace("\r\n", "\n");
            FrontmatterCodec.TryExtract(raw, out _, out var body);
            if (body.Length > MaxBodyChars) body = body[..MaxBodyChars] + "\n… [truncated]";
            return new { path = map.Path, project = proj.Title, updated = map.Updated, content = body };
        });

    [McpServerTool(Name = "mindvault_build_route_card", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Agent navigation brief for a project (optionally focused by a goal, current source file or query): the 3-5 notes to READ FIRST with reasons, token estimates and summary snippets; what can wait; what NOT to read and why; constraints, decisions in force, do-not-repeat rules, risks and tasks; suggested next tool calls — all under a token budget. Call this BEFORE broad searches. Ambiguous projects return candidates.")]
    public string BuildRouteCard(
        [Description("Project name (alias or repo name also works)")] string project,
        [Description("What you are trying to achieve (free text)")] string? goal = null,
        [Description("Source file being edited (repo-relative path)")] string? currentFile = null,
        [Description("Free-text focus query (alternative to goal)")] string? query = null,
        [Description("Max read-first notes (default 5)")] int maxNotes = 0,
        [Description("Token budget for the read-first list (default 4000)")] int maxTokens = 0,
        [Description("'json' (default) or 'markdown'")] string format = "json") =>
        Safe(() =>
        {
            var budget = new ContextBudget(
                MaxNotes: maxNotes > 0 ? maxNotes : null,
                MaxEstimatedTokens: maxTokens > 0 ? maxTokens : null);
            var outcome = ctx.Routes.Build(project, goal, currentFile, query, budget);
            if (outcome.Card is null)
                return new { ambiguous = true, candidates = outcome.Candidates };
            return string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase)
                ? new { markdown = RouteCardService.ToMarkdown(outcome.Card) }
                : (object)new { routeCard = outcome.Card };
        });

    [McpServerTool(Name = "mindvault_build_read_plan", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Strict ordered read plan: at most 5 read_note steps (maps and hubs first), each with a reason and expected use, an explicit stop condition, do-not-read guidance and a narrowed search as the only fallback. Follow it literally and stop when the stop conditions are met — do not keep reading.")]
    public string BuildReadPlan(
        [Description("Project name (alias or repo name also works)")] string project,
        [Description("What you are trying to achieve (free text)")] string? goal = null,
        [Description("Source file being edited (repo-relative path)")] string? currentFile = null,
        [Description("Max reads (default and cap: 5)")] int maxReads = 5) =>
        Safe(() =>
        {
            var outcome = ctx.ReadPlans.Build(project, goal, currentFile, maxReads);
            if (outcome.Plan is null)
                return new { ambiguous = true, candidates = outcome.Candidates };
            return (object)new { readPlan = outcome.Plan };
        });

    [McpServerTool(Name = "mindvault_token_audit", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Where the tokens go: estimated totals (ceil(chars/4)) by tier, the largest notes, large notes lacking generated summaries (read raw = wasted tokens), notes worth splitting, capsule-vs-route cost, waste warnings and recommended fixes.")]
    public string TokenAudit(
        [Description("Limit to one project (alias or repo name also works)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.TokenAudit.Run(project);
            return new { tokenAudit = r };
        });

    [McpServerTool(Name = "mindvault_generate_summaries", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Deterministic extractive summary blocks (mindvault-summary markers) for large notes: one-line summary, agentUse, key points — generated from the note's own headings/frontmatter/body, never invented, no external LLM. Dry-run by default; apply=true splices only the generated block (snapshot-first) and preserves all human text. Low-quality summaries are marked needsReview.")]
    public string GenerateSummaries(
        [Description("Summarize one project's large notes (alias or repo name also works)")] string? project = null,
        [Description("Or summarize exactly one note (path, title or [[wiki link]])")] string? note = null,
        [Description("Write the blocks (default false = dry-run preview)")] bool apply = false) =>
        Safe(() =>
        {
            if (project is not null && note is not null)
                throw new MindVaultException("Pass 'project' or 'note', not both.");
            var r = note is not null
                ? ctx.Summaries.ForNote(note, apply)
                : ctx.Summaries.ForProject(project, apply);
            return new
            {
                dryRun = r.DryRun, notesConsidered = r.NotesConsidered,
                proposals = r.Proposals, applied = r.Applied, warnings = r.Warnings,
            };
        });

    [McpServerTool(Name = "mindvault_organisation_score", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Organisation score 0-100 across 11 explainable categories (folder placement, frontmatter, link/map/summary coverage, duplicate/orphan/stale risk, thought hygiene, token efficiency, agent readiness), each with its evidence, plus weaknesses, recommended fixes and estimated token waste/savings.")]
    public string OrganisationScore(
        [Description("Limit to one project (alias or repo name also works)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.OrgScore.Run(project);
            return new { score = r };
        });

    [McpServerTool(Name = "mindvault_build_graph", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Build the typed relationship graph from explicit wiki links (typed by endpoint note types: task_tracks_decision, mistake_prevented_by, risk_mitigated_by, supersedes, ...), frontmatter project membership and title-collision duplicates. Writes the operational sidecar .mindvault/link-graph.jsonl (disposable, like the index) and returns edge counts by type. Deterministic — no inference beyond what the vault already states.")]
    public string BuildGraph(
        [Description("Limit to edges touching one project (alias or repo name also works)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.Graph.Build(project);
            return new { notes = r.NoteCount, edges = r.EdgeCount, edgesByType = r.EdgesByType, sidecar = r.SidecarPath };
        });

    [McpServerTool(Name = "mindvault_explain_relationships", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Explain why two notes matter together: the direct typed edge between them, or the strongest two-hop path, with reasons and confidence. Computed live from the current vault, never stale.")]
    public string ExplainRelationships(
        [Description("First note (path, title or [[wiki link]])")] string from,
        [Description("Second note (path, title or [[wiki link]])")] string to) =>
        Safe(() =>
        {
            var r = ctx.Graph.Explain(from, to);
            return new { found = r.Found, path = r.Path, explanation = r.Explanation };
        });

    [McpServerTool(Name = "mindvault_find_low_value_notes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Notes agents should NOT read by default, each with explicit reasons: archived, superseded, rejected, hidden/negative feedback, raw thoughts, orphans, stale logs, missing/ambiguous project, large-without-summary. Guidance only — nothing is moved or deleted. Route cards and read plans already honour this list.")]
    public string FindLowValueNotes(
        [Description("Limit to one project (alias or repo name also works)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.LowValue.Find(project);
            return new { project = r.Project, scanned = r.Scanned, count = r.Notes.Count, notes = r.Notes, truncated = r.Truncated };
        });

    [McpServerTool(Name = "mindvault_compile_brain", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("One-pass organisation compiler: maps (create/rebuild), generated summaries, the typed link-graph sidecar, health reports and the organisation score. Dry-run by default — apply=true writes, snapshot-first, generated blocks only; human text is never touched. Never moves notes (that stays with mindvault_organize_vault).")]
    public string CompileBrain(
        [Description("Compile one project (alias or repo name also works); omit for all projects")] string? project = null,
        [Description("Write the artefacts (default false = dry-run preview)")] bool apply = false) =>
        Safe(() =>
        {
            var r = ctx.Compiler.Compile(project, apply);
            return new
            {
                dryRun = r.DryRun, project = r.Project, overallScore = r.OverallScore,
                artifacts = r.Artifacts, warnings = r.Warnings,
            };
        });

    [McpServerTool(Name = "mindvault_suggest_links", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Deterministic link suggestions with a reason and confidence per pair: type relationships within a project (decision↔task, risk↔task, ...), shared specific tags, shared title tokens and body mentions. Never applies anything — apply a suggestion with mindvault_link_notes. Archived notes, raw thoughts and already-linked pairs are excluded.")]
    public string SuggestLinks(
        [Description("Suggest for one note (path, title or [[wiki link]])")] string? note = null,
        [Description("Or suggest across a project (alias or repo name also works)")] string? project = null,
        [Description("Max suggestions (default 10 per note, 20 per project)")] int limit = 0) =>
        Safe(() =>
        {
            if (note is null == (project is null))
                throw new MindVaultException("Pass exactly one of 'note' or 'project'.");
            var suggestions = note is not null
                ? ctx.LinkIntel.SuggestForNote(note, limit > 0 ? limit : 10)
                : ctx.LinkIntel.SuggestForProject(project!, limit > 0 ? limit : 20);
            return new { count = suggestions.Count, suggestions };
        });

    [McpServerTool(Name = "mindvault_find_broken_links", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List wiki links whose target matches no note title or file name (frontmatter links and body [[links]] alike). Fix by creating the missing note, correcting the link text in Obsidian, or removing the stale link.")]
    public string FindBrokenLinks() =>
        Safe(() =>
        {
            var (rows, truncated) = ctx.LinkIntel.BrokenLinks();
            return new { count = rows.Count, truncated, broken = rows };
        });

    [McpServerTool(Name = "mindvault_find_orphans", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List managed, non-archived notes with no links in either direction — memory that nothing connects to. Raw thoughts are excluded (inbox captures are expected to be unlinked). Fix by linking them (mindvault_suggest_links helps) or archiving what is obsolete.")]
    public string FindOrphans() =>
        Safe(() =>
        {
            var (rows, truncated) = ctx.LinkIntel.Orphans();
            return new { count = rows.Count, truncated, orphans = rows };
        });

    [McpServerTool(Name = "mindvault_audit_frontmatter", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Frontmatter quality audit with a proposed fix per finding: missing/invalid keys, unresolvable or inconsistent project names, notes not linked to their project hub, project notes without aliases/repoNames. Read-only — nothing is auto-fixed.")]
    public string AuditFrontmatter(
        [Description("Limit to one project (alias or repo name also works)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.Audits.AuditFrontmatter(project);
            return new { notesChecked = r.NotesChecked, truncated = r.Truncated, findings = r.Findings };
        });

    [McpServerTool(Name = "mindvault_audit_aliases", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Alias hygiene audit across project notes: duplicate aliases, redundant self-aliases, cross-project alias collisions (which make detection refuse to guess) and projects missing aliases/repoNames. Read-only.")]
    public string AuditAliases() =>
        Safe(() =>
        {
            var r = ctx.Audits.AuditAliases();
            return new { projectsChecked = r.NotesChecked, truncated = r.Truncated, findings = r.Findings };
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
        [Description("Remaining risks or follow-ups (comma-separated), or omit for none")] string? followUps = null,
        [Description("Preview only — change nothing")] bool dryRun = false,
        [Description("Store even if the content gate flags secrets (default false)")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var result = ctx.Sessions.End(project, summary, tests, followUps, dryRun, allowRiskyContent);
            return new { dryRun, path = result.Path, snapshot = result.SnapshotPath, message = result.Message, riskWarnings = result.RiskWarnings };
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
