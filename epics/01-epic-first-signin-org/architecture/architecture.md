# Epic 1 Architecture: First Sign-In & Organization

## 1. System Context

Epic 1 establishes the entire infrastructure footprint of Vut. Every subsequent epic builds on the actors, streams, projections, and deployment infrastructure defined here.

```mermaid
graph TB
    subgraph External
        User["User (Browser)"]
        GH["GitHub OAuth"]
        Auth0["Auth0"]
        SMTP["Email Service (SMTP)"]
    end

    subgraph Vut Platform
        subgraph Frontend["Astro.js SPA"]
            UI["UI Shell<br/>Sidebar / Routing"]
        end

        subgraph API["API Layer"]
            BFF["BFF / API Gateway<br/>(Astro.js SSR)"]
        end

        subgraph Backend[".NET Proto.Actor Backend"]
            UA["User Actor"]
            OA["Organization Actor"]
        end

        subgraph EventStore["KurrentDB"]
            US["user-{userId}"]
            OS["organization-{orgId}"]
        end

        subgraph Messaging["Redpanda"]
            RT["vut.user-events"]
            RO["vut.org-events"]
        end

        subgraph ReadModel["PostgreSQL"]
            UP["user_projection"]
            OP["org_projection"]
            OMP["org_member_projection"]
        end

        subgraph Projectors["Projector Services"]
            PU["User Projector"]
            PO["Org Projector"]
        end
    end

    User --> UI
    UI --> BFF
    BFF --> Auth0
    Auth0 --> GH
    BFF --> UA
    BFF --> OA
    UA --> US
    OA --> OS
    US --> RT
    OS --> RO
    RT --> PU
    RO --> PO
    PU --> UP
    PO --> OP
    PO --> OMP
    OA --> SMTP
```

## 2. Component Diagram

```mermaid
graph TB
    subgraph Kubernetes Cluster
        subgraph "Namespace: vut"
            ING["Ingress Controller<br/>(NGINX / Traefik)"]

            subgraph "Deployment: vut-frontend"
                FE["Astro.js SSR Pods<br/>(BFF + Static)"]
            end

            subgraph "Deployment: vut-actor-service"
                AS["Proto.Actor Pods<br/>(User + Org Actors)"]
            end

            subgraph "Deployment: vut-projector-service"
                PS["Projector Pods<br/>(User + Org Projectors)"]
            end

            subgraph "StatefulSet: vut-kurrentdb"
                KDB["KurrentDB<br/>(3-node cluster)"]
            end

            subgraph "StatefulSet: vut-redpanda"
                RP["Redpanda<br/>(3-broker cluster)"]
            end

            subgraph "StatefulSet: vut-postgresql"
                PG["PostgreSQL<br/>(Primary + Replica)"]
            end
        end
    end

    ING --> FE
    FE --> AS
    AS --> KDB
    AS --> RP
    RP --> PS
    PS --> PG
```

## 3. Infrastructure Setup (Kubernetes)

### 3.1 Namespace and Resource Quotas

All Vut services run in the `vut` namespace.

```yaml
# k8s/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: vut
  labels:
    app.kubernetes.io/part-of: vut
```

### 3.2 KurrentDB StatefulSet

```yaml
# k8s/kurrentdb/statefulset.yaml (sketch)
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: vut-kurrentdb
  namespace: vut
spec:
  replicas: 3
  serviceName: vut-kurrentdb
  selector:
    matchLabels:
      app: vut-kurrentdb
  template:
    spec:
      containers:
        - name: kurrentdb
          image: kurrentdb/kurrentdb:latest
          ports:
            - containerPort: 2113  # HTTP/API
            - containerPort: 1113  # TCP
          env:
            - name: EVENTSTORE_CLUSTER_SIZE
              value: "3"
            - name: EVENTSTORE_RUN_PROJECTIONS
              value: "None"  # We project in .NET consumers
            - name: EVENTSTORE_DB
              value: "/data/db"
          volumeMounts:
            - name: data
              mountPath: /data
  volumeClaimTemplates:
    - metadata:
        name: data
      spec:
        accessModes: ["ReadWriteOnce"]
        resources:
          requests:
            storage: 10Gi
```

