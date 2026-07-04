---
name: codebase-visualiser
description: Use this skill when the user asks to explain, visualise, explore, or walk through a codebase. Produces a human-readable breakdown with architecture diagrams, data flow traces, component relationship tables, domain notes, and tech debt markers — diagrams render inline as Mermaid or SVG. Also handles internal orientation before editing — see Internal Orientation Mode below. For general-purpose diagrams or charts about any topic (not an actual codebase), use the visualisation skill.
license: MIT
metadata:
  stack: agnostic
  version: 1.3
  last-reviewed: 2026-07-03
---

# Codebase Visualiser

## Use When

- The user asks to "explain", "visualise", "explore", "map", or "walk me through" the codebase.
- The user asks "how is this structured?", "what does this project do?", or "show me how it fits together".
- You need to produce a human-readable breakdown a developer can read and act on.
- Onboarding a new developer to an existing codebase.

## Do Not Use When

- The user wants code reviewed for quality issues — use **code-reviewer**.
- The user wants a plan for new work — use **planner**.
- You are debugging a failure — use **bug-hunter**.

## Internal Orientation Mode

If you need to orient yourself in an unfamiliar codebase before editing — not to produce output for the user — run steps 1–8 of the Process below, then reply with a single paragraph summarising what was found and what it means for the upcoming work. Omit diagrams and the full output format. The goal is working context, not a deliverable.

## Process

1. **Read the project structure.** Folder tree, solution/project files, package manifests.
2. **Identify the stack.** Language, runtime, framework, key libraries.
3. **Find entry points.** Where execution begins, what wires the app together.
4. **Determine the architectural pattern.** Layered, CQRS, event-driven, monolith, microservices — note the evidence.
5. **Extract domain language.** Nouns in folder names, class names, and key files.
6. **Map component relationships.** Which components depend on which, and how they communicate.
7. **Trace a representative data flow.** Pick one real request or operation and follow it end-to-end.
8. **Note test state and tech debt.** What is tested, what is not, and where obvious debt lives.

## Output Format

Full worked template with example diagrams: [templates/codebase-map-template.md](templates/codebase-map-template.md)

### Codebase Map: [Project Name]

**Stack:** [Language / Runtime / Framework / Key Libraries]

**In one sentence:** [What this codebase does and who uses it]

#### Project Structure

Annotated folder tree with the purpose of each area.

#### Architecture Diagram

Prefer Mermaid — it renders inline and is easy to revise. Fall back to ASCII only when Mermaid cannot be rendered. Use the actual component names from the codebase. Adapt the shape to what is present — do not force a layered diagram onto a flat or service-oriented project.

#### Entry Points

Table: Entry Point | File | Purpose.

#### Component Relationships

Table: From | To | How | Notes. Focus on non-obvious connections. Skip trivial or self-evident ones.

#### Data Flow

Trace one representative request end-to-end using real class and method names.

#### Architectural Pattern

**Pattern:** [Name or "unclear"]
**Evidence:** [What in the code indicates this pattern]
**Deviations:** [Where the code does not follow it consistently]

#### Domain Language

Table: Concept | Where It Appears | Notes.

#### Test State

Table: Type | Location | Coverage Impression. Plus **Gaps:** what appears untested based on folder/file inspection.

#### Tech Debt Markers

- [ ] [File or area]: [What was observed]

#### Open Questions

- [ ] [Thing that cannot be determined from static inspection alone]

## Diagram Guidelines

- Use `graph TD` (top-down) for layered or hierarchical architectures.
- Use `graph LR` (left-right) for pipelines, request flows, or service maps.
- Label edges with the communication mechanism: `HTTP`, `MediatR`, `event`, `DI`, `gRPC`, etc.
- Use actual names from the codebase. Never invent placeholders.
- Omit relationships you cannot verify — note them in Open Questions instead. If a connection clearly exists but its mechanism is unknown, label the edge "unknown mechanism" and list it in Open Questions.
- One diagram per concern is clearer than one overloaded diagram.
- If the codebase is simple, a single annotated folder tree may be more useful than a Mermaid diagram.

## Quality Bar

- At least one diagram reflects the real structure with real names.
- Entry points are located, not guessed.
- Data flow traces use actual class/method names, not invented ones.
- Domain concepts come from the code, not general knowledge.
- Test state is described based on files found, not assumed.
- Open questions are listed — gaps in understanding are useful to surface.
- Diagrams show only relationships verified from source inspection; unverified ones are listed in Open Questions, not drawn.
- Every row in Component Relationships cites the file:line where the dependency is declared.

## Failure Modes To Avoid

- Producing a diagram with invented names not present in the codebase.
- Describing test coverage without finding actual test files.
- Generating a diagram more complex than the codebase warrants.
- Treating a folder name as proof of what the folder contains.
- Producing generic output that could describe any project.
- Skipping the open questions section.
