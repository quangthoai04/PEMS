/**
 * Rich email compose modal: react-quill-new body, file attachments + inline images (uploaded to the
 * `files` store), previewed and sent DIRECTLY.
 *
 * <b>There are no drafts.</b> The message lives in this component's state from the moment the modal opens
 * until it is sent, and then it is gone. What that removes is a class of failure rather than a feature:
 * the composer used to autosave to an `email_drafts` row and send by id, so a draft that had been
 * discarded, sent, or never created left the screen showing a form full of generated text that looked
 * exactly like a loaded draft — and pressing send then quietly created a NEW draft to send from. A dead
 * id therefore produced a real, sent email that no draft in the database had ever backed.
 *
 * What replaces the DRAFT → SENT claim as double-click protection is `Idempotency-Key`: one key per
 * opening of this modal, reused by every retry within it, so a second click of the same message is
 * recognised by the server and a genuinely edited message is not.
 *
 * Inline images: inserted as an <img> pointing at the file proxy for instant preview and tracked in a map
 * keyed by src (quill drops unknown attributes, so we don't rely on them in the editor). On send each
 * tracked <img> src is rewritten to `cid:{contentId}` and registered as an INLINE_IMAGE attachment, which
 * the backend MIME builder turns into a linked resource so it renders inline in the recipient's client.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import { X, Loader2, Paperclip, Send, Image as ImageIcon, Eye, ChevronLeft } from 'lucide-react';
import {
  emailsApi,
  type EmailComposeAttachmentInput,
  type SendEmailPayload,
} from '../api/emailsApi';
import { RecipientChipInput } from './RecipientChipInput';
import { useRecipientLimit } from '../hooks/useRecipientLimit';
import { classifyRecipientError } from '../utils/recipientErrors';
import {
  RECIPIENT_GROUP_LABELS,
  countRecipients,
  emptyEnvelope,
  isUsableLimit,
  normalizeEmail,
  splitPastedRecipients,
  validateEnvelope,
  type RecipientEnvelope,
  type RecipientGroup,
} from '../types/recipients';
import { filesApi } from '../../../shared/api/filesApi';
import { authStorage } from '../../../shared/auth/authStorage';
import { contentIdForFile } from '../utils/inlineImages';
import { ConfirmModal } from '../../../components/modals/ConfirmModal';
import { getApiErrorMessage } from '../../../shared/utils/toast';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import { FileAttachmentItem } from '../../../shared/components/files/FileAttachmentItem';
import { FilePreviewModal } from '../../../shared/components/files/FilePreviewModal';
import type { PreviewableFile } from '../../../shared/components/files/filePreviewKind';

type Toast = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

interface FileAttachment {
  fileId: number;
  name: string;
  size?: number | null;
  mimeType?: string | null;
}

/** One recipient line as the send API takes it. */
export interface ComposeRecipientPayload {
  email: string;
  name?: string | null;
  recipientType: RecipientGroup;
  displayOrder: number;
}

/** Everything a caller needs to send this message its own way. */
export interface ComposePayload {
  subject: string;
  bodyHtml: string;
  recipients: ComposeRecipientPayload[];
  attachments: EmailComposeAttachmentInput[];
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
  /**
   * Recipients to seed, already grouped. Used by callers whose defaults are resolved server-side (the
   * setup-progress flow), where a flat `initialRecipients` string could not say which addresses were
   * copies — so a CC the backend chose would have arrived as a primary recipient.
   */
  initialEnvelope?: ComposeRecipientPayload[];

  // ── Opt-in extensions. Every one is optional and off by default, so the email-management screens
  // that opened this modal before them behave exactly as they did. ──────────────────────────────

  /**
   * Hide the template picker. Used when the caller opened the composer ON a specific message whose
   * content the backend rendered — switching to another template mid-flow would replace it. Subject and
   * body stay editable: the point is to fix WHICH email this is, not to stop the author writing it.
   */
  lockedTemplate?: boolean;

  /** Contextual heading in place of "Soạn email", e.g. the delegation this message is about. */
  contextTitle?: string;

  /**
   * Attachments the author may not remove, by file id. Shown with a "Bắt buộc" tag and no delete button.
   * Enforced again server-side — this only stops the accident, not a crafted request.
   */
  lockedAttachmentFileIds?: number[];

  /**
   * Replaces the generic send for this composer. The setup-progress flow passes its own endpoint, which
   * re-checks the visit's host and stage; callers that omit it keep `POST /Emails/sendemail`.
   *
   * It is handed the whole message and the idempotency key, because with no draft id there is nothing
   * smaller that would identify what to send.
   */
  onSend?: (payload: ComposePayload, idempotencyKey: string)
    => Promise<{ success: boolean; message?: string }>;

  /**
   * Rebuilds the locked attachment — and, when the backend returns one, the body — from current data.
   * When supplied, the composer offers a "đồng bộ" control next to the attachment.
   *
   * `bodyHtml` in the result is the whole point of the operation for the setup-progress flow: the PDF and
   * the tables in the body are two renderings of ONE snapshot, so refreshing only the file would attach a
   * report that contradicts the email around it. Recipients and subject are still the author's.
   *
   * Because the returned body REPLACES what is in the editor, the composer asks first whenever the author
   * has typed into it since the last generation — an unannounced overwrite of someone's own paragraphs is
   * not an acceptable cost of pressing a sync button.
   */
  onRefreshRequiredAttachment?: () => Promise<{
    fileId: number;
    name: string;
    generatedAt?: string;
    bodyHtml?: string;
  }>;

