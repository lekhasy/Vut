# Epic 2: Product Setup with Configurable Workflow

## Vertical Slice Statement

An organization member creates a product, names it, writes a description, and defines the set of statuses that will govern the team's workflow. After this Epic, the organization has at least one product ready to receive tasks, and the team's workflow stages are configured.

## Target Personas

- Team Lead / Engineering Manager (primary -- defines workflow)
- Developer / Team Member (secondary -- views products)

## User Stories

1. As an organization member, I want to create a product within my organization so that my team has a named container for our work.
2. As the product creator, I want to define the initial set of statuses (e.g., "New", "In Progress", "In Review", "Done") so that the workflow matches how my team actually works.
3. As a team member, I want to see a list of all products in my organization so that I can navigate to the right workspace.
4. As an organization member, I want to add, rename, or remove statuses on an existing product so that the workflow can evolve as the team's process changes.
5. As an organization member, I want to rename a product or update its description so that the workspace stays accurate.

## Acceptance Criteria

- [ ] Any org member (owner or member role) can create a product within their organization.
- [ ] Creating a product requires a name (unique within the org), an optional description, and at least two statuses.
- [ ] One of the initial statuses must be the designated starting status (convention: "New"). The UI makes this clear during creation.
- [ ] `ProductCreated` event is emitted with the full initial status configuration.
- [ ] The product appears in the organization's product list in the sidebar navigation.
- [ ] Navigating to a product shows the product landing page (backlog view placeholder; full backlog is Epic 3).
- [ ] A member can add a new status to a product (emitting `StatusAdded`), rename an existing status (emitting `StatusRenamed`), or remove a status (emitting `StatusRemoved`).
- [ ] A member can rename the product (`ProductRenamed`) or change its description (`ProductDescriptionChanged`).
- [ ] A product cannot be created outside an organization.
- [ ] Tasks cannot yet be created (that is Epic 3) -- the product shows an empty state with a prompt.

## Event Streams Introduced

| Stream | Events |
|--------|--------|
| Product | `ProductCreated`, `ProductRenamed`, `ProductDescriptionChanged`, `StatusAdded`, `StatusRenamed`, `StatusRemoved`, `ProductDeleted` |

## Projection Views Introduced

| View | Purpose |
|------|---------|
| Product Projection | Product name, description, configured statuses (ordered list) |

## Technical Scope

- **Proto.Actor Product actor**: handles create, rename, description change, and status mutations. Validates that status names are unique within the product and that at least two statuses remain.
- **KurrentDB stream**: `product-{productId}`.
- **Redpanda topic**: product events.
- **PostgreSQL projection**: product view with status list.
- **Astro.js UI**: product creation form (name, description, status builder), product settings page for status management, product list in sidebar.
- **Sidebar update**: products appear under the currently selected organization.

## Out of Scope for This Epic

- Task creation and management (Epic 3).
- Kanban board visualization (Epic 4).
- Cumulative flow diagram (Epic 5).
- Product deletion semantics (deferred; event is defined but UI action not required).

## Estimated Complexity

**Medium** -- The infrastructure is already in place from Epic 1. This Epic adds one new aggregate root (Product), its actor, stream, projection, and CRUD UI.

## How to Demo

1. Navigate to the "Acme Corp" organization from Epic 1.
2. Click "Create Product."
3. Fill in name: "Vut Mobile App", description: "Cross-platform mobile client."
4. Define statuses: "New", "In Progress", "In Review", "Done". Submit.
5. The product appears in the sidebar. Click it to see the empty backlog state.
6. Go to product settings. Add a status "Blocked". Rename "In Review" to "Review". Remove "Blocked".
7. Verify the status list reflects the changes.
