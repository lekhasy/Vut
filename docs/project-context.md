# Project Context — Velucid

*Last revised: 2026-06-07 (post-redesign).*

## Overview

Velucid is a project management platform with explicit-aggregate work organization and probabilistic forecasting. Built on .NET 10 Orleans (event-sourced grains) + KurrentDB (event store) + PostgreSQL (read model, projected by the C# projector) + OpenFGA (authorization). The product surface is a React 19 SPA that is local-first; the Astro 5 BFF owns authentication (email-OTP) and serves the landing page only.

## Architecture

- **Backend:** .NET 10, Orleans Silo, KurrentDB event sourcing, PostgreSQL read model.
- **Projector:** C# (.NET) — canonical. The TS/Bun port is deferred indefinitely.
- **Authorization:** OpenFGA for declarative, centralized permission checks.
- **Frontend split:** Astro 5 (BFF + landing page) and React 19 SPA (product surface, local-first, separate Nx app).
- **Auth:** email-OTP. No third-party IdP. Astro BFF owns login. Transactional email provider (TBD) for OTP codes and notification emails.
- **Real-time:** transport TBD (the client lib is in development). Behavior: teammate changes appear in the local store live.

## Key Reference Documents

- [Domain Command Reference](authorization-reference.md) — all commands, events, roles, permissions, and their mapping to OpenFGA. **Load this when working on any authorization or grain-related story.**
- [PRD](../_bmad-output/planning-artifacts/prds/prd-Velucid-2026-06-07/prd.md) — full product vision, requirements, and feature specifications (per the 2026-06-07 redesign). **Load this when planning, making product decisions, or needing to understand feature intent.**
- [Architecture Design](../_bmad-output/planning-artifacts/architecture.md) — system architecture decisions (revised 2026-06-07 to match the new PRD).

## Project Conventions

- CQRS: commands emit events (source of truth), queries read from projected state.
- OpenFGA tuple writes happen in the projector (not in grains) — avoids inconsistency windows.
- Auth failures throw `UnauthorizedAccessException`; domain validation throws `InvalidOperationException`.
- Event-sourced grains extend `EventSourcedGrain<TState>` with `Apply(state, event)` pattern.
- Events encode the *meaning* of actions — no `TagAdded` / `TagRemoved` events for structural relations. Use the explicit aggregate event (e.g., `WorkItemAssignedToTeam`).
- Per-Workspace tenancy: all reads scoped to Workspaces the user is an Account of.
