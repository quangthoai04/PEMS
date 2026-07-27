import React, { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Loader2, X } from 'lucide-react';
import {
  GENDER_OPTIONS,
  PERSONNEL_STATUS_LABELS,
  type PersonnelDetail,
  type PersonnelGender,
} from '../types/departmentLeaderPersonnel.types';
import {
  hasErrors,
  normalizeEmail,
  validatePersonnelForm,
  type PersonnelFormErrors,
  type PersonnelFormValues,
} from '../validation/personnelValidation';

interface Props {
  open: boolean;
  mode: 'create' | 'edit';
  /** Required in edit mode — supplies the current values and the account status. */
  personnel?: PersonnelDetail | null;
  submitting: boolean;
  onClose: () => void;
  onSubmit: (values: { fullName: string; email: string; phone: string; gender: PersonnelGender }) => void;
}

const EMPTY: PersonnelFormValues = { fullName: '', email: '', phone: '', gender: '' };

/**
 * Add / edit personnel modal.
 *
 * Two details this component exists to get right:
 *
 *  • **Gender is never inferred.** The select binds the canonical wire value (MALE/FEMALE/OTHER)
 *    directly and starts EMPTY rather than defaulting to a value. A default is how an existing
 *    Male/Female record silently became Other just by opening the modal and pressing save.
 *  • **An email change is confirmed explicitly.** Editing the login address signs the account out
 *    everywhere, so on submit the modal shows exactly what will happen and waits for a second
 *    confirmation — the operator never triggers that as a side effect of a name fix.
 */
