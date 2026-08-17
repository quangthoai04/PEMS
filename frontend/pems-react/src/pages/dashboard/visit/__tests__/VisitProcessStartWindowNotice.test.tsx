/**
 * NP-05 — the BEFORE → DURING policy, as decided: T-6h is a RECOMMENDATION, not a gate.
 *
 * The version this replaces pinned a hybrid nobody had actually chosen: the capability flag said
 * "chưa tới giờ", the banner said "Có thể chuyển … từ 03:00", and the button underneath worked
 * anyway because the backend had stopped enforcing the window. Three layers, three different
 * stories.
 *
 * What is pinned now:
 *   • `canStartVisit` is true throughout BEFORE_VISIT — the button is offered, never time-locked.
 *   • Starting early only changes the WORDS — an extra note inside the confirm dialog (the banner
 *     itself stays a single line).
 *   • Every transition goes through a confirmation that states the read-only consequence.
 *   • Cancel calls nothing; confirm sends exactly one request; a failure leaves the dialog's own
 *     screen state alone rather than pretending the stage moved.
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

/** Host, preparation open, and EARLIER than the recommended window (the interesting case). */
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
  // No clock term any more: the capability is true for the whole of BEFORE_VISIT.
  canStartVisit: true,
  isBeforeRecommendedStartWindow: true,
  recommendedStartVisitAt: '2026-08-22T03:00:00',
  canCompleteVisit: false,
  canCloseVisit: false,
  canSendSetupProgressEmail: false,
  canStartPreparation: false,
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

const openConfirm = async (user: ReturnType<typeof userEvent.setup>) => {
  const trigger = await screen.findByTestId('stage-advance-before');
  await user.click(trigger);
  return screen.findByTestId('stage-confirm-modal');
};

describe('BEFORE → DURING: T-6h is advice, and the transition is confirmed', () => {
  it('offers the transition before the recommended window, unconditionally', async () => {
    render(<VisitProcess />);

    const button = await screen.findByTestId('stage-advance-before');
    expect(button).not.toBeDisabled();
  });

  it('asks for confirmation and names the read-only consequence before calling anything', async () => {
    const user = userEvent.setup();
    render(<VisitProcess />);

    const modal = await openConfirm(user);
    expect(modal).toHaveTextContent(/chỉ còn chế độ xem/);
    expect(modal).toHaveTextContent(/lịch trình, thành phần tham gia, hậu cần, cảnh báo và ghi chú chuẩn bị/);
    // Starting early gets its own line — inside the dialog, where the decision is made.
    expect(screen.getByTestId('stage-confirm-early')).toHaveTextContent(/sớm hơn mốc thường lệ/);
    // Opening a dialog is not an action.
    expect(completeBeforeVisit).not.toHaveBeenCalled();
  });

  it('calls nothing when the Host cancels', async () => {
    const user = userEvent.setup();
    render(<VisitProcess />);

    await openConfirm(user);
    await user.click(screen.getByTestId('stage-confirm-cancel'));

    await waitFor(() => expect(screen.queryByTestId('stage-confirm-modal')).not.toBeInTheDocument());
    expect(completeBeforeVisit).not.toHaveBeenCalled();
  });

  it('sends exactly one transition request on confirm', async () => {
    completeBeforeVisit.mockResolvedValue({});
    const user = userEvent.setup();
    render(<VisitProcess />);

    await openConfirm(user);
    await user.click(screen.getByTestId('stage-confirm-submit'));

    await waitFor(() => expect(completeBeforeVisit).toHaveBeenCalledWith(9001, 501));
    expect(completeBeforeVisit).toHaveBeenCalledTimes(1);
  });

  it('does not pretend the stage moved when the API fails', async () => {
    completeBeforeVisit.mockRejectedValue(Object.assign(new Error('500'), {
      isAxiosError: true,
      response: { status: 500, data: { message: 'Lỗi hệ thống.' } },
    }));
    const user = userEvent.setup();
    render(<VisitProcess />);

    await openConfirm(user);
    await user.click(screen.getByTestId('stage-confirm-submit'));

    await waitFor(() => expect(completeBeforeVisit).toHaveBeenCalledTimes(1));
    // Still on the confirm dialog, still on the before tab — nothing advanced, and the button is
    // live again so the Host can retry.
    expect(screen.getByTestId('stage-confirm-modal')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByTestId('stage-confirm-submit')).not.toBeDisabled());
  });

  it('refetches the real state when the API answers 409 (it moved under us)', async () => {
    completeBeforeVisit.mockRejectedValue(Object.assign(new Error('409'), {
      isAxiosError: true,
      response: { status: 409, data: { message: 'Chưa thể chuyển sang giai đoạn Trong tiếp khách.' } },
    }));
    const user = userEvent.setup();
    render(<VisitProcess />);

    await openConfirm(user);
    await user.click(screen.getByTestId('stage-confirm-submit'));

    // Permissions AND detail AND reminders are all re-read — the whole point of refreshProcessState.
    await waitFor(() => expect(getVisitProcessPermissions).toHaveBeenCalledTimes(2));
    expect(getVisitProcessDetail).toHaveBeenCalledTimes(2);
  });
});
