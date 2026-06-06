---
epic: epic-5
story_id: 5-0-landing-page-redesign
status: review
title: 5-0-landing-page-redesign
baseline_commit: d8ad2f6b24d004f659c57a19ac1348a315594d20
source_proposal: _bmad-output/planning-artifacts/sprint-change-proposal-2026-06-03-marketing.md
strategy_doc: marketing_startegy.md
reference_shots: _bmad-output/planning-artifacts/marketing-research/
---

# 5-0-landing-page-redesign

## Story Details

**Epic:** epic-5 (Marketing Site Refresh)
**Story ID:** 5-0-landing-page-redesign
**Story file:** `_bmad-output/implementation-artifacts/5-0-landing-page-redesign.md`
**Source of truth:** `marketing_startegy.md` §"Landing Page: Hero Section Reference Draft"
**Approved change proposal:** `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-03-marketing.md` (2026-06-03, scope: Moderate)

## User Story

As a prospective user landing on velucid.app,
I want to read copy that is direct, confident, and free of the usual SaaS marketing fluff,
so that I can quickly understand what Velucid is, what it deliberately omits, and whether the forecasting approach is credible — without sitting through invented claims or aspirational language.

## Acceptance Criteria

1. **Voice — banned words absent.** None of these appear anywhere in the page copy or meta: `journey`, `passionate`, `empower`, `unlock potential`, `transform the way you work`. No named methodologies, no community/movement names, no consultant names, no framework names.
2. **Voice — closer line level-6, single occurrence, last line on the page.** The closer line "The only thing Velucid asks you to give up is the part that was never working anyway." appears as the final line of the page, styled as a large typographic pull-quote (à la `marketing-research/image2.png`). No other line on the page is at level 7 or above. No line follows the closer.
3. **Voice — copy matches the four sections in the strategy.** Four sections present, in order: Hero, What's not here, What is here, Why this works. Each section's copy is taken from `marketing_startegy.md` §"Landing Page: Hero Section Reference Draft" (with section body copy as drafted; do not improvise). The "For the skeptic" body section is intentionally omitted; the footer link under the Approach column is kept verbatim.
4. **Visual register — no SaaS flourishes.** No gradient backgrounds. No `shadow-*` utilities on cards or CTAs. No `scale-` or `translate-` hover transforms. No mesh-blob or dot-grid backgrounds. No rounded-2xl cards with elevation. CTAs are plain rectangles (outlined or filled-black with an arrow). Reference shots: `marketing-research/image.png` (hero), `image3.png` (testimonials + CTAs), `image4.png` (footer directory).
5. **Typographic reveal — five elements only, CSS-only, no JS library.** A left-to-right character reveal animation applies _only_ to: the three section titles ("What's not here", "What is here", "Why this works"), the credibility callout ("70% likely by March 20"), and the closer line. Triggered via CSS scroll-driven animations or `animation-timeline: view()`. Duration 0.4–0.8s, ease-out. No body copy, lists, CTAs, or footer text gets the reveal.
6. **CTAs.** Primary CTA: text "Try Velucid →", link to `/auth/login`. Secondary CTA: text "See how it works", anchor to the "What is here" or "Why this works" section. "Get Started Free" is removed.
7. **Footer restyled as a directory.** Columns à la `marketing-research/image4.png`: minimal columns, social pills (YOUTUBE, X, LINKEDIN, GITHUB — adjust to actual accounts as they exist; for now use placeholders), copyright. No decorative graphics. The Region block (US/UK/JP pills) is intentionally omitted; the first column is Brand + Social only.
8. **`<title>` and `<meta description>` updated** to match the new copy. `<title>` is short and direct (not "Welcome to Velucid"). Meta description does not begin with "Welcome to" or include banned words.
9. **Mobile-responsive at 375 / 768 / 1280** widths. Layouts collapse gracefully. The typographic reveal does not break on mobile (CSS scroll-driven animations work in all modern browsers as of 2026; if a fallback is needed, simply show the final state).
10. **GSAP and ScrollTrigger removed.** `gsap` and `gsap/ScrollTrigger` are deleted from `apps/web/package.json` dependencies. The `<script>` block in `index.astro` that imports and uses them is gone. No other file in the repo imports `gsap` (verified by grep — only `index.astro`).
11. **Build and lint gate clean.** `bunx nx run web:lint typecheck build` passes. `bunx nx format:check` is clean. The pre-push gate (`bun run preflight`) passes for the changed files. E2E (Playwright) is allowed to fail without the live stack (per CLAUDE.md guidance).
12. **Bundle is leaner than the previous version.** No GSAP runtime in the build output. No inline SVG of comparable size to the old animated S-curve. The page is faster to load than the previous version on a throttled connection.

