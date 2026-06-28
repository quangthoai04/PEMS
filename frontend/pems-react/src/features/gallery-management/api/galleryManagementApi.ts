import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  ChangeGalleryStatusInput,
  CreateGalleryItemInput,
  GalleryFilterOptions,
  GalleryItemDetail,
  GalleryListItem,
  GalleryListQueryParams,
  PaginatedResult,
  UpdateGalleryItemInput,
} from '../types/galleryManagement.types';

/** Drops undefined/null/'' so they never reach the query string. */
function cleanParams(params: GalleryListQueryParams): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

export const galleryManagementApi = {
  /** UC-GAL-01 / UC-GAL-02 — paged gallery list for the caller's campus. */
  async getGalleryItems(params: GalleryListQueryParams): Promise<PaginatedResult<GalleryListItem>> {
    const { data } = await httpClient.get<PaginatedResult<GalleryListItem>>(
      API_ENDPOINTS.gallery.list,
      { params: cleanParams(params) },
    );
    return data;
  },

  /** UC-GAL-03 — full detail of one gallery item. */
  async getGalleryItemDetails(galleryItemId: number): Promise<GalleryItemDetail> {
    const { data } = await httpClient.get<GalleryItemDetail>(API_ENDPOINTS.gallery.details, {
      params: { galleryItemId },
    });
    return data;
  },

  /** Area + location reference data for filter dropdowns and the upload picker. */
  async getFilterOptions(): Promise<GalleryFilterOptions> {
    const { data } = await httpClient.get<GalleryFilterOptions>(API_ENDPOINTS.gallery.filterOptions);
    return data;
  },

  /** UC-GAL-04 — create a gallery item with one or more media files (multipart). */
  async createGalleryItem(input: CreateGalleryItemInput): Promise<GalleryItemDetail> {
    const form = new FormData();
    form.append('title', input.title);
    form.append('description', input.description);
    form.append('locationId', String(input.locationId));
    form.append('status', input.status);
    input.files.forEach((file) => form.append('files', file));

    const { data } = await httpClient.post<GalleryItemDetail>(API_ENDPOINTS.gallery.create, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** UC-GAL-07 — edit metadata and reconcile media (multipart). */
  async updateGalleryItem(input: UpdateGalleryItemInput): Promise<GalleryItemDetail> {
    const form = new FormData();
    form.append('galleryItemId', String(input.galleryItemId));
    form.append('title', input.title);
    form.append('description', input.description);
    form.append('locationId', String(input.locationId));
    input.keepMediaIds.forEach((id) => form.append('keepMediaIds', String(id)));
    if (input.primaryMediaId != null) form.append('primaryMediaId', String(input.primaryMediaId));
    input.newFiles.forEach((file) => form.append('newFiles', file));

    const { data } = await httpClient.post<GalleryItemDetail>(API_ENDPOINTS.gallery.update, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** UC-GAL-05 / UC-GAL-06 — enable/disable (toggle status only). */
  async changeStatus(input: ChangeGalleryStatusInput): Promise<{ galleryItemId: number; status: string; message: string }> {
    const { data } = await httpClient.post(API_ENDPOINTS.gallery.changeStatus, input);
    return data;
  },
};
