# Sprint Change Proposal — Marketing Site Refresh (Pause Epic 4, Add Epic 5)

**Project:** Velucid
**Date:** 2026-06-03
**Author:** bmad-correct-course workflow
**Status:** DRAFT — pending approval
**Scope classification:** Moderate — backlog reorganization, no fundamental replan
**Related:** [sprint-change-proposal-2026-06-03.md](./sprint-change-proposal-2026-06-03.md) (Nx migration; in-progress)

---

## 1. Issue Summary

### Triggering context

The marketing strategy for Velucid has been formalized in `marketing_strategy.md` (root of repo). It defines a brand voice (dbrand × Palantir register — blunt, quiet confidence, restrained), an explicit content spec for the landing page (5 sections with copy drafted, spice levels assigned, banned words listed), and a credibility framing that positions probability-based forecasting as a method other industries have used for decades — never to be named specifically.

The current `apps/web/src/pages/index.astro` was authored before this strategy existed. It uses a different voice: gradient mesh backgrounds, dot-grid textures, GSAP entrance and ScrollTrigger animations, an animated S-curve SVG, a slogan trick ("Your" chip with a strikethrough on "Their"), a 3-step "How it works" grid, and a gradient CTA card. Copy is warmer and more aspirational ("Purpose-built for modern software teams," "Less overhead. More control.").

The landing page is the front door of the product. It must match the brand voice. Until it does, the marketing strategy exists as a document but is not yet expressed in the user-facing surface that will be shared externally.

### Why now

- The marketing strategy is finalized and exhaustive — section-by-section copy is drafted with explicit spice levels. It functions as the spec.
- The current landing page is structurally complete and reviewable, but its tonal mismatch is total: it's not a tweak, it's a rewrite.
- Story 4-0 (Nx monorepo bootstrap) is in `review`; the rest of Epic 4 (4-1 → 4-6) is in `backlog` and was already queued behind the projector migration that needs the Kurrent TypeScript sync engine to land before it can be fully activated.
- Pure-frontend marketing work does not require Epic 4's backend infrastructure to be complete. It is independent work that can ship now and benefit from the already-wired `apps/web` (the Nx layout from 4-0).

### Evidence

- `marketing_strategy.md` §"Landing Page: Hero Section Reference Draft" — full copy draft, 5 sections, with one level-6 closer line.
- `apps/web/src/pages/index.astro` — 804 lines, current implementation, tonal mismatch with the strategy.
- PRD §2 already includes "Local-first, instantly responsive UI" goal (added in the 2026-06-03 Nx change proposal) — no PRD edit required for this change.
- `sprint-status.yaml` — Epic 4 is `in-progress` with 4-0 in `review`; 4-1 → 4-6 in `backlog`. No Epic 5 exists yet.

---

## 2. Impact Analysis

### Epic Impact

| Epic | Status today | Impact | Action |
|---|---|---|---|
| **Epic 1 — Core Platform Kanban MVP** | paused | None — already paused, no change | No change |
| **Epic 2 — Flow Features** | paused | None | No change |
| **Epic 3 — Authorization (OpenFGA)** | paused | None — story 3-2's projector-side port is part of Epic 4 | No change |
| **Epic 4 — Nx Monorepo + Projector Migration** | in-progress | 4-1 → 4-6 are pure-backend / infrastructure work; the landing page does not depend on them and they do not block the landing page | 4-0 stays in `review`; 4-1 → 4-6 paused (deferred until after Epic 5 lands) |
| **NEW Epic 5 — Marketing Site Refresh** | — | New epic, takes priority over Epic 4 | Add with story 5-0 |

### Story Impact

