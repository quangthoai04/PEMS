import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { useForm, type FieldPath } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { AlertCircle, ArrowLeft, CheckCircle2, Loader2, RefreshCw, Send } from 'lucide-react';
import axios from 'axios';
import { useTranslation } from 'react-i18next';
import {
  buildVisitRequestV2Schema,
  V2_MIN_ADVANCE_HOURS_EDIT,
  type VisitRequestV2Schema,
} from '../../../features/visit-request/schema/visitRequestV2.schema';
import {
  getVisitRequestFormV2,
  updatePendingVisitInstance,
  type ResolvedCampusVisit,
  type ResolvedVisitForm,
  type V2ApproveAfterSave,
} from '../../../features/visit-request/api/visitRequestV2Api';
import {
  buildV2EditPayload,
  mapServerFieldPathToFormPath,
  resolvedFormToV2Schema,
} from '../../../features/visit-request/utils/visitRequestV2Form';
import { CampusVisitCard } from '../../../features/visit-request/components/v2/CampusVisitCard';
import { FormSection } from '../../../features/visit-request/components/shared/FormSection';
import { ReadOnlyInfoGrid } from '../../../features/visit-request/components/v2/shared/ReadOnlyInfoGrid';
import { useRegistrationCampuses } from '../../../features/visit-request/hooks/useRegistrationCampuses';
import { getApiErrorMessage } from '../../../shared/utils/toast';
import {
  errorCodeOf,
  hasAction,
  VisitEditErrorCode,
  VisitMutationErrorCode,
  VisitV2Action,
} from '../../../features/visit-request/utils/visitV2Actions';
import AssignHostPicker from '../../../features/visit-request/components/v2/AssignHostPicker';

/**
 * Edits ONE campus that is still waiting for its decision.
 *
 * <p>The whole-request edit screen beside this one needs EVERY campus of the request to be waiting,
 * because it rewrites data they share. On a mixed request — HN approved, HCM waiting, DN refused — it
 * simply is not offered, and until this screen existed HCM had nowhere to be corrected while DN had
 * resubmit and HN had amendments. Hence a screen scoped to one campus, whose payload names that campus
 * and nothing else.</p>
 *
 * <p>The registrant and this campus's operational contact edit within the ordinary rules — a Staff
 * Leader account among them, on a request they filed. Two extra privileges live on this screen and
 * belong to ONE actor, the Staff Leader of <i>this</i> campus who is also the registrant: filing a start
 * inside the 72-hour registration floor (the backend asks them to confirm rather than refusing) and
 * approving in the same action, which is one transaction rather than a save followed by a hopeful
 * approve. A leader who filed a request for a campus they do NOT lead gets the ordinary screen and
 * neither privilege; a leader looking at somebody else's request is not offered the screen at all and
 * decides that campus by approving or rejecting it, which they keep in full.</p>
 *
 * <p>Whether any of that is allowed is the backend's answer, always: the screen renders on the
 * EDIT_PENDING_CAMPUS action the read model granted, and every refusal it shows is one the API
 * returned.</p>
 */
