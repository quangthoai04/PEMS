import React, { useCallback, useEffect, useRef, useState } from 'react';
import { AlertCircle, Loader2, Plus, Send } from 'lucide-react';
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
import { CampusVisitCard } from './CampusVisitCard';
import { FormField, inputCls } from '../shared/FormField';
import { FormSection } from '../shared/FormSection';
import { OtpVerificationModal } from '../OtpVerificationModal';
import type { CreatorRole } from '../sections/CampusProcessingSection';
import type { CampusProcessingChoice } from '../../api/visitRequestApi';
import { useAuthContext } from '../../../../shared/auth/AuthContext';

interface Props {
  mode: UseVisitRequestFormV2Options['mode'];
  draftNamespace?: string;
  onSuccess: (result: V2CreateResponse, values: VisitRequestV2Schema) => void;
}

/**
 * Per-campus form v2 (plan §9.1): request-level registrant + primary contact once, then one
 * complete snapshot card per campus. Every campus is independent — copying is an explicit,
 * confirmed one-time deep clone, and the submit payload is the REAL v2 contract (the backend
 * derives scope/mixed state; nothing here is a mock and there is no silent v1 fallback).
 */
export const VisitRequestFormV2: React.FC<Props> = ({ mode, draftNamespace, onSuccess }) => {
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
    getCampusProcessing: () => {
      if (!isAuthenticated) return [];
      const selected = new Set(
        form.getValues('campusVisits').map(cv => (cv.campus || '').toUpperCase()).filter(Boolean),
      );
      return Object.values(campusProcessingRef.current).filter(p => selected.has(p.campusId));
    },
  });
  const { form, campusVisitFields } = vm;

  // Hydrate the draft once (per-campus draft, or a one-time migration of the global draft).
  const hydratedRef = useRef(false);
  useEffect(() => {
    if (hydratedRef.current) return;
    hydratedRef.current = true;
    vm.hydrateDraft();
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

  const syncContactFromRegistrant = () => {
    const reg = form.getValues('registerInfo');
    form.setValue(
      'contactPoint',
      { fullName: reg.fullName, organization: reg.organization, phone: reg.phone, email: reg.email },
      { shouldValidate: true, shouldDirty: true },
    );
  };

  return (
    <form onSubmit={vm.onSubmit} noValidate className="space-y-2">
      {vm.migratedFromGlobalDraft && (
        <div role="status" className="rounded-xl border border-blue-200 bg-blue-50 p-3 text-sm font-medium text-blue-800">
          {t('visitRequestV2:draft.migrated')}
        </div>
      )}

      {/* ── Request-level: registrant ── */}
      <FormSection id="v2-registrant" title={t('visitRequestV2:sections.registrant')}>
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 lg:grid-cols-2">
          <FormField label={t('visitRequestV2:registrant.fullName')} required error={regErr?.fullName?.message} showValidIcon={false}>
            <input {...register('registerInfo.fullName')} className={inputCls(!!regErr?.fullName, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:registrant.organization')} required error={regErr?.organization?.message} showValidIcon={false}>
            <input {...register('registerInfo.organization')} className={inputCls(!!regErr?.organization, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:registrant.jobTitle')} required error={regErr?.jobTitle?.message} showValidIcon={false}>
            <input {...register('registerInfo.jobTitle')} className={inputCls(!!regErr?.jobTitle, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:registrant.nationality')} required error={regErr?.nationality?.message} showValidIcon={false}>
            <input {...register('registerInfo.nationality')} className={inputCls(!!regErr?.nationality, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:card.phone')} required error={regErr?.phone?.message} showValidIcon={false}>
            <input {...register('registerInfo.phone')} placeholder="+84…" className={inputCls(!!regErr?.phone, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:card.email')} required error={regErr?.email?.message} showValidIcon={false}>
            <input type="email" {...register('registerInfo.email')} className={inputCls(!!regErr?.email, false, false)} />
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
            className="rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-semibold text-slate-700 hover:bg-slate-50"
            onClick={syncContactFromRegistrant}
          >
            {t('visitRequestV2:sections.contactSameAsRegistrant')}
          </button>
        }
      >
        <div className="grid grid-cols-1 gap-x-8 gap-y-5 lg:grid-cols-2">
          <FormField label={t('visitRequestV2:person.fullName')} required error={cpErr?.fullName?.message} showValidIcon={false}>
            <input {...register('contactPoint.fullName')} className={inputCls(!!cpErr?.fullName, false, false)} />
          </FormField>
          <FormField label={t('visitRequestV2:person.organization')} error={cpErr?.organization?.message} showValidIcon={false}>
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
            const clientKey = form.getValues(`campusVisits.${index}.clientKey`) || field.id;
            return (
              <div key={clientKey} ref={el => { cardRefs.current.set(clientKey, el); }}>
                <CampusVisitCard
                  form={form}
                  index={index}
                  clientKey={clientKey}
                  open={openKeys.has(clientKey)}
                  onToggle={() => toggleCard(clientKey)}
                  campuses={campuses}
                  campusesLoading={campusesLoading}
                  copySources={campusVisitFields.fields
                    .map((_, i) => i)
                    .filter(i => i !== index)
                    .map(i => ({ index: i, label: campusLabel(form.getValues('campusVisits')[i], i) }))}
                  onCopyFrom={source => vm.copyContentIntoCampus(index, source)}
                  onApplyToAll={() => vm.requestApplyToAll(index, campusLabel)}
                  onRemove={() => requestRemove(index)}
                  canRemove={campusVisitFields.fields.length > 1}
                  showErrors={showErrors}
                  processing={isAuthenticated ? {
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
          disabled={campusVisitFields.fields.length >= V2_MAX_CAMPUSES}
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
          {t('visitRequestV2:card.addCampus', { count: campusVisitFields.fields.length, max: V2_MAX_CAMPUSES })}
        </button>
      </FormSection>

      {/* ── Submit ── */}
      {vm.submitError && (
        <div role="alert" className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-700">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>{vm.submitError}</span>
        </div>
      )}
      <div className="flex justify-end pt-4">
        <button
          type="submit"
          disabled={vm.isSubmitting}
          className="inline-flex items-center gap-2 rounded-xl bg-[#f37021] px-6 py-3 text-sm font-bold text-white shadow hover:bg-[#e0631a] disabled:opacity-60"
        >
          {vm.isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
          {mode === 'authenticated' ? t('visitRequestV2:submit.authenticated') : t('visitRequestV2:submit.public')}
        </button>
      </div>

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

      {/* ── Public OTP (v2 create happens at verify; see hook) ── */}
      {mode !== 'authenticated' && vm.sessionToken && (
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
