/** Legacy placeholder kept so the (stub) adapter import keeps compiling. */
export type DepartmentManagement = {
  // TODO: define types
};

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

/** One department row for the Staff Leader list (UC-104/UC-103). Mirrors DepartmentListItemDto. */
export interface DepartmentListItem {
  departmentId: number;
  campusId: number;
  campusName: string;
  name: string;
  headUserId?: number | null;
  headFullName?: string | null;
  status: string; // ACTIVE | INACTIVE
  departmentType: string; // IC | GENERAL (not shown as a column)
  canToggleStatus: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

/** Query params for the department list/search (campus scope resolved server-side). */
export interface DepartmentListQueryParams {
  page?: number;
  pageSize?: number;
  keyword?: string;
  status?: string; // ACTIVE | INACTIVE | ''
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

/** UC-101 create request (only the name is accepted; everything else is server-populated). */
export interface CreateDepartmentRequest {
  name: string;
}

export interface CreateDepartmentResponse {
  departmentId: number;
  campusId: number;
  campusName: string;
  name: string;
  headUserId?: number | null;
  headFullName?: string | null;
  status: string;
  departmentType: string;
  canToggleStatus: boolean;
  createdAt: string;
  message: string;
}

export interface ManageDepartmentStatusRequest {
  departmentId: number;
  status: 'ACTIVE' | 'INACTIVE';
}

export interface ManageDepartmentStatusResponse {
  departmentId: number;
  status: string;
  message: string;
}

/** UC-105 — department detail (general info + UI permission flags). */
export interface DepartmentDetail {
  departmentId: number;
  name: string;
  campusId: number;
  campusCode?: string | null;
  campusName: string;
  headUserId?: number | null;
  headFullName?: string | null;
  status: string;
  departmentType: string;
  canEditName: boolean;
  canToggleStatus: boolean;
}

/** UC-102 — update department name (only the name is sent). */
export interface UpdateDepartmentNameRequest {
  departmentId: number;
  name: string;
}

export interface UpdateDepartmentNameResponse {
  departmentId: number;
  name: string;
  campusName: string;
  headFullName?: string | null;
  status: string;
  departmentType: string;
  updatedAt?: string | null;
  changed: boolean;
  message: string;
}
