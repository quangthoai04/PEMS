import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({
  transferVisitHost: vi.fn(),
}));

vi.mock('../../delegations/api/delegationsApi', () => ({
  delegationsApi: { getHostCandidates: vi.fn() },
}));

const showSuccessToast = vi.fn();
const showErrorToast = vi.fn();
vi.mock('../../../shared/utils/toast', () => ({
  showSuccessToast: (...a: unknown[]) => showSuccessToast(...a),
  showErrorToast: (...a: unknown[]) => showErrorToast(...a),
}));

import VisitHostTransferModal, { type HostTransferTarget } from '../components/VisitHostTransferModal';
import { transferVisitHost } from '../api/visitRequestV2Api';
import { delegationsApi } from '../../delegations/api/delegationsApi';
import type { HostCandidate } from '../../delegations/types/delegations.types';

const campus: HostTransferTarget = {
  visitInstanceId: 10,
  campusName: 'FPTU Hà Nội',
  currentHostUserId: 5,
  currentHostName: 'Host Hiện Tại',
  plannedStartAt: '2026-09-01T09:00:00',
  rowVersion: 3,
};

const candidate: HostCandidate = {
  userId: 101, fullName: 'Ứng Viên Một', email: 'uv1@fpt.edu.vn', campusId: 1,
  departmentName: 'IC', subRole: null, roleLabel: 'IC Staff', isSelf: false,
  isStaffLeaderSelfHostOption: false, hasScheduleConflict: false, conflictCount: 0, conflicts: [],
};

// PEMS_VALIDATION_UX §3: the submit button no longer plays dead while required fields are missing —
// it stays clickable and Submit itself judges (and says) what's wrong, so there's always a reason on
// screen instead of a control that silently refuses input.
describe('VisitHostTransferModal — validation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(delegationsApi.getHostCandidates).mockResolvedValue([candidate]);
  });

  it('does not spam errors while the modal has just opened', async () => {
    render(<VisitHostTransferModal campus={campus} onClose={() => {}} onTransferred={() => {}} />);
    await screen.findByTestId(`host-transfer-candidate-${candidate.userId}`);

    expect(screen.queryByTestId('host-transfer-error-host')).toBeNull();
    expect(screen.queryByTestId('host-transfer-error-reason')).toBeNull();
  });

  it('clicking Submit with no host picked blocks the API and names the problem', async () => {
    render(<VisitHostTransferModal campus={campus} onClose={() => {}} onTransferred={() => {}} />);
    await screen.findByTestId(`host-transfer-candidate-${candidate.userId}`);
    fireEvent.change(screen.getByTestId('host-transfer-reason'), { target: { value: 'Lý do' } });

    fireEvent.click(screen.getByTestId('host-transfer-submit'));

    expect(transferVisitHost).not.toHaveBeenCalled();
    expect(screen.getByTestId('host-transfer-error-host')).toHaveTextContent(/select a new reception owner/i);
  });

  it('clicking Submit with no reason blocks the API and highlights Reason', async () => {
    render(<VisitHostTransferModal campus={campus} onClose={() => {}} onTransferred={() => {}} />);
    fireEvent.click(await screen.findByTestId(`host-transfer-candidate-${candidate.userId}`));

    fireEvent.click(screen.getByTestId('host-transfer-submit'));

    expect(transferVisitHost).not.toHaveBeenCalled();
    const reasonField = screen.getByTestId('host-transfer-reason');
    expect(reasonField).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByTestId('host-transfer-error-reason')).toHaveTextContent(/enter a reason/i);
  });

  it('clears the reason error the instant text is typed, without a second Submit', async () => {
    render(<VisitHostTransferModal campus={campus} onClose={() => {}} onTransferred={() => {}} />);
    fireEvent.click(await screen.findByTestId(`host-transfer-candidate-${candidate.userId}`));
    fireEvent.click(screen.getByTestId('host-transfer-submit'));
    expect(screen.getByTestId('host-transfer-error-reason')).toBeInTheDocument();

    fireEvent.change(screen.getByTestId('host-transfer-reason'), { target: { value: 'Đã có lý do' } });
    expect(screen.queryByTestId('host-transfer-error-reason')).toBeNull();
  });

  it('clears the host-selection error the instant a candidate is picked', async () => {
    render(<VisitHostTransferModal campus={campus} onClose={() => {}} onTransferred={() => {}} />);
    await screen.findByTestId(`host-transfer-candidate-${candidate.userId}`);
    fireEvent.click(screen.getByTestId('host-transfer-submit'));
    expect(screen.getByTestId('host-transfer-error-host')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId(`host-transfer-candidate-${candidate.userId}`));
    expect(screen.queryByTestId('host-transfer-error-host')).toBeNull();
  });

  it('submits exactly once with a valid host and reason', async () => {
    vi.mocked(transferVisitHost).mockResolvedValue({ message: 'Đã chuyển người phụ trách.' } as never);
    const onTransferred = vi.fn();
    render(<VisitHostTransferModal campus={campus} onClose={() => {}} onTransferred={onTransferred} />);
    fireEvent.click(await screen.findByTestId(`host-transfer-candidate-${candidate.userId}`));
    fireEvent.change(screen.getByTestId('host-transfer-reason'), { target: { value: 'Người cũ bận việc.' } });

    fireEvent.click(screen.getByTestId('host-transfer-submit'));

    await waitFor(() => expect(transferVisitHost).toHaveBeenCalledTimes(1));
    expect(transferVisitHost).toHaveBeenCalledWith(10, {
      newHostUserId: candidate.userId, reason: 'Người cũ bận việc.', expectedRowVersion: 3,
    });
    expect(onTransferred).toHaveBeenCalled();
  });

  it('still shows the dedicated 409 conflict message, unchanged by this fix', async () => {
    vi.mocked(transferVisitHost).mockRejectedValue({ response: { status: 409 } });
    render(<VisitHostTransferModal campus={campus} onClose={() => {}} onTransferred={() => {}} />);
    fireEvent.click(await screen.findByTestId(`host-transfer-candidate-${candidate.userId}`));
    fireEvent.change(screen.getByTestId('host-transfer-reason'), { target: { value: 'Lý do' } });

    fireEvent.click(screen.getByTestId('host-transfer-submit'));

    expect(await screen.findByRole('alert')).toHaveTextContent(/just changed by another action/i);
  });
});
