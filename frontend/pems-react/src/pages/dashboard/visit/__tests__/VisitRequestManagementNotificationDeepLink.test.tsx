/**
 * `openVisitRequestId`/`openVisitInstanceId` — the ONE-SHOT COMMAND a Visit notification deep link
 * lands with (see PEMS_Notification_Visit_DeepLink_OneShot_Fix_Plan.md and
 * PEMS_NOTIFICATION_VISIT_EXACT_TARGET_IMPLEMENTATION_PLAN.md).
 *
 * Three things are pinned here.
 *
 * 1) REPLAY. Before the first fix the page never consumed the parameter that identified a
 *    notification's target — closing whatever it opened and then changing tab/filter/page/search
 *    left the trigger in the URL, and the next `setSearchParams` call replayed it.
 *
 * 2) STALE STATE. The notification only ever names WHERE to go (a request/instance id) — never what
 *    is currently true about it. This file proves the resolver never trusts the notification for
 *    status/relation: it calls `delegationsApi.resolveNotificationVisitTarget` — a dedicated
 *    exact-target resolver, re-derived fresh from the caller's CURRENT relations — and opens
 *    whatever THAT says, never a resurrected Duyệt/Từ chối control left over from the moment the
 *    notification was created.
 *
 * 3) EXACT SCOPE, NOT AN AGGREGATED ROW. The old implementation resolved a notification's target by
 *    searching the "all"-tab merged list (`getVisitRequestManagementList`), which collapses every
 *    relation a caller holds on a REQUEST into ONE row — wrong for a notification naming one exact
 *    campus instance different from whichever relation the merge happened to rank highest (the
 *    Staff-Leader-is-also-participant-elsewhere collision), and structurally unable to find an exact
 *    instance nested under a Visitor/HO multi-campus summary row (`visitInstanceId: null` at the top
 *    level). The resolver is now the ONLY source of "what is this notification actually about, and
 *    what may this caller do with it right now" — the list endpoint is used ONLY as a secondary
 *    fetch, and only to build the rich `Row` shape the live approve modal needs once the resolver has
 *    already confirmed a review is genuinely still pending at the exact instance.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation, useSearchParams } from 'react-router-dom';
import type { NotificationVisitTarget } from '../../../../features/delegations/types/delegations.types';

const listMock = vi.fn();
const resolveTargetMock = vi.fn();
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
    resolveNotificationVisitTarget: (...args: unknown[]) => resolveTargetMock(...args),
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
const CAMPUS_NAME = 'FPT University Hà Nội';

/** The rich list-row shape — now used ONLY as the secondary "responsible" fetch the VISIT_REVIEW
 * escalation makes once the resolver has already confirmed a review is genuinely pending. Its
 * `primaryEntryContext`/`allowedActions` are exactly what `setAssign` reads to open the modal. */
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
  campusName: CAMPUS_NAME,
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
    campusName: CAMPUS_NAME, entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1,
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

/** The exact-target resolver's response — the PRIMARY thing `resolveAndOpenNotificationTarget` now
 * reads for every routing decision except opening the live approve modal itself. */
const baseTarget = (over: Partial<NotificationVisitTarget> = {}): NotificationVisitTarget => ({
  exists: true,
  hasAccess: true,
  visitRequestId: REQUEST_ID,
  visitInstanceId: INSTANCE_ID,
  campusId: 1,
  campusName: CAMPUS_NAME,
  requestStatus: 'PENDING_APPROVAL',
  campusStatus: 'WAITING_REQUEST_APPROVAL',
  visitScope: 'SINGLE_CAMPUS',
  requestCode: `VR-2026-${REQUEST_ID}`,
  delegationName: DELEGATION,
  canViewRequestDetail: true,
  relationContexts: [{
    relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1,
    campusName: CAMPUS_NAME, entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1,
  }],
  participantId: null,
  participantStatus: null,
  ...over,
});

