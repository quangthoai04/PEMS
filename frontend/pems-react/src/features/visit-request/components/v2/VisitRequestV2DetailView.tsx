import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import axios from 'axios';
import { AlertCircle, Loader2, PencilLine, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { getVisitRequestFormV2, type ResolvedCampusVisit, type ResolvedVisitForm } from '../../api/visitRequestV2Api';
import { CampusVisitDetailCard } from './CampusVisitDetailCard';
import ContactIdentityPanel from '../ContactIdentityPanel';
import VisitAmendmentPanel from '../VisitAmendmentPanel';
import VisitAmendmentSubmitModal from '../VisitAmendmentSubmitModal';
import VisitSafeEditModal from '../VisitSafeEditModal';
import VisitHistoryTimeline from '../VisitHistoryTimeline';
import { hasAction, VisitV2Action } from '../../utils/visitV2Actions';
import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
}

/**
 * The per-campus v2 detail screen (plan §9.2/§9.4/§9.5): request-level data exactly ONCE,
 * then one CampusVisitDetailCard per AUTHORIZED campus — the component renders only what
 * the backend scoped to this caller; role names on the client are presentation hints, the
 * backend re-authorizes every command. Mixed content is labeled explicitly; no campus is
 * ever promoted to "representative".
 */
export default function VisitRequestV2DetailView({ visitRequestId }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [data, setData] = useState<ResolvedVisitForm | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<'notfound' | 'forbidden' | 'generic' | null>(null);
  const [safeEditOpen, setSafeEditOpen] = useState(false);
  const [amendCampus, setAmendCampus] = useState<ResolvedCampusVisit | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setData(await getVisitRequestFormV2(visitRequestId));
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 404) setError('notfound');
      else if (axios.isAxiosError(err) && err.response?.status === 403) setError('forbidden');
      else setError('generic');
    } finally {
      setLoading(false);
    }
  }, [visitRequestId]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return (
      <p role="status" className="flex items-center gap-2 p-6 text-sm text-slate-500">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> {t('visitRequestV2:detail.loading')}
      </p>
    );
  }
  if (error || !data) {
    return (
      <div role="alert" className="m-4 flex items-start gap-2 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
        <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
        <p>{t(`visitRequestV2:detail.${error ?? 'generic'}`)}</p>
      </div>
    );
  }

  const { viewer } = data;
  // Relation is used ONLY for the identity workflow (claim/transfer) — a separate backend-authorized flow.
  const isManager = (viewer.relation === 'REGISTRANT' || viewer.relation === 'VISITOR_OWNER') && !viewer.isReadOnly;
  const showMixedLabel = data.hasMixedCampusDetails && data.campusVisits.length > 1;
  // Every mutation action is driven ONLY by backend allowedActions — never relation/status. The backend
  // re-authorizes each command; HO/Host/out-of-scope viewers receive no actions and so see no buttons.
  const canEditPending = hasAction(viewer.allowedActions, VisitV2Action.EditPendingRequest);
  const canResubmit = hasAction(viewer.allowedActions, VisitV2Action.ResubmitRejectedRequest);
  const canSafeEdit = hasAction(viewer.allowedActions, VisitV2Action.SubmitSafeEdit);

  return (
    <div className="space-y-6">
      {/* ── Request-level (rendered exactly once) ── */}
      <header className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-4 sm:p-5">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-lg font-extrabold text-slate-900 dark:text-slate-50">{data.requestCode}</h2>
          <span className="rounded-full bg-slate-100 dark:bg-slate-700 px-2.5 py-0.5 text-xs font-bold text-slate-700 dark:text-slate-200">
            {t(`visitRequestV2:status.${data.requestStatus}`, data.requestStatus)}
          </span>
          <span className="rounded-full bg-[#004c91]/10 px-2.5 py-0.5 text-xs font-bold text-[#004c91]">
            {t('visitRequestV2:detail.campusTotal', { count: data.campusVisits.length })}
          </span>
          {showMixedLabel && (
            <span className="rounded-full bg-purple-100 px-2.5 py-0.5 text-xs font-bold text-purple-800">
              {t('visitRequestV2:detail.mixedLabel')}
            </span>
          )}
          {canEditPending && (
            <Link data-testid="pending-edit-open" to={`/dashboard/visit/v2/${data.visitRequestId}/edit`} className="ml-auto inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5">
              <PencilLine className="h-4 w-4" aria-hidden /> {t('visitRequestV2:edit.saveEdit')}
            </Link>
          )}
          {canResubmit && (
            <Link data-testid="resubmit-open" to={`/dashboard/visit/v2/${data.visitRequestId}/resubmit`} className="ml-auto inline-flex items-center gap-1.5 rounded-lg border border-[#f37021] px-3 py-1.5 text-sm font-bold text-[#f37021] hover:bg-[#f37021]/5">
              <RefreshCw className="h-4 w-4" aria-hidden /> {t('visitRequestV2:edit.saveResubmit')}
            </Link>
          )}
          {canSafeEdit && (
            <button type="button" data-testid="safe-edit-open" onClick={() => setSafeEditOpen(true)}
              className={`${canEditPending || canResubmit ? '' : 'ml-auto '}inline-flex items-center gap-1.5 rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-bold text-slate-700 hover:bg-slate-50`}>
              <PencilLine className="h-4 w-4" aria-hidden /> {t('visitRequestV2:safeEdit.open')}
            </button>
          )}
        </div>
        <dl className="mt-3 grid grid-cols-1 gap-x-8 gap-y-1.5 text-sm sm:grid-cols-2">
          <div>
            <dt className="inline text-slate-400">{t('visitRequestV2:detail.submittedAt')}: </dt>
            <dd className="inline text-slate-700 dark:text-slate-200">{formatVietnamDateTime(data.submittedAt)}</dd>
          </div>
          <div>
            <dt className="inline text-slate-400">{t('visitRequestV2:sections.registrant')}: </dt>
            <dd className="inline text-slate-700 dark:text-slate-200">
              {data.registrant.fullName}{data.registrant.organization ? ` — ${data.registrant.organization}` : ''}
            </dd>
          </div>
          <div>
            <dt className="inline text-slate-400">{t('visitRequestV2:detail.primaryContact')}: </dt>
            <dd className="inline text-slate-700 dark:text-slate-200">
              {data.primaryContact.fullName}
              {data.primaryContact.accessStatus === 'PENDING_CONFIRMATION' && (
                <span className="ml-1 rounded bg-amber-100 px-1.5 py-0.5 text-[11px] font-bold text-amber-800">
                  {t('visitRequestV2:detail.contactPending')}
                </span>
              )}
            </dd>
          </div>
        </dl>
      </header>

      {/* ── Identity workflow (registrant / ACTIVE contact only — backend re-authorizes) ── */}
      {isManager && (
        <ContactIdentityPanel
          visitRequestId={data.visitRequestId}
          primaryContactAccessStatus={data.primaryContact.accessStatus}
          contactEmailMasked={data.primaryContact.email || null}
          canManage
          onChanged={() => void load()}
        />
      )}

      {/* ── Per-campus cards: ONLY the campuses the backend returned for this caller ── */}
      <div className="space-y-4">
        {data.campusVisits.map(cv => {
          const canDecide = hasAction(cv.allowedActions, VisitV2Action.ApproveAmendment);
          const canWithdraw = hasAction(cv.allowedActions, VisitV2Action.WithdrawAmendment);
          const canSubmitAmendment = hasAction(cv.allowedActions, VisitV2Action.SubmitAmendment);
          return (
            <CampusVisitDetailCard key={cv.visitInstanceId} campus={cv}>
              {cv.activeAmendment && (canDecide || canWithdraw) && (
                <div className="mt-4">
                  <VisitAmendmentPanel
                    visitRequestId={data.visitRequestId}
                    visitInstanceId={cv.visitInstanceId}
                    canDecide={canDecide}
                    canWithdraw={canWithdraw}
                    onChanged={() => void load()}
                  />
                </div>
              )}
              {canSubmitAmendment && (
                <div className="mt-4">
                  <button type="button" data-testid={`amendment-open-${cv.visitInstanceId}`} onClick={() => setAmendCampus(cv)}
                    className="inline-flex items-center gap-1.5 rounded-lg border border-[#f37021] px-3 py-1.5 text-sm font-bold text-[#f37021] hover:bg-[#f37021]/5">
                    <PencilLine className="h-4 w-4" aria-hidden /> {t('visitRequestV2:amend.open')}
                  </button>
                </div>
              )}
            </CampusVisitDetailCard>
          );
        })}
      </div>

      {/* ── Scoped, masked history ── */}
      <section
        aria-label={t('visitRequestV2:detail.historyTitle')}
        className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-4 sm:p-5"
      >
        <h3 className="mb-3 text-sm font-extrabold text-slate-900 dark:text-slate-50">
          {t('visitRequestV2:detail.historyTitle')}
        </h3>
        <VisitHistoryTimeline visitRequestId={data.visitRequestId} />
      </section>

      {safeEditOpen && (
        <VisitSafeEditModal
          form={data}
          onClose={() => setSafeEditOpen(false)}
          onSaved={() => { setSafeEditOpen(false); void load(); }}
        />
      )}
      {amendCampus && (
        <VisitAmendmentSubmitModal
          visitRequestId={data.visitRequestId}
          campus={amendCampus}
          onClose={() => setAmendCampus(null)}
          onSubmitted={() => { setAmendCampus(null); void load(); }}
        />
      )}
    </div>
  );
}
