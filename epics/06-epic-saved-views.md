# Epic 6: Saved Views & Personal Kanban Filters

## Vertical Slice Statement

Team members save their frequently used kanban filter and sort configurations as named views and switch between them instantly. After this Epic, each user can create a personalized set of perspectives on their kanban board, reducing repetitive filtering and making the board their own.

## Target Personas

- Developer / Team Member (primary -- creates personal views for their focus areas)
- Team Lead / Engineering Manager (secondary -- creates views for monitoring specific work streams)

## User Stories

1. As a team member, I want to apply filters (by tags, text) and sorting on the kanban board and then save that combination as a named view so that I can return to it later without re-configuring.
2. As a team member, I want to see my saved views as tabs or a dropdown at the top of the kanban board so that switching between perspectives is one click.
3. As a team member, I want to rename a saved view so that its label stays meaningful as my needs change.
4. As a team member, I want to delete a saved view so that I can clean up views I no longer use.
5. As a team member, I want switching between saved views to be instantaneous (no page reload) so that my flow is not interrupted.
6. As a team member, I want my saved views to be private to me (not shared with the team) so that personal organizational preferences do not clutter my teammates' boards.

## Acceptance Criteria

- [ ] A user can apply any combination of filters (tag include/exclude, text search) and sorting on the kanban board.
- [ ] A "Save this view" action is available when filters or sorting are active. Clicking it prompts for a name.
- [ ] The saved view stores: the filter configuration (tags, text), the sort order, and the user-provided name.
- [ ] Saved views appear as a list (tabs or dropdown) at the top of the kanban board. Clicking a view applies its filter and sort configuration instantly.
- [ ] The currently active view (if any) is visually highlighted.
- [ ] A user can rename a saved view via an inline edit or a context menu action.
- [ ] A user can delete a saved view via a context menu action. Deleting removes it from the list.
- [ ] Switching between saved views does not trigger a page reload; the board re-renders client-side with the new filter/sort applied.
- [ ] Saved views are scoped to the user and the product. A user's views in Product A do not appear in Product B.
- [ ] When no saved view is active, the board shows the default (unfiltered, default sort) state.
- [ ] Modifying filters while a saved view is active shows a visual indicator that the current state differs from the saved view (e.g., "Modified" badge, or an offer to update the saved view).

## Out of Scope for This Epic

- Shared/team-level saved views (Phase 2).
- Backlog saved views (extend to backlog if needed in Phase 2; MVP focuses on kanban board views).
- Report view saving (the report already has its own tag filter).

## Estimated Complexity

**Small-Medium** -- This is a focused feature built on the kanban board from Epic 4. The primary work is interaction design for saving, switching, and managing views.

## How to Demo

1. Navigate to the "Velucid Mobile App" kanban board.
2. Apply a filter: tag `area:frontend`. Sort by "Last Updated" ascending.
3. The board shows only frontend-tagged tasks.
4. Click "Save this view." Name it: "Frontend Tasks." Submit.
5. "Frontend Tasks" appears as a tab at the top of the board. It is highlighted as the active view.
6. Clear filters. The board shows all tasks. The "Frontend Tasks" tab is no longer highlighted.
7. Click "Frontend Tasks." The board instantly re-applies the frontend filter and sort.
8. Create a second saved view: filter by `type:bug`, name it "Bugs."
9. Switch between "Frontend Tasks" and "Bugs" tabs. The board updates instantly without a page reload.
10. Rename "Bugs" to "Bug Tracker." Delete the "Frontend Tasks" view. Verify only "Bug Tracker" remains.
