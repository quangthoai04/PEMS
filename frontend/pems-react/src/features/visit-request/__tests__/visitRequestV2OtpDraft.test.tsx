import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useVisitRequestFormV2 } from '../hooks/useVisitRequestFormV2';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import { loadVisitRequestV2Draft, visitDraftNamespace } from '../utils/visitRequestV2DraftStorage';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: { resendOtp: vi.fn(), recoverOtp: vi.fn() },
}));

vi.mock('../api/visitRequestV2Api', () => ({
  createVisitRequestV2: vi.fn(),
  initiateVisitRequestV2: vi.fn(),
  verifyAndCreateVisitRequestV2: vi.fn(),
}));

const showInfoToast = vi.fn();
vi.mock('../../../shared/utils/toast', async importOriginal => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return { ...actual, showInfoToast: (...a: unknown[]) => showInfoToast(...a), showSuccessToast: vi.fn() };
});

import { visitRequestApi } from '../api/visitRequestApi';
import {
  initiateVisitRequestV2,
  verifyAndCreateVisitRequestV2,
} from '../api/visitRequestV2Api';

const futureAt = (extraMs = 0): string => {
  const d = new Date(Date.now() + 200 * 3600 * 1000 + extraMs);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};

const validValues = (): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'VN',
  },
  contactPoint: { fullName: 'ĐM', organization: 'ĐH X', phone: '+84987654321', email: 'contact@example.com' },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits: [{
    ...createEmptyCampusVisit('ck-1'),
    campus: 'HN',
    startDatetime: futureAt(),
    endDatetime: futureAt(3 * 3600 * 1000),
    delegationName: 'Đoàn A',
    visitType: 'MEETING',
    purpose: 'Trao đổi',
    workingContent: 'Nội dung làm việc',
    visitors: [{ fullName: 'Khách 1', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }],
    operationalContact: { fullName: 'ĐM CS', organization: 'ĐH X', phone: '+84911111111', email: 'dmcs@example.com' },
  }],
});

const initiateResponse = (sessionToken = 'token-1') => ({
  sessionToken,
  message: 'sent',
  maskedEmail: 're***@example.com',
  expiresAt: '2026-07-26T10:00:00',
  resendAfterSeconds: 60,
  maxAttempts: 5,
});

const otpFailure = Object.assign(new Error('bad otp'), {
  isAxiosError: true,
  response: { status: 400, data: { errorCode: 'OTP_INVALID', message: 'Mã OTP không đúng.' } },
});

/**
 * Everything that happens between "submit" and "verified" — a wrong code, a closed modal, a dead
 * connection, a reload — used to be a reason to type the whole form again, and a retry minted a
 * fresh submissionId, which is what turns one intent into two requests.
 */
