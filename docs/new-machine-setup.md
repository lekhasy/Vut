# Production Machine Setup Guide (WSL + K3s)

Step-by-step guide to set up the Velucid platform on a **WSL2 production machine** running K3s + ArgoCD. After the one-time setup, a single script brings the entire stack back up on every WSL restart.

> **For local development on Windows**, use Docker Compose instead — see `infrastructure/README.md`.

---

## 0. Local Development with Bun + Nx (Story 4.0+)

The repository is an **Nx monorepo** with **Bun as the package manager and JS runtime** (Bun everywhere — the Astro SSR container runs under Bun in production too). Follow these steps to get a working local checkout before doing anything else.

### 0.1 Install Bun 1.3.14

```bash
curl -fsSL https://bun.sh/install | bash
exec $SHELL -l    # reload shell so `bun` is on PATH
bun --version     # → 1.3.14
```

Bun is the only JS toolchain you need — Nx is invoked via `bunx nx`, not via a separate `npx`. Node is NOT required (the Astro production container runs under Bun), but if you have it installed for IDE tooling that's fine.

### 0.2 Clone and install

```bash
git clone <repo-url> Vut
cd Vut
bun install --frozen-lockfile
```

`bun install` at the repo root resolves the workspace (apps + libs) and writes a single `bun.lock` (text format) at the root. **Do not commit `bun.lockb` (binary lockfile)** — the `.gitignore` excludes it.

### 0.3 Day-to-day commands

| Task                                          | Command                                              |
| --------------------------------------------- | ---------------------------------------------------- |
| Run the web app (Astro dev server on `:4321`) | `bunx nx serve web`                                  |
| Build the web app for production              | `bunx nx build web`                                  |
| Run the web e2e suite (Playwright)            | `bunx nx test web`                                   |
| Build the .NET silo                           | `bunx nx build silo`                                 |
| Run silo unit tests                           | `bunx nx test silo`                                  |
| Build the TS projector skeleton               | `bunx nx build projector`                            |
| Run projector unit tests                      | `bunx nx test projector`                             |
| Lint everything                               | `bunx nx run-many -t lint --parallel`                |
| Typecheck everything                          | `bunx nx run-many -t typecheck --parallel`           |
| Run every project's unit tests                | `bunx nx run-many -t test --parallel`                |
| Render the dependency graph                   | `bunx nx graph`                                      |
| Only run checks for what changed vs `main`    | `bunx nx affected -t lint typecheck test --parallel` |
| Format-check the whole repo (CI runs this)    | `bunx nx format:check`                               |
| Format-fix the whole repo (local only)        | `bunx nx format:write`                               |

`bun install --frozen-lockfile` before any `make` / `nx` target that rebuilds the project — keeps the lockfile honest.

### 0.3.1 Pre-push hook (auto-installed)

`bun install` triggers the `prepare` script in the root `package.json`, which runs `git config core.hooksPath scripts/hooks`. From then on, every `git push` runs the tracked hook at `scripts/hooks/pre-push` automatically.

The hook runs the same gate as the CI `nx-verify` job — `bunx nx format:check` + `bunx nx run-many -t lint typecheck build --parallel` — so a green pre-push guarantees a green CI nx-verify. It does **not** run `test` (the Playwright e2e suite needs live Auth0 + silo + KurrentDB + Postgres; CI runs that separately, locally you use `bun run preflight` or `bunx nx run-many -t test --exclude web`).

