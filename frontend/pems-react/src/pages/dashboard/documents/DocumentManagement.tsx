/**
 * Trang DocumentManagement
 * Kho lưu trữ số các loại quy định mẫu, biên nhận biểu mẫu, tải về cho nhân sự.
 */

import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Search, Filter, Download, ExternalLink, ArrowDown, ChevronLeft, ChevronRight, FileText, ChevronDown, ChevronUp, Eye, X, Mail, Share2 } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { useDocuments } from '../../../features/documents/hooks/useDocuments';
import { useAuth } from '../../../shared/hooks/useAuth';
import { DocumentOwnerType, DocumentStatus, StorageProvider, DocumentListItem } from '../../../features/documents/types/documents.types';
import { formatFileSize } from '../../../shared/utils/fileUtils';
import { resolveFileUrl } from '../../../shared/utils/resolveFileUrl';
import toast from 'react-hot-toast';
import { DocumentDetailModal } from './DocumentDetailModal';

export function DocumentManagement() {
  const navigate = useNavigate();
  const { user } = useAuth();
  
  const [searchQuery, setSearchQuery] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  
  // Advanced filters state
  const [showAdvancedFilters, setShowAdvancedFilters] = useState(false);
  const [statusFilter, setStatusFilter] = useState<DocumentStatus | 'All'>('All');
  const [ownerTypeFilter, setOwnerTypeFilter] = useState<DocumentOwnerType | 'All'>('All');
  const [documentCategoryFilter, setDocumentCategoryFilter] = useState('All');
  const [storageProviderFilter, setStorageProviderFilter] = useState<StorageProvider | 'All'>('All');
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');

  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [sortBy, setSortBy] = useState('uploaded_at');
  const [sortDir, setSortDir] = useState<'asc' | 'desc'>('desc');

  // Modal State
  const [selectedDoc, setSelectedDoc] = useState<DocumentListItem | null>(null);

  // React Query hook
  const { data: documentData, isLoading, isError } = useDocuments({
    q: debouncedSearch || undefined,
    status: statusFilter,
    ownerType: ownerTypeFilter,
    documentCategory: documentCategoryFilter !== 'All' ? documentCategoryFilter : undefined,
    storageProvider: storageProviderFilter,
    uploadedFrom: dateFrom || undefined,
    uploadedTo: dateTo || undefined,
    page,
    pageSize,
    sortBy,
    sortDir
  });

  // Handle Search Debounce
  React.useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedSearch(searchQuery);
      setPage(1); // reset to page 1 on search
    }, 500);
    return () => clearTimeout(handler);
  }, [searchQuery]);

  const handleSortToggle = () => {
    setSortDir(prev => prev === 'desc' ? 'asc' : 'desc');
  };

  const handleResetFilters = () => {
    setSearchQuery('');
    setDebouncedSearch('');
    setStatusFilter('All');
    setOwnerTypeFilter('All');
    setDocumentCategoryFilter('All');
    setStorageProviderFilter('All');
    setDateFrom('');
    setDateTo('');
    setPage(1);
  };

  const handleShareZalo = (doc: DocumentListItem) => {
    const link = doc.webViewUrl || doc.downloadUrl || resolveFileUrl(`/api/files/${doc.fileId}/content`) || window.location.href;
    navigator.clipboard.writeText(link as string).then(() => {
      toast.success('Đã sao chép link! Hãy dán vào Zalo để chia sẻ.');
      window.open('https://chat.zalo.me', '_blank');
    }).catch(() => {
      toast.error('Không thể sao chép đường dẫn.');
    });
  };

  const handleShareGmail = (doc: DocumentListItem) => {
    const link = doc.webViewUrl || doc.downloadUrl || resolveFileUrl(`/api/files/${doc.fileId}/content`) || window.location.href;
    const subject = doc.title || 'Chia sẻ tài liệu';
    const body = `Chào bạn,\n\nMình chia sẻ tài liệu: ${doc.title}\nLink truy cập: ${link}`;
    window.open(`https://mail.google.com/mail/?view=cm&fs=1&su=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`, '_blank');
  };

  const docs = documentData?.items || [];
  const totalCount = documentData?.totalCount || 0;
  const totalPages = documentData?.totalPages || 1;

  // Summaries calculation on current page for demo - if backend returns stats, use them instead.
  const summary = {
    total: totalCount,
    draft: docs.filter(d => d.status === 'DRAFT').length,
    published: docs.filter(d => d.status === 'PUBLISHED').length,
    archived: docs.filter(d => d.status === 'ARCHIVED').length,
  };

  return (
    <div className="w-full pb-12 flex flex-col space-y-6 animate-in fade-in duration-300">
      {/* 1. Header & Navigation Layer */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Quản lý tài liệu</span>
      </div>
      
      <div className="border-b border-gray-100 pb-4 flex flex-col md:flex-row justify-between items-start md:items-center gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tài liệu</h1>
            <span className="inline-flex px-2.5 py-1 text-xs font-semibold rounded-md bg-blue-100 text-blue-800 border border-blue-200 shadow-sm">
              Campus: {user?.campusName || 'Hệ thống'}
            </span>
          </div>
          <p className="text-gray-500 mt-2 font-medium">
            Tra cứu, xem và tải xuống tài liệu nghiệp vụ của campus {user?.campusName || 'Hệ thống'} được lưu trên Google Drive
          </p>
        </div>
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
          <p className="text-sm text-slate-500 font-medium">Tổng tài liệu</p>
          <p className="text-2xl font-bold text-[#004c91]">{summary.total}</p>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
          <p className="text-sm text-slate-500 font-medium">DRAFT</p>
          <p className="text-2xl font-bold text-yellow-600">{summary.draft}</p>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
          <p className="text-sm text-slate-500 font-medium">PUBLISHED</p>
          <p className="text-2xl font-bold text-emerald-600">{summary.published}</p>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
          <p className="text-sm text-slate-500 font-medium">ARCHIVED</p>
          <p className="text-2xl font-bold text-slate-600">{summary.archived}</p>
        </div>
      </div>

      {/* 2. Action & Filter Controller */}
      <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm space-y-4">
        <div className="flex flex-col lg:flex-row items-center gap-4">
          <div className="relative flex-1 w-full group">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 text-slate-400 w-5 h-5 group-focus-within:text-[#004c91] transition-colors pointer-events-none" />
            <input 
              type="text" 
              placeholder="Tìm kiếm tài liệu, filename..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-11 pr-4 py-2.5 border border-slate-200 rounded-lg bg-white text-sm font-medium text-slate-800 focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] transition-all placeholder:text-slate-400"
            />
          </div>
          
          <button 
            onClick={() => setShowAdvancedFilters(!showAdvancedFilters)}
            className="flex items-center gap-2 px-4 py-2.5 bg-slate-50 border border-slate-200 text-slate-700 rounded-lg text-sm font-medium hover:bg-slate-100 transition-colors shrink-0 cursor-pointer"
          >
            <Filter className="w-4 h-4" />
            Bộ lọc nâng cao
            {showAdvancedFilters ? <ChevronUp className="w-4 h-4 ml-1" /> : <ChevronDown className="w-4 h-4 ml-1" />}
          </button>
        </div>

        <AnimatePresence>
          {showAdvancedFilters && (
            <motion.div 
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: 'auto', opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              className="overflow-hidden"
            >
              <div className="pt-4 border-t border-slate-100 grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-500 uppercase">Trạng thái</label>
                  <select 
                    value={statusFilter}
                    onChange={(e) => setStatusFilter(e.target.value as any)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:border-[#004c91]"
                  >
                    <option value="All">Tất cả</option>
                    <option value="DRAFT">DRAFT</option>
                    <option value="PUBLISHED">PUBLISHED</option>
                    <option value="ARCHIVED">ARCHIVED</option>
                  </select>
                </div>
                
                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-500 uppercase">Nghiệp vụ (Owner)</label>
                  <select 
                    value={ownerTypeFilter}
                    onChange={(e) => setOwnerTypeFilter(e.target.value as any)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:border-[#004c91]"
                  >
                    <option value="All">Tất cả</option>
                    <option value="GENERAL">GENERAL</option>
                    <option value="VISIT">VISIT</option>
                    <option value="PARTNER">PARTNER</option>
                    <option value="MINUTES">MINUTES</option>
                    <option value="NEWS">NEWS</option>
                    <option value="LOGISTICS">LOGISTICS</option>
                    <option value="REPORT">REPORT</option>
                  </select>
                </div>

                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-500 uppercase">Ngày tải lên (Từ)</label>
                  <input 
                    type="date" 
                    value={dateFrom}
                    onChange={(e) => setDateFrom(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:border-[#004c91]"
                  />
                </div>

                <div className="space-y-1.5">
                  <label className="text-xs font-semibold text-slate-500 uppercase">Ngày tải lên (Đến)</label>
                  <input 
                    type="date" 
                    value={dateTo}
                    onChange={(e) => setDateTo(e.target.value)}
                    className="w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:outline-none focus:border-[#004c91]"
                  />
                </div>

                <div className="col-span-full flex justify-end gap-2 mt-2">
                  <button onClick={handleResetFilters} className="px-4 py-2 text-sm font-medium text-slate-600 hover:text-slate-900 cursor-pointer">
                    Reset
                  </button>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>

      {/* 3. Data View */}
      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse min-w-[900px]">
            <thead>
              <tr className="bg-[#004c91] border-b border-[#004c91]">
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap">Tài liệu</th>
                {/* Thu nhỏ cột Loại */}
                <th className="px-4 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap w-[140px]">Loại</th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider text-center whitespace-nowrap w-[120px]">Trạng thái</th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider whitespace-nowrap w-[140px]">File</th>
                <th 
                  className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider cursor-pointer group hover:bg-[#00386b] transition-colors whitespace-nowrap w-[160px]"
                  onClick={handleSortToggle}
                >
                  <div className="flex items-center gap-1">
                    Thời gian
                    <ArrowDown className={`w-4 h-4 text-white/70 group-hover:text-white transition-transform duration-300 ${sortDir === 'asc' ? 'rotate-180' : ''}`} />
                  </div>
                </th>
                <th className="px-6 py-4 text-sm font-bold text-white uppercase tracking-wider text-center whitespace-nowrap w-[80px]">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-slate-500">Đang tải dữ liệu...</td>
                </tr>
              ) : isError ? (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center text-red-500">Đã xảy ra lỗi khi tải dữ liệu.</td>
                </tr>
              ) : docs.length > 0 ? docs.map((doc, index) => (
                <motion.tr 
                  initial={{ opacity: 0, y: 10 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: index * 0.05 }}
                  key={doc.documentId} 
                  className="transition-colors duration-200 hover:bg-slate-50/50 group"
                >
                  {/* Tài liệu */}
                  <td className="px-6 py-4">
                    <div className="flex items-start gap-3">
                      <div className="w-10 h-10 rounded-lg bg-[#004c91]/5 border border-[#004c91]/10 flex items-center justify-center shrink-0 mt-1">
                        <FileText className="w-5 h-5 text-[#004c91]" />
                      </div>
                      <div className="flex flex-col max-w-[280px]">
                        <span className="text-[15px] font-semibold text-slate-800 line-clamp-2" title={doc.title}>{doc.title}</span>
                        <span className="text-xs text-slate-500 mt-1 truncate" title={doc.originalFilename}>{doc.originalFilename}</span>
                        <span className="text-[10px] font-medium text-slate-400 mt-1 uppercase tracking-wider">
                          DOC #{doc.documentId} &middot; FILE #{doc.fileId || 'N/A'}
                        </span>
                      </div>
                    </div>
                  </td>
                  
                  {/* Loại */}
                  <td className="px-4 py-4 w-[140px] max-w-[140px]">
                    <div className="flex flex-col items-start gap-1">
                      <span className="inline-flex px-2 py-0.5 text-[10px] font-bold rounded bg-slate-100 text-slate-600 border border-slate-200">
                        {doc.ownerType}
                      </span>
                      <span className="text-xs font-medium text-slate-700 truncate w-full" title={doc.ownerDisplayName || `${doc.ownerType} #${doc.ownerId || 'N/A'}`}>
                        {doc.ownerDisplayName || `${doc.ownerType} #${doc.ownerId || 'N/A'}`}
                      </span>
                      {doc.documentCategory && (
                        <span className="text-[10px] text-slate-500 truncate w-full" title={doc.documentCategory}>{doc.documentCategory}</span>
                      )}
                    </div>
                  </td>

                  {/* Trạng thái */}
                  <td className="px-6 py-4 whitespace-nowrap text-center">
                    <span className={`inline-flex px-2.5 py-1 text-xs font-semibold rounded-full border ${
                      doc.status === 'PUBLISHED' ? 'bg-emerald-50 text-emerald-700 border-emerald-200' :
                      doc.status === 'DRAFT' ? 'bg-amber-50 text-amber-700 border-amber-200' :
                      'bg-slate-100 text-slate-700 border-slate-200'
                    }`}>
                      {doc.status}
                    </span>
                  </td>

                  {/* File */}
                  <td className="px-6 py-4 whitespace-nowrap">
                    <div className="flex flex-col gap-1">
                      <span className="text-sm font-bold text-slate-700 uppercase">
                        {doc.mimeType?.split('/').pop() || doc.originalFilename?.split('.').pop() || 'N/A'}
                      </span>
                      <span className="text-xs font-medium text-slate-500">
                        {doc.fileSize ? formatFileSize(doc.fileSize) : 'N/A'}
                      </span>
                      {doc.storageProvider === 'GOOGLE_DRIVE' && (
                         <span className="text-[10px] font-bold text-blue-600 flex items-center gap-1 mt-0.5">
                            <span className="w-1.5 h-1.5 rounded-full bg-blue-600"></span> DRIVE
                         </span>
                      )}
                    </div>
                  </td>

                  {/* Thời gian */}
                  <td className="px-6 py-4 whitespace-nowrap">
                     <div className="flex flex-col gap-1 text-xs text-slate-600">
                        <span><span className="font-semibold">Tải lên:</span> {doc.createdAt ? new Date(doc.createdAt).toLocaleString('vi-VN') : 'N/A'}</span>
                        <span><span className="font-semibold">Cập nhật:</span> {doc.updatedAt ? new Date(doc.updatedAt).toLocaleString('vi-VN') : 'Chưa cập nhật'}</span>
                     </div>
                  </td>

                  {/* Hành động (chỉ 1 icon mở modal) */}
                  <td className="px-6 py-4 w-[80px] text-center">
                    <button 
                      onClick={() => setSelectedDoc(doc)}
                      className="p-2 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 rounded-full transition-colors cursor-pointer inline-flex items-center justify-center shadow-sm border border-transparent hover:border-blue-100"
                      title="Xem chi tiết & Hành động"
                    >
                      <Eye className="w-5 h-5" />
                    </button>
                  </td>
                </motion.tr>
              )) : (
                <tr>
                  <td colSpan={6} className="px-6 py-12 text-center">
                     <div className="flex flex-col items-center justify-center">
                        <div className="w-16 h-16 bg-slate-50 rounded-full flex items-center justify-center mb-4">
                          <Search className="w-8 h-8 text-slate-300" />
                        </div>
                        <p className="text-slate-500 font-medium font-sans">
                          {searchQuery || statusFilter !== 'All' ? 'Không tìm thấy tài liệu phù hợp với bộ lọc.' : 'Chưa có tài liệu nào trong campus của bạn.'}
                        </p>
                     </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        
        {/* Pagination */}
        {docs.length > 0 && (
          <div className="px-6 py-4 border-t border-slate-200 bg-slate-50/50 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-2 text-sm text-gray-600">
              <span>Hiển thị</span>
              <select 
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setPage(1);
                }}
                className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-medium"
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
              </select>
              <span>bản ghi / trang</span>
            </div>
            <div className="flex items-center gap-2">
              <button 
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
                className="p-1.5 border border-slate-200 rounded-lg text-slate-600 hover:bg-white disabled:text-slate-400 disabled:cursor-not-allowed transition-colors"
              >
                <ChevronLeft className="w-5 h-5" />
              </button>
              <span className="text-sm font-medium text-slate-700">Trang {page} / {totalPages}</span>
              <button 
                disabled={page >= totalPages}
                onClick={() => setPage(p => p + 1)}
                className="p-1.5 border border-slate-200 rounded-lg text-slate-600 hover:bg-white disabled:text-slate-400 disabled:cursor-not-allowed transition-colors"
              >
                <ChevronRight className="w-5 h-5" />
              </button>
            </div>
          </div>
        )}
      </div>

      {/* Modal Xem chi tiết có Preview */}
      <DocumentDetailModal 
        documentId={selectedDoc ? selectedDoc.documentId : null}
        onClose={() => setSelectedDoc(null)}
      />
    </div>
  );
}
