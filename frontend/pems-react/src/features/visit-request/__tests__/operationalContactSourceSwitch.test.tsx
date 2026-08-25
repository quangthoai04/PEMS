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

/**
 * `renderForm()` mounts the full public VisitRequestFormV2 — every section, every validator — and
 * `twoMembers()` (the shared setup most tests here start from) then drives ~9 sequential act-wrapped
 * re-renders of that tree (two full member-row fills, add-guest, two dropdown picks). None of that is
 * async-unsafe (every mutation is awaited through `act`), it is just genuinely CPU-heavy synchronous
 * render work. Alone the file finishes in a few seconds, but vitest runs files in parallel worker
 * threads, so under a full suite run this file sits close enough to the default 5s budget that it
 * intermittently loses the scheduling race — CanhIter3FixBug closure measured it: 1/1 failures across
 * 3 full-suite runs were this same file, always a `Test timed out in 5000ms`, never an assertion
 * failure, and it passed every time run alone. `emailHtmlSanitization.test.tsx` hit the identical
 * cause and fix (see its own comment) — nothing here asserts speed, so the budget is raised rather
 * than the assertions relaxed or the setup thinned out.
 */
vi.setConfig({ testTimeout: 20_000 });

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    // A second campus so the multi-campus-isolation test below can actually add a card — with only
    // one campus available the "add campus" button is disabled once card 0 exists.
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
vi.mock('../../../shared/utils/toast', async importOriginal => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return { ...actual, showInfoToast: vi.fn(), showSuccessToast: vi.fn(), showMessageErrorToast: vi.fn() };
});

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';
import { showSuccessToast, showMessageErrorToast } from '../../../shared/utils/toast';

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

// ── Registrant-as-member UX (plan CanhIter3FixBug §3-§20) ─────────────────────────────────────

const fillRegistrant = async (m: { fullName: string; phone: string; email: string; organization: string; jobTitle: string }) => {
  await type(screen.getByTestId('v2-registrant-fullName'), m.fullName);
  await type(screen.getByTestId('v2-registrant-phone'), m.phone);
  await type(screen.getByTestId('v2-registrant-email'), m.email);
  await type(screen.getByPlaceholderText(/organization\/partner|tổ chức\/đối tác/i), m.organization);
  await type(screen.getByTestId('v2-registrant-jobTitle'), m.jobTitle);
  // Nationality is required to add the registrant as a visitor (plan §8) — filled unconditionally
  // here so every scenario below can reach the "add" flow, even the ones that do not assert on it.
  await fillRegistrantNationality('Việt Nam');
};

/** The registrant's own `CountrySelect` — located by its FormField label, since (unlike a member
 * row's nationality cell) it carries no dedicated `data-testid`. */
const registrantNationalityCombobox = (): HTMLElement => {
  const label = Array.from(document.querySelectorAll('label')).find(l => /Quốc tịch|Nationality/.test(l.textContent ?? ''))!;
  const field = label.closest('div')!.parentElement as HTMLElement;
  return within(field).getByRole('combobox');
};
const fillRegistrantNationality = async (name: string) => {
  await type(registrantNationalityCombobox(), name);
  const option = await screen.findByText(name, {}, { timeout: 3000 });
  await act(async () => { fireEvent.click(option); });
};

const useRegistrantAsContact = () => click('campus-opcontact-use-registrant-0');

const addCampus = async () => {
  await act(async () => { fireEvent.click(screen.getByTestId('v2-add-campus')); });
};

/** Builds an .xlsx File with N distinct, valid guest rows — for tests that need the visitor list
 * at (or near) its cap without one fireEvent per row. */
