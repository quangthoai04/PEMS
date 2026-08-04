/**
 * The three-stage send modal: VIEW → EDIT → FINAL_PREVIEW.
 *
 * **VIEW** is what the eye icon opens. It shows the finished message — recipients, subject, the body
 * with every variable already substituted (including the sender's own name and address), the read-only
 * action block, attachments and Reply-To. Nothing is editable, and a sender who is happy with it may
 * send from here without passing through the other two stages.
 *
 * **EDIT** opens only when the sender asks for it, and only on a template whose capability allows a
 * runtime edit. The editor is seeded with the ALREADY-SUBSTITUTED body, so the sentence naming the
 * sender is ordinary text by then: it can be reworded, moved or deleted like any other. There is no
 * send button here — the only way forward is "Xem trước kết quả".
 *
 * **FINAL_PREVIEW** shows the assembled message exactly as it will arrive, built by the backend rather
 * than by concatenating strings in the browser, and carries the signed token the send presents as proof
 * of what was approved. Editing again invalidates it, because it is rebuilt from the new content.
 *
 * Why the stages are separate rather than one always-editable form: the previous modal opened straight
 * into an editor, so every sender was invited to rewrite a message most of them only wanted to read, and
 * "what will the recipient see" and "what am I typing" were the same box. Splitting them makes the
 * read-only answer the default and the edit a deliberate act.
 *
 * Inline images: inserted as `<img src="{proxyUrl}">` for instant preview and tracked in a map keyed by
 * src (quill strips data-* attrs in the editor). On finalise each tracked img is rewritten to
 * `cid:{contentId}` and registered as an INLINE_IMAGE attachment so the backend MIME builder renders it
 * inline in the recipient's client.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import {
  Eye, X, Loader2, AlertCircle, Send, Mail, RotateCcw, Paperclip, Pencil, ArrowLeft, CheckCircle2,
} from 'lucide-react';
import { filesApi } from '../../../shared/api/filesApi';
import { authStorage } from '../../../shared/auth/authStorage';
import { contentIdForFile } from '../../emails/utils/inlineImages';
import { delegationsApi } from '../api/delegationsApi';
import type {
  EmailAttachmentRefInput,
  ApprovedEmailContentPayload,
} from '../types/delegations.types';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import { FileAttachmentItem } from '../../../shared/components/files/FileAttachmentItem';
import { FilePreviewModal } from '../../../shared/components/files/FilePreviewModal';
import type { PreviewableFile } from '../../../shared/components/files/filePreviewKind';

type ToastFn = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

/** Which of the three stages the modal is showing. */
export type EmailPreviewStage = 'VIEW' | 'EDIT' | 'FINAL_PREVIEW';

/** Who the email will actually be sent to — shown at the top so the sender knows who they are writing for. */
export interface EmailPreviewRecipient {
  name?: string | null;
  email?: string | null;
  roleLabel?: string | null;
  departmentName?: string | null;
  campusName?: string | null;
}

/**
 * What the parent sends.
 *
 * `approvedContent` is present ONLY when the sender edited and passed the final preview. Its absence is
 * not a missing field — it is the statement that this send uses the template, which is what the backend
 * does when no token arrives.
 */
export interface EmailPreviewSendPayload {
  approvedContent?: ApprovedEmailContentPayload;
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
  /** True while "Khôi phục từ mẫu" is reloading the original template. */
  restoring?: boolean;
  error: string | null;
  subject: string;
  /** The rendered body: variables already substituted, action block stripped out. */
  body: string;
  isActionTemplate: boolean;
  systemActionDescription?: string | null;
  lockedActionBlockHtml?: string | null;
  recipient?: EmailPreviewRecipient | null;
  /** Where a reply goes. Reported by the backend, never inferred from the body. */
  replyToEmail?: string | null;
  /**
   * True when this template's send flow may offer "Chỉnh sửa". Comes from the backend's capability, so
   * a template that is sent automatically shows no edit button even though its body mentions the sender.
   */
  runtimeEditable?: boolean;
  /** Signed proof of the render, handed to `final-preview` when the sender edits. */
  previewToken?: string | null;
  language?: 'VI' | 'EN';
  /** When true, the primary send button is shown (a concrete target is bound). */
  canSend: boolean;
  /** Label for the primary send button, e.g. "Mời với nội dung này". */
  sendLabel: string;
  pushToast?: ToastFn;
  onSubjectChange: (value: string) => void;
  onBodyChange: (value: string) => void;
  onClose: () => void;
  onRestore: () => void;
  onSend: (payload: EmailPreviewSendPayload) => void;
}

