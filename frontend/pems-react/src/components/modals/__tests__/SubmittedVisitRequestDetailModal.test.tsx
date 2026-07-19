import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

// The v2 detail view and the flat info panel are replaced with markers so the test asserts ONLY which
// branch the modal chose (v1 flat vs v2 per-campus), never their internals.
vi.mock('../../../features/visit-request/components/v2/VisitRequestV2DetailView', () => ({
  default: ({ visitRequestId }: { visitRequestId: number }) => (
    <div data-testid="v2-detail">v2:{visitRequestId}</div>
  ),
}));
vi.mock('../../../features/delegations/components/SubmittedVisitRequestInfoPanel', () => ({
  SubmittedVisitRequestInfoPanel: () => <div data-testid="v1-panel">v1 flat</div>,
}));
vi.mock('../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: { getSubmittedVisitRequestFormDetail: vi.fn() },
}));

import { SubmittedVisitRequestDetailModal } from '../SubmittedVisitRequestDetailModal';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { SubmittedVisitRequestFormDetail } from '../../../features/delegations/types/delegations.types';

const flat = (overrides: Partial<SubmittedVisitRequestFormDetail> = {}): SubmittedVisitRequestFormDetail =>
  ({
    visitRequestId: 7, requestCode: 'VR-7', requestStatus: 'PENDING_APPROVAL', visitScope: 'SINGLE_CAMPUS',
    delegationName: 'Đoàn', registrant: {} as never, contactPerson: {} as never,
    campuses: [], guestMembers: [], externalSupportMembers: [],
    canApprove: false, canReject: false, canCancel: false,
    ...overrides,
  }) as SubmittedVisitRequestFormDetail;

const mockFetch = vi.mocked(delegationsApi.getSubmittedVisitRequestFormDetail);

describe('SubmittedVisitRequestDetailModal — version-aware branch', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders the flat v1 panel for a v1 request', async () => {
    mockFetch.mockResolvedValue(flat({ formSchemaVersion: 1 }));
    render(<SubmittedVisitRequestDetailModal isOpen visitRequestId={7} onClose={() => {}} />);
    expect(await screen.findByTestId('v1-panel')).toBeInTheDocument();
    expect(screen.queryByTestId('v2-detail')).toBeNull();
  });

  it('renders the v2 detail for a UNIFORM v2 request (flat-looking, version=2)', async () => {
    mockFetch.mockResolvedValue(flat({ formSchemaVersion: 2 }));
    render(<SubmittedVisitRequestDetailModal isOpen visitRequestId={7} onClose={() => {}} />);
    expect(await screen.findByTestId('v2-detail')).toBeInTheDocument();
    expect(screen.queryByTestId('v1-panel')).toBeNull();
  });

  it('treats a missing version as legacy v1 (fail-safe)', async () => {
    mockFetch.mockResolvedValue(flat({ formSchemaVersion: undefined }));
    render(<SubmittedVisitRequestDetailModal isOpen visitRequestId={7} onClose={() => {}} />);
    expect(await screen.findByTestId('v1-panel')).toBeInTheDocument();
    expect(screen.queryByTestId('v2-detail')).toBeNull();
  });

  it('routes a v1 upgrade-required 409 to the v2 detail (mixed v2), not a raw error', async () => {
    const err = Object.assign(new Error('conflict'), {
      isAxiosError: true,
      response: { status: 409, data: { errorCode: 'FORM_VERSION_UPGRADE_REQUIRED' } },
    });
    mockFetch.mockRejectedValue(err);
    render(<SubmittedVisitRequestDetailModal isOpen visitRequestId={7} onClose={() => {}} />);
    expect(await screen.findByTestId('v2-detail')).toBeInTheDocument();
    expect(screen.queryByTestId('v1-panel')).toBeNull();
  });

  it('opens the v2 detail immediately when the caller passes formSchemaVersion=2 (no flat fetch)', async () => {
    render(<SubmittedVisitRequestDetailModal isOpen visitRequestId={7} formSchemaVersion={2} onClose={() => {}} />);
    expect(await screen.findByTestId('v2-detail')).toBeInTheDocument();
    await waitFor(() => expect(mockFetch).not.toHaveBeenCalled());
  });
});
