import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, within } from '@testing-library/react';
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

import {
  getVisitRequestFormV2,
  getActiveAmendment,
  getVisitRequestHistory,
} from '../api/visitRequestV2Api';

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
  viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
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
    // Each card shows ITS campus content — HCM never inherits HN's delegation name:
    expect(screen.getByText('Đoàn ĐH ABC')).toBeInTheDocument();
    expect(screen.getByText('Đoàn HCM khác hẳn')).toBeInTheDocument();
    expect(screen.getByText('Host HCM')).toBeInTheDocument();
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

  it('contact actions follow the backend action codes, not the viewer relation', async () => {
    // A REGISTRANT with no contact action code granted (for instance inside the 24h window, or with
    // the request already cancelled) must see NOTHING — the relation alone never earns a button.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    const { unmount: unmountBare } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-identity-actions-10')).not.toBeInTheDocument();
    unmountBare();

    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      campusVisits: [campusFixture({ allowedActions: ['VIEW', 'INITIATE_CONTACT_TRANSFER'] })],
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
      campusVisits: [campusFixture({ allowedActions: ['VIEW', 'INITIATE_CONTACT_TRANSFER'] })],
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

    const { unmount } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText(/Đề xuất thay đổi #1/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Duyệt & áp dụng' })).toBeInTheDocument();
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
    expect(screen.queryByRole('button', { name: 'Duyệt & áp dụng' })).not.toBeInTheDocument();
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
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
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

  it('numbers the sections 1-2-3 with no gap where the roll-up used to be', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();

    // A page that ran 1, 3, 4 would read as if section 2 had failed to load.
    expect(within(screen.getByTestId('section-registrant')).getByText('1')).toBeInTheDocument();
    expect(within(screen.getByTestId('section-campuses')).getByText('2')).toBeInTheDocument();
    expect(within(screen.getByTestId('section-history')).getByText('3')).toBeInTheDocument();
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
