/**
 * Trang DeptReportManagement — Báo cáo phòng ban của Department Leader tại /dashboard/reports.
 * Bố cục 3 phần: (1) Báo cáo nhiệm vụ (thư mời + đơn yêu cầu), (2) Nhân sự phòng ban,
 * (3) Thống kê chi phí — đơn đã hoàn thành (đã ký nghiệm thu) kèm số tiền phòng ban đã
 * kê khai, tổng tiền + xuất thống kê PDF. Bộ lọc duy nhất là khoảng thời gian, dùng
 * chung cho cả 3 phần. Dữ liệu từ GET /reports/dept-leader-report-v2.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertTriangle, CalendarRange, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight,
  ChevronUp, Download, FileText, Loader2, RefreshCw, Send, Star, TrendingDown, TrendingUp,
  Users, X, XCircle, DollarSign, Eye, Mail, Clock,
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { reportsApi } from '../../../features/reports/api/reportsApi';
import type {
  DeptLeaderInvoiceItemV2, DeptLeaderReportV2, DeptLeaderV2Filters,
  DeptLeaderV2PersonnelRow, DeptLeaderV2Preset,
} from '../../../features/reports/types/deptLeaderReportsV2.types';
import { TaskHandoverModal } from '../departments/TaskHandoverModal';
import { useGuardedSend } from '../../../features/reports/hooks/useGuardedSend';
import { useIdempotentSend, attemptIsOver, sendFailureMessage } from '../../../features/reports/hooks/useIdempotentSend';
import { renderPrintDocument } from '../../../features/reports/print/printDocument';
import {
  DEPT_INVOICE_STATS_CSS,
  buildDeptInvoiceStatsDocument,
  deptInvoiceStatsTitle,
} from '../../../features/reports/print/deptInvoiceStatsDocument';

const CHART_BLUE = '#1e6fc0';
const CHART_GREEN = '#0a8a44';

const PRESETS: { value: DeptLeaderV2Preset; label: string }[] = [
  { value: 'THIS_MONTH', label: 'Tháng này' },
  { value: 'THIS_QUARTER', label: 'Quý này' },
  { value: 'THIS_YEAR', label: 'Năm nay' },
  { value: 'CUSTOM', label: 'Tùy chỉnh' },
];

const GRANULARITY_LABELS: Record<string, string> = {
  YEAR: 'năm', MONTH: 'tháng', WEEK: 'tuần', DAY: 'ngày', HOUR: 'giờ',
};

const ITEM_TYPE_LABELS: Record<string, string> = {
  ROOM: 'Phòng họp', TRANSPORT: 'Xe / di chuyển', MEAL: 'Ẩm thực', EQUIPMENT: 'Thiết bị',
  BANNER: 'Băng rôn', LED: 'LED', OTHER: 'Khác',
};

const thClass = 'px-3 py-2.5 text-[11px] font-bold text-slate-400 uppercase tracking-wide whitespace-nowrap text-left';
const tdClass = 'px-3 py-2.5 text-sm text-slate-600';

const vnMoney = (v: number) => `${v.toLocaleString('vi-VN')} ₫`;
const fmtDate = (iso: string) => (iso ? `${iso.slice(8, 10)}/${iso.slice(5, 7)}/${iso.slice(0, 4)}` : '—');
const fmtDateTime = (iso: string) => (iso ? `${iso.slice(11, 16)} ${fmtDate(iso)}` : '—');

/** Số dòng mỗi trang của bảng nhân sự. */
const PAGE_SIZE = 10;

type RankSort = 'DEFAULT' | 'BEST' | 'WORST';

/** Điểm xếp hạng tốt/kém: chuẩn hóa (hoàn thành, giờ làm, feedback) về 0..1 rồi cộng lại. */
const rankScore = (completed: number, maxCompleted: number, hours: number, maxHours: number, feedback: number | null) =>
  (maxCompleted > 0 ? completed / maxCompleted : 0)
  + (maxHours > 0 ? hours / maxHours : 0)
  + ((feedback ?? 0) / 5);

/** Cặp nút "Tốt nhất / Kém nhất" — bấm lại nút đang chọn để bỏ xếp hạng. */
function RankSortButtons({ sort, onChange }: { sort: RankSort; onChange: (s: RankSort) => void }) {
  return (
    <div className="flex items-center gap-1.5">
      <button
        type="button"
        onClick={() => onChange(sort === 'BEST' ? 'DEFAULT' : 'BEST')}
        title="Xếp hạng theo hoàn thành + giờ làm việc + feedback, tốt nhất lên đầu"
        className={`inline-flex items-center gap-1 px-3 py-1.5 rounded-full text-xs font-bold border transition-colors cursor-pointer ${
          sort === 'BEST' ? 'bg-emerald-600 text-white border-emerald-600' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
        }`}
      >
        <TrendingUp className="w-3.5 h-3.5" /> Tốt nhất
      </button>
      <button
        type="button"
        onClick={() => onChange(sort === 'WORST' ? 'DEFAULT' : 'WORST')}
        title="Xếp hạng theo hoàn thành + giờ làm việc + feedback, kém nhất lên đầu"
        className={`inline-flex items-center gap-1 px-3 py-1.5 rounded-full text-xs font-bold border transition-colors cursor-pointer ${
          sort === 'WORST' ? 'bg-rose-600 text-white border-rose-600' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
        }`}
      >
        <TrendingDown className="w-3.5 h-3.5" /> Kém nhất
      </button>
    </div>
  );
}

