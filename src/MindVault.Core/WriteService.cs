using System.Text.RegularExpressions;

namespace MindVault.Core;

public sealed record WriteResult(string Path, string? SnapshotPath, string Message, bool Changed = true);

public sealed record ArchiveResult(string FromPath, string ToPath, string SnapshotPath, IReadOnlyList<string> Warnings);

public sealed record RestoreResult(string Path, string RestoredFrom, string PreRestoreSnapshot);

public sealed record CreateNoteResult(NoteSummary Note, IReadOnlyList<string> Warnings);

public sealed record SupersedeResult(
    string OldPath, string NewPath, string OldSnapshot, string NewSnapshot, IReadOnlyList<string> Warnings);

/// <summary>
/// All vault mutations. Every operation resolves the target inside the vault (path traversal is
/// rejected), snapshots existing files before touching them, validates YAML after writing and
/// reindexes the changed note. Mutations share the single <see cref="VaultContext.Sync"/> lock
/// with scans so concurrent MCP tool calls (HTTP transport) cannot interleave read-modify-write
/// cycles with each other or with a reader-triggered scan within this process.
/// </summary>
public sealed partial class WriteService(VaultContext ctx)
{
    private static string Today => DateTime.Now.ToString("yyyy-MM-dd");

    // ---------- create ----------

