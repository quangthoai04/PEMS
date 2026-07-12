export type MinutesStatus = 'DRAFT' | 'SAVED';
export type AttendanceStatus = 'PRESENT' | 'ABSENT' | 'EXCUSED';
export type MinuteActionItemStatus = 'TODO' | 'IN_PROGRESS' | 'DONE' | 'CANCELLED';
export type LockState = 'LOCKED' | 'EXPIRED' | 'UNLOCKED';

export interface MinutesSummary {
  totalMinutes: number;
  draftCount: number;
  savedCount: number;
  lockedCount: number;
  openActionItemCount: number;
  latestUpdatedAt: string | null;
}

export interface MinutesListItem {
  minutesId: number;
  visitInstanceId: number;
  title: string;
  contentPreview: string | null;
  status: MinutesStatus;
  editLockedBy: number | null;
  editLockedByName: string | null;
  editLockedAt: string | null;
  editLockExpiresAt: string | null;
  lockState: LockState;
  rowVersion: number;
  createdAt: string;
  createdByName: string | null;
  updatedAt: string | null;
  updatedByName: string | null;
  visitTitle: string | null;
  visitRequestId: number | null;
  campusName: string | null;
  hostName: string | null;
  plannedStartAt: string | null;
  plannedEndAt: string | null;
  participantTotal: number;
  participantPresentCount: number;
  participantAbsentCount: number;
  participantExcusedCount: number;
  actionItemTotal: number;
  actionItemTodoCount: number;
  actionItemInProgressCount: number;
  actionItemDoneCount: number;
  actionItemCancelledCount: number;
  actionItemOverdueCount: number;
}

export interface MinuteParticipantDto {
  participantId: number;
  visitInstanceId: number;
  userId: number;
  participantRole: string;
  isHost: boolean;
  status: string;
  note: string | null;
  createdAt: string;
  // Note: the backend uses MinuteParticipant for minutes, but GetVisitInstanceMinutesQueryHandler returns MinuteParticipantDto
  // Let's assume these are the fields returned by Participants collection in MinuteDto
  minuteParticipantId?: number;
  minutesId?: number;
  guestMemberId?: number | null;
  fullNameSnapshot?: string | null;
  roleSnapshot?: string | null;
  organizationSnapshot?: string | null;
  emailSnapshot?: string | null;
  attendanceStatus?: AttendanceStatus | null;
  attendanceNote?: string | null;
  checkedAt?: string | null;
  checkedBy?: number | null;
  checkedByName?: string | null;
  displayOrder?: number;
}

export interface MinuteActionItemDto {
  actionItemId: number;
  minutesId: number;
  title: string;
  note: string | null;
  dueDate: string | null;
  status: MinuteActionItemStatus;
  completedAt: string | null;
  displayOrder: number;
  createdAt: string;
  createdBy: number | null;
  createdByName: string | null;
  updatedAt: string | null;
  updatedBy: number | null;
  updatedByName: string | null;
}

export interface MinutesDetail {
  exists: boolean;
  minutesId?: number;
  visitInstanceId: number;
  title?: string;
  content?: string | null;
  status?: MinutesStatus;
  rowVersion: number;
  editLockedBy?: number | null;
  editLockedByName?: string | null;
  editLockedAt?: string | null;
  editLockExpiresAt?: string | null;
  updatedAt?: string | null;
  isLockedByOther: boolean;
  isLockedByMe: boolean;
  editLockToken?: string | null;
  canView: boolean;
  canCreate: boolean;
  canEdit: boolean;
  participants: MinuteParticipantDto[];
  actionItems: MinuteActionItemDto[];
}

export interface MinutesFilterParams {
  q?: string;
  status?: string;
  attendanceStatus?: string;
  actionItemStatus?: string;
  dateType?: string;
  fromDate?: string;
  toDate?: string;
  lockState?: string;
  hasAbsentParticipants?: string;
  participantType?: string;
  /** Chỉ HO gửi lên — lọc theo cơ sở. */
  campusId?: number;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: string;
}

export interface MinutesListResponse {
  items: MinutesListItem[];
  totalCount: number;
  summary: MinutesSummary;
}
