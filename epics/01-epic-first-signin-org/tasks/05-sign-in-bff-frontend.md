# Simplify BFF callback.astro to use new sign-in endpoint

**Developer Type:** Frontend
**Priority:** Critical
**Phase:** Frontend

## Description
Refactor `callback.astro` to replace the 3 separate API calls (lookup by provider → lookup by email → link/create) with a single `POST /api/users/sign-in` call. The BFF should only: exchange code → validate token → extract claims → call sign-in endpoint → set session cookie → redirect.

## Architecture Reference
Section "Sign-In Flow" from the desired architecture:
```
Frontend (callback.astro)          Backend (Silo)
─────────────────────────          ──────────────
Exchange code for tokens
Validate ID token
Extract claims (sub, name, etc.)
                                   POST /api/users/sign-in
                                   { providerId, providerName,
                                     displayName, avatarUrl, email }
                                   
                                   return { userId, isEmailVerified }
Set session cookie
Redirect
```

Current `callback.astro` (lines 52-108) does the branching logic. This task removes that logic.

## Technical Requirements
- Replace the block from line 52 (`// Look up user by providerId`) to line 111 (end of the if/else chain) with a single call to `POST /api/users/sign-in`:
  ```typescript
  const signInRes = await fetch(`${siloApiUrl}/api/users/sign-in`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      providerId,
      providerName,
      displayName,
      avatarUrl,
      email,
    }),
  });

  if (!signInRes.ok) {
    const errBody = await signInRes.text();
    throw new Error(`Sign-in failed (${signInRes.status}): ${errBody}`);
  }

  const { userId, isEmailVerified } = await signInRes.json();
  ```

- The `userId` returned from the endpoint is a `Guid` string. Ensure the `SessionPayload.userId` type matches (currently it appears to be `string`).
- Keep the session cookie construction and redirect logic unchanged.
- Keep the error handling (try/catch with redirect to `/auth/login`) unchanged.

## Mock API Contract
```json
// POST /api/users/sign-in
// Request:
{
  "providerId": "github|12345678",
  "providerName": "github",
  "displayName": "Jane Doe",
  "avatarUrl": "https://avatars.githubusercontent.com/u/12345",
  "email": "jane@example.com"
}

// Response 200:
{
  "userId": "550e8400-e29b-41d4-a716-446655440000",
  "isEmailVerified": true
}
```

## Acceptance Criteria
- [ ] `callback.astro` no longer calls `/api/users/by-provider/{id}`, `/api/users/by-email/{email}`, or `POST /api/users/{userId}/link-identity` or `POST /api/users/create`
- [ ] `callback.astro` calls `POST /api/users/sign-in` with the exact contract above
- [ ] Session cookie is set with the `userId` and `isEmailVerified` returned from the new endpoint
- [ ] Redirect to `/dashboard` or `/verify-email` based on `isEmailVerified` still works
- [ ] Error handling still redirects to `/auth/login` on failure

## Dependencies
- Task 03 (sign-in endpoint must be implemented first)

## Estimated Effort
S (2-4h)

## Notes
- The `providerName` derivation (`const providerName = providerId.split('|')[0]`) is still correct for Auth0; keep it.
- Keep the state validation (lines 27-34) and token exchange (lines 36-48) unchanged.
- Ensure `SILO_API_URL` environment variable is correctly referenced.