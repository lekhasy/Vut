# Epic 4: Nx Monorepo + Projector Migration

## Overview

Convert the Velucid repository into an Nx monorepo, move the .NET projector service to a Node.js/TypeScript application, and create shared TypeScript libraries for event types, projection logic, and read-model types. Wire Nx Cloud for affected-graph CI and remote cache. Update infrastructure manifests and local-dev scripts to match.

This epic exists because the Kurrent TypeScript sync engine (forthcoming) requires the frontend to project events into a local read model. To share projection code between frontend and backend without drift, both must speak the same language (TypeScript) and live in the same monorepo.

**Target:** After this epic, the repository is a single Nx workspace. The web app and the Node projector both consume the same `libs/projection` package. The local-first read path is unblocked — only the Kurrent sync engine drop is required to fully activate it. All previously-passing tests still pass; the OpenFGA tuple-sync behavior is preserved.

**Out of Scope (for this epic):**
- Wiring the actual Kurrent TypeScript sync engine to the frontend (depends on Kurrent's release; becomes its own story after the engine drops).
- Replacing the Astro BFF or removing legacy `getElementById` code from 1-0.
- Forecast-engine implementation (lives in `libs/projection/forecast` but is a future epic's work).
- Replacing ReadModel EF Core migrations — they stay, the Node projector writes to the same Postgres schema.

**Sequencing:** This epic must complete before Epic 1 stories 1-2 onward, Epic 2, and Epic 3 stories 3-3 onward resume.

---

## Story 4.0: Nx Monorepo Bootstrap

**What's needed:**
- Add `package.json` at repo root with `nx` (latest stable, pinned to 22.7.5 — not 23 beta), `bun` 1.3.14 as the package manager AND JS runtime, and TypeScript dev deps.
- Add `nx.json` configuring the workspace: package manager, target defaults, Nx Cloud (`nxCloudId` placeholder — user supplies), affected defaults.
- Move `frontend/` → `apps/web/`. Update `apps/web/astro.config.mjs` if needed.
- Add `apps/projector/` skeleton (Node 20, TS, fastify or express — design decision in 4.2).
- Add `apps/silo/` as an Nx project that runs `dotnet build` and `dotnet test` via `nx:run-commands`. Use `@nx-dotnet/core` if compatible with dotnet 10 / Orleans, else a custom executor.
- Add `libs/events/`, `libs/projection/`, `libs/read-model/`, `libs/kurrent-client/` as TypeScript library projects (scaffolds — populated in 4.1).
- Add a `project.json` per app/lib with `build`, `test`, `lint` targets.
- Configure root-level `tsconfig.base.json` with path mappings.
- Set up `lint` (eslint) and `format` (prettier) at the workspace level.
- Update `CLAUDE.md` and `docs/new-machine-setup.md` to mention `bun install` and `nx` commands.

**Acceptance criteria:**
- `bun install` succeeds at repo root.
- `nx run-many -t build` builds all apps/libs.
- `nx run-many -t test` runs all unit tests.
- `nx graph` shows the workspace with apps and libs and their dependencies.
- Astro web app still serves on the same dev port via `nx serve web`.
- Silo still builds via `nx build silo` (which shells out to `dotnet build`).
- No regression in currently-passing tests (1-0, 1-1, 3-1, 3-2 still done; their tests still pass).

**Blocks:** 4.1, 4.2, 4.3, 4.4, 4.5.

---

## Story 4.1: Shared TypeScript Libraries

**What's needed:**
- `libs/events/` — TypeScript discriminated union of all event types (`OrgCreated`, `OrgRenamed`, `MemberAdded`, `MemberRemoved`, `OrgDeleted`, `UserCreated`, `TaskCreated`, etc.). Hand-written from the C# event records. Versioned; breaking changes to the union require a major version bump.
- `libs/projection/` — Pure functions: `(state, event) => state` for each aggregate. No I/O. No framework deps. Easily testable. This is the **shared code** that runs in both the projector and the frontend.
- `libs/read-model/` — TypeScript types matching the Postgres projection tables (mirrored from `Velucid.ReadModel/Entities/`). Used by both projector (to write) and frontend (to type-check its local store).
- `libs/kurrent-client/` — Thin adapter over the official KurrentDB Node client. Exposes a `subscribeFromCheckpoint(checkpoint, handler)` API. Designed to be swappable when the Kurrent sync engine lands (the engine will provide a similar API; we add a second implementation behind the same interface).

**Acceptance criteria:**
- Each lib has its own `package.json` and `project.json`.
- `libs/projection` is consumed by both `apps/web` and `apps/projector` (verified by import graph in `nx graph`).
- `libs/projection` is pure: `bun test projection` passes with no DB or network mocks.
- Nx tag constraints enforce that `libs/*` cannot import from `apps/*` (one-way dependency).

**Preceded by:** 4.0

---

## Story 4.2: Port Projector to Node/TypeScript

**What's needed:**
- Port `OrgProjector.cs` to `apps/projector/src/handlers/orgs.ts`. Logic: same event types → same Postgres writes → same OpenFGA tuple writes.
- Port `UserProjector.cs` to `apps/projector/src/handlers/users.ts`.
- Port `OpenFgaTupleSync.cs` to `apps/projector/src/services/openfga-tuple-sync.ts` using `@openfga/sdk` (Node SDK). (See `backend/src/Velucid.ProjectorService/Services/OpenFgaTupleSync.cs` for the C# reference.)
- Port KurrentDB client usage to the official Node client (`@kurrent/kurrentdb-client` or current package name).
- Port Postgres writes using `drizzle-orm` (default) or `pg` directly. Recommend `drizzle-orm` for type-safe schema alignment with `libs/read-model`.
- Port checkpoint logic to `ProjectionCheckpoint` table — same `last_sequence` semantics.
- `apps/projector/src/main.ts` boots: subscribes to `$ce` from last checkpoint, dispatches to handlers, writes Postgres + OpenFGA, updates checkpoint.
- Update `apps/projector/Dockerfile` to use `node:20-alpine` with multi-stage build.
- Delete `backend/src/Velucid.ProjectorService/` after port is verified.

**Acceptance criteria:**
- Local dev: with KurrentDB + Postgres + OpenFGA running, the Node projector subscribes to `$ce`, processes historical events, and writes the same projections the .NET projector did.
- All projection tests (currently passing on .NET) pass on the Node side.
- OpenFGA tuple sync behavior preserved: `OrgCreated` writes owner tuple; `MemberAdded` writes member tuple; `MemberRemoved` deletes tuple.
- Checkpoint resumes from last sequence on restart — no events skipped, no events double-processed.
- Container image builds and runs; healthcheck endpoint responds.

**Preceded by:** 4.1

---

## Story 4.3: Wire Local-First Read Path on Frontend

**What's needed:**
- `apps/web/src/state/` — local store (extending the existing nanostores) that holds the read model in memory. Hydrated from `/api/state` GET on first load (BFF proxies to Postgres for now).
- When the Kurrent sync engine lands (separate story, blocked on Kurrent release): replace the `/api/state` GET with a Kurrent subscription that feeds the same local store. Until then, the local store is built from a one-shot fetch and updates are picked up via the existing polling/refresh.
- `libs/projection` is consumed in the browser bundle. Webpack/Vite tree-shaking ensures only the projection functions actually used are shipped.

**Acceptance criteria:**
- Backlog view renders from local store, not from per-render fetch.
- Kanban drag-and-drop calls Silo via BFF, then updates local store optimistically (existing pattern).
- After the Kurrent sync engine lands (future story), the same `libs/projection` code runs in-browser against events received via the engine — no code change to projection logic.

**Note:** Full local-first activation is gated on Kurrent's sync engine release. This story delivers the read-path refactor and the local store; the live-event-feed wiring is a follow-up.

**Preceded by:** 4.1

---

## Story 4.4: Nx Cloud + CI Integration

**What's needed:**
- Connect Nx Cloud to the workspace (`nx connect` or manually add `nxCloudId` to `nx.json`).
- Configure remote cache (Nx Cloud stores build/test outputs keyed by `nx.json` + inputs hash).
- Update `.github/workflows/ci.yml` (or equivalent) to use `nx affected -t build test lint` with `--parallel` and `--cache`.
- Set up Nx Cloud PR integration (optional but recommended) for the affected graph in PR comments.
- Document the CI flow in `docs/new-machine-setup.md`.

**Acceptance criteria:**
- First CI run on a clean branch uploads cache.
- Subsequent runs on branches that only touch `apps/web` skip the Silo build and projector build (cache hit).
- Nx Cloud dashboard shows the workspace and recent runs.
- All existing GitHub Actions checks still pass.

**Preceded by:** 4.0

---

## Story 4.5: Local Dev + Deploy Updates

**What's needed:**
- Update `infrastructure/docker-compose.yml` to start the Node projector image (replacing the .NET one).
- Update `infrastructure/k8s/projector-service/` manifests for the new Node image and healthcheck endpoint.
- Update `infrastructure/scripts/start.sh` and `infrastructure/scripts/k3s-start.sh` to reference the new image tag.
- Update container registry tag in `architecture.md` integration notes: `ghcr.io/lekhasy/vut/{silo,web,projector}:<sha>`.
- Update `new-machine-setup.md` with `bun install` step and Nx prerequisites (Bun 1.3+).
- Update `makefile` to add `make nx-build`, `make nx-test`, `make nx-affected`.

**Acceptance criteria:**
- Fresh-clone dev setup runs `bun install` then `nx run-many -t serve` and the full stack comes up (web, bff, silo, projector, kurrentdb, postgres, openfga).
- K3s deploys the new Node projector; existing ArgoCD sync picks up the change.
- `make nx-test` and `make nx-build` work from the repo root.

**Preceded by:** 4.2

---

## Retrospective 4: Monorepo Migration Review

**Purpose:** Capture what worked, what didn't, and what to do differently in the next epic.

**Topics to cover:**
- Did Nx + .NET integration feel clean, or should we accept the friction and stay with `nx:run-commands`?
- Did the local-first read path deliver the expected performance wins, or did the `/api/state` fallback mask real issues?
- Where is code-sharing actually saving duplication, and where is it adding cross-language tax?
- How was the OpenFGA port — anything in the .NET SDK that the Node SDK lacks (e.g., model migration, batch writes)?
- Nx Cloud cache hit rate — are we running the right scope of affected commands?
