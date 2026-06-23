import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AccountDetails,
  AccountListItem,
  AccountListQueryParams,
  AccountStatistics,
  ActiveCampusOption,
  CampusDepartmentOption,
  CreateAccountRequest,
  CreateAccountResponse,
  HoCampusCheck,
  ManageAccountStatusRequest,
  ManageAccountStatusResponse,
  PaginatedResult,
  ReplaceStaffLeaderRequest,
  ReplaceStaffLeaderResponse,
  StaffLeaderAvailability,
  StaffLeaderReplacementPreview,
  UpdateAccountRoleRequest,
  UpdateAccountRoleResponse,
} from '../types/accountManagement.types';

/** Drops undefined/null/'' so they never reach the query string. */
function cleanParams(params: AccountListQueryParams): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

/**
 * Account management API (UC-95..UC-100). Backed by AccountsController.
 * List (UC-95), Search/Filter (UC-99), Create (UC-96) and Update Role (UC-100) are
 * implemented server-side; details/status flows are still scaffolded.
 */
export const accountManagementApi = {
  /** UC-95 — paged, scoped account list (also serves search/filter). */
  async getAccounts(params: AccountListQueryParams): Promise<PaginatedResult<AccountListItem>> {
    const { data } = await httpClient.get<PaginatedResult<AccountListItem>>(
      API_ENDPOINTS.accounts.list,
      { params: cleanParams(params) },
    );
    return data;
  },

  /** UC-99 — search & filter accounts (same shape as getAccounts, dedicated endpoint/permission). */
  async searchAccounts(params: AccountListQueryParams): Promise<PaginatedResult<AccountListItem>> {
    const { data } = await httpClient.get<PaginatedResult<AccountListItem>>(
      API_ENDPOINTS.accounts.search,
      { params: cleanParams(params) },
    );
    return data;
  },

  /** Active campuses for the campus filter dropdown. */
  async getActiveCampuses(): Promise<ActiveCampusOption[]> {
    const { data } = await httpClient.get<ActiveCampusOption[]>(API_ENDPOINTS.campuses.active);
    return data;
  },

  /** UC-95-SL — account statistics scoped to the caller (campus for a Staff Leader). */
  async getStatistics(): Promise<AccountStatistics> {
    const { data } = await httpClient.get<AccountStatistics>(API_ENDPOINTS.accounts.statistics);
    return data;
  },

  /** Active GENERAL departments of the caller's campus (Department-Leader dropdown). */
  async getCampusDepartments(): Promise<CampusDepartmentOption[]> {
    const { data } = await httpClient.get<CampusDepartmentOption[]>(API_ENDPOINTS.accounts.campusDepartments);
    return data;
  },

  /**
   * UC-96 — Staff Leader availability pre-check for a campus (HO only). Called when HO picks a
   * campus in the create modal so the form can warn/disable before submit.
   */
  async getStaffLeaderAvailability(campusId: string | number): Promise<StaffLeaderAvailability> {
    const { data } = await httpClient.get<StaffLeaderAvailability>(
      API_ENDPOINTS.accounts.staffLeaderAvailability,
      { params: { campusId } },
    );
    return data;
  },

  /**
   * UC-96 — HO campus pre-check (HO only). Called when HO picks a campus for role HO in the
   * create modal so the form can warn/disable before submit.
   */
  async getHoCampusCheck(campusId: string | number): Promise<HoCampusCheck> {
    const { data } = await httpClient.get<HoCampusCheck>(
      API_ENDPOINTS.accounts.hoCampusCheck,
      { params: { campusId } },
    );
    return data;
  },

  /** UC-96 — create an internal or visitor account. */
  async createAccount(payload: CreateAccountRequest): Promise<CreateAccountResponse> {
    const { data } = await httpClient.post<CreateAccountResponse>(API_ENDPOINTS.accounts.create, payload);
    return data;
  },

  /** UC-98 — fetch a single account's safe detail projection. */
  async getAccountDetails(userId: string | number): Promise<AccountDetails> {
    const { data } = await httpClient.get<AccountDetails>(API_ENDPOINTS.accounts.details, {
      params: { userId },
    });
    return data;
  },

  /** UC-97 — enable/disable an account (revokes sessions when disabled). */
  async manageAccountStatus(payload: ManageAccountStatusRequest): Promise<ManageAccountStatusResponse> {
    const { data } = await httpClient.post<ManageAccountStatusResponse>(
      API_ENDPOINTS.accounts.manageStatus,
      payload,
    );
    return data;
  },

  /** UC-100 — change a user's role/campus/department (revokes their sessions). */
  async updateAccountRole(payload: UpdateAccountRoleRequest): Promise<UpdateAccountRoleResponse> {
    const { data } = await httpClient.post<UpdateAccountRoleResponse>(API_ENDPOINTS.accounts.updateRole, payload);
    return data;
  },

  /** Replace Staff Leader — preview: current IC Head + eligible candidates for a campus (HO only). */
  async getStaffLeaderReplacementPreview(campusId: string | number): Promise<StaffLeaderReplacementPreview> {
    const { data } = await httpClient.get<StaffLeaderReplacementPreview>(
      API_ENDPOINTS.accounts.staffLeaderReplacementPreview,
      { params: { campusId } },
    );
    return data;
  },

  /** Replace Staff Leader — promote an existing IC Staff or create a new leader (HO only). */
  async replaceStaffLeader(payload: ReplaceStaffLeaderRequest): Promise<ReplaceStaffLeaderResponse> {
    const { data } = await httpClient.post<ReplaceStaffLeaderResponse>(
      API_ENDPOINTS.accounts.replaceStaffLeader,
      payload,
    );
    return data;
  },
};
