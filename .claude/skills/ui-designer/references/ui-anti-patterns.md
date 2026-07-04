# UI Anti-Patterns: Generic-AI Output and How to Avoid It

Load this during the Design Direction Debate, during Critique Mode, or whenever the user says a design looks generic, bland, or AI-generated. Each anti-pattern names the tell, why it happens, and the corrective move.

## Why AI-designed UI converges

Generated designs regress to the statistical centre of the training data: the median SaaS dashboard, the median landing page. The output is never wrong enough to reject and never specific enough to be good. The antidote is not "more creativity" — it is deriving every choice from THIS user's job, data, and context, because those specifics are exactly what the median design ignores.

## Layout and structure

| Anti-pattern | The tell | The corrective move |
|---|---|---|
| Default dashboard slop | Four stat cards in a row, a line chart, a table below, sidebar nav — regardless of what the product does | Ask what the user checks FIRST and most OFTEN. Lead with that at full prominence; delete any card the user wouldn't act on |
| Card-ification of everything | Lists, settings, and single facts each wrapped in a shadowed card | Cards are for heterogeneous, scannable collections. Settings are a form; records are a table; one number is a sentence |
| Hero-section reflex | Every page opens with a big centred heading + subtitle + two buttons | Interior pages are workplaces, not landing pages. Put the work at the top |
| Symmetric filler grids | 2×2 or 3-column feature grids padded to fill the row | If there are five real things, show five. Symmetry is not a requirement; invented content to achieve it is a defect |
| Modal overuse | Every action opens a centred modal | Inline edit for small changes, side panel to preserve context, dedicated page for complex work. Modals only for true interruptions |

## Visual style

| Anti-pattern | The tell | The corrective move |
|---|---|---|
| Purple-gradient genericism | Purple/indigo gradient accents, glassmorphism, glow shadows — the 2023-25 AI-output uniform | Derive colour from the domain and brand: finance earns restraint, healthcare earns calm, developer tools earn density. If there's a brand, extend it; never default to violet |
| Emoji as iconography | Emoji standing in for a designed icon set in headings, buttons, empty states | Use the platform/product icon set consistently, or no icons. Mixed emoji reads as unfinished |
| Uniform border-radius blanket | Same large radius on every element, buttons to modals | Radius is a scale (e.g. 2/4/8), applied by element size and nesting depth |
| Shadow soup | Every container elevated; nothing flat | Elevation encodes layering (overlays above content). If everything floats, nothing does. Most surfaces should be flat with borders or spacing doing the separation |
| Centred-everything | Headings, body copy, and forms all centre-aligned | Centre only short standalone statements. Work UIs are left-aligned (LTR); forms are always left-aligned |

## Content and interaction

| Anti-pattern | The tell | The corrective move |
|---|---|---|
| Placeholder-speak | "Unlock powerful insights", "Seamlessly manage your workflow", "Welcome back! 👋" | Copy states what the thing does in the user's vocabulary. A shipping tool says "3 shipments need customs forms", not "Manage your logistics" |
| Fake-data optimism | Mockup shows 4-8 tidy rows with short names | Design against the real cardinality: 0 rows, 1 row, 10,000 rows, a 90-character name, a null field. If the spec doesn't say what happens at 10k rows, it isn't done |
| Interaction amnesia | Only the default state designed — no hover, focus, disabled, loading, error | Every interactive element specifies all its states (the SKILL.md process enforces this; in critique mode, check it) |
| Onboarding theatre | Multi-step welcome tours and confetti for a tool used daily | Frequent-use tools optimise for the 500th session, not the 1st. Teach in context, once, dismissibly |
| Notification/badge inflation | Red badges, banners, and toasts competing on one screen | One attention system with defined priority levels; demote everything that isn't actionable now |

## The distinctiveness test

Before finalising any design, answer these. Two or more failures means the design is template output — return to the direction debate:

1. **Swap test:** Could this design serve a different product in a different industry with only a logo change? (It shouldn't.)
2. **Job trace:** Can you point at the three most prominent elements and connect each to the stated user job? (You must.)
3. **Opinion test:** Name one deliberate choice a generic template would not have made — a density decision, an unusual-but-justified layout, a domain-specific interaction. (There must be at least one, and it must be justified by the job, not by novelty.)
4. **Data reality:** Does the design specify behaviour at the real data extremes (empty, one, thousands, oversized values)? (It must.)

Distinctiveness is a by-product of specificity, never a goal pursued directly — a "creative" layout that ignores the job is worse than the template it replaced.
