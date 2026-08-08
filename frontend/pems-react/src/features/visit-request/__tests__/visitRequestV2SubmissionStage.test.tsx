import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useVisitRequestFormV2 } from '../hooks/useVisitRequestFormV2';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import { loadVisitRequestV2Draft } from '../utils/visitRequestV2DraftStorage';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

/**
 * Plan §16 — the submission state machine, and what happens when the answer never arrives.
 *
 * The flow used to be reconstructed from four independent booleans plus a session token, and there
 * was no state at all for "the verify died without a verdict". That case fell into the same branch
 * as a wrong code, so a user whose request HAD been created was told something went wrong and
 * invited to try again — which is how one delegation becomes two.
 */

vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: { resendOtp: vi.fn(), recoverOtp: vi.fn() },
}));

vi.mock('../api/visitRequestV2Api', () => ({
  createVisitRequestV2: vi.fn(),
  initiateVisitRequestV2: vi.fn(),
  verifyAndCreateVisitRequestV2: vi.fn(),
  getVisitSubmissionResult: vi.fn(),
}));

vi.mock('../../../shared/utils/toast', async importOriginal => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return { ...actual, showInfoToast: vi.fn(), showSuccessToast: vi.fn() };
});

import {
  initiateVisitRequestV2,
  verifyAndCreateVisitRequestV2,
  getVisitSubmissionResult,
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
    operationalContact: { fullName: 'ĐM CS', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác', phone: '+84911111111', email: 'dmcs@example.com' },
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

const createResponse = (visitRequestId = 2003) => ({
  visitRequestId,
  requestCode: 'VR-MC-HN-0003',
  visitScope: 'SINGLE_CAMPUS',
  hasMixedCampusDetails: false,
  instances: [{ visitInstanceId: 11, campusId: 1, status: 'WAITING_REQUEST_APPROVAL' }],
  pendingContactConfirmations: 0,
  idempotent: false,
  status: 'WAITING_REQUEST_APPROVAL',
  submittedAt: '2026-07-31T09:30:00',
  campusCount: 1,
});

/** A dropped connection: axios reports no response at all. */
const networkFailure = () => Object.assign(new Error('Network Error'), {
  isAxiosError: true, response: undefined,
});

/** A wrong code: the SERVER answered, so nothing is uncertain. */
const otpFailure = () => Object.assign(new Error('bad otp'), {
  isAxiosError: true,
  response: { status: 400, data: { errorCode: 'OTP_INVALID', message: 'Mã OTP không đúng.' } },
});

/** OTP was right, but the form no longer validates server-side. */
const businessFailure = () => Object.assign(new Error('campus closed'), {
  isAxiosError: true,
  response: {
    status: 400,
    data: {
      message: 'Cơ sở đã ngừng nhận đăng ký.',
      errors: { 'Form.CampusVisits[0].CampusId': ['Cơ sở đã ngừng nhận đăng ký.'] },
    },
  },
});

describe('visit request v2: the submission state machine (plan §16)', () => {
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

  // ── Stages ────────────────────────────────────────────────────────────────

  it('walks EDITING → OTP_PENDING → CREATE_CONFIRMED, one stage at a time', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockResolvedValue(createResponse());
    const { result, onSuccess } = setup();

    expect(result.current.stage).toBe('EDITING');

    await submitOnce(result);
    expect(result.current.stage).toBe('OTP_PENDING');
    // Derived, never independently settable: a caller cannot see "submitting AND verifying".
    expect(result.current.isSubmitting).toBe(false);
    expect(result.current.isVerifying).toBe(false);

    await act(async () => { await result.current.verifyOtp('123456'); });
    expect(result.current.stage).toBe('CREATE_CONFIRMED');
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });

  it('calls initiate ONCE and verify ONCE for a single successful submission', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockResolvedValue(createResponse());
    const { result } = setup();

    await submitOnce(result);
    await act(async () => { await result.current.verifyOtp('123456'); });

    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);
    expect(verifyAndCreateVisitRequestV2).toHaveBeenCalledTimes(1);
  });

  it('refuses a second verify while the first is still in flight', async () => {
    let release: (v: unknown) => void = () => {};
    vi.mocked(verifyAndCreateVisitRequestV2).mockImplementation(
      () => new Promise(resolve => { release = resolve; }) as never);

    const { result } = setup();
    await submitOnce(result);

    let first: Promise<void>;
    await act(async () => { first = result.current.verifyOtp('123456'); });
    expect(result.current.stage).toBe('VERIFYING_OTP');

    // A double-click, or an over-eager Enter.
    await act(async () => { await result.current.verifyOtp('123456'); });
    expect(verifyAndCreateVisitRequestV2).toHaveBeenCalledTimes(1);

    await act(async () => { release(createResponse()); await first; });
  });

  it('does not resend, cancel or review while a verify is in flight', async () => {
    let release: (v: unknown) => void = () => {};
    vi.mocked(verifyAndCreateVisitRequestV2).mockImplementation(
      () => new Promise(resolve => { release = resolve; }) as never);

    const { result } = setup();
    await submitOnce(result);
    let pending: Promise<void>;
    await act(async () => { pending = result.current.verifyOtp('123456'); });

    await act(async () => { result.current.cancelOtp(); });
    // Still verifying, still holding the challenge: leaving now would strand the outcome.
    expect(result.current.stage).toBe('VERIFYING_OTP');
    expect(result.current.sessionToken).toBe('token-1');

    await act(async () => { result.current.reviewFormDuringOtp(); });
    expect(result.current.stage).toBe('VERIFYING_OTP');

    await act(async () => { release(createResponse()); await pending; });
  });

  // ── A wrong code (plan §6) ────────────────────────────────────────────────

  it('keeps the challenge, the form and the draft when the code is wrong', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(otpFailure());
    const { result, onSuccess } = setup();
    await submitOnce(result);

    await act(async () => { await result.current.verifyOtp('000000'); });

    expect(result.current.stage).toBe('OTP_PENDING');   // the modal stays open
    expect(result.current.sessionToken).toBe('token-1');
    expect(result.current.otpError).toBeTruthy();
    expect(result.current.form.getValues('registerInfo.email')).toBe('reg@example.com');
    expect(loadVisitRequestV2Draft()).not.toBeNull();
    expect(onSuccess).not.toHaveBeenCalled();
    // And nothing asked for a second code.
    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);
  });

  // ── Right code, wrong data (plan §13) ─────────────────────────────────────

  it('sends the user back to the FIELD when the code was right but the data was not', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(businessFailure());
    const { result } = setup();
    await submitOnce(result);

    await act(async () => { await result.current.verifyOtp('123456'); });

    // Not presented as a wrong OTP — the code was correct.
    expect(result.current.stage).toBe('CREATE_FAILED');
    expect(result.current.sessionToken).toBeNull();
    // "Back to the FIELD" literally: the rejection is on the campus input the server named, and the
    // summary above the button counts it. There is no second copy of it in the banner — that used to
    // repeat the same sentence in a place that could not say which campus it was about.
    expect(result.current.form.formState.errors.campusVisits?.[0]?.campus?.message).toBeTruthy();
    expect(result.current.validationErrorCount).toBe(1);
    expect(result.current.submitError).toBeNull();
    expect(loadVisitRequestV2Draft()).not.toBeNull();
  });

  // ── The verify never came back (plan §10) ─────────────────────────────────

  it('reports a dropped connection as UNCERTAIN, not as a failure', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(networkFailure());
    const { result, onSuccess } = setup();
    await submitOnce(result);

    await act(async () => { await result.current.verifyOtp('123456'); });

    expect(result.current.stage).toBe('CREATE_UNCERTAIN');
    expect(onSuccess).not.toHaveBeenCalled();
    // The typing and the intent are both still on disk.
    expect(loadVisitRequestV2Draft()?.data.registerInfo?.email).toBe('reg@example.com');
    expect(loadVisitRequestV2Draft()?.submissionId).toBeTruthy();
  });

  it('a COMPLETED lookup promotes to the success screen without creating anything', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(networkFailure());
    vi.mocked(getVisitSubmissionResult).mockResolvedValue({
      state: 'COMPLETED',
      visitRequestId: 2003,
      requestCode: 'VR-MC-HN-0003',
      status: 'WAITING_REQUEST_APPROVAL',
      submittedAt: '2026-07-31T09:30:00',
      campusCount: 1,
    });

    const { result, onSuccess } = setup();
    await submitOnce(result);
    await act(async () => { await result.current.verifyOtp('123456'); });
    await act(async () => { await result.current.checkSubmissionResult(); });

    expect(result.current.stage).toBe('CREATE_CONFIRMED');
    expect(onSuccess).toHaveBeenCalledTimes(1);
    expect(onSuccess.mock.calls[0][0]).toMatchObject({
      visitRequestId: 2003, requestCode: 'VR-MC-HN-0003', recoveredByLookup: true,
    });
    // The lookup is a READ: verify was never called a second time.
    expect(verifyAndCreateVisitRequestV2).toHaveBeenCalledTimes(1);
    // A confirmed create is the ONLY thing that clears the draft.
    expect(loadVisitRequestV2Draft()).toBeNull();
  });

  it('a PENDING lookup keeps everything and creates nothing', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(networkFailure());
    vi.mocked(getVisitSubmissionResult).mockResolvedValue({
      state: 'PENDING', visitRequestId: null, requestCode: null,
      status: null, submittedAt: null, campusCount: null,
    });

    const { result, onSuccess } = setup();
    await submitOnce(result);
    await act(async () => { await result.current.verifyOtp('123456'); });
    await act(async () => { await result.current.checkSubmissionResult(); });

    expect(result.current.stage).toBe('CREATE_UNCERTAIN');
    expect(result.current.lastLookup?.state).toBe('PENDING');
    expect(onSuccess).not.toHaveBeenCalled();
    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);
    expect(loadVisitRequestV2Draft()).not.toBeNull();
  });

  it('a failing lookup says so instead of guessing', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(networkFailure());
    vi.mocked(getVisitSubmissionResult).mockRejectedValue(networkFailure());

    const { result } = setup();
    await submitOnce(result);
    await act(async () => { await result.current.verifyOtp('123456'); });
    await act(async () => { await result.current.checkSubmissionResult(); });

    expect(result.current.stage).toBe('CREATE_UNCERTAIN');
    expect(result.current.uncertainError).toBeTruthy();
  });

  it('going back to the form from uncertain keeps the draft and the intent', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(networkFailure());
    const { result } = setup();
    await submitOnce(result);
    const intent = loadVisitRequestV2Draft()?.submissionId;
    await act(async () => { await result.current.verifyOtp('123456'); });

    await act(async () => { result.current.backToFormFromUncertain(); });

    expect(result.current.stage).toBe('EDITING');
    expect(loadVisitRequestV2Draft()?.submissionId).toBe(intent);
    expect(result.current.form.getValues('registerInfo.email')).toBe('reg@example.com');
  });

  it('a gateway error counts as undecided, a 400 does not', async () => {
    const gateway = Object.assign(new Error('bad gateway'), {
      isAxiosError: true, response: { status: 502, data: {} },
    });
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(gateway);
    const { result } = setup();
    await submitOnce(result);
    await act(async () => { await result.current.verifyOtp('123456'); });
    expect(result.current.stage).toBe('CREATE_UNCERTAIN');
  });

  // ── Reviewing the form mid-challenge (plan §12) ───────────────────────────

  it('stepping out to review the form keeps the challenge', async () => {
    const { result } = setup();
    await submitOnce(result);

    await act(async () => { result.current.reviewFormDuringOtp(); });
    expect(result.current.stage).toBe('EDITING');
    // The token is KEPT — that is the whole point; no new code is needed.
    expect(result.current.sessionToken).toBe('token-1');

    await act(async () => { result.current.continueOtpAfterReview(); });
    expect(result.current.stage).toBe('OTP_PENDING');
    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);
  });

  // ── Draft lifecycle (plan §11) ────────────────────────────────────────────

  it('closing the OTP modal keeps the form and returns to EDITING', async () => {
    const { result } = setup();
    await submitOnce(result);

    await act(async () => { result.current.cancelOtp(); });

    expect(result.current.stage).toBe('EDITING');
    expect(result.current.sessionToken).toBeNull();
    expect(loadVisitRequestV2Draft()).not.toBeNull();
    expect(result.current.pendingOtp).not.toBeNull(); // the way back in is still offered
  });

  it('only a confirmed create clears the draft', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2)
      .mockRejectedValueOnce(otpFailure())
      .mockRejectedValueOnce(networkFailure())
      .mockResolvedValueOnce(createResponse());

    const { result } = setup();
    await submitOnce(result);

    await act(async () => { await result.current.verifyOtp('000000'); });
    expect(loadVisitRequestV2Draft()).not.toBeNull();

    await act(async () => { await result.current.verifyOtp('111111'); });
    expect(loadVisitRequestV2Draft()).not.toBeNull();

    await act(async () => { result.current.backToFormFromUncertain(); });
    await act(async () => { result.current.resumeOtp(); });
    await act(async () => { await result.current.verifyOtp('123456'); });

    await waitFor(() => expect(loadVisitRequestV2Draft()).toBeNull());
  });

  it('resetForm mints a NEW intent so "create another" cannot replay onto the finished one', async () => {
    vi.mocked(verifyAndCreateVisitRequestV2).mockResolvedValue(createResponse());
    const { result } = setup();
    await submitOnce(result);
    const firstIntent = loadVisitRequestV2Draft()?.submissionId;
    await act(async () => { await result.current.verifyOtp('123456'); });

    await act(async () => { result.current.resetForm(); });
    expect(result.current.stage).toBe('EDITING');

    await submitOnce(result);
    expect(loadVisitRequestV2Draft()?.submissionId).not.toBe(firstIntent);
  });
});
