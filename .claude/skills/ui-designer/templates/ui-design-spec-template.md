# UI Design Specification Template

Fill-in template for the **ui-designer** skill's full output. Load this when producing a complete design specification. Delete bracketed guidance as you fill it in. Small Change Mode and Critique Mode (defined in SKILL.md) do not use this template.

---

### UI Design: [Screen/Component Name]

**User Goal:** [One sentence — the job-to-be-done]
**Platform:** [WPF / MAUI / Web — desktop-first or mobile-first]
**Design Tone:** [Enterprise / Consumer / Data-dense / Minimal]
**Existing Design System:** [Name, or "None — tokens defined below"]

---

#### Design Direction

**Chosen:** [Direction name — e.g. "Master-detail split view"] — [2-3 sentences: why this wins for this user, job, and data shape]

**Rejected:**

| Direction | Rejected because |
|-----------|------------------|
| Dense table with inline actions | Users act on one record at a time and need full context — inline actions optimise the wrong workflow |
| Card grid | Data is 12 comparable fields per record; cards hide comparison, which is the primary job |

---

#### Information Architecture

**Primary (must be immediately visible):**
1. [Most important content/action]

**Secondary (supporting context):**
2. [Supporting information]

**Tertiary (available but not dominant):**
3. [Actions, metadata, navigation]

**Removed / out of scope:** [Anything considered and cut, with reason]

---

#### Design Tokens

| Token | Value | Usage |
|-------|-------|-------|
| `color-surface` | `#FFFFFF` | Page and card background |
| `color-primary` | `#1A73E8` | Primary actions and links |
| `color-text-primary` | `#1F1F1F` | Body and heading text |
| `color-text-muted` | `#6B6B6B` | Labels, captions, secondary text |
| `color-error` | `#D93025` | Error states and messages |
| `color-success` | `#1E8E3E` | Success confirmations |
| `color-border` | `#DADCE0` | Input borders, dividers |
| `space-base` | `4px` | Base unit |
| `space-component` | `16px` | Internal component padding |
| `space-section` | `32px` | Between major regions |
| `type-heading` | `20px / 600 / 1.3` | Page and section headings |
| `type-body` | `14px / 400 / 1.5` | Body text |
| `type-label` | `12px / 500 / 1.4` | Form labels, table headers |
| `motion-quick` | `150ms ease-out` | Hover and focus transitions |
| `motion-standard` | `250ms ease-in-out` | Panel opens, overlays |

[Add, remove, or override tokens as needed for the screen]

---

#### Layout

```
+----------------------------------------------------------+
| Header: [Title]                         [Primary Action] |
+----------------------------------------------------------+
| Filter bar / Toolbar (if applicable)                     |
+----------------------------------------------------------+
| [Left region — if applicable]  | [Main content region]  |
|                                |                         |
|  [Component A]                 |  [Component B]          |
|  [Component C]                 |  [Component D]          |
|                                |                         |
+----------------------------------------------------------+
| Footer / Pagination / Status bar (if applicable)         |
+----------------------------------------------------------+
```

**Spacing notes:** [Key spacing decisions — region padding, component gaps]
**Scroll behaviour:** [What scrolls, what is fixed]
**Fixed dimensions:** [Any fixed widths/heights and why]

---

#### Component Specifications

##### [Component Name] — e.g., Order Table

**Purpose:** [What this component does]

**Default state:**
[Description of visual appearance at rest]

**Variants and states:**

| State | Trigger | Visual Change | Notes |
|-------|---------|---------------|-------|
| Hover (row) | Mouse over | Background `color-surface-hover` | Show row actions |
| Selected | Click row | Left border accent, background tint | Multi-select: checkbox appears |
| Focused | Tab | Ring outline `2px color-primary` | Same ring on all interactive elements |
| Loading (cell) | Data fetch | Skeleton pulse animation | 60% width placeholder |
| Error (cell) | Load failure | `—` with tooltip "Failed to load" | |

