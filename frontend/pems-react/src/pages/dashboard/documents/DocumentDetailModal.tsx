import React from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, FileText, Download, ExternalLink, Share2, Mail, Loader2 } from 'lucide-react';
import { useDocumentDetail } from '../../../features/documents/hooks/useDocumentDetail';
import { formatFileSize } from '../../../shared/utils/fileUtils';
import { resolveFileUrl } from '../../../shared/utils/resolveFileUrl';
import toast from 'react-hot-toast';
import httpClient from '../../../shared/api/httpClient';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { formatDocumentType, formatDocumentStatus, formatVisitStatus, formatMinutesStatus, formatNewsStatus } from '../../../shared/utils/domainLabels';
import { PROFILE_STATUS_LABELS } from '../../../features/partners/types/partners.types';

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

    const Row = ({ label, value }: { label: string; value: React.ReactNode }) => (
      <div className="flex gap-1.5 text-sm">
        <dt className="text-slate-500 shrink-0">{label}:</dt>
        <dd className="font-semibold text-slate-800 min-w-0 break-words">{value}</dd>
      </div>
    );

    switch (ownerType) {
      case 'VISIT':
        return (
          <div>
            <p className="text-xs font-bold text-[#004c91] uppercase tracking-wide mb-1.5">Thông tin đoàn khách</p>
            <dl className="space-y-1">
              <Row label="Tên đoàn" value={ctx.visitTitle || 'Chưa có'} />
              <Row label="Mã request" value={`REQ #${ctx.visitRequestId || 'N/A'}`} />
              <Row label="Host" value={ctx.hostName || 'Chưa có'} />
              <Row label="Thời gian diễn ra" value={
                <>{ctx.expectedStartDate ? formatVietnamDateTime(ctx.expectedStartDate) : 'N/A'} -{' '}
                {ctx.expectedEndDate ? formatVietnamDateTime(ctx.expectedEndDate) : 'N/A'}</>
              } />
              <Row label="Trạng thái" value={ctx.requestStatus ? formatVisitStatus(ctx.requestStatus) : 'N/A'} />
            </dl>
          </div>
        );
      case 'MINUTES':
        return (
          <div>
            <p className="text-xs font-bold text-[#004c91] uppercase tracking-wide mb-1.5">Thông tin biên bản</p>
            <dl className="space-y-1">
              <Row label="Tên biên bản" value={ctx.minuteTitle || 'Chưa có'} />
              <Row label="Trạng thái" value={ctx.status ? formatMinutesStatus(ctx.status) : 'N/A'} />
              <Row label="Thuộc đoàn" value={`${ctx.visitTitle || 'Chưa có'} (REQ #${ctx.visitRequestId || 'N/A'})`} />
            </dl>
          </div>
        );
      case 'PARTNER':
        return (
          <div>
            <p className="text-xs font-bold text-[#004c91] uppercase tracking-wide mb-1.5">Thông tin đối tác</p>
            <dl className="space-y-1">
              <Row label="Tên đối tác" value={ctx.partnerName || 'Chưa có'} />
              <Row label="Quốc gia" value={ctx.country || 'N/A'} />
              <Row label="Trạng thái" value={ctx.status ? (PROFILE_STATUS_LABELS[ctx.status as keyof typeof PROFILE_STATUS_LABELS] ?? ctx.status) : 'N/A'} />
              <Row label="Website" value={ctx.website ? <a href={ctx.website} target="_blank" rel="noreferrer" className="text-blue-600">{ctx.website}</a> : 'N/A'} />
              {ctx.profileSummary && <Row label="Ghi chú" value={<span className="italic text-xs">{ctx.profileSummary}</span>} />}
            </dl>
          </div>
        );
      case 'NEWS':
        return (
          <div>
            <p className="text-xs font-bold text-[#004c91] uppercase tracking-wide mb-1.5">Thông tin tin tức</p>
            <dl className="space-y-1">
              <Row label="Tiêu đề" value={ctx.title || 'Chưa có'} />
              <Row label="Trạng thái" value={ctx.status ? formatNewsStatus(ctx.status) : 'N/A'} />
              <Row label="Ngày xuất bản" value={ctx.publishedAt ? formatVietnamDateTime(ctx.publishedAt) : 'Chưa xuất bản'} />
              {ctx.summary && <Row label="Tóm tắt" value={<span className="italic text-xs">{ctx.summary}</span>} />}
            </dl>
          </div>
        );
      default:
        return (
          <div>
            <p className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-1.5">Tài liệu chung</p>
            <p className="text-sm text-slate-500 italic">{ctx.message || 'Không có ngữ cảnh bổ sung.'}</p>
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
                          {formatDocumentStatus(detail.document.status)}
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
                      {/* Thông tin tài liệu — key-value compact */}
                      <div>
                        <p className="text-xs font-bold text-slate-500 uppercase tracking-wide mb-1.5">Thông tin tài liệu</p>
                        <dl className="space-y-1 text-sm">
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Tên tài liệu:</dt><dd className="font-semibold text-slate-800 min-w-0 break-words">{detail.document.title}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Loại:</dt><dd className="font-semibold text-slate-800">{formatDocumentType(detail.document.ownerType)}{detail.document.documentCategory ? ` · ${detail.document.documentCategory}` : ''}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Trạng thái:</dt><dd className="font-semibold text-slate-800">{formatDocumentStatus(detail.document.status)}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">File:</dt><dd className="font-semibold text-slate-800 min-w-0 break-words">{detail.file.originalFilename}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Dung lượng:</dt><dd className="font-semibold text-slate-800">{detail.file.fileSize ? formatFileSize(detail.file.fileSize) : 'N/A'}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Google Drive:</dt><dd className="font-semibold text-slate-800">{previewUrl ? 'Có liên kết hợp lệ' : 'Chưa có liên kết hợp lệ'}</dd></div>
                          {detail.document.description && (
                            <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Mô tả:</dt><dd className="text-slate-700 min-w-0 break-words">{detail.document.description}</dd></div>
                          )}
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Tạo bởi:</dt><dd className="font-semibold text-slate-800">{detail.createdByUser?.fullName || 'N/A'}</dd></div>
                          <div className="flex gap-1.5"><dt className="text-slate-500 shrink-0">Cập nhật:</dt><dd className="font-semibold text-slate-800">{detail.document.updatedAt ? formatVietnamDateTime(detail.document.updatedAt) : 'Chưa cập nhật'}</dd></div>
                        </dl>
                      </div>

                      <hr className="border-slate-100" />

                      {/* Business Context (Thuộc đoàn / biên bản / đối tác / tin tức tương ứng) */}
                      {renderOwnerContext()}
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
                        <p className="text-sm text-slate-600 max-w-sm">Không thể xem trước. File chưa có liên kết Google Drive hợp lệ hoặc định dạng không hỗ trợ preview.</p>
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
