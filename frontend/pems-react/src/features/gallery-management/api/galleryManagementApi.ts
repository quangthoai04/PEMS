import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  ChangeGalleryLocationStatusInput,
  ChangeGalleryStatusInput,
  CreateGalleryItemInput,
  CreateGalleryLocationInput,
  GalleryFilterOptions,
  GalleryItemDetail,
  GalleryListItem,
  GalleryListQueryParams,
  GalleryLocationDetail,
  GalleryLocationListItem,
  GalleryLocationListQueryParams,
  PaginatedResult,
  UpdateGalleryItemInput,
  UpdateGalleryLocationInput,
} from '../types/galleryManagement.types';

/** Drops undefined/null/'' so they never reach the query string. */
function cleanParams(params: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

/** Builds the shared multipart body for create/update location (area cover VIDEO + location cover image). */
function buildLocationForm(input: CreateGalleryLocationInput): FormData {
  const form = new FormData();
  form.append('mode', input.mode);
  if (input.areaId != null) form.append('areaId', String(input.areaId));
  if (input.newAreaName) form.append('newAreaName', input.newAreaName);
  form.append('locationName', input.locationName);
  if (input.areaCoverVideo) form.append('areaCoverVideo', input.areaCoverVideo);
  if (input.locationCoverImage) form.append('locationCoverImage', input.locationCoverImage);
  return form;
}

export const galleryManagementApi = {
  /** UC-GAL-01 / UC-GAL-02 — paged gallery list for the caller's campus. */
  async getGalleryItems(params: GalleryListQueryParams): Promise<PaginatedResult<GalleryListItem>> {
    const { data } = await httpClient.get<PaginatedResult<GalleryListItem>>(
      API_ENDPOINTS.gallery.list,
      { params: cleanParams(params as unknown as Record<string, unknown>) },
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

  /** UC-GAL-04 — create a gallery item with bilingual content + audio + media (multipart). */
  async createGalleryItem(input: CreateGalleryItemInput): Promise<GalleryItemDetail> {
    const form = new FormData();
    form.append('title', input.title);
    form.append('descriptionVi', input.descriptionVi);
    form.append('descriptionEn', input.descriptionEn);
    form.append('audioVi', input.audioVi);
    form.append('audioEn', input.audioEn);
    form.append('locationId', String(input.locationId));
    form.append('itemType', input.itemType);
    form.append('status', input.status);
    input.files.forEach((file) => form.append('files', file));
    (input.youtubeUrls ?? []).forEach((url) => form.append('youtubeUrls', url));
    if (input.primaryMediaKey) form.append('primaryMediaKey', input.primaryMediaKey);

    const { data } = await httpClient.post<GalleryItemDetail>(API_ENDPOINTS.gallery.create, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** UC-GAL-07 — edit metadata + bilingual content and reconcile media (multipart). */
  async updateGalleryItem(input: UpdateGalleryItemInput): Promise<GalleryItemDetail> {
    const form = new FormData();
    form.append('galleryItemId', String(input.galleryItemId));
    form.append('title', input.title);
    form.append('descriptionVi', input.descriptionVi);
    form.append('descriptionEn', input.descriptionEn);
    if (input.newAudioVi) form.append('newAudioVi', input.newAudioVi);
    if (input.newAudioEn) form.append('newAudioEn', input.newAudioEn);
    form.append('locationId', String(input.locationId));
    form.append('itemType', input.itemType);
    input.keepMediaIds.forEach((id) => form.append('keepMediaIds', String(id)));
    if (input.primaryMediaId != null) form.append('primaryMediaId', String(input.primaryMediaId));
    input.newFiles.forEach((file) => form.append('newFiles', file));
    (input.youtubeUrls ?? []).forEach((url) => form.append('youtubeUrls', url));
    if (input.primaryMediaKey) form.append('primaryMediaKey', input.primaryMediaKey);

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

  // ── Quản lý khu vực (UC-LOC-01..09) ──

  /** UC-LOC-01/02/03 — paged area/location list for the caller's campus. */
  async getLocations(params: GalleryLocationListQueryParams): Promise<PaginatedResult<GalleryLocationListItem>> {
    const { data } = await httpClient.get<PaginatedResult<GalleryLocationListItem>>(
      API_ENDPOINTS.gallery.locationList,
      { params: cleanParams(params as unknown as Record<string, unknown>) },
    );
    return data;
  },

  /** UC-LOC-04/05 — add a location (into an existing area or a brand-new one) with cover images (multipart). */
  async createLocation(input: CreateGalleryLocationInput): Promise<GalleryLocationDetail> {
    const form = buildLocationForm(input);
    const { data } = await httpClient.post<GalleryLocationDetail>(API_ENDPOINTS.gallery.locationCreate, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** UC-LOC-06/07 — rename a location and/or move it to another area, optionally replacing cover images (multipart). */
  async updateLocation(input: UpdateGalleryLocationInput): Promise<GalleryLocationDetail> {
    const form = buildLocationForm(input);
    form.append('locationId', String(input.locationId));
    const { data } = await httpClient.post<GalleryLocationDetail>(API_ENDPOINTS.gallery.locationUpdate, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** UC-LOC-08/09 — enable/disable a location. */
  async changeLocationStatus(input: ChangeGalleryLocationStatusInput): Promise<GalleryLocationDetail> {
    const { data } = await httpClient.post<GalleryLocationDetail>(API_ENDPOINTS.gallery.locationChangeStatus, input);
    return data;
  },
};
