---
name: mindvault-route-card
description: Get a token-budgeted navigation brief before reading anything — the 3-5 notes that matter, what to skip, and why. Use at the start of any task in a repo with MindVault, before any broad search, or when unsure which notes are worth reading.
---

# MindVault: Route Card

Ask the brain for a route before spending tokens on reads. The route card says what to
read first, what can wait, what NOT to read, and what rules are already in force. It is a
mid-session navigation tool — `mindvault_start_session` already returns a read-first list for
the start of a session, so reach for a route card when the work moves to a new focus.

## Trigger conditions

Use when:
- Mid-session, the work shifts to a new goal, bug or file and you want a fresh route before
  any vault search.
- A goal, bug or file is known and you want only the memory that governs it.
- You caught yourself about to run a second or third `mindvault_search`.
- Starting work in a repo with MindVault configured but no session brief in hand.

Do NOT use for: recording anything (route cards are read-only), or when this session
already has a fresh route for the same goal.

## Required workflow

1. One call: `mindvault_build_route_card` with the project and ONE focus — `goal`,
   `currentFile` or `query`. No focus = general orientation.
2. Read ONLY the `readFirst` notes (already capped at 5, reasons and token estimates
   attached). Use each note's `summarySnippet` to decide if the full read is even needed;
   scope reads with `mindvault_read_note` `section`/`maxChars`.
3. Respect `doNotRead` — those notes are flagged archived/superseded/hidden/stale with
   reasons. Do not read them, do not resurface them.
4. Treat `activeConstraints` and the mistakes list as binding before writing any code.
5. Stop reading the moment the goal, constraints and risks are clear. `readIfNeeded`
   exists for genuine gaps only.

Expected final behaviour: 1 route call + at most 5 scoped reads replaces a search sweep,
and the do-not-read list kept dead notes out of context.

## Do not

- Do not fall back to broad `mindvault_search` before the route card — the card includes
  a narrowed search suggestion for when reads genuinely leave gaps.
- Do not read `doNotRead` notes to "double-check" — the reasons are the check.
- Do not ignore `tokenBudget`: if reads exceed it, you chose the wrong reads.
- Do not re-request routes for the same focus in one session.

## Efficiency rules

- One route card ≈ the triage of 3 searches plus 10 speculative reads, for a fraction of
  the tokens.
- `estimatedTokens` per note is the read price tag — prefer the cheap note when two cover
  the same ground.
- Summaries first, bodies second: a `summarySnippet` that answers the question ends the
  read chain right there.
- If you fall back to the card's narrowed `mindvault_search`, pass `snippetChars: 0` for
  refs-only hits, then scope the read with `mindvault_read_note`'s `section` / `maxChars`.

## Safety rules

- Use only the `mindvault_*` MCP tools — never read vault files directly or via shell.
- Route cards are read-only; they never move, edit or delete notes.
- Ambiguous project names return candidates — pick one explicitly, never guess for the
  user.
