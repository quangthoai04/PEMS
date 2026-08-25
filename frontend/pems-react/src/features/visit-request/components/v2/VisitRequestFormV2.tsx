import React, { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Controller } from 'react-hook-form';
import { AlertCircle, BadgeCheck, Globe, Loader2, Mail, Phone, Plus, RefreshCw, Send } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import {
  useVisitRequestFormV2,
  type UseVisitRequestFormV2Options,
  type SubmissionStage,
} from '../../hooks/useVisitRequestFormV2';
import { VisitCreateUncertainPanel } from './VisitCreateUncertainPanel';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import { V2_MAX_CAMPUSES } from '../../schema/visitRequestV2.schema';
import type { V2CreateResponse } from '../../api/visitRequestV2Api';
import { useRegistrationCampuses } from '../../hooks/useRegistrationCampuses';
import { campusVisitHasUserContent } from '../../utils/visitRequestV2Form';
import { focusFirstInvalidField } from '../../utils/formErrorNavigation';
import { hasMeaningfulV2Data, type SaveV2DraftResult } from '../../utils/visitRequestV2DraftStorage';
import { CampusVisitCard } from './CampusVisitCard';
import { FormField, inputCls } from '../shared/FormField';
import { PhoneField } from '../shared/PhoneField';
import { CountrySelect } from '../shared/CountrySelect';
import { PartnerOrgCombobox } from '../shared/PartnerOrgCombobox';
import { FormSection } from '../shared/FormSection';
import { OtpVerificationModal } from '../OtpVerificationModal';
import type { CreatorRole } from '../../schema/visitRequestV2.schema';
import type { CampusHostSelectionChoice } from '../../api/visitRequestApi';
import { useAuthContext } from '../../../../shared/auth/AuthContext';
import { profileApi } from '../../../profile/api/profileApi';
import type { ViewProfileResponse } from '../../../profile/types/profile.types';
import { getApiErrorMessage } from '../../../../shared/utils/toast';
import { isSameEmailIdentity } from '../../../../shared/utils/emailIdentity';
import { commitFieldValue, fieldChangeHandler } from '../../../../shared/utils/formRevalidate';
import { ContactLinkPromptDialog } from './ContactLinkPromptDialog';

interface Props {
  mode: UseVisitRequestFormV2Options['mode'];
  draftNamespace?: string;
  onSuccess: (result: V2CreateResponse, values: VisitRequestV2Schema) => void;
  /**
   * Optional sticky-footer node (supplied by the modal shell) to portal the submit actions into.
   * Omitted on the standalone route, where the actions simply end the page.
   */
  footerSlot?: HTMLElement | null;
  /** Lets a host warn before discarding typed data (modal close / Esc). */
  onDirtyChange?: (dirty: boolean) => void;
  /**
   * Hands the host the draft controls so a close prompt can offer "save draft and exit" and
   * "discard changes" without reimplementing draft storage.
   */
  onDraftControls?: (controls: {
    /** Writes the draft NOW (no debounce) and says whether it actually landed. */
    saveDraftNow: () => SaveV2DraftResult;
    /** "Exit without saving": deletes this namespace's draft outright and stops autosave. */
    abandonEdits: () => void;
    /** True when the form differs from its current baseline — NOT "is there enough to save". */
    isDirty: () => boolean;
    /** True when the form holds something worth writing to storage. */
    hasMeaningfulData: () => boolean;
    /** True when storage already holds a draft for this namespace. */
    hasPersistedDraft: () => boolean;
    /** True while a verify is in flight — the host must not tear the shell down mid-transaction. */
    isBusy: () => boolean;
  }) => void;
}

/**
 * Per-campus form v2 (plan §9.1): request-level registrant + primary contact once, then one
 * complete snapshot card per campus. Every campus is independent — copying is an explicit,
 * confirmed one-time deep clone, and the submit payload is the REAL v2 contract (the backend
 * derives scope/mixed state; nothing here is a mock and there is no silent v1 fallback).
 */
