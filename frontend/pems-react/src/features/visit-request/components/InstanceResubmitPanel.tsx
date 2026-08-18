import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { RotateCcw } from 'lucide-react';
import { resubmitVisitInstance, type ResolvedCampusVisit } from '../api/visitRequestV2Api';
import { hasAction, VisitV2Action } from '../utils/visitV2Actions';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';

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
 * <p>The schedule is the only thing offered for editing here. It is what a refusal is usually about,
 * it is genuinely instance-local, and the 72-hour floor that applies to a resubmission is a rule about
 * exactly this field. Everything else is sent back unchanged, so a resubmission cannot quietly become
 * a rewrite of the delegation.</p>
 */
export default function InstanceResubmitPanel({ visitRequestId, campusVisit, onResubmitted }: Props) {
  const { t } = useTranslation(['visitRequestV2', 'errors']);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [startAt, setStartAt] = useState(() => toLocalInput(campusVisit.plannedStartAt));
  const [endAt, setEndAt] = useState(() => toLocalInput(campusVisit.plannedEndAt));

  if (!hasAction(campusVisit.allowedActions, VisitV2Action.ResubmitRejectedInstance)) return null;

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await resubmitVisitInstance(visitRequestId, campusVisit.visitInstanceId, {
        // The instance's OWN version, which is what the backend guards on: a sibling campus being
        // decided bumps the REQUEST version and must not make this submission look stale.
        visitInstanceId: campusVisit.visitInstanceId,
        expectedRowVersion: campusVisit.rowVersion,
        campusId: campusVisit.campusCode,
        plannedStartAt: new Date(startAt).toISOString(),
        plannedEndAt: new Date(endAt).toISOString(),
        delegationName: campusVisit.delegationName,
        visitType: campusVisit.visitType,
        visitTypeOther: campusVisit.visitTypeOther,
        purpose: campusVisit.purpose,
        workingContent: campusVisit.workingContent,
        visitors: campusVisit.visitors.map((v) => ({
          fullName: v.fullName,
          nationality: v.nationality,
          jobTitle: v.jobTitle,
          organization: v.organization,
        })),
        externalSupportMembers: campusVisit.supportMembers.map((m) => ({
          fullName: m.fullName,
          nationality: m.nationality,
          jobTitle: m.jobTitle,
          organization: m.organization,
        })),
        // Sent back exactly as stored. The contact snapshot is managed in its own workflow, and the
        // backend refuses a resubmission that tries to change it.
        operationalContact: {
          fullName: campusVisit.operationalContact.fullName ?? '',
          organization: campusVisit.operationalContact.organization ?? '',
          jobTitle: campusVisit.operationalContact.jobTitle ?? '',
          phone: campusVisit.operationalContact.phone ?? '',
          email: campusVisit.operationalContact.email ?? '',
        },
        workingLanguage: campusVisit.workingLanguage,
        transportationNote: campusVisit.transportationNote,
        mediaConsentStatus: campusVisit.mediaConsentStatus,
        notes: campusVisit.notes,
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

/** ISO instant → the `datetime-local` shape, without dragging in a date library. */
function toLocalInput(iso: string): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** The server's own message, which for a lead-time refusal names the earliest legal start. */
function messageOf(error: unknown): string | null {
  const data = (error as { response?: { data?: { message?: string } } })?.response?.data;
  return typeof data?.message === 'string' && data.message.length > 0 ? data.message : null;
}
