import React, { useEffect, useState } from 'react';
import { X, Loader2, AlertCircle, ChevronLeft, ChevronRight, Check, CheckCircle2 } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useVisitRequestForm } from '../../features/visit-request/hooks/useVisitRequestForm';
import { RegisterInfoSection } from '../../features/visit-request/components/sections/RegisterInfoSection';
import { VisitInfoSection } from '../../features/visit-request/components/sections/VisitInfoSection';
import { VisitorListSection } from '../../features/visit-request/components/sections/VisitorListSection';
import { ContactSection } from '../../features/visit-request/components/sections/ContactSection';
import { AdditionalSection } from '../../features/visit-request/components/sections/AdditionalSection';
import { OtpVerificationModal } from '../../features/visit-request/components/OtpVerificationModal';
import type { VerifyResponse } from '../../features/visit-request/api/visitRequestApi';

const STEPS = [
  { num: 1, label: 'Thông tin đăng ký' },
  { num: 2, label: 'Thành phần tham dự' },
  { num: 3, label: 'Yêu cầu bổ sung' },
];

interface VisitingFormPopupProps {
  isOpen: boolean;
  onClose: () => void;
}

export function VisitingFormPopup({ isOpen, onClose }: VisitingFormPopupProps) {
  const [currentStep, setCurrentStep] = useState(1);
  const [successResult, setSuccessResult] = useState<VerifyResponse | null>(null);
  const [stepError, setStepError] = useState<string | null>(null);

  const handleSuccess = (result: VerifyResponse) => {
    setSuccessResult(result);
    // Auto-close after 4 seconds
    setTimeout(() => {
      setSuccessResult(null);
      onClose();
    }, 4000);
  };

  const {
    form,
    visitFields,
    visitorFields,
    supportTeamFields,
    syncSupportFromRegister,
    clearSupportFirstRow,
    syncContactFromRegister,
    clearContactPoint,
    onSubmit,
    isSubmitting,
    submitError,
    sessionToken,
    maskedEmail,
    otpError,
    isVerifying,
    isResending,
    verifyOtp,
    resendOtp,
    cancelOtp,
  } = useVisitRequestForm(handleSuccess);

  useEffect(() => {
    if (!isOpen) return;
    // Use position:fixed (not overflow:hidden) so dropdown portals to body still show correctly
    const scrollY = window.scrollY;
    document.body.style.position = 'fixed';
    document.body.style.top = `-${scrollY}px`;
    document.body.style.width = '100%';
    return () => {
      document.body.style.position = '';
      document.body.style.top = '';
      document.body.style.width = '';
      window.scrollTo(0, scrollY);
    };
  }, [isOpen]);

  useEffect(() => {
    if (!isOpen) {
      setCurrentStep(1);
      setSuccessResult(null);
    }
  }, [isOpen]);

  const handleNextStep = async () => {
    setStepError(null);
    const stepFields: Record<number, string[]> = {
      1: ['registerInfo', 'delegationName', 'visitMode', 'visits', 'purpose', 'workingContent'],
      2: ['visitors', 'supportTeam', 'contactPoint'],
    };
    const fields = stepFields[currentStep];
    if (!fields) { setCurrentStep((s) => s + 1); return; }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const valid = await form.trigger(fields as any);
    if (valid) {
      setCurrentStep((s) => s + 1);
    } else {
      setStepError('Vui lòng điền đầy đủ và đúng các trường bắt buộc trước khi tiếp tục.');
    }
  };

  return (
    <>
      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[100] bg-black/60 backdrop-blur-sm flex items-center justify-center p-3 sm:p-6"
            onClick={onClose}
          >
            <motion.div
              initial={{ opacity: 0, scale: 0.95, y: 20 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.95, y: 20 }}
              transition={{ duration: 0.3, ease: 'easeOut' }}
              onClick={(e) => e.stopPropagation()}
              className="bg-white w-full max-w-7xl max-h-[92vh] rounded-2xl shadow-2xl flex flex-col overflow-hidden relative border border-gray-100"
            >
              {/* ── Success overlay ── */}
              <AnimatePresence>
                {successResult && (
                  <motion.div
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    exit={{ opacity: 0 }}
                    className="absolute inset-0 z-50 flex items-center justify-center bg-white/95 rounded-2xl"
                  >
                    <div className="text-center px-8">
                      <motion.div
                        initial={{ scale: 0 }}
                        animate={{ scale: 1 }}
                        transition={{ type: 'spring', stiffness: 400, damping: 20, delay: 0.1 }}
                        className="w-20 h-20 rounded-full bg-green-100 flex items-center justify-center mx-auto mb-5"
                      >
                        <CheckCircle2 className="w-10 h-10 text-green-500" />
                      </motion.div>
                      <h3 className="text-2xl font-black text-gray-900 mb-2">Đăng ký thành công!</h3>
                      <p className="text-gray-500 text-sm mb-4">
                        Đơn của bạn đang chờ phê duyệt. Vui lòng kiểm tra email để theo dõi.
                      </p>
                      <div className="inline-flex items-center gap-2 px-4 py-2.5 bg-[#004c91]/10 rounded-xl">
                        <span className="text-xs font-bold text-gray-500 uppercase tracking-wide">Mã đơn:</span>
                        <span className="text-lg font-black text-[#004c91] tracking-wider">
                          {successResult.requestCode}
                        </span>
                      </div>
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>

              {/* ── Header ── */}
              <div className="flex-none px-6 py-5 sm:px-10 flex flex-col sm:flex-row items-start sm:items-center justify-between text-white relative z-10 overflow-hidden bg-gradient-to-br from-[#004c91] to-[#013565]">
                <div className="absolute top-0 right-0 w-64 h-64 bg-white/5 rounded-full -translate-y-1/2 translate-x-1/3 blur-2xl" />
                <div className="absolute bottom-0 left-0 w-40 h-40 bg-[#f37021]/20 rounded-full translate-y-1/2 -translate-x-1/4 blur-xl" />
                <div className="relative z-10 pr-8">
                  <div className="inline-flex items-center gap-2 px-2.5 py-1 bg-white/10 text-orange-200 rounded-full text-[10px] font-bold uppercase tracking-wider mb-2">
                    <span className="w-1.5 h-1.5 bg-[#f37021] rounded-full animate-pulse" />
                    Campus Visit
                  </div>
                  <h2 className="text-xl sm:text-2xl font-black tracking-tight mb-1">ĐĂNG KÝ THAM QUAN TRƯỜNG</h2>
                  <p className="text-blue-100/90 font-medium text-xs sm:text-sm max-w-2xl">
                    Vui lòng điền đầy đủ thông tin dưới đây để đăng ký lịch trình tham quan.
                  </p>
                </div>
                <button
                  onClick={onClose}
                  className="absolute top-4 right-4 sm:top-5 sm:right-6 p-2 text-white/70 hover:text-white hover:bg-white/20 rounded-full transition-all z-20"
                >
                  <X className="w-5 h-5 sm:w-6 sm:h-6" />
                </button>
              </div>

              {/* ── Step indicator ── */}
              <div className="flex-none px-6 sm:px-10 py-3 bg-gray-50/80 border-b border-gray-100">
                <div className="flex items-center">
                  {STEPS.map((step, i) => {
                    const isActive = step.num === currentStep;
                    const isDone = step.num < currentStep;
                    return (
                      <React.Fragment key={step.num}>
                        <button
                          type="button"
                          onClick={() => setCurrentStep(step.num)}
                          className="flex items-center gap-2 group shrink-0"
                        >
                          <span
                            className={[
                              'w-7 h-7 rounded-full flex items-center justify-center text-xs font-black transition-all',
                              isActive
                                ? 'bg-[#004c91] text-white shadow-md shadow-blue-900/30'
                                : isDone
                                  ? 'bg-green-500 text-white'
                                  : 'bg-gray-200 text-gray-400 group-hover:bg-gray-300',
                            ].join(' ')}
                          >
                            {isDone ? <Check className="w-3.5 h-3.5" /> : step.num}
                          </span>
                          <span
                            className={[
                              'text-xs sm:text-sm font-semibold transition-colors hidden sm:block',
                              isActive
                                ? 'text-[#004c91]'
                                : isDone
                                  ? 'text-green-600'
                                  : 'text-gray-400 group-hover:text-gray-600',
                            ].join(' ')}
                          >
                            {step.label}
                          </span>
                        </button>
                        {i < STEPS.length - 1 && (
                          <div
                            className={[
                              'flex-1 h-0.5 mx-3 rounded-full transition-colors',
                              isDone ? 'bg-green-400' : 'bg-gray-200',
                            ].join(' ')}
                          />
                        )}
                      </React.Fragment>
                    );
                  })}
                </div>
              </div>

              {/* ── Body ── */}
              <div className="flex-1 overflow-y-auto px-4 sm:px-10 py-8 bg-white custom-scrollbar">
                <form onSubmit={onSubmit} noValidate>
                  <AnimatePresence mode="wait">
                    {currentStep === 1 && (
                      <motion.div
                        key="step1"
                        initial={{ opacity: 0, x: 20 }}
                        animate={{ opacity: 1, x: 0 }}
                        exit={{ opacity: 0, x: -20 }}
                        transition={{ duration: 0.2 }}
                        className="space-y-12"
                      >
                        <RegisterInfoSection form={form} />
                        <VisitInfoSection form={form} visitFields={visitFields} />
                      </motion.div>
                    )}

                    {currentStep === 2 && (
                      <motion.div
                        key="step2"
                        initial={{ opacity: 0, x: 20 }}
                        animate={{ opacity: 1, x: 0 }}
                        exit={{ opacity: 0, x: -20 }}
                        transition={{ duration: 0.2 }}
                      >
                        <VisitorListSection
                          form={form}
                          visitorFields={visitorFields}
                        />
                        <ContactSection
                          form={form}
                          supportTeamFields={supportTeamFields}
                          onSyncSupportFromRegister={syncSupportFromRegister}
                          onClearSupportFirstRow={clearSupportFirstRow}
                          onSyncContactFromRegister={syncContactFromRegister}
                          onClearContactPoint={clearContactPoint}
                        />
                      </motion.div>
                    )}

                    {currentStep === 3 && (
                      <motion.div
                        key="step3"
                        initial={{ opacity: 0, x: 20 }}
                        animate={{ opacity: 1, x: 0 }}
                        exit={{ opacity: 0, x: -20 }}
                        transition={{ duration: 0.2 }}
                      >
                        <AdditionalSection form={form} />
                      </motion.div>
                    )}
                  </AnimatePresence>
                </form>
              </div>

              {/* ── Footer ── */}
              <div className="flex-none py-3 px-5 sm:py-4 sm:px-6 bg-white border-t border-gray-100 flex items-center justify-between gap-3 rounded-b-2xl shadow-[0_-4px_20px_rgba(0,0,0,0.02)] z-20">
                {/* Left side */}
                <div>
                  {currentStep === 1 ? (
                    <button
                      type="button"
                      onClick={onClose}
                      disabled={isSubmitting}
                      className="px-6 py-3 rounded-xl font-bold text-gray-600 bg-white border-2 border-gray-200 hover:bg-gray-50 hover:text-gray-900 transition-colors disabled:opacity-50"
                    >
                      Hủy
                    </button>
                  ) : (
                    <button
                      type="button"
                      onClick={() => { setStepError(null); setCurrentStep((s) => s - 1); }}
                      disabled={isSubmitting}
                      className="inline-flex items-center gap-2 px-6 py-3 rounded-xl font-bold text-gray-600 bg-white border-2 border-gray-200 hover:bg-gray-50 hover:text-gray-900 transition-colors disabled:opacity-50"
                    >
                      <ChevronLeft className="w-4 h-4" />
                      Quay lại
                    </button>
                  )}
                </div>

                {/* Right side */}
                <div className="flex items-center gap-3">
                  {stepError && currentStep < 3 && (
                    <div className="flex items-center gap-2 text-amber-700 text-xs font-medium bg-amber-50 px-3 py-2 rounded-lg border border-amber-200">
                      <AlertCircle className="w-4 h-4 shrink-0" />
                      {stepError}
                    </div>
                  )}
                  {submitError && currentStep === 3 && (
                    <div className="flex items-center gap-2 text-red-600 text-xs font-medium bg-red-50 px-3 py-2 rounded-lg border border-red-200">
                      <AlertCircle className="w-4 h-4 shrink-0" />
                      {submitError}
                    </div>
                  )}

                  {/* Step counter */}
                  <span className="text-xs text-gray-400 font-medium hidden sm:block">
                    {currentStep} / {STEPS.length}
                  </span>

                  {currentStep < 3 ? (
                    <button
                      type="button"
                      onClick={handleNextStep}
                      className="inline-flex items-center gap-2 px-8 py-3 rounded-xl font-black tracking-wide text-white bg-gradient-to-r from-[#004c91] to-[#013565] hover:from-[#013565] hover:to-[#012a52] shadow-lg shadow-blue-900/30 transition-all transform hover:-translate-y-0.5"
                    >
                      Tiếp theo
                      <ChevronRight className="w-4 h-4" />
                    </button>
                  ) : (
                    <button
                      type="submit"
                      disabled={isSubmitting}
                      onClick={onSubmit}
                      className="inline-flex items-center justify-center gap-2 px-8 py-3 rounded-xl font-black tracking-wide text-white bg-gradient-to-r from-[#f37021] to-[#e06111] hover:from-[#e06111] hover:to-[#c4530c] shadow-lg shadow-orange-500/30 transition-all transform hover:-translate-y-0.5 disabled:opacity-60 disabled:transform-none"
                    >
                      {isSubmitting ? (
                        <>
                          <Loader2 className="w-4 h-4 animate-spin" />
                          Đang gửi...
                        </>
                      ) : (
                        'Gửi đơn'
                      )}
                    </button>
                  )}
                </div>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* OTP Modal — rendered via portal above everything */}
      {sessionToken && (
        <OtpVerificationModal
          maskedEmail={maskedEmail}
          otpError={otpError}
          isVerifying={isVerifying}
          isResending={isResending}
          onVerify={verifyOtp}
          onResend={resendOtp}
          onCancel={cancelOtp}
        />
      )}
    </>
  );
}
