# Superpower Pass — Final Audit

Written at the end of the Superpower Pass (v0.2.0, 2026-07-04). Supersedes
[FINAL_SELF_AUDIT.md](FINAL_SELF_AUDIT.md) as the current honest assessment. State at
writing: build clean (0 warnings), **249/249 tests green**, benchmarks run at 100/1k/10k
notes on this desktop ([PERFORMANCE_RESULTS.md](PERFORMANCE_RESULTS.md)). Docker has still
**never been executed by a human in this environment** — see §7.

## 1. What is now excellent

- **Mutation safety** — vault jail → write lock → snapshot-first → atomic write → YAML
  verify/rollback → reindex, with a consolidated torture suite asserting the exact
  pre-mutation content lands in the snapshot and that supersede rolls back on partial
  failure. This chain is the strongest part of the product.
- **Measured performance** — no more "should be fast": 10.8 ms ranked search and 197 ms
  incremental scan at 9,110 notes, mutations ~10–15 ms at every size, with an order of
  magnitude of headroom against targets on desktop hardware.
- **Retrieval accountability** — ranking behaviour is pinned by 11 eval cases that assert
  ORDER, not existence; `--explain` gives evidence for future weight tuning, and the evals
  are the regression net for it.
- **Agent behaviour by construction** — outputs bounded (pack < 6 KB, read truncation,
  search clamp), draft checks before creation, 21 pinned tools with no unsafe surface,
  skills with enforced Trigger/Workflow/Do-not/Efficiency/Safety sections. All test-backed.
- **Diagnosability** — stable error codes everywhere, `doctor` that checks the things that
  actually go wrong (writability, placeholder paths, Docker mount, MCP env), `index verify`
  for cache drift, secrets-free MCP health/diagnostics for agent self-checks.
- **Cross-process honesty** — two MindVault instances can no longer silently interleave
  writes; the lock fails fast, recovers from crashes by staleness, and never blocks reads.

## 2. What is still not perfect

- Near-duplicate detection is title-token Jaccard; semantically identical notes with
  disjoint wording pass the draft check.
- Matched-section attribution uses the first highlighted term; multi-topic notes can
  report an adjacent section.
- The decision graph resolves edges only within the queried scope; cross-project
  supersedes render as edge-less nodes.
- `context` human output is functional, not beautiful; JSON/markdown pack output is the
  polished path.
- The write lock is best-effort across sync boundaries (lock file sync lag) — solid on one
  filesystem, advisory across machines. The one-authoritative-writer rule remains the real
  protection.
- Doctor's "edited example config" detection walks up from the current directory only — it
  helps in the repo, not from arbitrary install locations.

## 3. What could still corrupt data

Ranked by realism, with the mitigation that keeps each survivable:

1. **Obsidian and an agent editing the same note in the same seconds** — last writer wins.
   Survivable: the losing content is in the pre-write snapshot (`restore`).
2. **A sync engine propagating a half-pair of a two-note operation** (supersede) — each
   file is internally consistent; the pair can be momentarily contradictory on another
   machine. Survivable + detectable: `validate` flags `superseded-status-mismatch`.
3. **Crash between archive's status-write and its file move** — note archived-in-place.
   Survivable: snapshot + reported honestly; re-run archive.
4. **`prune`** deletes old snapshots irreversibly — that is its purpose, but it removes
   the safety net for anything older than the retention window. External backups
   (`backup` + your own) remain non-optional.
5. **Snapshots live inside the vault** — a catastrophic vault-folder loss loses notes and
   snapshots together. Only off-vault backups cover that.

Nothing in this list is silent, and nothing in the normal path is unrecoverable.

## 4. What could still cause noisy memories

- Draft checks warn but do not block: a determined agent can still create low-value notes.
  The damping is real (duplicates, vagueness, near-dupes surfaced) but consent-based.
- `session log` has no rate limit; the skills say "prefer none", nothing enforces it.
- Archive requires no confirmation at the tool level; the hygiene skill demands per-note
  user approval, but a misbehaving client could archive aggressively (reversibly).
