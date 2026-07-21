import React from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle } from 'lucide-react';

export default function UnsupportedVersionPage() {
  const navigate = useNavigate();

  return (
    <div className="min-h-[60vh] flex items-center justify-center p-4">
      <div className="max-w-md w-full bg-white rounded-lg shadow-sm border border-gray-100 p-8 text-center">
        <div className="mx-auto w-16 h-16 bg-red-50 rounded-full flex items-center justify-center mb-6">
          <AlertCircle className="w-8 h-8 text-red-500" />
        </div>
        
        <h2 className="text-xl font-bold text-gray-900 mb-3">
          Phiên bản biểu mẫu không được hỗ trợ
        </h2>
        
        <p className="text-gray-600 mb-8">
          Đơn tham quan này sử dụng phiên bản biểu mẫu cũ và không còn được hỗ trợ trong hệ thống mới. Vui lòng liên hệ quản trị viên hoặc tạo đơn tham quan mới.
        </p>

        <div className="flex gap-4 justify-center">
          <button
            onClick={() => navigate('/dashboard/visit')}
            className="px-6 py-2 bg-gray-100 text-gray-700 rounded-md font-semibold hover:bg-gray-200 transition-colors"
          >
            Quay lại danh sách
          </button>
        </div>
      </div>
    </div>
  );
}
