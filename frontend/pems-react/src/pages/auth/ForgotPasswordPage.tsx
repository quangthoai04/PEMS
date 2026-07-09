import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { authenticationApi } from '../../features/authentication/api/authenticationApi';
import { getAuthErrorMessage } from '../../features/authentication/api/authError';
import logo from '../../assets/images/2021-FPTU-Eng.png';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function ForgotPasswordPage() {
  const { t } = useTranslation(['loginModal', 'validation']);
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setMessage('');
    if (!EMAIL_RE.test(email.trim())) {
      setError(t('validation:emailInvalid'));
      return;
    }
    setSubmitting(true);
    try {
      await authenticationApi.forgotPassword(email.trim());
      // Always a generic, localized message — the API never reveals whether the email
      // exists, and its own `message` is Vietnamese-only.
      setMessage(t('loginModal:forgot.sent'));
    } catch (err) {
      setError(getAuthErrorMessage(err));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#eaf2fb] via-white to-[#fdeee5] px-4">
      <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-gray-100 p-7 md:p-9">
        <div className="flex flex-col items-center mb-6">
          <Link to="/"><img src={logo} alt="FPT University" className="h-14 object-contain mb-4" /></Link>
          <h1 className="text-[#004c91] text-xl font-black text-center">{t('loginModal:forgot.title')}</h1>
          <p className="text-gray-500 text-sm text-center mt-1">{t('loginModal:forgot.subtitle')}</p>
        </div>

        {message ? (
          <div className="space-y-4">
            <div className="p-3 bg-green-50 text-green-700 text-sm rounded-lg border border-green-100">{message}</div>
            <button
              onClick={() => navigate('/reset-password', { state: { email: email.trim() } })}
              className="w-full bg-[#004c91] hover:bg-[#003a6f] text-white py-3 rounded-xl font-bold"
            >
              {t('loginModal:forgot.haveCode')}
            </button>
            <Link to="/login" className="block text-center text-sm text-[#004c91] hover:underline">{t('loginModal:backToLogin')}</Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} noValidate className="space-y-4">
            {error && <div className="p-3 bg-red-50 text-red-600 text-sm rounded-lg border border-red-100">{error}</div>}
            <div>
              <label className="block text-gray-700 font-semibold text-sm mb-1.5">{t('loginModal:emailLabel')}</label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@fpt.edu.vn"
                className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
              />
            </div>
            <button
              type="submit"
              disabled={submitting}
              className="w-full bg-[#004c91] hover:bg-[#003a6f] disabled:opacity-60 text-white py-3 rounded-xl font-bold"
            >
              {submitting ? t('loginModal:forgot.sending') : t('loginModal:forgot.submit')}
            </button>
            <Link to="/login" className="block text-center text-sm text-[#004c91] hover:underline">{t('loginModal:backToLogin')}</Link>
          </form>
        )}
      </div>
    </div>
  );
}

export default ForgotPasswordPage;
