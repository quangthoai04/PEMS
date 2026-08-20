import type { NotificationItem } from '../types/notification.types';
import type { AuthUser } from '../../authentication/types/authentication.types';
import { parseNotificationSemantic } from './notificationSemantic';

/**
 * WHY the user is clicking — what a notification's own meaning says the destination screen should
 * let them do at most. Distinct from `NotificationItem.actionType` (a legacy/coarse backend field);
 * this is the frontend's classification, built from `eventKey` first.
 *
 * `VISIT_REVIEW` / `VISIT_HISTORY` / `VISIT_DETAIL` / `VISIT_READONLY_DETAIL` / `HOST_PROCESS` /
 * `VISIT_INVITATION` / `CONTRIBUTION` all route through the Visit management one-shot command
 * (`openVisitRequestId`/`openVisitInstanceId`/`notificationIntent`) — the destination is resolved
 * against CURRENT backend state there, never guessed here. The remaining values resolve directly to
 * a route/modal from this file.
 *
 * See docs/CanhIter3FixBug/GopYCQuyen/PEMS_Notification_Second_Click_Semantic_Routing_Full_Fix_Plan.md §10.
 */
export type NotificationNavigationIntent =
  | 'VISIT_REVIEW'
  | 'VISIT_HISTORY'
  | 'VISIT_DETAIL'
  | 'VISIT_READONLY_DETAIL'
  | 'HOST_PROCESS'
  | 'VISIT_INVITATION'
  | 'CONTRIBUTION'
  | 'FEEDBACK_MODAL'
  | 'LOGISTICS_DETAIL'
  | 'HANDOVER_DETAIL'
  | 'AGENDA_DETAIL'
  | 'MINUTES_DETAIL'
  | 'ACTION_ITEM_DETAIL'
  | 'NEWS_DETAIL'
  | 'PARTNER_DETAIL'
  | 'ACCOUNT_DETAIL'
  | 'NOTIFICATION_DETAIL';

/** The subset of intents that must be resolved against CURRENT Visit list state, never here. */
export const VISIT_COMMAND_INTENTS = new Set<NotificationNavigationIntent>([
  'VISIT_REVIEW', 'VISIT_HISTORY', 'VISIT_DETAIL', 'VISIT_READONLY_DETAIL',
  'HOST_PROCESS', 'VISIT_INVITATION', 'CONTRIBUTION',
]);

/**
 * eventKey -> intent. Every current `NotificationEventKeys` constant is classified here (plan §55
 * pins every one in a unit test) — WHAT HAPPENED decides the maximum interaction the click may open;
 * current state/permission (resolved downstream) may only ever DOWNGRADE this, never upgrade it into
 * a stronger action (plan §7/§45). A handful of these (marked below) still need a real producer/UI
 * audit before their exact destination screen is built — they are deliberately mapped to the
 * conservative non-mutating `VISIT_DETAIL` in the meantime rather than guessed.
 */
