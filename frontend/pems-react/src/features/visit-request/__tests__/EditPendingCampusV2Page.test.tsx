import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';
import EditPendingCampusV2Page from '../../../pages/dashboard/visit/EditPendingCampusV2Page';
import type { ResolvedCampusVisit, ResolvedVisitForm } from '../api/visitRequestV2Api';

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => mockNavigate };
});

vi.mock('../api/visitRequestV2Api', () => ({
  getVisitRequestFormV2: vi.fn(),
  updatePendingVisitInstance: vi.fn(),
}));

vi.mock('../../delegations/api/delegationsApi', () => ({
  delegationsApi: { getHostCandidates: vi.fn() },
}));

vi.mock('../../../shared/utils/toast', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return { ...actual, showInfoToast: vi.fn(actual.showInfoToast) };
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

import { getVisitRequestFormV2, updatePendingVisitInstance } from '../api/visitRequestV2Api';
import { delegationsApi } from '../../delegations/api/delegationsApi';
import { showInfoToast } from '../../../shared/utils/toast';

/** Far enough out that the 72-hour floor is satisfied by the EXISTING schedule. */
const FAR_START = '2027-09-01T09:00:00';
const FAR_END = '2027-09-01T11:30:00';

const campus = (
  id: number,
  code: string,
  name: string,
  status: string,
  allowedActions: string[],
  overrides: Partial<ResolvedCampusVisit> = {},
): ResolvedCampusVisit => ({
  visitInstanceId: id, campusId: id, campusCode: code, campusName: name,
  plannedStartAt: FAR_START, plannedEndAt: FAR_END,
  instanceStatus: status, currentHostUserId: null, currentHostName: null, decidedByUserId: null,
  decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
  delegationName: `Đoàn ${code}`, visitType: 'MEETING', visitTypeOther: null,
  purpose: 'Trao đổi hợp tác', workingContent: 'Nội dung làm việc',
  visitors: [{ guestMemberId: id * 10, memberType: 'VISITOR', fullName: `Khách ${code}`, organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
  supportMembers: [],
  operationalContact: {
    fullName: `OP ${code}`, organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
    phone: '+84912345678', email: 'op@example.com',
    confirmationStatus: 'CONFIRMED', confirmationSource: null, confirmedAt: null,
  },
  currentHost: null, proposedHost: null,
  hostSelection: { canProposeSelfAsHost: false, canProposeOtherHost: false, canWaitForLaterAssignment: false, canUpdateProposedHost: false },
  workingLanguage: 'VI', transportationNote: null, mediaConsentStatus: 'DECLINED',
  notes: `Ghi chú ${code}`,
  formRevision: 1, approvalRevision: 0, rowVersion: 4, activeAmendment: null,
  cancelledByUserId: null, cancelledByName: null, cancelledAt: null,
  cancellationActorType: null, cancellationSource: null, cancellationReason: null,
  allowedActions,
  ...overrides,
} as ResolvedCampusVisit);

/**
 * The shape this screen exists for: HN already ASSIGNED, HCM still waiting. The request aggregate reads
 * PARTIALLY_APPROVED, which is exactly the state in which the whole-request edit is refused.
 */
const mixedForm = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => ({
  visitRequestId: 5, requestCode: 'VR-5', rowVersion: 7,
  hasMixedCampusDetails: true, visitScope: 'MULTI_CAMPUS', requestStatus: 'PARTIALLY_APPROVED',
  createdSource: 'PUBLIC', submittedAt: '2026-07-15T08:00:00', partnerId: null,
  cancelledByUserId: null, cancelledByName: null, cancelledAt: null, cancellationReason: null,
  registrant: { fullName: 'Reg', organization: 'ĐH X', jobTitle: 'TP', phone: '+84912345678', email: 'reg@x.vn', nationality: 'VN' },
  confirmationSummary: { total: 2, confirmed: 2, pending: 0, declined: 0, expired: 0, gateOpen: true },
  requestOutcome: { code: 'IN_PROGRESS', total: 2, accepted: 1, inProgress: 0, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
  campusVisits: [
    campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
    campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS']),
  ],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
  ...overrides,
});

const renderPage = (instanceId = 2) =>
  render(
    <MemoryRouter initialEntries={[`/dashboard/visit/v2/5/campus/${instanceId}/edit`]}>
      <Routes>
        <Route
          path="/dashboard/visit/v2/:visitRequestId/campus/:visitInstanceId/edit"
          element={<EditPendingCampusV2Page />}
        />
      </Routes>
    </MemoryRouter>,
  );

const axiosError = (status: number, errorCode: string, message = 'nope') =>
  Object.assign(new Error(String(status)), {
    isAxiosError: true,
    response: { status, data: { errorCode, message } },
  });

describe('EditPendingCampusV2Page', () => {
  beforeEach(() => vi.clearAllMocks());

  /**
   * The dead end this screen removes. On a mixed request the whole-request edit is refused because a
   * sibling has been decided, and before this existed the campus still WAITING had no action at all.
   */
  it('opens on the waiting campus of a mixed request and edits only that campus', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    vi.mocked(updatePendingVisitInstance).mockResolvedValue({
      visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
      visitInstanceStatus: 'WAITING_REQUEST_APPROVAL', instanceRowVersion: 5, requestRowVersion: 8,
      approved: false, hostUserId: null, message: 'Đã cập nhật',
    } as never);

    renderPage();
    expect(await screen.findByDisplayValue('Đoàn HCM')).toBeInTheDocument();
    // Only the named campus is on screen — a sibling appearing here would be a sibling this form could
    // overwrite.
    expect(screen.queryByDisplayValue('Đoàn HN')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(1));
    const [requestId, instanceId, body] = vi.mocked(updatePendingVisitInstance).mock.calls[0];
    expect(requestId).toBe(5);
    expect(instanceId).toBe(2);
    // The INSTANCE's own version is the concurrency token: a sibling being decided bumps the request
    // version and must not make this save look stale.
    expect(body.content.visitInstanceId).toBe(2);
    expect(body.content.expectedRowVersion).toBe(4);
    expect(body.content.campusId).toBe('HCM');
    expect(body.overrideLeadTimeConfirmed).toBe(false);
    expect(body.approveAfterSave).toBeNull();
  });

  it('refuses to render when the backend did not grant EDIT_PENDING_CAMPUS for this campus', async () => {
    // The ASSIGNED campus. Its own actions are safe-edit and amendments; offering this form would be
    // promising an edit the backend refuses.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    renderPage(1);
    expect(await screen.findByTestId('pending-campus-not-editable')).toBeInTheDocument();
    expect(screen.queryByTestId('pending-campus-save')).not.toBeInTheDocument();
  });

  it('offers no add/remove campus control — the campus set is fixed', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    expect(screen.queryByRole('button', { name: /Add campus/ })).not.toBeInTheDocument();
    expect(screen.queryAllByLabelText(/Remove this campus/)).toHaveLength(0);
  });

  // ── "Đầu mối hiện tại có nằm trong danh sách đoàn không?" (plan CanhIter3FixBug) ───────────────
  // A relation-only change on an EXISTING campus: the contact PROFILE stays read-only and untouched,
  // but WHICH delegation member the contact corresponds to must now be settable from this screen.

  // Superseded by the operational-contact consistency fix: the relation picker this page used to
  // offer here has been REMOVED entirely (plan B4/F2) — relation existence/identity may only move
  // through Safe Edit's link/unlink or Replace/Transfer, never Pending Edit. What replaces it is a
  // read-only summary, and the relation is preserved SILENTLY through a save with no control to touch
  // it at all — mirroring TC-CONTACT-UI-01/02 on the whole-request edit page.

  it('shows a read-only relation summary for a linked contact — no picker to change it', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          operationalContact: {
            fullName: 'OP HCM', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
            phone: '+84912345678', email: 'op@example.com',
            confirmationStatus: 'CONFIRMED', confirmationSource: null, confirmedAt: null,
            guestMemberId: 20, // the campus's own visitor row above is guestMemberId: id * 10 = 20
          },
        }),
      ],
    }));
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    // The profile block: five read-only values, nothing editable.
    expect(screen.getByTestId('campus-opcontact-readonly-fullName-0')).toHaveTextContent('OP HCM');
    // The relation is shown read-only too — naming the linked member — with no control to change it.
    expect(screen.getByTestId('campus-opcontact-relation-readonly-0')).toHaveTextContent(/Khách HCM/);
    expect(screen.queryByTestId('campus-opcontact-relation-pick-0')).not.toBeInTheDocument();
  });

  it('shows the read-only summary as unlinked when the contact starts outside the delegation', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          operationalContact: {
            fullName: 'OP HCM', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
            phone: '+84912345678', email: 'op@example.com',
            confirmationStatus: 'CONFIRMED', confirmationSource: null, confirmedAt: null,
            guestMemberId: null,
          },
        }),
      ],
    }));
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    expect(screen.getByTestId('campus-opcontact-relation-readonly-0')).not.toHaveTextContent(/Khách HCM/);
    expect(screen.queryByTestId('campus-opcontact-relation-pick-0')).not.toBeInTheDocument();
  });

  it('a content-changing save silently preserves an existing relation — no UI to touch it, no drop', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          operationalContact: {
            fullName: 'OP HCM', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
            phone: '+84912345678', email: 'op@example.com',
            confirmationStatus: 'CONFIRMED', confirmationSource: null, confirmedAt: null,
            guestMemberId: 20, // linked to the campus's own visitor row
          },
        }),
      ],
    }));
    vi.mocked(updatePendingVisitInstance).mockResolvedValue({
      visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
      visitInstanceStatus: 'WAITING_REQUEST_APPROVAL', instanceRowVersion: 5, requestRowVersion: 8,
      approved: false, hostUserId: null, message: 'Đã cập nhật',
    } as never);
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(1));
    const body = vi.mocked(updatePendingVisitInstance).mock.calls[0][2];
    // The relation travels via the row's own resolved persisted id — the client key alone is not
    // trusted, and it is never turned into a NEW pick; the save simply echoes what was already there.
    expect(body.content.operationalContactGuestMemberId).toBe(20);
    // Never copied onto the profile — an unchanged relation is not redescribing the contact.
    expect(body.content.operationalContact).toEqual({
      fullName: 'OP HCM', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
      phone: '+84912345678', email: 'op@example.com',
    });
  });

  it('a content-changing save on an unlinked contact stays unlinked — no UI can introduce a relation', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          operationalContact: {
            fullName: 'OP HCM', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
            phone: '+84912345678', email: 'op@example.com',
            confirmationStatus: 'CONFIRMED', confirmationSource: null, confirmedAt: null,
            guestMemberId: null,
          },
        }),
      ],
    }));
    vi.mocked(updatePendingVisitInstance).mockResolvedValue({
      visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
      visitInstanceStatus: 'WAITING_REQUEST_APPROVAL', instanceRowVersion: 5, requestRowVersion: 8,
      approved: false, hostUserId: null, message: 'Đã cập nhật',
    } as never);
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(1));
    const body = vi.mocked(updatePendingVisitInstance).mock.calls[0][2];
    expect(body.content.operationalContactGuestMemberId ?? null).toBeNull();
    expect(body.content.operationalContact.fullName).toBe('OP HCM');
  });

  // ── The 72-hour floor and the Staff Leader's override ────────────────────────────────────────

  /**
   * A requester-side editor gets NO confirmation dialog: for them the answer is simply no, and offering
   * "continue anyway" would promise something the backend will not honour.
   */
  it('refuses a requester-side move inside 72 hours locally, without calling the API', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    // Move the visit to TOMORROW, keeping the 09:00–11:30 times. The picker owns the date and time as
    // two controls; changing the date alone is the smallest edit that puts the start inside the floor.
    const pad = (n: number) => String(n).padStart(2, '0');
    const tomorrow = new Date(Date.now() + 24 * 3600_000);
    const isoDate = `${tomorrow.getFullYear()}-${pad(tomorrow.getMonth() + 1)}-${pad(tomorrow.getDate())}`;
    fireEvent.change(screen.getByTestId('campus-0-start-date'), { target: { value: isoDate } });

    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(screen.getByTestId('pending-campus-leadtime-error')).toBeInTheDocument());
    expect(updatePendingVisitInstance).not.toHaveBeenCalled();
  });

  /**
   * The 72-hour floor is applied with a manual `form.setError` AFTER the schema already passed —
   * outside react-hook-form's own resolver-driven invalid path, so RHF's built-in auto-focus-on-error
   * never fires for it. Without `focusFirstInvalidField`, the message rendered below the form was the
   * only sign anything was wrong; the schedule row itself never got a red border or focus.
   */
  it('focuses the schedule field when a requester-side move is refused inside 72 hours', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    const pad = (n: number) => String(n).padStart(2, '0');
    const tomorrow = new Date(Date.now() + 24 * 3600_000);
    const isoDate = `${tomorrow.getFullYear()}-${pad(tomorrow.getMonth() + 1)}-${pad(tomorrow.getDate())}`;
    const startDateInput = screen.getByTestId('campus-0-start-date');
    fireEvent.change(startDateInput, { target: { value: isoDate } });
    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(startDateInput.closest('[data-field-error="true"]')).not.toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(startDateInput));
  });

  /**
   * The leader-registrant's path — `canOverrideScheduleLeadTime` is the backend's verdict on exactly
   * that actor, never "is this user a Staff Leader". The client does NOT decide the override: it sends
   * the ordinary request, the backend answers "confirm this", and only then does the dialog appear.
   * Confirming re-sends the SAME payload with the flag rather than skipping the call.
   */
  it('asks the leader-registrant to confirm a sub-72h schedule, then re-sends with the flag', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: true,
        }),
      ],
    }));
    vi.mocked(updatePendingVisitInstance)
      .mockRejectedValueOnce(axiosError(409, 'LEAD_TIME_OVERRIDE_CONFIRMATION_REQUIRED', 'Chưa đủ 72 giờ'))
      .mockResolvedValueOnce({
        visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
        visitInstanceStatus: 'WAITING_REQUEST_APPROVAL', instanceRowVersion: 5, requestRowVersion: 8,
        approved: false, hostUserId: null, message: 'Đã cập nhật',
      } as never);

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');
    fireEvent.click(screen.getByTestId('pending-campus-save'));

    // The TRANSLATED sentence, not the server's: the backend answers in Vietnamese and the shared
    // error helper drops raw Vietnamese in English mode, which would have left this dialog showing a
    // generic "data conflicts with an existing record".
    expect(await screen.findByTestId('pending-campus-override-body'))
      .toHaveTextContent(/72-hour minimum notice/i);
    expect(vi.mocked(updatePendingVisitInstance).mock.calls[0][2].overrideLeadTimeConfirmed).toBe(false);

    fireEvent.click(screen.getByTestId('pending-campus-override-confirm'));

    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(2));
    expect(vi.mocked(updatePendingVisitInstance).mock.calls[1][2].overrideLeadTimeConfirmed).toBe(true);
  });

  it('shows no save-and-approve button to a requester-side editor', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');
    expect(screen.queryByTestId('pending-campus-save-approve')).not.toBeInTheDocument();
  });

  /**
   * A Staff Leader of a DIFFERENT campus who filed this request. The backend grants the edit (they are
   * its registrant) but NOT the leader flag, and the screen has to reflect exactly that split: an
   * ordinary editable form with Save, no "Lưu và duyệt", and no way past the 72-hour floor — moving the
   * date inside it is refused locally, the same as it would be for a guest, rather than turning into
   * the leader's "confirm and continue" dialog.
   */
  it('gives a registrant who leads another campus the ordinary form and none of the leader extras', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        // Granted the edit; canOverrideScheduleLeadTime AND canSaveAndApprove deliberately false —
        // this is what the read model returns for a leader editing (as its registrant) a campus they
        // do not lead. Both fields explicit here: the registrant right on the REQUEST stays intact
        // (Save works below), only the two leader-only privileges are withheld.
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: false,
          canSaveAndApprove: false,
        }),
      ],
    }));

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    expect(screen.getByTestId('pending-campus-save')).toBeInTheDocument();
    expect(screen.queryByTestId('pending-campus-save-approve')).not.toBeInTheDocument();

    // The floor applies to them like any requester: refused in the browser, no call, no dialog.
    const pad = (n: number) => String(n).padStart(2, '0');
    const tomorrow = new Date(Date.now() + 24 * 3600_000);
    fireEvent.change(screen.getByTestId('campus-0-start-date'), {
      target: { value: `${tomorrow.getFullYear()}-${pad(tomorrow.getMonth() + 1)}-${pad(tomorrow.getDate())}` },
    });
    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(screen.getByTestId('pending-campus-leadtime-error')).toBeInTheDocument());
    expect(updatePendingVisitInstance).not.toHaveBeenCalled();
    expect(screen.queryByTestId('pending-campus-override-body')).not.toBeInTheDocument();
  });

  /**
   * The campus's Staff Leader, reviewing a request somebody else filed. The backend grants neither the
   * edit nor the leader flag, so the screen offers nothing at all — no form, no save, and above all no
   * "Lưu và duyệt", which is an edit wearing a decision. Their approve and reject live on the list
   * screen and are untouched (see VisitRequestManagementActions).
   */
  it('offers nothing to a campus Staff Leader who is not the registrant', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        // No EDIT_PENDING_CAMPUS, and canOverrideScheduleLeadTime absent — exactly what the read model
        // now returns for this actor.
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', []),
      ],
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
    }));

    renderPage();

    expect(await screen.findByTestId('pending-campus-not-editable')).toBeInTheDocument();
    expect(screen.queryByTestId('pending-campus-save')).not.toBeInTheDocument();
    expect(screen.queryByTestId('pending-campus-save-approve')).not.toBeInTheDocument();
    expect(updatePendingVisitInstance).not.toHaveBeenCalled();
  });

  /**
   * "Lưu và duyệt" is ONE call. Two calls — save, then approve — can leave the campus rewritten and
   * still waiting when the approval is refused, with the leader believing they approved it.
   */
  it('sends the edit and the approval in a single call, with the chosen host', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: true,
        }),
      ],
    }));
    vi.mocked(delegationsApi.getHostCandidates).mockResolvedValue([
      { userId: 77, fullName: 'Host A', email: 'a@fpt.edu.vn', campusId: 2, departmentName: 'IC', subRole: null, hasScheduleConflict: false, conflictCount: 0, conflicts: [] },
    ] as never);
    vi.mocked(updatePendingVisitInstance).mockResolvedValue({
      visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
      visitInstanceStatus: 'ASSIGNED', instanceRowVersion: 5, requestRowVersion: 8,
      approved: true, hostUserId: 77, message: 'Đã duyệt',
    } as never);

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    fireEvent.click(screen.getByTestId('pending-campus-save-approve'));
    await screen.findByTestId('pending-campus-host-77');
    fireEvent.click(screen.getByTestId('pending-campus-host-77'));
    fireEvent.click(screen.getByTestId('pending-campus-host-confirm'));

    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(1));
    expect(vi.mocked(updatePendingVisitInstance).mock.calls[0][2].approveAfterSave).toEqual({
      hostUserId: 77, decisionNote: null,
    });
  });

  it('shows a reload action when the campus was decided while the form was open', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    vi.mocked(updatePendingVisitInstance).mockRejectedValue(
      axiosError(409, 'PENDING_CAMPUS_NOT_EDITABLE', 'Cơ sở đã được xử lý'));

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');
    fireEvent.click(screen.getByTestId('pending-campus-save'));

    // Matched on the CODE, not the status: this arrives as a 409 like a version conflict does, and
    // collapsing the two would tell the user to reload when the real answer is "somebody decided it".
    expect(await screen.findByTestId('pending-campus-edit-error'))
      .toHaveTextContent(/no longer waiting for a decision/i);
    expect(screen.getByRole('button', { name: /Reload latest data/ })).toBeInTheDocument();
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  // ── A. Save with nothing to write is informational, never a failed mutation (bug report §A) ────

  it('shows an info toast — not a red inline alert — when Save has nothing to write', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm());
    vi.mocked(updatePendingVisitInstance).mockRejectedValue(
      axiosError(400, 'PENDING_CAMPUS_NO_CONTENT_CHANGES', 'Không có thay đổi nào để lưu cho cơ sở này.'));

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');
    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(vi.mocked(showInfoToast)).toHaveBeenCalledWith('Nothing to save.'));
    // Matched by CODE, never message text — and never rendered as the red inline alert other
    // refusals use, since this is not a failed mutation.
    expect(screen.queryByTestId('pending-campus-edit-error')).not.toBeInTheDocument();
    expect(mockNavigate).not.toHaveBeenCalled();
  });

  // ── C. The Staff Leader explanatory banner is gone; the buttons alone say what may be done ──────

  it('no longer renders the Staff Leader explanatory banner above Save/Save&Approve', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: true,
        }),
      ],
    }));

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    expect(screen.queryByText(/Staff Leader for this campus/i)).not.toBeInTheDocument();
    expect(screen.getByTestId('pending-campus-save-approve')).toBeInTheDocument();
  });

  // ── D. Shared Host UI: same visual language as the List/Detail AssignHostModal ───────────────────

  it('Host picker offers a search box, a self-host badge, a conflict warning, and the canonical note length', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: true,
        }),
      ],
    }));
    vi.mocked(delegationsApi.getHostCandidates).mockResolvedValue([
      {
        userId: 3, fullName: 'Leader HCM', email: 'leader@fpt.edu.vn', campusId: 2,
        departmentName: 'IC', subRole: 'LEADER', isStaffLeaderSelfHostOption: true,
        hasScheduleConflict: false, conflictCount: 0, conflicts: [],
      },
      {
        userId: 101, fullName: 'IC Staff Busy', email: 'ic@fpt.edu.vn', campusId: 2,
        departmentName: 'IC', subRole: 'STAFF', hasScheduleConflict: true, conflictCount: 2,
        conflicts: [{ source: 'VISIT_INSTANCE', title: 'Another delegation', startAt: '2027-09-01T09:00:00', endAt: '2027-09-01T10:00:00' }],
      },
    ] as never);

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');
    fireEvent.click(screen.getByTestId('pending-campus-save-approve'));
    await screen.findByTestId('pending-campus-host-3');

    // Search box — same visual language as List/Detail's AssignHostModal, not the old radio list.
    expect(screen.getByPlaceholderText('Search staff by name, email, department...')).toBeInTheDocument();
    // Self-host candidate carries the self-host badge.
    expect(screen.getByTestId('pending-campus-host-3')).toHaveTextContent('I will take it on');
    // A candidate with a schedule conflict shows the warning, not a silent selection.
    expect(screen.getByTestId('pending-campus-host-101')).toHaveTextContent('Schedule conflict with this request');
    // Canonical decisionNote max length — one source shared with the ordinary AssignHostModal
    // (VisitMutationPolicy.DecisionNoteMaxLength on the backend).
    expect(screen.getByTestId('pending-campus-decision-note')).toHaveAttribute('maxlength', '2000');
  });

  // ── Replay scope: an EXISTING campus's read-only contact/registrant snapshot must never block a
  //    mutation that never touches those fields (bug report §II/§III). ─────────────────────────────

  it('does not let a legacy invalid operational-contact phone block Save or Save&Approve', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: true,
          operationalContact: {
            fullName: 'OP HCM', organization: 'ĐH X', jobTitle: 'Trưởng phòng Hợp tác',
            // Structurally nonsense, exactly the shape reported: this campus's contact is read-only
            // here, and the backend's own OperationalContactReplayV2Validator never format-checks it.
            phone: '+8435352152512asdasdsadasd', email: 'op@example.com',
            confirmationStatus: 'CONFIRMED', confirmationSource: null, confirmedAt: null,
          },
        }),
      ],
    }));
    vi.mocked(updatePendingVisitInstance).mockResolvedValue({
      visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
      visitInstanceStatus: 'WAITING_REQUEST_APPROVAL', instanceRowVersion: 5, requestRowVersion: 8,
      approved: false, hostUserId: null, message: 'Đã cập nhật',
    } as never);
    vi.mocked(delegationsApi.getHostCandidates).mockResolvedValue([] as never);

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    fireEvent.click(screen.getByTestId('pending-campus-save'));
    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(1));

    fireEvent.click(screen.getByTestId('pending-campus-save-approve'));
    expect(await screen.findByTestId('pending-campus-decision-note')).toBeInTheDocument();
  });

  it('does not let a legacy registrant snapshot that fails create-strength validation block Save', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      registrant: {
        fullName: 'Reg', organization: 'ĐH X', jobTitle: 'TP',
        phone: 'not-a-phone', email: 'not-an-email', nationality: '',
      },
    }));
    vi.mocked(updatePendingVisitInstance).mockResolvedValue({
      visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
      visitInstanceStatus: 'WAITING_REQUEST_APPROVAL', instanceRowVersion: 5, requestRowVersion: 8,
      approved: false, hostUserId: null, message: 'Đã cập nhật',
    } as never);

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');
    fireEvent.click(screen.getByTestId('pending-campus-save'));

    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(1));
  });

  it('blocks Save&Approve on an invalid EDITABLE field and focuses it, without opening the Host picker', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: true,
        }),
      ],
    }));

    renderPage();
    const delegationInput = await screen.findByDisplayValue('Đoàn HCM');
    fireEvent.change(delegationInput, { target: { value: '' } });

    fireEvent.click(screen.getByTestId('pending-campus-save-approve'));

    await waitFor(() => expect(delegationInput.closest('[data-field-error="true"]')).not.toBeNull());
    await waitFor(() => expect(document.activeElement).toBe(delegationInput));
    expect(screen.queryByTestId('pending-campus-decision-note')).not.toBeInTheDocument();
    expect(updatePendingVisitInstance).not.toHaveBeenCalled();
  });

  // ── Save&Approve + the 72-hour floor together (bug report §IV) ───────────────────────────────────

  it('keeps the chosen Host and decision note through a 72-hour override confirmation on Save&Approve', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: true,
        }),
      ],
    }));
    vi.mocked(delegationsApi.getHostCandidates).mockResolvedValue([
      { userId: 77, fullName: 'Host A', email: 'a@fpt.edu.vn', campusId: 2, departmentName: 'IC', subRole: null, hasScheduleConflict: false, conflictCount: 0, conflicts: [] },
    ] as never);
    vi.mocked(updatePendingVisitInstance)
      .mockRejectedValueOnce(axiosError(409, 'LEAD_TIME_OVERRIDE_CONFIRMATION_REQUIRED', 'Chưa đủ 72 giờ'))
      .mockResolvedValueOnce({
        visitRequestId: 5, visitInstanceId: 2, visitRequestStatus: 'PARTIALLY_APPROVED',
        visitInstanceStatus: 'ASSIGNED', instanceRowVersion: 5, requestRowVersion: 8,
        approved: true, hostUserId: 77, message: 'Đã duyệt',
      } as never);

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');

    fireEvent.click(screen.getByTestId('pending-campus-save-approve'));
    await screen.findByTestId('pending-campus-host-77');
    fireEvent.click(screen.getByTestId('pending-campus-host-77'));
    fireEvent.change(screen.getByTestId('pending-campus-decision-note'), { target: { value: 'Đồng ý tiếp nhận' } });
    fireEvent.click(screen.getByTestId('pending-campus-host-confirm'));

    await screen.findByTestId('pending-campus-override-body');
    expect(vi.mocked(updatePendingVisitInstance).mock.calls[0][2].approveAfterSave).toEqual({
      hostUserId: 77, decisionNote: 'Đồng ý tiếp nhận',
    });

    fireEvent.click(screen.getByTestId('pending-campus-override-confirm'));

    await waitFor(() => expect(updatePendingVisitInstance).toHaveBeenCalledTimes(2));
    const secondCall = vi.mocked(updatePendingVisitInstance).mock.calls[1][2];
    expect(secondCall.overrideLeadTimeConfirmed).toBe(true);
    // The regression itself: before the fix this was undefined/null, so a "Lưu và duyệt" that hit the
    // 72-hour floor ended as a plain save — the leader believed they had approved the campus.
    expect(secondCall.approveAfterSave).toEqual({ hostUserId: 77, decisionNote: 'Đồng ý tiếp nhận' });
  });

  // ── CanSaveAndApprove is its own contract, not a canOverrideScheduleLeadTime proxy (bug report §V) ─

  it('does not use canOverrideScheduleLeadTime as a proxy for the Save&Approve button', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(mixedForm({
      campusVisits: [
        campus(1, 'HN', 'FPTU Hà Nội', 'ASSIGNED', ['SUBMIT_SAFE_EDIT']),
        campus(2, 'HCM', 'FPTU Hồ Chí Minh', 'WAITING_REQUEST_APPROVAL', ['EDIT_PENDING_CAMPUS'], {
          canOverrideScheduleLeadTime: true,
          canSaveAndApprove: false,
        }),
      ],
    }));

    renderPage();
    await screen.findByDisplayValue('Đoàn HCM');
    expect(screen.queryByTestId('pending-campus-save-approve')).not.toBeInTheDocument();
  });
});
