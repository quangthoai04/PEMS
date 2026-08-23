/**
 * The status dropdown must send the CANONICAL `effectiveStatus`/`effectiveStatuses` param (matching a
 * row's own `effectiveStatusCode`/`statusLabel`), never the legacy `requestStatus`/`campusStatus`/
 * `approvedAny`/`pendingApprovalAny`/`cancelledOnly` fields that could disagree with the rendered badge
 * — see docs/CanhIter3FixBug/GopYCQuyen/PEMS_PATCH_PLAN_STATUS_FILTER_VALIDATION_UX.md.
 *
 * "Đã duyệt" sends the SINGLE code `effectiveStatus=ASSIGNED` (dropped the `ASSIGNED,APPROVED` union
 * 2026-08-23 — docs/CanhIter3FixBug/GopYCQuyen/PEMS_ROLE_TAB_STATUS_FILTER_COMPREHENSIVE_FIX_PLAN.md):
 * under REQUEST_ANY_CAMPUS semantics the backend now matches "any campus instance carries this exact
 * status", so the rare request-aggregate-only `APPROVED` code (every campus individually
 * cancelled/rejected) is no longer unioned in — that row correctly surfaces under whichever status its
 * own campuses actually have instead (e.g. "Đã hủy").
 *
 * Covers the literal reported repro (regular Staff's default "Tất cả các loại đơn" tab, which lands
 * on tab=all) plus HO's merged "Chờ duyệt"/"Đã duyệt" rows, which is where the old `pendingApprovalAny`/
 * `approvedAny` union produced the exact filter-vs-badge mismatch (VisitRowLabelsTests.
 * Ho_merges_partially_approved_into_the_same_pending_word_as_waiting_request_approval documents the
 * backend half of this; this file is the frontend half — the param that reaches the backend at all).
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import i18n from '../../../../shared/i18n/config';

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
  { userId: '77', roleCode: 'STAFF', subRole: 'STAFF' };
vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: currentUser }),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

const renderPage = () => render(
  <MemoryRouter initialEntries={['/dashboard/visit']}>
    <VisitRequestManagement />
  </MemoryRouter>,
);

/**
 * Opens the status dropdown and clicks the option with this exact visible label.
 *
 * Resolves the trigger button ONCE (its text is "Tất cả trạng thái" only before any selection) and
 * reuses that same element reference on every call — after the first selection the trigger's own
 * label becomes whatever was just picked, so re-querying by that placeholder text on a later call
 * (a test that picks several statuses in sequence) would fail to find it again.
 */
let statusTrigger: HTMLElement | null = null;
const chooseStatus = async (label: string) => {
  if (!statusTrigger) statusTrigger = screen.getByText('Tất cả trạng thái').closest('button');
  await userEvent.click(statusTrigger!);
  const option = await screen.findAllByText(label);
  await userEvent.click(option[option.length - 1]);
};

const lastListCallParams = () => {
  const call = listMock.mock.calls[listMock.mock.calls.length - 1];
  return (call?.[0] ?? {}) as Record<string, unknown>;
};

beforeEach(() => {
  vi.clearAllMocks();
  listMock.mockResolvedValue({ items: [], totalItems: 0 });
  invitationsMock.mockResolvedValue({ items: [], totalItems: 0 });
  hostCandidatesMock.mockResolvedValue([]);
  statusTrigger = null; // each test renders a fresh instance — the previous DOM node is gone
});

