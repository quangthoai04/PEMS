/**
 * The management list's three information layers and its capability-driven actions (prompt §15).
 *
 * What these guard, in one sentence each:
 *   • the word "Host" is gone from the reader-facing list, while the WIRE still says TRANSFER_HOST;
 *   • status, relation and next task are three separate things on the row, not one overloaded badge;
 *   • the handover is offered exactly where the backend scoped it — the row for a single campus, the
 *     campus sub-row for a multi-campus request, and NEVER on a multi-campus summary row;
 *   • a refused capability appears disabled WITH its reason, and an ungranted one does not appear.
 *
 * The page is rendered whole, against a mocked list endpoint, so a regression in the wiring between
 * the DTO and the buttons is caught here rather than in a browser.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

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

let currentUser: { userId: string; roleCode: string; subRole?: string } =
  { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: currentUser }),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────

const transferAllowed = (visitInstanceId: number, campusName: string) => ({
  code: 'TRANSFER_HOST',
  scope: 'INSTANCE' as const,
  visitInstanceId,
  enabled: true,
  cutoffAt: '2026-08-15T03:00:00',
  plannedStartAt: '2026-08-15T09:00:00',
  campusName,
  requiredLeadHours: 6,
});

const transferRefused = (visitInstanceId: number, campusName: string) => ({
  code: 'TRANSFER_HOST',
  scope: 'INSTANCE' as const,
  visitInstanceId,
  enabled: false,
  disabledReasonCode: 'VISIT_MUTATION_CUTOFF_REACHED',
  disabledReason: 'Thao tác này chỉ được thực hiện ít nhất 6 giờ trước khi chuyến thăm bắt đầu.',
  cutoffAt: '2026-07-26T03:00:00',
  plannedStartAt: '2026-07-26T09:00:00',
  campusName,
  requiredLeadHours: 6,
});

/** One instance row — how a campus Staff Leader sees a campus they lead. */
const instanceRow = (over: Record<string, unknown> = {}) => ({
  visitRequestId: 4001,
  visitInstanceId: 5001,
  requestCode: 'VR-2026-4001',
  delegationName: 'Jeju Tourism Technology Delegation',
  partnerName: 'Jeju Tech Agency',
  requestStatus: 'APPROVED',
  campusStatus: 'BEFORE_VISIT',
  visitScope: 'SINGLE_CAMPUS',
  campusId: 1,
  campusName: 'FPT University Hà Nội',
  campusCount: 1,
  createdByUserId: 10,
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
  nextTask: {
    code: 'COMPLETE_PREPARATION',
    label: 'Hoàn thiện lịch trình và công tác chuẩn bị',
    requiresAction: true,
    scope: 'INSTANCE',
    visitInstanceId: 5001,
    actionCode: 'OPEN_HOST_PROCESS',
  },
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
  allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS', 'TRANSFER_HOST'],
  capabilities: [transferAllowed(5001, 'FPT University Hà Nội')],
  ...over,
});

/** A multi-campus SUMMARY row — one row for the whole request, with a per-campus accordion. */
const summaryRow = (over: Record<string, unknown> = {}) => ({
  ...instanceRow(),
  visitInstanceId: null,
  rowVersion: null,
  visitScope: 'MULTI_CAMPUS',
  campusCount: 2,
  campusName: '2 cơ sở',
  currentUserIsHost: false,
  currentUserRelation: 'VISITOR_OWNER',
  relationLabel: 'Bạn là đầu mối chính',
  statusLabel: 'Đã duyệt',
  campusStatus: null,
  nextTask: { code: 'NONE', label: 'Không có nhiệm vụ cần xử lý', requiresAction: false, scope: 'REQUEST' },
  canExpandCampuses: true,
  allowedActions: ['VIEW_DETAIL'],
  // The summary row carries NO instance-scoped verdict — the backend refuses to put one here.
  capabilities: [],
  campusProgressItems: [
    {
      visitInstanceId: 5001, campusId: 1, campusCode: 'HN', campusName: 'FPT University Hà Nội',
      plannedStartAt: '2026-08-15T09:00:00', plannedEndAt: '2026-08-15T12:00:00',
      instanceStatus: 'ASSIGNED', hostUserId: 77, hostName: 'IC Staff Hà Nội', rowVersion: 4,
      canViewCampusDetail: true, canCancelCampusVisit: false, canViewCancelReason: false,
      canViewRejectReason: false,
      capabilities: [transferAllowed(5001, 'FPT University Hà Nội')],
      canTransferHost: true,
    },
    {
      visitInstanceId: 5002, campusId: 4, campusCode: 'CT', campusName: 'FPT University Cần Thơ',
      plannedStartAt: '2026-08-20T09:00:00', plannedEndAt: '2026-08-20T12:00:00',
      instanceStatus: 'ASSIGNED', hostUserId: 91, hostName: 'IC Staff Cần Thơ', rowVersion: 2,
      canViewCampusDetail: true, canCancelCampusVisit: false, canViewCancelReason: false,
      canViewRejectReason: false,
      // Not this caller's campus → the backend sends no verdict at all.
      capabilities: [],
      canTransferHost: false,
    },
  ],
  ...over,
});

