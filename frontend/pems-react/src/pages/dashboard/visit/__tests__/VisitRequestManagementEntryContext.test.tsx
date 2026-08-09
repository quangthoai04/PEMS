/**
 * FILTER != AUTHORIZATION != ENTRY CONTEXT, from the frontend's side of the contract.
 *
 * The list renders one row for a reader who is several things to it at once. Three separate things
 * have to come out of that, and the page used to conflate all three by branching on the active tab:
 *
 *   • WHERE THE ROW OPENS comes from the backend's `primaryEntryContext` + `primaryEntryVisitInstanceId`.
 *     The same row under two filters opens two different screens — and that is the ONLY difference a
 *     filter is allowed to make.
 *   • WHAT ELSE THE READER MAY REACH is every other relation they hold, offered in the ⋯ menu, so
 *     changing filter is never the only way to get to a screen you are entitled to.
 *   • WHAT THEY ARE to the row is all of their relations as badges, not one tab-derived label.
 *
 * A row with no relation contexts at all (HO monitoring, a department assignment) must keep its
 * existing routing untouched — the last test here is the guard for that.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const listMock = vi.fn();
const invitationsMock = vi.fn();
const hostCandidatesMock = vi.fn();
const navigateMock = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitRequestManagementList: (...args: unknown[]) => listMock(...args),
    getMyInvitations: (...args: unknown[]) => invitationsMock(...args),
    getHostCandidates: (...args: unknown[]) => hostCandidatesMock(...args),
    visitInvitations: { getMyInvitations: (...a: unknown[]) => invitationsMock(...a) },
  },
}));

vi.mock('../../../../features/feedbacks/api/visitFeedbackApi', () => ({
  visitFeedbackApi: { getMyPending: vi.fn().mockResolvedValue({ items: [] }) },
}));

vi.mock('../../../../features/campus-management/hooks/useCampusManagement', () => ({
  useCampusFilterOptions: () => null,
}));

vi.mock('../../../../shared/features/perCampusV2Capability', () => ({
  usePerCampusV2Capability: () => ({ status: 'ready', enabled: true, retry: vi.fn() }),
}));

let currentUser: { userId: string; roleCode: string; subRole?: string } =
  { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: currentUser }),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────

const DELEGATION = 'Jeju Tourism Technology Delegation';

/**
 * A live campus row for somebody who BOTH registered the request and now hosts its campus — the
 * multi-relation shape every claim in this file needs. Which screen it opens is left to each test,
 * because that is exactly the thing under test.
 */
const multiRelationRow = (over: Record<string, unknown> = {}) => ({
  visitRequestId: 4001,
  visitInstanceId: 5001,
  requestCode: 'VR-2026-4001',
  delegationName: DELEGATION,
  partnerName: 'Jeju Tech Agency',
  requestStatus: 'APPROVED',
  campusStatus: 'BEFORE_VISIT',
  visitScope: 'SINGLE_CAMPUS',
  campusId: 1,
  campusName: 'FPT University Hà Nội',
  campusCount: 1,
  createdByUserId: 77,
  registrantUserId: 77,
  currentHostUserId: 77,
  hostName: 'IC Staff Hà Nội',
  currentUserIsHost: true,
  rowVersion: 4,
  visitorUserId: 10,
  visitorName: 'Nguyen Van A',
  isCurrentUserParticipant: false,
  participantRole: null,
  currentUserRelation: 'HOST',
  relationLabel: 'Bạn phụ trách tiếp đón',
  statusLabel: 'Đang chuẩn bị',
  relations: ['REGISTRANT', 'HOST'],
  relationContexts: [
    {
      relation: 'HOST', scope: 'INSTANCE', visitInstanceId: 5001, campusId: 1,
      campusName: 'FPT University Hà Nội', entryContext: 'HOST_PROCESS',
      requiresAction: true, priority: 2,
    },
    {
      relation: 'REGISTRANT', scope: 'REQUEST', visitInstanceId: null, campusId: null,
      campusName: null, entryContext: 'REQUEST_DETAIL', requiresAction: false, priority: 5,
    },
  ],
  primaryEntryContext: 'HOST_PROCESS',
  primaryEntryVisitInstanceId: 5001,
  nextTask: null,
  expectedStartAt: null,
  expectedEndAt: null,
  plannedStartAt: '2026-08-15T09:00:00',
  plannedEndAt: '2026-08-15T12:00:00',
  createdAt: '2026-07-01T09:00:00',
  submittedAt: '2026-07-01T09:00:00',
  cancelledAt: null,
  cancellationReason: null,
  decisionNote: null,
  canExpandCampuses: false,
  canViewRequestDetail: true,
  campusProgressItems: [],
  allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'],
  capabilities: [],
  ...over,
});

