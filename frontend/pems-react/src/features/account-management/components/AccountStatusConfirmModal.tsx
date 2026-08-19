import { useEffect, useRef } from 'react';
import { RefreshCw, ShieldCheck, ShieldOff, UserRound, X } from 'lucide-react';

/**
 * UC-97 — confirmation for switching an account between ACTIVE and INACTIVE.
 *
 * The old inline version stated the decision as one sentence with the email bolded inside it, which
 * is the shape an operator skims past: on a list of near-identical `*@fpt.edu.vn` addresses, the one
 * detail that has to register — WHICH account — was the least prominent thing on screen. So the
 * account is lifted out of the prose into a card of its own (avatar, name, address, role, campus),
 * and the sentence is replaced by the consequences the operator is actually agreeing to.
 *
 * Disabling and re-enabling are deliberately NOT symmetric in weight. Disabling ends live sessions
 * and locks somebody out immediately, so it is framed in red and says so; re-enabling only restores
 * what was already there, so it is calm green and says nothing alarming. Colour alone never carries
 * that difference — the icon and the wording change with it, for anyone who cannot separate the two
 * hues.
 *
 * Not dismissible by backdrop click or Escape: a stray click must not silently drop a decision that
 * revokes somebody's access.
 */
export function AccountStatusConfirmModal({
  account,
  action,
  submitting,
  error,
  onCancel,
  onConfirm,
}: {
  account: {
    name?: string | null;
    email?: string | null;
    avatar?: string | null;
    roleName?: string | null;
    campus?: string | null;
  };
  /** `disable`: ACTIVE → INACTIVE · `enable`: INACTIVE → ACTIVE. */
  action: 'disable' | 'enable';
  submitting: boolean;
  error?: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  // `submitting` comes from parent state, which React has not applied yet when a second click lands
  // in the same tick — so the disabled attribute alone does not prevent a double submit. The latch
  // closes synchronously and reopens once the request settles, keeping a retry possible after a
  // failure that leaves this dialog on screen.
  const inFlight = useRef(false);
  useEffect(() => {
    if (!submitting) inFlight.current = false;
  }, [submitting]);

  const handleConfirm = () => {
    if (inFlight.current || submitting) return;
    inFlight.current = true;
    onConfirm();
  };

  const disabling = action === 'disable';
  const theme = disabling
    ? {
        Icon: ShieldOff,
        badge: 'bg-red-50 text-red-600 ring-1 ring-red-100',
        title: 'Vô hiệu hóa tài khoản',
        subtitle: 'Người dùng sẽ bị đăng xuất và không thể đăng nhập lại',
        listBox: 'border-red-100 bg-red-50/60 text-red-900',
        bullet: 'bg-red-400',
        confirmLabel: 'Vô hiệu hóa',
        busyLabel: 'Đang vô hiệu hóa...',
        confirmClass:
          'bg-red-500 hover:bg-red-600 shadow-[0_4px_12px_rgba(239,68,68,0.25)] hover:shadow-[0_6px_16px_rgba(239,68,68,0.3)]',
        consequences: [
          'Tất cả phiên đăng nhập hiện tại bị thu hồi ngay lập tức.',
          'Tài khoản không thể đăng nhập cho đến khi được kích hoạt lại.',
          'Dữ liệu, vai trò và lịch sử hoạt động của tài khoản được giữ nguyên.',
        ],
      }
    : {
        Icon: ShieldCheck,
        badge: 'bg-green-50 text-[#0aa14f] ring-1 ring-green-100',
        title: 'Kích hoạt lại tài khoản',
        subtitle: 'Người dùng sẽ đăng nhập lại được ngay sau khi xác nhận',
        listBox: 'border-green-100 bg-green-50/60 text-green-900',
        bullet: 'bg-[#0aa14f]',
        confirmLabel: 'Kích hoạt',
        busyLabel: 'Đang kích hoạt...',
        confirmClass:
          'bg-[#0aa14f] hover:bg-[#088c44] shadow-[0_4px_12px_rgba(10,161,79,0.25)] hover:shadow-[0_6px_16px_rgba(10,161,79,0.3)]',
        consequences: [
          'Tài khoản đăng nhập lại được bằng thông tin hiện có.',
          'Vai trò và phạm vi quyền trước đây được giữ nguyên.',
          'Người dùng cần đăng nhập lại để bắt đầu phiên mới.',
        ],
      };

  const { Icon } = theme;
  const name = account.name?.trim();
  const email = account.email?.trim();
  // Only the parts that exist, joined once — an empty campus must not leave a dangling separator.
  const meta = [account.roleName?.trim(), account.campus?.trim()].filter(Boolean).join(' · ');
  // Derived from real text only: an initial taken from a placeholder would render the placeholder
  // character itself in the badge, which reads as corrupted data rather than missing data.
  const initial = (name || email || '').charAt(0).toUpperCase();

  return (
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm duration-200 animate-in fade-in sm:p-6">
      <div
        role="dialog"
        aria-modal="true"
        aria-label={theme.title}
        className="w-full max-w-md overflow-hidden rounded-2xl bg-white shadow-xl duration-300 animate-in zoom-in-95"
      >
        {/* The close button sits in the flow rather than absolutely, so a title that wraps on a narrow
            viewport does not run underneath it. */}
        <div className="flex items-start gap-3 border-b border-gray-100 px-6 py-4">
          <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${theme.badge}`}>
            <Icon className="h-5 w-5" />
          </span>
          <div className="min-w-0 flex-1">
            <h2 className="text-lg font-black leading-snug text-gray-800">{theme.title}</h2>
            <p className="mt-0.5 text-xs font-normal text-gray-500">{theme.subtitle}</p>
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
          {/* Who this is about, as its own object rather than a phrase inside a sentence. */}
          <div className="flex items-center gap-3 rounded-xl border border-gray-200 bg-gray-50 px-4 py-3">
            {account.avatar ? (
              <img
                src={account.avatar}
                alt=""
                className="h-11 w-11 shrink-0 rounded-xl object-cover ring-1 ring-black/5"
              />
            ) : (
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-[#004c91]/10 text-base font-black text-[#004c91]">
                {initial || <UserRound className="h-5 w-5" aria-hidden="true" />}
              </span>
            )}
            <div className="min-w-0 flex-1">
              {name && (
                <p className="truncate text-[15px] font-bold text-gray-800" title={name}>{name}</p>
              )}
              <p className="break-all text-sm font-normal text-[#004c91]">{email || '—'}</p>
              {meta && (
                <p className="mt-0.5 truncate text-xs font-normal text-gray-500" title={meta}>{meta}</p>
              )}
            </div>
          </div>

          <ul className={`space-y-2 rounded-xl border px-4 py-3 text-sm leading-relaxed ${theme.listBox}`}>
            {theme.consequences.map((line) => (
              <li key={line} className="flex gap-2.5">
                <span
                  className={`mt-[7px] h-1.5 w-1.5 shrink-0 rounded-full ${theme.bullet}`}
                  aria-hidden="true"
                />
                {line}
              </li>
            ))}
          </ul>

          {error && (
            <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-2.5 text-sm font-normal text-red-700">
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
            className={`inline-flex items-center gap-2 rounded-xl px-5 py-2.5 font-bold text-white outline-none transition-all disabled:cursor-not-allowed disabled:opacity-60 ${theme.confirmClass}`}
          >
            {submitting ? (
              <RefreshCw className="h-4 w-4 animate-spin" />
            ) : (
              <Icon className="h-4 w-4" />
            )}
            {submitting ? theme.busyLabel : theme.confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}
