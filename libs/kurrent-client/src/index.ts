// @velucid/kurrent-client
//
// Thin adapter over the official KurrentDB Node client. Exposes a
// subscribeFromCheckpoint(checkpoint, handler) API designed to be swappable
// when the Kurrent TypeScript sync engine lands — the engine will provide
// a similar API; we add a second implementation behind the same interface.
//
// Populated in Story 4.1 (Shared TypeScript Libraries). The export below
// is the bootstrap smoke shape.

export const KURRENT_CLIENT_LIB_VERSION = '0.0.0';

export interface SubscribeFromCheckpointRequest {
  checkpoint?: bigint;
  onEvent: (event: unknown) => Promise<void> | void;
}
