/**
 * The reply-contact panel of the "Soạn & xem trước email" modal.
 *
 * Two rules shape everything here.
 *
 * The block is READ-ONLY. Its HTML is rendered by the backend from values it resolved, shown in a
 * non-editable box, and never sent back — the send carries only the structured choice below it. That is
 * what keeps a sender from editing the table, deleting it, or pasting a second one, and it is why the
 * block lives here rather than inside the rich-text editor. It used to be inside: the preview drew a
 * dashed stand-in into the body, the host edited that body and sent it back, and the backend appended the
 * REAL card underneath — one message, two contact blocks, neither of them what the preview showed.
 *
 * The client decides nothing. Which modes are offered, whether the block may be hidden, whether a chosen
 * colleague is in scope and whether the resulting block is legal are all answered by the server, on every
 * change, through the same resolver the send uses. Validation below is for the user's benefit only.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { AlertCircle, Loader2, Search, UserCog, X } from 'lucide-react';
import { delegationsApi } from '../api/delegationsApi';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import type {
  EmailContactCandidate,
  EmailContactContext,
  EmailContactOverrideInput,
  EmailContactOverrideMode,
  EmailContactPreviewResult,
  EmailContactReplyToMode,
} from '../types/delegations.types';

export interface ContactOverrideDraft {
  mode: EmailContactOverrideMode;
  selectedUserId?: number;
  selectedUserLabel?: string;
  displayName?: string;
  roleLabel?: string;
  email?: string;
  phone?: string;
  departmentName?: string;
  campusName?: string;
  replyToMode: EmailContactReplyToMode;
  hideForThisEmail?: boolean;
  reason?: string;
}

export interface EmailContactOverrideSectionProps {
  /** Identifies the message. Null for a preview with no real recipient — the panel then stays hidden. */
  context: EmailContactContext | null;
  /** The panel as the first preview resolved it. */
  initial: EmailContactPreviewResult | null;
  /** Disable interaction while the parent is sending. */
  disabled?: boolean;
  /**
   * The committed choice, and whether the panel is in a state that must block the send. The parent
   * puts the override on `emailOverride.contactOverride` and refuses to send while `blocked` is true.
   */
  onChange: (state: { contactOverride: EmailContactOverrideInput | null; blocked: boolean }) => void;
}

const DEFAULT_DRAFT: ContactOverrideDraft = { mode: 'TEMPLATE_DEFAULT', replyToMode: 'POLICY_DEFAULT' };

const MODE_LABELS: Record<EmailContactOverrideMode, string> = {
  TEMPLATE_DEFAULT: 'Theo cấu hình mẫu',
  SYSTEM_USER: 'Chọn người trong hệ thống',
  MANUAL: 'Nhập thủ công',
};

const REPLY_TO_LABELS: Record<EmailContactReplyToMode, string> = {
  POLICY_DEFAULT: 'Theo cấu hình mẫu',
  CONTACT: 'Đầu mối liên hệ',
  SENDER: 'Người gửi (bạn)',
  NONE: 'Không đặt Reply-To',
};

/** Turns the editor's draft into the payload — or null, when nothing was actually changed. */
function toInput(draft: ContactOverrideDraft): EmailContactOverrideInput | null {
  const untouched =
    draft.mode === 'TEMPLATE_DEFAULT'
    && !draft.hideForThisEmail
    && draft.replyToMode === 'POLICY_DEFAULT';
  if (untouched) return null;

  if (draft.mode === 'SYSTEM_USER') {
    return {
      mode: 'SYSTEM_USER',
      userId: draft.selectedUserId,
      replyToMode: draft.replyToMode,
      reason: draft.reason?.trim() || null,
    };
  }

  if (draft.mode === 'MANUAL') {
    return {
      mode: 'MANUAL',
      displayName: draft.displayName?.trim() || null,
      roleLabel: draft.roleLabel?.trim() || null,
      email: draft.email?.trim() || null,
      phone: draft.phone?.trim() || null,
      departmentName: draft.departmentName?.trim() || null,
      campusName: draft.campusName?.trim() || null,
      replyToMode: draft.replyToMode,
      reason: draft.reason?.trim() || null,
    };
  }

  return {
    mode: 'TEMPLATE_DEFAULT',
    replyToMode: draft.replyToMode,
    hideForThisEmail: draft.hideForThisEmail ?? false,
  };
}

