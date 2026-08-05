/**
 * VisitProcess "Thành phần tham gia" — real participant coordination (replaces the old hard-coded
 * demo UI). The Host (read-only card) is shown to everyone; the invite panels (Staff IC / Student /
 * Department) are shown only to the official host while the instance is live. Nothing here fakes an
 * invitee's response — statuses come straight from the DB; the host can only invite / withdraw.
 */
import React, { useEffect, useRef, useState } from 'react';
import {
  Search, Loader2, AlertCircle, Mail, Phone, Building2,
  Users, UserCheck, GraduationCap, Trash2, Send, Eye,
} from 'lucide-react';
import { delegationsApi } from '../api/delegationsApi';
import { EmailPreviewModal, type EmailPreviewRecipient, type EmailPreviewSendPayload } from './EmailPreviewModal';
import { participantScopeKey } from '../../emails/utils/emailScopeKey';
import { stripLegacyActionHtml } from '../../emails/utils/actionLinks';
import { SentEmailsModal } from './SentEmailsModal';
import type {
  VisitParticipantListItem, VisitProcessHost, ParticipantCandidate, SupportDepartment,
} from '../types/delegations.types';

type ToastFn = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

interface Props {
  visitInstanceId: number;
  relation: string;            // HOST | STAFF_LEADER | HO | IC_SUPPORT | ...
  instanceStatus: string;      // ASSIGNED | BEFORE_VISIT | ...
  currentUserId: number | null;
  host: VisitProcessHost | null;
  participants: VisitParticipantListItem[];
  onChanged: () => void | Promise<void>;
  pushToast: ToastFn;
  // Real context for the email preview merge (so the body is not a mock).
  delegationName?: string | null;
  campusName?: string | null;
  plannedStartAt?: string | null;   // "yyyy-MM-ddTHH:mm[:ss]" wall-clock
  plannedEndAt?: string | null;
}

