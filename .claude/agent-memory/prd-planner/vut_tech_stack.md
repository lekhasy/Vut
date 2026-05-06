---
name: Vut Tech Stack
description: The confirmed technology choices and architectural decisions for the Vut platform
type: project
---

## Confirmed Tech Stack
- **Frontend:** Astro.js with Tailwind CSS, SPA. Component library TBD.
- **Backend:** .NET with Proto.Actor framework
- **Event Store:** KurrentDB (formerly EventStoreDB)
- **Read Model:** PostgreSQL
- **Messaging:** Redpanda (Kafka-compatible streaming)
- **Hosting:** Kubernetes
- **Auth:** GitHub SSO only — no username/password auth

## Architecture
- Event-sourcing throughout: users, tasks, products — all are streams of events, not mutable objects.
- Simple timestamps for event ordering (no vector clocks or similar complexity).
- Tags are namespaced strings (e.g., `area:frontend`, `type:bug`) for flexible categorization.
- **Status is a dedicated property on tasks**, NOT a tag.
- **Data hierarchy:** Organization > Product > Task. Products are proper entities, not tags.
- **Multi-tenancy:** GitHub-style org model. Users belong to multiple orgs. Roles: owner, member.
- SPA frontend.

**Why:** These are hard constraints that shape all architectural decisions in the PRD.
**How to apply:** All design decisions must align with this stack. Do not suggest alternatives unless there is a concrete problem to solve. The hierarchy (Org > Product > Task) is fixed.
