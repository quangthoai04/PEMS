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
  fromEmail?: string | null;
  fromName?: string | null;
  replyToEmail?: string | null;
  replyToName?: string | null;
  maxFileSizeMb?: number | null;
  allowedMimeTypes: string[];
  createdAt: string;
  updatedAt?: string | null;
  /**
   * DATABASE = quản lý qua console này; ENVIRONMENT = cấu hình trên server (read-only);
   * HYBRID = một phần trên server, một phần trong DB.
   *
   * Google Drive là trường hợp HYBRID: ClientId/ClientSecret/RedirectUri và các folder ID nằm ở
   * environment, chỉ refresh token — thứ duy nhất hết hạn và cần thay thường xuyên — nằm trong DB.
   */
  managementSource: 'DATABASE' | 'ENVIRONMENT' | 'HYBRID';
  canEdit: boolean;
  canTest: boolean;
  canToggleStatus: boolean;
  canConfigureQuota: boolean;
  /** ADMIN được mở luồng OAuth để cấp lại credential cho tích hợp này. */
  canConnectOAuth: boolean;
  /** ADMIN được xoá credential OAuth đang lưu. */
  canDisconnectOAuth: boolean;
  /** ERROR = đã có credential nhưng lần test kết nối gần nhất thất bại. Không bao giờ chứa giá trị token. */
  credentialStatus: 'NOT_CONFIGURED' | 'CONNECTED' | 'ERROR';
}

/** URL màn hình cấp quyền của Google. Không chứa ClientSecret, không chứa token. */
export interface GoogleDriveOAuthStartResult {
  authorizationUrl: string;
}

/**
 * Các lý do thất bại backend gửi kèm khi redirect về `?googleDriveOAuth=failed&reason=...`.
 * Cố định và không nhạy cảm — backend không bao giờ đưa nội dung Google trả về lên query string.
 */
export type GoogleDriveOAuthFailureReason =
  | 'access_denied'
  | 'invalid_state'
  | 'state_expired'
  | 'no_refresh_token'
  | 'token_exchange_failed'
  | 'save_failed'
  | 'config_missing';

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

export interface UpsertGoogleVisionFaceDetectionConfigRequest {
  name: string;
  projectId: string;
  location: string;
  endpoint: string;
  serviceAccountJson?: string | null;
  secretRef?: string | null;
  rateLimitPerMinute: number;
  monthlyQuota: number;
  timeoutSeconds: number;
}

export interface UpsertResendConfigRequest {
  name: string;
  apiKey?: string | null;
  fromEmail: string;
  fromName: string;
  replyToEmail?: string | null;
  replyToName?: string | null;
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
