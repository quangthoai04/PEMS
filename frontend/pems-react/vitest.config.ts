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
    // The suite has grown well past the ~123 files `emailHtmlSanitization.test.tsx`'s own comment was
    // measured against (186 as of CanhIter3FixBug closure) — vitest still runs files in parallel
    // worker threads, so a full-suite run now has more genuinely heavy full-form-render tests
    // (VisitRequestFormV2 with several campuses) contending for the same CPU cores. Verified 2026-08-25:
    // 2 separate full-suite runs each timed out on ONE such test at the vitest default of 5000ms — a
    // DIFFERENT file each time, never an assertion failure, and every one passed reliably alone. That is
    // scheduling contention, not a bug, so the shared budget is raised here rather than chasing it
    // file-by-file forever. Individual files may still raise it further (see the two above) for cases
    // measured to need more headroom than this.
    testTimeout: 10_000,
  },
});