const renderList = (rows: unknown[]) => {
  listMock.mockResolvedValue({ items: rows, totalItems: rows.length });
  return render(<MemoryRouter><VisitRequestManagement /></MemoryRouter>);
};

// Both layouts are in the DOM at once — CSS chooses which one is visible — so every row-level
// assertion has to say which layout it means, or it finds two of everything.
const desktop = () => within(screen.getByTestId('visit-list-desktop'));
const mobile = () => within(screen.getByTestId('visit-list-mobile'));

const openRowMenu = async (testId: string) => {
  await userEvent.click(screen.getByTestId(testId));
  return screen.getByTestId(`${testId}-panel`);
};

beforeEach(() => {
  vi.clearAllMocks();
  currentUser = { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
  invitationsMock.mockResolvedValue([]);
  hostCandidatesMock.mockResolvedValue([]);
});

// ── §15.1 / §15.2 terminology ────────────────────────────────────────────────────────────────────

describe('terminology: the reader sees "người phụ trách tiếp đón", the wire still says TRANSFER_HOST', () => {
  it('labels the owner field without the word Host', async () => {
    renderList([instanceRow()]);
    expect(await waitFor(() => desktop().getByText(/Người phụ trách tiếp đón:/))).toBeInTheDocument();
    // The whole rendered list must not contain the bare technical word anywhere a reader can see it.
    expect(document.body.textContent).not.toMatch(/\bHost\b/);
  });

  it('says "Chưa được phân công" instead of "Chờ duyệt & gán host" before a decision', async () => {
    renderList([instanceRow({
      campusStatus: 'WAITING_REQUEST_APPROVAL', hostName: null, currentHostUserId: null,
      currentUserIsHost: false, statusLabel: 'Chờ xử lý tại cơ sở', capabilities: [],
      relationLabel: 'Bạn có quyền duyệt tại cơ sở', currentUserRelation: 'CAMPUS_APPROVER',
      allowedActions: ['VIEW_DETAIL'], nextTask: null,
    })]);
    await waitFor(() => desktop().getByText('Chờ xử lý tại cơ sở'));
    // The value is a bare text node in the owner line, so match it on its own — getByText compares an
    // element's DIRECT text, and the label beside it lives in a sibling span.
    expect(desktop().getByText(/Chưa được phân công/)).toBeInTheDocument();
    expect(desktop().queryByText(/gán host/i)).not.toBeInTheDocument();
  });

  it('keeps the technical action CODE unchanged so the backend contract still matches', async () => {
    const { VISIT_ALLOWED_ACTIONS } = await import('../../../../features/delegations/types/delegations.types');
    expect(VISIT_ALLOWED_ACTIONS.TRANSFER_HOST).toBe('TRANSFER_HOST');
    expect(VISIT_ALLOWED_ACTIONS.APPROVE_AND_ASSIGN_HOST).toBe('APPROVE_AND_ASSIGN_HOST');
  });
});

// ── §15.5 / §15.6 status and relation stay apart; the next-task line was removed from the row ────
// "Việc cần làm" no longer renders anywhere in the list table — nextTask now only decides which
// primary action button appears (see renderRowActions), so there is no next-task element to assert on.

describe('a row keeps status and relation apart, with no next-task line in the row', () => {
  it('renders status and relation as two separate things, and no next-task element', async () => {
    renderList([instanceRow()]);
    expect(await waitFor(() => desktop().getByText('Đang chuẩn bị'))).toBeInTheDocument();        // status
    expect(desktop().getByText('Bạn phụ trách tiếp đón')).toBeInTheDocument();      // relation
    expect(desktop().queryByTestId('next-task-5001')).not.toBeInTheDocument();
  });

  it('still drives the primary action button off the backend-granted capability on a WAITING row', async () => {
    renderList([instanceRow({
      campusStatus: 'WAITING_REQUEST_APPROVAL', currentUserIsHost: false, hostName: null,
      currentHostUserId: null, statusLabel: 'Chờ xử lý tại cơ sở',
      relationLabel: 'Bạn có quyền duyệt tại cơ sở', capabilities: [],
      allowedActions: ['VIEW_DETAIL', 'APPROVE_AND_ASSIGN_HOST', 'CAMPUS_REJECT'],
      nextTask: {
        code: 'REVIEW_AND_ASSIGN', label: 'Duyệt hoặc từ chối và phân công người phụ trách',
        requiresAction: true, scope: 'INSTANCE', visitInstanceId: 5001,
        actionCode: 'APPROVE_AND_ASSIGN_HOST',
      },
    })]);
    await waitFor(() => desktop().getByText('Chờ xử lý tại cơ sở'));
    expect(desktop().getByRole('button', { name: 'Duyệt & phân công người phụ trách' })).toBeInTheDocument();
    expect(desktop().queryByTestId('next-task-5001')).not.toBeInTheDocument();
  });
});

// ── §15.8 / §15.9 / §15.14 the ⋯ menu is capability-driven ───────────────────────────────────────

describe('the ⋯ menu offers what the backend granted, and says why when it did not', () => {
  it('lists the handover when the verdict is enabled', async () => {
    renderList([instanceRow()]);
    await waitFor(() => desktop().getByText('Đang chuẩn bị'));
    const panel = await openRowMenu('row-menu-desktop-5001');
    expect(within(panel).getByTestId('row-menu-item-transfer-host')).toBeEnabled();
  });

  it('shows the handover disabled WITH the cutoff reason when the verdict refused it', async () => {
    renderList([instanceRow({
      allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'],
      capabilities: [transferRefused(5001, 'FPT University Hà Nội')],
    })]);
    await waitFor(() => desktop().getByText('Đang chuẩn bị'));
    const panel = await openRowMenu('row-menu-desktop-5001');
    const item = within(panel).getByTestId('row-menu-item-transfer-host');
    expect(item).toBeDisabled();
    expect(item).toHaveTextContent(/ít nhất 6 giờ trước/);
  });

  it('omits the handover entirely when the caller was granted no verdict for it', async () => {
    renderList([instanceRow({ allowedActions: ['VIEW_DETAIL', 'OPEN_HOST_PROCESS'], capabilities: [] })]);
    await waitFor(() => desktop().getByText('Đang chuẩn bị'));
    const panel = await openRowMenu('row-menu-desktop-5001');
    expect(within(panel).queryByTestId('row-menu-item-transfer-host')).not.toBeInTheDocument();
  });

  it('closes on Escape and returns focus to the trigger', async () => {
    renderList([instanceRow()]);
    await waitFor(() => desktop().getByText('Đang chuẩn bị'));
    const trigger = screen.getByTestId('row-menu-desktop-5001');
    await userEvent.click(trigger);
    expect(screen.getByTestId('row-menu-desktop-5001-panel')).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByTestId('row-menu-desktop-5001-panel')).not.toBeInTheDocument());
    expect(trigger).toHaveFocus();
  });
});

