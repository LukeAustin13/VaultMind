namespace MindVault.Core;

/// <summary>
/// Pulls compact pieces out of a note body by heading — section text, bullet items,
/// dated sub-entries. Used by project context and context packs so they can surface
/// content without dumping whole notes.
/// </summary>
public static class SectionExtractor
{
    /// <summary>Trimmed text of a section (heading excluded), or null when the heading is absent.</summary>
    public static string? GetSectionText(string body, string headingText, int maxChars = 400)
    {
        var lines = Slice(body, headingText);
        if (lines is null) return null;
        var text = string.Join("\n", lines).Trim();
        if (text.Length > maxChars) text = text[..maxChars].TrimEnd() + " …";
        return text.Length == 0 ? null : text;
    }

    /// <summary>Bullet items ("- x" / "* x") of a section, bullet markers stripped.</summary>
    public static List<string> GetBullets(string body, string headingText, int max = 10)
    {
        var lines = Slice(body, headingText);
        if (lines is null) return [];
        return lines
            .Select(l => l.TrimStart())
            .Where(l => l.StartsWith("- ") || l.StartsWith("* "))
            .Select(l => l[2..].Trim())
            .Where(l => l.Length > 0)
            .Take(max)
            .ToList();
    }

    /// <summary>
    /// Sub-headings one level below the given section (e.g. "### 2026-07-04 — shipped X"
    /// entries under "## Sessions"), newest-first by text (entries start with a date).
    /// </summary>
    public static List<string> GetSubheadings(string body, string headingText, int max = 3)
    {
        var normalized = body.Replace("\r\n", "\n");
        var headings = NoteParser.ExtractHeadings(normalized);
        var target = headings.FirstOrDefault(h =>
            string.Equals(h.Text, headingText.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null) return [];
        var end = headings
            .Where(h => h.Line > target.Line && h.Level <= target.Level)
            .OrderBy(h => h.Line)
            .FirstOrDefault();
        return headings
            .Where(h => h.Line > target.Line && (end is null || h.Line < end.Line) && h.Level == target.Level + 1)
            .Select(h => h.Text)
            .OrderByDescending(t => t, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .ToList();
    }

    private static string[]? Slice(string body, string headingText)
    {
        var normalized = body.Replace("\r\n", "\n");
        var headings = NoteParser.ExtractHeadings(normalized);
        var target = headings.FirstOrDefault(h =>
            string.Equals(h.Text, headingText.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null) return null;
        var lines = normalized.Split('\n');
        var end = headings
            .Where(h => h.Line > target.Line && h.Level <= target.Level)
            .OrderBy(h => h.Line)
            .FirstOrDefault()?.Line ?? lines.Length;
        return lines[(target.Line + 1)..Math.Min(end, lines.Length)];
    }
}
