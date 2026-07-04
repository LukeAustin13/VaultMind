namespace MindVault.Core;

public sealed record InitResult(IReadOnlyList<string> CreatedFolders, IReadOnlyList<string> CreatedTemplates);

public static class VaultStructure
{
    public static readonly IReadOnlyList<string> RequiredFolders =
    [
        "00_Inbox", "01_Projects", "02_Areas", "03_Resources", "04_Decisions",
        "05_Prompts", "06_Agent_Memory", "07_Reviews", "08_Templates", "99_Archive",
        ".mindvault",
    ];

    public static readonly IReadOnlyList<string> TemplateFiles =
    [
        "08_Templates/Project.md", "08_Templates/Decision.md", "08_Templates/Task.md",
        "08_Templates/Risk.md", "08_Templates/Constraint.md", "08_Templates/Architecture.md",
        "08_Templates/Implementation Log.md", "08_Templates/Review.md",
        "08_Templates/Prompt.md", "08_Templates/Memory.md",
    ];

    /// <summary>Folder a managed note type is expected to live in (besides 99_Archive). Null = anywhere.</summary>
    public static string? ExpectedFolder(string? type) => type?.ToLowerInvariant() switch
    {
        "project" => "01_Projects",
        "task" => "01_Projects",
        "decision" => "04_Decisions",
        "prompt" => "05_Prompts",
        "memory" => "06_Agent_Memory",
        "review" => "07_Reviews",
        _ => null,
    };

    public static InitResult EnsureStructure(string vaultRoot)
    {
        var createdFolders = new List<string>();
        foreach (var folder in RequiredFolders)
        {
            var full = Path.Combine(vaultRoot, folder);
            if (!Directory.Exists(full))
            {
                Directory.CreateDirectory(full);
                createdFolders.Add(folder);
            }
        }
        foreach (var sub in new[] { ".mindvault/snapshots", ".mindvault/logs" })
            Directory.CreateDirectory(Path.Combine(vaultRoot, sub));

        var createdTemplates = new List<string>();
        foreach (var (relPath, content) in NoteTemplates.InitTemplates())
        {
            var full = Path.Combine(vaultRoot, relPath);
            if (!File.Exists(full))
            {
                File.WriteAllText(full, content);
                createdTemplates.Add(relPath);
            }
        }
        return new InitResult(createdFolders, createdTemplates);
    }
}
