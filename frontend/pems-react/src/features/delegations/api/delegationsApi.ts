import httpClient from '../../../shared/api/httpClient';
import { API_ENDPOINTS } from '../../../shared/api/endpoints';
import type {
  CancelVisitRequestPayload,
  CancelVisitRequestResult,
  HostCandidate,
  VisitInvitation,
  RespondInvitationPayload,
  RespondInvitationResult,
  SubmittedVisitRequestFormDetail,
  VisitProcessPermission,
  ContributionPage,
  ProcessSummaryPage,
  VisitMinute,
  MinuteParticipant,
  MinuteUserSearchItem,
  SaveMinuteParticipantPayload,
  SaveMinuteActionItemPayload,
  VisitNews,
  VisitNewsList,
  VisitParticipantListItem,
  ParticipantCandidate,
  SupportDepartment,
  InviteVisitParticipantPayload,
  InviteVisitParticipantResult,
  UpdatePreparationNoteResult,
  GetVisitReminderSettingsResult,
  SaveVisitReminderSettingItem,
  SaveVisitReminderSettingsResult,
  PreviewEmailTemplatePayload,
  PreviewEmailTemplateResult,
  PrepareVisitLogisticsPayload,
  PrepareVisitLogisticsResult,
  GetVisitInstanceLogisticsResult,
  SignHandoverBorrowerPayload,
  SignHandoverResult,
  GetSentEmailsResult,
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

  /**
   * Read-only "what the guest submitted" snapshot, shared by the pre-approval review,
   * the approved/waiting-host detail and the rejected detail screens. The backend enforces
   * role/scope/status visibility (HO → MULTI_CAMPUS; Staff Leader → own-campus SINGLE_CAMPUS
   * any status, or own-campus MULTI_CAMPUS only after HO approval; Visitor → own request) and
   * returns 403/404 otherwise.
   */
  async getSubmittedVisitRequestFormDetail(
    visitRequestId: number | string,
  ): Promise<SubmittedVisitRequestFormDetail> {
    const { data } = await httpClient.get<SubmittedVisitRequestFormDetail>(
      API_ENDPOINTS.delegations.submittedFormDetail(visitRequestId),
    );
    return data;
  },

  /**
   * Campus-independent approval: the campus Staff Leader rejects ONLY their campus instance
   * (reason mandatory → stored in the instance's decision_note).
   */
  async rejectCampusInstance(
    visitRequestId: number | string,
    visitInstanceId: number | string,
    reason: string,
  ): Promise<any> {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.delegations.rejectCampusInstance(visitRequestId, visitInstanceId),
      { reason },
    );
    return data;
  },

  /** UC-22: list staff who can host a campus instance, each flagged with schedule conflicts. */
  async getHostCandidates(visitInstanceId: number | string): Promise<HostCandidate[]> {
    const { data } = await httpClient.get<HostCandidate[]>(API_ENDPOINTS.delegations.hostCandidates(visitInstanceId));
    return data;
  },

  /**
   * Phase 2: permission flags for a campus instance's process page (source of truth for which
   * tabs the caller may view/edit). The backend enforces scope (403 if no relation to the instance).
   */
  async getVisitProcessPermissions(visitInstanceId: number | string): Promise<VisitProcessPermission> {
    const { data } = await httpClient.get<VisitProcessPermission>(
      API_ENDPOINTS.delegations.processPermissions(visitInstanceId),
    );
    return data;
  },

  /**
   * Contribution Page payload (permission gate + read-only summary + workspace status). The backend
   * enforces access (Host / ACCEPTED|ASSIGNED participant / Department-with-logistics only) and
   * returns 403/404 otherwise — the page shows Access Denied on those statuses.
   */
  async getVisitInstanceContribution(visitInstanceId: number | string): Promise<ContributionPage> {
    const { data } = await httpClient.get<ContributionPage>(
      API_ENDPOINTS.delegations.contribution(visitInstanceId),
    );
    return data;
  },

  async getVisitInstanceSummary(visitInstanceId: number | string): Promise<ProcessSummaryPage> {
    const { data } = await httpClient.get<ProcessSummaryPage>(
      API_ENDPOINTS.delegations.summary(visitInstanceId),
    );
    return data;
  },

  /**
   * Campus-independent approval: the campus Staff Leader approves their campus instance —
   * the official host is MANDATORY in the same action (IC Staff of the campus, or the Staff
   * Leader themself). Gán host không gửi email — Staff được gán xem qua thông báo/"Lịch của
   * tôi" và tự vào Setup đoàn khách.
   */
  async approveCampusInstance(
    visitRequestId: number | string,
    visitInstanceId: number | string,
    hostUserId: number,
    decisionNote?: string,
  ): Promise<any> {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.delegations.approveCampusInstance(visitRequestId, visitInstanceId),
      { hostUserId, decisionNote: decisionNote?.trim() || undefined },
    );
    return data;
  },

  /** VisitProcess "Trước tiếp khách": real setup detail (agenda + common) for an instance. */
  async getVisitProcessDetail(
    visitRequestId: number | string,
    visitInstanceId: number | string,
  ): Promise<import('../types/delegations.types').VisitProcessDetail> {
    const { data } = await httpClient.get(
      API_ENDPOINTS.delegations.processDetail(visitRequestId, visitInstanceId));
    return data;
  },

  /** "Báo cáo Lịch trình" PDF (giai đoạn Trước tiếp khách) — backend generates + returns the file.
   * languageCode 'en' asks the backend to auto-translate the report content before rendering. */
  async exportScheduleReportPdf(
    visitRequestId: number | string,
    visitInstanceId: number | string,
    languageCode: 'vi' | 'en' = 'vi',
  ): Promise<{ blob: Blob; fileName: string }> {
    const response = await httpClient.get(
      API_ENDPOINTS.delegations.scheduleReportPdf(visitRequestId, visitInstanceId),
      { responseType: 'blob', params: { languageCode } },
    );
    const disposition: string = response.headers?.['content-disposition'] ?? '';
    const utf8Match = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
    const plainMatch = /filename="?([^";]+)"?/i.exec(disposition);
    const fileName = utf8Match
      ? decodeURIComponent(utf8Match[1])
      : plainMatch
        ? plainMatch[1]
        : `BaoCaoLichTrinh-${visitInstanceId}.pdf`;
    return { blob: response.data as Blob, fileName };
  },

  /** VisitProcess "Thành phần tham gia": the instance's host snapshot + invited supporters. */
  async getInstanceParticipants(visitInstanceId: number | string): Promise<VisitParticipantListItem[]> {
    const { data } = await httpClient.get<VisitParticipantListItem[]>(
      API_ENDPOINTS.delegations.instanceParticipants(visitInstanceId));
    return data;
  },

  /** Candidate search for the host's invite dropdowns. type = IC_SUPPORT | STUDENT. */
  async getParticipantCandidates(
    visitInstanceId: number | string,
    type: 'IC_SUPPORT' | 'STUDENT',
    keyword?: string,
  ): Promise<ParticipantCandidate[]> {
    const { data } = await httpClient.get<ParticipantCandidate[]>(
      API_ENDPOINTS.delegations.participantCandidates(visitInstanceId),
      { params: { type, keyword: keyword || undefined } });
    return data;
  },

  /** GENERAL departments of the instance's campus + their resolved active leader (the invitee). */
  async getSupportDepartments(visitInstanceId: number | string, keyword?: string): Promise<SupportDepartment[]> {
    const { data } = await httpClient.get<SupportDepartment[]>(
      API_ENDPOINTS.delegations.supportDepartments(visitInstanceId),
      { params: { keyword: keyword || undefined } });
    return data;
  },

  /** Department staff the calling Department Leader may assign (own department/campus). */
  async getDepartmentStaffCandidates(visitInstanceId: number | string, keyword?: string): Promise<ParticipantCandidate[]> {
    const { data } = await httpClient.get<ParticipantCandidate[]>(
      API_ENDPOINTS.delegations.departmentStaffCandidates(visitInstanceId),
      { params: { keyword: keyword || undefined } });
    return data;
  },

  /** Host invites a supporting participant (sends the invitation email with one-time tokens). */
  async inviteVisitParticipant(
    visitInstanceId: number | string,
    payload: InviteVisitParticipantPayload,
  ): Promise<InviteVisitParticipantResult> {
    const { data } = await httpClient.post<InviteVisitParticipantResult>(
      API_ENDPOINTS.delegations.inviteParticipant(visitInstanceId), payload);
    return data;
  },

  /** VisitProcess "Ghi chú chung": save the host's preparation note (null clears it). Host-only. */
  async updatePreparationNote(
    visitInstanceId: number | string,
    note: string | null,
  ): Promise<UpdatePreparationNoteResult> {
    const { data } = await httpClient.put<UpdatePreparationNoteResult>(
      API_ENDPOINTS.delegations.preparationNote(visitInstanceId), { note });
    return data;
  },

  /** VisitProcess "Cảnh báo & Thông báo": load the saved reminder schedule rows. */
  async getReminderSettings(visitInstanceId: number | string): Promise<GetVisitReminderSettingsResult> {
    const { data } = await httpClient.get<GetVisitReminderSettingsResult>(
      API_ENDPOINTS.delegations.reminderSettings(visitInstanceId));
    return data;
  },

  /** VisitProcess "Cảnh báo & Thông báo": upsert the full reminder schedule (enabled=false cancels). */
  async saveReminderSettings(
    visitInstanceId: number | string,
    items: SaveVisitReminderSettingItem[],
  ): Promise<SaveVisitReminderSettingsResult> {
    const { data } = await httpClient.put<SaveVisitReminderSettingsResult>(
      API_ENDPOINTS.delegations.reminderSettings(visitInstanceId), { items });
    return data;
  },

  /** VisitProcess "Cảnh báo & Thông báo": cancel every still-PENDING reminder. */
  async cancelReminderSettings(
    visitInstanceId: number | string,
  ): Promise<{ cancelledCount: number; message: string }> {
    const { data } = await httpClient.patch<{ cancelledCount: number; message: string }>(
      API_ENDPOINTS.delegations.cancelReminderSettings(visitInstanceId), {});
    return data;
  },

  /** Render an email template for the "Xem trước email" modal (read-only — never sends). */
  async previewEmailTemplate(payload: PreviewEmailTemplatePayload): Promise<PreviewEmailTemplateResult> {
    const { data } = await httpClient.post<PreviewEmailTemplateResult>(
      API_ENDPOINTS.emailTemplates.preview, payload);
    return data;
  },

  /** VisitProcess "Chuẩn bị chi tiết": list the campus instance's logistics requests. */
  async getInstanceLogistics(visitInstanceId: number | string): Promise<GetVisitInstanceLogisticsResult> {
    const { data } = await httpClient.get<GetVisitInstanceLogisticsResult>(
      API_ENDPOINTS.delegations.instanceLogistics(visitInstanceId));
    return data;
  },

  /** Host sends a logistics/resource request to a department (optionally with an edited email). */
  async prepareVisitLogistics(payload: PrepareVisitLogisticsPayload): Promise<PrepareVisitLogisticsResult> {
    const { data } = await httpClient.post<PrepareVisitLogisticsResult>(
      API_ENDPOINTS.delegations.prepareVisitLogistics, payload);
    return data;
  },

  /** Host soft-cancels one of their logistics requests (status → CANCELLED). Reason is required and
   * stored in decision_note (validated server-side). */
  async cancelLogisticsItem(
    visitInstanceId: number | string,
    logisticsItemId: number | string,
    reason?: string | null,
  ): Promise<{ success: boolean; logisticsItemId: number; status: string; message: string }> {
    const { data } = await httpClient.patch<{ success: boolean; logisticsItemId: number; status: string; message: string }>(
      API_ENDPOINTS.delegations.cancelLogisticsItem(visitInstanceId, logisticsItemId), { reason: reason ?? null });
    return data;
  },

  /** Host accepts/rejects a Department's change proposal (status CHANGE_PROPOSED). Accept commits the
   * proposed time/content; the PLANNED quantity is never overwritten (final qty is computed on display). */
  async confirmChangeProposal(
    logisticsItemId: number | string,
    accepted: boolean,
    note?: string | null,
  ): Promise<{ logisticsItemId: number; status: string; message: string }> {
    const { data } = await httpClient.post<{ logisticsItemId: number; status: string; message: string }>(
      API_ENDPOINTS.delegations.confirmChangeProposal,
      { logisticsItemId: Number(logisticsItemId), accepted, note: note ?? null },
    );
    return data;
  },

  /** VisitProcess "Đang/Sau tiếp khách": Host signs the BORROWER side of a borrow/return handover. */
  async signLogisticsHandoverBorrower(
    visitInstanceId: number | string,
    logisticsItemId: number | string,
    payload: SignHandoverBorrowerPayload,
  ): Promise<SignHandoverResult> {
    const { data } = await httpClient.post<SignHandoverResult>(
      API_ENDPOINTS.delegations.signLogisticsHandoverBorrower(visitInstanceId, logisticsItemId), payload);
    return data;
  },

  /** "Lưu vào hệ thống" trong TaskHandoverModal — sinh PDF biên bản đã ký đủ 2 bên, upload Drive +
   * lưu document (Hậu cần). Chỉ gọi được khi cả 2 chữ ký (bên giao/bên nhận) đã có. */
  async saveLogisticsHandoverDocument(
    visitInstanceId: number | string,
    logisticsItemId: number | string,
    handoverType: 'BORROW' | 'RETURN',
  ): Promise<{ documentId: number; fileId: number; webViewUrl?: string | null; downloadUrl?: string | null }> {
    const { data } = await httpClient.post(
      API_ENDPOINTS.delegations.saveLogisticsHandoverDocument(visitInstanceId, logisticsItemId, handoverType));
    return data;
  },

  /** "Xem mail đã gửi": sent-email history for one invited participant (subject/body/recipients/status). */
  async getParticipantSentEmails(
    visitInstanceId: number | string,
    participantId: number | string,
  ): Promise<GetSentEmailsResult> {
    const { data } = await httpClient.get<GetSentEmailsResult>(
      API_ENDPOINTS.delegations.participantSentEmails(visitInstanceId, participantId));
    return data;
  },

  /** "Xem mail đã gửi": sent-email history for one logistics request. */
  async getLogisticsSentEmails(
    visitInstanceId: number | string,
    logisticsItemId: number | string,
  ): Promise<GetSentEmailsResult> {
    const { data } = await httpClient.get<GetSentEmailsResult>(
      API_ENDPOINTS.delegations.logisticsSentEmails(visitInstanceId, logisticsItemId));
    return data;
  },

  /** Host withdraws a still-pending (INVITED) participant they invited. */
  async removeVisitParticipant(
    visitInstanceId: number | string,
    participantId: number | string,
  ): Promise<{ participantId: number; status: string; message: string }> {
    const { data } = await httpClient.patch<{ participantId: number; status: string; message: string }>(
      API_ENDPOINTS.delegations.removeParticipant(visitInstanceId, participantId), {});
    return data;
  },

  /** Upsert the instance's agenda (Host only, prep window). Saves setup only — never changes stage. */
  async saveVisitAgenda(
    visitRequestId: number | string,
    visitInstanceId: number | string,
    items: Array<{ agendaId?: number | null; title: string; startTime: string; endTime?: string | null; description?: string | null; location?: string | null; responsibleName?: string | null }>,
  ): Promise<any> {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.delegations.saveAgenda(visitRequestId, visitInstanceId), { items });
    return data;
  },

  /** Valid responsible-person candidates for the agenda editor: the active host + ACCEPTED supporting
   * participants of THIS instance only (never the whole-system user list). */
  async getAgendaResponsibleCandidates(
    visitInstanceId: number | string,
  ): Promise<import('../types/delegations.types').AgendaResponsibleCandidate[]> {
    const { data } = await httpClient.get(
      API_ENDPOINTS.delegations.agendaResponsibleCandidates(visitInstanceId));
    return data;
  },

  /**
   * Operational reception stage transitions (Host only). Each advances the campus instance one
   * stage: complete-before → DURING_VISIT, complete-during → AFTER_VISIT, complete-after → CLOSED.
   * The backend re-validates status (409) and host scope (403); callers should refetch the
   * process permissions afterwards to unlock the next tab.
   */
  async completeBeforeVisit(visitRequestId: number | string, visitInstanceId: number | string): Promise<any> {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.delegations.completeBeforeVisit(visitRequestId, visitInstanceId), {});
    return data;
  },
  async completeDuringVisit(visitRequestId: number | string, visitInstanceId: number | string): Promise<any> {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.delegations.completeDuringVisit(visitRequestId, visitInstanceId), {});
    return data;
  },
  // §10: confirmNoNews = Host xác nhận chuyến này không cần bài tin tức (một điều kiện đóng đoàn).
  async completeAfterVisit(visitRequestId: number | string, visitInstanceId: number | string, confirmNoNews = false): Promise<any> {
    const { data } = await httpClient.post<any>(
      API_ENDPOINTS.delegations.completeAfterVisit(visitRequestId, visitInstanceId), { confirmNoNews });
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

  /**
   * Phase 3: meeting minutes (biên bản) for a campus instance — 1 record per instance with an
   * edit-lock workflow. The backend enforces scope, the one-per-instance rule, lock ownership
   * (token) and optimistic concurrency (rowVersion).
   */
  minutes: {
    /** Read the single minutes record + lock state + action flags (no lock token returned). */
    async get(visitInstanceId: number | string): Promise<VisitMinute> {
      const { data } = await httpClient.get<VisitMinute>(API_ENDPOINTS.meetingMinutes.byInstance(visitInstanceId));
      return data;
    },
    /** Open the editor: create-if-missing + acquire the edit lock (returns a lock token). */
    async createOrLock(visitInstanceId: number | string, title?: string): Promise<VisitMinute> {
      const { data } = await httpClient.post<VisitMinute>(
        API_ENDPOINTS.meetingMinutes.createOrLock(visitInstanceId), { title });
      return data;
    },
    /** Re-acquire the lock on an existing record (re-open for editing). */
    async acquireLock(minutesId: number | string): Promise<VisitMinute> {
      const { data } = await httpClient.post<VisitMinute>(API_ENDPOINTS.meetingMinutes.acquireLock(minutesId), {});
      return data;
    },
    /**
     * Save content + attendance snapshot + action items (requires the caller's lock token + matching
     * rowVersion); releases the lock. `participants`/`actionItems` are reconciled server-side.
     */
    async save(
      minutesId: number | string,
      payload: {
        title: string;
        content: string | null;
        editLockToken: string;
        rowVersion: number;
        participants: SaveMinuteParticipantPayload[];
        actionItems: SaveMinuteActionItemPayload[];
      },
    ): Promise<VisitMinute> {
      const { data } = await httpClient.put<VisitMinute>(API_ENDPOINTS.meetingMinutes.save(minutesId), payload);
      return data;
    },
    /** Release the lock without saving (Hủy chỉnh sửa). */
    async releaseLock(minutesId: number | string, editLockToken: string): Promise<VisitMinute> {
      const { data } = await httpClient.post<VisitMinute>(
        API_ENDPOINTS.meetingMinutes.releaseLock(minutesId), { editLockToken });
      return data;
    },
    /** "Đồng bộ người mới": attendance rows that should exist but are missing (not yet persisted). */
    async newParticipantCandidates(minutesId: number | string): Promise<MinuteParticipant[]> {
      const { data } = await httpClient.get<MinuteParticipant[]>(
        API_ENDPOINTS.meetingMinutes.newParticipantCandidates(minutesId));
      return data;
    },
    /** Search system users to add a participant manually (scoped to editors of the instance). */
    async searchUsers(visitInstanceId: number | string, query: string): Promise<MinuteUserSearchItem[]> {
      const { data } = await httpClient.get<MinuteUserSearchItem[]>(
        API_ENDPOINTS.meetingMinutes.userSearch(visitInstanceId), { params: { query } });
      return data;
    },
  },

  /**
   * Phase 4: news (tin tức) attached to a campus instance — many posts allowed. Create/edit limited
   * to Host / accepted IC-Staff / Student (backend-enforced); Visitor sees only published posts.
   */
  visitNews: {
    async list(visitInstanceId: number | string): Promise<VisitNewsList> {
      const { data } = await httpClient.get<VisitNewsList>(API_ENDPOINTS.visitNews.byInstance(visitInstanceId));
      return data;
    },
    async create(
      visitInstanceId: number | string,
      payload: { title: string; summary: string | null; body: string | null },
    ): Promise<VisitNews> {
      const { data } = await httpClient.post<VisitNews>(API_ENDPOINTS.visitNews.create(visitInstanceId), payload);
      return data;
    },
    async update(
      newsId: number | string,
      payload: { title: string; summary: string | null; body: string | null; rowVersion: number },
    ): Promise<VisitNews> {
      const { data } = await httpClient.put<VisitNews>(API_ENDPOINTS.visitNews.update(newsId), payload);
      return data;
    },
    async submitReview(newsId: number | string): Promise<VisitNews> {
      const { data } = await httpClient.post<VisitNews>(API_ENDPOINTS.visitNews.submitReview(newsId), {});
      return data;
    },
  },

  visitInvitations: {
    async getMyInvitations(params?: Record<string, unknown>): Promise<any> {
      const { data } = await httpClient.get<any>(API_ENDPOINTS.visitInvitations.my, { params });
      return data;
    },
    async getInvitationDetail(participantId: string | number): Promise<any> {
      const { data } = await httpClient.get<any>(API_ENDPOINTS.visitInvitations.detail(participantId));
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
    async assignDepartmentStaff(
      participantId: string | number,
      departmentStaffUserId: number,
      note: string,
      emailOverride?: {
        useEditedContent: boolean;
        subject: string;
        bodyHtml: string;
        attachments?: { fileId: number; attachmentType?: 'ATTACHMENT' | 'INLINE_IMAGE'; contentId?: string | null; displayName?: string | null; displayOrder?: number }[];
      },
    ): Promise<any> {
      const { data } = await httpClient.post<any>(API_ENDPOINTS.visitInvitations.assignDepartmentStaff(participantId), { departmentStaffUserId, note, emailOverride });
      return data;
    },
  },
};
