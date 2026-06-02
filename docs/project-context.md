# Project Context — Velucid

## Overview

Velucid is a project forecasting platform with Kanban-style task management. Built on Orleans (event-sourced grains) + KurrentDB (event store) + PostgreSQL (read model) + OpenFGA (authorization).

## Architecture

- **Backend:** .NET 10, Orleans Silo, KurrentDB event sourcing, PostgreSQL read model
- **Projector:** Separate BackgroundService host, subscribes to KurrentDB persistent subscriptions
- **Authorization:** OpenFGA for declarative, centralized permission checks
- **Frontend:** React (planned)

## Key Reference Documents

- [Domain Command Reference](authorization-reference.md) — all commands, events, roles, permissions, and their mapping to OpenFGA. **Load this when working on any authorization or grain-related story.**
- [PRD](../PRD.md) — full product vision, requirements, success metrics, and feature specifications. **Load this when planning, making product decisions, or needing to understand feature intent.**
- [Architecture Design](../_bmad-output/planning-artifacts/architecture.md) — system architecture decisions

## Project Conventions

- CQRS: commands emit events (source of truth), queries read from projected state
- OpenFGA tuple writes happen in the projector (not in grains) — avoids inconsistency windows
- Auth failures throw `UnauthorizedAccessException`; domain validation throws `InvalidOperationException`
- Event-sourced grains extend `EventSourcedGrain<TState>` with `Apply(state, event)` pattern