export default function EditPendingCampusV2Page() {
  const { visitRequestId, visitInstanceId } = useParams<{
    visitRequestId: string;
    visitInstanceId: string;
  }>();
  const requestId = Number(visitRequestId);
  const instanceId = Number(visitInstanceId);
  const navigate = useNavigate();
  const { t, i18n } = useTranslation(['visitRequestV2', 'validation']);
  const { campuses, loading: campusesLoading } = useRegistrationCampuses();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [notEditable, setNotEditable] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [conflict, setConflict] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [showErrors, setShowErrors] = useState(false);
  const [campus, setCampus] = useState<ResolvedCampusVisit | null>(null);
  const [registrant, setRegistrant] = useState<ResolvedVisitForm['registrant'] | null>(null);
  const [requestCode, setRequestCode] = useState('');
  /** Set when the backend asked the Staff Leader to confirm a start inside the 72-hour floor. */
  const [overridePrompt, setOverridePrompt] = useState<string | null>(null);
  /** Open while the Staff Leader is choosing the Host for a "Lưu và duyệt". */
  const [hostPickerOpen, setHostPickerOpen] = useState(false);
  const requestRowVersionRef = useRef(0);

  /**
   * The backend's verdict on the two leader-only privileges INSIDE this screen: passing the 72-hour
   * floor, and "Lưu và duyệt". True only for the Staff Leader of THIS campus who ALSO filed the
   * request — so it is not the question "is this user a Staff Leader", and it is false for a leader who
   * registered a visit to a campus they do not lead even though they are editing it right now. The
   * browser must never re-derive any of that from a role on the token: that is exactly how somebody
   * would be shown a button the API then refuses.
   */
  const actsAsCampusLeader = campus?.canOverrideScheduleLeadTime === true;

  /**
   * The client-side floor is deliberately ZERO, and the 72-hour rule is applied at submit instead.
   *
   * A resolver floor would refuse a campus whose EXISTING start is already nearer than 72 hours — which
   * is the ordinary state of a request a few days before the visit — so a guest fixing a typo would be
   * told their unchanged date was invalid. The rule only ever applies to a schedule being MOVED, and
   * only to actors who cannot override it, so that is exactly where it is enforced.
   */
  const schema = useMemo(
    () => buildVisitRequestV2Schema(0, (key, opts) => t(key, { ns: 'validation', ...opts }), 1),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, i18n.language],
  );

  const form = useForm<VisitRequestV2Schema>({
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    resolver: zodResolver(schema) as any,
    mode: 'onBlur',
    reValidateMode: 'onChange',
  });

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    setNotEditable(false);
    try {
      const data = await getVisitRequestFormV2(requestId);
      const target = data.campusVisits.find(c => c.visitInstanceId === instanceId);
      if (!target) {
        setLoadError(t('visitRequestV2:detail.notfound'));
        return;
      }
      // The server's verdict, not a guess from status: it already weighed relation, lifecycle and the
      // cutoff for THIS campus, and a screen that re-derived any of that would eventually disagree.
      if (!hasAction(target.allowedActions, VisitV2Action.EditPendingCampus)) {
        setNotEditable(true);
        return;
      }
      const { values, expectedRequestRowVersion } = resolvedFormToV2Schema(data);
      requestRowVersionRef.current = expectedRequestRowVersion;
      const only = values.campusVisits.filter(cv => cv.visitInstanceId === instanceId);
      form.reset({ ...values, campusVisits: only });
      setCampus(target);
      setRegistrant(data.registrant);
      setRequestCode(data.requestCode);
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 404) setLoadError(t('visitRequestV2:detail.notfound'));
      else if (axios.isAxiosError(err) && err.response?.status === 403) setLoadError(t('visitRequestV2:edit.forbidden'));
      else setLoadError(getApiErrorMessage(err, t('visitRequestV2:detail.generic')));
    } finally {
      setLoading(false);
    }
  }, [requestId, instanceId, form, t]);

  useEffect(() => {
    if (Number.isFinite(requestId) && requestId > 0 && Number.isFinite(instanceId) && instanceId > 0) void load();
    else { setLoading(false); setLoadError(t('visitRequestV2:detail.notfound')); }
  }, [requestId, instanceId, load, t]);

  const applyServerErrors = (err: unknown): boolean => {
    if (!axios.isAxiosError(err)) return false;
    const errors = (err.response?.data as { errors?: Record<string, string[]> } | undefined)?.errors;
    if (!errors) return false;
    let mapped = false;
    for (const [serverPath, messages] of Object.entries(errors)) {
      const formPath = mapServerFieldPathToFormPath(serverPath);
      if (!formPath || !messages?.length) continue;
      form.setError(formPath as FieldPath<VisitRequestV2Schema>, { type: 'server', message: messages[0] });
      mapped = true;
    }
    return mapped;
  };

  /**
   * The one write. Called three ways — a plain save, the retry after a lead-time confirmation, and the
   * Staff Leader's save-and-approve — so all three go through the same payload, the same error
   * handling and the same optimistic-concurrency token.
   */
  const send = async (options: {
    overrideLeadTimeConfirmed?: boolean;
    approveAfterSave?: V2ApproveAfterSave | null;
  }) => {
    const values = form.getValues();
    const payload = buildV2EditPayload(values, requestRowVersionRef.current);
    const content = payload.campusVisits[0];
    if (!content) return;

    setIsSubmitting(true);
    setSubmitError(null);
    setConflict(false);
    try {
      const res = await updatePendingVisitInstance(requestId, instanceId, {
        content,
        overrideLeadTimeConfirmed: options.overrideLeadTimeConfirmed ?? false,
        approveAfterSave: options.approveAfterSave ?? null,
      });
      navigate(`/dashboard/visit/v2/${requestId}`, { replace: true, state: { flash: res.message } });
    } catch (err) {
      const status = axios.isAxiosError(err) ? err.response?.status : undefined;
      const code = errorCodeOf(err);

      // Stable CODES are matched before the status, always. Several of these arrive as 409, and a
      // status-first branch would collapse three different situations — "somebody else saved", "this
      // campus was just decided", "confirm the short notice" — into one generic conflict message.

      // The Staff Leader filed a schedule inside the 72-hour floor. Nothing is wrong with it — they are
      // being asked to mean it. The dialog is offered on THIS code alone, so an ordinary refusal can
      // never be turned into a "continue anyway".
      //
      // The body is the translated sentence rather than the server's: the backend answers in
      // Vietnamese, and the shared error helper deliberately drops raw Vietnamese in English mode,
      // which would leave this dialog showing a generic "data conflicts with an existing record".
      if (code === VisitMutationErrorCode.LeadTimeOverrideRequired) {
        setOverridePrompt(t('visitRequestV2:pendingCampusEdit.overrideBody'));
        return;
      }
      // A lifecycle refusal means somebody decided this campus while the form was open — reloading is
      // the only useful next step, so it is offered the same way a version conflict is.
      if (code === VisitEditErrorCode.PendingCampusNotEditable) {
        setConflict(true);
        setSubmitError(t('visitRequestV2:pendingCampusEdit.notEditable'));
        return;
      }
      if (
        code === VisitEditErrorCode.RequestVersionConflict
        || code === VisitEditErrorCode.InstanceVersionConflict
        || status === 409
      ) {
        setConflict(true);
        setSubmitError(t('visitRequestV2:edit.conflict'));
        return;
      }
      if (!applyServerErrors(err))
        setSubmitError(getApiErrorMessage(err, t('visitRequestV2:edit.submitFailed')));
    } finally {
      setIsSubmitting(false);
    }
  };

  /**
   * The 72-hour floor, checked here rather than in the resolver: it applies ONLY when this edit moves
   * the schedule, and never to the actor the backend lets past it — for them it asks for a confirmation
   * instead, which is a conversation the resolver cannot have.
   */
  const scheduleLeadTimeError = (): string | null => {
    if (!campus || actsAsCampusLeader) return null;
    const cv = form.getValues('campusVisits')[0];
    if (!cv) return null;
    const newStart = new Date(cv.startDatetime);
    if (Number.isNaN(newStart.getTime())) return null;
    const unchanged =
      newStart.getTime() === new Date(campus.plannedStartAt).getTime()
      && new Date(cv.endDatetime).getTime() === new Date(campus.plannedEndAt).getTime();
    if (unchanged) return null;
    const earliest = new Date(Date.now() + V2_MIN_ADVANCE_HOURS_EDIT * 3600_000);
    return newStart < earliest
      ? t('startTimeMinAdvance', { ns: 'validation', hours: V2_MIN_ADVANCE_HOURS_EDIT })
      : null;
  };

  const onSubmit = form.handleSubmit(
    async () => {
      const leadTimeError = scheduleLeadTimeError();
      if (leadTimeError) {
        setShowErrors(true);
        form.setError('campusVisits.0.startDatetime', { type: 'manual', message: leadTimeError });
        return;
      }
      await send({});
    },
    () => setShowErrors(true),
  );

  const onSaveAndApprove = async () => {
    const valid = await form.trigger();
    if (!valid) { setShowErrors(true); return; }
    setHostPickerOpen(true);
  };

  if (loading) {
    return (
      <p role="status" className="flex items-center gap-2 p-6 text-sm text-slate-500">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> {t('visitRequestV2:detail.loading')}
      </p>
    );
  }

  const back = (
    <Link
      to={`/dashboard/visit/v2/${requestId}`}
      className="inline-flex items-center gap-1.5 text-sm font-semibold text-[#004c91] hover:underline"
    >
      <ArrowLeft className="h-4 w-4" aria-hidden /> {t('visitRequestV2:detail.backToList')}
    </Link>
  );

  if (loadError) {
    return <div className="mx-auto max-w-3xl space-y-4 p-6">{back}<p role="alert" className="text-sm text-red-600">{loadError}</p></div>;
  }
  if (notEditable || !campus) {
    return (
      <div className="mx-auto max-w-3xl space-y-4 p-6">
        {back}
        <div role="alert" data-testid="pending-campus-not-editable" className="flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          <p>{t('visitRequestV2:pendingCampusEdit.notEditable')}</p>
        </div>
      </div>
    );
  }

  const { formState: { errors } } = form;
  const clientKey = form.getValues('campusVisits.0.clientKey') ?? 'pending-campus';

  return (
    <div className="mx-auto max-w-7xl space-y-4 p-4 sm:p-6">
      {back}
      <header>
        <h1 className="text-2xl font-extrabold text-[#004c91]">
          {t('visitRequestV2:pendingCampusEdit.title', { campus: campus.campusName })}
        </h1>
        <p className="mt-1 text-sm text-slate-600">
          {t('visitRequestV2:pendingCampusEdit.subtitle', { code: requestCode })}
        </p>
      </header>

      {/* The registrant block is READ-ONLY here. This screen writes one campus; the shared
          registrant snapshot belongs to the request and has its own workflows. */}
      {registrant && (
        <FormSection id="v2pc-registrant" title={t('visitRequestV2:sections.registrant')}>
          <ReadOnlyInfoGrid
            rows={[
              { label: t('visitRequestV2:registrant.fullName'), value: registrant.fullName },
              { label: t('visitRequestV2:registrant.organization'), value: registrant.organization },
              { label: t('visitRequestV2:card.phone'), value: registrant.phone },
              { label: t('visitRequestV2:card.email'), value: registrant.email },
            ]}
          />
        </FormSection>
      )}

      <form onSubmit={onSubmit} noValidate className="space-y-2">
        {/* Exactly one card, and no add/remove control anywhere: the campus set is fixed from the
            moment the request exists, and this screen edits the campus named in its own URL. */}
        <FormSection
          id="v2pc-campus"
          title={campus.campusName}
          description={t('visitRequestV2:pendingCampusEdit.campusFixed')}
        >
          <CampusVisitCard
            form={form}
            index={0}
            clientKey={clientKey}
            open
            onToggle={() => {}}
            campuses={campuses}
            campusesLoading={campusesLoading}
            takenCampusCodes={[]}
            contactReadOnly
            copySources={[]}
            onCopyFrom={() => {}}
            onApplyToAll={() => {}}
            onRemove={() => {}}
            canRemove={false}
            showErrors={showErrors}
            // Zero, for the same reason the resolver is: an unchanged near-term schedule must stay
            // editable. The real floor is applied at submit, to a schedule that actually moved.
            minAdvanceHours={0}
          />
        </FormSection>

        {actsAsCampusLeader && (
          <p className="rounded-xl border border-[#004c91]/20 bg-[#004c91]/5 px-3 py-2 text-sm text-[#004c91]">
            {t('visitRequestV2:pendingCampusEdit.leaderHint')}
          </p>
        )}

        {submitError && (
          <div role="alert" className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm font-semibold text-red-700">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
            <div>
              <p data-testid="pending-campus-edit-error">{submitError}</p>
              {conflict && (
                <button type="button" className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-red-300 px-3 py-1.5 text-xs font-bold hover:bg-red-100" onClick={() => void load()}>
                  <RefreshCw className="h-3.5 w-3.5" /> {t('visitRequestV2:edit.reload')}
                </button>
              )}
            </div>
          </div>
        )}
        {errors.campusVisits?.[0]?.startDatetime?.message && (
          <p role="alert" data-testid="pending-campus-leadtime-error" className="text-sm font-semibold text-red-700">
            {errors.campusVisits[0]?.startDatetime?.message}
          </p>
        )}

        <div className="flex flex-wrap justify-end gap-2 pt-4">
          {/* Saving is not deciding: a Staff Leader who only fixes a typo leaves the campus exactly
              where it was, still waiting for their answer. */}
          <button
            type="submit"
            data-testid="pending-campus-save"
            disabled={isSubmitting}
            className="inline-flex items-center gap-2 rounded-xl border-2 border-[#004c91] px-6 py-3 text-sm font-bold text-[#004c91] disabled:opacity-60"
          >
            {isSubmitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            {t('visitRequestV2:pendingCampusEdit.save')}
          </button>
          {/* Only for the leader-registrant. Rendered on the backend's flag alone, never on a role the
              browser read off the token — approving is the one thing a leader keeps on every request,
              and it must not be reachable from an edit screen they were never granted. */}
          {actsAsCampusLeader && (
            <button
              type="button"
              data-testid="pending-campus-save-approve"
              disabled={isSubmitting}
              onClick={() => void onSaveAndApprove()}
              className="inline-flex items-center gap-2 rounded-xl bg-[#f37021] px-6 py-3 text-sm font-bold text-white shadow hover:bg-[#e0631a] disabled:opacity-60"
            >
              <CheckCircle2 className="h-4 w-4" />
              {t('visitRequestV2:pendingCampusEdit.saveAndApprove')}
            </button>
          )}
        </div>
      </form>

      {/* The 72-hour confirmation. Raised only by the backend, and answered by re-sending the SAME
          payload with the flag — never by skipping the call. */}
      {overridePrompt && (
        <div role="dialog" aria-modal="true" aria-labelledby="v2pc-override-title" className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl">
            <h3 id="v2pc-override-title" className="text-base font-extrabold text-slate-900">
              {t('visitRequestV2:pendingCampusEdit.overrideTitle')}
            </h3>
            <p className="mt-2 text-sm text-slate-600" data-testid="pending-campus-override-body">{overridePrompt}</p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold"
                onClick={() => setOverridePrompt(null)}
              >
                {t('visitRequestV2:pendingCampusEdit.overrideCancel')}
              </button>
              <button
                type="button"
                data-testid="pending-campus-override-confirm"
                className="rounded-lg bg-[#f37021] px-4 py-2 text-sm font-bold text-white"
                onClick={() => { setOverridePrompt(null); void send({ overrideLeadTimeConfirmed: true }); }}
              >
                {t('visitRequestV2:pendingCampusEdit.overrideConfirm')}
              </button>
            </div>
          </div>
        </div>
      )}

      {hostPickerOpen && (
        <AssignHostPicker
          visitInstanceId={instanceId}
          campusName={campus.campusName}
          onCancel={() => setHostPickerOpen(false)}
          onConfirm={(hostUserId, decisionNote) => {
            setHostPickerOpen(false);
            // ONE call: the edit and the approval commit together, so a refused approval cannot leave
            // the campus rewritten and still waiting.
            void send({ approveAfterSave: { hostUserId, decisionNote } });
          }}
        />
      )}
    </div>
  );
}
