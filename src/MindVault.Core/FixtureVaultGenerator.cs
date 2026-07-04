using System.Text;

namespace MindVault.Core;

public sealed record GeneratedVaultStats(
    int Projects, int Decisions, int Tasks, int Risks, int Architecture, int Constraints,
    int Logs, int Resources, int ArchivedNotes, int SupersededDecisions, int BrokenLinks,
    int StaleTasks, int DuplicateishTitles, int TotalNotes);

/// <summary>
/// Dev/test helper: generates a synthetic but realistic vault for benchmarks and evals.
/// Deterministic for a given (seed, today) pair. Includes the messy realities on purpose:
/// broken links, archived notes, superseded decision chains, duplicate-ish titles and stale
/// tasks. Refuses to write into a non-empty directory — it can never touch a real vault.
/// </summary>
public static class FixtureVaultGenerator
{
    private static readonly string[] Domains =
    [
        "sync engine", "auth service", "cache layer", "message queue", "search index",
        "billing pipeline", "telemetry collector", "config loader", "retry policy", "rate limiter",
        "backup scheduler", "migration runner", "webhook dispatcher", "session store", "export module",
    ];

    private static readonly string[] Verbs =
    [
        "Refactor", "Harden", "Migrate", "Optimize", "Instrument", "Simplify",
        "Stabilize", "Document", "Benchmark", "Decouple",
    ];

    private static readonly string[] Techs =
    [
        "SQLite", "Redis", "PostgreSQL", "gRPC", "GraphQL", "RabbitMQ",
        "Kestrel", "Docker", "Nginx", "Prometheus",
    ];

