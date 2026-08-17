/**
 * Trang LocationManagement
 * Quản lý định vị, thêm địa điểm cơ sở vi mô như phòng học, phòng ban thư viện.
 */

import React, { useState, useMemo } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { Plus, Edit, Trash2, Search, X, ChevronRight, ChevronLeft, ChevronDown, ChevronUp, ArrowLeft } from 'lucide-react';
import { Link, useNavigate } from 'react-router-dom';
import { LocationManagementStaffLeader } from './LocationManagementStaffLeader';
import { formatVietnamDate } from '../../../shared/utils/vietnamTime';

const MOCK_LOCATIONS = [
  { id: 1, campus: 'Hà Nội', category: 'TÒA DELTA', detail: 'Sảnh chính', status: 'Hoạt động', date: '01/05/2026' },
  { id: 2, campus: 'Hà Nội', category: 'TÒA DELTA', detail: 'Thư viện', status: 'Hoạt động', date: '02/05/2026' },
  { id: 3, campus: 'Hà Nội', category: 'TÒA DELTA', detail: 'Trước tòa nhà Delta', status: 'Hoạt động', date: '03/05/2026' },
  { id: 4, campus: 'Hà Nội', category: 'TÒA ALPHA', detail: 'Trước tòa nhà Alpha', status: 'Hoạt động', date: '04/05/2026' },
  { id: 5, campus: 'Hà Nội', category: 'TÒA ALPHA', detail: 'Hồ sen', status: 'Ngừng hoạt động', date: '05/05/2026' },
];

export function LocationManagement() {
  const userStr = localStorage.getItem('currentUser');
  const currentUser = userStr ? JSON.parse(userStr) : null;
  const isStaffLeader = currentUser?.role?.toUpperCase() === 'STAFF' && currentUser?.subRole?.toUpperCase() === 'LEADER';

  // Staff Leader (sub_role LEADER) gets the real, server-driven "Quản lý khu vực" (UC-LOC-01..09).
  // Other roles keep the legacy mock below untouched. Early return before any hooks keeps hook order stable.
  if (isStaffLeader) {
    return <LocationManagementStaffLeader />;
  }

  return <LocationManagementMock />;
}