// ── §15.10 single campus opens the modal for THAT campus ─────────────────────────────────────────

describe('single-campus handover', () => {
  it('opens the transfer modal scoped to the row\'s own campus instance', async () => {
    renderList([instanceRow()]);
    await waitFor(() => desktop().getByText('Đang chuẩn bị'));
    const panel = await openRowMenu('row-menu-desktop-5001');
    await userEvent.click(within(panel).getByTestId('row-menu-item-transfer-host'));

    // Asserted by role + test ids rather than by label text: the modal is i18n-driven (the test
    // locale is EN) while the list page itself is Vietnamese, so matching its title here would only
    // be testing which locale the harness picked.
    expect(await screen.findByRole('dialog')).toBeInTheDocument();
    expect(screen.getByTestId('host-transfer-current')).toHaveTextContent('IC Staff Hà Nội');
    // Candidates are fetched for THIS instance — never the request, which has no single host.
    expect(hostCandidatesMock).toHaveBeenCalledWith(5001);
    // The cutoff travels into the form so the deadline is not first learned from an error.
    expect(screen.getByTestId('host-transfer-cutoff')).toBeInTheDocument();
  });
});

// ── §15.11 / §15.12 / §15.13 multi-campus scoping ────────────────────────────────────────────────

describe('multi-campus handover lives on the campus, never on the summary row', () => {
  it('gives the summary row no handover at all', async () => {
    renderList([summaryRow()]);
    await waitFor(() => desktop().getByText('Đã duyệt'));
    const panel = await openRowMenu('row-menu-desktop-4001');
    expect(within(panel).queryByTestId('row-menu-item-transfer-host')).not.toBeInTheDocument();
  });

  it('offers it inside the accordion, on the campus the caller leads', async () => {
    renderList([summaryRow()]);
    await waitFor(() => desktop().getByText('Đã duyệt'));
    await userEvent.click(desktop().getByRole('button', { name: 'Xem tiến trình theo từng cơ sở' }));

    // Hà Nội (led by this caller) has a menu; Cần Thơ has no verdict, so no menu at all.
    expect(await waitFor(() => screen.getByTestId('campus-menu-desktop-5001'))).toBeInTheDocument();
    expect(screen.queryByTestId('campus-menu-desktop-5002')).not.toBeInTheDocument();

    const panel = await openRowMenu('campus-menu-desktop-5001');
    await userEvent.click(within(panel).getByTestId(`row-menu-item-transfer-host-5001`));

    await screen.findByRole('dialog');
    // Scoped to Hà Nội — the sibling campus is untouched by construction, because its id never
    // reaches the modal.
    expect(hostCandidatesMock).toHaveBeenCalledWith(5001);
    expect(hostCandidatesMock).not.toHaveBeenCalledWith(5002);
    expect(screen.getByTestId('host-transfer-current')).toHaveTextContent('IC Staff Hà Nội');
  });

  it('shows each campus its own reception owner, by the new name', async () => {
    renderList([summaryRow()]);
    await waitFor(() => desktop().getByText('Đã duyệt'));
    await userEvent.click(desktop().getByRole('button', { name: 'Xem tiến trình theo từng cơ sở' }));
    const labels = await waitFor(() => desktop().getAllByText(/Người phụ trách tiếp đón:/));
    expect(labels.length).toBeGreaterThanOrEqual(2);
    expect(desktop().getByText('IC Staff Cần Thơ')).toBeInTheDocument();
  });
});

// ── §15.15 mobile affordances carry words, not only icons ────────────────────────────────────────
// "Xem form" and "Mở quy trình" no longer live in the action column as their own icons — the
// row/card itself is the click target now (handleRowClick), so this only has to prove the escape
// hatch is still text-labeled, not that a hidden icon exists.

describe('mobile', () => {
  it('keeps "Xem form đăng ký tham quan" reachable as a text-labeled ⋯ menu item', async () => {
    renderList([instanceRow()]);
    await waitFor(() => desktop().getByText('Đang chuẩn bị'));
    const panel = await openRowMenu('row-menu-mobile-5001');
    expect(within(panel).getByText('Xem form đăng ký tham quan')).toBeInTheDocument();
  });
});
