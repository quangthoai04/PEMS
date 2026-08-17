import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within, act } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({
  submitAmendment: vi.fn(),
  patchSafeDetails: vi.fn(),
}));

// PartnerOrgCombobox/OrganizationCombobox reach a DIFFERENT module (visitRequestApi, singular) for
// their search — mocked here so typing >=2 chars never fires a real request in these tests.
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: { searchOrganizations: vi.fn().mockResolvedValue([]) },
}));

// The modal reads the signed-in user to decide whether the viewer is Staff (commit 6be02a28), so it
// needs the context even here, where every case is a requester editing their own request. Mocked
// rather than wrapped in <AuthProvider> to match every other test in this folder — the provider does
// real session work these cases have no use for. user: null is the requester path.
vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));

import { submitAmendment, patchSafeDetails } from '../api/visitRequestV2Api';
import VisitAmendmentSubmitModal from '../components/VisitAmendmentSubmitModal';
import VisitSafeEditModal from '../components/VisitSafeEditModal';
import { campusFixture } from './fixtures';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';
import i18n from '../../../shared/i18n/config';

const form = (): ResolvedVisitForm => ({
  visitRequestId: 1, requestCode: 'VR-1', rowVersion: 4,
  hasMixedCampusDetails: false, visitScope: 'SINGLE_CAMPUS', requestStatus: 'APPROVED',
  createdSource: 'PUBLIC', submittedAt: '2026-07-15T08:00:00', partnerId: null,
  cancelledByUserId: null, cancelledByName: null, cancelledAt: null, cancellationReason: null,
  registrant: { fullName: 'Reg', organization: 'Org', jobTitle: 'Head', phone: '+84900000001', email: 'r@x.vn', nationality: 'VN' },
  confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },

  // Full-request scope in this fixture, so the backend sends the request-wide verdict.

  requestOutcome: { code: 'ALL_WAITING', total: 1, accepted: 0, inProgress: 0, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
  campusVisits: [campusFixture()],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW', 'SUBMIT_SAFE_EDIT'] },
});

describe('VisitAmendmentSubmitModal', () => {
  beforeEach(() => vi.clearAllMocks());

  it('requires a reason and submits the proposal with base revisions', async () => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    const onSubmitted = vi.fn();
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture()} onClose={() => {}} onSubmitted={onSubmitted} />);

    const submit = screen.getByRole('button', { name: 'Submit proposal' });
    expect(submit).toBeDisabled(); // no reason yet

    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'Đổi mục đích' } });
    expect(submit).toBeEnabled();
    fireEvent.click(submit);

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, instanceId, payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(instanceId).toBe(10);
    expect(payload.reason).toBe('Đổi mục đích');
    expect(payload.expectedInstanceRowVersion).toBe(3);
    expect(payload.baseFormRevision).toBe(2);
    expect(onSubmitted).toHaveBeenCalled();
  });

  it('maps AMENDMENT_ALREADY_PENDING to a stable message (no raw code)', async () => {
    vi.mocked(submitAmendment).mockRejectedValue({ response: { data: { errorCode: 'AMENDMENT_ALREADY_PENDING' } } });
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture()} onClose={() => {}} onSubmitted={() => {}} />);

    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'x' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/already has a pending proposal/i);
    expect(screen.queryByText(/AMENDMENT_ALREADY_PENDING/)).toBeNull();
  });
});

