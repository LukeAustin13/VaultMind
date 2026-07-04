#!/usr/bin/env bash
#
# MindVault safe updater for the Raspberry Pi (or any Docker host).
#
# What it guarantees:
#   * It NEVER touches your Obsidian vault — the vault lives outside this repo, mounted
#     into the container; git only ever touches files in this checkout.
#   * It NEVER overwrites your local config or secrets — docker-compose.yml, the config
#     .local.json and any token/.env files are git-ignored, so a pull cannot clobber them.
#   * It refuses to run if you have edited tracked repo files, instead of clobbering or
#     merging them. Nothing is destroyed; you get a clear message.
#   * It only fast-forwards (never a surprise merge/rebase), so a diverged checkout fails
#     loudly rather than tangling.
#
# Usage (from anywhere):  ./scripts/pi-update.sh
#
set -euo pipefail

# --- locate and enter the repo root -----------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

if [ ! -d .git ]; then
  echo "error: $REPO_ROOT is not a git checkout." >&2
  exit 1
fi

echo "MindVault updater — repo: $REPO_ROOT"

# --- refuse to proceed with local edits to TRACKED files --------------------------------
# Ignored files (docker-compose.yml, config/*.local.json, .env, *.token) do NOT show here,
# so your local setup never blocks the update.
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo >&2
  echo "error: you have uncommitted changes to tracked files. Refusing to update so nothing" >&2
  echo "       is lost. Your local config (docker-compose.yml, config/*.local.json, tokens)" >&2
  echo "       is git-ignored and is NOT the problem — these tracked files are:" >&2
  echo >&2
  git status --short --untracked-files=no | sed 's/^/       /' >&2
  echo >&2
  echo "       Revert them (git checkout -- <file>) or move your changes into an ignored" >&2
  echo "       file, then re-run. See docs/UPDATING.md." >&2
  exit 1
fi

# --- fast-forward pull -------------------------------------------------------------------
BEFORE="$(git rev-parse HEAD)"
BRANCH="$(git rev-parse --abbrev-ref HEAD)"
echo "Pulling latest on '$BRANCH' (fast-forward only)…"
if ! git pull --ff-only; then
  echo >&2
  echo "error: fast-forward failed. Your Pi checkout has commits that upstream does not," >&2
  echo "       so a plain pull would need a merge. This is almost always because a tracked" >&2
  echo "       file was edited and committed locally (e.g. the compose example instead of a" >&2
  echo "       copy). See docs/UPDATING.md for how to reset cleanly." >&2
  exit 1
fi
AFTER="$(git rev-parse HEAD)"

if [ "$BEFORE" = "$AFTER" ]; then
  echo "Already up to date — nothing changed, nothing to rebuild."
  exit 0
fi

echo "Updated $(git rev-parse --short "$BEFORE") -> $(git rev-parse --short "$AFTER")."

# --- rebuild the container (only if this host runs the Docker deployment) ----------------
if command -v docker >/dev/null 2>&1 && [ -f docker-compose.yml ]; then
  COMPOSE="docker compose"
  $COMPOSE version >/dev/null 2>&1 || COMPOSE="docker-compose"

  echo "Rebuilding and restarting the container…"
  $COMPOSE up -d --build

  # The server auto-rescans on its next query if the index schema changed (your Markdown is
  # canonical, so this is always safe and lossless). A read-only status confirms it is live.
  echo "Container status:"
  $COMPOSE ps
  echo
  echo "Health check (read-only):"
  $COMPOSE exec -T mindvault mindvault status || \
    echo "  (status check skipped — the container may still be starting; check 'docker compose logs')"
  echo
  echo "Done. The MCP server picks up changes automatically; it re-indexes lazily on the"
  echo "next query. Force it now with:  $COMPOSE run --rm mindvault index rebuild"
else
  echo
  echo "Repo updated. No docker-compose.yml here (or Docker not installed), so nothing was"
  echo "rebuilt — restart MindVault however you run it, then 'mindvault index verify'."
fi
