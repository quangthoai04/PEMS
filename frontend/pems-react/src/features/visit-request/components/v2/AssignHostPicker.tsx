import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle, Loader2 } from 'lucide-react';
import { delegationsApi } from '../../../delegations/api/delegationsApi';
import type { HostCandidate } from '../../../delegations/types/delegations.types';
import { getApiErrorMessage } from '../../../../shared/utils/toast';

interface Props {
  visitInstanceId: number;
  campusName: string;
  onCancel: () => void;
  /** Hands the choice back; the CALLER makes the write, so the approval can ride along with an edit. */
  onConfirm: (hostUserId: number, decisionNote: string | null) => void;
}

/**
 * Picks the Host for a campus that is about to be approved.
 *
 * <p>Deliberately does not approve anything itself — it returns the choice. Approving a campus assigns
 * its Host in the same act, and the Staff Leader's "Lưu và duyệt" additionally has an edit to commit
 * beside it; a picker that fired its own approve call would split that into two writes, and the campus
 * could end up rewritten but still waiting. <c>AssignHostModal</c> elsewhere in the app is the
 * standalone approval, and keeps its own call.</p>
 *
 * <p>A schedule conflict is shown, not blocked: hosting two delegations at once is a judgement the
 * leader makes, and the backend records it as a warning on the approval rather than refusing it.</p>
 */
export default function AssignHostPicker({ visitInstanceId, campusName, onCancel, onConfirm }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [candidates, setCandidates] = useState<HostCandidate[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [note, setNote] = useState('');

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    delegationsApi
      .getHostCandidates(visitInstanceId)
      .then(rows => { if (!cancelled) { setCandidates(rows); setError(null); } })
      .catch(err => { if (!cancelled) setError(getApiErrorMessage(err, t('visitRequestV2:pendingCampusEdit.hostLoadFailed'))); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [visitInstanceId, t]);

  const selected = candidates.find(c => c.userId === selectedId) ?? null;

  return (
    <div role="dialog" aria-modal="true" aria-labelledby="v2pc-host-title" className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
      <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl">
        <h3 id="v2pc-host-title" className="text-base font-extrabold text-slate-900">
          {t('visitRequestV2:pendingCampusEdit.hostTitle', { campus: campusName })}
        </h3>
        {/* Naming a Host is not an extra step of approving — it IS part of it. An approved campus with
            nobody running it has no owner for its setup, its logistics or its amendments. */}
        <p className="mt-1 text-sm text-slate-600">{t('visitRequestV2:pendingCampusEdit.hostRequired')}</p>

        {loading ? (
          <p role="status" className="mt-4 flex items-center gap-2 text-sm text-slate-500">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> {t('visitRequestV2:detail.loading')}
          </p>
        ) : error ? (
          <p role="alert" className="mt-4 text-sm text-red-600">{error}</p>
        ) : candidates.length === 0 ? (
          <p className="mt-4 text-sm text-amber-700">{t('visitRequestV2:pendingCampusEdit.hostNone')}</p>
        ) : (
          <div className="mt-4 max-h-64 space-y-2 overflow-y-auto">
            {candidates.map(c => (
              <label
                key={c.userId}
                className={`flex cursor-pointer items-start gap-2 rounded-xl border-2 p-3 text-sm ${
                  selectedId === c.userId ? 'border-[#004c91] bg-[#004c91]/5' : 'border-slate-200'
                }`}
              >
                <input
                  type="radio"
                  name="v2pc-host"
                  data-testid={`pending-campus-host-${c.userId}`}
                  className="mt-1"
                  checked={selectedId === c.userId}
                  onChange={() => setSelectedId(c.userId)}
                />
                <span className="min-w-0">
                  <span className="block font-semibold text-slate-900">
                    {c.fullName}
                    {c.roleLabel ? <span className="ml-1 font-normal text-slate-500">({c.roleLabel})</span> : null}
                  </span>
                  <span className="block truncate text-xs text-slate-500">{c.email}</span>
                  {c.hasScheduleConflict && (
                    <span className="mt-1 flex items-center gap-1 text-xs font-semibold text-amber-700">
                      <AlertTriangle className="h-3.5 w-3.5" aria-hidden />
                      {t('visitRequestV2:pendingCampusEdit.hostConflict', { count: c.conflictCount })}
                    </span>
                  )}
                </span>
              </label>
            ))}
          </div>
        )}

        <label className="mt-4 block">
          <span className="text-xs font-medium text-slate-600">
            {t('visitRequestV2:pendingCampusEdit.decisionNote')}
          </span>
          <textarea
            data-testid="pending-campus-decision-note"
            value={note}
            onChange={e => setNote(e.target.value)}
            rows={2}
            maxLength={500}
            className="mt-1 w-full rounded-xl border-2 border-slate-200 px-3 py-2 text-sm"
          />
        </label>

        <div className="mt-4 flex justify-end gap-2">
          <button type="button" className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold" onClick={onCancel}>
            {t('visitRequestV2:common.cancel')}
          </button>
          <button
            type="button"
            data-testid="pending-campus-host-confirm"
            disabled={selected === null}
            className="rounded-lg bg-[#f37021] px-4 py-2 text-sm font-bold text-white disabled:opacity-50"
            onClick={() => selected && onConfirm(selected.userId, note.trim() || null)}
          >
            {t('visitRequestV2:pendingCampusEdit.saveAndApprove')}
          </button>
        </div>
      </div>
    </div>
  );
}
