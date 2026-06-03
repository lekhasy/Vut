---
epic: epic-4
story_id: 4-0-nx-monorepo-bootstrap
status: ready-for-dev
title: Nx Monorepo Bootstrap (Bun + TypeScript path)
---

# Story 4.0: Nx Monorepo Bootstrap

## User Story

As a Velucid maintainer,
I want the repository restructured into an Nx monorepo with Bun as the package manager and JS runtime, with the existing Astro frontend moved to `apps/web` and skeleton placeholders for `apps/projector` and `apps/silo`,
so that stories 4.1 (shared TS libraries) and 4.2 (port projector to TS) can land without the repo churning again, and so that the Kurrent TypeScript sync engine can share `@velucid/projection` code with the backend once it drops.

## Acceptance Criteria

1. **`pnpm` is replaced by `bun` everywhere** — root `package.json` declares `"packageManager": "bun@1.3.14"`; `bun install` succeeds at repo root; no `pnpm-lock.yaml` or `package-lock.json` is checked in.
2. **Nx workspace builds** — `bunx nx run-many -t build` builds all apps and libs; `bunx nx graph` renders the dependency graph including apps and libs.
3. **Nx workspace tests** — `bunx nx run-many -t test` runs all unit tests (lib pure-function tests come in 4.1; this story wires the targets so they exist and pass with at least a smoke test in each project).
4. **Frontend still serves on the same dev port** — `bunx nx serve web` starts Astro on `http://localhost:4321` (existing dev port); Playwright config and existing E2E suite continue to pass.
5. **Silo still builds** — `bunx nx build silo` shells out to `dotnet build` and produces the same artifact the legacy path produced.
6. **Skeleton projects exist** — `apps/projector` (Bun + Fastify + TS), `apps/silo` (Nx wrapper over `dotnet`), `libs/events`, `libs/projection`, `libs/read-model`, `libs/kurrent-client` (TypeScript libraries) all have `project.json` + `package.json` with working `build`, `test`, `lint` targets. Library bodies are intentionally empty for now (populated in 4.1).
7. **No regression in currently-passing tests** — 1-0, 1-1, 3-1, 3-2 tests still pass (under their original test commands and under the new Nx wrappers). `astro check` and `playwright test` clean.
8. **CI updates land alongside the bootstrap** — `.github/workflows/ci.yaml` uses `oven-sh/setup-bun@v2` and invokes `bunx nx affected -t build test lint --parallel` instead of four independent image builds. Image tag updates for the legacy images still happen so the existing K3s deployment doesn't break during the migration (4.2 swaps in the Node projector image; 4.5 cleans up the legacy one).
9. **Docs updated** — `docs/new-machine-setup.md` documents `bun install` at repo root, the `oven/bun` Docker base for Astro, and the `nx` commands developers will use day-to-day. `CLAUDE.md` is populated with a concise monorepo + Nx command reference (currently a 1-line file).
10. **Nx Cloud wiring is a placeholder** — `nx.json` has an `nxCloudId` placeholder field with a `TODO` comment and the user supplies the actual ID via `NX_CLOUD_ACCESS_TOKEN` env var; the workspace connects to Nx Cloud on first CI run (full verification is in 4.4).

## Tasks / Subtasks

