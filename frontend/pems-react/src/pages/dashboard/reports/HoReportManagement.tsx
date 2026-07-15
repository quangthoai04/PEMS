/**
 * Trang HoReportManagement — Báo cáo hệ thống của Head Office tại /dashboard/reports.
 * Format 3 phần như báo cáo Staff Leader: (1) bộ lọc thời gian chung cho cả trang,
 * (2) tổng quan toàn hệ thống + biểu đồ tiến trình tiếp khách theo TỪNG campus (số
 * đường = số campus thực tế, không hard-code) + bảng campus kèm gửi email báo cáo,
 * (3) xu hướng đối tác + bảng đối tác xếp theo lượt tham quan.
 * Dữ liệu từ GET /reports/ho-report-v2.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertTriangle, Building2, CalendarRange, CheckCircle2, ChevronDown, ChevronLeft, ChevronRight,
  ChevronUp, Download, FileText, Globe2, Loader2, RefreshCw, Send, Star, Users, XCircle,
} from 'lucide-react';
import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { reportsApi } from '../../../features/reports/api/reportsApi';
import type {
  HoReportV2, HoV2CampusRow, HoV2Filters, HoV2Preset,
} from '../../../features/reports/types/hoReportsV2.types';

// Palette series theo campus — lặp vòng nếu campus nhiều hơn số màu (đã validate CVD/contrast).
const CAMPUS_COLORS = ['#1e6fc0', '#d95f18', '#0a8a44', '#7a5cc4', '#b0257c', '#0e7f8a', '#8a6d00'];
const CHART_BLUE = '#1e6fc0';
const CHART_GREEN = '#0a8a44';

const PRESETS: { value: HoV2Preset; label: string }[] = [
  { value: 'THIS_MONTH', label: 'Tháng này' },
  { value: 'THIS_QUARTER', label: 'Quý này' },
  { value: 'THIS_YEAR', label: 'Năm nay' },
  { value: 'CUSTOM', label: 'Tùy chỉnh' },
];

const GRANULARITY_LABELS: Record<string, string> = {
  YEAR: 'năm', MONTH: 'tháng', WEEK: 'tuần', DAY: 'ngày', HOUR: 'giờ',
};

const PARTNER_TYPE_LABELS: Record<string, string> = {
  UNIVERSITY: 'Trường ĐH', COMPANY: 'Doanh nghiệp', GOVERNMENT: 'Chính phủ', NGO: 'NGO', OTHER: 'Khác',
};

const thClass = 'px-3 py-2.5 text-[11px] font-bold text-slate-400 uppercase tracking-wide whitespace-nowrap text-left';
const tdClass = 'px-3 py-2.5 text-sm text-slate-600';

const fmtDate = (iso: string) => (iso ? `${iso.slice(8, 10)}/${iso.slice(5, 7)}/${iso.slice(0, 4)}` : '—');

/** Ô thông số compact — nhiều phần chung 1 trang nên hạn chế khung/ô to. */
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

