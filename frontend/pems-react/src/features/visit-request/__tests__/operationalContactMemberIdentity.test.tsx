import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, within } from '@testing-library/react';
import i18n from '../../../shared/i18n/config';

/**
 * NP-03 — "Đầu mối của đoàn" has a STABLE identity.
 *
 * <p>Four things were wrong, and they compound. The picker offered guests only, so an interpreter
 * travelling with the delegation could not be named at all. It offered rows with no name yet, so the
 * contact could be "Khách 1 — chưa có tên". It copied the member's fields ONCE, at the moment of
 * picking, so everything typed into that row afterwards never reached the contact. And it remembered
 * the choice by ARRAY POSITION, so adding, removing or reordering a row above the chosen one re-aimed
 * the contact at somebody else with nothing on screen to show for it.</p>
 *
 * <p>These exercise the real form, so what they pin is what the user gets, not what a helper returns.</p>
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
const showMessageErrorToast = vi.fn();
vi.mock('../../../shared/utils/toast', async importOriginal => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return {
    ...actual,
    showInfoToast: vi.fn(),
    showSuccessToast: vi.fn(),
    showMessageErrorToast: (m: string) => showMessageErrorToast(m),
  };
});

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const NS = 'opcontact-member-test';

const renderForm = () =>
  render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);

const type = async (el: Element, value: string) => {
  await act(async () => { fireEvent.change(el, { target: { value } }); });
};

const picker = (i = 0) => screen.getByTestId(`campus-opcontact-pick-${i}`) as HTMLSelectElement;
const options = (i = 0) => Array.from(picker(i).querySelectorAll('option'));

/** The explicit "Người trong đoàn" answer (plan CanhIter3FixBug) — the dropdown only renders after this. */
const chooseMemberSource = async (i = 0) => {
  await act(async () => { fireEvent.click(screen.getByTestId(`campus-opcontact-source-member-${i}`)); });
};

/** The explicit "Người không đi cùng đoàn" answer — reveals the 5 free-text fields. */
const chooseExternalSource = async (i = 0) => {
  await act(async () => { fireEvent.click(screen.getByTestId(`campus-opcontact-source-external-${i}`)); });
};

/** One member row's field, addressed the way the card renders it (desktop table). */
const memberField = (kind: 'visitors' | 'supportTeam', row: number, field: string) =>
  screen.getAllByTestId(`${kind}-${row}-${field}`)[0];

const memberRow = (kind: 'visitors' | 'supportTeam', row: number) =>
  screen.getAllByTestId(`v2-${kind}-table`)[0].querySelectorAll('tbody tr')[row] as HTMLElement;

const fillMember = async (kind: 'visitors' | 'supportTeam', row: number, m: {
  fullName: string; jobTitle: string; organization: string;
}) => {
  await type(memberField(kind, row, 'fullName'), m.fullName);
  await type(memberField(kind, row, 'jobTitle'), m.jobTitle);
  // The organisation cell is the searchable combobox shared with the rest of the form; typing
  // commits the free-text value. In cell mode react-select renders its placeholder as a sibling div
  // rather than on the input, so the control is reached by role — organisation first, nationality
  // second, in column order.
  await type(within(memberRow(kind, row)).getAllByRole('combobox')[0], m.organization);
};

const addGuest = async () => {
  await act(async () => { fireEvent.click(screen.getByText(/thêm khách|add guest/i)); });
};

const addSupport = async () => {
  const buttons = screen.getAllByText(/thêm nhân sự hỗ trợ|add support/i);
  await act(async () => { fireEvent.click(buttons[buttons.length - 1]); });
};

beforeEach(async () => {
  localStorage.clear();
  vi.clearAllMocks();
  await i18n.changeLanguage('vi');
});

