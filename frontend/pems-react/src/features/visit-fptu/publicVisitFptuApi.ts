import httpClient from '../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../shared/api/endpoints';
import type {
  PublicCampus,
  PublicGalleryNavigation,
  PublicLocationGalleryGrid,
  PublicLocationShowcase,
  PublicGalleryItemDetail,
} from './publicVisitFptu.types';

/**
 * Read API for the public VisitFPTU Gallery page. All endpoints are anonymous; a logged-in user's
 * Bearer token (auto-attached by httpClient) is simply ignored server-side. 404 from navigation means
 * the campus is missing/inactive; 404 from the grid/detail means the location/item is no longer
 * public-visible (BR-PGAL-GRID-10/11) and the caller should fall back.
 */
export const publicVisitFptuApi = {
  async getCampuses(): Promise<PublicCampus[]> {
    const { data } = await httpClient.get(API_ENDPOINTS.publicVisitFptu.campuses);
    return data?.items ?? [];
  },

  async getNavigation(campusCode: string): Promise<PublicGalleryNavigation> {
    const { data } = await httpClient.get(API_ENDPOINTS.publicVisitFptu.navigation(campusCode));
    return data;
  },

  /** Tier 1 — the album grid of a location (each item by its primary media). */
  async getLocationGalleryItems(locationId: number): Promise<PublicLocationGalleryGrid> {
    const { data } = await httpClient.get(
      API_ENDPOINTS.publicVisitFptu.locationGalleryItems(locationId),
    );
    return data;
  },

  /** Location Showcase — the location's MEDIA items (column) + VISIT_DELEGATION items (row). */
  async getLocationShowcase(locationId: number): Promise<PublicLocationShowcase> {
    const { data } = await httpClient.get(API_ENDPOINTS.publicVisitFptu.locationShowcase(locationId));
    return data;
  },

  /** Tier 2 — the full detail of one gallery item (all its media + bilingual content). */
  async getGalleryItemDetail(galleryItemId: number): Promise<PublicGalleryItemDetail> {
    const { data } = await httpClient.get(
      API_ENDPOINTS.publicVisitFptu.galleryItemDetail(galleryItemId),
    );
    return data;
  },
};
