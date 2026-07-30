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
import { X, Loader2, Paperclip, Send, Trash2, Image as ImageIcon, Eye, ChevronLeft } from 'lucide-react';
import {
  emailDraftsApi,
  type EmailDraftAttachmentInput,
  type EmailDraftRecipientInput,
  type EmailDraftRecipientDto,
} from '../api/emailDraftsApi';
import { emailsApi } from '../api/emailsApi';
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
import { formatVietnamTime } from '../../../shared/utils/vietnamTime';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';

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
  /**
   * Reopen an existing draft instead of starting a new one. The draft is fetched and its recipients
   * are put back into the group `recipient_type` says they came from.
   */
  initialDraftId?: number | null;
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
 * Rebuilds the three groups from a stored draft.
 *
 * `recipient_type` is the only thing that says which group a row belonged to, and the mapping is
 * exhaustive on purpose: TO, CC, BCC, nothing else. A value outside those three is corrupt data or a
 * contract mismatch, and there is no safe group to put it in — coercing it to TO would change what the
 * data means, write an unverified address into a visible header, and turn a broken row into a
 * perfectly sendable payload. Coercing it to BCC would leak it.
 *
 * So unrecognised rows are neither placed nor dropped: they are handed back separately, the caller
 * refuses to preview or send the draft, and the stored draft is left alone so nothing is lost.
 */