const EVENT_INTENT: Record<string, NotificationNavigationIntent> = {
  // Guest/Visitor-facing.
  CAMPUS_APPROVED: 'VISIT_DETAIL',
  CAMPUS_REJECTED: 'VISIT_DETAIL',
  FEEDBACK_INVITE_VISITOR: 'FEEDBACK_MODAL',
  VISIT_CLOSED: 'VISIT_READONLY_DETAIL',
  VISIT_CANCELLED_BY_HOST: 'VISIT_READONLY_DETAIL',
  OPCONTACT_TRANSFER_FROM: 'VISIT_DETAIL',
  OPCONTACT_TRANSFER_TO: 'VISIT_DETAIL',
  AMENDMENT_APPROVED: 'VISIT_HISTORY',
  AMENDMENT_REJECTED: 'VISIT_HISTORY',
  HOST_CHANGED: 'VISIT_DETAIL',
  ACCOUNT_CREATED: 'ACCOUNT_DETAIL',
  ACCOUNT_LOCKED: 'ACCOUNT_DETAIL',
  ACCOUNT_UNLOCKED: 'ACCOUNT_DETAIL',

  // Visit request lifecycle.
  VISIT_REQUEST_WAITING_APPROVAL: 'VISIT_REVIEW',
  VISIT_REQUEST_UPDATED_PENDING: 'VISIT_HISTORY',
  VISIT_REQUEST_RESUBMITTED: 'VISIT_HISTORY',
  VISIT_PRIVACY_CONSENT_WITHDRAWN: 'VISIT_READONLY_DETAIL',
  HOST_ASSIGNED: 'HOST_PROCESS',
  // Producer (ProposedHostNotifier.AnnounceOutcomeAsync, pending branch): recipient is the PROPOSED
  // host, not yet confirmed by the guest-side contact. Own message text: "đang chờ đầu mối đoàn
  // khách xác nhận. Chưa cần chuẩn bị cho tới khi được phân công chính thức" — explicitly nothing to
  // do yet, `IsActionRequired: false` in the producer itself. Setup/Host Process only becomes
  // reachable once the row's own CURRENT primaryEntryContext says so (after real activation).
  HOST_PROPOSAL_PENDING: 'VISIT_DETAIL',
  // Producer (same file, failed-revalidation branch): recipient is `instance.CoordinatorUserId` —
  // the IDENTICAL field VISIT_REQUEST_WAITING_APPROVAL uses. Own message text: "đã đủ điều kiện xử
  // lý, nhưng host dự kiến không còn hợp lệ... Vui lòng duyệt và chọn người phụ trách tiếp đón" — the
  // exact same approve+assign-host action as a campus review, just re-triggered by a revalidation
  // failure instead of the original submission.
  HOST_REASSIGNMENT_REQUIRED: 'VISIT_REVIEW',
  HOST_TRANSFER_INCOMING: 'HOST_PROCESS',
  HOST_TRANSFER_OUTGOING: 'VISIT_DETAIL',
  CAMPUS_APPROVED_HO_VISIBILITY: 'VISIT_READONLY_DETAIL',
  CAMPUS_REJECTED_HO_VISIBILITY: 'VISIT_READONLY_DETAIL',
  VISIT_CANCELLED_HO_VISIBILITY: 'VISIT_READONLY_DETAIL',
  HOST_CHANGED_HO_VISIBILITY: 'VISIT_READONLY_DETAIL',
  VISIT_CANCELLED_STAFF_LEADER: 'VISIT_READONLY_DETAIL',
  // Producer (HoUnprocessedCampusAlertHostedService): recipient is every ACTIVE HO user only — HO
  // has no approve/assign-host capability anywhere in this system (read-only visibility role, plan
  // §21). `IsActionRequired: true` on the row is an urgency badge, not evidence of a mutation HO can
  // perform. In practice HO/Visitor never reach this classifier at all (intercepted earlier by
  // resolveNotificationDestination's own isViewOnlyRole branch below); classified here for
  // completeness/testability, not because it changes HO's current click behavior.
  HO_CAMPUS_UNPROCESSED_ALERT: 'VISIT_READONLY_DETAIL',
  // Producer (VisitAmendmentHandlers): recipient is the campus's CURRENT Host, told a content-change
  // proposal is waiting for their decision. Non-escalating VISIT_DETAIL rather than VISIT_REVIEW —
  // unlike HOST_REASSIGNMENT_REQUIRED (evidenced as the SAME campus-review/approve-assign-host
  // control), this decision lives in the request detail's own amendment panel, a different control
  // with no evidence it maps to CAMPUS_REVIEW+APPROVE_AND_ASSIGN_HOST.
  AMENDMENT_PROPOSED: 'VISIT_DETAIL',
  // Producer (V2CreateNotifier, multi-campus branch): HO-only, IsActionRequired=false, purely
  // informational — a new multi-campus request now exists somewhere for a campus to process.
  MULTI_CAMPUS_REQUEST_SUBMITTED_HO_VISIBILITY: 'VISIT_READONLY_DETAIL',
  // Producer (CampusApprovalExecutor / RejectCampusInstanceCommandHandler, shared aggregate-status
  // branch): HO-only visibility on a multi-campus request's aggregate state — same non-escalating
  // treatment as every other *_HO_VISIBILITY event.
  VISIT_REQUEST_PARTIALLY_APPROVED_HO_VISIBILITY: 'VISIT_READONLY_DETAIL',
  VISIT_REQUEST_FULLY_PROCESSED_HO_VISIBILITY: 'VISIT_READONLY_DETAIL',
  // Producer (CancelVisitRequestCommandHandler, self-service pre-processing cancel branch): Staff
  // Leader recipient, but there is nothing left to review — the request was cancelled before any
  // campus started processing it.
  VISIT_REQUEST_CANCELLED_BEFORE_APPROVAL: 'VISIT_READONLY_DETAIL',

  // Participation.
  PARTICIPATION_INVITED: 'VISIT_INVITATION',
  PARTICIPATION_ACCEPTED: 'VISIT_DETAIL',
  PARTICIPATION_DECLINED: 'VISIT_DETAIL',

  // Agenda / Minutes / Action items.
  AGENDA_UPDATED: 'AGENDA_DETAIL',
  MINUTES_UPDATED: 'MINUTES_DETAIL',
  ACTION_ITEM_ASSIGNED: 'ACTION_ITEM_DETAIL',
  ACTION_ITEM_DUE: 'ACTION_ITEM_DETAIL',

  // Logistics.
  LOGISTICS_REQUEST_CREATED: 'LOGISTICS_DETAIL',
  LOGISTICS_ASSIGNED: 'LOGISTICS_DETAIL',
  LOGISTICS_ASSIGNEE_ACCEPTED: 'LOGISTICS_DETAIL',
  LOGISTICS_ASSIGNEE_DECLINED: 'LOGISTICS_DETAIL',
  LOGISTICS_PROPOSAL_CREATED: 'LOGISTICS_DETAIL',
  LOGISTICS_PROPOSAL_ACCEPTED: 'LOGISTICS_DETAIL',
  LOGISTICS_PROPOSAL_REJECTED: 'LOGISTICS_DETAIL',
  LOGISTICS_HANDOVER_SIGNED: 'HANDOVER_DETAIL',
  LOGISTICS_EXPENSE_REMINDER: 'LOGISTICS_DETAIL',

  // News / Partner.
  NEWS_PENDING_APPROVAL: 'NEWS_DETAIL',
  NEWS_APPROVED: 'NEWS_DETAIL',
  NEWS_REJECTED: 'NEWS_DETAIL',
  PARTNER_PENDING_APPROVAL: 'PARTNER_DETAIL',
  PARTNER_APPROVED: 'PARTNER_DETAIL',
  PARTNER_REJECTED: 'PARTNER_DETAIL',

  // Feedback / reminders.
  HOST_FEEDBACK_INVITE: 'FEEDBACK_MODAL',
  // Producer (SubmitVisitFeedbackCommandHandler, both branches) sets ActionType
  // OpenVisitorFeedbackModal/OpenHostFeedbackModal explicitly, so ACTION_TYPE_INTENT below always
  // classifies these before this eventKey is consulted (audited, stabilization round 2 §3/§23: the
  // handler's own comment is explicit — "Bấm vào phải mở modal xem NỘI DUNG đánh giá... không điều
  // hướng sang trang setup đoàn"). This mapping is the conservative-but-CORRECT fallback for a row
  // that somehow lost its actionType (never a live producer today) — FEEDBACK_MODAL, not the
  // strictly-weaker VISIT_DETAIL that used to be here, which would have opened the wrong screen
  // (visit detail instead of the feedback-content modal) for that hypothetical row.
  VISITOR_FEEDBACK_RECEIVED: 'FEEDBACK_MODAL',
  HOST_FEEDBACK_RECEIVED: 'FEEDBACK_MODAL',
  // Producer (VisitReminderDispatchService.SendInAppAsync) now sets an explicit per-recipient
  // actionType (OPEN_HOST_PROCESS for the current Host, OPEN_CONTRIBUTION for every other
  // recipient — plan RC-05/RM-01..03), which ACTION_TYPE_INTENT below classifies BEFORE this
  // eventKey is ever consulted. This mapping is now only the conservative fallback for reminder
  // rows created before that fix (no actionType stored) — never escalating on its own.
  VISIT_REMINDER: 'VISIT_DETAIL',

  // Accounts.
  ACCOUNT_STATUS_ACTIVATED: 'ACCOUNT_DETAIL',
  ACCOUNT_STATUS_DEACTIVATED: 'ACCOUNT_DETAIL',
};

