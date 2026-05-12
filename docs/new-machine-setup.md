# Production Machine Setup Guide (WSL + K3s)

Step-by-step guide to set up the Velucid platform on a **WSL2 production machine** running K3s + ArgoCD. After the one-time setup, a single script brings the entire stack back up on every WSL restart.

> **For local development on Windows**, use Docker Compose instead — see `infrastructure/README.md`.

---

## Prerequisites

| Requirement | Minimum | Notes |
|---|---|---|
| OS | Windows 10/11 with WSL2 | Ubuntu 22.04+ recommended |
| RAM | 16 GB total | ~22 GB for full prod budget; 8 GB workable for light use |
| Disk | 40 GB free (in WSL) | K3s images + persistent volumes |
| Internet | Required | For pulling images from ghcr.io |

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

| Key | Used by | Description |
|-----|---------|-------------|
| `AUTH0_DOMAIN` | Frontend, Silo | Auth0 tenant domain |
| `AUTH0_AUDIENCE` | Frontend, Silo | Auth0 API identifier |
| `AUTH0_APP_CLIENT_ID` | Frontend, Silo | Auth0 application client ID |
| `AUTH0_APP_CLIENT_SECRET` | Frontend, Silo | Auth0 application client secret |
| `POSTGRES_USERNAME` | PostgreSQL | Database username |
| `POSTGRES_PASSWORD` | PostgreSQL | Database password |
| `RESEND_API_KEY` | Silo | Resend email API key |

The Infisical Operator syncs these into K8s secrets every 60 seconds. Changes in Infisical propagate automatically.

> **Note:** The `cloudflared-tunnel-credentials` secret is managed separately via `kubectl create secret` since it contains a JSON file, not key-value pairs.

---

## 6. Container Images (Automatic via CI)

Application images are built automatically by **GitHub Actions** and pushed to GitHub Container Registry:

| Image | Registry Path |
|-------|--------------|
| Silo | `ghcr.io/lekhasy/vut/silo:<commit-sha>` |
| Frontend | `ghcr.io/lekhasy/vut/frontend:<commit-sha>` |
| Projector Service | `ghcr.io/lekhasy/vut/projector-service:<commit-sha>` |

The CI pipeline:
1. Builds Docker images on push to `main`
2. Pushes to `ghcr.io/lekhasy/vut/`
3. Updates the image tag in the K8s manifests (git commit)
4. ArgoCD detects the manifest change and deploys the new image

> **Private repo?** K3s needs a pull secret to access ghcr.io:
> ```bash
> sudo k3s kubectl create secret docker-registry ghcr-pull-secret \
>   --namespace=velucid \
>   --docker-server=ghcr.io \
>   --docker-username=<github-username> \
>   --docker-password=<personal-access-token>
> ```
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

| Access Method | URL |
|---|---|
| Cloudflare Tunnel | https://velucid.app |
| Port-forward (Frontend) | `sudo k3s kubectl port-forward -n velucid svc/velucid-frontend 3000:3000` |
| Port-forward (API) | `sudo k3s kubectl port-forward -n velucid svc/velucid-silo 5000:5000` |
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
