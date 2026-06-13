/**
 * Trang CampusManagement
 * Liệt kê và thay đổi trạng thái theo dõi của các địa điểm lớn (Campus) trong trường.
 */

import React, { useState } from 'react';
import { Search, Plus, Eye, ChevronLeft, ChevronRight, X, Building2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';

const mockCampuses = [
  { id: 1, name: 'FPT University Hà Nội', location: 'Hà Nội', base: 'Hà Nội', status: 'Hoạt động', ic_head: 'Nguyễn Văn A' },
  { id: 2, name: 'FPT University HCM', location: 'TP. Hồ Chí Minh', base: 'Hồ Chí Minh', status: 'Hoạt động', ic_head: 'Trần Thị B' },
  { id: 3, name: 'FPT University Đà Nẵng', location: 'Đà Nẵng', base: 'Đà Nẵng', status: 'Hoạt động', ic_head: 'Lê Văn C' },
  { id: 4, name: 'FPT University Cần Thơ', location: 'Cần Thơ', base: 'Cần Thơ', status: 'Hoạt động', ic_head: 'Phạm Thị D' },
  { id: 5, name: 'FPT University Quy Nhơn', location: 'Bình Định', base: 'Quy Nhơn', status: 'Ngừng hoạt động', ic_head: 'Hoàng Văn E' },
];

const provinces = [
  'Hà Nội', 'TP. Hồ Chí Minh', 'Đà Nẵng', 'Hải Phòng', 'Cần Thơ', 'Huế',
  'An Giang', 'Bắc Giang', 'Bắc Ninh', 'Bến Tre', 'Bình Dương', 'Bình Định',
  'Bình Thuận', 'Cà Mau', 'Đắk Lắk', 'Đồng Nai', 'Đồng Tháp', 'Gia Lai',
  'Hà Giang', 'Hà Nam', 'Hà Tĩnh', 'Hải Dương', 'Hưng Yên', 'Khánh Hòa',
  'Kiên Giang', 'Lâm Đồng', 'Lạng Sơn', 'Lào Cai', 'Long An', 'Nam Định',
  'Nghệ An', 'Ninh Bình', 'Phú Thọ', 'Quảng Ninh'
];

export function CampusManagement() {
  const navigate = useNavigate();
  const [data, setData] = useState(mockCampuses);
  const [page, setPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(5);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedBase, setSelectedBase] = useState('');
  const [selectedStatus, setSelectedStatus] = useState('');
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [newCampus, setNewCampus] = useState({ name: '', location: provinces[0], ic_head: '' });

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();

  if (userRole !== 'HO') {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500">Trang này chỉ dành cho tài khoản HO.</p>
        </div>
      </div>
    );
  }

  const toggleVisibility = (id: number) => {
    setData(data.map(item => {
      if (item.id === id) {
        return { ...item, status: item.status === 'Hoạt động' ? 'Ngừng hoạt động' : 'Hoạt động' };
      }
      return item;
    }));
  };

  const filteredData = data.filter(item => {
    const matchSearch = item.name.toLowerCase().includes(searchQuery.toLowerCase()) || 
                        item.location.toLowerCase().includes(searchQuery.toLowerCase());
    const matchBase = selectedBase === '' || item.base === selectedBase;
    const matchStatus = selectedStatus === '' || item.status === selectedStatus;
    return matchSearch && matchBase && matchStatus;
  });

  const totalPages = Math.ceil(filteredData.length / itemsPerPage);
  const currentData = filteredData.slice((page - 1) * itemsPerPage, page * itemsPerPage);

  return (
    <div className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen">
      {/* Breadcrumb & Tiêu đề */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Quản lý campus</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6 text-left flex justify-start">
        <h1 className="text-3xl font-bold text-[#004c91]">Quản lý campus</h1>
      </div>

      {/* Filters */}
      <div className="flex flex-col md:flex-row gap-4 items-center justify-between relative z-20">
        <div className="flex flex-col md:flex-row items-center gap-4 w-full flex-1">
          <div className="relative w-full md:max-w-[400px]">
            <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
            <input 
              type="text" 
              placeholder="Tìm kiếm campus..." 
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full pl-10 pr-4 py-2.5 bg-white border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all shadow-sm"
            />
          </div>
          
          <div className="flex items-center gap-3 w-full md:w-auto">
            <select 
              value={selectedBase}
              onChange={(e) => setSelectedBase(e.target.value)}
              className="w-full md:w-[150px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
            >
              <option value="">Tất cả cơ sở</option>
              <option value="Hà Nội">Hà Nội</option>
              <option value="Hồ Chí Minh">Hồ Chí Minh</option>
              <option value="Đà Nẵng">Đà Nẵng</option>
              <option value="Cần Thơ">Cần Thơ</option>
              <option value="Quy Nhơn">Quy Nhơn</option>
            </select>

            <select 
              value={selectedStatus}
              onChange={(e) => setSelectedStatus(e.target.value)}
              className="w-full md:w-[200px] px-3 py-2.5 bg-white border border-gray-200 rounded-xl text-sm font-medium focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-600 shadow-sm"
            >
              <option value="">Tất cả trạng thái</option>
              <option value="Hoạt động">Hoạt động</option>
              <option value="Ngừng hoạt động">Ngừng hoạt động</option>
            </select>
          </div>
        </div>

        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="flex items-center justify-center gap-2 bg-[#f37021] hover:bg-[#e06218] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all shadow-[#f37021]/20 hover:shadow-[#f37021]/40 shrink-0"
        >
           <Plus className="w-4 h-4" /> Thêm mới campus
        </button>
      </div>

      {/* Table */}
      <div className="bg-white border rounded-2xl shadow-sm border-gray-100 relative z-10 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[700px]">
            <thead className="bg-[#004c91] text-white">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-16 text-center">STT</th>
                <th className="px-4 py-3 text-left text-xs font-bold uppercase tracking-wider w-[35%]">Tên Campus</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Cơ sở</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[20%]">Trưởng phòng IC</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-[15%]">Trạng thái</th>
                <th className="px-4 py-3 text-center text-xs font-bold uppercase tracking-wider w-24">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {currentData.map((item, index) => (
                <tr key={item.id} className="hover:bg-gray-50/50 transition-colors">
                  <td className="px-4 py-3 text-sm text-gray-500 font-medium text-center">
                    {(page - 1) * itemsPerPage + index + 1}
                  </td>
                  <td className="px-4 py-3">
                    <div className="text-sm font-bold text-gray-900">{item.name}</div>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <div className="text-sm text-gray-600 font-medium">{item.base}</div>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <div className="text-sm font-bold text-[#004c91]">{item.ic_head}</div>
                  </td>
                  <td className="px-4 py-3 text-center">
                    <span className={`inline-flex px-3 py-1 text-xs font-bold rounded-full ${
                      item.status === 'Hoạt động' ? 'bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]' : 'bg-gray-100 text-gray-600 border border-gray-200'
                    }`}>
                      {item.status}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-1 justify-center">
                      <button 
                        onClick={() => navigate(`/dashboard/campus/${item.id}`)}
                        className="p-1.5 rounded-lg transition-colors hover:bg-[#e6eff7] hover:text-[#004c91] text-gray-400" 
                        title="Xem chi tiết"
                      >
                        <Eye className="w-[16px] h-[16px]" />
                      </button>
                      <button 
                        onClick={() => toggleVisibility(item.id)}
                        className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-full border-2 border-transparent transition-colors duration-200 ease-in-out focus:outline-none ml-2 ${item.status === 'Hoạt động' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
                        title={item.status === 'Hoạt động' ? 'Ngừng hoạt động' : 'Hoạt động'}
                      >
                        <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${item.status === 'Hoạt động' ? 'translate-x-4' : 'translate-x-0'}`} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
              {currentData.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-12 text-center text-gray-500 font-medium">
                    Không tìm thấy campus nào
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
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
               disabled={page === totalPages}
               onClick={() => setPage(page + 1)}
             >
               <ChevronRight className="w-5 h-5" />
             </button>
          </div>
        </div>
      </div>

      {/* Create Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm p-4">
          <div className="bg-white rounded-2xl shadow-xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in-95 duration-200">
            <div className="p-5 border-b border-gray-100 flex items-center justify-between">
              <h3 className="text-xl font-bold text-[#004c91] flex items-center gap-2">
                <Building2 className="w-5 h-5" /> 
                Thêm mới campus
              </h3>
              <button onClick={() => setIsCreateModalOpen(false)} className="p-1.5 text-gray-400 hover:text-gray-600 transition-colors bg-gray-50 hover:bg-gray-100 rounded-lg">
                <X className="w-5 h-5" />
              </button>
            </div>
            
            <div className="p-6 space-y-5">
              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Tên campus<span className="text-red-500 ml-1">*</span></label>
                <input 
                  type="text"
                  className="w-full px-3 py-2 border border-gray-200 rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all"
                  placeholder="Nhập tên campus..."
                  value={newCampus.name}
                  onChange={(e) => setNewCampus({...newCampus, name: e.target.value})}
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Chọn vị trí<span className="text-red-500 ml-1">*</span></label>
                <select 
                  className="w-full px-3 py-2 border border-blue-500/50 bg-[#004c91] text-white rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/50 transition-all font-medium max-h-48 overflow-y-auto"
                  value={newCampus.location}
                  onChange={(e) => setNewCampus({...newCampus, location: e.target.value})}
                  size={1}
                >
                  {provinces.map((prov) => (
                    <option key={prov} value={prov} className="bg-white text-gray-900">{prov}</option>
                  ))}
                </select>
              </div>

              <div className="space-y-1.5">
                <label className="text-sm font-bold text-gray-900 block">Chọn trưởng phòng IC<span className="text-red-500 ml-1">*</span></label>
                <select 
                  className="w-full px-3 py-2 border border-blue-500/50 bg-[#004c91] text-white rounded-xl text-sm focus:outline-none focus:ring-2 focus:ring-[#004c91]/50 transition-all font-medium"
                  value={newCampus.ic_head}
                  onChange={(e) => setNewCampus({...newCampus, ic_head: e.target.value})}
                >
                  <option value="" className="bg-white text-gray-900 opacity-50">-- Chọn trưởng phòng --</option>
                  <option value="Nguyễn Văn A" className="bg-white text-gray-900">Nguyễn Văn A</option>
                  <option value="Trần Thị B" className="bg-white text-gray-900">Trần Thị B</option>
                  <option value="Lê Văn C" className="bg-white text-gray-900">Lê Văn C</option>
                  <option value="Phạm Thị D" className="bg-white text-gray-900">Phạm Thị D</option>
                  <option value="Hoàng Văn E" className="bg-white text-gray-900">Hoàng Văn E</option>
                </select>
              </div>
            </div>

            <div className="p-5 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3">
              <button 
                onClick={() => setIsCreateModalOpen(false)}
                className="px-5 py-2 bg-white border border-gray-200 text-gray-600 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm"
              >
                Hủy
              </button>
              <button 
                onClick={() => {
                  if (newCampus.name && newCampus.location && newCampus.ic_head) {
                    const newId = data.length > 0 ? Math.max(...data.map(d => d.id)) + 1 : 1;
                    setData([{ 
                      id: newId, 
                      status: 'Hoạt động', 
                      name: newCampus.name, 
                      location: newCampus.location,
                      base: ['Hà Nội', 'Hồ Chí Minh', 'Đà Nẵng', 'Cần Thơ'].includes(newCampus.location) ? newCampus.location : 'Hà Nội', // Dummmy base assignment
                      ic_head: newCampus.ic_head
                    }, ...data]);
                    setIsCreateModalOpen(false);
                    setNewCampus({ name: '', location: provinces[0], ic_head: '' });
                  }
                }}
                className="px-5 py-2 bg-[#f37021] text-white font-bold rounded-xl hover:bg-[#e85c0d] transition-colors shadow-sm cursor-pointer"
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
