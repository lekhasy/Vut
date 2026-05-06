# Vut Epic Overview

## Project: Vut -- #noestimate Project Management Platform

### Slicing Strategy

Each Epic is a **vertical slice** that delivers standalone, demonstrable value to real users. No Epic is a "layer" (no "Backend API", no "Frontend UI", no "Database Setup"). Every Epic spans the full stack -- event streams, projections, backend actors, and frontend UI -- so that a user can sign in, interact, and benefit.

The Epics are ordered by dependency: earlier Epics establish the foundation that later Epics build on. Each Epic assumes the infrastructure from previous Epics exists but is independently deployable.

---

## Epic Summary

| Epic | Title | User Value |
|------|-------|------------|
| 1 | [First Sign-In & Organization](./01-epic-first-signin-org.md) | A user can authenticate via GitHub, create an organization, and invite team members. |
| 2 | [Product Setup with Configurable Workflow](./02-epic-product-setup.md) | An organization member can create a product with custom statuses, establishing the container where work will live. |
| 3 | [Task Management & Backlog](./03-epic-task-management-backlog.md) | Users can create, edit, tag, and filter tasks in a searchable backlog -- the core data-entry experience. |
| 4 | [Kanban Board with Drag-and-Drop](./04-epic-kanban-board.md) | Users can move tasks through workflow columns via drag-and-drop, watching work flow in real time. |
| 5 | [Cumulative Flow & Completion Forecast](./05-epic-cumulative-flow-forecast.md) | Users see a stacked-area chart of work over time with a projected completion date -- the central signal of Vut. |
| 6 | [Saved Views & Personal Kanban Filters](./06-epic-saved-views.md) | Users can save, name, and switch between filtered kanban views for recurring perspectives on their work. |

---

## Dependency Graph

```
Epic 1: First Sign-In & Organization
  |
  v
Epic 2: Product Setup with Configurable Workflow
  |
  v
Epic 3: Task Management & Backlog
  |
  +---> Epic 4: Kanban Board with Drag-and-Drop
  |       |
  |       v
  |     Epic 6: Saved Views & Personal Kanban Filters
  |
  v
Epic 5: Cumulative Flow & Completion Forecast
```

Epic 4 and Epic 5 both depend on Epic 3 (tasks must exist to visualize or move them). Epic 6 depends on Epic 4 (saved views extend the kanban board). Epic 5 can be built in parallel with Epics 4 and 6.

---

## Technical Context (Cross-Cutting)

These concerns are not Epics themselves but are established progressively as part of the Epics that need them:

| Concern | Introduced In |
|---------|---------------|
| Event sourcing infrastructure (KurrentDB, streams, projections) | Epic 1 |
| Proto.Actor actors for aggregate roots | Epic 1 |
| Redpanda event publishing + PostgreSQL read model projectors | Epic 1 |
| Auth0 integration + GitHub SSO | Epic 1 |
| Astro.js SPA shell, routing, Tailwind design system | Epic 1 |
| Kubernetes deployment manifests | Epic 1 |

---

## Definition of Done (Per Epic)

An Epic is done when:

1. All acceptance criteria in the Epic file are met.
2. The feature is usable end-to-end by the target persona(s) listed in the Epic.
3. Events are correctly emitted to KurrentDB and projected to PostgreSQL.
4. The UI follows the design guidelines from the PRD (sidebar nav, toast notifications, inline editing where applicable).
5. No time-centric features have been introduced (hard constraint).
