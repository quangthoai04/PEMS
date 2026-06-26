/**
 * VisitProcess "Chuẩn bị chi tiết" — real Host → Department logistics requests.
 *
 * Visual layout reproduces the original (da14fba) styled prototype: orange-accented collapsible "Mục"
 * cards (Welcome LED / Campus Tour / Họp / Khác) with resource sub-cards and the yellow "Trưởng phòng"
 * action card. Logic is 100% real — NO mock data, NO hard-coded department/leader name:
 *   - departments from GET .../support-departments,
 *   - "Gửi yêu cầu" → POST /api/delegations/preparevisitlogistics (REQUESTED + notification + email),
 *   - "Xem trước & sửa email" → editable text preview (EmailPreviewModal, plain text not raw HTML),
 *   - status badges from the real enum (LOGISTICS_STATUS_META).
 *
 * Part D: Welcome LED supports "đã trao đổi bên ngoài" → coordinationMode OFFLINE_COORDINATED (no email,
 *         status DONE, required note). Part E: usage time is a datetime range (Từ → Đến).
 * Part F: "Khác" lets the host add multiple independent requests with a + button.
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Loader2, Send, Eye, Plus, Trash2, ChevronUp, ChevronDown, CheckCircle, CheckCircle2, AlertCircle,
  MonitorPlay, MapPin, Building2, MoreHorizontal, Car, UserCheck, Coffee,
} from 'lucide-react';
import { delegationsApi } from '../api/delegationsApi';
import { EmailPreviewModal } from './EmailPreviewModal';
import {
  LOGISTICS_STATUS_META,
  type LogisticsItemType,
  type LogisticsCoordinationMode,
  type PrepareVisitLogisticsPayload,
  type SupportDepartment,
  type VisitInstanceLogisticsItem,
} from '../types/delegations.types';

type ToastFn = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

interface Props {
  visitInstanceId: number;
  relation: string;            // HOST | STAFF_LEADER | HO | ...
  instanceStatus: string;      // ASSIGNED | BEFORE_VISIT | ...
  delegationName: string;
  campusName: string;
  hostName: string;
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

type ResourceForm = {
  quantity: string;
  usageStartAt: string;  // datetime-local "YYYY-MM-DDTHH:mm"
  usageEndAt: string;
  note: string;
  departmentId: string;
  title: string;         // editable only for "Khác"
};
const emptyForm = (title = ''): ResourceForm =>
  ({ quantity: '', usageStartAt: '', usageEndAt: '', note: '', departmentId: '', title });

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

export function LogisticsRequestSection({
  visitInstanceId, relation, instanceStatus, delegationName, campusName, hostName, pushToast,
}: Props) {
  const canManage = relation === 'HOST' && (instanceStatus === 'ASSIGNED' || instanceStatus === 'BEFORE_VISIT');

  const [departments, setDepartments] = useState<SupportDepartment[]>([]);
  const [items, setItems] = useState<VisitInstanceLogisticsItem[]>([]);
  const [loadingList, setLoadingList] = useState(false);
  const [busyKey, setBusyKey] = useState<string | null>(null);

  const [openSection, setOpenSection] = useState<Record<number, boolean>>({ 1: true, 2: true, 3: true, 4: true });
  const toggleSection = (n: number) => setOpenSection((p) => ({ ...p, [n]: !p[n] }));

  // Welcome LED: 'none' | 'system' (request via system) | 'offline' (handled outside).
  const [ledChoice, setLedChoice] = useState<'none' | 'system' | 'offline'>('none');

  // Mục 4 "Khác": dynamic list of independent requests (Part F). Each id renders one ResourceCard.
  const otherIdRef = useRef(1);
  const [otherIds, setOtherIds] = useState<number[]>([1]);

  // Editable email preview (shared modal) — mirrors the participant flow, but text not raw HTML.
  const [preview, setPreview] = useState({
    open: false, loading: false, sending: false, error: null as string | null,
    subject: '', body: '', isActionTemplate: false,
    systemActionDescription: null as string | null, lockedActionBlockHtml: null as string | null,
  });
  const previewPayload = useRef<PrepareVisitLogisticsPayload | null>(null);
  const previewCtx = useRef<{ leaderName: string } | null>(null);
  const previewResetRef = useRef<(() => void) | null>(null);

  const loadList = useCallback(async () => {
    setLoadingList(true);
    try {
      const res = await delegationsApi.getInstanceLogistics(visitInstanceId);
      setItems(res.items || []);
    } catch {
      setItems([]);
    } finally {
      setLoadingList(false);
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

  const latestItemFor = (title: string): VisitInstanceLogisticsItem | null => {
    const matches = items.filter((i) => i.title === title);
    if (matches.length === 0) return null;
    return matches.reduce((a, b) => (b.logisticsItemId > a.logisticsItemId ? b : a));
  };

  const ctxFor = (payload: PrepareVisitLogisticsPayload, leaderName: string) => ({
    departmentLeaderName: leaderName,
    requesterName: hostName,
    DelegationName: delegationName,
    CampusName: campusName,
    logisticsTitle: payload.title,
    itemType: ITEM_TYPE_LABEL[payload.itemType] ?? payload.itemType,
    quantity: payload.quantity != null ? String(payload.quantity) : '—',
    usageStartAt: payload.usageStartAt || '—',
    usageEndAt: payload.usageEndAt || '—',
  });

  // Send a request directly (default email template) — returns true on success so the card can reset.
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

  // Open the editable email preview bound to a request payload.
  const openPreview = async (payload: PrepareVisitLogisticsPayload, onReset: () => void) => {
    const dept = departments.find((d) => String(d.departmentId) === String(payload.departmentId));
    previewPayload.current = payload;
    previewCtx.current = { leaderName: dept?.leaderName ?? 'Trưởng phòng' };
    previewResetRef.current = onReset;
    setPreview((p) => ({ ...p, open: true, loading: true, error: null }));
    await fetchPreview(payload, dept?.leaderName ?? 'Trưởng phòng');
  };

  const fetchPreview = async (payload: PrepareVisitLogisticsPayload, leaderName: string) => {
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode: 'LOGISTICS_REQUEST_TO_DEPARTMENT',
        context: ctxFor(payload, leaderName),
      });
      setPreview((p) => ({
        ...p, open: true, loading: false, error: null,
        subject: res.subject, body: res.editableBodyText, // text, not raw HTML (Part A)
        isActionTemplate: res.isActionTemplate,
        systemActionDescription: res.systemActionDescription ?? null,
        lockedActionBlockHtml: res.lockedActionBlockHtml ?? null,
      }));
    } catch (e: any) {
      setPreview((p) => ({ ...p, open: true, loading: false, error: apiError(e, 'Không thể tải bản xem trước email.') }));
    }
  };

  const restorePreview = () => {
    const pl = previewPayload.current; const ctx = previewCtx.current;
    if (!pl || !ctx) return;
    setPreview((p) => ({ ...p, loading: true, error: null }));
    void fetchPreview(pl, ctx.leaderName);
  };

  const closePreview = () => setPreview((p) => ({ ...p, open: false }));

  const sendWithEditedContent = async () => {
    const pl = previewPayload.current;
    if (!pl) return;
    if (!preview.subject.trim()) { pushToast('error', 'Tiêu đề email không được để trống.'); return; }
    if (!preview.body.trim()) { pushToast('error', 'Nội dung email không được để trống.'); return; }
    setPreview((p) => ({ ...p, sending: true }));
    try {
      const res = await delegationsApi.prepareVisitLogistics({
        ...pl,
        emailOverride: { useEditedContent: true, subject: preview.subject.trim(), bodyText: preview.body },
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
    visitInstanceId, departments, canManage, busyKey, latestItemFor,
    onSubmit: submitRequest, onPreview: openPreview,
  };

  return (
    <div className="space-y-8">
      {/* Mục 1: Welcome LED — 3 lựa chọn (Part D) */}
      <MucCard title="Mục 1: Welcome LED" icon={<MonitorPlay className="w-5 h-5 text-[#f37021]" />}
        open={openSection[1]} onToggle={() => toggleSection(1)}>
        <div className="space-y-3 mb-4">
          {([
            ['none', 'Không cần màn LED'],
            ['system', 'Cần màn LED — gửi yêu cầu qua hệ thống'],
            ['offline', 'Cần màn LED — đã trao đổi bên ngoài'],
          ] as const).map(([val, label]) => (
            <label key={val} className="flex items-center gap-3 cursor-pointer">
              <input type="radio" name="ledChoice" checked={ledChoice === val} disabled={!canManage}
                onChange={() => setLedChoice(val)}
                className="w-5 h-5 border-gray-300 text-[#004c91] focus:ring-[#004c91]" />
              <span className="text-[15px] font-bold text-gray-700">{label}</span>
            </label>
          ))}
        </div>
        {ledChoice === 'system' && (
          <div className="pt-4 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
            <ResourceCard {...shared} cardKey="led" icon={<MonitorPlay className="w-6 h-6 text-[#f37021]" />}
              label="Welcome LED" itemType="LED" qtyLabel="Số lượng màn"
              notePlaceholder="Kích thước, nội dung hiển thị, đã gửi ảnh thiết kế..." />
          </div>
        )}
        {ledChoice === 'offline' && (
          <div className="pt-4 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
            <OfflineCard {...shared} cardKey="led-offline" itemType="LED" label="Welcome LED (trao đổi bên ngoài)" />
          </div>
        )}
      </MucCard>

      {/* Mục 2: Chuẩn bị cho Campus Tour */}
      <MucCard title="Mục 2: Chuẩn bị cho Campus Tour" icon={<MapPin className="w-5 h-5 text-[#f37021]" />}
        open={openSection[2]} onToggle={() => toggleSection(2)}>
        <div className="space-y-8">
          <ResourceCard {...shared} cardKey="electricCar" icon={<Car className="w-6 h-6 text-[#f37021]" />}
            label="Xe điện" itemType="TRANSPORT" qtyLabel="Số lượng cần mượn" notePlaceholder="Ghi chú thêm..." />
          <hr className="border-t-[2px] border-gray-200" />
          <ResourceCard {...shared} cardKey="driver" icon={<UserCheck className="w-6 h-6 text-[#f37021]" />}
            label="Người lái" itemType="TRANSPORT" qtyLabel="Số lượng" notePlaceholder="Yêu cầu về tài xế, thời gian hỗ trợ..." />
        </div>
      </MucCard>

      {/* Mục 3: Chuẩn bị cho họp */}
      <MucCard title="Mục 3: Chuẩn bị cho họp" icon={<Building2 className="w-5 h-5 text-[#f37021]" />}
        open={openSection[3]} onToggle={() => toggleSection(3)}>
        <div className="space-y-8">
          <ResourceCard {...shared} cardKey="room" icon={<Building2 className="w-6 h-6 text-[#f37021]" />}
            label="Phòng họp" itemType="ROOM" qtyLabel="Số phòng"
            notePlaceholder="Tên phòng / vị trí (VD: Tòa Alpha, P.101), layout, thiết bị..." />
          <hr className="border-t-[2px] border-gray-200" />
          <ResourceCard {...shared} cardKey="teabreak" icon={<Coffee className="w-6 h-6 text-[#f37021]" />}
            label="Teabreak" itemType="MEAL" qtyLabel="Số lượng (suất)"
            notePlaceholder="Layout, khăn trải bàn, biển tên, yêu cầu đặc biệt..." />
        </div>
      </MucCard>

      {/* Mục 4: Khác — thêm nhiều yêu cầu (Part F) */}
      <MucCard title="Mục 4: Khác" icon={<MoreHorizontal className="w-5 h-5 text-[#f37021]" />}
        open={openSection[4]} onToggle={() => toggleSection(4)}>
        <div className="space-y-6">
          {otherIds.map((id, idx) => (
            <div key={id} className={idx > 0 ? 'pt-6 border-t border-gray-100' : ''}>
              <ResourceCard {...shared} cardKey={`other-${id}`} icon={<MoreHorizontal className="w-6 h-6 text-[#f37021]" />}
                label={`Yêu cầu khác ${otherIds.length > 1 ? `#${idx + 1}` : ''}`.trim()} itemType="OTHER"
                qtyLabel="Số lượng" editableTitle notePlaceholder="Mô tả chi tiết công việc cần hỗ trợ..."
                onRemove={otherIds.length > 1 ? () => setOtherIds((p) => p.filter((x) => x !== id)) : undefined} />
            </div>
          ))}
          {canManage && (
            <button type="button"
              onClick={() => { otherIdRef.current += 1; setOtherIds((p) => [...p, otherIdRef.current]); }}
              className="inline-flex items-center gap-1.5 rounded-xl border-2 border-dashed border-[#f37021]/40 px-4 py-2 text-sm font-bold text-[#f37021] outline-none hover:bg-orange-50">
              <Plus className="w-4 h-4" /> Thêm yêu cầu khác
            </button>
          )}
        </div>
      </MucCard>

      {/* Danh sách yêu cầu hậu cần thật (Part H: badge coordination + status enum) */}
      <div className="bg-white border border-gray-200 rounded-2xl shadow-sm overflow-hidden">
        <div className="flex items-center justify-between px-6 py-4 bg-white border-b border-gray-100">
          <h3 className="text-xl font-bold text-orange-900 flex items-center gap-2">
            <div className="p-1.5 bg-orange-100 rounded-lg"><Plus className="w-5 h-5 text-[#f37021]" /></div>
            Danh sách yêu cầu hậu cần
          </h3>
          {loadingList && <Loader2 className="w-4 h-4 animate-spin text-gray-400" />}
        </div>
        <div className="p-6 pt-4">
          {loadingList ? (
            <div className="flex items-center gap-2 py-4 text-sm text-gray-500"><Loader2 className="w-4 h-4 animate-spin" /> Đang tải...</div>
          ) : items.length === 0 ? (
            <p className="py-2 text-sm italic text-slate-400">Chưa có yêu cầu hậu cần nào.</p>
          ) : (
            <div className="space-y-2">
              {items.map((it) => {
                const meta = LOGISTICS_STATUS_META[it.status] ?? { label: it.status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
                const offline = it.coordinationMode === 'OFFLINE_COORDINATED';
                return (
                  <div key={it.logisticsItemId} className="rounded-xl border border-gray-200 bg-white p-3 shadow-sm">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                      <div className="min-w-0">
                        <div className="truncate text-sm font-bold text-gray-800">{it.title}</div>
                        <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-gray-500">
                          <span>{ITEM_TYPE_LABEL[it.itemType] ?? it.itemType}</span>
                          {it.quantity != null && <span>SL: {it.quantity}</span>}
                          {it.departmentName && <span>Phòng ban: {it.departmentName}</span>}
                          {it.assignedToName && <span>Nhân sự: {it.assignedToName}</span>}
                        </div>
                        {offline && it.offlineCoordinationNote && (
                          <div className="mt-1 text-[11px] italic text-amber-700">Ghi chú: {it.offlineCoordinationNote}</div>
                        )}
                      </div>
                      <div className="flex flex-col items-end gap-1">
                        <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
                          {meta.label}
                        </span>
                        {it.coordinationMode && (
                          <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[10px] font-bold ${offline ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-slate-200 bg-slate-50 text-slate-500'}`}>
                            {COORD_LABEL[it.coordinationMode]}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      </div>

      <EmailPreviewModal
        open={preview.open}
        loading={preview.loading}
        sending={preview.sending}
        error={preview.error}
        subject={preview.subject}
        body={preview.body}
        isActionTemplate={preview.isActionTemplate}
        systemActionDescription={preview.systemActionDescription}
        lockedActionBlockHtml={preview.lockedActionBlockHtml}
        canSend
        sendLabel="Gửi với nội dung này"
        onSubjectChange={(v) => setPreview((p) => ({ ...p, subject: v }))}
        onBodyChange={(v) => setPreview((p) => ({ ...p, body: v }))}
        onClose={closePreview}
        onRestore={restorePreview}
        onSend={sendWithEditedContent}
      />
    </div>
  );
}

/** Collapsible "Mục" card — original orange-accented header + animated body. */
function MucCard({ title, icon, open, onToggle, children }: {
  title: string; icon: React.ReactNode; open: boolean; onToggle: () => void; children: React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-2xl shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-6 py-4 cursor-pointer hover:bg-orange-50/50 transition-colors bg-white" onClick={onToggle}>
        <h3 className="text-xl font-bold text-orange-900 flex items-center gap-2">
          <div className="p-1.5 bg-orange-100 rounded-lg">{icon}</div>
          {title}
        </h3>
        <div className="w-8 h-8 rounded-full bg-gray-50 flex items-center justify-center text-gray-500">
          {open ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
        </div>
      </div>
      <AnimatePresence>
        {open && (
          <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }}>
            <div className="p-6 pt-2 border-t border-gray-100 bg-white">{children}</div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}

interface SharedCardProps {
  visitInstanceId: number;
  departments: SupportDepartment[];
  canManage: boolean;
  busyKey: string | null;
  latestItemFor: (title: string) => VisitInstanceLogisticsItem | null;
  onSubmit: (key: string, payload: PrepareVisitLogisticsPayload) => Promise<boolean>;
  onPreview: (payload: PrepareVisitLogisticsPayload, onReset: () => void) => void;
}

interface ResourceCardProps extends SharedCardProps {
  cardKey: string;
  icon: React.ReactNode;
  label: string;
  itemType: LogisticsItemType;
  qtyLabel: string;
  notePlaceholder?: string;
  editableTitle?: boolean;
  onRemove?: () => void;
}

/** SYSTEM_REQUEST resource form (datetime range, dept required), wired to the real API. */
function ResourceCard({
  cardKey, icon, label, itemType, qtyLabel, notePlaceholder, editableTitle, onRemove,
  visitInstanceId, departments, canManage, busyKey, latestItemFor, onSubmit, onPreview,
}: ResourceCardProps) {
  const [form, setForm] = useState<ResourceForm>(() => emptyForm(editableTitle ? '' : label));
  const [err, setErr] = useState<string | null>(null);
  const set = (k: keyof ResourceForm, v: string) => { setForm((f) => ({ ...f, [k]: v })); setErr(null); };
  const reset = () => { setForm(emptyForm(editableTitle ? '' : label)); setErr(null); };

  const title = editableTitle ? form.title.trim() : label;
  const busy = busyKey === cardKey;
  const dept = departments.find((d) => String(d.departmentId) === form.departmentId);
  const existing = latestItemFor(title || label);
  const meta = existing ? (LOGISTICS_STATUS_META[existing.status] ?? null) : null;
  const disabled = !canManage;

  const validate = (): string | null => {
    if (editableTitle && !title) return 'Vui lòng nhập tiêu đề / nội dung công việc.';
    if (!form.departmentId) return 'Vui lòng chọn phòng ban xử lý.';
    if (form.quantity && (Number.isNaN(Number(form.quantity)) || Number(form.quantity) < 1)) return 'Số lượng phải là số ≥ 1.';
    if (!form.usageStartAt) return 'Vui lòng nhập thời gian bắt đầu sử dụng.';
    if (!form.usageEndAt) return 'Vui lòng nhập thời gian kết thúc sử dụng.';
    if (form.usageEndAt <= form.usageStartAt) return 'Thời gian kết thúc phải sau thời gian bắt đầu.';
    return null;
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
    priority: 'MEDIUM',
    coordinationMode: 'SYSTEM_REQUEST',
  });

  const doSend = async () => {
    const v = validate();
    if (v) { setErr(v); return; }
    if (await onSubmit(cardKey, buildPayload())) reset();
  };
  const doPreview = () => {
    const v = validate();
    if (v) { setErr(v); return; }
    onPreview(buildPayload(), reset);
  };

  return (
    <div>
      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2 flex-wrap">
        {icon} {label}
        {meta && (
          <span className={`ml-1 inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
            {meta.label}
          </span>
        )}
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

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-4">
            {editableTitle && (
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">Tiêu đề / nội dung công việc <span className="text-red-500">*</span></label>
                <input type="text" maxLength={255} disabled={disabled} value={form.title} onChange={(e) => set('title', e.target.value)}
                  placeholder="VD: Hỗ trợ kỹ thuật âm thanh"
                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
              </div>
            )}
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">{qtyLabel}</label>
              <input type="number" min="1" disabled={disabled} value={form.quantity} onChange={(e) => set('quantity', e.target.value)}
                placeholder="VD: 2"
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian bắt đầu sử dụng <span className="text-red-500">*</span></label>
              <input type="datetime-local" disabled={disabled} value={form.usageStartAt} onChange={(e) => set('usageStartAt', e.target.value)}
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian kết thúc sử dụng <span className="text-red-500">*</span></label>
              <input type="datetime-local" disabled={disabled} value={form.usageEndAt} onChange={(e) => set('usageEndAt', e.target.value)}
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
          </div>
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú (Note)</label>
              <textarea disabled={disabled} value={form.note} onChange={(e) => set('note', e.target.value)} placeholder={notePlaceholder ?? 'Ghi chú thêm...'}
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm resize-none h-[120px] disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Chọn phòng ban xử lý <span className="text-red-500">*</span></label>
              <select disabled={disabled} value={form.departmentId} onChange={(e) => set('departmentId', e.target.value)}
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400">
                <option value="">-- Chọn phòng ban --</option>
                {departments.map((d) => (
                  <option key={d.departmentId} value={d.departmentId} disabled={!d.canInvite}>
                    {d.departmentName}{!d.canInvite ? ' — không khả dụng' : ''}
                  </option>
                ))}
              </select>

              {form.departmentId && dept && (
                <div className="mt-3 p-3 bg-yellow-50/80 border border-yellow-200 rounded-xl flex flex-col gap-3 animate-in fade-in slide-in-from-top-2">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full bg-yellow-500 text-white flex items-center justify-center font-bold text-lg shrink-0">
                      {(dept.leaderName ?? dept.departmentName).charAt(0)}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="text-sm font-bold text-yellow-900 flex items-center gap-2 flex-wrap">
                        {dept.leaderName ?? 'Chưa có trưởng phòng đang hoạt động'}
                        {dept.leaderName && <span className="text-[11px] font-bold uppercase tracking-wider text-yellow-700 bg-white px-2 py-0.5 rounded-md border border-yellow-200 shadow-sm">Trưởng phòng</span>}
                      </div>
                      {existing && meta && (
                        <div className="text-[13px] font-bold text-[#10b981] flex items-center gap-1 mt-1">
                          <CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi yêu cầu{existing.departmentName ? ` tới ${existing.departmentName}` : ''} — {meta.label}
                        </div>
                      )}
                    </div>
                  </div>
                  {canManage && (
                    <div className="flex flex-wrap justify-end gap-2">
                      <button type="button" disabled={busy} onClick={doPreview}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-yellow-300 bg-white px-3 py-2 text-xs font-bold text-[#004c91] outline-none transition-colors hover:bg-yellow-100 disabled:opacity-50">
                        <Eye className="w-4 h-4" /> Xem trước & sửa email
                      </button>
                      <button type="button" disabled={busy} onClick={doSend}
                        className="inline-flex items-center gap-1.5 rounded-lg bg-[#004c91] px-4 py-2 text-xs font-bold text-white outline-none transition-colors hover:bg-[#013565] disabled:opacity-50">
                        {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />} Gửi yêu cầu
                      </button>
                    </div>
                  )}
                </div>
              )}
            </div>
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
}

/** "Đã trao đổi bên ngoài" form (Part D) — required note, optional department, NO email; status DONE. */
function OfflineCard({
  cardKey, itemType, label, visitInstanceId, departments, canManage, busyKey, latestItemFor, onSubmit,
}: OfflineCardProps) {
  const [note, setNote] = useState('');
  const [departmentId, setDepartmentId] = useState('');
  const [err, setErr] = useState<string | null>(null);
  const busy = busyKey === cardKey;
  const existing = latestItemFor(label);

  const doSave = async () => {
    if (!note.trim()) { setErr('Vui lòng nhập ghi chú trao đổi bên ngoài (bắt buộc).'); return; }
    const payload: PrepareVisitLogisticsPayload = {
      visitInstanceId,
      departmentId: departmentId ? Number(departmentId) : null,
      itemType,
      title: label,
      description: note.trim(),
      coordinationMode: 'OFFLINE_COORDINATED',
      offlineCoordinationNote: note.trim(),
      priority: 'MEDIUM',
    };
    if (await onSubmit(cardKey, payload)) { setNote(''); setDepartmentId(''); setErr(null); }
  };

  return (
    <div className="flex flex-col gap-4 p-5 bg-amber-50/40 border border-amber-200 rounded-xl shadow-sm">
      <div className="flex items-center gap-2 text-sm font-bold text-amber-800">
        <AlertCircle className="w-4 h-4" /> Đã trao đổi/xử lý bên ngoài hệ thống — chỉ lưu dấu vết, không gửi email.
      </div>
      <div>
        <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú trao đổi bên ngoài <span className="text-red-500">*</span></label>
        <textarea disabled={!canManage} value={note} onChange={(e) => { setNote(e.target.value); setErr(null); }} maxLength={5000}
          placeholder="VD: Đã liên hệ trực tiếp phòng Truyền thông qua điện thoại, ảnh LED gửi qua email nội bộ..."
          className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm resize-none h-[100px] disabled:bg-gray-50 disabled:text-gray-400" />
      </div>
      <div>
        <label className="block text-xs font-bold text-gray-600 mb-1">Phòng ban liên quan (tùy chọn)</label>
        <select disabled={!canManage} value={departmentId} onChange={(e) => setDepartmentId(e.target.value)}
          className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400">
          <option value="">-- Không gắn phòng ban --</option>
          {departments.map((d) => (
            <option key={d.departmentId} value={d.departmentId}>{d.departmentName}</option>
          ))}
        </select>
      </div>
      {existing && (
        <div className="text-[13px] font-bold text-[#10b981] flex items-center gap-1">
          <CheckCircle2 className="w-3.5 h-3.5" /> Đã lưu (đã trao đổi bên ngoài).
        </div>
      )}
      {err && (
        <p className="flex items-center gap-1.5 text-xs font-semibold text-red-600">
          <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {err}
        </p>
      )}
      {canManage && (
        <div className="flex justify-end">
          <button type="button" disabled={busy} onClick={doSave}
            className="inline-flex items-center gap-1.5 rounded-lg bg-[#f37021] px-4 py-2 text-xs font-bold text-white outline-none transition-colors hover:bg-[#d95f12] disabled:opacity-50">
            {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle2 className="w-4 h-4" />} Lưu (đã trao đổi bên ngoài)
          </button>
        </div>
      )}
    </div>
  );
}
