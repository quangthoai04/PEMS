import { defineConfig, devices } from '@playwright/test';

/**
 * REAL-STACK E2E config (H-4): real Chromium → real React (Vite) → real .NET API → real disposable MySQL.
 *
 * The .NET backend (Testing env, both v2 flags ON, connection → the disposable `pems_e2e_realstack`, the
 * Testing-only FileSink email/OTP sink) is started by the orchestration script `scripts/run-realstack-e2e.mjs`
 * BEFORE Playwright runs; this config only starts the real Vite frontend and points it at the backend via
 * `VITE_API_BASE_URL`. Specs here do NOT mock the network — they read the OTP/invitation from the sink file
 * (`PEMS_E2E_TEST_SINK_PATH`). Kept separate from `playwright.config.ts` (the mocked browser-contract suite).
 */
const FRONTEND_PORT = Number(process.env.PEMS_E2E_FRONTEND_PORT ?? 3100);
const API_BASE = process.env.PEMS_E2E_API_BASE ?? 'http://localhost:5299/api';

export default defineConfig({
  testDir: './tests-realstack',
  fullyParallel: false,
  workers: 1,
  timeout: 120_000,
  expect: { timeout: 15_000 },
  reporter: [['list']],
  use: {
    baseURL: `http://localhost:${FRONTEND_PORT}`,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: `npx vite --port=${FRONTEND_PORT} --strictPort`,
    url: `http://localhost:${FRONTEND_PORT}`,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    // Point the real frontend at the real test backend (wins over the .env default via process.env).
    env: { VITE_API_BASE_URL: API_BASE },
  },
});
