import React, { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Eye, EyeOff } from 'lucide-react';
import { useAuth } from '../../shared/hooks/useAuth';
import { getDashboardRoute } from '../../shared/auth/dashboardRoute';
import { getAuthErrorMessage } from '../../features/authentication/api/authError';
import type { LoginPortal } from '../../features/authentication/types/authentication.types';
import logo from '../../assets/images/2021-FPTU-Eng.png';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function LoginPage() {
  const { login, isAuthenticated, isLoading } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [portal, setPortal] = useState<LoginPortal>('INTERNAL');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const fromPath = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname;

  // Already signed in → leave the login page.
  useEffect(() => {
    if (!isLoading && isAuthenticated) {
      navigate(fromPath ?? '/dashboard', { replace: true });
    }
  }, [isAuthenticated, isLoading, navigate, fromPath]);

  const validate = () => {
    const errors: { email?: string; password?: string } = {};
    if (!email.trim()) errors.email = 'Vui lòng nhập email.';
    else if (!EMAIL_RE.test(email.trim())) errors.email = 'Email không hợp lệ.';
    if (!password) errors.password = 'Vui lòng nhập mật khẩu.';
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError('');
    if (!validate()) return;

    setSubmitting(true);
    try {
      const user = await login(email.trim(), password, portal);
      if (user.mustChangePassword || user.mustSetPassword) {
        navigate('/change-password', { replace: true });
      } else {
        navigate(fromPath ?? getDashboardRoute(user), { replace: true });
      }
    } catch (err) {
      setFormError(getAuthErrorMessage(err, 'Invalid email or password.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-[#eaf2fb] via-white to-[#fdeee5] px-4">
      <div className="w-full max-w-[440px] bg-white rounded-2xl shadow-xl border border-gray-100 p-7 md:p-9">
        <div className="flex flex-col items-center mb-6">
          <Link to="/"><img src={logo} alt="FPT University" className="h-16 object-contain mb-4" /></Link>
          <h1 className="text-[#004c91] text-xl font-black text-center leading-tight">Đăng nhập hệ thống PEMS</h1>
          <p className="text-gray-500 text-sm text-center mt-1">Partnership Engagement Management System</p>
        </div>

        {formError && (
          <div className="mb-4 p-3 bg-red-50 text-red-600 text-sm rounded-lg border border-red-100" role="alert">
            {formError}
          </div>
        )}

        <form onSubmit={handleSubmit} noValidate className="space-y-4">
          <div>
            <label className="block text-gray-700 font-semibold text-sm mb-1.5">Cổng đăng nhập</label>
            <div className="grid grid-cols-2 gap-2">
              {(['INTERNAL', 'VISITOR'] as LoginPortal[]).map((p) => (
                <button
                  type="button"
                  key={p}
                  onClick={() => setPortal(p)}
                  className={`py-2.5 rounded-xl text-sm font-bold border transition-colors ${
                    portal === p
                      ? 'bg-[#004c91] text-white border-[#004c91]'
                      : 'bg-white text-gray-600 border-gray-300 hover:border-[#004c91]'
                  }`}
                >
                  {p === 'INTERNAL' ? 'Nội bộ (Internal)' : 'Khách (Visitor)'}
                </button>
              ))}
            </div>
          </div>

          <div>
            <label className="block text-gray-700 font-semibold text-sm mb-1.5">Email</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="you@fpt.edu.vn"
              autoComplete="username"
              className="w-full px-4 py-2.5 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
            />
            {fieldErrors.email && <p className="mt-1 text-xs text-red-600">{fieldErrors.email}</p>}
          </div>

          <div>
            <label className="block text-gray-700 font-semibold text-sm mb-1.5">Mật khẩu</label>
            <div className="relative">
              <input
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                autoComplete="current-password"
                className="w-full px-4 py-2.5 pr-11 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none"
              />
              <button
                type="button"
                onClick={() => setShowPassword((s) => !s)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              >
                {showPassword ? <Eye className="w-5 h-5" /> : <EyeOff className="w-5 h-5" />}
              </button>
            </div>
            {fieldErrors.password && <p className="mt-1 text-xs text-red-600">{fieldErrors.password}</p>}
          </div>

          <div className="flex justify-end">
            <Link to="/forgot-password" className="text-sm text-[#004c91] hover:underline font-medium">
              Quên mật khẩu?
            </Link>
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="w-full bg-[#004c91] hover:bg-[#003a6f] disabled:opacity-60 disabled:cursor-not-allowed text-white py-3 rounded-xl font-bold transition-colors shadow-sm"
          >
            {submitting ? 'Đang đăng nhập...' : 'Đăng nhập'}
          </button>
        </form>

        <div className="my-5 flex items-center gap-3 text-gray-400 text-xs">
          <div className="h-px flex-1 bg-gray-200" />
          <span>HOẶC</span>
          <div className="h-px flex-1 bg-gray-200" />
        </div>

        <GoogleSignInButton portal={portal} onError={setFormError} fromPath={fromPath} />

        <p className="mt-6 text-center text-xs text-gray-400">
          Bằng việc đăng nhập, bạn đồng ý với quy định sử dụng hệ thống của FPT University.
        </p>
      </div>
    </div>
  );
}

/**
 * Renders Google Sign-In when VITE_GOOGLE_CLIENT_ID is configured. The Google
 * Identity Services script (gsi/client) returns an ID token, which we exchange
 * with the backend (/api/auth/google).
 */
function GoogleSignInButton({
  portal,
  onError,
  fromPath,
}: {
  portal: LoginPortal;
  onError: (msg: string) => void;
  fromPath?: string;
}) {
  const { loginWithGoogle } = useAuth();
  const navigate = useNavigate();
  const clientId = import.meta.env.VITE_GOOGLE_CLIENT_ID as string | undefined;
  const containerRef = React.useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!clientId) return;

    const handleCredential = async (response: { credential?: string }) => {
      if (!response.credential) return;
      try {
        const user = await loginWithGoogle(response.credential, portal);
        if (user.mustChangePassword || user.mustSetPassword) navigate('/change-password', { replace: true });
        else navigate(fromPath ?? getDashboardRoute(user), { replace: true });
      } catch (err) {
        onError(getAuthErrorMessage(err, 'Unable to sign in with this account.'));
      }
    };

    const init = () => {
      const google = (window as any).google;
      if (!google?.accounts?.id || !containerRef.current) return;
      google.accounts.id.initialize({ client_id: clientId, callback: handleCredential });
      google.accounts.id.renderButton(containerRef.current, { theme: 'outline', size: 'large', width: 360 });
    };

    if ((window as any).google?.accounts?.id) {
      init();
      return;
    }

    const existing = document.getElementById('google-gsi-script');
    if (existing) {
      existing.addEventListener('load', init);
      return () => existing.removeEventListener('load', init);
    }

    const script = document.createElement('script');
    script.src = 'https://accounts.google.com/gsi/client';
    script.async = true;
    script.defer = true;
    script.id = 'google-gsi-script';
    script.onload = init;
    document.body.appendChild(script);
  }, [clientId, portal, fromPath, loginWithGoogle, navigate, onError]);

  if (!clientId) {
    return (
      <button
        type="button"
        disabled
        title="Cấu hình VITE_GOOGLE_CLIENT_ID để bật đăng nhập Google"
        className="w-full flex items-center justify-center gap-3 border border-gray-300 text-gray-400 py-2.5 rounded-xl font-medium cursor-not-allowed"
      >
        <svg className="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
          <path d="M12.48 10.92v3.28h7.84c-.24 1.84-.853 3.187-1.787 4.133-1.147 1.147-2.933 2.4-6.053 2.4-4.827 0-8.6-3.893-8.6-8.72s3.773-8.72 8.6-8.72c2.6 0 4.507 1.027 5.907 2.347l2.307-2.307C18.747 1.44 16.133 0 12.48 0 5.867 0 .307 5.387.307 12s5.56 12 12.173 12c3.573 0 6.267-1.173 8.373-3.36 2.16-2.16 2.84-5.213 2.84-7.667 0-.76-.053-1.467-.173-2.053H12.48z" />
        </svg>
        Sign in with Google
      </button>
    );
  }

  return <div ref={containerRef} className="flex justify-center" />;
}

export default LoginPage;
