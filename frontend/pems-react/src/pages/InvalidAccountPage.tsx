import React from 'react';
import { useNavigate } from 'react-router-dom';
import { UserX } from 'lucide-react';
import { useAuth } from '../shared/hooks/useAuth';
import { useTranslation } from 'react-i18next';

/**
 * Trang /invalid-account
 * Hiển thị khi tài khoản chưa được cấu hình vai trò hợp lệ:
 * - STAFF / DEPARTMENT thiếu sub_role (Leader/Staff)
 * - roleCode không hợp lệ hoặc thiếu role
 * Không cấp quyền ngầm, không làm trắng màn hình.
 */
export function InvalidAccountPage() {
  const navigate = useNavigate();
  const { logout } = useAuth();
  const { t } = useTranslation(['errors']);

  const handleLogout = async () => {
    await logout();
    navigate('/', { replace: true });
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#fafafa] px-4">
      <div className="text-center max-w-md">
        <div className="w-16 h-16 mx-auto rounded-2xl bg-amber-50 text-amber-500 flex items-center justify-center mb-5">
          <UserX className="w-8 h-8" />
        </div>
        <h1 className="text-2xl font-black text-[#004c91] mb-2">{t('errors:invalidAccount.title')}</h1>
        <p className="text-gray-700 font-semibold mb-1">
          {t('errors:invalidAccount.message')}
        </p>
        <p className="text-gray-500 text-sm mb-6">
          {t('errors:invalidAccount.instruction')}
        </p>
        <div className="flex items-center justify-center gap-3">
          <button
            onClick={handleLogout}
            className="px-5 py-2.5 bg-[#004c91] hover:bg-[#003a6f] text-white rounded-xl font-bold text-sm"
          >
            {t('errors:invalidAccount.logout')}
          </button>
          <button
            onClick={() => window.location.reload()}
            className="px-5 py-2.5 border border-gray-300 text-gray-700 rounded-xl font-bold text-sm hover:bg-gray-50"
          >
            {t('errors:invalidAccount.reload')}
          </button>
        </div>
      </div>
    </div>
  );
}

export default InvalidAccountPage;
