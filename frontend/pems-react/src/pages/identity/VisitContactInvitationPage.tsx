import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../shared/hooks/useAuth';
import {
  acceptOperationalContactInvitation,
  declineOperationalContactInvitation,
  publicAcceptOperationalContactInvitation,
  publicDeclineOperationalContactInvitation,
  getOperationalContactInvitationInfo,
  type OperationalContactInvitationInfo,
} from '../../features/visit-request/api/visitRequestV2Api';
import { getApiErrorMessage } from '../../shared/utils/toast';
import { formatLocalizedDateTime, type UiLanguage } from '../../shared/utils/vietnamTime';

const INVITATION_STATUS_KEYS = new Set(['APPLIED', 'DECLINED', 'EXPIRED', 'CANCELLED', 'SUPERSEDED', 'INVALID']);

type InvitationKind = 'claim' | 'transfer';

interface Props {
  /**
   * Which wording to lead with. It is a HINT only: the invitation itself knows whether it is an
   * initial confirmation or a transfer, and the page re-reads that from the loaded record rather
   * than trusting the route — the same token answered through the wrong URL must still do the right
   * thing.
   */
  kind: InvitationKind;
}

type Info = OperationalContactInvitationInfo;

/**
 * Landing page for the per-campus operational-contact invitation links: INITIAL_CONFIRMATION (72h)
 * and TRANSFER (24h) both arrive here, because a link is a link and the invitation knows which it is.
 *
 * The anonymous GET only ever shows MASKED data for ONE campus — never a sibling campus and never
 * form content. Opening the link or logging in applies nothing: only the explicit "Đồng ý làm đầu
 * mối" POST does, and the backend requires the signed-in account's email to equal the invited
 * address exactly. Possession of a token proves nothing on its own.
 */
