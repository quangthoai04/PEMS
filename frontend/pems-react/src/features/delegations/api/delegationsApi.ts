import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  CancelVisitRequestPayload,
  CancelVisitRequestResult,
  HostCandidate,
  VisitInvitation,
  RespondInvitationPayload,
  RespondInvitationResult,
} from '../types/delegations.types';

export const delegationsApi = {
  /**
   * UC-20 list. `params.tab` = "responsible" (Đơn phụ trách) | "attending" (Đơn mời tham dự).
   * The backend filters by role/scope and returns `allowedActions` per row.
   */
  async getVisitRequestManagementList(params?: Record<string, unknown>): Promise<any> {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.delegations.managementList, { params });
    return data;
  },

  /** UC-18: HO approves a MULTI_CAMPUS request (auto-assigns each campus IC head as interim host). */
  async hoApprove(visitRequestId: number | string): Promise<any> {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.delegations.hoApprove(visitRequestId), {});
    return data;
  },

  /** UC-18: HO rejects a MULTI_CAMPUS request (reason mandatory). */
  async hoReject(visitRequestId: number | string, reason: string): Promise<any> {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.delegations.hoReject(visitRequestId), { reason });
    return data;
  },

  /** UC-22: Staff Leader rejects a SINGLE_CAMPUS request of their campus (reason mandatory). */
  async campusReject(visitRequestId: number | string, reason: string): Promise<any> {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.delegations.campusReject(visitRequestId), { reason });
    return data;
  },

  /** UC-22: list staff who can host a campus instance, each flagged with schedule conflicts. */
  async getHostCandidates(visitInstanceId: number | string): Promise<HostCandidate[]> {
    const { data } = await httpClient.get<HostCandidate[]>(API_ENDPOINTS.delegations.hostCandidates(visitInstanceId));
    return data;
  },

  /**
   * UC-22: Staff Leader picks the host for a campus instance — approves+assigns for a
   * single-campus request, or transfers the host for an HO-approved multi-campus request.
   */
  async assignHost(
    visitRequestId: number | string,
    visitInstanceId: number | string,
    hostUserId: number,
  ): Promise<any> {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.delegations.assignHost(visitRequestId, visitInstanceId),
      { hostUserId },
    );
    return data;
  },

  /**
   * UC-136: cancel a visit request (Visitor self-cancel — incl. pending — or the assigned Host).
   * Cancels every still-cancellable campus instance; the request becomes CANCELLED when all are.
   */
  async cancelVisitRequest(
    visitRequestId: number | string,
    payload: CancelVisitRequestPayload,
  ): Promise<CancelVisitRequestResult> {
    const { data } = await httpClient.post<CancelVisitRequestResult>(
      API_ENDPOINTS.delegations.cancel(visitRequestId),
      payload,
    );
    return data;
  },

  /** UC-136: cancel a single campus instance (current Host after external confirmation). */
  async cancelVisitRequestCampus(
    visitRequestId: number | string,
    visitInstanceId: number | string,
    payload: CancelVisitRequestPayload,
  ): Promise<CancelVisitRequestResult> {
    const { data } = await httpClient.post<CancelVisitRequestResult>(
      API_ENDPOINTS.delegations.cancelCampus(visitRequestId, visitInstanceId),
      payload,
    );
    return data;
  },

  /** UC-27: the signed-in user's invitations (pending only unless `includeResponded`). */
  async getMyInvitations(includeResponded = false): Promise<VisitInvitation[]> {
    const { data } = await httpClient.get<VisitInvitation[]>(
      API_ENDPOINTS.delegations.myInvitations,
      { params: { includeResponded } },
    );
    return data;
  },

  /** UC-27: one invitation for the invitation-detail screen (ownership enforced server-side). */
  async getInvitationDetail(participantId: number | string): Promise<VisitInvitation> {
    const { data } = await httpClient.get<VisitInvitation>(
      API_ENDPOINTS.delegations.invitationDetail(participantId),
    );
    return data;
  },

  /** UC-27: accept or decline an invitation (decline requires a reason). */
  async respondInvitation(
    participantId: number | string,
    payload: RespondInvitationPayload,
  ): Promise<RespondInvitationResult> {
    const { data } = await httpClient.post<RespondInvitationResult>(
      API_ENDPOINTS.delegations.respondInvitation(participantId),
      payload,
    );
    return data;
  },

  visitInvitations: {
    async getMyInvitations(params?: Record<string, unknown>): Promise<any> {
      const { data } = await httpClient.get<any>(API_ENDPOINTS.visitInvitations.my, { params });
      return data;
    },
    async acceptInvitation(participantId: string | number): Promise<any> {
      const { data } = await httpClient.post<any>(API_ENDPOINTS.visitInvitations.accept(participantId));
      return data;
    },
    async declineInvitation(participantId: string | number, reason: string): Promise<any> {
      const { data } = await httpClient.post<any>(API_ENDPOINTS.visitInvitations.decline(participantId), { reason });
      return data;
    },
    async assignDepartmentStaff(participantId: string | number, departmentStaffUserId: number, note: string): Promise<any> {
      const { data } = await httpClient.post<any>(API_ENDPOINTS.visitInvitations.assignDepartmentStaff(participantId), { departmentStaffUserId, note });
      return data;
    },
  },
};
