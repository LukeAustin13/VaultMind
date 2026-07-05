using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Syntax;

namespace MindVault.Core;

public sealed record HeadingInfo(int Level, string Text, int Line);

public sealed record WikiLink(string Target, string TargetNorm);

public sealed class ParsedNote
{
    public required string RelativePath { get; init; }
    public required string Title { get; init; }
    public required string Stem { get; init; }
    public required string Slug { get; init; }
    public string? Type { get; init; }
    public string? Status { get; init; }
    public string? Project { get; init; }
    public string? Created { get; init; }
    public string? Updated { get; init; }
    public List<string> Tags { get; init; } = [];
    public List<WikiLink> Links { get; init; } = [];
    public List<HeadingInfo> Headings { get; init; } = [];
    public List<FrontmatterEntry> FrontmatterEntries { get; init; } = [];
    public required string Body { get; init; }
    public required string BodyHash { get; init; }
    public long ModifiedTicks { get; init; }
    public long FileSize { get; init; }

    /// <summary>
    /// Char length of the note with generated regions (map + summary blocks) stripped — the
    /// "human content" size. Size heuristics (large-note/summary-candidate/token-waste) use this
    /// so a hub carrying a generated map block is not mistaken for a large unsummarized note.
    /// </summary>
    public long ContentSize { get; init; }
    public bool HasFrontmatter { get; init; }
    public string? ParseError { get; init; }
}

public static partial class NoteParser
{
    public static ParsedNote ParseFile(string vaultRoot, string absolutePath)
    {
        var content = File.ReadAllText(absolutePath);
        var info = new FileInfo(absolutePath);
        return Parse(content, PathGuard.ToRelative(vaultRoot, absolutePath),
            info.LastWriteTimeUtc.Ticks, info.Length);
    }

