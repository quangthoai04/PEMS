import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import axios from 'axios';
import { AlertCircle, Loader2, PencilLine, RefreshCw, UserCog } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  getVisitRequestFormV2,
  markVisitChangesSeen,
  type ResolvedCampusVisit,
  type ResolvedVisitForm,
} from '../../api/visitRequestV2Api';
import { CampusVisitDetailCard } from './CampusVisitDetailCard';
import VisitAmendmentPanel from '../VisitAmendmentPanel';
import VisitAmendmentSubmitModal from '../VisitAmendmentSubmitModal';
import VisitSafeEditModal from '../VisitSafeEditModal';
import VisitHostTransferModal, { type HostTransferTarget } from '../VisitHostTransferModal';
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
  // Nothing on this screen is decided from the signed-in role any more. Whether a change is applied
  // straight away is a per-campus fact — requester side AND that campus's Host — and the backend says
  // so through `amendmentSelfApproves`; a role check here told a staff registrant who hosted nothing
  // that their proposal would apply immediately, and it did not.

  const [data, setData] = useState<ResolvedVisitForm | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<'notfound' | 'forbidden' | 'generic' | null>(null);
  const [safeEditOpen, setSafeEditOpen] = useState(false);
  const [amendCampus, setAmendCampus] = useState<ResolvedCampusVisit | null>(null);
  const [transferCampus, setTransferCampus] = useState<HostTransferTarget | null>(null);
  // The timeline fetches for itself, so reloading this screen's data does not reload it. Anything that
  // writes history bumps this instead of only calling load(): a user who cancels an invitation and
  // watches the timeline below not mention it reasonably concludes the cancel did not happen.
  const [historyRefreshKey, setHistoryRefreshKey] = useState(0);
  /**
   * WHICH campus cards the reader has open, on a request that has more than one.
   *
   * A SET, not a single id: every campus opens and closes independently, so reading Hà Nội and
   * TP.HCM side by side is possible. It used to be one "the open campus" value, which meant opening
   * a second campus silently shut the first — on a multi-campus request the reader had to keep
   * re-opening the card they had just been reading in order to compare it with another.
   *
   * `undefined` still means "the reader has not chosen yet", so the default (first campus open) can
   * be resolved during RENDER rather than written in by an effect — the first paint already shows
   * campus 1 open instead of flashing every card shut. An empty set is a real choice ("I closed them
   * all") and is therefore distinct from `undefined`. Because this is state rather than something
   * recomputed from `data`, a background reload cannot pull the reader off what they were reading.
   */
  const [campusChoice, setCampusChoice] = useState<Set<number> | undefined>(undefined);

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

  /** Reload after a mutation that writes to BOTH the request and its history. */
  const reloadWithHistory = useCallback(async () => {
    await load();
    setHistoryRefreshKey(v => v + 1);
  }, [load]);

  // Mark the request's changes seen once the detail has actually rendered. Deliberately here and not
  // in the list: a badge cleared by a row scrolling past is a badge the reader never got to act on.
  // Failure is silent — an unread count that stays up is a far smaller problem than an error toast
  // on a screen the user opened to read something else.
  useEffect(() => {
    if (!data) return;
    void markVisitChangesSeen(visitRequestId).catch(() => {});
  }, [data, visitRequestId]);

  // The edit/resubmit page navigates here with its success message in router state, and THIS is the
  // one place that turns it into a toast — the form deliberately shows none of its own, so a save
  // produces exactly one confirmation.
  //
  // Shown once and the state cleared immediately, so a back/forward or a refresh cannot replay a stale
  // confirmation. The ref is what makes "once" true rather than nearly true: under StrictMode the
  // effect runs, cleans up and runs again on mount, both times reading the router state the first pass
  // has not finished clearing — which is how one save produced two identical toasts. The toast id is
  // belt and braces; react-hot-toast collapses repeats of the same id into one.
  const location = useLocation();
  const navigate = useNavigate();
  const consumedFlashRef = useRef<string | null>(null);
  useEffect(() => {
    const flash = (location.state as { flash?: string } | null)?.flash;
    if (!flash || consumedFlashRef.current === flash) return;
    consumedFlashRef.current = flash;
    showSuccessToast(flash, `v2-detail-flash-${visitRequestId}`);
    navigate(location.pathname, { replace: true, state: null });
  }, [location.state, location.pathname, navigate, visitRequestId]);

  // Which campuses are open right now: the reader's own set if they have touched anything,
  // otherwise the default of the FIRST campus alone — and only on a request that HAS more than one,
  // since a lone card is never collapsible. Computed, not stored, so it is right on the first paint.
  const multipleCampuses = (data?.campusVisits.length ?? 0) > 1;
  const openCampusIds = useMemo<Set<number>>(() => {
    if (campusChoice !== undefined) {
      // A campus that has vanished from the authorized payload is pruned rather than kept in the
      // set, so a reload that removes one cannot leave a stale id behind for a future campus id to
      // collide with.
      if (!data) return campusChoice;
      const visible = new Set(data.campusVisits.map(c => c.visitInstanceId));
      const pruned = new Set([...campusChoice].filter(id => visible.has(id)));
      return pruned.size === campusChoice.size ? campusChoice : pruned;
    }
    return multipleCampuses ? new Set([data!.campusVisits[0].visitInstanceId]) : new Set<number>();
  }, [campusChoice, data, multipleCampuses]);

  /** Flip ONE campus, leaving every other card exactly as the reader left it. */
  const toggleCampus = useCallback((visitInstanceId: number) => {
    setCampusChoice(prev => {
      const base = prev ?? openCampusIds;
      const next = new Set(base);
      if (next.has(visitInstanceId)) next.delete(visitInstanceId);
      else next.add(visitInstanceId);
      return next;
    });
  }, [openCampusIds]);

  // Deep link from the edit form's "Thay đổi đầu mối": the campus contact panels are far down a long
  // screen and only exist once the request has loaded, so the scroll waits for the data rather than
  // firing at mount and landing on a skeleton.
  //
  // With the accordion, the target campus may be closed — and a closed card renders no body, so the
  // anchor does not exist yet. The campus is opened first, and the scroll runs on the pass AFTER
  // that state has rendered; scrolling in the same pass would find nothing and silently do nothing.
  //
  // The deep link ADDS its campus to the open set; it never replaces it. Arriving at one campus is
  // no reason to close the one the reader already had open beside it.
  useEffect(() => {
    if (!data || !location.hash) return;
    const anchor = location.hash.slice(1);
    const match = /^contact-(\d+)$/.exec(anchor);
    const targetInstanceId = match ? Number(match[1]) : null;
    if (targetInstanceId != null && multipleCampuses
        && data.campusVisits.some(c => c.visitInstanceId === targetInstanceId)
        && !openCampusIds.has(targetInstanceId)) {
      setCampusChoice(prev => new Set(prev ?? openCampusIds).add(targetInstanceId));
      return; // the element appears on the next render; this effect re-runs and scrolls then
    }
    document.getElementById(anchor)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }, [data, location.hash, multipleCampuses, openCampusIds]);

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
  // The shared registrant/contact block is all-or-nothing across campuses, so on a MIXED request
  // this is correctly false.
  const canSafeEditRequestLevel = hasAction(viewer.allowedActions, VisitV2Action.SubmitSafeEdit);
  // ...but an individual campus can still be editable while its siblings are not, and that campus
  // must stay reachable. Opening the modal is offered when EITHER scope has something to edit;
  // inside, each scope is gated on its own verdict.
  const editableCampusCount = data.campusVisits
    .filter(c => hasAction(c.allowedActions, VisitV2Action.SubmitSafeEdit)).length;
  const canOpenSafeEdit = canSafeEditRequestLevel || editableCampusCount > 0;
  // The refused verdicts too — a missed deadline is shown as a disabled button with its reason
  // rather than as an action that silently vanished.
  const safeEditCap = capabilityFor(viewer.capabilities, VisitV2Action.SubmitSafeEdit);
  const editPendingCap = capabilityFor(viewer.capabilities, VisitV2Action.EditPendingRequest);

  // There is no request-level confirmation roll-up. It counted campuses ("1/1", "còn N cơ sở chờ")
  // directly above the very cards that name each contact and show that contact's own state, so it
  // never told the reader anything the section below it did not — on one campus it was a heading
  // over its own answer. The workflow itself is untouched: it lives per campus, where the contact
  // does. See the campus cards in section ② for the full per-campus contact block.

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
              granted={canOpenSafeEdit}
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

      {/* ── ② Per-campus cards: ONLY the campuses the backend returned for this caller ──
          Each card carries its OWN operational contact in full — name, organisation, job title,
          email, phone, confirmation state and who/when decided — which is what the removed
          request-level roll-up was counting. */}
      <VisitSectionCard
        step={2}
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
              // Only a request with more than one campus gets the accordion. One campus has nothing
              // to collapse against, and putting its single card behind a chevron would add a click
              // to reach the only thing on the page.
              const collapsible = multipleCampuses;
              const expanded = !collapsible || openCampusIds.has(cv.visitInstanceId);
              const canDecide = hasAction(cv.allowedActions, VisitV2Action.ApproveAmendment);
              const canWithdraw = hasAction(cv.allowedActions, VisitV2Action.WithdrawAmendment);
              const canSubmitAmendment = hasAction(cv.allowedActions, VisitV2Action.SubmitAmendment);
              const canTransferHost = hasAction(cv.allowedActions, VisitV2Action.TransferHost);
              // The per-campus door for a campus still waiting for its decision. It is the action that
              // makes a MIXED request workable: the request-level "Sửa đơn" above is refused as soon as
              // one campus has been decided, and this campus has not been. Purely the backend's verdict:
              // a Staff Leader reviewing somebody else's request does not receive it (they approve or
              // reject instead), and nothing here re-derives that from the viewer's role.
              const canEditPendingCampus = hasAction(cv.allowedActions, VisitV2Action.EditPendingCampus);
              // Per-campus verdicts: these belong to THIS card, not to the request. A sibling that is
              // under way says nothing about this campus, and a global button could not express that.
              const amendCap = capabilityFor(cv.capabilities, VisitV2Action.SubmitAmendment);
              const transferCap = capabilityFor(cv.capabilities, VisitV2Action.TransferHost);
              const editPendingCampusCap = capabilityFor(cv.capabilities, VisitV2Action.EditPendingCampus);
              return (
                <CampusVisitDetailCard
                  key={cv.visitInstanceId}
                  campus={cv}
                  visitRequestId={data.visitRequestId}
                  onContactChanged={() => void reloadWithHistory()}
                  collapsible={collapsible}
                  expanded={expanded}
                  // Flips THIS campus only. Opening a second campus leaves the first open — comparing
                  // two campuses of one request is the ordinary thing to do here — and closing one
                  // leaves the rest exactly as they were. "Show me nothing but the headers" is still
                  // reachable by closing them all.
                  onToggle={() => toggleCampus(cv.visitInstanceId)}
                >
                  {cv.activeAmendment && (canDecide || canWithdraw) && (
                    <VisitAmendmentPanel
                      visitRequestId={data.visitRequestId}
                      visitInstanceId={cv.visitInstanceId}
                      canDecide={canDecide}
                      canWithdraw={canWithdraw}
                      onChanged={() => void reloadWithHistory()}
                    />
                  )}
                  <div className="flex flex-wrap items-start gap-2">
                    {/* A campus still waiting for its answer. Navigation, not a submit — the screen it
                        opens edits this campus alone and leaves every sibling untouched. */}
                    {canEditPendingCampus ? (
                      <Link
                        data-testid={`pending-campus-edit-open-${cv.visitInstanceId}`}
                        to={`/dashboard/visit/v2/${data.visitRequestId}/campus/${cv.visitInstanceId}/edit`}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91] hover:bg-[#004c91]/5"
                      >
                        <PencilLine className="h-4 w-4" aria-hidden /> {t('visitRequestV2:pendingCampusEdit.open')}
                      </Link>
                    ) : (
                      <VisitActionButton
                        capability={editPendingCampusCap}
                        granted={false}
                        onClick={() => {}}
                        data-testid={`pending-campus-edit-open-${cv.visitInstanceId}`}
                        icon={<PencilLine className="h-4 w-4" aria-hidden />}
                        className="inline-flex items-center gap-1.5 rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91]"
                      >
                        {t('visitRequestV2:pendingCampusEdit.open')}
                      </VisitActionButton>
                    )}
                    <VisitActionButton
                      capability={amendCap}
                      granted={canSubmitAmendment}
                      onClick={() => setAmendCampus(cv)}
                      data-testid={`amendment-open-${cv.visitInstanceId}`}
                      icon={<PencilLine className="h-4 w-4" aria-hidden />}
                      className="inline-flex items-center gap-1.5 rounded-lg border border-[#f37021] px-3 py-1.5 text-sm font-bold text-[#f37021] hover:bg-[#f37021]/5"
                    >
                      {/* "Cập nhật" when the backend says this viewer's change is applied in the same
                          call — they are the requester side AND this campus's Host, so there is nobody
                          to wait for. It used to read `user.roleCode === 'STAFF'`, which promised
                          instant application to any staff account, including one that hosted nothing. */}
                      {cv.amendmentSelfApproves ? t('visitRequestV2:amend.openUpdate') : t('visitRequestV2:amend.open')}
                    </VisitActionButton>
                    <VisitActionButton
                      capability={transferCap}
                      granted={canTransferHost}
                      onClick={() => setTransferCampus({
                        visitInstanceId: cv.visitInstanceId,
                        campusName: cv.campusName,
                        currentHostUserId: cv.currentHostUserId,
                        currentHostName: cv.currentHostName,
                        plannedStartAt: cv.plannedStartAt,
                        rowVersion: cv.rowVersion,
                        cutoffAt: transferCap?.cutoffAt ?? null,
                        requiredLeadHours: transferCap?.requiredLeadHours ?? null,
                      })}
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

      {/* ── ③ Scoped, masked history ── */}
      <VisitSectionCard
        step={3}
        title={t('visitRequestV2:detail.historyTitle')}
        readOnlyLabel={t('visitRequestV2:detail.readOnly')}
        data-testid="section-history"
      >
        <VisitHistoryTimeline visitRequestId={data.visitRequestId} refreshKey={historyRefreshKey} />
      </VisitSectionCard>

      {safeEditOpen && (
        <VisitSafeEditModal
          form={data}
          onClose={() => setSafeEditOpen(false)}
          onSaved={() => { setSafeEditOpen(false); void reloadWithHistory(); }}
        />
      )}
      {amendCampus && (
        <VisitAmendmentSubmitModal
          visitRequestId={data.visitRequestId}
          campus={amendCampus}
          onClose={() => setAmendCampus(null)}
          onSubmitted={() => { setAmendCampus(null); void reloadWithHistory(); }}
        />
      )}
      {transferCampus && (
        <VisitHostTransferModal
          campus={transferCampus}
          onClose={() => setTransferCampus(null)}
          onTransferred={() => { setTransferCampus(null); void reloadWithHistory(); }}
        />
      )}
    </div>
  );
}