describe('VisitAmendmentSubmitModal — member list', () => {
  beforeEach(() => vi.clearAllMocks());

  const openWithReason = (campus = campusFixture()) => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campus} onClose={() => {}} onSubmitted={() => {}} />);
    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'Đổi đoàn' } });
  };

  it('adds a guest to the proposal and submits the enlarged member list', async () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' }));
    const nameInputs = screen.getAllByLabelText(/Guest list.*Full name/);
    expect(nameInputs).toHaveLength(2);
    // The new row needs every required field (same set Create/Edit enforce), not just a name — see
    // the AM-VAL suite below for what happens when one of these is left blank.
    fireEvent.change(nameInputs[1], { target: { value: 'Khách Hai' } });
    fireEvent.change(screen.getAllByLabelText(/Guest list.*Job title/)[1], { target: { value: 'NV' } });
    fireEvent.change(within(screen.getAllByTestId(/amendment-visitors-organization-/)[1]).getByRole('combobox'),
      { target: { value: 'Org Hai' } });
    const nationalityInput = screen.getAllByLabelText(/Guest list.*Nationality/)[1];
    fireEvent.change(nationalityInput, { target: { value: 'Vietnam' } });
    fireEvent.keyDown(nationalityInput, { key: 'Enter', code: 'Enter' });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.visitors.map(v => v.fullName)).toEqual(['Khách Một', 'Khách Hai']);
  });

  it('deep-clones members so editing the modal never mutates the source campus', async () => {
    const campus = campusFixture();
    const original = campus.visitors[0].fullName;
    openWithReason(campus);
    fireEvent.change(screen.getByLabelText(/Guest list.*Full name/), { target: { value: 'Đã sửa' } });
    // No reference is shared between the passed-in campus and the editor's own state.
    expect(campus.visitors[0].fullName).toBe(original);
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.visitors[0].fullName).toBe('Đã sửa');
  });

  it('summarizes member additions vs the active content', () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' }));
    expect(screen.getByRole('status')).toHaveTextContent(/1 added/);
  });

  it('requires at least one visitor before submitting', () => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture({ visitors: [] })} onClose={() => {}} onSubmitted={() => {}} />);
    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'x' } });
    expect(screen.getByRole('button', { name: 'Submit proposal' })).toBeDisabled();
    expect(screen.getByText('At least one guest is required.')).toBeInTheDocument();
  });

  it('keeps an existing member organizationPartnerId when only another field is edited', async () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC',
        organizationPartnerId: 42, jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
    });
    openWithReason(campus);
    fireEvent.change(screen.getByLabelText(/Guest list.*Full name/), { target: { value: 'Đã sửa tên' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.visitors[0].organizationPartnerId).toBe(42);
  });

  it('typing over a picked member organization clears its organizationPartnerId', async () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC',
        organizationPartnerId: 42, jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
    });
    openWithReason(campus);
    const orgInput = within(screen.getByTestId(/amendment-visitors-organization-/)).getByRole('combobox');
    fireEvent.change(orgInput, { target: { value: 'Đơn vị khác' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.visitors[0].organizationPartnerId).toBeNull();
  });

  // AM-NEW: the contact PROFILE has exactly one editable door left — "Manage the contact role" — so
  // this modal renders it read-only and never lets the submitted payload diverge from what the
  // backend already has on file, no matter what the picker below it does.
  it('shows the contact profile as read-only and offers no editable contact-profile inputs', async () => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    const campus = campusFixture();
    openWithReason(campus);

    expect(screen.getByTestId('amendment-contact-fullname-readonly')).toHaveTextContent('Đầu Mối HN');
    expect(screen.getByTestId('amendment-contact-jobtitle-readonly')).toHaveTextContent('Trưởng phòng');
    expect(screen.getByTestId('amendment-contact-organization-readonly')).toHaveTextContent('ĐH ABC');
    expect(screen.getByTestId('amendment-contact-phone-readonly')).toHaveTextContent('+84912345678');
    expect(screen.getByTestId('amendment-contact-email-readonly')).toHaveTextContent('dm@x.vn');

    // No input/select/combobox anywhere in this block lets the user type over any of the five fields —
    // the ONLY interactive control inside it is the contact-member picker, added separately below.
    expect(screen.queryByTestId('amendment-contact-organization')).toBeNull();
    expect(screen.queryByRole('textbox', { name: /email/i })).toBeNull();
    const readonlyBlock = screen.getByTestId('amendment-contact-profile-display');
    expect(within(readonlyBlock).queryAllByRole('textbox')).toHaveLength(0);
    expect(within(readonlyBlock).queryAllByRole('combobox')).toHaveLength(0);

    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));
    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.operationalContact).toEqual({
      fullName: 'Đầu Mối HN', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng',
      phone: '+84912345678', email: 'dm@x.vn',
    });
  });

  // AM-NEW: the picker still lets a proposal say WHO the contact is — a relationship, not a
  // description — and both Guest and External Support rows are eligible picks (plan PEMS_CONTACT_ONE_DOOR).
  it('offers both guest and support members in the contact-member picker', () => {
    const campus = campusFixture({
      supportMembers: [
        { guestMemberId: 2, memberType: 'EXTERNAL_SUPPORT', fullName: 'Hỗ Trợ Một', organization: 'Org S', jobTitle: 'Trợ lý', nationality: 'VN', displayOrder: 1 },
      ],
    });
    openWithReason(campus);
    const picker = screen.getByTestId('amendment-contact-pick');
    expect(within(picker).getByText(/Khách Một.*Guest/)).toBeInTheDocument();
    expect(within(picker).getByText(/Hỗ Trợ Một.*Support staff/)).toBeInTheDocument();
  });

  it('preselects the contact picker from the resolved operational contact link', () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
      operationalContact: {
        fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng', phone: '+84912345678', email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: 1,
      },
    });
    openWithReason(campus);
    const picker = screen.getByTestId('amendment-contact-pick') as HTMLSelectElement;
    expect(picker.value).not.toBe('');
    // Scoped to the picker itself: the read-only contact block above ALSO shows "Khách Một" (it is
    // the contact's own snapshot name), so an unscoped query now matches more than one element.
    expect(within(picker).getByText(/Khách Một/)).toBeInTheDocument();
  });

  it('clears the contact pick when the member holding it is removed', () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
      operationalContact: {
        fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng', phone: '+84912345678', email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: 1,
      },
    });
    openWithReason(campus);
    const picker = () => screen.getByTestId('amendment-contact-pick') as HTMLSelectElement;
    expect(picker().value).not.toBe('');
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' })); // keeps ≥1 visitor so removal is allowed
    fireEvent.click(screen.getAllByRole('button', { name: 'Remove row' })[0]);
    expect(picker().value).toBe('');
  });
});