    /// <summary>
    /// Normalizes raw note content exactly as <see cref="Parse"/> does (strip BOM, CRLF-&gt;LF,
    /// lone CR-&gt;LF) then returns its uppercase hex SHA-256. Kept alongside Parse so the two
    /// can never diverge.
    /// </summary>
    public static string ComputeBodyHash(string rawContent)
    {
        var content = Normalize(rawContent);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    private static string Normalize(string rawContent) =>
        rawContent.TrimStart('﻿').Replace("\r\n", "\n").Replace("\r", "\n");

    public static ParsedNote Parse(string rawContent, string relativePath, long modifiedTicks = 0, long fileSize = 0)
    {
        var content = Normalize(rawContent);
        var hasFm = FrontmatterCodec.TryExtract(content, out var yamlText, out var body);
        Frontmatter? fm = null;
        string? parseError = null;
        if (hasFm)
        {
            var result = FrontmatterCodec.Parse(yamlText);
            fm = result.Frontmatter;
            parseError = result.Error;
        }

        var stem = Path.GetFileNameWithoutExtension(relativePath);
        var doc = Markdown.Parse(body);
        var headings = ExtractHeadings(body, doc);
        var title = headings.FirstOrDefault(h => h.Level == 1)?.Text is { Length: > 0 } h1 ? h1 : stem;

        // Links and inline tags are extracted from the body with generated regions removed:
        // wiki-links and #tags rendered inside a generated map/summary block are navigation
        // aids, not authored connections. Counting them would let a note the map merely lists
        // stop being an orphan, and re-parsing the block's own "Broken Links" / "Orphans"
        // sections would fabricate links attributed to the hub — breaking rebuild idempotency.
        var structuralBody = GeneratedBlocks.StripAll(body);
        var masked = MaskCodeBlocks(structuralBody, Markdown.Parse(structuralBody));
        var links = new List<WikiLink>();
        var seenLinks = new HashSet<string>();
        foreach (Match m in WikiLinkPattern().Matches(masked))
            AddLink(links, seenLinks, m.Groups[1].Value);
        if (fm is not null)
        {
            foreach (var raw in fm.GetList("links"))
            {
                var inner = raw.Trim();
                if (inner.StartsWith("[[") && inner.EndsWith("]]")) inner = inner[2..^2];
                AddLink(links, seenLinks, inner);
            }
        }

        var tags = new List<string>();
        var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (fm is not null)
        {
            foreach (var t in fm.GetList("tags"))
            {
                var tag = t.Trim().TrimStart('#');
                if (tag.Length > 0 && seenTags.Add(tag)) tags.Add(tag);
            }
        }
        foreach (Match m in InlineTagPattern().Matches(masked))
        {
            var tag = m.Groups[1].Value;
            if (seenTags.Add(tag)) tags.Add(tag);
        }

        return new ParsedNote
        {
            RelativePath = relativePath,
            Title = title,
            Stem = stem,
            Slug = SlugHelper.Slugify(title),
            Type = fm?.GetScalar("type")?.Trim().ToLowerInvariant() is { Length: > 0 } type ? type : null,
            Status = fm?.GetScalar("status")?.Trim() is { Length: > 0 } status ? status : null,
            Project = fm?.GetScalar("project")?.Trim() is { Length: > 0 } project ? project : null,
            Created = fm?.GetScalar("created")?.Trim() is { Length: > 0 } created ? created : null,
            Updated = fm?.GetScalar("updated")?.Trim() is { Length: > 0 } updated ? updated : null,
            Tags = tags,
            Links = links,
            Headings = headings,
            FrontmatterEntries = fm?.Entries.ToList() ?? [],
            Body = body,
            BodyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))), // == ComputeBodyHash(rawContent)
            ModifiedTicks = modifiedTicks,
            FileSize = fileSize,
            ContentSize = GeneratedBlocks.ContentSize(content),
            HasFrontmatter = hasFm,
            ParseError = parseError,
        };
    }

    private static void AddLink(List<WikiLink> links, HashSet<string> seen, string rawTarget)
    {
        var target = rawTarget.Split('|')[0].Split('#')[0].Trim();
        if (target.Length == 0) return;
        var norm = SlugHelper.NormalizeWiki(target);
        if (seen.Add(norm)) links.Add(new WikiLink(target, norm));
    }

    /// <summary>Extracts headings from a body (line numbers are 0-based within the body).</summary>
    public static List<HeadingInfo> ExtractHeadings(string body) => ExtractHeadings(body, Markdown.Parse(body));

    private static List<HeadingInfo> ExtractHeadings(string body, MarkdownDocument doc)
    {
        var list = new List<HeadingInfo>();
        foreach (var h in doc.Descendants<HeadingBlock>())
        {
            var start = h.Span.Start;
            var length = Math.Min(h.Span.Length, body.Length - start);
            if (start < 0 || length <= 0) continue;
            var raw = body.Substring(start, length);
            var firstLine = raw.Split('\n')[0];
            var text = firstLine.TrimStart('#', ' ', '\t');
            // ATX closing hashes: "## Heading ##"
            text = ClosingHashes().Replace(text, "").Trim();
            list.Add(new HeadingInfo(h.Level, text, h.Line));
        }
        return list;
    }

    private static string MaskCodeBlocks(string body, MarkdownDocument doc)
    {
        char[]? chars = null;
        foreach (var block in doc.Descendants<CodeBlock>())
        {
            chars ??= body.ToCharArray();
            var end = Math.Min(block.Span.End, body.Length - 1);
            for (var i = Math.Max(block.Span.Start, 0); i <= end; i++)
            {
                if (chars[i] != '\n') chars[i] = ' ';
            }
        }
        return chars is null ? body : new string(chars);
    }

    [GeneratedRegex(@"\[\[([^\[\]\n]+)\]\]")]
    private static partial Regex WikiLinkPattern();

    [GeneratedRegex(@"(?<![\w#\[])#([A-Za-z][\w/-]*)")]
    private static partial Regex InlineTagPattern();

    [GeneratedRegex(@"\s+#+\s*$")]
    private static partial Regex ClosingHashes();
}
