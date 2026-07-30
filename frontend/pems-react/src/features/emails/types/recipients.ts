/**
 * The recipient contract shared by compose, draft, preview and reply.
 *
 * This mirrors the backend deliberately and narrowly. `SendEmailCommand` carries three separate lists
 * (`To` / `Cc` / `Bcc`) of `EmailRecipientDto { Email, Name }`, and `ReplytoEmailCommand` carries
 * `Cc` / `Bcc` of `EmailRecipientInput { Email, Name }` — the same shape under a different name. The
 * screens used to post `any[]`, which is how a CC could be collected by the UI and silently dropped or
 * reshaped before it reached the wire: nothing on the way there knew what a recipient was.
 *
 * The rules below are a copy of `EmailRecipientValidator` (backend). They exist so the person typing
 * gets told at the field instead of after a round trip — NOT so the frontend can decide what is
 * acceptable. The backend re-validates every payload and stays the authority; where the two could
 * disagree this file is deliberately the more permissive one, so a legitimate address is never blocked
 * client-side. An illegitimate one is refused by the server either way.
 */

/** One addressee, exactly as the API serialises it (camelCase of `EmailRecipientDto`). */
export interface EmailRecipientInput {
  email: string;
  /** Optional friendly name. Like the address it must not contain CR/LF — both land in MIME headers. */
  name?: string;
}

/** The three envelope groups, matching `sent_email_recipients.recipient_type`. */
export type RecipientGroup = 'TO' | 'CC' | 'BCC';

/** Vietnamese labels used in messages, matching the wording the backend produces. */
export const RECIPIENT_GROUP_LABELS: Record<RecipientGroup, string> = {
  TO: 'Đến',
  CC: 'CC',
  BCC: 'BCC',
};

/**
 * Stable error codes from `EmailErrorCodes`. Backend failures are matched on these, never on message
 * text — the text is Vietnamese prose and changing a word must not change what the UI does with it.
 */
export const EMAIL_ERROR_CODES = {
  recipientRequired: 'EMAIL_RECIPIENT_REQUIRED',
  recipientInvalid: 'EMAIL_RECIPIENT_INVALID',
  recipientDuplicate: 'EMAIL_RECIPIENT_DUPLICATE',
  recipientCrossGroupDuplicate: 'EMAIL_RECIPIENT_CROSS_GROUP_DUPLICATE',
  recipientLimitExceeded: 'EMAIL_RECIPIENT_LIMIT_EXCEEDED',
  headerInvalid: 'EMAIL_HEADER_INVALID',
} as const;

/**
 * How much this client knows about the recipient ceiling.
 *
 * There is deliberately no default number here. A constant `50` copied into the frontend is the exact
 * drift `EmailRecipientOptions` warns about: raise the limit in configuration and the UI keeps refusing
 * at the old one, or lower it and the UI keeps promising the old one. `null` means "not known yet or
 * not available", and the UI says so rather than inventing a ceiling.
 *
 * A configured value of zero or less is treated as unavailable too: that is what the server would
 * enforce (every envelope exceeds it), so showing "3/0" would be nonsense — the notice is the honest
 * rendering, and the backend still refuses the send.
 */
export type RecipientLimit = number | null;

export function isUsableLimit(limit: RecipientLimit): limit is number {
  return typeof limit === 'number' && Number.isFinite(limit) && limit > 0;
}

/** Characters that would let a value break out of its header and inject another one. */
const HEADER_BREAKERS = ['\r', '\n', '\0'];

export function hasHeaderBreak(value: string): boolean {
  return HEADER_BREAKERS.some(c => value.includes(c));
}

/** Lower-cased, trimmed address used for duplicate detection. The original casing is what we keep. */
export function normalizeEmail(email: string): string {
  return email.trim().toLowerCase();
}

/**
 * Structural check, copied rule-for-rule from `EmailRecipientValidator.IsWellFormed`:
 * exactly one '@', a non-empty local part, and a dotted domain of at least 3 characters with no
 * leading/trailing dot and no '..'.
 *
 * The backend follows these with `MailAddress.TryCreate`, which has no faithful equivalent here. We do
 * not approximate it with a regex: a hand-rolled address regex is how valid mailboxes start getting
 * rejected. Anything that passes here and fails there comes back as EMAIL_RECIPIENT_INVALID and is
 * shown on the offending field.
 */
export function isWellFormedEmail(email: string): boolean {
  const value = email.trim();
  if (value.length === 0) return false;
  if (hasHeaderBreak(value)) return false;

  const parts = value.split('@');
  if (parts.length !== 2) return false;

  const [local, domain] = parts;
  if (local.length === 0 || domain.length < 3) return false;
  if (!domain.includes('.')) return false;
  if (domain.startsWith('.') || domain.endsWith('.') || domain.includes('..')) return false;
  if (/\s/.test(value)) return false;

  return true;
}

