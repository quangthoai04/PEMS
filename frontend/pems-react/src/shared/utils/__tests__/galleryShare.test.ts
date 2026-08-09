import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  buildFacebookShareUrl,
  buildGalleryShareUrl,
  copyTextToClipboard,
  openFacebookShare,
} from '../galleryShare';

const ORIGIN = 'https://www.pems-fpt.site';

/** jsdom's default origin is http://localhost — point it at the production one for URL assertions. */
function setOrigin(origin: string) {
  Object.defineProperty(window, 'location', {
    configurable: true,
    value: { ...window.location, origin, href: `${origin}/visit-fptu/hn` },
  });
}

describe('buildGalleryShareUrl — the one canonical link of a gallery item', () => {
  beforeEach(() => setOrigin(ORIGIN));

  it('builds /visit-fptu/{campus}?locationId=&itemId= on the current origin', () => {
    expect(buildGalleryShareUrl({ campusCode: 'HN', locationId: 21, galleryItemId: 105 })).toBe(
      `${ORIGIN}/visit-fptu/hn?locationId=21&itemId=105`,
    );
  });

  it('lowercases the campus code so one item has exactly one shared URL', () => {
    const upper = buildGalleryShareUrl({ campusCode: 'DN', locationId: 51, galleryItemId: 7 });
    const lower = buildGalleryShareUrl({ campusCode: 'dn', locationId: 51, galleryItemId: 7 });
    expect(upper).toBe(lower);
    expect(upper).toContain('/visit-fptu/dn');
  });

  it('does not inherit whatever the current page URL happens to carry', () => {
    setOrigin(ORIGIN);
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { origin: ORIGIN, href: `${ORIGIN}/visit-fptu/hn?locationId=999&itemId=999&debug=1` },
    });

    expect(buildGalleryShareUrl({ campusCode: 'HN', locationId: 21, galleryItemId: 105 })).toBe(
      `${ORIGIN}/visit-fptu/hn?locationId=21&itemId=105`,
    );
  });
});

describe('buildFacebookShareUrl', () => {
  it('percent-encodes the whole canonical URL into the sharer query', () => {
    const canonical = `${ORIGIN}/visit-fptu/hn?locationId=21&itemId=105`;

    const shareUrl = buildFacebookShareUrl(canonical);

    expect(shareUrl).toBe(
      'https://www.facebook.com/sharer/sharer.php?u=' +
        'https%3A%2F%2Fwww.pems-fpt.site%2Fvisit-fptu%2Fhn%3FlocationId%3D21%26itemId%3D105',
    );
    // Facebook must receive the item URL intact — the & of itemId cannot leak as a second param.
    expect(decodeURIComponent(new URL(shareUrl).searchParams.get('u') ?? '')).toBe(canonical);
  });

  it('opens the sharer in a popup, never in the current tab', () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);

    openFacebookShare(`${ORIGIN}/visit-fptu/hn?locationId=21&itemId=105`);

    const [target, name, features] = open.mock.calls[0];
    expect(target).toContain('facebook.com/sharer');
    expect(name).toBe('_blank');
    expect(String(features)).toContain('noopener');
    open.mockRestore();
  });
});

describe('copyTextToClipboard', () => {
  const originalClipboard = navigator.clipboard;

  afterEach(() => {
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: originalClipboard });
    vi.restoreAllMocks();
  });

  function stubClipboard(writeText: unknown) {
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: writeText ? { writeText } : undefined });
  }

  it('uses the Clipboard API when it is available', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    stubClipboard(writeText);

    await expect(copyTextToClipboard('https://example.test/a')).resolves.toBe(true);
    expect(writeText).toHaveBeenCalledWith('https://example.test/a');
  });

  it('falls back to execCommand when the Clipboard API is missing (http origin, old browser)', async () => {
    stubClipboard(undefined);
    const exec = vi.fn().mockReturnValue(true);
    (document as unknown as { execCommand: unknown }).execCommand = exec;

    await expect(copyTextToClipboard('https://example.test/b')).resolves.toBe(true);
    expect(exec).toHaveBeenCalledWith('copy');
    // The scratch textarea must not be left behind in the DOM.
    expect(document.querySelector('textarea')).toBeNull();
  });

  it('falls back when the Clipboard API rejects (permission denied)', async () => {
    stubClipboard(vi.fn().mockRejectedValue(new Error('denied')));
    const exec = vi.fn().mockReturnValue(true);
    (document as unknown as { execCommand: unknown }).execCommand = exec;

    await expect(copyTextToClipboard('https://example.test/c')).resolves.toBe(true);
    expect(exec).toHaveBeenCalledWith('copy');
  });

  it('reports failure when neither path can copy', async () => {
    stubClipboard(vi.fn().mockRejectedValue(new Error('denied')));
    (document as unknown as { execCommand: unknown }).execCommand = vi.fn().mockReturnValue(false);

    await expect(copyTextToClipboard('https://example.test/d')).resolves.toBe(false);
  });
});
