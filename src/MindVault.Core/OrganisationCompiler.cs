namespace MindVault.Core;

public sealed record CompileArtifact(string Kind, string Target, string Status, string Detail);

public sealed record CompileReport(
    bool DryRun, string? Project, int OverallScore,
    IReadOnlyList<CompileArtifact> Artifacts, IReadOnlyList<string> Warnings);

/// <summary>
/// Compiles vault structure into agent-efficient navigation artefacts in one pass: maps
/// (create/rebuild), generated summaries, the typed link-graph sidecar, and the
/// health/score reports. Dry-run by default: nothing is written without --apply, and every
/// vault-note write goes through the snapshot-first writers. Human content outside
/// generated markers is never touched.
/// </summary>
public sealed class OrganisationCompiler(VaultContext ctx)
{
    public const int ProjectCap = 25;

    public CompileReport Compile(string? project = null, bool apply = false)
    {
        ctx.Scanner.EnsureFresh();
        var artifacts = new List<CompileArtifact>();
        var warnings = new List<string>();
        var archive = ctx.Config.DefaultArchiveFolder;

        List<NoteSummary> projects;
        if (!string.IsNullOrWhiteSpace(project))
        {
            var (proj, _) = ctx.ProjectDetect.ResolveOrThrow(project!);
            projects = [proj];
        }
        else
        {
            projects = ctx.Db.GetAllNotes()
                .Where(n => string.Equals(n.Type, "project", StringComparison.OrdinalIgnoreCase))
                .Where(n => !n.Path.StartsWith("08_Templates/", StringComparison.OrdinalIgnoreCase))
                .Where(n => !n.Path.StartsWith(archive + "/", StringComparison.OrdinalIgnoreCase))
                .Where(n => !string.Equals(n.Status, "archived", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (projects.Count > ProjectCap)
            {
                warnings.Add($"compiling the first {ProjectCap} of {projects.Count} projects — " +
                             "run per-project for the rest.");
                projects = projects.Take(ProjectCap).ToList();
            }
        }

        foreach (var proj in projects)
        {
            var mapPath = MapService.MapPathFor(proj);
            var mapExists = ctx.Db.FindByPath(mapPath) is not null ||
                            File.Exists(PathGuard.ResolveNotePath(ctx.VaultRoot, mapPath));
            if (apply)
            {
                var result = mapExists ? ctx.Maps.Rebuild(proj.Title) : ctx.Maps.Create(proj.Title);
                warnings.AddRange(result.Warnings);
                artifacts.Add(new CompileArtifact("map", result.Path,
                    mapExists ? "rebuilt" : "created", result.Message));
            }
            else
            {
                artifacts.Add(new CompileArtifact("map", mapPath,
                    mapExists ? "would rebuild" : "would create",
                    "generated block only; human sections preserved"));
            }

            var summaries = ctx.Summaries.ForProject(proj.Title, apply);
            warnings.AddRange(summaries.Warnings);
            var needsReview = summaries.Proposals.Count(p => p.NeedsReview);
            artifacts.Add(new CompileArtifact("summaries", proj.Title,
                apply ? $"applied {summaries.Applied}" : $"would update {summaries.Proposals.Count}",
                $"{summaries.NotesConsidered} large note(s) considered; {needsReview} marked needsReview"));
        }

        var graph = ctx.Graph.Build(project, write: apply);
        artifacts.Add(new CompileArtifact("link-graph",
            graph.SidecarPath ?? ".mindvault/link-graph.jsonl",
            apply ? "written" : "would write",
            $"{graph.EdgeCount} typed edge(s) across {graph.EdgesByType.Count} relationship type(s)"));

        var broken = ctx.LinkIntel.BrokenLinks();
        var orphans = ctx.LinkIntel.Orphans(null);
        var lowValue = ctx.LowValue.Find(project);
        artifacts.Add(new CompileArtifact("health-report", project ?? "(vault)", "computed",
            $"{broken.Rows.Count} broken link(s), {orphans.Rows.Count} orphan(s), " +
            $"{lowValue.Notes.Count} low-value note(s)"));

        var ta = ctx.TokenAudit.Run(project);
        artifacts.Add(new CompileArtifact("token-audit", project ?? "(vault)", "computed",
            $"~{ta.ActiveEstimatedTokens} active tokens; ~{ta.EstimatedTokenWaste} estimated waste; " +
            $"{ta.LargeNoteCount - ta.LargeWithSummaryCount} large note(s) unsummarized"));

        var score = ctx.OrgScore.Run(project);
        artifacts.Add(new CompileArtifact("organisation-score", project ?? "(vault)", "computed",
            $"{score.OverallScore}/100" +
            (score.Weaknesses.Count > 0 ? $"; weakest: {score.Weaknesses[0]}" : "; no weak categories")));

        return new CompileReport(!apply, project, score.OverallScore, artifacts, warnings);
    }
}
