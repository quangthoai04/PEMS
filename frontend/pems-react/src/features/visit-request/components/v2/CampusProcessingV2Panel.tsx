import React, { useEffect, useState } from 'react';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import {
  visitRequestApi,
  type CampusProcessingChoice,
  type CreateHostCandidate,
} from '../../api/visitRequestApi';
import type { CreatorRole } from '../sections/CampusProcessingSection';

interface Props {
  /** Campus CODE of THIS card. Empty until the user picks a campus → panel stays hidden. */
  campusCode: string;
  role: CreatorRole;
  /** Campus CODE of the creator's own primary campus (Staff/Leader only). */
  ownCampusCode?: string | null;
  /** This campus's planned window — used only to flag candidate schedule overlaps. */
  startDatetime?: string;
  endDatetime?: string;
  value?: CampusProcessingChoice;
  onChange: (next: CampusProcessingChoice) => void;
}

/**
 * Per-campus processing choice for the AUTHENTICATED v2 create — rendered INSIDE the campus card
 * so the decision travels with the campus it applies to (a multi-campus request can self-host HN
 * while HCM waits for the HCM Staff Leader).
 *
 * Only the creator's OWN primary campus is decidable: regular Staff get self-host / ask-leader,
 * a Staff Leader also gets assign-to-another-Staff. Every other campus renders a read-only routed
 * notice and sends NO processing intent at all. The backend re-authorizes all of this — this panel
 * is the affordance, never the enforcement.
 */
export const CampusProcessingV2Panel: React.FC<Props> = ({
  campusCode, role, ownCampusCode, startDatetime, endDatetime, value, onChange,
}) => {
  const { t } = useTranslation(['visitRequest']);

  const [candidates, setCandidates] = useState<CreateHostCandidate[] | null>(null);
  const [candidatesError, setCandidatesError] = useState(false);
  const [loadingCandidates, setLoadingCandidates] = useState(false);

  const isLeader = role === 'STAFF_LEADER';
  const isInternal = role === 'STAFF' || isLeader;
  const code = (campusCode || '').toUpperCase();
  const ownCode = (ownCampusCode || '').toUpperCase();
  const isOwnCampus = !!code && code === ownCode;

  const mode = value?.mode ?? 'SEND_FOR_REVIEW';
  const selectedHost = value?.hostUserId ?? null;

  const loadCandidates = React.useCallback(() => {
    setLoadingCandidates(true);
    setCandidatesError(false);
    visitRequestApi
      .getCreateHostCandidates(startDatetime || undefined, endDatetime || undefined)
      .then(list => setCandidates(list.filter(c => !c.isStaffLeaderSelfHostOption)))
      .catch(() => { setCandidates(null); setCandidatesError(true); })
      .finally(() => setLoadingCandidates(false));
  }, [startDatetime, endDatetime]);

  // Candidates load lazily the first time the Leader picks assign-to-another.
  useEffect(() => {
    if (!isLeader || !isOwnCampus || mode !== 'ASSIGN_HOST') return;
    if (candidates !== null || candidatesError || loadingCandidates) return;
    loadCandidates();
  }, [isLeader, isOwnCampus, mode, candidates, candidatesError, loadingCandidates, loadCandidates]);

  if (!isInternal || !code) return null;

  // Switching mode always drops a stale host id — an ASSIGN_HOST pick must never survive into
  // self-host / send-for-review and reach the payload.
  const setMode = (next: CampusProcessingChoice['mode'], hostUserId?: number | null) =>
    onChange({
      campusId: code,
      mode: next,
      hostUserId: next === 'ASSIGN_HOST' ? (hostUserId ?? null) : null,
    });

  if (!isOwnCampus) {
    return (
      <div
        data-testid={`campus-processing-readonly-${code}`}
        className="mt-4 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-sm font-medium text-slate-600"
      >
        {t('visitRequest:campusProcessing.otherCampusPending')}
      </div>
    );
  }

  const radio = (value_: CampusProcessingChoice['mode'], label: string) => (
    <label className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-slate-700">
      <input
        type="radio"
        name={`v2-campus-mode-${code}`}
        data-testid={`campus-processing-${value_}-${code}`}
        checked={mode === value_}
        onChange={() => setMode(value_, value_ === 'ASSIGN_HOST' ? selectedHost : null)}
        className="h-4 w-4 text-[#004c91] focus:ring-[#004c91]"
      />
      {label}
    </label>
  );

  return (
    <div
      data-testid={`campus-processing-${code}`}
      className="mt-4 rounded-xl border border-[#004c91]/20 bg-[#004c91]/[0.03] p-4"
    >
      <p className="mb-1 text-sm font-bold text-slate-800">{t('visitRequest:campusProcessing.title')}</p>
      <p className="mb-3 text-xs text-slate-500">{t('visitRequest:campusProcessing.desc')}</p>

      <div className="space-y-2">
        {radio('SEND_FOR_REVIEW', t('visitRequest:campusProcessing.sendForReview'))}
        {radio('SELF_HOST', t('visitRequest:campusProcessing.selfHost'))}
        {isLeader && radio('ASSIGN_HOST', t('visitRequest:campusProcessing.assignHost'))}

        {isLeader && mode === 'ASSIGN_HOST' && (
          <div className="ml-6 mt-1">
            <label
              htmlFor={`v2-host-picker-${code}`}
              className="mb-1 block text-xs font-semibold text-slate-600"
            >
              {t('visitRequest:campusProcessing.hostPickerLabel')}
            </label>
            {loadingCandidates ? (
              <span className="inline-flex items-center gap-2 text-xs text-slate-500">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                {t('visitRequest:campusProcessing.loadingCandidates')}
              </span>
            ) : candidatesError ? (
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-semibold text-red-600">
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
              <span className="text-xs font-medium text-amber-600">
                {t('visitRequest:campusProcessing.noCandidates')}
              </span>
            ) : (
              <select
                id={`v2-host-picker-${code}`}
                data-testid={`campus-processing-host-${code}`}
                value={selectedHost ?? ''}
                onChange={e => setMode('ASSIGN_HOST', e.target.value ? Number(e.target.value) : null)}
                className="w-full max-w-md rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-[#004c91] focus:outline-none focus:ring-1 focus:ring-[#004c91]"
              >
                <option value="">{t('visitRequest:campusProcessing.hostPickerPlaceholder')}</option>
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

        {mode !== 'SEND_FOR_REVIEW' && (
          <p className="ml-6 mt-1 flex items-start gap-1.5 text-xs font-medium text-amber-700">
            <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
            {t('visitRequest:campusProcessing.hostFinalWarning')}
          </p>
        )}
      </div>
    </div>
  );
};
