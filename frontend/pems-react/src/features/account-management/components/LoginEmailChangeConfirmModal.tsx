import { useEffect, useRef } from 'react';
import { Mail, RefreshCw, ShieldAlert, X } from 'lucide-react';
import { EmailChangePreview } from './EmailChangePreview';

/**
 * Confirmation for changing the login email of a PROVISIONED account (HO_BASIC_INFO §10).
 *
 * Sibling of the pending-account dialog and deliberately built from the same parts, so the two read
 * as one family — what differs is the single consequence that matters here: the account is signed
 * out everywhere and has to re-link Google SSO. That is disruptive rather than merely informative, so
 * it is called out in amber instead of being buried in the body text.
 *
 * Not dismissible by backdrop click or Escape: a stray click must not silently drop a decision that
 * is about to revoke someone's sessions.
 */
export function LoginEmailChangeConfirmModal({
  oldEmail,
  newEmail,
  submitting,
  error,
  onCancel,
  onConfirm,
}: {
  oldEmail: string;
  newEmail: string;
  submitting: boolean;
  error?: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  // See the pending dialog: parent state has not been applied yet when a second click lands in the
  // same tick, so `disabled` alone would not stop a duplicate request.
  const inFlight = useRef(false);
  useEffect(() => {
    if (!submitting) inFlight.current = false;
  }, [submitting]);

  const handleConfirm = () => {
    if (inFlight.current || submitting) return;
    inFlight.current = true;
    onConfirm();
  };

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm duration-200 animate-in fade-in sm:p-6">
      <div className="w-full max-w-md overflow-hidden rounded-2xl bg-white shadow-xl duration-300 animate-in zoom-in-95">
        <div className="flex items-start gap-3 border-b border-gray-100 px-6 py-4">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-[#004c91]/10 text-[#004c91]">
            <Mail className="h-5 w-5" />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="text-lg font-black leading-snug text-gray-800">
              Xác nhận thay đổi email đăng nhập
            </h2>
            <p className="mt-0.5 text-xs font-medium text-gray-500">
              Địa chỉ dùng để đăng nhập vào hệ thống
            </p>
          </div>
          <button
            type="button"
            aria-label="Đóng"
            onClick={onCancel}
            disabled={submitting}
            className="-mr-1.5 -mt-1 flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-gray-400 outline-none transition-colors hover:bg-gray-100 hover:text-gray-600 disabled:opacity-50"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="space-y-4 p-6">
          <EmailChangePreview oldEmail={oldEmail} newEmail={newEmail} />

          <div className="flex gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
            <ShieldAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden="true" />
            <p className="text-sm leading-relaxed text-amber-900">
              Tài khoản sẽ bị đăng xuất khỏi các phiên hiện tại và phải liên kết lại Google SSO khi
              đăng nhập lần tiếp theo.
            </p>
          </div>

          {error && (
            <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
              {error}
            </div>
          )}
        </div>

        <div className="flex items-center justify-end gap-3 rounded-b-2xl border-t border-gray-100 bg-gray-50 px-6 py-4">
          <button
            type="button"
            onClick={onCancel}
            disabled={submitting}
            className="rounded-xl border border-gray-200 bg-white px-5 py-2.5 font-bold text-gray-600 outline-none transition-colors hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-60"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={submitting}
            className="inline-flex items-center gap-2 rounded-xl bg-[#004c91] px-5 py-2.5 font-bold text-white shadow-[0_4px_12px_rgba(0,76,145,0.25)] outline-none transition-all hover:bg-[#00386b] hover:shadow-[0_6px_16px_rgba(0,76,145,0.3)] disabled:cursor-not-allowed disabled:opacity-60"
          >
            {submitting && <RefreshCw className="h-4 w-4 animate-spin" />}
            {submitting ? 'Đang lưu...' : 'Xác nhận thay đổi'}
          </button>
        </div>
      </div>
    </div>
  );
}
