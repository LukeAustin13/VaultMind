---
name: local-dev-orchestrator
description: Use this skill when you need to start, verify, or troubleshoot a local development stack including APIs, frontends, databases, Docker Compose services, and supporting infrastructure. When shell access is available it starts the stack and runs each health check, pasting real output; otherwise it produces a startup plan with every unexecuted check marked UNVERIFIED. It does not design infrastructure (use backend-architect) or deploy to remote environments (use devops-deploy).
license: MIT
metadata:
  stack: agnostic
  version: 1.1
  last-reviewed: 2026-07-03
---

# Local Dev Orchestrator

## Purpose

Guide the process of starting and verifying a local development stack. Produce a clear service map with startup commands, health checks, expected ports, and troubleshooting steps for common failures.

## Use When

- The user asks "how do I run this locally?" or "what services do I need?".
- Setting up a new development environment.
- A service is not starting or not reachable locally.
- You need to verify the full stack is healthy before testing.
- The user wants to know what ports, URLs, or environment variables are needed.

## Do Not Use When

- You are deploying to a remote environment — use **devops-deploy**.
- You are designing the infrastructure — use **backend-architect**.
- You are debugging application logic — use **bug-hunter**.
- You are triaging CI failures — use **ci-triage**.

## Inputs To Inspect

- `docker-compose.yml` or `compose.yaml` files.
- `.env` or `appsettings.Development.json` files.
- `launchSettings.json` for .NET projects.
- `package.json` scripts for frontend projects.
- Dockerfile definitions.
- Database connection strings and seed scripts.
- Health check endpoints or readiness probes.
- Port mappings and service dependencies.

## Process

1. **Inventory services.** List every service the project expects to run locally: APIs, frontends, databases, caches, message brokers, etc.
2. **Map dependencies.** Determine startup order based on service dependencies (database before API, API before frontend, etc.).
3. **Identify configuration.** List required environment variables, connection strings, config files, and their expected values or sources.
4. **Determine startup commands.** For each service, identify the correct startup command.
5. **Define health checks.** For each service, identify how to verify it is running (URL, port check, CLI command).
6. **Execute and verify (when you have shell access).** Actually start the stack in dependency order and run each health check yourself; paste each check's real output into the Startup Sequence. Check the preconditions too (`docker info` before assuming Docker is running, port probes before assuming ports are free). A health-check column filled from reading config rather than from a run must be marked **UNVERIFIED** — the table must distinguish "I ran this" from "the config says this".
7. **Identify known failure points.** Derive them from this repo's actual config (the ports it binds, the env vars it requires, the seed scripts it ships) — not from a stock list. Generic issues (Docker not running, port conflicts) belong only if they apply here.
8. **Document shutdown.** How to cleanly stop all services without orphaning processes or containers.

## Output Format

### Local Dev Stack: [Project Name]

**Services:** [Count]
**Startup Order:** [Ordered list]

#### Service Map

| # | Service | Type | Port | Startup Command | Health Check | Depends On |
|---|---------|------|------|----------------|--------------|------------|
| 1 | PostgreSQL | Database | 5432 | `docker compose up -d db` | `pg_isready -h localhost` | — |
| 2 | API | .NET API | 5000 | `dotnet run --project src/Api` | `GET http://localhost:5000/health` | PostgreSQL |
| 3 | Frontend | React | 3000 | `npm run dev` | `GET http://localhost:3000` | API |

#### Environment Variables

| Variable | Service | Required | Source | Example |
|----------|---------|----------|--------|---------|
| `ConnectionStrings__Default` | API | Yes | `appsettings.Development.json` | `Host=localhost;Database=app;...` |

#### Startup Sequence

1. [Command with expected output]
2. [Command with expected output]
3. [Verification step]

#### Known Failure Points

| Problem | Symptom | Fix |
|---------|---------|-----|
| Port 5432 in use | DB container exits immediately | Stop conflicting PostgreSQL instance |
| Missing .env file | API crashes on startup | Copy `.env.example` to `.env` |

#### Shutdown

```
[Clean shutdown commands]
```

## Quality Bar

- Every service is listed with its port, command, and health check.
- Every health check is either executed with its output shown, or explicitly marked UNVERIFIED with the reason it could not run.
- Known failure points cite this repo's actual config, not a stock list.
- Startup order respects dependencies.
- Environment variables are documented with their source.
- Known failure points include symptoms and fixes, not just descriptions.
- Shutdown steps are included and clean.

## Failure Modes To Avoid

- Assuming Docker is running without checking.
- Hardcoding project-specific paths or credentials.
- Missing database seeding or migration steps.
- Ignoring port conflicts with existing local services.
- Providing startup commands without health check verification.
- Forgetting to document how to stop everything cleanly.

## Related Skills And Agents

- **devops-deploy** — for remote deployment and infrastructure change review (Review mode), not local dev.
- **dotnet-quality-gate** — run after the stack is up to validate code.
- **bug-hunter** — when a service starts but behaves incorrectly.