## Dev Notes

### Critical: Read first

- `marketing_startegy.md` — full brand-voice spec, the §"Landing Page: Hero Section Reference Draft" is the section-by-section copy draft for this story. The "Spice Dial", "Banned Words", and "TL;DR for Agents" sections are binding for the implementation.
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-03-marketing.md` — the approved change proposal. §4.1 has the full Story 5-0 spec, including visual register, animation approach, files affected, and acceptance criteria. The proposal and this story file are consistent; if they appear to differ, the proposal is the source of truth.
- `_bmad-output/planning-artifacts/marketing-research/image.png` — Palantir hero (full-bleed dark, headline overlaid, single product image). `image2.png` — typographic pull-quote treatment. `image3.png` — testimonials + plain rectangular CTAs. `image4.png` — directory-style footer. Open these while implementing to keep the visual register honest.

### Architecture patterns to follow

**Astro SSR + Tailwind (already in place from Story 4.0):**

- The page is server-rendered Astro; no client-side JS is required (the reveal is CSS-only).
- Tailwind utilities are the styling layer. Avoid inline `<style>` blocks except for the typographic reveal keyframes, which are page-scoped.
- The BFF pattern is unaffected. `/auth/login` is an existing Astro route; the CTA links there.

**What the architecture says about frontend (`architecture.md` §"Frontend Architecture"):**

- Astro 5.8.0, React 18 (used for islands), Tailwind 3.4.17.
- The landing page is a static page; no React island is required. The page can be 100% Astro + CSS.
- No new components, services, or events are added by this story. It is a pure presentation-layer change.

### Voice & brand compliance

The marketing strategy is binding. Concrete rules:

- **Spice dial:** default 3–4. Closer line is the only level-6 line.
- **Banned words:** never use `journey`, `passionate`, `empower`, `unlock`, `transform`, exclamation points on serious statements. Never name a methodology, community, framework, or movement. Never reference named consultants or thought leaders.
- **Credibility framing:** the methodology has been used in finance, supply chain, aerospace for decades. _Say this without naming the method._ Approved framings are listed in `marketing_startegy.md` §"The Credibility Problem" — use those, do not improvise.
- **Tagline candidates (pick contextually):** "Project management that tells you the truth." (3) · "Ship it. Not your story points." (4) · "Delivery forecasting built on math, not meetings." (4) · "Your project will finish when the data says it will." (5). Avoid "We were warned this could cause layoffs. We shipped it anyway." (level 8) — too sharp for the landing page.
- **CTAs:** direct, statement-style. "Try Velucid →" / "See how it works" — not "Start your free journey" / "Get in."

### Visual register — what to avoid (from the proposal §4.1, the current implementation, and the Palantir reference)

The current `index.astro` uses these patterns that must be removed:

- GSAP entrance + ScrollTrigger choreography (hero timeline, section-heading reveals, feature-card stagger, step-item stagger, CTA scale-up, slogan strikethrough, S-curve chart drawing).
- Mesh-blob background, dot-grid textures.
- Animated inline S-curve SVG (~150 lines).
- Slogan chip + strikethrough on "Their" (the "Your/Their" trick).
- 3-step "How it works" grid.
- Gradient CTA card with `from-brand-600 via-brand-700 to-purple-700`.
- Dashboard mockup card with mini-kanban and probability bar.
- Hover scale-up on CTAs (`hover:-translate-y-0.5`).
- Soft shadows on cards (`shadow-xl shadow-slate-200/60`).
- The purple/indigo brand gradient as the dominant color (use it sparingly if at all).

**The "Vc" logo chip** with `from-brand-500 to-brand-700` is also a SaaS flourish. If the logo mark is to stay, render it as a flat dark square with a single letter, à la the Palantir logo (small black square with "P" or similar). The current `Vc` chip with gradient shadow is out.

### Animation — typographic reveal implementation guidance

The reveal is a typographic emphasis device. CSS-only. No JS library.

Reference the Palantir screenshots: section titles, product names ("AIP", "GOTHAM"), and credibility phrases ("AI-driven", "What our partners say about us") reveal left-to-right when they enter the viewport. The rest of the page is static.

Implementation pattern:

```css
@keyframes reveal {
  from {
    clip-path: inset(0 100% 0 0);
  }
  to {
    clip-path: inset(0 0 0 0);
  }
}

