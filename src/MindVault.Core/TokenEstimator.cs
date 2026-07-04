namespace MindVault.Core;

/// <summary>
/// Deterministic token estimation: ceil(chars / 4). Not a model tokenizer — a stable,
/// explainable approximation for budgets, audits and route cards. File-size estimates
/// treat bytes as chars, which slightly over-counts multi-byte text; the bias is safe
/// because budgets then err toward reading less, never more.
/// </summary>
public static class TokenEstimator
{
    public static int Estimate(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : (text.Length + 3) / 4;

    public static int EstimateBytes(long bytes) =>
        bytes <= 0 ? 0 : (int)((bytes + 3) / 4);
}

/// <summary>Reusable context budget applied to route cards, read plans and audited outputs.</summary>
public sealed record ContextBudget(
    int? MaxNotes = null,
    int? MaxChars = null,
    int? MaxEstimatedTokens = null,
    int MaxSnippetChars = 240,
    bool IncludeArchived = false)
{
    public static readonly ContextBudget Default = new();

    /// <summary>Char ceiling implied by whichever of maxChars / maxEstimatedTokens is tighter.</summary>
    public int? EffectiveMaxChars =>
        (MaxChars, MaxEstimatedTokens) switch
        {
            (int c, int t) => Math.Min(c, t * 4),
            (int c, null) => c,
            (null, int t) => t * 4,
            _ => null,
        };
}
