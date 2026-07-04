---
name: security-reviewer
description: Use this skill when you need to review code, configuration, infrastructure, APIs, or workflows for security vulnerabilities — "security review this", "is this safe to expose?", "check this for vulnerabilities". The security-reviewer states the threat model, checks for secrets exposure, auth flaws, injection risks, unsafe defaults, exposed services, dependency vulnerabilities, and sensitive-data logging, verifies exploitability of critical findings by tracing the input path, and delivers a prioritised report with a GREEN/AMBER/RED deploy verdict. It does not fix bugs (use bug-hunter) or review general code quality (use code-reviewer).
license: MIT
metadata:
  stack: agnostic
  version: 2.0
  last-reviewed: 2026-07-03
---

# Security Reviewer

## Use When
- The user asks for a security review of code, config, or infrastructure.
- You are reviewing code that handles authentication, authorisation, or user input.
- You notice potential security issues during another task.
- New dependencies are being added and need vetting.
- Configuration files expose ports, secrets, or services.
- An API accepts user input that could be used for injection.

## Do Not Use When
- You are reviewing general code quality — use **code-reviewer**.
- You are debugging a functional bug — use **bug-hunter**.
- You are assessing performance — use **performance-profiler**.
- You are configuring deployment infrastructure — use **devops-deploy** (though flag security issues you find).

## Skill vs Agent

- Use this skill for a **thorough standalone security pass** across source code, config, infrastructure, and dependencies — produces a prioritised findings report.
- Use the **`security-config-reviewer` agent** for a **focused PR-pass** with restricted read-only tools — faster, narrower, suitable as one pass inside a PR review workflow.

## Inputs To Look For
- Source code, especially: controllers, middleware, auth handlers, input parsing, SQL queries, file operations.
- Configuration files: `appsettings.json`, `.env`, `docker-compose.yml`, CI/CD configs.
- Dependency manifests: `.csproj` (NuGet), `package.json`, `requirements.txt`.
- Infrastructure config: Dockerfiles, nginx configs, firewall rules.
- API endpoint definitions and route handlers.

## Process
1. **Define the review scope and threat model.** What are you reviewing (code, config, infra, API, all?) — and who can reach it: public internet, authenticated users, internal network, or build-time only? The same flaw is Critical on a public endpoint and Low in a build script; every severity call in this review depends on this line, so state it explicitly before any finding.
2. **Check for secrets exposure.**
   - Hardcoded passwords, API keys, connection strings in source code.
   - Secrets in config files committed to version control.
   - Secrets in environment variables that are logged or exposed.
3. **Check authentication.**
   - Are auth tokens validated on every protected endpoint?
   - Is token expiry enforced?
   - Are passwords hashed with a strong algorithm (bcrypt, Argon2)?
   - Is there protection against brute force (rate limiting, lockout)?
4. **Check authorisation.**
   - Can users access resources they do not own?
   - Are role/permission checks applied consistently?
   - Is there IDOR (Insecure Direct Object Reference) risk?
5. **Check injection risks.**
   - SQL injection: parameterised queries or ORM?
   - XSS: output encoding in views?
   - Command injection: user input in shell commands?
   - Path traversal: user input in file paths?
6. **Check unsafe defaults.**
   - CORS: overly permissive (`*`)?
   - Debug mode enabled in production config?
   - Default credentials in config?
   - Verbose error messages exposing internals?
7. **Check dependencies.**
   - Known vulnerabilities in package versions?
   - Unmaintained or abandoned packages?
8. **Check logging.**
   - Is sensitive data (passwords, tokens, PII) being logged?
   - Are logs accessible to unauthorised users?
9. **Verify exploitability of Critical/High candidates.** Before a finding lands in the report at Critical or High, trace the exploit path: is the input actually attacker-controlled (follow it from the entry point, not from the sink)? Is the endpoint reachable under the stated threat model? Does an existing mitigation — framework encoding, middleware, parameterisation upstream, network boundary — already block it? A traced path is **CONFIRMED**; a plausible finding you could not fully trace is **SUSPECTED**, with the missing check named; a refuted candidate is dropped and counted. Theoretical vulnerabilities reported as Critical are how security reviews lose their audience.
10. **Prioritise and verdict.** Rank by severity and exploitability, then issue the deploy verdict: **GREEN** (nothing above Medium — deploy, improvements listed), **AMBER** (High findings — fix the named items before deploy), **RED** (a CONFIRMED Critical — do not deploy until resolved).

