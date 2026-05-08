---
name: Vut Virtual Actor Architecture Redesign
description: Epic 1 architecture redesigned from Actor Manager pattern to Proto.Actor virtual actors (grains) with KurrentDB event sourcing
type: project
---

Vut's Epic 1 architecture was redesigned to use Proto.Actor virtual actors (grains) instead of the manual ActorManagerBase pattern.

**Why:** The Actor Manager pattern introduced bottlenecks (single manager per kind), manual lifecycle bookkeeping, PID caching complexity, failure recovery gaps, and single-node affinity. Virtual actors solve all of these through automatic activation, location-transparent routing, and cluster identity partitioning.

**How to apply:** All future epics (2-6) should follow the virtual actor pattern:
1. Define grain class inheriting from AggregateGrain<TState>
2. Register cluster kind: ClusterKind.Get("kind", Props)
3. Use Proto.Persistence.EventStore for KurrentDB event sourcing bridge
4. Add projector handlers for new stream types
5. No manager actors needed

Key cluster kinds: "user", "organization" (Epic 1), "product" (Epic 2), "task" (Epic 3).
Cluster transport: Redpanda (NOT for event distribution -- projectors use KurrentDB persistent subscriptions directly).
Identity lookup: PartitionIdentityLookup (hash-based partitioning).