/** Explicit modern `actionType` values (plan §33) that outrank eventKey when present. */
const ACTION_TYPE_INTENT: Partial<Record<string, NotificationNavigationIntent>> = {
  OPEN_CAMPUS_REVIEW: 'VISIT_REVIEW',
  OPEN_VISIT_HISTORY: 'VISIT_HISTORY',
  OPEN_VISIT_READONLY_DETAIL: 'VISIT_READONLY_DETAIL',
  OPEN_HOST_PROCESS: 'HOST_PROCESS',
  OPEN_CONTRIBUTION: 'CONTRIBUTION',
  OPEN_VISIT_INVITATION: 'VISIT_INVITATION',
  OPEN_HOST_FEEDBACK_MODAL: 'FEEDBACK_MODAL',
  OPEN_VISITOR_FEEDBACK_MODAL: 'FEEDBACK_MODAL',
  OPEN_LOGISTICS_DETAIL: 'LOGISTICS_DETAIL',
  OPEN_HANDOVER_DETAIL: 'HANDOVER_DETAIL',
  OPEN_NEWS_DETAIL: 'NEWS_DETAIL',
  OPEN_PARTNER_DETAIL: 'PARTNER_DETAIL',
  OPEN_ACCOUNT_DETAIL: 'ACCOUNT_DETAIL',
  // OPEN_VISIT_DETAIL is deliberately absent: it is the coarse legacy actionType dozens of Visit
  // producers still emit for genuinely different events (plan §33/§34) — mapping it to any single
  // intent here would let it silently outrank the real eventKey classification below. Precedence
  // (plan §11) requires eventKey to decide for it, not the other way around.
  // OPEN_NOTIFICATION_PAGE has zero live producers (verified repo-wide) — left unclassified rather
  // than guessed.
};

