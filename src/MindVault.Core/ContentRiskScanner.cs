using System.Text.RegularExpressions;

namespace MindVault.Core;

public sealed record RiskFinding(string Code, string Severity, string Evidence);

/// <summary>
/// Lightweight content gate for durable writes: blocks high-confidence secrets (private
/// key blocks, cloud/API tokens, bearer tokens) unless explicitly overridden, and warns on
/// prompt-injection-style language. Evidence NEVER contains the matched text — only the
/// pattern name, length and offset — so secret values cannot leak into results, errors or
/// logs. Deterministic regex only; no network, no models.
/// </summary>
public static partial class ContentRiskScanner
{
    public const string Block = "block";
    public const string Warn = "warn";

    public static List<RiskFinding> Scan(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var findings = new List<RiskFinding>();

        void Add(Regex pattern, string code, string severity)
        {
            foreach (Match m in pattern.Matches(text))
                findings.Add(new RiskFinding(code, severity,
                    $"{code} ({m.Length} chars at offset {m.Index}; value redacted)"));
        }

        // High-confidence secrets — these block by default.
        Add(PrivateKeyPattern(), "private-key-block", Block);
        Add(AwsAccessKeyPattern(), "aws-access-key", Block);
        Add(GitHubTokenPattern(), "github-token", Block);
        Add(SkApiKeyPattern(), "api-secret-key", Block);
        Add(BearerTokenPattern(), "bearer-token", Block);

        // Likely credentials — warn (too many legitimate look-alikes to block).
        Add(CredentialAssignmentPattern(), "credential-assignment", Warn);

        // Prompt-injection-style language — warn.
        Add(IgnoreInstructionsPattern(), "prompt-injection", Warn);
        Add(SystemPromptLeakPattern(), "system-prompt-probe", Warn);
        Add(ExfiltrationPattern(), "exfiltration-language", Warn);
        Add(SecretSolicitationPattern(), "secret-solicitation", Warn);

        return findings;
    }

    /// <summary>
    /// Scans and enforces: blockers throw <see cref="ErrorCodes.RiskyContent"/> unless
    /// overridden; everything found comes back as human-readable warnings.
    /// </summary>
    public static List<string> Gate(string? text, bool allowRiskyContent)
    {
        var findings = Scan(text);
        if (findings.Count == 0) return [];
        var blockers = findings.Where(f => f.Severity == Block).ToList();
        if (blockers.Count > 0 && !allowRiskyContent)
        {
            throw new MindVaultException(
                $"Content blocked — it appears to contain secrets: " +
                $"{string.Join("; ", blockers.Select(b => b.Evidence).Distinct())}. " +
                "Secrets do not belong in vault notes. Remove them, or pass " +
                "allow-risky-content to store it anyway (deliberately).",
                ErrorCodes.RiskyContent);
        }
        return findings.Select(f => $"content risk ({f.Severity}): {f.Evidence}").Distinct().ToList();
    }

    [GeneratedRegex(@"-----BEGIN [A-Z0-9 ]*PRIVATE KEY( BLOCK)?-----")]
    private static partial Regex PrivateKeyPattern();

    [GeneratedRegex(@"\bAKIA[0-9A-Z]{16}\b")]
    private static partial Regex AwsAccessKeyPattern();

    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9]{30,}|github_pat_[A-Za-z0-9_]{30,})\b")]
    private static partial Regex GitHubTokenPattern();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{20,}\b")]
    private static partial Regex SkApiKeyPattern();

    [GeneratedRegex(@"\bbearer\s+[A-Za-z0-9._~+/=-]{25,}", RegexOptions.IgnoreCase)]
    private static partial Regex BearerTokenPattern();

    [GeneratedRegex(@"\b(?:api[_-]?key|apikey|secret|passwd|password|auth[_-]?token)\s*[:=]\s*\S{10,}",
        RegexOptions.IgnoreCase)]
    private static partial Regex CredentialAssignmentPattern();

    [GeneratedRegex(@"\b(?:ignore|disregard|forget)\s+(?:all\s+|any\s+)?(?:previous|prior|above|earlier)\s+instructions\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex IgnoreInstructionsPattern();

    [GeneratedRegex(@"\b(?:reveal|print|show|leak)\b.{0,30}\bsystem prompt\b", RegexOptions.IgnoreCase)]
    private static partial Regex SystemPromptLeakPattern();

    [GeneratedRegex(@"\bexfiltrat\w*\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExfiltrationPattern();

    [GeneratedRegex(@"\bsend\b.{0,30}\b(?:secrets?|credentials?|passwords?|api keys?)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SecretSolicitationPattern();
}
