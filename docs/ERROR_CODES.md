# Error Codes

MindVault errors carry a stable machine-readable code alongside the human message. Codes
appear in:

- **CLI** — `--json` failures: `{ "ok": false, "error": "…", "code": "NOTE_NOT_FOUND" }`
- **MCP** — tool error payloads: `{ "error": "…", "code": "NOTE_NOT_FOUND" }`
- **HTTP MCP auth** — the 401 body starts with `MCP_AUTH_FAILED:`

Codes are a public contract: they are never renamed, only added. Branch on `code`, never on
message text. Defined in `src/MindVault.Core/Exceptions.cs` (`ErrorCodes`).

| Code | Meaning | Typical fix |
| --- | --- | --- |
| `CONFIG_MISSING` | No vault path from `--vault`, `MINDVAULT_VAULT_PATH`, or local config | Configure one of the three sources (`mindvault status` shows which is active) |
| `CONFIG_INVALID` | Local config file exists but is empty/not valid JSON | Fix `config/mindvault.config.local.json` |
| `VAULT_NOT_FOUND` | Configured vault path does not exist on disk | Create the folder or fix the path |
| `VAULT_NOT_WRITABLE` | Vault/snapshot folder failed a write probe (diagnostics contexts) | Fix permissions / the Docker `user:` mapping |
| `NOTE_NOT_FOUND` | No note matches the reference (path, title, filename, slug) | Check the ref; `search` first |
| `NOTE_REF_AMBIGUOUS` | Several notes match; MindVault refuses to guess (CLI exit 3) | Use the exact path from `candidates` |
| `INVALID_FRONTMATTER` | Broken YAML on the target note, an invalid key/value, nested YAML, or a write that would have produced invalid YAML (auto-rolled-back) | Fix the note in Obsidian / flatten the value |
| `PATH_TRAVERSAL_REJECTED` | Path escapes the vault or targets an operational folder | Use a vault-relative note path |
| `WRITE_LOCKED` | Another MindVault process holds `.mindvault/write.lock` (fresh) | Retry shortly; if nothing is running, delete the lock file |
| `SNAPSHOT_FAILED` | The pre-mutation snapshot could not be taken (e.g. source vanished) | Nothing was mutated; investigate the path |
| `INDEX_STALE` | Index missing/outdated where an operation needed it (`index verify`) | `mindvault scan` or `index rebuild` |
| `MCP_AUTH_REQUIRED` | HTTP transport started without a token and without explicit anonymous opt-in | Set `MINDVAULT_MCP_AUTH_TOKEN` (or `--no-auth` for local dev only) |
| `MCP_AUTH_FAILED` | Request had a missing/wrong bearer token (HTTP 401) | Send `Authorization: Bearer <token>` |
| `DUPLICATE_SUSPECTED` | A create was refused because a very similar note (or an alias-colliding project) already exists; `candidates` lists them | Update/supersede the existing note, or pass `--allow-duplicate` / `allowDuplicate: true` deliberately |
| `RISKY_CONTENT` | A write was blocked because the content looks like a secret (private key, cloud/API/bearer token); evidence is redacted | Remove the secret (describe it instead), or pass `--allow-risky-content` / `allowRiskyContent: true` deliberately |
| `MINDVAULT_ERROR` | Any other known-and-handled MindVault error | Read the message; it names the fix |
| `UNEXPECTED_ERROR` | Unhandled exception (CLI exit 1; MCP returns a sanitized message) | Bug — the CLI/stderr has details |

## CLI exit codes

| Exit | Meaning |
| --- | --- |
| 0 | Success (for `validate`/`check-*`/`index verify`: no criticals/blockers/issues) |
| 1 | Unexpected error, or checks found critical problems |
| 2 | Known error or bad usage (the `code` says which) |
| 3 | Ambiguous note reference |
