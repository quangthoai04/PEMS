/**
 * NP-04 — once a campus moves into DURING_VISIT, the "Trước tiếp khách" tab is read-only IMMEDIATELY.
 *
 * <p>What went wrong: a stage transition changes neither `visitRequestId` nor `visitInstanceId`, and
 * `loadDetail` is keyed on exactly those two. So a transition refetched only the permissions and the
 * page ended up holding two contradictory answers:</p>
 *
 * <pre>
 *   permissions.instanceStatus = DURING_VISIT   (fresh)
 *   detail.instanceStatus      = BEFORE_VISIT   (stale)
 * </pre>
 *
 * <p>Every capability computed from `detail` kept its buttons, and pressing one earned a toast from
 * the backend about the wrong stage — the backend was right all along; the screen was showing
 * controls that could not work.</p>
 *
 * <p>A second, quieter half of the same bug: `ASSIGNED` was accepted as an editable state by the
 * page and by both section components, while `VisitPreparationGate` refuses every one of those
 * commands outside `BEFORE_VISIT`. Both are pinned below.</p>
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
// Rendered with the status they were HANDED, so the test can assert the page passes the fresh one.
vi.mock('../../../../features/delegations/components/ParticipantInvitationSection', () => ({
  ParticipantInvitationSection: ({ instanceStatus }: { instanceStatus: string }) =>
    <div data-testid="participants-status">{instanceStatus}</div>,
}));
vi.mock('../../../../features/delegations/components/LogisticsRequestSection', () => ({
  LogisticsRequestSection: ({ instanceStatus }: { instanceStatus: string }) =>
    <div data-testid="logistics-status">{instanceStatus}</div>,
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

const permission = (over: Record<string, unknown> = {}) => ({
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
  canStartPreparation: false,
  canStartVisit: true,
  isBeforeRecommendedStartWindow: false,
  recommendedStartVisitAt: '2026-08-22T03:00:00',
  canCompleteVisit: false,
  canCloseVisit: false,
  canSendSetupProgressEmail: false,
  ...over,
});

const detail = (over: Record<string, unknown> = {}) => ({
  visitInstanceId: 501,
  visitRequestId: 9001,
  relation: 'HOST',
  instanceStatus: 'BEFORE_VISIT',
  delegationName: 'Đoàn ĐH Quốc gia',
  campusName: 'FPTU HCM',
  hostName: 'Trần Cảnh',
  plannedStartAt: '2026-08-22T09:00:00',
  plannedEndAt: '2026-08-22T11:30:00',
  agenda: [],
  requestSummary: {},
  ...over,
});

/** Controls that exist only while the preparation window is open. */
const prepControls = () => ({
  saveReminders: screen.queryByRole('button', { name: /Lưu cảnh báo/ }),
  cancelReminders: screen.queryByRole('button', { name: /Tắt tất cả cảnh báo/ }),
  saveNote: screen.queryByRole('button', { name: /Lưu ghi chú/ }),
  agendaTemplate: screen.queryByTestId('agenda-template-toggle'),
  editAgenda: screen.queryByRole('button', { name: /Chỉnh sửa/ }),
});

beforeEach(() => {
  vi.clearAllMocks();
  getReminderSettings.mockResolvedValue({ items: [] });
});

