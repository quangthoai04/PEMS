import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  CreateDepartmentRequest,
  CreateDepartmentResponse,
  DepartmentDetail,
  DepartmentListItem,
  DepartmentListQueryParams,
  DepartmentStatusImpact,
  ManageDepartmentStatusRequest,
  ManageDepartmentStatusResponse,
  PaginatedResult,
  UpdateDepartmentNameRequest,
  UpdateDepartmentNameResponse,
} from '../types/departmentManagement.types';

/** Drops undefined/null/'' so they never reach the query string. */
function cleanParams(params: DepartmentListQueryParams): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

export const departmentManagementApi = {
  // ── Staff Leader Department Management (UC-101..UC-105) ──

  /** UC-104/UC-103 — paged department list for the caller's campus (also serves search/filter). */
  async getDepartments(params: DepartmentListQueryParams): Promise<PaginatedResult<DepartmentListItem>> {
    const { data } = await httpClient.get<PaginatedResult<DepartmentListItem>>(
      API_ENDPOINTS.departments.list,
      { params: cleanParams(params) },
    );
    return data;
  },

  /** UC-101 — create a GENERAL department in the caller's campus (name only). */
  async createDepartment(payload: CreateDepartmentRequest): Promise<CreateDepartmentResponse> {
    const { data } = await httpClient.post<CreateDepartmentResponse>(
      API_ENDPOINTS.departments.create,
      payload,
    );
    return data;
  },

  /** UC-106 — impact preview (affected accounts/sessions/blockers) for the confirmation modal. */
  async getDepartmentStatusImpact(
    departmentId: number,
    newStatus: 'ACTIVE' | 'INACTIVE',
  ): Promise<DepartmentStatusImpact> {
    const { data } = await httpClient.get<DepartmentStatusImpact>(
      API_ENDPOINTS.departments.statusImpact,
      { params: { departmentId, newStatus } },
    );
    return data;
  },

  /** Enable/disable a GENERAL department (toggle shown in the UC-104 list). */
  async manageDepartmentStatus(
    payload: ManageDepartmentStatusRequest,
  ): Promise<ManageDepartmentStatusResponse> {
    const { data } = await httpClient.post<ManageDepartmentStatusResponse>(
      API_ENDPOINTS.departments.manageStatus,
      payload,
    );
    return data;
  },

  /** UC-105 — department detail (general info + canEditName/canToggleStatus). */
  async getDepartmentDetails(departmentId: string | number): Promise<DepartmentDetail> {
    const { data } = await httpClient.get<DepartmentDetail>(API_ENDPOINTS.departments.details, {
      params: { departmentId },
    });
    return data;
  },

  /** UC-102 — update a GENERAL department's name (name only). */
  async updateDepartmentName(payload: UpdateDepartmentNameRequest): Promise<UpdateDepartmentNameResponse> {
    const { data } = await httpClient.post<UpdateDepartmentNameResponse>(
      API_ENDPOINTS.departments.update,
      payload,
    );
    return data;
  },

  // ── Department personnel (separate module, unchanged) ──
  searchPersonnel: (params: { departmentId: number; keyword?: string; status?: string; pageNumber?: number; pageSize?: number }) => {
    return httpClient.get('/Departments/searchpersonnel', { params });
  },

  addDepartmentPersonnel: (data: { departmentId: number; fullName: string; email: string; phone: string; gender: number; role: string }) => {
    return httpClient.post('/Departments/adddepartmentpersonnel', data);
  },

  updateDepartmentPersonnel: (data: { departmentId: number; userId: number; fullName: string; phone: string; gender: number }) => {
    return httpClient.put('/Departments/updatedepartmentpersonnel', data);
  },

  viewPersonnelDetails: (params: { departmentId: number; userId: number }) => {
    return httpClient.get('/Departments/viewpersonneldetails', { params });
  },

  reassignDepartmentLead: (data: { departmentId: number; newLeaderUserId: number }) => {
    return httpClient.post('/Departments/reassigndepartmentlead', data);
  },

  removePersonnel: (data: { departmentId: number; userId: number }) => {
    return httpClient.post('/Departments/removepersonnel', data);
  },
};
