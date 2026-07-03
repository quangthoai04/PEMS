/**
 * VisitProcess "Đang / Sau tiếp khách" — borrow/return handover signing for logistics items.
 *
 * Model: the instance Host signs the BORROWER side (bên nhận); the Department signs the PROVIDER side
 * (bên giao) from its own portal. Both signatures live on one visit_logistics_item_handovers row
 * (unique per item + type). RETURN can only be signed once BORROW is fully signed by both sides.
 * The borrower RETURN signature closes the item (status DONE). All wired to the real handover API.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { Loader2, PackageCheck, Undo2, CheckCircle2, AlertCircle, PenLine, Clock, X } from 'lucide-react';
import { delegationsApi } from '../api/delegationsApi';
import {
  LOGISTICS_STATUS_META,
  type LogisticsHandover,
  type LogisticsHandoverType,
  type LogisticsItemCondition,
  type VisitInstanceLogisticsItem,
} from '../types/delegations.types';

type ToastFn = (type: 'success' | 'error' | 'warning' | 'info', msg: string) => void;

interface Props {
  visitInstanceId?: number;
  /** Host && not read-only — only then can the borrower side be signed. */
  canManage: boolean;
  pushToast?: ToastFn;
}

const CONDITION_OPTIONS: { value: LogisticsItemCondition; label: string }[] = [
  { value: 'GOOD', label: 'Tốt' },
  { value: 'DAMAGED', label: 'Hư hỏng' },
  { value: 'MISSING', label: 'Thiếu / mất' },
  { value: 'OTHER', label: 'Khác' },
];
// Conditions that require a note (mirrors HandoverItemConditions.RequireNote on the backend).
const CONDITION_REQUIRES_NOTE = new Set<LogisticsItemCondition>(['DAMAGED', 'MISSING', 'OTHER']);
// Items eligible for a handover: a system request the department has granted / is using / has returned.
const HANDOVER_STATUSES = new Set(['ACCEPTED', 'IN_PROGRESS', 'DONE']);

// "yyyy-MM-ddTHH:mm[:ss]" → "HH:mm dd/MM/yyyy" by pure string slicing (no Date / no TZ shift).
function fmtDateTime(value?: string | null): string {
  if (!value) return '—';
  const [d, t] = value.replace(' ', 'T').split('T');
  if (!d) return value;
  const [y, m, day] = d.split('-');
  const hm = (t || '').slice(0, 5);
  if (!y || !m || !day) return value;
  return hm ? `${hm} ${day}/${m}/${y}` : `${day}/${m}/${y}`;
}

function apiError(e: any, fallback: string): string {
  const data = e?.response?.data;
  if (typeof data === 'string' && data.trim()) return data;
  if (data?.message) return data.message;
  return fallback;
}

const find = (hs: LogisticsHandover[] | undefined, t: LogisticsHandoverType) =>
  hs?.find((h) => h.handoverType === t) ?? null;
const fullySigned = (h: LogisticsHandover | null) => !!(h && h.borrowerSignedAt && h.providerSignedAt);

