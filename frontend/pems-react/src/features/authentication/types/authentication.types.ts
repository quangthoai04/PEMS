// Authentication domain types shared across the frontend.

export type LoginPortal = 'INTERNAL' | 'VISITOR';

export type PermissionLevel = 'F' | 'E' | 'R' | 'O';

export type RoleCode = 'ADMIN' | 'HO' | 'STAFF' | 'DEPT' | 'STUDENT' | 'VISITOR';

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

export interface LoginRequest {
  email: string;
  password: string;
  loginPortal: LoginPortal;
}

export interface GoogleLoginRequest {
  idToken: string;
  loginPortal: LoginPortal;
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
