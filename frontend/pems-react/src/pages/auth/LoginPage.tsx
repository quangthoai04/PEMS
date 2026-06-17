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
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#eaf2fb] via-white to-[#fdeee5] px-4">
      <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-gray-100 p-7 md:p-9">
        <div className="flex flex-col items-center mb-6">
          <Link to="/"><img src={logo} alt="FPT University" className="h-16 object-contain mb-4" /></Link>
          <h1 className="text-[#004c91] text-xl font-black text-center leading-tight">Đăng nhập hệ thống PEMS</h1>
          <p className="text-gray-500 text-sm text-center mt-1">Partnership Engagement Management System</p>
        </div>

        <div className="mb-6">
          <div className="flex p-1 bg-gray-100 rounded-xl">
            <button
              type="button"
              onClick={() => setPortal('INTERNAL')}
              className={`flex-1 py-2 text-sm font-bold rounded-lg transition-all ${
                portal === 'INTERNAL'
                  ? 'bg-white text-[#004c91] shadow-sm'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Nội bộ (Internal)
            </button>
            <button
              type="button"
              onClick={() => setPortal('VISITOR')}
              className={`flex-1 py-2 text-sm font-bold rounded-lg transition-all ${
                portal === 'VISITOR'
                  ? 'bg-white text-[#004c91] shadow-sm'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              Khách (Visitor)
            </button>
          </div>
          <p className="text-xs text-gray-500 text-center mt-3">
            {portal === 'INTERNAL' 
              ? 'Dành cho Cán bộ, Giảng viên, và Sinh viên FPTU.' 
              : 'Dành cho Khách, Đối tác theo dõi thông tin chuyến thăm.'}
          </p>
        </div>

        {portal === 'INTERNAL' ? (
          <InternalLoginForm fromPath={fromPath} />
        ) : (
          <VisitorLoginForm fromPath={fromPath} />
        )}

        <p className="mt-6 text-center text-xs text-gray-400">
          Bằng việc đăng nhập, bạn đồng ý với quy định sử dụng hệ thống của FPT University.
        </p>
      </div>
    </div>
  );
}

export default LoginPage;
