# Epic 6 Architecture: Saved Views & Personal Kanban Filters

## 1. System Context

Epic 6 adds personal, persistent filter/sort configurations on top of the Kanban Board from Epic 4. Saved views are scoped per user per product and allow instant switching between perspectives without reconfiguring filters. This is a focused feature with primarily frontend and API work; the backend adds a lightweight persistence mechanism.

```mermaid
graph TB
    subgraph External
        User["User (Browser)"]
    end

    subgraph "Vut Platform (Epic 1-6)"
        subgraph Frontend["Astro.js SPA"]
            KB["Kanban Board<br/>(Epic 4)"]
            SVUI["Saved Views UI<br/>(NEW)<br/>Tabs / Dropdown"]
            FSC["Filter/Sort<br/>Serialization<br/>(NEW)"]
        end

        subgraph API["API Layer"]
            BFF["BFF / API Gateway"]
        end

        subgraph Backend[".NET Proto.Actor Backend"]
            UPA["UserPreference Actor<br/>(NEW)"]
        end

        subgraph EventStore["KurrentDB"]
            UPS["user-prefs-{userId}-product-{productId}<br/>(NEW)"]
        end

        subgraph ReadModel["PostgreSQL"]
            SVP["saved_view_projection<br/>(NEW)"]
        end

        subgraph Projectors["Projector Services"]
            SVPR["Saved View Projector<br/>(NEW)"]
        end
    end

    User --> KB
    KB --> SVUI
    SVUI --> FSC
    SVUI --> BFF
    BFF --> UPA
    UPA --> UPS
    UPS --> SVPR
    SVPR --> SVP
```

## 2. Component Diagram

```mermaid
graph TB
    subgraph "Saved Views System"
        subgraph "Frontend Components"
            ViewBar["View Bar<br/>(Tabs or Dropdown)"]
            SaveButton["Save View Button"]
            RenameControl["Rename Control<br/>(Inline / Context Menu)"]
            DeleteControl["Delete Control<br/>(Context Menu)"]
            ModifiedBadge["Modified Badge"]
        end

        subgraph "State Management"
            ActiveView["Active View State"]
            FilterSerializer["Filter/Sort Serializer"]
            ViewComparator["View Comparator<br/>(detect modifications)"]
        end

        subgraph "API"
            CRUD["Saved View CRUD Endpoints"]
        end

        subgraph "Persistence"
            Actor["UserPreference Actor"]
            Stream["user-prefs stream"]
            Projector["Saved View Projector"]
            Table["saved_view_projection"]
        end
    end

    ViewBar --> ActiveView
    SaveButton --> FilterSerializer
    RenameControl --> CRUD
    DeleteControl --> CRUD
    ActiveView --> ViewComparator
    ViewComparator --> ModifiedBadge
    FilterSerializer --> CRUD
    CRUD --> Actor
    Actor --> Stream
    Stream --> Projector
    Projector --> Table
```

## 3. Storage Design Decision

The epic spec raises a question: should saved views be event-sourced or stored directly in PostgreSQL?

### 3.1 Decision: Event-Sourced with Lightweight Projection

Saved views ARE event-sourced for consistency with the overall architecture, but with pragmatic concessions:

**Rationale:**
- The project is event-sourced throughout. Mixing in a CRUD table for one entity type creates architectural inconsistency.
- Event sourcing enables audit trails for preference changes (useful in Phase 2 for shared views).
- The projection is trivial (no complex joins or aggregations).

**Concession:** The UserPreference Actor is lightweight. It does not need the full rehydration-from-KurrentDB pattern because:
- Preference streams are tiny (a few events per product per user).
- The actor is almost always passivated between uses.
- The projection is the primary read path; the actor is only for writes.

### 3.2 Stream Naming

`user-prefs-{userId}-product-{productId}`

Each user-product combination gets its own stream. This keeps streams small and avoids cross-contamination between products.

### 3.3 Alternative Considered: Direct PostgreSQL Table

A simpler approach would be a single PostgreSQL table with no event sourcing:

```sql
-- Not chosen for MVP, but viable for Phase 2 simplification
CREATE TABLE saved_views (
    view_id UUID PRIMARY KEY,
    user_id UUID NOT NULL,
    product_id UUID NOT NULL,
    name TEXT NOT NULL,
    filters JSONB NOT NULL,
    sort JSONB NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);
```

