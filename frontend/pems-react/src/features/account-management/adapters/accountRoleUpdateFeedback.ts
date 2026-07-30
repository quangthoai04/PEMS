/**
 * What the caller is told after a role/identity edit.
 *
 * The account change is committed in every branch below — nothing here may suggest otherwise. What
 * varies is whether a message actually went out, and that is exactly the fact a "success" toast tends
 * to swallow: `SKIPPED` (mail disabled in this environment) and `FAILED` mean nobody received
 * anything, so announcing "đã gửi" would send the caller away waiting on a mail that cannot arrive.
 *
 * Up to TWO messages can be due from one request, and they are reported separately because they carry
 * different consequences. The activation link is what makes a still-pending account usable at all, so
 * its failure has to name the way out ("Gửi lại email xác nhận"); the role notice only informs, so its
 * failure is a warning, not a dead end. Rounding both into one "email" status is how a caller ends up
 * believing an activation link went out because a role notice did.
 *
 * A pending account also keeps being described as pending in every branch: re-roling it does not
 * activate it, and a message that only says "đã cập nhật" invites the caller to assume otherwise.
 */

export type AccountUpdateToastKind = 'success' | 'warning' | 'error';

export type AccountUpdateFeedback = {
  kind: AccountUpdateToastKind;
  message: string;
};

const RESEND_HINT = 'Bạn có thể sử dụng chức năng “Gửi lại email xác nhận”.';
const STILL_PENDING = 'Tài khoản vẫn đang chờ xác nhận email.';

const CONFIRMATION_MAIL = 'email xác nhận';
const ROLE_MAIL = 'email thông báo thay đổi vai trò';
const ADDRESS_MAIL = 'email thông báo thay đổi địa chỉ đăng nhập';

const norm = (value?: string | null) => String(value ?? '').trim().toUpperCase();

const join = (...parts: Array<string | false | undefined>) => parts.filter(Boolean).join(' ');

const capitalize = (text: string) => text.charAt(0).toUpperCase() + text.slice(1);

/**
 * Says what became of a message that did NOT reach anybody. `SKIPPED` and `FAILED` are kept apart
 * because the operator's next move differs: mail is off in this environment (nothing to retry) versus
 * the send was attempted and lost.
 */
function undelivered(status: string, mail: string): string {
  if (status === 'SKIPPED') return `${capitalize(mail)} không được gửi trong môi trường hiện tại.`;
  if (status === 'FAILED') return `Không thể gửi ${mail}.`;
  return `Chưa xác định được trạng thái gửi ${mail}.`;
}

/**
 * An environment with mail switched off is not an error to escalate, and neither is a group where some
 * messages landed. A send that was attempted and lost is.
 */
const severity = (status: string): AccountUpdateToastKind =>
  status === 'SKIPPED' || status === 'PARTIAL' ? 'warning' : 'error';

/** The worse of two non-success outcomes. */
const worse = (a: AccountUpdateToastKind, b: AccountUpdateToastKind): AccountUpdateToastKind =>
  a === 'error' || b === 'error' ? 'error' : 'warning';

/**
 * Maps an `updateAccountRole` response onto the message to show.
 *
 * @param result.emailChanged                       whether the address moved at all
 * @param result.roleChanged                        whether the role/sub-role moved
 * @param result.requiresEmailConfirmation          whether the account was (and remains) pending
 * @param result.emailNotificationStatus            delivery of the address-change message(s)
 * @param result.confirmationEmailNotificationStatus delivery of the activation link, when one was due
 * @param result.roleChangeEmailNotificationStatus   delivery of the role notice, when one was due
 * @param newEmail                                  the address the mail was aimed at, for the wording
 */