const STATUS_META: Record<string, { label: string; cls: string }> = {
  INVITED: { label: 'Chờ phản hồi', cls: 'bg-amber-50 text-amber-700 border-amber-200' },
  ACCEPTED: { label: 'Đã chấp nhận', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  DECLINED: { label: 'Đã từ chối', cls: 'bg-red-50 text-red-700 border-red-200' },
  ASSIGNED: { label: 'Đã phân công', cls: 'bg-blue-50 text-blue-700 border-blue-200' },
  REMOVED: { label: 'Đã gỡ', cls: 'bg-slate-100 text-slate-500 border-slate-200' },
};

function StatusBadge({ status }: { status: string }) {
  const meta = STATUS_META[status] ?? { label: status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  return (
    <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
      {meta.label}
    </span>
  );
}

function ConflictBadge({ count, allPrivate }: { count: number; allPrivate: boolean }) {
  if (count <= 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-0.5 text-[11px] font-bold text-emerald-700">
        Không trùng lịch
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 rounded-md border border-amber-200 bg-amber-50 px-2 py-0.5 text-[11px] font-bold text-amber-700">
      <AlertCircle className="w-3 h-3" />
      {allPrivate ? 'Có lịch cá nhân trùng' : `Có ${count} lịch trùng`}
    </span>
  );
}

/** Generic debounced search dropdown.
 *
 * UX rules:
 * - Focus/click the input → dropdown opens AND fetches candidates with an empty keyword (shows the
 *   first page of valid users immediately; the list is scrollable + filters as you type).
 * - Click outside the wrapper → closes. Escape key → closes. Clicking an option does NOT close (the
 *   option is inside the wrapper, and close happens explicitly after a successful invite).
 * - Parent can call `closeDropdown` (via `onCloseRef`) after a successful invite to close + clear.
 */
export function SearchDropdown<T>({
  placeholder, emptyText, search, disabled, onCloseRef, renderRow, dropUp = false,
}: {
  placeholder: string;
  emptyText: string;
  search: (kw: string) => Promise<T[]>;
  disabled?: boolean;
  onCloseRef?: React.RefObject<(() => void) | null>;
  renderRow: (item: T, index: number, close: () => void) => React.ReactNode;
  dropUp?: boolean;
}) {
  const [kw, setKw] = useState('');
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(false);
  const [items, setItems] = useState<T[]>([]);
  const reqId = useRef(0);
  const wrapperRef = useRef<HTMLDivElement | null>(null);

  // Expose close function to parent if requested.
  const close = () => { setOpen(false); setKw(''); setItems([]); };
  useEffect(() => {
    if (onCloseRef) onCloseRef.current = close;
  });

  // Click-outside handler.
  useEffect(() => {
    if (!open) return;
    const handler = (e: MouseEvent) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [open]);

  // Debounced fetch — only runs while open.
  useEffect(() => {
    if (!open) return;
    const id = ++reqId.current;
    setLoading(true);
    setError(false);
    const t = setTimeout(async () => {
      try {
        const res = await search(kw.trim());
        if (id === reqId.current) setItems(Array.isArray(res) ? res : []);
      } catch {
        if (id === reqId.current) { setItems([]); setError(true); }
      } finally {
        if (id === reqId.current) setLoading(false);
      }
    }, 350);
    return () => clearTimeout(t);
  }, [kw, open, search]);

  // Show the panel whenever the input is focused — including an empty keyword (lists the first page
  // of valid candidates so the host can scroll/pick without typing).
  const showPanel = open;

  return (
    <div ref={wrapperRef} className="relative">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
        <input
          type="text"
          value={kw}
          disabled={disabled}
          placeholder={placeholder}
          onFocus={() => { if (!disabled) setOpen(true); }}
          onChange={(e) => setKw(e.target.value)}
          onKeyDown={(e) => { if (e.key === 'Escape') { setOpen(false); } }}
          className="w-full rounded-xl border border-gray-200 bg-white py-2.5 pl-9 pr-3 text-sm outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20 disabled:bg-gray-50 disabled:text-gray-400"
        />
        {loading && open && <Loader2 className="absolute right-3 top-1/2 -translate-y-1/2 w-4 h-4 animate-spin text-gray-400" />}
      </div>
      {showPanel && (
        <div className={`absolute left-0 z-[100] w-full max-h-72 overflow-y-auto rounded-xl border border-gray-200 bg-white shadow-xl ${
          dropUp ? 'bottom-full mb-1.5' : 'top-full mt-1.5'
        }`}>
          {loading && items.length === 0 ? (
            <div className="px-4 py-3 text-sm text-gray-400">Đang tải...</div>
          ) : error ? (
            <div className="px-4 py-3 text-sm text-red-500">Không thể tải danh sách. Thử lại.</div>
          ) : items.length === 0 ? (
            <div className="px-4 py-3 text-sm text-gray-400">{emptyText}</div>
          ) : (
            items.map((it, i) => renderRow(it, i, close))
          )}
        </div>
      )}
    </div>
  );
}

export function ParticipantInvitationSection({
  visitInstanceId, relation, instanceStatus, currentUserId, host, participants, onChanged, pushToast,
  delegationName, campusName, plannedStartAt, plannedEndAt,
}: Props) {
  // The host manages invitations only while the instance is in the prep window.
  const canManage = relation === 'HOST' && (instanceStatus === 'ASSIGNED' || instanceStatus === 'BEFORE_VISIT');
  const [busyId, setBusyId] = useState<string | null>(null);

  // "Xem mail đã gửi" history modal — bound to one participant at a time.
  const [sentModal, setSentModal] = useState<{ open: boolean; participantId: number | null; title: string; subtitle: string | null }>(
    { open: false, participantId: null, title: '', subtitle: null });
  const openSentEmails = (p: VisitParticipantListItem) =>
    setSentModal({ open: true, participantId: p.participantId, title: p.fullName, subtitle: p.email });
  const closeSentEmails = () => setSentModal((s) => ({ ...s, open: false }));

  // Editable "Xem trước email" modal. When `target` is set the host can send the (edited) email with
  // "Mời với nội dung này"; the system action block (accept/decline tokens) is injected by the backend.
  type PreviewTarget = {
    key: string;
    payload: Parameters<typeof delegationsApi.inviteVisitParticipant>[1];
    displayName: string;
    recipient: EmailPreviewRecipient;
  };
  type PreviewState = {
    open: boolean; loading: boolean; sending: boolean; restoring: boolean; error: string | null;
    templateCode: string; subject: string; body: string;
    /** The assembled message VIEW shows — backend-composed, shell and action block included. */
    initialFinalPreviewHtml: string;
    isActionTemplate: boolean; systemActionDescription: string | null; lockedActionBlockHtml: string | null;
    target: PreviewTarget | null;
    /** Where a reply goes, as the backend resolved it from the sending account. */
    replyToEmail: string | null;
    /** True when this template's flow may offer "Chỉnh sửa" — capability, not wording. */
    runtimeEditable: boolean;
    /** Signed proof of the render, handed to final-preview when the Host edits. */
    previewToken: string | null;
  };
  const EMPTY_PREVIEW: PreviewState = {
    open: false, loading: false, sending: false, restoring: false, error: null,
    templateCode: '', subject: '', body: '', initialFinalPreviewHtml: '',
    isActionTemplate: false, systemActionDescription: null, lockedActionBlockHtml: null, target: null,
    replyToEmail: null, runtimeEditable: false, previewToken: null,
  };
  const [preview, setPreview] = useState<PreviewState>(EMPTY_PREVIEW);

  // Real merge context from the actual instance + the bound recipient (no mock values). Empty fields
  // fall back to a friendly label instead of "undefined"/"null".
  //
  // These keys are EXACTLY the variables the three invitation templates declare — no more, no fewer.
  // The preview now goes through the same renderer as the send, which refuses an unknown or missing
  // variable rather than quietly substituting a placeholder, so a drifted key here would surface as a
  // failed preview instead of a preview that no recipient could ever receive.
  const FB = 'Chưa có thông tin';
  const previewContext = (target: PreviewTarget | null): Record<string, string> => {
    const r = target?.recipient;
    const start = fmtDateTime(plannedStartAt);
    const end = fmtDateTime(plannedEndAt);
    return {
      recipientName: r?.name || FB,
      delegationName: delegationName || FB,
      campusName: campusName || r?.campusName || 'FPT University',
      plannedTime: end ? `${start} - ${end}` : start,
      hostName: host?.fullName ?? 'Host',
      roleLabel: r?.roleLabel || FB,
      // The host has not written anything yet when the modal opens.
      hostMessage: '',
    };
  };

  const applyTemplate = (
    res: import('../types/delegations.types').PreviewEmailTemplateResult,
  ) =>
    setPreview((p) => ({
      ...p, open: true, loading: false, restoring: false, error: null,
      subject: res.subject, body: stripLegacyActionHtml(res.editableBodyHtml),
      // NOT stripped: this one is the assembled message, and its action block is the point of it.
      initialFinalPreviewHtml: res.initialFinalPreviewHtml ?? '',
      isActionTemplate: res.isActionTemplate,
      systemActionDescription: res.systemActionDescription ?? null,
      lockedActionBlockHtml: res.lockedActionBlockHtml ?? null,
      replyToEmail: res.replyToEmail ?? null,
      runtimeEditable: !!res.runtimeEditable,
      previewToken: res.previewToken ?? null,
    }));

  /**
   * The scope the send will recompute. Only a preview BOUND to a candidate is a real message: the
   * panel-header "xem mẫu" links have no recipient, so they get no scope and no token, and the modal
   * shows them read-only with no edit button.
   */
  const scopeFor = (target: PreviewTarget | null) =>
    target?.payload.userId ? participantScopeKey(visitInstanceId, target.payload.userId) : null;

  const loadPreview = async (templateCode: string, target: PreviewTarget | null) => {
    setPreview((p) => ({ ...p, open: true, loading: true, error: null, templateCode, target }));
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode,
        context: previewContext(target),
        visitInstanceId: target ? visitInstanceId : null,
        scopeKey: scopeFor(target),
      });
      applyTemplate(res);
    } catch (e: any) {
      setPreview((p) => ({ ...p, open: true, loading: false, error: apiError(e, 'Không thể tải bản xem trước email.') }));
    }
  };

  // Pure template preview (no candidate) — opened from each panel header.
  const openEmailPreview = (templateCode: string) => { void loadPreview(templateCode, null); };
  // Editable preview bound to a candidate — "Mời với nội dung này" sends with the edited content.
  const openEmailPreviewFor = (templateCode: string, target: PreviewTarget) => { void loadPreview(templateCode, target); };
  // "Khôi phục mẫu gốc": re-fetch the original template from the DB (picks up any seed patch) and
  // reset subject/body/preview — without closing the modal or losing the bound recipient.
  const restoreTemplate = async () => {
    if (!preview.templateCode) return;
    setPreview((p) => ({ ...p, restoring: true, error: null }));
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode: preview.templateCode,
        context: previewContext(preview.target),
        visitInstanceId: preview.target ? visitInstanceId : null,
        scopeKey: scopeFor(preview.target),
      });
      // A fresh preview, so a fresh token too: the wording the Host is about to approve is the wording
      // this render produced, and reusing the previous token would bind them to the one they discarded.
      applyTemplate(res);
      pushToast('success', 'Đã khôi phục nội dung email theo mẫu gốc.');
    } catch {
      setPreview((p) => ({ ...p, restoring: false }));
      pushToast('error', 'Không thể khôi phục mẫu gốc. Vui lòng thử lại.');
    }
  };
  const closePreview = () => setPreview(EMPTY_PREVIEW);

  const sendWithEditedContent = async (payload: EmailPreviewSendPayload) => {
    if (!preview.target) return;
    setPreview((p) => ({ ...p, sending: true }));
    try {
      // approvedContent is absent when the Host read the preview and sent it unchanged — the backend
      // then renders the template, exactly as it did before anybody could edit anything.
      const res = await delegationsApi.inviteVisitParticipant(visitInstanceId, {
        ...preview.target.payload,
        approvedContent: payload.approvedContent,
      });
      {
        const who = res.emailRecipient ? `${preview.target.displayName} (${res.emailRecipient})` : preview.target.displayName;
        pushToast(res.emailStatus === 'FAILED' ? 'warning' : 'success',
          res.emailStatus === 'FAILED'
            ? `Đã tạo lời mời cho ${who} nhưng gửi email thất bại.`
            : `Đã gửi email mời tới ${who}.`);
      }
      closePreview();
      closeStaffDropdown.current?.();
      closeStudentDropdown.current?.();
      closeDeptDropdown.current?.();
      await onChanged();
    } catch (e: any) {
      // Keep the modal open with the user's edits so they can fix and retry.
      setPreview((p) => ({ ...p, sending: false }));
      pushToast('error', apiError(e, 'Không thể gửi lời mời. Vui lòng thử lại.'));
    }
  };

  // Refs to close each search dropdown after a successful invite.
  const closeStaffDropdown = useRef<(() => void) | null>(null);
  const closeStudentDropdown = useRef<(() => void) | null>(null);
  const closeDeptDropdown = useRef<(() => void) | null>(null);

  const active = participants.filter((p) => p.status !== 'REMOVED');
  const supporters = active.filter((p) => p.participantRole === 'IC_SUPPORT' && !p.isHost);
  const students = active.filter((p) => p.participantRole === 'STUDENT');
  const departments = active.filter((p) => p.participantRole === 'DEPT_SUPPORT'
    && (p.subRole == null || p.subRole.toUpperCase() === 'LEADER'));

  const invite = async (
    key: string,
    payload: Parameters<typeof delegationsApi.inviteVisitParticipant>[1],
    displayName: string,
    onSuccess?: () => void,
  ) => {
    if (busyId) return;
    setBusyId(key);
    try {
      const res = await delegationsApi.inviteVisitParticipant(visitInstanceId, payload);
      const who = res.emailRecipient ? `${displayName} (${res.emailRecipient})` : displayName;
      pushToast(res.emailStatus === 'FAILED' ? 'warning' : 'success',
        res.emailStatus === 'FAILED'
          ? `Đã tạo lời mời cho ${who} nhưng gửi email thất bại. Xem chi tiết trong “Mail đã gửi”.`
          : `Đã gửi email mời tới ${who}.`);
      onSuccess?.();
      await onChanged();
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể gửi lời mời. Vui lòng thử lại.'));
    } finally {
      setBusyId(null);
    }
  };

  const remove = async (p: VisitParticipantListItem) => {
    if (busyId) return;
    setBusyId(`rm-${p.participantId}`);
    try {
      await delegationsApi.removeVisitParticipant(visitInstanceId, p.participantId);
      pushToast('success', `Đã gỡ ${p.fullName} khỏi danh sách mời.`);
      await onChanged();
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể gỡ lời mời. Vui lòng thử lại.'));
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="space-y-6">
      {/* ── Host chính (read-only) ── */}
      <div className="rounded-xl border border-gray-200 border-l-[5px] border-l-[#004c91] bg-white px-4 py-3 shadow-sm">
        {host ? (
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-2.5 min-w-0">
              <span className="flex items-center gap-2 text-sm sm:text-base font-bold text-[#004c91] shrink-0">
                <UserCheck className="w-5 h-5 text-[#004c91]" />
                <span>Người phụ trách tiếp đón:</span>
              </span>
              <div className="flex h-7 w-7 items-center justify-center rounded-full bg-[#004c91] text-xs font-extrabold text-white shrink-0">
                {host.fullName.charAt(0)}
              </div>
              <span className="text-sm font-bold text-[#004c91] truncate" title={host.fullName}>
                {host.fullName}
              </span>
              {currentUserId != null && host.userId === currentUserId && (
                <span className="rounded-md bg-blue-100 px-2 py-0.5 text-[11px] font-bold text-[#004c91]">
                  Bạn phụ trách tiếp đón
                </span>
              )}
              {host.departmentName && (
                <span className="text-xs text-gray-500 font-medium truncate">
                  • {host.departmentName}
                </span>
              )}
            </div>
            <span className="inline-flex shrink-0 items-center rounded-md border border-blue-200 bg-blue-50 px-2.5 py-1 text-xs font-bold text-[#004c91]">
              {host.statusLabel || 'Đã được phân công'}
            </span>
          </div>
        ) : (
          <div className="flex items-center gap-2 text-xs font-semibold text-amber-700">
            <AlertCircle className="w-4 h-4 shrink-0" /> Chưa xác định Host chính cho cơ sở này.
          </div>
        )}
      </div>

      {!canManage && (
        <p className="text-xs font-medium text-slate-500">
          Chỉ Host phụ trách mới có thể mời thành phần tham gia. Danh sách dưới đây ở chế độ xem.
        </p>
      )}

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-2">
        {/* ── Staff hỗ trợ IC ── */}
        <Panel title="Staff hỗ trợ IC" icon={<Users className="w-5 h-5" />}>
          {canManage && (
            <SearchDropdown<ParticipantCandidate>
              placeholder="Tìm theo tên..."
              emptyText="Không tìm thấy nhân sự phù hợp."
              search={(kw) => delegationsApi.getParticipantCandidates(visitInstanceId, 'IC_SUPPORT', kw)}
              onCloseRef={closeStaffDropdown}
              renderRow={(c, _i, close) => (
                <CandidateRow
                  key={c.userId}
                  candidate={c}
                  roleLabel={icSupportRoleLabel(c)}
                  busy={busyId === `ic-${c.userId}`}
                  onInvite={() => invite(`ic-${c.userId}`, { participantType: 'IC_SUPPORT', userId: c.userId }, c.fullName, close)}
                  onPreview={() => openEmailPreviewFor('VISIT_PARTICIPANT_INVITATION', { key: `ic-${c.userId}`, payload: { participantType: 'IC_SUPPORT', userId: c.userId }, displayName: c.fullName, recipient: { name: c.fullName, email: c.email, roleLabel: icSupportRoleLabel(c), departmentName: c.departmentName, campusName: c.campusName } })}
                />
              )}
            />
          )}
          {canManage && <PreviewLink onClick={() => openEmailPreview('VISIT_PARTICIPANT_INVITATION')} />}
          <ParticipantList
            rows={supporters}
            canManage={canManage}
            busyId={busyId}
            onRemove={remove}
            onViewSent={openSentEmails}
            emptyText="Chưa mời Staff hỗ trợ IC nào."
          />
        </Panel>

        {/* ── Sinh viên hỗ trợ ── */}
        <Panel title="Sinh viên hỗ trợ" icon={<GraduationCap className="w-5 h-5" />}>
          {canManage && (
            <SearchDropdown<ParticipantCandidate>
              placeholder="Tìm theo tên / mã SV..."
              emptyText="Không tìm thấy sinh viên hợp lệ trong campus này."
              search={(kw) => delegationsApi.getParticipantCandidates(visitInstanceId, 'STUDENT', kw)}
              onCloseRef={closeStudentDropdown}
              renderRow={(c, _i, close) => (
                <CandidateRow
                  key={c.userId}
                  candidate={c}
                  roleLabel="Sinh viên hỗ trợ"
                  subtitle={c.studentCode ? `MSSV: ${c.studentCode}` : undefined}
                  busy={busyId === `st-${c.userId}`}
                  onInvite={() => invite(`st-${c.userId}`, { participantType: 'STUDENT', userId: c.userId }, c.fullName, close)}
                  onPreview={() => openEmailPreviewFor('VISIT_STUDENT_INVITATION', { key: `st-${c.userId}`, payload: { participantType: 'STUDENT', userId: c.userId }, displayName: c.fullName, recipient: { name: c.fullName, email: c.email, roleLabel: 'Sinh viên hỗ trợ', campusName: c.campusName } })}
                />
              )}
            />
          )}
          {canManage && <PreviewLink onClick={() => openEmailPreview('VISIT_STUDENT_INVITATION')} />}
          <ParticipantList
            rows={students}
            canManage={canManage}
            busyId={busyId}
            onRemove={remove}
            onViewSent={openSentEmails}
            emptyText="Chưa mời sinh viên hỗ trợ nào."
          />
        </Panel>

        {/* ── Phòng ban hỗ trợ (mời Trưởng phòng) ── */}
        <Panel title="Phòng ban hỗ trợ" icon={<Building2 className="w-5 h-5" />} wide>
          {canManage && (
            <SearchDropdown<SupportDepartment>
              placeholder="Tìm phòng ban..."
              emptyText="Không tìm thấy phòng ban phù hợp."
              search={(kw) => delegationsApi.getSupportDepartments(visitInstanceId, kw)}
              onCloseRef={closeDeptDropdown}
              renderRow={(d, _i, close) => (
                <div key={d.departmentId} className="flex items-center justify-between gap-3 border-b border-gray-100 px-4 py-2.5 last:border-b-0">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-bold text-gray-800">{d.departmentName}</div>
                    <div className="truncate text-xs text-gray-500">
                      {d.leaderName ? `Trưởng phòng: ${d.leaderName}` : 'Chưa có trưởng phòng đang hoạt động'}
                    </div>
                    {!d.canInviteParticipant && d.participantDisabledReason && (
                      <div className="mt-0.5 text-[11px] font-medium text-amber-600">{d.participantDisabledReason}</div>
                    )}
                  </div>
                  <div className="flex shrink-0 items-center gap-1.5">
                    {d.canInviteParticipant && (
                      <button
                        type="button"
                        title="Xem trước & sửa email"
                        onClick={() => openEmailPreviewFor('VISIT_DEPARTMENT_LEADER_INVITATION', { key: `dept-${d.departmentId}`, payload: { participantType: 'DEPT_SUPPORT', departmentId: d.departmentId }, displayName: `trưởng phòng ${d.departmentName}`, recipient: { name: d.leaderName, email: d.leaderEmail, roleLabel: 'Trưởng phòng', departmentName: d.departmentName, campusName: d.campusName } })}
                        className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200 bg-white text-[#004c91] outline-none transition-colors hover:bg-gray-50"
                      >
                        <Eye className="w-3.5 h-3.5" />
                      </button>
                    )}
                    <button
                      type="button"
                      disabled={!d.canInviteParticipant || busyId === `dept-${d.departmentId}`}
                      onClick={() => {
                        if (!d.canInviteParticipant) {
                          pushToast('warning', d.participantDisabledReason || 'Không thể mời phòng ban này.');
                          return;
                        }
                        invite(
                          `dept-${d.departmentId}`,
                          { participantType: 'DEPT_SUPPORT', departmentId: d.departmentId },
                          `trưởng phòng ${d.departmentName}`,
                          close,
                        );
                      }}
                      className="inline-flex shrink-0 items-center gap-1 rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-40"
                    >
                      {busyId === `dept-${d.departmentId}` ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                      Mời
                    </button>
                  </div>
                </div>
              )}
            />
          )}
          {canManage && <PreviewLink onClick={() => openEmailPreview('VISIT_DEPARTMENT_LEADER_INVITATION')} />}
          <div className="mt-3 space-y-2">
            {departments.length === 0 ? (
              <p className="text-sm italic text-slate-400">Chưa mời phòng ban hỗ trợ nào.</p>
            ) : departments.map((p) => (
              <div key={p.participantId} className="rounded-xl border border-gray-200 bg-white p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0">
                    <div className="truncate text-sm font-bold text-gray-800">
                      {p.departmentName || 'Phòng ban'}
                    </div>
                    <div className="truncate text-xs text-gray-500">Trưởng phòng: {p.fullName}</div>
                  </div>
                  <div className="flex items-center gap-2">
                    <StatusBadge status={p.status} />
                    <ViewSentButton onClick={() => openSentEmails(p)} />
                    {canManage && p.status === 'INVITED' && (
                      <RemoveButton busy={busyId === `rm-${p.participantId}`} onClick={() => remove(p)} />
                    )}
                  </div>
                </div>
                {p.departmentAssignment?.assignedStaffName && (
                  <div className="mt-2 rounded-lg bg-blue-50/70 px-3 py-1.5 text-xs font-medium text-[#004c91]">
                    Nhân sự xử lý: <span className="font-bold">{p.departmentAssignment.assignedStaffName}</span>
                  </div>
                )}
                {p.status === 'DECLINED' && p.note && (
                  <div className="mt-2 text-xs italic text-red-500">Lý do từ chối: {p.note}</div>
                )}
              </div>
            ))}
          </div>
        </Panel>
      </div>

      {/* Editable "Xem trước email" modal (shared component). */}
      <EmailPreviewModal
        open={preview.open}
        loading={preview.loading}
        sending={preview.sending}
        restoring={preview.restoring}
        error={preview.error}
        subject={preview.subject}
        body={preview.body}
        initialFinalPreviewHtml={preview.initialFinalPreviewHtml}
        isActionTemplate={preview.isActionTemplate}
        systemActionDescription={preview.systemActionDescription}
        lockedActionBlockHtml={preview.lockedActionBlockHtml}
        recipient={preview.target?.recipient ?? null}
        replyToEmail={preview.replyToEmail}
        runtimeEditable={preview.runtimeEditable}
        previewToken={preview.previewToken}
        canSend={!!preview.target}
        sendLabel="Mời với nội dung này"
        pushToast={pushToast}
        onSubjectChange={(v) => setPreview((p) => ({ ...p, subject: v }))}
        onBodyChange={(v) => setPreview((p) => ({ ...p, body: v }))}
        onClose={closePreview}
        onRestore={restoreTemplate}
        onSend={sendWithEditedContent}
      />

      {/* "Xem mail đã gửi" history (per participant). */}
      <SentEmailsModal
        open={sentModal.open}
        title={sentModal.title}
        subtitle={sentModal.subtitle}
        targetKey={sentModal.participantId}
        load={() => delegationsApi.getParticipantSentEmails(visitInstanceId, sentModal.participantId!)}
        onClose={closeSentEmails}
      />
    </div>
  );
}

/** Display label for an IC_SUPPORT candidate — Staff Leader vs Staff, derived from subRole.
 * The participant role stays IC_SUPPORT either way; this is presentation only. */
export function icSupportRoleLabel(c: Pick<ParticipantCandidate, 'subRole'>): string {
  return c.subRole?.toUpperCase() === 'LEADER' ? 'Staff Leader hỗ trợ IC' : 'Staff hỗ trợ IC';
}

function PreviewLink({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-[#004c91] outline-none hover:underline"
    >
      <Eye className="w-3.5 h-3.5" /> Xem trước email mời
    </button>
  );
}

function Panel({ title, icon, wide, children }: { title: string; icon: React.ReactNode; wide?: boolean; children: React.ReactNode }) {
  return (
    <div className={`rounded-xl border border-gray-200 bg-white p-5 shadow-sm ${wide ? 'xl:col-span-2' : ''}`}>
      <h4 className="mb-4 flex items-center gap-2 text-base font-bold text-[#004c91]">{icon} {title}</h4>
      {children}
    </div>
  );
}

function CandidateRow({
  candidate, roleLabel, subtitle, busy, onInvite, onPreview,
}: { candidate: ParticipantCandidate; roleLabel?: string; subtitle?: string; busy: boolean; onInvite: () => void; onPreview?: () => void }) {
  const hasEmail = !!candidate.email && candidate.email.trim().length > 0;
  const canInvite = candidate.canInvite && hasEmail;
  return (
    <div className="flex items-center justify-between gap-3 border-b border-gray-100 px-4 py-2.5 last:border-b-0">
      <div className="min-w-0">
        <div className="truncate text-sm font-bold text-gray-800">{candidate.fullName}</div>
        <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-[11px] text-gray-500">
          {roleLabel && <span>{roleLabel}</span>}
          {candidate.departmentName && <span>{candidate.departmentName}</span>}
          {subtitle && <span>{subtitle}</span>}
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-1.5">
          <ConflictBadge
            count={candidate.conflictCount}
            allPrivate={candidate.hasPrivateConflict && candidate.conflictCount > 0}
          />
          {!hasEmail && (
            <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-red-500">
              <AlertCircle className="w-3 h-3" /> Chưa có email
            </span>
          )}
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-1.5">
        {onPreview && canInvite && (
          <button
            type="button"
            title="Xem trước & sửa email"
            disabled={busy}
            onClick={onPreview}
            className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200 bg-white text-[#004c91] outline-none transition-colors hover:bg-gray-50 disabled:opacity-40"
          >
            <Eye className="w-3.5 h-3.5" />
          </button>
        )}
        <button
          type="button"
          disabled={!canInvite || busy}
          title={!hasEmail ? 'Người nhận chưa có email, không thể gửi lời mời.' : undefined}
          onClick={onInvite}
          className="inline-flex items-center gap-1 rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-40"
        >
          {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
          Mời
        </button>
      </div>
    </div>
  );
}

/** Small "Xem mail đã gửi" icon button (opens the sent-email history modal for a target). */
function ViewSentButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      title="Xem mail đã gửi"
      onClick={onClick}
      className="inline-flex h-7 items-center gap-1 rounded-lg border border-gray-200 bg-white px-2 text-[11px] font-bold text-[#004c91] outline-none transition-colors hover:bg-gray-50"
    >
      <Eye className="w-3.5 h-3.5" /> Mail đã gửi
    </button>
  );
}

function ParticipantList({
  rows, canManage, busyId, onRemove, onViewSent, emptyText,
}: {
  rows: VisitParticipantListItem[];
  canManage: boolean;
  busyId: string | null;
  onRemove: (p: VisitParticipantListItem) => void;
  onViewSent: (p: VisitParticipantListItem) => void;
  emptyText: string;
}) {
  if (rows.length === 0) return <p className="mt-3 text-sm italic text-slate-400">{emptyText}</p>;
  return (
    <div className="mt-3 space-y-2">
      {rows.map((p) => (
        <div key={p.participantId} className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-gray-200 bg-white p-3">
          <div className="min-w-0">
            <div className="truncate text-sm font-bold text-gray-800">{p.fullName}</div>
            {p.invitedAt && (
              <div className="mt-0.5 text-[11px] text-gray-400">Đã gửi email lúc {fmtDateTime(p.invitedAt)}</div>
            )}
            {p.status === 'DECLINED' && p.note && (
              <div className="mt-1 text-xs italic text-red-500">Lý do từ chối: {p.note}</div>
            )}
          </div>
          <div className="flex items-center gap-2">
            <StatusBadge status={p.status} />
            <ViewSentButton onClick={() => onViewSent(p)} />
            {canManage && p.status === 'INVITED' && (
              <RemoveButton busy={busyId === `rm-${p.participantId}`} onClick={() => onRemove(p)} />
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

function RemoveButton({ busy, onClick }: { busy: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      title="Gỡ khỏi danh sách mời"
      disabled={busy}
      onClick={onClick}
      className="inline-flex h-7 w-7 items-center justify-center rounded-lg border border-red-200 bg-red-50 text-red-500 outline-none transition-colors hover:bg-red-100 disabled:opacity-40"
    >
      {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Trash2 className="w-3.5 h-3.5" />}
    </button>
  );
}

function apiError(e: any, fallback: string): string {
  const data = e?.response?.data;
  if (!data) return fallback;
  if (typeof data === 'string' && data.trim()) return data;
  if (data.message) return data.message;
  return fallback;
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
