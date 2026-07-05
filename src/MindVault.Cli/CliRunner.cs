using MindVault.Core;

namespace MindVault.Cli;

/// <summary>
/// Command dispatch for the mindvault CLI. Kept as a plain class (writers injected) so tests can
/// drive it end to end. Exit codes: 0 ok, 1 unexpected/validation errors, 2 known error or bad
/// usage, 3 ambiguous note reference.
/// </summary>
public static class CliRunner
{
    public static int Run(string[] argv, TextWriter stdout, TextWriter stderr,
        Func<string, string?>? getEnv = null, string? workingDirectory = null)
    {
        CliArgs args;
        try
        {
            args = CliArgs.Parse(argv);
        }
        catch (MindVaultException ex)
        {
            stderr.WriteLine(ex.Message);
            return 2;
        }

        if (args.Has("version") ||
            (args.Positionals.Count > 0 && args.Positionals[0].Equals("version", StringComparison.OrdinalIgnoreCase)))
        {
            if (args.Has("json"))
                stdout.WriteLine(Json.Serialize(new
                {
                    ok = true,
                    version = MindVaultVersion.Current,
                    indexSchemaVersion = IndexDatabase.CurrentSchemaVersion,
                }));
            else
                stdout.WriteLine($"mindvault {MindVaultVersion.Current} (index schema v{IndexDatabase.CurrentSchemaVersion})");
            return 0;
        }

        if (args.Positionals.Count == 0 || args.Has("help"))
        {
            stdout.WriteLine(UsageText);
            return args.Positionals.Count == 0 && !args.Has("help") && argv.Length > 0 ? 2 : 0;
        }

        var command = args.Positionals[0].ToLowerInvariant();
        var json = args.Has("json");
        var vaultArg = args.Opt("vault");
        // --quiet silences success/progress chatter on mutation commands; results and errors still print.
        var infoOut = args.Has("quiet") && !json ? TextWriter.Null : stdout;
        var timer = args.Has("verbose") ? System.Diagnostics.Stopwatch.StartNew() : null;

        try
        {
            if (command == "status") return CmdStatus(args, stdout, stderr, json, vaultArg, getEnv, workingDirectory);
            if (command == "generate-fixture-vault") return CmdGenerateFixtureVault(args, stdout, json);

            using var ctx = VaultContext.Create(vaultArg, getEnv, workingDirectory);
            var exit = command switch
            {
                "init" => CmdInit(ctx, infoOut, json),
                "scan" => CmdScan(ctx, infoOut, json, full: args.Has("full")),
                "rebuild-index" => CmdScan(ctx, infoOut, json, full: true),
                "validate" => CmdValidate(ctx, stdout, json),
                "doctor" => CmdDoctor(ctx, stdout, json),
                "index" => CmdIndex(ctx, args, stdout, infoOut, json),
                "search" => CmdSearch(ctx, args, stdout, json),
                "read" => CmdRead(ctx, args, stdout, json),
                "list" => CmdList(ctx, args, stdout, json),
                "create" => CmdCreate(ctx, args, infoOut, json),
                "append" => CmdAppend(ctx, args, infoOut, json, workingDirectory),
                "update-frontmatter" => CmdUpdateFrontmatter(ctx, args, infoOut, json),
                "link" => CmdLink(ctx, args, infoOut, json),
                "archive" => CmdArchive(ctx, args, infoOut, json),
                "restore" => CmdRestore(ctx, args, infoOut, json),
                "backup" => CmdBackup(ctx, infoOut, json),
                "prune" => CmdPrune(ctx, args, infoOut, json),
                "project-context" => CmdProjectContext(ctx, args, stdout),
                "detect-project" => CmdDetectProject(ctx, args, stdout, json),
                "related" => CmdRelated(ctx, args, stdout, json),
                "context" => CmdContext(ctx, args, stdout, json),
                "context-pack" => CmdContextPack(ctx, args, stdout, json),
                "check-note" => CmdCheckNote(ctx, args, stdout, json),
                "check-draft" => CmdCheckDraft(ctx, args, stdout, json),
                "decision" => CmdDecision(ctx, args, stdout, infoOut, json),
                "session" => CmdSession(ctx, args, stdout, infoOut, json),
                "organize" => CmdOrganize(ctx, args, stdout, json),
                "promote" => CmdPromote(ctx, args, infoOut, json),
                "map" => CmdMap(ctx, args, stdout, infoOut, json),
                "links" => CmdLinks(ctx, args, stdout, infoOut, json),
                "frontmatter" => CmdFrontmatter(ctx, args, stdout, json),
                "aliases" => CmdAliases(ctx, args, stdout, json),
                "capsule" => CmdCapsule(ctx, args, stdout, json),
                "work-context" => CmdWorkContext(ctx, args, stdout, json),
                "recall" => CmdRecall(ctx, args, stdout, json),
                "ops" => CmdOps(ctx, stdout, json),
                "pin" => CmdRecordFeedback(ctx, args, infoOut, json, "pinned"),
                "hide" => CmdRecordFeedback(ctx, args, infoOut, json, "hidden"),
                "feedback" => CmdRecordFeedback(ctx, args, infoOut, json, null),
                "mistake" => CmdMistake(ctx, args, stdout, json),
                "inbox" => CmdInbox(ctx, args, stdout, json),
                "compile" => CmdCompile(ctx, args, stdout, json),
                "route" => CmdRoute(ctx, args, stdout, json),
                "read-plan" => CmdReadPlan(ctx, args, stdout),
                "token-audit" => CmdTokenAudit(ctx, args, stdout, json),
                "summarize" => CmdSummarize(ctx, args, stdout, json),
                "organisation-score" => CmdOrganisationScore(ctx, args, stdout, json),
                "graph" => CmdGraph(ctx, args, stdout, json),
                "low-value" => CmdLowValue(ctx, args, stdout, json),
                _ => Unknown(command, stdout, stderr, json),
            };
            if (timer is not null)
                stderr.WriteLine($"[verbose] {command} completed in {timer.ElapsedMilliseconds} ms");
            return exit;
        }
        catch (AmbiguousNoteRefException ex)
        {
            return Fail(ex.Message, ex.Code, stdout, stderr, json, 3);
        }
        catch (DuplicateSuspectedException ex)
        {
            if (json)
            {
                stdout.WriteLine(Json.Serialize(new
                {
                    ok = false, created = false, reason = "possible_duplicate",
                    error = ex.Message, code = ex.Code, candidates = ex.Candidates,
                }));
                return 2;
            }
            stderr.WriteLine(ex.Message);
            stderr.WriteLine("Pass --allow-duplicate to create it anyway.");
            return 2;
        }
        catch (MindVaultException ex)
        {
            return Fail(ex.Message, ex.Code, stdout, stderr, json, 2);
        }
        catch (Exception ex)
        {
            return Fail($"Unexpected error: {ex.Message}", ErrorCodes.Unexpected, stdout, stderr, json, 1);
        }
    }

    private static int Fail(string message, string errorCode, TextWriter stdout, TextWriter stderr, bool json, int code)
    {
        if (json) stdout.WriteLine(Json.Serialize(new { ok = false, error = message, code = errorCode }));
        else stderr.WriteLine(message);
        return code;
    }

