// @velucid/read-model
//
// TypeScript types matching the Postgres projection tables
// (mirrored from backend/src/Velucid.ReadModel/Entities/).
//
// Used by both apps/projector (to write) and apps/web (to type-check its
// local store). Story 4.0 only ships the bootstrap shape; Story 4.1
// populates the entity types from the EF Core models.

export const READ_MODEL_LIB_VERSION = '0.0.0';

export type Uuid = string;
export type Timestamp = string; // ISO-8601 UTC
