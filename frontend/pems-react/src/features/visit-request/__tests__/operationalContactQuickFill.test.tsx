import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, waitFor, within } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';

/**
 * Plan §21 — the per-campus operational contact.
 *
 * Two things were wrong with it. Its organization was a plain text box while every comparable field
 * in the form searched the organizations already on file, so the same body got typed three
 * different ways in one request. And there was no way to say "it is me" or "it is the request's
 * contact", which is the answer most of the time — so the same four values were retyped per campus.
 */

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [
      { campusCode: 'HN', campusName: 'Hòa Lạc', campusId: 1 },
      { campusCode: 'HCM', campusName: 'Hồ Chí Minh', campusId: 2 },
    ],
    loading: false,
  }),
}));
vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));

const searchOrganizations = vi.fn().mockResolvedValue([]);
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: vi.fn().mockResolvedValue([]),
    searchOrganizations: (kw: string) => searchOrganizations(kw),
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

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const NS = 'opcontact-test';

const renderForm = () =>
  render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);

const type = async (el: Element, value: string) => {
  await act(async () => { fireEvent.change(el, { target: { value } }); });
};

const fillRegistrant = async () => {
  await type(screen.getByTestId('v2-registrant-fullName'), 'Người Đăng Ký');
  await type(screen.getByTestId('v2-registrant-phone'), '0912345678');
  await type(screen.getByTestId('v2-registrant-email'), 'reg@example.com');
  // The registrant organization is the free-solo partner combobox.
  await type(screen.getByPlaceholderText(/organization\/partner|tổ chức\/đối tác/i), 'ĐH Nguồn');
};

const fillPrimaryContact = async (container: HTMLElement) => {
  const named = (name: string) => container.querySelector(`input[name="${name}"]`)!;
  await type(named('contactPoint.fullName'), 'Đầu Mối Chính');
  // The contact's organization is the same free-solo combobox as the registrant's and the campus
  // one — react-select owns the input, so it is reached through the wrapper's test id rather than
  // by field name. Typing alone commits the value; that is what `input-change` does here.
  await type(within(screen.getByTestId('v2-contact-org')).getByRole('combobox'), 'Tổ Chức Đầu Mối');
  await type(screen.getByTestId('v2-contact-phone'), '0987654321');
  await type(named('contactPoint.email'), 'contact@example.com');
};

/** The organization combobox for campus card `i` — react-select, reached through its wrapper. */
const orgCombobox = (i = 0) => screen.getAllByTestId('campus-opcontact-org')[i];
const orgInput = (i = 0) => orgCombobox(i).querySelector('input')!;
const orgText = (i = 0) => orgCombobox(i).textContent ?? '';

const opName = (i = 0) => screen.getAllByTestId('campus-opcontact-name')[i] as HTMLTextAreaElement;
const opPhone = (i = 0) => screen.getByTestId(`campus-opcontact-phone-${i}`) as HTMLInputElement;
const opEmail = (container: HTMLElement, i = 0) =>
  container.querySelector(`input[name="campusVisits.${i}.operationalContact.email"]`) as HTMLInputElement;

describe('the operational organization is a searchable combobox (plan §9)', () => {
  beforeEach(async () => {
    localStorage.clear();
    vi.clearAllMocks();
    searchOrganizations.mockResolvedValue([]);
    await i18n.changeLanguage('en');
  });

  it('renders the shared combobox rather than a bare text input', () => {
    renderForm();
    const combobox = orgCombobox();
    expect(combobox).toBeInTheDocument();
    // react-select renders its own input; a plain <input type="text"> named for the field would
    // mean the old control is still there.
    expect(within(combobox).getByRole('combobox')).toBeInTheDocument();
  });

  it('lets the user pick an organization already on file', async () => {
    searchOrganizations.mockResolvedValue([
      { partnerId: 7, displayName: 'Đại học Đối Tác', country: 'VN', city: 'Hà Nội' },
    ]);
    renderForm();

    await type(orgInput(), 'Đại học');
    const option = await screen.findByText('Đại học Đối Tác', {}, { timeout: 3000 });
    await act(async () => { fireEvent.click(option); });

    await waitFor(() => expect(orgText()).toContain('Đại học Đối Tác'));
  });

  it('still accepts an organization that is not on file', async () => {
    renderForm();

    await type(orgInput(), 'Một Tổ Chức Hoàn Toàn Mới');
    await act(async () => { fireEvent.blur(orgInput()); });

    await waitFor(() => expect(orgText()).toContain('Một Tổ Chức Hoàn Toàn Mới'));
  });

  it('does not repeat the request-level partner badge on this field', async () => {
    searchOrganizations.mockResolvedValue([
      { partnerId: 7, displayName: 'Đại học Đối Tác', country: 'VN', city: 'Hà Nội' },
    ]);
    renderForm();

    await type(orgInput(), 'Đại học');
    const option = await screen.findByText('Đại học Đối Tác', {}, { timeout: 3000 });
    await act(async () => { fireEvent.click(option); });

    // The big green "an existing partner is selected" badge belongs to the request's own partner
    // field alone. Repeating it here would claim this snapshot is linked to a partner record; it
    // is not, and the schema has no column for it.
    await waitFor(() => expect(orgText()).toContain('Đại học Đối Tác'));
    expect(screen.queryByText(/An existing partner is selected/i)).toBeNull();
    // A light note is fine, and is what tells the user their choice matched something known.
    expect(screen.getByTestId('campus-opcontact-org-known')).toBeInTheDocument();
  });
});

