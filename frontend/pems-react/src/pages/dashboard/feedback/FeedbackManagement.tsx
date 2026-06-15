/**
 * Trang FeedbackManagement
 * Giao diện toàn trình quản lý các đánh giá lưu trữ công cộng hoặc qua mail cảm ơn.
 */

import React, { useState, useMemo } from 'react';
import { Search, ChevronDown, ChevronLeft, ChevronRight, Eye, Star, X, ArrowDownUp } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { MOCK_VISIT_FEEDBACKS } from './mockData';

export function FeedbackManagement() {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState('');
  const [filterRating, setFilterRating] = useState('');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');
  const [sortBy, setSortBy] = useState<'date' | 'rating'>('date');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);

  const [isViewModalOpen, setIsViewModalOpen] = useState(false);
  const [selectedItem, setSelectedItem] = useState<any>(null);

  const filteredData = useMemo(() => {
    let result = MOCK_VISIT_FEEDBACKS.filter(item => 
      item.guestName.toLowerCase().includes(searchQuery.toLowerCase())
    );

    if (filterRating) {
      result = result.filter(item => Math.floor(item.averageRating).toString() === filterRating);
    }
    
    return result.sort((a, b) => {
      if (sortBy === 'rating') {
         return sortOrder === 'asc' ? a.averageRating - b.averageRating : b.averageRating - a.averageRating;
      } else {
        const dateA = a.date.split('/').reverse().join('');
        const dateB = b.date.split('/').reverse().join('');
        return sortOrder === 'asc' ? dateA.localeCompare(dateB) : dateB.localeCompare(dateA);
      }
    });
  }, [searchQuery, filterRating, sortOrder, sortBy]);

  const totalPages = Math.ceil(filteredData.length / pageSize);
  const paginatedData = filteredData.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const handleOpenView = (item: any) => {
    navigate(`/dashboard/feedback/${item.id}`);
  };

  const renderStars = (rating: number) => {
    return Array.from({ length: 5 }).map((_, i) => (
      <Star key={i} className={`w-3.5 h-3.5 ${i < rating ? 'fill-yellow-400 text-yellow-400' : 'fill-slate-100 text-slate-200'}`} />
    ));
  };

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto pb-12 animate-in fade-in duration-500 font-sans">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý feedback</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">Quản lý feedback</h1>
          <p className="text-gray-500 mt-1 font-medium">Danh sách đánh giá từ các đoàn khách đã đóng đoàn</p>
        </div>
      </div>

      {/* Toolbar / Search & Filters */}
      <div className="bg-[#004c91] rounded-t-2xl p-4 shadow-sm flex flex-col md:flex-row gap-4">
        <div className="relative w-full md:max-w-lg shrink-0 flex-1">
          <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-slate-400" />
          <input 
            type="text"
            placeholder="Tìm kiếm tên đoàn khách, người đánh giá..."
            value={searchQuery}
            onChange={(e) => {
              setSearchQuery(e.target.value);
              setCurrentPage(1);
            }}
            className="w-full pl-11 pr-4 py-2.5 rounded-xl border border-white/20 focus:border-white focus:ring-1 focus:ring-white outline-none text-sm transition-all font-medium bg-white/10 text-white placeholder:text-white/60"
          />
        </div>
        
        <div className="flex gap-3 w-full md:w-auto overflow-hidden">
          <div className="relative w-full md:w-48 shrink-0">
            <select 
              value={filterRating}
              onChange={(e) => {
                setFilterRating(e.target.value);
                setCurrentPage(1);
              }}
              className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-medium appearance-none"
            >
              <option value="" className="text-slate-800">Tất cả mức độ</option>
              <option value="5" className="text-slate-800">5 Sao</option>
              <option value="4" className="text-slate-800">4 Sao</option>
              <option value="3" className="text-slate-800">3 Sao</option>
              <option value="2" className="text-slate-800">2 Sao</option>
              <option value="1" className="text-slate-800">1 Sao</option>
            </select>
            <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
          </div>
        </div>
      </div>

      <div className="bg-white rounded-b-2xl border-x border-b border-gray-200 shadow-sm overflow-hidden flex flex-col">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead>
              <tr className="bg-[#004c91] text-white">
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">STT</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Tên đoàn khách</th>
                <th 
                  className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap cursor-pointer select-none group"
                  onClick={() => {
                    if (sortBy === 'rating') {
                       setSortOrder(prev => prev === 'asc' ? 'desc' : 'asc');
                    } else {
                       setSortBy('rating');
                       setSortOrder('desc');
                    }
                  }}
                >
                  <div className="flex items-center justify-center gap-1.5">
                    Trung bình đánh giá
                    <ArrowDownUp className={`w-3 h-3 text-white/50 group-hover:text-white transition-colors ${sortBy === 'rating' && sortOrder === 'asc' ? 'rotate-180' : ''} ${sortBy === 'rating' ? 'text-white/100' : ''}`} />
                  </div>
                </th>
                <th 
                  className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap cursor-pointer select-none group text-center"
                  onClick={() => {
                    if (sortBy === 'date') {
                       setSortOrder(prev => prev === 'asc' ? 'desc' : 'asc');
                    } else {
                       setSortBy('date');
                       setSortOrder('desc');
                    }
                  }}
                >
                  <div className="flex items-center justify-center gap-1.5">
                    Thời gian
                    <ArrowDownUp className={`w-3 h-3 text-white/50 group-hover:text-white transition-colors ${sortBy === 'date' && sortOrder === 'asc' ? 'rotate-180' : ''} ${sortBy === 'date' ? 'text-white/100' : ''}`} />
                  </div>
                </th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {paginatedData.length > 0 ? paginatedData.map((item, index) => (
                  <tr 
                    key={item.id}
                    className="hover:bg-blue-50/50 transition-colors group"
                  >
                    <td className="p-4 font-bold text-slate-500 text-center whitespace-nowrap">
                      {(currentPage - 1) * pageSize + index + 1}
                    </td>
                    <td className="p-4">
                      <p className="font-bold text-[#004c91] mb-0.5 line-clamp-1" title={item.guestName}>
                        {item.guestName}
                      </p>
                    </td>
                    <td className="p-4 text-center">
                      <div className="flex items-center justify-center gap-0.5">
                        <span className="font-bold text-slate-700 mr-1">{item.averageRating.toFixed(1)}</span>
                        {renderStars(Math.round(item.averageRating))}
                      </div>
                    </td>
                    <td className="p-4 text-sm font-medium text-slate-600 text-center whitespace-nowrap">
                      {item.date}
                    </td>
                    <td className="p-4">
                      <div className="flex items-center justify-center gap-2">
                        <button 
                          onClick={() => handleOpenView(item)}
                          className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 flex items-center justify-center transition-colors outline-none cursor-pointer shadow-sm border border-slate-100"
                          title="Xem chi tiết"
                        >
                          <Eye className="w-4 h-4" />
                        </button>
                      </div>
                    </td>
                  </tr>
                )) : (
                  <tr className="bg-slate-50/50">
                    <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                      <Star className="w-12 h-12 text-slate-300 mx-auto mb-3" />
                      <p className="font-medium text-slate-600 mb-1">Không tìm thấy feedback nào</p>
                    </td>
                  </tr>
                )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        <div className="p-4 border-t border-gray-100 flex flex-col md:flex-row items-center justify-between gap-4 bg-gray-50/50">
          <div className="flex items-center gap-2 text-sm text-gray-500 font-medium">
            <span>Hiển thị</span>
            <select 
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setCurrentPage(1);
              }}
              className="px-2 py-1 bg-white border border-gray-200 rounded-lg outline-none cursor-pointer focus:border-[#004c91] transition-colors"
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
              className="cursor-pointer p-1 text-gray-500 hover:bg-gray-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed outline-none select-none transition-colors border border-transparent hover:border-gray-300"
            >
              <ChevronLeft className="w-5 h-5" />
            </button>

            <div className="flex items-center gap-1">
              {Array.from({ length: totalPages }).map((_, i) => (
                <button
                  key={i}
                  onClick={() => setCurrentPage(i + 1)}
                  className={`cursor-pointer w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none select-none border box-border ${currentPage === i + 1 ? 'bg-[#004c91] text-white border-[#004c91] shadow-sm' : 'bg-white text-gray-600 border-gray-200 hover:bg-blue-50 hover:text-[#004c91] hover:border-blue-200'}`}
                >
                  {i + 1}
                </button>
              ))}
            </div>

            <button 
              onClick={() => setCurrentPage(prev => Math.min(totalPages, prev + 1))}
              disabled={currentPage === totalPages || totalPages === 0}
              className="cursor-pointer p-1 text-gray-500 hover:bg-gray-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed outline-none select-none transition-colors border border-transparent hover:border-gray-300"
            >
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

