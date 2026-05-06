---
name: Vut Organization Model
description: Multi-tenancy model -- GitHub-style orgs, roles, and membership
type: project
---

## GitHub-Style Org Model
- One user can belong to multiple organizations.
- Each org has owners and members.
- Owners: full control -- manage members, manage products, configure org settings, delete org.
- Members: access all products in the org, create tasks, manage tags/statuses within products.
- Invitation by email or GitHub username; invitee signs in with GitHub to accept.
- Products belong to exactly one org.
- Tasks belong to exactly one product.

**Hierarchy:** Organization > Product > Task (fixed, three levels).

**Why:** User explicitly chose GitHub's org model as the reference pattern. This shapes auth, authorization, and data isolation.
**How to apply:** Design org/product/task relationships following GitHub's org/repo pattern. Role-based access is at the org level, inherited by products. Do not propose finer-grained permissions (e.g., per-product roles) unless explicitly requested.
