import React from 'react';
import { motion } from 'framer-motion';
import { Search, ArrowDown, ChevronLeft, ChevronRight, FileText, Eye } from 'lucide-react';
import { DocumentListItem } from '../types/documents.types';
import { formatDocumentType, formatDocumentStatus } from '../../../shared/utils/domainLabels';
import { formatFileSize } from '../../../shared/utils/fileUtils';

import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
interface Props {
  docs: DocumentListItem[];
  isLoading: boolean;
  isError: boolean;
  searchQuery: string;
  statusFilterActive: boolean;
  sortDir: 'asc' | 'desc';
  onSortToggle: () => void;
  onView: (doc: DocumentListItem) => void;
  page: number;
  pageSize: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}

export function DocumentTable({
  docs,
  isLoading,
  isError,
  searchQuery,
  statusFilterActive,
  sortDir,
  onSortToggle,
  onView,
  page,
  pageSize,
  totalPages,
  onPageChange,
  onPageSizeChange,
}: Props) {
  return (
    <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
      <div className="overflow-x-auto">
        <table className="w-full text-left border-collapse min-w-[900px]">
          <thead>
            <tr className="bg-[#004c91] border-b border-[#004c91]">
              <th className="px-4 py-2.5 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap">Tài liệu</th>
              <th className="px-3 py-2.5 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap w-[130px]">Loại</th>
              <th className="px-4 py-2.5 text-xs font-bold text-white uppercase tracking-wider text-center whitespace-nowrap w-[110px]">Trạng thái</th>
              <th className="px-4 py-2.5 text-xs font-bold text-white uppercase tracking-wider whitespace-nowrap w-[130px]">File</th>
              <th
                className="px-4 py-2.5 text-xs font-bold text-white uppercase tracking-wider cursor-pointer group hover:bg-[#00386b] transition-colors whitespace-nowrap w-[150px]"
                onClick={onSortToggle}
              >
                <div className="flex items-center gap-1">
                  Thời gian
                  <ArrowDown className={`w-3.5 h-3.5 text-white/70 group-hover:text-white transition-transform duration-300 ${sortDir === 'asc' ? 'rotate-180' : ''}`} />
                </div>
              </th>
              <th className="px-4 py-2.5 text-xs font-bold text-white uppercase tracking-wider text-center whitespace-nowrap w-[70px]">Hành động</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {isLoading ? (
              <tr>
                <td colSpan={6} className="px-6 py-10 text-center text-slate-500">Đang tải dữ liệu...</td>
              </tr>
            ) : isError ? (
              <tr>
                <td colSpan={6} className="px-6 py-10 text-center text-red-500">Đã xảy ra lỗi khi tải dữ liệu.</td>
              </tr>
            ) : docs.length > 0 ? docs.map((doc, index) => (
              <motion.tr
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.03 }}
                key={doc.documentId}
                className="transition-colors duration-200 hover:bg-slate-50/50 group"
              >
                {/* Tài liệu */}
                <td className="px-4 py-2.5">
                  <div className="flex items-start gap-2.5">
                    <div className="w-8 h-8 rounded-lg bg-[#004c91]/5 border border-[#004c91]/10 flex items-center justify-center shrink-0 mt-0.5">
                      <FileText className="w-4 h-4 text-[#004c91]" />
                    </div>
                    <div className="flex flex-col max-w-[260px]">
                      <span className="text-sm font-semibold text-slate-800 line-clamp-2" title={doc.title}>{doc.title}</span>
                      <span className="text-xs text-slate-500 mt-0.5 truncate" title={doc.originalFilename}>{doc.originalFilename}</span>
                      <span className="text-[10px] font-medium text-slate-400 mt-0.5 uppercase tracking-wider">
                        DOC #{doc.documentId} &middot; FILE #{doc.fileId || 'N/A'}
                      </span>
                    </div>
                  </div>
                </td>

                {/* Loại */}
                <td className="px-3 py-2.5 w-[130px] max-w-[130px]">
                  <div className="flex flex-col items-start gap-0.5">
                    <span className="inline-flex px-1.5 py-0.5 text-[10px] font-bold rounded bg-slate-100 text-slate-600 border border-slate-200">
                      {formatDocumentType(doc.ownerType)}
                    </span>
                    <span className="text-xs font-normal text-slate-700 truncate w-full" title={doc.ownerDisplayName || `${formatDocumentType(doc.ownerType)} #${doc.ownerId || 'N/A'}`}>
                      {doc.ownerDisplayName || `${formatDocumentType(doc.ownerType)} #${doc.ownerId || 'N/A'}`}
                    </span>
                    {doc.documentCategory && (
                      <span className="text-[10px] text-slate-500 truncate w-full" title={doc.documentCategory}>{doc.documentCategory}</span>
                    )}
                  </div>
                </td>

                {/* Trạng thái */}
                <td className="px-4 py-2.5 whitespace-nowrap text-center">
                  <span className={`inline-flex px-2 py-0.5 text-[11px] font-semibold rounded-full border ${
                    doc.status === 'PUBLISHED' ? 'bg-emerald-50 text-emerald-700 border-emerald-200' :
                    doc.status === 'DRAFT' ? 'bg-amber-50 text-amber-700 border-amber-200' :
                    'bg-slate-100 text-slate-700 border-slate-200'
                  }`}>
                    {formatDocumentStatus(doc.status)}
                  </span>
                </td>

                {/* File */}
                <td className="px-4 py-2.5">
                  <div className="flex flex-col gap-0.5 w-[110px]">
                    <span className="text-xs font-normal text-slate-700 uppercase break-words">
                      {doc.originalFilename?.split('.').pop() || doc.mimeType?.split('/').pop() || 'N/A'}
                    </span>
                    <span className="text-[11px] font-normal text-slate-500">
                      {doc.fileSize ? formatFileSize(doc.fileSize) : 'N/A'}
                    </span>
                    {doc.storageProvider === 'GOOGLE_DRIVE' && (
                       <span className="text-[10px] font-bold text-blue-600 flex items-center gap-1">
                          <span className="w-1.5 h-1.5 rounded-full bg-blue-600"></span> DRIVE
                       </span>
                    )}
                  </div>
                </td>

                {/* Thời gian */}
                <td className="px-4 py-2.5 whitespace-nowrap">
                   <div className="flex flex-col gap-0.5 text-[11px] text-slate-600">
                      <span><span className="font-semibold">Tải lên:</span> {doc.createdAt ? formatVietnamDateTime(doc.createdAt) : 'N/A'}</span>
                      <span><span className="font-semibold">Cập nhật:</span> {doc.updatedAt ? formatVietnamDateTime(doc.updatedAt) : 'Chưa cập nhật'}</span>
                   </div>
                </td>

                {/* Hành động */}
                <td className="px-4 py-2.5 w-[70px] text-center">
                  <button
                    onClick={() => onView(doc)}
                    className="p-1.5 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 rounded-full transition-colors cursor-pointer inline-flex items-center justify-center shadow-sm border border-transparent hover:border-blue-100"
                    title="Xem chi tiết & Hành động"
                  >
                    <Eye className="w-4 h-4" />
                  </button>
                </td>
              </motion.tr>
            )) : (
              <tr>
                <td colSpan={6} className="px-6 py-10 text-center">
                   <div className="flex flex-col items-center justify-center">
                      <div className="w-14 h-14 bg-slate-50 rounded-full flex items-center justify-center mb-3">
                        <Search className="w-7 h-7 text-slate-300" />
                      </div>
                      <p className="text-slate-500 font-normal font-sans text-sm">
                        {searchQuery || statusFilterActive ? 'Không tìm thấy tài liệu phù hợp với bộ lọc.' : 'Chưa có tài liệu nào trong campus của bạn.'}
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
        <div className="px-4 py-3 border-t border-slate-200 bg-slate-50/50 flex flex-col sm:flex-row items-center justify-between gap-3">
          <div className="flex items-center gap-2 text-xs text-gray-600">
            <span>Hiển thị</span>
            <select
              value={pageSize}
              onChange={(e) => onPageSizeChange(Number(e.target.value))}
              className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-normal"
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
              onClick={() => onPageChange(page - 1)}
              className="p-1.5 border border-slate-200 rounded-lg text-slate-600 hover:bg-white disabled:text-slate-400 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="text-xs font-normal text-slate-700">Trang {page} / {totalPages}</span>
            <button
              disabled={page >= totalPages}
              onClick={() => onPageChange(page + 1)}
              className="p-1.5 border border-slate-200 rounded-lg text-slate-600 hover:bg-white disabled:text-slate-400 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
