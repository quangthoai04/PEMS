import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
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
  resendContactClaim: vi.fn(),
  replacePendingContact: vi.fn(),
  initiateContactTransfer: vi.fn(),
  resendContactTransfer: vi.fn(),
  cancelContactTransfer: vi.fn(),
  approveAmendment: vi.fn(),
  rejectAmendment: vi.fn(),
  withdrawAmendment: vi.fn(),
}));

import {
  getVisitRequestFormV2,
  getActiveAmendment,
  getVisitRequestHistory,
} from '../api/visitRequestV2Api';

const formFixture = (overrides: Partial<ResolvedVisitForm> = {}): ResolvedVisitForm => ({
  visitRequestId: 1,
  requestCode: 'VR-2026-001',
  rowVersion: 0,
  formSchemaVersion: 2,
  hasMixedCampusDetails: false,
  visitScope: 'SINGLE_CAMPUS',
  requestStatus: 'PENDING_APPROVAL',
  createdSource: 'PUBLIC',
  submittedAt: '2026-07-15T08:00:00',
  partnerId: null,
  registrant: {
    fullName: 'Người Đăng Ký', organization: 'ĐH ABC', jobTitle: 'TP',
    phone: '+84912345678', email: 'reg@x.vn', nationality: 'VN',
  },
  primaryContact: {
    fullName: 'Đầu Mối', organization: 'ĐH ABC', phone: '+84987654321',
    email: 'd***@x.vn', accessStatus: 'ACTIVE', verifiedAt: '2026-07-15T09:00:00',
  },
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
          currentHostName: 'Host HCM', instanceStatus: 'PENDING',
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

  it('identity panel is wired for the request manager and ABSENT for read-only HO', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    const { unmount } = render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    // ContactIdentityPanel (G-1, VI aria-label) appears for REGISTRANT:
    expect(await screen.findByLabelText('Quản lý đầu mối liên hệ')).toBeInTheDocument();
    unmount();

    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'HO', canViewAllCampuses: true, isReadOnly: true, allowedActions: ['VIEW'] },
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.queryByLabelText('Quản lý đầu mối liên hệ')).not.toBeInTheDocument();
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
    // REGISTRANT on a PENDING request but backend granted NO edit action → no edit link.
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      requestStatus: 'PENDING_APPROVAL',
      viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW'] },
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

  it('per-instance SUBMIT_AMENDMENT surfaces a propose-change entry point', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture({
      viewer: { relation: 'REGISTRANT', canViewAllCampuses: true, isReadOnly: false, allowedActions: ['VIEW', 'SUBMIT_SAFE_EDIT'] },
      campusVisits: [campusFixture({ allowedActions: ['SUBMIT_AMENDMENT'] })],
    }));
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);
    expect(await screen.findByText('VR-2026-001')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Propose change' })).toBeInTheDocument();
  });

  it('history timeline renders the server-scoped MASKED entries as-is', async () => {
    vi.mocked(getVisitRequestFormV2).mockResolvedValue(formFixture());
    vi.mocked(getVisitRequestHistory).mockResolvedValue({
      visitRequestId: 1,
      requestCode: 'VR-2026-001',
      entries: [{
        at: '2026-07-16T10:00:00', kind: 'IDENTITY', visitInstanceId: null,
        title: 'Đầu mối d***@x.vn đã xác nhận vai trò', detail: null, actorName: null,
      }],
    });
    render(<MemoryRouter><VisitRequestV2DetailView visitRequestId={1} /></MemoryRouter>);

    expect(await screen.findByText('Đầu mối d***@x.vn đã xác nhận vai trò')).toBeInTheDocument();
    // Masked means masked — the full address never appears anywhere in the DOM:
    expect(screen.queryByText(/dauMoi@|d@x\.vn/)).not.toBeInTheDocument();
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
