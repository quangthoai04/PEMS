import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';
import { randomUUID } from 'node:crypto';

// jsdom does not always expose crypto.randomUUID (the submit intent id generator).
if (typeof globalThis.crypto === 'undefined') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (globalThis as any).crypto = {};
}
if (typeof globalThis.crypto.randomUUID !== 'function') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (globalThis.crypto as any).randomUUID = randomUUID;
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  sessionStorage.clear();
});
