/**
 * Trang EmailManagement
 * Bảng quản lý hộp thư chiến dịch, thư theo lịch và lưu trữ hộp thư tự động.
 */

// Đây là trang quản lý email (danh sách mẫu email và phần gửi email) trong khu vực quản trị
import React, { useState } from 'react';
import { Search, Plus, Eye, Edit2, ChevronLeft, ChevronRight, Check, ArrowUpDown } from 'lucide-react';
import { useNavigate, useLocation } from 'react-router-dom';
import { SendEmailTab } from './SendEmailTab';

const mockEmailData = [
  { id: 1, name: 'Thư mời đoàn đại biểu', subject: 'Thư mời tham quan và làm việc tại Đại học FPT', desc: 'Mẫu thư mời chính thức gửi cho các đoàn đại biểu đối tác quốc tế đến thăm trường.', creator: 'Nguyễn Văn B', campus: 'Hà Nội', date: '01/05/2024', status: 'Sử dụng' },
  { id: 2, name: 'Thư cảm ơn sau chuyến thăm', subject: 'Cảm ơn chuyến thăm của quý vị đến Đại học FPT', desc: 'Mẫu email cảm ơn gửi sau khi cuộc gặp gỡ, đón tiếp kết thúc.', creator: 'Nguyễn Văn C', campus: 'Quy Nhơn', date: '02/05/2024', status: 'Không sử dụng' },
  { id: 3, name: 'Lịch trình công tác', subject: 'Chi tiết lịch trình đón tiếp đoàn đại biểu', desc: 'Gửi lịch trình dự kiến cho đối tác trước ngày sang làm việc.', creator: 'Nguyễn Văn D', campus: 'Hồ Chí Minh', date: '03/05/2024', status: 'Sử dụng' },
  { id: 4, name: 'Thông tin ký kết MOU', subject: 'Dự thảo Biên bản ghi nhớ hợp tác (MOU)', desc: 'Gửi bản dự thảo và trao đổi chi tiết về nội dung ký kết MOU.', creator: 'Nguyễn Văn B', campus: 'Cần Thơ', date: '04/05/2024', status: 'Sử dụng' },
  { id: 5, name: 'Mời dự hội thảo quốc tế', subject: 'Thư mời tham dự Hội thảo Quốc tế', desc: 'Thư mời các đối tác tham dự hội thảo khoa học hoặc sự kiện hợp tác quốc tế.', creator: 'Nguyễn Văn C', campus: 'Đà Nẵng', date: '05/05/2024', status: 'Không sử dụng' },
  { id: 6, name: 'Trao đổi sinh viên', subject: 'Thông tin về chương trình trao đổi sinh viên kỳ Fall', desc: 'Email gửi các trường đối tác bàn về chỉ tiêu và thủ tục trao đổi sinh viên.', creator: 'Nguyễn Văn D', campus: 'Hà Nội', date: '06/05/2024', status: 'Sử dụng' },
  { id: 7, name: 'Đề xuất hợp tác dự án', subject: 'Đề xuất nội dung hợp tác nghiên cứu chung', desc: 'Gửi các ý tưởng hoặc văn bản đề xuất triển khai các dự án thực tế với đối tác.', creator: 'Nguyễn Văn B', campus: 'Toàn quốc', date: '07/05/2024', status: 'Sử dụng' },
  { id: 8, name: 'Tài liệu giới thiệu', subject: 'Tài liệu giới thiệu về Đại học FPT (Brochure)', desc: 'Gửi thông tin tổng quan, thông tin về các chuyên ngành và năng lực của nhà trường.', creator: 'Nguyễn Văn C', campus: 'Toàn quốc', date: '08/05/2024', status: 'Không sử dụng' },
  { id: 9, name: 'Cập nhật tiến độ', subject: 'Cập nhật tiến độ dự án hợp tác tháng', desc: 'Mẫu email định kỳ báo cáo tình hình các chương trình hợp tác đang diễn ra.', creator: 'Nguyễn Văn D', campus: 'Đà Nẵng', date: '09/05/2024', status: 'Sử dụng' },
  { id: 10, name: 'Chúc mừng sự kiện', subject: 'Thư chúc mừng nhân dịp lễ kỷ niệm công ty', desc: 'Gửi thư chúc mừng đối tác nhân ngày quốc khánh nước bạn hoặc kỷ niệm thành lập.', creator: 'Nguyễn Văn B', campus: 'Quy Nhơn', date: '10/05/2024', status: 'Không sử dụng' },
];