### 3.3 Redpanda StatefulSet

```yaml
# k8s/redpanda/statefulset.yaml (sketch)
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: vut-redpanda
  namespace: vut
spec:
  replicas: 3
  serviceName: vut-redpanda
  selector:
    matchLabels:
      app: vut-redpanda
  template:
    spec:
      containers:
        - name: redpanda
          image: redpandadata/redpanda:latest
          ports:
            - containerPort: 9092  # Kafka API
            - containerPort: 9644  # Admin API
          command:
            - redpanda
            - start
            - --smp 1
            - --memory 512M
            - --overprovisioned
            - --kafka-addr internal://0.0.0.0:9092
```

### 3.4 PostgreSQL StatefulSet

```yaml
# k8s/postgresql/statefulset.yaml (sketch)
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: vut-postgresql
  namespace: vut
spec:
  replicas: 1  # Primary; read replica added later
  serviceName: vut-postgresql
  selector:
    matchLabels:
      app: vut-postgresql
  template:
    spec:
      containers:
        - name: postgresql
          image: postgres:16
          ports:
            - containerPort: 5432
          env:
            - name: POSTGRES_DB
              value: vut_readmodel
            - name: POSTGRES_USER
              valueFrom:
                secretKeyRef:
                  name: vut-postgresql-secret
                  key: username
            - name: POSTGRES_PASSWORD
              valueFrom:
                secretKeyRef:
                  name: vut-postgresql-secret
                  key: password
```

### 3.5 Actor Service Deployment

```yaml
# k8s/actor-service/deployment.yaml (sketch)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: vut-actor-service
  namespace: vut
spec:
  replicas: 2
  selector:
    matchLabels:
      app: vut-actor-service
  template:
    spec:
      containers:
        - name: actor-service
          image: vut/actor-service:latest
          ports:
            - containerPort: 5000  # gRPC
          env:
            - name: KurrentDB__ConnectionString
              value: "esdb://vut-kurrentdb:2113?tls=false"
            - name: Redpanda__BootstrapServers
              value: "vut-redpanda:9092"
            - name: Auth0__Domain
              valueFrom:
                secretKeyRef:
                  name: vut-auth0-secret
                  key: domain
            - name: Auth0__Audience
              valueFrom:
                secretKeyRef:
                  name: vut-auth0-secret
                  key: audience
```

### 3.6 Frontend Deployment (Astro.js BFF)

```yaml
# k8s/frontend/deployment.yaml (sketch)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: vut-frontend
  namespace: vut
spec:
  replicas: 2
  selector:
    matchLabels:
      app: vut-frontend
  template:
    spec:
      containers:
        - name: frontend
          image: vut/frontend:latest
          ports:
            - containerPort: 3000
          env:
            - name: ACTOR_SERVICE_URL
              value: "http://vut-actor-service:5000"
            - name: READMODEL_URL
              value: "http://vut-readmodel-api:5001"
            - name: AUTH0_DOMAIN
              valueFrom:
                secretKeyRef:
                  name: vut-auth0-secret
                  key: domain
            - name: AUTH0_CLIENT_ID
              valueFrom:
                secretKeyRef:
                  name: vut-auth0-secret
                  key: client-id
            - name: AUTH0_CLIENT_SECRET
              valueFrom:
                secretKeyRef:
                  name: vut-auth0-secret
                  key: client-secret
```

## 4. Auth0 Integration Architecture

### 4.1 Auth0 Configuration

