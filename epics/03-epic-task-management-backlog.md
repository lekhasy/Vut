# Epic 3: Task Management & Backlog

## Vertical Slice Statement

Team members create tasks, write descriptions, apply namespaced tags, and manage them in a filterable, sortable backlog list. After this Epic, the core data-entry loop of Velucid is complete -- users can populate their product with work items and find them again quickly.

## Target Personas

- Developer / Team Member (primary -- creates and tags tasks)
- Team Lead / Engineering Manager (secondary -- views and filters the backlog)

## User Stories

1. As a team member, I want to create a task with a title and optional markdown description so that I can capture work that needs to be done.
2. As a team member, I want a newly created task to default to "New" status so that it appears in the backlog but not on the kanban board.
3. As a team member, I want to edit a task's title and description inline so that I can keep details current without opening a modal.
4. As a team member, I want to add and remove namespaced tags (e.g., `area:frontend`, `type:bug`) so that I can categorize work flexibly.
5. As a team member, I want tag autocomplete to suggest previously used tags in the product so that I maintain consistency without looking up exact strings.
6. As a team member, I want to change a task's status to any status defined for the product so that I can move work through the workflow.
7. As a team member, I want to filter the backlog by status, tags (include/exclude), and text search so that I can find relevant tasks quickly.
8. As a team member, I want to sort the backlog by created date, last updated, title, or status so that I can view tasks in the order that matters to me.
9. As a team member, I want to create tasks directly from the backlog view so that my workflow is not interrupted.

## Acceptance Criteria

- [ ] A user can create a task in any product they have access to. Required field: title. Optional: description (markdown), tags.
- [ ] The task's status defaults to the product's starting status ("New").
- [ ] A user can edit a task's title inline.
- [ ] A user can edit a task's description inline with markdown preview.
- [ ] A user can add a tag in `namespace:value` format.
- [ ] A user can remove a tag.
- [ ] Tag autocomplete suggests tags previously used in the same product, scoped by namespace prefix (e.g., typing `area:` suggests `area:frontend`, `area:backend`).
- [ ] A user can change a task's status to any status defined for the product.
- [ ] The backlog view displays all tasks in the product in a list with columns: title, status, tags, created date, last updated.
- [ ] Filters: status (single or multiple selection), tags (include/exclude by namespace:value), text search (title and description).
- [ ] Sorting: created date, last updated, title, status. Default: created date descending.
- [ ] The backlog view loads in under 1 second for products with up to 10,000 tasks.

## Out of Scope for This Epic

- Kanban board drag-and-drop (Epic 4) -- status changes in this Epic are done via dropdown or inline control.
- Probabilistic forecast (Epic 5).
- Saved views (Epic 6).
- Task assignment to users (Phase 2).
- Comments / activity feed (Phase 2).

## Estimated Complexity

**Large** -- This is the most feature-rich data-entry Epic: task creation, inline editing, tag autocomplete, and full filter/sort capabilities.

## How to Demo

1. Navigate to the "Velucid Mobile App" product from Epic 2.
2. Click "New Task." Enter title: "Implement login screen." Add description in markdown. Add tag `area:frontend`. Submit.
3. The task appears in the backlog with "New" status.
4. Create a second task: "Fix API timeout." Tag: `type:bug`. Create a third: "Design onboarding flow." Tag: `area:design`.
5. Inline-edit the first task's title to "Implement mobile login screen."
6. Add tag `priority:high` to the bug task. Verify autocomplete suggests `priority:` from the tag index after typing `p`.
7. Filter the backlog to show only `type:bug`. One task appears.
8. Sort by last updated. Recently edited tasks appear first.
9. Change the bug task's status from "New" to "In Progress" via the status dropdown.
