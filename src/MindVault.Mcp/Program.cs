using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MindVault.Core;
using MindVault.Mcp;
using ModelContextProtocol.Protocol;

McpOptions options;
try
{
    options = McpOptions.Parse(args);
}
catch (MindVaultException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

VaultContext context;
try
{
    // Fail fast with a clear setup message instead of exposing a half-configured server.
    context = VaultContext.Create(options.VaultPath);
}
catch (MindVaultConfigException ex)
{
    Console.Error.WriteLine("MindVault MCP server cannot start:");
    Console.Error.WriteLine(ex.Message);
    return 2;
}

// Startup diagnostics go to stderr in both transports; stdio stdout stays protocol-only.
LogStartup(options, context);

return options.Transport == "http"
    ? await RunHttpAsync(options, context)
    : await RunStdioAsync(options, context);

static void LogStartup(McpOptions options, VaultContext context)
{
    Console.Error.WriteLine(
        $"mindvault-mcp v{MindVaultVersion.Current} starting: transport={options.Transport}, " +
        $"profile={options.ToolProfile}, " +
        $"vault exists={Directory.Exists(context.VaultRoot)}, writable={ProbeWritable(context.VaultRoot)}");
}

// The full profile reflects every [McpServerTool] on MindVaultTools; the core profile registers
// only the curated subset. MindVaultTools stays the single source of truth for all declarations.
static IMcpServerBuilder WithProfileTools(IMcpServerBuilder builder, McpOptions options) =>
    options.ToolProfile == "core"
        ? builder.WithTools(ToolProfiles.BuildCoreTools())
        : builder.WithTools<MindVaultTools>();

static bool ProbeWritable(string directory)
{
    try
    {
        var probe = Path.Combine(directory, $".mindvault-probe-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "probe");
        File.Delete(probe);
        return true;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        return false;
    }
}

static async Task<int> RunStdioAsync(McpOptions options, VaultContext context)
{
    // stdout is reserved for the MCP protocol; all logging goes to stderr.
    var builder = Host.CreateApplicationBuilder();
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    builder.Services.AddSingleton(context);
    builder.Services.AddSingleton(new McpRuntimeInfo("stdio"));
    WithProfileTools(
        builder.Services
            .AddMcpServer(o => o.ServerInfo = ServerInfo)
            .WithStdioServerTransport(),
        options);

    await builder.Build().RunAsync();
    return 0;
}

static async Task<int> RunHttpAsync(McpOptions options, VaultContext context)
{
    var builder = WebApplication.CreateBuilder();
    builder.WebHost.UseUrls($"http://{options.Host}:{options.Port}");

    builder.Services.AddSingleton(context);
    builder.Services.AddSingleton(new McpRuntimeInfo("http"));
    WithProfileTools(
        builder.Services
            .AddMcpServer(o => o.ServerInfo = ServerInfo)
            .WithHttpTransport(),
        options);

    var app = builder.Build();

    if (options.AuthToken is null)
    {
        app.Logger.LogWarning(
            "MCP HTTP endpoint is running WITHOUT authentication (anonymous access was explicitly enabled). " +
            "Only do this for local development on a trusted machine.");
    }
    else
    {
        if (options.AuthToken == "change-this-token")
            app.Logger.LogWarning("MINDVAULT_MCP_AUTH_TOKEN still has the placeholder value 'change-this-token'. Change it.");

        var expected = Encoding.UTF8.GetBytes("Bearer " + options.AuthToken);
        app.Use(async (http, next) =>
        {
            if (http.Request.Path == "/healthz")
            {
                await next();
                return;
            }
            var provided = Encoding.UTF8.GetBytes(http.Request.Headers.Authorization.ToString());
            if (!CryptographicOperations.FixedTimeEquals(provided, expected))
            {
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await http.Response.WriteAsync(
                    $"{ErrorCodes.McpAuthFailed}: send 'Authorization: Bearer <token>'.");
                return;
            }
            await next();
        });
    }

    app.MapGet("/healthz", () => Results.Text("ok"));
    app.MapMcp();

    app.Logger.LogInformation(
        "MindVault MCP HTTP endpoint on http://{Host}:{Port} (vault: {Vault}). " +
        "Keep this LAN-only; never expose it to the internet.",
        options.Host, options.Port, context.VaultRoot);

    await app.RunAsync();
    return 0;
}

static partial class Program
{
    private static Implementation ServerInfo => new() { Name = "mindvault", Version = MindVaultVersion.Current };
}
