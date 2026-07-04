---
name: devops-deploy
description: Use this skill when you need to design, configure, or review Docker, docker-compose, CI/CD pipelines, environment variables, deployment configuration, ports, reverse proxies, or server setup. Operates in two modes — Design (build or modify infrastructure) and Review (assess a change for a rubric-based risk verdict). Destructive commands are always flagged with their impact and require explicit confirmation before being recommended. It does not decide what to log, trace, or health-check (use observability-designer), start a local dev stack (use local-dev-orchestrator), or triage a failed CI run (use ci-triage).
license: MIT
metadata:
  stack: agnostic
  version: 1.2
  last-reviewed: 2026-07-03
---

# DevOps / Deploy

## Use When
- Setting up Docker or docker-compose for a project.
- Configuring CI/CD pipelines (GitHub Actions, Azure DevOps, etc.).
- Managing environment variables, secrets, and configuration across environments.
- Setting up reverse proxies (nginx, Caddy, Traefik).
- Wiring health-check endpoints, log shipping, or monitoring agents into Docker/CI/proxy config (deciding WHAT to log or check is **observability-designer**).
- Deploying to a server, cloud service, or container registry.
- Troubleshooting deployment failures, port conflicts, or networking issues.
- Reviewing infrastructure or deployment changes before they are applied.

## Do Not Use When
- You are designing the application itself — use **backend-architect**.
- You are reviewing application code — use **code-reviewer**.
- You are debugging application logic — use **bug-hunter**.
- You are triaging a CI failure — use **ci-triage**.
- You are assessing security posture — use **security-reviewer** (though this skill will flag obvious security issues in configs).

## Mode

State the mode at the start of your output.

- **Design** — building or modifying deployment infrastructure. Use the Design Process below.
- **Review** — assessing a proposed change for risk before it is applied. Use the Review Process below.

If the intent is ambiguous, ask: "Are you setting something up or reviewing an existing change?"

## Inputs To Look For
- Existing `Dockerfile`, `docker-compose.yml`, CI/CD config files.
- `appsettings.json`, `.env` files, environment variable documentation.
- Deployment target (Linux server, Azure, AWS, local Docker).
- Port assignments and networking requirements.
- Current infrastructure state (running containers, services, DNS).
- Error logs from failed deployments or builds.

---

## Design Process

1. **Understand the target.** Where is this being deployed? What infrastructure exists?
2. **Review existing config.** Read all deployment-related files before suggesting changes.
3. **Identify the change needed.** What specific deployment problem are you solving?
4. **Design the solution.** For each file change: what it does, why it is needed, what could go wrong.
5. **Flag destructive operations.** Before any command that stops running services, deletes data or volumes, changes DNS or routing, or modifies production config — explicitly warn the user and ask for confirmation.
6. **Provide rollback plan.** For non-trivial changes, explain how to undo them.
7. **Verify.** Suggest how to confirm the deployment works (health check, curl, logs).

## Design Output Format

### Deployment: [Task Description]

**Target:** [Local Docker / Azure / Linux server / etc.]
**Risk Level:** [Low / Medium / High]

#### Changes

##### `docker-compose.yml`
```yaml
# [Change with comments explaining each section]
```
**What this does:** [One sentence]
**Rollback:** [How to undo]

##### `.github/workflows/deploy.yml`
```yaml
# [Change with comments]
```
**What this does:** [One sentence]

#### Environment Variables
| Variable | Purpose | Where Set | Sensitive |
|----------|---------|-----------|-----------|
| `DATABASE_URL` | DB connection | `.env` / Secret store | Yes |
| `LOG_LEVEL` | Logging verbosity | `appsettings.json` | No |

#### Verification
- [ ] `docker-compose up` starts without errors
- [ ] Health check endpoint returns 200
- [ ] Logs show expected startup messages
- [ ] [Service] is reachable on expected port

#### Destructive Commands (if any)
```bash
# WARNING: This will remove all stopped containers and unused images
docker system prune -a
```
**Impact:** [What this deletes]
**Confirm with user before running.**

---

## Review Process

1. **Identify what changed.** List all infrastructure and deployment files in the diff.
2. **Classify the target environment.** Local / staging / production. Production changes have higher scrutiny.
3. **Check for dangerous operations.** Resource deletion, port exposure changes, permission changes, volume mount changes to sensitive host paths, `:latest` image tag changes, secret or credential exposure.
4. **Verify rollback capability.** Can this change be reversed? What is the rollback procedure?
5. **Check environment variable safety.** Are secrets hardcoded? Are env vars documented? Do dev and prod configs diverge safely?
6. **Check for environment confusion.** Could this change accidentally affect an environment it should not?
7. **Assign the verdict using this rubric.**
   - **Critical:** possible data loss or production secret exposure.
   - **High:** irreversible without a backup, or a production routing/permission change.
   - **Medium:** reversible but affects a shared environment.
   - **Low:** local/dev only, fully reversible.
   Every finding in the Dangerous Operations table must quote the file and line from the diff — a risk label without a quoted operation is an opinion, not a review.

See `checklists/deployment-risk-checklist.md` and `checklists/infrastructure-change-review.md` for full item lists.

## Review Output Format

### Deployment Risk Review: [Change Description]

**Environment:** Local / Staging / Production / Multiple
**Risk Level:** Low / Medium / High / Critical
**Rollback Possible:** Yes / Partial / No

#### Dangerous Operations

| # | File | Operation | Risk | Mitigation |
|---|------|-----------|------|------------|
| 1 | `docker-compose.yml` | Removed `db` volume mount | Data loss on container rebuild | Add named volume |
| 2 | `deploy.yml` | Changed image tag to `:latest` | Non-deterministic deploys | Pin to specific version |

#### Secret / Credential Review

| # | File | Finding | Severity |
|---|------|---------|----------|

#### Environment Variable Changes

| Variable | Before | After | Impact |
|----------|--------|-------|--------|

#### Rollback Plan

1. [Step to reverse the change]
2. [Verification after rollback]

#### Verification Checks

- [ ] [How to verify the deployment worked correctly]

---

## Quality Bar
- Mode is stated at the start of output.
- Every config change has an explanation of what it does.
- Destructive commands are flagged and require confirmation.
- Environment variables are documented with sensitivity marked.
- A verification checklist is included.
- Rollback steps exist for non-trivial changes.
- Ports, volumes, and network names do not conflict with existing services.
- For Review mode: every infrastructure file change is reviewed, not just Dockerfiles.

## Failure Modes To Avoid
- Running destructive commands (`docker system prune`, `rm -rf`, `DROP DATABASE`) without warning.
- Hardcoding secrets in config files or Dockerfiles.
- Exposing internal services to the public network.
- Forgetting to document environment variables.
- Using `latest` tags in production Dockerfiles.
- Assuming the user's environment matches your assumptions — verify first.
- Creating overly complex CI/CD pipelines for simple projects.
- (Review mode) Approving a change that deletes a production database volume.
- (Review mode) Reviewing only the Dockerfile and missing Compose or CI changes.
- (Review mode) Treating all changes as equal risk regardless of target environment.

## References

- `checklists/deployment-risk-checklist.md` — pre-deployment safety checklist for Review mode.
- `checklists/infrastructure-change-review.md` — infrastructure change review checklist for Review mode.
