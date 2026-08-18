import { useState, useEffect } from 'react';
import { Search, Plus, Eye, Edit2, ChevronLeft, ChevronRight, ArrowUpDown } from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import httpClient from '../../../shared/api/httpClient';

import { formatVietnamDate } from '../../../shared/utils/vietnamTime';
interface NewsAvailableActions {
  canViewDetail: boolean;
  canEdit: boolean;
  canApprove: boolean;
  canReject: boolean;
  canHide: boolean;
  canShow: boolean;
}

interface NewsItem {
  newsId: number;
  title: string;
  description?: string;
  coverImageUrl?: string;
  coverThumbnailUrl?: string;
  campusId?: number;
  campusName?: string;
  visitInstanceId?: number;
  authorUserId: number;
  authorName: string;
  createdAt: string;
  updatedAt?: string;
  status: string;
  statusLabel: string;
  reviewedBy?: number;
  reviewedByName?: string;
  reviewedAt?: string;
  availableActions: NewsAvailableActions;
}

interface NewsPagination {
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

interface NewsListResponse {
  viewerMode: string;
  canCreateNews: boolean;
  items: NewsItem[];
  pagination: NewsPagination;
}

const STATUS_OPTIONS = [
  { value: '', label: 'Tất cả trạng thái' },
  { value: 'PENDING_REVIEW', label: 'Chờ Duyệt' },
  { value: 'PUBLISHED', label: 'Đã Duyệt' },
  { value: 'REJECTED', label: 'Từ Chối' },
  { value: 'HIDDEN', label: 'Ẩn' },
];

const HO_STATUS_OPTIONS = [
  { value: '', label: 'Tất cả trạng thái' },
  { value: 'PUBLISHED', label: 'Đã Duyệt' },
];

function formatDate(dateStr?: string): string {
  return formatVietnamDate(dateStr);
}

export function NewsManagement() {
  const navigate = useNavigate();

  // Đến từ 1 thông báo cụ thể (?newsId=...): chỉ hiển thị đúng bài đó thay vì cả danh sách.
  // Nút "Xem tất cả" (hoặc đổi filter/tìm kiếm) xoá param này để xem lại toàn bộ.
  const [searchParams, setSearchParams] = useSearchParams();
  const notificationNewsId = searchParams.get('newsId') || '';

  const [response, setResponse] = useState<NewsListResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedStatus, setSelectedStatus] = useState('');
  const [selectedLang, setSelectedLang] = useState<'vi' | 'en'>('vi');
  const [sortDirection, setSortDirection] = useState<'asc' | 'desc' | ''>('');

  // Debounce search 400ms
  useEffect(() => {
    const t = setTimeout(() => {
      setDebouncedSearch(searchQuery.trim());
      setPage(1);
    }, 400);
    return () => clearTimeout(t);
  }, [searchQuery]);