describe('NP-04: the preparation tab locks the moment the stage moves', () => {
  it('offers the preparation controls while the campus is BEFORE_VISIT', async () => {
    getVisitProcessPermissions.mockResolvedValue(permission());
    getVisitProcessDetail.mockResolvedValue(detail());

    render(<VisitProcess />);

    await waitFor(() => expect(prepControls().saveReminders).toBeInTheDocument());
    expect(prepControls().saveNote).toBeInTheDocument();
    expect(prepControls().agendaTemplate).toBeInTheDocument();
    expect(screen.queryByTestId('before-readonly-banner')).not.toBeInTheDocument();
  });

  it('withdraws every preparation control after a transition, with no reload', async () => {
    getVisitProcessPermissions.mockResolvedValue(permission());
    getVisitProcessDetail.mockResolvedValue(detail());
    completeBeforeVisit.mockResolvedValue({});
    const user = userEvent.setup();

    render(<VisitProcess />);
    await waitFor(() => expect(prepControls().saveReminders).toBeInTheDocument());

    // The API confirms the move; BOTH payloads now answer DURING_VISIT.
    getVisitProcessPermissions.mockResolvedValue(permission({ instanceStatus: 'DURING_VISIT' }));
    getVisitProcessDetail.mockResolvedValue(detail({ instanceStatus: 'DURING_VISIT' }));

    await user.click(await screen.findByTestId('stage-advance-before'));
    await user.click(screen.getByTestId('stage-confirm-submit'));
    await waitFor(() => expect(completeBeforeVisit).toHaveBeenCalled());

    // The transition also switches to the "during" tab, so come back to the one under test.
    await user.click(screen.getByRole('button', { name: /Trước tiếp khách/ }));

    await waitFor(() => expect(screen.getByTestId('before-readonly-banner')).toBeInTheDocument());
    const controls = prepControls();
    expect(controls.saveReminders).not.toBeInTheDocument();
    expect(controls.cancelReminders).not.toBeInTheDocument();
    expect(controls.saveNote).not.toBeInTheDocument();
    expect(controls.agendaTemplate).not.toBeInTheDocument();
  });

  it('refetches the DETAIL too, not just the permissions', async () => {
    // The precise regression: refetching permissions alone left `detail.instanceStatus` on
    // BEFORE_VISIT, and every capability derived from it kept its buttons.
    getVisitProcessPermissions.mockResolvedValue(permission());
    getVisitProcessDetail.mockResolvedValue(detail());
    completeBeforeVisit.mockResolvedValue({});
    const user = userEvent.setup();

    render(<VisitProcess />);
    await waitFor(() => expect(getVisitProcessDetail).toHaveBeenCalledTimes(1));

    await user.click(await screen.findByTestId('stage-advance-before'));
    await user.click(screen.getByTestId('stage-confirm-submit'));

    await waitFor(() => expect(getVisitProcessDetail).toHaveBeenCalledTimes(2));
    expect(getReminderSettings).toHaveBeenCalledTimes(2);
  });

  it('hands the child sections the FRESH status, not the detail payload it happens to hold', async () => {
    // Both sections gate their own invite/logistics buttons on the status they are given, so passing
    // a stale one re-creates the bug inside them.
    getVisitProcessPermissions.mockResolvedValue(permission({ instanceStatus: 'DURING_VISIT' }));
    getVisitProcessDetail.mockResolvedValue(detail({ instanceStatus: 'BEFORE_VISIT' }));

    render(<VisitProcess />);

    await waitFor(() => expect(screen.getByTestId('participants-status')).toHaveTextContent('DURING_VISIT'));
    expect(screen.getByTestId('logistics-status')).toHaveTextContent('DURING_VISIT');
  });

  it('keeps the tab read-only on a fresh load at DURING_VISIT', async () => {
    getVisitProcessPermissions.mockResolvedValue(permission({ instanceStatus: 'DURING_VISIT' }));
    getVisitProcessDetail.mockResolvedValue(detail({ instanceStatus: 'DURING_VISIT' }));

    render(<VisitProcess />);

    await waitFor(() => expect(screen.getByTestId('before-readonly-banner')).toBeInTheDocument());
    expect(prepControls().saveReminders).not.toBeInTheDocument();
    expect(prepControls().saveNote).not.toBeInTheDocument();
  });

  it('does not offer preparation mutations at ASSIGNED — the backend refuses them there', async () => {
    // The recovery from ASSIGNED is the "Bắt đầu chuẩn bị" button, not a form whose only possible
    // answer is "Host chưa bắt đầu giai đoạn chuẩn bị".
    getVisitProcessPermissions.mockResolvedValue(permission({
      instanceStatus: 'ASSIGNED', canEditBeforeVisit: false, canStartPreparation: true, canStartVisit: false,
    }));
    getVisitProcessDetail.mockResolvedValue(detail({ instanceStatus: 'ASSIGNED' }));

    render(<VisitProcess />);

    await waitFor(() => expect(screen.getByTestId('participants-status')).toHaveTextContent('ASSIGNED'));
    expect(screen.getByTestId('start-preparation-banner')).toBeInTheDocument();
    expect(prepControls().saveReminders).not.toBeInTheDocument();
    expect(prepControls().saveNote).not.toBeInTheDocument();
    expect(prepControls().agendaTemplate).not.toBeInTheDocument();
  });
});
