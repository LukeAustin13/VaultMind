using System.Text;
using System.Text.RegularExpressions;

namespace MindVault.Core;

public static partial class SlugHelper
{
    /// <summary>Lowercase, alphanumerics kept, everything else collapsed to single dashes.</summary>
    public static string Slugify(string value)
    {
        var sb = new StringBuilder(value.Length);
        var lastDash = true;
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c)) { sb.Append(c); lastDash = false; }
            else if (!lastDash) { sb.Append('-'); lastDash = true; }
        }
        return sb.ToString().TrimEnd('-');
    }

    /// <summary>Normalization used to compare wiki-link targets: trimmed, inner whitespace collapsed, lowercase.</summary>
    public static string NormalizeWiki(string value) =>
        WhitespaceRun().Replace(value.Trim(), " ").ToLowerInvariant();

    /// <summary>Turns a human-provided name into a safe file name (no separators, traversal or reserved chars).</summary>
    public static string SanitizeFileName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' || char.IsControl(c))
                sb.Append(' ');
            else
                sb.Append(c);
        }
        var clean = WhitespaceRun().Replace(sb.ToString(), " ").Trim(' ', '.');
        if (clean.Length == 0)
            throw new MindVaultException($"'{name}' does not contain any characters usable in a file name.");
        // Windows reserved device names stay reserved even with an extension; prefix to neutralize.
        var stem = clean.Split('.')[0];
        if (ReservedDeviceNames.Contains(stem))
            clean = "_" + clean;
        return clean;
    }

    private static readonly HashSet<string> ReservedDeviceNames =
        new(new[]
            {
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            }, StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();
}
