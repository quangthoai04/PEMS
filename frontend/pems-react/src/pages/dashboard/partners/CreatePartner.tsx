/**
 * Trang CreatePartner
 * Khai báo nhân sự tổ chức tham quan kí kết làm đối tác hợp tác.
 */

import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ChevronRight, UploadCloud, Image as ImageIcon } from 'lucide-react';

export function CreatePartner() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isHO = user?.role?.toUpperCase() === 'HO';

  const [partnerDetails, setPartnerDetails] = useState({
    code: '',
    name: '',
    country: '',
    website: '',
    address: '',
    description: '',
    campus: '',
  });

  const InputFieldWrapper = ({ label, required, children, hint }: { label: string, required?: boolean, children: React.ReactNode, hint?: string }) => (
    <div className="flex flex-col gap-2">
      <label className="text-[15px] font-bold text-gray-800">
        {label}{required && <span className="text-red-500 ml-1">*</span>}
      </label>
      {children}
      {hint && <span className="text-sm text-gray-500">{hint}</span>}
    </div>
  );

  return (
    <div className="p-4 sm:p-6 md:p-8 max-w-6xl mx-auto w-full">
      {/* Breadcrumb */}
      <div className="flex items-center gap-2 text-sm text-gray-500 mb-6 font-medium">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors">Dashboard</button>
        <span className="text-gray-400">/</span>
        <button onClick={() => navigate('/dashboard/partners')} className="hover:text-[#004c91] transition-colors">Quản lý đối tác</button>
        <span className="text-gray-400">/</span>
        <span className="text-[#004c91]">Tạo mới đối tác</span>
      </div>

      {/* Page Title */}
      <div className="mb-8">
        <h1 className="text-2xl font-bold text-[#004c91]">Thêm mới đối tác</h1>
      </div>

      <div className="bg-white rounded-2xl shadow-[0_4px_24px_rgba(0,0,0,0.02)] border border-gray-100 p-8 flex flex-col gap-8">
        
        {/* Row 1 */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
          <InputFieldWrapper label="Mã đối tác" required>
            <input 
              type="text" 
              placeholder="Nhập mã đối tác (VD: P001)..."
              value={partnerDetails.code}
              onChange={(e) => setPartnerDetails({...partnerDetails, code: e.target.value})}
              className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-[15px]"
            />
          </InputFieldWrapper>

          <InputFieldWrapper label="Tên đối tác" required>
            <input 
              type="text" 
              placeholder="Nhập tên đối tác đầy đủ..."
              value={partnerDetails.name}
              onChange={(e) => setPartnerDetails({...partnerDetails, name: e.target.value})}
              className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-[15px]"
            />
          </InputFieldWrapper>
        </div>

        {/* Row 2 */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
          <InputFieldWrapper label="Quốc gia" required>
            <div className="relative">
              <select 
                value={partnerDetails.country}
                onChange={(e) => setPartnerDetails({...partnerDetails, country: e.target.value})}
                className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-[15px] bg-white appearance-none cursor-pointer"
              >
                <option value="" disabled>-- Chọn quốc gia --</option>
                <option value="Hàn Quốc">Hàn Quốc</option>
                <option value="Nhật Bản">Nhật Bản</option>
                <option value="Úc">Úc</option>
                <option value="Mỹ">Mỹ</option>
                <option value="Pháp">Pháp</option>
                <option value="Anh">Anh</option>
                <option value="Canada">Canada</option>
                <option value="Đức">Đức</option>
              </select>
              <div className="absolute inset-y-0 right-4 flex items-center pointer-events-none text-gray-500">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="m6 9 6 6 6-6"/></svg>
              </div>
            </div>
          </InputFieldWrapper>

          <InputFieldWrapper label="Website">
            <input 
              type="url" 
              placeholder="https://..."
              value={partnerDetails.website}
              onChange={(e) => setPartnerDetails({...partnerDetails, website: e.target.value})}
              className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-[15px]"
            />
          </InputFieldWrapper>
        </div>

        {/* Cở sở (Only for HO) */}
        {isHO && (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
            <InputFieldWrapper label="Cơ sở" required>
              <div className="relative">
                <select 
                  value={partnerDetails.campus}
                  onChange={(e) => setPartnerDetails({...partnerDetails, campus: e.target.value})}
                  className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-[15px] bg-white appearance-none cursor-pointer"
                >
                  <option value="" disabled>-- Chọn cơ sở --</option>
                  <option value="Hà Nội">Cơ sở Hà Nội</option>
                  <option value="Đà Nẵng">Cơ sở Đà Nẵng</option>
                  <option value="Cần Thơ">Cơ sở Cần Thơ</option>
                  <option value="Hồ Chí Minh">Cơ sở Hồ Chí Minh</option>
                  <option value="Quy Nhơn">Cơ sở Quy Nhơn</option>
                </select>
                <div className="absolute inset-y-0 right-4 flex items-center pointer-events-none text-gray-500">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="m6 9 6 6 6-6"/></svg>
                </div>
              </div>
            </InputFieldWrapper>
          </div>
        )}

        {/* Row 3 */}
        <div className="grid grid-cols-1 gap-8">
          <InputFieldWrapper label="Địa chỉ">
            <input 
              type="text" 
              placeholder="Nhập địa chỉ đầy đủ của đối tác..."
              value={partnerDetails.address}
              onChange={(e) => setPartnerDetails({...partnerDetails, address: e.target.value})}
              className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-[15px]"
            />
          </InputFieldWrapper>
        </div>

        {/* Row 4 */}
        <div className="grid grid-cols-1 gap-8">
          <InputFieldWrapper label="Mô tả chung">
            <textarea 
              rows={4}
              placeholder="Nhập giới thiệu tóm tắt về đối tác này..."
              value={partnerDetails.description}
              onChange={(e) => setPartnerDetails({...partnerDetails, description: e.target.value})}
              className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none transition-shadow text-[15px] resize-none"
            />
          </InputFieldWrapper>
        </div>

        {/* Row 5 - Images */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
          {/* Logo */}
          <div className="flex flex-col gap-2">
            <span className="text-[15px] font-bold text-gray-800">Logo đối tác</span>
            <label className="border-2 border-dashed border-gray-300 rounded-xl p-8 bg-gray-50/50 hover:bg-gray-50 hover:border-[#004c91] transition-colors flex flex-col items-center justify-center cursor-pointer group h-48 w-full">
              <div className="w-12 h-12 rounded-full bg-white shadow-sm flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                <UploadCloud className="w-6 h-6 text-[#004c91]" />
              </div>
              <span className="font-bold text-[#004c91]">Tải logo mới</span>
              <span className="text-sm text-gray-500 mt-1">(1:1, ~300x300)</span>
              <input type="file" className="hidden" accept="image/*" />
            </label>
          </div>

          {/* Thumbnail */}
          <div className="flex flex-col gap-2">
            <span className="text-[15px] font-bold text-gray-800">Ảnh bìa (Thumbnail)</span>
            <label className="border-2 border-dashed border-gray-300 rounded-xl p-8 bg-gray-50/50 hover:bg-gray-50 hover:border-[#004c91] transition-colors flex flex-col items-center justify-center cursor-pointer group h-48 w-full">
              <div className="w-12 h-12 rounded-full bg-white shadow-sm flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                <ImageIcon className="w-6 h-6 text-[#004c91]" />
              </div>
              <span className="font-bold text-[#004c91]">Tải ảnh bìa mới</span>
              <span className="text-sm text-gray-500 mt-1">(16:9, ~1280x720)</span>
              <input type="file" className="hidden" accept="image/*" />
            </label>
          </div>
        </div>

        {/* Actions */}
        <div className="flex justify-end items-center gap-4 pt-6 mt-4 border-t border-gray-100">
          <button 
            type="button" 
            onClick={() => navigate('/dashboard/partners')}
            className="px-6 py-2.5 rounded-xl font-bold text-gray-600 border border-gray-300 hover:bg-gray-50 transition-colors bg-white shadow-sm"
          >
            Hủy bỏ
          </button>
          <button 
            type="button" 
            className="bg-[#f37021] text-white px-8 py-2.5 rounded-xl shadow-[0_4px_14px_rgba(243,112,33,0.3)] hover:shadow-[0_6px_20px_rgba(243,112,33,0.4)] hover:-translate-y-0.5 transition-all font-bold tracking-wide"
            onClick={() => navigate('/dashboard/partners')}
          >
            Lưu thông tin
          </button>
        </div>

      </div>
    </div>
  );
}
