/**
 * AssignHostModal — Staff Leader APPROVES a campus instance and picks the official host in the
 * SAME action (campus-independent approval, SQL v10). Candidates = IC Staff of the campus +
 * "Tôi làm host chính" (the approving Staff Leader themself); schedule conflicts are loaded from
 * the backend (UC-22 host-candidates) and are advisory (the backend hard-blocks only a real
 * double-hosting overlap). An optional decision note is stored on the instance's decision_note.
 *
 * A thin ORCHESTRATION wrapper around `HostSelectionModalView` (the shared visual source of truth
 * with `AssignHostPicker`): this component owns the write — it calls `approveCampusInstance` itself —
 * the shared view owns only the presentation. See that file's header comment for why the two must stay
 * separate.
 */
import { useEffect, useState } from 'react';
import { delegationsApi } from '../../features/delegations/api/delegationsApi';
import type { HostCandidate } from '../../features/delegations/types/delegations.types';
import {
  isInstanceVersionConflict,
  INSTANCE_VERSION_CONFLICT_MESSAGE,
} from '../../features/visit-request/utils/decisionConflict';
import { HostSelectionModalView } from './HostSelectionModalView';

type AssignHostModalProps = {
  isOpen: boolean;
  /** Kept for call-site compatibility; approve là hành động duy nhất (không còn transfer host). */
  mode?: 'approve';
  visitRequestId: number;
  visitInstanceId: number | null;
  delegationName?: string | null;
  currentHostUserId?: number | null;
  customTitle?: string;
  /**
   * rowVersion của campus ĐÚNG NHƯ màn hình duyệt đang hiển thị. BẮT BUỘC: backend từ chối
   * (409 VISIT_INSTANCE_VERSION_CONFLICT) nếu khách đã sửa đơn sau khi màn hình mở, và từ chối
   * hẳn (400 VISIT_INSTANCE_VERSION_REQUIRED) nếu không gửi — người duyệt không bao giờ được phê
   * duyệt nội dung họ chưa đọc, kể cả khi màn hình gọi quên truyền phiên bản.
   */
  expectedInstanceRowVersion: number;
  onClose: () => void;
  onConfirmed: () => void;
  /**
   * Gọi khi backend trả 409 phiên bản cũ và người dùng bấm "Tải phiên bản mới". Bên gọi có nhiệm
   * vụ tải lại đơn/campus. KHÔNG tự động duyệt lại sau khi tải — người duyệt phải đọc lại rồi
   * bấm quyết định lần nữa.
   */
  onReloadRequested?: () => void;
};