- **Already done (1-0, 1-1, 3-1, 3-2):** no regression. None of these stories reference landing-page copy.
- **4-0 (Nx bootstrap, in `review`):** the new layout (`apps/web/`, Astro + Bun) is what Story 5-0 builds on. Landing page work is the first meaningful exercise of the new layout. Resolve 4-0 to `done` when ready — does not block 5-0 if the layout is already usable, but landing the review cleanly is the right order.
- **4-1 → 4-6:** all `backlog`, no in-flight work. Pausing them has zero in-flight cost.
- **5-0 (new):** landing page rewrite per `marketing_strategy.md`. Bounded scope, frontend-only.

### Artifact Conflicts

| Artifact | Conflict | Resolution |
|---|---|---|
| **PRD.md** | None. The marketing landing page does not change product behavior, MVP scope, or any feature acceptance criteria. The "Local-first, instantly responsive UI" goal (already added in the prior change proposal) is unaffected. | No edit. |
| **architecture.md** | None. The landing page is a presentation-layer concern. No new components, services, or event flows. | No edit. |
| **Epics 1, 2, 3** | None. They are paused and untouched. | No edit. |
| **Epic 4** | No content change — the Nx migration epic is the right scope. The pause is a sprint-status update, not a content change. | Pause marker in `sprint-status.yaml`. |
| **sprint-status.yaml** | Doesn't reflect Epic 5; shows Epic 4-1 → 4-6 as `backlog`. | Add `epic-5: backlog` and `5-0-landing-page-redesign: backlog`; keep 4-1 → 4-6 as `backlog` (the `paused` marker for stories isn't used; the sprint-change-proposal and a comment explain the pause). |
| **CLAUDE.md** | Does not reference `marketing_strategy.md` as a brand-voice source of truth. | One-line add to a "Voice" or "Reference docs" section pointing to `marketing_strategy.md` as the spec for any landing/marketing/email/copy work. |
| **index.astro** | Full rewrite of voice, structure, and assets. The file stays; the content is replaced. | Story 5-0 deliverable. |
| **apps/web/package.json** | GSAP + ScrollTrigger are no longer used. | Story 5-0 may remove the dep (optional — keep if the new design uses any entrance animation, but the strategy implies restraint). |
| **deploy / CI** | None — the web app's build pipeline does not change. | No edit. |

### Technical Impact

- **Pure frontend work.** No backend, no event-sourcing, no projector, no auth changes.
- **Builds on the Nx + Astro + Bun + Tailwind stack that Story 4-0 wired up.** The new layout is the foundation; the marketing site is its first substantial page (replacing the placeholder that the current 4-0 implementation has).
- **No new dependencies required.** Tailwind + existing Astro components are sufficient. GSAP is the only candidate for removal.
- **No new routes.** The rewrite stays on `/` (current path). About, Philosophy, Changelog, Pricing, etc. are future stories in Epic 5 — not in scope here.
- **Performance budget:** current page ships a large inline SVG + GSAP runtime. New page is leaner. The PRD's "backlog loads in <1s for 10k tasks" NFR is unaffected (that's a logged-in view, not the marketing page).

---

## 3. Recommended Approach

**Selected path: Hybrid — insert new Epic 5 and pause Epic 4-1 → 4-6 backlog.**

### Rationale

- **Direct Adjustment (Option 1):** Viable in spirit (add a story, no rollback) but the marketing strategy is its own coherent body of work, not a tweak to an existing story. A new epic is the cleanest unit of organization.
- **Rollback (Option 2):** Not viable. No completed work conflicts with the marketing site; no rollback simplifies anything.
- **MVP Review (Option 3):** Not needed. MVP scope is unchanged — the marketing site is a public surface, not product functionality.
- **Hybrid (recommended):** Add Epic 5 to the sprint plan. Pause Epic 4-1 → 4-6 (defer until landing page ships). The pause is intentional sequencing, not a planning failure — landing page is a public-facing artifact that should be solid before further investment in the projector migration that depends on the Kurrent sync engine (which has not yet landed).

### Trade-offs considered

