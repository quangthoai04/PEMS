/**
 * Reusable editable "Soạn & xem trước email" modal (rich editor). Subject + body are editable with a
 * react-quill-new toolbar (bold/italic/underline/lists/align/link/image/clear); the host can insert
 * inline images and attach files (uploaded to the `files` store → Google Drive). For action templates
 * the accept/decline (or detail) block is system-controlled (read-only) and gets real tokens only on
 * the actual send. Presentational/controlled — the parent owns subject/body + the API send; on send
 * the modal hands back the finalized payload (cid-rewritten HTML + attachment refs).
 *
 * Inline images: inserted as a `<img src="{proxyUrl}">` for instant preview and tracked in a map keyed
 * by src (quill strips data-* attrs in the editor). On send each tracked img is rewritten to
 * `cid:{contentId}` and registered as an INLINE_IMAGE attachment so the backend MIME builder renders
 * it inline in the recipient's client.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { Eye, X, Loader2, AlertCircle, Send, Mail, RotateCcw, Paperclip, Trash2, Image as ImageIcon } from 'lucide-react';
import { filesApi } from '../../../shared/api/filesApi';
import { authStorage } from '../../../shared/auth/authStorage';
import { contentIdForFile } from '../../emails/utils/inlineImages';
import type { EmailAttachmentRefInput } from '../types/delegations.types';

type ToastFn = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

/** Who the email will actually be sent to — shown as a block at the top so the host knows exactly
 * which recipient they are writing for (YÊU CẦU 8). */
export interface EmailPreviewRecipient {
  name?: string | null;
  email?: string | null;
  roleLabel?: string | null;
  departmentName?: string | null;
  campusName?: string | null;
}

/** The finalized content handed to the parent on send (cid-rewritten HTML + attachment refs). */
export interface EmailPreviewSendPayload {
  subject: string;
  bodyHtml: string;
  attachments: EmailAttachmentRefInput[];
}

interface FileAttachment {
  fileId: number;
  name: string;
  size?: number | null;
  mimeType?: string | null;
}

export interface EmailPreviewModalProps {
  open: boolean;
  loading: boolean;
  sending: boolean;
  /** True while "Khôi phục mẫu gốc" is reloading the original template. */
  restoring?: boolean;
  error: string | null;
  subject: string;
  /** Editable HTML body (the rich editor binds to this). */
  body: string;
  isActionTemplate: boolean;
  systemActionDescription?: string | null;
  lockedActionBlockHtml?: string | null;
  /** Recipient block shown at the top (omit for the generic, recipient-less preview). */
  recipient?: EmailPreviewRecipient | null;
  /** When true, the primary "send" button is shown (a concrete target is bound). */
  canSend: boolean;
  /** Label for the primary send button, e.g. "Mời với nội dung này" / "Gửi với nội dung này". */
  sendLabel: string;
  /** Optional toast for upload errors. */
  pushToast?: ToastFn;
  onSubjectChange: (value: string) => void;
  onBodyChange: (value: string) => void;
  onClose: () => void;
  onRestore: () => void;
  /** Called with the finalized rich payload (the parent performs the actual API send). */
  onSend: (payload: EmailPreviewSendPayload) => void;
}

const QUILL_MODULES_TOOLBAR = [
  ['bold', 'italic', 'underline', 'strike'],
  [{ align: [] }],
  [{ list: 'ordered' }, { list: 'bullet' }],
  ['link', 'image'],
  ['clean'],
];

function formatBytes(bytes?: number | null): string {
  if (bytes == null || bytes < 0) return '';
  if (bytes < 1024) return `${bytes} B`;
  const kb = bytes / 1024;
  return kb < 1024 ? `${kb.toFixed(kb < 10 ? 1 : 0)} KB` : `${(kb / 1024).toFixed(1)} MB`;
}