export default function VisitContactInvitationPage({ kind }: Props) {
  const { t, i18n } = useTranslation('visitRequestV2');
  const language = i18n.language as UiLanguage;
  const { token = '' } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated, user } = useAuth();

  const [info, setInfo] = useState<Info | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [outcome, setOutcome] = useState<{ ok: boolean; message: string } | null>(null);
  const [declineReason, setDeclineReason] = useState('');

  /**
   * WHICH answer this link is allowed to give — the only mutation the page may offer.
   *
   * An invitation email carries two links, one per answer, and each token is minted for exactly one of
   * them. Offering both buttons therefore invited the reader to press the one their own link cannot
   * perform: they clicked "Đồng ý" on a decline link, the backend refused it as
   * `CONFIRMATION_NOT_FOUND`, and the page told them the invitation was invalid — for an action they
   * had every right to take, through the other link, in the same email.
   *
   * `null` means "no mutation may be offered", and it is deliberately the answer to BOTH "already
   * settled" (`actionable === false`) and "the server did not say" (missing/unknown
   * `intendedAction`). Guessing in the unknown case is the failure this exists to prevent: an absent
   * field must not be read as permission to show everything.
   */
  const mutationAction: 'ACCEPT' | 'DECLINE' | null = useMemo(() => {
    if (!info || !info.actionable) return null;
    if (info.intendedAction === 'ACCEPT') return 'ACCEPT';
    if (info.intendedAction === 'DECLINE') return 'DECLINE';
    return null;
  }, [info]);

  // The loaded record is the authority on which kind this is; the route only decides what to show
  // during the first render, before anything has loaded.
  const effectiveKind: InvitationKind =
    info?.kind === 'TRANSFER' ? 'transfer' : info?.kind === 'INITIAL_CONFIRMATION' ? 'claim' : kind;

  const labels = useMemo(
    () =>
      effectiveKind === 'claim'
        ? {
            title: t('contactInvitation.claim.title'),
            intro: t('contactInvitation.claim.intro'),
            acceptCta: t('contactInvitation.claim.acceptCta'),
            windowNote: t('contactInvitation.claim.windowNote'),
          }
        : {
            title: t('contactInvitation.transfer.title'),
            intro: t('contactInvitation.transfer.intro'),
            acceptCta: t('contactInvitation.transfer.acceptCta'),
            windowNote: t('contactInvitation.transfer.windowNote'),
          },
    [effectiveKind, t],
  );

  const loadInfo = useCallback(async () => {
    setLoading(true);
    try {
      setInfo(await getOperationalContactInvitationInfo(token));
    } catch {
      setInfo(null);
    } finally {
      setLoading(false);
    }
  }, [kind, token]);

  useEffect(() => {
    void loadInfo();
  }, [loadInfo]);

  const act = async (action: 'accept' | 'decline') => {
    setSubmitting(true);
    try {
      // One pair of endpoints for both kinds: the invitation decides the effect, not the URL the
      // recipient happened to be sent.
      //
      // Signed in → the authenticated pair, where the session's address must match the invitation.
      // Not signed in → the PUBLIC pair, where the single-use, action-bound, address-bound token is
      // the authorization. The invited person is usually an external guest with no PEMS account, and
      // demanding one before they may answer is why invitations went unanswered and campuses stayed
      // behind the confirmation gate.
      await (action === 'accept'
        ? (isAuthenticated
            ? acceptOperationalContactInvitation(token)
            : publicAcceptOperationalContactInvitation(token))
        : (isAuthenticated
            ? declineOperationalContactInvitation(token, declineReason || undefined)
            : publicDeclineOperationalContactInvitation(token, declineReason || undefined)));
      const successMessage =
        action === 'accept'
          ? t(effectiveKind === 'transfer' ? 'contactInvitation.outcome.acceptSuccessTransfer' : 'contactInvitation.outcome.acceptSuccessClaim')
          : t('contactInvitation.outcome.declineSuccess');
      setOutcome({ ok: action === 'accept', message: successMessage });
    } catch (err: unknown) {
      setOutcome({ ok: false, message: getApiErrorMessage(err, t('contactInvitation.outcome.genericError')) });
    } finally {
      setSubmitting(false);
    }
  };

  const statusBanner = (status: string): string | null =>
    INVITATION_STATUS_KEYS.has(status) ? t(`contactInvitation.status.${status}`) : null;

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900">
        <p className="text-gray-500" role="status">{t('contactInvitation.loading')}</p>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gray-50 dark:bg-gray-900 flex items-center justify-center px-4 py-10">
      <div className="w-full max-w-lg bg-white dark:bg-gray-800 rounded-2xl shadow-lg p-6 sm:p-8">
        <h1 className="text-xl font-bold text-gray-900 dark:text-gray-50">{labels.title}</h1>
        <p className="mt-2 text-sm text-gray-600 dark:text-gray-300">{labels.intro}</p>

        {!info || info.status === 'INVALID' ? (
          <div className="mt-6 rounded-lg bg-red-50 dark:bg-red-900/30 p-4 text-sm text-red-700 dark:text-red-300" role="alert">
            {t('contactInvitation.invalidBanner')}
          </div>
        ) : outcome ? (
          <div
            className={`mt-6 rounded-lg p-4 text-sm ${outcome.ok ? 'bg-green-50 text-green-700 dark:bg-green-900/30 dark:text-green-300' : 'bg-amber-50 text-amber-800 dark:bg-amber-900/30 dark:text-amber-200'}`}
            role="status"
          >
            {outcome.message}
            {outcome.ok && (
              <button
                type="button"
                className="mt-4 block w-full rounded-lg bg-orange-600 px-4 py-2 font-medium text-white hover:bg-orange-700"
                onClick={() => navigate('/dashboard/visit')}
              >
                {t('contactInvitation.actions.goToManagement')}
              </button>
            )}
          </div>
        ) : (
          <>
            <dl className="mt-6 space-y-2 rounded-lg bg-gray-50 dark:bg-gray-700/40 p-4 text-sm">
              <div className="flex justify-between gap-4">
                <dt className="text-gray-500 dark:text-gray-400">{t('contactInvitation.fields.requestCode')}</dt>
                <dd className="font-normal text-gray-900 dark:text-gray-100">{info.requestCode ?? '—'}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt className="text-gray-500 dark:text-gray-400">{t('contactInvitation.fields.delegation')}</dt>
                <dd className="font-normal text-gray-900 dark:text-gray-100">{info.delegationName ?? '—'}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt className="text-gray-500 dark:text-gray-400">{t('contactInvitation.fields.invitedEmail')}</dt>
                <dd className="font-normal text-gray-900 dark:text-gray-100">{info.maskedEmail ?? '—'}</dd>
              </div>
              {/* The campus this invitation is about. It is the whole point of a per-campus
                  invitation: accepting binds the recipient to THIS campus and no other. */}
              {info.campusName ? (
                <div className="flex justify-between gap-4">
                  <dt className="text-gray-500 dark:text-gray-400">{t('contactInvitation.fields.campus')}</dt>
                  <dd className="font-normal text-gray-900 dark:text-gray-100">{info.campusName}</dd>
                </div>
              ) : null}
              {info.expiresAt && (
                <div className="flex justify-between gap-4">
                  <dt className="text-gray-500 dark:text-gray-400">{t('contactInvitation.fields.expiresAt')}</dt>
                  <dd className="font-normal text-gray-900 dark:text-gray-100">
                    {formatLocalizedDateTime(info.expiresAt, language)}
                  </dd>
                </div>
              )}
            </dl>
            <p className="mt-2 text-xs text-gray-400">{labels.windowNote}</p>

            {statusBanner(info.status) ? (
              <div className="mt-4 rounded-lg bg-amber-50 dark:bg-amber-900/30 p-4 text-sm text-amber-800 dark:text-amber-200" role="alert">
                {statusBanner(info.status)}
              </div>
            ) : mutationAction === null ? (
              /* Nothing this link may do — either the invitation is settled or the server did not say
                 which answer the link carries. Both end here rather than in a pair of buttons, because
                 a button the token cannot honour is worse than no button: it spends the reader's one
                 attempt and answers them with an error about a link that is perfectly valid. */
              <div className="mt-4 rounded-lg bg-amber-50 dark:bg-amber-900/30 p-4 text-sm text-amber-800 dark:text-amber-200" role="alert">
                {t('contactInvitation.actions.noActionAvailable')}
              </div>
            ) : (
              <div className="mt-6 space-y-3">
                {/* Đăng nhập là LỰA CHỌN, không phải rào chắn. Liên kết trong email đã là bằng chứng
                    đủ mạnh cho đúng một hành động này: dùng-một-lần, gắn đúng một câu trả lời, và gắn
                    email do NGƯỜI ĐĂNG KÝ chọn. Bắt khách bên ngoài tạo tài khoản Google trước khi
                    được nói có/không chính là lý do lời mời không ai trả lời và cơ sở kẹt ở cổng xác
                    nhận. Ai đã có tài khoản PEMS thì vẫn đi đường đăng nhập như cũ. */}
                {isAuthenticated ? (
                  <p className="text-sm text-gray-600 dark:text-gray-300">
                    {t('contactInvitation.auth.signedInAs', {
                      identity: user?.email ?? user?.fullName ?? t('contactInvitation.auth.currentAccountFallback'),
                    })}
                  </p>
                ) : (
                  <div className="space-y-2">
                    <p className="text-sm text-gray-600 dark:text-gray-300" role="note">
                      {t('contactInvitation.auth.anonymousNote', { email: info.maskedEmail ?? '' })}
                    </p>
                    <button
                      type="button"
                      className="w-full rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-200 dark:hover:bg-gray-700"
                      onClick={() => navigate(`/?login=true&returnUrl=${encodeURIComponent(window.location.pathname)}`)}
                    >
                      {t('contactInvitation.auth.loginCta')}
                    </button>
                  </div>
                )}
                {/* One link, one answer. The reader already chose in the email by pressing a button
                    there; this page confirms that choice with the latest visit details in front of
                    them, it does not re-open the question. */}
                {mutationAction === 'DECLINE' ? (
                  <div className="space-y-2">
                    <label htmlFor="decline-reason" className="text-sm text-gray-600 dark:text-gray-300">
                      {t('contactInvitation.actions.declineReasonLabel')}
                    </label>
                    <textarea
                      id="decline-reason"
                      className="w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 p-2 text-sm"
                      rows={2}
                      maxLength={500}
                      value={declineReason}
                      onChange={e => setDeclineReason(e.target.value)}
                    />
                    <button
                      type="button"
                      disabled={submitting}
                      className="w-full rounded-lg bg-red-600 px-4 py-2 font-medium text-white hover:bg-red-700 disabled:opacity-50"
                      onClick={() => void act('decline')}
                    >
                      {submitting ? t('contactInvitation.actions.processing') : t('contactInvitation.actions.confirmDecline')}
                    </button>
                  </div>
                ) : (
                  <button
                    type="button"
                    disabled={submitting}
                    className="w-full rounded-lg bg-orange-600 px-4 py-2 font-medium text-white hover:bg-orange-700 disabled:opacity-50"
                    onClick={() => void act('accept')}
                  >
                    {submitting ? t('contactInvitation.actions.processing') : labels.acceptCta}
                  </button>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
