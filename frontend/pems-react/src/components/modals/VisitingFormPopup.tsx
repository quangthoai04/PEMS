import React, { useEffect, useState } from 'react';
import { X, Loader2, AlertCircle, ChevronLeft, ChevronRight, Check, CheckCircle2 } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useVisitRequestForm, DEFAULT_VISIT_REQUEST_VALUES } from '../../features/visit-request/hooks/useVisitRequestForm';
import { RegisterInfoSection } from '../../features/visit-request/components/sections/RegisterInfoSection';
import { VisitInfoSection } from '../../features/visit-request/components/sections/VisitInfoSection';
import { VisitorListSection } from '../../features/visit-request/components/sections/VisitorListSection';
import { ContactSection } from '../../features/visit-request/components/sections/ContactSection';
import { AdditionalSection } from '../../features/visit-request/components/sections/AdditionalSection';
import { OtpVerificationModal } from '../../features/visit-request/components/OtpVerificationModal';
import { findCampusTimeOverlaps } from '../../features/visit-request/schema/visitRequest.schema';
import type { VisitRequestSchema } from '../../features/visit-request/schema/visitRequest.schema';
import type { VerifyResponse } from '../../features/visit-request/api/visitRequestApi';
import { loadVisitRequestDraft, clearVisitRequestDraft, saveVisitRequestDraft, hasAnyUserInput } from '../../features/visit-request/utils/visitRequestDraftStorage';
import { useTranslation } from 'react-i18next';

const STEPS = [
  { num: 1, labelKey: 'step1' },
  { num: 2, labelKey: 'step2' },
  { num: 3, labelKey: 'step3' },
];

interface VisitingFormPopupProps {
  isOpen: boolean;
  onClose: () => void;
}

