import React, { useEffect, useMemo, useRef, useState } from 'react';
import { X, Loader2, AlertCircle, CheckCircle2, AlertTriangle } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useVisitRequestForm, DEFAULT_VISIT_REQUEST_VALUES } from '../../features/visit-request/hooks/useVisitRequestForm';
import { RegisterInfoSection } from '../../features/visit-request/components/sections/RegisterInfoSection';
import { VisitInfoSection } from '../../features/visit-request/components/sections/VisitInfoSection';
import { VisitorListSection } from '../../features/visit-request/components/sections/VisitorListSection';
import { ContactSection } from '../../features/visit-request/components/sections/ContactSection';
import { AdditionalSection } from '../../features/visit-request/components/sections/AdditionalSection';
import { CampusProcessingSection, type CreatorRole } from '../../features/visit-request/components/sections/CampusProcessingSection';
import { OtpVerificationModal } from '../../features/visit-request/components/OtpVerificationModal';
import { SubmittedVisitRequestSummary, type SubmittedVisitRequest } from '../../features/visit-request/components/SubmittedVisitRequestSummary';
import { findCampusTimeOverlaps } from '../../features/visit-request/schema/visitRequest.schema';
import type { VisitRequestSchema } from '../../features/visit-request/schema/visitRequest.schema';
import type { VerifyResponse, CampusProcessingChoice } from '../../features/visit-request/api/visitRequestApi';
import { loadVisitRequestDraft, saveVisitRequestDraft, hasMeaningfulVisitRequestData, isVisitRequestDraftExpired, clearVisitRequestDraft } from '../../features/visit-request/utils/visitRequestDraftStorage';
import { useAuthContext } from '../../shared/auth/AuthContext';
import { useTranslation } from 'react-i18next';

interface VisitingFormPopupProps {
  isOpen: boolean;
  onClose: () => void;
  /**
   * 'public' (default): anonymous OTP flow. 'authenticated': the signed-in user is the
   * registrant — identity prefilled/read-only, no OTP, per-campus processing for Staff/Leader.
   */
  mode?: 'public' | 'authenticated';
}

