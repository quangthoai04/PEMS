/** API Integration admin types — mirrors backend PEMS.Application.ApiIntegrations DTOs. */

export interface ApiIntegration {
  apiConfigId: number;
  apiCode: string;
  name: string;
  providerName?: string | null;
  purpose?: string | null;
  baseUrl: string;
  status: 'ACTIVE' | 'INACTIVE' | 'DISABLED';
  dataSensitivity: string;
  allowsProviderTraining: boolean;
  retentionDays?: number | null;
  rateLimitPerMinute?: number | null;
  monthlyQuota?: number | null;
  timeoutSeconds: number;
  lastTestStatus?: 'SUCCESS' | 'FAILED' | null;
  lastTestedAt?: string | null;
  lastTestMessage?: string | null;
  hasCredential: boolean;
  secretRef?: string | null;
  projectId?: string | null;
  location?: string | null;
  processorId?: string | null;
  endpoint?: string | null;
  maxFileSizeMb?: number | null;
  allowedMimeTypes: string[];
  createdAt: string;
  updatedAt?: string | null;
}

export interface UpsertGoogleDocumentAiOcrConfigRequest {
  name: string;
  projectId: string;
  location: string;
  processorId: string;
  endpoint: string;
  serviceAccountJson?: string | null;
  secretRef?: string | null;
  rateLimitPerMinute: number;
  monthlyQuota: number;
  timeoutSeconds: number;
  retentionDays: number;
}

export interface UpsertGoogleTranslationConfigRequest {
  name: string;
  projectId: string;
  location: string;
  serviceAccountJson?: string | null;
  secretRef?: string | null;
  rateLimitPerMinute: number;
  monthlyQuota: number;
  timeoutSeconds: number;
}

export interface ApiConnectionTestResult {
  success: boolean;
  message: string;
  errorCode?: string | null;
  responseTimeMs: number;
  testedAt: string;
}

export interface ApiQuota {
  apiUsageQuotaId: number;
  apiConfigId: number;
  campusId?: number | null;
  campusScopeKey: string;
  periodYyyymm: string;
  monthlyLimit: number;
  usedCount: number;
  lastUsedAt?: string | null;
}

export interface ApiRequestLog {
  apiRequestLogId: number;
  apiConfigId: number;
  endpoint: string;
  method: string;
  httpStatus?: number | null;
  responseTimeMs?: number | null;
  success: boolean;
  errorCode?: string | null;
  errorMessage?: string | null;
  requestedByName?: string | null;
  createdAt: string;
}

export interface ApiRequestLogListResponse {
  items: ApiRequestLog[];
  totalCount: number;
  page: number;
  pageSize: number;
}
