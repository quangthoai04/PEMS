export type AccountRoleCode = 'ADMIN' | 'HO' | 'STAFF' | 'DEPT' | 'STUDENT' | 'VISITOR';
export type AccountSubRole = 'Leader' | 'Staff';

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
