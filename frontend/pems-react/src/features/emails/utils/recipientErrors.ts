/**
 * Turns a rejected recipient request into the field it belongs on.
 *
 * Compose and reply post to different commands but are refused by the same `EmailRecipientValidator`,
 * so they must read a refusal the same way. When each screen had its own `catch`, one of them showed
 * "Gửi email thất bại" for a duplicate CC that the other pinned to the CC field — the same server
 * response, two different stories.
 *
 * Matching is on `errorCode` (the stable strings in `EmailErrorCodes`), never on message text: the text
 * is Vietnamese prose, and rewording it must not change what the UI does with it. The group is only
 * inferred from the message where the code alone cannot say which of the three lists was at fault, and
 * an unattributable failure is reported at form level rather than guessed onto a field.
 */
import {
  EMAIL_ERROR_CODES,
  RECIPIENT_GROUP_LABELS,
  type RecipientGroup,
} from '../types/recipients';

export interface ClassifiedRecipientError {
  /** The field to show it on, or undefined when it belongs to the form as a whole. */
  group?: RecipientGroup;
  message: string;
}

const FALLBACK = 'Không thể gửi email. Vui lòng thử lại.';

export function classifyRecipientError(
  error: unknown,
  fallbackMessage: string = FALLBACK,
): ClassifiedRecipientError {
  const data = (error as { response?: { data?: { errorCode?: string; code?: string; message?: string } } })
    ?.response?.data;
  const code = data?.errorCode ?? data?.code;
  const message = data?.message;

  const groupFromMessage = (): RecipientGroup => {
    const text = message ?? '';
    if (text.includes(RECIPIENT_GROUP_LABELS.BCC)) return 'BCC';
    if (text.includes(RECIPIENT_GROUP_LABELS.CC)) return 'CC';
    return 'TO';
  };

  switch (code) {
    case EMAIL_ERROR_CODES.recipientRequired:
    case EMAIL_ERROR_CODES.recipientLimitExceeded:
      // Both are about the envelope as a whole; TO is where the summary lives.
      return { group: 'TO', message: message ?? 'Danh sách người nhận không hợp lệ.' };
    case EMAIL_ERROR_CODES.recipientInvalid:
    case EMAIL_ERROR_CODES.recipientDuplicate:
    case EMAIL_ERROR_CODES.recipientCrossGroupDuplicate:
    case EMAIL_ERROR_CODES.headerInvalid:
      return { group: groupFromMessage(), message: message ?? 'Người nhận không hợp lệ.' };
    default:
      return { message: message ?? fallbackMessage };
  }
}
