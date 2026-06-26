/**
 * Reusable editable "Xem trước email" modal. Subject/body are editable; for action templates the
 * accept/decline (or detail) block is system-controlled (read-only) and gets real tokens only on the
 * actual send. Presentational/controlled — the parent owns the state + handlers. Used by the
 * participant-invite flow and the logistics request/assignment flows.
 */
import React from 'react';
import { Eye, X, Loader2, AlertCircle, Send } from 'lucide-react';

export interface EmailPreviewModalProps {
  open: boolean;
  loading: boolean;
  sending: boolean;
  error: string | null;
  subject: string;
  body: string;
  isActionTemplate: boolean;
  systemActionDescription?: string | null;
  lockedActionBlockHtml?: string | null;
  /** When true, the primary "send" button is shown (a concrete target is bound). */
  canSend: boolean;
  /** Label for the primary send button, e.g. "Mời với nội dung này" / "Gửi với nội dung này". */
  sendLabel: string;
  onSubjectChange: (value: string) => void;
  onBodyChange: (value: string) => void;
  onClose: () => void;
  onRestore: () => void;
  onSend: () => void;
}

export function EmailPreviewModal({
  open, loading, sending, error, subject, body, isActionTemplate,
  systemActionDescription, lockedActionBlockHtml, canSend, sendLabel,
  onSubjectChange, onBodyChange, onClose, onRestore, onSend,
}: EmailPreviewModalProps) {
  if (!open) return null;
  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-4" onMouseDown={sending ? undefined : onClose}>
      <div
        className="w-full max-w-2xl max-h-[88vh] overflow-hidden rounded-2xl bg-white shadow-2xl flex flex-col"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h3 className="flex items-center gap-2 text-base font-bold text-[#004c91]">
            <Eye className="w-5 h-5" /> Xem trước email
          </h3>
          <button type="button" onClick={onClose} disabled={sending} className="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600 outline-none disabled:opacity-40">
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="overflow-y-auto px-6 py-4 space-y-4">
          {loading ? (
            <div className="flex items-center gap-2 py-8 text-sm text-gray-500">
              <Loader2 className="w-4 h-4 animate-spin" /> Đang tải bản xem trước...
            </div>
          ) : error ? (
            <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-600">
              <AlertCircle className="w-4 h-4 shrink-0" /> {error}
            </div>
          ) : (
            <>
              <div>
                <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Tiêu đề</label>
                <input
                  type="text"
                  maxLength={255}
                  value={subject}
                  onChange={(e) => onSubjectChange(e.target.value)}
                  className="mt-1 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm font-semibold text-gray-800 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20"
                />
              </div>
              <div>
                <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Nội dung email</label>
                <p className="mt-0.5 mb-1 text-[11px] text-gray-400">
                  Soạn nội dung dạng văn bản dễ đọc — hệ thống tự định dạng khi gửi. Để dòng trống để tách đoạn.
                </p>
                <textarea
                  value={body}
                  onChange={(e) => onBodyChange(e.target.value)}
                  placeholder="Soạn nội dung email..."
                  className="mt-1 w-full min-h-[200px] resize-y rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm leading-relaxed text-gray-800 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20"
                />
              </div>
              {/* Read-only "what the email will look like" preview — escaped text, never raw HTML. */}
              <div>
                <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Xem trước</label>
                <div className="mt-1 whitespace-pre-wrap break-words rounded-xl border border-gray-200 bg-gray-50/60 px-4 py-3 text-sm leading-relaxed text-gray-700">
                  {body.trim() ? body : <span className="italic text-gray-400">Nội dung email sẽ hiển thị ở đây.</span>}
                </div>
              </div>
              {isActionTemplate && (
                <div className="rounded-xl border border-amber-200 bg-amber-50/60 p-4">
                  <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-amber-700">
                    <AlertCircle className="w-3.5 h-3.5" /> Nút phản hồi hệ thống (không sửa được)
                  </div>
                  <p className="mt-1 text-[12px] text-amber-700/90">
                    {systemActionDescription || 'Nút Chấp nhận/Từ chối sẽ được hệ thống tự gắn khi gửi email.'}
                  </p>
                  {lockedActionBlockHtml && (
                    <div
                      className="mt-2 rounded-lg border border-amber-200 bg-white p-2 opacity-80 pointer-events-none select-none"
                      dangerouslySetInnerHTML={{ __html: lockedActionBlockHtml }}
                    />
                  )}
                </div>
              )}
              <p className="text-[11px] italic text-gray-400">
                Email chỉ được gửi khi bạn bấm “{sendLabel}”.
              </p>
            </>
          )}
        </div>
        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-gray-100 px-6 py-3">
          <button type="button" onClick={onClose} disabled={sending} className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-40">
            Đóng
          </button>
          <button type="button" onClick={onRestore} disabled={loading || sending} className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-[#004c91] outline-none hover:bg-gray-50 disabled:opacity-40">
            Khôi phục mẫu gốc
          </button>
          {canSend && (
            <button
              type="button"
              onClick={onSend}
              disabled={loading || sending || !!error}
              className="inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-5 py-2 text-sm font-bold text-white outline-none hover:bg-[#013565] disabled:opacity-50"
            >
              {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
              {sendLabel}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