const renderList = (rows: unknown[]) => {
  listMock.mockResolvedValue({ items: rows, totalItems: rows.length });
  return render(<MemoryRouter><VisitRequestManagement /></MemoryRouter>);
};

const desktop = () => within(screen.getByTestId('visit-list-desktop'));

const clickRow = async () => {
  await waitFor(() => desktop().getByText(DELEGATION));
  await userEvent.click(desktop().getByText(DELEGATION));
};

const paths = () => navigateMock.mock.calls.map(([path]) => path as string);

beforeEach(() => {
  vi.clearAllMocks();
  currentUser = { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
  invitationsMock.mockResolvedValue([]);
  hostCandidatesMock.mockResolvedValue([]);
});

// ── Entry context decides the screen ─────────────────────────────────────────────────────────────

describe('the backend decides which screen a row opens', () => {
  it('opens the host process for the campus the entry context names', async () => {
    renderList([multiRelationRow()]);
    await clickRow();
    expect(paths()).toContain('/dashboard/visit/process/5001');
  });

  it('opens the request instead when the same row is read through the registrant relation', async () => {
    // Identical rights, identical relations — only the entry context differs, because the reader
    // picked "Đơn tôi đăng ký". The host actions are still granted; they simply are not the default.
    renderList([multiRelationRow({
      primaryEntryContext: 'REQUEST_DETAIL',
      primaryEntryVisitInstanceId: null,
    })]);
    await clickRow();
    expect(paths()).toContain('/dashboard/visit/v2/4001');
    expect(paths()).not.toContain('/dashboard/visit/process/5001');
  });

  it('opens the campus review through the canonical submitted-form route, not a new one', async () => {
    renderList([multiRelationRow({
      campusStatus: 'WAITING_REQUEST_APPROVAL',
      requestStatus: 'PENDING_APPROVAL',
      currentHostUserId: null,
      hostName: null,
      currentUserIsHost: false,
      relations: ['REGISTRANT', 'CAMPUS_REVIEWER'],
      relationContexts: [
        {
          relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: 5001, campusId: 1,
          campusName: 'FPT University Hà Nội', entryContext: 'CAMPUS_REVIEW',
          requiresAction: true, priority: 1,
        },
      ],
      primaryEntryContext: 'CAMPUS_REVIEW',
      primaryEntryVisitInstanceId: 5001,
      allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST', 'CAMPUS_REJECT'],
    })]);
    await clickRow();
    expect(paths()).toContain('/dashboard/visit/v2/4001');
  });

  it('leaves a row with no relations on its established routing', async () => {
    // HO monitoring: the backend sends no entry context at all, and the page must not invent one.
    currentUser = { userId: '90', roleCode: 'HO' };
    renderList([multiRelationRow({
      registrantUserId: 10,
      currentHostUserId: 42,
      currentUserIsHost: false,
      relations: [],
      relationContexts: [],
      primaryEntryContext: null,
      primaryEntryVisitInstanceId: null,
      currentUserRelation: 'HO_MONITOR',
      relationLabel: 'Chỉ theo dõi',
      allowedActions: ['VIEW_DETAIL', 'OPEN_PROCESS_SUMMARY'],
    })]);
    await clickRow();
    expect(paths()).toContain('/dashboard/visit/process-summary/5001');
  });
});

// ── The other relations stay reachable ───────────────────────────────────────────────────────────

describe('every relation the reader holds stays reachable from the row', () => {
  it('offers the host process in the ⋯ menu when the row itself opens the request', async () => {
    renderList([multiRelationRow({
      primaryEntryContext: 'REQUEST_DETAIL',
      primaryEntryVisitInstanceId: null,
    })]);
    await waitFor(() => desktop().getByText(DELEGATION));

    await userEvent.click(screen.getByTestId('row-menu-desktop-5001'));
    const panel = screen.getByTestId('row-menu-desktop-5001-panel');
    const entry = within(panel).getByText(/Mở trang xử lý/);
    await userEvent.click(entry);

    expect(paths()).toContain('/dashboard/visit/process/5001');
  });

  it('does not offer a relation the backend did not also grant an action for', async () => {
    // The relation is real, the screen is not open to them right now (no OPEN_HOST_PROCESS). Both
    // have to agree before an entry appears — a menu item that 403s teaches nothing.
    renderList([multiRelationRow({
      primaryEntryContext: 'REQUEST_DETAIL',
      primaryEntryVisitInstanceId: null,
      allowedActions: ['VIEW_DETAIL'],
    })]);
    await waitFor(() => desktop().getByText(DELEGATION));

    await userEvent.click(screen.getByTestId('row-menu-desktop-5001'));
    const panel = screen.getByTestId('row-menu-desktop-5001-panel');
    expect(within(panel).queryByText(/Mở trang xử lý/)).not.toBeInTheDocument();
  });
});

// ── Badges show all of the relations, not one ────────────────────────────────────────────────────

describe('the row says everything the reader is to it', () => {
  it('shows both the host and the registrant relation on one row', async () => {
    renderList([multiRelationRow()]);
    await waitFor(() => desktop().getByText(DELEGATION));
    expect(desktop().getByText(/Phụ trách: FPT University Hà Nội/)).toBeInTheDocument();
    expect(desktop().getByText('Người đăng ký')).toBeInTheDocument();
  });

  it('names the campus whose decision is waiting on the reader', async () => {
    renderList([multiRelationRow({
      campusStatus: 'WAITING_REQUEST_APPROVAL',
      requestStatus: 'PENDING_APPROVAL',
      currentUserIsHost: false,
      currentHostUserId: null,
      hostName: null,
      relations: ['CAMPUS_REVIEWER'],
      relationContexts: [
        {
          relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: 5001, campusId: 1,
          campusName: 'FPT University Hà Nội', entryContext: 'CAMPUS_REVIEW',
          requiresAction: true, priority: 1,
        },
      ],
      primaryEntryContext: 'CAMPUS_REVIEW',
      primaryEntryVisitInstanceId: 5001,
      allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST'],
    })]);
    await waitFor(() => desktop().getByText(DELEGATION));
    expect(desktop().getByText(/Cần bạn duyệt: FPT University Hà Nội/)).toBeInTheDocument();
  });

  it('says nothing about a campus the reader merely leads with no decision pending', async () => {
    // "I am the leader here" is not news on a list that only ever shows them their own campus.
    renderList([multiRelationRow({
      relations: ['CAMPUS_REVIEWER'],
      relationContexts: [
        {
          relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: 5001, campusId: 1,
          campusName: 'FPT University Hà Nội', entryContext: 'PROCESS_SUMMARY',
          requiresAction: false, priority: 5,
        },
      ],
      currentUserIsHost: false,
      primaryEntryContext: 'PROCESS_SUMMARY',
      primaryEntryVisitInstanceId: 5001,
      allowedActions: ['VIEW_DETAIL', 'OPEN_PROCESS_SUMMARY'],
    })]);
    await waitFor(() => desktop().getByText(DELEGATION));
    expect(desktop().queryByText(/Cần bạn duyệt/)).not.toBeInTheDocument();
  });
});
