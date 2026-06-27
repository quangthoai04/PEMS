/**
 * "Xem mail đã gửi" — read-only history of the emails already sent for one target (a participant
 * invitation or a logistics request). Self-fetching: give it `open`, a `targetKey` (so it refetches
 * when the target changes) and a `load` callback. Shows subject, recipients, status, time and the
 * exact body_snapshot that was sent. Newest first (the backend orders by sent_email_id desc).
 */
import React, { useEffect, useRef, useState } from 'react';
import { Mail, X, Loader2, AlertCircle, Clock, User2, ChevronDown, ChevronUp, Paperclip, Image as ImageIcon, Download, ExternalLink } from 'lucide-react';
import type { GetSentEmailsResult, SentEmailHistoryItem, SentEmailAttachmentItem } from '../types/delegations.types';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import { resolveCidImages } from '../../emails/utils/inlineImages';

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
  QUEUED:    { label: 'Đang chờ gửi', cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  SENT:      { label: 'Đã gửi', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  DELIVERED: { label: 'Đã nhận', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  FAILED:    { label: 'Gửi lỗi', cls: 'bg-red-50 text-red-700 border-red-200' },
};

function StatusBadge({ status }: { status: string }) {
  const meta = STATUS_META[status?.toUpperCase()] ?? { label: status || '—', cls: 'bg-slate-100 text-slate-600 border-slate-200' };
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
            items.map((it) => <SentEmailCard key={it.sentEmailId} item={it} />)
          )}
        </div>
      </div>
    </div>
  );
}

function SentEmailCard({ item }: { item: SentEmailHistoryItem }) {
  const [showBody, setShowBody] = useState(false);
  const isHtml = (item.bodyFormat ?? 'HTML') === 'HTML';
  const sanitizedBody = isHtml ? sanitizeHtml(item.bodySnapshot) : (item.bodySnapshot ?? '');
  const [renderedBody, setRenderedBody] = useState(sanitizedBody);

  // Resolve inline <img src="cid:.."> back to authenticated blob URLs once the body is expanded.
  useEffect(() => {
    setRenderedBody(sanitizedBody);
    if (!showBody || !isHtml) return;
    const map: Record<string, number> = {};
    (item.attachments || []).forEach((a) => {
      if (a.attachmentType === 'INLINE_IMAGE' && a.contentId) map[a.contentId] = a.fileId;
    });
    if (Object.keys(map).length === 0) return;
    let alive = true;
    resolveCidImages(sanitizedBody, map).then((html) => { if (alive) setRenderedBody(html); });
    return () => { alive = false; };
  }, [showBody, isHtml, sanitizedBody, item.attachments]);
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

      {/* Error (if the send failed) */}
      {(item.emailStatus?.toUpperCase() === 'FAILED' || item.recipients.some((r) => r.errorMessage)) && (
        <div className="mt-2 rounded-lg border border-red-100 bg-red-50/60 px-3 py-1.5 text-[11px] text-red-600">
          {item.recipients.find((r) => r.errorMessage)?.errorMessage || 'Gửi email thất bại.'}
        </div>
      )}

      {/* Attachments (files + inline images). */}
      {(item.attachments?.length ?? 0) > 0 && (
        <div className="mt-3">
          <div className="mb-1.5 flex items-center gap-1 text-[11px] font-bold uppercase tracking-wide text-gray-400">
            <Paperclip className="w-3 h-3" /> Tệp đính kèm ({item.attachments!.length})
          </div>
          <div className="flex flex-wrap gap-2">
            {item.attachments!.map((a) => <AttachmentChip key={a.sentEmailAttachmentId} att={a} />)}
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
                // HTML: sanitized + inline cid images resolved to blob URLs.
                <div className="select-text" dangerouslySetInnerHTML={{ __html: renderedBody }} />
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

/** One attachment as a chip: inline images show a thumbnail, files show an icon + size. */
function AttachmentChip({ att }: { att: SentEmailAttachmentItem }) {
  const name = att.displayName || att.originalFilename || `Tệp #${att.fileId}`;
  const url = att.downloadUrl || att.webViewUrl || null;
  const isInlineImage = att.attachmentType === 'INLINE_IMAGE';
  const thumb = att.thumbnailUrl || att.webViewUrl;
  return (
    <a
      href={url || undefined}
      target={url ? '_blank' : undefined}
      rel="noopener noreferrer"
      className={`group inline-flex max-w-[220px] items-center gap-2 rounded-lg border border-gray-200 bg-gray-50/70 px-2.5 py-1.5 text-xs ${url ? 'hover:border-[#004c91]/40 hover:bg-blue-50/50' : 'cursor-default'}`}
      title={name}
    >
      {isInlineImage && thumb ? (
        <img src={thumb} alt={att.displayName || ''} className="h-7 w-7 shrink-0 rounded object-cover" />
      ) : isInlineImage ? (
        <ImageIcon className="h-4 w-4 shrink-0 text-violet-500" />
      ) : (
        <Paperclip className="h-4 w-4 shrink-0 text-gray-400" />
      )}
      <span className="min-w-0 flex-1">
        <span className="block truncate font-semibold text-gray-700">{name}</span>
        <span className="block text-[10px] text-gray-400">
          {isInlineImage ? 'Ảnh trong nội dung' : 'Đính kèm'}{att.fileSize ? ` · ${formatBytes(att.fileSize)}` : ''}
        </span>
      </span>
      {url && (att.downloadUrl ? <Download className="h-3.5 w-3.5 shrink-0 text-gray-400 group-hover:text-[#004c91]" /> : <ExternalLink className="h-3.5 w-3.5 shrink-0 text-gray-400 group-hover:text-[#004c91]" />)}
    </a>
  );
}