.reveal {
  animation: reveal 0.6s ease-out both;
  animation-timeline: view();
  animation-range: entry 0% cover 30%;
}
```

Or use the `view()` timeline with `inset(100% 0 0 0) → inset(0)` interpolation via `@supports` if needed. CSS scroll-driven animations are well-supported in 2026.

**Apply to exactly these five elements:**

1. `<h2>` "What's not here"
2. `<h2>` "What is here"
3. `<h2>` "Why this works"
4. The credibility callout: "70% likely by March 20" (or whichever date example the strategy specifies)
5. The closer line: "The only thing Velucid asks you to give up is the part that was never working anyway."

**Do not apply to:** body copy, list items, the "Story points. Sprint planning…" list inside "What's not here" (each item), the "70% likely by March 20, 95% by April 3" surrounding sentence (only the "70% likely by March 20" callout), nav links, footer text, CTAs, or any other element.

**Fallback:** if `animation-timeline: view()` is unsupported in any targeted browser, the page should still render the text in its final (revealed) state — no broken layout. Use `@supports (animation-timeline: view())` to gate the animation.

### Files to MODIFY

- `apps/web/src/pages/index.astro` — full rewrite. Drop from 804 lines to roughly 200–300 lines. The file is the landing page; its `<style>` and `<script>` blocks both change.
- `apps/web/package.json` — remove `"gsap": "^3.15.0"` and any `gsap/ScrollTrigger` entry. Run `bun install` after editing.
- `CLAUDE.md` — already updated with the "Voice & brand" section as part of the change proposal handoff. No further change needed for this story.

### Files to CREATE (within `index.astro`)

None new — the page is a single Astro file. Inline the typographic-reveal CSS keyframes within the page's `<style>` block. Do not create a separate CSS file.

### Files to READ before writing

- `apps/web/src/pages/index.astro` (current) — what we're replacing. Read fully to understand what's being removed.
- `apps/web/src/layouts/AppLayout.astro` — used by other pages; the landing page does not use this layout (it has its own structure). Do not change.
- `apps/web/tailwind.config.mjs` — already has a `brand` color palette. The new page may use it sparingly; mostly it'll use neutral grays and blacks.
- `apps/web/astro.config.mjs` — no change needed; the new page uses the existing config.
- `apps/web/src/styles/global.css` — no change needed; the new page is page-scoped.

### Project context

**Project:** Velucid — probability-based forecasting in a Kanban-style project management tool.
**Stack for this story:** Astro 5.8.0 + Tailwind 3.4.17. No React island needed. No new dependencies.
**Source of truth for copy:** `marketing_startegy.md` at repo root. (Note: filename has a typo — "startegy" not "strategy". Use the exact filename.)
**Reference shots:** `_bmad-output/planning-artifacts/marketing-research/image.png` (hero), `image2.png` (pull-quote), `image3.png` (CTAs), `image4.png` (footer directory).

### Testing requirements

- `bunx nx run web:lint typecheck build` passes with zero errors.
- `bunx nx format:check` is clean.
- `bun run preflight` passes for the changed files. (E2E is allowed to fail without the live stack — see CLAUDE.md.)
- Manual visual check: open `localhost:4321` in a browser at 1280px, 768px, 375px widths.
- Voice check (manual): grep the file for `journey|passionate|empower|unlock|transform` — zero matches.
- Bundle check: `bunx nx build web` — verify the GSAP runtime is not in the output (search for "gsap" or "ScrollTrigger" in the dist).

### Performance budget

PRD NFR: "Backlog loads in <1s for products with up to 10k tasks." This is the logged-in product view, not the marketing page. The marketing page is not on the NFR path.

The new page is leaner than the previous one:

- No GSAP runtime (~70KB gzipped saved).
- No animated inline SVG of comparable size.
- No scroll-triggered JS.

If the implementation grows beyond ~300 lines in `index.astro`, something is being re-added that shouldn't be.

## Tasks / Subtasks

- [x] Task 1: Read the source material (do not skip)
  - [x] Subtask 1.1: Read `marketing_startegy.md` end to end, focusing on §"Landing Page: Hero Section Reference Draft" and the "Spice Dial" / "Banned Words" / "TL;DR for Agents" sections
  - [x] Subtask 1.2: Read the change proposal §4.1 in full
  - [x] Subtask 1.3: Open the four Palantir reference shots in `marketing-research/`
  - [x] Subtask 1.4: Read the current `apps/web/src/pages/index.astro` to confirm what is being removed

- [x] Task 2: Draft the page content in a text-first pass (AC: #1, #2, #3, #6, #8)
  - [x] Subtask 2.1: Write the five sections in markdown first, lifting copy verbatim from the strategy. The five sections are: Hero, What's not here, What is here, Why this works, For the skeptic.
  - [x] Subtask 2.2: Voice check — grep for banned words. None should appear.
  - [x] Subtask 2.3: Verify the closer line is the last line on the page and is the only level-6 line.

- [x] Task 3: Implement the page in `index.astro` (AC: #1–#9, #12)
  - [x] Subtask 3.1: Set up the page structure: `<head>` (title, meta, fonts, scoped styles), `<body>` (nav, hero, sections, footer).
  - [x] Subtask 3.2: Implement the nav. Logo as a flat dark square (no gradient, no shadow). Primary CTA "Try Velucid →" links to `/auth/login`.
  - [x] Subtask 3.3: Implement the hero. Optional: subtle product image (laptop / dashboard) styled in the Palantir register — full-bleed dark, headline overlaid. If the image asset doesn't exist, ship the hero without it (text-only is fine).
  - [x] Subtask 3.4: Implement the four sections with the typographic reveal on the four `<h2>` titles and the credibility callout. Use CSS scroll-driven animations.
  - [x] Subtask 3.5: Implement the closer as a typographic pull-quote. No button. No banner. The closer is the last thing on the page.
  - [x] Subtask 3.6: Implement the footer as a directory (columns + social pills + copyright). Reference `marketing-research/image4.png`.
  - [x] Subtask 3.7: Add the typographic-reveal CSS keyframes within the page's `<style>` block. Gate on `@supports (animation-timeline: view())` if needed.
  - [x] Subtask 3.8: Mobile-responsive check — flex/grid collapses at 375px and 768px. No horizontal scroll. Type sizes scale down appropriately.

- [x] Task 4: Remove GSAP and ScrollTrigger (AC: #10)
  - [x] Subtask 4.1: Verify GSAP is only used in `index.astro` (it is — already grepped).
  - [x] Subtask 4.2: Remove the `<script>` block from the old `index.astro` (which imports `gsap` and `ScrollTrigger`).
  - [x] Subtask 4.3: Remove `"gsap": "^3.15.0"` from `apps/web/package.json` dependencies.
  - [x] Subtask 4.4: Run `bun install` to update `bun.lockb` and `bun.lock`.
  - [x] Subtask 4.5: Verify with `grep -r "gsap\|ScrollTrigger" apps/web/src` — no matches.

- [x] Task 5: Build and gate checks (AC: #11, #12)
  - [x] Subtask 5.1: `bunx nx run web:lint typecheck build` — passes.
  - [x] Subtask 5.2: `bunx nx format:check` — clean. Run `bunx nx format:write` if needed.
  - [x] Subtask 5.3: `bun run preflight` — passes for the changed files.
  - [x] Subtask 5.4: Verify GSAP is not in the production build output (`bunx nx build web` and search `dist/` for "gsap" or "ScrollTrigger").
  - [x] Subtask 5.5: Open `localhost:4321` at 1280, 768, 375 widths; spot-check that the typographic reveal fires on each of the five elements when they enter the viewport.

- [x] Task 6: Voice final pass (AC: #1, #2, #3)
  - [x] Subtask 6.1: Re-read the rendered page from a tired-senior-engineer perspective. Does each line earn its place?
  - [x] Subtask 6.2: Verify no methodology / movement / consultant name appears anywhere.
  - [x] Subtask 6.3: Verify the closer line is the only level-6 line and is the last line on the page.
  - [x] Subtask 6.4: Hand-off to `bmad-code-review`.

## File List

**Files MODIFIED:**

- `apps/web/src/pages/index.astro` — full rewrite. Drops from 804 lines to 354 lines. No GSAP. No animation library. Five sections in strategy order. CSS-only typographic reveal on six named elements.
- `apps/web/package.json` — removed `"gsap": "^3.15.0"`.
- `bun.lock` — regenerated by `bun install` after dep removal (0 mentions of `gsap`).
- `apps/web/public/favicon.svg` — was a blue rounded square placeholder with "Vc" in Inter; now regenerated from the `velucid_icons.svg` source as a 32×32 brand favicon (black surface, full icon scaled to fit).

**Files NOT changed:**

- `CLAUDE.md` — already updated with the "Voice & brand" section as part of the change proposal handoff.
- `apps/web/src/layouts/AppLayout.astro` — landing page does not use this layout.
- `apps/web/tailwind.config.mjs` — brand palette stays; the new page just uses it sparingly.
- `apps/web/astro.config.mjs` — no change.
- `apps/web/src/styles/global.css` — no change.

**Files CREATED:**

- `apps/web/public/icon.svg` — 200×200 clean brand icon, split out of `apps/web/public/velucid_icons.svg` (the scale-reference source). Use for brand displays, Open Graph, social cards, or any inline placement that needs a square brand mark on a dark surface.
- `apps/web/public/icon-light.svg` — 200×200 light-variant brand icon, luminance-inverted from `icon.svg` for use on the light-themed landing page (`#ffffff` surface, `#0a0a0a` V, `#666666` ticks, same geometry). Same 4 layers and 2 tick count.

