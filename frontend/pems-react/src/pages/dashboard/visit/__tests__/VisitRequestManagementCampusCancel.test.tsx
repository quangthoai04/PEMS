/**
 * Campus-level cancellation from the multi-campus accordion: the modal must name the campus the
 * reader actually clicked, and the request it sends must still be scoped to that campus alone.
 *
 * The bug this guards: the accordion's cancel button hands the modal the request-level PARENT row
 * plus the clicked child's visitInstanceId. The modal then took its campus name from
 * `row.campus`, which on a multi-campus request reads "2 cơ sở" — so someone cancelling Cần Thơ was
 * asked to confirm "Hủy lịch thăm tại cơ sở 2 cơ sở", wording that reads like it cancels everything
 * while the request underneath correctly cancelled one campus. A confirmation dialog that misstates
 * its own blast radius is the one place wrong wording does real damage.
 *
 * So these tests hold two INDEPENDENT things at once, and that separation is the actual subject:
 *
 *     mutation target  = cancel.instanceId          → /campuses/{id}/cancel
 *     display target   = cancel.instanceCampusName  → the modal's wording
 *
 * Every case therefore asserts the wording AND the endpoint together; asserting either alone would
 * let a "fix" that moved the wrong one pass.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const listMock = vi.fn();
const invitationsMock = vi.fn();
const cancelRequestMock = vi.fn();
const cancelCampusMock = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitRequestManagementList: (...args: unknown[]) => listMock(...args),
    getMyInvitations: (...args: unknown[]) => invitationsMock(...args),
    getHostCandidates: vi.fn().mockResolvedValue([]),
    cancelVisitRequest: (...args: unknown[]) => cancelRequestMock(...args),
    cancelVisitRequestCampus: (...args: unknown[]) => cancelCampusMock(...args),
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

vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: { userId: '10', roleCode: 'VISITOR' } }),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

// ── Fixtures ─────────────────────────────────────────────────────────────────────────────────────

const HN = 'FPT University Hà Nội';
const CT = 'FPT University Cần Thơ';

/**
 * The multi-campus SUMMARY row, as the registrant sees it: one row for the whole request, whose own
 * `campusName` is the aggregate "2 cơ sở" — precisely the value that must NOT reach the modal — with
 * two cancellable campuses underneath it.
 */
const summaryRow = (over: Record<string, unknown> = {}) => ({
  visitRequestId: 4001,
  visitInstanceId: null,
  requestCode: 'VR-2026-4001',
  delegationName: 'Jeju Tourism Technology Delegation',
  partnerName: 'Jeju Tech Agency',
  requestStatus: 'APPROVED',
  campusStatus: null,
  visitScope: 'MULTI_CAMPUS',
  campusId: null,
  campusName: '2 cơ sở',
  campusCount: 2,
  createdByUserId: 10,
  currentHostUserId: null,
  hostName: null,
  currentUserIsHost: false,
  rowVersion: null,
  visitorUserId: 10,
  visitorName: 'Nguyen Van A',
  registrantUserId: 10,
  isCurrentUserParticipant: false,
  participantRole: null,
  currentUserRelation: 'VISITOR_OWNER',
  relationLabel: 'Bạn là đầu mối đoàn khách',
  statusLabel: 'Đã duyệt',
  nextTask: null,
  plannedStartAt: '2026-08-15T09:00:00',
  plannedEndAt: '2026-08-20T12:00:00',
  createdAt: '2026-07-01T09:00:00',
  submittedAt: '2026-07-01T09:00:00',
  cancelledAt: null,
  cancellationReason: null,
  decisionNote: null,
  canExpandCampuses: true,
  canViewRequestDetail: true,
  allowedActions: ['VIEW_DETAIL', 'CANCEL_BY_VISITOR'],
  capabilities: [],
  relationContexts: [],
  campusProgressItems: [
    {
      visitInstanceId: 5001, campusId: 1, campusCode: 'HN', campusName: HN,
      plannedStartAt: '2026-08-15T09:00:00', plannedEndAt: '2026-08-15T12:00:00',
      instanceStatus: 'ASSIGNED', hostUserId: 77, hostName: 'IC Staff Hà Nội', rowVersion: 4,
      canViewCampusDetail: true, canCancelCampusVisit: true, canViewCancelReason: false,
      canViewRejectReason: false, capabilities: [], canTransferHost: false,
    },
    {
      visitInstanceId: 5002, campusId: 4, campusCode: 'CT', campusName: CT,
      plannedStartAt: '2026-08-20T09:00:00', plannedEndAt: '2026-08-20T12:00:00',
      instanceStatus: 'ASSIGNED', hostUserId: 91, hostName: 'IC Staff Cần Thơ', rowVersion: 2,
      canViewCampusDetail: true, canCancelCampusVisit: true, canViewCancelReason: false,
      canViewRejectReason: false, capabilities: [], canTransferHost: false,
    },
  ],
  ...over,
});

