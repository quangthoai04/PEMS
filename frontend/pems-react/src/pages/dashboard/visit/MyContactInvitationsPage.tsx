import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { AlertCircle, CalendarDays, Check, Loader2, Mail, MapPin, X } from 'lucide-react';
import {
  acceptMyOperationalContactInvitation,
  declineMyOperationalContactInvitation,
  getMyOperationalContactInvitations,
  type MyOperationalContactInvitation,
} from '../../../features/visit-request/api/visitRequestV2Api';
import { errorCodeOf } from '../../../features/visit-request/utils/visitV2Actions';
import { formatLocalizedDateTime, type UiLanguage } from '../../../shared/utils/vietnamTime';
import { showErrorToast, showMessageErrorToast, showSuccessToast } from '../../../shared/utils/toast';

/**
 * "Lời mời đầu mối của tôi" — the invitations addressed to the signed-in account's own address.
 *
 * Somebody who is already using PEMS and whose account IS the invited address had, until now, only
 * one way to answer: go back to their inbox and find the email. The link is still the path for an
 * external guest with no account; this is the path for everybody who is already here.
 *
 * <p>What this screen deliberately does NOT do is show the request. A pending invitee holds no
 * relation the system has granted — they are not the operational contact of anything until they
 * accept — so there is no detail route here and no attempt to open one. The backend refuses it
 * anyway ({@code VisitFormReadService}), and an address matching a form field has never been
 * evidence of authority. What the list carries is what a person needs in order to DECIDE: which
 * request, which campus, when, who is asking, and how long the invitation lasts.</p>
 *
 * <p>Every verdict is the backend's. The list only contains invitations that are still answerable —
 * PENDING, not overdue, on a live campus of a live request — so a row and its buttons cannot promise
 * something the accept would refuse. When one settles between the read and the click, the answer is
 * a stable error code, mapped below to the one true sentence and followed by a refresh.</p>
 */

type AnsweredOutcome = { status: 'ACCEPTED' | 'DECLINED'; message: string };

/** Stable backend codes → i18n key. Matched on the CODE; messages change, codes do not. */
const INVITATION_ERROR_KEYS: Record<string, string> = {
  OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED: 'visitRequestV2:contact.errorInvitationExpired',
  OPERATIONAL_CONTACT_CONFIRMATION_SUPERSEDED: 'visitRequestV2:contact.errorInvitationSuperseded',
  OPERATIONAL_CONTACT_CONFIRMATION_NOT_FOUND: 'visitRequestV2:contact.errorInvitationNotFound',
  OPERATIONAL_CONTACT_CHANGE_CONFLICT: 'visitRequestV2:contact.errorChangeConflict',
  OPERATIONAL_CONTACT_ALREADY_CONFIRMED: 'visitRequestV2:contact.errorAlreadyConfirmed',
  OPERATIONAL_CONTACT_EMAIL_MISMATCH: 'visitRequestV2:contact.errorEmailMismatch',
  OPERATIONAL_CONTACT_ACCOUNT_INACTIVE: 'visitRequestV2:contact.errorAccountInactive',
};

/** Codes that mean "this invitation is settled" — the row is stale and the list must be re-read. */
const SETTLED_CODES = new Set([
  'OPERATIONAL_CONTACT_CONFIRMATION_EXPIRED',
  'OPERATIONAL_CONTACT_CONFIRMATION_SUPERSEDED',
  'OPERATIONAL_CONTACT_CONFIRMATION_NOT_FOUND',
  'OPERATIONAL_CONTACT_CHANGE_CONFLICT',
  'OPERATIONAL_CONTACT_ALREADY_CONFIRMED',
]);

