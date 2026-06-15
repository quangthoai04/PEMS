/**
 * Trang SentEmailDetail
 * Trạng thái nội dung cấu hình xem lại hộp thư đã đánh dấu trạng thái Gửi ra bên ngoài.
 */

import React, { useState } from 'react';
import { motion } from 'motion/react';
import { ChevronLeft, Trash2, Reply, Send, FileText, Clock, AlertTriangle, Paperclip, Bold, Italic, Underline, Link, ImageIcon, List, ListOrdered } from 'lucide-react';
import { useNavigate, useParams } from 'react-router-dom';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';

const mockOriginalEmail = {
  id: 1,
  program: 'Đón tiếp ĐH Deakin (Úc)',
  subject: 'Thư mời tham quan và làm việc tại ĐH FPT',
  sender: 'Nguyễn Văn B (Phòng HTQT - HO)',
  receivers: 'Đại diện ĐH Deakin (international@deakin.edu.au)',
  sendTime: '01/05/2024 08:00',
  content: `Kính gửi Đại diện Tuyển sinh và Hợp tác Quốc tế - Đại học Deakin,\n\nThay mặt Ban Giám hiệu Đại học FPT, chúng tôi trân trọng kính mời quý đoàn đại biểu đến thăm và làm việc tại cơ sở Hòa Lạc (Hà Nội, Việt Nam).\n\nMục đích chuyến thăm lần này nhằm thảo luận mở rộng về:\n1. Chương trình trao đổi sinh viên (Student Exchange Program) cho học kỳ Spring 2025.\n2. Phát triển chương trình liên kết đào tạo 2+2 ngành Công nghệ Thông tin.\n3. Khả năng đồng tổ chức hội thảo khoa học quốc tế vào cuối năm nay.\n\nĐính kèm email này là dự thảo lịch trình làm việc và tài liệu giới thiệu về Đại học FPT. Kính mong quý vị xem xét và phản hồi để chúng tôi chuẩn bị công tác đón tiếp chu đáo nhất.\n\nTrân trọng,\nNguyễn Văn B.`,
  attachments: [
    { name: 'FPTU_Campus_Tour_Agenda.pdf', size: '1.2 MB' },
    { name: 'FPTU_International_Brochure_2024.pdf', size: '5.4 MB' }
  ]
};

const mockThread = [
  {
    id: 101,
    role: 'receiver',
    level: 0,
    subject: 'Re: Thư mời tham quan và làm việc tại ĐH FPT',
    senderName: 'Sarah Jenkins (Deakin Global)',
    email: 'sarah.jenkins@deakin.edu.au',
    receiverName: 'Nguyễn Văn B (Phòng HTQT - HO)',
    receiverEmail: 'bnv@fe.edu.vn',
    time: '01/05/2024 lúc 14:15',
    content: `Kính gửi anh Nguyễn Văn B,\n\nCảm ơn lời mời trân trọng từ Đại học FPT. Chúng tôi rất vinh dự và sẵn lòng sắp xếp chuyến thăm đến cơ sở Hòa Lạc.\n\nĐoàn chúng tôi dự kiến có 4 thành viên, bao gồm Giám đốc Khu vực Châu Á và Trưởng khoa CNTT. Về lịch trình, chúng tôi muốn dời phần thảo luận ký kết 2+2 sang buổi chiều để có thêm thời gian tham quan campus vào buổi sáng. Anh xem có khả thi không?\n\nTrân trọng,\nSarah Jenkins.`
  },
  {
    id: 102,
    role: 'manager',
    level: 1,
    subject: 'Re: Thư mời tham quan và làm việc tại ĐH FPT',
    senderName: 'Nguyễn Văn B (Phòng HTQT - HO)',
    email: 'bnv@fe.edu.vn',
    receiverName: 'Sarah Jenkins (Deakin Global)',
    receiverEmail: 'sarah.jenkins@deakin.edu.au',
    time: '01/05/2024 lúc 15:30',
    content: `Chào chị Sarah,\n\nTuyệt vời, chúng tôi rất hoan nghênh sự hiện diện của đoàn đại biểu ĐH Deakin. Về yêu cầu thay đổi lịch trình, chúng tôi hoàn toàn đồng ý và sẽ điều chỉnh lại Agenda để gửi cho chị trong ngày mai.\n\nNgoài ra, đối với thủ tục xuất nhập cảnh và di chuyển từ sân bay Nội Bài, quý vị có cần nhà trường hỗ trợ thêm không?\n\nTrân trọng,\nNguyễn Văn B.`
  },
  {
    id: 103,
    role: 'receiver',
    level: 2,
    subject: 'Re: Thư mời tham quan và làm việc tại ĐH FPT',
    senderName: 'Sarah Jenkins (Deakin Global)',
    email: 'sarah.jenkins@deakin.edu.au',
    receiverName: 'Nguyễn Văn B (Phòng HTQT - HO)',
    receiverEmail: 'bnv@fe.edu.vn',
    time: '01/05/2024 lúc 16:45',
    content: `Cảm ơn anh B đã hỗ trợ nhiệt tình.\n\nVề vấn đề di chuyển, chúng tôi đã đặt trước xe đưa đón của khách sạn nên sẽ tự di chuyển đến campus. Chúng tôi rất mong chờ chuyến thăm sắp tới.\n\nHẹn gặp anh tại Hà Nội!`
  }
];

