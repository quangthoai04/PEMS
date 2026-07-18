import { useCallback, useEffect, useState } from 'react';
import {
  cancelContactTransfer,
  getActiveContactTransfer,
  initiateContactTransfer,
  replacePendingContact,
  resendContactClaim,
  resendContactTransfer,
  type ContactTransferState,
} from '../api/visitRequestV2Api';

interface Props {
  visitRequestId: number;
  /** ACTIVE | PENDING_CONFIRMATION — drives which workflow (claim vs transfer) is offered. */
  primaryContactAccessStatus: string;
  /** Masked contact email for display (never the full address of someone else). */
  contactEmailMasked?: string | null;
  /** Whether the CURRENT user may manage the invitation (registrant or ACTIVE contact). */
  canManage: boolean;
  onChanged?: () => void;
}

interface ContactFormState {
  fullName: string;
  organization: string;
  phone: string;
  email: string;
  reason: string;
}

const emptyForm: ContactFormState = { fullName: '', organization: '', phone: '', email: '', reason: '' };

/**
 * Identity management panel (plan §9.4): a DISTINCT action area — never just a disabled email input.
 * While the contact is PENDING_CONFIRMATION: resend the INITIAL_CLAIM (72h) or replace the invited
 * email (typo fix). Once ACTIVE: propose a 24h TRANSFER — the current owner keeps every right until
 * the invited person explicitly accepts; the panel shows/cancels/resends the pending transfer.
 */