export function accountRoleUpdateFeedback(
  result: {
    emailChanged?: boolean | null;
    roleChanged?: boolean | null;
    requiresEmailConfirmation?: boolean | null;
    emailNotificationStatus?: string | null;
    confirmationEmailNotificationStatus?: string | null;
    roleChangeEmailNotificationStatus?: string | null;
  },
  newEmail?: string | null,
): AccountUpdateFeedback {
  const addressStatus = norm(result.emailNotificationStatus);
  const roleStatus = norm(result.roleChangeEmailNotificationStatus);
  // Older responses carried only one status field. Falling back to it keeps a pending account's
  // activation link reported truthfully instead of silently downgraded to "unknown".
  const confirmationStatus = norm(result.confirmationEmailNotificationStatus) || addressStatus;

  const pending = !!result.requiresEmailConfirmation;
  const pendingNote = pending ? STILL_PENDING : '';

  // "A message was due" — not "a field changed". A change with no mail behind it must not produce a
  // sentence about mail, and a status this client has not been taught yet is still a message.
  //
  // Which status answers "did the address mail go out?" depends on the account: for a pending one it is
  // the activation link, for a confirmed one the pair of notices. Reading the wrong field is how a
  // request that mailed an activation link ends up described as having mailed nothing.
  const announced = (status: string) => status !== '' && status !== 'NOT_REQUIRED';
  const emailChanged = !!result.emailChanged;
  const addressMailDue = emailChanged && announced(pending ? confirmationStatus : addressStatus);
  const roleMailDue = !!result.roleChanged && announced(roleStatus);

  // Nothing was mailed: the ordinary role/MSSV/name edit. Nothing is claimed about mail either.
  if (!addressMailDue && !roleMailDue) {
    return {
      kind: 'success',
      message: join('Cập nhật tài khoản thành công.', pendingNote),
    };
  }

  const address = String(newEmail ?? '').trim();

  // ── The role moved and the address did not: one message, and it went to the address the account
  //    already had. A pending account gets one too — it cannot log in yet, but its permissions have
  //    changed and the holder is entitled to know. ──
  if (roleMailDue && !addressMailDue) {
    if (roleStatus === 'SENT') {
      return {
        kind: 'success',
        message: join('Đã cập nhật vai trò và gửi email thông báo tới người dùng.', pendingNote),
      };
    }
    return {
      kind: severity(roleStatus),
      message: join('Đã cập nhật vai trò nhưng', lowerFirst(undelivered(roleStatus, ROLE_MAIL)), pendingNote),
    };
  }

  // ── Only the address moved: the existing behaviour, unchanged. Which message went out depends on
  //    whether the account had ever proven an address — an activation link, or a pair of notices. ──
  if (addressMailDue && !roleMailDue) {
    if (pending) return confirmationOnly(confirmationStatus, address);
    return noticesOnly(addressStatus);
  }

  // ── Both moved. Two independent messages, so "one landed and the other did not" is a real outcome
  //    and has to be said plainly rather than rounded to success or to failure. ──
  if (pending) {
    const confirmationSent = confirmationStatus === 'SENT';
    const roleSent = roleStatus === 'SENT';

    if (confirmationSent && roleSent) {
      return {
        kind: 'success',
        message: address
          ? `Đã cập nhật tài khoản, gửi email xác nhận và thông báo thay đổi vai trò tới ${address}. `
            + 'Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận email.'
          : 'Đã cập nhật tài khoản, gửi email xác nhận và thông báo thay đổi vai trò tới địa chỉ email mới. '
            + 'Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận email.',
      };
    }

    // The activation link is the one that decides whether this account can ever be used, so its
    // outcome drives both the wording and the severity.
    if (confirmationSent) {
      return {
        kind: 'warning',
        message: join(
          'Đã cập nhật tài khoản và gửi email xác nhận.',
          undelivered(roleStatus, ROLE_MAIL),
          STILL_PENDING,
        ),
      };
    }
    if (roleSent) {
      return {
        kind: severity(confirmationStatus),
        message: join(
          'Đã cập nhật tài khoản và gửi thông báo thay đổi vai trò.',
          undelivered(confirmationStatus, CONFIRMATION_MAIL),
          STILL_PENDING,
          RESEND_HINT,
        ),
      };
    }
    return {
      kind: severity(confirmationStatus),
      message: join(
        'Đã cập nhật tài khoản nhưng không gửi được email xác nhận và email thông báo thay đổi vai trò.',
        STILL_PENDING,
        RESEND_HINT,
      ),
    };
  }

  // An account that HAS confirmed an address: notices about the address, plus the role notice.
  const addressSent = addressStatus === 'SENT';
  const roleSent = roleStatus === 'SENT';

  if (addressSent && roleSent) {
    return {
      kind: 'success',
      message: 'Đã cập nhật tài khoản, gửi email thông báo thay đổi địa chỉ đăng nhập '
        + 'và thông báo thay đổi vai trò.',
    };
  }
  if (addressSent) {
    return {
      kind: severity(roleStatus),
      message: join(
        'Đã cập nhật tài khoản và gửi email thông báo thay đổi địa chỉ đăng nhập.',
        undelivered(roleStatus, ROLE_MAIL),
      ),
    };
  }
  if (roleSent) {
    return {
      kind: severity(addressStatus),
      message: join(
        'Đã cập nhật tài khoản và gửi thông báo thay đổi vai trò.',
        addressStatus === 'PARTIAL'
          ? 'Một số email thông báo thay đổi địa chỉ đăng nhập chưa gửi được.'
          : undelivered(addressStatus, ADDRESS_MAIL),
      ),
    };
  }
  return {
    kind: worse(severity(addressStatus), severity(roleStatus)),
    message: 'Đã cập nhật tài khoản nhưng các email thông báo chưa gửi được.',
  };
}