describe('who may be picked as the contact', () => {
  it('does not let an unfinished row be chosen, but still lists it', async () => {
    // A row nobody has filled in cannot describe a contact — the campus would be coordinated by
    // "Khách 1 — chưa có tên". Hiding it instead would leave the user hunting for a person they had
    // definitely typed in, so it is listed and disabled, and the label says which.
    renderForm();
    await chooseMemberSource();

    const blank = options().find(o => /chưa hoàn tất/i.test(o.textContent ?? ''));
    expect(blank).toBeTruthy();
    expect(blank).toBeDisabled();

    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });

    const ready = options().find(o => /Daniel Kim/.test(o.textContent ?? ''));
    expect(ready).toBeTruthy();
    expect(ready).not.toBeDisabled();
  });

  it('offers support staff travelling with the delegation, not only guests', async () => {
    // An interpreter or a coordinator is frequently exactly who the campus rings.
    renderForm();
    await chooseMemberSource();
    await addSupport();
    await fillMember('supportTeam', 0, {
      fullName: 'Nguyễn Văn A', jobTitle: 'Phiên dịch', organization: 'ABC University',
    });

    const option = options().find(o => /Nguyễn Văn A/.test(o.textContent ?? ''));
    expect(option).toBeTruthy();
    expect(option?.textContent).toMatch(/Nhân sự hỗ trợ/);
    expect(option).not.toBeDisabled();

    await act(async () => { fireEvent.change(picker(), { target: { value: option!.value } }); });
    expect(screen.getByTestId('campus-opcontact-name-readonly').textContent).toBe('Nguyễn Văn A');
  });
});

describe('the snapshot follows the member while the form is open', () => {
  it('updates the contact when the member is edited AFTER being picked', async () => {
    // The bug: the copy happened once, on pick. Users pick from a half-typed row and finish it
    // afterwards, so the request was filed naming a contact with no job title.
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'PM', organization: 'ABC University',
    });

    const option = options().find(o => /Daniel Kim/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(picker(), { target: { value: option.value } }); });

    await type(memberField('visitors', 0, 'jobTitle'), 'Program Manager');

    expect(screen.getByTestId('campus-opcontact-jobtitle-readonly').textContent).toBe('Program Manager');
    expect(screen.getByTestId('campus-opcontact-name-readonly').textContent).toBe('Daniel Kim');
  });

  it('keeps pointing at the same person when a row is added above them', async () => {
    // With an array index this is where the contact silently became the newcomer.
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });
    const option = options().find(o => /Daniel Kim/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(picker(), { target: { value: option.value } }); });

    await addGuest();
    await fillMember('visitors', 1, {
      fullName: 'Người Mới', jobTitle: 'Thành viên', organization: 'ABC University',
    });

    expect(screen.getByTestId('campus-opcontact-name-readonly').textContent).toBe('Daniel Kim');
  });

  it('does not let the snapshot be edited into a different person', async () => {
    // Bug 2: the pick was kept while the three fields stayed freely editable, so one record could
    // describe two people. Picked → the fields are the member's, shown not typed. The editable
    // free-text fields only exist at all under EXTERNAL — proven here before switching to MEMBER.
    renderForm();
    await chooseExternalSource();
    expect(screen.getAllByTestId('campus-opcontact-name').length).toBeGreaterThan(0);

    // Nothing was typed into the EXTERNAL fields yet, so switching away has nothing to confirm.
    await chooseMemberSource();
    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });
    const option = options().find(o => /Daniel Kim/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(picker(), { target: { value: option.value } }); });

    expect(screen.queryByTestId('campus-opcontact-name')).toBeNull();
    expect(screen.queryByTestId('campus-opcontact-jobtitle')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-edit-member-0')).toBeInTheDocument();
  });

  it('leaves phone and email to be typed — a member row has neither', async () => {
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });
    const option = options().find(o => /Daniel Kim/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(picker(), { target: { value: option.value } }); });

    expect(screen.getByTestId('campus-opcontact-phone-0')).toBeEnabled();
  });
});