```mermaid
sequenceDiagram
    participant U as User Browser
    participant FE as Astro.js BFF
    participant A as Auth0
    participant GH as GitHub OAuth
    participant AS as Actor Service
    participant K as KurrentDB

    U->>FE: GET /login
    FE->>U: Redirect to Auth0 /authorize
    U->>A: Authorize request (connection=github)
    A->>GH: GitHub OAuth redirect
    GH->>A: Authorization code
    A->>FE: Callback with authorization code
    FE->>A: Exchange code for tokens
    A->>FE: ID token + access token
    FE->>FE: Validate JWT, extract claims
    FE->>FE: Check if user exists (via read model)
    alt New User
        FE->>AS: CreateUserCommand(providerId, displayName, avatarUrl)
        AS->>K: Append UserCreated event to user-{userId}
        K-->>AS: OK
        AS-->>FE: userId
    else Existing User
        FE->>FE: User found, continue
    end
    FE->>U: Set session cookie, redirect to dashboard
```

### 4.2 JWT Claims Used

Auth0 token includes these claims that Vut extracts:
- `sub`: The Auth0 user ID (format: `github|12345678`)
- `nickname`: GitHub username
- `name`: Display name
- `picture`: Avatar URL
- `email`: Email address (if available from GitHub scope)

### 4.3 Auth Middleware

The BFF validates the JWT on every request:
1. Extract Bearer token or session cookie
2. Validate JWT signature against Auth0 JWKS
3. Extract `sub` claim as the Vut `providerId`
4. Look up the Vut `userId` from the read model using `providerId`
5. Attach `userId` and `providerId` to the request context as the `actorId` for all commands

## 5. Actor Model Design

### 5.1 Proto.Actor Hierarchy

```mermaid
graph TD
    Root["Root Context"]
    Root --> UAManager["User Actor Manager"]
    Root --> OAManager["Organization Actor Manager"]
    UAManager --> UA1["UserActor<br/>user-{userId}"]
    UAManager --> UA2["UserActor<br/>user-{userId}"]
    OAManager --> OA1["OrgActor<br/>org-{orgId}"]
    OAManager --> OA2["OrgActor<br/>org-{orgId}"]
```

The Actor Managers are responsible for creating and locating actor instances. When a command arrives for a specific aggregate, the manager either returns the existing PID or spawns a new actor that rehydrates from KurrentDB.

### 5.2 Actor Lifecycle

```mermaid
stateDiagram-v2
    [*] --> DoesNotExist
    DoesNotExist --> Loading: Command received<br/>(spawn actor)
    Loading --> Active: Events replayed from KurrentDB
    Active --> Idle: No messages for N minutes
    Idle --> Active: New command received
    Idle --> Passivated: Passivation timeout
    Passivated --> Loading: Command received<br/>(rehydrate)
    Passivated --> [*]
```

### 5.3 User Actor

**Stream:** `user-{userId}`
**Responsibility:** Manages the user aggregate root. Creates user on first login, handles profile updates.

```
Commands:
  CreateUser(providerId, displayName, avatarUrl) -> userId
  UpdateProfile(displayName, avatarUrl)

Events:
  UserCreated(userId, providerId, displayName, avatarUrl, actorId, timestamp)
  UserProfileUpdated(userId, displayName, avatarUrl, actorId, timestamp)

State:
  userId: UUID
  providerId: string (Auth0 subject)
  displayName: string
  avatarUrl: string
```

**Validation Rules:**
- `CreateUser` is idempotent: if the user already exists, return the existing userId without emitting a duplicate event.
- `UpdateProfile` only emits `UserProfileUpdated` if displayName or avatarUrl actually changed.

### 5.4 Organization Actor

**Stream:** `organization-{orgId}`
**Responsibility:** Manages the organization aggregate root. Handles creation, member management, and role changes.

```
Commands:
  CreateOrganization(name, ownerId) -> orgId
  RenameOrganization(newName)
  InviteMember(inviteeEmail, role)
  AcceptInvitation(userId, email)
  DeclineInvitation(userId, email)
  RemoveMember(userId)
  ChangeMemberRole(userId, newRole)

Events:
  OrganizationCreated(orgId, name, actorId, timestamp)
  OrganizationRenamed(orgId, newName, actorId, timestamp)
  MemberInvited(orgId, inviteeEmail, role, actorId, timestamp)
  MemberJoined(orgId, userId, actorId, timestamp)
  MemberRemoved(orgId, userId, actorId, timestamp)
  MemberRoleChanged(orgId, userId, oldRole, newRole, actorId, timestamp)
  OrganizationDeleted(orgId, actorId, timestamp)

State:
  orgId: UUID
  name: string
  members: Map<userId, MemberEntry>
  invitations: Map<email, InvitationEntry>
  isDeleted: bool

MemberEntry:
  userId: UUID
  role: Owner | Member
  joinedAt: timestamp

InvitationEntry:
  email: string
  role: Owner | Member
  invitedAt: timestamp
  status: Pending | Accepted | Declined
```

