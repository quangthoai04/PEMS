import httpClient from '../../../shared/api/httpClient';
import { MinutesListResponse, MinutesDetail, MinutesFilterParams } from './types';

export const minutesApi = {
  list: async (params: MinutesFilterParams): Promise<MinutesListResponse> => {
    const { data } = await httpClient.get<MinutesListResponse>('/meetingminutes/searchandfilterminutes', { params });
    return data;
  },

  detail: async (visitInstanceId: number): Promise<MinutesDetail> => {
    // We use the existing GetVisitInstanceMinutesQuery which works by visitInstanceId
    const { data } = await httpClient.get<MinutesDetail>(`/meetingminutes/visit-instances/${visitInstanceId}`);
    return data;
  },

  exportPdf: async (minutesId: number, filename?: string): Promise<void> => {
    const response = await httpClient.get(`/meetingminutes/${minutesId}/export-pdf`, {
      responseType: 'blob',
    });
    const url = window.URL.createObjectURL(new Blob([response.data], { type: 'application/pdf' }));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', filename || `minutes-${minutesId}.pdf`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  exportExcel: async (minutesId: number, filename?: string): Promise<void> => {
    const response = await httpClient.get(`/meetingminutes/${minutesId}/export-excel`, {
      responseType: 'blob',
    });
    // application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
    const url = window.URL.createObjectURL(new Blob([response.data], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', filename || `minutes-${minutesId}.xlsx`);
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
  },

  acquireLock: async (minutesId: number): Promise<{ editLockToken: string }> => {
    const { data } = await httpClient.post(`/meetingminutes/${minutesId}/acquire-lock`);
    return data;
  },

  save: async (minutesId: number, payload: any): Promise<void> => {
    await httpClient.put(`/meetingminutes/${minutesId}`, payload);
  },

  releaseLock: async (minutesId: number, editLockToken: string): Promise<void> => {
    await httpClient.post(`/meetingminutes/${minutesId}/release-lock`, { editLockToken });
  }
};
