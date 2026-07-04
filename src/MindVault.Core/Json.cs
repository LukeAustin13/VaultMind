using System.Text.Json;
using System.Text.Json.Serialization;

namespace MindVault.Core;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    /// <summary>
    /// Reads an indexed frontmatter value as a string list: YAML lists are stored as JSON
    /// arrays, scalars as plain text (comma-separated tolerated). Never throws.
    /// </summary>
    public static List<string> ReadStringList(string value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0) return [];
        if (value.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(value, Options)?
                    .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList() ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
        return value.Split(',').Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
    }
}
