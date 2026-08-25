import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../../shared/i18n/config';
import VisitAmendmentSubmitModal from '../components/VisitAmendmentSubmitModal';
import type { ResolvedCampusVisit } from '../api/visitRequestV2Api';

// Real regression guard: VisitAmendmentSubmitModal's payload-building `.map()` calls were found to
// silently drop `guestMemberId` from every visitor/support row even though the local EditableMember
// state (via cloneMembers) already carried it — every legitimate member-list-changing amendment from
// THIS modal would have been misclassified as a stale pre-upgrade client by the backend's continuity
// check (operational-contact consistency fix). These tests prove the current payload carries the
// evidence the backend now requires.

vi.mock('../api/visitRequestV2Api', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../api/visitRequestV2Api')>();
  return { ...actual, submitAmendment: vi.fn().mockResolvedValue({ amendmentId: 1, status: 'PENDING_APPROVAL' }) };
});
vi.mock('../../../shared/utils/toast', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return { ...actual, showSuccessToast: vi.fn(), showMessageErrorToast: vi.fn() };
});

import { submitAmendment } from '../api/visitRequestV2Api';

const baseCampus = (overrides: Partial<ResolvedCampusVisit> = {}): ResolvedCampusVisit => ({
  visitInstanceId: 1, campusId: 1, campusCode: 'HN', campusName: 'FPTU Hà Nội',
  plannedStartAt: '2026-09-01T09:00:00', plannedEndAt: '2026-09-01T11:30:00', timezone: 'Asia/Ho_Chi_Minh',
  instanceStatus: 'ASSIGNED', currentHostUserId: null, currentHostName: null, decidedByUserId: null,
  decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
  delegationName: 'Đoàn HN', visitType: 'MEETING', visitTypeOther: null, purpose: 'Trao đổi', workingContent: 'ND',
  visitors: [
    { guestMemberId: 100, memberType: 'VISITOR', fullName: 'Kim', organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 },
  ],
  supportMembers: [],
  operationalContact: {
    fullName: 'Kim', organization: 'ĐH X', jobTitle: 'GV',
    phone: '+84912345678', email: 'op@example.com',
    confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T00:00:00',
    guestMemberId: 100,
  },
  currentHost: null, proposedHost: null,
  hostSelection: { canProposeSelfAsHost: false, canProposeOtherHost: false, canWaitForLaterAssignment: false, canUpdateProposedHost: false },
  workingLanguage: 'VI', transportationNote: null, mediaConsentStatus: 'AGREED',
  notes: '',
  formRevision: 1, approvalRevision: 0, rowVersion: 4, activeAmendment: null,
  cancelledByUserId: null, cancelledByName: null, cancelledAt: null,
  cancellationActorType: null, cancellationSource: null, cancellationReason: null,
  ...overrides,
} as ResolvedCampusVisit);

const renderModal = (campus: ResolvedCampusVisit) =>
  render(
    <VisitAmendmentSubmitModal
      visitRequestId={5}
      campus={campus}
      onClose={vi.fn()}
      onSubmitted={vi.fn()}
    />,
  );

const submit = async () => {
  fireEvent.change(screen.getByTestId('amendment-reason'), { target: { value: 'Cần đổi thông tin' } });
  fireEvent.click(screen.getByTestId('amendment-submit'));
  await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
  return vi.mocked(submitAmendment).mock.calls[0][2];
};