function LocationManagementMock() {
  const navigate = useNavigate();
  const [locationList, setLocationList] = useState(MOCK_LOCATIONS);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortOrder, setSortOrder] = useState<'desc' | 'asc'>('desc');
  const [filterCategory, setFilterCategory] = useState('');
  const [filterStatus, setFilterStatus] = useState('');

  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(5);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isEditMode, setIsEditMode] = useState(false);
  const [selectedItem, setSelectedItem] = useState<any>(null);

  const [formData, setFormData] = useState({
    campus: 'Hà Nội',
    category: '',
    detail: '',
    status: 'Hoạt động'
  });

  const [categoryType, setCategoryType] = useState<'existing' | 'new'>('existing');

  const uniqueCategories = useMemo(() => {
    const cats = locationList.map(loc => loc.category);
    return Array.from(new Set(cats));
  }, [locationList]);

  const filteredLocations = useMemo(() => {
    let result = locationList.filter(item => 
      (item.category.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.detail.toLowerCase().includes(searchQuery.toLowerCase()) ||
      item.campus.toLowerCase().includes(searchQuery.toLowerCase())) &&
      (filterCategory ? item.category === filterCategory : true) &&
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
  }, [locationList, searchQuery, sortOrder, filterCategory, filterStatus]);

  const totalPages = Math.ceil(filteredLocations.length / pageSize);
  const paginatedLocations = filteredLocations.slice((currentPage - 1) * pageSize, currentPage * pageSize);

  const handleToggleStatus = (item: any) => {
    setLocationList(prev => prev.map(loc => loc.id === item.id ? { ...loc, status: loc.status === 'Hoạt động' ? 'Ngừng hoạt động' : 'Hoạt động' } : loc));
  };

  const handleOpenCreate = () => {
    setFormData({ campus: 'Hà Nội', category: uniqueCategories[0] || '', detail: '', status: 'Hoạt động' });
    setCategoryType('existing');
    setIsEditMode(false);
    setIsModalOpen(true);
  };

  const handleOpenEdit = (item: any) => {
    setSelectedItem(item);
    setFormData({ ...item });
    setCategoryType('existing'); // assuming edited item usually is existing unless it's complex logic
    setIsEditMode(true);
    setIsModalOpen(true);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (isEditMode) {
      setLocationList(prev => prev.map(loc => loc.id === selectedItem.id ? { ...formData, id: loc.id, date: loc.date } : loc));
    } else {
      setLocationList([{ ...formData, id: Date.now(), date: formatVietnamDate(new Date()) }, ...locationList]);
    }
    setIsModalOpen(false);
  };

  return (
    <div className="w-full pb-12 animate-in fade-in duration-500 font-sans">
      <div className="flex items-center gap-2 text-sm font-medium text-slate-500 mb-6">
        <span>Dashboard</span>
        <span>/</span>
        <Link to="/dashboard/gallery" className="hover:text-[#004c91] transition-colors">Quản lý Gallery</Link>
        <span>/</span>
        <span className="text-[#004c91] font-bold">Quản lý khu vực</span>
      </div>

      <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-8">
        <div className="flex items-center gap-4">
          <button 
            onClick={() => navigate(-1)}
            className="w-10 h-10 rounded-full bg-slate-100 flex items-center justify-center text-slate-500 hover:bg-[#004c91] hover:text-white transition-colors"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <div>
            <h1 className="text-3xl font-bold text-[#004c91]">Quản lý khu vực</h1>
            <p className="text-gray-500 mt-1 font-medium">Danh sách các tòa và khu vực cụ thể</p>
          </div>
        </div>
        <button 
          onClick={handleOpenCreate}
          className="flex items-center gap-2 bg-[#f37021] hover:bg-[#e85c0d] text-white px-5 py-2.5 rounded-xl text-sm font-bold shadow-sm transition-all hover:shadow-md outline-none"
        >
          <Plus className="w-5 h-5" /> Thêm khu vực mới
        </button>
      </div>

      <div className="bg-[#004c91] rounded-t-2xl p-4 shadow-sm flex flex-col gap-4">
        <div className="flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="relative w-full md:w-80 shrink-0">
            <Search className="w-5 h-5 absolute left-4 top-1/2 -translate-y-1/2 text-slate-400" />
            <input 
              type="text"
              placeholder="Tìm kiếm khu vực, vị trí..."
              value={searchQuery}
              onChange={(e) => {
                setSearchQuery(e.target.value);
                setCurrentPage(1);
              }}
              className="w-full pl-11 pr-4 py-2.5 rounded-xl border border-white/20 focus:border-white focus:ring-1 focus:ring-white outline-none text-sm transition-all font-normal bg-white/10 text-white placeholder:text-white/60"
            />
          </div>

          <div className="flex flex-wrap items-center gap-2 w-full md:w-auto">
            <div className="relative min-w-[140px] flex-1 md:flex-none">
              <select 
                value={filterCategory}
                onChange={(e) => {
                  setFilterCategory(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-normal appearance-none"
              >
                <option value="" className="text-slate-800">Tất cả khu vực</option>
                {uniqueCategories.map(cat => <option key={cat} value={cat} className="text-slate-800">{cat}</option>)}
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
            </div>

            <div className="relative min-w-[140px] flex-1 md:flex-none">
              <select 
                value={filterStatus}
                onChange={(e) => {
                  setFilterStatus(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-full px-3 py-2.5 pr-8 rounded-xl border border-white/20 bg-white/10 text-white outline-none text-sm font-normal appearance-none"
              >
                <option value="" className="text-slate-800">Trạng thái</option>
                <option value="Hoạt động" className="text-slate-800">Hoạt động</option>
                <option value="Ngừng hoạt động" className="text-slate-800">Ngừng hoạt động</option>
              </select>
              <ChevronDown className="w-4 h-4 absolute right-3 top-1/2 -translate-y-1/2 text-white pointer-events-none" />
            </div>
          </div>
        </div>
      </div>

      <div className="bg-white rounded-b-2xl shadow-sm border border-slate-200 overflow-hidden text-sm">
        <div className="overflow-x-auto">
          <table className="w-full text-left border-collapse">
            <thead className="bg-[#f8fafc] text-gray-500 border-b border-gray-200">
              <tr>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap text-center">STT</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Khu vực (Tòa/Khu)</th>
                <th className="p-4 text-[11px] font-black uppercase tracking-widest whitespace-nowrap">Vị trí cụ thể</th>
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
              {paginatedLocations.length > 0 ? paginatedLocations.map((item, index) => (
                  <tr 
                    key={item.id}
                    className="hover:bg-blue-50/50 transition-colors group"
                  >
                    <td className="p-4 font-bold text-slate-500 text-center whitespace-nowrap">
                      {(currentPage - 1) * pageSize + index + 1}
                    </td>
                    <td className="p-4 font-semibold text-slate-800 whitespace-nowrap">
                      {item.category}
                    </td>
                    <td className="p-4 font-medium text-slate-600 whitespace-nowrap">
                      {item.detail}
                    </td>
                    <td className="p-4 text-center">
                      <span className={`inline-flex items-center px-2.5 py-1 rounded-full text-[11px] font-bold ${
                        item.status === 'Hoạt động' 
                          ? 'bg-green-100 text-green-700 border border-green-200' 
                          : 'bg-slate-100 text-slate-700 border border-slate-200'
                      }`}>
                        {item.status}
                      </span>
                    </td>
                    <td className="p-4 text-slate-600 font-medium whitespace-nowrap text-center">
                      {item.date}
                    </td>
                    <td className="p-4">
                      <div className="flex items-center justify-center gap-2">
                        <button 
                          onClick={() => handleOpenEdit(item)}
                          className="w-8 h-8 rounded-lg bg-slate-50 text-slate-400 hover:text-orange-500 hover:bg-orange-50 flex items-center justify-center transition-colors outline-none"
                          title="Chỉnh sửa"
                        >
                          <Edit className="w-4 h-4" />
                        </button>
                        <button 
                          onClick={() => handleToggleStatus(item)}
                          className={`relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center justify-center rounded-full transition-colors duration-200 ease-in-out outline-none ml-2 ${item.status === 'Hoạt động' ? 'bg-[#004c91]' : 'bg-gray-300'}`}
                          title={item.status === 'Hoạt động' ? 'Ngừng hoạt động' : 'Hoạt động'}
                        >
                          <span className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition duration-200 ease-in-out ${item.status === 'Hoạt động' ? 'translate-x-2' : '-translate-x-2'}`} />
                        </button>
                      </div>
                    </td>
                  </tr>
                )) : (
                  <tr className="bg-slate-50/50">
                    <td colSpan={6} className="px-6 py-16 text-center text-slate-500">
                      <p className="font-medium text-slate-600 mb-1">Không tìm thấy khu vực nào</p>
                    </td>
                  </tr>
                )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {filteredLocations.length > 0 && (
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
                className="px-3 py-1.5 pr-8 rounded-lg border border-gray-200 text-sm font-normal text-gray-700 bg-white focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 appearance-none min-w-[70px] text-left"
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

      <AnimatePresence>
        {isModalOpen && (
          <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 font-sans">
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              exit={{ opacity: 0, scale: 0.95 }}
              className="bg-white rounded-3xl w-full max-w-lg overflow-hidden shadow-2xl relative"
            >
              <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between bg-slate-50">
                <h3 className="text-xl font-black text-[#004c91]">
                  {isEditMode ? "Chỉnh sửa khu vực" : "Thêm khu vực mới"}
                </h3>
                <button onClick={() => setIsModalOpen(false)} className="w-8 h-8 bg-white border border-slate-200 text-slate-500 rounded-full flex items-center justify-center hover:text-red-500">
                  <X className="w-4 h-4" />
                </button>
              </div>

              <div className="p-6">
                <form onSubmit={handleSubmit} className="space-y-4">
                  <div className="space-y-3">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide border-b border-slate-100 pb-2 block">Tên Khu Vực / Tòa</label>
                    <div className="flex items-center gap-4">
                      <label className="flex items-center gap-2 cursor-pointer cursor-pointer text-sm font-medium text-slate-700">
                        <input
                          type="radio"
                          name="categoryType"
                          value="existing"
                          checked={categoryType === 'existing'}
                          onChange={() => {
                            setCategoryType('existing');
                            if (uniqueCategories.length > 0) {
                              setFormData({...formData, category: uniqueCategories[0]});
                            }
                          }}
                          className="w-4 h-4 text-[#004c91] border-gray-300 focus:ring-[#004c91]"
                        />
                        Khu vực có sẵn
                      </label>
                      <label className="flex items-center gap-2 cursor-pointer cursor-pointer text-sm font-medium text-slate-700">
                        <input
                          type="radio"
                          name="categoryType"
                          value="new"
                          checked={categoryType === 'new'}
                          onChange={() => {
                            setCategoryType('new');
                            setFormData({...formData, category: ''});
                          }}
                          className="w-4 h-4 text-[#004c91] border-gray-300 focus:ring-[#004c91]"
                        />
                        Khu vực mới
                      </label>
                    </div>

                    {categoryType === 'existing' ? (
                      <div className="relative">
                        <select
                          value={formData.category}
                          onChange={(e) => setFormData({...formData, category: e.target.value})}
                          className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal appearance-none bg-white max-h-48 overflow-y-auto"
                        >
                          {uniqueCategories.map(cat => (
                            <option key={cat} value={cat}>{cat}</option>
                          ))}
                        </select>
                        <ChevronDown className="w-4 h-4 text-slate-400 absolute right-4 top-1/2 -translate-y-1/2 pointer-events-none" />
                      </div>
                    ) : (
                      <input 
                        type="text" 
                        required
                        value={formData.category}
                        onChange={(e) => setFormData({...formData, category: e.target.value})}
                        className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal"
                        placeholder="Nhập tên khu vực mới (VD: TÒA DELTA)"
                      />
                    )}
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-xs font-bold text-slate-500 uppercase tracking-wide">Vị trí cụ thể</label>
                    <input 
                      type="text" 
                      required
                      value={formData.detail}
                      onChange={(e) => setFormData({...formData, detail: e.target.value})}
                      className="w-full px-4 py-2.5 rounded-xl border border-slate-200 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-sm font-normal"
                      placeholder="VD: Sảnh chính"
                    />
                  </div>
                  <div className="flex justify-end gap-3 pt-4 border-t border-slate-100 mt-4">
                    <button type="button" onClick={() => setIsModalOpen(false)} className="px-5 py-2.5 rounded-xl text-sm font-bold text-slate-600 bg-slate-100 hover:bg-slate-200">
                      Hủy
                    </button>
                    <button type="submit" className="px-5 py-2.5 rounded-xl text-sm font-bold text-white bg-[#f37021] hover:bg-[#e85c0d]">
                      {isEditMode ? "Cập nhật" : "Tạo mới"}
                    </button>
                  </div>
                </form>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>
    </div>
  );
}
