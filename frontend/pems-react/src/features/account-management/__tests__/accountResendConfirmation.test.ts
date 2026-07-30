import { describe, it, expect } from 'vitest';
import {
  canResendEmailConfirmation,
  resendDeliveryFeedback,
  resendResultSummary,
} from '../adapters/accountResendConfirmation';

/**
 * The two rules the resend action turns on (spec §2 and §14). Both are exercised through the same
 * functions AccountManagement.tsx calls, so a regression in the page's gating or its delivery
 * reporting fails here.
 */
describe('canResendEmailConfirmation', () => {
  const pending = { canResend: true, detailLoaded: true, detailStatus: 'PENDING_EMAIL_CONFIRMATION' };

  it('offers the action for a pending account once the detail has loaded', () => {
    expect(canResendEmailConfirmation(pending)).toBe(true);
  });

  it.each(['ACTIVE', 'INACTIVE', 'LOCKED'])('hides it for %s', (detailStatus) => {
    expect(canResendEmailConfirmation({ ...pending, detailStatus })).toBe(false);
  });

  // §2.3 — while the detail is in flight (or after it failed) there is no trustworthy status, so
  // the action must not appear. The list row is deliberately not consulted as a substitute.
  it('hides it until the detail request has resolved', () => {
    expect(canResendEmailConfirmation({ ...pending, detailLoaded: false })).toBe(false);
  });

  it('hides it when the detail request failed and left no status', () => {
    expect(canResendEmailConfirmation({ canResend: true, detailLoaded: false, detailStatus: undefined })).toBe(false);
    expect(canResendEmailConfirmation({ canResend: true, detailLoaded: true, detailStatus: null })).toBe(false);
  });

  // The permission is the backend's answer, not a role check re-derived here: a Staff Leader runs
  // this for their own campus, and only the query can weigh sub-role + campus + the self rule.
  it('hides it when the backend did not grant the permission', () => {
    expect(canResendEmailConfirmation({ ...pending, canResend: false })).toBe(false);
  });

  // An older backend that does not send the flag must hide the button, not offer one that 403s.
  it.each([undefined, null])('treats a missing permission flag (%j) as no', (canResend) => {
    expect(canResendEmailConfirmation({ ...pending, canResend })).toBe(false);
  });

  it('normalizes casing and whitespace from the server', () => {
    expect(canResendEmailConfirmation({ ...pending, detailStatus: '  pending_email_confirmation ' })).toBe(true);
  });

  // A display-mapped row value ("Pending") is NOT the detail status and must not open the action.
  it('does not accept the list row display status', () => {
    expect(canResendEmailConfirmation({ ...pending, detailStatus: 'Pending' })).toBe(false);
  });
});

describe('resendDeliveryFeedback', () => {
  const email = 'pending.user@fpt.edu.vn';

  it('reports a real send as success, naming the address', () => {
    const feedback = resendDeliveryFeedback('SENT', email);
    expect(feedback.kind).toBe('success');
    expect(feedback.message).toBe(`Đã gửi lại email xác nhận đến ${email}.`);
  });

  // §14.2/§14.3 — the request succeeded, the mail did not. Saying "đã gửi" here would be a lie.
  it('warns rather than claims success when delivery was skipped', () => {
    const feedback = resendDeliveryFeedback('SKIPPED', email);
    expect(feedback.kind).toBe('warning');
    expect(feedback.message).toBe(
      'Yêu cầu đã được xử lý nhưng email không được gửi trong môi trường hiện tại.',
    );
    expect(feedback.message).not.toContain('Đã gửi lại email xác nhận đến');
  });

  it('reports a failed delivery as an error and says the account is still pending', () => {
    const feedback = resendDeliveryFeedback('FAILED', email);
    expect(feedback.kind).toBe('error');
    expect(feedback.message).toContain('chờ xác nhận email');
  });

  it.each([undefined, null, '', 'SOMETHING_NEW'])(
    'degrades to a non-committal warning for %j',
    (status) => {
      const feedback = resendDeliveryFeedback(status, email);
      expect(feedback.kind).toBe('warning');
      expect(feedback.message).toBe(
        'Yêu cầu đã được xử lý nhưng chưa xác định được trạng thái gửi email.',
      );
    },
  );

  it('normalizes casing from the server', () => {
    expect(resendDeliveryFeedback('sent', email).kind).toBe('success');
    expect(resendDeliveryFeedback(' Failed ', email).kind).toBe('error');
  });
});

describe('resendResultSummary', () => {
  // The whole reason this function exists: the modal used to print the transport status verbatim
  // ("lần gần nhất: SENT"), which tells a Vietnamese-speaking operator nothing.
  it.each(['SENT', 'SKIPPED', 'FAILED', 'SOMETHING_NEW', null])(
    'never leaks the raw status code for %j',
    (status) => {
      const summary = resendResultSummary(status, 1);
      const text = `${summary.headline} ${summary.detail ?? ''}`;
      expect(text).not.toMatch(/SENT|SKIPPED|FAILED|SOMETHING_NEW/);
    },
  );

  it('says plainly that the mail went out, and how many sends have happened', () => {
    const summary = resendResultSummary('SENT', 2);
    expect(summary.kind).toBe('success');
    expect(summary.headline).toBe('Đã gửi email xác nhận.');
    expect(summary.detail).toBe('Số lần đã gửi lại: 2.');
  });

  it('omits the counter when there is no meaningful count', () => {
    expect(resendResultSummary('SENT', null).detail).toBeNull();
    expect(resendResultSummary('SENT', 0).detail).toBeNull();
  });

  // SKIPPED is an environment problem, not an account problem — the note must say so, otherwise HO
  // retries in a loop that cannot succeed.
  it('explains that a skipped send is the environment, not the account', () => {
    const summary = resendResultSummary('SKIPPED', 1);
    expect(summary.kind).toBe('warning');
    expect(summary.headline).toBe('Chưa gửi được email.');
    expect(summary.detail).toContain('tắt chức năng gửi mail');
  });

  it('tells the reader the account is still pending and a retry is possible after a failure', () => {
    const summary = resendResultSummary('FAILED', 1);
    expect(summary.kind).toBe('error');
    expect(summary.detail).toContain('vẫn chờ xác nhận');
    expect(summary.detail).toContain('gửi lại');
  });

  it('stays non-committal on an unrecognised status', () => {
    const summary = resendResultSummary('WAT', 1);
    expect(summary.kind).toBe('warning');
    expect(summary.headline).toBe('Chưa xác định được kết quả gửi email.');
  });
});
