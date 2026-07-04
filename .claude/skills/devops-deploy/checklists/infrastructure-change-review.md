# Infrastructure Change Review Checklist

Use this checklist during a Review Mode assessment of changes to infrastructure-as-code, Docker, Compose, or cloud configuration.

## Scope

- [ ] All changed infrastructure files identified
- [ ] Change purpose is clear (new service, config update, scaling, fix)
- [ ] Blast radius understood — what could break if this goes wrong

## Docker Changes

- [ ] Dockerfile uses specific base image tags (not `:latest`)
- [ ] `.dockerignore` excludes secrets, source control, and build artifacts
- [ ] No `RUN` commands install unnecessary tools or leave debug utilities
- [ ] Final image does not run as root (where applicable)
- [ ] Build args do not contain secrets
- [ ] Multi-stage build does not copy build-time-only files to final image

## Docker Compose Changes

- [ ] Service dependencies use `depends_on` with health checks
- [ ] Ports are not unnecessarily exposed to host
- [ ] Volumes use named volumes for data that must persist
- [ ] Environment variables reference `.env` or secret store, not inline values
- [ ] Network configuration is intentional

## CI/CD Changes

- [ ] Workflow file syntax is valid
- [ ] New steps use pinned action versions (not `@main` or `@latest`)
- [ ] Secrets are accessed via `${{ secrets.NAME }}`, not hardcoded
- [ ] Caching strategy is correct (not caching things that should not be cached)
- [ ] Concurrency and cancellation settings are appropriate

## IaC Changes (Terraform / Bicep / Kubernetes)

- [ ] Plan/preview run before apply
- [ ] Destructive changes flagged and approved
- [ ] State file is not committed to source control
- [ ] Resource naming follows conventions
- [ ] Tags/labels applied for cost tracking and ownership

## Network Changes

- [ ] Port changes are intentional and documented
- [ ] Reverse proxy config routes to correct backends
- [ ] TLS/SSL configuration is correct
- [ ] No unintended public exposure of internal services
