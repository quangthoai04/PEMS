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
    // Anchored to exclude the sibling "-known" test id: removing `isCell` (plan FIX-E) un-suppresses
    // OrganizationCombobox's own "picked from list" indicator paragraph, which now legitimately renders
    // here too and shares the same prefix.
    const orgInput = within(
      screen.getByTestId(/^amendment-visitors-organization-(?!.*-known$)/),
    ).getByRole('combobox');
    fireEvent.change(orgInput, { target: { value: 'Đơn vị khác' } });
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.visitors[0].organizationPartnerId).toBeNull();
  });

  // U1-U3 (plan CanhIter3FixBug §3/§21/§27-A): the general "Đề xuất thay đổi" modal shows NO
  // Operational Contact surface at all any more — not the profile, read-only or otherwise, and not a
  // relation picker. That workflow moved to Operational Contact Management (plan §5) so it is never
  // read as "this is where you change who the contact is".
  it('renders no Operational Contact section — no profile block, no relation picker', async () => {
    vi.mocked(submitAmendment).mockResolvedValue({} as never);
    const campus = campusFixture();
    openWithReason(campus);

    expect(screen.queryByTestId('amendment-contact-readonly')).toBeNull();
    expect(screen.queryByTestId('amendment-contact-profile-display')).toBeNull();
    expect(screen.queryByTestId('amendment-contact-fullname-readonly')).toBeNull();
    expect(screen.queryByTestId('amendment-contact-jobtitle-readonly')).toBeNull();
    expect(screen.queryByTestId('amendment-contact-organization-readonly')).toBeNull();
    expect(screen.queryByTestId('amendment-contact-phone-readonly')).toBeNull();
    expect(screen.queryByTestId('amendment-contact-email-readonly')).toBeNull();
    expect(screen.queryByTestId('amendment-contact-pick')).toBeNull();
    // Neither the profile's own values nor the contact-block heading text appear anywhere on the modal.
    expect(screen.queryByText('Đầu Mối HN')).toBeNull();
    expect(screen.queryByText('dm@x.vn')).toBeNull();

    // U4: even with nothing on screen for it, the submitted payload still carries the contact
    // EXACTLY as persisted — the backend's unchanged-profile check must keep passing for a proposal
    // built by this modal, same as before the UI moved.
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));
    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.operationalContact).toEqual({
      fullName: 'Đầu Mối HN', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng',
      phone: '+84912345678', email: 'dm@x.vn',
    });
  });

  // Plan CanhIter3FixBug §4/§27-B: removing the VISIBLE picker must not remove the underlying relation
  // tracking — a general amendment (schedule/purpose/members/...) still has to preserve WHO the contact
  // is, silently, exactly as it stood before the proposal, so it never gets read as a relation change.
  it('silently preserves the persisted contact relation in the payload with no UI to change it', async () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
      operationalContact: {
        fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng', phone: '+84912345678', email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: 1,
      },
    });
    openWithReason(campus);
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    // The member list is UNCHANGED, so the persistent id is what carries the (unchanged) relation.
    expect(payload.operationalContactGuestMemberId).toBe(1);
  });

  it('sends null relation fields when the contact starts outside the delegation', async () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
    });
    openWithReason(campus); // default fixture's operationalContact carries no guestMemberId
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    await waitFor(() => expect(submitAmendment).toHaveBeenCalledTimes(1));
    const [, , payload] = vi.mocked(submitAmendment).mock.calls[0];
    expect(payload.operationalContactClientMemberKey).toBeNull();
    expect(payload.operationalContactGuestMemberId).toBeNull();
  });

  // FIX-C (plan CanhIter3FixBug §19/§26): removing the member who IS the operational contact must
  // still be blocked outright, even with no picker on screen to re-point the relation first — the
  // guard is on the delete action itself, not on the (now-removed) picker's presence.
  it('still blocks removing the member who is the operational contact, with no picker on screen', () => {
    const campus = campusFixture({
      visitors: [{ guestMemberId: 1, memberType: 'VISITOR', fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
      operationalContact: {
        fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng', phone: '+84912345678', email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: 1,
      },
    });
    openWithReason(campus);
    expect(screen.queryByTestId('amendment-contact-pick')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: 'Add guest' })); // keeps ≥1 visitor so removal isn't disabled
    fireEvent.click(screen.getAllByRole('button', { name: 'Remove row' })[0]); // targets Khách Một's row

    // Blocked, not silently applied: the row is still there.
    expect(screen.getByDisplayValue('Khách Một')).toBeInTheDocument();
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

  // FIX-D (plan CanhIter3FixBug): WorkingContent had no frontend validation at all before — the backend
  // requires it, so a blank submission just round-tripped as a generic server refusal with nothing to
  // point at.
  it('FIX-D: a blank working content blocks submit and highlights the field', () => {
    openWithReason();
    const workingContent = screen.getByLabelText(/Working content/i);
    fireEvent.change(workingContent, { target: { value: '   ' } }); // whitespace-only counts as blank
    fireEvent.click(screen.getByRole('button', { name: 'Submit proposal' }));

    expect(submitAmendment).not.toHaveBeenCalled();
    const container = workingContent.closest('[data-field-error="true"]');
    expect(container).not.toBeNull();
    expect(workingContent).toHaveAttribute('aria-invalid', 'true');
    expect(within(container as HTMLElement).getByRole('alert')).toBeInTheDocument();
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

  // ── Registrant UX hardening (GitHub "SAFE EDIT REGISTRANT UX HARDENING") ─────────────────────────
  // Authority stays exactly `canEditShared` (form.viewer.allowedActions) — these tests pin only the
  // PRESENTATION of that backend verdict (info icon, exact reason, dynamic lead-hours) and that the
  // custom controls inside the registrant fieldset are genuinely locked, not merely wrapped by one.

  /** A GRANTED request-level capability entry, mirroring what the backend attaches for a registrant
   *  whose request qualifies — used to prove the tooltip/notice render the BACKEND's own numbers. */
  const grantedSafeEditCapability = {
    code: 'SUBMIT_SAFE_EDIT', scope: 'REQUEST' as const, visitInstanceId: null, enabled: true,
    disabledReasonCode: null, disabledReason: null, cutoffAt: '2026-07-25T15:00:00',
    plannedStartAt: '2026-08-01T09:00:00', campusName: 'FPTU Hà Nội', requiredLeadHours: 6,
  };

  it('TEST A: shows the info icon and every registrant control enabled when the request-level capability is granted', () => {
    const f = form();
    f.viewer.capabilities = [grantedSafeEditCapability];
    render(<VisitSafeEditModal form={f} onClose={() => {}} onSaved={() => {}} />);

    expect(screen.getByTestId('safe-edit-registrant-tooltip')).toBeInTheDocument();
    expect(screen.getByTestId('safe-edit-shared-fields')).not.toBeDisabled();
    expect(within(screen.getByTestId('safe-edit-registrant-organization')).getByRole('textbox')).not.toBeDisabled();
    // react-select drops its `combobox` role from the accessibility-role query once disabled (still
    // present as a raw attribute, per the DOM dump seen while developing this test) — queried as a
    // plain node instead, which is robust either way.
    expect(screen.getByTestId('safe-edit-registrant-nationality').querySelector('input')).not.toBeDisabled();
    expect(screen.getByTestId('safe-edit-registrant-phone-input')).not.toBeDisabled();
    expect(screen.queryByTestId('safe-edit-shared-locked')).not.toBeInTheDocument();
  });

  it('TEST B (main regression): a locked registrant block never disables an eligible sibling campus, and its own custom controls are truly disabled — not just the wrapping fieldset', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    // Hà Nội already approved and well ahead; TP.HCM still pending its own decision — a genuine MIXED
    // request. The registrant block is shared by both, so it is correctly locked; HN's own fields must
    // stay usable regardless.
    const mixed = form();
    mixed.viewer.allowedActions = ['VIEW']; // no request-level SUBMIT_SAFE_EDIT
    mixed.viewer.capabilities = [{
      code: 'SUBMIT_SAFE_EDIT', scope: 'REQUEST', visitInstanceId: null, enabled: false,
      disabledReasonCode: 'VISIT_MUTATION_LIFECYCLE_NOT_ALLOWED',
      disabledReason: 'Cơ sở FPTU HCM chưa được duyệt; hãy dùng chức năng sửa thông tin cơ sở đang chờ duyệt.',
      cutoffAt: null, plannedStartAt: '2026-08-03T09:00:00', campusName: 'FPTU HCM', requiredLeadHours: 6,
    }];
    mixed.campusVisits = [
      campusFixture(),
      campusFixture({
        visitInstanceId: 11, campusId: 2, campusName: 'FPTU HCM',
        instanceStatus: 'WAITING_REQUEST_APPROVAL', allowedActions: [],
      }),
    ];
    render(<VisitSafeEditModal form={mixed} onClose={() => {}} onSaved={() => {}} />);

    // Presentation: icon always present, locked notice shows the EXACT backend reason verbatim —
    // never a frontend reconstruction of "why" from campus statuses.
    expect(screen.getByTestId('safe-edit-registrant-tooltip')).toBeInTheDocument();
    expect(screen.getByTestId('safe-edit-shared-locked')).toBeInTheDocument();
    expect(screen.getByTestId('safe-edit-shared-locked-reason')).toHaveTextContent('FPTU HCM chưa được duyệt');
    expect(screen.getByTestId('safe-edit-locked-campuses')).toHaveTextContent('FPTU HCM');

    // The custom widgets are ACTUALLY locked — not only the ambient <fieldset disabled> around them,
    // which does not by itself stop a react-select-style control's own click-driven menu.
    expect(within(screen.getByTestId('safe-edit-registrant-organization')).getByRole('textbox')).toBeDisabled();
    expect(screen.getByTestId('safe-edit-registrant-nationality').querySelector('input')).toBeDisabled();
    expect(screen.getByTestId('safe-edit-registrant-phone-input')).toBeDisabled();

    // HN stays fully usable and saves on its own — a locked shared block must never disable a sibling
    // campus that is independently eligible.
    fireEvent.change(screen.getByTestId('safe-edit-transportation-10'), { target: { value: 'Xe 29 chỗ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.registrant).toBeNull();
    expect(payload.instances).toHaveLength(1);
    expect(payload.instances?.[0].visitInstanceId).toBe(10);
  });

  it('TEST C: a request-level CUTOFF reason renders verbatim, while a sibling campus still inside its own window stays editable', () => {
    // A valid, non-invented multi-campus shape: the campus that governs the request-level deadline
    // (VisitMutationPolicy.RequestLevelScope / the read model's `Governing` helper) has itself just
    // crossed its own cutoff — so its OWN per-instance capability is refused too, and it offers no
    // fields at all — while a LATER sibling, days out, is still comfortably inside its own window.
    const mixed = form();
    mixed.viewer.allowedActions = ['VIEW'];
    mixed.viewer.capabilities = [{
      code: 'SUBMIT_SAFE_EDIT', scope: 'REQUEST', visitInstanceId: null, enabled: false,
      disabledReasonCode: 'VISIT_MUTATION_CUTOFF_REACHED',
      disabledReason: 'Thao tác này chỉ được thực hiện ít nhất 6 giờ trước khi chuyến thăm bắt đầu.',
      cutoffAt: '2026-07-31T15:00:00', plannedStartAt: '2026-07-31T21:00:00', campusName: 'FPTU Hà Nội',
      requiredLeadHours: 6,
    }];
    mixed.campusVisits = [
      campusFixture({ allowedActions: [] }), // HN — past its own cutoff too, hence the request-level block
      campusFixture({ visitInstanceId: 11, campusId: 2, campusName: 'FPTU HCM', plannedStartAt: '2026-08-05T09:00:00' }),
    ];
    render(<VisitSafeEditModal form={mixed} onClose={() => {}} onSaved={() => {}} />);

    expect(screen.getByTestId('safe-edit-shared-locked-reason'))
      .toHaveTextContent('ít nhất 6 giờ trước khi chuyến thăm bắt đầu');
    expect(screen.getByTestId('safe-edit-transportation-11')).not.toBeDisabled();
    expect(screen.queryByTestId('safe-edit-transportation-10')).toBeNull(); // HN offers nothing at all now
    expect(screen.getByTestId('safe-edit-locked-campuses')).toHaveTextContent('FPTU Hà Nội');
  });

  it('TEST D: a non-registrant whose OWN campus is editable never gains registrant authority (no privilege escalation)', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    // Mirrors a campus's own confirmed operational contact opening Quick Edit for their campus: the
    // backend only ever attaches a request-level SUBMIT_SAFE_EDIT capability for the registrant
    // (VisitFormReadService, `if (isRegistrant && instances.Count > 0)`), so this actor's
    // `viewer.capabilities` is simply ABSENT — never a disabled entry with a reason. The general
    // explanation must still render without crashing, and the registrant block must stay locked even
    // though this actor's own campus is fully editable.
    const asContact = form();
    asContact.viewer.allowedActions = ['VIEW'];
    asContact.viewer.capabilities = undefined;
    render(<VisitSafeEditModal form={asContact} onClose={() => {}} onSaved={() => {}} />);

    expect(screen.getByTestId('safe-edit-shared-locked')).toBeInTheDocument();
    expect(screen.queryByTestId('safe-edit-shared-locked-reason')).not.toBeInTheDocument();
    expect(within(screen.getByTestId('safe-edit-registrant-organization')).getByRole('textbox')).toBeDisabled();

    fireEvent.change(screen.getByTestId('safe-edit-transportation-10'), { target: { value: 'Ghi chú riêng của HN' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.registrant).toBeNull();
    expect(payload.instances).toEqual([
      { visitInstanceId: 10, expectedRowVersion: 3, transportationNote: 'Ghi chú riêng của HN' },
    ]);
  });

  it('TEST E: the info icon exposes its help text on keyboard focus, not only on mouse hover', () => {
    const f = form();
    f.viewer.capabilities = [grantedSafeEditCapability];
    render(<VisitSafeEditModal form={f} onClose={() => {}} onSaved={() => {}} />);
    const trigger = screen.getByTestId('safe-edit-registrant-tooltip');

    expect(trigger.tagName).toBe('BUTTON');
    expect(trigger).toHaveAttribute('type', 'button');
    expect(trigger).toHaveAttribute('aria-label', 'Registrant'); // visitRequestV2:summary.registrant (en)
    expect(trigger).toHaveAttribute('aria-expanded', 'false');

    fireEvent.focus(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'true');
    const describedBy = trigger.getAttribute('aria-describedby');
    expect(describedBy).toBeTruthy();
    const tooltip = document.getElementById(describedBy!);
    expect(tooltip).not.toBeNull();
    expect(tooltip).toHaveTextContent(/shared across every campus/i);
    // Dynamic lead-hours sentence, sourced from the backend capability — never a hardcoded literal.
    expect(tooltip).toHaveTextContent(/6 hours/i);

    fireEvent.blur(trigger);
    expect(trigger).toHaveAttribute('aria-expanded', 'false');
  });

  it('TEST F: the registrant tooltip is start-aligned with readable, left-aligned, wrapping typography — not clipped against the modal edge', () => {
    const f = form();
    f.viewer.capabilities = [grantedSafeEditCapability];
    render(<VisitSafeEditModal form={f} onClose={() => {}} onSaved={() => {}} />);
    const trigger = screen.getByTestId('safe-edit-registrant-tooltip');

    fireEvent.focus(trigger);
    const tooltip = screen.getByRole('tooltip');
    const bubble = tooltip.firstElementChild as HTMLElement;

    // Anchored to the trigger's own left edge (grows rightward) rather than centered on it — centering
    // is what pushed the bubble's left half past the modal's edge for a trigger sitting this close to
    // it, clipping the first word(s) of the content.
    expect(tooltip.className).toContain('left-0');
    expect(tooltip.className).not.toContain('left-1/2');
    expect(bubble.className).toContain('text-left');
    expect(bubble.className).toContain('text-[12px]');
    expect(bubble.className).toContain('font-normal');
    expect(bubble.className).toContain('leading-5');

    // The full sentence renders — not just a truncated fragment — proving normal wrapping rather than
    // a clipped first/last word.
    expect(tooltip).toHaveTextContent(
      'Registrant information is shared across every campus. This section can only be edited once '
      + 'every campus has been approved, the visit has not started, and the change window is still '
      + 'open. Quick edit is currently available until at least 6 hours before the earliest start.',
    );
  });

  it('no longer renders the blue "apply now" banner', () => {
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
    expect(screen.queryByText(/administrative \/ privacy corrections apply immediately/i)).not.toBeInTheDocument();
    expect(document.querySelector('.bg-blue-50')).toBeNull();
  });

  // CONTACT (plan CanhIter3FixBug §4-§6) — same-person operational-contact metadata + relation now
  // live directly in Sửa nhanh, gated by UPDATE_OPERATIONAL_CONTACT_PROFILE independently of
  // SUBMIT_SAFE_EDIT (decision M).

  it('CONTACT-01: renders the contact block, disabled when the campus lacks UpdateContactProfile', () => {
    // campusFixture()'s default allowedActions is SUBMIT_SAFE_EDIT + SUBMIT_AMENDMENT only.
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);

    const block = screen.getByTestId('safe-edit-contact-10');
    expect(block).toBeDisabled();
    // Email is never an <input> — it is always static/readonly, regardless of capability.
    expect(screen.getByTestId('safe-edit-contact-email-10')).toHaveTextContent('dm@x.vn');
    expect(screen.queryByRole('textbox', { name: /^email$/i })).toBeNull();
  });

  it('CONTACT-02: a campus with UpdateContactProfile (even without SubmitSafeEdit) still shows an editable contact block', () => {
    const withContactOnly = form();
    withContactOnly.campusVisits[0] = campusFixture({
      allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'], // no SUBMIT_SAFE_EDIT — e.g. WAITING_REQUEST_APPROVAL
    });
    render(<VisitSafeEditModal form={withContactOnly} onClose={() => {}} onSaved={() => {}} />);

    // Still included in the modal (decision M: canGenericSafe || canEditContact).
    expect(screen.getByTestId('safe-edit-contact-10')).not.toBeDisabled();
    // Generic Notes field stays disabled — the two capabilities are independent.
    expect(screen.getByTestId('safe-edit-notes-10')).toBeDisabled();
  });

  it('CONTACT-03: saving a metadata-only change sends operationalContact without a memberLink wrapper', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({ allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'] });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-phone-10'), { target: { value: '+84900000099' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]?.operationalContact).toMatchObject({
      phone: '+84900000099', email: 'dm@x.vn',
    });
    expect(payload.instances?.[0]?.operationalContact).not.toHaveProperty('memberLink');
  });

  it('CONTACT-04: explicit unlink sends memberLink: { guestMemberId: null }, not an omitted field', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({
      allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'],
      operationalContact: {
        fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', phone: '+84912345678', email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: 1,
      },
    });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    const picker = screen.getByTestId('safe-edit-contact-relation-10') as HTMLSelectElement;
    expect(picker.value).toBe('1');
    fireEvent.change(picker, { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]?.operationalContact?.memberLink).toEqual({ guestMemberId: null });
  });

  // Superseded by the operational-contact consistency fix's OWN hardening: the relation picker now
  // offers only exact-identity candidates (SAFE-FE-04) and the shared fields become read-only the
  // moment a contact is linked (SAFE-FE-01) — so a mismatch can no longer be CAUSED through this modal
  // at all. What remains reachable is a PRE-EXISTING (legacy) mismatch loaded from the server: the
  // warning must still surface, but per the operation-aware contract (Case C) it must NOT block a save
  // that never touches the relation or the shared fields — only retyping them to something that still
  // mismatches would, and retyping is exactly what read-only removes as an option.
  it('CONTACT-05: a pre-existing legacy mismatch shows the inline warning on load, but does not block an untouched save', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({
      allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE', 'SUBMIT_SAFE_EDIT'],
      // Linked to "Khách Một" (guestMemberId 1), but the contact's OWN stored fields still say
      // "Đầu Mối HN" — a legacy mismatch that predates this fix, never created through this modal.
      operationalContact: {
        fullName: 'Đầu Mối HN', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng',
        phone: '+84912345678', email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: 1,
      },
    });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    // Informational on load — no interaction needed.
    expect(screen.getByTestId('safe-edit-contact-mismatch-10')).toBeInTheDocument();

    // An edit that touches neither the relation nor the shared fields (Notes) must still succeed —
    // the untouched legacy mismatch is never this save's problem to fix.
    editOneCampusNote('Xe 45 chỗ');
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
  });

  it('CONTACT-06: no "Đồng bộ theo thành viên đã chọn" (sync) button exists anywhere in the modal', () => {
    const editable = form();
    editable.campusVisits[0] = campusFixture({ allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'] });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-relation-10'), { target: { value: '1' } });
    expect(screen.queryByRole('button', { name: /đồng bộ|sync/i })).toBeNull();
  });

  it('CONTACT-07: the forbidden relation helper paragraph never renders', () => {
    render(<VisitSafeEditModal form={form()} onClose={() => {}} onSaved={() => {}} />);
    expect(screen.queryByText(/không thay đổi họ tên, chức vụ, đơn vị/i)).toBeNull();
    expect(screen.queryByTestId('safe-edit-contact-managed-elsewhere-10')).toBeNull();
  });

  it('CONTACT-08: changing only Notes omits operationalContact from the payload entirely', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({
      allowedActions: ['SUBMIT_SAFE_EDIT', 'UPDATE_OPERATIONAL_CONTACT_PROFILE'],
    });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    editOneCampusNote('Xe 45 chỗ');
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]).not.toHaveProperty('operationalContact');
  });

  // PHONE (GitHub bug report, CanhIter3FixBug live-UI repro): "The Phone field is required." on a
  // relation/name-only Safe Edit. Phone is OPTIONAL end to end — these pin the exact payload shape so a
  // regression here fails a frontend test, not just a live click-through.

  it('F1: changing FullName only still echoes the ON-FILE phone in the payload (not omitted, not blanked)', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({ allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'] });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-fullName-10'), { target: { value: 'Đầu Mối HN (đã sửa)' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]?.operationalContact).toMatchObject({
      fullName: 'Đầu Mối HN (đã sửa)', phone: '+84912345678',
    });
  });

  it('F2: changing relation only still echoes the ON-FILE phone in the payload', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({
      allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'],
      operationalContact: {
        fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', phone: '+84912345678', email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: null,
      },
    });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-relation-10'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]?.operationalContact?.memberLink).toEqual({ guestMemberId: 1 });
    expect(payload.instances?.[0]?.operationalContact?.phone).toBe('+84912345678');
  });

  it('F3: current phone null — FullName-only change sends phone: null (not omitted, not "")', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({
      allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'],
      operationalContact: {
        fullName: 'Đầu Mối HN', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng', phone: null, email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
      },
    });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-fullName-10'), { target: { value: 'Đầu Mối HN (đã sửa)' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]?.operationalContact?.phone).toBeNull();
  });

  it('F4: current phone null — relation-only change sends phone: null', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({
      allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'],
      operationalContact: {
        fullName: 'Khách Một', organization: 'ĐH ABC', jobTitle: 'GV', phone: null, email: 'dm@x.vn',
        confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        guestMemberId: null,
      },
    });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-relation-10'), { target: { value: '1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]?.operationalContact?.phone).toBeNull();
  });

  it('F5: clearing an on-file phone sends phone: null as a genuine change', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({ allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'] });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-phone-10'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    expect(payload.instances?.[0]?.operationalContact?.phone).toBeNull();
  });

  it('F7: multi-campus — changing HN contact only leaves HCM operationalContact entirely absent', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const multi = form();
    multi.hasMixedCampusDetails = true;
    multi.visitScope = 'MULTI_CAMPUS';
    multi.campusVisits = [
      campusFixture({ allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'] }),
      campusFixture({
        visitInstanceId: 20, campusId: 2, campusCode: 'HCM', campusName: 'FPTU HCM',
        allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'],
        operationalContact: {
          fullName: 'Đầu Mối HCM', organization: 'ĐH XYZ', jobTitle: 'Trưởng phòng',
          phone: null, email: 'dm.hcm@x.vn',
          confirmationStatus: 'CONFIRMED', confirmationSource: 'EMAIL_CONFIRMATION', confirmedAt: '2026-08-01T09:00:00',
        },
      }),
    ];
    render(<VisitSafeEditModal form={multi} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-fullName-10'), { target: { value: 'Đầu Mối HN (đã sửa)' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(patchSafeDetails).mock.calls[0];
    const hcmPatch = payload.instances?.find(i => i.visitInstanceId === 20);
    expect(hcmPatch).toBeUndefined(); // HCM never touched — not even as an empty entry
  });

  it('F8: an invalid non-blank contact phone blocks Save with an inline error, never round-trips to the API', () => {
    const editable = form();
    editable.campusVisits[0] = campusFixture({ allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'] });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-phone-10'), { target: { value: '123-not-a-phone' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    expect(screen.getByTestId('safe-edit-contact-phone-error-10')).toBeInTheDocument();
    expect(patchSafeDetails).not.toHaveBeenCalled();
  });

  it('F9: a blank contact phone never shows a "required" error and does not block Save', async () => {
    vi.mocked(patchSafeDetails).mockResolvedValue({
      visitRequestId: 1, appliedChanges: [], requestRowVersion: 5, instanceRowVersions: {}, message: 'ok',
    });
    const editable = form();
    editable.campusVisits[0] = campusFixture({ allowedActions: ['UPDATE_OPERATIONAL_CONTACT_PROFILE'] });
    render(<VisitSafeEditModal form={editable} onClose={() => {}} onSaved={() => {}} />);

    fireEvent.change(screen.getByTestId('safe-edit-contact-phone-10'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(patchSafeDetails).toHaveBeenCalledTimes(1));
    expect(screen.queryByTestId('safe-edit-contact-phone-error-10')).toBeNull();
    expect(screen.queryByText(/required/i)).toBeNull();
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
