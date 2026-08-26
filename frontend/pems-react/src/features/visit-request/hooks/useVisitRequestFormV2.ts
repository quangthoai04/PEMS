import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useFieldArray, useForm, type FieldPath, type UseFormReturn } from 'react-hook-form';
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
  restoreCampusVisitFromDraft,
} from '../utils/visitRequestV2Form';
import {
  clearOtpChallengeToken,
  clearVisitRequestV2Draft,
  hasMeaningfulV2Data,
  loadVisitRequestV2DraftWithMigration,
  readOtpChallengeToken,
  saveOtpChallengeToken,
  saveVisitRequestV2Draft,
  type SaveV2DraftResult,
  type VisitRequestV2OtpContext,
} from '../utils/visitRequestV2DraftStorage';
import { countFieldErrors } from '../utils/formErrorNavigation';
import { useContactLinkPrompt } from './useContactLinkPrompt';
import { visitRequestApi, type CampusHostSelectionChoice } from '../api/visitRequestApi';
import {
  createVisitRequestV2,
  initiateVisitRequestV2,
  verifyAndCreateVisitRequestV2,
  getVisitSubmissionResult,
  type V2CreateResponse,
  type VisitSubmissionLookup,
} from '../api/visitRequestV2Api';
import { getApiErrorMessage, showInfoToast, showSuccessToast, translateErrorCode } from '../../../shared/utils/toast';
import { isSameEmailIdentity } from '../../../shared/utils/emailIdentity';
import { parseApiDate } from '../../../shared/utils/vietnamTime';

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

/**
 * The four per-email quota codes, plus the per-attempt cooldown. All five arrive as 429 carrying
 * `retryAfterSeconds` and/or `retryAt`.
 */
const OTP_RATE_LIMIT_CODES = new Set([
  'OTP_RESEND_TOO_SOON',
  'OTP_STANDARD_RATE_LIMITED',
  'OTP_RECOVERY_RATE_LIMITED',
  'OTP_ABSOLUTE_RATE_LIMITED',
  'OTP_RESEND_RATE_LIMITED',
  'OTP_RETRY_LATER',
]);

/**
 * The sentence shown for a rate-limited OTP request, built from the code and the retry metadata
 * rather than taken from `response.data.message`.
 *
 * <p>Four different quota rules used to arrive as one English sentence — "Temporarily unable to issue
 * another verification code." — which `getApiErrorMessage` then displayed verbatim on a Vietnamese
 * screen. The backend now sends Vietnamese, but the message still cannot be the source of truth here:
 * the user picks the UI language, the server does not know it, and only the client knows how much of
 * the wait has already elapsed while this screen has been open.</p>
 *
 * <p>Returns null when the error is not a rate limit, so the caller falls through to its normal
 * handling instead of this function having to guess at unrelated failures.</p>
 */
function otpRateLimitMessage(
  code: string | null,
  meta: OtpErrorMeta,
  t: (key: string, options?: Record<string, unknown>) => string,
): string | null {
  if (!code || !OTP_RATE_LIMIT_CODES.has(code)) return null;

  const seconds = meta.retryAfterSeconds;
  const at = meta.retryAt ? parseApiDate(meta.retryAt) : null;
  // "lúc 15:04" for an hourly quota, "sau 42 giây" for a short cooldown. A quota that resets in
  // 3.412 seconds is not information anybody can use.
  const when = at
    ? t('visitRequestV2:otpFlow.rateLimit.atTime', {
        time: at.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }),
      })
    : seconds && seconds > 0
      ? t('visitRequestV2:otpFlow.rateLimit.inSeconds', { count: seconds })
      : t('visitRequestV2:otpFlow.rateLimit.shortly');

  switch (code) {
    case 'OTP_RESEND_TOO_SOON':
    case 'OTP_RETRY_LATER':
      return t('visitRequestV2:otpFlow.rateLimit.tooSoon', { when });
    case 'OTP_RECOVERY_RATE_LIMITED':
      return t('visitRequestV2:otpFlow.rateLimit.recovery', { when });
    case 'OTP_ABSOLUTE_RATE_LIMITED':
      return t('visitRequestV2:otpFlow.rateLimit.absolute', { when });
    default:
      return t('visitRequestV2:otpFlow.rateLimit.standard', { when });
  }
}
/**
 * The backend's answer when a submissionId already belongs to a request with different content.
 * The only way to reach it is a stored intent that outlived its request (a create that committed
 * while the browser failed to clear the draft), and the user cannot fix that by retrying — so the
 * stale intent is dropped and the next attempt starts a fresh one.
 */
const IDEMPOTENCY_KEY_REUSED = 'IDEMPOTENCY_KEY_REUSED';

/**
 * Backend codes that identify ONE input by themselves, for the refusals that arrive as a plain
 * code + message with no field dictionary.
 *
 * Deliberately short. Anything that can be about more than one field — every per-campus rule, where
 * only the server knows which campus named the offending value — must send its own
 * `CampusVisits[i]...` paths instead; guessing those here would put a red message on the wrong
 * campus card, which is worse than the banner this list exists to replace.
 */
const SINGLE_FIELD_ERROR_PATHS: Record<string, FieldPath<VisitRequestV2Schema>> = {
  // The public form may only be filed by an external guest/partner, and both of these are decided by
  // looking up the REGISTRANT address alone (see UserProvisionService.ValidateRegistrantEmail…).
  REGISTRANT_EMAIL_BELONGS_TO_INTERNAL_ACCOUNT: 'registerInfo.email',
  VISITOR_ACCOUNT_INACTIVE: 'registerInfo.email',
};

/**
 * A member's `organizationPartnerId` names a profile a registration form may not cite (PART-09) —
 * anything that is not ACTIVE + APPROVED + PUBLIC.
 *
 * <p>The dropdown stopped offering those, so the live way to hit this is a DRAFT or an edit whose id
 * was picked while the rules were wider, or a request made outside the UI entirely.</p>
 */
const PARTNER_NOT_SELECTABLE = 'PARTNER_NOT_SELECTABLE';

/**
 * Puts the server's refusal on every member row that carries a partner id, and reports where the
 * first one is so the card can be opened.
 *
 * <p>The organisation TEXT is left exactly as it is: it is the name the delegation gave, it is what
 * the request will display, and losing it because the profile behind it went out of state would cost
 * the user data they never got wrong. Only the choice needs re-making.</p>
 */
const markUnselectablePartnerFields = (
  form: UseFormReturn<VisitRequestV2Schema>,
  message: string,
): { count: number; firstCampusIndex: number | null } => {
  const campuses = form.getValues('campusVisits') ?? [];
  let count = 0;
  let firstCampusIndex: number | null = null;
  campuses.forEach((cv, campusIndex) => {
    (['visitors', 'supportTeam'] as const).forEach(kind => {
      (cv?.[kind] ?? []).forEach((member, rowIndex) => {
        if (member?.organizationPartnerId == null) return;
        form.setError(
          `campusVisits.${campusIndex}.${kind}.${rowIndex}.organization` as FieldPath<VisitRequestV2Schema>,
          { type: 'server', message },
        );
        count += 1;
        if (firstCampusIndex === null) firstCampusIndex = campusIndex;
      });
    });
  });
  return { count, firstCampusIndex };
};

