import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { useVisitRequestFormV2 } from '../hooks/useVisitRequestFormV2';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {

    resendOtp: vi.fn(),
    recoverOtp: vi.fn(),
  },
}));

vi.mock('../api/visitRequestV2Api', () => ({
  createVisitRequestV2: vi.fn(),
  initiateVisitRequestV2: vi.fn(),
  verifyAndCreateVisitRequestV2: vi.fn(),
}));

import { visitRequestApi } from '../api/visitRequestApi';
import {
  createVisitRequestV2,
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
    operationalContactSource: 'EXTERNAL',
  }],
});

const mockCreateResponse = {
  visitRequestId: 1,
  requestCode: 'VR-001',
  visitScope: 'SINGLE_CAMPUS',
  hasMixedCampusDetails: false,
  instances: [{ visitInstanceId: 10, campusId: 1, status: 'PENDING' }],
  pendingContactConfirmations: 0,
  idempotent: false,
};

describe('useVisitRequestFormV2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * `currentUserEmail` is compared against the registrant address as an INVARIANT ASSERTION in
   * authenticated mode (the hook no longer routes between two submit contracts on it — authenticated
   * create is self-registration always), so it defaults to the registrant address in `validValues()`
   * i.e. the invariant holds unless a test deliberately breaks it.
   */
  const setup = (
    mode: 'public' | 'authenticated' = 'public',
    currentUserEmail: string | null = 'reg@example.com',
    isInternalActor = false,
  ) => {
    const onSuccess = vi.fn();
    const view = renderHook(() =>
      useVisitRequestFormV2(onSuccess, undefined, { mode, currentUserEmail, isInternalActor }));
    return { ...view, onSuccess };
  };

  it('add/copy/remove keep campus cards independent with stable clientKeys', () => {
    const { result } = setup();
    act(() => {
      result.current.form.reset(validValues());
    });

    act(() => {
      expect(result.current.addCampusVisit(0)).toBe(true); // copy from card 0
    });
    let list = result.current.form.getValues('campusVisits');
    expect(list).toHaveLength(2);
    expect(list[1].clientKey).not.toBe(list[0].clientKey); // new identity
    expect(list[1].campus).toBe(''); // schedule/campus NOT copied
    expect(list[1].delegationName).toBe('Đoàn A'); // content copied

    // Editing the copy never touches the source:
    act(() => {
      result.current.form.setValue('campusVisits.1.visitors.0.fullName', 'ĐÃ SỬA');
    });
    list = result.current.form.getValues('campusVisits');
    expect(list[0].visitors[0].fullName).toBe('Khách 1');

    act(() => {
      expect(result.current.removeCampusVisit(1)).toBe(true);
    });
    list = result.current.form.getValues('campusVisits');
    expect(list).toHaveLength(1);
    expect(list[0].clientKey).toBe('ck-1');

    // The LAST campus can never be removed:
    act(() => {
      expect(result.current.removeCampusVisit(0)).toBe(false);
    });
  });

  it('apply-to-all is a two-step confirm: request → prompt, cancel applies nothing', () => {
    const { result } = setup();
    const values = validValues();
    values.campusVisits.push({
      ...createEmptyCampusVisit('ck-2'),
      campus: 'HCM',
      delegationName: 'Đoàn B riêng',
      purpose: 'Khác',
      visitors: [{ fullName: 'Khách B', jobTitle: 'x', organization: 'y', nationality: 'z' }],
    });
    act(() => {
      result.current.form.reset(values);
    });

    act(() => {
      result.current.requestApplyToAll(0, (_, i) => `Cơ sở ${i + 1}`);
    });
    expect(result.current.applyToAllPrompt).toEqual({
      sourceIndex: 0,
      overwrittenLabels: ['Cơ sở 2'],
    });

    // Cancel → nothing changed
    act(() => {
      result.current.cancelApplyToAll();
    });
    expect(result.current.applyToAllPrompt).toBeNull();
    expect(result.current.form.getValues('campusVisits.1.delegationName')).toBe('Đoàn B riêng');

    // Confirm → content copied, identity + campus preserved
    act(() => {
      result.current.requestApplyToAll(0, (_, i) => `Cơ sở ${i + 1}`);
    });
    act(() => {
      result.current.confirmApplyToAll();
    });
    const list = result.current.form.getValues('campusVisits');
    expect(list[1].delegationName).toBe('Đoàn A');
    expect(list[1].clientKey).toBe('ck-2');
    expect(list[1].campus).toBe('HCM');
  });

  it('PUBLIC: initiate sends the REAL v2 form (no v1 projection); verify replays the same submissionId', async () => {
    vi.mocked(initiateVisitRequestV2).mockResolvedValue({
      sessionToken: 'sess-1', message: 'ok', maskedEmail: 'r***@example.com', expiresAt: '',
      maxAttempts: 5, resendAfterSeconds: 60,
    } as never);
    vi.mocked(verifyAndCreateVisitRequestV2).mockResolvedValue(mockCreateResponse as never);

    const { result, onSuccess } = setup('public');
    act(() => {
      result.current.form.reset(validValues());
    });

    await act(async () => {
      await result.current.onSubmit();
    });

    // The public flow is pure v2 now.
    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);
    const [initPayload] = vi.mocked(initiateVisitRequestV2).mock.calls[0];
    expect(initPayload.campusVisits[0].campusId).toBe('HN');
    expect(initPayload.campusVisits[0].plannedStartAt).toBeTruthy();
    const submissionId = initPayload.submissionId;
    expect(typeof submissionId).toBe('string');
    await waitFor(() => expect(result.current.sessionToken).toBe('sess-1'));

    await act(async () => {
      await result.current.verifyOtp('123456');
    });

    expect(verifyAndCreateVisitRequestV2).toHaveBeenCalledTimes(1);
    const [payload, otpCode, sessionToken] = vi.mocked(verifyAndCreateVisitRequestV2).mock.calls[0];
    expect(otpCode).toBe('123456');
    expect(sessionToken).toBe('sess-1');
    expect(payload.submissionId).toBe(submissionId); // same intent across initiate → verify (binding key)
    expect(payload.campusVisits[0].campusId).toBe('HN');
    expect(payload.campusVisits[0].visitors[0].fullName).toBe('Khách 1');
    expect(payload.campusVisits[0].hostSelection).toBeNull();
    expect(onSuccess).toHaveBeenCalledWith(mockCreateResponse, expect.anything());
  });

  it('AUTHENTICATED SELF: posts the flat v2 create contract directly (no OTP)', async () => {
    vi.mocked(createVisitRequestV2).mockResolvedValue(mockCreateResponse as never);

    const { result, onSuccess } = setup('authenticated');
    act(() => {
      result.current.form.reset(validValues());
    });

    await act(async () => {
      await result.current.onSubmit();
    });


    expect(createVisitRequestV2).toHaveBeenCalledTimes(1);
    expect(initiateVisitRequestV2).not.toHaveBeenCalled();
    const [payload] = vi.mocked(createVisitRequestV2).mock.calls[0];
    expect(payload.registrant.email).toBe('reg@example.com');
    expect(payload.campusVisits[0].delegationName).toBe('Đoàn A');
    expect(onSuccess).toHaveBeenCalledTimes(1);
  });

  // ── Authenticated create is self-registration ONLY (plan CanhIter3FixBug) ──────────────────────
  // There is no more delegated-authenticated path: the registrant block is profile-locked/read-only
  // in the component, so the form can never legitimately carry an email other than the signed-in
  // account's own. `onSubmit` still asserts the invariant defensively (a stale draft, a race) — and a
  // mismatch there BLOCKS with an error rather than falling through to the public OTP flow, because
  // the session cannot vouch for an address it does not own.
  it.each([
    ['a different mailbox', 'someone.else@fpt.edu.vn'],
    ['a plus-alias of the same mailbox', 'reg+delegated@example.com'],
    ['a dot-variant of the same mailbox', 'r.eg@example.com'],
  ])(
    'AUTHENTICATED identity mismatch (%s): blocks with an error — never falls back to OTP',
    async (_label, actorEmail) => {
      const { result } = setup('authenticated', actorEmail);
      act(() => {
        result.current.form.reset(validValues());
      });

      await act(async () => {
        await result.current.onSubmit();
      });

      expect(createVisitRequestV2).not.toHaveBeenCalled();
      expect(initiateVisitRequestV2).not.toHaveBeenCalled();
      expect(result.current.stage).toBe('CREATE_FAILED');
      expect(result.current.submitError).toBeTruthy();
    },
  );

  it('isSelfRegistration is false in public mode even when the addresses match', () => {
    const { result } = setup('public', 'reg@example.com');
    expect(result.current.isSelfRegistration('reg@example.com')).toBe(false);
  });

  it('isSelfRegistration never matches a signed-in account with no email on record', () => {
    const { result } = setup('authenticated', null);
    expect(result.current.isSelfRegistration('')).toBe(false);
    expect(result.current.isSelfRegistration('reg@example.com')).toBe(false);
  });

  it('invalid form → no API call, first broken campus index exposed for expand/focus', async () => {
    const { result } = setup('authenticated');
    const values = validValues();
    values.campusVisits[0].endDatetime = values.campusVisits[0].startDatetime; // zero duration
    act(() => {
      result.current.form.reset(values);
    });

    await act(async () => {
      await result.current.onSubmit();
    });

    expect(createVisitRequestV2).not.toHaveBeenCalled();
    expect(result.current.firstErrorCampusIndex).toBe(0);
  });

  // ── Short-notice floor (PEMS_INTERNAL_SELF_CREATE_SHORT_NOTICE_72H plan; 72h fix CanhIter3FixBug)
  // Authenticated create is self-registration always, so the floor is now a plain synchronous
  // function of (mode, isInternalActor) alone — no form state to watch, nothing to desync, nothing
  // that needs `waitFor`. This is the fix for the reported bug: the floor used to be a `useState` kept
  // in sync by a `form.watch('registerInfo.email')` effect, and a WHOLE-OBJECT `form.setValue
  // ('registerInfo', {...})` (exactly what the profile autofill did) reports its `name` as
  // `'registerInfo'`, not `'registerInfo.email'` — so the watcher's guard never matched and the floor
  // stayed stuck at 72 until something else forced a `form.reset` (which is why a hard refresh, after
  // a draft-restore reset ran, appeared to "fix" it).

  it('internal actor (Staff/Staff Leader) in authenticated mode gets minAdvanceHours 0 immediately — no interaction needed', () => {
    const { result } = setup('authenticated', 'reg@example.com', true);
    expect(result.current.minAdvanceHours).toBe(0);
  });

  it('Visitor in authenticated mode keeps the 72h floor', () => {
    const { result } = setup('authenticated', 'reg@example.com', false);
    expect(result.current.minAdvanceHours).toBe(72);
  });

  it('public mode always keeps the 72h floor, even for an internal actor', () => {
    const { result } = setup('public', 'reg@example.com', true);
    expect(result.current.minAdvanceHours).toBe(72);
  });

  it('a whole-object registerInfo write (the profile hydration pattern) cannot desync the floor', () => {
    const { result } = setup('authenticated', 'reg@example.com', true);
    act(() => {
      result.current.form.setValue('registerInfo', {
        fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'TP',
        phone: '', email: 'reg@example.com', nationality: 'VN',
      });
    });
    // The floor never depended on this field in the first place, so there is nothing left to desync.
    expect(result.current.minAdvanceHours).toBe(0);
  });
});
