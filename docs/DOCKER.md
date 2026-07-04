# MindVault in Docker (Raspberry Pi / Linux / Docker Desktop)

Run MindVault as a container: the MCP server as a long-running LAN-only HTTP endpoint, and
the CLI through the same image. The Obsidian vault is mounted from the host; the SQLite
index, snapshots and backups live inside the mounted vault under `.mindvault/`, so they stay
with the vault.

```
Raspberry Pi / Linux Host
  ├─ Docker + Docker Compose
  ├─ Local Obsidian vault copy  ──mounted──►  /vault in the container
  ├─ MindVault container (MCP HTTP server, CLI via the same image)
  ├─ /vault/.mindvault/index.sqlite   (rebuildable cache, stays on the host)
  └─ MCP endpoint bound to localhost or a LAN address — never the internet
```

## Quick start

```bash
git clone <repo>
cd MindVault
cp docker-compose.example.yml docker-compose.yml
# edit docker-compose.yml:
#   - volumes: point at your vault           e.g.  /mnt/obsidian/MindVault:/vault
#   - MINDVAULT_MCP_AUTH_TOKEN: set a real token
docker compose up -d --build
docker compose logs -f mindvault
```

First-time vault preparation and a health check:

```bash
docker compose run --rm mindvault init
docker compose run --rm mindvault scan
docker compose run --rm mindvault doctor
```

## Updating later

Pull updates without disturbing your setup:

```bash
./scripts/pi-update.sh
```

It fast-forwards the repo, rebuilds the container and confirms the server is live —
refusing safely if it would clobber anything. Your vault, `docker-compose.yml`, config and
tokens are never touched (they live outside the repo or are git-ignored). The rule that
keeps it that way: edit your **copies** (`docker-compose.yml`,
`config/mindvault.config.local.json`), never the tracked example files. Full details,
including how to recover a diverged checkout: [UPDATING.md](UPDATING.md).

## CLI through Docker

The image has an entrypoint dispatcher: the first argument `mcp` starts the MCP server,
anything else is a normal MindVault CLI command — no `dotnet` incantations needed.

One-off commands (separate short-lived container, same image and vault):

```bash
docker compose run --rm mindvault status
docker compose run --rm mindvault init
docker compose run --rm mindvault scan
docker compose run --rm mindvault search "architecture"
docker compose run --rm mindvault doctor
```

Or inside the running server container:

```bash
docker compose exec mindvault mindvault status
docker compose exec mindvault mindvault scan
docker compose exec mindvault mindvault doctor
```

## MCP server through Docker

The compose service runs `mindvault mcp` with `MINDVAULT_MCP_TRANSPORT=http`. The endpoint
is streamable HTTP at `http://<bound-address>:7777/` and **requires**
`Authorization: Bearer <token>` (the server refuses to start in HTTP mode without a token
unless anonymous access is explicitly enabled — see [MCP_SETUP.md](MCP_SETUP.md)).

Quick checks from the host:

```bash
curl http://127.0.0.1:7777/healthz                          # "ok", no auth needed
curl -i http://127.0.0.1:7777/ -X POST                      # 401 without the token
```

Connect Claude Code (same machine):

```bash
claude mcp add --transport http mindvault http://127.0.0.1:7777/ \
  --header "Authorization: Bearer your-token-here"
```

From another machine on the LAN, first opt in to LAN binding (below), then use
`http://192.168.1.77:7777/` instead.

## Port binding: safe by default

The example compose file binds to localhost only:

```yaml
ports:
  - "127.0.0.1:7777:7777"
```

LAN access is an explicit opt-in — bind exactly one LAN interface:

```yaml
ports:
  - "192.168.1.77:7777:7777"
```

Avoid a bare `"7777:7777"`: it binds **all** interfaces, which on a machine with a public
interface means public exposure. If you use it anyway, you must have a firewall in front,
and you still should not — see Security below.

## Raspberry Pi

64-bit Raspberry Pi OS (or any ARM64 Linux) with Docker + the compose plugin works out of
the box: the .NET base images are multi-arch, so the same
`docker compose up -d --build` builds a native ARM64 image on the Pi.

Typical Pi compose volume:

```yaml
volumes:
  - /mnt/obsidian/MindVault:/vault
```

Vault ownership: the container runs as a non-root user that usually does not match the
`pi` user owning the vault. Uncomment in the compose file:

```yaml
user: "1000:1000"   # match: id -u / id -g on the Pi
```

