/**
 * VisitProcess — Borrow/Return handover signing cho logistics items.
 *
 * Rule đúng thứ tự ký:
 *   BORROW (Đang tiếp khách = "Ký nhận"):
 *     1. Phòng ban (Provider) ký bàn giao trước.
 *     2. Host (Borrower) ký nhận sau khi PB đã ký.
 *   RETURN (Sau tiếp khách = "Ký trả"):
 *     1. Host (Borrower) ký trả trước.
 *     2. Phòng ban (Provider) ký nhận lại sau.
 *
 * Props:
 *   handoverPhase: 'BORROW' → tab Đang tiếp khách; 'RETURN' → tab Sau tiếp khách.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import {
  Loader2, PackageCheck, Undo2, CheckCircle2, AlertCircle,
  PenLine, Clock, X, Building2,
} from 'lucide-react';
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
  /** 'BORROW' in During tab; 'RETURN' in After tab. */
  handoverPhase: 'BORROW' | 'RETURN';
  pushToast?: ToastFn;
}

const CONDITION_OPTIONS: { value: LogisticsItemCondition; label: string }[] = [
  { value: 'GOOD', label: 'Tốt' },
  { value: 'DAMAGED', label: 'Hư hỏng' },
  { value: 'MISSING', label: 'Thiếu / mất' },
  { value: 'OTHER', label: 'Khác' },
];
const CONDITION_REQUIRES_NOTE = new Set<LogisticsItemCondition>(['DAMAGED', 'MISSING', 'OTHER']);
const HANDOVER_STATUSES = new Set(['ACCEPTED', 'IN_PROGRESS', 'DONE']);

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

const findHandover = (hs: LogisticsHandover[] | undefined, t: LogisticsHandoverType) =>
  hs?.find((h) => h.handoverType === t) ?? null;

const fullySigned = (h: LogisticsHandover | null) =>
  !!(h && h.borrowerSignedAt && h.providerSignedAt);

// ─── Status badge ─────────────────────────────────────────────────────────────

function HandoverStatusBadge({ borrow, ret, phase }: {
  borrow: LogisticsHandover | null;
  ret: LogisticsHandover | null;
  phase: 'BORROW' | 'RETURN';
}) {
  if (phase === 'BORROW') {
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
          <PenLine className="w-3 h-3" /> Chờ Host ký nhận
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
  } else {
    // RETURN phase
    if (fullySigned(ret)) {
      return (
        <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 border border-emerald-200 px-2 py-0.5 text-[11px] font-bold text-emerald-700">
          <CheckCircle2 className="w-3 h-3" /> Hoàn tất ký trả
        </span>
      );
    }
    if (ret?.borrowerSignedAt && !ret?.providerSignedAt) {
      return (
        <span className="inline-flex items-center gap-1 rounded-full bg-blue-50 border border-blue-200 px-2 py-0.5 text-[11px] font-bold text-blue-700">
          <Clock className="w-3 h-3" /> Chờ phòng ban ký nhận lại
        </span>
      );
    }
    // Chưa ký trả
    return (
      <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 border border-slate-200 px-2 py-0.5 text-[11px] font-bold text-slate-600">
        <Clock className="w-3 h-3" /> Chưa ký trả
      </span>
    );
  }
  return null;
}

// ─── Main Export ──────────────────────────────────────────────────────────────

