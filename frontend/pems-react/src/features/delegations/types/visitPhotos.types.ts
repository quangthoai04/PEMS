/**
 * Student visit photos (ảnh đoàn khách) — mirrors PEMS.Application.Delegations.VisitPhotos DTOs.
 * Ảnh trả về qua proxy URL /api/files/{fileId}/content; link Drive (webViewUrl) do backend cấp.
 */

export interface VisitInstancePhotoItem {
  visitPhotoId: number;
  fileId: number;
  fileName: string;
  url: string;
  caption?: string | null;
  uploadedAt: string;
  uploadedByName: string;
  uploadedByMe: boolean;
  canRemove: boolean;
}

export interface VisitInstancePhotos {
  visitInstanceId: number;
  delegationName: string;
  campusName?: string | null;
  folderName?: string | null;
  folderWebViewUrl?: string | null;
  canUpload: boolean;
  photos: VisitInstancePhotoItem[];
}

export interface MyVisitPhotoFolderItem {
  visitInstanceId: number;
  delegationName: string;
  campusName?: string | null;
  folderName?: string | null;
  activePhotoCount: number;
  instanceStatus: string;
  plannedStartAt: string;
}

export interface MyVisitPhotoFoldersPage {
  items: MyVisitPhotoFolderItem[];
  totalCount: number;
  pageIndex: number;
  pageSize: number;
  totalPages: number;
}

/**
 * Face detection + manual guest tagging (Sau tiếp khách → Lưu trữ ảnh đoàn khách → Scan và gán tên
 * khuôn mặt). Mirrors PEMS.Application.Delegations.VisitPhotos.FaceScans DTOs. No auto identity
 * recognition, no face embedding, no emotion data — bounding boxes are normalized 0..1 fractions of
 * the image's width/height (draw with `left/top/width/height = value * 100%`).
 */
export type FaceScanStatus = 'PENDING' | 'PROCESSING' | 'SUCCEEDED' | 'FAILED' | 'CONFIRMED';
export type FaceReviewStatus = 'DETECTED' | 'CONFIRMED' | 'IGNORED';
export type GuestMemberType = 'GUEST' | 'EXTERNAL_SUPPORT';

export interface VisitPhotoFaceDetection {
  faceDetectionId: number;
  faceIndex: number;
  boundingBoxX: number;
  boundingBoxY: number;
  boundingBoxWidth: number;
  boundingBoxHeight: number;
  detectionConfidence: number;
  reviewStatus: FaceReviewStatus;
  guestMemberId?: number | null;
  guestFullName?: string | null;
  faceTagId?: number | null;
  reviewedAt?: string | null;
  reviewedByName?: string | null;
}

export interface VisitPhotoFaceScan {
  faceScanId: number;
  visitPhotoId: number;
  status: FaceScanStatus;
  providerName: string;
  imageWidth?: number | null;
  imageHeight?: number | null;
  detectedFaceCount: number;
  reviewedFaceCount: number;
  ignoredFaceCount: number;
  errorCode?: string | null;
  errorMessage?: string | null;
  requestedAt: string;
  requestedByName?: string | null;
  completedAt?: string | null;
  confirmedAt?: string | null;
  confirmedByName?: string | null;
  rowVersion: number;
  detections: VisitPhotoFaceDetection[];
}

export interface TaggableGuest {
  guestMemberId: number;
  fullName: string;
  memberType: GuestMemberType;
  organization: string;
  jobTitle: string;
  nationality: string;
}

export interface ConfirmFaceTagItem {
  faceDetectionId: number;
  guestMemberId: number | null;
  ignored: boolean;
}