export function EmailPreviewModal({
  open, loading, sending, restoring, error, subject, body, isActionTemplate,
  systemActionDescription, lockedActionBlockHtml, recipient, canSend, sendLabel, pushToast,
  onSubjectChange, onBodyChange, onClose, onRestore, onSend,
}: EmailPreviewModalProps) {
  const [attachments, setAttachments] = useState<FileAttachment[]>([]);
  const [uploading, setUploading] = useState(false);
  const quillRef = useRef<any>(null);
  // src (proxy URL) -> inline image identity, since quill strips data-* attributes off <img>.
  const inlineMapRef = useRef<Map<string, { fileId: number; contentId: string }>>(new Map());

  // Reset transient editor state whenever the modal (re)opens — it stays mounted between opens.
  useEffect(() => {
    if (!open) return;
    setAttachments([]);
    setUploading(false);
    inlineMapRef.current = new Map();
  }, [open]);

  // Rewrite tracked inline <img src=proxyUrl> → cid:{contentId}; return body + inline refs.
  const finalizeBody = useCallback((html: string): { html: string; inline: { fileId: number; contentId: string }[] } => {
    if (!html || typeof window === 'undefined' || !window.DOMParser) return { html, inline: [] };
    const doc = new window.DOMParser().parseFromString(html, 'text/html');
    const inline: { fileId: number; contentId: string }[] = [];
    doc.querySelectorAll('img').forEach((img) => {
      const src = img.getAttribute('src') || '';
      const m = inlineMapRef.current.get(src);
      if (m) {
        img.setAttribute('src', `cid:${m.contentId}`);
        img.setAttribute('data-content-id', m.contentId);
        img.setAttribute('data-file-id', String(m.fileId));
        inline.push(m);
      }
    });
    return { html: doc.body.innerHTML, inline };
  }, []);

  // ── Inline image upload (quill toolbar image button) ──
  const imageHandler = useCallback(() => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      setUploading(true);
      try {
        const uploaded = await filesApi.upload(file, 'EMAIL_INLINE');
        const cid = contentIdForFile(uploaded.fileId);
        const token = authStorage.getAccessToken();
        const proxyUrl = `/api/files/${uploaded.fileId}/content?access_token=${token}`;
        inlineMapRef.current.set(proxyUrl, { fileId: uploaded.fileId, contentId: cid });
        const editor = quillRef.current?.getEditor?.();
        const range = editor?.getSelection?.(true);
        const index = range ? range.index : (editor?.getLength?.() ?? 0);
        editor?.insertEmbed(index, 'image', proxyUrl, 'user');
        editor?.setSelection(index + 1, 0);
      } catch (err: any) {
        const status = err?.response?.status || 'Unknown';
        const msg = err?.response?.data?.message || err?.message || 'Không có chi tiết lỗi';
        pushToast?.('error', `Lỗi tải ảnh [${status}]: ${msg}`);
      } finally {
        setUploading(false);
      }
    };
    input.click();
  }, [pushToast]);

  const modules = useMemo(
    () => ({ toolbar: { container: QUILL_MODULES_TOOLBAR, handlers: { image: imageHandler } } }),
    [imageHandler],
  );

  // ── File attachments ──
  const onPickFiles = useCallback(async (files: FileList | null) => {
    if (!files || files.length === 0) return;
    setUploading(true);
    try {
      for (const file of Array.from(files)) {
        const uploaded = await filesApi.upload(file, 'EMAIL_ATTACHMENT');
        setAttachments((prev) => [...prev, {
          fileId: uploaded.fileId, name: uploaded.originalFilename, size: uploaded.fileSize, mimeType: uploaded.mimeType,
        }]);
      }
    } catch (err: any) {
      const status = err?.response?.status || 'Unknown';
      const msg = err?.response?.data?.message || err?.message || 'Không có chi tiết lỗi';
      pushToast?.('error', `Lỗi tải tệp đính kèm [${status}]: ${msg}`);
    } finally {
      setUploading(false);
    }
  }, [pushToast]);

  const removeAttachment = (fileId: number) => setAttachments((prev) => prev.filter((a) => a.fileId !== fileId));

  const handleSend = useCallback(() => {
    const { html, inline } = finalizeBody(body);
    const fileAtts: EmailAttachmentRefInput[] = attachments.map((a, i) => ({
      fileId: a.fileId, attachmentType: 'ATTACHMENT', displayName: a.name, displayOrder: i,
    }));
    const inlineAtts: EmailAttachmentRefInput[] = inline.map((im, i) => ({
      fileId: im.fileId, attachmentType: 'INLINE_IMAGE', contentId: im.contentId, displayOrder: 1000 + i,
    }));
    onSend({ subject: subject.trim(), bodyHtml: html, attachments: [...fileAtts, ...inlineAtts] });
  }, [finalizeBody, body, attachments, subject, onSend]);

  if (!open) return null;
  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-4" onMouseDown={sending ? undefined : onClose}>
      <div
        className="w-full max-w-2xl max-h-[90vh] overflow-hidden rounded-2xl bg-white shadow-2xl flex flex-col"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h3 className="flex items-center gap-2 text-base font-bold text-[#004c91]">
            <Eye className="w-5 h-5" /> Soạn & xem trước email
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
              {recipient && (
                <div className="rounded-xl border border-[#004c91]/20 bg-[#004c91]/[0.04] px-4 py-3">
                  <div className="flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-wide text-[#004c91]">
                    <Mail className="w-3.5 h-3.5" /> Người nhận
                  </div>
                  <div className="mt-1 text-sm font-bold text-gray-800">{recipient.name || 'Chưa có thông tin'}</div>
                  <div className="mt-0.5 flex flex-wrap gap-x-4 gap-y-0.5 text-xs text-gray-600">
                    <span>{recipient.email ? recipient.email : <span className="font-semibold text-red-500">Chưa có email</span>}</span>
                    {recipient.roleLabel && <span>Vai trò: <b>{recipient.roleLabel}</b></span>}
                    {recipient.departmentName && <span>Phòng ban: <b>{recipient.departmentName}</b></span>}
                    {recipient.campusName && <span>Cơ sở: <b>{recipient.campusName}</b></span>}
                  </div>
                  {!recipient.email && (
                    <div className="mt-1.5 flex items-center gap-1 text-[11px] font-semibold text-red-500">
                      <AlertCircle className="w-3 h-3" /> Người nhận chưa có email — không thể gửi lời mời.
                    </div>
                  )}
                </div>
              )}
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
                  Định dạng văn bản, chèn ảnh và liên kết. Ảnh chèn trong nội dung hiển thị inline trong email (qua cid).
                </p>
                <div className="mt-1 rounded-xl border border-gray-200">
                  <style>{`
                    .ql-editor img {
                      max-width: 560px;
                      max-height: 420px;
                      width: auto;
                      height: auto;
                      display: block;
                      margin: 16px auto;
                    }
                  `}</style>
                  <ReactQuill
                    ref={quillRef}
                    theme="snow"
                    value={body}
                    onChange={(v: string) => onBodyChange(v)}
                    placeholder="Soạn nội dung email..."
                    modules={modules}
                  />
                </div>
              </div>

              {/* Attachments */}
              <div>
                <div className="mb-1 flex items-center justify-between">
                  <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Tệp đính kèm</label>
                  <label className="inline-flex cursor-pointer items-center gap-1 rounded-lg border border-gray-300 px-2.5 py-1 text-xs font-semibold text-[#004c91] hover:bg-blue-50">
                    <Paperclip className="w-3.5 h-3.5" /> Thêm tệp
                    <input type="file" multiple className="hidden" onChange={(e) => { void onPickFiles(e.target.files); e.target.value = ''; }} />
                  </label>
                </div>
                {attachments.length === 0 ? (
                  <p className="text-xs text-gray-400">Chưa có tệp đính kèm.</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {attachments.map((a) => (
                      <span key={a.fileId} className="inline-flex max-w-[220px] items-center gap-2 rounded-lg border border-gray-200 bg-gray-50/70 px-2.5 py-1.5 text-xs">
                        {a.mimeType?.startsWith('image/') ? <ImageIcon className="h-4 w-4 shrink-0 text-violet-500" /> : <Paperclip className="h-4 w-4 shrink-0 text-gray-400" />}
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-semibold text-gray-700">{a.name}</span>
                          {a.size != null && <span className="block text-[10px] text-gray-400">{formatBytes(a.size)}</span>}
                        </span>
                        <button type="button" onClick={() => removeAttachment(a.fileId)} className="shrink-0 text-gray-400 hover:text-red-500" title="Xoá">
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </span>
                    ))}
                  </div>
                )}
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
          {uploading && <span className="mr-auto inline-flex items-center gap-1 text-xs text-gray-400"><Loader2 className="w-3.5 h-3.5 animate-spin" /> Đang tải tệp…</span>}
          <button type="button" onClick={onClose} disabled={sending} className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-40">
            Đóng
          </button>
          <button type="button" onClick={onRestore} disabled={loading || sending || restoring} className="inline-flex items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-[#004c91] outline-none hover:bg-gray-50 disabled:opacity-40">
            {restoring ? <Loader2 className="w-4 h-4 animate-spin" /> : <RotateCcw className="w-4 h-4" />}
            {restoring ? 'Đang khôi phục...' : 'Khôi phục mẫu gốc'}
          </button>
          {canSend && (
            <button
              type="button"
              onClick={handleSend}
              disabled={loading || sending || restoring || uploading || !!error}
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
