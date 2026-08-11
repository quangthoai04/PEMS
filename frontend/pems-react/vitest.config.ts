import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// Kept SEPARATE from vite.config.ts so unit-test settings never leak into the
// production build. `npm run test:unit` runs this config once (CI-friendly).
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    globals: true,
    css: false,
    // Vitest's default is 5s, which this suite outgrew. Measured on the full run (138 files across
    // 20 logical cores): 17 tests already spend over 2s of real work, and the slowest — the ones
    // that render the whole VisitRequestFormV2 and drive it through submit/validate cycles — take
    // 5.3s with the machine otherwise idle. The same tests take 300-800ms when their file runs
    // alone, so most of that is fork parallelism, and any extra load on the box pushes them further
    // (measured contention factor: median 1.09, p90 1.41, worst 4.29). At a 5s budget the heaviest
    // tests sat under 2x headroom and crossed it whenever the machine was busy — which is what made
    // this suite flaky rather than any race in the tests themselves.
    //
    // This is the one number for the whole suite. It replaces the per-test overrides that had been
    // added one at a time as individual tests got bitten (four `}, 15000)` and one
    // `vi.setConfig({ testTimeout: 20_000 })`), which fixed whichever test had just failed and left
    // the rest of the same cohort on the default. 20s is >2x the worst duration ever observed here,
    // so a test that genuinely hangs still fails — it just no longer fails for being slow.
    testTimeout: 20_000,
  },
});
