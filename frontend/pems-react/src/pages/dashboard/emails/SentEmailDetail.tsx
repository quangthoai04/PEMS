/**
 * Trang SentEmailDetail
 * Trạng thái nội dung cấu hình xem lại hộp thư đã đánh dấu trạng thái Gửi ra bên ngoài.
 */

import React, { useState } from 'react';
import { motion } from 'motion/react';
import { ChevronLeft, Trash2, Reply, Send, FileText, Clock, AlertTriangle, Paperclip, Bold, Italic, Underline, Link, ImageIcon, List, ListOrdered, Check } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';

import { emailsApi } from '../../../features/emails/api/emailsApi';
import { format } from 'date-fns';
import { toast } from 'react-hot-toast';
import { sanitizeHtml, sanitizeSentEmailPreviewHtml } from '../../../shared/security/sanitizeHtml';

export function SentEmailDetail() {
  const navigate = useNavigate();
  const { sourceType, id } = useParams();
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [replyContent, setReplyContent] = useState('');
  const [activeReplyId, setActiveReplyId] = useState<number | null>(null);
  const [emailData, setEmailData] = useState<any>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);

  React.useEffect(() => {
    fetchEmailDetail();
  }, [id, sourceType]);

  const fetchEmailDetail = async () => {
    if (!id || !sourceType) return;
    setIsLoading(true);
    try {
      const res = await emailsApi.getEmailDetail(Number(id), sourceType);
      setEmailData(res.data);
    } catch (error: any) {
      toast.error('Lỗi khi lấy chi tiết email');
      console.error(error);
    } finally {
      setIsLoading(false);
    }
  };

  const handleReply = async () => {
    if (!replyContent.trim() || !emailData) return;
    setIsSubmitting(true);
    try {
      await emailsApi.replyEmail({
        originalEmailId: emailData.id,
        body: replyContent
      });
      toast.success('Phản hồi thành công!');
      setReplyContent('');
      setActiveReplyId(null);
      fetchEmailDetail();
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Phản hồi thất bại');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleComplete = async () => {
    if (!emailData) return;
    setIsSubmitting(true);
    try {
      const res = await emailsApi.markCompleted(emailData.id);
      if (res.data && res.data.success === false) {
          toast.error(res.data.message || 'Thao tác thất bại');
      } else {
          toast.success('Đã chuyển sang trạng thái Hoàn thành');
          fetchEmailDetail();
      }
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Thao tác thất bại');
    } finally {
      setIsSubmitting(false);
    }
  };



  const handleDelete = () => {
    // Navigation handling here
    navigate(-1);
  };

  if (isLoading) {
    return <div className="p-8 text-center text-gray-500 font-medium">Đang tải chi tiết email...</div>;
  }

  if (!emailData) {
    return <div className="p-8 text-center text-red-500 font-medium">Không tìm thấy email</div>;
  }

  return (
    <motion.div 
      initial={{ opacity: 0, x: 20 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: -20 }}
      transition={{ duration: 0.3 }}
      className="p-4 sm:p-6 md:p-8 max-w-[1100px] mx-auto min-h-screen pb-20"
    >
      {/* Breadcrumbs */}
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate(-1)} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Quản lý email</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Chi tiết email đã gửi</span>
      </div>

      {/* Header Actions */}
      <div className="flex items-center justify-between mb-6 pb-4 border-b border-gray-200">
        <div className="flex items-center gap-4">
          <button 
            onClick={() => navigate(-1)}
            className="flex items-center gap-2 px-3 py-2 text-gray-500 hover:bg-gray-100 hover:text-[#004c91] rounded-lg transition-colors font-medium outline-none cursor-pointer"
          >
            <ChevronLeft className="w-5 h-5" />
            Quay lại
          </button>
          <div className="h-6 w-px bg-gray-300"></div>
          <span className="text-[#004c91] font-bold text-2xl">Chi tiết email đã gửi</span>
        </div>
        <button 
          onClick={() => setShowDeleteModal(true)}
          className="flex items-center gap-2 px-4 py-2 text-red-600 bg-red-50 hover:bg-red-100 rounded-lg transition-colors font-semibold outline-none border border-red-100 shadow-sm"
        >
          <Trash2 className="w-4 h-4" />
          Xóa email
        </button>
      </div>

      {/* Main Original Email Block */}
      <div className="bg-white rounded-2xl shadow-sm border border-gray-200 overflow-hidden mb-8">
        <div className="bg-[#004c91] px-8 py-5 border-b border-gray-200">
          <h1 className="text-[26px] font-bold text-white mb-3 leading-tight tracking-tight">
            {emailData.subject}
          </h1>
          <div className="inline-flex items-center gap-2 text-xs text-white font-bold uppercase tracking-wider bg-white/20 border border-white/30 px-3 py-1.5 rounded-full">
            Trạng thái gửi: {emailData.status}
          </div>
          {emailData.processStatus && (
            <div className={`ml-3 inline-flex items-center gap-2 text-xs font-bold uppercase tracking-wider border px-3 py-1.5 rounded-full ${
              emailData.processStatus === 'COMPLETED' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 
              emailData.processStatus === 'FAILED' ? 'bg-[#ffe4e4] text-[#a10a0a] border-[#efdada]' :
              'bg-[#fff6e0] text-[#cf8e00] border-[#ffecba]'
            }`}>
              {emailData.processStatus === 'COMPLETED' ? 'Hoàn thành' : 
               emailData.processStatus === 'FAILED' ? 'Thất bại' : 'Đang xử lý'}
            </div>
          )}
        </div>

        <div className="p-4 sm:p-6 md:p-8">
          <div className="flex items-start justify-between mb-8">
            <div className="flex items-start gap-4">
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-blue-100 to-blue-200 shadow-sm flex items-center justify-center text-[#004c91] font-bold text-xl uppercase shrink-0">
                {emailData.sender?.fullName ? emailData.sender.fullName.charAt(0).toUpperCase() : '?'}
              </div>
              <div className="mt-0.5">
                <div className="font-bold text-gray-900 text-[15px]">{emailData.sender?.fullName || 'Hệ thống'} <span className="text-gray-500 font-normal">&lt;{emailData.sender?.email || 'N/A'}&gt;</span></div>
                <div className="text-sm text-gray-500 mt-1">Đến: <span className="text-gray-700 font-medium">{emailData.recipients?.filter((r: any) => r.recipientType === 'TO').map((r: any) => r.recipientEmail).join(', ') || 'N/A'}</span></div>
                {emailData.recipients?.some((r: any) => r.recipientType === 'CC') && (
                  <div className="text-sm text-gray-500">CC: <span className="text-gray-700 font-medium">{emailData.recipients.filter((r: any) => r.recipientType === 'CC').map((r: any) => r.recipientEmail).join(', ')}</span></div>
                )}
                {emailData.recipients?.some((r: any) => r.recipientType === 'BCC') && (
                  <div className="text-sm text-gray-500">BCC: <span className="text-gray-700 font-medium">{emailData.recipients.filter((r: any) => r.recipientType === 'BCC').map((r: any) => r.recipientEmail).join(', ')}</span></div>
                )}
              </div>
            </div>
            <div className="flex items-center gap-2 text-orange-800 font-bold text-sm bg-[#ffe4c4] px-4 py-2 rounded-xl border border-[#ffd2a0] shadow-[0_2px_10px_-4px_rgba(255,165,0,0.3)]">
              <Clock className="w-4 h-4 text-orange-600" />
              {emailData.sentAt ? format(new Date(emailData.sentAt.endsWith('Z') ? emailData.sentAt : emailData.sentAt + 'Z'), 'dd/MM/yyyy HH:mm') : 'Chưa gửi'}
            </div>
          </div>

          <div className="relative mb-6">
            <div className="mb-2 rounded bg-blue-50 px-3 py-2 text-sm font-medium text-[#004c91] border border-blue-100 flex items-center gap-2">
              <AlertCircle className="w-4 h-4 shrink-0" /> Đây là bản xem lại email đã gửi. Các nút thao tác đã bị vô hiệu hóa trong chế độ xem trước.
            </div>
            <div 
              className="text-gray-800 text-[15px] p-6 rounded-xl border border-gray-100 bg-[#fbfcfd] whitespace-pre-wrap select-text pointer-events-none"
              dangerouslySetInnerHTML={{ __html: sanitizeSentEmailPreviewHtml(sanitizeHtml(emailData.bodySnapshot || '')) }}
            >
            </div>
          </div>

          {emailData.errorMessage && (
            <div className="mb-6 p-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm font-medium">
              <span className="font-bold block mb-1">Lỗi hệ thống:</span>
              {emailData.errorMessage}
            </div>
          )}

          {emailData.recipients && emailData.recipients.length > 0 && (
            <div className="mb-6">
              <h4 className="text-sm font-bold text-gray-700 mb-3 flex items-center gap-2"><List className="w-4 h-4" /> Chi tiết gửi tới người nhận:</h4>
              <div className="flex flex-col gap-2">
                {emailData.recipients.map((r: any, idx: number) => (
                  <div key={idx} className="flex items-center justify-between bg-gray-50 rounded-lg p-3 text-sm border border-gray-100">
                    <div>
                      <span className="font-semibold text-gray-800">{r.recipientEmail}</span>
                      <span className="ml-2 text-xs text-gray-500">({r.recipientType})</span>
                    </div>
                    <div className="flex items-center gap-4">
                      {r.errorMessage && <span className="text-xs text-red-500 max-w-xs truncate" title={r.errorMessage}>{r.errorMessage}</span>}
                      <span className={`px-2 py-1 text-xs font-bold rounded-full ${r.deliveryStatus === 'SENT' ? 'bg-green-100 text-green-700' : r.deliveryStatus === 'FAILED' || r.deliveryStatus === 'BOUNCED' ? 'bg-red-100 text-red-700' : 'bg-yellow-100 text-yellow-700'}`}>
                        {r.deliveryStatus}
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {emailData.attachments && emailData.attachments.length > 0 && (
            <div className="mt-8 border-t border-dashed border-gray-200 pt-6">
              <div className="text-xs font-bold text-gray-500 mb-4 flex items-center gap-2 uppercase tracking-wider">
                <Paperclip className="w-4 h-4" /> Tệp đính kèm ({emailData.attachments.length})
              </div>
              <div className="flex flex-wrap gap-4">
                {emailData.attachments.map((file: any, i: number) => (
                  <a key={i} href={file.downloadUrl || `/api/files/${file.fileId}/download`} target="_blank" rel="noreferrer" className="flex items-center gap-3.5 p-3 border border-gray-200 rounded-xl bg-white hover:bg-blue-50 hover:border-blue-200 hover:shadow-sm transition-all group cursor-pointer w-[300px]">
                    <div className="w-10 h-10 rounded-lg bg-[#e6eff7] flex items-center justify-center text-[#004c91] group-hover:bg-[#004c91] group-hover:text-white transition-colors">
                      <FileText className="w-5 h-5" />
                    </div>
                    <div className="flex-1 overflow-hidden">
                      <div className="text-sm font-semibold text-gray-800 truncate group-hover:text-[#004c91] transition-colors" title={file.fileName}>{file.fileName}</div>
                      <div className="text-xs text-gray-500 mt-0.5">{file.sizeBytes != null ? (file.sizeBytes / 1024).toFixed(1) + ' KB' : 'N/A'}</div>
                    </div>
                  </a>
                ))}
              </div>
            </div>
          )}
          
          <div className="mt-6 flex justify-end gap-3">
            {emailData.canReply && (
              <button 
                onClick={() => setActiveReplyId(emailData.id)}
                className="px-6 py-2.5 rounded-xl border border-[#004c91] text-[#004c91] font-bold hover:bg-blue-50 transition-colors shadow-sm flex items-center gap-2 outline-none"
              >
                <Reply className="w-4 h-4" />
                Phản hồi
              </button>
            )}
            {emailData.canMarkComplete && (
              <button 
                onClick={handleComplete}
                disabled={isSubmitting}
                className="px-6 py-2.5 rounded-xl bg-green-600 hover:bg-green-700 text-white font-bold transition-colors shadow-sm flex items-center gap-2 outline-none disabled:opacity-50"
              >
                <Check className="w-4 h-4" />
                Đánh dấu Hoàn thành
              </button>
            )}
          </div>
          
          {/* Quick Reply Form for Original Email */}
          {activeReplyId === emailData.id && (
            <div className="mt-6 animate-in slide-in-from-top-2 fade-in duration-200">
              <div className="border border-[#cde0f5] rounded-xl overflow-hidden bg-white shadow-sm focus-within:border-[#004c91] focus-within:ring-1 focus-within:ring-[#004c91] transition-all flex flex-col">
                <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                  <div className="flex items-center gap-2 text-[14px] mt-1.5">
                    <span className="text-gray-500 w-10">Tới:</span>
                    <span className="font-semibold text-gray-800">{emailData.senderName}</span>
                    <span className="text-gray-500">&lt;{emailData.senderEmail}&gt;</span>
                  </div>
                </div>
                <div className="bg-white">
                  <ReactQuill 
                    theme="snow" 
                    value={replyContent} 
                    onChange={setReplyContent}
                    placeholder="Nhập nội dung phản hồi..."
                    className="custom-quill-no-border min-h-[150px]"
                    modules={{
                      toolbar: [
                        ['bold', 'italic', 'underline', 'strike'],
                        [{'list': 'ordered'}, {'list': 'bullet'}],
                        ['link', 'image'],
                        ['clean']
                      ]
                    }}
                  />
                </div>
                <div className="bg-white px-4 py-3 flex justify-end gap-3 rounded-b-xl border-t border-gray-100">
                  <button 
                    onClick={() => setActiveReplyId(null)}
                    className="px-5 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:border-[#004c91] hover:text-[#004c91] hover:bg-blue-50 font-medium transition-all outline-none cursor-pointer"
                  >
                    Hủy
                  </button>
                  <button 
                    onClick={handleReply}
                    className="bg-[#004c91] hover:bg-[#003a70] text-white px-6 py-2 rounded-lg text-sm font-bold flex items-center gap-2 transition-all shadow-sm outline-none cursor-pointer disabled:opacity-50"
                    disabled={!replyContent.trim() || isSubmitting}
                  >
                    <Send className="w-4 h-4" /> Gửi
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Delete Confirmation Modal */}
      {showDeleteModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm animate-in fade-in duration-200">
          <div className="bg-white rounded-2xl w-full max-w-md shadow-2xl overflow-hidden scale-in-95 duration-200">
            <div className="p-6">
              <div className="w-14 h-14 rounded-full bg-red-100 flex items-center justify-center mb-5 mx-auto">
                <AlertTriangle className="w-7 h-7 text-red-600" />
              </div>
              <h3 className="text-xl font-bold text-center text-gray-900 mb-3">Xóa email này?</h3>
              <p className="text-center text-gray-500 mb-8 leading-relaxed">
                Hành động này sẽ xóa email gốc và <strong>ẩn toàn bộ chuỗi phản hồi</strong> liên quan. Không thể hoàn tác. Bạn đã chắc chắn?
              </p>
              <div className="flex items-center gap-3">
                <button 
                  onClick={() => setShowDeleteModal(false)}
                  className="flex-1 px-4 py-2.5 rounded-xl border border-gray-300 text-gray-700 font-bold hover:bg-gray-50 transition-colors outline-none"
                >
                  Hủy bỏ
                </button>
                <button 
                  onClick={handleDelete}
                  className="flex-1 px-4 py-2.5 rounded-xl bg-red-600 hover:bg-red-700 text-white font-bold transition-colors outline-none shadow-sm flex justify-center items-center gap-2"
                >
                  <Trash2 className="w-4 h-4" />
                  Xác nhận Xóa
                </button>
              </div>
            </div>
          </div>
        </div>
      )}
    </motion.div>
  );
}
