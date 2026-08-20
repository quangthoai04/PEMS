/**
 * `resolveNotificationDestination()` decides where a notification click navigates — moved out of
 * `NotificationBellButton` (plan §9/§40: it is business routing, not Bell UI, and five surfaces
 * share it — see plan §39). Formerly `getNotificationLink` in
 * `components/__tests__/getNotificationLink.test.ts`; this file replaces it one-for-one plus the new
 * semantic-intent coverage (plan §55: every `NotificationEventKeys` constant pinned).
 *
 * The one-shot-command tests below also cover the ORIGINAL bug this rewrite exists for: before it,
 * `/dashboard/visit?visitRequestId=N` (which every current Visit notification producer emits) passed
 * straight through — the visit list page only used that parameter to FILTER the list down to one
 * row, never to open it. The fix rewrites that pattern into the one-shot command
 * `openVisitRequestId`(+`openVisitInstanceId`+`notificationIntent`) the list page's own deep-link
 * effect consumes — see VisitRequestManagementNotificationDeepLink.test.tsx for what happens after
 * navigation.
 */
import { describe, expect, it } from 'vitest';
import { resolveNotificationDestination, classifyNotificationIntent } from '../resolveNotificationDestination';
import type { NotificationItem } from '../../types/notification.types';
import type { AuthUser } from '../../../authentication/types/authentication.types';

const baseItem: NotificationItem = {
  notificationId: 1,
  title: 'Có yêu cầu tiếp khách mới',
  message: null,
  notificationType: 'VISIT_REQUEST_SUBMITTED',
  category: 'VISIT',
  priority: 'NORMAL',
  isActionRequired: true,
  relatedType: 'VisitRequest',
  relatedId: 123,
  visitRequestId: 123,
  visitInstanceId: null,
  campusId: null,
  actionType: 'OPEN_VISIT_DETAIL',
  isRead: false,
  readAt: null,
  createdAt: '2026-08-19T09:00:00+07:00',
  timeAgoText: '1 phút trước',
  metadataJson: null,
  targetUrl: null,
  canOpen: true,
  disabledReason: null,
};

const item = (over: Partial<NotificationItem>): NotificationItem => ({ ...baseItem, ...over });
const withEvent = (eventKey: string, over: Partial<NotificationItem> = {}, params: Record<string, unknown> = {}) =>
  item({ metadataJson: JSON.stringify({ eventKey, params }), ...over });

const user = (over: Partial<AuthUser>): AuthUser => ({
  userId: '1',
  fullName: 'Test User',
  email: 'test@fpt.edu.vn',
  roleCode: 'STAFF',
  subRole: null,
  mustChangePassword: false,
  mustSetPassword: false,
  effectiveRole: 'STAFF',
  status: 'ACTIVE',
  ...over,
});

const staffLeader = user({ userId: '77', roleCode: 'STAFF', subRole: 'LEADER' });
const visitor = user({ userId: '10', roleCode: 'VISITOR', subRole: null });
const ho = user({ userId: '5', roleCode: 'HO', subRole: null });
const student = user({ userId: '20', roleCode: 'STUDENT', subRole: null });
const deptStaff = user({ userId: '30', roleCode: 'DEPARTMENT', subRole: 'STAFF' });
const deptLeader = user({ userId: '31', roleCode: 'DEPARTMENT', subRole: 'LEADER' });

describe('the currently-broken pattern: plain visit list + visitRequestId', () => {
  it('rewrites the CURRENT backend format to a one-shot open command', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
  });

  it('includes the campus instance when the notification named one (multi-campus exact target)', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123, visitInstanceId: 456 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456');
  });

  it('rewrites the very old bare "/dashboard/visit" format the same way', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit', visitRequestId: 123 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
  });

  it('applies for every role, not only Staff Leader', () => {
    for (const u of [staffLeader, visitor, ho]) {
      const link = resolveNotificationDestination(
        item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123 }),
        u,
      );
      expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
    }
  });

  it('does nothing without a user (falls through unchanged, same as before)', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123 }),
      null,
    );
    expect(link).toBe('/dashboard/visit?visitRequestId=123');
  });

  it('rewrites a link carrying an extra query param too (plan RC-02 — shape-agnostic, not a regex pin)', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123&tab=all', visitRequestId: 123 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
  });
});

