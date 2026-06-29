import React from 'react';
import { useNavigate } from 'react-router-dom';

export function NotFoundPage() {
  const navigate = useNavigate();

  return (
    <div className="flex flex-col items-center justify-center min-h-[70vh] px-4 text-center">
      <h1 className="text-6xl font-bold text-gray-200 mb-4">404</h1>
      <h2 className="text-2xl font-semibold text-gray-800 mb-2">Không tìm thấy nội dung</h2>
      <p className="text-gray-500 mb-8 max-w-md">
        Nội dung liên quan đến thông báo này không còn tồn tại hoặc đường dẫn không đúng.
      </p>
      <button
        onClick={() => navigate('/dashboard')}
        className="px-6 py-2 bg-[#004c91] text-white rounded-lg hover:bg-blue-800 transition-colors"
      >
        Quay lại Dashboard
      </button>
    </div>
  );
}
