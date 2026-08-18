import React, { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  visitRequestApi,
  type CampusHostSelectionChoice,
  type CreateHostCandidate,
} from '../../api/visitRequestApi';
import type { CreatorRole } from '../../schema/visitRequestV2.schema';

interface Props {
  /** Campus CODE of THIS card. Empty until the user picks a campus → panel stays hidden. */
  campusCode: string;
  role: CreatorRole;
  /** Campus CODE of the creator's own primary campus (Staff/Leader only). */
  ownCampusCode?: string | null;
  /** This campus's planned window — used only to flag candidate schedule overlaps. */
  startDatetime?: string;
  endDatetime?: string;
  value?: CampusHostSelectionChoice;
  onChange: (next: CampusHostSelectionChoice) => void;
}

/**
 * "Phương án người phụ trách tiếp đón" — the per-campus reception-host arrangement on the
 * AUTHENTICATED v2 create, rendered INSIDE the campus card so the choice travels with the campus it
 * applies to (a multi-campus request can name a host for HN while HCM waits for its own Leader).
 *
 * <p>
 * What this panel records is an INTENTION. It replaces the old direct-processing panel, which
 * assigned a current host at submit time — before anyone on the guest side had confirmed they were
 * coming, and therefore before there was anything to host. Here the choice is stored as a proposal
 * and only becomes an assignment when the request's confirmation gate opens and the backend
 * revalidates it. That is why there is no "this is final" warning any more: nothing here is final.
 * </p>
 *
 * <p>
 * Only the creator's OWN primary campus is arrangeable: a regular IC Staff gets self / assign-later,
 * a Staff Leader also gets choose-another-staff. Every other campus renders a read-only notice and
 * sends NO arrangement at all. The backend re-authorizes all of it — this panel is the affordance,
 * never the enforcement.
 * </p>
 */
export const CampusHostSelectionV2Panel: React.FC<Props> = ({
  campusCode, role, ownCampusCode, startDatetime, endDatetime, value, onChange,
}) => {
  const { t } = useTranslation(['visitRequest', 'visitRequestV2']);

  const [candidates, setCandidates] = useState<CreateHostCandidate[] | null>(null);
  const [candidatesError, setCandidatesError] = useState(false);
  const [loadingCandidates, setLoadingCandidates] = useState(false);

  const isLeader = role === 'STAFF_LEADER';
  const isInternal = role === 'STAFF' || isLeader;
  const code = (campusCode || '').toUpperCase();
  const ownCode = (ownCampusCode || '').toUpperCase();
  const isOwnCampus = !!code && code === ownCode;

  const mode = value?.mode ?? 'WAIT_FOR_LATER';
  const selectedHost = value?.proposedHostUserId ?? null;

  const loadCandidates = React.useCallback(() => {
    setLoadingCandidates(true);
    setCandidatesError(false);
    visitRequestApi
      .getCreateHostCandidates(startDatetime || undefined, endDatetime || undefined)
      .then(list => setCandidates(list.filter(c => !c.isStaffLeaderSelfHostOption)))
      .catch(() => { setCandidates(null); setCandidatesError(true); })
      .finally(() => setLoadingCandidates(false));
  }, [startDatetime, endDatetime]);

  // Candidates load lazily the first time the Leader picks choose-another.
  useEffect(() => {
    if (!isLeader || !isOwnCampus || mode !== 'SELECTED') return;
    if (candidates !== null || candidatesError || loadingCandidates) return;
    loadCandidates();
  }, [isLeader, isOwnCampus, mode, candidates, candidatesError, loadingCandidates, loadCandidates]);

  if (!isInternal || !code) return null;

  // Switching mode always drops a stale pick — a SELECTED person must never survive into SELF or
  // WAIT_FOR_LATER and reach the payload, where the backend would refuse the whole submission.
  const setMode = (next: CampusHostSelectionChoice['mode'], proposedHostUserId?: number | null) =>
    onChange({
      campusId: code,
      mode: next,
      proposedHostUserId: next === 'SELECTED' ? (proposedHostUserId ?? null) : null,
    });

  if (!isOwnCampus) {
    return (
      <div
        data-testid={`campus-host-selection-readonly-${code}`}
        className="mt-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm font-normal text-slate-600"
      >
        {t('visitRequest:campusProcessing.otherCampusPending')}
      </div>
    );
  }

  const radio = (value_: CampusHostSelectionChoice['mode'], label: string) => (
    <label className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-slate-700">
      <input
        type="radio"
        name={`v2-campus-host-mode-${code}`}
        data-testid={`campus-host-selection-${value_}-${code}`}
        checked={mode === value_}
        onChange={() => setMode(value_, value_ === 'SELECTED' ? selectedHost : null)}
        className="h-4 w-4 text-[#004c91] focus:ring-[#004c91]"
      />
      {label}
    </label>
  );

  return (
    <div
      data-testid={`campus-host-selection-${code}`}
      className="mt-4 rounded-xl border border-[#004c91]/20 bg-[#004c91]/[0.03] p-4"
    >
      <p className="mb-1 text-sm font-bold text-slate-800">
        {t('visitRequestV2:hostSelection.legend')}
      </p>
      <p className="mb-3 text-xs text-slate-500">
        {t('visitRequestV2:receptionHost.waitingForContact')}
      </p>

      <div className="space-y-2">
        {radio('SELF', t('visitRequestV2:hostSelection.self'))}
        {isLeader && radio('SELECTED', t('visitRequestV2:hostSelection.selectOther'))}
        {radio('WAIT_FOR_LATER', t('visitRequestV2:hostSelection.waitForLater'))}

        {isLeader && mode === 'SELECTED' && (
          <div className="ml-6 mt-1">
            <label
              htmlFor={`v2-host-picker-${code}`}
              className="mb-1 block text-xs font-semibold text-slate-600"
            >
              {t('visitRequestV2:hostSelection.selectOther')}
            </label>
            {loadingCandidates ? (
              <span className="inline-flex items-center gap-2 text-xs text-slate-500">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                {t('visitRequestV2:hostSelection.loadingCandidates')}
              </span>
            ) : candidatesError ? (
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-normal text-red-600">
                  {t('visitRequest:campusProcessing.candidatesError')}
                </span>
                <button
                  type="button"
                  onClick={loadCandidates}
                  className="rounded-lg border border-slate-300 px-2.5 py-1 text-xs font-bold text-slate-700 hover:bg-slate-50"
                >
                  {t('visitRequest:campusProcessing.retryCandidates')}
                </button>
              </div>
            ) : (candidates?.length ?? 0) === 0 ? (
              <span className="text-xs font-normal text-amber-600">
                {t('visitRequestV2:hostSelection.noCandidates')}
              </span>
            ) : (
              <select
                id={`v2-host-picker-${code}`}
                data-testid={`campus-host-selection-host-${code}`}
                value={selectedHost ?? ''}
                onChange={e => setMode('SELECTED', e.target.value ? Number(e.target.value) : null)}
                className="w-full max-w-md rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-[#004c91] focus:outline-none focus:ring-1 focus:ring-[#004c91]"
              >
                <option value="">{t('visitRequestV2:hostSelection.pickPlaceholder')}</option>
                {candidates!.map(c => (
                  <option key={c.userId} value={c.userId}>
                    {c.fullName}
                    {c.hasScheduleConflict
                      ? ` — ${t('visitRequest:campusProcessing.hostConflictBadge')} (${c.conflictCount})`
                      : ''}
                  </option>
                ))}
              </select>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default CampusHostSelectionV2Panel;