/** A recipient list keyed by group — the shape compose and reply both hold in state. */
export type RecipientEnvelope = Record<RecipientGroup, EmailRecipientInput[]>;

export const emptyEnvelope = (): RecipientEnvelope => ({ TO: [], CC: [], BCC: [] });

export interface EnvelopeProblem {
  group: RecipientGroup;
  code: string;
  message: string;
  /** The address at fault, when the problem is about one address. */
  email?: string;
}

/**
 * Validates a whole envelope the way the server will, and returns every problem rather than only the
 * first. The backend throws on the first rule broken because it only needs to refuse; a person filling
 * in three fields needs to see all of what is wrong.
 *
 * `requireTo` is false for reply, whose TO is the original sender and is not editable.
 */
export function validateEnvelope(
  envelope: RecipientEnvelope,
  maxRecipients: RecipientLimit,
  requireTo = true,
): EnvelopeProblem[] {
  const problems: EnvelopeProblem[] = [];
  const groups: RecipientGroup[] = ['TO', 'CC', 'BCC'];

  for (const group of groups) {
    const seen = new Set<string>();
    for (const recipient of envelope[group]) {
      const email = recipient.email.trim();
      if (email.length === 0) continue;

      const label = RECIPIENT_GROUP_LABELS[group];

      if (recipient.name && hasHeaderBreak(recipient.name)) {
        problems.push({
          group, email, code: EMAIL_ERROR_CODES.headerInvalid,
          message: `Giá trị của ${label} chứa ký tự xuống dòng không hợp lệ.`,
        });
        continue;
      }

      if (!isWellFormedEmail(email)) {
        problems.push({
          group, email, code: EMAIL_ERROR_CODES.recipientInvalid,
          message: `Địa chỉ email không hợp lệ ở mục ${label}: '${email}'.`,
        });
        continue;
      }

      const key = normalizeEmail(email);
      if (seen.has(key)) {
        problems.push({
          group, email, code: EMAIL_ERROR_CODES.recipientDuplicate,
          message: `Địa chỉ '${email}' bị lặp trong cùng mục ${label}.`,
        });
        continue;
      }
      seen.add(key);
    }
  }

  // Cross-group duplicates, in the same three pairings the backend checks.
  const pairs: Array<[RecipientGroup, RecipientGroup]> = [['TO', 'CC'], ['TO', 'BCC'], ['CC', 'BCC']];
  for (const [first, second] of pairs) {
    const firstSet = new Set(envelope[first].map(r => normalizeEmail(r.email)));
    for (const recipient of envelope[second]) {
      if (!firstSet.has(normalizeEmail(recipient.email))) continue;
      problems.push({
        group: second,
        email: recipient.email,
        code: EMAIL_ERROR_CODES.recipientCrossGroupDuplicate,
        message:
          `Địa chỉ '${recipient.email}' xuất hiện ở cả mục ${RECIPIENT_GROUP_LABELS[first]} và ` +
          `${RECIPIENT_GROUP_LABELS[second]}. Mỗi người nhận chỉ được thuộc một mục.`,
      });
    }
  }

  if (requireTo && envelope.TO.length === 0) {
    problems.push({
      group: 'TO', code: EMAIL_ERROR_CODES.recipientRequired,
      message: 'Email phải có ít nhất một người nhận ở mục Đến.',
    });
  }

  // Only checked when the server told us the ceiling. When it did not, this rule is simply not
  // evaluated here and the send is refused by `EmailRecipientValidator` instead — the limit is never
  // guessed, and never silently treated as "no limit".
  const total = countRecipients(envelope);
  if (isUsableLimit(maxRecipients) && total > maxRecipients) {
    problems.push({
      group: 'TO', code: EMAIL_ERROR_CODES.recipientLimitExceeded,
      message: `Tổng số người nhận (${total}) vượt quá giới hạn cho phép (${maxRecipients}).`,
    });
  }

  return problems;
}

export function countRecipients(envelope: RecipientEnvelope): number {
  return envelope.TO.length + envelope.CC.length + envelope.BCC.length;
}

/**
 * Splits pasted text into candidate addresses on comma, semicolon, whitespace and newlines.
 * Splitting is all this does — each piece still goes through {@link validateEnvelope}, so a paste
 * cannot introduce an address the typed path would have refused.
 */
export function splitPastedRecipients(raw: string): string[] {
  return raw
    .split(/[,;\s\r\n]+/)
    .map(part => part.trim())
    .filter(part => part.length > 0);
}