- **Cost of deferring 4-1 → 4-6:** all of those stories are pure backend / infrastructure. The first one that unblocks is 4-1 (shared TS libs), which is groundwork for 4-2 (projector port) and 4-3 (local-first read path) — all of which depend on the Kurrent TypeScript sync engine. The actual work that *can* be done today is small (4-0 wiring + 4-1 scaffolding); the substantive projector work is gated on the Kurrent release. So the practical cost of pausing is low.
- **Cost of not shipping the landing page first:** the marketing strategy exists, the brand voice is defined, and the public surface should match. Shipping the landing page establishes a coherent external presence that informs everything else.
- **4-0 review resolution:** landing 4-0 cleanly (commit `M nx.json`, flip to `done`) is the right order. Landing page work exercises the new layout and surfaces any gaps. If 4-0 is in flight when 5-0 starts, the new page still builds — it just inherits any review findings once 4-0 lands.

### Effort estimate

- **Story 5-0 (landing page only):** Low-to-medium effort. Pure frontend, one file, no backend. ~half-day to a day of focused work for a solo developer with the existing stack.
- **Subsequent Epic 5 stories (deferred, not in this proposal):** About/Philosophy, Changelog, OG images, design tokens — each scoped when picked up.

### Risk assessment

- **Risk: 4-0 review never resolves and blocks the layout.** Mitigation: 4-0 is in `review` with mostly cosmetic nits (the `M nx.json` is the visible diff). Resolve the review as part of the same push, then start 5-0.
- **Risk: marketing strategy not fully stable.** Mitigation: strategy is the spec and is approved. If changes emerge, they can be folded into a future story.
- **Risk: GSAP removal breaks something subtle.** Mitigation: GSAP is only used in `index.astro`'s `<script>` block. A grep confirms no other consumer. Remove cleanly.
- **Risk: visual register drifts toward "SaaS landing page" (gradients, shadows, mesh blobs, dot grids, hover scale-ups).** Mitigation: §4.1 specifies what to remove by name; acceptance criteria require no gradient backgrounds, no `shadow-*` utilities on CTAs, no `scale-` transforms. A reference pass against the Palantir screenshots in `_bmad-output/planning-artifacts/marketing-research/` keeps the register honest.
- **Risk: typographic reveal overused or misapplied.** Mitigation: §4.1 names the six elements that get the reveal; acceptance criteria forbid the reveal on body copy, lists, CTAs, and footer. If the implementation feels "decorative" rather than "emphatic," remove the reveal from that element.
- **Risk: voice / tone mismatch in implementation.** Mitigation: the strategy's "TL;DR for Agents" + "Spice Dial" are explicit. The acceptance criteria for 5-0 include a voice check (no banned words, no level-7+ copy outside the closer line, no methodology names).

---

## 4. Detailed Change Proposals

### 4.1 New Epic 5 — Marketing Site Refresh

**File:** `_bmad-output/planning-artifacts/epic-5-marketing-site-refresh.md` (to be created when 5-0 is picked up via `bmad-create-story`).

**Overview:** Refresh the public-facing surface of Velucid to match the brand voice, content spec, and credibility framing defined in `marketing_strategy.md`. Shipped incrementally: start with the landing page (the public front door), then extend to About/Philosophy, Changelog, and supporting marketing pages as follow-on stories.

**Target:** After Epic 5 lands, `velucid.app` reads as a confident, restrained, slightly unsettling product page in the dbrand × Palantir register — not a SaaS landing page. The credibility framing ("the method has been used in other industries for decades") is in the page without naming the methodology. The voice is consistent across all marketing surfaces.

**Out of scope (deferred stories within Epic 5):**
- About / Philosophy page
- Changelog page
- Pricing page (no public pricing model yet)
- OG / social share images
- Design tokens (Tailwind palette works as-is for this pass)
- About-the-team / careers (not part of the strategy)

**Story 5-0 — Landing Page Redesign**

