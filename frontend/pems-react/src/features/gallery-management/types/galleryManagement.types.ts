/**
 * Types for the Staff Leader VisitFPTU Gallery management feature (UC-GAL-01..07).
 * Mirrors the backend DTOs under `PEMS.Application.Galleries`.
 */

export type GalleryStatus = 'PUBLISHED' | 'HIDDEN';
export type GalleryMediaKind = 'IMAGE' | 'VIDEO' | 'MIXED';
export type GalleryMediaType = 'IMAGE' | 'VIDEO';
/** How a media is sourced: an uploaded Drive file vs an external YouTube reference. */
export type GalleryMediaSourceType = 'UPLOADED_FILE' | 'YOUTUBE';
/** Content type of a gallery item — distinct from media kind. MEDIA = giới thiệu vị trí; VISIT_DELEGATION = đoàn khách. */
export type GalleryItemType = 'MEDIA' | 'VISIT_DELEGATION';

/** Legacy alias kept so the (empty) scaffold adapter still compiles. */
export type GalleryManagement = Record<string, never>;

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface GalleryPrimaryMedia {
  mediaId: number;
  fileId: number;
  mediaType: GalleryMediaType;
  sourceType: GalleryMediaSourceType;
  /** Proxy content URL for an uploaded file; null for a YouTube reference. */
  fileUrl?: string | null;
  thumbnailUrl?: string | null;
  youtubeVideoId?: string | null;
  embedUrl?: string | null;
  webViewUrl?: string | null;
}

export interface GalleryListItem {
  galleryItemId: number;
  areaId: number;
  areaName: string;
  locationId: number;
  locationName: string;
  title: string;
  description: string;
  itemType: GalleryItemType;
  itemTypeLabel: string;
  mediaKind: GalleryMediaKind;
  status: GalleryStatus;
  createdAt: string;
  createdByName?: string | null;
  primaryMedia?: GalleryPrimaryMedia | null;
}

export interface GalleryMedia {
  mediaId: number;
  fileId: number;
  mediaType: GalleryMediaType;
  sourceType: GalleryMediaSourceType;
  /** Proxy content URL for an uploaded file; null for a YouTube reference. */
  fileUrl?: string | null;
  thumbnailUrl?: string | null;
  youtubeVideoId?: string | null;
  embedUrl?: string | null;
  webViewUrl?: string | null;
  isPrimary: boolean;
  caption?: string | null;
  altText?: string | null;
  displayOrder: number;
}

/** One audio recording's metadata + authenticated proxy URL (management detail/edit screens). */
export interface GalleryAudioInfo {
  fileId: number;
  fileName: string;
  mimeType?: string | null;
  fileSize?: number | null;
  /** Authenticated content proxy URL (`/api/files/{id}/content`). */
  url: string;
}

/** The item's bilingual content (VI/EN descriptions + audio). */
export interface GalleryItemContent {
  descriptionVi: string;
  audioVi?: GalleryAudioInfo | null;
  descriptionEn: string;
  audioEn?: GalleryAudioInfo | null;
}

export interface GalleryItemDetail {
  galleryItemId: number;
  title: string;
  /** English title — present ONLY when its translation is READY and non-blank. */
  titleEn?: string | null;
  content: GalleryItemContent;
  itemType: GalleryItemType;
  itemTypeLabel: string;
  status: GalleryStatus;
  mediaKind: GalleryMediaKind;
  area: { areaId: number; areaName: string };
  location: { locationId: number; locationName: string };
  campus: { campusId: number; campusCode: string; campusName: string };
  createdAt: string;
  createdByName?: string | null;
  updatedAt?: string | null;
  updatedByName?: string | null;
  media: GalleryMedia[];
  message?: string | null;
  /** Set when the save succeeded but the VI → EN auto-translation failed — show as a WARNING toast. */
  translationWarning?: string | null;
}

export interface GalleryLocationOption {
  locationId: number;
  locationName: string;
  status: 'ACTIVE' | 'INACTIVE';
}