describe('visit request v2: the draft survives the OTP round trip', () => {
  const setup = () => {
    const onSuccess = vi.fn();
    const view = renderHook(() => useVisitRequestFormV2(onSuccess, undefined, { mode: 'public' }));
    return { ...view, onSuccess };
  };

  const submitOnce = async (result: { current: ReturnType<typeof useVisitRequestFormV2> }) => {
    act(() => { result.current.form.reset(validValues()); });
    await act(async () => { await result.current.onSubmit(); });
  };

  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    sessionStorage.clear();
    vi.mocked(initiateVisitRequestV2).mockResolvedValue(initiateResponse());
  });

  // ── The draft is on disk before the challenge is even asked for ────────────

  it('writes the draft and the submission intent BEFORE calling initiate', async () => {
    let draftAtCallTime: ReturnType<typeof loadVisitRequestV2Draft> = null;
    vi.mocked(initiateVisitRequestV2).mockImplementation(async () => {
      draftAtCallTime = loadVisitRequestV2Draft();
      return initiateResponse();
    });

    const { result } = setup();
    await submitOnce(result);

    // The 700ms autosave has not necessarily fired when the last field is typed and submitted.
    expect(draftAtCallTime).not.toBeNull();
    expect(draftAtCallTime!.data.registerInfo?.email).toBe('reg@example.com');
    expect(draftAtCallTime!.submissionId).toBeTruthy();
  });

  it('never writes the OTP code or the challenge token into the draft', async () => {
    // The attempt FAILS on purpose: a successful verify clears everything, which would prove
    // nothing about what the draft looks like while a challenge is live.
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(otpFailure);
    const { result } = setup();
    await submitOnce(result);
    // A code that cannot collide with the phone numbers already in the fixture.
    await act(async () => { await result.current.verifyOtp('908070'); });

    const raw = localStorage.getItem('pems_visit_registration_draft_percampus') ?? '';
    expect(raw).not.toContain('908070');
    expect(raw).not.toContain('token-1');
    expect(raw).not.toContain('otpCode');
    // The token lives in sessionStorage instead — tab-scoped, gone when the tab is.
    expect(sessionStorage.getItem('pems_visit_registration_otp_challenge')).toContain('token-1');
  });

  // ── Failure does not cost the user their typing ───────────────────────────

  it('keeps the draft and the submissionId when the code is wrong', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(otpFailure);
    const { result } = setup();
    await submitOnce(result);
    const idAfterSubmit = loadVisitRequestV2Draft()?.submissionId;

    await act(async () => { await result.current.verifyOtp('000000'); });

    expect(result.current.otpError).toBeTruthy();
    expect(result.current.sessionToken).toBe('token-1'); // the modal stays open
    const draft = loadVisitRequestV2Draft();
    expect(draft?.data.campusVisits?.[0].delegationName).toBe('Đoàn A');
    expect(draft?.submissionId).toBe(idAfterSubmit);
  });

  it('keeps the draft when initiate itself fails (mail down, no network)', async () => {
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(new Error('network'));
    const { result } = setup();
    await submitOnce(result);

    expect(result.current.submitError).toBeTruthy();
    const draft = loadVisitRequestV2Draft();
    expect(draft?.data.registerInfo?.fullName).toBe('Người ĐK');
    expect(draft?.submissionId).toBeTruthy(); // the retry reuses this intent
  });

  it('reuses the same submissionId across a failed attempt, a resend and a re-submit', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(otpFailure);
    vi.mocked(visitRequestApi.resendOtp).mockResolvedValue({
      sessionToken: 'token-2', message: 'resent', maskedEmail: 're***@example.com',
      expiresAt: '2026-07-26T10:10:00', resendAfterSeconds: 60, maxAttempts: 5,
    } as never);

    const { result } = setup();
    await submitOnce(result);
    const first = vi.mocked(initiateVisitRequestV2).mock.calls[0][0].submissionId;

    await act(async () => { await result.current.verifyOtp('000000'); });
    await act(async () => { await result.current.resendOtp(); });
    act(() => { result.current.cancelOtp(); });
    await act(async () => { await result.current.onSubmit(); });

    const second = vi.mocked(initiateVisitRequestV2).mock.calls[1][0].submissionId;
    expect(second).toBe(first);
    expect(loadVisitRequestV2Draft()?.submissionId).toBe(first);
  });

  // ── Closing the modal is not cancelling the request ───────────────────────

  it('closing the OTP modal keeps everything and says the request was saved', async () => {
    const { result } = setup();
    await submitOnce(result);

    act(() => { result.current.cancelOtp(); });

    expect(result.current.sessionToken).toBeNull();          // modal closed
    expect(result.current.pendingOtp).not.toBeNull();        // …but the challenge is still ours
    expect(loadVisitRequestV2Draft()?.data.registerInfo?.email).toBe('reg@example.com');
    expect(showInfoToast).toHaveBeenCalled();
  });

  it('can resume the challenge it already asked for, without sending a new code', async () => {
    const { result } = setup();
    await submitOnce(result);
    act(() => { result.current.cancelOtp(); });

    let resumed = false;
    act(() => { resumed = result.current.resumeOtp(); });

    expect(resumed).toBe(true);
    expect(result.current.sessionToken).toBe('token-1');
    expect(result.current.maskedEmail).toBe('re***@example.com');
    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1); // no second code was requested
  });

  // ── Reload ────────────────────────────────────────────────────────────────

  it('a reload restores the answers, the submission intent and the pending challenge', async () => {
    const { result, unmount } = setup();
    await submitOnce(result);
    const original = loadVisitRequestV2Draft()?.submissionId;
    unmount(); // the tab reloads: React state is gone, storage is not

    const { result: reopened } = setup();
    act(() => { reopened.current.restoreDraft(); });

    expect(reopened.current.form.getValues('campusVisits')[0].delegationName).toBe('Đoàn A');
    expect(reopened.current.pendingOtp?.maskedEmail).toBe('re***@example.com');

    let resumed = false;
    act(() => { resumed = reopened.current.resumeOtp(); });
    expect(resumed).toBe(true);

    // Same intent as before the reload → the backend still sees ONE submission.
    vi.mocked(verifyAndCreateVisitRequestV2).mockResolvedValue({} as never);
    await act(async () => { await reopened.current.verifyOtp('123456'); });
    expect(vi.mocked(verifyAndCreateVisitRequestV2).mock.calls[0][0].submissionId).toBe(original);
  });

  it('does not offer to resume when the tab no longer holds the challenge token', async () => {
    const { result, unmount } = setup();
    await submitOnce(result);
    unmount();
    sessionStorage.clear(); // a NEW tab, or the browser was closed and reopened

    const { result: reopened } = setup();
    act(() => { reopened.current.restoreDraft(); });

    expect(reopened.current.pendingOtp).not.toBeNull(); // we know one was requested…
    let resumed = true;
    act(() => { resumed = reopened.current.resumeOtp(); });
    expect(resumed).toBe(false);                        // …but it cannot be continued here
    expect(reopened.current.form.getValues('registerInfo').email).toBe('reg@example.com');
  });

  // ── Changing the registrant email ─────────────────────────────────────────

  it('drops a challenge that was minted for a different mailbox, keeping the form', async () => {
    const { result } = setup();
    await submitOnce(result);
    act(() => { result.current.cancelOtp(); });
    expect(result.current.pendingOtp).not.toBeNull();

    act(() => {
      result.current.form.setValue('registerInfo.email', 'somebody.else@example.com');
    });

    await waitFor(() => expect(result.current.pendingOtp).toBeNull());
    expect(sessionStorage.getItem('pems_visit_registration_otp_challenge')).toBeNull();
    // The typing is untouched — only the challenge was about the old address.
    expect(result.current.form.getValues('campusVisits')[0].delegationName).toBe('Đoàn A');
    expect(loadVisitRequestV2Draft()?.otp).toBeUndefined();
  });

  it('treats the same mailbox in different case as the same challenge', async () => {
    const { result } = setup();
    await submitOnce(result);
    act(() => { result.current.cancelOtp(); });

    act(() => { result.current.form.setValue('registerInfo.email', ' REG@Example.com '); });

    await waitFor(() => expect(result.current.form.getValues('registerInfo').email).toBe(' REG@Example.com '));
    expect(result.current.pendingOtp).not.toBeNull();
  });

  // ── Success is the only thing that clears the draft ───────────────────────

  it('clears the draft, the intent and the token only after a confirmed create', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockResolvedValue({ visitRequestId: 7 } as never);
    const { result, onSuccess } = setup();
    await submitOnce(result);
    expect(loadVisitRequestV2Draft()).not.toBeNull();

    await act(async () => { await result.current.verifyOtp('123456'); });

    expect(onSuccess).toHaveBeenCalled();
    expect(loadVisitRequestV2Draft()).toBeNull();
    expect(sessionStorage.getItem('pems_visit_registration_otp_challenge')).toBeNull();
    expect(result.current.pendingOtp).toBeNull();
  });

  it('discarding the draft removes the pending challenge with it', async () => {
    const { result } = setup();
    await submitOnce(result);
    act(() => { result.current.cancelOtp(); });

    act(() => { result.current.discardDraft(); });

    expect(loadVisitRequestV2Draft()).toBeNull();
    expect(result.current.pendingOtp).toBeNull();
    expect(sessionStorage.getItem('pems_visit_registration_otp_challenge')).toBeNull();
  });
});

