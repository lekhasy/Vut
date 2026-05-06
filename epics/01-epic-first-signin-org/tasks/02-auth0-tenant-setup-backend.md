# Task 02: Auth0 Tenant Configuration

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 02 |
| **Priority** | P0 -- Blocking |
| **Estimated Effort** | 0.5 days |

## Description

Configure the Auth0 tenant for Vut: enable GitHub as the sole social connection, register the application, configure callback/logout URLs, and define the API identifier (audience). Document the configuration so it can be reproduced by any developer.

## Architecture Reference

- Architecture doc Section 4 (Auth0 Integration Architecture)
- Architecture doc Section 4.2 (JWT Claims Used)

## Technical Requirements

### Auth0 Application Setup
1. Create a new "Regular Web Application" in Auth0 (this is the BFF/server-side app).
2. Configure the following:
   - **Allowed Callback URLs:** `http://localhost:3000/auth/callback` (dev), production URL (prod).
   - **Allowed Logout URLs:** `http://localhost:3000` (dev), production URL (prod).
   - **Allowed Web Origins:** `http://localhost:3000` (dev).
   - **Grant Types:** Authorization Code (with PKCE), Refresh Token.
   - **Token Endpoint Authentication Method:** Post.
3. Note the **Client ID** and **Client Secret** -- these go into `vut-auth0-secret` in Kubernetes.

### GitHub Connection
1. In Auth0 > Authentication > Social, enable **GitHub**.
2. Create a GitHub OAuth App (Settings > Developer settings > OAuth Apps) with:
   - Authorization callback URL: `https://{auth0-domain}/login/callback`
3. Note the GitHub Client ID and Client Secret, enter them in Auth0's GitHub connection settings.
4. Ensure the GitHub connection requests scopes: `read:user`, `user:email`.

### API Identifier (Audience)
1. In Auth0 > Applications > APIs, create an API named "Vut API".
2. Set the **Identifier** (audience) to something like `https://api.vut.dev` (this is a logical identifier, not a real URL).
3. Enable "Allow Offline Access" for refresh tokens.
4. Set token expiration: Access Token 15 min, ID Token 24 hours.

### User Profile Claims
- Ensure the ID token includes: `sub`, `nickname`, `name`, `picture`, `email`.
- Add Auth0 Rules/Actions if needed to ensure `email` is always included (GitHub requires `user:email` scope).

### Documentation
- Create `docs/auth0-setup.md` with step-by-step instructions including screenshots (or clear text steps) so any developer can reproduce the setup.
- Include the expected JWT payload structure:
```json
{
  "sub": "github|12345678",
  "nickname": "janedev",
  "name": "Jane Developer",
  "picture": "https://avatars.githubusercontent.com/u/12345678?v=4",
  "email": "jane@example.com",
  "aud": "https://api.vut.dev",
  "iss": "https://{tenant}.us.auth0.com/",
  "exp": 1715000000,
  "iat": 1714999100
}
```

## Acceptance Criteria

- [ ] Auth0 application is created with correct callback/logout URLs.
- [ ] GitHub social connection is enabled and working.
- [ ] A test user can authenticate via GitHub and receive a valid JWT.
- [ ] JWT contains all required claims: `sub`, `nickname`, `name`, `picture`, `email`.
- [ ] API identifier (audience) is configured and included in tokens.
- [ ] Setup documentation exists in `docs/auth0-setup.md`.
- [ ] Client ID, Client Secret, Domain, and Audience are stored in `k8s/secrets/vut-auth0-secret.yaml` (base64-encoded).

## Dependencies

- None. Can start immediately in parallel with Task 01.

## Notes

- Auth0 free tier allows up to 7,500 MAU and 2 social connections -- sufficient for MVP.
- If the team prefers not to use Auth0's hosted UI, the BFF will handle the redirect flow manually (architecture doc Section 4.1).
- Keep Auth0 Actions minimal -- avoid custom claims manipulation in Epic 1 unless the default GitHub claims are insufficient.
