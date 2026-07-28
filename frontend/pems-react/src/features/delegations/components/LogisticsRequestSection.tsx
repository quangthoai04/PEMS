/**
 * VisitProcess "Chuẩn bị chi tiết" — real Host → Department logistics requests.
 *
 * Persistence model (no data loss on F5): the UI state is DERIVED from the persisted
 * visit_logistics_items list (loaded via getInstanceLogistics). For each fixed category (Welcome LED,
 * Xe điện, Người lái, Phòng họp, Teabreak) there is at most one ACTIVE item (status ≠ CANCELLED):
 *   - active item exists  → show a read-only summary + "Hủy yêu cầu" (soft-cancel → status CANCELLED),
 *   - no active item      → show the create form ("Gửi yêu cầu" / "Lưu (đã trao đổi bên ngoài)").
 * So a reload always reflects the DB; nothing lives only in local state. "Mục 4: Khác" stays a
 * dynamic create-list (multiple OTHER items), shown in the request list below.
 *
 * Welcome LED 3 choices (Part D): none / system request (REQUESTED + email) / offline coordinated
 * (coordinationMode OFFLINE_COORDINATED, status DONE, no email, required note). All wired to the real
 * API — no mock, no hard-coded department/leader.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'motion/react';
import {
  Loader2, Send, Eye, Plus, Trash2, ChevronUp, ChevronDown, CheckCircle, CheckCircle2, AlertCircle, X,
  MonitorPlay, MapPin, Building2, MoreHorizontal, Car, UserCheck, Coffee, History, Mail, FileText, PenLine, Clock, Download,
} from 'lucide-react';
import { delegationsApi } from '../api/delegationsApi';
import { EmailPreviewModal, type EmailPreviewRecipient, type EmailPreviewSendPayload } from './EmailPreviewModal';
import { stripLegacyActionHtml } from '../../emails/utils/actionLinks';
import { SentEmailsModal } from './SentEmailsModal';
import { SearchDropdown } from './ParticipantInvitationSection';
import {
  LOGISTICS_STATUS_META,
  type LogisticsItemType,
  type LogisticsCoordinationMode,
  type PrepareVisitLogisticsPayload,
  type SupportDepartment,
  type VisitInstanceLogisticsItem,
  type LogisticsHandover,
  type LogisticsHandoverType,
} from '../types/delegations.types';
import { isVehicleHandover, buildDefaultVehicleChecklist, buildDefaultGenericChecklist, type VehicleChecklistRow } from '../../department-reception-tasks/constants/vehicleHandover';
import { LogisticsExpensePanel } from '../../../pages/dashboard/departments/LogisticsExpensePanel';
import { toVietnamDateTimeLocalInput, vietnamNowDateTimeLocal } from '../../../shared/utils/vietnamTime';

type ToastFn = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

interface Props {
  visitInstanceId: number;
  relation: string;            // HOST | STAFF_LEADER | HO | ...
  instanceStatus: string;      // ASSIGNED | BEFORE_VISIT | ...
  delegationName: string;
  campusName: string;
  hostName: string;
  /** Planned window of THIS campus instance ("yyyy-MM-ddTHH:mm[:ss][+07:00]" wall-clock) — the
   * DEFAULT usage start/end of every new system logistics form (host-editable, never locked). */
  plannedStartAt?: string | null;
  plannedEndAt?: string | null;
  pushToast: ToastFn;
}

const ITEM_TYPE_LABEL: Record<LogisticsItemType, string> = {
  ROOM: 'Phòng / Hội trường', TRANSPORT: 'Xe / Di chuyển', MEAL: 'Suất ăn / Tea break',
  EQUIPMENT: 'Thiết bị', BANNER: 'Banner / Standee', LED: 'Màn hình LED', OTHER: 'Khác',
};
const COORD_LABEL: Record<LogisticsCoordinationMode, string> = {
  SYSTEM_REQUEST: 'Xử lý qua hệ thống',
  OFFLINE_COORDINATED: 'Trao đổi bên ngoài',
};
// Statuses where the department has taken the item → host can no longer cancel/replace it.
const LOCKED_STATUSES = new Set(['ASSIGNED', 'ACCEPTED', 'IN_PROGRESS']);
// Statuses whose decision/decline reason should surface as the closing reason.
const REASON_STATUSES = new Set(['REJECTED', 'CANCELLED', 'DECLINED']);
const HANDOVER_STATUSES = new Set(['ACCEPTED', 'IN_PROGRESS', 'DONE']);

const findHandover = (hs: LogisticsHandover[] | undefined, t: LogisticsHandoverType) =>
  hs?.find((h) => h.handoverType === t) ?? null;

const fullySigned = (h: LogisticsHandover | null) =>
  !!(h && h.borrowerSignedAt && h.providerSignedAt);

function HandoverStatusBadge({ borrow }: { borrow: LogisticsHandover | null }) {
  if (fullySigned(borrow)) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 border border-emerald-200 px-2 py-0.5 text-[11px] font-bold text-emerald-700">
        <CheckCircle2 className="w-3 h-3" /> Đã ký nhận đủ 2 bên
      </span>
    );
  }
  if (borrow?.providerSignedAt && !borrow?.borrowerSignedAt) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-blue-50 border border-blue-200 px-2 py-0.5 text-[11px] font-bold text-blue-700">
        <PenLine className="w-3 h-3" /> Chờ người phụ trách ký nhận
      </span>
    );
  }
  if (!borrow?.providerSignedAt) {
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 border border-amber-200 px-2 py-0.5 text-[11px] font-bold text-amber-700">
        <Clock className="w-3 h-3" /> Chờ phòng ban ký bàn giao
      </span>
    );
  }
  return null;
}

function SignChip({ label, at, name }: { label: string; at?: string | null; name?: string | null }) {
  const signed = !!at;
  return (
    <div className={`inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] font-semibold ${
      signed
        ? 'border-emerald-200 bg-emerald-50 text-emerald-700'
        : 'border-slate-200 bg-white text-slate-400'
    }`}>
      {signed
        ? <CheckCircle2 className="w-3 h-3 text-emerald-500 shrink-0" />
        : <Clock className="w-3 h-3 text-slate-300 shrink-0" />}
      <span>{label}: </span>
      {signed
        ? <span className="font-bold">{name || 'Đã ký'} • {fmtDateTime(at)}</span>
        : <span className="italic">Chưa ký</span>}
    </div>
  );
}

type ResourceForm = {
  quantity: string;
  usageStartAt: string;  // datetime-local "YYYY-MM-DDTHH:mm"
  usageEndAt: string;
  note: string;
  departmentId: string;
  title: string;         // editable only for "Khác"
};
/** Fresh create-form. `defaultStart`/`defaultEnd` are the campus-instance planned times (already in
 * datetime-local form, '' when unknown) — a DEFAULT the host can edit, never a lock. */
const emptyForm = (title = '', defaultStart = '', defaultEnd = ''): ResourceForm =>
  ({ quantity: '', usageStartAt: defaultStart, usageEndAt: defaultEnd, note: '', departmentId: '', title });

// datetime-local string of Vietnam "now" (minute precision) for past-date guards —
// fixed Asia/Ho_Chi_Minh, independent of the browser timezone.
const nowLocalMinute = (): string => vietnamNowDateTimeLocal();

