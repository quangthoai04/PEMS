/**
 * StaffVisitDetailModal — modal chi tiết "Yêu cầu tham quan" mở từ bảng lịch dashboard
 * của Staff / Staff Leader. Dữ liệu thật từ GET /api/dashboard/staff/calendar/{id}/detail.
 * Button chỉ render theo allowedActions do backend trả (không tự suy quyền ở frontend).
 */
import React, { useEffect, useState } from 'react';
import { motion } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import {
  X, Loader2, MapPin, Clock, Users, User, Phone, Mail, Globe, Car, Camera,
  FileText, AlertCircle, Check, UserCheck, MessageSquare, Ban, Settings, UserPlus,
} from 'lucide-react';
import {
  staffCalendarApi,
  type StaffCalendarDetail,
} from '../../../../features/dashboard/api/staffCalendarApi';
import { timeAgo } from '../../../../features/notifications/components/NotificationBellButton';
import type { NotificationItem } from '../../../../features/notifications/types/notification.types';
import { toVietnamDateTimeLocalInput } from '../../../../shared/utils/vietnamTime';

type StaffVisitDetailModalProps = {
  isOpen: boolean;
  visitInstanceId: number | null;
  /** Tăng giá trị để buộc refetch (sau khi gán host / từ chối thành công). */
  refreshKey?: number;
  onClose: () => void;
  /** Staff Leader bấm "Tôi là người phụ trách" — tự nhận làm host, submit ngay không cần chọn. */
  onSelfHost?: (detail: StaffCalendarDetail) => void;
  /** True khi đang gọi API self-host cho item đang mở (disable nút, hiện spinner). */
  selfHostSubmitting?: boolean;
  /** Staff Leader bấm "Gán người phụ trách" — mở modal chọn IC Staff làm host. */
  onAssignHost?: (detail: StaffCalendarDetail) => void;
  /** Staff Leader: từ chối campus instance của campus mình (lý do bắt buộc). */
  onReject?: (detail: StaffCalendarDetail) => void;
  /** Thông báo chưa đọc gắn với đơn tham quan này — hiện khung "Thay đổi mới". */
  changeNotifs?: NotificationItem[];
  /** Bấm 1 thay đổi → mark read + trỏ tới đúng chỗ như thông báo. */
  onChangeNotifClick?: (n: NotificationItem) => void;
};

const COLOR_BADGE: Record<string, string> = {
  NEEDS_ACTION: 'bg-amber-100 text-amber-800',
  MINE: 'bg-blue-100 text-[#004c91]',
  PROCESSED: 'bg-emerald-100 text-emerald-700',
  CANCELLED_OR_EXPIRED: 'bg-slate-200 text-slate-600',
  NEUTRAL: 'bg-slate-100 text-slate-600',
};

const VISIT_TYPE_LABELS: Record<string, string> = {
  CAMPUS_TOUR: 'Tham quan campus',
  MEETING: 'Họp / làm việc',
  EVENT: 'Sự kiện',
  OTHER: 'Khác',
};

const MEDIA_CONSENT_LABELS: Record<string, string> = {
  AGREED: 'Đồng ý ghi hình/chụp ảnh',
  DECLINED: 'Không đồng ý ghi hình',
  PARTIAL: 'Đồng ý một phần',
};

const PARTICIPANT_ROLE_LABELS: Record<string, string> = {
  IC_HOST: 'Host',
  IC_SUPPORT: 'Staff hỗ trợ',
  DEPT_SUPPORT: 'Phòng ban hỗ trợ',
  STUDENT: 'Sinh viên hỗ trợ',
};

