// Đây là tab gửi email (hiển thị form gửi email) nằm trong trang Quản lý email
import React, { useState } from 'react';
import { Send, Download, Upload, FileSpreadsheet } from 'lucide-react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';

export function SendEmailTab() {
  const [program, setProgram] = useState('');
  const [template, setTemplate] = useState('');
  const [subject, setSubject] = useState('');
  const [content, setContent] = useState('');
  
  const [recipientsType, setRecipientsType] = useState('all');
  const [recipientEmails, setRecipientEmails] = useState('');
  const [showConfirmModal, setShowConfirmModal] = useState(false);

  return (
    <div className="space-y-6">
      {/* Phần 1: Thông tin nội dung */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="bg-[#004c91] px-6 py-4 border-b border-[#004c91] flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-[#f37021] text-white flex items-center justify-center font-bold text-sm shadow-sm">
            1
          </div>
          <h2 className="text-white font-bold text-lg uppercase tracking-wide">Thông tin nội dung</h2>
        </div>
        
        <div className="p-6 md:p-8 space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div className="space-y-2">
              <label className="block text-[13px] font-bold text-gray-700 uppercase tracking-wide">
                Chương trình liên quan <span className="text-red-500">*</span>
              </label>
              <select 
                value={program}
                onChange={(e) => setProgram(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-medium bg-white"
              >
                <option value="">-- Chọn chương trình --</option>
                <option value="p1">Định hướng tân sinh viên 2024</option>
                <option value="p2">Seminar Trí tuệ Nhân tạo</option>
              </select>
            </div>
            
            <div className="space-y-2">
              <label className="block text-[13px] font-bold text-gray-700 uppercase tracking-wide">
                Sử dụng mẫu email <span className="text-red-500">*</span>
              </label>
              <select 
                value={template}
                onChange={(e) => setTemplate(e.target.value)}
                className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-medium bg-white"
              >
                <option value="">-- Chọn từ thư viện mẫu --</option>
                <option value="t1">Thông báo nhập học</option>
                <option value="t2">Cảnh báo học vụ</option>
              </select>
            </div>
          </div>

          <div className="space-y-2">
            <label className="block text-[13px] font-bold text-gray-700 uppercase tracking-wide">
              Tiêu đề email <span className="text-red-500">*</span>
            </label>
            <input 
              type="text" 
              value={subject}
              onChange={(e) => setSubject(e.target.value)}
              placeholder="Nhập tiêu đề email..." 
              className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-medium" 
            />
          </div>

          <div className="space-y-2">
            <label className="block text-[13px] font-bold text-gray-700 uppercase tracking-wide">
              Nội dung chi tiết <span className="text-red-500">*</span>
            </label>
            <div className="max-w-full [&_.ql-editor]:min-h-[250px] [&_.ql-editor]:text-[15px] [&_.ql-container]:rounded-b-lg [&_.ql-toolbar]:rounded-t-lg bg-white">
              {/* @ts-ignore */}
              <ReactQuill
                  theme="snow"
                  value={content}
                  onChange={setContent}
                  placeholder="Soạn nội dung email..."
                  modules={{
                    toolbar: [
                      ['bold', 'italic', 'underline', 'strike'],
                      [{ 'align': [] }],
                      [{ 'list': 'ordered'}, { 'list': 'bullet' }],
                      ['link', 'image'],
                      ['clean']
                    ],
                  }}
                />
            </div>
          </div>
        </div>
      </div>

      {/* Phần 2: Thông tin người nhận */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="bg-[#004c91] px-6 py-4 border-b border-[#004c91] flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-[#f37021] text-white flex items-center justify-center font-bold text-sm shadow-sm">
            2
          </div>
          <h2 className="text-white font-bold text-lg uppercase tracking-wide">Thông tin người nhận</h2>
        </div>
        
        <div className="p-6 md:p-8">
           <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
             {/* Left Column: Tải file mẫu */}
             <div className="bg-[#e8f5e9] rounded-xl p-8 border border-green-200 flex flex-col items-center justify-center text-center space-y-4">
               <h3 className="text-green-800 font-bold text-lg flex items-center gap-2">
                 <Download className="w-5 h-5" />
                 Bước 1: Tải file mẫu
               </h3>
               <p className="text-green-700 text-sm">
                 Đây là file định dạng của hệ thống, bạn hãy tải về và thực hiện điền email vào đúng cột cho sẵn.
               </p>
               <button className="bg-[#2e7d32] hover:bg-[#1b5e20] text-white px-6 py-2.5 rounded-full font-bold transition-colors flex items-center gap-2 shadow-sm">
                 <FileSpreadsheet className="w-5 h-5" />
                 Tải file Excel mẫu
               </button>
             </div>

             {/* Right Column: Tải danh sách lên */}
             <div className="bg-[#e3f2fd] rounded-xl p-8 border border-blue-200 flex flex-col items-center justify-center text-center space-y-4">
               <h3 className="text-[#004c91] font-bold text-lg flex items-center gap-2">
                 <Upload className="w-5 h-5" />
                 Bước 2: Tải danh sách lên
               </h3>
               <p className="text-blue-700 text-sm">
                 Sau khi điền email người nhận vào file định dạng của hệ thống, hãy tải file excel đó lên.
               </p>
               <button className="bg-[#004c91] hover:bg-[#003a70] text-white px-6 py-2.5 rounded-full font-bold transition-colors flex items-center gap-2 shadow-sm">
                 <Upload className="w-5 h-5" />
                 Chọn file danh sách
               </button>
             </div>
           </div>
        </div>
      </div>

      {/* Button Gửi */}
      <div className="flex justify-end pt-2 pb-8">
        <button 
          onClick={() => setShowConfirmModal(true)}
          className="bg-[#004c91] hover:bg-[#003a70] text-white px-8 py-3 rounded-lg font-bold transition-all shadow-md hover:shadow-lg flex items-center gap-2 uppercase tracking-wide transform hover:-translate-y-0.5"
        >
          <Send className="w-5 h-5" />
          GỬI EMAIL
        </button>
      </div>

      {showConfirmModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div 
            className="absolute inset-0 bg-black/40 backdrop-blur-[2px]" 
            onClick={() => setShowConfirmModal(false)}
          />
          <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden relative z-10 animate-in fade-in zoom-in-95 duration-200 border border-gray-100">
            <div className="p-7">
              <div className="w-14 h-14 rounded-full bg-blue-50 flex items-center justify-center mb-5 mx-auto border border-blue-100">
                <Send className="w-7 h-7 text-[#004c91] ml-1" />
              </div>
              <h3 className="text-xl font-bold text-center text-gray-900 mb-2">Bạn có chắc chắn muốn gửi email này ?</h3>
              <p className="text-center text-gray-500 mb-8 text-[15px]">
                Hệ thống sẽ tiến hành gửi email tới các danh sách người nhận đã được chọn.
              </p>
              
              <div className="flex gap-4 justify-center">
                <button 
                  onClick={() => setShowConfirmModal(false)}
                  className="flex-1 px-4 py-3 rounded-xl border border-gray-300 text-gray-700 font-bold hover:bg-gray-50 transition-colors uppercase text-sm"
                >
                  HỦY
                </button>
                <button 
                  onClick={() => {
                    // Xử lý gửi email ở đây
                    setShowConfirmModal(false);
                  }}
                  className="flex-1 px-4 py-3 rounded-xl bg-[#004c91] hover:bg-[#003a70] text-white font-bold transition-colors shadow-md uppercase text-sm"
                >
                  XÁC NHẬN
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

    </div>
  );
}
