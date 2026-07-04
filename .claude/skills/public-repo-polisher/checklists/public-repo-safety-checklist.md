# Public Repo Safety Checklist

Use this checklist before making a repository public or after adding content to a public repo.

## Secrets and Credentials

- [ ] No API keys, tokens, or passwords in any file
- [ ] No connection strings with real credentials
- [ ] No private keys or certificates
- [ ] `.gitignore` excludes `.env`, credential files, and key files
- [ ] Git history does not contain secrets (check with `git log -p --all -S 'password'` or similar)
- [ ] Example env files use obviously fake values

## Private Information

- [ ] No internal company names, project codenames, or private URLs
- [ ] No employee names, emails, or Slack handles
- [ ] No references to internal JIRA/Linear/Notion boards
- [ ] No internal architecture diagrams with real service names
- [ ] No customer data, even anonymised, without explicit approval

## Code Safety

- [ ] No commented-out code containing private logic
- [ ] No hardcoded IP addresses or internal hostnames
- [ ] No database dumps or seed files with real data
- [ ] No test fixtures with real user data

## Dependencies

- [ ] No references to private package registries
- [ ] No git submodules pointing to private repos
- [ ] No private NuGet/npm feeds in config files

## Presentation

- [ ] README exists and is useful
- [ ] No empty placeholder files that make the repo look unfinished
- [ ] No auto-generated files that add noise without value
- [ ] License file present (if applicable)
- [ ] `.gitignore` is appropriate for the project type
