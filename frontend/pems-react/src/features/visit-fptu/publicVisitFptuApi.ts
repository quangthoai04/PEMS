import httpClient from '../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../shared/api/endpoints';
import type {
  PublicCampus,
  PublicGalleryNavigation,
  PublicGalleryItemDetail,
} from './publicVisitFptu.types';

/**
 * Read API for the public VisitFPTU Gallery page. All endpoints are anonymous; a logged-in user's
 * Bearer token (auto-attached by httpClient) is simply ignored server-side. 404 from navigation means
 * the campus is missing/inactive; 404 from the location detail means the item is no longer public-visible
 * (BR-PGAL-22) and the caller should fall back to another location.
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

  async getLocationGalleryItem(locationId: number): Promise<PublicGalleryItemDetail> {
    const { data } = await httpClient.get(
      API_ENDPOINTS.publicVisitFptu.locationGalleryItem(locationId),
    );
    return data;
  },
};