export const VisitRequestFormV2: React.FC<Props> = ({
  mode, draftNamespace, onSuccess, footerSlot, onDirtyChange, onDraftControls,
}) => {
  const { t } = useTranslation(['visitRequestV2', 'visitRequest', 'validation']);
  const { campuses, loading: campusesLoading } = useRegistrationCampuses();
  const [showErrors, setShowErrors] = useState(false);
  /** Set when "continue verifying" found no usable challenge left (another tab, or storage cleared). */
  const [resumeFailed, setResumeFailed] = useState(false);
  /** Mirrors the submission stage for callbacks the host keeps across renders. */
  const stageRef = useRef<SubmissionStage>('EDITING');
  const [openKeys, setOpenKeys] = useState<Set<string>>(new Set());
  const [pendingRemove, setPendingRemove] = useState<number | null>(null);
  const cardRefs = useRef(new Map<string, HTMLDivElement | null>());
  const formRef = useRef<HTMLFormElement>(null);

  // ── Authenticated create: who processes each campus (backend re-authorizes everything). ──
  const { user, isReady: authReady, effectiveRole } = useAuthContext();
  const isAuthenticated = mode === 'authenticated';
  // A full navigation (not `useNavigate`) deliberately: this form is mounted both inside a modal and
  // on a standalone route, and a client-side navigate from inside the modal would leave the overlay
  // stuck open over the Profile page underneath it. A full page load closes everything and lands the
  // user cleanly on Profile — appropriate for what is already a rare, blocking edge case.
  const goToProfile = () => { window.location.href = '/dashboard/profile'; };

  // Derived from `effectiveRole` — the canonical role AuthContext resolves from the profile — rather
  // than re-deriving Staff/Leader from raw roleCode/subRole a second time (naming-conventions/BA
  // conventions aside, this project treats effectiveRole as the one source authorization decisions
  // should read). Only Visitor/Staff/Staff Leader are ever offered authenticated create, so anything
  // else (ADMIN/HO/DEPARTMENT*/STUDENT, or `null` while auth has not resolved yet) falls back to the
  // least-privileged shape — harmless, because nothing renders off it until `authReady`.
  const creatorRole: CreatorRole = React.useMemo(() => {
    if (effectiveRole === 'STAFF_LEADER') return 'STAFF_LEADER';
    if (effectiveRole === 'STAFF') return 'STAFF';
    return 'VISITOR';
  }, [effectiveRole]);
  // Internal Staff/Staff Leader is what may file inside the 72h floor (plan
  // PEMS_INTERNAL_SELF_CREATE_SHORT_NOTICE_72H) — self-registration is no longer a separate question:
  // authenticated create IS self-registration, always (plan CanhIter3FixBug), so the floor now follows
  // the role alone.
  const isInternalActor = creatorRole === 'STAFF' || creatorRole === 'STAFF_LEADER';

  // ── Authenticated registrant = the signed-in account's own profile, always (plan CanhIter3FixBug)
  //    ────────────────────────────────────────────────────────────────────────────────────────────
  // No "Tôi là người đăng ký" button, no delegated-OTP path: the profile loads automatically the
  // moment auth has settled, and Registrant renders read-only from it. If the profile is missing a
  // field this form requires, the fix is the Profile page, never a text box on this one.
  type ProfileLoadState = 'idle' | 'loading' | 'ready' | 'error';
  const [profileState, setProfileState] = useState<ProfileLoadState>('idle');
  const [profile, setProfile] = useState<ViewProfileResponse | null>(null);
  const [profileError, setProfileError] = useState<string | null>(null);

  const loadProfile = useCallback(async () => {
    setProfileState('loading');
    setProfileError(null);
    try {
      const me = await profileApi.getMyProfile();
      setProfile(me);
      setProfileState('ready');
    } catch (err) {
      setProfileState('error');
      setProfileError(getApiErrorMessage(err, t('visitRequestV2:registrant.autofillFailed')));
    }
  }, [t]);

  useEffect(() => {
    if (!isAuthenticated || !authReady) return;
    void loadProfile();
    // Deliberately NOT depending on `loadProfile` (it is stable across `t` changes we do not care
    // about here) — this must fire exactly once per (auth-ready) mount, not on every locale switch.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, authReady]);

  // Keyed by campus CODE, so reordering or removing a card never moves a decision onto another
  // campus. Entries for campuses no longer selected are dropped at submit time, never sent.
  const [campusHostSelections, setCampusHostSelection] = useState<Record<string, CampusHostSelectionChoice>>({});
  const campusHostSelectionsRef = useRef(campusHostSelections);
  campusHostSelectionsRef.current = campusHostSelections;

  const vm = useVisitRequestFormV2(onSuccess, () => setShowErrors(true), {
    mode,
    draftNamespace,
    currentUserEmail: user?.email,
    isInternalActor,
    // The ceiling is "one card per campus open for registration", read from the backend — not a
    // constant. Retiring or adding a campus changes the form with no code change.
    maxCampuses: campuses.length || undefined,
    getCampusHostSelections: () => {
      if (!isAuthenticated) return [];
      // A processing intent is only ever valid on a SELF-registration: on a delegated submission the
      // backend refuses the whole payload, so stale choices must never leave the browser. The panel is
      // hidden and the state cleared when the email changes, and this is the last gate before the wire.
      if (!isSameEmailIdentity(user?.email, form.getValues('registerInfo.email'))) return [];
      const selected = new Set(
        form.getValues('campusVisits').map(cv => (cv.campus || '').toUpperCase()).filter(Boolean),
      );
      return Object.values(campusHostSelectionsRef.current).filter(p => selected.has(p.campusId));
    },
  });
  const { form, campusVisitFields } = vm;
  useEffect(() => { stageRef.current = vm.stage; }, [vm.stage]);

  /**
   * Look for a saved draft once PER NAMESPACE, and never apply it silently — the user is offered
   * the choice.
   *
   * "Once per namespace", not "once per mount", is the whole fix. In authenticated mode the
   * namespace is `u{userId}`, and the user arrives from AuthContext one render LATE. A single
   * mount-time detect therefore ran while the namespace was still undefined: it looked in the
   * PUBLIC draft key, found nothing, and never looked again once the account key existed — so a
   * perfectly good draft sat in `u15` while the form claimed there was none. Whether the prompt
   * appeared came down to whether AuthContext happened to have resolved first, which is why the
   * same user saw it some of the time.
   *
   * Authenticated mode therefore WAITS for a namespace rather than falling back to the public key:
   * reading it would show one person a draft that is not theirs, and writing it would leave an
   * account's typing in the key anonymous visitors share.
   */
  const detectedNamespaceRef = useRef<string | null>(null);
  const { detectDraft } = vm;
  useEffect(() => {
    if (isAuthenticated && !draftNamespace) return;
    const namespaceKey = draftNamespace ?? '__public__';
    if (detectedNamespaceRef.current === namespaceKey) return;
    detectedNamespaceRef.current = namespaceKey;
    detectDraft();
  }, [isAuthenticated, draftNamespace, detectDraft]);

  /**
   * Canonical profile → registerInfo mapping (plan CanhIter3FixBug §6). Every value here is a
   * profile field that already exists — nothing is invented: `displayPosition` IS the job title
   * ("Trưởng phòng"/"Nhân viên"), `displayDepartmentName` (falling back to `department.name`, then
   * `displayCampusName` for a Visitor with neither) IS the organization the account belongs to.
   *
   * organization/jobTitle are only meaningful here for an INTERNAL account — a Visitor's profile has
   * no such fields at all (there is no account-level "organization" for an external guest whose
   * sponsoring org legitimately changes visit to visit), so both come back empty for one and the
   * caller must not treat that as "the profile is missing data" the way it would for Staff.
   */
  const mapProfileToRegisterInfo = (me: ViewProfileResponse): VisitRequestV2Schema['registerInfo'] => ({
    fullName: me.fullName ?? '',
    email: me.email ?? '',
    phone: me.phone ?? '',
    nationality: me.nationality ?? '',
    jobTitle: me.displayPosition ?? '',
    organization: me.displayDepartmentName ?? me.department?.name ?? me.displayCampusName ?? '',
  });

  /**
   * Overwrites `registerInfo` with the LIVE profile once the draft decision has been made (plan
   * §9 — "Draft — cực kỳ quan trọng"). Ordering is the whole point: `vm.draftHydrated` only becomes
   * true after a restore/discard has already run (or immediately, when there was no draft to ask
   * about), so this always runs AFTER any stale/delegated registrant a pre-rule draft might carry —
   * never before, which would just have the draft's own `form.reset` overwrite this again a moment
   * later with somebody else's snapshot.
   *
   * A Visitor's organization/jobTitle are the one exception (plan §6 Visitor org/title exception):
   * they are per-VISIT information the account model has no field for, so they are left exactly as
   * they already are on the form — whatever the user just typed, or whatever a legitimately restored
   * draft carried — rather than being blanked out by a profile that was never their source.
   *
   * `form.reset` (not a per-field `setValue`) so the new registrant becomes the DIRTY-tracking
   * baseline too: this is not an edit the user made, and a close-prompt firing the instant the
   * profile loads — with nothing yet typed — would be exactly the false positive `isDirty` already
   * had to be rescued from once (see `isFormDirty` below). Safe to re-run on every dependency change
   * (a draft restored after the profile was already ready, a profile reload from Retry): applying
   * the same values twice is a no-op, and there is no risk of a loop since none of the dependencies
   * are touched by this effect's own action.
   *
   * `useLayoutEffect`, not `useEffect`: this must land BEFORE the browser paints the commit that made
   * `canInteractWithForm` true, or the registrant summary would flash empty for one frame before this
   * runs — exactly the kind of "right for a frame, then flips" the auth-readiness gate (plan §4)
   * exists to rule out for the 72h floor, and the same standard applies to the identity it is shown
   * next to.
   */
  useLayoutEffect(() => {
    if (!isAuthenticated || !vm.draftHydrated || profileState !== 'ready' || !profile) return;
    const mapped = mapProfileToRegisterInfo(profile);
    const current = form.getValues();
    const registerInfo = isInternalActor
      ? mapped
      : { ...mapped, organization: current.registerInfo?.organization ?? '', jobTitle: current.registerInfo?.jobTitle ?? '' };
    form.reset({ ...current, registerInfo });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated, vm.draftHydrated, profileState, profile, form, isInternalActor]);

  /**
   * Registrant fields Visit Request V2 requires that the profile left blank. Phone is always
   * optional; organization/jobTitle are only checked for an INTERNAL account — a Visitor's profile
   * never carries them (the Visitor org/title exception above), so an empty value there is not an
   * incomplete profile, it is simply "not filled in on the form yet", which the schema (not this
   * blocking notice) already asks for.
   */
  const missingProfileFields = useMemo(() => {
    if (!isAuthenticated || profileState !== 'ready' || !profile) return [] as string[];
    const reg = mapProfileToRegisterInfo(profile);
    const required: Array<[keyof typeof reg, string]> = [
      ['fullName', t('visitRequestV2:registrant.fullName')],
      ['nationality', t('visitRequestV2:registrant.nationality')],
      ['email', t('visitRequestV2:card.email')],
    ];
    if (isInternalActor) {
      required.push(
        ['organization', t('visitRequestV2:registrant.organization')],
        ['jobTitle', t('visitRequestV2:registrant.jobTitle')],
      );
    }
    return required.filter(([key]) => !reg[key]?.trim()).map(([, label]) => label);
  }, [isAuthenticated, profileState, profile, t, isInternalActor]);

  /**
   * Whether the interactive form (registrant summary, campus cards, submit) may render at all.
   * Gates on auth bootstrap, the profile round trip, AND the draft decision together, so nothing
   * ever shows a picker — or a registrant identity — built from a role/profile that has not fully
   * settled yet and then flips under the user (plan §4 — "auth readiness"): a Staff Leader must
   * never see the 72h floor for even one frame before it drops to 0, and the registrant summary must
   * never render off the pre-profile empty defaults before the profile has actually been applied
   * (the profile-apply effect above waits on the SAME `vm.draftHydrated` condition).
   */
  const isRegistrantReady = !isAuthenticated || (authReady && profileState === 'ready' && vm.draftHydrated);
  const isProfileIncomplete = isAuthenticated && profileState === 'ready' && missingProfileFields.length > 0;
  const canInteractWithForm = isRegistrantReady && !isProfileIncomplete;
  const isProfileBootstrapping = isAuthenticated && (!authReady || profileState === 'idle' || profileState === 'loading' || !vm.draftHydrated);
  const isProfileError = isAuthenticated && authReady && profileState === 'error';

  // First card open by default; keep the set in sync when cards are added/removed.
  useEffect(() => {
    setOpenKeys(prev => {
      if (prev.size > 0) return prev;
      const first = form.getValues('campusVisits')[0]?.clientKey;
      return first ? new Set([first]) : prev;
    });
  }, [campusVisitFields.fields.length, form]);

  // A campus with an error is expanded and scrolled into view — closed cards still show
  // their error badge, so nothing is hidden.
  useEffect(() => {
    if (vm.firstErrorCampusIndex === null) return;
    const cv = form.getValues('campusVisits')[vm.firstErrorCampusIndex];
    if (!cv) return;
    setOpenKeys(prev => new Set(prev).add(cv.clientKey));
    const card = cardRefs.current.get(cv.clientKey);
    // Guarded because scrolling is a nicety and expanding the card is not: in a headless DOM
    // `scrollIntoView` does not exist, and letting it throw here would abandon the effect before
    // the state that actually matters had been applied.
    if (typeof card?.scrollIntoView === 'function') {
      card.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    vm.setFirstErrorCampusIndex(null);
  }, [vm.firstErrorCampusIndex, form, vm]);

  // …and then the caret lands ON the offending field (plan §19). Deferred by a tick because the
  // card above may only just have been expanded, and a field inside a `hidden` body cannot take
  // focus — running in the same commit would silently focus nothing.
  useEffect(() => {
    if (vm.focusErrorsToken === 0) return;
    const timer = window.setTimeout(() => {
      focusFirstInvalidField(formRef.current ?? document);
    }, 60);
    return () => window.clearTimeout(timer);
  }, [vm.focusErrorsToken]);

  const campusLabel = useCallback(
    (cv: VisitRequestV2Schema['campusVisits'][number], index: number): string => {
      const name = campuses.find(c => c.campusCode === cv.campus)?.campusName;
      return name ?? t('visitRequestV2:card.cardN', { n: index + 1 });
    },
    [campuses, t],
  );

  const toggleCard = (clientKey: string) =>
    setOpenKeys(prev => {
      const next = new Set(prev);
      if (next.has(clientKey)) next.delete(clientKey);
      else next.add(clientKey);
      return next;
    });

  const requestRemove = (index: number) => {
    const cv = form.getValues('campusVisits')[index];
    if (cv && campusVisitHasUserContent(cv)) {
      setPendingRemove(index);
    } else {
      vm.removeCampusVisit(index);
    }
  };

  const { register, formState: { errors } } = form;
  const regErr = errors.registerInfo;

  // One card per campus open for registration — the ceiling and the "already taken" set both come
  // from live data, so a campus added or retired in the backend is reflected without a code change.
  const watchedCampusVisits = form.watch('campusVisits');
  const campusLimit = campuses.length > 0
    ? Math.min(campuses.length, V2_MAX_CAMPUSES)
    : V2_MAX_CAMPUSES;
  const takenCampusCodes = (watchedCampusVisits ?? [])
    .map(cv => (cv.campus || '').toUpperCase())
    .filter(Boolean);

  /**
   * "Has the user changed anything?" — React Hook Form's own answer, compared against the CURRENT
   * baseline (the defaults, or the values a restored draft was `reset` with).
   *
   * This used to ask `hasMeaningfulV2Data(...)` instead, which answers a different question
   * entirely: "is there enough here to be worth saving?". Anything that question did not cover —
   * a job title, a working language, an operational contact, a visitor's organization — read as
   * "nothing has been typed", so the close prompt never appeared and X threw the work away without
   * asking. The two are kept apart deliberately: this one guards the close, that one guards the
   * write.
   */
  const isFormDirty = form.formState.isDirty;
  const isDirtyRef = useRef(false);
  useEffect(() => {
    isDirtyRef.current = isFormDirty;
    onDirtyChange?.(isFormDirty);
  }, [isFormDirty, onDirtyChange]);

  const { saveDraftNow, abandonEdits, hasPersistedDraft } = vm;
  useEffect(() => {
    onDraftControls?.({
      saveDraftNow,
      abandonEdits,
      // All read through refs or straight off the form: the host holds this object across renders and
      // would otherwise be answered by a closure from whenever it was handed over.
      isDirty: () => isDirtyRef.current,
      hasMeaningfulData: () => hasMeaningfulV2Data(form.getValues()),
      hasPersistedDraft,
      isBusy: () => stageRef.current === 'VERIFYING_OTP' || stageRef.current === 'SENDING_OTP',
    });
  }, [onDraftControls, saveDraftNow, abandonEdits, hasPersistedDraft, form]);

  const submitBar = (node: React.ReactNode) =>
    footerSlot ? createPortal(node, footerSlot) : node;

  const watchedReg = form.watch('registerInfo');
  const isRegInfoEmpty = !watchedReg?.fullName?.trim() && !watchedReg?.organization?.trim() && !watchedReg?.phone?.trim() && !watchedReg?.email?.trim();

  // Authenticated create is self-registration ALWAYS (plan CanhIter3FixBug) — there is no more
  // delegated state for the campus processing panel to disappear under, so the effect that used to
  // clear `campusHostSelections` when the registrant email stopped matching the account is gone: that
  // transition cannot happen any more (registerInfo is profile-locked/read-only once the form is
  // interactive at all).

  return (
    // `spellCheck={false}` is set HERE, on the form, and inherited by every control inside it (the
    // attribute's "inherit" default is what the HTML spec defines): Vietnamese prose in "Mục đích"
    // or "Nội dung làm việc" was being underlined in red by the browser's own English dictionary,
    // which on a form that also marks its real errors in red reads as "this data is wrong". The
    // rest of the site keeps its spell checking — this is not a global switch — and nothing about
    // autocomplete, Unicode input or validation changes.
    <form
      ref={formRef}
      id="visit-request-v2-form"
      onSubmit={vm.onSubmit}
      noValidate
      spellCheck={false}
      className="space-y-6"
    >
      {/* ── Restore Draft Modal ── */}
      <AnimatePresence>
        {vm.draftAvailableAt !== null && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            data-testid="v2-draft-prompt"
            className="fixed inset-0 z-[300] bg-black/50 flex items-center justify-center p-4 backdrop-blur-sm"
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
                  data-testid="v2-draft-discard"
                  onClick={vm.discardDraft}
                  className="px-4 py-2 rounded-xl bg-gray-100 hover:bg-gray-200 text-gray-700 text-sm font-bold transition-colors"
                >
                  {t('visitRequest:draft.discard')}
                </button>
                <button
                  type="button"
                  data-testid="v2-draft-restore"
                  onClick={vm.restoreDraft}
                  className="px-4 py-2 rounded-xl bg-[#004c91] hover:bg-[#013565] text-white text-sm font-bold transition-colors shadow-lg shadow-blue-900/20"
                >
                  {t('visitRequest:draft.restore')}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {vm.migratedFromGlobalDraft && (
        <div role="status" className="rounded-xl border border-blue-200 bg-blue-50 p-3 text-sm font-normal text-blue-800">
          {t('visitRequestV2:draft.migrated')}
        </div>
      )}

      {/* ── The verify never came back ──
          Shown INSTEAD of an error, because nobody knows yet whether the request exists. */}
      {vm.stage === 'CREATE_UNCERTAIN' && (
        <VisitCreateUncertainPanel
          isChecking={vm.isCheckingResult}
          lookup={vm.lastLookup}
          error={vm.uncertainError}
          onCheck={() => void vm.checkSubmissionResult()}
          onBackToForm={vm.backToFormFromUncertain}
        />
      )}

      {/* ── Auth bootstrap / profile round trip (authenticated mode only, plan §4/§5) ──
          Nothing role-dependent renders until BOTH auth has settled and the profile has loaded: a
          picker built off the wrong role for one frame and then flipped is exactly what "Ctrl+Shift+R
          fixes it" used to look like, and this is what removes that frame entirely. */}
      {isProfileBootstrapping && (
        <div data-testid="v2-registrant-loading" className="animate-pulse space-y-4" aria-busy="true">
          <div className="h-24 rounded-xl bg-slate-100" />
          <div className="h-40 rounded-xl bg-slate-100" />
        </div>
      )}

      {isProfileError && (
        <div
          role="alert"
          data-testid="v2-profile-error"
          className="flex flex-col items-start gap-3 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-700"
        >
          <div className="flex items-start gap-2">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>{profileError}</span>
          </div>
          <button
            type="button"
            data-testid="v2-profile-retry"
            onClick={() => void loadProfile()}
            className="inline-flex items-center gap-1.5 rounded-lg border border-red-300 bg-white px-3 py-1.5 text-sm font-semibold text-red-700 hover:bg-red-100"
          >
            <RefreshCw className="h-3.5 w-3.5" aria-hidden />
            {t('visitRequestV2:registrant.profileRetry')}
          </button>
        </div>
      )}

      {isProfileIncomplete && (
        <div
          role="alert"
          data-testid="v2-profile-incomplete"
          className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800"
        >
          <p className="font-bold">{t('visitRequestV2:registrant.profileIncompleteTitle')}</p>
          <p className="mt-1">{t('visitRequestV2:registrant.profileIncompleteDesc')}</p>
          <p className="mt-2 font-semibold">
            {t('visitRequestV2:registrant.profileIncompleteMissing', { fields: missingProfileFields.join(', ') })}
          </p>
          <button
            type="button"
            data-testid="v2-profile-goto"
            onClick={goToProfile}
            className="mt-3 inline-flex items-center gap-1.5 rounded-lg bg-[#004c91] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#013565]"
          >
            {t('visitRequestV2:registrant.goToProfile')}
          </button>
        </div>
      )}

      {/* ── Everything the user can change, locked as ONE unit while a submit is in flight ──
          A native `disabled` fieldset, not pointer-events or an overlay: it disables every control
          it contains — inputs, textareas, selects, comboboxes, date/time pickers, add/remove campus,
          the Excel import, the quick-fill buttons — for the mouse AND the keyboard, which is what a
          CSS-only lock quietly fails to do. The payload left the browser as a deep clone taken at
          submit time, so this is not about protecting the request in flight; it is about the screen
          never claiming to hold data that is not what was sent. `vm.isSubmitting` is the SAME state
          the submit button reads (stage === 'SENDING_OTP'), so the lock cannot outlive the request
          or lift before it: a failure puts the stage back and every field is editable again with
          the user's typing untouched.
          Rendered only once the form is actually interactive — bootstrapping/error/incomplete render
          their own panels above instead (plan §4/§5/§20: no registrant controls, no submit, until the
          profile round trip has actually settled). */}
      {canInteractWithForm && (
      <fieldset
        disabled={vm.isSubmitting}
        // `disabled` covers everything the browser recognises as a control. `inert` covers the rest:
        // the organization/nationality pickers are react-select, whose menu opens from a <div>, and
        // a disabled fieldset says nothing about a div. Together they take clicks, keystrokes and
        // focus off the whole block — leaving one of them out leaves a way to edit the form while
        // its data is being sent.
        inert={vm.isSubmitting}
        data-testid="v2-form-fields"
        className="min-w-0 space-y-6"
      >
      {/* ── Reviewing the form with the challenge still in hand (plan §12) ──
          The user stepped out of the modal to check something. The code in their inbox is still
          valid and the session token is still held — going back in costs them nothing. */}
      {vm.sessionToken && vm.stage === 'EDITING' && (
        <div
          role="status"
          data-testid="v2-otp-review"
          className="rounded-xl border border-blue-200 bg-blue-50 p-3 text-sm text-blue-900"
        >
          <p className="font-bold">{t('visitRequestV2:otpFlow.reviewBannerTitle', { email: vm.maskedEmail })}</p>
          <p className="mt-1">{t('visitRequestV2:otpFlow.reviewBannerBody')}</p>
          <button
            type="button"
            data-testid="v2-otp-review-continue"
            onClick={() => vm.continueOtpAfterReview()}
            className="mt-3 rounded-lg bg-[#004c91] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#003a6f]"
          >
            {t('visitRequestV2:otpFlow.continueVerification')}
          </button>
        </div>
      )}

      {/* ── A verification already asked for, waiting to be finished ──
          Closing the OTP modal or reloading the tab does not throw the request away, so the way
          back in has to be visible. It never re-sends by itself: an unnecessary code burns the
          rate limit and kills the one that may already be in the user's inbox. */}
      {vm.pendingOtp && !vm.sessionToken && (
        <div
          role="status"
          data-testid="v2-otp-resume"
          className="rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900"
        >
          <p className="font-bold">{t('visitRequestV2:draft.pendingOtpTitle')}</p>
          <p className="mt-1">
            {t('visitRequestV2:draft.pendingOtpBody', { email: vm.pendingOtp.maskedEmail })}
          </p>
          {resumeFailed && (
            <p className="mt-1 font-normal" role="alert">
              {t('visitRequestV2:draft.resumeOtpUnavailable')}
            </p>
          )}
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              data-testid="v2-otp-resume-continue"
              onClick={() => setResumeFailed(!vm.resumeOtp())}
              className="rounded-lg bg-[#f37021] px-3 py-1.5 text-sm font-bold text-white hover:bg-[#e0631a]"
            >
              {t('visitRequestV2:draft.resumeOtp')}
            </button>
            <button
              type="button"
              data-testid="v2-otp-resume-discard"
              onClick={() => { setResumeFailed(false); vm.discardPendingOtp(); }}
              className="rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-sm font-semibold text-amber-800 hover:bg-amber-100"
            >
              {t('visitRequestV2:draft.discardOtp')}
            </button>
          </div>
        </div>
      )}

      {/* ── Request-level: registrant ──
          Authenticated Staff/Staff Leader: fully read-only, profile-backed summary — organization and
          job title ARE a fixed HR attribute for an internal account, so both come from the profile
          like everything else. No "Tôi là người đăng ký" button, no delegated-OTP banner.
          Authenticated Visitor: identity (name/email/phone/nationality) is locked to the profile the
          same way, but organization/jobTitle stay EDITABLE — a Visitor's organization is per-VISIT
          information (a professor represents University A today, Ministry B next month), not a fixed
          account attribute the way it is for Staff, and the account model has no field for it at all
          (plan CanhIter3FixBug — Visitor org/title exception). Public: unchanged, fully editable + OTP. */}
      <FormSection id="v2-registrant" title={t('visitRequestV2:sections.registrant')}>
        {isAuthenticated ? (
          <div>
            <div data-testid="v2-registrant-readonly" className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <p className="text-base font-bold text-slate-900">{watchedReg?.fullName}</p>
              {isInternalActor && (
                <p className="mt-0.5 text-sm font-normal text-slate-600">
                  {[watchedReg?.jobTitle, watchedReg?.organization].filter(Boolean).join(' · ')}
                </p>
              )}
              <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-sm font-normal text-slate-700">
                <span className="inline-flex items-center gap-1.5">
                  <Mail className="h-3.5 w-3.5 text-slate-400" aria-hidden />
                  {watchedReg?.email}
                </span>
                {!!watchedReg?.phone?.trim() && (
                  <span className="inline-flex items-center gap-1.5">
                    <Phone className="h-3.5 w-3.5 text-slate-400" aria-hidden />
                    {watchedReg.phone}
                  </span>
                )}
                {!!watchedReg?.nationality?.trim() && (
                  <span className="inline-flex items-center gap-1.5">
                    <Globe className="h-3.5 w-3.5 text-slate-400" aria-hidden />
                    {watchedReg.nationality}
                  </span>
                )}
              </div>
              <p className="mt-3 flex items-start gap-1.5 text-xs font-normal text-slate-500">
                <BadgeCheck className="mt-0.5 h-3.5 w-3.5 shrink-0 text-emerald-600" aria-hidden />
                {t(isInternalActor
                  ? 'visitRequestV2:registrant.readOnlyNotice'
                  : 'visitRequestV2:registrant.readOnlyNoticeIdentityOnly')}
              </p>
            </div>
            {!isInternalActor && (
              <div className="mt-4 grid grid-cols-12 gap-x-6 gap-y-5">
                <FormField className="col-span-12 lg:col-span-6" label={t('visitRequestV2:registrant.jobTitle')} required error={regErr?.jobTitle?.message} showValidIcon={false}>
                  <input data-testid="v2-registrant-jobTitle" spellCheck={false} {...register('registerInfo.jobTitle')} className={inputCls(!!regErr?.jobTitle, false, false)} />
                </FormField>
                <FormField className="col-span-12 lg:col-span-6" label={t('visitRequestV2:registrant.organization')} required error={regErr?.organization?.message} showValidIcon={false}>
                  <Controller
                    name="registerInfo.organization"
                    control={form.control}
                    render={({ field }) => (
                      <PartnerOrgCombobox
                        organization={field.value ?? ''}
                        partnerId={form.watch('partnerId') ?? null}
                        hasError={!!regErr?.organization}
                        onBlur={field.onBlur}
                        onChange={next => {
                          commitFieldValue(
                            form, 'registerInfo.organization', next.organization, field.onChange);
                          form.setValue('partnerId', next.partnerId, { shouldDirty: true });
                          form.setValue('partnerSelectionMode', next.mode, { shouldDirty: true });
                        }}
                      />
                    )}
                  />
                </FormField>
              </div>
            )}
          </div>
        ) : (
          <div className="grid grid-cols-12 gap-x-6 gap-y-5">
            {/* Row 1: Họ và tên | Quốc tịch | Đơn vị công tác (4/2/6) */}
            <FormField className="col-span-12 lg:col-span-4" label={t('visitRequestV2:registrant.fullName')} required error={regErr?.fullName?.message} showValidIcon={false}>
              {/* Named explicitly as well as inheriting it from the form: a Vietnamese name is the
                  first thing typed into this form and the first thing the dictionary underlines. */}
              <input data-testid="v2-registrant-fullName" spellCheck={false} {...register('registerInfo.fullName')} className={inputCls(!!regErr?.fullName, false, false)} />
            </FormField>
            <FormField className="col-span-12 lg:col-span-2" label={t('visitRequestV2:registrant.nationality')} required error={regErr?.nationality?.message} showValidIcon={false}>
              <Controller
                name="registerInfo.nationality"
                control={form.control}
                render={({ field }) => (
                  <CountrySelect
                    strict
                    value={field.value ?? ''}
                    // Via commitFieldValue: picking a valid country must clear the "Quốc tịch không
                    // được để trống" error immediately, including the pre-submit case where nothing
                    // else would revalidate it (NP-02).
                    onChange={fieldChangeHandler(form, 'registerInfo.nationality', field.onChange)}
                    onBlur={field.onBlur}
                    hasError={!!regErr?.nationality}
                    placeholder={t('visitRequestV2:registrant.nationality')}
                  />
                )}
              />
            </FormField>
            {/* Free-solo partner/organization search: picking a known partner links partnerId,
                typing anything else keeps the text as a manually entered organization. */}
            <FormField className="col-span-12 lg:col-span-6" label={t('visitRequestV2:registrant.organization')} required error={regErr?.organization?.message} showValidIcon={false}>
              <Controller
                name="registerInfo.organization"
                control={form.control}
                render={({ field }) => (
                  <PartnerOrgCombobox
                    organization={field.value ?? ''}
                    partnerId={form.watch('partnerId') ?? null}
                    hasError={!!regErr?.organization}
                    onBlur={field.onBlur}
                    onChange={next => {
                      // Same revalidation contract as the country select: choosing/typing a real
                      // organization clears its required-error at once (NP-02).
                      commitFieldValue(
                        form, 'registerInfo.organization', next.organization, field.onChange);
                      form.setValue('partnerId', next.partnerId, { shouldDirty: true });
                      form.setValue('partnerSelectionMode', next.mode, { shouldDirty: true });
                    }}
                  />
                )}
              />
            </FormField>
            {/* Row 2: Chức vụ | Số điện thoại | Email (4/4/4) */}
            <FormField className="col-span-12 lg:col-span-4" label={t('visitRequestV2:registrant.jobTitle')} required error={regErr?.jobTitle?.message} showValidIcon={false}>
              <input data-testid="v2-registrant-jobTitle" spellCheck={false} {...register('registerInfo.jobTitle')} className={inputCls(!!regErr?.jobTitle, false, false)} />
            </FormField>
            <FormField className="col-span-12 lg:col-span-4" label={t('visitRequestV2:card.phone')} error={regErr?.phone?.message} showValidIcon={false}>
              <PhoneField
                field={register('registerInfo.phone')}
                hasError={!!regErr?.phone}
                error={regErr?.phone?.message}
                testId="v2-registrant-phone"
              />
            </FormField>
            <FormField className="col-span-12 lg:col-span-4" label={t('visitRequestV2:card.email')} required error={regErr?.email?.message} showValidIcon={false}>
              <input type="email" data-testid="v2-registrant-email" {...register('registerInfo.email')} className={inputCls(!!regErr?.email, false, false)} />
            </FormField>
          </div>
        )}
      </FormSection>


      {/* ── Per-campus cards ── */}
      <FormSection
        id="v2-campuses"
        title={t('visitRequestV2:sections.campuses')}
        description={t('visitRequestV2:sections.campusesDesc')}
      >
        {(() => {
          // Same array-root shape as CampusVisitCard's visitors/.min(1) fix: RHF's useFieldArray
          // puts a list-level error at `.root.message`, not `.message`. Currently unreachable in
          // the shipped UI (both the row's remove button and useVisitRequestFormV2's
          // removeCampusVisit independently refuse to drop below 1 campus), but hardened here too
          // so it can't silently reproduce the moment either guard changes.
          const campusVisitsError = errors.campusVisits as { message?: unknown; root?: { message?: unknown } } | undefined;
          const msg = campusVisitsError?.message ?? campusVisitsError?.root?.message;
          return typeof msg === 'string' && (
            <p className="mb-3 text-sm font-normal text-red-600" role="alert">{msg}</p>
          );
        })()}
        <div className="space-y-4">
          {campusVisitFields.fields.map((field, index) => {
            const clientKey = form.getValues(`campusVisits.${index}.clientKey`) || (field as any).clientKey || field.id;
            // A copy/apply-to-all patches this card's form values correctly, but register()-bound
            // inputs and the nested visitors/supportTeam field arrays only re-read fresh values on
            // mount — folding the bump counter into the key forces that remount (vm.cardVersion).
            const renderKey = `${clientKey}:${vm.cardVersion[clientKey] ?? 0}`;
            return (
              <div key={renderKey} ref={el => { cardRefs.current.set(clientKey, el); }}>
                <CampusVisitCard
                  form={form}
                  index={index}
                  clientKey={clientKey}
                  open={openKeys.has(clientKey)}
                  onToggle={() => toggleCard(clientKey)}
                  campuses={campuses}
                  campusesLoading={campusesLoading}
                  takenCampusCodes={takenCampusCodes}
                  copySources={campusVisitFields.fields
                    .map((_, i) => i)
                    .filter(i => i !== index)
                    .map(i => ({ index: i, label: campusLabel(form.getValues('campusVisits')[i], i) }))}
                  onCopyFrom={source => vm.copyContentIntoCampus(index, source)}
                  onApplyToAll={() => vm.requestApplyToAll(index, campusLabel)}
                  onRemove={() => requestRemove(index)}
                  canRemove={campusVisitFields.fields.length > 1}
                  showErrors={showErrors}
                  minAdvanceHours={vm.minAdvanceHours}
                  // Authenticated create is self-registration always, so the processing panel is
                  // simply "does this form belong to an authenticated account" — there is no more
                  // delegated state where a campus routes to its Staff Leader by default instead.
                  processing={isAuthenticated ? {
                    role: creatorRole,
                    ownCampusCode: user?.campusCode,
                    values: campusHostSelections,
                    onChange: next => setCampusHostSelection(prev => ({ ...prev, [next.campusId]: next })),
                  } : undefined}
                />
              </div>
            );
          })}
        </div>
        <button
          type="button"
          data-testid="v2-add-campus"
          disabled={campusVisitFields.fields.length >= campusLimit}
          className="mt-4 inline-flex items-center gap-2 rounded-xl border-2 border-dashed border-[#004c91]/40 px-4 py-2.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5 disabled:opacity-40"
          onClick={() => {
            if (vm.addCampusVisit()) {
              const list = form.getValues('campusVisits');
              const added = list[list.length - 1];
              if (added) setOpenKeys(prev => new Set(prev).add(added.clientKey));
            }
          }}
        >
          <Plus className="h-4 w-4" />
          {t('visitRequestV2:card.addCampus', { count: campusVisitFields.fields.length, max: campusLimit })}
        </button>
      </FormSection>
      </fieldset>
      )}

      {/* ── Submit ──
          When the host supplies a footer node (the modal shell), the actions are portalled into
          it so they can be sticky while the body scrolls. The portal keeps them inside THIS
          <form>, so type="submit" still works and there is no second form implementation.
          Withheld entirely while the authenticated form is not yet interactive (auth bootstrapping,
          profile loading/error, profile incomplete) — plan §4/§5/§20: "không cho submit". */}
      {canInteractWithForm && submitBar(
        <>
          {vm.submitError && (
            <div role="alert" className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-normal text-red-700">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{vm.submitError}</span>
            </div>
          )}
          {/* How much is left — read from the CURRENT validation result on every render, so fixing a
              field takes it from 11 to 10 straight away and the banner disappears of its own accord
              once the last one is fixed. It is never a count remembered from an earlier submit. */}
          {vm.showValidationSummary && (
            <div
              role="alert"
              data-testid="v2-error-summary"
              className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-normal text-red-700"
            >
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{t('validation:fixErrorsCount', { count: vm.validationErrorCount })}</span>
            </div>
          )}
          {/* The standing "72h / 30 phút" hint used to live here on every render. The rule itself is
              still enforced (schema + backend) and still explained contextually, next to the
              schedule fields themselves (VisitDateTimeRangePicker's own HelpTooltip) — this bar no
              longer repeats it permanently. */}
          <div className="flex items-center justify-center gap-4 pt-2 sm:justify-end sm:pt-4">
            <button
              type="submit"
              form="visit-request-v2-form"
              data-testid="v2-submit"
              disabled={vm.isSubmitting}
              className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-[#f37021] px-6 py-3 text-sm font-bold text-white shadow-lg shadow-orange-500/20 transition-colors hover:bg-[#e0631a] disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto"
            >
              {vm.isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
              {/* The label states the contract the form is actually on: authenticated always creates
                  directly, public always ends in an OTP round-trip. */}
              {isAuthenticated
                ? t('visitRequestV2:submit.authenticated')
                : t('visitRequestV2:submit.public')}
            </button>
          </div>
        </>,
      )}

      {/* ── Apply-to-all confirmation (never applies without an explicit confirm) ── */}
      {vm.applyToAllPrompt && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="v2-applyall-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
        >
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h3 id="v2-applyall-title" className="text-base font-extrabold text-slate-900">
              {t('visitRequestV2:applyAll.title')}
            </h3>
            <p className="mt-2 text-sm text-slate-600">
              {vm.applyToAllPrompt.overwrittenLabels.length > 0
                ? t('visitRequestV2:applyAll.overwrites', {
                    campuses: vm.applyToAllPrompt.overwrittenLabels.join(', '),
                  })
                : t('visitRequestV2:applyAll.noOverwrites')}
            </p>
            <p className="mt-1 text-xs text-slate-500">{t('visitRequestV2:applyAll.independentAfter')}</p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold"
                onClick={vm.cancelApplyToAll}
              >
                {t('visitRequestV2:common.cancel')}
              </button>
              <button
                type="button"
                className="rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white"
                onClick={vm.confirmApplyToAll}
              >
                {t('visitRequestV2:applyAll.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── "Đầu mối này có phải là người trong đoàn?" (ID-01) ──────────────────────────────
          Raised at submit, because that is the last moment the answer can still change what is
          stored. A contact typed out by hand who IS in the delegation arrives as a SECOND record of
          one person — the member row has a guest_member_id, the snapshot has nothing — and from
          then on the two are two people to everything downstream, the biên bản included.
          Asked, not assumed: one exact match on name + job title + organisation is worth a question
          and is not proof, and "they are two different people" is a real answer. */}
      {vm.contactLinkPrompt && (
        <ContactLinkPromptDialog
          prompt={vm.contactLinkPrompt}
          onSame={vm.confirmContactLink}
          onDifferent={vm.declineContactLink}
          onReview={vm.dismissContactLink}
        />
      )}

      {/* ── Remove-dirty-campus confirmation ── */}
      {pendingRemove !== null && (
        <div
          role="dialog"
          aria-modal="true"
          aria-labelledby="v2-remove-title"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
        >
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h3 id="v2-remove-title" className="text-base font-extrabold text-slate-900">
              {t('visitRequestV2:remove.title')}
            </h3>
            <p className="mt-2 text-sm text-slate-600">
              {t('visitRequestV2:remove.body', {
                campus: campusLabel(form.getValues('campusVisits')[pendingRemove], pendingRemove),
              })}
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold"
                onClick={() => setPendingRemove(null)}
              >
                {t('visitRequestV2:common.cancel')}
              </button>
              <button
                type="button"
                className="rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white"
                onClick={() => {
                  vm.removeCampusVisit(pendingRemove);
                  setPendingRemove(null);
                }}
              >
                {t('visitRequestV2:remove.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── OTP (v2 create happens at verify; see hook) ──
          Rendered whenever a challenge is live, not only in public mode: an authenticated user
          registering somebody else takes the same challenge, and gating this on `mode` would leave
          them with a pending session token and no way to enter the code. */}
      {/* Held open by the STAGE, not merely by the presence of a token: stepping out to review the
          form keeps the token (that is the point) while hiding the modal. */}
      {vm.sessionToken && (vm.stage === 'OTP_PENDING' || vm.stage === 'VERIFYING_OTP') && (
        <OtpVerificationModal
          maskedEmail={vm.maskedEmail}
          otpError={vm.otpError}
          isVerifying={vm.isVerifying}
          isResending={vm.isResending}
          remainingAttempts={vm.remainingAttempts}
          retryAfterSeconds={vm.retryAfterSeconds}
          retryAt={vm.retryAt}
          resendAfterSeconds={vm.resendAfterSeconds}
          humanVerificationRequired={vm.humanVerificationRequired}
          isRecovering={vm.isRecoveringOtp}
          onVerify={code => void vm.verifyOtp(code)}
          onResend={() => void vm.resendOtp()}
          onRecover={token => void vm.recoverOtp(token)}
          onCancel={vm.cancelOtp}
          onReviewForm={vm.reviewFormDuringOtp}
        />
      )}
    </form>
  );
};
