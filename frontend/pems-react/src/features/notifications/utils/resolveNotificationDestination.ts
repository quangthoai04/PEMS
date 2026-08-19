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
  VISITOR_FEEDBACK_RECEIVED: 'VISIT_DETAIL',
  HOST_FEEDBACK_RECEIVED: 'VISIT_DETAIL',
  VISIT_REMINDER: 'VISIT_DETAIL', // TODO: relation-aware routing (plan §29) — kept non-escalating.

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

    // Notification trỏ thẳng vào trang danh sách kèm định danh request — dạng bare
    // "/dashboard/visit" (rất cũ, trước khi ActionUrl bắt đầu kèm id) HOẶC dạng hiện tại
    // "/dashboard/visit?visitRequestId=N" (mọi handler backend đang tạo). Cả hai chỉ LỌC
    // danh sách xuống 1 dòng — không mở đúng entry context/campus review, và filter đó bị
    // "quên" lại trên URL nên đổi tab/filter/trang sau khi đóng có thể làm nó tái xuất hiện.
    // Rewrite sang ONE-SHOT COMMAND "openVisitRequestId"(+"openVisitInstanceId" nếu notification
    // đã biết đúng campus, +"notificationIntent" nếu eventKey/actionType phân loại được ý nghĩa
    // — plan §12) để trang tự resolve CURRENT state rồi mở đúng entry context theo ĐÚNG ý nghĩa
    // gốc của notification, và tự xoá command khỏi URL ngay sau khi dùng (xem
    // VisitRequestManagement — không dùng lại tên `visitRequestId` cũ vì nó đã có nghĩa
    // "persistent filter" khác, xem RC-03 trong plan).
    const isPlainVisitListLink = link === '/dashboard/visit'
      || /^\/dashboard\/visit\?visitRequestId=\d+$/.test(link);
    if (isPlainVisitListLink && item.visitRequestId) {
      const oneShot = new URLSearchParams();
      oneShot.set('openVisitRequestId', String(item.visitRequestId));
      if (item.visitInstanceId) oneShot.set('openVisitInstanceId', String(item.visitInstanceId));
      const intent = classifyNotificationIntent(item);
      if (intent && VISIT_COMMAND_INTENTS.has(intent)) {
        oneShot.set('notificationIntent', intent);
      }
      return `/dashboard/visit?${oneShot.toString()}`;
    }

    const isProcessDetailLink = /\/dashboard\/visit\/(process|reception-detail|ho-detail)\//.test(link);

    // Visitor & HO không bao giờ là Host theo thiết kế hệ thống — trang Host Operation
    // không dành cho họ. Notification cũ có thể còn trỏ vào /process|/reception-detail|
    // /ho-detail từ trước khi route này được đổi — luôn rewrite về trang Quản lý tiếp khách
    // lọc đúng đơn, tính theo dữ liệu hiện tại của notification thay vì tin URL đã lưu sẵn.
    const isViewOnlyRole = ['VISITOR', 'HO'].includes(user.roleCode?.toUpperCase() || '');
    if (isViewOnlyRole && (isProcessDetailLink || link.includes('/feedback/')) && item.visitRequestId) {
      if (item.visitInstanceId && (link.includes('/feedback/') || item.category === 'Feedback')) {
        return `/dashboard/visit?visitRequestId=${item.visitRequestId}&feedbackVisitInstanceId=${item.visitInstanceId}`;
      }
      return `/dashboard/visit?visitRequestId=${item.visitRequestId}`;
    }

    // "Bạn được mời tham gia đoàn" (participant invitation) — người nhận có thể là
    // Student/IC Staff KHÔNG phải Host của đoàn này (IC Staff đôi khi là Host đoàn khác,
    // nên không thể chặn theo role tĩnh). Trang Host Operation chỉ dành cho đúng Host của
    // đoàn, nên loại notification này luôn rewrite về trang danh sách "Quản lý tiếp khách"
    // lọc đúng đơn, bất kể role. Bắt buộc kèm tab=attending: mặc định trang này rơi vào tab
    // "Tất cả các loại đơn" (Staff/Staff Leader) — nơi dòng attending bị đánh dấu read-only
    // (backend BuildAllowedActions không có participantId/trạng thái mời để tính nút Chấp
    // nhận), nên nếu không ép tab, nút "Nhận lời" sẽ biến mất cho tới khi user tự đổi tab.
    if (item.actionType === 'OPEN_VISIT_INVITATION' && isProcessDetailLink && item.visitRequestId) {
      return `/dashboard/visit?visitRequestId=${item.visitRequestId}&tab=attending`;
    }

    // Student/Department không bao giờ là Host (theo thiết kế vai trò) — trang "Quy trình tiếp
    // khách" (process) chỉ dành cho Host. Notification cũ (tạo trước khi backend đổi ActionUrl
    // sang trang "Đóng góp kết quả") vẫn có thể còn trỏ vào /process/ — luôn rewrite về đúng trang
    // của 2 role này, tính theo dữ liệu hiện tại thay vì tin URL đã lưu sẵn.
    const isNeverHostRole = ['STUDENT', 'DEPARTMENT'].includes(user.roleCode?.toUpperCase() || '');
    if (isNeverHostRole && isProcessDetailLink && item.visitInstanceId) {
      return `/dashboard/visit/contribution/${item.visitInstanceId}`;
    }
  }

  return link;
}
