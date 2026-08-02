/**
 * "Xem mail đã gửi" — read-only history of the emails already sent for one target (a participant
 * invitation or a logistics request). Self-fetching: give it `open`, a `targetKey` (so it refetches
 * when the target changes) and a `load` callback. Shows subject, recipients, status, time and the
 * exact body_snapshot that was sent. Newest first (the backend orders by sent_email_id desc).
 */
import React, { useEffect, useRef, useState } from 'react';
import { Mail, X, Loader2, AlertCircle, Clock, User2, ChevronDown, ChevronUp, Paperclip, Image as ImageIcon, Download, ExternalLink, MousePointerClick } from 'lucide-react';
import type { GetSentEmailsResult, SentEmailHistoryItem, SentEmailAttachmentItem, SentEmailActionTokenItem } from '../types/delegations.types';
import { sanitizeHtml, sanitizeSentEmailPreviewHtml } from '../../../shared/security/sanitizeHtml';
import { resolveCidImages } from '../../emails/utils/inlineImages';
import { FileAttachmentItem } from '../../../shared/components/files/FileAttachmentItem';
import { FilePreviewModal } from '../../../shared/components/files/FilePreviewModal';
import type { PreviewableFile } from '../../../shared/components/files/filePreviewKind';

interface Props {
  open: boolean;
  title: string;
  subtitle?: string | null;
  /** Changes when the target changes → triggers a refetch while the modal stays open. */
  targetKey: string | number | null;
  load: () => Promise<GetSentEmailsResult>;
  onClose: () => void;
}

