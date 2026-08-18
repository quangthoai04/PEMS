import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { UserCog } from 'lucide-react';
import {
  syncOwnAccountProfile,
  type OperationalContactProfileDifference,
} from '../api/visitRequestV2Api';
import { showErrorToast, showSuccessToast } from '../../../shared/utils/toast';

interface Props {
  difference: OperationalContactProfileDifference;
  /** Re-reads the contact card so the prompt disappears once there is nothing left to reconcile. */
  onSynced: () => void;
}

/**
 * Offers the signed-in operational contact the chance to bring their PEMS profile in line with how
 * this visit describes them.
 *
 * Three things it deliberately is not:
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
export default function ContactProfileSyncPrompt({ difference, onSynced }: Props) {
  const { t } = useTranslation(['visitRequestV2']);
  const [dismissed, setDismissed] = useState(false);
  const [busy, setBusy] = useState(false);

  if (dismissed) return null;

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
      setDismissed(true);
      onSynced();
    } catch (error) {
      showErrorToast(error, t('visitRequestV2:profileSync.updateFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div
      data-testid="contact-profile-sync-prompt"
      className="mb-3 rounded-xl border border-sky-300 bg-sky-50 p-3"
    >
      <div className="flex items-start gap-2">
        <UserCog className="mt-0.5 h-4 w-4 shrink-0 text-sky-700" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-normal text-sky-900">
            {t('visitRequestV2:profileSync.question')}
          </p>

          <dl className="mt-2 grid grid-cols-1 gap-x-6 gap-y-1 text-xs sm:grid-cols-2">
            {difference.fullNameDiffers && (
              <div className="min-w-0">
                <dt className="font-medium text-sky-800">{t('visitRequestV2:person.fullName')}</dt>
                <dd className="break-words text-slate-700" data-testid="profile-sync-fullname">
                  {difference.accountFullName || '—'} → {difference.snapshotFullName || '—'}
                </dd>
              </div>
            )}
            {difference.phoneDiffers && (
              <div className="min-w-0">
                <dt className="font-medium text-sky-800">{t('visitRequestV2:card.phone')}</dt>
                <dd className="break-words text-slate-700" data-testid="profile-sync-phone">
                  {difference.accountPhone || '—'} → {difference.snapshotPhone || '—'}
                </dd>
              </div>
            )}
          </dl>

          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              data-testid="profile-sync-apply"
              disabled={busy}
              onClick={() => void apply()}
              className="rounded-xl bg-sky-700 px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-sky-800 disabled:opacity-50"
            >
              {t('visitRequestV2:profileSync.apply')}
            </button>
            {/* Keeping the profile is a real answer, not a postponement: it writes nothing, anywhere. */}
            <button
              type="button"
              data-testid="profile-sync-keep"
              disabled={busy}
              onClick={() => setDismissed(true)}
              className="rounded-xl border-2 border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50 disabled:opacity-50"
            >
              {t('visitRequestV2:profileSync.keep')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
