/** HO Campus Management (UC-82 list, UC-83 search/filter, UC-86 manage status). */

export type CampusStatus = 'ACTIVE' | 'INACTIVE';

/** Standard paged envelope (mirrors backend PaginatedResult<T>). */
export interface PaginatedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/** UC-82/UC-83 — one campus row. Mirrors backend CampusListItemDto. */
export interface CampusListItem {
  campusId: number;
  campusCode: string;
  name: string;
  city: string | null;
  icHeadUserId: number | null;
  icHeadName: string | null;
  status: CampusStatus;
  createdAt: string;
  updatedAt: string | null;
  canManageStatus: boolean;
}

/** UC-82/UC-83 query params. Empty/undefined values are dropped before the request. */
export interface CampusListQueryParams {
  keyword?: string;
  city?: string;
  campusId?: number;
  status?: CampusStatus;
  page?: number;
  pageSize?: number;
  sortBy?: 'name' | 'campusCode' | 'city' | 'status';
  sortOrder?: 'asc' | 'desc';
}

/** UC-83 §8 — filter options, all sourced from the database. */
export interface CampusFilterOptions {
  cities: string[];
  campuses: {
    campusId: number;
    campusCode: string;
    name: string;
    city: string | null;
    status: CampusStatus;
  }[];
  statuses: { value: CampusStatus; label: string }[];
}

/** UC-86 request/response. */
export interface ManageCampusStatusRequest {
  campusId: number;
  status: CampusStatus;
  reason?: string;
}

export interface ManageCampusStatusResponse {
  campusId: number;
  status: CampusStatus;
  updatedAt: string;
  updatedBy: number | null;
  message: string;
}

/** UC-81 — create campus (master data only; no IC head). */
export interface CreateCampusRequest {
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
}

export interface CreateCampusResponse {
  campusId: number;
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
  icHeadUserId: number | null;
  status: CampusStatus;
  icDepartment: {
    departmentId: number;
    campusId: number;
    name: string;
    departmentType: 'IC';
    status: CampusStatus;
  };
}

/** UC-84 — full campus detail. */
export interface CampusDetail {
  campusId: number;
  campusCode: string;
  name: string;
  city: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  icHeadUserId: number | null;
  icHeadName: string | null;
  status: CampusStatus;
  createdAt: string;
  createdBy: number | null;
  createdByName: string | null;
  updatedAt: string | null;
  updatedBy: number | null;
  updatedByName: string | null;
  icDepartment: {
    departmentId: number;
    name: string;
    departmentType: 'IC';
    status: CampusStatus;
    headUserId: number | null;
    headUserName: string | null;
  } | null;
}

/** UC-85 — update campus master data. */
export interface UpdateCampusRequest {
  campusId: number;
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
}

export interface UpdateCampusResponse {
  campusId: number;
  campusCode: string;
  name: string;
  city: string;
  address: string;
  phone: string;
  email: string;
  status: CampusStatus;
  updatedAt: string;
  updatedBy: number | null;
}