function envelopeFromDraft(recipients: EmailDraftRecipientDto[] | undefined): {
  envelope: RecipientEnvelope;
  unknown: EmailDraftRecipientDto[];
} {
  const envelope = emptyEnvelope();
  const unknown: EmailDraftRecipientDto[] = [];
  if (!recipients) return { envelope, unknown };

  for (const row of [...recipients].sort((a, b) => a.displayOrder - b.displayOrder)) {
    const type = (row.recipientType || '').toUpperCase();
    if (type === 'TO' || type === 'CC' || type === 'BCC') {
      envelope[type].push({ email: row.recipientEmail, name: row.recipientName ?? undefined });
    } else {
      unknown.push(row);
    }
  }
  return { envelope, unknown };
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
  initialSubject = '', initialBodyHtml = '', initialRecipients = '', initialDraftId = null,
}: Props) {
  const [envelope, setEnvelope] = useState<RecipientEnvelope>(() => seedEnvelopeFromString(initialRecipients));
  // CC/BCC start hidden but their data outlives the toggle — collapsing is a view concern, and a
  // collapsed field that silently dropped its addresses would send a different email than the one
  // the sender composed.
  const [showCc, setShowCc] = useState(false);
  const [showBcc, setShowBcc] = useState(false);
  const [recipientErrors, setRecipientErrors] = useState<Partial<Record<RecipientGroup, string>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  /**
   * Set when the loaded draft itself is unusable — currently a recipient row whose type is none of
   * TO/CC/BCC. Blocks preview, send AND autosave: persisting would rewrite the draft from the groups
   * we could classify, silently discarding the rows we could not.
   */
  const [draftBlocked, setDraftBlocked] = useState<string | null>(null);
  /**
   * True from the moment a draft id is supplied until `getDraft` has resolved (either way).
   *
   * Autosave must not run during this window. The form is empty until hydration lands, so a debounce
   * that fired first would PUT that empty form over the very draft being restored — turning "reopen my
   * draft" into "erase my draft". The flag starts true, so the gap between mount and the effect
   * running is covered too.
   */
  const [hydrating, setHydrating] = useState(initialDraftId != null);
  const [subject, setSubject] = useState(initialSubject);
  const [bodyHtml, setBodyHtml] = useState(initialBodyHtml);
  const [attachments, setAttachments] = useState<FileAttachment[]>([]);
  const [draftId, setDraftId] = useState<number | null>(null);
  const [saving, setSaving] = useState(false);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [showPreview, setShowPreview] = useState(false);
  const [templates, setTemplates] = useState<{ emailTemplateId: number; name: string; templateCode?: string }[]>([]);
  const [selectedTemplateId, setSelectedTemplateId] = useState<number | null>(emailTemplateId || null);
  const [confirmState, setConfirmState] = useState<{isOpen: boolean; onConfirm: () => void; message: string; title: string; variant?: 'warning' | 'danger' | 'default'}>({isOpen: false, onConfirm: () => {}, message: '', title: ''});

  // The ceiling comes from the server (EmailRecipientOptions). It is never assumed: when the request
  // fails the counter says so instead of showing a made-up denominator.
  const { limit: recipientLimit, status: limitStatus } = useRecipientLimit(open);

  const quillRef = useRef<any>(null);
  // src (data: URL) -> inline image identity, since quill strips data-* attributes off <img>.
  const inlineMapRef = useRef<Map<string, { fileId: number; contentId: string }>>(new Map());
  const draftIdRef = useRef<number | null>(null);
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const dirtyRef = useRef(false);

  useEffect(() => { draftIdRef.current = draftId; }, [draftId]);
  // Mirrored into refs because the debounced autosave runs outside React's render cycle.
  const draftBlockedRef = useRef<string | null>(null);
  useEffect(() => { draftBlockedRef.current = draftBlocked; }, [draftBlocked]);
  const hydratingRef = useRef(initialDraftId != null);
  useEffect(() => { hydratingRef.current = hydrating; }, [hydrating]);

  // Reset state each time the modal opens.
  useEffect(() => {
    if (!open) return;
    setEnvelope(seedEnvelopeFromString(initialRecipients));
    setShowCc(false);
    setShowBcc(false);
    setRecipientErrors({});
    setFormError(null);
    setDraftBlocked(null);
    // Reopening must never inherit a previous draft id from state — the id comes from the caller.
    setHydrating(initialDraftId != null);
    hydratingRef.current = initialDraftId != null;
    setSubject(initialSubject);
    setBodyHtml(initialBodyHtml);
    setAttachments([]);
    setDraftId(null);
    setSavedAt(null);
    setShowPreview(false);
    setSelectedTemplateId(emailTemplateId || null);
    inlineMapRef.current = new Map();
    dirtyRef.current = false;
    
    // Fetch ACTIVE templates
    emailsApi.getEmailTemplateList({ page: 1, pageSize: 100, mode: 'use' })
      .then(res => setTemplates(res.data.items || res.data.templates || []))
      .catch(() => pushToast?.('info', 'Không tải được danh sách mẫu email. Bạn vẫn có thể soạn thủ công.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  // Re-hydrate an existing draft. Runs after the reset above, so a failed load leaves a usable empty
  // composer rather than a half-populated one.
  useEffect(() => {
    if (!open) return;
    if (initialDraftId == null) { setHydrating(false); hydratingRef.current = false; return; }

    let cancelled = false;
    setHydrating(true);
    hydratingRef.current = true;   // set synchronously; a debounce could fire before the re-render

    (async () => {
      try {
        const draft = await emailDraftsApi.getDraft(initialDraftId);
        if (cancelled) return;

        const { envelope: restored, unknown } = envelopeFromDraft(draft.recipients);
        setEnvelope(restored);
        // Reveal a group only when it actually has addresses, so reopening a draft does not hide
        // recipients behind a collapsed toggle.
        setShowCc(restored.CC.length > 0);
        setShowBcc(restored.BCC.length > 0);

        // A row we cannot classify makes the whole draft unsendable. This is a data/contract fault,
        // not something the sender just mistyped, so it is reported at form level and the draft is
        // left untouched on the server rather than rewritten without the offending rows.
        if (unknown.length > 0) {
          const types = Array.from(new Set(unknown.map(r => r.recipientType || '(trống)'))).join(', ');
          setDraftBlocked(
            `Email nháp chứa loại người nhận không hợp lệ (${types}). ` +
            'Không thể xem trước hoặc gửi email nháp này. Dữ liệu nháp được giữ nguyên; ' +
            'vui lòng liên hệ quản trị viên.',
          );
        }
        setSubject(draft.subject ?? '');
        setBodyHtml(draft.bodyContent ?? '');
        setAttachments(
          (draft.attachments ?? [])
            .filter(a => a.attachmentType === 'ATTACHMENT')
            .map(a => ({
              fileId: a.fileId,
              name: a.displayName || a.originalFilename || `Tệp ${a.fileId}`,
              size: a.fileSize,
              mimeType: a.mimeType,
            })),
        );
        setDraftId(draft.emailDraftId);
        dirtyRef.current = false;
        // Autosave is enabled only now, and only on success.
        setHydrating(false);
        hydratingRef.current = false;
      } catch {
        if (cancelled) return;
        // Fail closed: no draft id is adopted, so nothing can be created or updated. The composer is
        // NOT presented as a working empty one — that would look like a draft whose content vanished.
        setDraftId(null);
        draftIdRef.current = null;
        dirtyRef.current = false;
        setFormError('Không tải được email nháp. Dữ liệu nháp trên hệ thống được giữ nguyên. Vui lòng đóng và thử lại.');
        // hydrating stays true: autosave remains disabled for this failed attempt.
      }
    })();

    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, initialDraftId]);

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
    // Every group keeps its own type. This used to be a single list stamped 'TO', which is why a CC
    // the screen collected came back from the server as a primary recipient.
    const groups: RecipientGroup[] = ['TO', 'CC', 'BCC'];
    let order = 0;
    const recipients: EmailDraftRecipientInput[] = groups.flatMap(group =>
      envelope[group].map(recipient => ({
        email: recipient.email,
        name: recipient.name ?? null,
        recipientType: group,
        displayOrder: order++,
      })),
    );
    const fileAtts: EmailDraftAttachmentInput[] = attachments.map((a, i) => ({
      fileId: a.fileId, attachmentType: 'ATTACHMENT', displayName: a.name, displayOrder: i,
    }));
    const inlineAtts: EmailDraftAttachmentInput[] = inline.map((im, i) => ({
      fileId: im.fileId, attachmentType: 'INLINE_IMAGE', contentId: im.contentId, displayOrder: 1000 + i,
    }));
    return {
      emailTemplateId: selectedTemplateId ?? null,
      relatedType: relatedType ?? null,
      relatedId: relatedId ?? null,
      subject,
      bodyContent: html,
      bodyFormat: 'HTML' as const,
      recipients,
      attachments: [...fileAtts, ...inlineAtts],
    };
  }, [finalizeBody, bodyHtml, envelope, attachments, subject, selectedTemplateId, relatedType, relatedId]);

  // ── Autosave (debounced) ──────────────────────────────────────────────────
  const persist = useCallback(async () => {
    if (!dirtyRef.current) return;
    // Never write back a draft we could not fully understand: buildPayload can only emit the rows it
    // managed to classify, so saving would delete the unclassifiable ones from the server copy.
    if (draftBlockedRef.current) return;
    // Never write while the draft is still loading — the form is empty until it arrives.
    if (hydratingRef.current) return;
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
      setSavedAt(formatVietnamTime(new Date()));
    } catch {
      /* autosave is best-effort; failures never block composing */
    } finally {
      setSaving(false);
    }
  }, [buildPayload]);

  // The timer must run the LATEST persist, not the one captured when it was scheduled. Closing over
  // `persist` directly meant the callback held the state from before the edit that scheduled it, so
  // the final change in a burst — the last recipient added, the last character typed — was never
  // written to the draft.
  const persistRef = useRef(persist);
  useEffect(() => { persistRef.current = persist; }, [persist]);

  const scheduleSave = useCallback(() => {
    dirtyRef.current = true;
    if (saveTimer.current) clearTimeout(saveTimer.current);
    saveTimer.current = setTimeout(() => { void persistRef.current(); }, 1200);
  }, []);

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
        
        const token = authStorage.getAccessToken();
        const proxyUrl = `/api/files/${uploaded.fileId}/content?access_token=${token}`;
        
        inlineMapRef.current.set(proxyUrl, { fileId: uploaded.fileId, contentId: cid });
        const editor = quillRef.current?.getEditor?.();
        const range = editor?.getSelection?.(true);
        const index = range ? range.index : (editor?.getLength?.() ?? 0);
        editor?.insertEmbed(index, 'image', proxyUrl, 'user');
        editor?.setSelection(index + 1, 0);
        scheduleSave();
      } catch (err: any) {
        const status = err.response?.status || 'Unknown';
        const msg = err.response?.data?.message || err.message || 'Không có chi tiết lỗi';
        pushToast?.('error', `Lỗi tải ảnh [${status}]: ${msg}`);
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
    } catch (err: any) {
      const status = err.response?.status || 'Unknown';
      const msg = err.response?.data?.message || err.message || 'Không có chi tiết lỗi';
      pushToast?.('error', `Lỗi tải tệp đính kèm [${status}]: ${msg}`);
    } finally {
      setUploading(false);
    }
  }, [pushToast, scheduleSave]);

  const removeAttachment = (fileId: number) => {
    setConfirmState({
      isOpen: true,
      title: 'Xóa tệp đính kèm',
      message: 'Bạn có chắc chắn muốn gỡ tệp đính kèm này?',
      variant: 'danger',
      onConfirm: () => {
        setConfirmState(prev => ({...prev, isOpen: false}));
        setAttachments((prev) => prev.filter((a) => a.fileId !== fileId));
        scheduleSave();
      }
    });
  };

  // ── Preview & Send ────────────────────────────────────────────────────────

  /**
   * Runs the whole envelope through the shared rules and parks each problem on its own field.
   * Returns true when the envelope is sendable. Nothing here mutates the draft: a refusal must never
   * cost the sender their subject, body, attachments or the recipients already entered.
   */
  const validateRecipients = useCallback((): boolean => {
    const problems = validateEnvelope(envelope, recipientLimit);
    const next: Partial<Record<RecipientGroup, string>> = {};
    for (const problem of problems) next[problem.group] ??= problem.message;
    setRecipientErrors(next);
    return problems.length === 0;
  }, [envelope, recipientLimit]);

  const handlePreview = useCallback(() => {
    if (draftBlocked) return;
    setFormError(null);
    if (!validateRecipients()) return;
    if (!subject.trim()) { pushToast?.('error', 'Tiêu đề email không được để trống.'); return; }
    if (uploading) { pushToast?.('error', 'Vui lòng đợi tệp đính kèm tải lên xong.'); return; }
    setShowPreview(true);
  }, [validateRecipients, subject, uploading, pushToast]);

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

  const handleSend = useCallback(async () => {
    if (sending) return;               // double-submit guard, before any await
    if (draftBlocked) return;          // an unclassifiable stored recipient makes this draft unsendable
    setFormError(null);
    if (!validateRecipients()) return;
    if (!subject.trim()) { pushToast?.('error', 'Tiêu đề email không được để trống.'); return; }
    const recipientTotal = countRecipients(envelope);
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
          ? `Đã gửi email tới ${recipientTotal} người nhận.`
          : (res.message || 'Đã tạo email nhưng gửi thất bại với một hoặc nhiều người nhận.'));
      onSent?.();
      onClose();
    } catch (e: any) {
      // The draft is left exactly as it was; only the error display changes. Drop back to the editor
      // so the sender can see which field was rejected — the errors live next to the inputs, and
      // leaving them on the preview would show a failure with nothing to act on.
      setShowPreview(false);
      mapServerError(e);
      pushToast?.('error', e?.response?.data?.message || 'Không thể gửi email. Vui lòng thử lại.');
    } finally {
      setSending(false);
    }
  }, [sending, envelope, subject, buildPayload, validateRecipients, mapServerError, pushToast, onSent, onClose]);

  const handleDiscard = useCallback(async () => {
    setConfirmState({
      isOpen: true,
      title: 'Hủy email',
      message: 'Email đang soạn sẽ bị hủy. Bạn có chắc chắn muốn hủy bỏ?',
      variant: 'danger',
      onConfirm: async () => {
        setConfirmState(prev => ({...prev, isOpen: false}));
        if (saveTimer.current) clearTimeout(saveTimer.current);
        const id = draftIdRef.current;
        if (id != null) { try { await emailDraftsApi.discardDraft(id); } catch { /* ignore */ } }
        onClose();
      }
    });
  }, [onClose]);

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
    dirtyRef.current = true;
    scheduleSave();
  };

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

        {showPreview ? (
          <div className="flex flex-col h-full overflow-hidden">
            <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
              <div className="bg-blue-50 border border-blue-100 rounded-lg p-4 mb-4">
                <h4 className="text-sm font-bold text-[#004c91] mb-2">Xem trước email</h4>
                <p className="text-xs text-gray-600">Kiểm tra kỹ nội dung và người nhận trước khi gửi chính thức.</p>
              </div>

              {/*
                All three groups are shown. This is the sender's own draft before it leaves, so the BCC
                list is theirs to see — the privacy rule that matters is that it never reaches the other
                recipients or the history of anyone else, which is enforced server-side.
              */}
              {(['TO', 'CC', 'BCC'] as RecipientGroup[]).filter(g => envelope[g].length > 0).map(group => (
                <div key={group}>
                  <label className="text-xs font-bold text-gray-500 uppercase">
                    {RECIPIENT_GROUP_LABELS[group]}:
                  </label>
                  <div className="mt-1 flex flex-wrap gap-1" data-testid={`preview-${group}`}>
                    {envelope[group].map(recipient => (
                      <span key={normalizeEmail(recipient.email)}
                        className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">
                        {recipient.name ? `${recipient.name} <${recipient.email}>` : recipient.email}
                      </span>
                    ))}
                  </div>
                </div>
              ))}

              <div>
                <label className="text-xs font-bold text-gray-500 uppercase">Tiêu đề:</label>
                <div className="mt-1 text-sm font-medium text-gray-900">{subject}</div>
              </div>

              {attachments.length > 0 && (
                <div>
                  <label className="text-xs font-bold text-gray-500 uppercase">Tệp đính kèm ({attachments.length}):</label>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {attachments.map(a => (
                      <span key={a.fileId} className="inline-flex max-w-[220px] items-center gap-2 rounded-lg border border-gray-200 bg-gray-50 px-2.5 py-1.5 text-xs">
                        {a.mimeType?.startsWith('image/') ? <ImageIcon className="h-4 w-4 shrink-0 text-violet-500" /> : <Paperclip className="h-4 w-4 shrink-0 text-gray-400" />}
                        <span className="min-w-0 flex-1 block truncate font-medium text-gray-700">{a.name}</span>
                        {a.size != null && <span className="text-[10px] text-gray-400 shrink-0">{formatBytes(a.size)}</span>}
                      </span>
                    ))}
                  </div>
                </div>
              )}

              <div className="border-t border-gray-200 pt-4 mt-2">
                <label className="text-xs font-bold text-gray-500 uppercase mb-2 block">Nội dung (HTML):</label>
                {/*
                  Sanitised at the render boundary, not at save. The stored draft and the sent payload
                  keep exactly what the author wrote — the backend is what decides the outgoing body —
                  so this only governs what this browser executes while previewing.
                */}
                <div className="bg-white rounded-lg border border-gray-200 p-4 min-h-[200px] text-sm text-gray-800 prose prose-sm max-w-none" dangerouslySetInnerHTML={{ __html: sanitizeHtml(bodyHtml) }} />
              </div>
            </div>

            <div className="flex items-center justify-between gap-3 border-t border-gray-100 px-6 py-4 bg-gray-50">
              <button type="button" onClick={() => setShowPreview(false)} disabled={sending} className="inline-flex items-center gap-1.5 rounded-lg px-4 py-2 text-sm font-bold text-gray-600 border border-gray-300 hover:bg-white bg-gray-50 transition-colors disabled:opacity-50">
                <ChevronLeft className="w-4 h-4" /> Quay lại sửa
              </button>
              <button
                type="button"
                onClick={() => {
                  setConfirmState({
                    isOpen: true,
                    title: 'Xác nhận gửi',
                    message: 'Bạn có chắc chắn muốn gửi email này?',
                    variant: 'default',
                    onConfirm: () => {
                      setConfirmState(prev => ({...prev, isOpen: false}));
                      handleSend();
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
              {/* Template Select */}
              <div>
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
                          scheduleSave();
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
                          changeTemplate(tid);
                        }
                      });
                    } else {
                      changeTemplate(tid);
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

                {draftBlocked && (
                  <p role="alert" data-testid="draft-blocked"
                    className="rounded-lg border border-red-300 bg-red-50 p-3 text-sm text-red-700">
                    <span aria-hidden="true">✕ </span>{draftBlocked}
                  </p>
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
                  onClick={handlePreview}
                  disabled={uploading || draftBlocked !== null}
                  className="inline-flex items-center gap-2 rounded-lg bg-[#004c91] px-5 py-2 text-sm font-bold text-white shadow-sm hover:bg-[#013565] disabled:opacity-60 transition-colors"
                >
                  <Eye className="w-4 h-4" /> Xem trước
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
      />
    </div>
  );
}

export default EmailComposeModal;
