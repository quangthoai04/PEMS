/**
 * Decision logic for the "Gửi lại email xác nhận" action on a pending account.
 *
 * Both rules live here rather than inline in the detail modal because both are easy to get subtly
 * wrong in ways the UI would not reveal: showing the button off a stale list row, or announcing a
 * successful send when the message was never delivered.
 */

export type ResendToastKind = 'success' | 'warning' | 'error';

export type ResendFeedback = {
  kind: ResendToastKind;
  message: string;
};

/**
 * Whether the caller may be offered the resend action.
 *
 * Two independent facts, both required, both from the UC-98 detail response:
 *
 * - `detailStatus` — never the list row, which holds a display-mapped status and can be stale (the
 *   holder may have confirmed in another tab). Until the detail request has resolved (`detailLoaded`)
 *   there is no trustworthy status at all, so the action stays hidden rather than guessing from what
 *   the table happened to render.
 * - `canResend` — the backend's answer to "may THIS caller manage THIS pending account". It replaced
 *   an `isHO` check here: the action is not HO's alone (a Staff Leader runs it for their own campus),
 *   and the conditions that separate the two — sub-role, campus scope, the self-account rule — are not
 *   things this component can evaluate as reliably as the query already has. A missing flag reads as
 *   `false`, so an older backend hides the button rather than offering one that 403s.
 */
export function canResendEmailConfirmation(input: {
  detailLoaded: boolean;
  detailStatus?: string | null;
  canResend?: boolean | null;
}): boolean {
  const normalized = String(input.detailStatus ?? '').trim().toUpperCase();
  return input.detailLoaded
    && normalized === 'PENDING_EMAIL_CONFIRMATION'
    && input.canResend === true;
}

export type ResendResultSummary = {
  kind: ResendToastKind;
  /** What happened, in the reader's language. Never a raw status code. */
  headline: string;
  /** Supporting detail, or null when there is nothing worth adding. */
  detail: string | null;
};

/**
 * The note left in the detail modal after a resend.
 *
 * Distinct from {@link resendDeliveryFeedback}: the toast is a moment's notification, this line
 * stays on screen, so it answers "what happened, and where does that leave me" rather than just
 * announcing. It deliberately never prints the transport status (`SENT` / `SKIPPED` / `FAILED`) —
 * those are wire values, and a reader who has to guess what "SENT" means has been told nothing.
 */
export function resendResultSummary(
  deliveryStatus: string | null | undefined,
  resendCount: number | null,
): ResendResultSummary {
  const attempts = typeof resendCount === 'number' && resendCount > 0
    ? `Số lần đã gửi lại: ${resendCount}.`
    : null;

  switch (String(deliveryStatus ?? '').trim().toUpperCase()) {
    case 'SENT':
      return {
        kind: 'success',
        headline: 'Đã gửi email xác nhận.',
        detail: attempts,
      };
    case 'SKIPPED':
      return {
        kind: 'warning',
        headline: 'Chưa gửi được email.',
        // Naming the cause matters: this one is the environment, not the account — retrying now
        // would fail the same way.
        detail: 'Môi trường hiện tại đang tắt chức năng gửi mail.',
      };
    case 'FAILED':
      return {
        kind: 'error',
        headline: 'Gửi email thất bại.',
        detail: 'Tài khoản vẫn chờ xác nhận. Anh/chị có thể thử gửi lại sau ít phút.',
      };
    default:
      return {
        kind: 'warning',
        headline: 'Chưa xác định được kết quả gửi email.',
        detail: 'Vui lòng kiểm tra lại hộp thư của người dùng trước khi gửi thêm.',
      };
  }
}

/**
 * Maps the backend's delivery outcome to what HO is told.
 *
 * The request succeeding and the mail arriving are different facts: a new token is issued either
 * way, but SKIPPED (no SMTP in this environment) and FAILED mean nobody received a link. Reporting
 * those as success would leave HO waiting on a confirmation that can never come.
 */
export function resendDeliveryFeedback(
  deliveryStatus: string | null | undefined,
  email: string,
): ResendFeedback {
  switch (String(deliveryStatus ?? '').trim().toUpperCase()) {
    case 'SENT':
      return { kind: 'success', message: `Đã gửi lại email xác nhận đến ${email}.` };
    case 'SKIPPED':
      return {
        kind: 'warning',
        message: 'Yêu cầu đã được xử lý nhưng email không được gửi trong môi trường hiện tại.',
      };
    case 'FAILED':
      return {
        kind: 'error',
        message: 'Không thể gửi email xác nhận. Tài khoản vẫn ở trạng thái chờ xác nhận email.',
      };
    default:
      return {
        kind: 'warning',
        message: 'Yêu cầu đã được xử lý nhưng chưa xác định được trạng thái gửi email.',
      };
  }
}
