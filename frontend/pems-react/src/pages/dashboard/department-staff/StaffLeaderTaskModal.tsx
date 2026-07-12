/**
 * StaffLeaderTaskModal – Modal chi tiết thư mời / nhiệm vụ logistics cho Department Staff.
 * Layout key-value compact, ít khung; biên bản bàn giao/nghiệm thu dùng chung TaskHandoverModal
 * với Dept Leader (văn bản biên bản đầy đủ + rule ký):
 * - BORROW: phòng ban (PROVIDER) ký bàn giao trước, Host ký nhận sau.
 * - RETURN: Host ký trả trước, phòng ban ký nhận lại sau.
 */
import React, { useEffect, useMemo, useState } from 'react';
import {
  AlertCircle,
  ChevronRight,
  FileSignature,
  FileText,
  Info,
  User,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { TaskHandoverModal } from '../departments/TaskHandoverModal';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import { toVietnamDateTimeLocalInput } from '../../../shared/utils/vietnamTime';

export type StaffLeaderTaskModalItem = {
  itemType: 'INVITATION' | 'REQUEST';
  rawId: number | string;
  visitRequestId?: number;
  visitInstanceId?: number;
  status?: string;
  title?: string;
  delegationName?: string;
  date?: string;
  time?: string;
  location?: string;
  host?: string;
  purpose?: string;
  canAccept?: boolean;
  canDecline?: boolean;
  canProposeChange?: boolean;
  canSignBorrow?: boolean;
  canSignReturn?: boolean;
};

type Props = {
  item: StaffLeaderTaskModalItem | null;
  onClose: () => void;
  onRefresh: () => void;
};

const statusLabel: Record<string, string> = {
  ASSIGNED: 'Mới được giao',
  ACCEPTED: 'Đã chấp nhận',
  DECLINED: 'Đã từ chối',
  IN_PROGRESS: 'Đang thực hiện',
  DONE: 'Hoàn thành',
  CANCELLED: 'Đã hủy',
  CHANGE_PROPOSED: 'Đang đề xuất',
  REQUESTED: 'Chờ phân công',
  REJECTED: 'Đã từ chối',
};

const statusBadge: Record<string, string> = {
  ASSIGNED: 'bg-orange-100 text-orange-700 border-orange-200',
  ACCEPTED: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  DECLINED: 'bg-slate-100 text-slate-600 border-slate-200',
  IN_PROGRESS: 'bg-cyan-100 text-cyan-700 border-cyan-200',
  DONE: 'bg-slate-100 text-slate-600 border-slate-200',
  CANCELLED: 'bg-gray-100 text-gray-500 border-gray-200',
  CHANGE_PROPOSED: 'bg-amber-100 text-amber-700 border-amber-200',
  REJECTED: 'bg-rose-100 text-rose-700 border-rose-200',
};

function toIsoDate(date: string) {
  if (/^\d{4}-\d{2}-\d{2}$/.test(date)) return date;
  const parts = date.split('-');
  if (parts.length === 3) return `${parts[2]}-${parts[1]}-${parts[0]}`;
  return date;
}

function formatDateTime(value?: string | null) {
  if (!value) return '';
  const local = toVietnamDateTimeLocalInput(value); // giờ VN cố định
  if (!local) return value;
  return `${local.slice(11, 16)} ${local.slice(8, 10)}/${local.slice(5, 7)}/${local.slice(0, 4)}`;
}

function formatTime(value?: string | null) {
  if (!value) return '--:--';
  const local = toVietnamDateTimeLocalInput(value);
  return local ? local.slice(11, 16) : (value.slice(11, 16) || value);
}

type SignatureInfo = { name?: string; signedAt?: string } | null | undefined;

/** Dòng key-value compact cho phần thông tin chi tiết */
function InfoRow({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-3 py-1.5">
      <span className="w-28 shrink-0 text-[11px] font-bold uppercase tracking-wide text-slate-400 pt-0.5">{label}</span>
      <div className="min-w-0 flex-1 text-sm font-semibold text-slate-800">{children}</div>
    </div>
  );
}

export function StaffLeaderTaskModal({ item, onClose, onRefresh }: Props) {
  const [detail, setDetail] = useState<any>(null);
  const [loadingDetail, setLoadingDetail] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);
  const [isRejecting, setIsRejecting] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [isProposing, setIsProposing] = useState(false);
  const [proposalStartTime, setProposalStartTime] = useState('');
  const [proposalEndTime, setProposalEndTime] = useState('');
  const [proposalNote, setProposalNote] = useState('');
  const [proposedContent, setProposedContent] = useState('');
  const [detailVisitRequestId, setDetailVisitRequestId] = useState<number | null>(null);


  useEffect(() => {
    if (!item?.rawId) {
      setDetail(null);
      return;
    }
    setLoadingDetail(true);
    setIsRejecting(false);
    setRejectReason('');
    setIsProposing(false);
    setProposalStartTime('');
    setProposalEndTime('');
    setProposalNote('');
    setProposedContent('');

    (async () => {
      try {
        const data = item.itemType === 'INVITATION'
          ? await departmentReceptionTasksApi.getInvitationDetail(item.rawId)
          : await departmentReceptionTasksApi.getRequestDetail(item.rawId);
        setDetail(data);
      } catch (e: any) {
        toast.error(e?.response?.data?.message || 'Không tải được thông tin chi tiết');
        setDetail(null);
      } finally {
        setLoadingDetail(false);
      }
    })();
  }, [item?.rawId, item?.itemType]);

  const effectiveStatus = detail?.status || item?.status || '';
  const visitRequestId = Number(detail?.visitRequestId || item?.visitRequestId || 0) || null;
  const startTime = detail?.startTime || item?.time?.split('-')[0]?.trim() || '';
  const endTime = detail?.endTime || item?.time?.split('-')[1]?.trim() || '';
  const displayDate = detail?.date || item?.date?.split('-').reverse().join('-') || '';
  const detailTitle = detail?.title || item?.title || '';
  const delegationName = detail?.delegationName || item?.delegationName || '';
  const senderName = detail?.senderName || item?.host || 'Hệ thống';
  const contentText = detail?.description || detail?.note || item?.purpose || detailTitle;
  const isRequest = item?.itemType === 'REQUEST';
  const canAct = effectiveStatus === 'ASSIGNED' && (item?.canAccept ?? true);
  const canDecline = effectiveStatus === 'ASSIGNED' && (item?.canDecline ?? true);
  const canPropose = isRequest
    && effectiveStatus === 'ASSIGNED'
    && (item?.canProposeChange ?? true);
  const canShowHandover = isRequest && ['ACCEPTED', 'IN_PROGRESS', 'DONE'].includes(effectiveStatus);
  const isWaitingProposal = isRequest && effectiveStatus === 'CHANGE_PROPOSED';

  // Chữ ký 4 bên – rule như Dept Leader (TaskHandoverModal):
  // Bàn giao: PROVIDER (phòng ban) ký trước, BORROWER (Host) ký nhận sau.
  // Nghiệm thu: BORROWER (Host) ký trả trước, PROVIDER (phòng ban) ký nhận lại sau.
  const signatures = useMemo(() => ({
    borrowProvider: detail?.borrowProviderSignature as SignatureInfo,
    borrowBorrower: detail?.borrowBorrowerSignature as SignatureInfo,
    returnBorrower: detail?.returnBorrowerSignature as SignatureInfo,
    returnProvider: detail?.returnProviderSignature as SignatureInfo,
  }), [detail]);

  // TaskHandoverModal (dùng chung với Dept Leader) nhận DTO PascalCase — map từ detail camelCase.
  const toPascalSig = (sig: SignatureInfo) =>
    sig?.name ? { Name: sig.name, SignedAt: sig.signedAt } : null;
  const handoverDto = detail ? {
    LogisticsItemId: item?.rawId,
    Title: detail.title || item?.title,
    Quantity: detail.quantity,
    DelegationName: detail.delegationName || item?.delegationName,
    SenderName: detail.senderName || item?.host,
    AssigneeName: detail.assigneeName || detail.currentResponsibleName,
    BorrowNote: detail.borrowNote,
    ReturnNote: detail.returnNote,
    BorrowProviderSignature: toPascalSig(signatures.borrowProvider),
    BorrowBorrowerSignature: toPascalSig(signatures.borrowBorrower),
    ReturnBorrowerSignature: toPascalSig(signatures.returnBorrower),
    ReturnProviderSignature: toPascalSig(signatures.returnProvider),
  } : null;

  if (!item) return null;

  const refreshDetail = async () => {
    const data = isRequest
      ? await departmentReceptionTasksApi.getRequestDetail(item.rawId)
      : await departmentReceptionTasksApi.getInvitationDetail(item.rawId);
    setDetail(data);
    onRefresh();
  };

  const handleAccept = async () => {
    setActionLoading(true);
    try {
      if (isRequest) {
        await departmentReceptionTasksApi.acceptAssignment(item.rawId);
        toast.success('Đã chấp nhận phân công');
      } else {
        await departmentReceptionTasksApi.acceptInvitation(item.rawId);
        toast.success('Đã chấp nhận thư mời');
      }
      await refreshDetail();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Thao tác thất bại');
    } finally {
      setActionLoading(false);
    }
  };

  const handleDecline = async () => {
    if (!rejectReason.trim()) {
      toast.error('Vui lòng nhập lý do từ chối');
      return;
    }
    setActionLoading(true);
    try {
      if (isRequest) {
        await departmentReceptionTasksApi.declineAssignment(item.rawId, rejectReason.trim());
        toast.success('Đã từ chối phân công');
      } else {
        await departmentReceptionTasksApi.declineInvitation(item.rawId, rejectReason.trim());
        toast.success('Đã từ chối thư mời');
      }
      await refreshDetail();
      onClose();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Từ chối thất bại');
    } finally {
      setActionLoading(false);
    }
  };

  const handlePropose = async () => {
    if (!proposalStartTime || !proposalEndTime) {
      toast.error('Vui lòng chọn giờ bắt đầu và giờ kết thúc');
      return;
    }
    if (proposalEndTime <= proposalStartTime) {
      toast.error('Giờ kết thúc phải sau giờ bắt đầu');
      return;
    }
    const note = proposalNote.trim() || proposedContent.trim();
    if (!note) {
      toast.error('Vui lòng nhập nội dung hoặc lý do đề xuất');
      return;
    }
    setActionLoading(true);
    try {
      const baseDate = toIsoDate(item.date || '');
      await departmentReceptionTasksApi.proposeChange(item.rawId, {
        proposedUsageStartAt: baseDate + 'T' + proposalStartTime + ':00',
        proposedUsageEndAt: baseDate + 'T' + proposalEndTime + ':00',
        proposedDescription: proposedContent.trim() || note,
        proposalNote: note,
      });
      toast.success('Đã gửi đề xuất thay đổi');
      setIsProposing(false);
      await refreshDetail();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi đề xuất thất bại');
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <>
      <div className="fixed inset-0 z-50 bg-slate-900/45 flex items-center justify-center p-4" onClick={onClose}>
        <div className="w-full max-w-3xl max-h-[92vh] bg-white rounded-2xl border border-slate-200 shadow-2xl flex flex-col overflow-hidden" onClick={e => e.stopPropagation()}>
          {/* Header compact */}
          <div className={`${isRequest && canShowHandover ? 'bg-[#f37021]' : 'bg-[#004c91]'} px-5 py-3.5 text-white flex items-center justify-between gap-3`}>
            <div className="flex items-center gap-2.5 min-w-0">
              {isRequest ? <FileText className="w-5 h-5 shrink-0 opacity-90" /> : <Info className="w-5 h-5 shrink-0 opacity-90" />}
              <div className="min-w-0">
                <h3 className="text-base font-black leading-tight truncate">
                  {isRequest ? 'Chi tiết nhiệm vụ được giao' : 'Chi tiết thư mời tham gia'}
                </h3>
                <p className="text-[11px] text-white/75 font-semibold truncate">{delegationName || detailTitle}</p>
              </div>
            </div>
            <button onClick={onClose} className="p-1.5 hover:bg-white/10 rounded-full text-white shrink-0">
              <X className="w-5 h-5" />
            </button>
          </div>

          {/* Body */}
          <div className="flex-1 overflow-y-auto px-5 py-4">
            {loadingDetail ? (
              <div className="py-14 text-center text-sm font-semibold text-slate-400">Đang tải chi tiết...</div>
            ) : (
              <>
                {/* Thông tin key-value compact */}
                <div className="divide-y divide-slate-100">
                  <InfoRow label="Đoàn khách">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <span className="min-w-0 truncate">{delegationName || detailTitle || '—'}</span>
                      <button
                        onClick={() => visitRequestId ? setDetailVisitRequestId(visitRequestId) : toast.error('Không tìm thấy đoàn tiếp khách')}
                        className="inline-flex items-center gap-1 text-[11px] font-black text-[#f37021] hover:underline shrink-0">
                        <User className="w-3.5 h-3.5" />
                        Xem chi tiết đoàn
                        <ChevronRight className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </InfoRow>
                  <InfoRow label="Loại nhiệm vụ">
                    {isRequest ? 'Nhiệm vụ hỗ trợ (logistics)' : 'Thư mời tham gia'}
                  </InfoRow>
                  <InfoRow label="Người giao">{senderName}</InfoRow>
                  <InfoRow label="Địa điểm">{item.location || 'Hòa Lạc'}</InfoRow>
                  <InfoRow label="Thời gian">
                    {startTime || '--:--'} - {endTime || '--:--'}{displayDate ? ` · ${displayDate}` : ''}
                  </InfoRow>
                  <InfoRow label="Trạng thái">
                    <span className={`inline-flex px-2.5 py-0.5 rounded-full border text-[11px] font-black ${statusBadge[effectiveStatus] || 'bg-slate-100 text-slate-600 border-slate-200'}`}>
                      {statusLabel[effectiveStatus] || effectiveStatus}
                    </span>
                  </InfoRow>
                </div>

                {/* Nội dung / ghi chú */}
                <div className="mt-2 pt-3 border-t border-slate-100">
                  <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400 mb-1">Nội dung / Ghi chú</p>
                  <p className="text-sm text-slate-700 whitespace-pre-line leading-relaxed">{contentText || '—'}</p>
                </div>

                {/* Form đề xuất thay đổi (mở từ footer) */}
                {isProposing && isRequest && (
                  <div className="mt-3 pt-3 border-t border-slate-100 space-y-2">
                    <p className="text-[11px] font-black uppercase tracking-wide text-[#e85c0d]">Đề xuất thay đổi</p>
                    <div className="grid grid-cols-2 gap-2">
                      <div>
                        <label className="block text-[10px] font-bold uppercase text-slate-400 mb-1">Giờ bắt đầu mới</label>
                        <input type="time" value={proposalStartTime} onChange={e => setProposalStartTime(e.target.value)}
                          className="w-full px-3 py-2 rounded-lg border border-slate-200 text-sm font-bold outline-none focus:border-[#e85c0d]" />
                      </div>
                      <div>
                        <label className="block text-[10px] font-bold uppercase text-slate-400 mb-1">Giờ kết thúc mới</label>
                        <input type="time" value={proposalEndTime} onChange={e => setProposalEndTime(e.target.value)}
                          className="w-full px-3 py-2 rounded-lg border border-slate-200 text-sm font-bold outline-none focus:border-[#e85c0d]" />
                      </div>
                    </div>
                    <textarea
                      rows={3}
                      value={proposedContent}
                      onChange={e => setProposedContent(e.target.value)}
                      placeholder="Nội dung chi tiết công việc (đề xuất)..."
                      className="w-full px-3 py-2 rounded-lg border border-slate-200 text-sm outline-none focus:border-[#e85c0d] resize-none"
                    />
                    <textarea
                      rows={2}
                      value={proposalNote}
                      onChange={e => setProposalNote(e.target.value)}
                      placeholder="Lý do đề xuất..."
                      className="w-full px-3 py-2 rounded-lg border border-slate-200 text-sm outline-none focus:border-[#e85c0d] resize-none"
                    />
                  </div>
                )}

                {/* Đề xuất đang chờ phản hồi */}
                {isWaitingProposal && (
                  <div className="mt-3 pt-3 border-t border-slate-100">
                    <div className="flex items-center justify-between gap-2 mb-1">
                      <p className="text-[11px] font-black uppercase tracking-wide text-[#e85c0d] inline-flex items-center gap-1.5">
                        <AlertCircle className="w-3.5 h-3.5" /> Đề xuất thay đổi
                      </p>
                      <span className="px-2 py-0.5 rounded-full border border-amber-200 bg-amber-50 text-amber-700 text-[10px] font-black whitespace-nowrap">
                        Đang chờ phản hồi
                      </span>
                    </div>
                    <div className="divide-y divide-slate-100">
                      <InfoRow label="Thời gian mới">
                        {formatTime(detail?.proposedUsageStartAt)} - {formatTime(detail?.proposedUsageEndAt)}
                      </InfoRow>
                      <InfoRow label="Nội dung">
                        <span className="whitespace-pre-line font-medium">{detail?.proposedDescription || 'Không thay đổi nội dung công việc.'}</span>
                      </InfoRow>
                      <InfoRow label="Lý do">
                        <span className="whitespace-pre-line font-medium">{detail?.proposalNote || 'Không có ghi chú.'}</span>
                      </InfoRow>
                    </div>
                    <p className="mt-1 text-[11px] font-semibold text-amber-800">
                      {detail?.proposedByName || 'Nhân sự phụ trách'} đã gửi đề xuất lúc {formatDateTime(detail?.proposedAt) || 'chưa ghi nhận thời gian'}.
                    </p>
                  </div>
                )}

                {/* Lý do từ chối (nếu có) */}
                {detail?.rejectReason && (
                  <div className="mt-3 pt-3 border-t border-slate-100 flex gap-2">
                    <AlertCircle className="w-4 h-4 text-rose-500 shrink-0 mt-0.5" />
                    <p className="text-sm text-rose-700 font-medium">{detail.rejectReason}</p>
                  </div>
                )}

                {/* Lịch sử phản hồi phân công */}
                {isRequest && Array.isArray(detail?.assignmentHistory) && detail.assignmentHistory.length > 0 && (
                  <div className="mt-3 pt-3 border-t border-slate-100">
                    <p className="text-[11px] font-bold uppercase tracking-wide text-slate-400 mb-1">Lịch sử phản hồi phân công</p>
                    <div className="divide-y divide-slate-100">
                      {detail.assignmentHistory.map((att: any) => (
                        <div key={att.attemptId} className="py-1.5 text-xs font-semibold text-slate-700">
                          <span className="font-black text-[#004c91]">{att.assigneeName}</span>
                          {att.status === 'ACCEPTED' && <> đã chấp nhận lúc <span className="font-black">{att.respondedAt || att.assignedAt}</span>.</>}
                          {(att.status === 'DECLINED' || att.status === 'REJECTED') && <> đã từ chối lúc <span className="font-black">{att.respondedAt || att.assignedAt}</span>{att.responseNote ? <> với lý do: <span className="text-rose-600 font-black">{att.responseNote}</span></> : '.'}</>}
                          {att.status === 'PENDING' && <> đang chờ phản hồi từ lúc <span className="font-black">{att.assignedAt}</span>.</>}
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Phản hồi thư mời */}
                {!isRequest && ['ACCEPTED', 'DECLINED', 'REJECTED', 'DONE', 'IN_PROGRESS'].includes(effectiveStatus) && (
                  <div className="mt-3 pt-3 border-t border-slate-100 text-xs font-semibold text-slate-700">
                    <span className="font-black text-[#004c91]">{detail?.responderName || 'Nhân sự phụ trách'}</span>
                    {effectiveStatus === 'DECLINED' || effectiveStatus === 'REJECTED'
                      ? <> đã từ chối lúc <span className="font-black">{detail?.actionTime || 'chưa ghi nhận thời gian'}</span>{detail?.rejectReason ? <> với lý do: <span className="text-rose-600 font-black">{detail.rejectReason}</span></> : '.'}</>
                      : <> đã chấp nhận lúc <span className="font-black">{detail?.actionTime || 'chưa ghi nhận thời gian'}</span>.</>}
                  </div>
                )}

                {/* Biên bản bàn giao & nghiệm thu — inline giống bên Dept Leader */}
                {canShowHandover && handoverDto && (
                  <TaskHandoverModal
                    inline
                    detailData={handoverDto}
                    onSuccess={refreshDetail}
                  />
                )}
              </>
            )}
          </div>

          {/* Footer: action gọn */}
          <div className="bg-slate-50 border-t border-slate-200 px-5 py-3">
            {isRejecting && canDecline && (
              <textarea
                rows={2}
                value={rejectReason}
                onChange={e => setRejectReason(e.target.value)}
                placeholder="Lý do từ chối..."
                autoFocus
                className="w-full mb-2 px-3 py-2 text-sm border border-slate-200 rounded-xl outline-none focus:border-rose-400 resize-none"
              />
            )}
            <div className="flex flex-wrap items-center justify-end gap-2">
              {canAct && !isRejecting && (
                <button onClick={handleAccept} disabled={actionLoading}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white text-xs font-black rounded-xl transition-colors disabled:opacity-50">
                  ✓ Chấp nhận
                </button>
              )}
              {canDecline && (
                !isRejecting ? (
                  <button onClick={() => setIsRejecting(true)} disabled={actionLoading}
                    className="px-4 py-2 bg-white border border-rose-200 text-rose-600 text-xs font-black rounded-xl hover:bg-rose-50 transition-colors disabled:opacity-50">
                    Từ chối
                  </button>
                ) : (
                  <>
                    <button onClick={handleDecline} disabled={actionLoading || !rejectReason.trim()}
                      className="px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white text-xs font-black rounded-xl disabled:opacity-50">
                      Xác nhận từ chối
                    </button>
                    <button onClick={() => setIsRejecting(false)}
                      className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">
                      Hủy
                    </button>
                  </>
                )
              )}
              {canPropose && !isRejecting && (
                !isProposing ? (
                  <button onClick={() => setIsProposing(true)} disabled={actionLoading}
                    className="px-4 py-2 bg-[#f37021] text-white hover:bg-orange-600 text-xs font-black rounded-xl transition-colors disabled:opacity-50">
                    Đề xuất thay đổi
                  </button>
                ) : (
                  <>
                    <button onClick={handlePropose} disabled={actionLoading}
                      className="px-4 py-2 bg-[#16a34a] text-white hover:bg-green-700 text-xs font-black rounded-xl transition-colors disabled:opacity-50">
                      Gửi đề xuất
                    </button>
                    <button onClick={() => setIsProposing(false)}
                      className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">
                      Hủy
                    </button>
                  </>
                )
              )}
              <button onClick={onClose}
                className="px-4 py-2 bg-[#004c91] text-white hover:opacity-90 text-xs font-bold rounded-xl transition-colors">
                Đóng
              </button>
            </div>
          </div>
        </div>
      </div>

      <SubmittedVisitRequestDetailModal
        isOpen={detailVisitRequestId != null}
        visitRequestId={detailVisitRequestId}
        onClose={() => setDetailVisitRequestId(null)}
      />
    </>
  );
}
