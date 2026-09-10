import { defineConfig, devices } from '@playwright/test';

/**
 * Real end-to-end tests for the login/register/refresh/logout flow (issue
 * #15's "with the refresh stuff" ask), driven against the actual Docker
 * dev stack (API + SQL Server + MinIO + Mailpit) — not mocked. See
 * e2e/global-setup.ts for how the stack is brought up/torn down and
 * e2e/mailpit.ts for how real confirmation emails are read back out.
 *
 * Run with `npm run test:e2e` (see package.json). First run pulls/builds
 * Docker images and waits for SQL Server's cold start, so it's noticeably
 * slower than the unit test suite — that's expected.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: false, // shared Docker stack + Mailpit inbox — tests must not race each other
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  globalSetup: './e2e/global-setup.ts',
  globalTeardown: './e2e/global-teardown.ts',
  timeout: 60_000,
  use: {
    baseURL: 'http://localhost:8080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
});
