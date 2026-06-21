import React, { useEffect, useState, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Eye, EyeOff, Mail, Lock, Building2 } from 'lucide-react';
import { useAuth } from '../../../shared/hooks/useAuth';
import { getDashboardRoute } from '../../../shared/auth/dashboardRoute';
import { getAuthErrorMessage } from '../api/authError';
import { authenticationApi } from '../api/authenticationApi';
import { useActiveCampuses } from '../hooks/useActiveCampuses';
import { AUTH_CONFIG } from '../../../shared/constants/auth';
import type { CampusOption, LoginPortal } from '../types/authentication.types';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function InternalLoginForm({ fromPath, onSuccess }: { fromPath?: string; onSuccess?: () => void }) {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [selectedCampusId, setSelectedCampusId] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string; campus?: string }>({});
  const [formError, setFormError] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const { campuses, loading: loadingCampuses, error: campusError, reload: reloadCampuses } = useActiveCampuses('INTERNAL');

  // Close dropdown on outside click
  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsDropdownOpen(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const validate = () => {
    const errors: { email?: string; password?: string; campus?: string } = {};
    if (!email.trim()) errors.email = 'Vui lòng nhập email.';
    else if (!EMAIL_RE.test(email.trim())) errors.email = 'Email không hợp lệ.';
    if (!password) errors.password = 'Vui lòng nhập mật khẩu.';
    
    if (loadingCampuses) errors.campus = 'Vui lòng đợi hệ thống tải danh sách cơ sở.';
    else if (campusError) errors.campus = 'Không thể đăng nhập vì chưa tải được danh sách cơ sở.';
    else if (!selectedCampusId) errors.campus = 'Vui lòng chọn cơ sở.';
    
    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError('');
    if (!validate()) return;

    setSubmitting(true);
    try {
      const user = await login(email.trim(), password, 'INTERNAL', selectedCampusId);
      if (onSuccess) onSuccess();
      if (user.mustChangePassword || user.mustSetPassword) {
        navigate('/change-password', { replace: true });
      } else {
        navigate(fromPath ?? getDashboardRoute(user), { replace: true });
      }
    } catch (err) {
      setFormError(getAuthErrorMessage(err, 'Email, mật khẩu hoặc cơ sở không chính xác.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      {formError && (
        <div className="mb-4 p-3 bg-red-50 text-red-600 text-[13px] rounded-lg border border-red-100" role="alert">
          {formError}
        </div>
      )}

      {/* Campus selector */}
      <div className="mb-2.5">
        <label className="block text-gray-700 font-semibold text-[13px] mb-0.5">Cơ sở (Campus)</label>
        {campusError ? (
          <div className="flex flex-col gap-2">
            <p className="text-sm text-red-600">{campusError}</p>
            <button
              type="button"
              onClick={reloadCampuses}
              className="self-start px-3 py-1.5 text-sm bg-red-50 text-red-700 font-medium rounded-lg hover:bg-red-100 transition-colors"
            >
              Thử lại
            </button>
          </div>
        ) : (
          <div className="relative" ref={dropdownRef}>
            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none z-10 text-gray-400">
              <Building2 className="w-[18px] h-[18px]" strokeWidth={1.5} />
            </div>
            
            <div 
              onClick={() => !loadingCampuses && setIsDropdownOpen(!isDropdownOpen)}
              className={`w-full pl-9 pr-4 py-2 rounded-xl border border-gray-300 ${isDropdownOpen ? 'border-[#004c91] ring-1 ring-[#004c91]' : ''} outline-none bg-white text-[14px] ${loadingCampuses ? 'bg-gray-50 text-gray-500 cursor-not-allowed' : 'cursor-pointer'} shadow-sm transition-all flex items-center justify-between`}
            >
              <span className={!selectedCampusId ? 'text-gray-500' : 'text-gray-900 truncate'}>
                {loadingCampuses 
                  ? 'Đang tải danh sách cơ sở...' 
                  : selectedCampusId 
                    ? campuses.find(c => c.campusId === selectedCampusId)?.campusName + ` (${campuses.find(c => c.campusId === selectedCampusId)?.campusCode})` 
                    : `-- Chọn cơ sở (${campuses.length}) --`
                }
              </span>
              <svg className={`w-4 h-4 text-gray-500 transition-transform duration-200 ${isDropdownOpen ? 'rotate-180' : ''}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
              </svg>
            </div>

            {isDropdownOpen && !loadingCampuses && (
              <div className="absolute z-50 w-full mt-1 bg-white border border-gray-100 rounded-xl shadow-[0_4px_20px_-4px_rgba(0,0,0,0.1)] max-h-60 overflow-auto py-1.5 animate-in fade-in slide-in-from-top-2">
                <div 
                  onClick={() => { setSelectedCampusId(''); setIsDropdownOpen(false); }}
                  className={`px-4 py-2 text-[14px] cursor-pointer transition-colors ${!selectedCampusId ? 'bg-blue-50 text-[#004c91] font-medium' : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'}`}
                >
                  -- Chọn cơ sở ({campuses.length}) --
                </div>
                {campuses.map((c) => (
                  <div
                    key={c.campusId}
                    onClick={() => { setSelectedCampusId(c.campusId); setIsDropdownOpen(false); }}
                    className={`px-4 py-2 text-[14px] cursor-pointer transition-colors ${selectedCampusId === c.campusId ? 'bg-[#004c91] text-white font-medium' : 'text-gray-600 hover:bg-blue-50 hover:text-[#004c91]'}`}
                  >
                    {c.campusName} ({c.campusCode})
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
        {fieldErrors.campus && <p className="mt-1 text-xs text-red-600">{fieldErrors.campus}</p>}
      </div>

      {AUTH_CONFIG.enablePasswordLogin && (
        <form onSubmit={handleSubmit} noValidate className="space-y-2.5">
          <div>
            <label className="block text-gray-700 font-semibold text-[13px] mb-0.5">Email</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-gray-400">
                <Mail className="w-[18px] h-[18px]" strokeWidth={1.5} />
              </div>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@fpt.edu.vn"
                autoComplete="username"
                className="w-full pl-9 pr-4 py-2 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-[14px] shadow-sm transition-all"
              />
            </div>
            {fieldErrors.email && <p className="mt-1 text-xs text-red-600">{fieldErrors.email}</p>}
          </div>

          <div>
            <label className="block text-gray-700 font-semibold text-[13px] mb-0.5">Mật khẩu</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-gray-400">
                <Lock className="w-[18px] h-[18px]" strokeWidth={1.5} />
              </div>
              <input
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                autoComplete="current-password"
                className="w-full pl-9 pr-11 py-2 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-[14px] shadow-sm transition-all"
              />
              <button
                type="button"
                onClick={() => setShowPassword((s) => !s)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              >
                {showPassword ? <Eye className="w-[18px] h-[18px]" strokeWidth={1.5} /> : <EyeOff className="w-[18px] h-[18px]" strokeWidth={1.5} />}
              </button>
            </div>
            {fieldErrors.password && <p className="mt-1 text-xs text-red-600">{fieldErrors.password}</p>}
          </div>

          <div className="flex justify-end">
            <Link to="/forgot-password" onClick={onSuccess} className="text-[13px] text-[#004c91] hover:underline font-medium">
              Quên mật khẩu?
            </Link>
          </div>

          <button
            type="submit"
            disabled={submitting || loadingCampuses || !!campusError}
            className="w-full h-[40px] bg-gradient-to-r from-[#004c91] to-[#005baa] hover:from-[#003a6f] hover:to-[#004c91] disabled:opacity-60 disabled:cursor-not-allowed text-white rounded-[4px] font-medium transition-all shadow-sm text-[14px] flex justify-center items-center gap-2"
          >
            {submitting ? 'Đang xử lý...' : 'Đăng nhập'}
          </button>
        </form>
      )}

      {AUTH_CONFIG.enablePasswordLogin && AUTH_CONFIG.enableGoogleSso && (
        <div className="my-4 flex items-center gap-3 text-gray-400 text-xs">
          <div className="h-px flex-1 bg-gray-200" />
          <span>HOẶC</span>
          <div className="h-px flex-1 bg-gray-200" />
        </div>
      )}

      {AUTH_CONFIG.enableGoogleSso && (
        <GoogleSignInButton
          portal="INTERNAL"
          selectedCampusId={selectedCampusId}
          onError={setFormError}
          fromPath={fromPath}
          onSuccess={onSuccess}
          onValidateCampus={() => {
            if (loadingCampuses) {
              setFieldErrors(prev => ({ ...prev, campus: 'Vui lòng đợi hệ thống tải danh sách cơ sở.' }));
              return false;
            }
            if (campusError) {
              setFieldErrors(prev => ({ ...prev, campus: 'Không thể đăng nhập bằng Google vì chưa tải được danh sách cơ sở.' }));
              return false;
            }
            if (!selectedCampusId) {
              setFieldErrors(prev => ({ ...prev, campus: 'Vui lòng chọn cơ sở trước khi đăng nhập bằng Google.' }));
              return false;
            }
            return true;
          }}
        />
      )}
    </>
  );
}

export function VisitorLoginForm({ fromPath, onSuccess }: { fromPath?: string; onSuccess?: () => void }) {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [formError, setFormError] = useState('');
  const [submitting, setSubmitting] = useState(false);

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
      const user = await login(email.trim(), password, 'VISITOR');
      if (onSuccess) onSuccess();
      if (user.mustChangePassword || user.mustSetPassword) {
        navigate('/change-password', { replace: true });
      } else {
        navigate(fromPath ?? getDashboardRoute(user), { replace: true });
      }
    } catch (err) {
      setFormError(getAuthErrorMessage(err, 'Email hoặc mật khẩu không chính xác.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      {formError && (
        <div className="mb-4 p-3 bg-red-50 text-red-600 text-[13px] rounded-lg border border-red-100" role="alert">
          {formError}
        </div>
      )}

      {AUTH_CONFIG.enablePasswordLogin && (
        <form onSubmit={handleSubmit} noValidate className="space-y-2.5">
          <div>
            <label className="block text-gray-700 font-semibold text-[13px] mb-0.5">Email</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-gray-400">
                <Mail className="w-[18px] h-[18px]" strokeWidth={1.5} />
              </div>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="guest@example.com"
                autoComplete="username"
                className="w-full pl-9 pr-4 py-2 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-[14px] shadow-sm transition-all"
              />
            </div>
            {fieldErrors.email && <p className="mt-1 text-xs text-red-600">{fieldErrors.email}</p>}
          </div>

          <div>
            <label className="block text-gray-700 font-semibold text-[13px] mb-0.5">Mật khẩu</label>
            <div className="relative">
              <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-gray-400">
                <Lock className="w-[18px] h-[18px]" strokeWidth={1.5} />
              </div>
              <input
                type={showPassword ? 'text' : 'password'}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                autoComplete="current-password"
                className="w-full pl-9 pr-11 py-2 rounded-xl border border-gray-300 focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91] outline-none text-[14px] shadow-sm transition-all"
              />
              <button
                type="button"
                onClick={() => setShowPassword((s) => !s)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-gray-600"
                aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
              >
                {showPassword ? <Eye className="w-[18px] h-[18px]" strokeWidth={1.5} /> : <EyeOff className="w-[18px] h-[18px]" strokeWidth={1.5} />}
              </button>
            </div>
            {fieldErrors.password && <p className="mt-1 text-xs text-red-600">{fieldErrors.password}</p>}
          </div>

          <div className="flex justify-end">
            <Link to="/forgot-password" onClick={onSuccess} className="text-[13px] text-[#004c91] hover:underline font-medium">
              Quên mật khẩu?
            </Link>
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="w-full h-[40px] bg-gradient-to-r from-[#004c91] to-[#005baa] hover:from-[#003a6f] hover:to-[#004c91] disabled:opacity-60 disabled:cursor-not-allowed text-white rounded-[4px] font-medium transition-all shadow-sm text-[14px] flex justify-center items-center gap-2"
          >
            {submitting ? 'Đang xử lý...' : 'Đăng nhập'}
          </button>
        </form>
      )}

      {AUTH_CONFIG.enablePasswordLogin && AUTH_CONFIG.enableGoogleSso && (
        <div className="my-5 flex items-center gap-3 text-gray-400 text-xs">
          <div className="h-px flex-1 bg-gray-200" />
          <span>HOẶC</span>
          <div className="h-px flex-1 bg-gray-200" />
        </div>
      )}

      {AUTH_CONFIG.enableGoogleSso && (
        <GoogleSignInButton
          portal="VISITOR"
          onError={setFormError}
          fromPath={fromPath}
          onSuccess={onSuccess}
        />
      )}
    </>
  );
}

export function GoogleSignInButton({
  portal,
  selectedCampusId,
  onError,
  fromPath,
  onSuccess,
  onValidateCampus,
}: {
  portal: LoginPortal;
  selectedCampusId?: string;
  onError: (msg: string) => void;
  fromPath?: string;
  onSuccess?: () => void;
  onValidateCampus?: () => boolean;
}) {
  const { loginWithGoogle } = useAuth();
  const navigate = useNavigate();
  const clientId = AUTH_CONFIG.googleClientId;
  const containerRef = React.useRef<HTMLDivElement>(null);

  // Visitor never requires a campus; only Internal does. Never gate Visitor on campus.
  const campusBlocked = portal === 'INTERNAL' && !selectedCampusId;

  useEffect(() => {
    if (import.meta.env.DEV) {
      // eslint-disable-next-line no-console
      console.log('[Google button]', {
        portal,
        selectedCampusId,
        enableGoogleSso: AUTH_CONFIG.enableGoogleSso,
        hasClientId: Boolean(clientId),
        campusBlocked,
      });
    }
  }, [portal, selectedCampusId, clientId, campusBlocked]);

  useEffect(() => {
    if (!clientId) return;

    const handleCredential = async (response: { credential?: string }) => {
      if (!response.credential) return;
      
      if (onValidateCampus && !onValidateCampus()) {
        return;
      }

      try {
        const user = await loginWithGoogle(response.credential, portal, selectedCampusId);
        if (onSuccess) onSuccess();
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
      google.accounts.id.renderButton(containerRef.current, { theme: 'outline', size: 'large', width: '100%', shape: 'rectangular' });
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
  }, [clientId, portal, selectedCampusId, fromPath, loginWithGoogle, navigate, onError, onValidateCampus, onSuccess]);

  // No client id configured: do NOT silently disable. Keep the button clickable and
  // surface a clear, actionable error so SSO mis-config is obvious in dev.
  if (!clientId) {
    return (
      <button
        type="button"
        onClick={() => {
          if (campusBlocked) {
            onError('Vui lòng chọn cơ sở trước khi đăng nhập bằng Google.');
            if (onValidateCampus) onValidateCampus();
            return;
          }
          onError('Google SSO chưa được cấu hình Client ID. Vui lòng kiểm tra VITE_GOOGLE_CLIENT_ID.');
        }}
        className="w-full flex items-center justify-center gap-3 border border-gray-300 text-gray-700 hover:bg-gray-50 py-2.5 rounded-xl font-medium text-[14px] transition-colors"
      >
        <svg className="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
          <path d="M12.48 10.92v3.28h7.84c-.24 1.84-.853 3.187-1.787 4.133-1.147 1.147-2.933 2.4-6.053 2.4-4.827 0-8.6-3.893-8.6-8.72s3.773-8.72 8.6-8.72c2.6 0 4.507 1.027 5.907 2.347l2.307-2.307C18.747 1.44 16.133 0 12.48 0 5.867 0 .307 5.387.307 12s5.56 12 12.173 12c3.573 0 6.267-1.173 8.373-3.36 2.16-2.16 2.84-5.213 2.84-7.667 0-.76-.053-1.467-.173-2.053H12.48z" />
        </svg>
        Sign in with Google
      </button>
    );
  }

  return (
    <div className="relative w-full">
      <div ref={containerRef} className="w-full flex justify-center [&>div]:w-full" />
      {onValidateCampus && !selectedCampusId && (
        <div 
          className="absolute inset-0 z-10 cursor-pointer"
          onClick={(e) => {
            e.preventDefault();
            e.stopPropagation();
            onValidateCampus();
          }}
          title="Vui lòng chọn cơ sở trước"
        />
      )}
    </div>
  );
}
