import React, { useState, useEffect } from 'react';
import { Search, Plus, Eye, ChevronLeft, ChevronRight, X, Loader2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import toast, { Toaster } from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';

const FAQ_TYPE_OPTIONS = [
  { value: '', label: 'Tất cả loại bài' },
  { value: 'ACCOUNT_ACCESS', label: 'Tài khoản & truy cập' },
  { value: 'VISIT_REQUEST', label: 'Đăng ký tham quan' },
  { value: 'DELEGATION_MANAGEMENT', label: 'Quản lý đoàn' },
  { value: 'LOGISTICS_RESOURCE', label: 'Hậu cần' },
  { value: 'DOCUMENT_MEDIA', label: 'Tài liệu' },
  { value: 'NOTIFICATION_EMAIL', label: 'Thông báo' },
  { value: 'OTHER', label: 'Khác' },
];

interface FaqItem {
  faqId: number;
  faqType: string;
  faqTypeLabel: string;
  question: string;
  answer: string;
  displayOrder: number;
  status: string;
  statusLabel: string;
  createdAt: string;
  createdBy?: number;
  createdByName?: string;
  updatedAt?: string;
  updatedBy?: number;
  updatedByName?: string;
}

interface PaginatedResponse {
  items: FaqItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export function FAQManagement() {
  const navigate = useNavigate();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isHO = userRole === 'HO';
  const isAdmin = userRole === 'ADMIN' || (userRole === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER');
  const isFullAccess = isHO || isAdmin;

  const [faqs, setFaqs] = useState<FaqItem[]>([]);
  const [totalItems, setTotalItems] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(10);
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [selectedType, setSelectedType] = useState('');
  const [selectedStatus, setSelectedStatus] = useState('');

  const [togglingIds, setTogglingIds] = useState<Set<number>>(new Set());

  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [newFAQ, setNewFAQ] = useState({ question: '', answer: '', type: 'ACCOUNT_ACCESS', status: 'PUBLISHED' });
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  // Debounce search 400ms
  useEffect(() => {
    const t = setTimeout(() => {
      setDebouncedSearch(searchQuery.trim());
      setPage(1);
    }, 400);
    return () => clearTimeout(t);
  }, [searchQuery]);

  // Fetch từ backend
  useEffect(() => {
    let cancelled = false;

    const fetchFaqs = async () => {
      setLoading(true);
      setError(null);
      try {
        const params: Record<string, string | number> = {
          page,
          pageSize: itemsPerPage,
        };
        if (debouncedSearch) params.keyword = debouncedSearch;
        if (selectedType) params.faqType = selectedType;
        if (selectedStatus) params.status = selectedStatus;

        const { data } = await httpClient.get<PaginatedResponse>('/faqs', { params });
        if (!cancelled) {
          setFaqs(data.items ?? []);
          setTotalItems(data.totalItems ?? 0);
          setTotalPages(data.totalPages ?? 0);
        }
      } catch (err: any) {
        if (!cancelled) {
          setError(err?.response?.data?.message ?? 'Không thể tải danh sách FAQ.');
          setFaqs([]);
          setTotalItems(0);
          setTotalPages(0);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    fetchFaqs();
    return () => { cancelled = true; };
  }, [page, itemsPerPage, debouncedSearch, selectedType, selectedStatus]);

  const closeCreateModal = () => {
    setIsCreateModalOpen(false);
    setNewFAQ({ question: '', answer: '', type: 'ACCOUNT_ACCESS', status: 'PUBLISHED' });
    setCreateError(null);
  };

  const handleCreate = async () => {
    if (isSubmitting) return;
    setIsSubmitting(true);
    setCreateError(null);
    try {
      const { data } = await httpClient.post<FaqItem>('/faqs', {
        faqType: newFAQ.type,
        question: newFAQ.question.trim(),
        answer: newFAQ.answer.trim(),
        status: newFAQ.status,
      });
      closeCreateModal();
      toast.success('Tạo FAQ thành công!', { duration: 3000 });
      setPage(1);
      setFaqs(prev => [data, ...prev].slice(0, itemsPerPage));
      setTotalItems(t => t + 1);
    } catch (err: any) {
      const msg = err?.response?.data?.message
        ?? err?.response?.data?.title
        ?? 'Không thể tạo FAQ. Vui lòng thử lại.';
      setCreateError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleVisibility = async (item: FaqItem) => {
    if (togglingIds.has(item.faqId)) return;
    setTogglingIds(prev => new Set(prev).add(item.faqId));

    // Optimistic update
    const newStatus = item.status === 'PUBLISHED' ? 'HIDDEN' : 'PUBLISHED';
    const newStatusLabel = newStatus === 'PUBLISHED' ? 'Hiển thị' : 'Ẩn';
    setFaqs(prev => prev.map(f =>
      f.faqId === item.faqId ? { ...f, status: newStatus, statusLabel: newStatusLabel } : f
    ));

    try {
      await httpClient.patch('/faqs/visibility', { faqId: item.faqId });
    } catch {
      // Revert nếu lỗi
      setFaqs(prev => prev.map(f =>
        f.faqId === item.faqId ? { ...f, status: item.status, statusLabel: item.statusLabel } : f
      ));
    } finally {
      setTogglingIds(prev => { const s = new Set(prev); s.delete(item.faqId); return s; });
    }
  };

  const getStatusBadge = (statusLabel: string) => {
    if (statusLabel === 'Hiển thị') {
      return <span className="inline-block px-3 py-1.5 bg-[#eaffe4] text-[#0aa14f] font-bold rounded-full text-[12px] border border-[#ceefda] whitespace-nowrap">Hiển thị</span>;
    }
    return <span className="inline-block px-3 py-1.5 bg-gray-100 text-gray-600 font-bold rounded-full text-[12px] border border-gray-200 whitespace-nowrap">Ẩn</span>;
  };

  const renderActions = (item: FaqItem) => {
    const isPublished = item.status === 'PUBLISHED';
    return (
      <div className="flex items-center gap-1 justify-center">
        <button
          onClick={() => navigate(`/dashboard/faq/${item.faqId}`)}
          className="p-1.5 rounded-lg transition-colors hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400"
          title="Xem chi tiết"
        >
          <Eye className="w-[16px] h-[16px]" />
        </button>
        {isFullAccess && (
          <button
            className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ml-2 ${togglingIds.has(item.faqId) ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer'} ${isPublished ? 'bg-[#004c91]' : 'bg-gray-300'}`}
            title={isPublished ? 'Ẩn FAQ' : 'Hiển thị FAQ'}
            disabled={togglingIds.has(item.faqId)}
            onClick={() => handleToggleVisibility(item)}
          >
            <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${isPublished ? 'translate-x-4' : 'translate-x-0'}`} />
          </button>
        )}
      </div>
    );
  };

  if (!['ADMIN', 'HO', 'STAFF', 'DEPARTMENT', 'STUDENT'].includes(userRole || '')) {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-800 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500">Trang này không dành cho vai trò của bạn.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen">
      <Toaster position="top-right" />
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Quản lý FAQ</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý FAQ</h1>
      </div>

      {/* Filters */}
      <div className="flex flex-col md:flex-row gap-4 items-center justify-between relative z-20">
        <div className="flex items-center gap-4 w-full md:w-auto flex-1">
          <div className="relative w-full md:max-w-[400px]">
            {loading && debouncedSearch
              ? <Loader2 className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400 animate-spin" />
              : <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            }
            <input
              type="text"
              placeholder="Tìm kiếm câu hỏi, câu trả lời..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all shadow-sm"
            />
          </div>

          <select
            value={selectedType}
            onChange={(e) => { setSelectedType(e.target.value); setPage(1); }}
            className="w-full md:w-[180px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
          >
            {FAQ_TYPE_OPTIONS.map(o => (
              <option key={o.value} value={o.value}>{o.label}</option>
            ))}
          </select>

          <select
            value={selectedStatus}
            onChange={(e) => { setSelectedStatus(e.target.value); setPage(1); }}
            className="w-full md:w-[160px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="PUBLISHED">Hiển thị</option>
            <option value="HIDDEN">Ẩn</option>
          </select>
        </div>

        {isFullAccess && (
          <button
            onClick={() => setIsCreateModalOpen(true)}
            className="flex items-center justify-center gap-2 bg-[#f37021] hover:bg-[#e06218] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all shrink-0"
          >
            <Plus className="w-4 h-4" /> Thêm mới FAQ
          </button>
        )}
      </div>

      {/* Table */}
      <div className="bg-white border rounded-2xl shadow-sm border-gray-100 relative z-10 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px]">
            <thead className="bg-[#004c91] text-white">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-16 text-center">STT</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[18%]">Loại</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[32%]">Câu hỏi</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[20%]">Trả lời</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Trạng thái</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {loading ? (
                Array.from({ length: itemsPerPage }).map((_, i) => (
                  <tr key={i}>
                    <td colSpan={6} className="px-4 py-4">
                      <div className="h-5 bg-gray-100 rounded animate-pulse" />
                    </td>
                  </tr>
                ))
              ) : error ? (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-red-500 font-medium">
                    {error}
                  </td>
                </tr>
              ) : faqs.length > 0 ? (
                faqs.map((item, index) => (
                  <tr key={item.faqId} className="hover:bg-blue-50/30 transition-colors">
                    <td className="px-4 py-4 whitespace-nowrap text-sm font-medium text-gray-500 text-center">
                      {(page - 1) * itemsPerPage + index + 1}
                    </td>
                    <td className="px-4 py-4 whitespace-nowrap">
                      <span className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-semibold bg-gray-50 text-gray-600 border border-gray-200">
                        {item.faqTypeLabel}
                      </span>
                    </td>
                    <td className="px-4 py-4">
                      <p className="text-sm font-bold text-gray-900 line-clamp-2">{item.question}</p>
                    </td>
                    <td className="px-4 py-4">
                      <p className="text-sm text-gray-500 line-clamp-2">{item.answer}</p>
                    </td>
                    <td className="px-4 py-4 whitespace-nowrap text-center">
                      {getStatusBadge(item.statusLabel)}
                    </td>
                    <td className="px-4 py-4 whitespace-nowrap">
                      {renderActions(item)}
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-gray-500 font-medium">
                    Không tìm thấy FAQ nào
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Footer / Pagination */}
        <div className="px-4 py-3 border-t border-gray-100 bg-gray-50/50 flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2 text-sm text-gray-600">
            <span>Hiển thị</span>
            <select
              className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-medium"
              value={itemsPerPage}
              onChange={(e) => { setItemsPerPage(Number(e.target.value)); setPage(1); }}
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
            <span>bản ghi / trang</span>
            {!loading && totalItems > 0 && (
              <span className="ml-2 text-gray-400">— {totalItems} kết quả</span>
            )}
          </div>

          <div className="flex items-center gap-1.5">
            <button
              className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-100 text-gray-500 disabled:opacity-50 disabled:cursor-not-allowed bg-white"
              disabled={page === 1 || loading}
              onClick={() => setPage(p => p - 1)}
            >
              <ChevronLeft className="w-5 h-5" />
            </button>
            {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (
              <button
                key={p}
                className={`w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold transition-colors ${
                  page === p ? 'bg-[#004c91] text-white border border-[#004c91]' : 'bg-white border border-gray-200 text-gray-600 hover:bg-gray-50'
                }`}
                onClick={() => setPage(p)}
              >
                {p}
              </button>
            ))}
            <button
              className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-100 text-gray-500 disabled:opacity-50 disabled:cursor-not-allowed bg-white"
              disabled={page === totalPages || totalPages === 0 || loading}
              onClick={() => setPage(p => p + 1)}
            >
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>
        </div>
      </div>

      {/* Create FAQ Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-xl font-bold text-[#004c91] flex items-center gap-2">
                <Plus className="w-5 h-5" /> Thêm mới FAQ
              </h3>
              <button onClick={closeCreateModal} className="p-1.5 text-gray-400 hover:text-gray-600 bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>

            <div className="p-6 space-y-4">
              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Loại FAQ<span className="text-red-500 ml-1">*</span></label>
                <select
                  className="w-full px-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91]"
                  value={newFAQ.type}
                  onChange={(e) => setNewFAQ({ ...newFAQ, type: e.target.value })}
                >
                  {FAQ_TYPE_OPTIONS.filter(o => o.value).map(o => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Câu hỏi<span className="text-red-500 ml-1">*</span></label>
                <textarea
                  rows={3}
                  className="w-full px-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] resize-none"
                  placeholder="Nhập câu hỏi..."
                  value={newFAQ.question}
                  onChange={(e) => setNewFAQ({ ...newFAQ, question: e.target.value })}
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Trả lời<span className="text-red-500 ml-1">*</span></label>
                <textarea
                  rows={3}
                  className="w-full px-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] resize-none"
                  placeholder="Nhập câu trả lời..."
                  value={newFAQ.answer}
                  onChange={(e) => setNewFAQ({ ...newFAQ, answer: e.target.value })}
                />
              </div>

              {/* Status toggle */}
              <div className="flex items-center justify-between p-3 bg-gray-50 rounded-xl border border-gray-200">
                <div>
                  <p className="text-sm font-bold text-gray-900">Trạng thái hiển thị<span className="text-red-500 ml-1">*</span></p>
                  <p className="text-xs text-gray-500 mt-0.5">
                    {newFAQ.status === 'PUBLISHED' ? 'Hiển thị trên trang FAQ công khai' : 'Ẩn khỏi trang FAQ công khai'}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setNewFAQ({ ...newFAQ, status: newFAQ.status === 'PUBLISHED' ? 'HIDDEN' : 'PUBLISHED' })}
                  className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ${newFAQ.status === 'PUBLISHED' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
                >
                  <span className={`pointer-events-none inline-block h-5 w-5 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${newFAQ.status === 'PUBLISHED' ? 'translate-x-5' : 'translate-x-0'}`} />
                </button>
              </div>

              {createError && (
                <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-xl px-3 py-2">{createError}</p>
              )}
            </div>

            <div className="p-5 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3">
              <button
                onClick={closeCreateModal}
                disabled={isSubmitting}
                className="px-5 py-2 bg-white border border-gray-200 text-gray-600 font-bold rounded-xl hover:bg-gray-50 disabled:opacity-50"
              >
                Hủy
              </button>
              <button
                onClick={handleCreate}
                disabled={!newFAQ.question.trim() || !newFAQ.answer.trim() || isSubmitting}
                className="flex items-center gap-2 px-5 py-2 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003366] disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isSubmitting && <Loader2 className="w-4 h-4 animate-spin" />}
                Tạo mới
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