const mockSentEmailData = [
  { id: 1, program: 'Đón tiếp ĐH Deakin (Úc)', subject: 'Thư mời tham quan và làm việc tại ĐH FPT', sender: 'Nguyễn Văn B', campus: 'Hà Nội', sendTime: '01/05/2024 08:00', status: 'Thành công', hasNewReply: true },
  { id: 2, program: 'Chuyến thăm Panasonic', subject: 'Cảm ơn quý tập đoàn đã ghé thăm ĐH FPT', sender: 'Trần Thị C', campus: 'Hồ Chí Minh', sendTime: '02/05/2024 09:30', status: 'Thành công', hasNewReply: true },
  { id: 3, program: 'Hợp tác ĐH Chulalongkorn', subject: 'Dự thảo Biên bản ghi nhớ hợp tác (MOU)', sender: 'Lê Văn D', campus: 'Đà Nẵng', sendTime: '03/05/2024 10:15', status: 'Đang xử lý', hasNewReply: false },
  { id: 4, program: 'Hội thảo Việt - Nhật', subject: 'Thư mời tham dự Hội thảo Giáo dục Song phương', sender: 'Nguyễn Văn B', campus: 'Hà Nội', sendTime: '04/05/2024 14:00', status: 'Thành công', hasNewReply: true },
  { id: 5, program: 'Trao đổi SV Quốc tế', subject: 'Bàn bạc về chỉ tiêu trao đổi sinh viên K19', sender: 'Trần Thị C', campus: 'Hồ Chí Minh', sendTime: '05/05/2024 15:45', status: 'Thành công', hasNewReply: false },
  { id: 6, program: 'Hợp tác nghiên cứu AI', subject: 'Đề xuất nội dung hợp tác dự án AI', sender: 'Lê Văn D', campus: 'Đà Nẵng', sendTime: '06/05/2024 16:30', status: 'Thành công', hasNewReply: false },
  { id: 7, program: 'Đối tác mới ở Mỹ', subject: 'Tài liệu giới thiệu về năng lực của Đại học FPT', sender: 'Nguyễn Văn B', campus: 'Hà Nội', sendTime: '07/05/2024 08:00', status: 'Đang xử lý', hasNewReply: false },
];

