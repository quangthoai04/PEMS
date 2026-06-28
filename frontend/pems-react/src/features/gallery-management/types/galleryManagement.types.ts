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
