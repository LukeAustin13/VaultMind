---
name: ui-designer
description: Use this skill when you need to design or critique UI/UX for any screen, dialog, page, or component — across WPF, MAUI, and web. Triggers include "design this screen", "how should this look?", "critique this design", "this looks generic/AI-generated, fix it". The ui-designer debates 2-3 design directions, commits to one with a rejection list, and produces a complete, implementation-ready specification covering information architecture, visual hierarchy, layout, component behaviour, interaction design, content/copy, all data states, accessibility, and responsive behaviour. It does not write implementation code — use frontend-implementer for that.
license: MIT
metadata:
  stack: dotnet-and-web
  version: 2.1
  last-reviewed: 2026-07-02
---

# UI Designer

## Use When

- Designing a new screen, dialog, page, or component from scratch.
- Redesigning or rethinking an existing UI for quality, usability, or clarity.
- The user asks "how should this look?", "design this screen", or "what should the user experience be?".
- The user asks for a critique of an existing design, mockup, or screenshot — see Critique Mode.
- The user says the UI looks generic, bland, or "AI-generated" and wants it distinctive.
- You need a complete spec a developer can build from without making design decisions.
- Accessibility, keyboard navigation, responsive layout, or state handling needs design.

## Do Not Use When

- You are implementing UI from a completed specification — use **frontend-implementer**.
- You are designing the API the UI calls — use **api-designer**.
- You are reviewing existing UI code — use **code-reviewer**.
- The task is a trivial one-line label change — just do it.

## Small Change Mode

For small changes — at most 3 elements, no more than one new state, and no navigation changes (label update, spacing tweak, adding one state to an existing component) — produce only:
- A list of the changed elements and their new specification.
- Accessibility impact, if any.
- One-line rationale.

Skip the full output format for small changes.

## Critique Mode

When the user asks you to critique or assess an existing design (a screenshot, mockup, Figma export, or built screen) rather than produce one, do not emit the full spec. Produce:

