---
name: artwork-generation
description: Use this skill when the user asks to create static visual artwork that Claude renders directly — logos, icons, icon sets, badges, monograms, wordmarks, simple illustrations, decorative graphics, patterns, or avatars. "Make a logo", "draw an icon", "design a badge", "create an SVG illustration". Claude authors the artwork itself as clean SVG (or HTML/CSS) with no external image models or API calls. It does NOT produce photographic/raster output or call image generators, render information graphics like charts and diagrams (use visualisation), or design application screens (use ui-designer).
license: MIT
metadata:
  stack: agnostic
  version: 2.0
  last-reviewed: 2026-06-29
---

# Artwork Generation

Claude produces the artwork directly as vector SVG (or HTML/CSS). There are no external models, APIs, or generation services in this skill — the model is the illustrator. That keeps output reproducible, editable, dependency-free, and crisp at any size.

## Use When

- The user wants a logo, wordmark, monogram, or brand mark.
- The user wants an icon or a consistent icon set.
- The user wants a badge, emblem, sticker, or label graphic.
- The user wants a simple illustration, spot graphic, mascot, or avatar.
- The user wants a repeatable pattern, texture, or decorative background.
- The user wants an existing SVG refined, restyled, recoloured, or turned into a set.

## Do Not Use When

- The user needs a photograph or painterly/photoreal raster image — that needs an external image model, which this skill deliberately does not use. Say so and offer a vector interpretation instead.
- The graphic conveys data or structure (flowchart, chart, ER/sequence diagram) — use **visualisation**.
- The deliverable is an application screen, layout, or component spec — use **ui-designer**.
- The user wants production front-end code for a feature, not a standalone graphic — use **frontend-implementer**.

## Inputs To Look For

- Subject and purpose (app icon, web logo, favicon, print, social avatar).
- Brand constraints: colours (hex), existing marks to match, tone (playful, corporate, minimal).
- Hard constraints: aspect ratio, viewBox size, monochrome vs colour, transparency, any text that must appear.
- Where it renders (favicon needs to read at 16px; a hero logo can carry detail).

## Process

1. **Clarify intent and constraints.** Confirm subject, use, style direction, palette, and size/ratio. Ask only when a missing answer would change the result materially.
2. **Choose a visual approach.** State it in one line: the concept, the geometry (grid, symmetry, golden-ratio, single-stroke), the palette, and the style (flat, line, duotone, geometric). Decide deliberately — do not start drawing shapes without a concept.
3. **Author the SVG.** Write clean, hand-built SVG: a sensible `viewBox`, grouped shapes, named/commented sections, `currentColor` or CSS variables where theming helps, and a `<title>`/`<desc>` for accessibility. Prefer geometric precision (aligned coordinates, consistent stroke widths) over random paths.
4. **Render and check.** Preview the SVG (inline where the client renders it). Verify it matches the brief, reads at its smallest intended size, and has no stray or overlapping nodes.
5. **Iterate on the specific defect** — recolour, rebalance weight, simplify — rather than regenerating from scratch.
6. **Deliver** the SVG plus short usage notes and any variants (mono, dark-mode, favicon crop).

## SVG Craft Rules

- Set an intentional `viewBox` (e.g. `0 0 64 64` for an icon) and let it scale — never hardcode pixel sizes into the art.
- Keep paths minimal and aligned to a grid; consistent stroke widths read as "designed", random ones read as "generated".
- Use `currentColor` (or a small set of CSS custom properties) so the mark inherits theme colour and supports dark mode.
- Add `<title>` and, when useful, `<desc>` for screen readers; mark purely decorative graphics `aria-hidden`.
- For an icon set, fix the grid, stroke width, corner radius, and padding once, then vary only the glyph — consistency is the whole point of a set.
- Validate by parsing or rendering: run it through an XML parser (`xmllint --noout`, or any parser available) or render it in the client — a claim of well-formedness without a parse or render is not validation. No external references (no remote `<image href>`).

## Output Format

Deliver the artwork inline (and/or as a file), then:

### Asset: [name]

| Field | Value |
|---|---|
| Format | SVG (vector) |
| viewBox / size | `0 0 64 64` |
| Palette | `#1E40AF`, `#F59E0B`, `currentColor` |
| Theming | inherits `currentColor`; dark-mode variant included |
| Variants | full-colour, monochrome, 16px favicon crop |

**Concept:** one line on the idea and why it fits the brief.
**Usage:** where it reads well, any size floor, how to recolour.

## Quality Bar

- The output is valid, self-contained SVG that renders with no external dependencies.
- It scales crisply and still reads at its smallest intended size (test the icon at ~16px mentally).
- It matches the stated brief — subject, palette, tone — and any required text is present and legible.
- Geometry is deliberate: aligned coordinates, consistent stroke weight, coherent grid — not arbitrary paths.
- Accessibility basics are present (`<title>`/`<desc>` or `aria-hidden` for decorative).
- For a set, every member shares grid, stroke, and padding.

## Failure Modes To Avoid

- Pretending to produce a photograph — this skill is vector-only by design; offer a vector take or say it needs a different tool.
- Emitting messy, misaligned paths with inconsistent stroke widths that look auto-generated.
- Hardcoding pixel dimensions instead of using `viewBox`, so the art does not scale.
- Inventing brand colours when the user supplied a palette.
- Referencing external image URLs inside the SVG, defeating the "self-contained" goal.
- Producing an icon "set" whose members do not share a grid, stroke width, or padding.

## Related Skills

- **visualisation** — when the graphic carries information (diagram, chart, flowchart) rather than being decorative or brand art.
- **ui-designer** — when the real need is a screen or component, not a standalone mark.
- **frontend-implementer** — to wire a delivered SVG into an actual interface.
