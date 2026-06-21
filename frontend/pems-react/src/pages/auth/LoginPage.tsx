import React, { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../shared/hooks/useAuth';
import type { LoginPortal } from '../../features/authentication/types/authentication.types';
import logo from '../../assets/images/2021-FPTU-Eng.png';
import { InternalLoginForm, VisitorLoginForm } from '../../features/authentication/components/DualPortalLoginForms';

export function LoginPage() {
  const { isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [portal, setPortal] = useState<LoginPortal>('INTERNAL');

  const fromPath = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname;

  // Already signed in → leave the login page.
  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      navigate(fromPath ?? '/dashboard', { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, fromPath]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#eaf2fb] via-white to-[#fdeee5] px-4 font-sans">
      <div className="w-full max-w-[460px] bg-white/95 backdrop-blur-xl rounded-[2rem] shadow-xl shadow-blue-900/5 border border-white p-8 sm:p-10 relative z-10">
        <div className="flex flex-col items-center mb-8">
          <Link to="/">
            <img src={logo} alt="FPT University" className="h-14 object-contain mb-5 drop-shadow-sm" />
          </Link>
          <h2 className="text-[#004c91] text-2xl font-black text-center leading-tight">Đăng nhập PEMS</h2>
          <p className="text-gray-500 text-sm text-center mt-2">Partnership Engagement Management System</p>
        </div>

        <div className="mb-8">
          <div className="flex p-1.5 bg-gray-100/80 rounded-2xl">
            <button
              type="button"
              onClick={() => setPortal('INTERNAL')}
              className={`flex-1 py-2.5 text-[14px] font-bold rounded-xl transition-all duration-300 ${
                portal === 'INTERNAL'
                  ? 'bg-white text-[#004c91] shadow-[0_2px_8px_rgba(0,0,0,0.08)]'
                  : 'text-gray-500 hover:text-gray-800 hover:bg-gray-200/50'
              }`}
            >
              Nội bộ (Internal)
            </button>
            <button
              type="button"
              onClick={() => setPortal('VISITOR')}
              className={`flex-1 py-2.5 text-[14px] font-bold rounded-xl transition-all duration-300 ${
                portal === 'VISITOR'
                  ? 'bg-white text-[#004c91] shadow-[0_2px_8px_rgba(0,0,0,0.08)]'
                  : 'text-gray-500 hover:text-gray-800 hover:bg-gray-200/50'
              }`}
            >
              Khách (Visitor)
            </button>
          </div>
          <p className="text-[13px] text-gray-500 text-center mt-4 px-2">
            {portal === 'INTERNAL' 
              ? 'Dành cho Cán bộ, Giảng viên, và Sinh viên FPTU.' 
              : 'Dành cho Khách, Đối tác theo dõi thông tin chuyến thăm.'}
          </p>
        </div>

        <div className="min-h-[250px]">
          {portal === 'INTERNAL' ? (
            <InternalLoginForm fromPath={fromPath} />
          ) : (
            <VisitorLoginForm fromPath={fromPath} />
          )}
        </div>

        <p className="mt-8 text-center text-[12px] text-gray-400">
          Bằng việc đăng nhập, bạn đồng ý với quy định sử dụng hệ thống của FPT University.
        </p>
      </div>
    </div>
  );
}

export default LoginPage;