describe('Regular Staff — default "Tất cả các loại đơn" tab (tab=all, the literal reported bug)', () => {
  beforeEach(() => {
    currentUser = { userId: '501', roleCode: 'STAFF', subRole: 'STAFF' };
  });

  it('"Đã duyệt" sends effectiveStatus=ASSIGNED — never the old broad requestStatus=APPROVED, an aggregate union, or a lifecycle union', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await chooseStatus('Đã duyệt');
    await waitFor(() => {
      const params = lastListCallParams();
      expect(params.effectiveStatus).toBe('ASSIGNED');
    });
    const params = lastListCallParams();
    expect(params.requestStatus).toBeUndefined();
    expect(params.campusStatus).toBeUndefined();
    // Single code only — no aggregate union (dropped 2026-08-23) and no later-lifecycle stage,
    // each of which has its own distinct badge text and its own separate option.
    expect(params.effectiveStatuses).toBeUndefined();
  });

  it('"Đang chuẩn bị" sends effectiveStatus=BEFORE_VISIT, disjoint from the "Đã duyệt" bucket above', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await chooseStatus('Đang chuẩn bị');
    await waitFor(() => {
      const params = lastListCallParams();
      expect(params.effectiveStatus).toBe('BEFORE_VISIT');
    });
    expect(lastListCallParams().campusStatus).toBeUndefined();
  });

  it('"Đã hủy" sends effectiveStatus=CANCELLED — no longer the broad cancelledOnly union', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await chooseStatus('Đã hủy');
    await waitFor(() => {
      const params = lastListCallParams();
      expect(params.effectiveStatus).toBe('CANCELLED');
    });
    expect(lastListCallParams().cancelledOnly).toBeUndefined();
  });

  it('has no "Duyệt một phần" option — PARTIALLY_APPROVED stays an internal canonical code, never a UI filter that was not in the original requirement', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());
    await userEvent.click(screen.getByText('Tất cả trạng thái'));
    expect(screen.queryByText('Duyệt một phần')).toBeNull();
  });
});

describe('Visitor — "Đã duyệt" must be narrow (this was the un-fixed branch: still a lifecycle union after the first pass)', () => {
  // Only the Visitor branch of getVisitRequestFilterConfig renders through real i18next t()
  // (every other role's labels are hardcoded Vietnamese) — pin the language so the Vietnamese
  // label assertions below match regardless of whatever language a prior test left active.
  beforeEach(async () => {
    currentUser = { userId: '200', roleCode: 'VISITOR' };
    await i18n.changeLanguage('vi');
  });

  it('"Đã duyệt" sends effectiveStatus=ASSIGNED (single code) — reproduces and fixes /dashboard/visit?tab=all&status=APPROVED returning a BEFORE_VISIT-badged row', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await chooseStatus('Đã duyệt');
    await waitFor(() => {
      const params = lastListCallParams();
      expect(params.effectiveStatus).toBe('ASSIGNED');
    });
    // No aggregate union and no lifecycle union — a request with a BEFORE_VISIT/DURING_VISIT/.../
    // CANCELLED/REJECTED campus never matches "Đã duyệt" through this param.
    expect(lastListCallParams().effectiveStatuses).toBeUndefined();
    expect(lastListCallParams().requestStatus).toBeUndefined();
  });

  it('"Đang chuẩn bị"/"Đang diễn ra"/"Chờ đánh giá"/"Đã hoàn tất" each stay single, disjoint codes — no overlap with "Đã duyệt"', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    const cases: Array<[string, string]> = [
      ['Đang chuẩn bị', 'BEFORE_VISIT'],
      ['Đang diễn ra', 'DURING_VISIT'],
      ['Chờ đánh giá', 'AFTER_VISIT'],
      ['Đã hoàn tất', 'CLOSED'],
    ];
    for (const [label, code] of cases) {
      await chooseStatus(label);
      await waitFor(() => expect(lastListCallParams().effectiveStatus).toBe(code));
      expect(lastListCallParams().effectiveStatuses).toBeUndefined();
    }
  });

  it('has no "Duyệt một phần" option', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());
    await userEvent.click(screen.getByText('Tất cả trạng thái'));
    expect(screen.queryByText('Duyệt một phần')).toBeNull();
  });
});