export function PersonnelFormModal({ open, mode, personnel, submitting, onClose, onSubmit }: Props) {
  const [values, setValues] = useState<PersonnelFormValues>(EMPTY);
  const [errors, setErrors] = useState<PersonnelFormErrors>({});
  const [touched, setTouched] = useState(false);
  const [confirmingEmailChange, setConfirmingEmailChange] = useState(false);

  // Re-seed whenever the modal opens or targets a different record.
  useEffect(() => {
    if (!open) return;
    setErrors({});
    setTouched(false);
    setConfirmingEmailChange(false);
    setValues(
      mode === 'edit' && personnel
        ? {
            fullName: personnel.fullName,
            email: personnel.email,
            phone: personnel.phone ?? '',
            // Preserved exactly as stored; null stays unselected instead of becoming OTHER.
            gender: personnel.gender ?? '',
          }
        : EMPTY,
    );
  }, [open, mode, personnel]);

  const originalEmail = useMemo(
    () => (mode === 'edit' && personnel ? normalizeEmail(personnel.email) : ''),
    [mode, personnel],
  );

  const emailWillChange =
    mode === 'edit' && originalEmail.length > 0 && normalizeEmail(values.email) !== originalEmail;

  if (!open) return null;

  const setField = <K extends keyof PersonnelFormValues>(key: K, value: PersonnelFormValues[K]) => {
    const next = { ...values, [key]: value };
    setValues(next);
    if (touched) setErrors(validatePersonnelForm(next));
  };

  const handleSubmit = (event: React.FormEvent) => {
    event.preventDefault();
    if (submitting) return;

    const validation = validatePersonnelForm(values);
    setErrors(validation);
    setTouched(true);
    if (hasErrors(validation)) return;

    // Changing the login address is disruptive enough to deserve its own confirmation step.
    if (emailWillChange && !confirmingEmailChange) {
      setConfirmingEmailChange(true);
      return;
    }

    onSubmit({
      fullName: values.fullName.trim(),
      email: normalizeEmail(values.email),
      phone: values.phone.trim(),
      gender: values.gender as PersonnelGender,
    });
  };

  const isPending = personnel?.status === 'PENDING_EMAIL_CONFIRMATION';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b px-6 py-4">
          <h3 className="text-lg font-semibold text-gray-900">
            {mode === 'create' ? 'Thêm nhân sự mới' : 'Chỉnh sửa thông tin nhân sự'}
          </h3>
          <button
            type="button"
            onClick={onClose}
            disabled={submitting}
            className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600 disabled:opacity-50"
            aria-label="Đóng"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {confirmingEmailChange ? (
          <EmailChangeConfirmation
            currentEmail={personnel?.email ?? ''}
            newEmail={normalizeEmail(values.email)}
            status={personnel?.status ?? 'ACTIVE'}
            isPending={isPending}
            submitting={submitting}
            onBack={() => setConfirmingEmailChange(false)}
            onConfirm={handleSubmit}
          />
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4 px-6 py-5" noValidate>
            <Field label="Họ và tên" required error={errors.fullName}>
              <input
                type="text"
                value={values.fullName}
                onChange={(e) => setField('fullName', e.target.value)}
                disabled={submitting}
                maxLength={150}
                className={inputClass(errors.fullName)}
                placeholder="Nguyễn Văn A"
              />
            </Field>

            <Field
              label="Email đăng nhập"
              required
              error={errors.email}
              hint={
                mode === 'edit'
                  ? 'Có thể sửa ở mọi trạng thái tài khoản. Đổi email sẽ thu hồi mọi phiên đăng nhập.'
                  : 'Dùng @gmail.com, @fpt.edu.vn hoặc @fe.edu.vn.'
              }
            >
              <input
                type="email"
                value={values.email}
                onChange={(e) => setField('email', e.target.value)}
                disabled={submitting}
                maxLength={150}
                className={inputClass(errors.email)}
                placeholder="nhansu@fpt.edu.vn"
              />
            </Field>

            <Field label="Số điện thoại" required error={errors.phone}>
              <input
                type="tel"
                value={values.phone}
                onChange={(e) => setField('phone', e.target.value)}
                disabled={submitting}
                maxLength={30}
                className={inputClass(errors.phone)}
                placeholder="0912345678"
              />
            </Field>

            <Field label="Giới tính" required error={errors.gender}>
              <select
                value={values.gender}
                onChange={(e) => setField('gender', e.target.value as PersonnelGender | '')}
                disabled={submitting}
                className={inputClass(errors.gender)}
              >
                {/* No preselected value: an implicit default is how a stored gender gets overwritten. */}
                <option value="">-- Chọn giới tính --</option>
                {GENDER_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </Field>

            {mode === 'create' && (
              <p className="rounded-md bg-blue-50 px-3 py-2 text-sm text-blue-800">
                Nhân sự mới được tạo với vai trò <strong>Nhân viên</strong> trong phòng ban của bạn và ở
                trạng thái <strong>Chờ xác nhận email</strong> cho tới khi họ xác nhận email.
              </p>
            )}

            {mode === 'edit' && personnel && (
              <p className="rounded-md bg-gray-50 px-3 py-2 text-sm text-gray-700">
                Trạng thái tài khoản: <strong>{PERSONNEL_STATUS_LABELS[personnel.status]}</strong>. Việc
                chỉnh sửa thông tin không làm thay đổi trạng thái này.
              </p>
            )}

            <div className="flex justify-end gap-3 border-t pt-4">
              <button
                type="button"
                onClick={onClose}
                disabled={submitting}
                className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
              >
                Hủy
              </button>
              <button
                type="submit"
                disabled={submitting}
                className="inline-flex items-center gap-2 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-60"
              >
                {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
                {mode === 'create' ? 'Thêm nhân sự' : 'Lưu thay đổi'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

/** The explicit "you are about to change a login identity" step. */
function EmailChangeConfirmation({
  currentEmail,
  newEmail,
  status,
  isPending,
  submitting,
  onBack,
  onConfirm,
}: {
  currentEmail: string;
  newEmail: string;
  status: keyof typeof PERSONNEL_STATUS_LABELS;
  isPending: boolean;
  submitting: boolean;
  onBack: () => void;
  onConfirm: (event: React.FormEvent) => void;
}) {
  return (
    <form onSubmit={onConfirm} className="space-y-4 px-6 py-5">
      <div className="flex items-start gap-3 rounded-md bg-amber-50 px-4 py-3">
        <AlertTriangle className="mt-0.5 h-5 w-5 flex-shrink-0 text-amber-600" />
        <p className="text-sm text-amber-900">
          Bạn đang thay đổi <strong>email đăng nhập</strong> của nhân sự này.
        </p>
      </div>

      <dl className="space-y-2 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-gray-500">Email hiện tại</dt>
          <dd className="break-all text-right font-medium text-gray-900">{currentEmail}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-gray-500">Email mới</dt>
          <dd className="break-all text-right font-medium text-blue-700">{newEmail}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-gray-500">Trạng thái tài khoản</dt>
          <dd className="text-right font-medium text-gray-900">{PERSONNEL_STATUS_LABELS[status]}</dd>
        </div>
      </dl>

      <div className="rounded-md border border-gray-200 px-4 py-3">
        <p className="mb-2 text-sm font-medium text-gray-900">Hậu quả:</p>
        <ul className="list-inside list-disc space-y-1 text-sm text-gray-700">
          <li>Nhân sự sẽ bị đăng xuất khỏi tất cả thiết bị.</li>
          <li>Nhân sự phải dùng email mới để đăng nhập.</li>
          <li>
            Trạng thái tài khoản giữ nguyên (<strong>{PERSONNEL_STATUS_LABELS[status]}</strong>).
          </li>
          {isPending && (
            <li>Link xác nhận cũ sẽ hết hiệu lực và link mới sẽ được gửi tới email mới.</li>
          )}
        </ul>
      </div>

      <div className="flex justify-end gap-3 border-t pt-4">
        <button
          type="button"
          onClick={onBack}
          disabled={submitting}
          className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
        >
          Quay lại
        </button>
        <button
          type="submit"
          disabled={submitting}
          className="inline-flex items-center gap-2 rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-60"
        >
          {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
          Xác nhận đổi email
        </button>
      </div>
    </form>
  );
}

function Field({
  label,
  required,
  error,
  hint,
  children,
}: {
  label: string;
  required?: boolean;
  error?: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <div>
      <label className="mb-1 block text-sm font-medium text-gray-700">
        {label} {required && <span className="text-red-500">*</span>}
      </label>
      {children}
      {error ? (
        <p className="mt-1 text-sm text-red-600">{error}</p>
      ) : (
        hint && <p className="mt-1 text-xs text-gray-500">{hint}</p>
      )}
    </div>
  );
}

function inputClass(error?: string): string {
  return [
    'w-full rounded-md border px-3 py-2 text-sm outline-none transition',
    'focus:ring-2 focus:ring-blue-500/30 disabled:bg-gray-100',
    error ? 'border-red-400 focus:border-red-500' : 'border-gray-300 focus:border-blue-500',
  ].join(' ');
}