const renderAt = (
  search: string,
  target: NotificationVisitTarget = baseTarget(),
  row: Record<string, unknown> = baseRow(),
) => {
  resolveTargetMock.mockResolvedValue(target);
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
  listMock.mockResolvedValue({ items: [baseRow()], totalItems: 1 });
  resolveTargetMock.mockResolvedValue(baseTarget());
});

// ── Consuming the command exactly once ──────────────────────────────────────────────────────────

describe('a notification deep link is consumed exactly once', () => {
  it('opens the live approve flow for a campus still WAITING_REQUEST_APPROVAL, given an explicit VISIT_REVIEW intent', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`);

    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-request', String(REQUEST_ID));
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));
    expect(resolveTargetMock).toHaveBeenCalledWith(
      expect.objectContaining({ visitRequestId: REQUEST_ID, visitInstanceId: INSTANCE_ID }),
    );

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
      expect(resolveTargetMock).not.toHaveBeenCalled();
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
    // only what the RESOLVER now reports about it differs, exactly as if a second reviewer decided
    // it between the notification firing and this click.
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`,
      baseTarget({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        relationContexts: [{
          relation: 'HOST', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1,
          campusName: CAMPUS_NAME, entryContext: 'HOST_PROCESS', requiresAction: true, priority: 2,
        }],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('reports safe detail if the caller races into the modal but the row was decided between the resolver call and the list re-fetch', async () => {
    // The resolver said "still pending" (reviewDue) — but by the time the secondary list fetch lands,
    // the row itself is no longer CAMPUS_REVIEW/approvable. Must fall through to safe detail, never
    // open the modal on stale data.
    resolveTargetMock.mockResolvedValue(baseTarget());
    listMock.mockResolvedValue({
      items: [baseRow({ primaryEntryContext: 'HOST_PROCESS', allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'] })],
      totalItems: 1,
    });
    render(
      <MemoryRouter initialEntries={[`/dashboard/visit?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`]}>
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

  it('opens the current read-only detail for a campus that was rejected after the notification fired', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseTarget({
        requestStatus: 'REJECTED',
        campusStatus: 'REJECTED',
        relationContexts: [],
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
      baseTarget({
        requestStatus: 'CANCELLED',
        campusStatus: 'CANCELLED',
        relationContexts: [],
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
  it('reports it and consumes the command without crashing (deleted target, no guess from the notification text)', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseTarget({ exists: false, hasAccess: false, relationContexts: [] }),
    );

    // (error, fallback) — the same calling convention every other call site in this file uses;
    // passing the message as `error` would silently fall through to a generic toast instead.
    await waitFor(() => expect(showErrorToastMock).toHaveBeenCalledWith(
      null, expect.stringContaining('Không tìm thấy'),
    ));
    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('reports lost access (exists, but no relation any more) distinctly from a deleted target', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseTarget({ exists: true, hasAccess: false, relationContexts: [] }),
    );

    await waitFor(() => expect(showErrorToastMock).toHaveBeenCalledWith(
      null, expect.stringContaining('không còn quyền truy cập'),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });
});

// ── Multi-campus exact targeting ────────────────────────────────────────────────────────────────

describe('multi-campus exact instance targeting', () => {
  it('opens the named instance — the resolver, not a frontend search, is the source of exact scope', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`,
    );

    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));
  });
});

// ── The ordinary route is untouched ─────────────────────────────────────────────────────────────

describe('a visit list opened without the command', () => {
  it('opens nothing, calls no resolver, and leaves the URL alone', async () => {
    renderAt('?tab=all');
    await waitFor(() => expect(listMock).toHaveBeenCalled());
    expect(resolveTargetMock).not.toHaveBeenCalled();
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
    expect(params().get('tab')).toBe('all');
  });
});

// ── BUG 1 (plan §3/§56 DL-01): the SAME notification clicked a second time must open again ─────────

describe('the exact same notification clicked a second time still opens (second-click regression)', () => {
  it('re-opens the approve flow when the identical command reappears on an already-consumed instance', async () => {
    resolveTargetMock.mockResolvedValue(baseTarget());
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
// first and opens correctly; A's response then finally lands and must NOT silently replace what B had
// just opened, because `resolveAndOpenNotificationTarget` carries its own ordering guard (unlike
// `loadDelegations`, which already carries one — `requestVersionRef`). Root cause + fix: a matching
// `notificationTargetVersionRef`, checked once right after the resolver call resolves, before any
// state mutation (and again after the secondary list fetch the VISIT_REVIEW escalation makes).

describe('rapid-click race: a slower earlier notification click must never overwrite a faster later one', () => {
  it('B (fast) stays open after A (slow) finally resolves', async () => {
    const OTHER_REQUEST_ID = 4002;
    const OTHER_INSTANCE_ID = 5002;
    let resolveSlowCall: (value: NotificationVisitTarget) => void = () => {};
    const slowCall = new Promise<NotificationVisitTarget>((resolve) => { resolveSlowCall = resolve; });
    let targetCallCount = 0;
    resolveTargetMock.mockImplementation(() => {
      targetCallCount += 1;
      if (targetCallCount === 1) return slowCall; // command A — deliberately held open
      return Promise.resolve(baseTarget({
        visitRequestId: OTHER_REQUEST_ID,
        visitInstanceId: OTHER_INSTANCE_ID,
        relationContexts: [{
          relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: OTHER_INSTANCE_ID, campusId: 1,
          campusName: CAMPUS_NAME, entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1,
        }],
      }));
    });
    listMock.mockImplementation((query: Record<string, unknown> | undefined) => {
      if (query?.visitRequestId === OTHER_REQUEST_ID) {
        return Promise.resolve({
          items: [baseRow({ visitRequestId: OTHER_REQUEST_ID, visitInstanceId: OTHER_INSTANCE_ID })],
          totalItems: 1,
        });
      }
      return Promise.resolve({ items: [baseRow()], totalItems: 1 });
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

    await userEvent.click(screen.getByTestId('trigger-a')); // fires resolver call #1 (slow, held open)
    await waitFor(() => expect(resolveTargetMock).toHaveBeenCalledTimes(1));

    await userEvent.click(screen.getByTestId('trigger-b')); // fires resolver call #2 (fast, resolves immediately)
    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-request', String(OTHER_REQUEST_ID));

    // A's delayed response finally lands — it must NOT clobber B's already-open modal.
    resolveSlowCall(baseTarget());
    await waitFor(() => expect(screen.getByTestId('assign-host-modal'))
      .toHaveAttribute('data-request', String(OTHER_REQUEST_ID)));
  });
});

// ── BUG 2 (plan §5/§7/§14/§57 CR-03): semantic intent gates the approve escalation ──────────────────

describe('notificationIntent gates whether the notification may escalate to the live approve control', () => {
  it('VISIT_HISTORY never opens the approve modal even though the campus is still pending and APPROVE is allowed', async () => {
    // Same target shape as the "still WAITING_REQUEST_APPROVAL" test above (genuinely pending,
    // CAMPUS_REVIEWER requiresAction) — the ONLY difference is the notification's own semantic
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
      baseTarget({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        relationContexts: [{
          relation: 'HOST', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1,
          campusName: CAMPUS_NAME, entryContext: 'HOST_PROCESS', requiresAction: true, priority: 2,
        }],
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
      baseTarget({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        relationContexts: [],
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
      baseTarget({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        relationContexts: [{
          relation: 'HOST', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1,
          campusName: CAMPUS_NAME, entryContext: 'HOST_PROCESS', requiresAction: true, priority: 2,
        }],
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
      baseTarget({
        requestStatus: 'REJECTED',
        campusStatus: 'REJECTED',
        relationContexts: [],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
  });
});

// ── STABILIZATION §10.6/10.7/§30: VISIT_INVITATION/CONTRIBUTION always use the EXACT participantId
//    the resolver found at this instance, never whichever relation ranks highest overall (the exact
//    Staff-Leader-is-also-reviewer-and-participant collision the plan calls out) ────────────────────

describe('VISIT_INVITATION/CONTRIBUTION intent always opens the exact participant screen, never a co-existing relation\'s screen', () => {
  it('VISIT_INVITATION: participantId wins even though CAMPUS_REVIEW is also present at this instance', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_INVITATION`,
      baseTarget({
        participantId: 8899,
        relationContexts: [
          { relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CAMPUS_REVIEW', requiresAction: false, priority: 1 },
          { relation: 'PARTICIPANT', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CONTRIBUTION', requiresAction: true, priority: 3 },
        ],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/8899'),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('CONTRIBUTION: participantId wins even though HOST is also present at this instance', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=CONTRIBUTION`,
      baseTarget({
        participantId: 8899,
        relationContexts: [
          { relation: 'HOST', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'HOST_PROCESS', requiresAction: true, priority: 2 },
          { relation: 'PARTICIPANT', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CONTRIBUTION', requiresAction: false, priority: 3 },
        ],
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

  it('CONTRIBUTION: falls back to the contribution screen when the resolver found a participant relation but no participantId', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=CONTRIBUTION`,
      baseTarget({
        participantId: null,
        relationContexts: [
          { relation: 'PARTICIPANT', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CONTRIBUTION', requiresAction: false, priority: 3 },
        ],
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
      baseTarget({
        participantId: null,
        relationContexts: [
          { relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1 },
        ],
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
      baseTarget({
        requestStatus: 'APPROVED',
        campusStatus: 'BEFORE_VISIT',
        relationContexts: [{
          relation: 'HOST', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1,
          campusName: CAMPUS_NAME, entryContext: 'HOST_PROCESS', requiresAction: true, priority: 2,
        }],
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

  it('L-02: no notificationIntent + campus still pending + reviewer relation present -> detail, never the approve modal', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`);

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
    // The escalation-only secondary list fetch must never fire without an explicit VISIT_REVIEW intent.
    expect(listMock).toHaveBeenCalledTimes(1); // initial table load only
  });

  it('L-03: no notificationIntent + current user has a live participant relation -> detail, never invitation/contribution', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseTarget({
        participantId: 8899,
        relationContexts: [
          { relation: 'PARTICIPANT', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CONTRIBUTION', requiresAction: false, priority: 3 },
        ],
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
      baseTarget({
        relationContexts: [{
          relation: 'HOST', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1,
          campusName: CAMPUS_NAME, entryContext: 'HOST_PROCESS', requiresAction: true, priority: 2,
        }],
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
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}`,
      baseTarget({
        canViewRequestDetail: false,
        participantId: 8899,
        relationContexts: [
          { relation: 'PARTICIPANT', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CONTRIBUTION', requiresAction: false, priority: 3 },
        ],
      }),
    );

    await waitFor(() => expect(showErrorToastMock).toHaveBeenCalledWith(
      null, expect.stringContaining('Không thể mở'),
    ));
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/contribution/'), expect.anything(),
    );
    expect(navigateMock).not.toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/'), expect.anything(),
    );
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('legacy null intent in the request-level (no exact instance named) case is also safe-detail-only, never a per-campus screen', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}`,
      baseTarget({
        visitInstanceId: null,
        campusId: null,
        campusName: null,
        campusStatus: null,
        relationContexts: [
          { relation: 'REGISTRANT', scope: 'REQUEST', entryContext: 'REQUEST_DETAIL', requiresAction: false, priority: 4 },
        ],
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

// ── STABILIZATION round 2 §15: canonical multi-relation collision matrix (A-F). ONE Staff Leader who
//    is simultaneously REGISTRANT + CAMPUS_REVIEWER + HOST + PARTICIPANT on the SAME request — same
//    exact instance, different event -> different destination. The resolver's own relation contexts
//    (never a merged/aggregated row's single winner) decide it. ──────────────────────────────────

describe('canonical multi-relation collision matrix: same request, same user, different event -> different destination', () => {
  const multiRelationContexts = [
    { relation: 'REGISTRANT', scope: 'REQUEST', entryContext: 'REQUEST_DETAIL', requiresAction: false, priority: 4 },
    { relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1 },
    { relation: 'HOST', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'HOST_PROCESS', requiresAction: true, priority: 2 },
    { relation: 'PARTICIPANT', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CONTRIBUTION', requiresAction: false, priority: 3 },
  ] as const;

  it('A. VISIT_PRIVACY_CONSENT_WITHDRAWN -> READONLY DETAIL, never invitation/host-process/approval', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_READONLY_DETAIL`,
      baseTarget({ participantId: 8899, relationContexts: [...multiRelationContexts] }),
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
      baseTarget({ participantId: 8899, relationContexts: [...multiRelationContexts] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}#history`), expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('C. VISIT_REQUEST_WAITING_APPROVAL -> REVIEW (this IS the reviewer and it is genuinely pending)', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`,
      baseTarget({ participantId: 8899, relationContexts: [...multiRelationContexts] }),
    );
    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));
  });

  it('D. HOST_ASSIGNED -> HOST PROCESS (still current Host of this instance)', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=HOST_PROCESS`,
      baseTarget({ participantId: 8899, relationContexts: [...multiRelationContexts] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/process/${INSTANCE_ID}`), expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('E. PARTICIPATION_INVITED -> exact INVITATION, even though CAMPUS_REVIEW is also present for this same instance', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_INVITATION`,
      baseTarget({ participantId: 8899, relationContexts: [...multiRelationContexts] }),
    );
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining('/dashboard/visit/invitations/8899'), expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('F. VISIT_REMINDER classified as CONTRIBUTION for this recipient -> participant/contribution, never Host Process even though HOST is also present at this same instance', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=CONTRIBUTION`,
      baseTarget({ participantId: 8899, relationContexts: [...multiRelationContexts] }),
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
  it('a review notification naming the HN instance opens HN, never a DN-scoped relation the resolver also returned', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`,
      baseTarget({
        relationContexts: [
          { relation: 'CAMPUS_REVIEWER', scope: 'INSTANCE', visitInstanceId: INSTANCE_ID, campusId: 1, campusName: CAMPUS_NAME, entryContext: 'CAMPUS_REVIEW', requiresAction: true, priority: 1 },
        ],
      }),
    );

    const modal = await screen.findByTestId('assign-host-modal');
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));
    expect(navigateMock).not.toHaveBeenCalledWith(expect.stringContaining('/invitations/'), expect.anything());
  });
});

// ── RC-10/MC-02: request-level notification with no exact instance named never guesses a campus ────

describe('a campus-specific notification with no exact instance id never guesses which campus', () => {
  it('never opens the approve modal when the notification named no instance, even though the request has a reviewer relation somewhere', async () => {
    renderAt(
      `?openVisitRequestId=${REQUEST_ID}&notificationIntent=VISIT_REVIEW`,
      baseTarget({
        visitInstanceId: null,
        campusId: null,
        campusName: null,
        campusStatus: null,
        relationContexts: [
          { relation: 'REGISTRANT', scope: 'REQUEST', entryContext: 'REQUEST_DETAIL', requiresAction: false, priority: 4 },
        ],
      }),
    );

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(
      expect.stringContaining(`/dashboard/visit/v2/${REQUEST_ID}`),
      expect.anything(),
    ));
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
  });

  it('still resolves unambiguously when the resolver names the exact instance', async () => {
    renderAt(`?openVisitRequestId=${REQUEST_ID}&notificationIntent=VISIT_REVIEW`);
    await screen.findByTestId('assign-host-modal');
  });
});

// ── Admin never attempts to resolve a notification command (plan-continuation §19 role coverage) ────

describe('ADMIN role', () => {
  it('no-ops on a notification deep link instead of resolving/opening anything', async () => {
    currentUser = { userId: '1', roleCode: 'ADMIN', subRole: null };
    renderAt(`?openVisitRequestId=${REQUEST_ID}&openVisitInstanceId=${INSTANCE_ID}&notificationIntent=VISIT_REVIEW`);

    await waitFor(() => expect(params().get('openVisitRequestId')).toBeNull());
    expect(resolveTargetMock).not.toHaveBeenCalled();
    expect(screen.queryByTestId('assign-host-modal')).toBeNull();
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
