/**
 * Khai báo Component/Trang: FAQManagement
 * Thuộc cấu trúc: faq
 * Chức năng: Hiển thị giao diện và logic liên quan đến FAQManagement
 */

// Đây là trang quản lý FAQ
import React, { useState } from 'react';
import { Search, Plus, Eye, Trash2, EyeOff, LayoutTemplate, HelpCircle, ChevronLeft, ChevronRight, X, User } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

const mockDataArray = [
  { id: 1, type: 'Chương trình', question: 'Điều kiện để tham gia học kỳ trao đổi là gì?', answer: 'Sinh viên phải hoàn thành ít nhất 2 học kỳ tại trường, điểm trung bình >= 7.0...', status: 'Hiển thị' },
  { id: 2, type: 'Học phí', question: 'Tôi có phải đóng học phí cho trường đối tác không?', answer: 'Tùy thuộc vào chương trình và thỏa thuận giữa 2 trường, đa phần là không đóng...', status: 'Hiển thị' },
  { id: 3, type: 'Visa', question: 'Trường có hỗ trợ làm visa không?', answer: 'Trường sẽ cung cấp các giấy tờ cần thiết như giấy chấp nhận nhập học, hướng dẫn...', status: 'Ẩn' },
  { id: 4, type: 'Ký túc xá', question: 'Có bắt buộc ở ký túc xá khi học trao đổi không?', answer: 'Không bắt buộc, sinh viên có thể tự thuê ngoài nếu tìm được chỗ ở phù hợp...', status: 'Hiển thị' },
  { id: 5, type: 'Chương trình', question: 'Có thể chuyển đổi tín chỉ như thế nào?', answer: 'Tín chỉ được chuyển đổi dựa trên sự tương đương của môn học giữa hai trường...', status: 'Hiển thị' },
  { id: 6, type: 'Visa', question: 'Thời gian xét duyệt visa mất bao lâu?', answer: 'Tùy vào quốc gia, thông thường sẽ mất từ 2-4 tuần từ ngày nộp hồ sơ đủ...', status: 'Hiển thị' },
  { id: 7, type: 'Học phí', question: 'Chi phí sinh hoạt trung bình là bao nhiêu?', answer: 'Chi phí sinh hoạt phụ thuộc vào thành phố và quốc gia nơi bạn đến học tập...', status: 'Hiển thị' },
  { id: 8, type: 'Ký túc xá', question: 'Chi phí ký túc xá là bao nhiêu?', answer: 'Chi phí ký túc xá phụ thuộc vào loại phòng và quy định của từng trường...', status: 'Ẩn' },
  { id: 9, type: 'Chương trình', question: 'Đăng ký môn học trao đổi như thế nào?', answer: 'Sinh viên sẽ được hướng dẫn tạo tài khoản và đăng ký môn học trực tuyến...', status: 'Hiển thị' },
  { id: 10, type: 'Visa', question: 'Có cần chứng minh tài chính khi làm visa không?', answer: 'Đa số các quốc gia sẽ yêu cầu chứng minh tài chính với một số tiền tối thiểu...', status: 'Hiển thị' },
];

