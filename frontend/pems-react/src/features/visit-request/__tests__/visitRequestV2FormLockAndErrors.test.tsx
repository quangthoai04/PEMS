import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, waitFor } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import { saveVisitRequestV2Draft } from '../utils/visitRequestV2DraftStorage';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

/**
 * Three things a long form has to get right once the user presses submit.
 *
 * 1. The count of what is left has to be TRUE at the moment it is read. It used to be a sentence
 *    frozen into state by the failed submit — "Còn 11 trường cần kiểm tra." stayed on screen, still
 *    saying eleven, while the user fixed them one by one; only another submit could correct it.
 * 2. Nothing may be edited while the request is in flight, or the screen ends up showing data that
 *    is not what was sent, and a second click sends the whole thing twice.
 * 3. Vietnamese prose must not come back underlined in red by the browser's English dictionary on a
 *    form that marks its real errors in exactly that colour.
 */

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [{ campusCode: 'HN', campusName: 'Hòa Lạc', campusId: 1 }],
    loading: false,
  }),
}));
vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: vi.fn().mockResolvedValue([]),
    searchOrganizations: vi.fn().mockResolvedValue([]),
    initiate: vi.fn(), resendOtp: vi.fn(), recoverOtp: vi.fn(),
  },
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

import { initiateVisitRequestV2 } from '../api/visitRequestV2Api';
import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const NS = 'lock-and-errors';

/** Comfortably past the 72-hour floor, in the wall-clock shape the picker stores. */
const futureAt = (extraMs = 0): string => {
  const d = new Date(Date.now() + 200 * 3600 * 1000 + extraMs);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};

const validValues = (): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'Trưởng phòng',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'Việt Nam',
  },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits: [{
    ...createEmptyCampusVisit('ck-1'),
    campus: 'HN',
    startDatetime: futureAt(),
    endDatetime: futureAt(3 * 3600 * 1000),
    delegationName: 'Đoàn Trường ĐH X',
    visitType: 'MEETING',
    purpose: 'Trao đổi hợp tác đào tạo',
    workingContent: 'Ký kết biên bản ghi nhớ',
    visitors: [{ fullName: 'Khách Một', jobTitle: 'Giảng viên', organization: 'ĐH X', nationality: 'Việt Nam' }],
    operationalContact: {
      fullName: 'Đầu Mối', organization: 'ĐH X', jobTitle: 'Chuyên viên',
      phone: '+84911111111', email: 'dm@example.com',
    },
  }],
});

/**
 * Fills the form by restoring a saved draft rather than typing forty fields: the restore path is
 * the form's own `reset`, so the state under test is the real one, and the tests stay about
 * validation and locking instead of about data entry.
 */
const renderWithValidForm = async (values: VisitRequestV2Schema = validValues()) => {
  saveVisitRequestV2Draft(values, undefined, NS);
  const view = render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);
  await act(async () => { fireEvent.click(await screen.findByTestId('v2-draft-restore')); });
  return view;
};

const submit = async () => {
  await act(async () => { fireEvent.click(screen.getByTestId('v2-submit')); });
};

const summary = () => screen.queryByTestId('v2-error-summary');

/** The number the banner is currently claiming, or null when there is no banner. */
const errorCount = (): number | null => {
  const text = summary()?.textContent ?? '';
  const match = /(\d+)/.exec(text);
  return match ? Number(match[1]) : null;
};

const setValue = (testId: string, value: string) =>
  act(async () => { fireEvent.change(screen.getByTestId(testId), { target: { value } }); });

beforeEach(async () => {
  localStorage.clear();
  sessionStorage.clear();
  vi.clearAllMocks();
  await i18n.changeLanguage('en');
});