/** Lowercases the first letter so a sentence fragment can follow "nhưng". */
function lowerFirst(text: string): string {
  return text.charAt(0).toLowerCase() + text.slice(1);
}

/** Pending account, address moved, role unchanged — the message is an ACTIVATION link. */
function confirmationOnly(status: string, address: string): AccountUpdateFeedback {
  switch (status) {
    case 'SENT':
      return {
        kind: 'success',
        message: address
          ? `Đã cập nhật tài khoản và gửi liên kết xác nhận đến ${address}. `
            + 'Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận email.'
          : 'Đã cập nhật tài khoản và gửi liên kết xác nhận đến địa chỉ email mới. '
            + 'Tài khoản sẽ được kích hoạt sau khi người nhận hoàn tất xác nhận email.',
      };
    case 'SKIPPED':
      return {
        kind: 'warning',
        message: 'Đã cập nhật tài khoản nhưng email xác nhận không được gửi trong môi trường hiện tại. '
          + RESEND_HINT,
      };
    case 'FAILED':
      return {
        kind: 'error',
        message: 'Đã cập nhật tài khoản nhưng không thể gửi email xác nhận. '
          + 'Tài khoản vẫn ở trạng thái chờ xác nhận email. ' + RESEND_HINT,
      };
    default:
      return {
        kind: 'warning',
        message: 'Đã cập nhật tài khoản nhưng chưa xác định được trạng thái gửi email xác nhận. '
          + RESEND_HINT,
      };
  }
}

/** Confirmed account, address moved, role unchanged — what went out are notices, not a link. */
function noticesOnly(status: string): AccountUpdateFeedback {
  switch (status) {
    case 'SENT':
      return { kind: 'success', message: 'Đã cập nhật tài khoản và gửi email thông báo thay đổi.' };
    case 'PARTIAL':
      return {
        kind: 'warning',
        message: 'Đã cập nhật tài khoản nhưng một số email thông báo chưa gửi được.',
      };
    case 'SKIPPED':
      return {
        kind: 'warning',
        message: 'Đã cập nhật tài khoản nhưng email thông báo không được gửi trong môi trường hiện tại.',
      };
    case 'FAILED':
      return {
        kind: 'error',
        message: 'Đã cập nhật tài khoản nhưng không thể gửi email thông báo.',
      };
    default:
      return {
        kind: 'warning',
        message: 'Đã cập nhật tài khoản nhưng chưa xác định được trạng thái gửi email thông báo.',
      };
  }
}
