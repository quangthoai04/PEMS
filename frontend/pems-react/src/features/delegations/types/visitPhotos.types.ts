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