describe('the "N fields still need attention" banner tracks the CURRENT form', () => {
  /** Three required fields emptied on an otherwise valid form — a count that is easy to verify. */
  const breakThreeFields = async () => {
    await setValue('v2-registrant-fullName', '');
    await setValue('v2-registrant-jobTitle', '');
    await setValue('campus-delegation-input', '');
  };

  it('says nothing at all until the user has actually submitted', async () => {
    await renderWithValidForm();
    await breakThreeFields();

    // Three required fields are empty, and the form still has not accused anybody of anything.
    expect(summary()).toBeNull();
  });

  it('counts what is wrong on submit, then counts DOWN as each one is fixed', async () => {
    await renderWithValidForm();
    await breakThreeFields();

    await submit();
    expect(errorCount()).toBe(3);
    expect(initiateVisitRequestV2).not.toHaveBeenCalled();

    // Case 2 — one field fixed, one field off the count, straight away and with no second submit.
    await setValue('v2-registrant-fullName', 'Người ĐK');
    await waitFor(() => expect(errorCount()).toBe(2));

    await setValue('v2-registrant-jobTitle', 'Trưởng phòng');
    await waitFor(() => expect(errorCount()).toBe(1));

    // Case 3 — the last one goes, and the banner goes with it. Nobody had to press submit again.
    await setValue('campus-delegation-input', 'Đoàn Trường ĐH X');
    await waitFor(() => expect(summary()).toBeNull());
  });

  it('brings the banner back when a field is broken again after being fixed', async () => {
    await renderWithValidForm();
    await breakThreeFields();
    await submit();
    await setValue('v2-registrant-fullName', 'Người ĐK');
    await setValue('v2-registrant-jobTitle', 'Trưởng phòng');
    await setValue('campus-delegation-input', 'Đoàn Trường ĐH X');
    await waitFor(() => expect(summary()).toBeNull());

    // Case 4 — the count follows the data in both directions, not just downwards.
    await setValue('v2-registrant-fullName', '');
    await waitFor(() => expect(errorCount()).toBe(1));
  });

  it('re-validates from the form as it is NOW when submitted again', async () => {
    await renderWithValidForm();
    await breakThreeFields();
    await submit();
    expect(errorCount()).toBe(3);

    await setValue('v2-registrant-fullName', 'Người ĐK');
    await setValue('v2-registrant-jobTitle', 'Trưởng phòng');
    await setValue('campus-delegation-input', 'Đoàn Trường ĐH X');

    // Case 5 — the second submit judges the current values, not the ones that failed the first.
    await submit();
    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);
    expect(summary()).toBeNull();
  });

  it('drops a removed campus\'s errors from the count instead of keeping them at a stale index', async () => {
    const twoCampuses = validValues();
    twoCampuses.campusVisits.push({
      ...createEmptyCampusVisit('ck-2'),
      campus: '',                       // required, and nothing else on the card is filled in either
      startDatetime: '',
      endDatetime: '',
    });
    await renderWithValidForm(twoCampuses);

    await submit();
    const withSecondCampus = errorCount();
    expect(withSecondCampus).not.toBeNull();
    expect(withSecondCampus!).toBeGreaterThan(0);

    // Case 6 — the second card is blank, so removing it needs no confirmation and takes every one
    // of its errors with it. The first card was valid, so nothing is left to report.
    const removeButtons = screen.getAllByRole('button', { name: 'Remove this campus' });
    await act(async () => { fireEvent.click(removeButtons[removeButtons.length - 1]); });

    await waitFor(() => expect(summary()).toBeNull());
  });

  it('does the same for a guest row that is removed while it is invalid', async () => {
    const withBlankGuest = validValues();
    // A second, entirely empty guest row: four required columns, four errors, all on a row the user
    // can delete rather than fill in.
    withBlankGuest.campusVisits[0].visitors.push({
      fullName: '', jobTitle: '', organization: '', nationality: '',
    });
    await renderWithValidForm(withBlankGuest);

    await submit();
    expect(errorCount()).toBe(4);

    // Desktop table and mobile card each render a delete control for the same row, so both handles
    // for row 2 are here; either one removes it.
    const removeGuest = screen.getAllByRole('button', { name: 'Remove guest' });
    await act(async () => { fireEvent.click(removeGuest[removeGuest.length - 1]); });

    await waitFor(() => expect(summary()).toBeNull());
  });
});

