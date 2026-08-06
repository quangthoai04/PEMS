import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

// The v2 detail view and the flat info panel are replaced with markers so the test asserts ONLY which
// branch the modal chose (flat vs per-campus v2), never their internals.
vi.mock('../../../features/visit-request/components/v2/VisitRequestV2DetailView', () => ({
  default: ({ visitRequestId }: { visitRequestId: number }) => (
    <div data-testid="v2-detail">v2:{visitRequestId}</div>
  ),
}));
vi.mock('../../../features/delegations/components/SubmittedVisitRequestInfoPanel', () => ({
  SubmittedVisitRequestInfoPanel: () => <div data-testid="v1-panel">flat</div>,
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
    delegationName: 'Đoàn', registrant: {} as never,
    campuses: [], guestMembers: [], externalSupportMembers: [],
    canApprove: false, canReject: false, canCancel: false,
    ...overrides,
  }) as SubmittedVisitRequestFormDetail;

const mockFetch = vi.mocked(delegationsApi.getSubmittedVisitRequestFormDetail);

// Pure V2: the modal chooses its shape from the backend's answer alone — a flat projection for a
// uniform request, or a stable upgrade-required 409 for a mixed one. There is no form-version field
// and no version prop; a mixed request cannot be represented flat, so the backend signals it by status.
describe('SubmittedVisitRequestDetailModal', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders the flat panel when the backend returns a flat projection (uniform request)', async () => {
    mockFetch.mockResolvedValue(flat());
    render(<SubmittedVisitRequestDetailModal isOpen visitRequestId={7} onClose={() => {}} />);
    expect(await screen.findByTestId('v1-panel')).toBeInTheDocument();
    expect(screen.queryByTestId('v2-detail')).toBeNull();
  });

  it('renders the per-campus v2 detail on a stable upgrade-required 409 (mixed request)', async () => {
    const err = Object.assign(new Error('conflict'), {
      isAxiosError: true,
      response: { status: 409, data: { errorCode: 'FORM_VERSION_UPGRADE_REQUIRED' } },
    });
    mockFetch.mockRejectedValue(err);
    render(<SubmittedVisitRequestDetailModal isOpen visitRequestId={7} onClose={() => {}} />);
    expect(await screen.findByTestId('v2-detail')).toBeInTheDocument();
    expect(screen.queryByTestId('v1-panel')).toBeNull();
  });
});