What's needed:
- Rewrite `apps/web/src/pages/index.astro` to match the spec in `marketing_strategy.md` §"Landing Page: Hero Section Reference Draft."
- Five sections, in order:
  1. **Hero** — "Your last delivery date was wrong. / So was the one before it." + 2-line pitch + "Not a guess. A forecast. There's a difference." + CTAs "Try Velucid →" / "See how it works". Optional subtle product image (laptop / dashboard) styled in the Palantir register: full-bleed dark background, headline overlaid.
  2. **What's not here** — story points, sprint planning, velocity charts, burndown graphs, three-hour meetings (as deliberate omissions)
  3. **What is here** — board, backlog, forecasting engine, with a worked example: "70% likely by March 20, 95% by April 3"
  4. **Why this works** — credibility framing: the method has been used in finance, supply chain, engineering for decades; software is late to adopt
  5. **For the skeptic** — closing copy, ending on the level-6 line as a large pull-quote (typographic treatment à la Palantir's `image2.png` big quote-mark), not a button or banner: *"The only thing Velucid asks you to give up is the part that was never working anyway."*
- **Remove** the following from the current implementation: GSAP + ScrollTrigger, mesh-blob background, dot-grid textures, animated S-curve SVG (~150 lines inline), slogan chip + strikethrough trick, 3-step "How it works" grid, gradient CTA card, dashboard mockup card with mini kanban, hover scale-up on CTAs, soft shadows on cards.
- **Visual register:** flat black-on-white (or dark hero / white body, à la Palantir `image.png`), no gradients, no `shadow-*` utilities, no `scale-` transforms, no rounded-2xl cards with shadow, plain rectangular CTAs (outlined or filled-black), horizontal hairline dividers between sections. Reference shots: `_bmad-output/planning-artifacts/marketing-research/image.png` (hero), `image3.png` (CTAs), `image4.png` (footer directory).
- **Animation (typographic reveal as emphasis):** apply a left-to-right character reveal (CSS `clip-path` animation, no JS library) on these specific elements only:
  - The four section titles: "What's not here", "What is here", "Why this works", "For the skeptic"
  - The credibility callout: "70% likely by March 20"
  - The closer line: "The only thing Velucid asks you to give up is the part that was never working anyway."
  - Trigger: CSS scroll-driven animations or `animation-timeline: view()` so the reveal runs once when the element enters the viewport. Duration: 0.4–0.8s with an ease-out. **Do not** apply the reveal to body copy, lists, CTAs, footer text, or any other element. The reveal is a typographic emphasis device, not decoration.
- Replace "Get Started Free" with "Try Velucid →" (and add a secondary "See how it works" anchor link).
- Update the `<title>` and `<meta description>` to match the new copy.
- Mobile-responsive at 375 / 768 / 1280.
- **Remove `gsap` and `gsap/ScrollTrigger` from `apps/web/package.json`** after grep confirms no other consumer.

**Acceptance criteria:**
- Voice: no banned words (journey, passionate, empower, unlock, transform), no named methodologies or movements, no level-7+ copy outside the closer line, no exclamation points on serious statements.
- One level-6 line appears as the closer (typographic pull-quote, not a button or banner), with no other lines above level 5.
- Visual register: no gradient backgrounds, no `shadow-*` utilities on cards or CTAs, no `scale-` or `translate-` hover transforms, no mesh-blob or dot-grid backgrounds, no rounded-2xl cards with elevation. Plain rectangular CTAs (outlined or filled-black with arrow). Reference the Palantir shots in `_bmad-output/planning-artifacts/marketing-research/`.
- Typographic reveal: applied to the four section titles, the credibility callout, and the closer line — and to no other elements (no body copy, lists, CTAs, footer). Implementation is CSS-only (no JS library).
- CTA links: primary to `/auth/login`, secondary anchors to the "see how it works" section.
- Footer: keep the existing structure, restyle as a directory layout à la `image4.png`, update the tagline if needed to match the strategy.
- `bunx nx run web:lint typecheck build` passes; `bunx nx format:check` clean.
- The page is leaner than the previous version: no GSAP runtime in the bundle, no inline SVG of comparable size, no scroll-triggered animation library.
- Pre-push gate (`bun run preflight`) passes for the changed files; full e2e (Playwright) is allowed to fail in absence of the live stack (per CLAUDE.md guidance).

**Blocks:** None. Independent story.

**Files affected:**
- `apps/web/src/pages/index.astro` (rewrite)
- `apps/web/package.json` (remove `gsap` and `gsap/ScrollTrigger`)
- `CLAUDE.md` (one-line add: reference to `marketing_strategy.md` as the brand-voice source of truth)
- Reference for visual register during implementation: `_bmad-output/planning-artifacts/marketing-research/image.png` (Palantir hero), `image2.png` (typographic pull-quote), `image3.png` (plain CTAs), `image4.png` (footer directory)

### 4.2 Epic 4 — pause 4-1 → 4-6 backlog

No content change to the epic file. Pause is a sprint-status marker. The 2026-06-03 Nx proposal is the source of truth on what those stories contain.

### 4.3 sprint-status.yaml

**Proposed changes:**

```yaml
development_status:
  # Epic 1 — Core Platform Kanban MVP (paused pending Epic 4; now also pending Epic 5)
  epic-1: paused
  # (no change to per-story entries)

  # Epic 2 — Flow Features (paused)
  epic-2: paused
  # (no change to per-story entries)

  # Epic 3 — Authorization (paused)
  epic-3: paused
  # (no change to per-story entries)

  # Epic 4 — Nx Monorepo + Projector Migration (in-progress; 4-1 → 4-6 deferred until Epic 5 ships)
  epic-4: in-progress
  4-0-nx-monorepo-bootstrap: review
  4-1-shared-typescript-libraries: backlog
  4-2-port-projector-to-node-typescript: backlog
  4-3-wire-local-first-read-path: backlog
  4-4-nx-cloud-ci-integration: backlog
  4-5-local-dev-deploy-updates: backlog
  4-6-ci-web-e2e-infra-job: backlog
  epic-4-retrospective: optional

  # Epic 5 — Marketing Site Refresh (NEW; takes priority over Epic 4 backlog)
  epic-5: backlog
  5-0-landing-page-redesign: backlog
  epic-5-retrospective: optional
```

### 4.4 CLAUDE.md

**Section: add a single line under the "Repo in three lines" block, or a new "Voice & brand" section.**

NEW (proposed addition, 1–2 lines):
> **Brand voice & marketing copy:** `marketing_strategy.md` is the source of truth for landing-page, README, email, and any other public copy. Deviating from its voice (banned words, spice levels, credibility framing) requires explicit instruction.

Rationale: makes the strategy discoverable to future sessions; one-time documentation, not a per-story repetition.

### 4.5 PRD.md

No change. The marketing site is a presentation-layer concern. The product functionality, MVP scope, and feature requirements are unchanged.

---

## 5. Implementation Handoff

### Scope classification

**Moderate.** Backlog reorganization (add Epic 5, pause Epic 4-1 → 4-6), one story file creation, one source-file rewrite, one CLAUDE.md line addition. No PRD or architecture edits, no rollback, no replan.

### Handoff recipients and responsibilities

| Role | Responsibility |
|---|---|
| **Senior Software Engineer (you)** | Resolve 4-0 review (commit `M nx.json`, flip to `done`). Run `bmad-create-story` for Story 5-0 to produce the story file. Implement Story 5-0 via `bmad-dev-story`. Update `sprint-status.yaml` and `CLAUDE.md` per this proposal. |
| **PM (self, in this scope)** | Approved the marketing strategy as the spec. No additional PM review needed for the landing page rewrite; the strategy is the spec. |
| **Architect (self, in this scope)** | No architecture review needed — pure frontend, no new components or services. |

### Handoff deliverables

On approval, this proposal is the source of truth for:
- New Epic 5 in `sprint-status.yaml` (status `backlog`).
- New Story 5-0 in `sprint-status.yaml` (status `backlog`; will be flipped to `ready-for-dev` by `bmad-create-story`).
- Pause of Epic 4-1 → 4-6 (status stays `backlog`; deferral is documented in this proposal and the prior 2026-06-03 Nx proposal).
- One-line CLAUDE.md addition for the marketing strategy reference.
- Story 5-0 file produced by `bmad-create-story` (action: `create`) — runs in a fresh context.

### Success criteria for Story 5-0

- `apps/web/src/pages/index.astro` reads as the strategy intends: lean, direct, no gradients, no animations beyond tasteful entrance (and only if earned).
- Five sections present in order; closer line is the last thing on the page.
- No banned words in copy. No methodology names. No movement references.
- Page is faster / leaner than the previous version (smaller bundle, no GSAP runtime if removed).
- Pre-push gate passes for the changed files.

### Concrete next steps

1. Approve this proposal.
2. Update `sprint-status.yaml` to add Epic 5 + Story 5-0 entries.
3. Update `CLAUDE.md` with the marketing-strategy reference.
4. Resolve 4-0 review (commit the `M nx.json` change, get to `done`).
5. Run `bmad-create-story` (action: `create`) for Story 5-0 — produces the story file in implementation-artifacts.
6. Run `bmad-dev-story` to implement Story 5-0.
7. Run `bmad-code-review` when dev cycle is complete.
8. After 5-0 ships, resume Epic 4 (likely starting with 4-1 or whichever is most decoupled from the Kurrent sync engine).

---

## 6. Open Questions for User

These decisions affect the proposal. Defaults are stated; please confirm or override.

1. **Animation approach (revised after Palantir reference review, 2026-06-03):** the strategy's "the site just doesn't flinch" is consistent with selective, restrained motion used as a *typographic emphasis device* — not as a "look at this" flourish. Palantir uses a left-to-right character reveal on specific terms that matter (section titles, product names, credibility phrases, key callouts) — not on body copy, lists, CTAs, or footer. **Default for the new page:** use a CSS-only typographic reveal on the four section titles ("What's not here", "What is here", "Why this works", "For the skeptic"), the credibility callout ("70% likely by March 20"), and the closer line. Concrete prohibitions: no scroll-triggered card reveals, no chart-drawing or count-up animations, no entrance animations on cards / step items, no slogan strikethrough trick, no scale-up on CTAs, no mesh-blob / dot-grid textures, no `shadow-*` on cards, no `scale-` hover transforms. **Implementation:** native CSS `clip-path` reveal triggered via CSS scroll-driven animations or `animation-timeline: view()`; no JS animation library. **Remove `gsap` and `gsap/ScrollTrigger` from `apps/web/package.json`** after grep confirms no other consumer. *Confirm or override (e.g., "typographic reveal on titles only, no body emphasis" or "skip the reveal entirely").*
2. **Story 5-0 file location:** default — `_bmad-output/implementation-artifacts/5-0-landing-page-redesign.md` (created by `bmad-create-story`). *Confirm: standard convention, no override expected.*
3. **CLAUDE.md placement of the marketing-strategy reference:** default — a one-line addition under "Repo in three lines" or a new "Voice & brand" section. *Confirm placement or override.*
4. **4-0 review resolution:** default — resolve as part of this push, before starting 5-0. *Confirm or override (e.g., "park 4-0 and start 5-0 first").*
5. **Naming for the closer line in the implementation:** the strategy is explicit ("Do not add anything sharper. Do not follow it with another line."). Default — implement verbatim from the strategy, no improvisation. *Confirm.*

---

**END OF PROPOSAL**

Next step: review and approve, or send back with edits. On approval, this proposal becomes the source of truth for Epic 5 and the pause of Epic 4-1 → 4-6, and the next workflow is `bmad-create-story` (action: `create`) for Story 5-0.
