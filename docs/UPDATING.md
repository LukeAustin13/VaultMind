# Updating MindVault (without breaking your setup)

You cloned MindVault on the Pi and configured it. This page guarantees that pulling future
updates never touches your vault, your config, or your secrets — and tells you the one rule
that keeps it that way.

## Why an update is safe

A `git pull` only ever changes files **inside this repo checkout**. Two facts make it
harmless:

1. **Your vault is outside the repo.** It lives on the host (mounted into the container at
   `/vault`); its notes, and the `.mindvault/` index/snapshots/backups inside it, are never
   part of this checkout. Git cannot see them, so it cannot change them.
2. **Your local config and secrets are git-ignored.** `docker-compose.yml`,
   `config/*.local.json`, `.env`, and `*.token`/`*.secret` files are listed in
   `.gitignore`. A pull never overwrites an ignored file. (Check any time with
   `git check-ignore docker-compose.yml` — it should print the name back.)

The SQLite index is disposable cache. If an update bumps the index schema, the server
resets and rebuilds the index automatically on its next query — your Markdown is canonical,
so this is lossless every time.

## The one rule

**Never edit a tracked file in place. Keep all your local settings in ignored copies.**

- Config → `config/mindvault.config.local.json` (copy of the example), never the example.
- Compose → `docker-compose.yml` (copy of the example), never `docker-compose.example.yml`.

If you follow this, your local changes and the upstream changes live in different files and
can never collide.

## How to update

From the repo directory on the Pi:

```bash
./scripts/pi-update.sh
```

It: refuses if you have uncommitted edits to tracked files (so nothing is lost),
fast-forwards the pull (no surprise merges), and — if a `docker-compose.yml` is present —
rebuilds and restarts the container, then runs a read-only `status` to confirm it is live.
If nothing changed upstream, it does nothing.

Prefer to do it by hand? The same three steps:

```bash
git pull --ff-only
docker compose up -d --build
docker compose run --rm mindvault index verify   # optional: confirm the cache is clean
```

## If the update refuses to run

The script stops (nothing changed) in two cases, both recoverable:

**"uncommitted changes to tracked files"** — you edited a file that git tracks. It lists
them. If those edits were accidental (or were your token/paths that belong in an ignored
copy), undo them and move the real settings into `docker-compose.yml` /
`config/mindvault.config.local.json`:

```bash
git status --short                 # see what changed
git checkout -- <file>             # discard an unwanted change to a tracked file
```

**"fast-forward failed"** — your Pi checkout has local *commits* upstream doesn't have
(usually from committing an edit to the example files early on). Reset the checkout to match
upstream — this only touches tracked files; your ignored config and your vault are safe:

```bash
git fetch origin
git reset --hard origin/main       # discards local commits to tracked files only
./scripts/pi-update.sh
```

If you are unsure whether a file you changed is local-only, `git check-ignore <file>`
prints the name when it is safely ignored and nothing when it is tracked.

## One-time safety check (worth running now)

Confirm none of your secrets are tracked by git — this both protects privacy and prevents
future pull conflicts:

```bash
git check-ignore docker-compose.yml config/mindvault.config.local.json .env
# each configured file should be printed back; anything NOT printed is tracked — move it
git ls-files | grep -Ei 'compose\.yml$|\.local\.json$|\.env$|token' || echo "clean: no local files are tracked"
```

If `git ls-files` lists your real `docker-compose.yml` or a token file, it was committed
before the ignore rules existed. Untrack it (keeps the file, removes it from git) and
recommit:

```bash
git rm --cached docker-compose.yml
git commit -m "stop tracking local compose file"
```
