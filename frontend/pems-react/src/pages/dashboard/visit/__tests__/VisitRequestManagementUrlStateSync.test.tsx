/**
 * Stale notification-target / URL-state sync bug (see
 * docs/CanhIter3FixBug/GopYCQuyen/PEMS_Notification_Stale_Target_URL_State_Fix_Prompt.md).
 *
 * Reproduced exactly as reported: click notification Nanning (`?visitRequestId=47028`) — the row
 * shows. Without leaving `/dashboard/visit`, click notification Shinyway (`?visitRequestId=47027`)
 * — the URL changes, but the table kept showing Nanning. Root cause: `notificationVisitRequestId`
 * (and `activeTab`) were `useState(searchParams.get(...))` — read from the URL only at MOUNT. React
 * Router does not remount this component for a same-route navigation, so the state never saw the
 * new value; `loadDelegations` kept using the stale target.
 *
 * This file drives REAL react-router navigation (no `useNavigate` mock) so a same-route URL change
 * behaves exactly as it does in production — the component instance is never unmounted between
 * "clicks".
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useNavigate } from 'react-router-dom';

const listMock = vi.fn();
const invitationsMock = vi.fn();
const hostCandidatesMock = vi.fn();

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
  showErrorToast: vi.fn(),
  showSuccessToast: vi.fn(),
  getApiErrorMessage: (_err: unknown, fallback: string) => fallback,
}));

let currentUser: { userId: string; roleCode: string; subRole?: string | null } =
  { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: currentUser }),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────

const NANNING_ID = 47028;
const SHINYWAY_ID = 47027;

const rowFor = (visitRequestId: number, delegationName: string, over: Record<string, unknown> = {}) => ({
  visitRequestId,
  visitInstanceId: visitRequestId + 100000,
  requestCode: `VR-2026-${visitRequestId}`,
  delegationName,
  partnerName: `${delegationName} Co.`,
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
  rowVersion: 1,
  visitorUserId: 10,
  visitorName: 'Visitor',
  isCurrentUserParticipant: false,
  participantId: null,
  participantRole: null,
  currentUserRelation: 'CAMPUS_REVIEWER',
  relationLabel: 'Cần bạn duyệt',
  statusLabel: 'Chờ duyệt',
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
  allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST'],
  capabilities: [],
  ...over,
});

const NANNING_ROW = rowFor(NANNING_ID, 'Nanning');
const SHINYWAY_ROW = rowFor(SHINYWAY_ID, 'Shinyway');

/** The real backend behavior this fix depends on (plan §6): `visitRequestId` narrows server-side —
 * only the matching row(s) ever come back, never all 1000 for the page to filter down itself. */
const backendVisitRequestIdMock = (rowsByRequestId: Record<number, ReturnType<typeof rowFor>>) =>
  vi.fn(async (params: Record<string, unknown>) => {
    const reqId = params.visitRequestId != null ? Number(params.visitRequestId) : null;
    if (reqId != null) {
      const row = rowsByRequestId[reqId];
      return { items: row ? [row] : [], totalItems: row ? 1 : 0 };
    }
    const all = Object.values(rowsByRequestId);
    return { items: all, totalItems: all.length };
  });

const desktop = () => within(screen.getByTestId('visit-list-desktop'));

/** Drives REAL same-route navigation — exactly what a second Bell/NotificationsPage click does
 * (`navigate(link)` on an already-mounted page, no remount). */
const NavigateTo = ({ to }: { to: string }) => {
  const navigate = useNavigate();
  return <button type="button" data-testid={`goto-${to}`} onClick={() => navigate(to)}>goto</button>;
};

const renderAt = (initialPath: string, rowsByRequestId: Record<number, ReturnType<typeof rowFor>>) => {
  listMock.mockImplementation(backendVisitRequestIdMock(rowsByRequestId));
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <NavigateTo to={`/dashboard/visit?visitRequestId=${SHINYWAY_ID}`} />
      <NavigateTo to={`/dashboard/visit?visitRequestId=${NANNING_ID}`} />
      <NavigateTo to="/dashboard/visit" />
      <NavigateTo to={`/dashboard/visit?visitRequestId=999999`} />
      <VisitRequestManagement />
    </MemoryRouter>,
  );
};

const clickGoto = async (to: string) => {
  await userEvent.click(screen.getByTestId(`goto-${to}`));
};

