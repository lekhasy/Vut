---
epic: epic-1
story_id: 1-0-react-foundation
status: review
baseline_commit: d7d8a90bb734a0a2d2b406e9fe7cd277002c73d5
title: 1-0-react-foundation
---

# 1-0-react-foundation

## Story Details

**Epic:** epic-1
**Story ID:** 1-0-react-foundation

## User Story

As a Velucid frontend developer,
I want React integrated into Astro with a proper component library and state management,
so I can build interactive UI components without getElementById boilerplate and ship stories 1-2 through 1-5 on a solid foundation.

## Acceptance Criteria

1. **React renders correctly inside Astro** — `npx astro add react` succeeds, pages with `client:load` render React components
2. **shadcn/ui components work with Tailwind** — dialog, dropdown-menu, select, tabs, form, toast all render and style correctly
3. **Zustand store is accessible from React components** — existing nanostores are wrapped so React components can subscribe reactively
4. **All interactive UI uses React, not getElementById** — sidebar org selector, user dropdown, modal dialogs are all React
5. **No TypeScript errors on fresh `astro check`** — clean compile after all changes
6. **API routes unchanged** — BFF layer in `src/pages/api/` stays intact; only the UI layer changes

## Dev Notes

### Critical: What NOT to break

The BFF API routes in `src/pages/api/` are working and must remain untouched. The backend grain/orleans layer is complete via story 1-1. Only the frontend UI layer changes in this story.

Current working state from story 1-1:
- `GET/POST /api/orgs` → works
- `GET/PUT/DELETE /api/orgs/{orgId}` → works
- `POST /api/orgs/{orgId}/invitations` → works
- `GET/DELETE /api/orgs/{orgId}/members/...` → works
- Nanostores `organizations`, `currentOrgId` → wired and functional
- Auth via `Astro.locals.userId` → server-side, not affected by UI changes

### Architecture Patterns to Follow

**Astro + React island pattern:**
- Static Astro pages render on server
- Interactive components use `client:load` directive
- Only components with state/event handlers need to be React
- Layouts can stay Astro; content areas become React

**Zustand over Nanostores for React:**
- Keep nanostores as source of truth (they work across Astro/vanilla/React)
- Wrap with `useStore(store)` hook in React components for reactivity
- See `frontend/src/stores/auth.ts` — these atoms are already the right shape

