/**
 * VisitParticipantInvitationDetail — UC-27 Confirm Participation.
 *
 * Màn chi tiết lời mời tham gia tiếp đón đoàn khách của chính người dùng. Đây là nơi DUY
 * NHẤT để Chấp nhận / Từ chối lời mời (KHÔNG đặt ở tab "Đơn mời tham dự" — tab đó chỉ liệt
 * kê các lời mời đã ACCEPTED và chỉ xem). Sau khi chấp nhận, đơn sẽ xuất hiện ở tab 2.
 * Mọi thao tác được backend validate lại (quyền sở hữu, trạng thái, lý do từ chối).
 */

import React, { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { motion } from 'motion/react';
import {
  ChevronLeft, Info, Clock, User, CheckCircle2, AlertCircle, Users, Calendar,
  MapPin, FileText, X, XCircle,
} from 'lucide-react';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import {
  PARTICIPANT_ROLE_LABELS,
  type VisitInvitation,
} from '../../../features/delegations/types/delegations.types';
import { useAuthContext } from '../../../shared/auth/AuthContext';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { formatVietnamDateTime } from '../../../shared/utils/vietnamTime';
import { showSuccessToast, showErrorToast } from '../../../shared/utils/toast';

const formatDateTime = (value?: string | null) => {
  if (!value) return '-';
  return formatVietnamDateTime(value);
};

export function VisitParticipantInvitationDetail() {
  const navigate = useNavigate();
  const { participantId } = useParams();

  const [invitation, setInvitation] = useState<VisitInvitation | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  const { user } = useAuthContext();
  const roleCode = (user?.roleCode || '').toUpperCase();
  const subRole = (user?.subRole || '').toUpperCase();
  const isDept = roleCode === 'DEPARTMENT' || roleCode === 'DEPT';
  const isDeptLeader = isDept && subRole === 'LEADER';
  const isDeptStaff = isDept && subRole === 'STAFF';
  const isStaffLeader = roleCode === 'STAFF' && subRole === 'LEADER';

  const [submitting, setSubmitting] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [rejectOpen, setRejectOpen] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [requestFormOpen, setRequestFormOpen] = useState(false);

  const fetchInvitation = async () => {
    if (!participantId) return;
    try {
      setIsLoading(true);
      setLoadError(null);
      const data = await delegationsApi.getInvitationDetail(participantId);
      setInvitation(data);
    } catch (e: any) {
      const status = e?.response?.status;
      setLoadError(
        status === 404
          ? 'Không tìm thấy lời mời, hoặc lời mời không thuộc về bạn.'
          : 'Không thể tải lời mời. Vui lòng thử lại.',
      );
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchInvitation();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [participantId]);

  const canRespond = invitation?.status === 'INVITED'
    && invitation.allowedActions.includes('ACCEPT_INVITATION');

  const canViewRequestForm = invitation?.allowedActions.includes('VIEW_REQUEST_FORM');
  const canOpenContribution = invitation?.allowedActions.includes('OPEN_CONTRIBUTION');

  // Decline-reason validation (mirrors backend: mandatory, 5–1000 chars after trim).
  const trimmedReason = rejectReason.trim();
  const declineError =
    trimmedReason.length === 0
      ? null // empty: guided by placeholder + disabled button (no noisy error)
      : trimmedReason.length < 5
        ? 'Lý do từ chối phải có ít nhất 5 ký tự.'
        : rejectReason.length > 1000
          ? 'Lý do từ chối không được vượt quá 1000 ký tự.'
          : null;
  const canSubmitDecline = trimmedReason.length >= 5 && rejectReason.length <= 1000;

  const handleAccept = async () => {
    if (!participantId) return;
    setSubmitting(true);
    setActionError(null);
    try {
      await delegationsApi.respondInvitation(participantId, { accept: true });
      await fetchInvitation();
    } catch (e: any) {
      const msg = e?.response?.data?.message || e?.response?.data?.title || e?.message || 'Lỗi không xác định';
      setActionError(`Không thể xác nhận tham gia. ${msg}`);
    } finally {
      setSubmitting(false);
    }
  };

  const handleDecline = async () => {
    if (!participantId) return;
    const reason = rejectReason.trim();
    // Mirror the backend rule: reason is mandatory, 5–1000 chars after trim.
    if (reason.length < 5 || reason.length > 1000) return;
    setSubmitting(true);
    setActionError(null);
    try {
      await delegationsApi.respondInvitation(participantId, { accept: false, declineReason: reason });
      setRejectOpen(false);
      await fetchInvitation();
    } catch (e: any) {
      const msg = e?.response?.data?.message || e?.response?.data?.title || e?.message || 'Lỗi không xác định';
      setActionError(`Không thể từ chối lời mời. ${msg}`);
    } finally {
      setSubmitting(false);
    }
  };

  const statusBadge = (status: string) => {
    const base = 'inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-sm font-bold border';
    if (status === 'ACCEPTED') return <span className={`${base} bg-green-50 text-green-700 border-green-200`}><CheckCircle2 className="w-4 h-4" /> {isDeptStaff ? 'Đã nhận' : 'Đã nhận lời'}</span>;
    if (status === 'DECLINED') return <span className={`${base} bg-red-50 text-red-700 border-red-200`}><XCircle className="w-4 h-4" /> Đã từ chối</span>;
    if (status === 'ASSIGNED') return <span className={`${base} bg-blue-50 text-blue-700 border-blue-200`}><User className="w-4 h-4" /> Mới được giao</span>;
    return <span className={`${base} bg-orange-50 text-orange-700 border-orange-200`}><Clock className="w-4 h-4" /> Chờ phản hồi</span>;
  };

  const getSecondaryStatus = (req?: string, camp?: string) => {
    if (camp === 'CANCELLED' || req === 'CANCELLED') return 'Đã hủy';
    if (req === 'REJECTED') return 'Từ chối';
    if (req === 'PENDING_APPROVAL') return 'Chờ duyệt';
    if (req === 'APPROVED') {
      if (camp === 'ASSIGNED') return 'Đã phân công người phụ trách';
      if (camp === 'BEFORE_VISIT') return 'Trước tiếp khách';
      if (camp === 'DURING_VISIT') return 'Trong tiếp khách';
      if (camp === 'AFTER_VISIT') return 'Chờ đóng đoàn';
      if (camp === 'CLOSED') return 'Đã đóng đoàn';
      return 'Đã duyệt';
    }
    return req || '-';
  };

  return (
    <div className="w-full max-w-3xl mx-auto p-4 sm:p-6 lg:p-8 pb-12 animate-in fade-in duration-300">
      {/* Breadcrumb */}
      <div className="mb-4 flex items-center text-sm font-medium text-gray-500">
        <button onClick={() => navigate('/dashboard')} className="hover:text-[#004c91] transition-colors outline-none">Dashboard</button>
        <span className="mx-2">/</span>
        <button onClick={() => navigate('/dashboard/visit')} className="hover:text-[#004c91] transition-colors outline-none">Quản lý tiếp khách</button>
        <span className="mx-2">/</span>
        <span className="text-[#004c91] font-bold">{isDeptStaff ? 'Nhiệm vụ được giao' : 'Lời mời tham dự'}</span>
      </div>

      <div className="mb-6 flex items-center gap-4">
        <button onClick={() => navigate(-1)} className="flex items-center justify-center p-2.5 rounded-xl border border-slate-200 bg-white shadow-sm hover:border-[#004c91] hover:text-[#004c91] hover:bg-blue-50 transition-all outline-none">
          <ChevronLeft className="w-5 h-5" />
        </button>
        <h1 className="text-2xl sm:text-3xl font-bold text-[#004c91]">{isDeptStaff ? 'Chi tiết nhiệm vụ được giao' : 'Lời mời tham dự tiếp khách'}</h1>
      </div>

      {isLoading ? (
        <div className="py-16 text-center text-slate-500 font-medium">Đang tải lời mời...</div>
      ) : loadError ? (
        <div className="py-16 text-center text-red-500 font-medium">
          <AlertCircle className="w-10 h-10 mx-auto mb-3 text-red-400" /><p>{loadError}</p>
        </div>
      ) : invitation ? (
        <>
          {(invitation.visitRequestStatus === 'CANCELLED' || invitation.visitRequestStatus === 'REJECTED' || invitation.campusVisitStatus === 'CANCELLED') && (
            <div className="mb-4 p-4 rounded-xl border border-red-200 bg-red-50 text-red-700 flex items-center gap-3">
              <AlertCircle className="w-5 h-5 flex-shrink-0" />
              <p className="text-sm font-semibold">Lịch thăm đã bị hủy hoặc từ chối, lời mời này không còn hiệu lực.</p>
            </div>
          )}
          {!(invitation.visitRequestStatus === 'CANCELLED' || invitation.visitRequestStatus === 'REJECTED' || invitation.campusVisitStatus === 'CANCELLED') 
            && invitation.status === 'INVITED' 
            && (invitation.campusVisitStatus === 'DURING_VISIT' || invitation.campusVisitStatus === 'AFTER_VISIT' || invitation.campusVisitStatus === 'CLOSED') && (
            <div className="mb-4 p-4 rounded-xl border border-orange-200 bg-orange-50 text-orange-700 flex items-center gap-3">
              <AlertCircle className="w-5 h-5 flex-shrink-0" />
              <p className="text-sm font-semibold">Lời mời đã hết hạn vì chuyến thăm đã bắt đầu hoặc kết thúc.</p>
            </div>
          )}
          <div className="rounded-2xl border border-slate-200 bg-white shadow-sm overflow-hidden">
          <div className="flex items-center justify-between gap-4 bg-[#004c91] px-6 py-5">
            <div className="flex items-center gap-3 min-w-0">
              <div className="p-2.5 bg-white/10 text-white rounded-xl border border-white/20"><Info className="w-6 h-6" /></div>
              <div className="min-w-0">
                <h2 className="text-lg font-bold text-white truncate">{invitation.delegationName || 'Đoàn khách'}</h2>
                <p className="text-sm text-blue-100 truncate">{invitation.organizationName || '-'}</p>
              </div>
            </div>
            <div className="flex flex-col items-end gap-2">
              {statusBadge(invitation.status)}
              <span className="inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-bold bg-white/20 text-white border border-white/30">
                {getSecondaryStatus(invitation.visitRequestStatus, invitation.campusVisitStatus)}
              </span>
            </div>
          </div>

          <div className="p-6 space-y-5">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {isDeptStaff ? (
                 <>
                   <Field icon={<User className="w-4 h-4" />} label="Người giao" value={invitation.assignedByName || '-'} />
                   <Field icon={<Clock className="w-4 h-4" />} label="Thời gian giao" value={formatDateTime(invitation.assignedAt)} />
                 </>
              ) : (
                 <>
                   <Field icon={<User className="w-4 h-4" />} label="Người mời" value={invitation.invitedByName || '-'} />
                   <Field icon={<Clock className="w-4 h-4" />} label="Thời gian mời" value={formatDateTime(invitation.invitedAt)} />
                 </>
              )}
              <Field icon={<Users className="w-4 h-4" />} label="Vai trò tham gia" value={isStaffLeader && invitation.participantRole === 'IC_SUPPORT' ? 'Staff Leader hỗ trợ IC' : (PARTICIPANT_ROLE_LABELS[invitation.participantRole] ?? invitation.participantRole)} />
              <Field icon={<MapPin className="w-4 h-4" />} label="Cơ sở" value={invitation.campusName || '-'} />
              <Field icon={<Calendar className="w-4 h-4" />} label="Bắt đầu" value={formatDateTime(invitation.plannedStartAt)} />
              <Field icon={<Calendar className="w-4 h-4" />} label="Kết thúc" value={formatDateTime(invitation.plannedEndAt)} />
            </div>

            {/* Form content of the invited campus. These three blocks are what the invitee is being
                asked to say yes to, so they stay put whether or not the guest filled each one in —
                and "Ghi chú gửi FPTU" is deliberately its own block, distinct from the decline reason
                below, which is this participant's own text and never the guest's. */}
            <div>
              <div className="flex items-center gap-2 text-slate-400 mb-1.5"><FileText className="w-4 h-4" /><span className="text-[11px] font-bold uppercase tracking-wider">Mục đích</span></div>
              <p className="p-4 bg-slate-50 rounded-xl text-sm font-medium text-slate-700 leading-relaxed border border-slate-100">
                {invitation.purpose?.trim() || <span className="italic text-slate-400">Chưa có thông tin</span>}
              </p>
            </div>
            <div>
              <div className="flex items-center gap-2 text-slate-400 mb-1.5"><FileText className="w-4 h-4" /><span className="text-[11px] font-bold uppercase tracking-wider">Nội dung làm việc</span></div>
              <p className="p-4 bg-slate-50 rounded-xl text-sm font-medium text-slate-700 leading-relaxed border border-slate-100">
                {invitation.workingContent?.trim() || <span className="italic text-slate-400">Chưa có thông tin</span>}
              </p>
            </div>
            <div>
              <div className="flex items-center gap-2 text-slate-400 mb-1.5"><FileText className="w-4 h-4" /><span className="text-[11px] font-bold uppercase tracking-wider">Ghi chú gửi FPTU</span></div>
              <p className="p-4 bg-slate-50 rounded-xl text-sm font-medium text-slate-700 leading-relaxed border border-slate-100 whitespace-pre-line">
                {invitation.notesToFptu?.trim() || <span className="italic text-slate-400">Chưa có thông tin</span>}
              </p>
            </div>

            {invitation.status === 'DECLINED' && invitation.note && (
              <div className="bg-red-50 rounded-xl p-4 border border-red-100">
                <span className="text-xs font-bold text-red-600 uppercase tracking-wide block mb-1">Lý do từ chối</span>
                <p className="text-sm font-semibold text-red-950 italic">"{invitation.note}"</p>
              </div>
            )}

            {actionError && <p className="text-red-500 text-sm font-medium"><AlertCircle className="w-4 h-4 inline-block mr-1" />{actionError}</p>}
          </div>

          {canRespond ? (
            <div className="flex gap-3 px-6 py-5 border-t border-slate-100 bg-slate-50/60">
              <button type="button" disabled={submitting} onClick={() => { setRejectReason(''); setRejectOpen(true); }}
                className="flex-1 py-3 rounded-xl border-2 border-[#F37021] text-[#F37021] font-bold hover:bg-[#F37021] hover:text-white transition-colors outline-none disabled:opacity-50">
                Từ chối
              </button>
              <button type="button" disabled={submitting} onClick={handleAccept}
                className="flex-1 py-3 rounded-xl bg-[#004c91] text-white font-bold hover:bg-[#003b70] shadow-sm transition-colors outline-none disabled:opacity-50">
                {submitting ? 'Đang xử lý...' : 'Xác nhận tham gia'}
              </button>
            </div>
          ) : invitation.allowedActions.includes('ASSIGN_TO_DEPARTMENT_STAFF') ? (
            <div className="flex gap-3 px-6 py-5 border-t border-slate-100 bg-slate-50/60">
              <button type="button" disabled={submitting} onClick={async () => {
                 const staffIdStr = window.prompt('Nhập ID của Department Staff để giao việc:');
                 if (!staffIdStr) return;
                 const note = window.prompt('Nhập ghi chú/nhiệm vụ:');
                 setSubmitting(true);
                 try {
                   await delegationsApi.visitInvitations.assignDepartmentStaff(participantId!, parseInt(staffIdStr, 10), note || '');
                   showSuccessToast('Giao việc thành công.');
                   await fetchInvitation();
                 } catch (e: any) {
                   showErrorToast(e, 'Không thể giao việc. Vui lòng thử lại.');
                 } finally {
                   setSubmitting(false);
                 }
              }}
                className="flex-1 py-3 rounded-xl bg-[#004c91] text-white font-bold hover:bg-[#003b70] shadow-sm transition-colors outline-none disabled:opacity-50">
                Giao xuống nhân sự phòng ban
              </button>
            </div>
          ) : invitation.status === 'ACCEPTED' ? (
            <div className="px-6 py-5 border-t border-slate-100 bg-green-50/60 text-center text-sm font-semibold text-green-700">
              <div className="mb-4">{isDeptStaff ? 'Bạn đã nhận nhiệm vụ này' : 'Bạn đã xác nhận tham gia'}{invitation.respondedAt ? ` lúc ${formatDateTime(invitation.respondedAt)}` : ''}.</div>
              <div className="flex justify-center gap-4">
                {canViewRequestForm && (
                  <button onClick={() => setRequestFormOpen(true)} className="flex items-center gap-2 px-4 py-2 text-sm font-bold text-[#004c91] bg-white hover:bg-blue-50 border border-[#004c91]/20 rounded-xl transition-colors shadow-sm">
                    <FileText className="w-4 h-4" /> Xem form đăng ký tham quan
                  </button>
                )}
                {canOpenContribution && invitation.visitInstanceId && (
                  <button onClick={() => navigate(`/dashboard/visit/contribution/${invitation.visitInstanceId}`)} className="flex items-center gap-2 px-4 py-2 text-sm font-bold text-orange-600 bg-white hover:bg-orange-50 border border-orange-500/20 rounded-xl transition-colors shadow-sm">
                    <FileText className="w-4 h-4" /> Vào trang đóng góp
                  </button>
                )}
              </div>
            </div>
          ) : invitation.status === 'ASSIGNED' ? (
            <div className="px-6 py-5 border-t border-slate-100 bg-blue-50/60 text-center text-sm font-semibold text-blue-700">
              <div className="mb-4">Bạn mới được giao nhiệm vụ này{invitation.assignedAt ? ` lúc ${formatDateTime(invitation.assignedAt)}` : ''}.</div>
              <div className="flex justify-center gap-4">
                {canViewRequestForm && (
                  <button onClick={() => setRequestFormOpen(true)} className="flex items-center gap-2 px-4 py-2 text-sm font-bold text-[#004c91] bg-white hover:bg-blue-50 border border-[#004c91]/20 rounded-xl transition-colors shadow-sm">
                    <FileText className="w-4 h-4" /> Xem form đăng ký tham quan
                  </button>
                )}
                {canOpenContribution && invitation.visitInstanceId && (
                  <button onClick={() => navigate(`/dashboard/visit/contribution/${invitation.visitInstanceId}`)} className="flex items-center gap-2 px-4 py-2 text-sm font-bold text-orange-600 bg-white hover:bg-orange-50 border border-orange-500/20 rounded-xl transition-colors shadow-sm">
                    <FileText className="w-4 h-4" /> Vào trang đóng góp
                  </button>
                )}
              </div>
            </div>
          ) : invitation.status === 'DECLINED' ? (
            <div className="px-6 py-5 border-t border-slate-100 bg-red-50/60 text-center text-sm font-semibold text-red-600">
              Bạn đã từ chối lời mời này{invitation.respondedAt ? ` lúc ${formatDateTime(invitation.respondedAt)}` : ''}.
            </div>
          ) : null}
        </div>
        </>
      ) : null}

      {requestFormOpen && invitation && (
        <SubmittedVisitRequestDetailModal
          isOpen={requestFormOpen}
          onClose={() => setRequestFormOpen(false)}
          visitRequestId={invitation.visitRequestId}
        />
      )}

      {/* Decline modal */}
      {rejectOpen && (
        <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <motion.div initial={{ opacity: 0, scale: 0.95, y: 10 }} animate={{ opacity: 1, scale: 1, y: 0 }} className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden">
            <div className="px-6 py-4 bg-[#F37021] flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2"><AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Từ chối lời mời</h3>
              <button type="button" disabled={submitting} onClick={() => setRejectOpen(false)} className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5"><X className="w-5 h-5" /></button>
            </div>
            <div className="p-6">
              <p className="text-sm text-gray-500 mb-3">Vui lòng nhập lý do từ chối để người phụ trách tiếp đón có thể nắm thông tin và điều phối lại.</p>
              <label className="block text-sm font-bold text-gray-700 mb-2">Lý do từ chối <span className="text-red-500">*</span></label>
              <textarea value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} placeholder="Ví dụ: Tôi bận lịch cá nhân vào thời gian này..." disabled={submitting} maxLength={1000}
                className={`w-full px-4 py-3 rounded-2xl border focus:ring-4 focus:ring-orange-500/10 outline-none transition-all text-sm min-h-[120px] resize-none bg-gray-50/50 focus:bg-white ${declineError ? 'border-red-300 focus:border-red-400' : 'border-gray-200 focus:border-[#F37021]'}`} />
              <div className="mt-1.5 flex items-center justify-between gap-2">
                <span className="text-xs font-medium text-red-500">{declineError || ''}</span>
                <span className="text-xs text-gray-400 shrink-0">{trimmedReason.length}/1000</span>
              </div>
            </div>
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button type="button" disabled={submitting} onClick={() => setRejectOpen(false)} className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm">Hủy bỏ</button>
              <button type="button" disabled={!canSubmitDecline || submitting} onClick={handleDecline} className="px-6 py-2 rounded-xl font-bold text-white bg-[#F37021] hover:bg-orange-600 shadow-sm transition-all outline-none text-sm disabled:opacity-50 disabled:cursor-not-allowed">{submitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}</button>
            </div>
          </motion.div>
        </div>
      )}
    </div>
  );
}

const Field = ({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) => (
  <div className="p-4 bg-slate-50/80 rounded-xl border border-slate-100">
    <div className="flex items-center gap-2 text-slate-400 mb-1.5">{icon}<span className="text-[11px] font-bold uppercase tracking-wider">{label}</span></div>
    <div className="text-sm font-bold text-slate-800 break-words">{value}</div>
  </div>
);