- Nothing ages out stale research/resource notes automatically — validation surfaces stale
  *tasks* only. Periodic human review (OP_WORKFLOW weekly pass) is the mechanism.

## 5. What could still make retrieval weak

- Lexical-only matching: a query in different vocabulary than the note ("auth" vs "login
  security") can miss entirely. Porter stemming narrows, doesn't close, this gap. The
  agreed response is eval-first weight tuning, embeddings only if real usage proves lexical
  insufficient.
- bm25 + boosts were tuned on small fixtures; real-vault tuning evidence (via `--explain`)
  hasn't accumulated yet.
- FTS syntax edge cases fall back to quoted-phrase search — safe but can under-return for
  queries full of operators.
- Very large single notes dilute bm25 and section attribution; `large-note` info in
  validation is the nudge, splitting is manual.

## 6. What has NOT been manually tested

- **Docker: anything at all.** No image build, no container run, no compose up has been
  executed in this environment (no Docker installed). CI buildx (amd64+arm64) validates
  that the image *builds*, nothing more.
- The HTTP MCP transport under a real Claude Code client on a second machine (the stdio
  transport IS exercised end-to-end by tests; HTTP auth/401 has unit-level coverage only).
- Behaviour on a real, years-old, messy human vault (fixtures + synthetic vaults only).
- Syncthing/Dropbox actually producing conflict files around MindVault writes (the
  patterns are tested against fabricated files).
- Pi-class hardware performance (benchmarks are desktop-only; targets carry ~10× headroom
  as the budget for it).

## 7. What Docker/Pi validation remains

Run on the Pi, in order — this is the exact checklist:

```bash
# 0. prerequisites: Docker + compose plugin installed; vault synced to the Pi
git clone <repo> && cd MindVault
cp docker-compose.example.yml docker-compose.yml   # edit: vault path, auth token, user:
docker compose up -d --build                        # 1. ARM64 image builds natively
docker compose run --rm mindvault version           # 2. prints 0.2.0
docker compose run --rm mindvault doctor            # 3. vault writable, /vault mounted, no warnings
docker compose run --rm mindvault init
docker compose run --rm mindvault scan
docker compose run --rm mindvault index verify      # 4. ok
docker compose run --rm mindvault session start --project "<p>" --task "pi smoke"
docker compose run --rm mindvault search "anything" --explain
curl http://127.0.0.1:7777/healthz                  # 5. "ok"
curl -i -X POST http://127.0.0.1:7777/              # 6. 401 with MCP_AUTH_FAILED
# 7. connect Claude Code from the LAN with the bearer token; run a skill test prompt
# 8. run the benchmarks on the Pi and add a section to PERFORMANCE_RESULTS.md:
docker compose run --rm --entrypoint dotnet mindvault --help  # or run benchmarks from a checkout
```

Also verify on the Pi: mount ownership under the `user:` override, snapshots appearing on
the mounted volume, and `validate` showing no `vault-unwritable`/`snapshot-unwritable`.

## 8. Before trusting it with the real vault

1. **Back the vault up externally first.** Non-negotiable.
2. Run the Pi checklist (§7) or, until then, run MindVault locally against a **copy** of
   the real vault for a week: `scan`, `validate`, `doctor`, `index verify`, a few sessions.
3. Review what `validate` says about the real vault (it will find things — old vaults
   always have broken links and duplicates) and fix criticals before letting agents write.
4. Only then point the authoritative writer at the real vault, keep `prune` retention at
   30 days, and keep the weekly OP_WORKFLOW pass.

## 9. Recommended exact next manual validation steps

1. `git add -A` and commit this state (nothing is committed yet — everything is working
   tree only).
2. On this PC: run `mindvault doctor` and `mindvault index verify` against a copy of your
   real vault; read what validation says.
3. Dog-food one real coding session end-to-end (session start → work → decision capture →
   session end) against the copy.
4. When the Pi is available: §7 checklist top to bottom, then add Pi numbers to
   PERFORMANCE_RESULTS.md.
5. After two weeks of real use: revisit ranking weights with `--explain` evidence and add
   an eval case for anything retrieval got wrong.
