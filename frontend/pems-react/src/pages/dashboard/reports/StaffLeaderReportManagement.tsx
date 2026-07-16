/**
 * Trang StaffLeaderReportManagement — Báo cáo campus của Staff Leader tại /dashboard/reports.
 * Bố cục 3 phần: (1) Đoàn tiếp khách + tiến độ hợp tác đối tác, (2) Nhân sự IC + Student,
 * (3) Các phòng ban khác (kèm xuất/gửi hóa đơn). Bộ lọc duy nhất là khoảng thời gian,
 * dùng chung cho cả 3 phần. Dữ liệu từ GET /reports/staff-leader-report-v2.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import toast from 'react-hot-toast';
import {
  AlertTriangle, Building2, CalendarRange, CheckCircle2, ChevronDown, ChevronUp, Download,
  FileText, Loader2, Mail, RefreshCw, Send, Star, Users, X, XCircle,
} from 'lucide-react';
import {
  CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts';
import { reportsApi } from '../../../features/reports/api/reportsApi';
import type {
  StaffLeaderInvoiceItem, StaffLeaderReportV2, StaffLeaderV2DepartmentRow,
  StaffLeaderV2Filters, StaffLeaderV2PersonnelRow, StaffLeaderV2Preset,
} from '../../../features/reports/types/staffLeaderReportsV2.types';
import { TaskHandoverModal } from '../departments/TaskHandoverModal';

// Palette chart đã validate CVD/contrast.
const CHART_BLUE = '#1e6fc0';
const CHART_GREEN = '#0a8a44';

const PRESETS: { value: StaffLeaderV2Preset; label: string }[] = [
  { value: 'THIS_MONTH', label: 'Tháng này' },
  { value: 'THIS_QUARTER', label: 'Quý này' },
  { value: 'THIS_YEAR', label: 'Năm nay' },
  { value: 'CUSTOM', label: 'Tùy chỉnh' },
];

const ITEM_TYPE_LABELS: Record<string, string> = {
  ROOM: 'Phòng họp', TRANSPORT: 'Xe / di chuyển', MEAL: 'Ẩm thực', EQUIPMENT: 'Thiết bị',
  BANNER: 'Băng rôn', LED: 'LED', OTHER: 'Khác',
};

const thClass = 'px-3 py-2.5 text-[11px] font-bold text-slate-400 uppercase tracking-wide whitespace-nowrap text-left';
const tdClass = 'px-3 py-2.5 text-sm text-slate-600';

const vnMoney = (v: number) => `${v.toLocaleString('vi-VN')} ₫`;
const fmtDate = (iso: string) => (iso ? `${iso.slice(8, 10)}/${iso.slice(5, 7)}/${iso.slice(0, 4)}` : '—');
const fmtDateTime = (iso: string) => (iso ? `${iso.slice(11, 16)} ${fmtDate(iso)}` : '—');

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

  // Mỗi phần đóng/mở độc lập — mặc định mở cả 3.
  const [openSections, setOpenSections] = useState({ visits: true, personnel: true, departments: true });
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

  const personnelRows = useMemo(() => {
    const rows = data?.personnel.rows ?? [];
    if (roleFilter === 'STAFF') return rows.filter((r) => r.role !== 'STUDENT');
    if (roleFilter === 'STUDENT') return rows.filter((r) => r.role === 'STUDENT');
    return rows;
  }, [data, roleFilter]);

  const sendPersonnelReport = async (row: StaffLeaderV2PersonnelRow) => {
    setSendingUserId(row.userId);
    try {
      const res = await reportsApi.sendStaffLeaderPersonnelReport({
        userId: row.userId,
        fromDate: data?.fromDate,
        toDate: data?.toDate,
        note: personnelNotes[row.userId]?.trim() || undefined,
      });
      toast.success(res.message || 'Đã gửi báo cáo.');
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi báo cáo thất bại.');
    } finally {
      setSendingUserId(null);
    }
  };

  // ── Phần 3: panel hóa đơn phòng ban ──
  const [invoiceDept, setInvoiceDept] = useState<StaffLeaderV2DepartmentRow | null>(null);
  const [invoiceRange, setInvoiceRange] = useState<{ fromDate: string; toDate: string }>({ fromDate: '', toDate: '' });
  const [invoiceItems, setInvoiceItems] = useState<StaffLeaderInvoiceItem[]>([]);
  const [invoiceLoading, setInvoiceLoading] = useState(false);
  const [invoiceLoaded, setInvoiceLoaded] = useState(false);
  const [prices, setPrices] = useState<Record<number, string>>({});
  const [invoiceSending, setInvoiceSending] = useState(false);
  const [viewItem, setViewItem] = useState<StaffLeaderInvoiceItem | null>(null);

  const openInvoicePanel = (dept: StaffLeaderV2DepartmentRow) => {
    setInvoiceDept(dept);
    setInvoiceRange({ fromDate: data?.fromDate ?? '', toDate: data?.toDate ?? '' });
    setInvoiceItems([]);
    setPrices({});
    setInvoiceLoaded(false);
  };

  const loadInvoiceItems = async () => {
    if (!invoiceDept) return;
    if (!invoiceRange.fromDate || !invoiceRange.toDate) {
      toast.error('Chọn khoảng ngày để lấy danh sách đơn.');
      return;
    }
    setInvoiceLoading(true);
    try {
      const items = await reportsApi.getStaffLeaderDeptInvoiceItems(
        invoiceDept.departmentId, invoiceRange.fromDate, invoiceRange.toDate);
      setInvoiceItems(items);
      setInvoiceLoaded(true);
      setPrices({});
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Không tải được danh sách đơn.');
    } finally {
      setInvoiceLoading(false);
    }
  };

  const priceOf = (id: number) => {
    const raw = (prices[id] ?? '').replace(/[^\d]/g, '');
    return raw ? Number(raw) : 0;
  };
  const lineTotal = (it: StaffLeaderInvoiceItem) => priceOf(it.logisticsItemId) * (it.quantity || 1);
  const grandTotal = invoiceItems.reduce((sum, it) => sum + lineTotal(it), 0);

  const sendInvoice = async () => {
    if (!invoiceDept) return;
    const items = invoiceItems
      .filter((it) => priceOf(it.logisticsItemId) > 0)
      .map((it) => ({ logisticsItemId: it.logisticsItemId, unitPrice: priceOf(it.logisticsItemId) }));
    if (items.length === 0) {
      toast.error('Nhập đơn giá cho ít nhất một đơn trước khi gửi.');
      return;
    }
    setInvoiceSending(true);
    try {
      const res = await reportsApi.sendStaffLeaderDeptInvoice(invoiceDept.departmentId, {
        fromDate: invoiceRange.fromDate,
        toDate: invoiceRange.toDate,
        items,
      });
      toast.success(res.message || 'Đã gửi hóa đơn.');
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi hóa đơn thất bại.');
    } finally {
      setInvoiceSending(false);
    }
  };

  // "Xuất hóa đơn (PDF)" mở hộp thoại IN (không tải file): dựng hẳn 1 cửa sổ mới chỉ chứa
  // HTML hóa đơn rồi in cửa sổ đó — không phụ thuộc CSS/layout của trang nên không bị in trắng.
  const exportInvoicePdf = () => {
    if (!invoiceDept || invoiceItems.length === 0) {
      toast.error('Chưa có đơn nào để xuất hóa đơn.');
      return;
    }
    const esc = (s: string | null | undefined) =>
      (s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    const rowsHtml = invoiceItems.map((it, idx) => `
      <tr>
        <td style="text-align:center">${idx + 1}</td>
        <td>${esc(it.title)}</td>
        <td>${esc(it.delegationName)}</td>
        <td style="text-align:center">${fmtDate(it.usageStartAt)}</td>
        <td style="text-align:center">${it.quantity}</td>
        <td style="text-align:right">${vnMoney(priceOf(it.logisticsItemId))}</td>
        <td style="text-align:right">${vnMoney(lineTotal(it))}</td>
      </tr>`).join('');
    const html = `<!doctype html><html><head><meta charset="utf-8" />
      <title>Hóa đơn hậu cần — ${esc(invoiceDept.name)}</title>
      <style>
        body { font-family: 'Segoe UI', Arial, sans-serif; color: #0f172a; padding: 28px; }
        .top { display: flex; justify-content: space-between; border-bottom: 1px solid #cbd5e1; padding-bottom: 12px; margin-bottom: 22px; font-size: 12px; }
        h2 { text-align: center; text-transform: uppercase; margin: 4px 0 2px; font-size: 20px; }
        .sub { text-align: center; font-size: 14px; margin-bottom: 20px; }
        table { width: 100%; border-collapse: collapse; font-size: 13px; }
        th, td { border: 1px solid #475569; padding: 6px 8px; }
        th { background: #f1f5f9; }
        .total td { font-weight: 700; }
        .sign { display: flex; justify-content: space-between; margin-top: 44px; text-align: center; font-size: 14px; }
        .sign div { width: 48%; }
        .sign .hint { font-size: 11px; color: #64748b; }
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
      <h2>HÓA ĐƠN HẬU CẦN TIẾP KHÁCH</h2>
      <p class="sub">Phòng ban: <b>${esc(invoiceDept.name)}</b> · Kỳ: <b>${fmtDate(invoiceRange.fromDate)} – ${fmtDate(invoiceRange.toDate)}</b></p>
      <table>
        <thead><tr><th>STT</th><th>Hạng mục</th><th>Đoàn khách</th><th>Ngày</th><th>SL</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead>
        <tbody>
          ${rowsHtml}
          <tr class="total"><td colspan="6" style="text-align:right">TỔNG CỘNG</td><td style="text-align:right">${vnMoney(grandTotal)}</td></tr>
        </tbody>
      </table>
      <div class="sign">
        <div><b>ĐẠI DIỆN VĂN PHÒNG IC</b><div class="hint">(Ký, ghi rõ họ tên)</div></div>
        <div><b>ĐẠI DIỆN PHÒNG BAN</b><div class="hint">(Ký, ghi rõ họ tên)</div></div>
      </div>
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
    toast.success('Đã mở bản in hóa đơn — chọn "Save as PDF" để lưu.');
  };

  // Biên bản đã ký giữa 2 bên — TaskHandoverModal (chỉ xem) nhận DTO PascalCase.
  const toHandoverDto = (it: StaffLeaderInvoiceItem) => ({
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

            {/* Bộ lọc vai trò */}
            <div className="flex items-center gap-2 mt-5 mb-3">
              {(['ALL', 'STAFF', 'STUDENT'] as const).map((rf) => (
                <button
                  key={rf}
                  type="button"
                  onClick={() => setRoleFilter(rf)}
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
            </div>

            <div className="overflow-x-auto rounded-2xl border border-slate-200">
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
                  {personnelRows.map((row, idx) => {
                    const lowFeedback = row.feedbackAverage != null && row.feedbackAverage < 2;
                    return (
                      <tr key={row.userId} className={lowFeedback ? 'bg-rose-50/50' : idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                        <td className={`${tdClass} whitespace-nowrap`}>{idx + 1}</td>
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

            <div className="overflow-x-auto rounded-2xl border border-slate-200 mt-5">
              <table className="w-full text-left border-collapse">
                <thead className="bg-slate-50">
                  <tr>
                    <th className={thClass}>STT</th>
                    <th className={thClass}>Tên phòng ban</th>
                    <th className={thClass}>Tổng đơn/thư yêu cầu</th>
                    <th className={thClass}>Hoàn thành</th>
                    <th className={thClass}>Từ chối</th>
                    <th className={thClass}>Feedback</th>
                    <th className={thClass}>Hành động</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {d!.rows.length === 0 && (
                    <tr><td colSpan={7} className="px-4 py-8 text-center text-sm text-slate-400">Không có phòng ban nào.</td></tr>
                  )}
                  {d!.rows.map((row, idx) => (
                    <tr key={row.departmentId} className={invoiceDept?.departmentId === row.departmentId ? 'bg-orange-50/60' : idx % 2 === 1 ? 'bg-slate-50/40' : ''}>
                      <td className={`${tdClass} whitespace-nowrap`}>{idx + 1}</td>
                      <td className={`${tdClass} font-semibold text-slate-800`}>{row.name}</td>
                      <td className={`${tdClass} whitespace-nowrap`}>{row.totalRequests}</td>
                      <td className={`${tdClass} whitespace-nowrap text-emerald-700 font-semibold`}>{row.completed}</td>
                      <td className={`${tdClass} whitespace-nowrap text-rose-600 font-semibold`}>{row.rejected}</td>
                      <td className={`${tdClass} whitespace-nowrap`}>
                        {row.feedbackAverage != null
                          ? <span className="font-bold text-slate-700">{row.feedbackAverage.toFixed(1)}★ <span className="text-[11px] font-normal text-slate-400">({row.feedbackCount})</span></span>
                          : <span className="text-slate-400">—</span>}
                      </td>
                      <td className={`${tdClass} whitespace-nowrap`}>
                        <button
                          type="button"
                          onClick={() => openInvoicePanel(row)}
                          className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-orange-50 hover:bg-orange-100 text-[#f37021] text-xs font-bold rounded-lg border border-orange-200 transition-colors cursor-pointer"
                        >
                          <FileText className="w-3.5 h-3.5" /> Xuất hóa đơn
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {/* ── Panel hóa đơn của phòng ban đã chọn ── */}
            {invoiceDept && (
              <div className="mt-5 rounded-2xl border-2 border-orange-200 bg-orange-50/40 overflow-hidden">
                <div className="px-5 py-3.5 bg-[#f37021] text-white flex items-center justify-between gap-3">
                  <h3 className="text-sm font-black flex items-center gap-2">
                    <FileText className="w-4 h-4" /> Hóa đơn hậu cần — {invoiceDept.name}
                  </h3>
                  <button type="button" onClick={() => setInvoiceDept(null)} className="p-1.5 hover:bg-white/10 rounded-full cursor-pointer">
                    <X className="w-4 h-4" />
                  </button>
                </div>

                <div className="p-5 space-y-4">
                  {/* Chọn khoảng ngày */}
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
                      {invoiceLoading ? <Loader2 className="w-3.5 h-3.5 animate-spin inline" /> : 'Tải danh sách đơn'}
                    </button>
                  </div>

                  {invoiceLoaded && invoiceItems.length === 0 && (
                    <p className="text-sm text-slate-500 py-4 text-center">Phòng ban chưa nhận đơn yêu cầu nào trong khoảng ngày này.</p>
                  )}

                  {invoiceItems.length > 0 && (
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
                              <th className={thClass}>Biên bản</th>
                              <th className={thClass}>Đơn giá (₫)</th>
                              <th className={thClass}>Thành tiền</th>
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
                                  <button
                                    type="button"
                                    onClick={() => setViewItem(it)}
                                    className="text-xs font-bold text-[#004c91] hover:underline cursor-pointer"
                                  >
                                    Xem chi tiết
                                  </button>
                                </td>
                                <td className={`${tdClass} whitespace-nowrap`}>
                                  <input
                                    type="text"
                                    inputMode="numeric"
                                    value={prices[it.logisticsItemId] ?? ''}
                                    onChange={(e) => setPrices((s) => ({ ...s, [it.logisticsItemId]: e.target.value.replace(/[^\d]/g, '') }))}
                                    placeholder="0"
                                    className="w-28 border border-slate-200 rounded-lg px-2 py-1.5 text-sm text-right outline-none focus:border-[#f37021]"
                                  />
                                </td>
                                <td className={`${tdClass} whitespace-nowrap font-bold text-slate-800 text-right`}>{vnMoney(lineTotal(it))}</td>
                              </tr>
                            ))}
                          </tbody>
                          <tfoot>
                            <tr className="bg-orange-50">
                              <td colSpan={7} className="px-3 py-3 text-right text-sm font-black text-slate-700">TỔNG TIỀN</td>
                              <td className="px-3 py-3 text-right text-base font-black text-[#c2410c] whitespace-nowrap">{vnMoney(grandTotal)}</td>
                            </tr>
                          </tfoot>
                        </table>
                      </div>

                      <div className="flex flex-wrap items-center justify-end gap-3">
                        <button
                          type="button"
                          onClick={exportInvoicePdf}
                          className="inline-flex items-center gap-1.5 px-4 py-2.5 bg-white border border-slate-300 text-slate-700 text-xs font-black rounded-xl hover:bg-slate-50 transition-colors cursor-pointer"
                        >
                          <Download className="w-4 h-4" /> Xuất hóa đơn (PDF)
                        </button>
                        <button
                          type="button"
                          onClick={sendInvoice}
                          disabled={invoiceSending}
                          className="inline-flex items-center gap-1.5 px-4 py-2.5 bg-[#f37021] text-white text-xs font-black rounded-xl hover:opacity-90 disabled:opacity-50 transition-all cursor-pointer"
                        >
                          {invoiceSending ? <Loader2 className="w-4 h-4 animate-spin" /> : <Mail className="w-4 h-4" />}
                          Gửi hóa đơn qua email
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
    </div>
  );
}
