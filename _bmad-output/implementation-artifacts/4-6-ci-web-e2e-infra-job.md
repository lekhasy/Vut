---
epic: epic-4
story_id: 4-6-ci-web-e2e-infra-job
status: backlog
title: CI e2e Job for apps/web (Playwright + Auth0/Silo/KurrentDB/Postgres)
---

# Story 4.6: CI e2e Job for apps/web

## User Story

As a Velucid maintainer,
I want CI to run the Playwright e2e suite against a live Auth0 + silo + KurrentDB + Postgres stack,
so that the web app's auth flows (currently 7/9 e2e tests that need infra, see Dev Notes) are exercised in CI, regressions are caught before merge, and the test target in `apps/web` stops being the silent failure it is today (excluded from `nx-verify` since Story 4.0's third post-review fix).

## Context (why this exists)

Story 4.0 bootstrapped the Nx monorepo. The `nx-verify` CI job runs `bunx nx run-many -t lint typecheck test --parallel`. With the pre-push hook explicitly excluding `test` because `apps/web:test` (Playwright) needs Auth0 + silo + KurrentDB + Postgres, CI was running the same broken target with no infra — 7/9 tests fail because the auth fixture (`apps/web/tests/e2e/auth.ts`) calls `signInAs(page, user)` which talks to a real stack that a fresh runner doesn't have.

Story 4.0's third post-review fix added `--exclude web` to the CI `test` invocation so the unit tests for silo (46 dotnet tests), projector (`bun test`), and the 4 libs (`bun test`) run. The web e2e suite is **deferred to this story** — a separate CI job that brings up the infra first, then runs Playwright.

## Acceptance Criteria

1. **CI has a new `e2e-web` job** that runs `bunx nx run web:test` and passes (all 9 Playwright tests green) on a clean runner.
2. **The e2e job brings up its own infra** — Auth0, silo, KurrentDB, Postgres — using either `docker compose` (preferred; the `infrastructure/docker-compose.yml` already exists for local dev) or GH Actions `services:` blocks. The job MUST NOT depend on an external cluster.
3. **`apps/web/playwright.config.ts` reads infra endpoints from env vars** (`AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_CLIENT_SECRET`, `AUTH0_AUDIENCE`, `SILO_API_URL`, etc.) so the same config works for both `webServer.command` (local dev / current CI dev-server boot) and the e2e job (real endpoints pointed at the docker-compose stack). No hardcoded URLs in test code.
4. **The `e2e-web` job runs in parallel with `nx-verify`**, not after it (or as a `needs:` of `nx-verify` — design choice, see Dev Notes). Failures fail the build.
5. **`scripts/hooks/pre-push` stays unchanged** — it still excludes `test` entirely because the local dev machine rarely has Auth0 + silo + KurrentDB + Postgres. Developers who want to run e2e locally use `docker compose -f infrastructure/docker-compose.yml up -d` first, then `bunx nx run web:test`.
6. **The job is reproducible on a brand-new self-hosted runner** (a fresh checkout, no pre-cached state) — no manual secret configuration outside what the workflow already declares.
7. **Story 4.0's `--exclude web` flag is removed** from `nx-verify`'s `test` invocation once this story lands (replaced by the `e2e-web` job that catches the same coverage).

## Tasks / Subtasks

