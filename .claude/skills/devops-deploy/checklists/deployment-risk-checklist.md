# Deployment Risk Checklist

Use this checklist during a Review Mode assessment before applying infrastructure or deployment changes.

## Environment Identification

- [ ] Target environment clearly identified (local / staging / production)
- [ ] No accidental cross-environment impact
- [ ] Environment-specific config values are correct

## Dangerous Operations

- [ ] No resource deletion without backup (databases, volumes, storage)
- [ ] No port exposure changes without justification
- [ ] No permission escalation without review
- [ ] No image tag changes to `:latest` (pin specific versions)
- [ ] No volume mount changes to sensitive host paths

## Secrets and Credentials

- [ ] No hardcoded secrets in config files
- [ ] No secrets in Docker build args or environment variables in plain text
- [ ] Secret references use a secret manager or environment injection
- [ ] No credentials in CI/CD logs (check for `echo` or verbose modes)

## Docker / Compose

- [ ] Base images use specific version tags
- [ ] Multi-stage builds do not leak build-time secrets
- [ ] Health checks are defined
- [ ] Named volumes used for persistent data (not anonymous)
- [ ] Network exposure is intentional

## CI/CD Pipeline

- [ ] Pipeline changes tested on a branch before merging
- [ ] Secrets are injected via CI secret store, not hardcoded
- [ ] Deployment steps have rollback capability
- [ ] Manual approval gates exist for production deployments

## Rollback

- [ ] Rollback procedure documented
- [ ] Previous version is identifiable and deployable
- [ ] Database changes are backward-compatible (or rollback script exists)
- [ ] Rollback has been tested or is clearly reversible

## Verification

- [ ] Post-deployment health checks defined
- [ ] Smoke tests cover critical paths
- [ ] Monitoring/alerting covers the change
- [ ] Logs are accessible for debugging
