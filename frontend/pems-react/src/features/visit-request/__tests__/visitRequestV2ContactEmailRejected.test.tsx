import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, waitFor } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';
import viErrors from '../../../shared/i18n/locales/vi/errors.json';
import enErrors from '../../../shared/i18n/locales/en/errors.json';
import { createEmptyCampusVisit } from '../utils/visitRequestV2Form';
import { saveVisitRequestV2Draft } from '../utils/visitRequestV2DraftStorage';
import type { VisitRequestV2Schema } from '../schema/visitRequestV2.schema';

/**
 * "Đầu mối của đoàn → Email" was refused, and the form has to say so ON THAT BOX.
 *
 * The refusal is a real business rule — the person a campus will call on the day must be an external
 * guest or partner — but the way it arrived made it unusable. The address is rejected by the SERVER
 * (only it knows what an address belongs to), and the answer used to reach the user as one sentence
 * under the submit button: it named no field, and on a request with three campuses it named no
 * campus either. The sentence itself then told the user to sign in to the internal portal and use
 * "Tạo đoàn khách", which does not make an address eligible to be the delegation's contact.
 *
 * What these pin: the exact wording, the exact input it lands on, that the OTHER campuses are left
 * alone, and that it behaves like every other error on this form — counted in the summary, and gone
 * the moment the address is changed.
 */

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [
      { campusCode: 'HN', campusName: 'Hòa Lạc', campusId: 1 },
      { campusCode: 'DN', campusName: 'Đà Nẵng', campusId: 2 },
      { campusCode: 'HCM', campusName: 'Hồ Chí Minh', campusId: 3 },
    ],
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

import { initiateVisitRequestV2, verifyAndCreateVisitRequestV2 } from '../api/visitRequestV2Api';
import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const NS = 'contact-email-rejected';

/** The wording that was agreed for a delegation contact the system will not accept. */
const VI_MESSAGE =
  'Không thể sử dụng email này cho đầu mối của đoàn. Vui lòng nhập email khác của khách hoặc đối tác bên ngoài.';
const EN_MESSAGE =
  'This email cannot be used as the delegation contact. Please enter a different email belonging to an external guest or partner.';

