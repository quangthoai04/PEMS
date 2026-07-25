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
  initiateContactTransfer,
} from '../api/visitRequestV2Api';

const noTransfer = {
  visitRequestId: 1, hasPendingTransfer: false, identityChangeId: null,
  status: null, newEmailMasked: null, expiresAt: null, resendCount: 0,
};

/** The action sets the backend emits for the states this panel can be in. */
const CLAIM_ACTIONS = ['VIEW', 'RESEND_CONTACT_CLAIM', 'REPLACE_PENDING_CONTACT'];
const ACTIVE_ACTIONS = ['VIEW', 'INITIATE_CONTACT_TRANSFER'];
const PENDING_TRANSFER_ACTIONS = ['VIEW', 'RESEND_CONTACT_TRANSFER', 'CANCEL_CONTACT_TRANSFER'];

const renderActions = (props: Partial<React.ComponentProps<typeof ContactIdentityActions>> = {}) =>
  render(
    <ContactIdentityActions
      visitRequestId={1}
      primaryContactAccessStatus="ACTIVE"
      contactEmail="owner@example.com"
      allowedActions={ACTIVE_ACTIONS}
      {...props}
    />,
  );

const fillTransferForm = () => {
  fireEvent.change(screen.getByLabelText(/Full name|Họ (và )?tên/i), { target: { value: 'Trần B' } });
  fireEvent.change(screen.getByLabelText(/Phone|Số điện thoại/i), { target: { value: '+84900000000' } });
  fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'new.owner@example.com' } });
};

