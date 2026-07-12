import React, { useEffect, useState } from 'react';
import { AlertTriangle, Loader2 } from 'lucide-react';
import type { UseFormReturn } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { VisitRequestSchema } from '../../schema/visitRequest.schema';
import { FormSection } from '../shared/FormSection';
import {
  visitRequestApi,
  type CampusProcessingChoice,
  type CreateHostCandidate,
} from '../../api/visitRequestApi';

export type CreatorRole = 'VISITOR' | 'STAFF' | 'STAFF_LEADER';

interface Props {
  form: UseFormReturn<VisitRequestSchema>;
  role: CreatorRole;
  /** Campus CODE of the creator's primary campus (Staff/Leader only). */
  ownCampusCode?: string | null;
  value: Record<string, CampusProcessingChoice>;
  onChange: (next: Record<string, CampusProcessingChoice>) => void;
}

/**
 * Authenticated-create only: per-campus processing choice. The creator's OWN campus offers
 * SELF_HOST (Staff/Leader) and ASSIGN_HOST (Leader); every other campus is locked to
 * SEND_FOR_REVIEW. Visitors see just an informational note (backend rejects direct modes).
 */
export const CampusProcessingSection: React.FC<Props> = ({ form, role, ownCampusCode, value, onChange }) => {
  const { t } = useTranslation(['visitRequest']);
  const visits = form.watch('visits') || [];

  const [candidates, setCandidates] = useState<CreateHostCandidate[] | null>(null);
  const [loadingCandidates, setLoadingCandidates] = useState(false);

  const isLeader = role === 'STAFF_LEADER';
  const isInternal = role === 'STAFF' || isLeader;
  const ownCode = (ownCampusCode || '').toUpperCase();

  const ownSlot = visits.find(v => (v.campus || '').toUpperCase() === ownCode);
  const ownMode = (ownCode && value[ownCode]?.mode) || 'SEND_FOR_REVIEW';

  // Load ASSIGN_HOST candidates lazily the first time the Leader picks that mode.
  useEffect(() => {
    if (!isLeader || ownMode !== 'ASSIGN_HOST' || candidates !== null || loadingCandidates) return;
    setLoadingCandidates(true);
    visitRequestApi
      .getCreateHostCandidates(ownSlot?.startDatetime || undefined, ownSlot?.endDatetime || undefined)
      .then(list => setCandidates(list.filter(c => !c.isStaffLeaderSelfHostOption)))
      .catch(() => setCandidates([]))
      .finally(() => setLoadingCandidates(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLeader, ownMode]);

  if (!isInternal) {
    return (
      <div className="mb-8 rounded-xl border border-blue-100 bg-blue-50/60 px-4 py-3 text-sm font-medium text-slate-600">
        {t('visitRequest:campusProcessing.visitorInfo')}
      </div>
    );
  }

  const setMode = (code: string, mode: CampusProcessingChoice['mode'], hostUserId?: number | null) => {
    onChange({
      ...value,
      [code]: { campusId: code, mode, hostUserId: mode === 'ASSIGN_HOST' ? (hostUserId ?? null) : null },
    });
  };

  const campusLabel = (code: string) =>
    t(`visitRequest:step2Info.campusOptions.${code}`, code);

  return (
    <FormSection id="section-campus-processing" title={t('visitRequest:campusProcessing.title')}>
      <p className="mb-4 text-xs text-slate-500">{t('visitRequest:campusProcessing.desc')}</p>

      <div className="space-y-3">
        {visits.map((v, idx) => {
          const code = (v.campus || '').toUpperCase();
          if (!code) return null;
          const isOwn = code === ownCode;
          const current = value[code]?.mode || 'SEND_FOR_REVIEW';
          const selectedHost = value[code]?.hostUserId ?? null;

          return (
            <div key={`${code}-${idx}`} className="rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <span className="text-sm font-bold text-slate-800">{campusLabel(code)}</span>
                {!isOwn && (
                  <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold text-slate-500">
                    {t('visitRequest:campusProcessing.otherCampusPending')}
                  </span>
                )}
              </div>

              {isOwn && (
                <div className="mt-3 space-y-2">
                  <label className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-slate-700">
                    <input
                      type="radio"
                      name={`campus-mode-${code}`}
                      checked={current === 'SEND_FOR_REVIEW'}
                      onChange={() => setMode(code, 'SEND_FOR_REVIEW')}
                      className="h-4 w-4 text-[#004c91] focus:ring-[#004c91]"
                    />
                    {t('visitRequest:campusProcessing.sendForReview')}
                  </label>

                  <label className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-slate-700">
                    <input
                      type="radio"
                      name={`campus-mode-${code}`}
                      checked={current === 'SELF_HOST'}
                      onChange={() => setMode(code, 'SELF_HOST')}
                      className="h-4 w-4 text-[#004c91] focus:ring-[#004c91]"
                    />
                    {t('visitRequest:campusProcessing.selfHost')}
                  </label>

                  {isLeader && (
                    <label className="flex cursor-pointer items-center gap-2.5 text-sm font-medium text-slate-700">
                      <input
                        type="radio"
                        name={`campus-mode-${code}`}
                        checked={current === 'ASSIGN_HOST'}
                        onChange={() => setMode(code, 'ASSIGN_HOST', selectedHost)}
                        className="h-4 w-4 text-[#004c91] focus:ring-[#004c91]"
                      />
                      {t('visitRequest:campusProcessing.assignHost')}
                    </label>
                  )}

                  {isLeader && current === 'ASSIGN_HOST' && (
                    <div className="ml-6 mt-1">
                      <label className="mb-1 block text-xs font-semibold text-slate-600">
                        {t('visitRequest:campusProcessing.hostPickerLabel')}
                      </label>
                      {loadingCandidates ? (
                        <span className="inline-flex items-center gap-2 text-xs text-slate-500">
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                          {t('visitRequest:campusProcessing.loadingCandidates')}
                        </span>
                      ) : (candidates?.length ?? 0) === 0 ? (
                        <span className="text-xs font-medium text-amber-600">
                          {t('visitRequest:campusProcessing.noCandidates')}
                        </span>
                      ) : (
                        <select
                          value={selectedHost ?? ''}
                          onChange={(e) => setMode(code, 'ASSIGN_HOST', e.target.value ? Number(e.target.value) : null)}
                          className="w-full max-w-md rounded-lg border border-slate-300 px-3 py-2 text-sm focus:border-[#004c91] focus:outline-none focus:ring-1 focus:ring-[#004c91]"
                        >
                          <option value="">{t('visitRequest:campusProcessing.hostPickerPlaceholder')}</option>
                          {candidates!.map(c => (
                            <option key={c.userId} value={c.userId}>
                              {c.fullName}
                              {c.hasScheduleConflict ? ` — ${t('visitRequest:campusProcessing.hostConflictBadge')} (${c.conflictCount})` : ''}
                            </option>
                          ))}
                        </select>
                      )}
                    </div>
                  )}

                  {current !== 'SEND_FOR_REVIEW' && (
                    <p className="ml-6 mt-1 flex items-start gap-1.5 text-xs font-medium text-amber-700">
                      <AlertTriangle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
                      {t('visitRequest:campusProcessing.hostFinalWarning')}
                    </p>
                  )}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </FormSection>
  );
};