export function LogisticsHandoverSection({ visitInstanceId, canManage, pushToast }: Props) {
  const [items, setItems] = useState<VisitInstanceLogisticsItem[]>([]);
  const [loadedOnce, setLoadedOnce] = useState(false);
  const [loading, setLoading] = useState(false);
  // Chọn một việc mượn/trả → mở modal biên bản để nhập tình trạng + ghi chú và ký.
  const [signTarget, setSignTarget] = useState<{ item: VisitInstanceLogisticsItem; type: LogisticsHandoverType } | null>(null);
  const [busy, setBusy] = useState(false);
  const [condition, setCondition] = useState<LogisticsItemCondition>('GOOD');
  const [note, setNote] = useState('');
  const [err, setErr] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!visitInstanceId) { setLoadedOnce(true); return; }
    setLoading(true);
    try {
      const res = await delegationsApi.getInstanceLogistics(visitInstanceId);
      setItems(res.items || []);
    } catch {
      setItems([]);
    } finally {
      setLoading(false);
      setLoadedOnce(true);
    }
  }, [visitInstanceId]);

  useEffect(() => { void load(); }, [load]);

  const eligible = items.filter(
    (i) => i.coordinationMode === 'SYSTEM_REQUEST' && HANDOVER_STATUSES.has(i.status),
  );

  const openSignModal = (item: VisitInstanceLogisticsItem, type: LogisticsHandoverType) => {
    setSignTarget({ item, type }); setCondition('GOOD'); setNote(''); setErr(null);
  };
  const closeSignModal = () => { if (!busy) { setSignTarget(null); setErr(null); } };

  const submit = async () => {
    if (!signTarget) return;
    if (CONDITION_REQUIRES_NOTE.has(condition) && !note.trim()) {
      const m = 'Vui lòng nhập ghi chú tình trạng tài sản.'; setErr(m); pushToast?.('error', m); return;
    }
    if (!visitInstanceId) return;
    setBusy(true);
    try {
      const res = await delegationsApi.signLogisticsHandoverBorrower(visitInstanceId, signTarget.item.logisticsItemId, {
        handoverType: signTarget.type, itemCondition: condition, note: note.trim() || null,
      });
      pushToast?.('success', res.message || 'Đã ký biên bản.');
      setSignTarget(null); setNote('');
      await load();
    } catch (e: any) {
      const m = apiError(e, 'Không thể ký biên bản. Vui lòng thử lại.');
      setErr(m); pushToast?.('error', m);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="bg-white border border-gray-200 rounded-2xl shadow-sm overflow-hidden">
      <div className="flex items-center justify-between px-6 py-4 bg-white border-b border-gray-100">
        <h3 className="text-xl font-bold text-orange-900 flex items-center gap-2">
          <div className="p-1.5 bg-orange-100 rounded-lg"><PackageCheck className="w-5 h-5 text-[#f37021]" /></div>
          Ký mượn / ký trả tài sản hậu cần
        </h3>
        {loading && <Loader2 className="w-4 h-4 animate-spin text-gray-400" />}
      </div>
      <div className="p-6 pt-4 space-y-4">
        {!loadedOnce ? (
          <div className="flex items-center gap-2 py-3 text-sm text-gray-500"><Loader2 className="w-4 h-4 animate-spin" /> Đang tải...</div>
        ) : eligible.length === 0 ? (
          <p className="py-2 text-sm italic text-slate-400">
            Chưa có hạng mục hậu cần nào đã được phòng ban tiếp nhận để ký biên bản mượn/trả.
          </p>
        ) : (
          eligible.map((it) => {
            const borrow = find(it.handovers, 'BORROW');
            const ret = find(it.handovers, 'RETURN');
            const meta = LOGISTICS_STATUS_META[it.status] ?? { label: it.status, cls: 'bg-slate-100 text-slate-600 border-slate-200' };
            const canReturn = fullySigned(borrow);
            return (
              <div key={it.logisticsItemId} className="rounded-xl border border-gray-200 p-4 shadow-sm">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="min-w-0">
                    <div className="text-sm font-bold text-gray-800">{it.title}</div>
                    <div className="truncate text-xs text-gray-500">
                      {[
                        it.departmentName,
                        it.quantity != null ? `SL: ${it.quantity}` : null,
                        it.assignedToName ? `Người xử lý: ${it.assignedToName}` : null,
                      ].filter(Boolean).join(' • ')}
                    </div>
                  </div>
                  <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>{meta.label}</span>
                </div>

                <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3">
                  <HandoverBlock
                    title="Biên bản mượn / bàn giao" icon={<PackageCheck className="w-4 h-4" />}
                    handover={borrow}
                    canSign={canManage}
                    onOpen={() => openSignModal(it, 'BORROW')}
                    signLabel="Bên nhận ký mượn"
                  />
                  <HandoverBlock
                    title="Biên bản trả / nhận lại" icon={<Undo2 className="w-4 h-4" />}
                    handover={ret}
                    canSign={canManage && canReturn}
                    disabledHint={!canReturn ? 'Cần hoàn tất ký mượn (đủ hai bên) trước khi ký trả.' : undefined}
                    onOpen={() => openSignModal(it, 'RETURN')}
                    signLabel="Bên nhận ký trả"
                  />
                </div>
              </div>
            );
          })
        )}
      </div>

      {/* Modal biên bản ký mượn/trả — mở khi chọn một việc mượn đồ */}
      {signTarget && (
        <div className="fixed inset-0 z-[80] flex items-end sm:items-center justify-center">
          <div className="absolute inset-0 bg-black/40" onClick={closeSignModal} />
          <div className="relative w-full sm:w-[460px] rounded-t-2xl sm:rounded-xl bg-white shadow-xl border border-slate-200 p-4">
            <div className="mb-3 flex items-start justify-between gap-3">
              <div className="min-w-0">
                <h4 className="text-sm font-bold text-slate-800 flex items-center gap-1.5">
                  {signTarget.type === 'BORROW'
                    ? <><PackageCheck className="w-4 h-4 text-[#f37021]" /> Biên bản ký mượn (bên nhận)</>
                    : <><Undo2 className="w-4 h-4 text-[#f37021]" /> Biên bản ký trả (bên trả)</>}
                </h4>
                <p className="truncate text-xs text-slate-500 mt-0.5">
                  {signTarget.item.title}
                  {signTarget.item.departmentName ? ` • ${signTarget.item.departmentName}` : ''}
                  {signTarget.item.quantity != null ? ` • SL: ${signTarget.item.quantity}` : ''}
                </p>
              </div>
              <button type="button" onClick={closeSignModal} disabled={busy}
                className="rounded-full p-1 text-slate-500 hover:bg-slate-100 outline-none" aria-label="Đóng">
                <X className="w-4 h-4" />
              </button>
            </div>

            <label className="mb-1 block text-[11px] font-bold text-slate-600">Tình trạng tài sản</label>
            <select value={condition} onChange={(e) => setCondition(e.target.value as LogisticsItemCondition)}
              className="mb-3 w-full rounded-lg border border-slate-300 bg-white px-2.5 py-1.5 text-sm outline-none focus:border-[#004c91]">
              {CONDITION_OPTIONS.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
            </select>

            <label className="mb-1 block text-[11px] font-bold text-slate-600">
              Ghi chú tình trạng {CONDITION_REQUIRES_NOTE.has(condition) && <span className="text-red-500">*</span>}
            </label>
            <textarea value={note} onChange={(e) => setNote(e.target.value)} maxLength={1000} rows={3}
              placeholder="VD: Tài sản đầy đủ, hoạt động tốt..."
              className="w-full resize-none rounded-lg border border-slate-300 px-2.5 py-1.5 text-sm outline-none focus:border-[#004c91]" />
            {err && (
              <p className="mt-2 flex items-center gap-1 text-[11px] font-semibold text-red-600">
                <AlertCircle className="w-3 h-3 shrink-0" /> {err}
              </p>
            )}
            <div className="mt-3 flex items-center justify-end gap-2">
              <button type="button" disabled={busy} onClick={closeSignModal}
                className="rounded-lg border border-slate-200 bg-white px-3.5 py-1.5 text-xs font-bold text-slate-600 hover:bg-slate-50 disabled:opacity-50 outline-none">
                Hủy
              </button>
              <button type="button" disabled={busy} onClick={submit}
                className="inline-flex items-center gap-1 rounded-lg bg-[#004c91] px-3.5 py-1.5 text-xs font-bold text-white hover:bg-[#003b70] disabled:opacity-50 outline-none">
                {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
                Xác nhận ký {signTarget.type === 'BORROW' ? 'mượn' : 'trả'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function HandoverBlock({
  title, icon, handover, canSign, disabledHint, onOpen, signLabel,
}: {
  title: string; icon: React.ReactNode; handover: LogisticsHandover | null;
  canSign: boolean; disabledHint?: string;
  onOpen: () => void; signLabel: string;
}) {
  const borrowerSigned = !!handover?.borrowerSignedAt;
  const conditionLabel = handover?.itemCondition
    ? CONDITION_OPTIONS.find((c) => c.value === handover.itemCondition)?.label ?? handover.itemCondition
    : null;
  return (
    <div className="rounded-lg border border-gray-200 bg-gray-50/60 p-3">
      <div className="flex items-center gap-1.5 text-[12px] font-bold uppercase tracking-wide text-[#004c91]">{icon} {title}</div>
      <div className="mt-2 space-y-1 text-xs text-gray-700">
        <SignRow label="Bên nhận" name={handover?.borrowerSignedByName} at={handover?.borrowerSignedAt} />
        <SignRow label="Bên giao" name={handover?.providerSignedByName} at={handover?.providerSignedAt} waitingText="Chờ phòng ban ký" />
        {conditionLabel && <div><span className="text-gray-500">Tình trạng:</span> <b>{conditionLabel}</b></div>}
        {handover?.conditionNote && <div className="italic text-gray-500">Ghi chú: {handover.conditionNote}</div>}
      </div>

      {!borrowerSigned && (
        canSign ? (
          <button type="button" onClick={onOpen}
            className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] bg-white px-3 py-1.5 text-[11px] font-bold text-[#004c91] outline-none hover:bg-[#f0f7ff]">
            <PenLine className="w-3.5 h-3.5" /> {signLabel}
          </button>
        ) : disabledHint ? (
          <p className="mt-2 flex items-center gap-1 text-[11px] italic text-gray-400"><Clock className="w-3 h-3" /> {disabledHint}</p>
        ) : null
      )}
    </div>
  );
}

function SignRow({ label, name, at, waitingText = 'Chưa ký' }: {
  label: string; name?: string | null; at?: string | null; waitingText?: string;
}) {
  const signed = !!at;
  return (
    <div className="flex items-center gap-1.5">
      {signed
        ? <CheckCircle2 className="w-3.5 h-3.5 text-emerald-500 shrink-0" />
        : <Clock className="w-3.5 h-3.5 text-gray-300 shrink-0" />}
      <span className="text-gray-500">{label}:</span>
      {signed
        ? <span className="font-semibold text-gray-800">{name || 'Đã ký'} • {fmtDateTime(at)}</span>
        : <span className="italic text-gray-400">{waitingText}</span>}
    </div>
  );
}
