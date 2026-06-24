import React, { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Search, ChevronDown, HelpCircle, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';
import httpClient from '../shared/api/httpClient';

interface FaqItem {
  faqId: number;
  faqType: string;
  faqTypeLabel: string;
  question: string;
  answer: string;
  displayOrder: number;
  createdAt: string;
}

interface PaginatedFaqResponse {
  items: FaqItem[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

const FAQ_TYPES = [
  { value: 'ALL', label: 'Tất cả' },
  { value: 'ACCOUNT_ACCESS', label: 'Tài khoản & truy cập' },
  { value: 'VISIT_REQUEST', label: 'Đăng ký tham quan' },
  { value: 'DELEGATION_MANAGEMENT', label: 'Quản lý đoàn' },
  { value: 'LOGISTICS_RESOURCE', label: 'Hậu cần' },
  { value: 'DOCUMENT_MEDIA', label: 'Tài liệu' },
  { value: 'NOTIFICATION_EMAIL', label: 'Thông báo' },
  { value: 'OTHER', label: 'Khác' },
];

const PAGE_SIZE = 10;

export function FAQPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [selectedType, setSelectedType] = useState('ALL');
  const [openFAQ, setOpenFAQ] = useState<number | null>(null);
  const [currentPage, setCurrentPage] = useState(1);

  const [faqs, setFaqs] = useState<FaqItem[]>([]);
  const [totalPages, setTotalPages] = useState(0);
  const [totalItems, setTotalItems] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [fetchTick, setFetchTick] = useState(0);

  // Re-fetch khi tab được focus lại (user có thể vừa tạo FAQ ở tab khác)
  useEffect(() => {
    const handleVisibility = () => {
      if (!document.hidden) setFetchTick(t => t + 1);
    };
    document.addEventListener('visibilitychange', handleVisibility);
    return () => document.removeEventListener('visibilitychange', handleVisibility);
  }, []);

  // Debounce keyword 400ms
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedKeyword(searchQuery.trim());
      setCurrentPage(1);
    }, 400);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Fetch from backend whenever params change
  useEffect(() => {
    let cancelled = false;

    const fetchFaqs = async () => {
      setLoading(true);
      setError(null);
      try {
        const params: Record<string, string | number> = {
          page: currentPage,
          pageSize: PAGE_SIZE,
        };
        if (debouncedKeyword) params.keyword = debouncedKeyword;
        if (selectedType !== 'ALL') params.faqType = selectedType;

        const { data } = await httpClient.get<PaginatedFaqResponse>('/public/faqs', { params });
        if (!cancelled) {
          setFaqs(data.items ?? []);
          setTotalPages(data.totalPages ?? 0);
          setTotalItems(data.totalItems ?? 0);
        }
      } catch {
        if (!cancelled) {
          setError('Không thể tải dữ liệu FAQ. Vui lòng thử lại sau.');
          setFaqs([]);
          setTotalPages(0);
          setTotalItems(0);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    fetchFaqs();
    return () => { cancelled = true; };
  }, [debouncedKeyword, selectedType, currentPage, fetchTick]);

  const handleSearch = (e: React.ChangeEvent<HTMLInputElement>) => {
    setSearchQuery(e.target.value);
    setOpenFAQ(null);
  };

  const handleTypeChange = (type: string) => {
    setSelectedType(type);
    setCurrentPage(1);
    setOpenFAQ(null);
  };

  const handlePageChange = (page: number) => {
    setCurrentPage(page);
    setOpenFAQ(null);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  return (
    <div className="bg-slate-50 min-h-screen pt-24 pb-20">
      {/* Header */}
      <div className="bg-[#004c91] text-white py-16 px-4 md:px-8 shadow-inner relative overflow-hidden mb-12">
        <div className="absolute top-0 right-0 p-8 opacity-10 pointer-events-none transform translate-x-12 -translate-y-12">
          <HelpCircle className="w-64 h-64 text-white" />
        </div>
        <div className="max-w-4xl mx-auto text-center relative z-10">
          <h1 className="text-4xl md:text-5xl font-extrabold mb-6">Câu hỏi thường gặp</h1>
          <p className="text-blue-100 text-lg md:text-xl max-w-2xl mx-auto mb-10">
            Tìm kiếm thông tin nhanh chóng hoặc duyệt qua các danh mục dưới đây để giải đáp thắc mắc của bạn về các chương trình hợp tác quốc tế.
          </p>

          {/* Search bar */}
          <div className="relative max-w-xl mx-auto">
            <div className="absolute inset-y-0 left-0 pl-4 flex items-center pointer-events-none">
              {loading && debouncedKeyword
                ? <Loader2 className="h-6 w-6 text-gray-400 animate-spin" />
                : <Search className="h-6 w-6 text-gray-400" />
              }
            </div>
            <input
              type="text"
              placeholder="Nhập từ khóa tìm kiếm (VD: đăng nhập, tài liệu...)"
              value={searchQuery}
              onChange={handleSearch}
              className="block w-full pl-12 pr-4 py-4 md:py-5 border-none rounded-2xl text-gray-900 bg-white placeholder-gray-400 focus:outline-none focus:ring-4 focus:ring-orange-500/30 text-lg shadow-xl"
            />
          </div>
        </div>
      </div>

      <div className="max-w-4xl mx-auto px-4 md:px-8">
        {/* Category filter */}
        <div className="flex flex-wrap gap-3 justify-center mb-10">
          {FAQ_TYPES.map(({ value, label }) => (
            <button
              key={value}
              onClick={() => handleTypeChange(value)}
              className={`px-5 py-2.5 rounded-full text-sm font-bold transition-all ${
                selectedType === value
                  ? 'bg-[#f37021] text-white shadow-md'
                  : 'bg-white text-gray-600 hover:bg-orange-50 hover:text-[#f37021] border border-gray-200'
              }`}
            >
              {label}
            </button>
          ))}
        </div>

        {/* Total count */}
        {!loading && !error && (
          <p className="text-center text-sm text-gray-500 mb-6">
            {totalItems > 0
              ? `Tìm thấy ${totalItems} câu hỏi`
              : ''}
          </p>
        )}

        {/* Loading skeleton */}
        {loading && (
          <div className="space-y-4">
            {[...Array(5)].map((_, i) => (
              <div key={i} className="bg-white rounded-2xl shadow-sm border border-gray-100 h-20 animate-pulse" />
            ))}
          </div>
        )}

        {/* Error state */}
        {!loading && error && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="text-center py-20 bg-white rounded-3xl border border-red-100"
          >
            <HelpCircle className="w-16 h-16 text-red-200 mx-auto mb-4" />
            <h3 className="text-xl font-bold text-gray-900 mb-2">Lỗi tải dữ liệu</h3>
            <p className="text-gray-500">{error}</p>
            <button
              onClick={() => { setCurrentPage(1); setDebouncedKeyword(''); setSearchQuery(''); }}
              className="mt-6 px-6 py-2.5 bg-[#004c91] text-white rounded-xl font-bold hover:bg-[#003a70] transition-colors"
            >
              Thử lại
            </button>
          </motion.div>
        )}

        {/* FAQ List */}
        {!loading && !error && (
          <>
            <div className="space-y-4">
              <AnimatePresence>
                {faqs.map((faq) => {
                  const isOpen = openFAQ === faq.faqId;
                  return (
                    <motion.div
                      key={faq.faqId}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0, scale: 0.95 }}
                      className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden hover:shadow-md transition-shadow"
                    >
                      <button
                        onClick={() => setOpenFAQ(isOpen ? null : faq.faqId)}
                        className="w-full px-6 py-5 flex items-center justify-between text-left focus:outline-none focus:bg-slate-50/50"
                      >
                        <div className="flex flex-col md:flex-row md:items-center gap-2 md:gap-4 pr-8">
                          <span className="inline-block px-3 py-1 bg-blue-50 text-[#004c91] text-xs font-bold rounded-lg border border-blue-100 whitespace-nowrap self-start md:self-auto">
                            {faq.faqTypeLabel}
                          </span>
                          <h3 className={`text-lg md:text-xl font-bold transition-colors ${isOpen ? 'text-[#f37021]' : 'text-[#004c91]'}`}>
                            {faq.question}
                          </h3>
                        </div>
                        <div className={`flex-shrink-0 w-10 h-10 rounded-full flex items-center justify-center transition-transform duration-300 ${isOpen ? 'bg-orange-50 rotate-180' : 'bg-gray-50'}`}>
                          <ChevronDown className={`w-5 h-5 ${isOpen ? 'text-[#f37021]' : 'text-gray-400'}`} />
                        </div>
                      </button>

                      <AnimatePresence>
                        {isOpen && (
                          <motion.div
                            initial={{ height: 0, opacity: 0 }}
                            animate={{ height: 'auto', opacity: 1 }}
                            exit={{ height: 0, opacity: 0 }}
                            transition={{ duration: 0.3, ease: 'easeInOut' }}
                            className="overflow-hidden"
                          >
                            <div className="px-6 pb-6 pt-2">
                              <div className="p-5 bg-slate-50 rounded-xl rounded-tl-none border-l-4 border-[#004c91]">
                                <p className="text-gray-700 leading-relaxed font-medium whitespace-pre-line">
                                  {faq.answer}
                                </p>
                              </div>
                            </div>
                          </motion.div>
                        )}
                      </AnimatePresence>
                    </motion.div>
                  );
                })}
              </AnimatePresence>

              {faqs.length === 0 && (
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  className="text-center py-20 bg-white rounded-3xl border border-gray-100"
                >
                  <HelpCircle className="w-16 h-16 text-gray-300 mx-auto mb-4" />
                  <h3 className="text-xl font-bold text-gray-900 mb-2">Không tìm thấy kết quả</h3>
                  <p className="text-gray-500">
                    {searchQuery
                      ? `Không tìm thấy câu hỏi nào phù hợp với "${searchQuery}". Vui lòng thử lại với từ khóa khác.`
                      : 'Chưa có câu hỏi nào trong danh mục này.'}
                  </p>
                </motion.div>
              )}
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="mt-10 flex justify-center items-center gap-2">
                <button
                  onClick={() => handlePageChange(currentPage - 1)}
                  disabled={currentPage === 1}
                  className="w-10 h-10 rounded-xl flex items-center justify-center bg-white border border-gray-200 text-gray-600 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
                >
                  <ChevronLeft className="w-5 h-5" />
                </button>

                {Array.from({ length: totalPages }).map((_, idx) => (
                  <button
                    key={idx}
                    onClick={() => handlePageChange(idx + 1)}
                    className={`w-10 h-10 rounded-xl flex items-center justify-center font-bold text-sm transition-all ${
                      currentPage === idx + 1
                        ? 'bg-[#004c91] text-white shadow-sm'
                        : 'bg-white border border-gray-200 text-gray-600 hover:bg-gray-50'
                    }`}
                  >
                    {idx + 1}
                  </button>
                ))}

                <button
                  onClick={() => handlePageChange(currentPage + 1)}
                  disabled={currentPage === totalPages}
                  className="w-10 h-10 rounded-xl flex items-center justify-center bg-white border border-gray-200 text-gray-600 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-gray-50 transition-colors"
                >
                  <ChevronRight className="w-5 h-5" />
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
