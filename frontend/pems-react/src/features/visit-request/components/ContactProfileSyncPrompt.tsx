import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { BadgeAlert, Loader2 } from 'lucide-react';
import {
  syncOwnAccountProfile,
  type OperationalContactProfileDifference,
} from '../api/visitRequestV2Api';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';

interface Props {
  /** Scopes the trigger/popover ids so a two-campus screen addresses each one independently. */
  visitInstanceId: number;
  difference: OperationalContactProfileDifference;
  /** Re-reads the contact card so the icon disappears once there is nothing left to reconcile. */
  onSynced: () => void;
}

/**
 * The reconciliation offer, as a two-level disclosure (UI mức icon → popover):
 *
 * - **Mức 1** is a small amber "profile needs attention" icon (`BadgeAlert` — deliberately NOT an
 *   info/help glyph, which reads as "explanation available" rather than "something to decide") next
 *   to the contact card's title — present only while `profileDifference` says there is something to
 *   reconcile, silent otherwise.
 * - **Mức 2** is a popover the icon opens: the question, the field(s) that differ, and the two
 *   actions. Nothing in between — no third "xem thay đổi" step.
 *
 * Three things this is deliberately not:
 *
 * - **Not a correction.** Both values are legitimate. A delegation may list a translated title or a
 *   desk number that applies to one visit only, and the account is who the person is everywhere else.
 *   So this is an offer with a real "leave it" answer, not a validation error to clear.
 * - **Not visible to anyone else.** The server sends `profileDifference` only to the account the
 *   campus's contact relation points at, so a registrant never gets the chance to tidy up somebody
 *   else's identity from a form they happened to fill in.
 * - **Not a rewrite of history.** It copies the CURRENT snapshot onto the account and stops there.
 *   Past visits keep saying what they were told, because a visit that happened is a record and not a
 *   view of today's directory.
 *
 * Only full name and phone are on offer: the users table has no organization or job-title column, and
 * email is the account's identity rather than one of its fields.
 */
export default function ContactProfileSyncPrompt({ visitInstanceId, difference, onSynced }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const popoverRef = useRef<HTMLDivElement>(null);
  const popoverId = `profile-sync-popover-${visitInstanceId}`;

  // Closes on an outside click or Escape — the only two ways to dismiss besides the buttons
  // themselves. Listened for only while open, so a closed popover never pays for a global listener.
  useEffect(() => {
    if (!open) return undefined;
    const handlePointerDown = (e: MouseEvent) => {
      const target = e.target as Node;
      if (popoverRef.current?.contains(target) || triggerRef.current?.contains(target)) return;
      setOpen(false);
    };
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setOpen(false);
    };
    document.addEventListener('mousedown', handlePointerDown);
    document.addEventListener('keydown', handleKeyDown);
    return () => {
      document.removeEventListener('mousedown', handlePointerDown);
      document.removeEventListener('keydown', handleKeyDown);
    };
  }, [open]);

  const apply = async () => {
    setBusy(true);
    try {
      // Only the fields that actually differ are sent. Sending an unchanged value would be a write
      // nobody asked for, and would put this action in the account's audit trail for no reason.
      await syncOwnAccountProfile({
        ...(difference.fullNameDiffers ? { fullName: difference.snapshotFullName ?? '' } : {}),
        ...(difference.phoneDiffers ? { phone: difference.snapshotPhone ?? '' } : {}),
      });
      showSuccessToast(t('visitRequestV2:profileSync.updated'));
      setOpen(false);
      onSynced();
    } catch (error) {
      showErrorToast(error, t('visitRequestV2:profileSync.updateFailed'));
    } finally {
      setBusy(false);
    }
  };

  // Keeping the profile is a real answer, not a postponement: it writes nothing, anywhere.
  const dismiss = () => setOpen(false);

  return (
    <div className="relative inline-flex">
      <button
        type="button"
        ref={triggerRef}
        data-testid={`profile-sync-trigger-${visitInstanceId}`}
        aria-label={t('visitRequestV2:profileSync.tooltip')}
        title={t('visitRequestV2:profileSync.tooltip')}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-controls={popoverId}
        className={
          'inline-flex h-7 w-7 items-center justify-center rounded-full text-amber-600 transition-colors '
          + 'hover:bg-amber-50 hover:text-amber-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-amber-400 '
          + (open ? 'bg-amber-50' : '')
        }
        onClick={() => setOpen(v => !v)}
      >
        <BadgeAlert className="h-4 w-4" aria-hidden />
      </button>

      {open && (
        <div
          id={popoverId}
          role="dialog"
          aria-label={t('visitRequestV2:profileSync.tooltip')}
          ref={popoverRef}
          data-testid={`profile-sync-popover-${visitInstanceId}`}
          className="absolute left-0 top-full z-30 mt-2 w-[320px] max-w-[calc(100vw-2rem)] rounded-xl border border-slate-200 bg-white p-3 shadow-xl sm:w-[380px]"
        >
          <p className="text-sm font-normal text-slate-700">
            {t('visitRequestV2:profileSync.question')}
          </p>

          <dl className="mt-2 grid grid-cols-1 gap-y-2 text-xs">
            {difference.fullNameDiffers && (
              <div className="min-w-0">
                <dt className="font-medium text-slate-500">{t('visitRequestV2:person.fullName')}</dt>
                <dd className="break-words text-slate-800" data-testid="profile-sync-fullname">
                  {difference.accountFullName || '—'} → {difference.snapshotFullName || '—'}
                </dd>
              </div>
            )}
            {difference.phoneDiffers && (
              <div className="min-w-0">
                <dt className="font-medium text-slate-500">{t('visitRequestV2:card.phone')}</dt>
                <dd className="break-words text-slate-800" data-testid="profile-sync-phone">
                  {difference.accountPhone || '—'} → {difference.snapshotPhone || '—'}
                </dd>
              </div>
            )}
          </dl>

          <div className="mt-3 flex justify-end gap-2">
            <button
              type="button"
              data-testid="profile-sync-keep"
              disabled={busy}
              onClick={dismiss}
              className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-50 disabled:opacity-50"
            >
              {t('visitRequestV2:profileSync.keep')}
            </button>
            <button
              type="button"
              data-testid="profile-sync-apply"
              disabled={busy}
              onClick={() => void apply()}
              className="inline-flex items-center gap-1.5 rounded-lg bg-sky-700 px-3 py-1.5 text-xs font-semibold text-white transition-colors hover:bg-sky-800 disabled:opacity-50"
            >
              {busy && <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />}
              {t('visitRequestV2:profileSync.apply')}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
