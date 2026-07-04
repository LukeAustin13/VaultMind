# MindVault MCP Setup

`src/MindVault.Mcp` is an MCP server built on the official C# MCP SDK
(`ModelContextProtocol`). It exposes only the safe `mindvault_*` tools — no raw file,
shell or SQL access — and supports two transports:

- **stdio** (default): the MCP client launches the process; nothing touches the network.
  Use this whenever the client and vault are on the same machine.
- **HTTP** (streamable HTTP, token-protected): a long-running LAN endpoint, used by the
  [Docker deployment](DOCKER.md). Never expose it to the internet.

## Recommended: publish once, run the exe

```bash
dotnet publish src/MindVault.Mcp -c Release -o artifacts/mcp
```

Then register the server with your MCP client (Claude Code, Claude Desktop, etc.).
Replace the placeholder paths:

```json
{
  "mcpServers": {
    "mindvault": {
      "command": "C:/PATH/TO/MindVault/artifacts/mcp/MindVault.Mcp.exe",
      "args": [],
      "env": {
        "MINDVAULT_VAULT_PATH": "C:/PATH/TO/YourObsidianVault"
      }
    }
  }
}
```

With Claude Code you can do the same from the terminal:

```bash
claude mcp add mindvault --env MINDVAULT_VAULT_PATH=C:/PATH/TO/YourObsidianVault -- C:/PATH/TO/MindVault/artifacts/mcp/MindVault.Mcp.exe
```

## Alternative: dotnet run (no publish step)

```json
{
  "mcpServers": {
    "mindvault": {
      "command": "dotnet",
      "args": [
        "run",
        "--project", "C:/PATH/TO/MindVault/src/MindVault.Mcp",
        "--"
      ],
      "env": {
        "MINDVAULT_VAULT_PATH": "C:/PATH/TO/YourObsidianVault"
      }
    }
  }
}
```

You can pass `--vault C:/PATH/TO/YourObsidianVault` as a final entry in `args` instead of
setting the environment variable; the CLI argument wins if both are present.

**Prefer the env var or `--vault` for MCP.** The config-file fallback works when the server
happens to start inside the repo, but MCP clients often launch servers from an arbitrary
working directory.

## HTTP transport

Options (CLI argument wins over its environment variable):

| Argument | Environment variable | Default | Meaning |
| --- | --- | --- | --- |
| `--transport stdio\|http` | `MINDVAULT_MCP_TRANSPORT` | `stdio` | Transport selection |
| `--host` | `MINDVAULT_MCP_HOST` | `127.0.0.1` | Bind address (use `0.0.0.0` only inside a container) |
| `--port` | `MINDVAULT_MCP_PORT` | `7777` | Bind port |
| `--auth-token` | `MINDVAULT_MCP_AUTH_TOKEN` | — | Bearer token, **required** in HTTP mode |
| `--auth-token-file` | `MINDVAULT_MCP_AUTH_TOKEN_FILE` | — | Read the token from a file (Docker secrets); an explicit token wins |
| `--no-auth` | `MINDVAULT_MCP_ALLOW_ANONYMOUS=true` | off | Explicitly allow anonymous HTTP (local development only) |
| `--vault` | `MINDVAULT_VAULT_PATH` | config file | Vault path |

Behaviour:

- HTTP mode **refuses to start** without a token unless anonymous access is explicitly
  enabled — and then it logs a loud warning.
- Every request (except `GET /healthz`) must send `Authorization: Bearer <token>`;
  anything else gets `401` (constant-time token comparison).
- The MCP endpoint is `http://<host>:<port>/` (streamable HTTP).

Run it directly:

```bash
dotnet run --project src/MindVault.Mcp -- --transport http --port 7777 \
  --auth-token your-token-here --vault "C:/PATH/TO/YourObsidianVault"
```

Connect Claude Code to it:

```bash
claude mcp add --transport http mindvault http://127.0.0.1:7777/ \
  --header "Authorization: Bearer your-token-here"
```

