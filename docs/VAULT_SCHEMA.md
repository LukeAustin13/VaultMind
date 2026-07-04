# MindVault Vault Schema

## Folder layout

`init` creates these folders (existing content is never touched):

```
00_Inbox          unfiled captures
01_Projects       project notes and their tasks
02_Areas          ongoing areas of responsibility
03_Resources      reference material
04_Decisions      decision records
05_Prompts        reusable prompts
06_Agent_Memory   agent memory notes
07_Reviews        reviews / retrospectives
08_Templates      note templates (excluded from validation)
99_Archive        archived notes (archive target; never hard-delete)
.mindvault        operational data only — index.sqlite, snapshots/, backups/, logs/, state.json
```

Markdown files are canonical. Everything under `.mindvault` is rebuildable and safe to delete
(`rebuild-index` restores the index; snapshots/backups are point-in-time copies).

## Managed note types

```
project decision task bug feature architecture prompt
research review meeting memory constraint risk
```

A note is "managed" when its `type` frontmatter is one of these. Managed notes must carry:

```yaml
type:
status:
created:
updated:
tags:
```

Project-related notes should also carry `project:` and `links:`.

Allowed statuses:

```
active open draft accepted rejected superseded done archived blocked cancelled
```

## Flat YAML rule

Frontmatter is a single flat mapping: values are scalars or lists of scalars. Nested mappings
(and lists containing mappings/lists) are rejected — `validate` reports them as `nested-yaml`
and write operations refuse to edit notes whose YAML cannot be parsed flat.

```yaml
# ok
tags:
  - project
links:
  - "[[Alpha]]"

# rejected
meta:
  nested: true
```

## Where created notes go

| Type | Path | Title (H1) |
| --- | --- | --- |
| project | `01_Projects/<Name>.md` | `# <Name>` |
| decision | `04_Decisions/Decision - <Title>.md` | `# Decision: <Title>` |
| task | `01_Projects/Task - <Title>.md` | `# Task: <Title>` |

`validate` warns (`outside-structure`) when a managed note of type project/task/decision/
prompt/memory/review lives outside its expected folder (or `99_Archive`).

## Note identity and references

- A note's **title** is its first `# H1` (falling back to the file name).
- Both the title and the file name (stem) are valid wiki-link targets and note references.
- A note reference can be: an exact relative path, a title, a stem, a slug
  (`decision-use-sqlite`), or a `[[Wiki Link]]` (aliases `|` and sections `#` are stripped).
- Ambiguous references are an error listing all candidates — MindVault never guesses.

## Index schema (rebuildable cache)

SQLite tables: `notes` (path, title, stem, slug, type, status, project, created, updated,
body hash, mtime, size, parse errors), `note_tags`, `note_links` (with normalized targets for
backlink queries), `note_headings`, `note_frontmatter` (every flat key/value), and the FTS5
table `fts_notes` (title + body). Folders skipped during indexing: any dot-folder
(`.obsidian`, `.trash`, `.git`, `.mindvault`, …), `node_modules`, `bin`, `obj`.

## Validation checks

| Code | Severity | Meaning |
| --- | --- | --- |
| `missing-folder` | error | Required folder missing (run `init`) |
| `missing-template` | warning | Template missing from `08_Templates` |
| `invalid-yaml` | error | Frontmatter fails to parse |
| `nested-yaml` | error | Frontmatter contains nested structures |
| `missing-frontmatter` | error | Managed note missing a required key |
| `invalid-status` | error | Status not in the allowed list |
| `duplicate-title` | error | Two notes share a title (breaks resolution) |
| `ambiguous-note-ref` | warning | Two notes share a file name |
| `broken-link` | warning | `[[target]]` matches no note title/stem |
| `missing-project-note` | error | `project:` value has no project note |
| `outside-structure` | warning | Managed note outside its expected folder |

## Snapshots, archive, backup

- Every mutation of an existing note first copies it to
  `.mindvault/snapshots/YYYY-MM-DD/<timestamp>-<safe-name>.md`.
- `archive` = snapshot → set `status: archived` + bump `updated` → move to `99_Archive` →
  reindex. Hard delete does not exist in v0.1.
- `backup` zips every indexed Markdown file to `.mindvault/backups/vault-backup-<ts>.zip`.
