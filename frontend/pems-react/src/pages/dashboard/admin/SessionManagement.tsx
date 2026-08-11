/**
 * Trang SessionManagement (ADMIN) — đọc user_sessions qua /api/admin/sessions.
 * Xem phiên ACTIVE/EXPIRED/REVOKED, thu hồi 1 phiên hoặc toàn bộ phiên của 1 user.
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  KeyRound, Search, ChevronDown, ChevronLeft, ChevronRight,
  Loader2, RefreshCw, ShieldOff, UserX, X, Info,
} from 'lucide-react';
import { adminApi } from '../../../features/admin/api/adminApi';
import type { AdminSessionItem, PaginatedResult } from '../../../features/admin/types/admin.types';
import { useDebounce } from '../../../shared/hooks/useDebounce';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import {
  getApiErrorMessage, showLoadingToast, updateToastSuccess, updateToastMessageError,
} from '../../../shared/utils/toast';

const selectCls =
  'px-4 py-3 pr-10 rounded-2xl border-none text-sm font-medium focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all min-w-[150px] bg-white/10 text-white shadow-inner appearance-none';

const STATUS_BADGE: Record<string, string> = {
  ACTIVE: 'bg-[#eaffe4] text-[#0aa14f] border-[#0aa14f]/30',
  EXPIRED: 'bg-gray-100 text-gray-500 border-gray-200',
  REVOKED: 'bg-red-50 text-red-600 border-red-200',
};

const STATUS_LABEL: Record<string, string> = {
  ACTIVE: 'Đang hoạt động',
  EXPIRED: 'Hết hạn',
  REVOKED: 'Đã thu hồi',
};

// Nhãn tiếng Việt cho các giá trị kỹ thuật hiển thị trong bảng — tránh lộ thuật ngữ hệ thống
// (INTERNAL/VISITOR, LOCAL_PASSWORD/GOOGLE_SSO, role code) ra người dùng cuối.
const PORTAL_LABEL: Record<string, string> = {
  INTERNAL: 'Nội bộ',
  VISITOR: 'Khách',
};

const PROVIDER_LABEL: Record<string, string> = {
  LOCAL_PASSWORD: 'Mật khẩu',
  GOOGLE_SSO: 'Google SSO',
};

const ROLE_LABEL: Record<string, string> = {
  ADMIN: 'Quản trị viên',
  HO: 'Head Office',
  STAFF: 'Nhân viên',
  DEPARTMENT: 'Phòng ban',
  STUDENT: 'Sinh viên',
  VISITOR: 'Khách',
};

export function SessionManagement() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // `?keyword=` lets another screen open this page already scoped to one account — the account
  // detail drawer's "Xem phiên đăng nhập" sends the account's email. It seeds the VISIBLE search
  // box rather than filtering behind its back: the operator can see why the list is short, widen it
  // by clearing the box, and the URL stays shareable.
  const [keyword, setKeyword] = useState(() => searchParams.get('keyword') ?? '');
  const [status, setStatus] = useState('');
  const [portal, setPortal] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const [data, setData] = useState<PaginatedResult<AdminSessionItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Chuỗi thiết bị (User-Agent) mặc định rút gọn 1 dòng; bấm icon "i" để xem đầy đủ cho riêng
  // dòng đó (di chuột vào cũng thấy đầy đủ qua tooltip title).
  const [expandedDevices, setExpandedDevices] = useState<Set<number>>(new Set());
  const toggleDevice = (sessionId: number) => setExpandedDevices((prev) => {
    const next = new Set(prev);
    if (next.has(sessionId)) next.delete(sessionId); else next.add(sessionId);
    return next;
  });

  // Modal thu hồi: 1 phiên hoặc toàn bộ phiên của user.
  const [revokeTarget, setRevokeTarget] = useState<{ kind: 'session' | 'user'; session: AdminSessionItem } | null>(null);
  const [revoking, setRevoking] = useState(false);

  const debouncedKeyword = useDebounce(keyword, 450);

  const query = useMemo(() => ({
    page,
    pageSize,
    keyword: debouncedKeyword.trim() || undefined,
    status: status || undefined,
    portal: portal || undefined,
  }), [page, pageSize, debouncedKeyword, status, portal]);

  const load = useCallback(() => {
    setLoading(true);
    setError(null);
    adminApi.getSessions(query)
      .then(setData)
      .catch((e: any) => setError(getApiErrorMessage(e, 'Không tải được danh sách phiên đăng nhập.')))
      .finally(() => setLoading(false));
  }, [query]);

  useEffect(() => { load(); }, [load]);
  useEffect(() => { setPage(1); }, [debouncedKeyword, status, portal, pageSize]);

  const confirmRevoke = async () => {
    if (!revokeTarget) return;
    setRevoking(true);
    const toastId = showLoadingToast('Đang thu hồi phiên...', 'admin-revoke');
    try {
      if (revokeTarget.kind === 'session') {
        const res = await adminApi.revokeSession(revokeTarget.session.sessionId);
        updateToastSuccess(toastId, res.message);
      } else {
        const res = await adminApi.revokeUserSessions(revokeTarget.session.userId);
        updateToastSuccess(toastId, res.message);
      }
      setRevokeTarget(null);
      load();
    } catch (e: any) {
      updateToastMessageError(toastId, getApiErrorMessage(e, 'Không thể thu hồi phiên. Vui lòng thử lại.'));
    } finally {
      setRevoking(false);
    }
  };

  const totalPages = data?.totalPages ?? 0;

  return (
    <div className="w-full pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm font-medium text-gray-500 mb-6">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] cursor-pointer">Dashboard</button>
        <span>/</span>
        <span className="text-[#004c91]">Phiên đăng nhập</span>
      </div>

      <div className="flex items-center justify-between mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] flex items-center gap-3">
            <KeyRound className="w-8 h-8" /> Phiên đăng nhập
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
        {/* Filter bar */}
        <div className="p-6 bg-[#004c91] flex flex-wrap items-center gap-4 border-b border-[#00386b]">
          <div className="relative flex-1 min-w-[250px]">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-gray-400" />
            <input
              type="text"
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              aria-label="Tìm theo email hoặc họ tên"
              placeholder="Tìm theo email hoặc họ tên..."
              className="w-full pl-11 pr-4 py-3 rounded-2xl border-none focus:outline-none focus:ring-2 focus:ring-white/50 focus:bg-white/20 transition-all text-sm shadow-inner bg-white/10 text-white placeholder:text-blue-200"
            />
          </div>
          <div className="relative">
            <select value={status} onChange={(e) => setStatus(e.target.value)} className={selectCls}>
              <option className="text-gray-900" value="">Tất cả trạng thái</option>
              <option className="text-gray-900" value="ACTIVE">Đang hoạt động</option>
              <option className="text-gray-900" value="EXPIRED">Hết hạn</option>
              <option className="text-gray-900" value="REVOKED">Đã thu hồi</option>
            </select>
            <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
          </div>
          <div className="relative">
            <select value={portal} onChange={(e) => setPortal(e.target.value)} className={selectCls}>
              <option className="text-gray-900" value="">Tất cả cổng đăng nhập</option>
              <option className="text-gray-900" value="INTERNAL">Nội bộ</option>
              <option className="text-gray-900" value="VISITOR">Khách</option>
            </select>
            <ChevronDown className="w-4 h-4 absolute right-4 top-1/2 -translate-y-1/2 text-white pointer-events-none opacity-70" />
          </div>
        </div>

        {/* Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse table-fixed">
            <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
              <tr>
                <th className="p-3 pl-4 w-12 text-[11px] font-black uppercase tracking-widest text-center">STT</th>
                <th className="p-3 w-[18%] text-[11px] font-black uppercase tracking-widest">Người dùng</th>
                <th className="p-3 w-[14%] text-[11px] font-black uppercase tracking-widest">Cổng / Phương thức</th>
                <th className="p-3 w-[15%] text-[11px] font-black uppercase tracking-widest">IP / Thiết bị</th>
                <th className="p-3 w-[12%] text-[11px] font-black uppercase tracking-widest">Tạo lúc</th>
                <th className="p-3 w-[12%] text-[11px] font-black uppercase tracking-widest">Hết hạn</th>
                <th className="p-3 w-[11%] text-[11px] font-black uppercase tracking-widest text-center">Trạng thái</th>
                <th className="p-3 pr-4 w-[12%] text-[11px] font-black uppercase tracking-widest text-center">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {loading ? (
                <tr><td colSpan={8} className="py-16 text-center text-gray-400 text-sm font-medium">
                  <Loader2 className="w-5 h-5 animate-spin inline mr-2" /> Đang tải danh sách phiên...
                </td></tr>
              ) : error ? (
                <tr><td colSpan={8} className="py-16 text-center">
                  <p className="text-sm font-bold text-red-500 mb-2">{error}</p>
                  <button onClick={load} className="text-sm font-bold text-[#004c91] hover:underline cursor-pointer">Thử lại</button>
                </td></tr>
              ) : (data?.items.length ?? 0) === 0 ? (
                <tr><td colSpan={8} className="py-16 text-center text-gray-400 text-sm font-medium">
                  Không có phiên nào phù hợp bộ lọc
                </td></tr>
              ) : data!.items.map((sess, idx) => (
                <tr key={sess.sessionId} className="hover:bg-blue-50/30 transition-colors">
                  <td className="p-3 pl-4 text-center text-xs font-bold text-gray-400">{(page - 1) * pageSize + idx + 1}</td>
                  <td className="p-3 break-words">
                    <p className="text-[13px] font-bold text-[#004c91]">
                      {sess.fullName}
                      {sess.isCurrentSession && (
                        <span className="block mt-1 w-fit text-[10px] font-black text-[#f37021] bg-orange-50 border border-orange-200 px-1.5 py-0.5 rounded-md uppercase">Phiên của bạn</span>
                      )}
                    </p>
                    <p className="text-xs text-gray-500 break-all">{sess.email}</p>
                    <p className="text-[10px] text-gray-400 font-bold uppercase">
                      {ROLE_LABEL[sess.roleCode ?? ''] ?? sess.roleCode ?? ''} · Phiên #{sess.sessionId}
                    </p>
                  </td>
                  <td className="p-3 text-[13px] text-gray-600 break-words">
                    <p className="font-bold text-gray-700">{PORTAL_LABEL[sess.loginPortal] ?? sess.loginPortal}</p>
                    {sess.providerType && (
                      <p className="text-xs text-gray-400">{PROVIDER_LABEL[sess.providerType] ?? sess.providerType}</p>
                    )}
                  </td>
                  <td className="p-3 text-xs text-gray-500 break-words">
                    <p className="font-bold text-gray-600 break-all">{sess.ipAddress || '—'}</p>
                    {sess.userAgent && (
                      <div className="mt-1 flex items-center gap-1 min-w-0">
                        <button
                          type="button"
                          onClick={() => toggleDevice(sess.sessionId)}
                          title={sess.userAgent}
                          className="shrink-0 text-gray-400 hover:text-[#004c91] cursor-pointer"
                        >
                          <Info className="w-3.5 h-3.5" />
                        </button>
                        <p
                          onClick={() => toggleDevice(sess.sessionId)}
                          title={sess.userAgent}
                          className={`min-w-0 flex-1 cursor-pointer hover:text-[#004c91] ${expandedDevices.has(sess.sessionId) ? 'break-all' : 'truncate'}`}
                        >
                          {sess.userAgent}
                        </p>
                      </div>
                    )}
                  </td>
                  <td className="p-3 text-xs text-gray-500 break-words">{formatVietnamDateTime(sess.createdAt)}</td>
                  <td className="p-3 text-xs text-gray-500 break-words">
                    {formatVietnamDateTime(sess.expiresAt)}
                    {sess.revokedAt && (
                      <p className="text-red-500 font-semibold">Thu hồi: {formatVietnamDateTime(sess.revokedAt)}</p>
                    )}
                  </td>
                  <td className="p-3 text-center">
                    <span className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold border whitespace-nowrap ${STATUS_BADGE[sess.status]}`}>
                      {STATUS_LABEL[sess.status] ?? sess.status}
                    </span>
                  </td>
                  <td className="p-3 pr-4 text-center">
                    {sess.status === 'ACTIVE' ? (
                      <div className="flex flex-col items-stretch gap-1.5">
                        <button
                          onClick={() => setRevokeTarget({ kind: 'session', session: sess })}
                          className="px-2 py-1.5 rounded-lg text-[11px] font-bold text-red-500 border border-red-200 hover:bg-red-50 transition-colors flex items-center justify-center gap-1 cursor-pointer"
                          title="Thu hồi phiên này"
                        >
                          <ShieldOff className="w-3.5 h-3.5 shrink-0" /> Thu hồi
                        </button>
                        <button
                          onClick={() => setRevokeTarget({ kind: 'user', session: sess })}
                          className="px-2 py-1.5 rounded-lg text-[11px] font-bold text-gray-600 border border-gray-300 hover:bg-gray-50 transition-colors flex items-center justify-center gap-1 cursor-pointer"
                          title="Thu hồi toàn bộ phiên của người dùng này"
                        >
                          <UserX className="w-3.5 h-3.5 shrink-0" /> Cả user
                        </button>
                      </div>
                    ) : (
                      <span className="text-gray-300 text-sm">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {(data?.totalItems ?? 0) > 0 && (
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
              <span>/ trang · Tổng {data!.totalItems} phiên</span>
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

      {/* Confirm revoke modal */}
      {revokeTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md p-6 animate-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-lg font-black text-gray-800">Xác nhận thu hồi</h3>
              <button onClick={() => setRevokeTarget(null)} className="p-1.5 rounded-lg text-gray-400 hover:bg-gray-100 cursor-pointer">
                <X className="w-5 h-5" />
              </button>
            </div>
            <p className="text-sm text-gray-600 mb-2">
              {revokeTarget.kind === 'session'
                ? <>Thu hồi phiên <strong>#{revokeTarget.session.sessionId}</strong> của <strong>{revokeTarget.session.fullName}</strong> ({revokeTarget.session.email})?</>
                : <>Thu hồi <strong>toàn bộ phiên đang hoạt động</strong> của <strong>{revokeTarget.session.fullName}</strong> ({revokeTarget.session.email})?</>}
            </p>
            <p className="text-xs text-gray-400 mb-5">
              Người dùng sẽ bị đăng xuất ngay lập tức và phải đăng nhập lại.
              {(revokeTarget.session.isCurrentSession || (revokeTarget.kind === 'user' && revokeTarget.session.isCurrentSession)) &&
                ' Lưu ý: đây là phiên của chính bạn — bạn cũng sẽ bị đăng xuất.'}
            </p>
            <div className="flex justify-end gap-2">
              <button
                onClick={() => setRevokeTarget(null)}
                disabled={revoking}
                className="px-4 py-2 rounded-xl text-sm font-bold text-gray-500 hover:bg-gray-100 transition-colors cursor-pointer"
              >
                Hủy
              </button>
              <button
                onClick={() => void confirmRevoke()}
                disabled={revoking}
                className="px-5 py-2 rounded-xl text-sm font-bold text-white bg-red-500 hover:bg-red-600 transition-colors disabled:opacity-50 cursor-pointer flex items-center gap-2"
              >
                {revoking && <Loader2 className="w-4 h-4 animate-spin" />}
                Thu hồi
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