- [ ] **Task 1: Init Nx workspace at repo root (Bun)** (AC: #1, #2)
  - [ ] 1.1 Install Bun 1.3.14 locally (`curl -fsSL https://bun.sh/install | bash`) and verify with `bun --version`
  - [ ] 1.2 Run `bunx create-nx-workspace@22.7.5 --preset=apps --packageManager=bun --nxCloud=skip` at a temp dir to capture the canonical layout, then mirror it into the existing repo root. **Do NOT** blow away `frontend/`, `backend/`, or `infrastructure/` — preserve them; the new files (`package.json`, `nx.json`, `tsconfig.base.json`, `apps/`, `libs/`) are added alongside.
  - [ ] 1.3 Hand-author `package.json` at repo root (the workspace root) with:
    - `"packageManager": "bun@1.3.14"`
    - `"engines": { "bun": ">=1.3.0", "node": ">=20.19.0" }`
    - `"workspaces": ["apps/*", "libs/*"]`
    - devDependencies: `nx@22.7.5`, `typescript@5.8.x` (matching `frontend/`), `@nx/dotnet@22.7.5`, `eslint`, `prettier`
  - [ ] 1.4 Hand-author `nx.json` with `packageManager: bun`, `defaultBase: main`, target defaults (`build` cacheable, `test` cacheable, `lint` cacheable), `nxCloudId: "TODO_NX_CLOUD_ID"` placeholder, and `affected.defaultBase: main`
  - [ ] 1.5 Hand-author root `tsconfig.base.json` with strict mode, `composite: true`, `declaration: true`, `paths` ready for libs to be filled in (4.1)
  - [ ] 1.6 Run `bun install` at root; commit `bun.lock` (text lockfile, NOT `bun.lockb`); verify `bun.lock` is the only lockfile
- [ ] **Task 2: Move `frontend/` → `apps/web/`** (AC: #4, #7)
  - [ ] 2.1 `git mv frontend apps/web` (preserves history)
  - [ ] 2.2 Delete `apps/web/package.json`'s `package-lock.json` reference and any node_modules — `bun install` will produce `apps/web/node_modules` linked from the workspace root via Bun's hoisting
  - [ ] 2.3 Verify `apps/web/astro.config.mjs` still works; check `server.port: 4321` is preserved
  - [ ] 2.4 Update `apps/web/playwright.config.ts` `webServer.command` from `npm run dev` to `bun run dev` (Bun runs Astro scripts natively)
  - [ ] 2.5 Add `apps/web/project.json` with Nx targets delegating to existing scripts: `build` → `astro check && astro build`, `serve` → `astro dev`, `test` → `playwright test`
  - [ ] 2.6 **READ `apps/web/Dockerfile` first** — current Dockerfile uses `node:22-alpine` and `npm ci`. Replace with multi-stage `oven/bun:1.3.14-alpine` (build) → `oven/bun:1.3.14-alpine` (runtime); use `bun install --frozen-lockfile` in build stage; runtime CMD becomes `bun ./dist/server/entry.mjs` (per user's "Bun everywhere" choice — see Dev Notes §Bun runtime)
  - [ ] 2.7 Run `bunx nx serve web` and confirm `http://localhost:4321` serves the existing app
- [ ] **Task 3: Scaffold `apps/projector/` (Bun + Fastify + TS)** (AC: #6)
  - [ ] 3.1 Generate via `bunx nx g @nx/node:app apps/projector --framework=fastify --bundler=esbuild --packageManager=bun` then prune the boilerplate — empty `main.ts` + a `/health` route is enough for now
  - [ ] 3.2 `apps/projector/package.json` declares: `@kurrent/kurrentdb-client@^1.3.0`, `@openfga/sdk@^0.9.6`, `drizzle-orm@^0.36.x`, `pg@^8.13.x`, `fastify@^5.x`, `pino@^9.x`. Pin Bun to `1.3.14` via `"packageManager"`.
  - [ ] 3.3 `apps/projector/src/main.ts` is intentionally a `// Skeleton — populated in Story 4.2` placeholder; the file must exist and start Fastify on a configurable port, expose `GET /health` returning `{status: "ok", service: "projector", version: "0.0.0"}`
  - [ ] 3.4 `apps/projector/Dockerfile` mirrors `apps/web/Dockerfile` (oven/bun:1.3.14-alpine, multi-stage)
  - [ ] 3.5 Add `apps/projector/project.json` with `build` (esbuild), `test` (`bun test`), `serve` (Bun runtime)
- [ ] **Task 4: Scaffold `apps/silo/` (Nx + dotnet 10 wrapper)** (AC: #5, #6)
  - [ ] 4.1 Generate via `bunx nx add @nx/dotnet` (per the new official plugin, NOT the archived `@nx-dotnet/core` — see Dev Notes §@nx/dotnet)
  - [ ] 4.2 `bunx nx g @nx/dotnet:app apps/silo --template=api` and point it at `backend/src/Velucid.Silo/Velucid.Silo.csproj`. Confirm `bunx nx build silo` invokes `dotnet build` and produces the same artifact the legacy `backend/` path produced.
  - [ ] 4.3 Add `apps/silo/project.json` if the generator didn't include one; otherwise verify it has `build`, `test`, `serve` targets
  - [ ] 4.4 Do NOT delete `backend/src/` in this story — the legacy `Velucid.slnx` and `Program.cs` paths must keep working until 4.2 finishes the projector port and 4.5 cleans up
- [ ] **Task 5: Scaffold shared TS libraries** (AC: #6, #2)
  - [ ] 5.1 For each of `libs/events`, `libs/projection`, `libs/read-model`, `libs/kurrent-client`: run `bunx nx g @nx/node:lib libs/{name} --buildable --publishable=false --importPath=@velucid/{name} --packageManager=bun`
  - [ ] 5.2 Each lib gets a `package.json` (with `name: "@velucid/{name}"` and `packageManager: bun@1.3.14`) and a `project.json`; body is intentionally empty (a single `index.ts` that re-exports nothing yet)
  - [ ] 5.3 Add `apps/web` and `apps/projector` as consumers via `"@velucid/{name}": "workspace:*"` in their `package.json`
  - [ ] 5.4 Verify `bunx nx graph` shows edges `apps/web -> @velucid/*` and `apps/projector -> @velucid/*` with no reverse edges (libs do NOT depend on apps)
- [ ] **Task 6: Lint, format, typecheck workspace-wide** (AC: #2, #3)
  - [ ] 6.1 Root `.eslintrc.cjs` extends `@nx/eslint-plugin-nx` and `eslint:recommended`; `apps/web` keeps its existing Astro/React ESLint config (don't fight it)
  - [ ] 6.2 Root `.prettierrc` with `printWidth: 100`, `singleQuote: true`, `trailingComma: "all"`
  - [ ] 6.3 `bunx nx run-many -t lint` passes across all projects
  - [ ] 6.4 `bunx nx run-many -t typecheck` passes (add a `typecheck` target to `nx.json` defaults delegating to `tsc --noEmit`)
- [ ] **Task 7: Update CI workflow** (AC: #8)
  - [ ] 7.1 **READ `.github/workflows/ci.yaml` first** — current pipeline builds 4 separate Docker images (silo, projector-service, migrations, frontend). Modify, don't replace.
  - [ ] 7.2 Replace `actions/checkout` → install with `oven-sh/setup-bun@v2` (caches Bun binary by default); run `bun install --frozen-lockfile`
  - [ ] 7.3 Insert a new `nx-verify` job before the image builds that runs `bunx nx format:check`, `bunx nx run-many -t lint typecheck test --parallel`. Fail-fast if this fails; otherwise proceed to image builds.
  - [ ] 7.4 Keep all four existing image-build jobs but update their contexts: `apps/silo` for silo, `apps/projector` for projector, `apps/web` for frontend. Migrations image stays as-is for now (it has its own Dockerfile outside the monorepo).
  - [ ] 7.5 Add a new `update-manifests` step that sets `image: ghcr.io/lekhasy/vut/{silo,projector,web,frontend}:${SHA}` — both `projector` and `web` images exist; ArgoCD picks up whichever manifest the deployment uses
  - [ ] 7.6 The `update-manifests` job also rewrites `infrastructure/k8s/projector-service/deployment.yaml` to point at the new `ghcr.io/lekhasy/vut/projector:${SHA}` tag (NOT the old `projector-service` tag). The `.NET projector-service` deployment keeps its old tag for now; 4.5 will delete it.
- [ ] **Task 8: Update docs** (AC: #9)
  - [ ] 8.1 **READ `docs/new-machine-setup.md` first** — add a new section at the top: "Repository setup: install Bun 1.3.14 (`curl -fsSL https://bun.sh/install | bash`); clone repo; `cd Vut && bun install`; `bunx nx serve web` to run the frontend locally"
  - [ ] 8.2 Document the `bunx nx` command surface: `nx serve web`, `nx build silo`, `nx test projector`, `nx graph`, `nx affected -t build test lint --parallel`
  - [ ] 8.3 Add a `bun install --frozen-lockfile` step before any `make` target that builds the project
  - [ ] 8.4 Populate `CLAUDE.md` (currently 1 line) with: a 3-line repo summary, the bun + Nx command reference, and the apps/libs layout
  - [ ] 8.5 Add a "Bun everywhere" note: Astro runs under Bun in production; KurrentDB's NAPI native bridge has a `linux-x64-musl` prebuilt that loads under `oven/bun:*-alpine`; if a future dep lacks a musl prebuilt, fall back to `oven/bun:1.3.14` (Debian) or to Node 20 LTS for that one container
- [ ] **Task 9: Nx Cloud placeholder + verification** (AC: #10)
  - [ ] 9.1 `nx.json` contains `"nxCloudId": "TODO_NX_CLOUD_ID"` with a `// TODO: replace with real Nx Cloud workspace ID; see Story 4.4` comment
  - [ ] 9.2 `bunx nx connect` is NOT run in this story; documented in `CLAUDE.md` as a Story 4.4 task
  - [ ] 9.3 Verify `bunx nx build web` writes to `.nx/cache` (local cache works without Nx Cloud)
  - [ ] 9.4 Run `bunx nx run-many -t build` twice in a row and confirm the second run prints "Nx read the output from the cache" for at least the `web` target (validates local cache path)
- [ ] **Task 10: Final smoke test** (AC: #7)
  - [ ] 10.1 From a clean clone: `bun install && bunx nx run-many -t build test lint typecheck` passes
  - [ ] 10.2 `bunx nx serve web` boots Astro on `:4321`; manual click-through of `/api/orgs` (BFF) returns the same data the legacy path returned
  - [ ] 10.3 `bunx nx build silo` produces a `dist/` with the same entry DLL the legacy `dotnet publish` produced
  - [ ] 10.4 `bunx nx test projector` runs `bun test` and passes the smoke test (the placeholder health-check route)
  - [ ] 10.5 All previously-passing tests (1-0, 1-1, 3-1, 3-2) still pass when invoked via `bunx nx run-many -t test` or via their original test commands

## Dev Notes

### Bun everywhere (user-confirmed decision, 2026-06-03)

Per the user's explicit choice ("Bun everywhere"), `package.json` at the workspace root and every app/lib declares `"packageManager": "bun@1.3.14"`. Astro's production container runs the SSR output under Bun (`bun ./dist/server/entry.mjs`). Trade-off: Astro's docs flag Bun as having "rough edges" — expect to debug one or two subtle issues (most commonly: outgoing `fetch` body buffering differences vs Node). This is a known, accepted risk; if it materializes, the fallback is a hybrid (Node 20 in `apps/web` container, Bun elsewhere) but **do NOT silently fall back** — flag it and let the user decide.

### Nx + Bun specifics (June 2026)

- **Pin Nx to `22.7.5`**, NOT `23.x` (23.0.0-beta.22 is current; v23 deprecates `nxViteTsPaths`, `nxCopyAssetsPlugin`, and old Jest setup options — wait for stable).
- **Lockfile is `bun.lock` (text)**, NOT `bun.lockb` (binary). Bun 1.2+ defaults to text. Older docs reference `bun.lockb`; ignore them.
- **`@nx/dotNet/core` is archived (2026-04-27)**. Use the official replacement `@nx/dotnet@22.7.5`. Do NOT add `@nx-dotnet/core` even if older blog posts recommend it.
- **Bun's `node:http` outgoing body is buffered, not streamed.** Affects long-poll outbound calls from the projector (4.2) and from SSR handlers in `apps/web`. Note for future stories; not blocking 4.0.
- **No first-party Nx Cloud + Bun docs.** The remote cache is content-hash-driven and should be agnostic to package manager, but verify empirically on first CI run (deferred to 4.4).

### Existing files the dev agent MUST read first

These files exist in the legacy layout. Reading them is non-negotiable — modifying or moving them blindly will break things:

- `frontend/package.json` — Astro 5.8, React 19, all current deps. Will move to `apps/web/`.
- `frontend/astro.config.mjs` — SSR with `node` adapter standalone mode; port 4321; reads env via `envField` from a known schema.
- `frontend/Dockerfile` — currently `node:22-alpine` + `npm ci`; replace with `oven/bun:1.3.14-alpine`.
- `frontend/playwright.config.ts` — `webServer.command` is `npm run dev`; change to `bun run dev`.
- `backend/src/Velucid.slnx` — solution file. Keep intact; the `@nx/dotnet` plugin points at the `.csproj` not the slnx.
- `backend/src/Velucid.ProjectorService/` — .NET projector (will be ported in 4.2, deleted in 4.5). `Handlers/OrgProjector.cs` and `Services/OpenFgaTupleSync.cs` are the porting references for 4.2.
- `infrastructure/k8s/projector-service/deployment.yaml` — currently references the old `projector-service` image. Will dual-track in this story (new `projector` image added; old `projector-service` deployment stays until 4.5).
- `.github/workflows/ci.yaml` — current 4-job image-build pipeline; this story adds `nx-verify` and updates contexts; the four build jobs stay.
- `docs/new-machine-setup.md` — production WSL+K3s setup doc. This story adds a "Local development with Bun" section at the top; do not edit the prod steps.
- `CLAUDE.md` — currently a 1-line file. Populating it is a Task 8 deliverable.
- `PRD.md` — was modified 2026-06-03 (the proposal run); re-read in case it now describes the Nx migration in product terms.

### Architecture patterns this story MUST preserve

(From `_bmad-output/planning-artifacts/architecture.md`. Do not change these in 4.0 — only the build/layout changes.)

- **CQRS:** commands emit events; queries read from projected state. Unchanged.
- **BFF auth pattern:** Astro is the sole auth entry point. `apps/web` keeps this role; no auth changes.
- **Catch-up subscription pattern:** projector subscribes to `$ce` from last checkpoint. Unchanged for 4.0; the .NET projector is still the projector. 4.2 ports it to `apps/projector`.
- **Event sourcing in grains:** unchanged. `apps/silo` builds the same .NET solution.
- **Optimistic UI:** unchanged. 4.3 wires the local store; 4.0 is layout-only.
- **Container images:** existing registry path `ghcr.io/lekhasy/vut/{silo,web,projector-service}:<sha>`. 4.0 adds a parallel `projector` image; 4.5 removes `projector-service`.

### Project Structure (post-4.0 target)

```
Vut/
├── apps/
│   ├── web/                 ← moved from frontend/ (Astro 5 + React 19)
│   │   ├── src/
│   │   ├── astro.config.mjs
│   │   ├── package.json     (name: @velucid/web, packageManager: bun@1.3.14)
│   │   ├── project.json     (Nx targets: build/serve/test/lint)
│   │   └── Dockerfile       (oven/bun:1.3.14-alpine, bun ./dist/server/entry.mjs)
│   ├── projector/           ← NEW (Bun + Fastify + TS, skeleton in 4.0; populated 4.2)
│   │   ├── src/main.ts      (placeholder + /health)
│   │   ├── package.json     (deps listed in Task 3.2)
│   │   ├── project.json
│   │   └── Dockerfile       (oven/bun:1.3.14-alpine)
│   └── silo/                ← NEW (Nx wrapper over Velucid.Silo.csproj)
│       ├── project.json     (Nx targets delegating to dotnet build/test/run)
│       └── (points at backend/src/Velucid.Silo)
├── libs/
│   ├── events/              ← NEW (@velucid/events, empty index.ts)
│   ├── projection/          ← NEW (@velucid/projection, empty index.ts — populated 4.1)
│   ├── read-model/          ← NEW (@velucid/read-model, empty index.ts)
│   └── kurrent-client/      ← NEW (@velucid/kurrent-client, empty index.ts)
├── backend/                 ← UNCHANGED in 4.0; cleaned up in 4.5
│   └── src/...
├── infrastructure/          ← UNCHANGED in 4.0
├── docs/                    ← new-machine-setup.md updated in Task 8
├── CLAUDE.md                ← populated in Task 8
├── package.json             ← workspace root (packageManager, workspaces, devDeps)
├── bun.lock                 ← committed (NOT bun.lockb)
├── nx.json                  ← packageManager, targets, nxCloudId TODO
├── tsconfig.base.json       ← strict, composite, paths ready for libs
├── eslint.config.cjs        ← @nx/eslint-plugin-nx + Astro extends
├── .prettierrc              ← root format config
└── _bmad-output/...         ← unchanged
```

### Tag constraints (pre-empting 4.1)

When Task 5 generates libs, add an ESLint rule in `eslint.config.cjs`:

```js
{
  files: ['libs/**/*.ts'],
  rules: {
    '@nx/enforce-module-boundaries': ['error', {
      depConstraints: [
        { sourceTag: 'type:lib', onlyDependOnLibsWithTags: ['type:lib'] },
        { sourceTag: 'type:app', onlyDependOnLibsWithTags: ['type:lib', 'type:app'] },
      ],
    }],
  },
}
```

This enforces the "libs do NOT import apps" rule from the epic; 4.1 will rely on it.

### Testing Standards

- **Lib tests (4.0):** Each lib has one trivial `index.test.ts` that asserts the `index.ts` re-exports nothing (or whatever minimal smoke check). Populated in 4.1.
- **Projector skeleton (4.0):** `apps/projector/src/health.test.ts` asserts `GET /health` returns `{status: "ok"}`. Uses `bun test` (built-in, fast, zero-config).
- **Frontend (4.0):** Existing Playwright suite must pass unchanged. `bunx nx test web` invokes `playwright test` against the Bun-served dev server.
- **Silo (4.0):** `bunx nx test silo` invokes `dotnet test`. Existing tests stay; no new tests added in 4.0.
- **No new tests for shared libraries** — libs are empty in 4.0; 4.1 adds the test suites.

### References

- Epic 4 plan: `_bmad-output/planning-artifacts/epic-4-nx-monorepo-projector-migration.md` [Story 4.0]
- Sprint change proposal: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-03.md`
- Architecture: `_bmad-output/planning-artifacts/architecture.md` §"Critical Conflict Points" and §"Enforcement Guidelines"
- Project context: `docs/project-context.md`
- Existing .NET projector (porting target for 4.2): `backend/src/Velucid.ProjectorService/Handlers/OrgProjector.cs`, `Services/OpenFgaTupleSync.cs`, `Program.cs`
- Legacy CI: `.github/workflows/ci.yaml`
- Legacy deployment: `infrastructure/k8s/projector-service/deployment.yaml`
- PRD: `PRD.md` (modified 2026-06-03 during proposal run)
- Nx 22.7.5 docs: https://nx.dev (CI recipes, `.nxignore`, `nx.json` reference)
- Bun 1.3.14 docs: https://bun.sh/docs (Docker base, lockfile, install)
- Bun + Astro caveat: https://docs.astro.build/en/recipes/bun/

## Dev Agent Record

### Agent Model Used

### Debug Log References

### Completion Notes List

### File List

**Files to CREATE:**
- `package.json` (workspace root)
- `bun.lock` (generated; commit it)
- `nx.json`
- `tsconfig.base.json`
- `eslint.config.cjs`
- `.prettierrc`
- `apps/projector/package.json`
- `apps/projector/project.json`
- `apps/projector/src/main.ts` (skeleton)
- `apps/projector/src/health.test.ts` (smoke)
- `apps/projector/Dockerfile`
- `apps/silo/project.json` (if `@nx/dotnet` generator doesn't create it)
- `libs/events/package.json`
- `libs/events/project.json`
- `libs/events/src/index.ts` (empty re-export)
- `libs/events/src/index.test.ts` (smoke)
- `libs/projection/package.json`
- `libs/projection/project.json`
- `libs/projection/src/index.ts`
- `libs/projection/src/index.test.ts`
- `libs/read-model/package.json`
- `libs/read-model/project.json`
- `libs/read-model/src/index.ts`
- `libs/read-model/src/index.test.ts`
- `libs/kurrent-client/package.json`
- `libs/kurrent-client/project.json`
- `libs/kurrent-client/src/index.ts`
- `libs/kurrent-client/src/index.test.ts`
- `apps/web/project.json`

**Files to MODIFY:**
- `frontend/` → `apps/web/` (git mv preserves history)
- `apps/web/package.json` — add `packageManager: bun@1.3.14`, drop `package-lock.json` reference
- `apps/web/playwright.config.ts` — `webServer.command` → `bun run dev`
- `apps/web/Dockerfile` — replace `node:22-alpine` + `npm ci` with `oven/bun:1.3.14-alpine` + `bun install --frozen-lockfile` + `bun ./dist/server/entry.mjs` runtime CMD
- `infrastructure/k8s/projector-service/deployment.yaml` — add new `image: ghcr.io/lekhasy/vut/projector:${SHA}` line as comment for 4.5; old `projector-service` tag stays active
- `.github/workflows/ci.yaml` — add `oven-sh/setup-bun@v2` step, add `nx-verify` job, update image-build contexts to point at `apps/silo`, `apps/projector`, `apps/web`
- `docs/new-machine-setup.md` — add "Local development with Bun" section at the top
- `CLAUDE.md` — populate with monorepo + Nx command reference
- `PRD.md` — re-read for any post-proposal product changes; do not edit unless 4.0 changes require a product-doc update (and if so, propose to the user first)

**Files to DELETE (in 4.5, not 4.0):**
- `backend/src/Velucid.ProjectorService/` — ported to `apps/projector` in 4.2
- `frontend/package-lock.json` — replaced by `bun.lock`
- `frontend/node_modules/` — never committed; `bun install` regenerates

**Files to READ (before modifying — non-negotiable):**
- `frontend/package.json`, `frontend/astro.config.mjs`, `frontend/Dockerfile`, `frontend/playwright.config.ts`, `frontend/tsconfig.json`
- `backend/src/Velucid.slnx`, `backend/src/Velucid.ProjectorService/Program.cs`
- `backend/src/Velucid.Silo/Events/EventTypeMapping.cs` (don't change in 4.0, but know what it does)
- `.github/workflows/ci.yaml` (read fully before modifying)
- `infrastructure/k8s/projector-service/deployment.yaml` (read fully)
- `docs/new-machine-setup.md` (read fully; append, do not rewrite)
- `CLAUDE.md` (currently 1 line)
- `_bmad-output/planning-artifacts/architecture.md` §"Implementation Patterns & Consistency Rules" and §"Project Structure & Boundaries"
- `_bmad-output/planning-artifacts/epic-4-nx-monorepo-projector-migration.md` (Story 4.0 and 4.5 — for cleanup context)
- `_bmad-output/planning-artifacts/sprint-change-proposal-2026-06-03.md` (for the user's "why Bun" rationale and the "Bun everywhere" confirmation)

## Change Log

| Date       | Changes |
|------------|---------|
| 2026-06-03 | Story created from epic-4 + sprint-change-proposal-2026-06-03. Package manager/runtime changed from pnpm to Bun 1.3.14 per user direction ("Bun everywhere"). Nx version pinned to 22.7.5 (not 23 beta). `@nx-dotnet/core` (archived 2026-04-27) replaced with `@nx/dotnet`. Frontend runs under Bun in production per explicit user choice. |

## Out of Scope (carried to follow-up stories)

- Wiring the actual Kurrent TypeScript sync engine to the frontend (4.3 + future story after Kurrent release).
- Replacing the Astro BFF or removing legacy `getElementById` code (already done in 1-0).
- Forecast-engine implementation (lives in `libs/projection/forecast`; future epic).
- Replacing the existing ReadModel EF Core migrations (Node projector in 4.2 writes to the same Postgres schema).
- Porting the .NET projector code (4.2). 4.0 leaves `backend/src/Velucid.ProjectorService/` in place.
- Cleaning up `backend/src/Velucid.ProjectorService/`, `frontend/package-lock.json`, the legacy `projector-service` deployment, and the `apps/silo` shim (all 4.5).
- Full Nx Cloud wiring (4.4 — Task 9.1 leaves a placeholder).
