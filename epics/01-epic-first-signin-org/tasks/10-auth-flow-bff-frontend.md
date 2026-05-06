# Task 10: Auth Flow & BFF Session Management

| Field | Value |
|-------|-------|
| **Developer** | Frontend |
| **Work Order** | 10 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 2.5 days |

## Description

Implement the complete authentication flow in the Astro.js BFF: Auth0 login redirect, callback handling, JWT validation, user creation/retrieval on first login, session cookie management, and logout. This task also includes the Auth middleware that runs on every request to validate the session.

## Architecture Reference

- Architecture doc Section 4 (Auth0 Integration Architecture)
- Architecture doc Section 4.1 (Auth flow sequence diagram)
- Architecture doc Section 4.3 (Auth Middleware)
- Architecture doc Section 8.1 (First-Time User Sign-In)
- Architecture doc Section 8.2 (Email Verification)
- Architecture doc Section 8.3 (Returning User Sign-In)

## Technical Requirements

### Auth Endpoints (BFF Server Routes)

#### GET /auth/login
1. Generate a random `state` parameter and store it in a short-lived cookie.
2. Construct the Auth0 authorization URL:
   ```
   https://{AUTH0_DOMAIN}/authorize?
     response_type=code&
     client_id={AUTH0_CLIENT_ID}&
     redirect_uri={BASE_URL}/auth/callback&
     scope=openid profile email&
     audience={AUTH0_AUDIENCE}&
     connection=github&
     state={state}
   ```
3. Redirect the browser to this URL.

#### GET /auth/callback
1. Validate the `state` parameter matches the cookie.
2. Exchange the authorization `code` for tokens:
   ```
   POST https://{AUTH0_DOMAIN}/oauth/token
   Body: {
     grant_type: "authorization_code",
     client_id: AUTH0_CLIENT_ID,
     client_secret: AUTH0_CLIENT_SECRET,
     code: request.query.code,
     redirect_uri: "{BASE_URL}/auth/callback"
   }
   ```
3. Parse the response to get `id_token` and `access_token`.
4. Validate the ID token JWT:
   - Verify signature against Auth0 JWKS endpoint (`https://{AUTH0_DOMAIN}/.well-known/jwks.json`).
   - Verify `iss` matches Auth0 domain.
   - Verify `aud` matches the client ID.
   - Verify `exp` is in the future.
