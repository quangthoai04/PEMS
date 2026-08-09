/**
 * GeneralExpensePanel — Bảng chi phí của Host trong tiến trình đoàn (tab Sau tiếp khách).
 *
 * - Phần "Chi phí phòng ban": đồng bộ (read-only) các bảng kê phòng ban đã lưu — mọi dòng
 *   hiển thị loại "Hạng mục yêu cầu"; đơn chưa kê khai hiện "Chưa kê khai", đơn xác nhận
 *   không có chi phí hiện "Không có chi phí".
 * - Phần "Chi phí chung" của Host: chỉ được chọn loại Phát sinh / Đền bù / Khác.
 * - Nút "Nhắc nhở": gửi mail + thông báo hệ thống đến người phụ trách các đơn chưa kê khai.
 * - Nút "Xuất thống kê": in bảng thống kê chi phí toàn đoàn (PDF qua hộp thoại in).
 */
import React, { useEffect, useState } from 'react';
import { createPortal } from 'react-dom';
import { Loader2, Plus, Save, Trash2, DollarSign, AlertTriangle, Bell, Printer, CheckCircle2, Clock, Building2, Eye, Mail, X, Send } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import toast from 'react-hot-toast';
import visitExpenseService, {
  VisitExpenseReport, VisitInstanceExpenseSummary, SaveExpenseReportCommand, SaveExpenseItemDto
} from '../../../services/visit-expense.service';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { VisitInstanceLogisticsItem } from '../../../features/delegations/types/delegations.types';
import { ConfirmModal } from '../../../components/modals/ConfirmModal';
import { UnitPriceInput } from '../../../components/common/UnitPriceInput';

interface Props {
  visitInstanceId: number;
  isReadOnly?: boolean;
  sectionNumber?: string | number;
  /**
   * The campus instance's lifecycle status. The panel needs it to tell "there is no report yet
   * because the visit has not finished" from "there is no report and there never will be" — those
   * are two different empty states, and neither is an error.
   */
  instanceStatus?: string;
}

const ORIGIN_LABELS: Record<string, string> = {
  REQUEST_ITEM: 'Hạng mục yêu cầu',
  MANUAL: 'Nhập tay',
  ADDITIONAL: 'Phát sinh',
  DAMAGE_LOSS: 'Đền bù hư hỏng/mất mát',
  OTHER: 'Khác',
};

// Host chỉ được kê các loại này (bỏ "Nhập tay" và "Hạng mục yêu cầu").
const HOST_ORIGIN_OPTIONS = ['ADDITIONAL', 'DAMAGE_LOSS', 'OTHER'] as const;

const EXPENSE_ELIGIBLE_STATUSES = new Set(['ACCEPTED', 'IN_PROGRESS', 'DONE']);

