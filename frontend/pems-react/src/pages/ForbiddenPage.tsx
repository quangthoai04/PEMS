import React from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ShieldAlert } from 'lucide-react';

export function ForbiddenPage() {
  const navigate = useNavigate();
  return (
    <div className="min-h-screen flex items-center justify-center bg-[#fafafa] px-4">
      <div className="text-center max-w-md">
        <div className="w-16 h-16 mx-auto rounded-2xl bg-red-50 text-red-500 flex items-center justify-center mb-5">
          <ShieldAlert className="w-8 h-8" />
        </div>
        <h1 className="text-3xl font-black text-[#004c91] mb-2">403</h1>
        <p className="text-gray-700 font-semibold mb-1">Bạn không có quyền truy cập chức năng này.</p>
        <p className="text-gray-500 text-sm mb-6">
          Nếu bạn cho rằng đây là nhầm lẫn, vui lòng liên hệ quản trị viên hệ thống.
        </p>
        <div className="flex items-center justify-center gap-3">
          <button
            onClick={() => navigate('/dashboard', { replace: true })}
            className="px-5 py-2.5 bg-[#004c91] hover:bg-[#003a6f] text-white rounded-xl font-bold text-sm"
          >
            Về trang quản trị
          </button>
          <Link to="/" className="px-5 py-2.5 border border-gray-300 text-gray-700 rounded-xl font-bold text-sm hover:bg-gray-50">
            Trang chủ
          </Link>
        </div>
      </div>
    </div>
  );
}

export default ForbiddenPage;
