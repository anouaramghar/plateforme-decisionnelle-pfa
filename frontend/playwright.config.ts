import { defineConfig, devices } from '@playwright/test'

// E2E smoke tests for the Plateforme Décisionnelle.
//
// Run against a LIVE stack — the suite does not mock the backend:
//   docker compose up -d --build --wait
//   cd frontend && npm run e2e:install && npm run e2e
//
// Base URL and admin credentials come from env so the same suite works against
// docker-compose (http://localhost) or a deployed environment. See e2e/
// for the tests and .github/workflows/e2e.yml for the CI driver.
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:80',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
})
