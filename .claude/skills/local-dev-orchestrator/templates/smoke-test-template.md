# Smoke Test Template

Run these checks after starting the local dev stack to verify everything is working.

## Infrastructure

| # | Service | Check | Command | Expected Result |
|---|---------|-------|---------|----------------|
| 1 | Database | Port reachable | `pg_isready -h localhost -p 5432` | "accepting connections" |
| 2 | Cache | Port reachable | `redis-cli ping` | "PONG" |
| 3 | Message broker | Management UI | `curl http://localhost:15672` | HTTP 200 |

## Application

| # | Service | Check | Command | Expected Result |
|---|---------|-------|---------|----------------|
| 1 | API | Health endpoint | `curl http://localhost:5000/health` | HTTP 200 with "Healthy" |
| 2 | API | Swagger UI | `curl http://localhost:5000/swagger` | HTTP 200 |
| 3 | Frontend | Home page | `curl http://localhost:3000` | HTTP 200 |

## Data

| # | Check | Command | Expected Result |
|---|-------|---------|----------------|
| 1 | Database has tables | Connect and list tables | Expected tables exist |
| 2 | Seed data present | Query a known seed record | Record exists |
| 3 | Migrations current | `dotnet ef migrations list` | No pending migrations |

## Integration

| # | Check | Method | Expected Result |
|---|-------|--------|----------------|
| 1 | Frontend reaches API | Open app, check network tab | API calls succeed |
| 2 | API reaches database | Call an endpoint that queries data | Data returned |
| 3 | Auth flow works | Log in with test credentials | Token issued |

## If Something Fails

1. Check Docker containers are running: `docker compose ps`
2. Check logs: `docker compose logs [service-name]`
3. Check port availability: `netstat -an | grep [port]`
4. Check environment variables are set correctly
5. Try a full reset: stop everything, `docker compose down -v`, restart