/**
 * Where a submit intent has got to (plan §3). Exactly one of these is true at any moment.
 *
 * CREATE_UNCERTAIN is the one that did not exist before and matters most: when the verify call
 * fails without an answer from the server — a timeout, a dropped connection, a gateway error — the
 * request may well have been committed. Reporting that as a plain failure is what invites the user
 * to send the whole thing again.
 */
export type SubmissionStage =
  | 'EDITING'
  | 'SENDING_OTP'
  | 'OTP_PENDING'
  | 'VERIFYING_OTP'
  | 'CREATE_CONFIRMED'
  | 'CREATE_UNCERTAIN'
  | 'CREATE_FAILED';

/**
 * True when a failed request never got a verdict from the server, so the create may or may not have
 * happened. An HTTP status means the server answered and decided; no status at all — or a 502/503/504
 * from something in front of it — means nobody knows yet.
 */
const isUndecided = (error: unknown): boolean => {
  if (!axios.isAxiosError(error)) return false;
  const status = error.response?.status;
  if (status === undefined) return true;              // timeout, abort, DNS, connection reset
  return status === 502 || status === 503 || status === 504;
};

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
  getCampusHostSelections?: () => CampusHostSelectionChoice[];
  /**
   * Authenticated mode only: the signed-in user's own email. Authenticated create is
   * self-registration ONLY (plan CanhIter3FixBug — registrant is locked to the profile of the
   * signed-in account, with no editable/delegated path any more), so this is used to ASSERT that
   * invariant right before submit rather than to route between two contracts. Leave undefined in
   * public mode.
   */
  currentUserEmail?: string | null;
  minAdvanceHours?: number;
  /**
   * Authenticated mode only: is the signed-in actor internal Staff/Staff Leader? This alone is what
   * may file a visit under the 72h registration floor — see the
   * PEMS_INTERNAL_SELF_CREATE_SHORT_NOTICE_72H plan. Authenticated create is self-registration only
   * (the registrant is always the signed-in account), so the floor no longer needs to watch anything
   * on the form itself. The backend re-derives and re-checks the same thing from the actor's own DB
   * role; this only decides what the FORM offers/validates, never what the server accepts. Ignored in
   * public mode.
   */
  isInternalActor?: boolean;
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

