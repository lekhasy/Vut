# CLAUDE.md

> Auto-loaded context for Claude Code sessions on the Velucid repo. Keep this file
> concise — it's read on every session. Long-form details live in `docs/` and
> `_bmad-output/planning-artifacts/`.

## Repo in three lines

Velucid is an event-sourced, CQRS-style task-management platform: a .NET 10
Orleans **silo** writes events to **KurrentDB**, a **projector** (TypeScript /
Fastify / Bun, porting from .NET) projects them into **PostgreSQL**, and an
**Astro SSR web app** acts as the BFF and sole Auth0 entry point.

**Stack:** .NET 10 (silo + projector) · Astro 5 + React 19 (web) · KurrentDB ·
PostgreSQL · OpenFGA · Auth0 · K3s + ArgoCD · Bun 1.3.14 (JS toolchain, Astro
runtime) · Nx 22.7.5 monorepo.

## Day-to-day commands (Story 4.0+)

| Task                                 | Command                                              |
| ------------------------------------ | ---------------------------------------------------- |
| Install workspace deps               | `bun install --frozen-lockfile`                      |
| Serve the web app (`localhost:4321`) | `bunx nx serve web`                                  |
| Build everything                     | `bunx nx run-many -t build --parallel`               |
| Lint / typecheck / test everything   | `bunx nx run-many -t lint typecheck test --parallel` |
| Only what changed vs `main`          | `bunx nx affected -t lint typecheck test --parallel` |
| Render the dep graph                 | `bunx nx graph`                                      |
| Format-check (CI does this)          | `bunx nx format:check`                               |
| Format-fix (local)                   | `bunx nx format:write`                               |
| Pre-push gate (mirrors CI nx-verify) | `bun run preflight`                                  |

> **Bun is the only JS toolchain.** `bunx nx ...`, never `npx nx ...`. The web
> container runs under `bun ./dist/server/entry.mjs` in production.

## Repo layout

```
apps/
  web/         Astro 5 SSR + React 19 islands (Bun runtime in prod)
  projector/   Bun + Fastify + TS (skeleton in 4.0; port of .NET projector in 4.2)
  silo/        Nx wrapper over backend/src/Velucid.Silo (dotnet 10)
libs/
  events/           @velucid/events          (populated 4.1)
  projection/       @velucid/projection      (populated 4.1)
  read-model/       @velucid/read-model      (populated 4.1)
  kurrent-client/   @velucid/kurrent-client  (populated 4.1)
backend/       Legacy .NET solution (silo + projector-service + migrations).
                Untouched in 4.0; cleaned up in 4.5.
infrastructure/ K8s manifests, Docker Compose, secrets, observability stack.
docs/          Setup, forecasting spec, authorization reference.
_bmad-output/  BMad planning + implementation artifacts. Source of truth for
               active sprint, stories, and planning docs.
```

## Architectural guardrails (do not violate)

- **CQRS**: commands emit events; queries read from projected state.
- **BFF auth**: the Astro web app (`apps/web`) is the **sole** auth entry point.
  The silo and projector do not implement their own auth flow.
- **Catch-up subscription**: projector subscribes to `$ce` from the last
  checkpoint. Same pattern in .NET and TS implementations.
- **Event sourcing**: grain state is rebuilt from events. Never mutate grain
  state directly.
- **Optimistic UI**: 4.3 wires a local read-model store; 4.0 is layout-only.
- **Tag constraints** (enforced by `@nx/enforce-module-boundaries`):
  `type:lib` may only import from `type:lib`. `type:app` may import from
  `type:lib` or `type:app`. Libs **never** import apps.

## Voice & brand

`marketing_strategy.md` (repo root) is the source of truth for landing-page,
README, email, changelog, and any other public-facing copy. It defines brand
voice (dbrand × Palantir register), banned words, spice levels, and the
credibility framing. Deviating from it — including in tone, in copy, or in
visual register — requires explicit instruction. Visual reference shots for
the landing-page redesign live in
`_bmad-output/planning-artifacts/marketing-research/`.

## Nx Cloud (opt-in, Story 4.4)

The workspace is currently **local-cache only** — no `nxCloudId` is set, so
Nx never tries to talk to the cloud. Every build, test, and lint hits the
local `.nx/cache` and is fully reproducible. If/when you want to share cache
across machines (CI + local), run `bunx nx connect`, set the resulting
`nxCloudId` in `nx.json`, and add `NX_CLOUD_ACCESS_TOKEN` as a CI secret.
That's a Story 4.4 task — flag it when we get there.

## Pre-push hook (catch CI failures before they land)

A git `pre-push` hook lives in `scripts/hooks/pre-push` and is wired up via
`git config core.hooksPath scripts/hooks` (the `prepare` script in the root
`package.json` sets this automatically after `bun install`, so anyone who
clones the repo and installs gets the hook).

The hook runs `bunx nx format:check` + `bunx nx run-many -t lint typecheck
build --parallel` — the same gate the CI `nx-verify` job runs, plus `build`
(to catch regressions like a `--no-restore` flag in a dotnet typecheck target
that would fail on a clean CI checkout). It does **not** run `test` because
`apps/web:test` (Playwright e2e) needs a live Auth0 + silo + KurrentDB +
Postgres stack that almost never exists locally.

- `bun run preflight` — the same set + `test`. Use this when you want full
  coverage locally; it will fail on the e2e suite without infra (expected).
- `git push --no-verify` — bypass the hook for a WIP push; you'll get a red
  CI nx-verify and have to fix forward, but it's faster than fighting the
  hook for a hotfix.

## Story workflow reminder

Active sprint state is in `_bmad-output/implementation-artifacts/sprint-status.yaml`.
The `dev-story` skill reads this file to find the next story. Always update
`File List`, `Change Log`, and `Status` in the story file before flipping
status to `review` in `sprint-status.yaml`.
