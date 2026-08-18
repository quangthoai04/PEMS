/**
 * Trang StaffLeaderReportManagement — Báo cáo campus của Staff Leader tại /dashboard/reports.
 * Bố cục 3 phần: (1) Đoàn tiếp khách + tiến độ hợp tác đối tác, (2) Nhân sự IC + Student,
 * (3) Các phòng ban khác (kèm xuất/gửi hóa đơn). Bộ lọc duy nhất là khoảng thời gian,
 * dùng chung cho cả 3 phần. Dữ liệu từ GET /reports/staff-leader-report-v2.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertTriangle, Building2, CalendarRange, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight,
  ChevronUp, Download, FileText, Loader2, RefreshCw, Send, Star, TrendingDown, TrendingUp,
  Users, X, XCircle, DollarSign, Handshake, PlusCircle, Percent, BarChart2, Eye, Mail, Clock,
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import {
  Bar, BarChart, CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { reportsApi } from '../../../features/reports/api/reportsApi';
import type {
  StaffLeaderExpenseVisit, StaffLeaderExpenseVisits,
  StaffLeaderReportV2, StaffLeaderV2DepartmentRow,
  StaffLeaderV2Filters, StaffLeaderV2PersonnelRow, StaffLeaderV2Preset,
} from '../../../features/reports/types/staffLeaderReportsV2.types';
import { useGuardedSend } from '../../../features/reports/hooks/useGuardedSend';
import { useIdempotentSend, attemptIsOver, sendFailureMessage } from '../../../features/reports/hooks/useIdempotentSend';
import { renderPrintDocument } from '../../../features/reports/print/printDocument';
import {
  STAFF_LEADER_EXPENSE_CSS,
  buildStaffLeaderExpenseDocument,
  staffLeaderExpenseTitle,
} from '../../../features/reports/print/staffLeaderExpenseDocument';

// Palette chart đã validate CVD/contrast.
const CHART_BLUE = '#1e6fc0';
const CHART_GREEN = '#0a8a44';

const PRESETS: { value: StaffLeaderV2Preset; label: string }[] = [
  { value: 'THIS_MONTH', label: 'Tháng này' },
  { value: 'THIS_QUARTER', label: 'Quý này' },
  { value: 'THIS_YEAR', label: 'Năm nay' },
  { value: 'CUSTOM', label: 'Tùy chỉnh' },
];

// Loại chi phí — bảng kê phòng ban (LOGISTICS) luôn hiển thị là "Hạng mục yêu cầu".
const ORIGIN_LABELS: Record<string, string> = {
  REQUEST_ITEM: 'Hạng mục yêu cầu', MANUAL: 'Nhập tay', ADDITIONAL: 'Phát sinh',
  DAMAGE_LOSS: 'Đền bù hư hỏng/mất mát', OTHER: 'Khác',
};

const thClass = 'px-3 py-2.5 text-[11px] font-bold text-slate-400 uppercase tracking-wide whitespace-nowrap text-left';
const tdClass = 'px-3 py-2.5 text-sm text-slate-600';

const vnMoney = (v: number) => `${v.toLocaleString('vi-VN')} ₫`;
const fmtDate = (iso: string) => (iso ? `${iso.slice(8, 10)}/${iso.slice(5, 7)}/${iso.slice(0, 4)}` : '—');
const fmtDateTime = (iso: string) => (iso ? `${iso.slice(11, 16)} ${fmtDate(iso)}` : '—');

/** Số dòng mỗi trang của các bảng nhân sự / phòng ban. */
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
      <span className="text-[11px] font-normal text-slate-400">
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
        <span className="text-xs font-normal text-slate-600 px-2 whitespace-nowrap">{page}/{totalPages}</span>
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

/** Ô thông số compact — 3 phần cùng 1 trang nên hạn chế khung/ô to. */
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
      {sub && <p className="text-[10px] font-normal opacity-75">{sub}</p>}
    </div>
  );
}

/** Section đóng/mở được — header là nút toggle, thân chỉ render khi mở. */
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

