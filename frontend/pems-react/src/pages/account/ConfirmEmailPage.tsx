import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import httpClient from '../../shared/api/httpClient';
import { normalizeApiError } from '../../shared/api/normalizeApiError';

/** Backend confirm outcome (POST /api/public/account-confirmations/confirm). */
interface ConfirmResponse {
  success: boolean;
  status: 'CONFIRMED' | 'ALREADY_CONFIRMED' | 'INVALID' | 'EXPIRED';
  message: string;
}

type ViewState =
  | { kind: 'loading' }
  | { kind: 'success'; alreadyConfirmed: boolean; message: string }
  | { kind: 'invalid'; message: string }
  | { kind: 'expired'; message: string }
  | { kind: 'error'; message: string; retryable: boolean };

/**
 * Public email-confirmation landing (P0 #1). Reads the one-time token from the URL and POSTs it (never a
 * GET side-effect). Renders every outcome — success / already-confirmed / invalid / expired / server /
 * network-timeout — and offers retry on transient failures and a link to the login page. It never logs the
 * user in automatically.
 */
export default function ConfirmEmailPage() {
  const [params] = useSearchParams();
  const token = params.get('token') ?? '';
  const [state, setState] = useState<ViewState>({ kind: 'loading' });
  // Guards React 18 StrictMode's double-invoke of effects so we POST the token once.
  const startedRef = useRef(false);

  const confirm = useCallback(async () => {
    if (!token) {
      setState({ kind: 'invalid', message: 'Liên kết xác nhận không hợp lệ (thiếu mã xác nhận).' });
      return;
    }
    setState({ kind: 'loading' });
    try {
      const { data } = await httpClient.post<ConfirmResponse>(
        '/public/account-confirmations/confirm',
        { token },
      );
      switch (data.status) {
        case 'CONFIRMED':
          setState({ kind: 'success', alreadyConfirmed: false, message: data.message });
          break;
        case 'ALREADY_CONFIRMED':
          setState({ kind: 'success', alreadyConfirmed: true, message: data.message });
          break;
        case 'EXPIRED':
          setState({ kind: 'expired', message: data.message });
          break;
        default:
          setState({ kind: 'invalid', message: data.message });
      }
    } catch (error) {
      const normalized = normalizeApiError(error);
      const isTransient = normalized.category === 'network' || normalized.category === 'timeout' || normalized.category === 'server';
      setState({
        kind: 'error',
        message: isTransient
          ? 'Không thể kết nối máy chủ để xác nhận. Vui lòng thử lại.'
          : (normalized.message || 'Đã xảy ra lỗi khi xác nhận email.'),
        retryable: true,
      });
    }
  }, [token]);

  useEffect(() => {
    if (startedRef.current) return;
    startedRef.current = true;
    void confirm();
  }, [confirm]);

  return (
    <div className="min-h-[70vh] flex items-center justify-center px-4 py-12">
      <div className="w-full max-w-md bg-white border border-gray-200 rounded-xl shadow-sm p-8 text-center">
        <h1 className="text-xl font-bold text-[#004c91] mb-6">Xác nhận email tài khoản PEMS</h1>

        {state.kind === 'loading' && (
          <div data-testid="confirm-loading" className="text-gray-600">
            <div className="mx-auto mb-4 h-10 w-10 animate-spin rounded-full border-4 border-gray-200 border-t-[#004c91]" />
            Đang xác nhận email của bạn…
          </div>
        )}

        {state.kind === 'success' && (
          <div data-testid="confirm-success">
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-emerald-50 text-3xl text-emerald-600">✓</div>
            <p className="mb-2 font-semibold text-emerald-700">
              {state.alreadyConfirmed ? 'Email đã được xác nhận' : 'Xác nhận thành công'}
            </p>
            <p className="mb-6 text-sm text-gray-600">{state.message}</p>
            <Link to="/login" className="inline-block rounded-md bg-[#004c91] px-6 py-2.5 font-semibold text-white hover:bg-[#013565]">
              Đến trang đăng nhập
            </Link>
          </div>
        )}

        {state.kind === 'expired' && (
          <div data-testid="confirm-expired">
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-amber-50 text-3xl text-amber-600">⏱</div>
            <p className="mb-2 font-semibold text-amber-700">Liên kết đã hết hạn</p>
            <p className="mb-6 text-sm text-gray-600">{state.message} Vui lòng liên hệ quản trị để được gửi lại email xác nhận.</p>
            <Link to="/login" className="inline-block rounded-md border border-gray-300 px-6 py-2.5 font-semibold text-gray-700 hover:bg-gray-50">
              Đến trang đăng nhập
            </Link>
          </div>
        )}

        {state.kind === 'invalid' && (
          <div data-testid="confirm-invalid">
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-red-50 text-3xl text-red-600">✕</div>
            <p className="mb-2 font-semibold text-red-700">Liên kết không hợp lệ</p>
            <p className="mb-6 text-sm text-gray-600">{state.message}</p>
            <Link to="/login" className="inline-block rounded-md border border-gray-300 px-6 py-2.5 font-semibold text-gray-700 hover:bg-gray-50">
              Đến trang đăng nhập
            </Link>
          </div>
        )}

        {state.kind === 'error' && (
          <div data-testid="confirm-error">
            <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-red-50 text-3xl text-red-600">!</div>
            <p className="mb-2 font-semibold text-red-700">Không thể xác nhận</p>
            <p className="mb-6 text-sm text-gray-600">{state.message}</p>
            {state.retryable && (
              <button
                type="button"
                onClick={() => void confirm()}
                className="mb-3 inline-block w-full rounded-md bg-[#004c91] px-6 py-2.5 font-semibold text-white hover:bg-[#013565]"
              >
                Thử lại
              </button>
            )}
            <Link to="/login" className="inline-block text-sm text-[#004c91] hover:underline">
              Đến trang đăng nhập
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}