**Validation Rules:**
- `CreateOrganization`: name must be non-empty, creator is automatically added as Owner.
- `RenameOrganization`: only Owners can rename.
- `InviteMember`: only Owners can invite.
- `AcceptInvitation`: the email used in the invitation must match the user's verified email from Auth0, or the user must be the one the invitation was sent to.
- `RemoveMember`: only Owners can remove. Cannot remove the last Owner.
- `ChangeMemberRole`: only Owners can change roles. Cannot demote the last Owner.
- `OrganizationDeleted` event is defined but the UI action is deferred (not required in Epic 1).

## 6. Event Stream Design

### 6.1 Stream Naming Convention

| Aggregate | Stream ID Format | Example |
|-----------|-----------------|---------|
| User | `user-{userId}` | `user-a1b2c3d4-e5f6-7890-abcd-ef1234567890` |
| Organization | `organization-{orgId}` | `organization-f7e6d5c4-b3a2-1098-7654-321fedcba098` |
| Product | `product-{productId}` | (Epic 2) |
| Task | `task-{taskId}` | (Epic 3) |

### 6.2 Event Envelope

Every event is wrapped in a consistent envelope:

```json
{
  "eventId": "uuid-v4",
  "eventType": "UserCreated",
  "streamId": "user-a1b2c3d4-...",
  "eventNumber": 1,
  "timestamp": "2026-05-05T14:30:00.000Z",
  "actorId": "user-a1b2c3d4-...",
  "payload": {
    "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "providerId": "github|12345678",
    "displayName": "Jane Developer",
    "avatarUrl": "https://avatars.githubusercontent.com/u/12345678"
  }
}
```

### 6.3 Event Serialization

Events are serialized as JSON in KurrentDB. The .NET backend uses `System.Text.Json` with camelCase naming. Each event type maps to a concrete CLR type via a discriminator (`eventType` field).

### 6.4 Redpanda Topic Design

| Topic | Key | Value | Partitions | Purpose |
|-------|-----|-------|------------|---------|
| `vut.user-events` | userId (string) | Event envelope (JSON) | 3 | All User stream events |
| `vut.org-events` | orgId (string) | Event envelope (JSON) | 6 | All Organization stream events |
| `vut.product-events` | productId (string) | Event envelope (JSON) | 6 | (Epic 2) All Product stream events |
| `vut.task-events` | taskId (string) | Event envelope (JSON) | 12 | (Epic 3) All Task stream events |

Partitioning by aggregate ID ensures ordering per entity. The partition count is chosen upfront based on expected throughput; KurrentDB appends events then publishes to Redpanda atomically via a background process in the actor service.

## 7. Read Model (PostgreSQL Projections)

### 7.1 Projection Views

