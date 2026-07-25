import React, { useCallback, useEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import { Controller } from 'react-hook-form';
import { AlertCircle, BadgeCheck, Loader2, Plus, Send, ShieldCheck, UserRound } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useTranslation } from 'react-i18next';
import {
  useVisitRequestFormV2,
  type UseVisitRequestFormV2Options,
} from '../../hooks/useVisitRequestFormV2';
import type { VisitRequestV2Schema } from '../../schema/visitRequestV2.schema';
import { V2_MAX_CAMPUSES } from '../../schema/visitRequestV2.schema';
import type { V2CreateResponse } from '../../api/visitRequestV2Api';
import { useRegistrationCampuses } from '../../hooks/useRegistrationCampuses';
import { campusVisitHasUserContent } from '../../utils/visitRequestV2Form';
import { hasMeaningfulV2Data } from '../../utils/visitRequestV2DraftStorage';
import { CampusVisitCard } from './CampusVisitCard';
import { FormField, inputCls } from '../shared/FormField';
import { CountrySelect } from '../shared/CountrySelect';
import { PartnerOrgCombobox } from '../shared/PartnerOrgCombobox';
import { FormSection } from '../shared/FormSection';
import { OtpVerificationModal } from '../OtpVerificationModal';
import type { CreatorRole } from '../../schema/visitRequestV2.schema';
import type { CampusProcessingChoice } from '../../api/visitRequestApi';
import { useAuthContext } from '../../../../shared/auth/AuthContext';
import { profileApi } from '../../../profile/api/profileApi';
import { getApiErrorMessage } from '../../../../shared/utils/toast';
import { isSameEmailIdentity } from '../../../../shared/utils/emailIdentity';

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
  onDraftControls?: (controls: { saveDraftNow: () => void; discardDraft: () => void; isDirty: () => boolean }) => void;
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
  const [openKeys, setOpenKeys] = useState<Set<string>>(new Set());
  const [pendingRemove, setPendingRemove] = useState<number | null>(null);
  const cardRefs = useRef(new Map<string, HTMLDivElement | null>());

  // ── Authenticated create: who processes each campus (backend re-authorizes everything). ──
  const { user } = useAuthContext();
  const isAuthenticated = mode === 'authenticated';

  const creatorRole: CreatorRole = React.useMemo(() => {
    const rc = (user?.roleCode || '').toUpperCase();
    const sr = (user?.subRole || '').toUpperCase();
    if (rc === 'STAFF') return sr === 'LEADER' ? 'STAFF_LEADER' : 'STAFF';
    return 'VISITOR';
  }, [user?.roleCode, user?.subRole]);

  // Keyed by campus CODE, so reordering or removing a card never moves a decision onto another
  // campus. Entries for campuses no longer selected are dropped at submit time, never sent.
  const [campusProcessing, setCampusProcessing] = useState<Record<string, CampusProcessingChoice>>({});
  const campusProcessingRef = useRef(campusProcessing);
  campusProcessingRef.current = campusProcessing;

  const vm = useVisitRequestFormV2(onSuccess, () => setShowErrors(true), {
    mode,
    draftNamespace,
    currentUserEmail: user?.email,
    // The ceiling is "one card per campus open for registration", read from the backend — not a
    // constant. Retiring or adding a campus changes the form with no code change.
    maxCampuses: campuses.length || undefined,
    getCampusProcessing: () => {
      if (!isAuthenticated) return [];
      // A processing intent is only ever valid on a SELF-registration: on a delegated submission the
      // backend refuses the whole payload, so stale choices must never leave the browser. The panel is
      // hidden and the state cleared when the email changes, and this is the last gate before the wire.
      if (!isSameEmailIdentity(user?.email, form.getValues('registerInfo.email'))) return [];
      const selected = new Set(
        form.getValues('campusVisits').map(cv => (cv.campus || '').toUpperCase()).filter(Boolean),
      );
      return Object.values(campusProcessingRef.current).filter(p => selected.has(p.campusId));
    },
  });
  const { form, campusVisitFields } = vm;

  // Look for a saved draft once, but never apply it silently — the user is offered the choice.
  const hydratedRef = useRef(false);
  useEffect(() => {
    if (hydratedRef.current) return;
    hydratedRef.current = true;
    vm.detectDraft();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
    cardRefs.current.get(cv.clientKey)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    vm.setFirstErrorCampusIndex(null);
  }, [vm.firstErrorCampusIndex, form, vm]);

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
  const cpErr = errors.contactPoint;

  // One card per campus open for registration — the ceiling and the "already taken" set both come
  // from live data, so a campus added or retired in the backend is reflected without a code change.
  const watchedCampusVisits = form.watch('campusVisits');
  const campusLimit = campuses.length > 0
    ? Math.min(campuses.length, V2_MAX_CAMPUSES)
    : V2_MAX_CAMPUSES;
  const takenCampusCodes = (watchedCampusVisits ?? [])
    .map(cv => (cv.campus || '').toUpperCase())
    .filter(Boolean);

  const { saveDraftNow, discardDraft } = vm;
  useEffect(() => {
    onDraftControls?.({ saveDraftNow, discardDraft, isDirty: () => hasMeaningfulV2Data(form.getValues()) });
  }, [onDraftControls, saveDraftNow, discardDraft, form]);

  const submitBar = (node: React.ReactNode) =>
    footerSlot ? createPortal(node, footerSlot) : node;

  const syncContactFromRegistrant = () => {
    const reg = form.getValues('registerInfo');
    form.setValue(
      'contactPoint',
      { fullName: reg.fullName, organization: reg.organization, phone: reg.phone, email: reg.email },
      { shouldValidate: true, shouldDirty: true },
    );
  };

  const watchedReg = form.watch('registerInfo');
  const isRegInfoEmpty = !watchedReg?.fullName?.trim() && !watchedReg?.organization?.trim() && !watchedReg?.phone?.trim() && !watchedReg?.email?.trim();

  // ── Registrant identity (authenticated only) ──
  // Recomputed from the WATCHED email, so editing the field flips the banner, the submit contract and
  // the campus processing panel in the same render — there is no stale "verified" state to leak.
  const isInternalActor = creatorRole === 'STAFF' || creatorRole === 'STAFF_LEADER';
  const isSelfRegistrant = isAuthenticated && isSameEmailIdentity(user?.email, watchedReg?.email);

  const [autofillState, setAutofillState] = useState<'idle' | 'loading' | 'error'>('idle');

  /**
   * Fills the registrant block from the signed-in user's own profile. Explicitly user-triggered:
   * pre-filling on mount would silently overwrite a restored draft, which is exactly the behaviour
   * that loses typed work. Fields the profile has no value for are left blank for the user to add
   * rather than being padded with a role label.
   */
  const fillRegistrantFromProfile = async () => {
    setAutofillState('loading');
    try {
      const me = await profileApi.getMyProfile();
      form.setValue('registerInfo', {
        fullName: me.fullName ?? '',
        email: me.email ?? '',
        phone: me.phone ?? '',
        nationality: me.nationality ?? '',
        jobTitle: me.displayPosition ?? '',
        organization: me.displayDepartmentName ?? me.department?.name ?? me.displayCampusName ?? '',
      }, { shouldValidate: true, shouldDirty: true });
      setAutofillState('idle');
    } catch (err) {
      setAutofillState('error');
      vm.setSubmitError(getApiErrorMessage(err, t('visitRequestV2:registrant.autofillFailed')));
    }
  };

  // Choices made while the form named the signed-in user must not survive that name changing:
  // once this is a delegated submission the whole payload would be rejected for carrying them.
  const wasSelfRegistrantRef = useRef(isSelfRegistrant);
  useEffect(() => {
    if (wasSelfRegistrantRef.current && !isSelfRegistrant) {
      setCampusProcessing(prev => (Object.keys(prev).length === 0 ? prev : {}));
    }
    wasSelfRegistrantRef.current = isSelfRegistrant;
  }, [isSelfRegistrant]);

  return (
    <form id="visit-request-v2-form" onSubmit={vm.onSubmit} noValidate className="space-y-2">
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
        <div role="status" className="rounded-xl border border-blue-200 bg-blue-50 p-3 text-sm font-medium text-blue-800">
          {t('visitRequestV2:draft.migrated')}
        </div>
      )}

      {/* ── Request-level: registrant ── */}
      <FormSection
        id="v2-registrant"
        title={t('visitRequestV2:sections.registrant')}
        headerRight={isAuthenticated ? (
          <button
            type="button"
            data-testid="v2-registrant-use-me"
            disabled={autofillState === 'loading'}
            onClick={() => void fillRegistrantFromProfile()}
            className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-semibold text-[#004c91] hover:bg-[#004c91]/5 disabled:opacity-40 disabled:cursor-not-allowed"
          >
            {autofillState === 'loading'
              ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
              : <UserRound className="h-4 w-4" aria-hidden />}
            {t('visitRequestV2:registrant.useMyProfile')}
          </button>
        ) : undefined}
      >
        {/* Which submit contract this form is on, in the user's words — shown BEFORE they submit so the
            OTP round-trip is never a surprise. Driven by the watched email, so it flips as they type. */}
        {isAuthenticated && (
          <div
            role="status"
            data-testid={isSelfRegistrant ? 'v2-registrant-self' : 'v2-registrant-delegated'}
            className={`mb-4 flex items-start gap-2 rounded-xl border p-3 text-sm font-medium ${
              isSelfRegistrant
                ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
                : 'border-amber-200 bg-amber-50 text-amber-800'
            }`}
          >
            {isSelfRegistrant
              ? <BadgeCheck className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
              : <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />}
            <span>
              {isSelfRegistrant
                ? t('visitRequestV2:registrant.matchesAccount')
                : t('visitRequestV2:registrant.requiresOtp')}
            </span>
          </div>
        )}
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 lg:grid-cols-2">
          <FormField label={t('visitRequestV2:registrant.fullName')} required error={regErr?.fullName?.message} showValidIcon={false}>
            <input data-testid="v2-registrant-fullName" {...register('registerInfo.fullName')} className={inputCls(!!regErr?.fullName, false, false)} />
          </FormField>
          {/* Free-solo partner/organization search: picking a known partner links partnerId,
              typing anything else keeps the text as a manually entered organization. */}
          <FormField label={t('visitRequestV2:registrant.organization')} required error={regErr?.organization?.message} showValidIcon={false}>
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
                    field.onChange(next.organization);
                    form.setValue('partnerId', next.partnerId, { shouldDirty: true });
                    form.setValue('partnerSelectionMode', next.mode, { shouldDirty: true });
                  }}
                />
              )}
            />
          </FormField>
          <FormField label={t('visitRequestV2:registrant.jobTitle')} required error={regErr?.jobTitle?.message} showValidIcon={false}>
            <input data-testid="v2-registrant-jobTitle" {...register('registerInfo.jobTitle')} className={inputCls(!!regErr?.jobTitle, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:registrant.nationality')} required error={regErr?.nationality?.message} showValidIcon={false}>
            <Controller
              name="registerInfo.nationality"
              control={form.control}
              render={({ field }) => (
                <CountrySelect
                  value={field.value ?? ''}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  hasError={!!regErr?.nationality}
                  placeholder={t('visitRequestV2:registrant.nationality')}
                />
              )}
            />
          </FormField>
          <FormField label={t('visitRequestV2:card.phone')} required error={regErr?.phone?.message} showValidIcon={false}>
            <input data-testid="v2-registrant-phone" {...register('registerInfo.phone')} placeholder="+84…" className={inputCls(!!regErr?.phone, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:card.email')} required error={regErr?.email?.message} showValidIcon={false}>
            <input type="email" data-testid="v2-registrant-email" {...register('registerInfo.email')} className={inputCls(!!regErr?.email, false, false)} />
          </FormField>
        </div>
      </FormSection>

      {/* ── Request-level: primary contact (the request manager / VISITOR login) ── */}
      <FormSection
        id="v2-contact"
        title={t('visitRequestV2:sections.contact')}
        description={t('visitRequestV2:sections.contactDesc')}
        headerRight={
          <button
            type="button"
            data-testid="v2-contact-same-as-registrant"
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed"
            // An internal member of staff can never be the delegation's contact — the backend rejects it
            // with INTERNAL_REGISTRANT_CANNOT_BE_CONTACT. Copying their own details in is therefore a dead
            // end, so the affordance is removed instead of letting them fill the block and fail on submit.
            disabled={isRegInfoEmpty || (isInternalActor && isSelfRegistrant)}
            onClick={syncContactFromRegistrant}
          >
            {t('visitRequestV2:sections.contactSameAsRegistrant')}
          </button>
        }
      >
        {isInternalActor && isSelfRegistrant && (
          <p className="mb-4 rounded-xl border border-slate-200 bg-slate-50 p-3 text-sm font-medium text-slate-600">
            {t('visitRequestV2:sections.contactInternalNotAllowed')}
          </p>
        )}
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 lg:grid-cols-2">
          <FormField label={t('visitRequestV2:person.fullName')} required error={cpErr?.fullName?.message} showValidIcon={false}>
            <input {...register('contactPoint.fullName')} className={inputCls(!!cpErr?.fullName, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:person.organization')} required error={cpErr?.organization?.message} showValidIcon={false}>
            <input {...register('contactPoint.organization')} className={inputCls(!!cpErr?.organization, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:card.phone')} required error={cpErr?.phone?.message} showValidIcon={false}>
            <input {...register('contactPoint.phone')} placeholder="+84…" className={inputCls(!!cpErr?.phone, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:card.email')} required error={cpErr?.email?.message} showValidIcon={false}>
            <input type="email" {...register('contactPoint.email')} className={inputCls(!!cpErr?.email, false, false)} />
          </FormField>
        </div>
      </FormSection>

      {/* ── Per-campus cards ── */}
      <FormSection
        id="v2-campuses"
        title={t('visitRequestV2:sections.campuses')}
        description={t('visitRequestV2:sections.campusesDesc')}
      >
        {typeof errors.campusVisits?.message === 'string' && (
          <p className="mb-3 text-sm font-semibold text-red-600" role="alert">{errors.campusVisits.message}</p>
        )}
        <div className="space-y-4">
          {campusVisitFields.fields.map((field, index) => {
            const clientKey = form.getValues(`campusVisits.${index}.clientKey`) || (field as any).clientKey || field.id;
            return (
              <div key={field.id} ref={el => { cardRefs.current.set(clientKey, el); }}>
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
                  // Only a SELF-registration may state how a campus is processed: on a delegated
                  // submission the OTP verifies the registrant, not the person typing, so every campus
                  // routes to its Staff Leader and the backend rejects any intent to the contrary.
                  processing={isAuthenticated && isSelfRegistrant ? {
                    role: creatorRole,
                    ownCampusCode: user?.campusCode,
                    values: campusProcessing,
                    onChange: next => setCampusProcessing(prev => ({ ...prev, [next.campusId]: next })),
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

      {/* ── Submit ──
          When the host supplies a footer node (the modal shell), the actions are portalled into
          it so they can be sticky while the body scrolls. The portal keeps them inside THIS
          <form>, so type="submit" still works and there is no second form implementation. */}
      {submitBar(
        <>
          {vm.submitError && (
            <div role="alert" className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-700">
              <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>{vm.submitError}</span>
            </div>
          )}
          <div className="flex justify-end pt-4">
            <button
              type="submit"
              form="visit-request-v2-form"
              data-testid="v2-submit"
              disabled={vm.isSubmitting}
              className="inline-flex items-center gap-2 rounded-xl bg-[#f37021] px-6 py-3 text-sm font-bold text-white shadow hover:bg-[#e0631a] disabled:opacity-60"
            >
              {vm.isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
              {/* The label states the contract the form is actually on: a delegated submission ends in
                  an OTP round-trip, so it must not promise an immediate create. */}
              {isAuthenticated && isSelfRegistrant
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
      {vm.sessionToken && (
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
        />
      )}
    </form>
  );
};
