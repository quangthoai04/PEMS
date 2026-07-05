/**
 * AssignHostModal — Staff Leader picks the staff member who will host a campus instance.
 * Used both to APPROVE + assign a single-campus request and to TRANSFER the host of an
 * HO-approved multi-campus instance. Candidates and any schedule conflict are loaded from
 * the backend (UC-22 host-candidates); the conflict warning is advisory and does not block.
 */
import { useEffect, useState } from 'react';
import { motion } from 'motion/react';
import { X, AlertTriangle, Check, Users, Loader2, Search } from 'lucide-react';
import { delegationsApi } from '../../features/delegations/api/delegationsApi';
import type { HostCandidate } from '../../features/delegations/types/delegations.types';

type AssignHostModalProps = {
  isOpen: boolean;
  mode: 'approve' | 'transfer';
  visitRequestId: number;
  visitInstanceId: number | null;
  delegationName?: string | null;
  currentHostUserId?: number | null;
  customTitle?: string;
  /**
   * Khi được truyền, modal KHÔNG gọi API gán host — chỉ trả candidate đã chọn cho parent
   * (dashboard dùng để tiếp tục bước chọn mẫu email mời host trước khi submit).
   */
  onHostPicked?: (host: HostCandidate) => void;
  onClose: () => void;
  onConfirmed: () => void;
};

const formatDateTime = (value?: string | null) => {
  if (!value) return '-';
  return new Date(value).toLocaleString('vi-VN', {
    hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric',
  });
};