export interface GalleryAreaOption {
  areaId: number;
  areaName: string;
  areaNameEn?: string | null;
  status: 'ACTIVE' | 'INACTIVE';
  coverFileId?: number | null;
  coverUrl?: string | null;
  /** IMAGE (legacy areas) or VIDEO (MP4 cover) — lets the edit modal preview the right element. */
  coverMediaType?: AreaCoverMediaType;
  locations: GalleryLocationOption[];
}

/** Summary of one area as returned inside a create/update response — used for the optimistic
 *  dropdown upsert (no cover/locations payload). */
export interface GalleryFilterArea {
  areaId: number;
  areaName: string;
  areaNameEn?: string | null;
  status: 'ACTIVE' | 'INACTIVE';
}

export interface GalleryFilterOptions {
  areas: GalleryAreaOption[];
}

export interface GalleryListQueryParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  areaId?: number;
  locationId?: number;
  mediaKind?: GalleryMediaKind | '';
  itemType?: GalleryItemType | '';
  status?: GalleryStatus | '';
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CreateGalleryItemInput {
  title: string;
  /** Previewed/manual English title. Empty → backend auto-translates on save. */
  titleEn?: string | null;
  titleTranslationOrigin?: TranslationOrigin;
  titleTranslationSourceHash?: string | null;
  /** All four bilingual fields are mandatory. */
  descriptionVi: string;
  audioVi: File;
  descriptionEn: string;
  audioEn: File;
  locationId: number;
  itemType: GalleryItemType;
  status: GalleryStatus;
  files: File[];
  /** YouTube video URLs to attach as external VIDEO media (0..N). */
  youtubeUrls?: string[];
  /** `upload:{i}` or `youtube:{i}` — which media is primary. */
  primaryMediaKey?: string | null;
}

export interface UpdateGalleryItemInput {
  galleryItemId: number;
  title: string;
  /** Previewed/manual English title. Empty → kept/auto-translated server-side. */
  titleEn?: string | null;
  titleTranslationOrigin?: TranslationOrigin;
  titleTranslationSourceHash?: string | null;
  descriptionVi: string;
  descriptionEn: string;
  /** Replacement audio; omit to keep the current recording. Audio can never be removed. */
  newAudioVi?: File | null;
  newAudioEn?: File | null;
  locationId: number;
  itemType: GalleryItemType;
  keepMediaIds: number[];
  newFiles: File[];
  primaryMediaId?: number | null;
  /** New YouTube video URLs to add as external VIDEO media (0..N). */
  youtubeUrls?: string[];
  /** `existing:{mediaId}`, `upload:{i}` or `youtube:{i}` — which media is primary. */
  primaryMediaKey?: string | null;
}

export interface ChangeGalleryStatusInput {
  galleryItemId: number;
  status: GalleryStatus;
}

// ── Quản lý khu vực (area/location management, UC-LOC-01..09) ──

export type GalleryLocationStatus = 'ACTIVE' | 'INACTIVE';
export type GalleryLocationMode = 'EXISTING_AREA' | 'NEW_AREA';
/** How an area cover is rendered: legacy image vs the new MP4 area-cover video. */
export type AreaCoverMediaType = 'IMAGE' | 'VIDEO';

export interface GalleryLocationListItem {
  locationId: number;
  areaId: number;
  areaName: string;
  areaCoverFileId?: number | null;
  areaCoverUrl?: string | null;
  /** IMAGE (legacy areas) or VIDEO (MP4 cover). */
  areaCoverMediaType?: AreaCoverMediaType;
  locationName: string;
  locationCoverFileId?: number | null;
  locationCoverUrl?: string | null;
  status: GalleryLocationStatus;
  createdAt: string;
  updatedAt?: string | null;
  // A location may hold 0, 1 or many gallery items — reported as aggregate counts.
  hasGalleryItems: boolean;
  galleryItemCount: number;
  publishedGalleryItemCount: number;
  hiddenGalleryItemCount: number;
}

export interface GalleryLocationDetail extends GalleryLocationListItem {
  mediaCount?: number;
  visitDelegationCount?: number;
  message?: string | null;
  /** Set when the save succeeded but the VI → EN auto-translation failed — show as a WARNING toast. */
  translationWarning?: string | null;
  /** Summary of the (possibly renamed) area — used for the optimistic dropdown upsert. */
  area?: GalleryFilterArea | null;
  /** Summary of the saved location. */
  location?: {
    locationId: number;
    locationName: string;
    locationNameEn?: string | null;
    status: GalleryLocationStatus;
  } | null;
}

