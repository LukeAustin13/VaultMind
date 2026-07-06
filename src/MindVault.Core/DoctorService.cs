namespace MindVault.Core;

/// <summary>MINDVAULT_MCP_* environment variable names (defined in Core so diagnostics can report on them).</summary>
public static class McpEnv
{
    public const string Transport = "MINDVAULT_MCP_TRANSPORT";
    public const string Host = "MINDVAULT_MCP_HOST";
    public const string Port = "MINDVAULT_MCP_PORT";
    public const string AuthToken = "MINDVAULT_MCP_AUTH_TOKEN";
    public const string AuthTokenFile = "MINDVAULT_MCP_AUTH_TOKEN_FILE";
    public const string AllowAnonymous = "MINDVAULT_MCP_ALLOW_ANONYMOUS";
    public const string ToolProfile = "MINDVAULT_TOOL_PROFILE";
}

/// <summary>MCP-related environment as booleans/plain values only — never the token itself.</summary>
public sealed record McpEnvReport(
    string? Transport, string? Host, string? Port,
    bool AuthTokenSet, bool AuthTokenFileSet, bool AllowAnonymous);

public sealed record DoctorReport(
    string AppVersion,
    string VaultPath, string ConfigSource, string? ConfigFilePath, bool LocalConfigFound,
    bool VaultExists, bool VaultWritable,
    string IndexPath, bool IndexExists, int IndexSchemaVersion, int ExpectedSchemaVersion,
    string SnapshotPath, bool SnapshotWritable,
    DateTime? LastScanUtc, int NoteCount, int BrokenLinkCount, int DuplicateTitleCount,
    bool RunningInContainer, bool ContainerVaultMounted, string User,
    McpEnvReport McpEnvironment,
    IReadOnlyList<string> Warnings,
    string WatcherStatus,
    string Verdict = "good",
    IReadOnlyList<string>? VerdictReasons = null,
    int ValidationCriticalCount = 0);

public sealed class DoctorService(VaultContext ctx)
{
    private static readonly string[] PlaceholderMarkers =
        ["path/to", "path\\to", "yourobsidianvault", "your vault", "yourvault", "changeme", "<", ">"];

    /// <summary>True when the path still looks like documentation, not a real vault.</summary>
    public static bool LooksLikePlaceholderPath(string path)
    {
        var lower = path.ToLowerInvariant();
        return PlaceholderMarkers.Any(lower.Contains);
    }

    public DoctorReport Run(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var report = ctx.Validator.Validate(); // includes an incremental scan
        var state = ctx.State.Load();
        var warnings = new List<string>();

        var vaultWritable = ProbeWritable(ctx.VaultRoot);
        var snapshotWritable = ProbeWritable(ctx.SnapshotDir);
        if (!vaultWritable) warnings.Add($"Vault folder is not writable: {ctx.VaultRoot}");
        if (!snapshotWritable) warnings.Add($"Snapshot folder is not writable: {ctx.SnapshotDir}");

        if (LooksLikePlaceholderPath(ctx.VaultRoot))
            warnings.Add($"Vault path looks like a placeholder, not a real vault: {ctx.VaultRoot}");

        var localConfigFound = ctx.ConfigFilePath is not null;
        if (!localConfigFound && FindEditedExampleConfig() is { } example)
            warnings.Add($"{example} has been edited but is never loaded — copy it to " +
                         $"config/{ConfigLoader.LocalConfigFileName} for it to take effect.");

        var inContainer = RunningInContainer(getEnv);
        var containerVaultMounted = inContainer && Directory.Exists("/vault");
        if (inContainer && !containerVaultMounted)
            warnings.Add("Running inside a container but /vault is not mounted — check the compose volumes.");

        var mcpEnv = new McpEnvReport(
            getEnv(McpEnv.Transport), getEnv(McpEnv.Host), getEnv(McpEnv.Port),
            !string.IsNullOrWhiteSpace(getEnv(McpEnv.AuthToken)),
            !string.IsNullOrWhiteSpace(getEnv(McpEnv.AuthTokenFile)),
            string.Equals(getEnv(McpEnv.AllowAnonymous), "true", StringComparison.OrdinalIgnoreCase));
        if (mcpEnv.AllowAnonymous)
            warnings.Add("MCP anonymous access is enabled — only acceptable for local development on a trusted machine.");

        var (verdict, reasons) = ComputeVerdict(
            vaultWritable, snapshotWritable, ctx.IndexExists,
            ctx.IndexExists ? ctx.Db.UserVersion : 0,
            LooksLikePlaceholderPath(ctx.VaultRoot), report.CriticalCount, warnings);

        return new DoctorReport(
            MindVaultVersion.Current,
            ctx.VaultRoot,
            ctx.ConfigSource,
            ctx.ConfigFilePath,
            localConfigFound,
            Directory.Exists(ctx.VaultRoot),
            vaultWritable,
            ctx.IndexFile,
            ctx.IndexExists,
            ctx.IndexExists ? ctx.Db.UserVersion : 0,
            IndexDatabase.CurrentSchemaVersion,
            ctx.SnapshotDir,
            snapshotWritable,
            state?.LastScanUtc,
            ctx.Db.CountNotes(),
            report.Issues.Count(i => i.Code == "broken-link"),
            report.Issues.Count(i => i.Code == "duplicate-title"),
            inContainer,
            containerVaultMounted,
            Environment.UserName,
            mcpEnv,
            warnings,
            ctx.Config.EnableWatcher
                ? "requested in config but not implemented (run 'scan' to refresh)"
                : "disabled",
            verdict,
            reasons,
            report.CriticalCount);
    }