This approach is noted as a fallback if the event-sourced approach proves too heavyweight for user preferences.

## 4. Actor Model: UserPreference Actor

### 4.1 Actor Design

**Stream:** `user-prefs-{userId}-product-{productId}`
**Responsibility:** Manages saved view CRUD for a specific user-product combination.

```
Commands:
  SaveView(name, filters, sort) -> viewId
  RenameView(viewId, newName)
  DeleteView(viewId)

Events:
  ViewSaved(viewId, userId, productId, name, filters, sort, actorId, timestamp)
  ViewRenamed(viewId, newName, actorId, timestamp)
  ViewDeleted(viewId, actorId, timestamp)

State:
  views: Map<viewId, SavedView>

SavedView:
  viewId: UUID
  name: string
  filters: FilterConfiguration
  sort: SortConfiguration
```

### 4.2 Validation Rules

- **SaveView:**
  - `name` must be non-empty and unique among the user's views for this product.
  - `filters` and `sort` must be valid serializable configurations.
  - Maximum 20 saved views per user per product (prevent clutter).

- **RenameView:**
  - `viewId` must exist in the current state.
  - `newName` must be non-empty and unique among the user's views for this product.

- **DeleteView:**
  - `viewId` must exist in the current state.

### 4.3 Actor Lifecycle

The UserPreference Actor follows the same lifecycle pattern as other actors:
- Spawned on first command for the user-product pair.
- Rehydrated from the KurrentDB stream.
- Passivated after 5 minutes of inactivity.

Given the small stream size (typically < 20 events), rehydration is near-instant.

## 5. Event Stream Design

### 5.1 Events

**ViewSaved:**
```json
{
  "eventType": "ViewSaved",
  "payload": {
    "viewId": "v1a2b3c4-...",
    "userId": "user-...",
    "productId": "product-...",
    "name": "Frontend Tasks",
    "filters": {
      "tags": { "include": ["area:frontend"], "exclude": [] },
      "textSearch": null
    },
    "sort": {
      "field": "updated_at",
      "direction": "desc"
    },
    "actorId": "user-...",
    "timestamp": "2026-05-05T14:30:00.000Z"
  }
}
```

**ViewRenamed:**
```json
{
  "eventType": "ViewRenamed",
  "payload": {
    "viewId": "v1a2b3c4-...",
    "newName": "Frontend Focus",
    "actorId": "user-...",
    "timestamp": "2026-05-05T15:00:00.000Z"
  }
}
```

**ViewDeleted:**
```json
{
  "eventType": "ViewDeleted",
  "payload": {
    "viewId": "v1a2b3c4-...",
    "actorId": "user-...",
    "timestamp": "2026-05-05T15:30:00.000Z"
  }
}
```

### 5.2 Redpanda Topic

| Topic | Key | Partitions | Purpose |
|-------|-----|------------|---------|
| `vut.user-prefs-events` | `{userId}:{productId}` | 3 | Saved view events |

## 6. Read Model Projection

### 6.1 Saved View Projection

```sql
-- Saved views per user per product
CREATE TABLE saved_view_projection (
    view_id     UUID PRIMARY KEY,
    user_id     UUID NOT NULL REFERENCES user_projection(user_id),
    product_id  UUID NOT NULL REFERENCES product_projection(product_id),
    name        TEXT NOT NULL,
    filters     JSONB NOT NULL,
    sort        JSONB NOT NULL,
    is_deleted  BOOLEAN NOT NULL DEFAULT FALSE,
    created_at  TIMESTAMPTZ NOT NULL,
    updated_at  TIMESTAMPTZ NOT NULL
);

-- Indexes
CREATE INDEX idx_svp_user_product ON saved_view_projection(user_id, product_id) WHERE is_deleted = FALSE;
CREATE UNIQUE INDEX idx_svp_name_unique ON saved_view_projection(user_id, product_id, name) WHERE is_deleted = FALSE;
```

### 6.2 Projector Event Handling

| Event | Action |
|-------|--------|
| `ViewSaved` | INSERT into `saved_view_projection` |
| `ViewRenamed` | UPDATE `name`, `updated_at` |
| `ViewDeleted` | UPDATE `is_deleted = TRUE`, `updated_at` |

