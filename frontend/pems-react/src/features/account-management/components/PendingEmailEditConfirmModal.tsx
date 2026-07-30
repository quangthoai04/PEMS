import { useEffect, useRef } from 'react';
import { MailCheck, RefreshCw, X } from 'lucide-react';
import { EmailChangePreview } from './EmailChangePreview';

/**
 * Confirmation for correcting the email of an account still awaiting activation.
 *
 * Kept separate from the ordinary "đổi email đăng nhập" prompt because the consequences it must
 * state are different ones: the link already mailed to the old address dies, a new link is issued to
 * the new address, and the account stays pending until somebody clicks it. An operator who read the
 * generic wording here would think the change was complete.
 *
 * Deliberately not dismissible by backdrop click or Escape — a stray click must not silently drop a
 * decision that is about to invalidate a live activation link.
 */
export function PendingEmailEditConfirmModal({
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
  // `submitting` arrives from parent state, which React has not applied yet when a second click
  // lands in the same tick — so a disabled button alone does not prevent a double submit. The latch
  // closes synchronously on the first click and reopens only once the request has settled, which
  // keeps a retry possible after a failure that leaves this modal on screen.
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
        {/* The close button sits in the flow, not absolutely: this title wraps to two lines on a
            narrow viewport and would otherwise run underneath it. */}
        <div className="flex items-start gap-3 border-b border-gray-100 px-6 py-4">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-sky-50 text-sky-600">
            <MailCheck className="h-5 w-5" />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="text-lg font-black leading-snug text-gray-800">
              Xác nhận thay đổi email chờ kích hoạt
            </h2>
            <p className="mt-0.5 text-xs font-medium text-gray-500">
              Tài khoản chưa xác nhận email
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

          <ul className="space-y-2 rounded-xl border border-sky-100 bg-sky-50/70 px-4 py-3 text-sm leading-relaxed text-sky-900">
            <li className="flex gap-2.5">
              <span className="mt-[7px] h-1.5 w-1.5 shrink-0 rounded-full bg-sky-400" aria-hidden="true" />
              Liên kết xác nhận đã gửi tới email cũ sẽ không còn hiệu lực.
            </li>
            <li className="flex gap-2.5">
              <span className="mt-[7px] h-1.5 w-1.5 shrink-0 rounded-full bg-sky-400" aria-hidden="true" />
              Hệ thống sẽ phát hành một liên kết xác nhận mới và gửi tới email mới.
            </li>
            <li className="flex gap-2.5">
              <span className="mt-[7px] h-1.5 w-1.5 shrink-0 rounded-full bg-sky-400" aria-hidden="true" />
              Tài khoản chỉ được kích hoạt sau khi người nhận hoàn tất xác nhận email.
            </li>
          </ul>

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
            {submitting ? 'Đang cập nhật...' : 'Cập nhật và gửi email xác nhận'}
          </button>
        </div>
      </div>
    </div>
  );
}
