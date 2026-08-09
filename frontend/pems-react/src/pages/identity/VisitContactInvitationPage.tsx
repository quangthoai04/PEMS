import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../shared/hooks/useAuth';
import {
  acceptOperationalContactInvitation,
  declineOperationalContactInvitation,
  publicAcceptOperationalContactInvitation,
  publicDeclineOperationalContactInvitation,
  getOperationalContactInvitationInfo,
  type OperationalContactInvitationInfo,
} from '../../features/visit-request/api/visitRequestV2Api';

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
            title: 'Lời mời làm đầu mối liên hệ',
            intro: 'Bạn được chỉ định làm đầu mối liên hệ cho đơn đăng ký tham quan dưới đây.',
            acceptCta: 'Đồng ý làm đầu mối',
            windowNote: 'Lời mời có hiệu lực 72 giờ kể từ khi gửi.',
          }
        : {
            title: 'Lời mời tiếp nhận vai trò đầu mối',
            intro:
              'Bạn được đề nghị TIẾP NHẬN vai trò đầu mối liên hệ thay cho đầu mối hiện tại. Đầu mối hiện tại vẫn giữ nguyên quyền cho tới khi bạn xác nhận.',
            acceptCta: 'Đồng ý tiếp nhận vai trò',
            windowNote: 'Lời mời có hiệu lực 24 giờ kể từ khi gửi.',
          },
    [effectiveKind],
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
      const result = action === 'accept'
        ? (isAuthenticated
            ? await acceptOperationalContactInvitation(token)
            : await publicAcceptOperationalContactInvitation(token))
        : (isAuthenticated
            ? await declineOperationalContactInvitation(token, declineReason || undefined)
            : await publicDeclineOperationalContactInvitation(token, declineReason || undefined));
      setOutcome({ ok: action === 'accept', message: result.message });
    } catch (err: unknown) {
      const message =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        'Không thể xử lý lời mời. Vui lòng kiểm tra tài khoản đăng nhập và thử lại.';
      setOutcome({ ok: false, message });
    } finally {
      setSubmitting(false);
    }
  };

  const statusBanner = (status: string) => {
    const map: Record<string, string> = {
      APPLIED: 'Lời mời này đã được chấp nhận trước đó.',
      DECLINED: 'Lời mời này đã bị từ chối.',
      EXPIRED: 'Lời mời đã hết hạn. Vui lòng đề nghị người đăng ký gửi lại lời mời mới.',
      CANCELLED: 'Lời mời đã bị hủy.',
      SUPERSEDED: 'Lời mời này đã được thay bằng một lời mời mới hơn — hãy dùng liên kết trong email mới nhất.',
      INVALID: 'Liên kết không hợp lệ hoặc không còn tồn tại.',
    };
    return map[status] ?? null;
  };

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900">
        <p className="text-gray-500" role="status">Đang tải lời mời…</p>
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
            Liên kết không hợp lệ hoặc không còn tồn tại.
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
                Vào trang quản lý đơn
              </button>
            )}
          </div>
        ) : (
          <>
            <dl className="mt-6 space-y-2 rounded-lg bg-gray-50 dark:bg-gray-700/40 p-4 text-sm">
              <div className="flex justify-between gap-4">
                <dt className="text-gray-500 dark:text-gray-400">Mã đơn</dt>
                <dd className="font-medium text-gray-900 dark:text-gray-100">{info.requestCode ?? '—'}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt className="text-gray-500 dark:text-gray-400">Đoàn khách</dt>
                <dd className="font-medium text-gray-900 dark:text-gray-100">{info.delegationName ?? '—'}</dd>
              </div>
              <div className="flex justify-between gap-4">
                <dt className="text-gray-500 dark:text-gray-400">Email được mời</dt>
                <dd className="font-medium text-gray-900 dark:text-gray-100">{info.maskedEmail ?? '—'}</dd>
              </div>
              {/* The campus this invitation is about. It is the whole point of a per-campus
                  invitation: accepting binds the recipient to THIS campus and no other. */}
              {info.campusName ? (
                <div className="flex justify-between gap-4">
                  <dt className="text-gray-500 dark:text-gray-400">Cơ sở</dt>
                  <dd className="font-medium text-gray-900 dark:text-gray-100">{info.campusName}</dd>
                </div>
              ) : null}
              {info.expiresAt && (
                <div className="flex justify-between gap-4">
                  <dt className="text-gray-500 dark:text-gray-400">Hiệu lực đến</dt>
                  <dd className="font-medium text-gray-900 dark:text-gray-100">
                    {new Date(info.expiresAt).toLocaleString('vi-VN')}
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
                Liên kết này hiện không thực hiện được thao tác nào. Vui lòng mở lại liên kết trong email mới
                nhất, hoặc đề nghị người đăng ký gửi lại lời mời.
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
                    Đang đăng nhập: <b>{user?.email ?? user?.fullName ?? 'tài khoản hiện tại'}</b>. Nếu đây không phải
                    email được mời, hãy đăng xuất và đăng nhập đúng tài khoản trước khi xác nhận.
                  </p>
                ) : (
                  <div className="space-y-2">
                    <p className="text-sm text-gray-600 dark:text-gray-300" role="note">
                      Bạn <b>không cần đăng nhập</b> để trả lời lời mời này — liên kết trong email đã xác định
                      người nhận ({info.maskedEmail}) và chỉ dùng được một lần.
                    </p>
                    <button
                      type="button"
                      className="w-full rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-600 dark:text-gray-200 dark:hover:bg-gray-700"
                      onClick={() => navigate(`/?login=true&returnUrl=${encodeURIComponent(window.location.pathname)}`)}
                    >
                      Hoặc đăng nhập bằng Google (nếu bạn đã có tài khoản PEMS)
                    </button>
                  </div>
                )}
                {/* One link, one answer. The reader already chose in the email by pressing a button
                    there; this page confirms that choice with the latest visit details in front of
                    them, it does not re-open the question. */}
                {mutationAction === 'DECLINE' ? (
                  <div className="space-y-2">
                    <label htmlFor="decline-reason" className="text-sm text-gray-600 dark:text-gray-300">
                      Lý do từ chối (không bắt buộc)
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
                      {submitting ? 'Đang xử lý…' : 'Xác nhận từ chối'}
                    </button>
                  </div>
                ) : (
                  <button
                    type="button"
                    disabled={submitting}
                    className="w-full rounded-lg bg-orange-600 px-4 py-2 font-medium text-white hover:bg-orange-700 disabled:opacity-50"
                    onClick={() => void act('accept')}
                  >
                    {submitting ? 'Đang xử lý…' : labels.acceptCta}
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