## 7. Filter and Sort Serialization

### 7.1 Filter Configuration Schema

The filter configuration is a JSON object that captures the current board filter state:

```typescript
// Filter configuration (serializable)
interface FilterConfiguration {
  tags: {
    include: string[];   // Tags that must be present
    exclude: string[];   // Tags that must be absent
  };
  textSearch: string | null;  // Free-text search query
}

// Sort configuration (serializable)
interface SortConfiguration {
  field: "updated_at" | "created_at" | "title" | "status";
  direction: "asc" | "desc";
}

// Complete saved view
interface SavedView {
  viewId: string;
  name: string;
  filters: FilterConfiguration;
  sort: SortConfiguration;
}
```

### 7.2 Serialization and Deserialization

When saving a view, the frontend serializes the current filter/sort state into the JSON schema above. When loading a saved view, the frontend deserializes and applies it.

```mermaid
flowchart LR
    subgraph "Save Flow"
        FS["Current Filter/Sort<br/>State (UI)"] --> SER["Serialize to JSON"]
        SER --> API["POST /api/saved-views"]
        API --> PERSIST["Persist via Actor"]
    end

    subgraph "Load Flow"
        PERSIST2["Read from Projection"] --> API2["GET /api/saved-views"]
        API2 --> DESER["Deserialize from JSON"]
        DESER --> APPLY["Apply to Board<br/>(Update Filter/Sort State)"]
    end
```

### 7.3 View Modification Detection

The frontend detects when the current filter/sort state differs from the active saved view:

```mermaid
flowchart TD
    Current["Current Filter/Sort State"] --> Compare["Deep Compare"]
    Saved["Active Saved View<br/>Filter/Sort"] --> Compare
    Compare -->|Match| NoBadge["No modification indicator"]
    Compare -->|Different| Badge["Show 'Modified' badge<br/>+ 'Update view' option"]
```

Deep comparison is straightforward because the filter/sort state is a plain JSON object. Structural equality check determines if the view has been modified.

## 8. Key Workflow Sequence Diagrams

### 8.1 Save a View

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant AS as Actor Service
    participant K as KurrentDB
    participant RP as Redpanda
    participant PJ as Saved View Projector
    participant PG as PostgreSQL

    User->>Browser: Apply filters (tag: area:frontend), sort by updated_at
    Browser->>Browser: Board updates to show filtered tasks

    User->>Browser: Click "Save this view"
    Browser->>Browser: Prompt for view name
    User->>Browser: Enter "Frontend Tasks", submit

    Browser->>BFF: POST /api/products/{productId}/saved-views
    Note over BFF: Body: { name: "Frontend Tasks", filters: {...}, sort: {...} }

    BFF->>BFF: Validate session, extract userId
    BFF->>AS: SaveViewCommand(userId, productId, name, filters, sort)
    AS->>AS: Validate: name unique, < 20 views
    AS->>K: Append ViewSaved to user-prefs-{userId}-product-{productId}
    K-->>AS: OK
    AS-->>BFF: { viewId }

    K->>RP: Publish ViewSaved
    RP->>PJ: Consume
    PJ->>PG: INSERT saved_view_projection

    BFF->>Browser: 201 Created { viewId, name }
    Browser->>Browser: Add "Frontend Tasks" tab, highlight as active
    Browser->>User: View saved and active
```

### 8.2 Switch Between Saved Views

```mermaid
sequenceDiagram
    actor User
    participant Browser

    Note over Browser: All saved views are already loaded<br/>(fetched on board mount)

    User->>Browser: Click "Bugs" tab
    Browser->>Browser: Look up saved view "Bugs" in local state
    Browser->>Browser: Deserialize filters: { tags: { include: ["type:bug"] } }
    Browser->>Browser: Apply filters to board data (client-side)
    Browser->>Browser: Highlight "Bugs" tab as active
    Browser->>Browser: Clear "Modified" badge
    Browser->>User: Board shows only bug-tagged tasks

    Note over Browser: No API call needed --<br/>filtering is client-side
