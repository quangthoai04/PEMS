import { StrictMode } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import VisitRequestV2DetailView from '../components/v2/VisitRequestV2DetailView';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';
import { campusFixture } from './fixtures';

/**
 * ONE success toast per save (fix plan §6). The edit/resubmit form raises none of its own and hands the
 * message to this screen in router state; this screen shows it once and clears the state.
 *
 * Rendered inside <StrictMode> deliberately: that is where the bug lived. StrictMode mounts, tears down
 * and re-mounts, so the effect ran twice against a router state the first pass had not finished
 * clearing — two identical toasts for one save. A ref, not the state clear, is what makes it once.
 */

const mockNavigate = vi.fn();
vi.mock('react-router-dom', async (importOriginal) => {
  const actual = await importOriginal<typeof import('react-router-dom')>();
  return { ...actual, useNavigate: () => mockNavigate };
});

vi.mock('../api/visitRequestV2Api', () => ({
  getVisitRequestFormV2: vi.fn(),
  getActiveContactTransfer: vi.fn().mockResolvedValue({
    visitRequestId: 1, hasPendingTransfer: false, identityChangeId: null,
    status: null, newEmailMasked: null, expiresAt: null, resendCount: 0,
  }),
  getActiveAmendment: vi.fn().mockResolvedValue(null),
  getVisitRequestHistory: vi.fn().mockResolvedValue({ visitRequestId: 1, requestCode: 'VR-1', entries: [] }),
  getVisitHistoryDetail: vi.fn(),
  markVisitChangesSeen: vi.fn().mockResolvedValue({ markedCount: 0 }),
  resendContactClaim: vi.fn(),
  replacePendingContact: vi.fn(),
  initiateContactTransfer: vi.fn(),
  resendContactTransfer: vi.fn(),
  cancelContactTransfer: vi.fn(),
  approveAmendment: vi.fn(),
  rejectAmendment: vi.fn(),
  withdrawAmendment: vi.fn(),
}));

vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));

vi.mock('../../../shared/utils/toast', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../../shared/utils/toast')>();
  return { ...actual, showSuccessToast: vi.fn() };
});

import { getVisitRequestFormV2 } from '../api/visitRequestV2Api';
import { showSuccessToast } from '../../../shared/utils/toast';

const form = (): ResolvedVisitForm => ({
  visitRequestId: 1,
  requestCode: 'VR-2026-001',
  rowVersion: 0,
  hasMixedCampusDetails: false,
  visitScope: 'SINGLE_CAMPUS',
  requestStatus: 'PENDING_APPROVAL',
  createdSource: 'PUBLIC',
  submittedAt: '2026-07-15T08:00:00',
  partnerId: null,
  cancelledByUserId: null,
  cancelledByName: null,
  cancelledAt: null,
  cancellationReason: null,
  registrant: {
    fullName: 'Người Đăng Ký', organization: 'ĐH ABC', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@x.vn', nationality: 'VN',
  },
  confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },
  requestOutcome: { code: 'ALL_WAITING', total: 1, accepted: 0, inProgress: 0, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
  campusVisits: [campusFixture()],
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
});

const renderWithFlash = (flash?: string) =>
  render(
    <StrictMode>
      <MemoryRouter
        initialEntries={[{ pathname: '/dashboard/visit/v2/1', state: flash ? { flash } : null }]}
      >
        <VisitRequestV2DetailView visitRequestId={1} />
      </MemoryRouter>
    </StrictMode>,
  );

describe('the edit/resubmit success message is shown exactly once', () => {
  beforeEach(() => vi.clearAllMocks());

  it('raises ONE toast for one save, even under StrictMode double-mount (TC-TOAST-01/02)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());

    renderWithFlash('Đã cập nhật đơn');

    await screen.findByText('VR-2026-001');
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledTimes(1));
    expect(showSuccessToast).toHaveBeenCalledWith('Đã cập nhật đơn', 'v2-detail-flash-1');

    // …and the router state is cleared, so a refresh or a back/forward cannot replay it (TC-TOAST-03).
    expect(mockNavigate).toHaveBeenCalledWith('/dashboard/visit/v2/1', { replace: true, state: null });
  });

  it('stays silent when there is nothing to announce (a plain visit to the screen)', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(form());

    renderWithFlash();

    await screen.findByText('VR-2026-001');
    expect(showSuccessToast).not.toHaveBeenCalled();
  });
});
