import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';

export const departmentReceptionTasksApi = {
  getCalendar: async (month: string) => {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.departmentReceptionTasks.calendar, { params: { month } });
    return data;
  },

  getAssignmentsProgress: async (params: Record<string, any>) => {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.departmentReceptionTasks.assignmentsProgress, { params });
    return data;
  },

  getAttentionItems: async () => {
    const { data } = await httpClient.get<any>(API_ENDPOINTS.departmentReceptionTasks.attentionItems);
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

  assignInvitation: async (
    participantId: number | string,
    assigneeUserId: number | string,
    note = '',
    emailOverride?: {
      useEditedContent: boolean;
      subject: string;
      bodyHtml: string;
      attachments?: { fileId: number; attachmentType?: 'ATTACHMENT' | 'INLINE_IMAGE'; contentId?: string | null; displayName?: string | null; displayOrder?: number }[];
    },
  ) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.assignInvitation(participantId), { assigneeUserId, note, emailOverride });
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

  acceptRequestSelf: async (logisticsItemId: number | string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.acceptRequestSelf(logisticsItemId));
    return data;
  },

  rejectRequest: async (logisticsItemId: number | string, reason: string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.rejectRequest(logisticsItemId), { reason });
    return data;
  },

  proposeChange: async (
    logisticsItemId: number | string,
    payload: {
      proposedQuantity?: number | null;
      proposedUsageStartAt?: string | null;
      proposedUsageEndAt?: string | null;
      proposedDescription?: string | null;
      proposalNote: string;
    },
  ) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.proposeChange(logisticsItemId), payload);
    return data;
  },

  assignAssignee: async (
    logisticsItemId: number | string,
    assigneeUserId: number | string,
    emailOverride?: {
      useEditedContent: boolean;
      subject: string;
      bodyHtml: string;
      attachments?: { fileId: number; attachmentType?: 'ATTACHMENT' | 'INLINE_IMAGE'; contentId?: string | null; displayName?: string | null; displayOrder?: number }[];
    },
  ) => {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.departmentReceptionTasks.assignAssignee(logisticsItemId),
      { assigneeUserId, emailOverride });
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
  },

  updatePersonalEvent: async (id: number | string, title: string, description: string, date: string, startTime: string, endTime: string) => {
    const { data } = await httpClient.put<any>(`${API_ENDPOINTS.departmentReceptionTasks.personalEvents}/${id}`, {
      calendarEventId: Number(id), title, description, date, startTime, endTime
    });
    return data;
  },

  deletePersonalEvent: async (id: number | string) => {
    const { data } = await httpClient.delete<any>(`${API_ENDPOINTS.departmentReceptionTasks.personalEvents}/${id}`);
    return data;
  },

  acceptAssignment: async (logisticsItemId: number | string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.acceptAssignment(logisticsItemId));
    return data;
  },

  declineAssignment: async (logisticsItemId: number | string, reason: string) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.declineAssignment(logisticsItemId), { reason });
    return data;
  },

  signHandover: async (
    logisticsItemId: number | string,
    handoverType: 'BORROW' | 'RETURN',
    signerSide: 'BORROWER' | 'PROVIDER',
    note?: string
  ) => {
    const { data } = await httpClient.post<any>(API_ENDPOINTS.departmentReceptionTasks.signHandover(logisticsItemId), {
      handoverType,
      signerSide,
      note
    });
    return data;
  }
};
