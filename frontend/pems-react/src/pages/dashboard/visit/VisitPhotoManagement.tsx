/**
 * VisitPhotoManagement — tab "Quản lý ảnh đoàn khách" của Student.
 *
 * Bảng: STT | Tên đoàn khách | Tên thư mục | Hành động. Chỉ liệt kê các campus instance mà
 * Student có participation STUDENT + ACCEPTED (backend enforce, chống IDOR); tên đoàn lấy theo
 * read-path v2 per-campus (dual-read) — không dùng compatibility projection của visit_requests.
 * "Xem chi tiết" / "Chỉnh sửa" mở modal dùng chung VisitPhotoPanel (view / edit).
 */
import { useCallback, useEffect, useState } from 'react';
import { Camera, ChevronLeft, ChevronRight, Eye, Pencil, Search, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { visitPhotosApi } from '../../../features/delegations/api/visitPhotosApi';
import { VisitPhotoPanel } from '../../../features/delegations/components/VisitPhotoPanel';
import type { MyVisitPhotoFolderItem, MyVisitPhotoFoldersPage } from '../../../features/delegations/types/visitPhotos.types';

const PAGE_SIZE = 10;

const INSTANCE_STATUS_LABELS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ xử lý tại cơ sở',
  ASSIGNED: 'Đã duyệt & gán Host',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Chờ đóng đoàn',
  CLOSED: 'Đã đóng đoàn',
  CANCELLED: 'Đã hủy',
  REJECTED: 'Từ chối',
};

