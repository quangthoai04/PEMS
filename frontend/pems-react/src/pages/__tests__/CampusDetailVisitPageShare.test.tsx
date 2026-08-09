import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import type {
  PublicGalleryItemDetail,
  PublicGalleryMedia,
  PublicGalleryNavigation,
  PublicLocationShowcase,
} from '../../features/visit-fptu/publicVisitFptu.types';

const getNavigation = vi.fn();
const getLocationShowcase = vi.fn();
const getGalleryItemDetail = vi.fn();
const getLocationGalleryItems = vi.fn();
const getCampuses = vi.fn();
vi.mock('../../features/visit-fptu/publicVisitFptuApi', () => ({
  publicVisitFptuApi: {
    getNavigation: (...a: unknown[]) => getNavigation(...a),
    getLocationShowcase: (...a: unknown[]) => getLocationShowcase(...a),
    getGalleryItemDetail: (...a: unknown[]) => getGalleryItemDetail(...a),
    getLocationGalleryItems: (...a: unknown[]) => getLocationGalleryItems(...a),
    getCampuses: (...a: unknown[]) => getCampuses(...a),
  },
}));

const showSuccessToast = vi.fn();
const showMessageErrorToast = vi.fn();
vi.mock('../../shared/utils/toast', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../shared/utils/toast')>()),
  showSuccessToast: (...a: unknown[]) => showSuccessToast(...a),
  showMessageErrorToast: (...a: unknown[]) => showMessageErrorToast(...a),
}));

import { CampusDetailVisitPage } from '../CampusDetailVisitPage';
import i18n from '../../shared/i18n/config';

const ORIGIN = 'https://www.pems-fpt.site';
const LOCATION_ID = 21;
const ITEM_ID = 105;
/** What every share action must produce, whatever the address bar currently says. */
const CANONICAL = `${ORIGIN}/visit-fptu/hn?locationId=${LOCATION_ID}&itemId=${ITEM_ID}`;

const media = (mediaId: number): PublicGalleryMedia => ({
  mediaId, fileId: mediaId, mediaType: 'IMAGE', sourceType: 'UPLOADED_FILE',
  url: `/api/public/visit-fptu/media/${mediaId}/content`,
  thumbnailUrl: `/api/public/visit-fptu/media/${mediaId}/content`,
  isPrimary: true, displayOrder: 0,
});

const navigation: PublicGalleryNavigation = {
  campus: { campusId: 1, campusCode: 'HN', campusName: 'FPTU Hà Nội', city: 'Hà Nội' },
  areas: [
    {
      areaId: 3, areaName: 'Tòa Demo 01', areaNameEn: 'Demo Building 01', displayOrder: 0,
      areaCoverUrl: '/cover.jpg', areaCoverMediaType: 'IMAGE',
      locations: [
        {
          locationId: LOCATION_ID, locationName: 'Sảnh chính', locationNameEn: 'Main Hall',
          displayOrder: 0, galleryItemId: ITEM_ID, title: 'Tượng 01',
          mediaKind: 'IMAGE', publicGalleryItemCount: 1,
          primaryMediaUrl: '/api/public/visit-fptu/media/1/content',
          locationCoverUrl: '/api/public/visit-fptu/media/1/content',
        },
      ],
    },
  ],
};

const showcase: PublicLocationShowcase = {
  campus: navigation.campus,
  area: { areaId: 3, areaName: 'Tòa Demo 01', areaNameEn: 'Demo Building 01' },
  location: { locationId: LOCATION_ID, locationName: 'Sảnh chính', locationNameEn: 'Main Hall' },
  mediaItems: [
    { galleryItemId: ITEM_ID, title: 'Tượng 01', itemType: 'MEDIA', mediaKind: 'IMAGE', primaryMedia: media(1) },
  ],
  visitDelegationItems: [],
};

const itemDetail: PublicGalleryItemDetail = {
  campus: navigation.campus,
  area: { areaId: 3, areaName: 'Tòa Demo 01', areaNameEn: 'Demo Building 01' },
  location: { locationId: LOCATION_ID, locationName: 'Sảnh chính', locationNameEn: 'Main Hall' },
  galleryItem: {
    galleryItemId: ITEM_ID,
    title: 'Tượng 01',
    titleEn: 'Statue 01',
    content: { vi: { description: 'FPT là một tập đoàn công nghệ.' }, en: { description: 'FPT is a tech group.' } },
    mediaKind: 'IMAGE',
    status: 'PUBLISHED',
  },
  media: [media(1)],
};

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/visit-fptu/:campusCode" element={<CampusDetailVisitPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

/** Opens the item detail modal (deep link) and then its share menu. */
async function openShareMenu() {
  renderAt(`/visit-fptu/hn?locationId=${LOCATION_ID}&itemId=${ITEM_ID}`);
  await waitFor(() => expect(getGalleryItemDetail).toHaveBeenCalledWith(ITEM_ID));

  const shareButtons = await screen.findAllByRole('button', { name: 'Chia sẻ' });
  fireEvent.click(shareButtons[shareButtons.length - 1]);
  return await screen.findByRole('button', { name: /Sao chép liên kết/ });
}

