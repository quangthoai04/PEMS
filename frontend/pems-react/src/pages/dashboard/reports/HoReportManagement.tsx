/**
 * Trang HoReportManagement — Báo cáo Head Office tại /dashboard/reports (role HO).
 * Enterprise dashboard: KPI strip compact, attention bar, chart xu hướng + tỷ lệ quyết định,
 * lifecycle pipeline, hiệu suất cơ sở, đơn liên cơ sở chờ duyệt, close readiness,
 * feedback & content. Toàn bộ dữ liệu lấy từ GET /reports/ho-overview (không mock).
 */

import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import {
  AlertTriangle, ArrowRight, CheckCircle2, ChevronDown, Download, FileSpreadsheet,
  FileText, Info, Loader2, RefreshCw, ShieldAlert, X,
} from 'lucide-react';
import {
  CartesianGrid, Cell, Line, LineChart, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { useHoReport } from '../../../features/reports/hooks/useReports';
import { reportsAdapter as fmt } from '../../../features/reports/adapters/reportsAdapter';
import type {
  HoExportFormat, HoInstanceStatusFilter, HoReportOverview, HoReportSection,
} from '../../../features/reports/types/reports.types';

// Palette chart đã validate CVD/contrast (scripts/validate_palette.js — ALL PASS trên nền trắng).
const CHART_BLUE = '#1e6fc0';
const CHART_ORANGE = '#d95f18';
const CHART_GREEN = '#0a8a44';
// Màu trạng thái (semantic) cho donut tỷ lệ quyết định; thứ tự slice tránh đỏ cạnh vàng (CVD).
const STATUS_APPROVED = '#0a8a44';
const STATUS_PENDING = '#f59e0b';
const STATUS_CANCELLED = '#94a3b8';
const STATUS_REJECTED = '#b91c1c';

const SECTION_OPTIONS: { value: HoReportSection; label: string }[] = [
  { value: 'EXECUTIVE_SUMMARY', label: 'Executive Summary' },
  { value: 'APPROVAL_OVERVIEW', label: 'Approval & Request Overview' },
  { value: 'CAMPUS_PERFORMANCE', label: 'Campus Performance' },
  { value: 'LIFECYCLE_CLOSE_READINESS', label: 'Lifecycle & Close Readiness' },
  { value: 'FEEDBACK_QUALITY', label: 'Feedback Quality' },
  { value: 'CONTENT_EMAIL_EFFECTIVENESS', label: 'Content & Email Effectiveness' },
  { value: 'PARTNER_ENGAGEMENT', label: 'Partner Engagement' },
];

const PARTNER_TYPE_LABELS: Record<string, string> = {
  UNIVERSITY: 'Trường ĐH',
  COMPANY: 'Doanh nghiệp',
  GOVERNMENT: 'Chính phủ',
  NGO: 'NGO',
  OTHER: 'Khác',
};
const partnerTypeLabel = (t: string) => PARTNER_TYPE_LABELS[t] ?? t;

const COOPERATION_LABELS: Record<string, string> = {
  ACTIVE: 'Đang hợp tác',
  INACTIVE: 'Ngừng hợp tác',
  PAUSED: 'Tạm dừng',
};
const cooperationLabel = (s: string) => COOPERATION_LABELS[s] ?? s;

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

const selectClass =
  'bg-white border border-slate-200 rounded-lg px-2.5 py-2 text-sm font-medium text-slate-700 outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] cursor-pointer';

export function HoReportManagement() {
  const navigate = useNavigate();
  const {
    filters, setFilters, appliedFilters, data, loading, error, refetch,
    applyFilters, resetFilters, exportReport, exportLoading,
  } = useHoReport();

  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [exportConfirm, setExportConfirm] = useState<HoExportFormat | null>(null);
  const [exportSections, setExportSections] = useState<HoReportSection[]>(SECTION_OPTIONS.map((s) => s.value));
  const exportMenuRef = useRef<HTMLDivElement>(null);

  // Danh sách campus cho filter: giữ danh sách đầy đủ từ lần load không lọc campus.
  const [campusOptions, setCampusOptions] = useState<{ id: number; name: string }[]>([]);
  useEffect(() => {
    if (data && !appliedFilters.campusId) {
      setCampusOptions(data.campusPerformance.map((c) => ({ id: c.campusId, name: c.campusName })));
    }
  }, [data, appliedFilters.campusId]);

  useEffect(() => {
    const close = (e: MouseEvent) => {
      if (exportMenuRef.current && !exportMenuRef.current.contains(e.target as Node)) setExportMenuOpen(false);
    };
    document.addEventListener('mousedown', close);
    return () => document.removeEventListener('mousedown', close);
  }, []);

  const scrollTo = (sectionId: string) => {
    document.getElementById(sectionId)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  };

  // Click step = lọc toàn báo cáo theo trạng thái đó (bấm lại để bỏ lọc).
  const handlePipelineClick = (status: string) => {
    const nextStatus = appliedFilters.campusInstanceStatus === status ? 'ALL' : status;
    applyFilters({ ...filters, campusInstanceStatus: nextStatus as HoInstanceStatusFilter });
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
        ? 'Bạn không có quyền xuất báo cáo Head Office.'
        : 'Không thể xuất báo cáo. Vui lòng thử lại sau.');
    }
  };

  // ── 403: không render dashboard rỗng ──
  if (error === 'FORBIDDEN') {
    return (
      <div className="max-w-[1400px] mx-auto flex flex-col items-center justify-center py-24 text-center">
        <ShieldAlert className="w-12 h-12 text-red-400 mb-4" />
        <h2 className="text-xl font-bold text-slate-800 mb-1">Bạn không có quyền xem báo cáo Head Office</h2>
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
          <span className="text-[#004c91] font-bold">Quản lý báo cáo</span>
        </div>
        <div className="flex flex-col md:flex-row md:items-end justify-between gap-3">
          <div>
            <div className="flex items-center gap-3 flex-wrap">
              <h1 className="text-2xl font-black text-[#004c91] tracking-tight">Báo cáo Head Office</h1>
              <span className="text-[11px] font-bold uppercase tracking-wide text-[#004c91] bg-blue-50 border border-blue-100 rounded-full px-2.5 py-1">
                HO · Toàn hệ thống
              </span>
            </div>
            <p className="text-sm font-medium text-slate-500 mt-0.5">
              Tổng quan yêu cầu tiếp đón, hiệu suất cơ sở và chất lượng sau chuyến thăm
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
                  { format: 'EXCEL' as HoExportFormat, label: 'Excel (.xlsx)', icon: FileSpreadsheet },
                  { format: 'PDF' as HoExportFormat, label: 'PDF (.pdf)', icon: FileText },
                  { format: 'CSV' as HoExportFormat, label: 'CSV (.csv)', icon: FileText },
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
          onChange={(e) => setFilters({ ...filters, preset: e.target.value as typeof filters.preset })}
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
          value={filters.campusId ?? ''}
          onChange={(e) => setFilters({ ...filters, campusId: e.target.value ? Number(e.target.value) : undefined })}
          className={selectClass}
          aria-label="Cơ sở"
        >
          <option value="">Campus: Tất cả</option>
          {campusOptions.map((c) => (
            <option key={c.id} value={c.id}>{c.name}</option>
          ))}
        </select>

        <select
          value={filters.visitScope}
          onChange={(e) => setFilters({ ...filters, visitScope: e.target.value as typeof filters.visitScope })}
          className={selectClass}
          aria-label="Phạm vi chuyến"
        >
          <option value="ALL">Scope: Tất cả</option>
          <option value="SINGLE_CAMPUS">Single-campus</option>
          <option value="MULTI_CAMPUS">Multi-campus</option>
        </select>

        <select
          value={filters.requestStatus}
          onChange={(e) => setFilters({ ...filters, requestStatus: e.target.value as typeof filters.requestStatus })}
          className={selectClass}
          aria-label="Trạng thái đơn"
        >
          <option value="ALL">Đơn: Tất cả</option>
          <option value="PENDING_APPROVAL">Chờ duyệt</option>
          <option value="APPROVED">Đã duyệt</option>
          <option value="REJECTED">Từ chối</option>
          <option value="CANCELLED">Đã hủy</option>
        </select>

        <select
          value={filters.campusInstanceStatus}
          onChange={(e) => setFilters({ ...filters, campusInstanceStatus: e.target.value as typeof filters.campusInstanceStatus })}
          className={selectClass}
          aria-label="Trạng thái instance"
        >
          <option value="ALL">Instance: Tất cả</option>
          <option value="WAITING_REQUEST_APPROVAL">Chờ duyệt</option>
          <option value="WAITING_HOST_ASSIGNMENT">Chờ gán host</option>
          <option value="ASSIGNED">Đã gán host</option>
          <option value="BEFORE_VISIT">Trước tiếp khách</option>
          <option value="DURING_VISIT">Đang tiếp</option>
          <option value="AFTER_VISIT">Sau tiếp khách</option>
          <option value="CLOSED">Đã đóng</option>
          <option value="CANCELLED">Đã hủy</option>
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
          <AttentionBar data={data} onView={scrollTo} />
          <ChartsRow data={data} />
          <LifecyclePipeline
            data={data}
            activeStatus={appliedFilters.campusInstanceStatus}
            onSelect={handlePipelineClick}
          />
          <CampusPerformanceTable data={data} />
          <PendingRequestsTable data={data} onOpen={() => navigate('/dashboard/visit')} />
          <CloseReadinessTable
            data={data}
            onOpen={(visitInstanceId) => navigate(`/dashboard/visit/process-summary/${visitInstanceId}`)}
          />
          <PartnerEngagementSection data={data} onOpen={() => navigate('/dashboard/partners')} />
          <FeedbackContentSection data={data} />
          <p className="text-[11px] text-slate-400 text-right">
            Số liệu theo kỳ tính bằng ngày gửi yêu cầu / ngày thăm dự kiến · Khối tác vụ (chờ duyệt, đóng hồ sơ)
            tính theo trạng thái hiện tại · Cập nhật {fmt.formatDateTime(data.generatedAt)}
          </p>
        </>
      )}

      {/* ── Modal xác nhận export ── */}
      {exportConfirm && data && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden">
            <div className="flex items-center justify-between px-5 py-4 border-b border-slate-100">
              <h3 className="text-base font-bold text-slate-800">Xuất báo cáo Head Office</h3>
              <button onClick={() => setExportConfirm(null)} className="text-slate-400 hover:text-slate-600 cursor-pointer" aria-label="Đóng">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="px-5 py-4 space-y-3 text-sm">
              <div className="grid grid-cols-[110px_1fr] gap-y-1.5 text-slate-600">
                <span className="font-semibold text-slate-400">Định dạng</span>
                <span className="font-bold text-slate-800">{exportConfirm}</span>
                <span className="font-semibold text-slate-400">Thời gian</span>
                <span>{PRESET_LABELS[data.filterSummary.preset]} ({fmt.formatDate(data.filterSummary.fromDate)} – {fmt.formatDate(data.filterSummary.toDate)})</span>
                <span className="font-semibold text-slate-400">Campus</span>
                <span>{data.filterSummary.campusName}</span>
                <span className="font-semibold text-slate-400">Scope</span>
                <span>{data.filterSummary.visitScope === 'ALL' ? 'Tất cả' : data.filterSummary.visitScope}</span>
                <span className="font-semibold text-slate-400">Trạng thái</span>
                <span>
                  Đơn: {data.filterSummary.requestStatus === 'ALL' ? 'Tất cả' : fmt.requestStatusLabel(data.filterSummary.requestStatus)} ·
                  {' '}Instance: {data.filterSummary.campusInstanceStatus === 'ALL' ? 'Tất cả' : fmt.instanceStatusLabel(data.filterSummary.campusInstanceStatus)}
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
      <div className="h-64 bg-white border border-slate-200 rounded-xl" />
    </div>
  );
}