export function EmailManagement() {
  const navigate = useNavigate();
  const location = useLocation();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isStaff = userRole === 'STAFF' || userRole === 'DEPT' || userRole === 'STUDENT' || userRole === 'VISITOR';

  const [data, setData] = useState(mockEmailData);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);
  
  // Parse initial tab from URL if present
  const queryParams = new URLSearchParams(location.search);
  const isVisitor = userRole === 'VISITOR';
  const defaultTab = isVisitor ? 'Gửi email' : 'Mẫu email';
  const initialTab = queryParams.get('tab') === 'sent' ? 'Danh sách email đã gửi' : defaultTab;
  
  const [activeTab, setActiveTab] = useState(initialTab);
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [campusFilter, setCampusFilter] = useState('');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc' | null>(null);

  const [sentData, setSentData] = useState(mockSentEmailData);
  const [pageSent, setPageSent] = useState(1);
  const [itemsPerPageSent, setItemsPerPageSent] = useState(5);
  const [searchQuerySent, setSearchQuerySent] = useState('');
  const [programFilter, setProgramFilter] = useState('');
  const [statusFilterSent, setStatusFilterSent] = useState('');
  const [replyFilter, setReplyFilter] = useState('');
  const [campusFilterSent, setCampusFilterSent] = useState('');
  const [sortOrderSent, setSortOrderSent] = useState<'asc' | 'desc' | null>(null);

  const toggleStatus = (id: number) => {
    setData(data.map(item => {
      if (item.id === id) {
        return {
          ...item,
          status: item.status === 'Sử dụng' ? 'Không sử dụng' : 'Sử dụng'
        };
      }
      return item;
    }));
  };

  let filteredData = data.filter(item => {
    const matchSearch = item.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
                       item.subject.toLowerCase().includes(searchQuery.toLowerCase()) ||
                       item.desc.toLowerCase().includes(searchQuery.toLowerCase());
    const matchStatus = statusFilter ? item.status === statusFilter : true;
    const matchCampus = (userRole === 'HO' && campusFilter) ? item.campus === campusFilter : true;
    return matchSearch && matchStatus && matchCampus;
  });

  if (sortOrder) {
    filteredData = [...filteredData].sort((a, b) => {
      const parseDate = (d: string) => {
        const [day, month, year] = d.split('/');
        return new Date(`${year}-${month}-${day}`).getTime();
      };
      const timeA = parseDate(a.date);
      const timeB = parseDate(b.date);
      return sortOrder === 'asc' ? timeA - timeB : timeB - timeA;
    });
  }

  const totalPages = Math.ceil(filteredData.length / itemsPerPage);
  const currentItems = filteredData.slice((page - 1) * itemsPerPage, page * itemsPerPage);

  let filteredSentData = sentData.filter(item => {
    const matchSearch = item.program.toLowerCase().includes(searchQuerySent.toLowerCase()) || 
                       item.subject.toLowerCase().includes(searchQuerySent.toLowerCase());
    const matchProgram = programFilter ? item.program.includes(programFilter) : true;
    const matchStatus = statusFilterSent ? item.status === statusFilterSent : true;
    const matchReply = replyFilter === 'new' ? item.hasNewReply : (replyFilter === 'none' ? !item.hasNewReply : true);
    const matchCampus = (userRole === 'HO' && campusFilterSent) ? item.campus === campusFilterSent : true;
    return matchSearch && matchProgram && matchStatus && matchReply && matchCampus;
  });

  if (sortOrderSent) {
    filteredSentData = [...filteredSentData].sort((a, b) => {
      const parseDate = (d: string) => {
        const [date, time] = d.split(' ');
        const [day, month, year] = date.split('/');
        return new Date(`${year}-${month}-${day}T${time}`).getTime();
      };
      const timeA = parseDate(a.sendTime);
      const timeB = parseDate(b.sendTime);
      return sortOrderSent === 'asc' ? timeA - timeB : timeB - timeA;
    });
  }

  const totalPagesSent = Math.ceil(filteredSentData.length / itemsPerPageSent);
  const currentItemsSent = filteredSentData.slice((pageSent - 1) * itemsPerPageSent, pageSent * itemsPerPageSent);

  const handleMarkReplyViewed = (id: number) => {
    setSentData(sentData.map(item => {
      if (item.id === id) {
        return { ...item, hasNewReply: false };
      }
      return item;
    }));
  };

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
        {userRole !== 'VISITOR' && (
          <button 
            onClick={() => setActiveTab('Mẫu email')}
            className={`pb-3 font-bold text-[15px] border-b-2 transition-colors ${activeTab === 'Mẫu email' ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
          >
            Mẫu email
          </button>
        )}
        <button 
          onClick={() => setActiveTab('Gửi email')}
          className={`pb-3 font-bold text-[15px] border-b-2 transition-colors ${activeTab === 'Gửi email' ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
        >
          Gửi email
        </button>
        <button 
          onClick={() => setActiveTab('Danh sách email đã gửi')}
          className={`pb-3 font-bold text-[15px] border-b-2 transition-colors ${activeTab === 'Danh sách email đã gửi' ? 'border-[#004c91] text-[#004c91]' : 'border-transparent text-gray-500 hover:text-gray-700'}`}
        >
          Danh sách email đã gửi
        </button>
      </div>

      {activeTab === 'Mẫu email' && userRole !== 'VISITOR' && (
        <>
          {/* Toolbar */}
          <div className="flex items-center flex-wrap gap-3 mb-6">
            <div className="relative flex-1 min-w-[200px] max-w-sm">
              <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
              <input 
                type="text" 
                value={searchQuery}
                onChange={(e) => { setSearchQuery(e.target.value); setPage(1); }}
                placeholder="Tìm kiếm mẫu email..." 
                className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm" 
              />
            </div>

            {userRole !== 'VISITOR' && (
              <select 
                value={statusFilter}
                onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }}
                className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none"
              >
                <option value="">Tất cả trạng thái</option>
                <option value="Sử dụng">Sử dụng</option>
                <option value="Không sử dụng">Không sử dụng</option>
              </select>
            )}

            {userRole === 'HO' && (
              <select 
                value={campusFilter}
                onChange={(e) => { setCampusFilter(e.target.value); setPage(1); }}
                className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none"
              >
                <option value="">Chọn cơ sở</option>
                <option value="Hà Nội">Hà Nội</option>
                <option value="Hồ Chí Minh">Hồ Chí Minh</option>
                <option value="Đà Nẵng">Đà Nẵng</option>
                <option value="Cần Thơ">Cần Thơ</option>
                <option value="Quy Nhơn">Quy Nhơn</option>
                <option value="Toàn quốc">Toàn quốc</option>
              </select>
            )}

            {!isStaff && (
              <button 
                onClick={() => navigate('/dashboard/email/create')}
                className="ml-auto bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-lg font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
              >
                <Plus className="w-4 h-4 flex-shrink-0" />
                Thêm mẫu email
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
                    <th className="p-3 font-bold w-[15%] text-left pl-6">TÊN MẪU</th>
                    <th className="p-3 font-bold w-[20%] text-left pl-6">TIÊU ĐỀ EMAIL</th>
                    <th className="p-3 font-bold w-[25%] text-left pl-6">MÔ TẢ</th>
                    {userRole !== 'VISITOR' && <th className="p-3 font-bold w-[110px] text-center">TRẠNG THÁI</th>}
                    {!isStaff && <th className="p-3 font-bold w-[130px] text-center">{userRole === 'HO' ? 'CƠ SỞ' : 'NGƯỜI TẠO'}</th>}
                    <th 
                      className="p-3 font-bold w-[160px] text-center cursor-pointer hover:bg-[#003a70] bg-[#004c91] text-white transition-colors select-none group"
                      onClick={() => {
                        if (!sortOrder) setSortOrder('asc');
                        else if (sortOrder === 'asc') setSortOrder('desc');
                        else setSortOrder(null);
                      }}
                    >
                      <div className="flex items-center justify-center gap-1 text-white">
                        NGÀY TẠO
                        <ArrowUpDown className={`w-3.5 h-3.5 transition-colors ${sortOrder ? 'text-white' : 'text-white/50 group-hover:text-white'}`} />
                      </div>
                    </th>
                    <th className="p-3 font-bold w-[110px] text-center">HÀNH ĐỘNG</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {currentItems.map((item, index) => (
                    <tr key={item.id} className="hover:bg-gray-50/80 transition-colors group">
                      <td className="p-3 align-middle text-center text-[14px] text-gray-600">
                        {(page - 1) * itemsPerPage + index + 1}
                      </td>
                      <td className="p-3 align-middle font-bold text-[#004c91] text-[14px] text-left pl-6">
                        <div className="line-clamp-2">{item.name}</div>
                      </td>
                      <td className="p-3 align-middle text-gray-800 font-medium text-[14px] text-left pl-6">
                        <div className="line-clamp-2">{item.subject}</div>
                      </td>
                      <td className="p-3 align-middle text-gray-500 text-[13px] leading-relaxed text-left pl-6">
                        <div className="line-clamp-2">{item.desc}</div>
                      </td>
                      {userRole !== 'VISITOR' && (
                        <td className="p-3 align-middle text-center">
                          {isStaff ? (
                            <span className={`inline-flex items-center justify-center px-2.5 py-1.5 rounded-full text-[12px] font-bold border whitespace-nowrap ${
                              item.status === 'Sử dụng' ? 'bg-[#eaffe4] text-[#0aa14f] border-[#ceefda]' : 'bg-gray-100 text-gray-500 border-gray-200'
                            }`}>
                              {item.status}
                            </span>
                          ) : (
                            <button 
                              onClick={() => toggleStatus(item.id)}
                              className="flex items-center mx-auto outline-none" 
                              title={item.status === 'Sử dụng' ? 'Sử dụng' : 'Không sử dụng'}
                            >
                              <div className={`w-8 h-4 rounded-full p-0.5 transition-colors relative flex items-center ${item.status === 'Sử dụng' ? 'bg-[#0aa14f]' : 'bg-gray-300'}`}>
                                <div className={`w-3 h-3 rounded-full bg-white shadow-sm transition-transform absolute ${item.status === 'Sử dụng' ? 'translate-x-4' : 'translate-x-0'}`}></div>
                              </div>
                            </button>
                          )}
                        </td>
                      )}
                      {!isStaff && (
                        <td className="p-3 align-middle whitespace-nowrap text-center">
                          {userRole === 'HO' ? (
                            <div className="font-bold text-[#004c91] text-[13px] bg-blue-50/50 px-2.5 py-1 rounded-md inline-block">{item.campus || 'Toàn quốc'}</div>
                          ) : (
                            <div className="font-bold text-gray-700 text-[14px]">{item.creator}</div>
                          )}
                        </td>
                      )}
                      <td className="p-3 align-middle whitespace-nowrap text-center text-[13px] text-gray-500 font-medium">
                          {item.date}
                      </td>
                      <td className="p-3 align-middle whitespace-nowrap text-center">
                        <div className="flex items-center justify-center gap-2">
                          <button 
                            onClick={() => navigate(`/dashboard/email/${item.id}`)}
                            className="p-1.5 rounded-lg hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400 transition-colors outline-none" 
                            title="Xem chi tiết"
                          >
                            <Eye className="w-[16px] h-[16px]" />
                          </button>
                          {!isStaff && (
                            <button 
                              onClick={() => navigate(`/dashboard/email/${item.id}/edit`)}
                              className="p-1.5 rounded-lg hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400 transition-colors outline-none" 
                              title="Chỉnh sửa"
                            >
                              <Edit2 className="w-[16px] h-[16px]" />
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          {/* Pagination */}
          <div className="flex items-center justify-between mt-6">
            <div className="flex items-center gap-3 text-sm text-gray-600 font-medium">
              <span>Hiển thị</span>
              <select 
                value={itemsPerPage} 
                onChange={(e) => {
                  setItemsPerPage(Number(e.target.value));
                  setPage(1);
                }} 
                className="border border-gray-300 bg-white rounded-lg px-2 py-1 outline-none focus:border-[#004c91] hover:border-gray-400 transition-colors cursor-pointer text-gray-700"
              >
                <option value="5">5</option>
                <option value="10">10</option>
                <option value="20">20</option>
                <option value="50">50</option>
                <option value="100">100</option>
              </select>
              <span>mẫu / trang</span>
            </div>

            <div className="flex items-center gap-1.5">
              <button 
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm" 
                onClick={() => setPage(page - 1)} 
                disabled={page === 1}
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              
              <div className="flex items-center gap-1">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map(p => (
                  <button 
                    key={p}
                    className={`w-9 h-9 rounded-xl font-bold text-sm transition-colors ${page === p ? 'bg-[#004c91] text-white shadow-sm border border-[#004c91]' : 'text-gray-600 hover:bg-gray-100 border border-transparent'}`} 
                    onClick={() => setPage(p)}
                  >
                    {p}
                  </button>
                ))}
              </div>

              <button 
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm" 
                onClick={() => setPage(page + 1)} 
                disabled={page === totalPages || totalPages === 0}
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

      {activeTab === 'Danh sách email đã gửi' && (
        <>
          {/* Toolbar */}
          <div className="flex items-center flex-wrap gap-3 mb-6">
            <div className="relative flex-1 min-w-[200px] max-w-sm">
              <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
              <input 
                type="text" 
                value={searchQuerySent}
                onChange={(e) => { setSearchQuerySent(e.target.value); setPageSent(1); }}
                placeholder="Tìm kiếm email đã gửi..." 
                className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-lg focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm" 
              />
            </div>

            <select 
              value={statusFilterSent}
              onChange={(e) => { setStatusFilterSent(e.target.value); setPageSent(1); }}
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Thành công">Thành công</option>
              <option value="Đang xử lý">Đang xử lý</option>
            </select>

            <select 
              value={replyFilter}
              onChange={(e) => { setReplyFilter(e.target.value); setPageSent(1); }}
              className="border border-gray-300 rounded-lg px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none"
            >
              <option value="">Tất cả email</option>
              <option value="new">Có phản hồi mới</option>
              <option value="none">Chưa có phản hồi</option>
            </select>

          </div>

          {/* Table */}
          <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full border-collapse min-w-[1000px]">
                <thead>
                  <tr className="bg-[#004c91] text-white text-[12px] tracking-wide uppercase whitespace-nowrap">
                    <th className="p-3 font-bold w-[70px] text-center">STT</th>
                    <th className="p-3 font-bold text-left pl-6">TIÊU ĐỀ</th>
                    <th 
                      className="p-3 font-bold w-[160px] text-center cursor-pointer hover:bg-[#003a70] bg-[#004c91] text-white transition-colors select-none group"
                      onClick={() => {
                        if (!sortOrderSent) setSortOrderSent('asc');
                        else if (sortOrderSent === 'asc') setSortOrderSent('desc');
                        else setSortOrderSent(null);
                      }}
                    >
                      <div className="flex items-center justify-center gap-1 text-white">
                        THỜI GIAN GỬI
                        <ArrowUpDown className={`w-3.5 h-3.5 transition-colors ${sortOrderSent ? 'text-white' : 'text-white/50 group-hover:text-white'}`} />
                      </div>
                    </th>
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
                      <td className="p-3 align-middle text-gray-800 text-[14px] text-left pl-6 transition-colors">
                        <div className="flex items-center gap-2">
                          {item.hasNewReply && <div className="w-2.5 h-2.5 rounded-full bg-orange-500 shrink-0"></div>}
                          <div className={`line-clamp-2 ${item.hasNewReply ? 'font-bold' : 'font-medium'}`}>{item.subject}</div>
                        </div>
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
                            onClick={() => {
                              if (item.hasNewReply) {
                                handleMarkReplyViewed(item.id);
                              }
                              navigate(`/dashboard/email/sent/${item.id}`);
                            }}
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
                      <td colSpan={7} className="p-4 sm:p-6 md:p-8 text-center text-gray-500">
                        Không tìm thấy dữ liệu phù hợp
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Pagination */}
          <div className="flex items-center justify-between mt-6">
            <div className="flex items-center gap-3 text-sm text-gray-600 font-medium">
              <span>Hiển thị</span>
              <select 
                value={itemsPerPageSent}
                onChange={(e) => {
                  setItemsPerPageSent(Number(e.target.value));
                  setPageSent(1);
                }}
                className="border border-gray-300 bg-white rounded-lg px-2 py-1 outline-none focus:border-[#004c91] hover:border-gray-400 transition-colors cursor-pointer text-gray-700"
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
              <span>mẫu / trang</span>
            </div>
            
            <div className="flex items-center gap-1.5">
               <button 
                onClick={() => setPageSent(p => Math.max(1, p - 1))}
                disabled={pageSent === 1}
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              
              <div className="flex items-center gap-1">
                {Array.from({ length: totalPagesSent }, (_, i) => i + 1).map(p => (
                  <button 
                    key={p}
                    className={`w-9 h-9 rounded-xl font-bold text-sm transition-colors ${pageSent === p ? 'bg-[#004c91] text-white shadow-sm border border-[#004c91]' : 'text-gray-600 hover:bg-gray-100 border border-transparent'}`} 
                    onClick={() => setPageSent(p)}
                  >
                    {p}
                  </button>
                ))}
              </div>

              <button 
                onClick={() => setPageSent(p => Math.min(totalPagesSent, p + 1))}
                disabled={pageSent === totalPagesSent || totalPagesSent === 0}
                className="p-2 rounded-xl border border-gray-200 text-gray-500 hover:bg-gray-50 hover:text-[#004c91] transition-colors disabled:opacity-50 disabled:cursor-not-allowed bg-white shadow-sm"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
