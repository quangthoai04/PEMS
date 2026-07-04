/**
 * Trang DeptReportManagement — Báo cáo hiệu suất phòng ban tại /dashboard/reports
 * (role DEPARTMENT · LEADER). Department operation dashboard: KPI strip compact,
 * khối "Cần xử lý ngay", tabs (Tổng quan / Công việc / Nhân sự / Bàn giao /
 * Phát sinh & Feedback / Hóa đơn) và xuất hóa đơn PDF theo đơn giá leader nhập.
 * Toàn bộ dữ liệu lấy từ GET /reports/department-leader-overview — scope đúng
 * department của leader (backend enforce), không mock.
 */

import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  AlertTriangle, CheckCircle2, ChevronDown, Download, FileSpreadsheet, FileText,
  Info, Loader2, Paperclip, ReceiptText, RefreshCw, ShieldAlert, X,
} from 'lucide-react';
import {
  CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { useDeptLeaderReport } from '../../../features/reports/hooks/useReports';
import { reportsAdapter as fmt } from '../../../features/reports/adapters/reportsAdapter';
import type {
  DeptLeaderExportFormat, DeptLeaderFeedbackEntry, DeptLeaderInvoiceItem,
  DeptLeaderReportOverview, DeptLeaderReportSection,
} from '../../../features/reports/types/deptLeaderReports.types';

// Palette chart đã validate CVD/contrast (scripts/validate_palette.js — ALL PASS light & dark,
// cùng bộ với StaffLeaderReportManagement).
const CHART_BLUE = '#1e6fc0';
const CHART_ORANGE = '#d95f18';
const CHART_GREEN = '#0a8a44';

type TabKey = 'overview' | 'tasks' | 'staff' | 'handover' | 'incidents' | 'invoice';

const TABS: { key: TabKey; label: string }[] = [
  { key: 'overview', label: 'Tổng quan' },
  { key: 'tasks', label: 'Công việc' },
  { key: 'staff', label: 'Nhân sự' },
  { key: 'handover', label: 'Bàn giao' },
  { key: 'incidents', label: 'Phát sinh & Feedback' },
  { key: 'invoice', label: 'Hóa đơn' },
];

// Backend attention.targetSection → tab trên UI.
const SECTION_TO_TAB: Record<string, TabKey> = {
  TASKS: 'tasks',
  STAFF: 'staff',
  HANDOVER: 'handover',
  INCIDENTS: 'incidents',
};

const SECTION_OPTIONS: { value: DeptLeaderReportSection; label: string }[] = [
  { value: 'EXECUTIVE_SUMMARY', label: 'Executive Summary' },
  { value: 'TASK_PIPELINE', label: 'Công việc & Đề xuất' },
  { value: 'STAFF_PERFORMANCE', label: 'Hiệu suất nhân sự' },
  { value: 'HANDOVER_SUMMARY', label: 'Bàn giao / ký nhận' },
  { value: 'INCIDENT_SUMMARY', label: 'Phát sinh sau bàn giao' },
  { value: 'FEEDBACK_SUMMARY', label: 'Feedback' },
];

const PRESET_LABELS: Record<string, string> = {
  THIS_MONTH: 'Tháng này',
  THIS_QUARTER: 'Quý này',
  THIS_YEAR: 'Năm nay',
  CUSTOM: 'Tùy chỉnh',
};

const ITEM_TYPE_LABELS: Record<string, string> = {
  ROOM: 'Phòng / địa điểm',
  TRANSPORT: 'Phương tiện / xe',
  MEAL: 'Trà nước / đồ ăn',
  EQUIPMENT: 'Thiết bị',
  BANNER: 'Banner / ấn phẩm',
  LED: 'LED / màn hình',
  DEPARTMENT: 'Phòng ban (chung)',
  OTHER: 'Khác',
};
const itemTypeLabel = (t: string) => ITEM_TYPE_LABELS[t] ?? t;

const TASK_STATUS_LABELS: Record<string, string> = {
  REQUESTED: 'Yêu cầu mới',
  CHANGE_PROPOSED: 'Đề xuất thay đổi',
  ASSIGNED: 'Chờ phản hồi',
  ACCEPTED: 'Đã nhận',
  IN_PROGRESS: 'Đang xử lý',
  DONE: 'Hoàn thành',
  REJECTED: 'PB từ chối',
  DECLINED: 'NS từ chối',
  CANCELLED: 'Đã hủy',
};
const taskStatusLabel = (s: string) => TASK_STATUS_LABELS[s] ?? s;

const TASK_STATUS_BADGES: Record<string, string> = {
  REQUESTED: 'bg-sky-50 text-sky-700 border-sky-200',
  CHANGE_PROPOSED: 'bg-violet-50 text-violet-700 border-violet-200',
  ASSIGNED: 'bg-amber-50 text-amber-700 border-amber-200',
  ACCEPTED: 'bg-blue-50 text-blue-700 border-blue-200',
  IN_PROGRESS: 'bg-indigo-50 text-indigo-700 border-indigo-200',
  DONE: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  REJECTED: 'bg-red-50 text-red-600 border-red-200',
  DECLINED: 'bg-red-50 text-red-600 border-red-200',
  CANCELLED: 'bg-slate-100 text-slate-500 border-slate-200',
};
const taskStatusBadge = (s: string) => TASK_STATUS_BADGES[s] ?? 'bg-slate-100 text-slate-500 border-slate-200';

const PRIORITY_LABELS: Record<string, string> = {
  URGENT: 'Khẩn cấp', HIGH: 'Cao', MEDIUM: 'Trung bình', LOW: 'Thấp',
};
const PRIORITY_BADGES: Record<string, string> = {
  URGENT: 'bg-red-50 text-red-600 border-red-200',
  HIGH: 'bg-orange-50 text-orange-700 border-orange-200',
  MEDIUM: 'bg-slate-100 text-slate-600 border-slate-200',
  LOW: 'bg-slate-50 text-slate-400 border-slate-200',
};

const SEVERITY_STYLES: Record<string, { dot: string; text: string }> = {
  DANGER: { dot: 'bg-red-500', text: 'text-red-600' },
  WARNING: { dot: 'bg-amber-500', text: 'text-amber-600' },
  INFO: { dot: 'bg-sky-500', text: 'text-sky-600' },
  SUCCESS: { dot: 'bg-emerald-500', text: 'text-emerald-600' },
};

const formatVnd = (v: number) => `${new Intl.NumberFormat('vi-VN').format(Math.round(v))} đ`;

const selectClass =
  'bg-white border border-slate-200 rounded-lg px-2.5 py-2 text-sm font-medium text-slate-700 outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] cursor-pointer';
const thClass = 'px-3 py-2.5 text-[11px] font-bold text-slate-400 uppercase tracking-wide whitespace-nowrap';
const tdClass = 'px-3 py-2.5 text-sm text-slate-600 whitespace-nowrap';

export function DeptReportManagement() {
  const navigate = useNavigate();
  const {
    filters, setFilters, data, loading, error, refetch,
    applyFilters, resetFilters, exportReport, exportLoading,
    invoiceVisits, invoiceItems, invoiceVisitsLoading, invoiceItemsLoading,
    invoiceExportLoading, fetchInvoiceVisits, fetchInvoiceItems, exportInvoicePdf,
  } = useDeptLeaderReport();

  const [tab, setTab] = useState<TabKey>('overview');
  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [exportConfirm, setExportConfirm] = useState<DeptLeaderExportFormat | null>(null);
  const [exportSections, setExportSections] = useState<DeptLeaderReportSection[]>(SECTION_OPTIONS.map((s) => s.value));
  const exportMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const close = (e: MouseEvent) => {
      if (exportMenuRef.current && !exportMenuRef.current.contains(e.target as Node)) setExportMenuOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  // Giữ danh sách nhân sự đầy đủ từ lần load không lọc để option không biến mất khi lọc.
  const [staffOptions, setStaffOptions] = useState<{ id: number; name: string }[]>([]);
  useEffect(() => {
    if (!data) return;
    if (data.filterSummary.assignedUserId === 'ALL') {
      setStaffOptions(data.staffPerformance.map((s) => ({ id: s.userId, name: s.fullName })));
    }
  }, [data]);

  const handleExport = async () => {
    if (!exportConfirm) return;
    try {
      await exportReport(exportConfirm, exportSections);
      toast.success('Đã xuất báo cáo thành công.');
      setExportConfirm(null);
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status;
      toast.error(status === 403
        ? 'Bạn không có quyền xuất báo cáo này.'
        : 'Không thể xuất báo cáo. Vui lòng thử lại sau.');
    }
  };

  if (error === 'FORBIDDEN') {
    return (
      <div className="max-w-[1400px] mx-auto flex flex-col items-center justify-center py-24 text-center">
        <ShieldAlert className="w-12 h-12 text-red-400 mb-4" />
        <h2 className="text-xl font-bold text-slate-800 mb-1">Bạn không có quyền xem báo cáo Department Leader</h2>
        <p className="text-sm text-slate-500">Vui lòng liên hệ quản trị viên nếu bạn cho rằng đây là nhầm lẫn.</p>
      </div>
    );
  }

  return (
    <div className="max-w-[1400px] mx-auto space-y-4 pb-12 font-sans animate-in fade-in duration-300">
      {/* ── Header compact ── */}
      <div>
        <div className="flex items-center gap-2 text-xs font-medium text-slate-500 mb-1">
          <span>Dashboard</span>
          <span>/</span>
          <span className="text-[#004c91] font-bold">Thống kê phòng ban</span>
        </div>
        <div className="flex flex-col md:flex-row md:items-end justify-between gap-3">
          <div>
            <div className="flex items-center gap-3 flex-wrap">
              <h1 className="text-2xl font-black text-[#004c91] tracking-tight">Báo cáo hiệu suất phòng ban</h1>
              <span className="text-[11px] font-bold uppercase tracking-wide text-[#004c91] bg-blue-50 border border-blue-100 rounded-full px-2.5 py-1">
                Department Leader{data ? ` · ${data.filterSummary.departmentName}` : ''}
              </span>
            </div>
            <p className="text-sm font-medium text-slate-500 mt-0.5">
              Tổng quan công việc, nhân sự, bàn giao và phát sinh của phòng ban
            </p>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => setTab('invoice')}
              disabled={loading || !data}
              className="flex items-center gap-2 px-4 py-2.5 bg-white border border-[#004c91]/30 text-[#004c91] text-sm font-bold rounded-xl hover:bg-blue-50 transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
            >
              <ReceiptText className="w-4 h-4" />
              Xuất hóa đơn PDF
            </button>
            {/* Export dropdown */}
            <div className="relative" ref={exportMenuRef}>
              <button
                onClick={() => setExportMenuOpen((v) => !v)}
                disabled={loading || !data}
                className="flex items-center gap-2 px-4 py-2.5 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:bg-[#00386b] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
              >
                <Download className="w-4 h-4" />
                Xuất báo cáo
                <ChevronDown className="w-4 h-4" />
              </button>
              {exportMenuOpen && (
                <div className="absolute right-0 mt-2 w-56 bg-white border border-slate-200 rounded-xl shadow-lg z-20 overflow-hidden">
                  {([
                    { format: 'EXCEL' as DeptLeaderExportFormat, label: 'Excel (.xlsx)', icon: FileSpreadsheet },
                    { format: 'PDF' as DeptLeaderExportFormat, label: 'PDF (.pdf)', icon: FileText },
                    { format: 'CSV' as DeptLeaderExportFormat, label: 'CSV (.csv)', icon: FileText },
                  ]).map((opt) => (
                    <button
                      key={opt.format}
                      onClick={() => { setExportMenuOpen(false); setExportConfirm(opt.format); }}
                      className="w-full flex items-center gap-2.5 px-4 py-2.5 text-sm font-medium text-slate-700 hover:bg-slate-50 transition-colors text-left cursor-pointer"
                    >
                      <opt.icon className="w-4 h-4 text-slate-400" />
                      {opt.label}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* ── Filter bar ── */}
      <div className="bg-white border border-slate-200 rounded-xl p-3 flex flex-wrap items-center gap-2">
        <select
          value={filters.preset}
          onChange={(e) => setFilters({ ...filters, preset: e.target.value })}
          className={selectClass}
          aria-label="Khoảng thời gian"
        >
          <option value="THIS_MONTH">Tháng này</option>
          <option value="THIS_QUARTER">Quý này</option>
          <option value="THIS_YEAR">Năm nay</option>
          <option value="CUSTOM">Tùy chỉnh…</option>
        </select>

        {filters.preset === 'CUSTOM' && (
          <>
            <input
              type="date"
              value={filters.fromDate ?? ''}
              onChange={(e) => setFilters({ ...filters, fromDate: e.target.value })}
              className={selectClass}
              aria-label="Từ ngày"
            />
            <span className="text-slate-400 text-sm">→</span>
            <input
              type="date"
              value={filters.toDate ?? ''}
              onChange={(e) => setFilters({ ...filters, toDate: e.target.value })}
              className={selectClass}
              aria-label="Đến ngày"
            />
          </>
        )}

        <select
          value={filters.logisticsStatus}
          onChange={(e) => setFilters({ ...filters, logisticsStatus: e.target.value })}
          className={selectClass}
          aria-label="Trạng thái"
        >
          <option value="ALL">Trạng thái: Tất cả</option>
          {Object.entries(TASK_STATUS_LABELS).map(([value, label]) => (
            <option key={value} value={value}>{label}</option>
          ))}
        </select>

        <select
          value={filters.itemType}
          onChange={(e) => setFilters({ ...filters, itemType: e.target.value })}
          className={selectClass}
          aria-label="Mảng việc"
        >
          <option value="ALL">Mảng việc: Tất cả</option>
          {['ROOM', 'TRANSPORT', 'MEAL', 'EQUIPMENT', 'BANNER', 'LED', 'OTHER'].map((t) => (
            <option key={t} value={t}>{itemTypeLabel(t)}</option>
          ))}
        </select>

        <select
          value={filters.priority}
          onChange={(e) => setFilters({ ...filters, priority: e.target.value })}
          className={selectClass}
          aria-label="Ưu tiên"
        >
          <option value="ALL">Ưu tiên: Tất cả</option>
          <option value="URGENT">Khẩn cấp</option>
          <option value="HIGH">Cao</option>
          <option value="MEDIUM">Trung bình</option>
          <option value="LOW">Thấp</option>
        </select>

        <select
          value={filters.assignedUserId}
          onChange={(e) => setFilters({ ...filters, assignedUserId: e.target.value })}
          className={selectClass}
          aria-label="Nhân sự"
        >
          <option value="ALL">Nhân sự: Tất cả</option>
          {staffOptions.map((s) => (
            <option key={s.id} value={String(s.id)}>{s.name}</option>
          ))}
        </select>

        <select
          value={filters.dueStatus}
          onChange={(e) => setFilters({ ...filters, dueStatus: e.target.value })}
          className={selectClass}
          aria-label="Deadline"
        >
          <option value="ALL">Deadline: Tất cả</option>
          <option value="DUE_SOON">Sắp đến hạn (72h)</option>
          <option value="OVERDUE">Quá hạn</option>
        </select>

        <select
          value={filters.handoverStatus}
          onChange={(e) => setFilters({ ...filters, handoverStatus: e.target.value })}
          className={selectClass}
          aria-label="Bàn giao"
        >
          <option value="ALL">Bàn giao: Tất cả</option>
          <option value="COMPLETE">Đủ chữ ký</option>
          <option value="MISSING_SIGNATURE">Thiếu chữ ký</option>
          <option value="DAMAGED">Có hư hỏng</option>
          <option value="MISSING">Thiếu/mất</option>
        </select>

        <select
          value={filters.feedbackRating}
          onChange={(e) => setFilters({ ...filters, feedbackRating: e.target.value })}
          className={selectClass}
          aria-label="Mức đánh giá"
        >
          <option value="ALL">Rating: Tất cả</option>
          <option value="LOW">Thấp (≤ 2)</option>
          <option value="HIGH">Tốt (≥ 4)</option>
        </select>

        {data && (
          <span className="text-xs font-semibold text-slate-400 border border-dashed border-slate-200 rounded-lg px-2.5 py-2">
            Phòng ban: {data.filterSummary.departmentName}
          </span>
        )}

        <div className="flex items-center gap-2 ml-auto">
          <button
            onClick={() => applyFilters()}
            disabled={loading}
            className="px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-lg hover:bg-[#00386b] transition-colors disabled:opacity-50 cursor-pointer"
          >
            Áp dụng
          </button>
          <button
            onClick={resetFilters}
            disabled={loading}
            className="px-3 py-2 text-sm font-bold text-slate-500 hover:text-slate-700 hover:bg-slate-100 rounded-lg transition-colors disabled:opacity-50 cursor-pointer"
          >
            Reset
          </button>
        </div>
      </div>

      {/* ── Error state ── */}
      {error === 'ERROR' && (
        <div className="bg-white border border-slate-200 rounded-xl p-10 flex flex-col items-center text-center">
          <AlertTriangle className="w-8 h-8 text-amber-500 mb-3" />
          <p className="text-sm font-semibold text-slate-700 mb-3">Không thể tải báo cáo. Vui lòng thử lại.</p>
          <button
            onClick={refetch}
            className="flex items-center gap-2 px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-lg hover:bg-[#00386b] cursor-pointer"
          >
            <RefreshCw className="w-4 h-4" /> Thử lại
          </button>
        </div>
      )}

      {/* ── Loading skeleton ── */}
      {loading && !error && <ReportSkeleton />}

      {!loading && !error && data && (
        <>
          <KpiStrip data={data} />
          <AttentionBar data={data} onView={(section) => setTab(SECTION_TO_TAB[section] ?? 'tasks')} />

          {/* ── Tabs ── */}
          <div className="bg-white border border-slate-200 rounded-xl px-3 pt-1.5 flex items-center gap-1 overflow-x-auto">
            {TABS.map((t) => (
              <button
                key={t.key}
                onClick={() => setTab(t.key)}
                className={`px-4 py-2.5 text-sm font-bold rounded-t-lg border-b-2 whitespace-nowrap transition-colors cursor-pointer ${
                  tab === t.key ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-slate-400 hover:text-slate-600'
                }`}
              >
                {t.label}
              </button>
            ))}
          </div>

          {tab === 'overview' && <OverviewTab data={data} />}
          {tab === 'tasks' && <TasksTab data={data} onOpen={() => navigate('/dashboard/visit')} />}
          {tab === 'staff' && <StaffTab data={data} />}
          {tab === 'handover' && <HandoverTab data={data} />}
          {tab === 'incidents' && <IncidentsTab data={data} />}
          {tab === 'invoice' && (
            <InvoiceTab
              visits={invoiceVisits}
              items={invoiceItems}
              visitsLoading={invoiceVisitsLoading}
              itemsLoading={invoiceItemsLoading}
              exportLoading={invoiceExportLoading}
              onLoadVisits={fetchInvoiceVisits}
              onLoadItems={fetchInvoiceItems}
              onExport={exportInvoicePdf}
            />
          )}

          <p className="text-[11px] text-slate-400 text-right">
            Số liệu theo kỳ tính bằng ngày thăm dự kiến · Khối tác vụ (chưa phân công, chờ phản hồi, quá hạn…)
            tính theo trạng thái hiện tại · Cập nhật {fmt.formatDateTime(data.generatedAt)}
          </p>
        </>
      )}

      {/* ── Modal xác nhận export ── */}
      {exportConfirm && data && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100">
              <h3 className="text-base font-bold text-slate-800">Xuất báo cáo phòng ban</h3>
              <button onClick={() => setExportConfirm(null)} className="text-slate-400 hover:text-slate-600 cursor-pointer" aria-label="Đóng">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="px-5 py-4 space-y-3 text-sm">
              <div className="grid grid-cols-[110px_1fr] gap-y-1.5 text-slate-600">
                <span className="font-semibold text-slate-400">Định dạng</span>
                <span className="font-bold text-slate-800">{exportConfirm}</span>
                <span className="font-semibold text-slate-400">Phòng ban</span>
                <span>{data.filterSummary.departmentName}</span>
                <span className="font-semibold text-slate-400">Thời gian</span>
                <span>{PRESET_LABELS[data.filterSummary.preset]} ({fmt.formatDate(data.filterSummary.fromDate)} – {fmt.formatDate(data.filterSummary.toDate)})</span>
                <span className="font-semibold text-slate-400">Bộ lọc</span>
                <span>
                  Trạng thái: {data.filterSummary.logisticsStatus === 'ALL' ? 'Tất cả' : taskStatusLabel(data.filterSummary.logisticsStatus)}
                  {' '}· Mảng việc: {data.filterSummary.itemType === 'ALL' ? 'Tất cả' : itemTypeLabel(data.filterSummary.itemType)}
                  {' '}· Nhân sự: {data.filterSummary.assignedUserId === 'ALL' ? 'Tất cả' : data.filterSummary.assignedUserName ?? data.filterSummary.assignedUserId}
                  {' '}· Rating: {data.filterSummary.feedbackRating === 'ALL' ? 'Tất cả' : data.filterSummary.feedbackRating}
                </span>
              </div>
              <div>
                <p className="font-semibold text-slate-400 mb-1.5">Section</p>
                <div className="grid grid-cols-1 gap-1">
                  {SECTION_OPTIONS.map((s) => (
                    <label key={s.value} className="flex items-center gap-2 text-slate-700 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={exportSections.includes(s.value)}
                        onChange={(e) => setExportSections((prev) =>
                          e.target.checked ? [...prev, s.value] : prev.filter((v) => v !== s.value))}
                        className="rounded border-slate-300 text-[#004c91] focus:ring-[#004c91]/30"
                      />
                      {s.label}
                    </label>
                  ))}
                </div>
              </div>
            </div>
            <div className="flex justify-end gap-2 px-5 py-4 border-t border-slate-100 bg-slate-50">
              <button
                onClick={() => setExportConfirm(null)}
                className="px-4 py-2 text-sm font-bold text-slate-500 hover:text-slate-700 rounded-lg hover:bg-slate-100 cursor-pointer"
              >
                Hủy
              </button>
              <button
                onClick={handleExport}
                disabled={exportLoading || exportSections.length === 0}
                className="flex items-center gap-2 px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-lg hover:bg-[#00386b] disabled:opacity-50 cursor-pointer"
              >
                {exportLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <Download className="w-4 h-4" />}
                Xuất báo cáo
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ───────────────────────────── Skeleton ─────────────────────────────

function ReportSkeleton() {
  return (
    <div className="space-y-4 animate-pulse">
      <div className="bg-white border border-slate-200 rounded-xl grid grid-cols-2 sm:grid-cols-3 xl:grid-cols-6 gap-px overflow-hidden">
        {Array.from({ length: 6 }).map((_, i) => (
          <div key={i} className="p-4 space-y-2">
            <div className="h-3 bg-slate-100 rounded w-2/3" />
            <div className="h-6 bg-slate-200 rounded w-1/2" />
          </div>
        ))}
      </div>
      <div className="h-12 bg-white border border-slate-200 rounded-xl" />
      <div className="grid grid-cols-1 lg:grid-cols-5 gap-4">
        <div className="lg:col-span-3 h-[320px] bg-white border border-slate-200 rounded-xl" />
        <div className="lg:col-span-2 h-[320px] bg-white border border-slate-200 rounded-xl" />
      </div>
      <div className="h-56 bg-white border border-slate-200 rounded-xl" />
    </div>
  );
}

// ───────────────────────────── KPI strip (6 KPI theo spec) ─────────────────────────────

function KpiStrip({ data }: { data: DeptLeaderReportOverview }) {
  const k = data.kpis;
  const items: { label: string; value: string; sub?: string; tone?: 'warn' | 'danger' | 'good'; title?: string }[] = [
    { label: 'Yêu cầu mới', value: fmt.formatNumber(k.newRequests), sub: `${k.waitingAssignment} chưa phân công`, tone: k.waitingAssignment > 0 ? 'warn' : undefined, title: 'Yêu cầu logistics ở trạng thái REQUESTED (hiện tại)' },
    { label: 'Chờ phản hồi', value: fmt.formatNumber(k.waitingStaffResponse), sub: 'nhân sự chưa nhận việc', tone: k.waitingStaffResponse > 0 ? 'warn' : undefined },
    { label: 'Đang xử lý', value: fmt.formatNumber(k.inProgress), sub: 'nhiệm vụ đang thực hiện' },
    { label: 'Hoàn thành', value: fmt.formatNumber(k.completed), sub: 'trong kỳ báo cáo', tone: 'good' },
    { label: 'Quá hạn', value: fmt.formatNumber(k.overdue), sub: 'qua deadline chưa xong', tone: k.overdue > 0 ? 'danger' : undefined },
    { label: 'Thiếu ký', value: fmt.formatNumber(k.missingHandoverSignature), sub: 'biên bản bàn giao', tone: k.missingHandoverSignature > 0 ? 'warn' : undefined },
  ];

  return (
    <div className="bg-slate-200 border border-slate-200 rounded-xl grid grid-cols-2 sm:grid-cols-3 xl:grid-cols-6 gap-px overflow-hidden">
      {items.map((item) => (
        <div key={item.label} className="bg-white px-4 py-3" title={item.title}>
          <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wide truncate">{item.label}</p>
          <p className={`text-xl font-black mt-0.5 ${
            item.tone === 'warn' ? 'text-amber-600' : item.tone === 'danger' ? 'text-red-600' : item.tone === 'good' ? 'text-emerald-600' : 'text-slate-800'
          }`}>
            {item.value}
          </p>
          {item.sub && <p className="text-[11px] font-medium text-slate-400 truncate">{item.sub}</p>}
        </div>
      ))}
    </div>
  );
}

// ───────────────────────────── Cần xử lý ngay ─────────────────────────────

function AttentionBar({ data, onView }: { data: DeptLeaderReportOverview; onView: (section: string) => void }) {
  const actionable = data.attentionItems.filter((a) => a.count > 0);
  return (
    <div className="bg-white border border-slate-200 rounded-xl px-4 py-3">
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs font-bold text-slate-500 uppercase tracking-wide mr-1 shrink-0">Cần xử lý ngay</span>
        {actionable.length === 0 && (
          <span className="flex items-center gap-1.5 text-sm font-medium text-emerald-600">
            <CheckCircle2 className="w-4 h-4" /> Không có công việc cần xử lý
          </span>
        )}
        {actionable.map((a) => {
          const style = SEVERITY_STYLES[a.severity] ?? SEVERITY_STYLES.INFO;
          return (
            <div
              key={a.key}
              title={a.description}
              className="flex items-center gap-2 border border-slate-200 rounded-lg pl-2.5 pr-1.5 py-1.5 bg-slate-50/60"
            >
              <span className={`w-2 h-2 rounded-full shrink-0 ${style.dot}`} />
              <span className="text-xs font-semibold text-slate-600">{a.label}</span>
              <span className={`text-sm font-black ${style.text}`}>{a.count}</span>
              <button
                onClick={() => onView(a.targetSection)}
                className="text-[11px] font-bold text-[#004c91] hover:bg-blue-50 rounded px-1.5 py-0.5 cursor-pointer"
              >
                Xem
              </button>
            </div>
          );
        })}
      </div>
    </div>
  );
}

// ───────────────────────────── Empty helpers ─────────────────────────────

function ChartEmpty({ height }: { height: number }) {
  return (
    <div style={{ height }} className="flex flex-col items-center justify-center text-slate-400">
      <Info className="w-6 h-6 mb-2" />
      <p className="text-sm font-medium">Không có dữ liệu trong khoảng thời gian đã chọn.</p>
    </div>
  );
}

function SectionEmpty({ text }: { text: string }) {
  return (
    <div className="flex flex-col items-center justify-center text-slate-400 py-6">
      <Info className="w-5 h-5 mb-1.5" />
      <p className="text-sm font-medium">{text}</p>
    </div>
  );
}

// ───────────────────────────── Tab: Tổng quan ─────────────────────────────

function OverviewTab({ data }: { data: DeptLeaderReportOverview }) {
  const trend = data.monthlyTrend;
  const trendEmpty = trend.length === 0 || trend.every((m) => m.totalTasks === 0);
  const pipeline = data.taskStatusPipeline.filter((s) => s.count > 0);
  const pipelineTotal = data.taskStatusPipeline.reduce((sum, s) => sum + s.count, 0);
  const pipelineMax = Math.max(1, ...pipeline.map((s) => s.count));
  const workTypes = data.workTypeDistribution;
  const workTypeMax = Math.max(1, ...workTypes.map((w) => w.count));

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Task status pipeline — horizontal bars, label trực tiếp từng dòng */}
        <div className="bg-white border border-slate-200 rounded-xl p-4 flex flex-col">
          <h3 className="text-sm font-bold text-slate-800">Trạng thái công việc</h3>
          <p className="text-xs text-slate-400 font-medium mb-2">{fmt.formatNumber(pipelineTotal)} nhiệm vụ trong kỳ</p>
          {pipelineTotal === 0 ? (
            <ChartEmpty height={230} />
          ) : (
            <div className="flex-1 flex flex-col justify-center gap-2">
              {pipeline.map((s) => (
                <div key={s.status} className="flex items-center gap-2 text-xs" title={`${s.labelVi}: ${s.count} (${s.percentage}%)`}>
                  <span className="w-28 truncate font-semibold text-slate-600 shrink-0">{s.labelVi}</span>
                  <div className="flex-1 h-4 bg-slate-100 rounded overflow-hidden">
                    <div className="h-full rounded" style={{ width: `${(s.count / pipelineMax) * 100}%`, background: CHART_BLUE }} />
                  </div>
                  <span className="font-bold text-slate-800 w-8 text-right">{s.count}</span>
                  <span className="text-slate-400 w-12 text-right">{s.percentage}%</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Work type distribution */}
        <div className="bg-white border border-slate-200 rounded-xl p-4 flex flex-col">
          <h3 className="text-sm font-bold text-slate-800">Phân bổ mảng việc</h3>
          <p className="text-xs text-slate-400 font-medium mb-2">Số nhiệm vụ và tổng số lượng theo mảng việc</p>
          {workTypes.length === 0 ? (
            <ChartEmpty height={230} />
          ) : (
            <div className="flex-1 flex flex-col justify-center gap-2">
              {workTypes.map((w) => (
                <div key={w.itemType} className="flex items-center gap-2 text-xs" title={`${w.labelVi}: ${w.count} nhiệm vụ · SL ${w.quantityTotal} (${w.percentage}%)`}>
                  <span className="w-28 truncate font-semibold text-slate-600 shrink-0">{w.labelVi}</span>
                  <div className="flex-1 h-4 bg-slate-100 rounded overflow-hidden">
                    <div className="h-full rounded" style={{ width: `${(w.count / workTypeMax) * 100}%`, background: CHART_BLUE }} />
                  </div>
                  <span className="font-bold text-slate-800 w-8 text-right">{w.count}</span>
                  <span className="text-slate-400 w-16 text-right">SL {fmt.formatNumber(w.quantityTotal)}</span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* Monthly trend */}
      <div className="bg-white border border-slate-200 rounded-xl p-4">
        <div className="flex items-start justify-between gap-2 mb-2 flex-wrap">
          <div>
            <h3 className="text-sm font-bold text-slate-800">Xu hướng hoàn thành theo tháng</h3>
            <p className="text-xs text-slate-400 font-medium">Tính theo ngày thăm dự kiến của chuyến</p>
          </div>
          <div className="flex items-center gap-3 text-[11px] font-semibold text-slate-500 flex-wrap">
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_BLUE }} />Tổng nhiệm vụ</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_GREEN }} />Hoàn thành</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_ORANGE }} />Quá hạn</span>
          </div>
        </div>
        {trendEmpty ? (
          <ChartEmpty height={260} />
        ) : (
          <div className="h-[260px] w-full">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <LineChart data={trend} margin={{ top: 8, right: 8, left: -18, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis dataKey="monthLabel" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#64748b' }} dy={8} />
                <YAxis allowDecimals={false} axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#64748b' }} />
                <Tooltip
                  contentStyle={{ borderRadius: 10, border: '1px solid #e2e8f0', boxShadow: '0 4px 12px rgb(0 0 0 / 0.08)', fontSize: 12 }}
                  labelStyle={{ fontWeight: 700, color: '#1e293b' }}
                />
                <Line type="monotone" dataKey="totalTasks" name="Tổng nhiệm vụ" stroke={CHART_BLUE} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
                <Line type="monotone" dataKey="completedTasks" name="Hoàn thành" stroke={CHART_GREEN} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
                <Line type="monotone" dataKey="overdueTasks" name="Quá hạn" stroke={CHART_ORANGE} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </div>
    </div>
  );
}

// ───────────────────────────── Tab: Công việc ─────────────────────────────

function TasksTab({ data, onOpen }: { data: DeptLeaderReportOverview; onOpen: () => void }) {
  const rows = data.pendingTasks;
  const proposals = data.proposalChanges;
  return (
    <div className="space-y-4">
      <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
        <div className="px-4 py-3 border-b border-slate-100 flex items-center justify-between gap-2 flex-wrap">
          <h3 className="text-sm font-bold text-slate-800">
            Công việc cần xử lý
            {data.pendingTasksTotal > rows.length && (
              <span className="text-slate-400 font-medium"> · hiển thị {rows.length}/{data.pendingTasksTotal}</span>
            )}
          </h3>
          {data.pendingTasksTotal > 0 && (
            <button onClick={onOpen} className="text-xs font-bold text-[#004c91] hover:underline cursor-pointer">
              Mở Quản lý nhiệm vụ phòng ban
            </button>
          )}
        </div>
        {rows.length === 0 ? (
          <SectionEmpty text="Không có công việc cần xử lý." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[1020px]">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-100">
                  <th className={thClass}>Ưu tiên</th>
                  <th className={thClass}>Tên nhiệm vụ</th>
                  <th className={thClass}>Đoàn / Visit</th>
                  <th className={thClass}>Mảng việc</th>
                  <th className={`${thClass} text-right`}>Số lượng</th>
                  <th className={thClass}>Deadline</th>
                  <th className={thClass}>Trạng thái</th>
                  <th className={thClass}>Người xử lý</th>
                  <th className={thClass}>Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {rows.map((r) => (
                  <tr key={r.logisticsItemId} className="hover:bg-blue-50/40 transition-colors">
                    <td className={tdClass}>
                      <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${PRIORITY_BADGES[r.priority] ?? PRIORITY_BADGES.MEDIUM}`}>
                        {PRIORITY_LABELS[r.priority] ?? r.priority}
                      </span>
                    </td>
                    <td className={`${tdClass} font-semibold text-slate-800 max-w-[220px] truncate`} title={r.itemName}>{r.itemName}</td>
                    <td className={`${tdClass} max-w-[200px] truncate`} title={`${r.requestCode} · ${r.delegationName}`}>
                      <span className="font-bold text-[#004c91]">{r.requestCode}</span> · {r.delegationName}
                    </td>
                    <td className={tdClass}>{itemTypeLabel(r.itemType)}</td>
                    <td className={`${tdClass} text-right font-semibold`}>{r.quantity}</td>
                    <td className={`${tdClass} ${r.dueAt && new Date(r.dueAt) < new Date() ? 'text-red-600 font-bold' : ''}`}>
                      {fmt.formatDate(r.dueAt)}
                    </td>
                    <td className={tdClass}>
                      <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${taskStatusBadge(r.status)}`}>
                        {taskStatusLabel(r.status)}
                      </span>
                    </td>
                    <td className={tdClass}>{r.assignedToName ?? <span className="text-slate-400">Chưa gán</span>}</td>
                    <td className={tdClass}>
                      <button onClick={onOpen} className="text-xs font-bold text-[#004c91] hover:underline cursor-pointer" title={`Đã chờ ${fmt.formatWaitingHours(r.waitingHours)}`}>
                        {r.actionLabel}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
        <div className="px-4 py-3 border-b border-slate-100">
          <h3 className="text-sm font-bold text-slate-800">Đề xuất thay đổi</h3>
          <p className="text-xs text-slate-400 font-medium">Đề xuất thay đổi số lượng/thời gian đang chờ host phản hồi</p>
        </div>
        {proposals.length === 0 ? (
          <SectionEmpty text="Không có đề xuất thay đổi nào đang chờ." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[860px]">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-100">
                  <th className={thClass}>Nhiệm vụ</th>
                  <th className={thClass}>Người đề xuất</th>
                  <th className={`${thClass} text-right`}>SL đề xuất</th>
                  <th className={thClass}>Thời gian đề xuất</th>
                  <th className={thClass}>Ghi chú</th>
                  <th className={thClass}>Trạng thái</th>
                  <th className={thClass}>Ngày tạo</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {proposals.map((p) => (
                  <tr key={p.logisticsItemId} className="hover:bg-blue-50/40 transition-colors">
                    <td className={`${tdClass} font-semibold text-slate-800 max-w-[220px] truncate`} title={p.itemName}>{p.itemName}</td>
                    <td className={tdClass}>{p.proposedByName}</td>
                    <td className={`${tdClass} text-right font-semibold`}>{p.proposedQuantity ?? '—'}</td>
                    <td className={tdClass}>{fmt.formatDate(p.proposedUsageStartAt)} – {fmt.formatDate(p.proposedUsageEndAt)}</td>
                    <td className={`${tdClass} max-w-[240px] truncate`} title={p.proposalNote ?? undefined}>
                      {p.proposalNote || <span className="text-slate-400">—</span>}
                    </td>
                    <td className={tdClass}>
                      <span className="inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 bg-violet-50 text-violet-700 border-violet-200">
                        {p.proposalStatus}
                      </span>
                    </td>
                    <td className={tdClass}>{fmt.formatDate(p.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

// ───────────────────────────── Tab: Nhân sự ─────────────────────────────

function StaffTab({ data }: { data: DeptLeaderReportOverview }) {
  const rows = data.staffPerformance;
  return (
    <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
      <div className="px-4 py-3 border-b border-slate-100">
        <h3 className="text-sm font-bold text-slate-800">Hiệu suất nhân sự phòng ban</h3>
        <p className="text-xs text-slate-400 font-medium">Theo lượt phân công và nhiệm vụ trong kỳ báo cáo</p>
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Chưa có dữ liệu nhân sự trong kỳ này." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[980px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Nhân sự</th>
                <th className={`${thClass} text-right`}>Được giao</th>
                <th className={`${thClass} text-right`}>Chờ phản hồi</th>
                <th className={`${thClass} text-right`}>Đã nhận</th>
                <th className={`${thClass} text-right`}>Đang xử lý</th>
                <th className={`${thClass} text-right`}>Hoàn thành</th>
                <th className={`${thClass} text-right`}>Từ chối</th>
                <th className={`${thClass} text-right`}>Quá hạn</th>
                <th className={thClass}>Tỷ lệ hoàn thành</th>
                <th className={`${thClass} text-right`}>Phản hồi TB</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((s) => (
                <tr key={s.userId} className="hover:bg-blue-50/40 transition-colors">
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[200px] truncate`} title={s.fullName}>{s.fullName}</td>
                  <td className={`${tdClass} text-right font-bold text-[#004c91]`}>{s.assignedCount}</td>
                  <td className={`${tdClass} text-right ${s.pendingResponseCount > 0 ? 'text-amber-600 font-semibold' : 'text-slate-400'}`}>{s.pendingResponseCount}</td>
                  <td className={`${tdClass} text-right`}>{s.acceptedCount}</td>
                  <td className={`${tdClass} text-right`}>{s.inProgressCount}</td>
                  <td className={`${tdClass} text-right text-emerald-600 font-semibold`}>{s.completedCount}</td>
                  <td className={`${tdClass} text-right ${s.declinedCount > 0 ? 'text-red-600 font-semibold' : 'text-slate-400'}`}>{s.declinedCount}</td>
                  <td className={`${tdClass} text-right ${s.overdueCount > 0 ? 'text-red-600 font-bold' : 'text-slate-400'}`}>{s.overdueCount}</td>
                  <td className={tdClass}>
                    <div className="flex items-center gap-2" title={`${s.completionRate}%`}>
                      <div className="w-24 h-2 bg-slate-100 rounded overflow-hidden">
                        <div className="h-full rounded" style={{ width: `${Math.min(100, s.completionRate)}%`, background: CHART_GREEN }} />
                      </div>
                      <span className="text-xs font-bold text-slate-700 w-11">{fmt.formatPercent(s.completionRate)}</span>
                    </div>
                  </td>
                  <td className={`${tdClass} text-right font-semibold`}>
                    {s.averageResponseHours != null ? fmt.formatWaitingHours(s.averageResponseHours) : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ───────────────────────────── Tab: Bàn giao ─────────────────────────────

function HandoverBadge({ label }: { label: string }) {
  const cls = label === 'Đủ chữ ký'
    ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
    : label === 'Có hư hỏng' || label === 'Thiếu/mất'
      ? 'bg-red-50 text-red-600 border-red-200'
      : 'bg-amber-50 text-amber-700 border-amber-200';
  return <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${cls}`}>{label}</span>;
}

function SignBadge({ signed }: { signed: boolean }) {
  return (
    <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${
      signed ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-amber-50 text-amber-700 border-amber-200'
    }`}>
      {signed ? 'Đã ký' : 'Chưa ký'}
    </span>
  );
}

function HandoverTab({ data }: { data: DeptLeaderReportOverview }) {
  const rows = data.handoverSummary;
  return (
    <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
      <div className="px-4 py-3 border-b border-slate-100">
        <h3 className="text-sm font-bold text-slate-800">
          Bàn giao / ký mượn / ký trả
          {data.handoverTotal > rows.length && (
            <span className="text-slate-400 font-medium"> · hiển thị {rows.length}/{data.handoverTotal}</span>
          )}
        </h3>
        <p className="text-xs text-slate-400 font-medium">Biên bản thiếu chữ ký được xếp lên đầu</p>
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Không có biên bản bàn giao trong kỳ này." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[980px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Item</th>
                <th className={thClass}>Visit</th>
                <th className={thClass}>Loại bàn giao</th>
                <th className={thClass}>Bên mượn/trả ký</th>
                <th className={thClass}>Bên giao/nhận ký</th>
                <th className={thClass}>Tình trạng đồ</th>
                <th className={thClass}>Ghi chú</th>
                <th className={thClass}>File</th>
                <th className={thClass}>Trạng thái</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((h, index) => (
                <tr key={`${h.logisticsItemId}-${h.handoverType}-${index}`} className="hover:bg-blue-50/40 transition-colors">
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[200px] truncate`} title={h.itemName}>{h.itemName}</td>
                  <td className={`${tdClass} max-w-[180px] truncate`} title={`${h.visitCode} · ${h.delegationName}`}>
                    <span className="font-bold text-[#004c91]">{h.visitCode}</span> · {h.delegationName}
                  </td>
                  <td className={tdClass}>
                    <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${
                      h.handoverType === 'BORROW' ? 'bg-blue-50 text-blue-700 border-blue-200' : 'bg-violet-50 text-violet-700 border-violet-200'
                    }`}>
                      {h.handoverType === 'BORROW' ? 'Ký mượn' : 'Ký trả'}
                    </span>
                  </td>
                  <td className={tdClass}><SignBadge signed={h.borrowerSigned} /></td>
                  <td className={tdClass}><SignBadge signed={h.providerSigned} /></td>
                  <td className={tdClass}>
                    {h.itemCondition === 'GOOD' ? 'Tốt'
                      : h.itemCondition === 'DAMAGED' ? <span className="text-red-600 font-semibold">Hư hỏng</span>
                        : h.itemCondition === 'MISSING' ? <span className="text-red-600 font-semibold">Thiếu/mất</span>
                          : h.itemCondition ?? '—'}
                  </td>
                  <td className={`${tdClass} max-w-[200px] truncate`} title={h.conditionNote ?? undefined}>
                    {h.conditionNote || <span className="text-slate-400">—</span>}
                  </td>
                  <td className={tdClass}>
                    {h.attachmentFileId != null
                      ? <span className="inline-flex items-center gap-1 text-xs font-semibold text-slate-600"><Paperclip className="w-3.5 h-3.5" /> Có</span>
                      : <span className="text-slate-400">—</span>}
                  </td>
                  <td className={tdClass}><HandoverBadge label={h.statusLabel} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ───────────────────────────── Tab: Phát sinh & Feedback ─────────────────────────────

function IncidentsTab({ data }: { data: DeptLeaderReportOverview }) {
  const incidents = data.incidentSummary;
  const fb = data.feedbackSummary;
  const [fbTab, setFbTab] = useState<'low' | 'recent'>('low');
  const fbRows = fbTab === 'low' ? fb.lowRatedItems : fb.recentFeedbacks;

  return (
    <div className="space-y-4">
      {/* Phát sinh sau bàn giao (đổi tên từ "Thanh toán khắc phục") */}
      <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
        <div className="px-4 py-3 border-b border-slate-100">
          <h3 className="text-sm font-bold text-slate-800">Phát sinh sau bàn giao</h3>
          <p className="text-xs text-slate-400 font-medium">Hư hỏng, thiếu/mất và biên bản chưa hoàn tất theo mảng việc</p>
        </div>
        {incidents.length === 0 ? (
          <SectionEmpty text="Không có phát sinh sau bàn giao trong kỳ này." />
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[860px]">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-100">
                  <th className={thClass}>Mảng việc</th>
                  <th className={`${thClass} text-right`}>Tổng số lượng</th>
                  <th className={`${thClass} text-right`}>Hư hỏng</th>
                  <th className={`${thClass} text-right`}>Thiếu/mất</th>
                  <th className={`${thClass} text-right`}>Cần xử lý</th>
                  <th className={thClass}>Ghi chú mới nhất</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {incidents.map((i) => (
                  <tr key={i.itemType} className="hover:bg-blue-50/40 transition-colors">
                    <td className={`${tdClass} font-semibold text-slate-800`}>{i.itemTypeLabelVi}</td>
                    <td className={`${tdClass} text-right font-semibold`}>{fmt.formatNumber(i.totalQuantity)}</td>
                    <td className={`${tdClass} text-right ${i.damagedCount > 0 ? 'text-red-600 font-bold' : 'text-slate-400'}`}>{i.damagedCount}</td>
                    <td className={`${tdClass} text-right ${i.missingCount > 0 ? 'text-red-600 font-bold' : 'text-slate-400'}`}>{i.missingCount}</td>
                    <td className={`${tdClass} text-right ${i.needActionCount > 0 ? 'text-amber-600 font-bold' : 'text-slate-400'}`}>{i.needActionCount}</td>
                    <td className={`${tdClass} max-w-[320px] truncate`} title={i.latestNote ?? undefined}>
                      {i.latestNote || <span className="text-slate-400">Không có ghi chú phát sinh</span>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Feedback theo mảng việc */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
          <div className="px-4 py-3 border-b border-slate-100">
            <h3 className="text-sm font-bold text-slate-800">Feedback theo mảng việc</h3>
            <p className="text-xs text-slate-400 font-medium">
              Điểm TB <span className="font-black text-slate-700">{fmt.formatRating(fb.averageRating)}</span>/5
              {' '}· {fmt.formatNumber(fb.totalFeedbacks)} feedback trong kỳ
            </p>
          </div>
          {fb.feedbackByItemType.length === 0 ? (
            <SectionEmpty text="Chưa có feedback trong kỳ này." />
          ) : (
            <div className="p-4 space-y-2">
              {fb.feedbackByItemType.map((t) => (
                <div key={t.itemType} className="flex items-center gap-2 text-xs" title={`${t.labelVi}: ${t.averageRating}/5 (${t.feedbackCount} feedback)`}>
                  <span className="w-28 truncate font-semibold text-slate-600 shrink-0">{t.labelVi}</span>
                  <div className="flex-1 h-4 bg-slate-100 rounded overflow-hidden">
                    <div
                      className="h-full rounded"
                      style={{ width: `${(t.averageRating / 5) * 100}%`, background: t.averageRating < 3 ? CHART_ORANGE : CHART_BLUE }}
                    />
                  </div>
                  <span className={`font-bold w-10 text-right ${t.averageRating < 3 ? 'text-red-600' : 'text-slate-800'}`}>{t.averageRating}/5</span>
                  <span className="text-slate-400 w-8 text-right">({t.feedbackCount})</span>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Feedback thấp / gần đây */}
        <div className="lg:col-span-2 bg-white border border-slate-200 rounded-xl overflow-hidden">
          <div className="px-4 pt-3 border-b border-slate-100 flex items-center gap-1">
            {([
              { key: 'low' as const, label: `Feedback thấp cần chú ý (${fb.lowFeedbackCount})` },
              { key: 'recent' as const, label: 'Feedback gần đây' },
            ]).map((t) => (
              <button
                key={t.key}
                onClick={() => setFbTab(t.key)}
                className={`px-4 py-2 text-sm font-bold rounded-t-lg border-b-2 transition-colors cursor-pointer ${
                  fbTab === t.key ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-slate-400 hover:text-slate-600'
                }`}
              >
                {t.label}
              </button>
            ))}
          </div>
          {fbRows.length === 0 ? (
            <SectionEmpty text={fbTab === 'low' ? 'Không có feedback thấp trong kỳ này.' : 'Chưa có feedback trong kỳ này.'} />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse min-w-[620px]">
                <thead>
                  <tr className="bg-slate-50 border-b border-slate-100">
                    <th className={thClass}>Đoàn</th>
                    <th className={thClass}>Đối tượng</th>
                    <th className={`${thClass} text-right`}>Rating</th>
                    <th className={thClass}>Nội dung</th>
                    <th className={thClass}>Ngày gửi</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {fbRows.map((e: DeptLeaderFeedbackEntry) => (
                    <tr key={e.feedbackId} className="hover:bg-blue-50/40 transition-colors">
                      <td className={`${tdClass} font-semibold text-slate-800 max-w-[180px] truncate`} title={e.delegationName}>{e.delegationName}</td>
                      <td className={`${tdClass} max-w-[160px] truncate`} title={e.itemName ?? undefined}>{e.itemName ?? '—'}</td>
                      <td className={`${tdClass} text-right font-black ${e.rating <= 2 ? 'text-red-600' : e.rating >= 4 ? 'text-emerald-600' : 'text-slate-700'}`}>
                        {e.rating}/5
                      </td>
                      <td className={`${tdClass} max-w-[240px] truncate`} title={e.comment ?? undefined}>
                        {e.comment || <span className="text-slate-400">Không có nhận xét</span>}
                      </td>
                      <td className={tdClass}>{fmt.formatDate(e.submittedAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

// ───────────────────────────── Tab: Hóa đơn ─────────────────────────────

interface InvoiceRowState {
  selected: boolean;
  unitPrice: string;
  unit: string;
  note: string;
}

function InvoiceTab({
  visits, items, visitsLoading, itemsLoading, exportLoading,
  onLoadVisits, onLoadItems, onExport,
}: {
  visits: { visitInstanceId: number; requestCode: string; delegationName: string; plannedStartAt: string | null; plannedEndAt: string | null }[];
  items: DeptLeaderInvoiceItem[];
  visitsLoading: boolean;
  itemsLoading: boolean;
  exportLoading: boolean;
  onLoadVisits: () => Promise<void>;
  onLoadItems: (visitInstanceId: number) => Promise<void>;
  onExport: (payload: {
    visitInstanceId: number;
    invoiceTitle: string;
    invoiceNote: string;
    items: { logisticsItemId: number; itemName: string; itemType: string; quantity: number; unit: string | null; unitPrice: number; note: string }[];
  }) => Promise<void>;
}) {
  const [selectedVisit, setSelectedVisit] = useState<number | ''>('');
  const [rowState, setRowState] = useState<Record<number, InvoiceRowState>>({});
  const [invoiceNote, setInvoiceNote] = useState('');
  const loadedRef = useRef(false);

  // Load danh sách visit một lần khi mở tab.
  useEffect(() => {
    if (loadedRef.current) return;
    loadedRef.current = true;
    onLoadVisits().catch(() => toast.error('Không thể tải danh sách chuyến thăm.'));
  }, [onLoadVisits]);

  // Reset state dòng khi items đổi (chọn visit khác).
  useEffect(() => {
    const next: Record<number, InvoiceRowState> = {};
    items.forEach((item) => {
      next[item.logisticsItemId] = { selected: true, unitPrice: '', unit: '', note: '' };
    });
    setRowState(next);
  }, [items]);

  const handleSelectVisit = (value: string) => {
    const id = value ? Number(value) : '';
    setSelectedVisit(id);
    if (id !== '') {
      onLoadItems(id).catch(() => toast.error('Không thể tải danh sách hạng mục.'));
    }
  };

  const updateRow = (id: number, patch: Partial<InvoiceRowState>) => {
    setRowState((prev) => ({ ...prev, [id]: { ...prev[id], ...patch } }));
  };

  const selectedRows = items.filter((item) => rowState[item.logisticsItemId]?.selected);
  const lineAmount = (item: DeptLeaderInvoiceItem) => {
    const price = Number(rowState[item.logisticsItemId]?.unitPrice ?? '');
    return Number.isFinite(price) && price >= 0 ? item.quantity * price : 0;
  };
  const total = selectedRows.reduce((sum, item) => sum + lineAmount(item), 0);
  const hasInvalidPrice = selectedRows.some((item) => {
    const raw = rowState[item.logisticsItemId]?.unitPrice ?? '';
    const price = Number(raw);
    return raw.trim() === '' || !Number.isFinite(price) || price < 0;
  });
  const canExport = selectedVisit !== '' && selectedRows.length > 0 && !hasInvalidPrice && !exportLoading;

  const handleExport = async () => {
    if (!canExport || selectedVisit === '') return;
    try {
      await onExport({
        visitInstanceId: selectedVisit,
        invoiceTitle: 'Hóa đơn chuẩn bị hậu cần',
        invoiceNote,
        items: selectedRows.map((item) => ({
          logisticsItemId: item.logisticsItemId,
          itemName: item.itemName,
          itemType: item.itemType,
          quantity: item.quantity,
          unit: rowState[item.logisticsItemId]?.unit.trim() || null,
          unitPrice: Number(rowState[item.logisticsItemId]?.unitPrice ?? 0),
          note: rowState[item.logisticsItemId]?.note.trim() ?? '',
        })),
      });
      toast.success('Đã xuất hóa đơn PDF thành công.');
    } catch {
      toast.error('Không thể xuất hóa đơn PDF. Vui lòng kiểm tra đơn giá và thử lại.');
    }
  };

  return (
    <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
      <div className="px-4 py-3 border-b border-slate-100 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-bold text-slate-800">Xuất hóa đơn chuẩn bị hậu cần</h3>
          <p className="text-xs text-slate-400 font-medium">
            Số lượng lấy theo yêu cầu của host trong hệ thống · Đơn giá do Department Leader nhập · Chỉ xuất PDF, không lưu hóa đơn
          </p>
        </div>
        <select
          value={selectedVisit === '' ? '' : String(selectedVisit)}
          onChange={(e) => handleSelectVisit(e.target.value)}
          className={`${selectClass} min-w-[280px]`}
          aria-label="Chọn chuyến thăm"
          disabled={visitsLoading}
        >
          <option value="">{visitsLoading ? 'Đang tải chuyến thăm…' : '— Chọn chuyến thăm / đoàn —'}</option>
          {visits.map((v) => (
            <option key={v.visitInstanceId} value={String(v.visitInstanceId)}>
              {v.requestCode} · {v.delegationName} ({fmt.formatDate(v.plannedStartAt)})
            </option>
          ))}
        </select>
      </div>

      {selectedVisit === '' ? (
        <SectionEmpty text="Chọn chuyến thăm để xem danh sách hạng mục host yêu cầu phòng ban chuẩn bị." />
      ) : itemsLoading ? (
        <div className="flex items-center justify-center py-10 text-slate-400 gap-2">
          <Loader2 className="w-5 h-5 animate-spin" />
          <span className="text-sm font-medium">Đang tải hạng mục…</span>
        </div>
      ) : items.length === 0 ? (
        <SectionEmpty text="Không có item để xuất hóa đơn cho chuyến này." />
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse min-w-[1020px]">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-100">
                  <th className={`${thClass} w-10`}></th>
                  <th className={`${thClass} w-10`}>STT</th>
                  <th className={thClass}>Tên hạng mục</th>
                  <th className={thClass}>Loại</th>
                  <th className={`${thClass} text-right`}>Số lượng</th>
                  <th className={thClass}>Đơn vị</th>
                  <th className={`${thClass} text-right`}>Đơn giá (đ)</th>
                  <th className={`${thClass} text-right`}>Thành tiền</th>
                  <th className={thClass}>Ghi chú</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {items.map((item, index) => {
                  const state = rowState[item.logisticsItemId] ?? { selected: true, unitPrice: '', unit: '', note: '' };
                  const raw = state.unitPrice;
                  const price = Number(raw);
                  const invalid = state.selected && (raw.trim() === '' || !Number.isFinite(price) || price < 0);
                  return (
                    <tr key={item.logisticsItemId} className={`transition-colors ${state.selected ? 'hover:bg-blue-50/40' : 'opacity-50 bg-slate-50/40'}`}>
                      <td className={tdClass}>
                        <input
                          type="checkbox"
                          checked={state.selected}
                          onChange={(e) => updateRow(item.logisticsItemId, { selected: e.target.checked })}
                          className="rounded border-slate-300 text-[#004c91] focus:ring-[#004c91]/30 cursor-pointer"
                          aria-label={`Chọn ${item.itemName}`}
                        />
                      </td>
                      <td className={`${tdClass} text-slate-400 font-semibold`}>{index + 1}</td>
                      <td className={`${tdClass} font-semibold text-slate-800 max-w-[220px] truncate`} title={item.itemName}>{item.itemName}</td>
                      <td className={tdClass}>{item.itemTypeLabelVi}</td>
                      <td className={`${tdClass} text-right font-bold`}>{fmt.formatNumber(item.quantity)}</td>
                      <td className={tdClass}>
                        <input
                          type="text"
                          value={state.unit}
                          onChange={(e) => updateRow(item.logisticsItemId, { unit: e.target.value })}
                          disabled={!state.selected}
                          placeholder="cái, chai…"
                          className="w-20 bg-white border border-slate-200 rounded-lg px-2 py-1.5 text-sm outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] disabled:bg-slate-50"
                        />
                      </td>
                      <td className={`${tdClass} text-right`}>
                        <input
                          type="number"
                          min={0}
                          step={1000}
                          value={state.unitPrice}
                          onChange={(e) => updateRow(item.logisticsItemId, { unitPrice: e.target.value })}
                          disabled={!state.selected}
                          placeholder="0"
                          className={`w-28 text-right bg-white border rounded-lg px-2 py-1.5 text-sm font-semibold outline-none focus:ring-2 focus:ring-[#004c91]/20 disabled:bg-slate-50 ${
                            invalid ? 'border-red-300 focus:border-red-400' : 'border-slate-200 focus:border-[#004c91]'
                          }`}
                          aria-label={`Đơn giá ${item.itemName}`}
                        />
                      </td>
                      <td className={`${tdClass} text-right font-bold text-slate-800`}>
                        {state.selected && !invalid ? formatVnd(lineAmount(item)) : '—'}
                      </td>
                      <td className={tdClass}>
                        <input
                          type="text"
                          value={state.note}
                          onChange={(e) => updateRow(item.logisticsItemId, { note: e.target.value })}
                          disabled={!state.selected}
                          placeholder="Ghi chú…"
                          className="w-40 bg-white border border-slate-200 rounded-lg px-2 py-1.5 text-sm outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] disabled:bg-slate-50"
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="px-4 py-3 border-t border-slate-100 flex flex-col md:flex-row md:items-center gap-3">
            <input
              type="text"
              value={invoiceNote}
              onChange={(e) => setInvoiceNote(e.target.value)}
              placeholder="Ghi chú hóa đơn (tùy chọn)…"
              className="flex-1 bg-white border border-slate-200 rounded-lg px-3 py-2 text-sm outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91]"
            />
            <div className="flex items-center gap-4 justify-between md:justify-end">
              <div className="text-right">
                <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wide">Tổng tiền ({selectedRows.length} hạng mục)</p>
                <p className="text-xl font-black text-[#f37021]">{formatVnd(total)}</p>
              </div>
              <button
                onClick={handleExport}
                disabled={!canExport}
                title={hasInvalidPrice ? 'Nhập đơn giá (≥ 0) cho các hạng mục đã chọn' : undefined}
                className="flex items-center gap-2 px-5 py-2.5 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:bg-[#00386b] transition-colors shadow-sm disabled:opacity-50 disabled:cursor-not-allowed cursor-pointer"
              >
                {exportLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <ReceiptText className="w-4 h-4" />}
                Xuất hóa đơn PDF
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