export function GeneralExpensePanel({ visitInstanceId, isReadOnly = false, sectionNumber, instanceStatus }: Props) {
  const [report, setReport] = useState<VisitExpenseReport | null>(null);
  const [initializing, setInitializing] = useState(false);
  const [isExpanded, setIsExpanded] = useState(true);
  const [summary, setSummary] = useState<VisitInstanceExpenseSummary | null>(null);
  const [logisticsItems, setLogisticsItems] = useState<VisitInstanceLogisticsItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [reminding, setReminding] = useState(false);
  const [printing, setPrinting] = useState(false);
  const [items, setItems] = useState<SaveExpenseItemDto[]>([]);
  const [reportNote, setReportNote] = useState('');
  const [showRemindEmailPreview, setShowRemindEmailPreview] = useState(false);
  const [lastRemindSent, setLastRemindSent] = useState<{ count: number; recipients: string[]; sentAt: string } | null>(null);
  const [remindNote, setRemindNote] = useState('');

  /** Loads the report into the editable state, or clears it when there is none yet. */
  const applyReport = (general: VisitExpenseReport | null) => {
    setReport(general);
    setReportNote(general?.reportNote || '');
    setItems((general?.items || []).map(it => ({
      expenseItemId: it.expenseItemId,
      itemOrigin: it.itemOrigin,
      itemName: it.itemName,
      description: it.description,
      quantity: it.quantity,
      unitName: it.unitName,
      unitPrice: it.unitPrice,
      itemNote: it.itemNote,
      displayOrder: it.displayOrder,
    })));
  };

  const fetchAll = async () => {
    try {
      setLoading(true);
      // Read only. Nothing here creates a report: merely opening this tab used to POST-in-disguise and
      // fail with a 403 toast on any campus that was not exactly at AFTER_VISIT.
      const [general, sum, logistics] = await Promise.all([
        visitExpenseService.getGeneralExpenseReport(visitInstanceId),
        visitExpenseService.getExpenseSummary(visitInstanceId),
        delegationsApi.getInstanceLogistics(visitInstanceId).catch(() => ({ items: [] as VisitInstanceLogisticsItem[] })),
      ]);
      setSummary(sum);
      setLogisticsItems((logistics.items || []).filter(
        (i) => i.coordinationMode === 'SYSTEM_REQUEST' && EXPENSE_ELIGIBLE_STATUSES.has(i.status),
      ));
      applyReport(general);
    } catch (e: any) {
      // One id per instance: a re-render or a second mount cannot stack identical toasts.
      toast.error(e?.response?.data?.message || 'Lỗi khi tải thông tin chi phí.', {
        id: `expense-load-${visitInstanceId}`,
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchAll();
    // eslint-disable-next-line
  }, [visitInstanceId]);

  const handleInitialize = async () => {
    try {
      setInitializing(true);
      applyReport(await visitExpenseService.initializeGeneralExpenseReport(visitInstanceId));
      toast.success('Đã khởi tạo bảng chi phí chung.');
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Không thể khởi tạo bảng chi phí.', {
        id: `expense-init-${visitInstanceId}`,
      });
    } finally {
      setInitializing(false);
    }
  };

  // In bảng thống kê: mount vùng in xong mới gọi window.print.
  useEffect(() => {
    if (!printing) return;
    const t = setTimeout(() => {
      window.print();
      setPrinting(false);
      toast.success('Đã xuất thống kê thành công!');
    }, 80);
    return () => clearTimeout(t);
  }, [printing]);

  if (loading) {
    return (
      <div className="p-6 flex justify-center items-center text-slate-500 text-sm">
        <Loader2 className="w-5 h-5 animate-spin mr-2 text-[#004c91]" />
        Đang tải bảng chi phí...
      </div>
    );
  }

  // No report is a STATE, not a failure. The panel still renders — the department declarations below
  // exist independently of the Host's own sheet, and the reader came here to see them.
  const readonly = isReadOnly || report?.status === 'FINALIZED' || report?.status === 'CANCELLED';
  // The one place a report may be opened, mirroring the server's rule so the button is not offered
  // where the call would be refused. The server re-checks; this only avoids a doomed request.
  const canInitialize = !report && !isReadOnly && instanceStatus === 'AFTER_VISIT';

  // Bảng kê phòng ban (đồng bộ từ đơn yêu cầu) — ghép trạng thái từng đơn.
  const deptReports = summary?.logisticsReports || [];
  const reportByItemId = new Map(deptReports.filter(r => r.logisticsItemId != null).map(r => [r.logisticsItemId as number, r]));
  const deptRows = logisticsItems.map(item => {
    const r = reportByItemId.get(item.logisticsItemId);
    const state: 'SAVED' | 'NO_EXPENSE' | 'PENDING' =
      r && r.status === 'SAVED' ? (r.noExpense ? 'NO_EXPENSE' : 'SAVED') : 'PENDING';
    return { item, report: r, state };
  });
  // Bảng kê phòng ban không gắn được với đơn đang hiển thị (đơn đã đổi trạng thái...) vẫn tính tiền.
  const orphanReports = deptReports.filter(r => r.status === 'SAVED' && !r.noExpense
    && !logisticsItems.some(i => i.logisticsItemId === r.logisticsItemId));

  const deptTotal = deptReports.filter(r => r.status === 'SAVED' && !r.noExpense)
    .reduce((s, r) => s + r.totalAmount, 0);
  const generalTotal = items.reduce((sum, it) => sum + (it.quantity || 0) * (it.unitPrice || 0), 0);
  const grandTotal = deptTotal + generalTotal;
  const pendingCount = deptRows.filter(r => r.state === 'PENDING').length;

  const handleAddItem = () => {
    setItems([...items, { itemOrigin: 'ADDITIONAL', itemName: '', quantity: 1, unitName: '', unitPrice: 0, displayOrder: items.length + 1 }]);
  };

  const handleUpdateItem = (index: number, field: keyof SaveExpenseItemDto, value: any) => {
    const next = [...items];
    (next[index] as any)[field] = value;
    setItems(next);
  };

  const handleRemoveItem = (index: number) => setItems(items.filter((_, i) => i !== index));

  const handleSave = async () => {
    if (!report) return;
    if (items.some(i => !i.itemName.trim())) {
      toast.error('Vui lòng nhập tên cho tất cả các hạng mục chi phí.');
      return;
    }
    try {
      setSaving(true);
      const cmd: SaveExpenseReportCommand = {
        rowVersion: report.rowVersion,
        reportNote,
        noExpense: false,
        items: items.map((it, idx) => ({ ...it, displayOrder: idx + 1 })),
      };
      await visitExpenseService.saveExpenseReport(report.expenseReportId, cmd);
      toast.success('Đã lưu chi phí chung thành công.');
      await fetchAll();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Lỗi khi lưu chi phí.');
    } finally {
      setSaving(false);
    }
  };

  const handleRemind = async () => {
    try {
      setReminding(true);
      const res = await visitExpenseService.remindExpenseReports(visitInstanceId);
      if (res.remindedCount === 0) {
        toast('Không có đơn yêu cầu nào cần nhắc kê khai chi phí.', { icon: 'ℹ️' });
      } else {
        const nowStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) + ' ' + new Date().toLocaleDateString('vi-VN');
        setLastRemindSent({ count: res.remindedCount, recipients: res.recipients || [], sentAt: nowStr });
        toast.success(`Đã gửi nhắc nhở ${res.remindedCount} đơn (${res.recipients.join(', ')}).`);
      }
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Không thể gửi nhắc nhở.');
    } finally {
      setReminding(false);
    }
  };

  // Options phân loại cho từng dòng: 3 loại chuẩn + giữ giá trị cũ (dữ liệu trước đây) nếu khác.
  const originOptionsFor = (current: string) => {
    const base: string[] = [...HOST_ORIGIN_OPTIONS];
    if (current && !base.includes(current)) base.unshift(current);
    return base;
  };

  return (
    <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden font-sans">
      <div 
        className="flex items-center justify-between bg-[#004c91] px-5 py-3.5 cursor-pointer select-none transition-colors"
        onClick={() => setIsExpanded(!isExpanded)}
      >
        <div className="flex items-center gap-3">
          {sectionNumber && (
            <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm font-black text-white shrink-0">
              {sectionNumber}
            </span>
          )}
          {!sectionNumber && (
            <div className="p-1.5 bg-white/10 text-white rounded-lg border border-white/20 shrink-0">
              <DollarSign className="w-4 h-4" />
            </div>
          )}
          <div>
            <h2 className="text-sm font-bold text-white tracking-tight uppercase">Chi phí đoàn</h2>
          </div>
        </div>
        <div className="flex items-center gap-3">
          {readonly && (
            <span className="inline-flex items-center gap-1 px-2 py-1 rounded-lg text-[10px] font-bold bg-white/20 text-white border border-white/30">
              <AlertTriangle className="w-3 h-3" /> Chỉ xem
            </span>
          )}
          <div className="p-1 hover:bg-white/10 rounded-md transition-colors text-white">
            <svg className={`w-5 h-5 transform transition-transform ${!isExpanded ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
            </svg>
          </div>
        </div>
      </div>

      {isExpanded && (
        <div className="p-4 space-y-4">
          {/* ── 1. Chi phí phòng ban (Hạng mục yêu cầu) ── */}
        <div>
          <div className="flex items-center justify-between mb-1.5">
            <h3 className="text-[11px] font-black text-slate-600 uppercase tracking-wide flex items-center gap-1.5">
              <Building2 className="w-3.5 h-3.5 text-[#004c91]" /> Chi phí phòng ban — Hạng mục yêu cầu
            </h3>
            {pendingCount > 0 && (
              <span className="text-[10px] font-bold text-amber-600">{pendingCount} đơn chưa kê khai</span>
            )}
          </div>

          {deptRows.length === 0 && orphanReports.length === 0 ? (
            <p className="text-[11px] text-slate-400 italic px-1 py-2">Đoàn không có đơn yêu cầu phòng ban nào cần kê khai chi phí.</p>
          ) : (
            <div className="rounded-lg border border-slate-200 divide-y divide-slate-100 overflow-hidden">
              {deptRows.map(({ item, report: r, state }) => (
                <div key={item.logisticsItemId} className="bg-slate-50/40">
                  <div className="flex flex-wrap items-center gap-2 px-3 py-2">
                    <span className="text-xs font-bold text-slate-800">{item.title}</span>
                    <span className="text-[10px] text-slate-500">{item.departmentName}</span>
                    {state === 'SAVED' && (
                      <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
                        <CheckCircle2 className="w-2.5 h-2.5" /> Đã kê khai
                      </span>
                    )}
                    {state === 'NO_EXPENSE' && (
                      <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
                        <CheckCircle2 className="w-2.5 h-2.5" /> Không có chi phí
                      </span>
                    )}
                    {state === 'PENDING' && (
                      <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-amber-50 text-amber-700 border border-amber-200">
                        <Clock className="w-2.5 h-2.5" /> Chưa kê khai
                      </span>
                    )}
                    <span className="ml-auto text-xs font-black text-[#004c91]">
                      {state === 'SAVED' && r ? `${r.totalAmount.toLocaleString('vi-VN')} ₫` : state === 'NO_EXPENSE' ? '0 ₫' : '—'}
                    </span>
                  </div>
                  {state === 'SAVED' && r && r.items.length > 0 && (
                    <table className="w-full text-left border-collapse whitespace-nowrap text-[11px] bg-white">
                      <tbody className="divide-y divide-slate-50">
                        {r.items.map(it => (
                          <tr key={it.expenseItemId}>
                            <td className="pl-6 pr-2 py-1 text-slate-400 w-32">Hạng mục yêu cầu</td>
                            <td className="px-2 py-1 font-semibold text-slate-700">{it.itemName}</td>
                            <td className="px-2 py-1 text-right text-slate-500 w-14">{it.quantity}</td>
                            <td className="px-2 py-1 text-slate-400 w-20">{it.unitName || ''}</td>
                            <td className="px-2 py-1 text-right text-slate-500 w-24">{it.unitPrice.toLocaleString('vi-VN')}</td>
                            <td className="px-3 py-1 text-right font-bold text-slate-700 w-28">{it.totalAmount.toLocaleString('vi-VN')} ₫</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </div>
              ))}
              {orphanReports.map(r => (
                <div key={r.expenseReportId} className="flex flex-wrap items-center gap-2 px-3 py-2 bg-slate-50/40">
                  <span className="text-xs font-bold text-slate-800">{r.logisticsItemTitle || 'Đơn yêu cầu'}</span>
                  <span className="text-[10px] text-slate-500">{r.departmentName}</span>
                  <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[9px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
                    <CheckCircle2 className="w-2.5 h-2.5" /> Đã kê khai
                  </span>
                  <span className="ml-auto text-xs font-black text-[#004c91]">{r.totalAmount.toLocaleString('vi-VN')} ₫</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* ── 2. Chi phí chung (Host) ── */}
        <div>
          <h3 className="text-[11px] font-black text-slate-600 uppercase tracking-wide mb-1.5 flex items-center gap-1.5">
            <DollarSign className="w-3.5 h-3.5 text-[#f37021]" /> Chi phí chung (người phụ trách kê khai)
          </h3>
          {!report ? (
            /* Chưa có bảng chi phí chung. Đây là trạng thái hợp lệ — không phải lỗi:
               - đang ở giai đoạn trước/trong tiếp khách  → chưa tới lúc kê khai;
               - đã đóng đoàn mà không kê khai            → chỉ xem, không tạo mới. */
            <div
              data-testid="general-expense-empty"
              className="rounded-lg border border-dashed border-slate-300 bg-slate-50/60 px-3 py-4 text-center"
            >
              <p className="text-[11px] font-semibold text-slate-500">
                {canInitialize
                  ? 'Chưa có bảng chi phí chung cho chuyến này.'
                  : instanceStatus === 'CLOSED'
                    ? 'Chuyến thăm đã đóng và không có bảng chi phí chung nào được kê khai.'
                    : 'Bảng chi phí chung sẽ khả dụng khi chuyến thăm chuyển sang giai đoạn "Sau tiếp khách".'}
              </p>
              {canInitialize && (
                <button
                  type="button"
                  data-testid="general-expense-initialize"
                  onClick={handleInitialize}
                  disabled={initializing}
                  className="mt-2.5 inline-flex items-center gap-1.5 px-3 py-1.5 bg-[#f37021] hover:opacity-90 text-white text-[11px] font-black rounded-lg transition-all shadow-sm cursor-pointer outline-none disabled:opacity-50"
                >
                  {initializing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                  Khởi tạo bảng chi phí
                </button>
              )}
            </div>
          ) : (
          <>
          <div className="overflow-x-auto rounded-lg border border-slate-200">
            <table className="w-full text-left border-collapse whitespace-nowrap text-xs">
              <thead className="bg-slate-50">
                <tr>
                  <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase w-56">Phân loại</th>
                  <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase min-w-[140px]">Tên hạng mục</th>
                  <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase text-right w-16">SL</th>
                  <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase w-24">Đơn vị</th>
                  <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase text-right w-28">Đơn giá (₫)</th>
                  <th className="px-2.5 py-1.5 text-[10px] font-bold text-slate-500 uppercase text-right w-28">Thành tiền</th>
                  {!readonly && <th className="px-1 py-1.5 w-8"></th>}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.length === 0 ? (
                  <tr>
                    <td colSpan={readonly ? 6 : 7} className="px-3 py-4 text-center text-[11px] text-slate-400 italic">
                      Chưa có hạng mục chi phí chung nào.
                    </td>
                  </tr>
                ) : items.map((it, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/50">
                    <td className="px-2.5 py-1">
                      <select
                        value={it.itemOrigin}
                        onChange={(e) => handleUpdateItem(idx, 'itemOrigin', e.target.value)}
                        disabled={readonly}
                        className="w-full bg-transparent border-none text-xs font-medium text-slate-700 outline-none focus:ring-0 disabled:opacity-70 disabled:bg-transparent"
                      >
                        {originOptionsFor(it.itemOrigin).map(k => (
                          <option key={k} value={k}>{ORIGIN_LABELS[k] || k}</option>
                        ))}
                      </select>
                    </td>
                    <td className="px-2.5 py-1">
                      <input type="text" value={it.itemName}
                        onChange={(e) => handleUpdateItem(idx, 'itemName', e.target.value)}
                        disabled={readonly} placeholder="Nhập tên..."
                        className="w-full bg-transparent border-none text-xs font-semibold text-slate-800 placeholder-slate-300 outline-none focus:ring-0 px-0 disabled:bg-transparent" />
                    </td>
                    <td className="px-2.5 py-1">
                      <input type="number" min="0" step="1" value={Math.floor(it.quantity || 0)}
                        onChange={(e) => handleUpdateItem(idx, 'quantity', Math.max(0, parseInt(e.target.value, 10) || 0))}
                        disabled={readonly}
                        className="w-full bg-transparent border-none text-xs font-bold text-slate-700 text-right outline-none focus:ring-0 px-0 disabled:bg-transparent" />
                    </td>
                    <td className="px-2.5 py-1">
                      <input type="text" value={it.unitName || ''}
                        onChange={(e) => handleUpdateItem(idx, 'unitName', e.target.value)}
                        disabled={readonly} placeholder="Cái, Chuyến..."
                        className="w-full bg-transparent border-none text-xs text-slate-600 placeholder-slate-300 outline-none focus:ring-0 px-0 disabled:bg-transparent" />
                    </td>
                    <td className="px-2.5 py-1">
                      <UnitPriceInput
                        value={it.unitPrice || 0}
                        onChange={(val) => handleUpdateItem(idx, 'unitPrice', val)}
                        disabled={readonly}
                        step={1000}
                        className="w-full bg-transparent border-none text-xs font-bold text-slate-700 text-right outline-none focus:ring-0 px-0 disabled:bg-transparent"
                      />
                    </td>
                    <td className="px-2.5 py-1 text-right font-black text-[#004c91]">
                      {((it.quantity || 0) * (it.unitPrice || 0)).toLocaleString('vi-VN')} ₫
                    </td>
                    {!readonly && (
                      <td className="px-1 py-1 text-center">
                        <button type="button" onClick={() => handleRemoveItem(idx)} title="Xóa"
                          className="p-1 text-slate-400 hover:text-rose-500 hover:bg-rose-50 rounded-md transition-colors outline-none cursor-pointer">
                          <Trash2 className="w-3.5 h-3.5" />
                        </button>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!readonly && (
            <button type="button" onClick={handleAddItem}
              className="mt-2 inline-flex items-center gap-1 px-2.5 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-[11px] font-bold rounded-lg transition-colors cursor-pointer outline-none">
              <Plus className="w-3.5 h-3.5" /> Thêm hạng mục
            </button>
          )}
          </>
          )}
        </div>

        {/* ── Tổng cộng + hành động ── */}
        <div className="rounded-lg bg-orange-50 border border-orange-100 px-3 py-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs">
          <span className="font-bold text-slate-600">Phòng ban: <b className="text-[#004c91]">{deptTotal.toLocaleString('vi-VN')} ₫</b></span>
          <span className="font-bold text-slate-600">Chi phí chung: <b className="text-[#004c91]">{generalTotal.toLocaleString('vi-VN')} ₫</b></span>
          <span className="ml-auto font-black text-slate-700 uppercase text-[11px]">Tổng chi phí đoàn:
            <span className="text-sm font-black text-[#c2410c] ml-1.5">{grandTotal.toLocaleString('vi-VN')} ₫</span>
          </span>
        </div>

        {!readonly && report && (
          <div className="flex flex-wrap items-center justify-end gap-2">
            <input
              type="text"
              value={reportNote}
              onChange={(e) => setReportNote(e.target.value)}
              placeholder="Ghi chú tổng thể (không bắt buộc)..."
              className="flex-1 min-w-[160px] px-2.5 py-1.5 text-[11px] rounded-lg border border-slate-200 outline-none focus:border-[#004c91] transition-shadow placeholder-slate-400"
            />
            <div className="inline-flex items-center">
              <button type="button" onClick={handleRemind} disabled={reminding}
                title="Gửi mail + thông báo đến người phụ trách các đơn chưa kê khai chi phí"
                className="inline-flex items-center gap-1 px-2.5 py-1.5 bg-amber-50 hover:bg-amber-100 text-amber-700 text-[11px] font-bold rounded-l-lg border border-amber-300 transition-colors cursor-pointer outline-none disabled:opacity-50">
                {reminding ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Bell className="w-3.5 h-3.5" />}
                Nhắc nhở{pendingCount > 0 ? ` (${pendingCount})` : ''}
              </button>
              <button type="button" onClick={() => setShowRemindEmailPreview(true)}
                title="Xem trước / Xem email nhắc nhở kê khai chi phí"
                className="inline-flex items-center justify-center px-2 py-1.5 bg-amber-100 hover:bg-amber-200 text-amber-800 text-[11px] font-bold rounded-r-lg border border-l-0 border-amber-300 transition-colors cursor-pointer outline-none">
                <Eye className="w-3.5 h-3.5" />
              </button>
            </div>
            <button type="button" onClick={() => setPrinting(true)}
              className="inline-flex items-center gap-1 px-2.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 text-[11px] font-bold rounded-lg border border-slate-300 transition-colors cursor-pointer outline-none">
              <Printer className="w-3.5 h-3.5" /> Xuất thống kê
            </button>
            <button type="button" onClick={handleSave} disabled={saving}
              className="inline-flex items-center gap-1 px-3 py-1.5 bg-[#f37021] hover:opacity-90 text-white text-[11px] font-black rounded-lg transition-all shadow-sm cursor-pointer outline-none disabled:opacity-50">
              {saving ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Save className="w-3.5 h-3.5" />} Lưu chi phí
            </button>
          </div>
        )}
        {(readonly || !report) && (
          <div className="flex justify-end">
            <button type="button" onClick={() => setPrinting(true)}
              className="inline-flex items-center gap-1 px-2.5 py-1.5 bg-white hover:bg-slate-50 text-slate-700 text-[11px] font-bold rounded-lg border border-slate-300 transition-colors cursor-pointer outline-none">
              <Printer className="w-3.5 h-3.5" /> Xuất thống kê
            </button>
          </div>
        )}
        </div>
      )}

      {/* ── Vùng in "Xuất thống kê" — portal ra document.body: vùng in nằm trong layout cuộn/
          overflow-hidden của trang tiến trình sẽ bị cắt mất khi in (ra trang trắng), nên phải
          thoát khỏi cây layout và ẩn mọi phần tử khác bằng display:none lúc in ── */}
      {printing && createPortal(
        <>
          <style type="text/css" media="print">
            {`
              body > *:not(#expense-statement-print) { display: none !important; }
              #expense-statement-print { display: block !important; margin: 0; padding: 24px; }
            `}
          </style>
          <div id="expense-statement-print" className="hidden bg-white text-slate-900">
            <div className="text-center mb-6">
              <h4 className="text-xl font-bold uppercase tracking-wide">BẢNG THỐNG KÊ CHI PHÍ TIẾP ĐÓN ĐOÀN</h4>
              <p className="text-xs text-slate-500 mt-1">Mã tiến trình: vr-{visitInstanceId} • Xuất ngày {new Date().toLocaleDateString('vi-VN')}</p>
            </div>
            <table className="w-full border-collapse border border-slate-500 text-[13px]">
              <thead>
                <tr className="bg-slate-50">
                  <th className="border border-slate-500 p-2 text-center w-10">STT</th>
                  <th className="border border-slate-500 p-2 text-center">Phân loại</th>
                  <th className="border border-slate-500 p-2 text-center">Tên hạng mục</th>
                  <th className="border border-slate-500 p-2 text-center w-16">SL</th>
                  <th className="border border-slate-500 p-2 text-center w-20">Đơn vị</th>
                  <th className="border border-slate-500 p-2 text-center w-28">Đơn giá (₫)</th>
                  <th className="border border-slate-500 p-2 text-center w-28">Thành tiền (₫)</th>
                </tr>
              </thead>
              <tbody>
                {(() => {
                  let stt = 0;
                  const rows: React.ReactNode[] = [];
                  deptRows.forEach(({ item, report: r, state }) => {
                    rows.push(
                      <tr key={`h-${item.logisticsItemId}`} className="bg-slate-100/60">
                        <td className="border border-slate-500 p-2 font-bold" colSpan={5}>
                          {item.title} — {item.departmentName || 'Phòng ban'}
                          {state === 'NO_EXPENSE' ? ' (Không có chi phí)' : state === 'PENDING' ? ' (Chưa kê khai)' : ''}
                        </td>
                        <td className="border border-slate-500 p-2" colSpan={2}></td>
                      </tr>
                    );
                    if (state === 'SAVED' && r) {
                      r.items.forEach(it => {
                        stt += 1;
                        rows.push(
                          <tr key={`d-${it.expenseItemId}`}>
                            <td className="border border-slate-500 p-2 text-center">{stt}</td>
                            <td className="border border-slate-500 p-2">Hạng mục yêu cầu</td>
                            <td className="border border-slate-500 p-2">{it.itemName}</td>
                            <td className="border border-slate-500 p-2 text-center">{Math.floor(it.quantity || 0)}</td>
                            <td className="border border-slate-500 p-2 text-center">{it.unitName || ''}</td>
                            <td className="border border-slate-500 p-2 text-right">{it.unitPrice.toLocaleString('vi-VN')}</td>
                            <td className="border border-slate-500 p-2 text-right">{it.totalAmount.toLocaleString('vi-VN')}</td>
                          </tr>
                        );
                      });
                    }
                  });
                  orphanReports.forEach(r => {
                    rows.push(
                      <tr key={`ho-${r.expenseReportId}`} className="bg-slate-100/60">
                        <td className="border border-slate-500 p-2 font-bold" colSpan={5}>{r.logisticsItemTitle || 'Đơn yêu cầu'} — {r.departmentName || 'Phòng ban'}</td>
                        <td className="border border-slate-500 p-2" colSpan={2}></td>
                      </tr>
                    );
                    r.items.forEach(it => {
                      stt += 1;
                      rows.push(
                        <tr key={`do-${it.expenseItemId}`}>
                          <td className="border border-slate-500 p-2 text-center">{stt}</td>
                          <td className="border border-slate-500 p-2">Hạng mục yêu cầu</td>
                          <td className="border border-slate-500 p-2">{it.itemName}</td>
                          <td className="border border-slate-500 p-2 text-center">{Math.floor(it.quantity || 0)}</td>
                          <td className="border border-slate-500 p-2 text-center">{it.unitName || ''}</td>
                          <td className="border border-slate-500 p-2 text-right">{it.unitPrice.toLocaleString('vi-VN')}</td>
                          <td className="border border-slate-500 p-2 text-right">{it.totalAmount.toLocaleString('vi-VN')}</td>
                        </tr>
                      );
                    });
                  });
                  if (items.length > 0) {
                    rows.push(
                      <tr key="h-general" className="bg-slate-100/60">
                        <td className="border border-slate-500 p-2 font-bold" colSpan={5}>Chi phí chung (người phụ trách)</td>
                        <td className="border border-slate-500 p-2" colSpan={2}></td>
                      </tr>
                    );
                    items.forEach((it, i) => {
                      stt += 1;
                      rows.push(
                        <tr key={`g-${i}`}>
                          <td className="border border-slate-500 p-2 text-center">{stt}</td>
                          <td className="border border-slate-500 p-2">{ORIGIN_LABELS[it.itemOrigin] || it.itemOrigin}</td>
                          <td className="border border-slate-500 p-2">{it.itemName}</td>
                          <td className="border border-slate-500 p-2 text-center">{Math.floor(it.quantity || 0)}</td>
                          <td className="border border-slate-500 p-2 text-center">{it.unitName || ''}</td>
                          <td className="border border-slate-500 p-2 text-right">{it.unitPrice.toLocaleString('vi-VN')}</td>
                          <td className="border border-slate-500 p-2 text-right">{((it.quantity || 0) * (it.unitPrice || 0)).toLocaleString('vi-VN')}</td>
                        </tr>
                      );
                    });
                  }
                  return rows;
                })()}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={6} className="border border-slate-500 p-2 text-right font-black uppercase">Tổng chi phí đoàn</td>
                  <td className="border border-slate-500 p-2 text-right font-black">{grandTotal.toLocaleString('vi-VN')} ₫</td>
                </tr>
              </tfoot>
            </table>
            {reportNote && <p className="mt-4 text-[12px] italic">Ghi chú: {reportNote}</p>}
          </div>
        </>,
        document.body
      )}
      {/* ── Modal Xem trước / Xem Email Nhắc nhở Kê khai Chi phí ── */}
      <AnimatePresence>
        {showRemindEmailPreview && (
          <div className="fixed inset-0 z-[150] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
              className="bg-white rounded-2xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-hidden flex flex-col border border-gray-100 font-sans text-left"
            >
              {/* Header */}
              <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-gradient-to-r from-amber-50/60 to-white">
                <div className="flex items-center gap-2.5">
                  <div className="p-2 rounded-lg bg-amber-500/10 text-amber-700">
                    <Mail className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="font-bold text-gray-900 text-base">Xem trước / Chi tiết Email Nhắc nhở</h3>
                    <p className="text-xs text-gray-500">Mô phỏng thư thông báo gửi đến các đơn vị chưa kê khai chi phí</p>
                  </div>
                </div>
                <button
                  type="button"
                  onClick={() => setShowRemindEmailPreview(false)}
                  className="text-gray-400 hover:text-gray-600 p-1.5 rounded-lg hover:bg-gray-100 transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Body */}
              <div className="p-6 overflow-y-auto space-y-4 text-sm bg-slate-50/50">
                <div className="bg-white p-4 rounded-xl border border-gray-200 space-y-2 shadow-sm">
                  <div className="flex items-center justify-between border-b border-gray-100 pb-2">
                    <span className="text-xs font-semibold text-gray-400">TRẠNG THÁI EMAIL:</span>
                    {lastRemindSent ? (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-green-50 text-green-700 border border-green-200">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi nhắc nhở ({lastRemindSent.sentAt})
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-amber-50 text-amber-700 border border-amber-200">
                        <Clock className="w-3.5 h-3.5" /> Xem trước (Chưa gửi)
                      </span>
                    )}
                  </div>
                  <div className="grid grid-cols-1 gap-2 text-xs">
                    <div>
                      <span className="font-semibold text-gray-500">Người gửi: </span>
                      <span className="font-medium text-gray-800">Ban quản trị PEMS &lt;no-reply@mail.pems-fpt.site&gt;</span>
                    </div>
                    <div>
                      <span className="font-semibold text-gray-500">Người nhận ({lastRemindSent ? lastRemindSent.count : pendingCount} đơn vị): </span>
                      <span className="font-bold text-[#004c91]">
                        {lastRemindSent
                          ? lastRemindSent.recipients.join(', ')
                          : pendingCount > 0
                          ? deptRows.filter(r => r.state === 'PENDING').map(r => r.item.departmentName || r.item.title || `Đơn vị #${r.item.logisticsItemId}`).join(', ')
                          : 'Tất cả các phòng ban đã hoàn thành kê khai'}
                      </span>
                    </div>
                    <div>
                      <span className="font-semibold text-gray-500">Tiêu đề: </span>
                      <span className="font-bold text-gray-900">
                        [PEMS] Nhắc nhở hoàn tất kê khai chi phí đợt công tác
                      </span>
                    </div>
                  </div>
                </div>

                {/* Email Content Visual */}
                <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                  <div className="bg-[#004c91] text-white px-5 py-4 font-bold text-base flex items-center justify-between">
                    <span>PEMS Expense Reminder</span>
                    <span className="text-xs font-normal opacity-80">Thông báo nhắc nhở kê khai chi phí</span>
                  </div>
                  <div className="p-5 space-y-4 text-gray-700">
                    <p className="font-semibold">
                      Kính gửi <strong className="text-gray-900">Người phụ trách kê khai chi phí các phòng ban</strong>,
                    </p>
                    <p className="text-sm leading-relaxed">
                      Ban quản trị hệ thống PEMS trân trọng thông báo nhắc nhở các đơn vị/phòng ban chưa hoàn thành việc kê khai chi phí thực tế cho đợt tiếp khách công tác.
                    </p>

                    <div className="bg-amber-50/70 rounded-xl p-4 border border-amber-200 space-y-2 text-xs">
                      <div className="font-bold text-amber-900 text-xs mb-2 border-b border-amber-200 pb-1">
                        DANH SÁCH ĐƠN YÊU CẦN KÊ KHAI CHI PHÍ ({pendingCount} ĐƠN VỊ)
                      </div>
                      {deptRows.filter(r => r.state === 'PENDING').length > 0 ? (
                        <ul className="space-y-1.5">
                          {deptRows.filter(r => r.state === 'PENDING').map((r, idx) => (
                            <li key={idx} className="flex items-center justify-between bg-white p-2 rounded border border-amber-100">
                              <span className="font-semibold text-gray-800">{r.item.title || 'Hạng mục dịch vụ'}</span>
                              <span className="text-gray-500">{r.item.departmentName || 'Phòng ban chuyên trách'}</span>
                            </li>
                          ))}
                        </ul>
                      ) : (
                        <p className="italic text-gray-500">Hiện tại tất cả các đơn vị đã kê khai đầy đủ chi phí.</p>
                      )}

                      <div className="pt-2">
                        <span className="font-bold text-amber-900 block mb-1">GHI CHÚ NHẮC NHỞ BỔ SUNG (CÓ THỂ SỬA TRƯỚC KHI GỬI):</span>
                        {!lastRemindSent ? (
                          <textarea
                            value={remindNote}
                            onChange={(e) => setRemindNote(e.target.value)}
                            placeholder="Nhập/chỉnh sửa nội dung ghi chú nhắc nhở gửi các phòng ban..."
                            rows={3}
                            className="w-full text-xs p-2.5 rounded-lg border border-amber-200 focus:border-[#004c91] outline-none bg-white text-gray-800 shadow-xs"
                          />
                        ) : (
                          <p className="italic text-gray-600 bg-white p-2.5 rounded-lg border border-amber-100">{remindNote || '(Không có ghi chú bổ sung)'}</p>
                        )}
                      </div>
                    </div>

                    <p className="text-xs text-gray-500">
                      Vui lòng truy cập hệ thống PEMS, chọn đợt công tác tương ứng và cập nhật bảng kê khai chi phí chung để Ban quản trị tổng hợp và nghiệm thu chi phí đoàn.
                    </p>
                    <div className="pt-3 border-t border-gray-100 text-xs text-gray-400">
                      Trân trọng,<br />
                      <strong>Ban quản trị Hệ thống PEMS — FPT University</strong>
                    </div>
                  </div>
                </div>
              </div>

              {/* Footer */}
              <div className="flex items-center justify-between px-6 py-3 border-t border-gray-100 bg-gray-50">
                <button
                  type="button"
                  onClick={() => setShowRemindEmailPreview(false)}
                  className="px-5 py-2 rounded-xl text-sm font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors"
                >
                  Đóng
                </button>
                {!lastRemindSent && (
                  <button
                    type="button"
                    onClick={() => {
                      setShowRemindEmailPreview(false);
                      handleRemind();
                    }}
                    disabled={reminding || pendingCount === 0}
                    className="px-5 py-2 rounded-xl text-sm font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                  >
                    {reminding ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                    Gửi nhắc nhở ngay
                  </button>
                )}
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>
    </div>
  );
}
