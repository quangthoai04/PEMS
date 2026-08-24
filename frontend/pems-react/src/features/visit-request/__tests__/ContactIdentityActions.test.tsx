import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({
  getOperationalContactState: vi.fn(),
  resendOperationalContactConfirmation: vi.fn(),
  reinviteOperationalContactConfirmation: vi.fn(),
  replaceOperationalContact: vi.fn(),
  initiateOperationalContactTransfer: vi.fn(),
  cancelOperationalContactChange: vi.fn(),
}));

// Organization is the shared search combobox (react-select); it calls this on every keystroke, so an
// unmocked call would hang the async loadOptions promise. No test here exercises the dropdown itself —
// that behavior is covered where the SAME component is exercised at Create time
// (operationalContactQuickFill.test.tsx) — this mock only keeps the field's free-text/required
// behavior testable in isolation.
vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: { searchOrganizations: vi.fn().mockResolvedValue([]) },
}));

const showSuccessToast = vi.fn();
const showErrorToast = vi.fn();
const showMessageErrorToast = vi.fn();
vi.mock('../../../shared/utils/toast', () => ({
  showSuccessToast: (...a: unknown[]) => showSuccessToast(...a),
  showErrorToast: (...a: unknown[]) => showErrorToast(...a),
  showMessageErrorToast: (...a: unknown[]) => showMessageErrorToast(...a),
}));

import ContactIdentityActions from '../components/ContactIdentityActions';
import {
  getOperationalContactState,
  resendOperationalContactConfirmation,
  reinviteOperationalContactConfirmation,
  cancelOperationalContactChange,
  replaceOperationalContact,
  initiateOperationalContactTransfer,
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

// The action codes the BACKEND actually emits (PEMS.Domain.Constants.VisitFormActions).
// UPDATE_OPERATIONAL_CONTACT_PROFILE grants Sửa nhanh's contact block (a DIFFERENT component now) — it
// no longer grants anything in THIS panel, which is Transfer-only (plan CanhIter3FixBug).
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
      allowedActions={DECIDED_ACTIONS}
      {...props}
    />,
  );

const emailField = () => screen.getByTestId('contact-field-email') as HTMLInputElement;
/** Organization is react-select (OrganizationCombobox) — reached through its wrapper, like every
 *  other test of this shared control (see operationalContactQuickFill.test.tsx). */
const orgWrapper = () => screen.getByTestId('contact-field-organization');
const orgInput = () => orgWrapper().querySelector('input')!;