describe('removing the person who holds the role', () => {
  it('is refused, with the reason, instead of silently re-aiming the contact', async () => {
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });
    await addGuest();
    await fillMember('visitors', 1, {
      fullName: 'Người Khác', jobTitle: 'Thành viên', organization: 'ABC University',
    });

    const option = options().find(o => /Daniel Kim/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(picker(), { target: { value: option.value } }); });

    const rows = screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr');
    const removeContactRow = within(rows[0] as HTMLElement).getByRole('button');
    await act(async () => { fireEvent.click(removeContactRow); });

    expect(showMessageErrorToast).toHaveBeenCalledWith(expect.stringContaining('đầu mối'));
    // Still two rows, and the contact is still Daniel: nothing was deleted and nothing moved.
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(2);
    expect(screen.getByTestId('campus-opcontact-name-readonly').textContent).toBe('Daniel Kim');
  });

  it('marks which row holds the role so editing it is never a surprise', async () => {
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });
    const option = options().find(o => /Daniel Kim/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(picker(), { target: { value: option.value } }); });

    expect(screen.getByTestId('visitors-0-is-contact')).toBeInTheDocument();
  });

  it('lets an ordinary member be removed', async () => {
    // The counterweight: the guard must not have been bought by breaking deletion generally.
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });
    await addGuest();
    const option = options().find(o => /Daniel Kim/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(picker(), { target: { value: option.value } }); });

    const rows = screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr');
    await act(async () => {
      fireEvent.click(within(rows[1] as HTMLElement).getByRole('button'));
    });

    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(1);
    expect(showMessageErrorToast).not.toHaveBeenCalled();
  });
});

describe('Operational Contact UI cleanup (CanhIter3FixBug §9-§14)', () => {
  it('the quick-fill button reads "Đầu mối là người đăng ký" — the old "Dùng người đăng ký" is gone', () => {
    renderForm();

    expect(screen.getByTestId('campus-opcontact-use-registrant-0')).toHaveTextContent('Đầu mối là người đăng ký');
    expect(screen.queryByText('Dùng người đăng ký')).toBeNull();
  });

  it('MEMBER mode drops the redundant "Đầu mối là ai trong đoàn?" question, its info icon and helper text', async () => {
    renderForm();
    await chooseMemberSource();

    // The dropdown itself is still there and still functional.
    expect(picker()).toBeInTheDocument();

    expect(screen.queryByText('Đầu mối là ai trong đoàn?')).toBeNull();
    expect(screen.queryByTestId('campus-opcontact-pick-help-0')).toBeNull();
    expect(screen.queryByText('Họ tên, chức vụ và đơn vị lấy theo thành viên được chọn.')).toBeNull();
  });

  it('MEMBER dropdown placeholder is "Chọn đầu mối trong đoàn", not the old "không nằm trong đoàn" wording', async () => {
    renderForm();
    await chooseMemberSource();

    const blankOption = options().find(o => o.value === '')!;
    expect(blankOption.textContent).toBe('Chọn đầu mối trong đoàn');
    expect(picker()).toHaveAccessibleName('Chọn đầu mối trong đoàn');

    // The MEMBER picker must never offer a selectable option that reads as "not in the delegation"
    // — that contradicts the radio group's own "Người trong đoàn" answer one step above it.
    expect(options().some(o => /không nằm trong danh sách đoàn/i.test(o.textContent ?? ''))).toBe(false);
  });

  it('Guest and Support members both still appear in the MEMBER picker', async () => {
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, { fullName: 'Nguyễn Văn A', jobTitle: 'GV', organization: 'ABC University' });
    await addSupport();
    await fillMember('supportTeam', 0, { fullName: 'Trần Thị B', jobTitle: 'NV', organization: 'ABC University' });

    expect(options().some(o => /Nguyễn Văn A/.test(o.textContent ?? ''))).toBe(true);
    expect(options().some(o => /Trần Thị B/.test(o.textContent ?? ''))).toBe(true);
  });
});

