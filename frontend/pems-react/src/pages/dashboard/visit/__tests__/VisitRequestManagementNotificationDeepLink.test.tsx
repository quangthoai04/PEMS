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
import { MemoryRouter, useLocation, useSearchParams } from 'react-router-dom';

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

/**
 * Simulates the Bell/NotificationsPage/dashboard attaching a NEW one-shot command onto the SAME
 * mounted `VisitRequestManagement` instance — exactly what a real second click on a notification does
 * (the SPA never remounts the page; it just navigates to the same base path with a fresh query
 * string). Uses the real (unmocked) `useSearchParams` setter — only `useNavigate` is mocked in this
 * file — so it drives the actual router location `VisitRequestManagement`'s own `useSearchParams()`
 * observes, the same way `setSearchParams` inside the component itself works.
 */
const ReplaySameCommand = ({ search }: { search: string }) => {
  const [, setSearchParams] = useSearchParams();
  return (
    <button type="button" data-testid="replay-command" onClick={() => setSearchParams(new URLSearchParams(search))}>
      replay
    </button>
  );
};

/** Like {@link ReplaySameCommand}, but parameterized by testid so two DIFFERENT commands (A/B) can
 * each be triggered independently on the same mounted instance — simulating two separate real clicks. */
const TriggerCommand = ({ testId, search }: { testId: string; search: string }) => {
  const [, setSearchParams] = useSearchParams();
  return (
    <button type="button" data-testid={testId} onClick={() => setSearchParams(new URLSearchParams(search))}>
      trigger
    </button>
  );
};

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
  it('opens the live approve flow for a campus still WAITING_REQUEST_APPROVAL, given an explicit VISIT_REVIEW intent', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`);

    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-request', String(REQUEST_ID));
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));

    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(params().get('openVisitInstanceId')).toBeNull();
  });

  // Plan PEMS_FIX_NOTIFICATION_SEMANTIC_ROUTING_SYSTEM_WIDE.md §3/§4/§21 — the reported live bug: a
  // notification with no classifiable intent at all (no `notificationIntent` on the URL — the exact
  // shape a HISTORICAL "Visitor đã cập nhật đơn"/"Thông tin cơ sở chờ duyệt đã được cập nhật" row
  // reaches this page with, since it predates the eventKey scheme) used to fall through
  // `intent == null` and open the SAME live approve/assign-host modal as a real
  // VISIT_REQUEST_WAITING_APPROVAL notification — even though its own meaning is "something changed,
  // go look", never "a decision is waiting". Only an EXPLICIT VISIT_REVIEW intent may ever open it now.
  it('a command with NO notificationIntent never opens the approve modal, even though the campus is still pending and APPROVE is allowed', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`);

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();

    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(params().get('openVisitInstanceId')).toBeNull();
  });

  it('resolves regardless of the current tab/filter — it is not a list filter', async () => {
    // The old bug: `visitRequestId` only narrowed whatever tab/filter was already active. This
    // command must find its target even starting from an unrelated tab.
    renderAt(`?tab=all&status=CLOSED&openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`);
    await screen.findByTestId('assign-host-modal');
  });

  it('keeps every persistent filter across the consume', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`
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
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`);
    await screen.findByTestId('assign-host-modal');
    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());

    await userEvent.click(screen.getByTestId('assign-close'));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();

    await searchFor('ha');
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
    expect(params().get('openVisitRequestId')).toBeNull();
  });

  it('never carries the command forward through a filter/search URL update', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW&tab=all`);
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
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`,
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
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`);
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
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`]}>
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

// ── BUG 1 (plan §3/§56 DL-01): the SAME notification clicked a second time must open again ─────────

