using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace MindVault.Core;

/// <summary>
/// Extracts, parses and serializes flat YAML frontmatter. Parse errors are reported with a
/// "yaml-invalid:" prefix; nested structures with a "yaml-nested:" prefix (nested keys are
/// skipped but flat keys are still returned so the note stays partially usable).
/// </summary>
public static partial class FrontmatterCodec
{
    /// <summary>
    /// Splits normalized ("\n" line endings, no BOM) content into the YAML text between the
    /// `---` fences and the remaining body. Returns false when there is no frontmatter block.
    /// </summary>
    public static bool TryExtract(string content, out string yamlText, out string body)
    {
        yamlText = "";
        body = content;
        if (!content.StartsWith("---\n", StringComparison.Ordinal)) return false;

        var idx = 4;
        while (idx <= content.Length)
        {
            var lineEnd = content.IndexOf('\n', idx);
            var line = lineEnd < 0 ? content[idx..] : content[idx..lineEnd];
            var trimmed = line.TrimEnd();
            if (trimmed is "---" or "...")
            {
                yamlText = content[4..idx];
                body = lineEnd < 0 ? "" : content[(lineEnd + 1)..];
                return true;
            }
            if (lineEnd < 0) return false;
            idx = lineEnd + 1;
        }
        return false;
    }

    public static FrontmatterParseResult Parse(string yamlText)
    {
        YamlMappingNode map;
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(yamlText));
            if (stream.Documents.Count == 0) return new FrontmatterParseResult(new Frontmatter(), null);
            if (stream.Documents[0].RootNode is not YamlMappingNode m)
                return new FrontmatterParseResult(null, "yaml-invalid: frontmatter is not a key/value mapping");
            map = m;
        }
        catch (YamlException ex)
        {
            return new FrontmatterParseResult(null, $"yaml-invalid: {ex.Message}");
        }

        var fm = new Frontmatter();
        string? error = null;
        foreach (var (keyNode, valueNode) in map.Children)
        {
            if (keyNode is not YamlScalarNode { Value: { Length: > 0 } key })
                return new FrontmatterParseResult(null, "yaml-invalid: non-scalar frontmatter key");

            switch (valueNode)
            {
                case YamlScalarNode scalar:
                    fm.Entries.Add(new FrontmatterEntry { Key = key, Scalar = scalar.Value ?? "" });
                    break;
                case YamlSequenceNode seq:
                    var entry = new FrontmatterEntry { Key = key, IsList = true };
                    var flat = true;
                    foreach (var item in seq)
                    {
                        if (item is YamlScalarNode s) entry.Items.Add(s.Value ?? "");
                        else { flat = false; break; }
                    }
                    if (flat) fm.Entries.Add(entry);
                    else error ??= $"yaml-nested: key '{key}' contains a nested structure inside a list";
                    break;
                default:
                    error ??= $"yaml-nested: key '{key}' is a nested mapping (only flat YAML is allowed)";
                    break;
            }
        }
        return new FrontmatterParseResult(fm, error);
    }

    public static string Serialize(Frontmatter fm)
    {
        var sb = new StringBuilder();
        foreach (var e in fm.Entries)
        {
            if (e.IsList)
            {
                if (e.Items.Count == 0)
                {
                    sb.Append(QuoteKeyIfNeeded(e.Key)).Append(": []\n");
                }
                else
                {
                    sb.Append(QuoteKeyIfNeeded(e.Key)).Append(":\n");
                    foreach (var item in e.Items)
                        sb.Append("  - ").Append(QuoteIfNeeded(item)).Append('\n');
                }
            }
            else
            {
                sb.Append(QuoteKeyIfNeeded(e.Key)).Append(':');
                var value = QuoteIfNeeded(e.Scalar ?? "");
                if (value.Length > 0) sb.Append(' ').Append(value);
                sb.Append('\n');
            }
        }
        return sb.ToString();
    }

    public static string BuildDocument(Frontmatter fm, string body) =>
        "---\n" + Serialize(fm) + "---\n" + body;

    private static readonly char[] NeedsQuoting =
        [':', '#', '[', ']', '{', '}', '&', '*', '!', '|', '>', '%', '@', '`', '"', '\'', ','];

    private static string QuoteIfNeeded(string value)
    {
        if (value.Length == 0) return "";
        var needs = value.IndexOfAny(NeedsQuoting) >= 0
                    || value is "~" or "null" or "true" or "false"
                    || value[0] is '-' or '?' or ' '
                    || char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1])
                    || value.Contains('\n') || value.Contains('\r');
        if (!needs) return value;
        return Escape(value);
    }

    private static readonly Regex SafeKeyPattern = SafeKey();

    private static string QuoteKeyIfNeeded(string key) =>
        SafeKeyPattern.IsMatch(key) ? key : Escape(key);

    private static string Escape(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r") + "\"";

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9_-]*$")]
    private static partial Regex SafeKey();
}