export default function ContactIdentityPanel({
  visitRequestId,
  primaryContactAccessStatus,
  contactEmailMasked,
  canManage,
  onChanged,
}: Props) {
  const isPending = primaryContactAccessStatus === 'PENDING_CONFIRMATION';
  const [transfer, setTransfer] = useState<ContactTransferState | null>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [showForm, setShowForm] = useState<'replace' | 'transfer' | null>(null);
  const [form, setForm] = useState<ContactFormState>(emptyForm);

  const refreshTransfer = useCallback(async () => {
    if (isPending || !canManage) return;
    try {
      setTransfer(await getActiveContactTransfer(visitRequestId));
    } catch {
      setTransfer(null); // flag OFF / no rights → hide silently
    }
  }, [visitRequestId, isPending, canManage]);

  useEffect(() => {
    void refreshTransfer();
  }, [refreshTransfer]);

  if (!canManage) return null;

  const run = async (fn: () => Promise<{ message: string }>) => {
    setBusy(true);
    setMessage(null);
    try {
      const result = await fn();
      setMessage(result.message);
      setShowForm(null);
      setForm(emptyForm);
      await refreshTransfer();
      onChanged?.();
    } catch (err: unknown) {
      setMessage(
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
          'Không thể thực hiện thao tác. Vui lòng thử lại.',
      );
    } finally {
      setBusy(false);
    }
  };

  const contactForm = (mode: 'replace' | 'transfer') => (
    <form
      className="mt-3 space-y-2"
      onSubmit={e => {
        e.preventDefault();
        void run(() =>
          mode === 'replace'
            ? replacePendingContact(visitRequestId, {
                fullName: form.fullName,
                organization: form.organization,
                phone: form.phone,
                email: form.email,
              })
            : initiateContactTransfer(visitRequestId, {
                fullName: form.fullName,
                organization: form.organization,
                phone: form.phone,
                email: form.email,
                reason: form.reason || undefined,
              }),
        );
      }}
    >
      {(['fullName', 'organization', 'phone', 'email'] as const).map(field => (
        <div key={field}>
          <label htmlFor={`ci-${field}`} className="block text-xs text-gray-500 dark:text-gray-400">
            {field === 'fullName' ? 'Họ tên' : field === 'organization' ? 'Đơn vị' : field === 'phone' ? 'Điện thoại' : 'Email'}
            {field !== 'organization' && <span className="text-red-500"> *</span>}
          </label>
          <input
            id={`ci-${field}`}
            type={field === 'email' ? 'email' : 'text'}
            required={field !== 'organization'}
            maxLength={field === 'organization' ? 200 : 150}
            className="mt-1 w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 p-2 text-sm"
            value={form[field]}
            onChange={e => setForm(f => ({ ...f, [field]: e.target.value }))}
          />
        </div>
      ))}
      {mode === 'transfer' && (
        <div>
          <label htmlFor="ci-reason" className="block text-xs text-gray-500 dark:text-gray-400">Lý do chuyển giao</label>
          <input
            id="ci-reason"
            maxLength={500}
            className="mt-1 w-full rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 p-2 text-sm"
            value={form.reason}
            onChange={e => setForm(f => ({ ...f, reason: e.target.value }))}
          />
        </div>
      )}
      {mode === 'transfer' && (
        <p className="text-xs text-amber-700 dark:text-amber-300" role="note">
          Đầu mối hiện tại giữ nguyên quyền cho tới khi người mới đăng nhập đúng Google và bấm chấp nhận
          (hiệu lực 24 giờ). Tài khoản của đầu mối cũ không bị xóa hay khóa.
        </p>
      )}
      <div className="flex gap-2 pt-1">
        <button
          type="submit"
          disabled={busy}
          className="rounded-lg bg-orange-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-orange-700 disabled:opacity-50"
        >
          {mode === 'replace' ? 'Cập nhật & gửi lời mời mới' : 'Gửi lời mời chuyển giao'}
        </button>
        <button
          type="button"
          className="rounded-lg border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm"
          onClick={() => setShowForm(null)}
        >
          Hủy
        </button>
      </div>
    </form>
  );

  return (
    <section
      aria-label="Quản lý đầu mối liên hệ"
      className="rounded-xl border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-4"
    >
      <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">Đầu mối liên hệ</h3>
      {isPending ? (
        <>
          <p className="mt-1 text-sm text-amber-700 dark:text-amber-300">
            Đầu mối {contactEmailMasked ? <b>{contactEmailMasked}</b> : null} <b>chưa xác nhận</b> lời mời
            (hiệu lực 72 giờ). Việc duyệt của các cơ sở không chờ xác nhận này.
          </p>
          {showForm === 'replace' ? (
            contactForm('replace')
          ) : (
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                type="button"
                disabled={busy}
                className="rounded-lg bg-orange-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-orange-700 disabled:opacity-50"
                onClick={() => void run(() => resendContactClaim(visitRequestId))}
              >
                Gửi lại lời mời
              </button>
              <button
                type="button"
                className="rounded-lg border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm"
                onClick={() => setShowForm('replace')}
              >
                Nhập lại email đầu mối
              </button>
            </div>
          )}
        </>
      ) : transfer?.hasPendingTransfer ? (
        <>
          <p className="mt-1 text-sm text-amber-700 dark:text-amber-300">
            Đang chờ <b>{transfer.newEmailMasked}</b> tiếp nhận vai trò đầu mối
            {transfer.expiresAt ? ` (hiệu lực đến ${new Date(transfer.expiresAt).toLocaleString('vi-VN')})` : ''}.
            Đầu mối hiện tại vẫn giữ nguyên quyền.
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              disabled={busy}
              className="rounded-lg bg-orange-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-orange-700 disabled:opacity-50"
              onClick={() => void run(() => resendContactTransfer(visitRequestId))}
            >
              Gửi lại lời mời
            </button>
            <button
              type="button"
              disabled={busy}
              className="rounded-lg border border-red-300 px-3 py-1.5 text-sm text-red-600 hover:bg-red-50 dark:border-red-700 dark:text-red-300"
              onClick={() => void run(() => cancelContactTransfer(visitRequestId))}
            >
              Hủy lời mời chuyển giao
            </button>
          </div>
        </>
      ) : (
        <>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-300">
            Đầu mối hiện tại đã xác nhận và đang quản lý đơn. Cần đổi người phụ trách? Gửi lời mời chuyển giao.
          </p>
          {showForm === 'transfer' ? (
            contactForm('transfer')
          ) : (
            <button
              type="button"
              className="mt-3 rounded-lg border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm"
              onClick={() => setShowForm('transfer')}
            >
              Chuyển giao vai trò đầu mối
            </button>
          )}
        </>
      )}
      {message && (
        <p className="mt-3 text-sm text-gray-700 dark:text-gray-200" role="status">
          {message}
        </p>
      )}
    </section>
  );
}