describe('the exact same notification clicked a second time still opens (second-click regression)', () => {
  it('re-opens the approve flow when the identical command reappears on an already-consumed instance', async () => {
    listMock.mockResolvedValue({ items: [baseRow()], totalItems: 1 });
    render(
      <MemoryRouter initialEntries={['/dashboard/visit']}>
        <UrlProbe />
        <ReplaySameCommand search={`openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`} />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    // Nothing open yet — the page was not entered via a notification link.
    await waitFor(() => expect(listMock).toHaveBeenCalled());
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();

    // CLICK 1 (simulated): the command lands on the URL, the modal opens, the command is stripped.
    await userEvent.click(screen.getByTestId('replay-command'));
    await screen.findByTestId('assign-host-modal');
    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());

    // User closes it.
    await userEvent.click(screen.getByTestId('assign-close'));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();

    // CLICK 2 (simulated): the exact SAME requestId/instanceId reappears — a real second click on
    // the identical Bell notification. Before the fix, `consumedNotificationCommandRef` was still
    // holding this same commandKey from CLICK 1 and this second, perfectly ordinary user action was
    // silently dropped as if it were a StrictMode duplicate.
    await userEvent.click(screen.getByTestId('replay-command'));
    await screen.findByTestId('assign-host-modal');
    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
  });
});

// ── Rapid-click race (live-verification round) ──────────────────────────────────────────────────
//
// Live-reproduced in a real browser: click notification A (its target-resolution round-trip happens
// to be slow), then — before A resolves — click notification B on the SAME mounted page (a real
// second click never remounts `VisitRequestManagement`, it just changes the query string). B resolves
// first and opens correctly; A's response then finally lands and SILENTLY REPLACED what B had just
// opened, because `resolveAndOpenNotificationTarget` had no ordering guard of its own (unlike
// `loadDelegations`, which already carries one — `requestVersionRef`). Root cause + fix: a matching
// `notificationTargetVersionRef`, checked once right after the fetch resolves, before any state
// mutation.

describe('rapid-click race: a slower earlier notification click must never overwrite a faster later one', () => {
  it('B (fast) stays open after A (slow) finally resolves', async () => {
    const OTHER_REQUEST_ID = 4002;
    const OTHER_INSTANCE_ID = 5002;
    let resolveSlowCall: (value: { items: unknown[]; totalItems: number }) => void = () => {};
    const slowCall = new Promise<{ items: unknown[]; totalItems: number }>((resolve) => { resolveSlowCall = resolve; });
    let callCount = 0;
    listMock.mockImplementation(() => {
      callCount += 1;
      if (callCount === 1) return Promise.resolve({ items: [baseRow()], totalItems: 1 }); // initial mount load
      if (callCount === 2) return slowCall; // command A — deliberately held open
      return Promise.resolve({
        items: [baseRow({ visitRequestId: OTHER_REQUEST_ID, visitInstanceId: OTHER_INSTANCE_ID })],
        totalItems: 1,
      }); // command B — resolves immediately
    });

    render(
      <MemoryRouter initialEntries={['/dashboard/visit']}>
        <UrlProbe />
        <TriggerCommand testId="trigger-a" search={`openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`} />
        <TriggerCommand testId="trigger-b" search={`openVisitRequestId=${OTHER_REQUEST_ID}&openVisitInstanceId=${OTHER_INSTANCE_ID}&notificationIntent=VISIT_REVIEW`} />
        <VisitRequestManagement />
      </MemoryRouter>,
    );
    await waitFor(() => expect(listMock).toHaveBeenCalledTimes(1));

    await userEvent.click(screen.getByTestId('trigger-a')); // fires call #2 (slow, held open)
    await waitFor(() => expect(listMock).toHaveBeenCalledTimes(2));

    await userEvent.click(screen.getByTestId('trigger-b')); // fires call #3 (fast, resolves immediately)
    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-request', String(OTHER_REQUEST_ID));

    // A's delayed response finally lands — it must NOT clobber B's already-open modal.
    resolveSlowCall({ items: [baseRow()], totalItems: 1 });
    await waitFor(() => expect(screen.getByTestId('assign-host-modal'))
      .toHaveAttribute('data-request', String(OTHER_REQUEST_ID)));
  });
});

// ── BUG 2 (plan §5/§7/§14/§57 CR-03): semantic intent gates the approve escalation ──────────────────

