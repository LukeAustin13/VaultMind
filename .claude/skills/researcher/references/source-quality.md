# Source Quality Reference

Load this when ranking conflicting sources, deciding whether a dated source is still usable, or assessing an unfamiliar publication. The SKILL.md rules are the summary; this is the working detail.

## The hierarchy, expanded

| Tier | Sources | Confidence ceiling | Notes |
|------|---------|--------------------|-------|
| 1 — Official | Official docs, spec documents, RFCs, the tool's own release notes and changelog | High | Still check the docs version matches the version in use — official docs for v5 are Tier 4 evidence about v3 behaviour |
| 2 — Primary | Source code itself, maintainer comments on issues/PRs, official repos' READMEs, first-party benchmarks, court filings / financial reports (for market research) | High | Source code outranks docs when they disagree — code is what runs |
| 3 — Reputable secondary | Major engineering blogs with named authors (company engineering blogs, well-known practitioners), conference talks, established tech press | Medium | Good for context, patterns, and "how people actually use it"; verify any specific version/API claim against Tier 1–2 |
| 4 — Weak | SEO-farm tutorials, undated blogs, most Medium posts, forum threads, Reddit, Q&A answers | Low | Usable only as leads. A Tier 4 claim graduates by being confirmed at a higher tier, never by being repeated at Tier 4 |
| 5 — Unusable as evidence | AI-generated content farms, marketing pages for the thing being evaluated, sources with no date and no author | — | A vendor's page about its own product is a claim to verify, not evidence. May still identify what to go verify |

Two independent Tier 3 sources agreeing raises confidence; ten Tier 4 sources agreeing does not — they usually share one upstream origin.

## Staleness windows by domain

How old a source can be before its claims need re-verification. These are defaults — a major release in the interval resets the clock to that release.

| Domain | Treat as stale after | Why |
|--------|---------------------|-----|
| AI/LLM models, APIs, pricing | ~3 months | Fastest-moving domain there is; model claims rot in weeks |
| JS/frontend framework specifics | ~6–12 months | Major-version churn |
| Cloud provider services/pricing | ~6–12 months | Frequent renames, new tiers, regional changes |
| .NET / Java / mainstream backend | ~12–18 months | Annual release cadence, strong compat culture |
| Databases, protocols, OS behaviour | ~2–3 years | Slow-moving, strong stability guarantees |
| CS fundamentals, algorithms, maths | Effectively never | Time-stable — training knowledge acceptable |

The question is not "is this source old?" but "has the subject plausibly changed since this was written?" — a 2019 post about TCP is fine; a 2019 post about React hooks best practice is a museum piece.

## Red flags that downgrade a source

- No publication date, or a "last updated" that only reflects ad refreshes.
- No named author, or an author bio that is clearly a content-farm persona.
- Version numbers absent from a claim where the answer is version-dependent.
- Code samples that don't compile or mix APIs from different major versions (a hallmark of AI-generated tutorials).
- The page's purpose is affiliate revenue or selling the thing being evaluated.
- Superlatives without measurements ("blazingly fast", "the best way in 2026").

## Query craft

Search quality is capped by query quality. Moves that separate professional research from typing the question into a search box:

- **Exact strings for errors.** Quote the literal error message (minus your local paths/values); an exact-string hit on the project's issue tracker beats twenty tutorial results.
- **Go where the primary sources live.** Search the library's own repo issues and changelog directly ("site:github.com/owner/repo deadlock", the releases page) before the open web.
- **Vary the formulation.** Run the practitioner phrasing ("X slow with large payloads"), the maintainer phrasing ("X buffer pooling"), and the comparison phrasing ("X vs Y production") — each surfaces a different stratum of sources.
- **Search the negative space.** "X migration off", "X regrets", "why we stopped using X" — the failure literature rarely appears in feature-phrased results (this feeds the counter-evidence check).
- **Pin the version in the query** when behaviour is version-dependent: "X v7 breaking changes", not "X breaking changes".

## Practical verification moves

- **Version claim:** check the changelog/release notes for the version in use; failing that, the source or type definitions.
- **Deprecation claim:** find the deprecation notice itself (docs, changelog, or annotated source), not a blog reporting it.
- **"Best practice" claim:** find what official docs currently recommend; a practice contradicted by current official guidance is stale regardless of how many posts repeat it.
- **Popularity/adoption claim:** package download stats, repo activity, and survey data beat anyone's blog assertion.
- **Security claim:** the CVE record or the project's advisory, never a news article alone.
