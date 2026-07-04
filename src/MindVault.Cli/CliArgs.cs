namespace MindVault.Cli;

/// <summary>Minimal argument parser: positionals, --name value options and boolean --flags.</summary>
public sealed class CliArgs
{
    private static readonly HashSet<string> FlagNames =
        new(["json", "create-section", "full", "incremental", "help", "explain",
             "include-archived", "deep", "brief", "all", "version", "verbose", "quiet",
             "dry-run", "allow-duplicate", "apply", "allow-risky-content", "on-this-day", "v2"],
            StringComparer.OrdinalIgnoreCase);

    public List<string> Positionals { get; } = [];
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static CliArgs Parse(string[] argv)
    {
        var args = new CliArgs();
        for (var i = 0; i < argv.Length; i++)
        {
            var token = argv[i];
            if (token.StartsWith("--", StringComparison.Ordinal) && token.Length > 2)
            {
                var name = token[2..];
                if (FlagNames.Contains(name))
                {
                    args.Flags.Add(name);
                }
                else
                {
                    if (i + 1 >= argv.Length)
                        throw new MindVault.Core.MindVaultException($"Option --{name} requires a value.");
                    var next = argv[i + 1];
                    // Reject swallowing a registered flag as this option's value (e.g. `--limit --json`).
                    if (next.StartsWith("--", StringComparison.Ordinal) && next.Length > 2 &&
                        FlagNames.Contains(next[2..]))
                        throw new MindVault.Core.MindVaultException($"Option --{name} requires a value.");
                    args.Options[name] = argv[++i];
                }
            }
            else
            {
                args.Positionals.Add(token);
            }
        }
        return args;
    }

    public string? Opt(string name) => Options.TryGetValue(name, out var v) ? v : null;

    public string Require(string name)
    {
        var v = Opt(name);
        if (string.IsNullOrWhiteSpace(v))
            throw new MindVault.Core.MindVaultException($"Missing required option --{name}.");
        return v;
    }

    public bool Has(string flag) => Flags.Contains(flag);

    public int IntOpt(string name, int fallback)
    {
        var v = Opt(name);
        if (v is null) return fallback;
        if (!int.TryParse(v, out var parsed))
            throw new MindVault.Core.MindVaultException($"Option --{name} must be an integer, got '{v}'.");
        return parsed;
    }
}
