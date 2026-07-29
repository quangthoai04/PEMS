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
  EditPendingAccountEmailRequest,
  EditPendingAccountEmailResponse,
  HoCampusCheck,
  ManageAccountStatusRequest,
  ManageAccountStatusResponse,
  PaginatedResult,
  RelatedVisitorDetail,
  RelatedVisitorListItem,
  RelatedVisitorQueryParams,
  ReplaceStaffLeaderRequest,
  ReplaceStaffLeaderResponse,
  ResendAccountEmailConfirmationRequest,
  ResendAccountEmailConfirmationResponse,
  RoleAssignmentOptions,
  StaffLeaderAvailability,
  StaffLeaderReplacementPreview,
  UpdateAccountRoleRequest,
  UpdateAccountRoleResponse,
  UpdateBasicAccountInfoRequest,
  UpdateBasicAccountInfoResponse,
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
   * UC-100-SL — role-assignment options for the "Chỉnh sửa vai trò" modal. Campus scope is
   * resolved server-side from the authenticated Staff Leader; only the target account id is sent.
   */
  async getRoleAssignmentOptions(targetUserId: string | number): Promise<RoleAssignmentOptions> {
    const { data } = await httpClient.get<RoleAssignmentOptions>(
      API_ENDPOINTS.accounts.roleAssignmentOptions,
      { params: { targetUserId } },
    );
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

  /**
   * HO_BASIC_INFO — HO edits ONLY the full name + email of another HO / Staff Leader. When the email
   * changes the backend re-points auth providers and revokes all active sessions.
   */
  async updateBasicAccountInfo(payload: UpdateBasicAccountInfoRequest): Promise<UpdateBasicAccountInfoResponse> {
    const { data } = await httpClient.post<UpdateBasicAccountInfoResponse>(
      API_ENDPOINTS.accounts.updateBasicInfo,
      payload,
    );
    return data;
  },

  /**
   * Re-issues the email-confirmation link for an account still awaiting confirmation. The backend
   * supersedes the previous token (so the old link dies), enforces the cooldown and the resend cap,
   * and reports the delivery outcome truthfully — the account stays pending either way.
   */
  async resendEmailConfirmation(
    payload: ResendAccountEmailConfirmationRequest,
  ): Promise<ResendAccountEmailConfirmationResponse> {
    const { data } = await httpClient.post<ResendAccountEmailConfirmationResponse>(
      API_ENDPOINTS.accounts.resendEmailConfirmation,
      payload,
    );
    return data;
  },

  /**
   * Corrects the address of an account still awaiting confirmation, optionally renaming it in the
   * same transaction. The backend supersedes the old token and mails the SAME activation email the
   * create flow sends to the new address — the account stays pending until its holder clicks it.
   *
   * Use this instead of {@link updateBasicAccountInfo} whenever a pending account's email changes:
   * the basic-info flow only sends a "your address changed" notice, which carries no activation link
   * and therefore leaves such an account with no way to ever log in.
   */
  async editPendingAccountEmail(
    payload: EditPendingAccountEmailRequest,
  ): Promise<EditPendingAccountEmailResponse> {
    const { data } = await httpClient.post<EditPendingAccountEmailResponse>(
      API_ENDPOINTS.accounts.editPendingEmail,
      payload,
    );
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

  /**
   * Staff Leader "Visitor liên quan" tab — paged, read-only list of Visitor accounts related to
   * the caller's campus. The campus scope is resolved server-side from the authenticated Staff
   * Leader; no campusId is sent (and any would be ignored by the backend).
   */
  async getRelatedVisitors(
    params: RelatedVisitorQueryParams,
  ): Promise<PaginatedResult<RelatedVisitorListItem>> {
    const clean = Object.fromEntries(
      Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
    );
    const { data } = await httpClient.get<PaginatedResult<RelatedVisitorListItem>>(
      API_ENDPOINTS.accounts.relatedVisitors,
      { params: clean },
    );
    return data;
  },

  /** Staff Leader "Visitor liên quan" tab — detail of one related Visitor (scope re-checked server-side). */
  async getRelatedVisitorDetails(visitorUserId: string | number): Promise<RelatedVisitorDetail> {
    const { data } = await httpClient.get<RelatedVisitorDetail>(
      API_ENDPOINTS.accounts.relatedVisitorDetails,
      { params: { visitorUserId } },
    );
    return data;
  },
};
