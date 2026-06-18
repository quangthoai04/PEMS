import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  CreateAccountRequest,
  CreateAccountResponse,
  UpdateAccountRoleRequest,
  UpdateAccountRoleResponse,
} from '../types/accountManagement.types';

/**
 * Account management API (UC-95..UC-100). Backed by AccountsController.
 * Only Create (UC-96) and Update Role (UC-100) are implemented server-side so far;
 * list/search/status endpoints are scaffolded and will 501/NotImplemented until built.
 */
export const accountManagementApi = {
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
