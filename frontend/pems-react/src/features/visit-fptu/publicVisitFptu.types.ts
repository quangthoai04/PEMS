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
  galleryItemId: number;
  title: string;
  mediaKind: string;
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

export interface PublicGalleryItemDetail {
  campus: { campusId: number; campusCode: string; campusName: string; city?: string | null };
  area: { areaId: number; areaName: string };
  location: { locationId: number; locationName: string };
  galleryItem: {
    galleryItemId: number;
    title: string;
    description: string;
    mediaKind: string;
    status: string;
  };
  media: PublicGalleryMedia[];
}
