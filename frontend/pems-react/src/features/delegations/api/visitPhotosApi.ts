/**
 * API "Ảnh đoàn khách" của Student (visit_photos / visit_photo_folders — độc lập Gallery).
 * Backend là nguồn quyền duy nhất: mọi endpoint re-check ACTIVE Student + ACCEPTED participant
 * theo đúng visit_instance_id (chống IDOR); frontend chỉ ẩn/hiện UI theo cờ trả về.
 */
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type { MyVisitPhotoFoldersPage, VisitInstancePhotos } from '../types/visitPhotos.types';

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
};
