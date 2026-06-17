import React, { useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Eye, EyeOff } from 'lucide-react';
import { authenticationApi } from '../../features/authentication/api/authenticationApi';
import { getAuthErrorMessage } from '../../features/authentication/api/authError';
import { isStrongPassword, PASSWORD_REQUIREMENTS } from '../../shared/utils/passwordPolicy';
import logo from '../../assets/images/2021-FPTU-Eng.png';

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const prefillEmail = (location.state as { email?: string } | null)?.email ?? '';

  const [email, setEmail] = useState(prefillEmail);
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    if (!email.trim() || !code.trim()) {
      setError('Vui lòng nhập email và mã đặt lại.');
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
      const res = await authenticationApi.resetPassword({
        email: email.trim(),
        otpOrToken: code.trim(),
        newPassword,
        confirmPassword,
      });
      setSuccess(res.message);
      setTimeout(() => navigate('/login', { replace: true }), 1500);
    } catch (err) {
      setError(getAuthErrorMessage(err, 'Invalid or expired reset code.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#eaf2fb] via-white to-[#fdeee5] px-4">
      <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-gray-100 p-7 md:p-9">
        <div className="flex flex-col items-center mb-6">
          <Link to="/"><img src={logo} alt="FPT University" className="h-14 object-contain mb-4" /></Link>
          <h1 className="text-[#004c91] text-xl font-black text-center">Đặt lại mật khẩu</h1>
          <p className="text-gray-500 text-sm text-center mt-1">Nhập mã đã nhận và mật khẩu mới.</p>
        </div>

        {success ? (
          <div className="p-3 bg-green-50 text-green-700 text-sm rounded-lg border border-green-100">{success}</div>
        ) : (
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && <div className="p-3 bg-red-50 text-red-600 text-sm rounded-lg border border-red-100">{error}</div>}

            <div>
              <label className="block text-gray-700 font-semibold text-sm mb-1.5">Email</label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
              />
            </div>

            <div>
              <label className="block text-gray-700 font-semibold text-sm mb-1.5">Mã đặt lại (OTP)</label>
              <input
                type="text"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                placeholder="6 chữ số"
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none tracking-widest"
              />
            </div>

            <div>
              <label className="block text-gray-700 font-semibold text-sm mb-1.5">Mật khẩu mới</label>
              <div className="relative">
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
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
              <label className="block text-gray-700 font-semibold text-sm mb-1.5">Xác nhận mật khẩu</label>
              <input
                type={showPassword ? 'text' : 'password'}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
              />
            </div>

            <button
              type="submit"
              disabled={submitting}
              className="w-full bg-[#004c91] hover:bg-[#003a6f] disabled:opacity-60 text-white py-3 rounded-xl font-bold"
            >
              {submitting ? 'Đang xử lý...' : 'Đặt lại mật khẩu'}
            </button>
            <Link to="/login" className="block text-center text-sm text-[#004c91] hover:underline">Quay lại đăng nhập</Link>
          </form>
        )}
      </div>
    </div>
  );
}

export default ResetPasswordPage;
