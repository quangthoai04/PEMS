/**
 * G11 / R-103 — a retry must carry the same key; a new send must not.
 *
 * `useGuardedSend` (tested next door) stops the second click while the first request is in flight. It
 * cannot stop the case that actually duplicates emails: the browser gives up on a slow request, the
 * promise rejects, the flag clears, and the user — told it failed — presses again. The server never saw
 * the disconnect and runs the whole thing a second time.
 *
 * So the rule under test is about intent, not timing. A timeout, a 5xx, an "đang xử lý" and an unknown
 * outcome all mean "still trying to finish ONE send" and keep the key. A confirmed success, a refusal
 * decided before anything was sent, and a key the server rejects all end the attempt.
 */
import { describe, expect, it, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import {
  useIdempotentSend,
  attemptIsOver,
  sendFailureMessage,
} from '../hooks/useIdempotentSend';

/** The error shapes axios produces, named for what actually happened. */
const timeout = () => ({ code: 'ECONNABORTED', message: 'timeout of 30000ms exceeded' });
const offline = () => ({ message: 'Network Error' });
const serverError = () => ({ response: { status: 500, data: { message: 'Lỗi máy chủ' } } });
const badGateway = () => ({ response: { status: 502, data: {} } });
const businessRefusal = () => ({
  response: { status: 422, data: { errorCode: 'EMAIL_REPORT_DELIVERY_FAILED', message: 'Không gửi được' } },
});
const scopeRefusal = () => ({ response: { status: 404, data: { message: 'Không tìm thấy campus.' } } });
const inProgress = () => ({
  response: { status: 409, data: { errorCode: 'EMAIL_IDEMPOTENCY_IN_PROGRESS', message: 'Đang xử lý' } },
});
const outcomeUnknown = () => ({
  response: { status: 409, data: { errorCode: 'EMAIL_IDEMPOTENCY_OUTCOME_UNKNOWN', message: 'Chưa rõ' } },
});
const keyReused = () => ({
  response: {
    status: 409,
    data: { errorCode: 'IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_REQUEST', message: 'Khác nội dung' },
  },
});
const keyMissing = () => ({
  response: { status: 400, data: { errorCode: 'EMAIL_IDEMPOTENCY_KEY_REQUIRED', message: 'Thiếu key' } },
});

describe('useIdempotentSend', () => {
  beforeEach(() => sessionStorage.clear());

  it('gives one key per resource and keeps it until the attempt ends', () => {
    const { result } = renderHook(() => useIdempotentSend());

    const first = result.current.keyFor('ho-campus-report', 7);
    const second = result.current.keyFor('ho-campus-report', 7);

    expect(second).toBe(first);
  });

  it('gives different rows different keys', () => {
    const { result } = renderHook(() => useIdempotentSend());

    expect(result.current.keyFor('ho-campus-report', 7))
      .not.toBe(result.current.keyFor('ho-campus-report', 8));
  });

  it('gives different operations different keys for the same id', () => {
    const { result } = renderHook(() => useIdempotentSend());

    // A campus 7 report and a department 7 report are not the same send.
    expect(result.current.keyFor('ho-campus-report', 7))
      .not.toBe(result.current.keyFor('sl-department-report', 7));
  });

  it('mints a new key only after the attempt is completed', () => {
    const { result } = renderHook(() => useIdempotentSend());

    const first = result.current.keyFor('ho-campus-report', 7);
    act(() => result.current.complete('ho-campus-report', 7));
    const second = result.current.keyFor('ho-campus-report', 7);

    expect(second).not.toBe(first);
  });

  it('survives a remount, so a reload mid-send does not become a second send', () => {
    const a = renderHook(() => useIdempotentSend());
    const key = a.result.current.keyFor('ho-campus-report', 7);
    a.unmount();

    // A fresh component tree reading the same session: this is the reload case.
    const b = renderHook(() => useIdempotentSend());
    expect(b.result.current.keyFor('ho-campus-report', 7)).toBe(key);
  });

  it('keeps the key out of localStorage, so tomorrow is a new send', () => {
    const { result } = renderHook(() => useIdempotentSend());
    result.current.keyFor('ho-campus-report', 7);

    expect(Object.keys(localStorage)).toHaveLength(0);
    expect(Object.keys(sessionStorage).length).toBeGreaterThan(0);
  });

  it('produces a key the backend will accept', () => {
    const { result } = renderHook(() => useIdempotentSend());
    const key = result.current.keyFor('ho-campus-report', 7);

    // The server's rule: 8–200 printable ASCII, no space, no control characters.
    expect(key.length).toBeGreaterThanOrEqual(8);
    expect(key.length).toBeLessThanOrEqual(200);
    expect(key).toMatch(/^[\x21-\x7e]+$/);
  });
});

describe('attemptIsOver', () => {
  it('keeps the key when the request never got an answer', () => {
    // The send may already have happened. Minting a new key here is exactly how one click becomes two
    // emails — the single most important case in this file.
    expect(attemptIsOver(timeout())).toBe(false);
    expect(attemptIsOver(offline())).toBe(false);
  });

  it('keeps the key on a server error or a gateway failure', () => {
    expect(attemptIsOver(serverError())).toBe(false);
    expect(attemptIsOver(badGateway())).toBe(false);
  });

  it('keeps the key while the server says the send is in progress', () =>
    expect(attemptIsOver(inProgress())).toBe(false));

  it('keeps the key when the outcome is unknown', () =>
    expect(attemptIsOver(outcomeUnknown())).toBe(false));

  it('ends the attempt on a refusal decided before anything was sent', () => {
    expect(attemptIsOver(businessRefusal())).toBe(true);
    expect(attemptIsOver(scopeRefusal())).toBe(true);
  });

  it('ends the attempt when the key itself is the problem', () => {
    // Retrying with the same unusable key would just repeat the same refusal forever.
    expect(attemptIsOver(keyReused())).toBe(true);
    expect(attemptIsOver(keyMissing())).toBe(true);
  });
});

describe('sendFailureMessage', () => {
  it('does not say "failed" when the outcome is unknown', () => {
    const message = sendFailureMessage(outcomeUnknown(), 'Gửi báo cáo thất bại.');

    expect(message).toContain('có thể đã được gửi');
    expect(message).not.toContain('thất bại');
  });

  it('does not say "failed" when the connection dropped', () => {
    const message = sendFailureMessage(timeout(), 'Gửi báo cáo thất bại.');

    expect(message).toContain('Mất kết nối');
    expect(message).not.toContain('thất bại');
  });

  it('distinguishes in-progress from a failure', () => {
    const message = sendFailureMessage(inProgress(), 'Gửi báo cáo thất bại.');

    expect(message).toContain('đang được xử lý');
    expect(message).toContain('đừng gửi lại');
  });

  it('explains a reused key as a content change', () =>
    expect(sendFailureMessage(keyReused(), 'x')).toContain('Nội dung đã thay đổi'));

  it('passes a real business refusal through unchanged', () =>
    expect(sendFailureMessage(businessRefusal(), 'fallback')).toBe('Không gửi được'));

  it('falls back when the server said nothing useful', () =>
    expect(sendFailureMessage({ response: { status: 500, data: {} } }, 'fallback')).toBe('fallback'));
});

// ── The two rules together: what a screen actually does ──────────────────────

describe('a send attempt end to end', () => {
  beforeEach(() => sessionStorage.clear());

  /** What the report screens do in their catch block. */
  const onFailure = (idem: ReturnType<typeof useIdempotentSend>, error: unknown) => {
    if (attemptIsOver(error)) idem.complete('ho-campus-report', 7);
  };

  it('retries a timed-out send under the same key', () => {
    const { result } = renderHook(() => useIdempotentSend());

    const first = result.current.keyFor('ho-campus-report', 7);
    act(() => onFailure(result.current, timeout()));
    const retry = result.current.keyFor('ho-campus-report', 7);

    expect(retry).toBe(first);
  });

  it('starts a new attempt after a success', () => {
    const { result } = renderHook(() => useIdempotentSend());

    const first = result.current.keyFor('ho-campus-report', 7);
    act(() => result.current.complete('ho-campus-report', 7)); // what the success path does
    const next = result.current.keyFor('ho-campus-report', 7);

    expect(next).not.toBe(first);
  });

  it('starts a new attempt after a refusal that sent nothing', () => {
    const { result } = renderHook(() => useIdempotentSend());

    const first = result.current.keyFor('ho-campus-report', 7);
    act(() => onFailure(result.current, scopeRefusal()));
    const next = result.current.keyFor('ho-campus-report', 7);

    expect(next).not.toBe(first);
  });

  it('holds the key across a whole run of failed retries', () => {
    const { result } = renderHook(() => useIdempotentSend());
    const first = result.current.keyFor('ho-campus-report', 7);

    for (const error of [timeout(), badGateway(), inProgress(), outcomeUnknown(), offline()]) {
      act(() => onFailure(result.current, error));
      expect(result.current.keyFor('ho-campus-report', 7)).toBe(first);
    }
  });
});
