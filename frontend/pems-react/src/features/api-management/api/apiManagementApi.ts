import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  ApiConnectionTestResult,
  ApiIntegration,
  ApiQuota,
  ApiRequestLogListResponse,
  UpsertGoogleDocumentAiOcrConfigRequest,
  UpsertGoogleTranslationConfigRequest,
  UpsertGoogleVisionFaceDetectionConfigRequest,
} from '../types/apiManagement.types';

/** ADMIN API Integration management — ApiIntegrationsController. Secrets never round-trip. */
export const apiManagementApi = {
  async getIntegrations(purpose?: string): Promise<ApiIntegration[]> {
    const { data } = await httpClient.get<ApiIntegration[]>(API_ENDPOINTS.apiIntegrations.list, {
      params: purpose ? { purpose } : undefined,
    });
    return data;
  },

  async getIntegration(apiConfigId: number | string): Promise<ApiIntegration> {
    const { data } = await httpClient.get<ApiIntegration>(
      API_ENDPOINTS.apiIntegrations.detail(apiConfigId),
    );
    return data;
  },

  async upsertGoogleDocumentAiConfig(
    payload: UpsertGoogleDocumentAiOcrConfigRequest,
  ): Promise<ApiIntegration> {
    const { data } = await httpClient.post<ApiIntegration>(
      API_ENDPOINTS.apiIntegrations.upsertGoogleDocumentAi,
      payload,
    );
    return data;
  },

  /** Create-or-update the single Google Cloud Translation config (News auto-translate). */
  async upsertGoogleTranslationConfig(
    payload: UpsertGoogleTranslationConfigRequest,
  ): Promise<ApiIntegration> {
    const { data } = await httpClient.post<ApiIntegration>(
      API_ENDPOINTS.apiIntegrations.upsertGoogleTranslation,
      payload,
    );
    return data;
  },

  /** Create-or-update the single Google Cloud Vision config (Face Detection). */
  async upsertGoogleVisionFaceDetectionConfig(
    payload: UpsertGoogleVisionFaceDetectionConfigRequest,
  ): Promise<ApiIntegration> {
    const { data } = await httpClient.post<ApiIntegration>(
      API_ENDPOINTS.apiIntegrations.upsertGoogleVisionFaceDetection,
      payload,
    );
    return data;
  },

  async updateConfig(
    apiConfigId: number | string,
    payload: UpsertGoogleDocumentAiOcrConfigRequest,
  ): Promise<ApiIntegration> {
    const { data } = await httpClient.put<ApiIntegration>(
      API_ENDPOINTS.apiIntegrations.update(apiConfigId),
      payload,
    );
    return data;
  },

  async testConnection(apiConfigId: number | string): Promise<ApiConnectionTestResult> {
    const { data } = await httpClient.post<ApiConnectionTestResult>(
      API_ENDPOINTS.apiIntegrations.test(apiConfigId),
    );
    return data;
  },

  async enable(apiConfigId: number | string): Promise<ApiIntegration> {
    const { data } = await httpClient.post<ApiIntegration>(
      API_ENDPOINTS.apiIntegrations.enable(apiConfigId),
    );
    return data;
  },

  async disable(apiConfigId: number | string): Promise<ApiIntegration> {
    const { data } = await httpClient.post<ApiIntegration>(
      API_ENDPOINTS.apiIntegrations.disable(apiConfigId),
    );
    return data;
  },

  async getLogs(
    apiConfigId: number | string,
    page = 1,
    pageSize = 20,
  ): Promise<ApiRequestLogListResponse> {
    const { data } = await httpClient.get<ApiRequestLogListResponse>(
      API_ENDPOINTS.apiIntegrations.logs(apiConfigId),
      { params: { page, pageSize } },
    );
    return data;
  },

  async getQuota(apiConfigId: number | string): Promise<ApiQuota[]> {
    const { data } = await httpClient.get<ApiQuota[]>(
      API_ENDPOINTS.apiIntegrations.quota(apiConfigId),
    );
    return data;
  },

  async updateQuota(apiConfigId: number | string, monthlyLimit: number): Promise<ApiQuota> {
    const { data } = await httpClient.put<ApiQuota>(
      API_ENDPOINTS.apiIntegrations.quota(apiConfigId),
      { monthlyLimit },
    );
    return data;
  },
};