  /** Notices to show above the form (a missing guest address). Display only. */
  notices?: string[];
}

const QUILL_MODULES_TOOLBAR = [
  ['bold', 'italic', 'underline', 'strike'],
  [{ align: [] }],
  [{ list: 'ordered' }, { list: 'bullet' }],
  ['link', 'image'],
  ['clean'],
];

/**
 * One key per opening of the composer.
 *
 * The server recognises a repeat of the SAME message under the same key and replays the first answer, so
 * a double click, an impatient retry after a slow response, or a retry after a provider error all resolve
 * to one email. Editing the message changes its fingerprint, so a genuine second message under the same
 * key is refused rather than silently answered "already sent" — which is why the key is per composer
 * session and not per application run.
 */
function newIdempotencyKey(): string {
  const cryptoObj = typeof globalThis !== 'undefined' ? globalThis.crypto : undefined;
  if (cryptoObj && typeof cryptoObj.randomUUID === 'function') return cryptoObj.randomUUID();
  return `compose-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * Seeds the TO group from the `initialRecipients` prop, which callers still pass as a comma/newline
 * string. Splitting is all this does — the addresses go through the same validation as typed ones.
 */
function seedEnvelopeFromString(raw: string): RecipientEnvelope {
  const envelope = emptyEnvelope();
  const seen = new Set<string>();
  for (const email of splitPastedRecipients(raw)) {
    const key = normalizeEmail(email);
    if (seen.has(key)) continue;
    seen.add(key);
    envelope.TO.push({ email });
  }
  return envelope;
}

/**
 * Seeds all three groups from a caller-supplied, already-grouped list.
 *
 * A row whose type is none of TO/CC/BCC is DROPPED rather than coerced. Coercing it to TO would write an
 * unverified address into a visible header; coercing it to BCC would hide it. Both change what the data
 * means, and the caller of this component builds the list from its own backend response, so an
 * unrecognised value is a contract mismatch rather than something a sender mistyped.
 */
function seedEnvelopeFromGroups(rows: ComposeRecipientPayload[]): RecipientEnvelope {
  const envelope = emptyEnvelope();
  for (const row of [...rows].sort((a, b) => a.displayOrder - b.displayOrder)) {
    const type = String(row.recipientType || '').toUpperCase();
    if (type === 'TO' || type === 'CC' || type === 'BCC') {
      envelope[type].push({ email: row.email, name: row.name ?? undefined });
    }
  }
  return envelope;
}

export function EmailComposeModal({
  open, onClose, onSent, pushToast,
  relatedType, relatedId, emailTemplateId,
  initialSubject = '', initialBodyHtml = '', initialRecipients = '', initialEnvelope,
  lockedTemplate = false, contextTitle, lockedAttachmentFileIds, onSend,
  onRefreshRequiredAttachment, notices,
}: Props) {
  const [envelope, setEnvelope] = useState<RecipientEnvelope>(() => seedEnvelopeFromString(initialRecipients));
  // CC/BCC start hidden but their data outlives the toggle — collapsing is a view concern, and a
  // collapsed field that silently dropped its addresses would send a different email than the one
  // the sender composed.
  const [showCc, setShowCc] = useState(false);
  const [showBcc, setShowBcc] = useState(false);
  const [recipientErrors, setRecipientErrors] = useState<Partial<Record<RecipientGroup, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [subject, setSubject] = useState(initialSubject);
  const [bodyHtml, setBodyHtml] = useState(initialBodyHtml);
  const [attachments, setAttachments] = useState<FileAttachment[]>([]);
  const [sending, setSending] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [previewing, setPreviewing] = useState(false);
  /**
   * The backend's answer to "what would go out". Null means the author is still editing; setting it is
   * what shows the confirm step, so the screen can never offer "Xác nhận gửi" over a preview that was
   * never checked.
   */
  const [preview, setPreview] = useState<{
    subject: string; body: string; to: string[]; cc: string[]; bcc: string[]; attachments: string[];
  } | null>(null);
  const [templates, setTemplates] = useState<{ emailTemplateId: number; name: string; templateCode?: string }[]>([]);
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(emailTemplateId || null);
  const [confirmState, setConfirmState] = useState<{isOpen: boolean; onConfirm: () => void; message: string; title: string; variant?: 'warning' | 'danger' | 'default'; confirmText?: string; cancelText?: string}>({isOpen: false, onConfirm: () => {}, message: '', title: ''});
  const [refreshingAttachment, setRefreshingAttachment] = useState(false);

  /**
   * The attachment currently being looked at. Held here — NOT in the item — so the whole strip shares one
   * modal, and nothing else in this component reads it.
   */
  const [previewFile, setPreviewFile] = useState<PreviewableFile | null>(null);

  /**
   * File ids the author may not remove. Held in state rather than read from the prop directly because
   * regenerating a locked attachment gives it a NEW file id — keeping the prop as the source would leave
   * the fresh file unprotected and the replaced one protected.
   */
  const [lockedFileIds, setLockedFileIds] = useState<number[]>(lockedAttachmentFileIds ?? []);
  const isLocked = useCallback((fileId: number) => lockedFileIds.includes(fileId), [lockedFileIds]);

  /**
   * The body exactly as the backend last generated it. Compared against the editor's current value to
   * tell "the author has written something here" from "this is still the generated text", which is the
   * only thing that decides whether a sync needs to warn before overwriting.
   */
  const generatedBodyRef = useRef(initialBodyHtml);
  const bodyWasEdited = useCallback(
    () => (bodyHtml ?? '').trim() !== (generatedBodyRef.current ?? '').trim(),
    [bodyHtml],
  );

  /**
   * Whether the author has changed anything since the composer opened.
   *
   * This is what the close confirmation is about. With autosave gone, closing a dirty composer really
   * does discard the message, so the question has to be asked — and only when there is something to lose,
   * because a confirmation that appears when there is nothing to confirm is one that stops being read.
   */
  const dirtyRef = useRef(false);
  const markDirty = useCallback(() => { dirtyRef.current = true; }, []);

  /** One key for this whole composer session — see `newIdempotencyKey`. */
  const idempotencyKeyRef = useRef<string>(newIdempotencyKey());

  // The ceiling comes from the server (EmailRecipientOptions). It is never assumed: when the request
  // fails the counter says so instead of showing a made-up denominator.
  const { limit: recipientLimit, status: limitStatus } = useRecipientLimit(open);

  const quillRef = useRef<any>(null);
  // src -> inline image identity, since quill strips data-* attributes off <img>.
  const inlineMapRef = useRef<Map<string, { fileId: number; contentId: string }>>(new Map());

  // Reset state each time the modal opens.
  useEffect(() => {
    if (!open) return;
    setEnvelope(initialEnvelope && initialEnvelope.length > 0
      ? seedEnvelopeFromGroups(initialEnvelope)
      : seedEnvelopeFromString(initialRecipients));
    // Reveal a group only when it actually has addresses, so a server-chosen CC is not hidden behind a
    // collapsed toggle.
    const seeded = initialEnvelope && initialEnvelope.length > 0
      ? seedEnvelopeFromGroups(initialEnvelope)
      : seedEnvelopeFromString(initialRecipients);
    setShowCc(seeded.CC.length > 0);
    setShowBcc(seeded.BCC.length > 0);
    setRecipientErrors({});
    setFormError(null);
    setSubject(initialSubject);
    setBodyHtml(initialBodyHtml);
    generatedBodyRef.current = initialBodyHtml;
    setAttachments([]);
    setPreview(null);
    setSelectedTemplateId(emailTemplateId || null);
    setLockedFileIds(lockedAttachmentFileIds ?? []);
    inlineMapRef.current = new Map();
    dirtyRef.current = false;
    // A new session is a new message: a key carried over from the previous opening would make the first
    // send of THIS message look like a replay of the last one.
    idempotencyKeyRef.current = newIdempotencyKey();

    // Fetch ACTIVE templates
    emailsApi.getEmailTemplateList({ page: 1, pageSize: 100, mode: 'use' })
      .then(res => setTemplates(res.data.items || res.data.templates || []))
      .catch(() => pushToast?.('info', 'Không tải được danh sách mẫu email. Bạn vẫn có thể soạn thủ công.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Rewrite tracked inline <img src> → cid:{contentId} and return the body + inline list.
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

  /** The message as the API takes it. One builder, used by preview and by send, so they cannot disagree. */
  const buildPayload = useCallback((): ComposePayload => {
    const { html, inline } = finalizeBody(bodyHtml);
    const groups: RecipientGroup[] = ['TO', 'CC', 'BCC'];
    let order = 0;
    const recipients: ComposeRecipientPayload[] = groups.flatMap(group =>
      envelope[group].map(recipient => ({
        email: recipient.email,
        name: recipient.name ?? null,
        recipientType: group,
        displayOrder: order++,
      })),
    );
    const fileAtts: EmailComposeAttachmentInput[] = attachments.map((a, i) => ({
      fileId: a.fileId, attachmentType: 'ATTACHMENT', displayName: a.name, displayOrder: i,
    }));
    const inlineAtts: EmailComposeAttachmentInput[] = inline.map((im, i) => ({
      fileId: im.fileId, attachmentType: 'INLINE_IMAGE', contentId: im.contentId, displayOrder: 1000 + i,
    }));
    return {
      subject,
      bodyHtml: html,
      recipients,
      attachments: [...fileAtts, ...inlineAtts],
    };
  }, [finalizeBody, bodyHtml, envelope, attachments, subject]);

  /** The generic send/preview shape, derived from the same payload. */
  const toSendPayload = useCallback((payload: ComposePayload): SendEmailPayload => ({
    templateId: selectedTemplateId ?? null,
    subject: payload.subject,
    body: payload.bodyHtml,
    bodyFormat: 'HTML',
    to: payload.recipients.filter(r => r.recipientType === 'TO').map(r => ({ email: r.email, name: r.name ?? undefined })),
    cc: payload.recipients.filter(r => r.recipientType === 'CC').map(r => ({ email: r.email, name: r.name ?? undefined })),
    bcc: payload.recipients.filter(r => r.recipientType === 'BCC').map(r => ({ email: r.email, name: r.name ?? undefined })),
    attachments: payload.attachments,
    relatedType: relatedType ?? null,
    relatedId: relatedId ?? null,
  }), [selectedTemplateId, relatedType, relatedId]);

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

        const token = authStorage.getAccessToken();
        const proxyUrl = `/api/files/${uploaded.fileId}/content?access_token=${token}`;

        inlineMapRef.current.set(proxyUrl, { fileId: uploaded.fileId, contentId: cid });
        const editor = quillRef.current?.getEditor?.();
        const range = editor?.getSelection?.(true);
        const index = range ? range.index : (editor?.getLength?.() ?? 0);
        editor?.insertEmbed(index, 'image', proxyUrl, 'user');
        editor?.setSelection(index + 1, 0);
        markDirty();
      } catch (err: any) {
        const status = err.response?.status || 'Unknown';
        const msg = err.response?.data?.message || err.message || 'Không có chi tiết lỗi';
        pushToast?.('error', `Lỗi tải ảnh [${status}]: ${msg}`);
      } finally {
        setUploading(false);
      }
    };
    input.click();
  }, [pushToast, markDirty]);

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
      markDirty();
    } catch (err: any) {
      const status = err.response?.status || 'Unknown';
      const msg = err.response?.data?.message || err.message || 'Không có chi tiết lỗi';
      pushToast?.('error', `Lỗi tải tệp đính kèm [${status}]: ${msg}`);
    } finally {
      setUploading(false);
    }
  }, [pushToast, markDirty]);

  const removeAttachment = (fileId: number) => {
    // Belt and braces: the delete button is not rendered for a locked file, but the guard means a future
    // call site cannot drop a mandatory attachment by going through this function.
    if (isLocked(fileId)) return;
    setConfirmState({
      isOpen: true,
      title: 'Xóa tệp đính kèm',
      message: 'Bạn có chắc chắn muốn gỡ tệp đính kèm này?',
      variant: 'danger',
      onConfirm: () => {
        setConfirmState(prev => ({...prev, isOpen: false}));
        setAttachments((prev) => prev.filter((a) => a.fileId !== fileId));
        markDirty();
      }
    });
  };

  // ── Preview & Send ────────────────────────────────────────────────────────

  /**
   * Runs the whole envelope through the shared rules and parks each problem on its own field. Returns
   * true when the envelope is sendable. Nothing here mutates the message: a refusal must never cost the
   * sender their subject, body, attachments or the recipients already entered.
   */
  const validateRecipients = useCallback((): boolean => {
    const problems = validateEnvelope(envelope, recipientLimit);
    const next: Partial<Record<RecipientGroup, string>> = {};
    for (const problem of problems) next[problem.group] ??= problem.message;
    setRecipientErrors(next);
    return problems.length === 0;
  }, [envelope, recipientLimit]);

  /** Puts a server-side rejection back on the field it belongs to, matched on the stable code. */
  const mapServerError = useCallback((error: any) => {
    // Shared with the reply composer so the two screens read the same refusal the same way.
    const classified = classifyRecipientError(error);
    if (classified.group) {
      setRecipientErrors({ [classified.group]: classified.message });
      return;
    }
    // Anything we cannot attribute to a field is shown at form level rather than guessed onto one.
    setFormError(classified.message);
  }, []);

  /**
   * Asks the backend what would go out.
   *
   * The preview is a round trip rather than a local render because the two answers are not the same: the
   * body is sanitised server-side with a different allow-list from the browser's, and an attachment whose
   * bytes have gone is only discoverable by trying to read them. A refusal here is the refusal the send
   * would have given, which is worth learning before pressing send rather than after.
   */
  const handlePreview = useCallback(async () => {
    if (previewing || sending) return;
    setFormError(null);
    if (!validateRecipients()) return;
    if (!subject.trim()) { pushToast?.('error', 'Tiêu đề email không được để trống.'); return; }
    if (uploading) { pushToast?.('error', 'Vui lòng đợi tệp đính kèm tải lên xong.'); return; }

    setPreviewing(true);
    try {
      const { data } = await emailsApi.previewEmail(toSendPayload(buildPayload()));
      setPreview({
        subject: data.subject,
        body: data.body,
        to: data.to ?? [],
        cc: data.cc ?? [],
        bcc: data.bcc ?? [],
        attachments: data.attachments ?? [],
      });
    } catch (e: any) {
      mapServerError(e);
      pushToast?.('error', getApiErrorMessage(e, 'Không xem trước được email. Vui lòng thử lại.'));
    } finally {
      setPreviewing(false);
    }
  }, [previewing, sending, validateRecipients, subject, uploading, pushToast, toSendPayload, buildPayload, mapServerError]);

  const handleSend = useCallback(async () => {
    if (sending) return;               // double-submit guard, before any await
    setFormError(null);
    if (!validateRecipients()) return;
    if (!subject.trim()) { pushToast?.('error', 'Tiêu đề email không được để trống.'); return; }
    const recipientTotal = countRecipients(envelope);
    setSending(true);
    try {
      const payload = buildPayload();
      const key = idempotencyKeyRef.current;

      // A caller that owns extra send-time rules supplies its own endpoint; everyone else posts the
      // generic one. Both carry the same key, so a retry is a retry either way.
      const res = onSend
        ? await onSend(payload, key)
        : await emailsApi.sendEmail(toSendPayload(payload), key).then(r => r.data);

      pushToast?.(res.success ? 'success' : 'warning',
        res.success
          ? `Đã gửi email tới ${recipientTotal} người nhận.`
          : (res.message || 'Đã tạo email nhưng gửi thất bại với một hoặc nhiều người nhận.'));
      onSent?.();
      onClose();
    } catch (e: any) {
      // The modal STAYS OPEN and the message is untouched. With no draft behind it, closing here would
      // destroy what the author wrote — so a provider failure drops back to the editor, where the field
      // errors live, and the same idempotency key is reused for the retry.
      setPreview(null);
      mapServerError(e);
      pushToast?.('error', getApiErrorMessage(e, 'Không thể gửi email. Vui lòng thử lại.'));
    } finally {
      setSending(false);
    }
  }, [sending, envelope, subject, buildPayload, toSendPayload, validateRecipients, mapServerError, pushToast, onSent, onClose, onSend]);

  /**
   * Rebuilds the locked attachment, and the body with it when the backend returns one. The new file id
   * takes over the lock so the replacement is protected and the replaced one is not. Recipients and
   * subject are left alone.
   */
  const runRefreshRequiredAttachment = useCallback(async () => {
    if (!onRefreshRequiredAttachment) return;
    setRefreshingAttachment(true);
    try {
      const fresh = await onRefreshRequiredAttachment();
      setAttachments(prev => [
        { fileId: fresh.fileId, name: fresh.name, size: null, mimeType: 'application/pdf' },
        ...prev.filter(a => !isLocked(a.fileId)),
      ]);
      setLockedFileIds([fresh.fileId]);

      if (typeof fresh.bodyHtml === 'string' && fresh.bodyHtml.length > 0) {
        setBodyHtml(fresh.bodyHtml);
        generatedBodyRef.current = fresh.bodyHtml;
        pushToast?.('success', 'Đã đồng bộ nội dung email và tệp báo cáo từ dữ liệu setup mới nhất.');
      } else {
        pushToast?.('success', 'Đã tạo lại tệp đính kèm từ dữ liệu mới nhất.');
      }
      markDirty();
    } catch (e: any) {
      pushToast?.('error', getApiErrorMessage(e, 'Không đồng bộ được từ dữ liệu mới nhất. Vui lòng thử lại.'));
    } finally {
      setRefreshingAttachment(false);
    }
  }, [onRefreshRequiredAttachment, isLocked, pushToast, markDirty]);

  /**
   * Asks before syncing when the author has typed into the body, because the sync replaces it. The check
   * is on the body only: the attachment is regenerated either way, and subject and recipients are never
   * rewritten, so there is nothing else of the author's to lose.
   */
  const handleRefreshRequiredAttachment = useCallback(() => {
    if (!onRefreshRequiredAttachment || refreshingAttachment) return;

    if (!bodyWasEdited()) {
      void runRefreshRequiredAttachment();
      return;
    }

    setConfirmState({
      isOpen: true,
      title: 'Đồng bộ sẽ ghi đè nội dung đã sửa',
      message:
        'Nội dung email đang có phần bạn tự sửa. Đồng bộ sẽ dựng lại toàn bộ nội dung và tệp báo cáo ' +
        'đính kèm từ dữ liệu setup mới nhất, nên những chỗ bạn đã sửa sẽ bị thay thế. ' +
        'Tiêu đề và danh sách người nhận được giữ nguyên. Bạn có muốn tiếp tục?',
      variant: 'warning',
      confirmText: 'Đồng bộ và thay thế',
      cancelText: 'Hủy',
      onConfirm: () => {
        setConfirmState(prev => ({ ...prev, isOpen: false }));
        void runRefreshRequiredAttachment();
      },
    });
  }, [onRefreshRequiredAttachment, refreshingAttachment, bodyWasEdited, runRefreshRequiredAttachment]);

  /**
   * Closing, with the one question that is now worth asking.
   *
   * There is no "Lưu nháp" because there is nowhere to save to, and saying so plainly is the honest
   * version of what the old "Huỷ nháp" button implied. An untouched composer closes without a word.
   */
  const handleRequestClose = useCallback(() => {
    if (sending) return;               // a send in flight owns the modal until it resolves
    if (!dirtyRef.current) { onClose(); return; }

    setConfirmState({
      isOpen: true,
      title: 'Đóng email đang soạn?',
      message:
        'Nội dung email chưa được gửi và sẽ không được lưu.\n\n'
        + 'Đóng và hủy nội dung này?',
      variant: 'danger',
      confirmText: 'Đóng và hủy',
      cancelText: 'Tiếp tục chỉnh sửa',
      onConfirm: () => {
        setConfirmState(prev => ({ ...prev, isOpen: false }));
        onClose();
      },
    });
  }, [sending, onClose]);

  if (!open) return null;

  const recipientCount = countRecipients(envelope);

  /** Addresses used in the other two groups, so each field can refuse a cross-group duplicate. */
  const takenOutside = (group: RecipientGroup): Set<string> => {
    const others: RecipientGroup[] = (['TO', 'CC', 'BCC'] as RecipientGroup[]).filter(g => g !== group);
    return new Set(others.flatMap(g => envelope[g].map(r => normalizeEmail(r.email))));
  };

  const setGroup = (group: RecipientGroup) => (next: typeof envelope.TO) => {
    setEnvelope(prev => ({ ...prev, [group]: next }));
    setRecipientErrors(prev => ({ ...prev, [group]: undefined }));
    markDirty();
  };

  return (
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/40 p-4" onMouseDown={handleRequestClose}>
      <div
        className="flex w-full max-w-2xl max-h-[92vh] flex-col overflow-hidden rounded-2xl bg-white shadow-2xl"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="flex items-center justify-between border-b border-gray-100 px-6 py-4">
          <h3 className="flex items-center gap-2 text-base font-bold text-[#004c91]">
            <Send className="w-5 h-5" /> {contextTitle || 'Soạn email'}
          </h3>
          {/* Same path as every other exit: the X used to close outright, so the person most likely to
              lose work — the one who reaches for the corner — was the only one never asked. */}
          <button type="button" onClick={handleRequestClose} aria-label="Đóng"
            className="rounded-lg p-1.5 text-gray-400 outline-none hover:bg-gray-100 hover:text-gray-600">
            <X className="w-5 h-5" />
          </button>
        </div>

        {preview ? (
          <div className="flex flex-col h-full overflow-hidden" data-testid="compose-preview">
            <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
              <div className="bg-blue-50 border border-blue-100 rounded-lg p-4 mb-4">
                <h4 className="text-sm font-bold text-[#004c91] mb-2">Xem trước email</h4>
                <p className="text-xs text-gray-600">
                  Đây là nội dung máy chủ sẽ gửi đi. Kiểm tra kỹ nội dung và người nhận trước khi gửi chính thức.
                </p>
              </div>

              {/*
                All three groups are shown. This is the sender's own message before it leaves, so the BCC
                list is theirs to see — the privacy rule that matters is that it never reaches the other
                recipients or anyone else's history, which is enforced server-side.
              */}
              {(['TO', 'CC', 'BCC'] as RecipientGroup[])
                .map(group => ({ group, list: group === 'TO' ? preview.to : group === 'CC' ? preview.cc : preview.bcc }))
                .filter(({ list }) => list.length > 0)
                .map(({ group, list }) => (
                  <div key={group}>
                    <label className="text-xs font-bold text-gray-500 uppercase">
                      {RECIPIENT_GROUP_LABELS[group]}:
                    </label>
                    <div className="mt-1 flex flex-wrap gap-1" data-testid={`preview-${group}`}>
                      {list.map(address => (
                        <span key={address}
                          className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                          {address}
                        </span>
                      ))}
                    </div>
                  </div>
                ))}

              <div>
                <label className="text-xs font-bold text-gray-500 uppercase">Tiêu đề:</label>
                <div className="mt-1 text-sm font-medium text-gray-900">{preview.subject}</div>
              </div>

              {preview.attachments.length > 0 && (
                <div>
                  <label className="text-xs font-bold text-gray-500 uppercase">
                    Tệp đính kèm ({preview.attachments.length}):
                  </label>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {attachments.map(a => (
                      <FileAttachmentItem
                        key={a.fileId}
                        data-testid={isLocked(a.fileId) ? 'preview-locked-attachment' : 'preview-attachment'}
                        file={a}
                        onPreview={setPreviewFile}
                        required={isLocked(a.fileId)}
                      />
                    ))}
                  </div>
                </div>
              )}

              <div className="border-t border-gray-200 pt-4 mt-2">
                <label className="text-xs font-bold text-gray-500 uppercase mb-2 block">Nội dung:</label>
                {/*
                  The body the SERVER returned, which is the sanitised one. Sanitised again here before it
                  is put into this document — the backend decides what goes in the email, this decides
                  what this browser executes, and neither is a substitute for the other.
                */}
                <div className="pems-email-body bg-white rounded-lg border border-gray-200 p-4 min-h-[200px] text-sm text-gray-800 max-w-none"
                  data-testid="compose-preview-body"
                  dangerouslySetInnerHTML={{ __html: sanitizeHtml(preview.body) }} />
              </div>
            </div>

            <div className="flex items-center justify-between gap-3 border-t border-gray-100 px-6 py-4 bg-gray-50">
              <button type="button" onClick={() => setPreview(null)} disabled={sending} className="inline-flex items-center gap-1.5 rounded-lg px-4 py-2 text-sm font-bold text-gray-600 border border-gray-300 hover:bg-white bg-gray-50 transition-colors disabled:opacity-50">
                <ChevronLeft className="w-4 h-4" /> Quay lại sửa
              </button>
              <button
                type="button"
                data-testid="confirm-send"
                onClick={() => {
                  setConfirmState({
                    isOpen: true,
                    title: 'Xác nhận gửi',
                    message: 'Bạn có chắc chắn muốn gửi email này?',
                    variant: 'default',
                    onConfirm: () => {
                      setConfirmState(prev => ({...prev, isOpen: false}));
                      void handleSend();
                    }
                  });
                }}
                disabled={sending}
                className="inline-flex items-center gap-2 rounded-lg bg-[#004c91] px-6 py-2 text-sm font-bold text-white shadow-sm hover:bg-[#013565] disabled:opacity-60 transition-colors"
              >
                {sending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                {sending ? 'Đang gửi…' : 'Xác nhận gửi'}
              </button>
            </div>
          </div>
        ) : (
          <>
            <div className="space-y-4 overflow-y-auto px-6 py-4">
              {/* Caller-supplied notices (a missing guest address). Display only — what makes the message
                  sendable is its TO group, not the absence of these. */}
              {notices && notices.length > 0 && (
                <div data-testid="compose-notices" className="rounded-lg border border-amber-200 bg-amber-50 p-3 space-y-1">
                  {notices.map((notice, i) => (
                    <p key={i} className="text-xs font-medium text-amber-800">{notice}</p>
                  ))}
                </div>
              )}

              {/* Template Select — hidden when the caller opened the composer on backend-rendered content. */}
              <div className={lockedTemplate ? 'hidden' : undefined}>
                <label className="mb-1 block text-xs font-bold uppercase tracking-wide text-gray-500">Chọn mẫu email</label>
                <select
                  value={selectedTemplateId || ''}
                  onChange={(e) => {
                    const tid = e.target.value ? Number(e.target.value) : null;
                    const changeTemplate = async (targetId: number | null) => {
                      if (targetId) {
                        try {
                          const res = await emailsApi.getEmailTemplateDetail(targetId);
                          setSubject(res.data.subjectVi || res.data.subject || '');
                          setBodyHtml(res.data.bodyVi || res.data.content || '');
                          setSelectedTemplateId(targetId);
                          markDirty();
                        } catch (err: any) {
                          const msg = err?.response?.data?.message || 'Không tải được mẫu email. Bạn vẫn có thể soạn thủ công.';
                          pushToast?.('error', msg);
                        }
                      } else {
                        setSelectedTemplateId(null);
                      }
                    };

                    if (tid && (subject.trim() || bodyHtml.trim()) && tid !== selectedTemplateId) {
                      setConfirmState({
                        isOpen: true,
                        title: 'Thay đổi mẫu email',
                        message: 'Nội dung hiện tại sẽ được thay bằng mẫu đã chọn. Bạn có muốn tiếp tục?',
                        variant: 'warning',
                        onConfirm: () => {
                          setConfirmState(prev => ({...prev, isOpen: false}));
                          void changeTemplate(tid);
                        }
                      });
                    } else {
                      void changeTemplate(tid);
                    }
                  }}
                  className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm text-gray-700 outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] bg-white cursor-pointer"
                >
                  <option value="">Không dùng mẫu / Soạn thủ công</option>
                  {templates.map(t => (
                    <option key={t.emailTemplateId} value={t.emailTemplateId}>{t.name}</option>
                  ))}
                </select>
              </div>

              {/* Recipients — TO always shown; CC/BCC revealed on demand but never cleared by hiding */}
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <label className="block text-xs font-bold uppercase tracking-wide text-gray-500">
                    Người nhận
                  </label>
                  <div className="flex items-center gap-3 text-xs">
                    {!showCc && (
                      <button type="button" onClick={() => setShowCc(true)}
                        className="font-medium text-[#004c91] hover:underline">Thêm CC</button>
                    )}
                    {!showBcc && (
                      <button type="button" onClick={() => setShowBcc(true)}
                        className="font-medium text-[#004c91] hover:underline">Thêm BCC</button>
                    )}
                    <span
                      data-testid="recipient-counter"
                      className={
                        isUsableLimit(recipientLimit) && recipientCount > recipientLimit
                          ? 'font-medium text-red-600'
                          : 'text-gray-500'
                      }
                    >
                      {isUsableLimit(recipientLimit)
                        ? `${recipientCount}/${recipientLimit} người nhận`
                        : limitStatus === 'loading'
                          ? `${recipientCount} người nhận`
                          : `${recipientCount} người nhận — chưa tải được giới hạn, hệ thống sẽ kiểm tra khi gửi`}
                    </span>
                  </div>
                </div>

                <RecipientChipInput
                  group="TO"
                  value={envelope.TO}
                  onChange={setGroup('TO')}
                  takenElsewhere={takenOutside('TO')}
                  externalError={recipientErrors.TO ?? null}
                  disabled={sending}
                />

                {showCc && (
                  <div className="flex items-start gap-2">
                    <div className="flex-1">
                      <RecipientChipInput
                        group="CC"
                        value={envelope.CC}
                        onChange={setGroup('CC')}
                        takenElsewhere={takenOutside('CC')}
                        externalError={recipientErrors.CC ?? null}
                        disabled={sending}
                      />
                    </div>
                    <button type="button" onClick={() => setShowCc(false)} aria-label="Thu gọn CC"
                      className="mt-6 text-xs text-gray-500 hover:text-gray-700">Thu gọn</button>
                  </div>
                )}

                {showBcc && (
                  <div className="flex items-start gap-2">
                    <div className="flex-1">
                      <RecipientChipInput
                        group="BCC"
                        value={envelope.BCC}
                        onChange={setGroup('BCC')}
                        takenElsewhere={takenOutside('BCC')}
                        externalError={recipientErrors.BCC ?? null}
                        disabled={sending}
                      />
                    </div>
                    <button type="button" onClick={() => setShowBcc(false)} aria-label="Thu gọn BCC"
                      className="mt-6 text-xs text-gray-500 hover:text-gray-700">Thu gọn</button>
                  </div>
                )}

                {formError && (
                  <p role="alert" className="text-sm text-red-600">
                    <span aria-hidden="true">✕ </span>{formError}
                  </p>
                )}
              </div>

              {/* Subject */}
              <div>
                <label className="mb-1 block text-xs font-bold uppercase tracking-wide text-gray-500">Tiêu đề</label>
                <input
                  type="text"
                  value={subject}
                  maxLength={255}
                  onChange={(e) => { setSubject(e.target.value); markDirty(); }}
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
                    onChange={(v: string) => { setBodyHtml(v); markDirty(); }}
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
                  <div className="flex items-center gap-2">
                    {onRefreshRequiredAttachment && (
                      <button
                        type="button"
                        data-testid="refresh-required-attachment"
                        onClick={handleRefreshRequiredAttachment}
                        disabled={refreshingAttachment}
                        title="Dựng lại nội dung email và tệp báo cáo từ dữ liệu setup mới nhất. Nội dung bạn tự sửa sẽ bị thay thế."
                        className="inline-flex items-center gap-1 rounded-lg border border-gray-300 px-2.5 py-1 text-xs font-semibold text-[#004c91] hover:bg-blue-50 disabled:opacity-60"
                      >
                        {refreshingAttachment ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : null}
                        Đồng bộ dữ liệu mới nhất
                      </button>
                    )}
                    <label className="inline-flex cursor-pointer items-center gap-1 rounded-lg border border-gray-300 px-2.5 py-1 text-xs font-semibold text-[#004c91] hover:bg-blue-50">
                      <Paperclip className="w-3.5 h-3.5" /> Thêm tệp
                      <input type="file" multiple className="hidden" onChange={(e) => { void onPickFiles(e.target.files); e.target.value = ''; }} />
                    </label>
                  </div>
                </div>
                {attachments.length === 0 ? (
                  <p className="text-xs text-gray-400">Chưa có tệp đính kèm.</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {attachments.map((a) => (
                      <FileAttachmentItem
                        key={a.fileId}
                        data-testid={isLocked(a.fileId) ? 'locked-attachment' : 'attachment'}
                        file={a}
                        onPreview={setPreviewFile}
                        // A locked attachment shows WHY it has no delete button. Hiding the control
                        // without saying anything reads as a rendering bug.
                        required={isLocked(a.fileId)}
                        onRemove={isLocked(a.fileId) ? undefined : () => removeAttachment(a.fileId)}
                      />
                    ))}
                  </div>
                )}
              </div>
            </div>

            <div className="flex items-center justify-between gap-3 border-t border-gray-100 px-6 py-4">
              {/* No "Lưu nháp" and no "Huỷ nháp": there is no draft. Closing is what discards the message,
                  and it asks first — see handleRequestClose. */}
              <span className="text-xs text-gray-400">
                Nội dung chỉ tồn tại trong phiên soạn này cho tới khi được gửi.
              </span>
              <div className="flex items-center gap-2">
                {uploading && <span className="inline-flex items-center gap-1 text-xs text-gray-400"><Loader2 className="w-3.5 h-3.5 animate-spin" /> Đang tải tệp…</span>}
                <button
                  type="button"
                  data-testid="preview-email"
                  onClick={() => { void handlePreview(); }}
                  disabled={uploading || previewing || sending}
                  className="inline-flex items-center gap-2 rounded-lg bg-[#004c91] px-5 py-2 text-sm font-bold text-white shadow-sm hover:bg-[#013565] disabled:opacity-60 transition-colors"
                >
                  {previewing ? <Loader2 className="w-4 h-4 animate-spin" /> : <Eye className="w-4 h-4" />}
                  {previewing ? 'Đang kiểm tra…' : 'Xem trước'}
                </button>
              </div>
            </div>
          </>
        )}
      </div>
      <ConfirmModal
        isOpen={confirmState.isOpen}
        onClose={() => setConfirmState(prev => ({...prev, isOpen: false}))}
        onConfirm={confirmState.onConfirm}
        title={confirmState.title}
        message={confirmState.message}
        variant={confirmState.variant}
        confirmText={confirmState.confirmText}
        cancelText={confirmState.cancelText}
      />
      <FilePreviewModal
        open={previewFile != null}
        file={previewFile}
        onClose={() => setPreviewFile(null)}
      />
    </div>
  );
}

export default EmailComposeModal;
