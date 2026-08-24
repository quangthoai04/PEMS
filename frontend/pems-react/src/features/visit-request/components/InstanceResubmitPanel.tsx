import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RotateCcw } from 'lucide-react';
import { resubmitVisitInstance, type ResolvedCampusVisit } from '../api/visitRequestV2Api';
import { hasAction, VisitV2Action } from '../utils/visitV2Actions';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';
import { fromDateTimeLocalInput, toVietnamDateTimeLocalInput } from '../../../shared/utils/vietnamTime';

interface Props {
  visitRequestId: number;
  campusVisit: ResolvedCampusVisit;
  /** Re-reads the request so the campus shows its new status and version. */
  onResubmitted: () => void;
}

/**
 * Lets the guest side of ONE refused campus fix what was wrong and ask for it to be looked at again.
 *
 * <p>Who sees it is decided by the server. The panel renders only when the read model put
 * `RESUBMIT_REJECTED_INSTANCE` in THIS campus's `allowedActions`, which the backend grants to the
 * registrant and to the person who confirmed this campus — never from a role. The holder of a sibling
 * campus receives the action on their own campus and not on this one, so the button simply is not
 * there for them.</p>
 *
 * <p>It calls the per-instance endpoint, never the request-wide `/resubmit`. That one demands every
 * campus be rejected and resets all of them, which would pull an approved sibling — its host, its
 * schedule — back into review because a different campus said no.</p>
 *
 * <p>The schedule is the only thing offered for editing here, and (plan CanhIter3FixBug FIX-G/H) it is
 * now the only thing the payload carries at all — content, member lists and the operational contact are
 * never sent, because the backend never reads them for this action any more. A resubmission cannot
 * become a rewrite of the delegation because there is nothing left in the request for it to rewrite
 * with: `OrganizationPartnerId`, every member's identity and the contact link all stay exactly the rows
 * they already were.</p>
 */
export default function InstanceResubmitPanel({ visitRequestId, campusVisit, onResubmitted }: Props) {
  const { t } = useTranslation(['visitRequestV2', 'errors']);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [startAt, setStartAt] = useState(() => toVietnamDateTimeLocalInput(campusVisit.plannedStartAt));
  const [endAt, setEndAt] = useState(() => toVietnamDateTimeLocalInput(campusVisit.plannedEndAt));

  if (!hasAction(campusVisit.allowedActions, VisitV2Action.ResubmitRejectedInstance)) return null;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      // SCHEDULE-ONLY (plan FIX-G/H): this is the whole payload now. The backend no longer reads
      // content/member/contact fields for a resubmit at all, so nothing here echoes them back —
      // there is no snapshot left to go stale, drop a partner id, or re-guess a contact link from.
      await resubmitVisitInstance(visitRequestId, campusVisit.visitInstanceId, {
        // The instance's OWN version, which is what the backend guards on: a sibling campus being
        // decided bumps the REQUEST version and must not make this submission look stale.
        expectedRowVersion: campusVisit.rowVersion,
        campusId: campusVisit.campusCode,
        // Bare Vietnam wall-clock strings — never new Date(...).toISOString(), which would reinterpret
        // the datetime-local value through the BROWSER's own timezone and shift the visit's actual time
        // for anyone not on Asia/Ho_Chi_Minh (plan FIX-I).
        plannedStartAt: fromDateTimeLocalInput(startAt) ?? startAt,
        plannedEndAt: fromDateTimeLocalInput(endAt) ?? endAt,
      });

      // One toast, raised here where the call succeeded — not by a parent that also re-renders.
      showSuccessToast(t('visitRequestV2:instanceResubmit.success'));
      setOpen(false);
      onResubmitted();
    } catch (err) {
      // The canonical backend message is shown as-is: the 72-hour refusal names the earliest legal
      // start, and a locally invented sentence could not.
      const message = messageOf(err) ?? t('visitRequestV2:instanceResubmit.failed');
      setError(message);
      showErrorToast(err, t('visitRequestV2:instanceResubmit.failed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      data-testid={`instance-resubmit-${campusVisit.visitInstanceId}`}
      className="mt-4 rounded-xl border border-amber-300 bg-amber-50 p-3"
    >
      <p className="text-sm font-normal text-amber-900">
        {t('visitRequestV2:instanceResubmit.title')}
      </p>

      {/* The reason this campus was refused, so the person fixing it can see what to fix. */}
      {campusVisit.decisionNote && (
        <p
          className="mt-1 text-sm text-slate-700"
          data-testid={`instance-resubmit-reason-${campusVisit.visitInstanceId}`}
        >
          {t('visitRequestV2:instanceResubmit.reason')}: {campusVisit.decisionNote}
        </p>
      )}

      {!open ? (
        <button
          type="button"
          data-testid={`instance-resubmit-open-${campusVisit.visitInstanceId}`}
          onClick={() => setOpen(true)}
          className="mt-3 inline-flex items-center gap-1.5 rounded-xl bg-amber-700 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-amber-800"
        >
          <RotateCcw className="h-4 w-4" /> {t('visitRequestV2:instanceResubmit.open')}
        </button>
      ) : (
        <div className="mt-3 space-y-3">
          <p className="text-xs text-slate-600">{t('visitRequestV2:instanceResubmit.leadTimeHint')}</p>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <label className="block">
              <span className="text-xs font-normal text-slate-600">
                {t('visitRequestV2:card.startAt')}
              </span>
              <input
                type="datetime-local"
                data-testid={`instance-resubmit-start-${campusVisit.visitInstanceId}`}
                value={startAt}
                onChange={(e) => setStartAt(e.target.value)}
                className="mt-1 w-full rounded-xl border-2 border-slate-200 px-3 py-2 text-sm"
              />
            </label>
            <label className="block">
              <span className="text-xs font-normal text-slate-600">
                {t('visitRequestV2:card.endAt')}
              </span>
              <input
                type="datetime-local"
                data-testid={`instance-resubmit-end-${campusVisit.visitInstanceId}`}
                value={endAt}
                onChange={(e) => setEndAt(e.target.value)}
                className="mt-1 w-full rounded-xl border-2 border-slate-200 px-3 py-2 text-sm"
              />
            </label>
          </div>

          {error && (
            <p
              role="alert"
              data-testid={`instance-resubmit-error-${campusVisit.visitInstanceId}`}
              className="rounded-xl border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-800"
            >
              {error}
            </p>
          )}

          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              data-testid={`instance-resubmit-submit-${campusVisit.visitInstanceId}`}
              disabled={busy}
              onClick={() => void submit()}
              className="rounded-xl bg-amber-700 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-amber-800 disabled:opacity-50"
            >
              {t('visitRequestV2:instanceResubmit.submit')}
            </button>
            <button
              type="button"
              data-testid={`instance-resubmit-cancel-${campusVisit.visitInstanceId}`}
              disabled={busy}
              onClick={() => setOpen(false)}
              className="rounded-xl border-2 border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50 disabled:opacity-50"
            >
              {t('visitRequestV2:instanceResubmit.cancel')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}

/** The server's own message, which for a lead-time refusal names the earliest legal start. */
function messageOf(error: unknown): string | null {
  const data = (error as { response?: { data?: { message?: string } } })?.response?.data;
  return typeof data?.message === 'string' && data.message.length > 0 ? data.message : null;
}
