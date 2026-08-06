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
  }],
});

const mockCreateResponse = {
  visitRequestId: 1,
  requestCode: 'VR-001',
  visitScope: 'SINGLE_CAMPUS',
  hasMixedCampusDetails: false,
  instances: [{ visitInstanceId: 10, campusId: 1, status: 'PENDING' }],
  pendingConfirmations: 0,
  idempotent: false,
};

describe('useVisitRequestFormV2', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * `currentUserEmail` decides the submit contract in authenticated mode, so it defaults to the
   * registrant address in `validValues()` — i.e. self-registration unless a test says otherwise.
   */
  const setup = (
    mode: 'public' | 'authenticated' = 'public',
    currentUserEmail: string | null = 'reg@example.com',
  ) => {
    const onSuccess = vi.fn();
    const view = renderHook(() => useVisitRequestFormV2(onSuccess, undefined, { mode, currentUserEmail }));
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

  it.each([
    ['a different mailbox', 'someone.else@fpt.edu.vn'],
    ['a plus-alias of the same mailbox', 'reg+delegated@example.com'],
    ['a dot-variant of the same mailbox', 'r.eg@example.com'],
  ])(
    'AUTHENTICATED DELEGATED (%s): never direct-creates — it mints an OTP challenge instead',
    async (_label, actorEmail) => {
      vi.mocked(initiateVisitRequestV2).mockResolvedValue({
        sessionToken: 'sess-deleg', message: 'ok', maskedEmail: 'r***@example.com', expiresAt: '',
        maxAttempts: 5, resendAfterSeconds: 60,
      } as never);

      const { result } = setup('authenticated', actorEmail);
      act(() => {
        result.current.form.reset(validValues());
      });

      await act(async () => {
        await result.current.onSubmit();
      });

      // The session cannot vouch for an address it does not own, so the direct contract is never used.
      expect(createVisitRequestV2).not.toHaveBeenCalled();
      expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);
      await waitFor(() => expect(result.current.sessionToken).toBe('sess-deleg'));
    },
  );

  it('AUTHENTICATED DELEGATED: the created request carries no processing intent', async () => {
    vi.mocked(initiateVisitRequestV2).mockResolvedValue({
      sessionToken: 'sess-deleg', message: 'ok', maskedEmail: 'r***@example.com', expiresAt: '',
      maxAttempts: 5, resendAfterSeconds: 60,
    } as never);
    vi.mocked(verifyAndCreateVisitRequestV2).mockResolvedValue(mockCreateResponse as never);

    const { result, onSuccess } = setup('authenticated', 'someone.else@fpt.edu.vn');
    act(() => {
      result.current.form.reset(validValues());
    });

    await act(async () => {
      await result.current.onSubmit();
    });
    await waitFor(() => expect(result.current.sessionToken).toBe('sess-deleg'));

    await act(async () => {
      await result.current.verifyOtp('123456');
    });

    const [payload] = vi.mocked(verifyAndCreateVisitRequestV2).mock.calls[0];
    // The backend rejects a delegated payload that carries one, so it must never be built.
    expect(payload.campusVisits[0].hostSelection).toBeNull();
    expect(onSuccess).toHaveBeenCalledWith(mockCreateResponse, expect.anything());
  });

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
});
