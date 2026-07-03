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
  PenLine, Clock, X, Building2, FileText,
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
                  <div className="shrink-0 flex flex-col items-end gap-1">
                    <button
                      type="button"
                      onClick={() => openSignModal(item)}
                      className={`inline-flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-[12px] font-bold outline-none transition-colors ${
                        canSign
                          ? 'border-[#004c91] bg-[#004c91] text-white hover:bg-[#003b70]'
                          : 'border-slate-300 bg-white text-slate-600 hover:bg-slate-50'
                      }`}
                    >
                      <FileText className="w-3.5 h-3.5" />
                      {canSign ? (handoverPhase === 'BORROW' ? 'Ký nhận' : 'Ký trả') : 'Xem biên bản'}
                    </button>
                    {!canSign && disabledHint && (
                      <p className="text-[10px] italic text-slate-400 flex items-center gap-1">
                        <Clock className="w-3 h-3 shrink-0" /> {disabledHint}
                      </p>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>

      {/* Modal biên bản ký mượn/trả */}
      {signTarget && createPortal(
        <div className="fixed inset-0 z-[80] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/60 backdrop-blur-sm" onClick={closeSignModal} />
          <div className="relative w-full max-w-4xl max-h-[90vh] flex flex-col rounded-2xl bg-white shadow-2xl overflow-hidden font-sans border border-slate-200">
            {/* Modal header */}
            <div className="bg-[#004c91] text-white px-6 py-4 flex items-center justify-between shrink-0">
              <h3 className="font-bold text-sm uppercase tracking-wide flex items-center gap-2">
                <FileText className="w-5 h-5 opacity-80" />
                BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU
              </h3>
              <button type="button" onClick={closeSignModal} disabled={busy}
                className="text-white/70 hover:text-white outline-none transition-colors" aria-label="Đóng">
                <X className="w-5 h-5" />
              </button>
            </div>

            {/* Modal body (scrollable) */}
            <div className="p-6 md:p-12 overflow-y-auto bg-white flex-1 text-slate-900 shadow-[inset_0_0_20px_rgba(0,0,0,0.02)]">
              {(() => {
                const b = findHandover(signTarget.item.handovers, 'BORROW');
                const r = findHandover(signTarget.item.handovers, 'RETURN');
                const bg1 = b?.providerSignedAt ? `${b.providerSignedByName || 'Phòng ban'}` : null;
                const bg2 = b?.borrowerSignedAt ? `${b.borrowerSignedByName || 'Host'}` : null;
                const nt1 = r?.borrowerSignedAt ? `${r.borrowerSignedByName || 'Host'}` : null;
                const nt2 = r?.providerSignedAt ? `${r.providerSignedByName || 'Phòng ban'}` : null;

                const canSignBG2 = signTarget.type === 'BORROW' && bg1 && !bg2;
                const isBorrowDone = bg1 && bg2;
                const canSignNT1 = signTarget.type === 'RETURN' && isBorrowDone && !nt1;

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
                          <p className="flex-1 min-w-[200px]">Bộ phận: <b>Host (Tiếp đón)</b></p>
                        </div>
                        <p>Lý do bàn giao: <b>Phục vụ công tác đón tiếp đoàn khách</b></p>
                        <p>Thời gian hẹn trả tài sản: <b>Sau khi kết thúc chuyến thăm</b></p>
                      </div>
                    </div>

                    <p className="font-bold text-[15px] mb-2">Cùng bàn giao tài sản với tình trạng sau:</p>
                    <div className="overflow-x-auto mb-6">
                      <table className="w-full border-collapse border border-slate-500 text-[14px]">
                        <thead>
                          <tr className="bg-slate-50">
                            <th className="border border-slate-500 p-2 text-center w-12">STT</th>
                            <th className="border border-slate-500 p-2 text-center">Nội dung</th>
                            <th className="border border-slate-500 p-2 text-center w-24">Số Lượng</th>
                            <th className="border border-slate-500 p-2 text-center">Tình Trạng bàn giao</th>
                            <th className="border border-slate-500 p-2 text-center">Tình Trạng nhận</th>
                            <th className="border border-slate-500 p-2 text-center">Ghi chú</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr>
                            <td className="border border-slate-500 p-2 text-center">1</td>
                            <td className="border border-slate-500 p-2 font-semibold">{signTarget.item.title}</td>
                            <td className="border border-slate-500 p-2 text-center">{signTarget.item.quantity || 1}</td>
                            <td className="border border-slate-500 p-2 text-center">
                              {b?.conditionNote || ''}
                            </td>
                            <td className="border border-slate-500 p-2 text-center">
                              {b?.borrowerSignedAt ? (signTarget.type === 'BORROW' ? note : 'Đã xác nhận') : ''}
                            </td>
                            <td className="border border-slate-500 p-2"></td>
                          </tr>
                        </tbody>
                      </table>
                    </div>

                    <div className="space-y-1 text-[14px] mb-8">
                      <p className="font-bold">Quy định khi sử dụng tài sản:</p>
                      <ul className="list-disc pl-8 space-y-1">
                        <li>Người mượn tài sản phải tuân thủ đúng mục đích sử dụng, không tự ý chuyển giao cho người khác.</li>
                        <li>Khi có vấn đề xảy ra (bị hỏng hoặc không nguyên hiện trạng ban đầu), <b>người mượn tài sản</b> sẽ phải chịu hoàn toàn trách nhiệm chi trả chi phí sửa chữa/đền bù.</li>
                        <li>An toàn trong quá trình sử dụng tài sản sẽ do <b>người mượn tài sản</b> chịu hoàn toàn trách nhiệm.</li>
                        <li>Ghi chú khác: ....................................................................................................................</li>
                      </ul>
                      <p className="mt-4">
                        Tôi là <b>{hostName}</b>, đã đọc hiểu và cam kết thực hiện đúng quy định sử dụng.
                      </p>
                    </div>

                    {/* 4 NÚT KÝ (Thiết kế dạng chữ ký văn bản) */}
                    <div className="space-y-8">
                    {/* KHỐI BÀN GIAO */}
                    <div className="relative my-7">
                      <div className="absolute inset-0 flex items-center" aria-hidden="true"><div className="w-full border-t border-slate-300"></div></div>
                      <div className="relative flex justify-center"><span className="bg-white px-4 py-1.5 text-[10px] font-black text-slate-900 uppercase tracking-widest border border-slate-200 rounded-full shadow-sm">BÀN GIAO</span></div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-slate-50/70 p-4.5 rounded-2xl border border-slate-200">
                      {/* Bên Giao */}
                      <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
                        <div>
                          <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-2">Ghi chú Bên Giao</label>
                          <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
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
                          {signTarget.type === 'BORROW' && !bg2 ? (
                            <textarea rows={2} value={note} onChange={e => setNote(e.target.value)} disabled={!canSignBG2 || busy} className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#f37021] focus:ring-1 focus:ring-orange-200 outline-none resize-none bg-slate-50/30" placeholder={canSignBG2 ? "Nhập ý kiến xác nhận nhận..." : "Chờ phòng ban ký trước..."} />
                          ) : (
                            <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                              {bg2 ? 'Đã xác nhận nhận tài sản.' : 'Chưa ký nhận.'}
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
                          ) : signTarget.type === 'BORROW' ? (
                            <div className="flex flex-row items-center justify-between gap-3 w-full">
                              <div className="flex items-center gap-2">
                                <FileText className={`w-4 h-4 shrink-0 ${canSignBG2 ? 'text-[#f37021]' : 'text-slate-400'}`} />
                                <div className="text-left">
                                  <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                  <span className="text-[9px] text-slate-450">{canSignBG2 ? 'Nhấp để xác nhận' : 'Chờ PB ký trước'}</span>
                                </div>
                              </div>
                              <button type="button" onClick={submit} disabled={!canSignBG2 || busy} className="py-2 px-3 bg-orange-50 hover:bg-orange-100 text-[#f37021] font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm">
                                {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />} Ký xác nhận (BG2)
                              </button>
                            </div>
                          ) : (
                            <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                              <div className="flex items-center gap-2">
                                <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                                <div className="text-left">
                                  <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                  <span className="text-[9px] text-slate-450">Chưa ký nhận</span>
                                </div>
                              </div>
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
                            {signTarget.type === 'RETURN' && !nt1 ? (
                              <>
                                <select value={condition} onChange={e => setCondition(e.target.value as LogisticsItemCondition)} className="mb-2 w-full text-xs p-2 border border-slate-250 rounded-xl outline-none focus:border-[#004c91]">
                                  {CONDITION_OPTIONS.map(c => <option key={c.value} value={c.value}>{c.label}</option>)}
                                </select>
                                <textarea rows={2} value={note} onChange={e => setNote(e.target.value)} disabled={busy} className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] focus:ring-1 focus:ring-blue-200 outline-none resize-none bg-slate-50/30" placeholder="Ghi nhận hiện trạng lúc trả..." />
                              </>
                            ) : (
                              <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                                {r?.conditionNote || 'Đã bàn giao trả tài sản.'}
                              </div>
                            )}
                          </div>
                          <div className={`border-2 rounded-xl p-3 relative ${nt1 ? 'border-emerald-500 bg-emerald-50/20' : canSignNT1 ? 'border-dashed border-[#004c91]/50 bg-blue-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                            {nt1 ? (
                              <div className="flex items-center gap-2.5">
                                <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs shrink-0 shadow-sm border border-emerald-100">✓</div>
                                <div>
                                  <span className="text-[9px] font-black uppercase text-emerald-800 block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                  <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{nt1}</p>
                                  <p className="text-[9px] text-slate-500">{fmtDateTime(r?.borrowerSignedAt)}</p>
                                </div>
                              </div>
                            ) : signTarget.type === 'RETURN' ? (
                              <div className="flex flex-row items-center justify-between gap-3 w-full">
                                <div className="flex items-center gap-2">
                                  <FileText className={`w-4 h-4 shrink-0 ${canSignNT1 ? 'text-[#004c91]' : 'text-slate-400'}`} />
                                  <div className="text-left">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                    <span className="text-[9px] text-slate-450">{canSignNT1 ? 'Nhấp để hoàn tất' : 'Chờ hoàn tất'}</span>
                                  </div>
                                </div>
                                <button type="button" onClick={submit} disabled={!canSignNT1 || busy} className="py-2 px-3 bg-blue-50 hover:bg-blue-100 text-[#004c91] font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-sm">
                                  {busy ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />} Ký Nghiệm thu (NT1)
                                </button>
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
                            <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-400 italic">
                              Chờ phòng ban nhận xét tình trạng tài sản và xác nhận...
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
                    </div>
                  </>
                );
              })()}
            </div>

            {/* Footer controls inside modal */}
            <div className="bg-slate-50 px-6 py-4 flex flex-col sm:flex-row justify-between items-center border-t border-slate-200 gap-4 shrink-0 rounded-b-2xl">
              <span className="text-[11px] text-slate-500 font-medium italic">
                {err ? <span className="text-red-500 flex items-center gap-1"><AlertCircle className="w-4 h-4"/> {err}</span> : 'Vui lòng kiểm tra kỹ hiện trạng trước khi ký xác nhận.'}
              </span>
              <button onClick={closeSignModal} className="px-6 py-2.5 bg-[#004c91] text-white hover:bg-[#003b70] text-[12px] font-bold rounded-xl transition-colors shadow-sm w-full sm:w-auto outline-none">
                Đóng biên bản
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