// Delivery / send status → Vietnamese label + tailwind classes.
const STATUS_META: Record<string, { label: string; cls: string }> = {
  QUEUED:          { label: 'Đang chờ gửi', cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  SENT:            { label: 'Đã gửi', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  DELIVERED:       { label: 'Đã nhận', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  PARTIAL_FAILED:  { label: 'Gửi lỗi 1 phần', cls: 'bg-orange-50 text-orange-700 border-orange-200' },
  FAILED:          { label: 'Gửi lỗi', cls: 'bg-red-50 text-red-700 border-red-200' },
  BOUNCED:         { label: 'Bị trả về', cls: 'bg-red-50 text-red-700 border-red-200' },
};

function StatusBadge({ status }: { status: string }) {
  const meta = STATUS_META[status?.toUpperCase()] ?? { label: status || '—', cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  return (
    <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
      {meta.label}
    </span>
  );
}

// email_action_tokens.result_status → Vietnamese label + classes (the action-button state).
const TOKEN_STATUS_META: Record<string, { label: string; cls: string }> = {
  PENDING:           { label: 'Chưa phản hồi', cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  SUCCESS:           { label: 'Đã phản hồi', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  ALREADY_RESPONDED: { label: 'Đã phản hồi trước đó', cls: 'bg-slate-100 text-slate-500 border-slate-200' },
  EXPIRED:           { label: 'Hết hạn', cls: 'bg-gray-100 text-gray-500 border-gray-200' },
  INVALID:           { label: 'Không hợp lệ', cls: 'bg-red-50 text-red-700 border-red-200' },
  FAILED:            { label: 'Lỗi xử lý', cls: 'bg-red-50 text-red-700 border-red-200' },
};
// intended_action → button label shown in the email.
const ACTION_LABEL: Record<string, string> = {
  ACCEPT: 'Chấp nhận', DECLINE: 'Từ chối', NEGOTIATE: 'Đề xuất thay đổi',
  APPROVE_PROPOSAL: 'Chấp nhận đề xuất', REJECT_PROPOSAL: 'Từ chối đề xuất',
  CONFIRM_BORROW: 'Xác nhận ký mượn', CONFIRM_RETURN: 'Xác nhận ký trả',
};

function TokenStatusBadge({ status }: { status: string }) {
  const meta = TOKEN_STATUS_META[status?.toUpperCase()] ?? { label: status || '—', cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  return (
    <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
      {meta.label}
    </span>
  );
}

function formatBytes(bytes?: number | null): string {
  if (bytes == null || bytes < 0) return '';
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  if (kb < 1024) return `${kb.toFixed(kb < 10 ? 1 : 0)} KB`;
  return `${(kb / 1024).toFixed(1)} MB`;
}

// "yyyy-MM-ddTHH:mm[:ss]" → "HH:mm dd/MM/yyyy" via pure string slicing (no Date / no TZ shift).
function fmtDateTime(value?: string | null): string {
  if (!value) return '—';
  const [d, t] = value.replace(' ', 'T').split('T');
  if (!d) return value;
  const [y, m, day] = d.split('-');
  const hm = (t || '').slice(0, 5);
  if (!y || !m || !day) return value;
  return hm ? `${hm} ${day}/${m}/${y}` : `${day}/${m}/${y}`;
}

export function SentEmailsModal({ open, title, subtitle, targetKey, load, onClose }: Props) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [items, setItems] = useState<SentEmailHistoryItem[]>([]);
  /** Lifted out of the cards so the whole history shares ONE preview, not one per message. */
  const [previewFile, setPreviewFile] = useState<PreviewableFile | null>(null);
  const loadRef = useRef(load);
  loadRef.current = load;

  useEffect(() => {
    if (!open) return;
    let alive = true;
    setLoading(true);
    setError(null);
    (async () => {
      try {
        const res = await loadRef.current();
        if (alive) setItems(res.items || []);
      } catch {
        if (alive) { setItems([]); setError('Không thể tải lịch sử email. Vui lòng thử lại.'); }
      } finally {
        if (alive) setLoading(false);
      }
    })();
    return () => { alive = false; };
  }, [open, targetKey]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-black/40 p-4" onMouseDown={onClose}>
      <div
        className="flex w-full max-w-2xl max-h-[88vh] flex-col overflow-hidden rounded-2xl bg-white shadow-2xl"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-start justify-between border-b border-gray-100 px-6 py-4">
          <div className="min-w-0">
            <h3 className="flex items-center gap-2 text-base font-bold text-[#004c91]">
              <Mail className="w-5 h-5" /> Email đã gửi
            </h3>
            <p className="mt-0.5 truncate text-xs text-gray-500">{title}{subtitle ? ` · ${subtitle}` : ''}</p>
          </div>
          <button type="button" onClick={onClose} className="rounded-lg p-1.5 text-gray-400 outline-none hover:bg-gray-100 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="space-y-3 overflow-y-auto px-6 py-4">
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-gray-500">
              <Loader2 className="w-4 h-4 animate-spin" /> Đang tải lịch sử email...
            </div>
          ) : error ? (
            <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-600">
              <AlertCircle className="w-4 h-4 shrink-0" /> {error}
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center gap-2 py-10 text-center text-sm text-gray-400">
              <Mail className="w-8 h-8 text-gray-300" />
              Chưa có email nào được gửi cho người nhận này.
            </div>
          ) : (
            items.map((it) => (
              <SentEmailCard key={it.sentEmailId} item={it} onPreview={setPreviewFile} />
            ))
          )}
        </div>
      </div>
      <FilePreviewModal
        open={previewFile != null}
        file={previewFile}
        onClose={() => setPreviewFile(null)}
      />
    </div>
  );
}

function SentEmailCard({
  item, onPreview,
}: { item: SentEmailHistoryItem; onPreview: (file: PreviewableFile) => void }) {
  const [showBody, setShowBody] = useState(false);
  const isHtml = (item.bodyFormat ?? 'HTML') === 'HTML';
  const sanitizedBody = isHtml ? sanitizeHtml(item.bodySnapshot) : (item.bodySnapshot ?? '');
  const neutralizedBody = isHtml ? sanitizeSentEmailPreviewHtml(sanitizedBody) : sanitizedBody;
  const [renderedBody, setRenderedBody] = useState(neutralizedBody);

  // Resolve inline <img src="cid:.."> back to authenticated blob URLs once the body is expanded.
  useEffect(() => {
    setRenderedBody(neutralizedBody);
    if (!showBody || !isHtml) return;
    const map: Record<string, number> = {};
    (item.attachments || []).forEach((a) => {
      if (a.attachmentType === 'INLINE_IMAGE' && a.contentId) map[a.contentId] = a.fileId;
    });
    if (Object.keys(map).length === 0) return;
    let alive = true;
    resolveCidImages(neutralizedBody, map).then((html) => { if (alive) setRenderedBody(html); });
    return () => { alive = false; };
  }, [showBody, isHtml, neutralizedBody, item.attachments]);
  return (
    <div className="rounded-xl border border-gray-200 bg-white p-4 shadow-sm">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="truncate text-sm font-bold text-gray-800">{item.subject || '(Không có tiêu đề)'}</div>
          <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-gray-500">
            <span className="inline-flex items-center gap-1"><Clock className="w-3 h-3" /> {fmtDateTime(item.sentAt || item.createdAt)}</span>
            {item.sentByName && <span className="inline-flex items-center gap-1"><User2 className="w-3 h-3" /> {item.sentByName}</span>}
            {item.templateName && <span className="text-gray-400">{item.templateName}</span>}
          </div>
        </div>
        <StatusBadge status={item.emailStatus} />
      </div>

      {/* Recipients */}
      {item.recipients.length > 0 && (
        <div className="mt-3 space-y-1.5">
          {item.recipients.map((r, i) => (
            <div key={i} className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-gray-50/70 px-3 py-1.5">
              <div className="min-w-0 text-xs">
                <span className="font-semibold text-gray-700">{r.recipientName || r.recipientEmail}</span>
                <span className="text-gray-500"> · {r.recipientEmail}</span>
                {r.recipientType && r.recipientType !== 'TO' && <span className="ml-1 text-gray-400">({r.recipientType})</span>}
              </div>
              <StatusBadge status={r.deliveryStatus} />
            </div>
          ))}
        </div>
      )}

      {/* Error (if the send failed or partially failed) */}
      {(['FAILED', 'PARTIAL_FAILED'].includes(item.emailStatus?.toUpperCase()) || item.recipients.some((r) => r.errorMessage)) && (
        <div className="mt-2 space-y-1">
          {item.recipients.filter((r) => r.errorMessage).map((r, i) => (
            <div key={i} className="rounded-lg border border-red-100 bg-red-50/60 px-3 py-1.5 text-[11px] text-red-600">
              <span className="font-semibold">{r.recipientEmail}:</span> {r.errorMessage}
            </div>
          ))}
          {item.recipients.filter((r) => r.errorMessage).length === 0 && (
            <div className="rounded-lg border border-red-100 bg-red-50/60 px-3 py-1.5 text-[11px] text-red-600">
              Gửi email thất bại.
            </div>
          )}
        </div>
      )}

      {/* Attachments (files + inline images). */}
      {(item.attachments?.length ?? 0) > 0 && (
        <div className="mt-3">
          <div className="mb-1.5 flex items-center gap-1 text-[11px] font-bold uppercase tracking-wide text-gray-400">
            <Paperclip className="w-3 h-3" /> Tệp đính kèm ({item.attachments!.length})
          </div>
          <div className="flex flex-wrap gap-2">
            {item.attachments!.map((a) => (
              <AttachmentChip key={a.sentEmailAttachmentId} att={a} onPreview={onPreview} />
            ))}
          </div>
        </div>
      )}

      {/* Action-button tokens — whether the recipient has clicked / let the link expire. */}
      {(item.actionTokens?.length ?? 0) > 0 && (
        <div className="mt-3">
          <div className="mb-1.5 flex items-center gap-1 text-[11px] font-bold uppercase tracking-wide text-gray-400">
            <MousePointerClick className="w-3 h-3" /> Nút thao tác trong email
          </div>
          <div className="space-y-1.5">
            {item.actionTokens!.map((t, i) => <ActionTokenRow key={i} token={t} />)}
          </div>
        </div>
      )}

      {/* Body snapshot (the exact content that was sent) — collapsed by default. */}
      {item.bodySnapshot && (
        <div className="mt-3">
          <button
            type="button"
            onClick={() => setShowBody((s) => !s)}
            className="inline-flex items-center gap-1 text-xs font-semibold text-[#004c91] outline-none hover:underline"
          >
            {showBody ? <ChevronUp className="w-3.5 h-3.5" /> : <ChevronDown className="w-3.5 h-3.5" />}
            {showBody ? 'Ẩn nội dung email' : 'Xem nội dung email đã gửi'}
          </button>
          {showBody && (
            <div className="mt-2 max-h-72 overflow-y-auto rounded-lg border border-gray-200 bg-white p-2">
              {!isHtml ? (
                // Plain text: keep line breaks, never interpret as HTML.
                <div className="whitespace-pre-wrap break-words text-sm text-gray-700">{item.bodySnapshot}</div>
              ) : (
                // HTML: sanitized + neutralized action tokens + inline cid images resolved to blob URLs.
                <div className="relative">
                  <div className="mb-2 rounded bg-blue-50 px-2 py-1 text-[11px] font-medium text-[#004c91] border border-blue-100 flex items-center gap-1.5">
                    <AlertCircle className="w-3.5 h-3.5 shrink-0" /> Đây là bản xem lại email đã gửi. Các nút thao tác đã bị vô hiệu hóa trong chế độ xem trước.
                  </div>
                  <div className="pems-email-body select-text pointer-events-none" dangerouslySetInnerHTML={{ __html: renderedBody }} />
                </div>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/** One action button embedded in the email + its one-time-token status (clicked / pending / expired). */
function ActionTokenRow({ token }: { token: SentEmailActionTokenItem }) {
  const label = ACTION_LABEL[token.intendedAction?.toUpperCase()] ?? token.intendedAction;
  const respondedAt = token.resultStatus === 'SUCCESS' ? token.usedAt : null;
  return (
    <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg bg-gray-50/70 px-3 py-1.5 text-xs">
      <span className="font-semibold text-gray-700">{label}</span>
      <div className="flex items-center gap-2">
        {respondedAt && <span className="text-gray-400">{fmtDateTime(respondedAt)}</span>}
        <TokenStatusBadge status={token.resultStatus} />
      </div>
    </div>
  );
}

/**
 * One attachment of a message that has already gone out — viewable and re-downloadable, never
 * editable.
 *
 * <p>This used to be an <c>&lt;a href={att.downloadUrl}&gt;</c>. Every EMAIL_ATTACHMENT is stored on
 * Google Drive, so that href was a Drive <c>webContentLink</c>: clicking it took the reader to
 * Google's "request access" page (the browser cannot attach our bearer token to a plain link, and the
 * reader has no Drive account of ours), while the payload handed the provider's file id to anyone
 * reading it. Both ends are fixed — the backend now returns only our own routes, and the fetch goes
 * through the authenticated client.</p>
 */
function AttachmentChip({
  att, onPreview,
}: { att: SentEmailAttachmentItem; onPreview: (file: PreviewableFile) => void }) {
  const name = att.displayName || att.originalFilename || `Tệp #${att.fileId}`;
  const isInlineImage = att.attachmentType === 'INLINE_IMAGE';

  return (
    <FileAttachmentItem
      data-testid="sent-email-attachment"
      file={{
        fileId: att.fileId,
        name,
        mimeType: att.mimeType,
        size: att.fileSize,
      }}
      onPreview={onPreview}
      hint={isInlineImage ? 'Ảnh trong nội dung' : 'Đính kèm'}
    />
  );
}
