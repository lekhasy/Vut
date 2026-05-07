# Task 16: CI/CD Pipeline & Dockerfiles

| Field | Value |
|-------|-------|
| **Developer** | Backend |
| **Work Order** | 16 |
| **Priority** | P2 |
| **Estimated Effort** | 1.5 days |

## Description

Set up Dockerfiles for all .NET services and the Astro.js frontend, and create a CI/CD pipeline (GitHub Actions) that builds, tests, and pushes Docker images. The pipeline should run on pull requests and merges to main.

## Architecture Reference

- Architecture doc Section 2 (Component Diagram - all deployments)
- Architecture doc Section 3.5 (Actor Service Deployment)
- Architecture doc Section 3.6 (Frontend Deployment)

## Technical Requirements

### Dockerfiles

#### Actor Service Dockerfile (`src/Vut.ActorService/Dockerfile`)
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Vut.ActorService/Vut.ActorService.csproj", "Vut.ActorService/"]
COPY ["Vut.Shared/Vut.Shared.csproj", "Vut.Shared/"]
RUN dotnet restore "Vut.ActorService/Vut.ActorService.csproj"
COPY . .
RUN dotnet publish "Vut.ActorService/Vut.ActorService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENTRYPOINT ["dotnet", "Vut.ActorService.dll"]
```

#### Projector Service Dockerfile (`src/Vut.ProjectorService/Dockerfile`)
- Same pattern as Actor Service, different entry point.
- No exposed ports (background worker).

#### Read Model API Dockerfile (`src/Vut.ReadModelApi/Dockerfile`)
- Same pattern, expose port 5001.

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
- Redpanda (single broker).
- PostgreSQL.
- Actor Service (with hot reload or rebuild).
- Projector Service.
- Read Model API.
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

  redpanda:
    image: redpandadata/redpanda:latest
    ports: ["9092:9092"]
    command: redpanda start --smp 1 --memory 512M --overprovisioned --kafka-addr internal://0.0.0.0:9092,external://0.0.0.0:9092

  postgres:
    image: postgres:16
    ports: ["5432:5432"]
    environment:
      POSTGRES_DB: vut_readmodel
      POSTGRES_USER: vut
      POSTGRES_PASSWORD: vut_dev_password

  actor-service:
    build: ./src/Vut.ActorService
    ports: ["5000:5000"]
    depends_on: [kurrentdb, redpanda]
    environment:
      KurrentDB__ConnectionString: "esdb://kurrentdb:2113?tls=false"
      Redpanda__BootstrapServers: "redpanda:9092"

  projector-service:
    build: ./src/Vut.ProjectorService
    depends_on: [kurrentdb, postgres]
    environment:
      KurrentDB__ConnectionString: "esdb://kurrentdb:2113?tls=false"
      Postgres__ConnectionString: "Host=postgres;Database=vut_readmodel;Username=vut;Password=vut_dev_password"

  readmodel-api:
    build: ./src/Vut.ReadModelApi
    ports: ["5001:5001"]
    depends_on: [postgres]
    environment:
      Postgres__ConnectionString: "Host=postgres;Database=vut_readmodel;Username=vut;Password=vut_dev_password"

  frontend:
    build: ./frontend
    ports: ["3000:3000"]
    depends_on: [actor-service, readmodel-api]
    environment:
      ACTOR_SERVICE_URL: "http://actor-service:5000"
      READMODEL_URL: "http://readmodel-api:5001"
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
          docker build -t vut/actor-service:${{ github.sha }} ./src/Vut.ActorService
          docker build -t vut/projector-service:${{ github.sha }} ./src/Vut.ProjectorService
          docker build -t vut/readmodel-api:${{ github.sha }} ./src/Vut.ReadModelApi

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
  Vut.ActorService/Dockerfile
  Vut.ProjectorService/Dockerfile
  Vut.ReadModelApi/Dockerfile
  Vut.ReadModel.Migrations/Dockerfile
frontend/
  Dockerfile
```

## Acceptance Criteria

- [ ] All 5 Dockerfiles build successfully.
- [ ] `docker-compose up` starts all services and they can communicate.
- [ ] Actor service connects to KurrentDB inside Docker Compose.
- [ ] Projector service connects to KurrentDB and PostgreSQL inside Docker Compose (no Redpanda dependency).
- [ ] Read Model API connects to PostgreSQL inside Docker Compose.
- [ ] Frontend connects to actor service and read model API inside Docker Compose.
- [ ] GitHub Actions CI pipeline runs on pull requests.
- [ ] CI pipeline builds and tests .NET services.
- [ ] CI pipeline builds and tests the frontend.
- [ ] Docker images are built with correct tags on main branch merges.

## Dependencies

- Tasks 04-08 (Backend services) -- Dockerfiles reference the actual service projects.
- Task 09 (Frontend setup) -- Frontend Dockerfile references the Astro project.
- Can create the Dockerfiles and compose file in parallel with development, then validate once services are complete.

## Notes

- Docker Compose is the recommended local development setup. It is faster and lighter than running Kubernetes locally.
- For Kubernetes deployment, the CI pipeline should push images to a container registry (Docker Hub, GitHub Container Registry, or ECR). The registry configuration can be added when deployment targets are finalized.
- Consider adding a `docker-compose.dev.yml` override that mounts source directories for hot reload during development.
- The CI pipeline does NOT deploy to Kubernetes in Epic 1. Deployment is manual (`kubectl apply`) until CD is set up.
