import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({
  getOperationalContactState: vi.fn(),
  resendOperationalContactConfirmation: vi.fn(),
  saveOperationalContact: vi.fn(),
  cancelOperationalContactChange: vi.fn(),
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
  getOperationalContactState,
  resendOperationalContactConfirmation,
  cancelOperationalContactChange,
  saveOperationalContact,
} from '../api/visitRequestV2Api';

/** A campus whose contact is settled and has no invitation in flight. */
const noPending = {
  visitRequestId: 1, visitInstanceId: 10, campusStatus: 'WAITING_REQUEST_APPROVAL',
  contactConfirmed: true, confirmedEmailMasked: 'o***@example.com', confirmedAt: '2026-08-01T09:00:00',
  confirmationSource: 'EMAIL_CONFIRMATION',
  pendingChangeKind: null, pendingChangeStatus: null, pendingEmailMasked: null,
  expiresAt: null, resendCount: 0, tokenVersion: 1,
};

/** What the detail read model serves for this campus's contact. */
const contact = {
  fullName: 'Nguyễn Văn A',
  organization: 'Công ty ABC',
  jobTitle: 'Trưởng phòng',
  phone: '+84912345678',
  email: 'owner@example.com',
  confirmationStatus: 'CONFIRMED',
  confirmationSource: 'EMAIL_CONFIRMATION',
  confirmedAt: '2026-08-01T09:00:00',
};

// The action codes the BACKEND actually emits (PEMS.Domain.Constants.VisitFormActions). This file
// used to assert against four codes nobody sends — RESEND_CONTACT_CLAIM, REPLACE_PENDING_CONTACT,
// INITIATE_CONTACT_TRANSFER, CANCEL_CONTACT_TRANSFER — which is why the panel could render nothing in
// production while every test here passed.
const PROFILE_ONLY = ['VIEW', 'UPDATE_OPERATIONAL_CONTACT_PROFILE'];
const UNDECIDED_ACTIONS = [
  'VIEW', 'UPDATE_OPERATIONAL_CONTACT_PROFILE',
  'RESEND_OPERATIONAL_CONTACT_CONFIRMATION', 'REPLACE_OPERATIONAL_CONTACT',
];
const DECIDED_ACTIONS = [
  'VIEW', 'UPDATE_OPERATIONAL_CONTACT_PROFILE', 'INITIATE_OPERATIONAL_CONTACT_TRANSFER',
];
const PENDING_TRANSFER_ACTIONS = [
  'VIEW', 'UPDATE_OPERATIONAL_CONTACT_PROFILE',
  'RESEND_OPERATIONAL_CONTACT_CONFIRMATION', 'CANCEL_OPERATIONAL_CONTACT_CHANGE',
];

const renderActions = (props: Partial<React.ComponentProps<typeof ContactIdentityActions>> = {}) =>
  render(
    <ContactIdentityActions
      visitRequestId={1}
      visitInstanceId={10}
      contactConfirmed
      contact={contact}
      rowVersion={7}
      allowedActions={DECIDED_ACTIONS}
      {...props}
    />,
  );

const emailField = () => screen.getByTestId('contact-field-email') as HTMLInputElement;

describe('ContactIdentityActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(getOperationalContactState).mockResolvedValue(noPending);
  });

  // ── Permission comes from the backend, never from the relation (plan §3, §17 UI) ──

  it('renders nothing for a read-only viewer', () => {
    const { container } = renderActions({ allowedActions: ['VIEW'] });
    expect(container).toBeEmptyDOMElement();
    expect(getOperationalContactState).not.toHaveBeenCalled();
  });

  it('renders nothing for a viewer with no actions at all (HO, host, scoped leader)', () => {
    const { container } = renderActions({ allowedActions: undefined });
    expect(container).toBeEmptyDOMElement();
  });

  it('offers the contact edit whenever the backend granted it', async () => {
    renderActions({ allowedActions: PROFILE_ONLY });
    expect(await screen.findByTestId('contact-edit-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-cancel-transfer')).not.toBeInTheDocument();
  });

  it('drops the resend button when the backend withheld it at the cap', () => {
    renderActions({ allowedActions: ['VIEW', 'REPLACE_OPERATIONAL_CONTACT'] });
    expect(screen.getByTestId('contact-edit-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
  });

  // ── The form opens on what is stored (plan §4) ────────────────────────────

  it('opens the form prefilled from the campus contact', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    expect((screen.getByTestId('contact-field-fullName') as HTMLInputElement).value).toBe('Nguyễn Văn A');
    expect((screen.getByTestId('contact-field-organization') as HTMLInputElement).value).toBe('Công ty ABC');
    expect((screen.getByTestId('contact-field-jobTitle') as HTMLInputElement).value).toBe('Trưởng phòng');
    expect((screen.getByTestId('contact-field-phone') as HTMLInputElement).value).toBe('+84912345678');
    expect(emailField().value).toBe('owner@example.com');
  });

  it('keeps the form closed until it is asked for', async () => {
    renderActions();
    await screen.findByTestId('contact-edit-open');
    expect(screen.queryByTestId('contact-form')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('contact-edit-open'));
    expect(screen.getByTestId('contact-form')).toBeInTheDocument();
  });

  it('lays the fields out in two columns on desktop and one on mobile', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    const grid = screen.getByTestId('contact-form-grid');
    expect(grid.className).toContain('grid-cols-1');
    expect(grid.className).toContain('md:grid-cols-2');
  });

  // ── Metadata-only: same address, no warning, no identity language (plan §5) ──

  it('sends a metadata-only save when the address is untouched, with the row version', async () => {
    vi.mocked(saveOperationalContact).mockResolvedValue({
      ...noPending, requestStatus: 'PENDING_APPROVAL', message: 'Đã cập nhật thông tin đầu mối.',
    });
    const onChanged = vi.fn();
    renderActions({ onChanged });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    fireEvent.change(screen.getByTestId('contact-field-phone'), { target: { value: '+84900000000' } });
    // No identity warning while the address is the stored one.
    expect(screen.queryByTestId('contact-identity-warning')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    await waitFor(() => expect(saveOperationalContact).toHaveBeenCalledTimes(1));
    expect(saveOperationalContact).toHaveBeenCalledWith(1, 10, expect.objectContaining({
      email: 'owner@example.com',
      phone: '+84900000000',
      jobTitle: 'Trưởng phòng',       // the field the old form never collected at all
      expectedRowVersion: 7,
    }));
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã cập nhật thông tin đầu mối.'));
    expect(onChanged).toHaveBeenCalled();
  });

  it('treats a case/whitespace-only address difference as the SAME identity', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    fireEvent.change(emailField(), { target: { value: '  Owner@Example.COM ' } });

    // Nothing about handing the campus over: this is the same mailbox.
    expect(screen.queryByTestId('contact-identity-warning')).not.toBeInTheDocument();
  });

  // ── Identity change: the consequence is stated BEFORE the save (plan §6, §14) ──

  it('warns that an undecided campus re-closes the gate when the address changes', async () => {
    renderActions({ allowedActions: UNDECIDED_ACTIONS, contactConfirmed: false });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    fireEvent.change(emailField(), { target: { value: 'someone.else@example.com' } });

    expect(screen.getByTestId('contact-identity-warning')).toBeInTheDocument();
    // A replace is not a handover, so it must not ask for a handover reason.
    expect(screen.queryByTestId('contact-form-reason')).not.toBeInTheDocument();
  });

  it('asks for a handover reason and promises the current contact keeps their rights (decided campus)', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    fireEvent.change(emailField(), { target: { value: 'someone.else@example.com' } });

    expect(screen.getByTestId('contact-identity-warning')).toBeInTheDocument();
    expect(screen.getByTestId('contact-form-reason')).toBeInTheDocument();
  });

  it('refuses an address change the backend did not grant, inline on the field', async () => {
    renderActions({ allowedActions: PROFILE_ONLY });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(emailField(), { target: { value: 'someone.else@example.com' } });

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    expect(saveOperationalContact).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toBeInTheDocument();   // inline, next to the field
    expect(showErrorToast).not.toHaveBeenCalled();            // a field problem is not a toast
  });

  it('closes the form on cancel without saving, and reopens from the stored values', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(screen.getByTestId('contact-field-fullName'), { target: { value: 'Ai đó' } });

    fireEvent.click(screen.getByTestId('contact-form-cancel'));

    expect(screen.queryByTestId('contact-form')).not.toBeInTheDocument();
    expect(saveOperationalContact).not.toHaveBeenCalled();
    // Reopening starts from the campus again — the abandoned draft is not carried over.
    fireEvent.click(screen.getByTestId('contact-edit-open'));
    expect((screen.getByTestId('contact-field-fullName') as HTMLInputElement).value).toBe('Nguyễn Văn A');
  });

  it('cannot be submitted twice while the first save is in flight', async () => {
    let resolveIt: (v: { message: string }) => void = () => {};
    vi.mocked(saveOperationalContact).mockReturnValue(
      new Promise(res => { resolveIt = res as (v: { message: string }) => void; }) as never,
    );
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(emailField(), { target: { value: 'new.owner@example.com' } });

    const submit = screen.getByTestId('contact-form-submit');
    fireEvent.click(submit);
    fireEvent.click(submit);
    fireEvent.click(submit);

    expect(saveOperationalContact).toHaveBeenCalledTimes(1);
    expect(submit).toBeDisabled();

    resolveIt({ message: 'Đã gửi lời mời chuyển giao.' });
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã gửi lời mời chuyển giao.'));
  });

  // ── Pending invitation state ──────────────────────────────────────────────

  it('shows a pending transfer as pending, not as the current identity', async () => {
    vi.mocked(getOperationalContactState).mockResolvedValue({
      ...noPending, pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
      pendingEmailMasked: 'n***@x.vn', expiresAt: '2026-08-01T09:00:00',
    });
    renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

    expect(await screen.findByTestId('contact-transfer-pending')).toBeInTheDocument();
    expect(screen.getByTestId('contact-cancel-transfer')).toBeInTheDocument();
  });

  it('reports a successful mutation through the shared toast, not an inline message', async () => {
    vi.mocked(resendOperationalContactConfirmation).mockResolvedValue({
      ...noPending, contactConfirmed: false, requestStatus: 'PENDING_CONTACT_CONFIRMATION',
      pendingChangeKind: 'INITIAL_CONFIRMATION', pendingChangeStatus: 'PENDING',
      resendCount: 1, message: 'Đã gửi lại lời mời.',
    });
    const onChanged = vi.fn();
    renderActions({ contactConfirmed: false, allowedActions: UNDECIDED_ACTIONS, onChanged });

    fireEvent.click(screen.getByTestId('contact-resend-claim'));

    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã gửi lại lời mời.'));
    expect(onChanged).toHaveBeenCalled();
  });

  it('reports a failed mutation through the shared error toast', async () => {
    vi.mocked(resendOperationalContactConfirmation).mockRejectedValue(new Error('boom'));
    renderActions({ contactConfirmed: false, allowedActions: UNDECIDED_ACTIONS });

    fireEvent.click(screen.getByTestId('contact-resend-claim'));

    await waitFor(() => expect(showErrorToast).toHaveBeenCalled());
    expect(showSuccessToast).not.toHaveBeenCalled();
  });

  it('surfaces a state load failure with a retry instead of claiming there is none', async () => {
    // Swallowing this into "no pending change" invited the user to start a SECOND invitation while
    // one was already in flight.
    vi.mocked(getOperationalContactState).mockRejectedValueOnce(new Error('network'));
    renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

    const retry = await screen.findByTestId('contact-transfer-retry');
    expect(screen.getByRole('alert')).toBeInTheDocument();

    vi.mocked(getOperationalContactState).mockResolvedValueOnce({
      ...noPending, pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
      pendingEmailMasked: 'n***@x.vn',
    });
    fireEvent.click(retry);

    await waitFor(() => expect(screen.queryByTestId('contact-transfer-retry')).not.toBeInTheDocument());
    expect(screen.getByTestId('contact-cancel-transfer')).toBeInTheDocument();
  });

  it('cancels a pending transfer through the shared toast', async () => {
    vi.mocked(getOperationalContactState).mockResolvedValue({
      ...noPending, pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
      pendingEmailMasked: 'n***@x.vn', expiresAt: '2026-08-01T09:00:00',
    });
    vi.mocked(cancelOperationalContactChange).mockResolvedValue({
      ...noPending, requestStatus: 'PENDING_APPROVAL', pendingChangeStatus: 'CANCELLED',
      message: 'Đã hủy lời mời chuyển giao.',
    });
    renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

    fireEvent.click(await screen.findByTestId('contact-cancel-transfer'));
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã hủy lời mời chuyển giao.'));
  });
});
