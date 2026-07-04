# MindVault Setup

## Prerequisites

- .NET 10 SDK (`dotnet --list-sdks` should show a 10.x entry)
- A local Obsidian vault folder. MindVault also works on any plain folder of Markdown files.

## First machine

```bash
git clone <repo>
cd MindVault
copy config\mindvault.config.example.json config\mindvault.config.local.json
```

Edit `config/mindvault.config.local.json`:

```json
{
  "vaultPath": "D:\\Obsidian\\VaultName"
}
```

Only `vaultPath` is required; the other keys have sensible defaults
(see `config/mindvault.config.example.json` for the full shape).

Then:

```bash
dotnet restore
dotnet build
dotnet run --project src/MindVault.Cli -- status
dotnet run --project src/MindVault.Cli -- init
dotnet run --project src/MindVault.Cli -- scan
dotnet run --project src/MindVault.Cli -- search "test"
```

`init` is idempotent: it creates the standard folders (`00_Inbox` … `99_Archive`), the
`.mindvault` operational folder and three note templates in `08_Templates`, and never touches
existing files.

## Every additional machine

Repeat only the config step. The vault itself is synced by whatever you already use
(Obsidian Sync, Syncthing, OneDrive, git…); MindVault only needs to know where the local copy
lives on each machine.

```bash
git clone <repo>
cd MindVault
copy config\mindvault.config.example.json config\mindvault.config.local.json
# set that machine's vaultPath
dotnet run --project src/MindVault.Cli -- status
```

`.mindvault/` lives inside the vault and contains only rebuildable/operational data
(index, snapshots, backups, state). If your sync tool lets you exclude folders, excluding
`.mindvault` avoids syncing cache between machines; if it syncs anyway nothing breaks —
run `rebuild-index` when in doubt.

## Configuration priority

1. `--vault <path>` CLI argument
2. `MINDVAULT_VAULT_PATH` environment variable
3. `config/mindvault.config.local.json` (found by searching upward from the current directory,
   so running from a subfolder works)

The example config is never read. A relative `vaultPath` in the local config resolves against
the repo root.

## Docker alternative

Instead of installing the .NET SDK on the host, MindVault can run entirely in Docker —
including on a Raspberry Pi — with your vault mounted into the container and the MCP server
exposed as a LAN-only HTTP endpoint. See [DOCKER.md](DOCKER.md).

## Verifying the install

```bash
dotnet test                                        # full suite, uses its own fixture vault
dotnet run --project src/MindVault.Cli -- doctor   # health report for your vault
dotnet run --project src/MindVault.Cli -- validate # schema check of your notes
```
