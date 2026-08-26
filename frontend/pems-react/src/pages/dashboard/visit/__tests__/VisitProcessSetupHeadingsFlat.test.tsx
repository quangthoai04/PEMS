/**
 * "Thiết lập & Điều phối sự kiện (Set up)" subheadings — 1. Agenda / 2. Thành phần tham gia / etc. —
 * used to render inside an orange pill (background + border + rounded box + vertical bar), which
 * read as "card inside a card". They are now flat headings with the accent kept only on the number.
 * This is a pure visual refinement: no gating, action, or data logic changes.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

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

beforeEach(() => {
  vi.clearAllMocks();
  getVisitProcessPermissions.mockResolvedValue(HOST_PERMISSION);
  getVisitProcessDetail.mockResolvedValue(DETAIL);
  getReminderSettings.mockResolvedValue({ items: [] });
});

describe('Set up section — flat subheadings (no orange pill)', () => {
  it('renders "1. Agenda" without the old pill container classes', async () => {
    render(<VisitProcess />);
    const heading = await screen.findByRole('heading', { name: /Agenda/ });

    expect(heading).toBeTruthy();
    expect(heading.className).not.toMatch(/bg-orange-50/);
    expect(heading.className).not.toMatch(/border-orange-100/);
    expect(heading.className).not.toMatch(/rounded-lg/);
    // The vertical accent bar (`w-1.5 h-4 bg-[#f37021] rounded-full`) is gone too.
    expect(heading.querySelector('.rounded-full')).toBeNull();
  });

  it('renders "2. Thành phần tham gia" without the old pill container classes', async () => {
    render(<VisitProcess />);
    const heading = await screen.findByRole('heading', { name: /Thành phần tham gia/ });

    expect(heading).toBeTruthy();
    expect(heading.className).not.toMatch(/bg-orange-50/);
    expect(heading.className).not.toMatch(/border-orange-100/);
    expect(heading.className).not.toMatch(/rounded-lg/);
    expect(heading.querySelector('.rounded-full')).toBeNull();
  });

  it('still offers "Áp dụng mẫu Agenda" for a Host who can edit', async () => {
    render(<VisitProcess />);
    await waitFor(() => expect(screen.getByTestId('before-setup-body')).toBeTruthy());
    expect(screen.getByRole('button', { name: /Áp dụng mẫu Agenda/ })).toBeInTheDocument();
  });

  it('still renders the participant section for a Host', async () => {
    render(<VisitProcess />);
    expect(await screen.findByText('participant-invitation-section')).toBeInTheDocument();
  });
});
