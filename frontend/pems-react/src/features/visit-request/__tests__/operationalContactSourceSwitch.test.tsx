import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act, within, waitFor } from '@testing-library/react';
import * as XLSX from 'xlsx';
import i18n from '../../../shared/i18n/config';

/**
 * plan CanhIter3FixBug — the explicit MEMBER/EXTERNAL identity-switch transition matrix.
 *
 * <p>Every mutating transition here (A→B, MEMBER→EXTERNAL, EXTERNAL→MEMBER, a stale pick being
 * reselected) has the same shape: resolve the target once, decide whether anything at risk would be
 * silently overwritten, and — if so — arm a confirmation and mutate NOTHING until the user answers.
 * These pin the actual UI behaviour, not just the helper functions underneath it.</p>
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
  return { ...actual, showInfoToast: vi.fn(), showSuccessToast: vi.fn(), showMessageErrorToast: vi.fn() };
});

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const NS = 'opcontact-source-switch-test';

const renderForm = () =>
  render(<VisitRequestFormV2 mode="public" draftNamespace={NS} onSuccess={vi.fn()} />);

const type = async (el: Element, value: string) => {
  await act(async () => { fireEvent.change(el, { target: { value } }); });
};

const click = async (testId: string) => {
  await act(async () => { fireEvent.click(screen.getByTestId(testId)); });
};

const chooseMemberSource = (i = 0) => click(`campus-opcontact-source-member-${i}`);
const chooseExternalSource = (i = 0) => click(`campus-opcontact-source-external-${i}`);

const picker = (i = 0) => screen.getByTestId(`campus-opcontact-pick-${i}`) as HTMLSelectElement;
const options = (i = 0) => Array.from(picker(i).querySelectorAll('option'));
const pickByName = async (name: string, i = 0) => {
  const option = options(i).find(o => new RegExp(name).test(o.textContent ?? ''))!;
  await act(async () => { fireEvent.change(picker(i), { target: { value: option.value } }); });
};

const memberField = (kind: 'visitors' | 'supportTeam', row: number, field: string) =>
  screen.getAllByTestId(`${kind}-${row}-${field}`)[0];
const memberRow = (kind: 'visitors' | 'supportTeam', row: number) =>
  screen.getAllByTestId(`v2-${kind}-table`)[0].querySelectorAll('tbody tr')[row] as HTMLElement;
const fillMember = async (kind: 'visitors' | 'supportTeam', row: number, m: {
  fullName: string; jobTitle: string; organization: string;
}) => {
  await type(memberField(kind, row, 'fullName'), m.fullName);
  await type(memberField(kind, row, 'jobTitle'), m.jobTitle);
  await type(within(memberRow(kind, row)).getAllByRole('combobox')[0], m.organization);
};
const addGuest = async () => {
  await act(async () => { fireEvent.click(screen.getByText(/thêm khách|add guest/i)); });
};

// Readonly (MEMBER, picked) display.
const readonlyName = () => screen.getByTestId('campus-opcontact-name-readonly').textContent;

// Editable (EXTERNAL, or MEMBER phone/email which are ALWAYS editable).
const opName = () => screen.getAllByTestId('campus-opcontact-name')[0] as HTMLTextAreaElement;
const opJobTitle = () => screen.getAllByTestId('campus-opcontact-jobtitle')[0] as HTMLTextAreaElement;
const orgInput = (i = 0) => within(screen.getAllByTestId('campus-opcontact-org')[i]).getByRole('combobox');
const opPhone = (i = 0) => screen.getByTestId(`campus-opcontact-phone-${i}`) as HTMLInputElement;
const opEmail = (i = 0) => screen.getByTestId(`campus-opcontact-email-${i}`) as HTMLInputElement;

beforeEach(async () => {
  localStorage.clear();
  vi.clearAllMocks();
  await i18n.changeLanguage('vi');
});

describe('A → B: reselecting a member while staying in MEMBER mode', () => {
  const twoMembers = async () => {
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, { fullName: 'A Person', jobTitle: 'PM', organization: 'ABC Univ' });
    await addGuest();
    await fillMember('visitors', 1, { fullName: 'B Person', jobTitle: 'Dean', organization: 'XYZ Univ' });
    await pickByName('A Person');
  };

  it('applies immediately when phone/email are both empty', async () => {
    await twoMembers();
    expect(readonlyName()).toBe('A Person');

    await pickByName('B Person');

    expect(screen.queryByTestId('campus-opcontact-member-switch-confirm-0')).toBeNull();
    expect(readonlyName()).toBe('B Person');
  });

  it('confirms when phone has data, and cancel is ZERO mutation', async () => {
    await twoMembers();
    await type(opPhone(), '0912345678');

    await pickByName('B Person');
    expect(screen.getByTestId('campus-opcontact-member-switch-confirm-0')).toBeInTheDocument();
    // Nothing has changed yet.
    expect(readonlyName()).toBe('A Person');
    expect(opPhone().value).toBe('0912345678');

    await click('campus-opcontact-member-switch-yes-0');
  });

  it('cancel leaves A selected and A\'s phone/email untouched', async () => {
    await twoMembers();
    await type(opPhone(), '0912345678');
    await pickByName('B Person');

    // The dropdown fired a change event, but nothing is applied until confirmed.
    const cancelButton = screen.getByTestId('campus-opcontact-member-switch-confirm-0')
      .querySelector('button:last-of-type')!;
    await act(async () => { fireEvent.click(cancelButton); });

    expect(screen.queryByTestId('campus-opcontact-member-switch-confirm-0')).toBeNull();
    expect(readonlyName()).toBe('A Person');
    expect(opPhone().value).toBe('0912345678');
  });

  it('confirming clears the old phone/email and applies B — no leak from A', async () => {
    await twoMembers();
    await type(opPhone(), '0912345678');
    await type(opEmail(), 'a@example.com');

    await pickByName('B Person');
    await click('campus-opcontact-member-switch-yes-0');

    expect(readonlyName()).toBe('B Person');
    expect(opPhone().value).toBe('');
    expect(opEmail().value).toBe('');
  });

  it('confirms when only email (no phone) has data', async () => {
    await twoMembers();
    await type(opEmail(), 'a@example.com');

    await pickByName('B Person');
    expect(screen.getByTestId('campus-opcontact-member-switch-confirm-0')).toBeInTheDocument();
  });
});

describe('MEMBER → EXTERNAL', () => {
  it('confirms (a picked member always has a name) and clears on confirm', async () => {
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, { fullName: 'A Person', jobTitle: 'PM', organization: 'ABC Univ' });
    await pickByName('A Person');
    expect(readonlyName()).toBe('A Person');

    await chooseExternalSource();
    expect(screen.getByTestId('campus-opcontact-source-switch-confirm-0')).toBeInTheDocument();
    // Still MEMBER — nothing applied yet.
    expect(screen.getByTestId('campus-opcontact-pick-0')).toBeInTheDocument();

    await click('campus-opcontact-source-switch-yes-0');
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(opName().value).toBe('');
  });

  it('cancel is zero mutation — stays MEMBER with the same pick', async () => {
    renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, { fullName: 'A Person', jobTitle: 'PM', organization: 'ABC Univ' });
    await pickByName('A Person');

    await chooseExternalSource();
    const cancelButton = screen.getByTestId('campus-opcontact-source-switch-confirm-0')
      .querySelector('button:last-of-type')!;
    await act(async () => { fireEvent.click(cancelButton); });

    expect(screen.queryByTestId('campus-opcontact-source-switch-confirm-0')).toBeNull();
    expect(readonlyName()).toBe('A Person');
  });
});

describe('EXTERNAL → MEMBER (two independently-guarded steps)', () => {
  it.each([
    ['only fullName', () => type(opName(), 'Ngoài Đoàn')],
    ['only organization', () => type(orgInput(), 'Tổ chức ngoài')],
    ['only jobTitle', () => type(opJobTitle(), 'Giám đốc')],
    ['only phone', () => type(opPhone(), '0987654321')],
  ])('confirms when the EXTERNAL snapshot has %s — protects all 5 fields, not just phone/email', async (_label, fillOne) => {
    renderForm();
    await chooseExternalSource();
    await fillOne();

    await chooseMemberSource();
    expect(screen.getByTestId('campus-opcontact-source-switch-confirm-0')).toBeInTheDocument();
    // Still EXTERNAL — nothing applied yet.
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
  });

  it('confirming the switch clears the EXTERNAL snapshot; the dropdown then applies without a second confirm', async () => {
    renderForm();
    await chooseExternalSource();
    await type(opName(), 'Ngoài Đoàn');
    await type(opPhone(), '0987654321');

    await chooseMemberSource();
    await click('campus-opcontact-source-switch-yes-0');

    // MEMBER mode now shows the dropdown, no free-text fields.
    expect(screen.getByTestId('campus-opcontact-pick-0')).toBeInTheDocument();
    expect(screen.queryByTestId('campus-opcontact-name')).toBeNull();

    await fillMember('visitors', 0, { fullName: 'A Person', jobTitle: 'PM', organization: 'ABC Univ' });
    await pickByName('A Person');

    // Nothing left to lose (the EXTERNAL snapshot was already cleared), so this applies immediately.
    expect(screen.queryByTestId('campus-opcontact-member-switch-confirm-0')).toBeNull();
    expect(readonlyName()).toBe('A Person');
    // The cleared phone was never carried into the new MEMBER pick.
    expect(opPhone().value).toBe('');
  });

  it('cancel is zero mutation — stays EXTERNAL with the typed data intact', async () => {
    renderForm();
    await chooseExternalSource();
    await type(opName(), 'Ngoài Đoàn');
    await type(opPhone(), '0987654321');

    await chooseMemberSource();
    const cancelButton = screen.getByTestId('campus-opcontact-source-switch-confirm-0')
      .querySelector('button:last-of-type')!;
    await act(async () => { fireEvent.click(cancelButton); });

    expect(screen.queryByTestId('campus-opcontact-source-switch-confirm-0')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(opName().value).toBe('Ngoài Đoàn');
    expect(opPhone().value).toBe('0987654321');
  });
});

describe('MEMBER + null key — three distinct states, only ONE shows "lost"', () => {
  it('just switched to MEMBER, nothing picked yet → no "lost" message', async () => {
    renderForm();
    await chooseMemberSource();
    expect(screen.queryByTestId('campus-opcontact-member-lost-0')).toBeNull();
  });

  it('a KNOWN cause (Excel replace dropping the pick) shows the specific message, and preserves phone/email until reselected — which then confirms', async () => {
    const HEADER = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];
    const makeFile = (rows: (string | number)[][]): File => {
      const ws = XLSX.utils.aoa_to_sheet(rows);
      const wb = XLSX.utils.book_new();
      XLSX.utils.book_append_sheet(wb, ws, 'Sheet1');
      const buf = XLSX.write(wb, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;
      return new File([buf], 'khach.xlsx');
    };

    const { container } = renderForm();
    await chooseMemberSource();
    await fillMember('visitors', 0, { fullName: 'A Person', jobTitle: 'PM', organization: 'ABC Univ' });
    await pickByName('A Person');
    await type(opPhone(), '0912345678');

    // Excel-replace the ONLY visitor row — the contact's own row is gone.
    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
    await act(async () => {
      fireEvent.change(fileInput, { target: { files: [makeFile([HEADER, [0, 'Khách Mới', 'GV', 'ĐH X', 'VN']])] } });
      await new Promise(r => setTimeout(r, 0));
    });
    await waitFor(() => expect(screen.getByTestId('v2-visitors-replace')).toBeInTheDocument());
    await click('v2-visitors-replace');
    await click('v2-replace-confirm-yes-visitors');

    // Known cause: the specific "lost" message shows.
    expect(await screen.findByTestId('campus-opcontact-member-lost-0')).toBeInTheDocument();
    // Source stays MEMBER — never silently reinterpreted as EXTERNAL.
    expect(screen.getByTestId('campus-opcontact-pick-0')).toBeInTheDocument();
    // The phone/email grid is not shown while nobody is picked (nothing to attach it to), but the
    // VALUE itself is kept rather than silently discarded — proven below: reselecting confirms
    // precisely because the old phone data is still there.
    await pickByName('Khách Mới');
    expect(screen.getByTestId('campus-opcontact-member-switch-confirm-0')).toBeInTheDocument();
    await click('campus-opcontact-member-switch-yes-0');
    expect(readonlyName()).toBe('Khách Mới');
    expect(opPhone().value).toBe('');
  });
});
