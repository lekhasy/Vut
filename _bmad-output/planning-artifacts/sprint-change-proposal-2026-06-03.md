# Sprint Change Proposal — Nx Monorepo + TypeScript Projector Migration

**Project:** Velucid
**Date:** 2026-06-03
**Author:** bmad-correct-course workflow
**Status:** DRAFT — pending approval
**Scope classification:** Major — fundamental replan required

---

## 1. Issue Summary

### Triggering context

Kurrent (the event store vendor) is shipping a TypeScript sync engine library in the coming weeks. The library exposes KurrentDB events to a frontend client via a catch-up subscription pattern. Once it lands, the Velucid frontend will need to consume events and project them into a local read model — the same projection logic the backend projector runs today.

The backend projector is currently a .NET 10 `BackgroundService` (`Velucid.ProjectorService`). If the frontend reimplements projection logic in TypeScript, the two implementations will drift over time — different event-format assumptions, different read-model shapes, different edge cases.

To prevent that, the projector service must move to Node.js/TypeScript so the same code (`@velucid/projection`) can be consumed by both:
- The backend Node projector (subscribes to `$ce` via KurrentDB Node client)
- The frontend (subscribes via the new Kurrent TypeScript sync engine)

To host a TS frontend, a TS Node backend, and shared TS libraries together, the entire repo moves into an **Nx monorepo**, with **Nx Cloud** providing the remote cache + affected graph for CI and local builds.

### Why now

- The Kurrent sync engine is on a near-term timeline. If the repo isn't ready for code-sharing when it lands, the velocity cost of duplicated-and-drifting projection code is permanent.
- Active work (Epic 1 stories 1-2 onwards, all of Epic 2) is mostly stubbed projector code that hasn't been written yet — the migration cost is bounded because there's no working .NET projector code to port (only `OrgProjector` and `UserProjector` exist, both small).
- The OpenFGA tuple-write code moved into the projector in story 3-2 — that small surface is also part of the port.

### Evidence

- Kurrent team confirmation: library is TypeScript, catch-up subscription model, sends events to the frontend.
- Today's projector surface: `Velucid.ProjectorService` has only `OrgProjector.cs` and `UserProjector.cs` handlers, plus `OpenFgaTupleSync.cs`. ~3 files of working code need to be ported.
- Stories 1-2, 1-3, 2-1 through 2-6 are currently 200–400 byte stubs (verified in `implementation-artifacts/`) — no working .NET projection code to port for those.

---

## 2. Impact Analysis

### Epic Impact

| Epic | Status today | Impact | Action |
|---|---|---|---|
| **Epic 1 — Core Platform Kanban MVP** | in-progress; 1-0 + 1-1 done; 1-2 → 1-5 backlog stubs | Stories 1-2, 1-3 are **projector-side** — will be re-scoped to TS projection in new libs | Pause; re-scope in migration epic |
| **Epic 2 — Flow Features (Tags, Saved Views, Forecast)** | backlog; all 6 stories are stubs | **All projector-side and frontend-side** — re-scope to TS | Pause; re-scope in migration epic |
| **Epic 3 — Authorization (OpenFGA)** | in-progress; 3-1 + 3-2 done; 3-3 → 3-6 backlog | Stories are mostly **grain-side**; 3-2's projector-side OpenFGA code needs to be ported to TS as part of the migration | Pause 3-3 → 3-6; track port as part of new epic |
| **NEW Epic 4 — Nx Monorepo + Projector Migration** | — | New epic, must run first before any other story resumes | Add to sprint plan |

### Story Impact

- **Already done (1-0, 1-1, 3-1, 3-2):** no regression. 1-0 (React foundation) and 1-1 (orgs) are frontend + grain + projector work; both will live in the new layout. 3-1 and 3-2 are grain-side and projector-side respectively; the projector-side code moves with the rest of the projector.
- **Backlog stubs (1-2 → 1-5, 2-1 → 2-6, 3-3 → 3-6):** all paused. They will be re-scoped inside or after the migration epic.
- **No completed story is rolled back.**

### Artifact Conflicts

