/**
 * Permission codes — MUST match `permissions.permission_code` seeded in the DB
 * (database/scripts/pems_full.sql) and returned by the backend in
 * AuthResponse.permissions / GET /auth/me. Format: `UC-NN.NAME` (NN NOT
 * zero-padded). Do not invent codes; only add codes that exist in the seed.
 *
 * Single source of truth: database/scripts/pems_full.sql. Backend
 * PermissionConstants.cs and database/seed/permissions.sql have been synced to
 * these same codes (canonical: UC-NN, 2 digits for 1-99, 3 for 100+; no zero-pad
 * like UC-098). The frontend never decides access — backend is the final check.
 */

export const PERMISSIONS = {
  // ── Profile (any authenticated user) ──────────────────────────────────
  VIEW_PROFILE: 'UC-14.VIEW_PROFILE',
  UPDATE_PROFILE: 'UC-15.UPDATE_PROFILE',
  CHANGE_PASSWORD: 'UC-16.CHANGE_PASSWORD',

  // ── Delegation / Visit Reception ──────────────────────────────────────
  SUBMIT_VISIT_REQUEST: 'UC-17.SUBMIT_VISIT_REQUEST',
  APPROVE_CROSS_CAMPUS_REQUEST: 'UC-18.APPROVE_CROSS_CAMPUS_REQUEST',
  VIEW_GUEST_DELEGATION_DETAILS: 'UC-19.VIEW_GUEST_DELEGATION_DETAILS',
  VIEW_GUEST_DELEGATION_LIST: 'UC-20.VIEW_GUEST_DELEGATION_LIST',
  SEARCH_DELEGATIONS: 'UC-21.SEARCH_DELEGATIONS',
  PROCESS_VISIT_REQUEST: 'UC-22.PROCESS_VISIT_REQUEST',
  CONFIRM_PARTICIPATION: 'UC-27.CONFIRM_PARTICIPATION',
  CANCEL_VISIT_REQUEST: 'UC-136.CANCEL_VISIT_REQUEST',

  // ── Reports / Dashboard statistics ────────────────────────────────────
  VIEW_DASHBOARD_STATISTICS: 'UC-69.VIEW_DASHBOARD_STATISTICS',
  EXPORT_STATISTICS_REPORT: 'UC-70.EXPORT_STATISTICS_REPORT',
  FILTER_DASHBOARD_BY_TIME: 'UC-71.FILTER_DASHBOARD_BY_TIME',

  // ── Campus Management ─────────────────────────────────────────────────
  VIEW_CAMPUS_LIST: 'UC-82.VIEW_CAMPUS_LIST',

  // ── Account Management ────────────────────────────────────────────────
  VIEW_ACCOUNT_LIST: 'UC-95.VIEW_ACCOUNT_LIST',
  CREATE_ACCOUNT: 'UC-96.CREATE_ACCOUNT',
  MANAGE_ACCOUNT_STATUS: 'UC-97.MANAGE_ACCOUNT_STATUS',
  VIEW_ACCOUNT_DETAILS: 'UC-98.VIEW_ACCOUNT_DETAILS',
  UPDATE_ACCOUNT_ROLE: 'UC-100.UPDATE_ACCOUNT_ROLE',

  // ── Email Management ──────────────────────────────────────────────────
  EDIT_EMAIL_CONTENT: 'UC-46.EDIT_EMAIL_CONTENT',
  SEND_EMAIL: 'UC-47.SEND_EMAIL',
  VIEW_EMAIL: 'UC-48.VIEW_EMAIL',
  REPLY_TO_EMAIL: 'UC-49.REPLY_TO_EMAIL',

  // ── Role & Permission Management (Admin) ──────────────────────────────
  VIEW_ROLE_LIST: 'UC-117.VIEW_ROLE_LIST',
  CREATE_NEW_ROLE: 'UC-118.CREATE_NEW_ROLE',
  CONFIGURE_ROLE_PERMISSIONS: 'UC-119.CONFIGURE_ROLE_PERMISSIONS',
  UPDATE_ROLE_DETAILS: 'UC-120.UPDATE_ROLE_DETAILS',

  // ── API Management (Admin) ────────────────────────────────────────────
  VIEW_API_CONFIGURATION: 'UC-122.VIEW_API_CONFIGURATION',
  VIEW_API_LOGS: 'UC-129.VIEW_API_LOGS',
} as const;

export type PermissionKey = keyof typeof PERMISSIONS;
