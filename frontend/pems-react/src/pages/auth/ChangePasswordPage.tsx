import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../../shared/hooks/useAuth';
import { getAuthErrorMessage } from '../../features/authentication/api/authError';
import { isStrongPassword, PASSWORD_REQUIREMENTS } from '../../shared/utils/passwordPolicy';
import logo from '../../assets/images/2021-FPTU-Eng.png';

export function ChangePasswordPage() {
  const { user, changePassword, logout } = useAuth();
  const navigate = useNavigate();

  const forced = !!(user?.mustChangePassword || user?.mustSetPassword);
  const requireCurrent = !user?.mustSetPassword;

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (requireCurrent && !currentPassword) {
      setError('Vui lòng nhập mật khẩu hiện tại.');
      return;
    }
    if (!isStrongPassword(newPassword)) {
      setError(PASSWORD_REQUIREMENTS);
      return;
    }
    if (newPassword !== confirmPassword) {
      setError('Mật khẩu xác nhận không khớp.');
      return;
    }

    setSubmitting(true);
    try {
      await changePassword({
        currentPassword: requireCurrent ? currentPassword : undefined,
        newPassword,
        confirmPassword,
      });
      setSuccess('Đổi mật khẩu thành công.');
      setTimeout(() => navigate('/dashboard', { replace: true }), 1200);
    } catch (err) {
      setError(getAuthErrorMessage(err, 'Không thể đổi mật khẩu.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#eaf2fb] via-white to-[#fdeee5] px-4">
      <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-gray-100 p-7 md:p-9">
        <div className="flex flex-col items-center mb-6">
          <img src={logo} alt="FPT University" className="h-14 object-contain mb-4" />
          <h1 className="text-[#004c91] text-xl font-black text-center">Đổi mật khẩu</h1>
          {forced && (
            <p className="text-amber-600 text-sm text-center mt-1 font-medium">
              Bạn cần đặt/đổi mật khẩu trước khi tiếp tục.
            </p>
          )}
        </div>

        {success ? (
          <div className="p-3 bg-green-50 text-green-700 text-sm rounded-lg border border-green-100">{success}</div>
        ) : (
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && <div className="p-3 bg-red-50 text-red-600 text-sm rounded-lg border border-red-100">{error}</div>}

            {requireCurrent && (
              <div>
                <label className="block text-gray-700 font-semibold text-sm mb-1.5">Mật khẩu hiện tại</label>
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  autoComplete="current-password"
                  className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
                />
              </div>
            )}

            <div>
              <label className="block text-gray-700 font-semibold text-sm mb-1.5">Mật khẩu mới</label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  autoComplete="new-password"
                  className="w-full px-4 py-2.5 pr-11 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((s) => !s)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                >
                  {showPassword ? <Eye className="w-5 h-5" /> : <EyeOff className="w-5 h-5" />}
                </button>
              </div>
              <p className="mt-1 text-xs text-gray-400">{PASSWORD_REQUIREMENTS}</p>
            </div>

            <div>
              <label className="block text-gray-700 font-semibold text-sm mb-1.5">Xác nhận mật khẩu mới</label>
              <input
                type={showPassword ? 'text' : 'password'}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                autoComplete="new-password"
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
              />
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="w-full bg-[#004c91] hover:bg-[#003a6f] disabled:opacity-60 text-white py-3 rounded-xl font-bold"
            >
              {submitting ? 'Đang xử lý...' : 'Đổi mật khẩu'}
            </button>

            <button
              type="button"
              onClick={() => (forced ? logout().then(() => navigate('/login', { replace: true })) : navigate(-1))}
              className="w-full text-sm text-gray-500 hover:text-gray-700"
            >
              {forced ? 'Đăng xuất' : 'Hủy'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}

export default ChangePasswordPage;