/** One draft per ACCOUNT, and the public form's draft is not any account's. */
describe('draft namespaces keep one person out of another person\'s form', () => {
  beforeEach(() => { localStorage.clear(); vi.clearAllMocks(); });

  it('gives each account its own key and public visitors the bare one', () => {
    expect(visitDraftNamespace(12)).toBe('u12');
    expect(visitDraftNamespace('12')).toBe('u12');
    expect(visitDraftNamespace(undefined)).toBeUndefined();
    expect(visitDraftNamespace(null)).toBeUndefined();
  });

  it('never reads another user\'s draft, and public never reads an authenticated one', async () => {
    const renderFor = (draftNamespace?: string) =>
      renderHook(() => useVisitRequestFormV2(vi.fn(), undefined, { mode: 'public', draftNamespace }));

    const a = renderFor(visitDraftNamespace(1));
    act(() => { a.result.current.form.reset(validValues()); });
    act(() => { a.result.current.saveDraftNow(); });
    a.unmount();

    const b = renderFor(visitDraftNamespace(2));
    expect(b.result.current.detectDraft()).toBe(false);
    b.unmount();

    const anonymous = renderFor(undefined);
    expect(anonymous.result.current.detectDraft()).toBe(false);
    anonymous.unmount();

    const backAsA = renderFor(visitDraftNamespace(1));
    expect(backAsA.result.current.detectDraft()).toBe(true);
  });
});