// ───────────────────────────── KPI strip ─────────────────────────────

function KpiStrip({ data }: { data: HoReportOverview }) {
  const k = data.kpis;
  const items: { label: string; value: string; sub?: string; tone?: 'warn' | 'danger' | 'good'; title?: string }[] = [
    { label: 'Tổng yêu cầu', value: fmt.formatNumber(k.totalRequests), sub: `${fmt.formatNumber(k.totalGuests)} lượt khách`, title: 'Tính theo ngày gửi yêu cầu trong kỳ' },
    { label: 'Chờ HO duyệt', value: fmt.formatNumber(k.multiCampusPending), sub: 'đơn liên cơ sở', tone: k.multiCampusPending > 0 ? 'warn' : undefined, title: 'Đơn MULTI_CAMPUS đang chờ duyệt (trạng thái hiện tại)' },
    { label: 'Đã duyệt', value: fmt.formatNumber(k.approvedRequests), sub: `${fmt.formatPercent(data.approvalBreakdown.approvalRate)} tổng đơn` },
    { label: 'Bị từ chối', value: fmt.formatNumber(k.rejectedRequests), sub: `${fmt.formatNumber(k.cancelledRequests)} đã hủy` },
    { label: 'Campus đang xử lý', value: fmt.formatNumber(k.activeCampusInstances), sub: 'instance chưa đóng' },
    { label: 'Đã đóng', value: fmt.formatNumber(k.closedCampusInstances), sub: 'hồ sơ hoàn tất' },
    { label: 'Quá hạn đóng', value: fmt.formatNumber(k.overdueCloseInstances), sub: 'qua ngày kết thúc', tone: k.overdueCloseInstances > 0 ? 'danger' : undefined },
    { label: 'Feedback TB', value: k.averageFeedbackRating != null ? `${fmt.formatRating(k.averageFeedbackRating)}/5` : '—', sub: k.averageDecisionHours != null ? `duyệt TB ${fmt.formatWaitingHours(k.averageDecisionHours)}` : undefined, tone: 'good' },
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

// ───────────────────────────── Attention bar ─────────────────────────────

function AttentionBar({ data, onView }: { data: HoReportOverview; onView: (section: string) => void }) {
  const actionable = data.attentionItems.filter((a) => a.count > 0);
  return (
    <div className="bg-white border border-slate-200 rounded-xl px-4 py-3">
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-xs font-bold text-slate-500 uppercase tracking-wide mr-1 shrink-0">Cần HO chú ý</span>
        {actionable.length === 0 && (
          <span className="flex items-center gap-1.5 text-sm font-medium text-emerald-600">
            <CheckCircle2 className="w-4 h-4" /> Không có mục nào cần xử lý
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

// ───────────────────────────── Charts row ─────────────────────────────

function ChartsRow({ data }: { data: HoReportOverview }) {
  const trend = data.monthlyTrend;
  const trendEmpty = trend.length === 0 || trend.every((m) => m.totalRequests === 0);
  const b = data.approvalBreakdown;
  const donutTotal = b.approved + b.pending + b.rejected + b.cancelled;
  // Thứ tự slice tránh đỏ nằm cạnh vàng (hỗ trợ người mù màu), kèm gap trắng giữa slice.
  const donutData = [
    { name: 'Đã duyệt', value: b.approved, color: STATUS_APPROVED },
    { name: 'Chờ duyệt', value: b.pending, color: STATUS_PENDING },
    { name: 'Đã hủy', value: b.cancelled, color: STATUS_CANCELLED },
    { name: 'Từ chối', value: b.rejected, color: STATUS_REJECTED },
  ].filter((d) => d.value > 0);

  return (
    <div className="grid grid-cols-1 lg:grid-cols-5 gap-4">
      {/* Trend */}
      <div className="lg:col-span-3 bg-white border border-slate-200 rounded-xl p-4">
        <div className="flex items-start justify-between gap-2 mb-2 flex-wrap">
          <div>
            <h3 className="text-sm font-bold text-slate-800">Xu hướng yêu cầu theo tháng</h3>
            <p className="text-xs text-slate-400 font-medium">Tính theo ngày gửi yêu cầu</p>
          </div>
          <div className="flex items-center gap-3 text-[11px] font-semibold text-slate-500">
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_BLUE }} />Tổng yêu cầu</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_GREEN }} />Single-campus</span>
            <span className="flex items-center gap-1.5"><span className="w-2.5 h-2.5 rounded-full" style={{ background: CHART_ORANGE }} />Multi-campus</span>
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
                <Line type="monotone" dataKey="totalRequests" name="Tổng yêu cầu" stroke={CHART_BLUE} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
                <Line type="monotone" dataKey="singleCampusRequests" name="Single-campus" stroke={CHART_GREEN} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
                <Line type="monotone" dataKey="multiCampusRequests" name="Multi-campus" stroke={CHART_ORANGE} strokeWidth={2} dot={false} activeDot={{ r: 4 }} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        )}
      </div>

      {/* Approval donut */}
      <div className="lg:col-span-2 bg-white border border-slate-200 rounded-xl p-4 flex flex-col">
        <h3 className="text-sm font-bold text-slate-800">Tỷ lệ quyết định</h3>
        <p className="text-xs text-slate-400 font-medium mb-1">
          Duyệt {fmt.formatPercent(b.approvalRate)} · Từ chối {fmt.formatPercent(b.rejectionRate)}
          {b.averageDecisionHours != null && <> · Quyết định TB {fmt.formatWaitingHours(b.averageDecisionHours)}</>}
        </p>
        {donutTotal === 0 ? (
          <ChartEmpty height={250} />
        ) : (
          <div className="flex-1 flex flex-col items-center justify-center">
            <div className="relative w-full h-[190px]">
              <ResponsiveContainer width="100%" height="100%" minWidth={1} minHeight={1}>
                <PieChart>
                  <Pie
                    data={donutData}
                    cx="50%" cy="50%"
                    innerRadius={58} outerRadius={82}
                    paddingAngle={2}
                    dataKey="value"
                    stroke="#ffffff"
                    strokeWidth={2}
                  >
                    {donutData.map((entry) => (
                      <Cell key={entry.name} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip
                    formatter={(value: number, name: string) => [`${fmt.formatNumber(value)} đơn`, name]}
                    contentStyle={{ borderRadius: 10, border: '1px solid #e2e8f0', boxShadow: '0 4px 12px rgb(0 0 0 / 0.08)', fontSize: 12 }}
                  />
                </PieChart>
              </ResponsiveContainer>
              <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
                <span className="text-2xl font-black text-slate-800">{fmt.formatNumber(donutTotal)}</span>
                <span className="text-[11px] font-semibold text-slate-400">tổng đơn</span>
              </div>
            </div>
            <div className="grid grid-cols-2 gap-x-4 gap-y-1 w-full mt-2">
              {donutData.map((d) => (
                <div key={d.name} className="flex items-center gap-1.5 text-xs">
                  <span className="w-2.5 h-2.5 rounded-full shrink-0" style={{ background: d.color }} />
                  <span className="font-semibold text-slate-600 flex-1 truncate">{d.name}</span>
                  <span className="font-bold text-slate-800">{fmt.formatNumber(d.value)}</span>
                </div>
              ))}
            </div>
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
      <p className="text-sm font-medium">Không có dữ liệu trong khoảng thời gian đã chọn.</p>
    </div>
  );
}

// ───────────────────────────── Lifecycle pipeline ─────────────────────────────

const PIPELINE_TONES: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'bg-amber-400',
  WAITING_HOST_ASSIGNMENT: 'bg-orange-400',
  ASSIGNED: 'bg-blue-400',
  BEFORE_VISIT: 'bg-sky-400',
  DURING_VISIT: 'bg-indigo-400',
  AFTER_VISIT: 'bg-violet-400',
  CLOSED: 'bg-emerald-400',
  CANCELLED: 'bg-slate-300',
};

function LifecyclePipeline({ data, activeStatus, onSelect }: {
  data: HoReportOverview;
  activeStatus: string;
  onSelect: (status: string) => void;
}) {
  return (
    <div className="bg-white border border-slate-200 rounded-xl p-4">
      <div className="flex items-center justify-between mb-3 flex-wrap gap-1">
        <h3 className="text-sm font-bold text-slate-800">Tiến độ campus instances</h3>
        <p className="text-[11px] text-slate-400 font-medium">Bấm một bước để lọc toàn bộ báo cáo theo trạng thái đó</p>
      </div>
      <div className="grid grid-cols-4 lg:grid-cols-8 gap-2">
        {data.lifecyclePipeline.map((step) => {
          const active = activeStatus === step.status;
          return (
            <button
              key={step.status}
              onClick={() => onSelect(step.status)}
              title={`${step.labelVi}: ${step.count} (${step.percentage}%)`}
              className={`text-left rounded-lg border px-2.5 py-2 transition-colors cursor-pointer ${
                active ? 'border-[#004c91] bg-blue-50/60 ring-1 ring-[#004c91]/30' : 'border-slate-200 hover:bg-slate-50'
              }`}
            >
              <span className={`block h-1 rounded-full mb-1.5 ${PIPELINE_TONES[step.status] ?? 'bg-slate-300'}`} />
              <span className="block text-[11px] font-semibold text-slate-500 truncate">{step.labelVi}</span>
              <span className="flex items-baseline gap-1">
                <span className="text-lg font-black text-slate-800">{fmt.formatNumber(step.count)}</span>
                <span className="text-[11px] font-medium text-slate-400">{step.percentage}%</span>
              </span>
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ───────────────────────────── Campus performance ─────────────────────────────

const thClass = 'px-3 py-2.5 text-[11px] font-bold text-slate-400 uppercase tracking-wide whitespace-nowrap';
const tdClass = 'px-3 py-2.5 text-sm text-slate-600 whitespace-nowrap';

function CampusPerformanceTable({ data }: { data: HoReportOverview }) {
  const rows = data.campusPerformance;
  const hasData = rows.some((r) => r.totalInstances > 0);
  return (
    <div className="bg-white border border-slate-200 rounded-xl overflow-hidden">
      <div className="px-4 py-3 border-b border-slate-100">
        <h3 className="text-sm font-bold text-slate-800">Hiệu suất theo cơ sở</h3>
      </div>
      {!hasData ? (
        <SectionEmpty text="Không có dữ liệu trong khoảng thời gian đã chọn." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[900px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Campus</th>
                <th className={`${thClass} text-right`}>Tổng chuyến</th>
                <th className={`${thClass} text-right`}>Chờ host</th>
                <th className={`${thClass} text-right`}>Chuẩn bị</th>
                <th className={`${thClass} text-right`}>Đang tiếp</th>
                <th className={`${thClass} text-right`}>Sau tiếp</th>
                <th className={thClass}>Đã đóng</th>
                <th className={`${thClass} text-right`}>Quá hạn</th>
                <th className={`${thClass} text-right`}>Khách</th>
                <th className={`${thClass} text-right`}>Feedback TB</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((c) => {
                const closedPct = c.totalInstances > 0 ? Math.round((c.closed / c.totalInstances) * 100) : 0;
                return (
                  <tr key={c.campusId} className="hover:bg-blue-50/40 transition-colors">
                    <td className={`${tdClass} font-bold text-slate-800`}>{c.campusName}</td>
                    <td className={`${tdClass} text-right font-bold text-slate-800`}>{fmt.formatNumber(c.totalInstances)}</td>
                    <td className={`${tdClass} text-right`}>{fmt.formatNumber(c.waitingHostAssignment)}</td>
                    <td className={`${tdClass} text-right`}>{fmt.formatNumber(c.assigned + c.beforeVisit)}</td>
                    <td className={`${tdClass} text-right`}>{fmt.formatNumber(c.duringVisit)}</td>
                    <td className={`${tdClass} text-right`}>{fmt.formatNumber(c.afterVisit)}</td>
                    <td className={tdClass}>
                      <div className="flex items-center gap-2 min-w-[110px]">
                        <div className="flex-1 h-1.5 bg-slate-100 rounded-full overflow-hidden">
                          <div className="h-full rounded-full" style={{ width: `${closedPct}%`, background: CHART_GREEN }} />
                        </div>
                        <span className="text-xs font-semibold text-slate-500 w-14">{c.closed} ({closedPct}%)</span>
                      </div>
                    </td>
                    <td className={`${tdClass} text-right font-bold ${c.overdueCloseCount > 0 ? 'text-red-600' : 'text-slate-400'}`}>
                      {fmt.formatNumber(c.overdueCloseCount)}
                    </td>
                    <td className={`${tdClass} text-right`}>{fmt.formatNumber(c.guestCount)}</td>
                    <td className={`${tdClass} text-right font-semibold ${c.averageFeedbackRating != null && c.averageFeedbackRating < 3 ? 'text-red-600' : 'text-slate-700'}`}>
                      {fmt.formatRating(c.averageFeedbackRating)}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ───────────────────────────── Pending multi-campus requests ─────────────────────────────

function PendingRequestsTable({ data, onOpen }: { data: HoReportOverview; onOpen: () => void }) {
  const rows = data.multiCampusPendingRequests;
  return (
    <div id="pending-requests" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 py-3 border-b border-slate-100 flex items-center justify-between gap-2 flex-wrap">
        <h3 className="text-sm font-bold text-slate-800">
          Đơn liên cơ sở cần HO xử lý
          {data.multiCampusPendingTotal > rows.length && (
            <span className="text-slate-400 font-medium"> · hiển thị {rows.length}/{data.multiCampusPendingTotal}</span>
          )}
        </h3>
        {data.multiCampusPendingTotal > 0 && (
          <button onClick={onOpen} className="flex items-center gap-1 text-xs font-bold text-[#004c91] hover:underline cursor-pointer">
            Mở Quản lý tiếp khách <ArrowRight className="w-3.5 h-3.5" />
          </button>
        )}
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Không có đơn liên cơ sở đang chờ xử lý." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[980px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Mã đơn</th>
                <th className={thClass}>Tên đoàn</th>
                <th className={thClass}>Tổ chức</th>
                <th className={`${thClass} text-right`}>Campus</th>
                <th className={`${thClass} text-right`}>Khách</th>
                <th className={thClass}>Ngày thăm</th>
                <th className={thClass}>Thời gian chờ</th>
                <th className={thClass}>Trạng thái</th>
                <th className={thClass}>Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {rows.map((r) => (
                <tr key={r.requestId} className="hover:bg-blue-50/40 transition-colors">
                  <td className={`${tdClass} font-bold text-[#004c91]`}>{r.requestCode}</td>
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[220px] truncate`} title={r.delegationName}>{r.delegationName}</td>
                  <td className={`${tdClass} max-w-[200px] truncate`} title={r.organizationName}>{r.organizationName}</td>
                  <td className={`${tdClass} text-right`}>{r.requestedCampusCount}</td>
                  <td className={`${tdClass} text-right`}>{r.guestCount}</td>
                  <td className={tdClass}>{fmt.formatDate(r.plannedStartAt)} – {fmt.formatDate(r.plannedEndAt)}</td>
                  <td className={`${tdClass} font-semibold ${r.waitingHours >= 48 ? 'text-red-600' : 'text-amber-600'}`}>
                    {fmt.formatWaitingHours(r.waitingHours)}
                  </td>
                  <td className={tdClass}>
                    <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${fmt.statusBadgeClass(r.status)}`}>
                      {fmt.requestStatusLabel(r.status)}
                    </span>
                  </td>
                  <td className={tdClass}>
                    <button onClick={onOpen} className="text-xs font-bold text-[#004c91] hover:underline cursor-pointer">
                      Xem chi tiết
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

function CloseReadinessTable({ data, onOpen }: { data: HoReportOverview; onOpen: (visitInstanceId: number) => void }) {
  const rows = data.closeReadiness;
  return (
    <div id="close-readiness" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 py-3 border-b border-slate-100">
        <h3 className="text-sm font-bold text-slate-800">
          Hồ sơ sau tiếp khách cần hoàn tất
          {data.closeReadinessTotal > rows.length && (
            <span className="text-slate-400 font-medium"> · hiển thị {rows.length}/{data.closeReadinessTotal}</span>
          )}
        </h3>
      </div>
      {rows.length === 0 ? (
        <SectionEmpty text="Không có hồ sơ sau tiếp khách cần hoàn tất." />
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[1000px]">
            <thead>
              <tr className="bg-slate-50 border-b border-slate-100">
                <th className={thClass}>Đoàn</th>
                <th className={thClass}>Campus</th>
                <th className={thClass}>Host</th>
                <th className={thClass}>Kết thúc</th>
                <th className={thClass}>Logistics</th>
                <th className={thClass}>Biên bản</th>
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
                  <td className={`${tdClass} font-semibold text-slate-800 max-w-[220px] truncate`} title={`${r.requestCode} · ${r.delegationName}`}>
                    {r.delegationName}
                  </td>
                  <td className={tdClass}>{r.campusName}</td>
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

// ───────────────────────────── Partner engagement ─────────────────────────────

function PartnerStat({ label, value, warn, good }: { label: string; value: number; warn?: boolean; good?: boolean }) {
  return (
    <div className="border border-slate-100 rounded-lg px-3 py-2.5">
      <p className="text-[11px] font-bold text-slate-400 uppercase truncate">{label}</p>
      <p className={`text-lg font-black ${warn && value > 0 ? 'text-amber-600' : good ? 'text-emerald-600' : 'text-slate-800'}`}>
        {fmt.formatNumber(value)}
      </p>
    </div>
  );
}

function PartnerEngagementSection({ data, onOpen }: { data: HoReportOverview; onOpen: () => void }) {
  const ps = data.partnerSummary;
  const hasAny = ps.totalPartners > 0 || ps.pendingApprovalPartners > 0 || ps.topPartners.length > 0;
  const maxTypeCount = Math.max(1, ...ps.partnersByType.map((t) => t.count));

  return (
    <div id="partners" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 py-3 border-b border-slate-100 flex items-center justify-between gap-2 flex-wrap">
        <div>
          <h3 className="text-sm font-bold text-slate-800">Đối tác (Partner)</h3>
          <p className="text-xs text-slate-400 font-medium">Mạng lưới partner toàn hệ thống và mức độ gắn kết trong kỳ báo cáo</p>
        </div>
        <button onClick={onOpen} className="flex items-center gap-1 text-xs font-bold text-[#004c91] hover:underline cursor-pointer">
          Mở Quản lý partner <ArrowRight className="w-3.5 h-3.5" />
        </button>
      </div>

      {!hasAny ? (
        <SectionEmpty text="Chưa có partner nào trong bộ lọc hiện tại." />
      ) : (
        <div className="p-4 space-y-4">
          {/* Mini stats */}
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
            <PartnerStat label="Tổng partner" value={ps.totalPartners} />
            <PartnerStat label="Đang hợp tác" value={ps.activePartners} good />
            <PartnerStat label="Hồ sơ chờ duyệt" value={ps.pendingApprovalPartners} warn />
            <PartnerStat label="Mới trong kỳ" value={ps.newPartnersInPeriod} />
            <PartnerStat label="Chuyến có partner" value={ps.visitsWithPartner} />
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
            {/* Phân bổ theo loại */}
            <div>
              <p className="text-[11px] font-bold text-slate-400 uppercase mb-1.5">Phân bổ theo loại</p>
              {ps.partnersByType.length === 0 ? (
                <p className="text-xs text-slate-400">Chưa có partner được duyệt.</p>
              ) : (
                <div className="space-y-1.5">
                  {ps.partnersByType.map((t) => (
                    <div key={t.partnerType} className="flex items-center gap-2 text-xs">
                      <span className="w-24 truncate font-semibold text-slate-600">{partnerTypeLabel(t.partnerType)}</span>
                      <div className="flex-1 h-1.5 bg-slate-100 rounded-full overflow-hidden">
                        <div className="h-full rounded-full" style={{ width: `${(t.count / maxTypeCount) * 100}%`, background: CHART_BLUE }} />
                      </div>
                      <span className="font-bold text-slate-700 w-8 text-right">{t.count}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Partner theo campus */}
            <div className="lg:col-span-2">
              <p className="text-[11px] font-bold text-slate-400 uppercase mb-1.5">Partner theo campus</p>
              {ps.partnersByCampus.length === 0 ? (
                <p className="text-xs text-slate-400">Chưa có dữ liệu theo campus.</p>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse min-w-[480px]">
                    <thead>
                      <tr className="bg-slate-50 border-b border-slate-100">
                        <th className={thClass}>Campus</th>
                        <th className={`${thClass} text-right`}>Đã duyệt</th>
                        <th className={`${thClass} text-right`}>Chờ duyệt</th>
                        <th className={`${thClass} text-right`}>Mới trong kỳ</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-100">
                      {ps.partnersByCampus.map((c) => (
                        <tr key={c.campusId} className="hover:bg-blue-50/40 transition-colors">
                          <td className={`${tdClass} font-semibold text-slate-800`}>{c.campusName}</td>
                          <td className={`${tdClass} text-right font-bold text-slate-800`}>{c.approvedCount}</td>
                          <td className={`${tdClass} text-right font-semibold ${c.pendingCount > 0 ? 'text-amber-600' : 'text-slate-400'}`}>{c.pendingCount}</td>
                          <td className={`${tdClass} text-right`}>{c.newInPeriod}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </div>

          {/* Top partner theo số chuyến */}
          <div>
            <p className="text-[11px] font-bold text-slate-400 uppercase mb-1.5">Top partner theo số chuyến trong kỳ</p>
            {ps.topPartners.length === 0 ? (
              <p className="text-xs text-slate-400">Chưa có chuyến nào gắn partner trong bộ lọc này.</p>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse min-w-[760px]">
                  <thead>
                    <tr className="bg-slate-50 border-b border-slate-100">
                      <th className={thClass}>Partner</th>
                      <th className={thClass}>Loại</th>
                      <th className={thClass}>Quốc gia</th>
                      <th className={thClass}>Campus quản lý</th>
                      <th className={thClass}>Hợp tác</th>
                      <th className={`${thClass} text-right`}>Chuyến</th>
                      <th className={`${thClass} text-right`}>Khách gắn</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {ps.topPartners.map((p) => (
                      <tr key={p.partnerId} className="hover:bg-blue-50/40 transition-colors">
                        <td className={`${tdClass} font-semibold text-slate-800 max-w-[240px] truncate`} title={p.name}>{p.name}</td>
                        <td className={tdClass}>{partnerTypeLabel(p.partnerType)}</td>
                        <td className={tdClass}>{p.country ?? '—'}</td>
                        <td className={tdClass}>{p.ownerCampusName}</td>
                        <td className={tdClass}>
                          <span className={`inline-block text-[11px] font-bold border rounded-full px-2 py-0.5 ${
                            p.cooperationStatus === 'ACTIVE'
                              ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                              : 'bg-slate-100 text-slate-500 border-slate-200'
                          }`}>
                            {cooperationLabel(p.cooperationStatus)}
                          </span>
                        </td>
                        <td className={`${tdClass} text-right font-bold text-[#004c91]`}>{p.visitCount}</td>
                        <td className={`${tdClass} text-right`}>{p.linkedGuestCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

// ───────────────────────────── Feedback & Content ─────────────────────────────

function FeedbackContentSection({ data }: { data: HoReportOverview }) {
  const [tab, setTab] = useState<'feedback' | 'content'>('feedback');
  const fb = data.feedbackSummary;
  const ce = data.contentAndEmailSummary;

  return (
    <div id="feedback-content" className="bg-white border border-slate-200 rounded-xl overflow-hidden scroll-mt-4">
      <div className="px-4 pt-3 border-b border-slate-100 flex items-center gap-1">
        {([
          { key: 'feedback' as const, label: 'Feedback' },
          { key: 'content' as const, label: 'News & Email' },
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

      {tab === 'feedback' && (
        <div className="p-4">
          {fb.totalFeedbacks === 0 ? (
            <SectionEmpty text="Chưa có feedback trong bộ lọc hiện tại." bare />
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-5">
              <div>
                <div className="flex items-end gap-4 mb-3">
                  <div>
                    <p className="text-[11px] font-bold text-slate-400 uppercase">Điểm trung bình</p>
                    <p className="text-3xl font-black text-slate-800">{fmt.formatRating(fb.averageRating)}<span className="text-base text-slate-400 font-bold">/5</span></p>
                  </div>
                  <div>
                    <p className="text-[11px] font-bold text-slate-400 uppercase">Tổng feedback</p>
                    <p className="text-xl font-black text-slate-800">{fmt.formatNumber(fb.totalFeedbacks)}</p>
                  </div>
                  <div>
                    <p className="text-[11px] font-bold text-slate-400 uppercase">Thấp (≤2)</p>
                    <p className={`text-xl font-black ${fb.lowFeedbackCount > 0 ? 'text-red-600' : 'text-slate-800'}`}>{fmt.formatNumber(fb.lowFeedbackCount)}</p>
                  </div>
                </div>
                <p className="text-[11px] font-bold text-slate-400 uppercase mb-1.5">Điểm theo cơ sở</p>
                <div className="space-y-1.5">
                  {fb.ratingByCampus.map((c) => (
                    <div key={c.campusId} className="flex items-center gap-2 text-xs">
                      <span className="w-24 truncate font-semibold text-slate-600">{c.campusName}</span>
                      <div className="flex-1 h-1.5 bg-slate-100 rounded-full overflow-hidden">
                        <div className="h-full rounded-full" style={{ width: `${(c.averageRating / 5) * 100}%`, background: CHART_BLUE }} />
                      </div>
                      <span className="font-bold text-slate-700 w-8 text-right">{fmt.formatRating(c.averageRating)}</span>
                      <span className="text-slate-400 w-10 text-right">({c.feedbackCount})</span>
                    </div>
                  ))}
                  {fb.ratingByCampus.length === 0 && <p className="text-xs text-slate-400">Chưa có dữ liệu theo cơ sở.</p>}
                </div>
              </div>

              <RatedVisitList title="Đánh giá cao nhất" visits={fb.topRatedVisits} tone="good" />
              <RatedVisitList title="Đánh giá thấp nhất" visits={fb.lowRatedVisits} tone="bad" />
            </div>
          )}
        </div>
      )}

      {tab === 'content' && (
        <div className="p-4 grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
          <ContentStat label="News đã đăng" value={fmt.formatNumber(ce.publishedNewsCount)} />
          <ContentStat label="News chờ duyệt" value={fmt.formatNumber(ce.pendingNewsCount)} warn={ce.pendingNewsCount > 0} />
          <ContentStat label="Instance thiếu news" value={fmt.formatNumber(ce.instancesMissingNewsCount)} warn={ce.instancesMissingNewsCount > 0} />
          <ContentStat label="Email gửi thành công" value={fmt.formatNumber(ce.emailSentCount)} />
          <ContentStat label="Tỷ lệ gửi thành công" value={ce.emailDeliveredRate != null ? fmt.formatPercent(ce.emailDeliveredRate) : '—'} sub={`${fmt.formatNumber(ce.emailFailedCount)} email lỗi`} danger={ce.emailFailedCount > 0} />
          <ContentStat label="Token đã phản hồi" value={fmt.formatNumber(ce.actionTokenRespondedCount)} />
          <ContentStat label="Token đang chờ" value={fmt.formatNumber(ce.actionTokenPendingCount)} />
          <ContentStat label="Token hết hạn" value={fmt.formatNumber(ce.actionTokenExpiredCount)} warn={ce.actionTokenExpiredCount > 0} />
        </div>
      )}
    </div>
  );
}

function RatedVisitList({ title, visits, tone }: { title: string; visits: HoReportOverview['feedbackSummary']['topRatedVisits']; tone: 'good' | 'bad' }) {
  return (
    <div>
      <p className="text-[11px] font-bold text-slate-400 uppercase mb-1.5">{title}</p>
      {visits.length === 0 ? (
        <p className="text-xs text-slate-400">Chưa có dữ liệu.</p>
      ) : (
        <div className="space-y-1">
          {visits.map((v) => (
            <div key={`${title}-${v.visitInstanceId}`} className="flex items-center gap-2 text-xs border border-slate-100 rounded-lg px-2.5 py-1.5">
              <div className="flex-1 min-w-0">
                <p className="font-semibold text-slate-700 truncate" title={v.delegationName}>{v.delegationName}</p>
                <p className="text-slate-400">{v.campusName} · {fmt.formatDate(v.plannedStartAt)} · {v.feedbackCount} feedback</p>
              </div>
              <span className={`font-black text-sm ${tone === 'good' ? 'text-emerald-600' : 'text-red-600'}`}>
                {fmt.formatRating(v.averageRating)}
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function ContentStat({ label, value, sub, warn, danger }: { label: string; value: string; sub?: string; warn?: boolean; danger?: boolean }) {
  return (
    <div className="border border-slate-100 rounded-lg px-3 py-2.5">
      <p className="text-[11px] font-bold text-slate-400 uppercase truncate">{label}</p>
      <p className={`text-lg font-black ${danger ? 'text-red-600' : warn ? 'text-amber-600' : 'text-slate-800'}`}>{value}</p>
      {sub && <p className="text-[11px] text-slate-400">{sub}</p>}
    </div>
  );
}

function SectionEmpty({ text, bare }: { text: string; bare?: boolean }) {
  return (
    <div className={`flex flex-col items-center justify-center text-slate-400 ${bare ? 'py-8' : 'py-10'}`}>
      <Info className="w-5 h-5 mb-1.5" />
      <p className="text-sm font-medium">{text}</p>
    </div>
  );
}
