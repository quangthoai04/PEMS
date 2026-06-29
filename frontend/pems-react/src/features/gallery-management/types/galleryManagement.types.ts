/**
 * Types for the Staff Leader VisitFPTU Gallery management feature (UC-GAL-01..07).
 * Mirrors the backend DTOs under `PEMS.Application.Galleries`.
 */

export type GalleryStatus = 'PUBLISHED' | 'HIDDEN';
export type GalleryMediaKind = 'IMAGE' | 'VIDEO' | 'MIXED';
export type GalleryMediaType = 'IMAGE' | 'VIDEO';

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
  fileUrl: string;
  thumbnailUrl?: string | null;
}

export interface GalleryListItem {
  galleryItemId: number;
  areaId: number;
  areaName: string;
  locationId: number;
  locationName: string;
  title: string;
  description: string;
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
  fileUrl: string;
  thumbnailUrl?: string | null;
  isPrimary: boolean;
  caption?: string | null;
  altText?: string | null;
  displayOrder: number;
}

export interface GalleryItemDetail {
  galleryItemId: number;
  title: string;
  description: string;
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
  status?: GalleryStatus | '';
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CreateGalleryItemInput {
  title: string;
  description: string;
  locationId: number;
  status: GalleryStatus;
  files: File[];
}

export interface UpdateGalleryItemInput {
  galleryItemId: number;
  title: string;
  description: string;
  locationId: number;
  keepMediaIds: number[];
  newFiles: File[];
  primaryMediaId?: number | null;
}

export interface ChangeGalleryStatusInput {
  galleryItemId: number;
  status: GalleryStatus;
}

// ── Quản lý khu vực (area/location management, UC-LOC-01..09) ──

export type GalleryLocationStatus = 'ACTIVE' | 'INACTIVE';
export type GalleryLocationMode = 'EXISTING_AREA' | 'NEW_AREA';

export interface GalleryLocationListItem {
  locationId: number;
  areaId: number;
  areaName: string;
  locationName: string;
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
}

export interface UpdateGalleryLocationInput extends CreateGalleryLocationInput {
  locationId: number;
}

export interface ChangeGalleryLocationStatusInput {
  locationId: number;
  status: GalleryLocationStatus;
}