describe('"Dùng người đăng ký"', () => {
  // REG-MEMBER-01: registrant matches exactly one existing delegation member.
  it('selects the registrant\'s existing delegation row instead of adding a duplicate, and preserves Phone/Email', async () => {
    // §8: the same human twice — once as a member, once as a typed contact — is exactly what the
    // link exists to prevent. Matched on name + job title + organisation together; a name alone
    // would merge two different people who happen to share one.
    renderForm();
    await type(screen.getByTestId('v2-registrant-fullName'), 'Daniel Kim');
    await type(screen.getByTestId('v2-registrant-phone'), '0912345678');
    await type(screen.getByTestId('v2-registrant-email'), 'daniel@example.com');
    const registrantOrg = screen.getByPlaceholderText(/organization\/partner|tổ chức\/đối tác/i);
    await type(registrantOrg, 'ABC University');
    await type(screen.getByTestId('v2-registrant-jobTitle'), 'Program Manager');

    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });

    await act(async () => {
      fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0'));
    });

    // Picked, not typed: the contact IS the delegation row, and the list did not grow.
    expect(screen.getByTestId('campus-opcontact-picked-0')).toBeInTheDocument();
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(1);
    // §9/§10: a member row carries no phone/email — the registrant's own values must not be lost
    // just because the contact was routed through the member-link path instead of a plain copy.
    expect((screen.getByTestId('campus-opcontact-phone-0') as HTMLInputElement).value).toBe('0912345678');
    expect((screen.getByTestId('campus-opcontact-email-0') as HTMLInputElement).value).toBe('daniel@example.com');
  });

  // REG-MEMBER-02: the registrant's details match more than one existing member.
  it('shows the ambiguity warning instead of guessing, with no selection and no new row', async () => {
    renderForm();
    await type(screen.getByTestId('v2-registrant-fullName'), 'Daniel Kim');
    await type(screen.getByTestId('v2-registrant-email'), 'daniel@example.com');
    const registrantOrg = screen.getByPlaceholderText(/organization\/partner|tổ chức\/đối tác/i);
    await type(registrantOrg, 'ABC University');
    await type(screen.getByTestId('v2-registrant-jobTitle'), 'Program Manager');

    await fillMember('visitors', 0, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });
    await addGuest();
    await fillMember('visitors', 1, {
      fullName: 'Daniel Kim', jobTitle: 'Program Manager', organization: 'ABC University',
    });

    await act(async () => {
      fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0'));
    });

    expect(showMessageErrorToast).toHaveBeenCalledWith(expect.stringContaining('nhiều thành viên'));
    expect(screen.queryByTestId('campus-opcontact-picked-0')).toBeNull();
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(2);
  });

  // REG-MEMBER-03: no matching member — quick-fill copies the registrant as an EXTERNAL contact and
  // does not touch the delegation list.
  it('copies the registrant as an EXTERNAL contact when nobody matches, without adding a member yet', async () => {
    renderForm();
    await type(screen.getByTestId('v2-registrant-fullName'), 'Daniel Kim');
    await type(screen.getByTestId('v2-registrant-email'), 'daniel@example.com');
    const registrantOrg = screen.getByPlaceholderText(/organization\/partner|tổ chức\/đối tác/i);
    await type(registrantOrg, 'ABC University');
    await type(screen.getByTestId('v2-registrant-jobTitle'), 'Program Manager');

    await act(async () => {
      fireEvent.click(screen.getByTestId('campus-opcontact-use-registrant-0'));
    });

    expect((screen.getByTestId('campus-opcontact-source-external-0') as HTMLInputElement).checked).toBe(true);
    expect((screen.getAllByTestId('campus-opcontact-name')[0] as HTMLTextAreaElement).value).toBe('Daniel Kim');
    // Still exactly the one scaffolding row the campus card started with.
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(1);
  });
});
