# Content Risk Scanner

Every durable write passes a deterministic content gate: high-confidence secrets are
blocked, prompt-injection-style language is warned about, and matched values are never
echoed anywhere.

## What it scans

The gate runs on content entering the vault through: `append`, `update-frontmatter`,
thought capture (`inbox add` / `mindvault_capture_thought`), `mistake add`, and session
checkpoint/handoff (which write via append). Creates from pure templates (project/
decision/task skeletons) carry no user body and are not scanned.

## Patterns

**Block** (write refused with `RISKY_CONTENT` unless overridden):
private key blocks (`-----BEGIN … PRIVATE KEY-----`), AWS access keys (`AKIA…`), GitHub
tokens (`ghp_…`, `github_pat_…`), `sk-…` API keys, bearer tokens.

**Warn** (write proceeds; structured warnings attached):
credential-looking assignments (`password=…`, `api_key: …`), "ignore previous
instructions" variants, system-prompt probing, exfiltration language, secret solicitation.

## Override and evidence

```bash
mindvault append --note X --section Notes --content "..." --allow-risky-content
```

MCP: `allowRiskyContent: true` on the affected tools; results carry `riskWarnings`.

Evidence is always redacted: findings report `private-key-block (1650 chars at offset 42;
value redacted)` — the pattern name, length and position, **never the matched text**. The
value cannot leak through error messages, tool results or logs.

## Design limits (honest)

- Regex-shaped secrets only. High-entropy strings without a known shape pass silently —
  the gate is a seatbelt, not a DLP product.
- Injection language is warn-only by design: quoting a phrase like "ignore previous
  instructions" in a legitimate note (this file does it) must not block writes.
- The scan runs on inbound content, not retroactively; `validate` does not rescan the
  vault. Secrets already in notes need human cleanup.
