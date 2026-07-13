/** System Administration Console types — mirror backend PEMS.Application.Admin DTOs. */

export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

// ── Dashboard ──────────────────────────────────────────────────────────────

export interface AdminDashboardSummary {
  accounts: {
    total: number;
    active: number;
    inactive: number;
    locked: number;
    newLast30Days: number;
  };
  sessions: {
    active: number;
    expired: number;
    revoked: number;
  };
  logins24h: {
    success: number;
    failed: number;
  };
  security: {
    highLast7Days: number;
    criticalLast7Days: number;
  };
  integrations: {
    total: number;
    active: number;
    testFailed: number;
    missingCredential: number;
    quotaAbove80Percent: number;
  };
}

export interface AdminLoginActivityPoint {
  date: string;
  success: number;
  failed: number;
}

export interface AdminApiRequestActivityPoint {
  date: string;
  success: number;
  failed: number;
  avgResponseTimeMs?: number | null;
}

export interface AdminSecurityOverview {
  low: number;
  medium: number;
  high: number;
  critical: number;
  recentHighSeverity: {
    securityEventId: number;
    eventType: string;
    result: string;
    severity: string;
    email?: string | null;
    ipAddress?: string | null;
    loginPortal?: string | null;
    providerType?: string | null;
    createdAt: string;
  }[];
}

export interface AdminRecentAuditItem {
  auditLogId: number;
  actorName?: string | null;
  actorEmail?: string | null;
  action: string;
  entityType: string;
  entityId?: number | null;
  campusName?: string | null;
  ipAddress?: string | null;
  createdAt: string;
}

// ── Sessions ───────────────────────────────────────────────────────────────

export type AdminSessionStatus = 'ACTIVE' | 'EXPIRED' | 'REVOKED';

export interface AdminSessionItem {
  sessionId: number;
  userId: number;
  email: string;
  fullName: string;
  roleCode?: string | null;
  loginPortal: string;
  providerType?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  createdAt: string;
  expiresAt: string;
  revokedAt?: string | null;
  revokedReason?: string | null;
  status: AdminSessionStatus;
  isCurrentSession: boolean;
}

export interface AdminSessionsQuery {
  page?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
  portal?: string;
  provider?: string;
  userId?: number;
}

export interface RevokeSessionResponse {
  sessionId: number;
  revoked: boolean;
  wasCurrentSession: boolean;
  message: string;
}

export interface RevokeUserSessionsResponse {
  userId: number;
  revokedSessions: number;
  affectsCurrentUser: boolean;
  message: string;
}

// ── Login logs / security events ───────────────────────────────────────────

export interface AdminLoginLogItem {
  loginLogId: number;
  userId?: number | null;
  email: string;
  fullName?: string | null;
  loginPortal: string;
  providerType?: string | null;
  status: string;
  failureReason?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  sessionId?: number | null;
  createdAt: string;
}

export interface AdminLoginLogsQuery {
  page?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
  portal?: string;
  provider?: string;
  ipAddress?: string;
  fromDate?: string;
  toDate?: string;
}

export interface AdminSecurityEventItem {
  securityEventId: number;
  userId?: number | null;
  email?: string | null;
  eventType: string;
  result: string;
  failureReasonCode?: string | null;
  severity: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  loginPortal?: string | null;
  providerType?: string | null;
  sessionId?: number | null;
  detailText?: string | null;
  createdAt: string;
}

export interface AdminSecurityEventsQuery {
  page?: number;
  pageSize?: number;
  keyword?: string;
  severity?: string;
  result?: string;
  eventType?: string;
  portal?: string;
  provider?: string;
  ipAddress?: string;
  fromDate?: string;
  toDate?: string;
}

// ── Audit logs ─────────────────────────────────────────────────────────────

export interface AdminAuditLogItem {
  auditLogId: number;
  actorUserId?: number | null;
  actorName?: string | null;
  actorEmail?: string | null;
  action: string;
  entityType: string;
  entityId?: number | null;
  campusId?: number | null;
  campusName?: string | null;
  ipAddress?: string | null;
  requestId?: string | null;
  changeCount: number;
  createdAt: string;
}

export interface AdminAuditLogsQuery {
  page?: number;
  pageSize?: number;
  keyword?: string;
  action?: string;
  entityType?: string;
  campusId?: number;
  fromDate?: string;
  toDate?: string;
}

export interface AdminAuditLogChange {
  auditLogChangeId: number;
  fieldName: string;
  oldValue?: string | null;
  newValue?: string | null;
  isMasked: boolean;
}

export interface AdminAuditLogDetail {
  auditLogId: number;
  actorUserId?: number | null;
  actorName?: string | null;
  actorEmail?: string | null;
  actorRoleCode?: string | null;
  action: string;
  entityType: string;
  entityId?: number | null;
  campusId?: number | null;
  campusName?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  requestId?: string | null;
  createdAt: string;
  changes: AdminAuditLogChange[];
}