// AM-VAL: validation errors must reach the FIELD, not just a banner (plan
// PEMS_AMENDMENT_VALIDATION_HIGHLIGHT). Before this suite, `errors.visitors` was one string covering
// the whole member list — Submit was blocked and the footer said "fix the highlighted fields", but
// nothing was actually highlighted. Required set mirrors Create/Edit's own schema (fullName, jobTitle,
// organization, nationality — see `buildPersonSchema` in visitRequestV2.schema.ts).
describe('VisitAmendmentSubmitModal — validation highlighting', () => {
  beforeEach(() => vi.clearAllMocks());

  const openWithReason = (campus = campusFixture()) => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campus} onClose={() => {}} onSubmitted={() => {}} />);
    fireEvent.change(screen.getByRole('textbox', { name: /Reason/i }), { target: { value: 'Đổi đoàn' } });
  };

  it('AM-VAL-01: an empty added guest row blocks submit and highlights its full name', () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    const nameInputs = screen.getAllByLabelText(/Guest list.*Full name/);
    const newRowName = nameInputs[1];
    expect(newRowName.closest('[data-field-error="true"]')).not.toBeNull();
    expect(newRowName).toHaveAttribute('aria-invalid', 'true');
    const container = newRowName.closest('[data-field-error="true"]') as HTMLElement;
    expect(within(container).getByRole('alert')).toHaveTextContent(/full name is required/i);
    // The original, already-complete row is untouched.
    expect(nameInputs[0]).not.toHaveAttribute('aria-invalid');
  });

  it('AM-VAL-02: a guest missing job title highlights only that field', () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: '', nationality: 'VN', displayOrder: 1 }],
    });
    openWithReason(campus);
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    const jobTitleInput = screen.getByLabelText(/Guest list.*Job title/);
    expect(jobTitleInput.closest('[data-field-error="true"]')).not.toBeNull();
    expect(jobTitleInput).toHaveAttribute('aria-invalid', 'true');
    // Same row's other three fields — already filled by the fixture — stay clean.
    expect(screen.getByLabelText(/Guest list.*Full name/)).not.toHaveAttribute('aria-invalid');
  });

  it('AM-VAL-03: a guest missing organization shows the OrganizationCombobox error state', () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: '', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
    });
    openWithReason(campus);
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    // The single original visitor row always mints this exact key — see `cloneMembers`'s
    // `v-orig-${i}` convention. An unanchored regex here would ALSO match the sibling error
    // paragraph's testid (`...-organization-error-v-orig-0`), which is the point of this test.
    const orgWrap = screen.getByTestId('amendment-visitors-organization-v-orig-0');
    expect(orgWrap.closest('[data-field-error="true"]')).not.toBeNull();
    expect(screen.getByText(/Organization is required/i)).toBeInTheDocument();
  });

  it('AM-VAL-04: a guest missing nationality shows the CountrySelect error state', () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', nationality: '', displayOrder: 1 }],
    });
    openWithReason(campus);
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    const nationalityInput = screen.getByLabelText(/Guest list.*Nationality/);
    expect(nationalityInput.closest('[data-field-error="true"]')).not.toBeNull();
    expect(screen.getByText(/Nationality is required/i)).toBeInTheDocument();
  });

  it('AM-VAL-05: an incomplete support member is highlighted in the SUPPORT list, not the guest one', () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add support member' }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    const supportName = screen.getByLabelText(/Accompanying support staff.*Full name/);
    expect(supportName.closest('[data-field-error="true"]')).not.toBeNull();
    // The already-complete guest row is unaffected — the error belongs to the support list alone.
    expect(screen.getByLabelText(/Guest list.*Full name/)).not.toHaveAttribute('aria-invalid');
  });

  it('AM-VAL-07: an end time at or before start blocks submit and highlights End', () => {
    openWithReason();
    const start = screen.getByLabelText(/Schedule/);
    const end = screen.getByLabelText('End');
    fireEvent.change(start, { target: { value: '2026-09-01T10:00' } });
    fireEvent.change(end, { target: { value: '2026-09-01T09:00' } }); // before start
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    const container = end.closest('[data-field-error="true"]');
    expect(container).not.toBeNull();
    expect(end).toHaveAttribute('aria-invalid', 'true');
    expect(within(container as HTMLElement).getByRole('alert')).toHaveTextContent(/end time must be after/i);
  });

  it('AM-VAL-08: fixing a field clears its own error immediately, without a second Submit', () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' }));
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));
    expect(submitAmendment).not.toHaveBeenCalled();

    const nameInputs = () => screen.getAllByLabelText(/Guest list.*Full name/);
    expect(nameInputs()[1].closest('[data-field-error="true"]')).not.toBeNull();
    // jobTitle on the SAME new row is also invalid — fixing fullName must not touch it.
    const jobTitleInputs = () => screen.getAllByLabelText(/Guest list.*Job title/);
    expect(jobTitleInputs()[1].closest('[data-field-error="true"]')).not.toBeNull();

    fireEvent.change(nameInputs()[1], { target: { value: 'Khách Hai' } });
    expect(nameInputs()[1].closest('[data-field-error="true"]')).toBeNull();
    expect(nameInputs()[1]).not.toHaveAttribute('aria-invalid');
    // Job title on that same row is STILL invalid — clearing one field never clears a sibling's error.
    expect(jobTitleInputs()[1].closest('[data-field-error="true"]')).not.toBeNull();
  });

  it('AM-VAL-09: submitting multiple errors focuses and scrolls to the first one in display order', async () => {
    openWithReason();
    const delegationInput = screen.getByTestId('amendment-delegation-input');
    fireEvent.change(delegationInput, { target: { value: '' } }); // request-level field, renders BEFORE members
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' })); // a second, empty guest row
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    await waitFor(() => expect(document.activeElement).toBe(delegationInput), { timeout: 1000 });
  });

  it('AM-VAL-10: removing the invalid row clears its error without moving it to another row', () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' })); // row 2 — empty
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));
    expect(submitAmendment).not.toHaveBeenCalled();

    const nameInputs = () => screen.getAllByLabelText(/Guest list.*Full name/);
    expect(nameInputs()[1].closest('[data-field-error="true"]')).not.toBeNull();
    expect(nameInputs()[0].closest('[data-field-error="true"]')).toBeNull(); // original row was never touched

    fireEvent.click(screen.getAllByRole('button', { name: 'Remove row' })[1]);
    expect(nameInputs()).toHaveLength(1);
    expect(nameInputs()[0].closest('[data-field-error="true"]')).toBeNull();
  });

  it('AM-VAL-11: a stable key keeps each row\'s own error attached after a DIFFERENT row is removed', () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' })); // row A — left fully empty
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' })); // row B — gets a name, nothing else
    const nameInputs = () => screen.getAllByLabelText(/Guest list.*Full name/);
    fireEvent.change(nameInputs()[2], { target: { value: 'Khách B' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));
    expect(submitAmendment).not.toHaveBeenCalled();

    const jobTitleInputs = () => screen.getAllByLabelText(/Guest list.*Job title/);
    expect(nameInputs()[1].closest('[data-field-error="true"]')).not.toBeNull();  // A: no name
    expect(nameInputs()[2].closest('[data-field-error="true"]')).toBeNull();      // B: has a name now
    expect(jobTitleInputs()[2].closest('[data-field-error="true"]')).not.toBeNull(); // B: still no job title

    // Remove row A (index 1) — B's job-title error must travel WITH B, not stay pinned to "index 2".
    fireEvent.click(screen.getAllByRole('button', { name: 'Remove row' })[1]);
    expect(nameInputs()).toHaveLength(2);
    expect((nameInputs()[1] as HTMLInputElement).value).toBe('Khách B');
    expect(jobTitleInputs()[1].closest('[data-field-error="true"]')).not.toBeNull();
  });

  it('AM-VAL-12: a fully valid form submits exactly once', async () => {
    openWithReason();
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));
    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
  });
});