describe('the whole form is locked while the submit is in flight', () => {
  /** A request that never settles on its own, so the in-flight window can be inspected. */
  const deferredInitiate = () => {
    // Captured when the call is MADE, so both handles have to be read through a closure — the
    // promise does not exist yet at the moment this helper returns.
    let settle!: (value: unknown) => void;
    let fail!: (reason: unknown) => void;
    vi.mocked(initiateVisitRequestV2).mockImplementation(
      () => new Promise((resolve, reject) => { settle = resolve; fail = reject; }) as never,
    );
    return {
      resolveWith: (v: unknown) => settle(v),
      rejectWith: (e: unknown) => fail(e),
    };
  };

  const fields = () => screen.getByTestId('v2-form-fields');

  it('disables every control — for the keyboard too — until the answer comes back', async () => {
    const deferred = deferredInitiate();
    await renderWithValidForm();

    await submit();

    // A disabled fieldset, so the lock is the browser's own and covers tabbing into a field just as
    // much as clicking one. Nothing here relies on pointer-events.
    expect(fields()).toBeDisabled();
    expect(screen.getByTestId('v2-registrant-fullName')).toBeDisabled();
    expect(screen.getByTestId('campus-delegation-input')).toBeDisabled();
    expect(screen.getByTestId('campus-opcontact-name')).toBeDisabled();
    expect(screen.getByTestId('v2-add-campus')).toBeDisabled();
    expect(screen.getByTestId('v2-visitors-import')).toBeDisabled();
    expect(screen.getByTestId('v2-submit')).toBeDisabled();
    // …and `inert` alongside it, because the organization/nationality pickers open their menu from
    // a <div>, which `disabled` on a fieldset does not reach.
    expect(fields()).toHaveAttribute('inert');

    await act(async () => { deferred.resolveWith({ sessionToken: 't', maskedEmail: 'r***@x.com', resendAfterSeconds: 60, maxAttempts: 5 }); });
  });

  it('cannot be submitted twice, however the second submit arrives', async () => {
    const deferred = deferredInitiate();
    await renderWithValidForm();

    await submit();
    // The button is disabled, so this is the case it cannot catch: a submit event on the form
    // itself (an Enter key, a stray dispatch) while the first request is still open.
    await act(async () => { fireEvent.submit(document.getElementById('visit-request-v2-form')!); });
    await submit();

    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(1);

    await act(async () => { deferred.resolveWith({ sessionToken: 't', maskedEmail: 'r***@x.com', resendAfterSeconds: 60, maxAttempts: 5 }); });
  });

  it('hands the form back — with the typing intact — when the request fails', async () => {
    const deferred = deferredInitiate();
    await renderWithValidForm();
    await submit();
    expect(fields()).toBeDisabled();

    await act(async () => {
      deferred.rejectWith(Object.assign(new Error('mail down'), { isAxiosError: true, response: { status: 500, data: {} } }));
    });

    await waitFor(() => expect(fields()).not.toBeDisabled());
    const name = screen.getByTestId('v2-registrant-fullName') as HTMLInputElement;
    expect(name).not.toBeDisabled();
    expect(name.value).toBe('Người ĐK');
    // …and it can be edited and sent again.
    await setValue('v2-registrant-fullName', 'Người ĐK 2');
    expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value).toBe('Người ĐK 2');
  });

  it('lifts the lock and moves on to the OTP step when the request succeeds', async () => {
    vi.mocked(initiateVisitRequestV2).mockResolvedValue({
      sessionToken: 'token-1', maskedEmail: 'r***@example.com',
      resendAfterSeconds: 60, maxAttempts: 5,
    } as never);
    await renderWithValidForm();

    await submit();

    expect(await screen.findByTestId('otp-confirm')).toBeTruthy();
    expect(fields()).not.toBeDisabled();
  });
});

describe('the browser spell checker is off inside the registration form', () => {
  it('marks the form itself, so every control inside it inherits the setting', async () => {
    await renderWithValidForm();
    expect(document.getElementById('visit-request-v2-form')).toHaveAttribute('spellcheck', 'false');
  });

  it('turns it off on the free-text controls themselves as well', async () => {
    await renderWithValidForm();
    // The one-line auto-grow field (delegation name, job titles, notes…) and the prose textareas
    // (purpose, working content) are where Vietnamese gets underlined word by word.
    expect(screen.getByTestId('campus-delegation-input')).toHaveAttribute('spellcheck', 'false');
    const textareas = Array.from(document.querySelectorAll('#visit-request-v2-form textarea'));
    expect(textareas.length).toBeGreaterThan(0);
    textareas.forEach(el => expect(el).toHaveAttribute('spellcheck', 'false'));
  });
});
