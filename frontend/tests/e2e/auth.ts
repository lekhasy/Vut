import { test as base, type Page } from '@playwright/test';
import { signInAs } from '../support/auth-session';
import { createTestUser } from '../factories/user';
import type { TestUser } from '../support/auth-session';

export { createTestUser };

type AuthFixture = {
  page: Page;
  user: TestUser;
};

export const test = base.extend<AuthFixture>({
  page: async ({ browser }, use) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();
    await use(page);
    await ctx.close();
  },

  user: async ({ page }, use) => {
    const user = createTestUser();
    await signInAs(page, user);
    await use(user);
  },
});

export { expect } from '@playwright/test';

/**
 * Helper: ensure the dev server is reachable.
 * Fails fast if BASE_URL is not accessible.
 */
export async function ensureAppRunning(baseUrl: string): Promise<void> {
  try {
    const resp = await fetch(baseUrl, { signal: AbortSignal.timeout(5000) });
    if (!resp.ok) throw new Error(`App at ${baseUrl} returned ${resp.status}`);
  } catch {
    throw new Error(
      `App is not running at ${baseUrl}.\n` +
      `Start it with: cd frontend && npm run dev\n` +
      `Then run tests with: npx playwright test`
    );
  }
}

/**
 * Helper: make an API request to the Astro BFF (not the Silo directly).
 */
export async function apiRequest(
  page: Page,
  method: 'GET' | 'POST' | 'PUT' | 'DELETE',
  path: string,
  body?: unknown
): Promise<{ status: number; data: unknown }> {
  const baseURL = process.env.BASE_URL || 'http://localhost:4321';
  const url = `${baseURL}${path}`;

  const resp = await page.request.fetch(url, {
    method,
    headers: { 'Content-Type': 'application/json' },
    data: body ? JSON.stringify(body) : undefined,
  });

  let data: unknown;
  try { data = await resp.json(); } catch { data = null; }

  return { status: resp.status(), data };
}