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
  },
});
