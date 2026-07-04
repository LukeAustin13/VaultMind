# Security Review Checklist

Run this checklist during a security pass. Work through each category, tick what holds, and turn every unticked item into a finding in the review table. Each category ends with a one-line "pass vs fail looks like" so you can judge the category at a glance.

## Secrets

- [ ] No hardcoded passwords, API keys, tokens, or connection strings in source code
- [ ] No live secrets committed to config files in version control (`appsettings.json`, `.env`)
- [ ] Secrets are loaded from a secret manager, environment injection, or user secrets
- [ ] No secrets pasted into CI/CD logs, build args, or error messages

Pass vs fail: pass = grep for keys/passwords/tokens returns only references to injected config; fail = a real credential value appears in tracked source or config.

## AuthN/AuthZ

- [ ] Every protected endpoint validates an auth token and enforces expiry
- [ ] Authorisation (role/permission) checks are applied consistently, not just on the UI
- [ ] No IDOR — a user cannot access another user's resource by changing an ID
- [ ] Passwords are hashed with a strong algorithm (bcrypt, Argon2) and brute force is throttled

Pass vs fail: pass = changing the ID or dropping the token returns 401/403; fail = an unauthenticated or cross-user request returns the protected resource.

## Injection

- [ ] SQL uses parameterised queries or an ORM, never string concatenation of user input
- [ ] Output is encoded in views to prevent XSS
- [ ] No user input is passed unescaped into shell commands or process arguments
- [ ] File paths built from user input are validated against traversal (`../`)

Pass vs fail: pass = a payload like `' OR 1=1 --` or `../../etc/passwd` is treated as literal data; fail = it alters the query, command, or resolved path.

## Unsafe defaults / config

- [ ] CORS is restricted to known origins, not `*` with credentials
- [ ] Debug mode, developer exception pages, and stack traces are disabled in production
- [ ] No default or sample credentials remain in config
- [ ] Error responses to clients do not leak internals (paths, versions, SQL, stack traces)

Pass vs fail: pass = production config locks down origins and hides internals; fail = a default password, wildcard CORS, or verbose error reaches a client.

## Dependencies

- [ ] No package version with a known CVE relevant to how it is used
- [ ] No unmaintained or abandoned packages on a critical path
- [ ] Lockfile is present and pinned so builds are reproducible
- [ ] Transitive dependencies pulling in risky packages have been spot-checked

Pass vs fail: pass = an audit of the manifest surfaces no actionable known vulnerabilities; fail = a flagged CVE applies to a dependency the app actually exercises.

## Sensitive logging

- [ ] Passwords, tokens, and secrets are never written to logs at any level
- [ ] PII (emails, names, full card/account numbers) is not logged or is masked
- [ ] Request/response logging excludes auth headers and sensitive bodies
- [ ] Logs are not world-readable or exposed to unauthorised users

Pass vs fail: pass = a log sample around an auth or payment path contains no credential or raw PII; fail = a token, password, or unmasked PII value appears in log output.