**Security:** HTTP mode is for localhost or a trusted LAN, bound to a specific interface.
Do not port-forward it, proxy it publicly, or tunnel it to the internet. If you do not need
a network endpoint, use stdio — it is strictly safer. See the security section in
[DOCKER.md](DOCKER.md).

## Vault path resolution

Same priority as the CLI:

1. `--vault` argument
2. `MINDVAULT_VAULT_PATH` environment variable
3. `config/mindvault.config.local.json` (searched from the working directory and the
   executable location)

If nothing is configured the server exits immediately and prints the setup instructions to
stderr — check your MCP client's server logs.

## Exposed tools

| Tool | Purpose |
| --- | --- |
| `mindvault_status` | Vault path, index state, note count, last scan |
| `mindvault_search` | FTS5 search with type/project/tag/status filters |
| `mindvault_read_note` | Read one note (frontmatter, body, backlinks) |
| `mindvault_list_notes` | Filtered note listing |
| `mindvault_create_project` | New project note in `01_Projects` |
| `mindvault_create_decision` | New decision linked to an existing project |
| `mindvault_create_task` | New task linked to an existing project |
| `mindvault_append_to_note` | Append under a heading (snapshot first) |
| `mindvault_update_frontmatter` | Set one flat frontmatter key (snapshot first) |
| `mindvault_link_notes` | Add a `[[wiki link]]` between notes (snapshot first) |
| `mindvault_archive_note` | Snapshot → `status: archived` → move to `99_Archive` |
| `mindvault_validate_vault` | Schema/link/status validation report |
| `mindvault_get_project_context` | Compact project bundle (goal, tasks, decisions, warnings) |
| `mindvault_get_context_pack` | Generated briefing pack; pass the task for relevance-first ordering |
| `mindvault_check_draft` | Duplicate/vagueness check BEFORE creating a note |
| `mindvault_supersede_decision` | Replace a decision safely (two-note snapshot + rollback) |
| `mindvault_start_session` | Context pack + session log setup in one call — use this first |
| `mindvault_end_session` | One concise dated handoff block |
| `mindvault_detect_project` | Map a repo/folder name to a vault project (aliases, repoNames, confidence tiers) |
| `mindvault_find_related` | Links, backlinks and related notes for one note, each with a reason |
| `mindvault_health` | Fast health check with a good/warning/critical verdict — no secrets, no paths |
| `mindvault_diagnostics` | Deeper: transport, schema versions, validation summary, warnings — no secrets, no paths |
| `mindvault_rebuild_index` | Rebuild the SQLite cache from Markdown |

Creates refuse likely duplicates (same/near-identical name, alias collisions) with
`reason: "possible_duplicate"` and candidate paths; pass `allowDuplicate: true` to
override deliberately. `mindvault_append_to_note`, `mindvault_update_frontmatter` and
`mindvault_archive_note` accept `dryRun: true` to preview without writing.

Deliberately **not** exposed: `write_file`, `delete_file`, `run_shell`, `raw_sql`,
raw filesystem access. Error payloads carry a stable `code`
(see [ERROR_CODES.md](ERROR_CODES.md)).

## Manual smoke test

The server speaks newline-delimited JSON-RPC on stdio, so you can test it without a client.
From the repo root (bash / Git Bash):

```bash
{
  printf '%s\n' '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"0"}}}'
  printf '%s\n' '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  printf '%s\n' '{"jsonrpc":"2.0","id":2,"method":"tools/list"}'
  printf '%s\n' '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"mindvault_status","arguments":{}}}'
  sleep 2
} | dotnet run --project src/MindVault.Mcp -- --vault "C:/PATH/TO/YourObsidianVault"
```

Expected: an `initialize` result with `"name":"mindvault"`, a `tools/list` result containing
all 23 tools, and a `tools/call` result whose text payload is the status JSON. Logs go to
stderr only; stdout carries nothing but protocol frames.
