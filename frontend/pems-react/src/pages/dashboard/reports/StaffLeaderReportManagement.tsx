/**
 * Trang StaffLeaderReportManagement — Báo cáo vận hành campus tại /dashboard/reports
 * (role STAFF · LEADER). Campus operation dashboard: KPI strip compact, action bar,
 * chart xu hướng + phân bổ trạng thái, host workload, đơn cần xử lý, close readiness,
 * logistics theo phòng ban, feedback thấp/tốt. Toàn bộ dữ liệu lấy từ
 * GET /reports/staff-leader-overview — scope đúng campus của Staff Leader, không mock.
 */

import React, { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  AlertTriangle, ArrowRight, CheckCircle2, ChevronDown, Download, FileSpreadsheet, FileText,
  Info, Loader2, RefreshCw, ShieldAlert, X,
} from 'lucide-react';
import {
  CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { useStaffLeaderReport } from '../../../features/reports/hooks/useReports';
import { reportsAdapter as fmt } from '../../../features/reports/adapters/reportsAdapter';
import type {
  StaffLeaderExportFormat, StaffLeaderFeedbackEntry, StaffLeaderReportOverview, StaffLeaderReportSection,
} from '../../../features/reports/types/staffLeaderReports.types';

// Palette chart đã validate CVD/contrast (scripts/validate_palette.js — ALL PASS light & dark).
const CHART_BLUE = '#1e6fc0';
const CHART_ORANGE = '#d95f18';
const CHART_GREEN = '#0a8a44';
const CHART_VIOLET = '#7a5cc4';

const SECTION_OPTIONS: { value: StaffLeaderReportSection; label: string }[] = [
  { value: 'EXECUTIVE_SUMMARY', label: 'Executive Summary' },
  { value: 'LIFECYCLE_SUMMARY', label: 'Lifecycle & Trend' },
  { value: 'HOST_WORKLOAD', label: 'Host Workload' },
  { value: 'PENDING_ACTIONS', label: 'Pending Actions' },
  { value: 'LOGISTICS_SUMMARY', label: 'Logistics Summary' },
  { value: 'CLOSE_READINESS', label: 'Close Readiness' },
  { value: 'FEEDBACK_SUMMARY', label: 'Feedback Summary' },
];

const PRESET_LABELS: Record<string, string> = {
  THIS_MONTH: 'Tháng này',
  THIS_QUARTER: 'Quý này',
  THIS_YEAR: 'Năm nay',
  CUSTOM: 'Tùy chỉnh',
};

const SEVERITY_STYLES: Record<string, { dot: string; text: string }> = {
  DANGER: { dot: 'bg-red-500', text: 'text-red-600' },
  WARNING: { dot: 'bg-amber-500', text: 'text-amber-600' },
  INFO: { dot: 'bg-sky-500', text: 'text-sky-600' },
  SUCCESS: { dot: 'bg-emerald-500', text: 'text-emerald-600' },
};

const VISIT_TYPE_LABELS: Record<string, string> = {
  CAMPUS_TOUR: 'Tham quan',
  MEETING: 'Họp / Gặp gỡ',
  WORKSHOP: 'Workshop',
  SIGNING_CEREMONY: 'Lễ ký kết',
  EXCHANGE: 'Giao lưu',
};
const visitTypeLabel = (t: string) => VISIT_TYPE_LABELS[t] ?? t;

const selectClass =
  'bg-white border border-slate-200 rounded-lg px-2.5 py-2 text-sm font-medium text-slate-700 outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] cursor-pointer';
const thClass = 'px-3 py-2.5 text-[11px] font-bold text-slate-400 uppercase tracking-wide whitespace-nowrap';
const tdClass = 'px-3 py-2.5 text-sm text-slate-600 whitespace-nowrap';

export function StaffLeaderReportManagement() {
  const navigate = useNavigate();
  const {
    filters, setFilters, data, loading, error, refetch,
    applyFilters, resetFilters, exportReport, exportLoading,
  } = useStaffLeaderReport();

  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [exportConfirm, setExportConfirm] = useState<StaffLeaderExportFormat | null>(null);
  const [exportSections, setExportSections] = useState<StaffLeaderReportSection[]>(SECTION_OPTIONS.map((s) => s.value));
  const exportMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const close = (e: MouseEvent) => {
      if (exportMenuRef.current && !exportMenuRef.current.contains(e.target as Node)) setExportMenuOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  // Giữ danh sách host/phòng ban đầy đủ từ lần load không lọc để option không biến mất khi lọc.
  const [hostOptions, setHostOptions] = useState<{ id: number; name: string }[]>([]);
  const [deptOptions, setDeptOptions] = useState<{ id: number; name: string }[]>([]);
  useEffect(() => {
    if (!data) return;
    if (data.filterSummary.hostUserId === 'ALL') {
      const hosts = new Map<number, string>();
      data.hostWorkload.forEach((h) => hosts.set(h.hostUserId, h.hostName));
      data.feedbackSummary.ratingByHost.forEach((h) => hosts.set(h.hostUserId, h.hostName));
      setHostOptions([...hosts.entries()].map(([id, name]) => ({ id, name })));
    }
    if (data.filterSummary.departmentId === 'ALL') {
      setDeptOptions(data.logisticsByDepartment
        .filter((d) => d.departmentId > 0)
        .map((d) => ({ id: d.departmentId, name: d.departmentName })));
    }
  }, [data]);

  const scrollTo = (sectionId: string) => {
    document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

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
        <h2 className="text-xl font-bold text-slate-800 mb-1">Bạn không có quyền xem báo cáo vận hành campus</h2>
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
          <span className="text-[#004c91] font-bold">Báo cáo campus</span>
        </div>
        <div className="flex flex-col md:flex-row md:items-end justify-between gap-3">
          <div>
            <div className="flex items-center gap-3 flex-wrap">
              <h1 className="text-2xl font-black text-[#004c91] tracking-tight">Báo cáo vận hành campus</h1>
              <span className="text-[11px] font-bold uppercase tracking-wide text-[#004c91] bg-blue-50 border border-blue-100 rounded-full px-2.5 py-1">
                Staff Leader{data ? ` · ${data.filterSummary.campusName}` : ''}
              </span>
            </div>
            <p className="text-sm font-medium text-slate-500 mt-0.5">
              Tổng quan phê duyệt, phân công host, logistics và chất lượng tiếp đón
            </p>
          </div>
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
                  { format: 'EXCEL' as StaffLeaderExportFormat, label: 'Excel (.xlsx)', icon: FileSpreadsheet },
                  { format: 'PDF' as StaffLeaderExportFormat, label: 'PDF (.pdf)', icon: FileText },
                  { format: 'CSV' as StaffLeaderExportFormat, label: 'CSV (.csv)', icon: FileText },
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
          value={filters.visitStatus}
          onChange={(e) => setFilters({ ...filters, visitStatus: e.target.value })}
          className={selectClass}
          aria-label="Trạng thái chuyến"
        >
          <option value="ALL">Chuyến: Tất cả</option>
          <option value="WAITING_REQUEST_APPROVAL">Chờ duyệt</option>
          <option value="WAITING_HOST_ASSIGNMENT">Chờ gán host</option>
          <option value="ASSIGNED">Đã gán host</option>
          <option value="BEFORE_VISIT">Trước chuyến</option>
          <option value="DURING_VISIT">Đang diễn ra</option>
          <option value="AFTER_VISIT">Sau chuyến</option>
          <option value="CLOSED">Đã đóng</option>
          <option value="CANCELLED">Đã hủy</option>
        </select>

        <select
          value={filters.hostUserId}
          onChange={(e) => setFilters({ ...filters, hostUserId: e.target.value })}
          className={selectClass}
          aria-label="Host"
        >
          <option value="ALL">Host: Tất cả</option>
          {hostOptions.map((h) => (
            <option key={h.id} value={String(h.id)}>{h.name}</option>
          ))}
        </select>

        <select
          value={filters.departmentId}
          onChange={(e) => setFilters({ ...filters, departmentId: e.target.value })}
          className={selectClass}
          aria-label="Phòng ban logistics"
        >
          <option value="ALL">Phòng ban: Tất cả</option>
          {deptOptions.map((d) => (
            <option key={d.id} value={String(d.id)}>{d.name}</option>
          ))}
        </select>

        <select
          value={filters.logisticsStatus}
          onChange={(e) => setFilters({ ...filters, logisticsStatus: e.target.value })}
          className={selectClass}
          aria-label="Trạng thái logistics"
        >
          <option value="ALL">Logistics: Tất cả</option>
          <option value="REQUESTED">Chờ phản hồi</option>
          <option value="ACCEPTED">Đã nhận</option>
          <option value="IN_PROGRESS">Đang xử lý</option>
          <option value="DONE">Hoàn thành</option>
          <option value="REJECTED">Từ chối</option>
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
          <ActionBar data={data} onView={scrollTo} />
          <ChartsRow data={data} />
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <HostWorkloadCard data={data} />
            <LogisticsCard data={data} />
          </div>
          <PendingActionsTable data={data} onOpen={() => navigate('/dashboard/visit')} />
          <CloseReadinessTable
            data={data}
            onOpen={(visitInstanceId) => navigate(`/dashboard/visit/process-summary/${visitInstanceId}`)}
          />
          <FeedbackSection
            data={data}
            onOpen={(visitInstanceId) => navigate(`/dashboard/visit/process-summary/${visitInstanceId}`)}
          />
          <p className="text-[11px] text-slate-400 text-right">
            Số liệu theo kỳ tính bằng ngày thăm dự kiến · Khối tác vụ (chờ duyệt, gán host, đóng hồ sơ)
            tính theo trạng thái hiện tại · Cập nhật {fmt.formatDateTime(data.generatedAt)}
          </p>
        </>
      )}

      {/* ── Modal xác nhận export ── */}
      {exportConfirm && data && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100">
              <h3 className="text-base font-bold text-slate-800">Xuất báo cáo vận hành campus</h3>
              <button onClick={() => setExportConfirm(null)} className="text-slate-400 hover:text-slate-600 cursor-pointer" aria-label="Đóng">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="px-5 py-4 space-y-3 text-sm">
              <div className="grid grid-cols-[110px_1fr] gap-y-1.5 text-slate-600">
                <span className="font-semibold text-slate-400">Định dạng</span>
                <span className="font-bold text-slate-800">{exportConfirm}</span>
                <span className="font-semibold text-slate-400">Campus</span>
                <span>{data.filterSummary.campusName}</span>
                <span className="font-semibold text-slate-400">Thời gian</span>
                <span>{PRESET_LABELS[data.filterSummary.preset]} ({fmt.formatDate(data.filterSummary.fromDate)} – {fmt.formatDate(data.filterSummary.toDate)})</span>
                <span className="font-semibold text-slate-400">Bộ lọc</span>
                <span>
                  Chuyến: {data.filterSummary.visitStatus === 'ALL' ? 'Tất cả' : fmt.instanceStatusLabel(data.filterSummary.visitStatus)}
                  {' '}· Host: {data.filterSummary.hostUserId === 'ALL' ? 'Tất cả' : data.filterSummary.hostName ?? data.filterSummary.hostUserId}
                  {' '}· Phòng ban: {data.filterSummary.departmentId === 'ALL' ? 'Tất cả' : data.filterSummary.departmentName ?? data.filterSummary.departmentId}
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
      <div className="bg-white border border-slate-200 rounded-xl grid grid-cols-2 sm:grid-cols-4 xl:grid-cols-8 gap-px overflow-hidden">
        {Array.from({ length: 8 }).map((_, i) => (
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

// ───────────────────────────── KPI strip ─────────────────────────────

function KpiStrip({ data }: { data: StaffLeaderReportOverview }) {
  const k = data.kpis;
  const items: { label: string; value: string; sub?: string; tone?: 'warn' | 'danger' | 'good'; title?: string }[] = [
    { label: 'Chờ duyệt', value: fmt.formatNumber(k.pendingSingleCampusApproval), sub: 'đơn single-campus', tone: k.pendingSingleCampusApproval > 0 ? 'warn' : undefined, title: 'Đơn SINGLE_CAMPUS đang chờ duyệt (trạng thái hiện tại)' },
    { label: 'Chờ gán host', value: fmt.formatNumber(k.waitingHostAssignment), sub: 'chuyến chưa có host', tone: k.waitingHostAssignment > 0 ? 'warn' : undefined },
    { label: 'Đang chuẩn bị', value: fmt.formatNumber(k.assignedVisits + k.beforeVisit), sub: 'đã gán + trước chuyến' },
    { label: 'Đang diễn ra', value: fmt.formatNumber(k.duringVisit), sub: 'chuyến đang tiếp' },
    { label: 'Sau tiếp khách', value: fmt.formatNumber(k.afterVisit), sub: 'chờ hoàn tất hồ sơ' },
    { label: 'Chưa đóng/quá hạn', value: fmt.formatNumber(k.overdueOrNotClosed), sub: 'qua ngày kết thúc', tone: k.overdueOrNotClosed > 0 ? 'danger' : undefined },
    { label: 'Feedback TB', value: k.averageFeedbackRating != null ? `${fmt.formatRating(k.averageFeedbackRating)}/5` : '—', sub: 'trong kỳ báo cáo', tone: 'good' },
    { label: 'Tổng khách', value: fmt.formatNumber(k.totalGuests), sub: 'lượt khách trong kỳ' },
  ];

  return (
    <div className="bg-slate-200 border border-slate-200 rounded-xl grid grid-cols-2 sm:grid-cols-4 xl:grid-cols-8 gap-px overflow-hidden">
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

// ───────────────────────────── Action bar ─────────────────────────────

function ActionBar({ data, onView }: { data: StaffLeaderReportOverview; onView: (section: string) => void }) {
  const actionable = data.attentionItems.filter((a) => a.count > 0);
  return (
    <div className="bg-white border border-slate-200 rounded-xl px-4 py-3">
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs font-bold text-slate-500 uppercase tracking-wide mr-1 shrink-0">Cần xử lý</span>
        {actionable.length === 0 && (
          <span className="flex items-center gap-1.5 text-sm font-medium text-emerald-600">
            <CheckCircle2 className="w-4 h-4" /> Không có mục nào cần xử lý
          </span>
        )}
        {actionable.map((a) => {
          const style = SEVERITY_STYLES[a.severity] ?? SEVERITY_STYLES.INFO;
          return (
            <div
              key={a.type}
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

// ───────────────────────────── Charts row ─────────────────────────────

function ChartsRow({ data }: { data: StaffLeaderReportOverview }) {
  const trend = data.monthlyTrend;
  const trendEmpty = trend.length === 0 || trend.every((m) => m.totalInstances === 0);
  const pipeline = data.campusLifecyclePipeline;
  const pipelineTotal = pipeline.reduce((sum, s) => sum + s.count, 0);
  const pipelineRows = pipeline.filter((s) => s.count > 0);
  const maxCount = Math.max(1, ...pipelineRows.map((s) => s.count));

  return (
    <div className="grid grid-cols-1 lg:grid-cols-5 gap-4">
      {/* Chart 1 — Xu hướng chuyến thăm theo tháng */}
      <div className="lg:col-span-3 bg-white border border-slate-200 rounded-xl p-4">
        <div className="flex items-start justify-between gap-2 mb-2 flex-wrap">
          <div>
            <h3 className="text-sm font-bold text-slate-800">Xu hướng chuyến thăm theo tháng</h3>
            <p className="text-xs text-slate-400 font-medium">Tính theo ngày thăm dự kiến tại campus</p>
          </div>
          <div className="flex items-center gap-3 text-[11px] font-semibold text-slate-500 flex-wrap">
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_BLUE }} />Tổng chuyến</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_ORANGE }} />Đang xử lý</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_GREEN }} />Đã đóng</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_VIOLET }} />Bị hủy</span>
          </div>
        </div>
        {trendEmpty ? (
          <ChartEmpty height={272} />
        ) : (
          <div className="h-[272px] w-full">
            <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
              <LineChart data={trend} margin={{ top: 8, right: 8, left: -18, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#e2e8f0" />
                <XAxis dataKey="monthLabel" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#64748b' }} dy={8} />
                <YAxis allowDecimals={false} axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#64748b' }} />
                <Tooltip
                  contentStyle={{ borderRadius: 10, border: '1px solid #e2e8f0', boxShadow: '0 4px 12px rgb(0 0 0 / 0.08)', fontSize: 12 }}
                  labelStyle={{ fontWeight: 700, color: '#1e293b' }}
                />
                <Line type="monotone" dataKey="totalInstances" name="Tổng chuyến" stroke={CHART_BLUE} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
                <Line type="monotone" dataKey="activeInstances" name="Đang xử lý" stroke={CHART_ORANGE} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
                <Line type="monotone" dataKey="closedInstances" name="Đã đóng" stroke={CHART_GREEN} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
                <Line type="monotone" dataKey="cancelledInstances" name="Bị hủy" stroke={CHART_VIOLET} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </div>

      {/* Chart 2 — Phân bổ trạng thái chuyến (horizontal bars, label trực tiếp từng dòng) */}
      <div className="lg:col-span-2 bg-white border border-slate-200 rounded-xl p-4 flex flex-col">
        <h3 className="text-sm font-bold text-slate-800">Phân bổ trạng thái chuyến</h3>
        <p className="text-xs text-slate-400 font-medium mb-2">{fmt.formatNumber(pipelineTotal)} chuyến trong kỳ</p>
        {pipelineTotal === 0 ? (
          <ChartEmpty height={250} />
        ) : (
          <div className="flex-1 flex flex-col justify-center gap-2">
            {pipelineRows.map((s) => (
              <div key={s.status} className="flex items-center gap-2 text-xs" title={`${s.labelVi}: ${s.count} (${s.percentage}%)`}>
                <span className="w-24 truncate font-semibold text-slate-600 shrink-0">{s.labelVi}</span>
                <div className="flex-1 h-4 bg-slate-100 rounded overflow-hidden">
                  <div
                    className="h-full rounded"
                    style={{ width: `${(s.count / maxCount) * 100}%`, background: CHART_BLUE }}
                  />
                </div>
                <span className="font-bold text-slate-800 w-8 text-right">{s.count}</span>
                <span className="text-slate-400 w-12 text-right">{s.percentage}%</span>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

function ChartEmpty({ height }: { height: number }) {
  return (
    <div style={{ height }} className="flex flex-col items-center justify-center text-slate-400">
      <Info className="w-6 h-6 mb-2" />
      <p className="text-sm font-medium">Không có dữ liệu trong bộ lọc này.</p>
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

// ───────────────────────────── Host workload ─────────────────────────────

function HostWorkloadCard({ data }: { data: StaffLeaderReportOverview }) {
  const rows = data.hostWorkload;
  return (
    <div id="host-workload" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 py-3 border-b border-slate-100">
        <h3 className="text-sm font-bold text-slate-800">Khối lượng host</h3>
        <p className="text-xs text-slate-400 font-medium">Chuyến đang phụ trách theo trạng thái hiện tại</p>
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Không có host đang phụ trách trong bộ lọc này." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[560px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Host</th>
                <th className={`${thClass} text-right`}>Phụ trách</th>
                <th className={`${thClass} text-right`}>7 ngày tới</th>
                <th className={`${thClass} text-right`}>Chuẩn bị</th>
                <th className={`${thClass} text-right`}>Đang tiếp</th>
                <th className={`${thClass} text-right`}>Sau chuyến</th>
                <th className={`${thClass} text-right`}>FB TB</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((h) => (
                <tr key={h.hostUserId} className="hover:bg-blue-50/40 transition-colors">
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[180px] truncate`} title={h.hostName}>{h.hostName}</td>
                  <td className={`${tdClass} text-right font-bold text-[#004c91]`}>{h.assignedCount}</td>
                  <td className={`${tdClass} text-right font-semibold ${h.upcoming7Days > 0 ? 'text-amber-600' : 'text-slate-400'}`}>{h.upcoming7Days}</td>
                  <td className={`${tdClass} text-right`}>{h.beforeVisitCount}</td>
                  <td className={`${tdClass} text-right`}>{h.duringVisitCount}</td>
                  <td className={`${tdClass} text-right`}>{h.afterVisitCount}</td>
                  <td className={`${tdClass} text-right font-semibold ${h.averageFeedbackRating != null && h.averageFeedbackRating < 3 ? 'text-red-600' : 'text-slate-700'}`}>
                    {fmt.formatRating(h.averageFeedbackRating)}
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

// ───────────────────────────── Logistics ─────────────────────────────

function LogisticsCard({ data }: { data: StaffLeaderReportOverview }) {
  const rows = data.logisticsByDepartment;
  return (
    <div id="logistics" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 py-3 border-b border-slate-100">
        <h3 className="text-sm font-bold text-slate-800">Tiến độ logistics / hỗ trợ phòng ban</h3>
        <p className="text-xs text-slate-400 font-medium">Yêu cầu hậu cần của các chuyến trong kỳ</p>
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Không có dữ liệu trong bộ lọc này." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[560px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Phòng ban</th>
                <th className={`${thClass} text-right`}>Tổng</th>
                <th className={`${thClass} text-right`}>Chờ phản hồi</th>
                <th className={`${thClass} text-right`}>Đang xử lý</th>
                <th className={`${thClass} text-right`}>Hoàn thành</th>
                <th className={`${thClass} text-right`}>Từ chối</th>
                <th className={`${thClass} text-right`}>Quá hạn</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((d) => (
                <tr key={d.departmentId} className="hover:bg-blue-50/40 transition-colors">
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[180px] truncate`} title={d.departmentName}>{d.departmentName}</td>
                  <td className={`${tdClass} text-right font-bold text-slate-800`}>{d.totalItems}</td>
                  <td className={`${tdClass} text-right font-semibold ${d.requested > 0 ? 'text-amber-600' : 'text-slate-400'}`}>{d.requested}</td>
                  <td className={`${tdClass} text-right`}>{d.accepted + d.inProgress}</td>
                  <td className={`${tdClass} text-right text-emerald-600 font-semibold`}>{d.done}</td>
                  <td className={`${tdClass} text-right ${d.rejected > 0 ? 'text-red-600 font-semibold' : 'text-slate-400'}`}>{d.rejected}</td>
                  <td className={`${tdClass} text-right font-bold ${d.overdueCount > 0 ? 'text-red-600' : 'text-slate-400'}`}>{d.overdueCount}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ───────────────────────────── Pending actions ─────────────────────────────

function PriorityBadge({ waitingHours }: { waitingHours: number }) {
  if (waitingHours >= 48) {
    return <span className="inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 bg-red-50 text-red-600 border-red-200">Cao</span>;
  }
  if (waitingHours >= 24) {
    return <span className="inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 bg-amber-50 text-amber-700 border-amber-200">Trung bình</span>;
  }
  return <span className="inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 bg-slate-100 text-slate-500 border-slate-200">Bình thường</span>;
}

function PendingActionsTable({ data, onOpen }: { data: StaffLeaderReportOverview; onOpen: () => void }) {
  const rows = data.pendingActionRequests;
  return (
    <div id="pending-actions" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 py-3 border-b border-slate-100 flex items-center justify-between gap-2 flex-wrap">
        <h3 className="text-sm font-bold text-slate-800">
          Đơn cần Staff Leader xử lý
          {data.pendingActionTotal > rows.length && (
            <span className="text-slate-400 font-medium"> · hiển thị {rows.length}/{data.pendingActionTotal}</span>
          )}
        </h3>
        {data.pendingActionTotal > 0 && (
          <button onClick={onOpen} className="flex items-center gap-1 text-xs font-bold text-[#004c91] hover:underline cursor-pointer">
            Mở Quản lý tiếp khách <ArrowRight className="w-3.5 h-3.5" />
          </button>
        )}
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Không có đơn nào đang chờ xử lý." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[980px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Ưu tiên</th>
                <th className={thClass}>Mã đơn</th>
                <th className={thClass}>Tên đoàn</th>
                <th className={thClass}>Loại đơn</th>
                <th className={thClass}>Ngày thăm</th>
                <th className={`${thClass} text-right`}>Khách</th>
                <th className={thClass}>Trạng thái</th>
                <th className={thClass}>Thời gian chờ</th>
                <th className={thClass}>Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((r) => (
                <tr key={`${r.type}-${r.requestId}-${r.visitInstanceId ?? 0}`} className="hover:bg-blue-50/40 transition-colors">
                  <td className={tdClass}><PriorityBadge waitingHours={r.waitingHours} /></td>
                  <td className={`${tdClass} font-bold text-[#004c91]`}>{r.requestCode}</td>
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[220px] truncate`} title={`${r.delegationName} · ${r.organizationName}`}>
                    {r.delegationName}
                  </td>
                  <td className={tdClass}>{visitTypeLabel(r.visitType)}</td>
                  <td className={tdClass}>{fmt.formatDate(r.plannedStartAt)} – {fmt.formatDate(r.plannedEndAt)}</td>
                  <td className={`${tdClass} text-right`}>{r.guestCount}</td>
                  <td className={tdClass}>
                    <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${fmt.statusBadgeClass(r.status)}`}>
                      {r.type === 'APPROVAL' ? fmt.requestStatusLabel(r.status) : fmt.instanceStatusLabel(r.status)}
                    </span>
                  </td>
                  <td className={`${tdClass} font-semibold ${r.waitingHours >= 48 ? 'text-red-600' : 'text-amber-600'}`}>
                    {fmt.formatWaitingHours(r.waitingHours)}
                  </td>
                  <td className={tdClass}>
                    <button onClick={onOpen} className="text-xs font-bold text-[#004c91] hover:underline cursor-pointer">
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
  );
}

// ───────────────────────────── Close readiness ─────────────────────────────

function ReadyBadge({ ok, okText, failText }: { ok: boolean; okText: string; failText: string }) {
  return (
    <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${
      ok ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-amber-50 text-amber-700 border-amber-200'
    }`}>
      {ok ? okText : failText}
    </span>
  );
}

function CloseReadinessTable({ data, onOpen }: { data: StaffLeaderReportOverview; onOpen: (visitInstanceId: number) => void }) {
  const rows = data.closeReadiness;
  return (
    <div id="close-readiness" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 py-3 border-b border-slate-100">
        <h3 className="text-sm font-bold text-slate-800">
          Hồ sơ cần hoàn tất sau tiếp khách
          {data.closeReadinessTotal > rows.length && (
            <span className="text-slate-400 font-medium"> · hiển thị {rows.length}/{data.closeReadinessTotal}</span>
          )}
        </h3>
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Không có hồ sơ sau tiếp khách cần hoàn tất." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[900px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Đoàn</th>
                <th className={thClass}>Host</th>
                <th className={thClass}>Ngày kết thúc</th>
                <th className={thClass}>Logistics</th>
                <th className={thClass}>Minutes</th>
                <th className={thClass}>News</th>
                <th className={`${thClass} text-right`}>Feedback</th>
                <th className={thClass}>Có thể đóng</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((r) => (
                <tr
                  key={r.visitInstanceId}
                  onClick={() => onOpen(r.visitInstanceId)}
                  title={r.blockers.length > 0 ? `Vướng mắc: ${r.blockers.map(fmt.blockerLabel).join(', ')}` : 'Đủ điều kiện đóng'}
                  className="hover:bg-blue-50/40 transition-colors cursor-pointer"
                >
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[240px] truncate`} title={`${r.requestCode} · ${r.delegationName}`}>
                    {r.delegationName}
                  </td>
                  <td className={tdClass}>{r.hostName ?? '—'}</td>
                  <td className={tdClass}>{fmt.formatDate(r.plannedEndAt)}</td>
                  <td className={tdClass}>
                    <ReadyBadge
                      ok={r.logisticsOpenCount === 0 && r.missingHandoverSignatureCount === 0}
                      okText="Đủ"
                      failText={`Còn mở ${r.logisticsOpenCount + r.missingHandoverSignatureCount}`}
                    />
                  </td>
                  <td className={tdClass}>
                    {r.hasMinutes
                      ? <ReadyBadge ok={r.openActionItemCount === 0} okText="Đủ" failText={`${r.openActionItemCount} việc mở`} />
                      : <span className="inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 bg-slate-100 text-slate-500 border-slate-200">Thiếu</span>}
                  </td>
                  <td className={tdClass}>
                    {r.newsNotRequired
                      ? <span className="inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 bg-slate-100 text-slate-500 border-slate-200">Không cần</span>
                      : <ReadyBadge ok={r.hasPublishedNews} okText="Đã đăng" failText="Thiếu" />}
                  </td>
                  <td className={`${tdClass} text-right font-semibold ${r.feedbackCount === 0 ? 'text-slate-400' : 'text-slate-700'}`}>
                    {r.feedbackCount}
                  </td>
                  <td className={tdClass}>
                    <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${
                      r.canClose ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-red-50 text-red-600 border-red-200'
                    }`}>
                      {r.canClose ? 'Có thể đóng' : 'Chưa thể đóng'}
                    </span>
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

// ───────────────────────────── Feedback ─────────────────────────────

function FeedbackSection({ data, onOpen }: {
  data: StaffLeaderReportOverview;
  onOpen: (visitInstanceId: number) => void;
}) {
  // Mặc định tab feedback thấp — Staff Leader cần xử lý vấn đề trước.
  const [tab, setTab] = useState<'low' | 'good'>('low');
  const fb = data.feedbackSummary;
  const rows = tab === 'low' ? fb.lowFeedbacks : fb.goodFeedbacks;

  return (
    <div id="feedback" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 pt-3 border-b border-slate-100 flex items-center justify-between gap-2 flex-wrap">
        <div className="flex items-center gap-1">
          {([
            { key: 'low' as const, label: `Feedback thấp cần chú ý (${fb.lowFeedbackCount})` },
            { key: 'good' as const, label: 'Feedback tốt gần đây' },
          ]).map((t) => (
            <button
              key={t.key}
              onClick={() => setTab(t.key)}
              className={`px-4 py-2 text-sm font-bold rounded-t-lg border-b-2 transition-colors cursor-pointer ${
                tab === t.key ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-slate-400 hover:text-slate-600'
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>
        <p className="text-xs font-medium text-slate-400 pb-2">
          Điểm TB <span className="font-black text-slate-700">{fmt.formatRating(fb.averageRating)}</span>/5
          {' '}· {fmt.formatNumber(fb.totalFeedbacks)} feedback trong kỳ
        </p>
      </div>

      {rows.length === 0 ? (
        <SectionEmpty text={tab === 'low' ? 'Không có feedback thấp trong bộ lọc này.' : 'Chưa có feedback tốt trong bộ lọc này.'} />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[860px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Đoàn</th>
                <th className={thClass}>Host</th>
                <th className={`${thClass} text-right`}>Rating</th>
                <th className={thClass}>Nội dung</th>
                <th className={thClass}>Ngày thăm</th>
                <th className={thClass}>Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((e: StaffLeaderFeedbackEntry) => (
                <tr key={e.feedbackId} className="hover:bg-blue-50/40 transition-colors">
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[220px] truncate`} title={e.delegationName}>
                    {e.delegationName}
                  </td>
                  <td className={tdClass}>{e.hostName ?? '—'}</td>
                  <td className={`${tdClass} text-right font-black ${e.rating <= 2 ? 'text-red-600' : e.rating >= 4 ? 'text-emerald-600' : 'text-slate-700'}`}>
                    {e.rating}/5
                  </td>
                  <td className={`${tdClass} max-w-[320px] truncate`} title={e.comment ?? undefined}>
                    {e.comment || <span className="text-slate-400">Không có nhận xét</span>}
                  </td>
                  <td className={tdClass}>{fmt.formatDate(e.plannedStartAt)}</td>
                  <td className={tdClass}>
                    <button onClick={() => onOpen(e.visitInstanceId)} className="text-xs font-bold text-[#004c91] hover:underline cursor-pointer">
                      Xem chuyến
                    </button>
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
