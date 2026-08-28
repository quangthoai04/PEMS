import { describe, expect, it, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import VisitRequestV2DetailView from '../components/v2/VisitRequestV2DetailView';
import type { ResolvedVisitForm } from '../api/visitRequestV2Api';
import { campusFixture } from './fixtures';

vi.mock('../api/visitRequestV2Api', () => ({
  getVisitRequestFormV2: vi.fn(),
  // Wired G-1 panels fetch on mount — give them quiet defaults.
  getActiveContactTransfer: vi.fn().mockResolvedValue({
    visitRequestId: 1, hasPendingTransfer: false, identityChangeId: null,
    status: null, newEmailMasked: null, expiresAt: null, resendCount: 0,
  }),
  getActiveAmendment: vi.fn().mockResolvedValue(null),
  getVisitRequestHistory: vi.fn().mockResolvedValue({ visitRequestId: 1, requestCode: 'VR-1', entries: [] }),
  getVisitHistoryDetail: vi.fn(),
  // The detail view clears the caller's unread badge once it has rendered.
  markVisitChangesSeen: vi.fn().mockResolvedValue({ markedCount: 0 }),
  resendContactClaim: vi.fn(),
  replacePendingContact: vi.fn(),
  initiateContactTransfer: vi.fn(),
  resendContactTransfer: vi.fn(),
  cancelContactTransfer: vi.fn(),
  approveAmendment: vi.fn(),
  rejectAmendment: vi.fn(),
  withdrawAmendment: vi.fn(),
}));

// The view reads the signed-in user (commit 6be02a28) purely to pick between two labels on the
// "open amendment" button. Every case below is driven by viewer.relation and per-instance
// allowedActions from the API, which is the point the STAFF_LEADER case makes, so the null user is
// accurate rather than merely convenient. Mocked, not <AuthProvider>-wrapped, as elsewhere here.
vi.mock('../../../shared/auth/AuthContext', () => ({ useAuthContext: () => ({ user: null }) }));

// AssignHostModal (ordinary Approve) and VisitCampusRejectModal (ordinary Reject) both go through
// this API — mocked here so the gap-closure tests below can prove the SAME commands/component are
// reused rather than a new approval path.
vi.mock('../../delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getHostCandidates: vi.fn(),
    approveCampusInstance: vi.fn(),
    rejectCampusInstance: vi.fn(),
  },
}));

import {
  getVisitRequestFormV2,
  getActiveAmendment,
  getVisitRequestHistory,
} from '../api/visitRequestV2Api';
import { delegationsApi } from '../../delegations/api/delegationsApi';

const formFixture = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => ({
  visitRequestId: 1,
  requestCode: 'VR-2026-001',
  rowVersion: 0,
  hasMixedCampusDetails: false,
  visitScope: 'SINGLE_CAMPUS',
  requestStatus: 'PENDING_APPROVAL',
  createdSource: 'PUBLIC',
  submittedAt: '2026-07-15T08:00:00',
  partnerId: null,
  cancelledByUserId: null,
  cancelledByName: null,
  cancelledAt: null,
  cancellationReason: null,
  registrant: {
    fullName: 'Người Đăng Ký', organization: 'ĐH ABC', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@x.vn', nationality: 'VN',
  },
  confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },

  // Full-request scope in this fixture, so the backend sends the request-wide verdict.

  requestOutcome: { code: 'ALL_WAITING', total: 1, accepted: 0, inProgress: 0, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
  campusVisits: [campusFixture()],
  // A REGISTRANT reads the change history, so the backend sends VIEW_CHANGE_HISTORY beside VIEW —
  // the two read capabilities are separate because a supporting participant gets only the first.
  viewer: {
    relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false,
    allowedActions: ['VIEW', 'VIEW_CHANGE_HISTORY'],
  },
  ...overrides,
});

const axios404 = Object.assign(new Error('404'), {
  isAxiosError: true,
  response: { status: 404, data: { message: 'Không tìm thấy.' } },
});

