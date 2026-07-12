/**
 * Staff Leader "Visitor liên quan" tab (read-only).
 *
 * Self-contained: lists the Visitor accounts related to the Staff Leader's campus (via visit
 * requests), with search / status / nationality filters, paging and a detail modal. The campus
 * scope is resolved entirely server-side from the authenticated Staff Leader — this component
 * never sends a campusId. Read-only by design: the only row action is "Xem chi tiết".
 *
 * The table, action icon, pagination and detail modal intentionally mirror the internal Account
 * Management screen so the two tabs feel identical.
 */
import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Search, Eye, X, ChevronLeft, ChevronRight, ChevronDown, XCircle, FileText, Loader2, UserCog, Check,
} from 'lucide-react';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { useRelatedVisitors } from '../hooks/useRelatedVisitors';
import { accountManagementApi } from '../api/accountManagementApi';
import { getAccountErrorMessage } from '../api/accountError';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import type {
  RelatedVisitorDetail,
  RelatedVisitorListItem,
  RelatedVisitorQueryParams,
} from '../types/accountManagement.types';

const STATUS_LABELS: Record<string, string> = {
  ACTIVE: 'Hoạt động',
  INACTIVE: 'Vô hiệu hóa',
  LOCKED: 'Bị khóa',
};

const SCOPE_LABELS: Record<string, string> = {
  SINGLE_CAMPUS: 'Một cơ sở',
  MULTI_CAMPUS: 'Liên cơ sở',
};

const GENDER_LABELS: Record<string, string> = {
  '0': 'Nam', '1': 'Nữ', '2': 'Khác',
  MALE: 'Nam', FEMALE: 'Nữ', OTHER: 'Khác', UNKNOWN: 'Không xác định',
};
const genderLabel = (g?: unknown): string => {
  if (g === null || g === undefined || g === '') return '';
  const raw = String(g);
  return GENDER_LABELS[raw.trim().toUpperCase()] ?? raw;
};

function formatDateTime(value?: string | null): string {
  return formatVietnamDateTime(value, { fallback: '—' });
}

function StatusBadge({ status }: { status: string }) {
  const s = (status || '').toUpperCase();
  if (s === 'ACTIVE')
    return <span className="inline-flex items-center gap-1.5 text-[#0aa14f] bg-[#eaffe4] px-3 py-1.5 rounded-full text-[11px] font-bold border border-[#0aa14f]/30"><div className="w-1.5 h-1.5 rounded-full bg-[#0aa14f]" /> Hoạt động</span>;
  if (s === 'INACTIVE')
    return <span className="inline-flex items-center gap-1.5 text-amber-600 bg-amber-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-amber-200"><div className="w-1.5 h-1.5 rounded-full bg-amber-500" /> Vô hiệu hóa</span>;
  if (s === 'LOCKED')
    return <span className="inline-flex items-center gap-1.5 text-red-600 bg-red-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-red-200"><div className="w-1.5 h-1.5 rounded-full bg-red-600" /> Bị khóa</span>;
  return <span className="inline-flex items-center gap-1.5 text-gray-600 bg-gray-50 px-3 py-1.5 rounded-full text-[11px] font-bold border border-gray-200">{STATUS_LABELS[s] ?? status}</span>;
}

