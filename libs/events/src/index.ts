// @velucid/events
//
// TypeScript discriminated union of all Velucid event types.
//
// This is the shared, versioned contract for events flowing through KurrentDB
// and consumed by both the backend projector and the frontend (via the
// Kurrent TypeScript sync engine). Hand-written from the C# event records in
// backend/src/Velucid.Silo/Events/. A breaking change to the union requires
// a major version bump.
//
// Populated in Story 4.1 (Shared TypeScript Libraries). The empty export
// below is the bootstrap smoke shape.

export const EVENTS_LIB_VERSION = '0.0.0';

export type EventName = string; // placeholder — replaced in Story 4.1
