// Types for the public VisitFPTU Gallery display layer (UC_Public_VisitFPTU_Gallery).
// These mirror the anonymous read DTOs returned by /api/public/visit-fptu/*.

export interface PublicCampus {
  campusId: number;
  campusCode: string;
  campusName: string;
  city?: string | null;
  coverFileId?: number | null;
  coverUrl?: string | null;
}

export interface PublicGalleryLocation {
  locationId: number;
  locationName: string;
  displayOrder: number;
  /** Lead item shown as the location's nav thumbnail (a location may hold many items). */
  galleryItemId: number;
  title: string;
  mediaKind: string;
  /** How many public-visible items this location carries (>= 1). */
  publicGalleryItemCount: number;
  primaryMediaUrl?: string | null;
}

export interface PublicGalleryArea {
  areaId: number;
  areaName: string;
  displayOrder: number;
  locations: PublicGalleryLocation[];
}

export interface PublicGalleryNavigation {
  campus: PublicCampus;
  areas: PublicGalleryArea[];
}

export interface PublicGalleryMedia {
  mediaId: number;
  fileId: number;
  mediaType: string; // IMAGE | VIDEO
  url: string;
  thumbnailUrl?: string | null;
  caption?: string | null;
  altText?: string | null;
  isPrimary: boolean;
  displayOrder: number;
}

export interface PublicGalleryItemSummary {
  galleryItemId: number;
  title: string;
  description: string;
  mediaKind: string;
  status: string;
}

// ── Tier 1: Location album grid ──
/** One card in the location grid — a gallery item shown by its primary media. */
export interface PublicGalleryGridItem {
  galleryItemId: number;
  title: string;
  descriptionPreview: string;
  mediaKind: string;
  primaryMedia: PublicGalleryMedia | null;
}

/** The album grid of one location: every public-visible item by its primary media. */
export interface PublicLocationGalleryGrid {
  campus: { campusId: number; campusCode: string; campusName: string; city?: string | null };
  area: { areaId: number; areaName: string };
  location: { locationId: number; locationName: string };
  items: PublicGalleryGridItem[];
}

// ── Tier 2: Gallery item detail ──
/** One gallery item with its full ordered media list (the detail screen). */
export interface PublicGalleryItemDetail {
  campus: { campusId: number; campusCode: string; campusName: string; city?: string | null };
  area: { areaId: number; areaName: string };
  location: { locationId: number; locationName: string };
  galleryItem: PublicGalleryItemSummary;
  media: PublicGalleryMedia[];
}