const renderList = (rows: unknown[]) => {
  listMock.mockResolvedValue({ items: rows, totalItems: rows.length });
  return render(<MemoryRouter><VisitRequestManagement /></MemoryRouter>);
};

// Both layouts render at once (CSS picks one), so every query is scoped to the desktop one or it
// finds two of everything.
const desktop = () => within(screen.getByTestId('visit-list-desktop'));

/** Opens the per-campus accordion on the summary row. */
const expandCampuses = async () => {
  await waitFor(() => desktop().getByText('Jeju Tourism Technology Delegation'));
  await userEvent.click(desktop().getAllByLabelText('View progress by campus')[0]);
};

/**
 * Clicks the cancel button of ONE campus. Both buttons share an aria-label (it names the action, not
 * the campus), so they are addressed positionally — index follows campusProgressItems order.
 */
const clickCampusCancel = async (index: number) => {
  const buttons = await waitFor(() => {
    const found = desktop().getAllByLabelText('Cancel this campus visit');
    expect(found.length).toBeGreaterThan(index);
    return found;
  });
  await userEvent.click(buttons[index]);
};

const modalHeading = () => screen.getByRole('heading', { level: 3 }).textContent ?? '';

// The modal is a plain div, and the page behind it owns a search box and filter checkboxes — so the
// dialog's own two inputs are addressed by the text only IT carries, never by bare role.
const reasonBox = () => screen.getByPlaceholderText('Enter the cancellation reason...');
const confirmBox = () => screen.getByLabelText('I understand this cancellation cannot be undone.');

const closeModal = async () => {
  await userEvent.click(screen.getByRole('button', { name: 'Back' }));
  await waitFor(() => expect(screen.queryByText('Cancellation reason')).not.toBeInTheDocument());
};

beforeEach(() => {
  vi.clearAllMocks();
  invitationsMock.mockResolvedValue([]);
  cancelCampusMock.mockResolvedValue({});
  cancelRequestMock.mockResolvedValue({});
});

// ── The wording ──────────────────────────────────────────────────────────────────────────────────

describe('the campus cancellation modal names the campus that was clicked', () => {
  it('names the clicked campus in the title and the body, never the parent row\'s "2 cơ sở"', async () => {
    renderList([summaryRow()]);
    await expandCampuses();
    await clickCampusCancel(1); // Cần Thơ

    await waitFor(() => expect(screen.getByText('Cancellation reason')).toBeInTheDocument());

    expect(modalHeading()).toContain(CT);
    expect(screen.getByText(new RegExp(`You are cancelling the visit at ${CT}`))).toBeInTheDocument();

    // The aggregate name is the whole bug — it must appear nowhere in the open dialog.
    expect(modalHeading()).not.toContain('2 cơ sở');
    expect(screen.queryByText(/the visit at 2 cơ sở/)).not.toBeInTheDocument();
  });

  it('names the OTHER campus when that is the one clicked', async () => {
    renderList([summaryRow()]);
    await expandCampuses();
    await clickCampusCancel(0); // Hà Nội

    await waitFor(() => expect(screen.getByText('Cancellation reason')).toBeInTheDocument());
    expect(modalHeading()).toContain(HN);
    expect(modalHeading()).not.toContain(CT);
  });

  /**
   * Reopening on a different campus must not show the previous one. The name is state, and state
   * that is only written and never rewritten is the classic way a second click keeps the first
   * click's answer.
   */
  it('does not keep the previous campus name when reopened on another campus', async () => {
    renderList([summaryRow()]);
    await expandCampuses();

    await clickCampusCancel(1);
    await waitFor(() => expect(modalHeading()).toContain(CT));
    await closeModal();

    await clickCampusCancel(0);
    await waitFor(() => expect(screen.getByText('Cancellation reason')).toBeInTheDocument());
    expect(modalHeading()).toContain(HN);
    expect(modalHeading()).not.toContain(CT);
  });
});