For a runnable, category-grouped pass with concrete checks, see [checklists/security-review-checklist.md](checklists/security-review-checklist.md).

## Verified-Remediation Loop

This skill is review-first: by default it reports findings and stops. Enter this loop only when the user explicitly asks you to **fix** the findings, not just report them. Keep the loop scoped to vulnerability remediation — **bug-hunter** owns general runtime bugs, so do not fold unrelated functional fixes into a security pass.

For each finding you are asked to fix:

1. **Apply the fix** for that one finding, using the remediation already recorded in the findings table.
2. **Re-check it.** Re-run the specific check that produced the finding — re-scan the file or path, re-run the dependency audit, re-run the targeted query/grep, or re-run the relevant test. Do not assume the edit worked; confirm from fresh output.
3. **Confirm no regression.** Verify the fix did not introduce a new issue (a broken build, a new finding, or a behaviour change). If a build or test command exists, run it.
4. **Roll back if it regresses.** If the re-check shows the finding is not resolved or a regression appeared, revert that edit and either try the next remediation approach or leave the finding open with a note. Cap retries at roughly 3 iterations per finding; if still unresolved, stop and report it as open with what you tried.

When fixing multiple findings in the **same file**, order the edits **bottom-to-top** (highest line number first) so that applying an earlier edit does not shift the line numbers of findings you have not yet touched.

Report each finding as `fixed (re-checked)`, `rolled back`, or `still open` so the result of every edit is explicit.

## Output Format

### Security Review: [Scope]

**Review Date:** [Date]
**Scope:** [What was reviewed]
**Threat model:** [Public internet / authenticated users / internal network / build-time only — and any trust assumptions]
**Verdict:** GREEN / AMBER / RED — [one sentence]
**Verification:** [N] Critical/High candidates traced, [N] confirmed, [N] suspected, [N] refuted and dropped

#### Findings

| # | Severity | Status | Category | Location | Finding | Remediation |
|---|----------|--------|----------|----------|---------|-------------|
| 1 | Critical | CONFIRMED | Injection | `OrderController.cs:45` | Raw SQL concatenates `search` param — traced from public endpoint to query with no sanitisation | Use parameterised query |
| 2 | High | CONFIRMED | Secrets | `appsettings.json:12` | Database password in source, file is committed | Move to user secrets or env var |
| 3 | Medium | — | Auth | `Startup.cs:30` | No rate limiting on login | Add rate limiting middleware |
| 4 | Low | — | Logging | `UserService.cs:88` | Email logged at Info level | Reduce to Debug or remove |

Status applies to Critical/High: CONFIRMED = exploit path traced end to end; SUSPECTED = plausible, with the confirming check named in the finding. Medium/Low are not individually traced.

**Severity definitions:**
- **Critical:** Exploitable vulnerability that can lead to data breach, RCE, or privilege escalation.
- **High:** Significant security weakness that should be fixed before deployment.
- **Medium:** Security improvement that reduces attack surface.
- **Low:** Defence-in-depth improvement or best practice.

#### Positive Practices
- [Security practices done well in this codebase]

#### Recommendations
1. [Highest priority action]
2. [Second priority action]

## Quality Bar
- The threat model is stated before any finding, and every severity is graded against it.
- Every Critical/High finding is CONFIRMED via a traced exploit path, or SUSPECTED with the specific confirming check named; the refuted count is reported.
- The review ends in a GREEN/AMBER/RED deploy verdict justified by the verified findings.
- Every finding has a specific location (file and line).
- Every finding has a concrete remediation, not just "fix this".
- Findings are prioritised by severity and exploitability.
- The review covers all applicable categories (secrets, auth, injection, etc.).
- False positives are avoided — only flag things that are actually risky.
- Good security practices are acknowledged.

## Failure Modes To Avoid
- Flagging theoretical vulnerabilities that are not exploitable in context — a sink-side pattern match (string concatenation near SQL) is a candidate, not a finding, until the input is traced to an attacker-controlled source.
- Grading severity without a threat model — Critical on an internal build script and Critical on a public endpoint are different claims.
- Missing actual vulnerabilities while focusing on low-severity style issues.
- Recommending security measures that break functionality without alternatives.
- Ignoring the threat model (an internal tool has different risks than a public API).
- Reporting the same finding multiple times for every occurrence instead of once with "applies to all X".
- Failing to check dependencies — known CVEs are low-hanging fruit.
