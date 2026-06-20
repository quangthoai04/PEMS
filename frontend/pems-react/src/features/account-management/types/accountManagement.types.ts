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
  message: string;
}

/** UC-100 Update Account Role request. Mirrors backend UpdateAccountRoleCommand. */
export interface UpdateAccountRoleRequest {
  userId: string;
  newRoleCode: AccountRoleCode;
  subRole?: AccountSubRole | null;
  primaryCampusId?: string | null;
  departmentId?: string | null;
}

export interface UpdateAccountRoleResponse {
  userId: string;
  roleCode: string;
  primaryCampusId?: string | null;
  revokedSessions: number;
  message: string;
}
