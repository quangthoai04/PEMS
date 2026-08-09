/**
 * The BEFORE → DURING early-start window ("6 giờ trước thời gian bắt đầu") used to render its
 * confirm button permanently `disabled` while the window was closed — the Host saw the notice but
 * could not act on it at all. The button is now always clickable: the amber banner stays a plain
 * heads-up, and the backend (CompleteVisitStageCommandHandler) remains the real authority, refusing
 * an early attempt with a 409 VISIT_START_WINDOW_NOT_OPEN that `advanceStage` already turns into a
 * toast and a permissions refetch.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const getVisitProcessPermissions = vi.fn();
const getVisitProcessDetail = vi.fn();
const getReminderSettings = vi.fn();
const completeBeforeVisit = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitProcessPermissions: (...a: unknown[]) => getVisitProcessPermissions(...a),
    getVisitProcessDetail: (...a: unknown[]) => getVisitProcessDetail(...a),
    getReminderSettings: (...a: unknown[]) => getReminderSettings(...a),
    completeBeforeVisit: (...a: unknown[]) => completeBeforeVisit(...a),
  },
}));

vi.mock('../../../../features/agenda-templates/components/AgendaSetupPanel', () => ({
  AgendaSetupPanel: () => <div>agenda-setup-panel</div>,
}));
vi.mock('../../../../features/delegations/components/ParticipantInvitationSection', () => ({
  ParticipantInvitationSection: () => <div>participant-invitation-section</div>,
}));
vi.mock('../../../../features/delegations/components/LogisticsRequestSection', () => ({
  LogisticsRequestSection: () => <div>logistics-request-section</div>,
}));
vi.mock('../../../../features/delegations/components/RequestInfoReadOnly', () => ({
  RegistrantInfoReadOnly: () => <div>registrant-info</div>,
  DelegationInfoReadOnly: () => <div>delegation-info</div>,
}));
vi.mock('../VisitDuringTab', () => ({ VisitDuringTab: () => <div>during-tab</div> }));
vi.mock('../VisitAfterTab', () => ({ VisitAfterTab: () => <div>after-tab</div> }));
vi.mock('../VisitorVisitDetailPage', () => ({ VisitorVisitDetailPage: () => <div>visitor-page</div> }));

vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: { userId: '77', roleCode: 'STAFF' } }),
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
  useParams: () => ({ id: '501' }),
  useLocation: () => ({ state: null, pathname: '/dashboard/visit/process/501' }),
}));

import { VisitProcess } from '../VisitProcess';

const HOST_PERMISSION = {
  visitInstanceId: 501,
  visitRequestId: 9001,
  requestStatus: 'APPROVED',
  instanceStatus: 'BEFORE_VISIT',
  relation: 'HOST',
  hostAssigned: true,
  canViewOriginalRequest: true,
  canViewOverview: true,
  canViewBeforeVisit: true,
  canEditBeforeVisit: true,
  canViewDuringVisit: true,
  canEditDuringVisit: true,
  canViewAfterVisit: true,
  canEditAfterVisit: true,
  canAssignHost: false,
  canViewMinutes: true,
  canCreateMinutes: true,
  canEditMinutes: true,
  canViewNews: true,
  canCreateNews: true,
  canStartVisit: false,
  startVisitDisabledReasonCode: 'VISIT_START_WINDOW_NOT_OPEN',
  startVisitAvailableAt: '2026-08-22T03:00:00',
  canCompleteVisit: false,
  canCloseVisit: false,
};

const DETAIL = {
  visitInstanceId: 501,
  visitRequestId: 9001,
  relation: 'HOST',
  instanceStatus: 'BEFORE_VISIT',
  delegationName: 'Đoàn ĐH Quốc gia',
  campusName: 'FPTU HCM',
  hostName: 'Trần Cảnh',
  plannedStartAt: '2026-08-22T09:00:00',
  plannedEndAt: '2026-08-22T11:30:00',
  agendaItems: [],
  requestSummary: {},
};

beforeEach(() => {
  vi.clearAllMocks();
  getVisitProcessPermissions.mockResolvedValue(HOST_PERMISSION);
  getVisitProcessDetail.mockResolvedValue(DETAIL);
  getReminderSettings.mockResolvedValue({ items: [] });
});

describe('BEFORE → DURING early-start window is a notice, not a lock', () => {
  it('shows the window-not-open banner with the confirm button enabled, not disabled', async () => {
    render(<VisitProcess />);

    const banner = await screen.findByTestId('stage-before-window-closed');
    expect(banner).toHaveTextContent(/Đã hoàn tất công tác chuẩn bị/);
    expect(banner).toHaveTextContent(/6 giờ trước thời gian bắt đầu/);

    const button = screen.getByRole('button', { name: /Xác nhận hoàn thành chuẩn bị/ });
    expect(button).not.toBeDisabled();
  });

  it('still calls the complete-preparation action when clicked before the window opens', async () => {
    completeBeforeVisit.mockResolvedValue({});
    const user = userEvent.setup();
    render(<VisitProcess />);

    const button = await screen.findByRole('button', { name: /Xác nhận hoàn thành chuẩn bị/ });
    await user.click(button);

    await waitFor(() => expect(completeBeforeVisit).toHaveBeenCalledWith(9001, 501));
  });

  it('surfaces the backend 409 refusal instead of pretending the click worked', async () => {
    const conflict = Object.assign(new Error('409'), {
      isAxiosError: true,
      response: { status: 409, data: { message: 'Chưa thể chuyển sang giai đoạn Trong tiếp khách.' } },
    });
    completeBeforeVisit.mockRejectedValue(conflict);
    const user = userEvent.setup();
    render(<VisitProcess />);

    const button = await screen.findByRole('button', { name: /Xác nhận hoàn thành chuẩn bị/ });
    await user.click(button);

    // On a 409 advanceStage refetches permissions to reflect the real state.
    await waitFor(() => expect(getVisitProcessPermissions).toHaveBeenCalledTimes(2));
    // The banner is still here — the window really did not open, so nothing silently advanced.
    expect(await screen.findByTestId('stage-before-window-closed')).toBeInTheDocument();
  });
});
