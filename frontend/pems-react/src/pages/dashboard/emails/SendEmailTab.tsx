import React, { useState, useEffect } from 'react';
import { Send, Save, AlertCircle, Download, Upload, Check } from 'lucide-react';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';
import * as XLSX from 'xlsx';
import { useLocalEmailDraft } from '../../../features/emails/hooks/useLocalEmailDraft';
import httpClient from '../../../shared/api/httpClient';

export function SendEmailTab() {
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const userId = user?.id || user?.userId || 'anonymous';

  const { saveDraft, getValidDraft, clearDraft } = useLocalEmailDraft(userId);

  const [template, setTemplate] = useState('');
  const [subject, setSubject] = useState('');
  const [content, setContent] = useState('');
  const [to, setTo] = useState('');
  const [excelFile, setExcelFile] = useState<File | null>(null);
  
  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [showDraftPrompt, setShowDraftPrompt] = useState(false);
  const [draftData, setDraftData] = useState<any>(null);
  
  const [isSending, setIsSending] = useState(false);
  const [toastMessage, setToastMessage] = useState<{type: 'success' | 'error' | 'info', text: string} | null>(null);

  useEffect(() => {
    const validDraft = getValidDraft();
    if (validDraft) {
      setDraftData(validDraft);
      setShowDraftPrompt(true);
    }
  }, []);

  const handleRestoreDraft = () => {
    if (draftData) {
      setTemplate(draftData.templateId?.toString() || '');
      setSubject(draftData.subject || '');
      setContent(draftData.body || '');
      setTo(draftData.to || '');
    }
    setShowDraftPrompt(false);
  };

  const handleDiscardDraft = () => {
    clearDraft();
    setShowDraftPrompt(false);
  };

  const showToast = (type: 'success' | 'error' | 'info', text: string) => {
    setToastMessage({ type, text });
    setTimeout(() => setToastMessage(null), 3000);
  };

  const handleSaveDraft = () => {
    saveDraft({
      templateId: template ? parseInt(template) : null,
      subject,
      body: content,
      to
    });
    showToast('info', 'Đã lưu bản nháp trong 30 phút');
  };

  const parseEmails = (input: string) => {
    if (!input.trim()) return [];
    return input.split(',').map(email => ({ email: email.trim() })).filter(e => e.email !== '');
  };

  const handleSendEmail = async () => {
    try {
      setIsSending(true);
      setShowConfirmModal(false);
      
      let finalEmails = parseEmails(to);

      if (excelFile) {
        const data = await excelFile.arrayBuffer();
        const workbook = XLSX.read(data, { type: 'array' });
        const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
        const jsonData = XLSX.utils.sheet_to_json<any>(firstSheet);
        
        const excelEmails = jsonData
          .map(row => row.Email || row.email || Object.values(row)[0])
          .filter(val => typeof val === 'string' && val.trim() !== '')
          .map(email => ({ email: String(email).trim() }));
          
        finalEmails = [...finalEmails, ...excelEmails];
      }

      const payload = {
        templateId: template ? parseInt(template) : null,
        subject,
        body: content,
        to: finalEmails
      };

      if (payload.to.length === 0) {
        showToast('error', 'Vui lòng nhập email người nhận (To) hoặc đính kèm danh sách Excel hợp lệ.');
        setIsSending(false);
        return;
      }

      // Check format
      const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
      for (const recipient of payload.to) {
        if (!emailRegex.test(recipient.email)) {
          showToast('error', `Email không hợp lệ: ${recipient.email}`);
          setIsSending(false);
          return;
        }
      }

      if (!payload.subject.trim() || !payload.body.trim()) {
        showToast('error', 'Vui lòng nhập tiêu đề và nội dung email.');
        setIsSending(false);
        return;
      }

      await httpClient.post('/emails/sendemail', payload);
      
      showToast('success', 'Gửi email thành công!');
      clearDraft();
      
      // Clear form
      setTemplate('');
      setSubject('');
      setContent('');
      setTo('');
      setExcelFile(null);
    } catch (error: any) {
      showToast('error', error?.response?.data?.message || 'Gửi email thất bại.');
    } finally {
      setIsSending(false);
    }
  };

  return (
    <div className="space-y-6 relative">
      {toastMessage && (
        <div className={`fixed top-4 right-4 z-50 px-6 py-3 rounded-lg shadow-lg text-white font-medium animate-in fade-in slide-in-from-top-2 ${
          toastMessage.type === 'success' ? 'bg-green-600' : 
          toastMessage.type === 'error' ? 'bg-red-600' : 'bg-blue-600'
        }`}>
          {toastMessage.text}
        </div>
      )}

      {/* Draft Recovery Prompt Modal */}
      {showDraftPrompt && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/40 backdrop-blur-[2px]"></div>
          <div className="bg-white rounded-xl shadow-xl w-full max-w-sm relative z-10 p-6 animate-in fade-in zoom-in-95">
            <div className="flex items-center gap-3 mb-4 text-[#004c91]">
              <AlertCircle className="w-6 h-6" />
              <h3 className="text-lg font-bold">Khôi phục bản nháp</h3>
            </div>
            <p className="text-gray-600 mb-6 text-sm">
              Tìm thấy bản nháp email đã lưu. Bạn có muốn khôi phục không?
            </p>
            <div className="flex gap-3 justify-end">
              <button 
                onClick={handleDiscardDraft}
                className="px-4 py-2 rounded-lg text-gray-600 font-medium hover:bg-gray-100 transition-colors"
              >
                Bỏ qua
              </button>
              <button 
                onClick={handleRestoreDraft}
                className="px-4 py-2 rounded-lg bg-[#004c91] text-white font-medium hover:bg-[#003a70] transition-colors"
              >
                Khôi phục
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Phần 1: Thông tin người nhận */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="bg-[#004c91] px-6 py-4 border-b border-[#004c91] flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-[#f37021] text-white flex items-center justify-center font-bold text-sm shadow-sm">
            1
          </div>
          <h2 className="text-white font-bold text-lg uppercase tracking-wide">Thông tin người nhận</h2>
        </div>
        
        <div className="p-6 md:p-8 space-y-4">
           <div className="space-y-2">
            <label className="block text-[13px] font-bold text-gray-700 uppercase tracking-wide">
              To (Đến) <span className="text-red-500">*</span>
            </label>
            <input 
              type="text" 
              value={to}
              onChange={(e) => setTo(e.target.value)}
              placeholder="Nhập email, phân cách bằng dấu phẩy..." 
              className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-medium" 
            />
          </div>
          <div className="flex flex-col sm:flex-row items-start sm:items-center gap-4">
            <button
              onClick={() => {
                const csvData = "Email,Name\nexample1@fpt.edu.vn,Nguyen Van A\nexample2@fpt.edu.vn,Tran Thi B";
                const blob = new Blob([csvData], { type: 'text/csv;charset=utf-8;' });
                const url = URL.createObjectURL(blob);
                const link = document.createElement("a");
                link.setAttribute("href", url);
                link.setAttribute("download", "PEMS_Email_Template.csv");
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                showToast('success', 'Đã tải mẫu danh sách người nhận thành công!');
              }}
              className="px-4 py-2 bg-white border border-[#004c91] text-[#004c91] rounded-lg font-bold text-sm flex items-center gap-2 hover:bg-blue-50 transition-colors"
            >
              <Download className="w-4 h-4" />
              Tải mẫu Excel
            </button>
            <div className="relative">
              <input
                type="file"
                accept=".xlsx, .xls"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (file) {
                    if (file.name.endsWith('.xlsx') || file.name.endsWith('.xls')) {
                      setExcelFile(file);
                      showToast('success', 'Đã tải lên danh sách người nhận.');
                    } else {
                      showToast('error', 'Định dạng file không hợp lệ. Vui lòng chọn file Excel.');
                      e.target.value = '';
                    }
                  }
                }}
                className="hidden"
                id="excel-upload"
              />
              <label
                htmlFor="excel-upload"
                className="px-4 py-2 bg-[#004c91] text-white rounded-lg font-bold text-sm flex items-center gap-2 hover:bg-[#003a70] transition-colors cursor-pointer"
              >
                <Upload className="w-4 h-4" />
                Import Excel
              </label>
            </div>
          </div>
          {excelFile && (
            <div className="text-sm text-green-600 font-medium flex items-center gap-2">
              <Check className="w-4 h-4" />
              Đã đính kèm file: {excelFile.name}
              <button 
                onClick={() => setExcelFile(null)}
                className="text-red-500 hover:text-red-700 ml-2 font-bold"
              >
                (Xóa)
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Phần 2: Thông tin nội dung */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
        <div className="bg-[#004c91] px-6 py-4 border-b border-[#004c91] flex items-center gap-3">
          <div className="w-8 h-8 rounded-full bg-[#f37021] text-white flex items-center justify-center font-bold text-sm shadow-sm">
            2
          </div>
          <h2 className="text-white font-bold text-lg uppercase tracking-wide">Nội dung email</h2>
        </div>
        
        <div className="p-6 md:p-8 space-y-6">
          {userRole !== 'VISITOR' && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-2">
                <label className="block text-[13px] font-bold text-gray-700 uppercase tracking-wide">
                  Sử dụng mẫu email
                </label>
                <select 
                  value={template}
                  onChange={(e) => {
                    const val = e.target.value;
                    setTemplate(val);
                    if (val === "1") {
                      setSubject("Thông báo nhập học - Đại học FPT");
                      setContent("<p>Chào bạn,</p><p>Chúc mừng bạn đã trúng tuyển vào Đại học FPT.</p>");
                    } else if (val === "2") {
                      setSubject("Thư mời tham quan cơ sở Đại học FPT");
                      setContent("<p>Kính gửi Quý đối tác,</p><p>Trân trọng kính mời Quý đối tác đến tham quan cơ sở của chúng tôi.</p>");
                    }
                  }}
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-medium bg-white"
                >
                  <option value="">-- Chọn từ thư viện mẫu (Tùy chọn) --</option>
                  <option value="1">Thông báo nhập học</option>
                  <option value="2">Thư mời tham quan</option>
                </select>
              </div>
            </div>
          )}

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
            <div className="max-w-full [&_.ql-editor]:min-h-[250px] [&_.ql-editor]:text-[15px] [&_.ql-container]:rounded-b-lg [&_.ql-toolbar]:rounded-t-lg bg-white border-gray-300 rounded-lg">
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

      {/* Buttons */}
      <div className="flex justify-end pt-2 pb-8 gap-4">
        <button 
          onClick={handleSaveDraft}
          className="bg-white border-2 border-[#004c91] text-[#004c91] hover:bg-blue-50 px-8 py-3 rounded-lg font-bold transition-all shadow-sm hover:shadow flex items-center gap-2 uppercase tracking-wide transform hover:-translate-y-0.5"
        >
          <Save className="w-5 h-5" />
          LƯU DRAFT
        </button>
        <button 
          onClick={() => setShowConfirmModal(true)}
          disabled={isSending}
          className="bg-[#004c91] hover:bg-[#003a70] text-white px-8 py-3 rounded-lg font-bold transition-all shadow-md hover:shadow-lg flex items-center gap-2 uppercase tracking-wide transform hover:-translate-y-0.5 disabled:opacity-70 disabled:cursor-not-allowed"
        >
          <Send className="w-5 h-5" />
          {isSending ? 'ĐANG GỬI...' : 'GỬI EMAIL'}
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
                Hệ thống sẽ tiến hành gửi email tới các danh sách người nhận đã được chỉ định.
              </p>
              
              <div className="flex gap-4 justify-center">
                <button 
                  onClick={() => setShowConfirmModal(false)}
                  className="flex-1 px-4 py-3 rounded-xl border border-gray-300 text-gray-700 font-bold hover:bg-gray-50 transition-colors uppercase text-sm"
                >
                  HỦY
                </button>
                <button 
                  onClick={handleSendEmail}
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
