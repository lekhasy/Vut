# Epic 1: Core Platform — Kanban MVP

## Overview

Ship a working Velucid instance where teams can create organizations, add products, manage tasks on a kanban board, and use the probabilistic forecast. No email verification, no saved views, no tags — just the core flow.

**Target:** A team can sign in via GitHub, create an org, create a product with statuses, add tasks, and move them across a kanban board. The forecast tab is visible but shows the "gathering data" state until enough history accumulates.

**Out of Scope:** Forecast Monte Carlo engine, saved views, email verification, invite flow, role management beyond owner/member.

---

## Story 1.0: React Foundation 🔥 NEW

**What's needed:**
- `npx astro add react` + TypeScript config updates
- Install: zustand, react-hook-form, @hookform/resolvers, zod
- Install shadcn/ui + Radix primitives: dialog, dropdown-menu, select, tabs, form, toast
- Migrate `MainLayout.astro` → React component with Tailwind
- Wrap nanostores in React-compatible `useStore` hooks
- Org selector sidebar component (replaces getElementById-based vanilla JS)
- Nav header with org/product tabs as React components
- Shared `useAuth`, `useOrg`, `useProduct` hooks over nanostores

**Acceptance criteria:**
- React renders correctly inside Astro pages via `client:load` directives
- shadcn/ui components work with Tailwind and are styled consistently
- Zustand store is accessible from React components
- All interactive UI (org selector, nav, modals) uses React, not getElementById
- No TypeScript errors on fresh `astro check`

**Blocks:** All remaining stories (1-2 through 1-5)

---

## Story 1.1: Organizations

**What's needed:**
- `OrgGrain` with `[GrainType("org")]` — create, rename, delete org; owner/member membership
- `OrgProjection`, `OrgMemberProjection`, `OrgInvitationProjection` entities + EF Core migrations
- `OrgController` on Silo — `GET /api/orgs`, `POST /api/orgs`, `GET/PUT/DELETE /api/orgs/{orgId}`
- Astro API routes in `src/pages/api/orgs/`
- Projector handler for org projections
- Frontend: org selector in sidebar, org creation modal, org settings page

**Acceptance criteria:**
- User can create an organization (becomes owner)
- User can view their org list in sidebar
- User can navigate to org dashboard
- User can invite a member by email (invitation stored, no email sent yet)
- Owner can remove members

---

## Story 1.2: Products

**What's needed:**
- `ProductGrain` with `[GrainType("product")]` — create, rename, delete product; configure statuses
- `ProductProjection` entity + migration
- `ProductController` on Silo — `GET/POST /api/orgs/{orgId}/products`, `GET/PUT/DELETE /api/orgs/{orgId}/products/{productId}`
- Astro API routes
- Projector handler
- Frontend: product list, create product modal with status definition, product settings

**Acceptance criteria:**
- When creating a product, owner/member defines the initial status list (minimum 2)
- First status in the list is automatically the backlog-only status (excluded from kanban)
- Backlog-only status can be renamed but not removed or reordered
- User can navigate to a product's backlog view

---

## Story 1.3: Tasks + Kanban Board

**What's needed:**
- `TaskGrain` with `[GrainType("task")]` — create, update, delete task; change status; add/remove tags
- `TaskProjection` entity + migration (includes `DeletedAt` for soft-delete)
- `TaskController` on Silo — CRUD endpoints + status change
- Astro API routes for tasks: `GET/POST /api/tasks`, `GET/PUT/DELETE /api/tasks/{taskId}`, `PATCH /api/tasks/{taskId}/status`
- Projector handler
- Frontend backlog view: all tasks filterable by status/tags/text, sortable
- Frontend kanban board: non-backlog statuses as columns, drag-and-drop to change status, card shows title + tags

**Acceptance criteria:**
- Tasks can be created in any product — default to backlog-only status
- Kanban board shows only non-backlog statuses as columns
- Drag-and-drop between columns triggers status change event and optimistic UI update
- Soft-deleted tasks are excluded from all views and event stream projections
- Tags follow `namespace:value` format (no validation — free-form)
- Backlog loads in <1s for 10,000 tasks (index on `product_id` + `status`)

---

## Story 1.4: Basic Navigation + Dashboard

**What's needed:**
- Astro layout with org/product sidebar navigation
- Product landing page: backlog view (default)
- Header navigation: Backlog | Kanban | Forecast tabs
- Org dashboard: product list with create button
- User menu: display name, avatar, sign out

**Acceptance criteria:**
- User lands on their org dashboard after sign-in
- Can switch between orgs via org selector
- Can navigate between Backlog, Kanban, and Forecast tabs within a product
- All navigation is client-side (no page reloads)

---

## Story 1.5: Forecast Tab (Gathering Data State)

**What's needed:**
- Forecast tab visible in product header navigation
- Shows "Gathering data" empty state with explanation text
- No Monte Carlo computation yet — just the shell UI and explanatory copy
- Connecting to forecast spec: minimum 7 days of data required before showing forecasts

**Acceptance criteria:**
- Forecast tab is accessible and readable
- Clear message: "We need at least 7 days of task history before we can generate a forecast"
- No error states — just a friendly empty state

---

## Retrospective 1: Epic 1 Review

**Purpose:** Capture what's working, what's not, and what to do differently in Epic 2 (Tags, Saved Views, Forecast).

**Topics to cover:**
- Grain structure — did the Orleans pattern feel right?
- BFF routing — any friction with Astro API routes?
- Projection lag — did it cause any real UX friction or was optimistic UI enough?
- What's next: tags + saved views + forecast engine
