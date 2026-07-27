import React, { useState } from 'react';
import { AlertTriangle, Loader2, ShieldAlert, X } from 'lucide-react';
import {
  PERSONNEL_STATUS_LABELS,
  type PersonnelStatusImpact,
} from '../types/departmentLeaderPersonnel.types';

interface Props {
  open: boolean;
  loading: boolean;
  error: string | null;
  impact: PersonnelStatusImpact | null;
  personnelName: string;
  submitting: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
}

const MAX_REASON_LENGTH = 500;

/**
 * Confirmation step for enable/disable, driven by the read-only impact preview.
 *
 * The toggle never writes directly. This modal shows what the change would cost — blockers that
 * prevent it, sessions it would terminate — and only then offers the confirm button. When the
 * preview reports blockers the button is not rendered at all; the backend would refuse anyway, and
 * offering it would be a lie about what the operator can do.
 */
export function StatusImpactModal({
  open,
  loading,
  error,
  impact,
  personnelName,
  submitting,
  onClose,
  onConfirm,
}: Props) {
  const [reason, setReason] = useState('');

  React.useEffect(() => {
    if (open) setReason('');
  }, [open, impact?.userId, impact?.targetStatus]);

  if (!open) return null;

  const isDisabling = impact?.targetStatus === 'INACTIVE';
  const title = isDisabling ? 'Vô hiệu hóa nhân sự' : 'Kích hoạt lại nhân sự';

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b px-6 py-4">
          <h3 className="text-lg font-semibold text-gray-900">{title}</h3>
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

        {loading && (
          <div className="flex items-center justify-center gap-2 px-6 py-12 text-gray-500">
            <Loader2 className="h-5 w-5 animate-spin" />
            <span className="text-sm">Đang kiểm tra ảnh hưởng...</span>
          </div>
        )}

        {!loading && error && (
          <div className="px-6 py-8">
            <p className="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{error}</p>
          </div>
        )}

        {!loading && !error && impact && (
          <div className="space-y-4 px-6 py-5">
            <p className="text-sm text-gray-700">
              Nhân sự: <strong>{personnelName}</strong>
            </p>

            <dl className="grid grid-cols-2 gap-3 rounded-md bg-gray-50 px-4 py-3 text-sm">
              <div>
                <dt className="text-xs uppercase tracking-wide text-gray-500">Trạng thái hiện tại</dt>
                <dd className="font-medium text-gray-900">
                  {PERSONNEL_STATUS_LABELS[impact.currentStatus]}
                </dd>
              </div>
              <div>
                <dt className="text-xs uppercase tracking-wide text-gray-500">Trạng thái sau thay đổi</dt>
                <dd className="font-medium text-gray-900">
                  {PERSONNEL_STATUS_LABELS[impact.targetStatus]}
                </dd>
              </div>
            </dl>

            {impact.blockers.length > 0 && (
              <div className="rounded-md border border-red-200 bg-red-50 px-4 py-3">
                <p className="mb-2 flex items-center gap-2 text-sm font-medium text-red-800">
                  <ShieldAlert className="h-4 w-4" />
                  Không thể thực hiện thay đổi này
                </p>
                <ul className="list-inside list-disc space-y-1 text-sm text-red-700">
                  {impact.blockers.map((blocker) => (
                    <li key={`${blocker.code}-${blocker.count}`}>{blocker.message}</li>
                  ))}
                </ul>
              </div>
            )}

            {impact.warnings.length > 0 && (
              <div className="rounded-md border border-amber-200 bg-amber-50 px-4 py-3">
                <p className="mb-2 flex items-center gap-2 text-sm font-medium text-amber-900">
                  <AlertTriangle className="h-4 w-4" />
                  Lưu ý
                </p>
                <ul className="list-inside list-disc space-y-1 text-sm text-amber-800">
                  {impact.warnings.map((warning) => (
                    <li key={warning.code}>{warning.message}</li>
                  ))}
                </ul>
              </div>
            )}

            {impact.canChangeStatus && isDisabling && (
              <div>
                <label className="mb-1 block text-sm font-medium text-gray-700">
                  Lý do vô hiệu hóa <span className="text-gray-400">(tùy chọn)</span>
                </label>
                <textarea
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  disabled={submitting}
                  maxLength={MAX_REASON_LENGTH}
                  rows={3}
                  className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm outline-none focus:border-blue-500 focus:ring-2 focus:ring-blue-500/30 disabled:bg-gray-100"
                  placeholder="Ví dụ: Nhân sự đã chuyển công tác."
                />
                <p className="mt-1 text-xs text-gray-500">
                  {reason.length}/{MAX_REASON_LENGTH} ký tự. Lý do được lưu vào nhật ký hệ thống.
                </p>
              </div>
            )}

            <div className="flex justify-end gap-3 border-t pt-4">
              <button
                type="button"
                onClick={onClose}
                disabled={submitting}
                className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
              >
                {impact.canChangeStatus ? 'Hủy' : 'Đóng'}
              </button>

              {/* Rendered only when the preview says the change is permitted — the backend re-checks. */}
              {impact.canChangeStatus && (
                <button
                  type="button"
                  onClick={() => onConfirm(reason.trim())}
                  disabled={submitting}
                  className={`inline-flex items-center gap-2 rounded-md px-4 py-2 text-sm font-medium text-white disabled:opacity-60 ${
                    isDisabling ? 'bg-red-600 hover:bg-red-700' : 'bg-green-600 hover:bg-green-700'
                  }`}
                >
                  {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
                  {isDisabling ? 'Xác nhận vô hiệu hóa' : 'Xác nhận kích hoạt'}
                </button>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
