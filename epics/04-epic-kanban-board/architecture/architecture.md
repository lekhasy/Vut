# Epic 4 Architecture: Kanban Board with Drag-and-Drop

## 1. System Context

Epic 4 introduces the Kanban Board -- a visual, column-based view of active work. It consumes existing event types (no new events) and reuses the Task Actor for status changes. The primary complexity is in the frontend: drag-and-drop, optimistic updates, keyboard accessibility, and the "Add from Backlog" drawer.

```mermaid
graph TB
    subgraph External
        User["User (Browser)"]
    end

    subgraph "Vut Platform (Epic 1-4)"
        subgraph Frontend["Astro.js SPA"]
            KB["Kanban Board<br/>(NEW)"]
            DnD["Drag-and-Drop Engine<br/>(NEW)"]
            SP["Card Detail Side Panel<br/>(NEW)"]
            BD["Backlog Drawer<br/>(NEW)"]
        end

        subgraph API["API Layer"]
            BFF["BFF / API Gateway"]
        end

        subgraph Backend[".NET Proto.Actor Backend"]
            TA["Task Actor<br/>(Epic 3)"]
        end

        subgraph ReadModel["PostgreSQL"]
            TP["task_projection<br/>(Epic 3)"]
            PSP["product_status_projection<br/>(Epic 2)"]
        end
    end

    User --> KB
    KB --> DnD
    KB --> SP
    KB --> BD
    KB --> BFF
    BFF --> TP
    BFF --> PSP
    DnD --> BFF
    BFF --> TA
```

## 2. Component Diagram

```mermaid
graph TB
    subgraph "Kanban Board Components"
        subgraph "Page Layout"
            BoardHeader["Board Header<br/>(Product name, filters)"]
            ColumnsContainer["Columns Container<br/>(Horizontal scroll)"]
        end

        subgraph "Column"
            ColHeader["Column Header<br/>(Status name, count)"]
            CardList["Card List<br/>(Virtualized, droppable zone)"]
        end

        subgraph "Card"
            CardTitle["Task Title"]
            CardTags["Tag Badges"]
        end

        subgraph "Overlays"
            DetailPanel["Detail Side Panel<br/>(Title, desc, tags, status)"]
            BacklogDrawer["Backlog Drawer<br/>(New tasks, searchable)"]
        end

        subgraph "Drag-and-Drop Engine"
            DnDManager["DnD Manager<br/>(Sensors, context)"]
            DragOverlay["Drag Overlay<br/>(Floating card)"]
            DropDetector["Drop Detector<br/>(Column boundaries)"]
        end

        subgraph "State Management"
            OptimisticStore["Optimistic Update Store<br/>(Pending changes)"]
            FilterState["Filter State<br/>(Tags, text search)"]
        end
    end

    BoardHeader --> ColumnsContainer
    ColumnsContainer --> ColHeader
    ColumnsContainer --> CardList
    CardList --> CardTitle
    CardList --> CardTags
    DnDManager --> DragOverlay
    DnDManager --> DropDetector
    OptimisticStore --> BoardHeader
    FilterState --> BoardHeader
```

## 3. Data Flow

### 3.1 Board Loading

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant RM as Read Model API
    participant PG as PostgreSQL

    User->>Browser: Navigate to Kanban Board tab
    Browser->>BFF: GET /api/products/{productId}/board

    par Fetch product statuses
        BFF->>RM: GET /api/products/{productId}/statuses
        RM->>PG: SELECT * FROM product_status_projection WHERE product_id = ?
        PG-->>RM: Statuses ordered by sort_order
        RM-->>BFF: statuses (exclude first = "New")
    and Fetch active tasks
        BFF->>RM: GET /api/products/{productId}/tasks?status!=New
        RM->>PG: SELECT * FROM task_projection WHERE product_id = ? AND status != 'New'
        PG-->>RM: Active tasks
        RM-->>BFF: Task list
    end

    BFF->>Browser: Board data (columns + cards)
    Browser->>Browser: Render columns and cards
    Browser->>User: Kanban board displayed
