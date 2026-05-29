import { useStore as useNanoStore } from '@nanostores/react';
import type { Atom, MapStore } from 'nanostores';

/**
 * React hook for nanostores atoms and maps.
 * Wraps @nanostores/react's useStore to handle both Atom and MapStore types.
 * Note: type parameter is intentionally loose due to nanostores' complex type hierarchy
 * (Atom<T> requires T extends object, but our stores use primitives like string|null).
 */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export function useStore(store: Atom<any> | MapStore<any>): any {
  return useNanoStore(store as Atom<any>);
}