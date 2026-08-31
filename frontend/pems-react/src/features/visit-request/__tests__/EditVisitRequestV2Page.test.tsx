import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import EditVisitRequestV2Page from '../../../pages/dashboard/visit/EditVisitRequestV2Page';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => mockNavigate };
});

vi.mock('../api/visitRequestV2Api', () => ({
  getVisitRequestFormV2: vi.fn(),
  updatePendingVisitRequestV2: vi.fn(),
  resubmitVisitRequestV2: vi.fn(),
}));

// Real helpers except the success toast, which this screen must NOT raise: the message travels in
// router state and the detail screen shows it exactly once (fix plan §6).
vi.mock('../../../shared/utils/toast', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return { ...actual, showSuccessToast: vi.fn() };
});

vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => ({
    campuses: [
      { campusId: 1, campusCode: 'HN', campusName: 'FPTU Hà Nội', city: null },
      { campusId: 2, campusCode: 'HCM', campusName: 'FPTU Hồ Chí Minh', city: null },
    ],
    loading: false,
    error: false,
  }),
}));

/**
 * A live variable rather than a fixed return value: the page re-renders several times per test
 * (load → hydrate → user interaction), and `useAuthContext()` is read on every one of them.
 *
 * Default: a non-internal viewer — every existing test in this file exercises the pre-existing 72-hour
 * floor, so the new short-notice capability (PEMS_SHORT_NOTICE_72H_ALL_REGISTRANT_MUTATIONS added the
 * client-side `useAuthContext()` read this page now does) must stay OFF here; before that this file
 * needed no AuthProvider/mock at all. The short-notice-specific test below reassigns this before
 * rendering; `beforeEach` restores it.
 */
let mockAuthContextValue: { user: { email: string }; effectiveRole: string | null } = {
  user: { email: 'viewer-not-internal@example.com' },
  effectiveRole: 'VISITOR',
};
vi.mock('../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => mockAuthContextValue,
}));

// Excel parsing itself is out of scope here — only what a SUCCESSFUL import report does matters for
// the replace-block guard, so the parser is mocked to return one directly rather than exercising real
// XLSX binary parsing.
const mockReport = vi.fn();
vi.mock('../components/ExcelUpload/excelValidator', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../components/ExcelUpload/excelValidator')>();
  return { ...actual, validatePersonExcel: (...args: unknown[]) => mockReport(...args) };
});

import {
  getVisitRequestFormV2,
  updatePendingVisitRequestV2,
  resubmitVisitRequestV2,
} from '../api/visitRequestV2Api';
import { showSuccessToast } from '../../../shared/utils/toast';

