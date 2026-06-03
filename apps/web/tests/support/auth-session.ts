/**
 * Standalone auth session helper for Playwright E2E tests.
 * Does NOT import from src/lib/auth/session (which requires Astro server context).
 * Uses a fixed test secret so it works outside the Astro runtime.
 */
import * as crypto from 'node:crypto';

const SESSION_COOKIE = 'vut_session';
// Must match SESSION_SECRET in .env so the app can decrypt test cookies
const TEST_SECRET = '+5BcYzKH9Md/PSisWWg9Au95zpmg0DywNE5MeDwWqz4=';

function getKey(): Buffer {
  return Buffer.from(TEST_SECRET, 'base64');
}

export interface TestUser {
  userId: string;
  email: string;
  displayName: string;
  avatarUrl: string;
  isEmailVerified: boolean;
}

function encrypt(payload: object): string {
  const key = getKey();
  const iv = crypto.randomBytes(12);
  const cipher = crypto.createCipheriv('aes-256-gcm', key, iv);

  const plaintext = JSON.stringify(payload);
  const encrypted = Buffer.concat([cipher.update(plaintext, 'utf8'), cipher.final()]);
  const authTag = cipher.getAuthTag();

  return [iv.toString('base64'), authTag.toString('base64'), encrypted.toString('base64')].join(
    '.',
  );
}

/**
 * Creates a valid session cookie for the given test user.
 * Sets browser cookies directly so middleware reads session and populates locals.
 */
export async function signInAs(
  page: import('@playwright/test').Page,
  user: TestUser,
): Promise<void> {
  const payload = {
    userId: user.userId,
    email: user.email,
    displayName: user.displayName,
    avatarUrl: user.avatarUrl,
    isEmailVerified: user.isEmailVerified,
    iat: Math.floor(Date.now() / 1000),
  };

  const encrypted = encrypt(payload);
  await page.context().addCookies([
    {
      name: SESSION_COOKIE,
      value: encrypted,
      domain: 'localhost',
      path: '/',
      httpOnly: true,
      secure: false,
      sameSite: 'Lax',
    },
  ]);
}
