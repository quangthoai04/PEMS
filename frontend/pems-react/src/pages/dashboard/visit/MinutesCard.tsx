/**
 * MinutesCard — biên bản cuộc họp cho 1 campus instance (Phase 3).
 *
 * Mỗi visit_instance chỉ có 1 biên bản. Chỉ Host hoặc participant đã ACCEPT mới tạo/sửa được, và
 * tại một thời điểm chỉ 1 người được sửa (cơ chế lock có token + hết hạn 10 phút). Mọi quyền do
 * backend quyết (canCreate/canEdit + trạng thái lock) — frontend chỉ render theo dữ liệu trả về.
 *
 * Khi tạo biên bản lần đầu backend tự fill người tham gia (Host + khách trong đơn + người nội bộ đã
 * ACCEPTED/ASSIGNED) vào minute_participants; Host chỉ điểm danh có mặt/vắng mặt và có thể thêm người
 * thủ công. Dữ liệu trong biên bản là snapshot riêng (không sửa ngược danh sách gốc).
 */
import { useCallback, useEffect, useRef, useState } from 'react';
import {
  ChevronUp, ChevronDown, FileText, Lock, Edit3, Save, X, Plus, Clock, Users, ClipboardList,
  Trash2, UserPlus, RefreshCw, Calendar, Building2, CheckCircle2, AlertCircle, Info,
  CheckSquare, Square,
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type {
  VisitMinute, SaveMinuteParticipantPayload, SaveMinuteActionItemPayload, AgendaResponsibleCandidate,
} from '../../../features/delegations/types/delegations.types';
import { partnersApi } from '../../../features/partners/api/partnersApi';
import type { VisitGuestPartnerLink } from '../../../features/partners/types/partners.types';
import { ParticipantPartnerCell } from '../../../features/partners/components/ParticipantPartnerCell';
import { RichTextEditor } from '../../../shared/components/RichTextEditor';
import { formatVietnamDateTime, toVietnamDateTimeLocalInput, vietnamNowDateTimeLocal } from '../../../shared/utils/vietnamTime';
const formatDateTime = (value?: string | null) =>
  value ? formatVietnamDateTime(value) : '-';
// ISO/DB value ("+07:00" hoặc chuỗi trần VN) → "yyyy-MM-ddTHH:mm" cho <input type="datetime-local">,
// luôn theo giờ Việt Nam — không drift khi browser ở timezone khác.
const toDateTimeLocalValue = (value?: string | null): string => toVietnamDateTimeLocalInput(value);
// datetime-local "yyyy-MM-ddTHH:mm" → payload "yyyy-MM-ddTHH:mm:ss" (keep the picked wall-clock as-is).
const toPayloadDateTime = (value: string): string => (value.length === 16 ? `${value}:00` : value);

// Same error-parsing shape as VisitProcess (handles message / errors[] / title envelopes).
const errMsg = (e: any, fallback: string): string => {
  const data = e?.response?.data;
  if (!data) return fallback;
  if (typeof data === 'string' && data.trim()) return data;
  if (data.message) return data.message;
  if (data.error) return data.error;
  if (data.errors) {
    const flat = Array.isArray(data.errors) ? data.errors : Object.values(data.errors).flat();
    const first = (flat as any[]).find((x) => typeof x === 'string' && x.trim());
    if (first) return first;
  }
  if (data.title) return data.title;
  return fallback;
};

// Lightweight in-page toast (top-right) — same pattern as VisitProcess/VisitRequestManagement.
type Toast = { id: number; type: 'success' | 'error' | 'info'; msg: string };

type DraftParticipant = {
  _key: string;
  minuteParticipantId: number; // 0 = new
  userId: number | null;
  guestMemberId: number | null;
  fullNameSnapshot: string;
  roleSnapshot: string;
  organizationSnapshot: string;
  emailSnapshot: string;
  attendanceStatus: string;
  attendanceNote: string;
  participantKind: string; // INTERNAL | GUEST | MANUAL
  guestNationality: string | null;
};

type DraftActionItem = {
  _key: string;
  actionItemId: number; // 0 = new
  title: string;
  note: string;
  assignedToUserId: number | null;
  /** Tên hiển thị người phụ trách từ server — chỉ dùng cho view-only (editing mode chọn qua responsibleCandidates). */
  assignedToUserName: string | null;
  dueDate: string; // datetime-local "YYYY-MM-DDTHH:mm" or ''
  status: string;
};

const KIND_META: Record<string, { label: string; cls: string }> = {
  INTERNAL: { label: 'Nội bộ', cls: 'bg-blue-50 text-blue-700 border-blue-200' },
  GUEST: { label: 'Khách', cls: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
  MANUAL: { label: 'Khách', cls: 'bg-indigo-50 text-indigo-700 border-indigo-200' },
};
// Điểm danh giờ là tick nhanh (không còn dropdown/badge trạng thái). Ánh xạ checkbox ↔ enum backend:
//   ticked   → PRESENT
//   unticked → ABSENT   (backend enum vẫn còn EXCUSED cho dữ liệu cũ, nhưng UI không dùng/không hiện)
const isPresentStatus = (status?: string | null) => status === 'PRESENT';
const ACTION_STATUS_META: Record<string, { label: string; cls: string }> = {
  TODO: { label: 'Chưa làm', cls: 'bg-gray-100 text-gray-600 border-gray-200' },
  IN_PROGRESS: { label: 'Đang làm', cls: 'bg-blue-50 text-blue-700 border-blue-200' },
  DONE: { label: 'Hoàn thành', cls: 'bg-green-50 text-green-700 border-green-200' },
  CANCELLED: { label: 'Đã hủy', cls: 'bg-red-50 text-red-600 border-red-200' },
};
// Dropdown chỉ cho chọn tay 2 trạng thái — "Đang làm" (dữ liệu cũ) và "Đã hủy" (chỉ sinh ra khi Host
// xóa đầu mục, không phải lựa chọn thủ công) vẫn hiển thị đúng màu qua ACTION_STATUS_META ở trên.
const ACTION_STATUS_OPTIONS = ['TODO', 'DONE'].map((value) => ({ value, label: ACTION_STATUS_META[value].label }));

export function MinutesCard({ visitInstanceId, isReadOnly = false }: { visitInstanceId: number; isReadOnly?: boolean }) {
  const [expanded, setExpanded] = useState(true);
  const [data, setData] = useState<VisitMinute | null>(null);
  const [loading, setLoading] = useState(true);
  // Persistent banner ONLY for the initial-load failure (when there is no card body to show).
  const [loadError, setLoadError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  // Toasts (top-right) — reuses the approve/reject/cancel pattern; transient notifications go here.
  const [toasts, setToasts] = useState<Toast[]>([]);
  const toastSeq = useRef(0);
  const pushToast = (type: Toast['type'], msg: string) => {
    const id = ++toastSeq.current;
    setToasts((prev) => [...prev, { id, type, msg }]);
    setTimeout(() => setToasts((prev) => prev.filter((t) => t.id !== id)), 4500);
  };

  // Inline field errors (rendered under the specific input/row that failed).
  const [titleError, setTitleError] = useState<string | null>(null);
  const [actionErrors, setActionErrors] = useState<Record<string, string>>({});
  const [participantErrors, setParticipantErrors] = useState<Record<string, string>>({});
  const clearActionError = (key: string) =>
    setActionErrors((prev) => {
      if (!prev[key]) return prev;
      const next = { ...prev };
      delete next[key];
      return next;
    });

  // Editing session (only set while this user holds the lock).
  const [editing, setEditing] = useState(false);
  const [draftTitle, setDraftTitle] = useState('');
  const [draftContent, setDraftContent] = useState('');
  const [draftParticipants, setDraftParticipants] = useState<DraftParticipant[]>([]);
  const [draftActionItems, setDraftActionItems] = useState<DraftActionItem[]>([]);
  // "Người phụ trách" picker for action items — Host + ACCEPTED IC_SUPPORT/DEPT_SUPPORT/STUDENT
  // participants of this instance (never guests). Reuses the endpoint built for the Agenda editor.
  const [responsibleCandidates, setResponsibleCandidates] = useState<AgendaResponsibleCandidate[]>([]);
  const tokenRef = useRef<string | null>(null);
  const minutesIdRef = useRef<number | null>(null);
  const rowVersionRef = useRef<number>(0);
  const expiresRef = useRef<string | null>(null);
  const keyRef = useRef(0);
  const [remainingMs, setRemainingMs] = useState<number>(0);

  const nextKey = (prefix: string) => `${prefix}-new-${++keyRef.current}`;

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const d = await delegationsApi.minutes.get(visitInstanceId);
      setData(d);
      setLoadError(null);
    } catch (e: any) {
      setLoadError(errMsg(e, 'Không thể tải biên bản. Vui lòng thử lại.'));
    } finally {
      setLoading(false);
    }
  }, [visitInstanceId]);

  useEffect(() => { load(); }, [load]);

  // Partner-link badges for the participants table (docs/PARTNER_canh/01 §10.3).
  const [partnerLinks, setPartnerLinks] = useState<VisitGuestPartnerLink[]>([]);
  const loadPartnerLinks = useCallback(async () => {
    try { setPartnerLinks(await partnersApi.getVisitPartnerLinks(visitInstanceId)); }
    catch { setPartnerLinks([]); }
  }, [visitInstanceId]);
  useEffect(() => { void loadPartnerLinks(); }, [loadPartnerLinks]);

  const loadResponsibleCandidates = useCallback(async () => {
    try { setResponsibleCandidates(await delegationsApi.getAgendaResponsibleCandidates(visitInstanceId)); }
    catch { setResponsibleCandidates([]); }
  }, [visitInstanceId]);
  useEffect(() => { void loadResponsibleCandidates(); }, [loadResponsibleCandidates]);
  const findPartnerLink = (p: { minuteParticipantId: number; guestMemberId: number | null }) =>
    partnerLinks.find(
      (l) =>
        (p.minuteParticipantId > 0 && l.minuteParticipantId === p.minuteParticipantId)
        || (p.guestMemberId != null && l.guestMemberId === p.guestMemberId),
    ) ?? null;

  // Countdown of the edit session; auto-exit when the lock expires.
  useEffect(() => {
    if (!editing) return;
    const tick = () => {
      const ms = expiresRef.current ? new Date(expiresRef.current).getTime() - Date.now() : 0;
      setRemainingMs(ms);
      if (ms <= 0) { setEditing(false); load(); }
    };
    tick();
    const t = setInterval(tick, 1000);
    return () => clearInterval(t);
  }, [editing, load]);

  const enterEditing = (d: VisitMinute) => {
    tokenRef.current = d.editLockToken ?? null;
    minutesIdRef.current = d.minutesId ?? null;
    rowVersionRef.current = d.rowVersion;
    expiresRef.current = d.editLockExpiresAt ?? null;
    setDraftTitle(d.title ?? 'Biên bản cuộc họp');
    setDraftContent(d.content ?? '');
    setDraftParticipants((d.participants ?? []).map((p) => ({
      _key: `p-${p.minuteParticipantId}`,
      minuteParticipantId: p.minuteParticipantId,
      userId: p.userId,
      guestMemberId: p.guestMemberId,
      fullNameSnapshot: p.fullNameSnapshot ?? '',
      roleSnapshot: p.roleSnapshot ?? '',
      organizationSnapshot: p.organizationSnapshot ?? '',
      emailSnapshot: p.emailSnapshot ?? '',
      attendanceStatus: p.attendanceStatus || 'ABSENT',
      attendanceNote: p.attendanceNote ?? '',
      participantKind: p.participantKind,
      guestNationality: p.guestNationality,
    })));
    setDraftActionItems((d.actionItems ?? []).map((a) => ({
      _key: `a-${a.actionItemId}`,
      actionItemId: a.actionItemId,
      title: a.title ?? '',
      note: a.note ?? '',
      assignedToUserId: a.assignedToUserId ?? null,
      assignedToUserName: a.assignedToUserName ?? null,
      dueDate: toDateTimeLocalValue(a.dueDate),
      status: a.status || 'TODO',
    })));
    setData(d);
    setEditing(true);
    setLoadError(null);
    setTitleError(null);
    setActionErrors({});
    setParticipantErrors({});
  };

  const handleCreate = async () => {
    setBusy(true);
    try { enterEditing(await delegationsApi.minutes.createOrLock(visitInstanceId)); }
    catch (e: any) { pushToast('error', errMsg(e, 'Không thể tạo biên bản. Vui lòng thử lại.')); }
    finally { setBusy(false); }
  };

  const handleEdit = async () => {
    if (!data?.minutesId) return;
    setBusy(true);
    try { enterEditing(await delegationsApi.minutes.acquireLock(data.minutesId)); }
    catch (e: any) { pushToast('error', errMsg(e, 'Không thể mở biên bản để chỉnh sửa.')); await load(); }
    finally { setBusy(false); }
  };

  // A row the user never touched (default empty) — must NOT be sent to the backend.
  const isBlankActionItem = (a: DraftActionItem) =>
    !a.title.trim() && !a.note.trim() && !a.dueDate && !a.assignedToUserId && a.status === 'TODO';

  const handleSave = async () => {
    if (!minutesIdRef.current || !tokenRef.current) return;

    // Re-validate from a clean slate so stale field errors don't linger.
    setTitleError(null);
    setActionErrors({});
    setParticipantErrors({});

    // 1) Tiêu đề bắt buộc.
    let titleErr: string | null = null;
    if (!draftTitle.trim()) titleErr = 'Vui lòng nhập tên biên bản.';

    // 2) Đầu mục công việc: bỏ qua dòng rỗng hoàn toàn; dòng đã nhập một phần phải có nội dung.
    // Hạn hoàn thành (nếu có) không được ở quá khứ — cùng quy tắc với input min= bên dưới,
    // chặn thêm cả trường hợp gõ tay bỏ qua giao diện lịch.
    const filledActions = draftActionItems.filter((a) => !isBlankActionItem(a));
    const nextActionErrors: Record<string, string> = {};
    const nowLocal = vietnamNowDateTimeLocal();
    for (const a of filledActions) {
      if (!a.title.trim()) { nextActionErrors[a._key] = 'Vui lòng nhập nội dung công việc.'; continue; }
      if (a.dueDate && a.dueDate < nowLocal) nextActionErrors[a._key] = 'Hạn hoàn thành không được ở quá khứ.';
    }

    const hasActionErr = Object.keys(nextActionErrors).length > 0;
    if (titleErr || hasActionErr) {
      if (titleErr) setTitleError(titleErr);
      if (hasActionErr) setActionErrors(nextActionErrors);
      // Inline error tại field + toast tổng quát ở góc phải trên; không gọi API.
      pushToast('error', hasActionErr
        ? 'Vui lòng kiểm tra lại các đầu mục công việc.'
        : 'Vui lòng kiểm tra lại thông tin biên bản.');
      return;
    }

    // 3) Build payload sạch: bỏ participant ngoài hệ thống + bỏ trùng userId/guestMemberId.
    const seenUser = new Set<number>();
    const seenGuest = new Set<number>();
    const participants: SaveMinuteParticipantPayload[] = [];
    for (const p of draftParticipants) {
      const isNew = p.minuteParticipantId <= 0;
      const isExternal = p.userId == null && p.guestMemberId == null;
      if (isNew && isExternal && !p.fullNameSnapshot.trim()) continue; // skip blank empty manual rows
      if (p.userId != null) {
        if (seenUser.has(p.userId)) continue;
        seenUser.add(p.userId);
      } else if (p.guestMemberId != null) {
        if (seenGuest.has(p.guestMemberId)) continue;
        seenGuest.add(p.guestMemberId);
      }
      participants.push({
        minuteParticipantId: p.minuteParticipantId > 0 ? p.minuteParticipantId : null,
        userId: p.userId,
        guestMemberId: p.guestMemberId,
        fullNameSnapshot: p.fullNameSnapshot.trim(),
        roleSnapshot: p.roleSnapshot.trim() || null,
        organizationSnapshot: p.organizationSnapshot.trim() || null,
        emailSnapshot: p.emailSnapshot.trim() || null,
        attendanceStatus: p.attendanceStatus,
        attendanceNote: p.attendanceNote.trim() || null,
      });
    }
    const actionItems: SaveMinuteActionItemPayload[] = filledActions.map((a) => ({
      actionItemId: a.actionItemId > 0 ? a.actionItemId : null,
      title: a.title.trim(),
      note: a.note.trim() || null,
      assignedToUserId: a.assignedToUserId ?? null,
      dueDate: a.dueDate ? toPayloadDateTime(a.dueDate) : null,
      status: a.status,
    }));

    setBusy(true);
    try {
      const d = await delegationsApi.minutes.save(minutesIdRef.current, {
        title: draftTitle.trim(),
        content: draftContent,
        editLockToken: tokenRef.current,
        rowVersion: rowVersionRef.current,
        participants,
        actionItems,
      });
      setEditing(false);
      tokenRef.current = null;
      setData(d);
      setLoadError(null);
      pushToast('success', 'Đã lưu biên bản cuộc họp.');
    } catch (e: any) {
      const code = e?.response?.data?.errorCode;
      const msg = errMsg(e, 'Không thể lưu biên bản. Vui lòng thử lại.');
      // Map known backend field errors back to the matching input where possible.
      if (/tiêu đề|tên biên bản/i.test(msg)) setTitleError(msg);
      // Guest scope error → flag the newly-synced guest rows so the user knows which to re-sync.
      if (code === 'MINUTE_GUEST_NOT_IN_CURRENT_REQUEST') {
        const marks: Record<string, string> = {};
        for (const p of draftParticipants) {
          if (p.minuteParticipantId <= 0 && p.guestMemberId != null && p.userId == null)
            marks[p._key] = 'Khách này không thuộc đoàn hiện tại.';
        }
        if (Object.keys(marks).length > 0) setParticipantErrors(marks);
      }
      pushToast('error', msg);
    } finally { setBusy(false); }
  };

  const handleCancel = async () => {
    const id = minutesIdRef.current, token = tokenRef.current;
    setEditing(false);
    tokenRef.current = null;
    if (id && token) {
      try { await delegationsApi.minutes.releaseLock(id, token); } catch { /* lock will expire anyway */ }
    }
    await load();
  };

  // Best-effort: release the lock if the user navigates away mid-edit.
  useEffect(() => {
    return () => {
      const id = minutesIdRef.current, token = tokenRef.current;
      if (id && token) { delegationsApi.minutes.releaseLock(id, token).catch(() => {}); }
    };
  }, []);

  const updateParticipant = (key: string, patch: Partial<DraftParticipant>) =>
    setDraftParticipants((prev) => prev.map((p) => (p._key === key ? { ...p, ...patch } : p)));
  const removeParticipant = (key: string) =>
    setDraftParticipants((prev) => prev.filter((p) => p._key !== key));
  const updateActionItem = (key: string, patch: Partial<DraftActionItem>) =>
    setDraftActionItems((prev) => prev.map((a) => (a._key === key ? { ...a, ...patch } : a)));
  const removeActionItem = (key: string) =>
    setDraftActionItems((prev) => prev.filter((a) => a._key !== key));

  const handleAddParticipantRow = () => {
    setDraftParticipants((prev) => [...prev, {
      _key: nextKey('p'),
      minuteParticipantId: 0,
      userId: null,
      guestMemberId: null,
      fullNameSnapshot: '',
      roleSnapshot: '',
      organizationSnapshot: '',
      emailSnapshot: '',
      attendanceStatus: 'ABSENT',
      attendanceNote: '',
      participantKind: 'GUEST',
      guestNationality: null,
    }]);
  };

  const handleSyncNew = async () => {
    const id = minutesIdRef.current;
    if (!id) return;
    setBusy(true);
    try {
      const candidates = await delegationsApi.minutes.newParticipantCandidates(id);
      const haveUser = new Set(draftParticipants.filter((p) => p.userId != null).map((p) => p.userId));
      const haveGuest = new Set(draftParticipants.filter((p) => p.guestMemberId != null).map((p) => p.guestMemberId));
      const fresh = candidates.filter((c) =>
        (c.userId != null && !haveUser.has(c.userId)) || (c.guestMemberId != null && !haveGuest.has(c.guestMemberId)));
      // No-op sync is NOT an error and must not block saving — just an info toast.
      if (fresh.length === 0) { pushToast('info', 'Không có người mới cần đồng bộ.'); return; }
      setDraftParticipants((prev) => [...prev, ...fresh.map((c) => ({
        _key: nextKey('sync'),
        minuteParticipantId: 0,
        userId: c.userId,
        guestMemberId: c.guestMemberId,
        fullNameSnapshot: c.fullNameSnapshot ?? '',
        roleSnapshot: c.roleSnapshot ?? '',
        organizationSnapshot: c.organizationSnapshot ?? '',
        emailSnapshot: c.emailSnapshot ?? '',
        attendanceStatus: c.attendanceStatus || 'ABSENT',
        attendanceNote: c.attendanceNote ?? '',
        participantKind: c.participantKind,
        guestNationality: null,
      }))]);
      pushToast('success', 'Đã đồng bộ người tham gia mới.');
    } catch (e: any) {
      // On failure keep the user's in-progress edits intact — only surface a toast.
      pushToast('error', errMsg(e, 'Không thể đồng bộ người mới. Vui lòng thử lại.'));
    } finally { setBusy(false); }
  };

  const mm = String(Math.max(0, Math.floor(remainingMs / 60000))).padStart(2, '0');
  const ss = String(Math.max(0, Math.floor((remainingMs % 60000) / 1000))).padStart(2, '0');

  // Unified render rows (same shape whether editing the draft or viewing the saved snapshot).
  const participantRows: DraftParticipant[] = editing
    ? draftParticipants
    : (data?.participants ?? []).map((p) => ({
        _key: `v-${p.minuteParticipantId}`,
        minuteParticipantId: p.minuteParticipantId,
        userId: p.userId,
        guestMemberId: p.guestMemberId,
        fullNameSnapshot: p.fullNameSnapshot ?? '',
        roleSnapshot: p.roleSnapshot ?? '',
        organizationSnapshot: p.organizationSnapshot ?? '',
        emailSnapshot: p.emailSnapshot ?? '',
        attendanceStatus: p.attendanceStatus || 'ABSENT',
        attendanceNote: p.attendanceNote ?? '',
        participantKind: p.participantKind,
        guestNationality: p.guestNationality,
      }));
  const actionRows: DraftActionItem[] = editing
    ? draftActionItems
    : (data?.actionItems ?? []).map((a) => ({
        _key: `v-${a.actionItemId}`,
        actionItemId: a.actionItemId,
        title: a.title ?? '',
        note: a.note ?? '',
        assignedToUserId: a.assignedToUserId ?? null,
        assignedToUserName: a.assignedToUserName ?? null,
        dueDate: toDateTimeLocalValue(a.dueDate),
        status: a.status || 'TODO',
      }));

  const presentCount = participantRows.filter((p) => p.attendanceStatus === 'PRESENT').length;

  return (
    <div className="bg-white rounded-2xl border border-gray-200 overflow-hidden shadow-sm transition-all relative">
      {/* Header — mẫu cũ: nền xanh #004c91, badge số 1 màu cam */}
      <div
        className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] cursor-pointer"
        onClick={() => setExpanded(!expanded)}
      >
        <h2 className="text-sm font-bold text-white tracking-tight uppercase flex items-center gap-2">
          <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black shrink-0">1</span>
          Biên bản cuộc họp
        </h2>
        <button type="button" className="text-white hover:bg-white/20 p-1 rounded-full transition-colors">
          {expanded ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
        </button>
      </div>

      <AnimatePresence>
        {expanded && (
          <motion.div initial={{ height: 0 }} animate={{ height: 'auto' }} exit={{ height: 0 }} className="overflow-hidden">
            <div className="p-4 sm:p-6 md:p-8">
              {/* Chỉ giữ banner cho lỗi tải dữ liệu (khi không có gì để hiển thị). Mọi thông báo
                  thao tác (lưu/đồng bộ/thêm người) dùng toast ở góc phải trên. */}
              {loadError && (
                <div className="mb-4 rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-medium text-red-700">{loadError}</div>
              )}

              {loading ? (
                <div className="py-8 text-center text-slate-500 font-medium">Đang tải biên bản...</div>
              ) : !data ? null : !data.exists ? (
                /* Chưa có biên bản — nút tạo màu cam như mẫu cũ */
                <div className="text-center py-6">
                  <FileText className="w-10 h-10 mx-auto text-slate-300 mb-3" />
                  <p className="text-slate-600 font-medium mb-4">Chưa có biên bản cho chuyến thăm này.</p>
                  {data.canCreate && !isReadOnly && (
                    <button type="button" onClick={handleCreate} disabled={busy}
                      className="px-6 py-3 bg-[#f37021] text-white font-bold rounded-xl shadow-sm hover:bg-[#e0611d] transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                      <Plus className="w-5 h-5" /> {busy ? 'Đang tạo...' : 'Tạo biên bản cuộc họp'}
                    </button>
                  )}
                </div>
              ) : (
                <>
                  {editing ? (
                    <div className="mb-6 flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2.5 text-sm font-bold text-amber-800">
                      <Clock className="w-4 h-4" /> Bạn đang chỉnh sửa biên bản. Phiên sửa còn {mm}:{ss}
                    </div>
                  ) : data.isLockedByOther ? (
                    <div className="mb-6 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm font-medium text-amber-800">
                      <Lock className="w-4 h-4 mt-0.5 shrink-0" />
                      <span>
                        Biên bản đang được chỉnh sửa bởi <b>{data.editLockedByName || 'người khác'}</b>. Bạn chỉ có thể xem nội dung hiện tại;
                        quyền sửa sẽ mở lại sau khi người này lưu hoặc phiên sửa hết hạn.
                      </span>
                    </div>
                  ) : null}

                  {/* Tên biên bản + trạng thái */}
                  <div className="flex flex-col sm:flex-row sm:items-start gap-6 mb-8">
                    <div className="flex-1 w-full max-w-[450px]">
                      <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">
                        Tên biên bản {editing && <span className="text-red-500">*</span>}
                      </label>
                      <input
                        type="text"
                        value={editing ? draftTitle : (data.title || 'Biên bản cuộc họp')}
                        onChange={(e) => { setDraftTitle(e.target.value); setTitleError(null); }}
                        readOnly={!editing}
                        placeholder="Nhập tên biên bản..."
                        className={`px-4 py-2.5 rounded-xl font-bold border outline-none w-full transition-all ${
                          editing
                            ? (titleError
                                ? 'bg-red-50 text-red-900 border-red-300 focus:ring-2 focus:ring-red-200 focus:border-red-500'
                                : 'bg-blue-50 text-blue-900 border-blue-200 focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91]')
                            : 'bg-gray-50 text-gray-800 border-gray-200 cursor-default'
                        }`}
                      />
                      {editing && titleError && (
                        <p className="mt-1.5 ml-1 text-xs font-semibold text-red-600">{titleError}</p>
                      )}
                    </div>
                    <div>
                      <label className="block text-sm font-bold text-gray-700 mb-2 ml-1">Trạng thái</label>
                      <div className="bg-gray-50 text-gray-700 px-4 py-2.5 rounded-xl font-bold flex items-center gap-2 border border-gray-200">
                        <FileText className="w-5 h-5 text-[#004c91] shrink-0" />
                        <span className="whitespace-nowrap">
                          {data.status === 'SAVED' ? 'Đã lưu' : 'Bản nháp'}{data.updatedAt ? ` · ${formatDateTime(data.updatedAt)}` : ''}
                        </span>
                      </div>
                    </div>
                  </div>

                  {/* Bảng người tham gia — dữ liệu THẬT từ minute_participants */}
                  <div className="mb-8 overflow-hidden rounded-xl border border-gray-200 bg-white">
                    <div className="bg-gray-50/70 px-5 py-3 border-b border-gray-200 flex flex-wrap items-center justify-between gap-2">
                      <h3 className="text-sm font-bold text-gray-800 flex items-center gap-2">
                        <Users className="w-4 h-4 text-[#004c91]" />
                        Bảng danh sách chi tiết người tham gia cuộc họp
                      </h3>
                      <div className="flex items-center gap-2">
                        <span className="text-xs bg-green-50 text-green-700 border border-green-200 px-2.5 py-1 rounded-full font-bold">
                          {presentCount} có mặt
                        </span>
                        <span className="text-xs bg-[#004c91]/10 text-[#004c91] px-2.5 py-1 rounded-full font-bold">
                          {participantRows.length} thành viên
                        </span>
                      </div>
                    </div>

                    {participantRows.length === 0 && !editing ? (
                      <div className="px-5 py-8 text-center text-sm text-slate-400 italic">
                        Chưa có dữ liệu người tham gia.
                      </div>
                    ) : (
                      <div className="overflow-x-auto">
                        <table className="w-full text-left border-collapse text-sm min-w-[900px]">
                          <thead className="bg-gray-100/50 text-[11px] uppercase tracking-wider text-gray-500 font-extrabold">
                            <tr className="border-b border-gray-200">
                              <th className="px-4 py-3 w-16 text-center whitespace-nowrap">Điểm danh</th>
                              <th className="px-4 py-3 w-48">Họ tên</th>
                              <th className="px-4 py-3 w-44">Vai trò / chức danh</th>
                              <th className="px-4 py-3 w-56">Đơn vị</th>
                              <th className="px-4 py-3 w-28">Loại nguồn</th>
                              {!editing && <th className="px-4 py-3 w-44">Đối tác</th>}
                              <th className="px-4 py-3 w-52">Ghi chú</th>
                              {editing && <th className="px-4 py-3 w-14 text-center">Xóa</th>}
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-100">
                             {participantRows.map((p) => {
                              const selectedKind = p.participantKind === 'INTERNAL' ? 'INTERNAL' : 'GUEST';
                              const kind = KIND_META[selectedKind];
                              const isNewRow = p.minuteParticipantId <= 0;
                              const rowError = participantErrors[p._key];
                              const isPresent = isPresentStatus(p.attendanceStatus);
                              return (
                                <tr key={p._key} className={`transition-colors align-top ${rowError ? 'bg-red-50/70' : 'hover:bg-gray-50/55'}`}>
                                  <td className="px-4 py-3 text-center">
                                    {/* Điểm danh = chỉ hiển thị ô tích checkbox, KHÔNG kèm chữ */}
                                    <button
                                      type="button"
                                      disabled={!editing}
                                      onClick={() => updateParticipant(p._key, { attendanceStatus: isPresent ? 'ABSENT' : 'PRESENT' })}
                                      aria-pressed={isPresent}
                                      aria-label={isPresent ? 'Bỏ đánh dấu có mặt' : 'Đánh dấu có mặt'}
                                      title={isPresent ? 'Có mặt' : 'Vắng mặt'}
                                      className="inline-flex items-center justify-center rounded-lg p-1 transition-colors disabled:cursor-default enabled:hover:bg-slate-100"
                                    >
                                      {isPresent
                                        ? <CheckSquare className="h-5 w-5 shrink-0 text-emerald-600" />
                                        : <Square className="h-5 w-5 shrink-0 text-slate-400" />}
                                    </button>
                                  </td>
                                  <td className="px-4 py-3">
                                    {editing && isNewRow ? (
                                      <input
                                        value={p.fullNameSnapshot}
                                        onChange={(e) => updateParticipant(p._key, { fullNameSnapshot: e.target.value })}
                                        placeholder="Nhập họ tên..."
                                        className="w-full text-sm font-semibold rounded-lg border border-gray-300 px-2 py-1.5 outline-none focus:border-[#004c91]"
                                      />
                                    ) : (
                                      <span className="font-semibold text-gray-900">{p.fullNameSnapshot || '-'}</span>
                                    )}
                                    {rowError && (
                                      <p className="mt-1 text-xs font-semibold text-red-600">{rowError}</p>
                                    )}
                                  </td>
                                  <td className="px-4 py-3">
                                    {editing && isNewRow ? (
                                      <input
                                        value={p.roleSnapshot}
                                        onChange={(e) => updateParticipant(p._key, { roleSnapshot: e.target.value })}
                                        placeholder="Vai trò..."
                                        className="w-full text-sm rounded-lg border border-gray-300 px-2 py-1.5 outline-none focus:border-[#004c91]"
                                      />
                                    ) : (
                                      <span className="text-gray-700">{p.roleSnapshot || '-'}</span>
                                    )}
                                  </td>
                                  <td className="px-4 py-3">
                                    {editing && isNewRow ? (
                                      <input
                                        value={p.organizationSnapshot}
                                        onChange={(e) => updateParticipant(p._key, { organizationSnapshot: e.target.value })}
                                        placeholder="Đơn vị..."
                                        className="w-full text-sm rounded-lg border border-gray-300 px-2 py-1.5 outline-none focus:border-[#004c91]"
                                      />
                                    ) : (
                                      <span className="inline-flex items-center gap-1.5 text-gray-700">
                                        <Building2 className="w-3.5 h-3.5 text-gray-400 shrink-0" />{p.organizationSnapshot || '-'}
                                      </span>
                                    )}
                                  </td>
                                  <td className="px-4 py-3">
                                    {editing ? (
                                      <select
                                        value={selectedKind}
                                        onChange={(e) => updateParticipant(p._key, { participantKind: e.target.value })}
                                        className="text-xs font-bold rounded-lg border border-gray-300 px-2 py-1.5 outline-none bg-white focus:border-[#004c91] shadow-xs cursor-pointer"
                                      >
                                        <option value="INTERNAL">Nội bộ</option>
                                        <option value="GUEST">Khách</option>
                                      </select>
                                    ) : (
                                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-[11px] font-bold border ${kind.cls}`}>{kind.label}</span>
                                    )}
                                  </td>
                                  {!editing && (
                                    <td className="px-4 py-3">
                                      <ParticipantPartnerCell
                                        visitInstanceId={visitInstanceId}
                                        participantKind={p.participantKind}
                                        minuteParticipantId={p.minuteParticipantId}
                                        guestMemberId={p.guestMemberId}
                                        link={findPartnerLink(p)}
                                        canManage={!isReadOnly}
                                        prefillOrganization={p.organizationSnapshot}
                                        prefillContactName={p.fullNameSnapshot}
                                        prefillContactEmail={p.emailSnapshot}
                                        prefillJobTitle={p.roleSnapshot}
                                        prefillNationality={p.guestNationality}
                                        sourceLabel={data?.minutesId ? `biên bản cuộc họp #${data.minutesId}` : 'biên bản cuộc họp'}
                                        onChanged={() => { void loadPartnerLinks(); }}
                                      />
                                    </td>
                                  )}
                                  <td className="px-4 py-3">
                                    {editing ? (
                                      <input
                                        value={p.attendanceNote}
                                        onChange={(e) => updateParticipant(p._key, { attendanceNote: e.target.value })}
                                        placeholder="Ghi chú điểm danh..."
                                        className="w-full text-sm rounded-lg border border-gray-300 px-2 py-1.5 outline-none focus:border-[#004c91]"
                                      />
                                    ) : (
                                      <div title={p.attendanceNote || ''} className="max-w-[200px] line-clamp-2 text-xs text-gray-500">
                                        {p.attendanceNote || '—'}
                                      </div>
                                    )}
                                  </td>
                                  {editing && (
                                    <td className="px-4 py-3 text-center">
                                      <button
                                        type="button"
                                        onClick={() => removeParticipant(p._key)}
                                        className="text-gray-400 hover:text-red-600 p-1.5 rounded-lg hover:bg-red-50 transition-colors"
                                      >
                                        <Trash2 className="w-4 h-4" />
                                      </button>
                                    </td>
                                  )}
                                </tr>
                              );
                            })}
                            {editing && participantRows.length === 0 && (
                              <tr><td colSpan={7} className="px-5 py-6 text-center text-sm text-slate-400 italic">Chưa có người tham gia. Dùng nút bên dưới để thêm người mới.</td></tr>
                            )}
                          </tbody>
                        </table>
                      </div>
                    )}

                    {/* Hành động trên danh sách — chỉ khi đang edit */}
                    {editing && (
                      <div className="px-5 py-3 border-t border-gray-200 bg-gray-50/40 space-y-3">
                        <div className="flex flex-wrap items-center gap-2">
                          <button
                            type="button"
                            onClick={handleAddParticipantRow}
                            className="inline-flex items-center gap-1.5 px-3 py-2 text-xs font-bold bg-[#004c91] hover:bg-[#00386b] text-white rounded-lg shadow-sm transition-colors"
                          >
                            <UserPlus className="w-4 h-4" /> Thêm người tham gia
                          </button>
                          <button
                            type="button"
                            onClick={handleSyncNew}
                            disabled={busy}
                            className="inline-flex items-center gap-1.5 px-3 py-2 text-xs font-bold bg-white border border-[#004c91]/30 text-[#004c91] hover:bg-blue-50 rounded-lg transition-colors disabled:opacity-50"
                          >
                            <RefreshCw className="w-4 h-4" /> Đồng bộ người mới
                          </button>
                        </div>
                      </div>
                    )}
                  </div>

                  {/* Ghi chú / Nội dung biên bản — dữ liệu THẬT */}
                  <div className="space-y-6">
                    <div>
                      <h3 className="text-base font-bold text-gray-800 mb-3 ml-2 relative before:content-[''] before:absolute before:left-[-12px] before:top-[6px] before:w-1.5 before:h-1.5 before:bg-[#f37021] before:rounded-full">
                        Ghi chú / Nội dung biên bản
                      </h3>
                      {editing ? (
                        <RichTextEditor
                          value={draftContent}
                          onChange={setDraftContent}
                          placeholder="Nhập ghi chú hoặc nội dung biên bản cuộc họp..."
                        />
                      ) : data.content?.trim() ? (
                        // ql-editor (không phải "prose" — plugin @tailwindcss/typography chưa cài nên
                        // class đó không có hiệu lực gì) tái dùng đúng CSS Quill dùng để vẽ bullet/số
                        // thứ tự/indent/align lúc đang gõ, để nội dung xem lại giống hệt lúc lưu.
                        <div
                          className="ql-editor !h-auto !min-h-[120px] !p-4 w-full rounded-xl border border-gray-200 bg-gray-50/60 text-sm text-gray-800"
                          dangerouslySetInnerHTML={{ __html: sanitizeHtml(data.content) }}
                        />
                      ) : (
                        <div className="w-full rounded-xl border border-gray-200 bg-gray-50/60 p-4 min-h-[120px] text-sm text-slate-400 italic">
                          Chưa có nội dung.
                        </div>
                      )}
                    </div>

                    {/* Đầu mục công việc — dữ liệu THẬT từ minute_action_items */}
                    <div>
                      <h3 className="text-base font-bold text-gray-800 mb-3 ml-2 relative before:content-[''] before:absolute before:left-[-12px] before:top-[6px] before:w-1.5 before:h-1.5 before:bg-[#004c91] before:rounded-full">
                        Đầu mục công việc
                      </h3>
                      {actionRows.length === 0 && !editing ? (
                        <div className="bg-gray-50/50 border border-gray-100 rounded-xl p-6 text-center text-sm text-slate-400 italic flex items-center justify-center gap-2">
                          <ClipboardList className="w-5 h-5 text-slate-300" /> Chưa có đầu mục công việc.
                        </div>
                      ) : (
                        <div className="space-y-3 bg-gray-50/50 border border-gray-100 rounded-xl p-4">
                          {actionRows.map((a) => {
                            const meta = ACTION_STATUS_META[a.status] ?? ACTION_STATUS_META.TODO;
                            return (
                              <div key={a._key} className="flex flex-col gap-3 bg-white p-3 rounded-lg border border-gray-200 shadow-sm">
                                <div className="flex flex-col sm:flex-row sm:items-center gap-3">
                                  {editing ? (
                                    <div className="flex-1">
                                      <input value={a.title} onChange={(e) => { updateActionItem(a._key, { title: e.target.value }); clearActionError(a._key); }}
                                        placeholder="Nội dung công việc..."
                                        className={`w-full bg-transparent text-sm font-medium rounded-lg border px-3 py-2 outline-none ${actionErrors[a._key] ? 'border-red-400 focus:border-red-500' : 'border-gray-300 focus:border-[#004c91]'} ${a.status === 'DONE' ? 'line-through text-gray-400' : 'text-gray-800'}`} />
                                      {actionErrors[a._key] && (
                                        <p className="mt-1 text-xs font-semibold text-red-600">{actionErrors[a._key]}</p>
                                      )}
                                    </div>
                                  ) : (
                                    <span className={`flex-1 text-sm font-medium ${a.status === 'DONE' ? 'line-through text-gray-400' : 'text-gray-800'}`}>{a.title || '-'}</span>
                                  )}
                                  <div className="flex items-center gap-2">
                                    <UserPlus className="w-4 h-4 text-[#004c91] shrink-0" />
                                    {editing ? (
                                      <select value={a.assignedToUserId ?? ''}
                                        onChange={(e) => updateActionItem(a._key, { assignedToUserId: e.target.value ? Number(e.target.value) : null })}
                                        className="text-xs font-bold rounded-md border border-gray-300 px-2 py-1.5 outline-none focus:border-[#004c91] bg-white max-w-[190px]">
                                        <option value="">Chưa chọn người phụ trách</option>
                                        {responsibleCandidates.map((c) => (
                                          <option key={c.userId} value={c.userId}>{c.fullName} — {c.displayRole}</option>
                                        ))}
                                      </select>
                                    ) : (
                                      <span className="text-xs font-semibold text-[#004c91]">
                                        {a.assignedToUserId
                                          ? (a.assignedToUserName
                                              ?? responsibleCandidates.find((c) => c.userId === a.assignedToUserId)?.fullName
                                              ?? `Người dùng #${a.assignedToUserId}`)
                                          : 'Chưa phân công'}
                                      </span>
                                    )}
                                  </div>
                                  <div className="flex items-center gap-2">
                                    <Calendar className="w-4 h-4 text-orange-500 shrink-0" />
                                    {editing ? (
                                      <input type="datetime-local" value={a.dueDate} min={vietnamNowDateTimeLocal()}
                                        onChange={(e) => updateActionItem(a._key, { dueDate: e.target.value })}
                                        className="text-xs font-bold text-orange-700 bg-orange-50 px-2 py-1.5 rounded-md border border-orange-200 outline-none hover:border-orange-300" />
                                    ) : (
                                      <span className="text-xs font-bold text-orange-700">{a.dueDate ? formatDateTime(a.dueDate) : 'Chưa đặt hạn'}</span>
                                    )}
                                  </div>
                                  {editing ? (
                                    <select value={a.status} onChange={(e) => updateActionItem(a._key, { status: e.target.value })}
                                      className="text-xs font-bold rounded-md border border-gray-300 px-2 py-1.5 outline-none focus:border-[#004c91] bg-white">
                                      {ACTION_STATUS_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
                                    </select>
                                  ) : (
                                    <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold border ${meta.cls}`}>{meta.label}</span>
                                  )}
                                  {editing && (
                                    <button type="button" onClick={() => removeActionItem(a._key)}
                                      disabled={a.actionItemId > 0 && a.status === 'DONE'}
                                      title={a.actionItemId > 0 && a.status === 'DONE' ? 'Không thể xóa đầu mục đã hoàn thành' : 'Xóa'}
                                      className="text-gray-400 hover:text-red-600 p-1.5 rounded-lg hover:bg-red-50 transition-colors disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:bg-transparent">
                                      <Trash2 className="w-4 h-4" />
                                    </button>
                                  )}
                                </div>
                                {editing ? (
                                  <input value={a.note} onChange={(e) => updateActionItem(a._key, { note: e.target.value })}
                                    placeholder="Ghi chú (tùy chọn)..."
                                    className="w-full text-xs rounded-lg border border-gray-200 px-3 py-1.5 outline-none focus:border-[#004c91] text-gray-600" />
                                ) : a.note ? (
                                  <p className="text-xs text-gray-500 italic">{a.note}</p>
                                ) : null}
                              </div>
                            );
                          })}
                          {editing && (
                            <button type="button"
                              onClick={() => setDraftActionItems((prev) => [...prev, { _key: nextKey('a'), actionItemId: 0, title: '', note: '', assignedToUserId: null, assignedToUserName: null, dueDate: '', status: 'TODO' }])}
                              className="text-sm font-bold text-[#004c91] hover:text-[#003366] flex items-center gap-1.5 px-2 py-1 outline-none">
                              <Plus className="w-4 h-4" /> Thêm đầu mục công việc
                            </button>
                          )}
                        </div>
                      )}
                    </div>
                  </div>

                  {/* Nút lưu/hủy/sửa — logic backend thật + cơ chế lock */}
                  <div className="mt-8 flex items-center justify-end gap-3">
                    {editing ? (
                      <>
                        <button type="button" onClick={handleCancel} disabled={busy}
                          className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                          <X className="w-4 h-4" /> Hủy chỉnh sửa
                        </button>
                        {/* Không disable theo title rỗng — để click vẫn chạy validate (inline error + toast). */}
                        <button type="button" onClick={handleSave} disabled={busy}
                          className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] shadow-sm transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                          <Save className="w-4 h-4" /> {busy ? 'Đang lưu...' : 'Lưu biên bản'}
                        </button>
                      </>
                    ) : (data.canEdit && !data.isLockedByOther && !isReadOnly) ? (
                      <button type="button" onClick={handleEdit} disabled={busy}
                        className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#f37021] hover:bg-[#e0611d] shadow-sm transition-colors inline-flex items-center gap-2 disabled:opacity-50">
                        <Edit3 className="w-4 h-4" /> {busy ? 'Đang mở...' : 'Sửa biên bản'}
                      </button>
                    ) : null}
                  </div>
                </>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Toasts (góc phải trên) — cùng pattern với màn duyệt/từ chối/hủy đơn. */}
      {toasts.length > 0 && (
        <div className="fixed top-5 right-5 z-[200] flex flex-col gap-2 w-[min(92vw,360px)]">
          {toasts.map((t) => (
            <motion.div
              key={t.id}
              initial={{ opacity: 0, x: 24 }}
              animate={{ opacity: 1, x: 0 }}
              role="status"
              className={`flex items-start gap-2 rounded-xl border px-4 py-3 text-sm font-semibold shadow-lg ${
                t.type === 'success'
                  ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
                  : t.type === 'info'
                  ? 'bg-blue-50 border-blue-200 text-blue-800'
                  : 'bg-red-50 border-red-200 text-red-700'
              }`}
            >
              {t.type === 'success' ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0" />
                : t.type === 'info' ? <Info className="mt-0.5 h-4 w-4 shrink-0" />
                : <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />}
              <span className="flex-1">{t.msg}</span>
              <button type="button" aria-label="Đóng" onClick={() => setToasts((prev) => prev.filter((x) => x.id !== t.id))} className="text-current/70 hover:text-current">
                <X className="h-4 w-4" />
              </button>
            </motion.div>
          ))}
        </div>
      )}
    </div>
  );
}
