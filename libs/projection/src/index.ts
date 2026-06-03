// @velucid/projection
//
// Pure functions: (state, event) => state for each Velucid aggregate.
//
// This is the SHARED projection code that runs in both:
//   - apps/projector (backend Node worker, post-Story-4.2)
//   - apps/web (frontend, when the Kurrent TypeScript sync engine lands)
//
// Pure: no I/O, no framework deps, no DB, no network. Easily testable.
//
// Populated in Story 4.1 (Shared TypeScript Libraries). The export below
// is the bootstrap smoke shape.

export const PROJECTION_LIB_VERSION = '0.0.0';

/** Identity projection used as a placeholder. Replaced in Story 4.1. */
export const identity = <S>(state: S): S => state;
