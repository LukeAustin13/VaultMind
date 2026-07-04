namespace MindVault.Core;

public sealed record MoveProposal(
    string Note, string CurrentPath, string ProposedPath, string Reason, string Confidence);

public sealed record OrganizeReviewItem(string Path, string Reason);

public sealed record OrganizeAppliedMove(string FromPath, string ToPath, string SnapshotPath);

public sealed record OrganizeReport(
    bool DryRun,
    IReadOnlyList<MoveProposal> Proposals,
    IReadOnlyList<OrganizeReviewItem> NeedsReview,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<OrganizeAppliedMove> Applied);

/// <summary>
/// The organisation engine's planner: compares every managed note's location against
/// <see cref="PlacementPolicy"/> and produces move proposals with reasons. Dry-run by
/// default — Apply executes only the emitted (safe, high-confidence) proposals, snapshot
/// first, one atomic move at a time. Archived notes and templates are never touched;
/// anything uncertain (broken YAML, unresolvable project, untyped notes squatting in
/// managed folders, destination collisions) goes to needs-review instead of being moved.
/// </summary>
public sealed class OrganizeService(VaultContext ctx)
{
    public const int MaxProposals = 200;

    private static readonly string[] TypedFolders =
        ["01_Projects", "04_Decisions", "05_Prompts", "06_Agent_Memory", "07_Reviews", "09_Maps"];

    public OrganizeReport Plan(string? project = null) => Build(project, apply: false);

    public OrganizeReport Apply(string? project = null) => Build(project, apply: true);

    private OrganizeReport Build(string? project, bool apply)
    {
        ctx.Scanner.EnsureFresh();
        var archiveFolder = ctx.Config.DefaultArchiveFolder;
        var proposals = new List<MoveProposal>();
        var review = new List<OrganizeReviewItem>();
        var warnings = new List<string>();
        var claimedDestinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string[]? projectNames = null;
        long projectId = -1;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project.Trim());
            projectNames = ctx.ProjectDetect.QueryNamesFor(proj);
            projectId = proj.Id;
        }

        var notes = ctx.Db.GetAllNotes()
            .Where(n => !IsTemplate(n))
            .Where(n => !n.Path.StartsWith(archiveFolder + "/", StringComparison.OrdinalIgnoreCase))
            .Where(n => !string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase))
            .Where(n => projectNames is null
                        || n.Id == projectId
                        || (n.Project is { Length: > 0 } p &&
                            projectNames.Contains(p, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(n => n.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var note in notes)
        {
            if (note.ParseError is not null)
            {
                review.Add(new(note.Path,
                    $"invalid frontmatter ({note.ParseError}) — fix the YAML in Obsidian before organising"));
                continue;
            }

            var type = note.Type?.Trim().ToLowerInvariant();
            var placeable = NoteTypes.IsManaged(type) || type == "map";
            if (!placeable)
            {
                // Untyped/foreign notes are fine in the inbox and human folders; they only
                // need review when squatting in a typed, managed folder.
                if (TypedFolders.Any(f => note.Path.StartsWith(f + "/", StringComparison.OrdinalIgnoreCase)))
                    review.Add(new(note.Path,
                        "no managed type: frontmatter — give it a type (or move it to 00_Inbox) so it can be filed"));
                continue;
            }

            if (PlacementPolicy.IsAcceptablePath(note.Path, type, archiveFolder))
                continue;

            // The project must resolve (or be absent) before a move counts as safe.
            var projectPart = "";
            if (note.Project is { Length: > 0 } projName)
            {
                var detection = ctx.ProjectDetect.Detect(projName);
                if (detection.Project is null)
                {
                    review.Add(new(note.Path, detection.Ambiguous
                        ? $"project '{projName}' matches more than one project note — fix project: before filing"
                        : $"project '{projName}' does not resolve to a project note — fix project: before filing"));
                    continue;
                }
                projectPart = $", project={detection.Project.Title}";
            }

            var destFolder = PlacementPolicy.PreferredFolder(type)!;
            var (fileName, renamed) = ProposedFileName(note, type!);
            var proposedPath = $"{destFolder}/{fileName}";

            // A different note already claiming the destination is a duplicate signal, not a move.
            var occupant = ctx.Db.FindByPath(proposedPath);
            if (occupant is not null && occupant.Id != note.Id)
            {
                review.Add(new(note.Path,
                    $"a note already exists at {proposedPath} — possible duplicate; merge or rename before filing"));
                continue;
            }
            if (!claimedDestinations.Add(proposedPath))
            {
                review.Add(new(note.Path,
                    $"another note in this run is already proposed at {proposedPath} — resolve the duplicate first"));
                continue;
            }

            var reason = $"type={type}" +
                         (note.Status is { Length: > 0 } ? $", status={note.Status}" : "") +
                         projectPart +
                         (renamed ? "; canonical name applied (no backlinks to break)" : "");
            proposals.Add(new(note.Title, note.Path, proposedPath, reason, "high"));
            if (proposals.Count >= MaxProposals)
            {
                warnings.Add($"Proposal list capped at {MaxProposals} — apply these and re-run for the rest.");
                break;
            }
        }

        var applied = new List<OrganizeAppliedMove>();
        if (apply)
        {
            foreach (var p in proposals)
            {
                try
                {
                    var slash = p.ProposedPath.LastIndexOf('/');
                    var destFolder = p.ProposedPath[..slash];
                    var fileName = p.ProposedPath[(slash + 1)..];
                    var keepName = string.Equals(fileName, Path.GetFileName(p.CurrentPath),
                        StringComparison.OrdinalIgnoreCase);
                    var move = ctx.Writer.MoveNote(p.CurrentPath, destFolder, keepName ? null : fileName);
                    applied.Add(new(move.FromPath, move.ToPath, move.SnapshotPath));
                }
                catch (MindVaultException ex)
                {
                    warnings.Add($"Could not move {p.CurrentPath}: {ex.Message}");
                }
            }
        }

        return new OrganizeReport(!apply, proposals, review, warnings, applied);
    }

    /// <summary>
    /// Canonical "Decision - X" / "Task - X" naming is offered only when nothing links to
    /// the note — renames change the stem, and stem-targeted links would silently break.
    /// </summary>
    private (string FileName, bool Renamed) ProposedFileName(NoteSummary note, string type)
    {
        var current = Path.GetFileName(note.Path);
        var prefix = type switch { "decision" => "Decision - ", "task" => "Task - ", _ => null };
        if (prefix is null || note.Stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return (current, false);

        var backlinks = ctx.Db.GetBacklinkPaths(
            SlugHelper.NormalizeWiki(note.Title), SlugHelper.NormalizeWiki(note.Stem), note.Id);
        if (backlinks.Count > 0) return (current, false);

        var title = note.Title;
        var titlePrefix = type == "decision" ? "Decision:" : "Task:";
        if (title.StartsWith(titlePrefix, StringComparison.OrdinalIgnoreCase))
            title = title[titlePrefix.Length..].Trim();
        return ($"{prefix}{SlugHelper.SanitizeFileName(title)}.md", true);
    }

    private static bool IsTemplate(NoteSummary n) =>
        n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase);
}
