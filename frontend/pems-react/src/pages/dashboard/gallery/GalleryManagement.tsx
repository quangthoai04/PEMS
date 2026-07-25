/**
 * Trang GalleryManagement
 * Trung tâm điều khiển bộ sưu tập tĩnh và hệ đa phương tiện quảng bá trên trang công cộng.
 */

import React, { useState, useMemo } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Edit, Trash2, Search, X, Upload, CheckCircle2, ChevronRight, ChevronLeft, ChevronDown, ChevronUp, Plus, Image as ImageIcon, Film, Eye, MapPin } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import bgHN from "../../../assets/FPTbanner_visit/hola_new.jpg";
import { GalleryManagementStaffLeader } from './GalleryManagementStaffLeader';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';

// Defined Locations from Visit FPTU
const LOCATIONS: Record<string, string[]> = {
  'TỔNG QUAN': ['Toàn cảnh Hola Park', 'Toàn cảnh từ trên cao'],
  'TÒA ALPHA': ['Trước tòa nhà Alpha', 'Sảnh chính', 'Thư viện', 'Phòng học điển hình'],
  'TÒA BETA': ['Trước tòa nhà Beta', 'Sảnh chính', 'Hội trường'],
  'TÒA DELTA': [
    'Trước tòa nhà Delta',
    'Sảnh chính',
    'Thư viện',
    'Phòng học điển hình',
    'Trung tâm khởi nghiệp & nghiên cứu',
    'Phòng thí nghiệm đổi mới & sáng tạo SAP',
    'Phòng học Nhạc cụ dân tộc'
  ],
  'TÒA EPSILON': ['Trước tòa nhà Epsilon', 'Sảnh chính', 'Xưởng thực hành'],
  'TÒA GAMMA': ['Trước tòa nhà Gamma', 'Sảnh chính'],
  'KHU DỊCH VỤ': ['Nhà ăn sinh viên', 'Khu trung tâm dịch vụ', 'Siêu thị'],
  'KÝ TÚC XÁ': ['Khuôn viên KTX', 'Sảnh KTX', 'Phòng mẫu'],
  'KHU THỂ THAO': ['Sân bóng đá', 'Nhà thi đấu đa năng', 'Sân tập Vovinam', 'Khu street workout']
};

const MOCK_GALLERY = [
  { id: 1, title: "Sảnh chờ sinh viên - Tòa Beta", type: "Hình ảnh", views: 2540, status: "PUBLISHED", images: [bgHN, bgHN], date: "10/05/2026", description: "Khu vực sinh viên chờ và nghỉ ngơi giữa giờ học.", locationCategory: "TÒA BETA", locationDetail: "Sảnh chính" },
  { id: 2, title: "View đường chân trời - Delta", type: "Hình ảnh", views: 1205, status: "PUBLISHED", images: [bgHN], date: "11/05/2026", description: "Khu vực sân trước thoáng mát với bãi cỏ.", locationCategory: "TÒA DELTA", locationDetail: "Trước tòa nhà Delta" },
  { id: 3, title: "Sân bóng AI hiện đại", type: "Video", views: 840, status: "PUBLISHED", images: [bgHN], date: "15/05/2026", description: "Sân bóng tiêu chuẩn do AI hỗ trợ đo lường.", locationCategory: "KHU THỂ THAO", locationDetail: "Sân bóng đá" },
  { id: 4, title: "Hồ sen Tòa Alpha", type: "Hình ảnh", views: 1650, status: "HIDDEN", images: [bgHN], date: "16/05/2026", description: "Hồ sen check-in xịn xò", locationCategory: "TÒA ALPHA", locationDetail: "Trước tòa nhà Alpha" },
  { id: 5, title: "Phòng LAB ERP-SAP", type: "Hình ảnh", views: 3120, status: "PUBLISHED", images: [bgHN, bgHN], date: "18/05/2026", description: "Trang thiết bị đào tạo trực tiếp trên hệ thống SAP.", locationCategory: "TÒA DELTA", locationDetail: "Phòng thí nghiệm đổi mới & sáng tạo SAP" },
  { id: 6, title: "Thư viện 3 tầng cực lộng lẫy", type: "Video", views: 2100, status: "PUBLISHED", images: [bgHN], date: "20/05/2026", description: "Video tham quan thư viện đồ sộ bậc nhất.", locationCategory: "TÒA DELTA", locationDetail: "Thư viện" },
  { id: 7, title: "Phòng học chuẩn quốc tế", type: "Hình ảnh", views: 1450, status: "PUBLISHED", images: [bgHN], date: "22/05/2026", description: "Sĩ số 30 người, điều hòa mát rượi.", locationCategory: "TÒA DELTA", locationDetail: "Phòng học điển hình" },
  { id: 8, title: "Không gian thực hành Khởi nghiệp", type: "Hình ảnh", views: 980, status: "PUBLISHED", images: [bgHN], date: "25/05/2026", description: "Môi trường câm trải nghiệm startup.", locationCategory: "TÒA DELTA", locationDetail: "Trung tâm khởi nghiệp & nghiên cứu" },
  { id: 9, title: "Góc phòng đàn tỳ bà", type: "Hình ảnh", views: 1100, status: "PUBLISHED", images: [bgHN], date: "26/05/2026", description: "Môi trường gìn giữ văn hóa dân tộc.", locationCategory: "TÒA DELTA", locationDetail: "Phòng học Nhạc cụ dân tộc" },
  { id: 10, title: "Khuôn viên flycam xịn", type: "Video", views: 4200, status: "PUBLISHED", images: [bgHN], date: "28/05/2026", description: "Toàn cảnh campus siêu đẹp xanh biếc.", locationCategory: "TỔNG QUAN", locationDetail: "Toàn cảnh Hola Park" },
];