const PARTICIPANT_STATUS_LABELS: Record<string, { label: string; cls: string }> = {
  ACCEPTED: { label: 'Đã nhận tham gia', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' },
  DECLINED: { label: 'Đã từ chối', cls: 'bg-rose-50 text-rose-700 border-rose-200' },
  ASSIGNED: { label: 'Được gán', cls: 'bg-blue-50 text-blue-700 border-blue-200' },
};

const fmtDateTime = (value?: string | null) => {
  if (!value) return '—';
  const local = toVietnamDateTimeLocalInput(value); // giờ Việt Nam cố định
  if (!local) return value;
  return `${local.slice(11, 16)} ${local.slice(8, 10)}/${local.slice(5, 7)}/${local.slice(0, 4)}`;
};

/** Shown in the contact block so a field that is not filled in yet reads as "not yet", not as absent. */
const EMPTY_FIELD = 'Chưa có thông tin';

function InfoRow({ icon, label, value }: { icon?: React.ReactNode; label: string; value?: React.ReactNode }) {
  if (value === null || value === undefined || value === '' || value === '—') return null;
  return (
    <div className="flex items-start gap-2 py-1">
      <span className="w-40 shrink-0 text-[11px] font-bold text-slate-400 uppercase tracking-wide flex items-center gap-1.5 pt-0.5">
        {icon}
        {label}
      </span>
      <span className="text-sm text-slate-700 font-medium min-w-0 break-words">{value}</span>
    </div>
  );
}

export function StaffVisitDetailModal({
  isOpen, visitInstanceId, refreshKey = 0, onClose, onSelfHost, onAssignHost, onReject, selfHostSubmitting = false,
  changeNotifs = [], onChangeNotifClick,
}: StaffVisitDetailModalProps) {
  // Sau khi bấm "Chấp nhận" (canApprove), hiện 2 lựa chọn con thay vì mở modal ngay:
  // "Tôi là người phụ trách" hoặc "Gán người phụ trách". Reset mỗi khi đổi item xem.
  const [showApproveChoices, setShowApproveChoices] = useState(false);
  const navigate = useNavigate();
  const [detail, setDetail] = useState<StaffCalendarDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const canSeeRegistrantInfo = (() => {
    try {
      const userStr = typeof window !== 'undefined' ? localStorage.getItem('currentUser') : null;
      if (!userStr) return false;
      const user = JSON.parse(userStr);
      const roleCode = (user?.roleCode || user?.role || '').toUpperCase();
      return roleCode === 'STAFF' || roleCode === 'HO' || roleCode === 'ADMIN';
    } catch {
      return false;
    }
  })();

  useEffect(() => {
    if (!isOpen || !visitInstanceId) {
      setDetail(null);
      return;
    }
    setShowApproveChoices(false);
    let cancelled = false;
    setLoading(true);
    setError(null);
    staffCalendarApi
      .getDetail(visitInstanceId)
      .then((d) => { if (!cancelled) setDetail(d); })
      .catch((e: any) => {
        if (!cancelled) {
          setError(e?.response?.data?.message || e?.response?.data?.title || 'Không thể tải chi tiết yêu cầu tham quan. Vui lòng thử lại.');
        }
      })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [isOpen, visitInstanceId, refreshKey]);

  if (!isOpen) return null;

  const actions = detail?.allowedActions;
  // Campus-independent approval: duyệt luôn kèm gán host (một action duy nhất).
  const showApproveAssign = !!actions?.canApprove;
  const showReject = !!actions?.canReject;
  const showSetup = !!actions?.canSetupDelegation;
  const hasFooterActions = !!detail && (showApproveAssign || showReject || showSetup);

  const goToSetup = () => {
    if (!detail) return;
    onClose();
    navigate(`/dashboard/visit/process/${detail.visitInstanceId}`);
  };

  return (
    <div className="fixed inset-0 z-[100] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 12 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="bg-white rounded-3xl shadow-2xl w-full max-w-2xl overflow-hidden flex flex-col max-h-[90vh]"
      >
        {/* Header */}
        <div className="px-6 py-4 bg-[#004c91] flex items-start justify-between gap-3 flex-shrink-0">
          <div className="min-w-0">
            <div className="flex items-center gap-2 flex-wrap">
              <h3 className="text-lg font-bold text-white truncate">
                {detail?.delegationName || 'Yêu cầu tham quan'}
              </h3>
              {detail && (
                <span className={`text-[11px] font-bold px-2.5 py-1 rounded-full ${COLOR_BADGE[detail.colorType] || 'bg-slate-100 text-slate-600'}`}>
                  {detail.displayStatus}
                </span>
              )}
            </div>
            <div className="mt-1 flex items-center gap-4 text-xs text-white/85 font-medium flex-wrap">
              {detail?.requestCode && <span>{detail.requestCode}</span>}
              {detail && (
                <span className="flex items-center gap-1">
                  <Clock className="w-3.5 h-3.5" />
                  {fmtDateTime(detail.plannedStartAt)} → {fmtDateTime(detail.plannedEndAt)}
                </span>
              )}
              {detail && (
                <span className="flex items-center gap-1">
                  <MapPin className="w-3.5 h-3.5" /> {detail.campusName}
                </span>
              )}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="text-white/85 hover:text-white transition-colors hover:bg-white/10 rounded-full p-1.5 cursor-pointer shrink-0"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto p-6">
          {loading ? (
            <div className="py-14 text-center text-slate-500">
              <Loader2 className="w-6 h-6 mx-auto mb-2 animate-spin text-[#004c91]" />
              <p className="text-sm">Đang tải chi tiết yêu cầu tham quan...</p>
            </div>
          ) : error ? (
            <div className="py-14 text-center">
              <AlertCircle className="w-8 h-8 mx-auto mb-2 text-red-400" />
              <p className="text-sm font-medium text-red-600">{error}</p>
            </div>
          ) : detail ? (
            <div className="space-y-5">
              {/* Thay đổi mới (thông báo chưa đọc gắn với đơn tham quan này) */}
              {changeNotifs.length > 0 && (
                <div className="bg-red-50/70 border border-red-200 rounded-2xl overflow-hidden">
                  <div className="px-4 py-2.5 flex items-center gap-2.5 border-b border-red-100 bg-red-50">
                    <span className="relative flex h-2.5 w-2.5 shrink-0">
                      <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
                      <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-red-500" />
                    </span>
                    <span className="text-sm font-bold text-red-700">Thay đổi mới ({changeNotifs.length})</span>
                    <span className="text-[11px] text-red-400 ml-auto hidden sm:block">Bấm vào từng thay đổi để mở đúng chỗ cần xem</span>
                  </div>
                  <div className="divide-y divide-red-100">
                    {changeNotifs.map((n) => (
                      <button
                        key={n.notificationId}
                        type="button"
                        onClick={() => onChangeNotifClick?.(n)}
                        className="w-full text-left px-4 py-2.5 hover:bg-red-100/50 transition-colors cursor-pointer"
                      >
                        <div className="flex items-start justify-between gap-3">
                          <span className="text-sm font-semibold text-slate-800">{n.title}</span>
                          <span className="text-[10px] text-slate-400 whitespace-nowrap shrink-0 mt-0.5">{timeAgo(n.createdAt)}</span>
                        </div>
                        {n.message && <p className="text-xs text-slate-500 mt-0.5 line-clamp-2">{n.message}</p>}
                      </button>
                    ))}
                  </div>
                </div>
              )}

              {/* Lý do hủy / từ chối (nếu có) */}
              {detail.isCancelled && (
                <div className="rounded-xl border border-slate-300 bg-slate-50 px-4 py-3 text-sm text-slate-700">
                  <p className="font-bold flex items-center gap-1.5 text-slate-600">
                    <Ban className="w-4 h-4" /> Yêu cầu đến thăm đã bị hủy
                    {detail.cancelledAt ? ` (${fmtDateTime(detail.cancelledAt)})` : ''}
                  </p>
                  {detail.cancellationReason && <p className="mt-1">Lý do: {detail.cancellationReason}</p>}
                </div>
              )}
              {detail.requestStatus === 'REJECTED' && (
                <div className="rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-700">
                  <p className="font-bold flex items-center gap-1.5">
                    <AlertCircle className="w-4 h-4" /> Yêu cầu đã bị từ chối
                    {detail.decidedByName ? ` bởi ${detail.decidedByName}` : ''}
                    {detail.decidedAt ? ` (${fmtDateTime(detail.decidedAt)})` : ''}
                  </p>
                  {detail.decisionNote && <p className="mt-1">Lý do: {detail.decisionNote}</p>}
                </div>
              )}

              {/* Người đăng ký / tổ chức */}
              {canSeeRegistrantInfo && (
                <section>
                  <h4 className="text-xs font-extrabold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                    <User className="w-3.5 h-3.5 text-[#f37021]" /> Người đăng ký / Tổ chức
                  </h4>
                  <div className="divide-y divide-slate-100 border-y border-slate-100">
                    <InfoRow label="Họ tên" value={detail.registrantFullName} />
                    <InfoRow label="Tổ chức" value={detail.registrantOrganization} />
                    <InfoRow label="Chức danh" value={detail.registrantJobTitle} />
                    <InfoRow label="Quốc tịch" value={detail.registrantNationality} />
                    <InfoRow icon={<Phone className="w-3 h-3" />} label="Điện thoại" value={detail.registrantPhone} />
                    <InfoRow icon={<Mail className="w-3 h-3" />} label="Email" value={detail.registrantEmail} />
                  </div>
                </section>
              )}

              {/* Đầu mối đoàn khách phối hợp tại cơ sở — this campus's own, always shown. A name
                  that is still blank means the invited person has not confirmed yet, which is
                  information; dropping the section makes it look like nobody was ever nominated. */}
              <section>
                <h4 className="text-xs font-extrabold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                  <Phone className="w-3.5 h-3.5 text-[#f37021]" /> Đầu mối đoàn khách phối hợp tại cơ sở
                </h4>
                <div className="divide-y divide-slate-100 border-y border-slate-100">
                  <InfoRow label="Họ tên" value={detail.operationalContactFullName || EMPTY_FIELD} />
                  <InfoRow label="Đơn vị công tác" value={detail.operationalContactOrganization || EMPTY_FIELD} />
                  <InfoRow label="Chức vụ" value={detail.operationalContactJobTitle || EMPTY_FIELD} />
                  <InfoRow icon={<Phone className="w-3 h-3" />} label="Điện thoại" value={detail.operationalContactPhone || EMPTY_FIELD} />
                  <InfoRow icon={<Mail className="w-3 h-3" />} label="Email" value={detail.operationalContactEmail || EMPTY_FIELD} />
                </div>
              </section>

              {/* Nội dung chuyến thăm */}
              <section>
                <h4 className="text-xs font-extrabold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                  <FileText className="w-3.5 h-3.5 text-[#f37021]" /> Nội dung chuyến thăm
                </h4>
                <div className="divide-y divide-slate-100 border-y border-slate-100">
                  <InfoRow label="Mục đích" value={detail.purpose} />
                  <InfoRow label="Nội dung làm việc" value={detail.workingContent} />
                  <InfoRow
                    label="Hình thức"
                    value={detail.visitType
                      ? (VISIT_TYPE_LABELS[detail.visitType] || detail.visitType) + (detail.visitTypeOther ? ` — ${detail.visitTypeOther}` : '')
                      : null}
                  />
                  <InfoRow icon={<Users className="w-3 h-3" />} label="Số lượng khách" value={detail.guestCount > 0 ? `${detail.guestCount} người` : null} />
                  <InfoRow icon={<Globe className="w-3 h-3" />} label="Ngôn ngữ làm việc" value={detail.workingLanguage === 'VI' ? 'Tiếng Việt' : detail.workingLanguage === 'EN' ? 'Tiếng Anh' : detail.workingLanguage} />
                  <InfoRow
                    icon={<Camera className="w-3 h-3" />}
                    label="Ghi hình / media"
                    value={detail.mediaConsentStatus
                      ? (MEDIA_CONSENT_LABELS[detail.mediaConsentStatus] || detail.mediaConsentStatus) + (detail.mediaConsentNote ? ` — ${detail.mediaConsentNote}` : '')
                      : null}
                  />
                  <InfoRow
                    icon={<Car className="w-3 h-3" />}
                    label="Nhận diện phương tiện"
                    value={detail.transportationNote}
                  />
                </div>
              </section>

              {/* Host hiện tại */}
              <section>
                <h4 className="text-xs font-extrabold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                  <UserCheck className="w-3.5 h-3.5 text-[#f37021]" /> Host phụ trách
                </h4>
                {detail.currentHostName ? (
                  <div className="divide-y divide-slate-100 border-y border-slate-100">
                    <InfoRow
                      label="Host hiện tại"
                      value={
                        <span>
                          {detail.currentHostName}
                          {detail.isCurrentHost && (
                            <span className="ml-2 text-[10px] font-bold text-[#004c91] bg-blue-100 rounded px-1.5 py-0.5">Bạn</span>
                          )}
                        </span>
                      }
                    />
                    <InfoRow icon={<Mail className="w-3 h-3" />} label="Email host" value={detail.currentHostEmail} />
                    <InfoRow label="Được gán lúc" value={detail.hostAssignedAt ? fmtDateTime(detail.hostAssignedAt) : null} />
                    <InfoRow label="Người gán" value={detail.hostAssignedByName} />
                  </div>
                ) : (
                  <p className="text-sm text-slate-500 italic border-y border-slate-100 py-2">
                    Chưa có host phụ trách cho yêu cầu đến thăm này.
                  </p>
                )}
              </section>

              {/* Phản hồi thành phần tham gia (nếu có dữ liệu) */}
              {detail.participantResponses.length > 0 && (
                <section>
                  <h4 className="text-xs font-extrabold text-slate-500 uppercase tracking-wider mb-2 flex items-center gap-1.5">
                    <Users className="w-3.5 h-3.5 text-[#f37021]" /> Phản hồi tham gia
                  </h4>
                  <div className="space-y-1.5">
                    {detail.participantResponses.map((p) => {
                      const st = PARTICIPANT_STATUS_LABELS[p.status] || { label: p.status, cls: 'bg-slate-50 text-slate-600 border-slate-200' };
                      return (
                        <div key={p.participantId} className="flex items-start justify-between gap-2 rounded-xl border border-slate-100 px-3 py-2">
                          <div className="min-w-0">
                            <p className="text-sm font-semibold text-slate-700 truncate">
                              {p.fullName}
                              <span className="ml-2 text-[10px] font-bold text-slate-400 uppercase">{PARTICIPANT_ROLE_LABELS[p.participantRole] || p.participantRole}</span>
                            </p>
                            {p.status === 'DECLINED' && p.note && (
                              <p className="text-xs text-rose-600 mt-0.5">Lý do từ chối: {p.note}</p>
                            )}
                            {p.respondedAt && (
                              <p className="text-[11px] text-slate-400 mt-0.5">Phản hồi lúc {fmtDateTime(p.respondedAt)}</p>
                            )}
                          </div>
                          <span className={`text-[10px] font-bold px-2 py-1 rounded-lg border shrink-0 ${st.cls}`}>{st.label}</span>
                        </div>
                      );
                    })}
                  </div>
                </section>
              )}
            </div>
          ) : null}
        </div>

        {/* Footer actions — chỉ render theo allowedActions backend trả */}
        {hasFooterActions && (
          <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100 flex-shrink-0 flex-wrap">
            {showReject && !showApproveChoices && (
              <button
                type="button"
                onClick={() => detail && onReject?.(detail)}
                className="px-5 py-2 rounded-xl font-bold text-red-600 border border-red-200 bg-white hover:bg-red-50 transition-colors outline-none text-sm cursor-pointer flex items-center gap-1.5"
              >
                <X className="w-4 h-4" /> Từ chối
              </button>
            )}
            {showApproveAssign && !showApproveChoices && (
              <button
                type="button"
                onClick={() => setShowApproveChoices(true)}
                className="px-6 py-2 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] shadow-sm transition-all outline-none text-sm cursor-pointer flex items-center gap-1.5"
              >
                <Check className="w-4 h-4" />
                Chấp nhận
              </button>
            )}
            {showApproveAssign && showApproveChoices && (
              <>
                <button
                  type="button"
                  onClick={() => setShowApproveChoices(false)}
                  disabled={selfHostSubmitting}
                  className="px-4 py-2 rounded-xl font-bold text-slate-500 hover:bg-slate-100 transition-colors outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  Quay lại
                </button>
                <button
                  type="button"
                  onClick={() => detail && onSelfHost?.(detail)}
                  disabled={selfHostSubmitting}
                  className="px-5 py-2 rounded-xl font-bold text-white bg-emerald-600 hover:bg-emerald-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5"
                >
                  {selfHostSubmitting ? <Loader2 className="w-4 h-4 animate-spin" /> : <UserCheck className="w-4 h-4" />}
                  Tôi là người phụ trách
                </button>
                <button
                  type="button"
                  onClick={() => detail && onAssignHost?.(detail)}
                  disabled={selfHostSubmitting}
                  className="px-6 py-2 rounded-xl font-bold text-white bg-[#004c91] hover:bg-[#003b70] shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-1.5"
                >
                  <UserPlus className="w-4 h-4" />
                  Gán người phụ trách
                </button>
              </>
            )}
            {showSetup && (
              <button
                type="button"
                onClick={goToSetup}
                className="px-6 py-2 rounded-xl font-bold text-white bg-[#f37021] hover:opacity-90 shadow-sm transition-all outline-none text-sm cursor-pointer flex items-center gap-1.5"
              >
                <Settings className="w-4 h-4" /> Setup đoàn khách
              </button>
            )}
          </div>
        )}
      </motion.div>
    </div>
  );
}
