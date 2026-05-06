# Epic 4: Kanban Board with Drag-and-Drop

## Vertical Slice Statement

Team members view active work as columns on a kanban board and drag task cards between columns to update status. After this Epic, the team has a visual, tactile way to manage flow -- the core daily interaction surface of Vut.

## Target Personas

- Developer / Team Member (primary -- moves cards daily)
- Team Lead / Engineering Manager (secondary -- monitors board state)

## User Stories

1. As a team member, I want to see a kanban board with one column per non-"New" status so that I can visualize where work stands.
2. As a team member, I want tasks in "New" status to be excluded from the board so that the board only shows active work.
3. As a team member, I want to drag a task card from one column to another so that I can update its status with a single gesture.
4. As a team member, I want to click a card to see its details (title, description, tags) in a side panel or expanded view so that I can review context without leaving the board.
5. As a team member, I want to move a task from "New" to the first active column (e.g., "In Progress") directly from the board so that I can start work on backlog items without switching views.
6. As a team lead, I want the board to update instantly when I drag a card (optimistic update) so that the UI feels fast.
7. As a team member using keyboard navigation, I want to move between cards with arrow keys and open details with Enter so that the board is accessible without a mouse.

## Acceptance Criteria

- [ ] The kanban board displays one column for each status defined for the product, excluding the starting status ("New").
- [ ] Tasks in "New" status do not appear on the kanban board under any circumstance.
- [ ] Each task card shows: title, tags (as badges), and a visual indicator of its current status column.
- [ ] Dragging a card from column A to column B emits a `TaskStatusChanged` event (oldStatus = A's status, newStatus = B's status) and moves the card in the UI.
- [ ] The drag-and-drop interaction provides visual feedback: the card lifts from its column, a drop zone highlights, and the card animates into the new column.
- [ ] An "Add from Backlog" affordance (e.g., a mini-backlog drawer or a "+" at the top of the first column) allows moving a "New" task to the first active status without navigating away.
- [ ] Clicking a card opens a detail side panel showing title, description (rendered markdown), tags, and status. Title and description can be edited inline from this panel.
- [ ] Optimistic update: the card moves immediately in the UI; if the backend command fails, the card snaps back with an error toast.
- [ ] Keyboard navigation: Tab/arrow keys move focus between cards; Enter opens the detail panel; Escape closes it.
- [ ] The board is the secondary navigation view for a product (accessible via tab or sidebar link, alongside Backlog and Report).
- [ ] Basic filters (by tags, text search) are available on the board, with a clear indication when filters are active.

## Event Streams Introduced

No new event types. This Epic consumes existing `TaskStatusChanged` events and emits them via drag-and-drop.

## Projection Views Used

| View | Purpose |
|------|---------|
| Task Projection | Current status, title, tags for rendering cards in columns |
| Product Projection | Status list for column headers |

## Technical Scope

- **Astro.js UI**: kanban board layout, drag-and-drop library integration (e.g., dnd-kit, react-beautiful-dnd equivalent), card components, detail side panel, "Add from Backlog" drawer.
- **Proto.Actor**: no new actors. Task actor handles `TaskStatusChanged` commands from board interactions.
- **Optimistic update logic**: client-side state management that applies the status change immediately, reverts on failure.
- **Accessibility**: keyboard-navigable board, ARIA labels on columns and cards, focus management.
- **Responsive**: columns scroll horizontally on narrower viewports; cards stack vertically within columns.

## Out of Scope for This Epic

- Saved views / named filter presets (Epic 6).
- Cumulative flow diagram (Epic 5).
- Real-time collaborative updates (Phase 2 -- WebSocket-based).
- Task assignment (Phase 2).

## Estimated Complexity

**Medium-Large** -- The drag-and-drop interaction, optimistic updates, and keyboard accessibility represent significant frontend complexity. The backend changes are minimal (reusing the Task actor's status change command).

## How to Demo

1. Navigate to the "Vut Mobile App" product. Switch to the Kanban Board tab.
2. The board shows three columns: "In Progress", "In Review", "Done" (matching the statuses from Epic 2, minus "New").
3. The bug task "Fix API timeout" is in "In Progress" (moved there in Epic 3). It appears as a card with tag `type:bug`.
4. Drag "Fix API timeout" from "In Progress" to "In Review." The card animates into the new column. A toast confirms: "Status changed to In Review."
5. Click the card to open the detail side panel. Verify title, description, and tags are displayed.
6. Use the "Add from Backlog" drawer to move "Implement mobile login screen" from "New" to "In Progress." It appears as a card.
7. Navigate using only the keyboard: Tab to a card, arrow keys to move between cards, Enter to open details, Escape to close.
8. Filter by tag `area:frontend`. Only matching cards appear. A filter badge indicates the active filter.