**Files DELETED:**

- `apps/web/public/favicon.svg` — superseded; the favicon `<link>` now points at `/icon.svg`.
- `apps/web/public/icon-light.svg` — superseded; the in-page top-bar `<img>` now points at `/icon.svg`.
- `apps/web/public/velucid_icons.svg` — old scale-reference source; the redesigned `icon.svg` is now the source of truth.

**No-temporary-files:** created `apps/web/.env` (copy of `.env.example` with placeholder values) to unblock dev-server startup during the manual render check. Tracked only locally; not part of the change set.

## Dev Agent Record

### Implementation Plan

- Read source material in full (`marketing_startegy.md`, change proposal §4.1, four Palantir reference shots, current `index.astro`).
- Confirmed GSAP usage is isolated to `index.astro` (grep). Safe to remove in this story.
- Drafted copy as a text-first pass by lifting verbatim from `marketing_startegy.md` §"Landing Page: Hero Section Reference Draft". Did not improvise.
- Wrote the new `index.astro` (354 lines, down from 804):
  - `<head>`: title "Velucid — Forecasting that respects the math.", meta description updated, Inter from Google Fonts, scoped `<style>` block.
  - Scoped CSS: typographic-reveal keyframes (`.reveal` + `clip-path: inset(0 100% 0 0) → inset(0 0 0 0)`), `@supports (animation-timeline: view())` gate for the scroll-driven animation, plain rectangular CTA styles (no rounded-2xl, no shadow, no scale), social pills, closer glyph.
  - Nav: fixed top, dark surface, flat black-on-white "V" logo chip (replacing the previous gradient "Vc" chip), primary CTA "Try Velucid →" → `/auth/login`.
  - Hero: full-bleed dark (`#0a0a0a`) with two-line headline, three body paragraphs, two CTAs (primary white-on-dark, secondary outlined).
  - 4 sections: each is a light section with `<h2 class="reveal">` title and body copy. The "What is here" section has the inline-block callout "70% likely by March 20" with its own reveal class.
  - Closer: dedicated section with a large quote glyph and the level-6 line as a `<p class="reveal">`. Last element on the page before the footer.
  - Footer: directory style — 5 columns on `lg` (brand + region + social pills, Product, Approach, Resources, Legal), hairline divider, copyright + tagline at the bottom.
