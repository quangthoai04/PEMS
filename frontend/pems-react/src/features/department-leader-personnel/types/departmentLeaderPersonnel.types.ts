/**
 * Types for the Department Leader personnel screen (/dashboard/my-department).
 *
 * Mirrors the `/api/department-leader` contract. Note what is NOT here: no `departmentId` on any
 * request type. The department is resolved server-side from the caller's session, so the frontend
 * has nothing to send and nothing to tamper with.
 */

/** Canonical gender wire values. The Vietnamese labels are presentation only — never sent. */
export type PersonnelGender = 'MALE' | 'FEMALE' | 'OTHER';

/** Account statuses as stored. INACTIVE and LOCKED are distinct and must never be merged. */
export type PersonnelStatus = 'ACTIVE' | 'INACTIVE' | 'PENDING_EMAIL_CONFIRMATION' | 'LOCKED';

/** Status filter, plus the "no filter" sentinel. */
export type PersonnelStatusFilter = 'ALL' | PersonnelStatus;

/** Truthful delivery outcome. A 200 does NOT imply the email was sent. */
export type EmailNotificationStatus = 'SENT' | 'PARTIAL' | 'FAILED' | 'SKIPPED' | 'NOT_REQUIRED';

export interface MyDepartment {
  departmentId: number;
  departmentName: string;
  departmentType: string;
  departmentStatus: string;
  campusId: number;
  campusName: string;
  currentLeaderUserId: number | null;
  currentLeaderName: string | null;
  totalPersonnelCount: number;
  activePersonnelCount: number;
  inactivePersonnelCount: number;
  pendingEmailConfirmationCount: number;
  lockedPersonnelCount: number;
}

/**
 * Per-row capability flags computed by the backend. They drive what the UI OFFERS; the backend
 * re-checks every one of them when the action is actually invoked, so hiding a button is a
 * convenience, never the security boundary.
 */
export interface PersonnelActionFlags {
  canView: boolean;
  canEdit: boolean;
  canDisable: boolean;
  canEnable: boolean;
  canTransferLeadershipTo: boolean;
  canResendEmailConfirmation: boolean;
}

export interface PersonnelListItem extends PersonnelActionFlags {
  userId: number;
  fullName: string;
  email: string;
  phone: string | null;
  gender: PersonnelGender | null;
  status: PersonnelStatus;
  subRole: string | null;
  position: string;
  avatarUrl: string | null;
  departmentName: string;
  campusName: string;
  createdAt: string;
}

export interface PersonnelListQuery {
  keyword?: string;
  status?: PersonnelStatusFilter;
  page?: number;
  pageSize?: number;
  sortBy?: 'fullName' | 'email' | 'status' | 'createdAt';
  sortDirection?: 'asc' | 'desc';
}

