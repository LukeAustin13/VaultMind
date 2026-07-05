namespace MindVault.Core;

/// <summary>
/// Shared handling for MindVault's two generated-block marker pairs — the map block
/// (<see cref="MapService.MarkerStart"/>/<see cref="MapService.MarkerEnd"/>) and the summary
/// block (<see cref="SummaryService.MarkerStart"/>/<see cref="SummaryService.MarkerEnd"/>).
///
/// Both live on the SAME note now (the project hub carries a map block, and any note can carry
/// a summary block), so size heuristics must exclude generated regions: a hub carrying a big
/// map block is not "a large note missing a summary". <see cref="StripAll"/> removes every
/// region defensively (all occurrences of both pairs), and <see cref="ContentSize"/> is the
/// char length of what a human actually wrote — the number the summary/large-note/token-waste
/// decisions use, while raw file bytes still drive full-read token accounting.
/// </summary>
public static class GeneratedBlocks
{
    private static readonly (string Start, string End)[] Pairs =
    [
        (MapService.MarkerStart, MapService.MarkerEnd),
        (SummaryService.MarkerStart, SummaryService.MarkerEnd),
    ];

    /// <summary>How a body's occurrences of one marker pair classify.</summary>
    public enum BlockKind
    {
        /// <summary>Zero occurrences of either marker — no block present.</summary>
        None,
        /// <summary>Exactly one start and one end, start before end — a single well-formed block.</summary>
        Single,
        /// <summary>Any other combination (duplicate starts, duplicate ends, end-before-start, an
        /// unpaired marker) — the block cannot be located unambiguously and must not be guessed.</summary>
        Ambiguous,
    }

    /// <summary>
    /// Locating result for one marker pair in a body. For <see cref="BlockKind.Single"/>, Start and
    /// End are the char indices of the start marker and the end marker (the closing marker begins at
    /// End, so the block spans <c>[Start, End + endMarker.Length)</c>). StartCount/EndCount always
    /// report how many times each marker string occurs — used to explain an ambiguous refusal.
    /// </summary>
    public readonly record struct BlockLocation(BlockKind Kind, int Start, int End, int StartCount, int EndCount);

    /// <summary>
    /// Classifies a body's occurrences of a single marker pair without guessing. This is the one
    /// place that decides whether a generated block can be safely located: callers splice only on
    /// <see cref="BlockKind.Single"/>, append on <see cref="BlockKind.None"/>, and refuse on
    /// <see cref="BlockKind.Ambiguous"/> (a stray literal marker in prose or a code fence, a
    /// duplicated block, or an end-before-start pairing). Never silently trusts first-occurrence
    /// positions the way naive <c>IndexOf</c> did.
    /// </summary>
    public static BlockLocation Locate(string body, string startMarker, string endMarker)
    {
        var startCount = CountOccurrences(body, startMarker);
        var endCount = CountOccurrences(body, endMarker);
        if (startCount == 0 && endCount == 0)
            return new BlockLocation(BlockKind.None, -1, -1, 0, 0);
        if (startCount == 1 && endCount == 1)
        {
            var start = body.IndexOf(startMarker, StringComparison.Ordinal);
            var end = body.IndexOf(endMarker, StringComparison.Ordinal);
            if (start < end)
                return new BlockLocation(BlockKind.Single, start, end, 1, 1);
        }
        return new BlockLocation(BlockKind.Ambiguous, -1, -1, startCount, endCount);
    }

    private static int CountOccurrences(string body, string marker)
    {
        var count = 0;
        var from = 0;
        int at;
        while ((at = body.IndexOf(marker, from, StringComparison.Ordinal)) >= 0)
        {
            count++;
            from = at + marker.Length;
        }
        return count;
    }

    /// <summary>
    /// Removes every generated region (both marker pairs, all occurrences) from the text,
    /// markers included. Unmatched or malformed markers leave the text unchanged for that pair.
    /// Input may be a full note or a body; newlines are normalised to LF.
    /// </summary>
    public static string StripAll(string text)
    {
        var result = text.Replace("\r\n", "\n");
        foreach (var (start, end) in Pairs)
        {
            int from;
            // Scan left-to-right; each removed region shrinks the string so the next search
            // starts at the splice point, which cannot re-enter a region we just deleted. Each end
            // is matched after its own start, so pairing is well-formed here. NOTE: a stray start
            // marker written in prose (or a code fence) will pair with the next real end marker and
            // over-strip the human text between them for sizing purposes — accepted, because this
            // feeds only size heuristics (ContentSize), never a write. The write paths (Map/Summary
            // splice) refuse on that ambiguity via Locate instead of guessing.
            var search = 0;
            while ((from = result.IndexOf(start, search, StringComparison.Ordinal)) >= 0)
            {
                var to = result.IndexOf(end, from + start.Length, StringComparison.Ordinal);
                if (to < 0) break; // no closing marker — leave the rest untouched
                result = result[..from] + result[(to + end.Length)..];
                search = from;
            }
        }
        return result;
    }

    /// <summary>
    /// The char length of <paramref name="normalizedContent"/> once every generated region is
    /// removed — the "content size" stored in the index and compared against
    /// <see cref="SummaryService.LargeBodyChars"/>.
    /// </summary>
    public static long ContentSize(string normalizedContent) => StripAll(normalizedContent).Length;
}