    public static GeneratedVaultStats Generate(string path, int projects = 10, int notesPerProject = 100,
        int seed = 1337, DateTime? today = null)
    {
        if (projects < 1 || notesPerProject < 5)
            throw new MindVaultException("generate-fixture-vault needs at least 1 project and 5 notes per project.");
        var root = Path.GetFullPath(path);
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new MindVaultException(
                $"Refusing to generate a fixture vault into a non-empty directory: {root}. " +
                "Point --path at a new or empty folder — this command must never touch a real vault.");

        Directory.CreateDirectory(root);
        VaultStructure.EnsureStructure(root);
        var rng = new Random(seed);
        var day = (today ?? DateTime.Today).Date;

        int decisions = 0, tasks = 0, risks = 0, architecture = 0, constraints = 0, logs = 0,
            resources = 0, archived = 0, superseded = 0, brokenLinks = 0, staleTasks = 0, dupes = 0;

        for (var p = 1; p <= projects; p++)
        {
            var projName = $"Genproj {p:00}";
            var domain = Domains[(p - 1) % Domains.Length];
            Write(root, $"01_Projects/{projName}.md", $"""
                ---
                type: project
                status: active
                created: {D(day.AddDays(-200 - p))}
                updated: {D(day.AddDays(-p))}
                tags: [generated, {Slug(domain)}]
                ---

                # {projName}

                ## Goal

                Ship a reliable {domain} for internal tooling with predictable latency.

                ## Non-Negotiables

                - No data loss on crash
                - {Techs[p % Techs.Length]} stays the storage layer

                ## Open Questions

                - How far can the {domain} scale before sharding?

                ## Active Work

                - Ongoing hardening of the {domain}.
                """);

            var lastDecisionStem = (string?)null;
            for (var n = 1; n <= notesPerProject - 1; n++)
            {
                var kind = n % 10;
                var tech = Techs[(n + p) % Techs.Length];
                var verb = Verbs[(n + p) % Verbs.Length];
                var created = day.AddDays(-((n * 3 + p) % 400) - 1);
                var updated = day.AddDays(-((n * 7 + p) % 90));

                if (kind is 0 or 1 or 2) // tasks (30%)
                {
                    tasks++;
                    var stale = n % 30 == 2;
                    if (stale) { staleTasks++; updated = day.AddDays(-120 - n % 60); }
                    var dupe = n % 40 == 12;
                    var title = dupe
                        ? $"Task - {verb} the {domain} for {projName}"
                        : $"Task - {verb} {domain} {n:000} for {projName}";
                    if (dupe) { dupes++; title = n % 80 == 12 ? title : title.Replace(" the ", " "); }
                    var isArchived = n % 20 == 10; // lands on a task index (n%10==0) even in small vaults
                    var folder = isArchived ? "99_Archive" : "01_Projects";
                    var status = isArchived ? "archived" : stale ? "open" : (n % 3 == 0 ? "done" : "open");
                    if (isArchived) archived++;
                    Write(root, $"{folder}/{title}.md", $"""
                        ---
                        type: task
                        status: {status}
                        project: {projName}
                        created: {D(created)}
                        updated: {D(updated)}
                        tags: [task, {Slug(domain)}]
                        links: ["[[{projName}]]"]
                        ---

                        # {title[7..]}

                        ## Context

                        The {domain} needs work on its {tech} integration; see [[{projName}]].

                        ## Acceptance Criteria

                        - {verb} pass completes with tests green
                        - No regression in {domain} throughput

                        ## Status Notes

                        - {D(updated)} — investigated {tech} behaviour under load.
                        """);
                }
                else if (kind is 3 or 4) // decisions (20%), every 4th superseded by the next
                {
                    decisions++;
                    var stem = $"Decision - Use {tech} for the {domain} {n:000} ({projName})";
                    var isSuperseded = kind == 3 && n % 20 == 3 && lastDecisionStem is not null;
                    string extra = "";
                    var status = "accepted";
                    if (isSuperseded)
                    {
                        // The PREVIOUS decision becomes superseded by this one (chain of two).
                        superseded++;
                        extra = $"supersedes: [\"[[{lastDecisionStem}]]\"]\n";
                    }
                    var brokenLink = n % 15 == 4;
                    if (brokenLink) brokenLinks++;
                    Write(root, $"04_Decisions/{stem}.md", $"""
                        ---
                        type: decision
                        status: {status}
                        project: {projName}
                        created: {D(created)}
                        updated: {D(updated)}
                        tags: [decision, {Slug(domain)}]
                        links: ["[[{projName}]]"]
                        {extra}---

                        # Use {tech} for the {domain} {n:000}

                        ## Context

                        The {domain} of {projName} needed a dependable backbone.{(brokenLink ? $" Related: [[Missing Spec {n:000}-{p}]]" : "")}

                        ## Decision

                        Adopt {tech} for the {domain}.

                        ## Reasoning

                        {tech} won on operational simplicity and latency.

                        ## Reversal Conditions

                        - {tech} cannot hold p99 under load
                        """);
                    if (isSuperseded && lastDecisionStem is not null)
                    {
                        // Rewrite the previous decision as superseded (deterministic content).
                        var prevPath = Path.Combine(root, "04_Decisions", lastDecisionStem + ".md");
                        var prev = File.ReadAllText(prevPath)
                            .Replace("status: accepted", "status: superseded")
                            .Replace("links: [", $"superseded_by: [\"[[{stem}]]\"]\nlinks: [");
                        File.WriteAllText(prevPath, prev);
                    }
                    lastDecisionStem = stem;
                }
                else if (kind == 5) // risks (10%)
                {
                    risks++;
                    Write(root, $"03_Resources/Risk - {domain} saturation {n:000} ({projName}).md", $"""
                        ---
                        type: risk
                        status: open
                        project: {projName}
                        created: {D(created)}
                        updated: {D(updated)}
                        tags: [risk]
                        links: ["[[{projName}]]"]
                        ---

                        # {domain} saturation {n:000}

                        ## Risk

                        The {domain} may saturate under peak load.

                        ## Mitigation

                        Watch {tech} queue depth; add backpressure before it breaks.
                        """);
                }
                else if (kind == 6) // architecture (10%)
                {
                    architecture++;
                    Write(root, $"02_Areas/Architecture - {domain} of {projName}.md", $"""
                        ---
                        type: architecture
                        status: active
                        project: {projName}
                        created: {D(created)}
                        updated: {D(updated)}
                        tags: [architecture, {Slug(domain)}]
                        links: ["[[{projName}]]"]
                        ---

                        # Architecture - {domain} of {projName}

                        ## Overview

                        Request -> {tech} -> {domain} -> store. Writes are serialized; reads fan out.

                        ## Components

                        - ingress adapter
                        - {domain} core
                        - {tech} persistence
                        """);
                }
                else if (kind == 7) // constraints (10%)
                {
                    constraints++;
                    Write(root, $"03_Resources/Constraint - {tech} budget {n:000} ({projName}).md", $"""
                        ---
                        type: constraint
                        status: active
                        project: {projName}
                        created: {D(created)}
                        updated: {D(updated)}
                        tags: [constraint]
                        links: ["[[{projName}]]"]
                        ---

                        # {tech} budget {n:000}

                        ## Constraint

                        The {domain} must stay under 512 MB RSS on the target host.
                        """);
                }
                else // resources/research (20%)
                {
                    resources++;
                    Write(root, $"03_Resources/Notes on {tech} {verb.ToLowerInvariant()} {n:000} ({projName}).md", $"""
                        ---
                        type: research
                        status: draft
                        created: {D(created)}
                        updated: {D(updated)}
                        tags: [research, {Slug(domain)}]
                        ---

                        # Notes on {tech} {verb.ToLowerInvariant()} {n:000}

                        Reading notes about {tech} and how it applies to the {domain}.
                        Key figure: throughput {1000 + rng.Next(9000)} ops/s in the reference setup.
                        """);
                }
            }

            logs++;
            Write(root, $"06_Agent_Memory/Log - {projName}.md", $"""
                ---
                type: memory
                status: active
                project: {projName}
                created: {D(day.AddDays(-100))}
                updated: {D(day.AddDays(-1))}
                tags: [log]
                links: ["[[{projName}]]"]
                ---

                # Log - {projName}

                ## Sessions

                ### {D(day.AddDays(-8))} — initial hardening pass

                - Tests: green
                - Follow-ups: watch the {domain}

                ### {D(day.AddDays(-1))} — tuned the {domain}

                - Tests: green
                - Follow-ups: none
                """);
        }

        var total = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Count(f => !f.Contains(".mindvault"));
        return new GeneratedVaultStats(projects, decisions, tasks, risks, architecture, constraints,
            logs, resources, archived, superseded, brokenLinks, staleTasks, dupes, total);
    }

    private static void Write(string root, string relativePath, string content)
    {
        var full = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content.Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
    }

    private static string D(DateTime d) => d.ToString("yyyy-MM-dd");

    private static string Slug(string s) => s.Replace(' ', '-').ToLowerInvariant();
}
