# CLAUDE.md

> Auto-loaded context for Claude Code sessions on the platform repo.
> Keep this file concise — it's read on every session.

## Repo in three lines

This is the **platform** repo for a personal K3s cluster running on a WSL
machine. It hosts shared infrastructure (ArgoCD, Infisical Operator, an
observability stack) and an App-of-Apps root that manages one or more
app repositories. Velucid lives in its own repo (`lekhasy/Velucid`); this
repo no longer contains app code.

**Stack:** K3s · ArgoCD (App-of-Apps) · Infisical Operator · Stakater
Reloader · Grafana · Loki · Tempo · Prometheus · OpenTelemetry Collector.

## Day-to-day commands

| Task                                  | Command                                                  |
| ------------------------------------- | -------------------------------------------------------- |
| Boot the cluster + ArgoCD + apps      | `sudo ./infrastructure/scripts/k3s-start.sh`             |
| Tear the cluster down                 | `sudo ./infrastructure/scripts/k3s-wipe.sh`              |
| Inspect ArgoCD state                  | `sudo k3s kubectl get applications -n argocd`            |
| Inspect all pods                      | `sudo k3s kubectl get pods -A`                           |

## Repo layout

```
infrastructure/
  k8s/
    argocd/
      appproject-platform.yaml       AppProject: this repo → platform-* namespaces
      appproject-apps.yaml           AppProject: any app repo → its own namespace
      root-app.yaml                  App-of-Apps root, watches argocd/apps/
      apps/
        platform-observability.yaml  Leaf: this repo → platform-observability ns
        velucid.yaml                 Leaf: lekhasy/Velucid → velucid ns
        <future-app>.yaml            Add new apps here
      ingress.yaml                   argo.velucid.app
    observability/                   Grafana, Loki, Tempo, Prometheus, otel-collector
      namespace.yaml
      grafana/  loki/  tempo/  prometheus/  otel-collector/
      secrets/platform-infisical-secrets.yaml
  scripts/
    k3s-start.sh                     Boots the cluster + bootstraps ArgoCD
    k3s-wipe.sh                      Tears the cluster down
  README.md                          Architecture + onboarding notes
```

## Architectural guardrails

- **Single k3s cluster, many apps.** Each app is its own ArgoCD leaf
  Application, in its own namespace, with its own Infisical project.
- **Shared observability.** Prometheus / Loki / Tempo / Grafana /
  otel-collector run in `platform-observability` and are consumed by
  every app via cross-namespace DNS (`<svc>.platform-observability.svc.cluster.local`).
- **Per-app Infisical.** Each app's `InfisicalSecret` CR references an
  `infisical-machine-identity` secret **in its own namespace** with
  credentials scoped to that app's Infisical project only.
- **Per-app DBs.** Each app deploys its own Postgres / KurrentDB / Redis
  StatefulSets in its own namespace. No DB sharing between apps.
- **ArgoCD AppProject restrictions.** `platform` only sources from
  this repo and only deploys into `platform-*` namespaces. `apps`
  deploys into one app namespace per app — a misconfigured app cannot
  accidentally deploy into another app's namespace.

## Onboarding a new app

1. Create the app repo (mirror the `velucid` repo's layout).
2. Add a leaf Application at
   `infrastructure/k8s/argocd/apps/<app-name>.yaml` (copy `velucid.yaml`,
   change `repoURL`, `path`, `destination.namespace`).
3. Add the repo URL and destination namespace to
   `infrastructure/k8s/argocd/appproject-apps.yaml`.
4. Create the app's Infisical project + machine identity, then run
   `sudo ./infrastructure/scripts/k3s-start.sh` (it will prompt for the
   new machine identity credentials).
5. Push to the app repo. ArgoCD picks it up.

## Day-to-day reminder

- The Velucid app lives at `lekhasy/Velucid`, not here.
- The observability stack uses the `platform` Infisical project.
- The grafana admin password is sourced from
  `platform-infisical-secrets.yaml` in the `platform-observability`
  namespace.