    /// <summary>
    /// One verdict a human or agent can branch on. critical = mutations are unsafe or the
    /// setup is wrong (fix before using MindVault); warning = usable but the brain has
    /// problems worth fixing; good = everything checked out.
    /// </summary>
    private static (string Verdict, List<string> Reasons) ComputeVerdict(
        bool vaultWritable, bool snapshotWritable, bool indexExists, int indexSchemaVersion,
        bool placeholderPath, int validationCriticals, IReadOnlyList<string> warnings)
    {
        var critical = new List<string>();
        if (!vaultWritable) critical.Add("vault folder is not writable — all writes will fail");
        if (!snapshotWritable) critical.Add("snapshot folder is not writable — mutations would run without their safety net");
        if (placeholderPath) critical.Add("vault path looks like a placeholder, not a real vault");
        if (indexExists && indexSchemaVersion != IndexDatabase.CurrentSchemaVersion)
            critical.Add($"index schema is v{indexSchemaVersion} but this build expects v{IndexDatabase.CurrentSchemaVersion} — rebuild the index");
        if (critical.Count > 0) return ("critical", critical);

        var warn = new List<string>();
        if (!indexExists) warn.Add("index not built yet — run 'scan'");
        if (validationCriticals > 0)
            warn.Add($"validation found {validationCriticals} critical issue(s) — run 'validate' for the list");
        warn.AddRange(warnings);
        return warn.Count > 0 ? ("warning", warn) : ("good", []);
    }

    public static bool RunningInContainer(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        return string.Equals(getEnv("DOTNET_RUNNING_IN_CONTAINER"), "true", StringComparison.OrdinalIgnoreCase)
               || File.Exists("/.dockerenv");
    }

    private static bool ProbeWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
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

    /// <summary>An example config someone customised without copying it to the local name.</summary>
    private string? FindEditedExampleConfig()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        for (var depth = 0; dir is not null && depth < 10; depth++, dir = dir.Parent)
        {
            var example = Path.Combine(dir.FullName, "config", "mindvault.config.example.json");
            if (!File.Exists(example)) continue;
            if (File.Exists(Path.Combine(dir.FullName, "config", ConfigLoader.LocalConfigFileName)))
                return null;
            try
            {
                var text = File.ReadAllText(example);
                return text.Contains("vaultPath", StringComparison.OrdinalIgnoreCase) &&
                       !text.Contains("Path\\\\To\\\\Your", StringComparison.OrdinalIgnoreCase)
                    ? example
                    : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        return null;
    }
}
