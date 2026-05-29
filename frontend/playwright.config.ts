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

  webServer: process.env.CI
    ? {
        command: 'npm run dev',
        url: 'http://localhost:4321',
        reuseExistingServer: false,
        timeout: 120 * 1000,
      }
    : undefined,
});