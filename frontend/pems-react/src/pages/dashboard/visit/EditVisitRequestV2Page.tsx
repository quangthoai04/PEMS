import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { Controller, useForm, useFieldArray, type FieldPath } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { AlertCircle, ArrowLeft, Loader2, RefreshCw, Send } from 'lucide-react';
import axios from 'axios';
import { useTranslation } from 'react-i18next';
import {
  buildVisitRequestV2Schema,
  V2_MAX_CAMPUSES,
  V2_MIN_ADVANCE_HOURS_EDIT,
  type VisitRequestV2Schema,
} from '../../../features/visit-request/schema/visitRequestV2.schema';
import {
  getVisitRequestFormV2,
  updatePendingVisitRequestV2,
  resubmitVisitRequestV2,
  type ResolvedVisitForm,
} from '../../../features/visit-request/api/visitRequestV2Api';
import {
  applyContentToAllCampuses,
  buildV2EditPayload,
  cloneCampusVisitContent,
  listOverwrittenCampuses,
  mapServerFieldPathToFormPath,
  resolvedFormToV2Schema,
} from '../../../features/visit-request/utils/visitRequestV2Form';
import { CampusVisitCard } from '../../../features/visit-request/components/v2/CampusVisitCard';
import { ContactLinkPromptDialog } from '../../../features/visit-request/components/v2/ContactLinkPromptDialog';
import { useContactLinkPrompt } from '../../../features/visit-request/hooks/useContactLinkPrompt';
import { FormField, inputCls } from '../../../features/visit-request/components/shared/FormField';
import { PhoneField } from '../../../features/visit-request/components/shared/PhoneField';
import { PartnerOrgCombobox } from '../../../features/visit-request/components/shared/PartnerOrgCombobox';
import { CountrySelect } from '../../../features/visit-request/components/shared/CountrySelect';
import { FormSection } from '../../../features/visit-request/components/shared/FormSection';
import { useRegistrationCampuses } from '../../../features/visit-request/hooks/useRegistrationCampuses';
import { getApiErrorMessage } from '../../../shared/utils/toast';
import { commitFieldValue } from '../../../shared/utils/formRevalidate';

type Mode = 'edit' | 'resubmit';

/**
 * These request statuses accept a pending-edit (fully-pending) vs a resubmit (fully-rejected).
 * Both pre-decision stages qualify for edit — PENDING_CONTACT_CONFIRMATION (campuses still waiting
 * for their operational contact) and PENDING_APPROVAL (contacts confirmed, waiting on Staff Leader) —
 * mirroring UpdatePendingVisitRequestV2CommandHandler's own gate (VisitRequestConstants.cs), which is
 * the actual authority on this. `PENDING` is a legacy alias kept only because no audited caller/fixture
 * proved it dead; VisitRequestStatuses (backend) no longer emits it.
 */
const EDITABLE_STATUSES = new Set(['PENDING_CONTACT_CONFIRMATION', 'PENDING_APPROVAL', 'PENDING']);
const RESUBMITTABLE_STATUSES = new Set(['REJECTED']);

/**
 * Per-campus v2 pending-edit / rejected-resubmit screen. Reuses the SAME v2 form model
 * (`VisitRequestV2Schema`), the SAME `CampusVisitCard`, and the SAME utilities as create — no third
 * form model. It hydrates from the scoped read model (so hidden campuses never appear), carries the
 * stable `visitInstanceId` + per-instance/request `rowVersion` for optimistic concurrency, and lets the
 * BACKEND be the authority on editor identity, lifecycle and campus routing (a 409 shows a stable message
 * and offers a reload). Resubmit keeps the campus set fixed; pending-edit may add/remove campuses.
 */