describe('the already-fixed feedback one-shot link is untouched', () => {
  it('never rewrites a link that already carries feedbackVisitInstanceId', () => {
    const link = resolveNotificationDestination(
      item({
        targetUrl: '/dashboard/visit?visitRequestId=123&feedbackVisitInstanceId=456',
        visitRequestId: 123,
        visitInstanceId: 456,
      }),
      visitor,
    );
    expect(link).toBe('/dashboard/visit?visitRequestId=123&feedbackVisitInstanceId=456');
  });
});

describe('existing role-specific rewrites keep working', () => {
  it('Dept regular staff task links still go to the dashboard, not the visit list', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit/process/900/tasks/77', actionType: 'OPEN_VISIT_DETAIL' }),
      deptStaff,
    );
    expect(link).toBe('/dashboard?taskId=77&itemType=REQUEST');
  });

  it('Dept leader task links still go to the visit list with taskId, not openVisitRequestId', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit/process/900/tasks/77' }),
      deptLeader,
    );
    expect(link).toBe('/dashboard/visit?taskId=77&itemType=REQUEST');
  });

  it('Visitor direct process-detail links now go through the one-shot command too (plan RC-03/04)', () => {
    // Previously rewrote straight to a filtered list (`?visitRequestId=123`), bypassing current-state
    // resolution entirely. A stale direct link must be re-resolved the same as every other role's.
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit/process/900', visitRequestId: 123 }),
      visitor,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
  });

  it('Student direct process-detail links also go through the one-shot command (plan RC-03/04)', () => {
    // The `/dashboard/visit/contribution/{id}` fallback below only matters when `visitRequestId`
    // itself is absent — every producer sets it, so this is the common path in practice.
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit/process/900', visitInstanceId: 456 }),
      student,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456');
  });

  it('Student contribution fallback still fires when visitRequestId itself is missing', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit/process/900', visitRequestId: null, visitInstanceId: 456 }),
      student,
    );
    expect(link).toBe('/dashboard/visit/contribution/456');
  });

  it('NEWS notifications still route to the news manager regardless of targetUrl', () => {
    const link = resolveNotificationDestination(
      item({ relatedType: 'NEWS', relatedId: 55, targetUrl: '/dashboard/visit?visitRequestId=123' }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/news?newsId=55');
  });
});

// ── Semantic intent (plan §5/§7/§10/§14 — the actual UPDATED_PENDING bug) ──────────────────────────

