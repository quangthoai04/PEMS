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
  /** Location cover image (gallery_locations.cover_file_id) — the Area Showcase thumbnail rail. */
  locationCoverFileId?: number | null;
  locationCoverUrl?: string | null;
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
  /** Area cover image (gallery_areas.cover_file_id) — the Area Showcase fullscreen background. */
  areaCoverFileId?: number | null;
  areaCoverUrl?: string | null;
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
  /** UPLOADED_FILE | YOUTUBE — render an iframe for YOUTUBE, else img/video. */
  sourceType: 'UPLOADED_FILE' | 'YOUTUBE' | string;
  /** Scoped proxy URL for an uploaded file; null for a YouTube reference. */
  url?: string | null;
  thumbnailUrl?: string | null;
  youtubeVideoId?: string | null;
  embedUrl?: string | null;
  webViewUrl?: string | null;
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

// ── Location Showcase: MEDIA column + VISIT_DELEGATION row ──
/** One gallery item shown by its primary media (used for both the MEDIA column and the delegation row). */
export interface PublicGalleryShowcaseItem {
  galleryItemId: number;
  title: string;
  itemType: 'MEDIA' | 'VISIT_DELEGATION' | string;
  mediaKind: string;
  primaryMedia: PublicGalleryMedia | null;
}

/** The Location Showcase payload: the location's MEDIA items (column) + VISIT_DELEGATION items (row). */
export interface PublicLocationShowcase {
  campus: { campusId: number; campusCode: string; campusName: string; city?: string | null };
  area: { areaId: number; areaName: string };
  location: { locationId: number; locationName: string };
  mediaItems: PublicGalleryShowcaseItem[];
  visitDelegationItems: PublicGalleryShowcaseItem[];
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

// ── EverAI TTS narration (speaker icon) ──
export type PublicTtsAudioStatus =
  | 'READY'
  | 'PROCESSING'
  | 'NOT_CREATED'
  | 'TEMPORARILY_UNAVAILABLE'
  | 'DISABLED'
  | 'INVALID_DESCRIPTION';

/** Response of the ensure/poll TTS endpoints. audioUrl (READY only) is a PEMS-served proxy URL. */
export interface PublicTtsAudio {
  status: PublicTtsAudioStatus;
  audioUrl?: string | null;
  message?: string | null;
  voiceCode?: string | null;
  audioType?: string | null;
}
