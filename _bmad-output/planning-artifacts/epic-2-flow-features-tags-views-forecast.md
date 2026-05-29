# Epic 2: Flow Features — Tags, Saved Views, Forecast

## Overview

Build on the core platform to add the features that make Velucid useful: tag filtering, per-user saved views, and the probabilistic forecast with Monte Carlo simulation.

**Target:** A team actively using Velucid can filter tasks by tags, save named filter configurations, and — once they have 7+ days of task history — see a probabilistic completion forecast.

**Out of Scope:** Real-time collaborative editing (WebSockets), public API, email notifications.

---

## Story 2.1: Tags

**What's needed:**
- Tag support is already partially in `TaskProjection` (tags as jsonb)
- Add `TagAdded` and `TagRemoved` events to `TaskGrain`
- Tag autocomplete: Silo API endpoint returns previously used tags per product
- Astro API route: `GET /api/products/{productId}/tags` (for autocomplete)
- Frontend: tag input with autocomplete dropdown, tag chips on task cards

**Acceptance criteria:**
- User can add a tag `area:backend` to a task — stored as-is
- Autocomplete suggests tags previously used in the same product
- Tags appear as chips on task cards in backlog and kanban views
- Backlog/kanban can filter by tag include/exclude

---

## Story 2.2: Saved Views

**What's needed:**
- Saved view model: user_id, product_id, name, filter_config (jsonb), sort_config (jsonb)
- Can live in `UserProjection` or a dedicated `SavedViewProjection` entity
- `SavedViewGrain` or stored as part of user preferences
- Astro API routes: `GET/POST /api/products/{productId}/views`, `PUT/DELETE /api/products/{productId}/views/{viewId}`
- Frontend: save filter button, saved views dropdown, instant switching

**Acceptance criteria:**
- User can apply filters/sort on kanban board, click "Save this view," name it, and it appears in the views list
- Clicking a saved view instantly applies its filter + sort (no page reload)
- Saved views are per-user — not shared
- User can delete a saved view

---

## Story 2.3: Forecast Engine (Monte Carlo)

**What's needed:**
- `ForecastService` — reads event stream (or pre-aggregated time series table) to compute daily completed_count and scope_count
- Runs 10,000 Monte Carlo simulations, produces CDF
- Caches result in memory or Redis; invalidates on new event
- `ForecastController` on Silo — `GET /api/products/{productId}/forecast`
- Returns: `{ p50_date, p70_date, p95_date, never_finish_pct, spread_health, cdf_points }`

**Acceptance criteria:**
- Server runs 10,000 Monte Carlo simulations per forecast request
- CDF returned to client with all 10,000 daily percentile points
- Result is cached; next request within same time window returns cached result
- Minimum 7 days of history required — below that, returns `{ status: "gathering_data" }`

---

## Story 2.4: Forecast UI — Stat Cards + S-Curve

**What's needed:**
- Frontend: Forecast tab replaces "gathering data" shell with real chart
- Three stat cards: 50% date, 70% "Planning date" (highlighted), 95% date
- Threshold slider: 50–99%, updates all cards and chart crosshair instantly
- Primary view: S-curve (probability CDF) — X=date, Y=probability
- Hover tooltips: show probability of completion at hovered date
- Never-finish indicator as a fourth stat card with severity-appropriate styling

**Acceptance criteria:**
- S-curve renders with blue line (#378ADD) and amber threshold line (#EF9F27)
- Slider updates stat card dates without re-running simulations (uses cached CDF points)
- Never-finish displays as fourth card when >0% of simulations never completed

---

## Story 2.5: Forecast UI — Progress & Forecast Chart (Dual-Cone)

**What's needed:**
- Secondary view toggle: "Progress & Forecast"
- Historical completed and scope lines (solid)
- Projected cones (dashed bands for p10–p90)
- Intersection region (amber fill where cones overlap)
- Today marker (vertical dashed line)
- Confidence date line (vertical amber at slider-selected date)

**Acceptance criteria:**
- Dual-cone chart renders with correct line colors and band opacities
- Intersection region (amber ~35% opacity) visually represents probable completion window
- Hover tooltip shows: completed count, scope count, WIP gap, probability of completion at date

---

## Story 2.6: Forecast — Tag-Based Filtering

**What's needed:**
- Forecast tab includes tag filter controls
- Selecting tag(s) recalculates forecast for filtered subset (same Monte Carlo engine, filtered query)
- Recalculation: under 2 seconds

**Acceptance criteria:**
- User can filter forecast to show only tasks with tag `team:backend`
- All stat cards and charts update to reflect filtered subset
- "Gathering data" state shown if filtered subset has <7 days of history

---

## Retrospective 2: Epic 2 Review

**Purpose:** Assess forecast quality, tag utility, and saved views adoption.

**Topics to cover:**
- Did the Monte Carlo p70 date feel accurate after a few weeks of use?
- Did saved views reduce friction for different team members working differently?
- Was the dual-cone chart understandable to non-technical stakeholders?
- What's next: real-time collab, notifications, org analytics
