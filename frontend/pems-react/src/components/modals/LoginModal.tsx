/**
 * Component LoginModal
 * Màn hình đăng nhập hệ thống dành cho nhân viên và đối tác.
 */

// Đây là component hiển thị cửa sổ bật lên để đăng nhập
import React, { useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { X, Eye, EyeOff, Mail } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import logo from '../../assets/images/2021-FPTU-Eng.png';

interface LoginModalProps {
  isOpen: boolean;
  onClose: () => void;
}

const campuses = [
  'Hà Nội',
  'Đà Nẵng',
  'Cần Thơ',
  'Quy Nhơn',
  'Hồ Chí Minh'
];

const mockAccounts = [
  { account: 'student', password: '123', role: 'Student' },
  { account: 'staff', password: '123', role: 'Staff' },
  { account: 'admin', password: '123', role: 'Admin' },
  { account: 'ho', password: '123', role: 'HO' },
  { account: 'dept_leader', password: '123', role: 'Dept', subRole: 'Leader', departmentId: 1 },
  { account: 'dept_staff', password: '123', role: 'Dept', subRole: 'Staff', departmentId: 1 },
  { account: 'staff_leader', password: '123', role: 'Staff', subRole: 'Leader' },
  { account: 'visitor', password: '123', role: 'Visitor' }
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

  const handleDevLogin = (e: React.FormEvent) => {
    e.preventDefault();
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

    const user = mockAccounts.find(acc => acc.account === email.toLowerCase() && acc.password === password);
    if (user) {
      // Get mock name based on role
      let mockName = 'Nguyễn Văn A';
      if (user.role === 'Staff') mockName = 'Nguyễn Văn B';
      if (user.role === 'Admin') mockName = 'Nguyễn Văn C';
      if (user.role === 'HO') mockName = 'Nguyễn Văn D';
      if (user.account === 'dept_leader') mockName = 'Nguyễn Văn Trưởng Phòng';
      if (user.account === 'dept_staff') mockName = 'Nguyễn Văn Nhân Viên';
      if (user.account === 'staff_leader') mockName = 'Nguyễn Văn Trưởng Phòng CTSV';
      if (user.role === 'Visitor') mockName = 'Nguyễn Khách Trọng';

      // Save to localStorage
      localStorage.setItem('currentUser', JSON.stringify({
        ...user,
        campus: selectedCampus,
        name: mockName
      }));
      handleClose();
      navigate('/');
    } else {
      setError('Tài khoản hoặc mật khẩu không chính xác');
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
                      defaultValue=""
                      className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none appearance-none bg-white text-gray-600 font-medium text-[14px]"
                    >
                      <option value="" disabled>Vui lòng chọn cơ sở học tập</option>
                      {campuses.map(c => (
                        <option key={c} value={c}>{c}</option>
                      ))}
                    </select>
                    <div className="absolute inset-y-0 right-4 flex items-center pointer-events-none text-gray-400">
                      <svg width="12" height="8" viewBox="0 0 12 8" fill="none" xmlns="http://www.w3.org/2000/svg">
                        <path d="M1.41 0.589966L6 5.16997L10.59 0.589966L12 1.99997L6 7.99997L0 1.99997L1.41 0.589966Z" fill="currentColor"/>
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
                          <option key={c} value={c} className="text-gray-800">{c}</option>
                        ))}
                      </select>
                      <div className="absolute inset-y-0 right-4 flex items-center pointer-events-none text-gray-400">
                        <svg width="12" height="8" viewBox="0 0 12 8" fill="none" xmlns="http://www.w3.org/2000/svg">
                          <path d="M1.41 0.589966L6 5.16997L10.59 0.589966L12 1.99997L6 7.99997L0 1.99997L1.41 0.589966Z" fill="currentColor"/>
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