- **Verdict:** one sentence — is this design fit for its user's job, and what is the single biggest problem?
- **Ranked issues:** each with severity (Blocking / Major / Minor), the specific element, why it fails (hierarchy, state coverage, copy, accessibility, distinctiveness — cite the concern), and the concrete fix.
- **What works:** genuinely good decisions worth preserving through any rework.
- **Rework recommendation:** targeted fixes vs redesign, and if redesign, which direction (then this skill's full process applies).

Check the design against [references/ui-anti-patterns.md](references/ui-anti-patterns.md) as part of the pass. Be direct — a critique that finds nothing wrong with a mediocre screen is a failed critique.

## Design Direction Debate

For any new screen or redesign (not Small Change Mode), run this after establishing the user goal (Process step 1) and before any layout or token work:

1. Sketch 2–3 genuinely different directions — different layout strategies or interaction models, not the same layout with different accent colours. E.g. for a data-heavy admin list: (a) dense table with inline actions, (b) master-detail split view, (c) card grid with progressive disclosure.
2. Evaluate each against the user's job, frequency of use, data shape, and platform. One sentence of trade-off each.
3. Commit to one. State why it wins for THIS user and job.
4. Record the rejected directions with their one-line rejection reasons — they go in the output so the reader can see what was considered and reopen it if the constraints change.

Never present all directions as equally valid and ask the user to pick without a recommendation. If the user's request already locks the direction ("add a column to the existing table"), say so and skip the debate.

## Inputs To Look For

- Feature requirements, user stories, or job-to-be-done descriptions.
- Existing UI patterns, screenshots, or XAML/component files in the project.
- Target platform(s): WPF, MAUI, web (responsive), or all.
- The design system or component library in use, if any.
- Brand guide: typography, colour palette, spacing conventions, icon set.
- User roles and what each role needs to accomplish on this screen.
- The data shape that will populate the UI — field names, types, cardinality.
- Related screens: what comes before this, what comes after.

## Clarify Before Starting

Do not design without answers to these. Wrong assumptions here produce specs that cannot be built.

- **What is the user's primary goal on this screen?** One job-to-be-done, stated precisely.
- **What platform?** WPF, MAUI, web (desktop-first or mobile-first), or multiple — constraints differ significantly.
- **Does a design system or component library exist?** If yes, new screens must use it. If no, define tokens for this screen.
- **What is the existing visual tone?** Inspect sibling screens or existing components first. If none exist, ask: Enterprise/professional, consumer/friendly, data-dense/analytical, or minimal/editorial.
- **What data populates this screen?** Unknown data shapes mean unknown states and unknown loading behaviour.
- **Who are the users and what is their context?** A power user who lives in this screen needs different design than someone who visits once a month.
- **What are the adjacent screens?** What navigates here, what does this navigate to?

## Process

### 1. Establish the user goal and context
State the user's job-to-be-done in one sentence. Identify who the user is, how often they use this screen, and what they already know when they arrive. This governs every design decision that follows.

### 2. Define the information architecture
List every piece of content and data this screen must contain. Organise it into a hierarchy:
- **Primary:** What the user came here for. Must be immediately visible.
- **Secondary:** Supporting context. Visible but not dominant.
- **Tertiary:** Actions, metadata, navigation. Available but not intrusive.

Cut anything that does not serve the user's goal. Every element earns its place.

### 3. Design visual hierarchy
Before laying out pixels, define how the eye moves through the screen:
- What is the single strongest visual anchor?
- How does size, weight, colour, and spacing direct attention in the correct sequence?
- What is the reading order? Is it natural for the platform (left-to-right, top-to-bottom)?
- Where does the eye rest when the task is complete?

### 4. Define the design tokens for this screen
If the project has a design system, every token must reference or extend it — do not define a new token for a value the system already provides. If there is no design system, define them:
- **Typography:** Heading, subheading, body, label, caption — font, weight, size, line-height for each.
- **Colour:** Surface, primary action, secondary action, text (primary/secondary/muted), border, error, success, warning, info.
- **Spacing:** Base unit and the scale used for padding, gaps, and margins (e.g., 4px base, multiples of 4).
- **Elevation/depth:** Shadow levels for overlays, cards, and modals.
- **Motion:** Duration and easing for transitions and micro-interactions (e.g., 150ms ease-out for hover, 250ms ease-in-out for panel open).

### 5. Design the layout
Define the spatial structure of the screen:
- Major regions and their purpose.
- Component placement within regions.
- Alignment grid and spacing rhythm.
- What is fixed vs scrollable.
- For web: responsive breakpoints and what changes at each.

### 6. Define every component and its variants
For each component on the screen:
- **Default state:** What it looks like at rest.
- **Hover / focus:** Visual feedback that it is interactive.
- **Active / pressed:** Visual feedback during interaction.
- **Disabled:** How it appears and behaves when unavailable.
- **Loading:** If the component can be in a loading state independently.
- **Error:** If the component can display validation or error state.
- **Content variants:** How does it look with maximum content? With minimum content?

### 7. Define all data states for the full screen
For every area that depends on data:

| Area | Empty | Loading | Error | Loaded | Partial |
|------|-------|---------|-------|--------|---------|
| [Name] | [Copy + action] | [Skeleton/spinner] | [Copy + retry] | [Normal] | [Degraded UI] |

Empty states must have a message and a path forward — never a blank space.
Error states must tell the user what happened and what to do next.

### 8. Write the content and copy
Design is incomplete without real words. Do not use placeholder text.
- **Page/dialog title:** Precise, action-oriented.
- **Labels:** Concise, unambiguous, consistent with platform conventions.
- **Empty state messages:** Explain why it is empty and what to do (e.g., "No orders yet. Create your first order to get started.").
- **Error messages:** State what went wrong in plain language, not error codes.
- **Confirmation messages:** Confirm what happened, not what was attempted.
- **CTAs (calls to action):** Verb-first, specific ("Save changes" not "OK"; "Delete order" not "Confirm").
- **Placeholder text:** Hint at expected input, not a repeat of the label.
- **Tooltips:** For non-obvious elements only. Never duplicate the label.

### 9. Design interactions and transitions
For every user action, define the complete system response:
- What triggers the action (click, keyboard, swipe, focus).
- What immediate feedback the user receives (visual state change within 100ms).
- What the system does in the background (if async).
- What the UI shows during processing (loading state, disabled controls).
- What the user sees when it succeeds (confirmation, navigation, state update).
- What the user sees when it fails (error placement, recovery path).
- What transition occurs (animation, panel slide, fade — with duration and easing).

### 10. Define focus management and keyboard navigation
- Tab order sequence across the screen.
- Where focus lands on screen open/navigation.
- Where focus goes after a form submit, dialog close, or item delete.
- Keyboard shortcuts for power users (if applicable).
- Focus trap for modals and overlays.

### 11. Define accessibility requirements
- Semantic roles for every region (landmark, heading level, list, form).
- Screen reader labels for icons, images, and non-obvious interactive elements.
- Colour contrast requirements (WCAG AA minimum: 4.5:1 for text, 3:1 for UI components).
- Touch target sizes (minimum 44×44px for mobile).
- Motion: provide reduced-motion alternative for any animation.

### 12. Review against UX heuristics

Before finalising, check: Does the user always know what the system is doing? Can they undo or escape any action? Do all patterns match the rest of the application? Are destructive actions confirmed? Is every element earning its space? If any check fails, revise before producing the spec.

## Output Format

The full specification, in order: header (user goal, platform, tone, design system) -> design direction (chosen + rejected directions table) -> information architecture -> design tokens -> layout diagram -> component specifications with state tables -> data states matrix -> content and copy -> interaction design -> focus management -> accessibility -> responsive behaviour (web) -> navigation and flow -> open questions.

Write the spec against the fill-in template in [templates/ui-design-spec-template.md](templates/ui-design-spec-template.md) - load it before producing the spec; it contains the exact table shapes and a worked token set. Small Change Mode and Critique Mode use their own compact formats defined above.

## Quality Bar

- The user goal is stated in one sentence and every design decision can be traced back to it.
- A direction debate happened: 2-3 genuinely different directions were weighed, one was chosen with stated reasoning, and the rejected directions are recorded with reasons.
- The design would not pass for template output — it makes at least one deliberate, job-driven choice a generic dashboard template would not (checked against [references/ui-anti-patterns.md](references/ui-anti-patterns.md)).
- Every data-dependent area has all five states defined: empty, loading, error, loaded, partial.
- Empty states have a message and a path forward — never a blank.
- Error messages are in plain language and tell the user what to do, not just what went wrong.
- All copy is written — no placeholder text like "Label" or "Lorem ipsum".
- CTAs are verb-first and specific.
- Every interactive element has defined hover, focus, active, and disabled states.
- Colour contrast meets WCAG AA for all text and UI component pairs.
- Focus management is defined for every significant action.
- The design is platform-appropriate — no web patterns on WPF, no desktop-only patterns on mobile.
- Tokens are defined or referenced — no undefined "blue button" or "standard spacing".
- Adjacent screens are named — the design does not exist in isolation.
- The information architecture removes at least one thing that does not serve the user goal.

## Failure Modes To Avoid

- **Generic-template output.** The default SaaS dashboard — stat cards in a row, a line chart, a table, sidebar nav — produced regardless of what the user's job actually is. If the design would fit any product, it fits this one by accident. Run the checks in [references/ui-anti-patterns.md](references/ui-anti-patterns.md).
- **Skipping the direction debate.** Committing to the first layout that comes to mind. The first idea is usually the most generic one.
- **Presenting options without a verdict.** Listing three directions and asking the user to choose does the easy half of the job. Recommend one.
- **Designing only the happy path.** Empty, loading, and error states are not afterthoughts — they are what most users see most of the time.
- **Placeholder copy.** "First Name" is a label, not a design. Write the actual words.
- **Vague spacing.** "Some padding" is not a spec. Use tokens and be specific.
- **Ignoring the platform.** A hamburger menu and infinite scroll do not belong in a WPF desktop app. A fixed multi-column layout does not belong on mobile.
- **Designing without the data shape.** If you do not know what fields exist, you cannot design a form or table.
- **Accessibility as an afterthought.** Keyboard navigation and screen reader labels must be designed, not retrofitted.
- **Forgetting focus management.** Every action that changes the UI must have a defined answer to "where does focus go?".
- **Over-specifying visual style.** If the project has a design system, reference it. Do not redesign the button.
- **Under-specifying interaction.** "Click to open modal" is not a complete interaction spec. What opens, what is in it, what happens on close, where does focus go?
- **No information hierarchy.** If everything is equally prominent, nothing is prominent.
- **Designing in isolation.** Every screen is part of a flow. Define what comes before and after.