describe('HO — merged quick filters used to union PARTIALLY_APPROVED into "Đã duyệt" (P0-01\'s own repro)', () => {
  beforeEach(() => {
    currentUser = { userId: '900', roleCode: 'HO' };
  });

  it('"Đã duyệt" sends effectiveStatus=ASSIGNED (single code) — no longer approvedAny, and no longer the ASSIGNED,APPROVED aggregate union', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await chooseStatus('Đã duyệt');
    await waitFor(() => {
      const params = lastListCallParams();
      expect(params.effectiveStatus).toBe('ASSIGNED');
    });
    // REQUEST_ANY_CAMPUS (2026-08-23): HO matches "any campus instance is ASSIGNED" directly, so the
    // request-aggregate-only APPROVED code is no longer unioned in — dropping it is what lets a
    // request with one campus ASSIGNED and a sibling still WAITING_REQUEST_APPROVAL correctly appear
    // under BOTH "Đã duyệt" and "Chờ duyệt" instead of being force-collapsed into one bucket.
    expect(lastListCallParams().effectiveStatuses).toBeUndefined();
    expect(lastListCallParams().approvedAny).toBeUndefined();
  });

  it('"Chờ duyệt" sends effectiveStatus=WAITING_REQUEST_APPROVAL — no longer pendingApprovalAny', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await chooseStatus('Chờ duyệt');
    await waitFor(() => {
      const params = lastListCallParams();
      expect(params.effectiveStatus).toBe('WAITING_REQUEST_APPROVAL');
    });
    expect(lastListCallParams().pendingApprovalAny).toBeUndefined();
  });
});

describe('Staff Leader — options already mapped 1:1 to canonical campus codes', () => {
  beforeEach(() => {
    currentUser = { userId: '600', roleCode: 'STAFF', subRole: 'LEADER' };
  });

  it('"Đã duyệt" sends effectiveStatus=ASSIGNED (single code, not a union)', async () => {
    renderPage();
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    await chooseStatus('Đã duyệt');
    await waitFor(() => {
      const params = lastListCallParams();
      expect(params.effectiveStatus).toBe('ASSIGNED');
    });
    expect(lastListCallParams().campusStatus).toBeUndefined();
  });
});

describe('Badge rendering prefers the backend statusLabel/effectiveStatusCode over client re-derivation', () => {
  beforeEach(() => {
    currentUser = { userId: '600', roleCode: 'STAFF', subRole: 'LEADER' };
  });

  it('renders row.statusLabel as the visible status text', async () => {
    listMock.mockResolvedValue({
      items: [{
        visitRequestId: 1, visitInstanceId: 101, requestCode: 'VR-2026-0001',
        delegationName: 'Đoàn Test', partnerName: 'Test Co.',
        requestStatus: 'PARTIALLY_APPROVED', campusStatus: 'ASSIGNED',
        effectiveStatusCode: 'ASSIGNED',
        // Deliberately atypical label text so the assertion can only pass if the component is
        // actually reading statusLabel, not re-deriving Vietnamese text from the raw codes itself.
        statusLabel: '__CANONICAL_LABEL__',
        visitScope: 'SINGLE_CAMPUS', campusId: 1, campusName: 'FPT University Hà Nội',
        campusCount: 1, createdByUserId: 10, registrantUserId: 10,
        currentHostUserId: null, hostName: null, currentUserIsHost: false, rowVersion: 1,
        visitorUserId: 10, visitorName: 'Visitor', isCurrentUserParticipant: false,
        participantId: null, participantRole: null, currentUserRelation: 'CAMPUS_REVIEWER',
        relationLabel: 'Cần bạn duyệt', nextTask: null, expectedStartAt: null, expectedEndAt: null,
        plannedStartAt: '2026-08-25T09:00:00', plannedEndAt: '2026-08-25T12:00:00',
        createdAt: '2026-08-19T09:00:00', submittedAt: '2026-08-19T09:00:00',
        cancelledAt: null, cancellationReason: null, decisionNote: null,
        canExpandCampuses: false, canViewRequestDetail: true, campusProgressItems: [],
        allowedActions: ['VIEW_DETAIL'], capabilities: [],
      }],
      totalItems: 1,
    });

    renderPage();
    // Rendered in both the desktop table and the mobile card layout — either is proof enough here.
    await waitFor(() => expect(screen.getAllByText('__CANONICAL_LABEL__').length).toBeGreaterThan(0));
  });
});
