/**
 * `feedbackVisitInstanceId` is a ONE-SHOT COMMAND, not page state.
 *
 * A feedback notification links to `/dashboard/visit?visitRequestId=…&feedbackVisitInstanceId=…`.
 * The page read that parameter in an effect and opened the modal — but never consumed it, and its URL
 * helper cloned the whole query string into every later update. So the parameter outlived the modal:
 * closing it, or submitting the feedback it asked for, left the trigger in the URL, and the next
 * filter / page / tab / search change re-ran the effect and reopened the modal. Deterministic, and
 * escapable only by leaving the page.
 *
 * The fix is to consume the intent on arrival — open the modal, strip the parameter with `replace`,
 * and let React state own the modal from there. These tests pin that: opened once, stripped once,
 * every other query parameter left alone, and no path back to a replay.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';

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

vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: { userId: '10', roleCode: 'VISITOR' } }),
}));

/**
 * Stubbed so the assertions are about the PAGE's control of the modal rather than about the modal's
 * own loading. `submit` mirrors the real component, which calls `onSubmitted` and then `onClose`.
 */
vi.mock('../../../../features/feedbacks/components/VisitFeedbackModal', () => ({
  VisitFeedbackModal: ({ open, visitInstanceId, onClose, onSubmitted }: {
    open: boolean;
    visitInstanceId: number | string | null;
    onClose: () => void;
    onSubmitted?: (id: number) => void;
  }) => (open ? (
    <div data-testid="feedback-modal" data-instance={String(visitInstanceId)}>
      <button type="button" data-testid="feedback-close" onClick={onClose}>close</button>
      <button
        type="button"
        data-testid="feedback-submit"
        onClick={() => { onSubmitted?.(Number(visitInstanceId)); onClose(); }}
      >
        submit
      </button>
    </div>
  ) : null),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

// ── Harness ──────────────────────────────────────────────────────────────────────────────────────

/** Reads the live query string, so "was it stripped" is answered by the router, not by a spy. */
const UrlProbe = () => <span data-testid="url-search">{useLocation().search}</span>;

const REQUEST_ID = 2003;
const INSTANCE_ID = 3105;

/** Enough rows for the pager to render — page 2 is how these tests trigger a later URL update. */
const rows = () => Array.from({ length: 10 }, (_, i) => ({
  visitRequestId: REQUEST_ID + i,
  visitInstanceId: INSTANCE_ID + i,
  requestCode: `VR-2026-${REQUEST_ID + i}`,
  delegationName: `Delegation ${i}`,
  partnerName: null,
  requestStatus: 'CLOSED',
  campusStatus: 'CLOSED',
  visitScope: 'SINGLE_CAMPUS',
  campusId: 1,
  campusName: 'FPT University Hà Nội',
  campusCount: 1,
  createdByUserId: 10,
  registrantUserId: 10,
  currentHostUserId: 42,
  hostName: 'IC Staff Hà Nội',
  currentUserIsHost: false,
  rowVersion: 1,
  visitorUserId: 10,
  visitorName: 'Nguyen Van A',
  isCurrentUserParticipant: false,
  participantRole: null,
  currentUserRelation: 'REGISTRANT',
  relationLabel: 'Đơn bạn đăng ký',
  statusLabel: 'Đã kết thúc',
  relations: ['REGISTRANT'],
  relationContexts: [],
  primaryEntryContext: 'REQUEST_DETAIL',
  primaryEntryVisitInstanceId: null,
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
  allowedActions: ['VIEW_DETAIL'],
  capabilities: [],
}));

const renderAt = (search: string) => {
  listMock.mockResolvedValue({ items: rows(), totalItems: 25 });
  return render(
    <MemoryRouter initialEntries={[`/dashboard/visit${search}`]}>
      <UrlProbe />
      <VisitRequestManagement />
    </MemoryRouter>,
  );
};

const params = () => new URLSearchParams(screen.getByTestId('url-search').textContent ?? '');

/**
 * A later search-param change made through the page's OWN controls — the reported repro, and the
 * path that runs `updateUrlParams`, which is the helper that used to carry the trigger forward.
 * Searching is used rather than paging because a deep link also sets `visitRequestId`, which puts
 * the list in single-request mode where there is no second page to click.
 */
const searchFor = async (keyword: string) => {
  await userEvent.type(await screen.findByTestId('visit-search-input'), keyword);
  // The keyword is debounced (400ms) before it reaches the URL.
  await waitFor(() => expect(params().get('keyword')).toBe(keyword), { timeout: 3000 });
};

beforeEach(() => {
  vi.clearAllMocks();
  invitationsMock.mockResolvedValue([]);
  hostCandidatesMock.mockResolvedValue([]);
});

// ── Consuming the intent ─────────────────────────────────────────────────────────────────────────

describe('a feedback deep link is consumed exactly once', () => {
  it('opens the modal for the named instance and strips only that parameter', async () => {
    renderAt(`?visitRequestId=${REQUEST_ID}&feedbackVisitInstanceId=${INSTANCE_ID}`);

    const modal = await screen.findByTestId('feedback-modal');
    expect(modal).toHaveAttribute('data-instance', String(INSTANCE_ID));

    await waitFor(() => expect(params().get('feedbackVisitInstanceId')).toBeNull());
    // The rest of the URL is not this effect's business.
    expect(params().get('visitRequestId')).toBe(String(REQUEST_ID));
  });

  it('keeps every other query parameter across the consume', async () => {
    renderAt(
      `?visitRequestId=${REQUEST_ID}&feedbackVisitInstanceId=${INSTANCE_ID}`
      + '&tab=all&keyword=ha&page=2',
    );
    await screen.findByTestId('feedback-modal');

    await waitFor(() => expect(params().get('feedbackVisitInstanceId')).toBeNull());
    expect(params().get('visitRequestId')).toBe(String(REQUEST_ID));
    expect(params().get('tab')).toBe('all');
    expect(params().get('keyword')).toBe('ha');
    expect(params().get('page')).toBe('2');
  });

  it.each(['abc', '0', '-1'])(
    'opens nothing for %s and still cleans the parameter away',
    async raw => {
      renderAt(`?visitRequestId=${REQUEST_ID}&feedbackVisitInstanceId=${raw}&tab=all`);
      await waitFor(() => expect(params().get('feedbackVisitInstanceId')).toBeNull());

      expect(screen.queryByTestId('feedback-modal')).toBeNull();
      // Junk in the trigger parameter must not take the reader's filters down with it.
      expect(params().get('visitRequestId')).toBe(String(REQUEST_ID));
      expect(params().get('tab')).toBe('all');
    },
  );
});

// ── No path back to a replay ─────────────────────────────────────────────────────────────────────

describe('the modal does not come back on its own', () => {
  it('stays closed after the user closes it and then searches', async () => {
    renderAt(`?visitRequestId=${REQUEST_ID}&feedbackVisitInstanceId=${INSTANCE_ID}`);
    await screen.findByTestId('feedback-modal');
    await waitFor(() => expect(params().get('feedbackVisitInstanceId')).toBeNull());

    await userEvent.click(screen.getByTestId('feedback-close'));
    expect(screen.queryByTestId('feedback-modal')).toBeNull();

    // The exact action that used to bring it back: any change that rewrites searchParams.
    await searchFor('ha');
    expect(screen.queryByTestId('feedback-modal')).toBeNull();
    expect(params().get('feedbackVisitInstanceId')).toBeNull();
  });

  it('stays closed after a successful submission and then searches', async () => {
    renderAt(`?visitRequestId=${REQUEST_ID}&feedbackVisitInstanceId=${INSTANCE_ID}`);
    await screen.findByTestId('feedback-modal');
    await waitFor(() => expect(params().get('feedbackVisitInstanceId')).toBeNull());

    await userEvent.click(screen.getByTestId('feedback-submit'));
    expect(screen.queryByTestId('feedback-modal')).toBeNull();

    await searchFor('ha');
    expect(screen.queryByTestId('feedback-modal')).toBeNull();
    expect(params().get('feedbackVisitInstanceId')).toBeNull();
  });

  it('never carries the trigger forward through a URL update, even if one survives', async () => {
    // Defense in depth for the helper itself: were the parameter still present when a filter/search
    // change ran, cloning the query string is exactly how it used to ride along to the next URL.
    renderAt(`?visitRequestId=${REQUEST_ID}&feedbackVisitInstanceId=${INSTANCE_ID}&tab=all`);
    await screen.findByTestId('feedback-modal');
    await userEvent.click(screen.getByTestId('feedback-close'));

    await searchFor('ha');
    expect(params().get('feedbackVisitInstanceId')).toBeNull();
    expect(screen.queryByTestId('feedback-modal')).toBeNull();
  });
});

// ── The ordinary route is untouched ──────────────────────────────────────────────────────────────

describe('a visit list opened without the parameter', () => {
  it('opens no feedback modal and leaves the URL alone', async () => {
    renderAt(`?visitRequestId=${REQUEST_ID}&tab=all`);
    await waitFor(() => expect(listMock).toHaveBeenCalled());

    expect(screen.queryByTestId('feedback-modal')).toBeNull();
    expect(params().get('visitRequestId')).toBe(String(REQUEST_ID));
    expect(params().get('tab')).toBe('all');
  });
});