  useEffect(() => {
    let cancelled = false;

    const fetchNews = async () => {
      setLoading(true);
      setError(null);
      try {
        const params: Record<string, string | number> = {
          page,
          pageSize: itemsPerPage,
          languageCode: selectedLang,
        };
        if (debouncedSearch) params.keyword = debouncedSearch;
        if (selectedStatus) params.status = selectedStatus;
        if (notificationNewsId) params.newsId = notificationNewsId;
        if (sortDirection) {
          params.sortBy = 'createdAt';
          params.sortDirection = sortDirection;
        }

        const { data } = await httpClient.get<NewsListResponse>('/news', { params });
        if (!cancelled) setResponse(data);
      } catch (err: any) {
        if (!cancelled) {
          setError(err?.response?.data?.message ?? 'Không thể tải danh sách tin tức.');
          setResponse(null);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    fetchNews();
    return () => { cancelled = true; };
  }, [page, itemsPerPage, debouncedSearch, selectedStatus, selectedLang, sortDirection, notificationNewsId]);


  // Bấm thông báo khi đang đứng ở trang khác trang 1 → về trang 1 để thấy đúng bài.
  useEffect(() => {
    if (notificationNewsId) setPage(1);
  }, [notificationNewsId]);

  const clearNotificationFilter = () => {
    const params = new URLSearchParams(searchParams);
    params.delete('newsId');
    setSearchParams(params, { replace: true });
    setPage(1);
  };

  const viewerMode = response?.viewerMode ?? '';
  const isHO = viewerMode === 'HO_READONLY';
  const canCreateNews = response?.canCreateNews ?? false;
  const items = response?.items ?? [];
  const pagination = response?.pagination;
  const totalPages = pagination?.totalPages ?? 0;
  const statusOptions = isHO ? HO_STATUS_OPTIONS : STATUS_OPTIONS;

  const getStatusBadge = (statusLabel: string) => {
    switch (statusLabel) {
      case 'Đã Duyệt':
        return <span className="inline-block px-3 py-1.5 bg-[#eaffe4] text-[#0aa14f] font-bold rounded-full text-[12px] border border-[#ceefda] whitespace-nowrap">Đã Duyệt</span>;
      case 'Chờ Duyệt':
        return <span className="inline-block px-3 py-1.5 bg-[#fff6e0] text-[#cf8e00] font-bold rounded-full text-[12px] border border-[#ffecba] whitespace-nowrap">Chờ Duyệt</span>;
      case 'Từ Chối':
        return <span className="inline-block px-3 py-1.5 bg-red-50 text-red-600 font-bold rounded-full text-[12px] border border-red-100 whitespace-nowrap">Từ Chối</span>;
      case 'Ẩn':
        return <span className="inline-block px-3 py-1.5 bg-gray-100 text-gray-600 font-bold rounded-full text-[12px] border border-gray-200 whitespace-nowrap">Ẩn</span>;
      default:
        return <span className="inline-block px-3 py-1.5 bg-gray-100 text-gray-500 font-bold rounded-full text-[12px] border border-gray-200 whitespace-nowrap">{statusLabel}</span>;
    }
  };

  const renderActions = (item: NewsItem) => {
    const { availableActions, newsId } = item;
    const btnClass = 'p-1.5 rounded-lg transition-colors';

    return (
      <div className="flex items-center justify-center gap-1">
        {availableActions.canViewDetail && (
          <button
            onClick={() => navigate(`/dashboard/news/${newsId}`)}
            className={`${btnClass} hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400`}
            title="Xem chi tiết"
          >
            <Eye className="w-4 h-4" />
          </button>
        )}
        {availableActions.canEdit && (
          <button
            onClick={() => navigate(`/dashboard/news/${newsId}/edit`)}
            className={`${btnClass} hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400`}
            title="Chỉnh sửa"
          >
            <Edit2 className="w-4 h-4" />
          </button>
        )}
      </div>
    );
  };

  return (
    <div className="w-full pb-12">
      {/* Breadcrumb */}
      <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý tin tức</span>
      </div>
      <div className="border-b border-gray-100 pb-3 mb-4 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tin tức</h1>
      </div>

      {/* Toolbar */}
      <div className="flex items-center flex-wrap gap-3 mb-4">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
          <input
            type="text"
            placeholder="Tìm kiếm tin tức..."
            value={searchQuery}
            onChange={(e) => { setSearchQuery(e.target.value); if (notificationNewsId) clearNotificationFilter(); }}
            className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-md focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm"
          />
        </div>

        <select
          value={selectedStatus}
          onChange={(e) => { setSelectedStatus(e.target.value); setPage(1); if (notificationNewsId) clearNotificationFilter(); }}
          className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-normal shadow-sm transition-colors cursor-pointer outline-none"
        >
          {statusOptions.map(opt => (
            <option key={opt.value} value={opt.value}>{opt.label}</option>
          ))}
        </select>

        {/* Language Switcher next to filters */}
        <div className="flex items-center bg-gray-100 p-1 rounded-md border border-gray-300 shrink-0 shadow-sm">
          <button
            type="button"
            onClick={() => { setSelectedLang('vi'); setPage(1); }}
            className={`px-3 py-1 rounded text-xs font-bold transition-all ${
              selectedLang === 'vi' ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:text-gray-900'
            }`}
          >
            VI
          </button>
          <button
            type="button"
            onClick={() => { setSelectedLang('en'); setPage(1); }}
            className={`px-3 py-1 rounded text-xs font-bold transition-all ${
              selectedLang === 'en' ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:text-gray-900'
            }`}
          >
            EN
          </button>
        </div>


        {canCreateNews && (
          <button
            onClick={() => navigate('/dashboard/news/create')}
            className="ml-auto bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-md font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
          >
            <Plus className="w-4 h-4 flex-shrink-0" />
            Thêm tin tức mới
          </button>
        )}
      </div>

      {notificationNewsId && (
        <div className="flex items-center justify-between gap-3 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3 text-sm mb-4">
          <span className="font-normal text-blue-800">
            Đang hiển thị đúng tin tức từ thông báo bạn vừa bấm.
          </span>
          <button
            onClick={clearNotificationFilter}
            className="shrink-0 rounded-lg border border-blue-300 bg-white px-3 py-1.5 text-xs font-bold text-blue-700 hover:bg-blue-100 transition-colors"
          >
            Xem tất cả
          </button>
        </div>
      )}

      {/* Table */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse min-w-[1000px]">
            <thead>
              <tr className="bg-[#004c91] text-white text-[12px] tracking-wide uppercase text-center">
                <th className="p-3 font-bold w-[60px]">STT</th>
                <th className="p-3 font-bold w-[30%] text-left pl-6">TIÊU ĐỀ</th>
                <th className="p-3 font-bold w-[25%] text-left pl-6">MÔ TẢ</th>
                <th className="p-3 font-bold w-[130px] whitespace-nowrap">NGƯỜI TẠO</th>
                <th
                  className="p-3 font-bold w-[150px] whitespace-nowrap cursor-pointer hover:bg-[#003a70] bg-[#004c91] text-white transition-colors select-none group"
                  onClick={() => {
                    setSortDirection(prev => prev === '' ? 'asc' : prev === 'asc' ? 'desc' : '');
                    setPage(1);
                  }}
                >
                  <div className="flex items-center justify-center gap-1 text-white">
                    NGÀY TẠO
                    <ArrowUpDown className={`w-3.5 h-3.5 transition-colors ${sortDirection ? 'text-white' : 'text-white/50 group-hover:text-white'}`} />
                  </div>
                </th>
                <th className="p-3 font-bold w-[130px] whitespace-nowrap">TRẠNG THÁI</th>
                <th className="p-3 font-bold w-[140px]">HÀNH ĐỘNG</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {loading ? (
                <tr>
                  <td colSpan={7} className="py-16 text-center text-gray-400">
                    <div className="flex items-center justify-center gap-2">
                      <div className="w-5 h-5 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin"></div>
                      <span>Đang tải...</span>
                    </div>
                  </td>
                </tr>
              ) : error ? (
                <tr>
                  <td colSpan={7} className="py-16 text-center text-red-500">{error}</td>
                </tr>
              ) : items.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-16 text-center text-gray-400">Không có dữ liệu.</td>
                </tr>
              ) : (
                items.map((item, index) => (
                  <tr key={item.newsId} className="hover:bg-gray-50/80 transition-colors group text-center">
                    <td className="p-3 align-middle font-bold text-gray-500 text-xs">{(page - 1) * itemsPerPage + index + 1}</td>
                    <td className="p-3 align-middle font-bold text-gray-800 text-[13px] text-left pl-6">
                      <div className="line-clamp-2 leading-relaxed">{item.title}</div>
                    </td>
                    <td className="p-3 align-middle text-gray-500 text-[12px] leading-relaxed text-left pl-6">
                      <div className="line-clamp-2">{item.description}</div>
                    </td>
                    <td className="p-3 align-middle whitespace-nowrap">
                      <div className="font-normal text-[#004c91] text-[13px]">{item.authorName}</div>
                      {isHO && item.campusName && (
                        <div className="text-[11px] text-gray-500 mt-0.5">{item.campusName}</div>
                      )}
                    </td>
                    <td className="p-3 align-middle whitespace-nowrap">
                      <div className="font-normal text-gray-600 text-[13px]">{formatDate(item.createdAt)}</div>
                    </td>
                    <td className="p-3 align-middle whitespace-nowrap">{getStatusBadge(item.statusLabel)}</td>
                    <td className="p-3 align-middle whitespace-nowrap">{renderActions(item)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Pagination */}
      {!loading && !error && (
        <div className="flex items-center justify-between mt-6">
          <div className="flex items-center gap-3 text-sm text-gray-600 font-normal">
            <span>Hiển thị</span>
            <select
              value={itemsPerPage}
              onChange={(e) => { setItemsPerPage(Number(e.target.value)); setPage(1); }}
              className="border border-gray-300 bg-white rounded-lg px-2 py-1 outline-none focus:border-[#004c91] hover:border-gray-400 transition-colors cursor-pointer text-gray-700"
            >
              <option value="5">5</option>
              <option value="10">10</option>
              <option value="20">20</option>
              <option value="50">50</option>
            </select>
            <span>bài / trang</span>
            {pagination && (
              <span className="text-gray-400 ml-2">| Tổng: {pagination.totalItems} bài</span>
            )}
          </div>

          <div className="flex items-center gap-1.5">
            <button
              className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm"
              onClick={() => setPage(p => p - 1)}
              disabled={!pagination?.hasPrevious}
            >
              <ChevronLeft className="w-4 h-4" />
            </button>

            <div className="flex items-center gap-1">
              {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (
                <button
                  key={p}
                  className={`w-9 h-9 rounded-xl font-bold text-sm transition-colors ${page === p ? 'bg-[#004c91] text-white shadow-sm border border-[#004c91]' : 'text-gray-600 hover:bg-gray-100 border border-transparent'}`}
                  onClick={() => setPage(p)}
                >
                  {p}
                </button>
              ))}
            </div>

            <button
              className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm"
              onClick={() => setPage(p => p + 1)}
              disabled={!pagination?.hasNext}
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
