import React, { useState } from 'react';
import { Search, Plus, Eye, Edit2, ChevronLeft, ChevronRight, Check, ArrowUpDown, Send } from 'lucide-react';
import { useNavigate, useLocation, useSearchParams } from 'react-router-dom';
import { EmailComposeModal } from '../../../features/emails/components/EmailComposeModal';
import { TemplateManagement } from './TemplateManagement';

import { emailsApi } from '../../../features/emails/api/emailsApi';
import { format } from 'date-fns';

import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { sanitizeHtml } from '../../../shared/security/sanitizeHtml';
export function EmailManagement() {
  const navigate = useNavigate();
  const location = useLocation();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isStaff = userRole === 'STAFF' || userRole === 'DEPARTMENT' || userRole === 'STUDENT' || userRole === 'VISITOR';
  const isVisitor = userRole === 'VISITOR';

  const [searchParams, setSearchParams] = useSearchParams();

  const tabParam = searchParams.get('tab');
  const initialMailbox = tabParam === 'sent' ? 'sent' : tabParam === 'received' ? 'received' : 'all';
  const initialPage = parseInt(searchParams.get('page') || '1', 10);
  const initialSearch = searchParams.get('search') || '';
  const initialRelated = searchParams.get('related') || '';
  const initialStart = searchParams.get('start') || '';
  const initialEnd = searchParams.get('end') || '';

  const [activeTab, setActiveTab] = useState<'emails' | 'templates'>('emails');
  const [showTemplateModal, setShowTemplateModal] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<any>(null);
  const [showCompose, setShowCompose] = useState(false);
  const [composeInitialData, setComposeInitialData] = useState<{
    subject: string;
    bodyHtml: string;
    templateId: number | null;
  }>({ subject: '', bodyHtml: '', templateId: null });

  // Template Data state
  const [data, setData] = useState<any[]>([]);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [purposeFilter, setPurposeFilter] = useState('');
  const [isLoadingTemplates, setIsLoadingTemplates] = useState(false);
  const [totalTemplates, setTotalTemplates] = useState(0);

  // Email List state
  const [sentData, setSentData] = useState<any[]>([]);
  const [pageSent, setPageSent] = useState(initialPage);
  const [itemsPerPageSent, setItemsPerPageSent] = useState(10);
  const [searchQuerySent, setSearchQuerySent] = useState(initialSearch);
  const [mailboxFilter, setMailboxFilter] = useState(initialMailbox); // all, sent, received
  const [relatedTypeFilter, setRelatedTypeFilter] = useState(initialRelated); // VISIT_REQUEST, GENERAL
  const [startDate, setStartDate] = useState(initialStart);
  const [endDate, setEndDate] = useState(initialEnd);
  const [totalEmails, setTotalEmails] = useState(0);
  const [isLoadingEmails, setIsLoadingEmails] = useState(false);
  const [toastMessage, setToastMessage] = useState<{ type: 'success' | 'error' | 'info'; text: string } | null>(null);

  React.useEffect(() => {
    const params = new URLSearchParams(searchParams);
    let changed = false;
    
    if (pageSent !== 1) { params.set('page', pageSent.toString()); changed = true; } else { params.delete('page'); }
    if (searchQuerySent) { params.set('search', searchQuerySent); changed = true; } else { params.delete('search'); }
    if (mailboxFilter !== 'all') { params.set('tab', mailboxFilter); changed = true; } else { params.delete('tab'); }
    if (relatedTypeFilter) { params.set('related', relatedTypeFilter); changed = true; } else { params.delete('related'); }
    if (startDate) { params.set('start', startDate); changed = true; } else { params.delete('start'); }
    if (endDate) { params.set('end', endDate); changed = true; } else { params.delete('end'); }

    if (params.toString() !== searchParams.toString()) {
      setSearchParams(params, { replace: true });
    }
  }, [pageSent, searchQuerySent, mailboxFilter, relatedTypeFilter, startDate, endDate, searchParams, setSearchParams]);

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
    const timeoutId = setTimeout(fetchEmails, 300);
    return () => clearTimeout(timeoutId);
  }, [fetchEmails]);

  React.useEffect(() => {
    if (!showTemplateModal || selectedTemplate) return;

    const fetchTemplates = async () => {
      setIsLoadingTemplates(true);
      try {
        const res = await emailsApi.getEmailTemplateList({
          keyword: searchQuery,
          status: statusFilter,
          purpose: purposeFilter,
          mode: 'use',
          page,
          pageSize: itemsPerPage,
        });
        const items = res.data.items || res.data.templates || [];
        setData(Array.isArray(items) ? items : []);
        setTotalTemplates(res.data.totalCount || res.data.totalItems || 0);
      } catch (error) {
        console.error('Failed to fetch email templates:', error);
        setData([]);
        setTotalTemplates(0);
      } finally {
        setIsLoadingTemplates(false);
      }
    };

    const timeoutId = setTimeout(fetchTemplates, 300);
    return () => clearTimeout(timeoutId);
  }, [showTemplateModal, selectedTemplate, searchQuery, statusFilter, purposeFilter, page, itemsPerPage]);

  const totalPagesSent = Math.ceil(totalEmails / itemsPerPageSent);
  const currentItemsSent = sentData;

  return (
    <div className="w-full">
      {toastMessage && (
        <div className={`fixed top-4 right-4 z-[120] px-6 py-3 rounded-lg shadow-lg text-white font-medium animate-in fade-in slide-in-from-top-2 ${
          toastMessage.type === 'success' ? 'bg-green-600' :
          toastMessage.type === 'error' ? 'bg-red-600' : 'bg-blue-600'
        }`}>
          {toastMessage.text}
        </div>
      )}

      <div className="mb-2 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý email</span>
      </div>
      <div className="border-b border-gray-100 pb-3 mb-4 text-left flex justify-between items-end">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý email</h1>
        {userRole === 'HO' && (
          <div className="flex gap-4">
            <button 
              className={`pb-3 -mb-[13px] border-b-2 px-1 font-medium text-sm transition-colors ${activeTab === 'emails' ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
              onClick={() => setActiveTab('emails')}
            >
              Danh sách email
            </button>
            <button 
              className={`pb-3 -mb-[13px] border-b-2 px-1 font-medium text-sm transition-colors ${activeTab === 'templates' ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
              onClick={() => setActiveTab('templates')}
            >
              Cấu hình mẫu email
            </button>
          </div>
        )}
      </div>

      {activeTab === 'emails' ? (
        <>
          {/* Toolbar */}
      <div className="flex items-center flex-wrap gap-3 mb-4">
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
                  onClick={() => {
                    setComposeInitialData({ subject: '', bodyHtml: '', templateId: null });
                    setShowCompose(true);
                  }}
                  className="bg-[#004c91] hover:bg-[#013565] text-white px-4 py-2 rounded-lg font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
                >
                  <Send className="w-4 h-4 flex-shrink-0" />
                  Soạn email
                </button>
                <button
                  onClick={() => {
                    setShowTemplateModal(true);
                    setSelectedTemplate(null);
                  }}
                  className="bg-white border border-[#004c91] hover:bg-blue-50 text-[#004c91] px-4 py-2 rounded-lg font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
                >
                  <Eye className="w-4 h-4 flex-shrink-0" />
                  Xem mẫu mail
                </button>
              </div>
            )}
          </div>

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
                             ? formatVietnamDateTime(item.sentAt)
                             : (item.createdAt ? formatVietnamDateTime(item.createdAt) : '')}
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
      ) : activeTab === 'templates' && userRole === 'HO' ? (
        <TemplateManagement pushToast={showPageToast} />
      ) : null}

      {/* Rich compose (react-quill + attachments + inline images), previewed and sent directly. */}
      <EmailComposeModal
        open={showCompose}
        onClose={() => {
          setShowCompose(false);
          setComposeInitialData({ subject: '', bodyHtml: '', templateId: null });
        }}
        initialSubject={composeInitialData.subject}
        initialBodyHtml={composeInitialData.bodyHtml}
        emailTemplateId={composeInitialData.templateId}
        pushToast={(type, msg) => showPageToast(type === 'warning' ? 'info' : type, msg)}
        onSent={() => {
          setMailboxFilter('sent');
          setPageSent(1);
        }}
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
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Mục đích/Mô tả</label>
                    <div className="text-[14px] text-gray-600">{selectedTemplate.purpose || selectedTemplate.description}</div>
                  </div>
                  <div>
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-1">Trạng thái</label>
                    <span className={`inline-flex px-2 py-1 rounded-full text-[11px] font-bold border ${selectedTemplate.status === 'ACTIVE' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
                      {selectedTemplate.status === 'ACTIVE' ? 'Đang hoạt động' : 'Tạm khóa'}
                    </span>
                  </div>
                  <div className="pt-4 border-t border-gray-200 flex justify-end">
                     <button
                       onClick={() => {
                         setComposeInitialData({
                           subject: selectedTemplate.subjectVi || selectedTemplate.subject || '',
                           bodyHtml: selectedTemplate.bodyVi || selectedTemplate.content || '',
                           templateId: selectedTemplate.emailTemplateId || selectedTemplate.id || null
                         });
                         setShowTemplateModal(false);
                         setSelectedTemplate(null);
                         setShowCompose(true);
                       }}
                       className="bg-[#004c91] text-white px-5 py-2 rounded-lg font-bold hover:bg-[#013565] transition-colors"
                     >
                       Dùng mẫu này
                     </button>
                  </div>
                  <div className="pt-4 border-t border-gray-200">
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-2">Tiêu đề (Tiếng Việt)</label>
                    <div className="text-[14px] font-medium text-gray-800 mb-4">{selectedTemplate.subjectVi || selectedTemplate.subject}</div>
                    
                    <label className="block text-xs font-bold text-gray-500 uppercase mb-2">Nội dung mẫu (Preview)</label>
                    <div className="bg-white p-4 border border-gray-200 rounded min-h-[200px] text-sm text-gray-700" dangerouslySetInnerHTML={{ __html: sanitizeHtml(selectedTemplate.bodyVi || selectedTemplate.content || '') }} />
                  </div>
                </div>
              </div>
            ) : (
              <div className="flex flex-col h-[calc(90vh-140px)]">
                <div className="flex items-center gap-3 mb-4 shrink-0">
                  <div className="relative flex-1">
                    <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
                    <input 
                      type="text" 
                      value={searchQuery}
                      onChange={(e) => { setSearchQuery(e.target.value); setPage(1); }}
                      placeholder="Tìm kiếm mẫu email..." 
                      className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91]" 
                    />
                  </div>
                  <select
                    value={purposeFilter}
                    onChange={(e) => { setPurposeFilter(e.target.value); setPage(1); }}
                    className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] bg-white outline-none"
                  >
                    <option value="">Tất cả mục đích</option>
                    <option value="VISIT_REQUEST">Lịch thăm</option>
                    <option value="LOGISTICS">Hậu cần</option>
                    <option value="GENERAL">Khác</option>
                  </select>
                  <select
                    value={itemsPerPage}
                    onChange={(e) => { setItemsPerPage(Number(e.target.value)); setPage(1); }}
                    className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none focus:border-[#004c91] bg-white outline-none"
                  >
                    <option value="5">5 dòng/trang</option>
                    <option value="10">10 dòng/trang</option>
                    <option value="20">20 dòng/trang</option>
                  </select>
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
                      {data.length === 0 ? (
                        <tr>
                          <td colSpan={5} className="p-8 text-center text-gray-500">
                            Không tìm thấy mẫu email phù hợp.
                          </td>
                        </tr>
                      ) : (
                        data.map((item, index) => (
                          <tr key={item.id || item.emailTemplateId} className="hover:bg-gray-50">
                            <td className="p-3 align-middle text-center text-[13px]">{index + 1 + (page - 1) * itemsPerPage}</td>
                            <td className="p-3 align-middle font-bold text-[#004c91] text-[13px] pl-4">{item.name}</td>
                            <td className="p-3 align-middle text-gray-600 text-[13px] pl-4">{item.purpose || item.description || ''}</td>
                            <td className="p-3 align-middle text-center">
                              <span className={`inline-flex px-2 py-1 rounded-full text-[11px] font-bold border ${item.status === 'ACTIVE' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 'bg-gray-100 text-gray-500 border-gray-200'}`}>
                                {item.status === 'ACTIVE' ? 'Đang hoạt động' : 'Tạm khóa'}
                              </span>
                            </td>
                            <td className="p-3 align-middle text-center">
                              <button onClick={async () => {
                                try {
                                  const res = await emailsApi.getEmailTemplateDetail(item.emailTemplateId);
                                  setSelectedTemplate(res.data);
                                } catch (e) {
                                  showPageToast('error', 'Không thể tải mẫu email');
                                }
                              }} className="p-1.5 text-gray-500 hover:text-[#004c91] transition-colors" title="Xem chi tiết">
                                <Eye className="w-[16px] h-[16px]" />
                              </button>
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                  </table>
                </div>
                
                {/* Pagination for Template List */}
                {totalTemplates > 0 && (
                  <div className="flex items-center justify-between border-t border-slate-200 pt-4 mt-4 shrink-0">
                    <p className="text-sm font-medium text-slate-500">
                      Hiển thị {(page - 1) * itemsPerPage + 1}-{Math.min(page * itemsPerPage, totalTemplates)} / {totalTemplates} mẫu
                    </p>
                    <div className="flex items-center gap-1.5">
                      <button 
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        disabled={page === 1}
                        className="px-3 py-1.5 rounded-md border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:hover:bg-transparent outline-none"
                      >
                        Trước
                      </button>
                      <div className="flex items-center gap-1">
                        {Array.from({ length: Math.ceil(totalTemplates / itemsPerPage) }, (_, i) => i + 1).map(p => (
                          <button
                            key={p}
                            onClick={() => setPage(p)}
                            className={`w-8 h-8 rounded-md text-sm font-medium transition-colors outline-none ${page === p ? 'bg-[#004c91] text-white' : 'text-gray-600 hover:bg-gray-100 border border-transparent'}`}
                          >
                            {p}
                          </button>
                        ))}
                      </div>
                      <button 
                        onClick={() => setPage(p => Math.min(Math.ceil(totalTemplates / itemsPerPage), p + 1))}
                        disabled={page === Math.ceil(totalTemplates / itemsPerPage)}
                        className="px-3 py-1.5 rounded-md border border-gray-200 text-sm font-medium text-gray-600 hover:bg-gray-100 transition-colors disabled:opacity-50 disabled:hover:bg-transparent outline-none"
                      >
                        Sau
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