export function VisitingFormPopup({ isOpen, onClose }: VisitingFormPopupProps) {
  const { t } = useTranslation(['visitRequest']);
  const [currentStep, setCurrentStep] = useState(1);
  const [successResult, setSuccessResult] = useState<VerifyResponse | null>(null);
  const [stepError, setStepError] = useState<string | null>(null);
  const [showOverlapConfirm, setShowOverlapConfirm] = useState(false);
  const [stepAttempted, setStepAttempted] = useState<Record<number, boolean>>({});

  const [pendingDraft, setPendingDraft] = useState<ReturnType<typeof loadVisitRequestDraft> | null>(null);
  const [showRestoreDraftModal, setShowRestoreDraftModal] = useState(false);
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const [showSaveDraftConfirm, setShowSaveDraftConfirm] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  useEffect(() => {
    if (toastMessage) {
      const timer = setTimeout(() => setToastMessage(null), 3000);
      return () => clearTimeout(timer);
    }
  }, [toastMessage]);

  const handleSuccess = (result: VerifyResponse) => {
    clearVisitRequestDraft();
    setSuccessResult(result);
    // Auto-close after 4 seconds
    setTimeout(() => {
      setSuccessResult(null);
      onClose();
    }, 4000);
  };

  const handleInvalidSubmit = (errors: any) => {
    console.warn("Submit blocked by validation:", errors);
    
    const step1Fields = ['registerInfo', 'delegationName', 'visitMode', 'visits', 'purpose', 'workingContent'];
    const step2Fields = ['visitors', 'supportTeam', 'contactPoint'];
    
    const hasStep1Error = Object.keys(errors).some(k => step1Fields.includes(k));
    const hasStep2Error = Object.keys(errors).some(k => step2Fields.includes(k));
    
    if (hasStep1Error) {
      setCurrentStep(1);
      setStepError(t('visitRequest:popup.checkInfoError'));
    } else if (hasStep2Error) {
      setCurrentStep(2);
      setStepError(t('visitRequest:popup.checkInfoError'));
    } else {
      setCurrentStep(3);
      setSubmitError(t('visitRequest:popup.checkInfoError'));
    }
    
    setTimeout(() => {
      const firstError = document.querySelector('.error-scroll-target');
      if (firstError) {
        firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }, 100);
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
    setSubmitError,
    sessionToken,
    maskedEmail,
    otpError,
    isVerifying,
    isResending,
    verifyOtp,
    resendOtp,
    cancelOtp,
    setDraftHydrated,
    isRestoringDraftRef,
  } = useVisitRequestForm(handleSuccess, handleInvalidSubmit);

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
    if (!isOpen) return;

    const draft = loadVisitRequestDraft();
    if (draft?.data) {
      setPendingDraft(draft);
      setShowRestoreDraftModal(true);
      setDraftHydrated(false);
    } else {
      setDraftHydrated(true);
    }
  }, [isOpen, setDraftHydrated]);

  const handleRestoreDraft = () => {
    if (pendingDraft?.data) {
      const restoredValues: VisitRequestSchema = {
        ...DEFAULT_VISIT_REQUEST_VALUES,
        ...pendingDraft.data,
        registerInfo: {
          ...DEFAULT_VISIT_REQUEST_VALUES.registerInfo,
          ...pendingDraft.data.registerInfo,
        },
        contactPoint: {
          ...DEFAULT_VISIT_REQUEST_VALUES.contactPoint,
          ...pendingDraft.data.contactPoint,
        },
        visits: pendingDraft.data.visits?.length
          ? pendingDraft.data.visits
          : DEFAULT_VISIT_REQUEST_VALUES.visits,
        visitors: pendingDraft.data.visitors?.length
          ? pendingDraft.data.visitors
          : DEFAULT_VISIT_REQUEST_VALUES.visitors,
        supportTeam: pendingDraft.data.supportTeam?.length
          ? pendingDraft.data.supportTeam
          : DEFAULT_VISIT_REQUEST_VALUES.supportTeam,
      } as VisitRequestSchema;

      isRestoringDraftRef.current = true;

      form.reset(restoredValues, {
        keepDefaultValues: false,
        keepDirty: false,
        keepTouched: false,
      });

      visitFields.replace(restoredValues.visits);
      visitorFields.replace(restoredValues.visitors);
      supportTeamFields.replace(restoredValues.supportTeam);

      isRestoringDraftRef.current = false;
    }
    setCurrentStep(1);
    setShowRestoreDraftModal(false);
    setPendingDraft(null);
    setDraftHydrated(true);
  };

  const handleDiscardDraft = () => {
    clearVisitRequestDraft();
    form.reset(DEFAULT_VISIT_REQUEST_VALUES);
    setCurrentStep(1);
    setStepAttempted({});
    setShowRestoreDraftModal(false);
    setPendingDraft(null);
    setDraftHydrated(true);
  };

  const requestCloseForm = () => {
    const isDirty = hasAnyUserInput(form.getValues());
    if (!isDirty) {
      onClose();
      return;
    }
    setShowCancelConfirm(true);
  };

  const handleConfirmCancel = () => {
    clearVisitRequestDraft();
    form.reset(DEFAULT_VISIT_REQUEST_VALUES);
    setShowCancelConfirm(false);
    onClose();
  };

  const handleSaveDraft = () => {
    const isDirty = hasAnyUserInput(form.getValues());
    if (!isDirty) {
      setToastMessage(t('visitRequest:draft.emptyMsg'));
      return;
    }
    saveVisitRequestDraft(form.getValues());
    setShowSaveDraftConfirm(true);
  };

  useEffect(() => {
    if (!isOpen) {
      setCurrentStep(1);
      setSuccessResult(null);
    }
  }, [isOpen]);

  const handleNextStep = async () => {
    setStepError(null);
    setStepAttempted((prev) => ({ ...prev, [currentStep]: true }));

    const stepFields: Record<number, string[]> = {
      1: ['registerInfo', 'delegationName', 'visitMode', 'visits', 'purpose', 'workingContent'],
      2: ['visitors', 'supportTeam', 'contactPoint'],
    };
    const fields = stepFields[currentStep];
    if (!fields) { setCurrentStep((s) => s + 1); return; }
    
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const valid = await form.trigger(fields as any);
    if (!valid) {
      setStepError(t('visitRequest:popup.fillAllError'));
      setTimeout(() => {
        const firstError = document.querySelector('.error-scroll-target');
        if (firstError) {
          firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
          // Optionally focus the first invalid input if it exists within the error boundary
          const firstInput = firstError.querySelector('input[aria-invalid="true"], input.border-red-500, .border-red-500 input, .border-red-500');
          if (firstInput && firstInput instanceof HTMLElement) {
            firstInput.focus();
          }
        }
      }, 100);
      return;
    }

    if (currentStep === 1) {
      const values = form.getValues();
      const overlaps = findCampusTimeOverlaps(values.visits || []);
      const isConfirmed = values.timeOverlapConfirmed;
      if (values.visitMode === 'multiple' && overlaps.length > 0 && !isConfirmed) {
        setShowOverlapConfirm(true);
        return; // wait for confirmation
      }
    }

    setCurrentStep((s) => s + 1);
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
            onClick={requestCloseForm}
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
                      <h3 className="text-2xl font-black text-gray-900 mb-2">{t('visitRequest:popup.successTitle')}</h3>
                      <p className="text-gray-500 text-sm mb-4">
                        {t('visitRequest:popup.successDesc')}
                      </p>
                      <div className="inline-flex items-center gap-2 px-4 py-2.5 bg-[#004c91]/10 rounded-xl">
                        <span className="text-xs font-bold text-gray-500 uppercase tracking-wide">{t('visitRequest:popup.reqCode')}</span>
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
                    {t('visitRequest:popup.badge')}
                  </div>
                  <h2 className="text-xl sm:text-2xl font-black tracking-tight mb-1 uppercase">{t('visitRequest:popup.title')}</h2>
                  <p className="text-blue-100/90 font-medium text-xs sm:text-sm max-w-2xl">
                    {t('visitRequest:popup.subtitle')}
                  </p>
                </div>
                <button
                  onClick={requestCloseForm}
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
                            {t(`visitRequest:popup.${step.labelKey}`)}
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
                        <RegisterInfoSection form={form} showErrors={!!stepAttempted[1]} />
                        <VisitInfoSection form={form} visitFields={visitFields} showErrors={!!stepAttempted[1]} />
                      </motion.div>
                    )}

                    {currentStep === 2 && (
                      <motion.div
                        key="step2"
                        initial={{ opacity: 0, x: 20 }}
                        animate={{ opacity: 1, x: 0 }}
                        exit={{ opacity: 0, x: -20 }}
                        transition={{ duration: 0.2 }}
                        className="space-y-8"
                      >
                        <VisitorListSection
                          form={form}
                          visitorFields={visitorFields}
                          showErrors={!!stepAttempted[2]}
                        />
                        <ContactSection
                          form={form}
                          supportTeamFields={supportTeamFields}
                          onSyncSupportFromRegister={syncSupportFromRegister}
                          onClearSupportFirstRow={clearSupportFirstRow}
                          onSyncContactFromRegister={syncContactFromRegister}
                          onClearContactPoint={clearContactPoint}
                          showErrors={!!stepAttempted[2]}
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
              <div className="flex-none py-4 px-6 bg-white border-t border-gray-100 flex items-center justify-between gap-3 rounded-b-2xl shadow-[0_-4px_20px_rgba(0,0,0,0.02)] z-20">
                {/* Left side */}
                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={requestCloseForm}
                    disabled={isSubmitting}
                    className="px-6 py-3 rounded-xl font-bold text-gray-600 bg-white border-2 border-gray-200 hover:bg-gray-50 hover:text-gray-900 transition-colors disabled:opacity-50"
                  >
                    {t('visitRequest:popup.cancel')}
                  </button>
                  {currentStep > 1 && (
                    <button
                      type="button"
                      onClick={() => { setStepError(null); setCurrentStep((s) => s - 1); }}
                      disabled={isSubmitting}
                      className="inline-flex items-center gap-2 px-6 py-3 rounded-xl font-bold text-gray-600 bg-white border-2 border-gray-200 hover:bg-gray-50 hover:text-gray-900 transition-colors disabled:opacity-50"
                    >
                      <ChevronLeft className="w-4 h-4" />
                      {t('visitRequest:popup.back')}
                    </button>
                  )}
                  <button
                    type="button"
                    onClick={handleSaveDraft}
                    disabled={isSubmitting}
                    className="px-4 py-3 rounded-xl font-bold text-[#004c91] bg-blue-50 border-2 border-transparent hover:bg-blue-100 transition-colors disabled:opacity-50"
                  >
                    {t('visitRequest:popup.saveDraft')}
                  </button>
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
                      {t('visitRequest:popup.next')}
                      <ChevronRight className="w-4 h-4" />
                    </button>
                  ) : (
                    <button
                      type="button"
                      disabled={isSubmitting}
                      onClick={onSubmit}
                      className="inline-flex items-center justify-center gap-2 px-8 py-3 rounded-xl font-black tracking-wide text-white bg-gradient-to-r from-[#f37021] to-[#e06111] hover:from-[#e06111] hover:to-[#c4530c] shadow-lg shadow-orange-500/30 transition-all transform hover:-translate-y-0.5 disabled:opacity-60 disabled:transform-none"
                    >
                      {isSubmitting ? (
                        <>
                          <Loader2 className="w-4 h-4 animate-spin" />
                          {t('visitRequest:popup.submitting')}
                        </>
                      ) : (
                        t('visitRequest:popup.submit')
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

      {/* Overlap Confirm Modal */}
      <AnimatePresence>
        {showOverlapConfirm && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[200] bg-black/60 backdrop-blur-sm flex items-center justify-center p-4"
          >
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl p-6 max-w-sm w-full shadow-2xl"
            >
              <div className="flex items-center gap-3 mb-4">
                <div className="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center shrink-0">
                  <AlertCircle className="w-6 h-6 text-amber-600" />
                </div>
                <h3 className="text-lg font-bold text-gray-900">{t('visitRequest:overlaps.title')}</h3>
              </div>
              <p className="text-sm text-gray-600 mb-6" dangerouslySetInnerHTML={{ __html: t('visitRequest:overlaps.desc') }} />
              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowOverlapConfirm(false)}
                  className="px-4 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-bold rounded-xl transition-colors"
                >
                  {t('visitRequest:overlaps.recheck')}
                </button>
                <button
                  type="button"
                  onClick={() => {
                    form.setValue('timeOverlapConfirmed', true, {
                      shouldValidate: false,
                      shouldDirty: true,
                    });
                    setShowOverlapConfirm(false);
                    setCurrentStep((s) => s + 1);
                  }}
                  className="px-4 py-2 bg-amber-500 hover:bg-amber-600 text-white text-sm font-bold rounded-xl transition-colors shadow-lg shadow-amber-500/30"
                >
                  {t('visitRequest:overlaps.continue')}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Restore Draft Modal */}
      <AnimatePresence>
        {showRestoreDraftModal && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[300] bg-black/50 flex items-center justify-center p-4"
          >
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="bg-white rounded-2xl shadow-2xl max-w-md w-full p-6"
            >
              <h3 className="text-lg font-bold text-gray-900 mb-2">
                {t('visitRequest:draft.title')}
              </h3>
              <p className="text-sm text-gray-600 mb-6">
                {t('visitRequest:draft.desc')}
              </p>
              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={handleDiscardDraft}
                  className="px-4 py-2 rounded-xl bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-bold"
                >
                  {t('visitRequest:draft.discard')}
                </button>
                <button
                  type="button"
                  onClick={handleRestoreDraft}
                  className="px-4 py-2 rounded-xl bg-[#004c91] hover:bg-[#013565] text-white text-sm font-bold"
                >
                  {t('visitRequest:draft.restore')}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Cancel Confirm Modal */}
      <AnimatePresence>
        {showCancelConfirm && (
          <div className="fixed inset-0 z-[120] flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-sm">
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="w-full max-w-md overflow-hidden rounded-3xl bg-white shadow-2xl"
            >
              <div className="border-b border-slate-200 px-6 py-5">
                <h3 className="text-lg font-extrabold text-slate-900">
                  {t('visitRequest:cancelConfirm.title')}
                </h3>
                <p className="mt-2 text-sm font-medium text-slate-600">
                  {t('visitRequest:cancelConfirm.desc')}
                </p>
              </div>
              <div className="flex items-center justify-end gap-3 px-6 py-4">
                <button
                  type="button"
                  onClick={() => setShowCancelConfirm(false)}
                  className="px-4 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-bold rounded-xl transition-colors"
                >
                  {t('visitRequest:cancelConfirm.no')}
                </button>
                <button
                  type="button"
                  onClick={handleConfirmCancel}
                  className="px-4 py-2 bg-red-500 hover:bg-red-600 text-white text-sm font-bold rounded-xl transition-colors shadow-lg shadow-red-500/30"
                >
                  {t('visitRequest:cancelConfirm.yes')}
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Save Draft Confirm Modal */}
      <AnimatePresence>
        {showSaveDraftConfirm && (
          <div className="fixed inset-0 z-[120] flex items-center justify-center bg-slate-900/40 p-4 backdrop-blur-sm">
            <motion.div
              initial={{ scale: 0.95, opacity: 0 }}
              animate={{ scale: 1, opacity: 1 }}
              exit={{ scale: 0.95, opacity: 0 }}
              className="w-full max-w-md overflow-hidden rounded-3xl bg-white shadow-2xl"
            >
              <div className="border-b border-slate-200 px-6 py-5">
                <h3 className="text-lg font-extrabold text-slate-900 flex items-center gap-2">
                  <CheckCircle2 className="w-6 h-6 text-green-500" />
                  {t('visitRequest:saveDraftConfirm.title')}
                </h3>
                <p className="mt-2 text-sm font-medium text-slate-600">
                  {t('visitRequest:saveDraftConfirm.desc')}
                </p>
              </div>
              <div className="flex items-center justify-end gap-3 px-6 py-4">
                <button
                  type="button"
                  onClick={() => {
                    setShowSaveDraftConfirm(false);
                    onClose();
                  }}
                  className="px-4 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-bold rounded-xl transition-colors"
                >
                  {t('visitRequest:saveDraftConfirm.saveAndExit')}
                </button>
                <button
                  type="button"
                  onClick={() => setShowSaveDraftConfirm(false)}
                  className="px-4 py-2 bg-[#004c91] hover:bg-[#013565] text-white text-sm font-bold rounded-xl transition-colors shadow-lg shadow-blue-900/30"
                >
                  {t('visitRequest:saveDraftConfirm.saveAndContinue')}
                </button>
              </div>
            </motion.div>
          </div>
        )}
      </AnimatePresence>

      {/* Toast */}
      <AnimatePresence>
        {toastMessage && (
          <motion.div
            initial={{ opacity: 0, y: 50, x: '-50%' }}
            animate={{ opacity: 1, y: 0, x: '-50%' }}
            exit={{ opacity: 0, y: 50, x: '-50%' }}
            className="fixed bottom-6 left-1/2 z-[200] bg-gray-800 text-white px-6 py-3 rounded-full shadow-2xl font-semibold text-sm flex items-center gap-2"
          >
            <AlertCircle className="w-5 h-5 text-amber-400" />
            {toastMessage}
          </motion.div>
        )}
      </AnimatePresence>

    </>
  );
}