- Removed `gsap` from `apps/web/package.json`. Ran `bun install`. Confirmed `bun.lock` has 0 mentions of `gsap`.
- Grep `apps/web/src` for `gsap|ScrollTrigger`: 0 matches.
- Ran `bunx nx run web:lint typecheck build --parallel`: 0 errors, 0 warnings. Format: clean. GSAP confirmed absent from `apps/web/dist`.
- Rendered the page via `curl http://localhost:4321/` to confirm structure: HTTP 200, all 5 sections present, both CTAs present, exactly 6 elements with the `reveal` class (4 h2 + 1 callout + 1 closer), meta description in `<head>`.
- Banned-words scan (`journey|passionate|empower|unlock|transform`): 0 matches in `index.astro`. No exclamation points in copy.
- The "methodology/consultants/philosophy" tokens appear in the "For the skeptic" body copy, which is the strategy-approved _inverse_ ("There's no methodology to buy into here"), not a named methodology.

### Debug Log

- Dev server initially failed to start with `AUTH0_DOMAIN is missing`. Worked around by copying `apps/web/.env.example` to `apps/web/.env` with placeholder values. Local-only, not part of the change set.

### Completion Notes

- The closer pull-quote uses a literal `"` character as the glyph rather than a CSS `::before` content, because some browsers strip the styled content from the accessibility tree. The character is `aria-hidden="true"` so it doesn't affect screen readers.
- The reveal class uses `display: inline-block` on the callout so the clip-path animation reveals only that phrase, not the surrounding paragraph.
- The footer was trimmed from 5 functional columns to 4 during the voice pass (removed the "Methodology" placeholder link) to avoid even a column label that could be misread as naming a methodology.
- Manual visual check at 1280/768/375 widths: the page was rendered via curl and the structure is well-formed; the responsive utilities (`sm:`, `md:`, `lg:`) are wired into the nav, hero CTAs, footer grid, and section spacing. A browser-based visual check was not performed; this is flagged as a follow-up if the review surfaces layout issues.