/**
 * Precedence (plan §11): explicit modern actionType > eventKey semantic > legacy actionType >
 * unclassified. Never title/message text, never targetUrl-first.
 */
export function classifyNotificationIntent(
  item: Pick<NotificationItem, 'metadataJson' | 'actionType'>,
): NotificationNavigationIntent | null {
  if (item.actionType && ACTION_TYPE_INTENT[item.actionType]) {
    return ACTION_TYPE_INTENT[item.actionType]!;
  }
  const event = parseNotificationSemantic(item.metadataJson);
  if (event && EVENT_INTENT[event.eventKey]) {
    return EVENT_INTENT[event.eventKey];
  }
  return null;
}

/**
 * Where a click on this notification should go. Moved out of `NotificationBellButton` (plan §9/§40)
 * — it is business routing, not Bell UI, and 5 different surfaces (Bell, NotificationsPage,
 * SharedDashboardView, StaffCalendarTab, StaffDashboardCalendar) must resolve the SAME destination
 * for the same item + current user (plan §39).
 */
export function resolveNotificationDestination(item: NotificationItem, user: AuthUser | null): string | undefined {
  // Tin tức / đối tác: mọi thông báo (duyệt, từ chối, chờ duyệt) đều đưa về trang quản lý
  // lọc đúng 1 bản ghi — có nút "Xem tất cả" để thoát. Rewrite theo relatedType/relatedId
  // để xử lý luôn notification cũ trỏ trang chi tiết hoặc không có targetUrl.
  const relatedType = item.relatedType?.toUpperCase();
  if (relatedType === 'NEWS' && item.relatedId) {
    return `/dashboard/news?newsId=${item.relatedId}`;
  }
  if (relatedType === 'PARTNER' && item.relatedId) {
    return `/dashboard/partners?partnerId=${item.relatedId}`;
  }

  let link = item.targetUrl || undefined;

  if (link && user) {
    const isDeptStaff = user.roleCode?.toUpperCase() === 'DEPARTMENT' && user.subRole?.toUpperCase() !== 'LEADER';
    const isDeptLeader = user.roleCode?.toUpperCase() === 'DEPARTMENT' && user.subRole?.toUpperCase() === 'LEADER';
    if (isDeptStaff) {
      if (link.includes('/tasks/')) {
        const parts = link.split('/tasks/');
        return `/dashboard?taskId=${parts[1]}&itemType=REQUEST`;
      }
      if (link.includes('/invitations/')) {
        const parts = link.split('/invitations/');
        return `/dashboard?taskId=${parts[1]}&itemType=INVITATION`;
      }
    }

    // Dept Leader: đơn/thư mời mở thẳng modal chi tiết (giống bấm 1 đơn trong Bảng lịch)
    // thay vì trang "Chi tiết nhiệm vụ điều phối" đứng riêng (đã bỏ) — dùng luôn id tách
    // từ targetUrl, không phụ thuộc visitRequestId (một số notification hậu cần không có field này).
    if (isDeptLeader) {
      if (link.includes('/tasks/')) {
        const parts = link.split('/tasks/');
        return `/dashboard/visit?taskId=${parts[1]}&itemType=REQUEST`;
      }
      if (link.includes('/invitations/')) {
        const parts = link.split('/invitations/');
        return `/dashboard/visit?taskId=${parts[1]}&itemType=INVITATION`;
      }
    }

    const isProcessDetailLink = /\/dashboard\/visit\/(process|reception-detail|ho-detail)\//.test(link);
    const isViewOnlyRole = ['VISITOR', 'HO'].includes(user.roleCode?.toUpperCase() || '');

    // Feedback deep link: a SEPARATE one-shot param (feedbackVisitInstanceId) that opens a modal,
    // not an entry-context screen — kept as its own case rather than folded into the general
    // rewrite below, which only ever knows how to open entry contexts/intents. Matched by URL shape
    // (including an already-resolved link, left untouched/idempotent), or by category (a feedback
    // notification's stored link is not always `/feedback/`-shaped).
    if (isViewOnlyRole && item.visitRequestId && item.visitInstanceId
      && (link.includes('/feedback/') || link.includes('feedbackVisitInstanceId=') || item.category === 'Feedback')) {
      return `/dashboard/visit?visitRequestId=${item.visitRequestId}&feedbackVisitInstanceId=${item.visitInstanceId}`;
    }

    // Semantic-first (plan RC-01/RC-02/RC-03/RC-04/RC-06): ANY legacy Visit-family link that names a
    // request — the plain list ("/dashboard/visit", "/dashboard/visit?visitRequestId=N", or any
    // other query-string shape a producer/hand-typed link might carry, e.g. "&tab=all") OR a direct
    // deep link into /process|/reception-detail|/ho-detail/{instanceId} — is rewritten to the
    // ONE-SHOT COMMAND "openVisitRequestId"(+"openVisitInstanceId" if the notification already named
    // the exact campus, +"notificationIntent" from eventKey/actionType — plan §12), for EVERY role
    // and REGARDLESS of the exact URL shape a producer happened to store. Previously this only fired
    // for an exact bare/`visitRequestId=N` list link, or for a hand-picked subset of roles
    // (Visitor/HO/Student/Department) on a direct process/reception-detail/ho-detail link — any
    // other shape (a stray query param) or any other role (Staff, Staff Leader — the very people who
    // CAN be Host) fell straight through to the raw stored URL, letting a stale direct "/process/
    // {id}" walk a former Host straight into Host Process with no current-state check at all.
    // VisitRequestManagement resolves CURRENT state before opening anything — whether the click may
    // reach Host Process, Campus Review, an approve control, an exact invitation target (participant
    // id, re-resolved fresh, never parsed off this URL) etc. is decided there, never by which literal
    // URL a producer stored at creation time. The command is consumed and stripped from the URL on
    // the same run (see VisitRequestManagement) so it never replays.
    //
    // Gated on `classifyNotificationIntent`, not just URL shape: a Logistics/Handover/Agenda/Minutes/
    // Action-Item/News/Partner/Account/Feedback notification can legitimately reuse this exact same
    // "/dashboard/visit/process/{id}" shape (its own detail lives on a tab of that same screen) —
    // those already have an exact, correct destination of their own (plan §23/§31) and must be left
    // untouched, never folded into the Visit list's one-shot command. Only an intent this resolver
    // itself owns (one of `VISIT_COMMAND_INTENTS`) — or no classifiable intent at all, the true
    // legacy-notification case — is eligible.
    const intent = classifyNotificationIntent(item);
    const isVisitCommandCandidate = intent == null || VISIT_COMMAND_INTENTS.has(intent);
    const isPlainVisitListLink = /^\/dashboard\/visit(\?.*)?$/.test(link);
    if (isVisitCommandCandidate && (isPlainVisitListLink || isProcessDetailLink) && item.visitRequestId) {
      const oneShot = new URLSearchParams();
      oneShot.set('openVisitRequestId', String(item.visitRequestId));
      if (item.visitInstanceId) oneShot.set('openVisitInstanceId', String(item.visitInstanceId));
      if (intent) {
        oneShot.set('notificationIntent', intent);
      }
      return `/dashboard/visit?${oneShot.toString()}`;
    }

    // Fallbacks below only matter when `visitRequestId` itself is missing (the general rewrite
    // above already requires it) — kept role-scoped exactly as before for that narrow edge case.
    const isNeverHostRole = ['STUDENT', 'DEPARTMENT'].includes(user.roleCode?.toUpperCase() || '');
    if (isNeverHostRole && isProcessDetailLink && item.visitInstanceId) {
      return `/dashboard/visit/contribution/${item.visitInstanceId}`;
    }
  }

  return link;
}