// ── VI → EN translation preview (Dịch sang EN trước khi lưu) ──

export type TranslationStatus = 'PENDING' | 'READY' | 'FAILED' | 'OUTDATED';

/** Where the EN value in the save payload came from. */
export type TranslationOrigin = 'NONE' | 'AUTO_PREVIEW' | 'MANUAL' | 'AUTO_ON_SAVE';

export type TranslationPreviewMode = 'EXISTING_AREA' | 'NEW_AREA' | 'EDIT';

export interface TranslationPreviewField {
  sourceText: string;
  sourceHash: string;
  translatedText: string;
}

export interface PreviewLocationTranslationInput {
  mode: TranslationPreviewMode;
  areaNameVi?: string | null;
  locationNameVi?: string | null;
  /** EDIT mode only — which fields actually need translating. */
  includeArea?: boolean;
  includeLocation?: boolean;
}

export interface GalleryLocationTranslationPreview {
  area?: TranslationPreviewField | null;
  location?: TranslationPreviewField | null;
}

/** Preview request for the gallery item title ("Dịch sang EN" in the item create/edit modals). */
export interface PreviewItemTranslationInput {
  entityType: 'GALLERY_ITEM';
  field: 'TITLE';
  /** Set by the EDIT modal so an unchanged READY title is served from the DB (0 Google calls). */
  entityId?: number | null;
  sourceText: string;
}

/** Preview response: normalized VI source + hash + the EN, and where it was served from. */
export interface GalleryItemTranslationPreview extends TranslationPreviewField {
  servedFrom: 'GOOGLE' | 'DATABASE';
}

/** Authoritative detail of one location + its area, loaded when opening the edit modal. */
export interface GalleryLocationEditDetail {
  locationId: number;
  areaId: number;

  areaName: string;
  areaNameEn?: string | null;
  areaTranslationStatus: TranslationStatus;
  areaCoverFileId?: number | null;
  areaCoverUrl?: string | null;
  areaCoverMediaType?: AreaCoverMediaType | null;

  locationName: string;
  locationNameEn?: string | null;
  locationTranslationStatus: TranslationStatus;
  locationCoverFileId?: number | null;
  locationCoverUrl?: string | null;

  updatedAt?: string | null;
}

export interface GalleryLocationListQueryParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  areaId?: number;
  status?: GalleryLocationStatus | '';
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CreateGalleryLocationInput {
  mode: GalleryLocationMode;
  areaId?: number | null;
  newAreaName?: string | null;
  /** Previewed/manual EN of the NEW area name (NEW_AREA mode only). */
  newAreaNameEn?: string | null;
  areaTranslationOrigin?: TranslationOrigin;
  areaTranslationSourceHash?: string | null;
  locationName: string;
  /** Previewed/manual EN of the location name. Empty → backend auto-translates on save. */
  locationNameEn?: string | null;
  locationTranslationOrigin?: TranslationOrigin;
  locationTranslationSourceHash?: string | null;
  /** MP4 area cover video. Required when mode = NEW_AREA. */
  areaCoverVideo?: File | null;
  /** Required on create. */
  locationCoverImage?: File | null;
}

/** Direct edit of the CURRENT area + location (no mode, no move, no new area). */
export interface UpdateGalleryLocationInput {
  locationId: number;
  areaName: string;
  areaNameEn?: string | null;
  areaTranslationOrigin?: TranslationOrigin;
  areaTranslationSourceHash?: string | null;
  locationName: string;
  locationNameEn?: string | null;
  locationTranslationOrigin?: TranslationOrigin;
  locationTranslationSourceHash?: string | null;
  /** Optional replacement MP4 for the area cover (kept when omitted). */
  areaCoverVideo?: File | null;
  /** Optional replacement image for the location cover (kept when omitted). */
  locationCoverImage?: File | null;
}

export interface ChangeGalleryLocationStatusInput {
  locationId: number;
  status: GalleryLocationStatus;
}
