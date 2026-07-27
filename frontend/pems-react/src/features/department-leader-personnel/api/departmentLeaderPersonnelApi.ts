import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  ChangePersonnelStatusRequest,
  ChangePersonnelStatusResponse,
  CreatePersonnelRequest,
  CreatePersonnelResponse,
  LeaderCandidates,
  MyDepartment,
  PagedPersonnel,
  PersonnelDetail,
  PersonnelListQuery,
  PersonnelStatusImpact,
  ResendConfirmationResponse,
  TransferLeadershipResponse,
  UpdatePersonnelRequest,
  UpdatePersonnelResponse,
} from '../types/departmentLeaderPersonnel.types';

/** Drops undefined/null/'' so they never reach the query string as literal "undefined". */
function cleanParams(params: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''),
  );
}

/**
 * Client for `/api/department-leader`.
 *
 * Every call is implicitly scoped to the signed-in Leader's own department — there is no
 * `departmentId` argument on any method, because the API does not accept one. That is the whole
 * point of this module existing separately from `departmentManagementApi`, whose personnel endpoints
 * take a client-supplied department id.
 */
export const departmentLeaderPersonnelApi = {
  /** Department header + personnel head-count breakdown. */
  async getMyDepartment(): Promise<MyDepartment> {
    const { data } = await httpClient.get<MyDepartment>(API_ENDPOINTS.departmentLeader.department);
    return data;
  },

  /** Paged list. Search/filter/sort/paging are all applied server-side. */
  async listPersonnel(query: PersonnelListQuery): Promise<PagedPersonnel> {
    const { data } = await httpClient.get<PagedPersonnel>(API_ENDPOINTS.departmentLeader.personnel, {
      // 'ALL' is the client-side sentinel for "no filter"; the server treats it the same way, but
      // dropping it keeps the request URL honest about what is actually being filtered.
      params: cleanParams({ ...query, status: query.status === 'ALL' ? undefined : query.status }),
    });
    return data;
  },

  /**
   * Full record for the detail/edit modal. Always call this when opening the modal — a list row is
   * a summary and does not carry every field the edit form needs.
   */
  async getPersonnelDetail(userId: number): Promise<PersonnelDetail> {
    const { data } = await httpClient.get<PersonnelDetail>(
      API_ENDPOINTS.departmentLeader.personnelDetail(userId),
    );
    return data;
  },

  /** Creates a department staff member. Role/department/campus/status are server-assigned. */
  async createPersonnel(payload: CreatePersonnelRequest): Promise<CreatePersonnelResponse> {
    const { data } = await httpClient.post<CreatePersonnelResponse>(
      API_ENDPOINTS.departmentLeader.personnel,
      payload,
    );
    return data;
  },

  /** Edits name/email/phone/gender. The email is editable in every account status. */
  async updatePersonnel(
    userId: number,
    payload: UpdatePersonnelRequest,
  ): Promise<UpdatePersonnelResponse> {
    const { data } = await httpClient.put<UpdatePersonnelResponse>(
      API_ENDPOINTS.departmentLeader.personnelDetail(userId),
      payload,
    );
    return data;
  },

  /** Read-only preview shown before a status toggle is confirmed. Writes nothing. */
  async getStatusImpact(
    userId: number,
    targetStatus: 'ACTIVE' | 'INACTIVE',
  ): Promise<PersonnelStatusImpact> {
    const { data } = await httpClient.get<PersonnelStatusImpact>(
      API_ENDPOINTS.departmentLeader.personnelStatusImpact(userId),
      { params: { targetStatus } },
    );
    return data;
  },

  /** Applies the status change. The server re-runs the same blocker evaluation as the preview. */
  async changePersonnelStatus(
    userId: number,
    payload: ChangePersonnelStatusRequest,
  ): Promise<ChangePersonnelStatusResponse> {
    const { data } = await httpClient.patch<ChangePersonnelStatusResponse>(
      API_ENDPOINTS.departmentLeader.personnelStatus(userId),
      payload,
    );
    return data;
  },

  /** Re-sends the confirmation link. The destination address is read server-side. */
  async resendEmailConfirmation(userId: number): Promise<ResendConfirmationResponse> {
    const { data } = await httpClient.post<ResendConfirmationResponse>(
      API_ENDPOINTS.departmentLeader.personnelResendConfirmation(userId),
      {},
    );
    return data;
  },

  /** Eligible successors — a dedicated endpoint, never the current page of the personnel list. */
  async getLeaderCandidates(): Promise<LeaderCandidates> {
    const { data } = await httpClient.get<LeaderCandidates>(
      API_ENDPOINTS.departmentLeader.leaderCandidates,
    );
    return data;
  },

  /** Hands the department over. On success the caller is no longer a Leader and must sign in again. */
  async transferLeadership(newLeaderUserId: number): Promise<TransferLeadershipResponse> {
    const { data } = await httpClient.post<TransferLeadershipResponse>(
      API_ENDPOINTS.departmentLeader.transferLeadership,
      { newLeaderUserId },
    );
    return data;
  },
};
