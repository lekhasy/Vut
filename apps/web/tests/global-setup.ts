/**
 * Global setup — verifies the dev server is reachable before running any tests.
 * The Playwright webServer config (see playwright.config.ts) auto-starts the
 * dev server, so by the time this runs the server should already be up.
 *
 * To run tests manually: cd apps/web && bun run test:e2e
 */
export default async () => {
  const baseUrl = process.env.BASE_URL || 'http://localhost:4321';
  try {
    const resp = await fetch(baseUrl, { signal: AbortSignal.timeout(5000) });
    if (!resp.ok) throw new Error(`App at ${baseUrl} returned ${resp.status}`);
  } catch {
    throw new Error(
      `App is not running at ${baseUrl}.\n` +
        `Start it with: cd apps/web && bun run dev\n` +
        `Then run tests with: cd apps/web && bun run test:e2e`,
    );
  }
};
