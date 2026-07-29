export type AccountRoleCode = 'ADMIN' | 'HO' | 'STAFF' | 'DEPARTMENT' | 'STUDENT' | 'VISITOR';
export type AccountSubRole = 'Leader' | 'Staff';

/** Generic paged envelope returned by list/search endpoints. Mirrors backend PaginatedResult<T>. */
export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/** UC-95/UC-99 — one account row. Mirrors backend AccountListItemDto (no sensitive fields). */
export interface AccountListItem {
  userId: string;
  email: string;
  fullName: string;
  phone?: string | null;
  gender?: string | null;
  avatarUrl?: string | null;
  nationality?: string | null;
  studentCode?: string | null;

  roleCode: string;
  roleName: string;
  subRole?: string | null;

  campusId?: string | null;
  campusCode?: string | null;
  campusName?: string | null;

  departmentId?: string | null;
  departmentName?: string | null;

  status: string;
  createdVia?: string | null;
  providers: string[];

  lastLoginAt?: string | null;
  createdAt: string;
  updatedAt?: string | null;

  canViewDetails: boolean;
  canUpdateRole: boolean;
  canManageStatus: boolean;
  /** When canManageStatus is false, why the status toggle is hidden (e.g. SELF_ACCOUNT, ACCOUNT_LOCKED). */
  hideStatusToggleReason?: string | null;
  isCurrentUser: boolean;
  /** HO_BASIC_INFO — true when an HO caller may edit this row's full name / email. */
  canEditBasicInfo?: boolean;
  /** When canEditBasicInfo is false (HO caller), the reason (SELF_ACCOUNT | ACCOUNT_LOCKED | ...). */
  editBasicInfoDisabledReason?: string | null;
}

/** UC-95/UC-99 — query string params accepted by GET /accounts/viewaccountlist. */
export interface AccountListQueryParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  roleCode?: string;
  subRole?: string;
  status?: string;
  campusId?: string;
  departmentId?: string;
  providerType?: string;
  createdVia?: string;
  accountType?: 'INTERNAL' | 'VISITOR' | 'ALL';
  hasCampus?: boolean;
  fromDate?: string;
  toDate?: string;
  lastLoginFrom?: string;
  lastLoginTo?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

/** Active campus option for the campus filter dropdown (GET /campuses/active). */
export interface ActiveCampusOption {
  campusId: string;
  campusCode: string;
  campusName: string;
}

/** UC-95-SL account statistics (scoped to the caller). Mirrors backend ViewAccountStatisticsDto. */
export interface AccountStatistics {
  totalAccounts: number;
  activeAccounts: number;
  lockedAccounts: number;
  inactiveAccounts: number;
}

/** Active GENERAL department option for the Department-Leader dropdown (GET /accounts/campus-departments). */
export interface CampusDepartmentOption {
  departmentId: string;
  name: string;
  hasHead: boolean;
}

/** UC-96 — existing Staff Leader summary returned by the availability pre-check. */
export interface ExistingLeaderInfo {
  userId: string;
  fullName: string;
  email: string;
  status: string;
}

/**
 * UC-96 — Staff Leader availability pre-check result (GET /accounts/staff-leader-availability).
 * Mirrors backend StaffLeaderAvailabilityDto. `canCreateStaffLeader=false` always comes with a
 * `blockingReason` and a user-facing `message`; `existingLeader` is set for leader-exists cases.
 */
export interface StaffLeaderAvailability {
  campusId: string;
  campusName?: string | null;
  canCreateStaffLeader: boolean;
  icDepartmentId?: string | null;
  icDepartmentName?: string | null;
  existingLeader?: ExistingLeaderInfo | null;
  blockingReason?: string | null;
  message: string;
}

/** UC-96 — existing HO summary returned by the HO campus pre-check. */
export interface ExistingHoInfo {
  userId: string;
  fullName: string;
  email: string;
  status: string;
}

/**
 * UC-96 — HO campus pre-check result (GET /accounts/ho-campus-check). Mirrors backend
 * HoCampusCheckDto. `canCreateHo=false` always comes with a `reasonCode` and a user-facing
 * `message`; `existingHo` is set for the HO-exists cases (ACTIVE/INACTIVE/LOCKED).
 */
export interface HoCampusCheck {
  campusId: string;
  campusName?: string | null;
  canCreateHo: boolean;
  existingHo?: ExistingHoInfo | null;
  reasonCode?: string | null;
  message: string;
}

// ── Replace Staff Leader (REPLACE_STAFF_LEADER spec) ──

export interface ReplacementLeader {
  userId: string;
  fullName: string;
  email: string;
  status: string;
  roleCode: string;
  subRole?: string | null;
}

export interface ReplacementCandidate {
  userId: string;
  fullName: string;
  email: string;
  status: string;
  roleCode: string;
  subRole?: string | null;
}

