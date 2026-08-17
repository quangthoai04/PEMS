/**
 * Trang EditEmail
 * Màn hình thao tác để chỉnh sửa bản nháp danh mục thư gửi ra hệ thống.
 */

// Đây là trang chỉnh sửa một mẫu email đã có trong khu vực quản trị
import React, { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import { ChevronRight, ArrowLeft } from 'lucide-react';
import { useNavigate, Link, useParams } from 'react-router-dom';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.snow.css';

const MOCK_EMAIL_DATA = {
  id: '1',
  templateName: 'Thông báo nhập học',
  emailSubject: 'Chào mừng tân sinh viên',
  emailContent: '<h1>Xin chào!</h1><p>Đây là nội dung của mẫu email. Chào mừng các bạn!</p>',
  status: 'Sử dụng',
  desc: 'Email gửi cho các bạn tân sinh viên vừa trúng tuyển.',
};

export function EditEmail() {
  const navigate = useNavigate();
  const { id } = useParams();

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const isHO = user?.role?.toUpperCase() === 'HO';

  const [templateName, setTemplateName] = useState(MOCK_EMAIL_DATA.templateName);
  const [emailSubject, setEmailSubject] = useState(MOCK_EMAIL_DATA.emailSubject);
  const [desc, setDesc] = useState(MOCK_EMAIL_DATA.desc);
  const [emailContent, setEmailContent] = useState(MOCK_EMAIL_DATA.emailContent);
  const [status, setStatus] = useState(MOCK_EMAIL_DATA.status);
  const [campus, setCampus] = useState('Hà Nội'); // Pre-fill sample if needed

  useEffect(() => {
    // Fake fetch
    setTemplateName(MOCK_EMAIL_DATA.templateName);
    setEmailSubject(MOCK_EMAIL_DATA.emailSubject);
    setDesc(MOCK_EMAIL_DATA.desc);
    setEmailContent(MOCK_EMAIL_DATA.emailContent);
    setStatus(MOCK_EMAIL_DATA.status);
  }, [id]);

  const handleCancel = () => {
    navigate(`/dashboard/email`);
  };

  const handleUpdate = () => {
    // Implement update logic here
    console.log('Updating template:', { templateName, emailSubject, desc, status, emailContent });
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
        <span className="text-[#004c91]">Chỉnh sửa mẫu email</span>
      </div>

      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden mb-10">
        {/* Header Section */}
        <div className="bg-[#004c91] px-6 py-4 border-b border-[#004c91] flex items-center gap-3">
          <button 
            onClick={handleCancel}
            className="w-8 h-8 flex items-center justify-center rounded-full hover:bg-white/10 transition-colors text-white"
          >
            <ArrowLeft className="w-5 h-5" />
          </button>
          <h1 className="text-3xl font-bold text-white tracking-wide">Chỉnh sửa mẫu email</h1>
        </div>

        <div className="p-4 sm:p-6 md:p-8">
          {/* Tiêu đề phụ 1 */}
          <div className="inline-block bg-blue-50 px-4 py-2 rounded-md mb-6">
            <h2 className="text-[#004c91] font-bold text-lg tracking-wide uppercase">Thông tin mẫu email</h2>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
            <div className="space-y-2">
              <label className="block text-sm font-bold text-gray-700">
                Tên mẫu email <span className="text-red-500">*</span>
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
                Trạng thái
              </label>
              <div className="flex items-center gap-6 h-[46px] px-2">
                <label className="flex items-center gap-2 cursor-pointer group">
                  <div className={`w-4 h-4 rounded-full border-2 flex items-center justify-center transition-colors ${status === 'Sử dụng' ? 'border-[#004c91]' : 'border-gray-400 group-hover:border-[#004c91]'}`}>
                    {status === 'Sử dụng' && <div className="w-2 h-2 rounded-full bg-[#004c91]" />}
                  </div>
                  <input
                    type="radio"
                    name="status"
                    value="Sử dụng"
                    checked={status === 'Sử dụng'}
                    onChange={(e) => setStatus(e.target.value)}
                    className="hidden"
                  />
                  <span className={`text-sm ${status === 'Sử dụng' ? 'text-gray-900 font-medium' : 'text-gray-500'}`}>Hoạt động</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer group">
                  <div className={`w-4 h-4 rounded-full border-2 flex items-center justify-center transition-colors ${status === 'Không sử dụng' ? 'border-[#004c91]' : 'border-gray-400 group-hover:border-[#004c91]'}`}>
                    {status === 'Không sử dụng' && <div className="w-2 h-2 rounded-full bg-[#004c91]" />}
                  </div>
                  <input
                    type="radio"
                    name="status"
                    value="Không sử dụng"
                    checked={status === 'Không sử dụng'}
                    onChange={(e) => setStatus(e.target.value)}
                    className="hidden"
                  />
                  <span className={`text-sm ${status === 'Không sử dụng' ? 'text-gray-900 font-medium' : 'text-gray-500'}`}>Ngừng hiển thị</span>
                </label>
              </div>
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
              <div className="space-y-2">
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
          <h2 className="text-[#004c91] font-bold text-lg tracking-wide mb-4 uppercase">
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

          <div className="flex justify-end gap-3 pt-4">
            <button
              onClick={handleCancel}
              className="px-6 py-2.5 rounded-lg border border-gray-300 text-gray-700 font-bold hover:text-[#004c91] hover:bg-blue-50 hover:border-[#004c91] transition-all bg-white uppercase"
            >
              Hủy
            </button>
            <button
              onClick={handleUpdate}
              className="bg-[#f37021] hover:bg-[#d65f1a] text-white px-6 py-2.5 rounded-lg font-bold transition-colors shadow-sm uppercase"
            >
              Lưu thay đổi
            </button>
          </div>
        </div>
      </div>
    </motion.div>
  );
}

export default EditEmail;
