/**
 * The reply form on a sent-email detail page.
 *
 * <b>What it does not do is the point.</b> A reply carries a fresh envelope: the server addresses it to
 * the author of the message being answered, and the copies are whatever THIS author chooses now. Nothing
 * is carried over from the original — least of all its BCC. Restoring a blind copy into a reply would
 * announce, to everyone on the new message, who had been quietly included on the old one; that is the one
 * thing BCC promises will not happen. So this component never reads `recipients` from the email it is
 * replying to, for any group.
 *
 * TO is shown but not editable and not sent. `ReplytoEmailCommand` has no `To` field at all: it resolves
 * the address from `originalEmailId`, which is what stops a reply being redirected to somebody who was
 * never party to the thread. The address rendered here comes from the same `users` row the command will
 * resolve, so it is a preview of the server's decision rather than a second opinion — but it still counts
 * against the recipient limit and against duplicate checks, because the server counts it.
 *
 * <b>Reply All (G11-H).</b> When `replyAll` is set, the extra recipients are still not this component's
 * to choose: it posts to a different route and the SERVER reads the parent message's visible recipients,
 * excluding its blind copies and the current user. The client sends a mode, never an address list — if it
 * sent the list, a client could name someone who had been on BCC and the server would have no way to
 * know they had not simply been on CC.
 */
import { useCallback, useMemo, useState } from 'react';
import { Send, Loader2 } from 'lucide-react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { emailsApi } from '../api/emailsApi';
import { RecipientChipInput } from './RecipientChipInput';
import { useRecipientLimit } from '../hooks/useRecipientLimit';
import { classifyRecipientError } from '../utils/recipientErrors';
import { attemptIsOver, useIdempotentSend } from '../../reports/hooks/useIdempotentSend';
import {
  countRecipients,
  isUsableLimit,
  normalizeEmail,
  validateEnvelope,
  type EmailRecipientInput,
  type RecipientEnvelope,
  type RecipientGroup,
} from '../types/recipients';

export interface ReplyComposerProps {
  /** `sent_emails.sent_email_id` of the message being answered. */
  originalEmailId: number;
  /**
   * The address the server will resolve as TO — the original sender. Read-only and never posted.
   * `null` when the detail response carried no sender (a system-generated message), which the reply
   * command refuses; the caller should not render the composer in that case.
   */
  resolvedTo: EmailRecipientInput | null;
  onCancel: () => void;
  /** Called after the server accepted the reply. */
  onReplied: () => void;
  /**
   * Reply All rather than Reply. Changes which route is posted to and nothing else the client controls:
   * the additional recipients are read by the server from the parent message. Defaults to false, so an
   * existing caller keeps plain Reply.
   */
  replyAll?: boolean;
}

const QUILL_MODULES = {
  toolbar: [
    ['bold', 'italic', 'underline', 'strike'],
    [{ list: 'ordered' }, { list: 'bullet' }],
    ['link'],
    ['clean'],
  ],
};

/** Quill's "empty" is `<p><br></p>`, which `.trim()` alone reports as content. */
function hasBody(html: string): boolean {
  return html.replace(/<[^>]*>/g, '').replace(/ /g, ' ').trim().length > 0;
}