export function HoReportManagement() {
  // ── Phần 1: bộ lọc thời gian (chung cho cả trang) ──
  const [filters, setFilters] = useState<HoV2Filters>({ preset: 'THIS_YEAR', fromDate: '', toDate: '' });
  const [data, setData] = useState<HoReportV2 | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const fetchReport = useCallback(async (f: HoV2Filters) => {
    setLoading(true);
    setError(null);
    try {
      const res = await reportsApi.getHoReportV2(f);
      setData(res);
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể tải báo cáo. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetchReport(filters); /* eslint-disable-next-line react-hooks/exhaustive-deps */ }, []);

  const [openSections, setOpenSections] = useState({ overview: true, partners: true });
  const toggleSection = (key: keyof typeof openSections) =>
    setOpenSections((s) => ({ ...s, [key]: !s[key] }));

  // ── Xuất báo cáo (PDF/Excel/CSV) — chọn phần ──
  const [exportMenuOpen, setExportMenuOpen] = useState(false);
  const [exportSections, setExportSections] = useState<string[]>(['OVERVIEW', 'PARTNERS']);
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
      const file = await reportsApi.exportHoReportV2({
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

  // ── Gửi email báo cáo từng campus ──
  const [campusNotes, setCampusNotes] = useState<Record<number, string>>({});
  const [sendingCampusId, setSendingCampusId] = useState<number | null>(null);

  const sendCampusReport = async (row: HoV2CampusRow) => {
    setSendingCampusId(row.campusId);
    try {
      const res = await reportsApi.sendHoCampusReport({
        campusId: row.campusId,
        fromDate: data?.fromDate,
        toDate: data?.toDate,
        note: campusNotes[row.campusId]?.trim() || undefined,
      });
      toast.success(res.message || 'Đã gửi báo cáo.');
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi báo cáo thất bại.');
    } finally {
      setSendingCampusId(null);
    }
  };

  // Biểu đồ đa campus: chuyển trend về dạng phẳng { monthLabel, [tên campus]: số đoàn }.
  const campusTrendRows = useMemo(() => {
    if (!data) return [];
    return data.overview.trend.map((t) => {
      const row: Record<string, string | number> = { monthLabel: t.monthLabel };
      data.overview.campuses.forEach((c) => {
        row[c.name] = t.byCampus[String(c.campusId)] ?? 0;
      });
      return row;
    });
  }, [data]);

  // ── Phân trang bảng đối tác ──
  const [partnerPageSize, setPartnerPageSize] = useState(5);
  const [partnerPage, setPartnerPage] = useState(1);
  const partnerRows = data?.partners.rows ?? [];
  const partnerTotalPages = Math.max(1, Math.ceil(partnerRows.length / partnerPageSize));
  const partnerPageRows = useMemo(() => {
    const start = (partnerPage - 1) * partnerPageSize;
    return partnerRows.slice(start, start + partnerPageSize);
  }, [partnerRows, partnerPage, partnerPageSize]);
  useEffect(() => { setPartnerPage(1); }, [partnerPageSize, data]);

  const o = data?.overview;
  const p = data?.partners;

  return (
    <div className="w-full space-y-8 pb-16 animate-in fade-in duration-300">
      {/* ── Header + nút xuất báo cáo ── */}
      <div className="border-b border-gray-100 pb-4 flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91]">Báo cáo hệ thống</h1>
          <p className="text-slate-500 mt-2">
            {data ? `Toàn hệ thống · Kỳ ${fmtDate(data.fromDate)} – ${fmtDate(data.toDate)}` : 'Báo cáo toàn hệ thống của Head Office.'}
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
                {[
                  { key: 'OVERVIEW', label: 'Phần 2 · Tổng quan hệ thống' },
                  { key: 'PARTNERS', label: 'Phần 3 · Đối tác' },
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
                    onChange={() => setExportSections(exportSections.length === 2 ? [] : ['OVERVIEW', 'PARTNERS'])}
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

      {/* ── Phần 1: bộ lọc thời gian ── */}
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
          onClick={() => fetchReport(filters)}
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
          <p className="text-sm font-medium">Đang tổng hợp báo cáo hệ thống...</p>
        </div>
      ) : data && (
        <>
          {/* ═══ 2 · Tổng quan hệ thống ═══ */}
          <Section
            index={2}
            title="Tổng quan hệ thống"
            subtitle="Số liệu tiếp khách của toàn bộ các campus trong kỳ."
            open={openSections.overview}
            onToggle={() => toggleSection('overview')}
          >
            <div className="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-5 gap-3">
              <StatTile label="Số campus" value={o!.campusCount} tone="slate" icon={<Building2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Tổng đoàn khách" value={o!.totalVisits} sub={`${o!.totalGuests} khách`} tone="blue" icon={<Users className="w-4 h-4 opacity-60" />} />
              <StatTile label="Tổng đối tác" value={o!.totalPartners} tone="violet" icon={<Globe2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Đơn liên cơ sở" value={o!.multiCampusRequests} tone="amber" />
              <StatTile label="Đơn một cơ sở" value={o!.singleCampusRequests} tone="amber" />
              <StatTile label="Hoàn thành" value={o!.completed} sub="Đã đóng đoàn" tone="green" icon={<CheckCircle2 className="w-4 h-4 opacity-60" />} />
              <StatTile label="Bị hủy" value={o!.cancelled} tone="red" />
              <StatTile label="Từ chối" value={o!.rejected} tone="red" icon={<XCircle className="w-4 h-4 opacity-60" />} />
              <StatTile
                label="Feedback TB"
                value={o!.feedbackAverage != null ? `${o!.feedbackAverage.toFixed(1)}★` : '—'}
                sub={`${o!.feedbackCount} lượt đánh giá · tất cả campus`}
                tone="violet"
                icon={<Star className="w-4 h-4 opacity-60" />}
              />
            </div>

            {/* Biểu đồ tiến trình tiếp khách theo từng campus (số đường = số campus thực tế) */}
            <div>
              <h3 className="text-sm font-bold text-slate-700 mb-1">Tiến trình tiếp khách theo campus</h3>
              <p className="text-xs text-slate-400 mb-3">
                Số đoàn khách của từng campus theo {GRANULARITY_LABELS[o!.trendGranularity] ?? 'tháng'} — mốc trục thời gian tự đổi theo khoảng lọc.
              </p>
              <div className="h-80">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={campusTrendRows} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="monthLabel" tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <YAxis allowDecimals={false} tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <Tooltip labelStyle={{ fontWeight: 700 }} contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    {o!.campuses.map((c, idx) => (
                      <Line
                        key={c.campusId}
                        type="monotone"
                        dataKey={c.name}
                        name={c.name}
                        stroke={CAMPUS_COLORS[idx % CAMPUS_COLORS.length]}
                        strokeWidth={2.2}
                        dot={{ r: 2.5 }}
                      />
                    ))}
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>

            {/* Bảng campus + gửi email báo cáo từng cơ sở */}
            <div className="overflow-x-auto rounded-2xl border border-slate-200">
              <table className="w-full text-left border-collapse">
                <thead className="bg-slate-50">
                  <tr>
                    <th className={thClass}>STT</th>
                    <th className={thClass}>Tên campus</th>
                    <th className={thClass}>Tổng số đoàn khách</th>
                    <th className={thClass}>Tổng đối tác</th>
                    <th className={thClass}>Feedback</th>
                    <th className={thClass}>Ghi chú</th>
                    <th className={thClass}></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {o!.campusRows.map((row, idx) => {
                    const lowFeedback = row.feedbackAverage != null && row.feedbackAverage < 2;
                    return (
                      <tr key={row.campusId} className={lowFeedback ? 'bg-rose-50/50' : idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                        <td className={`${tdClass} whitespace-nowrap`}>{idx + 1}</td>
                        <td className={`${tdClass} font-semibold text-slate-800`}>{row.name}</td>
                        <td className={`${tdClass} whitespace-nowrap`}>{row.totalVisits}</td>
                        <td className={`${tdClass} whitespace-nowrap`}>{row.totalPartners}</td>
                        <td className={`${tdClass} whitespace-nowrap`}>
                          {row.feedbackAverage != null ? (
                            <span className={`inline-flex items-center gap-1 font-bold ${lowFeedback ? 'text-rose-600' : 'text-slate-700'}`}>
                              {row.feedbackAverage.toFixed(1)}★
                              <span className="text-[11px] font-normal text-slate-400">({row.feedbackCount} lượt)</span>
                              {lowFeedback && <AlertTriangle className="w-3.5 h-3.5 text-rose-500" aria-label="Feedback dưới 2 sao" />}
                            </span>
                          ) : <span className="text-slate-400">—</span>}
                        </td>
                        <td className={tdClass}>
                          <input
                            type="text"
                            value={campusNotes[row.campusId] ?? ''}
                            onChange={(e) => setCampusNotes((s) => ({ ...s, [row.campusId]: e.target.value }))}
                            placeholder="Ghi chú..."
                            className="w-44 border border-slate-200 rounded-lg px-2 py-1.5 text-xs outline-none focus:border-[#004c91]"
                          />
                        </td>
                        <td className={`${tdClass} whitespace-nowrap`}>
                          <button
                            type="button"
                            onClick={() => sendCampusReport(row)}
                            disabled={sendingCampusId === row.campusId}
                            title={`Gửi báo cáo qua email cho Staff Leader ${row.name}`}
                            className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-blue-50 hover:bg-blue-100 text-[#004c91] text-xs font-bold rounded-lg border border-blue-200 transition-colors disabled:opacity-50 cursor-pointer"
                          >
                            {sendingCampusId === row.campusId ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Send className="w-3.5 h-3.5" />}
                            Gửi
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          </Section>

          {/* ═══ 3 · Đối tác ═══ */}
          <Section
            index={3}
            title="Đối tác"
            subtitle="Xu hướng hợp tác đối tác toàn hệ thống và bảng đối tác xếp theo số lượt tham quan."
            open={openSections.partners}
            onToggle={() => toggleSection('partners')}
          >
            <div>
              <h3 className="text-sm font-bold text-slate-700 mb-1">Xu hướng đối tác</h3>
              <p className="text-xs text-slate-400 mb-3">
                Lũy kế đối tác đã duyệt và số chuyến thăm gắn với đối tác theo {GRANULARITY_LABELS[p!.trendGranularity] ?? 'tháng'}.
              </p>
              <div className="h-72">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={p!.trend} margin={{ top: 8, right: 16, bottom: 0, left: -12 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                    <XAxis dataKey="monthLabel" tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <YAxis allowDecimals={false} tick={{ fontSize: 11 }} stroke="#94a3b8" />
                    <Tooltip labelStyle={{ fontWeight: 700 }} contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
                    <Legend wrapperStyle={{ fontSize: 12 }} />
                    <Line type="monotone" dataKey="cumulativePartners" name="Lũy kế đối tác" stroke={CHART_BLUE} strokeWidth={2.5} dot={{ r: 3 }} />
                    <Line type="monotone" dataKey="visitsWithPartner" name="Chuyến thăm gắn đối tác" stroke={CHART_GREEN} strokeWidth={2.5} dot={{ r: 3 }} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="overflow-x-auto rounded-2xl border border-slate-200">
              <table className="w-full text-left border-collapse">
                <thead className="bg-slate-50">
                  <tr>
                    <th className={thClass}>STT</th>
                    <th className={thClass}>Tên đối tác</th>
                    <th className={thClass}>Đất nước</th>
                    <th className={thClass}>Số lần tham quan</th>
                    <th className={thClass}>Feedback</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {partnerRows.length === 0 && (
                    <tr><td colSpan={5} className="px-4 py-8 text-center text-sm text-slate-400">Chưa có đối tác nào gắn với chuyến thăm trong kỳ.</td></tr>
                  )}
                  {partnerPageRows.map((row, idx) => (
                    <tr key={row.partnerId} className={idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                      <td className={`${tdClass} whitespace-nowrap`}>{(partnerPage - 1) * partnerPageSize + idx + 1}</td>
                      <td className={`${tdClass} font-semibold text-slate-800`}>
                        {row.name}
                        <span className="block text-[11px] font-normal text-slate-400">{PARTNER_TYPE_LABELS[row.partnerType] ?? row.partnerType}</span>
                      </td>
                      <td className={`${tdClass} whitespace-nowrap`}>{row.country || '—'}</td>
                      <td className={`${tdClass} whitespace-nowrap font-bold text-slate-700`}>{row.visitCount}</td>
                      <td className={`${tdClass} whitespace-nowrap`}>
                        {row.feedbackAverage != null
                          ? <span className="font-bold text-slate-700">{row.feedbackAverage.toFixed(1)}★ <span className="text-[11px] font-normal text-slate-400">({row.feedbackCount} lượt)</span></span>
                          : <span className="text-slate-400">—</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* Phân trang: chọn số dòng/trang động (5/10/20/50) */}
            {partnerRows.length > 0 && (
              <div className="flex flex-wrap items-center justify-between gap-3 px-1">
                <div className="flex items-center gap-2 text-xs text-slate-500">
                  <span>Hiển thị</span>
                  <select
                    value={partnerPageSize}
                    onChange={(e) => setPartnerPageSize(Number(e.target.value))}
                    className="border border-slate-200 rounded-lg px-2 py-1 text-xs font-bold text-slate-700 outline-none focus:border-[#004c91] cursor-pointer"
                  >
                    {[5, 10, 20, 50].map((n) => (
                      <option key={n} value={n}>{n}</option>
                    ))}
                  </select>
                  <span>/ trang · Tổng {partnerRows.length} đối tác</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <button
                    type="button"
                    onClick={() => setPartnerPage((p2) => Math.max(1, p2 - 1))}
                    disabled={partnerPage === 1}
                    className="w-8 h-8 rounded-lg border border-slate-200 flex items-center justify-center text-slate-500 hover:bg-slate-50 disabled:opacity-40 transition-colors cursor-pointer"
                  >
                    <ChevronLeft className="w-4 h-4" />
                  </button>
                  <span className="text-xs font-bold text-slate-600 px-1">Trang {partnerPage}/{partnerTotalPages}</span>
                  <button
                    type="button"
                    onClick={() => setPartnerPage((p2) => Math.min(partnerTotalPages, p2 + 1))}
                    disabled={partnerPage === partnerTotalPages}
                    className="w-8 h-8 rounded-lg border border-slate-200 flex items-center justify-center text-slate-500 hover:bg-slate-50 disabled:opacity-40 transition-colors cursor-pointer"
                  >
                    <ChevronRight className="w-4 h-4" />
                  </button>
                </div>
              </div>
            )}
          </Section>
        </>
      )}
    </div>
  );
}