export function LogisticsHandoverSection({ visitInstanceId, canManage, handoverPhase, pushToast }: Props) {
  const [items, setItems] = useState<VisitInstanceLogisticsItem[]>([]);
  const [loadedOnce, setLoadedOnce] = useState(false);
  const [loading, setLoading] = useState(false);
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

  // Xác định xem Host có thể ký item đó trong phase hiện tại không.
  const canSignItem = (item: VisitInstanceLogisticsItem): boolean => {
    if (!canManage) return false;
    const borrow = findHandover(item.handovers, 'BORROW');
    if (handoverPhase === 'BORROW') {
      // Phòng ban phải ký trước (providerSignedAt), Host ký sau (borrowerSignedAt null).
      return !!(borrow?.providerSignedAt) && !borrow?.borrowerSignedAt;
    } else {
      // RETURN: BORROW phải đủ 2 bên. Host ký trả (borrowerSignedAt null trên RETURN record).
      const ret = findHandover(item.handovers, 'RETURN');
      return fullySigned(borrow) && !ret?.borrowerSignedAt;
    }
  };

  const openSignModal = (item: VisitInstanceLogisticsItem) => {
    setSignTarget({ item, type: handoverPhase });
    setCondition('GOOD');
    setNote('');
    setErr(null);
  };
  const closeSignModal = () => { if (!busy) { setSignTarget(null); setErr(null); } };

  const submit = async () => {
    if (!signTarget) return;
    if (CONDITION_REQUIRES_NOTE.has(condition) && !note.trim()) {
      const m = 'Vui lòng nhập ghi chú tình trạng tài sản.';
      setErr(m); pushToast?.('error', m); return;
    }
    if (!visitInstanceId) return;
    setBusy(true);
    try {
      const res = await delegationsApi.signLogisticsHandoverBorrower(
        visitInstanceId, signTarget.item.logisticsItemId,
        { handoverType: signTarget.type, itemCondition: condition, note: note.trim() || null },
      );
      pushToast?.('success', res.message || 'Đã ký biên bản.');
      setSignTarget(null);
      setNote('');
      await load();
    } catch (e: any) {
      const m = apiError(e, 'Không thể ký biên bản. Vui lòng thử lại.');
      setErr(m); pushToast?.('error', m);
    } finally {
      setBusy(false);
    }
  };

  const sectionTitle = handoverPhase === 'BORROW'
    ? 'Ký nhận tài sản hậu cần'
    : 'Ký trả tài sản hậu cần';
  const sectionIcon = handoverPhase === 'BORROW'
    ? <PackageCheck className="w-5 h-5 text-[#f37021]" />
    : <Undo2 className="w-5 h-5 text-[#f37021]" />;

  return (
    <div className="bg-white border border-gray-200 rounded-xl shadow-sm overflow-hidden">
      {/* Section header */}
      <div className="flex items-center justify-between px-4 py-3 bg-white border-b border-gray-100">
        <h3 className="text-[14px] font-bold text-slate-800 flex items-center gap-2">
          <div className="p-1 bg-orange-100 rounded-md">{sectionIcon}</div>
          {sectionTitle}
        </h3>
        {loading && <Loader2 className="w-4 h-4 animate-spin text-gray-400" />}
      </div>

      {/* Body */}
      <div className="p-4">
        {!loadedOnce ? (
          <div className="flex items-center gap-2 py-3 text-sm text-gray-500">
            <Loader2 className="w-4 h-4 animate-spin" /> Đang tải...
          </div>
        ) : eligible.length === 0 ? (
          <p className="py-2 text-[13px] italic text-slate-400">
            Chưa có hạng mục hậu cần nào đã được phòng ban tiếp nhận.
          </p>
        ) : (
          <div className="space-y-2">
            {eligible.map((item) => {
              const borrow = findHandover(item.handovers, 'BORROW');
              const ret = findHandover(item.handovers, 'RETURN');
              const meta = LOGISTICS_STATUS_META[item.status] ?? {
                label: item.status,
                cls: 'bg-slate-100 text-slate-600 border-slate-200',
              };
              const canSign = canSignItem(item);

              // Hint text khi không thể ký
              let disabledHint: string | null = null;
              if (!canSign && canManage) {
                if (handoverPhase === 'BORROW') {
                  if (!borrow?.providerSignedAt) {
                    disabledHint = 'Chờ phòng ban ký bàn giao trước.';
                  } else if (borrow?.borrowerSignedAt) {
                    disabledHint = 'Đã ký nhận.';
                  }
                } else {
                  if (!fullySigned(borrow)) {
                    disabledHint = 'Cần hoàn tất ký mượn (đủ 2 bên) trước.';
                  } else if (ret?.borrowerSignedAt) {
                    disabledHint = 'Đã ký trả.';
                  }
                }
              }

              return (
                <div key={item.logisticsItemId}
                  className="rounded-lg border border-gray-200 bg-gray-50/50 p-3 flex flex-wrap items-start gap-3"
                >
                  {/* Left: item info */}
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2 mb-1">
                      <span className="text-[13px] font-bold text-gray-900">{item.title}</span>
                      <HandoverStatusBadge borrow={borrow} ret={ret} phase={handoverPhase} />
                      <span className={`inline-flex items-center rounded border px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide ${meta.cls}`}>
                        {meta.label}
                      </span>
                    </div>
                    <div className="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[12px] text-gray-500">
                      {item.departmentName && (
                        <span className="flex items-center gap-1">
                          <Building2 className="w-3 h-3" /> {item.departmentName}
                        </span>
                      )}
                      {item.quantity != null && <span>SL: {item.quantity}</span>}
                      {item.assignedToName && <span>Người xử lý: {item.assignedToName}</span>}
                    </div>

                    {/* Chữ ký tóm tắt */}
                    <div className="mt-2 flex flex-wrap gap-3">
                      <SignChip
                        label={handoverPhase === 'BORROW' ? 'Phòng ban ký giao' : 'Host ký trả'}
                        at={handoverPhase === 'BORROW' ? borrow?.providerSignedAt : ret?.borrowerSignedAt}
                        name={handoverPhase === 'BORROW' ? borrow?.providerSignedByName : ret?.borrowerSignedByName}
                      />
                      <SignChip
                        label={handoverPhase === 'BORROW' ? 'Host ký nhận' : 'Phòng ban ký nhận lại'}
                        at={handoverPhase === 'BORROW' ? borrow?.borrowerSignedAt : ret?.providerSignedAt}
                        name={handoverPhase === 'BORROW' ? borrow?.borrowerSignedByName : ret?.providerSignedByName}
                      />
                    </div>
                  </div>

                  {/* Right: action */}
                  <div className="shrink-0 flex items-start">
                    {canSign ? (
                      <button
                        type="button"
                        onClick={() => openSignModal(item)}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] bg-white px-3 py-1.5 text-[12px] font-bold text-[#004c91] hover:bg-[#f0f7ff] outline-none"
                      >
                        <PenLine className="w-3.5 h-3.5" />
                        {handoverPhase === 'BORROW' ? 'Ký nhận' : 'Ký trả'}
                      </button>
                    ) : disabledHint ? (
                      <p className="text-[11px] italic text-slate-400 flex items-center gap-1 pt-1">
                        <Clock className="w-3 h-3 shrink-0" /> {disabledHint}
                      </p>
                    ) : null}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Modal biên bản ký mượn/trả */}
      {signTarget && createPortal(
        <div className="fixed inset-0 z-[80] flex items-end sm:items-center justify-center">
          <div className="absolute inset-0 bg-black/40" onClick={closeSignModal} />
          <div className="relative w-full sm:w-[480px] rounded-t-2xl sm:rounded-xl bg-white shadow-xl border border-slate-200 p-5">
            {/* Modal header */}
            <div className="mb-4 flex items-start justify-between gap-3">
              <div className="min-w-0">
                <h4 className="text-sm font-bold text-slate-800 flex items-center gap-1.5">
                  {signTarget.type === 'BORROW'
                    ? <><PackageCheck className="w-4 h-4 text-[#f37021]" /> Biên bản ký nhận (Host nhận từ phòng ban)</>
                    : <><Undo2 className="w-4 h-4 text-[#f37021]" /> Biên bản ký trả (Host trả lại cho phòng ban)</>}
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

            {/* Hiển thị chữ ký bên kia (đã ký) */}
            {signTarget.type === 'BORROW' && (() => {
              const b = findHandover(signTarget.item.handovers, 'BORROW');
              return b?.providerSignedAt ? (
                <div className="mb-4 rounded-lg bg-emerald-50 border border-emerald-200 px-3 py-2.5">
                  <p className="text-[11px] font-bold text-emerald-700 mb-1 uppercase tracking-wide">
                    Phòng ban đã ký bàn giao
                  </p>
                  <p className="text-[12px] text-emerald-800 font-semibold">
                    {b.providerSignedByName || 'Phòng ban'} — {fmtDateTime(b.providerSignedAt)}
                  </p>
                  {b.conditionNote && (
                    <p className="text-[11px] text-emerald-700 mt-1 italic">Ghi chú: {b.conditionNote}</p>
                  )}
                </div>
              ) : null;
            })()}

            {signTarget.type === 'RETURN' && (() => {
              const b = findHandover(signTarget.item.handovers, 'BORROW');
              return b ? (
                <div className="mb-4 rounded-lg bg-blue-50 border border-blue-200 px-3 py-2.5">
                  <p className="text-[11px] font-bold text-blue-700 mb-1 uppercase tracking-wide">
                    Biên bản mượn (đã hoàn tất)
                  </p>
                  <div className="grid grid-cols-2 gap-x-4 text-[12px] text-blue-800">
                    <div>PB ký giao: <b>{b.providerSignedByName || '—'}</b></div>
                    <div>Host ký nhận: <b>{b.borrowerSignedByName || '—'}</b></div>
                  </div>
                </div>
              ) : null;
            })()}

            {/* Tình trạng tài sản */}
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

            <div className="mt-4 flex items-center justify-end gap-2">
              <button type="button" disabled={busy} onClick={closeSignModal}
                className="rounded-lg border border-slate-200 bg-white px-3.5 py-1.5 text-xs font-bold text-slate-600 hover:bg-slate-50 disabled:opacity-50 outline-none">
                Hủy
              </button>
              <button type="button" disabled={busy} onClick={submit}
                className="inline-flex items-center gap-1 rounded-lg bg-[#004c91] px-3.5 py-1.5 text-xs font-bold text-white hover:bg-[#003b70] disabled:opacity-50 outline-none">
                {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <CheckCircle2 className="w-3.5 h-3.5" />}
                Xác nhận {signTarget.type === 'BORROW' ? 'ký nhận' : 'ký trả'}
              </button>
            </div>
          </div>
        </div>,
        document.body
      )}
    </div>
  );
}

// ─── Sub-components ────────────────────────────────────────────────────────────

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