function apiError(e: any, fallback: string): string {
  const data = e?.response?.data;
  if (!data) return fallback;
  if (typeof data === 'string' && data.trim()) return data;
  if (data.message) return data.message;
  if (data.errors) {
    const flat = Array.isArray(data.errors) ? data.errors : Object.values(data.errors).flat();
    const first = (flat as any[]).find((x) => typeof x === 'string' && x.trim());
    if (first) return first;
  }
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

export function LogisticsRequestSection({
  visitInstanceId, relation, instanceStatus, delegationName, campusName, hostName,
  plannedStartAt, plannedEndAt, pushToast,
}: Props) {
  const canManage = relation === 'HOST' && (instanceStatus === 'ASSIGNED' || instanceStatus === 'BEFORE_VISIT');

  // Default usage window for NEW system forms = the planned window of THIS campus instance,
  // hydrated via the Vietnam datetime-local helper (handles "+07:00" offsets, never shifts the
  // wall-clock, '' fallback when planned time is missing/invalid — no invented time).
  const defaultUsageStart = toVietnamDateTimeLocalInput(plannedStartAt);
  const defaultUsageEnd = toVietnamDateTimeLocalInput(plannedEndAt);

  const [departments, setDepartments] = useState<SupportDepartment[]>([]);
  const [items, setItems] = useState<VisitInstanceLogisticsItem[]>([]);
  const [loadedOnce, setLoadedOnce] = useState(false);
  const [loadingList, setLoadingList] = useState(false);
  const [busyKey, setBusyKey] = useState<string | null>(null);

  const [openSection, setOpenSection] = useState<Record<number, boolean>>({ 1: true, 2: true, 3: true, 4: true });
  const toggleSection = (n: number) => setOpenSection((p) => ({ ...p, [n]: !p[n] }));

  // Mục 4 "Khác": dynamic list of independent create-cards (Part F).
  const otherIdRef = useRef(1);
  const [otherIds, setOtherIds] = useState<number[]>([1]);
  const [firstOtherMode, setFirstOtherMode] = useState<'none' | 'system' | 'offline'>('none');

  const [preview, setPreview] = useState({
    open: false, loading: false, sending: false, restoring: false, error: null as string | null,
    subject: '', body: '', isActionTemplate: false,
    systemActionDescription: null as string | null, lockedActionBlockHtml: null as string | null,
    recipient: null as EmailPreviewRecipient | null,
  });
  const previewPayload = useRef<PrepareVisitLogisticsPayload | null>(null);
  const previewCtx = useRef<{ leaderName: string } | null>(null);
  const previewResetRef = useRef<(() => void) | null>(null);

  // Cancel-reason modal — a cancellation must record a reason (decision_note).
  const [cancelModal, setCancelModal] = useState<{
    open: boolean; key: string; item: VisitInstanceLogisticsItem | null; reason: string; busy: boolean; err: string | null;
  }>({ open: false, key: '', item: null, reason: '', busy: false, err: null });

  // "Xem mail đã gửi" history modal — bound to one logistics item at a time.
  const [sentModal, setSentModal] = useState<{ open: boolean; item: VisitInstanceLogisticsItem | null }>(
    { open: false, item: null });
  const openSentEmails = (item: VisitInstanceLogisticsItem) => setSentModal({ open: true, item });
  const closeSentEmails = () => setSentModal((s) => ({ ...s, open: false }));

  // Asset handover signing modal state (BORROW phase in Before Visit tab).
  const [signTarget, setSignTarget] = useState<{ item: VisitInstanceLogisticsItem; type: LogisticsHandoverType } | null>(null);
  const [signBusy, setSignBusy] = useState(false);
  const [signNote, setSignNote] = useState('');
  const [signErr, setSignErr] = useState<string | null>(null);
  const [checklistDraft, setChecklistDraft] = useState<VehicleChecklistRow[]>([]);

  const canSignItem = useCallback((item: VisitInstanceLogisticsItem): boolean => {
    if (!canManage) return false;
    const borrow = findHandover(item.handovers, 'BORROW');
    return !!(borrow?.providerSignedAt) && !borrow?.borrowerSignedAt;
  }, [canManage]);

  const openSignModal = (item: VisitInstanceLogisticsItem) => {
    setSignTarget({ item, type: 'BORROW' });
    setSignNote('');
    setSignErr(null);
    const borrow = findHandover(item.handovers, 'BORROW');
    const rows = (() => {
      if (borrow?.checklistJson) {
        try {
          const parsed = JSON.parse(borrow.checklistJson);
          if (Array.isArray(parsed) && parsed.length > 0) return parsed;
        } catch { }
      }
      return isVehicleHandover(item.itemType)
        ? buildDefaultVehicleChecklist()
        : buildDefaultGenericChecklist(item.title, finalQuantity(item));
    })();
    setChecklistDraft(rows);
  };
  const closeSignModal = () => { if (!signBusy) { setSignTarget(null); setSignErr(null); } };

  const submitHandover = async () => {
    if (!signTarget || !visitInstanceId) return;
    setSignBusy(true);
    try {
      const res = await delegationsApi.signLogisticsHandoverBorrower(
        visitInstanceId, signTarget.item.logisticsItemId,
        { handoverType: 'BORROW', note: signNote.trim() || null }
      );
      pushToast('success', res.message || 'Đã ký biên bản.');
      setSignTarget(null);
      setSignNote('');
      await loadList();
    } catch (e: any) {
      const m = apiError(e, 'Không thể ký biên bản. Vui lòng thử lại.');
      setSignErr(m);
      pushToast('error', m);
    } finally {
      setSignBusy(false);
    }
  };

  const loadList = useCallback(async () => {
    setLoadingList(true);
    try {
      const res = await delegationsApi.getInstanceLogistics(visitInstanceId);
      setItems(res.items || []);
    } catch {
      setItems([]);
    } finally {
      setLoadingList(false);
      setLoadedOnce(true);
    }
  }, [visitInstanceId]);

  useEffect(() => { void loadList(); }, [loadList]);

  useEffect(() => {
    if (!canManage) return;
    let alive = true;
    (async () => {
      try {
        const list = await delegationsApi.getSupportDepartments(visitInstanceId);
        if (alive) setDepartments(Array.isArray(list) ? list : []);
      } catch {
        if (alive) setDepartments([]);
      }
    })();
    return () => { alive = false; };
  }, [visitInstanceId, canManage]);

  // The single ACTIVE item for a fixed category, matched by itemType + canonical title. "Active" =
  // not in a closed state: CANCELLED/REJECTED/DECLINED items stay visible (read-only) in the request
  // list below but must NOT lock the create form, so the host can re-request after a rejection. This
  // mirrors the server-side duplicate guard in PrepareVisitLogisticsCommandHandler.
  // items come newest-first from the backend, so .find() returns the most recent one.
  const isActive = (i: VisitInstanceLogisticsItem): boolean =>
    i.status !== 'CANCELLED' && i.status !== 'REJECTED' && i.status !== 'DECLINED';
  const activeItem = (itemType: LogisticsItemType, title: string): VisitInstanceLogisticsItem | null =>
    items.find((i) => i.itemType === itemType && i.title === title && isActive(i)) ?? null;

  const ctxFor = (payload: PrepareVisitLogisticsPayload, dept: SupportDepartment | null) => ({
    visitName: delegationName,
    campusName: campusName,
    hostName: hostName,
    requesterName: hostName,
    departmentName: dept?.departmentName || '',
    departmentHeadName: dept?.leaderName || '',
    departmentLeaderName: dept?.leaderName || '',
    departmentHeadEmail: dept?.leaderEmail || '',
    logisticsTitle: payload.title,
    logisticsItemTitle: payload.title,
    logisticsItemType: ITEM_TYPE_LABEL[payload.itemType] ?? payload.itemType,
    itemType: ITEM_TYPE_LABEL[payload.itemType] ?? payload.itemType,
    logisticsDescription: payload.description || '',
    quantity: payload.quantity != null ? String(payload.quantity) : '',
    usageStartAt: fmtDateTime(payload.usageStartAt) || '',
    usageEndAt: fmtDateTime(payload.usageEndAt) || '',
    coordinationNote: payload.offlineCoordinationNote || '',
  });

  const submitRequest = async (key: string, payload: PrepareVisitLogisticsPayload): Promise<boolean> => {
    setBusyKey(key);
    try {
      const res = await delegationsApi.prepareVisitLogistics(payload);
      const offline = payload.coordinationMode === 'OFFLINE_COORDINATED';
      pushToast(res.emailStatus === 'FAILED' ? 'warning' : 'success',
        res.message || (offline ? 'Đã lưu yêu cầu (đã trao đổi bên ngoài).' : 'Đã gửi yêu cầu hậu cần.'));
      await loadList();
      return true;
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể gửi yêu cầu hậu cần.'));
      return false;
    } finally {
      setBusyKey(null);
    }
  };

  // Host accepts/rejects a Department's change proposal (status CHANGE_PROPOSED).
  const respondToProposal = async (item: VisitInstanceLogisticsItem, accepted: boolean, note: string) => {
    const key = `proposal-${item.logisticsItemId}`;
    setBusyKey(key);
    try {
      const res = await delegationsApi.confirmChangeProposal(item.logisticsItemId, accepted, note || null);
      pushToast('success', res.message || (accepted ? 'Đã chấp nhận đề xuất thay đổi.' : 'Đã từ chối đề xuất thay đổi.'));
      await loadList();
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể phản hồi đề xuất. Vui lòng thử lại.'));
    } finally {
      setBusyKey(null);
    }
  };

  // Cancelling requires a reason (saved to decision_note) → open the modal; the API call runs on confirm.
  const cancelItem = (key: string, item: VisitInstanceLogisticsItem) => {
    setCancelModal({ open: true, key, item, reason: '', busy: false, err: null });
  };

  const confirmCancel = async () => {
    const { key, item, reason } = cancelModal;
    if (!item) return;
    if (!reason.trim()) { setCancelModal((m) => ({ ...m, err: 'Vui lòng nhập lý do hủy yêu cầu logistics.' })); return; }
    setCancelModal((m) => ({ ...m, busy: true, err: null }));
    setBusyKey(key);
    try {
      const res = await delegationsApi.cancelLogisticsItem(visitInstanceId, item.logisticsItemId, reason.trim());
      pushToast('success', res.message || 'Đã hủy yêu cầu hậu cần.');
      setCancelModal({ open: false, key: '', item: null, reason: '', busy: false, err: null });
      await loadList();
    } catch (e: any) {
      const m = apiError(e, 'Không thể hủy yêu cầu hậu cần.');
      setCancelModal((cm) => ({ ...cm, busy: false, err: m }));
      pushToast('error', m);
    } finally {
      setBusyKey(null);
    }
  };

  const openPreview = async (payload: PrepareVisitLogisticsPayload, onReset: () => void) => {
    const dept = departments.find((d) => String(d.departmentId) === String(payload.departmentId));
    previewPayload.current = payload;
    previewCtx.current = { leaderName: dept?.leaderName ?? 'Trưởng phòng' };
    previewResetRef.current = onReset;
    setPreview((p) => ({
      ...p, open: true, loading: true, error: null,
      recipient: {
        name: dept?.leaderName ?? null,
        email: dept?.leaderEmail ?? null,
        roleLabel: 'Trưởng phòng',
        departmentName: dept?.departmentName ?? null,
        campusName,
      },
    }));
    await fetchPreview(payload, dept || null);
  };

  const fetchPreview = async (payload: PrepareVisitLogisticsPayload, dept: SupportDepartment | null): Promise<boolean> => {
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode: 'LOGISTICS_REQUEST_TO_DEPARTMENT',
        context: ctxFor(payload, dept),
      });
      setPreview((p) => ({
        ...p, open: true, loading: false, restoring: false, error: null,
        subject: res.subject, body: stripLegacyActionHtml(res.bodyHtml), // editable HTML, legacy action links stripped
        isActionTemplate: res.isActionTemplate,
        systemActionDescription: res.systemActionDescription ?? null,
        lockedActionBlockHtml: res.lockedActionBlockHtml ?? null,
      }));
      return true;
    } catch (e: any) {
      setPreview((p) => ({ ...p, open: true, loading: false, restoring: false, error: apiError(e, 'Không thể tải bản xem trước email.') }));
      return false;
    }
  };

  // "Khôi phục mẫu gốc": re-fetch the original template from the DB and reset subject/body — without
  // closing the modal or losing the bound recipient/context. Clear toast on success/failure.
  const restorePreview = async () => {
    const pl = previewPayload.current; const ctx = previewCtx.current;
    if (!pl || !ctx) return;
    setPreview((p) => ({ ...p, restoring: true, error: null }));
    const dept = departments.find((d) => String(d.departmentId) === String(pl.departmentId)) || null;
    const ok = await fetchPreview(pl, dept);
    pushToast(ok ? 'success' : 'error',
      ok ? 'Đã khôi phục nội dung email theo mẫu gốc.' : 'Không thể khôi phục mẫu gốc. Vui lòng thử lại.');
  };
  const closePreview = () => setPreview((p) => ({ ...p, open: false }));

  const sendWithEditedContent = async (payload: EmailPreviewSendPayload) => {
    const pl = previewPayload.current;
    if (!pl) return;
    if (!payload.subject.trim()) { pushToast('error', 'Tiêu đề email không được để trống.'); return; }
    if (!payload.bodyHtml.trim()) { pushToast('error', 'Nội dung email không được để trống.'); return; }
    setPreview((p) => ({ ...p, sending: true }));
    try {
      const res = await delegationsApi.prepareVisitLogistics({
        ...pl,
        emailOverride: { useEditedContent: true, subject: payload.subject.trim(), bodyHtml: payload.bodyHtml, attachments: payload.attachments },
      });
      setPreview((p) => ({ ...p, open: false, sending: false }));
      pushToast(res.emailStatus === 'FAILED' ? 'warning' : 'success', res.message || 'Đã gửi yêu cầu hậu cần.');
      previewResetRef.current?.();
      await loadList();
    } catch (e: any) {
      setPreview((p) => ({ ...p, sending: false }));
      pushToast('error', apiError(e, 'Không thể gửi yêu cầu hậu cần.'));
    }
  };

  const shared = {
    visitInstanceId, departments, canManage, busyKey, loadedOnce,
    defaultUsageStart, defaultUsageEnd,
    onSubmit: submitRequest, onPreview: openPreview, onCancel: cancelItem, pushToast,
  };

  return (
    <div className="space-y-0">
      {/* Mục 1: Welcome LED — 3 lựa chọn (none / hệ thống / trao đổi ngoài), state suy ra từ item đã lưu */}
      <MucCard title="Mục 1: Welcome LED" icon={<MonitorPlay className="w-5 h-5 text-[#f37021]" />}
        open={openSection[1]} onToggle={() => toggleSection(1)}>
        <CategoryCard {...shared} cardKey="led" icon={<MonitorPlay className="w-5 h-5 text-[#f37021]" />}
          label="Welcome LED" itemType="LED" qtyLabel="Số lượng màn" activeItem={activeItem('LED', 'Welcome LED')}
          notePlaceholder="Kích thước, nội dung hiển thị, đã gửi ảnh thiết kế..." />
      </MucCard>

      {/* Mục 2: Chuẩn bị cho Campus Tour */}
      <MucCard title="Mục 2: Chuẩn bị cho Campus Tour" icon={<MapPin className="w-5 h-5 text-[#f37021]" />}
        open={openSection[2]} onToggle={() => toggleSection(2)}>
        <div className="space-y-8">
          <CategoryCard {...shared} cardKey="electricCar" icon={<Car className="w-5 h-5 text-[#f37021]" />}
            label="Xe điện" itemType="TRANSPORT" qtyLabel="Số lượng cần mượn" activeItem={activeItem('TRANSPORT', 'Xe điện')}
            notePlaceholder="Ghi chú thêm..." />
          <hr className="border-t-[2px] border-gray-200" />
          <CategoryCard {...shared} cardKey="driver" icon={<UserCheck className="w-5 h-5 text-[#f37021]" />}
            label="Người lái" itemType="TRANSPORT" qtyLabel="Số lượng" activeItem={activeItem('TRANSPORT', 'Người lái')}
            notePlaceholder="Yêu cầu về tài xế, thời gian hỗ trợ..." />
        </div>
      </MucCard>

      {/* Mục 3: Chuẩn bị cho họp */}
      <MucCard title="Mục 3: Chuẩn bị cho họp" icon={<Building2 className="w-5 h-5 text-[#f37021]" />}
        open={openSection[3]} onToggle={() => toggleSection(3)}>
        <div className="space-y-8">
          <CategoryCard {...shared} cardKey="room" icon={<Building2 className="w-5 h-5 text-[#f37021]" />}
            label="Phòng họp" itemType="ROOM" qtyLabel="Số phòng" activeItem={activeItem('ROOM', 'Phòng họp')}
            notePlaceholder="Tên phòng / vị trí (VD: Tòa Alpha, P.101), layout, thiết bị..." />
          <hr className="border-t-[2px] border-gray-200" />
          <CategoryCard {...shared} cardKey="teabreak" icon={<Coffee className="w-5 h-5 text-[#f37021]" />}
            label="Teabreak" itemType="MEAL" qtyLabel="Số lượng (suất)" activeItem={activeItem('MEAL', 'Teabreak')}
            notePlaceholder="Layout, khăn trải bàn, biển tên, yêu cầu đặc biệt..." />
        </div>
      </MucCard>

      {/* Mục 4: Khác — thêm nhiều yêu cầu (Part F); mỗi dòng chọn gửi qua hệ thống / trao đổi ngoài */}
      <MucCard title="Mục 4: Khác" icon={<MoreHorizontal className="w-5 h-5 text-[#f37021]" />}
        open={openSection[4]} onToggle={() => toggleSection(4)}>
        <div className="space-y-6">
          {otherIds.map((id, idx) => (
            <div key={id} className={idx > 0 ? 'pt-6 border-t border-gray-100' : ''}>
              <OtherCard {...shared} cardKey={`other-${id}`}
                label={`Yêu cầu khác ${otherIds.length > 1 ? `#${idx + 1}` : ''}`.trim()}
                isExtra={idx > 0}
                hideNoneOption={otherIds.length > 1}
                onModeChange={idx === 0 ? setFirstOtherMode : undefined}
                onRemove={otherIds.length > 1 ? () => setOtherIds((p) => p.filter((x) => x !== id)) : undefined} />
            </div>
          ))}
          {canManage && firstOtherMode !== 'none' && (
            <button type="button"
              onClick={() => { otherIdRef.current += 1; setOtherIds((p) => [...p, otherIdRef.current]); }}
              className="inline-flex items-center gap-1.5 rounded-xl border-2 border-dashed border-[#f37021]/40 px-4 py-2 text-sm font-bold text-[#f37021] outline-none hover:bg-orange-50">
              <Plus className="w-4 h-4" /> Thêm yêu cầu khác
            </button>
          )}
        </div>
      </MucCard>

      {/* Danh sách yêu cầu hậu cần thật (Part H: badge coordination + status enum) */}
      <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100">
          <h3 className="text-lg font-bold text-orange-900 flex items-center gap-2">
            <Plus className="w-5 h-5 text-orange-900" />
            Danh sách yêu cầu hậu cần
          </h3>
          {loadingList && <Loader2 className="w-4 h-4 animate-spin text-gray-400" />}
        </div>
        <div className="p-5 space-y-3">
          {!loadedOnce ? (
            <LoadingRow />
          ) : items.length === 0 ? (
            <p className="py-2 text-sm italic text-slate-400">Chưa có yêu cầu hậu cần nào.</p>
          ) : (
            items.map((it) => (
              <LogisticsListRow
                key={it.logisticsItemId}
                it={it}
                delegationName={delegationName}
                canManage={canManage}
                busy={busyKey === `proposal-${it.logisticsItemId}`}
                onRespond={respondToProposal}
                onViewSent={openSentEmails}
                onOpenSign={openSignModal}
                canSignItem={canSignItem}
              />
            ))
          )}
        </div>
      </div>

      <EmailPreviewModal
        open={preview.open}
        loading={preview.loading}
        sending={preview.sending}
        restoring={preview.restoring}
        error={preview.error}
        subject={preview.subject}
        body={preview.body}
        isActionTemplate={preview.isActionTemplate}
        systemActionDescription={preview.systemActionDescription}
        lockedActionBlockHtml={preview.lockedActionBlockHtml}
        recipient={preview.recipient}
        canSend
        sendLabel="Gửi với nội dung này"
        pushToast={pushToast}
        onSubjectChange={(v) => setPreview((p) => ({ ...p, subject: v }))}
        onBodyChange={(v) => setPreview((p) => ({ ...p, body: v }))}
        onClose={closePreview}
        onRestore={restorePreview}
        onSend={sendWithEditedContent}
      />

      {/* "Xem mail đã gửi" history (per logistics request). */}
      <SentEmailsModal
        open={sentModal.open}
        title={sentModal.item?.title ?? ''}
        subtitle={sentModal.item ? (ITEM_TYPE_LABEL[sentModal.item.itemType] ?? sentModal.item.itemType) : null}
        targetKey={sentModal.item?.logisticsItemId ?? null}
        load={() => delegationsApi.getLogisticsSentEmails(visitInstanceId, sentModal.item!.logisticsItemId)}
        onClose={closeSentEmails}
      />

      {/* Cancel-reason modal — a cancellation must record its reason (decision_note). */}
      {cancelModal.open && (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-black/40 p-4"
          onMouseDown={() => { if (!cancelModal.busy) setCancelModal((m) => ({ ...m, open: false })); }}>
          <div className="w-full max-w-md overflow-hidden rounded-2xl bg-white shadow-2xl" onMouseDown={(e) => e.stopPropagation()}>
            <div className="flex items-center justify-between bg-[#004c91] px-5 py-4">
              <h3 className="text-base font-bold text-white">Hủy yêu cầu logistics</h3>
              <button type="button" disabled={cancelModal.busy} onClick={() => setCancelModal((m) => ({ ...m, open: false }))}
                className="rounded-full p-1 text-white/85 outline-none hover:bg-white/10 hover:text-white disabled:opacity-50">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="space-y-3 p-5">
              {cancelModal.item && <p className="text-sm text-gray-600">Hạng mục: <b className="text-gray-800">{cancelModal.item.title}</b></p>}
              <div>
                <label className="mb-1 block text-xs font-bold text-gray-600">Lý do hủy <span className="text-red-500">*</span></label>
                <textarea autoFocus value={cancelModal.reason} maxLength={1000}
                  onChange={(e) => setCancelModal((m) => ({ ...m, reason: e.target.value, err: null }))}
                  placeholder="Nhập lý do hủy yêu cầu logistics..."
                  className="h-[100px] w-full resize-none rounded-lg border border-gray-300 px-3 py-2 text-sm outline-none focus:border-[#004c91]" />
                {cancelModal.err && (
                  <p className="mt-1 flex items-center gap-1 text-xs font-semibold text-red-600">
                    <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {cancelModal.err}
                  </p>
                )}
              </div>
            </div>
            <div className="flex items-center justify-end gap-2 border-t border-gray-100 bg-gray-50 px-5 py-4">
              <button type="button" disabled={cancelModal.busy} onClick={() => setCancelModal((m) => ({ ...m, open: false }))}
                className="rounded-lg border border-gray-200 bg-white px-4 py-2 text-sm font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-50">Đóng</button>
              <button type="button" disabled={cancelModal.busy || !cancelModal.reason.trim()} onClick={confirmCancel}
                className="inline-flex items-center gap-1.5 rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white outline-none hover:bg-red-700 disabled:opacity-50">
                {cancelModal.busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <X className="w-4 h-4" />} Xác nhận hủy
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal biên bản ký mượn/bàn giao (BORROW phase trong Trước tiếp khách) */}
      {signTarget && createPortal(
        <>
          <style type="text/css" media="print">
            {`
              body * {
                visibility: hidden;
              }
              #logistics-handover-modal, #logistics-handover-modal * {
                visibility: visible;
              }
              #logistics-handover-modal {
                position: absolute;
                left: 0;
                top: 0;
                width: 100%;
                margin: 0;
                padding: 0;
              }
              #logistics-handover-modal, #logistics-handover-modal * {
                overflow: visible !important;
                max-height: none !important;
              }
              #logistics-handover-modal > div {
                display: block !important;
              }
            `}
          </style>
          <div id="logistics-handover-modal" className="fixed inset-0 z-[80] flex items-center justify-center p-4 print:static print:inset-auto print:p-0">
            <div className="absolute inset-0 bg-black/60 backdrop-blur-sm print:hidden" onClick={closeSignModal} />
            <div className="relative w-full max-w-4xl max-h-[90vh] flex flex-col rounded-2xl bg-white shadow-2xl overflow-hidden font-sans border border-slate-200 print:max-w-none print:max-h-none print:shadow-none print:border-none print:rounded-none">
            {/* Modal header */}
            <div className="bg-[#004c91] text-white px-6 py-4 flex items-center justify-between shrink-0 print:hidden">
              <h3 className="font-bold text-sm uppercase tracking-wide flex items-center gap-2">
                <FileText className="w-5 h-5 opacity-80" />
                BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU
              </h3>
              <div className="flex items-center gap-4">
                <button 
                  type="button" 
                  onClick={() => window.print()}
                  className="flex items-center gap-1.5 text-xs font-bold bg-white/10 hover:bg-white/20 px-3 py-1.5 rounded-lg transition-colors outline-none"
                >
                  <Download className="w-4 h-4" /> Tải PDF
                </button>
                <button type="button" onClick={closeSignModal} disabled={signBusy}
                  className="text-white/70 hover:text-white outline-none transition-colors" aria-label="Đóng">
                  <X className="w-5 h-5" />
                </button>
              </div>
            </div>

            {/* Modal body (scrollable) */}
            <div className="p-6 md:p-12 overflow-y-auto bg-white flex-1 text-slate-900 shadow-[inset_0_0_20px_rgba(0,0,0,0.02)] print:overflow-visible print:shadow-none">
              {(() => {
                const b = findHandover(signTarget.item.handovers, 'BORROW');
                const r = findHandover(signTarget.item.handovers, 'RETURN');
                const bg1 = b?.providerSignedAt ? `${b.providerSignedByName || 'Phòng ban'}` : null;
                const bg2 = b?.borrowerSignedAt ? `${b.borrowerSignedByName || 'Host'}` : null;
                const nt1 = r?.borrowerSignedAt ? `${r.borrowerSignedByName || 'Host'}` : null;
                const nt2 = r?.providerSignedAt ? `${r.providerSignedByName || 'Phòng ban'}` : null;

                const canSignBG2 = bg1 && !bg2;
                const isBorrowDone = bg1 && bg2;

                // parse date for top section
                let handoverTime = "....";
                let handoverDate = "..../..../20.....";
                if (b?.providerSignedAt) {
                  const [d, t] = b.providerSignedAt.replace(' ', 'T').split('T');
                  if (d && t) {
                    const hm = t.slice(0, 5);
                    const [y, m, day] = d.split('-');
                    handoverTime = `${hm}`;
                    handoverDate = `${day}/${m}/${y}`;
                  }
                }

                // Host and Provider names
                const hostName = b?.borrowerSignedByName || r?.borrowerSignedByName || 'Đại diện Host đón tiếp';
                const providerName = b?.providerSignedByName || r?.providerSignedByName || signTarget.item.departmentName || 'Đại diện Phòng ban';

                const isVehicle = isVehicleHandover(signTarget.item.itemType);

                return (
                  <>
                    <div className="text-center space-y-1 mb-8">
                      <h4 className="text-xl sm:text-2xl font-bold uppercase tracking-wide">
                        BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU
                      </h4>
                      <p className="text-lg font-bold uppercase">
                        TÀI SẢN / TRANG THIẾT BỊ
                      </p>
                    </div>

                    <div className="space-y-3 text-[15px] leading-relaxed mb-6">
                      <p>
                        Hôm nay, lúc: <b>{handoverTime}</b> giờ, ngày <b>{handoverDate}</b>, tại: <b>Trường Đại học FPT Hòa Lạc</b>.
                      </p>
                      <p>Chúng tôi gồm:</p>
                      <div className="space-y-2 pl-4">
                        <div className="flex flex-wrap gap-x-8 gap-y-2">
                          <p className="flex-1 min-w-[250px]">Người bàn giao: <b>{providerName}</b></p>
                          <p className="flex-1 min-w-[200px]">Bộ phận: <b>{signTarget.item.departmentName || '................'}</b></p>
                        </div>
                        <div className="flex flex-wrap gap-x-8 gap-y-2">
                          <p className="flex-1 min-w-[250px]">Người nhận bàn giao: <b>{hostName}</b></p>
                          <p className="flex-1 min-w-[200px]">Bộ phận: <b>Người phụ trách tiếp đón</b></p>
                        </div>
                        {signTarget.item.description && (
                          <p>Mô tả: <b>{signTarget.item.description}</b></p>
                        )}
                        <p>Lý do bàn giao: <b>Phục vụ công tác đón tiếp đoàn khách</b></p>
                        <p>Thời gian hẹn trả tài sản: <b>Sau khi kết thúc chuyến thăm</b></p>
                      </div>
                    </div>

                    <p className="font-bold text-[15px] mb-2">
                      {isVehicle
                        ? `Cùng bàn giao xe ${finalQuantity(signTarget.item) || 1} ô tô điện với tình trạng sau:`
                        : `Cùng bàn giao ${finalQuantity(signTarget.item) || 1} ${signTarget.item.title || 'hạng mục'} với tình trạng sau:`}
                    </p>
                    <div className="overflow-x-auto mb-6">
                      <table className="w-full border-collapse border border-slate-500 text-[14px]">
                        <thead>
                          <tr className="bg-slate-50">
                            <th className="border border-slate-500 p-2 text-center w-12">STT</th>
                            <th className="border border-slate-500 p-2 text-center">Nội dung</th>
                            <th className="border border-slate-500 p-2 text-center w-24">Số Lượng</th>
                            <th className="border border-slate-500 p-2 text-center">{isVehicle ? 'Tình Trạng BTS bàn giao' : 'Tình Trạng bàn giao'}</th>
                            <th className="border border-slate-500 p-2 text-center">Tình trạng nghiệm thu</th>
                          </tr>
                        </thead>
                        <tbody>
                          {checklistDraft.map((row, i) => (
                            <tr key={i}>
                              <td className="border border-slate-500 p-2 text-center">{i + 1}</td>
                              <td className="border border-slate-500 p-2">{row.name}</td>
                              <td className="border border-slate-500 p-2 text-center">{row.qty}</td>
                              <td className="border border-slate-500 p-2 text-center">{row.giao}</td>
                              <td className="border border-slate-500 p-0 bg-blue-50/20">
                                <div className="min-h-[36px] flex items-center px-2 text-slate-600">{row.nhan}</div>
                              </td>
                            </tr>
                          ))}
                          <tr>
                            <td className="border border-slate-500 p-2 text-center font-semibold">{checklistDraft.length + 1}</td>
                            <td className="border border-slate-500 p-2 font-semibold">Ghi chú</td>
                            <td className="border border-slate-500 p-2"></td>
                            <td className="border border-slate-500 p-2 whitespace-pre-line align-top">{b?.conditionNote || ''}</td>
                            <td className="border border-slate-500 p-2 whitespace-pre-line align-top">{r?.conditionNote || ''}</td>
                          </tr>
                        </tbody>
                      </table>
                    </div>

                    <div className="space-y-1 text-[14px] mb-8">
                      <p className="font-bold">{isVehicle ? 'Quy định khi sử dụng xe ô tô điện:' : 'Quy định khi sử dụng tài sản:'}</p>
                      {isVehicle ? (
                        <ul className="list-disc pl-8 space-y-1">
                          <li>Người mượn xe phải tuân thủ đúng mục đích sử dụng, không tự ý chuyển giao xe cho người khác.</li>
                          <li>Khi có vấn đề xảy ra (xe bị hỏng hoặc không nguyên hiện trạng ban đầu), <b>người mượn xe</b> sẽ phải chịu hoàn toàn trách nhiệm chi trả chi phí sửa chữa/đền bù.</li>
                          <li>An toàn trong quá trình sử dụng xe sẽ do <b>người mượn xe</b> chịu hoàn toàn trách nhiệm.</li>
                          <li>Ghi chú khác: ....................................................................................................................</li>
                        </ul>
                      ) : (
                        <ul className="list-disc pl-8 space-y-1">
                          <li>Người mượn tài sản phải tuân thủ đúng mục đích sử dụng, không tự ý chuyển giao cho người khác.</li>
                          <li>Khi có vấn đề xảy ra (bị hỏng hoặc không nguyên hiện trạng ban đầu), <b>người mượn tài sản</b> sẽ phải chịu hoàn toàn trách nhiệm chi trả chi phí sửa chữa/đền bù.</li>
                          <li>An toàn trong quá trình sử dụng tài sản sẽ do <b>người mượn tài sản</b> chịu hoàn toàn trách nhiệm.</li>
                          <li>Ghi chú khác: ....................................................................................................................</li>
                        </ul>
                      )}
                      <p className="mt-4">
                        Tôi là <b>{hostName}</b>, đã đọc hiểu và cam kết thực hiện đúng quy định sử dụng.
                      </p>
                    </div>

                    {/* KHỐI BÀN GIAO */}
                    <div className="space-y-8">
                    <div className="relative my-7">
                      <div className="absolute inset-0 flex items-center" aria-hidden="true"><div className="w-full border-t border-slate-300"></div></div>
                      <div className="relative flex justify-center"><span className="bg-white px-4 py-1.5 text-[10px] font-black text-slate-900 uppercase tracking-widest border border-slate-200 rounded-full shadow-sm">BÀN GIAO</span></div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-slate-50/70 p-4.5 rounded-2xl border border-slate-200">
                      {/* Bên Giao */}
                      <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                        <div>
                          <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Bên Giao</label>
                          <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic whitespace-pre-line">
                            {b?.conditionNote || 'Không có ghi chú.'}
                          </div>
                        </div>
                        <div className={`border-2 rounded-xl p-3 relative ${bg1 ? 'border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                          {bg1 ? (
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                              <div>
                                <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                                <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{bg1}</p>
                                <p className="text-[9px] text-slate-500">{fmtDateTime(b?.providerSignedAt)}</p>
                              </div>
                            </div>
                          ) : (
                            <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                              <div className="flex items-center gap-2">
                                <FileText className="w-4 h-4 text-[#004c91]/80 shrink-0" />
                                <div className="text-left">
                                  <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                  <span className="text-[9px] text-slate-450">Chờ phòng ban ký</span>
                                </div>
                              </div>
                            </div>
                          )}
                        </div>
                      </div>

                      {/* Bên Nhận */}
                      <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                        <div>
                          <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-2">Ghi chú Bên Nhận</label>
                          {!bg2 ? (
                            <textarea rows={2} value={signNote} onChange={e => setSignNote(e.target.value)} disabled={!canSignBG2 || signBusy} className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#f37021] focus:ring-1 focus:ring-orange-200 outline-none resize-none bg-slate-50/30" placeholder={canSignBG2 ? "Nhập ý kiến xác nhận nhận..." : "Chờ phòng ban ký trước..."} />
                          ) : (
                            <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                              {b?.conditionNote || 'Đã xác nhận nhận tài sản.'}
                            </div>
                          )}
                        </div>
                        <div className={`border-2 rounded-xl p-3 relative ${bg2 ? 'border-emerald-500 bg-emerald-50/20' : canSignBG2 ? 'border-dashed border-[#f37021]/50 bg-orange-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                          {bg2 ? (
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                              <div>
                                <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                                <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{bg2}</p>
                                <p className="text-[9px] text-slate-500">{fmtDateTime(b?.borrowerSignedAt)}</p>
                              </div>
                            </div>
                          ) : (
                            <div className="flex flex-row items-center justify-between gap-3 w-full">
                              <div className="flex items-center gap-2">
                                <FileText className={`w-4 h-4 shrink-0 ${canSignBG2 ? 'text-[#f37021]' : 'text-slate-400'}`} />
                                <div className="text-left">
                                  <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                  <span className="text-[9px] text-slate-450">{canSignBG2 ? 'Nhấp để xác nhận' : 'Chờ PB ký trước'}</span>
                                </div>
                              </div>
                              <button type="button" onClick={submitHandover} disabled={!canSignBG2 || signBusy} className="py-2 px-3 bg-orange-50 hover:bg-orange-100 text-[#f37021] font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm print:hidden">
                                {signBusy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />} Ký xác nhận (BG2)
                              </button>
                            </div>
                          )}
                        </div>
                      </div>
                    </div>

                    {/* KHỐI NGHIỆM THU */}
                    <div className={`transition-opacity pt-4 ${!isBorrowDone ? 'opacity-40 pointer-events-none' : ''}`}>
                      <div className="relative my-7">
                        <div className="absolute inset-0 flex items-center" aria-hidden="true"><div className="w-full border-t border-slate-300"></div></div>
                        <div className="relative flex justify-center"><span className="bg-white px-4 py-1.5 text-[10px] font-black text-slate-900 uppercase tracking-widest border border-slate-200 rounded-full shadow-sm">NGHIỆM THU</span></div>
                      </div>

                      <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-[#f8fbfe] p-4.5 rounded-2xl border border-blue-200/50">
                        {/* Bên Giao (Host trả) */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                          <div>
                            <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Nghiệm thu (Bên Giao)</label>
                            <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                              {r?.conditionNote || 'Chưa ký trả.'}
                            </div>
                          </div>
                          <div className={`border-2 rounded-xl p-3 relative ${nt1 ? 'border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                            {nt1 ? (
                              <div className="flex items-center gap-2.5">
                                <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                                <div>
                                  <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                  <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{nt1}</p>
                                  <p className="text-[9px] text-slate-500">{fmtDateTime(r?.borrowerSignedAt)}</p>
                                </div>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                                  <div className="text-left">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                    <span className="text-[9px] text-slate-450">Chưa ký trả</span>
                                  </div>
                                </div>
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Bên Nhận (Phòng ban nhận lại) */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                          <div>
                            <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Nghiệm thu (Bên Nhận)</label>
                            <div className={`w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] italic whitespace-pre-line ${nt2 ? 'text-slate-600' : 'text-slate-400'}`}>
                              {nt2 ? (r?.conditionNote || 'Đã nghiệm thu nhận lại tài sản.') : 'Chờ phòng ban nhận xét tình trạng tài sản và xác nhận...'}
                            </div>
                          </div>
                          <div className={`border-2 rounded-xl p-3 relative ${nt2 ? 'border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                            {nt2 ? (
                              <div className="flex items-center gap-2.5">
                                <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                                <div>
                                  <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                  <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{nt2}</p>
                                  <p className="text-[9px] text-slate-500">{fmtDateTime(r?.providerSignedAt)}</p>
                                </div>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                                  <div className="text-left">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                    <span className="text-[9px] text-slate-450">Chờ PB nghiệm thu lại</span>
                                  </div>
                                </div>
                              </div>
                            )}
                          </div>
                        </div>
                      </div>
                    </div>

                    <LogisticsExpensePanel logisticsItemId={signTarget.item.logisticsItemId} readOnly />
                    </div>
                  </>
                );
              })()}
            </div>

            {/* Footer controls inside modal */}
            <div className="bg-slate-50 px-6 py-4 flex flex-col sm:flex-row justify-between items-center border-t border-slate-200 gap-4 shrink-0 rounded-b-2xl">
              <span className="text-[11px] text-slate-500 font-medium italic">
                {signErr ? <span className="text-red-500 flex items-center gap-1"><AlertCircle className="w-4 h-4"/> {signErr}</span> : 'Vui lòng kiểm tra kỹ hiện trạng trước khi ký xác nhận.'}
              </span>
              <button onClick={closeSignModal} className="px-6 py-2.5 bg-[#004c91] text-white hover:bg-[#003b70] text-[12px] font-bold rounded-xl transition-colors shadow-sm w-full sm:w-auto outline-none">
                Đóng biên bản
              </button>
            </div>
          </div>
        </div>
        </>,
        document.body
      )}
    </div>
  );
}

function LoadingRow() {
  return <div className="flex items-center gap-2 py-3 text-sm text-gray-500"><Loader2 className="w-4 h-4 animate-spin" /> Đang tải...</div>;
}

function MucCard({ title, icon, open, onToggle, children }: {
  title: string; icon: React.ReactNode; open: boolean; onToggle: () => void; children: React.ReactNode;
}) {
  return (
    <div className="bg-white border-b border-gray-100 last:border-b-0">
      <div className="bg-slate-50 px-4 py-2.5 flex items-center justify-between cursor-pointer group" onClick={onToggle}>
        <h3 className="text-sm font-bold text-[#004c91] flex items-center gap-2">
          {icon}
          {title}
        </h3>
        <div className="w-6 h-6 flex items-center justify-center text-gray-400 group-hover:text-gray-600 transition-colors">
          {open ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
        </div>
      </div>
      <AnimatePresence>
        {open && (
          <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }}>
            <div className="p-4 bg-white">{children}</div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

/** Read-only summary of a persisted (active) logistics item + soft-cancel — the "configured" state. */
function ItemSummary({ item, label, departments, canManage, busy, onCancel }: {
  item: VisitInstanceLogisticsItem;
  label: string;
  departments: SupportDepartment[];
  canManage: boolean;
  busy: boolean;
  onCancel: () => void;
}) {
  const meta = LOGISTICS_STATUS_META[item.status] ?? { label: item.status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  const offline = item.coordinationMode === 'OFFLINE_COORDINATED';
  const deptName = item.departmentName
    ?? departments.find((d) => d.departmentId === item.requestedToDepartmentId)?.departmentName;
  const locked = LOCKED_STATUSES.has(item.status);
  return (
    <div className="flex flex-col gap-3 p-5 bg-blue-50/40 border border-blue-200 rounded-xl shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-sm font-bold text-[#004c91] flex items-center gap-1.5">
          <CheckCircle2 className="w-4 h-4 text-[#10b981]" /> {item.title}
        </span>
        <div className="flex items-center gap-1.5">
          <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>{meta.label}</span>
          {item.coordinationMode && (
            <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[10px] font-bold ${offline ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-slate-200 bg-slate-50 text-slate-500'}`}>
              {COORD_LABEL[item.coordinationMode]}
            </span>
          )}
        </div>
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-1 text-sm text-gray-700">
        {item.quantity != null && <div><span className="text-gray-500">Số lượng:</span> <b>{item.quantity}</b></div>}
        {deptName && <div><span className="text-gray-500">Phòng ban:</span> <b>{deptName}</b></div>}
        {(item.usageStartAt || item.usageEndAt) && (
          <div className="sm:col-span-2"><span className="text-gray-500">Thời gian:</span> {fmtDateTime(item.usageStartAt)} – {fmtDateTime(item.usageEndAt)}</div>
        )}
        {(item.description || item.offlineCoordinationNote) && (
          <div className="sm:col-span-2"><span className="text-gray-500">Ghi chú:</span> {item.offlineCoordinationNote || item.description}</div>
        )}
      </div>
      {canManage && (
        <div className="flex items-center justify-end gap-2 pt-1">
          {locked ? (
            <span className="text-[11px] italic text-gray-400">Phòng ban đang xử lý — không thể hủy.</span>
          ) : (
            <button type="button" disabled={busy} onClick={onCancel}
              className="inline-flex items-center gap-1.5 rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-bold text-red-600 outline-none transition-colors hover:bg-red-50 disabled:opacity-50">
              {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <X className="w-3.5 h-3.5" />} Hủy yêu cầu
            </button>
          )}
        </div>
      )}
    </div>
  );
}

interface SharedCardProps {
  visitInstanceId: number;
  departments: SupportDepartment[];
  canManage: boolean;
  busyKey: string | null;
  loadedOnce: boolean;
  /** Campus-instance planned window in datetime-local form ('' when unknown) — the default
   * usageStartAt/usageEndAt of every NEW system form. Editable; existing items keep their own. */
  defaultUsageStart: string;
  defaultUsageEnd: string;
  onSubmit: (key: string, payload: PrepareVisitLogisticsPayload) => Promise<boolean>;
  onPreview: (payload: PrepareVisitLogisticsPayload, onReset: () => void) => void;
  onCancel: (key: string, item: VisitInstanceLogisticsItem) => void;
  pushToast: ToastFn;
}

interface ResourceCardProps extends SharedCardProps {
  cardKey: string;
  icon: React.ReactNode;
  label: string;
  itemType: LogisticsItemType;
  qtyLabel: string;
  notePlaceholder?: string;
  editableTitle?: boolean;
  existingItem: VisitInstanceLogisticsItem | null;
  onRemove?: () => void;
}

/** SYSTEM_REQUEST resource form (datetime range, dept required), or a summary when already saved. */
function ResourceCard({
  cardKey, icon, label, itemType, qtyLabel, notePlaceholder, editableTitle, existingItem, onRemove,
  visitInstanceId, departments, canManage, busyKey, loadedOnce, defaultUsageStart, defaultUsageEnd,
  onSubmit, onPreview, onCancel, pushToast,
}: ResourceCardProps) {
  // A SAVED item always shows its OWN stored usage times (hydrated through the Vietnam
  // datetime-local helper — API values may carry "+07:00" and must not shift); only a NEW form is
  // pre-filled with the campus-instance planned window. Both remain host-editable.
  const formFromItem = (it: VisitInstanceLogisticsItem): ResourceForm => ({
    title: it.title || label,
    quantity: it.quantity?.toString() || '',
    usageStartAt: toVietnamDateTimeLocalInput(it.usageStartAt),
    usageEndAt: toVietnamDateTimeLocalInput(it.usageEndAt),
    note: it.description || '',
    departmentId: it.requestedToDepartmentId?.toString() || '',
  });
  const freshForm = () => emptyForm(editableTitle ? '' : label, defaultUsageStart, defaultUsageEnd);

  const [form, setForm] = useState<ResourceForm>(() => (existingItem ? formFromItem(existingItem) : freshForm()));
  const [err, setErr] = useState<string | null>(null);
  const [localSubmitted, setLocalSubmitted] = useState(false);
  const set = (k: keyof ResourceForm, v: string) => { setForm((f) => ({ ...f, [k]: v })); setErr(null); };
  // Reset of an UNSAVED form returns to the planned-time defaults (not empty strings).
  const reset = () => { setForm(freshForm()); setErr(null); setLocalSubmitted(false); };

  const noteRef = useRef<HTMLTextAreaElement | null>(null);

  useEffect(() => {
    if (noteRef.current) {
      noteRef.current.style.height = 'auto';
      noteRef.current.style.height = `${Math.max(44, noteRef.current.scrollHeight)}px`;
      noteRef.current.scrollTop = 0;
    }
  }, [form.note]);

  // Only a (re)appearing saved item overwrites the form. When there is no saved item we leave the
  // form alone so a re-render/refetch never clobbers values the host already edited.
  useEffect(() => {
    if (existingItem) {
      setForm(formFromItem(existingItem));
      setErr(null);
      setLocalSubmitted(true);
    } else {
      setLocalSubmitted(false);
    }
  }, [existingItem, label]);

  const busy = busyKey === cardKey;
  // If departmentId is set in form, find it. If it was already submitted but the API didn't return the full department object, we fallback to showing the departmentName from the item if available.
  const dept = departments.find((d) => String(d.departmentId) === form.departmentId) || (existingItem && existingItem.departmentName ? { departmentId: Number(form.departmentId), departmentName: existingItem.departmentName, leaderName: '', leaderEmail: '', canInviteParticipant: true, canReceiveLogistics: true } as SupportDepartment : undefined);
  const title = editableTitle ? form.title.trim() : label;
  
  const isSubmitted = !!existingItem || localSubmitted;
  const locked = existingItem ? LOCKED_STATUSES.has(existingItem.status) : false;
  const isFormDisabled = !canManage || isSubmitted;

  // Loading guard so we never flash the empty create form before the saved item arrives.
  if (!loadedOnce) {
    return (
      <div>
        <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">{icon} {label}</h4>
        <LoadingRow />
      </div>
    );
  }

  const validate = (): string | null => {
    if (editableTitle && !title) return 'Vui lòng nhập tiêu đề / nội dung công việc.';
    if (!form.departmentId) return 'Vui lòng chọn phòng ban xử lý.';
    
    const qtyRequiredTypes: LogisticsItemType[] = ['ROOM', 'TRANSPORT', 'MEAL', 'EQUIPMENT'];
    if (qtyRequiredTypes.includes(itemType) && !form.quantity) return 'Vui lòng nhập số lượng.';
    if (form.quantity && (Number.isNaN(Number(form.quantity)) || Number(form.quantity) < 1)) return 'Số lượng phải là số nguyên ≥ 1.';
    
    if (!form.usageStartAt) return 'Vui lòng nhập thời gian bắt đầu sử dụng.';
    if (!form.usageEndAt) return 'Vui lòng nhập thời gian kết thúc sử dụng.';

    const nowStr = nowLocalMinute();
    if (form.usageStartAt < nowStr) return 'Thời gian bắt đầu không được trong quá khứ.';
    if (form.usageEndAt <= form.usageStartAt) return 'Thời gian kết thúc phải sau thời gian bắt đầu.';

    return null;
  };

  const handleQuantityChange = (val: string) => {
    if (val === '') { set('quantity', ''); return; }
    const digits = val.replace(/\D/g, ''); // strip non-digits
    const normalized = digits ? parseInt(digits, 10).toString() : '';
    set('quantity', normalized);
  };

  const buildPayload = (): PrepareVisitLogisticsPayload => ({
    visitInstanceId,
    departmentId: Number(form.departmentId),
    itemType,
    title: title || label,
    description: form.note.trim() || null,
    quantity: form.quantity ? Number(form.quantity) : null,
    usageStartAt: form.usageStartAt || null,
    usageEndAt: form.usageEndAt || null,
    coordinationMode: 'SYSTEM_REQUEST',
  });

  // Inline error stays at the field; a single toast surfaces the first error (no spam).
  const doSend = async () => {
    const v = validate();
    if (v) { setErr(v); pushToast('error', v); return; }
    const payload = buildPayload();
    if (await onSubmit(cardKey, payload)) {
      setLocalSubmitted(true);
    }
  };
  const doPreview = () => {
    const v = validate();
    if (v) { setErr(v); pushToast('error', v); return; }
    onPreview(buildPayload(), reset);
  };

  return (
    <div className="pl-5">
      <h4 className="text-sm font-bold text-[#004c91] mb-3 flex items-center gap-2 flex-wrap">
        {icon} {label}
        {onRemove && canManage && (
          <button type="button" onClick={onRemove} title="Xóa dòng"
            className="ml-auto inline-flex h-8 w-8 items-center justify-center rounded-lg bg-red-50 text-red-500 outline-none hover:bg-red-100">
            <Trash2 className="w-4 h-4" />
          </button>
        )}
      </h4>

      <div className="flex flex-col gap-4 p-5 bg-white border border-gray-200 rounded-xl shadow-sm">
        <div className="flex items-center justify-between pb-3 border-b border-gray-100">
          <span className="text-sm font-bold text-[#004c91] flex items-center gap-1.5">
            <CheckCircle className="w-4 h-4 text-[#004c91]" /> Cấu hình chi tiết
          </span>
        </div>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
          {/* KHỐI TRÁI: Dòng 1 (Số lượng, Bắt đầu, Kết thúc), Dòng 2 (Chọn phòng ban), Dòng 3 (Ưu tiên, Hạn phản hồi) */}
          <div className="lg:col-span-7 space-y-3.5">
            {editableTitle && (
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">Tiêu đề / nội dung công việc <span className="text-red-500">*</span></label>
                <input type="text" maxLength={255} disabled={isFormDisabled} value={form.title} onChange={(e) => set('title', e.target.value)}
                  placeholder="VD: Hỗ trợ kỹ thuật âm thanh"
                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
              </div>
            )}

            {/* Dòng 1: Số lượng (110px), Thời gian bắt đầu (1fr), Thời gian kết thúc (1fr) */}
            <div className="grid grid-cols-1 sm:grid-cols-[110px_1fr_1fr] gap-3">
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">Số lượng <span className="font-normal text-gray-400">(dự kiến)</span></label>
                <input type="text" inputMode="numeric" disabled={isFormDisabled} value={form.quantity} onChange={(e) => handleQuantityChange(e.target.value)}
                  placeholder="VD: 2"
                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian bắt đầu <span className="text-red-500">*</span></label>
                <input type="datetime-local" disabled={isFormDisabled} value={form.usageStartAt} onChange={(e) => set('usageStartAt', e.target.value)}
                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
              </div>
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian kết thúc <span className="text-red-500">*</span></label>
                <input type="datetime-local" disabled={isFormDisabled} value={form.usageEndAt} onChange={(e) => set('usageEndAt', e.target.value)}
                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
              </div>
            </div>

            {/* Dòng 2: Chọn phòng ban xử lý */}
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Chọn phòng ban xử lý <span className="text-red-500">*</span></label>
              {!form.departmentId ? (
                <SearchDropdown<SupportDepartment>
                  placeholder="Tìm phòng ban (GENERAL) cùng cơ sở..."
                  emptyText="Không tìm thấy phòng ban phù hợp."
                  search={(kw) => delegationsApi.getSupportDepartments(visitInstanceId, kw)}
                  disabled={isFormDisabled}
                  renderRow={(d, _i, close) => (
                    <div
                      key={d.departmentId}
                      onClick={() => {
                        if (!d.canReceiveLogistics || isFormDisabled) return;
                        set('departmentId', d.departmentId.toString());
                        close();
                      }}
                      className={`flex items-center justify-between gap-3 border-b border-gray-100 px-4 py-2.5 last:border-b-0 transition-colors ${d.canReceiveLogistics && !isFormDisabled ? 'cursor-pointer hover:bg-[#f0f7ff]' : 'opacity-60 cursor-not-allowed'}`}
                    >
                      <div className="min-w-0">
                        <div className="truncate text-sm font-bold text-gray-800">{d.departmentName}</div>
                        <div className="truncate text-xs text-gray-500">
                          {d.leaderName ? `Trưởng phòng: ${d.leaderName}` : 'Chưa có trưởng phòng đang hoạt động'}
                        </div>
                        {!d.canReceiveLogistics && d.logisticsDisabledReason && (
                          <div className="mt-0.5 text-[11px] font-medium text-amber-600">{d.logisticsDisabledReason}</div>
                        )}
                      </div>
                      {d.canReceiveLogistics && (
                        <button type="button" className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-[#004c91] px-2 py-1 text-xs font-bold text-[#004c91]">
                          Chọn
                        </button>
                      )}
                    </div>
                  )}
                />
              ) : null}

              {form.departmentId && dept && (
                <div className="mt-2 p-3 bg-white border border-gray-200 rounded-xl flex items-center justify-between gap-3 shadow-sm">
                  <div className="min-w-0">
                    <div className="flex items-center gap-2">
                      <div className="truncate text-sm font-bold text-gray-800">{dept.departmentName}</div>
                      {!isSubmitted && (
                        <button type="button" onClick={() => set('departmentId', '')} className="text-xs text-[#004c91] hover:underline font-semibold">
                          Đổi
                        </button>
                      )}
                    </div>
                    <div className="flex items-center gap-2 text-xs text-gray-600 mt-0.5">
                      <span>Trưởng phòng: <span className="font-semibold">{dept.leaderName || 'Chưa có'}</span></span>
                    </div>
                  </div>
                  {canManage && !isSubmitted && (
                    <div className="flex items-center gap-2 shrink-0">
                      {dept.canReceiveLogistics && (
                        <button type="button" disabled={busy} onClick={doPreview}
                          className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200 bg-white text-[#004c91] outline-none transition-colors hover:bg-gray-50 disabled:opacity-50"
                          title="Xem trước & sửa email">
                          <Eye className="w-3.5 h-3.5" />
                        </button>
                      )}
                      <button type="button" disabled={!dept.canReceiveLogistics || busy} onClick={doSend}
                        className="inline-flex items-center gap-1 rounded-lg bg-[#004c91] px-3.5 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-40">
                        {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                        Gửi yêu cầu
                      </button>
                    </div>
                  )}
                  {canManage && isSubmitted && existingItem && (
                    <div className="flex items-center gap-2 shrink-0">
                      <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-600">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi yêu cầu
                      </span>
                      {!locked && (
                        <button type="button" disabled={busy} onClick={() => onCancel(cardKey, existingItem)}
                          className="inline-flex items-center gap-1 rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-bold text-red-600 outline-none transition-colors hover:bg-red-50 disabled:opacity-50">
                          {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <X className="w-3.5 h-3.5" />} Hủy
                        </button>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>

          {/* KHỐI PHẢI: Mô tả chi tiết (Tự động co giãn theo độ dài chữ) */}
          <div className="lg:col-span-5 flex flex-col space-y-2">
            <label className="block text-xs font-bold text-gray-600">Mô tả chi tiết</label>
            <textarea
              ref={noteRef}
              disabled={isFormDisabled}
              rows={1}
              value={form.note}
              onChange={(e) => {
                set('note', e.target.value);
                e.target.style.height = 'auto';
                e.target.style.height = `${Math.max(44, e.target.scrollHeight)}px`;
                e.target.scrollTop = 0;
              }}
              placeholder={notePlaceholder ?? 'Nhập mô tả chi tiết yêu cầu cần chuẩn bị...'}
              className="w-full px-3 py-2.5 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm leading-relaxed resize-none min-h-[44px] overflow-hidden disabled:bg-gray-50 disabled:text-gray-400"
            />
            <p className="text-[11px] text-gray-400">Mô tả rõ yêu cầu, cách setup, lưu ý đặc biệt hoặc nội dung cần phòng ban xử lý.</p>
          </div>
        </div>

        {err && (
          <p className="flex items-center gap-1.5 text-xs font-semibold text-red-600">
            <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {err}
          </p>
        )}
      </div>
    </div>
  );
}

interface OfflineCardProps extends SharedCardProps {
  cardKey: string;
  itemType: LogisticsItemType;
  label: string;
  /** Canonical DB title for the category (e.g. 'Welcome LED'). Defaults to `label`. Kept distinct so
   * the persisted title matches the system-request item of the same category — both the FE active-item
   * lookup and the server-side duplicate guard key on (itemType, title). */
  title?: string;
  /** "Khác": let the user type the title (otherwise the canonical category title is used). */
  editableTitle?: boolean;
  onRemove?: () => void;
  existingItem?: VisitInstanceLogisticsItem | null;
}

/** "Đã trao đổi bên ngoài" form (Part D) — required note, optional department, NO email; status DONE. */
function OfflineCard({
  cardKey, itemType, label, title, editableTitle, onRemove, visitInstanceId, departments, canManage, busyKey, onSubmit, existingItem, onCancel, pushToast,
}: OfflineCardProps) {
  const [note, setNote] = useState(() => existingItem?.offlineCoordinationNote || existingItem?.description || '');
  const [departmentId, setDepartmentId] = useState(() => existingItem?.requestedToDepartmentId?.toString() || '');
  const [titleVal, setTitleVal] = useState(() => existingItem?.title || '');
  const [err, setErr] = useState<string | null>(null);
  const [localSubmitted, setLocalSubmitted] = useState(false);
  const busy = busyKey === cardKey;

  const offlineNoteRef = useRef<HTMLTextAreaElement | null>(null);

  useEffect(() => {
    if (offlineNoteRef.current) {
      offlineNoteRef.current.style.height = 'auto';
      offlineNoteRef.current.style.height = `${Math.max(42, offlineNoteRef.current.scrollHeight)}px`;
    }
  }, [note]);

  useEffect(() => {
    if (existingItem) {
      setNote(existingItem.offlineCoordinationNote || existingItem.description || '');
      setDepartmentId(existingItem.requestedToDepartmentId?.toString() || '');
      setTitleVal(existingItem.title || '');
      setErr(null);
      setLocalSubmitted(true);
    } else {
      setLocalSubmitted(false);
    }
  }, [existingItem]);

  const isSubmitted = !!existingItem || localSubmitted;
  const locked = existingItem ? LOCKED_STATUSES.has(existingItem.status) : false;
  const isFormDisabled = !canManage || isSubmitted;

  const doSave = async () => {
    if (editableTitle && !titleVal.trim()) { const m = 'Vui lòng nhập tiêu đề / nội dung công việc.'; setErr(m); pushToast('error', m); return; }
    if (!note.trim()) { const m = 'Vui lòng nhập ghi chú trao đổi bên ngoài (bắt buộc).'; setErr(m); pushToast('error', m); return; }
    const payload: PrepareVisitLogisticsPayload = {
      visitInstanceId,
      departmentId: departmentId ? Number(departmentId) : null,
      itemType,
      title: (editableTitle ? titleVal.trim() : (title ?? label)).trim() || label,
      description: note.trim(),
      coordinationMode: 'OFFLINE_COORDINATED',
      offlineCoordinationNote: note.trim(),
    };
    if (await onSubmit(cardKey, payload)) {
      setLocalSubmitted(true);
      setErr(null);
    }
  };

  return (
    <div className="ml-5 flex flex-col gap-4 p-5 bg-amber-50/40 border border-amber-200 rounded-xl shadow-sm">
      <div className="flex items-center gap-2 text-sm font-bold text-amber-800">
        <AlertCircle className="w-4 h-4" /> Đã trao đổi/xử lý bên ngoài hệ thống — chỉ lưu dấu vết, không gửi email.
        {onRemove && canManage && !isSubmitted && (
          <button type="button" onClick={onRemove} title="Xóa dòng"
            className="ml-auto inline-flex h-7 w-7 items-center justify-center rounded-lg bg-red-50 text-red-500 outline-none hover:bg-red-100">
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      {editableTitle && (
        <div>
          <label className="block text-xs font-bold text-gray-600 mb-1">Tiêu đề / nội dung công việc <span className="text-red-500">*</span></label>
          <input type="text" maxLength={255} disabled={isFormDisabled} value={titleVal} onChange={(e) => { setTitleVal(e.target.value); setErr(null); }}
            placeholder="VD: Hỗ trợ kỹ thuật âm thanh"
            className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
        </div>
      )}

      {/* Dòng 1: Ghi chú trao đổi bên ngoài (trái) & Phòng ban liên quan (phải) */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div>
          <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú trao đổi bên ngoài <span className="text-red-500">*</span></label>
          <textarea
            ref={offlineNoteRef}
            rows={1}
            disabled={isFormDisabled}
            value={note}
            onChange={(e) => {
              setNote(e.target.value);
              setErr(null);
              e.target.style.height = 'auto';
              e.target.style.height = `${Math.max(44, e.target.scrollHeight)}px`;
              e.target.scrollTop = 0;
            }}
            maxLength={5000}
            placeholder="VD: Đã liên hệ trực tiếp phòng Truyền thông qua điện thoại..."
            className="w-full px-3 py-2.5 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm leading-relaxed resize-none min-h-[44px] overflow-hidden disabled:bg-gray-50 disabled:text-gray-400"
          />
        </div>
        <div>
          <label className="block text-xs font-bold text-gray-600 mb-1">Phòng ban liên quan (tùy chọn)</label>
          <select disabled={isFormDisabled} value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}
            className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400">
            <option value="">-- Không gắn phòng ban --</option>
            {departments.map((d) => (
              <option key={d.departmentId} value={d.departmentId}>{d.departmentName}</option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex items-end">
        <div className="flex items-center justify-end w-full">
          {!isSubmitted && (
            <button type="button" disabled={busy || isFormDisabled} onClick={doSave}
              className="inline-flex items-center justify-center gap-2 rounded-xl bg-[#004c91] px-5 py-2.5 text-sm font-bold text-white shadow-sm outline-none transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-40">
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />}
              Lưu (đã trao đổi bên ngoài)
            </button>
          )}
          {isSubmitted && (
            <div className="flex items-center gap-3">
              <button type="button" disabled
                className="inline-flex items-center gap-1.5 rounded-xl bg-gray-100 px-4 py-2 text-sm font-bold text-gray-500 outline-none">
                <CheckCircle className="w-4 h-4" /> Đã lưu yêu cầu
              </button>
              {canManage && existingItem && !locked && onCancel && (
                <button type="button" disabled={busy} onClick={() => onCancel(cardKey, existingItem)}
                  className="inline-flex items-center gap-1.5 rounded-xl border border-red-200 bg-white px-4 py-2 text-sm font-bold text-red-600 outline-none transition-colors hover:bg-red-50 disabled:opacity-50">
                  {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />} Hủy yêu cầu
                </button>
              )}
            </div>
          )}
        </div>
      </div>

      {err && (
        <p className="flex items-center gap-1.5 text-xs font-semibold text-red-600">
          <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {err}
        </p>
      )}
    </div>
  );
}

interface CategoryCardProps extends SharedCardProps {
  cardKey: string;
  icon: React.ReactNode;
  label: string;
  itemType: LogisticsItemType;
  qtyLabel: string;
  notePlaceholder?: string;
  /** The single active (non-closed) item for this fixed category, if any. */
  activeItem: VisitInstanceLogisticsItem | null;
}

/**
 * A fixed logistics category (Welcome LED, Xe điện, Người lái, Phòng họp, Teabreak). When no active
 * item exists it offers the 3-way choice — Không cần / Cần (gửi qua hệ thống) / Cần (đã trao đổi bên
 * ngoài) — generalising the original Welcome-LED pattern to every card. When an active item exists it
 * renders the matching read-only card (system form summary or offline summary).
 */
function CategoryCard({
  cardKey, icon, label, itemType, qtyLabel, notePlaceholder, activeItem, ...shared
}: CategoryCardProps) {
  const [choice, setChoice] = useState<'none' | 'system' | 'offline'>('none');

  if (!shared.loadedOnce) {
    return (
      <div>
        <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">{icon} {label}</h4>
        <LoadingRow />
      </div>
    );
  }

  if (activeItem) {
    return activeItem.coordinationMode === 'OFFLINE_COORDINATED' ? (
      <OfflineCard {...shared} cardKey={`${cardKey}-offline`} itemType={itemType} title={label}
        label={`${label} (trao đổi bên ngoài)`} existingItem={activeItem}
        onCancel={() => shared.onCancel(`${cardKey}-offline`, activeItem)} />
    ) : (
      <ResourceCard {...shared} cardKey={cardKey} icon={icon} label={label} itemType={itemType}
        qtyLabel={qtyLabel} existingItem={activeItem} notePlaceholder={notePlaceholder} />
    );
  }

  return (
    <div>
      <div className="grid grid-cols-1 lg:grid-cols-[230px_370px_1fr] gap-y-2.5 gap-x-4 mb-2 pl-5">
        {([
          ['none', `Không cần ${label}`],
          ['system', `Cần ${label} — gửi yêu cầu qua hệ thống`],
          ['offline', `Cần ${label} — đã trao đổi bên ngoài`],
        ] as const).map(([val, lbl]) => (
          <label key={val} className="flex items-center gap-2.5 cursor-pointer select-none">
            <input type="radio" name={`choice-${cardKey}`} checked={choice === val} disabled={!shared.canManage}
              onChange={() => setChoice(val)}
              className="w-4 h-4 border-gray-300 text-[#004c91] focus:ring-[#004c91] shrink-0" />
            <span className="text-sm font-semibold text-gray-700">{lbl}</span>
          </label>
        ))}
      </div>
      {choice === 'system' && (
        <div className="pt-4 mt-3 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
          <ResourceCard {...shared} cardKey={cardKey} icon={icon} label={label} itemType={itemType}
            qtyLabel={qtyLabel} existingItem={null} notePlaceholder={notePlaceholder} />
        </div>
      )}
      {choice === 'offline' && (
        <div className="pt-4 mt-3 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
          <OfflineCard {...shared} cardKey={`${cardKey}-offline`} itemType={itemType} title={label}
            label={`${label} (trao đổi bên ngoài)`} />
        </div>
      )}
    </div>
  );
}

/** A "Mục 4: Khác" create row: pick coordination mode (system / offline), then the matching create form. */
function OtherCard({
  cardKey, label, isExtra = false, hideNoneOption = false, onRemove, onModeChange, ...shared
}: SharedCardProps & {
  cardKey: string;
  label: string;
  isExtra?: boolean;
  hideNoneOption?: boolean;
  onRemove?: () => void;
  onModeChange?: (mode: 'none' | 'system' | 'offline') => void;
}) {
  const shouldHideNone = isExtra || hideNoneOption;
  const [mode, setMode] = useState<'none' | 'system' | 'offline'>(shouldHideNone ? 'system' : 'none');

  const handleModeChange = (newMode: 'none' | 'system' | 'offline') => {
    setMode(newMode);
    onModeChange?.(newMode);
  };

  useEffect(() => {
    if (shouldHideNone && mode === 'none') {
      handleModeChange('system');
    } else {
      onModeChange?.(mode);
    }
  }, [mode, shouldHideNone]);

  return (
    <div>
      <div className="grid grid-cols-1 lg:grid-cols-[230px_370px_1fr] gap-y-2.5 gap-x-4 mb-3 pl-5">
        {shouldHideNone ? (
          <>
            <div className="hidden lg:block"></div>
            {([
              ['system', `Cần ${label} — gửi yêu cầu qua hệ thống`],
              ['offline', `Cần ${label} — đã trao đổi bên ngoài`],
            ] as const).map(([val, lbl]) => (
              <label key={val} className="flex items-center gap-2.5 cursor-pointer select-none">
                <input type="radio" name={`mode-${cardKey}`} checked={mode === val} disabled={!shared.canManage}
                  onChange={() => handleModeChange(val)}
                  className="w-4 h-4 border-gray-300 text-[#004c91] focus:ring-[#004c91] shrink-0" />
                <span className="text-sm font-semibold text-gray-700">{lbl}</span>
              </label>
            ))}
          </>
        ) : (
          ([
            ['none', `Không cần ${label}`],
            ['system', `Cần ${label} — gửi yêu cầu qua hệ thống`],
            ['offline', `Cần ${label} — đã trao đổi bên ngoài`],
          ] as const).map(([val, lbl]) => (
            <label key={val} className="flex items-center gap-2.5 cursor-pointer select-none">
              <input type="radio" name={`mode-${cardKey}`} checked={mode === val} disabled={!shared.canManage}
                onChange={() => handleModeChange(val)}
                className="w-4 h-4 border-gray-300 text-[#004c91] focus:ring-[#004c91] shrink-0" />
              <span className="text-sm font-semibold text-gray-700">{lbl}</span>
            </label>
          ))
        )}
      </div>
      {mode === 'system' && (
        <div className="pt-4 mt-3 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
          <ResourceCard {...shared} cardKey={cardKey} icon={<MoreHorizontal className="w-5 h-5 text-[#f37021]" />}
            label={label} itemType="OTHER" qtyLabel="Số lượng" editableTitle existingItem={null}
            notePlaceholder="Mô tả chi tiết công việc cần hỗ trợ..." onRemove={onRemove} />
        </div>
      )}
      {mode === 'offline' && (
        <div className="pt-4 mt-3 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
          <OfflineCard {...shared} cardKey={`${cardKey}-offline`} itemType="OTHER" label={label}
            editableTitle existingItem={null} onRemove={onRemove} />
        </div>
      )}
    </div>
  );
}

/** Final ("chốt") quantity: the proposed figure once the Host ACCEPTED, else the planned figure. */
function finalQuantity(it: VisitInstanceLogisticsItem): number | null | undefined {
  return it.proposalResponse === 'ACCEPTED' && it.proposedQuantity != null ? it.proposedQuantity : it.quantity;
}

// Tiêu đề tự nhập đôi khi lặp lại tên đoàn khách (vd "Phòng họp HN cho Đoàn X") dù cả trang đã
// scope theo đúng đoàn đó — cắt hậu tố " cho {tên đoàn}" khi hiển thị cho gọn, không đụng dữ liệu
// gốc. Chỉ cắt khi khớp CHÍNH XÁC tên đoàn hiện tại, tránh cắt nhầm nội dung hợp lệ chứa " cho ".
function displayItemTitle(title: string, delegationName?: string): string {
  if (!delegationName) return title;
  const suffix = ` cho ${delegationName}`;
  return title.toLowerCase().endsWith(suffix.toLowerCase()) ? title.slice(0, title.length - suffix.length).trim() : title;
}

function getStatusBadgeCls(status: string, fallback: string) {
  if (['ASSIGNED', 'ACCEPTED', 'IN_PROGRESS'].includes(status)) return 'bg-blue-50 text-blue-700 border-blue-200';
  if (status === 'DONE') return 'bg-green-50 text-green-700 border-green-200';
  if (['REJECTED', 'DECLINED'].includes(status)) return 'bg-red-50 text-red-700 border-red-200';
  if (status === 'CANCELLED') return 'bg-slate-100 text-slate-600 border-slate-200';
  return fallback;
}

function LogisticsListRow({ it, delegationName, canManage, busy, onRespond, onViewSent, onOpenSign, canSignItem }: {
  it: VisitInstanceLogisticsItem;
  delegationName?: string;
  canManage: boolean;
  busy: boolean;
  onRespond: (item: VisitInstanceLogisticsItem, accepted: boolean, note: string) => void;
  onViewSent: (item: VisitInstanceLogisticsItem) => void;
  onOpenSign: (item: VisitInstanceLogisticsItem) => void;
  canSignItem: (item: VisitInstanceLogisticsItem) => boolean;
}) {
  const meta = LOGISTICS_STATUS_META[it.status] ?? { label: it.status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  const statusCls = getStatusBadgeCls(it.status, meta.cls);
  const offline = it.coordinationMode === 'OFFLINE_COORDINATED';
  const proposed = it.status === 'CHANGE_PROPOSED';
  const finalQty = finalQuantity(it);
  const displayTitle = displayItemTitle(it.title, delegationName);
  const [rejecting, setRejecting] = useState(false);
  const [rejectNote, setRejectNote] = useState('');

  const isTerminal = it.status === 'CANCELLED' || it.status === 'REJECTED' || it.status === 'DECLINED' || it.status === 'DONE';
  let reasonLabel = '';
  let reasonText = '';
  if (it.status === 'CANCELLED') { reasonLabel = 'Lý do hủy'; reasonText = it.decisionNote || ''; }
  else if (it.status === 'REJECTED') { reasonLabel = 'Lý do từ chối'; reasonText = it.decisionNote || ''; }
  else if (it.status === 'DECLINED') { reasonLabel = 'Lý do từ chối nhận nhiệm vụ'; reasonText = it.assigneeResponseNote || ''; }

  const isHandoverEligible = it.coordinationMode === 'SYSTEM_REQUEST' && HANDOVER_STATUSES.has(it.status);
  const borrow = findHandover(it.handovers, 'BORROW');
  const canSign = canSignItem(it);

  return (
    <div className={`rounded-2xl border bg-white p-4 shadow-sm transition-all hover:shadow-md ${proposed ? 'border-violet-200 bg-violet-50/20' : 'border-slate-200 hover:border-slate-300'}`}>
      {/* Top Header: Title + All Status Badges in a clean horizontal layout */}
      <div className="flex flex-wrap items-center justify-between gap-3 pb-3 border-b border-slate-100">
        {/* Title & Quantity */}
        <div className="flex flex-wrap items-center gap-2 min-w-0">
          <h4 className="text-base font-bold text-slate-900 truncate" title={it.title}>{displayTitle}</h4>
          <span className="inline-flex items-center rounded-md bg-slate-100 border border-slate-200 px-2 py-0.5 text-xs font-semibold text-slate-600">
            {ITEM_TYPE_LABEL[it.itemType] ?? it.itemType}
          </span>
          {it.quantity != null && (
            <span className="inline-flex items-center text-xs font-medium text-slate-500 bg-slate-50 px-2 py-0.5 rounded border border-slate-200">
              SL dự kiến: <b className="ml-1 text-slate-800">{it.quantity}</b>
            </span>
          )}
          {it.proposedQuantity != null && it.proposalResponse !== 'ACCEPTED' && (
            <span className="inline-flex items-center text-xs font-semibold text-violet-700 bg-violet-50 px-2 py-0.5 rounded border border-violet-200">
              Đề xuất: {it.proposedQuantity}
            </span>
          )}
          {it.proposalResponse && finalQty != null && (
            <span className="inline-flex items-center text-xs font-semibold text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
              Chốt: {finalQty}
            </span>
          )}
        </div>

        {/* Badges Bar (Horizontal) */}
        <div className="flex flex-wrap items-center gap-1.5 shrink-0">
          {isHandoverEligible && (
            <HandoverStatusBadge borrow={borrow} />
          )}
          <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-bold ${statusCls}`}>
            {meta.label}
          </span>
          {it.coordinationMode && (
            <span className={`inline-flex items-center rounded-full border px-2.5 py-0.5 text-xs font-semibold ${offline ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-slate-200 bg-slate-50 text-slate-600'}`}>
              {COORD_LABEL[it.coordinationMode]}
            </span>
          )}
        </div>
      </div>

      {/* Card Body: Structured in 3 exact rows specified by user */}
      <div className="space-y-2 pt-3 text-xs text-slate-600">
        {/* Dòng 1: Tên phòng ban (Bên trái) + 2 Trạng thái ký (Bên phải) */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="min-w-0">
            {it.departmentName && (
              <div><span className="text-slate-400 font-medium">Phòng ban:</span> <b className="text-slate-800">{it.departmentName}</b></div>
            )}
          </div>
          {isHandoverEligible && (
            <div className="flex flex-wrap items-center gap-1.5 shrink-0">
              <SignChip
                label="Phòng ban ký giao"
                at={borrow?.providerSignedAt}
                name={borrow?.providerSignedByName}
              />
              <SignChip
                label="Người phụ trách ký nhận"
                at={borrow?.borrowerSignedAt}
                name={borrow?.borrowerSignedByName}
              />
            </div>
          )}
        </div>

        {/* Dòng 2: Thời gian (Bên trái, trải dài) + 2 Nút hành động (Bên phải) */}
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div className="min-w-0 flex-1">
            {(it.usageStartAt || it.usageEndAt) && (
              <div>
                <span className="text-slate-400 font-medium">Thời gian:</span>{' '}
                <span className="font-semibold text-slate-800">{fmtDateTime(it.usageStartAt)} – {fmtDateTime(it.usageEndAt)}</span>
              </div>
            )}
          </div>
          <div className="flex flex-wrap items-center gap-2 shrink-0">
            {isHandoverEligible && (
              <button
                type="button"
                onClick={() => onOpenSign(it)}
                className={`inline-flex items-center gap-1.5 rounded-xl border px-3 py-1.5 text-xs font-bold outline-none transition-colors shadow-sm ${
                  canSign
                    ? 'border-[#004c91] bg-[#004c91] text-white hover:bg-[#003b70]'
                    : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'
                }`}
              >
                <FileText className="w-3.5 h-3.5" />
                {canSign ? 'Ký nhận' : 'Xem biên bản'}
              </button>
            )}
            {it.coordinationMode === 'SYSTEM_REQUEST' && (
              <button type="button" onClick={() => onViewSent(it)}
                className="inline-flex items-center gap-1.5 rounded-xl border border-blue-200 bg-blue-50/50 px-3 py-1.5 text-xs font-bold text-[#004c91] outline-none hover:bg-blue-100 transition-colors shadow-sm">
                <Mail className="w-3.5 h-3.5" /> Mail đã gửi
              </button>
            )}
          </div>
        </div>

        {/* Dòng 3: Người gửi • Nhân sự + Ghi chú phụ */}
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            {it.requestedByName && (
              <div><span className="text-slate-400 font-medium">Người gửi:</span> <span className="font-semibold text-slate-700">{it.requestedByName}</span></div>
            )}
            {it.assignedToName && (
              <div className="flex items-center gap-1.5">
                {it.requestedByName && <span className="text-slate-300">•</span>}
                <span className="text-slate-400 font-medium">Nhân sự:</span>{' '}
                <span className="font-semibold text-slate-700">{it.assignedToName}</span>
              </div>
            )}
          </div>

          {offline && it.offlineCoordinationNote && (
            <div className="italic text-slate-500 line-clamp-1" title={it.offlineCoordinationNote}>
              <span className="font-semibold text-amber-700/80 not-italic">Ghi chú ngoài:</span> {it.offlineCoordinationNote}
            </div>
          )}
          {it.proposalResponseNote && (
            <div className="italic text-slate-500 line-clamp-1">
              <span className="font-semibold text-slate-600 not-italic">Phản hồi:</span> {it.proposalResponseNote}
            </div>
          )}
        </div>
      </div>

      {isTerminal && it.status !== 'DONE' && reasonText && (
        <div className="mt-3 rounded-xl border border-red-100 bg-red-50/70 p-3 text-xs text-red-700">
          <span className="font-bold">{reasonLabel}:</span> {reasonText}
        </div>
      )}

      {(proposed || it.proposalResponse === 'ACCEPTED') && (
        <div className={`mt-3 rounded-xl border p-3 ${proposed ? 'border-violet-200 bg-violet-50/30' : 'border-emerald-200 bg-emerald-50/30'}`}>
          <div className={`text-xs font-bold uppercase tracking-wide mb-2 ${proposed ? 'text-violet-700' : 'text-emerald-700'}`}>
            {proposed ? 'Phòng ban đề xuất thay đổi' : 'Đã chấp nhận đề xuất thay đổi'}
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-2 text-xs text-gray-700">
            {it.proposedQuantity != null && <div><span className="text-slate-500">Số lượng đề xuất:</span> <b className={proposed ? 'text-violet-900' : 'text-emerald-900'}>{it.proposedQuantity}</b></div>}
            {(it.proposedUsageStartAt || it.proposedUsageEndAt) && <div><span className="text-slate-500">Thời gian đề xuất:</span> <span className="font-semibold text-slate-800">{fmtDateTime(it.proposedUsageStartAt)} – {fmtDateTime(it.proposedUsageEndAt)}</span></div>}
            {it.proposedDescription && <div className="sm:col-span-2"><span className="text-slate-500">Nội dung đề xuất:</span> {it.proposedDescription}</div>}
            {it.proposalNote && <div className={`sm:col-span-2 font-medium ${proposed ? 'text-violet-800' : 'text-emerald-800'}`}>Lý do: {it.proposalNote}</div>}
          </div>
          {proposed && canManage && (!rejecting ? (
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <button type="button" disabled={busy} onClick={() => onRespond(it, true, '')}
                className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-bold text-white outline-none hover:bg-emerald-700 transition-colors disabled:opacity-50">
                {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />} Chấp nhận đề xuất
              </button>
              <button type="button" disabled={busy} onClick={() => setRejecting(true)}
                className="inline-flex items-center gap-1 rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-bold text-red-600 outline-none hover:bg-red-50 transition-colors disabled:opacity-50">
                <X className="w-3.5 h-3.5" /> Từ chối đề xuất
              </button>
            </div>
          ) : (
            <div className="mt-3 space-y-2">
              <textarea value={rejectNote} onChange={(e) => setRejectNote(e.target.value)} maxLength={1000}
                placeholder="Lý do từ chối đề xuất (bắt buộc)..."
                className="w-full h-[70px] resize-none rounded-lg border border-gray-300 px-3 py-2 text-xs outline-none focus:border-red-400 transition-colors" />
              <div className="flex items-center justify-end gap-2">
                <button type="button" disabled={busy} onClick={() => { setRejecting(false); setRejectNote(''); }}
                  className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-bold text-gray-600 outline-none hover:bg-gray-50 transition-colors disabled:opacity-50">Hủy</button>
                <button type="button" disabled={busy || !rejectNote.trim()} onClick={() => onRespond(it, false, rejectNote.trim())}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-red-600 px-3 py-1.5 text-xs font-bold text-white outline-none hover:bg-red-700 transition-colors disabled:opacity-50">
                  {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <X className="w-4 h-4" />} Xác nhận từ chối
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
