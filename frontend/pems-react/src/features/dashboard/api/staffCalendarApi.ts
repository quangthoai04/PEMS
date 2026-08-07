/**
 * API dashboard bảng lịch cho Staff Leader (STAFF+LEADER) và Staff thường (STAFF+STAFF).
 * Backend lọc toàn bộ theo role/scope và trả action flags — frontend chỉ render theo flags.
 */
import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';

export type StaffCalendarColorType =
  | 'NEEDS_ACTION'
  | 'MINE'
  | 'PROCESSED'
  | 'CANCELLED_OR_EXPIRED'
  | 'NEUTRAL';

export interface StaffCalendarAllowedActions {
  canViewDetail: boolean;
  canApprove: boolean;
  canReject: boolean;
  canAssignHost: boolean;
  /** True khi user hiện tại là host của instance — vào trang Setup đoàn khách. */
  canSetupDelegation: boolean;
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

/** Lịch cá nhân tự tạo (nút + trên bảng lịch) — hiển thị màu tím, không phải yêu cầu đến thăm. */
export interface StaffCalendarPersonalEvent {
  calendarEventId: number;
  title: string;
  description: string | null;
  startAt: string;
  endAt: string;
}

export interface StaffCalendarResponse {
  viewMode: 'office' | 'mine';
  from: string;
  to: string;
  items: StaffCalendarItem[];
  personalEvents: StaffCalendarPersonalEvent[];
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
  /**
   * The guest-side coordinator of THIS campus instance — never the registrant and never the
   * reception host. Three independent relations that can be three different people.
   */
  operationalContactFullName: string | null;
  operationalContactOrganization: string | null;
  operationalContactJobTitle: string | null;
  operationalContactPhone: string | null;
  operationalContactEmail: string | null;
  purpose: string | null;
  workingContent: string | null;
  visitType: string | null;
  visitTypeOther: string | null;
  guestCount: number;
  workingLanguage: string | null;
  mediaConsentStatus: string | null;
  /** Free text nhận diện phương tiện di chuyển tới FPTU (SQL v10). */
  transportationNote: string | null;
  /** "Ghi chú gửi FPTU" — ghi chú chung của khách, độc lập với đồng ý truyền thông. */
  notes: string | null;
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
  /**
   * Lịch văn phòng / Lịch của tôi theo khoảng ngày đang hiển thị (from/to = YYYY-MM-DD),
   * hoặc theo trọn năm khi truyền `year` (dùng cho bộ lọc năm trên UI — from/to bị bỏ qua).
   */
  async getCalendar(params: {
    viewMode: 'office' | 'mine';
    from: string;
    to: string;
    year?: number;
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
