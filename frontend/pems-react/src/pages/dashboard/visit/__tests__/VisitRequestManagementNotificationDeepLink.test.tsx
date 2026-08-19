/**
 * `openVisitRequestId`/`openVisitInstanceId` — the ONE-SHOT COMMAND a Visit notification deep link
 * lands with (see PEMS_Notification_Visit_DeepLink_OneShot_Fix_Plan.md).
 *
 * Two separate bugs are pinned here.
 *
 * 1) REPLAY. Before this fix the page never consumed the parameter that identified a notification's
 *    target — closing whatever it opened and then changing tab/filter/page/search left the trigger
 *    in the URL, and the next `setSearchParams` call replayed it (the exact shape of the
 *    `feedbackVisitInstanceId` bug this mirrors, see VisitRequestManagementFeedbackDeepLink.test.tsx).
 *
 * 2) STALE STATE. The notification only ever names WHERE to go (a request/instance id) — never what
 *    is currently true about it. A campus that was WAITING_REQUEST_APPROVAL when the notification
 *    was created may be decided, rejected, cancelled or closed by the time it is clicked. This file
 *    proves the resolver never trusts the notification for status/actions: it re-fetches the row
 *    through the same list endpoint every other row on this screen uses, and opens whatever the
 *    CURRENT `primaryEntryContext`/`allowedActions` say — never a resurrected Duyệt/Từ chối control
 *    left over from the moment the notification was created.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';

const listMock = vi.fn();
const invitationsMock = vi.fn();
const hostCandidatesMock = vi.fn();
const navigateMock = vi.fn();
const showErrorToastMock = vi.fn();
const showSuccessToastMock = vi.fn();

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

vi.mock('../../../../shared/utils/toast', () => ({
  showErrorToast: (...args: unknown[]) => showErrorToastMock(...args),
  showSuccessToast: (...args: unknown[]) => showSuccessToastMock(...args),
  getApiErrorMessage: (_err: unknown, fallback: string) => fallback,
}));

let currentUser: { userId: string; roleCode: string; subRole?: string | null } =
  { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: currentUser }),
}));

/** Stubbed exactly like the row's own primary "Duyệt & phân công" button opens it. */
vi.mock('../../../../components/modals/AssignHostModal', () => ({
  AssignHostModal: ({ isOpen, visitRequestId, visitInstanceId, onClose }: {
    isOpen: boolean;
    visitRequestId: number;
    visitInstanceId: number | null;
    onClose: () => void;
  }) => (isOpen ? (
    <div data-testid="assign-host-modal" data-request={visitRequestId} data-instance={String(visitInstanceId)}>
      <button type="button" data-testid="assign-close" onClick={onClose}>close</button>
    </div>
  ) : null),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────

const DELEGATION = 'Jeju Tourism Technology Delegation';
const REQUEST_ID = 4001;
const INSTANCE_ID = 5001;

const baseRow = (over: Record<string, unknown> = {}) => ({
  visitRequestId: REQUEST_ID,
  visitInstanceId: INSTANCE_ID,
  requestCode: `VR-2026-${REQUEST_ID}`,
  delegationName: DELEGATION,
  partnerName: 'Jeju Tech Agency',
  requestStatus: 'PENDING_APPROVAL',
  campusStatus: 'WAITING_REQUEST_APPROVAL',
  visitScope: 'SINGLE_CAMPUS',
  campusId: 1,
  campusName: 'FPT University Hà Nội',
  campusCount: 1,
  createdByUserId: 10,
  registrantUserId: 10,
  currentHostUserId: null,
  hostName: null,
  currentUserIsHost: false,
  rowVersion: 4,
  visitorUserId: 10,
  visitorName: 'Nguyen Van A',
  isCurrentUserParticipant: false,
  participantId: null,
  participantRole: null,
  currentUserRelation: 'CAMPUS_REVIEWER',
  relationLabel: 'Cần bạn duyệt',
  statusLabel: 'Chờ duyệt',
  relations: ['CAMPUS_REVIEWER'],
  relationContexts: [{
    relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1,
    campusName: 'FPT University Hà Nội', entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1,
  }],
  primaryEntryContext: 'CAMPUS_REVIEW',
  primaryEntryVisitInstanceId: INSTANCE_ID,
  nextTask: null,
  expectedStartAt: null,
  expectedEndAt: null,
  plannedStartAt: '2026-08-25T09:00:00',
  plannedEndAt: '2026-08-25T12:00:00',
  createdAt: '2026-08-19T09:00:00',
  submittedAt: '2026-08-19T09:00:00',
  cancelledAt: null,
  cancellationReason: null,
  decisionNote: null,
  canExpandCampuses: false,
  canViewRequestDetail: true,
  campusProgressItems: [],
  allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST', 'CAMPUS_REJECT'],
  capabilities: [],
  ...over,
});

const renderAt = (search: string, row: Record<string, unknown> = baseRow()) => {
  listMock.mockResolvedValue({ items: [row], totalItems: 1 });
  return render(
    <MemoryRouter initialEntries={[`/dashboard/visit${search}`]}>
      <UrlProbe />
      <VisitRequestManagement />
    </MemoryRouter>,
  );
};

const UrlProbe = () => <span data-testid="url-search">{useLocation().search}</span>;
const params = () => new URLSearchParams(screen.getByTestId('url-search').textContent ?? '');

const searchFor = async (keyword: string) => {
  await userEvent.type(await screen.findByTestId('visit-search-input'), keyword);
  await waitFor(() => expect(params().get('keyword')).toBe(keyword), { timeout: 3000 });
};

beforeEach(() => {
  vi.clearAllMocks();
  currentUser = { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
  invitationsMock.mockResolvedValue([]);
  hostCandidatesMock.mockResolvedValue([]);
});

// ── Consuming the command exactly once ──────────────────────────────────────────────────────────

describe('a notification deep link is consumed exactly once', () => {
  it('opens the live approve flow for a campus still WAITING_REQUEST_APPROVAL', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`);

    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-request', String(REQUEST_ID));
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));

    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(params().get('openVisitInstanceId')).toBeNull();
  });

  it('resolves regardless of the current tab/filter — it is not a list filter', async () => {
    // The old bug: `visitRequestId` only narrowed whatever tab/filter was already active. This
    // command must find its target even starting from an unrelated tab.
    renderAt(`?tab=all&status=CLOSED&openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`);
    await screen.findByTestId('assign-host-modal');
  });

  it('keeps every persistent filter across the consume', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`
      + '&tab=all&keyword=ha&page=2',
    );
    await screen.findByTestId('assign-host-modal');

    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(params().get('tab')).toBe('all');
    expect(params().get('keyword')).toBe('ha');
    expect(params().get('page')).toBe('2');
  });

  it.each(['abc', '0', '-1'])(
    'opens nothing for id %s and still cleans both command params away',
    async raw => {
      renderAt(`?openVisitRequestId=${raw}&openVisitInstanceId=${INSTANCE_ID}&tab=all`);
      await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
      expect(params().get('openVisitInstanceId')).toBeNull();
      expect(screen.queryByTestId('assign-host-modal')).toBeNull();
      expect(params().get('tab')).toBe('all');
    },
  );
});

// ── No path back to a replay ────────────────────────────────────────────────────────────────────

describe('the target does not come back on its own', () => {
  it('stays closed after the user closes it and then searches', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`);
    await screen.findByTestId('assign-host-modal');
    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());

    await userEvent.click(screen.getByTestId('assign-close'));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();

    await searchFor('ha');
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
    expect(params().get('openVisitRequestId')).toBeNull();
  });

  it('never carries the command forward through a filter/search URL update', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&tab=all`);
    await screen.findByTestId('assign-host-modal');
    await userEvent.click(screen.getByTestId('assign-close'));

    await searchFor('ha');
    expect(params().get('openVisitRequestId')).toBeNull();
    expect(params().get('openVisitInstanceId')).toBeNull();
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── Current state wins over the notification's historical snapshot ────────────────────────────────

describe('current backend state decides what opens, never the notification snapshot', () => {
  it('does NOT resurrect the approve modal once the campus has already been decided', async () => {
    // Same notification target (request 4001 / instance 5001) the "still pending" test above uses —
    // only what the backend NOW reports about it differs, exactly as if a second reviewer decided it
    // between the notification firing and this click.
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseRow({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        currentHostUserId: 42,
        hostName: 'IC Staff Hà Nội',
        currentUserIsHost: true,
        primaryEntryContext: 'HOST_PROCESS',
        allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/process/${INSTANCE_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('opens the current read-only detail for a campus that was rejected after the notification fired', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseRow({
        requestStatus: 'REJECTED',
        campusStatus: 'REJECTED',
        primaryEntryContext: 'REQUEST_DETAIL',
        primaryEntryVisitInstanceId: null,
        allowedActions: ['VIEW_DETAIL'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('opens the current read-only detail for a campus that was cancelled after the notification fired', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseRow({
        requestStatus: 'CANCELLED',
        campusStatus: 'CANCELLED',
        primaryEntryContext: 'REQUEST_DETAIL',
        primaryEntryVisitInstanceId: null,
        allowedActions: ['VIEW_DETAIL'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('never auto-runs a mutation — opening the approve flow still requires an explicit submit inside it', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`);
    await screen.findByTestId('assign-host-modal');
    // The mocked modal exposes no submit action reachable from the notification click itself — the
    // resolver's job stops at OPEN, and any decision is a separate, explicit act inside the modal.
    expect(showSuccessToastMock).not.toHaveBeenCalled();
  });
});

// ── Not found / lost permission ─────────────────────────────────────────────────────────────────

describe('a target the caller can no longer resolve', () => {
  it('reports it and consumes the command without crashing (empty result, no guess from the notification text)', async () => {
    listMock.mockResolvedValue({ items: [], totalItems: 0 });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`]}>
        <UrlProbe />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    // (error, fallback) — the same calling convention every other call site in this file uses;
    // passing the message as `error` would silently fall through to a generic toast instead.
    await waitFor(() => expect(showErrorToastMock).toHaveBeenCalledWith(
      null, expect.stringContaining('Không tìm thấy'),
    ));
    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── Multi-campus exact targeting ────────────────────────────────────────────────────────────────

describe('multi-campus exact instance targeting', () => {
  it('opens the named instance, not another campus of the same request', async () => {
    const otherInstance = baseRow({
      visitInstanceId: 9002,
      campusId: 2,
      campusName: 'FPT University Đà Nẵng',
      primaryEntryVisitInstanceId: 9002,
      relationContexts: [{
        relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: 9002, campusId: 2,
        campusName: 'FPT University Đà Nẵng', entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1,
      }],
    });
    listMock.mockResolvedValue({ items: [otherInstance, baseRow()], totalItems: 2 });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`]}>
        <UrlProbe />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));
  });
});

// ── The ordinary route is untouched ─────────────────────────────────────────────────────────────

describe('a visit list opened without the command', () => {
  it('opens nothing and leaves the URL alone', async () => {
    renderAt('?tab=all');
    await waitFor(() => expect(listMock).toHaveBeenCalled());
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
    expect(params().get('tab')).toBe('all');
  });
});
