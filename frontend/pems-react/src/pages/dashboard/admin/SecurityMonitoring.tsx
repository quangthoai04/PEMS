/**
 * Trang SecurityMonitoring (ADMIN) — 2 tab:
 *  - Login Logs: đọc login_logs qua /api/admin/login-logs
 *  - Security Events: đọc security_events qua /api/admin/security-events
 * Filter theo thời gian, kết quả, provider, portal, severity, IP và user.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Shield, Search, ChevronDown, ChevronLeft, ChevronRight, Loader2, RefreshCw,
} from 'lucide-react';
import { adminApi } from '../../../features/admin/api/adminApi';
import type {
  AdminLoginLogItem, AdminSecurityEventItem, PaginatedResult,
} from '../../../features/admin/types/admin.types';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { getApiErrorMessage } from '../../../shared/utils/toast';

const selectCls =
  'px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[140px] bg-white/10 text-white shadow-inner appearance-none';
const dateCls =
  'px-4 py-3 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all bg-white/10 text-white shadow-inner [color-scheme:dark]';

const SEVERITY_BADGE: Record<string, string> = {
  LOW: 'bg-gray-100 text-gray-500 border-gray-200',
  MEDIUM: 'bg-blue-50 text-[#0461b5] border-blue-200',
  HIGH: 'bg-orange-50 text-orange-600 border-orange-200',
  CRITICAL: 'bg-red-50 text-red-600 border-red-200',
};

interface Filters {
  keyword: string;
  status: string;      // login logs: SUCCESS/FAILED
  severity: string;    // security events
  result: string;      // security events
  portal: string;
  provider: string;
  ipAddress: string;
  fromDate: string;
  toDate: string;
}

const EMPTY_FILTERS: Filters = {
  keyword: '', status: '', severity: '', result: '', portal: '',
  provider: '', ipAddress: '', fromDate: '', toDate: '',
};

export function SecurityMonitoring() {
  const navigate = useNavigate();
  const [tab, setTab] = useState<'LOGIN_LOGS' | 'SECURITY_EVENTS'>('LOGIN_LOGS');
  const [filters, setFilters] = useState<Filters>(EMPTY_FILTERS);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [loginLogs, setLoginLogs] = useState<PaginatedResult<AdminLoginLogItem> | null>(null);
  const [events, setEvents] = useState<PaginatedResult<AdminSecurityEventItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const debouncedKeyword = useDebounce(filters.keyword, 450);
  const debouncedIp = useDebounce(filters.ipAddress, 450);

  const setFilter = (key: keyof Filters) => (value: string) =>
    setFilters((prev) => ({ ...prev, [key]: value }));

  const commonQuery = useMemo(() => ({
    page,
    pageSize,
    keyword: debouncedKeyword.trim() || undefined,
    portal: filters.portal || undefined,
    provider: filters.provider || undefined,
    ipAddress: debouncedIp.trim() || undefined,
    fromDate: filters.fromDate || undefined,
    toDate: filters.toDate || undefined,
  }), [page, pageSize, debouncedKeyword, filters.portal, filters.provider, debouncedIp, filters.fromDate, filters.toDate]);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    const request = tab === 'LOGIN_LOGS'
      ? adminApi.getLoginLogs({ ...commonQuery, status: filters.status || undefined }).then(setLoginLogs)
      : adminApi.getSecurityEvents({
          ...commonQuery,
          severity: filters.severity || undefined,
          result: filters.result || undefined,
        }).then(setEvents);
    request
      .catch((e: any) => setError(getApiErrorMessage(e, 'Không tải được dữ liệu bảo mật.')))
      .finally(() => setLoading(false));
  }, [tab, commonQuery, filters.status, filters.severity, filters.result]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [tab, debouncedKeyword, debouncedIp, filters.status, filters.severity, filters.result, filters.portal, filters.provider, filters.fromDate, filters.toDate, pageSize]);

  const current = tab === 'LOGIN_LOGS' ? loginLogs : events;
  const totalPages = current?.totalPages ?? 0;

  return (
    <div className="w-full pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] cursor-pointer">Dashboard</button>
        <span>/</span>
        <span className="text-[#004c91]">Bảo mật</span>
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
        {/* Tabs */}
        <div className="flex px-6 bg-[#004c91]">
          {([['LOGIN_LOGS', 'Login Logs'], ['SECURITY_EVENTS', 'Security Events']] as const).map(([key, label]) => (
            <button
              key={key}
              onClick={() => setTab(key)}
              className={`px-6 py-4 font-bold text-sm outline-none border-b-2 transition-colors cursor-pointer ${
                tab === key ? 'border-white text-white' : 'border-transparent text-blue-200 hover:text-white'
              }`}
            >
              {label}
            </button>
          ))}
        </div>

        {/* Filter bar */}
        <div className="p-6 bg-[#004c91] flex flex-wrap items-center gap-3 border-b border-[#00386b]">
          <div className="relative flex-1 min-w-[220px]">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={filters.keyword}
              onChange={(e) => setFilter('keyword')(e.target.value)}
              placeholder="Tìm theo email / họ tên..."
              className="w-full pl-11 pr-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
            />
          </div>

          {tab === 'LOGIN_LOGS' ? (
            <div className="relative">
              <select value={filters.status} onChange={(e) => setFilter('status')(e.target.value)} className={selectCls}>
                <option className="text-gray-900" value="">Mọi kết quả</option>
                <option className="text-gray-900" value="SUCCESS">Thành công</option>
                <option className="text-gray-900" value="FAILED">Thất bại</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
            </div>
          ) : (
            <>
              <div className="relative">
                <select value={filters.severity} onChange={(e) => setFilter('severity')(e.target.value)} className={selectCls}>
                  <option className="text-gray-900" value="">Mọi severity</option>
                  {['LOW', 'MEDIUM', 'HIGH', 'CRITICAL'].map((sv) => (
                    <option className="text-gray-900" key={sv} value={sv}>{sv}</option>
                  ))}
                </select>
                <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
              </div>
              <div className="relative">
                <select value={filters.result} onChange={(e) => setFilter('result')(e.target.value)} className={selectCls}>
                  <option className="text-gray-900" value="">Mọi kết quả</option>
                  <option className="text-gray-900" value="SUCCESS">SUCCESS</option>
                  <option className="text-gray-900" value="FAILURE">FAILURE</option>
                  <option className="text-gray-900" value="BLOCKED">BLOCKED</option>
                </select>
                <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
              </div>
            </>
          )}

          <div className="relative">
            <select value={filters.portal} onChange={(e) => setFilter('portal')(e.target.value)} className={selectCls}>
              <option className="text-gray-900" value="">Mọi portal</option>
              <option className="text-gray-900" value="INTERNAL">INTERNAL</option>
              <option className="text-gray-900" value="VISITOR">VISITOR</option>
            </select>
            <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
          </div>
          <div className="relative">
            <select value={filters.provider} onChange={(e) => setFilter('provider')(e.target.value)} className={selectCls}>
              <option className="text-gray-900" value="">Mọi provider</option>
              <option className="text-gray-900" value="LOCAL_PASSWORD">LOCAL_PASSWORD</option>
              <option className="text-gray-900" value="GOOGLE_SSO">GOOGLE_SSO</option>
              <option className="text-gray-900" value="FEID">FEID</option>
            </select>
            <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
          </div>
          <input
            type="text"
            value={filters.ipAddress}
            onChange={(e) => setFilter('ipAddress')(e.target.value)}
            placeholder="IP..."
            className="w-32 px-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
          />
          <input
            type="date"
            value={filters.fromDate}
            onChange={(e) => setFilter('fromDate')(e.target.value)}
            className={dateCls}
            title="Từ ngày"
          />
          <input
            type="date"
            value={filters.toDate}
            onChange={(e) => setFilter('toDate')(e.target.value)}
            className={dateCls}
            title="Đến ngày"
          />
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          {loading ? (
            <div className="py-16 text-center text-gray-400 text-sm font-medium">
              <Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Đang tải dữ liệu...
            </div>
          ) : error ? (
            <div className="py-16 text-center">
              <p className="text-sm font-bold text-red-500 mb-2">{error}</p>
              <button onClick={load} className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
            </div>
          ) : tab === 'LOGIN_LOGS' ? (
            <table className="w-full text-left border-collapse table-fixed">
              <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
                <tr>
                  <th className="p-3 pl-4 w-12 text-[11px] font-black uppercase tracking-widest text-center">STT</th>
                  <th className="p-3 w-[12%] text-[11px] font-black uppercase tracking-widest">Thời gian</th>
                  <th className="p-3 w-[18%] text-[11px] font-black uppercase tracking-widest">Người dùng</th>
                  <th className="p-3 w-[14%] text-[11px] font-black uppercase tracking-widest">Portal / Provider</th>
                  <th className="p-3 w-[10%] text-[11px] font-black uppercase tracking-widest text-center">Kết quả</th>
                  <th className="p-3 w-[17%] text-[11px] font-black uppercase tracking-widest">Lý do thất bại</th>
                  <th className="p-3 pr-4 w-[19%] text-[11px] font-black uppercase tracking-widest">IP / Thiết bị</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {(loginLogs?.items.length ?? 0) === 0 ? (
                  <tr><td colSpan={7} className="py-16 text-center text-gray-400 text-sm font-medium">Không có bản ghi nào phù hợp bộ lọc</td></tr>
                ) : loginLogs!.items.map((log, idx) => (
                  <tr key={log.loginLogId} className="hover:bg-blue-50/30 transition-colors">
                    <td className="p-3 pl-4 text-center text-xs font-bold text-gray-400">{(page - 1) * pageSize + idx + 1}</td>
                    <td className="p-3 text-xs text-gray-500 break-words">{formatVietnamDateTime(log.createdAt)}</td>
                    <td className="p-3 break-words">
                      <p className="text-[13px] font-bold text-[#004c91]">{log.fullName || '—'}</p>
                      <p className="text-xs text-gray-500 break-all">{log.email}</p>
                    </td>
                    <td className="p-3 text-[13px] text-gray-600 break-words">
                      {log.loginPortal}{log.providerType ? ` · ${log.providerType}` : ''}
                    </td>
                    <td className="p-3 text-center">
                      {log.status === 'SUCCESS'
                        ? <span className="inline-flex px-2.5 py-1 rounded-full text-[10px] font-bold border bg-[#eaffe4] text-[#0aa14f] border-[#0aa14f]/30 whitespace-nowrap">Thành công</span>
                        : <span className="inline-flex px-2.5 py-1 rounded-full text-[10px] font-bold border bg-red-50 text-red-600 border-red-200 whitespace-nowrap">Thất bại</span>}
                    </td>
                    <td className="p-3 text-xs text-gray-500 break-words" title={log.failureReason || undefined}>
                      {log.failureReason || '—'}
                    </td>
                    <td className="p-3 pr-4 text-xs text-gray-500 break-words">
                      <p className="font-bold text-gray-600">{log.ipAddress || '—'}</p>
                      <p className="break-all" title={log.userAgent || undefined}>{log.userAgent || '—'}</p>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : (
            <table className="w-full text-left border-collapse table-fixed">
              <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
                <tr>
                  <th className="p-3 pl-4 w-12 text-[11px] font-black uppercase tracking-widest text-center">STT</th>
                  <th className="p-3 w-[11%] text-[11px] font-black uppercase tracking-widest">Thời gian</th>
                  <th className="p-3 w-[9%] text-[11px] font-black uppercase tracking-widest text-center">Severity</th>
                  <th className="p-3 w-[16%] text-[11px] font-black uppercase tracking-widest">Sự kiện</th>
                  <th className="p-3 w-[15%] text-[11px] font-black uppercase tracking-widest">Người dùng</th>
                  <th className="p-3 w-[14%] text-[11px] font-black uppercase tracking-widest">Portal / Provider</th>
                  <th className="p-3 pr-4 w-[20%] text-[11px] font-black uppercase tracking-widest">IP / Chi tiết</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {(events?.items.length ?? 0) === 0 ? (
                  <tr><td colSpan={7} className="py-16 text-center text-gray-400 text-sm font-medium">Không có sự kiện nào phù hợp bộ lọc</td></tr>
                ) : events!.items.map((ev, idx) => (
                  <tr key={ev.securityEventId} className="hover:bg-blue-50/30 transition-colors">
                    <td className="p-3 pl-4 text-center text-xs font-bold text-gray-400">{(page - 1) * pageSize + idx + 1}</td>
                    <td className="p-3 text-xs text-gray-500 break-words">{formatVietnamDateTime(ev.createdAt)}</td>
                    <td className="p-3 text-center">
                      <span className={`inline-flex px-2 py-1 rounded-md text-[10px] font-black tracking-wider border whitespace-nowrap ${SEVERITY_BADGE[ev.severity] ?? SEVERITY_BADGE.LOW}`}>
                        {ev.severity}
                      </span>
                    </td>
                    <td className="p-3 break-words">
                      <p className="text-[13px] font-bold text-gray-700">{ev.eventType}</p>
                      <p className="text-xs text-gray-500">
                        {ev.result}{ev.failureReasonCode ? ` · ${ev.failureReasonCode}` : ''}
                      </p>
                    </td>
                    <td className="p-3 text-xs text-gray-500 break-words">{ev.email || '—'}</td>
                    <td className="p-3 text-[13px] text-gray-600 break-words">
                      {ev.loginPortal || '—'}{ev.providerType ? ` · ${ev.providerType}` : ''}
                    </td>
                    <td className="p-3 pr-4 text-xs text-gray-500 break-words">
                      <p className="font-bold text-gray-600">{ev.ipAddress || '—'}</p>
                      <p className="break-all" title={ev.detailText || undefined}>{ev.detailText || '—'}</p>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {/* Pagination */}
        {(current?.totalItems ?? 0) > 0 && !loading && !error && (
          <div className="p-5 border-t border-gray-100 flex flex-wrap items-center justify-between gap-3 bg-gray-50/50">
            <div className="flex items-center gap-2 text-sm font-medium text-gray-500">
              <span>Hiển thị</span>
              <div className="relative">
                <select
                  value={pageSize}
                  onChange={(e) => setPageSize(Number(e.target.value))}
                  className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none appearance-none"
                >
                  {[10, 20, 50].map((n) => <option key={n} value={n}>{n}</option>)}
                </select>
                <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
              </div>
              <span>/ trang · Tổng {current!.totalItems} bản ghi</span>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1}
                className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] disabled:opacity-50 cursor-pointer"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <span className="text-sm font-bold text-gray-700">{page} / {Math.max(totalPages, 1)}</span>
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
