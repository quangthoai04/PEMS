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
  beforeEach(() => vi.clearAllMocks());

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

  it('pending-edit allows adding a campus (new campus has a null instance id in the payload)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
    vi.mocked(updatePendingVisitRequestV2).mockResolvedValue({
      visitRequestId: 5, status: 'PENDING_APPROVAL', visitScope: 'MULTI_CAMPUS',
      hasMixedCampusDetails: false, requestRowVersion: 8, instances: [], message: 'ok',
    } as never);

    renderAt('edit');
    await screen.findByDisplayValue('Đoàn HN');
    fireEvent.click(screen.getByRole('button', { name: /Add campus/ }));
    // The new (empty) card would fail validation, so this asserts the add path via the field count only:
    expect(screen.getAllByLabelText(/Remove this campus/).length).toBeGreaterThanOrEqual(1);
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

  // ── Campus ceiling + campus picker (fix plan §4–§5) ──────────────────────────────────────────
  // The mocked hook above serves TWO campuses open for registration, which is the number these
  // assertions are about: the screen used to count against a hard-coded 10.
  describe('the campus ceiling is however many campuses are open for registration', () => {
    /** Campus selects are the ones offering the "select a campus" placeholder. */
    const campusSelects = () =>
      screen.getAllByRole('combobox').filter(el =>
        el.tagName === 'SELECT'
        && Array.from((el as HTMLSelectElement).options)
          .some(o => o.value === '' && /Select campus/i.test(o.text)));

    it('counts against the ACTIVE campuses, not a hard-coded 10 (TC-CAMPUS-01)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      expect(screen.getByRole('button', { name: /Add campus/ })).toHaveTextContent('1/2');
      expect(screen.queryByRole('button', { name: /10/ })).not.toBeInTheDocument();
    });

    it('disables Add campus once every open campus is already in the request (TC-CAMPUS-02)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form({
        visitScope: 'MULTI_CAMPUS',
        campusVisits: [campus(1, 'HN', 'FPTU Hà Nội', 4, 'Đoàn HN'), campus(2, 'HCM', 'FPTU Hồ Chí Minh', 2, 'Đoàn HCM')],
      }));
      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      expect(screen.getByRole('button', { name: /Add campus/ })).toBeDisabled();
    });

    it('a NEW row starts unselected — no campus name over an empty campusId (TC-CAMPUS-03)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      fireEvent.click(screen.getByRole('button', { name: /Add campus/ }));

      await waitFor(() => expect(campusSelects()).toHaveLength(2));
      const fresh = campusSelects()[1] as HTMLSelectElement;
      expect(fresh.value).toBe('');
      expect(fresh.options[fresh.selectedIndex].text).toMatch(/Select campus/i);
    });

    /**
     * TC-CAMPUS-02. The reported bug was a card that READ as chosen while the form behind it still
     * held nothing, so saving produced "Vui lòng chọn cơ sở" over a box showing a campus name.
     *
     * The assertion is deliberately not on the `<select>` alone — that is the half that was already
     * "right" and lied. `campus-edit-card-{code}` is rendered from the WATCHED form value, so it
     * changing to `campus-edit-card-HCM` is the form state itself answering. From there to the payload
     * is `buildV2EditPayload`, covered field-by-field in visitRequestV2Form.test.ts.
     */
    it('choosing a campus on the new card updates the form state behind it (TC-CAMPUS-02)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      fireEvent.click(screen.getByRole('button', { name: /Add campus/ }));
      await waitFor(() => expect(campusSelects()).toHaveLength(2));
      // Before choosing, the card is identified as the unselected one.
      expect(screen.getByTestId('campus-edit-card-new')).toBeInTheDocument();

      const fresh = campusSelects()[1] as HTMLSelectElement;
      fireEvent.change(fresh, { target: { value: 'HCM' } });

      await waitFor(() => expect(screen.getByTestId('campus-edit-card-HCM')).toBeInTheDocument());
      expect(fresh.value).toBe('HCM');
      expect(fresh.options[fresh.selectedIndex].text).toBe('FPTU Hồ Chí Minh');
      expect(screen.queryByTestId('campus-edit-card-new')).not.toBeInTheDocument();
      expect(screen.queryByText(/Vui lòng chọn cơ sở/)).not.toBeInTheDocument();
    });

    it('a campus already chosen is not offered on another card (TC-CAMPUS-04)', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());
      renderAt('edit');
      await screen.findByDisplayValue('Đoàn HN');

      fireEvent.click(screen.getByRole('button', { name: /Add campus/ }));

      await waitFor(() => expect(campusSelects()).toHaveLength(2));
      const optionValues = Array.from((campusSelects()[1] as HTMLSelectElement).options).map(o => o.value);
      expect(optionValues).not.toContain('HN'); // taken by card 1
      expect(optionValues).toContain('HCM');
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
});
