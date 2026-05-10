# Task 16: CI/CD Pipeline & Dockerfiles

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 16 |
| **Priority** | P2 |
| **Estimated Effort** | 1.5 days |

## Description

Set up Dockerfiles for all .NET services (Orleans Silo, Projector Service, Read Model Migrations) and the Astro.js frontend, and create a CI/CD pipeline (GitHub Actions) that builds, tests, and pushes Docker images. The pipeline should run on pull requests and merges to main. The ASP.NET Core API is co-hosted inside the Orleans silo — no separate Read Model API service or Dockerfile is needed.

## Architecture Reference

- Architecture doc Section 3 (Component Diagram - all deployments)
- Architecture doc Section 9.4 (Orleans Silo Deployment)
- Architecture doc Section 9.5 (Frontend Deployment)
- Architecture doc Section 4 (Cluster Topology & Placement)

## Technical Requirements

### Dockerfiles

#### Orleans Silo Dockerfile (`src/Vut.Silo/Dockerfile`)
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Vut.Silo/Vut.Silo.csproj", "Vut.Silo/"]
COPY ["Vut.Shared/Vut.Shared.csproj", "Vut.Shared/"]
RUN dotnet restore "Vut.Silo/Vut.Silo.csproj"
COPY . .
RUN dotnet publish "Vut.Silo/Vut.Silo.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000 11111 30000
ENTRYPOINT ["dotnet", "Vut.Silo.dll"]
```

#### Projector Service Dockerfile (`src/Vut.ProjectorService/Dockerfile`)
- Same pattern as the Silo Dockerfile, different entry point.
- No exposed ports (background worker).

#### Read Model Migrations Dockerfile (`src/Vut.ReadModel.Migrations/Dockerfile`)
- Console app that runs migrations and exits.
- Entry point: `dotnet Vut.ReadModel.Migrations.dll`.

#### Frontend Dockerfile (`frontend/Dockerfile`)
```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM node:20-alpine
WORKDIR /app
COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/package*.json ./
EXPOSE 3000
CMD ["node", "./dist/server/entry.mjs"]
```
- Note: Astro SSR requires Node.js in production. The build output is a Node.js server.
- Alternatively, use `@astrojs/node` adapter for SSR.

### Docker Compose (Local Development)
Create `docker-compose.yml` for local development that starts:
- KurrentDB (single node).
- PostgreSQL (read model + Orleans clustering).
- Orleans Silo (with hot reload or rebuild).
- Projector Service.
- Frontend (with hot reload).

```yaml
# docker-compose.yml (simplified)
version: '3.8'
services:
  kurrentdb:
    image: kurrentdb/kurrentdb:latest
    ports: ["2113:2113", "1113:1113"]
    environment:
      EVENTSTORE_DEV: "true"
      EVENTSTORE_RUN_PROJECTIONS: "None"

  postgres:
    image: postgres:16
    ports: ["5432:5432"]
    environment:
      POSTGRES_DB: vut_readmodel
      POSTGRES_USER: vut
      POSTGRES_PASSWORD: vut_dev_password

  silo:
    build: ./src/Vut.Silo
    ports: ["5000:5000", "11111:11111", "30000:30000"]
    depends_on: [kurrentdb, postgres]
    environment:
      KurrentDb__ConnectionString: "esdb://kurrentdb:2113?tls=false"
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=vut_readmodel;Username=vut;Password=vut_dev_password"
      Orleans__ClusterId: "vut-cluster"
      Orleans__ServiceId: "vut"

  projector-service:
    build: ./src/Vut.ProjectorService
    depends_on: [kurrentdb, postgres]
    environment:
      KurrentDb__ConnectionString: "esdb://kurrentdb:2113?tls=false"
      ConnectionStrings__PostgreSQL: "Host=postgres;Database=vut_readmodel;Username=vut;Password=vut_dev_password"

  frontend:
    build: ./frontend
    ports: ["3000:3000"]
    depends_on: [silo]
    environment:
      SILO_API_URL: "http://silo:5000"
```

### GitHub Actions CI Pipeline (`.github/workflows/ci.yml`)
```yaml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  backend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal
      - name: Build & push Docker images
        if: github.ref == 'refs/heads/main'
        run: |
          docker build -t vut/silo:${{ github.sha }} ./src/Vut.Silo
          docker build -t vut/projector-service:${{ github.sha }} ./src/Vut.ProjectorService

  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - working-directory: frontend
        run: npm ci
      - working-directory: frontend
        run: npm run build
      - working-directory: frontend
        run: npm test
      - name: Build & push Docker image
        if: github.ref == 'refs/heads/main'
        run: docker build -t vut/frontend:${{ github.sha }} ./frontend
```

### File Structure
```
docker-compose.yml
.github/
  workflows/
    ci.yml
src/
  Vut.Silo/Dockerfile
  Vut.ProjectorService/Dockerfile
  Vut.ReadModel.Migrations/Dockerfile
frontend/
  Dockerfile
```

## Acceptance Criteria

- [ ] All 4 Dockerfiles build successfully.
- [ ] `docker-compose up` starts all services and they can communicate.
- [ ] Orleans silo connects to KurrentDB and PostgreSQL inside Docker Compose.
- [ ] Projector service connects to KurrentDB and PostgreSQL inside Docker Compose.
- [ ] Frontend connects to the silo API inside Docker Compose.
- [ ] GitHub Actions CI pipeline runs on pull requests.
- [ ] CI pipeline builds and tests .NET services.
- [ ] CI pipeline builds and tests the frontend.
- [ ] Docker images are built with correct tags on main branch merges.

## Dependencies

- Tasks 04-08 (Backend services) -- Dockerfiles reference the actual service projects.
- Task 09 (Frontend setup) -- Frontend Dockerfile references the Astro project.
- Can create the Dockerfiles and compose file in parallel with development, then validate once services are complete.

## Notes

- Docker Compose is the recommended local development setup. It is faster and lighter than running the full K3s stack for iterative development.
- For K3s deployment, images can be imported directly via `k3s ctr images import <image>.tar` or pushed to a local registry. The CI pipeline should build images tagged by commit SHA.
- Consider adding a `docker-compose.dev.yml` override that mounts source directories for hot reload during development.
- The CI pipeline does NOT deploy to K3s in Epic 1. Deployment is manual (`k3s kubectl apply`) until CD is set up.
- The deployment target is a single dev machine running K3s with Cloudflare Tunnel for internet access via `vut.app`. No cloud infrastructure is needed.