describe('VisitAmendmentSubmitModal — payload carries continuity evidence', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    await i18n.changeLanguage('vi');
  });

  it('AMD-FE-01: an existing linked member, edited, still carries its OWN guestMemberId and the contact key names it', async () => {
    renderModal(baseCampus());

    // Edit Kim's own JobTitle — a content change to the linked member, not a relation change.
    const jobTitleInput = screen.getByDisplayValue('GV');
    fireEvent.change(jobTitleInput, { target: { value: 'Senior Director' } });

    const payload = await submit();

    expect(payload.visitors).toHaveLength(1);
    expect(payload.visitors[0].guestMemberId).toBe(100); // NEVER dropped
    expect(payload.visitors[0].jobTitle).toBe('Senior Director');
    expect(payload.operationalContactClientMemberKey).toBe(payload.visitors[0].clientMemberKey);
    expect(payload.operationalContactGuestMemberId).toBe(100);
  });

  it('AMD-FE-02: an unlinked contact — operationalContactClientMemberKey/GuestMemberId are both null, existing member ids are still preserved', async () => {
    const unlinked = baseCampus({
      operationalContact: {
        fullName: 'Someone Else', organization: 'Org', jobTitle: 'Coordinator',
        phone: null, email: 'else@example.com',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T00:00:00',
        guestMemberId: null,
      },
    });
    renderModal(unlinked);

    const jobTitleInput = screen.getByDisplayValue('GV');
    fireEvent.change(jobTitleInput, { target: { value: 'Senior Lecturer' } });

    const payload = await submit();

    expect(payload.operationalContactClientMemberKey).toBeNull();
    expect(payload.operationalContactGuestMemberId).toBeNull();
    // The existing row's own persisted id still travels — it is continuity evidence for the MEMBER
    // regardless of whether the contact happens to be linked to it.
    expect(payload.visitors[0].guestMemberId).toBe(100);
  });

  it('AMD-FE-03: member list unchanged — operationalContactGuestMemberId still names the active persisted relation, not derived from names', async () => {
    renderModal(baseCampus());

    // No member edits at all — only the reason changes.
    const payload = await submit();

    expect(payload.visitors[0].guestMemberId).toBe(100);
    expect(payload.operationalContactGuestMemberId).toBe(100);
    expect(payload.operationalContactClientMemberKey).toBe(payload.visitors[0].clientMemberKey);
  });

  // §9 root-cause probe: the real-stack E2E "owner adds a guest via the real modal" flow hangs with
  // no diagnosable cause found live. This proves the ADD-A-NEW-VISITOR-ROW path (the one thing the
  // 3 tests above never exercise — they only edit an EXISTING, already-valid row) at the React level,
  // decoupled from browser/harness variables live debugging couldn't rule in or out.
  it('§9: adding a new guest row, filling every required field, then submitting calls submitAmendment with the new visitor included', async () => {
    renderModal(baseCampus());

    fireEvent.click(screen.getByTestId('amendment-add-visitor'));

    const fullnameInputs = screen.getAllByTestId('amendment-visitors-fullname');
    expect(fullnameInputs).toHaveLength(2); // the existing row + the freshly-added one
    fireEvent.change(fullnameInputs[1], { target: { value: 'Khach moi' } });
    fireEvent.change(screen.getAllByTestId('amendment-visitors-jobtitle')[1], { target: { value: 'Chuyen vien' } });

    // Organization is Creatable (free text committed on change, no option click needed) — same
    // pattern as the existing-row edit above and as operationalContactSourceSwitch.test.tsx's
    // fillMember helper. Nationality is a STRICT CountrySelect (no free text): type to filter, then
    // commit the resulting option, same pattern as that file's fillRegistrantNationality helper.
    // Neither carries a stable per-row testid; located by aria-label instead of position (the label
    // is bilingual depending on the test's active i18n language) and take the LAST match, since the
    // new row's fields render after the existing row's in DOM order.
    const orgCombos = screen.getAllByRole('combobox', { name: /organization|đơn vị công tác/i });
    const nationalityCombos = screen.getAllByRole('combobox', { name: /nationality|quốc tịch/i });
    const orgCombo = orgCombos[orgCombos.length - 1];
    const nationalityCombo = nationalityCombos[nationalityCombos.length - 1];

    fireEvent.change(orgCombo, { target: { value: 'Org Moi' } });

    // react-select's menu-open/filter behavior does not reliably respond to a bare fireEvent.change
    // on a STRICT Select in JSDOM (unlike the Creatable organization field above) — userEvent's fuller
    // click+type event sequence correctly opens the menu and filters to exactly one option (confirmed
    // directly against the DOM: exactly one [role="option"] node, textContent "Việt Nam"). react-select
    // renders its menu through a document.body portal, and `screen.findByText` inexplicably could not
    // locate that same node even though a raw querySelector does, so the option is selected via a
    // direct DOM query instead of RTL's text query for reliability.
    const user = userEvent.setup();
    await user.click(nationalityCombo);
    await user.type(nationalityCombo, 'Việt Nam');
    await waitFor(() => expect(document.querySelectorAll('[role="option"]').length).toBe(1));
    const option = document.querySelector('[role="option"]') as HTMLElement;
    expect(option.textContent).toBe('Việt Nam');
    await act(async () => { fireEvent.click(option); });

    const payload = await submit();

    expect(payload.visitors).toHaveLength(2);
    const added = payload.visitors[1];
    expect(added.fullName).toBe('Khach moi');
    expect(added.jobTitle).toBe('Chuyen vien');
    expect(added.organization).toBe('Org Moi');
    // CountrySelect's storeLang defaults to 'en' for visit-request forms (see its own comment: the
    // UI shows the localized label, but the STORED value is always the English name, regardless of
    // display language) — the option is picked by its Vietnamese label, but the submitted value is
    // the English name that convention dictates.
    expect(added.nationality).toBe('Vietnam');
  });
});