/** Fills the (blank) form with a genuinely different, valid identity. */
const fillNewIdentity = (email = 'someone.else@example.com') => {
  fireEvent.change(screen.getByTestId('contact-field-fullName'), { target: { value: 'Người mới' } });
  fireEvent.change(orgInput(), { target: { value: 'Đơn vị mới' } });
  fireEvent.change(screen.getByTestId('contact-field-jobTitle'), { target: { value: 'Chức vụ mới' } });
  fireEvent.change(emailField(), { target: { value: email } });
};

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

  // Sửa nhanh's contact block (a different component) is what UPDATE_OPERATIONAL_CONTACT_PROFILE now
  // grants — this panel is Transfer-only, so that capability alone must not show its button.
  it('does not offer "Chuyển đầu mối" from UPDATE_OPERATIONAL_CONTACT_PROFILE alone', () => {
    renderActions({ allowedActions: PROFILE_ONLY });
    expect(screen.queryByTestId('contact-edit-open')).not.toBeInTheDocument();
  });

  it('offers the transfer-contact action whenever the backend granted an identity-change action', async () => {
    renderActions({ allowedActions: UNDECIDED_ACTIONS });
    expect(await screen.findByTestId('contact-edit-open')).toBeInTheDocument();
    expect(screen.getByTestId('contact-edit-open')).toHaveTextContent(/transfer contact/i);
  });

  it('drops the resend button when the backend withheld it at the cap', () => {
    renderActions({ allowedActions: ['VIEW', 'REPLACE_OPERATIONAL_CONTACT'] });
    expect(screen.getByTestId('contact-edit-open')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
  });

  // ── The Transfer form opens BLANK (plan CanhIter3FixBug §17.1) ───────────────────────────────

  it('opens a genuinely blank form, never prefilled from the current contact', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    expect((screen.getByTestId('contact-field-fullName') as HTMLInputElement).value).toBe('');
    expect(orgWrapper().textContent ?? '').not.toContain('Công ty ABC');
    expect((screen.getByTestId('contact-field-jobTitle') as HTMLInputElement).value).toBe('');
    expect((screen.getByTestId('contact-field-phone') as HTMLInputElement).value).toBe('');
    expect(emailField().value).toBe('');
  });

  it('keeps the form closed until it is asked for', async () => {
    renderActions();
    await screen.findByTestId('contact-edit-open');
    expect(screen.queryByTestId('contact-form')).not.toBeInTheDocument();

    fireEvent.click(screen.getByTestId('contact-edit-open'));
    expect(screen.getByTestId('contact-form')).toBeInTheDocument();
  });

  it('has no relation picker or "Lưu liên kết" button anywhere in this component', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    expect(screen.queryByTestId('contact-relation-pick')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-relation-submit')).not.toBeInTheDocument();
  });

  // ── Same-email is blocked, client-side first (plan §17.2/§33) ────────────────────────────────

  it('blocks submit inline when the typed email matches the current contact, without calling the API', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity('owner@example.com'); // same as `contact.email`

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    expect(replaceOperationalContact).not.toHaveBeenCalled();
    expect(initiateOperationalContactTransfer).not.toHaveBeenCalled();
    expect(await screen.findByRole('alert')).toHaveTextContent(/matches the current contact|quick edit/i);
  });

  it('treats a case/whitespace-only address difference as the SAME identity (still blocked)', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity('  Owner@Example.COM ');

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    expect(replaceOperationalContact).not.toHaveBeenCalled();
    expect(initiateOperationalContactTransfer).not.toHaveBeenCalled();
  });

  // ── Explicit dispatch by current-holder state (plan §17.3/§33) ───────────────────────────────

  it('dispatches to replaceOperationalContact when there is no confirmed holder', async () => {
    vi.mocked(replaceOperationalContact).mockResolvedValue({
      ...noPending, requestStatus: 'PENDING_APPROVAL', message: 'Đã cập nhật đầu mối vận hành.',
    });
    renderActions({ contactConfirmed: false, allowedActions: UNDECIDED_ACTIONS });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    await waitFor(() => expect(replaceOperationalContact).toHaveBeenCalledTimes(1));
    expect(initiateOperationalContactTransfer).not.toHaveBeenCalled();
    expect(replaceOperationalContact).toHaveBeenCalledWith(1, 10, expect.objectContaining({
      fullName: 'Người mới', organization: 'Đơn vị mới', jobTitle: 'Chức vụ mới',
      email: 'someone.else@example.com',
    }));
  });

  it('dispatches to initiateOperationalContactTransfer when a confirmed holder exists', async () => {
    vi.mocked(initiateOperationalContactTransfer).mockResolvedValue({
      ...noPending, requestStatus: 'PENDING_APPROVAL', message: 'Đã gửi lời mời chuyển giao.',
    });
    renderActions({ contactConfirmed: true, allowedActions: DECIDED_ACTIONS });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    await waitFor(() => expect(initiateOperationalContactTransfer).toHaveBeenCalledTimes(1));
    expect(replaceOperationalContact).not.toHaveBeenCalled();
    expect(initiateOperationalContactTransfer).toHaveBeenCalledWith(1, 10, expect.objectContaining({
      fullName: 'Người mới', email: 'someone.else@example.com',
    }));
  });

  it('backend same-email rejection (ChangeConflict) maps to the same generic conflict handling', async () => {
    vi.mocked(replaceOperationalContact).mockRejectedValue({
      response: {
        status: 409,
        data: { errorCode: 'OPERATIONAL_CONTACT_CHANGE_CONFLICT', message: 'Email mới trùng với đầu mối hiện tại.' },
      },
    });
    renderActions({ contactConfirmed: false, allowedActions: UNDECIDED_ACTIONS });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();
    fireEvent.click(screen.getByTestId('contact-form-submit'));

    await waitFor(() => expect(replaceOperationalContact).toHaveBeenCalled());
    await waitFor(() => expect(showMessageErrorToast).toHaveBeenCalled());
  });

  // ── Identity change: the consequence is stated BEFORE the save (plan §6, §14) ──

  it('warns that an undecided campus re-closes the gate when a new address is entered, with no reason field (Replace)', async () => {
    renderActions({ allowedActions: UNDECIDED_ACTIONS, contactConfirmed: false });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();

    expect(screen.getByTestId('contact-identity-warning')).toBeInTheDocument();
    // "Lý do chuyển giao" was removed from the transfer form — reason never gates authorization,
    // lifecycle, invitation, eligibility, accept/decline or handover, so there is nothing for the
    // user to fill in here regardless of whether this ends up being a Replace or a Transfer.
    expect(screen.queryByTestId('contact-form-reason')).not.toBeInTheDocument();
    expect(document.getElementById('ci-reason')).toBeNull();
  });

  it('promises the current contact keeps their rights, with no reason field anywhere (Transfer, decided campus)', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();

    expect(screen.getByTestId('contact-identity-warning')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-form-reason')).not.toBeInTheDocument();
    expect(document.getElementById('ci-reason')).toBeNull();
  });

  it('submits a Transfer request without a reason field ever having existed to fill in', async () => {
    vi.mocked(initiateOperationalContactTransfer).mockResolvedValue({
      ...noPending, requestStatus: 'PENDING_APPROVAL', message: 'Đã gửi lời mời chuyển giao.',
    });
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();
    fireEvent.click(screen.getByTestId('contact-form-submit'));

    await waitFor(() => expect(initiateOperationalContactTransfer).toHaveBeenCalledTimes(1));
    const [, , body] = vi.mocked(initiateOperationalContactTransfer).mock.calls[0];
    expect(body).not.toHaveProperty('reason');
  });

  // ── Field-level validation (plan PEMS_VALIDATION_UX §2) — required fields must highlight, not
  //    just refuse silently or via a generic toast. ──

  it('blocks submit and highlights Full name when it is blank', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(orgInput(), { target: { value: 'Đơn vị mới' } });
    fireEvent.change(screen.getByTestId('contact-field-jobTitle'), { target: { value: 'Chức vụ' } });
    fireEvent.change(emailField(), { target: { value: 'someone.else@example.com' } });
    fireEvent.click(screen.getByTestId('contact-form-submit'));

    expect(replaceOperationalContact).not.toHaveBeenCalled();
    expect(initiateOperationalContactTransfer).not.toHaveBeenCalled();
    const fullNameInput = screen.getByTestId('contact-field-fullName');
    expect(fullNameInput).toHaveAttribute('aria-invalid', 'true');
  });

  it('blocks submit and highlights Organization when it is blank', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(screen.getByTestId('contact-field-fullName'), { target: { value: 'Người mới' } });
    fireEvent.change(screen.getByTestId('contact-field-jobTitle'), { target: { value: 'Chức vụ' } });
    fireEvent.change(emailField(), { target: { value: 'someone.else@example.com' } });
    fireEvent.click(screen.getByTestId('contact-form-submit'));

    expect(replaceOperationalContact).not.toHaveBeenCalled();
    expect(orgInput()).toHaveAttribute('aria-invalid', 'true');
  });

  it('blocks submit and highlights Email when it is blank', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(screen.getByTestId('contact-field-fullName'), { target: { value: 'Người mới' } });
    fireEvent.change(orgInput(), { target: { value: 'Đơn vị mới' } });
    fireEvent.change(screen.getByTestId('contact-field-jobTitle'), { target: { value: 'Chức vụ' } });
    fireEvent.click(screen.getByTestId('contact-form-submit'));

    expect(replaceOperationalContact).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('accepts an organization not on file as free text', async () => {
    vi.mocked(replaceOperationalContact).mockResolvedValue({
      ...noPending, requestStatus: 'PENDING_APPROVAL', message: 'Đã cập nhật đầu mối vận hành.',
    });
    renderActions({ contactConfirmed: false, allowedActions: UNDECIDED_ACTIONS });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(screen.getByTestId('contact-field-fullName'), { target: { value: 'Người mới' } });
    fireEvent.change(orgInput(), { target: { value: 'Custom Research Center' } });
    fireEvent.blur(orgInput());
    fireEvent.change(screen.getByTestId('contact-field-jobTitle'), { target: { value: 'Chức vụ' } });
    fireEvent.change(emailField(), { target: { value: 'someone.else@example.com' } });

    fireEvent.click(screen.getByTestId('contact-form-submit'));
    await waitFor(() => expect(replaceOperationalContact).toHaveBeenCalledTimes(1));
    expect(replaceOperationalContact).toHaveBeenCalledWith(1, 10, expect.objectContaining({
      organization: 'Custom Research Center',
    }));
  });

  it('renders the shared search combobox for Organization, not a plain text input', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    expect(orgInput()).toHaveAttribute('role', 'combobox');
  });

  it('closes the form on cancel without saving, and reopens blank again', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fireEvent.change(screen.getByTestId('contact-field-fullName'), { target: { value: 'Ai đó' } });

    fireEvent.click(screen.getByTestId('contact-form-cancel'));

    expect(screen.queryByTestId('contact-form')).not.toBeInTheDocument();
    expect(replaceOperationalContact).not.toHaveBeenCalled();
    fireEvent.click(screen.getByTestId('contact-edit-open'));
    expect((screen.getByTestId('contact-field-fullName') as HTMLInputElement).value).toBe('');
  });

  it('cannot be submitted twice while the first save is in flight', async () => {
    let resolveIt: (v: { message: string }) => void = () => {};
    vi.mocked(initiateOperationalContactTransfer).mockReturnValue(
      new Promise(res => { resolveIt = res as (v: { message: string }) => void; }) as never,
    );
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();

    const submit = screen.getByTestId('contact-form-submit');
    fireEvent.click(submit);
    fireEvent.click(submit);
    fireEvent.click(submit);

    expect(initiateOperationalContactTransfer).toHaveBeenCalledTimes(1);
    expect(submit).toBeDisabled();

    resolveIt({ message: 'Đã gửi lời mời chuyển giao.' });
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã gửi lời mời chuyển giao.'));
  });

  // ── Pending invitation state (unrelated to the identity form itself) ─────────────────────────

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
    const boom = new Error('boom');
    vi.mocked(resendOperationalContactConfirmation).mockRejectedValue(boom);
    renderActions({ contactConfirmed: false, allowedActions: UNDECIDED_ACTIONS });

    fireEvent.click(screen.getByTestId('contact-resend-claim'));

    await waitFor(() => expect(showErrorToast).toHaveBeenCalled());
    expect(showErrorToast).toHaveBeenCalledWith(boom, expect.any(String));
    expect(showSuccessToast).not.toHaveBeenCalled();
  });

  // ── A refusal has to say WHY (plan §18) ────────────────────────────────────────────────────────

  it('refuses an internal address inline on the email field, saying why', async () => {
    vi.mocked(replaceOperationalContact).mockRejectedValue({
      response: {
        status: 409,
        data: {
          success: false, errorCode: 'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT',
          message: 'Không thể sử dụng email này cho đầu mối của đoàn.',
        },
      },
    });
    renderActions({ contactConfirmed: false, allowedActions: UNDECIDED_ACTIONS });
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity('library.staff@fpt.edu.vn');

    fireEvent.click(screen.getByTestId('contact-form-submit'));

    await waitFor(() => expect(replaceOperationalContact).toHaveBeenCalled());
    expect(await screen.findByRole('alert')).toHaveTextContent(/cannot be used as the delegation contact/i);
    expect(screen.getByTestId('contact-form')).toBeInTheDocument();
    expect(showErrorToast).not.toHaveBeenCalled();
    expect(showMessageErrorToast).not.toHaveBeenCalled();
  });

  it('falls back to a toast for an email refusal raised with no form open', async () => {
    vi.mocked(reinviteOperationalContactConfirmation).mockRejectedValue({
      response: {
        status: 409,
        data: { errorCode: 'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT', message: 'nội bộ' },
      },
    });
    renderActions({
      contactConfirmed: false,
      allowedActions: [...UNDECIDED_ACTIONS, 'REINVITE_OPERATIONAL_CONTACT_CONFIRMATION'],
    });

    fireEvent.click(await screen.findByTestId('contact-reinvite'));

    await waitFor(() => expect(showMessageErrorToast).toHaveBeenCalled());
    expect(showMessageErrorToast).toHaveBeenCalledWith(
      expect.stringMatching(/cannot be used as the delegation contact/i),
    );
    expect(showErrorToast).not.toHaveBeenCalled();
  });

  it('surfaces a state load failure with a retry instead of claiming there is none', async () => {
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
    expect(cancelOperationalContactChange).not.toHaveBeenCalled();

    expect(await screen.findByTestId('contact-cancel-confirm')).toBeInTheDocument();

    fireEvent.click(screen.getByTestId('contact-cancel-confirm-submit'));
    await waitFor(() => expect(showSuccessToast).toHaveBeenCalledWith('Đã hủy lời mời chuyển giao.'));
  });

  it('still offers the cancel cleanup when every mutation action is withheld', async () => {
    vi.mocked(getOperationalContactState).mockResolvedValue({
      ...noPending, campusStatus: 'DURING_VISIT',
      pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
      pendingEmailMasked: 'n***@x.vn', expiresAt: '2026-08-01T09:00:00',
    });
    renderActions({ allowedActions: ['VIEW', 'CANCEL_OPERATIONAL_CONTACT_CHANGE'] });

    expect(await screen.findByTestId('contact-cancel-transfer')).toBeInTheDocument();
    expect(screen.queryByTestId('contact-edit-open')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-reinvite')).not.toBeInTheDocument();
  });
});