describe('classifyNotificationIntent pins every current eventKey to a navigation intent', () => {
  const cases: Array<[string, string]> = [
    ['CAMPUS_APPROVED', 'VISIT_DETAIL'],
    ['CAMPUS_REJECTED', 'VISIT_DETAIL'],
    ['FEEDBACK_INVITE_VISITOR', 'FEEDBACK_MODAL'],
    ['VISIT_CLOSED', 'VISIT_READONLY_DETAIL'],
    ['VISIT_CANCELLED_BY_HOST', 'VISIT_READONLY_DETAIL'],
    ['OPCONTACT_TRANSFER_FROM', 'VISIT_DETAIL'],
    ['OPCONTACT_TRANSFER_TO', 'VISIT_DETAIL'],
    ['AMENDMENT_APPROVED', 'VISIT_HISTORY'],
    ['AMENDMENT_REJECTED', 'VISIT_HISTORY'],
    ['HOST_CHANGED', 'VISIT_DETAIL'],
    ['ACCOUNT_CREATED', 'ACCOUNT_DETAIL'],
    ['ACCOUNT_LOCKED', 'ACCOUNT_DETAIL'],
    ['ACCOUNT_UNLOCKED', 'ACCOUNT_DETAIL'],
    ['VISIT_REQUEST_WAITING_APPROVAL', 'VISIT_REVIEW'],
    ['VISIT_REQUEST_UPDATED_PENDING', 'VISIT_HISTORY'],
    ['VISIT_REQUEST_RESUBMITTED', 'VISIT_HISTORY'],
    ['VISIT_PRIVACY_CONSENT_WITHDRAWN', 'VISIT_READONLY_DETAIL'],
    ['HOST_ASSIGNED', 'HOST_PROCESS'],
    ['HOST_PROPOSAL_PENDING', 'VISIT_DETAIL'],
    ['HOST_REASSIGNMENT_REQUIRED', 'VISIT_REVIEW'],
    ['HOST_TRANSFER_INCOMING', 'HOST_PROCESS'],
    ['HOST_TRANSFER_OUTGOING', 'VISIT_DETAIL'],
    ['CAMPUS_APPROVED_HO_VISIBILITY', 'VISIT_READONLY_DETAIL'],
    ['CAMPUS_REJECTED_HO_VISIBILITY', 'VISIT_READONLY_DETAIL'],
    ['VISIT_CANCELLED_HO_VISIBILITY', 'VISIT_READONLY_DETAIL'],
    ['HOST_CHANGED_HO_VISIBILITY', 'VISIT_READONLY_DETAIL'],
    ['VISIT_CANCELLED_STAFF_LEADER', 'VISIT_READONLY_DETAIL'],
    ['HO_CAMPUS_UNPROCESSED_ALERT', 'VISIT_READONLY_DETAIL'],
    ['AMENDMENT_PROPOSED', 'VISIT_DETAIL'],
    ['MULTI_CAMPUS_REQUEST_SUBMITTED_HO_VISIBILITY', 'VISIT_READONLY_DETAIL'],
    ['VISIT_REQUEST_PARTIALLY_APPROVED_HO_VISIBILITY', 'VISIT_READONLY_DETAIL'],
    ['VISIT_REQUEST_FULLY_PROCESSED_HO_VISIBILITY', 'VISIT_READONLY_DETAIL'],
    ['VISIT_REQUEST_CANCELLED_BEFORE_APPROVAL', 'VISIT_READONLY_DETAIL'],
    ['PARTICIPATION_INVITED', 'VISIT_INVITATION'],
    ['PARTICIPATION_ACCEPTED', 'VISIT_DETAIL'],
    ['PARTICIPATION_DECLINED', 'VISIT_DETAIL'],
    ['AGENDA_UPDATED', 'AGENDA_DETAIL'],
    ['MINUTES_UPDATED', 'MINUTES_DETAIL'],
    ['ACTION_ITEM_ASSIGNED', 'ACTION_ITEM_DETAIL'],
    ['ACTION_ITEM_DUE', 'ACTION_ITEM_DETAIL'],
    ['LOGISTICS_REQUEST_CREATED', 'LOGISTICS_DETAIL'],
    ['LOGISTICS_ASSIGNED', 'LOGISTICS_DETAIL'],
    ['LOGISTICS_ASSIGNEE_ACCEPTED', 'LOGISTICS_DETAIL'],
    ['LOGISTICS_ASSIGNEE_DECLINED', 'LOGISTICS_DETAIL'],
    ['LOGISTICS_PROPOSAL_CREATED', 'LOGISTICS_DETAIL'],
    ['LOGISTICS_PROPOSAL_ACCEPTED', 'LOGISTICS_DETAIL'],
    ['LOGISTICS_PROPOSAL_REJECTED', 'LOGISTICS_DETAIL'],
    ['LOGISTICS_HANDOVER_SIGNED', 'HANDOVER_DETAIL'],
    ['LOGISTICS_EXPENSE_REMINDER', 'LOGISTICS_DETAIL'],
    ['NEWS_PENDING_APPROVAL', 'NEWS_DETAIL'],
    ['NEWS_APPROVED', 'NEWS_DETAIL'],
    ['NEWS_REJECTED', 'NEWS_DETAIL'],
    ['PARTNER_PENDING_APPROVAL', 'PARTNER_DETAIL'],
    ['PARTNER_APPROVED', 'PARTNER_DETAIL'],
    ['PARTNER_REJECTED', 'PARTNER_DETAIL'],
    ['HOST_FEEDBACK_INVITE', 'FEEDBACK_MODAL'],
    ['VISITOR_FEEDBACK_RECEIVED', 'FEEDBACK_MODAL'],
    ['HOST_FEEDBACK_RECEIVED', 'FEEDBACK_MODAL'],
    ['VISIT_REMINDER', 'VISIT_DETAIL'],
    ['ACCOUNT_STATUS_ACTIVATED', 'ACCOUNT_DETAIL'],
    ['ACCOUNT_STATUS_DEACTIVATED', 'ACCOUNT_DETAIL'],
  ];

  it.each(cases)('%s -> %s', (eventKey, expected) => {
    expect(classifyNotificationIntent(withEvent(eventKey))).toBe(expected);
  });

  it('returns null for an unknown/legacy eventKey', () => {
    expect(classifyNotificationIntent(withEvent('SOME_FUTURE_EVENT_NOT_YET_CLASSIFIED'))).toBeNull();
  });

  it('returns null for a row with no metadataJson at all (true legacy row)', () => {
    expect(classifyNotificationIntent(item({ metadataJson: null }))).toBeNull();
  });

  it('an explicit modern actionType outranks the eventKey', () => {
    const conflicting = withEvent('VISIT_REQUEST_WAITING_APPROVAL', { actionType: 'OPEN_VISIT_HISTORY' });
    expect(classifyNotificationIntent(conflicting)).toBe('VISIT_HISTORY');
  });

  // Plan RC-05/RM-01..03: VisitReminderDispatchService.SendInAppAsync now sets a per-recipient
  // actionType — the current Host gets OPEN_HOST_PROCESS, every other recipient gets
  // OPEN_CONTRIBUTION — instead of one shared eventKey-only classification for everyone.
  it('a Host VISIT_REMINDER (actionType=OPEN_HOST_PROCESS) classifies as HOST_PROCESS, not VISIT_DETAIL', () => {
    const hostReminder = withEvent('VISIT_REMINDER', { actionType: 'OPEN_HOST_PROCESS' });
    expect(classifyNotificationIntent(hostReminder)).toBe('HOST_PROCESS');
  });

  it('a participant VISIT_REMINDER (actionType=OPEN_CONTRIBUTION) classifies as CONTRIBUTION, never HOST_PROCESS', () => {
    const participantReminder = withEvent('VISIT_REMINDER', { actionType: 'OPEN_CONTRIBUTION' });
    expect(classifyNotificationIntent(participantReminder)).toBe('CONTRIBUTION');
  });

  it('a pre-fix legacy VISIT_REMINDER row (no actionType) still falls back to the conservative VISIT_DETAIL', () => {
    expect(classifyNotificationIntent(withEvent('VISIT_REMINDER'))).toBe('VISIT_DETAIL');
  });

  it('does not depend on the active UI language — same item, same intent regardless of caller locale', () => {
    // classifyNotificationIntent never reads title/message/locale at all; asserting it twice on the
    // same item is the language-independence guarantee (plan §52) at the intent layer.
    const evt = withEvent('VISIT_REQUEST_UPDATED_PENDING');
    expect(classifyNotificationIntent(evt)).toBe(classifyNotificationIntent(evt));
  });
});