| Artifact | Conflict | Resolution |
|---|---|---|
| **PRD.md** | No direct conflict. §4.9 architectural constraints still hold. Local-first approach strengthens "fast and minimal" UX goal. | Minor edits: add "Local-First Frontend" to Goals; add a note in §4.9 about the read path. No scope change. |
| **architecture.md** | Major conflicts. Current architecture is .NET-centric with a single Astro BFF. The new architecture has Nx workspace, Node projector, shared TS libs, and a local-first read path. | Treat current doc as **v1 (as-is)**. Generate a new **architecture-v2-nx.md** via `bmad-create-architecture` as Story 4.0.1. v1 stays for reference until v2 is approved. |
| **Epics 1, 2, 3** | Story files reference specific .NET file paths (`backend/src/Velucid.Silo/...`, `backend/src/Velucid.ProjectorService/...`) that won't exist after the move. | After migration, update story file paths in epic 1-3 to reference the new Nx layout. Until then, paths are stale but no implementation work is in flight to use them. |
| **sprint-status.yaml** | Doesn't reflect new epic 4; shows epic 1, 2, 3 as in-progress/backlog. | Add `epic-4: backlog`; mark `epic-1`, `epic-2`, `epic-3` as `paused` (new status — see Open Question below). |
| **infrastructure/** (k3s manifests, docker-compose, start.sh) | K3s pod for `projector-service` runs .NET 10 image. Will be replaced by Node image. | Updated as part of Story 4.5. |
| **CI/CD (GitHub Actions)** | Single-pipeline build for whole repo. Nx Cloud replaces with affected-graph + remote cache. | Updated as part of Story 4.4. |
| **new-machine-setup.md** | Doesn't mention bun/Nx. | Updated as part of Story 4.5. |
| **PRD §4.9 "No direct database access by clients"** | Still satisfied — frontend reads from its own local projection, not from Postgres directly. Backend projector remains the sole writer. | No PRD change needed; clarify in architecture-v2. |

### Technical Impact

- **New runtime dependency:** Bun 1.3+ (package manager AND JS runtime — replaces Node.js 20+ LTS and pnpm), `@nx/*` packages, `@openfga/sdk`, `@kurrent/kurrentdb-client`, Postgres client (`pg` / `drizzle-orm` / `kysely`).
- **Container images:** `ghcr.io/lekhasy/vut/projector-service` switches base from `mcr.microsoft.com/dotnet/runtime:10.0` to `node:20-alpine`. Image build pipeline changes.
- **Build orchestration:** Nx workspace, `nx.json`, `project.json` per app/lib, Nx Cloud workspace token.
- **Local dev experience:** `bun install` at root; `nx run-many -t serve` boots all dev servers; `nx affected -t test` for affected tests; `nx affected -t build` for affected builds.
- **TypeScript everywhere on the read path:** `libs/events` (event type definitions), `libs/projection` (pure projection functions), `libs/read-model` (read-model DTOs). Both projector and frontend import from these.
- **Orleans Silo stays .NET.** No grain code changes from this proposal. The only .NET ↔ TS boundary is the event stream format (KurrentDB) plus the event-type names (which are PascalCase strings in the stream — see architecture.md §"Event Type Names").
- **Astro BFF stays.** It still handles auth (session cookie validation) and proxies writes from the web client to the Silo. The local-first read path means reads no longer need to go through the BFF for projection data — but the BFF remains for the foreseeable future.

---

## 3. Recommended Approach

**Selected path: Hybrid — pause and replan with a new migration epic.**

### Rationale

- **Direct Adjustment (Option 1):** Not viable alone. Adding monorepo setup, Node runtime, shared libs, and a brand-new projector stack is bigger than story-level changes. It needs its own epic with its own acceptance criteria.
- **Rollback (Option 2):** Not viable. Already-done stories (1-0, 1-1, 3-1, 3-2) are valuable and grain-side code is unaffected. The only porting work is the small projector surface (3 files). Rollback would lose progress for no simplification.
- **MVP Review (Option 3):** Not viable. MVP is still achievable. Local-first actually improves the UX target. No scope reduction is needed.
- **Hybrid (recommended):** Pause all in-flight and backlog epics. Add Epic 4 to do the migration. After Epic 4 lands, resume Epic 1 stories 1-2 onwards (now in the new layout), then Epic 3 stories 3-3 onwards, then Epic 2.

### Trade-offs considered

- **Cost of waiting for Kurrent sync engine:** Doing the migration now before the Kurrent lib lands means we don't have the actual sync-engine API to design `libs/kurrent-client` against. The library will be designed against the official KurrentDB Node client first, then extended when the sync engine drops.
- **Cost of deferring the migration:** The window is narrow. If we don't land it before the Kurrent lib ships, we'll be retrofitting code sharing under deadline pressure with two divergent code paths already in flight.
- **Nx + .NET:** Nx has first-class support for Node/TS, but .NET support is community / plug-in. We're accepting that `apps/silo` lives in the Nx workspace for visibility but its build is still `dotnet build` and its tests are still `dotnet test`. Nx will run them as "external" tasks.

### Effort estimate

- Epic 4 (this proposal): Medium-High effort, ~1-2 weeks of focused work for a solo developer with Nx experience.
- Subsequent epics: roughly the same effort as before, with the small added cost of TS projection code instead of C#.

### Risk assessment

- **Risk: Kurrent sync engine API diverges from assumption.** Mitigation: `libs/kurrent-client` is a thin adapter; the projection code doesn't depend on the Kurrent client directly. The projection code consumes events, not Kurrent-specific objects.
- **Risk: Nx + .NET tooling friction.** Mitigation: Nx has `@nx-dotnet/dotnet` community plugin; alternatively, treat Silo as a `nx:run-commands` task that wraps `dotnet build` / `dotnet test`. We're not losing anything by having it in the workspace.
- **Risk: OpenFGA SDK differences between .NET and Node.** Mitigation: tuple write logic is trivial; we already moved it to the projector in 3-2 to centralize it. Porting `OpenFgaTupleSync` to `@openfga/sdk` is mechanical.
- **Risk: Scope creep.** Mitigation: migration epic ends at "running on the new layout, all existing tests pass." Forecasting and local-first read path improvements are downstream stories, not part of the migration.

---

## 4. Detailed Change Proposals

### 4.1 PRD.md

**Section: §2 Goals & Non-Goals → Goals**

OLD:
> - Provide a fast, opinionated task management tool with a #noestimate philosophy baked in.
> - Deliver a single report -- a probabilistic forecast powered by Monte Carlo simulation -- that replaces the need for velocity charts, burn-downs, and story points. Every forecast is expressed as a probability distribution, never a single date.
> - Support multi-tenant, multi-org usage via GitHub SSO, following GitHub's organization model.
> - Build on an event-sourced architecture so that all state changes are auditable, replayable, and suitable for the analytical needs of the probabilistic forecast.
> - **Self-hostable on developer machines:** ...
> - Ship an MVP that is immediately useful for a single team managing a product backlog and kanban board.

NEW (insert as the 5th goal, before "Self-hostable"):
> - **Local-first frontend:** The web client maintains its own read model by projecting events received via the Kurrent TypeScript sync engine, so the UI renders without round-trips to the BFF for read paths. Writes still flow through the BFF. The same projection code runs in both the backend projector and the frontend, eliminating code drift.

Rationale: Captures the architectural shift in product-level terms without committing to implementation details. The product behavior visible to users is unchanged; what changes is the read path.

---

**Section: §4.9 Architectural Constraints**

OLD (final bullet):
> - **No direct database access by clients:** All data access goes through the API layer to enforce tenant isolation and authorization.

NEW:
> - **No direct database access by clients:** All data access goes through the API layer (writes) or the local projection (reads) to enforce tenant isolation and authorization. The frontend's local projection is populated only from KurrentDB events emitted by the backend; clients never query PostgreSQL directly.

Rationale: Clarifies that "no direct DB access" is preserved under the local-first read path. The local projection is a derived view, not a database query.

---

### 4.2 architecture.md

**Proposed treatment:** Mark current doc as v1 (as-is). Generate a fresh **architecture-v2-nx-monorepo.md** via `bmad-create-architecture` as the first architectural sub-task of Story 4.0. v1 stays in the repo for reference until v2 is approved and locked.

The v2 will need to revise (at minimum) these sections of v1:
- **Project Context Analysis → Technical Constraints & Dependencies:** Add Node/TS projection layer, Nx workspace, Kurrent TS sync engine (forthcoming).
- **Core Architectural Decisions:** Add decisions: monorepo tool (Nx), shared TS lib pattern, local-first read path, Node projector runtime, Postgres client for Node.
- **Data Architecture:** Add `libs/events`, `libs/projection`, `libs/read-model` as TS packages. Add Node projector to the read-model pipeline.
- **Frontend Architecture:** Shift from "Astro SSR with React islands" to "Nx workspace containing Astro (BFF) and web app" — keep Astro for BFF and writes, web app becomes the local-first client. Update nanostores / Tailwind to be consumed via Nx lib boundaries.
- **Project Structure & Boundaries:** Full directory tree needs redo under `apps/` and `libs/`. Show how `apps/silo` (still .NET), `apps/projector` (Node/TS), `apps/web` (Astro+React), `apps/bff` (or keep Astro pages/api as BFF), and the `libs/*` packages all fit in one Nx workspace.
- **Implementation Patterns & Consistency Rules:** Add TS naming conventions, lib boundary rules, Nx tags/constraints, and the projection function contract (pure `Event → ReadModelDelta`).
- **Integration Points:** Add the Kurrent sync-engine integration point on the frontend.

Detailed v2 drafting is out of scope for this proposal — that's a `bmad-create-architecture` session.

---

### 4.3 Epic 1 — Core Platform Kanban MVP

**Epic Overview**

OLD:
> Ship a working Velucid instance where teams can create organizations, add products, manage tasks on a kanban board, and use the probabilistic forecast. No email verification, no saved views, no tags — just the core flow.

NEW:
> Ship a working Velucid instance where teams can create organizations, add products, manage tasks on a kanban board, and use the probabilistic forecast. No email verification, no saved views, no tags — just the core flow. **Implementation note: stories 1-2 onward are written assuming the Nx monorepo + Node projector layout from Epic 4. The grain-side code (Orleans, KurrentDB event store) remains .NET; the projector and frontend live in the Nx workspace and share `libs/projection`.**

Rationale: Flags the layout assumption so the story-level path references are taken with the new structure in mind.

**Per-story updates (after Epic 4 lands):**

- **Story 1.2 (Products):** Replace the "Projector handler" line with: "TS projection handler in `apps/projector/src/handlers/products.ts`. Projection logic in `libs/projection/products.ts`. Consumed by both projector and frontend." Replace the `ProductProjection` EF entity line with: "Read model defined in `libs/read-model` (TS types) and persisted in Postgres via the Node projector (using `pg` or `drizzle-orm` — design decision in 4.0)."
- **Story 1.3 (Tasks + Kanban):** Same pattern. Also note: "Frontend renders tasks from its local projection, not from `/api/tasks` GET. The GET endpoint remains for now (legacy/SSR), but kanban interactions read from local store."
- **Stories 1.4, 1.5:** Frontend-only — path references update to `apps/web/src/...`.

---

### 4.4 Epic 2 — Flow Features

**Epic Overview**

OLD:
> Build on the core platform to add the features that make Velucid useful: tag filtering, per-user saved views, and the probabilistic forecast with Monte Carlo simulation.

NEW:
> Build on the core platform to add the features that make Velucid useful: tag filtering, per-user saved views, and the probabilistic forecast with Monte Carlo simulation. **Implementation note: the Monte Carlo engine lives in `libs/projection/forecast` as a pure TS module. The forecast query path runs in the frontend (no BFF round-trip) for instant threshold-slider feedback; the backend projector can precompute the CDF in the background for cold clients. Tag-based filtering is a query against the local projection, not a server call.**

**Per-story updates:**

- **Story 2.3 (Forecast Engine):** Replace "ForecastService" (Silograined) with: "Forecast engine in `libs/projection/forecast` — pure TS module that takes event-derived time series and returns the CDF + cone data. Consumed by both the frontend (instant) and the backend projector (precomputed). No Silo service needed."
- **Stories 2.4, 2.5 (Forecast UI):** Add: "All chart rendering reads from `libs/projection/forecast` output, runs in the browser. No server round-trip on slider drag."
- **Story 2.6 (Tag-Based Filtering):** Add: "Tag filter is applied client-side against the local projection. Recalculation target stays under 2 seconds."

---

### 4.5 Epic 3 — Authorization (OpenFGA)

**No epic-level changes** — the grain-side work and OpenFGA model are unaffected.

**Per-story updates:**

- **Story 3.1 (OpenFGA Infrastructure):** Status remains `done`. **Add a follow-up: the projector-side `OpenFgaTupleSync` from 3.2 needs to be ported to TS as part of Story 4.2 of the new migration epic.** No story-file edit needed; tracked via the new epic.
- **Story 3.2 (Migrate OrgGrain Auth):** Status remains `done`. The `OrgProjector` (C#) and the `OpenFgaTupleSync` (C#) are the artifacts of this story. Both will be ported to TS in Story 4.2 — when ported, they replace the C# versions. The grain-side changes (Task 1-7 of 3-2) are not affected.
- **Stories 3-3, 3-4, 3-5, 3-6:** No content changes. Path references may need a `backend/src/Velucid.Silo/...` → `apps/silo/src/...` update after the repo moves. This is a mechanical edit and can happen as part of Story 4.0 (Nx monorepo bootstrap).

---

### 4.6 NEW Epic 4 — Nx Monorepo + Projector Migration

**Overview:** Convert the Velucid repository into an Nx monorepo, move the .NET projector service to a Node.js/TypeScript application, and create shared TypeScript libraries for event types, projection logic, and read-model types. Wire Nx Cloud for affected-graph CI and remote cache. Update infrastructure manifests and local-dev scripts to match.

**Target:** After this epic, the repository is a single Nx workspace. The web app and the projector both consume the same `libs/projection` package. The local-first read path is unblocked (frontend has the projection code; only the Kurrent sync engine drop is required to fully activate it). All previously-passing tests still pass; the OpenFGA tuple-sync behavior is preserved.

**Out of Scope (for this epic):**
- Wiring the actual Kurrent TypeScript sync engine to the frontend (depends on Kurrent's release).
- Replacing the Astro BFF or removing the `getElementById` legacy code from 1-0.
- Forecast-engine implementation (lives in `libs/projection/forecast` but is a future epic's work).
- Replacing ReadModel EF Core migrations — they stay, the Node projector just writes to the same Postgres schema.

---

**Story 4.0: Nx Monorepo Bootstrap**

What's needed:
- Add `package.json` at repo root with `nx` (latest stable, pinned to 22.7.5 — not 23 beta), `bun` 1.3.14 as the package manager AND JS runtime, and TypeScript dev deps.
- Add `nx.json` configuring the workspace: package manager, target defaults, Nx Cloud (`nxCloudId` placeholder — user supplies), affected defaults.
- Move `frontend/` → `apps/web/`. Update `apps/web/astro.config.mjs` if needed.
- Add `apps/projector/` skeleton (Node 20, TS, fastify or express — design decision in 4.2).
- Add `apps/silo/` as an Nx project that runs `dotnet build` and `dotnet test` via `nx:run-commands`. Use `@nx-dotnet/core` if compatible, else a custom executor.
- Add `libs/events/`, `libs/projection/`, `libs/read-model/`, `libs/kurrent-client/` as TypeScript library projects.
- Add a `project.json` per app/lib with `build`, `test`, `lint` targets.
- Configure root-level `tsconfig.base.json` with path mappings.
- Set up `lint` (eslint) and `format` (prettier) at the workspace level.
- Update `CLAUDE.md` and `docs/new-machine-setup.md` to mention `bun install` and `nx` commands.

Acceptance criteria:
- `bun install` succeeds at repo root.
- `nx run-many -t build` builds all apps/libs.
- `nx run-many -t test` runs all unit tests.
- `nx graph` shows the workspace with apps and libs and their dependencies.
- Astro web app still serves on the same dev port via `nx serve web` (or `bunx nx serve web`).
- Silo still builds via `nx build silo` (which shells out to `dotnet build`).
- No regression in currently-passing tests (1-0, 1-1, 3-1, 3-2 still done; their tests still pass).

Blocks: 4.1, 4.2, 4.3, 4.4, 4.5.

---

**Story 4.1: Shared TypeScript Libraries**

What's needed:
- `libs/events/` — TypeScript discriminated union of all event types (`OrgCreated`, `OrgRenamed`, `MemberAdded`, `MemberRemoved`, `OrgDeleted`, `UserCreated`, `TaskCreated`, etc.). Hand-written from the C# event records. Versioned; breaking changes to the union require a major version bump.
- `libs/projection/` — Pure functions: `(state, event) => state` for each aggregate. No I/O. No framework deps. Easily testable. This is the **shared code** that runs in both the projector and the frontend.
- `libs/read-model/` — TypeScript types matching the Postgres projection tables (mirrored from `Velucid.ReadModel/Entities/`). Used by both projector (to write) and frontend (to type-check its local store).
- `libs/kurrent-client/` — Thin adapter over the official KurrentDB Node client. Exposes a `subscribeFromCheckpoint(checkpoint, handler)` API. Designed to be swappable when the Kurrent sync engine lands (the engine will provide a similar API; we add a second implementation behind the same interface).

Acceptance criteria:
- Each lib has its own `package.json` and `project.json`.
- `libs/projection` is consumed by both `apps/web` and `apps/projector` (verified by import graph in `nx graph`).
- `libs/projection` is pure: `bun test projection` passes with no DB or network mocks.
- Nx tag constraints enforce that `libs/*` cannot import from `apps/*` (one-way dependency).

---

**Story 4.2: Port Projector to Node/TypeScript**

What's needed:
- Port `OrgProjector.cs` to `apps/projector/src/handlers/orgs.ts`. Logic: same event types → same Postgres writes → same OpenFGA tuple writes.
- Port `UserProjector.cs` to `apps/projector/src/handlers/users.ts`.
- Port `OpenFgaTupleSync.cs` to `apps/projector/src/services/openfga-tuple-sync.ts` using `@openfga/sdk` (Node SDK).
- Port KurrentDB client usage to the official Node client (`@kurrent/kurrentdb-client` or current package name).
- Port Postgres writes using `pg` directly or `drizzle-orm` (design decision — recommend `drizzle-orm` for type-safe schema alignment with `libs/read-model`).
- Port checkpoint logic to `ProjectionCheckpoint` table — same `last_sequence` semantics.
- `apps/projector/src/main.ts` boots: subscribes to `$ce` from last checkpoint, dispatches to handlers, writes Postgres + OpenFGA, updates checkpoint.
- Update `apps/projector/Dockerfile` to use `node:20-alpine` with multi-stage build.
- Delete `backend/src/Velucid.ProjectorService/` after port is verified.

Acceptance criteria:
- Local dev: with KurrentDB + Postgres + OpenFGA running, the Node projector subscribes to `$ce`, processes historical events, and writes the same projections the .NET projector did.
- All projection tests (currently passing on .NET) pass on the Node side.
- OpenFGA tuple sync behavior preserved: `OrgCreated` writes owner tuple; `MemberAdded` writes member tuple; `MemberRemoved` deletes tuple.
- Checkpoint resumes from last sequence on restart — no events skipped, no events double-processed.
- Container image builds and runs; healthcheck endpoint responds.

---

**Story 4.3: Wire Local-First Read Path on Frontend**

What's needed:
- `apps/web/src/state/` — local store (extending the existing nanostores) that holds the read model in memory. Hydrated from `/api/state` GET on first load (BFF proxies to Postgres for now).
- When the Kurrent sync engine lands (separate story, blocked on Kurrent release): replace the `/api/state` GET with a Kurrent subscription that feeds the same local store. Until then, the local store is built from a one-shot fetch and updates are picked up via the existing polling/refresh.
- `libs/projection` is consumed in the browser bundle. Webpack/Vite tree-shaking ensures only the projection functions actually used are shipped.

Acceptance criteria:
- Backlog view renders from local store, not from per-render fetch.
- Kanban drag-and-drop calls Silo via BFF, then updates local store optimistically (existing pattern).
- After the Kurrent sync engine lands (future story), the same `libs/projection` code runs in-browser against events received via the engine — no code change to projection logic.

Note: Full local-first activation is gated on Kurrent's sync engine release. This story delivers the read-path refactor and the local store; the live-event-feed wiring is a follow-up.

---

**Story 4.4: Nx Cloud + CI Integration**

What's needed:
- Connect Nx Cloud to the workspace (`nx connect` or manually add `nxCloudId` to `nx.json`).
- Configure remote cache (Nx Cloud stores build/test outputs keyed by `nx.json` + inputs hash).
- Update `.github/workflows/ci.yml` (or equivalent) to use `nx affected -t build test lint` with `--parallel` and `--cache`.
- Set up Nx Cloud PR integration (optional but recommended) for the affected graph in PR comments.
- Document the CI flow in `docs/new-machine-setup.md`.

Acceptance criteria:
- First CI run on a clean branch uploads cache.
- Subsequent runs on branches that only touch `apps/web` skip the Silo build and projector build (cache hit).
- Nx Cloud dashboard shows the workspace and recent runs.
- All existing GitHub Actions checks still pass.

---

**Story 4.5: Local Dev + Deploy Updates**

What's needed:
- Update `infrastructure/docker-compose.yml` to start the Node projector image (replacing the .NET one).
- Update `infrastructure/k8s/projector-service/` manifests for the new Node image and healthcheck endpoint.
- Update `infrastructure/scripts/start.sh` and `infrastructure/scripts/k3s-start.sh` to reference the new image tag.
- Update container registry tag in `architecture.md` integration notes: `ghcr.io/lekhasy/vut/{silo,web,projector}:<sha>`.
- Update `new-machine-setup.md` with `bun install` step and Nx prerequisites (Bun 1.3+).
- Update `makefile` to add `make nx-build`, `make nx-test`, `make nx-affected`.

Acceptance criteria:
- Fresh-clone dev setup runs `bun install` then `nx run-many -t serve` and the full stack comes up (web, bff, silo, projector, kurrentdb, postgres, openfga).
- K3s deploys the new Node projector; existing ArgoCD sync picks up the change.
- `make nx-test` and `make nx-build` work from the repo root.

---

**Retrospective 4: Monorepo Migration Review**

- Did Nx + .NET integration feel clean, or should we accept the friction?
- Did the local-first read path deliver the expected performance wins, or did the `/api/state` fallback mask real issues?
- Where is code-sharing actually saving duplication, and where is it adding cross-language tax?

---

### 4.7 sprint-status.yaml

**Proposed changes:**

```yaml
development_status:
  # Epic 1 — Core Platform Kanban MVP (paused pending Epic 4)
  epic-1: paused
  1-0-react-foundation: done
  1-1-organizations: done
  1-2-products: backlog
  1-3-tasks-kanban: backlog
  1-4-basic-navigation-dashboard: backlog
  1-5-forecast-tab-gathering-data-state: backlog
  epic-1-retrospective: optional

  # Epic 2 — Flow Features (paused pending Epic 4)
  epic-2: paused
  2-1-tags: backlog
  2-2-saved-views: backlog
  2-3-forecast-engine-monte-carlo: backlog
  2-4-forecast-ui-stat-cards-s-curve: backlog
  2-5-forecast-ui-progress-forecast-dual-cone: backlog
  2-6-forecast-tag-based-filtering: backlog
  epic-2-retrospective: optional

  # Epic 3 — Authorization (paused pending Epic 4 — grain-side unaffected, projector-side ported in 4.2)
  epic-3: paused
  3-1-openfga-infrastructure: done
  3-2-migrate-org-grain-auth: done
  3-3-migrate-user-grain-auth: backlog
  3-4-migrate-controller-auth: backlog
  3-5-remove-inline-auth-org-state: backlog
  3-6-authorization-test-suite: backlog
  epic-3-retrospective: optional

  # Epic 4 — Nx Monorepo + Projector Migration (NEW — runs first)
  epic-4: backlog
  4-0-nx-monorepo-bootstrap: backlog
  4-1-shared-typescript-libraries: backlog
  4-2-port-projector-to-node-typescript: backlog
  4-3-wire-local-first-read-path: backlog
  4-4-nx-cloud-ci-integration: backlog
  4-5-local-dev-deploy-updates: backlog
  epic-4-retrospective: optional
```

Note: Introduces a new `paused` status for epics awaiting prerequisites. (Sprint status format will need a one-line addition to its comment header explaining the new status.)

---

## 5. Implementation Handoff

### Scope classification

**Major.** Fundamental replan required.

### Handoff recipients and responsibilities

| Role | Responsibility |
|---|---|
| **Product Manager (PM)** | Review and approve the new Epic 4 scope. Confirm that the MVP remains achievable and the new "Local-First Frontend" goal aligns with product direction. Update PRD (Section 4.1). |
| **Solution Architect** | Lead Story 4.0 (Nx bootstrap) and Story 4.1 (shared libs). Author architecture-v2-nx-monorepo.md. Decide: Postgres client (drizzle vs pg vs kysely), HTTP server for the projector (fastify vs express), Nx + .NET integration approach (community plugin vs custom executor). |
| **Senior Software Engineer** | Implement Stories 4.2 (port projector), 4.3 (local-first read path), 4.4 (Nx Cloud + CI), 4.5 (deploy updates). Execute PRD edits and sprint-status updates. |
| **DevOps / Platform** | Nx Cloud account setup, GitHub Actions workflow rewrite, K3s manifest updates, container image pipeline update, ArgoCD sync validation. (Could be the same engineer for solo work.) |

### Handoff deliverables

This proposal, plus on approval:
- Updated PRD (Section 4.1, Section 4.9 edits applied).
- New Epic 4 file at `planning-artifacts/epic-4-nx-monorepo-projector-migration.md`.
- Updated `sprint-status.yaml` with Epic 4 entries and pause markers.
- architecture-v2 drafting queued for Story 4.0.

### Success criteria for the migration

- Nx workspace builds and tests pass for all apps and libs.
- Node projector behaves identically to the .NET projector (verified by replaying the test event stream).
- OpenFGA tuples are written/deleted on the same events as before.
- Nx Cloud shows cache hits on second CI run.
- Local dev: `bun install && nx run-many -t serve` boots the full stack with no .NET projector process required.
- The repo is structurally ready for the Kurrent TypeScript sync engine drop — when it lands, the frontend can subscribe and the local projection code starts receiving events without further refactor.

---

## 6. Open Questions for User

These decisions affect the proposal. Defaults are stated; please confirm or override.

1. **Postgres client for Node projector:** default `drizzle-orm` (type-safe, schema-first, good fit for `libs/read-model` types). Alternatives: `kysely`, raw `pg`. *Confirm or override.*
2. **HTTP server for the Node projector:** default none (it's a BackgroundService-style worker, no HTTP needed except for healthcheck). If healthcheck is needed: `fastify` (default) or `express`. *Confirm: is the healthcheck endpoint required?*
3. **Nx + .NET integration:** default `@nx-dotnet/core` community plugin (active, MIT-licensed). If it doesn't support the current dotnet 10 / Orleans stack cleanly: fall back to a custom `nx:run-commands` executor that wraps `dotnet build` / `dotnet test`. *Confirm: are you open to using the community plugin?*
4. **Astro BFF scope:** default — keep Astro for auth + writes; reads move to local projection. Eventually the BFF can shrink, but that's a future epic. *Confirm: keep Astro as-is for this migration, defer simplification?*
5. **`paused` sprint status:** default — introduce the new status. Alternative — leave epics as `in-progress` with a comment in the epic file explaining the pause. *Confirm or override.*
6. **Nx Cloud workspace:** default — set up an Nx Cloud workspace under the Lekhasy org. Need: the workspace token (or confirmation to generate one). *Confirm: do you have an Nx Cloud account, or do we set one up as part of Story 4.4?*
7. **Package manager:** default `bun` 1.3+ (package manager + JS runtime; replaces both Node and pnpm). *Confirmed by Syle 2026-06-03.*

---

**END OF PROPOSAL**

Next step: review and approve, or send back with edits. On approval, this proposal becomes the source of truth for the new epic, and the next workflow is `bmad-create-architecture` (to author architecture-v2-nx-monorepo.md) followed by `bmad-create-story` for Story 4.0.