```sql
-- User projection
CREATE TABLE user_projection (
    user_id       UUID PRIMARY KEY,
    provider_id   TEXT NOT NULL UNIQUE,
    display_name  TEXT NOT NULL,
    avatar_url    TEXT,
    created_at    TIMESTAMPTZ NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL
);

-- Organization projection
CREATE TABLE org_projection (
    org_id        UUID PRIMARY KEY,
    name          TEXT NOT NULL,
    is_deleted    BOOLEAN NOT NULL DEFAULT FALSE,
    created_at    TIMESTAMPTZ NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL
);

-- Organization member projection (derived from org stream events)
CREATE TABLE org_member_projection (
    org_id        UUID NOT NULL REFERENCES org_projection(org_id),
    user_id       UUID NOT NULL REFERENCES user_projection(user_id),
    role          TEXT NOT NULL CHECK (role IN ('Owner', 'Member')),
    joined_at     TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (org_id, user_id)
);

-- Organization invitation projection (derived from org stream events)
CREATE TABLE org_invitation_projection (
    org_id            UUID NOT NULL REFERENCES org_projection(org_id),
    email             TEXT NOT NULL,
    role              TEXT NOT NULL CHECK (role IN ('Owner', 'Member')),
    status            TEXT NOT NULL CHECK (status IN ('Pending', 'Accepted', 'Declined')),
    invited_at        TIMESTAMPTZ NOT NULL,
    user_id           UUID,  -- NULL until the invitee signs in
    PRIMARY KEY (org_id, email)
);

-- User organization membership (reverse index for "my orgs" queries)
CREATE TABLE user_org_projection (
    user_id       UUID NOT NULL REFERENCES user_projection(user_id),
    org_id        UUID NOT NULL REFERENCES org_projection(org_id),
    role          TEXT NOT NULL,
    PRIMARY KEY (user_id, org_id)
);

-- Indexes
CREATE INDEX idx_user_projection_provider ON user_projection(provider_id);
CREATE INDEX idx_org_member_projection_user ON org_member_projection(user_id);
CREATE INDEX idx_org_invitation_projection_email ON org_invitation_projection(email, status);
```

### 7.2 Projector Service Design

The projector service is a .NET worker that subscribes to Redpanda consumer groups and updates PostgreSQL projections.

```mermaid
graph LR
    subgraph Redpanda
        T1["vut.user-events"]
        T2["vut.org-events"]
    end

    subgraph ProjectorService["Projector Service (.NET Worker)"]
        CG1["Consumer Group: vut-projector-user"]
        CG2["Consumer Group: vut-projector-org"]
        PU["User Projector Handler"]
        PO["Org Projector Handler"]
    end

    subgraph PostgreSQL
        UP["user_projection"]
        ORP["org_projection"]
        OMP["org_member_projection"]
        OIP["org_invitation_projection"]
        UOP["user_org_projection"]
    end

    T1 --> CG1 --> PU --> UP
    T2 --> CG2 --> PO --> ORP
    PO --> OMP
    PO --> OIP
    PO --> UOP
```

**Projector Idempotency:** Each projector tracks the last consumed offset per partition in a `projection_checkpoint` table. On restart, it resumes from the last checkpoint. The projector handles events idempotently -- re-processing an event produces the same result.

```sql
CREATE TABLE projection_checkpoint (
    projector_name   TEXT NOT NULL,
    topic            TEXT NOT NULL,
    partition_id     INT NOT NULL,
    last_offset      BIGINT NOT NULL,
    updated_at       TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (projector_name, topic, partition_id)
);
```

## 8. Key Workflow Sequence Diagrams

### 8.1 First-Time User Sign-In

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant Auth0
    participant GitHub
    participant RM as Read Model API
    participant AS as Actor Service
    participant K as KurrentDB
    participant RP as Redpanda
    participant PJ as Projector
    participant PG as PostgreSQL

    User->>Browser: Click "Sign in with GitHub"
    Browser->>BFF: GET /auth/login
    BFF->>Auth0: Redirect to /authorize?connection=github
    Auth0->>GitHub: OAuth authorize
    User->>GitHub: Approve
    GitHub->>Auth0: Authorization code
    Auth0->>BFF: Callback /auth/callback?code=xxx
    BFF->>Auth0: Exchange code for tokens
    Auth0->>BFF: ID token (sub, name, picture)
    BFF->>BFF: Validate JWT, extract sub = providerId
    BFF->>RM: GET /api/users/by-provider/{providerId}
    RM->>PG: SELECT * FROM user_projection WHERE provider_id = ?
    PG-->>RM: Empty result
    RM-->>BFF: 404 Not Found

    Note over BFF: New user -- create via actor

    BFF->>AS: CreateUserCommand(providerId, displayName, avatarUrl)
    AS->>K: Append UserCreated to stream user-{userId}
    K->>RP: Publish event to vut.user-events
    K-->>AS: Append confirmation
    AS-->>BFF: userId

    RP->>PJ: Consume UserCreated event
    PJ->>PG: INSERT INTO user_projection

    BFF->>BFF: Create session (userId, providerId)
    BFF->>Browser: Set session cookie, redirect to /dashboard
    Browser->>User: Dashboard with empty state
