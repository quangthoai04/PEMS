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
  /** EverAI narration status for the item's current description (drives the AUDIO column). */
  audioStatus: GalleryItemTtsManagementStatus;
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

export interface GalleryItemDetail {
  galleryItemId: number;
  title: string;
  description: string;
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
}

export interface GalleryLocationOption {
  locationId: number;
  locationName: string;
  status: 'ACTIVE' | 'INACTIVE';
}

export interface GalleryAreaOption {
  areaId: number;
  areaName: string;
  status: 'ACTIVE' | 'INACTIVE';
  coverFileId?: number | null;
  coverUrl?: string | null;
  /** IMAGE (legacy areas) or VIDEO (MP4 cover) — lets the edit modal preview the right element. */
  coverMediaType?: AreaCoverMediaType;
  locations: GalleryLocationOption[];
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
  audioStatus?: GalleryItemTtsManagementStatus | '';
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CreateGalleryItemInput {
  title: string;
  description: string;
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
  description: string;
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

// ── EverAI TTS narration (Staff Leader dashboard) ──

export type GalleryItemTtsManagementStatus =
  | 'READY'
  | 'PROCESSING'
  | 'FAILED'
  | 'STALE'
  | 'NOT_CREATED'
  | 'DISABLED'
  | 'INVALID_DESCRIPTION';

/** Management status of an item's narration; drives the badge + enables the "Tạo lại audio" button. */
export interface GalleryItemTtsStatus {
  status: GalleryItemTtsManagementStatus;
  canRegenerate: boolean;
  audioUrl?: string | null;
  voiceCode?: string | null;
  audioType?: string | null;
  errorMessage?: string | null;
}

/** Result of the "Tạo lại audio" action. UP_TO_DATE = current description already has matching audio. */
export interface GalleryItemTtsRegenerateResult {
  status: 'PROCESSING' | 'UP_TO_DATE' | string;
  message?: string | null;
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
  locationName: string;
  /** MP4 area cover video. Required when mode = NEW_AREA; optional on edit of an existing area (replaces it). */
  areaCoverVideo?: File | null;
  /** Required on create; optional on edit (kept when omitted). */
  locationCoverImage?: File | null;
}

export interface UpdateGalleryLocationInput extends CreateGalleryLocationInput {
  locationId: number;
}

export interface ChangeGalleryLocationStatusInput {
  locationId: number;
  status: GalleryLocationStatus;
}
