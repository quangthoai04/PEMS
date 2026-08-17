import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  ChangeGalleryLocationStatusInput,
  ChangeGalleryStatusInput,
  CreateGalleryItemInput,
  CreateGalleryLocationInput,
  DeleteGalleryItemInput,
  DeleteGalleryItemResult,
  GalleryFilterOptions,
  GalleryItemDetail,
  GalleryItemTranslationPreview,
  GalleryListItem,
  GalleryListQueryParams,
  GalleryLocationDetail,
  GalleryLocationEditDetail,
  GalleryLocationListItem,
  GalleryLocationListQueryParams,
  GalleryLocationTranslationPreview,
  PaginatedResult,
  PreviewItemTranslationInput,
  PreviewLocationTranslationInput,
  UpdateGalleryItemInput,
  UpdateGalleryLocationInput,
} from '../types/galleryManagement.types';

/** Drops undefined/null/'' so they never reach the query string. */
function cleanParams(params: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

/** Builds the multipart body for CREATE location (mode + previewed/manual EN + covers). */
function buildCreateLocationForm(input: CreateGalleryLocationInput): FormData {
  const form = new FormData();
  form.append('mode', input.mode);
  if (input.areaId != null) form.append('areaId', String(input.areaId));
  if (input.newAreaName) form.append('newAreaName', input.newAreaName);
  if (input.newAreaNameEn) form.append('newAreaNameEn', input.newAreaNameEn);
  if (input.areaTranslationOrigin) form.append('areaTranslationOrigin', input.areaTranslationOrigin);
  if (input.areaTranslationSourceHash) form.append('areaTranslationSourceHash', input.areaTranslationSourceHash);
  form.append('locationName', input.locationName);
  if (input.locationNameEn) form.append('locationNameEn', input.locationNameEn);
  if (input.locationTranslationOrigin) form.append('locationTranslationOrigin', input.locationTranslationOrigin);
  if (input.locationTranslationSourceHash) form.append('locationTranslationSourceHash', input.locationTranslationSourceHash);
  if (input.areaCoverVideo) form.append('areaCoverVideo', input.areaCoverVideo);
  if (input.locationCoverImage) form.append('locationCoverImage', input.locationCoverImage);
  return form;
}

/** Builds the multipart body for the DIRECT area/location edit (no mode — updates the current rows). */
function buildUpdateLocationForm(input: UpdateGalleryLocationInput): FormData {
  const form = new FormData();
  form.append('locationId', String(input.locationId));
  form.append('areaName', input.areaName);
  if (input.areaNameEn) form.append('areaNameEn', input.areaNameEn);
  if (input.areaTranslationOrigin) form.append('areaTranslationOrigin', input.areaTranslationOrigin);
  if (input.areaTranslationSourceHash) form.append('areaTranslationSourceHash', input.areaTranslationSourceHash);
  form.append('locationName', input.locationName);
  if (input.locationNameEn) form.append('locationNameEn', input.locationNameEn);
  if (input.locationTranslationOrigin) form.append('locationTranslationOrigin', input.locationTranslationOrigin);
  if (input.locationTranslationSourceHash) form.append('locationTranslationSourceHash', input.locationTranslationSourceHash);
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
    if (input.titleEn) form.append('titleEn', input.titleEn);
    if (input.titleTranslationOrigin) form.append('titleTranslationOrigin', input.titleTranslationOrigin);
    if (input.titleTranslationSourceHash) form.append('titleTranslationSourceHash', input.titleTranslationSourceHash);
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
    if (input.titleEn) form.append('titleEn', input.titleEn);
    if (input.titleTranslationOrigin) form.append('titleTranslationOrigin', input.titleTranslationOrigin);
    if (input.titleTranslationSourceHash) form.append('titleTranslationSourceHash', input.titleTranslationSourceHash);
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

  /** "Xóa nội dung Gallery" — permanent for the user (server-side soft delete). Not reversible in UI. */
  async deleteGalleryItem(input: DeleteGalleryItemInput): Promise<DeleteGalleryItemResult> {
    const { data } = await httpClient.post<DeleteGalleryItemResult>(API_ENDPOINTS.gallery.deleteItem, input);
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
    const form = buildCreateLocationForm(input);
    const { data } = await httpClient.post<GalleryLocationDetail>(API_ENDPOINTS.gallery.locationCreate, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** Direct edit of the location AND its current area (no move, no new area) — multipart, covers optional. */
  async updateLocation(input: UpdateGalleryLocationInput): Promise<GalleryLocationDetail> {
    const form = buildUpdateLocationForm(input);
    const { data } = await httpClient.post<GalleryLocationDetail>(API_ENDPOINTS.gallery.locationUpdate, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return data;
  },

  /** Authoritative detail of one location + its area for the edit modal. */
  async getLocationDetail(locationId: number): Promise<GalleryLocationEditDetail> {
    const { data } = await httpClient.get<GalleryLocationEditDetail>(API_ENDPOINTS.gallery.locationDetail, {
      params: { locationId },
    });
    return data;
  },

  /** "Dịch sang EN" preview — ONE backend/Google call; nothing is saved. Google is never called from React. */
  async previewLocationTranslation(
    input: PreviewLocationTranslationInput,
  ): Promise<GalleryLocationTranslationPreview> {
    const { data } = await httpClient.post<GalleryLocationTranslationPreview>(
      API_ENDPOINTS.gallery.locationPreviewTranslation,
      input,
    );
    return data;
  },

  /** "Dịch sang EN" preview for the item title — at most ONE backend/Google call (an unchanged READY
   *  title is served from the DB); nothing is saved. Google is never called from React. */
  async previewItemTranslation(
    input: PreviewItemTranslationInput,
  ): Promise<GalleryItemTranslationPreview> {
    const { data } = await httpClient.post<GalleryItemTranslationPreview>(
      API_ENDPOINTS.gallery.itemPreviewTranslation,
      input,
    );
    return data;
  },

  /** UC-LOC-08/09 — enable/disable a location. */
  async changeLocationStatus(input: ChangeGalleryLocationStatusInput): Promise<GalleryLocationDetail> {
    const { data } = await httpClient.post<GalleryLocationDetail>(API_ENDPOINTS.gallery.locationChangeStatus, input);
    return data;
  },
};
