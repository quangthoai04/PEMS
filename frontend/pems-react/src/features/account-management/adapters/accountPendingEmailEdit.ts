/**
 * Which endpoint a basic-info edit goes to, and what HO is told afterwards.
 *
 * The distinction this module exists for: an account in PENDING_EMAIL_CONFIRMATION has never proven
 * it owns an address, so changing that address has to re-issue the activation link. The generic
 * basic-info endpoint only mails a "your login email changed" notice — no token, no button — which
 * would leave the account permanently unable to log in. Getting the branch wrong is invisible in the
 * UI (both calls return success), so it is decided here and covered by tests.
 */

import { normalizeAccountEmail } from '../validation/accountIdentityValidation';

export type PendingEmailEditToastKind = 'success' | 'warning' | 'error';

export const PENDING_EMAIL_CONFIRMATION_STATUS = 'PENDING_EMAIL_CONFIRMATION';

/**
 * Whether the account detail says this account is still awaiting email confirmation.
 *
 * The value must come from the UC-98 detail response (`rawStatus`), never from the list row, which
 * carries a display-mapped status ("Pending") and can be stale.
 */
export function isPendingEmailConfirmation(rawStatus?: string | null): boolean {
  return String(rawStatus ?? '').trim().toUpperCase() === PENDING_EMAIL_CONFIRMATION_STATUS;
}

/**
 * True when the submit must go to `edit-pending-email` rather than `updatebasicaccountinfo`.
 *
 * Both conditions are required. A pending account whose address is unchanged has no token to
 * re-issue (the backend would refuse with EMAIL_UNCHANGED), and a non-pending account has already
 * proven its address — for it, an email change is the ordinary re-verification flow.
 *
 * Emails are compared after normalization, so a pure casing/whitespace edit is not a change.
 */
export function shouldUsePendingEmailEdit(input: {
  rawStatus?: string | null;
  oldEmail?: string | null;
  newEmail?: string | null;
}): boolean {
  if (!isPendingEmailConfirmation(input.rawStatus)) return false;
  const oldEmail = normalizeAccountEmail(input.oldEmail ?? '');
  const newEmail = normalizeAccountEmail(input.newEmail ?? '');
  return newEmail.length > 0 && oldEmail !== newEmail;
}

export type PendingEmailEditFeedback = {
  kind: PendingEmailEditToastKind;
  message: string;
};

/**
 * What HO is told once the address has been changed.
 *
 * The address change is already committed in every branch, so no message may suggest otherwise; what
 * varies is whether a link actually went out. Announcing "đã gửi" on a SKIPPED or FAILED delivery
 * would send HO away waiting for a confirmation that can never arrive, so those branches name the
 * fallback ("Gửi lại email xác nhận") instead.
 */
export function pendingEmailEditFeedback(
  deliveryStatus: string | null | undefined,
  newEmail: string,
): PendingEmailEditFeedback {
  switch (String(deliveryStatus ?? '').trim().toUpperCase()) {
    case 'SENT':
      return {
        kind: 'success',
        message: `Đã cập nhật email và gửi liên kết xác nhận tới ${newEmail}. `
          + 'Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận.',
      };
    case 'SKIPPED':
      return {
        kind: 'warning',
        message: 'Đã cập nhật email, nhưng email xác nhận không được gửi trong môi trường hiện tại. '
          + 'Bạn có thể sử dụng chức năng “Gửi lại email xác nhận”.',
      };
    case 'FAILED':
      return {
        kind: 'error',
        message: 'Đã cập nhật email nhưng không thể gửi email xác nhận. '
          + 'Tài khoản vẫn ở trạng thái chờ xác nhận email. '
          + 'Vui lòng sử dụng chức năng “Gửi lại email xác nhận”.',
      };
    default:
      return {
        kind: 'warning',
        message: 'Đã cập nhật email nhưng chưa xác định được trạng thái gửi email xác nhận.',
      };
  }
}
