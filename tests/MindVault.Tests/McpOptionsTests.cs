using MindVault.Core;
using MindVault.Mcp;

namespace MindVault.Tests;

public sealed class McpOptionsTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] vars) =>
        name => vars.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public void DefaultsToStdioOnLocalhost()
    {
        var o = McpOptions.Parse([], Env());
        Assert.Equal("stdio", o.Transport);
        Assert.Equal("127.0.0.1", o.Host);
        Assert.Equal(7777, o.Port);
        Assert.Null(o.AuthToken);
        Assert.False(o.AllowAnonymous);
    }

    [Fact]
    public void EnvironmentVariablesConfigureHttpMode()
    {
        var o = McpOptions.Parse([], Env(
            (McpOptions.TransportEnvVar, "http"),
            (McpOptions.HostEnvVar, "0.0.0.0"),
            (McpOptions.PortEnvVar, "7791"),
            (McpOptions.AuthTokenEnvVar, "secret")));
        Assert.Equal("http", o.Transport);
        Assert.Equal("0.0.0.0", o.Host);
        Assert.Equal(7791, o.Port);
        Assert.Equal("secret", o.AuthToken);
    }

    [Fact]
    public void CliArgumentsWinOverEnvironment()
    {
        var o = McpOptions.Parse(
            ["--transport", "http", "--port", "9000", "--auth-token", "cli-token"],
            Env((McpOptions.TransportEnvVar, "stdio"),
                (McpOptions.PortEnvVar, "7777"),
                (McpOptions.AuthTokenEnvVar, "env-token")));
        Assert.Equal("http", o.Transport);
        Assert.Equal(9000, o.Port);
        Assert.Equal("cli-token", o.AuthToken);
    }

    [Fact]
    public void HttpWithoutTokenIsRefused()
    {
        var ex = Assert.Throws<MindVaultException>(() =>
            McpOptions.Parse(["--transport", "http"], Env()));
        Assert.Contains("auth token", ex.Message);
    }

    [Fact]
    public void HttpWithoutTokenAllowedWhenExplicitlyAnonymous()
    {
        var viaFlag = McpOptions.Parse(["--transport", "http", "--no-auth"], Env());
        Assert.True(viaFlag.AllowAnonymous);
        var viaEnv = McpOptions.Parse(["--transport", "http"],
            Env((McpOptions.AllowAnonymousEnvVar, "true")));
        Assert.True(viaEnv.AllowAnonymous);
    }

    [Fact]
    public void StdioNeverRequiresAToken()
    {
        var o = McpOptions.Parse(["--transport", "stdio"], Env());
        Assert.Null(o.AuthToken);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-port")]
    public void InvalidPortIsRejected(string port)
    {
        Assert.Throws<MindVaultException>(() =>
            McpOptions.Parse(["--port", port], Env()));
    }

    [Fact]
    public void UnknownTransportAndArgumentsAreRejected()
    {
        Assert.Throws<MindVaultException>(() => McpOptions.Parse(["--transport", "tcp"], Env()));
        Assert.Throws<MindVaultException>(() => McpOptions.Parse(["--bogus"], Env()));
    }

    [Fact]
    public void BlankTokenCountsAsMissing()
    {
        var ex = Assert.Throws<MindVaultException>(() =>
            McpOptions.Parse(["--transport", "http"], Env((McpOptions.AuthTokenEnvVar, "   "))));
        Assert.Contains("auth token", ex.Message);
    }

    [Fact]
    public void TokenCanComeFromAFile()
    {
        var file = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N") + ".token");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "  file-secret \n");
        try
        {
            var viaArg = McpOptions.Parse(["--transport", "http", "--auth-token-file", file], Env());
            Assert.Equal("file-secret", viaArg.AuthToken);

            var viaEnv = McpOptions.Parse(["--transport", "http"],
                Env((McpOptions.AuthTokenFileEnvVar, file)));
            Assert.Equal("file-secret", viaEnv.AuthToken);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void ExplicitTokenBeatsTokenFile()
    {
        var file = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N") + ".token");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "file-secret");
        try
        {
            var o = McpOptions.Parse(
                ["--transport", "http", "--auth-token", "direct", "--auth-token-file", file], Env());
            Assert.Equal("direct", o.AuthToken);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void MissingOrEmptyTokenFileIsRejected()
    {
        Assert.Throws<MindVaultException>(() =>
            McpOptions.Parse(["--transport", "http", "--auth-token-file", "Z:/does/not/exist.token"], Env()));

        var empty = Path.Combine(Path.GetTempPath(), "mindvault-tests", Guid.NewGuid().ToString("N") + ".token");
        Directory.CreateDirectory(Path.GetDirectoryName(empty)!);
        File.WriteAllText(empty, "   ");
        try
        {
            Assert.Throws<MindVaultException>(() =>
                McpOptions.Parse(["--transport", "http", "--auth-token-file", empty], Env()));
        }
        finally
        {
            File.Delete(empty);
        }
    }
}
