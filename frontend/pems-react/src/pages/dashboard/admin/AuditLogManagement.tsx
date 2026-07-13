/**
 * Trang AuditLogManagement (ADMIN) — list/detail từ audit_logs + audit_log_changes.
 * Giá trị nhạy cảm (password/token/credential/secret/cookie/refresh...) đã được
 * backend mask trước khi trả về.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  ScrollText, Search, ChevronDown, ChevronLeft, ChevronRight,
  Loader2, RefreshCw, Eye, X, ShieldAlert,
} from 'lucide-react';
import { adminApi } from '../../../features/admin/api/adminApi';
import type {
  AdminAuditLogDetail, AdminAuditLogItem, PaginatedResult,
} from '../../../features/admin/types/admin.types';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { getApiErrorMessage } from '../../../shared/utils/toast';

const dateCls =
  'px-4 py-3 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all bg-white/10 text-white shadow-inner [color-scheme:dark]';

/** Giá trị audit có thể là JSON — pretty-print để đọc diff trước/sau dễ hơn. */
function prettyValue(value?: string | null): string {
  if (value === null || value === undefined || value === '') return '—';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

export function AuditLogManagement() {
  const navigate = useNavigate();

  const [keyword, setKeyword] = useState('');
  const [action, setAction] = useState('');
  const [entityType, setEntityType] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [data, setData] = useState<PaginatedResult<AdminAuditLogItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Detail modal
  const [detail, setDetail] = useState<AdminAuditLogDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [detailId, setDetailId] = useState<number | null>(null);

  const debouncedKeyword = useDebounce(keyword, 450);
  const debouncedAction = useDebounce(action, 450);
  const debouncedEntity = useDebounce(entityType, 450);

  const query = useMemo(() => ({
    page,
    pageSize,
    keyword: debouncedKeyword.trim() || undefined,
    action: debouncedAction.trim() || undefined,
    entityType: debouncedEntity.trim() || undefined,
    fromDate: fromDate || undefined,
    toDate: toDate || undefined,
  }), [page, pageSize, debouncedKeyword, debouncedAction, debouncedEntity, fromDate, toDate]);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    adminApi.getAuditLogs(query)
      .then(setData)
      .catch((e: any) => setError(getApiErrorMessage(e, 'Không tải được nhật ký kiểm toán.')))
      .finally(() => setLoading(false));
  }, [query]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [debouncedKeyword, debouncedAction, debouncedEntity, fromDate, toDate, pageSize]);

  const openDetail = (auditLogId: number) => {
    setDetailId(auditLogId);
    setDetail(null);
    setDetailError(null);
    setDetailLoading(true);
    adminApi.getAuditLogDetail(auditLogId)
      .then(setDetail)
      .catch((e: any) => setDetailError(getApiErrorMessage(e, 'Không tải được chi tiết bản ghi.')))
      .finally(() => setDetailLoading(false));
  };

  const closeDetail = () => { setDetailId(null); setDetail(null); setDetailError(null); };

  const totalPages = data?.totalPages ?? 0;

  return (
    <div className="w-full pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] cursor-pointer">Dashboard</button>
        <span>/</span>
        <span className="text-[#004c91]">Nhật ký kiểm toán</span>
      </div>

      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] flex items-center gap-3">
            <ScrollText className="w-8 h-8" /> Nhật ký kiểm toán
          </h1>
          <p className="text-gray-500 mt-1 font-medium">
            audit_logs + audit_log_changes — dữ liệu nhạy cảm đã được che (***MASKED***)
          </p>
        </div>
        <button
          onClick={load}
          className="px-4 py-2.5 rounded-xl text-sm font-bold text-[#004c91] border border-[#004c91]/30 hover:bg-blue-50 transition-colors flex items-center gap-2 cursor-pointer"
        >
          <RefreshCw className="w-4 h-4" /> Làm mới
        </button>
      </div>

      <div className="bg-white rounded-[2rem] shadow-[0_8px_30px_-4px_rgba(0,0,0,0.05)] border border-[#004c91] overflow-hidden">
        {/* Filter bar */}
        <div className="p-6 bg-[#004c91] flex flex-wrap items-center gap-3 border-b border-[#00386b]">
          <div className="relative flex-1 min-w-[220px]">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              placeholder="Tìm theo actor (email / họ tên)..."
              className="w-full pl-11 pr-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
            />
          </div>
          <input
            type="text"
            value={action}
            onChange={(e) => setAction(e.target.value)}
            placeholder="Action (VD: UPDATE_ACCOUNT_ROLE)"
            className="w-64 px-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
          />
          <input
            type="text"
            value={entityType}
            onChange={(e) => setEntityType(e.target.value)}
            placeholder="Entity (VD: User)"
            className="w-40 px-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
          />
          <input type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} className={dateCls} title="Từ ngày" />
          <input type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} className={dateCls} title="Đến ngày" />
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
              <tr>
                <th className="p-4 pl-6 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Thời gian</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Actor</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Action</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Entity</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Campus</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">IP</th>
                <th className="p-4 pr-6 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">Chi tiết</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading ? (
                <tr><td colSpan={7} className="py-16 text-center text-gray-400 text-sm font-medium">
                  <Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Đang tải nhật ký...
                </td></tr>
              ) : error ? (
                <tr><td colSpan={7} className="py-16 text-center">
                  <p className="text-sm font-bold text-red-500 mb-2">{error}</p>
                  <button onClick={load} className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
                </td></tr>
              ) : (data?.items.length ?? 0) === 0 ? (
                <tr><td colSpan={7} className="py-16 text-center text-gray-400 text-sm font-medium">
                  Không có bản ghi nào phù hợp bộ lọc
                </td></tr>
              ) : data!.items.map((log) => (
                <tr key={log.auditLogId} className="hover:bg-blue-50/30 transition-colors">
                  <td className="p-4 pl-6 text-xs text-gray-500 whitespace-nowrap">{formatVietnamDateTime(log.createdAt)}</td>
                  <td className="p-4">
                    <p className="text-[13px] font-bold text-[#004c91] whitespace-nowrap">{log.actorName || 'Hệ thống'}</p>
                    <p className="text-xs text-gray-500">{log.actorEmail || '—'}</p>
                  </td>
                  <td className="p-4">
                    <span className="inline-flex px-2.5 py-1 rounded-lg text-[11px] font-bold bg-blue-50 text-[#004c91] border border-blue-100 whitespace-nowrap">
                      {log.action}
                    </span>
                  </td>
                  <td className="p-4 text-[13px] text-gray-600 whitespace-nowrap">
                    {log.entityType}{log.entityId ? ` #${log.entityId}` : ''}
                  </td>
                  <td className="p-4 text-[13px] text-gray-600 whitespace-nowrap">{log.campusName || '—'}</td>
                  <td className="p-4 text-xs text-gray-500 whitespace-nowrap">{log.ipAddress || '—'}</td>
                  <td className="p-4 pr-6 text-center">
                    <button
                      onClick={() => openDetail(log.auditLogId)}
                      className="p-2 text-gray-500 hover:text-[#004c91] hover:bg-blue-50/50 rounded-full transition-all cursor-pointer"
                      title={`Xem chi tiết (${log.changeCount} thay đổi)`}
                    >
                      <Eye className="w-4.5 h-4.5" />
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {(data?.totalItems ?? 0) > 0 && !loading && !error && (
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
              <span>/ trang · Tổng {data!.totalItems} bản ghi</span>
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

      {/* Detail modal */}
      {detailId !== null && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-3xl max-h-[85vh] overflow-y-auto p-6 animate-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-black text-gray-800">Chi tiết bản ghi kiểm toán #{detailId}</h3>
              <button onClick={closeDetail} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 cursor-pointer">
                <X className="w-5 h-5" />
              </button>
            </div>

            {detailLoading ? (
              <div className="py-12 text-center text-gray-400 text-sm font-medium">
                <Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Đang tải chi tiết...
              </div>
            ) : detailError ? (
              <div className="py-12 text-center">
                <p className="text-sm font-bold text-red-500 mb-2">{detailError}</p>
                <button onClick={() => openDetail(detailId)} className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
              </div>
            ) : detail && (
              <>
                <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-3 text-sm mb-6">
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Actor</dt>
                    <dd className="font-bold text-gray-800">
                      {detail.actorName || 'Hệ thống'}
                      {detail.actorRoleCode ? <span className="ml-1.5 text-[10px] font-black text-[#004c91] bg-blue-50 px-1.5 py-0.5 rounded uppercase">{detail.actorRoleCode}</span> : null}
                    </dd>
                    <dd className="text-xs text-gray-500">{detail.actorEmail || '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Action</dt>
                    <dd className="font-bold text-gray-800">{detail.action}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Entity</dt>
                    <dd className="text-gray-700">{detail.entityType}{detail.entityId ? ` #${detail.entityId}` : ''}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Campus</dt>
                    <dd className="text-gray-700">{detail.campusName || '—'}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Thời gian</dt>
                    <dd className="text-gray-700">{formatVietnamDateTime(detail.createdAt)}</dd>
                  </div>
                  <div>
                    <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">IP / Request ID</dt>
                    <dd className="text-gray-700">{detail.ipAddress || '—'}{detail.requestId ? ` · ${detail.requestId}` : ''}</dd>
                  </div>
                  {detail.userAgent && (
                    <div className="sm:col-span-2">
                      <dt className="text-[10px] font-bold text-gray-400 uppercase tracking-wider">Thiết bị</dt>
                      <dd className="text-xs text-gray-500 break-all">{detail.userAgent}</dd>
                    </div>
                  )}
                </dl>

                <h4 className="text-sm font-black text-gray-700 uppercase tracking-wider mb-3">
                  Thay đổi trước / sau ({detail.changes.length})
                </h4>
                {detail.changes.length === 0 ? (
                  <p className="text-sm text-gray-400 font-medium py-4 text-center">Bản ghi này không kèm thay đổi dữ liệu.</p>
                ) : (
                  <div className="space-y-4">
                    {detail.changes.map((change) => (
                      <div key={change.auditLogChangeId} className="border border-gray-100 rounded-xl overflow-hidden">
                        <div className="px-4 py-2 bg-slate-50 flex items-center justify-between">
                          <span className="text-xs font-black text-gray-600 uppercase tracking-wider">{change.fieldName}</span>
                          {change.isMasked && (
                            <span className="inline-flex items-center gap-1 text-[10px] font-black text-red-500 bg-red-50 border border-red-100 px-2 py-0.5 rounded-md uppercase">
                              <ShieldAlert className="w-3 h-3" /> Đã che dữ liệu nhạy cảm
                            </span>
                          )}
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-2 divide-y md:divide-y-0 md:divide-x divide-gray-100">
                          <div className="p-3">
                            <p className="text-[10px] font-bold text-red-400 uppercase tracking-wider mb-1">Trước</p>
                            <pre className="text-xs text-gray-600 whitespace-pre-wrap break-all font-mono bg-red-50/40 rounded-lg p-2 max-h-48 overflow-y-auto">{prettyValue(change.oldValue)}</pre>
                          </div>
                          <div className="p-3">
                            <p className="text-[10px] font-bold text-emerald-500 uppercase tracking-wider mb-1">Sau</p>
                            <pre className="text-xs text-gray-600 whitespace-pre-wrap break-all font-mono bg-emerald-50/40 rounded-lg p-2 max-h-48 overflow-y-auto">{prettyValue(change.newValue)}</pre>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
