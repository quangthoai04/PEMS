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
import { motion, AnimatePresence } from 'motion/react';
import {
  Loader2, Send, Eye, Plus, Trash2, ChevronUp, ChevronDown, CheckCircle, CheckCircle2, AlertCircle, X,
  MonitorPlay, MapPin, Building2, MoreHorizontal, Car, UserCheck, Coffee, History, Mail,
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
// Statuses where the department has taken the item → host can no longer cancel/replace it.
const LOCKED_STATUSES = new Set(['ASSIGNED', 'ACCEPTED', 'IN_PROGRESS']);

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
  visitInstanceId, relation, instanceStatus, delegationName, campusName, hostName, pushToast,
}: Props) {
  const canManage = relation === 'HOST' && (instanceStatus === 'ASSIGNED' || instanceStatus === 'BEFORE_VISIT');

  const [departments, setDepartments] = useState<SupportDepartment[]>([]);
  const [items, setItems] = useState<VisitInstanceLogisticsItem[]>([]);
  const [loadedOnce, setLoadedOnce] = useState(false);
  const [loadingList, setLoadingList] = useState(false);
  const [busyKey, setBusyKey] = useState<string | null>(null);

  const [openSection, setOpenSection] = useState<Record<number, boolean>>({ 1: true, 2: true, 3: true, 4: true });
  const toggleSection = (n: number) => setOpenSection((p) => ({ ...p, [n]: !p[n] }));

  // Welcome LED: which create-form to show WHEN there is no active LED item yet.
  const [ledChoice, setLedChoice] = useState<'none' | 'system' | 'offline'>('none');

  // Mục 4 "Khác": dynamic list of independent create-cards (Part F).
  const otherIdRef = useRef(1);
  const [otherIds, setOtherIds] = useState<number[]>([1]);

  const [preview, setPreview] = useState({
    open: false, loading: false, sending: false, restoring: false, error: null as string | null,
    subject: '', body: '', isActionTemplate: false,
    systemActionDescription: null as string | null, lockedActionBlockHtml: null as string | null,
    recipient: null as EmailPreviewRecipient | null,
  });
  const previewPayload = useRef<PrepareVisitLogisticsPayload | null>(null);
  const previewCtx = useRef<{ leaderName: string } | null>(null);
  const previewResetRef = useRef<(() => void) | null>(null);

  // "Xem mail đã gửi" history modal — bound to one logistics item at a time.
  const [sentModal, setSentModal] = useState<{ open: boolean; item: VisitInstanceLogisticsItem | null }>(
    { open: false, item: null });
  const openSentEmails = (item: VisitInstanceLogisticsItem) => setSentModal({ open: true, item });
  const closeSentEmails = () => setSentModal((s) => ({ ...s, open: false }));

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
  const activeLedItem = items.find((i) => i.itemType === 'LED' && isActive(i)) ?? null;

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

  const cancelItem = async (key: string, item: VisitInstanceLogisticsItem) => {
    setBusyKey(key);
    try {
      const res = await delegationsApi.cancelLogisticsItem(visitInstanceId, item.logisticsItemId);
      pushToast('success', res.message || 'Đã hủy yêu cầu hậu cần.');
      await loadList();
    } catch (e: any) {
      pushToast('error', apiError(e, 'Không thể hủy yêu cầu hậu cần.'));
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
    onSubmit: submitRequest, onPreview: openPreview, onCancel: cancelItem, pushToast,
  };

  return (
    <div className="space-y-8">
      {/* Mục 1: Welcome LED — 3 lựa chọn (Part D); state suy ra từ logistics item đã lưu (Part 1/2) */}
      <MucCard title="Mục 1: Welcome LED" icon={<MonitorPlay className="w-5 h-5 text-[#f37021]" />}
        open={openSection[1]} onToggle={() => toggleSection(1)}>
        {!loadedOnce ? (
          <LoadingRow />
        ) : activeLedItem ? (
          activeLedItem.coordinationMode === 'OFFLINE_COORDINATED' ? (
            <div className="pt-4 border-t border-gray-100">
              <OfflineCard {...shared} cardKey="led-offline" itemType="LED" title="Welcome LED" label="Welcome LED (trao đổi bên ngoài)" 
                existingItem={activeLedItem} onCancel={() => cancelItem('led-offline', activeLedItem)} />
            </div>
          ) : (
            <div className="pt-4 border-t border-gray-100">
              <ResourceCard {...shared} cardKey="led" icon={<MonitorPlay className="w-6 h-6 text-[#f37021]" />}
                label="Welcome LED" itemType="LED" qtyLabel="Số lượng màn" existingItem={activeLedItem}
                notePlaceholder="Kích thước, nội dung hiển thị, đã gửi ảnh thiết kế..." />
            </div>
          )
        ) : (
          <>
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
                  label="Welcome LED" itemType="LED" qtyLabel="Số lượng màn" existingItem={null}
                  notePlaceholder="Kích thước, nội dung hiển thị, đã gửi ảnh thiết kế..." />
              </div>
            )}
            {ledChoice === 'offline' && (
              <div className="pt-4 border-t border-gray-100 animate-in fade-in slide-in-from-top-2">
                <OfflineCard {...shared} cardKey="led-offline" itemType="LED" title="Welcome LED" label="Welcome LED (trao đổi bên ngoài)" />
              </div>
            )}
          </>
        )}
      </MucCard>

      {/* Mục 2: Chuẩn bị cho Campus Tour */}
      <MucCard title="Mục 2: Chuẩn bị cho Campus Tour" icon={<MapPin className="w-5 h-5 text-[#f37021]" />}
        open={openSection[2]} onToggle={() => toggleSection(2)}>
        <div className="space-y-8">
          <ResourceCard {...shared} cardKey="electricCar" icon={<Car className="w-6 h-6 text-[#f37021]" />}
            label="Xe điện" itemType="TRANSPORT" qtyLabel="Số lượng cần mượn" existingItem={activeItem('TRANSPORT', 'Xe điện')}
            notePlaceholder="Ghi chú thêm..." />
          <hr className="border-t-[2px] border-gray-200" />
          <ResourceCard {...shared} cardKey="driver" icon={<UserCheck className="w-6 h-6 text-[#f37021]" />}
            label="Người lái" itemType="TRANSPORT" qtyLabel="Số lượng" existingItem={activeItem('TRANSPORT', 'Người lái')}
            notePlaceholder="Yêu cầu về tài xế, thời gian hỗ trợ..." />
        </div>
      </MucCard>

      {/* Mục 3: Chuẩn bị cho họp */}
      <MucCard title="Mục 3: Chuẩn bị cho họp" icon={<Building2 className="w-5 h-5 text-[#f37021]" />}
        open={openSection[3]} onToggle={() => toggleSection(3)}>
        <div className="space-y-8">
          <ResourceCard {...shared} cardKey="room" icon={<Building2 className="w-6 h-6 text-[#f37021]" />}
            label="Phòng họp" itemType="ROOM" qtyLabel="Số phòng" existingItem={activeItem('ROOM', 'Phòng họp')}
            notePlaceholder="Tên phòng / vị trí (VD: Tòa Alpha, P.101), layout, thiết bị..." />
          <hr className="border-t-[2px] border-gray-200" />
          <ResourceCard {...shared} cardKey="teabreak" icon={<Coffee className="w-6 h-6 text-[#f37021]" />}
            label="Teabreak" itemType="MEAL" qtyLabel="Số lượng (suất)" existingItem={activeItem('MEAL', 'Teabreak')}
            notePlaceholder="Layout, khăn trải bàn, biển tên, yêu cầu đặc biệt..." />
        </div>
      </MucCard>

      {/* Mục 4: Khác — thêm nhiều yêu cầu (Part F); create-only, hiển thị trong danh sách bên dưới */}
      <MucCard title="Mục 4: Khác" icon={<MoreHorizontal className="w-5 h-5 text-[#f37021]" />}
        open={openSection[4]} onToggle={() => toggleSection(4)}>
        <div className="space-y-6">
          {otherIds.map((id, idx) => (
            <div key={id} className={idx > 0 ? 'pt-6 border-t border-gray-100' : ''}>
              <ResourceCard {...shared} cardKey={`other-${id}`} icon={<MoreHorizontal className="w-6 h-6 text-[#f37021]" />}
                label={`Yêu cầu khác ${otherIds.length > 1 ? `#${idx + 1}` : ''}`.trim()} itemType="OTHER"
                qtyLabel="Số lượng" editableTitle existingItem={null} notePlaceholder="Mô tả chi tiết công việc cần hỗ trợ..."
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
          {!loadedOnce ? (
            <LoadingRow />
          ) : items.length === 0 ? (
            <p className="py-2 text-sm italic text-slate-400">Chưa có yêu cầu hậu cần nào.</p>
          ) : (
            <div className="space-y-2">
              {items.map((it) => (
                <LogisticsListRow
                  key={it.logisticsItemId}
                  it={it}
                  canManage={canManage}
                  busy={busyKey === `proposal-${it.logisticsItemId}`}
                  onRespond={respondToProposal}
                  onViewSent={openSentEmails}
                />
              ))}
            </div>
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
    </div>
  );
}

function LoadingRow() {
  return <div className="flex items-center gap-2 py-3 text-sm text-gray-500"><Loader2 className="w-4 h-4 animate-spin" /> Đang tải...</div>;
}

/** Collapsible "Mục" card — original orange-accented header + animated body. */
function MucCard({ title, icon, open, onToggle, children }: {
  title: string; icon: React.ReactNode; open: boolean; onToggle: () => void; children: React.ReactNode;
}) {
  return (
    <div className="bg-white border border-gray-200 rounded-2xl shadow-sm">
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
  visitInstanceId, departments, canManage, busyKey, loadedOnce, onSubmit, onPreview, onCancel, pushToast,
}: ResourceCardProps) {
  const [form, setForm] = useState<ResourceForm>(() => {
    if (existingItem) {
      return {
        title: existingItem.title || label,
        quantity: existingItem.quantity?.toString() || '',
        usageStartAt: existingItem.usageStartAt ? existingItem.usageStartAt.slice(0, 16) : '',
        usageEndAt: existingItem.usageEndAt ? existingItem.usageEndAt.slice(0, 16) : '',
        note: existingItem.description || '',
        departmentId: existingItem.requestedToDepartmentId?.toString() || '',
      };
    }
    return emptyForm(editableTitle ? '' : label);
  });
  const [err, setErr] = useState<string | null>(null);
  const [localSubmitted, setLocalSubmitted] = useState(false);
  const set = (k: keyof ResourceForm, v: string) => { setForm((f) => ({ ...f, [k]: v })); setErr(null); };
  const reset = () => { setForm(emptyForm(editableTitle ? '' : label)); setErr(null); setLocalSubmitted(false); };

  useEffect(() => {
    if (existingItem) {
      setForm({
        title: existingItem.title || label,
        quantity: existingItem.quantity?.toString() || '',
        usageStartAt: existingItem.usageStartAt ? existingItem.usageStartAt.slice(0, 16) : '',
        usageEndAt: existingItem.usageEndAt ? existingItem.usageEndAt.slice(0, 16) : '',
        note: existingItem.description || '',
        departmentId: existingItem.requestedToDepartmentId?.toString() || '',
      });
      setErr(null);
      setLocalSubmitted(true);
    } else {
      setLocalSubmitted(false);
    }
  }, [existingItem, label]);

  const busy = busyKey === cardKey;
  // If departmentId is set in form, find it. If it was already submitted but the API didn't return the full department object, we fallback to showing the departmentName from the item if available.
  const dept = departments.find((d) => String(d.departmentId) === form.departmentId) || (existingItem && existingItem.departmentName ? { departmentId: Number(form.departmentId), departmentName: existingItem.departmentName, leaderName: '', leaderEmail: '', canInvite: true } as SupportDepartment : undefined);
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

    const nowStr = new Date(Date.now() - new Date().getTimezoneOffset() * 60000).toISOString().slice(0, 16);
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
    priority: 'MEDIUM',
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
    <div>
      <h4 className="text-lg font-bold text-[#004c91] mb-3 flex items-center gap-2 flex-wrap">
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

        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div className="space-y-4">
            {editableTitle && (
              <div>
                <label className="block text-xs font-bold text-gray-600 mb-1">Tiêu đề / nội dung công việc <span className="text-red-500">*</span></label>
                <input type="text" maxLength={255} disabled={isFormDisabled} value={form.title} onChange={(e) => set('title', e.target.value)}
                  placeholder="VD: Hỗ trợ kỹ thuật âm thanh"
                  className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
              </div>
            )}
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">{qtyLabel} <span className="font-normal text-gray-400">(dự kiến)</span></label>
              <input type="text" inputMode="numeric" disabled={isFormDisabled} value={form.quantity} onChange={(e) => handleQuantityChange(e.target.value)}
                placeholder="VD: 2 (có thể điều chỉnh sau khi phòng ban phản hồi)"
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian bắt đầu sử dụng <span className="text-red-500">*</span></label>
              <input type="datetime-local" disabled={isFormDisabled} value={form.usageStartAt} onChange={(e) => set('usageStartAt', e.target.value)}
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Thời gian kết thúc sử dụng <span className="text-red-500">*</span></label>
              <input type="datetime-local" disabled={isFormDisabled} value={form.usageEndAt} onChange={(e) => set('usageEndAt', e.target.value)}
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
          </div>
          <div className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú (Note)</label>
              <textarea disabled={isFormDisabled} value={form.note} onChange={(e) => set('note', e.target.value)} placeholder={notePlaceholder ?? 'Ghi chú thêm...'}
                className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm resize-none h-[120px] disabled:bg-gray-50 disabled:text-gray-400" />
            </div>
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
                        if (!d.canInvite || isFormDisabled) return;
                        set('departmentId', d.departmentId.toString());
                        close();
                      }}
                      className={`flex items-center justify-between gap-3 border-b border-gray-100 px-4 py-2.5 last:border-b-0 transition-colors ${d.canInvite && !isFormDisabled ? 'cursor-pointer hover:bg-[#f0f7ff]' : 'opacity-60 cursor-not-allowed'}`}
                    >
                      <div className="min-w-0">
                        <div className="truncate text-sm font-bold text-gray-800">{d.departmentName}</div>
                        <div className="truncate text-xs text-gray-500">
                          {d.leaderName ? `Trưởng phòng: ${d.leaderName}` : 'Chưa có trưởng phòng đang hoạt động'}
                          {d.leaderEmail ? ` · ${d.leaderEmail}` : ''}
                        </div>
                        {!d.canInvite && d.disabledReason && (
                          <div className="mt-0.5 text-[11px] font-medium text-amber-600">{d.disabledReason}</div>
                        )}
                      </div>
                      {d.canInvite && (
                        <button type="button" className="inline-flex shrink-0 items-center gap-1 rounded-lg border border-[#004c91] px-2 py-1 text-xs font-bold text-[#004c91]">
                          Chọn
                        </button>
                      )}
                    </div>
                  )}
                />
              ) : null}

              {form.departmentId && dept && (
                <div className="mt-3 p-4 bg-white border border-gray-200 rounded-xl flex flex-col gap-3 animate-in fade-in slide-in-from-top-2 shadow-sm">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <div className="truncate text-sm font-bold text-gray-800">{dept.departmentName}</div>
                        <button type="button" onClick={() => set('departmentId', '')} className="text-xs text-[#004c91] hover:underline font-semibold">
                          Đổi phòng ban
                        </button>
                      </div>
                      <div className="flex items-center gap-1 text-[11px] font-bold text-gray-500 uppercase tracking-wider mt-1">
                        <Building2 className="w-3 h-3 shrink-0" />
                        <span>Phòng ban: GENERAL</span>
                      </div>
                      <div className="flex items-center gap-1 text-xs text-gray-600 mt-1">
                        <UserCheck className="w-3.5 h-3.5 shrink-0 text-gray-400" />
                        <span>Trưởng phòng: <span className="font-semibold">{dept.leaderName || 'Chưa có'}</span></span>
                      </div>
                      <div className="flex items-center gap-1 text-xs text-gray-600 mt-0.5">
                        <Mail className="w-3.5 h-3.5 shrink-0 text-gray-400" />
                        {dept.leaderEmail ? <span className="break-all">{dept.leaderEmail}</span> : <span className="font-semibold text-red-500">Chưa có email</span>}
                      </div>
                      {!dept.canInvite && dept.disabledReason && (
                        <div className="mt-1.5 text-[11px] font-medium text-amber-600 flex items-center gap-1">
                          <AlertCircle className="w-3 h-3" /> {dept.disabledReason}
                        </div>
                      )}
                    </div>
                    {canManage && (
                      <div className="flex flex-col items-end gap-1.5 shrink-0">
                        {dept.canInvite && (
                          <button type="button" disabled={busy} onClick={doPreview}
                            className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200 bg-white text-[#004c91] outline-none transition-colors hover:bg-gray-50 disabled:opacity-50"
                            title="Xem trước & sửa email">
                            <Eye className="w-3.5 h-3.5" />
                          </button>
                        )}
                        <button type="button" disabled={!dept.canInvite || busy} onClick={doSend}
                          className="inline-flex items-center gap-1 rounded-lg bg-[#004c91] px-3 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-[#003b70] disabled:cursor-not-allowed disabled:opacity-40"
                          title={!dept.canInvite ? 'Không thể gửi yêu cầu' : undefined}>
                          {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                          Gửi yêu cầu
                        </button>
                      </div>
                    )}
                  </div>
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
  /** Canonical DB title for the category (e.g. 'Welcome LED'). Defaults to `label`. Kept distinct so
   * the persisted title matches the system-request item of the same category — both the FE active-item
   * lookup and the server-side duplicate guard key on (itemType, title). */
  title?: string;
  existingItem?: VisitInstanceLogisticsItem | null;
}

/** "Đã trao đổi bên ngoài" form (Part D) — required note, optional department, NO email; status DONE. */
function OfflineCard({
  cardKey, itemType, label, title, visitInstanceId, departments, canManage, busyKey, onSubmit, existingItem, onCancel, pushToast,
}: OfflineCardProps) {
  const [note, setNote] = useState(() => existingItem?.offlineCoordinationNote || existingItem?.description || '');
  const [departmentId, setDepartmentId] = useState(() => existingItem?.requestedToDepartmentId?.toString() || '');
  const [err, setErr] = useState<string | null>(null);
  const [localSubmitted, setLocalSubmitted] = useState(false);
  const busy = busyKey === cardKey;

  useEffect(() => {
    if (existingItem) {
      setNote(existingItem.offlineCoordinationNote || existingItem.description || '');
      setDepartmentId(existingItem.requestedToDepartmentId?.toString() || '');
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
    if (!note.trim()) { const m = 'Vui lòng nhập ghi chú trao đổi bên ngoài (bắt buộc).'; setErr(m); pushToast('error', m); return; }
    const payload: PrepareVisitLogisticsPayload = {
      visitInstanceId,
      departmentId: departmentId ? Number(departmentId) : null,
      itemType,
      title: (title ?? label).trim(),
      description: note.trim(),
      coordinationMode: 'OFFLINE_COORDINATED',
      offlineCoordinationNote: note.trim(),
      priority: 'MEDIUM',
    };
    if (await onSubmit(cardKey, payload)) {
      setLocalSubmitted(true);
      setErr(null);
    }
  };

  return (
    <div className="flex flex-col gap-4 p-5 bg-amber-50/40 border border-amber-200 rounded-xl shadow-sm">
      <div className="flex items-center gap-2 text-sm font-bold text-amber-800">
        <AlertCircle className="w-4 h-4" /> Đã trao đổi/xử lý bên ngoài hệ thống — chỉ lưu dấu vết, không gửi email.
      </div>
      <div>
        <label className="block text-xs font-bold text-gray-600 mb-1">Ghi chú trao đổi bên ngoài <span className="text-red-500">*</span></label>
        <textarea disabled={isFormDisabled} value={note} onChange={(e) => { setNote(e.target.value); setErr(null); }} maxLength={5000}
          placeholder="VD: Đã liên hệ trực tiếp phòng Truyền thông qua điện thoại, ảnh LED gửi qua email nội bộ..."
          className="w-full px-3 py-2 rounded-lg border border-gray-300 focus:border-[#004c91] hover:border-gray-400 transition-colors outline-none text-sm resize-none h-[100px] disabled:bg-gray-50 disabled:text-gray-400" />
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
      {err && (
        <p className="flex items-center gap-1.5 text-xs font-semibold text-red-600">
          <AlertCircle className="w-3.5 h-3.5 shrink-0" /> {err}
        </p>
      )}
      
      {isSubmitted ? (
        <div className="flex items-center justify-end gap-3 mt-2">
          <button type="button" disabled
            className="inline-flex items-center gap-1.5 rounded-xl bg-gray-100 px-4 py-2.5 text-sm font-bold text-gray-500 outline-none">
            <CheckCircle className="w-4 h-4" /> Đã lưu yêu cầu
          </button>
          {canManage && existingItem && !locked && onCancel && (
            <button type="button" disabled={busy} onClick={() => onCancel(cardKey, existingItem)}
              className="inline-flex items-center gap-1.5 rounded-xl border border-red-200 bg-white px-4 py-2.5 text-sm font-bold text-red-600 outline-none transition-colors hover:bg-red-50 disabled:opacity-50">
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <Trash2 className="w-4 h-4" />} Hủy yêu cầu
            </button>
          )}
        </div>
      ) : (
        <div className="flex items-center justify-end mt-2">
          {canManage && (
            <button type="button" disabled={busy} onClick={doSave}
              className="inline-flex items-center gap-1.5 rounded-xl bg-[#004c91] px-6 py-2.5 text-sm font-bold text-white outline-none transition-colors hover:bg-[#003d73] disabled:opacity-50">
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : <CheckCircle className="w-4 h-4" />} Lưu (đã trao đổi bên ngoài)
            </button>
          )}
        </div>
      )}
    </div>
  );
}

/** Final ("chốt") quantity: the proposed figure once the Host ACCEPTED, else the planned figure. */
function finalQuantity(it: VisitInstanceLogisticsItem): number | null | undefined {
  return it.proposalResponse === 'ACCEPTED' && it.proposedQuantity != null ? it.proposedQuantity : it.quantity;
}

/** One row of the logistics request list. Shows planned/proposed/final quantity and, for a pending
 * department change proposal (status CHANGE_PROPOSED), the Host's accept/reject controls. */
function LogisticsListRow({ it, canManage, busy, onRespond, onViewSent }: {
  it: VisitInstanceLogisticsItem;
  canManage: boolean;
  busy: boolean;
  onRespond: (item: VisitInstanceLogisticsItem, accepted: boolean, note: string) => void;
  onViewSent: (item: VisitInstanceLogisticsItem) => void;
}) {
  const meta = LOGISTICS_STATUS_META[it.status] ?? { label: it.status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
  const offline = it.coordinationMode === 'OFFLINE_COORDINATED';
  const proposed = it.status === 'CHANGE_PROPOSED';
  const finalQty = finalQuantity(it);
  const [rejecting, setRejecting] = useState(false);
  const [rejectNote, setRejectNote] = useState('');

  return (
    <div className={`rounded-xl border p-3 shadow-sm ${it.status === 'CANCELLED' ? 'border-gray-200 bg-gray-50 opacity-70' : proposed ? 'border-violet-200 bg-violet-50/30' : 'border-gray-200 bg-white'}`}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="truncate text-sm font-bold text-gray-800">{it.title}</div>
          <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-xs text-gray-500">
            <span>{ITEM_TYPE_LABEL[it.itemType] ?? it.itemType}</span>
            {it.quantity != null && <span>SL dự kiến: {it.quantity}</span>}
            {it.proposedQuantity != null && <span className="font-semibold text-violet-700">đề xuất: {it.proposedQuantity}</span>}
            {it.proposalResponse && finalQty != null && <span className="font-semibold text-emerald-700">chốt: {finalQty}</span>}
            {it.departmentName && <span>Phòng ban: {it.departmentName}</span>}
            {it.assignedToName && <span>Nhân sự: {it.assignedToName}</span>}
            {(it.usageStartAt || it.usageEndAt) && <span>{fmtDateTime(it.usageStartAt)} – {fmtDateTime(it.usageEndAt)}</span>}
          </div>
          {offline && it.offlineCoordinationNote && (
            <div className="mt-1 text-[11px] italic text-amber-700">Ghi chú: {it.offlineCoordinationNote}</div>
          )}
        </div>
        <div className="flex flex-col items-end gap-1">
          <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>{meta.label}</span>
          {it.coordinationMode && (
            <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[10px] font-bold ${offline ? 'border-amber-200 bg-amber-50 text-amber-700' : 'border-slate-200 bg-slate-50 text-slate-500'}`}>
              {COORD_LABEL[it.coordinationMode]}
            </span>
          )}
          {it.coordinationMode === 'SYSTEM_REQUEST' && (
            <button type="button" onClick={() => onViewSent(it)}
              className="mt-0.5 inline-flex h-7 items-center gap-1 rounded-lg border border-gray-200 bg-white px-2 text-[11px] font-bold text-[#004c91] outline-none transition-colors hover:bg-gray-50">
              <History className="w-3.5 h-3.5" /> Mail đã gửi
            </button>
          )}
        </div>
      </div>

      {/* Department change proposal — Host reviews planned vs proposed and accepts/rejects. */}
      {proposed && (
        <div className="mt-3 rounded-lg border border-violet-200 bg-white p-3">
          <div className="text-[11px] font-bold uppercase tracking-wide text-violet-700">Phòng ban đề xuất thay đổi</div>
          <div className="mt-1 grid grid-cols-1 gap-0.5 text-xs text-gray-700">
            {it.proposedQuantity != null && <div>Số lượng đề xuất: <b>{it.proposedQuantity}</b> (dự kiến: {it.quantity ?? '—'})</div>}
            {(it.proposedUsageStartAt || it.proposedUsageEndAt) && <div>Thời gian đề xuất: {fmtDateTime(it.proposedUsageStartAt)} – {fmtDateTime(it.proposedUsageEndAt)}</div>}
            {it.proposedDescription && <div>Nội dung đề xuất: {it.proposedDescription}</div>}
            {it.proposalNote && <div className="italic text-violet-800">Lý do: {it.proposalNote}</div>}
          </div>
          {canManage && (!rejecting ? (
            <div className="mt-2 flex items-center gap-2">
              <button type="button" disabled={busy} onClick={() => onRespond(it, true, '')}
                className="inline-flex items-center gap-1 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-bold text-white outline-none hover:bg-emerald-700 disabled:opacity-50">
                {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />} Chấp nhận đề xuất
              </button>
              <button type="button" disabled={busy} onClick={() => setRejecting(true)}
                className="inline-flex items-center gap-1 rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-bold text-red-600 outline-none hover:bg-red-50 disabled:opacity-50">
                <X className="w-3.5 h-3.5" /> Từ chối đề xuất
              </button>
            </div>
          ) : (
            <div className="mt-2 space-y-2">
              <textarea value={rejectNote} onChange={(e) => setRejectNote(e.target.value)} maxLength={1000}
                placeholder="Lý do từ chối đề xuất (bắt buộc)..."
                className="w-full h-[70px] resize-none rounded-lg border border-gray-300 px-3 py-2 text-xs outline-none focus:border-red-400" />
              <div className="flex items-center justify-end gap-2">
                <button type="button" disabled={busy} onClick={() => { setRejecting(false); setRejectNote(''); }}
                  className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-xs font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-50">Hủy</button>
                <button type="button" disabled={busy || !rejectNote.trim()} onClick={() => onRespond(it, false, rejectNote.trim())}
                  className="inline-flex items-center gap-1 rounded-lg bg-red-600 px-3 py-1.5 text-xs font-bold text-white outline-none hover:bg-red-700 disabled:opacity-50">
                  {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <X className="w-3.5 h-3.5" />} Xác nhận từ chối
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
