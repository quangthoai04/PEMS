import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslation } from 'react-i18next';
import {
  buildVisitRequestSchema,
  VISIT_REQUEST_MIN_ADVANCE_HOURS,
  type VisitRequestSchema,
} from '../schema/visitRequest.schema';
import {
  visitRequestApi,
  type VerifyResponse,
  type DuplicateVisitRequestData,
} from '../api/visitRequestApi';

const DEFAULT_VISITOR = {
  fullName: '',
  jobTitle: '',
  organization: '',
  nationality: '',
};

const DEFAULT_SUPPORT = {
  fullName: '',
  jobTitle: '',
  organization: '',
  nationality: '',
};

export const DEFAULT_VISIT_REQUEST_VALUES: VisitRequestSchema = {
  registerInfo: { fullName: '', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
  delegationName: '',
  visitMode: 'single',
  visitType: 'CAMPUS_TOUR',
  visitTypeOther: '',
  visits: [{ campus: 'HN', startDatetime: '', endDatetime: '' }],
  purpose: '',
  workingContent: '',
  visitors: [{ ...DEFAULT_VISITOR }],
  supportTeam: [],
  contactPoint: { fullName: '', organization: '', phone: '', email: '' },
  workingLanguage: 'VI',
  transportationNote: '',
  mediaConsentStatus: 'DECLINED',
  mediaConsentNote: '',
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  notes: '',
  timeOverlapConfirmed: false,
};

import axios from 'axios';
import { getApiErrorMessage } from '../../../shared/utils/toast';

/** Machine-readable backend error code (response.errorCode), if present. */
function getApiErrorCode(error: unknown): string | null {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as any;
    if (typeof data?.errorCode === 'string' && data.errorCode.trim()) {
      return data.errorCode;
    }
  }
  return null;
}

/** OTP challenge metadata the backend attaches to typed OTP errors. */
interface OtpErrorMeta {
  remainingAttempts: number | null;
  retryAfterSeconds: number | null;
  humanVerificationRequired: boolean;
}

function getOtpErrorMeta(error: unknown): OtpErrorMeta {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as any;
    return {
      remainingAttempts:
        typeof data?.remainingAttempts === 'number' ? data.remainingAttempts : null,
      retryAfterSeconds:
        typeof data?.retryAfterSeconds === 'number' && data.retryAfterSeconds > 0
          ? data.retryAfterSeconds
          : null,
      humanVerificationRequired: data?.humanVerificationRequired === true,
    };
  }
  return { remainingAttempts: null, retryAfterSeconds: null, humanVerificationRequired: false };
}

/** 409 DUPLICATE_VISIT_REQUEST structured payload (response.data.data), if present. */
function getDuplicateData(error: unknown): DuplicateVisitRequestData | null {
  if (axios.isAxiosError(error)) {
    const data = (error.response?.data as any)?.data;
    if (data && typeof data.existingRequestCode === 'string') {
      return data as DuplicateVisitRequestData;
    }
  }
  return null;
}

const CONTACT_EMAIL_CONFLICT = 'CONTACT_EMAIL_CANNOT_BE_USED_FOR_VISITOR_ACCOUNT';
const VISITOR_ACCOUNT_INACTIVE = 'VISITOR_ACCOUNT_INACTIVE';
const DUPLICATE_VISIT_REQUEST = 'DUPLICATE_VISIT_REQUEST';
const OTP_HUMAN_VERIFICATION_REQUIRED = 'OTP_HUMAN_VERIFICATION_REQUIRED';

/** Immutable snapshot shown by the duplicate result screen. */
export interface DuplicateSubmissionResult {
  data: DuplicateVisitRequestData;
  values: VisitRequestSchema;
}

import { saveVisitRequestDraft, loadVisitRequestDraft, clearVisitRequestDraft } from '../utils/visitRequestDraftStorage';

function debounce<T extends (...args: any[]) => void>(fn: T, delay = 700) {
  let timer: ReturnType<typeof setTimeout> | null = null;
  const debounced = (...args: Parameters<T>) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      fn(...args);
    }, delay);
  };
  debounced.cancel = () => {
    if (timer) clearTimeout(timer);
  };
  return debounced;
}

