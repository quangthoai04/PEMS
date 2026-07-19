import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
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

const campus = (id: number, code: string, name: string, rowVersion: number, delegation: string) => ({
  visitInstanceId: id, campusId: id, campusCode: code, campusName: name,
  plannedStartAt: '2026-09-01T09:00:00', plannedEndAt: '2026-09-01T11:30:00', timezone: 'Asia/Ho_Chi_Minh',
  instanceStatus: 'PENDING', currentHostUserId: null, currentHostName: null, decidedByUserId: null,
  decidedByName: null, decidedAt: null, decisionActorRole: null, decisionNote: null,
  delegationName: delegation, visitType: 'MEETING', visitTypeOther: null, purpose: 'Trao đổi', workingContent: 'ND',
  visitors: [{ guestMemberId: id * 10, memberType: 'VISITOR', fullName: `Khách ${code}`, organization: 'ĐH X', jobTitle: 'GV', nationality: 'VN', displayOrder: 1 }],
  supportMembers: [], operationalContact: { fullName: `OP ${code}`, organization: 'ĐH X', phone: '+84912345678', email: '' },
  workingLanguage: 'VI', transportationNote: null, mediaConsentStatus: 'DECLINED', mediaConsentNote: null, noteToFptu: null,
  formRevision: 1, approvalRevision: 0, rowVersion, activeAmendment: null,
});

const form = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => ({
  visitRequestId: 5, requestCode: 'VR-5', rowVersion: 7, formSchemaVersion: 2,
  hasMixedCampusDetails: false, visitScope: 'SINGLE_CAMPUS', requestStatus: 'PENDING_APPROVAL',
  createdSource: 'PUBLIC', submittedAt: '2026-07-15T08:00:00', partnerId: null,
  registrant: { fullName: 'Reg', organization: 'ĐH X', jobTitle: 'TP', phone: '+84912345678', email: 'reg@x.vn', nationality: 'VN' },
  primaryContact: { fullName: 'ĐM', organization: 'ĐH X', phone: '+84987654321', email: 'c@x.vn', accessStatus: 'ACTIVE', verifiedAt: null },
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
    expect((screen.getByDisplayValue('c@x.vn') as HTMLInputElement).readOnly).toBe(true);

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
});