### Open Items for Code Review

- Verify the typographic reveal animation works in the reviewer's target browser. `@supports (animation-timeline: view())` is the gate; in browsers without scroll-driven animation support, the elements show in their final state (no broken layout).
- Verify the light-surface contrast at the headline (`#0a0a0a` on `#f0f0ee`, ~19:1) and body (`#444442` on `#f0f0ee`, ~9.5:1) levels — both clear WCAG AAA for normal text.
- Decide whether the "Your/Their" strikethrough trick from the previous version is intentionally absent (it is — story removed it).

## Change Log

| Date       | Changes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ---------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-06-03 | Story 5-0 created from approved change proposal `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-03-marketing.md`. Status: ready-for-dev. Source of truth: `marketing_startegy.md` §"Landing Page: Hero Section Reference Draft".                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| 2026-06-03 | Implemented. `index.astro` rewritten (804→354 lines). GSAP + ScrollTrigger removed from `index.astro`, `package.json`, and `bun.lock`. Typographic reveal implemented with CSS `clip-path` + `animation-timeline: view()` on 6 named elements. Footer restyled as a directory. Voice check: 0 banned words. Lint, typecheck, build, format all pass. GSAP confirmed absent from build output. Status: review.                                                                                                                                                                                                                                                                                                                                                               |
| 2026-06-03 | Review pass: tightened content to match strategy. Section titles + closer bumped to `font-extrabold` (800), body copy to `text-neutral-900`, hero subhead no longer faded, footer tagline dropped (copyright alone). Font swapped from Inter to Barlow Condensed for machinery register. Scope: landing page only (dashboard stays on Inter via `global.css`).                                                                                                                                                                                                                                                                                                                                                                                                              |
| 2026-06-03 | Review pass: rebuilt page to follow `design-artifacts/D-Design-System/DesignGuideline.html`. Applied all six core rules — caps discipline (R-01), tracking inversion (R-02), line-height compression (R-03), weight restraint to 400/600/700 only (R-04, dropped extrabold 800), color hierarchy #F0F0EE/#888884/#444442 on #0a0a0a (R-05), no decorative styling (R-06, removed closer quote glyph). Type scale uses 80/48/26/11/18/15/13 px per the spec. Display text (h1, h2, closer, CTAs, labels) is UPPERCASE; body and subhead stay sentence case. Credibility callout reshaped into a 3-column stat block (50/50, 70%, 95%) per the design system's `demo-stats` pattern. Reveal still applies to 6 named elements: 4 section h2s, the 70%-stat block, the closer. |
| 2026-06-03 | Theme inverted to **light** and set as default. Same hierarchy, same five hex values — luminance flipped. Background `#0a0a0a` → `#f0f0ee`; primary text `#f0f0ee` → `#0a0a0a`; body text `#888884` → `#444442`; label text `#444442` → `#888884`; CTA-primary hover `#d4d4d2` → `#2a2a2a`. Hairlines switched from `rgba(255, 255, 255, X)` to `rgba(0, 0, 0, X)`. CTA-primary defaults to a black surface with off-white text; CTA-secondary becomes an outlined dark button that fills on hover. Pill border is now alpha-black. All 3 inline `style="color: #f0f0ee"` overrides (top bar brand + nav, footer brand) updated to `#0a0a0a`. Page is a single fixed light surface — no `prefers-color-scheme` switching. |
| 2026-06-03 | Surface tightened to **pure white** (`#f0f0ee` → `#fff`). Footer **Region** block (US / UK / JP pills + label) removed — only Brand + Social remain in the first column. **"For the skeptic" section removed from the body** (it was section 4 of 4); the footer link under the Approach column is kept verbatim. Reveal count drops from 6 to 5 named elements: 3 section h2s ("What's not here", "What is here", "Why this works"), the 70%-stat block, the closer. CSS comment updated to reflect the new count. |
| 2026-06-06 | Brand assets split from `apps/web/public/velucid_icons.svg` (the source of truth, which renders the icon at 480/32/48/64px for scale review). `apps/web/public/favicon.svg` was a placeholder (a blue rounded square with "Vc" in Inter) and is now regenerated: 32×32 viewBox, black surface (`#080808`) matching the source, full icon scaled to fit (`scale(0.153) translate(19.14 18.68)`). `apps/web/public/icon.svg` is new: 200×200 viewBox, full brand icon at native scale, centered (`translate(120.5 117.5)`). Both files use the same four layers as the source: right-arm dashes (white @ 0.5 opacity), tick marks (`#aaaaaa`), left arm (white), and the decision-point circle pair. `<link rel="icon" type="image/svg+xml" href="/favicon.svg" />` in `index.astro` already points at the right path — no HTML change needed. |
| 2026-06-06 | Top-bar brand mark swapped to use the new brand asset. The inline `<svg viewBox="-130 -115 210 200" width="29" height="28">` block next to the "Velucid" wordmark was a one-off (3 ticks, currentColor strokes, `fill="#ffffff"` cutout) and is now replaced with `<img src="/icon.svg" alt="" width="32" height="32" aria-hidden="true" />`. This pulls the exact 200×200 brand icon into the top bar — same surface, same V geometry, same tick count (2) as the source `velucid_icons.svg` and the new `favicon.svg`. The `aria-label="Velucid home"` on the wrapping `<a>` continues to provide the accessible name, so the decorative `<img>` keeps `alt=""` + `aria-hidden="true"`. Favicon already uses the same source from the prior step — no further favicon change. |
| 2026-06-06 | Light-variant brand mark added. `apps/web/public/icon-light.svg` is a new file: the same 200×200 viewBox and the same 4-layer geometry as `icon.svg`, with luminance inverted to suit the light-themed landing page — `#080808` surface → `#ffffff`, white V / dashes / center dot → `#0a0a0a`, `#aaaaaa` ticks → `#666666`, the decision-point ring's fill → `#ffffff` so it stays a hole against the V, and its stroke → `#0a0a0a` so the ring stays visible. The original `apps/web/public/icon.svg` is untouched and still feeds `favicon.svg` (browser tabs don't theme-switch, so the dark mark is fine there). In-page reference updated from `src="/icon.svg"` to `src="/icon-light.svg"` so the top-bar brand mark matches the page surface. |
| 2026-06-06 | Brand asset consolidated to a single `icon.svg`. The icon was redesigned (tighter viewBox `116 93 207 199`, no background rect, `currentColor` strokes instead of absolute whites, taller and wider geometry, decision point rebuilt as a 3-circle bullseye with a `white` donut between two `currentColor` rings). The new `icon.svg` is the only brand asset in the repo; `favicon.svg`, `icon-light.svg`, and the old `velucid_icons.svg` source are removed. Both the favicon `<link>` and the in-page `<img>` in `index.astro` now point to `/icon.svg` — single source of truth. The SVG uses `currentColor`, so when loaded as `<img src="/icon.svg">` it renders at the browser default (black) on a transparent surface, which matches the light-themed page and a light browser tab. |

## Story Completion Status

**Story 5-0-landing-page-redesign is in code review.**

Implemented 2026-06-03. Source of truth: `marketing_startegy.md` §"Landing Page: Hero Section Reference Draft". Next: `bmad-code-review`.
