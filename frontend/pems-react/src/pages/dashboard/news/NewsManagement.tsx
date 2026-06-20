/**
 * Trang NewsManagement
 * Giao diện quản trị, xem danh sách, đăng duyệt bài viết/tin tức.
 */

// Đây là trang quản lý danh sách các bài viết tin tức trong khu vực quản trị
import React, { useState } from 'react';
import { Search, Plus, Eye, Edit2, Trash2, Check, X, ChevronLeft, ChevronRight, ArrowUpDown } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import fptLogo from '../../../assets/images/2021-FPTU-Eng.png';

const mockDataArray = [
  { id: 1, type: 'News', title: 'Thông báo mở đơn đăng ký học kỳ Fall 2024 tại các trường đối tác Hàn Quốc', desc: 'Phòng Hợp tác Quốc tế (IC-PDP) thông báo chương trình trao đổi sinh viên học kỳ Fall 2024 tại các trường đối tác Hàn Quốc.', creator: 'Nguyễn Văn A', date: '01/05/2024', status: 'Đã Duyệt', image: fptLogo, campus: 'Hà Nội' },
  { id: 2, type: 'Review', title: 'Trải nghiệm 6 tháng học tập tại SolBridge International School of Business', desc: 'Một học kỳ ở Hàn Quốc đã mang đến cho mình những trải nghiệm không thể nào quên về văn hóa, con người, và môi trường học tập.', creator: 'Nguyễn Văn A', date: '02/05/2024', status: 'Chờ Duyệt', image: fptLogo, campus: 'Đà Nẵng' },
  { id: 3, type: 'News', title: 'Lễ ký kết biên bản ghi nhớ hợp tác (MOU) giữa ĐH FPT và ĐH Chulalongkorn', desc: 'Sáng nay, tại campus Hòa Lạc đã diễn ra lễ ký kết MOU quan trọng mở ra nhiều cơ hội cho sinh viên.', creator: 'Nguyễn Văn A', date: '03/05/2024', status: 'Từ Chối', image: fptLogo, campus: 'Hà Nội' },
  { id: 4, type: 'Review', title: 'Top 5 điều cần chuẩn bị trước khi đi du học trao đổi Nhật Bản', desc: 'Dành cho các bạn sinh viên đang chuẩn bị hành trang đến xứ sở hoa anh đào vào học kỳ tới.', creator: 'Nguyễn Văn B', date: '04/05/2024', status: 'Ẩn', image: fptLogo, campus: 'Hồ Chí Minh' },
  { id: 5, type: 'News', title: 'Giao lưu văn hóa: Tuần lễ văn hóa ASEAN tại ĐH FPT Cần Thơ', desc: 'Sự kiện quy tụ hàng ngàn sinh viên tham gia với hoạt động trải nghiệm đặc sắc.', creator: 'Nguyễn Văn C', date: '05/05/2024', status: 'Đã Duyệt', image: fptLogo, campus: 'Cần Thơ' },
  { id: 6, type: 'Review', title: 'Ăn gì và ở đâu khi tham gia học kỳ nước ngoài tại Đài Loan?', desc: 'Kinh nghiệm tìm nhà ở và ăn uống tiết kiệm dành cho sinh viên trao đổi.', creator: 'Nguyễn Văn D', date: '06/05/2024', status: 'Chờ Duyệt', image: fptLogo, campus: 'Quy Nhơn' },
  { id: 7, type: 'News', title: 'Kết quả học bổng trao đổi sinh viên học kỳ Spring 2024', desc: 'Danh sách 20 sinh viên xuất sắc nhận được học bổng trao đổi học kỳ Spring.', creator: 'Nguyễn Văn B', date: '07/05/2024', status: 'Từ Chối', image: fptLogo, campus: 'Hồ Chí Minh' },
  { id: 8, type: 'Review', title: 'Chuyện chưa kể về cuộc sống sinh viên tại Úc', desc: 'Những khó khăn và cách thích nghi với môi trường sống hoàn toàn mới.', creator: 'Nguyễn Văn C', date: '08/05/2024', status: 'Ẩn', image: fptLogo, campus: 'Hà Nội' },
  { id: 9, type: 'News', title: 'Workshop: Hành trang hội nhập toàn cầu cho sinh viên', desc: 'Chuyên gia chia sẻ kỹ năng làm việc trong môi trường đa văn hóa.', creator: 'Nguyễn Văn D', date: '09/05/2024', status: 'Đã Duyệt', image: fptLogo, campus: 'Đà Nẵng' },
  { id: 10, type: 'Review', title: 'Review môn học tiếng Pháp cho sinh viên đi Pháp', desc: 'Cách đạt điểm cao và giao tiếp tự tin chỉ sau 1 khóa học.', creator: 'Nguyễn Văn B', date: '10/05/2024', status: 'Chờ Duyệt', image: fptLogo, campus: 'Hồ Chí Minh' },
];

