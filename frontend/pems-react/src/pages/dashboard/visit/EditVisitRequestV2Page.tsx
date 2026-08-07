import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useForm, useFieldArray, type FieldPath } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { AlertCircle, ArrowLeft, Loader2, Plus, RefreshCw, Send } from 'lucide-react';
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
  campusVisitHasUserContent,
  cloneCampusVisitContent,
  createEmptyCampusVisit,
  listOverwrittenCampuses,
  mapServerFieldPathToFormPath,
  resolvedFormToV2Schema,
} from '../../../features/visit-request/utils/visitRequestV2Form';
import { CampusVisitCard } from '../../../features/visit-request/components/v2/CampusVisitCard';
import { FormField, inputCls } from '../../../features/visit-request/components/shared/FormField';
import { PhoneField } from '../../../features/visit-request/components/shared/PhoneField';
import { FormSection } from '../../../features/visit-request/components/shared/FormSection';
import { useRegistrationCampuses } from '../../../features/visit-request/hooks/useRegistrationCampuses';
import { getApiErrorMessage } from '../../../shared/utils/toast';

type Mode = 'edit' | 'resubmit';

/** These request statuses accept a pending-edit (fully-pending) vs a resubmit (fully-rejected). */
const EDITABLE_STATUSES = new Set(['PENDING_APPROVAL', 'PENDING']);
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
  const [pendingRemove, setPendingRemove] = useState<number | null>(null);
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

  const schema = useMemo(
    () => buildVisitRequestV2Schema(V2_MIN_ADVANCE_HOURS_EDIT, (key, opts) => t(key, { ns: 'validation', ...opts })),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, i18n.language],
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

  const addCampus = () => {
    if (campusVisitFields.fields.length >= V2_MAX_CAMPUSES) return;
    const fresh = createEmptyCampusVisit();
    campusVisitFields.append(fresh);
    setOpenKeys(prev => new Set(prev).add(fresh.clientKey));
  };

  const requestRemove = (index: number) => {
    if (campusVisitFields.fields.length <= 1) return;
    const cv = form.getValues('campusVisits')[index];
    if (cv && campusVisitHasUserContent(cv)) setPendingRemove(index);
    else campusVisitFields.remove(index);
  };

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

  const onSubmit = form.handleSubmit(
    async data => {
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
  const allowAddRemove = mode === 'edit'; // resubmit keeps the campus set fixed

  return (
    <div className="mx-auto max-w-7xl space-y-4 p-4 sm:p-6">
      {back}
      <header>
        <h1 className="text-2xl font-extrabold text-[#004c91]">
          {mode === 'edit' ? t('visitRequestV2:edit.titleEdit') : t('visitRequestV2:edit.titleResubmit')}
        </h1>
        <p className="mt-1 text-sm text-slate-600">{t('visitRequestV2:edit.subtitle')}</p>
      </header>

      <form onSubmit={onSubmit} noValidate className="space-y-2">
        <FormSection id="v2e-registrant" title={t('visitRequestV2:sections.registrant')}>
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


        <FormSection id="v2e-campuses" title={t('visitRequestV2:sections.campuses')} description={allowAddRemove ? t('visitRequestV2:sections.campusesDesc') : t('visitRequestV2:edit.resubmitCampusFixed')}>
          <div className="space-y-4">
            {campusVisitFields.fields.map((field, index) => {
              const clientKey = form.getValues(`campusVisits.${index}.clientKey`) || field.id;
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
                    copySources={campusVisitFields.fields
                      .map((_, i) => i)
                      .filter(i => i !== index)
                      .map(i => ({ index: i, label: campusLabel(form.getValues('campusVisits')[i], i) }))}
                    onCopyFrom={source => copyInto(index, source)}
                    onApplyToAll={() => requestApplyToAll(index)}
                    onRemove={() => requestRemove(index)}
                    canRemove={allowAddRemove && campusVisitFields.fields.length > 1}
                    showErrors={showErrors}
                    // Editing/resubmitting only needs 24h notice — the same floor the schema
                    // above was built with, so the picker and the resolver cannot disagree.
                    minAdvanceHours={V2_MIN_ADVANCE_HOURS_EDIT}
                  />
                </div>
              );
            })}
          </div>
          {allowAddRemove && (
            <button
              type="button"
              disabled={campusVisitFields.fields.length >= V2_MAX_CAMPUSES}
              className="mt-4 inline-flex items-center gap-2 rounded-xl border-2 border-dashed border-[#004c91]/40 px-4 py-2.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5 disabled:opacity-40"
              onClick={addCampus}
            >
              <Plus className="h-4 w-4" /> {t('visitRequestV2:card.addCampus', { count: campusVisitFields.fields.length, max: V2_MAX_CAMPUSES })}
            </button>
          )}
        </FormSection>

        {submitError && (
          <div role="alert" className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-700">
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

      {pendingRemove !== null && (
        <div role="dialog" aria-modal="true" aria-labelledby="v2e-remove-title" className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h3 id="v2e-remove-title" className="text-base font-extrabold text-slate-900">{t('visitRequestV2:remove.title')}</h3>
            <p className="mt-2 text-sm text-slate-600">{t('visitRequestV2:remove.body', { campus: campusLabel(form.getValues('campusVisits')[pendingRemove], pendingRemove) })}</p>
            <div className="mt-4 flex justify-end gap-2">
              <button type="button" className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold" onClick={() => setPendingRemove(null)}>{t('visitRequestV2:common.cancel')}</button>
              <button type="button" className="rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white" onClick={() => { campusVisitFields.remove(pendingRemove); setPendingRemove(null); }}>{t('visitRequestV2:remove.confirm')}</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