export function AssignHostModal({
  isOpen, visitRequestId, visitInstanceId, delegationName, currentHostUserId, customTitle,
  expectedInstanceRowVersion, onClose, onConfirmed, onReloadRequested,
}: AssignHostModalProps) {
  const [candidates, setCandidates] = useState<HostCandidate[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [decisionNote, setDecisionNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  // Backend đã từ chối vì phiên bản campus cũ. Đây là trạng thái CHẶN: nút duyệt bị khoá cho tới
  // khi người dùng tải bản mới và đọc lại — không auto-retry, không tự duyệt sau khi reload.
  const [versionConflict, setVersionConflict] = useState(false);

  useEffect(() => {
    if (!isOpen || !visitInstanceId) return;
    let cancelled = false;
    setIsLoading(true);
    setLoadError(null);
    setSelectedId(null);
    setDecisionNote('');
    setSubmitError(null);
    setVersionConflict(false);
    delegationsApi
      .getHostCandidates(visitInstanceId)
      .then((data) => { if (!cancelled) setCandidates(data ?? []); })
      .catch(() => { if (!cancelled) setLoadError('Không thể tải danh sách nhân sự. Vui lòng thử lại.'); })
      .finally(() => { if (!cancelled) setIsLoading(false); });
    return () => { cancelled = true; };
  }, [isOpen, visitInstanceId]);

  if (!isOpen) return null;

  const doApprove = async (hostUserId: number) => {
    if (!visitInstanceId || versionConflict) return;
    setIsSubmitting(true);
    setSubmitError(null);
    try {
      // Raw, un-trimmed: the note travels exactly as typed, same as before this component was
      // reskinned onto the shared view.
      await delegationsApi.approveCampusInstance(
        visitRequestId, visitInstanceId, hostUserId, decisionNote, expectedInstanceRowVersion);
      onConfirmed();
    } catch (e: any) {
      // Xung đột phiên bản KHÔNG phải lỗi để "thử lại": nội dung đã khác với bản người duyệt đọc.
      // Khoá hành động quyết định và chỉ chừa lối ra là tải lại rồi xem lại.
      if (isInstanceVersionConflict(e)) {
        setVersionConflict(true);
        setSubmitError(INSTANCE_VERSION_CONFLICT_MESSAGE);
        return;
      }
      const msg = e?.response?.data?.message || e?.response?.data?.title || e?.message || 'Lỗi không xác định';
      setSubmitError(`Không thể duyệt & phân công người phụ trách. ${msg}`);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <HostSelectionModalView
      title={customTitle || 'Duyệt & phân công người phụ trách'}
      subtitle={delegationName}
      infoBanner={
        <>
          Duyệt yêu cầu <span className="font-normal">bắt buộc chọn người phụ trách tiếp đón</span> trong cùng một bước.
          Bạn có thể chọn IC Staff của cơ sở hoặc chính mình phụ trách tiếp đón.
        </>
      }
      candidates={candidates}
      isLoading={isLoading}
      loadError={loadError}
      currentHostUserId={currentHostUserId}
      selectedId={selectedId}
      onSelect={setSelectedId}
      decisionNote={decisionNote}
      onDecisionNoteChange={setDecisionNote}
      decisionNoteLabel="Ghi chú duyệt (không bắt buộc)"
      decisionNotePlaceholder="VD: Đồng ý tiếp nhận đoàn..."
      isSubmitting={isSubmitting}
      versionConflict={versionConflict}
      versionConflictMessage={INSTANCE_VERSION_CONFLICT_MESSAGE}
      onReloadRequested={onReloadRequested}
      reloadLabel="Tải phiên bản mới"
      submitError={submitError}
      confirmLabel="Duyệt & phân công người phụ trách"
      onSubmit={(hostUserId) => void doApprove(hostUserId)}
      onClose={onClose}
      searchPlaceholder="Tìm nhân sự theo tên, email, đơn vị..."
      emptyCandidatesText="Cơ sở chưa có nhân sự (STAFF) phù hợp."
      noMatchText="Không tìm thấy nhân sự phù hợp."
      conflictLabel={(count) => `Trùng ${count} lịch trong khung giờ này`}
      conflictOverlayTitle="Xác nhận phân công người trùng lịch"
      conflictOverlayBody={(fullName) => (
        <>
          Nhân sự <span className="font-bold text-slate-900">{fullName}</span> đang có lịch trùng với thời
          gian đón đoàn. Bạn vẫn muốn duyệt và phân công người này phụ trách tiếp đón?
        </>
      )}
      conflictOverlayCancel="Quay lại"
      conflictOverlayConfirm="Vẫn duyệt & phân công"
      cancelLabel="Hủy bỏ"
      submittingLabel="Đang xử lý..."
      loadingCandidatesLabel="Đang tải danh sách nhân sự..."
      closeTestId="assign-host-modal-close"
      labels={{
        selfHostBadge: 'Tôi nhận phụ trách',
        leaderBadge: 'Leader',
        currentHostBadge: 'Người phụ trách hiện tại',
        noConflict: 'Không trùng lịch với đơn này',
        hasConflict: 'Trùng lịch với đơn này',
        conflictSourceCalendar: 'Lịch cá nhân',
        conflictSourceVisit: 'Đoàn khách khác',
      }}
    />
  );
}
