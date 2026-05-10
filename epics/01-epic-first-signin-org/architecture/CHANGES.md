# Architecture Changes — Epic 1

This file summarizes the latest architectural change to `architecture.md`.

---

## GitOps CI/CD Pipeline — GitHub Actions + ghcr.io + ArgoCD

**What changed:** Added a complete GitOps deployment pipeline. Every code change flows automatically from git commit to running containers in K3s.

| Aspect | Before | After |
|--------|--------|-------|
| Image building | Not specified | GitHub Actions (free cloud runners for OSS) |
| Container registry | Not specified | ghcr.io (GitHub Container Registry, free for public repos) |
| Deployment mechanism | Manual `kubectl apply` | ArgoCD (auto-sync from git) |
| Image tagging | `latest` only | Commit SHA + `latest` |
| Manifest updates | Manual | GitHub Actions bot commits new image tags |

**Pipeline flow:**
```
git push → GitHub Actions → build image → push to ghcr.io
  → update image tag in K8s manifests (git commit)
  → ArgoCD detects manifest change → syncs to K3s → rolling update
```

**Impact on implementation:**
- Create `.github/workflows/ci.yaml` with build jobs for silo and frontend images.
- All K8s manifests under `k8s/` — ArgoCD watches this directory recursively.
- Install ArgoCD in K3s (`argocd` namespace).
- Create ArgoCD Application manifest (`k8s/argocd/application.yaml`) pointing at the git repo's `k8s/` directory.
- Image references in manifests use `ghcr.io/lekhasy/vut/<service>:<commit-sha>`.
- GitHub Actions authenticates to ghcr.io using the built-in `GITHUB_TOKEN` (no extra secrets needed for public repos).
- ArgoCD `syncPolicy.automated` with `prune: true` and `selfHeal: true` — git is the single source of truth.

**New files to create:**
- `.github/workflows/ci.yaml` — CI pipeline definition
- `k8s/argocd/application.yaml` — ArgoCD application manifest

**Architecture sections affected:**
- §3 Component Diagram — now shows ArgoCD, ghcr.io, and GitHub repo
- §9.8 GitOps CI/CD Pipeline — new section with pipeline flow, GitHub Actions workflow, ArgoCD config, and deployment sequence diagram
- §18 Technology Decisions — added GitHub Actions, ghcr.io, ArgoCD with rationale
- K8s manifest image refs updated to `ghcr.io/lekhasy/vut/<service>:latest`