export function GalleryManagement() {
  const userStr = localStorage.getItem("currentUser");
  const currentUser = userStr ? JSON.parse(userStr) : null;
  const isStaffLeader = currentUser?.role?.toUpperCase() === 'STAFF' && currentUser?.subRole?.toUpperCase() === 'LEADER';

  // Staff Leader (sub_role LEADER) gets the real, server-driven gallery management (UC-GAL-01..07).
  // Other roles keep the legacy mock below untouched. Early return before any hooks keeps hook order stable.
  if (isStaffLeader) {
    return <GalleryManagementStaffLeader />;
  }

  return <GalleryManagementMock />;
}

function GalleryManagementMock() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem("currentUser");
  const currentUser = userStr ? JSON.parse(userStr) : null;
  const isStaffLeader = currentUser?.role?.toUpperCase() === 'STAFF' && currentUser?.subRole?.toUpperCase() === 'LEADER';

  const [galleryList, setGalleryList] = useState(MOCK_GALLERY);
  const [searchQuery, setSearchQuery] = useState('');
  
  // Modals state
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [isViewModalOpen, setIsViewModalOpen] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  
  const [selectedItem, setSelectedItem] = useState<any>(null);
  
  // Form state
  const [formData, setFormData] = useState<any>({
    title: '',
    type: 'Hình ảnh',
    status: 'PUBLISHED',
    description: '',
    locationCategory: 'TÒA DELTA',
    locationDetail: 'Trước tòa nhà Delta',
    images: [] as string[]
  });

  // Filters state
  const [filterCategory, setFilterCategory] = useState('');
  const [filterDetail, setFilterDetail] = useState('');
  const [filterType, setFilterType] = useState('');
  const [filterStatus, setFilterStatus] = useState('');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);
  
  const filteredGallery = useMemo(() => {
    let result = galleryList.filter(item => 
      (item.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
       item.locationCategory.toLowerCase().includes(searchQuery.toLowerCase()) ||
       item.locationDetail.toLowerCase().includes(searchQuery.toLowerCase())) &&
      (filterCategory ? item.locationCategory === filterCategory : true) &&
      (filterDetail ? item.locationDetail === filterDetail : true) &&
      (filterType ? item.type === filterType : true) &&
      (filterStatus ? item.status === filterStatus : true)
    );

    result.sort((a, b) => {
      const parseDate = (d: string) => {
        const [day, month, year] = d.split('/');
        return new Date(`${year}-${month}-${day}`).getTime();
      };
      return sortOrder === 'asc' ? parseDate(a.date) - parseDate(b.date) : parseDate(b.date) - parseDate(a.date);
    });

    return result;
  }, [galleryList, searchQuery, filterCategory, filterDetail, filterType, filterStatus, sortOrder]);

  const totalPages = Math.ceil(filteredGallery.length / pageSize);
  const paginatedGallery = filteredGallery.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const handleToggleStatus = (item: any) => {
    setGalleryList(prev => prev.map(g => g.id === item.id ? { ...g, status: g.status === 'PUBLISHED' ? 'HIDDEN' : 'PUBLISHED' } : g));
  };

  const getTypeIcon = (type: string) => {
    switch (type) {
      case 'Video': return <Film className="w-4 h-4 text-blue-500" />;
      default: return <ImageIcon className="w-4 h-4 text-orange-500" />;
    }
  };

  const handleOpenCreate = () => {
    setFormData({
      title: '',
      type: 'Hình ảnh',
      status: 'PUBLISHED',
      description: '',
      locationCategory: 'TÒA DELTA',
      locationDetail: LOCATIONS['TÒA DELTA'][0],
      images: []
    });
    setIsCreateModalOpen(true);
  };

  const handleOpenEdit = (item: any) => {
    setSelectedItem(item);
    setFormData({ ...item });
    setIsEditModalOpen(true);
  };

  const handleOpenView = (item: any) => {
    setSelectedItem(item);
    setIsViewModalOpen(true);
  };

  const handleOpenDelete = (item: any) => {
    setSelectedItem(item);
    setIsDeleteModalOpen(true);
  };

  const handleCreateSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const newItem = {
      ...formData,
      id: Date.now(),
      views: 0,
      date: formatVietnamDate(new Date())
    };
    if(newItem.images.length === 0) newItem.images = [bgHN]; // dummy fallback
    setGalleryList([newItem, ...galleryList]);
    setIsCreateModalOpen(false);
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setGalleryList(prev => prev.map(item => item.id === selectedItem.id ? { ...formData, id: item.id, views: item.views, date: item.date } : item));
    setIsEditModalOpen(false);
  };

  const handleDeleteSubmit = () => {
    setGalleryList(prev => prev.filter(item => item.id !== selectedItem.id));
    setIsDeleteModalOpen(false);
  };

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const files = Array.from(e.target.files);
      const newImages = files.map((file: File) => URL.createObjectURL(file));
      setFormData({
        ...formData,
        images: [...formData.images, ...newImages].slice(0, 5)
      });
    }
  };

  const removeFile = (idx: number) => {
    const newImages = [...formData.images];
    newImages.splice(idx, 1);
    setFormData({...formData, images: newImages});
  };

  return (
    <div className="w-full pb-12 animate-in fade-in duration-500 font-sans">
      {/* Breadcrumbs */}
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-2">
        <span>Dashboard</span>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý Gallery Hà Nội</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-4">
        <div>
          <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">VisitFPTU Gallery</h1>
        </div>
        <div className="flex items-center gap-3">

          {isStaffLeader && (
            <button
              onClick={() => navigate('/dashboard/gallery/locations')}
              className="flex items-center gap-2 bg-white hover:bg-slate-50 text-[#004c91] border border-[#004c91] px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all hover:shadow-md outline-none"
            >
              <MapPin className="w-5 h-5" /> Quản lý khu vực
            </button>
          )}

          <button
            onClick={handleOpenCreate}
            className="flex items-center gap-2 bg-[#f37021] hover:bg-[#e85c0d] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all hover:shadow-md outline-none"
          >
            <Plus className="w-5 h-5" /> Tải lên Media
          </button>
        </div>
      </div>

      {/* Toolbar / Search & Filters */}
      <div className="bg-[#004c91] rounded-t-2xl p-4 shadow-sm flex flex-col gap-4">
        <div className="flex flex-col xl:flex-row gap-4">
          <div className="relative w-full xl:w-80 shrink-0">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-slate-400" />
            <input 
              type="text"
              placeholder="Tìm kiếm tiêu đề, vị trí..."
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setCurrentPage(1);
              }}
              className="w-full pl-11 pr-4 py-2.5 rounded-xl border border-white/20 focus:border-white focus:ring-1 focus:ring-white outline-none text-sm transition-all font-medium bg-white/10 text-white placeholder:text-white/60"
            />
          </div>
          
          <div className="grid grid-cols-2 xl:grid-cols-4 gap-3 w-full">
            <div className="relative">
              <select 
                value={filterCategory}
                onChange={(e) => {
                  setFilterCategory(e.target.value);
                  setFilterDetail('');
                  setCurrentPage(1);
                }}
                className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-medium appearance-none"
              >
                <option value="" className="text-slate-800">Tất cả khu vực</option>
                {Object.keys(LOCATIONS).map(cat => <option key={cat} value={cat} className="text-slate-800">{cat}</option>)}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
            </div>

            <div className="relative">
              <select 
                value={filterDetail}
                onChange={(e) => {
                  setFilterDetail(e.target.value);
                  setCurrentPage(1);
                }}
                disabled={!filterCategory}
                className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-medium appearance-none disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <option value="" className="text-slate-800">Tất cả vị trí cụ thể</option>
                {(LOCATIONS[filterCategory] || []).map(det => <option key={det} value={det} className="text-slate-800">{det}</option>)}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
            </div>

            <div className="relative">
              <select 
                value={filterType}
                onChange={(e) => {
                  setFilterType(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-medium appearance-none"
              >
                <option value="" className="text-slate-800">Loại</option>
                <option value="Hình ảnh" className="text-slate-800">Hình ảnh</option>
                <option value="Video" className="text-slate-800">Video</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
            </div>

            <div className="relative">
              <select 
                value={filterStatus}
                onChange={(e) => {
                  setFilterStatus(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-medium appearance-none"
              >
                <option value="" className="text-slate-800">Trạng thái</option>
                <option value="PUBLISHED" className="text-slate-800">Hiển thị</option>
                <option value="HIDDEN" className="text-slate-800">Đang ẩn</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
            </div>
          </div>
        </div>
      </div>

      {/* Table */}
      <div className="bg-white rounded-b-2xl shadow-sm border border-slate-200 overflow-hidden text-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
              <tr>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">STT</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Khu vực</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Vị trí cụ thể</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap min-w-[200px]">Tiêu đề</th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Định dạng</th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Trạng thái</th>
                <th 
                  className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap cursor-pointer hover:bg-gray-50 transition-colors select-none group text-center"
                  onClick={() => setSortOrder(prev => prev === 'asc' ? 'desc' : 'asc')}
                >
                  <div className="flex items-center justify-center gap-1.5">
                    Ngày tạo
                    <div className="flex flex-col text-gray-300 group-hover:text-[#004c91] transition-colors">
                      <ChevronUp className={`w-2.5 h-2.5 -mb-0.5 ${sortOrder === 'asc' ? 'text-[#004c91]' : ''}`} />
                      <ChevronDown className={`w-2.5 h-2.5 -mt-0.5 ${sortOrder === 'desc' ? 'text-[#004c91]' : ''}`} />
                    </div>
                  </div>
                </th>
                <th className="p-4 text-[11px] font-black text-center uppercase tracking-widest whitespace-nowrap">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {paginatedGallery.length > 0 ? paginatedGallery.map((item, index) => (
                  <tr 
                    key={item.id}
                    className="hover:bg-blue-50/50 transition-colors group"
                  >
                    <td className="p-4 font-bold text-slate-500 text-center whitespace-nowrap">
                      {(currentPage - 1) * pageSize + index + 1}
                    </td>
                    <td className="p-4 font-semibold text-slate-800 whitespace-nowrap">
                      {item.locationCategory}
                    </td>
                    <td className="p-4 font-medium text-slate-600 whitespace-nowrap">
                      {item.locationDetail}
                    </td>
                    <td className="p-4">
                      <div className="font-bold text-[#004c91] group-hover:text-blue-800 transition-colors line-clamp-1 max-w-[250px]">{item.title}</div>
                    </td>
                    <td className="p-4 text-center">
                      <div className="flex items-center justify-center gap-1.5 text-[11px] font-bold text-slate-700 bg-slate-100 px-2.5 py-1.5 rounded-lg w-max mx-auto">
                        {getTypeIcon(item.type)} {item.type}
                      </div>
                    </td>
                    <td className="p-4 text-center">
                      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold ${
                        item.status === 'PUBLISHED' 
                          ? 'bg-green-100 text-green-700 border border-green-200' 
                          : 'bg-slate-100 text-slate-700 border border-slate-200'
                      }`}>
                        {item.status === 'PUBLISHED' ? 'Hiển thị' : 'Đang ẩn'}
                      </span>
                    </td>
                    <td className="p-4 text-slate-600 font-medium whitespace-nowrap text-center">
                      {item.date}
                    </td>
                    <td className="p-4">
                      <div className="flex items-center justify-center gap-2">
                        <button 
                          onClick={() => handleOpenView(item)}
                          className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-[#004c91] hover:bg-blue-50 flex items-center justify-center transition-colors outline-none"
                          title="Xem chi tiết"
                        >
                          <Eye className="w-4 h-4" />
                        </button>
                        <button 
                          onClick={() => handleToggleStatus(item)}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center justify-center rounded-full transition-colors duration-200 ease-in-out outline-none ${item.status === 'PUBLISHED' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
                          title={item.status === 'PUBLISHED' ? 'Ẩn' : 'Hiện'}
                        >
                          <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${item.status === 'PUBLISHED' ? 'translate-x-2' : '-translate-x-2'}`} />
                        </button>
                      </div>
                    </td>
                  </tr>
                )) : (
                  <tr className="bg-slate-50/50">
                    <td colSpan={8} className="px-6 py-16 text-center text-slate-500">
                      <ImageIcon className="w-12 h-12 text-slate-300 mx-auto mb-3" />
                      <p className="font-medium text-slate-600 mb-1">Không tìm thấy tài nguyên nào</p>
                      <p className="text-xs">Vui lòng thử từ khoá tìm kiếm hoặc đổi bộ lọc.</p>
                    </td>
                  </tr>
                )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {filteredGallery.length > 0 && (
        <div className="p-6 border-t border-gray-100 flex items-center justify-between bg-gray-50/50">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-gray-500">Hiển thị</span>
            <div className="relative">
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setCurrentPage(1);
                }}
                className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-bold text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 appearance-none min-w-[70px] text-left"
              >
                <option value={5}>5</option>
                <option value={10}>10</option>
                <option value={20}>20</option>
                <option value={50}>50</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-gray-500 pointer-events-none" />
            </div>
            <span className="text-sm font-medium text-gray-500">bản ghi / trang</span>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
              disabled={currentPage === 1}
              className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <div className="flex items-center gap-1">
              {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                <button
                  key={page}
                  onClick={() => setCurrentPage(page)}
                  className={`w-8 h-8 rounded-lg text-sm font-bold transition-all outline-none ${currentPage === page ? 'bg-[#004c91] text-white shadow-sm' : 'text-gray-600 hover:bg-gray-200'}`}
                >
                  {page}
                </button>
              ))}
            </div>
            <button
              onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
              disabled={currentPage === totalPages}
              className="p-2 rounded-lg border border-gray-200 text-gray-500 hover:text-[#004c91] hover:border-[#004c91] hover:bg-blue-50 transition-all disabled:opacity-50 disabled:cursor-not-allowed outline-none"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
        )}
      </div>

      {/* View Modal */}
      <AnimatePresence>
        {isViewModalOpen && selectedItem && (
          <div className="fixed inset-0 z-50 flex items-center justify-center overflow-auto bg-black/60 backdrop-blur-sm p-4 font-sans">
            <motion.div
              initial={{ opacity: 0, scale: 0.95, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 20 }}
              className="bg-white rounded-3xl w-full max-w-4xl overflow-hidden shadow-2xl relative flex flex-col md:flex-row"
            >
              <button 
                onClick={() => setIsViewModalOpen(false)}
                className="absolute top-4 right-4 w-8 h-8 bg-black/20 hover:bg-black/40 text-white rounded-full flex items-center justify-center transition-colors z-10 backdrop-blur-md"
              >
                <X className="w-4 h-4" />
              </button>
              
              <div className="w-full md:w-1/2 p-6 bg-slate-100 flex flex-col gap-4">
                <div className="flex-1 rounded-2xl overflow-hidden border border-slate-200 bg-white relative">
                  <img src={selectedItem.images[0]} alt={selectedItem.title} className="w-full h-full object-cover" />
                  <div className="absolute top-4 left-4 bg-white/90 backdrop-blur-sm px-3 py-1 rounded-lg text-xs font-bold text-[#004c91] flex items-center gap-1.5">
                    {getTypeIcon(selectedItem.type)} {selectedItem.type}
                  </div>
                </div>
                <div className="flex gap-2 overflow-x-auto pb-1 scrollbar-hide">
                  {selectedItem.images.map((img: string, i: number) => (
                    <div key={i} className="w-16 h-16 rounded-xl overflow-hidden border-2 border-[#004c91] shrink-0 opacity-100 opacity-90 transition-opacity">
                      <img src={img} className="w-full h-full object-cover" />
                    </div>
                  ))}
                </div>
              </div>

              <div className="w-full md:w-1/2 p-8 flex flex-col justify-center">
                <div className="flex items-center gap-2 mb-4">
                  <span className="inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold bg-green-100 text-green-700 border border-green-200">
                    {selectedItem.status === 'PUBLISHED' ? 'Hiển thị' : 'Đang ẩn'}
                  </span>
                  <span className="inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold bg-green-100 text-green-700 border border-green-200">
                    Hà Nội
                  </span>
                </div>

                <h3 className="text-2xl font-black text-gray-900 mb-2 leading-tight">{selectedItem.title}</h3>
                <div className="flex items-center gap-1.5 text-sm font-semibold text-slate-600 mb-4 border-b border-slate-100 pb-4">
                   <span className="text-[#004c91]">{selectedItem.locationCategory}</span> <ChevronRight className="w-4 h-4 text-slate-300" /> <span>{selectedItem.locationDetail}</span>
                </div>
                <p className="text-sm text-slate-500 mb-6 leading-relaxed">{selectedItem.description || 'Chưa có mô tả'}</p>
                
                <div className="bg-slate-50 rounded-xl p-4 border border-slate-100 flex items-center gap-4">
                  <div>
                    <span className="block text-[10px] font-bold text-slate-400 uppercase mb-1">Ngày tải lên</span>
                    <span className="text-sm font-bold text-gray-800">{selectedItem.date}</span>
                  </div>
                </div>

                <div className="mt-8">
                  <button 
                    onClick={() => { setIsViewModalOpen(false); handleOpenEdit(selectedItem); }}
                    className="w-full bg-[#004c91] text-white py-2.5 rounded-xl font-bold flex items-center justify-center hover:bg-[#00386b] transition-colors"
                  >
                    Chỉnh sửa
                  </button>
                </div>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Create / Edit Modal */}
      <AnimatePresence>
        {(isCreateModalOpen || isEditModalOpen) && (
          <div className="fixed inset-0 z-50 flex items-center justify-center overflow-auto bg-black/60 backdrop-blur-sm p-4 font-sans">
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
              className="bg-white rounded-3xl w-full max-w-4xl overflow-hidden shadow-2xl relative focus:outline-none flex flex-col md:flex-row"
            >
               {/* Header for Mobile only */}
               <div className="px-6 py-4 flex md:hidden items-center justify-between border-b border-slate-100 bg-slate-50">
                <h3 className="text-lg font-black text-[#004c91]">
                  {isEditModalOpen ? "Chỉnh sửa Media" : "Tải lên tài nguyên"}
                </h3>
                <button onClick={() => isEditModalOpen ? setIsEditModalOpen(false) : setIsCreateModalOpen(false)} className="w-8 h-8 rounded-full flex items-center justify-center text-slate-500 bg-white shadow-sm border border-slate-200">
                  <X className="w-4 h-4" />
                </button>
               </div>

              {/* Layout for form: Left side for upload, right side for fields */}
              <div className="w-full md:w-5/12 p-6 bg-slate-50 border-r border-slate-200 flex flex-col">
                 <div className="hidden md:flex justify-between items-center mb-6">
                    <h3 className="text-xl font-black text-[#004c91]">
                      {isEditModalOpen ? "Sửa File" : "Upload File"}
                    </h3>
                 </div>

                  <label 
                    className="flex-1 border-2 border-dashed border-[#004c91]/30 rounded-2xl p-6 flex flex-col items-center justify-center bg-white hover:bg-blue-50/50 hover:border-[#004c91]/50 transition-colors cursor-pointer group mb-4 min-h-[200px]"
                  >
                    <div className="w-16 h-16 bg-[#ebf5ff] text-[#004c91] rounded-2xl flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                      <Upload className="w-6 h-6" />
                    </div>
                    <p className="text-sm font-bold text-gray-700 text-center">Click để chọn files</p>
                    <p className="text-xs text-slate-400 mt-1 text-center">Hỗ trợ JPG, PNG, MP4. (Tối đa 5 file, mỗi file max 50MB)</p>
                    <input type="file" multiple accept="image/*,video/mp4" className="hidden" onChange={handleFileUpload} />
                  </label>

                  <div className="space-y-3">
                     <p className="text-xs font-bold text-slate-500 uppercase tracking-wide">Files đã chọn ({formData.images.length}/5)</p>
                     {formData.images.map((img: string, idx: number) => (
                        <div key={idx} className="flex items-center justify-between p-2 rounded-xl bg-white border border-slate-200 shadow-sm">
                           <div className="flex items-center gap-3">
                              <div className="w-10 h-10 rounded-lg overflow-hidden shrink-0 bg-slate-100">
                                <img src={img} className="w-full h-full object-cover" />
                              </div>
                              <div>
                                <p className="text-xs font-bold text-gray-700">media_file_{idx+1}.jpg</p>
                                <p className="text-[10px] text-green-600 font-semibold mt-0.5 flex items-center gap-1"><CheckCircle2 className="w-3 h-3" /> Đã tải lên</p>
                              </div>
                           </div>
                           <button type="button" onClick={() => removeFile(idx)} className="p-2 text-slate-400 hover:text-red-500 hover:bg-red-50 rounded-lg transition-colors">
                              <Trash2 className="w-4 h-4" />
                           </button>
                        </div>
                     ))}
                  </div>
              </div>

              <div className="w-full md:w-7/12 p-6 md:p-8 flex flex-col">
                <div className="hidden md:flex justify-between items-center mb-6 border-b border-slate-100 pb-4">
                    <h3 className="text-xl font-black text-gray-800">
                      Thông tin chi tiết
                    </h3>
                    <button onClick={() => isEditModalOpen ? setIsEditModalOpen(false) : setIsCreateModalOpen(false)} className="w-8 h-8 rounded-full flex items-center justify-center text-slate-500 bg-white hover:text-red-500 hover:bg-red-50 transition-colors border border-slate-200 shadow-sm">
                      <X className="w-4 h-4" />
                    </button>
                 </div>

                <form onSubmit={isEditModalOpen ? handleEditSubmit : handleCreateSubmit} className="space-y-5 flex-1 flex flex-col">
                  
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Tiêu đề <span className="text-red-500">*</span></label>
                    <input 
                      type="text" 
                      required
                      value={formData.title}
                      onChange={(e) => setFormData({...formData, title: e.target.value})}
                      className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all"
                      placeholder="Nhập tiêu đề..."
                    />
                  </div>

                  <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                    <div className="space-y-1.5">
                      <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Danh mục Tòa/Khu <span className="text-red-500">*</span></label>
                      <select 
                        value={formData.locationCategory}
                        onChange={(e) => {
                          const newCat = e.target.value;
                          setFormData({
                            ...formData, 
                            locationCategory: newCat, 
                            locationDetail: LOCATIONS[newCat]?.[0] || ''
                          })
                        }}
                        className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all appearance-none bg-slate-50 focus:bg-white"
                      >
                        {Object.keys(LOCATIONS).map(cat => (
                          <option key={cat} value={cat}>{cat}</option>
                        ))}
                      </select>
                    </div>

                    <div className="space-y-1.5">
                      <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Vị trí thực tế <span className="text-red-500">*</span></label>
                      <select 
                        value={formData.locationDetail}
                        onChange={(e) => setFormData({...formData, locationDetail: e.target.value})}
                        className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all appearance-none bg-slate-50 focus:bg-white"
                      >
                        {(LOCATIONS[formData.locationCategory] || []).map(det => (
                          <option key={det} value={det}>{det}</option>
                        ))}
                      </select>
                    </div>

                    <div className="space-y-1.5 md:col-span-2">
                      <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Định dạng <span className="text-red-500">*</span></label>
                      <select 
                        value={formData.type}
                        onChange={(e) => setFormData({...formData, type: e.target.value})}
                        className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all appearance-none bg-slate-50 focus:bg-white"
                      >
                        <option value="Hình ảnh">Hình ảnh</option>
                        <option value="Video">Video</option>
                      </select>
                    </div>
                  </div>

                  <div className="space-y-1.5 flex-1">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Mô tả (Không bắt buộc)</label>
                    <textarea 
                      rows={3}
                      value={formData.description}
                      onChange={(e) => setFormData({...formData, description: e.target.value})}
                      className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-medium transition-all resize-none"
                      placeholder="Nhập mô tả về tài nguyên..."
                    ></textarea>
                  </div>

                  <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100 mt-auto">
                    <button 
                      type="button" 
                      className="px-6 py-2.5 rounded-xl font-bold text-slate-500 bg-slate-100 hover:bg-slate-200 transition-colors"
                      onClick={() => isEditModalOpen ? setIsEditModalOpen(false) : setIsCreateModalOpen(false)}
                    >
                      Hủy bỏ
                    </button>
                    <button 
                      type="submit"
                      className="px-6 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] transition-colors shadow-sm"
                    >
                      {isEditModalOpen ? "Lưu thay đổi" : "Hoàn tất Upload"}
                    </button>
                  </div>
                </form>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Delete Confirmation Modal */}
      <AnimatePresence>
        {isDeleteModalOpen && selectedItem && (
          <div className="fixed inset-0 z-50 flex items-center justify-center overflow-auto bg-black/60 backdrop-blur-sm p-4 font-sans">
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
              className="bg-white rounded-3xl w-full max-w-sm overflow-hidden shadow-2xl relative text-center p-8"
            >
              <div className="w-16 h-16 bg-red-100 text-red-500 rounded-full flex items-center justify-center mx-auto mb-4">
                <Trash2 className="w-8 h-8" />
              </div>
              <h3 className="text-xl font-black text-gray-900 mb-2">Xóa thư viện này?</h3>
              <p className="text-sm font-medium text-slate-500 mb-8">
                Bạn có chắc chắn muốn xóa <span className="font-bold text-gray-800">"{selectedItem.title}"</span>? Quá trình này sẽ xóa tất cả các hình ảnh liên quan.
              </p>
              
              <div className="flex gap-3">
                <button 
                  onClick={() => setIsDeleteModalOpen(false)}
                  className="flex-1 py-3 rounded-xl font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 transition-colors"
                >
                  Hủy
                </button>
                <button 
                  onClick={handleDeleteSubmit}
                  className="flex-1 py-3 rounded-xl font-bold text-white bg-red-500 hover:bg-red-600 transition-colors shadow-sm shadow-red-500/30"
                >
                  Xóa
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

    </div>
  );
}