export function EmailContactOverrideSection({
  context, initial, disabled, onChange,
}: EmailContactOverrideSectionProps) {
  const [panel, setPanel] = useState<EmailContactPreviewResult | null>(initial);
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState<ContactOverrideDraft>(DEFAULT_DRAFT);
  /** The draft that produced `panel` — what actually gets sent. */
  const [applied, setApplied] = useState<ContactOverrideDraft>(DEFAULT_DRAFT);
  const [busy, setBusy] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  const [term, setTerm] = useState('');
  const [candidates, setCandidates] = useState<EmailContactCandidate[]>([]);
  const [searching, setSearching] = useState(false);

  // Monotonic request ids. A sender who changes their mind three times in a second gets three replies
  // in an order nobody controls, and without this the FIRST one can land last and redraw the panel with
  // a contact they have already moved on from.
  const applySeq = useRef(0);
  const searchSeq = useRef(0);

  // A fresh preview (modal reopened, template restored) resets the panel AND the committed choice: an
  // override belongs to one message, so carrying it into the next one would silently reuse a decision
  // the sender made about somebody else.
  useEffect(() => {
    setPanel(initial);
    setDraft(DEFAULT_DRAFT);
    setApplied(DEFAULT_DRAFT);
    setEditing(false);
    setFormError(null);
    setCandidates([]);
    setTerm('');
  }, [initial]);

  const blocked = !!panel?.errorCode;

  useEffect(() => {
    onChange({ contactOverride: toInput(applied), blocked });
    // `onChange` is a parent callback recreated per render in every call site; depending on it would
    // loop. The values it reports are the dependencies that matter.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [applied, blocked]);

  const search = useCallback(async (value: string) => {
    if (!context) return;
    const seq = ++searchSeq.current;
    setSearching(true);
    try {
      const rows = await delegationsApi.searchEmailContactCandidates(context, value);
      if (seq === searchSeq.current) setCandidates(rows);
    } catch {
      if (seq === searchSeq.current) setCandidates([]);
    } finally {
      if (seq === searchSeq.current) setSearching(false);
    }
  }, [context]);

  // Debounced, because this is a keystroke-driven query against `users`.
  useEffect(() => {
    if (!editing || draft.mode !== 'SYSTEM_USER' || !context) return;
    const timer = window.setTimeout(() => { void search(term); }, 300);
    return () => window.clearTimeout(timer);
  }, [editing, draft.mode, term, context, search]);

  /** Client-side checks, for the user's benefit. The backend re-runs all of them and owns the answer. */
  const localProblem = (d: ContactOverrideDraft): string | null => {
    if (d.mode === 'SYSTEM_USER' && !d.selectedUserId)
      return 'Hãy chọn một người trong hệ thống.';
    if (d.mode === 'MANUAL') {
      if (!d.displayName?.trim()) return 'Hãy nhập họ tên của đầu mối liên hệ.';
      if (!d.roleLabel?.trim()) return 'Hãy nhập vai trò của đầu mối liên hệ.';
      if (!d.email?.trim() && !d.phone?.trim())
        return 'Đầu mối liên hệ phải có ít nhất email hoặc số điện thoại.';
      if (!d.reason?.trim()) return 'Hãy nhập lý do dùng đầu mối nhập tay.';
      if (d.replyToMode === 'CONTACT' && !d.email?.trim())
        return 'Reply-To trỏ về đầu mối nhưng đầu mối chưa có email.';
    }
    return null;
  };

  const apply = async (next: ContactOverrideDraft) => {
    if (!context) return;

    const problem = localProblem(next);
    if (problem) { setFormError(problem); return; }

    const seq = ++applySeq.current;
    setBusy(true);
    setFormError(null);
    try {
      const result = await delegationsApi.previewEmailContact(context, toInput(next));
      if (seq !== applySeq.current) return;

      setPanel(result);

      // A refused override leaves the form open with everything the sender typed still in it. Committing
      // it would send a choice the backend has just said it will not accept.
      if (result.errorCode) {
        setFormError(result.errorMessage || 'Không thể áp dụng thông tin liên hệ này.');
        return;
      }

      setApplied(next);
      setEditing(false);
    } catch (e: any) {
      if (seq !== applySeq.current) return;
      setFormError(
        e?.response?.data?.message || e?.message || 'Không thể cập nhật thông tin liên hệ.');
    } finally {
      if (seq === applySeq.current) setBusy(false);
    }
  };

  const restoreDefault = () => { void apply(DEFAULT_DRAFT); setDraft(DEFAULT_DRAFT); };

  // No context means a preview with no real message behind it (the "xem mẫu" links), and an unsupported
  // template means there is no block on this mail at all. Neither has a contact to show or change.
  if (!context || !panel || !panel.supported) return null;

  const set = (patch: Partial<ContactOverrideDraft>) => setDraft((d) => ({ ...d, ...patch }));

  return (
    <div className="rounded-xl border border-gray-200 bg-gray-50/60 p-4" data-testid="contact-panel">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-[#004c91]">
            <UserCog className="w-3.5 h-3.5" /> Thông tin liên hệ (hệ thống chèn khi gửi)
          </div>
          <p className="mt-1 text-[12px] text-gray-500">
            {panel.hidden
              ? 'Email này sẽ không kèm khối thông tin liên hệ.'
              : panel.contactDisplayName
                ? <>Đầu mối: <b className="text-gray-700">{panel.contactDisplayName}</b>
                  {panel.source && <span className="text-gray-400"> · nguồn {panel.source}</span>}</>
                : 'Chưa xác định được đầu mối liên hệ cho email này.'}
          </p>
          {panel.replyToDisplay && (
            <p className="mt-0.5 text-[11px] text-gray-500">
              Thư trả lời sẽ đến: <b className="text-gray-700">{panel.replyToDisplay}</b>
            </p>
          )}
        </div>
        {panel.canOverride && !editing && (
          <button
            type="button"
            data-testid="contact-change"
            disabled={disabled || busy}
            onClick={() => { setDraft(applied); setFormError(null); setEditing(true); }}
            className="shrink-0 rounded-lg border border-gray-300 bg-white px-3 py-1.5 text-xs font-bold text-[#004c91] outline-none hover:bg-blue-50 disabled:opacity-40"
          >
            Thay đổi thông tin liên hệ
          </button>
        )}
      </div>

      {panel.errorCode && (
        <div
          data-testid="contact-error"
          className="mt-2 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs font-semibold text-red-600"
        >
          <AlertCircle className="w-3.5 h-3.5 shrink-0 mt-0.5" />
          <span>{panel.errorMessage || 'Không xác định được thông tin liên hệ.'}</span>
        </div>
      )}

      {busy && (
        <div className="mt-2 flex items-center gap-1.5 text-[11px] text-gray-400">
          <Loader2 className="w-3.5 h-3.5 animate-spin" /> Đang cập nhật khối liên hệ…
        </div>
      )}

      {panel.lockedContactBlockHtml && !editing && (
        <div
          data-testid="contact-block"
          className="mt-2 rounded-lg border border-gray-200 bg-white p-2 opacity-90 pointer-events-none select-none"
          // Server-generated and HTML-encoded at the source (EmailContactHtmlRenderer escapes every
          // value), but sanitised here too: this is the render boundary, and the block is built from
          // data. Its markup is <table>/<tr>/<td>/<p>, all of which survive the allow-list.
          dangerouslySetInnerHTML={{ __html: sanitizeHtml(panel.lockedContactBlockHtml) }}
        />
      )}

      {editing && (
        <div className="mt-3 space-y-3 rounded-lg border border-gray-200 bg-white p-3">
          <div className="flex items-center justify-between">
            <span className="text-[11px] font-bold uppercase tracking-wide text-gray-400">
              Đổi đầu mối cho email này
            </span>
            <button
              type="button" onClick={() => { setEditing(false); setFormError(null); }}
              className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 outline-none"
            >
              <X className="w-4 h-4" />
            </button>
          </div>

          <div className="space-y-1.5">
            {panel.availableModes.map((mode) => (
              <label key={mode} className="flex items-center gap-2 text-sm text-gray-700">
                <input
                  type="radio" name="contact-mode" value={mode}
                  checked={draft.mode === mode}
                  onChange={() => set({ mode, hideForThisEmail: false })}
                />
                {MODE_LABELS[mode]}
              </label>
            ))}
          </div>

          {draft.mode === 'TEMPLATE_DEFAULT' && panel.canHide && (
            <label className="flex items-center gap-2 text-sm text-gray-700">
              <input
                type="checkbox"
                data-testid="contact-hide"
                checked={!!draft.hideForThisEmail}
                onChange={(e) => set({ hideForThisEmail: e.target.checked })}
              />
              Không hiển thị khối liên hệ trong email này
            </label>
          )}

          {draft.mode === 'SYSTEM_USER' && (
            <div className="space-y-2">
              <div className="relative">
                <Search className="pointer-events-none absolute left-2.5 top-1/2 w-4 h-4 -translate-y-1/2 text-gray-400" />
                <input
                  type="text" value={term} placeholder="Tìm theo tên hoặc email…"
                  data-testid="contact-search"
                  onChange={(e) => setTerm(e.target.value)}
                  className="w-full rounded-lg border border-gray-200 py-2 pl-8 pr-3 text-sm outline-none focus:border-[#004c91]"
                />
              </div>
              {searching && (
                <div className="flex items-center gap-1.5 text-[11px] text-gray-400">
                  <Loader2 className="w-3 h-3 animate-spin" /> Đang tìm…
                </div>
              )}
              <div className="max-h-40 overflow-y-auto rounded-lg border border-gray-100">
                {candidates.length === 0 ? (
                  <p className="px-3 py-2 text-xs text-gray-400">
                    Không có người phù hợp trong phạm vi bạn được phép chọn.
                  </p>
                ) : candidates.map((c) => (
                  <button
                    key={c.userId} type="button"
                    onClick={() => set({ selectedUserId: c.userId, selectedUserLabel: c.fullName })}
                    className={`flex w-full flex-col items-start px-3 py-1.5 text-left text-xs hover:bg-blue-50 ${
                      draft.selectedUserId === c.userId ? 'bg-blue-50 font-bold' : ''}`}
                  >
                    <span className="text-gray-800">{c.fullName}</span>
                    <span className="text-gray-500">
                      {c.email || 'Chưa có email'}
                      {c.departmentName ? ` · ${c.departmentName}` : ''}
                      {c.campusName ? ` · ${c.campusName}` : ''}
                    </span>
                  </button>
                ))}
              </div>
              {draft.selectedUserLabel && (
                <p className="text-[11px] text-gray-500">
                  Đã chọn: <b className="text-gray-700">{draft.selectedUserLabel}</b>
                </p>
              )}
            </div>
          )}

          {draft.mode === 'MANUAL' && (
            <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              <Field label="Họ tên" testId="contact-manual-name" value={draft.displayName}
                onChange={(v) => set({ displayName: v })} />
              <Field label="Vai trò" testId="contact-manual-role" value={draft.roleLabel}
                onChange={(v) => set({ roleLabel: v })} />
              <Field label="Email" testId="contact-manual-email" value={draft.email}
                onChange={(v) => set({ email: v })} />
              <Field label="Số điện thoại" testId="contact-manual-phone" value={draft.phone}
                onChange={(v) => set({ phone: v })} />
              <Field label="Phòng ban" value={draft.departmentName}
                onChange={(v) => set({ departmentName: v })} />
              <Field label="Cơ sở" value={draft.campusName}
                onChange={(v) => set({ campusName: v })} />
              <div className="sm:col-span-2">
                <Field label="Lý do thay đổi" testId="contact-manual-reason" value={draft.reason}
                  onChange={(v) => set({ reason: v })} />
              </div>
            </div>
          )}

          <div>
            <label className="text-[11px] font-bold uppercase tracking-wide text-gray-400">Reply-To</label>
            <select
              data-testid="contact-replyto"
              value={draft.replyToMode}
              onChange={(e) => set({ replyToMode: e.target.value as EmailContactReplyToMode })}
              className="mt-1 w-full rounded-lg border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-[#004c91]"
            >
              {panel.availableReplyToModes.map((m) => (
                <option key={m} value={m}>{REPLY_TO_LABELS[m]}</option>
              ))}
            </select>
          </div>

          {formError && (
            <p data-testid="contact-form-error" className="text-xs font-semibold text-red-600">{formError}</p>
          )}

          <div className="flex flex-wrap justify-end gap-2">
            <button
              type="button" onClick={restoreDefault} disabled={busy || disabled}
              className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-40"
            >
              Khôi phục theo cấu hình mẫu
            </button>
            <button
              type="button" onClick={() => { setEditing(false); setFormError(null); }} disabled={busy}
              className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-40"
            >
              Hủy
            </button>
            <button
              type="button" data-testid="contact-apply"
              onClick={() => { void apply(draft); }} disabled={busy || disabled}
              className="inline-flex items-center gap-1.5 rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-bold text-white outline-none hover:bg-[#013565] disabled:opacity-50"
            >
              {busy && <Loader2 className="w-3.5 h-3.5 animate-spin" />} Áp dụng
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

function Field({
  label, value, onChange, testId,
}: { label: string; value?: string; onChange: (v: string) => void; testId?: string }) {
  return (
    <label className="block">
      <span className="text-[11px] font-bold uppercase tracking-wide text-gray-400">{label}</span>
      <input
        type="text" value={value ?? ''} data-testid={testId}
        onChange={(e) => onChange(e.target.value)}
        className="mt-1 w-full rounded-lg border border-gray-200 px-2 py-1.5 text-sm outline-none focus:border-[#004c91]"
      />
    </label>
  );
}
