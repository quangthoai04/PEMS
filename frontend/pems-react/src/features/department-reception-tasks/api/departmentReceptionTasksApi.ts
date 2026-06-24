import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';

export const departmentReceptionTasksApi = {
  getCalendar: async (month: string) => {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.departmentReceptionTasks.calendar, { params: { month } });
    return data;
  },

  getInvitationDetail: async (participantId: number | string) => {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.departmentReceptionTasks.invitationDetail(participantId));
    return data;
  },

  acceptInvitation: async (participantId: number | string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.acceptInvitation(participantId));
    return data;
  },

  declineInvitation: async (participantId: number | string, reason: string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.declineInvitation(participantId), { reason });
    return data;
  },

  getRequestDetail: async (logisticsItemId: number | string) => {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.departmentReceptionTasks.requestDetail(logisticsItemId));
    return data;
  },

  confirmRequest: async (logisticsItemId: number | string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.confirmRequest(logisticsItemId));
    return data;
  },

  rejectRequest: async (logisticsItemId: number | string, reason: string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.rejectRequest(logisticsItemId), { reason });
    return data;
  },

  proposeChange: async (logisticsItemId: number | string, proposedUsageStartAt: string | null, proposedUsageEndAt: string | null, proposedDescription: string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.proposeChange(logisticsItemId), {
      proposedUsageStartAt,
      proposedUsageEndAt,
      proposedDescription
    });
    return data;
  },

  assignAssignee: async (logisticsItemId: number | string, assigneeUserId: number | string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.assignAssignee(logisticsItemId), { assigneeUserId });
    return data;
  },

  getAssigneeCandidates: async () => {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.departmentReceptionTasks.assigneeCandidates);
    return data;
  },

  createPersonalEvent: async (title: string, description: string, date: string, startTime: string, endTime: string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.personalEvents, {
      title, description, date, startTime, endTime
    });
    return data;
  }
};