```

### 8.3 Rename a View

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant AS as Actor Service
    participant K as KurrentDB

    User->>Browser: Context menu on "Bugs" tab -> Rename
    Browser->>Browser: Switch tab name to inline edit mode
    User->>Browser: Type "Bug Tracker", press Enter

    Browser->>BFF: PATCH /api/saved-views/{viewId}
    Note over BFF: Body: { name: "Bug Tracker" }

    BFF->>AS: RenameViewCommand(viewId, "Bug Tracker")
    AS->>AS: Validate: new name unique
    AS->>K: Append ViewRenamed
    K-->>AS: OK
    AS-->>BFF: OK

    BFF->>Browser: 200 OK
    Browser->>Browser: Update tab label to "Bug Tracker"
    Browser->>User: Renamed
```

### 8.4 Delete a View

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant AS as Actor Service
    participant K as KurrentDB

    User->>Browser: Context menu on "Frontend Tasks" tab -> Delete
    Browser->>Browser: Confirm dialog: "Delete 'Frontend Tasks'?"
    User->>Browser: Confirm

    Browser->>BFF: DELETE /api/saved-views/{viewId}
    BFF->>AS: DeleteViewCommand(viewId)
    AS->>K: Append ViewDeleted
    K-->>AS: OK
    AS-->>BFF: OK

    BFF->>Browser: 200 OK
    Browser->>Browser: Remove tab from view list
    Browser->>Browser: If deleted view was active, reset to default (no filter)
    Browser->>User: View removed, board shows default state
```

### 8.5 Modified View Detection

```mermaid
sequenceDiagram
    actor User
    participant Browser

    Note over Browser: Active view: "Frontend Tasks"<br/>Filter: tag=area:frontend

    User->>Browser: Add filter: text search "login"
    Browser->>Browser: Detect: current filters != saved view filters
    Browser->>Browser: Show "Modified" badge on "Frontend Tasks" tab
    Browser->>Browser: Show "Update view" option in context menu

    User->>Browser: Click "Update view"
    Browser->>Browser: Overwrite saved view's filters with current filters
    Note over Browser: API call to update (same as rename, but with new filters)
    Browser->>Browser: Remove "Modified" badge
```

## 9. API Design

### 9.1 Saved View Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/products/{productId}/saved-views` | List user's saved views for a product |
| POST | `/api/products/{productId}/saved-views` | Create a saved view |
| GET | `/api/saved-views/{viewId}` | Get saved view details |
| PATCH | `/api/saved-views/{viewId}` | Rename a saved view |
| PUT | `/api/saved-views/{viewId}` | Update filters/sort of a saved view |
| DELETE | `/api/saved-views/{viewId}` | Delete a saved view |

### 9.2 Create Request

```json
POST /api/products/{productId}/saved-views
{
  "name": "Frontend Tasks",
  "filters": {
    "tags": {
      "include": ["area:frontend"],
      "exclude": []
    },
    "textSearch": null
  },
  "sort": {
    "field": "updated_at",
    "direction": "desc"
  }
}
```

### 9.3 List Response

```json
GET /api/products/{productId}/saved-views

{
  "views": [
    {
      "viewId": "v1a2b3c4-...",
      "name": "Frontend Tasks",
      "filters": {
        "tags": { "include": ["area:frontend"], "exclude": [] },
        "textSearch": null
      },
      "sort": { "field": "updated_at", "direction": "desc" },
      "createdAt": "2026-05-01T10:00:00Z",
      "updatedAt": "2026-05-01T10:00:00Z"
    },
    {
      "viewId": "d5e6f7a8-...",
      "name": "Bugs",
      "filters": {
        "tags": { "include": ["type:bug"], "exclude": [] },
        "textSearch": null
      },
      "sort": { "field": "updated_at", "direction": "asc" },
      "createdAt": "2026-05-03T14:00:00Z",
      "updatedAt": "2026-05-03T14:00:00Z"
    }
  ]
}
```

## 10. Frontend Architecture

### 10.1 View Bar Component

```mermaid
graph TD
    subgraph "View Bar (above board)"
        DefaultTab["Default Tab<br/>(All tasks, no filter)"]
        ViewTabs["Saved View Tabs<br/>(Frontend Tasks, Bugs, ...)"]
        SaveBtn["Save View Button<br/>(visible when filters active)"]
        Overflow["Overflow Menu<br/>(when tabs exceed width)"]
    end

    DefaultTab --> ClickDefault["Click -> clear filters"]
    ViewTabs --> ClickTab["Click -> apply view"]
    ViewTabs --> RightClick["Right-click -> context menu<br/>(Rename, Delete, Update)"]
    SaveBtn --> Modal["Name input modal"]
    Overflow --> Dropdown["Dropdown list of views"]
```

