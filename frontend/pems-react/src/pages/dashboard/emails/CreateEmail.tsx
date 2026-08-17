/**
 * Trang CreateEmail
 * Màn hình soạn thảo một thông báo email riêng biệt hoặc theo mẫu.
 */

// Đây là trang tạo mới một mẫu email trong khu vực quản trị
import React, { useState } from 'react';
import { motion } from 'motion/react';
import { ChevronRight, ArrowLeft } from 'lucide-react';
import { useNavigate, Link } from 'react-router-dom';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';

export function CreateEmail() {
  const navigate = useNavigate();
  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isHO = user?.role?.toUpperCase() === 'HO';

  const [templateName, setTemplateName] = useState('');
  const [emailSubject, setEmailSubject] = useState('');
  const [desc, setDesc] = useState('');
  const [emailContent, setEmailContent] = useState('');
  const [campus, setCampus] = useState('');

  const handleCancel = () => {
    navigate('/dashboard/email');
  };

  const handleCreate = () => {
    // Implement create logic here
    console.log('Creating template:', { templateName, emailSubject, desc, emailContent });
    navigate('/dashboard/email');
  };

  return (
    <motion.div 
      initial={{ opacity: 0, x: 20 }}
      animate={{ opacity: 1, x: 0 }}
      exit={{ opacity: 0, x: -20 }}
      transition={{ duration: 0.3 }}
      className="max-w-[900px] mx-auto space-y-6 pt-6"
    >
      <div className="flex items-center text-sm text-gray-500 mb-6 font-medium">
        <Link to="/dashboard" className="hover:text-[#004c91] transition-colors">Dashboard</Link>
        <span className="mx-2">/</span>
        <Link to="/dashboard/email" className="hover:text-[#004c91] transition-colors">Quản lý email</Link>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">Thêm mẫu email</span>
      </div>

      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden mb-10">
        {/* Header Section */}
        <div className="bg-[#004c91] px-6 py-4 border-b border-[#004c91] flex items-center gap-3">
          <button 
            onClick={() => navigate('/dashboard/email')}
            className="w-8 h-8 flex items-center justify-center rounded-full hover:bg-white/10 transition-colors text-white"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="text-3xl font-bold text-white tracking-wide">Tạo mẫu email</h1>
        </div>

        <div className="p-4 sm:p-6 md:p-8">
          {/* Tiêu đề phụ 1 */}
          <div className="inline-block bg-blue-50 px-4 py-2 rounded-md mb-6">
            <h2 className="text-[#004c91] font-bold text-lg tracking-wide">Thông tin mẫu</h2>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
            <div className="space-y-2">
              <label className="block text-sm font-bold text-gray-700">
                Tên mẫu <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={templateName}
                onChange={(e) => setTemplateName(e.target.value)}
                placeholder="Nhập tên mẫu email..."
                className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-normal"
              />
            </div>
            <div className="space-y-2">
              <label className="block text-sm font-bold text-gray-700">
                Tiêu đề email <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={emailSubject}
                onChange={(e) => setEmailSubject(e.target.value)}
                placeholder="Nhập tiêu đề email..."
                className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-normal"
              />
            </div>
            {isHO && (
              <div className="space-y-2 md:col-span-2">
                <label className="block text-sm font-bold text-gray-700">
                  Cơ sở <span className="text-red-500">*</span>
                </label>
                <select
                  value={campus}
                  onChange={(e) => setCampus(e.target.value)}
                  className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-normal bg-white"
                >
                  <option value="" disabled>Chọn cơ sở</option>
                  <option value="Hà Nội">Hà Nội</option>
                  <option value="Hồ Chí Minh">Hồ Chí Minh</option>
                  <option value="Đà Nẵng">Đà Nẵng</option>
                  <option value="Cần Thơ">Cần Thơ</option>
                  <option value="Quy Nhơn">Quy Nhơn</option>
                  <option value="Toàn quốc">Toàn quốc</option>
                </select>
              </div>
            )}
            <div className="space-y-2 md:col-span-2">
              <label className="block text-sm font-bold text-gray-700">
                Mô tả <span className="text-red-500">*</span>
              </label>
              <input
                type="text"
                value={desc}
                onChange={(e) => setDesc(e.target.value)}
                placeholder="Nói ngắn gọn về mục đích sử dụng..."
                className="w-full px-4 py-2.5 border border-gray-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-[#004c91]/20 focus:border-[#004c91] text-gray-700 font-normal"
              />
            </div>
          </div>

          {/* Ngăn cách */}
          <hr className="border-gray-200 my-8" />

          {/* Tiêu đề phụ 2 */}
          <h2 className="text-[#004c91] font-bold text-lg tracking-wide mb-4">
            Nội dung email <span className="text-red-500">*</span>
          </h2>

          <div className="mb-6">
            {/* @ts-ignore */}
            <ReactQuill
              theme="snow"
              value={emailContent}
              onChange={setEmailContent}
              placeholder="Nhập nội dung email..."
              className="bg-white"
              modules={{
                toolbar: [
                  ['bold', 'italic', 'underline', 'strike'],
                  [{ 'align': [] }],
                  [{ 'list': 'ordered'}, { 'list': 'bullet' }],
                  ['link', 'image'],
                  ['clean']
                ],
              }}
            />
          </div>

          <div className="flex justify-end gap-3">
            <button
              onClick={handleCancel}
              className="px-6 py-2.5 rounded-lg border border-gray-300 text-gray-700 font-bold hover:text-[#004c91] hover:bg-blue-50 hover:border-[#004c91] transition-all bg-white"
            >
              Hủy
            </button>
            <button
              onClick={handleCreate}
              className="bg-[#f37021] hover:bg-[#d9621a] text-white px-6 py-2.5 rounded-lg font-bold transition-colors shadow-sm"
            >
              Tạo mẫu
            </button>
          </div>
        </div>
      </div>
    </motion.div>
  );
}

export default CreateEmail;