```

### 8.2 Returning User Sign-In

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant Auth0
    participant RM as Read Model API
    participant PG as PostgreSQL

    User->>Browser: Click "Sign in with GitHub"
    Browser->>BFF: GET /auth/login
    BFF->>Auth0: Redirect to /authorize
    Auth0->>BFF: Callback with ID token
    BFF->>BFF: Validate JWT, extract providerId
    BFF->>RM: GET /api/users/by-provider/{providerId}
    RM->>PG: SELECT * FROM user_projection WHERE provider_id = ?
    PG-->>RM: User row found
    RM-->>BFF: { userId, displayName, avatarUrl }
    BFF->>BFF: Create session
    BFF->>RM: GET /api/users/{userId}/organizations
    RM->>PG: SELECT * FROM user_org_projection WHERE user_id = ?
    PG-->>RM: List of org memberships
    RM-->>BFF: [{ orgId, name, role }, ...]
    BFF->>Browser: Set session cookie, redirect to /dashboard
```

### 8.3 Create Organization

```mermaid
sequenceDiagram
    actor Owner
    participant Browser
    participant BFF as Astro.js BFF
    participant AS as Actor Service
    participant K as KurrentDB
    participant RP as Redpanda
    participant PJ as Org Projector
    participant PG as PostgreSQL

    Owner->>Browser: Fill "New Organization" form, submit
    Browser->>BFF: POST /api/organizations { name }
    BFF->>BFF: Validate session, extract userId as actorId
    BFF->>AS: CreateOrganizationCommand(name, ownerId=userId)
    AS->>AS: Validate name non-empty
    AS->>AS: Generate orgId (UUID)
    AS->>K: Append OrganizationCreated to organization-{orgId}
    Note over K: Event includes creator as first member with role=Owner

    K-->>AS: OK
    AS->>K: Append MemberJoined to organization-{orgId}
    K-->>AS: OK

    K->>RP: Publish events to vut.org-events
    AS-->>BFF: { orgId, name }

    RP->>PJ: Consume OrganizationCreated
    PJ->>PG: INSERT INTO org_projection
    RP->>PJ: Consume MemberJoined
    PJ->>PG: INSERT INTO org_member_projection
    PJ->>PG: INSERT INTO user_org_projection

    BFF->>Browser: 201 Created { orgId }
    Browser->>Owner: Redirect to org dashboard
```

### 8.4 Invite and Accept Member

```mermaid
sequenceDiagram
    actor Owner
    actor Invitee
    participant Browser1 as Owner Browser
    participant Browser2 as Invitee Browser
    participant BFF as Astro.js BFF
    participant AS as Actor Service
    participant K as KurrentDB
    participant RP as Redpanda
    participant PJ as Org Projector
    participant PG as PostgreSQL
    participant SMTP as Email Service

    Owner->>Browser1: Invite member (email)
    Browser1->>BFF: POST /api/orgs/{orgId}/members/invite { email }
    BFF->>AS: InviteMemberCommand(orgId, email, role="Member")
    AS->>AS: Verify caller is Owner
    AS->>K: Append MemberInvited to organization-{orgId}
    K-->>AS: OK
    AS-->>BFF: OK
    K->>RP: Publish MemberInvited

    RP->>PJ: Consume MemberInvited
    PJ->>PG: INSERT INTO org_invitation_projection

    BFF->>SMTP: Send invitation email

    Note over Invitee: Invitee receives email, clicks link

    Invitee->>Browser2: Sign in via GitHub (if new user, goes through first-time flow)
    Browser2->>BFF: GET /api/invitations?userId={userId}
    BFF->>PG: SELECT * FROM org_invitation_projection WHERE email = ? AND status = 'Pending'
    PG-->>BFF: List of pending invitations
    BFF-->>Browser2: [{ orgId, orgName, role }]

    Invitee->>Browser2: Accept invitation
    Browser2->>BFF: POST /api/orgs/{orgId}/members/accept
    BFF->>AS: AcceptInvitationCommand(orgId, userId, email)
    AS->>AS: Verify pending invitation exists for this email
    AS->>K: Append MemberJoined to organization-{orgId}
    K-->>AS: OK
    AS-->>BFF: OK
    K->>RP: Publish MemberJoined

    RP->>PJ: Consume MemberJoined
    PJ->>PG: INSERT INTO org_member_projection
    PJ->>PG: INSERT INTO user_org_projection
    PJ->>PG: UPDATE org_invitation_projection SET status = 'Accepted'
```