### 10.2 Client-Side State Extension

The Kanban Board state from Epic 4 is extended:

```typescript
// Extended board state
interface BoardState {
  columns: Map<string, Task[]>;
  pendingChanges: Map<string, PendingChange>;
  filters: BoardFilters;
  sort: SortConfig;
  savedViews: SavedView[];        // Loaded on mount
  activeViewId: string | null;    // Currently active view
  isModified: boolean;            // Current state differs from active view
}
```

### 10.3 View Lifecycle on the Board

```mermaid
stateDiagram-v2
    [*] --> DefaultView: Board loaded
    DefaultView --> Filtering: Apply filters manually
    Filtering --> ModifiedView: Active view + manual changes
    Filtering --> UnnamedFiltered: No active view

    ModifiedView --> SaveNew: "Save as new view"
    ModifiedView --> UpdateExisting: "Update view"
    ModifiedView --> Filtering: More changes
    ModifiedView --> DefaultView: Clear filters

    UnnamedFiltered --> SaveNew: "Save this view"
    UnnamedFiltered --> DefaultView: Clear filters

    SaveNew --> ViewActive: View saved and active
    UpdateExisting --> ViewActive: View updated

    ViewActive --> Filtering: Apply different filters (modified)
    ViewActive --> DefaultView: Clear filters
    ViewActive --> ViewActive: Switch to another saved view

    note right of ModifiedView
        "Modified" badge shown.
        "Update view" option available.
    end note
```

## 11. State Diagram: Saved View CRUD

```mermaid
stateDiagram-v2
    [*] --> NoViews: Board first opened
    NoViews --> OneView: Save first view
    OneView --> MultipleViews: Save additional views
    MultipleViews --> MultipleViews: Save / Rename / Delete

    OneView --> NoViews: Delete last view
    MultipleViews --> OneView: Delete to one remaining

    note right of MultipleViews
        Max 20 views per user per product.
        Names must be unique.
    end note
```

## 12. Data Flow

```mermaid
flowchart LR
    subgraph "Write Path"
        CMD["Save/Rename/<br/>Delete View"] --> ACT["UserPreference<br/>Actor"]
        ACT --> ES["KurrentDB<br/>user-prefs stream"]
        ES --> PUB["Redpanda<br/>vut.user-prefs-events"]
    end

    subgraph "Project Path"
        PUB --> CONS["Saved View<br/>Projector"]
        CONS --> PG["saved_view_projection<br/>(PostgreSQL)"]
    end

    subgraph "Read Path"
        API["Read Model API"] --> PG
        UI["View Bar<br/>(Browser)"] --> API
    end

    subgraph "Apply Path"
        UI2["Tab Click"] --> LOCAL["Apply filters<br/>locally (no API call)"]
    end
```

## 13. Performance Considerations

### 13.1 View Switching Speed

- Saved views are fetched on board mount and cached in client-side state.
- Switching views is a client-only operation: deserialize the view's filter/sort config and apply it to the in-memory task list.
- No API calls during view switching. This satisfies the "instantaneous" requirement.
- Target: < 50ms for view switch (DOM update only).

### 13.2 View List Loading

- The saved view list for a user-product pair is typically < 20 items.
- Fetched as part of the board data endpoint or as a single dedicated call.
- Stored in client state for the board's lifetime.

### 13.3 Projection Size

- `saved_view_projection` is tiny. Even at scale (10,000 users x 5 products x 10 views), it's 500,000 rows -- negligible for PostgreSQL.
- The `WHERE is_deleted = FALSE` partial index keeps queries fast.

## 14. Future Considerations

| Concern | Phase 2 Handling |
|---------|------------------|
| Shared views (team-level) | Add `scope` field to ViewSaved event: "personal" vs "team". Team views are visible to all org members. |
| Backlog saved views | Extend the same mechanism to the backlog view. The filter/sort schema is shared. |
| Report view saving | The report already has its own tag filter. Saved report configurations can reuse the same persistence pattern. |
| View ordering | Add a `ViewReordered` event with explicit ordering, replacing the current alphabetical/creation-date sort. |