// Re-exported from its own module now that the EDIT screen asks the same question. Kept here so the
// components already importing it from this hook keep compiling.
export type { ContactLinkPrompt } from './useContactLinkPrompt';

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
  const isInternalActor = options?.isInternalActor ?? false;
  /** The floor for everyone WITHOUT the short-notice capability — Visitor, and every public submit. */
  const staticMinAdvanceHours = options?.minAdvanceHours ?? V2_MIN_ADVANCE_HOURS_CREATE;
  /**
   * The floor actually enforced right now (plan CanhIter3FixBug — 72h fix). Authenticated create is
   * self-registration ONLY, so "internal Staff/Staff Leader registering themself" is no longer a form
   * STATE to derive from a watched email — it is exactly `isAuthenticatedMode && isInternalActor`, a
   * plain synchronous value computed on every render.
   *
   * This used to be `useState` + a `form.watch('registerInfo.email')` effect that flipped it to 0 once
   * the registrant email matched the signed-in account. That broke the moment the email was set via a
   * WHOLE-OBJECT `form.setValue('registerInfo', {...})` (the profile autofill): react-hook-form reports
   * the changed `name` as `'registerInfo'`, not `'registerInfo.email'`, so the watcher's `name ===
   * 'registerInfo.email'` guard never matched and the floor stayed stuck at 72 — until a `form.reset`
   * (draft hydrate, full reload) fired the watcher's `name === undefined` branch and it self-corrected,
   * which is exactly why Ctrl+Shift+R "fixed" it. Not deriving the floor from the form at all removes
   * the whole class of bug: nothing to desync, and the registrant email can no longer diverge from the
   * signed-in account anyway (registerInfo is profile-locked/read-only in authenticated mode).
   */
  const minAdvanceHours = isAuthenticatedMode && isInternalActor ? 0 : staticMinAdvanceHours;

  const { t, i18n } = useTranslation(['validation', 'toast', 'visitRequestV2']);

  /**
   * ONE stage at a time (plan §3). Previously "where is this submission?" had to be reconstructed
   * from four independent booleans plus a session token, and they could disagree: mid-verify the
   * form was both `isVerifying` and still `isSubmitting`, and a dropped connection left every flag
   * back at its resting value with nothing recording that a create might already have happened.
   * `isSubmitting` / `isVerifying` are now DERIVED from this, so no caller can observe a
   * combination the machine does not allow.
   */
  const [stage, setStage] = useState<SubmissionStage>('EDITING');
  // Read inside async callbacks that must not close over a stale render (the double-submit guard).
  const stageRef = useRef<SubmissionStage>('EDITING');
  useEffect(() => { stageRef.current = stage; }, [stage]);

  /** The submit intent whose outcome is unknown, plus the values that produced it. */
  const [uncertain, setUncertain] =
    useState<{ submissionId: string | null; values: VisitRequestV2Schema } | null>(null);
  const [isCheckingResult, setIsCheckingResult] = useState(false);
  const [lastLookup, setLastLookup] = useState<VisitSubmissionLookup | null>(null);
  const [uncertainError, setUncertainError] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  /** Index of the campus card holding the first server/client error — the UI expands + focuses it. */
  const [firstErrorCampusIndex, setFirstErrorCampusIndex] = useState<number | null>(null);
  /**
   * Bumped every time a submit comes back invalid. The form watches it to move the caret to the
   * first bad field — a plain boolean could not, because two failed submits in a row would not
   * change it and the second one would leave the user staring at an unchanged screen.
   */
  const [focusErrorsToken, setFocusErrorsToken] = useState(0);

  // OTP phase state (public mode; server-driven presentation values, same as v1)
  const [sessionToken, setSessionToken] = useState<string | null>(null);
  const [maskedEmail, setMaskedEmail] = useState('');
  const [otpError, setOtpError] = useState<string | null>(null);
  const [isResending, setIsResending] = useState(false);
  const [remainingAttempts, setRemainingAttempts] = useState<number | null>(null);
  const [retryAfterSeconds, setRetryAfterSeconds] = useState<number | null>(null);
  const [retryAt, setRetryAt] = useState<string | null>(null);
  const [resendAfterSeconds, setResendAfterSeconds] = useState(60);
  const [humanVerificationRequired, setHumanVerificationRequired] = useState(false);
  const [isRecoveringOtp, setIsRecoveringOtp] = useState(false);

  const [applyToAllPrompt, setApplyToAllPrompt] = useState<ApplyToAllPrompt | null>(null);

  /**
   * Keyed by clientKey, bumped whenever a copy/apply-to-all overwrites that card's content.
   * `useFieldArray.update()`/`.replace()` patch the underlying form values correctly, but they
   * skip resyncing `register()`-bound inputs and any NESTED `useFieldArray` (visitors/supportTeam
   * live inside `CampusVisitCard`, registered under their own name) — those keep showing their
   * pre-copy state until something else touches them. Folding this into the card's React `key`
   * forces a full remount, which is the only way those nested hooks re-read the fresh values.
   */
  const [cardVersion, setCardVersion] = useState<Record<string, number>>({});
  const bumpCardVersion = useCallback((clientKey: string) => {
    setCardVersion(prev => ({ ...prev, [clientKey]: (prev[clientKey] ?? 0) + 1 }));
  }, []);

  const currentUserEmail = options?.currentUserEmail;
  /**
   * "Is the person named as registrant the signed-in user?" — the single question that decides the
   * submit contract, so it is derived in ONE place and exposed for the UI to render from.
   * Always false in public mode: there is no session to be the same as.
   */
  const isSelfRegistration = useCallback(
    (registrantEmail: string | null | undefined): boolean =>
      isAuthenticatedMode && isSameEmailIdentity(currentUserEmail, registrantEmail),
    [isAuthenticatedMode, currentUserEmail],
  );

  const [draftHydrated, setDraftHydrated] = useState(false);
  const [migratedFromGlobalDraft, setMigratedFromGlobalDraft] = useState(false);
  /** When set, a saved draft is waiting for the user to restore or discard it. */
  const [draftAvailableAt, setDraftAvailableAt] = useState<number | null>(null);

  const submissionIdRef = useRef<string | null>(null);
  const autoSaveBlockedRef = useRef(false);
  const debouncedSaveRef = useRef<ReturnType<typeof debounce> | null>(null);
  /**
   * Does storage hold a draft for THIS namespace right now? A third state, kept apart from "the form
   * differs from its baseline" (`isDirty`) and "there is something worth writing" (`hasMeaningfulV2Data`):
   * a form can be empty and still have a stored draft behind it that the user is in the middle of
   * emptying, and that is the one case where an empty form still deserves to be asked about.
   */
  const hasPersistedDraftRef = useRef(false);
  /**
   * The OTP challenge that was already requested for this draft, if any. It survives a wrong code,
   * a closed modal and a reload — the whole point being that none of those are a reason to make
   * somebody type the form again.
   */
  const [pendingOtp, setPendingOtp] = useState<VisitRequestV2OtpContext | null>(null);

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
    mode: 'onSubmit',
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

  /**
   * How many fields are invalid RIGHT NOW — DERIVED from the live error tree on every render, never
   * a number captured at the moment a submit failed.
   *
   * The banner used to be a finished sentence frozen into `submitError` by the invalid-submit
   * handler, so it went on saying "11 fields left" while the user emptied that list one field at a
   * time, and only another submit could correct it. React Hook Form re-runs the resolver for a field
   * as it changes once the form has been submitted (`reValidateMode: 'onChange'`) and drops the
   * error when it passes — including errors the SERVER put there — so reading the count off
   * `formState.errors` makes it fall as fields are fixed and reach zero, which is what makes the
   * banner go away without a second submit.
   */
  const validationErrorCount = countFieldErrors(form.formState.errors);
  /**
   * …but only after a submit has been attempted. A form nobody has submitted owes the user no list
   * of everything it is still missing.
   *
   * <p>This gate is now belt AND braces: the profile autofill used to validate unconditionally and
   * put errors on the tree before the user had pressed anything, and it now follows `isSubmitted`
   * itself (NP-02). Keeping the check here still matters for any other pre-submit error source —
   * a mapped server error, a manual `setError`.</p>
   */
  const showValidationSummary = form.formState.isSubmitted && validationErrorCount > 0;

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
          // `restoreCampusVisitFromDraft` gives every restored member row its stable identity back,
          // translates a draft written in the array-index era into a real pick, and — reading the
          // RAW draft object before any default is merged in — infers `operationalContactSource` for
          // a draft saved before that field existed (plan CanhIter3FixBug §12-§15). A draft saved
          // before any of this existed would otherwise resume with rows the contact picker cannot
          // name (NP-03).
          ? draft.data.campusVisits.map(cv => restoreCampusVisitFromDraft(cv))
          : defaults.campusVisits,
    });
    // Restoring the TYPING without restoring the submission intent is what turns a resumed draft
    // into a second request: the backend keys idempotency on the submissionId.
    submissionIdRef.current = draft.submissionId ?? null;
    setPendingOtp(draft.otp ?? null);
    setMigratedFromGlobalDraft(migrated);
    setDraftHydrated(true);
    hasPersistedDraftRef.current = true;
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
      // Nothing is being offered, so nothing needs protecting: autosave is explicitly RELEASED
      // here. It is blocked whenever this form is (re)pointed at a namespace, and leaving it that
      // way on the "no draft" answer would silently disable saving for the whole session.
      autoSaveBlockedRef.current = false;
      hasPersistedDraftRef.current = false;
      setDraftHydrated(true);
      return false;
    }
    autoSaveBlockedRef.current = true;
    hasPersistedDraftRef.current = true;
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
    // Deleting the draft deletes the whole submission intent with it: the id, the pending challenge
    // and its token. Leaving any of them behind would attach the next form to an abandoned attempt.
    // The armed autosave goes first: a keystroke 700ms before "discard" would otherwise land AFTER
    // the delete and put the draft straight back, which is indistinguishable from discard not working.
    debouncedSaveRef.current?.cancel();
    clearVisitRequestV2Draft(draftNamespace);
    submissionIdRef.current = null;
    setPendingOtp(null);
    setSessionToken(null);
    setMigratedFromGlobalDraft(false);
    setDraftAvailableAt(null);
    autoSaveBlockedRef.current = false;
    hasPersistedDraftRef.current = false;
    setDraftHydrated(true);
  }, [draftNamespace]);

  /**
   * "Close without saving" — one meaning, whatever the draft's history: the stored draft for this
   * namespace is DELETED, along with the edits on screen. Where it came from makes no difference; a
   * draft restored ten seconds ago and one autosaved while typing both go.
   *
   * There is deliberately no snapshot of the pre-edit draft to fall back to. Restoring a version the
   * user can no longer see would be a third outcome nobody asked for, and it cannot be honest anyway:
   * autosave has usually already overwritten the stored copy with the edits by the time this is
   * clicked, so "the draft as it was" mostly does not exist. Keeping the work is what "Lưu nháp và
   * đóng" is for.
   *
   * Autosave is stopped for good on the way out — the armed keystroke is dropped and further writes
   * are blocked, or the debounce would land moments later and recreate exactly what was deleted.
   */
  const abandonEdits = useCallback(() => {
    discardDraft();
    autoSaveBlockedRef.current = true;
  }, [discardDraft]);

  /**
   * Force-saves immediately, bypassing the debounce. Used for "save draft and exit" AND before every
   * step of the OTP round trip: the 700ms autosave has not necessarily fired when somebody fills the
   * last field and submits, and that is exactly the draft worth keeping.
   *
   * The values come straight from `form.getValues()`, so whether the debounce happened to have fired
   * never changes what gets written. The RESULT is returned rather than swallowed: a caller that
   * tells the user "saved" has to be able to find out whether it actually was.
   */
  const saveDraftNow = useCallback(
    (options?: { submissionId?: string | null; otp?: VisitRequestV2OtpContext | null }): SaveV2DraftResult => {
      const outcome = saveVisitRequestV2Draft(form.getValues(), undefined, draftNamespace, options);
      if (outcome.success) hasPersistedDraftRef.current = true;
      return outcome;
    },
    [form, draftNamespace],
  );

  /**
   * The draft of a request that HAS been created. Autosave is stopped for good rather than merely
   * having its file deleted: a keystroke moments before the create returns leaves a debounced save
   * armed, and letting it fire would recreate the draft of a request that already exists — which the
   * next visit would then offer to "restore", carrying a submissionId the backend has already spent.
   */
  const clearDraftAfterCreate = useCallback(() => {
    autoSaveBlockedRef.current = true;
    debouncedSaveRef.current?.cancel();
    clearVisitRequestV2Draft(draftNamespace);
    hasPersistedDraftRef.current = false;
  }, [draftNamespace]);

  useEffect(() => {
    if (!draftHydrated) return;
    debouncedSaveRef.current = debounce((value: Partial<VisitRequestV2Schema>) => {
      if (autoSaveBlockedRef.current) return;
      // An autosave that lands is a draft on disk like any other — the close prompt has to know,
      // or emptying a form whose draft was written while typing would close without a word.
      if (saveVisitRequestV2Draft(value, undefined, draftNamespace).success) {
        hasPersistedDraftRef.current = true;
      }
    }, 700);
    const subscription = form.watch(value => {
      debouncedSaveRef.current?.(value as Partial<VisitRequestV2Schema>);
    });
    return () => {
      subscription.unsubscribe();
      debouncedSaveRef.current?.cancel();
    };
  }, [form, draftHydrated, draftNamespace]);

  /**
   * The namespace names WHOSE draft this is, and it is not always known at first render: in
   * authenticated mode it is derived from the signed-in user, who arrives asynchronously. Every time
   * it changes — `undefined` → `u15` on load, or `u15` → `u16` if the account changes underneath a
   * mounted form — this is a different person's draft, so the form goes back to "not decided yet":
   * autosave is held, the armed keystroke is dropped, and the submission intent (which belonged to
   * the previous namespace) is forgotten. `detectDraft` then answers for the NEW namespace and
   * either offers its draft or releases autosave.
   */
  const activeNamespaceRef = useRef(draftNamespace);
  useEffect(() => {
    if (activeNamespaceRef.current === draftNamespace) return;
    activeNamespaceRef.current = draftNamespace;
    debouncedSaveRef.current?.cancel();
    autoSaveBlockedRef.current = true;
    // Whether the NEW namespace has a draft is unknown until `detectDraft` answers, and the previous
    // account's answer must never stand in for it.
    hasPersistedDraftRef.current = false;
    submissionIdRef.current = null;
    setPendingOtp(null);
    setDraftAvailableAt(null);
    setDraftHydrated(false);
  }, [draftNamespace]);

  // ── Campus card operations ──
  const addCampusVisit = useCallback(
    (copyFromIndex?: number) => {
      const current = form.getValues('campusVisits');
      if (current.length >= Math.min(maxCampuses, V2_MAX_CAMPUSES)) return false;
      const fresh = createEmptyCampusVisit();
      const source = copyFromIndex !== undefined ? current[copyFromIndex] : undefined;
      campusVisitFields.append(source ? cloneCampusVisitContent(source, fresh) : fresh);
      showSuccessToast(t('visitRequestV2:card.addCampusSuccess'));
      return true;
    },
    [campusVisitFields, form, maxCampuses, t],
  );

  const removeCampusVisit = useCallback(
    (index: number) => {
      const current = form.getValues('campusVisits');
      if (current.length <= 1) return false;
      campusVisitFields.remove(index);
      // A card that is gone cannot still be "a field that needs attention". Removing it takes its
      // values away, but the ERROR tree keeps an entry at the index that no longer exists — so the
      // summary went on counting a card nobody could see or fix, and the form could not be cleared
      // of errors by any action the user was able to take. Re-running the resolver for the array is
      // the same validation everything else here uses; it just has to be asked for, because a
      // structural change is not a field change and nothing triggers it by itself.
      if (form.formState.isSubmitted) void form.trigger('campusVisits');
      showSuccessToast(t('visitRequestV2:card.removeCampusSuccess'));
      return true;
    },
    [campusVisitFields, form, t],
  );

  /** One-time copy INTO an existing card (its campus + schedule are preserved). */
  const copyContentIntoCampus = useCallback(
    (targetIndex: number, sourceIndex: number) => {
      const current = form.getValues('campusVisits');
      const source = current[sourceIndex];
      const target = current[targetIndex];
      if (!source || !target || sourceIndex === targetIndex) return;
      campusVisitFields.update(targetIndex, cloneCampusVisitContent(source, target));
      bumpCardVersion(target.clientKey);
      // Same reasoning as `removeCampusVisit`: `update()` patches the values correctly, but a
      // programmatic write is not a field `onChange`, so nothing reruns the resolver for this card on
      // its own — the OLD error tree (computed against the PRE-copy values) would otherwise keep
      // showing "required" on fields the copy just filled in. Re-validating only this card's scope
      // leaves every other campus's error state untouched.
      if (form.formState.isSubmitted) void form.trigger(`campusVisits.${targetIndex}`);
      showSuccessToast(t('visitRequestV2:card.copySuccess'));
    },
    [form, campusVisitFields, t, bumpCardVersion],
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
    current.forEach((cv, i) => {
      if (i !== applyToAllPrompt.sourceIndex) bumpCardVersion(cv.clientKey);
    });
    // Same reasoning as `copyContentIntoCampus`/`removeCampusVisit`: `replace()` is a programmatic
    // write, not a field `onChange`, so every target card's error tree would otherwise keep showing
    // its PRE-apply errors even though the values just became valid. Every campus but the source was
    // just overwritten, so the whole array is re-validated in one call.
    if (form.formState.isSubmitted) void form.trigger('campusVisits');
    setApplyToAllPrompt(null);
    showSuccessToast(t('visitRequestV2:card.applyAllSuccess'));
  }, [applyToAllPrompt, form, campusVisitFields, t, bumpCardVersion]);

  const cancelApplyToAll = useCallback(() => setApplyToAllPrompt(null), []);

  // ── "Đầu mối này có phải là người trong đoàn?" (ID-01) ──────────────────────────────────────
  //
  // The question itself lives in useContactLinkPrompt, because the EDIT screen has to ask it too and
  // for a while only this one did. Resumed through a ref: the answer re-enters the submit defined
  // below, which cannot be referenced before it exists.
  const resumeSubmitRef = useRef<() => void>(() => {});
  const contactLink = useContactLinkPrompt(form, () => resumeSubmitRef.current());

  // ── Server error mapping: land field errors on the exact campus card ──
  const applyServerFieldErrors = useCallback(
    (error: unknown): boolean => {
      const fieldErrors = getApiFieldErrors(error);
      // A refusal that names exactly ONE field on its own. The per-campus errors below carry their
      // own paths (they have to — only the server knows WHICH campus named a rejected address), but
      // a refusal about the registrant's own mailbox can only ever be about one input, and it used
      // to arrive as a bare code with no `errors` dictionary at all. That left the form with nowhere
      // to put it, so it fell through to the banner at the bottom of a long page — the user was told
      // an email was unusable with nothing pointing at which of the form's several email boxes.
      if (!fieldErrors) {
        // A partner id the form may no longer cite (PART-09). The server refuses the SET without
        // naming which id offended — telling them apart would turn the form into a probe for which
        // partner ids exist and what state they are in — but the client knows exactly which rows
        // carry an id, and those are the only rows that can be at fault. Marking them is what makes
        // this fixable: the alternative is a banner about "an organisation" on a form with thirty of
        // them. Nothing is cleared or re-pointed — which organisation a member works for is the
        // user's answer to change, not ours.
        if (getApiErrorCode(error) === PARTNER_NOT_SELECTABLE) {
          const mapped = markUnselectablePartnerFields(
            form, translateErrorCode(getApiErrorCode(error)) ?? getApiErrorMessage(error));
          if (mapped.count > 0) {
            if (mapped.firstCampusIndex !== null) setFirstErrorCampusIndex(mapped.firstCampusIndex);
            setFocusErrorsToken(n => n + 1);
            return true;
          }
          return false;
        }
        const path = SINGLE_FIELD_ERROR_PATHS[getApiErrorCode(error) ?? ''];
        if (!path) return false;
        form.setError(path, {
          type: 'server',
          message: translateErrorCode(getApiErrorCode(error)) ?? getApiErrorMessage(error),
        });
        setFocusErrorsToken(n => n + 1);
        return true;
      }
      // The raw string in `messages[0]` is whatever language the backend happened to write it in
      // (usually Vietnamese) — safe on a Vietnamese-only screen, but wrong the moment the UI is
      // switched to English. When the response also carries a stable `errorCode` with a matching
      // `errors:api.<CODE>` entry, that translated text is shown instead (same lookup the generic
      // error banner already uses via `getApiErrorMessage`/`translateErrorCode`).
      const translated = translateErrorCode(getApiErrorCode(error));
      let firstCampusIndex: number | null = null;
      let mappedAny = false;
      for (const [serverPath, messages] of Object.entries(fieldErrors)) {
        const formPath = mapServerFieldPathToFormPath(serverPath);
        if (!formPath || !messages?.length) continue;
        form.setError(formPath as FieldPath<VisitRequestV2Schema>, { type: 'server', message: translated ?? messages[0] });
        mappedAny = true;
        const campusMatch = /^campusVisits\.(\d+)\./.exec(formPath);
        if (campusMatch) {
          const idx = Number(campusMatch[1]);
          if (firstCampusIndex === null || idx < firstCampusIndex) firstCampusIndex = idx;
        }
      }
      if (firstCampusIndex !== null) setFirstErrorCampusIndex(firstCampusIndex);
      // A rejection from the SERVER lands on a field just like a client-side one, so it gets the
      // same treatment: the user should not have to find it themselves.
      if (mappedAny) setFocusErrorsToken(n => n + 1);
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
      setStage('SENDING_OTP');
      setSubmitError(null);
      setFirstErrorCampusIndex(null);
      try {
        const submissionId = submissionIdRef.current ?? crypto.randomUUID();
        submissionIdRef.current = submissionId;
        // Persist the intent BEFORE the request leaves: a retry after a network failure must reuse
        // this id, or the backend sees two unrelated submissions and creates two requests.
        saveDraftNow({ submissionId });
        const submittedValues = cloneValues(data);
        const payload = buildV2CreatePayload(
          submittedValues, submissionId, options?.getCampusHostSelections?.() ?? []);
        const result = await createVisitRequestV2(payload);
        submissionIdRef.current = null;
        setPendingOtp(null);
        clearDraftAfterCreate();
        setStage('CREATE_CONFIRMED');
        onSuccess(result, submittedValues);
      } catch (error) {
        // No verdict from the server: the create may already have committed, so this must NOT be
        // reported as a failure the user should answer by submitting again.
        if (isUndecided(error)) {
          setUncertain({ submissionId: submissionIdRef.current, values: cloneValues(data) });
          setStage('CREATE_UNCERTAIN');
          saveDraftNow({ submissionId: submissionIdRef.current });
          return;
        }
        // The id is KEPT: the same submit intent is what makes the retry idempotent. The one
        // exception is an intent the server says is already spoken for.
        if (getApiErrorCode(error) === IDEMPOTENCY_KEY_REUSED) submissionIdRef.current = null;
        saveDraftNow({ submissionId: submissionIdRef.current });
        const mapped = applyServerFieldErrors(error);
        // A refusal that landed ON a field says everything it needs to say there, and the summary
        // above the button counts it like any other. Repeating a second sentence in the banner —
        // "please fill in all required fields correctly" — was one more thing to read that named
        // neither the field nor the reason.
        setSubmitError(mapped ? null : getApiErrorMessage(error, t('toast:visitRequest.submitFailed')));
        setStage('CREATE_FAILED');
        if (!mapped) console.error('v2 authenticated create failed', getApiErrorCode(error));
      }
    },
    [applyServerFieldErrors, clearDraftAfterCreate, onSuccess, options, saveDraftNow, t],
  );

  const onSubmit = form.handleSubmit(async data => {
    // Asked BEFORE anything is sent, because the answer is what the payload MEANS. "Cùng một người"
    // adds the member key; "hai người khác nhau" deliberately leaves it out, and the backend reads
    // that absence as the answer — a payload that carries member keys but names no contact member is
    // one that asked this question and was told they are two humans, so it links nobody rather than
    // re-matching the fingerprint (OperationalContactLink.Match). Do NOT "fix" the decline branch by
    // making it write something: what it must send is nothing. The submit resumes from the resolver
    // below rather than continuing here — a modal cannot be awaited in the middle of a handler
    // without leaving the form locked if the user walks away.
    if (contactLink.interrupts(data)) return;
    // ONE submit at a time. The button is disabled and the fields are locked for as long as the
    // request is in flight, but a second click landing in the same tick — or an Enter arriving from
    // a control the lock has not reached — would otherwise start a second create: a duplicate
    // request, or a second OTP burnt against the same challenge. Read through the ref because this
    // callback is created once per render and would otherwise judge a stale stage.
    if (stageRef.current === 'SENDING_OTP' || stageRef.current === 'VERIFYING_OTP') return;
    // Authenticated create is SELF-registration only, always (plan CanhIter3FixBug — no delegated
    // authenticated path exists any more: the registrant block is profile-locked/read-only, so there
    // is no UI action that could ever produce a mismatch). This is checked anyway, once, right here —
    // an invariant assertion rather than a routing decision: a mismatch means the form's state has
    // drifted from the signed-in account (a stale draft that slipped past the profile overwrite, a
    // profile reload racing the submit), and the correct response is to block and let the profile
    // re-sync, NOT to fall through to the public OTP flow. The JWT can only ever vouch for the
    // signed-in mailbox, so silently offering "prove it with a code" here would be offering to verify
    // an identity the session itself says is wrong.
    if (isAuthenticatedMode) {
      if (!isSelfRegistration(data.registerInfo.email)) {
        setSubmitError(t('visitRequestV2:registrant.identityMismatch'));
        setStage('CREATE_FAILED');
        return;
      }
      await submitAuthenticated(data);
      return;
    }
    // Mint the OTP challenge through the v2 initiate endpoint. The FULL v2 form is
    // sent and its snapshot is bound server-side, so 30-minute / zero-support submissions
    // work and verify builds from exactly this form — no v1 projection, no silent fallback.
    setStage('SENDING_OTP');
    setSubmitError(null);
    setFirstErrorCampusIndex(null);
    try {
      // One submit INTENT, however many OTP attempts it takes. Re-initiating with the same id
      // refreshes the bound snapshot server-side (InitiateVisitRequestV2CommandHandler), so a new
      // id here would only ever mean "a second, unrelated request".
      const submissionId = submissionIdRef.current ?? crypto.randomUUID();
      submissionIdRef.current = submissionId;
      // Saved before the call, not after: if this throws — or the tab dies mid-flight — the typing
      // and the intent are already on disk.
      saveDraftNow({ submissionId });
      const res = await initiateVisitRequestV2(buildV2CreatePayload(data, submissionId));
      if (!res?.sessionToken) throw new Error(t('toast:visitRequest.otpTokenMissing'));
      resetOtpChallengeState();
      setRemainingAttempts(res.maxAttempts ?? null);
      setResendAfterSeconds(res.resendAfterSeconds ?? 60);
      setSessionToken(res.sessionToken);
      setMaskedEmail(res.maskedEmail);
      const otp: VisitRequestV2OtpContext = {
        targetEmail: data.registerInfo.email,
        maskedEmail: res.maskedEmail,
        expiresAt: res.expiresAt ?? null,
        resendAfterSeconds: res.resendAfterSeconds ?? null,
        savedAt: Date.now(),
      };
      setPendingOtp(otp);
      saveDraftNow({ submissionId, otp });
      saveOtpChallengeToken(submissionId, res.sessionToken, draftNamespace);
      setStage('OTP_PENDING');
      showSuccessToast(t('visitRequestV2:otpFlow.codeSent', { email: res.maskedEmail }), 'v2-otp-sent');
    } catch (error) {
      // Keep the id and the draft: the user has typed a whole form and the mail may simply have
      // failed to send. Clearing either would make them start over for a transport error.
      setSessionToken(null);
      if (getApiErrorCode(error) === IDEMPOTENCY_KEY_REUSED) submissionIdRef.current = null;
      saveDraftNow({ submissionId: submissionIdRef.current });
      const mapped = applyServerFieldErrors(error);
      // Landed on a field (a rejected contact address on one campus card, a registrant address the
      // public flow cannot use) → the field says it, and the summary counts it. Only a failure with
      // no field to blame — the mail transport, a gateway — belongs in the banner.
      setSubmitError(mapped ? null : getApiErrorMessage(error, t('toast:visitRequest.submitFailed')));
      // Initiate creates nothing, so a failure here is never "uncertain": there is no request to
      // wonder about, only a code that did not arrive.
      setStage('CREATE_FAILED');
    }
  }, errors => {
    // Expand + focus the first campus card with a client-side error.
    const campusErrors = (errors as { campusVisits?: unknown[] }).campusVisits;
    if (Array.isArray(campusErrors)) {
      const idx = campusErrors.findIndex(e => e != null);
      if (idx >= 0) setFirstErrorCampusIndex(idx);
    }
    // How much is left is said by `validationErrorCount`, which is recomputed from the live error
    // tree — NOT written into `submitError` here. A sentence stored at this moment would still be on
    // screen, with this moment's number, long after the user had fixed the fields it counted.
    // `submitError` is cleared instead: it belongs to the last SERVER answer, and this submit never
    // reached the server.
    setSubmitError(null);
    setFocusErrorsToken(n => n + 1);
    onInvalid?.(errors);
  });

  // The submit an answer returns to. Kept in a ref so the shared prompt hook, which is created
  // BEFORE this handler exists, still resumes the current one rather than a stale closure.
  useEffect(() => {
    resumeSubmitRef.current = () => { void onSubmit(); };
  });

  const verifyOtp = useCallback(
    async (otpCode: string) => {
      if (!sessionToken || !submissionIdRef.current) return;
      // Guards the double-click AND the "confirm again while the first is still in flight" case,
      // which would otherwise burn a second attempt against the same challenge.
      if (stageRef.current === 'VERIFYING_OTP') return;
      const submissionId = submissionIdRef.current;
      setStage('VERIFYING_OTP');
      setOtpError(null);
      try {
        const submittedValues = cloneValues(form.getValues());
        const payload = buildV2CreatePayload(submittedValues, submissionId);
        const result = await verifyAndCreateVisitRequestV2(payload, otpCode, sessionToken);
        // ONLY here — a confirmed create — is the draft allowed to disappear.
        setSessionToken(null);
        submissionIdRef.current = null;
        setPendingOtp(null);
        resetOtpChallengeState();
        clearDraftAfterCreate();
        setStage('CREATE_CONFIRMED');
        onSuccess(result, submittedValues);
        return;
      } catch (error) {
        // A wrong code, an expired challenge, a dropped connection: the form is untouched and stays
        // on disk with its submission intent. The user retries or asks for a new code.
        saveDraftNow();

        // No verdict from the server. The backend consumes the OTP in the SAME transaction as the
        // create, so "the connection died" genuinely can mean the request exists. Saying "failed"
        // here is what makes somebody fill the form in a second time.
        if (isUndecided(error)) {
          setUncertain({ submissionId, values: cloneValues(form.getValues()) });
          setStage('CREATE_UNCERTAIN');
          setSessionToken(null);
          return;
        }

        const code = getApiErrorCode(error);
        const meta = getOtpErrorMeta(error);
        if (meta.remainingAttempts !== null) setRemainingAttempts(meta.remainingAttempts);
        setRetryAfterSeconds(meta.retryAfterSeconds);
        setRetryAt(meta.retryAt);
        if (meta.humanVerificationRequired || code === OTP_HUMAN_VERIFICATION_REQUIRED) {
          setHumanVerificationRequired(true);
        }
        // A non-OTP business rejection (e.g. campus deactivated meanwhile) closes the modal
        // and surfaces on the form so the user can fix the data. The code was RIGHT — presenting
        // this as "wrong OTP" would send the user hunting through their inbox for nothing.
        //
        // Two shapes count as "a rejection about the data": field errors, and the handful of codes
        // that name one input on their own (an address the flow cannot accept). Without the second,
        // a refusal about the registrant's own mailbox — which no verification code can fix — was
        // shown inside the OTP modal, where it read as "your code was wrong".
        if (axios.isAxiosError(error)
          && ((error.response?.status === 400 && getApiFieldErrors(error))
            || (code !== null && code in SINGLE_FIELD_ERROR_PATHS))) {
          setSessionToken(null);
          resetOtpChallengeState();
          const mapped = applyServerFieldErrors(error);
          // The modal has just closed on the user, so something has to explain why — but only when
          // the reason could not be put on a field. When it could, that red line under the input is
          // the explanation, and the banner would repeat it word for word.
          setSubmitError(mapped ? null : getApiErrorMessage(error, t('toast:visitRequest.submitFailed')));
          setStage('CREATE_FAILED');
        } else {
          setOtpError(
            otpRateLimitMessage(code, meta, t)
            ?? getApiErrorMessage(error, t('toast:common.defaultError')),
          );
          // Still a live challenge with the modal open — not a terminal failure.
          setStage('OTP_PENDING');
        }
      }
    },
    [sessionToken, form, clearDraftAfterCreate, onSuccess, resetOtpChallengeState, applyServerFieldErrors, saveDraftNow, t],
  );

  /**
   * "Did it go through after all?" — the only correct answer to CREATE_UNCERTAIN (plan §10).
   *
   * Asks the server about the submit intent instead of re-sending it. COMPLETED promotes straight
   * to the success screen; PENDING means the OTP was never completed, so the challenge is still
   * the way forward; FAILED/NOT_FOUND hand the form back. Nothing here can create a request.
   */
  const checkSubmissionResult = useCallback(async (): Promise<VisitSubmissionLookup | null> => {
    const submissionId = uncertain?.submissionId;
    if (!submissionId || isCheckingResult) return null;
    setIsCheckingResult(true);
    setUncertainError(null);
    try {
      const lookup = await getVisitSubmissionResult(submissionId);
      if (lookup.state === 'COMPLETED' && lookup.visitRequestId) {
        const values = uncertain?.values ?? cloneValues(form.getValues());
        submissionIdRef.current = null;
        setPendingOtp(null);
        resetOtpChallengeState();
        clearDraftAfterCreate();
        setUncertain(null);
        setStage('CREATE_CONFIRMED');
        // The lookup is deliberately narrower than a create response — it answers "does this exist"
        // for an anonymous caller, so it carries no campus list and no contact state. The receipt is
        // rebuilt from what it DOES return; nothing is invented to fill the gaps.
        onSuccess({
          visitRequestId: lookup.visitRequestId,
          requestCode: lookup.requestCode ?? '',
          visitScope: (values.campusVisits?.length ?? 0) > 1 ? 'MULTI_CAMPUS' : 'SINGLE_CAMPUS',
          hasMixedCampusDetails: false,
          pendingContactConfirmations: 0,
          instances: [],
          idempotent: true,
          status: lookup.status ?? '',
          submittedAt: lookup.submittedAt ?? '',
          campusCount: lookup.campusCount ?? values.campusVisits?.length ?? 0,
          recoveredByLookup: true,
        }, values);
      }
      setLastLookup(lookup);
      return lookup;
    } catch (error) {
      setUncertainError(getApiErrorMessage(error, t('visitRequestV2:uncertain.checkFailed')));
      return null;
    } finally {
      setIsCheckingResult(false);
    }
  }, [uncertain, isCheckingResult, form, clearDraftAfterCreate, resetOtpChallengeState, onSuccess, t]);

  /** Abandons the uncertain panel and hands the form back, keeping the draft and the intent. */
  const backToFormFromUncertain = useCallback(() => {
    setUncertain(null);
    setLastLookup(null);
    setUncertainError(null);
    setStage('EDITING');
  }, []);

  const resendOtp = useCallback(async () => {
    if (!sessionToken || !submissionIdRef.current) return;
    // Never while a verify is in flight: the reply would arrive against a challenge that no longer
    // exists, and the user would be told their correct code was wrong.
    if (stageRef.current === 'VERIFYING_OTP') return;
    const submissionId = submissionIdRef.current;
    setIsResending(true);
    setOtpError(null);
    try {
      const data = form.getValues();
      const res = await visitRequestApi.resendOtp(
        data.registerInfo.email, data.registerInfo.fullName, submissionId, sessionToken);
      setSessionToken(res.sessionToken);
      resetOtpChallengeState();
      setRemainingAttempts(res.maxAttempts ?? null);
      setResendAfterSeconds(res.resendAfterSeconds ?? 60);
      // A resend supersedes the old code but stays the SAME submission — only the challenge is new.
      const otp: VisitRequestV2OtpContext = {
        targetEmail: data.registerInfo.email,
        maskedEmail: maskedEmail || data.registerInfo.email,
        expiresAt: null,
        resendAfterSeconds: res.resendAfterSeconds ?? null,
        savedAt: Date.now(),
      };
      setPendingOtp(otp);
      saveDraftNow({ submissionId, otp });
      saveOtpChallengeToken(submissionId, res.sessionToken, draftNamespace);
      showSuccessToast(t('visitRequestV2:otpFlow.codeResent'), 'v2-otp-resent');
    } catch (error) {
      const code = getApiErrorCode(error);
      const meta = getOtpErrorMeta(error);
      if (meta.humanVerificationRequired || code === OTP_HUMAN_VERIFICATION_REQUIRED) {
        setHumanVerificationRequired(true);
      }
      // A refused RESEND is the commonest way to meet a quota, and this branch used to throw its
      // retry metadata away: only `humanVerificationRequired` was read, so `retryAt` never reached
      // state, the cooldown screen never appeared, and the resend button re-enabled after its own
      // local 60s timer regardless of the hour-long wait the server had just described.
      if (meta.remainingAttempts !== null) setRemainingAttempts(meta.remainingAttempts);
      setRetryAfterSeconds(meta.retryAfterSeconds);
      setRetryAt(meta.retryAt);
      setOtpError(
        otpRateLimitMessage(code, meta, t)
        ?? getApiErrorMessage(error, t('toast:visitRequest.otpResendFailed')),
      );
    } finally {
      setIsResending(false);
    }
  }, [sessionToken, form, maskedEmail, draftNamespace, resetOtpChallengeState, saveDraftNow, t]);

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
        // Recovery has its own hourly quota (OTP_RECOVERY_RATE_LIMITED), so it needs the same
        // treatment as resend: keep the wait, and say which limit was reached.
        const meta = getOtpErrorMeta(error);
        setRetryAfterSeconds(meta.retryAfterSeconds);
        setRetryAt(meta.retryAt);
        setOtpError(
          otpRateLimitMessage(getApiErrorCode(error), meta, t)
          ?? getApiErrorMessage(error, t('toast:common.defaultError')),
        );
      } finally {
        setIsRecoveringOtp(false);
      }
    },
    [sessionToken, isRecoveringOtp, form, resetOtpChallengeState, t],
  );

  /**
   * Closing the OTP modal is NOT cancelling the request. The form, the draft, the submission id and
   * the challenge all survive; only the modal goes away. Previously this dropped the submissionId,
   * so coming back meant a brand-new submit intent — and the user was told nothing, so an
   * accidental Esc looked exactly like losing the whole form.
   */
  const cancelOtp = useCallback(() => {
    // Never mid-verify: the create may be committing right now, and dropping the session token here
    // would leave the user with no way to learn the outcome.
    if (stageRef.current === 'VERIFYING_OTP') return;
    setSessionToken(null);
    resetOtpChallengeState();
    saveDraftNow();
    setStage('EDITING');
    if (hasMeaningfulV2Data(form.getValues())) {
      showInfoToast(t('visitRequestV2:draft.savedForLater'));
    }
  }, [resetOtpChallengeState, saveDraftNow, form, t]);

  /**
   * Re-opens the modal for a challenge that was already issued — after closing it, or after a
   * reload in the same tab. It never asks for a new code: an unnecessary resend burns the user's
   * rate limit and invalidates the code that may already be sitting in their inbox.
   */
  const resumeOtp = useCallback((): boolean => {
    const submissionId = submissionIdRef.current;
    if (!submissionId || !pendingOtp) return false;
    const token = readOtpChallengeToken(submissionId, draftNamespace);
    if (!token) return false;
    setMaskedEmail(pendingOtp.maskedEmail);
    setSessionToken(token);
    resetOtpChallengeState();
    setStage('OTP_PENDING');
    return true;
  }, [pendingOtp, draftNamespace, resetOtpChallengeState]);

  /**
   * "Let me look at the form again" from inside the OTP modal (plan §12). The challenge is KEPT —
   * the session token, the submission id and the code already in the user's inbox all stay valid —
   * so checking a date does not cost them a new code or their place in the rate limit.
   */
  const reviewFormDuringOtp = useCallback(() => {
    if (stageRef.current === 'VERIFYING_OTP') return;
    setOtpError(null);
    setStage('EDITING');
  }, []);

  /** Back into the modal after a review; the challenge never left. */
  const continueOtpAfterReview = useCallback((): boolean => {
    if (sessionToken) { setStage('OTP_PENDING'); return true; }
    return resumeOtp();
  }, [sessionToken, resumeOtp]);

  /** Forgets the pending challenge (keeping the form) — "I will start the verification again". */
  const discardPendingOtp = useCallback(() => {
    setPendingOtp(null);
    setSessionToken(null);
    clearOtpChallengeToken(draftNamespace);
    saveDraftNow({ otp: null });
    resetOtpChallengeState();
    setStage('EDITING');
  }, [draftNamespace, saveDraftNow, resetOtpChallengeState]);

  /**
   * A challenge proves ONE mailbox. Retargeting the form at a different registrant email leaves the
   * old challenge unusable (the backend binds the token to email + purpose + submissionId and
   * answers SESSION_INVALID), so the client must forget it rather than offer to "continue" with a
   * code that can no longer verify anything. The typing is untouched.
   */
  const watchedRegistrantEmail = form.watch('registerInfo.email');
  useEffect(() => {
    if (!pendingOtp) return;
    if (isSameEmailIdentity(watchedRegistrantEmail, pendingOtp.targetEmail)) return;
    discardPendingOtp();
  }, [watchedRegistrantEmail, pendingOtp, discardPendingOtp]);

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
    setCardVersion({});
    submissionIdRef.current = null;
    setPendingOtp(null);
    resetOtpChallengeState();
    clearVisitRequestV2Draft(draftNamespace);
    hasPersistedDraftRef.current = false;
    // A brand-new submit intent: the next submit mints a fresh submissionId, so it can never
    // replay idempotently onto the request that was just completed.
    setUncertain(null);
    setLastLookup(null);
    setUncertainError(null);
    setStage('EDITING');
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
    contactLinkPrompt: contactLink.prompt,
    confirmContactLink: contactLink.confirmSame,
    declineContactLink: contactLink.confirmDifferent,
    dismissContactLink: contactLink.dismiss,
    /** clientKey -> version, bumped on copy/apply-to-all so the card can be force-remounted. */
    cardVersion,
    // Submit
    onSubmit,
    isSelfRegistration,
    /** The single source of truth for "where is this submission" (plan §3). */
    stage,
    // Derived, never independently settable — a caller cannot observe "submitting AND verifying".
    isSubmitting: stage === 'SENDING_OTP',
    submitError,
    setSubmitError,
    /**
     * Fields invalid RIGHT NOW, recomputed every render from `formState.errors` — the number the
     * summary banner states, and the reason it corrects itself while the user types.
     */
    validationErrorCount,
    /** Whether that banner belongs on screen at all (nothing before the first submit attempt). */
    showValidationSummary,
    firstErrorCampusIndex,
    setFirstErrorCampusIndex,
    /** Increments on every invalid submit — the form moves the caret to the first bad field. */
    focusErrorsToken,
    // OTP phase (public mode)
    sessionToken,
    maskedEmail,
    otpError,
    isVerifying: stage === 'VERIFYING_OTP',
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
    /** A challenge already issued for this draft, still waiting to be completed. */
    pendingOtp,
    resumeOtp,
    discardPendingOtp,
    /** §12 — step out to the form and back WITHOUT spending the challenge. */
    reviewFormDuringOtp,
    continueOtpAfterReview,
    // Uncertain result (§10)
    isCheckingResult,
    lastLookup,
    uncertainError,
    checkSubmissionResult,
    backToFormFromUncertain,
    // Draft
    draftHydrated,
    hydrateDraft,
    detectDraft,
    draftAvailableAt,
    restoreDraft,
    discardDraft,
    saveDraftNow,
    /**
     * Whether storage holds a draft for this namespace RIGHT NOW. A function, not a value: the close
     * prompt asks at click time, and a boolean captured in a render would answer for a moment that
     * has passed. Not `draftAvailableAt !== null` — that only says a draft is being OFFERED, and it
     * goes back to null the instant the offer is accepted while the draft itself lives on.
     */
    hasPersistedDraft: useCallback(() => hasPersistedDraftRef.current, []),
    /** "Close without saving" — see the callback for what it does and does not delete. */
    abandonEdits,
    migratedFromGlobalDraft,
    resetForm,
    /** Exposed so the schedule picker enforces the SAME floor the schema does (72h vs 24h). */
    minAdvanceHours,
  };
};