### 8.5 Organization Switching

```mermaid
sequenceDiagram
    actor User
    participant Browser
    participant BFF as Astro.js BFF
    participant RM as Read Model API
    participant PG as PostgreSQL

    User->>Browser: Click org selector, choose "Acme Corp"
    Browser->>BFF: GET /api/orgs/{orgId}/products
    BFF->>BFF: Verify user membership via session
    BFF->>RM: GET /api/users/{userId}/orgs
    RM->>PG: SELECT * FROM user_org_projection WHERE user_id = ?
    PG-->>RM: User memberships
    RM-->>BFF: Verify user belongs to {orgId}
    BFF->>RM: GET /api/orgs/{orgId}/products
    RM-->>BFF: Product list (empty for Epic 1)
    BFF->>Browser: Render org dashboard
```

## 9. API Design (BFF Endpoints)

### 9.1 Authentication Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/auth/login` | Initiates Auth0 login flow (redirect) |
| GET | `/auth/callback` | Auth0 callback, exchanges code, creates/retrieves user |
| POST | `/auth/logout` | Clears session, redirects to Auth0 logout |

### 9.2 User Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/users/me` | Current user profile |
| PATCH | `/api/users/me` | Update display name / avatar |

### 9.3 Organization Endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/organizations` | Create organization |
| GET | `/api/organizations` | List user's organizations |
| GET | `/api/organizations/{orgId}` | Get org details |
| PATCH | `/api/organizations/{orgId}` | Rename organization |
| GET | `/api/organizations/{orgId}/members` | List members |
| POST | `/api/organizations/{orgId}/members/invite` | Invite member |
| POST | `/api/organizations/{orgId}/members/accept` | Accept invitation |
| POST | `/api/organizations/{orgId}/members/decline` | Decline invitation |
| DELETE | `/api/organizations/{orgId}/members/{userId}` | Remove member |
| PATCH | `/api/organizations/{orgId}/members/{userId}/role` | Change role |

### 9.4 Invitation Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/invitations` | List pending invitations for current user |

## 10. Frontend Architecture

### 10.1 Astro.js SPA Shell

```mermaid
graph TD
    subgraph "Astro.js App"
        Router["Client-side Router<br/>(History API)"]
        AuthGuard["Auth Guard<br/>(Session Check)"]
        Sidebar["Sidebar Component"]
        OrgSelector["Org Selector Dropdown"]
    end

    subgraph Pages
        Landing["/ (Landing Page)"]
        Login["/auth/login"]
        Callback["/auth/callback"]
        Dashboard["/dashboard"]
        OrgDash["/orgs/{orgId}"]
        OrgSettings["/orgs/{orgId}/settings"]
        MembersPage["/orgs/{orgId}/members"]
    end

    Router --> AuthGuard
    AuthGuard --> Dashboard
    AuthGuard --> OrgDash
    AuthGuard --> OrgSettings
    AuthGuard --> MembersPage
    Sidebar --> OrgSelector
```

### 10.2 Client-Side State

The Astro.js frontend maintains a minimal client-side state store:
- `currentUser`: The logged-in user's profile
- `organizations`: List of orgs the user belongs to
- `currentOrgId`: The currently selected organization
- `pendingInvitations`: Invitations awaiting response

