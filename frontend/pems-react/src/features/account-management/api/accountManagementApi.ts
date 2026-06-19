import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  AccountListItem,
  AccountListQueryParams,
  ActiveCampusOption,
  CreateAccountRequest,
  CreateAccountResponse,
  PaginatedResult,
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

  /** UC-96 — create an internal or visitor account. */
  async createAccount(payload: CreateAccountRequest): Promise<CreateAccountResponse> {
    const { data } = await httpClient.post<CreateAccountResponse>(API_ENDPOINTS.accounts.create, payload);
    return data;
  },

  /** UC-100 — change a user's role/campus/department (revokes their sessions). */
  async updateAccountRole(payload: UpdateAccountRoleRequest): Promise<UpdateAccountRoleResponse> {
    const { data } = await httpClient.post<UpdateAccountRoleResponse>(API_ENDPOINTS.accounts.updateRole, payload);
    return data;
  },
};
