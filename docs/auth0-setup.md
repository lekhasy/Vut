# Auth0 Tenant Setup Guide

This guide walks through configuring Auth0 for the Vut platform. Complete all steps before starting backend development that depends on authentication.

## Prerequisites

- An Auth0 account (free tier is sufficient for MVP — 7,500 MAU, 2 social connections)
- A GitHub account with access to Developer Settings

---

## Step 1: Create the Auth0 Application

1. Log in to the [Auth0 Dashboard](https://manage.auth0.com/).
2. Navigate to **Applications > Applications**.
3. Click **Create Application**.
4. Name it `Vut App`.
5. Select **Regular Web Application** and click **Create**.
6. In the application **Settings** tab, configure:

| Setting | Dev Value | Prod Value |
|---------|-----------|------------|
| Allowed Callback URLs | `http://localhost:3000/auth/callback` | `https://your-production-domain/auth/callback` |
| Allowed Logout URLs | `http://localhost:3000` | `https://your-production-domain` |
| Allowed Web Origins | `http://localhost:3000` | `https://your-production-domain` |

7. Scroll to **Advanced Settings > Grant Types** and ensure only these are checked:
   - Authorization Code
   - Refresh Token
8. Click **Save Changes**.
   > **Note:** Token Endpoint Authentication Method defaults to `client_secret_post` for Regular Web Applications and is not shown as a configurable option in the dashboard. No action needed.
9. Note the **Client ID** and **Client Secret** from the Settings tab.

---

## Step 2: Enable GitHub Social Connection

1. Navigate to **Authentication > Social**.
2. Click **Create Connection**.
3. Select **GitHub**.

### Create a GitHub OAuth App (Organization-owned)

1. Go to your GitHub organization page > **Settings > Developer settings > OAuth Apps**.
   > For individual accounts this is under personal Settings > Developer settings, but for Vut, create it under the org so it persists independently of any individual account.
2. Click **New OAuth App**.
3. Fill in:
   - **Application name:** `Vut Dev`
   - **Homepage URL:** `http://localhost:3000`
   - **Authorization callback URL:** `https://{your-auth0-domain}/login/callback` (find your domain in Auth0 under Applications > Applications > Vut App > Settings > Domain)
4. Click **Register application**.
5. Note the **Client ID**.
6. Click **Generate a new client secret** and note the **Client Secret**.

### Configure the Connection in Auth0

1. Back in Auth0's GitHub connection setup, paste the GitHub Client ID and Client Secret.
2. Check the option to **retrieve email address from GitHub** (ensures `email` is included in the profile even when the user's GitHub email is private).
3. Under **Permissions**, ensure these scopes are selected:
   - `read:user`
   - `user:email`
3. Click **Create** and then **Save**.
4. When prompted, enable the connection for the `Vut App` application.

---

## Step 3: Create the API Identifier (Audience)

1. Navigate to **Applications > APIs**.
2. Click **Create API**.
3. Fill in:
   - **Name:** `Vut API`
   - **Identifier:** `https://api.vut.dev` (this is a logical identifier, not a real URL)
   - **Signing Algorithm:** RS256
4. Click **Create**.
5. In the API settings:
   - Enable **Allow Offline Access** (for refresh tokens).
   - Set **Token Expiration** to **15 minutes** (access token).
   - ID token lifetime is configured at the Application level, not here.

---

## Step 4: Verify User Profile Claims

You can test the login flow directly from the Auth0 Dashboard — no frontend needed.

1. Navigate to **Authentication > Social > GitHub** and click the connection.
2. Click **Try Connection** (or the **Test** button) to authenticate with your GitHub account.
3. After successful login, Auth0 shows the raw user profile with all returned claims.
4. Verify the following claims are present:
   - `sub` — e.g., `github|12345678`
   - `nickname` — GitHub username
   - `name` — Full display name
   - `picture` — Avatar URL
   - `email` — Primary email (requires `user:email` scope and "retrieve email" checked in Step 2)
5. If `email` is missing, create an Auth0 Action:
   - Go to **Actions > Flows > Login**.
   - Create a custom action that adds `email` to the ID token if present.
   - For Epic 1, the default GitHub claims should be sufficient — only add a custom action if the test reveals missing claims.

---

## Step 5: Update Kubernetes Secrets

After completing the above steps, update the Auth0 secret with your actual values.

### Using kubectl (recommended for dev)

```bash
# Create/update the secret with your actual values
kubectl create secret generic vut-auth0-secret \
  --namespace=vut \
  --from-literal=domain='YOUR_AUTH0_DOMAIN' \
  --from-literal=audience='https://api.vut.dev' \
  --from-literal=client-id='YOUR_CLIENT_ID' \
  --from-literal=client-secret='YOUR_CLIENT_SECRET' \
  --dry-run=client -o yaml | kubectl apply -f -
```

### Using the manifest file

Edit `infrastructure/k8s/secrets/vut-auth0-secret.yaml` and replace the placeholder values:

```yaml
stringData:
  domain: "your-tenant.us.auth0.com"
  audience: "https://api.vut.dev"
  client-id: "your-actual-client-id"
  client-secret: "your-actual-client-secret"
```

---

## Expected JWT Payload

After successful authentication, the access token JWT should contain:

```json
{
  "sub": "github|12345678",
  "nickname": "janedev",
  "name": "Jane Developer",
  "picture": "https://avatars.githubusercontent.com/u/12345678?v=4",
  "email": "jane@example.com",
  "aud": "https://api.vut.dev",
  "iss": "https://your-tenant.us.auth0.com/",
  "exp": 1715000000,
  "iat": 1714999100
}
```

## Testing the Setup

1. Use the Auth0 Dashboard **Quick Start** tab for the `Vut App` to test the login flow.
2. Alternatively, use [jwt.io](https://jwt.io) to decode a token and verify all claims are present.
3. Verify the GitHub connection works by logging in with your GitHub account through the Auth0 test flow.