/** Preview for the Replace Staff Leader modal (GET /accounts/staff-leader-replacement-preview). */
export interface StaffLeaderReplacementPreview {
  campusId: string;
  campusName?: string | null;
  campusStatus?: string | null;
  icDepartmentId?: string | null;
  icDepartmentName?: string | null;
  currentLeader?: ReplacementLeader | null;
  eligibleCandidates: ReplacementCandidate[];
  canReplace: boolean;
  blockingReason?: string | null;
  message: string;
}

export type ReplaceStaffLeaderMode = 'EXISTING_USER' | 'CREATE_NEW_USER';

/** Replace Staff Leader request (POST /accounts/replacestaffleader). */
export interface ReplaceStaffLeaderRequest {
  campusId: string | number;
  mode: ReplaceStaffLeaderMode;
  /** EXISTING_USER only. */
  newLeaderUserId?: string | number | null;
  /** CREATE_NEW_USER only. */
  fullName?: string | null;
  email?: string | null;
  phone?: string | null;
  gender?: string | null;
  reason: string;
}

export interface ReplaceStaffLeaderResponse {
  campusId: string;
  icDepartmentId: string;
  oldLeaderUserId: string;
  newLeaderUserId: string;
  newLeaderEmail: string;
  emailNotificationStatus: string;
  message: string;
}

// ── Staff Leader "Visitor liên quan" tab (read-only) — UC_StaffLeader_Related_Visitor_Accounts_Tab ──

/** One Visitor row in the Staff Leader related-visitors tab. Mirrors RelatedVisitorAccountListItemDto. */
export interface RelatedVisitorListItem {
  userId: string;
  fullName: string;
  email: string;
  phone?: string | null;
  nationality?: string | null;
  roleCode: string;
  status: string;
  createdVia?: string | null;
  createdAt: string;
  lastLoginAt?: string | null;
  relatedRequestCount: number;
  lastRelatedRequestAt?: string | null;
  latestPlannedStartAt?: string | null;
  canViewDetails: boolean;
  canManageStatus: boolean;
  canUpdateRole: boolean;
  canResetPassword: boolean;
}

/** Query params for GET /accounts/related-visitors (campus scope resolved server-side). */
export interface RelatedVisitorQueryParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  status?: string;
  nationality?: string;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

/** One visit request through which a Visitor is related to the caller's campus. */
export interface RelatedVisitorRequest {
  visitRequestId: string;
  visitInstanceId: string;
  requestCode: string;
  delegationName: string;
  visitScope: string;
  requestStatus: string;
  campusInstanceStatus: string;
  plannedStartAt: string;
  plannedEndAt: string;
}

/** Detail of a related Visitor account. Mirrors RelatedVisitorAccountDetailDto. */
export interface RelatedVisitorDetail {
  userId: string;
  fullName: string;
  email: string;
  phone?: string | null;
  nationality?: string | null;
  gender?: string | null;
  status: string;
  createdVia?: string | null;
  createdAt: string;
  lastLoginAt?: string | null;
  relatedRequests: RelatedVisitorRequest[];
  canManageStatus: boolean;
  canUpdateRole: boolean;
  canResetPassword: boolean;
}

/** UC-96 Create Account request. Mirrors backend CreateAccountCommand. */
export interface CreateAccountRequest {
  email: string;
  fullName: string;
  roleCode: AccountRoleCode;
  subRole?: AccountSubRole | null;
  primaryCampusId?: string | null;
  departmentId?: string | null;
  phone?: string | null;
  gender?: string | null;
  studentCode?: string | null;
  nationality?: string | null;
  /** Temporary password — honoured only in DevMixed mode. */
  password?: string | null;
}

export interface CreateAccountResponse {
  userId: string;
  email: string;
  roleCode: string;
  primaryCampusId?: string | null;
  passwordSet: boolean;
  /** UC-96 notification email outcome: SENT | FAILED. */
  emailNotificationStatus: string;
  message: string;
}

/** UC-98 — account detail. Mirrors backend ViewAccountDetailsDto (no sensitive fields). */
export interface AccountDetails {
  userId: string;
  fullName: string;
  email: string;
  phone?: string | null;
  gender?: string | null;
  roleCode: string;
  roleName: string;
  subRole?: string | null;
  displayRole: string;
  displayPosition?: string | null;
  campusId?: string | null;
  campusName?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  /** MSSV — only meaningful for STUDENT accounts. */
  studentCode?: string | null;
  status: string;
  createdVia?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  lastLoginAt?: string | null;
  /** HO_BASIC_INFO — true when an HO caller may edit this account's full name / email. */
  canEditBasicInfo?: boolean;
  editBasicInfoDisabledReason?: string | null;
}

/** HO_BASIC_INFO — request for POST /accounts/updatebasicaccountinfo (HO edits full name + email only). */
export interface UpdateBasicAccountInfoRequest {
  userId: string | number;
  fullName: string;
  email: string;
}

export interface UpdateBasicAccountInfoResponse {
  userId: string;
  fullName: string;
  email: string;
  emailChanged: boolean;
  revokedSessions: number;
  emailNotificationStatus: 'NOT_REQUIRED' | 'SENT' | 'FAILED' | 'PARTIAL';
  message: string;
}

