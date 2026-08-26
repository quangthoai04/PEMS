import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Filter, FileText, Eye, Download, ChevronLeft, ChevronRight, X, Calendar, Users, CheckSquare, Building2, User, Clock, ShieldAlert, Lock } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useAuth } from '../../../shared/hooks/useAuth';
import { useCampusFilterOptions } from '../../../features/campus-management/hooks/useCampusManagement';
import { useMinutes } from './useMinutes';
import { MinutesFilterParams, MinutesListItem } from './types';
import { formatVietnamDateTime, formatVietnamDate } from '../../../shared/utils/vietnamTime';
import {
  formatMinutesStatus, formatAttendanceStatus, formatVisitStatus, formatActionItemStatus,
} from '../../../shared/utils/domainLabels';
import { sanitizeHtml, htmlToPlainText } from '../../../shared/security/sanitizeHtml';

export function MinuteManagement() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const isHO = user?.roleCode === 'HO';
  // Patch 6: the campus dropdown below is already HO-gated in the JSX — the fetch itself must be
  // gated too, or every non-HO viewer of this page fires a request to the HO-only
  // /campuses/filter-options endpoint and gets a 403 on every mount.
  const campusFilterOptions = useCampusFilterOptions({ enabled: isHO });

  const {
    loading,
    listData,
    detailData,
    fetchList,
    fetchDetail,
    clearDetail,
    exportPdf,
    exportExcel
  } = useMinutes();

  const [filters, setFilters] = useState<MinutesFilterParams>({
    page: 1,
    pageSize: 10,
    sortBy: 'created_at',
    sortDir: 'desc'
  });
  
  const [searchInput, setSearchInput] = useState('');
  const [showAdvancedFilters, setShowAdvancedFilters] = useState(false);
  const [selectedMinute, setSelectedMinute] = useState<MinutesListItem | null>(null);
  // Chặn double-click khi tải PDF/Excel (thao tác có độ trễ) — null = không có tải nào đang chạy.
  const [downloading, setDownloading] = useState<'PDF' | 'EXCEL' | null>(null);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setFilters(prev => ({ ...prev, q: searchInput, page: 1 }));
    }, 500);
    return () => clearTimeout(timer);
  }, [searchInput]);

  useEffect(() => {
    fetchList(filters);
  }, [filters, fetchList]);

  const handleFilterChange = (key: keyof MinutesFilterParams, value: any) => {
    setFilters(prev => ({ ...prev, [key]: value, page: 1 }));
  };

  const clearAllFilters = () => {
    setSearchInput('');
    setFilters({ page: 1, pageSize: 10, sortBy: 'created_at', sortDir: 'desc' });
  };

  const handlePageChange = (newPage: number) => {
    if (newPage > 0 && (!listData?.totalCount || newPage <= Math.ceil(listData.totalCount / (filters.pageSize || 10)))) {
      setFilters(prev => ({ ...prev, page: newPage }));
    }
  };

  const handlePageSizeChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    setFilters(prev => ({ ...prev, pageSize: Number(e.target.value), page: 1 }));
  };

  const openDetail = (minute: MinutesListItem) => {
    setSelectedMinute(minute);
    fetchDetail(minute.visitInstanceId);
  };

  const closeDetail = () => {
    setSelectedMinute(null);
    clearDetail();
  };

  const handleDownloadPDF = async (e: React.MouseEvent, minutesId: number) => {
    e.stopPropagation();
    if (downloading) return;
    setDownloading('PDF');
    try {
      await exportPdf(minutesId);
    } finally {
      setDownloading(null);
    }
  };

  const handleDownloadExcel = async (e: React.MouseEvent, minutesId: number) => {
    e.stopPropagation();
    if (downloading) return;
    setDownloading('EXCEL');
    try {
      await exportExcel(minutesId);
    } finally {
      setDownloading(null);
    }
  };

  const totalPages = listData ? Math.ceil(listData.totalCount / (filters.pageSize || 10)) : 1;
  const currentPage = filters.page || 1;

  return (
    <div className="w-full pb-12 flex flex-col space-y-4 animate-in fade-in duration-300">
      {/* 1. Header & Navigation Layer */}
      <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Quản lý biên bản</span>
      </div>
      
      <div className="border-b border-gray-100 pb-3 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold text-[#004c91]">Quản lý biên bản</h1>
          </div>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <span className="text-sm font-bold text-slate-500 mb-1">Tổng biên bản</span>
          <span className="text-2xl font-bold text-[#004c91]">{listData?.summary?.totalMinutes || 0}</span>
        </div>
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <span className="text-sm font-bold text-slate-500 mb-1">{formatMinutesStatus('DRAFT')}</span>
          <span className="text-2xl font-bold text-slate-700">{listData?.summary?.draftCount || 0}</span>
        </div>
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <span className="text-sm font-bold text-slate-500 mb-1">{formatMinutesStatus('SAVED')}</span>
          <span className="text-2xl font-bold text-green-600">{listData?.summary?.savedCount || 0}</span>
        </div>
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <span className="text-sm font-bold text-slate-500 mb-1">Đang bị khóa sửa</span>
          <span className="text-2xl font-bold text-orange-500">{listData?.summary?.lockedCount || 0}</span>
        </div>
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <span className="text-sm font-bold text-slate-500 mb-1">Action item mở</span>
          <span className="text-2xl font-bold text-red-500">{listData?.summary?.openActionItemCount || 0}</span>
        </div>
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-sm flex flex-col">
          <span className="text-sm font-bold text-slate-500 mb-1">Cập nhật gần nhất</span>
          <span className="text-sm font-normal text-slate-700 mt-auto">{listData?.summary?.latestUpdatedAt ? formatVietnamDateTime(listData.summary.latestUpdatedAt) : 'Chưa có'}</span>
        </div>
      </div>

      {/* 2. Action & Filter Controller */}
      <div className="w-full bg-white p-4 rounded-2xl border border-slate-200 shadow-sm space-y-4">
        <div className="flex flex-col lg:flex-row items-center gap-4">
          <div className="relative flex-1 w-full group">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 w-5 h-5 group-focus-within:text-[#004c91] transition-colors pointer-events-none" />
            <input 
              type="text" 
              placeholder="Tìm theo tên biên bản, nội dung, đoàn khách, người tham gia, đầu việc..." 
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              className="w-full pl-11 pr-4 py-2.5 border border-slate-200 rounded-xl bg-white text-sm font-normal text-slate-800 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all placeholder:text-slate-400 shadow-sm"
            />
          </div>
          
          <div className="flex items-center gap-3 w-full lg:w-auto overflow-x-auto pb-2 lg:pb-0 hide-scrollbar">
            <select 
              value={filters.status || ''} 
              onChange={e => handleFilterChange('status', e.target.value)}
              className="px-3 py-2.5 border border-slate-200 rounded-xl text-sm font-normal focus:outline-none focus:border-[#004c91] bg-white min-w-[150px]"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="DRAFT">{formatMinutesStatus('DRAFT')}</option>
              <option value="SAVED">{formatMinutesStatus('SAVED')}</option>
            </select>
            
            <select 
              value={filters.attendanceStatus || ''} 
              onChange={e => handleFilterChange('attendanceStatus', e.target.value)}
              className="px-3 py-2.5 border border-slate-200 rounded-xl text-sm font-normal focus:outline-none focus:border-[#004c91] bg-white min-w-[160px]"
            >
              <option value="">Tất cả điểm danh</option>
              <option value="PRESENT">{formatAttendanceStatus('PRESENT')}</option>
              <option value="ABSENT">{formatAttendanceStatus('ABSENT')}</option>
              <option value="EXCUSED">{formatAttendanceStatus('EXCUSED')}</option>
            </select>
            
            <select 
              value={filters.actionItemStatus || ''} 
              onChange={e => handleFilterChange('actionItemStatus', e.target.value)}
              className="px-3 py-2.5 border border-slate-200 rounded-xl text-sm font-normal focus:outline-none focus:border-[#004c91] bg-white min-w-[150px]"
            >
              <option value="">Tất cả đầu việc</option>
              <option value="TODO">{formatActionItemStatus('TODO')}</option>
              <option value="IN_PROGRESS">{formatActionItemStatus('IN_PROGRESS')}</option>
              <option value="DONE">{formatActionItemStatus('DONE')}</option>
              <option value="CANCELLED">{formatActionItemStatus('CANCELLED')}</option>
            </select>

            {isHO && (
              <select
                value={filters.campusId || ''}
                onChange={e => handleFilterChange('campusId', e.target.value ? Number(e.target.value) : undefined)}
                className="px-3 py-2.5 border border-slate-200 rounded-xl text-sm font-normal focus:outline-none focus:border-[#004c91] bg-white min-w-[150px]"
              >
                <option value="">Tất cả cơ sở</option>
                {campusFilterOptions?.campuses.map((c) => (
                  <option key={c.campusId} value={c.campusId}>{c.name}</option>
                ))}
              </select>
            )}

            <button
              onClick={() => setShowAdvancedFilters(!showAdvancedFilters)}
              className={`flex items-center gap-2 px-4 py-2.5 border rounded-xl text-sm font-medium transition-colors whitespace-nowrap ${showAdvancedFilters ? 'border-[#004c91] bg-[#004c91]/5 text-[#004c91]' : 'border-slate-200 bg-white text-slate-700 hover:bg-slate-50'}`}
            >
              <Filter className="w-4 h-4" />
              Lọc nâng cao
            </button>
            <button 
              onClick={clearAllFilters}
              className="px-4 py-2.5 border border-slate-200 rounded-xl text-sm font-medium bg-white text-slate-700 hover:bg-slate-50 transition-colors whitespace-nowrap"
            >
              Đặt lại
            </button>
          </div>
        </div>

        {/* Advanced Filters */}
        <AnimatePresence>
          {showAdvancedFilters && (
            <motion.div 
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              className="overflow-hidden border-t border-slate-100 pt-4"
            >
              <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-4">
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Loại ngày</label>
                  <select 
                    value={filters.dateType || ''} 
                    onChange={e => handleFilterChange('dateType', e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm"
                  >
                    <option value="">Không lọc theo ngày</option>
                    <option value="created_at">Ngày tạo</option>
                    <option value="updated_at">Ngày cập nhật</option>
                    <option value="edit_locked_at">Ngày khóa sửa</option>
                    <option value="due_date">Deadline đầu việc</option>
                  </select>
                </div>
                {filters.dateType && (
                  <>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Từ ngày</label>
                      <input 
                        type="date" 
                        value={filters.fromDate || ''} 
                        onChange={e => handleFilterChange('fromDate', e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-bold text-slate-500 mb-1">Đến ngày</label>
                      <input 
                        type="date" 
                        value={filters.toDate || ''} 
                        onChange={e => handleFilterChange('toDate', e.target.value)}
                        className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm"
                      />
                    </div>
                  </>
                )}
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Trạng thái khóa</label>
                  <select 
                    value={filters.lockState || ''} 
                    onChange={e => handleFilterChange('lockState', e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm"
                  >
                    <option value="">Tất cả</option>
                    <option value="LOCKED">Đang bị khóa</option>
                    <option value="UNLOCKED">Không bị khóa</option>
                    <option value="EXPIRED">Lock đã hết hạn</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Người tham gia vắng mặt</label>
                  <select 
                    value={filters.hasAbsentParticipants || ''} 
                    onChange={e => handleFilterChange('hasAbsentParticipants', e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm"
                  >
                    <option value="">Tất cả</option>
                    <option value="true">Có</option>
                    <option value="false">Không</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-bold text-slate-500 mb-1">Loại người tham gia</label>
                  <select 
                    value={filters.participantType || ''} 
                    onChange={e => handleFilterChange('participantType', e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm"
                  >
                    <option value="">Tất cả</option>
                    <option value="INTERNAL">Internal user</option>
                    <option value="GUEST">Guest member</option>
                  </select>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* 3. Data View */}
      <div className="bg-white rounded-3xl shadow-sm border border-slate-200 overflow-hidden">
        <div className="overflow-x-auto w-full">
          <table className="w-full text-left border-collapse min-w-[1200px]">
            <thead>
              <tr className="bg-[#004c91] border-b border-[#004c91]">
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap w-16">STT</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap min-w-[250px]">Biên bản</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap min-w-[200px]">Đoàn khách</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap min-w-[150px]">Trạng thái & Khóa</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap">Audit</th>
                <th className="px-4 py-4 text-xs font-bold text-white uppercase tracking-wider text-center whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {loading ? (
                 <tr><td colSpan={8} className="px-6 py-12 text-center text-slate-500">Đang tải dữ liệu...</td></tr>
              ) : listData?.items && listData.items.length > 0 ? listData.items.map((doc, index) => (
                <motion.tr 
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: index * 0.05 }}
                  key={doc.minutesId} 
                  className="transition-colors duration-200 hover:bg-slate-50 group"
                >
                  <td className="px-4 py-4 text-sm font-medium text-slate-500">
                    {(currentPage - 1) * (filters.pageSize || 10) + index + 1}
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex flex-col gap-1">
                      <div className="font-bold text-slate-800 line-clamp-2">{doc.title}</div>
                      <div className="text-xs text-slate-500 font-mono">MIN #{doc.minutesId}</div>
                      <div className="text-xs text-slate-600 line-clamp-1 italic">{htmlToPlainText(doc.contentPreview) || 'Chưa có nội dung'}</div>
                    </div>
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex flex-col gap-1">
                      <div className="font-normal text-slate-700">{doc.visitTitle || 'Chưa có tên đoàn'}</div>
                      <div className="text-xs text-slate-500 font-mono">INST #{doc.visitInstanceId}</div>
                      {doc.hostName && <div className="text-xs text-slate-500">Host: {doc.hostName}</div>}
                    </div>
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex flex-col gap-2 items-start">
                      <span className={`inline-flex px-2 py-0.5 text-xs font-bold rounded-md ${doc.status === 'SAVED' ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-slate-100 text-slate-600 border border-slate-200'}`}>
                        {formatMinutesStatus(doc.status)}
                      </span>
                      {doc.lockState === 'LOCKED' && (
                        <div className="flex items-center gap-1 text-xs text-orange-600 bg-orange-50 px-2 py-0.5 rounded border border-orange-200" title={`Locked by ${doc.editLockedByName}`}>
                          <Lock className="w-3 h-3" /> Đang khóa
                        </div>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-4">
                    <div className="text-xs text-slate-500 space-y-1">
                      <div className="flex items-center gap-1" title="Ngày tạo"><Clock className="w-3 h-3"/> {doc.createdAt ? formatVietnamDate(doc.createdAt) : 'N/A'}</div>
                      <div className="flex items-center gap-1" title="Người cập nhật"><User className="w-3 h-3"/> {doc.updatedByName || doc.createdByName || 'N/A'}</div>
                    </div>
                  </td>
                  <td className="px-4 py-4">
                    <div className="flex items-center justify-center gap-2">
                      <button 
                        onClick={() => openDetail(doc)}
                        className="p-2 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 rounded-lg transition-colors outline-none cursor-pointer" 
                        title="Xem chi tiết"
                      >
                        <Eye className="w-5 h-5" />
                      </button>
                    </div>
                  </td>
                </motion.tr>
              )) : (
                <tr>
                  <td colSpan={8} className="px-6 py-12 text-center">
                     <div className="flex flex-col items-center justify-center">
                        <div className="w-16 h-16 bg-slate-50 rounded-full flex items-center justify-center mb-4">
                          <Search className="w-8 h-8 text-slate-300" />
                        </div>
                        <p className="text-slate-500 font-normal font-sans">Không tìm thấy biên bản phù hợp.</p>
                     </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        {/* Pagination */}
        {listData && listData.items.length > 0 && (
          <div className="px-6 py-4 border-t border-slate-200 bg-slate-50/50 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-2 text-sm text-gray-600">
              <span>Hiển thị</span>
              <select 
                value={filters.pageSize}
                onChange={handlePageSizeChange}
                className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-normal"
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
              <span>bản ghi / trang</span>
            </div>
            <div className="flex items-center gap-2">
              <button 
                disabled={currentPage === 1}
                onClick={() => handlePageChange(currentPage - 1)}
                className="p-1.5 border border-slate-200 rounded-lg text-slate-600 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
              >
                <ChevronLeft className="w-5 h-5" />
              </button>
              <span className="text-sm font-normal px-2">Trang {currentPage} / {totalPages}</span>
              <button 
                disabled={currentPage === totalPages}
                onClick={() => handlePageChange(currentPage + 1)}
                className="p-1.5 border border-slate-200 rounded-lg text-slate-600 bg-white hover:bg-slate-50 disabled:opacity-50 disabled:cursor-not-allowed transition-colors cursor-pointer"
              >
                <ChevronRight className="w-5 h-5" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal Detail */}
      <AnimatePresence>
        {selectedMinute && (
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 backdrop-blur-sm px-4"
          >
            <motion.div 
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              // Overlay has no vertical padding of its own (only `px-4`) -- the 90% figure IS the
              // gutter, via flex centering. A straight vh->dvh unit swap is the correct fix here
              // (unlike the p-4/p-6-overlay modals elsewhere, which need a calc() gutter instead).
              className="bg-white rounded-2xl shadow-xl w-full max-w-5xl max-h-[90dvh] overflow-hidden flex flex-col m-auto relative"
            >
              {/* Header */}
              <div className="bg-[#004c91] px-6 py-4 flex items-center justify-between border-b border-[#003366] shrink-0 sticky top-0 z-10">
                 <div>
                   <h2 className="text-xl font-bold text-white flex items-center gap-2">
                      <span className="w-8 h-8 rounded-full bg-[#f37021] flex items-center justify-center text-sm shrink-0">
                        <FileText className="w-4 h-4 text-white" />
                      </span>
                      <span className="truncate max-w-[400px]" title={detailData?.title || selectedMinute.title}>{detailData?.title || selectedMinute.title}</span>
                   </h2>
                   <div className="flex items-center gap-2 mt-2 ml-10">
                      <span className="text-xs bg-white/20 text-white px-2 py-0.5 rounded font-mono">MIN #{selectedMinute.minutesId}</span>
                      <span className="text-xs bg-white/20 text-white px-2 py-0.5 rounded font-mono">INST #{selectedMinute.visitInstanceId}</span>
                      <span className={`text-xs px-2 py-0.5 rounded font-bold ${selectedMinute.status === 'SAVED' ? 'bg-green-500/20 text-green-100 border border-green-400/30' : 'bg-slate-500/30 text-slate-100 border border-slate-400/30'}`}>
                        {formatMinutesStatus(selectedMinute.status)}
                      </span>
                      {user?.campusName && (
                        <span className="text-xs bg-white/20 text-white px-2 py-0.5 rounded">{user.campusName}</span>
                      )}
                   </div>
                 </div>
                 <div className="flex items-center gap-3">
                    {/* Trang "Quản lý biên bản" chỉ để XEM — không có quyền chỉnh sửa. Chỉnh sửa
                        biên bản chỉ thực hiện qua MinutesCard (trong Quy trình tiếp khách). */}
                    <button
                      onClick={(e) => handleDownloadPDF(e, selectedMinute.minutesId)}
                      disabled={downloading !== null}
                      className="flex items-center gap-2 bg-white/10 hover:bg-white/20 border border-white/20 text-white px-4 py-2 rounded-lg text-sm font-bold transition-colors outline-none cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <Download className="w-4 h-4" /> {downloading === 'PDF' ? 'Đang tải...' : 'PDF'}
                    </button>
                    <button
                      onClick={(e) => handleDownloadExcel(e, selectedMinute.minutesId)}
                      disabled={downloading !== null}
                      className="flex items-center gap-2 bg-white/10 hover:bg-white/20 border border-white/20 text-white px-4 py-2 rounded-lg text-sm font-bold transition-colors outline-none cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      <FileText className="w-4 h-4" /> {downloading === 'EXCEL' ? 'Đang tải...' : 'Excel'}
                    </button>
                    <button onClick={closeDetail} className="text-white hover:bg-white/20 p-2 rounded-full transition-colors cursor-pointer outline-none ml-2">
                       <X className="w-5 h-5" />
                    </button>
                 </div>
              </div>

              {/* Body */}
              <div className="px-6 py-4 overflow-y-auto w-full flex-1 bg-white text-sm">
                 {loading && !detailData ? (
                   <div className="flex justify-center items-center h-40">
                     <span className="text-slate-500">Đang tải chi tiết...</span>
                   </div>
                 ) : detailData ? (
                   <div className="space-y-4">
                      {/* Cảnh báo: biên bản đang bị khóa sửa ở nơi khác (MinutesCard) — trang này chỉ xem. */}
                      {detailData.editLockedBy && (
                        <div className="flex items-start gap-2 text-orange-700 bg-orange-50 border-l-2 border-orange-400 px-3 py-2 text-xs">
                          <Lock className="w-3.5 h-3.5 shrink-0 mt-0.5" />
                          <div>
                            <span className="font-bold">Đang được chỉnh sửa bởi {detailData.editLockedByName || detailData.editLockedBy}</span>
                            <span className="ml-1">— Lock hết hạn: {formatVietnamDateTime(detailData.editLockExpiresAt!)}</span>
                          </div>
                        </div>
                      )}

                      {/* Thông tin đoàn — key-value compact */}
                      <div>
                        <h3 className="text-xs font-bold text-[#004c91] uppercase tracking-wide mb-2 flex items-center gap-1.5"><Building2 className="w-3.5 h-3.5" /> Thông tin đoàn / Chuyến thăm tại cơ sở</h3>
                        <dl className="grid grid-cols-1 md:grid-cols-2 gap-x-6 gap-y-1 text-sm">
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Đoàn khách:</dt><dd className="font-semibold text-slate-800 truncate">{selectedMinute.visitTitle || 'Chưa có dữ liệu'}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Trạng thái:</dt><dd className="font-normal text-slate-800">{detailData.status ? formatVisitStatus(detailData.status) : 'Chưa có dữ liệu'}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Host:</dt><dd className="font-normal text-slate-800 truncate">{selectedMinute.hostName || 'Chưa có dữ liệu'}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Campus:</dt><dd className="font-normal text-slate-800 truncate">{selectedMinute.campusName || 'Chưa có dữ liệu'}</dd></div>
                          <div className="flex gap-1.5 md:col-span-2"><dt className="text-slate-500 shrink-0">Thời gian dự kiến:</dt><dd className="font-normal text-slate-800">
                            {selectedMinute.plannedStartAt ? formatVietnamDateTime(selectedMinute.plannedStartAt) : '?'} - {selectedMinute.plannedEndAt ? formatVietnamDateTime(selectedMinute.plannedEndAt) : '?'}
                          </dd></div>
                        </dl>
                      </div>

                      <hr className="border-slate-100" />

                      {/* Nội dung biên bản */}
                      <div>
                        <h3 className="text-xs font-bold text-[#004c91] uppercase tracking-wide mb-2 flex items-center gap-1.5"><FileText className="w-3.5 h-3.5" /> Nội dung biên bản</h3>
                        {detailData.content?.trim() ? (
                          // ql-editor (không phải "prose" — chưa cài @tailwindcss/typography) để bullet/
                          // số thứ tự/indent hiện đúng như lúc gõ trong RichTextEditor.
                          <div
                            className="ql-editor !h-auto !p-0 !min-h-0 text-sm text-slate-700 leading-relaxed"
                            dangerouslySetInnerHTML={{ __html: sanitizeHtml(detailData.content) }}
                          />
                        ) : (
                          <div className="text-sm italic text-slate-400">Chưa có dữ liệu nội dung.</div>
                        )}
                      </div>

                      <hr className="border-slate-100" />

                      {/* Danh sách người tham gia */}
                      <div>
                        <div className="flex items-center justify-between mb-2">
                          <h3 className="text-xs font-bold text-[#004c91] uppercase tracking-wide flex items-center gap-1.5">
                            <Users className="w-3.5 h-3.5" /> Người tham gia & điểm danh
                          </h3>
                          <div className="flex gap-2 text-[11px] text-slate-500">
                            <span>Tổng: <span className="font-bold text-slate-700">{detailData.participants?.length || 0}</span></span>
                            <span>Có mặt: <span className="font-bold text-green-700">{detailData.participants?.filter(p => p.attendanceStatus === 'PRESENT').length || 0}</span></span>
                          </div>
                        </div>
                        <div className="overflow-x-auto max-h-[260px] overflow-y-auto">
                          <table className="w-full text-left text-xs">
                            <thead className="bg-slate-50 sticky top-0">
                              <tr className="text-slate-500 font-bold uppercase text-[10px]">
                                <th className="px-2 py-1.5">Tên & Vai trò</th>
                                <th className="px-2 py-1.5">Đơn vị / Loại</th>
                                <th className="px-2 py-1.5 text-center">Trạng thái</th>
                                <th className="px-2 py-1.5">Ghi chú</th>
                              </tr>
                            </thead>
                            <tbody className="divide-y divide-slate-100">
                              {(detailData.participants || []).map((p, idx) => (
                                <tr key={p.minuteParticipantId || idx} className="hover:bg-slate-50/50">
                                  <td className="px-2 py-1.5">
                                    <div className="font-bold text-slate-800">{p.fullNameSnapshot || '-'}</div>
                                    <div className="text-slate-500">{p.roleSnapshot || '-'}</div>
                                  </td>
                                  <td className="px-2 py-1.5">
                                    <span className="text-slate-700">{p.organizationSnapshot || '-'}</span>
                                    <span className="ml-1.5 text-[10px] text-slate-400">
                                      {p.userId ? '(Internal)' : p.guestMemberId ? '(Guest)' : '(Snapshot only)'}
                                    </span>
                                  </td>
                                  <td className="px-2 py-1.5 text-center">
                                    {p.attendanceStatus === 'PRESENT' && <span className="text-green-700 font-normal">{formatAttendanceStatus('PRESENT')}</span>}
                                    {p.attendanceStatus === 'ABSENT' && <span className="text-red-700 font-normal">{formatAttendanceStatus('ABSENT')}</span>}
                                    {p.attendanceStatus === 'EXCUSED' && <span className="text-yellow-700 font-normal">{formatAttendanceStatus('EXCUSED')}</span>}
                                    {!p.attendanceStatus && <span className="text-slate-400">-</span>}
                                  </td>
                                  <td className="px-2 py-1.5 text-slate-600">{p.attendanceNote || '-'}</td>
                                </tr>
                              ))}
                              {detailData.participants?.length === 0 && (
                                <tr><td colSpan={4} className="px-2 py-4 text-center text-slate-500">Chưa có người tham gia</td></tr>
                              )}
                            </tbody>
                          </table>
                        </div>
                      </div>

                      <hr className="border-slate-100" />

                      {/* Đầu mục công việc */}
                      <div>
                        <h3 className="text-xs font-bold text-[#004c91] uppercase tracking-wide mb-2 flex items-center gap-1.5">
                          <CheckSquare className="w-3.5 h-3.5" /> Đầu mục công việc
                        </h3>
                        <div className="divide-y divide-slate-100">
                          {(detailData.actionItems || []).map((ai, idx) => {
                            const isOverdue = ai.dueDate && new Date(ai.dueDate) < new Date() && ai.status !== 'DONE' && ai.status !== 'CANCELLED';
                            return (
                              <div key={ai.actionItemId || idx} className={`py-2 flex flex-col md:flex-row md:items-center justify-between gap-2 ${ai.status === 'DONE' ? 'opacity-70' : ''}`}>
                                <div className="flex-1 flex gap-2">
                                  <div>
                                    <div className="flex items-center gap-1.5 flex-wrap">
                                      <span className={`text-[10px] font-bold px-1.5 py-0.5 rounded uppercase
                                        ${ai.status === 'DONE' ? 'bg-green-100 text-green-700' :
                                          ai.status === 'IN_PROGRESS' ? 'bg-blue-100 text-blue-700' :
                                          ai.status === 'CANCELLED' ? 'bg-slate-100 text-slate-600' :
                                          'bg-orange-100 text-orange-800'}`}>
                                        {formatActionItemStatus(ai.status)}
                                      </span>
                                      {isOverdue && <span className="text-[10px] font-bold px-1.5 py-0.5 rounded uppercase bg-red-100 text-red-700">Quá hạn</span>}
                                      <h4 className={`font-bold text-sm ${ai.status === 'DONE' ? 'text-slate-500 line-through' : 'text-slate-800'}`}>{ai.title}</h4>
                                    </div>
                                    {ai.note && <p className="text-xs text-slate-600 mt-0.5">{ai.note}</p>}
                                    <p className="text-[11px] text-slate-500 mt-0.5 flex items-center gap-1">
                                      <User className="w-3 h-3 shrink-0" />
                                      {ai.assignedToUserName ? (
                                        <>Phụ trách: <span className="font-normal text-slate-700">{ai.assignedToUserName}</span></>
                                      ) : 'Chưa có người phụ trách'}
                                    </p>
                                  </div>
                                </div>
                                <div className="flex items-center gap-3 text-[11px] text-slate-500 shrink-0">
                                  {ai.dueDate && <span className="flex items-center gap-1"><Calendar className="w-3 h-3"/> Deadline: {formatVietnamDate(ai.dueDate)}</span>}
                                  {ai.completedAt && <span className="flex items-center gap-1 text-green-600"><CheckSquare className="w-3 h-3"/> HT: {formatVietnamDate(ai.completedAt)}</span>}
                                </div>
                              </div>
                            );
                          })}
                          {detailData.actionItems?.length === 0 && (
                            <div className="text-center py-4 text-slate-500 text-sm">Biên bản này chưa có đầu mục công việc.</div>
                          )}
                        </div>
                      </div>

                      <hr className="border-slate-100" />

                      {/* Audit */}
                      <div>
                        <h3 className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-2 flex items-center gap-1.5"><ShieldAlert className="w-3.5 h-3.5" /> Audit & Concurrency</h3>
                        <dl className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-slate-600">
                          <div className="flex gap-1.5"><dt className="text-slate-500">Ngày khóa:</dt><dd className="font-normal text-slate-800">{detailData.editLockedAt ? formatVietnamDateTime(detailData.editLockedAt) : 'N/A'}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500">Ngày cập nhật:</dt><dd className="font-normal text-slate-800">{detailData.updatedAt ? formatVietnamDateTime(detailData.updatedAt) : 'N/A'}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500">Row version:</dt><dd className="font-normal text-slate-800">{detailData.rowVersion}</dd></div>
                        </dl>
                      </div>
                   </div>
                 ) : (
                   <div className="text-center py-10 text-red-500">Không tải được chi tiết.</div>
                 )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
