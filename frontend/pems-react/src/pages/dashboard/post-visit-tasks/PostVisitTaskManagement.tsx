/**
 * "Quản lý việc sau tiếp khách" — action items (minute_action_items) do CHÍNH người đang đăng nhập
 * phụ trách (không xem theo team/leader). Layout/màu sắc theo đúng pattern các trang quản lý khác
 * (Quản lý biên bản/tài liệu): filter bar + bảng + phân trang 5/10/20/50.
 */
import React, { useCallback, useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Search, Filter, ClipboardList, Eye, CheckSquare, Square, ChevronLeft, ChevronRight, X, Calendar, Building2, Bell } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import toast from 'react-hot-toast';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import type { MyActionItem, MyActionItemsFilterParams } from '../../../features/delegations/types/delegations.types';
import { formatVietnamDateTime, vietnamNowDateTimeLocal, toVietnamDateTimeLocalInput } from '../../../shared/utils/vietnamTime';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';

const STATUS_META: Record<string, { label: string; cls: string }> = {
  TODO: { label: 'Chưa làm', cls: 'bg-red-50 text-red-600 border-red-200' },
  IN_PROGRESS: { label: 'Chưa làm', cls: 'bg-red-50 text-red-600 border-red-200' },
  DONE: { label: 'Hoàn thành', cls: 'bg-green-50 text-green-700 border-green-200' },
  CANCELLED: { label: 'Đã hủy', cls: 'bg-slate-100 text-slate-500 border-slate-200' },
};
const isOpenStatus = (status: string) => status === 'TODO' || status === 'IN_PROGRESS';
const isOverdue = (item: MyActionItem) =>
  isOpenStatus(item.status) && !!item.dueDate && toVietnamDateTimeLocalInput(item.dueDate) <= vietnamNowDateTimeLocal();

const DEFAULT_FILTERS: MyActionItemsFilterParams = { page: 1, pageSize: 10, status: 'ALL', sortDir: 'asc' };

