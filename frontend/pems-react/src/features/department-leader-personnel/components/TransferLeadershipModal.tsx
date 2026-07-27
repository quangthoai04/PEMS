import React, { useEffect, useState } from 'react';
import { AlertTriangle, Crown, Loader2, X } from 'lucide-react';
import type { LeaderCandidates } from '../types/departmentLeaderPersonnel.types';

interface Props {
  open: boolean;
  loading: boolean;
  error: string | null;
  candidates: LeaderCandidates | null;
  /** Pre-selected successor when the flow was started from a specific person's detail modal. */
  preselectedUserId?: number | null;
  submitting: boolean;
  departmentName: string;
  onClose: () => void;
  onConfirm: (newLeaderUserId: number) => void;
}

/**
 * Leadership handover.
 *
 * The candidate list comes from its own endpoint, never from the current page of the personnel
 * table — a paginated, filtered view would silently hide eligible staff sitting on page 2.
 *
 * The consequence panel is not decoration: on success the operator stops being a Department Leader
 * and their current token is void, so the page signs them out immediately afterwards.
 */
export function TransferLeadershipModal({
  open,
  loading,
  error,
  candidates,
  preselectedUserId,
  submitting,
  departmentName,
  onClose,
  onConfirm,
}: Props) {
  const [selectedId, setSelectedId] = useState<number | null>(null);

  useEffect(() => {
    if (open) setSelectedId(preselectedUserId ?? null);
  }, [open, preselectedUserId]);

  if (!open) return null;

  const items = candidates?.items ?? [];
  const hasCandidates = items.length > 0;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-lg bg-white shadow-xl">
        <div className="flex items-center justify-between border-b px-6 py-4">
          <h3 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
            <Crown className="h-5 w-5 text-amber-500" />
            Đổi trưởng phòng
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

        {loading && (
          <div className="flex items-center justify-center gap-2 px-6 py-12 text-gray-500">
            <Loader2 className="h-5 w-5 animate-spin" />
            <span className="text-sm">Đang tải danh sách ứng viên...</span>
          </div>
        )}

        {!loading && error && (
          <div className="px-6 py-8">
            <p className="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{error}</p>
          </div>
        )}

        {!loading && !error && (
          <div className="space-y-4 px-6 py-5">
            <p className="text-sm text-gray-700">
              Phòng ban: <strong>{departmentName}</strong>
              {candidates?.currentLeaderName && (
                <>
                  {' '}
                  · Trưởng phòng hiện tại: <strong>{candidates.currentLeaderName}</strong>
                </>
              )}
            </p>

            {!hasCandidates ? (
              <p className="rounded-md bg-amber-50 px-4 py-3 text-sm text-amber-900">
                Chưa có nhân viên nào đủ điều kiện làm trưởng phòng. Ứng viên phải là nhân viên phòng ban
                đang <strong>hoạt động</strong> và thuộc đúng cơ sở của phòng ban.
              </p>
            ) : (
              <>
                <div className="max-h-64 space-y-2 overflow-y-auto">
                  {items.map((candidate) => (
                    <label
                      key={candidate.userId}
                      className={`flex cursor-pointer items-center gap-3 rounded-md border px-4 py-3 transition ${
                        selectedId === candidate.userId
                          ? 'border-blue-500 bg-blue-50'
                          : 'border-gray-200 hover:bg-gray-50'
                      }`}
                    >
                      <input
                        type="radio"
                        name="new-leader"
                        value={candidate.userId}
                        checked={selectedId === candidate.userId}
                        onChange={() => setSelectedId(candidate.userId)}
                        disabled={submitting}
                        className="h-4 w-4 text-blue-600"
                      />
                      <span className="min-w-0">
                        <span className="block truncate text-sm font-medium text-gray-900">
                          {candidate.fullName}
                        </span>
                        <span className="block truncate text-xs text-gray-500">{candidate.email}</span>
                      </span>
                    </label>
                  ))}
                </div>

                <div className="flex items-start gap-3 rounded-md bg-amber-50 px-4 py-3">
                  <AlertTriangle className="mt-0.5 h-5 w-5 flex-shrink-0 text-amber-600" />
                  <div className="text-sm text-amber-900">
                    <p className="mb-1 font-medium">Sau khi đổi trưởng phòng:</p>
                    <ul className="list-inside list-disc space-y-1">
                      <li>
                        Bạn sẽ trở thành <strong>Nhân viên</strong> của phòng ban và mất quyền quản lý.
                      </li>
                      <li>Nhân sự được chọn trở thành Trưởng phòng.</li>
                      <li>Cả hai tài khoản bị đăng xuất và phải đăng nhập lại.</li>
                      <li>Thao tác này không thể tự hoàn tác.</li>
                    </ul>
                  </div>
                </div>
              </>
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
              {hasCandidates && (
                <button
                  type="button"
                  onClick={() => selectedId !== null && onConfirm(selectedId)}
                  disabled={submitting || selectedId === null}
                  className="inline-flex items-center gap-2 rounded-md bg-amber-600 px-4 py-2 text-sm font-medium text-white hover:bg-amber-700 disabled:opacity-60"
                >
                  {submitting && <Loader2 className="h-4 w-4 animate-spin" />}
                  Xác nhận đổi trưởng phòng
                </button>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
