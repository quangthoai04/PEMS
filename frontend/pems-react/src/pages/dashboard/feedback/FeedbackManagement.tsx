/**
 * Trang FeedbackManagement
 * Giao diện toàn trình quản lý các đánh giá lưu trữ công cộng hoặc qua mail cảm ơn.
 */

import React, { useState, useEffect, useMemo } from 'react';
import { Search, ChevronDown, ChevronLeft, ChevronRight, Eye, Star, ChevronUp, MessageSquare, Filter } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../../shared/hooks/useAuth';
import { useFeedbacks } from '../../../features/feedbacks/hooks/useFeedbacks';
import { FeedbackFilterParams } from '../../../features/feedbacks/types/feedbacks.types';

export function FeedbackManagement() {
  const navigate = useNavigate();
  const { user } = useAuth();
  
  const { summaries, loading, fetchSummaries } = useFeedbacks();
  
  const [searchQuery, setSearchQuery] = useState('');
  const [filterRating, setFilterRating] = useState('');
  const [filterRoleFlow, setFilterRoleFlow] = useState('');
  const [filterSubmitterRole, setFilterSubmitterRole] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [dateError, setDateError] = useState('');
  
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  
  const [isAdvancedFilterOpen, setIsAdvancedFilterOpen] = useState(false);

  // Debounce search
  const [debouncedSearch, setDebouncedSearch] = useState(searchQuery);
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchQuery);
    }, 500);
    return () => clearTimeout(timer);
  }, [searchQuery]);

  // Fetch data
  useEffect(() => {
    const params: FeedbackFilterParams = {
      q: debouncedSearch || undefined,
      ratingLevel: filterRating || undefined,
      roleFlow: filterRoleFlow || undefined,
      submitterRole: filterSubmitterRole || undefined,
      fromDate: fromDate || undefined,
      toDate: toDate || undefined,
      page: currentPage,
      pageSize,
    };
    
    fetchSummaries(params);
  }, [debouncedSearch, filterRating, filterRoleFlow, filterSubmitterRole, fromDate, toDate, currentPage, pageSize, fetchSummaries]);

  const handleOpenViewSummary = (visitRequestId: number, visitInstanceId?: number | null) => {
    // For now, mapping to the existing /feedback/:id route to prevent 404s
    navigate(`/dashboard/feedback/${visitRequestId}`);
  };

  // Calculate summary stats from current summaries data (as approximation since no global stats API)
  const stats = useMemo(() => {
    if (!summaries?.items?.length) return { totalDelegations: 0, totalFeedbacks: 0, avgRating: 0, lowRating: 0, latest: null };
    
    let totalF = 0;
    let sumR = 0;
    let lowR = 0;
    let latest = '';
    
    summaries.items.forEach(item => {
      totalF += item.totalFeedbacks;
      sumR += (item.averageRating * item.totalFeedbacks);
      lowR += item.lowRatingCount;
      if (item.latestSubmittedAt && (!latest || new Date(item.latestSubmittedAt) > new Date(latest))) {
        latest = item.latestSubmittedAt;
      }
    });
    
    return {
      totalDelegations: summaries.totalItems,
      totalFeedbacks: totalF,
      avgRating: totalF > 0 ? (sumR / totalF) : 0,
      lowRating: lowR,
      latest
    };
  }, [summaries]);

  // Format date
  const formatDate = (dateStr: string) => {
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    } catch {
      return dateStr;
    }
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 animate-in fade-in duration-500 font-sans">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý feedback</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-6">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">Quản lý feedback</h1>
            {user?.roleCode === 'STAFF' && user?.campusName && (
               <span className="px-2.5 py-1 bg-blue-100 text-blue-700 text-xs font-bold rounded-lg border border-blue-200">
                 Campus: {user.campusName}
               </span>
            )}
          </div>
          <p className="text-gray-500 mt-1 font-medium">Tổng hợp và tra cứu đánh giá của các đoàn khách đã hoàn tất</p>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-center">
           <span className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Tổng đoàn</span>
           <span className="text-2xl font-black text-[#004c91]">{stats.totalDelegations}</span>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-center">
           <span className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Điểm trung bình</span>
           <div className="flex items-center gap-2">
             <span className="text-2xl font-black text-[#004c91]">{stats.avgRating.toFixed(1)}</span>
             <Star className="w-5 h-5 text-yellow-400 fill-yellow-400" />
           </div>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-center">
           <span className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Cảnh báo (1-2★)</span>
           <span className={`text-2xl font-black ${stats.lowRating > 0 ? 'text-red-500' : 'text-slate-700'}`}>{stats.lowRating}</span>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-center">
           <span className="text-xs font-bold text-slate-500 uppercase tracking-wider mb-1">Mới nhất</span>
           <span className="text-sm font-bold text-slate-700">{stats.latest ? formatDate(stats.latest) : '-'}</span>
        </div>
      </div>

      {/* Toolbar / Search & Filters */}
      <div className="bg-white rounded-t-2xl p-4 shadow-sm border border-slate-200 flex flex-col gap-4">
        <div className="flex flex-col md:flex-row gap-4 items-center">
          <div className="relative w-full md:max-w-lg shrink-0 flex-1">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 pointer-events-none" />
            <input 
              type="text"
              placeholder="Tìm theo tên đoàn, người đánh giá, người được đánh giá..."
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setCurrentPage(1);
              }}
              className="w-full pl-11 pr-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium"
            />
          </div>
          
          <button 
            onClick={() => setIsAdvancedFilterOpen(!isAdvancedFilterOpen)}
            className="flex items-center gap-2 text-sm font-bold text-[#004c91] w-max px-4 py-2.5 rounded-xl hover:bg-blue-50 transition-colors"
          >
            <Filter className="w-4 h-4" />
            Bộ lọc nâng cao
            {isAdvancedFilterOpen ? <ChevronUp className="w-4 h-4" /> : <ChevronDown className="w-4 h-4" />}
          </button>
        </div>

        {isAdvancedFilterOpen && (
          <div className="flex flex-wrap gap-3 pt-4 border-t border-slate-100">
            <select 
              value={filterRating}
              onChange={(e) => { setFilterRating(e.target.value); setCurrentPage(1); }}
              className="px-3 py-2.5 rounded-xl border border-slate-200 text-sm font-medium outline-none"
            >
              <option value="">Tất cả mức độ</option>
              <option value="5">5 Sao</option>
              <option value="4">4 Sao</option>
              <option value="3">3 Sao</option>
              <option value="LOW">1-2 Sao (Cần chú ý)</option>
            </select>

            <select 
              value={filterRoleFlow}
              onChange={(e) => { setFilterRoleFlow(e.target.value); setCurrentPage(1); }}
              className="px-3 py-2.5 rounded-xl border border-slate-200 text-sm font-medium outline-none"
            >
              <option value="">Tất cả chiều đánh giá</option>
              <option value="VISITOR_TO_HOST">Khách đánh giá Host</option>
              <option value="LOGISTICS_TO_HOST">Logistics đánh giá Host</option>
              <option value="HOST_TO_VISITOR">Host đánh giá Khách</option>
              <option value="HOST_TO_LOGISTICS">Host đánh giá Logistics</option>
            </select>

            <select 
              value={filterSubmitterRole}
              onChange={(e) => { setFilterSubmitterRole(e.target.value); setCurrentPage(1); }}
              className="px-3 py-2.5 rounded-xl border border-slate-200 text-sm font-medium outline-none"
            >
              <option value="">Tất cả người gửi</option>
              <option value="VISITOR">Khách (VISITOR)</option>
              <option value="HOST">Host (HOST)</option>
              <option value="LOGISTICS">Logistics (LOGISTICS)</option>
            </select>
            
            <div className="flex items-center gap-2 relative">
               <input 
                 type="date"
                 value={fromDate}
                 onChange={(e) => { 
                   setFromDate(e.target.value); 
                   if (toDate && e.target.value > toDate) setDateError('Từ ngày > Đến ngày');
                   else { setDateError(''); setCurrentPage(1); }
                 }}
                 className="px-3 py-2.5 rounded-xl border border-slate-200 text-sm font-medium outline-none"
                 title="Từ ngày"
               />
               <span className="text-slate-400">-</span>
               <input 
                 type="date"
                 value={toDate}
                 onChange={(e) => { 
                   setToDate(e.target.value); 
                   if (fromDate && e.target.value < fromDate) setDateError('Đến ngày < Từ ngày');
                   else { setDateError(''); setCurrentPage(1); }
                 }}
                 className="px-3 py-2.5 rounded-xl border border-slate-200 text-sm font-medium outline-none"
                 title="Đến ngày"
               />
               {dateError && <span className="text-red-500 text-xs absolute -bottom-5 left-0 whitespace-nowrap font-medium">{dateError}</span>}
            </div>

            <button 
              onClick={() => {
                setFilterRating('');
                setFilterRoleFlow('');
                setFilterSubmitterRole('');
                setFromDate('');
                setToDate('');
                setDateError('');
                setCurrentPage(1);
              }}
              className="px-5 py-2.5 bg-slate-100 hover:bg-slate-200 text-slate-700 text-sm font-bold rounded-xl transition-colors whitespace-nowrap ml-auto md:ml-0"
            >
              Reset Lọc
            </button>
          </div>
        )}
      </div>

      {/* Table Area */}
      <div className="bg-white rounded-b-2xl border border-slate-200 shadow-sm overflow-hidden flex flex-col relative min-h-[300px] border-t-0 mt-4">
        {loading && (
          <div className="absolute inset-0 bg-white/60 backdrop-blur-sm z-10 flex items-center justify-center">
            <div className="w-8 h-8 border-4 border-[#004c91] border-t-transparent rounded-full animate-spin"></div>
          </div>
        )}
        
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-slate-50 text-slate-600 border-b border-slate-200">
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">STT</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Đoàn khách / Phạm vi</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">Điểm TB</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Feedback mới nhất</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">Cảnh báo</th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {summaries?.items?.length ? (
                summaries.items.map((item, index) => (
                  <tr key={`${item.visitRequestId}-${item.visitInstanceId}`} className="hover:bg-blue-50/30 transition-colors">
                    <td className="p-4 font-bold text-slate-500 text-center whitespace-nowrap">
                      {(currentPage - 1) * pageSize + index + 1}
                    </td>
                    <td className="p-4">
                      <p className="font-bold text-[#004c91] mb-1 line-clamp-1">{item.visitTitle}</p>
                      <div className="flex items-center gap-2 text-xs font-medium text-slate-500">
                        <span className="bg-slate-100 px-2 py-0.5 rounded whitespace-nowrap">REQ #{item.visitRequestId}</span>
                        {item.visitInstanceId && <span className="bg-slate-100 px-2 py-0.5 rounded whitespace-nowrap">INST #{item.visitInstanceId}</span>}
                        {item.campusName && <span className="text-slate-400 whitespace-nowrap">• {item.campusName}</span>}
                      </div>
                    </td>
                    <td className="p-4 text-center whitespace-nowrap">
                      <div className="flex items-center justify-center gap-1">
                        <span className="font-bold text-slate-700">{item.averageRating.toFixed(1)}</span>
                        <Star className="w-4 h-4 fill-yellow-400 text-yellow-400" />
                      </div>
                    </td>
                    <td className="p-4">
                      {item.latestSubmittedAt ? (
                        <>
                          <div className="text-sm font-medium text-slate-700 whitespace-nowrap">{formatDate(item.latestSubmittedAt)}</div>
                          <div className="text-xs text-slate-500 mt-0.5 truncate max-w-[150px]">{item.latestSubmitterName}</div>
                        </>
                      ) : <span className="text-slate-400">-</span>}
                    </td>
                    <td className="p-4 text-center whitespace-nowrap">
                      {item.lowRatingCount > 0 ? (
                         <span className="px-2 py-1 bg-red-100 text-red-700 text-xs font-bold rounded-lg border border-red-200">
                           {item.lowRatingCount} cảnh báo
                         </span>
                      ) : <span className="text-slate-400 text-sm">-</span>}
                    </td>
                    <td className="p-4 whitespace-nowrap">
                      <div className="flex items-center justify-center gap-2">
                        <button 
                          onClick={() => handleOpenViewSummary(item.visitRequestId, item.visitInstanceId)}
                          className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 flex items-center justify-center transition-colors border border-slate-200 shadow-sm"
                          title="Xem chi tiết đoàn"
                        >
                          <Eye className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))
              ) : !loading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                    <MessageSquare className="w-12 h-12 text-slate-300 mx-auto mb-3" />
                    <p className="font-medium text-slate-600 mb-1">Không tìm thấy feedback nào</p>
                    <p className="text-sm text-slate-400">Vui lòng thử thay đổi bộ lọc hoặc tìm kiếm khác</p>
                  </td>
                </tr>
              ) : (
                <tr>
                  <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                     <p className="font-medium text-slate-600 mb-1">Đang tải dữ liệu...</p>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="p-4 border-t border-slate-200 flex flex-col md:flex-row items-center justify-between gap-4 bg-slate-50">
          <div className="flex items-center gap-2 text-sm text-slate-500 font-medium">
            <span>Hiển thị</span>
            <select 
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setCurrentPage(1);
              }}
              className="px-2 py-1 bg-white border border-slate-200 rounded-lg outline-none cursor-pointer focus:border-[#004c91] transition-colors"
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
            </select>
            <span>bản ghi / trang</span>
          </div>

          <div className="flex items-center gap-2">
            <button 
              onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
              disabled={currentPage === 1}
              className="cursor-pointer p-1 text-slate-500 hover:bg-slate-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronLeft className="w-5 h-5" />
            </button>
            <div className="text-sm font-bold text-slate-700">
              Trang {currentPage} / {Math.max(1, summaries?.totalPages || 1)}
            </div>
            <button 
              onClick={() => setCurrentPage(prev => prev + 1)}
              disabled={currentPage >= (summaries?.totalPages || 1)}
              className="cursor-pointer p-1 text-slate-500 hover:bg-slate-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
