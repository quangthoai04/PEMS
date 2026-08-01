/**
 * "Trước tiếp khách" opens with everything already unfolded.
 *
 * The Host used to land on this tab and have to bung four separate panels before they could see the
 * preparation they came to do. The defaults now open all five — "1. Thông tin chung" with its three
 * sub-blocks, plus "2. Chuẩn bị chi tiết" — and, because leaving the tab and coming back is a fresh
 * entry rather than a resumed session, the effect re-opens them.
 *
 * Collapsing by hand is still the user's to keep WITHIN one visit to the tab; that is asserted here
 * too, because "open by default" would otherwise be indistinguishable from "cannot be closed".
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const getVisitProcessPermissions = vi.fn();
const getVisitProcessDetail = vi.fn();
const getReminderSettings = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitProcessPermissions: (...a: unknown[]) => getVisitProcessPermissions(...a),
    getVisitProcessDetail: (...a: unknown[]) => getVisitProcessDetail(...a),
    getReminderSettings: (...a: unknown[]) => getReminderSettings(...a),
  },
}));

// The heavy real children of these panels (agenda editor, participant invites, logistics cards) are
// not what this file measures — only whether their containing block is expanded.
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

const navigate = vi.fn();
vi.mock('react-router-dom', () => ({
  useNavigate: () => navigate,
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
  canStartVisit: true,
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
  plannedStartAt: '2026-08-20T09:00:00',
  plannedEndAt: '2026-08-20T11:30:00',
  agendaItems: [],
  requestSummary: {},
};

/** The five blocks the requirement names, in the order they appear down the tab. */
const BLOCKS = [
  'before-info-body',
  'before-registrant-body',
  'before-delegation-body',
  'before-setup-body',
  'before-prep-body',
] as const;

const expectAllOpen = async () => {
  for (const id of BLOCKS) {
    await waitFor(() => expect(screen.getByTestId(id)).toBeTruthy());
  }
};

beforeEach(() => {
  vi.clearAllMocks();
  getVisitProcessPermissions.mockResolvedValue(HOST_PERMISSION);
  getVisitProcessDetail.mockResolvedValue(DETAIL);
  getReminderSettings.mockResolvedValue({ items: [] });
});

describe('Trước tiếp khách — default expansion', () => {
  it('opens all five blocks on the first render of the tab', async () => {
    render(<VisitProcess />);
    await expectAllOpen();
  });

  it('still lets the user collapse a block by hand', async () => {
    const user = userEvent.setup();
    render(<VisitProcess />);
    await expectAllOpen();

    await user.click(screen.getByRole('heading', { name: /2\. Chuẩn bị chi tiết/ }));
    await waitFor(() => expect(screen.queryByTestId('before-prep-body')).toBeNull());

    // Collapsing one must not disturb the others.
    expect(screen.getByTestId('before-info-body')).toBeTruthy();
    expect(screen.getByTestId('before-setup-body')).toBeTruthy();
  });

  it('re-opens the collapsed blocks after leaving the tab and coming back', async () => {
    // Tab 2 is locked until the instance has actually advanced, so the round trip is only
    // reachable once the visit is under way — the Before tab stays viewable (read-only) then.
    getVisitProcessPermissions.mockResolvedValue({
      ...HOST_PERMISSION,
      instanceStatus: 'DURING_VISIT',
      canEditBeforeVisit: false,
      canStartVisit: false,
      canCompleteVisit: true,
    });

    const user = userEvent.setup();
    render(<VisitProcess />);
    await expectAllOpen();

    await user.click(screen.getByRole('heading', { name: /2\. Chuẩn bị chi tiết/ }));
    await user.click(screen.getByRole('heading', { name: /Thông tin người tạo/ }));
    // Both are waited on: AnimatePresence keeps an exiting node mounted until its animation ends.
    await waitFor(() => expect(screen.queryByTestId('before-prep-body')).toBeNull());
    await waitFor(() => expect(screen.queryByTestId('before-registrant-body')).toBeNull());

    await user.click(screen.getByRole('button', { name: /2\. Đang tiếp khách/ }));
    await waitFor(() => expect(screen.queryByTestId('before-info-body')).toBeNull());

    await user.click(screen.getByRole('button', { name: /1\. Trước tiếp khách/ }));
    await expectAllOpen();
  });
});
