# Implement UserGrain.SignIn orchestration logic

**Developer Type:** Backend
**Priority:** Critical
**Phase:** Backend API

## Description
Implement the `SignIn` method on `UserGrain` that handles the full user resolution logic: lookup by providerId → if not found, lookup by email → if email matches, link identity → else create new user. Returns `{ userId, isEmailVerified }` idempotently.

## Architecture Reference
Section "Sign-In Flow" from the desired architecture:
> lookup by providerId → if not found, lookup by email → if email match, link identity → else create new user

The current `callback.astro` does exactly this via 3 separate API calls. This task consolidates that logic into one grain method.

## Technical Requirements
- Add a new `SignIn` method to `UserGrain.cs` that:
  1. Calls `GetUserByProviderIdAsync(providerId)` on a well-known registry grain to check if the providerId is already linked to any user
  2. If found: return `new SignInResult(userId, isEmailVerified)` — no events emitted
  3. If not found: calls `GetUserIdByEmailAsync(email)` on the registry grain to check for an existing user by email
  4. If email match found:
     - Activate the user grain by userId
     - Call `LinkIdentity(providerId, providerName, email)` on that grain
     - Return `new SignInResult(userId, isEmailVerified)`
  5. If no email match: activate a fresh user grain (use the registry grain's method to allocate a new userId) and call `CreateUser(...)` on it, then return `new SignInResult(newUserId, false)`
- The registry grain (`IUserRegistryGrain`) needs to be created as part of this task:
  - Grain interface: `IUserRegistryGrain : IGrainWithStringKey` (keyed by providerId or email lookup)
  - Actually, use a dedicated `IUserLookupGrain` that supports `GetUserIdByProviderId(providerId)` and `GetUserIdByEmail(email)` — both return `Guid?`
  - The lookup grain needs to be updated whenever a user is created or an identity is linked, so it must maintain two indexes: providerId→userId and email→userId

**Key design decisions:**
- `IUserLookupGrain` (string key) uses `GetGrain<IUserLookupGrain>("users")` as a singleton registry
- On `UserGrain.CreateUser` or `UserGrain.LinkIdentity`, after emitting the event, the grain should call `userLookup.RegisterProvider(userId, providerId, email)` to update the index
- On email match: call `userLookup.FindByEmail(email)` to get existing userId, then activate `GetGrain<IUserGrain>(existingUserId)` and call `LinkIdentity`
- The `SignIn` method on `UserGrain` should NOT be called on an already-activated user grain — it is the entry point that decides which grain to activate

**Implementation detail for `UserGrain.SignIn`:**
Since grains are activated by ID, and we don't know the userId until after the lookup, the `SignIn` logic is best implemented as a **static helper method on the grain class** or a separate **SignInGrain** that orchestrates lookups then activates the target grain. But given Orleans patterns, the cleanest approach is:
1. `IUserLookupGrain` (singleton `"user-lookup"`) — maintains indexes
2. `SignIn` command: first call `userLookup.GetUserIdByProviderId(providerId)` — if non-null, return existing user result
3. Then call `userLookup.GetUserIdByEmail(email)` — if non-null, activate that user grain and call `LinkIdentity`
4. Otherwise, allocate new `Guid userId = Guid.NewGuid()`, activate `GetGrain<IUserGrain>(userId)`, call `CreateUser(...)`, then register all indexes in `userLookup`
5. Return `SignInResult`

The `UserGrain.CreateUser` and `UserGrain.LinkIdentity` methods must also update the lookup index by calling `userLookup.Register(...)` after emitting events.

## Acceptance Criteria
- [ ] `IUserLookupGrain` interface added with `Task<Guid?> GetUserIdByProviderId(string providerId)` and `Task<Guid?> GetUserIdByEmail(string email)`
- [ ] `UserLookupGrain` implementation registers providerId→userId and email→userId indexes
- [ ] `UserGrain.SignIn` method implements the full lookup/link-or-create flow
- [ ] `UserGrain.CreateUser` calls `userLookup.RegisterNewUser(...)` to index the new user
- [ ] `UserGrain.LinkIdentity` calls `userLookup.RegisterProvider(...)` to index the new identity
- [ ] All paths return `SignInResult` with correct `userId` and `isEmailVerified`
- [ ] Idempotent: calling `SignIn` twice with the same providerId returns the same result without new events

## Dependencies
- Task 01 (SignIn interface must be merged first)

## Estimated Effort
M (4-8h)

## Notes
- The `IUserLookupGrain` singleton approach means it needs to be durable — consider storing the indexes in KurrentDB or in-memory with Orleans reminders for persistence. For now, in-memory with the understanding that the silo is the source of truth (users are never lost because the User grain events are the source) is acceptable for this iteration.
- The `userLookup.Register*` calls should be fire-and-forget on success (they are for indexing only; the User grain is the source of truth). Failures should be logged but not propagate.