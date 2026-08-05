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

// jsdom implements no layout, so `Range` has no geometry methods. Quill asks for them on every focus()
// (scrollSelectionIntoView → Selection.getBounds → range.getBoundingClientRect), which makes any test that
// drives a real editor throw from inside Quill rather than from the code under test.
//
// Stubbed as a zero rect rather than something plausible: nothing here should ever make an assertion about
// geometry — jsdom cannot produce a true one — and a zero rect is the honest answer to "where is this on
// screen?" in an environment that does not lay anything out.
if (typeof Range !== 'undefined') {
  const zeroRect = () => ({
    x: 0, y: 0, top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0,
    toJSON() { return this; },
  }) as DOMRect;

  if (typeof Range.prototype.getBoundingClientRect !== 'function') {
    Range.prototype.getBoundingClientRect = zeroRect;
  }
  if (typeof Range.prototype.getClientRects !== 'function') {
    Range.prototype.getClientRects = function getClientRects() {
      const list: DOMRect[] = [];
      return Object.assign(list, { item: (i: number) => list[i] ?? null }) as unknown as DOMRectList;
    };
  }
}

afterEach(() => {
  cleanup();
  localStorage.clear();
  sessionStorage.clear();
});
