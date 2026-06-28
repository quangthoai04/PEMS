import React, { useEffect, useMemo, useState } from 'react';
import {
  AlertCircle,
  Calendar,
  ChevronRight,
  FileSignature,
  FileText,
  Info,
  MapPin,
  User,
  X,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';

export type StaffLeaderTaskModalItem = {
  itemType: 'INVITATION' | 'REQUEST';
  rawId: number | string;
  visitRequestId?: number;
  visitInstanceId?: number;
  status: string;
  title: string;
  delegationName?: string;
  date: string;
  time: string;
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

type HandoverType = 'BORROW' | 'RETURN';
type SignerSide = 'PROVIDER' | 'BORROWER';

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

function signatureText(sig?: { name?: string; signedAt?: string } | null) {
  if (!sig?.name) return null;
  const d = sig.signedAt ? new Date(sig.signedAt) : null;
  const time = d && !Number.isNaN(d.getTime())
    ? `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')} ${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`
    : '';
  return `${sig.name}${time ? ` - ${time}` : ''}`;
}

function formatDateTime(value?: string | null) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')} ${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
}

function formatTime(value?: string | null) {
  if (!value) return '--:--';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value.slice(11, 16) || value;
  return `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
}

function parseNote(note?: string | null, side?: SignerSide) {
  if (!note) return '';
  const label = side === 'BORROWER' ? 'Bên nhận:' : 'Bên giao:';
  const line = note.split('\n').find(x => x.trim().startsWith(label));
  return line ? line.replace(label, '').trim() : note;
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
  const [handoverNotes, setHandoverNotes] = useState({
    bg1: '',
    bg2: '',
    nt1: '',
    nt2: '',
  });

  useEffect(() => {
    if (!item?.rawId) {
      setDetail(null);
      return;
    }
    setLoadingDetail(true);
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
        setHandoverNotes({
          bg1: parseNote(data?.borrowNote, 'PROVIDER') || 'Bàn giao đúng theo nội dung được phân công.',
          bg2: parseNote(data?.borrowNote, 'BORROWER') || 'Đã kiểm tra và nhận bàn giao.',
          nt1: parseNote(data?.returnNote, 'PROVIDER') || 'Đã kiểm tra hiện trạng khi nghiệm thu.',
          nt2: parseNote(data?.returnNote, 'BORROWER') || 'Hoàn tất nghiệm thu nhiệm vụ.',
        });
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

  const signatures = useMemo(() => ({
    bg1: signatureText(detail?.borrowProviderSignature),
    bg2: signatureText(detail?.borrowBorrowerSignature),
    nt1: signatureText(detail?.returnProviderSignature),
    nt2: signatureText(detail?.returnBorrowerSignature),
  }), [detail]);

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

  const handleSignHandover = async (handoverType: HandoverType, signerSide: SignerSide, note: string) => {
    setActionLoading(true);
    try {
      await departmentReceptionTasksApi.signHandover(item.rawId, handoverType, signerSide, note.trim());
      toast.success(handoverType === 'BORROW' ? 'Đã ký bàn giao' : 'Đã ký nghiệm thu');
      await refreshDetail();
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Ký biên bản thất bại');
    } finally {
      setActionLoading(false);
    }
  };

  const SignatureCard = ({
    title,
    color,
    value,
    note,
    noteKey,
    handoverType,
    signerSide,
    buttonLabel,
  }: {
    title: string;
    color: 'blue' | 'orange';
    value: string | null;
    note: string;
    noteKey: keyof typeof handoverNotes;
    handoverType: HandoverType;
    signerSide: SignerSide;
    buttonLabel: string;
  }) => {
    const accent = color === 'orange' ? '#f37021' : '#004c91';
    return (
      <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-sm flex flex-col justify-between gap-4">
        <div>
          <label className="block text-[10px] font-black uppercase tracking-wider mb-1.5" style={{ color: accent }}>
            {title}
          </label>
          <textarea
            rows={2}
            value={note}
            onChange={e => setHandoverNotes(prev => ({ ...prev, [noteKey]: e.target.value }))}
            disabled={!!value || actionLoading}
            className="w-full text-xs p-2.5 border border-slate-200 rounded-xl outline-none resize-none font-sans bg-slate-50/30 focus:ring-1"
            style={{ borderColor: undefined }}
          />
        </div>
        <div className={`border-2 rounded-xl p-3 transition-colors ${value ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-300 bg-white'}`}>
          {value ? (
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-sm shrink-0">✓</div>
              <div className="text-left min-w-0">
                <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider block leading-none mb-0.5">Đã ký duyệt</span>
                <p className="text-[11px] font-extrabold text-slate-800 leading-snug truncate">{value.split(' - ')[0]}</p>
                <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{value.split(' - ')[1] || ''}</p>
              </div>
            </div>
          ) : (
            <div className="flex items-center justify-between gap-3">
              <div className="flex items-center gap-2 min-w-0">
                <FileSignature className="w-4 h-4 shrink-0" style={{ color: accent }} />
                <div className="text-left min-w-0">
                  <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký điện tử</span>
                  <span className="text-[9px] text-slate-400">Nhấn để hoàn tất</span>
                </div>
              </div>
              <button
                type="button"
                disabled={actionLoading}
                onClick={() => handleSignHandover(handoverType, signerSide, note)}
                className="py-2 px-3 bg-slate-50 hover:bg-slate-100 font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 shrink-0 disabled:opacity-50"
                style={{ color: accent }}
              >
                <FileText className="w-3.5 h-3.5" />
                <span>{buttonLabel}</span>
              </button>
            </div>
          )}
        </div>
      </div>
    );
  };

  return (
    <>
      <div className="fixed inset-0 z-50 bg-slate-900/45 flex items-center justify-center p-4" onClick={onClose}>
        <div className="w-full max-w-[1280px] max-h-[92vh] bg-white rounded-2xl border border-slate-200 shadow-2xl flex flex-col overflow-hidden" onClick={e => e.stopPropagation()}>
          <div className={`${isRequest && canShowHandover ? 'bg-[#f37021]' : 'bg-[#004c91]'} px-6 py-5 text-white flex justify-between items-center relative shadow-sm border-b border-white/10`}>
            <div className="flex items-center gap-4 min-w-0">
              <div className="p-3 bg-white/10 text-white rounded-2xl shadow-inner border border-white/20 shrink-0">
                {isRequest ? <FileText className="w-5 h-5" /> : <Info className="w-5 h-5" />}
              </div>
              <div className="min-w-0">
                <h3 className="font-extrabold tracking-tight text-white leading-tight text-xl md:text-3xl truncate">
                  {isRequest ? 'Thông tin chi tiết' : 'Chi tiết thư mời'}
                </h3>
                <p className="text-white/80 mt-1 text-sm font-medium truncate">{isRequest ? 'Nhiệm vụ được giao' : 'Lời mời tham gia được giao'}</p>
              </div>
            </div>
            <button onClick={onClose} className="p-2 hover:bg-white/10 rounded-full text-white shrink-0">
              <X className="w-6 h-6" />
            </button>
          </div>

          <div className="p-6 md:p-8 space-y-6 flex-1 overflow-y-auto bg-white">
            {loadingDetail ? (
              <div className="py-16 text-center text-sm font-semibold text-slate-400">Đang tải chi tiết...</div>
            ) : (
              <>
                <div className="bg-white rounded-2xl border border-slate-200/85 p-6 md:p-8 font-sans w-full max-w-4xl mx-auto space-y-6">
                  <button
                    onClick={() => visitRequestId ? setDetailVisitRequestId(visitRequestId) : toast.error('Không tìm thấy đoàn tiếp khách')}
                    className="w-full py-4 px-5 rounded-xl border border-orange-200 bg-orange-50/50 text-[#f37021] text-sm md:text-base font-black hover:bg-orange-50 flex items-center justify-between gap-3"
                  >
                    <span className="inline-flex items-center gap-2">
                      <User className="w-5 h-5" />
                      Xem chi tiết đoàn đón khách
                    </span>
                    <ChevronRight className="w-5 h-5" />
                  </button>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <User className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Người giao</span>
                      </div>
                      <div className="text-sm font-black text-[#004c91]">{senderName}</div>
                    </div>
                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <MapPin className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Địa điểm</span>
                      </div>
                      <div className="text-sm font-black text-slate-800">{item.location || 'Hòa Lạc'}</div>
                    </div>
                    <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <Calendar className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng</span>
                      </div>
                      <div className="text-[15px] font-bold text-gray-800 flex items-center flex-wrap gap-3">
                        <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{startTime}</span>
                        <ChevronRight className="w-4 h-4 text-gray-400" />
                        <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{endTime}</span>
                        <span className="text-[#004c91] font-bold ml-1">{displayDate}</span>
                      </div>
                    </div>
                    <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100">
                      <div className="flex items-center justify-between gap-3 mb-2">
                        <div className="flex items-center gap-2 text-gray-400">
                          <FileText className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Thông tin đoàn / nhiệm vụ</span>
                        </div>
                        <span className={`px-3 py-1 rounded-full border text-[11px] font-black ${statusBadge[effectiveStatus] || 'bg-slate-100 text-slate-600 border-slate-200'}`}>
                          {statusLabel[effectiveStatus] || effectiveStatus}
                        </span>
                      </div>
                      <div className="text-base font-black text-[#004c91] border-l-4 border-[#f37021] pl-3 py-1">{delegationName || detailTitle}</div>
                      <div className="mt-4 p-5 bg-white rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-gray-200 whitespace-pre-line">
                        {contentText}
                      </div>
                    </div>

                    {isProposing && isRequest && (
                      <div className="col-span-1 sm:col-span-2 p-5 bg-[#fff1e0] rounded-2xl border border-[#ffd3a8] space-y-4">
                        <div className="flex items-center gap-2 text-[#e85c0d]">
                          <Calendar className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng (đề xuất)</span>
                        </div>
                        <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto_1fr] gap-4 items-center">
                          <input
                            type="time"
                            value={proposalStartTime}
                            onChange={e => setProposalStartTime(e.target.value)}
                            className="w-full px-4 py-3 rounded-xl border border-[#ffc288] bg-white text-sm font-bold focus:outline-none focus:border-[#e85c0d]"
                          />
                          <span className="text-[#e85c0d] font-black text-center">-</span>
                          <input
                            type="time"
                            value={proposalEndTime}
                            onChange={e => setProposalEndTime(e.target.value)}
                            className="w-full px-4 py-3 rounded-xl border border-[#ffc288] bg-white text-sm font-bold focus:outline-none focus:border-[#e85c0d]"
                          />
                        </div>
                        <div className="flex items-center gap-2 text-[#e85c0d]">
                          <FileText className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc (đề xuất)</span>
                        </div>
                        <textarea
                          rows={4}
                          value={proposedContent}
                          onChange={e => setProposedContent(e.target.value)}
                          placeholder="Nhập đề xuất nội dung..."
                          className="w-full p-5 bg-white rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-[#ffc288] focus:outline-none focus:border-[#e85c0d] resize-none"
                        />
                        <textarea
                          rows={3}
                          value={proposalNote}
                          onChange={e => setProposalNote(e.target.value)}
                          placeholder="Lý do đề xuất..."
                          className="w-full p-5 bg-white rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-[#ffc288] focus:outline-none focus:border-[#e85c0d] resize-none"
                        />
                      </div>
                    )}

                    {isWaitingProposal && (
                      <div className="col-span-1 sm:col-span-2 p-5 bg-[#fff1e0] rounded-2xl border border-[#ffd3a8] space-y-4">
                        <div className="flex items-start justify-between gap-3">
                          <div className="flex items-center gap-2 text-[#e85c0d]">
                            <AlertCircle className="w-4 h-4" />
                            <span className="text-[11px] font-bold uppercase tracking-wider">Đề xuất thay đổi</span>
                          </div>
                          <span className="px-3 py-1 rounded-full border border-amber-200 bg-amber-50 text-amber-700 text-[11px] font-black whitespace-nowrap">
                            Đang chờ phản hồi
                          </span>
                        </div>
                        <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto_1fr] gap-4 items-center">
                          <div className="px-4 py-3 rounded-xl border border-[#ffc288] bg-white text-sm font-bold text-slate-800">
                            {formatTime(detail?.proposedUsageStartAt)}
                          </div>
                          <span className="text-[#e85c0d] font-black text-center">-</span>
                          <div className="px-4 py-3 rounded-xl border border-[#ffc288] bg-white text-sm font-bold text-slate-800">
                            {formatTime(detail?.proposedUsageEndAt)}
                          </div>
                        </div>
                        <div className="rounded-xl bg-white border border-[#ffc288] p-4">
                          <p className="text-[11px] font-black uppercase tracking-wider text-[#e85c0d] mb-2">Nội dung đề xuất</p>
                          <p className="text-sm font-semibold text-slate-700 whitespace-pre-line">{detail?.proposedDescription || 'Không thay đổi nội dung công việc.'}</p>
                        </div>
                        <div className="rounded-xl bg-white border border-[#ffc288] p-4">
                          <p className="text-[11px] font-black uppercase tracking-wider text-[#e85c0d] mb-2">Ghi chú / lý do đề xuất</p>
                          <p className="text-sm font-semibold text-slate-700 whitespace-pre-line">{detail?.proposalNote || 'Không có ghi chú.'}</p>
                        </div>
                        <div className="text-xs font-semibold text-amber-800 bg-amber-50 border border-amber-100 rounded-xl px-4 py-3">
                          {detail?.proposedByName || 'Nhân sự phụ trách'} đã gửi đề xuất lúc {formatDateTime(detail?.proposedAt) || 'chưa ghi nhận thời gian'}.
                        </div>
                      </div>
                    )}

                    {detail?.rejectReason && (
                      <div className="col-span-1 sm:col-span-2 bg-rose-50 rounded-xl p-4 border border-rose-100 flex gap-2">
                        <AlertCircle className="w-4 h-4 text-rose-500 shrink-0 mt-0.5" />
                        <p className="text-sm text-rose-700 font-medium">{detail.rejectReason}</p>
                      </div>
                    )}

                    {isRequest && Array.isArray(detail?.assignmentHistory) && detail.assignmentHistory.length > 0 && (
                      <div className="col-span-1 sm:col-span-2 rounded-2xl border border-slate-200 bg-slate-50/70 p-4 space-y-3">
                        <p className="text-[11px] font-black uppercase tracking-wider text-slate-500">Lịch sử phản hồi phân công</p>
                        {detail.assignmentHistory.map((att: any) => (
                          <div key={att.attemptId} className="rounded-xl bg-white border border-slate-200 px-4 py-3 text-xs font-semibold text-slate-700">
                            <span className="font-black text-[#004c91]">{att.assigneeName}</span>
                            {att.status === 'ACCEPTED' && <> đã chấp nhận lúc <span className="font-black">{att.respondedAt || att.assignedAt}</span>.</>}
                            {att.status === 'DECLINED' && <> đã từ chối lúc <span className="font-black">{att.respondedAt || att.assignedAt}</span>{att.responseNote ? <> với lý do: <span className="text-rose-600 font-black">{att.responseNote}</span></> : '.'}</>}
                            {att.status === 'PENDING' && <> đang chờ phản hồi từ lúc <span className="font-black">{att.assignedAt}</span>.</>}
                          </div>
                        ))}
                      </div>
                    )}

                    {!isRequest && ['ACCEPTED', 'DECLINED', 'REJECTED', 'DONE', 'IN_PROGRESS'].includes(effectiveStatus) && (
                      <div className="col-span-1 sm:col-span-2 rounded-2xl border border-slate-200 bg-slate-50/70 p-4 text-xs font-semibold text-slate-700">
                        <span className="font-black text-[#004c91]">{detail?.responderName || 'Nhân sự phụ trách'}</span>
                        {effectiveStatus === 'DECLINED' || effectiveStatus === 'REJECTED'
                          ? <> đã từ chối lúc <span className="font-black">{detail?.actionTime || 'chưa ghi nhận thời gian'}</span>{detail?.rejectReason ? <> với lý do: <span className="text-rose-600 font-black">{detail.rejectReason}</span></> : '.'}</>
                          : <> đã chấp nhận lúc <span className="font-black">{detail?.actionTime || 'chưa ghi nhận thời gian'}</span>.</>}
                      </div>
                    )}
                  </div>

                  {canShowHandover && (
                    <div className="space-y-6 mt-4">
                      <div className="relative my-6">
                        <div className="absolute inset-0 flex items-center" aria-hidden="true">
                          <div className="w-full border-t border-slate-200" />
                        </div>
                        <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                          <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-sm text-[11px] tracking-widest uppercase">Bàn giao</span>
                        </div>
                      </div>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <SignatureCard title="Ghi chú Bên Giao" color="blue" value={signatures.bg1} note={handoverNotes.bg1} noteKey="bg1" handoverType="BORROW" signerSide="PROVIDER" buttonLabel="Ký xác nhận (BG1)" />
                        <SignatureCard title="Ghi chú Bên Nhận" color="orange" value={signatures.bg2} note={handoverNotes.bg2} noteKey="bg2" handoverType="BORROW" signerSide="BORROWER" buttonLabel="Ký xác nhận (BG2)" />
                      </div>
                      {(signatures.bg1 && signatures.bg2) || effectiveStatus === 'IN_PROGRESS' || effectiveStatus === 'DONE' ? (
                        <>
                          <div className="relative my-8">
                            <div className="absolute inset-0 flex items-center" aria-hidden="true">
                              <div className="w-full border-t border-slate-200" />
                            </div>
                            <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                              <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-sm text-[11px] tracking-widest uppercase">Nghiệm thu</span>
                            </div>
                          </div>
                          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <SignatureCard title="Ghi chú Nghiệm thu (Bên Giao)" color="blue" value={signatures.nt1} note={handoverNotes.nt1} noteKey="nt1" handoverType="RETURN" signerSide="PROVIDER" buttonLabel="Ký nghiệm thu (NT1)" />
                            <SignatureCard title="Ghi chú Nghiệm thu (Bên Nhận)" color="orange" value={signatures.nt2} note={handoverNotes.nt2} noteKey="nt2" handoverType="RETURN" signerSide="BORROWER" buttonLabel="Ký nghiệm thu (NT2)" />
                          </div>
                        </>
                      ) : (
                        <div className="bg-amber-50/85 rounded-2xl p-4 text-center text-xs text-amber-900 border border-amber-200 flex items-center justify-center gap-2 font-sans">
                          <span className="w-2 h-2 rounded-full bg-amber-500" />
                          <span className="font-semibold text-amber-950">Vui lòng ký đủ 2 ô Bàn giao trước khi mở nghiệm thu.</span>
                        </div>
                      )}
                    </div>
                  )}
                </div>

                <div className="w-full max-w-4xl mx-auto space-y-3">
                  {canAct && (
                    <button onClick={handleAccept} disabled={actionLoading} className="w-full py-3 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                      ✓ Chấp nhận
                    </button>
                  )}
                  {canDecline && (
                    <>
                      {!isRejecting ? (
                        <button onClick={() => setIsRejecting(true)} disabled={actionLoading} className="w-full py-3 bg-white border border-rose-200 text-rose-600 text-sm font-black rounded-xl hover:bg-rose-50 transition-colors disabled:opacity-50">
                          Từ chối phân công
                        </button>
                      ) : (
                        <div className="space-y-2">
                          <textarea rows={3} value={rejectReason} onChange={e => setRejectReason(e.target.value)} placeholder="Lý do từ chối..." className="w-full px-4 py-3 text-sm border border-slate-200 rounded-xl outline-none focus:border-rose-400 resize-none" />
                          <div className="flex gap-2">
                            <button onClick={handleDecline} disabled={actionLoading || !rejectReason.trim()} className="flex-1 py-2.5 bg-rose-600 hover:bg-rose-700 text-white text-sm font-black rounded-xl disabled:opacity-50">Xác nhận từ chối</button>
                            <button onClick={() => setIsRejecting(false)} className="px-4 py-2.5 text-sm font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
                          </div>
                        </div>
                      )}
                    </>
                  )}
                  {canPropose && (
                    <div className="flex justify-end">
                      {!isProposing ? (
                        <button onClick={() => setIsProposing(true)} disabled={actionLoading} className="px-5 py-2.5 bg-[#f37021] text-white hover:bg-orange-600 text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                          Đề xuất thay đổi
                        </button>
                      ) : (
                        <button onClick={handlePropose} disabled={actionLoading} className="px-5 py-2.5 bg-[#16a34a] text-white hover:bg-green-700 text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                          Gửi đề xuất
                        </button>
                      )}
                    </div>
                  )}
                </div>
              </>
            )}
          </div>

          <div className="bg-slate-50 px-6 py-4 flex justify-end items-center border-t border-slate-200 rounded-b-2xl">
            <button onClick={onClose} className="px-5 py-2.5 bg-[#004c91] text-white hover:opacity-90 text-[11px] font-bold rounded-xl transition-colors shadow-sm">
              {canShowHandover ? 'Đóng biên bản bàn giao & nghiệm thu' : 'Đóng bảng chi tiết'}
            </button>
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