describe('ContactIdentityActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getActiveContactTransfer).mockResolvedValue(noTransfer);
  });

  // ── Permission comes from the backend, never from the relation ─────────────

  it('renders nothing when the backend granted no contact action', () => {
    const { container } = renderActions({ allowedActions: ['VIEW'] });
    expect(container).toBeEmptyDOMElement();
    expect(getActiveContactTransfer).not.toHaveBeenCalled();
  });

  it('renders nothing for a viewer with no actions at all (HO, host, scoped leader)', () => {
    const { container } = renderActions({ allowedActions: undefined });
    expect(container).toBeEmptyDOMElement();
  });

  it('shows exactly the buttons whose action codes were granted', async () => {
    const { unmount } = renderActions({
      primaryContactAccessStatus: 'PENDING_CONFIRMATION', allowedActions: CLAIM_ACTIONS,
    });
    expect(screen.getByTestId('contact-resend-claim')).toBeInTheDocument();
    expect(screen.getByTestId('contact-replace-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-transfer-open')).not.toBeInTheDocument();
    unmount();

    renderActions({ allowedActions: ACTIVE_ACTIONS });
    expect(await screen.findByTestId('contact-transfer-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
  });

  it('drops the resend button when the backend withheld it at the cap', () => {
    // The handler answers CLAIM_RESEND_LIMIT past five sends, so the code is simply absent.
    renderActions({
      primaryContactAccessStatus: 'PENDING_CONFIRMATION',
      allowedActions: ['VIEW', 'REPLACE_PENDING_CONTACT'],
    });
    expect(screen.getByTestId('contact-replace-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
  });

  it('never offers a second transfer while one is pending', async () => {
    vi.mocked(getActiveContactTransfer).mockResolvedValue({
      ...noTransfer, hasPendingTransfer: true, newEmailMasked: 'n***@x.vn', expiresAt: '2026-08-01T09:00:00',
    });
    renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

    expect(await screen.findByTestId('contact-cancel-transfer')).toBeInTheDocument();
    expect(screen.getByTestId('contact-resend-transfer')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-transfer-open')).not.toBeInTheDocument();
  });

  // ── Layout: the form is an inline two-column block, not five stacked rows ──

  it('keeps the transfer form closed until it is asked for', async () => {
    renderActions();
    await screen.findByTestId('contact-transfer-open');
    expect(screen.queryByTestId('contact-form-transfer')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('contact-transfer-open'));
    expect(screen.getByTestId('contact-form-transfer')).toBeInTheDocument();
  });

  it('lays the fields out in two columns on desktop and one on mobile', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-transfer-open'));

    const grid = screen.getByTestId('contact-form-grid');
    expect(grid.className).toContain('grid-cols-1');   // mobile: one per row, no overflow
    expect(grid.className).toContain('md:grid-cols-2'); // desktop/tablet: two per row

    // The reason is prose — it spans the whole width instead of sitting in a half-width box.
    expect(screen.getByTestId('contact-form-reason').className).toContain('md:col-span-2');
  });

  it('closes the form on cancel without touching the contact', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-transfer-open'));
    fillTransferForm();

    fireEvent.click(screen.getByTestId('contact-form-cancel'));

    expect(screen.queryByTestId('contact-form-transfer')).not.toBeInTheDocument();
    expect(initiateContactTransfer).not.toHaveBeenCalled();
    // Reopening starts clean — the abandoned draft of this form is not carried over.
    fireEvent.click(screen.getByTestId('contact-transfer-open'));
    expect((screen.getByLabelText(/Email/i) as HTMLInputElement).value).toBe('');
  });

  // ── Submission ────────────────────────────────────────────────────────────

  it('refuses a transfer to the address that already holds the role, inline on the field', async () => {
    renderActions({ contactEmail: 'Owner@Example.com' });
    fireEvent.click(await screen.findByTestId('contact-transfer-open'));
    fillTransferForm();
    // Same mailbox, different case — the backend answers EMAIL_UNCHANGED.
    fireEvent.change(screen.getByLabelText(/Email/i), { target: { value: 'owner@example.com ' } });

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    expect(initiateContactTransfer).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toBeInTheDocument();   // inline, next to the field
    expect(showErrorToast).not.toHaveBeenCalled();            // a field problem is not a toast
  });

  it('cannot be submitted twice while the first invitation is in flight', async () => {
    let resolveIt: (v: { message: string }) => void = () => {};
    vi.mocked(initiateContactTransfer).mockReturnValue(
      new Promise(res => { resolveIt = res as (v: { message: string }) => void; }) as never,
    );
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-transfer-open'));
    fillTransferForm();

    const submit = screen.getByTestId('contact-form-submit');
    fireEvent.click(submit);
    fireEvent.click(submit);
    fireEvent.click(submit);

    expect(initiateContactTransfer).toHaveBeenCalledTimes(1);
    expect(submit).toBeDisabled();

    resolveIt({ message: 'Đã gửi lời mời chuyển giao.' });
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã gửi lời mời chuyển giao.'));
  });

  it('reports a successful mutation through the shared toast, not an inline message', async () => {
    vi.mocked(resendContactClaim).mockResolvedValue({
      visitRequestId: 1, primaryContactAccessStatus: 'PENDING_CONFIRMATION',
      claimStatus: 'PENDING', resendCount: 1, message: 'Đã gửi lại lời mời.',
    });
    const onChanged = vi.fn();
    renderActions({
      primaryContactAccessStatus: 'PENDING_CONFIRMATION', allowedActions: CLAIM_ACTIONS, onChanged,
    });

    fireEvent.click(screen.getByTestId('contact-resend-claim'));

    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã gửi lại lời mời.'));
    expect(onChanged).toHaveBeenCalled();
  });

  it('reports a failed mutation through the shared error toast', async () => {
    vi.mocked(resendContactClaim).mockRejectedValue(new Error('boom'));
    renderActions({ primaryContactAccessStatus: 'PENDING_CONFIRMATION', allowedActions: CLAIM_ACTIONS });

    fireEvent.click(screen.getByTestId('contact-resend-claim'));

    await waitFor(() => expect(showErrorToast).toHaveBeenCalled());
    expect(showSuccessToast).not.toHaveBeenCalled();
  });

  it('surfaces a transfer-state load failure with a retry instead of claiming there is none', async () => {
    // Swallowing this into "no pending transfer" invited the user to start a SECOND transfer while
    // one was already in flight.
    vi.mocked(getActiveContactTransfer).mockRejectedValueOnce(new Error('network'));
    renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

    const retry = await screen.findByTestId('contact-transfer-retry');
    expect(screen.getByRole('alert')).toBeInTheDocument();

    vi.mocked(getActiveContactTransfer).mockResolvedValueOnce({
      ...noTransfer, hasPendingTransfer: true, newEmailMasked: 'n***@x.vn',
    });
    fireEvent.click(retry);

    await waitFor(() => expect(screen.queryByTestId('contact-transfer-retry')).not.toBeInTheDocument());
    expect(screen.getByTestId('contact-cancel-transfer')).toBeInTheDocument();
  });

  it('cancels a pending transfer through the shared toast', async () => {
    vi.mocked(getActiveContactTransfer).mockResolvedValue({
      ...noTransfer, hasPendingTransfer: true, newEmailMasked: 'n***@x.vn', expiresAt: '2026-08-01T09:00:00',
    });
    vi.mocked(cancelContactTransfer).mockResolvedValue({
      visitRequestId: 1, transferStatus: 'CANCELLED', newEmailMasked: null,
      expiresAt: null, resendCount: 0, message: 'Đã hủy lời mời chuyển giao.',
    });
    renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

    fireEvent.click(await screen.findByTestId('contact-cancel-transfer'));
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã hủy lời mời chuyển giao.'));
  });
});