/**
 * Deep-clone the form values so the post-OTP summary shows an immutable snapshot —
 * later field-array replaces/resets must not mutate what the user reviews.
 * UC17 payload is JSON-safe (string/number/boolean/null/array), so the JSON
 * fallback is valid where structuredClone is unavailable.
 */
const cloneVisitRequestValues = (value: VisitRequestSchema): VisitRequestSchema =>
  typeof structuredClone === 'function'
    ? structuredClone(value)
    : (JSON.parse(JSON.stringify(value)) as VisitRequestSchema);

export const useVisitRequestForm = (
  onSuccess: (result: VerifyResponse, submittedValues: VisitRequestSchema) => void,
  onInvalid?: (errors: any) => void
) => {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  // OTP phase state
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [maskedEmail, setMaskedEmail] = useState<string>('');
  const [otpError, setOtpError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isResending, setIsResending] = useState(false);

  // OTP V2 challenge state — attempt/cooldown values always come from the BACKEND;
  // the frontend only presents them and never counts attempts itself.
  const [remainingAttempts, setRemainingAttempts] = useState<number | null>(null);
  const [retryAfterSeconds, setRetryAfterSeconds] = useState<number | null>(null);
  const [resendAfterSeconds, setResendAfterSeconds] = useState<number>(60);
  const [humanVerificationRequired, setHumanVerificationRequired] = useState(false);
  const [isRecoveringOtp, setIsRecoveringOtp] = useState(false);
  const [duplicateResult, setDuplicateResult] = useState<DuplicateSubmissionResult | null>(null);

  // UUID of ONE submit intent (kept across initiate/resend/recover/verify so backend
  // idempotency can collapse retries). Reset when the intent concludes or is abandoned.
  const submissionIdRef = useRef<string | null>(null);

  const [draftHydrated, setDraftHydrated] = useState(false);
  const isRestoringDraftRef = useRef(false);

  const { t, i18n } = useTranslation(['validation', 'toast', 'visitRequest']);

  const resetOtpChallengeState = useCallback(() => {
    setOtpError(null);
    setRemainingAttempts(null);
    setRetryAfterSeconds(null);
    setHumanVerificationRequired(false);
    setIsRecoveringOtp(false);
  }, []);

  // Zod bakes messages in at construction, so the schema must be rebuilt whenever the
  // language changes — otherwise validation keeps the language active on first render.
  const schema = useMemo(
    () => buildVisitRequestSchema(VISIT_REQUEST_MIN_ADVANCE_HOURS, (key, options) =>
      t(key, { ns: 'validation', ...options }),
    ),
    [t, i18n.language],
  );

  const form = useForm<VisitRequestSchema>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(schema) as any,
    mode: 'onBlur',
    reValidateMode: 'onChange',
    defaultValues: DEFAULT_VISIT_REQUEST_VALUES,
  });

  // Errors already on screen keep their old-language message until re-validated.
  const hasErrors = Object.keys(form.formState.errors).length > 0;
  useEffect(() => {
    if (hasErrors) form.trigger();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [i18n.language]);

  // No auto-restore here anymore.
  // The UI (VisitingFormPopup) is solely responsible for reading the draft
  // and deciding whether to prompt the user to restore it.

  const autoSaveBlockedRef = useRef(false);
  const debouncedSaveRef = useRef<ReturnType<typeof debounce> | null>(null);

  useEffect(() => {
    if (!draftHydrated) return;

    debouncedSaveRef.current = debounce((value: Partial<VisitRequestSchema>) => {
      if (autoSaveBlockedRef.current || isRestoringDraftRef.current) return;
      saveVisitRequestDraft(value);
    }, 700);

    const subscription = form.watch((value) => {
      debouncedSaveRef.current?.(value as Partial<VisitRequestSchema>);
    });

    return () => {
      subscription.unsubscribe();
      debouncedSaveRef.current?.cancel();
    };
  }, [form, draftHydrated]);

  const blockAutoSave = useCallback(() => {
    autoSaveBlockedRef.current = true;
  }, []);

  const unblockAutoSave = useCallback(() => {
    autoSaveBlockedRef.current = false;
  }, []);

  const cancelPendingAutoSave = useCallback(() => {
    debouncedSaveRef.current?.cancel();
  }, []);

  const contactEmailWatch = form.watch('contactPoint.email');
  const registerEmailWatch = form.watch('registerInfo.email');

  useEffect(() => {
    const contactState = form.getFieldState('contactPoint.email');
    if (contactState.error?.type === 'server') {
      form.clearErrors('contactPoint.email');
      setSubmitError(null);
    }
    const registerState = form.getFieldState('registerInfo.email');
    if (registerState.error?.type === 'server') {
      form.clearErrors('registerInfo.email');
      setSubmitError(null);
    }
  }, [contactEmailWatch, registerEmailWatch, form]);

  const visitFields = useFieldArray({ control: form.control, name: 'visits' });
  const visitorFields = useFieldArray({ control: form.control, name: 'visitors' });
  const supportTeamFields = useFieldArray({ control: form.control, name: 'supportTeam' });

  const normalizeText = (value?: string | null) => (value ?? '').trim().replace(/\s+/g, ' ').toLowerCase();

  const isSameSupportPerson = (a: any, b: any) => {
    return (
      normalizeText(a.fullName) === normalizeText(b.fullName) &&
      normalizeText(a.jobTitle) === normalizeText(b.jobTitle) &&
      normalizeText(a.organization) === normalizeText(b.organization) &&
      normalizeText(a.nationality) === normalizeText(b.nationality)
    );
  };

  const syncSupportFromRegister = () => {
    const reg = form.getValues('registerInfo');
    const registrantAsSupport = {
      fullName: reg.fullName,
      jobTitle: reg.jobTitle,
      organization: reg.organization,
      nationality: reg.nationality,
      isAutoFilledFromRegistrant: true,
    };

    const currentTeam = form.getValues('supportTeam') || [];
    
    let existingIndex = currentTeam.findIndex((member) => member.isAutoFilledFromRegistrant);
    if (existingIndex === -1) {
      existingIndex = currentTeam.findIndex((member) => isSameSupportPerson(member, registrantAsSupport));
    }

    if (existingIndex >= 0) {
      form.setValue(`supportTeam.${existingIndex}.fullName`, registrantAsSupport.fullName, { shouldValidate: true, shouldDirty: true, shouldTouch: true });
      form.setValue(`supportTeam.${existingIndex}.jobTitle`, registrantAsSupport.jobTitle, { shouldValidate: true, shouldDirty: true, shouldTouch: true });
      form.setValue(`supportTeam.${existingIndex}.organization`, registrantAsSupport.organization, { shouldValidate: true, shouldDirty: true, shouldTouch: true });
      form.setValue(`supportTeam.${existingIndex}.nationality`, registrantAsSupport.nationality, { shouldValidate: true, shouldDirty: true, shouldTouch: true });
      form.setValue(`supportTeam.${existingIndex}.isAutoFilledFromRegistrant`, true);
    } else {
      const nextSupportMembers = [...currentTeam, registrantAsSupport];
      form.setValue('supportTeam', nextSupportMembers, { shouldValidate: true, shouldDirty: true, shouldTouch: true });
      
      const newIndex = nextSupportMembers.length - 1;
      form.setValue(`supportTeam.${newIndex}.fullName`, registrantAsSupport.fullName, { shouldValidate: true });
      form.setValue(`supportTeam.${newIndex}.jobTitle`, registrantAsSupport.jobTitle, { shouldValidate: true });
      form.setValue(`supportTeam.${newIndex}.organization`, registrantAsSupport.organization, { shouldValidate: true });
      form.setValue(`supportTeam.${newIndex}.nationality`, registrantAsSupport.nationality, { shouldValidate: true });
    }
    
    form.trigger('supportTeam');
  };

  const addSupportMember = useCallback(() => {
    const currentTeam = form.getValues('supportTeam') || [];
    form.setValue('supportTeam', [...currentTeam, { ...DEFAULT_SUPPORT }]);
  }, [form]);

  const clearSupportFirstRow = () => {
    const currentTeam = form.getValues('supportTeam') || [];
    const filteredMembers = currentTeam.filter((m) => !m.isAutoFilledFromRegistrant);
    form.setValue('supportTeam', filteredMembers, { shouldValidate: true });
  };

  const syncContactFromRegister = () => {
    const reg = form.getValues('registerInfo');
    form.setValue('contactPoint', {
      fullName: reg.fullName,
      organization: reg.organization,
      phone: reg.phone,
      email: reg.email,
    }, { shouldValidate: true });
    form.trigger('contactPoint');
  };

  const clearContactPoint = () => {
    form.setValue('contactPoint', { fullName: '', organization: '', phone: '', email: '' });
  };


  // Step 1: Validate form → call /initiate → open OTP popup.
  // Every initiate call starts a NEW submit intent: a fresh submissionId is generated
  // here and kept unchanged across resend/recover/verify until the intent concludes
  // (success/duplicate) or is abandoned (cancel/reset).
  const onSubmit = form.handleSubmit(async (data) => {
    setIsSubmitting(true);
    setSubmitError(null);
    try {
      const submissionId = crypto.randomUUID();
      submissionIdRef.current = submissionId;

      const res = await visitRequestApi.initiate(data, submissionId);
      if ((res as any).success === false) {
        throw new Error((res as any).message || t('toast:visitRequest.otpSendFailed'));
      }
      if (!res?.sessionToken) {
        throw new Error(t('toast:visitRequest.otpTokenMissing'));
      }
      resetOtpChallengeState();
      setRemainingAttempts(res.maxAttempts ?? null);
      setResendAfterSeconds(res.resendAfterSeconds ?? 60);
      setSessionToken(res.sessionToken);
      setMaskedEmail(res.maskedEmail);
    } catch (error) {
      console.error('UC-17 submit/initiate failed', error);
      const message = getApiErrorMessage(error, t('toast:visitRequest.submitFailed'));
      setSessionToken(null);
      submissionIdRef.current = null;
      setSubmitError(message);
      mapContactEmailError(error, message);
    } finally {
      setIsSubmitting(false);
    }
  }, onInvalid);

  // Surface contact-email business conflicts on the specific field so the user knows
  // exactly which input to change (not just a generic submit banner).
  const mapContactEmailError = (error: unknown, message: string) => {
    const code = getApiErrorCode(error);
    if (code === CONTACT_EMAIL_CONFLICT || code === VISITOR_ACCOUNT_INACTIVE) {
      form.setError('contactPoint.email', { type: 'server', message });
    }
  };

  // Step 2: Verify OTP → create visit request
  const verifyOtp = async (otpCode: string) => {
    if (!sessionToken || !submissionIdRef.current) return;
    setIsVerifying(true);
    setOtpError(null);
    try {
      // Resubmit the full form (kept in the form state) together with the OTP.
      // Snapshot BEFORE the call so the summary can't drift if the form is reset later.
      const submittedValues = cloneVisitRequestValues(form.getValues());
      const result = await visitRequestApi.verify(
        submittedValues, otpCode, submissionIdRef.current, sessionToken);
      setSessionToken(null);
      submissionIdRef.current = null;
      resetOtpChallengeState();
      clearVisitRequestDraft();
      onSuccess(result, submittedValues);
    } catch (err: any) {
      const code = getApiErrorCode(err);
      // A contact-email business conflict is not an OTP problem — close the OTP modal,
      // return to the form and surface the message on the contact email field.
      if (code === CONTACT_EMAIL_CONFLICT || code === VISITOR_ACCOUNT_INACTIVE) {
        const message = getApiErrorMessage(err);
        setSessionToken(null);
        setSubmitError(message);
        mapContactEmailError(err, message);
      } else if (code === DUPLICATE_VISIT_REQUEST) {
        // Duplicate is a RESULT, not an OTP error: close the OTP modal and show the
        // dedicated "already submitted" result screen with the submitted snapshot.
        const duplicateData = getDuplicateData(err);
        const submittedValues = cloneVisitRequestValues(form.getValues());
        setSessionToken(null);
        submissionIdRef.current = null;
        resetOtpChallengeState();
        clearVisitRequestDraft();
        if (duplicateData) {
          setDuplicateResult({ data: duplicateData, values: submittedValues });
        } else {
          setSubmitError(getApiErrorMessage(err));
        }
      } else {
        console.error('UC-17 OTP verify failed:', err?.response?.status, err?.response?.data);
        const meta = getOtpErrorMeta(err);
        if (meta.remainingAttempts !== null) setRemainingAttempts(meta.remainingAttempts);
        setRetryAfterSeconds(meta.retryAfterSeconds);
        if (meta.humanVerificationRequired || code === OTP_HUMAN_VERIFICATION_REQUIRED) {
          setHumanVerificationRequired(true);
        }
        setOtpError(getApiErrorMessage(err, t('toast:common.defaultError')));
      }
    } finally {
      setIsVerifying(false);
    }
  };

  // Resend swaps the old challenge for a new one — the NEW sessionToken replaces the old.
  const resendOtp = async () => {
    if (!sessionToken || !submissionIdRef.current) return;
    setIsResending(true);
    setOtpError(null);
    try {
      const data = form.getValues();
      const res = await visitRequestApi.resendOtp(
        data.registerInfo.email, data.registerInfo.fullName,
        submissionIdRef.current, sessionToken);
      setSessionToken(res.sessionToken);
      resetOtpChallengeState();
      setRemainingAttempts(res.maxAttempts ?? null);
      setResendAfterSeconds(res.resendAfterSeconds ?? 60);
    } catch (err: any) {
      const meta = getOtpErrorMeta(err);
      if (meta.humanVerificationRequired || getApiErrorCode(err) === OTP_HUMAN_VERIFICATION_REQUIRED) {
        setHumanVerificationRequired(true);
      }
      setOtpError(getApiErrorMessage(err, t('toast:visitRequest.otpResendFailed')));
    } finally {
      setIsResending(false);
    }
  };

  // Human-verification recovery: Turnstile token → brand-new challenge (attempts reset).
  const recoverOtp = async (humanVerificationToken: string) => {
    if (!sessionToken || !submissionIdRef.current || isRecoveringOtp) return;
    setIsRecoveringOtp(true);
    setOtpError(null);
    try {
      const data = form.getValues();
      const res = await visitRequestApi.recoverOtp(
        submissionIdRef.current, sessionToken,
        humanVerificationToken, data.registerInfo.fullName);
      setSessionToken(res.sessionToken);
      resetOtpChallengeState();
      setRemainingAttempts(res.maxAttempts ?? null);
      setResendAfterSeconds(res.resendAfterSeconds ?? 60);
    } catch (err: any) {
      // Stay on the human-verification screen; the user may retry the CAPTCHA.
      setIsRecoveringOtp(false);
      setOtpError(getApiErrorMessage(err, t('toast:common.defaultError')));
      return;
    }
    setIsRecoveringOtp(false);
  };

  const cancelOtp = () => {
    setSessionToken(null);
    submissionIdRef.current = null;
    resetOtpChallengeState();
  };

  const clearDuplicateResult = useCallback(() => {
    setDuplicateResult(null);
  }, []);

  const resetVisitRequestForm = () => {
    const defaults = cloneVisitRequestValues(DEFAULT_VISIT_REQUEST_VALUES);
    
    blockAutoSave();
    cancelPendingAutoSave();

    form.reset(defaults);
    visitFields.replace(defaults.visits);
    visitorFields.replace(defaults.visitors);
    supportTeamFields.replace(defaults.supportTeam);
    form.clearErrors();
    setSessionToken(null);
    setMaskedEmail('');
    setSubmitError(null);
    setDuplicateResult(null);
    submissionIdRef.current = null;
    resetOtpChallengeState();
    clearVisitRequestDraft();
  };

  return {
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
    // OTP phase
    sessionToken,
    maskedEmail,
    otpError,
    isVerifying,
    isResending,
    verifyOtp,
    resendOtp,
    cancelOtp,
    // OTP V2 challenge state (server-driven presentation values)
    remainingAttempts,
    retryAfterSeconds,
    resendAfterSeconds,
    humanVerificationRequired,
    isRecoveringOtp,
    recoverOtp,
    // Duplicate result (a result state, never an OTP error)
    duplicateResult,
    clearDuplicateResult,
    resetVisitRequestForm,
    draftHydrated,
    setDraftHydrated,
    isRestoringDraftRef,
    blockAutoSave,
    unblockAutoSave,
    cancelPendingAutoSave,
  };
};