export default function EditVisitRequestV2Page({ mode }: { mode: Mode }) {
  const { visitRequestId } = useParams<{ visitRequestId: string }>();
  const id = Number(visitRequestId);
  const navigate = useNavigate();
  const { t, i18n } = useTranslation(['visitRequestV2', 'validation']);
  const { campuses, loading: campusesLoading } = useRegistrationCampuses();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [statusMismatch, setStatusMismatch] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showErrors, setShowErrors] = useState(false);
  const [openKeys, setOpenKeys] = useState<Set<string>>(new Set());
  const [applyPrompt, setApplyPrompt] = useState<{ sourceIndex: number; overwritten: string[] } | null>(null);
  const requestRowVersionRef = useRef<number>(0);
  const cardRefs = useRef(new Map<string, HTMLDivElement | null>());

  /**
   * Keyed by clientKey, bumped whenever a copy/apply-to-all overwrites that card's content.
   * `useFieldArray.update()`/`.replace()` patch the underlying form values correctly, but they
   * skip resyncing `register()`-bound inputs and any NESTED `useFieldArray` (visitors/supportTeam
   * live inside `CampusVisitCard`, registered under their own name) — those keep showing their
   * pre-copy state until something else touches them. Folding this into the card's React `key`
   * forces a full remount, which is the only way those nested hooks re-read the fresh values.
   * Mirrors useVisitRequestFormV2's cardVersion (create mode) — this screen has its own local
   * copy/apply-to-all handlers instead of that hook, so it needs its own copy of the mechanism.
   */
  const [cardVersion, setCardVersion] = useState<Record<string, number>>({});
  const bumpCardVersion = useCallback((clientKey: string) => {
    setCardVersion(prev => ({ ...prev, [clientKey]: (prev[clientKey] ?? 0) + 1 }));
  }, []);

  /**
   * How many campuses this request may hold: however many are currently open for registration, exactly
   * as create computes it (`V2_MAX_CAMPUSES` is only the payload backstop). It used to be a hard-coded
   * 10 here, so a five-campus university offered "Thêm cơ sở (2/10)" and let the user add rows for
   * campuses that do not exist. Falls back to the backstop only while the list is still loading.
   */
  const campusLimit = campuses.length > 0
    ? Math.min(campuses.length, V2_MAX_CAMPUSES)
    : V2_MAX_CAMPUSES;

  const schema = useMemo(
    () => buildVisitRequestV2Schema(
      V2_MIN_ADVANCE_HOURS_EDIT,
      (key, opts) => t(key, { ns: 'validation', ...opts }),
      campusLimit,
    ),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, i18n.language, campusLimit],
  );

  const form = useForm<VisitRequestV2Schema>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(schema) as any,
    mode: 'onBlur',
    reValidateMode: 'onChange',
  });
  const campusVisitFields = useFieldArray({ control: form.control, name: 'campusVisits' });

  const hydrate = useCallback((data: ResolvedVisitForm) => {
    const { values, expectedRequestRowVersion } = resolvedFormToV2Schema(data);
    requestRowVersionRef.current = expectedRequestRowVersion;
    form.reset(values);
    setOpenKeys(new Set(values.campusVisits.length ? [values.campusVisits[0].clientKey] : []));
  }, [form]);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    setStatusMismatch(false);
    try {
      const data = await getVisitRequestFormV2(id);
      const editableForMode =
        mode === 'edit' ? EDITABLE_STATUSES.has(data.requestStatus) : RESUBMITTABLE_STATUSES.has(data.requestStatus);
      // Backend write policy (UpdatePendingVisitRequestV2CommandHandler / ResubmitRejectedVisitRequestV2CommandHandler)
      // authorizes REGISTRANT alone. VISITOR_OWNER is a legacy relation string
      // (VisitInstanceAccess.cs: "replaces the old request-wide VISITOR_OWNER") that this endpoint's
      // read model (VisitFormReadService.ComputeScopeAsync) never actually returns — kept here as a
      // harmless no-op rather than pruned, since removing it is outside this fix's scope.
      const isManager = data.viewer.relation === 'REGISTRANT' || data.viewer.relation === 'VISITOR_OWNER';
      if (!isManager) {
        setLoadError(t('visitRequestV2:edit.forbidden'));
      } else if (!editableForMode) {
        setStatusMismatch(true);
      } else {
        hydrate(data);
      }
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 404) setLoadError(t('visitRequestV2:detail.notfound'));
      else setLoadError(getApiErrorMessage(err, t('visitRequestV2:detail.generic')));
    } finally {
      setLoading(false);
    }
  }, [id, mode, hydrate, t]);

  useEffect(() => {
    if (Number.isFinite(id) && id > 0) void load();
    else { setLoading(false); setLoadError(t('visitRequestV2:detail.notfound')); }
  }, [id, load, t]);

  const campusLabel = useCallback(
    (cv: VisitRequestV2Schema['campusVisits'][number], index: number): string =>
      campuses.find(c => c.campusCode === cv.campus)?.campusName ?? t('visitRequestV2:card.cardN', { n: index + 1 }),
    [campuses, t],
  );

  const toggleCard = (key: string) =>
    setOpenKeys(prev => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });

  const copyInto = (targetIndex: number, sourceIndex: number) => {
    const current = form.getValues('campusVisits');
    const source = current[sourceIndex];
    const target = current[targetIndex];
    if (source && target && sourceIndex !== targetIndex) {
      campusVisitFields.update(targetIndex, cloneCampusVisitContent(source, target));
      bumpCardVersion(target.clientKey);
    }
  };

  const requestApplyToAll = (sourceIndex: number) => {
    const current = form.getValues('campusVisits');
    if (current.length < 2) return;
    setApplyPrompt({ sourceIndex, overwritten: listOverwrittenCampuses(current, sourceIndex, campusLabel) });
  };

  const applyServerErrors = (err: unknown): boolean => {
    if (!axios.isAxiosError(err)) return false;
    const errors = (err.response?.data as { errors?: Record<string, string[]> } | undefined)?.errors;
    if (!errors) return false;
    let firstCampus: string | null = null;
    let mapped = false;
    for (const [serverPath, messages] of Object.entries(errors)) {
      const formPath = mapServerFieldPathToFormPath(serverPath);
      if (!formPath || !messages?.length) continue;
      form.setError(formPath as FieldPath<VisitRequestV2Schema>, { type: 'server', message: messages[0] });
      mapped = true;
      const m = /^campusVisits\.(\d+)\./.exec(formPath);
      if (m && firstCampus === null) firstCampus = form.getValues('campusVisits')[Number(m[1])]?.clientKey ?? null;
    }
    if (firstCampus) {
      setOpenKeys(prev => new Set(prev).add(firstCampus!));
      cardRefs.current.get(firstCampus)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
    return mapped;
  };

  // The same question the create form asks, on the screen that could also retype the contact block
  // and asked nothing. Without it, identical typing linked the contact to a delegation member on one
  // screen and left the request naming two people on the other.
  const resumeSubmitRef = useRef<() => void>(() => {});
  const contactLink = useContactLinkPrompt(form, () => resumeSubmitRef.current());

  const onSubmit = form.handleSubmit(
    async data => {
      if (contactLink.interrupts(data)) return;
      setIsSubmitting(true);
      setSubmitError(null);
      setConflict(false);
      try {
        const payload = buildV2EditPayload(data, requestRowVersionRef.current);
        const res = mode === 'edit'
          ? await updatePendingVisitRequestV2(id, payload)
          : await resubmitVisitRequestV2(id, payload);
        // Success → refresh the row version and leave to the detail screen.
        navigate(`/dashboard/visit/v2/${id}`, { replace: true, state: { flash: res.message } });
      } catch (err) {
        const status = axios.isAxiosError(err) ? err.response?.status : undefined;
        const code = axios.isAxiosError(err)
          ? (err.response?.data as { errorCode?: string } | undefined)?.errorCode
          : undefined;
        if (status === 409 || code === 'VISIT_REQUEST_VERSION_CONFLICT' || code === 'VISIT_INSTANCE_VERSION_CONFLICT') {
          setConflict(true);
          setSubmitError(t('visitRequestV2:edit.conflict'));
        } else if (!applyServerErrors(err)) {
          setSubmitError(getApiErrorMessage(err, t('visitRequestV2:edit.submitFailed')));
        }
      } finally {
        setIsSubmitting(false);
      }
    },
    errors => {
      setShowErrors(true);
      const campusErrors = (errors as { campusVisits?: unknown[] }).campusVisits;
      if (Array.isArray(campusErrors)) {
        const idx = campusErrors.findIndex(e => e != null);
        const key = idx >= 0 ? form.getValues('campusVisits')[idx]?.clientKey : undefined;
        if (key) { setOpenKeys(prev => new Set(prev).add(key)); cardRefs.current.get(key)?.scrollIntoView({ behavior: 'smooth', block: 'start' }); }
      }
    },
  );

  // Kept current through a ref: the prompt hook is created before this handler exists, and an answer
  // has to resume THIS submit rather than a closure from an earlier render.
  useEffect(() => {
    resumeSubmitRef.current = () => { void onSubmit(); };
  });

  if (loading) {
    return (
      <p role="status" className="flex items-center gap-2 p-6 text-sm text-slate-500">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> {t('visitRequestV2:detail.loading')}
      </p>
    );
  }

  const back = (
    <Link to={`/dashboard/visit/v2/${id}`} className="inline-flex items-center gap-1.5 text-sm font-semibold text-[#004c91] hover:underline">
      <ArrowLeft className="h-4 w-4" aria-hidden /> {t('visitRequestV2:detail.backToList')}
    </Link>
  );

  if (loadError) {
    return <div className="mx-auto max-w-3xl space-y-4 p-6">{back}<p role="alert" className="text-sm text-red-600">{loadError}</p></div>;
  }
  if (statusMismatch) {
    return (
      <div className="mx-auto max-w-3xl space-y-4 p-6">
        {back}
        <div role="alert" className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          <p>{mode === 'edit' ? t('visitRequestV2:edit.notEditable') : t('visitRequestV2:edit.notResubmittable')}</p>
        </div>
      </div>
    );
  }

  const { register, formState: { errors } } = form;
  const regErr = errors.registerInfo;
  // Campus CODEs already spoken for, so a campus cannot be picked twice in one request (the schema
  // rejects duplicates too — this keeps the user from choosing one in the first place).
  const takenCampusCodes = (form.watch('campusVisits') ?? [])
    .map(cv => (cv.campus || '').toUpperCase())
    .filter(Boolean);

  return (
    <div className="mx-auto max-w-7xl space-y-4 p-4 sm:p-6">
      {back}
      <header>
        <h1 className="text-2xl font-extrabold text-[#004c91]">
          {mode === 'edit' ? t('visitRequestV2:edit.titleEdit') : t('visitRequestV2:edit.titleResubmit')}
        </h1>
        <p className="mt-1 text-sm text-slate-600">{t('visitRequestV2:edit.subtitle')}</p>
      </header>

      {contactLink.prompt && (
        <ContactLinkPromptDialog
          prompt={contactLink.prompt}
          onSame={contactLink.confirmSame}
          onDifferent={contactLink.confirmDifferent}
          onReview={contactLink.dismiss}
        />
      )}

      <form onSubmit={onSubmit} noValidate className="space-y-2">
        <FormSection id="v2e-registrant" title={t('visitRequestV2:sections.registrant')}>
          <div className="grid grid-cols-1 gap-x-8 gap-y-5 lg:grid-cols-2">
            <FormField label={t('visitRequestV2:registrant.fullName')} required error={regErr?.fullName?.message} showValidIcon={false}>
              <input {...register('registerInfo.fullName')} className={inputCls(!!regErr?.fullName, false, false)} />
            </FormField>
            {/* Same shared control as Create — free-solo partner/organization search. PartnerId stays
                IMMUTABLE on edit (backend: IMMUTABLE_REGISTRANT_PARTNER), but the ORGANIZATION TEXT is
                still a correctable snapshot (see `Registrant_snapshot_fields_are_editable_and_do_not_
                touch_the_account`). A plain `<input>` here used to edit that text with zero connection
                to `partnerId` — so retyping it while an existing partner link stayed on the request
                silently left the two out of sync (text says one organization, partnerId still points at
                another). This control keeps them atomic: typing free text clears partnerId, exactly as
                Create does, and the backend still refuses the request outright if partnerId itself ends
                up different from what the request already has. */}
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
                      commitFieldValue(
                        form, 'registerInfo.organization', next.organization, field.onChange);
                      form.setValue('partnerId', next.partnerId, { shouldDirty: true });
                      form.setValue('partnerSelectionMode', next.mode, { shouldDirty: true });
                    }}
                  />
                )}
              />
            </FormField>
            <FormField label={t('visitRequestV2:registrant.jobTitle')} required error={regErr?.jobTitle?.message} showValidIcon={false}>
              <input {...register('registerInfo.jobTitle')} className={inputCls(!!regErr?.jobTitle, false, false)} />
            </FormField>
            {/* Patch 4 (nationality contract): was a plain <input> — the only registrant nationality
                surface in the app that was not already a country picker (Create and Safe Edit both
                use CountrySelect). `strict` matches those: no free-text "create new" option, since
                the backend now resolves-or-rejects every genuinely CHANGED value to a real country
                canonical form (an untouched legacy value round-trips exactly as it was). */}
            <FormField label={t('visitRequestV2:registrant.nationality')} required error={regErr?.nationality?.message} showValidIcon={false}>
              <Controller
                name="registerInfo.nationality"
                control={form.control}
                render={({ field }) => (
                  <CountrySelect
                    strict
                    value={field.value ?? ''}
                    onChange={value => commitFieldValue(form, 'registerInfo.nationality', value, field.onChange)}
                    onBlur={field.onBlur}
                    hasError={!!regErr?.nationality}
                    placeholder={t('visitRequestV2:registrant.nationality')}
                  />
                )}
              />
            </FormField>
            {/* Phone is OPTIONAL — matches Create and Safe Edit. Blank passes registerInfo.schema's
                buildPhoneSchema(); this field must never carry the `required` marker. */}
            <FormField label={t('visitRequestV2:card.phone')} error={regErr?.phone?.message} showValidIcon={false}>
              <PhoneField
                field={register('registerInfo.phone')}
                hasError={!!regErr?.phone}
                error={regErr?.phone?.message}
                testId="v2e-registrant-phone"
              />
            </FormField>
            <FormField label={t('visitRequestV2:card.email')} required error={regErr?.email?.message} showValidIcon={false} subtitle={t('visitRequestV2:edit.emailImmutable')}>
              <input type="email" readOnly aria-readonly {...register('registerInfo.email')} className={`${inputCls(!!regErr?.email, false, false)} bg-slate-50`} />
            </FormField>
          </div>
        </FormSection>


        {/* The campus set is fixed from the moment the request exists — for editing AND for
            resubmitting. Adding one is a new request; dropping one is a cancellation of that campus,
            which is its own workflow with its own notifications. The backend refuses a payload whose
            campus set differs from the stored one, so hiding these controls is the UI agreeing with
            the rule rather than the rule itself. */}
        <FormSection id="v2e-campuses" title={t('visitRequestV2:sections.campuses')} description={t('visitRequestV2:edit.campusSetFixed')}>
          <div className="space-y-4">
            {campusVisitFields.fields.map((field, index) => {
              const clientKey = form.getValues(`campusVisits.${index}.clientKey`) || field.id;
              // An EXISTING campus already has an operational contact, and none of that contact's five
              // fields is this form's to write: the backend refuses all of them here, and managing them
              // — including handing the campus to a different address, which the new person must accept
              // — belongs to the detail screen. Shown read-only, offered nowhere. A campus being ADDED
              // has no contact yet, so naming one is part of adding it.
              const instanceId = form.getValues(`campusVisits.${index}.visitInstanceId`) ?? null;
              // A copy/apply-to-all patches this card's form values correctly, but register()-bound
              // inputs and the nested visitors/supportTeam field arrays only re-read fresh values on
              // mount — folding the bump counter into the key forces that remount (cardVersion).
              const renderKey = `${clientKey}:${cardVersion[clientKey] ?? 0}`;
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
                    contactReadOnly={instanceId != null}
                    copySources={campusVisitFields.fields
                      .map((_, i) => i)
                      .filter(i => i !== index)
                      .map(i => ({ index: i, label: campusLabel(form.getValues('campusVisits')[i], i) }))}
                    onCopyFrom={source => copyInto(index, source)}
                    onApplyToAll={() => requestApplyToAll(index)}
                    onRemove={() => {}}
                    canRemove={false}
                    showErrors={showErrors}
                    // The same floor the schema above was built with — and the same one create uses,
                    // so the picker, the resolver and the backend cannot disagree.
                    minAdvanceHours={V2_MIN_ADVANCE_HOURS_EDIT}
                  />
                </div>
              );
            })}
          </div>
        </FormSection>

        {submitError && (
          <div role="alert" className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-normal text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <div>
              <p>{submitError}</p>
              {conflict && (
                <button type="button" className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-red-300 px-3 py-1.5 text-xs font-bold hover:bg-red-100" onClick={() => void load()}>
                  <RefreshCw className="h-3.5 w-3.5" /> {t('visitRequestV2:edit.reload')}
                </button>
              )}
            </div>
          </div>
        )}

        <div className="flex justify-end pt-4">
          <button type="submit" data-testid="v2-edit-submit" disabled={isSubmitting} className="inline-flex items-center gap-2 rounded-xl bg-[#f37021] px-6 py-3 text-sm font-bold text-white shadow hover:bg-[#e0631a] disabled:opacity-60">
            {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            {mode === 'edit' ? t('visitRequestV2:edit.saveEdit') : t('visitRequestV2:edit.saveResubmit')}
          </button>
        </div>
      </form>

      {applyPrompt && (
        <div role="dialog" aria-modal="true" aria-labelledby="v2e-applyall-title" className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h3 id="v2e-applyall-title" className="text-base font-extrabold text-slate-900">{t('visitRequestV2:applyAll.title')}</h3>
            <p className="mt-2 text-sm text-slate-600">
              {applyPrompt.overwritten.length > 0
                ? t('visitRequestV2:applyAll.overwrites', { campuses: applyPrompt.overwritten.join(', ') })
                : t('visitRequestV2:applyAll.noOverwrites')}
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <button type="button" className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold" onClick={() => setApplyPrompt(null)}>{t('visitRequestV2:common.cancel')}</button>
              <button
                type="button"
                className="rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white"
                onClick={() => {
                  const current = form.getValues('campusVisits');
                  campusVisitFields.replace(applyContentToAllCampuses(current, applyPrompt.sourceIndex));
                  current.forEach((cv, i) => {
                    if (i !== applyPrompt.sourceIndex) bumpCardVersion(cv.clientKey);
                  });
                  setApplyPrompt(null);
                }}
              >
                {t('visitRequestV2:applyAll.confirm')}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* The "remove this campus?" dialog used to live here. There is nothing left for it to confirm:
          a campus cannot be dropped from a request that exists, so the control that opened it is gone
          and the backend refuses the payload that would have followed. */}
    </div>
  );
}
