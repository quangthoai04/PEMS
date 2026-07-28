/**
 * API "Tài liệu" của widget VisitDuringTab — upload 1 file, gắn 0..N đối tác. Backend là nguồn
 * quyền duy nhất (Host/Admin/Staff-Leader/participant của đúng visit_instance_id) và tự resolve
 * hoặc tạo mới từng đối tác theo tên; không có đối tác nào được chọn thì lưu loại "Theo đoàn khách".
 */
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';

export interface UploadedVisitDocumentPartner {
  documentId: number;
  partnerId: number;
  partnerName: string;
}

export interface UploadVisitDocumentResult {
  fileId: number;
  fileName: string;
  url: string;
  partners: UploadedVisitDocumentPartner[];
  visitDocumentId: number | null;
}

export const visitDocumentsApi = {
  async upload(
    visitInstanceId: string | number,
    file: File,
    title: string,
    partnerNames: string[],
  ): Promise<UploadVisitDocumentResult> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('title', title);
    partnerNames.forEach((name) => formData.append('partnerNames', name));
    const { data } = await httpClient.post<UploadVisitDocumentResult>(
      API_ENDPOINTS.visitDocuments.upload(visitInstanceId), formData,
      { headers: { 'Content-Type': 'multipart/form-data' } },
    );
    return data;
  },
};
