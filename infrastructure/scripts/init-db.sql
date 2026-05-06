-- VUT Platform - PostgreSQL Read Model Initialization
-- This script runs automatically on first container start (docker-entrypoint-initdb.d)
-- It will NOT run again if the data volume already exists.

-- User projection
CREATE TABLE IF NOT EXISTS user_projection (
    user_id       UUID PRIMARY KEY,
    provider_id   TEXT NOT NULL UNIQUE,
    display_name  TEXT NOT NULL,
    avatar_url    TEXT,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Organization projection
CREATE TABLE IF NOT EXISTS org_projection (
    org_id        UUID PRIMARY KEY,
    name          TEXT NOT NULL,
    is_deleted    BOOLEAN NOT NULL DEFAULT FALSE,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Organization member projection
CREATE TABLE IF NOT EXISTS org_member_projection (
    org_id        UUID NOT NULL REFERENCES org_projection(org_id),
    user_id       UUID NOT NULL REFERENCES user_projection(user_id),
    role          TEXT NOT NULL CHECK (role IN ('Owner', 'Member')),
    joined_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (org_id, user_id)
);

-- Organization invitation projection
CREATE TABLE IF NOT EXISTS org_invitation_projection (
    org_id            UUID NOT NULL REFERENCES org_projection(org_id),
    email             TEXT NOT NULL,
    role              TEXT NOT NULL CHECK (role IN ('Owner', 'Member')),
    status            TEXT NOT NULL CHECK (status IN ('Pending', 'Accepted', 'Declined')),
    invited_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    user_id           UUID,
    PRIMARY KEY (org_id, email)
);

-- User organization membership (reverse index)
CREATE TABLE IF NOT EXISTS user_org_projection (
    user_id       UUID NOT NULL REFERENCES user_projection(user_id),
    org_id        UUID NOT NULL REFERENCES org_projection(org_id),
    role          TEXT NOT NULL,
    PRIMARY KEY (user_id, org_id)
);

-- Projection checkpoint table (for projector service)
CREATE TABLE IF NOT EXISTS projection_checkpoint (
    projector_name   TEXT NOT NULL,
    topic            TEXT NOT NULL,
    partition_id     INT NOT NULL,
    last_offset      BIGINT NOT NULL DEFAULT 0,
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (projector_name, topic, partition_id)
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_user_projection_provider ON user_projection(provider_id);
CREATE INDEX IF NOT EXISTS idx_org_member_projection_user ON org_member_projection(user_id);
CREATE INDEX IF NOT EXISTS idx_org_invitation_projection_email ON org_invitation_projection(email, status);
CREATE INDEX IF NOT EXISTS idx_user_org_projection_org ON user_org_projection(org_id);
