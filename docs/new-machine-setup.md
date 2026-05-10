# New Machine Setup Guide

Step-by-step guide to get the VUT platform running on a fresh developer machine. This covers both the **Docker Compose** workflow (recommended for daily dev) and the **K3s** workflow (for staging/production or K8s-flavored development).

---

## Prerequisites

| Requirement | Minimum | Notes |
|---|---|---|
| RAM | 8 GB free | ~7 GB used by dev stack |
| Disk | 20 GB free | Docker images + data volumes |
| OS | Linux, macOS, or Windows (WSL2) | K3s requires Linux or WSL2 |
| Git | Any recent version | To clone the repository |

---

## 1. Clone the Repository

```bash
git clone https://github.com/lekhasy/Vut.git
cd Vut
```

---

## 2a. Docker Compose Setup (Recommended for Dev)

### Install Docker

- **Linux:** https://docs.docker.com/engine/install/
- **macOS:** Docker Desktop (https://docs.docker.com/desktop/install/mac-install/)
- **Windows:** Docker Desktop with WSL2 backend (https://docs.docker.com/desktop/install/windows-install/)

Verify installation:

```bash
docker --version         # Docker Engine 24+
docker compose version   # Docker Compose v2+
```

### Configure Environment

```bash
cd infrastructure

# Review dev defaults (usually no changes needed)
cat .env.dev

# (Optional) Create a personal override file — git-ignored
cp .env.dev .env.local
# Edit .env.local to set your Auth0 credentials:
#   AUTH0_DOMAIN=dev-vut.us.auth0.com
#   AUTH0_CLIENT_ID=<your-client-id>
#   AUTH0_CLIENT_SECRET=<your-client-secret>
```

### Start Infrastructure

```bash
./scripts/start.sh
```

This starts **KurrentDB**, **PostgreSQL**, and **pgAdmin**. The silo, projector-service, and frontend are commented out until their Docker images are built (later tasks).

### Verify

```bash
./scripts/health-check.sh
```

You should see:

| Service | URL | Expected |
|---|---|---|
| KurrentDB Dashboard | http://localhost:2113 | Web UI loads |
| PostgreSQL | `localhost:5432` | Accepts connections |
| pgAdmin | http://localhost:8081 | Login page (admin@vut.dev / admin) |

### Stop

```bash
./scripts/stop.sh
```

---

## 2b. K3s Setup (Staging/Production or K8s Dev)

### Install K3s

```bash
# Linux / WSL2
curl -sfL https://get.k3s.io | sh -

# Verify
sudo k3s kubectl get nodes
# You should see one node in "Ready" state
```

> **Windows users:** K3s must be run inside WSL2. Install a WSL2 distro (Ubuntu recommended), then run the K3s install command inside WSL2.

> **macOS users:** K3s does not run natively on macOS. Use a Linux VM (e.g., with Multipass or Lima) or use the Docker Compose workflow instead.

### Configure kubectl (Optional)

K3s includes its own `kubectl`. To use the system `kubectl`:

```bash
# Copy K3s kubeconfig to the standard location
sudo cp /etc/rancher/k3s/k3s.yaml ~/.kube/config
sudo chown $USER:$USER ~/.kube/config

# Verify
kubectl get nodes
```

### Container Images (ghcr.io — Automatic)

Application images are built automatically by **GitHub Actions** and pushed to GitHub Container Registry (`ghcr.io/lekhasy/vut/`). You do **not** need to build or import images manually — ArgoCD pulls them from the registry.

| Image | Registry Path |
|-------|--------------|
| Silo | `ghcr.io/lekhasy/vut/silo:<commit-sha>` |
| Frontend | `ghcr.io/lekhasy/vut/frontend:<commit-sha>` |
| Projector Service | `ghcr.io/lekhasy/vut/projector-service:<commit-sha>` |

> **Private repo?** If the repository is private, K3s needs a pull secret to access ghcr.io. Create one with:
> ```bash
> kubectl create secret docker-registry ghcr-pull-secret \
>   --namespace=vut \
>   --docker-server=ghcr.io \
>   --docker-username=<github-username> \
>   --docker-password=<personal-access-token>
> ```
> Then add `imagePullSecrets: [{ name: ghcr-pull-secret }]` to each deployment.

### Update Secrets

Before deploying, update the secret files with your actual credentials:

```bash
cd infrastructure

# 1. PostgreSQL secret (dev defaults are fine for local dev)
# Edit k8s/secrets/vut-postgresql-secret.yaml if needed

# 2. Auth0 secret — replace placeholders with your Auth0 tenant values
# Edit k8s/secrets/vut-auth0-secret.yaml:
#   domain: dev-vut.us.auth0.com
#   audience: https://vut-api-dev
#   client-id: <your-client-id>
#   client-secret: <your-client-secret>
```

### Install ArgoCD

ArgoCD watches the K8s manifests in git and auto-syncs changes to K3s. Install it once:

```bash
# Create namespace and install ArgoCD
kubectl create namespace argocd
kubectl apply -n argocd -f https://raw.githubusercontent.com/argoproj/argo-cd/stable/manifests/install.yaml

# Wait for ArgoCD to be ready
kubectl wait --for=condition=available deployment/argocd-server -n argocd --timeout=120s

# Apply the VUT ArgoCD Application manifest
cd infrastructure
kubectl apply -f k8s/argocd/application.yaml
```

ArgoCD will now auto-sync all manifests from `infrastructure/k8s/` in the git repo. Any push to `main` that changes manifests will be deployed automatically.

> **Access ArgoCD UI (optional):**
> ```bash
> kubectl port-forward svc/argocd-server -n argocd 8080:443 &
> # Get initial admin password:
> kubectl -n argocd get secret argocd-initial-admin-secret -o jsonpath="{.data.password}" | base64 -d
> # Open https://localhost:8080 — login with user "admin" and the password above
> ```

### Deploy All Manifests (Manual — First Time or Without ArgoCD)

If ArgoCD is installed, deployment is automatic. For initial bootstrapping or manual deployment:

```bash
cd infrastructure
./k8s/dev-setup.sh
```

The script will:
1. Create the `vut` namespace
2. Apply secrets
3. Deploy PostgreSQL and KurrentDB (waits for Ready)
4. Deploy the silo, projector-service, and frontend
5. Create Traefik ingress rules
6. Attempt to deploy cloudflared (skips if credentials not configured)

> **Note:** Once ArgoCD is running, you should not need `kubectl apply` manually — ArgoCD is the single source of truth.

### Verify

```bash
kubectl get pods -n vut
# All pods should be Running

kubectl get svc -n vut
# Lists all services and their ClusterIPs
```

Access services via port-forward:

```bash
kubectl port-forward -n vut svc/vut-kurrentdb 2113:2113 &
# Open http://localhost:2113

kubectl port-forward -n vut svc/vut-silo 5000:5000 &
# API at http://localhost:5000

kubectl port-forward -n vut svc/vut-frontend 3000:3000 &
# Frontend at http://localhost:3000
```

### Teardown

```bash
kubectl delete namespace vut
```

---

## 3. Cloudflare Tunnel Setup (Optional — Internet Access)

This is only needed if you want the platform reachable from the internet at `vut.app`. Skip this for purely local development.

### One-Time Setup

```bash
# 1. Install cloudflared CLI
#    https://developers.cloudflare.com/cloudflare-one/connections/connect-networks/downloads/

# 2. Authenticate with your Cloudflare account
cloudflared tunnel login

# 3. Create a tunnel named "vut"
cloudflared tunnel create vut
# This outputs a Tunnel ID and creates ~/.cloudflared/<TUNNEL_ID>.json

# 4. Create DNS records pointing to the tunnel
cloudflared tunnel route dns vut vut.app
cloudflared tunnel route dns vut "*.vut.app"
```

### Configure K8s Manifests

```bash
# 1. Get the tunnel ID from step 3
TUNNEL_ID="<your-tunnel-id>"

# 2. Base64-encode the credentials file
cat ~/.cloudflared/${TUNNEL_ID}.json | base64

# 3. Edit k8s/cloudflared/secret.yaml
#    Replace <BASE64_TUNNEL_CREDENTIALS_JSON> with the base64 value

# 4. Edit k8s/cloudflared/configmap.yaml
#    Replace <TUNNEL_ID> with your actual tunnel ID

# 5. Apply
kubectl apply -f k8s/cloudflared/
```

### Verify

```bash
kubectl logs -n vut -l app.kubernetes.io/name=cloudflared
# Should show "Connection registered" and "Registered tunnel connection"
```

Traffic flow: `Internet → vut.app (Cloudflare DNS) → Cloudflare Edge → cloudflared pod → Traefik → services`

---

## 4. Auth0 Setup

Auth0 is used for authentication. A separate guide exists at `docs/auth0-setup.md`.

Quick summary:
1. Create a free Auth0 tenant at https://auth0.com
2. Create an API (`https://vut-api-dev`) and a Regular Web Application
3. Set the callback URLs to `http://localhost:3000/auth/callback` (Docker Compose) or `https://vut.app/auth/callback` (Cloudflare Tunnel)
4. Copy the Domain, Client ID, and Client Secret into `.env.dev` (Compose) or `k8s/secrets/vut-auth0-secret.yaml` (K3s)

---

## Quick Reference

### Docker Compose Commands

```bash
cd infrastructure
./scripts/start.sh          # Start dev environment
./scripts/stop.sh           # Stop (preserves data)
./scripts/health-check.sh   # Verify all services
docker compose logs -f silo  # Tail silo logs
docker compose down -v       # Stop + delete all data
```

### K3s Commands

```bash
./k8s/dev-setup.sh                      # Deploy everything
./k8s/dev-setup.sh --cleanup            # Wipe and redeploy
kubectl get pods -n vut                  # Pod status
kubectl logs -f -n vut -l app.kubernetes.io/component=backend   # Silo logs
kubectl logs -f -n vut -l app.kubernetes.io/component=projector # Projector logs
kubectl delete namespace vut             # Teardown
```

### Rebuilding After Code Changes

```bash
# Docker Compose (local dev — builds from source)
docker compose up -d --build silo projector-service frontend

# K3s with ArgoCD (automatic — just push to main)
git add . && git commit -m "feat: my changes" && git push origin main
# GitHub Actions builds new images → updates K8s manifests → ArgoCD deploys

# K3s manual (without ArgoCD, or for testing local image changes)
docker build -t ghcr.io/lekhasy/vut/silo:local ./backend/src/Vut.Silo
docker save ghcr.io/lekhasy/vut/silo:local | sudo k3s ctr images import -
kubectl set image deployment/vut-silo -n vut silo=ghcr.io/lekhasy/vut/silo:local
```
