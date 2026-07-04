---
name: frontend-implementer
description: Use this skill when you need to implement UI from a specification, wireframe, or design. The frontend-implementer writes view code for XAML-based frameworks (WPF, MAUI) and web frameworks (HTML, React, Blazor). Examples are XAML-first — adapt patterns to your framework. It implements data binding, state management, design tokens, all data states, interactions, transitions, and accessibility. It does not design the UI — use ui-designer for that.
license: MIT
metadata:
  stack: dotnet-primary
  version: 1.2
  last-reviewed: 2026-07-03
---

# Frontend Implementer

## Use When

- You have a UI design specification (from ui-designer or the user) and need to write the view code.
- The user asks you to build a screen, component, page, or dialog.
- An existing UI component needs modification, extension, or state handling added.
- You need to implement data binding, state management, or event handling in the UI layer.

## Do Not Use When

- You need to design the UI first — use **ui-designer**.
- You are writing backend code — use **backend-architect** or implement directly.
- You are reviewing frontend code — use **code-reviewer**.
- The design is incomplete — go back to **ui-designer** and get a complete spec before building.

## Inputs To Look For

- The UI design spec (from ui-designer). If none exists, stop and produce one first.
- The UI framework in use (WPF, MAUI, Blazor, React, etc.).
- Existing components, styles, themes, and patterns in the project.
- The view model or data model the UI binds to.
- The design token definitions (colours, spacing, typography, motion).
- Accessibility requirements from the spec.

## Process

### 1. Read the full spec before writing a line of code
Understand the complete design: all states, all interactions, all copy, all accessibility requirements. Do not start building until you know what you are building end-to-end. A spec read halfway produces an implementation built halfway.

### 2. Check existing patterns
Look at how similar views are built in the project. Read 2-3 existing views in the Views directory and their paired view models to extract the established pattern before writing new view code. Match the file structure, naming conventions, MVVM or component patterns, and style application method. Do not introduce a different pattern without a clear reason.

### 3. Define or verify the view model / component interface
Before building the view, confirm the data model:
- What properties does the view bind to?
- What commands or callbacks does it fire?
- What observables, signals, or reactive state does it consume?
- What loading/error state does it expose?

If the view model does not exist, define its interface (properties and commands) before writing the view. The view should never reach into services or fetch data directly. If the view model does not expose async error handling and loading state, request those additions before implementing the view rather than working around the gap.

### 4. Build the structural skeleton
Lay out the major regions and components without any styling. Get the hierarchy right first. Verify the structure matches the spec layout before proceeding.

### 5. Apply design tokens
Implement colours, typography, spacing, and elevation from the spec's token definitions. Use the project's existing token system (resource dictionaries, CSS variables, design tokens, theme files). Never use inline magic numbers — if a token does not exist in the project's system, add it to the appropriate token file.

### 6. Wire data binding and state
- Bind every element to the correct view model property.
- Bind commands to the correct triggers (button click, item selection, key gesture).
- Implement collection binding with correct item templates.
- Bind loading, error, and empty states to observable properties — do not hard-code visibility.

### 7. Implement all data states
Every data-dependent area must implement all states from the spec — empty, loading, error, loaded, and partial where applicable. Full state definitions: [references/states-and-accessibility.md](references/states-and-accessibility.md).

### 8. Implement interactions and transitions
For every interaction defined in the spec:
- Wire the trigger (click, keyboard, gesture).
- Implement the immediate visual feedback (state change within 100ms).
- Show the loading state during async operations.
- Navigate or update state on success.
- Show the error state on failure.
- Implement the transition animation (duration and easing from the spec tokens).

Do not implement interactions not in the spec. Do not skip interactions that are in the spec.

### 9. Implement copy
Use the exact copy from the spec for all labels, empty state messages, error messages, confirmation dialogs, button text, and placeholder text. Do not substitute generic text ("OK", "Error", "Loading…") when the spec provides specific copy.

### 10. Implement accessibility
Apply semantic roles, accessible names, tab order, focus management, keyboard shortcuts, focus traps, and (for web) contrast and reduced-motion fallbacks from the spec. Full checklist: [references/states-and-accessibility.md](references/states-and-accessibility.md).

### 11. Implement responsive behaviour (web only)
Apply the breakpoint changes from the spec using the project's responsive system (CSS Grid, Flexbox, media queries, Tailwind breakpoints, etc.). Test each breakpoint range.

### 12. Self-review before delivering
- Every state from the spec is implemented — not just the happy path.
- Every piece of copy matches the spec exactly.
- No inline styles or magic numbers — all values reference tokens.
- Accessibility attributes are present on every element that needs them.
- Focus management is implemented for every action.
- Naming matches the project conventions throughout.

### 13. Build and run
Compile the project (`dotnet build` / `npm run build`) and render the view; paste the build result in the report. Route to **verification-gate** or **dotnet-quality-gate** before claiming completion — an implementation that has never compiled is not delivered.

## Output Format

Full report template with example tables: [templates/implementation-report-template.md](templates/implementation-report-template.md)

### Implementation: [Screen/Component Name]

Framework, spec reference, and files created or modified (path and purpose), then:

- **View Model Interface** — table: Property / Command | Type | Direction | Purpose.
- **States Implemented**, **Interactions Implemented**, **Accessibility** — checklists.
- **Tokens Applied** — table: Token | Value | Applied To.
- **Build Result** — real build output, or an explicit statement of why the project could not be built here.
- Complete implementation code.

## Quality Bar

- Every state from the spec is implemented — empty, loading, error, loaded, partial where applicable.
- Every copy string matches the spec exactly — no placeholder text.
- Every value references a design token — no magic numbers or inline colours.
- Data binding follows the project's existing patterns.
- Accessibility attributes are present on every element that requires them.
- Focus management is implemented for every action defined in the spec.
- Naming matches the project conventions throughout.
- No new design system or component library was introduced unless the spec calls for it.
- Transitions and animations match the spec's duration and easing values.
- The report contains real build output, or an explicit statement of why the project could not be built here.

## Failure Modes To Avoid

- **Building only the happy path.** Empty, loading, and error states are non-negotiable.
- **Substituting generic copy.** "OK" and "Error" are not implementations of a spec.
- **Inline styles and magic numbers.** If a value is not in the token system, add it there — do not inline it.
- **Ignoring the view model interface.** The view should not know where data comes from.
- **Skipping accessibility.** Tab order, screen reader labels, and focus management are part of the deliverable, not optional.
- **Partial interaction implementation.** If the spec defines what happens on failure, implement the failure path.
- **Inventing design decisions.** If the spec is ambiguous, ask — do not guess.
- **Over-abstracting.** A specific list view for orders is better than a generic `DataList<T>` that nobody asked for.
- **Breaking the project's naming or file conventions** to match a personal preference.
