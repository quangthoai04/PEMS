import React, { useState, useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import { motion, AnimatePresence } from 'motion/react';
import { Mail, RefreshCw, ShieldCheck, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';

interface Props {
  maskedEmail: string;
  otpError: string | null;
  isVerifying: boolean;
  isResending: boolean;
  onVerify: (code: string) => void;
  onResend: () => void;
  onCancel: () => void;
}

const RESEND_COOLDOWN = 60;

export const OtpVerificationModal: React.FC<Props> = ({
  maskedEmail,
  otpError,
  isVerifying,
  isResending,
  onVerify,
  onResend,
  onCancel,
}) => {
  const { t } = useTranslation(['visitRequest']);
  const [code, setCode] = useState('');
  const [countdown, setCountdown] = useState(RESEND_COOLDOWN);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  useEffect(() => {
    if (countdown <= 0) return;
    const timer = setTimeout(() => setCountdown((c) => c - 1), 1000);
    return () => clearTimeout(timer);
  }, [countdown]);

  const handleResend = () => {
    if (countdown > 0 || isResending) return;
    onResend();
    setCountdown(RESEND_COOLDOWN);
    setCode('');
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (code.length === 6 && !isVerifying) {
      onVerify(code);
    }
  };

  const modal = (
    <div className="fixed inset-0 z-[9999] flex items-center justify-center p-4">
      {/* Backdrop */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={onCancel}
      />

      {/* Dialog */}
      <motion.div
        initial={{ opacity: 0, scale: 0.92, y: 16 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        exit={{ opacity: 0, scale: 0.92, y: 16 }}
        transition={{ type: 'spring', stiffness: 400, damping: 30 }}
        className="relative z-10 bg-white rounded-2xl shadow-2xl w-full max-w-md p-8"
      >
        {/* Close button */}
        <button
          type="button"
          onClick={onCancel}
          className="absolute top-4 right-4 p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-lg transition-colors"
        >
          <X className="w-5 h-5" />
        </button>

        {/* Icon */}
        <div className="flex justify-center mb-5">
          <div className="w-16 h-16 rounded-full bg-[#004c91]/10 flex items-center justify-center">
            <ShieldCheck className="w-8 h-8 text-[#004c91]" />
          </div>
        </div>

        {/* Title */}
        <h2 className="text-2xl font-bold text-gray-900 text-center mb-1">{t('visitRequest:otp.title')}</h2>
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
                  className="mt-2 text-sm text-red-600 text-center font-medium"
                >
                  {otpError}
                </motion.p>
              )}
            </AnimatePresence>
          </div>

          {/* Resend */}
          <div className="flex items-center justify-center gap-1.5 text-sm">
            <Mail className="w-4 h-4 text-gray-400" />
            <span className="text-gray-500">{t('visitRequest:otp.noCode')}</span>
            <button
              type="button"
              onClick={handleResend}
              disabled={countdown > 0 || isResending}
              className="font-bold text-[#f37021] hover:text-[#d9601a] disabled:text-gray-400 disabled:cursor-not-allowed flex items-center gap-1 transition-colors"
            >
              {isResending ? (
                <>
                  <RefreshCw className="w-3.5 h-3.5 animate-spin" />
                  {t('visitRequest:otp.sending')}
                </>
              ) : countdown > 0 ? (
                t('visitRequest:otp.resendTimer', { count: countdown })
              ) : (
                t('visitRequest:otp.resend')
              )}
            </button>
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-2">
            <button
              type="button"
              onClick={onCancel}
              className="flex-1 px-4 py-3 rounded-xl border border-gray-300 text-gray-700 text-sm font-bold hover:bg-gray-50 transition-colors"
            >
              {t('visitRequest:otp.back')}
            </button>
            <button
              type="submit"
              disabled={code.length !== 6 || isVerifying}
              className="flex-1 px-4 py-3 rounded-xl bg-[#004c91] text-white text-sm font-bold hover:bg-[#003d75] disabled:opacity-50 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-2"
            >
              {isVerifying ? (
                <>
                  <RefreshCw className="w-4 h-4 animate-spin" />
                  {t('visitRequest:otp.confirming')}
                </>
              ) : (
                t('visitRequest:otp.confirm')
              )}
            </button>
          </div>
        </form>

        <p className="mt-4 text-xs text-gray-400 text-center">
          {t('visitRequest:otp.validity')}
        </p>
      </motion.div>
    </div>
  );

  return createPortal(
    <AnimatePresence>{modal}</AnimatePresence>,
    document.body
  );
};
