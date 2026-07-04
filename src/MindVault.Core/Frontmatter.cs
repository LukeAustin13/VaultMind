namespace MindVault.Core;

public sealed class FrontmatterEntry
{
    public required string Key { get; init; }
    public bool IsList { get; set; }
    public string? Scalar { get; set; }
    public List<string> Items { get; } = [];
}

/// <summary>An ordered, flat (scalars and lists of scalars only) YAML frontmatter mapping.</summary>
public sealed class Frontmatter
{
    public List<FrontmatterEntry> Entries { get; } = [];

    public FrontmatterEntry? Find(string key) =>
        Entries.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

    public string? GetScalar(string key)
    {
        var e = Find(key);
        if (e is null) return null;
        return e.IsList ? string.Join(", ", e.Items) : e.Scalar;
    }

    public List<string> GetList(string key)
    {
        var e = Find(key);
        if (e is null) return [];
        if (e.IsList) return [.. e.Items];
        return string.IsNullOrWhiteSpace(e.Scalar) ? [] : [e.Scalar];
    }

    public void SetScalar(string key, string value)
    {
        var e = Find(key);
        if (e is null)
        {
            Entries.Add(new FrontmatterEntry { Key = key, Scalar = value });
            return;
        }
        e.IsList = false;
        e.Items.Clear();
        e.Scalar = value;
    }

    public void SetList(string key, IEnumerable<string> items)
    {
        var e = Find(key);
        if (e is null)
        {
            e = new FrontmatterEntry { Key = key, IsList = true };
            Entries.Add(e);
        }
        e.IsList = true;
        e.Scalar = null;
        e.Items.Clear();
        e.Items.AddRange(items);
    }
}

public sealed record FrontmatterParseResult(Frontmatter? Frontmatter, string? Error);
