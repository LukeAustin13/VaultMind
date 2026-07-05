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
| `mindvault_capture_thought` | Park a raw, uncertain idea in the agent inbox (not durable memory) |
| `mindvault_promote_note` | Thought → decision/memory/task/risk/mistake: validate, dedupe-gate, file correctly |
| `mindvault_organize_vault` | Placement proposals with reasons; dry-run by default, `apply: true` to move |
| `mindvault_create_map` | Add the generated map block to the project hub note (errors if one is present) |
| `mindvault_rebuild_map` | Refresh the hub's map block; idempotent, human text outside markers preserved |
| `mindvault_list_maps` | List projects with/without a map block; flags legacy `09_Maps` files |
| `mindvault_suggest_links` | Reason-tagged link suggestions (never auto-applied) |
| `mindvault_find_broken_links` | Wiki links whose target does not exist |
| `mindvault_find_orphans` | Managed notes with no links in either direction |
| `mindvault_audit_frontmatter` | Frontmatter quality findings, each with a proposed fix |
| `mindvault_audit_aliases` | Alias/repoName hygiene incl. cross-project collisions |
| `mindvault_build_context_capsule` | Mode-shaped, char-budgeted briefing with do-not-repeat rules and source paths |
| `mindvault_get_work_context` | Memory related to a source file / query / note, reasons on every result |
| `mindvault_recall` | What changed since a date (or on this day), grouped by type |
| `mindvault_record_feedback` | pinned/hidden/useful/noisy/outdated/wrong/clear — deterministic ranking signals |
| `mindvault_brain_ops` | One-call brain state: verdict + counts + recommended fixes (no content) |
| `mindvault_checkpoint_session` | One-line mid-session breadcrumb in the session log (dryRun supported) |
| `mindvault_recent_sessions` | Latest handoffs and checkpoints, newest first |
| `mindvault_list_inbox` | Unpromoted thought drafts (add=capture_thought, promote=promote_note, reject=archive_note) |
| `mindvault_add_mistake` | Durable lesson with a prevention rule (duplicate + content gated) |
| `mindvault_list_mistakes` | Active lessons — the do-not-repeat list |
| `mindvault_resolve_mistake` | Mark a lesson done; it leaves capsules but stays in the ledger |
| `mindvault_build_route_card` | Read-first ≤5 + do-not-read navigation brief with reasons + token estimates |
| `mindvault_build_read_plan` | Strict ordered read plan (max 5) with stop conditions and a single fallback search |
| `mindvault_get_project_map` | The hub's map block content in one payload — cheapest orientation read |
| `mindvault_find_low_value_notes` | Notes agents should skip by default, with reasons (guidance only) |
| `mindvault_token_audit` | Token totals, largest notes, unsummarized large notes, capsule-vs-route cost |
| `mindvault_organisation_score` | 0–100 across 11 explainable categories + weaknesses + token waste |
| `mindvault_generate_summaries` | Extractive summary blocks for large notes (dry-run default, block-splice only) |
| `mindvault_build_graph` | Typed relationship graph → `.mindvault/link-graph.jsonl` (deterministic) |
| `mindvault_explain_relationships` | Why two notes matter together — direct or two-hop, with reasons |
| `mindvault_compile_brain` | Maps + summaries + graph + health + score in one pass (dry-run default) |

Creates refuse likely duplicates (same/near-identical name, alias collisions) with
`reason: "possible_duplicate"` and candidate paths; pass `allowDuplicate: true` to
override deliberately. `mindvault_append_to_note`, `mindvault_update_frontmatter` and
`mindvault_archive_note` accept `dryRun: true` to preview without writing;
`mindvault_organize_vault` is a dry-run unless `apply: true`. Applying a link suggestion
is `mindvault_link_notes` — a separate apply-link tool was deliberately not added.

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
all 55 tools, and a `tools/call` result whose text payload is the status JSON. Logs go to
stderr only; stdout carries nothing but protocol frames.
