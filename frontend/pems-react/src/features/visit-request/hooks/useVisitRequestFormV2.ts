import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useFieldArray, useForm, type FieldPath } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useTranslation } from 'react-i18next';
import axios from 'axios';
import {
  buildVisitRequestV2Schema,
  V2_MAX_CAMPUSES,
  V2_MIN_ADVANCE_HOURS_CREATE,
  type VisitRequestV2Schema,
} from '../schema/visitRequestV2.schema';
import {
  applyContentToAllCampuses,
  buildV2CreatePayload,
  campusVisitHasUserContent,
  cloneCampusVisitContent,
  createEmptyCampusVisit,
  listOverwrittenCampuses,
  mapServerFieldPathToFormPath,
  newClientKey,
} from '../utils/visitRequestV2Form';
import {
  clearVisitRequestV2Draft,
  loadVisitRequestV2DraftWithMigration,
  saveVisitRequestV2Draft,
} from '../utils/visitRequestV2DraftStorage';
import { visitRequestApi, type CampusProcessingChoice } from '../api/visitRequestApi';
import {
  createVisitRequestV2,
  initiateVisitRequestV2,
  verifyAndCreateVisitRequestV2,
  type V2CreateResponse,
} from '../api/visitRequestV2Api';
import { getApiErrorMessage } from '../../../shared/utils/toast';

/** Machine-readable backend error code (response.errorCode), if present. */
function getApiErrorCode(error: unknown): string | null {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { errorCode?: unknown } | undefined;
    if (typeof data?.errorCode === 'string' && data.errorCode.trim()) return data.errorCode;
  }
  return null;
}

/** FluentValidation error dictionary (`errors: { "Form.CampusVisits[0].X": [...] }`), if present. */
function getApiFieldErrors(error: unknown): Record<string, string[]> | null {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { errors?: unknown } | undefined;
    if (data?.errors && typeof data.errors === 'object' && !Array.isArray(data.errors)) {
      return data.errors as Record<string, string[]>;
    }
  }
  return null;
}

interface OtpErrorMeta {
  remainingAttempts: number | null;
  retryAfterSeconds: number | null;
  retryAt: string | null;
  humanVerificationRequired: boolean;
}

function getOtpErrorMeta(error: unknown): OtpErrorMeta {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as Record<string, unknown> | undefined;
    return {
      remainingAttempts: typeof data?.remainingAttempts === 'number' ? data.remainingAttempts : null,
      retryAfterSeconds:
        typeof data?.retryAfterSeconds === 'number' && data.retryAfterSeconds > 0
          ? data.retryAfterSeconds
          : null,
      retryAt: typeof data?.retryAt === 'string' ? data.retryAt : null,
      humanVerificationRequired: data?.humanVerificationRequired === true,
    };
  }
  return { remainingAttempts: null, retryAfterSeconds: null, retryAt: null, humanVerificationRequired: false };
}

const OTP_HUMAN_VERIFICATION_REQUIRED = 'OTP_HUMAN_VERIFICATION_REQUIRED';

// eslint-disable-next-line @typescript-eslint/no-explicit-any
function debounce<T extends (...args: any[]) => void>(fn: T, delay = 700) {
  let timer: ReturnType<typeof setTimeout> | null = null;
  const debounced = (...args: Parameters<T>) => {
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => fn(...args), delay);
  };
  debounced.cancel = () => {
    if (timer) clearTimeout(timer);
  };
  return debounced;
}

const cloneValues = (value: VisitRequestV2Schema): VisitRequestV2Schema =>
  typeof structuredClone === 'function'
    ? structuredClone(value)
    : (JSON.parse(JSON.stringify(value)) as VisitRequestV2Schema);

export const DEFAULT_VISIT_REQUEST_V2_VALUES = (): VisitRequestV2Schema => ({
  registerInfo: { fullName: '', organization: '', jobTitle: '', phone: '', email: '', nationality: '' },
  contactPoint: { fullName: '', organization: '', phone: '', email: '' },
  partnerSelectionMode: 'NEW_ORGANIZATION',
  partnerId: null,
  campusVisits: [createEmptyCampusVisit()],
});

