// Self-service profile domain types. Mirrors backend ProfileResponse
// (PEMS.Application/Profiles/Common/ProfileResponse.cs).

export type RoleCode = 'ADMIN' | 'HO' | 'STAFF' | 'DEPARTMENT' | 'STUDENT' | 'VISITOR';
export type SubRole = 'LEADER' | 'STAFF';
export type GenderValue = 'MALE' | 'FEMALE' | 'OTHER';

export interface ProfileCampus {
  campusId: number;
  campusCode: string;
  name: string;
}

export interface ProfileDepartment {
  departmentId: number;
  name: string;
  departmentType: string;
}

export interface ViewProfileResponse {
  userId: number;
  fullName: string;
  avatarUrl: string | null;
  gender: GenderValue | null;
  email: string;
  phone: string | null;
  nationality: string | null;
  roleCode: RoleCode;
  subRole: SubRole | null;
  displayRole: string;
  displayPosition: 'Trưởng phòng' | 'Nhân viên' | null;
  studentCode: string | null;
  campus: ProfileCampus | null;
  displayCampusName: string | null;
  department: ProfileDepartment | null;
  displayDepartmentName: string | null;
  status: 'ACTIVE' | 'INACTIVE' | 'LOCKED';
}

/** UC-15 — only these four text fields are ever sent. nationality is VISITOR-only. */
export interface UpdateProfileRequest {
  fullName?: string;
  gender?: GenderValue | null;
  phone?: string | null;
  nationality?: string | null;
}