describe('the UPDATED_PENDING bug: intent travels into the one-shot command URL', () => {
  it('VISIT_REQUEST_UPDATED_PENDING carries notificationIntent=VISIT_HISTORY, never an approval hint', () => {
    const link = resolveNotificationDestination(
      withEvent('VISIT_REQUEST_UPDATED_PENDING', {
        targetUrl: '/dashboard/visit?visitRequestId=123',
        visitRequestId: 123,
        visitInstanceId: null,
      }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123&notificationIntent=VISIT_HISTORY');
  });

  it('VISIT_REQUEST_WAITING_APPROVAL carries notificationIntent=VISIT_REVIEW with the exact campus instance', () => {
    const link = resolveNotificationDestination(
      withEvent('VISIT_REQUEST_WAITING_APPROVAL', {
        targetUrl: '/dashboard/visit?visitRequestId=123',
        visitRequestId: 123,
        visitInstanceId: 456,
      }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456&notificationIntent=VISIT_REVIEW');
  });

  it('a row with no classifiable eventKey/actionType omits notificationIntent (legacy passthrough)', () => {
    const link = resolveNotificationDestination(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
  });
});

// ── PARTICIPATION_INVITED: current backend ActionUrl shape (plan RC-06) ─────────────────────────────

describe('PARTICIPATION_INVITED now routes through the one-shot command, never the generic tab', () => {
  // Producer (InviteVisitParticipantCommandHandler): the non-dept-routed ActionUrl carries
  // ?tab=participants&participantId=N (InviteVisitParticipantCommandHandler.cs L275). The OLD
  // rewrite discarded that participantId entirely and sent everyone to the generic attending-tab
  // list to "find it again" — exactly the RC-06 bug. The fix folds this into the same one-shot
  // command every other Visit-command intent uses: VisitRequestManagement re-resolves the CURRENT
  // participant row (with its real, live participantId) and opens the exact invitation screen —
  // never a client-side scan, and never a stale id parsed off this URL.
  it('rewrites to the one-shot command carrying notificationIntent=VISIT_INVITATION, for every role', () => {
    for (const u of [student, staffLeader]) {
      const link = resolveNotificationDestination(
        withEvent('PARTICIPATION_INVITED', {
          actionType: 'OPEN_VISIT_INVITATION',
          targetUrl: '/dashboard/visit/process/456?tab=participants&participantId=789',
          visitRequestId: 123,
          visitInstanceId: 456,
        }),
        u,
      );
      expect(link).toBe('/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456&notificationIntent=VISIT_INVITATION');
    }
  });
});

// ── Already-exact destinations stay untouched (plan-continuation §4-§8: Logistics/Agenda/Minutes/
//    ActionItem/News/Partner/Account producers already emit item-specific, role-safe URLs — this
//    pins that the resolver's rewrite branches never accidentally degrade them into something
//    generic) ──────────────────────────────────────────────────────────────────────────────────────

describe('already-exact non-visit-list destinations pass through unmodified', () => {
  it('Logistics dept-task link (Department Leader recipient) still maps to itemType=REQUEST', () => {
    const link = resolveNotificationDestination(
      withEvent('LOGISTICS_REQUEST_CREATED', {
        actionType: 'OPEN_LOGISTICS_DETAIL',
        targetUrl: '/dashboard/departments/9/tasks/321',
        visitInstanceId: 456,
      }),
      deptLeader,
    );
    expect(link).toBe('/dashboard/visit?taskId=321&itemType=REQUEST');
  });

  it('Logistics Host-process fallback (Host recipient) is untouched', () => {
    const link = resolveNotificationDestination(
      withEvent('LOGISTICS_ASSIGNEE_ACCEPTED', {
        targetUrl: '/dashboard/visit/process/456',
        visitInstanceId: 456,
      }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit/process/456');
  });

  it('Handover item link is untouched', () => {
    const link = resolveNotificationDestination(
      withEvent('LOGISTICS_HANDOVER_SIGNED', {
        actionType: 'OPEN_HANDOVER_DETAIL',
        targetUrl: '/dashboard/visit/process/456',
        visitInstanceId: 456,
      }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit/process/456');
  });

  it('Agenda contribution link is untouched for a Student participant', () => {
    const link = resolveNotificationDestination(
      withEvent('AGENDA_UPDATED', { targetUrl: '/dashboard/visit/contribution/456', visitInstanceId: 456 }),
      student,
    );
    expect(link).toBe('/dashboard/visit/contribution/456');
  });

  it('post-visit-tasks (Action Item) link is untouched', () => {
    const link = resolveNotificationDestination(
      withEvent('ACTION_ITEM_ASSIGNED', { targetUrl: '/dashboard/post-visit-tasks?actionItemId=55' }),
      student,
    );
    expect(link).toBe('/dashboard/post-visit-tasks?actionItemId=55');
  });

  it('News filtered-record link is untouched (already handled by the relatedType rewrite)', () => {
    const link = resolveNotificationDestination(
      withEvent('NEWS_PENDING_APPROVAL', {
        relatedType: 'NEWS', relatedId: 55, targetUrl: '/dashboard/visit?visitRequestId=123',
      }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/news?newsId=55');
  });

  it('Partner filtered-record link is untouched (already handled by the relatedType rewrite)', () => {
    const link = resolveNotificationDestination(
      withEvent('PARTNER_PENDING_APPROVAL', {
        relatedType: 'PARTNER', relatedId: 77, targetUrl: '/dashboard/visit?visitRequestId=123',
      }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/partners?partnerId=77');
  });

  it('Account: non-admin/HO/leader recipient already gets /dashboard/profile from the producer, untouched', () => {
    const link = resolveNotificationDestination(
      withEvent('ACCOUNT_STATUS_ACTIVATED', { actionType: 'OPEN_ACCOUNT_DETAIL', targetUrl: '/dashboard/profile' }),
      student,
    );
    expect(link).toBe('/dashboard/profile');
  });

  it('Account: HO/leader recipient gets /dashboard/accounts from the producer, untouched', () => {
    const link = resolveNotificationDestination(
      withEvent('ACCOUNT_STATUS_ACTIVATED', { actionType: 'OPEN_ACCOUNT_DETAIL', targetUrl: '/dashboard/accounts' }),
      ho,
    );
    expect(link).toBe('/dashboard/accounts');
  });
});
