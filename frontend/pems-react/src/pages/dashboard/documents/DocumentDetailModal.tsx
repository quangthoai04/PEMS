import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, FileText, Download, ExternalLink, Share2, Mail, Loader2, ChevronDown, ChevronUp } from 'lucide-react';
import { useDocumentDetail } from '../../../features/documents/hooks/useDocumentDetail';
import { formatFileSize } from '../../../shared/utils/fileUtils';
import { resolveFileUrl } from '../../../shared/utils/resolveFileUrl';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';

interface DocumentDetailModalProps {
  documentId: number | null;
  onClose: () => void;
}

export function DocumentDetailModal({ documentId, onClose }: DocumentDetailModalProps) {
  const { data: detail, isLoading, isError } = useDocumentDetail(documentId);

  const handleShareZalo = (link: string) => {
    navigator.clipboard.writeText(link).then(() => {
      toast.success('Đã sao chép link! Hãy dán vào Zalo để chia sẻ.');
      window.open('https://chat.zalo.me', '_blank');
    }).catch(() => {
      toast.error('Không thể sao chép đường dẫn.');
    });
  };

  const handleShareGmail = (title: string, link: string) => {
    const subject = title || 'Chia sẻ tài liệu';
    const body = `Chào bạn,\n\nMình chia sẻ tài liệu: ${title}\nLink truy cập: ${link}`;
    window.open(`https://mail.google.com/mail/?view=cm&fs=1&su=${encodeURIComponent(subject)}&body=${encodeURIComponent(body)}`, '_blank');
  };

  const handleDownload = async (fileId: number, filename: string) => {
    try {
      const toastId = toast.loading('Đang tải file...');
      const response = await httpClient.get(`/files/${fileId}/content`, {
        responseType: 'blob'
      });
      const url = window.URL.createObjectURL(new Blob([response.data]));
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', filename);
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
      toast.success('Tải file thành công!', { id: toastId });
    } catch (error) {
      toast.error('Có lỗi xảy ra hoặc bạn không có quyền tải file này.');
    }
  };

  const renderOwnerContext = () => {
    if (!detail) return null;
    const ownerType = detail.document.ownerType;
    const { ownerContext } = detail;
    const ctx = ownerContext || {};

    switch (ownerType) {
      case 'VISIT':
        return (
          <div className="p-3 rounded-lg border border-slate-100 bg-blue-50/30">
            <p className="text-[11px] font-bold text-[#004c91] uppercase tracking-wider mb-2">Thông tin đoàn khách</p>
            <ul className="text-sm text-slate-600 space-y-1">
              <li><span className="font-semibold text-slate-700">Tên đoàn:</span> {ctx.visitTitle || 'Chưa có'}</li>
              <li><span className="font-semibold text-slate-700">Mã request:</span> REQ #{ctx.visitRequestId || 'N/A'}</li>
              <li><span className="font-semibold text-slate-700">Host:</span> {ctx.hostName || 'Chưa có'}</li>
              <li>
                <span className="font-semibold text-slate-700">Thời gian diễn ra:</span>{' '}
                {ctx.expectedStartDate ? new Date(ctx.expectedStartDate).toLocaleString('vi-VN') : 'N/A'} -{' '}
                {ctx.expectedEndDate ? new Date(ctx.expectedEndDate).toLocaleString('vi-VN') : 'N/A'}
              </li>
              <li><span className="font-semibold text-slate-700">Trạng thái:</span> {ctx.requestStatus || 'N/A'}</li>
            </ul>
          </div>
        );
      case 'MINUTES':
        return (
          <div className="p-3 rounded-lg border border-slate-100 bg-orange-50/30">
            <p className="text-[11px] font-bold text-orange-700 uppercase tracking-wider mb-2">Thông tin biên bản</p>
            <ul className="text-sm text-slate-600 space-y-1">
              <li><span className="font-semibold text-slate-700">Tên biên bản:</span> {ctx.minuteTitle || 'Chưa có'}</li>
              <li><span className="font-semibold text-slate-700">Trạng thái:</span> {ctx.status || 'N/A'}</li>
              <li><span className="font-semibold text-slate-700">Thuộc đoàn:</span> {ctx.visitTitle || 'Chưa có'} (REQ #{ctx.visitRequestId || 'N/A'})</li>
            </ul>
          </div>
        );
      case 'PARTNER':
        return (
          <div className="p-3 rounded-lg border border-slate-100 bg-purple-50/30">
            <p className="text-[11px] font-bold text-purple-700 uppercase tracking-wider mb-2">Thông tin đối tác</p>
            <ul className="text-sm text-slate-600 space-y-1">
              <li><span className="font-semibold text-slate-700">Tên đối tác:</span> {ctx.partnerName || 'Chưa có'}</li>
              <li><span className="font-semibold text-slate-700">Quốc gia:</span> {ctx.country || 'N/A'}</li>
              <li><span className="font-semibold text-slate-700">Trạng thái:</span> {ctx.status || 'N/A'}</li>
              <li><span className="font-semibold text-slate-700">Website:</span> {ctx.website ? <a href={ctx.website} target="_blank" rel="noreferrer" className="text-blue-600">{ctx.website}</a> : 'N/A'}</li>
              {ctx.profileSummary && (
                <li className="mt-1 border-t pt-1 border-slate-200 text-xs italic">{ctx.profileSummary}</li>
              )}
            </ul>
          </div>
        );
      case 'NEWS':
        return (
          <div className="p-3 rounded-lg border border-slate-100 bg-emerald-50/30">
            <p className="text-[11px] font-bold text-emerald-700 uppercase tracking-wider mb-2">Thông tin tin tức</p>
            <ul className="text-sm text-slate-600 space-y-1">
              <li><span className="font-semibold text-slate-700">Tiêu đề:</span> {ctx.title || 'Chưa có'}</li>
              <li><span className="font-semibold text-slate-700">Trạng thái:</span> {ctx.status || 'N/A'}</li>
              <li>
                <span className="font-semibold text-slate-700">Ngày xuất bản:</span>{' '}
                {ctx.publishedAt ? new Date(ctx.publishedAt).toLocaleString('vi-VN') : 'Chưa xuất bản'}
              </li>
              {ctx.summary && (
                <li className="mt-1 border-t pt-1 border-slate-200 text-xs italic">{ctx.summary}</li>
              )}
            </ul>
          </div>
        );
      default:
        return (
          <div className="p-3 rounded-lg border border-slate-100 bg-slate-50">
            <p className="text-[11px] font-bold text-slate-500 uppercase tracking-wider mb-2">Tài liệu chung</p>
            <p className="text-sm text-slate-600 text-xs italic">{ctx.message || 'Không có ngữ cảnh bổ sung.'}</p>
          </div>
        );
    }
  };

  const getPreviewUrl = () => {
    if (!detail?.file?.webViewUrl) return null;
    const url = detail.file.webViewUrl;
    if (url.includes('drive.example') || url.includes('example.com') || !url.startsWith('http')) return null;
    return url.replace(/\/view.*/, '/preview');
  };

  const previewUrl = getPreviewUrl();
  const downloadLink = detail?.file?.downloadUrl || (detail?.file?.fileId ? resolveFileUrl(`/api/files/${detail.file.fileId}/content`) : '');

  return (
    <AnimatePresence>
      {documentId && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4 sm:p-6">
          <motion.div 
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="absolute inset-0 bg-slate-900/60 backdrop-blur-sm"
            onClick={onClose}
          />
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="relative w-full max-w-5xl bg-white rounded-2xl shadow-2xl overflow-hidden flex flex-col h-[85vh]"
          >
            {isLoading ? (
              <div className="flex-1 flex flex-col items-center justify-center">
                <Loader2 className="w-10 h-10 text-[#004c91] animate-spin mb-4" />
                <p className="text-slate-500 font-medium">Đang tải chi tiết tài liệu...</p>
              </div>
            ) : isError || !detail ? (
              <div className="flex-1 flex flex-col items-center justify-center">
                <FileText className="w-12 h-12 text-slate-300 mb-4" />
                <p className="text-red-500 font-medium">Không thể tải chi tiết tài liệu hoặc không có quyền truy cập.</p>
                <button onClick={onClose} className="mt-4 px-4 py-2 bg-slate-100 rounded-lg text-slate-700 font-medium hover:bg-slate-200">Đóng</button>
              </div>
            ) : (
              <>
                {/* Modal Header */}
                <div className="flex items-center justify-between p-4 border-b border-slate-100 bg-white z-10 shrink-0">
                  <div className="flex items-center gap-3 pr-4">
                    <div className="w-10 h-10 rounded-lg bg-[#004c91]/10 flex items-center justify-center shrink-0">
                      <FileText className="w-5 h-5 text-[#004c91]" />
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                         <h3 className="text-lg font-bold text-slate-800 line-clamp-1" title={detail.document.title}>
                           {detail.document.title}
                         </h3>
                         <span className={`inline-flex px-2 py-0.5 text-[10px] font-bold rounded-full border ${
                          detail.document.status === 'PUBLISHED' ? 'bg-emerald-50 text-emerald-700 border-emerald-200' :
                          detail.document.status === 'DRAFT' ? 'bg-amber-50 text-amber-700 border-amber-200' :
                          'bg-slate-100 text-slate-700 border-slate-200'
                        }`}>
                          {detail.document.status}
                        </span>
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5 line-clamp-1">{detail.file.originalFilename}</p>
                    </div>
                  </div>
                  <button 
                    onClick={onClose}
                    className="p-2 text-slate-400 hover:text-slate-700 hover:bg-slate-100 rounded-full transition-colors shrink-0 outline-none cursor-pointer"
                  >
                    <X className="w-5 h-5" />
                  </button>
                </div>
                
                {/* Modal Body: 2 Columns */}
                <div className="flex flex-col md:flex-row flex-1 overflow-hidden bg-slate-50">
                  {/* Left: Metadata */}
                  <div className="w-full md:w-[40%] bg-white border-r border-slate-100 flex flex-col relative h-full">
                    
                    {/* Scrollable Content */}
                    <div className="flex-1 overflow-y-auto custom-scrollbar p-5 flex flex-col gap-4">
                      {/* Business Context */}
                      {renderOwnerContext()}

                      <div className="p-3 rounded-lg border border-slate-100 bg-slate-50/50">
                        <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mb-2">Nội dung tài liệu</p>
                        <ul className="text-sm text-slate-600 space-y-1">
                           <li><span className="font-semibold text-slate-700">Danh mục:</span> {detail.document.documentCategory || 'N/A'}</li>
                           <li><span className="font-semibold text-slate-700">Mô tả:</span> {detail.document.description || 'Không có mô tả'}</li>
                           <li><span className="font-semibold text-slate-700">File gốc:</span> {detail.file.originalFilename}</li>
                           <li><span className="font-semibold text-slate-700">Tạo bởi:</span> {detail.createdByUser?.fullName || 'N/A'}</li>
                        </ul>
                      </div>
                    </div>

                    {/* Fixed Bottom Share & Download Actions */}
                    <div className="p-5 pt-4 border-t border-slate-100 bg-white shrink-0 space-y-3 z-10 shadow-[0_-4px_6px_-1px_rgba(0,0,0,0.05)]">
                      <p className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Chia sẻ & Tải xuống</p>
                      <div className="grid grid-cols-2 gap-2">
                        <button 
                          onClick={() => handleShareZalo(previewUrl || downloadLink)}
                          className="flex items-center justify-center gap-2 px-3 py-2 bg-blue-50 hover:bg-blue-100 text-blue-700 rounded-lg transition-colors text-sm font-medium border border-blue-100 cursor-pointer"
                        >
                          <Share2 className="w-4 h-4" /> Zalo
                        </button>
                        <button 
                          onClick={() => handleShareGmail(detail.document.title, previewUrl || downloadLink)}
                          className="flex items-center justify-center gap-2 px-3 py-2 bg-red-50 hover:bg-red-100 text-red-700 rounded-lg transition-colors text-sm font-medium border border-red-100 cursor-pointer"
                        >
                          <Mail className="w-4 h-4" /> Gmail
                        </button>
                      </div>
                      <div className="grid grid-cols-1 gap-2 pt-1">
                        {detail.file.fileId ? (
                          <button 
                            onClick={() => handleDownload(detail.file.fileId, detail.file.originalFilename)}
                            className="flex items-center justify-center gap-2 px-4 py-2.5 bg-[#004c91] hover:bg-[#00386b] text-white rounded-lg transition-colors text-sm font-medium shadow-md shadow-[#004c91]/20 cursor-pointer"
                          >
                            <Download className="w-4 h-4" /> Tải xuống máy
                          </button>
                        ) : downloadLink ? (
                          <a 
                            href={downloadLink}
                            target="_blank" 
                            rel="noopener noreferrer"
                            className="flex items-center justify-center gap-2 px-4 py-2.5 bg-[#004c91] hover:bg-[#00386b] text-white rounded-lg transition-colors text-sm font-medium shadow-md shadow-[#004c91]/20"
                          >
                            <Download className="w-4 h-4" /> Tải xuống máy (External)
                          </a>
                        ) : (
                          <button disabled className="flex items-center justify-center gap-2 px-4 py-2.5 bg-slate-100 text-slate-400 rounded-lg text-sm font-medium cursor-not-allowed">
                            <Download className="w-4 h-4" /> Không thể tải xuống
                          </button>
                        )}
                        
                        {previewUrl ? (
                          <a 
                            href={detail.file.webViewUrl}
                            target="_blank" 
                            rel="noopener noreferrer"
                            className="flex items-center justify-center gap-2 px-4 py-2 bg-white hover:bg-slate-50 text-slate-700 border border-slate-200 rounded-lg transition-colors text-sm font-medium"
                          >
                            <ExternalLink className="w-4 h-4" /> Mở trong Google Drive
                          </a>
                        ) : (
                           <button disabled title="Chưa có liên kết Google Drive hợp lệ cho file này" className="flex items-center justify-center gap-2 px-4 py-2 bg-slate-50 text-slate-400 border border-slate-200 rounded-lg text-sm font-medium cursor-not-allowed">
                             <ExternalLink className="w-4 h-4" /> Mở trong Google Drive
                           </button>
                        )}
                      </div>
                    </div>
                  </div>
                  
                  {/* Right: Iframe Preview */}
                  <div className="w-full md:w-[60%] relative bg-[#f1f3f4] min-h-[400px] flex items-center justify-center">
                    {previewUrl ? (
                      <iframe 
                        src={previewUrl} 
                        className="absolute inset-0 w-full h-full border-0"
                        allow="autoplay"
                        title={detail.document.title}
                      />
                    ) : (
                      <div className="flex flex-col items-center justify-center text-slate-400 p-6 text-center">
                        <FileText className="w-12 h-12 mb-3 text-slate-300" />
                        <p className="font-medium text-slate-600">Không thể xem trước tệp tin này</p>
                        <p className="text-sm mt-1">Tệp không hỗ trợ preview hoặc liên kết Google Drive không hợp lệ. Vui lòng nhấn nút tải xuống bên trái.</p>
                      </div>
                    )}
                  </div>
                </div>
              </>
            )}
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}