const guestListFile = (count: number, namePrefix = 'Person'): File => {
  const header = ['STT', 'Họ và tên', 'Chức vụ', 'Đơn vị công tác', 'Quốc tịch'];
  const rows = Array.from({ length: count }, (_, i) => [i + 1, `${namePrefix} ${i + 1}`, 'GV', 'Org X', 'VN']);
  const ws = XLSX.utils.aoa_to_sheet([header, ...rows]);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Sheet1');
  const buf = XLSX.write(wb, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;
  return new File([buf], 'khach.xlsx');
};

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

// REG-MEMBER-10 and the generic (non-registrant) half of plan CanhIter3FixBug §12/§13/§14: a
// hand-typed EXTERNAL contact that is NOT the registrant never destroys anything on the radio
// click, but also never offers "add the registrant" — it goes straight to picking a member.
describe('EXTERNAL → MEMBER, generic contact (plan §12-§14) — never destructive, never offers "add registrant"', () => {
  it.each([
    ['only fullName', () => type(opName(), 'Ngoài Đoàn')],
    ['only organization', () => type(orgInput(), 'Tổ chức ngoài')],
    ['only jobTitle', () => type(opJobTitle(), 'Giám đốc')],
    ['only phone', () => type(opPhone(), '0987654321')],
  ])('opens the member picker directly when the EXTERNAL snapshot has %s — never the old destructive confirm', async (_label, fillOne) => {
    renderForm();
    await chooseExternalSource();
    await fillOne();

    await chooseMemberSource();
    // The generic-contact case skips the add-or-choose decision (nothing to offer "add" for) and
    // goes straight to the picker — but nothing about it is the OLD wipe-everything confirm.
    expect(screen.queryByTestId('campus-opcontact-source-switch-confirm-0')).toBeNull();
    expect(screen.queryByTestId('campus-opcontact-switch-decision-0')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-switch-pick-member-0')).toBeInTheDocument();
    expect(screen.queryByTestId('campus-opcontact-switch-add-registrant-0')).toBeNull();
    // Still EXTERNAL — nothing applied yet.
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
  });

  it('picking a member still confirms away existing phone/email, and nothing leaks once confirmed', async () => {
    renderForm();
    await chooseExternalSource();
    await type(opName(), 'Ngoài Đoàn');
    await type(opPhone(), '0987654321');

    await chooseMemberSource();
    await fillMember('visitors', 0, { fullName: 'A Person', jobTitle: 'PM', organization: 'ABC Univ' });
    const select = screen.getByTestId('campus-opcontact-switch-pick-member-select-0') as HTMLSelectElement;
    const option = Array.from(select.querySelectorAll('option')).find(o => /A Person/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(select, { target: { value: option.value } }); });

    // Phone is at risk of being silently overwritten — confirms before committing.
    expect(screen.getByTestId('campus-opcontact-member-switch-confirm-0')).toBeInTheDocument();
    // Nothing applied yet: still EXTERNAL with the typed data intact.
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(opName().value).toBe('Ngoài Đoàn');

    await click('campus-opcontact-member-switch-yes-0');

    expect(screen.queryByTestId('campus-opcontact-switch-pick-member-0')).toBeNull();
    expect(readonlyName()).toBe('A Person');
    // The old EXTERNAL phone never leaked onto the newly-picked member.
    expect(opPhone().value).toBe('');
  });

  it('cancel is zero mutation — stays EXTERNAL with the typed data intact', async () => {
    renderForm();
    await chooseExternalSource();
    await type(opName(), 'Ngoài Đoàn');
    await type(opPhone(), '0987654321');

    await chooseMemberSource();
    await click('campus-opcontact-switch-pick-member-cancel-0');

    expect(screen.queryByTestId('campus-opcontact-switch-pick-member-0')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(opName().value).toBe('Ngoài Đoàn');
    expect(opPhone().value).toBe('0987654321');
  });
});

// plan CanhIter3FixBug §3-§9/§11/§15-§20 — registrant-as-member UX.
describe('EXTERNAL → MEMBER, registrant already copied in as the contact (plan §3-§9)', () => {
  const KIM = {
    fullName: 'Kim Min Jae', phone: '0912345678', email: 'kim@example.com',
    organization: 'SeoulTech', jobTitle: 'Director',
  };

  const quickFillKim = async () => {
    await fillRegistrant(KIM);
    await useRegistrantAsContact();
  };

  // REG-MEMBER-04
  it('opens the add-or-choose decision, with nothing applied yet', async () => {
    renderForm();
    await quickFillKim();

    await chooseMemberSource();

    expect(screen.getByTestId('campus-opcontact-switch-decision-0')).toBeInTheDocument();
    expect(screen.getByTestId('campus-opcontact-switch-add-registrant-0')).toBeInTheDocument();
    // Nothing committed: still EXTERNAL, still Kim's own data.
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(opName().value).toBe(KIM.fullName);
    expect(opPhone().value).toBe(KIM.phone);
    expect(opEmail().value).toBe(KIM.email);
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(1);
  });

  // REG-MEMBER-05
  it('cancel from the decision panel is a true no-op', async () => {
    renderForm();
    await quickFillKim();
    await chooseMemberSource();

    await click('campus-opcontact-switch-decision-cancel-0');

    expect(screen.queryByTestId('campus-opcontact-switch-decision-0')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(opName().value).toBe(KIM.fullName);
    expect(opPhone().value).toBe(KIM.phone);
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(1);
  });

  // REG-MEMBER-06 + REG-MEMBER-07 (the campus starts with exactly one blank scaffolding row, so
  // linking the registrant into it also proves the blank row is reused, not left behind).
  it('adds the registrant as a visitor, reusing the blank row, and preserves phone/email', async () => {
    renderForm();
    await quickFillKim();
    await chooseMemberSource();

    await click('campus-opcontact-switch-add-registrant-0');

    expect(screen.queryByTestId('campus-opcontact-switch-decision-0')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-picked-0')).toBeInTheDocument();
    // Exactly one visitor row — the blank scaffolding row was populated, not appended to.
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(1);
    expect((memberField('visitors', 0, 'fullName') as HTMLTextAreaElement).value).toBe(KIM.fullName);
    expect((memberField('visitors', 0, 'jobTitle') as HTMLTextAreaElement).value).toBe(KIM.jobTitle);
    expect(readonlyName()).toBe(KIM.fullName);
    // Phone/Email are contact-level, and the person has not changed — preserved, not cleared.
    expect(opPhone().value).toBe(KIM.phone);
    expect(opEmail().value).toBe(KIM.email);
    expect(vi.mocked(showSuccessToast)).toHaveBeenCalledWith(expect.stringContaining('Đã thêm người đăng ký'));
  });

  // REG-MEMBER-08 — race safety: somebody else added the registrant to the list while the panel
  // was open. The re-check before commit must link that row instead of duplicating it.
  it('links an exact match that appeared after the panel opened, instead of duplicating', async () => {
    renderForm();
    await quickFillKim();
    await chooseMemberSource();

    // A second guest, added AFTER the decision panel is already open, exactly matches the registrant.
    await addGuest();
    await fillMember('visitors', 1, {
      fullName: KIM.fullName, jobTitle: KIM.jobTitle, organization: KIM.organization,
    });

    await click('campus-opcontact-switch-add-registrant-0');

    // Still 2 rows (the original blank + Kim's) — no third row was created.
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(2);
    expect(screen.getByTestId('campus-opcontact-picked-0')).toBeInTheDocument();
    expect(readonlyName()).toBe(KIM.fullName);
    expect(opPhone().value).toBe(KIM.phone);
    expect(opEmail().value).toBe(KIM.email);
    expect(vi.mocked(showSuccessToast)).toHaveBeenCalledWith(expect.stringContaining(KIM.fullName));
  });

  // REG-MEMBER-02 style ambiguity, reached from the decision panel's "add" button.
  it('refuses to guess when the re-check finds more than one exact match', async () => {
    renderForm();
    await quickFillKim();
    await chooseMemberSource();

    await addGuest();
    await fillMember('visitors', 1, {
      fullName: KIM.fullName, jobTitle: KIM.jobTitle, organization: KIM.organization,
    });
    await addGuest();
    await fillMember('visitors', 2, {
      fullName: KIM.fullName, jobTitle: KIM.jobTitle, organization: KIM.organization,
    });

    await click('campus-opcontact-switch-add-registrant-0');

    expect(vi.mocked(showMessageErrorToast)).toHaveBeenCalledWith(expect.stringContaining('nhiều thành viên'));
    // No mutation: still EXTERNAL, no member picked, no row added.
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(3);
  });

  // REG-MEMBER-09 — choosing a DIFFERENT member instead: Kim's data must not leak onto them.
  it('choosing another member leaves Kim untouched until confirmed, then does not leak phone/email', async () => {
    renderForm();
    await quickFillKim();
    await addGuest();
    await fillMember('visitors', 1, { fullName: 'Moon', jobTitle: 'VP', organization: 'Other Univ' });

    await chooseMemberSource();
    await click('campus-opcontact-switch-choose-other-0');

    expect(screen.getByTestId('campus-opcontact-switch-pick-member-0')).toBeInTheDocument();
    // Kim is still the (EXTERNAL) contact — nothing committed by opening the picker.
    expect(opName().value).toBe(KIM.fullName);

    const select = screen.getByTestId('campus-opcontact-switch-pick-member-select-0') as HTMLSelectElement;
    const option = Array.from(select.querySelectorAll('option')).find(o => /Moon/.test(o.textContent ?? ''))!;
    await act(async () => { fireEvent.change(select, { target: { value: option.value } }); });

    // Kim has phone/email — picking Moon must still confirm before it overwrites them.
    expect(screen.getByTestId('campus-opcontact-member-switch-confirm-0')).toBeInTheDocument();
    expect(opName().value).toBe(KIM.fullName);

    await click('campus-opcontact-member-switch-yes-0');

    expect(readonlyName()).toBe('Moon');
    expect(opPhone().value).toBe('');
    expect(opEmail().value).toBe('');
  });

  // REG-MEMBER-11 — the visitor list is at the hard cap with no blank row to reuse: the operation
  // is refused atomically, nothing is added and nothing else changes.
  it('refuses to add the registrant when the visitor list is full, with no partial mutation', async () => {
    const { container } = renderForm();
    await fillRegistrant(KIM);

    // Fill the visitor list to the hard cap via a bulk Excel import — 200 people, none of them Kim.
    // The only scaffolding row is blank, so a plain import (append, dropping the blank row first)
    // already lands exactly on the cap without needing a "replace" confirmation.
    const fileInput = container.querySelector('input[type="file"]') as HTMLInputElement;
    await act(async () => {
      fireEvent.change(fileInput, { target: { files: [guestListFile(200)] } });
      await new Promise(r => setTimeout(r, 0));
    });
    await waitFor(() =>
      expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(200));

    await useRegistrantAsContact();
    await chooseMemberSource();
    await click('campus-opcontact-switch-add-registrant-0');

    expect(vi.mocked(showMessageErrorToast)).toHaveBeenCalledWith(expect.stringContaining('200'));
    // Still EXTERNAL, still 200 rows — nothing was added, nothing switched.
    expect(screen.getByTestId('campus-opcontact-name')).toBeInTheDocument();
    expect(screen.getAllByTestId('v2-visitors-table')[0].querySelectorAll('tbody tr')).toHaveLength(200);
  }, 30_000);

  // REG-MEMBER-12 — every mutation above is scoped to the campus card whose radio was clicked.
  it('touches only the campus whose decision panel was used', async () => {
    renderForm();
    await quickFillKim();
    await addCampus();

    await chooseMemberSource(0);
    await click('campus-opcontact-switch-add-registrant-0');

    expect(screen.getByTestId('campus-opcontact-picked-0')).toBeInTheDocument();
    // Campus 0's row now holds the registrant. Scoped through the campus's own <table> — each row's
    // testid also exists a second time in that card's mobile-card rendering of the SAME row, so an
    // unscoped `getAllByTestId` would mix the two campuses' duplicates together.
    const visitorsTable = (campusIndex: number) => screen.getAllByTestId('v2-visitors-table')[campusIndex];
    expect(within(visitorsTable(0)).getByTestId('visitors-0-fullName')).toHaveValue(KIM.fullName);
    // …campus 1 never had a contact source chosen, and its own row-0 is still the blank scaffolding
    // row it started with.
    expect(screen.getByTestId('campus-opcontact-source-member-1')).toBeInTheDocument();
    expect(visitorsTable(1).querySelectorAll('tbody tr')).toHaveLength(1);
    expect(within(visitorsTable(1)).getByTestId('visitors-0-fullName')).toHaveValue('');
  });
});

describe('EXTERNAL → MEMBER when the registrant snapshot was hand-edited away (plan §4)', () => {
  // The registrant-copy check re-verifies every time, never trusting a stale "quick-filled" flag.
  it('no longer offers "add registrant" once the copied snapshot has been edited', async () => {
    renderForm();
    await fillRegistrant({
      fullName: 'Kim Min Jae', phone: '0912345678', email: 'kim@example.com',
      organization: 'SeoulTech', jobTitle: 'Director',
    });
    await useRegistrantAsContact();

    // Edited away from the registrant after the copy.
    await type(opName(), 'Someone Else Entirely');

    await chooseMemberSource();

    expect(screen.queryByTestId('campus-opcontact-switch-add-registrant-0')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-switch-pick-member-0')).toBeInTheDocument();
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
