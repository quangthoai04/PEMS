import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { MemoryRouter, Routes, Route, useLocation } from 'react-router-dom';
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

import { CampusDetailVisitPage } from '../CampusDetailVisitPage';
import i18n from '../../shared/i18n/config';

const media = (mediaId: number): PublicGalleryMedia => ({
  mediaId, fileId: mediaId, mediaType: 'IMAGE', sourceType: 'UPLOADED_FILE',
  url: `/api/public/visit-fptu/media/${mediaId}/content`,
  thumbnailUrl: `/api/public/visit-fptu/media/${mediaId}/content`,
  isPrimary: true, displayOrder: 0,
});

const LOCATION_ID = 12;
const OTHER_LOCATION_ID = 99;
const ITEM_ID = 88;

const navigation: PublicGalleryNavigation = {
  campus: { campusId: 1, campusCode: 'HN', campusName: 'FPTU Hà Nội', city: 'Hà Nội' },
  areas: [
    {
      areaId: 3, areaName: 'Khu học tập', areaNameEn: 'Study Area', displayOrder: 0,
      areaCoverUrl: '/cover.jpg', areaCoverMediaType: 'IMAGE',
      locations: [
        {
          locationId: LOCATION_ID, locationName: 'Thư viện', locationNameEn: 'Library',
          displayOrder: 0, galleryItemId: ITEM_ID, title: 'Thư viện trung tâm',
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
  area: { areaId: 3, areaName: 'Khu học tập', areaNameEn: 'Study Area' },
  location: { locationId: LOCATION_ID, locationName: 'Thư viện', locationNameEn: 'Library' },
  mediaItems: [
    { galleryItemId: ITEM_ID, title: 'Thư viện trung tâm', itemType: 'MEDIA', mediaKind: 'IMAGE', primaryMedia: media(1) },
  ],
  visitDelegationItems: [],
};

function itemDetail(locationId: number): PublicGalleryItemDetail {
  return {
    campus: navigation.campus,
    area: { areaId: 3, areaName: 'Khu học tập', areaNameEn: 'Study Area' },
    location: { locationId, locationName: 'Thư viện', locationNameEn: 'Library' },
    galleryItem: {
      galleryItemId: ITEM_ID,
      title: 'Thư viện trung tâm',
      titleEn: 'Central Library',
      content: { vi: { description: 'Không gian đọc sách' }, en: { description: 'Reading space' } },
      mediaKind: 'IMAGE',
      status: 'PUBLISHED',
    },
    media: [media(1)],
  };
}

/** Exposes the live query string so the URL-sync rules can be asserted. */
function UrlProbe() {
  const location = useLocation();
  return <div data-testid="query">{location.search}</div>;
}

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route
          path="/visit-fptu/:campusCode"
          element={<><CampusDetailVisitPage /><UrlProbe /></>}
        />
      </Routes>
    </MemoryRouter>,
  );
}

const query = () => screen.getByTestId('query').textContent ?? '';

describe('VisitFPTU gallery deep link (/visit-fptu/:campus?locationId=&itemId=)', () => {
  beforeEach(async () => {
    getNavigation.mockReset().mockResolvedValue(navigation);
    getLocationShowcase.mockReset().mockResolvedValue(showcase);
    getGalleryItemDetail.mockReset().mockResolvedValue(itemDetail(LOCATION_ID));
    getLocationGalleryItems.mockReset().mockResolvedValue({ items: [] });
    getCampuses.mockReset().mockResolvedValue([]);
    Element.prototype.scrollIntoView = vi.fn();
    window.HTMLMediaElement.prototype.pause = vi.fn();
    await act(async () => { await i18n.changeLanguage('vi'); });
  });

  it('opens the location and its item straight from the URL on load', async () => {
    renderAt(`/visit-fptu/hn?locationId=${LOCATION_ID}&itemId=${ITEM_ID}`);

    await waitFor(() => expect(getLocationShowcase).toHaveBeenCalledWith(LOCATION_ID));
    await waitFor(() => expect(getGalleryItemDetail).toHaveBeenCalledWith(ITEM_ID));
  });

  it('opens only the location when the URL carries no item', async () => {
    renderAt(`/visit-fptu/hn?locationId=${LOCATION_ID}`);

    await waitFor(() => expect(getLocationShowcase).toHaveBeenCalledWith(LOCATION_ID));
    expect(getGalleryItemDetail).not.toHaveBeenCalled();
  });

  it('ignores a locationId that does not belong to this campus', async () => {
    renderAt(`/visit-fptu/hn?locationId=${OTHER_LOCATION_ID}&itemId=${ITEM_ID}`);

    await waitFor(() => expect(getNavigation).toHaveBeenCalled());
    // Neither the foreign location nor anything inside it is opened.
    await act(async () => { await Promise.resolve(); });
    expect(getLocationShowcase).not.toHaveBeenCalled();
    expect(getGalleryItemDetail).not.toHaveBeenCalled();
  });

  it('closes an item whose detail says it lives in a different location', async () => {
    // Hand-edited query string: itemId belongs to another location than locationId claims.
    getGalleryItemDetail.mockResolvedValue(itemDetail(OTHER_LOCATION_ID));
    renderAt(`/visit-fptu/hn?locationId=${LOCATION_ID}&itemId=${ITEM_ID}`);

    await waitFor(() => expect(getGalleryItemDetail).toHaveBeenCalledWith(ITEM_ID));
    // The mismatched item is dropped from the URL; the valid location stays open.
    await waitFor(() => expect(query()).not.toContain('itemId'));
    expect(query()).toContain(`locationId=${LOCATION_ID}`);
  });

  it('keeps the item in the URL while it is open, and drops only it when closed', async () => {
    renderAt(`/visit-fptu/hn?locationId=${LOCATION_ID}&itemId=${ITEM_ID}`);
    await waitFor(() => expect(getGalleryItemDetail).toHaveBeenCalledWith(ITEM_ID));
    expect(query()).toContain(`itemId=${ITEM_ID}`);

    // Close controls are icon buttons named by their title attribute ("Đóng" in VI). The location
    // showcase underneath has one too; the item modal is the topmost overlay, so it is the last.
    const closeButtons = await screen.findAllByRole('button', { name: 'Đóng' });
    fireEvent.click(closeButtons[closeButtons.length - 1]);

    await waitFor(() => expect(query()).not.toContain('itemId'));
    // Closing the item returns to its location, it does not leave the location too.
    expect(query()).toContain(`locationId=${LOCATION_ID}`);
  });

  it('does not crash when the linked item is no longer public', async () => {
    getGalleryItemDetail.mockRejectedValue(new Error('404'));
    renderAt(`/visit-fptu/hn?locationId=${LOCATION_ID}&itemId=${ITEM_ID}`);

    await waitFor(() => expect(getGalleryItemDetail).toHaveBeenCalledWith(ITEM_ID));
    // The location it was in is still open and usable.
    await waitFor(() => expect(getLocationShowcase).toHaveBeenCalledWith(LOCATION_ID));
    expect(query()).toContain(`locationId=${LOCATION_ID}`);
  });
});