| What you want to run              | Command                                                                |
| --------------------------------- | ---------------------------------------------------------------------- |
| Same gate as the hook (fast)      | `bash scripts/hooks/pre-push`                                          |
| Full coverage including e2e tests | `bun run preflight`                                                    |
| Unit tests only (skip e2e)        | `bunx nx run-many -t test --exclude web`                               |
| Bypass the hook for a hotfix      | `git push --no-verify` (you'll get a red CI nx-verify and fix forward) |

If the hook ever needs to be re-installed manually (e.g. you nuked `.git/config`): `bun run prepare` does it in one command.

### 0.4 Repository layout

```
apps/
  web/         ← Astro 5 + React 19 (moved from frontend/ in Story 4.0)
  projector/   ← Bun + Fastify + TypeScript (skeleton in 4.0; populated 4.2)
  silo/        ← Nx wrapper over backend/src/Velucid.Silo (dotnet 10)
libs/
  events/           ← @velucid/events            (populated 4.1)
  projection/       ← @velucid/projection        (populated 4.1)
  read-model/       ← @velucid/read-model        (populated 4.1)
  kurrent-client/   ← @velucid/kurrent-client    (populated 4.1)
backend/       ← legacy .NET solution (silo + projector-service + migrations);
                 untouched in 4.0; cleaned up in 4.5
infrastructure/ ← k8s manifests, docker-compose, secrets; unchanged
```

### 0.5 "Bun everywhere" notes

- **Astro production runs under Bun**: the Dockerfile at `apps/web/Dockerfile` builds with `oven/bun:1.3.14-alpine` and the runtime image starts with `bun ./dist/server/entry.mjs`. No Node runtime in the web container.
- **KurrentDB Node client (`@kurrent/kurrentdb-client@^1.3.0`)** ships a NAPI native bridge with a `linux-x64-musl` prebuilt, so it loads under the Alpine Bun image without needing build tools.
- **Fallback (only if a future dependency lacks a musl prebuilt)**: switch just that one container's base from `oven/bun:1.3.14-alpine` to `oven/bun:1.3.14` (Debian) or to `node:20-alpine`. **Do not silently fall back** — flag it and confirm with the team first.
- **Nx Cloud wiring is a Story 4.4 task.** The workspace is currently local-cache only (no `nxCloudId` in `nx.json`); the local `.nx/cache` works without it. When 4.4 lands, run `bunx nx connect`, set the real workspace ID in `nx.json`, and add `NX_CLOUD_ACCESS_TOKEN` as a CI secret.

---

## Prerequisites

| Requirement | Minimum                 | Notes                                                    |
| ----------- | ----------------------- | -------------------------------------------------------- |
| OS          | Windows 10/11 with WSL2 | Ubuntu 22.04+ recommended                                |
| RAM         | 16 GB total             | ~22 GB for full prod budget; 8 GB workable for light use |
| Disk        | 40 GB free (in WSL)     | K3s images + persistent volumes                          |
| Internet    | Required                | For pulling images from ghcr.io                          |

---

## 1. Set Up WSL2

If WSL2 is not already installed:

```powershell
# Run in PowerShell as Administrator
wsl --install -d Ubuntu
```

After reboot, open the Ubuntu terminal and complete the initial user setup.

### Enable systemd

K3s runs as a systemd service. Ensure systemd is enabled in WSL:

```bash
sudo tee /etc/wsl.conf > /dev/null << 'EOF'
[boot]
systemd=true

[network]
generateResolvConf=true
EOF
```

Then **restart WSL** from PowerShell:

```powershell
wsl --shutdown
# Re-open your Ubuntu terminal
```

Verify systemd is active:

```bash
systemctl is-system-running
# Should print: running (or degraded — that's fine)
```

---

## 2. Install K3s

```bash
curl -sfL https://get.k3s.io | sh -
```

K3s installs as a systemd service and starts automatically. Verify:

```bash
sudo k3s kubectl get nodes
# NAME        STATUS   ROLES                  AGE   VERSION
# <hostname>  Ready    control-plane,master   30s   v1.xx.x+k3s1
```

### (Optional) Set up kubectl alias

```bash
# Add to ~/.bashrc for convenience
echo 'alias kubectl="sudo k3s kubectl"' >> ~/.bashrc
source ~/.bashrc
```

---

## 3. Clone the Repository

```bash
git clone https://github.com/lekhasy/Vut.git
cd Vut
```

---

## 4. Install ArgoCD

ArgoCD watches the K8s manifests in git and auto-deploys on every push.

```bash
# Create ArgoCD namespace and install
sudo k3s kubectl create namespace argocd
sudo k3s kubectl apply --server-side -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# Wait for ArgoCD to be ready (~60-90 seconds)
sudo k3s kubectl wait --for=condition=available deployment/argocd-server -n argocd --timeout=180s
```

### Register the Velucid application

```bash
sudo k3s kubectl apply -f infrastructure/k8s/argocd/application.yaml
```

ArgoCD will now auto-sync all manifests from `infrastructure/k8s/` on the `main` branch. Any push to `main` that changes manifests triggers an automatic deployment.

### (Optional) Access ArgoCD UI

```bash
# Port-forward the ArgoCD server
sudo k3s kubectl port-forward svc/argocd-server -n argocd 8080:443 &

# Get the initial admin password
sudo k3s kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
echo ""

# Open https://localhost:8080 — login with user "admin" and the password above
```

---

## 5. Configure Secrets (Infisical)

Secrets are managed by [Infisical](https://infisical.com) and synced into K8s automatically via the Infisical Operator. No secrets are stored in git.

### a. Install Infisical Operator

The startup script (`k3s-start.sh`) installs this automatically. If you need to do it manually:

```bash
curl -fsSL https://raw.githubusercontent.com/helm/helm/main/scripts/get-helm-3 | bash
helm repo add infisical https://dl.cloudsmith.io/public/infisical/helm-charts/helm/charts/
helm repo update infisical
helm upgrade --install infisical-secrets-operator infisical/secrets-operator \
    --namespace infisical --create-namespace --wait
```

### b. Create Machine Identity credentials

1. In [Infisical Cloud](https://app.infisical.com), create a **Machine Identity** with access to the `velucid-8i-fk` project (Production environment).
2. Create the K8s secret with the identity credentials:

```bash
sudo k3s kubectl create namespace velucid  # if not already created
sudo k3s kubectl create secret generic infisical-machine-identity \
  -n velucid \
  --from-literal=clientId=<YOUR_MACHINE_IDENTITY_CLIENT_ID> \
  --from-literal=clientSecret=<YOUR_MACHINE_IDENTITY_CLIENT_SECRET>
```

### c. Required secrets in Infisical

Ensure these keys exist in the **Production** environment of your Infisical project:

| Key                   | Used by        | Description                     |
| --------------------- | -------------- | ------------------------------- |
| `AUTH0_DOMAIN`        | Frontend, Silo | Auth0 tenant domain             |
| `AUTH0_AUDIENCE`      | Frontend, Silo | Auth0 API identifier            |
| `AUTH0_CLIENT_ID`     | Frontend, Silo | Auth0 application client ID     |
| `AUTH0_CLIENT_SECRET` | Frontend, Silo | Auth0 application client secret |
| `POSTGRES_USERNAME`   | PostgreSQL     | Database username               |
| `POSTGRES_PASSWORD`   | PostgreSQL     | Database password               |
| `RESEND_API_KEY`      | Silo           | Resend email API key            |

The Infisical Operator syncs these into K8s secrets every 60 seconds. Changes in Infisical propagate automatically.

> **Note:** The `cloudflared-tunnel-credentials` secret is managed separately via `kubectl create secret` since it contains a JSON file, not key-value pairs.

---

## 6. Container Images (Automatic via CI)

Application images are built automatically by **GitHub Actions** and pushed to GitHub Container Registry:

| Image             | Registry Path                                        |
| ----------------- | ---------------------------------------------------- |
| Silo              | `ghcr.io/lekhasy/vut/silo:<commit-sha>`              |
| Frontend          | `ghcr.io/lekhasy/vut/frontend:<commit-sha>`          |
| Projector Service | `ghcr.io/lekhasy/vut/projector-service:<commit-sha>` |

The CI pipeline:

1. Builds Docker images on push to `main`
2. Pushes to `ghcr.io/lekhasy/vut/`
3. Updates the image tag in the K8s manifests (git commit)
4. ArgoCD detects the manifest change and deploys the new image

> **Private repo?** K3s needs a pull secret to access ghcr.io:
>
> ```bash
> sudo k3s kubectl create secret docker-registry ghcr-pull-secret \
>   --namespace=velucid \
>   --docker-server=ghcr.io \
>   --docker-username=<github-username> \
>   --docker-password=<personal-access-token>
> ```
>
> Then add `imagePullSecrets: [{ name: ghcr-pull-secret }]` to each deployment.

---

## 7. Cloudflare Tunnel (Internet Access)

Cloudflare Tunnel exposes the platform at `velucid.app` without opening inbound ports.

### One-Time Setup

```bash
# Install cloudflared
# https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/

# Authenticate with Cloudflare
cloudflared tunnel login

# Create a tunnel
cloudflared tunnel create velucid
# Note the Tunnel ID output

# Create DNS records
cloudflared tunnel route dns velucid velucid.app
cloudflared tunnel route dns velucid "*.velucid.app"
```

### Configure K8s Manifests

```bash
TUNNEL_ID="<your-tunnel-id>"

# Base64-encode the credentials
cat ~/.cloudflared/${TUNNEL_ID}.json | base64

# Edit the secret and configmap
nano infrastructure/k8s/cloudflared/secret.yaml
# Replace <BASE64_TUNNEL_CREDENTIALS_JSON> with the base64 value

nano infrastructure/k8s/cloudflared/configmap.yaml
# Replace <TUNNEL_ID> with your actual tunnel ID

# Commit and push — ArgoCD will deploy automatically
git add -A && git commit -m "chore: configure cloudflare tunnel" && git push
```

Traffic flow: `Internet → velucid.app → Cloudflare Edge → cloudflared pod → Traefik → services`

---

## 8. Verify the Stack

```bash
# Check all pods
sudo k3s kubectl get pods -n velucid
# All pods should be Running

# Check services
sudo k3s kubectl get svc -n velucid

# Check ArgoCD sync status
sudo k3s kubectl get applications -n argocd
# STATUS should be "Synced" and HEALTH "Healthy"
```

### Access points

| Access Method            | URL                                                                        |
| ------------------------ | -------------------------------------------------------------------------- |
| Cloudflare Tunnel        | https://velucid.app                                                        |
| Port-forward (Frontend)  | `sudo k3s kubectl port-forward -n velucid svc/velucid-frontend 3000:3000`  |
| Port-forward (API)       | `sudo k3s kubectl port-forward -n velucid svc/velucid-silo 5000:5000`      |
| Port-forward (KurrentDB) | `sudo k3s kubectl port-forward -n velucid svc/velucid-kurrentdb 2113:2113` |

---

## 9. After WSL Restart

K3s is a systemd service and restarts automatically with WSL. However, if pods are slow to recover or you want to verify everything is up, run:

```bash
cd Vut/infrastructure
./scripts/k3s-start.sh
```

This single script:

1. Ensures K3s is running
2. Waits for the API server
3. Verifies ArgoCD is operational
4. Waits for all Velucid pods to be healthy
5. Shows status and access points

> **Tip:** Add it to your `.bashrc` to run automatically on WSL start:
>
> ```bash
> echo '~/Vut/infrastructure/scripts/k3s-start.sh' >> ~/.bashrc
> ```

---

## Troubleshooting

### K3s won't start

```bash
sudo journalctl -u k3s -n 50 --no-pager
# Common issue: port 6443 already in use from a previous run
sudo systemctl restart k3s
```

### Pods stuck in Pending

```bash
sudo k3s kubectl describe node
# Check if memory/CPU pressure is reported. Reduce resource limits in manifests if needed.
```

### ImagePullBackOff

```bash
sudo k3s kubectl describe pod -n velucid <pod-name>
# If ghcr.io access is denied, create the pull secret (see Section 6)
```

### CrashLoopBackOff

```bash
sudo k3s kubectl logs -n velucid <pod-name> --previous
# Common: secrets not applied before deployments. ArgoCD should handle ordering,
# but check that all secrets exist:
sudo k3s kubectl get secrets -n velucid
```

### ArgoCD not syncing

```bash
sudo k3s kubectl get applications -n argocd
# If status is "OutOfSync", force a sync:
sudo k3s kubectl patch application velucid -n argocd --type merge -p '{"operation":{"sync":{"prune":true}}}'
```

### Cloudflare Tunnel not connecting

```bash
sudo k3s kubectl logs -n velucid -l app.kubernetes.io/name=cloudflared
# Verify credentials match the tunnel ID in the configmap
```

---

## Quick Reference

```bash
# Start everything after WSL restart
./scripts/k3s-start.sh

# Check pod status
sudo k3s kubectl get pods -n velucid

# Tail logs
sudo k3s kubectl logs -f -n velucid -l app.kubernetes.io/component=frontend
sudo k3s kubectl logs -f -n velucid -l app.kubernetes.io/component=backend

# Force ArgoCD re-sync
sudo k3s kubectl patch application velucid -n argocd --type merge -p '{"operation":{"sync":{"prune":true}}}'

# Restart a specific deployment
sudo k3s kubectl rollout restart deployment/velucid-frontend -n velucid

# Full teardown (WARNING: deletes everything)
sudo k3s kubectl delete namespace velucid
```