/** Thanh phân trang client-side gắn dưới bảng. */
function Pagination({ page, total, onChange }: { page: number; total: number; onChange: (p: number) => void }) {
  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  if (totalPages <= 1) return null;
  return (
    <div className="flex items-center justify-between gap-3 px-3 py-2 bg-slate-50 border-t border-slate-200">
      <span className="text-[11px] font-medium text-slate-400">
        {(page - 1) * PAGE_SIZE + 1}–{Math.min(page * PAGE_SIZE, total)} / {total} dòng
      </span>
      <div className="flex items-center gap-1">
        <button
          type="button"
          onClick={() => onChange(page - 1)}
          disabled={page <= 1}
          title="Trang trước"
          className="p-1.5 rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-slate-100 disabled:opacity-40 cursor-pointer disabled:cursor-default"
        >
          <ChevronLeft className="w-4 h-4" />
        </button>
        <span className="text-xs font-bold text-slate-600 px-2 whitespace-nowrap">{page}/{totalPages}</span>
        <button
          type="button"
          onClick={() => onChange(page + 1)}
          disabled={page >= totalPages}
          title="Trang sau"
          className="p-1.5 rounded-lg border border-slate-200 bg-white text-slate-500 hover:bg-slate-100 disabled:opacity-40 cursor-pointer disabled:cursor-default"
        >
          <ChevronRight className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}

function StatTile({ label, value, sub, tone = 'blue', icon }: {
  label: string; value: React.ReactNode; sub?: string; tone?: 'blue' | 'green' | 'red' | 'amber' | 'violet' | 'slate'; icon?: React.ReactNode;
}) {
  const tones: Record<string, string> = {
    blue: 'bg-blue-50 text-[#004c91] border-blue-100',
    green: 'bg-emerald-50 text-emerald-700 border-emerald-100',
    red: 'bg-rose-50 text-rose-700 border-rose-100',
    amber: 'bg-amber-50 text-amber-700 border-amber-100',
    violet: 'bg-violet-50 text-violet-700 border-violet-100',
    slate: 'bg-slate-50 text-slate-700 border-slate-200',
  };
  return (
    <div className={`rounded-xl border px-3 py-2.5 ${tones[tone]}`}>
      <div className="flex items-center justify-between gap-2">
        <p className="text-[10px] font-bold uppercase tracking-wide opacity-80 truncate">{label}</p>
        {icon}
      </div>
      <p className="text-lg font-extrabold mt-0.5 leading-tight">{value}</p>
      {sub && <p className="text-[10px] font-medium opacity-75">{sub}</p>}
    </div>
  );
}

function Section({ index, title, subtitle, open, onToggle, children }: {
  index: number; title: string; subtitle?: string; open: boolean; onToggle: () => void; children: React.ReactNode;
}) {
  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <button
        type="button"
        onClick={onToggle}
        className="w-full flex items-center gap-3 px-4 py-3 text-left hover:bg-slate-50/70 transition-colors cursor-pointer"
      >
        <span className="w-7 h-7 rounded-lg bg-[#004c91] text-white flex items-center justify-center text-xs font-black shrink-0">{index}</span>
        <span className="flex-1 min-w-0">
          <span className="block text-base font-extrabold text-[#004c91] leading-tight">{title}</span>
          {subtitle && <span className="block text-[11px] text-slate-400 mt-0.5">{subtitle}</span>}
        </span>
        {open ? <ChevronUp className="w-4 h-4 text-slate-400 shrink-0" /> : <ChevronDown className="w-4 h-4 text-slate-400 shrink-0" />}
      </button>
      {open && <div className="px-4 pb-4 space-y-4">{children}</div>}
    </section>
  );
}

