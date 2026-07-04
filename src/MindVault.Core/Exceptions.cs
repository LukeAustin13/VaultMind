namespace MindVault.Core;

/// <summary>
/// Stable machine-readable error codes. These appear in CLI JSON output (`code`) and MCP
/// error payloads so agents and scripts can branch on them. Documented in docs/ERROR_CODES.md;
/// treat them as a public contract — never rename, only add.
/// </summary>
public static class ErrorCodes
{
    public const string Unexpected = "UNEXPECTED_ERROR";
    public const string General = "MINDVAULT_ERROR";
    public const string ConfigMissing = "CONFIG_MISSING";
    public const string ConfigInvalid = "CONFIG_INVALID";
    public const string VaultNotFound = "VAULT_NOT_FOUND";
    public const string VaultNotWritable = "VAULT_NOT_WRITABLE";
    public const string NoteNotFound = "NOTE_NOT_FOUND";
    public const string NoteRefAmbiguous = "NOTE_REF_AMBIGUOUS";
    public const string InvalidFrontmatter = "INVALID_FRONTMATTER";
    public const string PathTraversalRejected = "PATH_TRAVERSAL_REJECTED";
    public const string WriteLocked = "WRITE_LOCKED";
    public const string SnapshotFailed = "SNAPSHOT_FAILED";
    public const string IndexStale = "INDEX_STALE";
    public const string McpAuthRequired = "MCP_AUTH_REQUIRED";
    public const string McpAuthFailed = "MCP_AUTH_FAILED";
}

public class MindVaultException(string message, string code = ErrorCodes.General) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class MindVaultConfigException(string message, string code = ErrorCodes.ConfigMissing)
    : MindVaultException(message, code);

public sealed class UnsafePathException(string message)
    : MindVaultException(message, ErrorCodes.PathTraversalRejected);

public sealed class WriteLockedException(string message)
    : MindVaultException(message, ErrorCodes.WriteLocked);

public sealed class NoteNotFoundException(string noteRef)
    : MindVaultException($"No note found for reference '{noteRef}'. Tried exact path, title, filename and slug forms.",
        ErrorCodes.NoteNotFound)
{
    public string NoteRef { get; } = noteRef;
}

public sealed class AmbiguousNoteRefException(string noteRef, IReadOnlyList<string> candidates)
    : MindVaultException(
        $"Note reference '{noteRef}' is ambiguous ({candidates.Count} matches). " +
        $"Use an exact path instead. Candidates: {string.Join(" | ", candidates)}",
        ErrorCodes.NoteRefAmbiguous)
{
    public string NoteRef { get; } = noteRef;
    public IReadOnlyList<string> Candidates { get; } = candidates;
}