export interface UseVisitRequestFormV2Options {
  /** 'public' (default): v1 initiate → OTP → v2 verify-create. 'authenticated': direct v2 create. */
  mode?: 'public' | 'authenticated';
  /** Per-user namespace for the draft storage (required in authenticated mode). */
  draftNamespace?: string;
  /** Supplier of per-campus processing choices (authenticated Staff/Leader only). */
  getCampusProcessing?: () => CampusProcessingChoice[];
  minAdvanceHours?: number;
  /**
   * How many campuses are open for registration right now (from the backend). Caps the campus
   * array so the limit tracks the real campus list instead of a hardcoded number.
   */
  maxCampuses?: number;
}

/** Pending apply-to-all confirmation: which card is the source and whose content gets replaced. */
export interface ApplyToAllPrompt {
  sourceIndex: number;
  overwrittenLabels: string[];
}

/**
 * Per-campus form v2 hook: `campusVisits[]` field array with STABLE client keys, one-time
 * deep copy between campuses (never shared state), confirmed apply-to-all, draft
 * autosave/migration, and the real v2 create contract on submit — public (OTP) and
 * authenticated. The backend re-validates and re-authorizes everything.
 */
export const useVisitRequestFormV2 = (
  onSuccess: (result: V2CreateResponse, submittedValues: VisitRequestV2Schema) => void,
  onInvalid?: (errors: unknown) => void,
  options?: UseVisitRequestFormV2Options,
) => {
  const mode = options?.mode ?? 'public';
  const isAuthenticatedMode = mode === 'authenticated';
  const draftNamespace = options?.draftNamespace;
  const minAdvanceHours = options?.minAdvanceHours ?? V2_MIN_ADVANCE_HOURS_CREATE;

  const { t, i18n } = useTranslation(['validation', 'toast', 'visitRequestV2']);

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  /** Index of the campus card holding the first server/client error — the UI expands + focuses it. */
  const [firstErrorCampusIndex, setFirstErrorCampusIndex] = useState<number | null>(null);

  // OTP phase state (public mode; server-driven presentation values, same as v1)
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [maskedEmail, setMaskedEmail] = useState('');
  const [otpError, setOtpError] = useState<string | null>(null);
  const [isVerifying, setIsVerifying] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const [remainingAttempts, setRemainingAttempts] = useState<number | null>(null);
  const [retryAfterSeconds, setRetryAfterSeconds] = useState<number | null>(null);
  const [retryAt, setRetryAt] = useState<string | null>(null);
  const [resendAfterSeconds, setResendAfterSeconds] = useState(60);
  const [humanVerificationRequired, setHumanVerificationRequired] = useState(false);
  const [isRecoveringOtp, setIsRecoveringOtp] = useState(false);

  const [applyToAllPrompt, setApplyToAllPrompt] = useState<ApplyToAllPrompt | null>(null);

  const [draftHydrated, setDraftHydrated] = useState(false);
  const [migratedFromGlobalDraft, setMigratedFromGlobalDraft] = useState(false);
  /** When set, a saved draft is waiting for the user to restore or discard it. */
  const [draftAvailableAt, setDraftAvailableAt] = useState<number | null>(null);

  const submissionIdRef = useRef<string | null>(null);
  const autoSaveBlockedRef = useRef(false);
  const debouncedSaveRef = useRef<ReturnType<typeof debounce> | null>(null);

  const maxCampuses = options?.maxCampuses ?? V2_MAX_CAMPUSES;

  const schema = useMemo(
    () => buildVisitRequestV2Schema(
      minAdvanceHours, (key, opts) => t(key, { ns: 'validation', ...opts }), maxCampuses),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, i18n.language, minAdvanceHours, maxCampuses],
  );

  const form = useForm<VisitRequestV2Schema>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(schema) as any,
    mode: 'onBlur',
    reValidateMode: 'onChange',
    defaultValues: DEFAULT_VISIT_REQUEST_V2_VALUES(),
  });

  // React keys come from the DATA clientKey (stable across replace/drafts) — `id` here is
  // only RHF's render bookkeeping and is never persisted.
  const campusVisitFields = useFieldArray({ control: form.control, name: 'campusVisits' });

  const hasErrors = Object.keys(form.formState.errors).length > 0;
  useEffect(() => {
    if (hasErrors) form.trigger();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [i18n.language]);

  // ── Draft: hydrate once (with global→per-campus migration), then autosave ──
  const hydrateDraft = useCallback((): boolean => {
    const { draft, migratedFromGlobalDraft: migrated } = loadVisitRequestV2DraftWithMigration(draftNamespace);
    if (!draft) {
      setDraftHydrated(true);
      return false;
    }
    const defaults = DEFAULT_VISIT_REQUEST_V2_VALUES();
    form.reset({
      ...defaults,
      ...draft.data,
      campusVisits:
        draft.data.campusVisits && draft.data.campusVisits.length > 0
          ? draft.data.campusVisits.map(cv => ({ ...createEmptyCampusVisit(cv.clientKey || newClientKey()), ...cv }))
          : defaults.campusVisits,
    });
    setMigratedFromGlobalDraft(migrated);
    setDraftHydrated(true);
    return true;
  }, [form, draftNamespace]);

  /**
   * Reports whether a usable draft exists WITHOUT applying it, so the user is asked first — v1
   * offered "restore" / "discard" and silently overwriting the form is the behaviour that lost
   * work. Autosave stays blocked until they decide; otherwise the empty form would immediately
   * overwrite the very draft being offered.
   */
  const detectDraft = useCallback((): boolean => {
    const { draft } = loadVisitRequestV2DraftWithMigration(draftNamespace);
    if (!draft) {
      setDraftHydrated(true);
      return false;
    }
    autoSaveBlockedRef.current = true;
    setDraftAvailableAt(draft.savedAt ?? null);
    return true;
  }, [draftNamespace]);

  /** Applies the offered draft and resumes autosave. */
  const restoreDraft = useCallback(() => {
    hydrateDraft();
    setDraftAvailableAt(null);
    autoSaveBlockedRef.current = false;
  }, [hydrateDraft]);

  const discardDraft = useCallback(() => {
    clearVisitRequestV2Draft(draftNamespace);
    setMigratedFromGlobalDraft(false);
    setDraftAvailableAt(null);
    autoSaveBlockedRef.current = false;
    setDraftHydrated(true);
  }, [draftNamespace]);

  /** Force-saves immediately, bypassing the debounce — for "save draft and exit". */
  const saveDraftNow = useCallback(() => {
    saveVisitRequestV2Draft(form.getValues(), undefined, draftNamespace);
  }, [form, draftNamespace]);

  useEffect(() => {
    if (!draftHydrated) return;
    debouncedSaveRef.current = debounce((value: Partial<VisitRequestV2Schema>) => {
      if (autoSaveBlockedRef.current) return;
      saveVisitRequestV2Draft(value, undefined, draftNamespace);
    }, 700);
    const subscription = form.watch(value => {
      debouncedSaveRef.current?.(value as Partial<VisitRequestV2Schema>);
    });
    return () => {
      subscription.unsubscribe();
      debouncedSaveRef.current?.cancel();
    };
  }, [form, draftHydrated, draftNamespace]);

  // ── Campus card operations ──
  const addCampusVisit = useCallback(
    (copyFromIndex?: number) => {
      const current = form.getValues('campusVisits');
      if (current.length >= Math.min(maxCampuses, V2_MAX_CAMPUSES)) return false;
      const fresh = createEmptyCampusVisit();
      const source = copyFromIndex !== undefined ? current[copyFromIndex] : undefined;
      campusVisitFields.append(source ? cloneCampusVisitContent(source, fresh) : fresh);
      return true;
    },
    [form, campusVisitFields, maxCampuses],
  );

  /** Caller confirms first when the card has user content (`campusVisitHasUserContent`). */
  const removeCampusVisit = useCallback(
    (index: number) => {
      const current = form.getValues('campusVisits');
      if (current.length <= 1) return false;
      campusVisitFields.remove(index);
      return true;
    },
    [form, campusVisitFields],
  );

  /** One-time copy INTO an existing card (its campus + schedule are preserved). */
  const copyContentIntoCampus = useCallback(
    (targetIndex: number, sourceIndex: number) => {
      const current = form.getValues('campusVisits');
      const source = current[sourceIndex];
      const target = current[targetIndex];
      if (!source || !target || sourceIndex === targetIndex) return;
      campusVisitFields.update(targetIndex, cloneCampusVisitContent(source, target));
    },
    [form, campusVisitFields],
  );

  /** Step 1 of apply-to-all: build the confirmation prompt (never applies by itself). */
  const requestApplyToAll = useCallback(
    (sourceIndex: number, labelOf: (cv: VisitRequestV2Schema['campusVisits'][number], index: number) => string) => {
      const current = form.getValues('campusVisits');
      if (current.length < 2) return;
      setApplyToAllPrompt({
        sourceIndex,
        overwrittenLabels: listOverwrittenCampuses(current, sourceIndex, labelOf),
      });
    },
    [form],
  );

  /** Step 2: the user explicitly confirmed the listed overwrites. */
  const confirmApplyToAll = useCallback(() => {
    if (!applyToAllPrompt) return;
    const current = form.getValues('campusVisits');
    const next = applyContentToAllCampuses(current, applyToAllPrompt.sourceIndex);
    campusVisitFields.replace(next);
    setApplyToAllPrompt(null);
  }, [applyToAllPrompt, form, campusVisitFields]);

  const cancelApplyToAll = useCallback(() => setApplyToAllPrompt(null), []);

  // ── Server error mapping: land field errors on the exact campus card ──
  const applyServerFieldErrors = useCallback(
    (error: unknown): boolean => {
      const fieldErrors = getApiFieldErrors(error);
      if (!fieldErrors) return false;
      let firstCampusIndex: number | null = null;
      let mappedAny = false;
      for (const [serverPath, messages] of Object.entries(fieldErrors)) {
        const formPath = mapServerFieldPathToFormPath(serverPath);
        if (!formPath || !messages?.length) continue;
        form.setError(formPath as FieldPath<VisitRequestV2Schema>, { type: 'server', message: messages[0] });
        mappedAny = true;
        const campusMatch = /^campusVisits\.(\d+)\./.exec(formPath);
        if (campusMatch) {
          const idx = Number(campusMatch[1]);
          if (firstCampusIndex === null || idx < firstCampusIndex) firstCampusIndex = idx;
        }
      }
      if (firstCampusIndex !== null) setFirstErrorCampusIndex(firstCampusIndex);
      return mappedAny;
    },
    [form],
  );

  const resetOtpChallengeState = useCallback(() => {
    setOtpError(null);
    setRemainingAttempts(null);
    setRetryAfterSeconds(null);
    setRetryAt(null);
    setHumanVerificationRequired(false);
    setIsRecoveringOtp(false);
  }, []);

  // ── Submit ──
  const submitAuthenticated = useCallback(
    async (data: VisitRequestV2Schema) => {
      setIsSubmitting(true);
      setSubmitError(null);
      setFirstErrorCampusIndex(null);
      try {
        const submissionId = submissionIdRef.current ?? crypto.randomUUID();
        submissionIdRef.current = submissionId;
        const submittedValues = cloneValues(data);
        const payload = buildV2CreatePayload(
          submittedValues, submissionId, options?.getCampusProcessing?.() ?? []);
        const result = await createVisitRequestV2(payload);
        submissionIdRef.current = null;
        clearVisitRequestV2Draft(draftNamespace);
        onSuccess(result, submittedValues);
      } catch (error) {
        submissionIdRef.current = null;
        const mapped = applyServerFieldErrors(error);
        setSubmitError(getApiErrorMessage(error, t('toast:visitRequest.submitFailed')));
        if (!mapped) console.error('v2 authenticated create failed', getApiErrorCode(error));
      } finally {
        setIsSubmitting(false);
      }
    },
    [applyServerFieldErrors, draftNamespace, onSuccess, options, t],
  );

  const onSubmit = form.handleSubmit(async data => {
    if (isAuthenticatedMode) {
      await submitAuthenticated(data);
      return;
    }
    // Public: mint the OTP challenge through the v2 initiate endpoint. The FULL v2 form is
    // sent and its snapshot is bound server-side, so 30-minute / zero-support submissions
    // work and verify builds from exactly this form — no v1 projection, no silent fallback.
    setIsSubmitting(true);
    setSubmitError(null);
    setFirstErrorCampusIndex(null);
    try {
      const submissionId = crypto.randomUUID();
      submissionIdRef.current = submissionId;
      const res = await initiateVisitRequestV2(buildV2CreatePayload(data, submissionId));
      if (!res?.sessionToken) throw new Error(t('toast:visitRequest.otpTokenMissing'));
      resetOtpChallengeState();
      setRemainingAttempts(res.maxAttempts ?? null);
      setResendAfterSeconds(res.resendAfterSeconds ?? 60);
      setSessionToken(res.sessionToken);
      setMaskedEmail(res.maskedEmail);
    } catch (error) {
      submissionIdRef.current = null;
      setSessionToken(null);
      applyServerFieldErrors(error);
      setSubmitError(getApiErrorMessage(error, t('toast:visitRequest.submitFailed')));
    } finally {
      setIsSubmitting(false);
    }
  }, errors => {
    // Expand + focus the first campus card with a client-side error.
    const campusErrors = (errors as { campusVisits?: unknown[] }).campusVisits;
    if (Array.isArray(campusErrors)) {
      const idx = campusErrors.findIndex(e => e != null);
      if (idx >= 0) setFirstErrorCampusIndex(idx);
    }
    onInvalid?.(errors);
  });

  const verifyOtp = useCallback(
    async (otpCode: string) => {
      if (!sessionToken || !submissionIdRef.current) return;
      setIsVerifying(true);
      setOtpError(null);
      try {
        const submittedValues = cloneValues(form.getValues());
        const payload = buildV2CreatePayload(submittedValues, submissionIdRef.current);
        const result = await verifyAndCreateVisitRequestV2(payload, otpCode, sessionToken);
        setSessionToken(null);
        submissionIdRef.current = null;
        resetOtpChallengeState();
        clearVisitRequestV2Draft(draftNamespace);
        onSuccess(result, submittedValues);
      } catch (error) {
        const code = getApiErrorCode(error);
        const meta = getOtpErrorMeta(error);
        if (meta.remainingAttempts !== null) setRemainingAttempts(meta.remainingAttempts);
        setRetryAfterSeconds(meta.retryAfterSeconds);
        setRetryAt(meta.retryAt);
        if (meta.humanVerificationRequired || code === OTP_HUMAN_VERIFICATION_REQUIRED) {
          setHumanVerificationRequired(true);
        }
        // A non-OTP business rejection (e.g. campus deactivated meanwhile) closes the modal
        // and surfaces on the form so the user can fix the data.
        if (axios.isAxiosError(error) && error.response?.status === 400 && getApiFieldErrors(error)) {
          setSessionToken(null);
          resetOtpChallengeState();
          applyServerFieldErrors(error);
          setSubmitError(getApiErrorMessage(error, t('toast:visitRequest.submitFailed')));
        } else {
          setOtpError(getApiErrorMessage(error, t('toast:common.defaultError')));
        }
      } finally {
        setIsVerifying(false);
      }
    },
    [sessionToken, form, draftNamespace, onSuccess, resetOtpChallengeState, applyServerFieldErrors, t],
  );

  const resendOtp = useCallback(async () => {
    if (!sessionToken || !submissionIdRef.current) return;
    setIsResending(true);
    setOtpError(null);
    try {
      const data = form.getValues();
      const res = await visitRequestApi.resendOtp(
        data.registerInfo.email, data.registerInfo.fullName, submissionIdRef.current, sessionToken);
      setSessionToken(res.sessionToken);
      resetOtpChallengeState();
      setRemainingAttempts(res.maxAttempts ?? null);
      setResendAfterSeconds(res.resendAfterSeconds ?? 60);
    } catch (error) {
      const meta = getOtpErrorMeta(error);
      if (meta.humanVerificationRequired || getApiErrorCode(error) === OTP_HUMAN_VERIFICATION_REQUIRED) {
        setHumanVerificationRequired(true);
      }
      setOtpError(getApiErrorMessage(error, t('toast:visitRequest.otpResendFailed')));
    } finally {
      setIsResending(false);
    }
  }, [sessionToken, form, resetOtpChallengeState, t]);

  const recoverOtp = useCallback(
    async (humanVerificationToken: string) => {
      if (!sessionToken || !submissionIdRef.current || isRecoveringOtp) return;
      setIsRecoveringOtp(true);
      setOtpError(null);
      try {
        const data = form.getValues();
        const res = await visitRequestApi.recoverOtp(
          submissionIdRef.current, sessionToken, humanVerificationToken, data.registerInfo.fullName);
        setSessionToken(res.sessionToken);
        resetOtpChallengeState();
        setRemainingAttempts(res.maxAttempts ?? null);
        setResendAfterSeconds(res.resendAfterSeconds ?? 60);
      } catch (error) {
        setOtpError(getApiErrorMessage(error, t('toast:common.defaultError')));
      } finally {
        setIsRecoveringOtp(false);
      }
    },
    [sessionToken, isRecoveringOtp, form, resetOtpChallengeState, t],
  );

  const cancelOtp = useCallback(() => {
    setSessionToken(null);
    submissionIdRef.current = null;
    resetOtpChallengeState();
  }, [resetOtpChallengeState]);

  const resetForm = useCallback(() => {
    autoSaveBlockedRef.current = true;
    debouncedSaveRef.current?.cancel();
    form.reset(DEFAULT_VISIT_REQUEST_V2_VALUES());
    form.clearErrors();
    setSessionToken(null);
    setMaskedEmail('');
    setSubmitError(null);
    setFirstErrorCampusIndex(null);
    setApplyToAllPrompt(null);
    submissionIdRef.current = null;
    resetOtpChallengeState();
    clearVisitRequestV2Draft(draftNamespace);
    autoSaveBlockedRef.current = false;
  }, [form, draftNamespace, resetOtpChallengeState]);

  return {
    form,
    campusVisitFields,
    // Card operations
    addCampusVisit,
    removeCampusVisit,
    copyContentIntoCampus,
    campusVisitHasUserContent,
    requestApplyToAll,
    confirmApplyToAll,
    cancelApplyToAll,
    applyToAllPrompt,
    // Submit
    onSubmit,
    isSubmitting,
    submitError,
    setSubmitError,
    firstErrorCampusIndex,
    setFirstErrorCampusIndex,
    // OTP phase (public mode)
    sessionToken,
    maskedEmail,
    otpError,
    isVerifying,
    isResending,
    remainingAttempts,
    retryAfterSeconds,
    retryAt,
    resendAfterSeconds,
    humanVerificationRequired,
    isRecoveringOtp,
    verifyOtp,
    resendOtp,
    recoverOtp,
    cancelOtp,
    // Draft
    draftHydrated,
    hydrateDraft,
    detectDraft,
    draftAvailableAt,
    restoreDraft,
    discardDraft,
    saveDraftNow,
    migratedFromGlobalDraft,
    resetForm,
  };
};
