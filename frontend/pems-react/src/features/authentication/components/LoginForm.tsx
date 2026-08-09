import React, { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Eye, EyeOff, Mail, Lock } from 'lucide-react';
import { useAuth } from '../../../shared/hooks/useAuth';
import { getAuthErrorMessage } from '../api/authError';
import { AUTH_CONFIG } from '../../../shared/constants/auth';
import { useTranslation } from 'react-i18next';
import { showSuccessToast, showMessageErrorToast } from '../../../shared/utils/toast';

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function LoginForm({ onSuccess }: { onSuccess?: () => void }) {
  const { t } = useTranslation(['loginModal', 'toast']);
  const { login } = useAuth();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<{ email?: string; password?: string }>({});
  const [submitting, setSubmitting] = useState(false);

  const clearLoginErrors = () => {
    setFieldErrors({});
  };

  // Clear form error when inputs change
  useEffect(() => {
    clearLoginErrors();
  }, [email, password]);

  const validate = () => {
    const errors: { email?: string; password?: string } = {};
    if (!email.trim()) errors.email = t('loginModal:emailRequired');
    else if (!EMAIL_RE.test(email.trim())) errors.email = t('loginModal:emailInvalid');
    if (!password) errors.password = t('loginModal:passwordRequired');

    setFieldErrors(errors);
    return Object.keys(errors).length === 0;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    clearLoginErrors();
    if (!validate()) return;

    setSubmitting(true);
    try {
      const user = await login(email.trim(), password);
      showSuccessToast(t('toast:auth.loginSuccess'), 'auth-status', 2000);
      if (onSuccess) onSuccess();
      if (user.mustChangePassword || user.mustSetPassword) {
        navigate('/change-password', { replace: true });
      } else {
        navigate('/', { replace: true });
      }
    } catch (err) {
      showMessageErrorToast(getAuthErrorMessage(err, t('loginModal:loginError')), 'login-error');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      {AUTH_CONFIG.enablePasswordLogin && (
        <form onSubmit={handleSubmit} noValidate className="space-y-2.5">
          <div>
            <label className="block text-gray-700 font-semibold text-[13px] mb-0.5">{t('loginModal:emailLabel')}</label>
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
            <label className="block text-gray-700 font-semibold text-[13px] mb-0.5">{t('loginModal:passwordLabel')}</label>
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
                aria-label={showPassword ? t('loginModal:hidePassword') : t('loginModal:showPassword')}
              >
                {showPassword ? <Eye className="w-[18px] h-[18px]" strokeWidth={1.5} /> : <EyeOff className="w-[18px] h-[18px]" strokeWidth={1.5} />}
              </button>
            </div>
            {fieldErrors.password && <p className="mt-1 text-xs text-red-600">{fieldErrors.password}</p>}
          </div>

          <div className="flex justify-end">
            <Link to="/forgot-password" onClick={onSuccess} className="text-[13px] text-[#004c91] hover:underline font-medium">
              {t('loginModal:forgotPassword')}
            </Link>
          </div>

          <button
            type="submit"
            disabled={submitting}
            className="w-full h-[40px] bg-gradient-to-r from-[#004c91] to-[#005baa] hover:from-[#003a6f] hover:to-[#004c91] disabled:opacity-60 disabled:cursor-not-allowed text-white rounded-[4px] font-medium transition-all shadow-sm text-[14px] flex justify-center items-center gap-2"
          >
            {submitting ? t('loginModal:processing') : t('loginModal:loginBtn')}
          </button>
        </form>
      )}

      {AUTH_CONFIG.enablePasswordLogin && AUTH_CONFIG.enableGoogleSso && (
        <div className="my-4 flex items-center gap-3 text-gray-400 text-xs">
          <div className="h-px flex-1 bg-gray-200" />
          <span>{t('loginModal:or')}</span>
          <div className="h-px flex-1 bg-gray-200" />
        </div>
      )}

      {AUTH_CONFIG.enableGoogleSso && (
        <GoogleSignInButton
          onError={(msg) => showMessageErrorToast(msg, 'login-error')}
          onSuccess={onSuccess}
        />
      )}
    </>
  );
}

export function GoogleSignInButton({
  onError,
  onSuccess,
}: {
  onError: (msg: string) => void;
  onSuccess?: () => void;
}) {
  const { t, i18n } = useTranslation(['loginModal', 'toast']);
  const { loginWithGoogle } = useAuth();
  const navigate = useNavigate();
  const clientId = AUTH_CONFIG.googleClientId;
  const containerRef = React.useRef<HTMLDivElement>(null);
  const language = i18n.language;

  // LoginForm passes `onError`/`onSuccess` as INLINE callbacks, recreated every render — and email/
  // password keystrokes re-render LoginForm on every character. If this effect depended on them
  // directly, every keystroke would re-run it: `containerRef.current.innerHTML = ''` then
  // `renderButton(...)` again, visibly clearing and rebuilding the Google iframe button mid-typing —
  // this IS the "giật giật" (jitter) while entering credentials. Reading the latest callbacks from a
  // ref (same pattern as AgendaSetupPanel's notifyRef) lets the effect depend only on what actually
  // changes what gets rendered — clientId and language — so typing never touches the button again.
  const latestRef = React.useRef({ onError, onSuccess, navigate, loginWithGoogle, t });
  latestRef.current = { onError, onSuccess, navigate, loginWithGoogle, t };

  useEffect(() => {
    if (!clientId) return;

    const handleCredential = async (response: { credential?: string }) => {
      if (!response.credential) return;
      const { onError, onSuccess, navigate, loginWithGoogle, t } = latestRef.current;

      try {
        const user = await loginWithGoogle(response.credential);
        showSuccessToast(t('toast:auth.loginSuccess'), 'auth-status', 2000);
        if (onSuccess) onSuccess();
        if (user.mustChangePassword || user.mustSetPassword) navigate('/change-password', { replace: true });
        else navigate('/', { replace: true });
      } catch (err) {
        onError(getAuthErrorMessage(err, t('loginModal:googleSignInFailed')));
      }
    };

    const init = () => {
      const google = (window as any).google;
      if (!google?.accounts?.id || !containerRef.current) return;
      google.accounts.id.initialize({ client_id: clientId, callback: handleCredential });
      // Google's GSI script renders its own button label and localizes it from the
      // browser / Google-account locale unless `locale` is passed. Without this the
      // button reads "Đăng nhập bằng Google" while the rest of the app is in English.
      // Clear first: renderButton appends, so a language switch would stack buttons.
      containerRef.current.innerHTML = '';
      google.accounts.id.renderButton(containerRef.current, {
        theme: 'outline',
        size: 'large',
        width: '100%',
        shape: 'rectangular',
        locale: language === 'en' ? 'en_US' : 'vi_VN',
      });
    };

    const targetHl = language === 'en' ? 'en' : 'vi';
    const scriptUrl = `https://accounts.google.com/gsi/client?hl=${targetHl}`;

    // If the GSI script is already loaded but for a DIFFERENT language, it ignores the
    // renderButton locale param. We must remove it and its global object to force a reload.
    const existing = document.getElementById('google-gsi-script') as HTMLScriptElement;
    if (existing && !existing.src.includes(`hl=${targetHl}`)) {
      existing.remove();
      delete (window as any).google;
    }

    if ((window as any).google?.accounts?.id) {
      init();
      return;
    }

    const currentScript = document.getElementById('google-gsi-script') as HTMLScriptElement;
    if (currentScript) {
      currentScript.addEventListener('load', init);
      return () => currentScript.removeEventListener('load', init);
    }

    const script = document.createElement('script');
    script.src = scriptUrl;
    script.async = true;
    script.defer = true;
    script.id = 'google-gsi-script';
    script.onload = init;
    document.body.appendChild(script);
    // onError/onSuccess/navigate/loginWithGoogle/t are intentionally excluded: handleCredential reads
    // their latest versions from latestRef, so only clientId/language (which change what the button
    // actually renders) should re-run initialize/renderButton.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [clientId, language]);

  // No client id configured: do NOT silently disable. Keep the button clickable and
  // surface a clear, actionable error so SSO mis-config is obvious in dev.
  if (!clientId) {
    return (
      <button
        type="button"
        onClick={() => onError(t('loginModal:googleMissingClientId'))}
        className="w-full flex items-center justify-center gap-3 border border-gray-300 text-gray-700 hover:bg-gray-50 py-2.5 rounded-xl font-medium text-[14px] transition-colors"
      >
        <svg className="w-5 h-5" viewBox="0 0 24 24" fill="currentColor">
          <path d="M12.48 10.92v3.28h7.84c-.24 1.84-.853 3.187-1.787 4.133-1.147 1.147-2.933 2.4-6.053 2.4-4.827 0-8.6-3.893-8.6-8.72s3.773-8.72 8.6-8.72c2.6 0 4.507 1.027 5.907 2.347l2.307-2.307C18.747 1.44 16.133 0 12.48 0 5.867 0 .307 5.387.307 12s5.56 12 12.173 12c3.573 0 6.267-1.173 8.373-3.36 2.16-2.16 2.84-5.213 2.84-7.667 0-.76-.053-1.467-.173-2.053H12.48z" />
        </svg>
        {t('loginModal:signInWithGoogle')}
      </button>
    );
  }

  return (
    <div className="relative w-full" key={language}>
      {/* min-h reserves the "large" GSI button's own height (40px) so the modal does not visibly
          grow/recentre the instant the async google script finishes loading and renderButton fires. */}
      <div ref={containerRef} className="w-full min-h-[40px] flex justify-center [&>div]:w-full" />
    </div>
  );
}
