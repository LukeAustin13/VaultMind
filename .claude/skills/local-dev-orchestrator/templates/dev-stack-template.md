# Local Dev Stack Template

Use this template to document a project's local development stack. Fill in the sections for your project.

## Services

| # | Service | Type | Port | Startup Command | Health Check | Depends On |
|---|---------|------|------|----------------|--------------|------------|
| 1 | | Database | | | | — |
| 2 | | API | | | | |
| 3 | | Frontend | | | | |
| 4 | | Cache | | | | — |

## Environment Variables

| Variable | Service | Required | Source | Example |
|----------|---------|----------|--------|---------|
| | | | | |

## Prerequisites

- [ ] Docker Desktop running
- [ ] .NET SDK [version]
- [ ] Node.js [version] (if frontend)
- [ ] Ports [list] available

## Startup Sequence

1. Start infrastructure: `docker compose up -d`
2. Wait for database: [health check command]
3. Run migrations: `dotnet ef database update --project [project]`
4. Start API: `dotnet run --project [project]`
5. Start frontend: `cd [frontend-dir] && npm run dev`
6. Verify: [health check URLs]

## Known Failure Points

| Problem | Symptom | Fix |
|---------|---------|-----|
| Port conflict | Container exits immediately | Stop conflicting process |
| Missing .env | App crashes on startup | Copy `.env.example` to `.env` |
| Stale containers | Old schema, wrong config | `docker compose down -v && docker compose up -d` |
| Missing migrations | EF error on startup | `dotnet ef database update` |

## Shutdown

```bash
# Stop application processes (Ctrl+C)
# Stop infrastructure
docker compose down
# To also remove volumes (full reset):
docker compose down -v
```
