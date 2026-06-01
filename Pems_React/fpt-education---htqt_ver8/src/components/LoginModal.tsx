// Đây là component hiển thị cửa sổ bật lên để đăng nhập
import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X, Eye, EyeOff, Mail } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import logo from '../assets/images/2021-FPTU-Eng.png';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const campuses = [
  { code: 'HL', name: 'Hà Nội (Hòa Lạc)' },
  { code: 'DN', name: 'Đà Nẵng' },
  { code: 'CT', name: 'Cần Thơ' },
  { code: 'HN', name: 'Hà Nội (Phố)' },
  { code: 'HCM', name: 'Hồ Chí Minh' }
];

const mockAccounts = [
  { account: 'student', password: '123', role: 'Student' },
  { account: 'staff', password: '123', role: 'Staff' },
  { account: 'admin', password: '123', role: 'Admin' },
  { account: 'ho', password: '123', role: 'HO' }
];

export function LoginModal({ isOpen, onClose }: LoginModalProps) {
  const [view, setView] = useState<'main' | 'dev'>('main');
  const navigate = useNavigate();

  // Dev login states
  const [selectedCampus, setSelectedCampus] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');

  const handleClose = () => {
    setView('main');
    setSelectedCampus('');
    setEmail('');
    setPassword('');
    setError('');
    setShowPassword(false);
    onClose();
  };

  const handleDevLogin = async (e: React.FormEvent) => {
    e.preventDefault();

    // 1. Kiểm tra nhanh dữ liệu đầu vào ở Client
    if (!selectedCampus) {
      setError('Vui lòng chọn cơ sở');
      return;
    }
    if (!email) {
      setError('Vui lòng nhập email');
      return;
    }
    if (!password) {
      setError('Vui lòng nhập mật khẩu');
      return;
    }

    try {
      // 2. BẮN HTTP REQUEST POST LÊN BACKEND .NET CORE
      // ⚠️ LƯU Ý: Hãy thay số port 7123 bằng số port thực tế trên Swagger của bạn
      const response = await fetch('https://localhost:7190/api/Auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          email: email,
          password: password,
          campusCode: selectedCampus,
        }),
      });

      const data = await response.json();

      // 3. NẾU BACKEND TRẢ VỀ LỖI (Ví dụ: 401 Unauthorized)
      if (!response.ok) {
        throw new Error(data.message || 'Tài khoản hoặc mật khẩu không chính xác');
      }

      // 4. ĐĂNG NHẬP THÀNH CÔNG MỸ MÃN:
      // - Bước 4.1: Lưu chuỗi JWT Token riêng biệt để làm "thẻ thông hành" gọi các API sau này
      localStorage.setItem('token', data.token);

      // - Bước 4.2: Ép dữ liệu từ DB trả về khớp với cấu trúc 'currentUser' cũ của bạn
      // Để đảm bảo trang /dashboard của bạn đọc ra tên và quyền không bị lỗi bề mặt
      localStorage.setItem('currentUser', JSON.stringify({
        account: data.email,
        role: data.roleCode,                       // Trả về 'Guest', 'Staff', 'Admin', 'HO'...
        campus: data.campusCode || selectedCampus, // Ưu tiên lấy mã cơ sở chuẩn từ Database
        name: data.fullName                        // Tên thật lấy từ MySQL (Ví dụ: Đinh Công Minh)
      }));

      // 5. Dọn dẹp trạng thái Form, đóng Modal và chuyển hướng sang Dashboard
      handleClose();
      navigate('/dashboard');

    } catch (err: any) {
      // Hiển thị thông báo lỗi lên giao diện nếu sai mật khẩu hoặc mất kết nối mạng
      setError(err.message || 'Không thể kết nối đến máy chủ Backend');
    }
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <div className="fixed inset-0 z-[200] flex items-center justify-center p-4">
          {/* Backdrop */}
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            onClick={handleClose}
            className="absolute inset-0 bg-black/40 backdrop-blur-sm"
          />

          {/* Modal Container */}
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            className="relative bg-white w-full max-w-[460px] rounded-2xl shadow-2xl overflow-hidden"
          >
            {view === 'main' ? (
              <div className="p-6 md:p-8 flex flex-col items-center">
                <button
                  onClick={handleClose}
                  className="absolute top-4 right-4 p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-full transition-colors"
                >
                  <X className="w-5 h-5" />
                </button>

                <img src={logo} alt="FPT University" className="h-16 md:h-20 mb-6 object-contain" />

                <h2 className="text-[#004c91] text-xl md:text-2xl font-black text-center leading-tight mb-2 tracking-tight">
                  International Cooperation and Guest Management System
                </h2>
                <p className="text-gray-500 text-[14px] text-center mb-6 font-medium">
                  Hệ thống quản lý tiếp khách và hợp tác quốc tế
                </p>

                <div className="w-full mb-6">
                  <label className="block text-gray-700 font-bold text-[14px] mb-2">Cơ sở (Campus):</label>
                  <div className="relative">
                    <select
                      value={selectedCampus}
                      onChange={(e) => setSelectedCampus(e.target.value)}
                      className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#284a32] focus:ring-1 focus:ring-[#284a32] outline-none appearance-none bg-white text-gray-500 font-medium"
                    >
                      <option value="" disabled>Chọn cơ sở</option>
                      {campuses.map(c => (
                        <option key={c.code} value={c.code} className="text-gray-800">{c.name}</option>
                      ))}
                    </select>
                    <div className="absolute inset-y-0 right-4 flex items-center pointer-events-none text-gray-400">
                      <svg width="12" height="8" viewBox="0 0 12 8" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1.41 0.589966L6 5.16997L10.59 0.589966L12 1.99997L6 7.99997L0 1.99997L1.41 0.589966Z" fill="currentColor" />
                      </svg>
                    </div>
                  </div>
                </div>

                <div className="w-full border border-dashed border-gray-300 rounded-xl p-6 bg-[#fafafa] flex flex-col items-center">
                  <p className="text-[13px] text-gray-600 mb-2 font-medium">Đăng nhập bằng tài khoản <span className="font-bold text-gray-800">@fpt.edu.vn</span></p>
                  <button className="w-full max-w-[300px] flex items-center justify-center gap-3 bg-[#e45140] hover:bg-[#d64537] text-white py-2.5 px-4 rounded-xl font-medium transition-colors shadow-sm mb-5 text-[14px]">
                    <svg className="w-5 h-5 flex-shrink-0" viewBox="0 0 24 24" fill="currentColor">
                      <path d="M12.48 10.92v3.28h7.84c-.24 1.84-.853 3.187-1.787 4.133-1.147 1.147-2.933 2.4-6.053 2.4-4.827 0-8.6-3.893-8.6-8.72s3.773-8.72 8.6-8.72c2.6 0 4.507 1.027 5.907 2.347l2.307-2.307C18.747 1.44 16.133 0 12.48 0 5.867 0 .307 5.387.307 12s5.56 12 12.173 12c3.573 0 6.267-1.173 8.373-3.36 2.16-2.16 2.84-5.213 2.84-7.667 0-.76-.053-1.467-.173-2.053H12.48z" />
                    </svg>
                    Sign in with Google
                  </button>

                  <p className="text-[13px] text-gray-600 mb-2 font-medium">Với sinh viên từ <span className="font-bold text-gray-800">K19 đăng nhập với FEID</span></p>
                  <button className="w-full max-w-[300px] flex items-center justify-center gap-3 bg-[#4285f4] hover:bg-[#3367d6] text-white py-2.5 px-4 rounded-xl font-medium transition-colors shadow-sm mb-6 text-[14px]">
                    <Mail className="w-5 h-5 flex-shrink-0" />
                    Sign in with FeID
                  </button>

                  <button
                    onClick={() => setView('dev')}
                    className="text-[13px] text-[#426151] hover:text-[#254d30] font-medium"
                  >
                    Đăng nhập bằng tài khoản Test (Dev only)
                  </button>
                </div>
              </div>
            ) : (
              <div className="flex flex-col">
                <div className="p-6 border-b border-gray-100 flex items-center justify-between">
                  <h2 className="text-xl font-bold text-gray-800">Đăng nhập hệ thống (Test)</h2>
                  <button
                    onClick={() => setView('main')}
                    className="p-2 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-full transition-colors"
                  >
                    <X className="w-6 h-6" />
                  </button>
                </div>

                <form onSubmit={handleDevLogin} className="p-6 space-y-6">
                  {error && (
                    <div className="p-3 bg-red-50 text-red-600 text-sm rounded-lg border border-red-100">
                      {error}
                    </div>
                  )}

                  <div>
                    <label className="block text-gray-700 text-[15px] mb-2 font-medium">
                      <span className="text-[#e45140] mr-1">*</span>Cơ sở (Campus)
                    </label>
                    <div className="relative">
                      <select
                        value={selectedCampus}
                        onChange={(e) => setSelectedCampus(e.target.value)}
                        className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#284a32] focus:ring-1 focus:ring-[#284a32] outline-none appearance-none bg-white text-gray-500 font-medium"
                      >
                        <option value="" disabled>Chọn cơ sở</option>
                        {campuses.map(c => (
                          <option key={c.code} value={c.code} className="text-gray-800">{c.name}</option>
                        ))}
                      </select>
                      <div className="absolute inset-y-0 right-4 flex items-center pointer-events-none text-gray-400">
                        <svg width="12" height="8" viewBox="0 0 12 8" fill="none" xmlns="http://www.w3.org/2000/svg">
                          <path d="M1.41 0.589966L6 5.16997L10.59 0.589966L12 1.99997L6 7.99997L0 1.99997L1.41 0.589966Z" fill="currentColor" />
                        </svg>
                      </div>
                    </div>
                  </div>

                  <div>
                    <label className="block text-gray-700 text-[15px] mb-2 font-medium">
                      <span className="text-[#e45140] mr-1">*</span>Email
                    </label>
                    <input
                      type="text"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      placeholder="a.outbound.fpt@gmail.com"
                      className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#284a32] focus:ring-1 focus:ring-[#284a32] outline-none bg-[#f2f5fa] text-gray-800 font-medium"
                    />
                  </div>

                  <div>
                    <label className="block text-gray-700 text-[15px] mb-2 font-medium">
                      <span className="text-[#e45140] mr-1">*</span>Mật khẩu
                    </label>
                    <div className="relative">
                      <input
                        type={showPassword ? 'text' : 'password'}
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        placeholder="••••••••"
                        className="w-full px-4 py-3 rounded-xl border border-gray-300 focus:border-[#284a32] focus:ring-1 focus:ring-[#284a32] outline-none bg-[#f2f5fa] pr-12 text-lg tracking-widest text-gray-800"
                      />
                      <button
                        type="button"
                        onClick={() => setShowPassword(!showPassword)}
                        className="absolute right-4 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600 transition-colors"
                      >
                        {showPassword ? <Eye className="w-5 h-5" /> : <EyeOff className="w-5 h-5" />}
                      </button>
                    </div>
                  </div>

                  <button
                    type="submit"
                    className="w-full bg-[#284a32] hover:bg-[#1e3825] text-white py-3.5 px-4 rounded-xl font-bold transition-all shadow-md hover:shadow-lg mt-2 text-[15px]"
                  >
                    Xác nhận đăng nhập
                  </button>
                </form>
              </div>
            )}
          </motion.div>
        </div>
      )}
    </AnimatePresence>
  );
}

