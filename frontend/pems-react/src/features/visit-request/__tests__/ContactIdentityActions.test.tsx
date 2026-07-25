import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({
  getActiveContactTransfer: vi.fn(),
  resendContactClaim: vi.fn(),
  replacePendingContact: vi.fn(),
  initiateContactTransfer: vi.fn(),
  resendContactTransfer: vi.fn(),
  cancelContactTransfer: vi.fn(),
}));

const showSuccessToast = vi.fn();
const showErrorToast = vi.fn();
vi.mock('../../../shared/utils/toast', () => ({
  showSuccessToast: (...a: unknown[]) => showSuccessToast(...a),
  showErrorToast: (...a: unknown[]) => showErrorToast(...a),
  getApiErrorMessage: (_e: unknown, fallback: string) => fallback,
}));

import ContactIdentityActions from '../components/ContactIdentityActions';
import {
  getActiveContactTransfer,
  resendContactClaim,
  cancelContactTransfer,
} from '../api/visitRequestV2Api';

const noTransfer = {
  visitRequestId: 1, hasPendingTransfer: false, identityChangeId: null,
  status: null, newEmailMasked: null, expiresAt: null, resendCount: 0,
};

const renderActions = (props: Partial<React.ComponentProps<typeof ContactIdentityActions>> = {}) =>
  render(
    <ContactIdentityActions
      visitRequestId={1}
      primaryContactAccessStatus="ACTIVE"
      contactEmailMasked="d***@x.vn"
      canManage
      {...props}
    />,
  );

describe('ContactIdentityActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getActiveContactTransfer).mockResolvedValue(noTransfer);
  });

  it('renders nothing at all when the caller may not manage the contact', () => {
    const { container } = renderActions({ canManage: false });
    expect(container).toBeEmptyDOMElement();
    expect(getActiveContactTransfer).not.toHaveBeenCalled();
  });

  it('reports a successful mutation through the shared toast, not an inline message', async () => {
    vi.mocked(resendContactClaim).mockResolvedValue({
      visitRequestId: 1, primaryContactAccessStatus: 'PENDING_CONFIRMATION',
      claimStatus: 'PENDING', resendCount: 1, message: 'Đã gửi lại lời mời.',
    });
    const onChanged = vi.fn();
    renderActions({ primaryContactAccessStatus: 'PENDING_CONFIRMATION', onChanged });

    fireEvent.click(screen.getByTestId('contact-resend-claim'));

    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã gửi lại lời mời.'));
    expect(onChanged).toHaveBeenCalled();
  });

  it('reports a failed mutation through the shared error toast', async () => {
    vi.mocked(resendContactClaim).mockRejectedValue(new Error('boom'));
    renderActions({ primaryContactAccessStatus: 'PENDING_CONFIRMATION' });

    fireEvent.click(screen.getByTestId('contact-resend-claim'));

    await waitFor(() => expect(showErrorToast).toHaveBeenCalled());
    expect(showSuccessToast).not.toHaveBeenCalled();
  });

  it('surfaces a transfer-state load failure with a retry instead of claiming there is none', async () => {
    // Swallowing this into "no pending transfer" invited the user to start a SECOND transfer while
    // one was already in flight.
    vi.mocked(getActiveContactTransfer).mockRejectedValueOnce(new Error('network'));
    renderActions();

    const retry = await screen.findByTestId('contact-transfer-retry');
    expect(screen.getByRole('alert')).toBeInTheDocument();

    vi.mocked(getActiveContactTransfer).mockResolvedValueOnce({ ...noTransfer, hasPendingTransfer: true, newEmailMasked: 'n***@x.vn' });
    fireEvent.click(retry);

    await waitFor(() => expect(screen.queryByTestId('contact-transfer-retry')).not.toBeInTheDocument());
    expect(screen.getByTestId('contact-cancel-transfer')).toBeInTheDocument();
  });

  it('offers claim actions while pending and transfer actions once active', async () => {
    const { unmount } = renderActions({ primaryContactAccessStatus: 'PENDING_CONFIRMATION' });
    expect(screen.getByTestId('contact-resend-claim')).toBeInTheDocument();
    expect(screen.getByTestId('contact-replace-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-transfer-open')).not.toBeInTheDocument();
    unmount();

    renderActions({ primaryContactAccessStatus: 'ACTIVE' });
    expect(await screen.findByTestId('contact-transfer-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
  });

  it('cancels a pending transfer through the shared toast', async () => {
    vi.mocked(getActiveContactTransfer).mockResolvedValue({
      ...noTransfer, hasPendingTransfer: true, newEmailMasked: 'n***@x.vn', expiresAt: '2026-08-01T09:00:00',
    });
    vi.mocked(cancelContactTransfer).mockResolvedValue({
      visitRequestId: 1, transferStatus: 'CANCELLED', newEmailMasked: null,
      expiresAt: null, resendCount: 0, message: 'Đã hủy lời mời chuyển giao.',
    });
    renderActions();

    fireEvent.click(await screen.findByTestId('contact-cancel-transfer'));
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã hủy lời mời chuyển giao.'));
  });
});