// AM-UI: layout contract. No visual-regression/pixel tooling in this stack, so these check the class
// contract that keeps the modal usable on a real desktop viewport instead — a wide, non-2xl-capped
// frame; Cancel/Submit outside the scrolling body; a wide track for organization text.
describe('VisitAmendmentSubmitModal — layout', () => {
  beforeEach(() => vi.clearAllMocks());

  it('AM-UI-01: the desktop frame is wider than the old max-w-2xl cap', () => {
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture()} onClose={() => {}} onSubmitted={() => {}} />);
    const frame = screen.getByRole('button', { name: 'Submit proposal' }).closest('.rounded-2xl');
    expect(frame).not.toBeNull();
    expect(frame!.className).not.toMatch(/\bmax-w-2xl\b/);
    expect(frame!.className).toMatch(/min\(1180px/);
  });

  it('AM-UI-09: Cancel/Submit sit in a footer outside the scrolling body', () => {
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture()} onClose={() => {}} onSubmitted={() => {}} />);
    const submit = screen.getByRole('button', { name: 'Submit proposal' });
    const footer = submit.closest('footer');
    expect(footer).not.toBeNull();
    // The scrollable body is a SIBLING of the footer, not an ancestor — so the buttons never scroll away.
    expect(footer!.parentElement?.querySelector('.overflow-y-auto')).not.toBeNull();
    expect(footer!.closest('.overflow-y-auto')).toBeNull();
  });

  it('AM-UI-02: the member organization track is wide enough not to wrap letter-by-letter', () => {
    render(<VisitAmendmentSubmitModal visitRequestId={1} campus={campusFixture()} onClose={() => {}} onSubmitted={() => {}} />);
    const orgWrap = screen.getByTestId(/amendment-visitors-organization-/).closest('.min-w-0');
    expect(orgWrap).not.toBeNull();
    // The grid track this cell sits in reserves a wide minimum width for organization text on desktop.
    expect(orgWrap!.parentElement?.className).toMatch(/minmax\(260px,1\.6fr\)/);
  });
});