```

### 3.2 Drag-and-Drop Status Change (Optimistic Update)

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant AS as Actor Service
    participant K as KurrentDB
    participant RP as Redpanda
    participant PJ as Task Projector
    participant PG as PostgreSQL

    User->>Browser: Start dragging card from "In Progress"
    Browser->>Browser: Show drag overlay (lifted card)
    Browser->>Browser: Highlight valid drop zones

    User->>Browser: Drop on "In Review" column
    Browser->>Browser: OPTIMISTIC: Move card to "In Review" in local state
    Browser->>Browser: Show pending indicator on card

    Browser->>BFF: PATCH /api/tasks/{taskId}/status { newStatus: "In Review" }

    BFF->>AS: ChangeStatusCommand(taskId, "In Review")
    AS->>K: Append TaskStatusChanged(oldStatus="In Progress", newStatus="In Review")
    K-->>AS: OK
    K->>RP: Publish event
    AS-->>BFF: 200 OK

    BFF->>Browser: Success response
    Browser->>Browser: Remove pending indicator
    Browser->>User: Card settled in "In Review", toast: "Status changed"

    Note over Browser: If backend fails:
    Note over Browser: 1. Revert card to original column
    Note over Browser: 2. Show error toast
    Note over Browser: 3. Remove pending indicator
```

### 3.3 "Add from Backlog" Drawer

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant RM as Read Model API
    participant AS as Actor Service
    participant K as KurrentDB

    User->>Browser: Click "+" on first column header
    Browser->>Browser: Open backlog drawer (slide-in panel)
    Browser->>BFF: GET /api/products/{productId}/tasks?status=New
    BFF->>RM: Query tasks with status "New"
    RM-->>BFF: New tasks list
    BFF->>Browser: Drawer shows "New" tasks

    User->>Browser: Click "Move" on a task
    Browser->>Browser: OPTIMISTIC: Add card to first active column, remove from drawer
    Browser->>BFF: PATCH /api/tasks/{taskId}/status { newStatus: "In Progress" }
    BFF->>AS: ChangeStatusCommand(taskId, "In Progress")
    AS->>K: Append TaskStatusChanged
    K-->>AS: OK
    AS-->>BFF: OK
    BFF->>Browser: Success
    Browser->>User: Card appears in column, drawer updates
```

### 3.4 Card Detail Panel

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant RM as Read Model API
    participant AS as Actor Service
    participant K as KurrentDB

    User->>Browser: Click card
    Browser->>Browser: Open side panel (slide from right)
    Browser->>BFF: GET /api/tasks/{taskId}
    BFF->>RM: Fetch task details
    RM-->>BFF: Full task data (title, desc, tags, status)
    BFF->>Browser: Render detail panel

    User->>Browser: Edit title inline
    Browser->>Browser: OPTIMISTIC: Update title locally
    Browser->>BFF: PATCH /api/tasks/{taskId} { title: "New Title" }
    BFF->>AS: ChangeTitleCommand(taskId, "New Title")
    AS->>K: Append TaskTitleChanged
    K-->>AS: OK
    AS-->>BFF: OK
    Browser->>User: Title updated in both panel and card
```

## 4. Frontend Architecture

### 4.1 Kanban Board Page Structure

```mermaid
graph TD
    subgraph "Kanban Page Layout"
        Nav["Product Navigation<br/>(Backlog | Board | Report)"]
        Toolbar["Board Toolbar<br/>(Filters, Search, Backlog Drawer toggle)"]
        BoardArea["Board Area<br/>(Horizontal scroll)"]
    end

    subgraph "Board Area Contents"
        C1["Column: In Progress<br/>(Droppable)"]
        C2["Column: In Review<br/>(Droppable)"]
        C3["Column: Done<br/>(Droppable)"]
    end

    subgraph "Overlays"
        Panel["Detail Side Panel"]
        Drawer["Backlog Drawer"]
        DragCard["Drag Overlay Card"]
    end

    Nav --> Toolbar
    Toolbar --> BoardArea
    BoardArea --> C1
    BoardArea --> C2
    BoardArea --> C3
    C1 --> Panel
    C1 --> DragCard
```