const futureAt = (extraMs = 0): string => {
  const d = new Date(Date.now() + 200 * 3600 * 1000 + extraMs);
  const p = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}T${p(d.getHours())}:${p(d.getMinutes())}`;
};

const campus = (code: string, contactEmail: string) => ({
  ...createEmptyCampusVisit(`ck-${code}`),
  campus: code,
  startDatetime: futureAt(),
  endDatetime: futureAt(3 * 3600 * 1000),
  delegationName: `Đoàn ${code}`,
  visitType: 'MEETING' as const,
  purpose: 'Trao đổi hợp tác',
  workingContent: 'Ký kết biên bản ghi nhớ',
  visitors: [{ fullName: 'Khách Một', jobTitle: 'Giảng viên', organization: 'ĐH X', nationality: 'Việt Nam' }],
  operationalContact: {
    fullName: 'Đầu Mối', organization: 'ĐH X', jobTitle: 'Chuyên viên',
    phone: '+84911111111', email: contactEmail,
  },
});

/** Three campuses; only the middle one names an address the server will refuse. */
const threeCampuses = (): VisitRequestV2Schema => ({
  registerInfo: {
    fullName: 'Người ĐK', organization: 'ĐH X', jobTitle: 'Trưởng phòng',
    phone: '+84912345678', email: 'reg@example.com', nationality: 'Việt Nam',
  },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits: [
    campus('HN', 'ok1@example.com'),
    campus('DN', 'staff@fpt.edu.vn'),
    campus('HCM', 'ok3@example.com'),
  ],
});

/** What the backend answers for a contact address that may not hold that role (initiate, 400). */
const contactRejected = (campusIndexes: number[]) => Object.assign(new Error('contact refused'), {
  isAxiosError: true,
  response: {
    status: 400,
    data: {
      success: false,
      errorCode: 'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT',
      // The server's own sentence. The client is expected to render the localized text for the CODE
      // instead of this — the user picks the language, and the server does not know which.
      message: VI_MESSAGE,
      errors: Object.fromEntries(campusIndexes.map(i =>
        [`CampusVisits[${i}].OperationalContact.Email`, [VI_MESSAGE]])),
    },
  },
});

/** The registrant refusal: a stable code, but no field dictionary to put it in. */
const registrantRejected = () => Object.assign(new Error('registrant refused'), {
  isAxiosError: true,
  response: {
    status: 409,
    data: {
      success: false,
      errorCode: 'REGISTRANT_EMAIL_BELONGS_TO_INTERNAL_ACCOUNT',
      message: 'Không thể sử dụng email này cho người đăng ký. Vui lòng nhập email khác của khách hoặc đối tác bên ngoài.',
    },
  },
});

const renderWithValidForm = async (values: VisitRequestV2Schema = threeCampuses()) => {
  saveVisitRequestV2Draft(values, undefined, NS);
  const view = render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);
  await act(async () => { fireEvent.click(await screen.findByTestId('v2-draft-restore')); });
  return view;
};

const submit = () => act(async () => { fireEvent.click(screen.getByTestId('v2-submit')); });

/** The message rendered under campus card `index`'s contact email input, if any. */
const contactEmailError = (index: number): string | null => {
  const input = screen.getByTestId(`campus-opcontact-email-${index}`);
  const field = input.closest('[data-field-error="true"]');
  return field?.querySelector('p')?.textContent ?? null;
};

const summary = () => screen.queryByTestId('v2-error-summary');

beforeEach(async () => {
  localStorage.clear();
  sessionStorage.clear();
  vi.clearAllMocks();
  await i18n.changeLanguage('vi');
});

describe('the agreed wording is what the codes resolve to', () => {
  it('says exactly that, in Vietnamese, for both refusals about the delegation contact', () => {
    expect(viErrors.api.CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT).toBe(VI_MESSAGE);
    expect(viErrors.api.INTERNAL_REGISTRANT_CANNOT_BE_CONTACT).toBe(VI_MESSAGE);
  });

  it('has the English counterpart for both', () => {
    expect(enErrors.api.CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT).toBe(EN_MESSAGE);
    expect(enErrors.api.INTERNAL_REGISTRANT_CANNOT_BE_CONTACT).toBe(EN_MESSAGE);
  });

  /**
   * The old sentence sent the user to the internal portal to use "Tạo đoàn khách". Signing in there
   * does not make an account eligible to be a delegation's contact, so no code in this flow may
   * resolve to that advice again — nor to anything naming a role, an account type or an id.
   */
  it('never sends the user to the internal portal, and never names the account behind the address', () => {
    const flowCodes = [
      'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT',
      'INTERNAL_REGISTRANT_CANNOT_BE_CONTACT',
      'REGISTRANT_EMAIL_BELONGS_TO_INTERNAL_ACCOUNT',
      'VISITOR_ACCOUNT_INACTIVE',
    ] as const;
    const api = viErrors.api as Record<string, string>;
    for (const code of flowCodes) {
      const message = api[code];
      expect(message, code).toBeTruthy();
      expect(message, code).not.toMatch(/cổng nội bộ|Tạo đoàn khách|nội bộ FPTU/i);
      expect(message, code).not.toMatch(/STAFF|ADMIN|LEADER|VISITOR|user[_ ]?id|partner[_ ]?id/i);
    }
  });
});

describe('a refused delegation-contact address lands on that campus\'s own email box', () => {
  it('puts the agreed message under Campus 2 and leaves Campus 1 and 3 untouched', async () => {
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(contactRejected([1]));
    await renderWithValidForm();

    await submit();

    await waitFor(() => expect(contactEmailError(1)).toBe(VI_MESSAGE));
    expect(contactEmailError(0)).toBeNull();
    expect(contactEmailError(2)).toBeNull();
    // One field is wrong, and the summary says so — not the backend's paragraph.
    expect(summary()).toHaveTextContent('Còn 1 trường cần kiểm tra.');
  });

  it('shows it in English when the form is in English, not the server\'s Vietnamese', async () => {
    await act(async () => { await i18n.changeLanguage('en'); });
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(contactRejected([1]));
    await renderWithValidForm();

    await submit();

    await waitFor(() => expect(contactEmailError(1)).toBe(EN_MESSAGE));
  });

  it('opens and focuses the campus card that holds it, rather than leaving it to be found', async () => {
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(contactRejected([1]));
    await renderWithValidForm();

    await submit();
    await act(async () => { await new Promise(r => setTimeout(r, 120)); });

    const input = screen.getByTestId('campus-opcontact-email-1');
    expect(input.closest('.hidden')).toBeNull();      // the card was expanded
    expect(document.activeElement).toBe(input);       // …and the caret is in the box
  });

  it('marks every campus that named the same address, and only those', async () => {
    const shared = threeCampuses();
    shared.campusVisits[2].operationalContact.email = 'staff@fpt.edu.vn';
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(contactRejected([1, 2]));
    await renderWithValidForm(shared);

    await submit();

    await waitFor(() => expect(contactEmailError(1)).toBe(VI_MESSAGE));
    expect(contactEmailError(2)).toBe(VI_MESSAGE);
    expect(contactEmailError(0)).toBeNull();
    expect(summary()).toHaveTextContent('Còn 2 trường cần kiểm tra.');
  });

  it('clears as soon as a different address is typed — no second submit needed', async () => {
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(contactRejected([1]));
    await renderWithValidForm();
    await submit();
    await waitFor(() => expect(contactEmailError(1)).toBe(VI_MESSAGE));

    await act(async () => {
      fireEvent.change(screen.getByTestId('campus-opcontact-email-1'), {
        target: { value: 'guest@partner.example.com' },
      });
    });

    await waitFor(() => expect(contactEmailError(1)).toBeNull());
    expect(summary()).toBeNull();
  });

  it('re-sends from the corrected form when submitted again', async () => {
    vi.mocked(initiateVisitRequestV2).mockRejectedValueOnce(contactRejected([1]));
    await renderWithValidForm();
    await submit();
    await waitFor(() => expect(contactEmailError(1)).toBe(VI_MESSAGE));

    vi.mocked(initiateVisitRequestV2).mockResolvedValue({
      sessionToken: 'token-1', maskedEmail: 'r***@example.com', resendAfterSeconds: 60, maxAttempts: 5,
    } as never);
    await act(async () => {
      fireEvent.change(screen.getByTestId('campus-opcontact-email-1'), {
        target: { value: 'guest@partner.example.com' },
      });
    });
    await submit();

    expect(initiateVisitRequestV2).toHaveBeenCalledTimes(2);
    const secondCall = vi.mocked(initiateVisitRequestV2).mock.calls[1][0] as {
      campusVisits: Array<{ operationalContact: { email: string } }>;
    };
    expect(secondCall.campusVisits[1].operationalContact.email).toBe('guest@partner.example.com');
    expect(await screen.findByTestId('otp-confirm')).toBeTruthy();
  });
});

describe('a refusal about the registrant address', () => {
  /**
   * This one arrives as a code with no field dictionary — there is only one registrant, so the
   * server has nothing to disambiguate. It still belongs on that input rather than in the banner.
   */
  it('lands on the registrant email box instead of the submit banner', async () => {
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(registrantRejected());
    await renderWithValidForm();

    await submit();

    const field = screen.getByTestId('v2-registrant-email').closest('[data-field-error="true"]');
    await waitFor(() => expect(field?.querySelector('p')?.textContent)
      .toBe('Không thể sử dụng email này cho người đăng ký. Vui lòng nhập email khác của khách hoặc đối tác bên ngoài.'));
    expect(summary()).toHaveTextContent('Còn 1 trường cần kiểm tra.');
    // No second copy of the sentence under the button, and nothing about internal portals.
    expect(document.body.textContent).not.toMatch(/cổng nội bộ|Tạo đoàn khách/);
  });

  /**
   * Same refusal, but arriving at the VERIFY step — the code the user typed was right, and the
   * address is what the server refused. It used to be shown inside the OTP modal, which reads as
   * "your code was wrong" and sends the user back to their inbox for something no code can fix.
   */
  it('closes the OTP modal and puts it on the field when it arrives at verify', async () => {
    vi.mocked(initiateVisitRequestV2).mockResolvedValue({
      sessionToken: 'token-1', maskedEmail: 'r***@example.com', resendAfterSeconds: 60, maxAttempts: 5,
    } as never);
    vi.mocked(verifyAndCreateVisitRequestV2).mockRejectedValue(registrantRejected());
    await renderWithValidForm();
    await submit();

    const codeBox = (await screen.findByPlaceholderText('______')) as HTMLInputElement;
    await act(async () => { fireEvent.change(codeBox, { target: { value: '135790' } }); });
    await act(async () => { fireEvent.click(screen.getByTestId('otp-confirm')); });

    await waitFor(() => expect(screen.queryByTestId('otp-confirm')).toBeNull());
    const field = screen.getByTestId('v2-registrant-email').closest('[data-field-error="true"]');
    expect(field?.querySelector('p')?.textContent)
      .toBe('Không thể sử dụng email này cho người đăng ký. Vui lòng nhập email khác của khách hoặc đối tác bên ngoài.');
  });

  it('clears when the registrant address is changed', async () => {
    vi.mocked(initiateVisitRequestV2).mockRejectedValue(registrantRejected());
    await renderWithValidForm();
    await submit();
    const field = () => screen.getByTestId('v2-registrant-email').closest('[data-field-error="true"]');
    await waitFor(() => expect(field()).not.toBeNull());

    await act(async () => {
      fireEvent.change(screen.getByTestId('v2-registrant-email'), { target: { value: 'guest@example.com' } });
    });

    await waitFor(() => expect(field()).toBeNull());
    expect(summary()).toBeNull();
  });
});