export function PostVisitTaskManagement() {
  const navigate = useNavigate();
  // Deep-link từ chuông thông báo "Bạn được giao đầu việc" / "Đến hạn hoàn thành công việc":
  // ?actionItemId=123 → chỉ hiển thị đúng 1 đầu việc đó, bỏ qua mọi filter mặc định.
  const [searchParams, setSearchParams] = useSearchParams();
  const deepLinkParam = searchParams.get('actionItemId');
  const deepLinkId = deepLinkParam && /^\d+$/.test(deepLinkParam) ? Number(deepLinkParam) : null;

  const [filters, setFilters] = useState<MyActionItemsFilterParams>(() =>
    deepLinkId ? { page: 1, pageSize: 10, actionItemId: deepLinkId } : DEFAULT_FILTERS
  );
  const [searchInput, setSearchInput] = useState('');

  // Filter thủ công (search/status/ngày/sắp xếp/số dòng) luôn thoát chế độ deep-link — giữ URL
  // đồng bộ với state để reload trang không tự nhảy lại đúng 1 item cũ.
  useEffect(() => {
    if (!filters.actionItemId && searchParams.has('actionItemId')) {
      const next = new URLSearchParams(searchParams);
      next.delete('actionItemId');
      setSearchParams(next, { replace: true });
    }
  }, [filters.actionItemId, searchParams, setSearchParams]);

  const showAllTasks = () => {
    setSearchInput('');
    setFilters(DEFAULT_FILTERS);
  };
  const [showAdvancedFilters, setShowAdvancedFilters] = useState(false);
  const [loading, setLoading] = useState(false);
  const [items, setItems] = useState<MyActionItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [busyId, setBusyId] = useState<number | null>(null);
  const [detailItem, setDetailItem] = useState<MyActionItem | null>(null);
  const [detailVisitRequestId, setDetailVisitRequestId] = useState<number | null>(null);

  useEffect(() => {
    const t = setTimeout(() => setFilters((prev) => {
      // Bỏ qua lần chạy đầu (mount) khi đang ở chế độ deep-link và search vẫn rỗng — chỉ thoát
      // deep-link khi user thật sự gõ tìm kiếm.
      if (prev.actionItemId && searchInput === '') return prev;
      const { actionItemId, ...rest } = prev;
      return { ...rest, q: searchInput, page: 1 };
    }), 500);
    return () => clearTimeout(t);
  }, [searchInput]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await delegationsApi.minutes.myActionItems(filters);
      setItems(res.items || []);
      setTotalCount(res.totalCount || 0);
    } catch {
      setItems([]);
      setTotalCount(0);
      toast.error('Không thể tải danh sách công việc. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  }, [filters]);
  useEffect(() => { void load(); }, [load]);

  const handleFilterChange = (key: keyof MyActionItemsFilterParams, value: any) =>
    setFilters((prev) => {
      const { actionItemId, ...rest } = prev;
      return { ...rest, [key]: value, page: 1 };
    });
  const clearAllFilters = () => showAllTasks();

  const currentPage = filters.page || 1;
  const pageSize = filters.pageSize || 10;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const handlePageChange = (p: number) => {
    if (p > 0 && p <= totalPages) setFilters((prev) => ({ ...prev, page: p }));
  };

  const handleMarkDone = async (item: MyActionItem) => {
    if (busyId != null) return;
    setBusyId(item.actionItemId);
    try {
      await delegationsApi.minutes.markActionItemDone(item.actionItemId);
      toast.success(`Đã đánh dấu hoàn thành "${item.title}".`);
      await load();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Không thể đánh dấu hoàn thành. Vui lòng thử lại.');
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="w-full pb-12 flex flex-col space-y-4 animate-in fade-in duration-300">
      {/* Header & Navigation Layer */}
      <div className="mb-1 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Quản lý việc sau tiếp khách</span>
      </div>

      <div className="border-b border-gray-100 pb-3">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý việc sau tiếp khách</h1>
      </div>

      {/* Banner khi mở từ chuông thông báo — chỉ đang hiện đúng 1 đầu việc, không phải toàn bộ danh sách. */}
      {filters.actionItemId && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-[#004c91]/20 bg-[#004c91]/5 px-4 py-3">
          <div className="flex items-center gap-2 text-sm font-medium text-[#004c91]">
            <Bell className="w-4 h-4 shrink-0" />
            Đang lọc theo thông báo — chỉ hiển thị đúng công việc bạn vừa mở.
          </div>
          <button type="button" onClick={showAllTasks}
            className="text-sm font-semibold text-[#004c91] hover:underline whitespace-nowrap">
            Xem tất cả công việc
          </button>
        </div>
      )}

      {/* Filter bar */}
      <div className="w-full bg-white p-4 rounded-2xl border border-slate-200 shadow-sm space-y-4">
        <div className="flex flex-col lg:flex-row items-center gap-4">
          <div className="relative flex-1 w-full group">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 w-5 h-5 group-focus-within:text-[#004c91] transition-colors pointer-events-none" />
            <input type="text" placeholder="Tìm theo nội dung công việc, tên đoàn khách..."
              value={searchInput} onChange={(e) => setSearchInput(e.target.value)}
              className="w-full pl-11 pr-4 py-2.5 border border-slate-200 rounded-xl bg-white text-sm font-medium text-slate-800 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all placeholder:text-slate-400 shadow-sm" />
          </div>
          <div className="flex items-center gap-3 w-full lg:w-auto overflow-x-auto pb-2 lg:pb-0">
            <select value={filters.status || 'ALL'} onChange={(e) => handleFilterChange('status', e.target.value)}
              className="px-3 py-2.5 border border-slate-200 rounded-xl text-sm font-medium focus:outline-none focus:border-[#004c91] bg-white min-w-[160px]">
              <option value="ALL">Tất cả trạng thái</option>
              <option value="TODO">Chưa làm</option>
              <option value="DONE">Hoàn thành</option>
              <option value="CANCELLED">Đã hủy</option>
            </select>
            <button onClick={() => setShowAdvancedFilters((s) => !s)}
              className={`flex items-center gap-2 px-4 py-2.5 border rounded-xl text-sm font-medium transition-colors whitespace-nowrap ${showAdvancedFilters ? 'border-[#004c91] bg-[#004c91]/5 text-[#004c91]' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'}`}>
              <Filter className="w-4 h-4" /> Lọc nâng cao
            </button>
            <button onClick={clearAllFilters}
              className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm font-medium bg-white text-slate-700 hover:bg-slate-50 transition-colors whitespace-nowrap">
              Đặt lại
            </button>
          </div>
        </div>

        <AnimatePresence>
          {showAdvancedFilters && (
            <motion.div initial={{ height: 0, opacity: 0 }} animate={{ height: 'auto', opacity: 1 }} exit={{ height: 0, opacity: 0 }}
              className="overflow-hidden border-t border-slate-100 pt-4">
              <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-4">
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Hạn từ ngày</label>
                  <input type="date" value={filters.fromDate || ''} onChange={(e) => handleFilterChange('fromDate', e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Hạn đến ngày</label>
                  <input type="date" value={filters.toDate || ''} onChange={(e) => handleFilterChange('toDate', e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm" />
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Sắp xếp theo hạn</label>
                  <select value={filters.sortDir || 'asc'} onChange={(e) => handleFilterChange('sortDir', e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm">
                    <option value="asc">Gần hạn nhất trước</option>
                    <option value="desc">Xa hạn nhất trước</option>
                  </select>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* Table */}
      <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden">
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse min-w-[900px]">
            <thead>
              <tr className="bg-[#004c91] border-b border-[#004c91]">
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap w-16">STT</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap min-w-[280px]">Nội dung công việc</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap min-w-[160px]">Thời hạn</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap min-w-[130px]">Trạng thái</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider text-center whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {loading ? (
                <tr><td colSpan={5} className="px-6 py-12 text-center text-slate-500">Đang tải dữ liệu...</td></tr>
              ) : items.length > 0 ? items.map((item, index) => {
                const meta = STATUS_META[item.status] ?? STATUS_META.TODO;
                const overdue = isOverdue(item);
                return (
                  <tr key={item.actionItemId} className="hover:bg-slate-50/60 transition-colors">
                    <td className="px-4 py-4 text-sm font-semibold text-slate-500">
                      <div className="flex items-center gap-2">
                        {overdue && (
                          <span className="relative flex h-2.5 w-2.5 shrink-0" title="Đã đến hạn">
                            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
                            <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-red-500" />
                          </span>
                        )}
                        {(currentPage - 1) * pageSize + index + 1}
                      </div>
                    </td>
                    <td className="px-4 py-4">
                      <div className={`text-sm font-semibold ${item.status === 'DONE' ? 'line-through text-slate-400' : 'text-slate-800'}`}>{item.title}</div>
                      <div className="text-xs text-slate-400 mt-0.5 flex items-center gap-1">
                        <Building2 className="w-3 h-3" /> {item.delegationName}
                      </div>
                    </td>
                    <td className="px-4 py-4 text-sm font-medium text-slate-600">
                      {item.dueDate ? (
                        <span className={`inline-flex items-center gap-1 ${overdue ? 'text-red-600 font-bold' : ''}`}>
                          <Calendar className="w-3.5 h-3.5" /> {formatVietnamDateTime(item.dueDate)}
                        </span>
                      ) : 'Chưa đặt hạn'}
                    </td>
                    <td className="px-4 py-4">
                      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold border ${meta.cls}`}>{meta.label}</span>
                    </td>
                    <td className="px-4 py-4">
                      <div className="flex items-center justify-center gap-2">
                        <button type="button" title="Xem chi tiết" onClick={() => setDetailItem(item)}
                          className="p-2 rounded-lg border border-slate-200 text-slate-500 hover:text-[#004c91] hover:bg-blue-50 transition-colors">
                          <Eye className="w-4 h-4" />
                        </button>
                        <button type="button"
                          title={item.status === 'DONE' ? 'Đã hoàn thành' : item.status === 'CANCELLED' ? 'Đã hủy' : 'Đánh dấu hoàn thành'}
                          disabled={item.status !== 'TODO' && item.status !== 'IN_PROGRESS' || busyId === item.actionItemId}
                          onClick={() => handleMarkDone(item)}
                          className="p-2 rounded-lg border border-slate-200 text-slate-500 hover:text-green-600 hover:bg-green-50 transition-colors disabled:opacity-40 disabled:cursor-not-allowed disabled:hover:bg-transparent">
                          {item.status === 'DONE' ? <CheckSquare className="w-4 h-4 text-green-600" /> : <Square className="w-4 h-4" />}
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              }) : (
                <tr>
                  <td colSpan={5} className="px-6 py-16">
                    <div className="flex flex-col items-center gap-2">
                      <ClipboardList className="w-10 h-10 text-slate-300" />
                      <p className="text-slate-500 font-medium">Không có công việc nào phù hợp.</p>
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {items.length > 0 && (
          <div className="px-6 py-4 border-t border-slate-200 bg-slate-50/50 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-2 text-sm text-gray-600">
              <span>Hiển thị</span>
              <select value={pageSize} onChange={(e) => handleFilterChange('pageSize', Number(e.target.value))}
                className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-medium">
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
              <span>bản ghi / trang</span>
            </div>
            <div className="flex items-center gap-2">
              <button disabled={currentPage === 1} onClick={() => handlePageChange(currentPage - 1)}
                className="p-1.5 border border-slate-200 rounded-lg text-slate-600 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
                <ChevronLeft className="w-5 h-5" />
              </button>
              <span className="text-sm font-medium px-2">Trang {currentPage} / {totalPages}</span>
              <button disabled={currentPage === totalPages} onClick={() => handlePageChange(currentPage + 1)}
                className="p-1.5 border border-slate-200 rounded-lg text-slate-600 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors">
                <ChevronRight className="w-5 h-5" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Detail modal — task content/note/due date + a link into the full delegation detail. */}
      {detailItem && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-black/40 p-4" onMouseDown={() => setDetailItem(null)}>
          <div className="w-full max-w-lg rounded-2xl bg-white shadow-2xl overflow-hidden" onMouseDown={(e) => e.stopPropagation()}>
            <div className="flex items-start justify-between border-b border-gray-100 px-6 py-4">
              <div className="min-w-0">
                <h3 className="text-base font-bold text-[#004c91]">Chi tiết công việc</h3>
                <p className="mt-0.5 truncate text-xs text-gray-500">{detailItem.delegationName}</p>
              </div>
              <button type="button" onClick={() => setDetailItem(null)} className="rounded-lg p-1.5 text-gray-400 hover:bg-gray-100 hover:text-gray-600">
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="px-6 py-4 space-y-4">
              <button type="button"
                onClick={() => detailItem.visitRequestId ? setDetailVisitRequestId(detailItem.visitRequestId) : toast.error('Không tìm thấy đoàn tiếp khách')}
                className="w-full flex items-center justify-between gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm font-bold text-[#004c91] hover:bg-slate-100 transition-colors">
                <span className="flex items-center gap-2"><Building2 className="w-4 h-4" /> Xem chi tiết đoàn khách</span>
                <span className="text-xs font-semibold text-slate-400">Mở</span>
              </button>
              <div>
                <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400 mb-1">Nội dung công việc</p>
                <p className="text-sm font-semibold text-slate-800">{detailItem.title}</p>
              </div>
              <div>
                <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400 mb-1">Hạn hoàn thành</p>
                <p className="text-sm text-slate-700">{detailItem.dueDate ? formatVietnamDateTime(detailItem.dueDate) : 'Chưa đặt hạn'}</p>
              </div>
              {detailItem.note && (
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400 mb-1">Ghi chú</p>
                  <p className="text-sm text-slate-600 italic whitespace-pre-line">{detailItem.note}</p>
                </div>
              )}
              <div>
                <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400 mb-1">Trạng thái</p>
                <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-xs font-bold border ${(STATUS_META[detailItem.status] ?? STATUS_META.TODO).cls}`}>
                  {(STATUS_META[detailItem.status] ?? STATUS_META.TODO).label}
                </span>
              </div>
            </div>
          </div>
        </div>
      )}

      <SubmittedVisitRequestDetailModal
        isOpen={detailVisitRequestId != null}
        visitRequestId={detailVisitRequestId}
        onClose={() => setDetailVisitRequestId(null)}
      />
    </div>
  );
}
