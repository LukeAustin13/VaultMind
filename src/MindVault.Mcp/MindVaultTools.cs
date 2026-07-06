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
    [Description("Vault status: name, whether the index exists, note count, rescan-pending flag, last scan time.")]
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
    [Description("Ranked full-text search (title-weighted, recency-boosted; archived excluded by default). Returns title, path, type, project, status, section and snippet per hit. A project filter finding nothing falls back vault-wide.")]
    public string Search(
        [Description("Search query (FTS5 syntax; plain words work)")] string query,
        [Description("Filter by note type, e.g. decision, task")] string? type = null,
        [Description("Preferred project scope (falls back vault-wide)")] string? project = null,
        [Description("Filter by tag")] string? tag = null,
        [Description("Filter by status, e.g. open, done")] string? status = null,
        [Description("Max results (default 10, max 100)")] int limit = 10,
        [Description("Only notes updated on/after (yyyy-MM-dd)")] string? updatedAfter = null,
        [Description("Only notes updated on/before (yyyy-MM-dd)")] string? updatedBefore = null,
        [Description("Include archived (heavily deprioritised)")] bool includeArchived = false,
        [Description("Include ranking factors for debugging")] bool explain = false,
        [Description("Snippet length (default 240; 0 = no snippets; max 1000)")] int snippetChars = SearchService.DefaultSnippetChars) =>
        Safe(() =>
        {
            var results = ctx.Search.Search(query, type, project, tag, status, limit,
                updatedAfter, updatedBefore, includeArchived, explain, snippetChars);
            return new { count = results.Count, results };
        });

    [McpServerTool(Name = "mindvault_read_note", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Read one note by reference. Ambiguous refs return candidates, not a guess. To save tokens pass 'section' for one heading or 'maxChars' to cap the body — prefer over full reads.")]
    public string ReadNote(
        [Description("Note ref: path, title, filename or [[wiki link]]")] string noteRef,
        [Description("Return only this heading's content")] string? section = null,
        [Description("Cap the body at N chars (0 = default cap)")] int maxChars = 0) =>
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
    [Description("List indexed notes, optionally filtered by type/project/status/tag, newest updated first.")]
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
    [Description("Create a new project note with valid frontmatter and the standard section skeleton. Refuses a name that already resolves to a project (including via alias) unless allowDuplicate.")]
    public string CreateProject(
        [Description("Project name (also becomes the file name)")] string name,
        [Description("Create even if a similar project exists")] bool allowDuplicate = false) =>
        Safe(() => Created(ctx.Writer.CreateProject(name, allowDuplicate)));

    [McpServerTool(Name = "mindvault_create_decision", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a decision note linked to an existing project (aliases resolve). Fails if the project is missing; refuses near-duplicate titles unless allowDuplicate — update or supersede the existing decision instead.")]
    public string CreateDecision(
        [Description("Existing project (alias/repo name ok)")] string project,
        [Description("Decision title")] string title,
        [Description("Create even if a similar decision exists")] bool allowDuplicate = false) =>
        Safe(() => Created(ctx.Writer.CreateDecision(project, title, allowDuplicate)));

    [McpServerTool(Name = "mindvault_create_task", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Create a task note linked to an existing project (aliases resolve). Fails if the project is missing; refuses near-duplicate titles unless allowDuplicate — update the existing task instead.")]
    public string CreateTask(
        [Description("Existing project (alias/repo name ok)")] string project,
        [Description("Task title")] string title,
        [Description("Create even if a similar task exists")] bool allowDuplicate = false) =>
        Safe(() => Created(ctx.Writer.CreateTask(project, title, allowDuplicate)));

    [McpServerTool(Name = "mindvault_append_to_note", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Append content under a note's existing heading (snapshots first). Errors if the heading is missing unless createSection. dryRun previews.")]
    public string AppendToNote(
        [Description("Note ref: path, title, filename or [[wiki link]]")] string noteRef,
        [Description("Heading to append under (no # markers)")] string section,
        [Description("Markdown content to append")] string content,
        [Description("Add the section at the note's end if missing")] bool createSection = false,
        [Description("Preview only — change nothing")] bool dryRun = false,
        [Description("Store even if the secret gate flags it")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.AppendToSection(noteRef, section, content, createSection, dryRun, allowRiskyContent);
            return new { dryRun, path = result.Path, message = result.Message, snapshot = result.SnapshotPath, riskWarnings = result.RiskWarnings };
        });

    [McpServerTool(Name = "mindvault_update_frontmatter", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Set one flat frontmatter key (e.g. status). Nested YAML rejected; for tags/links pass a comma-separated list. Snapshots first. dryRun previews old -> new.")]
    public string UpdateFrontmatter(
        [Description("Note reference")] string noteRef,
        [Description("Frontmatter key, e.g. status")] string key,
        [Description("New value (comma-separated for tags/links)")] string value,
        [Description("Preview only — change nothing")] bool dryRun = false,
        [Description("Store even if the secret gate flags it")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.UpdateFrontmatter(noteRef, key, value, dryRun, allowRiskyContent);
            return new { dryRun, path = result.Path, message = result.Message, snapshot = result.SnapshotPath, riskWarnings = result.RiskWarnings };
        });

    [McpServerTool(Name = "mindvault_link_notes", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Add a [[wiki link]] to the source note's frontmatter links, pointing at the target. Snapshots first; no-op if already linked.")]
    public string LinkNotes(
        [Description("Source note reference")] string fromRef,
        [Description("Target note reference")] string toRef) =>
        Safe(() =>
        {
            var result = ctx.Writer.LinkNotes(fromRef, toRef);
            return new { path = result.Path, changed = result.Changed, message = result.Message };
        });

    [McpServerTool(Name = "mindvault_archive_note", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Archive a note instead of deleting: snapshot, set status archived, move to the archive folder, reindex. There is no delete tool. dryRun previews the move.")]
    public string ArchiveNote(
        [Description("Note reference")] string noteRef,
        [Description("Preview only — change nothing")] bool dryRun = false) =>
        Safe(() =>
        {
            var result = ctx.Writer.Archive(noteRef, dryRun);
            return new { dryRun, from = result.FromPath, to = result.ToPath, snapshot = result.SnapshotPath, warnings = result.Warnings };
        });

    [McpServerTool(Name = "mindvault_capture_thought", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Capture a raw, uncertain idea as a thought note in the agent inbox — NOT durable memory. Use instead of creating decisions/memories you are unsure about; promote later with promote_note. List with list_inbox.")]
    public string CaptureThought(
        [Description("Short thought title")] string title,
        [Description("Optional body text for the Thought section")] string? content = null,
        [Description("Project to tag with (alias/repo ok)")] string? project = null,
        [Description("Store even if the secret gate flags it")] bool allowRiskyContent = false) =>
        Safe(() => Created(ctx.Writer.CaptureThought(title, content, agentInbox: true, project, allowRiskyContent)));

    [McpServerTool(Name = "mindvault_build_context_capsule", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("The full memory to hold before working: decisions, tasks, risks, constraints, do-not-repeat rules — the facts (build_route_card points at notes). mode reshapes priority (coding|debugging|review|planning|handoff|release|architecture); budgeted.")]
    public string BuildContextCapsule(
        [Description("Project (alias/repo name ok)")] string project,
        [Description("Mode (see tool description); default coding")] string mode = "coding",
        [Description("Char budget for the capsule (default 8000)")] int maxChars = CapsuleService.DefaultBudget,
        [Description("'json' (default) or 'markdown'")] string format = "json",
        [Description("Include the flat sourcePaths list")] bool includeSources = false) =>
        Safe(() =>
        {
            var outcome = ctx.Capsules.Build(project, mode, maxChars);
            if (outcome.Capsule is null)
                return (object)new { ambiguous = true, candidates = outcome.Candidates };
            var capsule = includeSources ? outcome.Capsule : outcome.Capsule with { SourcePaths = [] };
            return string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase)
                ? new { ambiguous = false, markdown = CapsuleService.ToMarkdown(capsule) }
                : (object)new { ambiguous = false, capsule };
        });

    [McpServerTool(Name = "mindvault_get_work_context", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Memory tied to what you are touching RIGHT NOW — pass exactly one of currentFile, query or note. Returns decisions/tasks/risks/mistakes/logs each with why it matched, plus reads. Use before risky edits.")]
    public string GetWorkContext(
        [Description("Project (alias/repo name ok)")] string project,
        [Description("Source file you are editing (repo-relative path)")] string? currentFile = null,
        [Description("Free-text query describing the work")] string? query = null,
        [Description("Note reference to expand from")] string? note = null,
        [Description("Max results per group (default 12)")] int limit = 12) =>
        Safe(() => new { workContext = ctx.WorkContext.Get(project, currentFile, query, note, limit) });

    [McpServerTool(Name = "mindvault_recall", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("What changed in a window: decisions/tasks/risks/mistakes/logs/reviews/notes created or updated since a date ('7 days', yyyy-MM-dd, or 'last-handoff'), or on this day in earlier years. Frontmatter dates, mtime fallback.")]
    public string Recall(
        [Description("Project name (optional — omit for vault-wide)")] string? project = null,
        [Description("Window: '7 days' or a yyyy-MM-dd date (default 7 days)")] string? since = null,
        [Description("Anniversaries: same month/day in earlier years")] bool onThisDay = false) =>
        Safe(() => new { recall = ctx.RecallSvc.Recall(project, since, onThisDay) });

    [McpServerTool(Name = "mindvault_record_feedback", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Record retrieval feedback for a note: pinned (always surface), hidden (never), useful, noisy, outdated, wrong, or clear. Stored in a sidecar — Markdown untouched. Shapes capsules, work-context and link suggestions.")]
    public string RecordFeedback(
        [Description("Note reference")] string note,
        [Description("pinned | hidden | useful | noisy | outdated | wrong | clear")] string signal,
        [Description("Short reason (recommended)")] string? reason = null) =>
        Safe(() =>
        {
            var entry = ctx.Feedback.Record(note, signal, reason);
            return new { path = entry.Path, signal = entry.Signal, reason = entry.Reason };
        });

    [McpServerTool(Name = "mindvault_brain_ops", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("One-call brain state: health verdict, counts, index age, broken links, orphans, duplicate suspects, alias collisions, inbox drafts, open risks, active mistakes, latest session, fixes. Counts only — no vault content.")]
    public string BrainOps() =>
        Safe(() => new { ops = ctx.Ops.Run() });

    [McpServerTool(Name = "mindvault_checkpoint_session", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Append a one-line mid-session checkpoint to the session log. Use sparingly — the handoff at end_session is the entry that matters. Supports dryRun.")]
    public string CheckpointSession(
        [Description("Project (alias/repo name ok)")] string project,
        [Description("One-line checkpoint summary")] string summary,
        [Description("Preview only — change nothing")] bool dryRun = false,
        [Description("Store even if the secret gate flags it")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var r = ctx.Sessions.Log(project, summary, dryRun, allowRiskyContent);
            return new { dryRun, path = r.Path, snapshot = r.SnapshotPath, message = r.Message, riskWarnings = r.RiskWarnings };
        });

    [McpServerTool(Name = "mindvault_recent_sessions", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Latest handoff (###) and checkpoint (####) entries from the project's session log, newest first — read when resuming to see where the last session stopped.")]
    public string RecentSessions(
        [Description("Project (alias/repo name ok)")] string project,
        [Description("Max entries (default 5)")] int limit = 5) =>
        Safe(() =>
        {
            var entries = ctx.Sessions.Recent(project, limit);
            return new { count = entries.Count, entries };
        });

    [McpServerTool(Name = "mindvault_list_inbox", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Unpromoted thought drafts in the inboxes, newest first. Promote a confirmed one with promote_note; reject a dead one with archive_note.")]
    public string ListInbox(
        [Description("Limit to one project (alias/repo ok)")] string? project = null) =>
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
    [Description("Record a durable lesson in the mistake ledger: what to never repeat, with a lesson and prevention rule. Refuses near-duplicates; capsules surface these as do-not-repeat rules. Only for repeat-prevention value.")]
    public string AddMistake(
        [Description("Short mistake title")] string title,
        [Description("Project (optional; alias/repo ok)")] string? project = null,
        [Description("The lesson — why it happened, what it taught")] string? lesson = null,
        [Description("The prevention rule a future agent must follow")] string? prevention = null,
        [Description("Create even if a similar mistake exists")] bool allowDuplicate = false,
        [Description("Store even if the secret gate flags it")] bool allowRiskyContent = false) =>
        Safe(() => Created(ctx.Writer.CreateMistake(title, project, lesson, prevention, allowDuplicate, allowRiskyContent)));

    [McpServerTool(Name = "mindvault_list_mistakes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Active lessons from the mistake ledger, newest first — the do-not-repeat list. includeResolved for full history.")]
    public string ListMistakes(
        [Description("Limit to one project (alias/repo ok)")] string? project = null,
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
    [Description("Mark a mistake's lesson no longer active (status: done). Stays in the ledger for history; stops appearing in capsules and do-not-repeat lists.")]
    public string ResolveMistake(
        [Description("Note reference of the mistake")] string note) =>
        Safe(() =>
        {
            var r = ctx.Writer.ResolveMistake(note);
            return new { path = r.Path, snapshot = r.SnapshotPath, message = r.Message };
        });

    [McpServerTool(Name = "mindvault_promote_note", Destructive = true, Idempotent = false, OpenWorld = false)]
    [Description("Promote a thought/untyped note into durable memory (decision, memory, task, risk or mistake): validates, runs the duplicate gate, rewrites frontmatter, links and moves it. Body preserved. Never guesses.")]
    public string PromoteNote(
        [Description("Note reference of the thought/untyped note")] string noteRef,
        [Description("Target type: decision, memory, task, risk or mistake")] string to,
        [Description("Project (required for decision/task/risk if the note has none)")] string? project = null,
        [Description("Promote even if a similar note exists")] bool allowDuplicate = false) =>
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
    [Description("Propose placement moves for misfiled notes, one reason per move. Dry-run by default; apply=true executes only safe high-confidence moves (snapshot-first, atomic). Archived/templates untouched; uncertain notes go under needsReview.")]
    public string OrganizeVault(
        [Description("Limit to one project (alias/repo ok)")] string? project = null,
        [Description("Execute the moves (default false = dry-run)")] bool apply = false) =>
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
    [Description("First-time build of the generated map block on a project's hub note (start-here, decisions, tasks, risks, do-not-repeat, health, org score). Fails if one exists — use rebuild_map. Snapshots first.")]
    public string CreateMap(
        [Description("Project (alias/repo name ok)")] string project) =>
        Safe(() =>
        {
            var r = ctx.Maps.Create(project);
            return new { path = r.Path, snapshot = r.SnapshotPath, message = r.Message, warnings = r.Warnings };
        });

    [McpServerTool(Name = "mindvault_rebuild_map", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Refresh an existing map block (see create_map) from current vault state. Only content between the generated markers is rewritten; human text preserved. Snapshots first; no-op when current.")]
    public string RebuildMap(
        [Description("Project (alias/repo name ok)")] string project) =>
        Safe(() =>
        {
            var r = ctx.Maps.Rebuild(project);
            return new { path = r.Path, snapshot = r.SnapshotPath, message = r.Message, warnings = r.Warnings };
        });

    [McpServerTool(Name = "mindvault_list_maps", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List project hubs and whether each carries a generated map block, plus any legacy 09_Maps files (flagged to migrate).")]
    public string ListMaps() =>
        Safe(() =>
        {
            var maps = ctx.Maps.List();
            return new { count = maps.Count, maps };
        });

    [McpServerTool(Name = "mindvault_get_project_map", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Read the pre-generated map block stored on a project's hub note — the cheapest orientation read (no computation). Static; rebuild_map refreshes it. Empty until create_map has run.")]
    public string GetProjectMap(
        [Description("Project (alias/repo name ok)")] string project) =>
        Safe(() =>
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project);
            var raw = File.ReadAllText(PathGuard.ResolveNotePath(ctx.VaultRoot, proj.Path))
                .Replace("\r\n", "\n");
            FrontmatterCodec.TryExtract(raw, out _, out var body);
            var located = GeneratedBlocks.Locate(body, MapService.MarkerStart, MapService.MarkerEnd);
            if (located.Kind == GeneratedBlocks.BlockKind.Ambiguous)
                throw new MindVaultException(
                    MapService.AmbiguityMessage(proj.Title, located.StartCount, located.EndCount));
            if (located.Kind != GeneratedBlocks.BlockKind.Single)
                throw new MindVaultException(
                    $"No map block on {proj.Title}. Run mindvault_create_map first.");
            var content = body[(located.Start + MapService.MarkerStart.Length)..located.End].Trim('\n');
            if (content.Length > MaxBodyChars) content = content[..MaxBodyChars] + "\n… [truncated]";
            return new { path = proj.Path, project = proj.Title, updated = proj.Updated, content };
        });

    [McpServerTool(Name = "mindvault_build_route_card", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("WHICH notes to read (and skip) before broad searching, ranked under a token budget: 3-5 to read first with reasons, what can wait, what NOT to read. build_context_capsule gives the facts. Focus by goal, file or query.")]
    public string BuildRouteCard(
        [Description("Project (alias/repo name ok)")] string project,
        [Description("What you are trying to achieve")] string? goal = null,
        [Description("Source file being edited (repo-relative)")] string? currentFile = null,
        [Description("Free-text focus query (alternative to goal)")] string? query = null,
        [Description("Max read-first notes (default 5)")] int maxNotes = 0,
        [Description("Token budget for read-first (default 4000)")] int maxTokens = 0,
        [Description("'json' (default) or 'markdown'")] string format = "json",
        [Description("Include the flat sourcePaths list")] bool includeSources = false) =>
        Safe(() =>
        {
            var budget = new ContextBudget(
                MaxNotes: maxNotes > 0 ? maxNotes : null,
                MaxEstimatedTokens: maxTokens > 0 ? maxTokens : null);
            var outcome = ctx.Routes.Build(project, goal, currentFile, query, budget);
            if (outcome.Card is null)
                return new { ambiguous = true, candidates = outcome.Candidates };
            var card = includeSources ? outcome.Card : outcome.Card with { SourcePaths = [] };
            return string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase)
                ? new { markdown = RouteCardService.ToMarkdown(card) }
                : (object)new { routeCard = card };
        });

    [McpServerTool(Name = "mindvault_build_read_plan", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("A strict ordered read_note sequence (<=5 steps) with an explicit stop condition — follow literally and stop when met. build_route_card ranks candidates; this dictates exact steps. For zero discretion about what to read next.")]
    public string BuildReadPlan(
        [Description("Project (alias/repo name ok)")] string project,
        [Description("What you are trying to achieve")] string? goal = null,
        [Description("Source file being edited (repo-relative)")] string? currentFile = null,
        [Description("Max reads (default and cap: 5)")] int maxReads = 5) =>
        Safe(() =>
        {
            var outcome = ctx.ReadPlans.Build(project, goal, currentFile, maxReads);
            if (outcome.Plan is null)
                return new { ambiguous = true, candidates = outcome.Candidates };
            return (object)new { readPlan = outcome.Plan };
        });

    [McpServerTool(Name = "mindvault_token_audit", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Where the tokens go: estimated totals by tier, largest notes, large notes lacking summaries, notes worth splitting, capsule-vs-route cost, waste warnings and fixes.")]
    public string TokenAudit(
        [Description("Limit to one project (alias/repo ok)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.TokenAudit.Run(project);
            return new { tokenAudit = r };
        });

    [McpServerTool(Name = "mindvault_generate_summaries", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Splice extractive summary blocks (summary, agentUse, key points) into large notes — from the note's own text, no LLM. Dry-run by default; apply=true writes the generated block only.")]
    public string GenerateSummaries(
        [Description("Summarize one project's large notes (alias/repo ok)")] string? project = null,
        [Description("Or summarize exactly one note (ref)")] string? note = null,
        [Description("Write the blocks (default false = dry-run)")] bool apply = false) =>
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
    [Description("Organisation score 0-100 across 11 explainable categories (placement, frontmatter, link/map/summary coverage, duplicate/orphan/stale risk, thought hygiene, token efficiency, agent readiness), with evidence and fixes.")]
    public string OrganisationScore(
        [Description("Limit to one project (alias/repo ok)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.OrgScore.Run(project);
            return new { score = r };
        });

    [McpServerTool(Name = "mindvault_build_graph", Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Build the typed relationship graph from wiki links (typed by endpoint, e.g. task_tracks_decision, supersedes), project membership and title collisions. Writes the disposable link-graph.jsonl sidecar, returns edge counts by type.")]
    public string BuildGraph(
        [Description("Limit to edges touching one project (alias/repo ok)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.Graph.Build(project);
            return new { notes = r.NoteCount, edges = r.EdgeCount, edgesByType = r.EdgesByType, sidecar = r.SidecarPath };
        });

    [McpServerTool(Name = "mindvault_explain_relationships", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Explain why two notes matter together: the direct typed edge or the strongest two-hop path, with reasons and confidence. Computed live.")]
    public string ExplainRelationships(
        [Description("First note (path, title or [[wiki link]])")] string from,
        [Description("Second note (path, title or [[wiki link]])")] string to) =>
        Safe(() =>
        {
            var r = ctx.Graph.Explain(from, to);
            return new { found = r.Found, path = r.Path, explanation = r.Explanation };
        });

    [McpServerTool(Name = "mindvault_find_low_value_notes", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Notes agents should NOT read by default, each with reasons (archived, superseded, rejected, negative feedback, raw thoughts, orphans, stale logs, large-without-summary). Route cards/read plans honour this.")]
    public string FindLowValueNotes(
        [Description("Limit to one project (alias/repo ok)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.LowValue.Find(project);
            return new { project = r.Project, scanned = r.Scanned, count = r.Notes.Count, notes = r.Notes, truncated = r.Truncated };
        });

    [McpServerTool(Name = "mindvault_compile_brain", Destructive = true, Idempotent = true, OpenWorld = false)]
    [Description("Run rebuild_map + generate_summaries + build_graph + org score in one pass. Dry-run by default; apply=true writes generated blocks only. Never moves notes (that is organize_vault).")]
    public string CompileBrain(
        [Description("Compile one project (alias/repo ok); omit for all")] string? project = null,
        [Description("Write the artefacts (default false = dry-run)")] bool apply = false) =>
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
    [Description("Suggest note links with a reason and confidence per pair (type relationships, shared tags/title tokens, body mentions). Never applies — pass one to link_notes. Excludes archived, raw thoughts and linked pairs.")]
    public string SuggestLinks(
        [Description("Suggest for one note (ref)")] string? note = null,
        [Description("Or suggest across a project (alias/repo ok)")] string? project = null,
        [Description("Max suggestions (default 10/note, 20/project)")] int limit = 0) =>
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
    [Description("List wiki links whose target matches no note title or file name (frontmatter and body [[links]] alike). Fix by creating the missing note, correcting the link, or removing it.")]
    public string FindBrokenLinks() =>
        Safe(() =>
        {
            var (rows, truncated) = ctx.LinkIntel.BrokenLinks();
            return new { count = rows.Count, truncated, broken = rows };
        });

    [McpServerTool(Name = "mindvault_find_orphans", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("List managed, non-archived notes with no links in either direction — memory nothing connects to. Raw thoughts excluded. Fix by linking (suggest_links helps) or archiving what is obsolete.")]
    public string FindOrphans() =>
        Safe(() =>
        {
            var (rows, truncated) = ctx.LinkIntel.Orphans();
            return new { count = rows.Count, truncated, orphans = rows };
        });

    [McpServerTool(Name = "mindvault_audit_frontmatter", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Frontmatter audit with a proposed fix per finding: missing/invalid keys, unresolvable/inconsistent project names, notes not linked to their hub, projects lacking aliases/repoNames. Read-only.")]
    public string AuditFrontmatter(
        [Description("Limit to one project (alias/repo ok)")] string? project = null) =>
        Safe(() =>
        {
            var r = ctx.Audits.AuditFrontmatter(project);
            return new { notesChecked = r.NotesChecked, truncated = r.Truncated, findings = r.Findings };
        });

    [McpServerTool(Name = "mindvault_audit_aliases", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Alias hygiene audit: duplicate aliases, redundant self-aliases, cross-project collisions (which make detection refuse to guess), projects missing aliases/repoNames. Read-only.")]
    public string AuditAliases() =>
        Safe(() =>
        {
            var r = ctx.Audits.AuditAliases();
            return new { projectsChecked = r.NotesChecked, truncated = r.Truncated, findings = r.Findings };
        });

    [McpServerTool(Name = "mindvault_detect_project", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Map a repo/folder name or shorthand to a vault project via titles, aliases, repoNames and separator-insensitive match. Returns the match with a confidence tier, or candidates when ambiguous — never guesses.")]
    public string DetectProject(
        [Description("Repo folder, shorthand or alias, e.g. 'mind-vault'")] string name) =>
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
    [Description("Related notes for one note, each with a reason: outgoing links, backlinks, active same-project memory, same-type similar titles. Finds the tasks/risks/reviews around a decision in one call.")]
    public string FindRelated(
        [Description("Note ref: path, title, filename or [[wiki link]]")] string noteRef,
        [Description("Max related notes (default 12, max 50)")] int limit = RelatedNotesService.DefaultLimit) =>
        Safe(() =>
        {
            var result = ctx.Related.Get(noteRef, limit);
            return new { note = result.Title, path = result.Path, count = result.Related.Count, related = result.Related };
        });

    [McpServerTool(Name = "mindvault_validate_vault", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Validate the vault: folders, frontmatter schema, nested YAML, duplicate titles, broken links, statuses, project refs, stale tasks, superseded-decision contradictions, environment. Returns severity counts plus top issues.")]
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
    [Description("Raw project bundle (goal, tasks, decisions, risks, constraints, logs, warnings) at a chosen detail level. Plumbing under the orientation tools — prefer start_session or build_context_capsule unless you want the raw bundle.")]
    public string GetProjectContext(
        [Description("Project name")] string project,
        [Description("Max items per list (default 10, max 50)")] int limit = 10,
        [Description("Detail level: brief, standard or deep")] string detailLevel = "standard") =>
        Safe(() => ctx.Projects.Get(project, limit, detailLevel));

    [McpServerTool(Name = "mindvault_get_context_pack", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Briefing pack keyed to a task description: pass what you are about to do and task-relevant notes surface first. build_context_capsule is mode-shaped and budgeted; this is task-shaped. Carries refs.")]
    public string GetContextPack(
        [Description("Project name")] string project,
        [Description("What you are about to work on (optional)")] string? task = null,
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
    [Description("Quality-check a note idea BEFORE creating it: duplicate detection, missing project, vague titles, supersede suggestions. Blockers mean the create would fail or duplicate; warnings advise.")]
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
    [Description("Replace one decision with another: old gets status 'superseded' + a superseded_by link, new gets a supersedes link. Both snapshotted first; if the second write fails the first rolls back.")]
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
    [Description("The one-call brief to start work: goal, non-negotiables, decisions, do-not-repeat rules, open/blocked tasks, risks, constraints, notes to read first / skip, and the delta since the last handoff — budgeted, each fact once.")]
    public string StartSession(
        [Description("Project name")] string project,
        [Description("What this session will work on (optional)")] string? task = null,
        [Description("Char budget for the brief (default 6000, 1000-32000)")] int maxChars = BriefService.DefaultBudget) =>
        Safe(() => new { brief = ctx.Sessions.StartBrief(project, task, maxChars) });

    [McpServerTool(Name = "mindvault_end_session", Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Write one concise handoff (summary, tests, follow-ups) AND optionally batch durable changes: decisions, mistakes, task creates/status-changes. Each item runs its normal gates; one bad item never aborts the rest.")]
    public string EndSession(
        [Description("Project name")] string project,
        [Description("One-line summary of what was accomplished")] string summary,
        [Description("Tests/builds run and result")] string? tests = null,
        [Description("Remaining risks/follow-ups (comma-separated), or omit")] string? followUps = null,
        [Description("Decisions to record: each {title, content?}")] CloseDecision[]? decisions = null,
        [Description("Mistakes to record: each {title, lesson?, prevention?}")] CloseMistake[]? mistakes = null,
        [Description("Tasks: {ref, status} to update OR {title} to create")] CloseTask[]? tasks = null,
        [Description("Preview only — change nothing")] bool dryRun = false,
        [Description("Store even if the secret gate flags it")] bool allowRiskyContent = false) =>
        Safe(() =>
        {
            var hasBatch = decisions is { Length: > 0 } || mistakes is { Length: > 0 } || tasks is { Length: > 0 };
            if (!hasBatch)
            {
                var basic = ctx.Sessions.End(project, summary, tests, followUps, dryRun, allowRiskyContent);
                return new { dryRun, path = basic.Path, snapshot = basic.SnapshotPath,
                    message = basic.Message, riskWarnings = basic.RiskWarnings, items = Array.Empty<object>() };
            }
            var items = new SessionCloseItems(decisions, mistakes, tasks);
            var result = ctx.Sessions.Close(project, summary, tests, followUps, items, dryRun, allowRiskyContent);
            return new { dryRun = result.DryRun, path = result.Path, snapshot = result.SnapshotPath,
                message = result.Message, riskWarnings = result.RiskWarnings, items = (object)result.Items };
        });

    [McpServerTool(Name = "mindvault_health", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Fast health check with a verdict (good/warning/critical): vault writable, index exists/stale, note count, last scan and version. No secrets, env vars or host paths.")]
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
    [Description("Deeper diagnostics: health plus transport, index schema version, validation summary and warnings. Runs a scan+validation, so slower than health. No secrets, env vars or host paths.")]
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
    [Description("Clear and rebuild the SQLite index from the Markdown files (files are canonical; the index is disposable cache).")]
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
