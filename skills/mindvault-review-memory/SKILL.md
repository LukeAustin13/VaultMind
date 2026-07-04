---
name: mindvault-review-memory
description: Store durable code review, architecture review, risk, and technical debt findings in the MindVault vault. Use after completing a review or audit whose findings should outlive the session - recurring risks, structural debt, security concerns - not for one-off nitpicks that get fixed immediately.
---

# MindVault: Review Memory

Persist review findings that matter beyond this session.

## Trigger conditions

Use after completing a review or audit whose findings should outlive the session:
recurring risks, structural debt, security concerns, architecture verdicts.

Do NOT use for one-off nitpicks that get fixed immediately, style comments, or findings the
user has already dismissed.

## Required workflow

1. Identify the project and load `mindvault_get_project_context` — its `openRisks` and
   `warnings` are your first duplicate check.
2. Search for existing coverage before writing anything:
   - `mindvault_list_notes` with `type: "risk"` / `type: "review"` and the project filter,
   - `mindvault_search` for the finding's key phrase (add `explain: true` if results look off).
   A finding that is already recorded gets an update (append to the existing note), not a twin.
3. Record the findings, structured like this:

   ```markdown
   ### Review 2026-07-04 — <scope, e.g. "auth module">

   **Summary** — one paragraph.
   **Critical issues** — must fix; each with file/component and why it matters.
   **Important improvements** — should fix soon.
   **Minor cleanup** — nice to have.
   **Risks** — things that could bite later, with trigger conditions.
   **Recommended next actions** — ordered, concrete.
   ```

   Write it into the vault with `mindvault_append_to_note`:
   - project note, section `"Risks"` for risk-centric findings, or
   - a dedicated section appended with `createSection: true`
     (e.g. section `"Reviews"`) when the findings are broader.
4. Convert findings into tracked items where warranted — always via
   `mindvault_check_draft` first (it catches duplicates of already-tracked findings):
   - **Critical issues / next actions** → `mindvault_create_task` (see `mindvault-task-sync`
     for the bar and statuses).
   - A review that overturned an architectural choice → `mindvault_create_decision`, then
     `mindvault_supersede_decision` to retire the old decision cleanly.
   - Connect related notes with `mindvault_link_notes`.
5. If a previously recorded risk is now resolved, update its status via
   `mindvault_update_frontmatter` instead of re-describing it.

Expected final behaviour: findings stored once, skimmable at a glance in six months, with
critical items escalated into tracked tasks/decisions.

## Do not

- Do not store low-confidence guesses as facts — label them as open questions in the
  Summary, or leave them out.
- Do not pad empty sections; omit them.
- Do not re-describe an existing risk — update its note or status.
- Do not escalate every finding into a task; only critical issues and real next actions.

## Efficiency rules

- One duplicate-check pass (context + one or two searches), one structured append,
  a handful of escalations — not a call per sentence.
- Keep it skimmable; the value is in six months, at a glance.

## Safety rules

- Use only the `mindvault_*` MCP tools — never write vault files directly or via shell.
- Facts only: findings you verified in the code.
- Retiring an overturned decision goes through `mindvault_supersede_decision`, never a
  hand-flipped status.
