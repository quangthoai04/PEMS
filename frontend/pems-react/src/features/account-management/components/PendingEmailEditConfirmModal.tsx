import { useEffect, useRef } from 'react';
import { X } from 'lucide-react';

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
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-[70] flex items-center justify-center p-4 sm:p-6 animate-in fade-in duration-200">
      <div className="bg-white rounded-2xl w-full max-w-md shadow-xl overflow-hidden animate-in zoom-in-95 duration-300 relative">
        <div className="px-6 py-4 border-b border-gray-100 flex items-center gap-2">
          <h2 className="text-xl font-black text-gray-800">✉️ Xác nhận thay đổi email chờ kích hoạt</h2>
          <button
            type="button"
            aria-label="Đóng"
            onClick={onCancel}
            disabled={submitting}
            className="absolute top-4 right-4 w-8 h-8 rounded-full hover:bg-gray-100 flex items-center justify-center text-gray-500 transition-colors outline-none disabled:opacity-50"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-6 text-gray-700 leading-relaxed text-[15px]">
          <div>Email tài khoản sẽ được thay đổi:</div>
          <div className="mt-3 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3 text-sm space-y-1">
            <div>
              <span className="text-gray-500">Từ: </span>
              <strong className="text-[#004c91] break-all">{oldEmail || '-'}</strong>
            </div>
            <div>
              <span className="text-gray-500">Sang: </span>
              <strong className="text-[#004c91] break-all">{newEmail}</strong>
            </div>
          </div>
          <div className="mt-4 text-sm text-gray-600 space-y-2">
            <p>Liên kết xác nhận đã gửi tới email cũ sẽ không còn hiệu lực.</p>
            <p>Hệ thống sẽ phát hành một liên kết xác nhận mới và gửi tới email mới.</p>
            <p>Tài khoản chỉ được kích hoạt sau khi người nhận hoàn tất xác nhận email.</p>
          </div>
          {error && (
            <div className="mt-4 rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-bold text-red-700">
              {error}
            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-gray-100 bg-gray-50 flex items-center justify-end gap-3 rounded-b-2xl">
          <button
            type="button"
            onClick={onCancel}
            disabled={submitting}
            className="px-5 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-100 transition-colors outline-none disabled:opacity-60"
          >
            Hủy
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={submitting}
            className="px-5 py-2.5 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#00386b] shadow-sm transition-all outline-none disabled:opacity-60 disabled:cursor-not-allowed"
          >
            {submitting ? 'Đang cập nhật...' : 'Cập nhật và gửi email xác nhận'}
          </button>
        </div>
      </div>
    </div>
  );
}
