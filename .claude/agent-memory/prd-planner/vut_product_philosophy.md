---
name: Vut Product Philosophy
description: Core product philosophy and hard exclusions for Vut -- what the product IS and IS NOT
type: project
---

## #Noestimate Philosophy
Vut is a #noestimate project management tool. Work is tracked by flow, not by time. The cumulative flow diagram (with projected completion date) is the single report. Everything else exists to make that signal accurate.

## Hard Exclusions (MVP and beyond)
- NO time tracking, timers, timesheets
- NO story points, estimates, T-shirt sizes, relative sizing
- NO sprints, iterations, time-boxed planning cycles
- NO velocity metrics or throughput-per-sprint
- NO Gantt charts, dependency mapping, timeline views
- NO multiple report types -- cumulative flow only
- NO custom field builder
- NO email/password auth -- GitHub SSO only
- NO mobile apps (MVP)
- NO public API (MVP)
- NO third-party integrations (MVP)

## Views
- **Backlog:** Shows ALL tasks with filters/sorting. Default landing page for a product.
- **Kanban board:** Shows tasks with status OTHER than "New". Columns = non-"New" statuses. Drag-and-drop. Saved filter/sort configurations replace dedicated views.
- **Report:** Cumulative flow diagram with projected completion date. Tag-filterable.

**Why:** These are non-negotiable product constraints. Violating them (e.g., adding story points) would undermine the product identity.
**How to apply:** Any feature request that introduces time-centric mechanics must be flagged as conflicting with the core philosophy. Do not suggest features from these excluded categories.
