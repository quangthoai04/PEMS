import React, { useState, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'motion/react';
import { Mail, RefreshCw, ShieldAlert, ShieldCheck, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { TurnstileWidget } from './TurnstileWidget';
import { parseApiDate } from '../../../shared/utils/vietnamTime';

interface Props {
  maskedEmail: string;
  otpError: string | null;
  isVerifying: boolean;
  isResending: boolean;
  /** Server-provided remaining wrong attempts (presentation only). */
  remainingAttempts: number | null;
  /** Server-enforced cooldown before the next attempt; the countdown here is presentation. */
  retryAfterSeconds: number | null;
  /** Absolute time when the quota resets and user can retry. Drives the Cooldown screen. */
  retryAt: string | null;
  /** Server-provided resend cooldown seed (seconds). */
  resendAfterSeconds: number;
  /** true → the challenge is burned; only human verification can issue a new code. */
  humanVerificationRequired: boolean;
  isRecovering: boolean;
  onVerify: (code: string) => void;
  onResend: () => void;
  onRecover: (humanVerificationToken: string) => void;
  onCancel: () => void;
  /**
   * "Let me check the form first" — steps back to the form WITHOUT spending the challenge
   * (plan §12). Omitted where there is no form behind the modal to go back to.
   */
  onReviewForm?: () => void;
}

/**
 * UC17 OTP modal — three server-driven states:
 *   OTP_ENTRY           → code input, remaining attempts, retry cooldown
 *   HUMAN_VERIFICATION  → Turnstile instead of the (dead) code input; resend hidden
 *   RECOVERING          → human verification in flight (double-call guarded)
 */
export const OtpVerificationModal: React.FC<Props> = ({
  maskedEmail,
  otpError,
  isVerifying,
  isResending,
  remainingAttempts,
  retryAfterSeconds,
  retryAt,
  resendAfterSeconds,
  humanVerificationRequired,
  isRecovering,
  onVerify,
  onResend,
  onRecover,
  onCancel,
  onReviewForm,
}) => {
  const { t } = useTranslation(['visitRequest', 'visitRequestV2']);
  const [code, setCode] = useState('');
  const [resendCountdown, setResendCountdown] = useState(resendAfterSeconds);
  const [retryCountdown, setRetryCountdown] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const wasHumanVerificationRef = useRef(humanVerificationRequired);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    if (resendCountdown <= 0) return;
    const timer = setTimeout(() => setResendCountdown((c) => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [resendCountdown]);

  // Seed the local presentation countdown from the server's retryAfterSeconds.
  useEffect(() => {
    setRetryCountdown(retryAfterSeconds ?? 0);
  }, [retryAfterSeconds]);

  useEffect(() => {
    if (retryCountdown <= 0) return;
    const timer = setTimeout(() => setRetryCountdown((c) => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [retryCountdown]);

  // Global quota cooldown based on retryAt.
  const [rateLimitCountdown, setRateLimitCountdown] = useState(0);

  useEffect(() => {
    if (!retryAt) {
      setRateLimitCountdown(0);
      return;
    }
    const updateCountdown = () => {
      const now = Date.now();
      // API sends "+07:00"-offset ISO (Vietnam wall-clock) — parse offset-aware, never append 'Z'.
      const target = parseApiDate(retryAt)?.getTime() ?? 0;
      const diff = Math.ceil((target - now) / 1000);
      setRateLimitCountdown(diff > 0 ? diff : 0);
    };
    updateCountdown();
    const timer = setInterval(updateCountdown, 1000);
    return () => clearInterval(timer);
  }, [retryAt]);

  // Recovery succeeded (human verification cleared) → new challenge: reset the input.
  useEffect(() => {
    if (wasHumanVerificationRef.current && !humanVerificationRequired) {
      setCode('');
      setResendCountdown(resendAfterSeconds);
      inputRef.current?.focus();
    }
    wasHumanVerificationRef.current = humanVerificationRequired;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [humanVerificationRequired]);

  const handleResend = () => {
    if (resendCountdown > 0 || isResending || isVerifying) return;
    onResend();
    setResendCountdown(resendAfterSeconds);
    setCode('');
  };

  /**
   * While the verify is in flight the create may be committing THIS INSTANT. Leaving now — via the
   * X, the backdrop or Back — would drop the session token and leave the user with no way to learn
   * whether their request exists (plan §5).
   */
  const dismiss = () => { if (!isVerifying) onCancel(); };

  const confirmDisabled = code.length !== 6 || isVerifying || retryCountdown > 0;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    // stopPropagation, not just preventDefault. This modal is rendered through a PORTAL, and a React
    // portal keeps the COMPONENT tree even though it leaves the DOM tree — so the synthetic submit
    // event still bubbles to the <form> that renders the modal, which is the visit-request form
    // itself. Without this, every "Xác nhận" click also re-submitted the whole registration and
    // minted a SECOND OTP challenge: the code the user was holding stopped working, and the initiate
    // that came back cleared the "wrong code" message they were reading.
    e.stopPropagation();
    if (!confirmDisabled) onVerify(code);
  };

  const formatDuration = (seconds: number) => {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`;
  };

  const modal = (
    <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
      {/* Backdrop */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={dismiss}
      />

      {/* Dialog */}
      <motion.div
        initial={{ opacity: 0, scale: 0.92, y: 16 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.92, y: 16 }}
        transition={{ type: 'spring', stiffness: 400, damping: 30 }}
        role="dialog"
        aria-modal="true"
        aria-labelledby="otp-dialog-title"
        className="relative z-10 max-h-[92dvh] w-full max-w-md overflow-y-auto rounded-2xl bg-white p-6 shadow-2xl sm:p-8"
      >
        {/* Close button */}
        <button
          type="button"
          onClick={dismiss}
          disabled={isVerifying}
          aria-label={t('visitRequest:popup.cancel')}
          className="absolute top-4 right-4 p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors disabled:cursor-not-allowed disabled:opacity-40"
        >
          <X className="w-5 h-5" />
        </button>

        {rateLimitCountdown > 0 ? (
          /* ── RATE_LIMITED / COOLDOWN ─────────────────────────────── */
          <div>
            <div className="flex justify-center mb-5">
              <div className="w-16 h-16 rounded-full bg-red-100 flex items-center justify-center">
                <ShieldAlert className="w-8 h-8 text-red-600" />
              </div>
            </div>

            <h2 id="otp-dialog-title" className="text-2xl font-bold text-gray-900 text-center mb-2">
              {t('visitRequest:otp.rateLimited.title', 'Chưa thể gửi mã xác thực')}
            </h2>
            <p className="text-sm text-gray-500 text-center mb-6">
              {t('visitRequest:otp.rateLimited.desc', 'Bạn đã yêu cầu mã quá nhiều lần. Vui lòng quay lại sau khoảng thời gian đếm ngược dưới đây.')}
            </p>

            <div className="mb-6 flex justify-center">
               <div className="text-4xl font-mono font-bold text-red-600 tracking-wider">
                  {formatDuration(rateLimitCountdown)}
               </div>
            </div>

            <button
              type="button"
              onClick={dismiss}
              className="w-full px-4 py-3 rounded-xl border border-gray-300 text-gray-700 text-sm font-bold hover:bg-gray-50 transition-colors"
            >
              {t('visitRequest:otp.back')}
            </button>
          </div>
        ) : humanVerificationRequired ? (
          /* ── HUMAN_VERIFICATION / RECOVERING ─────────────────────────────── */
          <div>
            <div className="flex justify-center mb-5">
              <div className="w-16 h-16 rounded-full bg-amber-100 flex items-center justify-center">
                <ShieldAlert className="w-8 h-8 text-amber-600" />
              </div>
            </div>

            <h2 id="otp-dialog-title" className="text-2xl font-bold text-gray-900 text-center mb-2">
              {t('visitRequest:otp.human.title')}
            </h2>
            <p className="text-sm text-gray-500 text-center mb-6">
              {t('visitRequest:otp.human.desc')}
            </p>

            <div className="mb-4">
              {isRecovering ? (
                <div
                  data-testid="otp-recovering"
                  className="flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-slate-50 px-4 py-4 text-sm font-bold text-slate-600"
                >
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  {t('visitRequest:otp.human.recovering')}
                </div>
              ) : (
                <TurnstileWidget onToken={onRecover} disabled={isRecovering} />
              )}
            </div>

            <AnimatePresence mode="wait">
              {otpError && (
                <motion.p
                  key="human-err"
                  initial={{ opacity: 0, y: -4 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0 }}
                  role="alert"
                  className="mb-4 text-sm text-red-600 text-center font-medium"
                >
                  {otpError}
                </motion.p>
              )}
            </AnimatePresence>

            <button
              type="button"
              onClick={dismiss}
              className="w-full px-4 py-3 rounded-xl border border-gray-300 text-gray-700 text-sm font-bold hover:bg-gray-50 transition-colors"
            >
              {t('visitRequest:otp.back')}
            </button>
          </div>
        ) : (
          /* ── OTP_ENTRY ────────────────────────────────────────────────────── */
          <div>
            {/* Icon */}
            <div className="flex justify-center mb-5">
              <div className="w-16 h-16 rounded-full bg-[#004c91]/10 flex items-center justify-center">
                <ShieldCheck className="w-8 h-8 text-[#004c91]" />
              </div>
            </div>

            {/* Title */}
            <h2 id="otp-dialog-title" className="text-2xl font-bold text-gray-900 text-center mb-1">
              {t('visitRequest:otp.title')}
            </h2>
            <p className="text-sm text-gray-500 text-center mb-6">
              {t('visitRequest:otp.sentTo')}
              <span className="font-semibold text-[#004c91] ml-1">{maskedEmail}</span>
            </p>

            <form onSubmit={handleSubmit} className="space-y-5">
              {/* OTP input */}
              <div>
                <label className="block text-sm font-bold text-gray-700 mb-2 text-center">
                  {t('visitRequest:otp.inputLabel')}
                </label>
                <input
                  ref={inputRef}
                  type="text"
                  inputMode="numeric"
                  pattern="[0-9]*"
                  maxLength={6}
                  value={code}
                  onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                  placeholder="______"
                  className={[
                    'w-full text-center text-3xl font-bold tracking-[0.5em] px-4 py-4 rounded-xl border-2 outline-none transition-all',
                    'placeholder:text-gray-200 placeholder:tracking-[0.5em]',
                    otpError
                      ? 'border-red-400 bg-red-50 focus:border-red-500 focus:ring-2 focus:ring-red-200'
                      : 'border-gray-300 bg-white focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/20',
                  ].join(' ')}
                />

                <AnimatePresence mode="wait">
                  {otpError && (
                    <motion.p
                      key="err"
                      initial={{ opacity: 0, y: -4 }}
                      animate={{ opacity: 1, y: 0 }}
                      exit={{ opacity: 0 }}
                      role="alert"
                      className="mt-2 text-sm text-red-600 text-center font-medium"
                    >
                      {otpError}
                    </motion.p>
                  )}
                </AnimatePresence>

                {/* Remaining attempts — server value, shown after a wrong attempt. */}
                {otpError && remainingAttempts !== null && remainingAttempts > 0 && (
                  <p data-testid="otp-remaining-attempts" className="mt-2 text-xs font-semibold text-amber-600 text-center">
                    {t('visitRequest:otp.remainingAttempts', { count: remainingAttempts })}
                  </p>
                )}

                {/* Server cooldown — presentation countdown, server re-enforces anyway. */}
                {retryCountdown > 0 && (
                  <p data-testid="otp-retry-countdown" className="mt-2 text-xs font-semibold text-slate-500 text-center">
                    {t('visitRequest:otp.retryIn', { count: retryCountdown })}
                  </p>
                )}
              </div>

              {/* Resend */}
              <div className="flex items-center justify-center gap-1.5 text-sm">
                <Mail className="w-4 h-4 text-gray-400" />
                <span className="text-gray-500">{t('visitRequest:otp.noCode')}</span>
                <button
                  type="button"
                  onClick={handleResend}
                  disabled={resendCountdown > 0 || isResending}
                  className="font-bold text-[#f37021] hover:text-[#d9601a] disabled:text-gray-400 disabled:cursor-not-allowed flex items-center gap-1 transition-colors"
                >
                  {isResending ? (
                    <>
                      <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                      {t('visitRequest:otp.sending')}
                    </>
                  ) : resendCountdown > 0 ? (
                    t('visitRequest:otp.resendTimer', { count: resendCountdown })
                  ) : (
                    t('visitRequest:otp.resend')
                  )}
                </button>
              </div>

              {/* Actions */}
              <div className="flex gap-3 pt-2">
                <button
                  type="button"
                  onClick={dismiss}
                  disabled={isVerifying}
                  className="flex-1 px-4 py-3 rounded-xl border border-gray-300 text-gray-700 text-sm font-bold hover:bg-gray-50 transition-colors disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {t('visitRequest:otp.back')}
                </button>
                <button
                  type="submit"
                  data-testid="otp-confirm"
                  disabled={confirmDisabled}
                  className="flex-1 px-4 py-3 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003d75] disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
                >
                  {isVerifying ? (
                    <>
                      <RefreshCw className="w-4 h-4 animate-spin" />
                      {t('visitRequestV2:otpFlow.verifyingAndCreating')}
                    </>
                  ) : (
                    t('visitRequest:otp.confirm')
                  )}
                </button>
              </div>

              {/* Checking a date should not cost a new code: this keeps the challenge and simply
                  steps back to the form (plan §12). */}
              {onReviewForm && (
                <button
                  type="button"
                  data-testid="otp-review-form"
                  onClick={onReviewForm}
                  disabled={isVerifying}
                  className="w-full text-center text-sm font-bold text-[#004c91] hover:underline disabled:cursor-not-allowed disabled:text-gray-400 disabled:no-underline"
                >
                  {t('visitRequestV2:otpFlow.reviewForm')}
                </button>
              )}
            </form>

            <p className="mt-4 text-xs text-gray-400 text-center">
              {t('visitRequest:otp.validity')}
            </p>
          </div>
        )}
      </motion.div>
    </div>
  );

  return createPortal(
    <AnimatePresence>{modal}</AnimatePresence>,
    document.body
  );
};
