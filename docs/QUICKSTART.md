# Quickstart — new machine in 5 minutes

Everything here is copy-pasteable. Prerequisites: .NET 10 SDK and a local (synced) copy of
your Obsidian vault. For the Docker/Raspberry Pi path use [DOCKER.md](DOCKER.md) instead.

## 1. Clone and configure

```bash
git clone https://github.com/LukeAustin13/VaultMind.git
cd VaultMind
cp config/mindvault.config.example.json config/mindvault.config.local.json
```

Edit `config/mindvault.config.local.json` — set `vaultPath` to this machine's vault folder
(the local config is git-ignored; each machine keeps its own). Alternatively skip the file
and set the environment variable `MINDVAULT_VAULT_PATH`.

## 2. Build and verify

```bash
dotnet build
dotnet run --project src/MindVault.Cli -- doctor
```

`doctor` leads with a health verdict. `GOOD` → continue. `CRITICAL` → the first reason
names the fix (usually a wrong/placeholder `vaultPath` or permissions).

## 3. Index and smoke-test

```bash
dotnet run --project src/MindVault.Cli -- init
dotnet run --project src/MindVault.Cli -- scan
dotnet run --project src/MindVault.Cli -- search "decision"
dotnet run --project src/MindVault.Cli -- detect-project
```

`init` is idempotent (creates missing folders/templates only). `detect-project` with no
argument uses the current folder name — the same lookup agents use.

## 4. Wire up MCP (for Claude Code / any MCP client)

Add to your MCP client config (stdio; see [MCP_SETUP.md](MCP_SETUP.md) for HTTP + token):

```json
{
  "mcpServers": {
    "mindvault": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/VaultMind/src/MindVault.Mcp", "--"],
      "env": { "MINDVAULT_VAULT_PATH": "C:/path/to/YourVault" }
    }
  }
}
```

## 5. Install the skills (optional, recommended)

Copy the folders under `skills/` into your project's `.claude/skills/` (or
`~/.claude/skills/` for everywhere). Details: [SKILLS_SETUP.md](SKILLS_SETUP.md).

## Common failure modes

| Symptom | Fix |
| --- | --- |
| "No vault path configured" | Step 1 was skipped, or the file is named wrong — it must be `config/mindvault.config.local.json` |
| Edited the example config, nothing happens | The example is never loaded; `doctor` warns about exactly this — copy it to the local name |
| Search returns nothing | Run `scan` once; afterwards queries auto-refresh incrementally |
| "Note reference is ambiguous" | Two notes share a title; use the exact path from the candidates list |
| MCP server exits immediately | It prints the reason to stderr; usually the `env` block is missing `MINDVAULT_VAULT_PATH` |
| Raspberry Pi updates | `./scripts/pi-update.sh` — see [UPDATING.md](UPDATING.md) |

Deeper docs: [SETUP.md](SETUP.md) (full setup tour), [WORKFLOW.md](WORKFLOW.md)
(day-to-day loop), [AGENT_WORKFLOWS.md](AGENT_WORKFLOWS.md) (how agents should use the tools).
