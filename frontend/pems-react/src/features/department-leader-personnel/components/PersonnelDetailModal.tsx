import React from 'react';
import { Crown, Loader2, Mail, X } from 'lucide-react';
import {
  GENDER_LABELS,
  PERSONNEL_STATUS_LABELS,
  type PersonnelDetail,
} from '../types/departmentLeaderPersonnel.types';
import { PersonnelStatusBadge } from './PersonnelStatusBadge';

interface Props {
  open: boolean;
  loading: boolean;
  error: string | null;
  personnel: PersonnelDetail | null;
  resending: boolean;
  onClose: () => void;
  onEdit: () => void;
  onChangeStatus: (target: 'ACTIVE' | 'INACTIVE') => void;
  onResendConfirmation: () => void;
  onTransferLeadership: () => void;
}

/**
 * Read-only detail view, loaded from the detail API rather than the list row — a row is a summary
 * and lacks fields (last login, updated-at, campus id) the modal shows and the edit form needs.
 *
 * Every action button is gated on the backend's `can*` flag. Those flags are a rendering hint only;
 * the corresponding command re-derives the same rule, so a hidden button is convenience, not the
 * security boundary.
 */
export function PersonnelDetailModal({
  open,
  loading,
  error,
  personnel,
  resending,
  onClose,
  onEdit,
  onChangeStatus,
  onResendConfirmation,
  onTransferLeadership,
}: Props) {
  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="max-h-[90vh] w-full max-w-2xl overflow-y-auto rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b px-6 py-4">
          <h3 className="text-lg font-semibold text-gray-900">Thông tin nhân sự</h3>
          <button
            type="button"
            onClick={onClose}
            className="rounded p-1 text-gray-400 hover:bg-gray-100 hover:text-gray-600"
            aria-label="Đóng"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        {loading && (
          <div className="flex items-center justify-center gap-2 px-6 py-12 text-gray-500">
            <Loader2 className="h-5 w-5 animate-spin" />
            <span className="text-sm">Đang tải thông tin nhân sự...</span>
          </div>
        )}

        {!loading && error && (
          <div className="px-6 py-8">
            <p className="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{error}</p>
          </div>
        )}

        {!loading && !error && personnel && (
          <>
            <div className="space-y-5 px-6 py-5">
              <div className="flex items-start justify-between gap-4">
                <div>
                  <p className="flex items-center gap-2 text-xl font-semibold text-gray-900">
                    {personnel.fullName}
                    {personnel.isCurrentDepartmentLeader && (
                      <Crown className="h-5 w-5 text-amber-500" aria-label="Trưởng phòng đương nhiệm" />
                    )}
                  </p>
                  <p className="text-sm text-gray-500">{personnel.position}</p>
                </div>
                <PersonnelStatusBadge status={personnel.status} />
              </div>

              <dl className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
                <Row label="Email đăng nhập" value={personnel.email} breakAll />
                <Row label="Số điện thoại" value={personnel.phone ?? '—'} />
                <Row
                  label="Giới tính"
                  value={personnel.gender ? GENDER_LABELS[personnel.gender] : '—'}
                />
                <Row label="Chức vụ" value={personnel.position} />
                <Row label="Phòng ban" value={personnel.departmentName} />
                <Row label="Cơ sở" value={personnel.campusName} />
                <Row label="Ngày tạo" value={formatDateTime(personnel.createdAt)} />
                <Row label="Cập nhật gần nhất" value={formatDateTime(personnel.updatedAt)} />
                <Row label="Đăng nhập gần nhất" value={formatDateTime(personnel.lastLoginAt)} />
              </dl>

              {personnel.status === 'PENDING_EMAIL_CONFIRMATION' && (
                <p className="rounded-md bg-amber-50 px-4 py-3 text-sm text-amber-900">
                  Tài khoản chưa xác nhận email nên chưa thể đăng nhập. Tài khoản sẽ tự hoạt động sau khi
                  nhân sự xác nhận email — không thể kích hoạt thủ công.
                </p>
              )}

              {personnel.status === 'LOCKED' && (
                <p className="rounded-md bg-red-50 px-4 py-3 text-sm text-red-800">
                  Tài khoản đang bị khóa vì lý do bảo mật. Bạn vẫn có thể sửa thông tin và email, nhưng
                  việc mở khóa phải thực hiện qua quy trình bảo mật riêng.
                </p>
              )}
            </div>

            <div className="flex flex-wrap justify-end gap-3 border-t px-6 py-4">
              {personnel.canResendEmailConfirmation && (
                <button
                  type="button"
                  onClick={onResendConfirmation}
                  disabled={resending}
                  className="inline-flex items-center gap-2 rounded-md border border-amber-300 px-4 py-2 text-sm font-medium text-amber-700 hover:bg-amber-50 disabled:opacity-60"
                >
                  {resending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Mail className="h-4 w-4" />}
                  Gửi lại email xác nhận
                </button>
              )}

              {personnel.canTransferLeadershipTo && (
                <button
                  type="button"
                  onClick={onTransferLeadership}
                  className="inline-flex items-center gap-2 rounded-md border border-amber-300 px-4 py-2 text-sm font-medium text-amber-700 hover:bg-amber-50"
                >
                  <Crown className="h-4 w-4" />
                  Chọn làm trưởng phòng
                </button>
              )}

              {personnel.canDisable && (
                <button
                  type="button"
                  onClick={() => onChangeStatus('INACTIVE')}
                  className="rounded-md border border-red-300 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50"
                >
                  Vô hiệu hóa
                </button>
              )}

              {personnel.canEnable && (
                <button
                  type="button"
                  onClick={() => onChangeStatus('ACTIVE')}
                  className="rounded-md border border-green-300 px-4 py-2 text-sm font-medium text-green-700 hover:bg-green-50"
                >
                  Kích hoạt lại
                </button>
              )}

              {personnel.canEdit && (
                <button
                  type="button"
                  onClick={onEdit}
                  className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
                >
                  Chỉnh sửa
                </button>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function Row({ label, value, breakAll }: { label: string; value: string; breakAll?: boolean }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-gray-500">{label}</dt>
      <dd className={`text-sm text-gray-900 ${breakAll ? 'break-all' : ''}`}>{value}</dd>
    </div>
  );
}

function formatDateTime(value: string | null): string {
  if (!value) return '—';
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? '—' : date.toLocaleString('vi-VN');
}