- [ ] **Task 1: Audit `apps/web/tests/e2e/auth.ts` and the existing infra setup** (AC: #2, #3)
  - [ ] 1.1 Read `apps/web/tests/e2e/auth.ts`, `apps/web/tests/e2e/*.spec.ts`, and `apps/web/tests/global-setup.ts` to inventory exactly which external services each test touches (Auth0 tenant, silo gRPC/HTTP, KurrentDB gRPC, Postgres).
  - [ ] 1.2 Read `infrastructure/docker-compose.yml` (and any overrides) to see what's already in repo; verify it covers all four services above without manual secrets.
  - [ ] 1.3 Document the gap in Dev Notes (this story) — what's missing for a self-contained e2e CI job.
- [ ] **Task 2: Add an `e2e-web` job to `.github/workflows/ci.yaml`** (AC: #1, #2, #4, #6)
  - [ ] 2.1 Add an `e2e-web` job at the bottom of the workflow, `runs-on: ubuntu-latest`, with `services:` or a `docker compose up -d` step that brings up the four services.
  - [ ] 2.2 Set the `AUTH0_*`, `SILO_API_URL`, `KURRENTDB_URL`, `POSTGRES_*` env vars on the job to point at the docker-compose service hostnames (e.g. `http://silo:5000`, `postgres://postgres:5432`).
  - [ ] 2.3 For Auth0: the dev tenant used by the e2e fixture must be reachable from CI. Either (a) use a real test tenant (declare `AUTH0_DOMAIN` + `AUTH0_CLIENT_ID` + client secret as repo/org secrets and the runner uses them), or (b) stand up a local Auth0 dev container (the `bitnami/auth0` or `nodeauth/oauth2-mock-server` images are common for e2e). Document the chosen path in Dev Notes.
  - [ ] 2.4 Job runs `bun install --frozen-lockfile` → `bunx playwright install --with-deps chromium` → `bunx nx run web:test`. Uses `BASE_URL=http://localhost:4321` (Playwright boots its own dev server via the `webServer.command` block; or skip that and pre-start `bun run dev` for clarity — design decision, see Dev Notes).
  - [ ] 2.5 Add an `e2e-web` artifact upload (Playwright's `playwright-report/` and `test-results/`) on failure so a red build has a downloadable report.
  - [ ] 2.6 Wire the job to `needs: nx-verify` so a lint/typecheck/unit-test failure short-circuits the e2e run. (E2e is the slowest gate; no point running it if the cheap gates fail.)
- [ ] **Task 3: Refactor `apps/web/playwright.config.ts` for env-driven infra** (AC: #3)
  - [ ] 3.1 Replace the hardcoded stub values in `webServer.command` with reads from `process.env`, falling back to local-dev defaults only when the env var is absent. Current stub list: `AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_CLIENT_SECRET`, `AUTH0_AUDIENCE`, `SESSION_SECRET`, `SILO_API_URL`, `APP_URL`.
  - [ ] 3.2 Add `baseURL`, `extraHTTPHeaders`, and any test fixtures' endpoint config to read from env. Test code (`auth.ts` etc.) stays env-driven; no hardcoded `https://test.us.auth0.com` in test files.
  - [ ] 3.3 Verify the e2e suite still runs end-to-end locally with `docker compose -f infrastructure/docker-compose.yml up -d && bunx nx run web:test` (no Playwright-config edits needed beyond env reads).
- [ ] **Task 4: Remove the `--exclude web` from `nx-verify` and confirm coverage** (AC: #7)
  - [ ] 4.1 Revert the Story 4.0 third post-review fix in `.github/workflows/ci.yaml` — `bunx nx run-many -t lint typecheck test --parallel` (no `--exclude web`). With the `e2e-web` job now running the full e2e suite, the cheap `test` target in `nx-verify` (which runs `apps/web:test` if invoked without `--exclude web`) needs reconsideration: decide whether to keep `--exclude web` (unit tests only in `nx-verify`; e2e in `e2e-web`) or run web:test in `nx-verify` and accept the duplicate run. **Recommended: keep `--exclude web` in `nx-verify`** so e2e runs once (in `e2e-web`) and the unit tests for the other 6 projects still run in `nx-verify`. Document the rationale in Dev Notes.
  - [ ] 4.2 Push a green build through both `nx-verify` and `e2e-web` end-to-end on a clean runner (or a self-hosted runner with no pre-cached state) and confirm all jobs pass.
- [ ] **Task 5: Update `docs/new-machine-setup.md`** (AC: #5)
  - [ ] 5.1 Add a new "Running e2e locally" section that documents the `docker compose up -d` + `bunx nx run web:test` workflow. Cross-link from the pre-push hook section that excludes `test`.

## Dev Notes

### Why this story exists (carried from Story 4.0)

- Story 4.0 ships with `bunx nx run-many -t lint typecheck test --parallel --exclude web` in `nx-verify`. The `--exclude web` is a **temporary** measure — it keeps the unit tests for silo / projector / 4 libs running, but skips `apps/web:test` (Playwright e2e) because that target needs Auth0 + silo + KurrentDB + Postgres, none of which a fresh runner has.
- The pre-push hook excludes `test` entirely for the same reason.
- This story is the durable answer: a separate CI job that stands up the infra and runs the e2e suite against it.

### What's in `infrastructure/` today (for Task 1.2)

- `infrastructure/docker-compose.yml` is referenced by `docs/new-machine-setup.md` for local dev. It likely covers silo, KurrentDB, Postgres, OpenFGA, and a few other services; the exact list is in Task 1.2's audit.
- The CI runner's docker-compose stack may need a separate override file (e.g. `infrastructure/docker-compose.ci.yml`) with `restart: always`, healthcheck-driven `depends_on:`, and CI-friendly networking (service hostnames are reachable as plain hostnames from the job container, e.g. `postgres:5432`).
- Auth0 is the awkward one. Two viable paths:
  - **Real Auth0 test tenant** — declare `AUTH0_DOMAIN`, `AUTH0_CLIENT_ID`, `AUTH0_CLIENT_SECRET` as repo secrets. The fixture's `signInAs` flow uses the real tenant's hosted login page. This is the most realistic test but requires a maintained test tenant with seeded users.
  - **Local Auth0 mock** — `nodeauth/oauth2-mock-server` or similar mocks the OIDC endpoints in a container. The Astro app's `OIDC` flow still goes through a real-looking login page, but the user is auto-created. Faster, hermetic, but diverges slightly from production.

### Test target behavior (for Task 1.1)

- `apps/web:test` → `bun run test:e2e` → `playwright test`.
- Playwright config (`apps/web/playwright.config.ts`) auto-starts the dev server via `webServer.command`, which stubs the server-secret env vars so the dev server boots in a clean checkout. For CI with real infra, those stubs are replaced with real values (Task 3.1).
- 9 total tests; 2 pass without infra, 7 fail because `signInAs` calls Auth0 + the silo. With this story, all 9 should pass.

### Design decision: `--exclude web` in `nx-verify` (Task 4.1)

Two options:

- **(A) Keep `--exclude web` in `nx-verify`**, e2e runs only in `e2e-web`. Pro: e2e runs once; no duplicate work; `nx-verify` stays fast (lint + typecheck + unit tests, ~minutes). Con: if someone breaks the e2e target's plumbing (e.g. a playwright config typo), `nx-verify` won't catch it.
- **(B) Remove `--exclude web` in `nx-verify`**, e2e runs in both `nx-verify` and `e2e-web`. Pro: redundant coverage. Con: e2e is the slowest target (~5+ min); running it twice per push is wasteful.
- **Recommended: (A)**. The `e2e-web` job is the source of truth for the e2e target; `nx-verify` is for cheap gates. The `playwright.config.ts` env-reads in Task 3 also need a working infra stack to even boot the dev server, so running `apps/web:test` in `nx-verify` (which has no infra) would be a guaranteed failure — i.e. option (B) only works if we ALSO stand up infra in `nx-verify`, which is option (B) becoming equivalent to folding the `e2e-web` job into `nx-verify`. The story's architecture is intentionally two jobs so the cheap gate stays cheap.

### Auth0 in CI: known unknowns

- The e2e fixture calls `signInAs(page, user)` which uses the real Auth0 hosted login. CI runners have no problem hitting `*.auth0.com`, but the test tenant must be reachable AND have the test users seeded. The repo's Auth0 setup is in `infrastructure/auth0/` (verify during Task 1.2) — there's likely a `terraform` or `auth0-deploy-cli` config that creates the tenant + users.
- The `SESSION_SECRET` env var must match between the dev server and the e2e fixture for session cookies to round-trip. This is a Playwright/Astro quirk; the e2e job's `webServer.command` must use the same `SESSION_SECRET` that the test config reads.

### Cross-cutting

- **Story 4.5 (Local Dev & Deploy Updates)** may have overlap with infra setup work. Coordinate via Dev Notes when both stories are in flight.
- **Story 4.4 (Nx Cloud CI Integration)** affects caching of the e2e job's `bun install` and `playwright install` steps. If Nx Cloud is wired by then, the e2e job can be a cache hit on most pushes.

## Dev Agent Record

### Agent Model Used

_TBD on assignment_

### Debug Log References

_TBD during implementation_

### Completion Notes List

_TBD at completion_

### File List

**CREATED:** _TBD_
**MODIFIED:** _TBD_
**DELETED:** _TBD_

## Change Log

| Date       | Changes                                                                                                                                                                                                                                                                                                                       |
| ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-06-03 | Story created from Story 4.0 post-review discussion. Story 4.0 ships with `--exclude web` on the CI nx-verify `test` invocation as a temporary measure; this story is the durable fix — a separate `e2e-web` CI job that brings up Auth0 + silo + KurrentDB + Postgres via docker-compose and runs the full Playwright suite. |

## Out of Scope (carried to follow-up stories)

- Nx Cloud cache wiring for the e2e job (4.4).
- Replacing the Playwright fixtures with unit-level tests that don't need infra (would be a test-quality refactor, not infra work).
- Migrating the silo to run as a container in the e2e job's docker-compose stack (the e2e job uses the existing `infrastructure/docker-compose.yml`; if that file is .NET-only, this story either expands it or stands up a parallel compose file).
