import React, { useState } from 'react';
import { Search, Plus, Eye, Edit2, ChevronLeft, ChevronRight, Check, ArrowUpDown, Send } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { SendEmailTab } from './SendEmailTab';
import { EmailComposeModal } from '../../../features/emails/components/EmailComposeModal';

import { emailsApi } from '../../../features/emails/api/emailsApi';
import { format } from 'date-fns';

export function EmailManagement() {
  const navigate = useNavigate();
  const location = useLocation();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isStaff = userRole === 'STAFF' || userRole === 'DEPARTMENT' || userRole === 'STUDENT' || userRole === 'VISITOR';
  const isVisitor = userRole === 'VISITOR';

  const tabParam = new URLSearchParams(location.search).get('tab');
  const defaultTab = 'Danh sách email';
  const initialTab = tabParam === 'send' ? 'Gửi email' : defaultTab;
  const initialMailbox = tabParam === 'sent' ? 'sent' : tabParam === 'received' ? 'received' : 'all';
  
  const [activeTab, setActiveTab] = useState(initialTab);
  const [showTemplateModal, setShowTemplateModal] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<any>(null);
  const [showCompose, setShowCompose] = useState(false);

  // Template Data state
  const [data, setData] = useState<any[]>([]);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [isLoadingTemplates, setIsLoadingTemplates] = useState(false);

  // Email List state
  const [sentData, setSentData] = useState<any[]>([]);
  const [pageSent, setPageSent] = useState(1);
  const [itemsPerPageSent, setItemsPerPageSent] = useState(10);
  const [searchQuerySent, setSearchQuerySent] = useState('');
  const [mailboxFilter, setMailboxFilter] = useState(initialMailbox); // all, sent, received
  const [relatedTypeFilter, setRelatedTypeFilter] = useState(''); // VISIT_REQUEST, GENERAL
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');
  const [totalEmails, setTotalEmails] = useState(0);
  const [isLoadingEmails, setIsLoadingEmails] = useState(false);
  const [toastMessage, setToastMessage] = useState<{ type: 'success' | 'error' | 'info'; text: string } | null>(null);

  const showPageToast = (type: 'success' | 'error' | 'info', text: string) => {
    setToastMessage({ type, text });
    setTimeout(() => setToastMessage(null), 3000);
  };

  const fetchEmails = React.useCallback(async () => {
    setIsLoadingEmails(true);
    try {
      const params: any = {
        mailBox: mailboxFilter,
        keyword: searchQuerySent,
        page: pageSent,
        pageSize: itemsPerPageSent
      };
      if (relatedTypeFilter) params.relatedType = relatedTypeFilter;
      if (startDate) params.startDate = startDate;
      if (endDate) params.endDate = endDate;

      const res = await emailsApi.getEmailList(params);
      setSentData(res.data.items || []);
      setTotalEmails(res.data.totalCount || 0);
    } catch (error) {
      console.error('Failed to fetch emails:', error);
    } finally {
      setIsLoadingEmails(false);
    }
  }, [pageSent, itemsPerPageSent, searchQuerySent, mailboxFilter, relatedTypeFilter, startDate, endDate]);

  React.useEffect(() => {
    if (activeTab !== 'Danh sách email') return;

    const timeoutId = setTimeout(fetchEmails, 300);
    return () => clearTimeout(timeoutId);
  }, [activeTab, fetchEmails]);

  React.useEffect(() => {
    if (!showTemplateModal || selectedTemplate) return;

    const fetchTemplates = async () => {
      setIsLoadingTemplates(true);
      try {
        const res = await emailsApi.getEmailTemplateList({
          keyword: searchQuery,
          status: statusFilter,
          page,
          pageSize: itemsPerPage,
        });
        const items = res.data.items || res.data.templates || [];
        setData(Array.isArray(items) ? items : []);
      } catch (error) {
        console.error('Failed to fetch email templates:', error);
        setData([]);
      } finally {
        setIsLoadingTemplates(false);
      }
    };

    const timeoutId = setTimeout(fetchTemplates, 300);
    return () => clearTimeout(timeoutId);
  }, [showTemplateModal, selectedTemplate, searchQuery, statusFilter, page, itemsPerPage]);

  // Filter templates
  const filteredData = data.filter(item => {
    const name = item.name || item.templateName || item.templateCode || '';
    const subject = item.subject || '';
    const status = item.status || '';
    const matchSearch = name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                       subject.toLowerCase().includes(searchQuery.toLowerCase());
    const matchStatus = statusFilter ? status === statusFilter : true;
    return matchSearch && matchStatus;
  });
  const totalPages = Math.ceil(filteredData.length / itemsPerPage);
  const currentItems = filteredData.slice((page - 1) * itemsPerPage, page * itemsPerPage);

  const totalPagesSent = Math.ceil(totalEmails / itemsPerPageSent);
  const currentItemsSent = sentData;

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto">
      {toastMessage && (
        <div className={`fixed top-4 right-4 z-[120] px-6 py-3 rounded-lg shadow-lg text-white font-medium animate-in fade-in slide-in-from-top-2 ${
          toastMessage.type === 'success' ? 'bg-green-600' :
          toastMessage.type === 'error' ? 'bg-red-600' : 'bg-blue-600'
        }`}>
          {toastMessage.text}
        </div>
      )}

      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý email</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý email</h1>
      </div>

      {/* Tabs */}
      <div className="flex items-center gap-6 border-b border-gray-200 mb-6">
        <button 
          onClick={() => setActiveTab('Danh sách email')}
          className={`pb-3 font-bold text-[15px] border-b-2 transition-colors ${activeTab === 'Danh sách email' ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
        >
          Danh sách email
        </button>
        <button 
          onClick={() => setActiveTab('Gửi email')}
          className={`pb-3 font-bold text-[15px] border-b-2 transition-colors ${activeTab === 'Gửi email' ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
        >
          Gửi email
        </button>
      </div>

      {activeTab === 'Danh sách email' && (
        <>
          {/* Toolbar */}
          <div className="flex items-center flex-wrap gap-3 mb-6">
            <div className="relative flex-1 min-w-[200px] max-w-sm">
              <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
              <input 
                type="text" 
                value={searchQuerySent}
                onChange={(e) => { setSearchQuerySent(e.target.value); setPageSent(1); }}
                placeholder="Tìm kiếm email..." 
                className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm" 
              />
            </div>

            <select 
              value={mailboxFilter}
              onChange={(e) => { setMailboxFilter(e.target.value); setPageSent(1); }}
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none"
            >
              <option value="all">Tất cả email</option>
              <option value="sent">Đã gửi</option>
              <option value="received">Đã nhận</option>
            </select>

            <select 
              value={relatedTypeFilter}
              onChange={(e) => { setRelatedTypeFilter(e.target.value); setPageSent(1); }}
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none"
            >
              <option value="">Tất cả phân loại</option>
              <option value="VISIT_REQUEST">Tiếp khách</option>
              <option value="GENERAL">Khác</option>
            </select>

            <div className="flex items-center gap-2 border border-gray-300 rounded-lg bg-white px-2 shadow-sm text-sm">
               <input 
                 type="date"
                 value={startDate}
                 onChange={(e) => { setStartDate(e.target.value); setPageSent(1); }}
                 className="py-1.5 px-1 outline-none text-gray-600 bg-transparent"
                 title="Từ ngày"
               />
               <span className="text-gray-400">-</span>
               <input 
                 type="date"
                 value={endDate}
                 onChange={(e) => { setEndDate(e.target.value); setPageSent(1); }}
                 className="py-1.5 px-1 outline-none text-gray-600 bg-transparent"
                 title="Đến ngày"
               />
            </div>

            {userRole !== 'VISITOR' && (
              <div className="ml-auto flex items-center gap-2">
                <button
                  onClick={() => setShowCompose(true)}
                  className="bg-[#004c91] hover:bg-[#013565] text-white px-4 py-2 rounded-lg font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
                >
                  <Send className="w-4 h-4 flex-shrink-0" />
                  Soạn email
                </button>
                <button
                  onClick={() => setShowTemplateModal(true)}
                  className="bg-white border border-[#004c91] hover:bg-blue-50 text-[#004c91] px-4 py-2 rounded-lg font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
                >
                  <Eye className="w-4 h-4 flex-shrink-0" />
                  Xem mẫu mail
                </button>
              </div>
            )}
          </div>

          {/* Table */}
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full border-collapse min-w-[1000px]">
                <thead>
                  <tr className="bg-[#004c91] text-white text-[12px] tracking-wide uppercase whitespace-nowrap">
                    <th className="p-3 font-bold w-[70px] text-center">STT</th>
                    <th className="p-3 font-bold w-[120px] text-center">PHÂN LOẠI</th>
                    <th className="p-3 font-bold text-left pl-6">TIÊU ĐỀ</th>
                    <th className="p-3 font-bold w-[160px] text-center">THỜI GIAN</th>
                    <th className="p-3 font-bold w-[130px] text-center">TRẠNG THÁI</th>
                    <th className="p-3 font-bold w-[130px] text-center">HÀNH ĐỘNG</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {currentItemsSent.map((item, index) => (
                    <tr key={item.id} className="hover:bg-gray-50/80 transition-colors group">
                      <td className="p-3 align-middle text-center text-[14px] text-gray-600">
                        {(pageSent - 1) * itemsPerPageSent + index + 1}
                      </td>
                      <td className="p-3 align-middle text-center">
                        <span className={`inline-flex px-2 py-1 rounded text-xs font-bold ${item.relatedType === 'VISIT_REQUEST' ? 'bg-purple-100 text-purple-700' : 'bg-gray-100 text-gray-700'}`}>
                          {item.relatedType === 'VISIT_REQUEST' ? 'Tiếp khách' : 'Khác'}
                        </span>
                      </td>
                      <td className="p-3 align-middle text-gray-800 text-[14px] text-left pl-6 transition-colors font-medium">
                        <div className="line-clamp-2">{item.subject}</div>
                      </td>
                      <td className="p-3 align-middle whitespace-nowrap text-center text-[13px] text-gray-500 font-medium">
                          {item.sentAt 
                             ? format(new Date(item.sentAt.endsWith('Z') ? item.sentAt : item.sentAt + 'Z'), 'dd/MM/yyyy HH:mm') 
                             : (item.createdAt ? format(new Date(item.createdAt.endsWith('Z') ? item.createdAt : item.createdAt + 'Z'), 'dd/MM/yyyy HH:mm') : '')}
                      </td>
                      <td className="p-3 align-middle text-center">
                        <span className={`inline-flex items-center justify-center px-2.5 py-1.5 rounded-full text-[12px] font-bold border whitespace-nowrap ${
                            item.processStatus === 'COMPLETED' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 
                            item.processStatus === 'FAILED' ? 'bg-[#ffe4e4] text-[#a10a0a] border-[#efdada]' :
                            'bg-[#fff6e0] text-[#cf8e00] border-[#ffecba]'
                        }`}>
                          {item.processStatus === 'COMPLETED' ? 'Hoàn thành' : 
                           item.processStatus === 'FAILED' ? 'Thất bại' : 'Đang xử lý'}
                        </span>
                      </td>
                      <td className="p-3 align-middle whitespace-nowrap text-center">
                        <div className="flex items-center justify-center gap-2">
                          <button 
                            onClick={() => navigate(`/dashboard/email/detail/${item.sourceType}/${item.id}`)}
                            className="p-1.5 rounded-lg hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400 transition-colors outline-none" 
                            title="Xem chi tiết"
                          >
                            <Eye className="w-[16px] h-[16px]" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {isLoadingEmails && (
                    <tr>
                      <td colSpan={6} className="p-4 sm:p-6 md:p-8 text-center text-gray-500">
                        Đang tải...
                      </td>
                    </tr>
                  )}
                  {!isLoadingEmails && currentItemsSent.length === 0 && (
                    <tr>
                      <td colSpan={6} className="p-4 sm:p-6 md:p-8 text-center text-gray-500">
                        Không tìm thấy dữ liệu phù hợp
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
          
          {/* Pagination for Emails */}
          <div className="flex justify-end items-center mt-6">
            <div className="flex items-center gap-1.5">
               <button 
                onClick={() => setPageSent(p => Math.max(1, p - 1))}
                disabled={pageSent === 1}
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 transition-colors disabled:opacity-50"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <button className="w-9 h-9 rounded-xl font-bold text-sm bg-[#004c91] text-white">
                {pageSent}
              </button>
              <button 
                onClick={() => setPageSent(p => Math.min(totalPagesSent, p + 1))}
                disabled={pageSent === totalPagesSent || totalPagesSent === 0}
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 transition-colors disabled:opacity-50"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        </>
      )}

      {activeTab === 'Gửi email' && (
        <SendEmailTab
          onSent={(message) => {
            const isFailure = message?.includes('thất bại');
            showPageToast(isFailure ? 'error' : 'success', message || 'Gửi email thành công!');
            setSearchQuerySent('');
            setRelatedTypeFilter('');
            setStartDate('');
            setEndDate('');
            setMailboxFilter('sent');
            setPageSent(1);
            setActiveTab('Danh sách email');
          }}
        />
      )}

      {/* Rich compose (react-quill + attachments + inline images + autosave draft) */}
      <EmailComposeModal
        open={showCompose}
        onClose={() => setShowCompose(false)}
        pushToast={(type, msg) => showPageToast(type === 'warning' ? 'info' : type, msg)}
        onSent={() => { setMailboxFilter('sent'); setPageSent(1); setActiveTab('Danh sách email'); }}
      />

      {/* Template Modal */}
      {showTemplateModal && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/50 backdrop-blur-[2px]" onClick={() => { setShowTemplateModal(false); setSelectedTemplate(null); }}></div>
          <div className="bg-white rounded-xl shadow-xl w-full max-w-5xl relative z-10 p-6 max-h-[90vh] overflow-hidden flex flex-col">
            <div className="flex justify-between items-center mb-6">
              <h2 className="text-xl font-bold text-[#004c91]">
                {selectedTemplate ? 'Chi tiết mẫu email' : 'Danh sách mẫu email'}
              </h2>
              <button onClick={() => { setShowTemplateModal(false); setSelectedTemplate(null); }} className="text-gray-500 hover:text-gray-800 text-2xl font-bold leading-none">&times;</button>
            </div>
            
            {selectedTemplate ? (
              <div className="flex-1 overflow-y-auto space-y-6">
                <button 
                  onClick={() => setSelectedTemplate(null)}
                  className="flex items-center gap-2 text-sm font-bold text-gray-500 hover:text-[#004c91] transition-colors"
                >
                  <ChevronLeft className="w-4 h-4" />
                  Quay lại danh sách
                </button>
                <div className="bg-gray-50 p-6 rounded-lg border border-gray-200 space-y-4">
                  <div>
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Tên mẫu</label>
                    <div className="text-[15px] font-bold text-[#004c91]">{selectedTemplate.name}</div>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Tiêu đề email</label>
                    <div className="text-[14px] font-medium text-gray-800">{selectedTemplate.subject}</div>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Mục đích/Mô tả</label>
                    <div className="text-[14px] text-gray-600">{selectedTemplate.desc}</div>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Trạng thái</label>
                    <span className={`inline-flex px-2 py-1 rounded-full text-[11px] font-bold border ${selectedTemplate.status === 'Sử dụng' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
                      {selectedTemplate.status}
                    </span>
                  </div>
                  <div className="pt-4 border-t border-gray-200">
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-2">Nội dung mẫu (Preview)</label>
                    <div className="bg-white p-4 border border-gray-200 rounded min-h-[200px] text-sm text-gray-700" dangerouslySetInnerHTML={{ __html: selectedTemplate.content }} />
                  </div>
                </div>
              </div>
            ) : (
              <>
                <div className="flex items-center gap-3 mb-4">
                  <div className="relative flex-1">
                    <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
                    <input 
                      type="text" 
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder="Tìm kiếm mẫu email..." 
                      className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91]" 
                    />
                  </div>
                </div>

                <div className="overflow-y-auto flex-1 rounded-lg border border-gray-200">
                  <table className="w-full border-collapse min-w-[800px]">
                    <thead className="sticky top-0 bg-[#004c91] text-white text-[12px] tracking-wide uppercase whitespace-nowrap z-10">
                      <tr>
                        <th className="p-3 font-bold w-[60px] text-center">STT</th>
                        <th className="p-3 font-bold text-left pl-4">TÊN MẪU</th>
                        <th className="p-3 font-bold text-left pl-4">MỤC ĐÍCH</th>
                        <th className="p-3 font-bold w-[120px] text-center">TRẠNG THÁI</th>
                        <th className="p-3 font-bold w-[100px] text-center">HÀNH ĐỘNG</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-100">
                      {currentItems.map((item, index) => (
                        <tr key={item.id} className="hover:bg-gray-50">
                          <td className="p-3 align-middle text-center text-[13px]">{index + 1}</td>
                          <td className="p-3 align-middle font-bold text-[#004c91] text-[13px] pl-4">{item.name}</td>
                          <td className="p-3 align-middle text-gray-600 text-[13px] pl-4">{item.desc}</td>
                          <td className="p-3 align-middle text-center">
                            <span className={`inline-flex px-2 py-1 rounded-full text-[11px] font-bold border ${item.status === 'Sử dụng' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
                              {item.status}
                            </span>
                          </td>
                          <td className="p-3 align-middle text-center">
                            <button onClick={() => setSelectedTemplate(item)} className="p-1.5 text-gray-500 hover:text-[#004c91] transition-colors" title="Xem chi tiết">
                              <Eye className="w-[16px] h-[16px]" />
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