export function VisitingFormPopup({ isOpen, onClose, mode = 'public' }: VisitingFormPopupProps) {
  const { t } = useTranslation(['visitRequest']);
  const isAuthenticatedMode = mode === 'authenticated';
  const { user } = useAuthContext();

  // Creator role for the campus-processing options (backend revalidates everything).
  const creatorRole: CreatorRole = useMemo(() => {
    const rc = (user?.roleCode || '').toUpperCase();
    const sr = (user?.subRole || '').toUpperCase();
    if (rc === 'STAFF') return sr === 'LEADER' ? 'STAFF_LEADER' : 'STAFF';
    return 'VISITOR';
  }, [user?.roleCode, user?.subRole]);

  // Per-user draft namespace so accounts on a shared device never see each other's draft.
  const draftNamespace = isAuthenticatedMode && user?.userId ? `u${user.userId}` : undefined;

  const [campusProcessing, setCampusProcessing] = useState<Record<string, CampusProcessingChoice>>({});
  const campusProcessingRef = useRef(campusProcessing);
  campusProcessingRef.current = campusProcessing;

  // UC17 single-form phases: editing → otp (sessionToken) → submitted (submission).
  const [submission, setSubmission] = useState<SubmittedVisitRequest | null>(null);
  const [submitAttempted, setSubmitAttempted] = useState(false);
  const [showOverlapConfirm, setShowOverlapConfirm] = useState(false);

  const [pendingDraft, setPendingDraft] = useState<ReturnType<typeof loadVisitRequestDraft> | null>(null);
  const [showRestoreDraftModal, setShowRestoreDraftModal] = useState(false);
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const formScrollRef = useRef<HTMLDivElement>(null);
  const submittedHeadingRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    if (toastMessage) {
      const timer = setTimeout(() => setToastMessage(null), 3000);
      return () => clearTimeout(timer);
    }
  }, [toastMessage]);

  const handleSuccess = (response: VerifyResponse, values: VisitRequestSchema) => {
    // No auto-close: the user reviews the submitted data and closes the modal themselves.
    blockAutoSave();
    cancelPendingAutoSave();
    clearVisitRequestDraft(isAuthenticatedMode && user?.userId ? `u${user.userId}` : undefined);
    setSubmission({ response, values });
    setSubmitAttempted(false);
    requestAnimationFrame(() => {
      formScrollRef.current?.scrollTo({ top: 0, behavior: 'smooth' });
      submittedHeadingRef.current?.focus({ preventScroll: true });
    });
  };

  const scrollToFirstInvalidField = () => {
    requestAnimationFrame(() => {
      const root = formScrollRef.current;
      const target = root?.querySelector<HTMLElement>(
        '[aria-invalid="true"], [data-field-error="true"], .error-scroll-target'
      );
      if (!target) return;

      target.scrollIntoView({ behavior: 'smooth', block: 'center' });

      if (
        target instanceof HTMLInputElement ||
        target instanceof HTMLSelectElement ||
        target instanceof HTMLTextAreaElement
      ) {
        target.focus({ preventScroll: true });
      } else {
        target
          .querySelector<HTMLElement>('input, select, textarea, button')
          ?.focus({ preventScroll: true });
      }
    });
  };

  const handleInvalidSubmit = (errors: any) => {
    console.warn('Submit blocked by validation:', errors);
    scrollToFirstInvalidField();
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
    remainingAttempts,
    retryAfterSeconds,
    retryAt,
    resendAfterSeconds,
    humanVerificationRequired,
    isRecoveringOtp,
    recoverOtp,
    duplicateResult,
    hostConflictPrompt,
    confirmHostConflictAndSubmit,
    dismissHostConflictPrompt,
    resetVisitRequestForm,
    setDraftHydrated,
    isRestoringDraftRef,
    blockAutoSave,
    unblockAutoSave,
    cancelPendingAutoSave,
  } = useVisitRequestForm(handleSuccess, handleInvalidSubmit, {
    mode,
    draftNamespace,
    getCampusProcessing: () => Object.values(campusProcessingRef.current),
  });

  // Authenticated prefill: identity from the signed-in account (read-only in the UI and
  // overridden server-side anyway); phone is a starting value the user may adjust.
  const applyAccountPrefill = React.useCallback(() => {
    if (!isAuthenticatedMode || !user) return;
    form.setValue('registerInfo.fullName', user.fullName || '', { shouldValidate: false });
    form.setValue('registerInfo.email', user.email || '', { shouldValidate: false });
    if (user.phone && !form.getValues('registerInfo.phone')) {
      form.setValue('registerInfo.phone', user.phone, { shouldValidate: false });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticatedMode, user?.userId]);

  // The duplicate result behaves like the success summary: no auto-close, scroll to top,
  // focus the heading — but it announces "already submitted before" instead of success.
  useEffect(() => {
    if (!duplicateResult) return;
    blockAutoSave();
    cancelPendingAutoSave();
    setSubmitAttempted(false);
    requestAnimationFrame(() => {
      formScrollRef.current?.scrollTo({ top: 0, behavior: 'smooth' });
      submittedHeadingRef.current?.focus({ preventScroll: true });
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [duplicateResult]);

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

    const draft = loadVisitRequestDraft(draftNamespace);
    if (draft && !isVisitRequestDraftExpired(draft)) {
      setPendingDraft(draft);
      setShowRestoreDraftModal(true);
      setDraftHydrated(false);
    } else {
      if (draft) {
        clearVisitRequestDraft(draftNamespace);
      }
      applyAccountPrefill();
      setDraftHydrated(true);
    }
  }, [isOpen, setDraftHydrated, draftNamespace, applyAccountPrefill]);

  // A contact-email business conflict after OTP closes the OTP modal and returns to the
  // editable form: scroll the Contact section into view and focus the email field.
  const contactEmailErrorType = form.formState.errors.contactPoint?.email?.type;
  useEffect(() => {
    if (contactEmailErrorType !== 'server' || sessionToken || submission) return;
    requestAnimationFrame(() => {
      const el = formScrollRef.current?.querySelector<HTMLInputElement>('input[name="contactPoint.email"]');
      el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
      el?.focus({ preventScroll: true });
    });
  }, [contactEmailErrorType, sessionToken, submission]);

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
    // Identity always wins over whatever the (namespaced) draft carried.
    applyAccountPrefill();
    setShowRestoreDraftModal(false);
    setPendingDraft(null);
    setDraftHydrated(true);
  };

  const handleDiscardDraft = () => {
    resetVisitRequestForm();
    applyAccountPrefill();
    setSubmitAttempted(false);
    setShowRestoreDraftModal(false);
    setPendingDraft(null);
    setDraftHydrated(true);
  };

  // Closing the submitted/duplicate view wipes the snapshot and resets the form, so the
  // next open shows a blank form with no PII from the request that was just reviewed.
  // resetVisitRequestForm also clears duplicateResult and the submission intent id.
  const closeSubmittedView = () => {
    setSubmission(null);
    setSubmitAttempted(false);
    resetVisitRequestForm();
    onClose();
  };

  const requestCloseForm = React.useCallback(() => {
    if (submission || duplicateResult) {
      closeSubmittedView();
      return;
    }
    const isDirty = hasMeaningfulVisitRequestData(form.getValues());
    if (!isDirty) {
      cancelPendingAutoSave();
      onClose();
      return;
    }
    setShowCancelConfirm(true);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [submission, duplicateResult, form, cancelPendingAutoSave, onClose]);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        // A child control that already handled this Escape (e.g. react-select closing
        // its open menu calls preventDefault) must not also prompt-close the whole form.
        if (e.defaultPrevented) return;
        if (!showRestoreDraftModal && !showCancelConfirm && !showOverlapConfirm && !sessionToken) {
          requestCloseForm();
        }
      }
    };
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, showRestoreDraftModal, showCancelConfirm, showOverlapConfirm, sessionToken, requestCloseForm]);

  const handleConfirmCancelWithSave = () => {
    blockAutoSave();
    cancelPendingAutoSave();
    const latestValues = form.getValues();
    const result = saveVisitRequestDraft(latestValues, undefined, draftNamespace);
    if (result.success === false) {
      unblockAutoSave();
      setToastMessage(result.error || 'Failed to save draft');
      return;
    }
    setShowCancelConfirm(false);
    onClose();
  };

  const handleConfirmCancelWithoutSave = () => {
    blockAutoSave();
    cancelPendingAutoSave();
    clearVisitRequestDraft(draftNamespace);
    resetVisitRequestForm();
    setShowCancelConfirm(false);
    onClose();
  };

  useEffect(() => {
    if (!isOpen) {
      // Safety net for closes that bypass closeSubmittedView (e.g. parent-driven):
      // never keep a submitted/duplicate snapshot or its form values around for the next open.
      if (submission || duplicateResult) {
        setSubmission(null);
        resetVisitRequestForm();
      }
      setSubmitAttempted(false);
      setCampusProcessing({});
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isOpen]);

  // Full-form submit: validate everything, then confirm overlaps, then initiate OTP.
  const handleSingleFormSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitAttempted(true);
    setSubmitError(null);

    const valid = await form.trigger(undefined, { shouldFocus: true });
    if (!valid) {
      scrollToFirstInvalidField();
      return;
    }

    const values = form.getValues();
    const overlaps = findCampusTimeOverlaps(values.visits || []);
    if (values.visitMode === 'multiple' && overlaps.length > 0 && !values.timeOverlapConfirmed) {
      setShowOverlapConfirm(true);
      return;
    }

    await onSubmit();
  };

  const handleConfirmOverlap = async () => {
    form.setValue('timeOverlapConfirmed', true, {
      shouldDirty: true,
      shouldValidate: false,
    });
    setShowOverlapConfirm(false);
    await onSubmit();
  };

  return (
    <>
      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 p-0 backdrop-blur-sm sm:p-4"
          >
            <motion.div
              initial={{ opacity: 0, scale: 0.97, y: 12 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              exit={{ opacity: 0, scale: 0.97, y: 12 }}
              transition={{ duration: 0.25, ease: 'easeOut' }}
              onClick={(e) => e.stopPropagation()}
              role="dialog"
              aria-modal="true"
              aria-labelledby="visit-request-dialog-title"
              className="relative flex h-[100dvh] w-full flex-col overflow-hidden bg-white sm:h-auto sm:max-h-[92dvh] sm:max-w-6xl sm:rounded-3xl sm:border sm:border-slate-200 sm:shadow-2xl"
            >
              {/* ── Header ── */}
              <div className="flex shrink-0 items-start justify-between gap-4 bg-[#004c91] px-4 py-4 text-white sm:px-6">
                <div className="min-w-0">
                  <h2 id="visit-request-dialog-title" className="text-lg font-extrabold tracking-tight sm:text-xl">
                    {t('visitRequest:popup.title')}
                  </h2>
                  <p className="mt-0.5 text-xs font-medium text-blue-100/90 sm:text-sm">
                    {t('visitRequest:popup.subtitle')}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={requestCloseForm}
                  aria-label={t('visitRequest:popup.cancel')}
                  title={t('visitRequest:popup.cancel')}
                  className="shrink-0 rounded-full p-2 text-white/70 transition-colors hover:bg-white/20 hover:text-white"
                >
                  <X className="h-5 w-5" />
                </button>
              </div>

              {/* ── Body: the single scroll area ── */}
              <div
                ref={formScrollRef}
                className="min-h-0 flex-1 overflow-y-auto px-4 py-6 sm:px-6 lg:px-8 custom-scrollbar"
              >
                {submission ? (
                  <SubmittedVisitRequestSummary submission={submission} headingRef={submittedHeadingRef} />
                ) : duplicateResult ? (
                  <SubmittedVisitRequestSummary
                    submission={{
                      response: {
                        visitRequestId: duplicateResult.data.existingVisitRequestId,
                        requestCode: duplicateResult.data.existingRequestCode,
                        status: duplicateResult.data.existingStatus,
                        message: '',
                      },
                      values: duplicateResult.values,
                    }}
                    duplicate={duplicateResult.data}
                    headingRef={submittedHeadingRef}
                  />
                ) : (
                  <form id="visit-request-form" onSubmit={handleSingleFormSubmit} noValidate>
                    <RegisterInfoSection form={form} showErrors={submitAttempted} identityReadOnly={isAuthenticatedMode} />
                    <VisitInfoSection form={form} visitFields={visitFields} showErrors={submitAttempted} />
                    {isAuthenticatedMode && (
                      <CampusProcessingSection
                        form={form}
                        role={creatorRole}
                        ownCampusCode={user?.campusCode}
                        value={campusProcessing}
                        onChange={setCampusProcessing}
                      />
                    )}
                    <VisitorListSection form={form} visitorFields={visitorFields} showErrors={submitAttempted} />
                    <ContactSection
                      form={form}
                      supportTeamFields={supportTeamFields}
                      onSyncSupportFromRegister={syncSupportFromRegister}
                      onClearSupportFirstRow={clearSupportFirstRow}
                      onSyncContactFromRegister={syncContactFromRegister}
                      onClearContactPoint={clearContactPoint}
                      showErrors={submitAttempted}
                      allowContactSelf={!isAuthenticatedMode || creatorRole === 'VISITOR'}
                    />
                    <AdditionalSection form={form} showErrors={submitAttempted} />
                  </form>
                )}
              </div>

              {/* ── Footer ── */}
              <div className="flex shrink-0 flex-wrap items-center justify-between gap-3 border-t border-slate-200 bg-white px-4 py-3 sm:px-6">
                {submission || duplicateResult ? (
                  <button
                    type="button"
                    onClick={closeSubmittedView}
                    className="ml-auto inline-flex h-11 items-center justify-center rounded-xl bg-[#004c91] px-6 text-sm font-bold text-white transition-colors hover:bg-[#013565] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#004c91]/40"
                  >
                    {t('visitRequest:singleForm.actions.close')}
                  </button>
                ) : (
                  <>
                    <div className="flex flex-wrap items-center gap-3">
                      {/* Left side empty or add other actions if needed */}
                    </div>

                    <div className="flex flex-wrap items-center justify-end gap-3">
                      {submitError && (
                        <div role="alert" className="flex items-center gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-xs font-medium text-red-600">
                          <AlertCircle className="w-4 h-4 shrink-0" />
                          {submitError}
                        </div>
                      )}
                      <button
                        type="submit"
                        form="visit-request-form"
                        disabled={isSubmitting || isVerifying}
                        className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-[#F37021] px-5 text-sm font-bold text-white transition-colors hover:bg-[#d95f18] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#F37021]/40 disabled:cursor-not-allowed disabled:opacity-60"
                      >
                        {isSubmitting ? (
                          <>
                            <Loader2 className="w-4 h-4 animate-spin" />
                            {t('visitRequest:singleForm.actions.submitting')}
                          </>
                        ) : (
                          t('visitRequest:singleForm.actions.submit')
                        )}
                      </button>
                    </div>
                  </>
                )}
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Host schedule conflict confirmation (authenticated direct processing only) */}
      <AnimatePresence>
        {hostConflictPrompt && (
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
              role="alertdialog"
              aria-modal="true"
              aria-label={t('visitRequest:hostConflictConfirm.title')}
            >
              <div className="flex items-center gap-3 mb-4">
                <div className="w-10 h-10 rounded-full bg-amber-100 flex items-center justify-center shrink-0">
                  <AlertTriangle className="w-6 h-6 text-amber-600" />
                </div>
                <h3 className="text-lg font-bold text-gray-900">{t('visitRequest:hostConflictConfirm.title')}</h3>
              </div>
              <p className="text-sm text-gray-600 mb-2">{hostConflictPrompt}</p>
              <p className="text-xs font-medium text-amber-700 mb-6">
                {t('visitRequest:campusProcessing.hostFinalWarning')}
              </p>
              <div className="flex justify-end gap-3">
                <button
                  type="button"
                  onClick={dismissHostConflictPrompt}
                  className="px-4 py-2 bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-bold rounded-xl transition-colors"
                >
                  {t('visitRequest:hostConflictConfirm.cancel')}
                </button>
                <button
                  type="button"
                  disabled={isSubmitting}
                  onClick={() => void confirmHostConflictAndSubmit()}
                  className="px-4 py-2 bg-amber-500 hover:bg-amber-600 text-white text-sm font-bold rounded-xl transition-colors shadow-lg shadow-amber-500/30 disabled:opacity-60"
                >
                  {t('visitRequest:hostConflictConfirm.confirm')}
                </button>
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
          remainingAttempts={remainingAttempts}
          retryAfterSeconds={retryAfterSeconds}
          retryAt={retryAt}
          resendAfterSeconds={resendAfterSeconds}
          humanVerificationRequired={humanVerificationRequired}
          isRecovering={isRecoveringOtp}
          onVerify={verifyOtp}
          onResend={resendOtp}
          onRecover={recoverOtp}
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
                  onClick={handleConfirmOverlap}
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
              className="w-full max-w-sm overflow-hidden rounded-3xl bg-white shadow-2xl"
            >
              <div className="px-6 py-6 text-center">
                <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-blue-50">
                  <AlertCircle className="h-7 w-7 text-[#004c91]" />
                </div>
                <h3 className="text-lg font-extrabold text-slate-900">
                  {t('visitRequest:cancelConfirm.title')}
                </h3>
                <p className="mt-2 text-sm font-medium text-slate-600 leading-relaxed">
                  {t('visitRequest:cancelConfirm.desc')}
                </p>
              </div>
              <div className="flex flex-col gap-2.5 px-6 pb-6">
                <button
                  type="button"
                  onClick={handleConfirmCancelWithSave}
                  className="flex w-full items-center justify-center rounded-xl bg-[#004c91] px-4 py-3 text-sm font-bold text-white transition-colors hover:bg-[#013565] shadow-lg shadow-blue-900/20"
                >
                  {t('visitRequest:cancelConfirm.saveAndExit')}
                </button>
                <button
                  type="button"
                  onClick={() => setShowCancelConfirm(false)}
                  className="flex w-full items-center justify-center rounded-xl border border-slate-300 bg-white px-4 py-3 text-sm font-bold text-slate-700 transition-colors hover:bg-slate-50"
                >
                  {t('visitRequest:cancelConfirm.continue')}
                </button>
                <button
                  type="button"
                  onClick={handleConfirmCancelWithoutSave}
                  className="flex w-full items-center justify-center rounded-xl border border-red-200 bg-white px-4 py-3 text-sm font-bold text-red-600 transition-colors hover:bg-red-50"
                >
                  {t('visitRequest:cancelConfirm.discard')}
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
