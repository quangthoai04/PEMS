import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  CampusDetail,
  CampusFilterOptions,
  CampusListItem,
  CampusListQueryParams,
  CreateCampusRequest,
  CreateCampusResponse,
  ManageCampusStatusRequest,
  ManageCampusStatusResponse,
  PaginatedResult,
  UpdateCampusRequest,
  UpdateCampusResponse,
} from '../types/campusManagement.types';

/** Drops undefined/null/'' so they never reach the query string. */
function cleanParams(params: CampusListQueryParams): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

/**
 * HO Campus Management API. Backed by CampusesController.
 * UC-82 list (also serves search/filter), UC-83 filter options, UC-86 status toggle.
 */
export const campusManagementApi = {
  /** UC-82 — paged campus list (ACTIVE + INACTIVE), default sort name ASC. */
  async getCampuses(params: CampusListQueryParams): Promise<PaginatedResult<CampusListItem>> {
    const { data } = await httpClient.get<PaginatedResult<CampusListItem>>(
      API_ENDPOINTS.campuses.list,
      { params: cleanParams(params) },
    );
    return data;
  },

  /** UC-83 — search & filter campuses (same shape as getCampuses, dedicated endpoint). */
  async searchCampuses(params: CampusListQueryParams): Promise<PaginatedResult<CampusListItem>> {
    const { data } = await httpClient.get<PaginatedResult<CampusListItem>>(
      API_ENDPOINTS.campuses.search,
      { params: cleanParams(params) },
    );
    return data;
  },

  /** UC-83 — campus/city/status filter options from the database. */
  async getFilterOptions(): Promise<CampusFilterOptions> {
    const { data } = await httpClient.get<CampusFilterOptions>(API_ENDPOINTS.campuses.filterOptions);
    return data;
  },

  /** UC-86 — enable/disable a campus. */
  async manageCampusStatus(payload: ManageCampusStatusRequest): Promise<ManageCampusStatusResponse> {
    const { data } = await httpClient.post<ManageCampusStatusResponse>(
      API_ENDPOINTS.campuses.manageStatus,
      payload,
    );
    return data;
  },

  /** UC-81 — create a campus (backend auto-creates the IC department). */
  async createCampus(payload: CreateCampusRequest): Promise<CreateCampusResponse> {
    const { data } = await httpClient.post<CreateCampusResponse>(API_ENDPOINTS.campuses.create, payload);
    return data;
  },

  /** UC-84 — full campus detail. */
  async getCampusDetails(campusId: string | number): Promise<CampusDetail> {
    const { data } = await httpClient.get<CampusDetail>(API_ENDPOINTS.campuses.details, {
      params: { campusId },
    });
    return data;
  },

  /** UC-85 — update campus master data. */
  async updateCampus(payload: UpdateCampusRequest): Promise<UpdateCampusResponse> {
    const { data } = await httpClient.post<UpdateCampusResponse>(API_ENDPOINTS.campuses.update, payload);
    return data;
  },
};
