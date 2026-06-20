// Authentication domain types shared across the frontend.

export type LoginPortal = 'INTERNAL' | 'VISITOR';

export type PermissionLevel = 'F' | 'E' | 'R' | 'O';

export type RoleCode = 'ADMIN' | 'HO' | 'STAFF' | 'DEPARTMENT' | 'STUDENT' | 'VISITOR';

export interface AuthUser {
  userId: string;
  fullName: string;
  email: string;
  phone?: string | null;
  avatarUrl?: string | null;
  roleCode: string;
  roleName?: string | null;
  subRole?: string | null;
  primaryCampusId?: string | null;
  campusCode?: string | null;
  campusName?: string | null;
  departmentId?: string | null;
  departmentName?: string | null;
  mustChangePassword: boolean;
  mustSetPassword: boolean;
}

export interface UserPermission {
  permissionCode: string;
  permissionLevel: PermissionLevel;
  permissionGroup: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
  permissions: UserPermission[];
}

export interface UserProfileResponse {
  user: AuthUser;
  permissions: UserPermission[];
}

export interface PermissionsResponse {
  roleCode: string;
  permissions: UserPermission[];
}

export interface MessageResponse {
  message: string;
}

export interface InternalLoginRequest {
  email: string;
  password: string;
  loginPortal: 'INTERNAL';
  selectedCampusId: string;
}

export interface VisitorLoginRequest {
  email: string;
  password: string;
  loginPortal: 'VISITOR';
}

export interface InternalGoogleLoginRequest {
  idToken: string;
  loginPortal: 'INTERNAL';
  selectedCampusId: string;
}

export interface VisitorGoogleLoginRequest {
  idToken: string;
  loginPortal: 'VISITOR';
}

/** @deprecated Use InternalLoginRequest or VisitorLoginRequest */
export interface LoginRequest {
  email: string;
  password: string;
  loginPortal: LoginPortal;
  selectedCampusId?: string;
}

/** @deprecated Use InternalGoogleLoginRequest or VisitorGoogleLoginRequest */
export interface GoogleLoginRequest {
  idToken: string;
  loginPortal: LoginPortal;
  selectedCampusId?: string;
}

export interface ResetPasswordRequest {
  email: string;
  otpOrToken: string;
  newPassword: string;
  confirmPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword?: string;
  newPassword: string;
  confirmPassword: string;
}

export interface CampusOption {
  campusId: string;
  campusCode: string;
  campusName: string;
}