    public CreateNoteResult CreateProject(string name, bool allowDuplicate = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var clean = SlugHelper.SanitizeFileName(name);
            var warnings = DraftWarnings("project", null, clean, allowDuplicate);
            var note = CreateNote($"01_Projects/{clean}.md", NoteTemplates.Project(clean, Today),
                $"A project named '{clean}' already exists");
            return new CreateNoteResult(note, warnings);
        }
    }

    public CreateNoteResult CreateDecision(string project, string title, bool allowDuplicate = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var proj = FindProject(project);
            var clean = SlugHelper.SanitizeFileName(title);
            var warnings = DraftWarnings("decision", proj.Title, clean, allowDuplicate);
            var note = CreateNote($"04_Decisions/Decision - {clean}.md",
                NoteTemplates.Decision(clean, proj.Title, proj.Stem, Today),
                $"A decision named '{clean}' already exists");
            return new CreateNoteResult(note, warnings);
        }
    }

    public CreateNoteResult CreateTask(string project, string title, bool allowDuplicate = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var proj = FindProject(project);
            var clean = SlugHelper.SanitizeFileName(title);
            var warnings = DraftWarnings("task", proj.Title, clean, allowDuplicate);
            var note = CreateNote($"01_Projects/Task - {clean}.md",
                NoteTemplates.Task(clean, proj.Title, proj.Stem, Today),
                $"A task named '{clean}' already exists");
            return new CreateNoteResult(note, warnings);
        }
    }

    /// <summary>
    /// The duplicate gate. High-confidence duplicates (same name, near-identical title of the
    /// same type+project, or a name that already resolves to a project via alias) REFUSE the
    /// create unless <paramref name="allowDuplicate"/> — an agent spamming near-identical
    /// memory is the main way a vault rots. Lower-confidence similarity stays advisory.
    /// </summary>
    private List<string> DraftWarnings(string type, string? project, string title, bool allowDuplicate)
    {
        var check = ctx.Drafts.CheckDraft(type, project, title);
        if (!allowDuplicate && check.LikelyDuplicatePaths.Count > 0)
            throw new DuplicateSuspectedException(type, title, check.LikelyDuplicatePaths);
        return check.Warnings.Concat(check.Suggestions).ToList();
    }

    /// <summary>Creates a note file from prepared content (vault-guarded, atomic, indexed).</summary>
    public NoteSummary CreateNoteFile(string relativePath, string content)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            return CreateNote(relativePath, content, "A note already exists at this path");
        }
    }

    private NoteSummary CreateNote(string relativePath, string content, string existsMessage)
    {
        var abs = PathGuard.ResolveNotePath(ctx.VaultRoot, relativePath);
        if (File.Exists(abs))
            throw new MindVaultException($"{existsMessage}: {PathGuard.ToRelative(ctx.VaultRoot, abs)}");
        Directory.CreateDirectory(Path.GetDirectoryName(abs)!);
        WriteAtomic(abs, content);
        var summary = ctx.Scanner.IndexFile(abs);
        if (summary.ParseError is not null)
            throw new MindVaultException($"Created note has invalid frontmatter ({summary.ParseError}) — this is a bug.");
        return summary;
    }

    public NoteSummary FindProject(string project)
    {
        if (string.IsNullOrWhiteSpace(project))
            throw new MindVaultException("Project name must not be empty.");
        // Alias/repo-name tolerant: "mind-vault" finds project "MindVault" instead of
        // failing (or worse, prompting the agent to create a duplicate project).
        return ctx.ProjectDetect.ResolveOrThrow(project.Trim()).Project;
    }

    // ---------- append ----------

    public WriteResult AppendToSection(string noteRef, string section, string content,
        bool createSection = false, bool dryRun = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            return AppendToSectionCore(noteRef, section, content, createSection, dryRun);
        }
    }

    private WriteResult AppendToSectionCore(string noteRef, string section, string content,
        bool createSection, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(section))
            throw new MindVaultException("Section heading must not be empty.");
        var normalizedContent = (content ?? "").Replace("\r\n", "\n").Trim('\n');
        if (normalizedContent.Trim().Length == 0)
            throw new MindVaultException("Content must not be empty.");

        var note = ctx.Resolver.Resolve(noteRef);
        var abs = ctx.Resolver.AbsolutePathOf(note);
        var (text, lineEnding) = ReadNormalized(abs);

        var hasFm = FrontmatterCodec.TryExtract(text, out var yamlText, out var body);
        var headings = NoteParser.ExtractHeadings(body);
        var wanted = section.Trim();
        var target = headings.FirstOrDefault(h => string.Equals(h.Text, wanted, StringComparison.OrdinalIgnoreCase));

        if (target is null && !createSection)
        {
            var available = headings.Count == 0 ? "(none)" : string.Join(", ", headings.Select(h => $"'{h.Text}'"));
            throw new MindVaultException(
                $"Heading '{wanted}' not found in {note.Path}. Available headings: {available}. " +
                "Pass --create-section to add it.");
        }
        if (dryRun)
        {
            return new WriteResult(note.Path, null,
                $"[dry-run] Would append {normalizedContent.Length} chars under " +
                (target is null ? $"NEW section '{wanted}'" : $"existing section '{wanted}'") +
                $" in {note.Path} (snapshot first). Nothing was changed.", Changed: false);
        }
        var snapshot = ctx.Snapshots.Snapshot(abs);

        string newBody;
        if (target is null)
        {
            newBody = body.TrimEnd('\n') + $"\n\n## {wanted}\n\n{normalizedContent}\n";
            if (newBody.StartsWith('\n')) newBody = newBody.TrimStart('\n');
        }
        else
        {
            var lines = body.Split('\n').ToList();
            var next = headings
                .Where(h => h.Line > target.Line && h.Level <= target.Level)
                .OrderBy(h => h.Line)
                .FirstOrDefault();
            var sectionEnd = next?.Line ?? lines.Count;
            var insertAt = sectionEnd;
            while (insertAt > target.Line + 1 && string.IsNullOrWhiteSpace(lines[insertAt - 1]))
                insertAt--;

            var insertion = new List<string> { "" };
            insertion.AddRange(normalizedContent.Split('\n'));
            if (sectionEnd < lines.Count) insertion.Add("");

            lines.RemoveRange(insertAt, sectionEnd - insertAt);
            lines.InsertRange(insertAt, insertion);
            newBody = string.Join("\n", lines);
        }
        if (!newBody.EndsWith('\n')) newBody += "\n";

        var newText = hasFm ? "---\n" + BumpUpdated(yamlText) + "---\n" + newBody : newBody;
        WriteBack(abs, newText, lineEnding);
        ctx.Scanner.IndexFile(abs);
        return new WriteResult(note.Path, snapshot, $"Appended to '{wanted}' in {note.Path}");
    }

    // ---------- frontmatter ----------

    public WriteResult UpdateFrontmatter(string noteRef, string key, string value, bool dryRun = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            return UpdateFrontmatterCore(noteRef, key, value, dryRun);
        }
    }

    private WriteResult UpdateFrontmatterCore(string noteRef, string key, string value, bool dryRun)
    {
        key = (key ?? "").Trim();
        if (!FrontmatterKeyPattern().IsMatch(key))
            throw new MindVaultException($"Invalid frontmatter key: '{key}'. Use letters, digits, '-' or '_'.",
                ErrorCodes.InvalidFrontmatter);
        value = (value ?? "").Trim();
        if (value.Contains('\n'))
            throw new MindVaultException("Multi-line frontmatter values are not supported in v0.1.",
                ErrorCodes.InvalidFrontmatter);
        if (value.StartsWith('{') || (value.StartsWith('[') && !value.StartsWith("[[")))
            throw new MindVaultException("Nested YAML values (objects or flow lists) are rejected. " +
                                         "Frontmatter must stay flat; for tags/links pass a comma-separated list.",
                ErrorCodes.InvalidFrontmatter);

        var note = ctx.Resolver.Resolve(noteRef);
        var abs = ctx.Resolver.AbsolutePathOf(note);
        var (text, lineEnding) = ReadNormalized(abs);

        var fm = ParseFrontmatterForEdit(text, note.Path, out var body);
        if (dryRun)
        {
            var current = fm.GetScalar(key) ?? (fm.GetList(key) is { Count: > 0 } list
                ? string.Join(", ", list) : null);
            return new WriteResult(note.Path, null,
                $"[dry-run] Would set {key}: '{current ?? "(unset)"}' -> '{value}' in {note.Path} " +
                "(snapshot first, updated bumped). Nothing was changed.", Changed: false);
        }
        var snapshot = ctx.Snapshots.Snapshot(abs);
        if (key.Equals("tags", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("links", StringComparison.OrdinalIgnoreCase))
        {
            fm.SetList(key.ToLowerInvariant(),
                value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0));
        }
        else
        {
            fm.SetScalar(key, value);
        }
        if (!key.Equals("updated", StringComparison.OrdinalIgnoreCase))
            fm.SetScalar("updated", Today);

        WriteAndVerify(abs, FrontmatterCodec.BuildDocument(fm, body), lineEnding, snapshot);
        ctx.Scanner.IndexFile(abs);
        return new WriteResult(note.Path, snapshot, $"Set {key} in {note.Path}");
    }

    // ---------- link ----------

    public WriteResult LinkNotes(string fromRef, string toRef)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            return LinkNotesCore(fromRef, toRef);
        }
    }

    private WriteResult LinkNotesCore(string fromRef, string toRef)
    {
        var from = ctx.Resolver.Resolve(fromRef);
        var to = ctx.Resolver.Resolve(toRef);
        if (from.Id == to.Id)
            throw new MindVaultException("A note cannot be linked to itself.");

        var abs = ctx.Resolver.AbsolutePathOf(from);
        var snapshot = ctx.Snapshots.Snapshot(abs);
        var (text, lineEnding) = ReadNormalized(abs);

        var fm = ParseFrontmatterForEdit(text, from.Path, out var body);
        var links = fm.GetList("links");
        var linkText = $"[[{to.Stem}]]";
        var toNorm = SlugHelper.NormalizeWiki(to.Stem);
        var alreadyLinked = links.Any(l =>
        {
            var inner = l.Trim();
            if (inner.StartsWith("[[") && inner.EndsWith("]]")) inner = inner[2..^2];
            return SlugHelper.NormalizeWiki(inner.Split('|')[0].Split('#')[0]) == toNorm;
        });
        if (alreadyLinked)
            return new WriteResult(from.Path, snapshot, $"{from.Path} already links to {to.Path}", Changed: false);

        links.Add(linkText);
        fm.SetList("links", links);
        fm.SetScalar("updated", Today);

        WriteAndVerify(abs, FrontmatterCodec.BuildDocument(fm, body), lineEnding, snapshot);
        ctx.Scanner.IndexFile(abs);
        return new WriteResult(from.Path, snapshot, $"Linked {from.Path} -> {to.Path}");
    }

    // ---------- archive ----------

    public ArchiveResult Archive(string noteRef, bool dryRun = false)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            return ArchiveCore(noteRef, dryRun);
        }
    }

    private ArchiveResult ArchiveCore(string noteRef, bool dryRun)
    {
        var note = ctx.Resolver.Resolve(noteRef);
        var archiveFolder = ctx.Config.DefaultArchiveFolder;
        if (note.Path.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase))
            throw new MindVaultException($"Note is already archived: {note.Path}");

        var abs = ctx.Resolver.AbsolutePathOf(note);
        if (dryRun)
        {
            var wouldBe = $"{archiveFolder}/{Path.GetFileName(abs)}";
            return new ArchiveResult(note.Path, wouldBe, "",
                [$"[dry-run] Would snapshot, set status: archived and move {note.Path} -> {wouldBe}. " +
                 "Nothing was changed."]);
        }
        var snapshot = ctx.Snapshots.Snapshot(abs);
        var warnings = new List<string>();

        // Set status: archived + updated. If YAML is broken we still archive, untouched.
        var (text, lineEnding) = ReadNormalized(abs);
        var hasFm = FrontmatterCodec.TryExtract(text, out var yamlText, out var body);
        var parsed = hasFm ? FrontmatterCodec.Parse(yamlText) : new FrontmatterParseResult(new Frontmatter(), null);
        if (parsed.Frontmatter is { } fm && parsed.Error is null)
        {
            fm.SetScalar("status", "archived");
            fm.SetScalar("updated", Today);
            WriteAndVerify(abs, FrontmatterCodec.BuildDocument(fm, hasFm ? body : text), lineEnding, snapshot);
        }
        else
        {
            warnings.Add($"Frontmatter not updated ({parsed.Error}); file moved as-is.");
        }

        var destDir = Path.Combine(ctx.VaultRoot, archiveFolder);
        Directory.CreateDirectory(destDir);
        var fileName = Path.GetFileName(abs);
        var destAbs = Path.Combine(destDir, fileName);
        if (File.Exists(destAbs))
        {
            var stem = Path.GetFileNameWithoutExtension(fileName);
            destAbs = Path.Combine(destDir, $"{stem} - {DateTime.Now:yyyyMMdd-HHmmss}.md");
        }
        PathGuard.ResolveNotePath(ctx.VaultRoot, destAbs);
        File.Move(abs, destAbs);

        ctx.Scanner.RemoveFromIndex(note.Path);
        var newSummary = ctx.Scanner.IndexFile(destAbs);
        return new ArchiveResult(note.Path, newSummary.Path, snapshot, warnings);
    }

    // ---------- decisions ----------

    /// <summary>
    /// Marks one decision as replaced by another: old gets status superseded + a
    /// superseded_by link, new gets a supersedes link. Both notes are snapshotted before
    /// either is touched; if the second write fails the first is rolled back from its snapshot.
    /// </summary>
    public SupersedeResult SupersedeDecision(string oldRef, string newRef)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var oldNote = ctx.Resolver.Resolve(oldRef);
            var newNote = ctx.Resolver.Resolve(newRef);
            if (oldNote.Id == newNote.Id)
                throw new MindVaultException("A decision cannot supersede itself.");
            foreach (var (note, label) in new[] { (oldNote, "old"), (newNote, "new") })
            {
                if (!string.Equals(note.Type, "decision", StringComparison.OrdinalIgnoreCase))
                    throw new MindVaultException($"The {label} note is not a decision: {note.Path} (type: {note.Type ?? "none"}).");
            }

            var oldAbs = ctx.Resolver.AbsolutePathOf(oldNote);
            var newAbs = ctx.Resolver.AbsolutePathOf(newNote);
            var oldSnapshot = ctx.Snapshots.Snapshot(oldAbs);
            var newSnapshot = ctx.Snapshots.Snapshot(newAbs);
            var warnings = new List<string>();

            var (oldText, oldEnding) = ReadNormalized(oldAbs);
            var oldFm = ParseFrontmatterForEdit(oldText, oldNote.Path, out var oldBody);
            oldFm.SetScalar("status", "superseded");
            AddWikiLink(oldFm, "superseded_by", newNote.Stem);
            oldFm.SetScalar("updated", Today);
            WriteAndVerify(oldAbs, FrontmatterCodec.BuildDocument(oldFm, oldBody), oldEnding, oldSnapshot);

            try
            {
                var (newText, newEnding) = ReadNormalized(newAbs);
                var newFm = ParseFrontmatterForEdit(newText, newNote.Path, out var newBody);
                AddWikiLink(newFm, "supersedes", oldNote.Stem);
                newFm.SetScalar("updated", Today);
                WriteAndVerify(newAbs, FrontmatterCodec.BuildDocument(newFm, newBody), newEnding, newSnapshot);
            }
            catch (MindVaultException ex)
            {
                File.Copy(oldSnapshot, oldAbs, overwrite: true);
                ctx.Scanner.IndexFile(oldAbs);
                throw new MindVaultException(
                    $"Could not update the new decision ({ex.Message}); the old decision was rolled back — nothing changed.");
            }

            ctx.Scanner.IndexFile(oldAbs);
            ctx.Scanner.IndexFile(newAbs);
            return new SupersedeResult(oldNote.Path, newNote.Path, oldSnapshot, newSnapshot, warnings);
        }
    }

    private static void AddWikiLink(Frontmatter fm, string key, string targetStem)
    {
        var items = fm.GetList(key);
        var norm = SlugHelper.NormalizeWiki(targetStem);
        if (!items.Any(i =>
            {
                var inner = i.Trim();
                if (inner.StartsWith("[[") && inner.EndsWith("]]")) inner = inner[2..^2];
                return SlugHelper.NormalizeWiki(inner.Split('|')[0].Split('#')[0]) == norm;
            }))
        {
            items.Add($"[[{targetStem}]]");
        }
        fm.SetList(key, items);
    }

    // ---------- restore ----------

    /// <summary>
    /// Restores a note from a snapshot (the newest matching one by default). The current
    /// content is snapshotted first, so a restore is itself reversible.
    /// </summary>
    public RestoreResult RestoreFromSnapshot(string noteRef, string? snapshotPath = null)
    {
        lock (ctx.Sync)
        using (ctx.WriteLock.Acquire())
        {
            var note = ctx.Resolver.Resolve(noteRef);
            var abs = ctx.Resolver.AbsolutePathOf(note);

            string source;
            if (snapshotPath is not null)
            {
                source = Path.GetFullPath(snapshotPath);
                var snapshotRoot = Path.GetFullPath(ctx.SnapshotDir);
                var comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (!source.StartsWith(snapshotRoot + Path.DirectorySeparatorChar, comparison))
                    throw new MindVaultException($"Snapshots live under {snapshotRoot}; '{snapshotPath}' is outside it.");
                if (!File.Exists(source))
                    throw new MindVaultException($"Snapshot not found: {source}");
            }
            else
            {
                source = ctx.Snapshots.ListSnapshots(note.Stem).FirstOrDefault()
                    ?? throw new MindVaultException(
                        $"No snapshots found for {note.Path}. Snapshots are created when a note is mutated.");
            }

            var preRestore = ctx.Snapshots.Snapshot(abs);
            File.Copy(source, abs, overwrite: true);
            ctx.Scanner.IndexFile(abs);
            return new RestoreResult(note.Path, source, preRestore);
        }
    }

    // ---------- helpers ----------

    private Frontmatter ParseFrontmatterForEdit(string normalizedText, string notePath, out string body)
    {
        if (!FrontmatterCodec.TryExtract(normalizedText, out var yamlText, out body))
        {
            body = normalizedText;
            return new Frontmatter();
        }
        var parsed = FrontmatterCodec.Parse(yamlText);
        if (parsed.Error is not null || parsed.Frontmatter is null)
            throw new MindVaultException(
                $"Cannot edit frontmatter of {notePath}: existing YAML is not valid flat YAML " +
                $"({parsed.Error ?? "unparseable"}). Fix the note in Obsidian first.",
                ErrorCodes.InvalidFrontmatter);
        return parsed.Frontmatter;
    }

    /// <summary>Replaces (or adds) the `updated:` line in raw YAML text without reformatting anything else.</summary>
    private static string BumpUpdated(string yamlText)
    {
        var lines = yamlText.TrimEnd('\n').Split('\n').ToList();
        var replaced = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (UpdatedLinePattern().IsMatch(lines[i]))
            {
                lines[i] = $"updated: {Today}";
                replaced = true;
                break;
            }
        }
        if (!replaced) lines.Add($"updated: {Today}");
        return string.Join("\n", lines) + "\n";
    }

    private static (string Text, string LineEnding) ReadNormalized(string absolutePath)
    {
        var raw = File.ReadAllText(absolutePath);
        var lineEnding = raw.Contains("\r\n") ? "\r\n" : "\n";
        return (raw.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n"), lineEnding);
    }

    private static void WriteBack(string absolutePath, string normalizedText, string lineEnding)
    {
        var text = lineEnding == "\r\n" ? normalizedText.Replace("\n", "\r\n") : normalizedText;
        WriteAtomic(absolutePath, text);
    }

    /// <summary>
    /// Write to a sibling temp file, then move it over the target — atomic on the same volume,
    /// so a crash mid-write can never leave a torn note. The temp suffix is not .md, so the
    /// scanner can never index a partial file.
    /// </summary>
    private static void WriteAtomic(string absolutePath, string text)
    {
        var tmp = absolutePath + ".mindvault-tmp";
        File.WriteAllText(tmp, text);
        File.Move(tmp, absolutePath, overwrite: true);
    }

    /// <summary>Writes, re-parses to confirm the YAML is still valid, and restores the snapshot if not.</summary>
    private void WriteAndVerify(string absolutePath, string normalizedText, string lineEnding, string snapshotPath)
    {
        WriteBack(absolutePath, normalizedText, lineEnding);
        var check = NoteParser.ParseFile(ctx.VaultRoot, absolutePath);
        if (check.ParseError is not null)
        {
            File.Copy(snapshotPath, absolutePath, overwrite: true);
            throw new MindVaultException(
                $"Write produced invalid YAML ({check.ParseError}); the note was restored from its snapshot.",
                ErrorCodes.InvalidFrontmatter);
        }
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_-]*$")]
    private static partial Regex FrontmatterKeyPattern();

    [GeneratedRegex(@"^updated\s*:")]
    private static partial Regex UpdatedLinePattern();
}