const QUILL_MODULES_TOOLBAR = [
  ['bold', 'italic', 'underline', 'strike'],
  [{ align: [] }],
  [{ list: 'ordered' }, { list: 'bullet' }],
  ['link', 'image'],
  ['clean'],
];

export function EmailPreviewModal({
  open, loading, sending, restoring, error, subject, body, isActionTemplate,
  systemActionDescription, lockedActionBlockHtml, recipient, replyToEmail,
  runtimeEditable, previewToken, language,
  canSend, sendLabel, pushToast,
  onSubjectChange, onBodyChange, onClose, onRestore, onSend,
}: EmailPreviewModalProps) {
  const [stage, setStage] = useState<EmailPreviewStage>('VIEW');
  const [attachments, setAttachments] = useState<FileAttachment[]>([]);
  const [uploading, setUploading] = useState(false);
  const [previewFile, setPreviewFile] = useState<PreviewableFile | null>(null);
  /** The finalised message and its token — held only while FINAL_PREVIEW is on screen. */
  const [finalHtml, setFinalHtml] = useState<string | null>(null);
  const [finalToken, setFinalToken] = useState<string | null>(null);
  const [finalising, setFinalising] = useState(false);
  const [stageError, setStageError] = useState<string | null>(null);
  const quillRef = useRef<any>(null);
  const inlineMapRef = useRef<Map<string, { fileId: number; contentId: string }>>(new Map());

  // Reset everything whenever the modal (re)opens — it stays mounted between opens, and a stage or a
  // token left over from the previous recipient is the one piece of state that must never survive.
  useEffect(() => {
    if (!open) return;
    setStage('VIEW');
    setAttachments([]);
    setUploading(false);
    setPreviewFile(null);
    setFinalHtml(null);
    setFinalToken(null);
    setFinalising(false);
    setStageError(null);
    inlineMapRef.current = new Map();
  }, [open]);

  // Any further edit invalidates the approval. Dropping the token here rather than trusting the sender
  // to walk back through FINAL_PREVIEW means a stale one can never reach the send: the backend would
  // refuse it anyway (the hash would not match), but refusing in the browser keeps their words on screen
  // instead of trading them for an error.
  const invalidateApproval = useCallback(() => {
    setFinalHtml(null);
    setFinalToken(null);
  }, []);

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

  /** The attachment refs as the backend hashes them — file attachments first, then inline images. */
  const buildAttachmentRefs = useCallback(
    (inline: { fileId: number; contentId: string }[]): EmailAttachmentRefInput[] => [
      ...attachments.map((a, i) => ({
        fileId: a.fileId, attachmentType: 'ATTACHMENT' as const, displayName: a.name, displayOrder: i,
      })),
      ...inline.map((im, i) => ({
        fileId: im.fileId, attachmentType: 'INLINE_IMAGE' as const, contentId: im.contentId, displayOrder: 1000 + i,
      })),
    ],
    [attachments],
  );

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
        invalidateApproval();
      } catch (err: any) {
        const status = err?.response?.status || 'Unknown';
        const msg = err?.response?.data?.message || err?.message || 'Không có chi tiết lỗi';
        pushToast?.('error', `Lỗi tải ảnh [${status}]: ${msg}`);
      } finally {
        setUploading(false);
      }
    };
    input.click();
  }, [pushToast, invalidateApproval]);

  const modules = useMemo(
    () => ({ toolbar: { container: QUILL_MODULES_TOOLBAR, handlers: { image: imageHandler } } }),
    [imageHandler],
  );

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
      invalidateApproval();
    } catch (err: any) {
      const status = err?.response?.status || 'Unknown';
      const msg = err?.response?.data?.message || err?.message || 'Không có chi tiết lỗi';
      pushToast?.('error', `Lỗi tải tệp đính kèm [${status}]: ${msg}`);
    } finally {
      setUploading(false);
    }
  }, [pushToast, invalidateApproval]);

  const removeAttachment = (fileId: number) => {
    setAttachments((prev) => prev.filter((a) => a.fileId !== fileId));
    invalidateApproval();
  };

  /** "Xem trước kết quả" — ask the backend to assemble and sign what will actually be sent. */
  const handleBuildFinalPreview = useCallback(async () => {
    if (!previewToken) {
      setStageError('Bản xem trước đã hết hạn. Vui lòng đóng và mở lại email.');
      return;
    }
    setFinalising(true);
    setStageError(null);
    try {
      const { html, inline } = finalizeBody(body);
      const result = await delegationsApi.buildFinalEmailPreview({
        previewToken,
        subject: subject.trim(),
        editableBodyHtml: html,
        attachments: buildAttachmentRefs(inline),
        language,
      });
      setFinalHtml(result.finalPreviewHtml);
      setFinalToken(result.finalPreviewToken);
      setStage('FINAL_PREVIEW');
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.message || 'Không tạo được bản xem trước cuối.';
      setStageError(msg);
    } finally {
      setFinalising(false);
    }
  }, [previewToken, finalizeBody, body, subject, buildAttachmentRefs, language]);

  const handleSend = useCallback(() => {
    // From VIEW with nothing edited: no token, no content — the backend renders the template. Deliberate,
    // not a gap: there is nothing of the sender's to approve, so there is nothing to bind.
    if (stage === 'VIEW' || !finalToken) {
      onSend({});
      return;
    }
    const { html, inline } = finalizeBody(body);
    onSend({
      approvedContent: {
        finalPreviewToken: finalToken,
        subject: subject.trim(),
        bodyHtml: html,
        attachments: buildAttachmentRefs(inline),
      },
    });
  }, [stage, finalToken, finalizeBody, body, subject, buildAttachmentRefs, onSend]);

  if (!open) return null;

  const busy = sending || restoring || finalising;
  const title = stage === 'EDIT'
    ? 'Chỉnh sửa email'
    : stage === 'FINAL_PREVIEW' ? 'Xem trước kết quả cuối' : 'Xem trước email';

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-4" onMouseDown={busy ? undefined : onClose}>
      <div
        className="w-full max-w-2xl max-h-[90vh] overflow-hidden rounded-2xl bg-white shadow-2xl flex flex-col"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h3 className="flex items-center gap-2 text-base font-bold text-[#004c91]">
            {stage === 'EDIT' ? <Pencil className="w-5 h-5" />
              : stage === 'FINAL_PREVIEW' ? <CheckCircle2 className="w-5 h-5" />
                : <Eye className="w-5 h-5" />}
            {title}
          </h3>
          <button type="button" onClick={onClose} disabled={busy} className="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600 outline-none disabled:opacity-40">
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
              {stageError && (
                <div className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm font-semibold text-red-600">
                  <AlertCircle className="w-4 h-4 shrink-0" /> {stageError}
                </div>
              )}

              {/* Recipients: shown in every stage. Who the message goes to never becomes editable. */}
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
                      <AlertCircle className="w-3 h-3" /> Người nhận chưa có email — không thể gửi email.
                    </div>
                  )}
                </div>
              )}

              {stage === 'FINAL_PREVIEW' ? (
                <>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Tiêu đề</label>
                    <p className="mt-1 rounded-xl bg-gray-50 px-3 py-2 text-sm font-semibold text-gray-800">{subject}</p>
                  </div>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wide text-gray-400">
                      Nội dung đúng như sẽ gửi
                    </label>
                    <div
                      data-testid="final-preview-body"
                      className="mt-1 rounded-xl border border-gray-200 bg-white p-3 text-sm text-gray-800"
                      // Backend-assembled: the sender's sanitised words, the locked action block and the
                      // branded shell, built by the same helpers the send uses. Sanitised again here
                      // because this is the render boundary and the string arrived over the network.
                      dangerouslySetInnerHTML={{ __html: sanitizeHtml(finalHtml || '') }}
                    />
                  </div>
                </>
              ) : stage === 'EDIT' ? (
                <>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Tiêu đề</label>
                    <input
                      type="text"
                      maxLength={255}
                      value={subject}
                      onChange={(e) => { onSubjectChange(e.target.value); invalidateApproval(); }}
                      className="mt-1 w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm font-semibold text-gray-800 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20"
                    />
                  </div>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Nội dung email</label>
                    <p className="mt-0.5 mb-1 text-[11px] text-gray-400">
                      Nội dung đã điền sẵn thông tin thật — kể cả phần thông tin người gửi. Anh/chị sửa
                      câu chữ tự do như văn bản bình thường.
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
                        onChange={(v: string) => { onBodyChange(v); invalidateApproval(); }}
                        placeholder="Soạn nội dung email..."
                        modules={modules}
                      />
                    </div>
                  </div>
                </>
              ) : (
                <>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Tiêu đề</label>
                    <p className="mt-1 rounded-xl bg-gray-50 px-3 py-2 text-sm font-semibold text-gray-800">{subject}</p>
                  </div>
                  <div>
                    <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Nội dung</label>
                    <div
                      data-testid="view-body"
                      className="mt-1 rounded-xl border border-gray-200 bg-white p-3 text-sm text-gray-800"
                      dangerouslySetInnerHTML={{ __html: sanitizeHtml(body || '') }}
                    />
                  </div>
                </>
              )}

              {/* Attachments: editable only in EDIT, listed everywhere so the sender always sees them. */}
              <div>
                <div className="mb-1 flex items-center justify-between">
                  <label className="text-xs font-bold uppercase tracking-wide text-gray-400">Tệp đính kèm</label>
                  {stage === 'EDIT' && (
                    <label className="inline-flex cursor-pointer items-center gap-1 rounded-lg border border-gray-300 px-2.5 py-1 text-xs font-semibold text-[#004c91] hover:bg-blue-50">
                      <Paperclip className="w-3.5 h-3.5" /> Thêm tệp
                      <input type="file" multiple className="hidden" onChange={(e) => { void onPickFiles(e.target.files); e.target.value = ''; }} />
                    </label>
                  )}
                </div>
                {attachments.length === 0 ? (
                  <p className="text-xs text-gray-400">Chưa có tệp đính kèm.</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {attachments.map((a) => (
                      <FileAttachmentItem
                        key={a.fileId}
                        data-testid="attachment"
                        file={a}
                        onPreview={setPreviewFile}
                        onRemove={stage === 'EDIT' ? () => removeAttachment(a.fileId) : undefined}
                      />
                    ))}
                  </div>
                )}
              </div>

              {/* Reply-To: its own field, never inferred from the body. */}
              {replyToEmail && (
                <div className="rounded-xl border border-gray-200 bg-gray-50 px-4 py-3">
                  <div className="text-[11px] font-bold uppercase tracking-wide text-gray-400">Trả lời email</div>
                  <p className="mt-1 text-xs text-gray-600">
                    Khi người nhận bấm “Trả lời”, email sẽ gửi tới <b className="text-gray-800">{replyToEmail}</b>.
                  </p>
                </div>
              )}

              {isActionTemplate && (
                <div className="rounded-xl border border-amber-200 bg-amber-50/60 p-4">
                  <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-amber-700">
                    <AlertCircle className="w-3.5 h-3.5" /> Nút phản hồi hệ thống (không sửa được)
                  </div>
                  <p className="mt-1 text-[12px] text-amber-700/90">
                    {systemActionDescription || 'Nút Chấp nhận/Từ chối sẽ được hệ thống tự gắn khi gửi email.'}
                  </p>
                  {/* Not shown in FINAL_PREVIEW: the assembled body already contains it there, and
                      printing it twice would suggest the recipient gets two sets of buttons. */}
                  {lockedActionBlockHtml && stage !== 'FINAL_PREVIEW' && (
                    <div
                      className="mt-2 rounded-lg border border-amber-200 bg-white p-2 opacity-80 pointer-events-none select-none"
                      dangerouslySetInnerHTML={{ __html: sanitizeHtml(lockedActionBlockHtml) }}
                    />
                  )}
                </div>
              )}

              <p className="text-[11px] italic text-gray-400">
                {stage === 'EDIT'
                  ? 'Email chưa được gửi. Bấm “Xem trước kết quả” để đối chiếu nội dung cuối cùng.'
                  : `Email chỉ được gửi khi bạn bấm “${sendLabel}”.`}
              </p>
            </>
          )}
        </div>

        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-gray-100 px-6 py-3">
          {uploading && <span className="mr-auto inline-flex items-center gap-1 text-xs text-gray-400"><Loader2 className="w-3.5 h-3.5 animate-spin" /> Đang tải tệp…</span>}

          {stage === 'EDIT' ? (
            <>
              <button
                type="button"
                onClick={() => { setStage('VIEW'); setStageError(null); }}
                disabled={busy}
                className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-40"
              >
                Hủy thay đổi
              </button>
              <button
                type="button"
                onClick={() => { onRestore(); invalidateApproval(); }}
                disabled={loading || busy}
                className="inline-flex items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-[#004c91] outline-none hover:bg-gray-50 disabled:opacity-40"
              >
                {restoring ? <Loader2 className="w-4 h-4 animate-spin" /> : <RotateCcw className="w-4 h-4" />}
                {restoring ? 'Đang khôi phục...' : 'Khôi phục từ mẫu'}
              </button>
              {/* No send button in EDIT, deliberately: the sender has to look at the assembled result
                  once before it goes to a recipient. */}
              <button
                type="button"
                onClick={() => { void handleBuildFinalPreview(); }}
                disabled={loading || busy || uploading || !!error}
                className="inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-5 py-2 text-sm font-bold text-white outline-none hover:bg-[#013565] disabled:opacity-50"
              >
                {finalising ? <Loader2 className="w-4 h-4 animate-spin" /> : <Eye className="w-4 h-4" />}
                Xem trước kết quả
              </button>
            </>
          ) : (
            <>
              {stage === 'FINAL_PREVIEW' ? (
                <button
                  type="button"
                  onClick={() => { setStage('EDIT'); setStageError(null); }}
                  disabled={busy}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-40"
                >
                  <ArrowLeft className="w-4 h-4" /> Quay lại chỉnh sửa
                </button>
              ) : (
                <>
                  <button type="button" onClick={onClose} disabled={busy} className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-40">
                    Đóng
                  </button>
                  {/* Capability decides this, not the wording: an automated template shows no edit
                      button even when its body names the sender. */}
                  {runtimeEditable && !!previewToken && (
                    <button
                      type="button"
                      onClick={() => { setStage('EDIT'); setStageError(null); }}
                      disabled={loading || busy || !!error}
                      className="inline-flex items-center gap-1.5 rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-[#004c91] outline-none hover:bg-gray-50 disabled:opacity-40"
                    >
                      <Pencil className="w-4 h-4" /> Chỉnh sửa
                    </button>
                  )}
                </>
              )}
              {canSend && (
                <button
                  type="button"
                  onClick={handleSend}
                  disabled={loading || busy || uploading || !!error}
                  className="inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-5 py-2 text-sm font-bold text-white outline-none hover:bg-[#013565] disabled:opacity-50"
                >
                  {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                  {sendLabel}
                </button>
              )}
            </>
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