export function RelatedVisitorsTab() {
  const [search, setSearch] = useState('');
  const [nationalityFilter, setNationalityFilter] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);

  const debouncedSearch = useDebounce(search, 450);

  // Reset to page 1 when any filter changes.
  useEffect(() => {
    setPage(1);
  }, [debouncedSearch, nationalityFilter, pageSize]);

  const params = useMemo<RelatedVisitorQueryParams>(() => ({
    page,
    pageSize,
    keyword: debouncedSearch.trim() || undefined,
    nationality: nationalityFilter || undefined,
    sortBy: 'lastRelatedRequestAt',
    sortDirection: 'desc',
  }), [page, pageSize, debouncedSearch, nationalityFilter]);

  const { data, visitors, loading, error, refetch } = useRelatedVisitors(params);

  const totalItems = data?.totalItems ?? 0;
  const totalPages = data?.totalPages ?? 0;

  // ── Nationality dropdown options: derived once from the full (unfiltered) campus set. ──
  const [nationalityOptions, setNationalityOptions] = useState<string[]>([]);
  useEffect(() => {
    let active = true;
    accountManagementApi
      .getRelatedVisitors({ page: 1, pageSize: 100 })
      .then((res) => {
        if (!active) return;
        const distinct = Array.from(
          new Set(res.items.map((v) => (v.nationality ?? '').trim()).filter(Boolean)),
        ).sort((a, b) => a.localeCompare(b, 'vi'));
        setNationalityOptions(distinct);
      })
      .catch(() => { /* non-fatal: nationality filter simply stays empty */ });
    return () => { active = false; };
  }, []);

  // ── Custom nationality dropdown (height-capped + scrollable so a long list never overflows) ──
  const [natOpen, setNatOpen] = useState(false);
  const natRef = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!natOpen) return;
    const onDown = (e: MouseEvent) => {
      if (natRef.current && !natRef.current.contains(e.target as Node)) setNatOpen(false);
    };
    document.addEventListener('mousedown', onDown);
    return () => document.removeEventListener('mousedown', onDown);
  }, [natOpen]);

  // ── Detail modal ──
  const [detailOpen, setDetailOpen] = useState(false);
  const [detail, setDetail] = useState<RelatedVisitorDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);

  const openDetail = async (visitor: RelatedVisitorListItem) => {
    setDetailOpen(true);
    setDetail(null);
    setDetailError(null);
    setDetailLoading(true);
    try {
      const result = await accountManagementApi.getRelatedVisitorDetails(visitor.userId);
      setDetail(result);
    } catch (err) {
      setDetailError(getAccountErrorMessage(err, 'Không thể tải chi tiết tài khoản khách. Vui lòng thử lại.'));
    } finally {
      setDetailLoading(false);
    }
  };

  const closeDetail = () => {
    setDetailOpen(false);
    setDetail(null);
    setDetailError(null);
  };

  return (
    <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-[#004c91] overflow-hidden">
      {/* Filter bar */}
      <div className="p-6 bg-[#004c91] flex flex-wrap items-center gap-4 border-b border-[#00386b]">
        <div className="relative flex-1 min-w-[250px]">
          <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-gray-400" />
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Tìm theo Họ tên, Email, SĐT hoặc Quốc tịch..."
            className="w-full pl-11 pr-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
          />
        </div>

        <div className="relative" ref={natRef}>
          <button
            type="button"
            onClick={() => setNatOpen((o) => !o)}
            className="flex items-center justify-between gap-2 bg-white/10 text-white px-4 py-3 rounded-2xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 cursor-pointer min-w-[180px] w-full"
          >
            <span className="truncate">{nationalityFilter || 'Tất cả quốc tịch'}</span>
            <ChevronDown className={`w-4 h-4 text-white/70 shrink-0 transition-transform ${natOpen ? 'rotate-180' : ''}`} />
          </button>
          {natOpen && (
            <div className="absolute right-0 mt-2 w-full min-w-[200px] max-h-60 overflow-y-auto bg-white rounded-2xl shadow-xl border border-gray-100 z-30 py-1">
              {[''].concat(nationalityOptions).map((n) => {
                const selected = nationalityFilter === n;
                return (
                  <button
                    key={n || '__all__'}
                    type="button"
                    onClick={() => { setNationalityFilter(n); setNatOpen(false); }}
                    className={`w-full flex items-center justify-between gap-2 px-4 py-2.5 text-left text-sm transition-colors ${selected ? 'bg-blue-50 text-[#004c91] font-bold' : 'text-gray-700 hover:bg-gray-50'}`}
                  >
                    <span className="truncate">{n || 'Tất cả quốc tịch'}</span>
                    {selected && <Check className="w-4 h-4 shrink-0" />}
                  </button>
                );
              })}
            </div>
          )}
        </div>

      </div>

      {/* Error */}
      {error && (
        <div className="m-6 rounded-2xl border border-red-200 bg-red-50 px-5 py-4 text-sm font-bold text-red-700 flex items-center justify-between gap-3">
          <span className="flex items-center gap-3"><XCircle className="w-5 h-5 shrink-0" />{error}</span>
          <button onClick={refetch} className="text-xs underline hover:no-underline">Thử lại</button>
        </div>
      )}

      {/* Table */}
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse">
          <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
            <tr>
              <th className="p-5 pl-8 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">STT</th>
              <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Họ và Tên</th>
              <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Tên đăng nhập (Email)</th>
              <th className="p-5 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Trạng thái</th>
              <th className="p-5 pr-8 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Hành động</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {loading && (
              <tr>
                <td colSpan={5} className="py-16 text-center text-gray-400 font-medium text-sm">
                  Đang tải danh sách...
                </td>
              </tr>
            )}

            {!loading && !error && visitors.length === 0 && (
              <tr>
                <td colSpan={5} className="py-16 text-center text-gray-400 font-medium text-sm">
                  Chưa có tài khoản khách nào liên quan đến cơ sở của bạn.
                </td>
              </tr>
            )}

            {!loading && visitors.map((v, idx) => (
              <tr key={v.userId} className="hover:bg-blue-50/30 transition-colors group">
                <td className="p-5 pl-8 text-sm font-bold text-[#004c91] text-center">{(page - 1) * pageSize + idx + 1}</td>
                <td className="p-5 text-center">
                  <p className="text-[13px] font-bold text-[#004c91] leading-tight whitespace-nowrap">{v.fullName}</p>
                </td>
                <td className="p-5 text-[13px] font-medium text-gray-600 truncate max-w-[200px] text-center">{v.email}</td>
                <td className="p-5 text-center"><StatusBadge status={v.status} /></td>
                <td className="p-5 pr-8 text-center">
                  <div className="flex items-center justify-center gap-2">
                    <button
                      onClick={() => openDetail(v)}
                      className="flex items-center justify-center p-2 text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50 rounded-full transition-all outline-none"
                      title="Xem tài khoản"
                    >
                      <Eye className="w-5 h-5" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Pagination (mirrors internal Account Management) */}
      {totalItems > 0 && (
        <div className="p-6 border-t border-gray-100 flex items-center justify-between bg-gray-50/50">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-gray-500">Hiển thị</span>
            <div className="relative">
              <select
                value={pageSize}
                onChange={(e) => setPageSize(Number(e.target.value))}
                className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 appearance-none min-w-[70px] text-left"
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
            </div>
            <span className="text-sm font-medium text-gray-500">tài khoản / trang</span>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <div className="flex items-center gap-1">
              {Array.from({ length: totalPages }, (_, i) => i + 1).map((p) => (
                <button
                  key={p}
                  onClick={() => setPage(p)}
                  className={`w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none ${page === p ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-200'}`}
                >
                  {p}
                </button>
              ))}
            </div>
            <button
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page === totalPages}
              className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}

      {/* Detail modal (mirrors internal Account Management — read-only) */}
      {detailOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 sm:p-6">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-sm animate-in fade-in duration-300" onClick={closeDetail} />
          <div className="relative w-full max-w-4xl bg-white rounded-2xl shadow-2xl flex flex-col md:flex-row h-auto max-h-[85vh] animate-in zoom-in-95 duration-300 overflow-hidden">
            {/* Left sidebar */}
            <div className="p-4 sm:p-6 md:p-8 bg-gradient-to-br from-[#004c91] to-[#00386b] shrink-0 md:w-1/3 flex flex-col items-center justify-center text-center relative border-r border-[#00386b]/50">
              <button
                onClick={closeDetail}
                className="absolute top-4 right-4 md:hidden w-8 h-8 rounded-full bg-white/10 flex items-center justify-center text-white hover:bg-white/20 transition-colors outline-none"
              >
                <X className="w-5 h-5" />
              </button>
              <div className="w-24 h-24 rounded-2xl bg-white p-1 shadow-xl mb-6 ring-4 ring-white/10 flex items-center justify-center">
                <span className="text-4xl font-black text-[#004c91]">{(detail?.fullName || 'V').charAt(0).toUpperCase()}</span>
              </div>
              <span className="inline-flex px-3 py-1 rounded-lg text-[11px] font-black tracking-widest uppercase mb-3 border shadow-sm bg-white/20 text-white border-white/20">
                VISITOR
              </span>
              <h2 className="text-2xl font-black text-white leading-tight mb-2 tracking-tight">{detail?.fullName ?? 'Tài khoản khách'}</h2>
              {detail?.email && (
                <p className="text-blue-200 text-sm font-medium mb-2 bg-[#00386b]/40 px-3 py-1 rounded-full break-all">{detail.email}</p>
              )}
            </div>

            {/* Right content */}
            <div className="flex-1 overflow-y-auto p-8 bg-[#f8fafc] relative">
              <div className="flex items-start justify-between mb-6">
                <h3 className="text-xl font-black text-[#004c91] flex items-center gap-2 tracking-tight mt-1">
                  <UserCog className="w-6 h-6" /> Thông tin chi tiết
                </h3>
                <button
                  onClick={closeDetail}
                  className="hidden md:flex w-8 h-8 rounded-full bg-white border border-gray-200 shadow-sm items-center justify-center text-gray-500 hover:text-red-500 hover:border-red-200 hover:bg-red-50 transition-all outline-none"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              {detailLoading && (
                <div className="py-16 text-center text-gray-400">
                  <Loader2 className="w-6 h-6 animate-spin mx-auto mb-3" />
                  Đang tải chi tiết...
                </div>
              )}

              {detailError && (
                <div className="rounded-2xl border border-red-200 bg-red-50 px-5 py-4 text-sm font-bold text-red-700 flex items-center gap-3">
                  <XCircle className="w-5 h-5 shrink-0" />{detailError}
                </div>
              )}

              {detail && !detailLoading && (
                <>
                  <div className="bg-white rounded-2xl p-6 shadow-sm border border-gray-100 w-full animate-in fade-in duration-300">
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5 w-full">
                      <InfoField label="Họ và tên" value={detail.fullName} />
                      <InfoField label="Email" value={detail.email} />
                      <InfoField label="Giới tính" value={genderLabel(detail.gender) || '-'} />
                      <InfoField label="Số điện thoại" value={detail.phone || '-'} />
                      <InfoField label="Quốc tịch" value={detail.nationality || '-'} />
                    </div>
                  </div>

                  {/* Related requests */}
                  <div className="flex items-center gap-2 mt-8 mb-4">
                    <FileText className="w-5 h-5 text-[#004c91]" />
                    <h4 className="font-bold text-gray-900">Đơn liên quan đến cơ sở của bạn</h4>
                    <span className="ml-auto text-xs font-bold text-gray-400">{detail.relatedRequests.length} đơn</span>
                  </div>

                  {detail.relatedRequests.length === 0 ? (
                    <p className="text-sm text-gray-400 italic">Không có đơn liên quan hiển thị.</p>
                  ) : (
                    <div className="space-y-3">
                      {detail.relatedRequests.map((r) => (
                        <div key={`${r.visitRequestId}-${r.visitInstanceId}`} className="rounded-2xl border border-gray-100 bg-white p-4 shadow-sm">
                          <div className="flex items-start justify-between gap-2 mb-2">
                            <p className="text-sm text-gray-800 font-bold">{r.delegationName}</p>
                            <span className="shrink-0 text-[10px] font-bold uppercase tracking-wide px-2 py-0.5 rounded-full bg-blue-50 text-[#004c91] border border-blue-100">
                              {SCOPE_LABELS[r.visitScope] ?? r.visitScope}
                            </span>
                          </div>
                          <div className="text-xs text-gray-500">
                            {formatDateTime(r.plannedStartAt)} → {formatDateTime(r.plannedEndAt)}
                          </div>
                        </div>
                      ))}
                    </div>
                  )}
                </>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function InfoField({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex flex-col">
      <span className="block text-[10px] font-bold uppercase tracking-wider mb-1 text-gray-500">{label}</span>
      <span className="block text-sm font-bold text-gray-900 bg-gray-50/50 p-2.5 rounded-lg border border-gray-100 break-words">{value}</span>
    </div>
  );
}

export default RelatedVisitorsTab;
