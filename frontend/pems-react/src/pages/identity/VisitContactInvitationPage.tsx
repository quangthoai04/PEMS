import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAuth } from '../../shared/hooks/useAuth';
import {
  acceptContactClaim,
  acceptContactTransfer,
  declineContactClaim,
  declineContactTransfer,
  getContactClaimInfo,
  getContactTransferInfo,
  type ContactClaimInfo,
  type ContactTransferInfo,
} from '../../features/visit-request/api/visitRequestV2Api';

type InvitationKind = 'claim' | 'transfer';

interface Props {
  kind: InvitationKind;
}

type Info = (ContactClaimInfo | ContactTransferInfo) & { requestedByName?: string | null };

/**
 * Landing page for the primary-contact INITIAL_CLAIM (72h) and TRANSFER (24h) email links.
 * The anonymous GET only ever shows MASKED data; opening the link or logging in never applies
 * anything — only the explicit "Đồng ý làm đầu mối" POST does, and the backend requires the
 * logged-in Google account's email to equal the invited email exactly.
 */
export default function VisitContactInvitationPage({ kind }: Props) {
  const { token = '' } = useParams();
  const navigate = useNavigate();
  const { isAuthenticated, user } = useAuth();

  const [info, setInfo] = useState<Info | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [outcome, setOutcome] = useState<{ ok: boolean; message: string } | null>(null);
  const [declineMode, setDeclineMode] = useState(false);
  const [declineReason, setDeclineReason] = useState('');

  const labels = useMemo(
    () =>
      kind === 'claim'
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
    [kind],
  );

  const loadInfo = useCallback(async () => {
    setLoading(true);
    try {
      const data = kind === 'claim' ? await getContactClaimInfo(token) : await getContactTransferInfo(token);
      setInfo(data as Info);
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
      const result =
        kind === 'claim'
          ? action === 'accept'
            ? await acceptContactClaim(token)
            : await declineContactClaim(token, declineReason || undefined)
          : action === 'accept'
            ? await acceptContactTransfer(token)
            : await declineContactTransfer(token, declineReason || undefined);
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
              {'requestedByName' in info && info.requestedByName ? (
                <div className="flex justify-between gap-4">
                  <dt className="text-gray-500 dark:text-gray-400">Người đề nghị</dt>
                  <dd className="font-medium text-gray-900 dark:text-gray-100">{info.requestedByName}</dd>
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
            ) : !isAuthenticated ? (
              <div className="mt-6 space-y-3">
                <p className="text-sm text-gray-600 dark:text-gray-300" role="note">
                  Để xác nhận, hãy đăng nhập bằng <b>đúng tài khoản Google của email được mời</b> ({info.maskedEmail}).
                  Đăng nhập không tự động chấp nhận lời mời — bạn vẫn phải bấm xác nhận sau đó.
                </p>
                <button
                  type="button"
                  className="w-full rounded-lg bg-orange-600 px-4 py-2 font-medium text-white hover:bg-orange-700"
                  onClick={() => navigate(`/login?returnUrl=${encodeURIComponent(window.location.pathname)}`)}
                >
                  Đăng nhập bằng Google
                </button>
              </div>
            ) : (
              <div className="mt-6 space-y-3">
                <p className="text-sm text-gray-600 dark:text-gray-300">
                  Đang đăng nhập: <b>{user?.email ?? user?.fullName ?? 'tài khoản hiện tại'}</b>. Nếu đây không phải
                  email được mời, hãy đăng xuất và đăng nhập đúng tài khoản trước khi xác nhận.
                </p>
                {declineMode ? (
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
                    <div className="flex gap-2">
                      <button
                        type="button"
                        disabled={submitting}
                        className="flex-1 rounded-lg bg-red-600 px-4 py-2 font-medium text-white hover:bg-red-700 disabled:opacity-50"
                        onClick={() => void act('decline')}
                      >
                        Xác nhận từ chối
                      </button>
                      <button
                        type="button"
                        className="rounded-lg border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm"
                        onClick={() => setDeclineMode(false)}
                      >
                        Quay lại
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="flex gap-2">
                    <button
                      type="button"
                      disabled={submitting}
                      className="flex-1 rounded-lg bg-orange-600 px-4 py-2 font-medium text-white hover:bg-orange-700 disabled:opacity-50"
                      onClick={() => void act('accept')}
                    >
                      {submitting ? 'Đang xử lý…' : labels.acceptCta}
                    </button>
                    <button
                      type="button"
                      disabled={submitting}
                      className="rounded-lg border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm text-gray-700 dark:text-gray-200"
                      onClick={() => setDeclineMode(true)}
                    >
                      Từ chối
                    </button>
                  </div>
                )}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
