import { randomUUID } from 'crypto';

export interface Org {
  orgId: string;
  name: string;
  isDeleted: boolean;
}

/**
 * Creates a unique org for test use.
 * Pass overrides to customize any field.
 */
export function createOrg(overrides: Partial<Org> = {}): Org {
  return {
    orgId: overrides.orgId ?? randomUUID(),
    name: overrides.name ?? `TestOrg-${randomUUID().slice(0, 8)}`,
    isDeleted: overrides.isDeleted ?? false,
  };
}
