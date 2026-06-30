import httpClient from '../../../shared/api/httpClient';
import { DocumentFilterParams, DocumentListItem, PaginatedResponse } from '../types/documents.types';

export const documentsApi = {
  getDocuments: async (params: DocumentFilterParams): Promise<PaginatedResponse<DocumentListItem>> => {
    // Clean up 'All' values before sending to backend
    const cleanParams = { ...params };
    if (cleanParams.status === 'All') delete cleanParams.status;
    if (cleanParams.ownerType === 'All') delete cleanParams.ownerType;
    if (cleanParams.storageProvider === 'All') delete cleanParams.storageProvider;
    
    const response = await httpClient.get('/documents/searchdocuments', { params: cleanParams });
    return {
      ...response.data,
      totalCount: response.data.totalItems || response.data.totalCount || 0
    };
  },
  
  getDocument: async (documentId: number): Promise<DocumentDetailResponse> => {
    const response = await httpClient.get(`/documents/${documentId}`);
    return response.data;
  }
};