describe('VisitFPTU gallery item — share actions', () => {
  const originalLocation = window.location;
  let writeText: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    getNavigation.mockReset().mockResolvedValue(navigation);
    getLocationShowcase.mockReset().mockResolvedValue(showcase);
    getGalleryItemDetail.mockReset().mockResolvedValue(itemDetail);
    getLocationGalleryItems.mockReset().mockResolvedValue({ items: [] });
    getCampuses.mockReset().mockResolvedValue([]);
    showSuccessToast.mockReset();
    showMessageErrorToast.mockReset();
    Element.prototype.scrollIntoView = vi.fn();
    window.HTMLMediaElement.prototype.pause = vi.fn();

    // The app runs on the production origin; the address bar deliberately carries junk the shared
    // link must NOT inherit.
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...originalLocation, origin: ORIGIN, href: `${ORIGIN}/visit-fptu/hn?utm=mail&itemId=999` },
    });

    writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { configurable: true, value: { writeText } });

    await act(async () => { await i18n.changeLanguage('vi'); });
  });

  afterEach(() => {
    Object.defineProperty(window, 'location', { configurable: true, value: originalLocation });
    vi.restoreAllMocks();
  });

  it('copies the canonical deep link of the open item, not the address bar', async () => {
    const copyButton = await openShareMenu();

    fireEvent.click(copyButton);

    await waitFor(() => expect(writeText).toHaveBeenCalledWith(CANONICAL));
  });

  it('confirms a successful copy with a toast', async () => {
    const copyButton = await openShareMenu();

    fireEvent.click(copyButton);

    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã sao chép liên kết.'));
    expect(showMessageErrorToast).not.toHaveBeenCalled();
  });

  it('reports a failed copy instead of pretending it worked', async () => {
    writeText.mockRejectedValue(new Error('denied'));
    (document as unknown as { execCommand: unknown }).execCommand = vi.fn().mockReturnValue(false);
    const copyButton = await openShareMenu();

    fireEvent.click(copyButton);

    await waitFor(() =>
      expect(showMessageErrorToast).toHaveBeenCalledWith('Không thể sao chép liên kết. Vui lòng thử lại.'));
    expect(showSuccessToast).not.toHaveBeenCalled();
  });

  it('hands Facebook the same canonical URL', async () => {
    const open = vi.spyOn(window, 'open').mockReturnValue(null);
    await openShareMenu();

    fireEvent.click(await screen.findByRole('button', { name: /Facebook/ }));

    await waitFor(() => expect(open).toHaveBeenCalled());
    const target = String(open.mock.calls[0][0]);
    expect(target.startsWith('https://www.facebook.com/sharer/sharer.php?u=')).toBe(true);
    expect(decodeURIComponent(new URL(target).searchParams.get('u') ?? '')).toBe(CANONICAL);
  });

  it('closes the menu once an action is taken', async () => {
    const copyButton = await openShareMenu();

    fireEvent.click(copyButton);

    await waitFor(() => expect(screen.queryByRole('button', { name: /Sao chép liên kết/ })).toBeNull());
  });

  it('closes the menu on a click outside it', async () => {
    await openShareMenu();

    fireEvent.mouseDown(document.body);

    await waitFor(() => expect(screen.queryByRole('button', { name: /Sao chép liên kết/ })).toBeNull());
  });

  it('Escape closes only the menu, leaving the item modal open', async () => {
    await openShareMenu();

    fireEvent.keyDown(document.body, { key: 'Escape' });

    await waitFor(() => expect(screen.queryByRole('button', { name: /Sao chép liên kết/ })).toBeNull());
    // The item itself is still on screen — the page's own Escape handler must not have fired too.
    expect(screen.getByRole('button', { name: 'Chia sẻ' })).toBeTruthy();
  });
});

/**
 * Structural guard for the defect a jsdom test physically cannot see: paint order.
 *
 * The share dropdown lives inside the modal header row, and the <h3> item title is a later sibling.
 * Both were positioned at `z-10`, so the equal z-index was broken by DOM order and the title painted
 * over the header — and with it over the dropdown, whose own `z-[80]` is confined to the header's
 * stacking context. The menu rendered, but its top row (Sao chép liên kết) sat under the title's box
 * and every click landed on the <h3>. Facebook, further down, still worked, which is exactly how the
 * bug looked in the wild. jsdom computes no layout and no stacking, so only the source can hold this.
 */
describe('share dropdown stacking', () => {
  const source = readFileSync(resolve(__dirname, '..', 'CampusDetailVisitPage.tsx'), 'utf8');

  it('gives the header row a higher z-index than the title that follows it', () => {
    const headerRow = source.match(/className="flex items-center justify-between mb-5 relative z-(\d+) gap-3"/);
    expect(headerRow).not.toBeNull();

    const headerZ = Number(headerRow![1]);
    // The title + its underline are the siblings that painted over the menu.
    const titleZ = 10;
    expect(headerZ).toBeGreaterThan(titleZ);
  });
});
