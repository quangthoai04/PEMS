/**
 * Trang SecurityMonitoring (ADMIN) — đọc security_events qua /api/admin/security-events.
 * Filter theo thời gian, kết quả, provider, portal, severity, IP và user. Không còn tab
 * Login Logs riêng — lịch sử đăng nhập/thu hồi phiên đã có ở trang Phiên đăng nhập.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Shield, Search, ChevronDown, ChevronLeft, ChevronRight, Loader2, RefreshCw, Info,
} from 'lucide-react';
import { adminApi } from '../../../features/admin/api/adminApi';
import type { AdminSecurityEventItem, PaginatedResult } from '../../../features/admin/types/admin.types';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { getApiErrorMessage } from '../../../shared/utils/toast';

const selectCls =
  'px-3.5 py-2.5 pr-9 rounded-xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[130px] bg-white/10 text-white shadow-inner appearance-none';
const dateCls =
  'px-3.5 py-2.5 rounded-xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all bg-white/10 text-white shadow-inner [color-scheme:dark]';

const SEVERITY_BADGE: Record<string, string> = {
  LOW: 'bg-gray-100 text-gray-500 border-gray-200',
  MEDIUM: 'bg-blue-50 text-[#0461b5] border-blue-200',
  HIGH: 'bg-orange-50 text-orange-600 border-orange-200',
  CRITICAL: 'bg-red-50 text-red-600 border-red-200',
};

// Nhãn tiếng Việt cho các giá trị kỹ thuật — tránh lộ mã hệ thống (enum/DB code) ra người dùng.
const SEVERITY_LABEL: Record<string, string> = {
  LOW: 'Thấp',
  MEDIUM: 'Trung bình',
  HIGH: 'Cao',
  CRITICAL: 'Nghiêm trọng',
};

const RESULT_LABEL: Record<string, string> = {
  SUCCESS: 'Thành công',
  FAILURE: 'Thất bại',
  BLOCKED: 'Đã chặn',
};

const PORTAL_LABEL: Record<string, string> = {
  INTERNAL: 'Nội bộ',
  VISITOR: 'Khách',
};

const PROVIDER_LABEL: Record<string, string> = {
  LOCAL_PASSWORD: 'Mật khẩu',
  GOOGLE_SSO: 'Google SSO',
  FEID: 'FE ID',
};

const EVENT_TYPE_LABEL: Record<string, string> = {
  SSO_LOGIN: 'Đăng nhập SSO',
  PORTAL_VALIDATION: 'Kiểm tra cổng đăng nhập',
  CAMPUS_VALIDATION: 'Kiểm tra cơ sở',
  VISITOR_AUTO_PROVISION: 'Tự tạo tài khoản khách',
  SESSION_CREATED: 'Tạo phiên đăng nhập',
  SESSION_REVOKED: 'Thu hồi phiên đăng nhập',
  SESSION_EXPIRED: 'Phiên hết hạn',
  TOKEN_REFRESH: 'Làm mới token',
  SECURITY_POLICY_CHECK: 'Kiểm tra chính sách bảo mật',
};

interface Filters {
  keyword: string;
  severity: string;
  result: string;
  portal: string;
  provider: string;
  fromDate: string;
  toDate: string;
}

const EMPTY_FILTERS: Filters = {
  keyword: '', severity: '', result: '', portal: '',
  provider: '', fromDate: '', toDate: '',
};

export function SecurityMonitoring() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // `?keyword=` lets the account detail drawer open this page already scoped to one account (it
  // sends the account's email). Email is the right key here rather than a user id: a security event
  // records the address it was attempted AGAINST (`email_snapshot`) even when no account resolved,
  // so filtering by email also surfaces attempts that never linked to a user row. It seeds the
  // VISIBLE search box, so the operator can see the scope and clear it.
  const [filters, setFilters] = useState<Filters>(
    () => ({ ...EMPTY_FILTERS, keyword: searchParams.get('keyword') ?? '' }),
  );
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [events, setEvents] = useState<PaginatedResult<AdminSecurityEventItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Chi tiết sự kiện (detailText) mặc định rút gọn 1 dòng; bấm icon "i" để xem đầy đủ cho
  // riêng dòng đó (di chuột vào cũng thấy đầy đủ qua tooltip title).
  const [expandedDetails, setExpandedDetails] = useState<Set<number>>(new Set());
  const toggleDetail = (id: number) => setExpandedDetails((prev) => {
    const next = new Set(prev);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  const debouncedKeyword = useDebounce(filters.keyword, 450);

  const setFilter = (key: keyof Filters) => (value: string) =>
    setFilters((prev) => ({ ...prev, [key]: value }));

  const query = useMemo(() => ({
    page,
    pageSize,
    keyword: debouncedKeyword.trim() || undefined,
    portal: filters.portal || undefined,
    provider: filters.provider || undefined,
    fromDate: filters.fromDate || undefined,
    toDate: filters.toDate || undefined,
    severity: filters.severity || undefined,
    result: filters.result || undefined,
  }), [page, pageSize, debouncedKeyword, filters.portal, filters.provider, filters.fromDate, filters.toDate, filters.severity, filters.result]);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    adminApi.getSecurityEvents(query)
      .then(setEvents)
      .catch((e: any) => setError(getApiErrorMessage(e, 'Không tải được dữ liệu bảo mật.')))
      .finally(() => setLoading(false));
  }, [query]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [debouncedKeyword, filters.severity, filters.result, filters.portal, filters.provider, filters.fromDate, filters.toDate, pageSize]);

  const totalPages = events?.totalPages ?? 0;

  return (
    <div className="w-full pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] cursor-pointer">Dashboard</button>
        <span>/</span>
        <span className="text-[#004c91] font-medium">Bảo mật</span>
      </div>

      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] flex items-center gap-3">
            <Shield className="w-8 h-8" /> Giám sát bảo mật
          </h1>
        </div>
        <button
          onClick={load}
          className="px-4 py-2.5 rounded-xl text-sm font-bold text-[#004c91] border border-[#004c91]/30 hover:bg-blue-50 transition-colors flex items-center gap-2 cursor-pointer"
        >
          <RefreshCw className="w-4 h-4" /> Làm mới
        </button>
      </div>

      <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-[#004c91] overflow-hidden">
        {/* Filter bar — hàng 1: tìm kiếm + khoảng ngày; hàng 2: các bộ lọc chọn nhanh */}
        <div className="p-5 bg-[#004c91] flex flex-col gap-3 border-b border-[#00386b]">
          <div className="flex flex-wrap items-center gap-3">
            <div className="relative flex-1 min-w-0 sm:min-w-[220px]">
              <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-gray-400" />
              <input
                type="text"
                value={filters.keyword}
                onChange={(e) => setFilter('keyword')(e.target.value)}
                aria-label="Tìm theo email / họ tên"
                placeholder="Tìm theo email / họ tên..."
                className="w-full pl-11 pr-4 py-2.5 rounded-xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
              />
            </div>
            <div className="flex items-center gap-1.5 bg-white/10 rounded-xl px-2.5 py-2 shadow-inner shrink-0">
              <input
                type="date"
                value={filters.fromDate}
                onChange={(e) => setFilter('fromDate')(e.target.value)}
                className="px-1.5 py-1 rounded-lg border-none text-sm font-normal focus:outline-none bg-transparent text-white shadow-none [color-scheme:dark]"
                title="Từ ngày"
              />
              <span className="text-blue-200 text-xs">đến</span>
              <input
                type="date"
                value={filters.toDate}
                onChange={(e) => setFilter('toDate')(e.target.value)}
                className="px-1.5 py-1 rounded-lg border-none text-sm font-normal focus:outline-none bg-transparent text-white shadow-none [color-scheme:dark]"
                title="Đến ngày"
              />
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2.5">
            <div className="relative">
              <select value={filters.severity} onChange={(e) => setFilter('severity')(e.target.value)} className={selectCls}>
                <option className="text-gray-900" value="">Tất cả mức độ</option>
                {['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'].map((sv) => (
                  <option className="text-gray-900" key={sv} value={sv}>{SEVERITY_LABEL[sv]}</option>
                ))}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
            <div className="relative">
              <select value={filters.result} onChange={(e) => setFilter('result')(e.target.value)} className={selectCls}>
                <option className="text-gray-900" value="">Tất cả kết quả</option>
                {['SUCCESS', 'FAILURE', 'BLOCKED'].map((r) => (
                  <option className="text-gray-900" key={r} value={r}>{RESULT_LABEL[r]}</option>
                ))}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
            <div className="relative">
              <select value={filters.portal} onChange={(e) => setFilter('portal')(e.target.value)} className={selectCls}>
                <option className="text-gray-900" value="">Tất cả cổng đăng nhập</option>
                <option className="text-gray-900" value="INTERNAL">Nội bộ</option>
                <option className="text-gray-900" value="VISITOR">Khách</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
            <div className="relative">
              <select value={filters.provider} onChange={(e) => setFilter('provider')(e.target.value)} className={selectCls}>
                <option className="text-gray-900" value="">Tất cả phương thức</option>
                <option className="text-gray-900" value="LOCAL_PASSWORD">Mật khẩu</option>
                <option className="text-gray-900" value="GOOGLE_SSO">Google SSO</option>
                <option className="text-gray-900" value="FEID">FE ID</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
          </div>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          {loading ? (
            <div className="py-16 text-center text-gray-400 text-sm font-normal">
              <Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Đang tải dữ liệu...
            </div>
          ) : error ? (
            <div className="py-16 text-center">
              <p className="text-sm font-normal text-red-500 mb-2">{error}</p>
              <button onClick={load} className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
            </div>
          ) : (
            <table className="w-full text-left border-collapse table-fixed">
              <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
                <tr>
                  <th className="p-3 pl-4 w-12 text-[11px] font-black uppercase tracking-widest text-center">STT</th>
                  <th className="p-3 w-[11%] text-[11px] font-black uppercase tracking-widest">Thời gian</th>
                  <th className="p-3 w-[9%] text-[11px] font-black uppercase tracking-widest text-center">Mức độ</th>
                  <th className="p-3 w-[16%] text-[11px] font-black uppercase tracking-widest">Sự kiện</th>
                  <th className="p-3 w-[15%] text-[11px] font-black uppercase tracking-widest">Người dùng</th>
                  <th className="p-3 w-[14%] text-[11px] font-black uppercase tracking-widest">Cổng / Phương thức</th>
                  <th className="p-3 pr-4 w-[20%] text-[11px] font-black uppercase tracking-widest">IP / Chi tiết</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {(events?.items.length ?? 0) === 0 ? (
                  <tr><td colSpan={7} className="py-16 text-center text-gray-400 text-sm font-normal">Không có sự kiện nào phù hợp bộ lọc</td></tr>
                ) : events!.items.map((ev, idx) => (
                  <tr key={ev.securityEventId} className="hover:bg-blue-50/30 transition-colors">
                    <td className="p-3 pl-4 text-center text-xs font-bold text-gray-400">{(page - 1) * pageSize + idx + 1}</td>
                    <td className="p-3 text-xs text-gray-500 break-words">{formatVietnamDateTime(ev.createdAt)}</td>
                    <td className="p-3 text-center">
                      <span className={`inline-flex px-2 py-1 rounded-md text-[10px] font-black tracking-wider border whitespace-nowrap ${SEVERITY_BADGE[ev.severity] ?? SEVERITY_BADGE.LOW}`}>
                        {SEVERITY_LABEL[ev.severity] ?? ev.severity}
                      </span>
                    </td>
                    <td className="p-3 break-words">
                      <p className="text-[13px] font-bold text-gray-700">{EVENT_TYPE_LABEL[ev.eventType] ?? ev.eventType}</p>
                      <p className="text-xs text-gray-500">
                        {RESULT_LABEL[ev.result] ?? ev.result}{ev.failureReasonCode ? ` · ${ev.failureReasonCode}` : ''}
                      </p>
                    </td>
                    <td className="p-3 text-xs text-gray-500 break-words">{ev.email || '—'}</td>
                    <td className="p-3 text-[13px] text-gray-600 break-words">
                      <p className="font-normal text-gray-700">{ev.loginPortal ? (PORTAL_LABEL[ev.loginPortal] ?? ev.loginPortal) : '—'}</p>
                      {ev.providerType && (
                        <p className="text-xs text-gray-400">{PROVIDER_LABEL[ev.providerType] ?? ev.providerType}</p>
                      )}
                    </td>
                    <td className="p-3 pr-4 text-xs text-gray-500 break-words">
                      <p className="font-normal text-gray-600 break-all">{ev.ipAddress || '—'}</p>
                      {ev.detailText && (
                        <div className="mt-1 flex items-center gap-1 min-w-0">
                          <button
                            type="button"
                            onClick={() => toggleDetail(ev.securityEventId)}
                            title={ev.detailText}
                            className="shrink-0 text-gray-400 hover:text-[#004c91] cursor-pointer"
                          >
                            <Info className="w-3.5 h-3.5" />
                          </button>
                          <p
                            onClick={() => toggleDetail(ev.securityEventId)}
                            title={ev.detailText}
                            className={`min-w-0 flex-1 cursor-pointer hover:text-[#004c91] ${expandedDetails.has(ev.securityEventId) ? 'break-all' : 'truncate'}`}
                          >
                            {ev.detailText}
                          </p>
                        </div>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Pagination */}
        {(events?.totalItems ?? 0) > 0 && !loading && !error && (
          <div className="p-5 border-t border-gray-100 flex flex-wrap items-center justify-between gap-3 bg-gray-50/50">
            <div className="flex items-center gap-2 text-sm font-normal text-gray-500">
              <span>Hiển thị</span>
              <div className="relative">
                <select
                  value={pageSize}
                  onChange={(e) => setPageSize(Number(e.target.value))}
                  className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-normal text-gray-700 bg-white focus:outline-none appearance-none"
                >
                  {[10, 20, 50].map((n) => <option key={n} value={n}>{n}</option>)}
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span>/ trang · Tổng {events!.totalItems} sự kiện</span>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] disabled:opacity-50 cursor-pointer"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <span className="text-sm font-normal text-gray-700">{page} / {Math.max(totalPages, 1)}</span>
              <button
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] disabled:opacity-50 cursor-pointer"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