    private static int Unknown(string command, TextWriter stdout, TextWriter stderr, bool json)
    {
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = false, error = $"Unknown command: {command}" }));
            return 2;
        }
        stderr.WriteLine($"Unknown command: {command}");
        stdout.WriteLine(UsageText);
        return 2;
    }

    // ---------- commands ----------

    private static int CmdStatus(CliArgs args, TextWriter stdout, TextWriter stderr, bool json,
        string? vaultArg, Func<string, string?>? getEnv, string? cwd)
    {
        LoadedConfig loaded;
        try
        {
            loaded = ConfigLoader.Load(vaultArg, getEnv, cwd);
        }
        catch (MindVaultConfigException ex)
        {
            return Fail(ex.Message, ex.Code, stdout, stderr, json, 2);
        }

        var vaultPath = loaded.Config.VaultPath!;
        if (!Directory.Exists(vaultPath))
            return Fail($"Vault path does not exist: {vaultPath} (source: {loaded.VaultPathSource})",
                ErrorCodes.VaultNotFound, stdout, stderr, json, 2);

        using var ctx = new VaultContext(loaded);
        var state = ctx.State.Load();
        int? noteCount = ctx.IndexExists ? ctx.Db.CountNotes() : null;
        bool? rescanPending = ctx.IndexExists ? ctx.Db.NeedsRescan : null;
        var foldersOk = VaultStructure.RequiredFolders.All(f => Directory.Exists(Path.Combine(ctx.VaultRoot, f)));

        var placeholder = DoctorService.LooksLikePlaceholderPath(ctx.VaultRoot);
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true,
                version = MindVaultVersion.Current,
                vaultPath = ctx.VaultRoot,
                configSource = ctx.ConfigSource,
                vaultPathLooksLikePlaceholder = placeholder,
                initialized = foldersOk,
                indexPath = ctx.IndexFile,
                indexExists = ctx.IndexExists,
                noteCount,
                rescanPending,
                lastScanUtc = state?.LastScanUtc,
            }));
        }
        else
        {
            stdout.WriteLine($"MindVault status (v{MindVaultVersion.Current})");
            stdout.WriteLine($"  vault:  {ctx.VaultRoot}");
            stdout.WriteLine($"  config: {ctx.ConfigSource}");
            if (placeholder)
                stdout.WriteLine("  warning: the vault path looks like a placeholder — point it at your real vault");
            stdout.WriteLine($"  folders: {(foldersOk ? "ok" : "incomplete (run 'init')")}");
            stdout.WriteLine(ctx.IndexExists
                ? $"  index:  {noteCount} notes, last scan {state?.LastScanUtc:yyyy-MM-dd HH:mm:ss}Z" +
                  (rescanPending == true ? " (rescan pending — run 'scan')" : "")
                : "  index:  not built yet (run 'scan')");
        }
        return 0;
    }

    private static int CmdGenerateFixtureVault(CliArgs args, TextWriter stdout, bool json)
    {
        // Dev/test helper: builds a synthetic vault for benchmarks and evals.
        // Refuses non-empty directories, so it can never damage a real vault.
        var path = args.Require("path");
        var stats = FixtureVaultGenerator.Generate(path,
            args.IntOpt("projects", 10),
            args.IntOpt("notes-per-project", 100),
            args.IntOpt("seed", 1337));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = true, path = Path.GetFullPath(path), stats }));
        }
        else
        {
            stdout.WriteLine($"Generated fixture vault at {Path.GetFullPath(path)}");
            stdout.WriteLine($"  {stats.TotalNotes} notes: {stats.Projects} projects, {stats.Decisions} decisions " +
                             $"({stats.SupersededDecisions} superseded), {stats.Tasks} tasks ({stats.StaleTasks} stale), " +
                             $"{stats.Risks} risks, {stats.Architecture} architecture, {stats.Constraints} constraints, " +
                             $"{stats.Logs} logs, {stats.Resources} research");
            stdout.WriteLine($"  messiness: {stats.BrokenLinks} broken links, {stats.ArchivedNotes} archived, " +
                             $"{stats.DuplicateishTitles} duplicate-ish titles");
        }
        return 0;
    }

    private static int CmdIndex(VaultContext ctx, CliArgs args, TextWriter stdout, TextWriter infoOut, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "status":
            {
                var s = ctx.IndexCheck.Status();
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, index = s }));
                }
                else
                {
                    stdout.WriteLine("MindVault index");
                    stdout.WriteLine($"  path:    {s.IndexPath}");
                    stdout.WriteLine($"  exists:  {s.IndexExists}{(s.IndexExists ? $" ({s.IndexSizeBytes / 1024} KB)" : "")}");
                    stdout.WriteLine($"  schema:  v{s.SchemaVersion} (expected v{s.ExpectedSchemaVersion})");
                    stdout.WriteLine($"  notes:   {s.NoteCount} (fts rows: {s.FtsRowCount})");
                    stdout.WriteLine($"  scanned: {(s.LastScanUtc is null ? "never" : s.LastScanUtc.Value.ToString("yyyy-MM-dd HH:mm:ss") + "Z")}" +
                                     (s.RescanPending ? " (rescan pending)" : ""));
                }
                return 0;
            }
            case "verify":
            {
                var report = ctx.IndexCheck.Verify();
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = report.Ok, issues = report.Issues,
                        recommendation = report.Recommendation, elapsedMs = report.ElapsedMs,
                    }));
                }
                else
                {
                    foreach (var issue in report.Issues)
                        stdout.WriteLine($"{issue.Code}: {issue.Message}{(issue.Path is null ? "" : $" [{issue.Path}]")}");
                    stdout.WriteLine(report.Ok
                        ? $"index verify: ok ({report.ElapsedMs} ms)"
                        : $"index verify: {report.Issues.Count} issue(s) ({report.ElapsedMs} ms). {report.Recommendation}");
                }
                return report.Ok ? 0 : 1;
            }
            case "rebuild":
                return CmdScan(ctx, infoOut, json, full: true);
            default:
                throw new MindVaultException("Usage: index status | index verify | index rebuild");
        }
    }

    private static int CmdInit(VaultContext ctx, TextWriter stdout, bool json)
    {
        var result = VaultStructure.EnsureStructure(ctx.VaultRoot);
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true,
                createdFolders = result.CreatedFolders,
                createdTemplates = result.CreatedTemplates,
            }));
        }
        else if (result.CreatedFolders.Count == 0 && result.CreatedTemplates.Count == 0)
        {
            stdout.WriteLine("Vault already initialized; nothing to create.");
        }
        else
        {
            foreach (var f in result.CreatedFolders) stdout.WriteLine($"created folder:   {f}");
            foreach (var t in result.CreatedTemplates) stdout.WriteLine($"created template: {t}");
        }
        return 0;
    }

    private static int CmdScan(VaultContext ctx, TextWriter stdout, bool json, bool full)
    {
        var result = ctx.Scanner.Scan(full);
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, result.Added, result.Updated, result.Removed, result.Unchanged, result.Errors,
            }));
        }
        else
        {
            var label = full ? "rebuild" : "scan";
            stdout.WriteLine($"{label}: {result.Added} added, {result.Updated} updated, " +
                             $"{result.Removed} removed, {result.Unchanged} unchanged");
            foreach (var error in result.Errors) stdout.WriteLine($"  error: {error}");
        }
        return 0;
    }

    private static int CmdValidate(VaultContext ctx, TextWriter stdout, bool json)
    {
        var report = ctx.Validator.Validate();
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = report.CriticalCount == 0,
                criticals = report.CriticalCount,
                warnings = report.WarningCount,
                infos = report.InfoCount,
                elapsedMs = report.ElapsedMs,
                issues = report.Issues,
            }));
        }
        else
        {
            foreach (var issue in report.Issues)
            {
                var location = issue.Path is null ? "" : $" [{issue.Path}]";
                stdout.WriteLine($"{issue.Severity.ToString().ToUpperInvariant(),-8} {issue.Code}: {issue.Message}{location}");
            }
            stdout.WriteLine($"validate: {report.CriticalCount} critical, {report.WarningCount} warning(s), " +
                             $"{report.InfoCount} info ({report.ElapsedMs} ms)");
        }
        return report.CriticalCount == 0 ? 0 : 1;
    }

    private static int CmdDoctor(VaultContext ctx, TextWriter stdout, bool json)
    {
        var r = ctx.Doctor.Run();
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = r.Verdict != "critical",
                health = r.Verdict,
                healthReasons = r.VerdictReasons,
                report = r,
            }));
        }
        else
        {
            stdout.WriteLine($"MindVault doctor (v{r.AppVersion})");
            stdout.WriteLine($"  health:           {r.Verdict.ToUpperInvariant()}" +
                             (r.VerdictReasons is { Count: > 0 } ? $" — {r.VerdictReasons[0]}" : ""));
            stdout.WriteLine($"  vault path:       {r.VaultPath}");
            stdout.WriteLine($"  vault writable:   {r.VaultWritable}");
            stdout.WriteLine($"  config source:    {r.ConfigSource}");
            stdout.WriteLine($"  local config:     {(r.LocalConfigFound ? r.ConfigFilePath : "not found (using CLI/env)")}");
            stdout.WriteLine($"  index path:       {r.IndexPath}");
            stdout.WriteLine($"  index exists:     {r.IndexExists} (schema v{r.IndexSchemaVersion}, expected v{r.ExpectedSchemaVersion})");
            stdout.WriteLine($"  snapshots:        {r.SnapshotPath} (writable: {r.SnapshotWritable})");
            stdout.WriteLine($"  last scan (utc):  {(r.LastScanUtc is null ? "never" : r.LastScanUtc.Value.ToString("yyyy-MM-dd HH:mm:ss"))}");
            stdout.WriteLine($"  notes:            {r.NoteCount}");
            stdout.WriteLine($"  broken links:     {r.BrokenLinkCount}");
            stdout.WriteLine($"  duplicate titles: {r.DuplicateTitleCount}");
            stdout.WriteLine($"  container:        {(r.RunningInContainer ? $"yes (/vault mounted: {r.ContainerVaultMounted})" : "no")}");
            stdout.WriteLine($"  user:             {r.User}");
            var mcp = r.McpEnvironment;
            stdout.WriteLine($"  mcp env:          transport={mcp.Transport ?? "-"} host={mcp.Host ?? "-"} port={mcp.Port ?? "-"} " +
                             $"token={(mcp.AuthTokenSet ? "set" : mcp.AuthTokenFileSet ? "file" : "not set")}" +
                             (mcp.AllowAnonymous ? " anonymous=ENABLED" : ""));
            stdout.WriteLine($"  watcher:          {r.WatcherStatus}");
            foreach (var warning in r.Warnings)
                stdout.WriteLine($"  warning: {warning}");
        }
        return 0;
    }

    private static int CmdSearch(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        if (args.Positionals.Count < 2)
            throw new MindVaultException(
                "Usage: search \"<query>\" [--type t] [--project p] [--tag t] [--status s] [--limit n] " +
                "[--updated-after yyyy-MM-dd] [--updated-before yyyy-MM-dd] [--include-archived] [--explain] [--json]");
        var results = ctx.Search.Search(args.Positionals[1],
            args.Opt("type"), args.Opt("project"), args.Opt("tag"), args.Opt("status"),
            args.IntOpt("limit", SearchService.DefaultLimit),
            args.Opt("updated-after"), args.Opt("updated-before"),
            args.Has("include-archived"), args.Has("explain"));

        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = true, count = results.Count, results }));
        }
        else if (results.Count == 0)
        {
            stdout.WriteLine("no matches");
        }
        else
        {
            if (results[0].Scope == "global-fallback")
                stdout.WriteLine("(no matches inside the project — showing vault-wide results)");
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var meta = string.Join("/", new[] { r.Type, r.Status }.Where(v => v is not null));
                var section = r.Section is null ? "" : $"  § {r.Section}";
                stdout.WriteLine($"{i + 1}. {r.Title}{(meta.Length > 0 ? $" [{meta}]" : "")}  ({r.Path}){section}");
                if (r.Snippet.Length > 0)
                    stdout.WriteLine($"   {r.Snippet.Replace('\n', ' ')}");
                if (r.Why is { Count: > 0 })
                    stdout.WriteLine($"   why: {string.Join(", ", r.Why)} => {r.Score}");
            }
        }
        return 0;
    }

    private static int CmdRead(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var noteRef = args.Positionals.Count > 1 ? args.Positionals[1] : args.Opt("note");
        if (string.IsNullOrWhiteSpace(noteRef))
            throw new MindVaultException("Usage: read \"<note-ref>\" [--json]");
        var note = ctx.Resolver.Resolve(noteRef);
        var abs = ctx.Resolver.AbsolutePathOf(note);
        var content = File.ReadAllText(abs);

        if (json)
        {
            var parsed = NoteParser.Parse(content, note.Path);
            var frontmatter = parsed.FrontmatterEntries.ToDictionary(
                e => e.Key,
                e => e.IsList ? (object)e.Items : e.Scalar ?? "");
            var backlinks = ctx.Db.GetBacklinkPaths(
                SlugHelper.NormalizeWiki(note.Title), SlugHelper.NormalizeWiki(note.Stem), note.Id);
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true,
                path = note.Path,
                title = note.Title,
                type = note.Type,
                status = note.Status,
                project = note.Project,
                frontmatter,
                body = parsed.Body,
                backlinks,
            }));
        }
        else
        {
            stdout.WriteLine(content);
        }
        return 0;
    }

    private static int CmdList(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var notes = ctx.Search.List(args.Opt("type"), args.Opt("project"),
            args.Opt("status"), args.Opt("tag"), args.IntOpt("limit", 20));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true,
                count = notes.Count,
                notes = notes.Select(n => new { n.Path, n.Title, n.Type, n.Status, n.Project, n.Updated }),
            }));
        }
        else if (notes.Count == 0)
        {
            stdout.WriteLine("no notes matched");
        }
        else
        {
            foreach (var n in notes)
                stdout.WriteLine($"{n.Type ?? "-",-12} {n.Status ?? "-",-10} {n.Title}  ({n.Path})");
        }
        return 0;
    }

    private static int CmdCreate(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        if (args.Positionals.Count < 2)
            throw new MindVaultException("Usage: create project \"<name>\" | create decision --project \"<p>\" --title \"<t>\" | create task --project \"<p>\" --title \"<t>\" | create thought \"<title>\" [--content \"<text>\"]");
        var kind = args.Positionals[1].ToLowerInvariant();
        var allowDuplicate = args.Has("allow-duplicate");
        var result = kind switch
        {
            "project" => ctx.Writer.CreateProject(
                args.Positionals.Count > 2 ? args.Positionals[2] : args.Require("name"), allowDuplicate),
            "decision" => ctx.Writer.CreateDecision(args.Require("project"), args.Require("title"), allowDuplicate),
            "task" => ctx.Writer.CreateTask(args.Require("project"), args.Require("title"), allowDuplicate),
            "thought" => ctx.Writer.CaptureThought(
                args.Positionals.Count > 2 ? args.Positionals[2] : args.Require("title"), args.Opt("content")),
            _ => throw new MindVaultException($"Unknown create target: '{kind}'. Use project, decision, task or thought."),
        };
        var note = result.Note;
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, path = note.Path, title = note.Title, type = note.Type,
                warnings = result.Warnings,
            }));
        }
        else
        {
            stdout.WriteLine($"Created {note.Type}: {note.Path}");
            foreach (var warning in result.Warnings) stdout.WriteLine($"  note: {warning}");
        }
        return 0;
    }

    private static int CmdAppend(VaultContext ctx, CliArgs args, TextWriter stdout, bool json, string? cwd)
    {
        var noteRef = args.Require("note");
        var section = args.Require("section");
        var content = args.Opt("content");
        var contentFile = args.Opt("content-file");
        if (content is null == (contentFile is null))
            throw new MindVaultException("Provide exactly one of --content or --content-file.");
        if (contentFile is not null)
        {
            var path = Path.IsPathRooted(contentFile)
                ? contentFile
                : Path.GetFullPath(Path.Combine(cwd ?? Environment.CurrentDirectory, contentFile));
            if (!File.Exists(path))
                throw new MindVaultException($"Content file not found: {path}");
            content = File.ReadAllText(path);
        }

        var result = ctx.Writer.AppendToSection(noteRef, section, content!, args.Has("create-section"),
            args.Has("dry-run"), args.Has("allow-risky-content"));
        if (json) stdout.WriteLine(Json.Serialize(new
        {
            ok = true, dryRun = args.Has("dry-run"), path = result.Path,
            snapshot = result.SnapshotPath, message = result.Message, riskWarnings = result.RiskWarnings,
        }));
        else PrintWrite(stdout, result.SnapshotPath is null ? result.Message : $"{result.Message} (snapshot: {result.SnapshotPath})", result.RiskWarnings);
        return 0;
    }

    private static int CmdUpdateFrontmatter(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var result = ctx.Writer.UpdateFrontmatter(args.Require("note"), args.Require("key"), args.Require("value"),
            args.Has("dry-run"), args.Has("allow-risky-content"));
        if (json) stdout.WriteLine(Json.Serialize(new
        {
            ok = true, dryRun = args.Has("dry-run"), path = result.Path,
            snapshot = result.SnapshotPath, message = result.Message, riskWarnings = result.RiskWarnings,
        }));
        else PrintWrite(stdout, result.SnapshotPath is null ? result.Message : $"{result.Message} (snapshot: {result.SnapshotPath})", result.RiskWarnings);
        return 0;
    }

    private static int CmdLink(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var result = ctx.Writer.LinkNotes(args.Require("from"), args.Require("to"));
        if (json) stdout.WriteLine(Json.Serialize(new { ok = true, path = result.Path, changed = result.Changed }));
        else stdout.WriteLine(result.Message);
        return 0;
    }

    private static int CmdOrganize(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var apply = args.Has("apply");
        var report = apply ? ctx.Organizer.Apply(args.Opt("project")) : ctx.Organizer.Plan(args.Opt("project"));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, dryRun = report.DryRun, proposals = report.Proposals,
                needsReview = report.NeedsReview, warnings = report.Warnings, applied = report.Applied,
            }));
            return 0;
        }
        stdout.WriteLine($"organize{(report.DryRun ? " (dry-run)" : " --apply")}: " +
                         $"{report.Proposals.Count} proposal(s), {report.NeedsReview.Count} need review");
        foreach (var p in report.Proposals)
        {
            stdout.WriteLine($"  move: {p.CurrentPath} -> {p.ProposedPath}");
            stdout.WriteLine($"        {p.Reason} [{p.Confidence}]");
        }
        foreach (var r in report.NeedsReview) stdout.WriteLine($"  review: {r.Path} — {r.Reason}");
        foreach (var w in report.Warnings) stdout.WriteLine($"  warning: {w}");
        if (!report.DryRun)
        {
            foreach (var m in report.Applied)
                stdout.WriteLine($"  moved: {m.FromPath} -> {m.ToPath} (snapshot: {m.SnapshotPath})");
            stdout.WriteLine($"organize: {report.Applied.Count} note(s) moved");
        }
        else if (report.Proposals.Count > 0)
        {
            stdout.WriteLine("Nothing was changed. Re-run with --apply to execute these moves.");
        }
        return 0;
    }

    private static int CmdPromote(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var noteRef = args.Positionals.Count > 1 ? args.Positionals[1] : args.Opt("note");
        if (string.IsNullOrWhiteSpace(noteRef))
            throw new MindVaultException(
                "Usage: promote \"<note-ref>\" --to <decision|memory|task|risk|mistake> [--project \"<p>\"] [--allow-duplicate]");
        var result = ctx.Writer.PromoteNote(noteRef, args.Require("to"), args.Opt("project"),
            args.Has("allow-duplicate"));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, from = result.FromPath, to = result.ToPath, type = result.Type,
                status = result.Status, snapshot = result.SnapshotPath,
                warnings = result.Warnings, suggestions = result.Suggestions,
            }));
            return 0;
        }
        stdout.WriteLine($"Promoted {result.FromPath} -> {result.Type} ({result.Status}) at {result.ToPath}");
        foreach (var w in result.Warnings) stdout.WriteLine($"  note: {w}");
        foreach (var s in result.Suggestions) stdout.WriteLine($"  suggest: {s}");
        return 0;
    }

    private static int CmdMap(VaultContext ctx, CliArgs args, TextWriter stdout, TextWriter infoOut, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "create":
            case "rebuild":
            {
                var result = sub == "create"
                    ? ctx.Maps.Create(args.Require("project"))
                    : ctx.Maps.Rebuild(args.Require("project"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, path = result.Path, snapshot = result.SnapshotPath,
                        message = result.Message, warnings = result.Warnings,
                    }));
                }
                else
                {
                    infoOut.WriteLine(result.Message);
                    foreach (var w in result.Warnings) infoOut.WriteLine($"  note: {w}");
                }
                return 0;
            }
            case "list":
            {
                var maps = ctx.Maps.List();
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, count = maps.Count, maps }));
                }
                else if (maps.Count == 0)
                {
                    stdout.WriteLine("No projects yet. Run: map create --project \"<name>\"");
                }
                else
                {
                    foreach (var m in maps)
                    {
                        var state = m.IsLegacy ? "legacy 09_Maps file — migrate its text and remove it"
                                  : m.HasMapBlock ? "map block present"
                                  : "no map block — run map create";
                        stdout.WriteLine($"  {m.Path} — {state}" +
                                         (m.Updated is null ? "" : $" (updated {m.Updated})"));
                    }
                }
                return 0;
            }
            default:
                throw new MindVaultException("Usage: map create --project \"<p>\" | map rebuild --project \"<p>\" | map list");
        }
    }

    private static int CmdLinks(VaultContext ctx, CliArgs args, TextWriter stdout, TextWriter infoOut, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "suggest":
            {
                var note = args.Opt("note");
                var project = args.Opt("project");
                if (note is null == (project is null))
                    throw new MindVaultException(
                        "Usage: links suggest --note \"<ref>\" [--limit n] | links suggest --project \"<p>\" [--limit n]");
                var suggestions = note is not null
                    ? ctx.LinkIntel.SuggestForNote(note, args.IntOpt("limit", 10))
                    : ctx.LinkIntel.SuggestForProject(project!, args.IntOpt("limit", 20));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, count = suggestions.Count, suggestions }));
                    return 0;
                }
                if (suggestions.Count == 0)
                {
                    stdout.WriteLine("No link suggestions — nothing scored two or more signals.");
                    return 0;
                }
                foreach (var s in suggestions)
                {
                    stdout.WriteLine($"  {s.FromPath} -> {s.ToPath} [{s.Confidence}]");
                    stdout.WriteLine($"    {s.Reason}");
                }
                stdout.WriteLine("Apply one with: links apply --note \"<from>\" --to \"<target>\"");
                return 0;
            }
            case "apply":
            {
                var result = ctx.Writer.LinkNotes(args.Require("note"), args.Require("to"));
                if (json)
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, path = result.Path, changed = result.Changed, snapshot = result.SnapshotPath,
                    }));
                else infoOut.WriteLine(result.Message);
                return 0;
            }
            case "broken":
            {
                var (rows, truncated) = ctx.LinkIntel.BrokenLinks();
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, count = rows.Count, truncated, broken = rows }));
                    return 0;
                }
                foreach (var r in rows) stdout.WriteLine($"  {r.FromPath}: [[{r.Target}]]");
                stdout.WriteLine($"links broken: {rows.Count} broken wiki link(s){(truncated ? " (truncated)" : "")}");
                return 0;
            }
            case "orphans":
            {
                var (rows, truncated) = ctx.LinkIntel.Orphans();
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, count = rows.Count, truncated, orphans = rows }));
                    return 0;
                }
                foreach (var r in rows) stdout.WriteLine($"  {r.Path} ({r.Type}{(r.Status is null ? "" : $", {r.Status}")})");
                stdout.WriteLine($"links orphans: {rows.Count} unlinked managed note(s){(truncated ? " (truncated)" : "")}");
                return 0;
            }
            default:
                throw new MindVaultException("Usage: links suggest | links apply | links broken | links orphans");
        }
    }

    private static int CmdFrontmatter(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        if (sub != "audit")
            throw new MindVaultException("Usage: frontmatter audit [--project \"<p>\"]");
        return PrintAudit(ctx.Audits.AuditFrontmatter(args.Opt("project")), "frontmatter audit", stdout, json);
    }

    private static int CmdAliases(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        if (sub != "audit")
            throw new MindVaultException("Usage: aliases audit");
        return PrintAudit(ctx.Audits.AuditAliases(), "aliases audit", stdout, json);
    }

    private static int PrintAudit(AuditReport report, string label, TextWriter stdout, bool json)
    {
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = report.Findings.All(f => f.Severity != "critical"),
                notesChecked = report.NotesChecked,
                criticals = report.Findings.Count(f => f.Severity == "critical"),
                warnings = report.Findings.Count(f => f.Severity == "warning"),
                infos = report.Findings.Count(f => f.Severity == "info"),
                truncated = report.Truncated,
                findings = report.Findings,
            }));
            return 0;
        }
        foreach (var f in report.Findings)
        {
            stdout.WriteLine($"{f.Severity.ToUpperInvariant(),-8} {f.Code}: {f.Issue}{(f.Path is null ? "" : $" [{f.Path}]")}");
            if (f.Proposal is not null) stdout.WriteLine($"         fix: {f.Proposal}");
        }
        stdout.WriteLine($"{label}: {report.NotesChecked} checked, {report.Findings.Count} finding(s)" +
                         (report.Truncated ? " (truncated)" : ""));
        return 0;
    }

    private static int CmdArchive(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var noteRef = args.Positionals.Count > 1 ? args.Positionals[1] : args.Opt("note");
        if (string.IsNullOrWhiteSpace(noteRef))
            throw new MindVaultException("Usage: archive \"<note-ref>\" [--dry-run]");
        var dryRun = args.Has("dry-run");
        var result = ctx.Writer.Archive(noteRef, dryRun);
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, dryRun, from = result.FromPath, to = result.ToPath,
                snapshot = result.SnapshotPath, warnings = result.Warnings,
            }));
        }
        else if (dryRun)
        {
            foreach (var w in result.Warnings) stdout.WriteLine(w);
        }
        else
        {
            stdout.WriteLine($"Archived {result.FromPath} -> {result.ToPath} (snapshot: {result.SnapshotPath})");
            foreach (var w in result.Warnings) stdout.WriteLine($"  warning: {w}");
        }
        return 0;
    }

    private static int CmdRestore(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var noteRef = args.Positionals.Count > 1 ? args.Positionals[1] : args.Opt("note");
        if (string.IsNullOrWhiteSpace(noteRef))
            throw new MindVaultException("Usage: restore \"<note-ref>\" [--snapshot <path>]");
        var result = ctx.Writer.RestoreFromSnapshot(noteRef, args.Opt("snapshot"));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, path = result.Path,
                restoredFrom = result.RestoredFrom, preRestoreSnapshot = result.PreRestoreSnapshot,
            }));
        }
        else
        {
            stdout.WriteLine($"Restored {result.Path} from {result.RestoredFrom}");
            stdout.WriteLine($"  previous content saved to: {result.PreRestoreSnapshot}");
        }
        return 0;
    }

    private static int CmdPrune(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var days = args.IntOpt("days", ctx.Config.SnapshotRetentionDays);
        var removed = ctx.Snapshots.Prune(days);
        if (json) stdout.WriteLine(Json.Serialize(new { ok = true, removedFiles = removed, retentionDays = days }));
        else stdout.WriteLine($"prune: removed {removed} snapshot file(s) older than {days} day(s)");
        return 0;
    }

    private static int CmdBackup(VaultContext ctx, TextWriter stdout, bool json)
    {
        var result = ctx.Backup.Run();
        if (json) stdout.WriteLine(Json.Serialize(new { ok = true, zip = result.ZipPath, files = result.FileCount }));
        else stdout.WriteLine($"Backup written: {result.ZipPath} ({result.FileCount} files)");
        return 0;
    }

    private static int CmdProjectContext(VaultContext ctx, CliArgs args, TextWriter stdout)
    {
        if (args.Positionals.Count < 2)
            throw new MindVaultException("Usage: project-context \"<project>\" [--limit n]");
        var result = ctx.Projects.Get(args.Positionals[1], args.IntOpt("limit", 10));
        stdout.WriteLine(Json.Serialize(result));
        return 0;
    }

    private static int CmdDetectProject(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        // Default input is the current directory name — exactly what a coding agent has.
        var name = args.Positionals.Count > 1
            ? args.Positionals[1]
            : Path.GetFileName(Environment.CurrentDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var d = ctx.ProjectDetect.Detect(name);
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true,
                input = name,
                project = d.Project?.Title,
                path = d.Project?.Path,
                confidence = d.Confidence,
                matchedVia = d.MatchedVia,
                candidates = d.Candidates,
            }));
            return 0;
        }
        if (d.Project is not null)
        {
            stdout.WriteLine($"{d.Project.Title}  ({d.Project.Path})");
            stdout.WriteLine($"  confidence: {d.Confidence} (via {d.MatchedVia})");
            return 0;
        }
        stdout.WriteLine(d.Ambiguous
            ? $"'{name}' is ambiguous between {d.Candidates.Count} projects:"
            : d.Candidates.Count > 0
                ? $"no confident match for '{name}'; closest:"
                : $"no project matches '{name}'");
        foreach (var c in d.Candidates)
            stdout.WriteLine($"  {c.Title}  ({c.Path}) via {c.MatchedVia}");
        return d.Project is null && d.Candidates.Count == 0 ? 1 : 0;
    }

    private static int CmdRelated(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var noteRef = args.Positionals.Count > 1 ? args.Positionals[1] : args.Opt("note");
        if (string.IsNullOrWhiteSpace(noteRef))
            throw new MindVaultException("Usage: related \"<note-ref>\" [--limit n] [--json]");
        var result = ctx.Related.Get(noteRef, args.IntOpt("limit", RelatedNotesService.DefaultLimit));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = true, result.Title, result.Path, related = result.Related }));
        }
        else if (result.Related.Count == 0)
        {
            stdout.WriteLine($"no related notes found for {result.Path}");
        }
        else
        {
            stdout.WriteLine($"related to {result.Title} ({result.Path}):");
            foreach (var r in result.Related)
                stdout.WriteLine($"  {r.Type ?? "-",-12} {r.Title}  ({r.Path}) — {r.Reason}");
        }
        return 0;
    }

    private static int CmdContext(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        if (args.Positionals.Count < 2)
            throw new MindVaultException("Usage: context \"<project>\" [--brief|--deep] [--limit n] [--json]");
        var detail = args.Has("deep") ? "deep" : args.Has("brief") ? "brief" : "standard";
        var c = ctx.Projects.Get(args.Positionals[1], args.IntOpt("limit", 10), detail);
        if (json)
        {
            stdout.WriteLine(Json.Serialize(c));
            return 0;
        }

        stdout.WriteLine($"# {c.Project} ({c.ProjectNote.Status ?? "no status"})  {c.ProjectNote.Path}");
        if (c.CurrentGoal is not null) stdout.WriteLine($"goal: {c.CurrentGoal.Replace('\n', ' ')}");
        PrintItems(stdout, "non-negotiables", c.NonNegotiables);
        PrintContextItems(stdout, "active tasks", c.ActiveTasks);
        PrintContextItems(stdout, "blocked tasks", c.BlockedTasks);
        PrintContextItems(stdout, "decisions", c.RecentDecisions);
        PrintContextItems(stdout, "open risks", c.OpenRisks);
        PrintContextItems(stdout, "constraints", c.Constraints);
        PrintContextItems(stdout, "architecture", c.RelevantArchitecture);
        PrintItems(stdout, "recent logs", c.RecentImplementationLogs);
        PrintItems(stdout, "open questions", c.KnownUnknowns);
        if (c.RecommendedNextReads.Count > 0)
        {
            stdout.WriteLine("read next:");
            foreach (var read in c.RecommendedNextReads) stdout.WriteLine($"  {read.Path} — {read.Reason}");
        }
        PrintItems(stdout, "warnings", c.Warnings);
        return 0;
    }

    private static int CmdContextPack(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        if (args.Positionals.Count < 2)
            throw new MindVaultException("Usage: context-pack \"<project>\" [--task \"<description>\"] [--output markdown|json] [--limit n]");
        var pack = ctx.Packs.Get(args.Positionals[1], args.Opt("task"), args.IntOpt("limit", 8));
        var output = (args.Opt("output") ?? (json ? "json" : "markdown")).ToLowerInvariant();
        switch (output)
        {
            case "json":
                stdout.WriteLine(Json.Serialize(pack));
                return 0;
            case "markdown":
                stdout.WriteLine(ContextPackService.ToMarkdown(pack));
                return 0;
            default:
                throw new MindVaultException($"Unknown output format '{output}'. Use markdown or json.");
        }
    }

    private static int CmdCheckNote(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var noteRef = args.Positionals.Count > 1 ? args.Positionals[1] : args.Opt("note");
        if (string.IsNullOrWhiteSpace(noteRef))
            throw new MindVaultException("Usage: check-note \"<note-ref>\" [--json]");
        return PrintCheck(ctx.Drafts.CheckNote(noteRef), stdout, json);
    }

    private static int CmdCheckDraft(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var result = ctx.Drafts.CheckDraft(args.Require("type"), args.Opt("project"), args.Require("title"));
        return PrintCheck(result, stdout, json);
    }

    private static int PrintCheck(DraftCheckResult result, TextWriter stdout, bool json)
    {
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = result.Ok, blockers = result.Blockers, warnings = result.Warnings,
                suggestions = result.Suggestions, relatedPaths = result.RelatedPaths,
                likelyDuplicatePaths = result.LikelyDuplicatePaths,
            }));
        }
        else
        {
            foreach (var b in result.Blockers) stdout.WriteLine($"BLOCKER  {b}");
            foreach (var w in result.Warnings) stdout.WriteLine($"WARNING  {w}");
            foreach (var s in result.Suggestions) stdout.WriteLine($"SUGGEST  {s}");
            stdout.WriteLine(result.Ok
                ? "check: ok to proceed"
                : "check: do not create — resolve the blockers first");
        }
        return result.Ok ? 0 : 1;
    }

    private static int CmdDecision(VaultContext ctx, CliArgs args, TextWriter stdout, TextWriter infoOut, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "list":
            {
                var decisions = ctx.Decisions.List(args.Opt("project"), args.Has("all"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, count = decisions.Count, decisions }));
                }
                else if (decisions.Count == 0)
                {
                    stdout.WriteLine("no decisions");
                }
                else
                {
                    foreach (var d in decisions)
                    {
                        var marks = new List<string>();
                        if (d.Supersedes.Count > 0) marks.Add($"supersedes {string.Join(", ", d.Supersedes)}");
                        if (d.SupersededBy.Count > 0) marks.Add($"superseded by {string.Join(", ", d.SupersededBy)}");
                        stdout.WriteLine($"{d.Status ?? "-",-11} {d.Title}  ({d.Path})" +
                                         (marks.Count > 0 ? $"  [{string.Join("; ", marks)}]" : ""));
                    }
                }
                return 0;
            }
            case "graph":
            {
                var graph = ctx.Decisions.Graph(args.Opt("project"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(graph));
                }
                else
                {
                    foreach (var node in graph.Nodes)
                        stdout.WriteLine($"node: {node.Title} [{node.Status ?? "-"}] ({node.Path})");
                    foreach (var edge in graph.Edges)
                        stdout.WriteLine($"edge: {edge.From} --{edge.Kind}--> {edge.To}");
                }
                return 0;
            }
            case "supersede":
            {
                var result = ctx.Writer.SupersedeDecision(args.Require("old"), args.Require("new"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, old = result.OldPath, @new = result.NewPath,
                        oldSnapshot = result.OldSnapshot, newSnapshot = result.NewSnapshot,
                    }));
                }
                else
                {
                    infoOut.WriteLine($"Superseded: {result.OldPath} -> replaced by {result.NewPath}");
                }
                return 0;
            }
            default:
                throw new MindVaultException("Usage: decision list [--project p] [--all] | decision graph [--project p] | decision supersede --old \"<ref>\" --new \"<ref>\"");
        }
    }

    private static int CmdSession(VaultContext ctx, CliArgs args, TextWriter stdout, TextWriter infoOut, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "start":
            {
                var result = ctx.Sessions.Start(args.Require("project"), args.Opt("task"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, logNote = result.LogNotePath, logNoteCreated = result.LogNoteCreated,
                        task = result.Task, pack = result.Pack,
                    }));
                }
                else
                {
                    if (result.LogNoteCreated) stdout.WriteLine($"created session log note: {result.LogNotePath}");
                    stdout.WriteLine(ContextPackService.ToMarkdown(result.Pack));
                }
                return 0;
            }
            case "log":
            case "checkpoint":
            {
                var result = ctx.Sessions.Log(args.Require("project"), args.Require("summary"),
                    args.Has("dry-run"), args.Has("allow-risky-content"));
                if (json)
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, dryRun = args.Has("dry-run"), path = result.Path,
                        snapshot = result.SnapshotPath, riskWarnings = result.RiskWarnings,
                    }));
                else PrintWrite(infoOut, result.Message, result.RiskWarnings);
                return 0;
            }
            case "end":
            case "handoff":
            {
                var result = ctx.Sessions.End(args.Require("project"), args.Require("summary"),
                    args.Opt("tests"), args.Opt("followups"), args.Has("dry-run"), args.Has("allow-risky-content"));
                if (json)
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, dryRun = args.Has("dry-run"), path = result.Path,
                        snapshot = result.SnapshotPath, riskWarnings = result.RiskWarnings,
                    }));
                else PrintWrite(infoOut, args.Has("dry-run") ? result.Message : $"Handoff written to {result.Path}",
                    result.RiskWarnings);
                return 0;
            }
            case "recent":
            {
                var entries = ctx.Sessions.Recent(args.Require("project"), args.IntOpt("limit", 5));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, count = entries.Count, entries }));
                }
                else if (entries.Count == 0)
                {
                    stdout.WriteLine("No session entries yet. Run: session start --project \"<p>\"");
                }
                else
                {
                    foreach (var e in entries) stdout.WriteLine($"  [{e.Kind}] {e.Heading}");
                    stdout.WriteLine($"log note: {entries[0].LogPath}");
                }
                return 0;
            }
            default:
                throw new MindVaultException(
                    "Usage: session start --project p [--task t] | session checkpoint|log --project p --summary s | " +
                    "session handoff|end --project p --summary s [--tests t] [--followups f] | session recent --project p");
        }
    }

    private static void PrintWrite(TextWriter writer, string message, IReadOnlyList<string>? riskWarnings)
    {
        writer.WriteLine(message);
        foreach (var w in riskWarnings ?? []) writer.WriteLine($"  risk: {w}");
    }

    private static int CmdCapsule(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var format = (args.Opt("format") ?? (json ? "json" : "markdown")).ToLowerInvariant();
        var outcome = ctx.Capsules.Build(args.Require("project"), args.Opt("mode") ?? "coding",
            args.IntOpt("max-chars", CapsuleService.DefaultBudget));
        if (outcome.Capsule is null)
        {
            if (json || format == "json")
            {
                stdout.WriteLine(Json.Serialize(new { ok = false, ambiguous = true, candidates = outcome.Candidates }));
            }
            else
            {
                stdout.WriteLine("Project name is ambiguous — pick one:");
                foreach (var c in outcome.Candidates)
                    stdout.WriteLine($"  - {c.Title} ({c.Path}) via {c.MatchedVia}");
            }
            return 3;
        }
        if (json || format == "json")
            stdout.WriteLine(Json.Serialize(new { ok = true, capsule = outcome.Capsule }));
        else
            stdout.WriteLine(CapsuleService.ToMarkdown(outcome.Capsule));
        return 0;
    }

    private static int CmdWorkContext(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var result = ctx.WorkContext.Get(args.Require("project"), args.Opt("current-file"),
            args.Opt("query"), args.Opt("note"), args.IntOpt("limit", 12));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = true, workContext = result }));
            return 0;
        }
        stdout.WriteLine($"work-context for {result.Project} ({result.InputKind}: {result.Input})");
        PrintWorkGroup(stdout, "Decisions", result.Decisions);
        PrintWorkGroup(stdout, "Tasks", result.Tasks);
        PrintWorkGroup(stdout, "Risks", result.Risks);
        PrintWorkGroup(stdout, "Mistakes", result.Mistakes);
        PrintWorkGroup(stdout, "Reviews", result.Reviews);
        PrintWorkGroup(stdout, "Logs/Memory", result.Logs);
        if (result.SuggestedReads.Count > 0)
        {
            stdout.WriteLine("Suggested reads:");
            foreach (var r in result.SuggestedReads) stdout.WriteLine($"  - {r.Path} — {r.Reason}");
        }
        foreach (var w in result.Warnings) stdout.WriteLine($"  warning: {w}");
        return 0;
    }

    private static void PrintWorkGroup(TextWriter stdout, string label, IReadOnlyList<WorkContextItem> items)
    {
        if (items.Count == 0) return;
        stdout.WriteLine($"{label}:");
        foreach (var i in items)
            stdout.WriteLine($"  - {i.Title}{(i.Status is null ? "" : $" [{i.Status}]")} — {i.Reason} ({i.Path})");
    }

    private static int CmdRecall(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var result = ctx.RecallSvc.Recall(args.Opt("project"), args.Opt("since"), args.Has("on-this-day"));
        var format = (args.Opt("format") ?? (json ? "json" : "markdown")).ToLowerInvariant();
        if (json || format == "json")
        {
            stdout.WriteLine(Json.Serialize(new { ok = true, recall = result }));
            return 0;
        }
        stdout.WriteLine($"recall{(result.Project is null ? "" : $" — {result.Project}")}: {result.Window}");
        PrintRecallGroup(stdout, "Decisions", result.Decisions);
        PrintRecallGroup(stdout, "Tasks", result.Tasks);
        PrintRecallGroup(stdout, "Risks", result.Risks);
        PrintRecallGroup(stdout, "Mistakes", result.Mistakes);
        PrintRecallGroup(stdout, "Sessions", result.Sessions);
        PrintRecallGroup(stdout, "Reviews", result.Reviews);
        PrintRecallGroup(stdout, "Other notes", result.Notes);
        foreach (var w in result.Warnings) stdout.WriteLine($"  warning: {w}");
        return 0;
    }

    private static void PrintRecallGroup(TextWriter stdout, string label, IReadOnlyList<RecallItem> items)
    {
        if (items.Count == 0) return;
        stdout.WriteLine($"{label}:");
        foreach (var i in items)
            stdout.WriteLine($"  - {i.Date} [{i.Change}] {i.Title}{(i.Status is null ? "" : $" ({i.Status})")} — {i.Path}");
    }

    private static int CmdOps(VaultContext ctx, TextWriter stdout, bool json)
    {
        var r = ctx.Ops.Run();
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = r.Health != "critical", ops = r }));
            return 0;
        }
        stdout.WriteLine($"MindVault brain ops (v{MindVaultVersion.Current})");
        stdout.WriteLine($"  health:           {r.Health.ToUpperInvariant()}" +
                         (r.HealthReasons.Count > 0 ? $" — {r.HealthReasons[0]}" : ""));
        stdout.WriteLine($"  vault:            {r.VaultPath} ({r.ConfigSource})");
        stdout.WriteLine($"  notes:            {r.NoteCount} ({r.ManagedNoteCount} managed, archived ratio {r.ArchivedRatio:0.00})");
        stdout.WriteLine($"  index:            scanned {r.LastScanUtc ?? "never"}" +
                         (r.IndexAgeMinutes is { } age ? $" ({age} min ago)" : "") +
                         (r.RescanPending ? " — RESCAN PENDING" : ""));
        stdout.WriteLine($"  links:            {r.BrokenLinkCount} broken, {r.OrphanCount} orphan note(s)");
        stdout.WriteLine($"  duplicates:       {r.DuplicateTitleCount} title(s), {r.AliasCollisionCount} alias collision(s)");
        stdout.WriteLine($"  inbox drafts:     {r.InboxDraftCount}");
        stdout.WriteLine($"  open risks:       {r.OpenRiskCount}; active mistakes: {r.ActiveMistakeCount}");
        stdout.WriteLine($"  feedback signals: {r.FeedbackEntryCount}");
        stdout.WriteLine($"  mcp tools:        {r.McpToolCount}; skills: {r.SkillsPack}");
        stdout.WriteLine($"  latest session:   {r.LatestSession ?? "none"}");
        stdout.WriteLine("  recommended:");
        foreach (var f in r.RecommendedFixes) stdout.WriteLine($"    - {f}");
        return 0;
    }

    private static int CmdCompile(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var report = ctx.Compiler.Compile(args.Opt("project"), args.Has("apply"));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, dryRun = report.DryRun, project = report.Project,
                overallScore = report.OverallScore, artifacts = report.Artifacts,
                warnings = report.Warnings,
            }));
            return 0;
        }
        stdout.WriteLine($"compile{(report.DryRun ? " (dry-run)" : " --apply")}" +
                         $"{(report.Project is null ? "" : $" — {report.Project}")}: " +
                         $"{report.Artifacts.Count} artefact(s), score {report.OverallScore}/100");
        foreach (var a in report.Artifacts)
            stdout.WriteLine($"  {a.Kind}: {a.Target} — {a.Status} ({a.Detail})");
        foreach (var w in report.Warnings) stdout.WriteLine($"  warning: {w}");
        if (report.DryRun)
            stdout.WriteLine("Nothing was written. Re-run with --apply to build the artefacts.");
        return 0;
    }

    private static int CmdRoute(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var format = (args.Opt("format") ?? (json ? "json" : "markdown")).ToLowerInvariant();
        var budget = new ContextBudget(
            MaxNotes: args.Options.ContainsKey("max-notes") ? args.IntOpt("max-notes", 5) : null,
            MaxChars: args.Options.ContainsKey("max-chars") ? args.IntOpt("max-chars", 0) : null,
            MaxEstimatedTokens: args.Options.ContainsKey("max-tokens") ? args.IntOpt("max-tokens", 0) : null);
        var outcome = ctx.Routes.Build(args.Require("project"), args.Opt("goal"),
            args.Opt("current-file"), args.Opt("query"), budget);
        if (outcome.Card is null)
        {
            if (json || format == "json")
            {
                stdout.WriteLine(Json.Serialize(new { ok = false, ambiguous = true, candidates = outcome.Candidates }));
            }
            else
            {
                stdout.WriteLine("Project name is ambiguous — pick one:");
                foreach (var c in outcome.Candidates)
                    stdout.WriteLine($"  - {c.Title} ({c.Path}) via {c.MatchedVia}");
            }
            return 3;
        }
        if (json || format == "json")
            stdout.WriteLine(Json.Serialize(new { ok = true, routeCard = outcome.Card }));
        else
            stdout.WriteLine(RouteCardService.ToMarkdown(outcome.Card));
        return 0;
    }

    private static int CmdReadPlan(VaultContext ctx, CliArgs args, TextWriter stdout)
    {
        // Always JSON — a read plan is an agent artifact (mirrors project-context).
        var outcome = ctx.ReadPlans.Build(args.Require("project"), args.Opt("goal"),
            args.Opt("current-file"), args.IntOpt("max-reads", ReadPlanService.DefaultMaxReads));
        if (outcome.Plan is null)
        {
            stdout.WriteLine(Json.Serialize(new { ok = false, ambiguous = true, candidates = outcome.Candidates }));
            return 3;
        }
        stdout.WriteLine(Json.Serialize(new { ok = true, readPlan = outcome.Plan }));
        return 0;
    }

    private static int CmdTokenAudit(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var r = ctx.TokenAudit.Run(args.Opt("project"));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = true, tokenAudit = r }));
            return 0;
        }
        stdout.WriteLine($"token audit{(r.Project is null ? "" : $" — {r.Project}")}: {r.NoteCount} note(s)");
        stdout.WriteLine($"  total ~{r.TotalEstimatedTokens} tokens (managed ~{r.ManagedEstimatedTokens}, " +
                         $"active ~{r.ActiveEstimatedTokens}, archived ~{r.ArchivedEstimatedTokens})");
        if (r.CapsuleEstimatedTokens > 0)
            stdout.WriteLine($"  capsule ~{r.CapsuleEstimatedTokens} tokens; route read-first ~{r.RouteReadFirstEstimatedTokens} tokens");
        stdout.WriteLine($"  large notes: {r.LargeNoteCount} ({r.LargeWithSummaryCount} summarized); " +
                         $"estimated waste ~{r.EstimatedTokenWaste} tokens");
        if (r.LargestNotes.Count > 0)
        {
            stdout.WriteLine("  largest:");
            foreach (var n in r.LargestNotes.Take(5))
                stdout.WriteLine($"    - {n.Path} ~{n.EstimatedTokens} tokens");
        }
        foreach (var n in r.NotesWithoutSummaries)
            stdout.WriteLine($"  no summary: {n.Path} ~{n.EstimatedTokens} tokens");
        foreach (var w in r.TokenWasteWarnings) stdout.WriteLine($"  warning: {w}");
        stdout.WriteLine("  recommended:");
        foreach (var f in r.RecommendedFixes) stdout.WriteLine($"    - {f}");
        return 0;
    }

    private static int CmdSummarize(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var note = args.Opt("note");
        var project = args.Opt("project");
        if (note is not null && project is not null)
            throw new MindVaultException("Pass --note or --project, not both.");
        var apply = args.Has("apply");
        var report = note is not null
            ? ctx.Summaries.ForNote(note, apply)
            : ctx.Summaries.ForProject(project, apply);
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, dryRun = report.DryRun, notesConsidered = report.NotesConsidered,
                proposals = report.Proposals, applied = report.Applied, warnings = report.Warnings,
            }));
            return 0;
        }
        stdout.WriteLine($"summarize{(report.DryRun ? " (dry-run)" : " --apply")}: " +
                         $"{report.NotesConsidered} candidate(s), {report.Applied} written");
        foreach (var p in report.Proposals)
        {
            stdout.WriteLine($"  {p.Path}{(p.HadBlock ? " (refresh)" : " (new)")}" +
                             $"{(p.NeedsReview ? " [needs review]" : "")}");
            stdout.WriteLine($"    summary: {p.Summary}");
        }
        foreach (var w in report.Warnings) stdout.WriteLine($"  warning: {w}");
        if (report.DryRun && report.Proposals.Count > 0)
            stdout.WriteLine("Nothing was written. Re-run with --apply to add these summary blocks.");
        return 0;
    }

    private static int CmdOrganisationScore(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var r = ctx.OrgScore.Run(args.Opt("project"));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new { ok = true, score = r }));
            return 0;
        }
        stdout.WriteLine($"Organisation Score{(r.Project is null ? "" : $" — {r.Project}")}: {r.OverallScore}/100");
        foreach (var c in r.Categories)
            stdout.WriteLine($"  {c.Name}: {c.Score} — {c.Evidence}");
        if (r.Strengths.Count > 0) stdout.WriteLine($"  strengths: {string.Join(", ", r.Strengths)}");
        if (r.Weaknesses.Count > 0)
        {
            stdout.WriteLine("  weaknesses:");
            foreach (var w in r.Weaknesses) stdout.WriteLine($"    - {w}");
        }
        stdout.WriteLine($"  estimated token waste ~{r.EstimatedTokenWaste}; " +
                         $"savings if fixed ~{r.EstimatedTokenSavingsIfFixed}");
        stdout.WriteLine("  recommended:");
        foreach (var f in r.RecommendedFixes) stdout.WriteLine($"    - {f}");
        return 0;
    }

    private static int CmdGraph(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "build":
            {
                var r = ctx.Graph.Build(args.Opt("project"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, notes = r.NoteCount, edges = r.EdgeCount,
                        edgesByType = r.EdgesByType, sidecar = r.SidecarPath,
                    }));
                    return 0;
                }
                stdout.WriteLine($"graph build: {r.EdgeCount} typed edge(s) across {r.NoteCount} note(s) -> {r.SidecarPath}");
                foreach (var (type, count) in r.EdgesByType)
                    stdout.WriteLine($"  {type}: {count}");
                return 0;
            }
            case "relationships":
            {
                var edges = ctx.Graph.RelationshipsFor(args.Require("note"), args.IntOpt("limit", 50));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, count = edges.Count, relationships = edges }));
                    return 0;
                }
                if (edges.Count == 0) { stdout.WriteLine("No typed relationships."); return 0; }
                foreach (var e in edges)
                    stdout.WriteLine($"  {e.FromPath} --{e.Type}--> {e.ToPath} — {e.Reason} [{e.Confidence:0.0#}, {e.Source}]");
                return 0;
            }
            case "explain":
            {
                var r = ctx.Graph.Explain(args.Require("from"), args.Require("to"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new { ok = true, found = r.Found, path = r.Path, explanation = r.Explanation }));
                    return 0;
                }
                stdout.WriteLine(r.Explanation);
                foreach (var e in r.Path)
                    stdout.WriteLine($"  {e.FromPath} --{e.Type}--> {e.ToPath} [{e.Confidence:0.0#}]");
                return 0;
            }
            default:
                throw new MindVaultException(
                    "Usage: graph build [--project \"<p>\"] | graph relationships --note \"<ref>\" | graph explain --from \"<ref>\" --to \"<ref>\"");
        }
    }

    private static int CmdLowValue(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var r = ctx.LowValue.Find(args.Opt("project"));
        if (json)
        {
            stdout.WriteLine(Json.Serialize(new
            {
                ok = true, project = r.Project, scanned = r.Scanned,
                count = r.Notes.Count, notes = r.Notes, truncated = r.Truncated,
            }));
            return 0;
        }
        stdout.WriteLine($"low-value{(r.Project is null ? "" : $" — {r.Project}")}: " +
                         $"{r.Notes.Count} of {r.Scanned} note(s) flagged");
        foreach (var n in r.Notes)
            stdout.WriteLine($"  - {n.Path} — {string.Join("; ", n.Reasons)}");
        if (r.Truncated) stdout.WriteLine($"  (truncated at {LowValueService.MaxResults})");
        return 0;
    }

    private static int CmdRecordFeedback(VaultContext ctx, CliArgs args, TextWriter stdout, bool json, string? fixedSignal)
    {
        var entry = ctx.Feedback.Record(args.Require("note"), fixedSignal ?? args.Require("signal"),
            args.Opt("reason"));
        if (json) stdout.WriteLine(Json.Serialize(new { ok = true, path = entry.Path, signal = entry.Signal }));
        else stdout.WriteLine($"Recorded '{entry.Signal}' for {entry.Path}");
        return 0;
    }

    private static int CmdMistake(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "add":
            {
                var result = ctx.Writer.CreateMistake(args.Require("title"), args.Opt("project"),
                    args.Opt("lesson"), args.Opt("prevention"),
                    args.Has("allow-duplicate"), args.Has("allow-risky-content"));
                if (json)
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, path = result.Note.Path, title = result.Note.Title, warnings = result.Warnings,
                    }));
                else
                {
                    stdout.WriteLine($"Recorded mistake: {result.Note.Path}");
                    foreach (var w in result.Warnings) stdout.WriteLine($"  note: {w}");
                }
                return 0;
            }
            case "list":
            {
                var rows = BrainQueries.Mistakes(ctx, args.Opt("project"), includeResolved: args.Has("all"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, count = rows.Count,
                        mistakes = rows.Select(n => new { n.Title, n.Path, n.Status, n.Project, n.Updated }),
                    }));
                }
                else if (rows.Count == 0)
                {
                    stdout.WriteLine("No active mistakes recorded — either a clean run or an unwritten ledger.");
                }
                else
                {
                    foreach (var n in rows)
                        stdout.WriteLine($"  - {n.Title} [{n.Status}]{(n.Project is null ? "" : $" ({n.Project})")} — {n.Path}");
                }
                return 0;
            }
            case "resolve":
            {
                var noteRef = args.Positionals.Count > 2 ? args.Positionals[2] : args.Require("note");
                var result = ctx.Writer.ResolveMistake(noteRef);
                if (json) stdout.WriteLine(Json.Serialize(new { ok = true, path = result.Path, snapshot = result.SnapshotPath }));
                else stdout.WriteLine($"Resolved (status: done): {result.Path}");
                return 0;
            }
            default:
                throw new MindVaultException(
                    "Usage: mistake add --title \"<t>\" [--project p] [--lesson l] [--prevention p] | " +
                    "mistake list [--project p] [--all] | mistake resolve \"<note-ref>\"");
        }
    }

    private static int CmdInbox(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var sub = args.Positionals.Count > 1 ? args.Positionals[1].ToLowerInvariant() : "";
        switch (sub)
        {
            case "add":
            {
                var type = args.Opt("type");
                if (type is not null && !string.Equals(type, "thought", StringComparison.OrdinalIgnoreCase))
                    throw new MindVaultException("The inbox holds thoughts. For durable notes use 'create' or 'promote'.");
                var result = ctx.Writer.CaptureThought(args.Require("title"), args.Opt("content"),
                    agentInbox: false, args.Opt("project"), args.Has("allow-risky-content"));
                if (json)
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, path = result.Note.Path, title = result.Note.Title, warnings = result.Warnings,
                    }));
                else
                {
                    stdout.WriteLine($"Captured thought: {result.Note.Path}");
                    foreach (var w in result.Warnings) stdout.WriteLine($"  note: {w}");
                }
                return 0;
            }
            case "list":
            {
                var rows = BrainQueries.Inbox(ctx, args.Opt("project"));
                if (json)
                {
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, count = rows.Count,
                        drafts = rows.Select(n => new { n.Title, n.Path, n.Project, n.Updated }),
                    }));
                }
                else if (rows.Count == 0)
                {
                    stdout.WriteLine("The inbox is empty.");
                }
                else
                {
                    foreach (var n in rows)
                        stdout.WriteLine($"  - {n.Title}{(n.Project is null ? "" : $" ({n.Project})")} — {n.Path}");
                    stdout.WriteLine("Promote with: inbox promote \"<ref>\" --to <type>  |  reject with: inbox reject \"<ref>\"");
                }
                return 0;
            }
            case "promote":
            {
                var noteRef = args.Positionals.Count > 2 ? args.Positionals[2] : args.Require("note");
                var result = ctx.Writer.PromoteNote(noteRef, args.Require("to"), args.Opt("project"),
                    args.Has("allow-duplicate"));
                if (json)
                    stdout.WriteLine(Json.Serialize(new
                    {
                        ok = true, from = result.FromPath, to = result.ToPath, type = result.Type,
                        status = result.Status, warnings = result.Warnings, suggestions = result.Suggestions,
                    }));
                else
                {
                    stdout.WriteLine($"Promoted {result.FromPath} -> {result.Type} ({result.Status}) at {result.ToPath}");
                    foreach (var s in result.Suggestions) stdout.WriteLine($"  suggest: {s}");
                }
                return 0;
            }
            case "reject":
            {
                var noteRef = args.Positionals.Count > 2 ? args.Positionals[2] : args.Require("note");
                var result = ctx.Writer.Archive(noteRef);
                if (json) stdout.WriteLine(Json.Serialize(new { ok = true, from = result.FromPath, to = result.ToPath }));
                else stdout.WriteLine($"Rejected (archived): {result.FromPath} -> {result.ToPath}");
                return 0;
            }
            default:
                throw new MindVaultException(
                    "Usage: inbox add --title \"<t>\" [--content c] [--project p] | inbox list [--project p] | " +
                    "inbox promote \"<ref>\" --to <type> | inbox reject \"<ref>\"");
        }
    }

    private static void PrintItems(TextWriter stdout, string label, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        stdout.WriteLine($"{label}:");
        foreach (var item in items) stdout.WriteLine($"  - {item}");
    }

    private static void PrintContextItems(TextWriter stdout, string label, IReadOnlyList<ContextItem> items)
    {
        if (items.Count == 0) return;
        stdout.WriteLine($"{label}:");
        foreach (var item in items)
            stdout.WriteLine($"  - {item.Title}{(item.Status is null ? "" : $" [{item.Status}]")} ({item.Path})");
    }

    private const string UsageText = """
        mindvault — portable local-first Obsidian vault brain for AI agents

        usage: mindvault <command> [options] [--vault <path>] [--json] [--verbose] [--quiet]

        commands:
          status                                   show config, vault and index state
          version | --version                      print app + index schema version
          init                                     create required vault folders + templates
          scan [--full]                            index changed notes (--full = rebuild everything)
          rebuild-index                            clear and rebuild the SQLite index
          index status|verify|rebuild              index health: report, drift check, rebuild
          validate                                 report vault problems (critical/warning/info)
          doctor                                   health verdict + system report (config, writability, Docker, MCP env)
          detect-project ["<name>"]                map a repo/folder name to a vault project (aliases, repoNames)
          related "<note-ref>" [--limit n]         links, backlinks and related notes with reasons
          search "<query>" [--type --project --tag --status --limit
                            --updated-after --updated-before --include-archived --explain]
          read "<note-ref>"                        print a note (path, title, slug or [[link]])
          list [--type --project --status --tag --limit]
          context "<project>" [--brief|--deep]     rich project context (goal, tasks, warnings)
          context-pack "<project>" [--task "<t>"] [--output markdown|json]
                                                   compact agent briefing built from vault notes
          check-draft --type t [--project p] --title "<t>"
                                                   quality-check a note idea before creating it
          check-note "<note-ref>"                  quality-check an existing note
          decision list [--project p] [--all]      active decisions (+relations)
          decision graph [--project p]             decision supersede/related graph
          decision supersede --old "<ref>" --new "<ref>"
          session start --project p [--task t]     briefing pack + ensures the session log note
          session checkpoint|log --project p --summary s [--dry-run]
                                                   mid-session breadcrumb (use sparingly)
          session handoff|end --project p --summary s [--tests t] [--followups f] [--dry-run]
                                                   concise handoff entry for the next session
          session recent --project p [--limit n]   latest handoffs and checkpoints
          capsule --project p [--mode coding|debugging|review|planning|handoff|release|architecture]
                  [--format markdown|json] [--max-chars n]
                                                   budgeted, source-backed context capsule
          work-context --project p (--current-file f | --query q | --note "<ref>") [--limit n]
                                                   memory related to what you are working on, with reasons
          recall [--project p] [--since "7 days"|yyyy-MM-dd] [--on-this-day] [--format markdown|json]
                                                   what changed in a time window, grouped
          ops                                      one-call brain state + recommended fixes
          pin --note "<ref>"                       boost a note in capsules/work-context ranking
          hide --note "<ref>"                      exclude a note from capsules/work-context/suggestions
          feedback --note "<ref>" --signal useful|noisy|outdated|wrong [--reason r]
          mistake add --title "<t>" [--project p] [--lesson l] [--prevention p]
          mistake list [--project p] [--all]       the mistake ledger (active lessons)
          mistake resolve "<note-ref>"             mark a lesson done (stays in the ledger)
          inbox add --title "<t>" [--content c] [--project p]
          inbox list [--project p]                 unpromoted thought drafts
          inbox promote "<ref>" --to <type>        thought -> durable memory
          inbox reject "<ref>"                     archive a draft that did not survive
          create project "<name>" [--allow-duplicate]
          create decision --project "<p>" --title "<t>" [--allow-duplicate]
          create task --project "<p>" --title "<t>" [--allow-duplicate]
                                                   creates REFUSE likely duplicates unless --allow-duplicate
          create thought "<title>" [--content "<text>"]
                                                   capture a raw thought into 00_Inbox (promote it later)
          promote "<note-ref>" --to <decision|memory|task|risk|mistake> [--project "<p>"] [--allow-duplicate]
                                                   thought -> durable memory: frontmatter, placement, project link
          organize [--project "<p>"] [--apply]     propose placement moves with reasons (dry-run by default)
          map create|rebuild --project "<p>"       generated map block on the project hub (human text preserved)
          map list                                 list project hubs and map-block state (plus legacy files)
          links suggest (--note "<ref>" | --project "<p>") [--limit n]
                                                   reason-tagged link suggestions (never auto-applied)
          links apply --note "<from>" --to "<target>"
          links broken                             wiki links whose target does not exist
          links orphans                            managed notes with no links in either direction
          frontmatter audit [--project "<p>"]      frontmatter quality findings with proposed fixes
          aliases audit                            alias/repoName hygiene across project notes
          compile [--project "<p>"] [--apply]      build navigation artefacts: maps, summaries, graph,
                                                   health + score (dry-run by default)
          route --project "<p>" [--goal g | --current-file f | --query q]
                [--format markdown|json] [--max-notes n] [--max-chars n] [--max-tokens n]
                                                   agent route card: read-first/do-not-read with reasons + tokens
          read-plan --project "<p>" [--goal g | --current-file f] [--max-reads n]
                                                   strict ordered read plan with stop conditions (JSON)
          token-audit [--project "<p>"]            where the tokens go: totals, largest, unsummarized, waste
          summarize (--project "<p>" | --note "<ref>") [--apply]
                                                   generated extractive summary blocks (dry-run by default)
          organisation-score [--project "<p>"]     11 explainable categories + weaknesses + token waste
          graph build [--project "<p>"]            typed relationship graph -> .mindvault/link-graph.jsonl
          graph relationships --note "<ref>"       typed edges touching a note, with reasons
          graph explain --from "<ref>" --to "<ref>" why two notes matter together (up to 2 hops)
          low-value [--project "<p>"]              notes agents should not read by default, with reasons
          append --note "<ref>" --section "<heading>" (--content "<text>" | --content-file <path>)
                 [--create-section] [--dry-run]
          update-frontmatter --note "<ref>" --key "<key>" --value "<value>" [--dry-run]
          link --from "<ref>" --to "<ref>"
          archive "<note-ref>" [--dry-run]         snapshot, mark archived and move to 99_Archive
          restore "<note-ref>" [--snapshot <path>] restore a note from its newest (or given) snapshot
          backup                                   zip all vault Markdown into .mindvault/backups
          prune [--days n]                         delete snapshots older than the retention window
          project-context "<project>" [--limit n]  compact project bundle (JSON)

        dev/test:
          generate-fixture-vault --path <new-dir> [--projects n] [--notes-per-project n] [--seed n]
                                                   synthetic vault for benchmarks/evals (refuses non-empty dirs)

        vault path resolution: --vault > MINDVAULT_VAULT_PATH > config/mindvault.config.local.json
        errors: --json failures carry a stable "code" (see docs/ERROR_CODES.md); exit 2 = known error, 3 = ambiguous ref
        """;
}