describe('notificationIntent gates whether the notification may escalate to the live approve control', () => {
  it('VISIT_HISTORY never opens the approve modal even though the campus is still pending and APPROVE is allowed', async () => {
    // Same row shape as the "still WAITING_REQUEST_APPROVAL" test above (genuinely pending,
    // APPROVE_AND_ASSIGN_HOST allowed) — the ONLY difference is the notification's own semantic
    // intent. This is the exact regression the plan calls out: "Visitor đã cập nhật đơn" must never
    // auto-open the approve/assign-host control, no matter how permissive the current state is.
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_HISTORY`,
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}#history`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('strips notificationIntent from the URL along with the rest of the one-shot command', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_HISTORY`,
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalled());
    expect(params().get('notificationIntent')).toBeNull();
    expect(params().get('openVisitRequestId')).toBeNull();
    expect(params().get('openVisitInstanceId')).toBeNull();
  });

  it('VISIT_REVIEW still opens the approve flow for a genuinely pending campus (unchanged from before)', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`,
    );
    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-request', String(REQUEST_ID));
  });

  // T-08 (plan §13/§21): a malformed/unrecognized intent parses to `null` (the same URL-parsing guard
  // that already exists — `VISIT_COMMAND_INTENTS.has(...)`), and `null` must NEVER open the approve
  // modal, exactly like a true legacy notification with no intent at all.
  it('an unrecognized notificationIntent value is treated as unclassified and never opens the approve modal', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=NOT_A_REAL_INTENT`,
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── DL-07/DL-08 (plan-continuation §10/§17): HOST_PROCESS intent never overrides CURRENT host state ──

describe('HOST_PROCESS intent (HOST_ASSIGNED / HOST_TRANSFER_INCOMING) defers entirely to current state', () => {
  it('DL-07: still the current Host -> opens Host Process, same as the entry-context fallback would', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=HOST_PROCESS`,
      baseRow({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        currentHostUserId: 77,
        hostName: 'Staff Leader',
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

  it('DL-08: no longer the current Host -> falls back to current detail, never Host Process', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=HOST_PROCESS`,
      baseRow({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        currentHostUserId: 999,
        hostName: 'Nguoi Khac',
        currentUserIsHost: false,
        primaryEntryContext: 'REQUEST_DETAIL',
        primaryEntryVisitInstanceId: null,
        allowedActions: ['VIEW_DETAIL'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── RC-08/R-07: VISIT_READONLY_DETAIL must never escalate to an operational/reviewer screen ────────

describe('VISIT_READONLY_DETAIL intent caps at request detail even when current state allows more', () => {
  it('R-07: currently the Host of this same instance -> still lands on request detail, never Host Process', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_READONLY_DETAIL`,
      baseRow({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        currentHostUserId: 77,
        hostName: 'Staff Leader',
        currentUserIsHost: true,
        primaryEntryContext: 'HOST_PROCESS',
        allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS', 'APPROVE_AND_ASSIGN_HOST'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('still allowed when current state genuinely is only request detail (no downgrade needed)', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_READONLY_DETAIL`,
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
  });
});

// ── STABILIZATION §10.6/10.7/§30: VISIT_INVITATION/CONTRIBUTION always use the row's OWN exact
//    participantId, never whichever relation primaryEntryContext ranks highest on a multi-relation
//    row (the exact Staff-Leader-is-also-reviewer-and-participant collision the plan calls out) ────

describe('VISIT_INVITATION/CONTRIBUTION intent always opens the exact participant screen, never a co-existing relation\'s screen', () => {
  it('VISIT_INVITATION: participantId wins even though CAMPUS_REVIEW ranked highest on the merged row', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_INVITATION`,
      baseRow({
        participantId: 8899,
        // Same as the default fixture: CAMPUS_REVIEW is the CURRENT primary entry (this Staff Leader
        // is also the campus reviewer here) — the invitation must not be lost behind it.
        primaryEntryContext: 'CAMPUS_REVIEW',
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/8899'),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('CONTRIBUTION: participantId wins even though HOST_PROCESS ranked highest on the merged row', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=CONTRIBUTION`,
      baseRow({
        participantId: 8899,
        currentHostUserId: 77,
        currentUserIsHost: true,
        primaryEntryContext: 'HOST_PROCESS',
        allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/8899'),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
  });

  it('CONTRIBUTION: falls back to the contribution screen when there is no participantId but OPEN_CONTRIBUTION is granted', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=CONTRIBUTION`,
      baseRow({
        participantId: null,
        primaryEntryContext: 'REQUEST_DETAIL',
        primaryEntryVisitInstanceId: null,
        allowedActions: ['VIEW_DETAIL', 'OPEN_CONTRIBUTION'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/contribution/${INSTANCE_ID}`),
      expect.anything(),
    ));
  });

  it('VISIT_INVITATION: downgrades to safe request detail when the relation no longer exists (declined/removed) — never the review/host screen', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_INVITATION`,
      baseRow({
        participantId: null,
        primaryEntryContext: 'CAMPUS_REVIEW',
        allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── STABILIZATION round 2 §4/§5: legacy/unclassified intent NEVER escalates, even when current
//    state would allow it — pinned as L-01/L-02/L-03 exactly as specified ──────────────────────────

describe('legacy policy: unknown intent is SAFE DETAIL ONLY, regardless of current relation (L-01/L-02/L-03)', () => {
  it('L-01: no notificationIntent + current user genuinely IS the current Host -> detail, never Host Process', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseRow({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        currentHostUserId: 77,
        hostName: 'Staff Leader',
        currentUserIsHost: true,
        primaryEntryContext: 'HOST_PROCESS',
        allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('L-02: no notificationIntent + campus still pending + APPROVE_AND_ASSIGN_HOST allowed -> detail, never the approve modal', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`);

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('L-03: no notificationIntent + current user has a live participant relation -> detail, never invitation/contribution', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseRow({
        participantId: 8899,
        primaryEntryContext: 'CONTRIBUTION',
        allowedActions: ['VIEW_DETAIL', 'OPEN_CONTRIBUTION'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/'), expect.anything(),
    );
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/contribution/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('an unrecognized notificationIntent value is treated identically to no intent at all -> detail, never Host Process', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=NOT_A_REAL_INTENT`,
      baseRow({
        currentHostUserId: 77,
        currentUserIsHost: true,
        primaryEntryContext: 'HOST_PROCESS',
        allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
  });

  it('legacy null intent still reports failure (never a silent generic-relation fallback) when the caller has no request-detail read scope at all', async () => {
    listMock.mockResolvedValue({
      items: [baseRow({
        canViewRequestDetail: false,
        participantId: 8899,
        primaryEntryContext: 'CONTRIBUTION',
        allowedActions: ['OPEN_CONTRIBUTION'],
      })],
      totalItems: 1,
    });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`]}>
        <UrlProbe />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    await waitFor(() => expect(showErrorToastMock).toHaveBeenCalledWith(
      null, expect.stringContaining('Không thể mở'),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/contribution/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('legacy null intent in the ambiguous multi-campus case is also safe-detail-only, never a per-campus screen', async () => {
    const otherInstance = baseRow({
      visitInstanceId: 9002,
      campusId: 2,
      campusName: 'FPT University Đà Nẵng',
      primaryEntryVisitInstanceId: 9002,
      currentHostUserId: 77,
      currentUserIsHost: true,
      primaryEntryContext: 'HOST_PROCESS',
    });
    listMock.mockResolvedValue({ items: [otherInstance, baseRow()], totalItems: 2 });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}`]}>
        <UrlProbe />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── STABILIZATION round 2 §15: canonical multi-relation collision matrix (A-F). ONE Staff Leader who
//    is simultaneously REGISTRANT + CAMPUS_REVIEWER + HOST + PARTICIPANT on the SAME request — same
//    row, different event -> different destination. primaryEntryContext (whichever relation the
//    backend currently ranks highest) must never hijack the notification's own semantic. ──────────

describe('canonical multi-relation collision matrix: same request, same user, different event -> different destination', () => {
  const multiRelation = (over: Record<string, unknown> = {}) => baseRow({
    relations: ['REGISTRANT', 'CAMPUS_REVIEWER', 'HOST', 'PARTICIPANT'],
    registrantUserId: 77,
    currentHostUserId: 77,
    currentUserIsHost: true,
    participantId: 8899,
    ...over,
  });

  it('A. VISIT_PRIVACY_CONSENT_WITHDRAWN -> READONLY DETAIL, never invitation/host-process/approval', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_READONLY_DETAIL`,
      multiRelation({ primaryEntryContext: 'CAMPUS_REVIEW', allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST'] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`), expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(expect.stringContaining('/invitations/'), expect.anything());
    expect(navigateMock).not.toHaveBeenCalledWith(expect.stringContaining('/process/'), expect.anything());
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('B. VISIT_REQUEST_UPDATED_PENDING -> HISTORY, never the approve modal', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_HISTORY`,
      multiRelation({ primaryEntryContext: 'CAMPUS_REVIEW', allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST'] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}#history`), expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('C. VISIT_REQUEST_WAITING_APPROVAL -> REVIEW (this IS the reviewer and it is genuinely pending)', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`,
      multiRelation({ primaryEntryContext: 'CAMPUS_REVIEW', allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST'] }),
    );
    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));
  });

  it('D. HOST_ASSIGNED -> HOST PROCESS (still current Host of this instance)', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=HOST_PROCESS`,
      multiRelation({ primaryEntryContext: 'HOST_PROCESS', allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/process/${INSTANCE_ID}`), expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('E. PARTICIPATION_INVITED -> exact INVITATION, even though CAMPUS_REVIEW ranks highest for this same row', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_INVITATION`,
      multiRelation({ primaryEntryContext: 'CAMPUS_REVIEW', allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST'] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/8899'), expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('F. VISIT_REMINDER classified as CONTRIBUTION for this recipient -> participant/contribution, never Host Process even though this same user IS also Host of a DIFFERENT relation on this row', async () => {
    // Models a reminder recipient who is a participant on the instance the reminder is ABOUT, distinct
    // from their Host relation elsewhere on the same request (plan RM-03: STAFF participant but not
    // Host of THIS instance) — participantId still resolves exactly regardless of the Host relation.
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=CONTRIBUTION`,
      multiRelation({ primaryEntryContext: 'HOST_PROCESS', allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/8899'), expect.anything(),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/process/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── STABILIZATION round 2 §16 MC-03: same Staff Leader, reviewer at HN + participant relation
//    elsewhere -> an HN-specific event must resolve to HN, never a different campus/relation ────────

describe('MC-03: multi-campus exact resolution is unaffected by an unrelated relation on another campus', () => {
  it('a review notification naming the HN instance opens HN, never the other campus row returned in the same list', async () => {
    const hn = baseRow({
      visitInstanceId: INSTANCE_ID, campusId: 1, campusName: 'FPT University Hà Nội',
      primaryEntryVisitInstanceId: INSTANCE_ID, primaryEntryContext: 'CAMPUS_REVIEW',
    });
    const dn = baseRow({
      visitInstanceId: 9003, campusId: 3, campusName: 'FPT University Đà Nẵng',
      primaryEntryVisitInstanceId: 9003, primaryEntryContext: 'CONTRIBUTION', participantId: 8899,
      relationContexts: [{
        relation: 'PARTICIPANT', scope: 'INSTANCE', visitInstanceId: 9003, campusId: 3,
        campusName: 'FPT University Đà Nẵng', entryContext: 'CONTRIBUTION', requiresAction: false, priority: 3,
      }],
    });
    listMock.mockResolvedValue({ items: [hn, dn], totalItems: 2 });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`]}>
        <UrlProbe />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));
    expect(navigateMock).not.toHaveBeenCalledWith(expect.stringContaining('/invitations/'), expect.anything());
  });
});

// ── RC-10/MC-02: ambiguous multi-campus (no exact instance named) never guesses a campus ───────────

describe('a campus-specific notification with no exact instance id never guesses which campus', () => {
  it('never opens the approve modal for a random campus when the request has more than one', async () => {
    const otherInstance = baseRow({
      visitInstanceId: 9002,
      campusId: 2,
      campusName: 'FPT University Đà Nẵng',
      primaryEntryVisitInstanceId: 9002,
    });
    listMock.mockResolvedValue({ items: [otherInstance, baseRow()], totalItems: 2 });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&notificationIntent=VISIT_REVIEW`]}>
        <UrlProbe />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('still resolves unambiguously when only one campus/instance is returned', async () => {
    listMock.mockResolvedValue({ items: [baseRow()], totalItems: 1 });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&notificationIntent=VISIT_REVIEW`]}>
        <UrlProbe />
        <VisitRequestManagement />
      </MemoryRouter>,
    );

    await screen.findByTestId('assign-host-modal');
  });
});

// ── Admin never attempts to resolve a notification command (plan-continuation §19 role coverage) ────

describe('ADMIN role', () => {
  it('no-ops on a notification deep link instead of resolving/opening anything', async () => {
    currentUser = { userId: '1', roleCode: 'ADMIN', subRole: null };
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`);

    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(listMock).not.toHaveBeenCalledWith(expect.objectContaining({ visitRequestId: REQUEST_ID }));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