// ── The mutation target, which the wording fix must not have moved ───────────────────────────────

describe('campus cancellation still mutates only the clicked campus', () => {
  it('posts to the CAMPUS endpoint with the clicked instance id, never the request endpoint', async () => {
    renderList([summaryRow()]);
    await expandCampuses();
    await clickCampusCancel(1); // Cần Thơ → instance 5002

    await waitFor(() => expect(screen.getByText('Cancellation reason')).toBeInTheDocument());
    await userEvent.type(reasonBox(), 'Đoàn khách đổi lịch');
    await userEvent.click(confirmBox());
    await userEvent.click(screen.getByRole('button', { name: 'Confirm cancelling this campus' }));

    await waitFor(() => expect(cancelCampusMock).toHaveBeenCalledTimes(1));
    expect(cancelCampusMock).toHaveBeenCalledWith(4001, 5002, { cancellationReason: 'Đoàn khách đổi lịch' });
    // Cancelling one campus must never fall through to cancelling the whole request.
    expect(cancelRequestMock).not.toHaveBeenCalled();
  });

  it('sends the OTHER campus\'s id when that campus was the one clicked', async () => {
    renderList([summaryRow()]);
    await expandCampuses();
    await clickCampusCancel(0); // Hà Nội → instance 5001

    await waitFor(() => expect(screen.getByText('Cancellation reason')).toBeInTheDocument());
    await userEvent.type(reasonBox(), 'Cơ sở bận lịch');
    await userEvent.click(confirmBox());
    await userEvent.click(screen.getByRole('button', { name: 'Confirm cancelling this campus' }));

    await waitFor(() => expect(cancelCampusMock).toHaveBeenCalledTimes(1));
    expect(cancelCampusMock).toHaveBeenCalledWith(4001, 5001, { cancellationReason: 'Cơ sở bận lịch' });
    expect(cancelRequestMock).not.toHaveBeenCalled();
  });
});

// ── Request-level cancellation, unchanged ────────────────────────────────────────────────────────

describe('request-level cancellation is untouched by the campus-name fix', () => {
  it('keeps the whole-request wording and posts to the request endpoint', async () => {
    renderList([summaryRow()]);
    await waitFor(() => desktop().getByText('Jeju Tourism Technology Delegation'));

    // Request-level cancel lives in the row's own ⋯ menu, not in the accordion. The menu's testId
    // is built from row.id, which is `visitInstanceId || visitRequestId` — a summary row has no
    // instance, so it falls back to the request id.
    await userEvent.click(screen.getByTestId('row-menu-desktop-4001'));
    await userEvent.click(
      within(screen.getByTestId('row-menu-desktop-4001-panel')).getByTestId('row-menu-item-cancel'));

    await waitFor(() => expect(screen.getByText('Cancellation reason')).toBeInTheDocument());
    // No instance is targeted, so the modal speaks about the whole multi-campus request — and in
    // particular does NOT reuse an instance name.
    expect(modalHeading()).toContain('Cancel the entire multi-campus visit');
    expect(modalHeading()).not.toContain(HN);
    expect(modalHeading()).not.toContain(CT);

    await userEvent.type(reasonBox(), 'Đoàn hủy chuyến');
    await userEvent.click(confirmBox());
    await userEvent.click(screen.getByRole('button', { name: 'Confirm cancelling all' }));

    await waitFor(() => expect(cancelRequestMock).toHaveBeenCalledTimes(1));
    expect(cancelRequestMock).toHaveBeenCalledWith(4001, { cancellationReason: 'Đoàn hủy chuyến' });
    expect(cancelCampusMock).not.toHaveBeenCalled();
  });
});
