import '@testing-library/jest-dom/vitest';
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';
import { randomUUID } from 'node:crypto';
// Initialize the real i18n instance once for every component under test. jsdom reports
// navigator.language = en-US, so tests deterministically assert the EN strings.
import '../shared/i18n/config';

// jsdom does not always expose crypto.randomUUID (the submit intent id generator).
if (typeof globalThis.crypto === 'undefined') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (globalThis as any).crypto = {};
}
if (typeof globalThis.crypto.randomUUID !== 'function') {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (globalThis.crypto as any).randomUUID = randomUUID;
}

// jsdom does not implement IntersectionObserver. Any component using motion's `whileInView`
// (or LazyGlobeShowcase) throws on mount without it. The stub reports every observed element as
// immediately in view so scroll-reveal sections render their final state under test.
if (typeof globalThis.IntersectionObserver === 'undefined') {
  class IntersectionObserverStub implements IntersectionObserver {
    readonly root: Element | Document | null = null;
    readonly rootMargin: string = '0px';
    readonly thresholds: ReadonlyArray<number> = [0];

    constructor(private readonly callback: IntersectionObserverCallback) {}

    observe(target: Element): void {
      this.callback(
        [
          {
            target,
            isIntersecting: true,
            intersectionRatio: 1,
            time: 0,
            boundingClientRect: target.getBoundingClientRect(),
            intersectionRect: target.getBoundingClientRect(),
            rootBounds: null,
          } as IntersectionObserverEntry,
        ],
        this,
      );
    }

    unobserve(): void {}
    disconnect(): void {}
    takeRecords(): IntersectionObserverEntry[] {
      return [];
    }
  }

  globalThis.IntersectionObserver = IntersectionObserverStub as unknown as typeof IntersectionObserver;
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  sessionStorage.clear();
});
