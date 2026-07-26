import { useCallback, useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import axios from 'axios';
import { AlertCircle, Loader2, PencilLine, RefreshCw, UserCog } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { getVisitRequestFormV2, type ResolvedCampusVisit, type ResolvedVisitForm } from '../../api/visitRequestV2Api';
import { CampusVisitDetailCard } from './CampusVisitDetailCard';
import ContactIdentityActions from '../ContactIdentityActions';
import VisitAmendmentPanel from '../VisitAmendmentPanel';
import VisitAmendmentSubmitModal from '../VisitAmendmentSubmitModal';
import VisitSafeEditModal from '../VisitSafeEditModal';
import VisitHostTransferModal from '../VisitHostTransferModal';
import VisitHistoryTimeline from '../VisitHistoryTimeline';
import { VisitActionButton } from './shared/VisitActionButton';
import { capabilityFor, hasAction, VisitV2Action } from '../../utils/visitV2Actions';
import { formatVietnamDateTime } from '../../../../shared/utils/vietnamTime';
import { showSuccessToast } from '../../../../shared/utils/toast';
import { VisitSectionCard } from './shared/VisitSectionCard';
import { VisitStatusBadge } from './shared/VisitStatusBadge';
import { ReadOnlyInfoGrid } from './shared/ReadOnlyInfoGrid';
import { VisitOutcomeSummary } from './shared/VisitOutcomeSummary';

interface Props {
  visitRequestId: number;
}

/**
 * The per-campus v2 detail screen (plan §9.2/§9.4/§9.5 + §12–§19): request-level data exactly ONCE,
 * then one CampusVisitDetailCard per AUTHORIZED campus — the component renders only what the
 * backend scoped to this caller; role names on the client are presentation hints, the backend
 * re-authorizes every command. Mixed content is labeled explicitly; no campus is ever promoted to
 * "representative".
 *
 * Visually it is the same system as the Xử lý đơn screen (blue section headers, orange step
 * numbers, the shared person table), so moving between reading and processing a request does not
 * feel like moving between two products.
 */
export default function VisitRequestV2DetailView({ visitRequestId }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [data, setData] = useState<ResolvedVisitForm | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<'notfound' | 'forbidden' | 'generic' | null>(null);
  const [safeEditOpen, setSafeEditOpen] = useState(false);
  const [amendCampus, setAmendCampus] = useState<ResolvedCampusVisit | null>(null);
  const [transferCampus, setTransferCampus] = useState<ResolvedCampusVisit | null>(null);

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

  // The edit/resubmit page navigates here with its success message in router state. Nothing consumed
  // it, so a successful save landed on a silent screen. Show it exactly once and clear the state
  // immediately — otherwise a back/forward or a refresh would replay a stale confirmation.
  const location = useLocation();
  const navigate = useNavigate();
  useEffect(() => {
    const flash = (location.state as { flash?: string } | null)?.flash;
    if (!flash) return;
    showSuccessToast(flash);
    navigate(location.pathname, { replace: true, state: null });
  }, [location.state, location.pathname, navigate]);

  if (loading) {
    return (
      <p role="status" className="flex items-center gap-2 p-6 text-sm text-slate-500">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> {t('visitRequestV2:detail.loading')}
      </p>
    );
  }
  if (error || !data) {
    return (
      <div role="alert" className="m-4 rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
        <div className="flex items-start gap-2">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
          <p>{t(`visitRequestV2:detail.${error ?? 'generic'}`)}</p>
        </div>
        {/* A transport failure is worth retrying in place; "not found" and "forbidden" are answers,
            not failures, so they get no retry that could only ever repeat itself. */}
        {error === 'generic' && (
          <button
            type="button"
            data-testid="detail-retry"
            onClick={() => void load()}
            className="mt-3 inline-flex items-center gap-1.5 rounded-lg border border-amber-300 bg-white px-3 py-1.5 text-sm font-bold text-amber-800 hover:bg-amber-100"
          >
            <RefreshCw className="h-4 w-4" aria-hidden /> {t('visitRequestV2:detail.retry')}
          </button>
        )}
      </div>
    );
  }

  const { viewer } = data;
  const showMixedLabel = data.hasMixedCampusDetails && data.campusVisits.length > 1;
  // Every mutation action is driven ONLY by backend allowedActions — never relation/status. The backend
  // re-authorizes each command; HO/Host/out-of-scope viewers receive no actions and so see no buttons.
  // The contact identity workflow is the last one to join this rule: it used to read viewer.relation.
  const canEditPending = hasAction(viewer.allowedActions, VisitV2Action.EditPendingRequest);
  const canResubmit = hasAction(viewer.allowedActions, VisitV2Action.ResubmitRejectedRequest);
  const canSafeEdit = hasAction(viewer.allowedActions, VisitV2Action.SubmitSafeEdit);
  // The refused verdicts too — a missed deadline is shown as a disabled button with its reason
  // rather than as an action that silently vanished.
  const safeEditCap = capabilityFor(viewer.capabilities, VisitV2Action.SubmitSafeEdit);
  const editPendingCap = capabilityFor(viewer.capabilities, VisitV2Action.EditPendingRequest);

  return (
    <div className="space-y-4">
      {/* ── Overview card (§14.1): identity of the request, its state, and what may be done to it ── */}
      <header className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="text-xl font-extrabold text-[#004c91]">{data.requestCode}</h2>
          <VisitStatusBadge kind="request" status={data.requestStatus} data-testid="request-status" />
          <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-bold text-slate-700 ring-1 ring-inset ring-slate-200">
            {t('visitRequestV2:detail.campusTotal', { count: data.campusVisits.length })}
          </span>
          {showMixedLabel && (
            <span className="rounded-full bg-[#f37021]/10 px-2.5 py-0.5 text-xs font-bold text-[#f37021] ring-1 ring-inset ring-[#f37021]/20">
              {t('visitRequestV2:detail.mixedLabel')}
            </span>
          )}

          <div className="ml-auto flex flex-wrap items-center gap-2">
            {/* These are NAVIGATION, not submits — they open the edit form. Labelling them
                "Lưu thay đổi" / "Gửi lại đơn" claimed an action that only happens on the form's own
                submit button, which still carries those labels. */}
            {canEditPending ? (
              <Link data-testid="pending-edit-open" to={`/dashboard/visit/v2/${data.visitRequestId}/edit`} className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5">
                <PencilLine className="h-4 w-4" aria-hidden /> {t('visitRequestV2:edit.openEdit')}
              </Link>
            ) : (
              <VisitActionButton
                capability={editPendingCap}
                granted={false}
                onClick={() => {}}
                data-testid="pending-edit-open"
                icon={<PencilLine className="h-4 w-4" aria-hidden />}
                className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91]"
              >
                {t('visitRequestV2:edit.openEdit')}
              </VisitActionButton>
            )}
            {canResubmit && (
              <Link data-testid="resubmit-open" to={`/dashboard/visit/v2/${data.visitRequestId}/resubmit`} className="inline-flex items-center gap-1.5 rounded-lg border border-[#f37021] px-3 py-1.5 text-sm font-bold text-[#f37021] hover:bg-[#f37021]/5">
                <RefreshCw className="h-4 w-4" aria-hidden /> {t('visitRequestV2:edit.openResubmit')}
              </Link>
            )}
            <VisitActionButton
              capability={safeEditCap}
              granted={canSafeEdit}
              onClick={() => setSafeEditOpen(true)}
              data-testid="safe-edit-open"
              icon={<PencilLine className="h-4 w-4" aria-hidden />}
              className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 px-3 py-1.5 text-sm font-bold text-slate-700 hover:bg-slate-50"
            >
              {t('visitRequestV2:safeEdit.open')}
            </VisitActionButton>
          </div>
        </div>

        {/* The registrant and contact blocks used to be repeated here, immediately above sections 1
            and 2 that show the same people in full. The overview answers "which request, what state,
            what can I do" — who the people are is the sections' job. */}
        <p className="mt-3 text-sm text-slate-500">
          {t('visitRequestV2:detail.submittedAt')}:{' '}
          <span className="font-medium text-slate-700">{formatVietnamDateTime(data.submittedAt)}</span>
        </p>

        <div className="mt-3">
          <VisitOutcomeSummary form={data} />
        </div>
      </header>

      {/* ── ① Registrant ── */}
      <VisitSectionCard
        step={1}
        title={t('visitRequestV2:sections.registrant')}
        readOnlyLabel={t('visitRequestV2:detail.readOnly')}
        data-testid="section-registrant"
      >
        <ReadOnlyInfoGrid
          rows={[
            { label: t('visitRequestV2:registrant.fullName'), value: data.registrant.fullName },
            { label: t('visitRequestV2:registrant.organization'), value: data.registrant.organization },
            { label: t('visitRequestV2:registrant.jobTitle'), value: data.registrant.jobTitle },
            { label: t('visitRequestV2:card.phone'), value: data.registrant.phone },
            { label: t('visitRequestV2:registrant.nationality'), value: data.registrant.nationality },
            { label: t('visitRequestV2:card.email'), value: data.registrant.email },
          ]}
        />
      </VisitSectionCard>

      {/* ── ② Primary contact ── */}
      <VisitSectionCard
        step={2}
        title={t('visitRequestV2:sections.contact')}
        readOnlyLabel={t('visitRequestV2:detail.readOnly')}
        data-testid="section-contact"
      >
        <ReadOnlyInfoGrid
          rows={[
            { label: t('visitRequestV2:person.fullName'), value: data.primaryContact.fullName },
            { label: t('visitRequestV2:person.organization'), value: data.primaryContact.organization },
            { label: t('visitRequestV2:card.phone'), value: data.primaryContact.phone },
            { label: t('visitRequestV2:card.email'), value: data.primaryContact.email },
            {
              label: t('visitRequestV2:detail.contactAccessStatus'),
              value: data.primaryContact.accessStatus === 'ACTIVE'
                ? t('visitRequestV2:detail.contactActive')
                : t('visitRequestV2:detail.contactPending'),
            },
            {
              label: t('visitRequestV2:detail.contactVerifiedAt'),
              value: data.primaryContact.verifiedAt ? formatVietnamDateTime(data.primaryContact.verifiedAt) : null,
            },
          ]}
        />

        {/* The claim/transfer workflow belongs to THIS contact, so it lives in this section rather
            than in a card of its own above — splitting one business object across two cards is what
            produced the duplicated contact block. Which of its actions exist is the backend's call,
            passed straight through; the panel renders nothing at all when none were granted. */}
        <ContactIdentityActions
          visitRequestId={data.visitRequestId}
          primaryContactAccessStatus={data.primaryContact.accessStatus}
          contactEmail={data.primaryContact.email || null}
          allowedActions={viewer.allowedActions}
          onChanged={() => void load()}
        />
      </VisitSectionCard>

      {/* ── ③ Per-campus cards: ONLY the campuses the backend returned for this caller ── */}
      <VisitSectionCard
        step={3}
        title={t('visitRequestV2:sections.campuses')}
        headerExtra={
          <span className="rounded-md bg-white/15 px-2 py-0.5 text-[10px] font-bold uppercase tracking-wide text-white">
            {t('visitRequestV2:detail.campusTotal', { count: data.campusVisits.length })}
          </span>
        }
        data-testid="section-campuses"
      >
        {data.campusVisits.length === 0 ? (
          <p className="text-sm italic text-slate-400">{t('visitRequestV2:detail.noCampusInScope')}</p>
        ) : (
          <div className="space-y-4">
            {data.campusVisits.map(cv => {
              const canDecide = hasAction(cv.allowedActions, VisitV2Action.ApproveAmendment);
              const canWithdraw = hasAction(cv.allowedActions, VisitV2Action.WithdrawAmendment);
              const canSubmitAmendment = hasAction(cv.allowedActions, VisitV2Action.SubmitAmendment);
              const canTransferHost = hasAction(cv.allowedActions, VisitV2Action.TransferHost);
              // Per-campus verdicts: these belong to THIS card, not to the request. A sibling that is
              // under way says nothing about this campus, and a global button could not express that.
              const amendCap = capabilityFor(cv.capabilities, VisitV2Action.SubmitAmendment);
              const transferCap = capabilityFor(cv.capabilities, VisitV2Action.TransferHost);
              return (
                <CampusVisitDetailCard key={cv.visitInstanceId} campus={cv}>
                  {cv.activeAmendment && (canDecide || canWithdraw) && (
                    <VisitAmendmentPanel
                      visitRequestId={data.visitRequestId}
                      visitInstanceId={cv.visitInstanceId}
                      canDecide={canDecide}
                      canWithdraw={canWithdraw}
                      onChanged={() => void load()}
                    />
                  )}
                  <div className="flex flex-wrap items-start gap-2">
                    <VisitActionButton
                      capability={amendCap}
                      granted={canSubmitAmendment}
                      onClick={() => setAmendCampus(cv)}
                      data-testid={`amendment-open-${cv.visitInstanceId}`}
                      icon={<PencilLine className="h-4 w-4" aria-hidden />}
                      className="inline-flex items-center gap-1.5 rounded-lg border border-[#f37021] px-3 py-1.5 text-sm font-bold text-[#f37021] hover:bg-[#f37021]/5"
                    >
                      {t('visitRequestV2:amend.open')}
                    </VisitActionButton>
                    <VisitActionButton
                      capability={transferCap}
                      granted={canTransferHost}
                      onClick={() => setTransferCampus(cv)}
                      data-testid={`host-transfer-open-${cv.visitInstanceId}`}
                      icon={<UserCog className="h-4 w-4" aria-hidden />}
                      className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5"
                    >
                      {t('visitRequestV2:hostTransfer.open')}
                    </VisitActionButton>
                  </div>
                </CampusVisitDetailCard>
              );
            })}
          </div>
        )}
      </VisitSectionCard>

      {/* ── ④ Scoped, masked history ── */}
      <VisitSectionCard
        step={4}
        title={t('visitRequestV2:detail.historyTitle')}
        readOnlyLabel={t('visitRequestV2:detail.readOnly')}
        data-testid="section-history"
      >
        <VisitHistoryTimeline visitRequestId={data.visitRequestId} />
      </VisitSectionCard>

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
      {transferCampus && (
        <VisitHostTransferModal
          campus={transferCampus}
          onClose={() => setTransferCampus(null)}
          onTransferred={() => { setTransferCampus(null); void load(); }}
        />
      )}
    </div>
  );
}