State is hydrated from the read model API on initial page load and kept fresh via refetch on navigation.

### 10.3 Authorization Model (Frontend)

```mermaid
flowchart TD
    Request[API Request] --> CheckSession{Session exists?}
    CheckSession -->|No| Redirect[Redirect to /auth/login]
    CheckSession -->|Yes| ExtractUserId[Extract userId from session]
    ExtractUserId --> CheckMembership{Belongs to org?}
    CheckMembership -->|No| Deny[403 Forbidden]
    CheckMembership -->|Yes| CheckRole{Required role?}
    CheckRole -->|Owner only| IsOwner{Is Owner?}
    IsOwner -->|Yes| Allow[Allow]
    IsOwner -->|No| Deny
    CheckRole -->|Any member| Allow
```

## 11. Data Flow Summary

```mermaid
flowchart LR
    subgraph Write Path
        CMD[Command] --> ACT[Proto.Actor]
        ACT --> ES[KurrentDB<br/>Append Event]
        ES --> PUB[Redpanda<br/>Publish Event]
    end

    subgraph Project Path
        PUB --> CONS[Projector Consumer]
        CONS --> PG[(PostgreSQL<br/>Projection)]
    end

    subgraph Read Path
        API[Read Model API] --> PG
        UI[Browser SPA] --> API
    end
```

**Write Path:** Browser -> BFF -> Actor Service -> KurrentDB -> Redpanda
**Project Path:** Redpanda -> Projector -> PostgreSQL
**Read Path:** Browser -> BFF -> Read Model API -> PostgreSQL

This separation ensures:
- Writes are always consistent (KurrentDB is the source of truth)
- Reads are eventually consistent (projection lag is typically <100ms)
- Projections can be rebuilt from KurrentDB at any time

## 12. Cross-Cutting Concerns Established in Epic 1

These patterns, once established in Epic 1, are reused by all subsequent epics:

| Concern | Implementation | Reused By |
|---------|---------------|-----------|
| Event envelope with actorId + timestamp | Standardized JSON envelope in actor service | Epics 2-6 |
| Actor lifecycle (spawn, hydrate, passivate) | Proto.Actor manager pattern | Epics 2-6 |
| KurrentDB stream append | Shared infrastructure client | Epics 2-6 |
| Redpanda publishing after append | Post-commit hook in actor base class | Epics 2-6 |
| Projector service (consume, checkpoint, project) | Shared projector framework | Epics 2-6 |
| BFF session management + Auth0 | Astro.js middleware | Epics 2-6 |
| Authorization middleware (org membership check) | BFF request pipeline | Epics 2-6 |
| Projection checkpoint table | PostgreSQL schema | Epics 2-6 |
| Kubernetes manifests pattern | Deployment + Service + ConfigMap | Epics 2-6 |

## 13. Technology Decisions for Epic 1

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Actor framework | Proto.Actor | PRD requirement. Supports location transparency, clustering, and grain-like virtual actors. |
| Event store | KurrentDB (EventStoreDB) | PRD requirement. Purpose-built for event sourcing with stream-based storage, built-in projections (we use external), and HTTP/gRPC APIs. |
| Message broker | Redpanda | PRD requirement. Kafka-compatible, no JVM dependency, simpler operations in K8s. |
| Read model | PostgreSQL | PRD requirement. Mature, reliable, supports the complex queries needed for projections and the cumulative flow (Epic 5). |
| Frontend | Astro.js + Tailwind CSS | PRD requirement. SSR-capable, island architecture for selective hydration, excellent performance. |
| Auth | Auth0 | PRD requirement. Managed service, supports GitHub SSO and future providers. |
| Serialization | System.Text.Json (JSON) | Native .NET, high performance, no external dependency. |
| ID generation | UUID v4 | Globally unique, no coordination needed, safe for distributed actor creation. |
| Session management | HTTP-only cookie (BFF) | Secure, no token exposure to JavaScript, BFF validates JWT server-side. |
