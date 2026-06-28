/**
 * VisitProcess "Đang / Sau tiếp khách" — borrow/return handover signing for logistics items.
 *
 * Model: the instance Host signs the BORROWER side (bên nhận); the Department signs the PROVIDER side
 * (bên giao) from its own portal. Both signatures live on one visit_logistics_item_handovers row
 * (unique per item + type). RETURN can only be signed once BORROW is fully signed by both sides.
 * The borrower RETURN signature closes the item (status DONE). All wired to the real handover API.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { Loader2, PackageCheck, Undo2, CheckCircle2, AlertCircle, PenLine, Clock } from 'lucide-react';
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
  const [openKey, setOpenKey] = useState<string | null>(null);   // `${itemId}:${type}` whose sign form is open
  const [busyKey, setBusyKey] = useState<string | null>(null);
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

  const openForm = (itemId: number, type: LogisticsHandoverType) => {
    setOpenKey(`${itemId}:${type}`); setCondition('GOOD'); setNote(''); setErr(null);
  };
  const closeForm = () => { setOpenKey(null); setErr(null); };

  const submit = async (item: VisitInstanceLogisticsItem, type: LogisticsHandoverType) => {
    if (CONDITION_REQUIRES_NOTE.has(condition) && !note.trim()) {
      const m = 'Vui lòng nhập ghi chú tình trạng tài sản.'; setErr(m); pushToast?.('error', m); return;
    }
    if (!visitInstanceId) return;
    const key = `${item.logisticsItemId}:${type}`;
    setBusyKey(key);
    try {
      const res = await delegationsApi.signLogisticsHandoverBorrower(visitInstanceId, item.logisticsItemId, {
        handoverType: type, itemCondition: condition, note: note.trim() || null,
      });
      pushToast?.('success', res.message || 'Đã ký biên bản.');
      setOpenKey(null); setNote('');
      await load();
    } catch (e: any) {
      const m = apiError(e, 'Không thể ký biên bản. Vui lòng thử lại.');
      setErr(m); pushToast?.('error', m);
    } finally {
      setBusyKey(null);
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
                  <div className="text-sm font-bold text-gray-800">{it.title}</div>
                  <span className={`inline-flex items-center rounded-md border px-2 py-0.5 text-[11px] font-bold uppercase tracking-wide ${meta.cls}`}>{meta.label}</span>
                </div>

                <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-3">
                  <HandoverBlock
                    title="Biên bản mượn / bàn giao" icon={<PackageCheck className="w-4 h-4" />}
                    handover={borrow}
                    canSign={canManage}
                    busy={busyKey === `${it.logisticsItemId}:BORROW`}
                    open={openKey === `${it.logisticsItemId}:BORROW`}
                    condition={condition} note={note} err={err}
                    onOpen={() => openForm(it.logisticsItemId, 'BORROW')}
                    onClose={closeForm}
                    onCondition={setCondition} onNote={setNote}
                    onSubmit={() => submit(it, 'BORROW')}
                    signLabel="Bên nhận ký mượn"
                  />
                  <HandoverBlock
                    title="Biên bản trả / nhận lại" icon={<Undo2 className="w-4 h-4" />}
                    handover={ret}
                    canSign={canManage && canReturn}
                    disabledHint={!canReturn ? 'Cần hoàn tất ký mượn (đủ hai bên) trước khi ký trả.' : undefined}
                    busy={busyKey === `${it.logisticsItemId}:RETURN`}
                    open={openKey === `${it.logisticsItemId}:RETURN`}
                    condition={condition} note={note} err={err}
                    onOpen={() => openForm(it.logisticsItemId, 'RETURN')}
                    onClose={closeForm}
                    onCondition={setCondition} onNote={setNote}
                    onSubmit={() => submit(it, 'RETURN')}
                    signLabel="Bên nhận ký trả"
                  />
                </div>
              </div>
            );
          })
        )}
      </div>
    </div>
  );
}

function HandoverBlock({
  title, icon, handover, canSign, disabledHint, busy, open, condition, note, err,
  onOpen, onClose, onCondition, onNote, onSubmit, signLabel,
}: {
  title: string; icon: React.ReactNode; handover: LogisticsHandover | null;
  canSign: boolean; disabledHint?: string; busy: boolean; open: boolean;
  condition: LogisticsItemCondition; note: string; err: string | null;
  onOpen: () => void; onClose: () => void;
  onCondition: (c: LogisticsItemCondition) => void; onNote: (v: string) => void;
  onSubmit: () => void; signLabel: string;
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
        open ? (
          <div className="mt-2 space-y-2 rounded-lg border border-[#004c91]/20 bg-white p-2.5">
            <div>
              <label className="block text-[11px] font-bold text-gray-600 mb-1">Tình trạng tài sản</label>
              <select value={condition} onChange={(e) => onCondition(e.target.value as LogisticsItemCondition)}
                className="w-full px-2.5 py-1.5 rounded-lg border border-gray-300 outline-none text-xs bg-white focus:border-[#004c91]">
                {CONDITION_OPTIONS.map((c) => <option key={c.value} value={c.value}>{c.label}</option>)}
              </select>
            </div>
            <div>
              <label className="block text-[11px] font-bold text-gray-600 mb-1">
                Ghi chú tình trạng {CONDITION_REQUIRES_NOTE.has(condition) && <span className="text-red-500">*</span>}
              </label>
              <textarea value={note} onChange={(e) => onNote(e.target.value)} maxLength={1000}
                placeholder="VD: Tài sản đầy đủ, hoạt động tốt..."
                className="w-full h-[60px] resize-none px-2.5 py-1.5 rounded-lg border border-gray-300 outline-none text-xs focus:border-[#004c91]" />
            </div>
            {err && <p className="flex items-center gap-1 text-[11px] font-semibold text-red-600"><AlertCircle className="w-3 h-3 shrink-0" /> {err}</p>}
            <div className="flex items-center justify-end gap-2">
              <button type="button" disabled={busy} onClick={onClose}
                className="rounded-lg border border-gray-200 bg-white px-3 py-1.5 text-[11px] font-bold text-gray-600 outline-none hover:bg-gray-50 disabled:opacity-50">Hủy</button>
              <button type="button" disabled={busy} onClick={onSubmit}
                className="inline-flex items-center gap-1 rounded-lg bg-[#004c91] px-3 py-1.5 text-[11px] font-bold text-white outline-none hover:bg-[#003b70] disabled:opacity-50">
                {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />} Xác nhận ký
              </button>
            </div>
          </div>
        ) : canSign ? (
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
