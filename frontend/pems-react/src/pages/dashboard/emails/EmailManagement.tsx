import React, { useState } from 'react';
import { Search, Plus, Eye, Edit2, ChevronLeft, ChevronRight, Check, ArrowUpDown } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { SendEmailTab } from './SendEmailTab';

const mockEmailData = [
  { id: 1, name: 'Thư mời đoàn đại biểu', subject: 'Thư mời tham quan và làm việc tại Đại học FPT', desc: 'Mẫu thư mời chính thức gửi cho các đoàn đại biểu đối tác quốc tế đến thăm trường.', content: '<p>Kính gửi Quý đại biểu,</p><p>Trân trọng kính mời Quý đại biểu đến tham quan cơ sở của chúng tôi.</p>', creator: 'Nguyễn Văn B', campus: 'Hà Nội', date: '01/05/2024', status: 'Sử dụng' },
  { id: 2, name: 'Thư cảm ơn sau chuyến thăm', subject: 'Cảm ơn chuyến thăm của quý vị đến Đại học FPT', desc: 'Mẫu email cảm ơn gửi sau khi cuộc gặp gỡ, đón tiếp kết thúc.', content: '<p>Kính gửi Quý vị,</p><p>Chúng tôi xin gửi lời cảm ơn chân thành nhất vì chuyến thăm vừa qua.</p>', creator: 'Nguyễn Văn C', campus: 'Quy Nhơn', date: '02/05/2024', status: 'Không sử dụng' },
];

const mockSentEmailData = [
  { id: 1, program: 'Đón tiếp ĐH Deakin (Úc)', subject: 'Thư mời tham quan và làm việc tại ĐH FPT', sender: 'Nguyễn Văn B', campus: 'Hà Nội', sendTime: '01/05/2024 08:00', status: 'Thành công', hasNewReply: true, mailbox: 'sent' },
  { id: 2, program: 'Chuyến thăm Panasonic', subject: 'Cảm ơn quý tập đoàn đã ghé thăm ĐH FPT', sender: 'Trần Thị C', campus: 'Hồ Chí Minh', sendTime: '02/05/2024 09:30', status: 'Thành công', hasNewReply: true, mailbox: 'received' },
  { id: 3, program: 'Hợp tác ĐH Chulalongkorn', subject: 'Dự thảo Biên bản ghi nhớ hợp tác (MOU)', sender: 'Lê Văn D', campus: 'Đà Nẵng', sendTime: '03/05/2024 10:15', status: 'Đang xử lý', hasNewReply: false, mailbox: 'sent' },
];

export function EmailManagement() {
  const navigate = useNavigate();
  const location = useLocation();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isStaff = userRole === 'STAFF' || userRole === 'DEPARTMENT' || userRole === 'STUDENT' || userRole === 'VISITOR';
  const isVisitor = userRole === 'VISITOR';

  const defaultTab = 'Danh sách email';
  const initialTab = new URLSearchParams(location.search).get('tab') === 'send' ? 'Gửi email' : defaultTab;
  
  const [activeTab, setActiveTab] = useState(initialTab);
  const [showTemplateModal, setShowTemplateModal] = useState(false);
  const [selectedTemplate, setSelectedTemplate] = useState<any>(null);

  // Template Data state
  const [data, setData] = useState(mockEmailData);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');

  // Email List state
  const [sentData, setSentData] = useState(mockSentEmailData);
  const [pageSent, setPageSent] = useState(1);
  const [itemsPerPageSent, setItemsPerPageSent] = useState(10);
  const [searchQuerySent, setSearchQuerySent] = useState('');
  const [mailboxFilter, setMailboxFilter] = useState('all'); // all, sent, received

  const toggleStatus = (id: number) => {
    setData(data.map(item => {
      if (item.id === id) {
        return { ...item, status: item.status === 'Sử dụng' ? 'Không sử dụng' : 'Sử dụng' };
      }
      return item;
    }));
  };

  // Filter templates
  const filteredData = data.filter(item => {
    const matchSearch = item.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
                       item.subject.toLowerCase().includes(searchQuery.toLowerCase());
    const matchStatus = statusFilter ? item.status === statusFilter : true;
    return matchSearch && matchStatus;
  });
  const totalPages = Math.ceil(filteredData.length / itemsPerPage);
  const currentItems = filteredData.slice((page - 1) * itemsPerPage, page * itemsPerPage);

  // Filter emails
  const filteredSentData = sentData.filter(item => {
    const matchSearch = item.subject.toLowerCase().includes(searchQuerySent.toLowerCase());
    const matchMailbox = mailboxFilter === 'all' ? true : item.mailbox === mailboxFilter;
    return matchSearch && matchMailbox;
  });
  const totalPagesSent = Math.ceil(filteredSentData.length / itemsPerPageSent);
  const currentItemsSent = filteredSentData.slice((pageSent - 1) * itemsPerPageSent, pageSent * itemsPerPageSent);

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto">
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

            {userRole !== 'VISITOR' && (
              <button 
                onClick={() => setShowTemplateModal(true)}
                className="ml-auto bg-white border border-[#004c91] hover:bg-blue-50 text-[#004c91] px-4 py-2 rounded-lg font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
              >
                <Eye className="w-4 h-4 flex-shrink-0" />
                Xem mẫu mail
              </button>
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
                        <span className={`inline-flex px-2 py-1 rounded text-xs font-bold ${item.mailbox === 'sent' ? 'bg-blue-100 text-blue-700' : 'bg-green-100 text-green-700'}`}>
                          {item.mailbox === 'sent' ? 'Đã gửi' : 'Đã nhận'}
                        </span>
                      </td>
                      <td className="p-3 align-middle text-gray-800 text-[14px] text-left pl-6 transition-colors font-medium">
                        <div className="line-clamp-2">{item.subject}</div>
                      </td>
                      <td className="p-3 align-middle whitespace-nowrap text-center text-[13px] text-gray-500 font-medium">
                          {item.sendTime}
                      </td>
                      <td className="p-3 align-middle text-center">
                        <span className={`inline-flex items-center justify-center px-2.5 py-1.5 rounded-full text-[12px] font-bold border whitespace-nowrap ${
                            item.status === 'Thành công' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 'bg-[#fff6e0] text-[#cf8e00] border-[#ffecba]'
                        }`}>
                          {item.status}
                        </span>
                      </td>
                      <td className="p-3 align-middle whitespace-nowrap text-center">
                        <div className="flex items-center justify-center gap-2">
                          <button 
                            onClick={() => navigate(`/dashboard/email/sent/${item.id}`)}
                            className="p-1.5 rounded-lg hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400 transition-colors outline-none" 
                            title="Xem chi tiết"
                          >
                            <Eye className="w-[16px] h-[16px]" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {currentItemsSent.length === 0 && (
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
        <SendEmailTab />
      )}

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