**Column definitions (if table):**

| Column | Width | Alignment | Sortable | Notes |
|--------|-------|-----------|----------|-------|
| [Name] | [%/px/flex] | [Left/Right/Center] | [Y/N] | |

[Repeat section for each significant component]

---

#### Data States

| Area | Empty | Loading | Error | Loaded | Partial |
|------|-------|---------|-------|--------|---------|
| [Component] | "[Message. [Action]]" | Skeleton rows (3) | "[Message.] [Retry]" | Data | Loaded data + error banner |

---

#### Content and Copy

| Element | Copy | Notes |
|---------|------|-------|
| Page title | "[Exact title]" | |
| Primary CTA | "[Verb phrase]" | Disabled when: [condition] |
| Empty state heading | "[Heading]" | |
| Empty state body | "[Body copy]" | |
| Empty state CTA | "[Action label]" | |
| Error message (load) | "[Plain language error]" | |
| Confirmation toast | "[What succeeded]" | Auto-dismiss: 4s |
| Delete confirmation | "[Exact modal copy]" | |
| [Field] placeholder | "[Hint text]" | |
| [Field] validation error | "[Specific error, not generic]" | |

---

#### Interaction Design

| Trigger | Element | System Response | Transition | Error Path |
|---------|---------|----------------|------------|------------|
| Click [Primary CTA] | Button | Disable button, show spinner in button | Immediate | Re-enable button, show inline error |
| Press Escape | Modal | Close modal, return focus to trigger | `250ms fade-out` | — |
| Submit form (valid) | Form | Submit, show loading, navigate on success | Page transition `300ms` | Inline field errors |
| Submit form (invalid) | Form | Highlight first error field, scroll to it | — | Focus moves to first error |
| Delete item | Row action | Show confirmation dialog | `200ms scale-in` | — |
| Confirm delete | Dialog | Remove row with exit animation, show toast | `200ms fade-out` | Toast: "Delete failed. [Retry]" |

---

#### Focus Management

- **On screen open:** Focus lands on [first interactive element / search field / primary action].
- **After form submit:** Focus moves to [success message / first result / next step].
- **After dialog close:** Focus returns to the element that triggered the dialog.
- **After item delete:** Focus moves to [next item / previous item / list container].
- **Tab order:** [Sequence through major regions and components]
- **Focus trap:** [Modals and overlays trap focus until closed]

---

#### Accessibility

- **Landmark regions:** `<header>`, `<main>`, `<nav>` (or XAML equivalents with AutomationProperties.LandmarkType).
- **Heading hierarchy:** `h1` — page title, `h2` — section headings, `h3` — subsections.
- **Interactive element labels:** [List non-obvious elements and their accessible names]
- **Icon-only buttons:** All must have `aria-label` or `AutomationProperties.Name`.
- **Images:** Decorative images `alt=""`, informative images describe content.
- **Colour contrast:** All text-background pairs checked against WCAG AA (4.5:1 for text, 3:1 for UI).
- **Touch targets:** Minimum 44×44px on mobile.
- **Reduced motion:** [Specify the reduced-motion fallback for any animation — typically instant show/hide]

---

#### Responsive Behaviour (web only)

| Breakpoint | Layout Change | Component Changes |
|------------|--------------|-------------------|
| ≥1280px (desktop) | [Base layout] | — |
| 768–1279px (tablet) | [Changes] | [Collapsed sidebar, stacked panels, etc.] |
| <768px (mobile) | [Changes] | [Bottom nav, full-width cards, single column] |

---

#### Navigation and Flow

**Navigates from:** [Previous screen(s) and trigger]
**Navigates to:** [Next screen(s) and trigger]
**Back behaviour:** [What "back" does — navigate up, discard changes, confirm?]
**Deep link:** [Can this screen be linked to directly? What state does it open in?]

---

#### Open Questions

- [ ] [Decision that cannot be made without more information — who can answer it]