export interface PagedPersonnel {
  items: PersonnelListItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface PersonnelDetail {
  userId: number;
  fullName: string;
  email: string;
  phone: string | null;
  gender: PersonnelGender | null;
  status: PersonnelStatus;
  roleCode: string;
  subRole: string | null;
  position: string;
  avatarUrl: string | null;
  departmentId: number;
  departmentName: string;
  campusId: number;
  campusName: string;
  createdAt: string;
  updatedAt: string | null;
  lastLoginAt: string | null;
  canEdit: boolean;
  canDisable: boolean;
  canEnable: boolean;
  canTransferLeadershipTo: boolean;
  canResendEmailConfirmation: boolean;
  isCurrentDepartmentLeader: boolean;
}

export interface CreatePersonnelRequest {
  fullName: string;
  email: string;
  phone: string;
  gender: PersonnelGender;
}

export interface CreatePersonnelResponse {
  success: boolean;
  userId: number;
  fullName: string;
  email: string;
  phone: string | null;
  gender: PersonnelGender | null;
  status: PersonnelStatus;
  subRole: string;
  emailNotificationStatus: EmailNotificationStatus;
  message: string;
}

export interface UpdatePersonnelRequest {
  fullName: string;
  email: string;
  phone: string;
  gender: PersonnelGender;
}

/**
 * The edit result. `changed` and `emailChanged` are what the UI must read — a 200 with
 * `changed: false` means nothing was modified, and `emailNotificationStatus` can report a failure
 * even when the identity change itself committed successfully.
 */
export interface UpdatePersonnelResponse {
  success: boolean;
  userId: number;
  fullName: string;
  email: string;
  phone: string | null;
  gender: PersonnelGender | null;
  /** Always identical to the status before the edit — changing an email never changes status. */
  status: PersonnelStatus;
  changed: boolean;
  emailChanged: boolean;
  confirmationReissued: boolean;
  authenticationRelinkRequired: boolean;
  revokedSessions: number;
  emailNotificationStatus: EmailNotificationStatus;
  message: string;
}

export interface StatusImpactBlocker {
  code: string;
  count: number;
  message: string;
}

export interface StatusImpactWarning {
  code: string;
  count: number;
  message: string;
}

export interface PersonnelStatusImpact {
  userId: number;
  currentStatus: PersonnelStatus;
  targetStatus: PersonnelStatus;
  canChangeStatus: boolean;
  activeSessionCount: number;
  blockers: StatusImpactBlocker[];
  warnings: StatusImpactWarning[];
}

export interface ChangePersonnelStatusRequest {
  targetStatus: 'ACTIVE' | 'INACTIVE';
  reason?: string;
}

export interface ChangePersonnelStatusResponse {
  success: boolean;
  userId: number;
  previousStatus: PersonnelStatus;
  status: PersonnelStatus;
  revokedSessions: number;
  emailNotificationStatus: EmailNotificationStatus;
  message: string;
}

export interface ResendConfirmationResponse {
  success: boolean;
  userId: number;
  email: string;
  emailNotificationStatus: EmailNotificationStatus;
  resendCount: number;
  message: string;
}

export interface LeaderCandidate {
  userId: number;
  fullName: string;
  email: string;
  phone: string | null;
  avatarUrl: string | null;
}

export interface LeaderCandidates {
  items: LeaderCandidate[];
  currentLeaderUserId: number | null;
  currentLeaderName: string | null;
}

export interface TransferLeadershipResponse {
  success: boolean;
  departmentId: number;
  previousLeaderUserId: number;
  previousLeaderName: string;
  newLeaderUserId: number;
  newLeaderName: string;
  revokedSessions: number;
  /** True — the caller lost their management role and must sign in again. */
  actorMustSignInAgain: boolean;
  emailNotificationStatus: EmailNotificationStatus;
  message: string;
}

// ── Display helpers ─────────────────────────────────────────────────────────

export const PERSONNEL_STATUS_LABELS: Record<PersonnelStatus, string> = {
  ACTIVE: 'Hoạt động',
  INACTIVE: 'Vô hiệu hóa',
  PENDING_EMAIL_CONFIRMATION: 'Chờ xác nhận email',
  LOCKED: 'Bị khóa',
};

export const GENDER_LABELS: Record<PersonnelGender, string> = {
  MALE: 'Nam',
  FEMALE: 'Nữ',
  OTHER: 'Khác',
};

/** The three options a gender picker may offer. Values are the wire values, labels are display only. */
export const GENDER_OPTIONS: ReadonlyArray<{ value: PersonnelGender; label: string }> = [
  { value: 'MALE', label: 'Nam' },
  { value: 'FEMALE', label: 'Nữ' },
  { value: 'OTHER', label: 'Khác' },
];

export const STATUS_FILTER_OPTIONS: ReadonlyArray<{ value: PersonnelStatusFilter; label: string }> = [
  { value: 'ALL', label: 'Tất cả trạng thái' },
  { value: 'ACTIVE', label: 'Hoạt động' },
  { value: 'INACTIVE', label: 'Vô hiệu hóa' },
  { value: 'PENDING_EMAIL_CONFIRMATION', label: 'Chờ xác nhận email' },
  { value: 'LOCKED', label: 'Bị khóa' },
];
