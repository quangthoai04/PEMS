import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AlertTriangle, Loader2, UserCheck, X } from 'lucide-react';
import { transferVisitHost } from '../api/visitRequestV2Api';
import { delegationsApi } from '../../delegations/api/delegationsApi';
import type { HostCandidate } from '../../delegations/types/delegations.types';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { focusFirstInvalidField } from '../utils/formErrorNavigation';

/**
 * Only what the handover actually needs. It used to take the whole per-campus read DTO, which meant the
 * modal could be opened from the detail screen and nowhere else — the management list has the same four
 * facts about a campus but not that DTO. Narrowing the contract is what lets one modal serve both.
 */
export interface HostTransferTarget {
  visitInstanceId: number;
  campusName: string;
  currentHostUserId?: number | null;
  currentHostName?: string | null;
  plannedStartAt?: string | null;
  /** Campus-instance concurrency token, echoed back so a stale form 409s instead of clobbering. */
  rowVersion: number;
  /** When the handover window closes, from the backend verdict. Shown so the deadline is not a surprise. */
  cutoffAt?: string | null;
  requiredLeadHours?: number | null;
}

interface Props {
  campus: HostTransferTarget;
  onClose: () => void;
  onTransferred: () => void;
}

/**
 * Hand this campus's Host role to a different eligible user (campus Staff Leader only).
 *
 * Candidate list is the SAME backend query the approval screen uses, so the choices offered here are
 * exactly the ones the backend will accept. The current Host is shown but not selectable — picking
 * them would be a no-op the backend refuses, and offering it invites the click.
 *
 * A reason is required. Three other people learn about this change from a notification (the outgoing
 * Host, the incoming one, and the visitor who was given a name); "Host đã thay đổi" with no reason
 * leaves all three to ask why.
 */