export function NewsManagement() {
  const navigate = useNavigate();
  const [data, setData] = useState(mockDataArray);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isStaffLeader = user?.role?.toUpperCase() === 'STAFF' && user?.subRole?.toUpperCase() === 'LEADER';
  const isAdmin = userRole === 'ADMIN' || isStaffLeader;
  const isStaff = userRole === 'STAFF';
  const isStudent = userRole === 'STUDENT';
  const isHO = userRole === 'HO';

  if (!isAdmin && !isStaff && !isStudent && !isHO) {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-800 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500">Trang này chỉ dành cho tài khoản Admin, Staff, Student và HO.</p>
        </div>
      </div>
    );
  }

  const [selectedCampus, setSelectedCampus] = useState('');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc' | null>(null);

  let filteredData = data;
  if (isStudent) {
    filteredData = filteredData.filter(item => item.creator === user?.name);
  } else if (isStaff) {
    // Staff logic was to only act on their own or view others, but list is all or what?
    // "đối với role staff thì: Đối với bài tin do mình tạo (người tạo là :"Nguyễn Văn B") thì được phép (view - edit - delete) ... Đối với tin tức của người khác : chỉ có view" - so they see all.
  }
  
  if (selectedCampus) {
    filteredData = filteredData.filter(item => item.campus === selectedCampus);
  }

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

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Đã Duyệt': return <span className="inline-block px-3 py-1.5 bg-[#eaffe4] text-[#0aa14f] font-bold rounded-full text-[12px] border border-[#ceefda] whitespace-nowrap">Đã Duyệt</span>;
      case 'Chờ Duyệt': return <span className="inline-block px-3 py-1.5 bg-[#fff6e0] text-[#cf8e00] font-bold rounded-full text-[12px] border border-[#ffecba] whitespace-nowrap">Chờ Duyệt</span>;
      case 'Từ Chối': return <span className="inline-block px-3 py-1.5 bg-red-50 text-red-600 font-bold rounded-full text-[12px] border border-red-100 whitespace-nowrap">Từ Chối</span>;
      case 'Ẩn': return <span className="inline-block px-3 py-1.5 bg-gray-100 text-gray-600 font-bold rounded-full text-[12px] border border-gray-200 whitespace-nowrap">Ẩn</span>;
      default: return null;
    }
  };

  const toggleVisibility = (id: number) => {
    setData(data.map(item => {
      if (item.id === id) {
        if (item.status === 'Ẩn') return { ...item, status: 'Đã Duyệt' };
        if (item.status === 'Đã Duyệt') return { ...item, status: 'Ẩn' };
      }
      return item;
    }));
  };

  const renderActions = (id: number, status: string, creator: string) => {
    const btnClass = "p-1.5 rounded-lg transition-colors";
    
    // Icon components
    const viewBtn = <button key="view" onClick={() => navigate(`/dashboard/news/${id}`)} className={`${btnClass} hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400`} title="Xem chi tiết"><Eye className="w-[16px] h-[16px]" /></button>;
    const editBtn = <button key="edit" onClick={() => navigate(`/dashboard/news/${id}/edit`)} className={`${btnClass} hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400`} title="Chỉnh sửa"><Edit2 className="w-[16px] h-[16px]" /></button>;
    const deleteBtn = (
      <button 
        key="delete" 
        onClick={() => {
          if (window.confirm('Bạn có chắc chắn muốn xóa bài viết này?')) {
            setData(data.filter(item => item.id !== id));
          }
        }} 
        className={`${btnClass} hover:bg-red-50 hover:text-red-600 text-gray-400`} 
        title="Xóa"
      >
        <Trash2 className="w-[16px] h-[16px]" />
      </button>
    );
    const toggleBtn = (
      <button key="toggle" onClick={() => toggleVisibility(id)} className="flex items-center mx-1" title="Ẩn/Hiện">
        <div className={`w-8 h-4 rounded-full p-0.5 transition-colors relative ${status !== 'Ẩn' ? 'bg-[#004c91]' : 'bg-gray-300'}`}>
          <div className={`w-3 h-3 rounded-full bg-white shadow-sm transition-transform ${status !== 'Ẩn' ? 'translate-x-4' : 'translate-x-0'}`}></div>
        </div>
      </button>
    );
    const acceptBtn = <button key="accept" className={`${btnClass} hover:bg-[#eaffe4] hover:text-[#0aa14f] text-gray-400`} title="Chấp nhận duyệt"><Check className="w-[18px] h-[18px] stroke-[2.5]" /></button>;
    const denyBtn = <button key="deny" className={`${btnClass} hover:bg-red-50 hover:text-red-600 text-gray-400`} title="Từ chối duyệt"><X className="w-[18px] h-[18px] stroke-[2.5]" /></button>;

    if (isHO) {
      return <div className="flex items-center justify-center gap-1">{viewBtn}</div>;
    }

    if (isAdmin) {
      switch (status) {
        case 'Đã Duyệt': return <div className="flex items-center justify-center gap-1">{viewBtn}{!isStaffLeader && deleteBtn}{toggleBtn}</div>;
        case 'Chờ Duyệt': return <div className="flex items-center justify-center gap-1">{viewBtn}{!isStaffLeader && editBtn}{acceptBtn}{denyBtn}</div>;
        case 'Từ Chối': return <div className="flex items-center justify-center gap-1">{viewBtn}{!isStaffLeader && deleteBtn}</div>;
        case 'Ẩn': return <div className="flex items-center justify-center gap-1">{viewBtn}{!isStaffLeader && deleteBtn}{toggleBtn}</div>;
        default: return null;
      }
    }

    if (isStaff) {
      if (creator === user?.name || creator === 'Nguyễn Văn B') {
        switch (status) {
          case 'Đã Duyệt':
          case 'Ẩn':
            return <div className="flex items-center justify-center gap-1">{viewBtn}</div>;
          case 'Chờ Duyệt':
          case 'Từ Chối':
            return <div className="flex items-center justify-center gap-1">{viewBtn}{editBtn}{deleteBtn}</div>;
          default:
            return null;
        }
      } else {
        if (status === 'Đã Duyệt' || status === 'Ẩn') {
          return <div className="flex items-center justify-center gap-1">{viewBtn}</div>;
        }
        return <div className="flex items-center justify-center gap-1">{viewBtn}</div>;
      }
    }

    if (isStudent && creator === user?.name) {
      switch (status) {
        case 'Đã Duyệt':
        case 'Ẩn':
          return <div className="flex items-center justify-center gap-1">{viewBtn}</div>;
        case 'Chờ Duyệt':
        case 'Từ Chối':
          return <div className="flex items-center justify-center gap-1">{viewBtn}{editBtn}{deleteBtn}</div>;
        default:
          return null;
      }
    }
    
    return null;
  };

  const totalPages = Math.ceil(filteredData.length / itemsPerPage);

  const currentItems = filteredData.slice((page - 1) * itemsPerPage, page * itemsPerPage);

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-[95%] mx-auto">
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Quản lý tin tức</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý tin tức</h1>
      </div>

      {/* Toolbar */}
      <div className="flex items-center flex-wrap gap-3 mb-6">
        <div className="relative flex-1 min-w-[200px] max-w-sm">
          <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
          <input 
            type="text" 
            placeholder="Tìm kiếm tin tức..." 
            className="w-full pl-10 pr-3 py-2 text-sm border border-gray-300 rounded-md focus:outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] text-gray-700 bg-white shadow-sm" 
          />
        </div>
        
        {isHO && (
          <select 
            value={selectedCampus}
            onChange={(e) => {
              setSelectedCampus(e.target.value);
              setPage(1);
            }}
            className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none"
          >
            <option value="">Tất cả cơ sở</option>
            <option value="Hà Nội">Hà Nội</option>
            <option value="Đà Nẵng">Đà Nẵng</option>
            <option value="Quy Nhơn">Quy Nhơn</option>
            <option value="Cần Thơ">Cần Thơ</option>
            <option value="Hồ Chí Minh">Hồ Chí Minh</option>
          </select>
        )}

        <select className="border border-gray-300 rounded-md px-3 py-2 text-sm focus:outline-none hover:border-[#004c91] hover:text-[#004c91] focus:border-[#004c91] text-gray-600 bg-white font-medium shadow-sm transition-colors cursor-pointer outline-none">
          <option value="">Tất cả trạng thái</option>
          <option value="Chờ Duyệt">Chờ Duyệt</option>
          <option value="Đã Duyệt">Đã Duyệt</option>
          <option value="Từ Chối">Từ Chối</option>
          <option value="Ẩn">Ẩn</option>
        </select>

        {!isHO && !isStaffLeader && (
          <button 
            onClick={() => navigate('/dashboard/news/create')}
            className="ml-auto bg-[#f37021] hover:bg-[#d9621a] text-white px-4 py-2 rounded-md font-bold flex items-center gap-1.5 transition-colors shadow-sm text-sm tracking-wide"
          >
            <Plus className="w-4 h-4 flex-shrink-0" />
            Thêm tin tức mới
          </button>
        )}
      </div>

      {/* Table */}
      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full border-collapse min-w-[1000px]">
            <thead>
              <tr className="bg-[#004c91] text-white text-[12px] tracking-wide uppercase text-center">
                <th className="p-3 font-bold w-[26%] text-left pl-6">TIÊU ĐỀ</th>
                <th className="p-3 font-bold w-[20%] text-left pl-6">MÔ TẢ</th>
                <th className="p-3 font-bold w-[90px]">ẢNH</th>
                <th className="p-3 font-bold w-[120px] whitespace-nowrap">NGƯỜI TẠO</th>
                <th 
                  className="p-3 font-bold w-[150px] whitespace-nowrap cursor-pointer hover:bg-[#003a70] bg-[#004c91] text-white transition-colors select-none group"
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
                <th className="p-3 font-bold w-[130px] whitespace-nowrap">TRẠNG THÁI</th>
                <th className="p-3 font-bold w-[140px]">HÀNH ĐỘNG</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-200">
              {currentItems.map(item => (
                <tr key={item.id} className="hover:bg-gray-50/80 transition-colors group text-center">
                  <td className="p-3 align-middle font-bold text-gray-800 text-[13px] text-left pl-6">
                    <div className="line-clamp-2 leading-relaxed">{item.title}</div>
                  </td>
                  <td className="p-3 align-middle text-gray-500 text-[12px] leading-relaxed text-left pl-6">
                    <div className="line-clamp-2">{item.desc}</div>
                  </td>
                  <td className="p-3 align-middle">
                    <div className="w-[72px] h-[50px] mx-auto rounded border border-gray-100 bg-white p-1 shadow-sm overflow-hidden flex items-center justify-center">
                      <img src={item.image} alt="" className="max-w-full max-h-full object-contain" />
                    </div>
                  </td>
                  <td className="p-3 align-middle whitespace-nowrap">
                    <div className="font-bold text-[#004c91] text-[13px]">{item.creator}</div>
                    {isHO && item.campus && (
                      <div className="text-[11px] text-gray-500 mt-0.5">{item.campus}</div>
                    )}
                  </td>
                  <td className="p-3 align-middle whitespace-nowrap">
                    <div className="font-medium text-gray-600 text-[13px]">{item.date}</div>
                  </td>
                  <td className="p-3 align-middle whitespace-nowrap">{getStatusBadge(item.status)}</td>
                  <td className="p-3 align-middle whitespace-nowrap">
                    {renderActions(item.id, item.status, item.creator)}
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
          <span>bài / trang</span>
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
    </div>
  );
}