describe('VisitRequestV2DetailView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getActiveAmendment).mockResolvedValue(null);
    vi.mocked(getVisitRequestHistory).mockResolvedValue({ visitRequestId: 1, requestCode: 'VR-1', entries: [] });
  });

  it('renders request-level data ONCE and one card per AUTHORIZED campus — no mixed label for same data', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.getAllByLabelText(/Campus detail/)).toHaveLength(1);
    expect(screen.queryByText('Varies by campus')).not.toBeInTheDocument();
  });

  it('mixed request: every returned campus renders its OWN content + the varies-by-campus label; no first-campus projection', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      hasMixedCampusDetails: true,
      visitScope: 'MULTI_CAMPUS',
      campusVisits: [
        campusFixture(),
        campusFixture({
          visitInstanceId: 11, campusId: 2, campusCode: 'HCM', campusName: 'FPTU Hồ Chí Minh',
          delegationName: 'Đoàn HCM khác hẳn', purpose: 'Mục đích HCM', workingContent: 'ND HCM',
          currentHostName: 'Host HCM', currentHost: { userId: 7, fullName: 'Host HCM', email: 'host@fptu.vn', phone: '+8490', departmentName: 'IC' }, instanceStatus: 'PENDING',
        }),
      ],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByText('Varies by campus')).toBeInTheDocument();
    expect(screen.getAllByLabelText(/Campus detail/)).toHaveLength(2);
    // Two campuses → accordion, and it opens CLOSED: the reader picks the campus they came for
    // instead of being handed whichever one happens to be first. What a closed campus still shows is
    // the point of the design — name, status, amendment badge and planned time stay on the header,
    // so the choice can be made without opening anything.
    expect(screen.queryByText('Đoàn ĐH ABC')).not.toBeInTheDocument();
    expect(screen.queryByText('Đoàn HCM khác hẳn')).not.toBeInTheDocument();
    expect(screen.getByText('FPTU Hồ Chí Minh')).toBeInTheDocument();
    expect(screen.getByTestId('campus-status-11')).toBeInTheDocument();

    // Opening HCM shows ITS content — it never inherits HN's delegation name — and opening HN beside
    // it LEAVES HCM OPEN. Each campus is independent: comparing two campuses of one request side by
    // side is the ordinary thing to do here, and it used to be impossible because opening one shut
    // the other.
    fireEvent.click(screen.getByTestId('campus-detail-toggle-11'));
    expect(await screen.findByText('Đoàn HCM khác hẳn')).toBeInTheDocument();
    expect(screen.getByText('Host HCM')).toBeInTheDocument();
    fireEvent.click(screen.getByTestId('campus-detail-toggle-10'));
    expect(await screen.findByText('Đoàn ĐH ABC')).toBeInTheDocument();
    expect(screen.getByText('Đoàn HCM khác hẳn')).toBeInTheDocument();

    // Closing HN touches HN alone.
    fireEvent.click(screen.getByTestId('campus-detail-toggle-10'));
    expect(screen.queryByText('Đoàn ĐH ABC')).not.toBeInTheDocument();
    expect(screen.getByText('Đoàn HCM khác hẳn')).toBeInTheDocument();

    // And re-opening it brings it back beside HCM rather than replacing it.
    fireEvent.click(screen.getByTestId('campus-detail-toggle-10'));
    expect(await screen.findByText('Đoàn ĐH ABC')).toBeInTheDocument();
    expect(screen.getByText('Đoàn HCM khác hẳn')).toBeInTheDocument();

    // Closing every card is still reachable — "show me nothing but the headers" is a real choice.
    fireEvent.click(screen.getByTestId('campus-detail-toggle-10'));
    fireEvent.click(screen.getByTestId('campus-detail-toggle-11'));
    expect(screen.queryByText('Đoàn HCM khác hẳn')).not.toBeInTheDocument();
    expect(screen.queryByText('Đoàn ĐH ABC')).not.toBeInTheDocument();
  });

  it('a single-campus request is NOT collapsible — its one card is open with no chevron', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.getByText('Đoàn ĐH ABC')).toBeInTheDocument();
    expect(screen.queryByTestId('campus-detail-toggle-10')).toBeNull();
  });

  /**
   * Reading ONE campus of a multi-campus request (`?campus=` → `focusInstanceId`). The reader came
   * from that campus's row in the list, so section ② answers about that campus — it used to hand
   * them the whole request, TP.HCM's row opening onto Hà Nội's content.
   */
  describe('one campus at a time', () => {
    const twoCampuses = () => formFixture({
      hasMixedCampusDetails: true,
      visitScope: 'MULTI_CAMPUS',
      campusVisits: [
        campusFixture(),
        campusFixture({
          visitInstanceId: 11, campusId: 2, campusCode: 'HCM', campusName: 'FPTU Hồ Chí Minh',
          delegationName: 'Đoàn HCM khác hẳn', instanceStatus: 'PENDING',
        }),
      ],
    });

    it('shows only the focused campus, already open and with no chevron to collapse it', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(twoCampuses());
      render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} focusInstanceId={11} /></MemoryRouter>);

      expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
      expect(screen.getAllByLabelText(/Campus detail/)).toHaveLength(1);
      // TP.HCM's own content, open on arrival — one campus is never collapsible.
      expect(screen.getByText('Đoàn HCM khác hẳn')).toBeInTheDocument();
      expect(screen.queryByTestId('campus-detail-toggle-11')).toBeNull();
      // Hà Nội is not part of the answer — not its card, not its header, not its content.
      expect(screen.queryByText('Đoàn ĐH ABC')).not.toBeInTheDocument();
      expect(screen.queryByTestId('campus-status-10')).toBeNull();
    });

    it('ignores a campus id the payload does not contain, rather than emptying the section', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(twoCampuses());
      // 99 is not in scope for this caller (revoked access, or a stale link).
      render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} focusInstanceId={99} /></MemoryRouter>);

      expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
      expect(screen.getAllByLabelText(/Campus detail/)).toHaveLength(2);
      expect(screen.queryByText(/Không có cơ sở|No campus/i)).toBeNull();
    });

    it('leaves the request-level facts alone — the overview still counts every campus', async () => {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(twoCampuses());
      render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} focusInstanceId={11} /></MemoryRouter>);

      expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
      // The overview badge describes the REQUEST (2 campuses); section ②'s own badge describes what
      // that section shows (1). Both are true at once, which is why they are counted separately.
      const counts = screen.getAllByText(/campus(es)?$/i).map(el => el.textContent);
      expect(counts.some(text => text?.includes('2'))).toBe(true);
      expect(counts.some(text => text?.includes('1'))).toBe(true);
    });
  });

  it('scoped payload is rendered verbatim: a single-campus response for an instance-scoped viewer shows ONE card and no sibling hints', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      hasMixedCampusDetails: true, // request IS mixed…
      visitScope: 'MULTI_CAMPUS',
      campusVisits: [campusFixture()], // …but the backend scoped this caller to one campus
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.getAllByLabelText(/Campus detail/)).toHaveLength(1);
    // The mixed badge needs >1 VISIBLE campuses — a scoped single card must not hint at siblings:
    expect(screen.queryByText('Varies by campus')).not.toBeInTheDocument();
  });

  // The codes below are the ones PEMS.Domain.Constants.VisitFormActions actually emits. They used to be
  // a parallel, shorter set that matched nothing the backend sends, so the panel rendered nothing in
  // production while this test passed against a fixture that agreed with the frontend's own mistake.
  it('contact actions follow the backend action codes, not the viewer relation', async () => {
    // A REGISTRANT with no contact action code granted (for instance inside the 24h window, or with
    // the request already cancelled) must see NOTHING — the relation alone never earns a button.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    const { unmount: unmountBare } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-identity-actions-10')).not.toBeInTheDocument();
    unmountBare();

    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      campusVisits: [campusFixture({ allowedActions: ['VIEW', 'INITIATE_OPERATIONAL_CONTACT_TRANSFER'] })],
      viewer: {
        relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false,
        allowedActions: ['VIEW'],
      },
    }));
    const { unmount } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByTestId('contact-identity-actions-10')).toBeInTheDocument();
    unmount();

    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'HO', canViewAllCampuses: true, isReadOnly: true, allowedActions: ['VIEW'] },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-identity-actions-10')).not.toBeInTheDocument();
  });

  it('contact actions live INSIDE the contact section, not in a card of their own', async () => {
    // One business object, one card. The old standalone panel produced a second contact heading
    // above sections 1 and 2, which is what made the screen look like it repeated itself.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      campusVisits: [campusFixture({ allowedActions: ['VIEW', 'INITIATE_OPERATIONAL_CONTACT_TRANSFER'] })],
      viewer: {
        relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false,
        allowedActions: ['VIEW'],
      },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    const actions = await screen.findByTestId('contact-identity-actions-10');
    // The workflow lives INSIDE the campus card it acts on — a request-level contact section is
    // exactly what let one campus's contact acquire rights over its siblings.
    expect(screen.getByTestId('campus-detail-card-10')).toContainElement(actions);
  });

  it('active amendment: the decision panel is gated by per-instance allowedActions, not relation', async () => {
    // Staff Leader with APPROVE_AMENDMENT on this instance → decision panel.
    const withAmendment = formFixture({
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({
        activeAmendment: { amendmentId: 9, amendmentNo: 1, status: 'PENDING', requestedAt: '2026-07-21T08:00:00', changedFieldCount: 2 },
        allowedActions: ['APPROVE_AMENDMENT', 'REJECT_AMENDMENT'],
      })],
    });
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(withAmendment);
    vi.mocked(getActiveAmendment).mockResolvedValue({
      amendmentId: 9, visitRequestId: 1, visitInstanceId: 10, amendmentNo: 1, status: 'PENDING',
      baseFormRevision: 2, baseApprovalRevision: 1, requestedBy: 3, requestedByName: 'Đầu Mối',
      requestedAt: '2026-07-21T08:00:00', reason: null, decidedBy: null, decidedByName: null,
      decidedAt: null, decisionNote: null, expiresAt: null,
      changes: [{ fieldPath: 'instance.purpose', changeClass: 'APPROVAL_SENSITIVE', oldValueJson: '"A"', newValueJson: '"B"' }],
    });

    // English strings: src/test/setup.ts documents jsdom's navigator.language = en-US, so every
    // test in this suite renders EN by default — VisitAmendmentPanel is now fully i18n'd (was
    // 100% hardcoded Vietnamese before this session, immune to language; now correctly localized).
    const { unmount } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText(/Change proposal #1/)).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Approve & apply' })).toBeInTheDocument();
    unmount();

    // Read-only HO: no per-instance actions → no decision panel even though an amendment exists.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue({
      ...withAmendment,
      viewer: { relation: 'HO', canViewAllCampuses: true, isReadOnly: true, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({
        activeAmendment: { amendmentId: 9, amendmentNo: 1, status: 'PENDING', requestedAt: '2026-07-21T08:00:00', changedFieldCount: 2 },
        allowedActions: [],
      })],
    });
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve & apply' })).not.toBeInTheDocument();
  });

  it('mutation UI is driven ONLY by allowedActions (never relation/status)', async () => {
    // REGISTRANT on a PENDING request but backend granted NO edit action ANYWHERE → no edit link.
    // The campus list has to be cleared too: an instance-scoped safe edit is reachable on its own,
    // so a campus that still granted it would legitimately keep the button on screen.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      requestStatus: 'PENDING_APPROVAL',
      viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({ allowedActions: [] })],
    }));
    const { unmount } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Sửa|edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Quick edit' })).not.toBeInTheDocument();
    unmount();

    // Same viewer, backend grants EDIT_PENDING_REQUEST + SUBMIT_SAFE_EDIT → both surface.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: {
        relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false,
        allowedActions: ['VIEW', 'EDIT_PENDING_REQUEST', 'SUBMIT_SAFE_EDIT'],
      },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Quick edit' })).toBeInTheDocument();
  });

  it('labels the edit entry points as navigation, never as a save', async () => {
    // These are <Link>s to the edit form. "Save changes" belongs to the button that actually submits;
    // reusing it here promised an action that did not happen.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: {
        relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false,
        allowedActions: ['VIEW', 'EDIT_PENDING_REQUEST', 'RESUBMIT_REJECTED_REQUEST'],
      },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    expect(screen.getByRole('link', { name: 'Edit request' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Edit & resubmit' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Save changes' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Resubmit request' })).not.toBeInTheDocument();
  });

  it('overview states the request and its outcome without repeating sections 1 and 2', async () => {
    // Two visible campuses so the outcome summary has something to say — see the gating tests below
    // for the single-campus case, where the summary is deliberately absent.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      requestOutcome: { code: 'ALL_WAITING', total: 2, accepted: 0, inProgress: 0, waiting: 2, rejected: 0, cancelled: 0, closed: 0 },
      campusVisits: [campusFixture(), campusFixture({ visitInstanceId: 11, campusCode: 'HCM', campusName: 'FPTU Hồ Chí Minh' })],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    // The people appear exactly once each - in their own section, not also in the overview.
    expect(screen.getAllByText('Người Đăng Ký')).toHaveLength(1);
    // The amendment requester's name appears once, on the campus card that carries the amendment.
    // It used to appear twice: once there and once in a request-level contact block that no longer
    // exists, because a request has no single contact.
    expect(screen.queryAllByText('Đầu Mối').length).toBeLessThanOrEqual(1);
    // …and the overview now answers "where has this got to" instead.
    expect(screen.getByTestId('visit-outcome-summary')).toBeInTheDocument();
  });

  // ── "Tình trạng hiện tại" is gated on VISIBLE campus count, never on visitScope or role ──────
  // A single visible campus already has its state on the request badge, the campus count badge and
  // the campus card's own status badge — the summary would only repeat those. What decides "visible"
  // is exactly what the backend chose to put in campusVisits, so the gate reads that array's length
  // and nothing about who is looking or how big the underlying request actually is.

  it('single visible campus (an honest single-campus request): the outcome summary does not render', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    expect(screen.queryByTestId('visit-outcome-summary')).not.toBeInTheDocument();
  });

  it('MULTI_CAMPUS request scoped down to one visible campus (Staff Leader): the outcome summary does not render', async () => {
    // The request itself spans several campuses, but the backend scoped this Staff Leader to just
    // one of them. The gate must read campusVisits.length (1), not visitScope (MULTI_CAMPUS).
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      hasMixedCampusDetails: true,
      visitScope: 'MULTI_CAMPUS',
      requestOutcome: null,
      campusVisits: [campusFixture()],
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    expect(screen.queryByTestId('visit-outcome-summary')).not.toBeInTheDocument();
  });

  it('two visible campuses: the outcome summary renders', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      hasMixedCampusDetails: true,
      visitScope: 'MULTI_CAMPUS',
      requestOutcome: { code: 'MIXED', total: 2, accepted: 1, inProgress: 0, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
      campusVisits: [
        campusFixture({ instanceStatus: 'ASSIGNED' }),
        campusFixture({ visitInstanceId: 11, campusCode: 'HCM', campusName: 'FPTU Hồ Chí Minh', instanceStatus: 'WAITING_REQUEST_APPROVAL' }),
      ],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    expect(screen.getByTestId('visit-outcome-summary')).toBeInTheDocument();
  });

  it('HO with canViewAllCampuses and 2+ campuses: the outcome summary renders', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      visitScope: 'MULTI_CAMPUS',
      requestOutcome: { code: 'MIXED', total: 3, accepted: 1, inProgress: 1, waiting: 1, rejected: 0, cancelled: 0, closed: 0 },
      campusVisits: [
        campusFixture({ instanceStatus: 'ASSIGNED' }),
        campusFixture({ visitInstanceId: 11, campusCode: 'DN', campusName: 'FPTU Đà Nẵng', instanceStatus: 'DURING_VISIT' }),
        campusFixture({ visitInstanceId: 12, campusCode: 'HCM', campusName: 'FPTU Hồ Chí Minh', instanceStatus: 'WAITING_REQUEST_APPROVAL' }),
      ],
      viewer: { relation: 'HO', canViewAllCampuses: true, isReadOnly: true, allowedActions: ['VIEW'] },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    expect(screen.getByTestId('visit-outcome-summary')).toBeInTheDocument();
  });

  it('per-instance SUBMIT_AMENDMENT surfaces a propose-change entry point', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW', 'SUBMIT_SAFE_EDIT'] },
      campusVisits: [campusFixture({ allowedActions: ['SUBMIT_AMENDMENT'] })],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Propose change' })).toBeInTheDocument();
  });

  it('history timeline renders the server-scoped MASKED entries as business sentences', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    vi.mocked(getVisitRequestHistory).mockResolvedValue({
      visitRequestId: 1,
      requestCode: 'VR-2026-001',
      entries: [{
        at: '2026-07-16T10:00:00', eventCode: 'CONTACT_IDENTITY_CHANGED', eventId: null, visitInstanceId: null,
        campusName: null, actorName: null, formRevision: null, approvalRevision: null,
        amendmentNo: null, statusCode: 'CLAIM_APPLIED', sourceType: null, reason: null,
        maskedEmail: 'd***@x.vn', fromStatus: 'PENDING', toStatus: 'APPLIED',
      }],
    });
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByText('The contact role changed (d***@x.vn).')).toBeInTheDocument();
    // Masked means masked — the full address never appears anywhere in the DOM:
    expect(screen.queryByText(/dauMoi@|d@x\.vn/)).not.toBeInTheDocument();
    // …and the raw status transition never reaches the reader either.
    expect(screen.queryByText(/PENDING|APPLIED|CLAIM_APPLIED/)).not.toBeInTheDocument();
  });

  // ── There is no request-level confirmation roll-up any more ───────────────────────────────────
  // It counted campuses ("1/1", "còn N cơ sở chờ") immediately above the cards that name each
  // contact and show that contact's own state, so it repeated the section below it in every shape
  // it was ever rendered in. The per-campus workflow is untouched — only the summary is gone.

  it('never renders the request-level confirmation roll-up, in any scope', async () => {
    const cases: Array<[string, Partial<ResolvedVisitForm>]> = [
      ['one confirmed campus, instance-scoped', {
        confirmationSummary: { total: 1, confirmed: 1, pending: 0, declined: 0, expired: 0, gateOpen: true },
        campusVisits: [campusFixture({ instanceStatus: 'ASSIGNED' })],
        viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      }],
      ['full-request scope', {
        confirmationSummary: { total: 1, confirmed: 1, pending: 0, declined: 0, expired: 0, gateOpen: true },
        viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
      }],
      ['more than one campus', {
        confirmationSummary: { total: 2, confirmed: 2, pending: 0, declined: 0, expired: 0, gateOpen: true },
        campusVisits: [campusFixture(), campusFixture({ visitInstanceId: 11, campusCode: 'HCM' })],
        viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      }],
      // The shape that used to force it on: an answer still outstanding. This is the OPC-10 case.
      ['a contact has not answered', {
        requestStatus: 'PENDING_CONTACT_CONFIRMATION',
        confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },
        campusVisits: [campusFixture({
          instanceStatus: 'WAITING_CONTACT_CONFIRMATION',
          operationalContact: {
            fullName: 'Đầu Mối HN', organization: 'ĐH ABC', jobTitle: 'Trưởng phòng',
            phone: '+84912345678', email: 'dm@x.vn',
            confirmationStatus: 'PENDING', confirmationSource: null, confirmedAt: null,
          },
        })],
        viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      }],
    ];

    for (const [label, overrides] of cases) {
      vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture(overrides));
      const { unmount } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
      expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

      expect(screen.queryByTestId('section-contact-summary'), label).toBeNull();
      // …by test id AND by what the reader would actually look for.
      expect(screen.queryByText('Contact confirmation'), label).toBeNull();
      expect(screen.queryByText(/Xác nhận đầu mối đoàn khách/), label).toBeNull();
      // Every campus still carries its own contact block — the data was never the problem.
      const cards = screen.getAllByLabelText(/Campus detail/);
      expect(cards.length, label).toBe(overrides.campusVisits?.length ?? 1);
      for (const card of cards) {
        // On a multi-campus request the cards are an accordion, so a closed one has to be opened
        // before its contact block exists. The claim under test is that EVERY campus carries its own
        // contact — not that every campus renders it simultaneously.
        const toggle = within(card).queryByRole('button', { expanded: false });
        if (toggle) fireEvent.click(toggle);
        expect(within(card).getByText('Đầu Mối HN'), label).toBeInTheDocument();
      }
      unmount();
    }
  });

  it('a campus waiting for its contact shows that state on the card, not as "Unknown"', async () => {
    // The OPC-10 shape end to end: the request badge and the campus badge are two DIFFERENT enum
    // values, and neither was in the UI vocabulary before.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      requestStatus: 'PENDING_CONTACT_CONFIRMATION',
      confirmationSummary: { total: 1, confirmed: 0, pending: 1, declined: 0, expired: 0, gateOpen: false },
      campusVisits: [campusFixture({ instanceStatus: 'WAITING_CONTACT_CONFIRMATION' })],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.getByTestId('request-status')).toHaveTextContent('Awaiting delegation contact confirmation');
    expect(screen.getByTestId('campus-status-10')).toHaveTextContent('Awaiting delegation contact confirmation');
    expect(screen.queryByText('Unknown')).toBeNull();
  });

  // ── Change history is gated on its own capability, not on being able to open the page ──────
  //
  // A Staff/participant invited to support a campus can read this screen and may NOT read the change
  // history. The section used to mount for them regardless, so the endpoint's 403 arrived as
  // "The change history could not be loaded." with a Retry button — an error message for a decision,
  // and a retry for something that will never succeed.

  it('without VIEW_CHANGE_HISTORY the section is absent and the history API is never called', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: {
        relation: 'IC_SUPPORT', canViewAllCampuses: false, isReadOnly: false,
        allowedActions: ['VIEW'],
      },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    // The detail they were invited to still renders in full…
    expect(screen.getByTestId('section-campuses')).toBeInTheDocument();
    // …and the history section is not there at all — heading included.
    expect(screen.queryByTestId('section-history')).toBeNull();
    // No request means no 403, so nothing to mis-render and no noise against the endpoint.
    expect(getVisitRequestHistory).not.toHaveBeenCalled();
    expect(screen.queryByTestId('history-retry')).toBeNull();
  });

  it('with VIEW_CHANGE_HISTORY the section mounts and loads as before', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: {
        relation: 'HOST', canViewAllCampuses: false, isReadOnly: false,
        allowedActions: ['VIEW', 'VIEW_CHANGE_HISTORY'],
      },
    }));
    vi.mocked(getVisitRequestHistory).mockResolvedValue({
      visitRequestId: 1,
      requestCode: 'VR-2026-001',
      entries: [{
        at: '2026-07-16T10:00:00', eventCode: 'INSTANCE_APPROVED', eventId: null, visitInstanceId: 10,
        campusName: 'FPT University Hà Nội', actorName: 'Kim Min Jae', formRevision: null,
        approvalRevision: null, amendmentNo: null, statusCode: 'ASSIGNED', sourceType: null,
        reason: null, maskedEmail: null, fromStatus: null, toStatus: null,
      }],
    });
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    expect(screen.getByTestId('section-history')).toBeInTheDocument();
    await waitFor(() => expect(getVisitRequestHistory).toHaveBeenCalledWith(1));
    expect(await screen.findByTestId('visit-history-timeline')).toBeInTheDocument();
  });

  it('numbers the sections 1-2-3 with no gap where the roll-up used to be', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    // A page that ran 1, 3, 4 would read as if section 2 had failed to load.
    expect(within(screen.getByTestId('section-registrant')).getByText('1')).toBeInTheDocument();
    expect(within(screen.getByTestId('section-campuses')).getByText('2')).toBeInTheDocument();
    expect(within(screen.getByTestId('section-history')).getByText('3')).toBeInTheDocument();
  });

  // ── Ordinary campus decision (approve+assign-host / reject) — the gap this session closes: V2
  //    Detail used to have EditPendingCampus and amendment actions but no way at all to decide a
  //    WAITING campus, so a Staff Leader of this campus who opened Detail rather than List had
  //    nothing to click. EDIT right and DECISION right are different verdicts on purpose. ──────────

  it('offers APPROVE_AND_ASSIGN_HOST + CAMPUS_REJECT only to a WAITING campus\'s own Staff Leader', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({
        instanceStatus: 'WAITING_REQUEST_APPROVAL',
        allowedActions: ['VIEW', 'APPROVE_AND_ASSIGN_HOST', 'CAMPUS_REJECT'],
      })],
    }));
    const { unmount } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByTestId('campus-approve-open-10')).toBeInTheDocument();
    expect(screen.getByTestId('campus-reject-open-10')).toBeInTheDocument();
    unmount();

    // The registrant of this SAME campus, but not its leader — EDIT right (EDIT_PENDING_CAMPUS)
    // without DECISION right. Approving/rejecting stays the leader's alone.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({
        instanceStatus: 'WAITING_REQUEST_APPROVAL',
        allowedActions: ['VIEW', 'EDIT_PENDING_CAMPUS'],
      })],
    }));
    const { unmount: unmount2 } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByTestId('campus-approve-open-10')).not.toBeInTheDocument();
    expect(screen.queryByTestId('campus-reject-open-10')).not.toBeInTheDocument();
    unmount2();

    // The confirmed operational contact of this campus — guest side, never a decision actor.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'OPERATIONAL_CONTACT', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({ instanceStatus: 'WAITING_REQUEST_APPROVAL', allowedActions: ['VIEW'] })],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByTestId('campus-approve-open-10')).not.toBeInTheDocument();
    expect(screen.queryByTestId('campus-reject-open-10')).not.toBeInTheDocument();
  });

  it('a Staff Leader of a DIFFERENT campus who is this REQUEST\'s registrant edits here but does not decide', async () => {
    // The combined identity this gate exists for: Staff Leader of campus HCM, also the registrant of
    // a request naming campus HN, viewing HN (which they do not lead). Backend ResolveScopeAsync
    // resolves REGISTRANT first regardless of the Staff Leader role — VisitFormReadService.cs still
    // computes EditPendingCampus (true, requester-side) and ordinary decision actions (false,
    // isLeaderHere false for THIS campus) as two INDEPENDENT verdicts. This pins the read model's
    // real allowedActions shape for that exact case, not a hand-picked one.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({
        instanceStatus: 'WAITING_REQUEST_APPROVAL',
        allowedActions: ['VIEW', 'EDIT_PENDING_CAMPUS'],
        canOverrideScheduleLeadTime: false,
        canSaveAndApprove: false,
      })],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByTestId('pending-campus-edit-open-10')).toBeInTheDocument();
    expect(screen.queryByTestId('campus-approve-open-10')).not.toBeInTheDocument();
    expect(screen.queryByTestId('campus-reject-open-10')).not.toBeInTheDocument();
  });

  it('Approve reuses AssignHostModal end to end — same host-candidate load and approve command as List/Management', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({
        instanceStatus: 'WAITING_REQUEST_APPROVAL',
        allowedActions: ['VIEW', 'APPROVE_AND_ASSIGN_HOST', 'CAMPUS_REJECT'],
        rowVersion: 9,
      })],
    }));
    vi.mocked(delegationsApi.getHostCandidates).mockResolvedValue([
      { userId: 77, fullName: 'Host A', email: 'a@fpt.edu.vn', campusId: 1, departmentName: 'IC', subRole: null, hasScheduleConflict: false, conflictCount: 0, conflicts: [] },
    ] as never);
    vi.mocked(delegationsApi.approveCampusInstance).mockResolvedValue({} as never);

    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    fireEvent.click(await screen.findByTestId('campus-approve-open-10'));

    expect(await screen.findByText('Host A')).toBeInTheDocument();
    fireEvent.click(screen.getByText('Host A'));
    fireEvent.click(screen.getByRole('button', { name: 'Duyệt & phân công người phụ trách' }));

    await waitFor(() => expect(delegationsApi.approveCampusInstance)
      .toHaveBeenCalledWith(1, 10, 77, '', 9));
  });

  it('Reject reuses rejectCampusInstance and keeps the mandatory-reason contract', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'STAFF_LEADER', canViewAllCampuses: false, isReadOnly: false, allowedActions: ['VIEW'] },
      campusVisits: [campusFixture({
        instanceStatus: 'WAITING_REQUEST_APPROVAL',
        allowedActions: ['VIEW', 'APPROVE_AND_ASSIGN_HOST', 'CAMPUS_REJECT'],
        rowVersion: 9,
      })],
    }));
    vi.mocked(delegationsApi.rejectCampusInstance).mockResolvedValue({} as never);

    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    fireEvent.click(await screen.findByTestId('campus-reject-open-10'));

    // Empty reason never reaches the API — the button is disabled until one is typed.
    expect(await screen.findByTestId('campus-reject-confirm')).toBeDisabled();

    fireEvent.change(screen.getByTestId('campus-reject-reason'), { target: { value: 'Không đủ điều kiện tiếp nhận' } });
    fireEvent.click(screen.getByTestId('campus-reject-confirm'));

    await waitFor(() => expect(delegationsApi.rejectCampusInstance)
      .toHaveBeenCalledWith(1, 10, 'Không đủ điều kiện tiếp nhận', 9));
  });

  it('flag OFF / not found → stable friendly message, no silent v1 fallback fetch', async () => {
    vi.mocked(getVisitRequestFormV2).mockRejectedValue(axios404);
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={99} /></MemoryRouter>);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Request not found or the per-campus feature is not enabled.',
    );
    expect(getVisitRequestFormV2).toHaveBeenCalledTimes(1);
  });
});
