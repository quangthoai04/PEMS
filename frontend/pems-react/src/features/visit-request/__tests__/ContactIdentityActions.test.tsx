import { createRef } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, render, screen, waitFor, fireEvent, within } from '@testing-library/react';

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

import ContactIdentityActions, {
  type ContactIdentityActionsHandle,
} from '../components/ContactIdentityActions';
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
// The REAL combination `VisitFormReadService` emits for an INITIAL_CONFIRMATION pending invitation
// (no confirmed holder yet): REPLACE is granted regardless of the pending row, and once `pending is
// not null` cancel is ALWAYS added alongside resend (see backend §4, line ~843-847) — unlike
// `UNDECIDED_ACTIONS` above, which omits cancel and so is not a state that occurs for real.
const INITIAL_CONFIRMATION_PENDING_ACTIONS = [
  'VIEW', 'UPDATE_OPERATIONAL_CONTACT_PROFILE', 'REPLACE_OPERATIONAL_CONTACT',
  'RESEND_OPERATIONAL_CONTACT_CONFIRMATION', 'CANCEL_OPERATIONAL_CONTACT_CHANGE',
];
// No confirmed holder, no invitation in flight (the state a cancel leaves behind): REPLACE stays
// offered and REINVITE opens a fresh one — resend/cancel are never granted here since `pending is
// null` returns before either is added.
const NO_ACTIVE_INVITATION_ACTIONS = [
  'VIEW', 'UPDATE_OPERATIONAL_CONTACT_PROFILE', 'REPLACE_OPERATIONAL_CONTACT',
  'REINVITE_OPERATIONAL_CONTACT_CONFIRMATION',
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

  // ── The trigger's LABEL names the actual workflow (Replace vs Transfer), never one generic word for
  //    two different consequences (plan: "Thay đầu mối" replaces an unconfirmed person, "Chuyển đầu
  //    mối" hands the role off from whoever holds it). ──

  it('labels the trigger "Replace contact" when only REPLACE_OPERATIONAL_CONTACT was granted', async () => {
    renderActions({ allowedActions: UNDECIDED_ACTIONS });
    expect(await screen.findByTestId('contact-edit-open')).toBeInTheDocument();
    expect(screen.getByTestId('contact-edit-open')).toHaveTextContent(/replace contact/i);
  });

  it('labels the trigger "Transfer contact" when only INITIATE_OPERATIONAL_CONTACT_TRANSFER was granted', async () => {
    renderActions({ allowedActions: DECIDED_ACTIONS });
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

  it('no longer shows an inline transfer-rights warning (Transfer, decided campus)', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));
    fillNewIdentity();

    expect(screen.queryByTestId('contact-identity-warning')).not.toBeInTheDocument();
    expect(screen.queryByTestId('contact-form-reason')).not.toBeInTheDocument();
    expect(document.getElementById('ci-reason')).toBeNull();
  });

  it('no longer renders the old inline email-identity hint under the Email field', async () => {
    renderActions();
    fireEvent.click(await screen.findByTestId('contact-edit-open'));

    // The old standalone paragraph (id="ci-email-hint", a sibling of the Email input) is gone, and so
    // is the explanation it carried — nothing replaces it.
    expect(document.getElementById('ci-email-hint')).toBeNull();
    const emailContainer = emailField().parentElement as HTMLElement;
    expect(within(emailContainer).queryByText(/only the details are updated/i)).not.toBeInTheDocument();
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

  // ── Pending transfer — "Xem chi tiết" collapsible detail (keeps the card short by default) ──────

  describe('pending transfer — Xem chi tiết (collapsible detail)', () => {
    const pendingTransferState = {
      ...noPending,
      pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
      pendingEmailMasked: 's***@s.ss', expiresAt: '2026-08-27T23:06:00',
      pendingContact: {
        fullName: 'Sarah Smith',
        organization: 'ABC University',
        jobTitle: 'International Coordinator',
        phone: '+84987654321',
        emailMasked: 's***@s.ss',
      },
    };

    // Test 1 — default collapsed.
    it('starts collapsed: summary + toggle + resend/cancel visible, pending person fields are not', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue(pendingTransferState);
      renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

      expect(await screen.findByTestId('contact-transfer-pending')).toBeInTheDocument();
      const toggle = screen.getByTestId('contact-pending-details-toggle');
      expect(toggle).toHaveAttribute('aria-expanded', 'false');
      expect(toggle).toHaveTextContent(/view details/i);
      expect(screen.queryByTestId('contact-pending-details')).not.toBeInTheDocument();
      expect(screen.queryByText('Sarah Smith')).not.toBeInTheDocument();
      // The main actions of a pending transfer are not gated behind the disclosure.
      expect(screen.getByTestId('contact-resend-claim')).toBeInTheDocument();
      expect(screen.getByTestId('contact-cancel-transfer')).toBeInTheDocument();
    });

    // Test 2 — expand.
    it('expands to show the pending person on click, with aria-expanded/aria-controls wired', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue(pendingTransferState);
      renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

      fireEvent.click(await screen.findByTestId('contact-pending-details-toggle'));

      const toggle = screen.getByTestId('contact-pending-details-toggle');
      expect(toggle).toHaveAttribute('aria-expanded', 'true');
      expect(toggle).toHaveTextContent(/collapse/i);
      const panel = screen.getByTestId('contact-pending-details');
      expect(toggle.getAttribute('aria-controls')).toBe(panel.id);
      expect(within(panel).getByTestId('contact-pending-full-name-10')).toHaveTextContent('Sarah Smith');
      expect(within(panel).getByTestId('contact-pending-organization-10')).toHaveTextContent('ABC University');
      expect(within(panel).getByTestId('contact-pending-job-title-10'))
        .toHaveTextContent('International Coordinator');
      expect(within(panel).getByTestId('contact-pending-phone-10')).toHaveTextContent('+84987654321');
      expect(within(panel).getByTestId('contact-pending-email-10')).toHaveTextContent('s***@s.ss');
    });

    // Test 3 — collapse again.
    it('collapses again on a second click, keeping the summary and actions visible', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue(pendingTransferState);
      renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

      const toggle = await screen.findByTestId('contact-pending-details-toggle');
      fireEvent.click(toggle);
      expect(screen.getByTestId('contact-pending-details')).toBeInTheDocument();

      fireEvent.click(toggle);

      expect(screen.queryByTestId('contact-pending-details')).not.toBeInTheDocument();
      expect(toggle).toHaveAttribute('aria-expanded', 'false');
      expect(screen.getByTestId('contact-transfer-pending')).toBeInTheDocument();
      expect(screen.getByTestId('contact-resend-claim')).toBeInTheDocument();
      expect(screen.getByTestId('contact-cancel-transfer')).toBeInTheDocument();
    });

    // Test 4 — no pending.
    it('offers no toggle or detail panel outside a pending transfer', async () => {
      renderActions({ allowedActions: DECIDED_ACTIONS }); // beforeEach mocks getOperationalContactState → noPending

      expect(await screen.findByTestId('contact-edit-open')).toBeInTheDocument();
      expect(screen.queryByTestId('contact-pending-details-toggle')).not.toBeInTheDocument();
      expect(screen.queryByTestId('contact-pending-details')).not.toBeInTheDocument();
    });

    // Test 5 — cancel pending.
    it('drops the summary, toggle and detail once the transfer is cancelled', async () => {
      vi.mocked(getOperationalContactState)
        .mockResolvedValueOnce(pendingTransferState)
        .mockResolvedValueOnce(noPending);
      vi.mocked(cancelOperationalContactChange).mockResolvedValue({
        ...noPending, requestStatus: 'PENDING_APPROVAL', pendingChangeStatus: 'CANCELLED',
        message: 'Đã hủy lời mời chuyển giao.',
      });
      renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

      fireEvent.click(await screen.findByTestId('contact-pending-details-toggle'));
      expect(screen.getByTestId('contact-pending-details')).toBeInTheDocument();

      fireEvent.click(screen.getByTestId('contact-cancel-transfer'));
      fireEvent.click(await screen.findByTestId('contact-cancel-confirm-submit'));

      await waitFor(() => expect(showSuccessToast).toHaveBeenCalled());
      await waitFor(() => expect(screen.queryByTestId('contact-transfer-pending')).not.toBeInTheDocument());
      expect(screen.queryByTestId('contact-pending-details')).not.toBeInTheDocument();
      expect(screen.queryByTestId('contact-pending-details-toggle')).not.toBeInTheDocument();
    });

    // Test 6 (resend leg) — a RESEND bumps tokenVersion/expiresAt on the SAME invitation (plan §19); the
    // expanded state must survive it rather than reset as if a different invitation had appeared.
    it('keeps the detail expanded through a resend of the same invitation', async () => {
      vi.mocked(getOperationalContactState)
        .mockResolvedValueOnce(pendingTransferState)
        .mockResolvedValueOnce({
          ...pendingTransferState, tokenVersion: 2, resendCount: 1, expiresAt: '2026-08-28T00:00:00',
        });
      vi.mocked(resendOperationalContactConfirmation).mockResolvedValue({
        ...noPending, requestStatus: 'PENDING_APPROVAL', message: 'Đã gửi lại lời mời.',
      });
      renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

      fireEvent.click(await screen.findByTestId('contact-pending-details-toggle'));
      expect(screen.getByTestId('contact-pending-details')).toBeInTheDocument();

      fireEvent.click(screen.getByTestId('contact-resend-claim'));
      await waitFor(() => expect(showSuccessToast).toHaveBeenCalled());

      expect(screen.getByTestId('contact-pending-details')).toBeInTheDocument();
      expect(screen.getByTestId('contact-pending-details-toggle')).toHaveAttribute('aria-expanded', 'true');
    });

    it('falls back to a plain notice when the snapshot has no pending-contact detail (legacy/redacted row)', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue({
        ...pendingTransferState, pendingContact: null,
      });
      renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

      fireEvent.click(await screen.findByTestId('contact-pending-details-toggle'));

      expect(screen.getByTestId('contact-pending-details-unavailable')).toBeInTheDocument();
      expect(screen.queryByTestId('contact-pending-full-name-10')).not.toBeInTheDocument();
    });
  });

  // ── State matrix (A1-A6): the exact allowedActions combinations VisitFormReadService emits for
  //    each business state, and the label/action set each one must produce. ──────────────────────

  describe('state matrix', () => {
    it('A1 — initial-confirmation pending: Thay đầu mối (not Chuyển đầu mối), resend, cancel confirmation', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue({
        ...noPending, contactConfirmed: false,
        pendingChangeKind: 'INITIAL_CONFIRMATION', pendingChangeStatus: 'PENDING',
        pendingEmailMasked: 'ad***@gsd.gh', expiresAt: '2026-08-01T09:00:00',
      });
      renderActions({ contactConfirmed: false, allowedActions: INITIAL_CONFIRMATION_PENDING_ACTIONS });

      const trigger = await screen.findByTestId('contact-edit-open');
      expect(trigger).toHaveTextContent(/replace contact/i);
      expect(trigger).not.toHaveTextContent(/^transfer contact$/i);
      expect(screen.getByTestId('contact-resend-claim')).toBeInTheDocument();
      expect(screen.getByTestId('contact-cancel-transfer')).toHaveTextContent(/cancel the confirmation invitation/i);
    });

    it('A2 — confirmed idle: Chuyển đầu mối only, no resend/cancel/pending summary', async () => {
      renderActions({ allowedActions: DECIDED_ACTIONS }); // beforeEach: getOperationalContactState → noPending

      const trigger = await screen.findByTestId('contact-edit-open');
      expect(trigger).toHaveTextContent(/transfer contact/i);
      expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
      expect(screen.queryByTestId('contact-cancel-transfer')).not.toBeInTheDocument();
      expect(screen.queryByTestId('contact-transfer-pending')).not.toBeInTheDocument();
    });

    it('A3 — transfer pending: no Chuyển đầu mối, has Xem chi tiết, resend, cancel transfer', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue({
        ...noPending, pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
        pendingEmailMasked: 'c***@x.vn', expiresAt: '2026-08-27T23:43:00',
      });
      renderActions({ allowedActions: PENDING_TRANSFER_ACTIONS });

      await screen.findByTestId('contact-transfer-pending');
      expect(screen.queryByTestId('contact-edit-open')).not.toBeInTheDocument();
      expect(screen.getByTestId('contact-pending-details-toggle')).toBeInTheDocument();
      expect(screen.getByTestId('contact-resend-claim')).toBeInTheDocument();
      expect(screen.getByTestId('contact-cancel-transfer')).toHaveTextContent(/cancel the transfer invitation/i);
    });

    // A4 (expand/collapse the pending-person detail) is covered in full by the
    // "pending transfer — Xem chi tiết" describe block above; not duplicated here.

    it('A5 — no active invitation: Thay đầu mối + Mời lại, no resend/cancel (pending is null)', async () => {
      renderActions({ contactConfirmed: false, allowedActions: NO_ACTIVE_INVITATION_ACTIONS });

      const trigger = await screen.findByTestId('contact-edit-open');
      expect(trigger).toHaveTextContent(/replace contact/i);
      expect(screen.getByTestId('contact-reinvite')).toBeInTheDocument();
      expect(screen.queryByTestId('contact-resend-claim')).not.toBeInTheDocument();
      expect(screen.queryByTestId('contact-cancel-transfer')).not.toBeInTheDocument();
    });

    // A6 — backend-contract check: VisitFormReadService only ever adds InitiateOperationalContactTransfer
    // when `pending is null` (VisitFormReadService.cs line ~819), so a TRANSFER_PENDING state carrying
    // that action code is a combination the real backend cannot produce — audited during this task, not
    // found. This test documents what the frontend does if that invariant is ever broken: it trusts
    // `allowedActions` as the authority and does NOT defensively hide the trigger, so a future regression
    // would be visible on screen (a spurious "Chuyển đầu mối" beside an active transfer) rather than
    // silently normalized away.
    it('A6 — trusts allowedActions rather than hiding it if the backend ever granted transfer during a pending transfer', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue({
        ...noPending, pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
        pendingEmailMasked: 'c***@x.vn', expiresAt: '2026-08-27T23:43:00',
      });
      renderActions({ allowedActions: [...PENDING_TRANSFER_ACTIONS, 'INITIATE_OPERATIONAL_CONTACT_TRANSFER'] });

      await screen.findByTestId('contact-transfer-pending');
      expect(screen.getByTestId('contact-edit-open')).toHaveTextContent(/transfer contact/i);
    });
  });

  // ── External trigger (CampusVisitDetailCard renders "Chuyển đầu mối" in the section header and
  //    drives this panel through the ref instead of its own inline button) ────────────────────────

  describe('external trigger (hidePrimaryTrigger)', () => {
    it('hides its own inline button but still opens the form through the ref', async () => {
      const ref = createRef<ContactIdentityActionsHandle>();
      render(
        <ContactIdentityActions
          ref={ref}
          visitRequestId={1}
          visitInstanceId={10}
          contactConfirmed
          contact={contact}
          allowedActions={DECIDED_ACTIONS}
          hidePrimaryTrigger
        />,
      );
      await waitFor(() => expect(getOperationalContactState).toHaveBeenCalled());

      expect(screen.queryByTestId('contact-edit-open')).not.toBeInTheDocument();
      act(() => ref.current?.openForm());
      expect(await screen.findByTestId('contact-form')).toBeInTheDocument();
    });

    it('renders nothing once the header owns the trigger and the contact is simply confirmed', async () => {
      const { container } = render(
        <ContactIdentityActions
          visitRequestId={1}
          visitInstanceId={10}
          contactConfirmed
          contact={contact}
          allowedActions={DECIDED_ACTIONS}
          hidePrimaryTrigger
        />,
      );
      await waitFor(() => expect(getOperationalContactState).toHaveBeenCalled());
      await waitFor(() => expect(container).toBeEmptyDOMElement());
    });

    it('still renders its pending/secondary-action state even with the trigger hidden', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue({
        ...noPending, pendingChangeKind: 'TRANSFER', pendingChangeStatus: 'PENDING',
        pendingEmailMasked: 'n***@x.vn', expiresAt: '2026-08-01T09:00:00',
      });
      render(
        <ContactIdentityActions
          visitRequestId={1}
          visitInstanceId={10}
          contactConfirmed
          contact={contact}
          allowedActions={PENDING_TRANSFER_ACTIONS}
          hidePrimaryTrigger
        />,
      );

      expect(await screen.findByTestId('contact-transfer-pending')).toBeInTheDocument();
      expect(screen.getByTestId('contact-cancel-transfer')).toBeInTheDocument();
      expect(screen.queryByTestId('contact-edit-open')).not.toBeInTheDocument();
    });

    it('reports form open/close through onFormOpenChange', async () => {
      const ref = createRef<ContactIdentityActionsHandle>();
      const onFormOpenChange = vi.fn();
      render(
        <ContactIdentityActions
          ref={ref}
          visitRequestId={1}
          visitInstanceId={10}
          contactConfirmed
          contact={contact}
          allowedActions={DECIDED_ACTIONS}
          hidePrimaryTrigger
          onFormOpenChange={onFormOpenChange}
        />,
      );
      await waitFor(() => expect(onFormOpenChange).toHaveBeenCalledWith(false));
      onFormOpenChange.mockClear();

      act(() => ref.current?.openForm());
      await waitFor(() => expect(onFormOpenChange).toHaveBeenCalledWith(true));
    });
  });

  // ── Profile-mismatch offer now lives in the contact card's title row (icon → popover), not inside
  // this panel's own body — this panel only reports the difference up through a callback + exposes
  // refreshState so the externally-rendered popover can ask it to re-read state after applying. ──
  describe('profile-mismatch offer (reported up, not rendered inline)', () => {
    const bothDiffer = {
      fullNameDiffers: true,
      phoneDiffers: true,
      accountFullName: 'Nguyen Van A',
      accountPhone: '+84912345678',
      snapshotFullName: 'Nguyễn Văn A (Trưởng đoàn)',
      snapshotPhone: '+84900000111',
    };

    it('reports the difference through onProfileDifferenceChange once state loads', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue({ ...noPending, profileDifference: bothDiffer });
      const onProfileDifferenceChange = vi.fn();
      renderActions({ onProfileDifferenceChange });

      await waitFor(() => expect(onProfileDifferenceChange).toHaveBeenCalledWith(bothDiffer));
    });

    it('reports null when there is nothing to reconcile', async () => {
      const onProfileDifferenceChange = vi.fn();
      renderActions({ onProfileDifferenceChange }); // beforeEach mocks getOperationalContactState → noPending

      await waitFor(() => expect(onProfileDifferenceChange).toHaveBeenCalledWith(null));
    });

    it('never renders the old inline banner/popover itself, even when there is a difference', async () => {
      vi.mocked(getOperationalContactState).mockResolvedValue({ ...noPending, profileDifference: bothDiffer });
      renderActions();

      await waitFor(() => expect(getOperationalContactState).toHaveBeenCalled());
      expect(screen.queryByTestId('contact-profile-sync-prompt')).not.toBeInTheDocument();
      expect(screen.queryByTestId('profile-sync-trigger-10')).not.toBeInTheDocument();
      expect(screen.queryByTestId('profile-sync-popover-10')).not.toBeInTheDocument();
    });

    it('exposes refreshState via the ref, so the externally-rendered popover can re-read state after it applies', async () => {
      const ref = createRef<ContactIdentityActionsHandle>();
      render(
        <ContactIdentityActions
          ref={ref}
          visitRequestId={1}
          visitInstanceId={10}
          contactConfirmed
          contact={contact}
          allowedActions={DECIDED_ACTIONS}
        />,
      );
      await waitFor(() => expect(getOperationalContactState).toHaveBeenCalledTimes(1));

      await act(async () => {
        await ref.current?.refreshState();
      });
      expect(getOperationalContactState).toHaveBeenCalledTimes(2);
    });
  });
});
