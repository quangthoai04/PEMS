/**
 * VisitProcess "Chuẩn bị chi tiết" — real Host → Department logistics requests.
 *
 * The VISUAL layout reproduces the original (commit da14fba) styled prototype: orange-accented
 * collapsible "Mục" cards (Welcome LED / Campus Tour / Họp / Khác) with resource sub-cards and the
 * yellow "Trưởng phòng" action card. The LOGIC is 100% real — there is NO mock data and NO hard-coded
 * department/leader name:
 *   - departments come from GET .../support-departments,
 *   - "Gửi yêu cầu" calls POST /api/delegations/preparevisitlogistics (status REQUESTED + notification
 *     + email to the department leader),
 *   - "Xem trước & sửa email" opens the editable email preview (EmailPreviewModal),
 *   - every card's status badge + the request list reflect the real enum
 *     (REQUESTED / CHANGE_PROPOSED / ASSIGNED / ACCEPTED / IN_PROGRESS / DONE / REJECTED / DECLINED / CANCELLED).
 *
 * Each resource sub-card maps to a logistics itemType and sends with a canonical title, so its status
 * badge can be matched back from the real list by (itemType + title).
 */
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Loader2, Send, Eye, Plus, ChevronUp, ChevronDown, Calendar, CheckCircle, CheckCircle2,
  MonitorPlay, MapPin, Building2, MoreHorizontal, Car, UserCheck, Coffee,
} from 'lucide-react';
import { delegationsApi } from '../api/delegationsApi';
import { EmailPreviewModal } from './EmailPreviewModal';
import {
  LOGISTICS_STATUS_META,
  type LogisticsItemType,
  type LogisticsItemStatus,
  type LogisticsPriority,
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

// One resource sub-card = one logistics itemType + a canonical title (used to match its real status).
type ResourceForm = {
  quantity: string;
  date: string;
  startTime: string;
  endTime: string;
  note: string;
  departmentId: string;
  title: string; // editable only for "Khác"
};
const emptyForm = (): ResourceForm => ({ quantity: '', date: '', startTime: '', endTime: '', note: '', departmentId: '', title: '' });

function apiError(e: any, fallback: string): string {
  const data = e?.response?.data;
  if (!data) return fallback;
  if (typeof data === 'string' && data.trim()) return data;
  if (data.message) return data.message;
  return fallback;
}

// Combine the old "date + time" inputs into the wall-clock string the API expects (no Date/TZ shift).
const combine = (date: string, time: string): string | null => (date && time ? `${date}T${time}` : null);

export function LogisticsRequestSection({
  visitInstanceId, relation, instanceStatus, delegationName, campusName, hostName, pushToast,
}: Props) {
  const canManage = relation === 'HOST' && (instanceStatus === 'ASSIGNED' || instanceStatus === 'BEFORE_VISIT');

  const [departments, setDepartments] = useState<SupportDepartment[]>([]);
  const [items, setItems] = useState<VisitInstanceLogisticsItem[]>([]);
  const [loadingList, setLoadingList] = useState(false);
  const [busyKey, setBusyKey] = useState<string | null>(null);

  // Collapsible "Mục" sections (open by default, like the original).
  const [openSection, setOpenSection] = useState<Record<number, boolean>>({ 1: true, 2: true, 3: true, 4: true });
  const toggleSection = (n: number) => setOpenSection((p) => ({ ...p, [n]: !p[n] }));

  // Welcome LED: keep the original "Cần / Không cần" radio.
  const [needLED, setNeedLED] = useState(false);

  // Editable email preview (shared modal) — mirrors the participant flow.
  const [preview, setPreview] = useState({
    open: false, loading: false, sending: false, error: null as string | null,
    subject: '', body: '', isActionTemplate: false,
    systemActionDescription: null as string | null, lockedActionBlockHtml: null as string | null,
  });
  // Payload the preview was opened for (so the send uses the previewed request).
  const previewPayload = useRef<PrepareVisitLogisticsPayload | null>(null);
  const previewCtx = useRef<{ leaderName: string } | null>(null);

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

  // Most recent real request matching a card's canonical title (for the per-card status badge).
  const latestItemFor = (title: string): VisitInstanceLogisticsItem | null => {
    const matches = items.filter((i) => i.title === title);
    if (matches.length === 0) return null;
    return matches.reduce((a, b) => (b.logisticsItemId > a.logisticsItemId ? b : a));
  };

  const buildPayload = (
    itemType: LogisticsItemType,
    title: string,
    form: ResourceForm,
    emailOverride?: { useEditedContent: boolean; subject: string; bodyHtml: string },
  ): PrepareVisitLogisticsPayload => ({
    visitInstanceId,
    departmentId: Number(form.departmentId),
    itemType,
    title: title.trim(),
    description: form.note.trim() || null,
    quantity: form.quantity ? Number(form.quantity) : null,
    usageStartAt: combine(form.date, form.startTime),
    usageEndAt: combine(form.date, form.endTime),
    priority: 'MEDIUM' as LogisticsPriority,
    dueAt: null,
    emailOverride,
  });

  const validate = (title: string, form: ResourceForm): string | null => {
    if (!form.departmentId) return 'Vui lòng chọn phòng ban xử lý.';
    if (!title.trim()) return 'Vui lòng nhập tiêu đề yêu cầu.';
    if (form.quantity && (Number.isNaN(Number(form.quantity)) || Number(form.quantity) < 1))
      return 'Số lượng phải là số ≥ 1.';
    const s = combine(form.date, form.startTime), e = combine(form.date, form.endTime);
    if (s && e && e <= s) return 'Thời gian kết thúc phải sau thời gian bắt đầu.';
    return null;
  };

  // Direct send (default email template).
  const sendDirect = async (key: string, itemType: LogisticsItemType, title: string, form: ResourceForm, onDone: () => void) => {
    const err = validate(title, form);
    if (err) { pushToast('error', err); return; }
    setBusyKey(key);
    try {
      const res = await delegationsApi.prepareVisitLogistics(buildPayload(itemType, title, form));
      pushToast(res.emailStatus === 'FAILED' ? 'warning' : 'success', res.message || 'Đã gửi yêu cầu hậu cần.');
      onDone();
      await loadList();
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể gửi yêu cầu hậu cần.'));
    } finally {
      setBusyKey(null);
    }
  };

  // Open the editable email preview bound to a card's request.
  const openPreview = async (itemType: LogisticsItemType, title: string, form: ResourceForm) => {
    const err = validate(title, form);
    if (err) { pushToast('error', err); return; }
    const dept = departments.find((d) => String(d.departmentId) === form.departmentId);
    previewPayload.current = buildPayload(itemType, title, form);
    previewCtx.current = { leaderName: dept?.leaderName ?? 'Trưởng phòng' };
    setPreview((p) => ({ ...p, open: true, loading: true, error: null }));
    await fetchPreviewTemplate(itemType, title, form, dept?.leaderName ?? 'Trưởng phòng');
  };

  const fetchPreviewTemplate = async (itemType: LogisticsItemType, title: string, form: ResourceForm, leaderName: string) => {
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode: 'LOGISTICS_REQUEST_TO_DEPARTMENT',
        context: {
          departmentLeaderName: leaderName,
          requesterName: hostName,
          DelegationName: delegationName,
          CampusName: campusName,
          logisticsTitle: title.trim(),
          itemType: ITEM_TYPE_LABEL[itemType] ?? itemType,
          quantity: form.quantity || '—',
          usageStartAt: combine(form.date, form.startTime) || '—',
          usageEndAt: combine(form.date, form.endTime) || '—',
        },
      });
      setPreview((p) => ({
        ...p, open: true, loading: false, error: null,
        subject: res.subject, body: res.bodyHtml,
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
    void fetchPreviewTemplate(pl.itemType, pl.title, {
      quantity: pl.quantity != null ? String(pl.quantity) : '',
      date: (pl.usageStartAt ?? '').slice(0, 10),
      startTime: (pl.usageStartAt ?? '').slice(11, 16),
      endTime: (pl.usageEndAt ?? '').slice(11, 16),
      note: pl.description ?? '', departmentId: String(pl.departmentId), title: pl.title,
    }, ctx.leaderName);
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
        emailOverride: { useEditedContent: true, subject: preview.subject.trim(), bodyHtml: preview.body },
      });
      setPreview((p) => ({ ...p, open: false, sending: false }));
      pushToast(res.emailStatus === 'FAILED' ? 'warning' : 'success', res.message || 'Đã gửi yêu cầu hậu cần.');
      await loadList();
    } catch (e: any) {
      setPreview((p) => ({ ...p, sending: false }));
      pushToast('error', apiError(e, 'Không thể gửi yêu cầu hậu cần.'));
    }
  };

  const cardProps = {
    departments, canManage, busyKey, latestItemFor,
    onSend: sendDirect, onPreview: openPreview,
  };

  return (
    <div className="space-y-8">
      {/* Mục 1: Welcome LED */}
      <MucCard title="Mục 1: Welcome LED" icon={<MonitorPlay className="w-5 h-5 text-[#f37021]" />}
        open={openSection[1]} onToggle={() => toggleSection(1)}>
        <div className="space-y-4 mb-4">
          <label className="flex items-center gap-3 cursor-pointer">
            <input type="radio" name="needLED" checked={needLED === false} disabled={!canManage}
              onChange={() => setNeedLED(false)}
              className="w-5 h-5 rounded-full border-gray-300 text-[#004c91] focus:ring-[#004c91]" />
            <span className="text-[15px] font-bold text-gray-700">Không cần màn LED</span>
          </label>
          <label className="flex items-center gap-3 cursor-pointer">
            <input type="radio" name="needLED" checked={needLED === true} disabled={!canManage}
              onChange={() => setNeedLED(true)}
              className="w-5 h-5 rounded-full border-gray-300 text-[#004c91] focus:ring-[#004c91]" />
            <span className="text-[15px] font-bold text-gray-700">Cần màn LED</span>
          </label>
        </div>
        {needLED && (
          <div className="pt-4 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
            <ResourceCard
              {...cardProps}
              cardKey="led"
              icon={<MonitorPlay className="w-6 h-6 text-[#f37021]" />}
              label="Welcome LED"
              itemType="LED"
              qtyLabel="Số lượng màn"
              notePlaceholder="Kích thước, nội dung hiển thị, đã gửi ảnh thiết kế..."
            />
          </div>
        )}
      </MucCard>

      {/* Mục 2: Chuẩn bị cho Campus Tour */}
      <MucCard title="Mục 2: Chuẩn bị cho Campus Tour" icon={<MapPin className="w-5 h-5 text-[#f37021]" />}
        open={openSection[2]} onToggle={() => toggleSection(2)}>
        <div className="space-y-8">
          <ResourceCard {...cardProps} cardKey="electricCar" icon={<Car className="w-6 h-6 text-[#f37021]" />}
            label="Xe điện" itemType="TRANSPORT" qtyLabel="Số lượng cần mượn"
            notePlaceholder="Ghi chú thêm..." />
          <hr className="border-t-[2px] border-gray-200" />
          <ResourceCard {...cardProps} cardKey="driver" icon={<UserCheck className="w-6 h-6 text-[#f37021]" />}
            label="Người lái" itemType="TRANSPORT" qtyLabel="Số lượng"
            notePlaceholder="Yêu cầu về tài xế, thời gian hỗ trợ..." />
        </div>
      </MucCard>

      {/* Mục 3: Chuẩn bị cho họp */}
      <MucCard title="Mục 3: Chuẩn bị cho họp" icon={<Building2 className="w-5 h-5 text-[#f37021]" />}
        open={openSection[3]} onToggle={() => toggleSection(3)}>
        <div className="space-y-8">
          <ResourceCard {...cardProps} cardKey="room" icon={<Building2 className="w-6 h-6 text-[#f37021]" />}
            label="Phòng họp" itemType="ROOM" qtyLabel="Số phòng"
            notePlaceholder="Tên phòng / vị trí (VD: Tòa Alpha, P.101), layout, thiết bị..." />
          <hr className="border-t-[2px] border-gray-200" />
          <ResourceCard {...cardProps} cardKey="teabreak" icon={<Coffee className="w-6 h-6 text-[#f37021]" />}
            label="Teabreak" itemType="MEAL" qtyLabel="Số lượng (suất)"
            notePlaceholder="Layout, khăn trải bàn, biển tên, yêu cầu đặc biệt..." />
        </div>
      </MucCard>

      {/* Mục 4: Khác */}
      <MucCard title="Mục 4: Khác" icon={<MoreHorizontal className="w-5 h-5 text-[#f37021]" />}
        open={openSection[4]} onToggle={() => toggleSection(4)}>
        <ResourceCard {...cardProps} cardKey="other" icon={<MoreHorizontal className="w-6 h-6 text-[#f37021]" />}
          label="Yêu cầu khác" itemType="OTHER" qtyLabel="Số lượng" editableTitle
          notePlaceholder="Mô tả chi tiết công việc cần hỗ trợ..." />
      </MucCard>

      {/* Danh sách yêu cầu hậu cần thật (mọi yêu cầu đã gửi + trạng thái enum) */}
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
                      </div>
                      <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
                        {meta.label}
                      </span>
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

/** Collapsible "Mục" card — reproduces the original orange-accented header + animated body. */
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

interface ResourceCardProps {
  cardKey: string;
  icon: React.ReactNode;
  label: string;
  itemType: LogisticsItemType;
  qtyLabel: string;
  notePlaceholder?: string;
  editableTitle?: boolean;
  departments: SupportDepartment[];
  canManage: boolean;
  busyKey: string | null;
  latestItemFor: (title: string) => VisitInstanceLogisticsItem | null;
  onSend: (key: string, itemType: LogisticsItemType, title: string, form: ResourceForm, onDone: () => void) => void;
  onPreview: (itemType: LogisticsItemType, title: string, form: ResourceForm) => void;
}

/** One resource request form, styled like the original setup sub-card, wired to the real API. */
function ResourceCard({
  cardKey, icon, label, itemType, qtyLabel, notePlaceholder, editableTitle,
  departments, canManage, busyKey, latestItemFor, onSend, onPreview,
}: ResourceCardProps) {
  const [form, setForm] = useState<ResourceForm>(() => ({ ...emptyForm(), title: editableTitle ? '' : label }));
  const set = (k: keyof ResourceForm, v: string) => setForm((f) => ({ ...f, [k]: v }));
  const reset = () => setForm({ ...emptyForm(), title: editableTitle ? '' : label });

  const title = editableTitle ? form.title : label;
  const busy = busyKey === cardKey;
  const dept = departments.find((d) => String(d.departmentId) === form.departmentId);
  const existing = latestItemFor(title);
  const meta = existing ? (LOGISTICS_STATUS_META[existing.status] ?? null) : null;
  const disabled = !canManage;

  return (
    <div>
      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2">
        {icon} {label}
        {meta && (
          <span className={`ml-2 inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>
            {meta.label}
          </span>
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
                <input type="text" maxLength={255} className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400"
                  placeholder="VD: Hỗ trợ kỹ thuật âm thanh" disabled={disabled} value={form.title} onChange={(e) => set('title', e.target.value)} />
              </div>
            )}
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">{qtyLabel}</label>
              <input type="number" min="1" className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400"
                placeholder="VD: 2" disabled={disabled} value={form.quantity} onChange={(e) => set('quantity', e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian sử dụng</label>
              <div className="flex flex-col sm:flex-row gap-2 w-full">
                <div className="relative flex-1">
                  <input type="date" className="w-full px-3 py-2 pl-9 rounded-lg border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400"
                    disabled={disabled} value={form.date} onChange={(e) => set('date', e.target.value)} />
                  <Calendar className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[#004c91] pointer-events-none" />
                </div>
                <div className="flex items-center gap-2 flex-1">
                  <input type="time" className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400"
                    disabled={disabled} value={form.startTime} onChange={(e) => set('startTime', e.target.value)} />
                  <span className="text-gray-400 font-bold text-xs uppercase shrink-0">Đến</span>
                  <input type="time" className="flex-1 w-full min-w-[80px] px-2 py-2 rounded-lg border border-gray-300 text-sm hover:border-gray-400 focus:border-[#004c91] transition-colors outline-none disabled:bg-gray-50 disabled:text-gray-400"
                    disabled={disabled} value={form.endTime} onChange={(e) => set('endTime', e.target.value)} />
                </div>
              </div>
            </div>
          </div>
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú (Note)</label>
              <textarea className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm resize-none h-[100px] disabled:bg-gray-50 disabled:text-gray-400"
                placeholder={notePlaceholder ?? 'Ghi chú thêm...'} disabled={disabled} value={form.note} onChange={(e) => set('note', e.target.value)} />
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Chọn phòng ban xử lý <span className="text-red-500">*</span></label>
              <select className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-[#004c91] transition-colors outline-none text-sm bg-white disabled:bg-gray-50 disabled:text-gray-400"
                value={form.departmentId} disabled={disabled} onChange={(e) => set('departmentId', e.target.value)}>
                <option value="">-- Chọn phòng ban --</option>
                {departments.map((d) => (
                  <option key={d.departmentId} value={d.departmentId} disabled={!d.canInvite}>
                    {d.departmentName}{!d.canInvite ? ' — không khả dụng' : ''}
                  </option>
                ))}
              </select>

              {/* Trưởng phòng + hành động — chỉ hiện khi đã chọn phòng ban (thay cho renderLeaderInfo cũ). */}
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
                      <button type="button" disabled={busy} onClick={() => onPreview(itemType, title, form)}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-yellow-300 bg-white px-3 py-2 text-xs font-bold text-[#004c91] outline-none transition-colors hover:bg-yellow-100 disabled:opacity-50">
                        <Eye className="w-4 h-4" /> Xem trước & sửa email
                      </button>
                      <button type="button" disabled={busy} onClick={() => onSend(cardKey, itemType, title, form, reset)}
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
      </div>
    </div>
  );
}
