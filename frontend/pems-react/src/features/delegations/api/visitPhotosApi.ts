/**
 * API "Ảnh đoàn khách" của Student (visit_photos / visit_photo_folders — độc lập Gallery).
 * Backend là nguồn quyền duy nhất: mọi endpoint re-check ACTIVE Student + ACCEPTED participant
 * theo đúng visit_instance_id (chống IDOR); frontend chỉ ẩn/hiện UI theo cờ trả về.
 */
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  ConfirmFaceTagItem,
  MyVisitPhotoFoldersPage,
  TaggableGuest,
  VisitInstancePhotos,
  VisitPhotoFaceScan,
} from '../types/visitPhotos.types';

export const visitPhotosApi = {
  async myFolders(page = 1, pageSize = 10, search?: string, sortDirection = 'DESC', fromDate?: string, toDate?: string): Promise<MyVisitPhotoFoldersPage> {
    const { data } = await httpClient.get<MyVisitPhotoFoldersPage>(API_ENDPOINTS.visitPhotos.myFolders, {
      params: { page, pageSize, search: search || undefined, sortDirection, fromDate, toDate },
    });
    return data;
  },

  async byInstance(visitInstanceId: string | number): Promise<VisitInstancePhotos> {
    const { data } = await httpClient.get<VisitInstancePhotos>(
      API_ENDPOINTS.visitPhotos.byInstance(visitInstanceId));
    return data;
  },

  async upload(visitInstanceId: string | number, files: File[]): Promise<void> {
    const formData = new FormData();
    files.forEach((f) => formData.append('files', f));
    await httpClient.post(API_ENDPOINTS.visitPhotos.upload(visitInstanceId), formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  async remove(visitPhotoId: string | number, reason: string): Promise<void> {
    await httpClient.patch(API_ENDPOINTS.visitPhotos.remove(visitPhotoId), { reason });
  },

  // ── Face detection + manual guest tagging ────────────────────────────────────────────────
  async startFaceScan(visitPhotoId: string | number): Promise<VisitPhotoFaceScan> {
    const { data } = await httpClient.post<VisitPhotoFaceScan>(API_ENDPOINTS.visitPhotos.faceScans(visitPhotoId));
    return data;
  },

  async getFaceScans(visitPhotoId: string | number): Promise<VisitPhotoFaceScan[]> {
    const { data } = await httpClient.get<VisitPhotoFaceScan[]>(API_ENDPOINTS.visitPhotos.faceScans(visitPhotoId));
    return data;
  },

  async getFaceScanDetail(faceScanId: string | number): Promise<VisitPhotoFaceScan> {
    const { data } = await httpClient.get<VisitPhotoFaceScan>(API_ENDPOINTS.visitPhotos.faceScanDetail(faceScanId));
    return data;
  },

  async getTaggableGuests(visitInstanceId: string | number): Promise<TaggableGuest[]> {
    const { data } = await httpClient.get<TaggableGuest[]>(API_ENDPOINTS.visitPhotos.taggableGuests(visitInstanceId));
    return data;
  },

  async confirmFaceTags(
    faceScanId: string | number, rowVersion: number, faces: ConfirmFaceTagItem[],
  ): Promise<VisitPhotoFaceScan> {
    const { data } = await httpClient.post<VisitPhotoFaceScan>(
      API_ENDPOINTS.visitPhotos.confirmFaceTags(faceScanId), { rowVersion, faces },
    );
    return data;
  },
};