export function AssignHostModal({
  isOpen, mode, visitRequestId, visitInstanceId, delegationName, currentHostUserId, customTitle, onHostPicked, onClose, onConfirmed,
}: AssignHostModalProps) {
  const [candidates, setCandidates] = useState<HostCandidate[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [keyword, setKeyword] = useState('');
  const [debouncedKeyword, setDebouncedKeyword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  // Confirm overlay shown when assigning a host that has a schedule conflict.
  const [confirmConflict, setConfirmConflict] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedKeyword(keyword), 300);
    return () => clearTimeout(timer);
  }, [keyword]);

  useEffect(() => {
    if (!isOpen || !visitInstanceId) return;
    let cancelled = false;
    setIsLoading(true);
    setLoadError(null);
    setSelectedId(null);
    setKeyword('');
    setDebouncedKeyword('');
    setSubmitError(null);
    delegationsApi
      .getHostCandidates(visitInstanceId)
      .then((data) => { if (!cancelled) setCandidates(data ?? []); })
      .catch(() => { if (!cancelled) setLoadError('Không thể tải danh sách nhân sự. Vui lòng thử lại.'); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, [isOpen, visitInstanceId]);

  if (!isOpen) return null;

  const title = customTitle || (mode === 'approve' ? 'Duyệt đơn & chọn host' : 'Chuyển host phụ trách');
  const confirmLabel = onHostPicked
    ? 'Tiếp tục: chọn mẫu email'
    : mode === 'approve' ? 'Duyệt & gán host' : (customTitle ? 'Lưu lựa chọn' : 'Chuyển host');

  const filtered = debouncedKeyword.trim()
    ? candidates.filter((c) =>
        `${c.fullName} ${c.email} ${c.departmentName ?? ''}`.toLowerCase().includes(debouncedKeyword.trim().toLowerCase()))
    : candidates;

  const selectedCandidate = candidates.find((c) => c.userId === selectedId) ?? null;

  // Selecting confirm: if the chosen host has a schedule conflict, ask for explicit confirmation
  // first (the backend still allows it — this is an advisory guard, not a hard block).
  const attemptConfirm = () => {
    if (!selectedId || !visitInstanceId) return;
    if (selectedCandidate?.hasScheduleConflict) {
      setConfirmConflict(true);
      return;
    }
    void doAssign();
  };

  const doAssign = async () => {
    if (!selectedId || !visitInstanceId) return;
    setConfirmConflict(false);
    // Pick-only mode: parent tiếp tục bước chọn mẫu email + submit gán host.
    if (onHostPicked) {
      const picked = candidates.find((c) => c.userId === selectedId);
      if (picked) onHostPicked(picked);
      return;
    }
    setIsSubmitting(true);
    setSubmitError(null);
    try {
      await delegationsApi.assignHost(visitRequestId, visitInstanceId, selectedId);
      onConfirmed();
    } catch (e: any) {
      const msg = e?.response?.data?.message || e?.response?.data?.title || e?.message || 'Lỗi không xác định';
      setSubmitError(`Không thể gán host. ${msg}`);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
      <motion.div
        initial={{ opacity: 0, scale: 0.95, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="bg-white rounded-3xl shadow-2xl w-full max-w-lg overflow-hidden flex flex-col max-h-[88vh]"
      >
        <div className="px-6 py-4 bg-[#004c91] flex items-center justify-between flex-shrink-0">
          <div>
            <h3 className="text-lg font-bold text-white flex items-center gap-2">
              <Users className="w-5 h-5" /> {title}
            </h3>
            {delegationName && <p className="text-xs text-white/80 mt-0.5 truncate max-w-[26rem]">{delegationName}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="text-white/85 hover:text-white transition-colors hover:bg-white/10 rounded-full p-1.5 cursor-pointer"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="p-5 flex-1 overflow-y-auto">
          <div className="relative mb-3">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
            <input
              type="text"
              placeholder="Tìm nhân sự theo tên, email, đơn vị..."
              value={keyword}
              onChange={(e) => setKeyword(e.target.value)}
              className="w-full pl-9 pr-3 h-10 bg-white border border-slate-300 rounded-xl text-sm font-medium text-slate-700 outline-none focus:border-[#004c91] focus:ring-2 focus:ring-[#004c91]/10"
            />
          </div>

          {isLoading ? (
            <div className="py-10 text-center text-slate-500">
              <Loader2 className="w-6 h-6 mx-auto mb-2 animate-spin text-[#004c91]" />
              <p className="text-sm">Đang tải danh sách nhân sự...</p>
            </div>
          ) : loadError ? (
            <div className="py-10 text-center text-red-500 text-sm font-medium">{loadError}</div>
          ) : filtered.length === 0 ? (
            <div className="py-10 text-center text-slate-500 text-sm">
              <Users className="w-10 h-10 mx-auto mb-2 text-slate-300" />
              {candidates.length === 0 ? 'Cơ sở chưa có nhân sự (STAFF) phù hợp.' : 'Không tìm thấy nhân sự phù hợp.'}
            </div>
          ) : (
            <div className="space-y-2">
              {filtered.map((c) => {
                const selected = selectedId === c.userId;
                const isCurrent = currentHostUserId != null && currentHostUserId === c.userId;
                return (
                  <button
                    key={c.userId}
                    type="button"
                    onClick={() => setSelectedId(c.userId)}
                    className={`w-full text-left rounded-2xl border p-3 transition-colors outline-none cursor-pointer ${
                      selected ? 'border-[#004c91] bg-blue-50/60 ring-2 ring-[#004c91]/10' : 'border-slate-200 hover:border-[#004c91]/40 hover:bg-slate-50'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-bold text-slate-800 truncate">
                          {c.fullName}
                          {c.subRole?.toUpperCase() === 'LEADER' && <span className="ml-2 text-[10px] font-semibold text-[#004c91] bg-blue-100 rounded px-1.5 py-0.5">Leader</span>}
                          {isCurrent && <span className="ml-2 text-[10px] font-semibold text-slate-600 bg-slate-100 rounded px-1.5 py-0.5">Host hiện tại</span>}
                        </p>
                        <p className="text-xs text-slate-500 truncate">{c.email}{c.departmentName ? ` · ${c.departmentName}` : ''}</p>
                      </div>
                      {selected && <Check className="w-5 h-5 text-[#004c91] flex-shrink-0" />}
                    </div>

                    {!c.hasScheduleConflict ? (
                      <p className="mt-1.5 text-[11px] font-semibold text-emerald-600 flex items-center gap-1">
                        <Check className="w-3.5 h-3.5" /> Không trùng lịch với đơn này
                      </p>
                    ) : (
                      <div className="mt-2 rounded-xl bg-amber-50 border border-amber-200 p-2 text-[11px] text-amber-800">
                        <p className="font-bold flex items-center gap-1">
                          <AlertTriangle className="w-3.5 h-3.5" /> Trùng lịch với đơn này
                        </p>
                        {c.conflicts[0] && (
                          <p className="mt-0.5 truncate">
                            • {c.conflicts[0].title || (c.conflicts[0].source === 'CALENDAR' ? 'Lịch cá nhân' : 'Đoàn khách khác')}: {formatDateTime(c.conflicts[0].startAt)} → {formatDateTime(c.conflicts[0].endAt)}
                          </p>
                        )}
                        {c.conflictCount > 1 && (
                          <p className="mt-0.5 font-semibold">Trùng {c.conflictCount} lịch trong khung giờ này</p>
                        )}
                      </div>
                    )}
                  </button>
                );
              })}
            </div>
          )}

          {submitError && <p className="text-red-500 text-sm mt-3">{submitError}</p>}
        </div>

        <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100 flex-shrink-0">
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
          >
            Hủy bỏ
          </button>
          <button
            type="button"
            onClick={attemptConfirm}
            disabled={!selectedId || isSubmitting}
            className="px-6 py-2 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2"
          >
            {isSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
            {isSubmitting ? 'Đang xử lý...' : confirmLabel}
          </button>
        </div>

        {/* Conflict confirmation overlay */}
        {confirmConflict && (
          <div className="absolute inset-0 z-10 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
            <div className="bg-white rounded-2xl shadow-2xl w-full max-w-sm overflow-hidden border border-gray-100">
              <div className="px-5 py-4 bg-amber-500 flex items-center gap-2">
                <AlertTriangle className="w-5 h-5 text-white" />
                <h4 className="text-base font-bold text-white">Xác nhận gán host trùng lịch</h4>
              </div>
              <div className="p-5 text-sm text-slate-700">
                <p>
                  Nhân sự <span className="font-bold text-slate-900">{selectedCandidate?.fullName}</span> đang có lịch trùng với thời gian đón đoàn. Bạn vẫn muốn gán làm host?
                </p>
              </div>
              <div className="px-5 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
                <button
                  type="button"
                  onClick={() => setConfirmConflict(false)}
                  disabled={isSubmitting}
                  className="px-4 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
                >
                  Quay lại
                </button>
                <button
                  type="button"
                  onClick={doAssign}
                  disabled={isSubmitting}
                  className="px-5 py-2 rounded-xl font-bold text-white bg-amber-600 hover:bg-amber-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 flex items-center gap-2"
                >
                  {isSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}
                  Vẫn gán host
                </button>
              </div>
            </div>
          </div>
        )}
      </motion.div>
    </div>
  );
}
