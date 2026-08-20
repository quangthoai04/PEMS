import { useState } from 'react';
import { AlertCircle, AlertTriangle, X } from 'lucide-react';
import { delegationsApi } from '../../delegations/api/delegationsApi';
import {
  isInstanceVersionConflict,
  INSTANCE_VERSION_CONFLICT_MESSAGE,
} from '../utils/decisionConflict';

interface VisitCampusRejectModalProps {
  visitRequestId: number;
  visitInstanceId: number;
  campusName: string;
  /** rowVersion của campus ĐÚNG NHƯ màn hình đang hiển thị — cùng hợp đồng với AssignHostModal: từ
   *  chối cũng là quyết định trên nội dung người duyệt đã đọc. */
  expectedInstanceRowVersion: number;
  onClose: () => void;
  onRejected: () => void;
  /** Gọi khi backend trả 409 phiên bản cũ và người dùng bấm "Tải phiên bản mới". KHÔNG tự từ chối
   *  lại sau khi tải — người duyệt phải đọc lại rồi bấm quyết định lần nữa. */
  onReloadRequested?: () => void;
}

/**
 * Ordinary campus reject from the V2 Detail screen — the same command
 * (`delegationsApi.rejectCampusInstance`) and the same stable version-conflict contract
 * (`isInstanceVersionConflict` / `INSTANCE_VERSION_CONFLICT_MESSAGE`) the List/Management screen's
 * own reject dialog already uses. That dialog is inline JSX inside `VisitRequestManagement.tsx`
 * rather than a standalone component, so it cannot be imported as-is; this reproduces its exact
 * behaviour (mandatory reason, version-conflict lockout, reload-not-retry) as its own small component
 * instead of duplicating a second approval/rejection SERVICE — the write authority stays entirely in
 * `RejectCampusInstanceCommandHandler`.
 */
export default function VisitCampusRejectModal({
  visitRequestId, visitInstanceId, campusName, expectedInstanceRowVersion,
  onClose, onRejected, onReloadRequested,
}: VisitCampusRejectModalProps) {
  const [text, setText] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [versionConflict, setVersionConflict] = useState(false);

  const submit = async () => {
    const reason = text.trim();
    if (!reason) { setError('Vui lòng nhập lý do từ chối.'); return; }
    setSubmitting(true);
    setError(null);
    try {
      await delegationsApi.rejectCampusInstance(
        visitRequestId, visitInstanceId, reason, expectedInstanceRowVersion);
      onRejected();
    } catch (e: unknown) {
      if (isInstanceVersionConflict(e)) {
        setVersionConflict(true);
        setError(INSTANCE_VERSION_CONFLICT_MESSAGE);
        return;
      }
      const axiosLike = e as { response?: { data?: { message?: string; title?: string } }; message?: string };
      const msg = axiosLike?.response?.data?.message || axiosLike?.response?.data?.title
        || axiosLike?.message || 'Lỗi không xác định';
      setError(`Không thể từ chối. ${msg}`);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
      <div className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden">
        <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
          <h3 className="text-lg font-bold text-white flex items-center gap-2">
            <AlertCircle className="w-5 h-5" /> Từ chối cơ sở này
          </h3>
          <button
            type="button"
            data-testid="campus-reject-modal-close"
            disabled={submitting}
            onClick={onClose}
            className="text-white/80 hover:text-white hover:bg-white/10 rounded-full p-1.5"
          >
            <X className="w-5 h-5" />
          </button>
        </div>
        <div className="p-6">
          <p className="text-sm text-gray-700 mb-3">
            Vui lòng nhập lý do từ chối tiếp nhận đoàn tại cơ sở{' '}
            <span className="font-bold text-[#004c91]">{campusName}</span>. Các cơ sở khác của đơn
            (nếu có) không bị ảnh hưởng:
          </p>
          <textarea
            data-testid="campus-reject-reason"
            value={text}
            onChange={(e) => setText(e.target.value)}
            placeholder="Nhập lý do chi tiết..."
            disabled={submitting || versionConflict}
            maxLength={2000}
            className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[120px] resize-none bg-gray-50/50 focus:bg-white"
          />
          {versionConflict ? (
            <div role="alert" className="mt-3 rounded-xl border border-amber-300 bg-amber-50 px-3 py-3 text-sm text-amber-900">
              <p className="flex items-start gap-2 font-normal">
                <AlertTriangle className="mt-0.5 h-4 w-4 flex-shrink-0" />
                {error}
              </p>
              <button
                type="button"
                onClick={() => { onReloadRequested?.(); onClose(); }}
                className="mt-2 rounded-lg bg-amber-600 px-3 py-1.5 text-xs font-bold text-white outline-none transition-colors hover:bg-amber-700"
              >
                Tải phiên bản mới
              </button>
            </div>
          ) : (
            error && <p className="text-red-500 text-sm mt-2">{error}</p>
          )}
        </div>
        <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
          <button
            type="button"
            disabled={submitting}
            onClick={onClose}
            className="px-6 py-2.5 rounded-xl font-bold text-gray-600 bg-white border border-gray-200 hover:bg-gray-50 transition-all outline-none text-sm"
          >
            Hủy bỏ
          </button>
          <button
            type="button"
            data-testid="campus-reject-confirm"
            disabled={!text.trim() || submitting || versionConflict}
            onClick={() => void submit()}
            className="px-6 py-2.5 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm disabled:opacity-50 disabled:cursor-not-allowed"
          >
            {submitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}
          </button>
        </div>
      </div>
    </div>
  );
}
