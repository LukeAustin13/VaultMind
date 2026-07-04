namespace MindVault.Core;

/// <summary>
/// Root object shared by the CLI and the MCP server: resolved configuration, vault paths and
/// the service graph. The SQLite index is opened lazily so read-only commands like `status`
/// never create index files as a side effect.
/// </summary>
public sealed class VaultContext : IDisposable
{
    public MindVaultConfig Config { get; }
    public string ConfigSource { get; }
    public string? ConfigFilePath { get; }
    public string VaultRoot { get; }
    public string MindVaultDir { get; }
    public string IndexFile { get; }
    public string SnapshotDir { get; }
    public string BackupDir { get; }

    public StateStore State { get; }
    public ScanService Scanner { get; }
    public SearchService Search { get; }
    public NoteResolver Resolver { get; }
    public SnapshotService Snapshots { get; }
    public WriteService Writer { get; }
    public ValidationService Validator { get; }
    public DoctorService Doctor { get; }
    public ProjectContextService Projects { get; }
    public BackupService Backup { get; }
    public ContextPackService Packs { get; }
    public DraftCheckService Drafts { get; }
    public DecisionService Decisions { get; }
    public SessionService Sessions { get; }
    public WriteLockService WriteLock { get; }
    public IndexVerifier IndexCheck { get; }
    public ProjectDetectService ProjectDetect { get; }
    public RelatedNotesService Related { get; }

    /// <summary>
    /// Single coordination lock serialising scans and writes so they never overlap.
    /// .NET Monitor is reentrant, so a write that triggers a scan on the same thread is safe.
    /// </summary>
    public object Sync { get; } = new();

    private readonly Lazy<IndexDatabase> _db;

    public VaultContext(LoadedConfig loaded)
    {
        var vaultPath = loaded.Config.VaultPath;
        if (string.IsNullOrWhiteSpace(vaultPath))
            throw new MindVaultConfigException(ConfigLoader.SetupHelp);
        if (!Directory.Exists(vaultPath))
            throw new MindVaultConfigException(
                $"Vault path does not exist: {vaultPath}\n" +
                "Create the folder (it should be your Obsidian vault) or fix the configured path.",
                ErrorCodes.VaultNotFound);

        Config = loaded.Config;
        ConfigSource = loaded.VaultPathSource;
        ConfigFilePath = loaded.ConfigFilePath;
        VaultRoot = Path.GetFullPath(vaultPath);
        MindVaultDir = Path.Combine(VaultRoot, ".mindvault");
        IndexFile = ResolveOperationalPath(Config.IndexPath);
        SnapshotDir = ResolveOperationalPath(Config.SnapshotPath);
        BackupDir = Path.Combine(MindVaultDir, "backups");

        _db = new Lazy<IndexDatabase>(() =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(IndexFile)!);
            return new IndexDatabase(IndexFile);
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        State = new StateStore(MindVaultDir);
        Scanner = new ScanService(this);
        Search = new SearchService(this);
        Resolver = new NoteResolver(this);
        Snapshots = new SnapshotService(this);
        Writer = new WriteService(this);
        Validator = new ValidationService(this);
        Doctor = new DoctorService(this);
        Projects = new ProjectContextService(this);
        Backup = new BackupService(this);
        Packs = new ContextPackService(this);
        Drafts = new DraftCheckService(this);
        Decisions = new DecisionService(this);
        Sessions = new SessionService(this);
        WriteLock = new WriteLockService(this);
        IndexCheck = new IndexVerifier(this);
        ProjectDetect = new ProjectDetectService(this);
        Related = new RelatedNotesService(this);
    }

    public static VaultContext Create(string? cliVaultPath = null, Func<string, string?>? getEnv = null,
        string? startDirectory = null) =>
        new(ConfigLoader.Load(cliVaultPath, getEnv, startDirectory));

    public bool IndexExists => File.Exists(IndexFile);

    public IndexDatabase Db => _db.Value;

    private string ResolveOperationalPath(string configured) =>
        Path.IsPathRooted(configured)
            ? Path.GetFullPath(configured)
            : Path.GetFullPath(Path.Combine(VaultRoot, configured));

    public void Dispose()
    {
        if (_db.IsValueCreated) _db.Value.Dispose();
    }
}