export function DeptReportManagement() {
  const userStr = typeof window !== 'undefined' ? localStorage.getItem("currentUser") : null;
  const user = userStr ? JSON.parse(userStr) : null;
  const isDeptLeader = user?.role?.toUpperCase() === 'DEPARTMENT' && user?.subRole?.toUpperCase() === 'LEADER';
  const isDeptStaff = user?.role?.toUpperCase() === 'DEPARTMENT' && !isDeptLeader;

  // ── Bộ lọc thời gian (chung cho cả 3 phần) ──
  const [filters, setFilters] = useState<DeptLeaderV2Filters>({ preset: 'THIS_MONTH', fromDate: '', toDate: '' });
  const [data, setData] = useState<DeptLeaderReportV2 | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchReport = useCallback(async (f: DeptLeaderV2Filters) => {
    setLoading(true);
    setError(null);
    try {
      const res = await reportsApi.getDeptLeaderReportV2(f);
      setData(res);
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể tải báo cáo. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchReport(filters); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);

  const [openSections, setOpenSections] = useState({ tasks: true, personnel: true, invoice: false });
  const toggleSection = (key: keyof typeof openSections) =>
    setOpenSections((s) => ({ ...s, [key]: !s[key] }));

  // ── Xuất báo cáo (PDF/Excel/CSV) — chọn phần ──
  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [exportSections, setExportSections] = useState<string[]>(isDeptStaff ? ['TASKS'] : ['TASKS', 'PERSONNEL']);
  const [exporting, setExporting] = useState(false);
  const toggleExportSection = (s: string) =>
    setExportSections((cur) => (cur.includes(s) ? cur.filter((x) => x !== s) : [...cur, s]));

  const exportReport = async (format: 'PDF' | 'EXCEL' | 'CSV') => {
    if (exportSections.length === 0) {
      toast.error('Chọn ít nhất một phần để xuất.');
      return;
    }
    setExporting(true);
    try {
      const file = await reportsApi.exportDeptLeaderReportV2({
        preset: filters.preset,
        fromDate: filters.preset === 'CUSTOM' ? filters.fromDate : undefined,
        toDate: filters.preset === 'CUSTOM' ? filters.toDate : undefined,
        exportFormat: format,
        sections: exportSections,
      });
      const url = URL.createObjectURL(file.blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = file.fileName;
      a.click();
      URL.revokeObjectURL(url);
      toast.success(`Đã xuất báo cáo ${format === 'EXCEL' ? 'Excel' : format} thành công.`);
      setExportMenuOpen(false);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Xuất báo cáo thất bại.');
    } finally {
      setExporting(false);
    }
  };

  // ── Phần 2: ghi chú + gửi email báo cáo nhân sự phòng ban ──
  const [personnelNotes, setPersonnelNotes] = useState<Record<number, string>>({});
  const [previewMemberRow, setPreviewMemberRow] = useState<DeptLeaderV2PersonnelRow | null>(null);
  const [sentMemberMap, setSentMemberMap] = useState<Record<number, string>>({});
  const personnelSend = useGuardedSend<number>();
  // Same key across a retry of the SAME send; a new key only when the attempt is over (G11 / R-103).
  const idem = useIdempotentSend();
  const [personnelSort, setPersonnelSort] = useState<RankSort>('DEFAULT');
  const [personnelPage, setPersonnelPage] = useState(1);

  const rankedPersonnelRows = useMemo(() => {
    const rows = data?.personnel.rows ?? [];
    if (personnelSort === 'DEFAULT') return rows;
    const maxTasks = Math.max(0, ...rows.map((r) => r.taskCount));
    const maxHours = Math.max(0, ...rows.map((r) => r.totalHours));
    const score = (r: DeptLeaderV2PersonnelRow) => rankScore(r.taskCount, maxTasks, r.totalHours, maxHours, r.feedbackAverage);
    const sorted = [...rows].sort((a, b) => score(b) - score(a));
    return personnelSort === 'BEST' ? sorted : sorted.reverse();
  }, [data, personnelSort]);
  const pagedPersonnelRows = rankedPersonnelRows.slice((personnelPage - 1) * PAGE_SIZE, personnelPage * PAGE_SIZE);

  // Dữ liệu kỳ mới → quay về trang 1.
  useEffect(() => { setPersonnelPage(1); }, [data]);

  // Guarded per row: a repeat click while this row is sending does nothing, and a send started on
  // another row no longer clears this row's flag (see useGuardedSend).
  const sendPersonnelReport = (row: DeptLeaderV2PersonnelRow) =>
    personnelSend.send(row.userId, async () => {
      try {
        const res = await reportsApi.sendDeptLeaderPersonnelReport({
          userId: row.userId,
          fromDate: data?.fromDate,
          toDate: data?.toDate,
          note: personnelNotes[row.userId]?.trim() || undefined,
        }, idem.keyFor('dl-personnel-report', row.userId));
        idem.complete('dl-personnel-report', row.userId);
        const nowStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) + ' ' + new Date().toLocaleDateString('vi-VN');
        setSentMemberMap((prev) => ({ ...prev, [row.userId]: nowStr }));
        toast.success(res.message || `Đã gửi báo cáo hiệu suất qua email cho ${row.fullName}.`);
      } catch (e: any) {
        if (attemptIsOver(e)) idem.complete('dl-personnel-report', row.userId);
        toast.error(sendFailureMessage(e, 'Gửi báo cáo thất bại.'));
      }
    });

  // ── Phần 3: xuất hóa đơn ──
  const [invoiceRange, setInvoiceRange] = useState<{ fromDate: string; toDate: string }>({ fromDate: '', toDate: '' });
  const [invoiceItems, setInvoiceItems] = useState<DeptLeaderInvoiceItemV2[]>([]);
  const [invoiceLoading, setInvoiceLoading] = useState(false);
  const [invoiceLoaded, setInvoiceLoaded] = useState(false);
  const [viewItem, setViewItem] = useState<DeptLeaderInvoiceItemV2 | null>(null);

  useEffect(() => {
    if (data && !invoiceRange.fromDate) {
      setInvoiceRange({ fromDate: data.fromDate, toDate: data.toDate });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data]);

  const loadInvoiceItems = async () => {
    if (!invoiceRange.fromDate || !invoiceRange.toDate) {
      toast.error('Chọn khoảng ngày để lấy danh sách đơn.');
      return;
    }
    setInvoiceLoading(true);
    try {
      const items = await reportsApi.getDeptLeaderInvoiceItemsV2(invoiceRange.fromDate, invoiceRange.toDate);
      setInvoiceItems(items);
      setInvoiceLoaded(true);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Không tải được danh sách đơn.');
    } finally {
      setInvoiceLoading(false);
    }
  };



  // Tổng số tiền phòng ban đã kê khai của các đơn trong danh sách (đơn "Không có
  // chi phí" có totalExpense = 0 nên cộng thẳng).
  const invoiceTotal = invoiceItems.reduce((s, it) => s + it.totalExpense, 0);

  // "Xuất thống kê PDF" mở hộp thoại IN qua 1 cửa sổ mới chỉ chứa bảng thống kê —
  // không phụ thuộc CSS/layout của trang dashboard nên không bị in trắng.
  // Tài liệu được dựng bằng DOM (createElement/textContent), không nối chuỗi HTML:
  // xem features/reports/print/printDocument.ts.
  const exportInvoicePdf = () => {
    if (invoiceItems.length === 0) {
      toast.error('Chưa có đơn nào để xuất thống kê.');
      return;
    }

    const root = buildDeptInvoiceStatsDocument({
      departmentName: data?.departmentName ?? '',
      periodFrom: fmtDate(invoiceRange.fromDate),
      periodTo: fmtDate(invoiceRange.toDate),
      grandTotal: invoiceTotal,
      lines: invoiceItems.map(it => ({
        title: it.title,
        delegationName: it.delegationName,
        usageDate: fmtDate(it.usageStartAt),
        quantity: it.quantity,
        totalExpense: it.totalExpense,
        noExpense: it.noExpense,
      })),
    });

    const win = window.open('', '_blank', 'width=980,height=720');
    if (!win) {
      toast.error('Trình duyệt đang chặn popup — hãy cho phép popup cho trang này rồi thử lại.');
      return;
    }
    renderPrintDocument(win, {
      title: deptInvoiceStatsTitle(data?.departmentName ?? ''),
      css: DEPT_INVOICE_STATS_CSS,
      root,
    });
    win.focus();
    setTimeout(() => win.print(), 350);
    toast.success('Đã mở bản in thống kê — chọn "Save as PDF" để lưu.');
  };

  // Biên bản đã ký giữa 2 bên — TaskHandoverModal (chỉ xem) nhận DTO PascalCase.
  const toHandoverDto = (it: DeptLeaderInvoiceItemV2) => ({
    LogisticsItemId: it.logisticsItemId,
    Title: it.title,
    Quantity: it.quantity,
    ItemType: it.itemType,
    UsageEndTime: it.usageEndAt ? it.usageEndAt.slice(11, 16) : undefined,
    UsageDate: it.usageEndAt ? `${it.usageEndAt.slice(8, 10)}-${it.usageEndAt.slice(5, 7)}-${it.usageEndAt.slice(0, 4)}` : undefined,
    DelegationName: it.delegationName,
    SenderName: it.hostName,
    AssigneeName: it.assigneeName,
    BorrowNote: it.borrowNote,
    ReturnNote: it.returnNote,
    BorrowProviderSignature: it.borrowProviderSignature ? { Name: it.borrowProviderSignature.name, SignedAt: it.borrowProviderSignature.signedAt } : null,
    BorrowBorrowerSignature: it.borrowBorrowerSignature ? { Name: it.borrowBorrowerSignature.name, SignedAt: it.borrowBorrowerSignature.signedAt } : null,
    ReturnProviderSignature: it.returnProviderSignature ? { Name: it.returnProviderSignature.name, SignedAt: it.returnProviderSignature.signedAt } : null,
    ReturnBorrowerSignature: it.returnBorrowerSignature ? { Name: it.returnBorrowerSignature.name, SignedAt: it.returnBorrowerSignature.signedAt } : null,
  });

  const t = data?.tasks;
  const p = data?.personnel;

  return (
    <div className="w-full space-y-8 pb-16 animate-in fade-in duration-300">
      {/* ── Header + nút xuất báo cáo ── */}
      <div className="border-b border-gray-100 pb-4 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">
            {isDeptStaff ? 'Báo cáo nhiệm vụ & Chi phí' : 'Báo cáo phòng ban'}
          </h1>
          <p className="text-slate-500 mt-2">
            {data
              ? `${data.departmentName} · Kỳ ${fmtDate(data.fromDate)} – ${fmtDate(data.toDate)}`
              : isDeptStaff
                ? 'Báo cáo nhiệm vụ cá nhân và thống kê chi phí phòng ban.'
                : 'Báo cáo vận hành phòng ban của Department Leader.'}
          </p>
        </div>

        <div className="relative">
          <button
            type="button"
            onClick={() => setExportMenuOpen((v) => !v)}
            disabled={!data || exporting}
            className="inline-flex items-center gap-2 px-4 py-2.5 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:opacity-90 disabled:opacity-50 transition-all cursor-pointer"
          >
            {exporting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
            Xuất báo cáo
            <ChevronDown className="w-4 h-4" />
          </button>
          {exportMenuOpen && (
            <>
              <div className="fixed inset-0 z-20" onClick={() => setExportMenuOpen(false)} />
              <div className="absolute right-0 top-full mt-2 w-64 bg-white border border-slate-200 rounded-2xl shadow-xl z-30 p-3 space-y-2">
                <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wide px-1">Chọn phần xuất</p>
                {!isDeptStaff ? (
                  <>
                    {[
                      { key: 'TASKS', label: 'Phần 1 · Nhiệm vụ' },
                      { key: 'PERSONNEL', label: 'Phần 2 · Nhân sự' },
                    ].map((s) => (
                      <label key={s.key} className="flex items-center gap-2 px-1 py-0.5 text-sm text-slate-700 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={exportSections.includes(s.key)}
                          onChange={() => toggleExportSection(s.key)}
                          className="accent-[#004c91]"
                        />
                        {s.label}
                      </label>
                    ))}
                    <label className="flex items-center gap-2 px-1 py-0.5 text-sm font-bold text-[#004c91] cursor-pointer border-t border-slate-100 pt-2">
                      <input
                        type="checkbox"
                        checked={exportSections.length === 2}
                        onChange={() => setExportSections(exportSections.length === 2 ? [] : ['TASKS', 'PERSONNEL'])}
                        className="accent-[#004c91]"
                      />
                      Chọn tất cả
                    </label>
                  </>
                ) : (
                  <label className="flex items-center gap-2 px-1 py-0.5 text-sm font-bold text-slate-700 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={exportSections.includes('TASKS')}
                      onChange={() => toggleExportSection('TASKS')}
                      className="accent-[#004c91]"
                    />
                    Báo cáo nhiệm vụ
                  </label>
                )}
                <div className="border-t border-slate-100 pt-2 space-y-1">
                  {([['EXCEL', 'Excel (.xlsx)'], ['PDF', 'PDF (.pdf)'], ['CSV', 'CSV (.csv)']] as const).map(([fmt2, label]) => (
                    <button
                      key={fmt2}
                      type="button"
                      onClick={() => exportReport(fmt2)}
                      disabled={exporting}
                      className="w-full flex items-center gap-2 px-2 py-2 text-sm font-semibold text-slate-700 hover:bg-blue-50 rounded-lg transition-colors disabled:opacity-50 cursor-pointer"
                    >
                      <FileText className="w-4 h-4 text-slate-400" /> {label}
                    </button>
                  ))}
                </div>
              </div>
            </>
          )}
        </div>
      </div>

      {/* ── Bộ lọc thời gian (chung cho cả 3 phần) ── */}
      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm px-4 py-3 flex flex-wrap items-center gap-3">
        <span className="flex items-center gap-1.5 text-sm font-bold text-slate-600">
          <CalendarRange className="w-4 h-4 text-[#f37021]" /> Thời gian
        </span>
        <div className="flex rounded-xl border border-slate-200 overflow-hidden">
          {PRESETS.map((pr) => (
            <button
              key={pr.value}
              type="button"
              onClick={() => {
                const nextFilters = { preset: pr.value, fromDate: '', toDate: '' };
                setFilters(nextFilters);
                if (pr.value !== 'CUSTOM') fetchReport(nextFilters);
              }}
              className={`px-3.5 py-2 text-xs font-bold transition-colors cursor-pointer ${
                filters.preset === pr.value ? 'bg-[#004c91] text-white' : 'bg-white text-slate-600 hover:bg-slate-50'
              }`}
            >
              {pr.label}
            </button>
          ))}
        </div>
        {filters.preset === 'CUSTOM' && (
          <div className="flex items-center gap-2">
            <input type="date" value={filters.fromDate} onChange={(e) => setFilters((f) => ({ ...f, fromDate: e.target.value }))}
              className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm outline-none focus:border-[#004c91]" />
            <span className="text-slate-400 text-sm">→</span>
            <input type="date" value={filters.toDate} onChange={(e) => setFilters((f) => ({ ...f, toDate: e.target.value }))}
              className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm outline-none focus:border-[#004c91]" />
          </div>
        )}
        <button
          type="button"
          onClick={() => fetchReport(filters)}
          disabled={loading}
          className="px-4 py-2 bg-[#f37021] text-white text-xs font-black rounded-xl hover:opacity-90 disabled:opacity-50 transition-all cursor-pointer"
        >
          Áp dụng
        </button>
        <button
          type="button"
          onClick={() => {
            const defaultFilters = { preset: 'THIS_MONTH' as const, fromDate: '', toDate: '' };
            setFilters(defaultFilters);
            fetchReport(defaultFilters);
          }}
          disabled={loading}
          className="ml-auto p-2 rounded-xl hover:bg-slate-100 text-slate-500 transition-colors cursor-pointer"
          title="Đặt lại về mặc định (Tháng này)"
        >
          {loading ? <Loader2 className="w-4 h-4 animate-spin text-[#004c91]" /> : <RefreshCw className="w-4 h-4" />}
        </button>
      </div>

      {error && (
        <div className="bg-rose-50 border border-rose-200 rounded-2xl px-5 py-4 text-sm text-rose-700 flex items-center gap-2">
          <AlertTriangle className="w-4 h-4 shrink-0" /> {error}
        </div>
      )}

      {loading && !data ? (
        <div className="py-24 text-center text-slate-500">
          <Loader2 className="w-7 h-7 mx-auto mb-3 animate-spin text-[#004c91]" />
          <p className="text-sm font-medium">Đang tổng hợp báo cáo...</p>
        </div>
      ) : data && (
        <>
          {/* ═══ 1 · Báo cáo nhiệm vụ ═══ */}
          <Section
            index={1}
            title="Báo cáo nhiệm vụ"
            subtitle={isDeptStaff ? "Số liệu thư mời tham gia và đơn yêu cầu hậu cần do bạn phụ trách trong kỳ." : "Số liệu thư mời tham gia và đơn yêu cầu hậu cần gửi tới phòng ban trong kỳ."}
            open={openSections.tasks}
            onToggle={() => toggleSection('tasks')}
          >
            <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-5 gap-3">
              <StatTile label="Tổng nhiệm vụ" value={t!.totalTasks} tone="blue" icon={<Users className="w-4 h-4 opacity-60" />} />
              <StatTile label="Đã hoàn thành" value={t!.completed} tone="green" icon={<CheckCircle2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Từ chối" value={t!.rejected} tone="red" icon={<XCircle className="w-4 h-4 opacity-60" />} />
              <StatTile label="Chưa hoàn thành" value={t!.notCompleted} tone="amber" />
              <StatTile
                label="Feedback"
                value={t!.feedbackAverage != null ? `${t!.feedbackAverage.toFixed(1)}★` : '—'}
                sub={`${t!.feedbackTotalStars} sao / ${t!.feedbackCount} lượt`}
                tone="violet"
                icon={<Star className="w-4 h-4 opacity-60" />}
              />
            </div>

            <div>
              <h3 className="text-sm font-bold text-slate-700 mb-1">Tiến độ nhiệm vụ</h3>
              <p className="text-xs text-slate-400 mb-3">
                Số nhiệm vụ (thư mời + đơn yêu cầu) theo {GRANULARITY_LABELS[t!.trendGranularity] ?? 'tháng'} — mốc trục thời gian tự đổi theo khoảng lọc.
              </p>
              <div className="h-72">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={t!.trend} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="monthLabel" tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <YAxis allowDecimals={false} tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <Tooltip labelStyle={{ fontWeight: 700 }} contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    <Line type="monotone" dataKey="totalTasks" name="Tổng nhiệm vụ" stroke={CHART_BLUE} strokeWidth={2.5} dot={{ r: 3 }} />
                    <Line type="monotone" dataKey="completed" name="Hoàn thành" stroke={CHART_GREEN} strokeWidth={2.5} dot={{ r: 3 }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>
          </Section>

          {/* ═══ 2 · Báo cáo nhân sự (Chỉ Dept Leader) ═══ */}
          {!isDeptStaff && (
            <Section
              index={2}
              title="Báo cáo nhân sự"
              subtitle="Hiệu suất của Department Leader và Dept Staff trong kỳ."
              open={openSections.personnel}
              onToggle={() => toggleSection('personnel')}
            >
              <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
                <StatTile label="Tổng nhân sự" value={p!.totalStaff} tone="blue" icon={<Users className="w-4 h-4 opacity-60" />} />
                <StatTile
                  label="Feedback trung bình"
                  value={p!.averageFeedback != null ? `${p!.averageFeedback.toFixed(1)}★` : '—'}
                  tone="violet"
                  icon={<Star className="w-4 h-4 opacity-60" />}
                />
              </div>

              {/* Xếp hạng nhân sự (hoàn thành + giờ làm + feedback) */}
              <div className="flex items-center justify-end">
                <RankSortButtons sort={personnelSort} onChange={(s) => { setPersonnelSort(s); setPersonnelPage(1); }} />
              </div>

              <div className="rounded-2xl border border-slate-200 overflow-hidden">
                <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <thead className="bg-slate-50">
                    <tr>
                      <th className={thClass}>STT</th>
                      <th className={thClass}>Tên</th>
                      <th className={thClass}>Số nhiệm vụ phụ trách</th>
                      <th className={thClass}>Tổng giờ làm việc</th>
                      <th className={thClass}>Feedback</th>
                      <th className={thClass}>Từ chối</th>
                      <th className={thClass}>Ghi chú</th>
                      <th className={thClass}></th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {p!.rows.length === 0 && (
                      <tr><td colSpan={8} className="px-4 py-8 text-center text-sm text-slate-400">Không có nhân sự nào.</td></tr>
                    )}
                    {pagedPersonnelRows.map((row, idx) => {
                      const lowFeedback = row.feedbackAverage != null && row.feedbackAverage < 2;
                      return (
                        <tr key={row.userId} className={lowFeedback ? 'bg-rose-50/50' : idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                          <td className={`${tdClass} whitespace-nowrap`}>{(personnelPage - 1) * PAGE_SIZE + idx + 1}</td>
                          <td className={`${tdClass} font-semibold text-slate-800`}>
                            <span className="flex items-center gap-1.5">
                              {row.fullName}
                              {row.role === 'DEPT_LEADER' && (
                                <Star className="w-3.5 h-3.5 text-amber-400 fill-amber-400 shrink-0" aria-label="Department Leader" />
                              )}
                            </span>
                            <span className="block text-[11px] font-normal text-slate-400">{row.email}</span>
                          </td>
                          <td className={`${tdClass} whitespace-nowrap`}>{row.taskCount}</td>
                          <td className={`${tdClass} whitespace-nowrap`}>{row.totalHours.toLocaleString('vi-VN')} giờ</td>
                          <td className={`${tdClass} whitespace-nowrap`}>
                            {row.feedbackAverage != null ? (
                              <span className={`inline-flex items-center gap-1 font-bold ${lowFeedback ? 'text-rose-600' : 'text-slate-700'}`}>
                                {row.feedbackAverage.toFixed(1)}★
                                <span className="text-[11px] font-normal text-slate-400">({row.feedbackCount})</span>
                                {lowFeedback && <AlertTriangle className="w-3.5 h-3.5 text-rose-500" aria-label="Feedback dưới 2 sao" />}
                              </span>
                            ) : <span className="text-slate-400">—</span>}
                          </td>
                          <td className={`${tdClass} whitespace-nowrap`}>{row.declinedCount}</td>
                          <td className={tdClass}>
                            <input
                              type="text"
                              value={personnelNotes[row.userId] ?? ''}
                              onChange={(e) => setPersonnelNotes((s) => ({ ...s, [row.userId]: e.target.value }))}
                              placeholder="Ghi chú..."
                              className="w-40 border border-slate-200 rounded-lg px-2 py-1.5 text-xs outline-none focus:border-[#004c91]"
                            />
                          </td>
                          <td className={`${tdClass} whitespace-nowrap`}>
                            <div className="inline-flex items-center">
                              <button
                                type="button"
                                onClick={() => sendPersonnelReport(row)}
                                disabled={personnelSend.isSending(row.userId)}
                                title={`Gửi báo cáo hiệu suất qua email cho ${row.fullName}`}
                                className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-[#004c91] text-xs font-bold rounded-l-lg border border-blue-200 transition-colors disabled:opacity-50 cursor-pointer"
                              >
                                {personnelSend.isSending(row.userId) ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                                Gửi
                              </button>
                              <button
                                type="button"
                                onClick={() => setPreviewMemberRow(row)}
                                title={`Xem trước / Xem email báo cáo hiệu suất cho ${row.fullName}`}
                                className="inline-flex items-center justify-center p-1.5 bg-blue-100 hover:bg-blue-200 text-[#004c91] text-xs font-bold rounded-r-lg border border-l-0 border-blue-200 transition-colors cursor-pointer outline-none"
                              >
                                <Eye className="w-3.5 h-3.5" />
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
                </div>
                <Pagination page={personnelPage} total={rankedPersonnelRows.length} onChange={setPersonnelPage} />
              </div>
            </Section>
          )}

          {/* ═══ Thống kê chi phí ═══ */}
          <Section
            index={isDeptStaff ? 2 : 3}
            title="Thống kê chi phí"
            subtitle="Đơn hậu cần đã hoàn thành (đã ký nghiệm thu) kèm số tiền phòng ban đã kê khai trong khoảng ngày."
            open={openSections.invoice}
            onToggle={() => toggleSection('invoice')}
          >
            {!invoiceLoaded ? (
              <div className="flex flex-wrap items-center gap-3 bg-orange-50/50 border border-orange-100 rounded-2xl p-4">
                <span className="text-xs font-bold text-slate-600">Khoảng ngày:</span>
                <input type="date" value={invoiceRange.fromDate}
                  onChange={(e) => setInvoiceRange((s) => ({ ...s, fromDate: e.target.value }))}
                  className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm bg-white outline-none focus:border-[#f37021]" />
                <span className="text-slate-400 text-sm">→</span>
                <input type="date" value={invoiceRange.toDate}
                  onChange={(e) => setInvoiceRange((s) => ({ ...s, toDate: e.target.value }))}
                  className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm bg-white outline-none focus:border-[#f37021]" />
                <button
                  type="button"
                  onClick={loadInvoiceItems}
                  disabled={invoiceLoading}
                  className="inline-flex items-center gap-1.5 px-4 py-2 bg-[#f37021] text-white text-xs font-black rounded-xl hover:opacity-90 disabled:opacity-50 transition-all cursor-pointer"
                >
                  {invoiceLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <FileText className="w-3.5 h-3.5" />}
                  Xem danh sách
                </button>
              </div>
            ) : (
              <div className="rounded-2xl border-2 border-orange-200 bg-orange-50/40 overflow-hidden">
                <div className="px-5 py-3.5 bg-[#f37021] text-white flex items-center justify-between gap-3">
                  <h3 className="text-sm font-black flex items-center gap-2">
                    <DollarSign className="w-4 h-4" /> Thống kê chi phí — {data.departmentName}
                  </h3>
                  <button
                    type="button"
                    onClick={() => { setInvoiceLoaded(false); setInvoiceItems([]); }}
                    className="p-1.5 hover:bg-white/10 rounded-full cursor-pointer"
                  >
                    <X className="w-4 h-4" />
                  </button>
                </div>

                <div className="p-5 space-y-4">
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="text-xs font-bold text-slate-600">Khoảng ngày:</span>
                    <input type="date" value={invoiceRange.fromDate}
                      onChange={(e) => setInvoiceRange((s) => ({ ...s, fromDate: e.target.value }))}
                      className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm bg-white outline-none focus:border-[#f37021]" />
                    <span className="text-slate-400 text-sm">→</span>
                    <input type="date" value={invoiceRange.toDate}
                      onChange={(e) => setInvoiceRange((s) => ({ ...s, toDate: e.target.value }))}
                      className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm bg-white outline-none focus:border-[#f37021]" />
                    <button
                      type="button"
                      onClick={loadInvoiceItems}
                      disabled={invoiceLoading}
                      className="px-4 py-2 bg-[#004c91] text-white text-xs font-black rounded-xl hover:opacity-90 disabled:opacity-50 transition-all cursor-pointer"
                    >
                      {invoiceLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin inline" /> : 'Tải lại danh sách'}
                    </button>
                  </div>

                  {invoiceItems.length === 0 ? (
                    <p className="text-sm text-slate-500 py-4 text-center">Không có đơn yêu cầu nào đã hoàn thành trong khoảng ngày này.</p>
                  ) : (
                    <>
                      <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
                        <table className="w-full text-left border-collapse">
                          <thead className="bg-slate-50">
                            <tr>
                              <th className={thClass}>STT</th>
                              <th className={thClass}>Đơn yêu cầu</th>
                              <th className={thClass}>Đoàn khách</th>
                              <th className={thClass}>Ngày</th>
                              <th className={thClass}>SL</th>
                              <th className={thClass}>Số tiền</th>
                              <th className={thClass}>Xem chi tiết</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-slate-100">
                            {invoiceItems.map((it, idx) => (
                              <tr key={it.logisticsItemId}>
                                <td className={`${tdClass} whitespace-nowrap`}>{idx + 1}</td>
                                <td className={`${tdClass} font-semibold text-slate-800`}>
                                  {it.title}
                                  <span className="block text-[11px] font-normal text-slate-400">{ITEM_TYPE_LABELS[it.itemType] ?? it.itemType} · {it.requestCode}</span>
                                </td>
                                <td className={tdClass}>{it.delegationName}</td>
                                <td className={`${tdClass} whitespace-nowrap`}>{fmtDateTime(it.usageStartAt)}</td>
                                <td className={`${tdClass} whitespace-nowrap`}>{it.quantity}</td>
                                <td className={`${tdClass} whitespace-nowrap`}>
                                  {it.noExpense
                                    ? <span className="text-[11px] font-bold text-emerald-700 bg-emerald-50 rounded px-1.5 py-0.5">Không có chi phí</span>
                                    : <span className="font-bold text-emerald-700">{vnMoney(it.totalExpense)}</span>}
                                </td>
                                <td className={`${tdClass} whitespace-nowrap`}>
                                  <button
                                    type="button"
                                    onClick={() => setViewItem(it)}
                                    className="text-xs font-bold text-[#004c91] hover:underline cursor-pointer"
                                  >
                                    Xem chi tiết
                                  </button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>

                      <div className="flex flex-wrap items-center justify-between gap-3">
                        <p className="text-sm font-black text-slate-700 uppercase">
                          Tổng số tiền:{' '}
                          <span className="text-base text-[#c2410c]">{vnMoney(invoiceTotal)}</span>
                        </p>
                        <button
                          type="button"
                          onClick={exportInvoicePdf}
                          className="inline-flex items-center gap-1.5 px-4 py-2.5 bg-white border border-slate-300 text-slate-700 text-xs font-black rounded-xl hover:bg-slate-50 transition-colors cursor-pointer"
                        >
                          <Download className="w-4 h-4" /> Xuất thống kê PDF
                        </button>
                      </div>
                    </>
                  )}
                </div>
              </div>
            )}
          </Section>

        </>
      )}

      {/* Modal biên bản đã ký giữa 2 bên (chỉ xem) */}
      {viewItem && (
        <TaskHandoverModal
          isOpen
          readOnly
          detailData={toHandoverDto(viewItem)}
          onClose={() => setViewItem(null)}
        />
      )}

      {/* Modal Xem trước / Chi tiết Email Báo cáo Thành viên Phòng ban */}
      <AnimatePresence>
        {previewMemberRow && (
          <div className="fixed inset-0 z-[150] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
              className="bg-white rounded-2xl shadow-2xl max-w-2xl w-full max-h-[90vh] overflow-hidden flex flex-col border border-gray-100 font-sans text-left"
            >
              <div className="flex items-center justify-between px-6 py-4 border-b border-gray-100 bg-gradient-to-r from-blue-50/60 to-white">
                <div className="flex items-center gap-2.5">
                  <div className="p-2 rounded-lg bg-[#004c91]/10 text-[#004c91]">
                    <Mail className="w-5 h-5" />
                  </div>
                  <div>
                    <h3 className="font-bold text-gray-900 text-base">Xem trước / Chi tiết Email Báo cáo Thành viên</h3>
                    <p className="text-xs text-gray-500">Mô phỏng thư báo cáo gửi thành viên {previewMemberRow.fullName}</p>
                  </div>
                </div>
                <button type="button" onClick={() => setPreviewMemberRow(null)} className="text-gray-400 hover:text-gray-600 p-1.5 rounded-lg hover:bg-gray-100">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="p-6 overflow-y-auto space-y-4 text-sm bg-slate-50/50">
                <div className="bg-white p-4 rounded-xl border border-gray-200 space-y-2 shadow-sm">
                  <div className="flex items-center justify-between border-b border-gray-100 pb-2">
                    <span className="text-xs font-semibold text-gray-400">TRẠNG THÁI EMAIL:</span>
                    {sentMemberMap[previewMemberRow.userId] ? (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-green-50 text-green-700 border border-green-200">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi báo cáo ({sentMemberMap[previewMemberRow.userId]})
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-blue-50 text-blue-700 border border-blue-200">
                        <Clock className="w-3.5 h-3.5" /> Xem trước (Chưa gửi)
                      </span>
                    )}
                  </div>
                  <div className="grid grid-cols-1 gap-2 text-xs">
                    <div><span className="font-semibold text-gray-500">Người gửi: </span><span className="font-medium text-gray-800">Department Leader Office &lt;deptleader-reports@mail.pems-fpt.site&gt;</span></div>
                    <div><span className="font-semibold text-gray-500">Người nhận: </span><span className="font-bold text-[#004c91]">{previewMemberRow.fullName} ({previewMemberRow.role})</span></div>
                    <div><span className="font-semibold text-gray-500">Tiêu đề: </span><span className="font-bold text-gray-900">[PEMS] Báo cáo hiệu suất công việc phòng ban — {previewMemberRow.fullName}</span></div>
                  </div>
                </div>

                <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                  <div className="bg-[#004c91] text-white px-5 py-4 font-bold text-base flex items-center justify-between">
                    <span>PEMS Department Performance Report</span>
                    <span className="text-xs font-normal opacity-80">{previewMemberRow.role}</span>
                  </div>
                  <div className="p-5 space-y-4 text-gray-700">
                    <p className="font-semibold">Kính gửi <strong className="text-gray-900">{previewMemberRow.fullName}</strong>,</p>
                    <p className="text-sm leading-relaxed">Trưởng phòng gửi đến bạn tổng hợp hiệu suất xử lý yêu cầu và nhiệm vụ hỗ trợ đợt công tác trong kỳ:</p>
                    <div className="bg-blue-50/60 rounded-xl p-4 border border-blue-100 space-y-2 text-xs">
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">ĐƠN YÊU CẦU ĐÃ XỬ LÝ:</span><span className="font-bold text-[#004c91]">{previewMemberRow.taskCount} đơn</span></div>
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">TỔNG GIỜ LÀM VIỆC:</span><span className="font-bold text-gray-800">{previewMemberRow.totalHours.toFixed(1)} giờ</span></div>
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">ĐÁNH GIÁ FEEDBACK:</span><span className="font-bold text-amber-600">{previewMemberRow.feedbackAverage ? `${previewMemberRow.feedbackAverage.toFixed(1)} ★` : 'Chưa có'}</span></div>
                      <div className="pt-1">
                        <span className="font-bold text-gray-500 block mb-1">GHI CHÚ ĐÁNH GIÁ (CÓ THỂ SỬA TRƯỚC KHI GỬI):</span>
                        {!sentMemberMap[previewMemberRow.userId] ? (
                          <textarea
                            value={personnelNotes[previewMemberRow.userId] || ''}
                            onChange={(e) => setPersonnelNotes((s) => ({ ...s, [previewMemberRow.userId]: e.target.value }))}
                            placeholder="Nhập/chỉnh sửa nội dung ghi chú gửi thành viên..."
                            rows={3}
                            className="w-full text-xs p-2.5 rounded-lg border border-blue-200 focus:border-[#004c91] outline-none bg-white text-gray-800 shadow-xs"
                          />
                        ) : (
                          <p className="italic text-gray-600 bg-white p-2.5 rounded-lg border border-blue-100">{personnelNotes[previewMemberRow.userId] || '(Không có ghi chú)'}</p>
                        )}
                      </div>
                    </div>
                    <div className="pt-3 border-t border-gray-100 text-xs text-gray-400">Trân trọng,<br /><strong>Ban Trưởng phòng — FPT University System</strong></div>
                  </div>
                </div>
              </div>
              <div className="flex items-center justify-between px-6 py-3 border-t border-gray-100 bg-gray-50">
                <button type="button" onClick={() => setPreviewMemberRow(null)} className="px-5 py-2 rounded-xl text-sm font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100">Đóng</button>
                {!sentMemberMap[previewMemberRow.userId] && (
                  <button
                    type="button"
                    onClick={() => {
                      const row = previewMemberRow;
                      setPreviewMemberRow(null);
                      sendPersonnelReport(row);
                    }}
                    disabled={personnelSend.isSending(previewMemberRow.userId)}
                    className="px-5 py-2 rounded-xl text-sm font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                  >
                    {personnelSend.isSending(previewMemberRow.userId) ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                    Gửi email ngay
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
