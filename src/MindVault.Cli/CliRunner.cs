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
            throw new MindVaultException("Usage: create project \"<name>\" | create decision --project \"<p>\" --title \"<t>\" | create task --project \"<p>\" --title \"<t>\"");
        var kind = args.Positionals[1].ToLowerInvariant();
        var allowDuplicate = args.Has("allow-duplicate");
        var result = kind switch
        {
            "project" => ctx.Writer.CreateProject(
                args.Positionals.Count > 2 ? args.Positionals[2] : args.Require("name"), allowDuplicate),
            "decision" => ctx.Writer.CreateDecision(args.Require("project"), args.Require("title"), allowDuplicate),
            "task" => ctx.Writer.CreateTask(args.Require("project"), args.Require("title"), allowDuplicate),
            _ => throw new MindVaultException($"Unknown create target: '{kind}'. Use project, decision or task."),
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
            args.Has("dry-run"));
        if (json) stdout.WriteLine(Json.Serialize(new
        {
            ok = true, dryRun = args.Has("dry-run"), path = result.Path,
            snapshot = result.SnapshotPath, message = result.Message,
        }));
        else stdout.WriteLine(result.SnapshotPath is null ? result.Message : $"{result.Message} (snapshot: {result.SnapshotPath})");
        return 0;
    }

    private static int CmdUpdateFrontmatter(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var result = ctx.Writer.UpdateFrontmatter(args.Require("note"), args.Require("key"), args.Require("value"),
            args.Has("dry-run"));
        if (json) stdout.WriteLine(Json.Serialize(new
        {
            ok = true, dryRun = args.Has("dry-run"), path = result.Path,
            snapshot = result.SnapshotPath, message = result.Message,
        }));
        else stdout.WriteLine(result.SnapshotPath is null ? result.Message : $"{result.Message} (snapshot: {result.SnapshotPath})");
        return 0;
    }

    private static int CmdLink(VaultContext ctx, CliArgs args, TextWriter stdout, bool json)
    {
        var result = ctx.Writer.LinkNotes(args.Require("from"), args.Require("to"));
        if (json) stdout.WriteLine(Json.Serialize(new { ok = true, path = result.Path, changed = result.Changed }));
        else stdout.WriteLine(result.Message);
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
            {
                var result = ctx.Sessions.Log(args.Require("project"), args.Require("summary"));
                if (json) stdout.WriteLine(Json.Serialize(new { ok = true, path = result.Path, snapshot = result.SnapshotPath }));
                else infoOut.WriteLine($"{result.Message}");
                return 0;
            }
            case "end":
            {
                var result = ctx.Sessions.End(args.Require("project"), args.Require("summary"),
                    args.Opt("tests"), args.Opt("followups"));
                if (json) stdout.WriteLine(Json.Serialize(new { ok = true, path = result.Path, snapshot = result.SnapshotPath }));
                else infoOut.WriteLine($"Handoff written to {result.Path}");
                return 0;
            }
            default:
                throw new MindVaultException(
                    "Usage: session start --project p [--task t] | session log --project p --summary s | " +
                    "session end --project p --summary s [--tests t] [--followups f]");
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
          session log --project p --summary s      mid-session breadcrumb (use sparingly)
          session end --project p --summary s [--tests t] [--followups f]
                                                   concise handoff entry for the next session
          create project "<name>" [--allow-duplicate]
          create decision --project "<p>" --title "<t>" [--allow-duplicate]
          create task --project "<p>" --title "<t>" [--allow-duplicate]
                                                   creates REFUSE likely duplicates unless --allow-duplicate
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
