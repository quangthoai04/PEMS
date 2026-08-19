/**
 * `getNotificationLink()` decides where a notification click navigates. Before this fix it passed
 * `/dashboard/visit?visitRequestId=N` straight through — which every current Visit notification
 * producer emits (V2CreateNotifier, CancelVisitRequest, RejectCampusInstance, ...): the visit list
 * page only used that parameter to FILTER the list down to one row, never to open it, so the
 * reported bug ("bấm notification không thấy gì đổi") was really this rewrite step doing nothing.
 *
 * The fix rewrites that one pattern (and the older bare "/dashboard/visit" one) into the one-shot
 * command `openVisitRequestId`(+`openVisitInstanceId`) the list page's own deep-link effect
 * consumes — see VisitRequestManagementNotificationDeepLink.test.tsx for what happens after
 * navigation. Every other existing rewrite (dept task links, process/reception/feedback links,
 * NEWS/PARTNER) must keep behaving exactly as before; those are pinned here too so this file cannot
 * regress them while fixing the one pattern that was actually broken.
 */
import { describe, expect, it } from 'vitest';
import { getNotificationLink } from '../NotificationBellButton';
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
  targetUrl: null,
  canOpen: true,
  disabledReason: null,
};

const item = (over: Partial<NotificationItem>): NotificationItem => ({ ...baseItem, ...over });

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
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
  });

  it('includes the campus instance when the notification named one (multi-campus exact target)', () => {
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123, visitInstanceId: 456 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123&openVisitInstanceId=456');
  });

  it('rewrites the very old bare "/dashboard/visit" format the same way', () => {
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit', visitRequestId: 123 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
  });

  it('applies for every role, not only Staff Leader', () => {
    for (const user of [staffLeader, visitor, ho]) {
      const link = getNotificationLink(
        item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123 }),
        user,
      );
      expect(link).toBe('/dashboard/visit?openVisitRequestId=123');
    }
  });

  it('does nothing without a user (falls through unchanged, same as before)', () => {
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123', visitRequestId: 123 }),
      null,
    );
    expect(link).toBe('/dashboard/visit?visitRequestId=123');
  });

  it('leaves a link with extra query params alone (not the exact pattern this rewrite targets)', () => {
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit?visitRequestId=123&tab=all', visitRequestId: 123 }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/visit?visitRequestId=123&tab=all');
  });
});

describe('the already-fixed feedback one-shot link is untouched', () => {
  it('never rewrites a link that already carries feedbackVisitInstanceId', () => {
    const link = getNotificationLink(
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
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit/process/900/tasks/77', actionType: 'OPEN_VISIT_DETAIL' }),
      deptStaff,
    );
    expect(link).toBe('/dashboard?taskId=77&itemType=REQUEST');
  });

  it('Dept leader task links still go to the visit list with taskId, not openVisitRequestId', () => {
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit/process/900/tasks/77' }),
      deptLeader,
    );
    expect(link).toBe('/dashboard/visit?taskId=77&itemType=REQUEST');
  });

  it('Visitor process-detail links still rewrite to visitRequestId (unrelated pattern, own branch)', () => {
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit/process/900', visitRequestId: 123 }),
      visitor,
    );
    expect(link).toBe('/dashboard/visit?visitRequestId=123');
  });

  it('Student contribution links still route to the contribution screen', () => {
    const link = getNotificationLink(
      item({ targetUrl: '/dashboard/visit/process/900', visitInstanceId: 456 }),
      student,
    );
    expect(link).toBe('/dashboard/visit/contribution/456');
  });

  it('NEWS notifications still route to the news manager regardless of targetUrl', () => {
    const link = getNotificationLink(
      item({ relatedType: 'NEWS', relatedId: 55, targetUrl: '/dashboard/visit?visitRequestId=123' }),
      staffLeader,
    );
    expect(link).toBe('/dashboard/news?newsId=55');
  });
});