Daily driving:

```bash
docker compose up -d --build
docker compose logs -f mindvault
docker compose exec mindvault mindvault status
docker compose exec mindvault mindvault scan
docker compose exec mindvault mindvault doctor
```

### Cross-building images for the Pi (optional)

Building on the Pi is simplest. To build on a PC instead:

```bash
docker buildx build --platform linux/arm64 -t mindvault:pi .
```

Multi-platform build:

```bash
docker buildx build --platform linux/amd64,linux/arm64 -t mindvault:latest .
```

(Pushing a multi-arch manifest needs a registry: add `--push -t <registry>/mindvault:latest`.
The Dockerfile cross-compiles — the build stage runs on the build platform and targets
`$TARGETARCH`, so ARM64 builds on an x64 PC are fast, no emulation of the compiler.)

## Local development (Docker Desktop on Windows/macOS/Linux x64)

Identical flow; only the volume path differs:

```yaml
volumes:
  - C:/Users/you/Obsidian/MyVault:/vault
```

For quick local experiments without a token you may run the server with auth explicitly
disabled (`MINDVAULT_MCP_ALLOW_ANONYMOUS: "true"` instead of the token) — only ever with the
default `127.0.0.1` port binding, and never on a shared machine.

## Where state lives

| Data | Location | Notes |
| --- | --- | --- |
| Notes (canonical) | `/vault` = your host vault | The only thing that matters |
| SQLite index | `/vault/.mindvault/index.sqlite` | Rebuildable: `mindvault rebuild-index` |
| Snapshots | `/vault/.mindvault/snapshots/` | Written before every mutation |
| Backups | `/vault/.mindvault/backups/` | `mindvault backup` zips |

The container itself is stateless — you can delete and rebuild it freely.

The image has a built-in `HEALTHCHECK` that runs `mindvault status` every 60s, so
`docker ps` shows the container as `healthy`/`unhealthy` (an unhealthy state usually means
the vault mount broke). Snapshots accumulate over time; prune old ones occasionally:

```bash
docker compose run --rm mindvault prune          # uses snapshotRetentionDays (default 30)
docker compose run --rm mindvault prune --days 7
```

## Security

1. **Do not expose MindVault MCP to the public internet.** No port forwarding, no reverse
   proxy on a public host, no tunnels to it. It is a LAN tool.
2. **Bind narrowly.** Default to `127.0.0.1`; LAN binding to one specific interface is the
   only sanctioned alternative.
3. **Always use an auth token in HTTP mode.** The server enforces this; anonymous mode
   exists only for localhost development and says so loudly in the logs. To keep the token
   out of the compose file, use a Docker secret and point
   `MINDVAULT_MCP_AUTH_TOKEN_FILE` at it:

   ```yaml
   services:
     mindvault:
       environment:
         MINDVAULT_MCP_AUTH_TOKEN_FILE: /run/secrets/mindvault_token
       secrets:
         - mindvault_token
   secrets:
     mindvault_token:
       file: ./mindvault_token.txt   # keep out of git
   ```
4. **Mount only the intended vault path.** Never mount your home directory or a parent
   folder "for convenience" — the mount is the container's entire filesystem reach into
   your data.
5. **The container has write access to the mounted vault.** That is the point (create,
   append, archive), but treat the mount accordingly.
6. **Snapshots are a safety net, not a backup strategy.** They protect against a bad edit,
   not against disk loss.
7. **Keep the vault backed up separately** (your existing sync plus a real backup).
8. **Prefer stdio MCP when HTTP isn't needed.** A local stdio server (or
   `ssh pi@host docker compose run --rm -T mindvault mcp` from a trusted machine) exposes
   no network port at all and is the maximum-safety option.

## Troubleshooting

- **`Vault path does not exist: /vault`** — the volume mount is wrong or missing; check the
  left-hand side of `volumes:` in your compose file.
- **Permission denied writing `.mindvault`** — vault owned by a different uid than the
  container user; set `user: "<uid>:<gid>"` (see Raspberry Pi section).
- **401 from the endpoint** — missing/wrong `Authorization: Bearer <token>` header;
  `/healthz` is the only unauthenticated route.
- **Server exits immediately** — `docker compose logs mindvault`; the two usual causes are
  a missing auth token in HTTP mode and a bad vault mount, both reported explicitly.
- **Search looks stale** — external edits are indexed on demand:
  `docker compose exec mindvault mindvault scan`.
