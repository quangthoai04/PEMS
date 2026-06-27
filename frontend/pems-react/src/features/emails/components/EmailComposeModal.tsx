/**
 * Rich email compose modal: react-quill-new body, file attachments + inline images (uploaded to the
 * `files` store), and DB-backed autosave to an email_draft. On send it finalises the draft (recipients
 * + attachments + inline cid body) and dispatches a real MIME email via the draft-send endpoint.
 *
 * Inline images: inserted as data:-URL <img> for instant preview and tracked in a map keyed by src
 * (quill drops unknown attributes, so we don't rely on them in the editor). On save/send each tracked
 * <img> src is rewritten to `cid:{contentId}` and registered as an INLINE_IMAGE attachment, which the
 * backend MIME builder turns into a linked resource so it renders inline in the recipient's client.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { X, Loader2, Paperclip, Send, Trash2, Image as ImageIcon } from 'lucide-react';
import { emailDraftsApi, type EmailDraftAttachmentInput } from '../api/emailDraftsApi';
import { filesApi } from '../../../shared/api/filesApi';
import { contentIdForFile } from '../utils/inlineImages';

type Toast = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

interface FileAttachment {
  fileId: number;
  name: string;
  size?: number | null;
  mimeType?: string | null;
}

interface Props {
  open: boolean;
  onClose: () => void;
  onSent?: () => void;
  pushToast?: Toast;
  relatedType?: string | null;
  relatedId?: number | null;
  emailTemplateId?: number | null;
  initialSubject?: string;
  initialBodyHtml?: string;
  initialRecipients?: string; // comma/newline separated
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

function parseRecipients(raw: string): string[] {
  return Array.from(
    new Set(
      raw
        .split(/[,;\n\s]+/)
        .map((s) => s.trim())
        .filter((s) => /^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(s)),
    ),
  );
}

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
}

export function EmailComposeModal({
  open, onClose, onSent, pushToast,
  relatedType, relatedId, emailTemplateId,
  initialSubject = '', initialBodyHtml = '', initialRecipients = '',
}: Props) {
  const [toInput, setToInput] = useState(initialRecipients);
  const [subject, setSubject] = useState(initialSubject);
  const [bodyHtml, setBodyHtml] = useState(initialBodyHtml);
  const [attachments, setAttachments] = useState<FileAttachment[]>([]);
  const [draftId, setDraftId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [uploading, setUploading] = useState(false);

  const quillRef = useRef<any>(null);
  // src (data: URL) -> inline image identity, since quill strips data-* attributes off <img>.
  const inlineMapRef = useRef<Map<string, { fileId: number; contentId: string }>>(new Map());
  const draftIdRef = useRef<number | null>(null);
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const dirtyRef = useRef(false);

  useEffect(() => { draftIdRef.current = draftId; }, [draftId]);

  // Reset state each time the modal opens.
  useEffect(() => {
    if (!open) return;
    setToInput(initialRecipients);
    setSubject(initialSubject);
    setBodyHtml(initialBodyHtml);
    setAttachments([]);
    setDraftId(null);
    setSavedAt(null);
    inlineMapRef.current = new Map();
    dirtyRef.current = false;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Rewrite tracked inline <img src=dataUrl> → cid:{contentId} and return the body + inline list.
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

  const buildPayload = useCallback(() => {
    const { html, inline } = finalizeBody(bodyHtml);
    const recipients = parseRecipients(toInput).map((email, i) => ({ email, recipientType: 'TO' as const, displayOrder: i }));
    const fileAtts: EmailDraftAttachmentInput[] = attachments.map((a, i) => ({
      fileId: a.fileId, attachmentType: 'ATTACHMENT', displayName: a.name, displayOrder: i,
    }));
    const inlineAtts: EmailDraftAttachmentInput[] = inline.map((im, i) => ({
      fileId: im.fileId, attachmentType: 'INLINE_IMAGE', contentId: im.contentId, displayOrder: 1000 + i,
    }));
    return {
      emailTemplateId: emailTemplateId ?? null,
      relatedType: relatedType ?? null,
      relatedId: relatedId ?? null,
      subject,
      bodyContent: html,
      bodyFormat: 'HTML' as const,
      recipients,
      attachments: [...fileAtts, ...inlineAtts],
    };
  }, [finalizeBody, bodyHtml, toInput, attachments, subject, emailTemplateId, relatedType, relatedId]);

  // ── Autosave (debounced) ──────────────────────────────────────────────────
  const persist = useCallback(async () => {
    if (!dirtyRef.current) return;
    dirtyRef.current = false;
    setSaving(true);
    try {
      const payload = buildPayload();
      if (draftIdRef.current == null) {
        const created = await emailDraftsApi.createDraft(payload);
        setDraftId(created.emailDraftId);
      } else {
        await emailDraftsApi.updateDraft(draftIdRef.current, payload);
      }
      const now = new Date();
      setSavedAt(`${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`);
    } catch {
      /* autosave is best-effort; failures never block composing */
    } finally {
      setSaving(false);
    }
  }, [buildPayload]);

  const scheduleSave = useCallback(() => {
    dirtyRef.current = true;
    if (saveTimer.current) clearTimeout(saveTimer.current);
    saveTimer.current = setTimeout(() => { void persist(); }, 1200);
  }, [persist]);

  useEffect(() => () => { if (saveTimer.current) clearTimeout(saveTimer.current); }, []);

  // ── Inline image upload (quill toolbar image button) ──────────────────────
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
        const dataUrl = await readAsDataUrl(file);
        inlineMapRef.current.set(dataUrl, { fileId: uploaded.fileId, contentId: cid });
        const editor = quillRef.current?.getEditor?.();
        const range = editor?.getSelection?.(true);
        const index = range ? range.index : (editor?.getLength?.() ?? 0);
        editor?.insertEmbed(index, 'image', dataUrl, 'user');
        editor?.setSelection(index + 1, 0);
        scheduleSave();
      } catch {
        pushToast?.('error', 'Không thể tải ảnh lên. Vui lòng thử lại.');
      } finally {
        setUploading(false);
      }
    };
    input.click();
  }, [pushToast, scheduleSave]);

  const modules = useMemo(
    () => ({ toolbar: { container: QUILL_MODULES_TOOLBAR, handlers: { image: imageHandler } } }),
    [imageHandler],
  );

  // ── File attachments ──────────────────────────────────────────────────────
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
      scheduleSave();
    } catch {
      pushToast?.('error', 'Không thể tải tệp đính kèm. Vui lòng thử lại.');
    } finally {
      setUploading(false);
    }
  }, [pushToast, scheduleSave]);

  const removeAttachment = (fileId: number) => {
    setAttachments((prev) => prev.filter((a) => a.fileId !== fileId));
    scheduleSave();
  };

  // ── Send ──────────────────────────────────────────────────────────────────
  const handleSend = useCallback(async () => {
    const recipients = parseRecipients(toInput);
    if (recipients.length === 0) { pushToast?.('error', 'Vui lòng nhập ít nhất một email người nhận hợp lệ.'); return; }
    if (!subject.trim()) { pushToast?.('error', 'Tiêu đề email không được để trống.'); return; }
    setSending(true);
    try {
      // Flush any pending autosave, then persist the final state synchronously.
      if (saveTimer.current) clearTimeout(saveTimer.current);
      const payload = buildPayload();
      let id = draftIdRef.current;
      if (id == null) {
        const created = await emailDraftsApi.createDraft(payload);
        id = created.emailDraftId;
        setDraftId(id);
      } else {
        await emailDraftsApi.updateDraft(id, payload);
      }
      const res = await emailDraftsApi.sendDraft(id!);
      pushToast?.(res.success ? 'success' : 'warning',
        res.success
          ? `Đã gửi email tới ${recipients.length} người nhận.`
          : (res.message || 'Đã tạo email nhưng gửi thất bại với một hoặc nhiều người nhận.'));
      onSent?.();
      onClose();
    } catch (e: any) {
      pushToast?.('error', e?.response?.data?.message || 'Không thể gửi email. Vui lòng thử lại.');
    } finally {
      setSending(false);
    }
  }, [toInput, subject, buildPayload, pushToast, onSent, onClose]);

  const handleDiscard = useCallback(async () => {
    if (saveTimer.current) clearTimeout(saveTimer.current);
    const id = draftIdRef.current;
    if (id != null) { try { await emailDraftsApi.discardDraft(id); } catch { /* ignore */ } }
    onClose();
  }, [onClose]);

  if (!open) return null;

  const recipientCount = parseRecipients(toInput).length;

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/40 p-4" onMouseDown={onClose}>
      <div
        className="flex w-full max-w-2xl max-h-[92vh] flex-col overflow-hidden rounded-2xl bg-white shadow-2xl"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h3 className="flex items-center gap-2 text-base font-bold text-[#004c91]">
            <Send className="w-5 h-5" /> Soạn email
          </h3>
          <div className="flex items-center gap-3">
            <span className="text-xs text-gray-400">
              {saving ? 'Đang lưu nháp…' : savedAt ? `Đã lưu nháp lúc ${savedAt}` : ''}
            </span>
            <button type="button" onClick={onClose} className="rounded-lg p-1.5 text-gray-400 outline-none hover:bg-gray-100 hover:text-gray-600">
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        <div className="space-y-4 overflow-y-auto px-6 py-4">
          {/* Recipients */}
          <div>
            <label className="mb-1 block text-xs font-bold uppercase tracking-wide text-gray-500">
              Người nhận {recipientCount > 0 && <span className="text-gray-400">({recipientCount})</span>}
            </label>
            <textarea
              value={toInput}
              onChange={(e) => { setToInput(e.target.value); scheduleSave(); }}
              placeholder="email1@fpt.edu.vn, email2@fpt.edu.vn"
              rows={2}
              className="w-full resize-y rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-700 outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]"
            />
          </div>

          {/* Subject */}
          <div>
            <label className="mb-1 block text-xs font-bold uppercase tracking-wide text-gray-500">Tiêu đề</label>
            <input
              type="text"
              value={subject}
              maxLength={255}
              onChange={(e) => { setSubject(e.target.value); scheduleSave(); }}
              placeholder="Tiêu đề email…"
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-700 outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]"
            />
          </div>

          {/* Body (rich text) */}
          <div>
            <label className="mb-1 block text-xs font-bold uppercase tracking-wide text-gray-500">Nội dung</label>
            <div className="rounded-lg border border-gray-200">
              <ReactQuill
                ref={quillRef}
                theme="snow"
                value={bodyHtml}
                onChange={(v: string) => { setBodyHtml(v); scheduleSave(); }}
                placeholder="Nhập nội dung email… (định dạng, chèn ảnh inline, liên kết)"
                modules={modules}
              />
            </div>
            <p className="mt-1 text-[11px] text-gray-400">
              Ảnh chèn trong nội dung sẽ hiển thị inline trong email (qua cid). Tệp đính kèm thêm ở dưới.
            </p>
          </div>

          {/* Attachments */}
          <div>
            <div className="mb-1 flex items-center justify-between">
              <label className="text-xs font-bold uppercase tracking-wide text-gray-500">Tệp đính kèm</label>
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
        </div>

        <div className="flex items-center justify-between gap-3 border-t border-gray-100 px-6 py-4">
          <button type="button" onClick={handleDiscard} className="inline-flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm font-semibold text-gray-500 hover:bg-gray-100">
            <Trash2 className="w-4 h-4" /> Huỷ nháp
          </button>
          <div className="flex items-center gap-2">
            {uploading && <span className="inline-flex items-center gap-1 text-xs text-gray-400"><Loader2 className="w-3.5 h-3.5 animate-spin" /> Đang tải tệp…</span>}
            <button
              type="button"
              onClick={handleSend}
              disabled={sending || uploading}
              className="inline-flex items-center gap-2 rounded-lg bg-[#004c91] px-5 py-2 text-sm font-bold text-white shadow-sm hover:bg-[#013565] disabled:opacity-60"
            >
              {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
              {sending ? 'Đang gửi…' : 'Gửi email'}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

export default EmailComposeModal;
