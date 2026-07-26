import React, { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { AlertTriangle } from 'lucide-react';
import { useAuth } from '../../shared/hooks/useAuth';
import logo from '../../assets/images/2021-FPTU-Eng.png';
import { LoginForm } from '../../features/authentication/components/LoginForm';
import { FORCED_LOGOUT_REASON_KEY } from '../../shared/api/httpClient';

export function LoginPage() {
  const { t } = useTranslation(['loginModal']);
  const { isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  // UC-86 force-logout: explain WHY the user landed here (campus disabled while signed in).
  // Read once and cleared so a normal visit to /login never shows a stale banner.
  const [forcedLogoutReason] = useState<string | null>(() => {
    const reason = sessionStorage.getItem(FORCED_LOGOUT_REASON_KEY);
    if (reason) sessionStorage.removeItem(FORCED_LOGOUT_REASON_KEY);
    return reason;
  });

  // Already signed in → leave the login page.
  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      navigate('/', { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#eaf2fb] via-white to-[#fdeee5] px-4 font-sans">
      <div className="w-full max-w-[460px] bg-white/95 backdrop-blur-xl rounded-[2rem] shadow-xl shadow-blue-900/5 border border-white p-8 sm:p-10 relative z-10">
        <div className="flex flex-col items-center mb-8">
          <Link to="/">
            <img src={logo} alt="FPT University" className="h-14 object-contain mb-5 drop-shadow-sm" />
          </Link>
          <h2 className="text-[#004c91] text-2xl font-black text-center leading-tight">{t('loginModal:title')}</h2>
          <p className="text-gray-500 text-sm text-center mt-2">{t('loginModal:subtitle')}</p>
        </div>

        {forcedLogoutReason && (
          <div role="alert" className="mb-6 flex items-start gap-2.5 rounded-2xl border border-amber-200 bg-amber-50 p-4">
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" aria-hidden="true" />
            <p className="text-[13px] font-medium leading-relaxed text-amber-800">{forcedLogoutReason}</p>
          </div>
        )}

        <div className="min-h-[250px]">
          <LoginForm />
        </div>

        <p className="mt-8 text-center text-[12px] text-gray-400">
          {t('loginModal:termsNotice')}
        </p>
      </div>
    </div>
  );
}

export default LoginPage;