export function VisitPhotoManagement() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [sortDirection, setSortDirection] = useState('DESC');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [data, setData] = useState<MyVisitPhotoFoldersPage | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [modal, setModal] = useState<{ item: MyVisitPhotoFolderItem; mode: 'view' | 'edit' } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setData(await visitPhotosApi.myFolders(page, PAGE_SIZE, search || undefined, sortDirection, fromDate || undefined, toDate || undefined));
      setError(null);
    } catch (e: any) {
      if (e?.response?.status === 403) setError('Chức năng này chỉ dành cho Sinh viên tham gia tiếp khách.');
      else setError('Không thể tải danh sách ảnh đoàn khách. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, [page, search, sortDirection, fromDate, toDate]);

  useEffect(() => { load(); }, [load]);

  // Debounce tìm kiếm theo convention các trang quản lý.
  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 400);
    return () => clearTimeout(t);
  }, [searchInput]);

  const closeModal = async (changed: boolean) => {
    setModal(null);
    if (changed) await load();
  };

  const totalPages = data?.totalPages ?? 0;
  const items = data?.items ?? [];

  return (
    <div className="p-4 sm:p-6 md:p-8 w-full mx-auto pb-16 animate-in fade-in duration-300">
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý ảnh đoàn khách</span>
      </div>

      <div className="bg-white rounded-2xl border border-slate-200 shadow-sm">
        <div className="px-4 sm:px-6 py-4 border-b border-slate-100 flex flex-wrap items-center justify-between gap-3">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
              <Camera className="w-5 h-5" />
            </div>
            <div>
              <h1 className="text-lg font-black text-[#004c91]">Quản lý ảnh đoàn khách</h1>
              <p className="text-xs font-semibold text-slate-500">
                Ảnh bạn đóng góp cho các chuyến thăm đã nhận lời tham gia.
              </p>
            </div>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <select
              value={sortDirection}
              onChange={(e) => { setSortDirection(e.target.value); setPage(1); }}
              className="text-sm rounded-xl border border-gray-300 px-3 py-2 outline-none focus:border-[#004c91] bg-white cursor-pointer"
            >
              <option value="DESC">Mới nhất</option>
              <option value="ASC">Cũ nhất</option>
            </select>
            <div className="flex items-center gap-2">
              <input
                type="date"
                value={fromDate}
                onChange={(e) => { setFromDate(e.target.value); setPage(1); }}
                className="text-sm rounded-xl border border-gray-300 px-3 py-2 outline-none focus:border-[#004c91]"
              />
              <span className="text-gray-400">-</span>
              <input
                type="date"
                value={toDate}
                onChange={(e) => { setToDate(e.target.value); setPage(1); }}
                className="text-sm rounded-xl border border-gray-300 px-3 py-2 outline-none focus:border-[#004c91]"
              />
            </div>
            <div className="relative">
              <Search className="w-4 h-4 text-gray-400 absolute left-3 top-1/2 -translate-y-1/2" />
              <input
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                placeholder="Tìm theo tên đoàn khách..."
                className="w-[260px] max-w-full text-sm rounded-xl border border-gray-300 pl-9 pr-3 py-2 outline-none focus:border-[#004c91]"
              />
            </div>
          </div>
        </div>

        {loading ? (
          <div className="py-16 text-center text-slate-500 font-medium">Đang tải danh sách...</div>
        ) : error ? (
          <div className="py-16 text-center text-slate-500 font-medium">{error}</div>
        ) : items.length === 0 ? (
          <div className="py-16 text-center">
            <Camera className="w-10 h-10 mx-auto text-slate-300 mb-3" />
            <p className="text-slate-600 font-medium">
              {search ? 'Không tìm thấy đoàn khách phù hợp.' : 'Bạn chưa tham gia chuyến thăm nào.'}
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-sm min-w-[720px]">
              <thead className="bg-gray-100/50 text-[11px] uppercase tracking-wider text-gray-500 font-extrabold">
                <tr className="border-b border-gray-200">
                  <th className="px-4 sm:px-6 py-3 w-14">STT</th>
                  <th className="px-4 py-3">Tên đoàn khách</th>
                  <th className="px-4 py-3 w-48">Tên thư mục</th>
                  <th className="px-4 py-3 w-56 text-right">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {items.map((item, idx) => (
                  <tr key={item.visitInstanceId} className="hover:bg-gray-50/55 transition-colors">
                    <td className="px-4 sm:px-6 py-3 font-bold text-slate-500">
                      {(page - 1) * PAGE_SIZE + idx + 1}
                    </td>
                    <td className="px-4 py-3">
                      <p className="font-bold text-slate-800">{item.delegationName || '—'}</p>
                      <p className="text-xs text-slate-500 font-semibold">
                        {item.campusName || '—'} · {INSTANCE_STATUS_LABELS[item.instanceStatus] || item.instanceStatus}
                        {item.activePhotoCount > 0 && <> · {item.activePhotoCount} ảnh</>}
                      </p>
                    </td>
                    <td className="px-4 py-3 font-semibold text-slate-700">
                      {item.folderName || <span className="text-slate-400 italic">Chưa có thư mục</span>}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex items-center justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => setModal({ item, mode: 'edit' })}
                          className="px-3 py-1.5 rounded-lg text-xs font-bold text-white bg-[#f37021] hover:bg-[#e0611d] inline-flex items-center gap-1.5 transition-colors"
                        >
                          <Pencil className="w-3.5 h-3.5" /> Chỉnh sửa
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!loading && !error && totalPages > 1 && (
          <div className="px-4 sm:px-6 py-3 border-t border-slate-100 flex items-center justify-between gap-3">
            <p className="text-xs font-semibold text-slate-500">
              Trang {page}/{totalPages} · {data?.totalCount ?? 0} đoàn khách
            </p>
            <div className="flex items-center gap-2">
              <button
                type="button"
                disabled={page <= 1}
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                className="p-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:opacity-40 transition-colors"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <button
                type="button"
                disabled={page >= totalPages}
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                className="p-2 rounded-lg border border-slate-200 text-slate-600 hover:bg-slate-50 disabled:opacity-40 transition-colors"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal Xem chi tiết / Chỉnh sửa — dùng chung VisitPhotoPanel với trang Đóng góp kết quả */}
      {modal && (
        <div className="fixed inset-0 bg-black/40 flex items-center justify-center z-[110] px-4 py-8">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-5xl max-h-full flex flex-col">
            <div className="px-6 py-4 border-b border-slate-100 flex items-start justify-between gap-3">
              <div className="min-w-0">
                <h3 className="text-base font-black text-[#004c91]">
                  {modal.mode === 'edit' ? 'Chỉnh sửa ảnh đoàn khách' : 'Ảnh đoàn khách'}
                </h3>
                <p className="text-sm font-bold text-slate-700 truncate">{modal.item.delegationName}</p>
                <p className="text-xs font-semibold text-slate-500">{modal.item.campusName || '—'}</p>
              </div>
              <button
                type="button"
                onClick={() => closeModal(modal.mode === 'edit')}
                className="p-2 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors"
                aria-label="Đóng"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="px-6 py-5 overflow-y-auto">
              <VisitPhotoPanel
                visitInstanceId={modal.item.visitInstanceId}
                mode={modal.mode}
                onForbidden={() => {
                  toast.error('Bạn không có quyền xem ảnh của đoàn khách này.');
                  setModal(null);
                }}
              />
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
