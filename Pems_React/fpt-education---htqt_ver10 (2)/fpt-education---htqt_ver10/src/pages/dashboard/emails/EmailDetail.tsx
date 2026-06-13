/**
 * Trang EmailDetail
 * Hiển thị cấu trúc nội dung, chủ thể cùng danh sách người đọc liên kết đến mẫu gửi email chi tiết.
 */

// Đây là trang hiển thị thông tin chi tiết của một mẫu email trong khu vực quản trị
import React, { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import { ChevronRight, ArrowLeft, User, Mail, Calendar, Edit3, Clock } from 'lucide-react';
import { useNavigate, Link, useParams } from 'react-router-dom';
import ReactQuill from 'react-quill-new';
import 'react-quill-new/dist/quill.bubble.css';

const MOCK_EMAIL_DATA = {
  id: '1',
  templateName: 'Thông báo nhập học',
  emailSubject: 'Chào mừng tân sinh viên',
  emailContent: '<h1>Xin chào!</h1><p>Đây là nội dung của mẫu email. Chào mừng các bạn!</p>',
  status: 'Sử dụng',
  desc: 'Email gửi cho các bạn tân sinh viên vừa trúng tuyển.',
  creatorName: 'Nguyễn Văn A',
  creatorCampus: 'Hồ Chí Minh',
  creatorEmail: 'nva@example.com',
  createdAt: '10/05/2024 09:30:00',
  updaterName: 'Trần Thị B',
  updaterCampus: 'Đà Nẵng',
  updaterEmail: 'ttb@example.com',
  updatedAt: '12/05/2024 14:15:00',
  campus: 'Hà Nội',
};

export function EmailDetail() {
  const navigate = useNavigate();
  const { id } = useParams();
  
  // Fake fetch data
  const [data, setData] = useState(MOCK_EMAIL_DATA);

  const userStr = localStorage.getItem('currentUser');
  const user = userStr ? JSON.parse(userStr) : null;
  const userRole = user?.role?.toUpperCase();

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
        <span className="text-[#004c91]">Chi tiết mẫu email</span>
      </div>

      <div className="bg-white rounded-lg shadow-sm border border-gray-200 overflow-hidden mb-10">
        {/* Header Section */}
        <div className="bg-[#004c91] px-6 py-4 border-b border-[#004c91] flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <button 
              onClick={() => navigate('/dashboard/email')}
              className="w-8 h-8 flex items-center justify-center rounded-full hover:bg-white/10 transition-colors text-white"
            >
              <ArrowLeft className="w-5 h-5" />
            </button>
            <h1 className="text-3xl font-bold text-white tracking-wide">Chi tiết mẫu email</h1>
          </div>
        </div>

        <div className="p-4 sm:p-6 md:p-8">
          {/* Tiêu đề phụ 1 */}
          <div className="inline-block bg-blue-50 px-4 py-2 rounded-md mb-6 border-l-4 border-[#004c91]">
            <h2 className="text-[#004c91] font-bold text-lg tracking-wide uppercase">THÔNG TIN MẪU EMAIL</h2>
          </div>

          <div className="space-y-6 mb-8">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
              <div className="space-y-1 pb-2">
                <label className="block font-bold text-gray-900 mb-2">
                  Tên mẫu <span className="text-red-500">*</span>
                </label>
                <div className="px-4 py-3 bg-gray-50 rounded-lg text-gray-800 font-medium border border-gray-200">{data.templateName}</div>
              </div>

              <div className="space-y-1 pb-2">
                <label className="block font-bold text-gray-900 mb-2">
                  Trạng thái
                </label>
                <div className="flex items-center h-[50px]">
                  <div className={`px-4 py-1.5 font-bold rounded-md text-sm uppercase ${data.status === 'Sử dụng' ? 'bg-green-500/20 text-green-600' : 'bg-gray-500/20 text-gray-600'}`}>
                    {data.status === 'Sử dụng' ? 'SỬ DỤNG' : 'KHÔNG SỬ DỤNG'}
                  </div>
                </div>
              </div>
            </div>

            <div className="space-y-1 pb-2">
              <label className="block font-bold text-gray-900 mb-2">
                Tiêu đề email <span className="text-red-500">*</span>
              </label>
              <div className="px-4 py-3 bg-gray-50 rounded-lg text-gray-800 font-medium border border-gray-200">{data.emailSubject}</div>
            </div>

            {userRole === 'HO' && (
              <div className="space-y-1 pb-2">
                <label className="block font-bold text-gray-900 mb-2">
                  Cơ sở <span className="text-red-500">*</span>
                </label>
                <div className="px-4 py-3 bg-gray-50 rounded-lg text-gray-800 font-medium border border-gray-200">{data.campus}</div>
              </div>
            )}

            <div className="space-y-1 pb-2">
              <label className="block font-bold text-gray-900 mb-2">
                Mô tả <span className="text-red-500">*</span>
              </label>
              <div className="px-4 py-3 bg-gray-50 rounded-lg text-gray-800 font-medium border border-gray-200">{data.desc}</div>
            </div>
          </div>

          <div className="space-y-2 mb-8">
              <label className="block font-bold text-gray-900 mb-2">
                Nội dung email <span className="text-red-500">*</span>
              </label>
              <div className="border border-gray-300 shadow-sm rounded-xl p-6 bg-[#f8fafc] min-h-[350px]">
                {/* @ts-ignore */}
                <ReactQuill
                  theme="bubble"
                  value={data.emailContent}
                  readOnly={true}
                  className="bg-transparent"
                />
              </div>
          </div>

        </div>
      </div>
    </motion.div>
  );
}

export default EmailDetail;
