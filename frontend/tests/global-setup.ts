/**
 * Global setup — verifies the dev server is reachable before running any tests.
 * Run with: BASE_URL=http://localhost:4321 npx playwright test
 *
 * To start the app: cd frontend && npm run dev
 */
export default async () => {
  const baseUrl = process.env.BASE_URL || 'http://localhost:4321';
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
};