export function MyContactInvitationsPage() {
  const { t, i18n } = useTranslation(['visitRequestV2']);
  const language = i18n.language as UiLanguage;
  const navigate = useNavigate();

  const [items, setItems] = useState<MyOperationalContactInvitation[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadFailed, setLoadFailed] = useState(false);
  /** Which row has an answer in flight — one at a time, so a double click cannot post twice. */
  const [submittingId, setSubmittingId] = useState<number | null>(null);
  const [answered, setAnswered] = useState<Record<number, AnsweredOutcome>>({});
  const [declineFor, setDeclineFor] = useState<number | null>(null);
  const [declineReason, setDeclineReason] = useState('');

  // The ref is the authority on what has been answered, and it is written BEFORE the refresh that
  // follows an answer. Reading the state variable there would race: `load()` runs in the same async
  // turn as `setAnswered`, so it can observe the map from before the answer and drop the row the
  // backend has (correctly) stopped listing — leaving the user with no confirmation at all.
  const answeredRef = useRef<Record<number, AnsweredOutcome>>({});
  const markAnswered = (identityChangeId: number, outcome: AnsweredOutcome) => {
    answeredRef.current = { ...answeredRef.current, [identityChangeId]: outcome };
    setAnswered(answeredRef.current);
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const fresh = await getMyOperationalContactInvitations();
      setLoadFailed(false);
      setItems(prev => {
        const freshIds = new Set(fresh.map(i => i.identityChangeId));
        const keptAnswered = prev.filter(
          i => answeredRef.current[i.identityChangeId] && !freshIds.has(i.identityChangeId),
        );
        return [...keptAnswered, ...fresh];
      });
    } catch {
      setLoadFailed(true);
      setItems([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const answer = async (invitation: MyOperationalContactInvitation, action: 'accept' | 'decline') => {
    if (submittingId !== null) return;
    setSubmittingId(invitation.identityChangeId);
    try {
      await (action === 'accept'
        ? acceptMyOperationalContactInvitation(invitation.identityChangeId)
        : declineMyOperationalContactInvitation(
            invitation.identityChangeId, declineReason.trim() || undefined));

      // Ignore the backend's raw message — Vietnamese-only prose, not a stable code — and use the
      // same fixed, localized outcome text VisitContactInvitationPage uses for the identical action.
      const outcomeMessage = action === 'accept'
        ? t(invitation.kind === 'TRANSFER' ? 'visitRequestV2:contactInvitation.outcome.acceptSuccessTransfer' : 'visitRequestV2:contactInvitation.outcome.acceptSuccessClaim')
        : t('visitRequestV2:contactInvitation.outcome.declineSuccess');

      markAnswered(invitation.identityChangeId, {
        status: action === 'accept' ? 'ACCEPTED' : 'DECLINED',
        message: outcomeMessage,
      });
      setDeclineFor(null);
      setDeclineReason('');
      showSuccessToast(outcomeMessage);

      // Accepting is what actually creates the relation, so anything derived from it — this list, and
      // whatever the visit screens show — has to be re-read from the server rather than guessed at.
      await load();
    } catch (err: unknown) {
      const code = errorCodeOf(err);
      const key = code ? INVITATION_ERROR_KEYS[code] : undefined;
      if (key) showMessageErrorToast(t(key));
      else showErrorToast(err, t('visitRequestV2:myInvitations.actionFailed'));
      // The row was stale: re-read so it stops offering an answer nobody can give any more.
      if (code && SETTLED_CODES.has(code)) await load();
    } finally {
      setSubmittingId(null);
    }
  };

  const isExpired = (invitation: MyOperationalContactInvitation) =>
    new Date(invitation.expiresAt).getTime() <= Date.now();

  return (
    <div className="p-4 sm:p-6 md:p-8 pb-12 max-w-5xl mx-auto">
      <div className="mb-6 flex items-center text-sm font-medium text-gray-500">
        <button
          type="button"
          onClick={() => navigate('/dashboard/visit')}
          className="hover:text-[#004c91] transition-colors"
        >
          {t('visitRequestV2:myInvitations.breadcrumbVisit')}
        </button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91]">{t('visitRequestV2:myInvitations.title')}</span>
      </div>

      <div className="mb-6">
        <h1 className="text-2xl sm:text-3xl font-bold text-[#004c91]">
          {t('visitRequestV2:myInvitations.title')}
        </h1>
        <p className="mt-2 text-sm text-gray-600">{t('visitRequestV2:myInvitations.intro')}</p>
      </div>

      {loading ? (
        <div className="flex items-center justify-center gap-2 py-16 text-gray-400" role="status">
          <Loader2 className="w-5 h-5 animate-spin" />
          <span>{t('visitRequestV2:myInvitations.loading')}</span>
        </div>
      ) : loadFailed ? (
        <div className="rounded-xl border border-amber-300 bg-amber-50 p-4 text-sm font-normal text-amber-800" role="alert">
          {t('visitRequestV2:myInvitations.loadFailed')}
          <button
            type="button"
            onClick={() => void load()}
            className="mt-3 block rounded-lg border border-[#004c91] px-3 py-1.5 text-sm font-bold text-[#004c91] transition-colors hover:bg-[#004c91] hover:text-white"
          >
            {t('visitRequestV2:myInvitations.retry')}
          </button>
        </div>
      ) : items.length === 0 ? (
        <div className="rounded-2xl border border-gray-200 bg-white py-16 text-center">
          <Mail className="mx-auto mb-3 h-10 w-10 text-gray-300" />
          <p className="font-normal text-gray-700">{t('visitRequestV2:myInvitations.empty')}</p>
          <p className="mt-1 text-sm text-gray-500">{t('visitRequestV2:myInvitations.emptyHint')}</p>
        </div>
      ) : (
        <ul className="space-y-4">
          {items.map(invitation => {
            const outcome = answered[invitation.identityChangeId];
            const expired = !outcome && isExpired(invitation);
            const busy = submittingId === invitation.identityChangeId;
            const decliningThis = declineFor === invitation.identityChangeId;

            return (
              <li
                key={invitation.identityChangeId}
                className="rounded-2xl border border-gray-200 bg-white p-5 shadow-sm"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className="font-bold text-gray-800">
                        {invitation.delegationName || t('visitRequestV2:myInvitations.untitledDelegation')}
                      </span>
                      <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs font-bold text-gray-600">
                        {invitation.kind === 'TRANSFER'
                          ? t('visitRequestV2:myInvitations.kindTransfer')
                          : t('visitRequestV2:myInvitations.kindInitial')}
                      </span>
                    </div>
                    <div className="mt-2 flex flex-wrap gap-4 text-sm text-gray-600">
                      <span className="flex items-center gap-1">
                        <MapPin className="h-3.5 w-3.5 text-gray-400" />
                        {invitation.campusName ?? '—'}
                      </span>
                      <span className="flex items-center gap-1">
                        <CalendarDays className="h-3.5 w-3.5 text-gray-400" />
                        {formatLocalizedDateTime(invitation.plannedStartAt, language)} – {formatLocalizedDateTime(invitation.plannedEndAt, language)}
                      </span>
                    </div>
                    <dl className="mt-3 grid gap-1 text-sm sm:grid-cols-2">
                      <div className="flex gap-2">
                        <dt className="text-gray-500">{t('visitRequestV2:myInvitations.requestCode')}</dt>
                        <dd className="font-normal text-gray-800">{invitation.requestCode ?? '—'}</dd>
                      </div>
                      <div className="flex gap-2">
                        <dt className="text-gray-500">{t('visitRequestV2:myInvitations.registrant')}</dt>
                        <dd className="font-normal text-gray-800">
                          {invitation.registrantFullName ?? '—'}
                          {invitation.registrantOrganization ? ` — ${invitation.registrantOrganization}` : ''}
                        </dd>
                      </div>
                      <div className="flex gap-2">
                        <dt className="text-gray-500">{t('visitRequestV2:myInvitations.expiresAt')}</dt>
                        <dd className="font-normal text-gray-800">{formatLocalizedDateTime(invitation.expiresAt, language)}</dd>
                      </div>
                    </dl>
                  </div>

                  {outcome && (
                    <span
                      className={`rounded-full px-3 py-1 text-xs font-bold ${
                        outcome.status === 'ACCEPTED'
                          ? 'bg-[#eaffe4] text-[#0aa14f]'
                          : 'bg-gray-100 text-gray-600'
                      }`}
                    >
                      {outcome.status === 'ACCEPTED'
                        ? t('visitRequestV2:myInvitations.statusAccepted')
                        : t('visitRequestV2:myInvitations.statusDeclined')}
                    </span>
                  )}
                </div>

                {/* Answered rows keep their outcome and lose every decision control — the decision has
                    been made, and offering it again invites a second answer to a settled question. */}
                {outcome ? (
                  <p className="mt-4 rounded-lg bg-gray-50 p-3 text-sm text-gray-700" role="status">
                    {outcome.message}
                  </p>
                ) : expired ? (
                  <p className="mt-4 flex items-start gap-2 rounded-lg bg-amber-50 p-3 text-sm text-amber-800" role="alert">
                    <AlertCircle className="mt-0.5 h-4 w-4 flex-shrink-0" />
                    {t('visitRequestV2:contact.errorInvitationExpired')}
                  </p>
                ) : decliningThis ? (
                  <div className="mt-4 space-y-2">
                    <label
                      htmlFor={`decline-reason-${invitation.identityChangeId}`}
                      className="block text-xs font-semibold text-slate-500"
                    >
                      {t('visitRequestV2:myInvitations.declineReasonLabel')}
                    </label>
                    <textarea
                      id={`decline-reason-${invitation.identityChangeId}`}
                      rows={2}
                      maxLength={500}
                      value={declineReason}
                      onChange={e => setDeclineReason(e.target.value)}
                      className="w-full rounded-lg border border-slate-300 p-2 text-sm outline-none focus:border-[#004c91] focus:ring-1 focus:ring-[#004c91]"
                    />
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => void answer(invitation, 'decline')}
                        className="rounded-lg bg-red-600 px-4 py-2 text-sm font-bold text-white transition-colors hover:bg-red-700 disabled:opacity-50"
                      >
                        {busy
                          ? t('visitRequestV2:myInvitations.submitting')
                          : t('visitRequestV2:myInvitations.confirmDecline')}
                      </button>
                      <button
                        type="button"
                        disabled={busy}
                        onClick={() => { setDeclineFor(null); setDeclineReason(''); }}
                        className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-600 disabled:opacity-50"
                      >
                        {t('visitRequestV2:myInvitations.back')}
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="mt-4 flex flex-wrap gap-2">
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => void answer(invitation, 'accept')}
                      className="inline-flex items-center gap-1.5 rounded-lg bg-[#004c91] px-4 py-2 text-sm font-bold text-white transition-colors hover:bg-[#003a70] disabled:opacity-50"
                    >
                      <Check className="h-4 w-4" />
                      {busy
                        ? t('visitRequestV2:myInvitations.submitting')
                        : t('visitRequestV2:myInvitations.accept')}
                    </button>
                    <button
                      type="button"
                      disabled={busy}
                      onClick={() => { setDeclineFor(invitation.identityChangeId); setDeclineReason(''); }}
                      className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-600 transition-colors hover:bg-slate-50 disabled:opacity-50"
                    >
                      <X className="h-4 w-4" />
                      {t('visitRequestV2:myInvitations.decline')}
                    </button>
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}

export default MyContactInvitationsPage;
