import { defineConfig, devices } from '@playwright/test';
import path from 'path';
import { fileURLToPath } from 'url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  testDir: './tests',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: 'html',

  use: {
    baseURL: process.env.BASE_URL || 'http://localhost:4321',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },

  globalSetup: './tests/global-setup.ts',
  globalTeardown: './tests/global-teardown.ts',

  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  // Always auto-start the dev server so `nx run web:test` is self-contained.
  // (Previously only started in CI; the legacy behavior assumed a developer
  // had the server running locally. After Story 4.0 we want `nx test` to work
  // for both developers and CI from a clean checkout.)
  //
  // Astro validates `env.schema` (AUTH0_DOMAIN, SILO_API_URL, etc.) at startup.
  // For a clean local checkout we stub the server-secret env vars with
  // placeholders so the dev server boots; real values should be supplied via
  // a developer-managed `.env` file or CI secrets when running e2e against a
  // real environment.
  webServer: {
    command:
      'AUTH0_DOMAIN=test.us.auth0.com AUTH0_CLIENT_ID=test AUTH0_CLIENT_SECRET=test AUTH0_AUDIENCE=https://test SESSION_SECRET=dGVzdHRlc3R0ZXN0dGVzdHRlc3R0ZXN0dGVzdHRlc3Q= SILO_API_URL=http://localhost:5000 APP_URL=http://localhost:4321 bun run dev',
    url: 'http://localhost:4321',
    reuseExistingServer: !process.env.CI,
    timeout: 120 * 1000,
  },
});
