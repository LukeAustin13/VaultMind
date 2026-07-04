using MindVault.Core;

namespace MindVault.Mcp;

/// <summary>
/// MCP server options. CLI arguments win over their MINDVAULT_MCP_* environment variable
/// equivalents. HTTP mode refuses to start without an auth token unless anonymous access
/// is explicitly enabled for local development.
/// </summary>
public sealed record McpOptions(
    string Transport, string Host, int Port, string? AuthToken, bool AllowAnonymous, string? VaultPath)
{
    public const string TransportEnvVar = McpEnv.Transport;
    public const string HostEnvVar = McpEnv.Host;
    public const string PortEnvVar = McpEnv.Port;
    public const string AuthTokenEnvVar = McpEnv.AuthToken;
    public const string AuthTokenFileEnvVar = McpEnv.AuthTokenFile;
    public const string AllowAnonymousEnvVar = McpEnv.AllowAnonymous;

    public static McpOptions Parse(string[] args, Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;

        string? transport = null, host = null, portText = null, token = null, tokenFile = null, vault = null;
        var noAuth = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--transport": transport = Next(args, ref i, "--transport"); break;
                case "--host": host = Next(args, ref i, "--host"); break;
                case "--port": portText = Next(args, ref i, "--port"); break;
                case "--auth-token": token = Next(args, ref i, "--auth-token"); break;
                case "--auth-token-file": tokenFile = Next(args, ref i, "--auth-token-file"); break;
                case "--vault": vault = Next(args, ref i, "--vault"); break;
                case "--no-auth": noAuth = true; break;
                default:
                    throw new MindVaultException(
                        $"Unknown MCP server argument '{args[i]}'. " +
                        "Supported: --transport stdio|http, --host, --port, --auth-token, --auth-token-file, --no-auth, --vault.");
            }
        }

        transport = (transport ?? getEnv(TransportEnvVar) ?? "stdio").Trim().ToLowerInvariant();
        if (transport is not ("stdio" or "http"))
            throw new MindVaultException($"Unknown MCP transport '{transport}'. Use 'stdio' or 'http'.");

        host = (host ?? getEnv(HostEnvVar) ?? "127.0.0.1").Trim();
        portText = (portText ?? getEnv(PortEnvVar) ?? "7777").Trim();
        if (!int.TryParse(portText, out var port) || port is < 1 or > 65535)
            throw new MindVaultException($"Invalid MCP port '{portText}'. Use a number between 1 and 65535.");

        token ??= getEnv(AuthTokenEnvVar);
        if (string.IsNullOrWhiteSpace(token)) token = null;

        // Docker-secrets style: read the token from a file when no explicit token is set.
        if (token is null)
        {
            tokenFile ??= getEnv(AuthTokenFileEnvVar);
            if (!string.IsNullOrWhiteSpace(tokenFile))
            {
                if (!File.Exists(tokenFile))
                    throw new MindVaultException($"Auth token file not found: {tokenFile}");
                token = File.ReadAllText(tokenFile).Trim();
                if (token.Length == 0)
                    throw new MindVaultException($"Auth token file is empty: {tokenFile}");
            }
        }

        var allowAnonymous = noAuth ||
            string.Equals(getEnv(AllowAnonymousEnvVar), "true", StringComparison.OrdinalIgnoreCase);

        if (transport == "http" && token is null && !allowAnonymous)
        {
            throw new MindVaultException(
                "HTTP transport requires an auth token. Set MINDVAULT_MCP_AUTH_TOKEN (or pass --auth-token), " +
                "or explicitly disable auth for local development with --no-auth / MINDVAULT_MCP_ALLOW_ANONYMOUS=true. " +
                "Never run the HTTP endpoint unauthenticated on a shared network.",
                ErrorCodes.McpAuthRequired);
        }

        return new McpOptions(transport, host, port, token, allowAnonymous, vault);
    }

    private static string Next(string[] args, ref int i, string name)
    {
        if (i + 1 >= args.Length)
            throw new MindVaultException($"Option {name} requires a value.");
        return args[++i];
    }
}
