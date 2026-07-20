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
  Users, X, XCircle, DollarSign,
} from 'lucide-react';
import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { reportsApi } from '../../../features/reports/api/reportsApi';
import type {
  StaffLeaderExpenseVisit, StaffLeaderExpenseVisits,
  StaffLeaderReportV2, StaffLeaderV2DepartmentRow,
  StaffLeaderV2Filters, StaffLeaderV2PersonnelRow, StaffLeaderV2Preset,
} from '../../../features/reports/types/staffLeaderReportsV2.types';

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
      {sub && <p className="text-[10px] font-medium opacity-75">{sub}</p>}
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
  const [filters, setFilters] = useState<StaffLeaderV2Filters>({ preset: 'THIS_YEAR', fromDate: '', toDate: '' });
  const [data, setData] = useState<StaffLeaderReportV2 | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Mỗi phần đóng/mở độc lập — mặc định mở cả 4.
  const [openSections, setOpenSections] = useState({ visits: true, personnel: true, departments: true, expenses: true });
  const toggleSection = (key: keyof typeof openSections) =>
    setOpenSections((s) => ({ ...s, [key]: !s[key] }));

  // ── Xuất báo cáo (PDF/Excel/CSV) — chọn phần 1/2/3 hoặc tất cả ──
  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [exportSections, setExportSections] = useState<string[]>(['VISITS', 'PERSONNEL', 'DEPARTMENTS']);
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
  const [sendingUserId, setSendingUserId] = useState<number | null>(null);
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

  const sendPersonnelReport = async (row: StaffLeaderV2PersonnelRow) => {
    setSendingUserId(row.userId);
    try {
      const res = await reportsApi.sendStaffLeaderPersonnelReport({
        userId: row.userId,
        fromDate: data?.fromDate,
        toDate: data?.toDate,
        note: personnelNotes[row.userId]?.trim() || undefined,
      });
      toast.success(res.message || `Đã gửi báo cáo hiệu suất qua email cho ${row.fullName}.`);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi báo cáo thất bại.');
    } finally {
      setSendingUserId(null);
    }
  };

  // ── Phần 3: ghi chú + gửi email báo cáo phối hợp cho từng phòng ban ──
  const [deptNotes, setDeptNotes] = useState<Record<number, string>>({});
  const [sendingDeptId, setSendingDeptId] = useState<number | null>(null);
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

  const sendDepartmentReport = async (row: StaffLeaderV2DepartmentRow) => {
    setSendingDeptId(row.departmentId);
    try {
      const res = await reportsApi.sendStaffLeaderDepartmentReport({
        departmentId: row.departmentId,
        fromDate: data?.fromDate,
        toDate: data?.toDate,
        note: deptNotes[row.departmentId]?.trim() || undefined,
      });
      toast.success(res.message || `Đã gửi báo cáo phối hợp qua email cho trưởng phòng ${row.name}.`);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi báo cáo thất bại.');
    } finally {
      setSendingDeptId(null);
    }
  };

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

  // "Xuất thống kê PDF" chi phí: cửa sổ in riêng (như exportInvoicePdf) gồm 2 phần —
  // (1) gộp bảng kê chi phí của từng đoàn, (2) thống kê theo loại + số tiền phải trả
  // từng phòng ban. Giữ bố cục gọn: mỗi đoàn 1 khối trong cùng 1 bảng.
  const exportExpensePdf = () => {
    if (!expenseData || expenseData.rows.length === 0) {
      toast.error('Chưa có dữ liệu chi phí để xuất thống kê.');
      return;
    }
    const esc = (s: string | null | undefined) =>
      (s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    const num = (v: number) => v.toLocaleString('vi-VN');

    // Phần 1 — chi tiết theo từng đoàn.
    const visitBlocks = expenseData.rows.map((v2, vIdx) => {
      const reportRows = v2.reports.map((r) => {
        const source = r.reportScope === 'GENERAL' ? 'Host' : (r.departmentName ?? 'Phòng ban');
        if (r.noExpense) {
          const title = r.logisticsItemTitle ?? 'Đơn yêu cầu';
          return `<tr><td colspan="5" style="color:#64748b">${esc(title)} — ${esc(source)}: <i>Không có chi phí</i></td><td style="text-align:right">0</td></tr>`;
        }
        const itemRows = r.items.map((it) => `
          <tr>
            <td>${esc(it.itemName)}</td>
            <td>${r.reportScope === 'LOGISTICS' ? 'Hạng mục yêu cầu' : esc(ORIGIN_LABELS[it.itemOrigin] ?? it.itemOrigin)}</td>
            <td>${esc(source)}</td>
            <td style="text-align:right">${it.quantity}</td>
            <td style="text-align:right">${num(it.unitPrice)}</td>
            <td style="text-align:right">${num(it.totalAmount)}</td>
          </tr>`).join('');
        const noteRow = r.reportNote
          ? `<tr><td colspan="6" style="color:#64748b;font-style:italic">Ghi chú (${esc(source)}): ${esc(r.reportNote)}</td></tr>`
          : '';
        return itemRows + noteRow;
      }).join('');
      return `
        <tr style="background:#eef2f7"><td colspan="5" style="font-weight:700">${vIdx + 1}. ${esc(v2.delegationName)} (${esc(v2.requestCode)}) — ${fmtDate(v2.visitDate)}</td>
          <td style="text-align:right;font-weight:700">${num(v2.totalExpense)}</td></tr>
        ${reportRows}`;
    }).join('');

    // Phần 2 — theo loại (bảng kê phòng ban tính là "Hạng mục yêu cầu") + theo phòng ban.
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
    const typeRows = [...byType.entries()].sort((a, b) => b[1].total - a[1].total)
      .map(([label, t2]) => `<tr><td>${esc(label)}</td><td style="text-align:right">${t2.count}</td><td style="text-align:right">${num(t2.total)}</td></tr>`)
      .join('');
    const deptRows2 = [...byDept.entries()].sort((a, b) => b[1].total - a[1].total)
      .map(([name, d2]) => `<tr><td>${esc(name)}</td><td style="text-align:right">${d2.count}</td><td style="text-align:right">${num(d2.total)}</td></tr>`)
      .join('');

    const html = `<!doctype html><html><head><meta charset="utf-8" />
      <title>Thống kê chi phí tiếp khách — ${esc(data?.campusName)}</title>
      <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; color: #0f172a; padding: 28px; }
        .top { display: flex; justify-content: space-between; border-bottom: 1px solid #cbd5e1; padding-bottom: 12px; margin-bottom: 22px; font-size: 12px; }
        h2 { text-align: center; text-transform: uppercase; margin: 4px 0 2px; font-size: 20px; }
        h3 { font-size: 14px; text-transform: uppercase; color: #004c91; margin: 22px 0 8px; }
        .sub { text-align: center; font-size: 14px; margin-bottom: 20px; }
        table { width: 100%; border-collapse: collapse; font-size: 12px; }
        th, td { border: 1px solid #475569; padding: 5px 7px; }
        th { background: #f1f5f9; }
        .total td { font-weight: 700; background: #fff7ed; }
      </style></head><body>
      <div class="top">
        <div>
          <div style="font-weight:800;text-transform:uppercase;font-size:13px">TRƯỜNG ĐẠI HỌC FPT — ${esc(data?.campusName)}</div>
          <div style="color:#64748b;font-weight:600">Văn phòng IC · Hệ thống PEMS</div>
        </div>
        <div style="text-align:right">
          <div style="font-weight:800;font-size:11px">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</div>
          <div style="font-weight:800;font-size:11px;color:#f37021">Độc lập - Tự do - Hạnh phúc</div>
        </div>
      </div>
      <h2>BẢNG THỐNG KÊ CHI PHÍ TIẾP KHÁCH</h2>
      <p class="sub">Kỳ: <b>${fmtDate(expenseRange.fromDate)} – ${fmtDate(expenseRange.toDate)}</b> · ${expenseData.rows.length} đoàn</p>

      <h3>Phần 1 · Chi phí theo từng đoàn</h3>
      <table>
        <thead><tr><th>Hạng mục</th><th>Loại</th><th>Bên kê khai</th><th>SL</th><th>Đơn giá (₫)</th><th>Thành tiền (₫)</th></tr></thead>
        <tbody>${visitBlocks}</tbody>
        <tfoot><tr class="total"><td colspan="5" style="text-align:right;text-transform:uppercase">Tổng chi phí các đoàn</td><td style="text-align:right">${num(expenseData.totalAmount)}</td></tr></tfoot>
      </table>

      <h3>Phần 2 · Thống kê theo loại chi phí</h3>
      <table>
        <thead><tr><th>Loại</th><th style="width:90px">Số hạng mục</th><th style="width:130px">Tổng tiền (₫)</th></tr></thead>
        <tbody>${typeRows || '<tr><td colspan="3" style="text-align:center;color:#64748b">Không có hạng mục nào</td></tr>'}</tbody>
      </table>

      <h3>Phần 3 · Chi phí phải thanh toán cho từng phòng ban</h3>
      <table>
        <thead><tr><th>Phòng ban</th><th style="width:90px">Số đơn</th><th style="width:130px">Tổng tiền (₫)</th></tr></thead>
        <tbody>${deptRows2 || '<tr><td colspan="3" style="text-align:center;color:#64748b">Không có phòng ban nào kê khai chi phí</td></tr>'}</tbody>
      </table>
      </body></html>`;
    const win = window.open('', '_blank', 'width=980,height=720');
    if (!win) {
      toast.error('Trình duyệt đang chặn popup — hãy cho phép popup cho trang này rồi thử lại.');
      return;
    }
    win.document.write(html);
    win.document.close();
    win.focus();
    setTimeout(() => win.print(), 350);
    toast.success('Đã mở bản in thống kê — chọn "Save as PDF" để lưu.');
  };



  const v = data?.visits;
  const p = data?.personnel;
  const d = data?.departments;

  return (
    <div className="w-full space-y-8 pb-16 animate-in fade-in duration-300">
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
                  { key: 'PERSONNEL', label: 'Phần 2 · Nhân sự' },
                  { key: 'DEPARTMENTS', label: 'Phần 3 · Phòng ban khác' },
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
                    checked={exportSections.length === 3}
                    onChange={() => setExportSections(exportSections.length === 3 ? [] : ['VISITS', 'PERSONNEL', 'DEPARTMENTS'])}
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
              onClick={() => setFilters((f) => ({ ...f, preset: pr.value }))}
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
          onClick={() => fetchReport(filters)}
          disabled={loading}
          className="ml-auto p-2 rounded-xl hover:bg-slate-100 text-slate-500 transition-colors cursor-pointer"
          title="Tải lại"
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

            {/* Biểu đồ đường tiến độ hợp tác đối tác — mốc trục thời gian đổi theo khoảng lọc */}
            <div className="mt-6">
              <h3 className="text-sm font-bold text-slate-700 mb-1">Tiến độ hợp tác với đối tác</h3>
              <p className="text-xs text-slate-400 mb-3">
                Lũy kế đối tác đã duyệt và số chuyến thăm gắn với đối tác theo{' '}
                {{ YEAR: 'năm', MONTH: 'tháng', WEEK: 'tuần', DAY: 'ngày', HOUR: 'giờ' }[v!.trendGranularity] ?? 'tháng'}
                {' '}— đường đi lên nghĩa là hợp tác đang tăng. Dữ liệu thuộc riêng {data.campusName}.
              </p>
              <div className="h-72">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={v!.partnerTrend} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
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
          </Section>

          {/* ═══ 2 · Báo cáo nhân sự ═══ */}
          <Section
            index={2}
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
                          <button
                            type="button"
                            onClick={() => sendPersonnelReport(row)}
                            disabled={sendingUserId === row.userId}
                            title={`Gửi báo cáo hiệu suất qua email cho ${row.fullName}`}
                            className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-[#004c91] text-xs font-bold rounded-lg border border-blue-200 transition-colors disabled:opacity-50 cursor-pointer"
                          >
                            {sendingUserId === row.userId ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                            Gửi
                          </button>
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

          {/* ═══ 3 · Báo cáo phòng ban khác ═══ */}
          <Section
            index={3}
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
                        <td className={`${tdClass} whitespace-nowrap text-emerald-700 font-semibold`}>{row.completed}</td>
                        <td className={`${tdClass} whitespace-nowrap text-rose-600 font-semibold`}>{row.rejected}</td>
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
                          <button
                            type="button"
                            onClick={() => sendDepartmentReport(row)}
                            disabled={sendingDeptId === row.departmentId}
                            title={`Gửi báo cáo phối hợp qua email cho trưởng phòng ${row.name}`}
                            className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-[#004c91] text-xs font-bold rounded-lg border border-blue-200 transition-colors disabled:opacity-50 cursor-pointer"
                          >
                            {sendingDeptId === row.departmentId ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                            Gửi
                          </button>
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

          {/* ═══ 4 · Thống kê chi phí ═══ */}
          <Section
            index={4}
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
                              <td className={`${tdClass} whitespace-nowrap text-emerald-700 font-bold`}>{vnMoney(row.totalExpense)}</td>
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
                <span className="block text-[11px] font-medium opacity-75">
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
                            <td className="pl-2 pr-3 py-1 text-right font-bold text-slate-700 whitespace-nowrap w-28">{vnMoney(it.totalAmount)}</td>
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
    </div>
  );
}
