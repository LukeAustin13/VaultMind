namespace MindVault.Core;

public sealed record MoveNoteResult(string FromPath, string ToPath, string SnapshotPath);

public sealed record PromoteResult(
    string FromPath, string ToPath, string Type, string Status, string SnapshotPath,
    IReadOnlyList<string> Warnings, IReadOnlyList<string> Suggestions);

/// <summary>
/// Organisation mutations: capture a raw thought, move a note (snapshot-first, atomic,
/// reindexed), promote a thought into durable memory, and rebuild a generated body.
/// Lives in the WriteService partial so it shares the snapshot → write → re-verify →
/// reindex pipeline and the same locking discipline as every other mutation.
/// </summary>
public sealed partial class WriteService
{
    private static readonly string[] PromotionTargets = ["decision", "memory", "task", "risk", "mistake"];
    private static readonly string[] ProjectRequiredTargets = ["decision", "task", "risk"];

    // ---------- capture ----------

    /// <summary>
    /// Captures a raw thought into an inbox. Thoughts are cheap and transient by design, so
    /// only an exact file collision blocks — the duplicate gate applies at promotion time,
    /// when a thought tries to become durable memory. The content gate still applies:
    /// secrets have no business even in the inbox.
    /// </summary>
    public CreateNoteResult CaptureThought(string title, string? content = null, bool agentInbox = false,
        string? project = null, bool allowRiskyContent = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var risk = ContentRiskScanner.Gate(content, allowRiskyContent);
            var proj = string.IsNullOrWhiteSpace(project)
                ? null
                : ctx.ProjectDetect.ResolveOrThrow(project.Trim()).Project;
            var clean = SlugHelper.SanitizeFileName(title);
            var folder = agentInbox ? "06_Agent_Memory/Inbox" : "00_Inbox";
            var note = CreateNote($"{folder}/{clean}.md",
                NoteTemplates.Thought(clean, Today, content, proj?.Title, proj?.Stem),
                $"A thought named '{clean}' already exists");
            return new CreateNoteResult(note, risk);
        }
    }

    // ---------- mistake ledger ----------

    /// <summary>
    /// Records a durable lesson in the mistake ledger (06_Agent_Memory/Mistakes). Runs the
    /// duplicate gate and the content gate; lesson/prevention land in their sections so the
    /// note is useful immediately.
    /// </summary>
    public CreateNoteResult CreateMistake(string title, string? project = null, string? lesson = null,
        string? prevention = null, bool allowDuplicate = false, bool allowRiskyContent = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var risk = ContentRiskScanner.Gate($"{lesson}\n{prevention}", allowRiskyContent);
            var proj = string.IsNullOrWhiteSpace(project)
                ? null
                : ctx.ProjectDetect.ResolveOrThrow(project.Trim()).Project;
            var clean = SlugHelper.SanitizeFileName(title);
            var warnings = DraftWarnings("mistake", proj?.Title, clean, allowDuplicate);
            var note = CreateNote($"06_Agent_Memory/Mistakes/Mistake - {clean}.md",
                NoteTemplates.Mistake(clean, proj?.Title, proj?.Stem, Today, lesson, prevention),
                $"A mistake named '{clean}' already exists");
            return new CreateNoteResult(note, warnings.Concat(risk).ToList());
        }
    }

    /// <summary>Marks a mistake's lesson as no longer active (status: done). It stays in the ledger.</summary>
    public WriteResult ResolveMistake(string noteRef)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var note = ctx.Resolver.Resolve(noteRef);
            if (!string.Equals(note.Type, "mistake", StringComparison.OrdinalIgnoreCase))
                throw new MindVaultException(
                    $"{note.Path} is not a mistake note (type: {note.Type ?? "none"}).");
            return UpdateFrontmatterCore(note.Path, "status", "done", dryRun: false);
        }
    }

    // ---------- move ----------

    /// <summary>
    /// Moves a note to another vault folder (optionally renaming it): snapshot first, then an
    /// atomic move, then reindex of both sides. Wiki links target titles/stems, so a plain
    /// move never breaks links; renames are the caller's responsibility to gate on backlinks.
    /// </summary>
    public MoveNoteResult MoveNote(string noteRef, string destFolder, string? newFileName = null)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            return MoveNoteCore(noteRef, destFolder, newFileName);
        }
    }

    private MoveNoteResult MoveNoteCore(string noteRef, string destFolder, string? newFileName)
    {
        var note = ctx.Resolver.Resolve(noteRef);
        if (note.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
            throw new MindVaultException("Templates are managed by 'init' and cannot be moved.");

        destFolder = (destFolder ?? "").Trim().Trim('/');
        if (destFolder.Length == 0)
            throw new MindVaultException("Destination folder must not be empty.");

        var fileName = newFileName is null
            ? Path.GetFileName(note.Path)
            : SlugHelper.SanitizeFileName(
                  newFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                      ? newFileName[..^3]
                      : newFileName) + ".md";

        var abs = ctx.Resolver.AbsolutePathOf(note);
        var destAbs = PathGuard.ResolveNotePath(ctx.VaultRoot, $"{destFolder}/{fileName}");
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(destAbs, abs, comparison))
            throw new MindVaultException($"Note is already at {note.Path}.");

        var snapshot = ctx.Snapshots.Snapshot(abs);
        Directory.CreateDirectory(Path.GetDirectoryName(destAbs)!);
        if (File.Exists(destAbs))
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            destAbs = Path.Combine(Path.GetDirectoryName(destAbs)!, $"{stem} - {DateTime.Now:yyyyMMdd-HHmmss}.md");
        }
        File.Move(abs, destAbs);
        ctx.Scanner.RemoveFromIndex(note.Path);
        var summary = ctx.Scanner.IndexFile(destAbs);
        return new MoveNoteResult(note.Path, summary.Path, snapshot);
    }

    // ---------- promote ----------

    /// <summary>
    /// Promotes a thought (or an untyped note) into durable memory: validates the target,
    /// resolves the project (never guessed), runs the duplicate gate, rewrites frontmatter,
    /// retitles the H1 when safe, and moves the file to its placement folder. The body is
    /// preserved verbatim and the file name never changes, so existing links keep working.
    /// </summary>
    public PromoteResult PromoteNote(string noteRef, string targetType, string? project = null,
        bool allowDuplicate = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            targetType = (targetType ?? "").Trim().ToLowerInvariant();
            if (!PromotionTargets.Contains(targetType))
                throw new MindVaultException(
                    $"Promotion target must be one of: {string.Join(", ", PromotionTargets)}.");

            var note = ctx.Resolver.Resolve(noteRef);
            if (note.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
                throw new MindVaultException("Templates cannot be promoted.");
            if (note.Path.StartsWith(ctx.Config.DefaultArchiveFolder + "/", StringComparison.OrdinalIgnoreCase))
                throw new MindVaultException($"Archived notes cannot be promoted — restore {note.Path} first.");

            var isThought = string.Equals(note.Type, "thought", StringComparison.OrdinalIgnoreCase);
            if (!isThought && NoteTypes.IsManaged(note.Type))
                throw new MindVaultException(
                    $"Only thoughts and untyped notes can be promoted; {note.Path} is already a durable " +
                    $"'{note.Type}' note. Change its status with update-frontmatter, or archive it, instead.");

            // Project: explicit parameter wins, then the note's own frontmatter. Never guessed.
            var projName = !string.IsNullOrWhiteSpace(project) ? project.Trim() : note.Project;
            NoteSummary? proj = null;
            if (!string.IsNullOrWhiteSpace(projName))
            {
                proj = ctx.ProjectDetect.ResolveOrThrow(projName!).Project;
            }
            else if (ProjectRequiredTargets.Contains(targetType))
            {
                var known = ctx.Db.Query(type: "project", limit: 500)
                    .Where(p => !p.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
                    .Select(p => p.Title).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).Take(10).ToList();
                throw new MindVaultException(
                    $"Promoting to {targetType} requires a project — pass one explicitly." +
                    (known.Count > 0
                        ? $" Known projects: {string.Join(", ", known)}."
                        : " The vault has no project notes yet."));
            }

            // The duplicate gate compares real titles, so strip the capture prefix first —
            // and never counts the note being promoted as its own duplicate.
            var cleanTitle = StripPrefix(note.Title, "Thought:");
            var warnings = DraftWarnings(targetType, proj?.Title, cleanTitle, allowDuplicate, note.Path);

            var abs = ctx.Resolver.AbsolutePathOf(note);
            var snapshot = ctx.Snapshots.Snapshot(abs);
            var (text, lineEnding) = ReadNormalized(abs);
            var fm = ParseFrontmatterForEdit(text, note.Path, out var body);

            var status = DefaultPromotedStatus(targetType);
            fm.SetScalar("type", targetType);
            fm.SetScalar("status", status);
            if (fm.GetScalar("created") is null or "") fm.SetScalar("created", Today);
            fm.SetScalar("updated", Today);
            var tags = fm.GetList("tags");
            tags.RemoveAll(t => string.Equals(t.Trim(), "thought", StringComparison.OrdinalIgnoreCase));
            if (!tags.Contains(targetType, StringComparer.OrdinalIgnoreCase)) tags.Add(targetType);
            fm.SetList("tags", tags);
            if (proj is not null)
            {
                fm.SetScalar("project", proj.Title);
                AddWikiLink(fm, "links", proj.Stem);
            }

            // Retitle "# Thought: X" → "# Decision: X" only when nothing links to the old
            // title — retitling under backlinks would silently break them.
            var suggestions = new List<string>();
            var newBody = body;
            if (TryRetitleThoughtHeading(body, targetType, out var retitled))
            {
                var hasBacklinks = ctx.Db.GetBacklinkPaths(
                    SlugHelper.NormalizeWiki(note.Title), SlugHelper.NormalizeWiki(note.Stem), note.Id).Count > 0;
                if (hasBacklinks)
                    suggestions.Add($"the H1 still says 'Thought:' — it was kept because other notes link to " +
                                    $"'{note.Title}'; retitle it manually if wanted");
                else
                    newBody = retitled;
            }

            WriteAndVerify(abs, FrontmatterCodec.BuildDocument(fm, newBody), lineEnding, snapshot);
            ctx.Scanner.IndexFile(abs);

            // Move into placement if needed. The file name is preserved so stem links survive.
            var toPath = note.Path;
            if (!PlacementPolicy.IsAcceptablePath(note.Path, targetType, ctx.Config.DefaultArchiveFolder))
                toPath = MoveNoteCore(note.Path, PlacementPolicy.PreferredFolder(targetType)!, null).ToPath;

            foreach (var section in ExpectedSections(targetType))
            {
                if (!NoteParser.ExtractHeadings(newBody).Any(h =>
                        string.Equals(h.Text, section, StringComparison.OrdinalIgnoreCase)))
                {
                    suggestions.Add($"add a '## {section}' section — {targetType} notes should record it");
                }
            }

            return new PromoteResult(note.Path, toPath, targetType, status, snapshot, warnings, suggestions);
        }
    }

    private static string DefaultPromotedStatus(string targetType) => targetType switch
    {
        "decision" => "accepted",
        "task" => "open",
        "risk" => "open",
        _ => "active", // memory, mistake
    };

    private static IReadOnlyList<string> ExpectedSections(string targetType) => targetType switch
    {
        "decision" => ["Context", "Decision", "Reasoning", "Consequences"],
        "task" => ["Description", "Acceptance Criteria"],
        "risk" => ["Risk", "Impact", "Mitigation"],
        "mistake" => ["What Happened", "Root Cause", "How To Avoid It"],
        "memory" => ["Fact", "Why It Matters", "Source"],
        _ => [],
    };

    private static string StripPrefix(string title, string prefix) =>
        title.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? title[prefix.Length..].Trim()
            : title;

    private static bool TryRetitleThoughtHeading(string body, string targetType, out string newBody)
    {
        newBody = body;
        var h1 = NoteParser.ExtractHeadings(body).FirstOrDefault(h => h.Level == 1);
        if (h1 is null || !h1.Text.StartsWith("Thought:", StringComparison.OrdinalIgnoreCase))
            return false;
        var prefix = targetType switch
        {
            "decision" => "Decision",
            "task" => "Task",
            "risk" => "Risk",
            "mistake" => "Mistake",
            _ => "Memory",
        };
        var lines = body.Split('\n');
        lines[h1.Line] = $"# {prefix}: {h1.Text["Thought:".Length..].Trim()}";
        newBody = string.Join("\n", lines);
        return true;
    }

    // ---------- generated bodies ----------

    /// <summary>
    /// Replaces a note's whole body (frontmatter preserved, `updated` bumped). Used by map
    /// rebuilds after MapService has merged the generated block with the human-written text.
    /// Core-only primitive — deliberately not exposed through the CLI or MCP surface.
    /// </summary>
    public WriteResult ReplaceBody(string noteRef, string newBody)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var note = ctx.Resolver.Resolve(noteRef);
            var abs = ctx.Resolver.AbsolutePathOf(note);
            var snapshot = ctx.Snapshots.Snapshot(abs);
            var (text, lineEnding) = ReadNormalized(abs);
            var fm = ParseFrontmatterForEdit(text, note.Path, out _);
            fm.SetScalar("updated", Today);
            if (!newBody.EndsWith('\n')) newBody += "\n";
            WriteAndVerify(abs, FrontmatterCodec.BuildDocument(fm, newBody), lineEnding, snapshot);
            ctx.Scanner.IndexFile(abs);
            return new WriteResult(note.Path, snapshot, $"Rebuilt {note.Path}");
        }
    }
}
