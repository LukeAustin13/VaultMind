using System.Text.Json;

namespace MindVault.Core;

public sealed record FeedbackEntry(string Ts, string Stem, string Path, string Signal, string? Reason);

/// <summary>Aggregated feedback for one note: flags plus a deterministic ranking score.</summary>
public sealed record FeedbackState(bool Pinned, bool Hidden, int Score, int SignalCount)
{
    public static readonly FeedbackState None = new(false, false, 0, 0);
}

/// <summary>
/// Deterministic retrieval feedback, stored as an append-only sidecar
/// (.mindvault/feedback.jsonl) so vault Markdown stays untouched. Keyed by the note's
/// normalized stem, which survives organize/promote moves (both preserve file names).
/// Signals: pinned/hidden (flags), useful/noisy/outdated/wrong (score), clear (reset).
/// Consumed by capsules, work-context, related notes and link suggestions — never by the
/// FTS hot path.
/// </summary>
public sealed class FeedbackService(VaultContext ctx)
{
    public static readonly IReadOnlyList<string> Signals =
        ["pinned", "hidden", "useful", "noisy", "outdated", "wrong", "clear"];

    private string FilePath => Path.Combine(ctx.MindVaultDir, "feedback.jsonl");

    public FeedbackEntry Record(string noteRef, string signal, string? reason = null)
    {
        signal = (signal ?? "").Trim().ToLowerInvariant();
        if (!Signals.Contains(signal))
            throw new MindVaultException($"Unknown feedback signal '{signal}'. Use: {string.Join(", ", Signals)}.");
        var note = ctx.Resolver.Resolve(noteRef);
        var entry = new FeedbackEntry(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            SlugHelper.NormalizeWiki(note.Stem), note.Path, signal,
            string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
        lock (ctx.Sync)
        {
            Directory.CreateDirectory(ctx.MindVaultDir);
            File.AppendAllText(FilePath, Json.Serialize(entry) + "\n");
        }
        return entry;
    }

    /// <summary>All feedback states keyed by normalized stem. Malformed lines are skipped.</summary>
    public Dictionary<string, FeedbackState> LoadAll()
    {
        var result = new Dictionary<string, FeedbackState>(StringComparer.Ordinal);
        if (!File.Exists(FilePath)) return result;
        foreach (var line in File.ReadLines(FilePath))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            FeedbackEntry? e;
            try { e = JsonSerializer.Deserialize<FeedbackEntry>(line, Json.Options); }
            catch (JsonException) { continue; }
            if (e?.Stem is not { Length: > 0 }) continue;

            var s = result.TryGetValue(e.Stem, out var existing) ? existing : FeedbackState.None;
            s = e.Signal switch
            {
                "pinned" => s with { Pinned = true },
                "hidden" => s with { Hidden = true },
                "clear" => FeedbackState.None,
                "useful" => s with { Score = s.Score + 2 },
                "noisy" => s with { Score = s.Score - 2 },
                "outdated" => s with { Score = s.Score - 3 },
                "wrong" => s with { Score = s.Score - 4 },
                _ => s,
            };
            result[e.Stem] = s with { SignalCount = s.SignalCount + 1 };
        }
        return result;
    }

    public static FeedbackState For(NoteSummary note, IReadOnlyDictionary<string, FeedbackState> all) =>
        all.TryGetValue(SlugHelper.NormalizeWiki(note.Stem), out var s) ? s : FeedbackState.None;

    public int EntryCount()
    {
        if (!File.Exists(FilePath)) return 0;
        return File.ReadLines(FilePath).Count(l => !string.IsNullOrWhiteSpace(l));
    }
}