describe('quick-filling the operational contact (plan §11–§13)', () => {
  beforeEach(async () => {
    localStorage.clear();
    vi.clearAllMocks();
    searchOrganizations.mockResolvedValue([]);
    await i18n.changeLanguage('en');
  });

  it('keeps the button off until the registrant block has something worth copying', async () => {
    // Only ONE source is left: the request-level contact block it used to offer as a second option
    // no longer exists, because a request has no single contact to copy from.
    renderForm();
    expect(screen.getByTestId('campus-opcontact-use-registrant-0')).toBeDisabled();
    expect(screen.queryByTestId('campus-opcontact-use-contact-0')).not.toBeInTheDocument();

    await fillRegistrant();
    expect(screen.getByTestId('campus-opcontact-use-registrant-0')).toBeEnabled();
  });

  it('copies exactly the four contact fields from the registrant', async () => {
    const { container } = renderForm();
    await fillRegistrant();

    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0')); });

    expect(opName().value).toBe('Người Đăng Ký');
    expect(orgText()).toContain('ĐH Nguồn');
    expect(opPhone().value).toBe('0912345678');
    expect(opEmail(container).value).toBe('reg@example.com');
  });

  it('touches only the campus whose button was pressed', async () => {
    renderForm();
    await fillRegistrant();
    await act(async () => { fireEvent.click(screen.getByTestId('v2-add-campus')); });

    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0')); });

    expect(opName(0).value).toBe('Người Đăng Ký');
    expect(opName(1).value).toBe('');
  });

  it('is a one-time copy: editing afterwards changes neither side of it', async () => {
    renderForm();
    await fillRegistrant();
    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0')); });

    await type(opName(), 'Người Khác Hẳn');
    expect((screen.getByTestId('v2-registrant-fullName') as HTMLInputElement).value).toBe('Người Đăng Ký');

    // …and changing the SOURCE afterwards does not reach back into the campus.
    await type(screen.getByTestId('v2-registrant-fullName'), 'Tên Đã Sửa');
    expect(opName().value).toBe('Người Khác Hẳn');
  });

  it('asks before replacing details that are already there', async () => {
    renderForm();
    await fillRegistrant();
    await type(opName(), 'Đã Nhập Bằng Tay');

    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0')); });

    // Nothing has changed yet — the question is the point.
    expect(screen.getByTestId('campus-opcontact-replace-confirm-0')).toBeInTheDocument();
    expect(opName().value).toBe('Đã Nhập Bằng Tay');

    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-replace-yes-0')); });
    expect(opName().value).toBe('Người Đăng Ký');
  });

  it('fills straight away when there is nothing to overwrite', async () => {
    renderForm();
    await fillRegistrant();

    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0')); });

    expect(screen.queryByTestId('campus-opcontact-replace-confirm-0')).toBeNull();
    expect(opName().value).toBe('Người Đăng Ký');
  });

  it('says the copy is independent from now on', async () => {
    renderForm();
    await fillRegistrant();
    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0')); });

    expect(screen.getByTestId('campus-opcontact-copied-0')).toHaveTextContent(/independently/i);
  });

  it('survives a draft restore, organization included', async () => {
    const { unmount } = renderForm();
    await fillRegistrant();
    await act(async () => { fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0')); });
    expect(opName().value).toBe('Người Đăng Ký');

    // Let the 700ms autosave fire — this is the path a real user takes, and the point of the test
    // is that what quick-fill wrote is ordinary form state that gets persisted like anything else.
    await act(async () => { await new Promise(r => setTimeout(r, 900)); });
    unmount();

    // Re-open with the same namespace and take the offered draft.
    renderForm();
    await waitFor(() => expect(screen.getByTestId('v2-draft-prompt')).toBeInTheDocument());
    await act(async () => { fireEvent.click(screen.getByTestId('v2-draft-restore')); });

    await waitFor(() => expect(opName().value).toBe('Người Đăng Ký'));
    expect(orgText()).toContain('ĐH Nguồn');
  });
});