beforeEach(() => {
  vi.clearAllMocks();
  currentUser = { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
  invitationsMock.mockResolvedValue({ items: [], totalItems: 0 });
  hostCandidatesMock.mockResolvedValue([]);
});

// ── N-01 / N-02: same-route notification-to-notification navigation ────────────────────────────────

describe('N-01/N-02: clicking a second notification target on the same route updates the table', () => {
  it('N-01: Nanning -> Shinyway — Shinyway becomes visible, Nanning does not', async () => {
    renderAt(`/dashboard/visit?visitRequestId=${NANNING_ID}`, { [NANNING_ID]: NANNING_ROW, [SHINYWAY_ID]: SHINYWAY_ROW });

    await waitFor(() => expect(desktop().getByText('Nanning')).toBeInTheDocument());
    expect(desktop().queryByText('Shinyway')).toBeNull();

    await clickGoto(`/dashboard/visit?visitRequestId=${SHINYWAY_ID}`);

    await waitFor(() => expect(desktop().getByText('Shinyway')).toBeInTheDocument());
    expect(desktop().queryByText('Nanning')).toBeNull();
  });

  it('N-02: Shinyway -> Nanning (reverse order) — the fix must not be order-dependent', async () => {
    renderAt(`/dashboard/visit?visitRequestId=${SHINYWAY_ID}`, { [NANNING_ID]: NANNING_ROW, [SHINYWAY_ID]: SHINYWAY_ROW });

    await waitFor(() => expect(desktop().getByText('Shinyway')).toBeInTheDocument());

    await clickGoto(`/dashboard/visit?visitRequestId=${NANNING_ID}`);

    await waitFor(() => expect(desktop().getByText('Nanning')).toBeInTheDocument());
    expect(desktop().queryByText('Shinyway')).toBeNull();
  });

  it('the backend receives visitRequestId directly — no client-side filter-after-fetch-1000', async () => {
    renderAt(`/dashboard/visit?visitRequestId=${NANNING_ID}`, { [NANNING_ID]: NANNING_ROW, [SHINYWAY_ID]: SHINYWAY_ROW });
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    const call = listMock.mock.calls.find(([p]) => Number(p.visitRequestId) === NANNING_ID);
    expect(call, 'expected a call with visitRequestId sent to the backend').toBeTruthy();
    const [params] = call!;
    expect(params.pageSize).not.toBe(1000);
  });
});

// ── N-04: rapid different-notification race ─────────────────────────────────────────────────────

describe('N-04: rapid click A then B — the slower A response must never overwrite the faster B', () => {
  it('final UI is B even when A resolves after B', async () => {
    let resolveNanning: (v: unknown) => void;
    const nanningPromise = new Promise((resolve) => { resolveNanning = resolve; });

    listMock.mockImplementation(async (params: Record<string, unknown>) => {
      const reqId = Number(params.visitRequestId);
      if (reqId === NANNING_ID) {
        await nanningPromise;
        return { items: [NANNING_ROW], totalItems: 1 };
      }
      if (reqId === SHINYWAY_ID) {
        return { items: [SHINYWAY_ROW], totalItems: 1 };
      }
      return { items: [], totalItems: 0 };
    });

    render(
      <MemoryRouter initialEntries={['/dashboard/visit']}>
        <NavigateTo to={`/dashboard/visit?visitRequestId=${NANNING_ID}`} />
        <NavigateTo to={`/dashboard/visit?visitRequestId=${SHINYWAY_ID}`} />
        <VisitRequestManagement />
      </MemoryRouter>,
    );
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    // click A (slow, still pending) then B (fast, resolves immediately)
    await clickGoto(`/dashboard/visit?visitRequestId=${NANNING_ID}`);
    await clickGoto(`/dashboard/visit?visitRequestId=${SHINYWAY_ID}`);

    await waitFor(() => expect(desktop().getByText('Shinyway')).toBeInTheDocument());

    // Now let the stale, slower A response resolve — it must NOT clobber the table back to Nanning.
    resolveNanning!({ items: [NANNING_ROW], totalItems: 1 });
    await new Promise((r) => setTimeout(r, 0));

    expect(desktop().getByText('Shinyway')).toBeInTheDocument();
    expect(desktop().queryByText('Nanning')).toBeNull();
  });
});

// ── N-09: missing/no-permission target clears the previously-shown row ─────────────────────────────

describe('N-09: a target that no longer resolves (deleted / no permission) clears the old row', () => {
  it('clicking a second notification whose backend result is empty removes the first target\'s row and shows the specific message', async () => {
    renderAt(`/dashboard/visit?visitRequestId=${NANNING_ID}`, { [NANNING_ID]: NANNING_ROW });
    await waitFor(() => expect(desktop().getByText('Nanning')).toBeInTheDocument());

    await clickGoto(`/dashboard/visit?visitRequestId=999999`);

    await waitFor(() => expect(desktop().queryByText('Nanning')).toBeNull());
    await waitFor(() => expect(desktop().getByText(
      'Không tìm thấy đoàn được nhắc trong thông báo, hoặc bạn không còn quyền xem đoàn này.',
    )).toBeInTheDocument());
  });
});

// ── N-05/N-06: Back/Forward must keep URL and UI in lockstep ────────────────────────────────────────

describe('N-05/N-06: Back/Forward restores the matching target, not whatever was on screen', () => {
  // `MemoryRouter` keeps its OWN history stack — it never touches the real browser History API — so
  // `window.history.back()` is a no-op against it. `navigate(-1)`/`navigate(1)` is the router's own
  // Back/Forward, and it drives the exact same `searchParams` change a real browser Back button does.
  const HistoryNav = ({ delta }: { delta: number }) => {
    const navigate = useNavigate();
    return <button type="button" data-testid={`history-${delta}`} onClick={() => navigate(delta)}>nav</button>;
  };

  it('Nanning -> Shinyway -> Back returns to Nanning; Forward returns to Shinyway', async () => {
    listMock.mockImplementation(backendVisitRequestIdMock({ [NANNING_ID]: NANNING_ROW, [SHINYWAY_ID]: SHINYWAY_ROW }));
    render(
      <MemoryRouter initialEntries={[
        `/dashboard/visit?visitRequestId=${NANNING_ID}`,
        `/dashboard/visit?visitRequestId=${SHINYWAY_ID}`,
      ]} initialIndex={1}>
        <HistoryNav delta={-1} />
        <HistoryNav delta={1} />
        <VisitRequestManagement />
      </MemoryRouter>,
    );
    await waitFor(() => expect(desktop().getByText('Shinyway')).toBeInTheDocument());

    await userEvent.click(screen.getByTestId('history--1'));
    await waitFor(() => expect(desktop().getByText('Nanning')).toBeInTheDocument());
    expect(desktop().queryByText('Shinyway')).toBeNull();

    await userEvent.click(screen.getByTestId('history-1'));
    await waitFor(() => expect(desktop().getByText('Shinyway')).toBeInTheDocument());
    expect(desktop().queryByText('Nanning')).toBeNull();
  });
});

// ── N-07: an external notification's tab param must actually switch activeTab, not just the URL ─────

describe('N-07: PARTICIPATION_INVITED (?tab=attending) actually switches the active tab', () => {
  it('external navigation to tab=attending re-queries the invitations endpoint and shows that row', async () => {
    listMock.mockResolvedValue({ items: [], totalItems: 0 });
    const participantRow = {
      visitRequestId: SHINYWAY_ID,
      visitInstanceId: SHINYWAY_ID + 100000,
      delegationName: 'Shinyway',
      participantId: 321,
      invitationStatus: 'INVITED',
      plannedStartAt: '2026-08-25T09:00:00',
      campusName: 'FPT University Hà Nội',
    };
    invitationsMock.mockResolvedValue({ items: [participantRow], totalItems: 1 });

    render(
      <MemoryRouter initialEntries={['/dashboard/visit?tab=all']}>
        <NavigateTo to={`/dashboard/visit?visitRequestId=${SHINYWAY_ID}&tab=attending`} />
        <VisitRequestManagement />
      </MemoryRouter>,
    );
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await clickGoto(`/dashboard/visit?visitRequestId=${SHINYWAY_ID}&tab=attending`);

    await waitFor(() => expect(invitationsMock).toHaveBeenCalled());
    await waitFor(() => expect(desktop().getByText('Shinyway')).toBeInTheDocument());
  });
});

// ── N-12: an ordinary filter/search change after viewing a notification target does not replay it ──

describe('N-12: leaving notification-target mode via Reset does not resurrect the old target on a later load', () => {
  it('Reset clears the target; a subsequent normal load is the unfiltered list, not the old target', async () => {
    renderAt(`/dashboard/visit?visitRequestId=${NANNING_ID}`, { [NANNING_ID]: NANNING_ROW, [SHINYWAY_ID]: SHINYWAY_ROW });
    await waitFor(() => expect(desktop().getByText('Nanning')).toBeInTheDocument());

    await userEvent.click(screen.getByText('Xem tất cả'));

    await waitFor(() => {
      expect(desktop().getByText('Nanning')).toBeInTheDocument();
      expect(desktop().getByText('Shinyway')).toBeInTheDocument();
    });
    const lastCall = listMock.mock.calls.at(-1)![0];
    expect(lastCall.visitRequestId).toBeUndefined();
  });
});
