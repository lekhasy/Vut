import { randomUUID } from 'crypto';

export interface TestUser {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl: string;
  isEmailVerified: boolean;
}

export function createTestUser(overrides: Partial<TestUser> = {}): TestUser {
  const id = randomUUID();
  return {
    userId: overrides.userId ?? id,
    email: overrides.email ?? `test-${id.slice(0, 8)}@example.com`,
    displayName: overrides.displayName ?? 'Test User',
    avatarUrl: overrides.avatarUrl ?? `https://www.gravatar.com/avatar/?d=mp`,
    isEmailVerified: overrides.isEmailVerified ?? true,
  };
}