describe('VisitSafeEditModal', () => {
  beforeEach(() => vi.clearAllMocks());

  /** Types into one campus's note, which is the smallest real edit. */
  const editOneCampusNote = (value = 'Chuẩn bị phiên dịch.') =>
    fireEvent.change(screen.getByTestId('safe-edit-transportation-10'), { target: { value } });

  it('sends expected row versions and reports a 409 conflict with a reload action', async () => {
    vi.mocked(patchSafeDetails).mockRejectedValue({ response: { status: 409, data: { errorCode: 'CONCURRENCY_CONFLICT' } } });
    const onSaved = vi.fn();
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={onSaved} />);

    editOneCampusNote();
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.expectedRequestRowVersion).toBe(4);
    expect(payload.instances?.[0].expectedRowVersion).toBe(3);

    expect(await screen.findByRole('alert')).toHaveTextContent(/changed since you opened/i);
    fireEvent.click(screen.getByRole('button', { name: 'Reload' }));
    expect(onSaved).toHaveBeenCalled();
  });

  it('applies immediately and shows the applied-change count on success', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [{ fieldPath: 'registrant.phone', visitInstanceId: null, changeClass: 'SAFE' }],
      requestRowVersion: 5, instanceRowVersions: { 10: 4 }, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    editOneCampusNote();
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(await screen.findByText(/Applied 1 change/i)).toBeInTheDocument();
  });

  // ── Changed-only payload (§6). The modal used to send a full snapshot of every safe field of every
  //    campus, which dragged untouched campuses into the request and could overwrite a value that had
  //    changed server-side since the form loaded. ──

  it('submits ONLY the campus that changed, and no request-level block', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    editOneCampusNote('Xe 45 chỗ');
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.registrant).toBeNull();
    expect(payload.instances).toHaveLength(1);
    expect(payload.instances?.[0]).toMatchObject({ visitInstanceId: 10, transportationNote: 'Xe 45 chỗ' });
    // The untouched fields of the touched campus are absent, not echoed back at their old values.
    expect(payload.instances?.[0].mediaConsentStatus).toBeUndefined();
  });

  it('sends instances: [] when only a request-level field changed', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(within(screen.getByTestId('safe-edit-registrant-phone')).getByRole('textbox'),
      { target: { value: '+84900000009' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances).toEqual([]);
    expect(payload.registrant).toMatchObject({ phone: '+84900000009' });
  });

  it('refuses to call the API when nothing was edited', async () => {
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(await screen.findByRole('alert')).toHaveTextContent(/have not changed anything/i);
    expect(patchSafeDetails).not.toHaveBeenCalled();
  });

  it('omits a campus the backend has closed, and names it', () => {
    const closed = form();
    closed.campusVisits = [campusFixture({ instanceStatus: 'DURING_VISIT', allowedActions: [] })];
    render(<VisitSafeEditModal form={closed} onClose={() => {}} onSaved={() => {}} />);

    expect(screen.queryByTestId('safe-edit-transportation-10')).toBeNull();
    expect(screen.getByTestId('safe-edit-locked-campuses')).toHaveTextContent('FPTU Hà Nội');
  });

  it('keeps a campus editable when the SHARED block is not', async () => {
    // A mixed request: HN is approved and well ahead, HCM is still pending. The registrant/contact
    // block is shared by both so it is correctly locked — but HN's own notes must stay editable,
    // and the modal must still be usable. Hiding the whole thing here is the bug this pins.
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const mixed = form();
    mixed.viewer.allowedActions = ['VIEW']; // no request-level SUBMIT_SAFE_EDIT
    mixed.campusVisits = [
      campusFixture(),
      campusFixture({
        visitInstanceId: 11, campusId: 2, campusName: 'FPTU HCM',
        instanceStatus: 'WAITING_REQUEST_APPROVAL', allowedActions: [],
      }),
    ];
    render(<VisitSafeEditModal form={mixed} onClose={() => {}} onSaved={() => {}} />);

    expect(screen.getByTestId('safe-edit-shared-fields')).toBeDisabled();
    expect(screen.getByTestId('safe-edit-shared-locked')).toBeInTheDocument();
    expect(screen.getByTestId('safe-edit-locked-campuses')).toHaveTextContent('FPTU HCM');

    fireEvent.change(screen.getByTestId('safe-edit-transportation-10'), { target: { value: 'Xe 29 chỗ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.registrant).toBeNull();
    expect(payload.instances).toHaveLength(1);
    expect(payload.instances?.[0].visitInstanceId).toBe(10);
  });

  it('renders a real label for every registrant field', () => {
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
    for (const testId of [
      'safe-edit-registrant-fullName', 'safe-edit-registrant-nationality',
      'safe-edit-registrant-organization', 'safe-edit-registrant-jobTitle', 'safe-edit-registrant-phone',
    ]) {
      const wrap = screen.getByTestId(testId);
      expect(wrap.tagName).toBe('LABEL');
      expect(wrap.querySelector('span')).toHaveTextContent(/.+/);
    }
  });

  it('renders the campus note field and includes it in a sparse patch', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-notes-10'), { target: { value: 'Cần thêm ghế.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]).toEqual({ visitInstanceId: 10, expectedRowVersion: 3, notes: 'Cần thêm ghế.' });
  });

  it('shows the registrant organization as a searchable partner combobox and clears the id when free text is typed', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const f = form();
    f.partnerId = 15;
    render(<VisitSafeEditModal form={f} onClose={() => {}} onSaved={() => {}} />);

    const orgInput = within(screen.getByTestId('safe-edit-registrant-organization')).getByRole('textbox');
    fireEvent.change(orgInput, { target: { value: 'Tổ chức tự nhập' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.registrant).toMatchObject({ organization: 'Tổ chức tự nhập', partnerId: null });
  });

  it('lets the user change registrant nationality via the country select', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    const nationalityInput = within(screen.getByTestId('safe-edit-registrant-nationality')).getByRole('combobox');
    fireEvent.change(nationalityInput, { target: { value: 'Japan' } });
    fireEvent.keyDown(nationalityInput, { key: 'Enter', code: 'Enter' });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.registrant?.nationality).toBeTruthy();
    expect(payload.registrant?.nationality).not.toBe('VN');
  });

  // QE: the campus contact profile has exactly one editable door left — "Manage the contact role" —
  // so Quick Edit points at it instead of offering a second one (plan PEMS_CONTACT_ONE_DOOR).
  it('points to "Manage the contact role" instead of offering editable contact-profile fields', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    expect(screen.getByTestId('safe-edit-contact-managed-elsewhere-10')).toBeInTheDocument();
    // No test id, input or combobox anywhere in the modal offers to edit the contact's name,
    // organization or phone any more.
    expect(screen.queryByTestId(/safe-edit-contact-(name|org|phone)-/)).toBeNull();

    editOneCampusNote('Xe 45 chỗ');
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    // Structurally impossible to smuggle a contact-profile field in: the payload type carries none.
    expect(payload.instances?.[0]).not.toHaveProperty('operationalContact');
  });

  // TOAST: `appliedCount` used to be one hard-coded string ("Applied {{count}} change(s).") for every
  // count. i18next's own pluralization (`_one`/`_other` suffixes) now picks the right form — asserted
  // here through the in-modal success panel, which renders the exact same key as the toast.
  it('TOAST-01: a single applied change reads as singular, never "change(s)"', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [{ fieldPath: 'registrant.phone', visitInstanceId: null, changeClass: 'SAFE' }],
      requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
    editOneCampusNote();
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    const panel = await screen.findByTestId('safe-edit-applied');
    expect(panel).toHaveTextContent('Applied 1 change.');
    expect(panel.textContent).not.toMatch(/change\(s\)/);
  });

  it('TOAST-02: two applied changes read as plural', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1,
      appliedChanges: [
        { fieldPath: 'registrant.phone', visitInstanceId: null, changeClass: 'SAFE' },
        { fieldPath: 'instance.notes', visitInstanceId: 10, changeClass: 'SAFE' },
      ],
      requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
    editOneCampusNote();
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    const panel = await screen.findByTestId('safe-edit-applied');
    expect(panel).toHaveTextContent('Applied 2 changes.');
    expect(panel.textContent).not.toMatch(/change\(s\)/);
  });

  it('TOAST-03/04: the Vietnamese wording is correct for both counts, and never leaks "change(s)"', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    try {
      vi.mocked(patchSafeDetails).mockResolvedValueOnce({
        visitRequestId: 1, appliedChanges: [{ fieldPath: 'registrant.phone', visitInstanceId: null, changeClass: 'SAFE' }],
        requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
      });
      const { unmount } = render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
      editOneCampusNote();
      fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));
      const panel1 = await screen.findByTestId('safe-edit-applied');
      expect(panel1).toHaveTextContent('Đã áp dụng 1 thay đổi.');
      unmount();

      vi.mocked(patchSafeDetails).mockResolvedValueOnce({
        visitRequestId: 1,
        appliedChanges: [
          { fieldPath: 'registrant.phone', visitInstanceId: null, changeClass: 'SAFE' },
          { fieldPath: 'instance.notes', visitInstanceId: 10, changeClass: 'SAFE' },
        ],
        requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
      });
      render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
      editOneCampusNote();
      fireEvent.click(screen.getByRole('button', { name: 'Lưu thay đổi' }));
      const panel2 = await screen.findByTestId('safe-edit-applied');
      expect(panel2).toHaveTextContent('Đã áp dụng 2 thay đổi.');
      expect(panel2.textContent).not.toMatch(/change\(s\)/);
    } finally {
      await act(async () => { await i18n.changeLanguage('en'); });
    }
  });
});