export default function VisitHostTransferModal({ campus, onClose, onTransferred }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [candidates, setCandidates] = useState<HostCandidate[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // No spam on open (plan PEMS_VALIDATION_UX §3) — required-field errors appear only after the first
  // Submit click. Once shown, they clear themselves the instant the field becomes valid, because they
  // are DERIVED from `submitAttempted` + the field's own state rather than a separately-set flag.
  const [submitAttempted, setSubmitAttempted] = useState(false);

  // Escape closes, focus starts inside the dialog, and focus returns to whatever opened it — the modal
  // is now reachable from a row menu, where being dropped back at the top of a 50-row list is a real cost.
  const dialogRef = useRef<HTMLDivElement | null>(null);
  useEffect(() => {
    const opener = document.activeElement as HTMLElement | null;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !busy) { e.stopPropagation(); onClose(); }
    };
    document.addEventListener('keydown', onKey, true);
    dialogRef.current?.focus();
    return () => {
      document.removeEventListener('keydown', onKey, true);
      opener?.focus?.();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setLoadError(false);
    delegationsApi
      .getHostCandidates(campus.visitInstanceId)
      .then(rows => { if (!cancelled) setCandidates(rows); })
      .catch(() => { if (!cancelled) setLoadError(true); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [campus.visitInstanceId]);

  // The current Host cannot be "transferred to" — filter them out rather than disable, so the list
  // is a list of real options.
  const selectable = candidates.filter(c => c.userId !== campus.currentHostUserId);
  const selected = selectable.find(c => c.userId === selectedId) ?? null;
  // The button stays enabled whenever there is something to submit — a disabled control with no
  // explanation is a dead end (plan PEMS_VALIDATION_UX §3: "ưu tiên cách ít gây nút chết không lý
  // do"). Missing selection/reason is judged, and said, at Submit instead of silently refusing clicks.
  const canSubmit = !busy && !loading && !loadError && selectable.length > 0;
  const hostError = submitAttempted && selectedId === null
    ? t('visitRequestV2:hostTransfer.errSelectHost') : undefined;
  const reasonError = submitAttempted && reason.trim().length === 0
    ? t('visitRequestV2:hostTransfer.errReasonRequired') : undefined;

  const submit = async () => {
    setSubmitAttempted(true);
    if (selectedId === null || reason.trim().length === 0) {
      window.setTimeout(() => focusFirstInvalidField(dialogRef.current ?? document), 60);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await transferVisitHost(campus.visitInstanceId, {
        newHostUserId: selectedId,
        reason: reason.trim(),
        expectedRowVersion: campus.rowVersion,
      });
      showSuccessToast(res.message);
      onTransferred();
    } catch (err) {
      // A 409 means somebody else changed this campus while the form was open; reloading is the only
      // way forward, so say that rather than offering a retry that would fail the same way.
      const status = (err as { response?: { status?: number } })?.response?.status;
      const message = status === 409
        ? t('visitRequestV2:hostTransfer.conflict')
        : t('visitRequestV2:hostTransfer.errGeneric');
      setError(message);
      showErrorToast(err, message);
    } finally {
      setBusy(false);
    }
  };

  const field = 'w-full rounded-lg border border-slate-300 bg-white p-2 text-sm';

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4"
      role="dialog"
      aria-modal="true"
      aria-label={t('visitRequestV2:hostTransfer.title')}
    >
      <div
        ref={dialogRef}
        tabIndex={-1}
        className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl bg-white p-5 shadow-xl outline-none"
      >
        <div className="mb-3 flex items-start justify-between gap-3">
          <div>
            <h2 className="text-base font-extrabold text-[#004c91]">
              {t('visitRequestV2:hostTransfer.title')}
            </h2>
            <p className="mt-0.5 text-xs text-slate-500">{campus.campusName}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-slate-500 hover:bg-slate-100"
            aria-label={t('visitRequestV2:common.cancel')}
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <dl className="mb-4 rounded-xl bg-slate-50 px-3 py-2 text-sm">
          <div className="flex gap-2">
            <dt className="font-semibold text-slate-600">{t('visitRequestV2:hostTransfer.currentHost')}</dt>
            <dd data-testid="host-transfer-current" className="text-slate-900">
              {campus.currentHostName ?? '—'}
            </dd>
          </div>
          <div className="mt-1 flex gap-2">
            <dt className="font-semibold text-slate-600">{t('visitRequestV2:hostTransfer.schedule')}</dt>
            <dd className="text-slate-900">
              {campus.plannedStartAt ? formatVietnamDateTime(campus.plannedStartAt) : '—'}
            </dd>
          </div>
          {/* The deadline belongs on the form, not only in the error you get for missing it. */}
          {campus.cutoffAt && (
            <div className="mt-1 flex gap-2">
              <dt className="font-semibold text-slate-600">{t('visitRequestV2:hostTransfer.cutoff')}</dt>
              <dd data-testid="host-transfer-cutoff" className="text-slate-900">
                {formatVietnamDateTime(campus.cutoffAt)}
                <span className="ml-1 text-xs text-slate-500">
                  {t('visitRequestV2:hostTransfer.cutoffHint', {
                    hours: campus.requiredLeadHours ?? 6,
                  })}
                </span>
              </dd>
            </div>
          )}
        </dl>

        {/* Approval, schedule and content are untouched by a handover — say so, because "chuyển Host"
            on an approved request reasonably makes people worry it goes back into a queue. */}
        <p className="mb-4 rounded-lg bg-blue-50 px-3 py-2 text-xs text-blue-800">
          {t('visitRequestV2:hostTransfer.keepsApproval')}
        </p>

        {loading ? (
          <p role="status" className="flex items-center gap-2 py-6 text-sm text-slate-500">
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden />
            {t('visitRequestV2:hostTransfer.loadingCandidates')}
          </p>
        ) : loadError ? (
          <p role="alert" className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
            {t('visitRequestV2:hostTransfer.loadFailed')}
          </p>
        ) : selectable.length === 0 ? (
          <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-800">
            {t('visitRequestV2:hostTransfer.noCandidates')}
          </p>
        ) : (
          <fieldset className="mb-3" data-field-error={hostError ? 'true' : undefined}>
            <legend className="mb-1 text-sm font-bold text-slate-700">
              {t('visitRequestV2:hostTransfer.newHost')} <span className="text-red-500">*</span>
            </legend>
            {hostError && (
              <p role="alert" data-testid="host-transfer-error-host" className="mb-1.5 text-xs font-normal text-red-600">
                {hostError}
              </p>
            )}
            <div className="space-y-1.5">
              {selectable.map(c => (
                <label
                  key={c.userId}
                  className={`flex cursor-pointer items-start gap-2 rounded-lg border p-2.5 ${
                    selectedId === c.userId ? 'border-[#004c91] bg-[#004c91]/5' : 'border-slate-200'
                  }`}
                >
                  <input
                    type="radio"
                    name="new-host"
                    className="mt-1"
                    data-testid={`host-transfer-candidate-${c.userId}`}
                    checked={selectedId === c.userId}
                    onChange={() => setSelectedId(c.userId)}
                  />
                  <span className="min-w-0">
                    <span className="block text-sm font-semibold text-slate-900">
                      {c.fullName}
                      {c.isStaffLeaderSelfHostOption && (
                        <span className="ml-1.5 text-xs font-bold text-[#f37021]">
                          {t('visitRequestV2:hostTransfer.selfOption')}
                        </span>
                      )}
                    </span>
                    <span className="block truncate text-xs text-slate-500">
                      {[c.roleLabel, c.departmentName, c.email].filter(Boolean).join(' · ')}
                    </span>
                    {/* Advisory, not a block: the backend permits double-hosting and only warns. */}
                    {c.hasScheduleConflict && (
                      <span className="mt-1 flex items-center gap-1 text-xs font-semibold text-amber-700">
                        <AlertTriangle className="h-3.5 w-3.5" aria-hidden />
                        {t('visitRequestV2:hostTransfer.conflictWarning', { count: c.conflictCount })}
                      </span>
                    )}
                  </span>
                </label>
              ))}
            </div>
          </fieldset>
        )}

        <label className="mb-3 block text-sm" data-field-error={reasonError ? 'true' : undefined}>
          <span className="mb-1 block text-xs font-semibold text-slate-600">
            {t('visitRequestV2:hostTransfer.reason')} <span className="text-red-500">*</span>
          </span>
          <textarea
            data-testid="host-transfer-reason"
            className={reasonError
              ? `${field} border-red-500 focus:border-red-500 focus:ring-2 focus:ring-red-200`
              : field}
            rows={3}
            maxLength={500}
            value={reason}
            onChange={e => setReason(e.target.value)}
            placeholder={t('visitRequestV2:hostTransfer.reasonPlaceholder')}
            aria-invalid={reasonError ? true : undefined}
            aria-describedby={reasonError ? 'host-transfer-reason-error' : undefined}
          />
          {reasonError && (
            <p id="host-transfer-reason-error" role="alert" data-testid="host-transfer-error-reason"
              className="mt-1 text-xs font-normal text-red-600">
              {reasonError}
            </p>
          )}
        </label>

        {selected?.hasScheduleConflict && (
          <p className="mb-3 rounded-lg bg-amber-50 px-3 py-2 text-xs text-amber-800" role="status">
            {t('visitRequestV2:hostTransfer.conflictAck', { name: selected.fullName })}
          </p>
        )}

        {error && (
          <p role="alert" className="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700"
          >
            {t('visitRequestV2:common.cancel')}
          </button>
          <button
            type="button"
            data-testid="host-transfer-submit"
            disabled={!canSubmit}
            onClick={() => void submit()}
            className="inline-flex items-center gap-1.5 rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white hover:bg-[#003a6f] disabled:opacity-50"
          >
            <UserCheck className="h-4 w-4" aria-hidden />
            {t('visitRequestV2:hostTransfer.submit')}
          </button>
        </div>
      </div>
    </div>
  );
}