const campus = (id: number, code: string, name: string, rowVersion: number, delegation: string) => ({
  visitInstanceId: id, campusId: id, campusCode: code, campusName: name,
  plannedStartAt: '2026-09-01T09:00:00', plannedEndAt: '2026-09-01T11:30:00', timezone: 'Asia/Ho_Chi_Minh',
  instanceStatus: 'PENDING', currentHostUserId: null, currentHostName: null, decidedByUserId: null,
  decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
  delegationName: delegation, visitType: 'MEETING', visitTypeOther: null, purpose: 'Trao đổi', workingContent: 'ND',
  visitors: [{ guestMemberId: id * 10, memberType: 'VISITOR', fullName: `Khách ${code}`, organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
  supportMembers: [],
  operationalContact: {
    fullName: `OP ${code}`, organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
    phone: '+84912345678', email: 'op@example.com',
    confirmationStatus: 'PENDING', confirmationSource: null, confirmedAt: null,
  },
  currentHost: null, proposedHost: null,
  hostSelection: { canProposeSelfAsHost: false, canProposeOtherHost: false, canWaitForLaterAssignment: false, canUpdateProposedHost: false },
  workingLanguage: 'VI', transportationNote: null, mediaConsentStatus: 'DECLINED',
  notes: `Ghi chú ${code}`,
  formRevision: 1, approvalRevision: 0, rowVersion, activeAmendment: null,
  cancelledByUserId: null, cancelledByName: null, cancelledAt: null,
  cancellationActorType: null, cancellationSource: null, cancellationReason: null,
});

const form = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => ({
  visitRequestId: 5, requestCode: 'VR-5', rowVersion: 7,
  hasMixedCampusDetails: false, visitScope: 'SINGLE_CAMPUS', requestStatus: 'PENDING_APPROVAL',
  createdSource: 'PUBLIC', submittedAt: '2026-07-15T08:00:00', partnerId: null,
  cancelledByUserId: null, cancelledByName: null, cancelledAt: null, cancellationReason: null,
  registrant: { fullName: 'Reg', organization: 'ĐH X', jobTitle: 'TP', phone: '+84912345678', email: 'reg@x.vn', nationality: 'VN' },
  confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },

  // Full-request scope in this fixture, so the backend sends the request-wide verdict.

  requestOutcome: { code: 'ALL_WAITING', total: 1, accepted: 0, inProgress: 0, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
  campusVisits: [campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN')],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
  ...overrides,
});

const renderAt = (mode: 'edit' | 'resubmit', path = `/dashboard/visit/v2/5/${mode}`) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/dashboard/visit/v2/:visitRequestId/edit" element={<EditVisitRequestV2Page mode="edit" />} />
        <Route path="/dashboard/visit/v2/:visitRequestId/resubmit" element={<EditVisitRequestV2Page mode="resubmit" />} />
      </Routes>
    </MemoryRouter>,
  );

describe('EditVisitRequestV2Page', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockAuthContextValue = { user: { email: 'viewer-not-internal@example.com' }, effectiveRole: 'VISITOR' };
  });

  /**
   * PEMS_SHORT_NOTICE_72H_ALL_REGISTRANT_MUTATIONS: an internal (Staff) registrant submits the SAME
   * fixture schedule (`campus()`'s fixed `2026-09-01T09:00:00`, ~1 day out — well inside 72h of real
   * wall-clock "now") that the default VISITOR-role tests in this file are held to, and the save must
   * go through with no local validation error (`minAdvanceHours` is 0 for this actor, not 72). Proves
   * the schema-level block is lifted for this actor rather than merely not-yet-triggered.
   */
  it('lets an internal (Staff) registrant save a schedule inside 72 hours with no local error', async () => {
    mockAuthContextValue = { user: { email: 'staff-registrant@example.com' }, effectiveRole: 'STAFF' };
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    vi.mocked(updatePendingVisitRequestV2).mockResolvedValue({
      visitRequestId: 5, status: 'PENDING_APPROVAL', visitScope: 'SINGLE_CAMPUS',
      hasMixedCampusDetails: false, requestRowVersion: 8, instances: [], message: 'Đã cập nhật',
    } as never);

    renderAt('edit');
    expect(await screen.findByDisplayValue('Đoàn HN')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Save changes/ }));

    // The API call itself is the proof: react-hook-form's handleSubmit only reaches the SUBMIT
    // handler (which calls updatePendingVisitRequestV2) when the resolver found zero errors — a
    // schema still enforcing 72h here would have gone down the invalid path instead and never called
    // this at all (see the sibling VISITOR-role tests in this file, which hit exactly that today).
    await waitFor(() => expect(updatePendingVisitRequestV2).toHaveBeenCalledTimes(1));
  });

  it('hydrates and submits a pending-edit payload carrying request + per-instance row versions', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    vi.mocked(updatePendingVisitRequestV2).mockResolvedValue({
      visitRequestId: 5, status: 'PENDING_APPROVAL', visitScope: 'SINGLE_CAMPUS',
      hasMixedCampusDetails: false, requestRowVersion: 8, instances: [], message: 'Đã cập nhật',
    } as never);

    renderAt('edit');
    expect(await screen.findByDisplayValue('Đoàn HN')).toBeInTheDocument();
    // Registrant + contact emails are read-only (account-binding, immutable on edit):
    expect((screen.getByDisplayValue('reg@x.vn') as HTMLInputElement).readOnly).toBe(true);

    fireEvent.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(updatePendingVisitRequestV2).toHaveBeenCalledTimes(1));
    const [reqId, payload] = vi.mocked(updatePendingVisitRequestV2).mock.calls[0];
    expect(reqId).toBe(5);
    expect(payload.expectedRequestRowVersion).toBe(7);
    expect(payload.campusVisits[0].visitInstanceId).toBe(1);
    expect(payload.campusVisits[0].expectedRowVersion).toBe(4);
    expect(resubmitVisitRequestV2).not.toHaveBeenCalled();
    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/dashboard/visit/v2/5', expect.anything()));
  });

  it('resubmit keeps the campus set fixed (no add-campus button) and calls the resubmit endpoint', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ requestStatus: 'REJECTED' }));
    vi.mocked(resubmitVisitRequestV2).mockResolvedValue({
      visitRequestId: 5, status: 'PENDING_APPROVAL', visitScope: 'SINGLE_CAMPUS',
      hasMixedCampusDetails: false, requestRowVersion: 8, instances: [], message: 'Đã gửi lại',
    } as never);

    renderAt('resubmit');
    expect(await screen.findByDisplayValue('Đoàn HN')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Add campus/ })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Resubmit request/ }));
    await waitFor(() => expect(resubmitVisitRequestV2).toHaveBeenCalledTimes(1));
    expect(updatePendingVisitRequestV2).not.toHaveBeenCalled();
  });

  /**
   * The campus set is chosen once, at create, and is fixed from the moment the request exists — for
   * editing as well as for resubmitting. This screen used to offer both an "Add campus" button and a
   * per-card "Remove this campus"; a request's identity (its scope, its fingerprint, the invitations
   * already sent to a campus about to be dropped) is not something an edit gets to rewrite underneath
   * everyone holding a link to it.
   *
   * The backend refuses a payload whose campus set differs from the stored one, so this asserts the UI
   * agreeing with the rule rather than the rule itself.
   */
  it('offers no way to add or remove a campus while editing (TC-CAMPUS-IMMUTABLE-01)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({
      visitScope: 'MULTI_CAMPUS',
      campusVisits: [campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN'), campus(2, 'HCM', 'FPTU Hồ Chí Minh', 2, 'Đoàn HCM')],
    }));

    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');

    expect(screen.queryByRole('button', { name: /Add campus/ })).not.toBeInTheDocument();
    // Two campuses on screen, and neither carries a remove control — the multi-campus case is the one
    // where a remove button would have been offered at all.
    expect(screen.queryAllByLabelText(/Remove this campus/)).toHaveLength(0);
  });

  it('shows a stable conflict message and a reload action on a 409', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    const conflict = Object.assign(new Error('409'), {
      isAxiosError: true,
      response: { status: 409, data: { errorCode: 'VISIT_REQUEST_VERSION_CONFLICT', message: 'conflict' } },
    });
    vi.mocked(updatePendingVisitRequestV2).mockRejectedValue(conflict);

    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');
    fireEvent.click(screen.getByRole('button', { name: /Save changes/ }));

    expect(await screen.findByText(/updated elsewhere/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Reload latest data/ })).toBeInTheDocument();
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  it('blocks a non-manager (backend still re-authorizes)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
    }));
    renderAt('edit');
    expect(await screen.findByRole('alert')).toHaveTextContent(/not allowed/i);
    expect(screen.queryByRole('button', { name: /Save changes/ })).not.toBeInTheDocument();
  });

  it('edit route on a REJECTED request shows a not-editable notice (status/mode mismatch)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ requestStatus: 'REJECTED' }));
    renderAt('edit');
    expect(await screen.findByRole('alert')).toHaveTextContent(/no longer editable/i);
  });

  // ── PENDING_CONTACT_CONFIRMATION edit-route gate (route policy drift fix) ───────────────────────
  //
  // A request whose campuses are still waiting for their operational contact
  // (PENDING_CONTACT_CONFIRMATION) is exactly as un-decided as one waiting for Staff Leader approval
  // (PENDING_APPROVAL) — UpdatePendingVisitRequestV2CommandHandler accepts a pending-edit on either
  // (VisitMutationGuard.EnsureRequestLevelAllowed against WAITING_CONTACT_CONFIRMATION /
  // WAITING_REQUEST_APPROVAL campuses). This screen's own EDITABLE_STATUSES set used to omit
  // PENDING_CONTACT_CONFIRMATION, so the registrant hit "This request is no longer editable" even
  // though the backend would have accepted their save — a frontend-only policy drift.
  describe('PENDING_CONTACT_CONFIRMATION is edit-compatible (route policy drift fix)', () => {
    it('opens the edit form for the reported case: PENDING_CONTACT_CONFIRMATION + REGISTRANT (T01)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({
        requestStatus: 'PENDING_CONTACT_CONFIRMATION',
        campusVisits: [{
          ...campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN'),
          instanceStatus: 'WAITING_CONTACT_CONFIRMATION',
        }],
      }));

      renderAt('edit');

      expect(await screen.findByDisplayValue('Đoàn HN')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Save changes/ })).toBeInTheDocument();
      expect(screen.queryByText('This request is no longer editable. Please go back.')).not.toBeInTheDocument();
    });

    // Technical debt: bare "PENDING" is not among the canonical request statuses in
    // VisitRequestStatuses (backend) — only PENDING_CONTACT_CONFIRMATION / PENDING_APPROVAL /
    // PARTIALLY_APPROVED / APPROVED / REJECTED / CANCELLED. Kept here as a legacy compatibility
    // alias rather than removed, since proving no caller/fixture still relies on it is outside this
    // fix's scope; this test documents and pins the current (kept) behavior.
    it('legacy "PENDING" status still opens the edit form (T03)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ requestStatus: 'PENDING' }));
      renderAt('edit');
      expect(await screen.findByDisplayValue('Đoàn HN')).toBeInTheDocument();
    });

    it.each(['APPROVED', 'CANCELLED'])('%s does not open the pending-edit form (T06)', async requestStatus => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ requestStatus }));
      renderAt('edit');
      expect(await screen.findByRole('alert')).toHaveTextContent(/no longer editable/i);
      expect(screen.queryByRole('button', { name: /Save changes/ })).not.toBeInTheDocument();
    });

    it('a backend GET refusal (403) is shown as-is, never bypassed by the local status set (T07)', async () => {
      const forbidden = Object.assign(new Error('403'), {
        isAxiosError: true,
        response: { status: 403, data: { message: 'forbidden' } },
      });
      vi.mocked(getVisitRequestFormV2).mockRejectedValue(forbidden);

      renderAt('edit');

      expect(await screen.findByRole('alert')).toBeInTheDocument();
      expect(screen.queryByDisplayValue('Đoàn HN')).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /Save changes/ })).not.toBeInTheDocument();
    });

    // VISITOR_OWNER is a legacy relation string (VisitInstanceAccess.cs: "replaces the old
    // request-wide VISITOR_OWNER") that this endpoint's own read model
    // (VisitFormReadService.ComputeScopeAsync) never actually returns for a whole-request viewer —
    // only "REGISTRANT" is. This test pins the current (unchanged, pre-existing) behavior as a
    // harmless no-op rather than an intentional widening of who may edit (T08).
    it('VISITOR_OWNER relation is accepted unchanged — legacy no-op, not a widened grant (T08)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({
        requestStatus: 'PENDING_CONTACT_CONFIRMATION',
        viewer: { relation: 'VISITOR_OWNER', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
      }));
      renderAt('edit');
      expect(await screen.findByDisplayValue('Đoàn HN')).toBeInTheDocument();
    });
  });

  // ── The campus PICKER, on cards that already exist ────────────────────────────────────────────
  //
  // The "campus ceiling" suite that used to live here exercised the Add-campus button: how it counted
  // against the campuses open for registration, what a freshly-added card offered, and which options
  // it excluded. None of that has a subject any more — a campus cannot be added to a request that
  // exists — so it was removed rather than rewritten around a button that is gone. What remains worth
  // asserting is that an EXISTING card still cannot be pointed at a different campus.
  describe('an existing campus card cannot be pointed at a different campus', () => {
    /** Campus selects are the ones offering the "select a campus" placeholder. */
    const campusSelects = () =>
      screen.getAllByRole('combobox').filter(el =>
        el.tagName === 'SELECT'
        && Array.from((el as HTMLSelectElement).options)
          .some(o => o.value === '' && /Select campus/i.test(o.text)));

    it('offers exactly one campus select per existing campus, already answered (TC-CAMPUS-IMMUTABLE-02)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      const selects = campusSelects();
      expect(selects).toHaveLength(1);
      expect((selects[0] as HTMLSelectElement).value).toBe('HN');
    });

    it('does not offer a campus that is already part of the request (TC-CAMPUS-04)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({
        visitScope: 'MULTI_CAMPUS',
        campusVisits: [campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN'), campus(2, 'HCM', 'FPTU Hồ Chí Minh', 2, 'Đoàn HCM')],
      }));
      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      await waitFor(() => expect(campusSelects()).toHaveLength(2));
      // The second card holds HCM, so HN — held by the first — is not on its menu.
      const optionValues = Array.from((campusSelects()[1] as HTMLSelectElement).options).map(o => o.value);
      expect(optionValues).not.toContain('HN');
    });
  });

  // ── Contact separation (repair v3 §2.1, §17 UI) ──────────────────────────────────────────────
  //
  // Editing a visit request and managing its operational contact are two workflows. This screen owns
  // the first and must not offer any part of the second — not an input, not a disabled input, and not
  // a button that jumps to it. A read-only summary is what remains, because who coordinates a campus
  // is worth SEEING while editing it.
  it('shows an existing campus contact read-only, with no editable field (TC-CONTACT-UI-01)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');

    const summary = screen.getByTestId('campus-opcontact-readonly-0');
    expect(summary).toBeInTheDocument();
    expect(summary.querySelector('input')).toBeNull();
    expect(screen.getByTestId('campus-opcontact-readonly-email-0').textContent).toContain('op@example.com');
    expect(screen.getByTestId('campus-opcontact-readonly-jobTitle-0').textContent)
      .toContain('Trưởng phòng Hợp tác');

    // None of the five fields exists as a control on this screen any more.
    for (const id of [
      'campus-opcontact-email-0', 'campus-opcontact-phone-0',
      'campus-opcontact-name', 'campus-opcontact-org', 'campus-opcontact-jobtitle',
    ]) {
      expect(screen.queryByTestId(id)).not.toBeInTheDocument();
    }
  });

  it('offers no contact-management control in the edit form at all (TC-CONTACT-UI-02)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');

    // The "Thay đổi đầu mối" button and its confirm dialog are gone: a second door into a workflow
    // that has one is how contact editing kept leaking back into the request form.
    expect(screen.queryByTestId('campus-opcontact-change-0')).not.toBeInTheDocument();
    expect(screen.queryByTestId('v2e-contact-change-confirm')).not.toBeInTheDocument();
    // Quick-fill writes contact fields, so it has nothing to do here either.
    expect(screen.queryByTestId('campus-opcontact-use-registrant-0')).not.toBeInTheDocument();
  });

  // ── Field-contract audit: Phone optional + legacy contact does not block unrelated edits ──────
  // See PEMS_FULL_VISIT_V2_FIELD_CONTRACT_AUDIT_FIX.md.

  it('does not mark the registrant Phone field as required, and a blank phone does not block Save (PHONE-02/05)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    vi.mocked(updatePendingVisitRequestV2).mockResolvedValue({
      visitRequestId: 5, status: 'PENDING_APPROVAL', visitScope: 'SINGLE_CAMPUS',
      hasMixedCampusDetails: false, requestRowVersion: 8, instances: [], message: 'Đã cập nhật',
    } as never);

    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');

    const phoneInput = screen.getByTestId('v2e-registrant-phone');
    const fieldRoot = phoneInput.closest('.relative')?.parentElement;
    expect(fieldRoot?.querySelector('label')?.textContent).not.toMatch(/\*/);

    fireEvent.change(phoneInput, { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(updatePendingVisitRequestV2).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(updatePendingVisitRequestV2).mock.calls[0];
    expect(payload.registrant.phone).toBeFalsy();
  });

  it('shows a legacy-incomplete contact as an informational notice, not a form error, and still saves an unrelated field change (LEGACY-CONTACT-01/02)', async () => {
    const legacyCampus = campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN');
    legacyCampus.operationalContact = { ...legacyCampus.operationalContact, organization: '' };
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ campusVisits: [legacyCampus] }));
    vi.mocked(updatePendingVisitRequestV2).mockResolvedValue({
      visitRequestId: 5, status: 'PENDING_APPROVAL', visitScope: 'SINGLE_CAMPUS',
      hasMixedCampusDetails: false, requestRowVersion: 8, instances: [], message: 'Đã cập nhật',
    } as never);

    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');

    // "—" placeholder and an amber notice, never a red validation error, for the field this screen
    // cannot fix.
    expect(screen.getByTestId('campus-opcontact-readonly-organization-0').textContent).toBe('—');
    expect(screen.getByTestId('campus-opcontact-legacy-warning-0')).toBeInTheDocument();

    // Editing an UNRELATED field (Purpose) and saving must not be blocked by the legacy gap.
    const purposeField = screen.getByDisplayValue('Trao đổi');
    fireEvent.change(purposeField, { target: { value: 'Trao đổi hợp tác' } });
    fireEvent.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(updatePendingVisitRequestV2).toHaveBeenCalledTimes(1));
    const [, payload] = vi.mocked(updatePendingVisitRequestV2).mock.calls[0];
    expect(payload.campusVisits[0].purpose).toBe('Trao đổi hợp tác');
    expect(payload.campusVisits[0].operationalContact.organization).toBe('');
  });

  // ── Success feedback (fix plan §6) ───────────────────────────────────────────────────────────
  // The form raises NO toast of its own: the message travels in router state and the detail screen is
  // its single owner. Two owners is exactly how one save produced two identical toasts.
  it('hands the success message to the detail screen instead of toasting it here (TC-TOAST-01)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    vi.mocked(updatePendingVisitRequestV2).mockResolvedValue({
      visitRequestId: 5, status: 'PENDING_APPROVAL', visitScope: 'SINGLE_CAMPUS',
      hasMixedCampusDetails: false, requestRowVersion: 8, instances: [], message: 'Đã cập nhật',
    } as never);

    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');
    fireEvent.click(screen.getByRole('button', { name: /Save changes/ }));

    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith(
      '/dashboard/visit/v2/5',
      { replace: true, state: { flash: 'Đã cập nhật' } },
    ));
    expect(showSuccessToast).not.toHaveBeenCalled();
  });

  // Pins the same regression VisitRequestFormV2CopyApply.test.tsx pins for create mode:
  // useFieldArray.update()/.replace() patch the underlying RHF values correctly, but
  // register()-bound inputs and nested field arrays (visitors) only re-read fresh values on
  // mount. This screen has its OWN copy/apply-to-all handlers (not the create-mode hook), so it
  // needs its own remount trigger — without it, the copy looks like a no-op on screen even though
  // form.getValues() already has the copied content.
  describe('copy / apply-to-all actually reach the screen', () => {
    const visitTypeSelects = () =>
      screen.getAllByRole('combobox').filter(el =>
        el.tagName === 'SELECT'
        && Array.from((el as HTMLSelectElement).options).some(o => o.value === 'MEETING'));

    it('"Copy content from" fills the register()-bound visit-type select AND the nested visitor row on the target card', async () => {
      const hn = campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN');
      const hcm = { ...campus(2, 'HCM', 'FPTU Hồ Chí Minh', 2, 'Đoàn HCM'), visitType: 'WORKSHOP' };
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ visitScope: 'MULTI_CAMPUS', campusVisits: [hn, hcm] }));

      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      // Before the copy: card 2 (HCM) still shows its own content.
      expect(visitTypeSelects()[1]).toHaveValue('WORKSHOP');
      expect(within(screen.getAllByTestId('v2-visitors-table')[1]).getByDisplayValue('Khách HCM')).toBeInTheDocument();

      const copySelect = document.querySelector('select[id^="copy-src-"]') as HTMLSelectElement;
      expect(copySelect).toBeTruthy();
      fireEvent.change(copySelect, { target: { value: '0' } });

      await waitFor(() => {
        expect(screen.getAllByTestId('campus-delegation-input')[1]).toHaveValue('Đoàn HN');
        expect(visitTypeSelects()[1]).toHaveValue('MEETING');
        expect(within(screen.getAllByTestId('v2-visitors-table')[1]).getByDisplayValue('Khách HN')).toBeInTheDocument();
      });
    }, 15000);

    it('"Apply to other campuses" (confirmed) reaches the other card on screen, not just form state', async () => {
      const hn = campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN');
      const hcm = { ...campus(2, 'HCM', 'FPTU Hồ Chí Minh', 2, 'Đoàn HCM'), visitType: 'WORKSHOP' };
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ visitScope: 'MULTI_CAMPUS', campusVisits: [hn, hcm] }));

      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      fireEvent.click(screen.getAllByRole('button', { name: 'Apply to other campuses' })[0]);
      fireEvent.click(screen.getByRole('button', { name: 'Apply' }));

      await waitFor(() => {
        expect(screen.getAllByTestId('campus-delegation-input')[1]).toHaveValue('Đoàn HN');
        expect(visitTypeSelects()[1]).toHaveValue('MEETING');
        expect(within(screen.getAllByTestId('v2-visitors-table')[1]).getByDisplayValue('Khách HN')).toBeInTheDocument();
      });
    }, 15000);
  });

  // ── Operational-contact consistency fix: Copy/Apply-To-All must never let a persisted target's OWN
  // contact relation/snapshot be overwritten by the SOURCE campus's, and must block the whole bulk
  // operation before any mutation when a target's linked member would be orphaned. ──
  describe('Copy / Apply-To-All preserve each target campus’s own Operational Contact', () => {
    /** A campus whose visitor IS the linked Operational Contact (fullName mirrors the contact's). */
    const linkedCampus = (id: number, code: string, name: string, delegation: string, contactName: string) => {
      const c = campus(id, code, name, id, delegation);
      c.visitors = [{
        guestMemberId: id * 10, memberType: 'VISITOR', fullName: contactName,
        organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1,
      }];
      c.operationalContact = {
        ...c.operationalContact, fullName: contactName, guestMemberId: id * 10,
      } as typeof c.operationalContact & { guestMemberId: number };
      return c;
    };

    it('"Copy content from" copies business content but leaves an UNLINKED target’s own contact untouched (COPY-FE)', async () => {
      // The target's member list is always FULLY REPLACED by the copy (it becomes an independent clone
      // of the source's), so a LINKED target can never safely survive a cross-campus copy at all — that
      // is the atomicity case below. An UNLINKED target has nothing to orphan, so the copy is safe; what
      // must still be proven is that the target's OWN free-text contact ("Lee") is not silently
      // overwritten by the source's ("Kim") the way cloneCampusVisitContent alone would do.
      const hn = linkedCampus(1, 'HN', 'FPTU Hà Nội', 'Đoàn HN', 'Kim');
      const hcm = campus(2, 'HCM', 'FPTU Hồ Chí Minh', 2, 'Đoàn HCM');
      hcm.operationalContact = {
        ...hcm.operationalContact, fullName: 'Lee', guestMemberId: null,
      } as typeof hcm.operationalContact & { guestMemberId: number | null };
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ visitScope: 'MULTI_CAMPUS', campusVisits: [hn, hcm] }));

      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');
      // Before the copy: HCM's own contact is Lee.
      expect(screen.getByTestId('campus-opcontact-readonly-fullName-1').textContent).toBe('Lee');

      const copySelect = document.querySelector('select[id^="copy-src-"]') as HTMLSelectElement;
      fireEvent.change(copySelect, { target: { value: '0' } });

      await waitFor(() => {
        // Business content copied from HN.
        expect(screen.getAllByTestId('campus-delegation-input')[1]).toHaveValue('Đoàn HN');
        expect(within(screen.getAllByTestId('v2-visitors-table')[1]).getByDisplayValue('Kim')).toBeInTheDocument();
      });
      // The target's OWN contact snapshot survives the copy — never silently repointed at Kim.
      expect(screen.getByTestId('campus-opcontact-readonly-fullName-1').textContent).toBe('Lee');
    }, 15000);

    it('Apply-To-All blocks the ENTIRE operation, with zero mutation, when one target’s linked member would be orphaned (APPLY-FE)', async () => {
      const hn = linkedCampus(1, 'HN', 'FPTU Hà Nội', 'Đoàn HN', 'Kim');
      // HCM's own linked member ("Lee") does not appear anywhere in HN's member list — copying HN's
      // content onto HCM would orphan HCM's relation.
      const hcm = linkedCampus(2, 'HCM', 'FPTU Hồ Chí Minh', 'Đoàn HCM', 'Lee');
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ visitScope: 'MULTI_CAMPUS', campusVisits: [hn, hcm] }));

      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      fireEvent.click(screen.getAllByRole('button', { name: 'Apply to other campuses' })[0]);
      fireEvent.click(screen.getByRole('button', { name: 'Apply' }));

      // Give any (incorrect) async mutation a chance to land, then assert NOTHING changed: not HCM's
      // business content, not its own contact — the whole operation must be atomic, never a partial
      // "some targets updated" outcome.
      await new Promise(resolve => setTimeout(resolve, 50));
      expect(screen.getAllByTestId('campus-delegation-input')[1]).toHaveValue('Đoàn HCM');
      expect(screen.getByTestId('campus-opcontact-readonly-fullName-1').textContent).toBe('Lee');
      expect(within(screen.getAllByTestId('v2-visitors-table')[1]).getByDisplayValue('Lee')).toBeInTheDocument();
    }, 15000);
  });

  // ── Persisted Excel Replace: block before mutation, never clear-and-warn (operational-contact
  // consistency fix). A DRAFT campus still gets the softer clear+recover flow — this screen only ever
  // renders PERSISTED campuses (contactReadOnly={instanceId != null} unconditionally), so every replace
  // here is the strict one. ──
  describe('Excel "Replace all" on a persisted, linked campus', () => {
    const linkedCampus = (id: number, code: string, name: string, delegation: string, contactName: string) => {
      const c = campus(id, code, name, id, delegation);
      c.visitors = [{
        guestMemberId: id * 10, memberType: 'VISITOR', fullName: contactName,
        organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1,
      }];
      c.operationalContact = {
        ...c.operationalContact, fullName: contactName, guestMemberId: id * 10,
      } as typeof c.operationalContact & { guestMemberId: number };
      return c;
    };

    const fireReplaceImport = (rows: { fullName: string; jobTitle: string; organization: string; nationality: string }[]) => {
      const fileInput = document.querySelectorAll('input[type="file"]')[0] as HTMLInputElement;
      const file = new File(['x'], 'members.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      Object.defineProperty(fileInput, 'files', { value: [file], configurable: true });
      fireEvent.change(fileInput);
    };

    it('blocks the whole replace before any mutation when it would orphan the linked contact', async () => {
      const hn = linkedCampus(1, 'HN', 'FPTU Hà Nội', 'Đoàn HN', 'Kim');
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ campusVisits: [hn] }));
      mockReport.mockResolvedValue({
        fileName: 'members.xlsx', kind: 'visitors', checkedAt: '', totalRows: 1, validRows: 1,
        errorRows: 0, duplicateRows: 0, overLimitRows: 0, remainingSlots: 10, resultingCount: 2,
        errors: [],
        // "Kim" (the linked contact) is NOT in the replacement set — replacing would orphan the link.
        data: [{ fullName: 'Guest Z', jobTitle: 'GV', organization: 'ĐH Z', nationality: 'VN' }],
      });

      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');
      fireReplaceImport([{ fullName: 'Guest Z', jobTitle: 'GV', organization: 'ĐH Z', nationality: 'VN' }]);

      // The import itself always APPENDS first (an append never orphans anyone, so it is never
      // blocked) — the list is [Kim, Guest Z] before "Replace all" is even clicked. What "Replace all"
      // would do is throw this away and keep ONLY the imported rows, which is exactly what must be
      // blocked here.
      const table = () => screen.getByTestId('v2-visitors-table');
      await waitFor(() => expect(within(table()).getByDisplayValue('Guest Z')).toBeInTheDocument());
      const rowCountBeforeReplace = within(table()).getAllByRole('row').length;

      fireEvent.click(await screen.findByTestId('v2-visitors-replace'));
      fireEvent.click(await screen.findByTestId('v2-replace-confirm-yes-visitors'));

      // Blocked: the replace action itself changes nothing — same row count, Kim still present, the
      // contact snapshot untouched. (The earlier append is real content, not part of this guard.)
      await new Promise(resolve => setTimeout(resolve, 50));
      expect(within(table()).getAllByRole('row').length).toBe(rowCountBeforeReplace);
      expect(within(table()).getByDisplayValue('Kim')).toBeInTheDocument();
      expect(screen.getByTestId('campus-opcontact-readonly-fullName-0').textContent).toBe('Kim');
    });

    it('an Excel row describing the same person by name is still blocked — no proven persisted identity, never trusted by name alone', async () => {
      const hn = linkedCampus(1, 'HN', 'FPTU Hà Nội', 'Đoàn HN', 'Kim');
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ campusVisits: [hn] }));
      mockReport.mockResolvedValue({
        fileName: 'members.xlsx', kind: 'visitors', checkedAt: '', totalRows: 1, validRows: 1,
        errorRows: 0, duplicateRows: 0, overLimitRows: 0, remainingSlots: 10, resultingCount: 1,
        errors: [],
        // A plain Excel row NEVER carries a persisted GuestMemberId (imports are always free text) —
        // even one that happens to name "Kim" is a brand-new, unproven row, not continuity evidence.
        data: [{ fullName: 'Kim', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }],
      });

      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');
      fireReplaceImport([{ fullName: 'Kim', jobTitle: 'GV', organization: 'ĐH X', nationality: 'VN' }]);

      // The append lands a SECOND "Kim" row (a name-alike, not the same proven row) — two rows now.
      const table = () => screen.getByTestId('v2-visitors-table');
      await waitFor(() => expect(within(table()).getAllByDisplayValue('Kim')).toHaveLength(2));

      fireEvent.click(await screen.findByTestId('v2-visitors-replace'));
      fireEvent.click(await screen.findByTestId('v2-replace-confirm-yes-visitors'));

      // Still blocked, still two rows — the replace never collapsed them down to the imported one.
      await new Promise(resolve => setTimeout(resolve, 50));
      expect(within(table()).getAllByDisplayValue('Kim')).toHaveLength(2);
      expect(screen.getByTestId('campus-opcontact-readonly-fullName-0').textContent).toBe('Kim');
    });

    it('a replace on the UNLINKED section (support team) is unaffected — the guard is scoped, not a blanket freeze', async () => {
      const hn = linkedCampus(1, 'HN', 'FPTU Hà Nội', 'Đoàn HN', 'Kim'); // linked via VISITORS
      hn.supportMembers = [{
        guestMemberId: 999, memberType: 'EXTERNAL_SUPPORT', fullName: 'Old Support',
        organization: 'ĐH X', jobTitle: '', nationality: 'VN', displayOrder: 1,
      }];
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({ campusVisits: [hn] }));
      mockReport.mockResolvedValue({
        fileName: 'support.xlsx', kind: 'supportTeam', checkedAt: '', totalRows: 1, validRows: 1,
        errorRows: 0, duplicateRows: 0, overLimitRows: 0, remainingSlots: 10, resultingCount: 1,
        errors: [],
        data: [{ fullName: 'New Support', jobTitle: '', organization: 'ĐH X', nationality: 'VN' }],
      });

      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');
      // The support-team file input is the second file input on this single-campus card.
      const fileInput = document.querySelectorAll('input[type="file"]')[1] as HTMLInputElement;
      const file = new File(['x'], 'support.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' });
      Object.defineProperty(fileInput, 'files', { value: [file], configurable: true });
      fireEvent.change(fileInput);

      fireEvent.click(await screen.findByTestId('v2-support-replace'));
      fireEvent.click(await screen.findByTestId('v2-replace-confirm-yes-supportTeam'));

      // Applies normally: the contact is linked to a VISITOR row, untouched by a support-only replace.
      await waitFor(() => {
        expect(within(screen.getByTestId('v2-supportTeam-table')).getByDisplayValue('New Support')).toBeInTheDocument();
      });
      expect(screen.getByTestId('campus-opcontact-readonly-fullName-0').textContent).toBe('Kim');
      expect(within(screen.getByTestId('v2-visitors-table')).getByDisplayValue('Kim')).toBeInTheDocument();
    });
  });
});
