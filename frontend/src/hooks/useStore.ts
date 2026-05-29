import { useStore as useNanoStore } from '@nanostores/react';

export function useStore<T>(store: import('nanostores').Atom<T>): T {
  return useNanoStore(store);
}