export function StaffLeaderReportManagement() {
  // ── Bộ lọc thời gian (chung cho cả 3 phần) ──
  const [filters, setFilters] = useState<StaffLeaderV2Filters>({ preset: 'THIS_MONTH', fromDate: '', toDate: '' });
  const [data, setData] = useState<StaffLeaderReportV2 | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Mỗi phần đóng/mở độc lập — mặc định mở cả 5.
  const [openSections, setOpenSections] = useState({ visits: true, partners: true, personnel: true, departments: true, expenses: true });
  const toggleSection = (key: keyof typeof openSections) =>
    setOpenSections((s) => ({ ...s, [key]: !s[key] }));

  // ── Xuất báo cáo (PDF/Excel/CSV) — chọn các phần cần xuất ──
  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [exportSections, setExportSections] = useState<string[]>(['VISITS', 'PARTNERS', 'PERSONNEL', 'DEPARTMENTS', 'EXPENSES']);
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
      const file = await reportsApi.exportStaffLeaderReportV2({
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

  const fetchReport = useCallback(async (f: StaffLeaderV2Filters) => {
    setLoading(true);
    setError(null);
    try {
      const res = await reportsApi.getStaffLeaderReportV2(f);
      setData(res);
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể tải báo cáo. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchReport(filters); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);

  const applyFilters = () => fetchReport(filters);

  // ── Phần 2: bảng nhân sự ──
  const [roleFilter, setRoleFilter] = useState<'ALL' | 'STAFF' | 'STUDENT'>('ALL');
  const [personnelNotes, setPersonnelNotes] = useState<Record<number, string>>({});
  const personnelSend = useGuardedSend<number>();
  // Same key across a retry of the SAME send; a new key only when the attempt is over (G11 / R-103).
  const idem = useIdempotentSend();
  const [personnelSort, setPersonnelSort] = useState<RankSort>('DEFAULT');
  const [personnelPage, setPersonnelPage] = useState(1);

  const personnelRows = useMemo(() => {
    const rows = data?.personnel.rows ?? [];
    if (roleFilter === 'STAFF') return rows.filter((r) => r.role !== 'STUDENT');
    if (roleFilter === 'STUDENT') return rows.filter((r) => r.role === 'STUDENT');
    return rows;
  }, [data, roleFilter]);

  const rankedPersonnelRows = useMemo(() => {
    if (personnelSort === 'DEFAULT') return personnelRows;
    const maxVisits = Math.max(0, ...personnelRows.map((r) => r.visitCount));
    const maxHours = Math.max(0, ...personnelRows.map((r) => r.totalHours));
    const score = (r: StaffLeaderV2PersonnelRow) => rankScore(r.visitCount, maxVisits, r.totalHours, maxHours, r.feedbackAverage);
    const sorted = [...personnelRows].sort((a, b) => score(b) - score(a));
    return personnelSort === 'BEST' ? sorted : sorted.reverse();
  }, [personnelRows, personnelSort]);
  const pagedPersonnelRows = rankedPersonnelRows.slice((personnelPage - 1) * PAGE_SIZE, personnelPage * PAGE_SIZE);

  // ── Phần 2: ghi chú + gửi email báo cáo nhân sự IC & Student ──
  const [previewStaffRow, setPreviewStaffRow] = useState<StaffLeaderV2PersonnelRow | null>(null);
  const [sentStaffMap, setSentStaffMap] = useState<Record<number, string>>({});
  const sendPersonnelReport = (row: StaffLeaderV2PersonnelRow) =>
    personnelSend.send(row.userId, async () => {
      try {
        const res = await reportsApi.sendStaffLeaderPersonnelReport({
          userId: row.userId,
          fromDate: data?.fromDate,
          toDate: data?.toDate,
          note: personnelNotes[row.userId]?.trim() || undefined,
        }, idem.keyFor('sl-personnel-report', row.userId));
        idem.complete('sl-personnel-report', row.userId);
        const nowStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) + ' ' + new Date().toLocaleDateString('vi-VN');
        setSentStaffMap((prev) => ({ ...prev, [row.userId]: nowStr }));
        toast.success(res.message || `Đã gửi báo cáo hiệu suất qua email cho ${row.fullName}.`);
      } catch (e: any) {
        if (attemptIsOver(e)) idem.complete('sl-personnel-report', row.userId);
        toast.error(sendFailureMessage(e, 'Gửi báo cáo thất bại.'));
      }
    });

  // ── Phần 3: ghi chú + gửi email báo cáo phối hợp cho từng phòng ban ──
  const [deptNotes, setDeptNotes] = useState<Record<number, string>>({});
  const [previewDeptRow, setPreviewDeptRow] = useState<StaffLeaderV2DepartmentRow | null>(null);
  const [sentDeptMap, setSentDeptMap] = useState<Record<number, string>>({});
  const departmentSend = useGuardedSend<number>();
  const [deptSort, setDeptSort] = useState<RankSort>('DEFAULT');
  const [deptPage, setDeptPage] = useState(1);

  // Xếp hạng phòng ban: không có số giờ làm việc nên chỉ dựa vào hoàn thành + feedback.
  const rankedDeptRows = useMemo(() => {
    const rows = data?.departments.rows ?? [];
    if (deptSort === 'DEFAULT') return rows;
    const maxCompleted = Math.max(0, ...rows.map((r) => r.completed));
    const score = (r: StaffLeaderV2DepartmentRow) => rankScore(r.completed, maxCompleted, 0, 0, r.feedbackAverage);
    const sorted = [...rows].sort((a, b) => score(b) - score(a));
    return deptSort === 'BEST' ? sorted : sorted.reverse();
  }, [data, deptSort]);
  const pagedDeptRows = rankedDeptRows.slice((deptPage - 1) * PAGE_SIZE, deptPage * PAGE_SIZE);

  // Dữ liệu kỳ mới → quay về trang 1 của các bảng.
  useEffect(() => { setPersonnelPage(1); setDeptPage(1); }, [data]);

  // Guarded per row — same rule as the personnel table above.
  const sendDepartmentReport = (row: StaffLeaderV2DepartmentRow) =>
    departmentSend.send(row.departmentId, async () => {
      try {
        const res = await reportsApi.sendStaffLeaderDepartmentReport({
          departmentId: row.departmentId,
          fromDate: data?.fromDate,
          toDate: data?.toDate,
          note: deptNotes[row.departmentId]?.trim() || undefined,
        }, idem.keyFor('sl-department-report', row.departmentId));
        idem.complete('sl-department-report', row.departmentId);
        const nowStr = new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) + ' ' + new Date().toLocaleDateString('vi-VN');
        setSentDeptMap((prev) => ({ ...prev, [row.departmentId]: nowStr }));
        toast.success(res.message || `Đã gửi báo cáo phối hợp qua email cho trưởng phòng ${row.name}.`);
      } catch (e: any) {
        if (attemptIsOver(e)) idem.complete('sl-department-report', row.departmentId);
        toast.error(sendFailureMessage(e, 'Gửi báo cáo thất bại.'));
      }
    });

  // ── Phần 4: thống kê chi phí các đoàn (panel tải theo khoảng ngày riêng) ──
  const [expenseRange, setExpenseRange] = useState<{ fromDate: string; toDate: string }>({ fromDate: '', toDate: '' });
  const [expenseData, setExpenseData] = useState<StaffLeaderExpenseVisits | null>(null);
  const [expenseLoading, setExpenseLoading] = useState(false);
  const [expenseLoaded, setExpenseLoaded] = useState(false);
  const [viewExpenseVisit, setViewExpenseVisit] = useState<StaffLeaderExpenseVisit | null>(null);

  useEffect(() => {
    if (data && !expenseRange.fromDate) setExpenseRange({ fromDate: data.fromDate, toDate: data.toDate });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data]);

  const loadExpenseVisits = async () => {
    if (!expenseRange.fromDate || !expenseRange.toDate) {
      toast.error('Chọn khoảng ngày để tải danh sách.');
      return;
    }
    setExpenseLoading(true);
    try {
      const res = await reportsApi.getStaffLeaderExpenseVisits(expenseRange.fromDate, expenseRange.toDate);
      setExpenseData(res);
      setExpenseLoaded(true);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Không tải được thống kê chi phí.');
    } finally {
      setExpenseLoading(false);
    }
  };

  // "Xuất thống kê PDF" chi phí: cửa sổ in riêng (như exportInvoicePdf) gồm 3 phần —
  // (1) bảng kê chi phí của từng đoàn, (2) thống kê theo loại, (3) số tiền phải trả
  // từng phòng ban. Tài liệu dựng bằng DOM, không nối chuỗi HTML —
  // xem features/reports/print/printDocument.ts.
  const exportExpensePdf = () => {
    if (!expenseData || expenseData.rows.length === 0) {
      toast.error('Chưa có dữ liệu chi phí để xuất thống kê.');
      return;
    }

    // Phần 2 + 3 — tổng hợp theo loại chi phí và theo phòng ban phải thanh toán.
    const byType = new Map<string, { count: number; total: number }>();
    const byDept = new Map<string, { count: number; total: number }>();
    for (const v2 of expenseData.rows) {
      for (const r of v2.reports) {
        if (r.reportScope === 'LOGISTICS') {
          const key = r.departmentName ?? 'Phòng ban khác';
          const cur = byDept.get(key) ?? { count: 0, total: 0 };
          cur.count += 1; cur.total += r.totalAmount;
          byDept.set(key, cur);
        }
        if (r.noExpense) continue;
        for (const it of r.items) {
          const label = r.reportScope === 'LOGISTICS' ? 'Hạng mục yêu cầu' : (ORIGIN_LABELS[it.itemOrigin] ?? it.itemOrigin);
          const cur = byType.get(label) ?? { count: 0, total: 0 };
          cur.count += 1; cur.total += it.totalAmount;
          byType.set(label, cur);
        }
      }
    }
    const toRows = (m: Map<string, { count: number; total: number }>) =>
      [...m.entries()].sort((a, b) => b[1].total - a[1].total)
        .map(([label, t]) => ({ label, count: t.count, total: t.total }));

    const root = buildStaffLeaderExpenseDocument({
      campusName: data?.campusName ?? '',
      periodFrom: fmtDate(expenseRange.fromDate),
      periodTo: fmtDate(expenseRange.toDate),
      grandTotal: expenseData.totalAmount,
      byType: toRows(byType),
      byDepartment: toRows(byDept),
      visits: expenseData.rows.map(v2 => ({
        delegationName: v2.delegationName,
        requestCode: v2.requestCode,
        visitDate: fmtDate(v2.visitDate),
        totalExpense: v2.totalExpense,
        reports: v2.reports.map(r => {
          const source = r.reportScope === 'GENERAL' ? 'Host' : (r.departmentName ?? 'Phòng ban');
          return {
            source,
            noExpense: r.noExpense,
            title: r.logisticsItemTitle ?? 'Đơn yêu cầu',
            note: r.reportNote,
            items: r.items.map(it => ({
              itemName: it.itemName,
              typeLabel: r.reportScope === 'LOGISTICS'
                ? 'Hạng mục yêu cầu'
                : (ORIGIN_LABELS[it.itemOrigin] ?? it.itemOrigin),
              source,
              quantity: it.quantity,
              unitPrice: it.unitPrice,
              totalAmount: it.totalAmount,
            })),
          };
        }),
      })),
    });

    const win = window.open('', '_blank', 'width=980,height=720');
    if (!win) {
      toast.error('Trình duyệt đang chặn popup — hãy cho phép popup cho trang này rồi thử lại.');
      return;
    }
    renderPrintDocument(win, {
      title: staffLeaderExpenseTitle(data?.campusName ?? ''),
      css: STAFF_LEADER_EXPENSE_CSS,
      root,
    });
    win.focus();
    setTimeout(() => win.print(), 350);
    toast.success('Đã mở bản in thống kê — chọn "Save as PDF" để lưu.');
  };



  const v = data?.visits;
  const pt = data?.partners;
  const p = data?.personnel;
  const d = data?.departments;

  return (
    <div className="w-full space-y-4 pb-16 animate-in fade-in duration-300">
      {/* ── Header + bộ lọc thời gian ── */}
      <div className="border-b border-gray-100 pb-4 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">Báo cáo campus</h1>
          <p className="text-slate-500 mt-2">
            {data ? `${data.campusName} · Kỳ ${fmtDate(data.fromDate)} – ${fmtDate(data.toDate)}` : 'Báo cáo vận hành campus của Staff Leader.'}
          </p>
        </div>

        {/* Nút xuất báo cáo — chọn phần + 3 định dạng */}
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
                {[
                  { key: 'VISITS', label: 'Phần 1 · Đoàn tiếp khách' },
                  { key: 'PARTNERS', label: 'Phần 2 · Đối tác' },
                  { key: 'PERSONNEL', label: 'Phần 3 · Nhân sự' },
                  { key: 'DEPARTMENTS', label: 'Phần 4 · Phòng ban khác' },
                  { key: 'EXPENSES', label: 'Phần 5 · Thống kê chi phí' },
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
                    checked={exportSections.length === 5}
                    onChange={() => setExportSections(exportSections.length === 5 ? [] : ['VISITS', 'PARTNERS', 'PERSONNEL', 'DEPARTMENTS', 'EXPENSES'])}
                    className="accent-[#004c91]"
                  />
                  Chọn tất cả
                </label>
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
          onClick={applyFilters}
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
          <p className="text-sm font-normal">Đang tổng hợp báo cáo...</p>
        </div>
      ) : data && (
        <>
          {/* ═══ 1 · Báo cáo đoàn tiếp khách ═══ */}
          <Section
            index={1}
            title="Báo cáo đoàn tiếp khách"
            subtitle="Số liệu các đoàn đến thăm campus trong kỳ và tiến độ hợp tác với đối tác."
            open={openSections.visits}
            onToggle={() => toggleSection('visits')}
          >
            <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-3">
              <StatTile label="Tổng đoàn khách" value={v!.totalVisits} sub={`${v!.totalGuests} khách`} tone="blue" icon={<Users className="w-4 h-4 opacity-60" />} />
              <StatTile label="Đã hoàn thành" value={v!.completed} tone="green" icon={<CheckCircle2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Từ chối" value={v!.rejected} sub={v!.cancelled > 0 ? `+${v!.cancelled} bị hủy` : undefined} tone="red" icon={<XCircle className="w-4 h-4 opacity-60" />} />
              <StatTile label="Chưa hoàn thành" value={v!.notCompleted} tone="amber" />
              <StatTile
                label="Feedback"
                value={v!.feedbackAverage != null ? `${v!.feedbackAverage.toFixed(1)}★` : '—'}
                sub={`${v!.feedbackTotalStars} sao / ${v!.feedbackCount} lượt`}
                tone="violet"
                icon={<Star className="w-4 h-4 opacity-60" />}
              />
              <StatTile label="Tổng đối tác" value={v!.totalPartners} tone="slate" icon={<Building2 className="w-4 h-4 opacity-60" />} />
            </div>

            {/* Biểu đồ xu hướng đoàn tiếp khách & lượt khách — mốc trục thời gian đổi theo khoảng lọc */}
            <div className="mt-6">
              <h3 className="text-sm font-bold text-slate-700 mb-1">Xu hướng đoàn tiếp khách & lượt khách</h3>
              <p className="text-xs text-slate-400 mb-3">
                Thống kê số lượng đoàn tiếp khách và tổng số khách theo{' '}
                {{ YEAR: 'năm', MONTH: 'tháng', WEEK: 'tuần', DAY: 'ngày', HOUR: 'giờ' }[v!.trendGranularity] ?? 'tháng'}. Dữ liệu thuộc riêng {data.campusName}.
              </p>
              <div className="h-72">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={v!.visitTrend ?? []} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="monthLabel" tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <YAxis allowDecimals={false} tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <Tooltip
                      formatter={(value: number, name: string) => [value, name]}
                      labelStyle={{ fontWeight: 700 }}
                      contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }}
                    />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    <Line type="monotone" dataKey="totalVisits" name="Tổng số đoàn" stroke={CHART_BLUE} strokeWidth={2.5} dot={{ r: 3 }} />
                    <Line type="monotone" dataKey="completedVisits" name="Đoàn hoàn thành" stroke={CHART_GREEN} strokeWidth={2.5} dot={{ r: 3 }} />
                    <Line type="monotone" dataKey="totalGuests" name="Tổng số khách" stroke="#f37021" strokeWidth={2} strokeDasharray="4 4" dot={{ r: 3 }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>
          </Section>

          {/* ═══ 2 · Báo cáo đối tác ═══ */}
          <Section
            index={2}
            title="Báo cáo đối tác"
            subtitle="Tổng quan đối tác, tiến độ hợp tác, phân loại đối tác và các đối tác nổi bật của campus trong kỳ."
            open={openSections.partners}
            onToggle={() => toggleSection('partners')}
          >
            <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-5 gap-3">
              <StatTile label="Tổng đối tác" value={pt?.totalPartners ?? 0} sub="Đã duyệt & quản lý" tone="blue" icon={<Handshake className="w-4 h-4 opacity-60" />} />
              <StatTile label="Đối tác mới" value={pt?.newPartnersInPeriod ?? 0} sub="Tạo/duyệt trong kỳ" tone="green" icon={<PlusCircle className="w-4 h-4 opacity-60" />} />
              <StatTile label="Đang hợp tác" value={pt?.activePartners ?? 0} sub="Trạng thái Active" tone="slate" icon={<Building2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Đoàn có đối tác" value={pt?.visitsWithPartnerCount ?? 0} sub="Chuyến thăm có đối tác" tone="amber" icon={<Users className="w-4 h-4 opacity-60" />} />
              <StatTile label="Tỷ lệ đoàn có đối tác" value={`${pt?.partnerVisitRatio ?? 0}%`} tone="violet" icon={<Percent className="w-4 h-4 opacity-60" />} />
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mt-6">
              <div className="bg-slate-50/50 p-4 rounded-2xl border border-slate-200">
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-sm font-bold text-slate-700 flex items-center gap-1.5">
                    <TrendingUp className="w-4 h-4 text-[#004c91]" /> Tiến độ hợp tác với đối tác
                  </h3>
                  <span className="text-[11px] font-normal text-slate-400">
                    Theo {{ YEAR: 'năm', MONTH: 'tháng', WEEK: 'tuần', DAY: 'ngày', HOUR: 'giờ' }[pt?.trendGranularity ?? 'MONTH'] ?? 'tháng'}
                  </span>
                </div>
                <div className="h-64">
                  <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={pt?.trend ?? []} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                      <XAxis dataKey="monthLabel" tick={{ fontSize: 11 }} stroke="#94a3b8" />
                      <YAxis allowDecimals={false} tick={{ fontSize: 11 }} stroke="#94a3b8" />
                      <Tooltip
                        formatter={(value: number, name: string) => [value, name]}
                        labelStyle={{ fontWeight: 700 }}
                        contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }}
                      />
                      <Legend wrapperStyle={{ fontSize: 12 }} />
                      <Line type="monotone" dataKey="cumulativePartners" name="Lũy kế đối tác" stroke={CHART_BLUE} strokeWidth={2.5} dot={{ r: 3 }} />
                      <Line type="monotone" dataKey="visitsWithPartner" name="Chuyến thăm gắn đối tác" stroke={CHART_GREEN} strokeWidth={2.5} dot={{ r: 3 }} />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              </div>

              <div className="bg-slate-50/50 p-4 rounded-2xl border border-slate-200">
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-sm font-bold text-slate-700 flex items-center gap-1.5">
                    <BarChart2 className="w-4 h-4 text-[#f37021]" /> Phân loại đối tác theo loại hình
                  </h3>
                  <span className="text-[11px] font-medium text-slate-400">Số lượng & số chuyến</span>
                </div>
                <div className="h-64">
                  {(!pt?.partnersByType || pt.partnersByType.length === 0) ? (
                    <div className="h-full flex items-center justify-center text-slate-400 text-xs">Chưa có dữ liệu phân loại đối tác.</div>
                  ) : (
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={pt.partnersByType} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                        <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                        <XAxis dataKey="label" tick={{ fontSize: 10 }} stroke="#94a3b8" interval={0} />
                        <YAxis allowDecimals={false} tick={{ fontSize: 11 }} stroke="#94a3b8" />
                        <Tooltip
                          formatter={(value: number, name: string) => [value, name]}
                          labelStyle={{ fontWeight: 700 }}
                          contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }}
                        />
                        <Legend wrapperStyle={{ fontSize: 12 }} />
                        <Bar dataKey="count" name="Số đối tác" fill="#004c91" radius={[4, 4, 0, 0]} />
                        <Bar dataKey="visitCount" name="Số chuyến thăm" fill="#f37021" radius={[4, 4, 0, 0]} />
                      </BarChart>
                    </ResponsiveContainer>
                  )}
                </div>
              </div>
            </div>

            {pt?.partnersByStatus && pt.partnersByStatus.length > 0 && (
              <div className="bg-slate-50 p-4 rounded-2xl border border-slate-200 mt-4">
                <h4 className="text-xs font-bold uppercase text-slate-500 tracking-wider mb-3">Phân bổ trạng thái hợp tác đối tác</h4>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                  {pt.partnersByStatus.map((st) => (
                    <div key={st.status} className="bg-white p-3 rounded-xl border border-slate-200 shadow-sm flex items-center justify-between">
                      <div>
                        <p className="text-xs font-bold text-slate-700">{st.label}</p>
                        <p className="text-lg font-black text-[#004c91] mt-0.5">{st.count}</p>
                      </div>
                      <span className={`text-[10px] font-bold px-2 py-1 rounded-full ${
                        st.status === 'ACTIVE' ? 'bg-emerald-100 text-emerald-800' :
                        st.status === 'POTENTIAL' ? 'bg-blue-100 text-blue-800' :
                        'bg-slate-100 text-slate-700'
                      }`}>
                        {st.status}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}

            <div className="mt-6">
              <h3 className="text-sm font-bold text-slate-700 mb-2">Top đối tác hợp tác tích cực nhất trong kỳ</h3>
              <div className="rounded-2xl border border-slate-200 overflow-hidden">
                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse">
                    <thead className="bg-slate-50">
                      <tr>
                        <th className={thClass}>STT</th>
                        <th className={thClass}>Mã đối tác</th>
                        <th className={thClass}>Tên đối tác</th>
                        <th className={thClass}>Loại hình</th>
                        <th className={thClass}>Trạng thái</th>
                        <th className={thClass}>Số chuyến thăm</th>
                        <th className={thClass}>Số lượt khách</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {(!pt?.topPartners || pt.topPartners.length === 0) ? (
                        <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-slate-400">Không có chuyến thăm gắn với đối tác nào trong kỳ.</td></tr>
                      ) : (
                        pt.topPartners.map((row, idx) => (
                          <tr key={row.partnerId} className={idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                            <td className={`${tdClass} whitespace-nowrap`}>{idx + 1}</td>
                            <td className={`${tdClass} whitespace-nowrap font-mono text-xs font-bold text-slate-500`}>{row.partnerCode || '—'}</td>
                            <td className={`${tdClass} font-semibold text-slate-800`}>{row.name}</td>
                            <td className={`${tdClass} whitespace-nowrap`}>
                              <span className="text-xs bg-slate-100 text-slate-700 px-2 py-0.5 rounded-md font-medium">{row.partnerType}</span>
                            </td>
                            <td className={`${tdClass} whitespace-nowrap`}>
                              <span className={`text-[11px] font-bold px-2 py-0.5 rounded-full ${
                                row.cooperationStatus === 'Đang hợp tác' || row.cooperationStatus === 'ACTIVE' ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'
                              }`}>
                                {row.cooperationStatus}
                              </span>
                            </td>
                            <td className={`${tdClass} whitespace-nowrap font-normal text-[#004c91]`}>{row.visitCount} chuyến</td>
                            <td className={`${tdClass} whitespace-nowrap font-normal text-slate-700`}>{row.guestCount} khách</td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
          </Section>

          {/* ═══ 3 · Báo cáo nhân sự ═══ */}
          <Section
            index={3}
            title="Báo cáo nhân sự"
            subtitle="Hiệu suất của Staff Leader, IC Staff và sinh viên hỗ trợ tiếp khách trong kỳ."
            open={openSections.personnel}
            onToggle={() => toggleSection('personnel')}
          >
            <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
              <StatTile label="Tổng nhân sự" value={p!.totalStaff} tone="blue" icon={<Users className="w-4 h-4 opacity-60" />} />
              <StatTile label="Tổng student" value={p!.totalStudents} tone="slate" />
              <StatTile
                label="Feedback trung bình"
                value={p!.averageFeedback != null ? `${p!.averageFeedback.toFixed(1)}★` : '—'}
                sub="Trung bình cộng nhân sự + student"
                tone="violet"
                icon={<Star className="w-4 h-4 opacity-60" />}
              />
            </div>

            {/* Bộ lọc vai trò + xếp hạng */}
            <div className="flex flex-wrap items-center gap-2 mt-5 mb-3">
              {(['ALL', 'STAFF', 'STUDENT'] as const).map((rf) => (
                <button
                  key={rf}
                  type="button"
                  onClick={() => { setRoleFilter(rf); setPersonnelPage(1); }}
                  className={`px-3.5 py-1.5 rounded-full text-xs font-bold border transition-colors cursor-pointer ${
                    roleFilter === rf ? 'bg-[#004c91] text-white border-[#004c91]' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  {rf === 'ALL' ? 'Tất cả' : rf === 'STAFF' ? 'Staff' : 'Student'}
                </button>
              ))}
              <span className="text-[11px] text-slate-400 ml-2 flex items-center gap-1">
                <Star className="w-3 h-3 text-amber-400 fill-amber-400" /> = Staff Leader
              </span>
              <div className="ml-auto">
                <RankSortButtons sort={personnelSort} onChange={(s) => { setPersonnelSort(s); setPersonnelPage(1); }} />
              </div>
            </div>

            <div className="rounded-2xl border border-slate-200 overflow-hidden">
              <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead className="bg-slate-50">
                  <tr>
                    <th className={thClass}>STT</th>
                    <th className={thClass}>Tên</th>
                    <th className={thClass}>Vai trò</th>
                    <th className={thClass}>Số đoàn phụ trách</th>
                    <th className={thClass}>Tổng giờ làm việc</th>
                    <th className={thClass}>Feedback</th>
                    <th className={thClass}>Từ chối</th>
                    <th className={thClass}>Ghi chú</th>
                    <th className={thClass}></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {personnelRows.length === 0 && (
                    <tr><td colSpan={9} className="px-4 py-8 text-center text-sm text-slate-400">Không có nhân sự nào.</td></tr>
                  )}
                  {pagedPersonnelRows.map((row, idx) => {
                    const lowFeedback = row.feedbackAverage != null && row.feedbackAverage < 2;
                    return (
                      <tr key={row.userId} className={lowFeedback ? 'bg-rose-50/50' : idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                        <td className={`${tdClass} whitespace-nowrap`}>{(personnelPage - 1) * PAGE_SIZE + idx + 1}</td>
                        <td className={`${tdClass} font-semibold text-slate-800`}>
                          <span className="flex items-center gap-1.5">
                            {row.fullName}
                            {row.role === 'STAFF_LEADER' && (
                              <Star className="w-3.5 h-3.5 text-amber-400 fill-amber-400 shrink-0" aria-label="Staff Leader" />
                            )}
                          </span>
                          <span className="block text-[11px] font-normal text-slate-400">{row.email}</span>
                        </td>
                        <td className={`${tdClass} whitespace-nowrap`}>{row.role === 'STUDENT' ? 'Student' : 'Staff'}</td>
                        <td className={`${tdClass} whitespace-nowrap`}>{row.visitCount}</td>
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
                              onClick={() => setPreviewStaffRow(row)}
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

          {/* ═══ 4 · Báo cáo phòng ban khác ═══ */}
          <Section
            index={4}
            title="Báo cáo phòng ban khác"
            subtitle="Đơn yêu cầu hậu cần & thư mời hỗ trợ gửi tới các phòng ban trong kỳ."
            open={openSections.departments}
            onToggle={() => toggleSection('departments')}
          >
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
              <StatTile label="Tổng phòng ban" value={d!.totalDepartments} tone="blue" icon={<Building2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Hoàn thành" value={d!.completedTotal} tone="green" icon={<CheckCircle2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Từ chối" value={d!.rejectedTotal} tone="red" icon={<XCircle className="w-4 h-4 opacity-60" />} />
              <StatTile
                label="Feedback trung bình"
                value={d!.averageFeedback != null ? `${d!.averageFeedback.toFixed(1)}★` : '—'}
                tone="violet"
                icon={<Star className="w-4 h-4 opacity-60" />}
              />
            </div>

            {/* Xếp hạng phòng ban (hoàn thành + feedback) */}
            <div className="flex items-center justify-end mt-5 mb-3">
              <RankSortButtons sort={deptSort} onChange={(s) => { setDeptSort(s); setDeptPage(1); }} />
            </div>

            <div className="rounded-2xl border border-slate-200 overflow-hidden">
              <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse">
                <thead className="bg-slate-50">
                  <tr>
                    <th className={thClass}>STT</th>
                    <th className={thClass}>Tên phòng ban</th>
                    <th className={thClass}>Tổng đơn/thư yêu cầu</th>
                    <th className={thClass}>Hoàn thành</th>
                    <th className={thClass}>Từ chối</th>
                    <th className={thClass}>Feedback</th>
                    <th className={thClass}>Ghi chú</th>
                    <th className={thClass}></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {d!.rows.length === 0 && (
                    <tr><td colSpan={8} className="px-4 py-8 text-center text-sm text-slate-400">Không có phòng ban nào.</td></tr>
                  )}
                  {pagedDeptRows.map((row, idx) => {
                    const lowFeedback = row.feedbackAverage != null && row.feedbackAverage < 2;
                    return (
                      <tr
                        key={row.departmentId}
                        className={lowFeedback ? 'bg-rose-50/50' : idx % 2 === 1 ? 'bg-slate-50/40' : ''}
                      >
                        <td className={`${tdClass} whitespace-nowrap`}>{(deptPage - 1) * PAGE_SIZE + idx + 1}</td>
                        <td className={`${tdClass} font-semibold text-slate-800`}>{row.name}</td>
                        <td className={`${tdClass} whitespace-nowrap`}>{row.totalRequests}</td>
                        <td className={`${tdClass} whitespace-nowrap text-emerald-700 font-normal`}>{row.completed}</td>
                        <td className={`${tdClass} whitespace-nowrap text-rose-600 font-normal`}>{row.rejected}</td>
                        <td className={`${tdClass} whitespace-nowrap`}>
                          {row.feedbackAverage != null ? (
                            <span className={`inline-flex items-center gap-1 font-bold ${lowFeedback ? 'text-rose-600' : 'text-slate-700'}`}>
                              {row.feedbackAverage.toFixed(1)}★
                              <span className="text-[11px] font-normal text-slate-400">({row.feedbackCount})</span>
                              {lowFeedback && <AlertTriangle className="w-3.5 h-3.5 text-rose-500" aria-label="Feedback dưới 2 sao" />}
                            </span>
                          ) : <span className="text-slate-400">—</span>}
                        </td>
                        <td className={tdClass}>
                          <input
                            type="text"
                            value={deptNotes[row.departmentId] ?? ''}
                            onChange={(ev) => setDeptNotes((s) => ({ ...s, [row.departmentId]: ev.target.value }))}
                            placeholder="Ghi chú..."
                            className="w-40 border border-slate-200 rounded-lg px-2 py-1.5 text-xs outline-none focus:border-[#004c91]"
                          />
                        </td>
                        <td className={`${tdClass} whitespace-nowrap`}>
                          <div className="inline-flex items-center">
                            <button
                              type="button"
                              onClick={() => sendDepartmentReport(row)}
                              disabled={departmentSend.isSending(row.departmentId)}
                              title={`Gửi báo cáo phối hợp qua email cho trưởng phòng ${row.name}`}
                              className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-[#004c91] text-xs font-bold rounded-l-lg border border-blue-200 transition-colors disabled:opacity-50 cursor-pointer"
                            >
                              {departmentSend.isSending(row.departmentId) ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                              Gửi
                            </button>
                            <button
                              type="button"
                              onClick={() => setPreviewDeptRow(row)}
                              title={`Xem trước / Xem email báo cáo phối hợp cho ${row.name}`}
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
              <Pagination page={deptPage} total={rankedDeptRows.length} onChange={setDeptPage} />
            </div>
          </Section>

          {/* ═══ 5 · Thống kê chi phí ═══ */}
          <Section
            index={5}
            title="Thống kê chi phí"
            subtitle="Chi phí tiếp khách của từng đoàn (bảng kê của Host và các phòng ban) theo khoảng ngày."
            open={openSections.expenses}
            onToggle={() => toggleSection('expenses')}
          >
            <div className="rounded-2xl border-2 border-orange-200 bg-orange-50/40 overflow-hidden">
              <div className="px-5 py-3.5 bg-[#f37021] text-white flex items-center gap-2">
                <DollarSign className="w-4 h-4" />
                <h3 className="text-sm font-black">Thống kê chi phí tiếp khách — {data.campusName}</h3>
              </div>

              <div className="p-5 space-y-4">
                {/* Chọn khoảng ngày */}
                <div className="flex flex-wrap items-center gap-3">
                  <span className="text-xs font-bold text-slate-600">Khoảng ngày:</span>
                  <input type="date" value={expenseRange.fromDate}
                    onChange={(ev) => setExpenseRange((s) => ({ ...s, fromDate: ev.target.value }))}
                    className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm bg-white outline-none focus:border-[#f37021]" />
                  <span className="text-slate-400 text-sm">→</span>
                  <input type="date" value={expenseRange.toDate}
                    onChange={(ev) => setExpenseRange((s) => ({ ...s, toDate: ev.target.value }))}
                    className="border border-slate-200 rounded-lg px-2.5 py-1.5 text-sm bg-white outline-none focus:border-[#f37021]" />
                  <button
                    type="button"
                    onClick={loadExpenseVisits}
                    disabled={expenseLoading}
                    className="px-4 py-2 bg-[#004c91] text-white text-xs font-black rounded-xl hover:opacity-90 disabled:opacity-50 transition-all cursor-pointer"
                  >
                    {expenseLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin inline" /> : 'Tải danh sách'}
                  </button>
                </div>

                {expenseLoaded && expenseData && expenseData.rows.length === 0 && (
                  <p className="text-sm text-slate-500 py-4 text-center">Không có đoàn nào có dữ liệu chi phí trong khoảng ngày này.</p>
                )}

                {expenseData && expenseData.rows.length > 0 && (
                  <>
                    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white">
                      <table className="w-full text-left border-collapse">
                        <thead className="bg-slate-50">
                          <tr>
                            <th className={thClass}>STT</th>
                            <th className={thClass}>Tên đoàn khách</th>
                            <th className={thClass}>Thời gian</th>
                            <th className={thClass}>Số tiền</th>
                            <th className={thClass}></th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-slate-100">
                          {expenseData.rows.map((row, idx) => (
                            <tr key={row.visitInstanceId} className={idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                              <td className={`${tdClass} whitespace-nowrap`}>{idx + 1}</td>
                              <td className={`${tdClass} font-semibold text-slate-800`}>
                                {row.delegationName}
                                <span className="block text-[11px] font-normal text-slate-400">{row.requestCode}</span>
                              </td>
                              <td className={`${tdClass} whitespace-nowrap`}>{fmtDateTime(row.visitDate)}</td>
                              <td className={`${tdClass} whitespace-nowrap text-emerald-700 font-normal`}>{vnMoney(row.totalExpense)}</td>
                              <td className={`${tdClass} whitespace-nowrap`}>
                                <button
                                  type="button"
                                  onClick={() => setViewExpenseVisit(row)}
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
                        Tổng chi phí các đoàn:{' '}
                        <span className="text-base text-[#c2410c]">{vnMoney(expenseData.totalAmount)}</span>
                      </p>
                      <button
                        type="button"
                        onClick={exportExpensePdf}
                        className="inline-flex items-center gap-1.5 px-4 py-2.5 bg-white border border-slate-300 text-slate-700 text-xs font-black rounded-xl hover:bg-slate-50 transition-colors cursor-pointer"
                      >
                        <Download className="w-4 h-4" /> Xuất thống kê PDF
                      </button>
                    </div>
                  </>
                )}
              </div>
            </div>
          </Section>

        </>
      )}

      {/* Modal ghi chú chi phí của 1 đoàn — phần 4 "Xem chi tiết" */}
      {viewExpenseVisit && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-slate-900/50" onClick={() => setViewExpenseVisit(null)} />
          <div className="relative bg-white rounded-2xl shadow-2xl w-full max-w-2xl max-h-[85vh] overflow-hidden flex flex-col">
            <div className="px-5 py-3.5 bg-[#004c91] text-white flex items-center justify-between gap-3">
              <h3 className="text-sm font-black truncate">
                Ghi chú chi phí — {viewExpenseVisit.delegationName}
                <span className="block text-[11px] font-normal opacity-75">
                  {viewExpenseVisit.requestCode} · {fmtDateTime(viewExpenseVisit.visitDate)}
                </span>
              </h3>
              <button type="button" onClick={() => setViewExpenseVisit(null)} className="p-1.5 hover:bg-white/10 rounded-full cursor-pointer shrink-0">
                <X className="w-4 h-4" />
              </button>
            </div>

            <div className="p-4 overflow-y-auto space-y-3">
              {viewExpenseVisit.reports.map((r, i) => (
                <div key={i} className="rounded-xl border border-slate-200 overflow-hidden">
                  <div className="flex flex-wrap items-center gap-2 px-3 py-2 bg-slate-50">
                    <span className="text-xs font-bold text-slate-800">
                      {r.reportScope === 'GENERAL' ? 'Chi phí chung (Host)' : (r.logisticsItemTitle ?? 'Đơn yêu cầu')}
                    </span>
                    {r.departmentName && (
                      <span className="text-[10px] font-bold text-slate-500 bg-slate-200/70 rounded px-1.5 py-0.5">{r.departmentName}</span>
                    )}
                    {r.noExpense && (
                      <span className="text-[10px] font-bold text-emerald-700 bg-emerald-100 rounded px-1.5 py-0.5">Không có chi phí</span>
                    )}
                    <span className="ml-auto text-xs font-black text-[#004c91]">{vnMoney(r.totalAmount)}</span>
                  </div>
                  {!r.noExpense && r.items.length > 0 && (
                    <table className="w-full text-left border-collapse text-[11px]">
                      <tbody className="divide-y divide-slate-50">
                        {r.items.map((it, j) => (
                          <tr key={j}>
                            <td className="pl-4 pr-2 py-1 text-slate-400 whitespace-nowrap w-36">
                              {r.reportScope === 'LOGISTICS' ? 'Hạng mục yêu cầu' : (ORIGIN_LABELS[it.itemOrigin] ?? it.itemOrigin)}
                            </td>
                            <td className="px-2 py-1 font-semibold text-slate-700">{it.itemName}</td>
                            <td className="px-2 py-1 text-right text-slate-500 whitespace-nowrap w-14">
                              {it.quantity}{it.unitName ? ` ${it.unitName}` : ''}
                            </td>
                            <td className="px-2 py-1 text-right text-slate-500 whitespace-nowrap w-24">{it.unitPrice.toLocaleString('vi-VN')}</td>
                            <td className="pl-2 pr-3 py-1 text-right font-normal text-slate-700 whitespace-nowrap w-28">{vnMoney(it.totalAmount)}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                  {r.reportNote && (
                    <p className="px-3 py-1.5 text-[11px] italic text-slate-500 border-t border-slate-100">Ghi chú: {r.reportNote}</p>
                  )}
                </div>
              ))}

              <div className="rounded-xl bg-orange-50 border border-orange-100 px-3 py-2 text-right text-xs font-black text-slate-700 uppercase">
                Tổng chi phí đoàn:
                <span className="text-sm text-[#c2410c] ml-1.5">{vnMoney(viewExpenseVisit.totalExpense)}</span>
              </div>
            </div>
          </div>
        </div>
      )}
      {/* Modal Xem trước / Chi tiết Email Báo cáo Hiệu suất Nhân sự */}
      <AnimatePresence>
        {previewStaffRow && (
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
                    <h3 className="font-bold text-gray-900 text-base">Xem trước / Chi tiết Email Báo cáo Hiệu suất</h3>
                    <p className="text-xs text-gray-500">Mô phỏng thư báo cáo gửi tới nhân sự {previewStaffRow.fullName}</p>
                  </div>
                </div>
                <button type="button" onClick={() => setPreviewStaffRow(null)} className="text-gray-400 hover:text-gray-600 p-1.5 rounded-lg hover:bg-gray-100">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="p-6 overflow-y-auto space-y-4 text-sm bg-slate-50/50">
                <div className="bg-white p-4 rounded-xl border border-gray-200 space-y-2 shadow-sm">
                  <div className="flex items-center justify-between border-b border-gray-100 pb-2">
                    <span className="text-xs font-semibold text-gray-400">TRẠNG THÁI EMAIL:</span>
                    {sentStaffMap[previewStaffRow.userId] ? (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-green-50 text-green-700 border border-green-200">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi báo cáo ({sentStaffMap[previewStaffRow.userId]})
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-blue-50 text-blue-700 border border-blue-200">
                        <Clock className="w-3.5 h-3.5" /> Xem trước (Chưa gửi)
                      </span>
                    )}
                  </div>
                  <div className="grid grid-cols-1 gap-2 text-xs">
                    <div><span className="font-semibold text-gray-500">Người gửi: </span><span className="font-normal text-gray-800">Staff Leader Office &lt;staffleader-reports@mail.pems-fpt.site&gt;</span></div>
                    <div><span className="font-semibold text-gray-500">Người nhận: </span><span className="font-normal text-[#004c91]">{previewStaffRow.fullName} ({previewStaffRow.role})</span></div>
                    <div><span className="font-semibold text-gray-500">Tiêu đề: </span><span className="font-normal text-gray-900">[PEMS] Báo cáo hiệu suất cá nhân trong kỳ — {previewStaffRow.fullName}</span></div>
                  </div>
                </div>

                <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                  <div className="bg-[#004c91] text-white px-5 py-4 font-bold text-base flex items-center justify-between">
                    <span>PEMS Performance Report</span>
                    <span className="text-xs font-normal opacity-80">{previewStaffRow.role}</span>
                  </div>
                  <div className="p-5 space-y-4 text-gray-700">
                    <p className="font-normal">Kính gửi <strong className="text-gray-900">{previewStaffRow.fullName}</strong>,</p>
                    <p className="text-sm leading-relaxed">Staff Leader gửi đến bạn tổng hợp chỉ số hiệu suất công việc tiếp đón và phục vụ đoàn trong kỳ:</p>
                    <div className="bg-blue-50/60 rounded-xl p-4 border border-blue-100 space-y-2 text-xs">
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">SỐ CHUYẾN THĂM THAM GIA:</span><span className="font-normal text-[#004c91]">{previewStaffRow.visitCount} đoàn</span></div>
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">TỔNG GIỜ PHỤC VỤ:</span><span className="font-normal text-gray-800">{previewStaffRow.totalHours.toFixed(1)} giờ</span></div>
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">ĐÁNH GIÁ TRUNG BÌNH:</span><span className="font-normal text-amber-600">{previewStaffRow.feedbackAverage ? `${previewStaffRow.feedbackAverage.toFixed(1)} ★` : 'Chưa có'}</span></div>
                      <div className="pt-1">
                        <span className="font-bold text-gray-500 block mb-1">GHI CHÚ ĐÁNH GIÁ (CÓ THỂ SỬA TRƯỚC KHI GỬI):</span>
                        {!sentStaffMap[previewStaffRow.userId] ? (
                          <textarea
                            value={personnelNotes[previewStaffRow.userId] || ''}
                            onChange={(e) => setPersonnelNotes((s) => ({ ...s, [previewStaffRow.userId]: e.target.value }))}
                            placeholder="Nhập/chỉnh sửa ghi chú đánh giá gửi nhân sự..."
                            rows={3}
                            className="w-full text-xs p-2.5 rounded-lg border border-blue-200 focus:border-[#004c91] outline-none bg-white text-gray-800 shadow-xs"
                          />
                        ) : (
                          <p className="italic text-gray-600 bg-white p-2.5 rounded-lg border border-blue-100">{personnelNotes[previewStaffRow.userId] || '(Không có ghi chú)'}</p>
                        )}
                      </div>
                    </div>
                    <div className="pt-3 border-t border-gray-100 text-xs text-gray-400">Trân trọng,<br /><strong>Ban Quản trị Staff Leader — FPT University</strong></div>
                  </div>
                </div>
              </div>
              <div className="flex items-center justify-between px-6 py-3 border-t border-gray-100 bg-gray-50">
                <button type="button" onClick={() => setPreviewStaffRow(null)} className="px-5 py-2 rounded-xl text-sm font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100">Đóng</button>
                {!sentStaffMap[previewStaffRow.userId] && (
                  <button
                    type="button"
                    onClick={() => {
                      const row = previewStaffRow;
                      setPreviewStaffRow(null);
                      sendPersonnelReport(row);
                    }}
                    disabled={personnelSend.isSending(previewStaffRow.userId)}
                    className="px-5 py-2 rounded-xl text-sm font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                  >
                    {personnelSend.isSending(previewStaffRow.userId) ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                    Gửi email ngay
                  </button>
                )}
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Modal Xem trước / Chi tiết Email Báo cáo Phối hợp Phòng ban */}
      <AnimatePresence>
        {previewDeptRow && (
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
                    <h3 className="font-bold text-gray-900 text-base">Xem trước / Chi tiết Email Báo cáo Phối hợp</h3>
                    <p className="text-xs text-gray-500">Mô phỏng thư báo cáo gửi Trưởng phòng {previewDeptRow.name}</p>
                  </div>
                </div>
                <button type="button" onClick={() => setPreviewDeptRow(null)} className="text-gray-400 hover:text-gray-600 p-1.5 rounded-lg hover:bg-gray-100">
                  <X className="w-5 h-5" />
                </button>
              </div>

              <div className="p-6 overflow-y-auto space-y-4 text-sm bg-slate-50/50">
                <div className="bg-white p-4 rounded-xl border border-gray-200 space-y-2 shadow-sm">
                  <div className="flex items-center justify-between border-b border-gray-100 pb-2">
                    <span className="text-xs font-semibold text-gray-400">TRẠNG THÁI EMAIL:</span>
                    {sentDeptMap[previewDeptRow.departmentId] ? (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-green-50 text-green-700 border border-green-200">
                        <CheckCircle2 className="w-3.5 h-3.5" /> Đã gửi báo cáo ({sentDeptMap[previewDeptRow.departmentId]})
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-bold bg-blue-50 text-blue-700 border border-blue-200">
                        <Clock className="w-3.5 h-3.5" /> Xem trước (Chưa gửi)
                      </span>
                    )}
                  </div>
                  <div className="grid grid-cols-1 gap-2 text-xs">
                    <div><span className="font-semibold text-gray-500">Người gửi: </span><span className="font-normal text-gray-800">Staff Leader Office &lt;staffleader-reports@mail.pems-fpt.site&gt;</span></div>
                    <div><span className="font-semibold text-gray-500">Người nhận: </span><span className="font-normal text-[#004c91]">Trưởng phòng — {previewDeptRow.name}</span></div>
                    <div><span className="font-semibold text-gray-500">Tiêu đề: </span><span className="font-normal text-gray-900">[PEMS] Báo cáo phối hợp hỗ trợ tiếp khách — {previewDeptRow.name}</span></div>
                  </div>
                </div>

                <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
                  <div className="bg-[#004c91] text-white px-5 py-4 font-bold text-base flex items-center justify-between">
                    <span>PEMS Coordination Report</span>
                    <span className="text-xs font-normal opacity-80">{previewDeptRow.name}</span>
                  </div>
                  <div className="p-5 space-y-4 text-gray-700">
                    <p className="font-normal">Kính gửi <strong className="text-gray-900">Ban Trưởng phòng — {previewDeptRow.name}</strong>,</p>
                    <p className="text-sm leading-relaxed">Staff Leader xin gửi báo cáo tổng hợp tình hình phối hợp xử lý đơn yêu cầu hậu cần và thư mời hỗ trợ đợt công tác:</p>
                    <div className="bg-blue-50/60 rounded-xl p-4 border border-blue-100 space-y-2 text-xs">
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">TỔNG ĐƠN ĐÃ GỬI TỚI:</span><span className="font-normal text-[#004c91]">{previewDeptRow.totalRequests} đơn</span></div>
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">ĐƠN ĐÃ HOÀN THÀNH:</span><span className="font-normal text-green-700">{previewDeptRow.completed} đơn</span></div>
                      <div className="flex justify-between border-b border-blue-100 pb-2"><span className="font-bold text-gray-500">ĐÁNH GIÁ FEEDBACK:</span><span className="font-normal text-amber-600">{previewDeptRow.feedbackAverage ? `${previewDeptRow.feedbackAverage.toFixed(1)} ★` : 'Chưa có'}</span></div>
                      <div className="pt-1">
                        <span className="font-bold text-gray-500 block mb-1">GHI CHÚ GỬI TRƯỞNG PHÒNG (CÓ THỂ SỬA TRƯỚC KHI GỬI):</span>
                        {!sentDeptMap[previewDeptRow.departmentId] ? (
                          <textarea
                            value={deptNotes[previewDeptRow.departmentId] || ''}
                            onChange={(e) => setDeptNotes((s) => ({ ...s, [previewDeptRow.departmentId]: e.target.value }))}
                            placeholder="Nhập/chỉnh sửa nội dung ghi chú gửi Trưởng phòng..."
                            rows={3}
                            className="w-full text-xs p-2.5 rounded-lg border border-blue-200 focus:border-[#004c91] outline-none bg-white text-gray-800 shadow-xs"
                          />
                        ) : (
                          <p className="italic text-gray-600 bg-white p-2.5 rounded-lg border border-blue-100">{deptNotes[previewDeptRow.departmentId] || '(Không có ghi chú)'}</p>
                        )}
                      </div>
                    </div>
                    <div className="pt-3 border-t border-gray-100 text-xs text-gray-400">Trân trọng,<br /><strong>Ban Quản trị Staff Leader — FPT University</strong></div>
                  </div>
                </div>
              </div>
              <div className="flex items-center justify-between px-6 py-3 border-t border-gray-100 bg-gray-50">
                <button type="button" onClick={() => setPreviewDeptRow(null)} className="px-5 py-2 rounded-xl text-sm font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100">Đóng</button>
                {!sentDeptMap[previewDeptRow.departmentId] && (
                  <button
                    type="button"
                    onClick={() => {
                      const row = previewDeptRow;
                      setPreviewDeptRow(null);
                      sendDepartmentReport(row);
                    }}
                    disabled={departmentSend.isSending(previewDeptRow.departmentId)}
                    className="px-5 py-2 rounded-xl text-sm font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
                  >
                    {departmentSend.isSending(previewDeptRow.departmentId) ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
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