### 4.2 Drag-and-Drop Library

Recommended library: **@dnd-kit/core** (or equivalent for Astro.js framework integration)

Key configuration:
- **Sensors:** Pointer sensor (mouse/touch) with activation constraint (5px distance to prevent accidental drags)
- **Droppable columns:** Each status column is a droppable area
- **Draggable cards:** Each task card is draggable
- **Drag overlay:** A rendered copy of the card follows the cursor during drag
- **Collision detection:** Rect intersection algorithm to determine target column

### 4.3 Optimistic Update Pattern

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Pending: Drag-and-drop completed
    Pending --> Confirmed: API 200 OK
    Pending --> Reverted: API error / timeout
    Confirmed --> Idle: Reset
    Reverted --> Idle: User dismisses error

    note right of Pending
        Card is in new column with
        visual pending indicator
        (e.g., subtle pulse animation)
    end note

    note right of Reverted
        Card snaps back to original
        column, error toast shown
    end note
```

### 4.4 Optimistic Update Store

The client-side store tracks pending operations:

```typescript
// Pseudocode for optimistic update state
interface PendingChange {
  taskId: string;
  type: "status_change";
  originalStatus: string;
  newStatus: string;
  timestamp: Date;
}

interface BoardState {
  columns: Map<string, Task[]>;  // status -> tasks
  pendingChanges: Map<string, PendingChange>;  // taskId -> pending
  filters: BoardFilters;
}
```

When a drag completes:
1. Add entry to `pendingChanges`.
2. Move the task in `columns` map from old status to new status.
3. Fire API request.
4. On success: remove from `pendingChanges`.
5. On failure: reverse the `columns` move, remove from `pendingChanges`, show error toast.

### 4.5 Keyboard Navigation

```mermaid
flowchart TD
    Tab[Tab key] --> FocusBoard["Focus enters board"]
    ArrowRight["Arrow Right"] --> NextCard["Focus next card in column"]
    ArrowLeft["Arrow Left"] --> PrevCard["Focus previous card in column"]
    ArrowUp["Arrow Up"] --> PrevColCard["Focus card above in same column"]
    ArrowDown["Arrow Down"] --> NextColCard["Focus card below in same column"]
    Enter["Enter"] --> OpenDetail["Open detail side panel"]
    Escape["Escape"] --> CloseDetail["Close detail panel"]

    FocusBoard --> ArrowRight
```

Keyboard accessibility requirements:
- All cards are focusable (`tabindex` managed by roving tabindex pattern).
- Arrow keys move focus between cards within and across columns.
- Enter opens the detail side panel.
- Escape closes the detail side panel.
- ARIA labels on columns (e.g., "In Progress column, 5 tasks") and cards (e.g., "Implement login screen, status: In Progress").
- Focus is trapped in the detail panel when open.

### 4.6 Responsive Behavior

- **Desktop (>1024px):** Columns are displayed side by side, horizontal scroll if > 4 columns.
- **Tablet (768-1024px):** Columns are narrower, horizontal scroll always enabled.
- **Mobile (<768px):** Columns stack vertically (swipe between columns), cards are full-width. Touch drag is supported.

## 5. API Design

No new API endpoints are introduced in this epic. The Kanban Board consumes:

| Endpoint | Usage |
|----------|-------|
| `GET /api/products/{productId}/statuses` | Column headers |
| `GET /api/products/{productId}/tasks?status!=New` | Board data (excludes "New") |
| `PATCH /api/tasks/{taskId}/status` | Drag-and-drop status change |
| `GET /api/tasks/{taskId}` | Card detail panel |
| `PATCH /api/tasks/{taskId}` | Inline title/description edit from panel |
| `POST /api/tasks/{taskId}/tags` | Add tag from panel |
| `DELETE /api/tasks/{taskId}/tags/{tag}` | Remove tag from panel |
| `GET /api/products/{productId}/tags?q=` | Tag autocomplete in panel |

### 5.1 Board Data Endpoint (Optimized)

A dedicated board endpoint that returns all data in a single call:

```
GET /api/products/{productId}/board

