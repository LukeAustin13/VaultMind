---
name: git-pr-assistant
description: Use this skill when you need help with branch naming, commit messages, PR descriptions, diff summaries, review responses, or changelog entries. The git-pr-assistant produces clean, professional GitHub-style output that follows conventional patterns. It does not review code (use code-reviewer) or manage deployment (use devops-deploy).
license: MIT
metadata:
  stack: agnostic
  version: 1.2
  last-reviewed: 2026-06-29
---

# Git / PR Assistant

## Use When
- The user needs a branch name for a feature, fix, or task.
- A commit message needs to be written or improved.
- A PR description needs to be drafted.
- A diff needs to be summarised for a PR or changelog.
- The user needs to respond to PR review comments.
- A changelog entry is needed for a merged PR. (Release notes for a tagged release cycle are owned by **release-manager**.)

## Do Not Use When
- You are reviewing the code in a PR — use **code-reviewer**.
- You are creating or managing the deployment pipeline — use **devops-deploy**.
- You are planning the work itself — use **planner**.

## Inputs To Look For
- The diff or list of changes (files changed, lines changed).
- The purpose of the change (feature, bugfix, refactor, chore).
- The ticket or issue number (if applicable).
- The target branch.
- Existing commit message and PR conventions in the repo (check recent commits and PRs).
- Review comments that need a response.

## Process
1. **Check existing conventions.** Run `git log --oneline -15` (and `gh pr list --limit 5 --json title` when a PR is involved) and cite the convention observed before producing output. Match the style.
2. **Understand the change.** Read the diff or change description. Identify what changed and why.
3. **Produce the requested output.** Follow the format rules below.

### Branch Naming
- Format: `type/short-description` or `type/ticket-number-short-description`
- Types: `feature`, `fix`, `refactor`, `chore`, `docs`, `test`
- Use kebab-case. Keep it under 50 characters.
- Examples: `feature/order-export`, `fix/null-ref-in-checkout`, `refactor/extract-validation`

### Commit Messages
- First line: imperative mood, under 72 characters, no period.
- Blank line, then body if needed (wrap at 72 characters).
- Body explains **why**, not **what** (the diff shows what).
- Reference issue numbers at the end: `Fixes #123` or `Relates to #456`.
- Do not add AI attribution (`Co-Authored-By: Claude` or similar) to commit messages, branch names, or PR descriptions unless the repository's convention explicitly requires it.
- Examples:
  ```
  Add order export to CSV

  Customers need to export their order history for accounting.
  Uses CsvHelper for generation and streams the response to
  avoid loading all orders into memory.

  Fixes #234
  ```

### PR Descriptions
- Structure:
  ```markdown
  ## Summary
  [1-3 sentences: what this PR does and why]

  ## Changes
  - [Bullet list of key changes]

  ## Testing
  - [How this was tested]

  ## Notes
  - [Anything reviewers should know]
  ```

### Diff Summaries
- Group changes by area (API, database, UI, config, tests).
- Lead with the most important change.
- Use past tense ("Added", "Fixed", "Removed").

### Review Responses
- Be factual and concise.
- If you agree with the feedback: state the fix.
- If you disagree: explain why with evidence, not opinion.
- If it is a trade-off: acknowledge both sides and state the choice.

### Changelog Entries
- Format: `- [Type] Description (#PR-number)`
- Types: Added, Changed, Fixed, Removed, Deprecated, Security
- Write for end users or downstream developers, not for the team.

## Posting a Review via gh (Pending-Review Pattern)

When posting review comments to a real GitHub PR, never fire comments straight to the PR. Stage everything locally, present it to the user, and require explicit approval before anything is posted.

1. **Stage all comments locally first.** Collect every comment, its file path, and line number into a single staged list. Do not post any of them yet.
2. **Present the full set to the user.** Show the complete list — file, line, and comment text — and ask for explicit approval to post. Wait for a clear yes.
3. **Use the gh pending-review flow.** Once approved, create a pending review, attach all comments to it, then submit it as one batch. Do not fire individual comments one at a time.

   ```bash
   # Create a pending review (no event = stays pending)
   gh api repos/{owner}/{repo}/pulls/{number}/reviews \
     --method POST \
     -f commit_id="$COMMIT_SHA" \
     -f body="Review summary"

   # Add a comment to the pending review
   gh api repos/{owner}/{repo}/pulls/{number}/reviews/{review_id}/comments \
     --method POST \
     -f path="src/Orders/OrderService.cs" \
     -F line=42 \
     -f body="Suggestion text here"

   # Submit the staged review as one batch
   gh api repos/{owner}/{repo}/pulls/{number}/reviews/{review_id}/events \
     --method POST \
     -f event="COMMENT"
   ```

4. **Format code suggestions correctly.** A suggestion block must use the ```` ```suggestion ```` fence and contain only the replacement lines for the commented range:

   ````markdown
   ```suggestion
   var total = items.Sum(i => i.Price);
   ```
   ````

### Red Flags
These are the agent's own rationalisations for skipping the approval gate. If you catch yourself thinking any of them, stop and present the staged comments instead:
- "It's just a small comment, posting it directly is fine."
- "The user probably wants this posted anyway."
- "It's only one comment, the batch flow is overkill."
- "I'll post it now and let them delete it if they disagree."

Never post to a public PR without the explicit approval gate. The user must see the staged comments and say yes before anything reaches GitHub.

## Output Format

Depends on the request. Each type has its own format defined above. Always produce the specific artifact requested — do not wrap it in extra explanation.

## Quality Bar
- Output matches the conventions found in the repository.
- Commit messages use imperative mood and explain why.
- PR descriptions include summary, changes, and testing.
- Branch names are concise and follow the type/description pattern.
- No filler text or motivational language.

## Failure Modes To Avoid
- Writing commit messages that describe what changed instead of why.
- Creating PR descriptions that are longer than the code change.
- Using inconsistent naming when the repo has a clear convention.
- Including implementation details in changelog entries meant for users.
- Writing defensive or argumentative PR review responses.
- Forgetting to reference issue numbers when they exist.
- Adding AI co-author lines or attribution the repository convention does not ask for.
