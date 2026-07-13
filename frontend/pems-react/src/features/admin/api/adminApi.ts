/** API client của System Administration Console (ADMIN-only, /api/admin/*). */

import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AdminApiRequestActivityPoint,
  AdminAuditLogDetail,
  AdminAuditLogItem,
  AdminAuditLogsQuery,
  AdminDashboardSummary,
  AdminLoginActivityPoint,
  AdminLoginLogItem,
  AdminLoginLogsQuery,
  AdminRecentAuditItem,
  AdminSecurityEventItem,
  AdminSecurityEventsQuery,
  AdminSecurityOverview,
  AdminSessionItem,
  AdminSessionsQuery,
  PaginatedResult,
  RevokeSessionResponse,
  RevokeUserSessionsResponse,
} from '../types/admin.types';

/** Bỏ các param rỗng/undefined để query string gọn và backend không nhận filter rác. */
function cleanParams<T extends object>(params?: T): Record<string, unknown> | undefined {
  if (!params) return undefined;
  const entries = Object.entries(params).filter(
    ([, v]) => v !== undefined && v !== null && v !== '',
  );
  return entries.length ? Object.fromEntries(entries) : undefined;
}

export const adminApi = {
  // ── Dashboard ──
  async getDashboardSummary(): Promise<AdminDashboardSummary> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.dashboardSummary);
    return data;
  },
  async getLoginActivity(days = 7): Promise<AdminLoginActivityPoint[]> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.dashboardLoginActivity, { params: { days } });
    return data;
  },
  async getSecurityOverview(): Promise<AdminSecurityOverview> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.dashboardSecurity);
    return data;
  },
  async getIntegrationsActivity(days = 7): Promise<AdminApiRequestActivityPoint[]> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.dashboardIntegrations, { params: { days } });
    return data;
  },
  async getRecentAudits(limit = 10): Promise<AdminRecentAuditItem[]> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.dashboardRecentAudits, { params: { limit } });
    return data;
  },

  // ── Sessions ──
  async getSessions(query?: AdminSessionsQuery): Promise<PaginatedResult<AdminSessionItem>> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.sessions, { params: cleanParams(query) });
    return data;
  },
  async revokeSession(sessionId: number, reason?: string): Promise<RevokeSessionResponse> {
    const { data } = await httpClient.post(API_ENDPOINTS.admin.revokeSession(sessionId), { reason: reason ?? null });
    return data;
  },
  async revokeUserSessions(userId: number, reason?: string): Promise<RevokeUserSessionsResponse> {
    const { data } = await httpClient.post(API_ENDPOINTS.admin.revokeUserSessions(userId), { reason: reason ?? null });
    return data;
  },

  // ── Login logs / security events ──
  async getLoginLogs(query?: AdminLoginLogsQuery): Promise<PaginatedResult<AdminLoginLogItem>> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.loginLogs, { params: cleanParams(query) });
    return data;
  },
  async getSecurityEvents(query?: AdminSecurityEventsQuery): Promise<PaginatedResult<AdminSecurityEventItem>> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.securityEvents, { params: cleanParams(query) });
    return data;
  },

  // ── Audit logs ──
  async getAuditLogs(query?: AdminAuditLogsQuery): Promise<PaginatedResult<AdminAuditLogItem>> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.auditLogs, { params: cleanParams(query) });
    return data;
  },
  async getAuditLogDetail(auditLogId: number): Promise<AdminAuditLogDetail> {
    const { data } = await httpClient.get(API_ENDPOINTS.admin.auditLogDetail(auditLogId));
    return data;
  },
};