export function SentEmailDetail() {
  const navigate = useNavigate();
  const { id } = useParams();
  const [showDeleteModal, setShowDeleteModal] = useState(false);
  const [replyContent, setReplyContent] = useState('');
  const [activeReplyId, setActiveReplyId] = useState<number | null>(null);
  const [expandedReplies, setExpandedReplies] = useState<number[]>([]);

  const toggleExpand = (replyId: number) => {
    setExpandedReplies(prev => 
      prev.includes(replyId) 
        ? prev.filter(id => id !== replyId) 
        : [...prev, replyId]
    );
  };

  const handleDelete = () => {
    // Navigate back to the sent list
    navigate('/dashboard/email?tab=sent');
  };

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
        <button onClick={() => navigate('/dashboard/email?tab=sent')} className="hover:text-[#004c91] transition-colors outline-none cursor-pointer">Quản lý email</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">Chi tiết email đã gửi</span>
      </div>

      {/* Header Actions */}
      <div className="flex items-center justify-between mb-6 pb-4 border-b border-gray-200">
        <div className="flex items-center gap-4">
          <button 
            onClick={() => navigate('/dashboard/email?tab=sent')}
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
            {mockOriginalEmail.subject}
          </h1>
          <div className="inline-flex items-center gap-2 text-xs text-white font-bold uppercase tracking-wider bg-white/20 border border-white/30 px-3 py-1.5 rounded-full">
            {mockOriginalEmail.program}
          </div>
        </div>

        <div className="p-4 sm:p-6 md:p-8">
          <div className="flex items-start justify-between mb-8">
            <div className="flex items-start gap-4">
              <div className="w-12 h-12 rounded-full bg-gradient-to-br from-blue-100 to-blue-200 shadow-sm flex items-center justify-center text-[#004c91] font-bold text-xl uppercase shrink-0">
                {mockOriginalEmail.sender.charAt(0)}
              </div>
              <div className="mt-0.5">
                <div className="font-bold text-gray-900 text-[15px]">{mockOriginalEmail.sender}</div>
                <div className="text-sm text-gray-500 mt-1">Đến: <span className="text-gray-700 font-medium">{mockOriginalEmail.receivers}</span></div>
              </div>
            </div>
            <div className="flex items-center gap-2 text-orange-800 font-bold text-sm bg-[#ffe4c4] px-4 py-2 rounded-xl border border-[#ffd2a0] shadow-[0_2px_10px_-4px_rgba(255,165,0,0.3)]">
              <Clock className="w-4 h-4 text-orange-600" />
              {mockOriginalEmail.sendTime}
            </div>
          </div>

          <div className="text-gray-800 text-[15px] p-6 rounded-xl border border-gray-100 bg-[#fbfcfd] mb-6">
            {mockOriginalEmail.content.split('\n').map((line, i) => (
              <p key={i} className="my-1.5 min-h-[1.5rem] leading-relaxed">{line}</p>
            ))}
          </div>

          {mockOriginalEmail.attachments.length > 0 && (
            <div className="mt-8 border-t border-dashed border-gray-200 pt-6">
              <div className="text-xs font-bold text-gray-500 mb-4 flex items-center gap-2 uppercase tracking-wider">
                <Paperclip className="w-4 h-4" /> Tệp đính kèm ({mockOriginalEmail.attachments.length})
              </div>
              <div className="flex flex-wrap gap-4">
                {mockOriginalEmail.attachments.map((file, i) => (
                  <div key={i} className="flex items-center gap-3.5 p-3 border border-gray-200 rounded-xl bg-white hover:bg-blue-50 hover:border-blue-200 hover:shadow-sm transition-all group cursor-pointer w-[300px]">
                    <div className="w-10 h-10 rounded-lg bg-[#e6eff7] flex items-center justify-center text-[#004c91] group-hover:bg-[#004c91] group-hover:text-white transition-colors">
                      <FileText className="w-5 h-5" />
                    </div>
                    <div className="flex-1 overflow-hidden">
                      <div className="text-sm font-semibold text-gray-800 truncate group-hover:text-[#004c91] transition-colors">{file.name}</div>
                      <div className="text-xs text-gray-500 mt-0.5">{file.size}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Thread / Replies Timeline */}
      <div className="mt-12">
        <div className="inline-flex items-center bg-[#004c91] text-white pl-5 pr-8 py-2.5 rounded-xl mb-6 shadow-sm">
          <h2 className="text-lg font-bold flex items-center gap-2.5">
            <Reply className="w-5 h-5 text-white" /> 
            <span>Email phản hồi</span>
          </h2>
        </div>
        
        <div className="space-y-4 px-2">
          {mockThread.map((reply) => {
            const isExpanded = expandedReplies.includes(reply.id);
            return (
            <div 
              key={reply.id} 
              style={{ marginLeft: `${reply.level * 32}px` }}
              className={`flex flex-col rounded-xl overflow-hidden border ${
                reply.role === 'manager' 
                  ? 'bg-[#f8fafd] border-blue-100' 
                  : 'bg-white border-gray-200'
              } shadow-sm transition-all duration-200`}
            >
              <div 
                className={`p-5 ${isExpanded ? 'border-b border-gray-100' : ''} cursor-pointer hover:bg-gray-50/50 transition-colors relative`}
                onClick={() => toggleExpand(reply.id)}
              >
                {!isExpanded && (
                   <div className="absolute inset-y-0 left-0 w-1 bg-blue-400"></div>
                )}
                <div className="flex justify-between items-start">
                  <div className="flex items-start gap-4">
                    <div className={`w-11 h-11 rounded-full flex items-center justify-center text-white font-bold shadow-sm shrink-0 ${
                      reply.role === 'manager' ? 'bg-[#004c91]' : 'bg-[#e28743]'
                    }`}>
                      {reply.senderName.charAt(0)}
                    </div>
                    <div className="flex flex-col">
                      <div className="flex items-center gap-2 mb-0.5">
                        <span className="font-bold text-gray-900 text-[15px]">{reply.senderName}</span>
                        <span className="text-sm text-gray-500 font-normal">&lt;{reply.email}&gt;</span>
                      </div>
                      <div className="flex items-center gap-1.5">
                         <span className="text-gray-500 text-[13px]">tới</span>
                         <span className="font-medium text-gray-700 text-[13px]">{reply.receiverName}</span>
                         <span className="text-xs text-gray-400 font-normal">&lt;{reply.receiverEmail}&gt;</span>
                      </div>
                      <div className="mt-3 text-gray-800 font-semibold text-[15px]">
                        {reply.subject}
                      </div>
                    </div>
                  </div>
                  
                  <div className="flex flex-col items-end gap-3">
                    <div className="text-[13px] text-gray-500 flex items-center gap-1.5 font-medium whitespace-nowrap">
                      <Clock className="w-3.5 h-3.5" /> {reply.time}
                    </div>
                    {isExpanded && (
                      <button 
                        onClick={(e) => {
                          e.stopPropagation();
                          setActiveReplyId(activeReplyId === reply.id ? null : reply.id);
                        }}
                        className={`px-3 py-1.5 rounded-lg transition-colors flex items-center gap-2 text-sm font-medium outline-none ${
                          activeReplyId === reply.id 
                            ? 'bg-blue-100 text-[#004c91]' 
                            : 'text-gray-500 hover:text-[#004c91] hover:bg-blue-50'
                        }`}
                      >
                        <Reply className="w-4 h-4" />
                        <span>Trả lời</span>
                      </button>
                    )}
                  </div>
                </div>
              </div>
              
              {isExpanded && (
                <div className="bg-white">
                  <div className="px-5 py-5 text-gray-800 text-[15px] leading-relaxed ml-[52px]">
                    {reply.content.split('\n').map((line, i) => (
                      <p key={i} className="my-1.5 min-h-[1rem]">{line}</p>
                    ))}
                  </div>

                  {/* Quick Reply Form */}
                  {activeReplyId === reply.id && (
                    <div className="px-5 pb-5 ml-[52px] animate-in slide-in-from-top-2 fade-in duration-200">
                      <div className="border border-[#cde0f5] rounded-xl overflow-hidden bg-white shadow-sm focus-within:border-[#004c91] focus-within:ring-1 focus-within:ring-[#004c91] transition-all flex flex-col">
                        
                        {/* Header: From / To */}
                        <div className="px-4 py-3 border-b border-gray-100 bg-gray-50">
                          <div className="flex items-center gap-2 text-[14px]">
                            <span className="text-gray-500 w-10">Từ:</span>
                            <span className="font-semibold text-gray-800">Nguyễn Văn B (Phòng HTQT - HO)</span>
                            <span className="text-gray-500">&lt;bnv@fe.edu.vn&gt;</span>
                          </div>
                          <div className="flex items-center gap-2 text-[14px] mt-1.5">
                            <span className="text-gray-500 w-10">Tới:</span>
                            <span className="font-semibold text-gray-800">{reply.senderName}</span>
                            <span className="text-gray-500">&lt;{reply.email}&gt;</span>
                          </div>
                        </div>

                        {/* Textarea */}
                        <div className="bg-white">
                          <ReactQuill 
                            theme="snow" 
                            value={replyContent} 
                            onChange={setReplyContent}
                            placeholder="Nhập nội dung phản hồi..."
                            className="custom-quill-no-border"
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

                        {/* Controls */}
                        <div className="bg-white px-4 py-3 flex justify-end gap-3 rounded-b-xl border-t border-gray-100">
                          <button 
                            onClick={() => setActiveReplyId(null)}
                            className="px-5 py-2 rounded-lg border border-gray-300 text-sm text-gray-700 hover:border-[#004c91] hover:text-[#004c91] hover:bg-blue-50 font-medium transition-all outline-none cursor-pointer"
                          >
                            Hủy
                          </button>
                          <button 
                            className="bg-[#004c91] hover:bg-[#003a70] text-white px-6 py-2 rounded-lg text-sm font-bold flex items-center gap-2 transition-all hover:-translate-y-0.5 hover:shadow-md disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:translate-y-0 disabled:hover:shadow-none shadow-sm outline-none cursor-pointer"
                            disabled={!replyContent.trim()}
                          >
                            <Send className="w-4 h-4" /> Gửi
                          </button>
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </div>
          )})}
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