5. Extract claims from the ID token:
   - `sub` -> `providerId` (e.g., `github|12345678`, `google-oauth2|12345`, `windowslive|12345`)
   - `nickname` -> username
   - `name` -> display name
   - `picture` -> avatar URL
   - `email` -> email address (may be null — providers don't always return it)
6. Look up user in read model by providerId: `GET http://{READMODEL_URL}/api/users/by-provider/{providerId}`.
7. **If user not found (404)** — check for auto-linking:
   a. If email is present from the provider, look up: `GET http://{READMODEL_URL}/api/users/by-email/{email}`.
   b. If an existing user is found by email, link the new provider: send `LinkIdentity` command to actor service.
   c. If no existing user found (truly new user), create user via actor service:
   ```
   POST http://{ACTOR_SERVICE_URL}/commands
   {
     "commandType": "CreateUser",
     "payload": {
       "providerId": "github|12345678",
       "providerName": "github",
       "displayName": "Jane Developer",
       "avatarUrl": "https://avatars.githubusercontent.com/u/12345678",
       "email": "jane@example.com"
     },
     "actorId": "pending"
   }
   ```
8. Set an HTTP-only, Secure, SameSite=Lax session cookie:
   ```
   Set-Cookie: vut_session={encrypted_payload}; HttpOnly; Secure; SameSite=Lax; Path=/; Max-Age=86400
   ```
   The cookie payload should contain: `{ userId, providerId, displayName, avatarUrl, isEmailVerified }` encrypted with a server-side secret.
9. If `isEmailVerified` is false, redirect to `/verify-email`. Otherwise, redirect to `/dashboard`.

#### POST /auth/logout
1. Clear the `vut_session` cookie.
2. Redirect to Auth0 logout URL:
   ```
   https://{AUTH0_DOMAIN}/v2/logout?returnTo={BASE_URL}&client_id={AUTH0_CLIENT_ID}
   ```

### Auth Middleware (Astro Middleware)
Create `src/middleware.ts` (or `src/middleware/index.ts`) that runs on every request:

1. **Public paths** (skip auth check): `/`, `/auth/login`, `/auth/callback`, `/verify-email`, public assets.
2. **Protected paths** (all others):
   - Extract `vut_session` cookie.
   - Decrypt and parse the cookie payload.
   - If invalid or missing, redirect to `/auth/login`.
   - If valid, attach `userId`, `providerId`, `displayName`, `avatarUrl`, `isEmailVerified` to `Astro.locals` (the request context).
3. **Email verification guard**: If `isEmailVerified` is false and the path is not `/verify-email` or `/auth/*`, redirect to `/verify-email`.
4. Attach `userId` as `actorId` for all downstream API calls.

### Session Cookie Encryption
- Use `crypto` (Node.js built-in) with AES-256-GCM.
- Encryption key from environment variable `SESSION_SECRET` (32 bytes, base64-encoded).
- Cookie payload: `{ userId, providerId, displayName, avatarUrl, isEmailVerified, iat }`.

### Environment Variables
```
AUTH0_DOMAIN=xxx.us.auth0.com
AUTH0_CLIENT_ID=abc123
AUTH0_CLIENT_SECRET=secret123
AUTH0_AUDIENCE=https://api.vut.dev
SESSION_SECRET=<32-byte-base64-key>
ACTOR_SERVICE_URL=http://vut-actor-service:5000
READMODEL_URL=http://vut-readmodel-api:5001
BASE_URL=http://localhost:3000
```

### File Structure
```
src/
  pages/
    auth/
      login.astro       # Minimal page that triggers redirect
      callback.astro    # Minimal page that handles callback
  middleware.ts          # Auth middleware
  lib/
    auth/
      session.ts        # Cookie encrypt/decrypt, session management
      auth0.ts          # Auth0 URL construction, token exchange, JWKS validation
      jwt.ts            # JWT validation utilities
```

## API Contracts

### External: Auth0 Token Response
```json
{
  "access_token": "...",
  "id_token": "eyJhbGciOiJSUzI1NiIs...",
  "scope": "openid profile email",
  "expires_in": 86400,
  "token_type": "Bearer"
}
```

### Internal: Create User Command (to Actor Service)
```json
POST /commands
{
  "commandType": "CreateUser",
  "payload": {
    "providerId": "github|12345678",
    "providerName": "github",
    "displayName": "Jane Developer",
    "avatarUrl": "https://avatars.githubusercontent.com/u/12345678",
    "email": "jane@example.com"
  },
  "actorId": "pending"
}
```
Response:
```json
{
  "success": true,
  "payload": "{\"userId\":\"a1b2c3d4-e5f6-7890-abcd-ef1234567890\"}"
}
```

### Internal: Link Identity Command (to Actor Service)
```json
POST /commands
{
  "commandType": "LinkIdentity",
  "payload": {
    "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "providerId": "google-oauth2|1234567890",
    "providerName": "google",
    "email": "jane@example.com"
  },
  "actorId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

### Internal: Lookup User (to Read Model API)
```
GET /api/users/by-provider/github%7C12345678
```
Response (200):
```json
{
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "displayName": "Jane Developer",
  "avatarUrl": "https://avatars.githubusercontent.com/u/12345678",
  "email": "jane@example.com",
  "isEmailVerified": false,
  "createdAt": "2026-05-05T14:30:00.000Z",
  "updatedAt": "2026-05-05T14:30:00.000Z"
}
```
Response (404):
```json
{
  "error": "NOT_FOUND",
  "message": "User not found"
}
```

## Acceptance Criteria

- [ ] Clicking "Sign in with GitHub" redirects to Auth0 and then GitHub OAuth.
- [ ] After GitHub approval, the callback correctly exchanges the code for tokens.
- [ ] JWT is validated against Auth0 JWKS (signature, issuer, audience, expiry).
- [ ] First-time users are created in the system (via actor service) and a session is set.
- [ ] Returning users are recognized and a session is set without creating a duplicate.
- [ ] Auto-linking: if a new provider's email matches an existing user, the identity is linked automatically.
- [ ] Unverified users are redirected to `/verify-email` after login.
- [ ] Session cookie is HTTP-only, Secure, SameSite=Lax, and encrypted.
- [ ] Auth middleware protects all non-public routes and redirects to login if no session.
- [ ] Email verification guard redirects unverified users to `/verify-email` from any protected route.
- [ ] Logout clears the session and redirects to Auth0 logout.
- [ ] `Astro.locals` contains `userId`, `providerId`, `displayName`, `avatarUrl`, `isEmailVerified` on all authenticated requests.
- [ ] No email/password sign-up path exists anywhere in the product.

## Dependencies

- Task 09 (Astro Project Setup) -- project structure must exist.
- Task 02 (Auth0 Tenant Setup) -- Auth0 tenant must be configured.
- For local dev without backend: can mock the actor service and read model API responses.

## Notes

- The BFF pattern means the browser never sees the JWT directly. The BFF validates it server-side and creates its own session cookie. This is more secure than storing tokens in localStorage.
- Use the `jose` npm package for JWT validation (it supports JWKS and RS256).
- The `state` parameter in the OAuth flow prevents CSRF attacks. It must be validated in the callback.
- The `actorId` in the `CreateUser` command is "pending" because the user doesn't have a userId yet. The actor service will generate one and return it.
- Session cookie expiry: 24 hours for MVP. Users will be redirected to re-authenticate via GitHub after expiry.