export function FAQManagement() {
  const navigate = useNavigate();
  const [data, setData] = useState(mockDataArray);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState('');
  const [selectedStatus, setSelectedStatus] = useState('');
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [newFAQ, setNewFAQ] = useState({ question: '', answer: '', type: 'Chương trình' });

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isHO = userRole === 'HO';
  const isAdmin = userRole === 'ADMIN' || (userRole === 'STAFF' && user?.subRole === 'Leader');
  const isFullAccess = isHO || isAdmin;

  if (!['ADMIN', 'HO', 'STAFF', 'DEPT', 'STUDENT'].includes(userRole || '')) {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-800 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500">Trang này không dành cho vai trò của bạn.</p>
        </div>
      </div>
    );
  }

  let filteredData = data.filter(item => {
    const matchesSearch = item.question.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesType = selectedType ? item.type === selectedType : true;
    const matchesStatus = selectedStatus ? item.status === selectedStatus : true;
    return matchesSearch && matchesType && matchesStatus;
  });

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Hiển thị': return <span className="inline-block px-3 py-1.5 bg-[#eaffe4] text-[#0aa14f] font-bold rounded-full text-[12px] border border-[#ceefda] whitespace-nowrap">Hiển thị</span>;
      case 'Ẩn': return <span className="inline-block px-3 py-1.5 bg-gray-100 text-gray-600 font-bold rounded-full text-[12px] border border-gray-200 whitespace-nowrap">Ẩn</span>;
      default: return null;
    }
  };

  const toggleVisibility = (id: number) => {
    setData(data.map(item => {
      if (item.id === id) {
        return { ...item, status: item.status === 'Hiển thị' ? 'Ẩn' : 'Hiển thị' };
      }
      return item;
    }));
  };

  const renderActions = (item: any) => {
    const btnClass = "p-1.5 rounded-lg transition-colors";
    
    return (
      <div className="flex items-center gap-1 justify-center">
        <button 
          onClick={() => navigate(`/dashboard/faq/${item.id}`)}
          className={`${btnClass} hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400`} 
          title="Xem chi tiết"
        >
          <Eye className="w-[16px] h-[16px]" />
        </button>
        {isFullAccess && (
          <button 
            onClick={() => toggleVisibility(item.id)}
            className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ml-2 ${item.status === 'Hiển thị' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
            title={item.status === 'Hiển thị' ? 'Ẩn FAQ' : 'Hiển thị FAQ'}
          >
            <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${item.status === 'Hiển thị' ? 'translate-x-4' : 'translate-x-0'}`} />
          </button>
        )}
      </div>
    );
  };

  const totalPages = Math.ceil(filteredData.length / itemsPerPage) || 1;
  const currentData = filteredData.slice((page - 1) * itemsPerPage, page * itemsPerPage);

  return (
    <div className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen">
      {/* Breadcrumb & Tiêu đề */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Quản lý FAQ</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý FAQ</h1>
      </div>

      {/* Filters */}
      <div className="flex flex-col md:flex-row gap-4 items-center justify-between relative z-20">
        <div className="flex items-center gap-4 w-full md:w-auto flex-1">
          <div className="relative w-full md:max-w-[400px]">
            <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input 
              type="text" 
              placeholder="Tìm kiếm câu hỏi, câu trả lời..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all shadow-sm"
            />
          </div>
          
          <select 
            value={selectedType}
            onChange={(e) => setSelectedType(e.target.value)}
            className="w-full md:w-[150px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
          >
            <option value="">Tất cả loại bài</option>
            <option value="Chương trình">Chương trình</option>
            <option value="Học phí">Học phí</option>
            <option value="Visa">Visa</option>
            <option value="Ký túc xá">Ký túc xá</option>
          </select>
          <select 
            value={selectedStatus}
            onChange={(e) => setSelectedStatus(e.target.value)}
            className="w-full md:w-[150px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
          >
            <option value="">Tất cả trạng thái</option>
            <option value="Hiển thị">Hiển thị</option>
            <option value="Ẩn">Ẩn</option>
          </select>
        </div>

        {isFullAccess && (
          <button 
            onClick={() => setIsCreateModalOpen(true)}
            className="flex items-center justify-center gap-2 bg-[#f37021] hover:bg-[#e06218] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all shadow-[#f37021]/20 hover:shadow-[#f37021]/40 shrink-0"
          >
             <Plus className="w-4 h-4" /> Thêm mới FAQ
          </button>
        )}
      </div>

      {/* Table */}
      <div className="bg-white border rounded-2xl shadow-sm border-gray-100 relative z-10 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[900px]">
            <thead className="bg-[#004c91] text-white">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-16 text-center">STT</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[15%]">Loại</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[35%]">Câu hỏi</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[20%]">Trả lời</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Trạng thái</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 bg-white">
              {currentData.length > 0 ? currentData.map((item, index) => (
                <tr key={item.id} className="hover:bg-blue-50/30 transition-colors">
                  <td className="px-4 py-4 whitespace-nowrap text-sm font-medium text-gray-500 text-center">
                    {(page - 1) * itemsPerPage + index + 1}
                  </td>
                  <td className="px-4 py-4 whitespace-nowrap">
                    <span className="inline-flex items-center px-2.5 py-1 rounded-md text-xs font-semibold bg-gray-50 text-gray-600 border border-gray-200">
                      {item.type}
                    </span>
                  </td>
                  <td className="px-4 py-4">
                    <p className="text-sm font-bold text-gray-900 line-clamp-2">{item.question}</p>
                  </td>
                  <td className="px-4 py-4">
                    <p className="text-sm text-gray-500 line-clamp-2">{item.answer}</p>
                  </td>
                  <td className="px-4 py-4 whitespace-nowrap text-center">
                    {getStatusBadge(item.status)}
                  </td>
                  <td className="px-4 py-4 whitespace-nowrap">
                    {renderActions(item)}
                  </td>
                </tr>
              )) : (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center text-gray-500 font-medium">
                    Không tìm thấy FAQ nào
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Footer / Pagination */}
        <div className="px-4 py-3 border-t border-gray-100 bg-gray-50/50 flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-2 text-sm text-gray-600">
             <span>Hiển thị</span>
             <select 
               className="border border-gray-200 rounded-lg px-2 py-1 bg-white focus:outline-none focus:ring-1 focus:ring-[#004c91] font-medium"
               value={itemsPerPage}
               onChange={(e) => {
                 setItemsPerPage(Number(e.target.value));
                 setPage(1);
               }}
             >
               <option value={5}>5</option>
               <option value={10}>10</option>
               <option value={20}>20</option>
               <option value={50}>50</option>
               <option value={100}>100</option>
             </select>
             <span>bản ghi / trang</span>
          </div>

          <div className="flex items-center gap-1.5">
             <button 
               className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-100 text-gray-500 disabled:opacity-50 disabled:cursor-not-allowed bg-white"
               disabled={page === 1}
               onClick={() => setPage(page - 1)}
             >
               <ChevronLeft className="w-5 h-5" />
             </button>
             {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (
               <button
                 key={p}
                 className={`w-8 h-8 rounded-lg flex items-center justify-center text-sm font-bold transition-colors ${
                   page === p ? 'bg-[#004c91] text-white border border-[#004c91]' : 'bg-white border border-gray-200 text-gray-600 hover:bg-gray-50'
                 }`}
                 onClick={() => setPage(p)}
               >
                 {p}
               </button>
             ))}
             <button 
               className="p-1.5 rounded-lg border border-gray-200 hover:bg-gray-100 text-gray-500 disabled:opacity-50 disabled:cursor-not-allowed bg-white"
               disabled={page === totalPages || totalPages === 0}
               onClick={() => setPage(page + 1)}
             >
               <ChevronRight className="w-5 h-5" />
             </button>
          </div>
        </div>
      </div>

      {/* Create FAQ Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-lg overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-xl font-bold text-[#004c91] flex items-center gap-2">
                <Plus className="w-5 h-5" /> 
                Thêm mới FAQ
              </h3>
              <button onClick={() => setIsCreateModalOpen(false)} className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-4">
              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Câu hỏi<span className="text-red-500 ml-1">*</span></label>
                <textarea 
                  rows={3}
                  className="w-full px-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all resize-none"
                  placeholder="Nhập câu hỏi..."
                  value={newFAQ.question}
                  onChange={(e) => setNewFAQ({...newFAQ, question: e.target.value})}
                ></textarea>
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Trả lời<span className="text-red-500 ml-1">*</span></label>
                <textarea 
                  rows={4}
                  className="w-full px-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all resize-none"
                  placeholder="Nhập câu trả lời..."
                  value={newFAQ.answer}
                  onChange={(e) => setNewFAQ({...newFAQ, answer: e.target.value})}
                ></textarea>
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Loại FAQ<span className="text-red-500 ml-1">*</span></label>
                <select 
                  className="w-full px-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all"
                  value={newFAQ.type}
                  onChange={(e) => setNewFAQ({...newFAQ, type: e.target.value})}
                >
                  <option value="Chương trình">Chương trình</option>
                  <option value="Học phí">Học phí</option>
                  <option value="Visa">Visa</option>
                  <option value="Ký túc xá">Ký túc xá</option>
                </select>
              </div>
            </div>

            <div className="p-5 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3">
              <button 
                onClick={() => setIsCreateModalOpen(false)}
                className="px-5 py-2 bg-white border border-gray-200 text-gray-600 font-bold rounded-xl hover:bg-gray-50 transition-colors"
              >
                Hủy
              </button>
              <button 
                onClick={() => {
                  if (newFAQ.question && newFAQ.answer) {
                    const newId = data.length > 0 ? Math.max(...data.map(d => d.id)) + 1 : 1;
                    setData([{ id: newId, status: 'Hiển thị', ...newFAQ }, ...data]);
                    setIsCreateModalOpen(false);
                    setNewFAQ({ question: '', answer: '', type: 'Chương trình' });
                  }
                }}
                className="px-5 py-2 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003366] transition-colors shadow-sm cursor-pointer"
              >
                Tạo mới
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
