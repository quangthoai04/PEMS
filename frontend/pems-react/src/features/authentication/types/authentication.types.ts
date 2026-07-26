// Authentication domain types shared across the frontend.

export type LoginPortal = 'INTERNAL' | 'VISITOR';


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
  effectiveRole: string;
  status: string;
}


export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: AuthUser;
}

export interface UserProfileResponse {
  user: AuthUser;
}


export interface MessageResponse {
  message: string;
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