Response:
{
  "columns": [
    { "status": "In Progress", "sortOrder": 1 },
    { "status": "In Review", "sortOrder": 2 },
    { "status": "Done", "sortOrder": 3 }
  ],
  "tasks": [
    {
      "taskId": "...",
      "title": "Implement login",
      "status": "In Progress",
      "tags": ["area:frontend", "priority:high"],
      "updatedAt": "2026-05-05T15:00:00Z"
    }
  ]
}
```

## 6. State Diagram: Board Interaction

```mermaid
stateDiagram-v2
    [*] --> Viewing: Board loaded
    Viewing --> Dragging: Mousedown on card + drag
    Dragging --> Viewing: Drop on column (optimistic update)
    Dragging --> Viewing: Escape (cancel drag)
    Viewing --> DetailOpen: Click card / Enter
    DetailOpen --> Viewing: Escape / Close
    DetailOpen --> Editing: Click title/description
    Editing --> DetailOpen: Save / Blur / Escape
    Viewing --> DrawerOpen: Click "+" / "Add from Backlog"
    DrawerOpen --> Viewing: Close drawer
    DrawerOpen --> Viewing: Move task to board
    Viewing --> Filtering: Apply filter
    Filtering --> Viewing: Clear filter
```

## 7. Performance Considerations

### 7.1 Board Rendering

- **Virtualized card lists:** Columns with > 20 cards use virtualization (render only visible cards).
- **Memoized cards:** Task cards are memoized components -- re-render only when their specific data changes.
- **Column reflow:** Columns are CSS Grid or Flexbox. No JavaScript-based layout calculations.

### 7.2 Drag-and-Drop Performance

- Drag overlay is a single DOM element that follows the cursor. The original card becomes a placeholder.
- Collision detection runs on `requestAnimationFrame`, not on every mouse move.
- During drag, no API calls are made until the drop event.

### 7.3 Optimistic Update Safety

- Pending changes have a client-side timeout (10 seconds). If the API doesn't respond, the change is reverted.
- Only one pending status change per task is allowed. A second drag on the same task while pending is queued.
- The pending indicator provides visual feedback that the change hasn't been confirmed yet.

## 8. Accessibility (a11y)

| Feature | Implementation |
|---------|---------------|
| Roving tabindex | Only one card is in the tab order at a time. Arrow keys move the tabindex. |
| ARIA roles | Board: `role="grid"`, Columns: `role="row"`, Cards: `role="gridcell"` |
| ARIA labels | Column: `"In Progress column, 5 tasks"`, Card: `"Implement login, In Progress"` |
| Live regions | `aria-live="polite"` on toast container for status change announcements |
| Focus management | Focus moves to detail panel when opened, returns to card when closed |
| Keyboard shortcuts | Enter (open detail), Escape (close), Arrow keys (navigate) |
| Screen reader | Status changes announced: `"Task moved to In Review"` |

## 9. Impact on Future Epics

| Component | Used By |
|-----------|---------|
| Board data endpoint | Epic 5 (Report may reference board), Epic 6 (Saved Views extends board) |
| Filter state management | Epic 6 (Saved Views serializes/deserializes filter state) |
| Optimistic update pattern | Epic 5 (if any interactive report features), Phase 2 (real-time updates) |
| Card detail panel | Reused across all epics for task interaction |
| Drag-and-drop engine | Phase 2 (real-time collaborative drag-and-drop) |
