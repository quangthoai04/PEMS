/**
 * API dashboard bảng lịch cho Staff Leader (STAFF+LEADER) và Staff thường (STAFF+STAFF).
 * Backend lọc toàn bộ theo role/scope và trả action flags — frontend chỉ render theo flags.
 */
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';

export type StaffCalendarColorType =
  | 'NEW'
  | 'NEEDS_ACTION'
  | 'PROCESSED'
  | 'CANCELLED_OR_EXPIRED'
  | 'MINE';

export interface StaffCalendarAllowedActions {
  canViewDetail: boolean;
  canApprove: boolean;
  canReject: boolean;
  canAssignHost: boolean;
  canAcceptHost: boolean;
  canDeclineHost: boolean;
  canSendHostInvitationEmail: boolean;
}

export interface StaffCalendarItem {
  visitRequestId: number;
  visitInstanceId: number;
  requestCode: string | null;
  title: string;
  delegationName: string | null;
  registrantFullName: string | null;
  registrantOrganization: string | null;
  campusId: number;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  requestStatus: string;
  campusStatus: string | null;
  visitScope: string | null;
  currentHostUserId: number | null;
  currentHostName: string | null;
  isCurrentHost: boolean;
  isPast: boolean;
  isCancelled: boolean;
  isExpired: boolean;
  displayStatus: string;
  colorType: StaffCalendarColorType;
  allowedActions: StaffCalendarAllowedActions;
}

export interface StaffCalendarResponse {
  viewMode: 'office' | 'mine';
  from: string;
  to: string;
  items: StaffCalendarItem[];
}

export interface StaffCalendarParticipantResponse {
  participantId: number;
  userId: number;
  fullName: string;
  participantRole: string;
  status: string;
  respondedAt: string | null;
  note: string | null;
}

export interface StaffCalendarDetail {
  visitRequestId: number;
  visitInstanceId: number;
  requestCode: string | null;
  delegationName: string | null;
  visitScope: string | null;
  requestStatus: string;
  campusStatus: string | null;
  displayStatus: string;
  colorType: StaffCalendarColorType;
  campusId: number;
  campusName: string;
  plannedStartAt: string;
  plannedEndAt: string;
  registrantFullName: string | null;
  registrantOrganization: string | null;
  registrantJobTitle: string | null;
  registrantNationality: string | null;
  registrantPhone: string | null;
  registrantEmail: string | null;
  contactPersonFullName: string | null;
  contactPersonPhone: string | null;
  contactPersonEmail: string | null;
  purpose: string | null;
  workingContent: string | null;
  visitType: string | null;
  visitTypeOther: string | null;
  guestCount: number;
  workingLanguage: string | null;
  mediaConsentStatus: string | null;
  mediaConsentNote: string | null;
  transportationType: string | null;
  transportationDetail: string | null;
  noteToFptu: string | null;
  currentHostUserId: number | null;
  currentHostName: string | null;
  currentHostEmail: string | null;
  hostAssignedAt: string | null;
  hostAssignedByName: string | null;
  isCurrentHost: boolean;
  decisionNote: string | null;
  decidedByName: string | null;
  decidedAt: string | null;
  isCancelled: boolean;
  cancellationReason: string | null;
  cancelledAt: string | null;
  isPast: boolean;
  isExpired: boolean;
  participantResponses: StaffCalendarParticipantResponse[];
  allowedActions: StaffCalendarAllowedActions;
}

export const staffCalendarApi = {
  /** Lịch văn phòng / Lịch của tôi theo khoảng ngày đang hiển thị (from/to = YYYY-MM-DD). */
  async getCalendar(params: {
    viewMode: 'office' | 'mine';
    from: string;
    to: string;
  }): Promise<StaffCalendarResponse> {
    const { data } = await httpClient.get<StaffCalendarResponse>(
      API_ENDPOINTS.dashboard.staffCalendar,
      { params },
    );
    return data;
  },

  /** Chi tiết một yêu cầu đến thăm cho modal trên bảng lịch. */
  async getDetail(visitInstanceId: number | string): Promise<StaffCalendarDetail> {
    const { data } = await httpClient.get<StaffCalendarDetail>(
      API_ENDPOINTS.dashboard.staffCalendarDetail(visitInstanceId),
    );
    return data;
  },
};