export function ReplyComposer({ originalEmailId, resolvedTo, onCancel, onReplied, replyAll = false }: ReplyComposerProps) {
  const [body, setBody] = useState('');
  const [cc, setCc] = useState<EmailRecipientInput[]>([]);
  const [bcc, setBcc] = useState<EmailRecipientInput[]>([]);
  const [showCc, setShowCc] = useState(false);
  const [showBcc, setShowBcc] = useState(false);
  const [sending, setSending] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);
  const [recipientErrors, setRecipientErrors] = useState<Partial<Record<RecipientGroup, string>>>({});

  const { limit, status: limitStatus } = useRecipientLimit();
  const { keyFor, complete } = useIdempotentSend();

  /**
   * The envelope as the SERVER will see it: the resolved TO included. Validating without it would let a
   * reply pass the client check at exactly the ceiling and then be refused for one recipient too many,
   * and would miss a CC that duplicates the person being replied to.
   */
  const envelope: RecipientEnvelope = useMemo(
    () => ({ TO: resolvedTo ? [resolvedTo] : [], CC: cc, BCC: bcc }),
    [resolvedTo, cc, bcc],
  );

  const total = countRecipients(envelope);

  const takenElsewhere = useCallback(
    (group: RecipientGroup) => {
      const others: EmailRecipientInput[] = [];
      if (group !== 'TO' && resolvedTo) others.push(resolvedTo);
      if (group !== 'CC') others.push(...cc);
      if (group !== 'BCC') others.push(...bcc);
      return new Set(others.map(r => normalizeEmail(r.email)));
    },
    [resolvedTo, cc, bcc],
  );

  const validate = useCallback((): boolean => {
    const problems = validateEnvelope(envelope, limit);
    const next: Partial<Record<RecipientGroup, string>> = {};
    for (const problem of problems) next[problem.group] ??= problem.message;
    setRecipientErrors(next);
    return problems.length === 0;
  }, [envelope, limit]);

  const handleSend = useCallback(async () => {
    if (sending) return;                       // double-submit guard, before any await
    setFormError(null);
    if (!hasBody(body)) {
      setFormError('Nội dung phản hồi không được để trống.');
      return;
    }
    if (!validate()) return;

    setSending(true);

    // One key names one attempt. Kept across a retry so a reply the browser gave up on is recognised as
    // the same send rather than posted a second time; retired only once the attempt is definitely over.
    const operation = replyAll ? 'email.replyall' : 'email.reply';
    const key = keyFor(operation, originalEmailId);

    try {
      // Only the fields `ReplytoEmailCommand` declares. No `to`, no subject, no carried-over recipients —
      // anything else would either be ignored or would be the client deciding delivery. In Reply All the
      // server derives the visible recipients from the parent message; the client still sends none.
      const payload = {
        originalEmailId,
        body,
        cc: cc.map(r => ({ email: r.email, name: r.name })),
        bcc: bcc.map(r => ({ email: r.email, name: r.name })),
      };

      if (replyAll) await emailsApi.replyAllEmail(payload, key);
      else await emailsApi.replyEmail(payload, key);

      complete(operation, originalEmailId);
      onReplied();
    } catch (error) {
      if (attemptIsOver(error)) complete(operation, originalEmailId);

      // The draft stays exactly as typed: body, CC and BCC are all still here to correct and resend.
      const classified = classifyRecipientError(error, 'Phản hồi thất bại. Vui lòng thử lại.');
      if (classified.group) setRecipientErrors({ [classified.group]: classified.message });
      else setFormError(classified.message);
    } finally {
      setSending(false);
    }
  }, [sending, body, validate, originalEmailId, cc, bcc, onReplied, replyAll, keyFor, complete]);

  return (
    <div className="mt-6 animate-in slide-in-from-top-2 fade-in duration-200">
      <div className="border border-[#cde0f5] rounded-xl overflow-hidden bg-white shadow-sm flex flex-col">
        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50 space-y-2">
          <div className="flex items-center gap-2 text-[14px]">
            <span className="text-gray-500 w-10 shrink-0">Tới:</span>
            <span className="font-normal text-gray-800">{resolvedTo?.name || 'Người gửi email gốc'}</span>
            <span className="text-gray-500">&lt;{resolvedTo?.email ?? '—'}&gt;</span>
            <span className="ml-1 rounded bg-gray-200 px-1.5 py-0.5 text-[11px] font-medium text-gray-600">
              hệ thống xác định
            </span>
          </div>

          {showCc ? (
            <RecipientChipInput
              group="CC"
              value={cc}
              onChange={setCc}
              takenElsewhere={takenElsewhere('CC')}
              disabled={sending}
              externalError={recipientErrors.CC ?? null}
              autoFocus
            />
          ) : null}

          {showBcc ? (
            <RecipientChipInput
              group="BCC"
              value={bcc}
              onChange={setBcc}
              takenElsewhere={takenElsewhere('BCC')}
              disabled={sending}
              externalError={recipientErrors.BCC ?? null}
              autoFocus
            />
          ) : null}

          <div className="flex items-center gap-3 text-[13px]">
            {!showCc && (
              <button type="button" onClick={() => setShowCc(true)}
                className="font-semibold text-[#004c91] hover:underline">Thêm CC</button>
            )}
            {!showBcc && (
              <button type="button" onClick={() => setShowBcc(true)}
                className="font-semibold text-[#004c91] hover:underline">Thêm BCC</button>
            )}
            <span className="ml-auto text-gray-500">
              {limitStatus === 'loading' && 'Đang lấy giới hạn người nhận…'}
              {limitStatus === 'ready' && isUsableLimit(limit) && `${total}/${limit} người nhận`}
              {limitStatus === 'unavailable' &&
                'Chưa lấy được giới hạn người nhận — máy chủ sẽ kiểm tra khi gửi.'}
            </span>
          </div>

          {recipientErrors.TO && (
            <p role="alert" className="text-[13px] font-normal text-red-600">{recipientErrors.TO}</p>
          )}
        </div>

        <div className="bg-white">
          <ReactQuill
            theme="snow"
            value={body}
            onChange={setBody}
            readOnly={sending}
            placeholder="Nhập nội dung phản hồi..."
            className="custom-quill-no-border min-h-[150px]"
            modules={QUILL_MODULES}
          />
        </div>

        {formError && (
          <p role="alert" className="px-4 py-2 text-[13px] font-normal text-red-600">{formError}</p>
        )}

        <div className="bg-white px-4 py-3 flex justify-end gap-3 rounded-b-xl border-t border-gray-100">
          <button
            type="button"
            onClick={onCancel}
            disabled={sending}
            className="px-5 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:border-[#004c91] hover:text-[#004c91] hover:bg-blue-50 font-medium transition-all outline-none disabled:opacity-50"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={handleSend}
            disabled={sending || !hasBody(body)}
            className="bg-[#004c91] hover:bg-[#003a70] text-white px-6 py-2 rounded-lg text-sm font-bold flex items-center gap-2 transition-all shadow-sm outline-none disabled:opacity-50"
          >
            {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
            {sending ? 'Đang gửi…' : 'Gửi'}
          </button>
        </div>
      </div>
    </div>
  );
}

export default ReplyComposer;
