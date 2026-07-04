# syntax=docker/dockerfile:1
# Multi-stage, multi-arch (linux/amd64 + linux/arm64) build for MindVault.
# The build stage runs on the build platform and cross-compiles for TARGETARCH,
# which keeps Raspberry Pi builds fast when cross-building from a PC.

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src

# Restore with only the project files first so dependency layers cache well.
COPY src/MindVault.Core/MindVault.Core.csproj src/MindVault.Core/
COPY src/MindVault.Cli/MindVault.Cli.csproj src/MindVault.Cli/
COPY src/MindVault.Mcp/MindVault.Mcp.csproj src/MindVault.Mcp/
RUN dotnet restore src/MindVault.Mcp/MindVault.Mcp.csproj -a $TARGETARCH \
 && dotnet restore src/MindVault.Cli/MindVault.Cli.csproj -a $TARGETARCH

COPY src/ src/
RUN dotnet publish src/MindVault.Mcp -c Release -a $TARGETARCH --no-restore -o /app/mcp \
 && dotnet publish src/MindVault.Cli -c Release -a $TARGETARCH --no-restore -o /app/cli

# Runtime: ASP.NET Core image (the MCP server's HTTP transport needs ASP.NET Core;
# the CLI runs on it too, so one image serves both).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/mcp mcp/
COPY --from=build /app/cli cli/

# Entrypoint dispatch: `mindvault mcp [...]` runs the MCP server, anything else is a CLI
# command (`mindvault status`, `mindvault scan`, ...). Generated here, not COPY'd, so
# Windows line endings can never break the shebang.
RUN printf '#!/bin/sh\nset -e\nif [ "$1" = "mcp" ]; then\n  shift\n  exec dotnet /app/mcp/MindVault.Mcp.dll "$@"\nfi\nexec dotnet /app/cli/MindVault.Cli.dll "$@"\n' \
      > /usr/local/bin/mindvault \
 && chmod 755 /usr/local/bin/mindvault

ENV DOTNET_EnableDiagnostics=0

# Run as the non-root `app` user built into the .NET images. If the mounted vault is owned
# by a different uid (typical on a Raspberry Pi), override with `user:` in docker-compose.
USER $APP_UID

# MCP HTTP port, container-internal only; publishing it is docker-compose's decision.
EXPOSE 7777

# Liveness: the CLI status command verifies the vault mount and config are usable.
HEALTHCHECK --interval=60s --timeout=15s --start-period=20s \
  CMD mindvault status > /dev/null || exit 1

ENTRYPOINT ["mindvault"]
CMD ["mcp"]