/**
 * UC-100-SL — role-assignment options for the "Chỉnh sửa vai trò" modal
 * (GET /accounts/role-assignment-options?targetUserId=). Campus is resolved server-side.
 * Mirrors backend RoleAssignmentOptionsDto.
 */
export interface RoleAssignmentOptions {
  campusId: string;
  campusName: string;
  /** Campus IC department (auto-assigned for role STAFF); null when the campus has none active. */
  icDepartment: { departmentId: string; name: string } | null;
  generalDepartments: Array<{
    departmentId: string;
    name: string;
    hasHead: boolean;
    /** The current head of this department is the target account being edited. */
    isCurrentTargetHead: boolean;
    /** !hasHead || isCurrentTargetHead. */
    selectable: boolean;
  }>;
  /**
   * Set when the target account currently heads a GENERAL department. Any change that takes them
   * out of that seat must name a successor in the same request — the backend refuses to leave a
   * department headless, and handing over separately would demote the account to a shape a Staff
   * Leader can no longer manage.
   */
  headedDepartment: {
    departmentId: string;
    name: string;
    /** Active DEPARTMENT/STAFF members of that department; empty means no valid successor exists. */
    replacementCandidates: Array<{ userId: string; fullName: string; email: string }>;
  } | null;
}

/** UC-97 — change account status request. Mirrors backend ManageAccountStatusCommand. */
export interface ManageAccountStatusRequest {
  userId: string | number;
  /** LOCKED chỉ dành cho flow khóa bảo mật riêng của ADMIN (UC-97 mở rộng). */
  status: 'ACTIVE' | 'INACTIVE' | 'LOCKED';
  reason?: string | null;
}

export interface ManageAccountStatusResponse {
  userId: string;
  status: string;
  revokedSessions: number;
  message: string;
}

/**
 * Re-issue the confirmation link of an account still in PENDING_EMAIL_CONFIRMATION. The account is
 * identified by id alone — the address is whatever the backend currently holds, never a value the
 * caller supplies (correcting a typo is the separate edit-pending-email flow).
 */
export interface ResendAccountEmailConfirmationRequest {
  userId: string | number;
}

export interface ResendAccountEmailConfirmationResponse {
  success: boolean;
  /**
   * Truthful delivery outcome. `success: true` only means the request was processed and a fresh
   * token issued — the mail itself may still have been SKIPPED (SMTP off) or FAILED, so the UI must
   * branch on this field, never on `success`. Widened with `string` because the server may add
   * outcomes this client has not been taught yet.
   */
  emailNotificationStatus: 'SENT' | 'SKIPPED' | 'FAILED' | string;
  resendCount: number;
  message: string;
}

/**
 * Correct the address of an account still in PENDING_EMAIL_CONFIRMATION
 * (POST /accounts/edit-pending-email). Mirrors backend EditPendingAccountEmailCommand.
 *
 * Separate from {@link UpdateBasicAccountInfoRequest} because the outcomes differ in kind: this one
 * kills the old confirmation token and mails a fresh activation link to the new address, which is
 * what such an account needs in order to ever become usable. `fullName` rides along so a modal that
 * edits both fields commits them together — two calls could half-succeed.
 */
export interface EditPendingAccountEmailRequest {
  userId: string | number;
  newEmail: string;
  /** Omit to leave the current name untouched. */
  fullName?: string;
}

export interface EditPendingAccountEmailResponse {
  success: boolean;
  /** The stored (normalized) address. */
  email: string;
  /**
   * Delivery outcome of the CONFIRMATION email sent to the new address — never the neutral notice
   * sent to the old one. `success: true` only means the address was changed and a fresh token issued;
   * the mail may still have been SKIPPED (SMTP off) or FAILED, so the UI must branch on this field.
   * Widened with `string` because the server may add outcomes this client has not been taught yet.
   */
  emailNotificationStatus: 'SENT' | 'SKIPPED' | 'FAILED' | string;
  message: string;
}

/** UC-100 Update Account Role request. Mirrors backend UpdateAccountRoleCommand. */
export interface UpdateAccountRoleRequest {
  userId: string;
  newRoleCode: AccountRoleCode;
  subRole?: AccountSubRole | null;
  primaryCampusId?: string | null;
  departmentId?: string | null;
  /** MSSV — required when newRoleCode is STUDENT (Staff Leader flow); ignored otherwise. */
  studentCode?: string | null;
  /**
   * Họ và tên — editable only when the target's ORIGINAL role permits identity edits
   * (STAFF/STAFF, DEPARTMENT/LEADER, STUDENT). Omit/null to leave unchanged.
   */
  fullName?: string | null;
  /** Email — same editability rule as fullName. Omit/null to leave unchanged. */
  email?: string | null;
  /**
   * Successor for the GENERAL department the target currently heads. Required when the change
   * vacates that seat; rejected (422) when the change does not. Handover and role change commit
   * together or not at all.
   */
  replacementDepartmentHeadUserId?: string | null;
}

export interface UpdateAccountRoleResponse {
  userId: string;
  roleCode: string;
  primaryCampusId?: string | null;
  revokedSessions: number;
  message: string;
}
