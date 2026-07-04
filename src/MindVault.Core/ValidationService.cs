using System.Diagnostics;

namespace MindVault.Core;

public enum IssueSeverity { Critical, Warning, Info }

public sealed record ValidationIssue(IssueSeverity Severity, string Code, string Message, string? Path = null);

public sealed record ValidationReport(IReadOnlyList<ValidationIssue> Issues, long ElapsedMs = 0)
{
    public int CriticalCount => Issues.Count(i => i.Severity == IssueSeverity.Critical);
    public int WarningCount => Issues.Count(i => i.Severity == IssueSeverity.Warning);
    public int InfoCount => Issues.Count(i => i.Severity == IssueSeverity.Info);

    /// <summary>Alias kept for callers written against the pre-severity-levels API.</summary>
    public int ErrorCount => CriticalCount;
}

public sealed class ValidationService(VaultContext ctx)
{
    public const int StaleTaskDays = 60;
    public const long LargeNoteBytes = 100_000;

    public ValidationReport Validate()
    {
        var stopwatch = Stopwatch.StartNew();
        var indexWasMissing = !ctx.IndexExists;
        ctx.Scanner.Scan(); // incremental refresh so the report reflects the vault on disk
        var issues = new List<ValidationIssue>();

        if (indexWasMissing)
            issues.Add(new(IssueSeverity.Info, "index-rebuilt",
                "The index did not exist and was built during this run."));

        // Environment probes: these failing means every mutation is unsafe.
        ProbeWritable(issues, ctx.VaultRoot, "vault-unwritable",
            "Vault folder is not writable — all note writes will fail");
        ProbeWritable(issues, ctx.SnapshotDir, "snapshot-unwritable",
            "Snapshot folder is not writable — mutations would run WITHOUT their safety net");

        // Sync-conflict copies are never indexed; a human must resolve them in Obsidian.
        foreach (var conflict in VaultFiles.EnumerateConflictMarkdown(ctx.VaultRoot))
            issues.Add(new(IssueSeverity.Warning, "sync-conflict-file",
                "Sync conflict copy present — resolve or delete it in Obsidian (MindVault ignores it)",
                PathGuard.ToRelative(ctx.VaultRoot, conflict)));

        foreach (var folder in VaultStructure.RequiredFolders)
        {
            if (!Directory.Exists(Path.Combine(ctx.VaultRoot, folder)))
                issues.Add(new(IssueSeverity.Critical, "missing-folder",
                    $"Required folder missing: {folder} (run 'init')"));
        }
        foreach (var template in VaultStructure.TemplateFiles)
        {
            if (!File.Exists(Path.Combine(ctx.VaultRoot, template)))
                issues.Add(new(IssueSeverity.Warning, "missing-template",
                    $"Template missing: {template} (run 'init')"));
        }

        var notes = ctx.Db.GetAllNotes();
        var contentNotes = notes.Where(n => !IsTemplateNote(n)).ToList();
        var archiveFolder = ctx.Config.DefaultArchiveFolder;
        bool IsArchived(NoteSummary n) =>
            string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase) ||
            n.Path.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase);

        foreach (var note in contentNotes.Where(n => n.ParseError is not null))
        {
            var code = note.ParseError!.StartsWith("yaml-nested") ? "nested-yaml" : "invalid-yaml";
            issues.Add(new(IssueSeverity.Critical, code, note.ParseError, note.Path));
        }

        var keyPresence = ctx.Db.GetFrontmatterKeyPresence(NoteTypes.RequiredFrontmatterKeys.ToList());
        foreach (var note in contentNotes.Where(n => NoteTypes.IsManaged(n.Type)))
        {
            foreach (var key in NoteTypes.RequiredFrontmatterKeys.Where(k => !keyPresence[k].Contains(note.Id)))
                issues.Add(new(IssueSeverity.Critical, "missing-frontmatter",
                    $"Managed note ({note.Type}) is missing required frontmatter key '{key}'", note.Path));
            if (keyPresence["status"].Contains(note.Id) && !NoteTypes.IsValidStatus(note.Status ?? ""))
                issues.Add(new(IssueSeverity.Critical, "invalid-status",
                    $"Invalid status '{note.Status}'. Allowed: {string.Join(", ", NoteTypes.Statuses)}", note.Path));

            var expected = VaultStructure.ExpectedFolder(note.Type);
            if (expected is not null &&
                !note.Path.StartsWith(expected + "/", StringComparison.OrdinalIgnoreCase) &&
                !note.Path.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new(IssueSeverity.Warning, "outside-structure",
                    $"{note.Type} note expected under {expected}/ (or {archiveFolder}/)", note.Path));
            }

            // Stale actionable notes rot silently unless surfaced.
            if (note.Type is "task" && note.Status is "open" or "active" or "blocked" &&
                note.Updated is not null &&
                DateTime.TryParseExact(note.Updated, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var updated) &&
                (DateTime.Today - updated).TotalDays > StaleTaskDays)
            {
                issues.Add(new(IssueSeverity.Info, "stale-task",
                    $"Task is '{note.Status}' but untouched for {(int)(DateTime.Today - updated).TotalDays} days", note.Path));
            }
        }

        foreach (var group in contentNotes.GroupBy(n => n.Title, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            issues.Add(new(IssueSeverity.Critical, "duplicate-title",
                $"Duplicate title '{group.Key}': {string.Join(" | ", group.Select(n => n.Path))}"));
        foreach (var group in contentNotes.GroupBy(n => n.Stem, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            if (group.Select(n => n.Title.ToLowerInvariant()).Distinct().Count() > 1)
                issues.Add(new(IssueSeverity.Warning, "ambiguous-note-ref",
                    $"Multiple notes share the file name '{group.Key}': {string.Join(" | ", group.Select(n => n.Path))}"));
        }

        var knownNames = new HashSet<string>();
        var byNorm = new Dictionary<string, NoteSummary>(StringComparer.Ordinal);
        foreach (var note in notes)
        {
            var titleNorm = SlugHelper.NormalizeWiki(note.Title);
            var stemNorm = SlugHelper.NormalizeWiki(note.Stem);
            knownNames.Add(titleNorm);
            knownNames.Add(stemNorm);
            byNorm.TryAdd(titleNorm, note);
            byNorm.TryAdd(stemNorm, note);
        }
        var noteById = notes.ToDictionary(n => n.Id);
        foreach (var link in ctx.Db.GetAllLinks())
        {
            if (!knownNames.Contains(link.TargetNorm))
            {
                issues.Add(new(IssueSeverity.Warning, "broken-link",
                    $"Wiki link target not found: [[{link.Target}]]", link.NotePath));
            }
            else if (noteById.TryGetValue(link.NoteId, out var source) && !IsArchived(source) &&
                     byNorm.TryGetValue(link.TargetNorm, out var target) && IsArchived(target) &&
                     !IsTemplateNote(source))
            {
                issues.Add(new(IssueSeverity.Info, "link-to-archived",
                    $"Active note links to archived note [[{link.Target}]] ({target.Path})", link.NotePath));
            }
        }

        // A superseded_by marker on a decision that still carries an active status is a contradiction.
        var supersededById = ctx.Db.GetFrontmatterValues("superseded_by")
            .Where(r => !r.NotePath.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase));
        foreach (var row in supersededById)
        {
            if (noteById.TryGetValue(row.NoteId, out var n) &&
                !string.Equals(n.Status, "superseded", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new(IssueSeverity.Warning, "superseded-status-mismatch",
                    $"Note has superseded_by but status is '{n.Status ?? "none"}' (expected 'superseded')", n.Path));
            }
        }

        var projectNames = new HashSet<string>();
        foreach (var project in notes.Where(n => string.Equals(n.Type, "project", StringComparison.OrdinalIgnoreCase)))
        {
            projectNames.Add(SlugHelper.NormalizeWiki(project.Title));
            projectNames.Add(SlugHelper.NormalizeWiki(project.Stem));
        }
        // Declared aliases/repoNames are valid project: values too — detection resolves
        // through them, so they must not be reported as missing project notes.
        foreach (var (_, _, value) in ctx.Db.GetProjectAliasRows())
        {
            foreach (var alias in Json.ReadStringList(value))
                projectNames.Add(SlugHelper.NormalizeWiki(alias));
        }
        foreach (var note in contentNotes.Where(n =>
                     n.Project is { Length: > 0 } p && !projectNames.Contains(SlugHelper.NormalizeWiki(p))))
        {
            issues.Add(new(IssueSeverity.Critical, "missing-project-note",
                $"References project '{note.Project}' but no project note with that name exists", note.Path));
        }

        foreach (var (path, size) in ctx.Db.GetLargeNotes(LargeNoteBytes)
                     .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(new(IssueSeverity.Info, "large-note",
                $"Note is {size / 1024} KB — consider splitting it for retrieval quality", path));
        }

        stopwatch.Stop();
        return new ValidationReport(issues
            .OrderBy(i => i.Severity)
            .ThenBy(i => i.Code, StringComparer.Ordinal)
            .ThenBy(i => i.Path, StringComparer.OrdinalIgnoreCase)
            .ToList(), stopwatch.ElapsedMilliseconds);
    }

    private static void ProbeWritable(List<ValidationIssue> issues, string directory, string code, string message)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".mindvault-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "probe");
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new(IssueSeverity.Critical, code, $"{message}: {directory} ({ex.Message})"));
        }
    }

    private static bool IsTemplateNote(NoteSummary note) =>
        note.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase);
}
