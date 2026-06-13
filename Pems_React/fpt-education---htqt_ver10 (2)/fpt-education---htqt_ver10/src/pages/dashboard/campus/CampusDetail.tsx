/**
 * Trang CampusDetail
 * Cài đặt cơ sở và chi tiết khu trung tâm tương ứng của campus nội nhóm.
 */

import React, { useEffect, useState } from 'react';
import { Building2, ChevronLeft, MapPin, Edit2, Save, X } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';

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

export function CampusDetail() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [campus, setCampus] = useState(mockCampuses[0]);
  const [isEditing, setIsEditing] = useState(false);
  const [editingCampus, setEditingCampus] = useState(mockCampuses[0]);

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();
  const isHO = userRole === 'HO';

  useEffect(() => {
    window.scrollTo(0, 0);
    if (id) {
      const found = mockCampuses.find(c => c.id === parseInt(id));
      if (found) {
        setCampus(found);
        setEditingCampus(found);
      }
    }
  }, [id]);

  if (!isHO) {
    return (
      <div className="p-4 sm:p-6 md:p-8 h-full flex items-center justify-center">
        <div className="text-center">
          <h2 className="text-2xl font-bold text-gray-900 mb-2">Không có quyền truy cập</h2>
          <p className="text-gray-500">Trang này chỉ dành cho tài khoản HO.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="p-4 md:p-8 space-y-6 bg-gray-50/50 min-h-screen">
      {/* Breadcrumbs & Title */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6">
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard')}>Dashboard</span>
        <span className="mx-2">/</span>
        <span className="hover:text-[#004c91] cursor-pointer transition-colors" onClick={() => navigate('/dashboard/campus')}>Quản lý campus</span>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-medium">Chi tiết campus</span>
      </div>
      <div className="border-b border-gray-100 pb-4 mb-6">
        <h1 className="text-3xl font-bold text-[#004c91]">Chi tiết Campus</h1>
      </div>

      <div className="bg-white rounded-3xl shadow-sm border border-gray-100 overflow-hidden w-full max-w-5xl">
        <div className="bg-[#004c91] p-8 md:p-10 border-b border-[#003366] relative overflow-hidden">
          <div className="absolute top-0 right-0 p-8 opacity-10 pointer-events-none transform translate-x-4 -translate-y-4">
            <Building2 className="w-48 h-48 text-white" />
          </div>
          
          <div className="relative z-10 flex flex-col items-start gap-4">
            <div className="flex items-center justify-between w-full">
              <div className="flex items-center gap-3">
                <span className={`inline-flex px-3 py-1 text-xs font-bold rounded-full ${
                  campus.status === 'Hoạt động' ? 'bg-[#eaffe4] text-[#0aa14f] border border-[#ceefda]' : 'bg-gray-100 text-gray-600 border border-gray-200'
                }`}>
                  {campus.status}
                </span>
                {!isEditing && (
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 bg-white rounded-full text-xs font-bold text-[#004c91] shadow-sm">
                    {campus.base}
                  </span>
                )}
              </div>
              {!isEditing && (
                <button 
                  onClick={() => {
                    setEditingCampus(campus);
                    setIsEditing(true);
                  }}
                  className="p-2 text-white/90 hover:text-white bg-transparent border border-white/30 hover:bg-white/10 rounded-xl transition-all cursor-pointer flex items-center justify-center"
                  title="Chỉnh sửa"
                >
                  <Edit2 className="w-[20px] h-[20px]" />
                </button>
              )}
            </div>
            
            {isEditing ? (
              <div className="w-full space-y-4">
                <input 
                  value={editingCampus.name}
                  onChange={(e) => setEditingCampus({...editingCampus, name: e.target.value})}
                  className="w-full text-2xl md:text-3xl font-bold text-white bg-transparent border border-white/30 focus:border-white focus:bg-white/10 p-3 rounded-2xl outline-none transition-all placeholder:text-white/50"
                  placeholder="Nhập tên campus..."
                />
              </div>
            ) : (
              <h2 className="text-2xl md:text-3xl font-bold text-white leading-snug mt-2">
                {campus.name}
              </h2>
            )}
          </div>
        </div>

        <div className="p-4 sm:p-6 md:p-8 md:p-10 space-y-8">
          <h3 className="text-lg font-bold text-[#004c91] flex items-center gap-2 border-b border-gray-100 pb-3 mb-4">
             <MapPin className="w-5 h-5" /> 
             Thông tin cơ sở
          </h3>
          <div className="bg-[#e6eff7] rounded-2xl p-6 md:p-8 border border-blue-100/50">
            {isEditing ? (
              <div className="space-y-4">
                <div>
                  <label className="text-sm font-bold text-gray-700 block mb-2">Cơ sở đăng ký</label>
                  <select 
                    value={editingCampus.base}
                    onChange={(e) => setEditingCampus({...editingCampus, base: e.target.value})}
                    className="w-full text-gray-900 leading-relaxed text-[15px] p-4 bg-white border border-[#004c91]/30 rounded-xl outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all font-medium"
                  >
                    <option value="Hà Nội">Hà Nội</option>
                    <option value="Hồ Chí Minh">Hồ Chí Minh</option>
                    <option value="Đà Nẵng">Đà Nẵng</option>
                    <option value="Cần Thơ">Cần Thơ</option>
                    <option value="Quy Nhơn">Quy Nhơn</option>
                  </select>
                </div>
                <div>
                  <label className="text-sm font-bold text-gray-700 block mb-2">Trưởng phòng IC</label>
                  <select 
                    value={editingCampus.ic_head || ""}
                    onChange={(e) => setEditingCampus({...editingCampus, ic_head: e.target.value})}
                    className="w-full text-gray-900 leading-relaxed text-[15px] p-4 bg-white border border-[#004c91]/30 rounded-xl outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] transition-all font-medium"
                  >
                    <option value="" className="opacity-50">-- Chọn trưởng phòng --</option>
                    <option value="Nguyễn Văn A">Nguyễn Văn A</option>
                    <option value="Trần Thị B">Trần Thị B</option>
                    <option value="Lê Văn C">Lê Văn C</option>
                    <option value="Phạm Thị D">Phạm Thị D</option>
                    <option value="Hoàng Văn E">Hoàng Văn E</option>
                  </select>
                </div>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <p className="text-sm text-gray-500 font-medium mb-1">Cơ sở đăng ký</p>
                  <p className="text-gray-900 font-bold text-lg">{campus.base}</p>
                </div>
                <div>
                  <p className="text-sm text-gray-500 font-medium mb-1">Trưởng phòng IC</p>
                  <p className="text-gray-900 font-bold text-lg">{campus.ic_head || 'Chưa cập nhật'}</p>
                </div>
              </div>
            )}
          </div>
          
          <div className="flex items-center justify-end gap-3 pt-6 border-t border-gray-100">
            {!isEditing ? (
              <button 
                onClick={() => navigate('/dashboard/campus')}
                className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 hover:text-[#004c91] transition-colors shadow-sm cursor-pointer"
              >
                <ChevronLeft className="w-4 h-4" />
                <span>Quay lại</span>
              </button>
            ) : (
              <>
                <button 
                  onClick={() => setIsEditing(false)}
                  className="flex items-center gap-2 px-6 py-2.5 bg-white border border-gray-200 text-gray-700 font-bold rounded-xl hover:bg-gray-50 transition-colors shadow-sm cursor-pointer"
                >
                  <X className="w-4 h-4" />
                  <span>Hủy</span>
                </button>
                <button 
                  onClick={() => {
                    setCampus(editingCampus);
                    setIsEditing(false);
                  }}
                  className="flex items-center gap-2 px-6 py-2.5 bg-[#004c91] text-white font-bold rounded-xl hover:bg-[#003366] transition-colors shadow-sm cursor-pointer"
                >
                  <Save className="w-4 h-4" />
                  <span>Lưu thay đổi</span>
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