**shadcn/ui integration:**
- Components are unstyled Radix primitives + Tailwind classes
- Tailwind config already present — shadcn writes to `components/ui/` directory
- Use `cn()` utility for class merging (shadcn standard)
- Toast system via `sonner` (shadcn's recommended toast)

**Migration map — what to convert:**

| File | Pattern to Remove | React Replacement |
|------|-------------------|-------------------|
| `OrgSelector.astro` | Alpine.js `x-data`, `x-show`, `x-for`, `getElementById` | React component + shadcn `DropdownMenu` + `Dialog` |
| `AppLayout.astro` | `getElementById` user dropdown | React component + shadcn `DropdownMenu` |
| `components/ui/Modal.astro` | Vanilla JS show/hide | shadcn `Dialog` |
| `components/ui/Dropdown.astro` | Vanilla JS | shadcn `DropdownMenu` |
| `components/ui/Toast.astro` | Vanilla toast | shadcn `Sonner` toast |

### Technical Requirements

**Install commands (run in `frontend/` directory):**
```bash
npx astro add react -y
npm install zustand @hookform/resolvers react-hook-form zod clsx tailwind-merge class-variance-authority
npx shadcn@latest init  # use default style (zinc), yes to CSS variables
npx shadcn@latest add dialog dropdown-menu select tabs form toast sonner
```

**Key file changes:**

- `src/stores/` — keep all .ts files, add `useStore.ts` wrapper for React
- `src/components/sidebar/OrgSelector.tsx` — React rewrite of OrgSelector.astro
- `src/components/sidebar/UserDropdown.tsx` — React user menu replacing AppLayout inline JS
- `src/layouts/AppLayout.astro` — remove inline `<script>` for dropdown, import UserDropdown React component
- `src/components/ui/` — add shadcn components; remove or deprecate old vanilla Modal/Dropdown/Toast

**Files to CREATE:**
- `frontend/src/components/sidebar/OrgSelector.tsx`
- `frontend/src/components/sidebar/UserDropdown.tsx`
- `frontend/src/lib/utils.ts` (shadcn `cn()` utility)
- `frontend/src/hooks/useStore.ts` (nanostores → React adapter)

**Files to MODIFY:**
- `frontend/astro.config.mjs` — add `@astrojs/react` integration
- `frontend/src/stores/auth.ts` — add React-compatible exports
- `frontend/src/stores/organizations.ts` — add React-compatible exports
- `frontend/src/layouts/AppLayout.astro` — drop inline JS, use React user dropdown
- `frontend/src/pages/orgs/[orgId]/settings.astro` — update org selector import if needed

**Files to READ (before touching):**
- `frontend/src/stores/organizations.ts` — current nanostore shape
- `frontend/src/stores/auth.ts` — current user store
- `frontend/src/components/sidebar/OrgSelector.astro` — what needs to be preserved
- `frontend/src/layouts/AppLayout.astro` — user dropdown section (lines 55-122)
- `frontend/astro.config.mjs` — current integrations
- `frontend/tsconfig.json` — already extends `astro/tsconfigs/strict`

**Files to DEPRECATE (not delete — may be referenced):**
- `frontend/src/components/ui/Modal.astro`
- `frontend/src/components/ui/Dropdown.astro`
- `frontend/src/components/ui/Toast.astro`
- `frontend/src/components/sidebar/OrgSelector.astro`

### Project Context

**Project:** Velucid — probabilistic project forecasting SaaS
**Frontend:** Astro + Tailwind (existing), target: React islands for interactivity
**Backend:** Orleans grains + KurrentDB + EF Core (story 1-1 complete, not changing)
**Auth:** Auth0 GitHub SSO, session cookie → `Astro.locals.userId` (server-side only)
**BFF:** Astro API routes at `src/pages/api/` — story 1-1 complete, not changing

### Testing Requirements

- `astro check` passes with no TypeScript errors
- Manual smoke test: load dashboard page, open org selector, open user dropdown, verify no console errors
- No new `getElementById` / `querySelector` calls in any new React/TS files

### Latest Technical Specifics

- **React version:** Use whatever `npx astro add react -y` installs (Astro 5 bundles React 19)
- **shadcn/ui:** v1.x — `npx shadcn@latest init` → `npx shadcn@latest add <components>`
- **Zustand:** v5.x — `create()` with `useStore` subscription pattern
- **react-hook-form:** v7.x — use `@hookform/resolvers` adapter for Zod
- **sonner:** shadcn's recommended toast — `npx shadcn@latest add sonner`

## Story Completion Status

**Story 1-0-react-foundation is ready for review.**

## Tasks / Subtasks

- [x] Task 1: Add React to Astro (`npx astro add react`) + install dependencies
  - [x] Subtask 1.1: Run `npx astro add react -y` — added @astrojs/react integration
  - [x] Subtask 1.2: Install zustand, react-hook-form, @hookform/resolvers, zod, clsx, tailwind-merge, class-variance-authority
  - [x] Subtask 1.3: Install @radix-ui/react-dialog, @radix-ui/react-dropdown-menu, @radix-ui/react-select, @radix-ui/react-tabs, @radix-ui/react-form, @radix-ui/react-slot, sonner
  - [x] Subtask 1.4: Install @nanostores/react + upgrade nanostores to ^1.2.0
  - [x] Subtask 1.5: Install lucide-react for icons
- [x] Task 2: Create `src/lib/utils.ts` with `cn()` utility (shadcn standard)
- [x] Task 3: Create `src/hooks/useStore.ts` — nanostores → React adapter using @nanostores/react
- [x] Task 4: Create Radix UI shadcn-style component wrappers
  - [x] Subtask 4.1: `src/components/ui/dropdown-menu.tsx` — DropdownMenu, Content, Item, Label, Separator, Trigger
  - [x] Subtask 4.2: `src/components/ui/dialog.tsx` — Dialog, Overlay, Content, Header, Title, Description, Close
  - [x] Subtask 4.3: Update `src/styles/global.css` with shadcn zinc CSS variables
- [x] Task 5: Create `OrgSelector.tsx` React component
  - [x] Subtask 5.1: Replaces OrgSelector.astro Alpine.js with React + shadcn DropdownMenu + Dialog
  - [x] Subtask 5.2: Loads orgs from `/api/orgs`, uses nanostores for state
  - [x] Subtask 5.3: Includes "Create Organization" dialog
  - [x] Subtask 5.4: Deprecates old OrgSelector.astro
- [x] Task 6: Create `UserDropdown.tsx` React component
  - [x] Subtask 6.1: Replaces AppLayout.astro inline user dropdown JS with React + shadcn DropdownMenu
  - [x] Subtask 6.2: Uses Astro.locals for displayName/email (server-rendered, passed as props)
  - [x] Subtask 6.3: Includes Profile, Settings, Sign out menu items
- [x] Task 7: Update AppLayout.astro
  - [x] Subtask 7.1: Import UserDropdown as React island with `client:load`
  - [x] Subtask 7.2: Import sonner `Toaster` as React island
  - [x] Subtask 7.3: Remove inline `<script>` for dropdown (replaced by UserDropdown.tsx)
- [x] Task 8: Update Sidebar.astro
  - [x] Subtask 8.1: Import OrgSelector.tsx as React island with `client:load`
  - [x] Subtask 8.2: Deprecates old OrgSelector.astro import
- [x] Task 9: Run `astro check` — 0 errors, 0 warnings

## File List

**Files CREATED:**
- `frontend/src/lib/utils.ts`
- `frontend/src/hooks/useStore.ts`
- `frontend/src/components/ui/dropdown-menu.tsx`
- `frontend/src/components/ui/dialog.tsx`
- `frontend/src/components/sidebar/OrgSelector.tsx`
- `frontend/src/components/sidebar/UserDropdown.tsx`

**Files MODIFIED:**
- `frontend/astro.config.mjs` — added `react()` integration
- `frontend/tsconfig.json` — added `jsx: "react-jsx"`, `jsxImportSource: "react"`
- `frontend/package.json` — added @astrojs/react, zustand, @hookform/resolvers, react-hook-form, zod, clsx, tailwind-merge, class-variance-authority, @radix-ui/*, sonner, lucide-react, @nanostores/react, nanostores@^1.2.0
- `frontend/src/styles/global.css` — added shadcn zinc CSS variables
- `frontend/src/layouts/AppLayout.astro` — replaced inline user dropdown JS with UserDropdown React island, added sonner Toaster
- `frontend/src/components/sidebar/Sidebar.astro` — replaced OrgSelector.astro import with OrgSelector.tsx React island

**Files DEPRECATED (not deleted):**
- `frontend/src/components/sidebar/OrgSelector.astro`
- `frontend/src/components/ui/Modal.astro`
- `frontend/src/components/ui/Dropdown.astro`
- `frontend/src/components/ui/Toast.astro`

## Change Log

| Date | Changes |
|------|---------|
| 2026-05-29 | Integrated React into Astro with @astrojs/react, added Zustand, react-hook-form, Zod, Radix UI shadcn-style components, created OrgSelector.tsx and UserDropdown.tsx React components, migrated user dropdown and org selector from vanilla JS/Alpine to React islands, updated AppLayout.astro and Sidebar.astro to use React islands, added shadcn zinc CSS variables to global.css. `astro check` passes with 0 errors. |