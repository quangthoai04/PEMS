/**
 * Component SharedDashboardView
 * Màn hình chung quy định bảng nhiệm vụ và tiến độ tiếp đón khách dành cho Roles.
 */

import React, { useState, useMemo } from 'react';
import {
  Calendar as CalendarIcon,
  Calendar,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Trash2,
  MoreVertical,
  X,
  MapPin,
  Clock,
  Users,
  User,
  Bookmark,
  CheckSquare,
  Plus,
  Eye,
  AlertCircle,
  TrendingUp,
  FileText,
  Bell,
  Sparkles,
  Info,
  ChevronDown,
  Edit2
} from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import toast from 'react-hot-toast';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import { notificationsApi } from '../../../features/notifications/api/notificationsApi';
import { useNotifications } from '../../../features/notifications/context/NotificationsContext';
import { getNotificationLink, timeAgo } from '../../../features/notifications/components/NotificationBellButton';
import { NotificationDetailModal } from '../../../features/notifications/components/NotificationDetailModal';
import type { NotificationItem } from '../../../features/notifications/types/notification.types';
import { matchCalendarChangeNotifs } from '../../../features/notifications/utils/calendarChangeNotifs';
import { TaskHandoverModal } from './TaskHandoverModal';
import { useAuth } from '../../../shared/hooks/useAuth';
import { EmailPreviewModal, type EmailPreviewSendPayload } from '../../../features/delegations/components/EmailPreviewModal';
import { stripLegacyActionHtml } from '../../../features/emails/utils/actionLinks';
import { formatVietnamDateTime, toVietnamCalendarDate, toVietnamDateTimeLocalInput } from '../../../shared/utils/vietnamTime';

interface Event {
  id: string;
  title: string;
  date: string; // YYYY-MM-DD
  time: string;
  category: 'Lời mời tham gia' | 'Đơn yêu cầu mượn đồ' | 'Lịch của tôi';
  color: string; // css color classes
  hoverColor: string;
  location: string;
  host: string;
  guests: string;
  checklist: string[];
  purpose?: string;       // Mục đích đón tiếp
  vipLevel?: string;      // Phân cấp VIP
  contactPerson?: string; // Điều phối viên phụ trách
  hotelInfo?: string;     // Khách sạn lưu trú
  bannerText?: string;    // Băng rôn LED
  carBooking?: string;    // Xe đưa đón
}

type AssignmentProgressItem = {
  itemType: 'INVITATION' | 'REQUEST';
  itemId: number;
  visitRequestId: number;
  visitInstanceId: number;
  logisticsItemId?: number;
  participantId?: number;
  delegationName: string;
  requestCode: string;
  organizationName?: string;
  title: string;
  description?: string;
  currentResponsibleUserId?: number;
  currentResponsibleName?: string;
  currentResponsibleRole?: string;
  isLeaderSelfAccepted?: boolean;
  rawStatus: string;
  uiStatus: string;
  statusLabel: string;
  startAt: string;
  endAt: string;
  canViewDelegationDetail: boolean;
  canAssign: boolean;
  canAccept: boolean;
  canDecline: boolean;
  canRejectRequest: boolean;
  canProposeChange: boolean;
  canSignBorrow: boolean;
  canSignReturn: boolean;
  latestDeclineReason?: string;
  latestDeclinedByName?: string;
  latestDeclinedAt?: string;
  /** True khi chính người đang đăng nhập là người thực hiện hành động gần nhất. */
  isActedByCurrentUser?: boolean;
  needsAttention: boolean;
  attentionReason?: string;
  cancelReason?: string;
};

function parseTimeToMinutes(timeStr?: string | null): { start: number; end: number } | null {
  if (!timeStr) return null;
  const parts = timeStr.split('-').map(s => s.trim());
  if (parts.length === 2 && parts[0].includes(':') && parts[1].includes(':')) {
    const [h1, m1] = parts[0].split(':').map(Number);
    const [h2, m2] = parts[1].split(':').map(Number);
    if (!isNaN(h1) && !isNaN(m1) && !isNaN(h2) && !isNaN(m2)) {
      return { start: h1 * 60 + m1, end: h2 * 60 + m2 };
    }
  }
  return null;
}

function checkTimeOverlap(
  date1?: string | null,
  time1?: string | null,
  date2?: string | null,
  time2?: string | null
): boolean {
  if (!date1 || !date2 || !time1 || !time2 || date1 !== date2) return false;
  const r1 = parseTimeToMinutes(time1);
  const r2 = parseTimeToMinutes(time2);
  if (!r1 || !r2) return false;
  return r1.start < r2.end && r1.end > r2.start;
}

/** Parse key "YYYY-MM-DD" theo PHẦN lịch — new Date('YYYY-MM-DD') là UTC midnight và lùi 1 ngày ở browser múi giờ âm. */
function parseDateKey(s: string): Date {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, (m || 1) - 1, d || 1);
}

const INITIAL_EVENTS: Event[] = [
  {
    id: 'e-invitation-8',
    title: 'Thư mời tham gia sự kiện',
    date: '2026-08-08',
    time: '14:00 - 16:30',
    category: 'Lời mời tham gia',
    color: 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100',
    hoverColor: 'border-emerald-500',
    location: 'Hội trường sảnh tòa nhà Alpha',
    host: 'Nguyễn Văn A',
    guests: 'Đoàn đối tác Nhật Bản',
    checklist: [],
    purpose: 'Trân trọng kính mời anh/chị tham gia tiếp đón và giao lưu cùng đoàn đối tác từ Nhật Bản.\n\nVui lòng chuẩn bị tài liệu liên quan để trao đổi hợp tác.',
    vipLevel: 'Standard',
    contactPerson: 'Nguyễn Văn A'
  },
  {
    id: 'safuri-car-event',
    title: 'Yêu cầu mượn xe điện cho đoàn khách Safuri',
    date: '2026-08-08',
    time: '08:00 - 17:30',
    category: 'Đơn yêu cầu mượn đồ',
    color: 'bg-orange-50 text-orange-700 border-orange-300 hover:bg-orange-100',
    hoverColor: 'border-orange-500',
    location: 'Campus Hòa Lạc, Đại học FPT',
    host: 'Phòng Hậu cần & Đội xe điện',
    guests: 'Đoàn khách Safuri',
    checklist: ['Kiểm tra xe bảo dưỡng', 'Cử tài xế túc trực', 'Hoàn tất biên bản bàn giao'],
    purpose: 'Mượn xe điện phục vụ di chuyển đoàn khách Safuri tham quan doanh nghiệp và campus.',
    vipLevel: 'VIP',
    contactPerson: 'Trần Văn Tuyến (Điều hành xe - 0914.555.666)'
  }];

/** Dòng key-value gọn cho khối thông tin — thay cho các "khung" bento to trước đây. */
function InfoLine({ icon: Icon, label, value, emphasize }: { icon: React.ElementType; label: string; value: React.ReactNode; emphasize?: boolean }) {
  if (value == null || value === '') return null;
  return (
    <div className="flex items-start gap-2 py-1">
      <Icon className="w-3.5 h-3.5 text-gray-400 mt-0.5 shrink-0" />
      <div className="min-w-0 flex-1">
        <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400 block leading-none mb-0.5">{label}</span>
        <span className={emphasize ? 'text-sm font-black text-[#004c91]' : 'text-sm font-semibold text-gray-800'}>{value}</span>
      </div>
    </div>
  );
}

export function SharedDashboardView({ user, isDeptLeader, isDeptStaff, isStudent, isVisitor, initialVisitInstanceId, viewMode = 'calendar' }: { user?: any, isDeptLeader?: boolean, isDeptStaff?: boolean, isStudent?: boolean, isVisitor?: boolean, initialVisitInstanceId?: number | null, viewMode?: 'calendar' | 'assignments' }) {
  // "Hôm nay" theo lịch Việt Nam — không lệch ngày ở browser nước ngoài.
  const today = toVietnamCalendarDate(new Date())!;
  const todayStr = `${today.getUTCFullYear()}-${String(today.getUTCMonth() + 1).padStart(2, '0')}-${String(today.getUTCDate()).padStart(2, '0')}`;

  const [events, setEvents] = useState<any[]>([]);
  const [candidates, setCandidates] = useState<any[]>([]);
  const navigate = useNavigate();
  const { user: authUser } = useAuth();
  const { markAsRead: markNotificationRead } = useNotifications();
  // Thông báo chưa đọc liên quan tới các đơn/thư mời — dùng cho chấm đỏ nháy trên lịch
  // và danh sách "Thay đổi mới" trong modal chi tiết.
  const [changeNotifs, setChangeNotifs] = useState<NotificationItem[]>([]);

  const fetchChangeNotifs = React.useCallback(async () => {
    try {
      const res = await notificationsApi.getNotifications({ page: 1, pageSize: 50, isRead: false });
      setChangeNotifs(res?.items || []);
    } catch (e) { console.error(e); }
  }, []);

  /** Các thông báo chưa đọc gắn với đúng sự kiện lịch này (đơn yêu cầu / thư mời). */
  const getEventChangeNotifs = React.useCallback(
    (ev: any): NotificationItem[] => matchCalendarChangeNotifs(changeNotifs, ev),
    [changeNotifs],
  );

  // Thay đổi không có link đích (notification cũ) → mở modal chi tiết như bell.
  const [changeNotifDetail, setChangeNotifDetail] = useState<NotificationItem | null>(null);

  /** Bấm 1 thay đổi trong modal: đánh dấu đã đọc rồi trỏ tới đúng chỗ như thông báo. */
  const handleChangeNotifClick = async (n: NotificationItem) => {
    try { await markNotificationRead(n.notificationId); } catch { /* ignore */ }
    setChangeNotifs(prev => prev.filter(x => x.notificationId !== n.notificationId));
    const link = getNotificationLink(n, authUser);
    if (link) {
      setActivePopoverEvent(null);
      navigate(link);
    } else {
      setChangeNotifDetail(n);
    }
  };
  const [searchParams, setSearchParams] = useSearchParams();
  const [assignmentItems, setAssignmentItems] = useState<AssignmentProgressItem[]>([]);
  const [attentionItems, setAttentionItems] = useState<AssignmentProgressItem[]>([]);
  const [assignmentTotal, setAssignmentTotal] = useState(0);
  const [assignmentLoading, setAssignmentLoading] = useState(false);
  const [assignmentSearch, setAssignmentSearch] = useState('');
  const [assignmentItemType, setAssignmentItemType] = useState('ALL');
  const [assignmentStatus, setAssignmentStatus] = useState('ALL');
  const [assignmentOwnerScope, setAssignmentOwnerScope] = useState('DEPARTMENT');
  const [assignmentFromDate, setAssignmentFromDate] = useState('');
  const [assignmentToDate, setAssignmentToDate] = useState('');
  const [assignmentSortDirection, setAssignmentSortDirection] = useState<'ASC' | 'DESC'>('ASC');
  const [assignmentPage, setAssignmentPage] = useState(1);
  const [assignmentPageSize, setAssignmentPageSize] = useState(10);
  const [focusVisitRequestId, setFocusVisitRequestId] = useState<number | null>(null);
  const [assigningTaskItem, setAssigningTaskItem] = useState<AssignmentProgressItem | null>(null);

  const [activePopoverEvent, setActivePopoverEvent] = useState<any>(null);
  const [selectedCategoryFilter, setSelectedCategoryFilter] = useState<string>('All');

  // Thư mời interaction states
  const [invitationStatus, setInvitationStatus] = useState<'pending' | 'rejecting' | 'rejected' | 'accepted' | 'assigned'>('pending');
  const [rejectReason, setRejectReason] = useState('');
  const [rejectSignature, setRejectSignature] = useState<{ name: string, time: string } | null>(null);
  const [acceptSignature, setAcceptSignature] = useState<{ name: string, time: string } | null>(null);
  const [showAssignDropdown, setShowAssignDropdown] = useState(false);
  const [assignedPerson, setAssignedPerson] = useState<string | null>(null);

  // Editable "Xem trước email" before assigning a logistics task to a staff member.
  const [assignPreview, setAssignPreview] = useState({
    open: false, loading: false, sending: false, error: null as string | null,
    subject: '', body: '', isActionTemplate: false,
    systemActionDescription: null as string | null, lockedActionBlockHtml: null as string | null,
  });
  const [pendingAssign, setPendingAssign] = useState<{
    itemType: 'REQUEST' | 'INVITATION';
    logisticsItemId?: number | string;
    participantId?: number | string;
    staffId: number | string;
    staffName: string;
    title?: string;
    delegationName?: string;
  } | null>(null);

  const openLogisticsAssignPreview = async (p: { logisticsItemId: number | string; staffId: number | string; staffName: string; title?: string; delegationName?: string }) => {
    setPendingAssign({ ...p, itemType: 'REQUEST' });
    setAssignPreview((s) => ({ ...s, open: true, loading: true, error: null }));
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode: 'LOGISTICS_ASSIGNEE_ASSIGNMENT',
        context: {
          assigneeName: p.staffName,
          DelegationName: p.delegationName ?? 'đoàn khách',
          logisticsTitle: p.title ?? 'hạng mục hậu cần',
        },
      });
      setAssignPreview((s) => ({
        ...s, open: true, loading: false, error: null,
        subject: res.subject, body: stripLegacyActionHtml(res.bodyHtml),
        isActionTemplate: res.isActionTemplate,
        systemActionDescription: res.systemActionDescription ?? null,
        lockedActionBlockHtml: res.lockedActionBlockHtml ?? null,
      }));
    } catch (e: any) {
      setAssignPreview((s) => ({ ...s, open: true, loading: false, error: e?.response?.data?.message || e?.message || 'Không thể tải bản xem trước email.' }));
    }
  };

  const openInvitationAssignPreview = async (p: { participantId: number | string; staffId: number | string; staffName: string; title?: string; delegationName?: string }) => {
    setPendingAssign({ ...p, itemType: 'INVITATION' });
    setAssignPreview((s) => ({ ...s, open: true, loading: true, error: null }));
    try {
      const res = await delegationsApi.previewEmailTemplate({
        templateCode: 'VISIT_PARTICIPANT_INVITATION',
        context: {
          recipientName: p.staffName,
          assigneeName: p.staffName,
          DelegationName: p.delegationName ?? p.title ?? 'đoàn khách',
          eventTitle: p.title ?? p.delegationName ?? 'lịch tiếp khách',
          coordinationNote: 'Bạn được Trưởng phòng ủy quyền tham gia đón tiếp.',
        },
      });
      setAssignPreview((s) => ({
        ...s, open: true, loading: false, error: null,
        subject: res.subject, body: stripLegacyActionHtml(res.bodyHtml),
        isActionTemplate: res.isActionTemplate,
        systemActionDescription: res.systemActionDescription ?? null,
        lockedActionBlockHtml: res.lockedActionBlockHtml ?? null,
      }));
    } catch (e: any) {
      setAssignPreview((s) => ({ ...s, open: true, loading: false, error: e?.response?.data?.message || e?.message || 'Không thể tải bản xem trước email.' }));
    }
  };

  const reloadAssignPreview = async () => {
    if (!pendingAssign) return;
    if (pendingAssign.itemType === 'INVITATION') {
      await openInvitationAssignPreview({
        participantId: pendingAssign.participantId!,
        staffId: pendingAssign.staffId,
        staffName: pendingAssign.staffName,
        title: pendingAssign.title,
        delegationName: pendingAssign.delegationName,
      });
      return;
    }
    await openLogisticsAssignPreview({
      logisticsItemId: pendingAssign.logisticsItemId!,
      staffId: pendingAssign.staffId,
      staffName: pendingAssign.staffName,
      title: pendingAssign.title,
      delegationName: pendingAssign.delegationName,
    });
  };
  const closeAssignPreview = () => setAssignPreview((s) => ({ ...s, open: false }));

  const confirmLogisticsAssign = async (payload: EmailPreviewSendPayload) => {
    if (!pendingAssign) return;
    if (!payload.subject.trim()) { toast.error('Tiêu đề email không được để trống.'); return; }
    if (!payload.bodyHtml.trim()) { toast.error('Nội dung email không được để trống.'); return; }
    setAssignPreview((s) => ({ ...s, sending: true }));
    try {
      const emailOverride = { useEditedContent: true, subject: payload.subject.trim(), bodyHtml: payload.bodyHtml, attachments: payload.attachments };
      if (pendingAssign.itemType === 'INVITATION') {
        await delegationsApi.visitInvitations.assignDepartmentStaff(
          pendingAssign.participantId!,
          Number(pendingAssign.staffId),
          '',
          emailOverride,
        );
      } else {
        await departmentReceptionTasksApi.assignAssignee(
          pendingAssign.logisticsItemId!, pendingAssign.staffId,
          emailOverride,
        );
      }
      toast.success('Đã phân công người phụ trách và gửi email.');
      setAssignPreview((s) => ({ ...s, open: false, sending: false }));
      setAssignedPerson(pendingAssign.staffName);
      if (pendingAssign.itemType === 'INVITATION') setInvitationStatus('assigned');
      setRequestStatus('assigned');
      setShowAssignDropdown(false);
      setAssigningTaskItem(null);
      setPendingAssign(null);
      try { await refetchAfterTaskAction(); } catch { /* ignore */ }
      try { await fetchCalendarEvents(); } catch { /* ignore */ }
    } catch (e: any) {
      setAssignPreview((s) => ({ ...s, sending: false }));
      toast.error(e?.response?.data?.message || e?.response?.data?.title || e?.message || 'Phân công thất bại');
    }
  };

  // Đơn yêu cầu interaction states
  const [requestStatus, setRequestStatus] = useState<'pending' | 'rejecting' | 'rejected' | 'accepted' | 'assigned' | 'awaiting-reassign'>('pending');
  const [requestAcceptSignature, setRequestAcceptSignature] = useState<{ name: string, time: string } | null>(null);
  const [requestRejectReason, setRequestRejectReason] = useState('');
  const [requestRejectSignature, setRequestRejectSignature] = useState<{ name: string, time: string } | null>(null);
  const [isProposing, setIsProposing] = useState(false);
  const [proposalContent, setProposalContent] = useState('');
  const [proposalNote, setProposalNote] = useState('');
  const [proposalStartTime, setProposalStartTime] = useState('');
  const [proposalEndTime, setProposalEndTime] = useState('');
  const [proposalQuantity, setProposalQuantity] = useState('');
  const [proposalSubmitted, setProposalSubmitted] = useState(false);
  const [proposalSubmitting, setProposalSubmitting] = useState(false);

  // Dept preliminary states
  const [deptPreliminaryStatus, setDeptPreliminaryStatus] = useState<'pending' | 'rejecting' | 'rejected' | 'accepted'>('pending');
  const [deptRejectReason, setDeptRejectReason] = useState('');

  React.useEffect(() => {
    setInvitationStatus('pending');
    setRejectReason('');
    setAcceptSignature(null);
    setShowAssignDropdown(false);
    setAssignedPerson(null);
    setRequestStatus('pending');
    setRequestAcceptSignature(null);
    setRequestRejectReason('');
    setRequestRejectSignature(null);
    setIsProposing(false);
    setProposalNote('');
    setProposalStartTime('');
    setProposalEndTime('');
    setProposalSubmitted(false);
    setDeptPreliminaryStatus('pending');
    setDeptRejectReason('');
  }, [activePopoverEvent?.id]);

  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({
    creator: false,
    guests: false,
    setup: false,
    details: false
  });

  const toggleSection = (section: string) => {
    setExpandedSections(prev => ({
      ...prev,
      [section]: !prev[section]
    }));
  };

  // Collapse all accordion sections whenever a new event is opened
  React.useEffect(() => {
    setExpandedSections({
      creator: false,
      guests: false,
      setup: false,
      details: false
    });
  }, [activePopoverEvent?.id]);

  const [activeEventDetail, setActiveEventDetail] = useState<any>(null);

  React.useEffect(() => {
    if (!activePopoverEvent || !activePopoverEvent.rawId) {
      setActiveEventDetail(null);
      return;
    }
    const fetchDetail = async () => {
      try {
        if (activePopoverEvent.itemType === 'INVITATION') {
          const detail = await departmentReceptionTasksApi.getInvitationDetail(activePopoverEvent.rawId);
          setActiveEventDetail(detail);

          if (detail.status === 'CANCELLED') {
            setInvitationStatus('rejected');
            setRejectReason(`Đơn yêu cầu / thư mời đã bị hủy do đoàn khách hủy.${detail.cancelReason ? ` Lý do: ${detail.cancelReason}` : ''}`);
          } else if (detail.status === 'ACCEPTED' || detail.status === 'IN_PROGRESS' || detail.status === 'DONE') {
            setInvitationStatus('accepted');
            setAcceptSignature({ name: detail.assigneeName || detail.responderName || detail.senderName, time: detail.actionTime });
          } else if (detail.status === 'REJECTED' || detail.status === 'DECLINED') {
            setInvitationStatus('rejected');
            setRejectReason(detail.rejectReason || '');
            setRejectSignature({ name: detail.assigneeName || detail.responderName || detail.senderName, time: detail.actionTime });
          } else if (detail.status === 'ASSIGNED' || detail.assigneeName) {
            setAssignedPerson(detail.assigneeName);
            setInvitationStatus('assigned');
          } else {
            setAssignedPerson(null);
            setInvitationStatus('pending');
          }
        } else if (activePopoverEvent.itemType === 'REQUEST') {
          const detail = await departmentReceptionTasksApi.getRequestDetail(activePopoverEvent.rawId);
          setActiveEventDetail(detail);

          if (detail.status === 'CANCELLED') {
            setRequestStatus('rejected');
            setRequestRejectReason(`Đơn yêu cầu / thư mời đã bị hủy do đoàn khách hủy.${detail.cancelReason ? ` Lý do: ${detail.cancelReason}` : ''}`);
          } else if (detail.status === 'ASSIGNED') {
            setAssignedPerson(detail.assigneeName);
            setRequestStatus('assigned');
            setRequestAcceptSignature({ name: detail.assigneeName || detail.responderName || detail.senderName, time: detail.actionTime });
          } else if (detail.status === 'ACCEPTED' || detail.status === 'IN_PROGRESS' || detail.status === 'DONE') {
            setAssignedPerson(detail.assigneeName);
            setRequestStatus('accepted');
            setRequestAcceptSignature({ name: detail.assigneeName || detail.responderName || detail.senderName, time: detail.actionTime });
          } else if (detail.status === 'REJECTED' || detail.status === 'DECLINED') {
            setRequestStatus('rejected');
            setRequestRejectReason(detail.rejectReason || '');
            setRequestRejectSignature({ name: detail.assigneeName || detail.responderName || detail.senderName, time: detail.actionTime });
            setAssignedPerson(null);
          } else if (detail.status === 'REQUESTED') {
            setAssignedPerson(null);
            setRequestStatus('pending');
          } else if (detail.status === 'CHANGE_PROPOSED') {
            setAssignedPerson(detail.assigneeName);
            setRequestStatus('pending');
            setProposalSubmitted(true);
          } else if (detail.status === 'REJECTED') {
            setRequestStatus('rejected');
            setRequestRejectReason(detail.rejectReason || '');
            setRequestRejectSignature({ name: detail.assigneeName || detail.responderName || detail.senderName, time: detail.actionTime });
          } else {
            setAssignedPerson(null);
            setRequestStatus('pending');
          }
        }
      } catch (err) { console.error(err); }
    };
    fetchDetail();
  }, [activePopoverEvent?.rawId, activePopoverEvent?.itemType]);

  const [currentYear, setCurrentYear] = useState(toVietnamCalendarDate(new Date())!.getUTCFullYear());
  const [currentMonth, setCurrentMonth] = useState(toVietnamCalendarDate(new Date())!.getUTCMonth());

  const fetchCalendarEvents = React.useCallback(async () => {
    try {
      const res = await departmentReceptionTasksApi.getCalendar(`${currentYear}`);
      const list = res?.data || res || [];
      if (Array.isArray(list)) {
        const mapped = list
          .filter((item: any) => {
            // Dept Leader: đơn bị từ chối không hiện trên bảng lịch nữa.
            if (!isDeptLeader) return true;
            const st = item.itemStatus || item.status;
            return st !== 'REJECTED' && st !== 'DECLINED';
          })
          .map((item: any, idx: number) => {
            let cat = '';
            let col = '';
            let hCol = '';
            const itemStatus = item.itemStatus || item.status;
            const relatedId = item.relatedUserId != null ? String(item.relatedUserId) : (item.assignedToUserId != null ? String(item.assignedToUserId) : null);
            const currentUserIdStr = user?.id ?? user?.userId ?? user?.user_id ?? user?.account;
            const isMine = relatedId != null && currentUserIdStr != null && String(currentUserIdStr) === String(relatedId);
            const isProcessed = itemStatus !== 'REQUESTED' && itemStatus !== 'ASSIGNED' && itemStatus !== 'SUBMITTED' && itemStatus !== 'PENDING';

            if (itemStatus === 'CANCELLED' || itemStatus === 'REJECTED') {
              cat = 'Hủy';
              col = 'bg-slate-100 text-slate-500 border-slate-200 hover:bg-slate-200';
              hCol = 'border-slate-400';
            } else if (item.itemType === 'PERSONAL') {
              if (calendarType === 'Lịch của tôi') {
                cat = 'Lịch cá nhân';
                col = 'bg-purple-100 text-purple-800 border-purple-400 hover:bg-purple-200';
                hCol = 'border-purple-600';
              } else {
                cat = 'Lịch của tôi';
                col = 'bg-blue-50 text-[#004c91] border-blue-300 hover:bg-blue-100';
                hCol = 'border-blue-500';
              }
            } else if (isMine && (itemStatus === 'ACCEPTED' || itemStatus === 'IN_PROGRESS' || itemStatus === 'DONE')) {
              cat = calendarType === 'Lịch của tôi' ? 'Đơn phụ trách' : 'Lịch của tôi';
              col = 'bg-blue-50 text-[#004c91] border-blue-300 hover:bg-blue-100';
              hCol = 'border-blue-500';
            } else if (!isDeptLeader && isMine && (itemStatus === 'ASSIGNED' || itemStatus === 'INVITED' || itemStatus === 'REQUESTED')) {
              // Dept Staff: "Cần xử lý" (Vàng) cho các đơn được leader giao nhưng chưa phản hồi
              cat = 'Cần xử lý';
              col = 'bg-amber-50 text-amber-800 border-amber-300 hover:bg-amber-100';
              hCol = 'border-amber-500';
            } else if (isDeptLeader && (itemStatus === 'SUBMITTED' || itemStatus === 'PENDING' || itemStatus === 'UNASSIGNED' || itemStatus === 'INVITED' || itemStatus === 'REQUESTED' || itemStatus === 'DECLINED')) {
              // Dept Leader: "Cần xử lý" (Vàng) cho các đơn chưa có người nhận/cần giao
              cat = 'Cần xử lý';
              col = 'bg-amber-50 text-amber-800 border-amber-300 hover:bg-amber-100';
              hCol = 'border-amber-500';
            } else {
              // Còn lại: "Đã có người phụ trách" (Xanh lá)
              cat = 'Đã có người phụ trách';
              col = 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100';
              hCol = 'border-emerald-500';
            }

            // Re-based: UTC getters trả đúng phần giờ Việt Nam.
            const sd = toVietnamCalendarDate(item.startAt) ?? new Date(NaN);
            const ed = toVietnamCalendarDate(item.endAt) ?? new Date(NaN);
            const dateStr = `${sd.getUTCFullYear()}-${String(sd.getUTCMonth() + 1).padStart(2, '0')}-${String(sd.getUTCDate()).padStart(2, '0')}`;
            const timeStr = `${String(sd.getUTCHours()).padStart(2, '0')}:${String(sd.getUTCMinutes()).padStart(2, '0')} - ${String(ed.getUTCHours()).padStart(2, '0')}:${String(ed.getUTCMinutes()).padStart(2, '0')}`;

            return {
              id: item.itemId + '_' + idx,
              rawId: item.itemId || item.id,
              visitRequestId: item.visitRequestId,
              visitInstanceId: item.visitInstanceId,
              itemType: item.itemType,
              status: itemStatus,
              latestAttemptStatus: item.latestAttemptStatus,
              cancelReason: item.cancelReason,
              isProcessed: isProcessed,
              title: itemStatus === 'CANCELLED' ? `${item.title} (đã hủy)` : item.title,
              fullTitle: itemStatus === 'CANCELLED' ? `${item.fullTitle || item.title} - Đã hủy${item.cancelReason ? `: ${item.cancelReason}` : ''}` : item.fullTitle,
              delegationName: item.delegationName,
              date: dateStr,
              time: timeStr,
              category: cat,
              color: col,
              hoverColor: hCol,
              location: item.campusName || 'Hòa Lạc',
              host: item.senderName || 'Hệ thống',
              guests: item.delegationName || item.title,
              purpose: itemStatus === 'CANCELLED' ? `Đơn yêu cầu / thư mời đã bị hủy do đoàn khách hủy.${item.cancelReason ? ` Lý do: ${item.cancelReason}` : ''}` : item.title || '',
              vipLevel: 'Standard',
              contactPerson: item.relatedUserName || 'N/A',
              relatedUserId: item.relatedUserId,
              checklist: []
            };
          });
        setEvents(mapped);
      }
    } catch (e) { console.error(e); }
  }, [currentYear, isDeptLeader, user?.id, user?.userId, user?.account, user?.user_id]);

  const fetchCandidates = React.useCallback(async () => {
    try {
      if (isDeptLeader || isDeptStaff) {
        const res = await departmentReceptionTasksApi.getAssigneeCandidates();
        if (res) setCandidates(res);
      }
    } catch (e) { console.error(e); }
  }, [isDeptLeader, isDeptStaff]);

  const currentUserId = user?.id || user?.userId || user?.user_id;
  const currentUserEmail = (user?.email || '').toLowerCase();
  const filteredCandidates = React.useMemo(() => {
    if (!candidates || !Array.isArray(candidates)) return [];
    if (!isDeptLeader) return candidates;
    return candidates.filter(staff => {
      const sId = staff.id || staff.userId || staff.user_id;
      const sRole = staff.role || staff.roleCode;
      const sEmail = (staff.email || '').toLowerCase();
      const sName = (staff.name || staff.fullName || '').toLowerCase();

      if (currentUserId && String(sId) === String(currentUserId)) return false;
      if (currentUserEmail && sEmail && sEmail === currentUserEmail) return false;
      if (sRole === 'DEPARTMENT' || sRole === 'DEPT_LEADER' || sRole === 'DEPT_HEAD') return false;
      if (sName.includes('dept leader') || sName.includes('department lead') || sName.includes('trưởng phòng')) return false;
      return true;
    });
  }, [candidates, isDeptLeader, currentUserId, currentUserEmail]);

  // ── Time Conflict Validations ──────────────────────────────────────────────
  const leaderSelfConflict = React.useMemo(() => {
    if (!isDeptLeader || !activePopoverEvent) return null;
    const leaderId = [user?.id, user?.userId, user?.account, user?.user_id].find(v => v != null);

    return events.find(ev => {
      if (ev.id === activePopoverEvent.id || (ev.rawId === activePopoverEvent.rawId && ev.itemType === activePopoverEvent.itemType)) {
        return false;
      }
      const st = ev.status;
      if (st === 'CANCELLED' || st === 'DECLINED' || st === 'REJECTED') return false;

      const isMine = ev.relatedUserId != null && leaderId != null && String(ev.relatedUserId) === String(leaderId);
      const isHandled = st === 'ACCEPTED' || st === 'CHANGE_PROPOSED' || ev.itemType === 'PERSONAL';

      if ((isHandled && isMine) || (ev.itemType === 'PERSONAL' && isMine)) {
        return checkTimeOverlap(activePopoverEvent.date, activePopoverEvent.time, ev.date, ev.time);
      }
      return false;
    });
  }, [isDeptLeader, activePopoverEvent, events, user]);

  const getCandidateConflict = React.useCallback((staffUserId: number | string) => {
    let targetDate: string | null = null;
    let targetTime: string | null = null;

    if (activePopoverEvent) {
      targetDate = activePopoverEvent.date;
      targetTime = activePopoverEvent.time;
    } else if (assigningTaskItem) {
      const startIso = assigningTaskItem.startAt;
      const endIso = assigningTaskItem.endAt;
      if (startIso && endIso) {
        const sd = toVietnamCalendarDate(startIso) ?? new Date(NaN);
        const ed = toVietnamCalendarDate(endIso) ?? new Date(NaN);
        if (!isNaN(sd.getTime()) && !isNaN(ed.getTime())) {
          targetDate = `${sd.getUTCFullYear()}-${String(sd.getUTCMonth() + 1).padStart(2, '0')}-${String(sd.getUTCDate()).padStart(2, '0')}`;
          targetTime = `${String(sd.getUTCHours()).padStart(2, '0')}:${String(sd.getUTCMinutes()).padStart(2, '0')} - ${String(ed.getUTCHours()).padStart(2, '0')}:${String(ed.getUTCMinutes()).padStart(2, '0')}`;
        }
      }
    }

    if (!targetDate || !targetTime) return null;

    const sIdStr = String(staffUserId);

    return events.find(ev => {
      if (activePopoverEvent && (ev.id === activePopoverEvent.id || (ev.rawId === activePopoverEvent.rawId && ev.itemType === activePopoverEvent.itemType))) {
        return false;
      }
      const st = ev.status;
      if (st === 'CANCELLED' || st === 'DECLINED' || st === 'REJECTED') return false;

      const isStaffAssigned = ev.relatedUserId != null && String(ev.relatedUserId) === sIdStr;
      const isStaffActiveTask = isStaffAssigned && (st === 'ACCEPTED' || st === 'ASSIGNED' || st === 'IN_PROGRESS' || st === 'CHANGE_PROPOSED' || ev.itemType === 'PERSONAL');

      if (isStaffActiveTask || (ev.itemType === 'PERSONAL' && isStaffAssigned)) {
        return checkTimeOverlap(targetDate, targetTime, ev.date, ev.time);
      }
      return false;
    });
  }, [activePopoverEvent, assigningTaskItem, events]);

  const fetchAssignmentsProgress = React.useCallback(async () => {
    if (!(isDeptLeader || isDeptStaff)) return;
    setAssignmentLoading(true);
    try {
      const params: Record<string, any> = {
        search: assignmentSearch || undefined,
        itemType: assignmentItemType,
        status: assignmentStatus,
        ownerScope: assignmentOwnerScope,
        fromDate: assignmentFromDate || undefined,
        toDate: assignmentToDate || undefined,
        sortDirection: assignmentSortDirection,
        page: assignmentPage,
        pageSize: assignmentPageSize,
        visitRequestId: focusVisitRequestId || undefined
      };
      const res = await departmentReceptionTasksApi.getAssignmentsProgress(params);
      setAssignmentItems(res?.items || []);
      setAssignmentTotal(res?.totalItems || 0);
      const attention = await departmentReceptionTasksApi.getAttentionItems();
      setAttentionItems(Array.isArray(attention) ? attention : []);
    } catch (e) {
      console.error(e);
      toast.error('Không tải được danh sách phân công và tiến độ');
    } finally {
      setAssignmentLoading(false);
    }
  }, [
    isDeptLeader,
    isDeptStaff,
    assignmentSearch,
    assignmentItemType,
    assignmentStatus,
    assignmentOwnerScope,
    assignmentFromDate,
    assignmentToDate,
    assignmentSortDirection,
    assignmentPage,
    assignmentPageSize,
    focusVisitRequestId
  ]);

  React.useEffect(() => {
    setAssignmentPage(1);
  }, [
    assignmentSearch,
    assignmentItemType,
    assignmentStatus,
    assignmentOwnerScope,
    assignmentFromDate,
    assignmentToDate,
    assignmentSortDirection,
    assignmentPageSize,
    focusVisitRequestId
  ]);

  React.useEffect(() => {
    const statusFromUrl = searchParams.get('status');
    if (viewMode === 'assignments' && statusFromUrl) {
      setAssignmentStatus(statusFromUrl);
    }
  }, [searchParams, viewMode]);

  React.useEffect(() => {
    const visitRequestIdFromUrl = searchParams.get('visitRequestId');
    if (viewMode === 'assignments' && visitRequestIdFromUrl) {
      setFocusVisitRequestId(Number(visitRequestIdFromUrl));
    }
  }, [searchParams, viewMode]);

  // Notification "Có yêu cầu hậu cần mới" / "Lời mời tham gia" trỏ thẳng vào đây qua
  // ?taskId=&itemType= (thay cho trang "Chi tiết nhiệm vụ điều phối" đứng riêng đã bỏ) —
  // mở đúng popover như khi bấm 1 đơn trong Bảng lịch. Ưu tiên lấy từ `events` đã tải sẵn
  // cho đủ trường hiển thị (host/guests/time/date); thiếu thì mở khung tối thiểu, phần chi
  // tiết còn lại tự nạp qua activeEventDetail (effect fetchDetail theo rawId/itemType).
  React.useEffect(() => {
    const taskIdFromUrl = searchParams.get('taskId');
    const itemTypeFromUrl = searchParams.get('itemType');
    if (!taskIdFromUrl || (itemTypeFromUrl !== 'REQUEST' && itemTypeFromUrl !== 'INVITATION')) return;
    if (events.length === 0) return;

    const matched = events.find(e => String(e.rawId) === String(taskIdFromUrl) && e.itemType === itemTypeFromUrl);
    setActivePopoverEvent(matched || {
      id: `${itemTypeFromUrl.toLowerCase()}_${taskIdFromUrl}`,
      rawId: taskIdFromUrl,
      itemType: itemTypeFromUrl,
      category: itemTypeFromUrl === 'INVITATION' ? 'Lời mời tham gia' : 'Đơn yêu cầu mượn đồ',
    });

    const next = new URLSearchParams(searchParams);
    next.delete('taskId');
    next.delete('itemType');
    setSearchParams(next, { replace: true });
  }, [events, searchParams, setSearchParams]);

  const clearFocusFilter = () => {
    setFocusVisitRequestId(null);
    const next = new URLSearchParams(searchParams);
    next.delete('visitRequestId');
    setSearchParams(next, { replace: true });
  };

  React.useEffect(() => {
    fetchCalendarEvents();
    fetchCandidates();
    fetchChangeNotifs();
  }, [fetchCalendarEvents, fetchCandidates, fetchChangeNotifs, user?.departmentId]);

  React.useEffect(() => {
    if (viewMode === 'assignments') {
      fetchAssignmentsProgress();
    }
  }, [viewMode, fetchAssignmentsProgress]);

  const [showAddFormModal, setShowAddFormModal] = useState(false);
  const [selectedCellDate, setSelectedCellDate] = useState<string | null>(todayStr);

  // States for Vietnamese Miniature Date Picker & Views
  const [showMiniCalendar, setShowMiniCalendar] = useState(false);
  const [miniMonth, setMiniMonth] = useState(toVietnamCalendarDate(new Date())!.getUTCMonth());
  const [miniYear, setMiniYear] = useState(toVietnamCalendarDate(new Date())!.getUTCFullYear());
  const [showDisplayDropdown, setShowDisplayDropdown] = useState(false);
  const [displayMode, setDisplayMode] = useState<'Ngày' | 'Tuần' | 'Tháng' | 'Năm'>('Tháng');

  // New states for Calendar Type ("Trong văn phòng", "Lịch của tôi")
  // Dept Leader mặc định xem lịch văn phòng; Dept Staff/Student/Visitor mặc định xem lịch của tôi.
  const [calendarType, setCalendarType] = useState<'Trong văn phòng' | 'Lịch của tôi'>(isDeptLeader ? 'Trong văn phòng' : 'Lịch của tôi');
  const [showTypeDropdown, setShowTypeDropdown] = useState(false);

  // Filter events based on type
  const filteredEvents = useMemo(() => {
    const currentUserIdStr = user?.id ?? user?.userId ?? user?.user_id ?? user?.account;
    const baseList = calendarType === 'Lịch của tôi'
      ? events.filter(e => {
          if (isStudent || isVisitor) return true;
          if (e.itemType === 'PERSONAL') return true;
          const relatedId = e.relatedUserId != null ? String(e.relatedUserId) : null;
          return relatedId != null && currentUserIdStr != null && String(currentUserIdStr) === String(relatedId);
        })
      : events;

    return baseList.map(e => {
      if (e.itemType === 'PERSONAL') {
        if (calendarType === 'Lịch của tôi') {
          return {
            ...e,
            category: 'Lịch cá nhân',
            color: 'bg-purple-100 text-purple-800 border-purple-400 hover:bg-purple-200',
            hoverColor: 'border-purple-600',
          };
        } else {
          return {
            ...e,
            category: 'Lịch của tôi',
            color: 'bg-blue-50 text-[#004c91] border-blue-300 hover:bg-blue-100',
            hoverColor: 'border-blue-500',
          };
        }
      }
      return e;
    });
  }, [events, calendarType, isStudent, isVisitor, user]);

  const eventsInCurrentMonthAndYear = useMemo(() => {
    return filteredEvents.filter(e => {
      if (e.itemType === 'PERSONAL') return false;
      const parts = e.date.split('-');
      if (parts.length < 3) return false;
      const year = parseInt(parts[0], 10);
      const month = parseInt(parts[1], 10);
      return month === (currentMonth + 1) && year === currentYear && !e.isProcessed;
    });
  }, [filteredEvents, currentMonth, currentYear]);
  // New Event Form State
  const [newTitle, setNewTitle] = useState('');
  const [newStartTime, setNewStartTime] = useState('09:00');
  const [newEndTime, setNewEndTime] = useState('11:00');
  const [newLocation, setNewLocation] = useState('');
  const [newContent, setNewContent] = useState('');
  const [showDetailSection, setShowDetailSection] = useState(false);
  const monthNames = [
    'Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
    'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'
  ];

  const handlePrev = () => {
    setActivePopoverEvent(null);
    if (displayMode === 'Ngày' || displayMode === 'Tuần') {
      if (selectedCellDate) {
        const d = parseDateKey(selectedCellDate);
        if (displayMode === 'Ngày') {
          d.setDate(d.getDate() - 1);
        } else {
          d.setDate(d.getDate() - 7);
        }
        const y = d.getFullYear();
        const m = d.getMonth();
        const dateStr = `${y}-${String(m + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        setSelectedCellDate(dateStr);
        setCurrentYear(y);
        setCurrentMonth(m);
      }
    } else if (displayMode === 'Năm') {
      setCurrentYear(y => y - 1);
    } else {
      if (currentMonth === 0) {
        setCurrentMonth(11);
        setCurrentYear(y => y - 1);
      } else {
        setCurrentMonth(m => m - 1);
      }
    }
  };

  const handleNext = () => {
    setActivePopoverEvent(null);
    if (displayMode === 'Ngày' || displayMode === 'Tuần') {
      if (selectedCellDate) {
        const d = parseDateKey(selectedCellDate);
        if (displayMode === 'Ngày') {
          d.setDate(d.getDate() + 1);
        } else {
          d.setDate(d.getDate() + 7);
        }
        const y = d.getFullYear();
        const m = d.getMonth();
        const dateStr = `${y}-${String(m + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
        setSelectedCellDate(dateStr);
        setCurrentYear(y);
        setCurrentMonth(m);
      }
    } else if (displayMode === 'Năm') {
      setCurrentYear(y => y + 1);
    } else {
      if (currentMonth === 11) {
        setCurrentMonth(0);
        setCurrentYear(y => y + 1);
      } else {
        setCurrentMonth(m => m + 1);
      }
    }
  };

  const handleResetToAugust2026 = () => {
    setCurrentMonth(toVietnamCalendarDate(new Date())!.getUTCMonth());
    setCurrentYear(toVietnamCalendarDate(new Date())!.getUTCFullYear());
    setActivePopoverEvent(null);
  };

  const daysGrid = useMemo(() => {
    const firstDayIndexRaw = new Date(currentYear, currentMonth, 1).getDay();
    const firstDayIndex = firstDayIndexRaw === 0 ? 6 : firstDayIndexRaw - 1;

    const totalDays = new Date(currentYear, currentMonth + 1, 0).getDate();
    const prevMonthTotalDays = new Date(currentYear, currentMonth, 0).getDate();

    const days = [];

    for (let i = 0; i < firstDayIndex; i++) {
      const d = prevMonthTotalDays - firstDayIndex + 1 + i;
      const m = currentMonth === 0 ? 11 : currentMonth - 1;
      const y = currentMonth === 0 ? currentYear - 1 : currentYear;
      const mStr = String(m + 1).padStart(2, '0');
      const dStr = String(d).padStart(2, '0');
      days.push({
        day: d,
        month: m,
        year: y,
        isCurrentMonth: false,
        isCurrent: false,
        dateString: `${y}-${mStr}-${dStr}`
      });
    }

    for (let i = 1; i <= totalDays; i++) {
      const mStr = String(currentMonth + 1).padStart(2, '0');
      const dStr = String(i).padStart(2, '0');
      days.push({
        day: i,
        month: currentMonth,
        year: currentYear,
        isCurrentMonth: true,
        isCurrent: true,
        dateString: `${currentYear}-${mStr}-${dStr}`
      });
    }

    const remaining = 35 - days.length;
    for (let i = 1; i <= remaining; i++) {
      const m = currentMonth === 11 ? 0 : currentMonth + 1;
      const y = currentMonth === 11 ? currentYear + 1 : currentYear;
      const mStr = String(m + 1).padStart(2, '0');
      const dStr = String(i).padStart(2, '0');
      days.push({
        day: i,
        month: m,
        year: y,
        isCurrentMonth: false,
        isCurrent: false,
        dateString: `${y}-${mStr}-${dStr}`
      });
    }

    return days;
  }, [currentYear, currentMonth]);

  // Partitions daysGrid into weeks lists (chunks of 7)
  const weeks = useMemo(() => {
    const list = [];
    for (let i = 0; i < daysGrid.length; i += 7) {
      list.push(daysGrid.slice(i, i + 7));
    }
    return list;
  }, [daysGrid]);

  // Find the sub-array of 7 days containing selectedCellDate
  const currentWeekDays = useMemo(() => {
    if (!selectedCellDate) return [];
    const d = parseDateKey(selectedCellDate);
    const day = d.getDay();
    const diff = d.getDate() - day + (day === 0 ? -6 : 1); // adjust when day is sunday

    const startOfWeek = new Date(d.setDate(diff));
    const week = [];

    for (let i = 0; i < 7; i++) {
      const wDate = new Date(startOfWeek);
      wDate.setDate(wDate.getDate() + i);
      const y = wDate.getFullYear();
      const m = wDate.getMonth();
      const dt = wDate.getDate();
      week.push({
        day: dt,
        month: m,
        year: y,
        isCurrentMonth: m === currentMonth,
        isCurrent: true,
        dateString: `${y}-${String(m + 1).padStart(2, '0')}-${String(dt).padStart(2, '0')}`
      });
    }
    return week;
  }, [selectedCellDate, currentMonth]);

  // Year view: helper to generate days for any specific month index of the current year (Monday-first)
  const getDaysForMonth = (year: number, monthIndex: number) => {
    // 0=Sun, 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat
    const firstDayRaw = new Date(year, monthIndex, 1).getDay();
    const firstDayIndex = firstDayRaw === 0 ? 6 : firstDayRaw - 1; // Mon-first representation
    const totalDays = new Date(year, monthIndex + 1, 0).getDate();
    const prevMonthTotal = new Date(year, monthIndex, 0).getDate();
    const cells = [];

    // Prior Month padding
    for (let i = firstDayIndex - 1; i >= 0; i--) {
      const d = prevMonthTotal - i;
      const m = monthIndex === 0 ? 11 : monthIndex - 1;
      cells.push({ day: d, isCurrent: false, month: m });
    }
    // Current month days
    for (let i = 1; i <= totalDays; i++) {
      cells.push({ day: i, isCurrent: true, month: monthIndex });
    }
    // Next month padding alignment
    const remaining = 35 - cells.length;
    for (let i = 1; i <= remaining; i++) {
      const m = monthIndex === 11 ? 0 : monthIndex + 1;
      cells.push({ day: i, isCurrent: false, month: m });
    }
    return cells;
  };

  const miniDaysGrid = useMemo(() => {
    const firstDayIndexRaw = new Date(miniYear, miniMonth, 1).getDay();
    const firstDayIndex = firstDayIndexRaw === 0 ? 6 : firstDayIndexRaw - 1;
    const totalDays = new Date(miniYear, miniMonth + 1, 0).getDate();
    const prevMonthTotalDays = new Date(miniYear, miniMonth, 0).getDate();

    const days = [];

    for (let i = 0; i < firstDayIndex; i++) {
      const m = miniMonth === 0 ? 11 : miniMonth - 1;
      const y = miniMonth === 0 ? miniYear - 1 : miniYear;
      days.push({
        day: prevMonthTotalDays - firstDayIndex + 1 + i,
        month: m,
        year: y,
        isCurrentMonth: false
      });
    }

    for (let i = 1; i <= totalDays; i++) {
      days.push({
        day: i,
        month: miniMonth,
        year: miniYear,
        isCurrentMonth: true
      });
    }

    const remaining = 35 - days.length;
    for (let i = 1; i <= remaining; i++) {
      const m = miniMonth === 11 ? 0 : miniMonth + 1;
      const y = miniMonth === 11 ? miniYear + 1 : miniYear;
      days.push({
        day: i,
        month: m,
        year: y,
        isCurrentMonth: false
      });
    }

    return days;
  }, [miniYear, miniMonth]);

  const handleOpenAddModal = (dateStr: string) => {
    if (dateStr < todayStr) {
      toast.error('Không thể tạo lịch trong quá khứ. Vui lòng chọn ngày từ hôm nay trở đi.');
      return;
    }
    setSelectedCellDate(dateStr);
    setNewTitle('');
    setNewContent('');
    setNewLocation('');
    setNewStartTime('09:00');
    setNewEndTime('11:00');
    setShowAddFormModal(true);
  };

  const handleAddEventSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim() || !selectedCellDate) return;

    if (selectedCellDate < todayStr) {
      toast.error('Không thể tạo lịch trong quá khứ. Vui lòng chọn ngày từ hôm nay trở đi.');
      return;
    }

    try {
      const st = newStartTime || '08:00';
      const et = newEndTime || '09:00';
      const newTime = `${st} - ${et}`;

      const conflictingEvent = events.find(ev => {
        const s = ev.status;
        if (s === 'CANCELLED' || s === 'DECLINED' || s === 'REJECTED') return false;
        return checkTimeOverlap(selectedCellDate, newTime, ev.date, ev.time);
      });

      if (conflictingEvent) {
        toast.error(`Thời gian tạo lịch cá nhân bị trùng với đơn/thư hoặc lịch khác trong ngày (${conflictingEvent.time}: ${conflictingEvent.title}). Vui lòng chọn khung giờ khác!`);
        return;
      }

      await departmentReceptionTasksApi.createPersonalEvent(
        newTitle,
        newContent,
        selectedCellDate,
        st,
        et
      );

      toast.success('Đã lưu lịch cá nhân vào hệ thống');

      await fetchCalendarEvents();
      setShowAddFormModal(false);
    } catch (err: any) {
      console.error(err);
      toast.error(err.response?.data?.message || err.response?.data?.title || err.message || 'Lỗi khi lưu lịch cá nhân');
    }
  };

  const handleDeleteEvent = (id: string) => {
    setEvents(p => p.filter(e => e.id !== id));
    if (activePopoverEvent?.id === id) {
      setActivePopoverEvent(null);
    }
  };

  const formatDateTime = (value?: string) => {
    if (!value) return '';
    return formatVietnamDateTime(value, { fallback: value });
  };

  // Hiển thị thời gian theo định dạng thống nhất "HH:mm dd/MM/yyyy" (ví dụ 08:30 15/10/2026).
  const formatDateTimeDisplay = (value?: string | null) => {
    if (!value) return 'Chưa có';
    const local = toVietnamDateTimeLocalInput(value); // "YYYY-MM-DDTHH:mm" giờ VN
    if (!local) return value;
    return `${local.slice(11, 16)} ${local.slice(8, 10)}/${local.slice(5, 7)}/${local.slice(0, 4)}`;
  };

  // Refetch chi tiết đơn sau khi ký biên bản trong TaskHandoverModal (dùng chung với Dept Staff) —
  // giữ đồng bộ activeEventDetail + trạng thái popover + lịch/tiến độ ngoài modal.
  const refreshActiveEventDetail = async () => {
    if (!activePopoverEvent?.rawId) return;
    const detail = await departmentReceptionTasksApi.getRequestDetail(activePopoverEvent.rawId);
    setActiveEventDetail(detail);
    if (detail.status === 'IN_PROGRESS' || detail.status === 'DONE') setRequestStatus('accepted');
    await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
  };

  const getStatusClass = (status: string) => {
    switch (status) {
      case 'REQUESTED': return 'bg-red-50 text-red-700 border-red-100';
      case 'ASSIGNED': return 'bg-blue-50 text-blue-700 border-blue-100';
      case 'ACCEPTED': return 'bg-emerald-50 text-emerald-700 border-emerald-100';
      case 'REJECTED': return 'bg-rose-50 text-rose-700 border-rose-100';
      case 'DECLINED': return 'bg-rose-50 text-rose-700 border-rose-100';
      case 'CHANGE_PROPOSED': return 'bg-amber-50 text-amber-700 border-amber-100';
      case 'IN_PROGRESS': return 'bg-cyan-50 text-cyan-700 border-cyan-100';
      case 'DONE': return 'bg-slate-100 text-slate-700 border-slate-200';
      case 'CANCELLED': return 'bg-gray-100 text-gray-500 border-gray-200';
      default: return 'bg-slate-50 text-slate-700 border-slate-100';
    }
  };

  const canShowChangeResponsible = (item: AssignmentProgressItem) => {
    const currentUserId = user?.id ?? user?.userId ?? user?.user_id ?? user?.account;
    const isSelfHandled = String(item.currentResponsibleUserId || '') === String(currentUserId || '')
      && (item.uiStatus === 'ACCEPTED' || item.uiStatus === 'REJECTED');
    if (item.isLeaderSelfAccepted) return false;
    if (isSelfHandled) return false;
    if (item.uiStatus === 'REJECTED') return false;
    if (item.uiStatus === 'DONE' || item.uiStatus === 'CANCELLED') return false;
    return !!isDeptLeader && item.uiStatus === 'REQUESTED';
  };

  const openAssignmentDetail = (item: AssignmentProgressItem) => {
    const event = events.find(e =>
      (item.itemType === 'INVITATION' && String(e.rawId) === String(item.participantId || item.itemId) && e.itemType === 'INVITATION') ||
      (item.itemType === 'REQUEST' && String(e.rawId) === String(item.logisticsItemId || item.itemId) && e.itemType === 'REQUEST')
    );

    if (event) {
      setActivePopoverEvent(event);
      setSelectedCellDate(event.date);
      setCurrentMonth(parseDateKey(event.date).getMonth());
      setCurrentYear(parseDateKey(event.date).getFullYear());
      setDisplayMode('Tháng');
      return;
    }

    if (item.itemType === 'INVITATION') {
      setActivePopoverEvent({
        id: `invitation_${item.itemId}`,
        rawId: item.participantId || item.itemId,
        itemType: 'INVITATION',
        category: 'Lời mời tham gia',
        title: item.title,
        delegationName: item.delegationName,
        guests: item.delegationName,
        purpose: item.description,
        date: item.startAt?.slice(0, 10),
        time: `${formatDateTime(item.startAt)} - ${formatDateTime(item.endAt)}`,
        location: item.organizationName || '',
        host: item.currentResponsibleName || 'Department Leader'
      });
    } else {
      setActivePopoverEvent({
        id: `request_${item.itemId}`,
        rawId: item.logisticsItemId || item.itemId,
        itemType: 'REQUEST',
        category: 'Đơn yêu cầu mượn đồ',
        title: item.title,
        delegationName: item.delegationName,
        guests: item.delegationName,
        purpose: item.description,
        date: item.startAt?.slice(0, 10),
        time: `${formatDateTime(item.startAt)} - ${formatDateTime(item.endAt)}`,
        location: item.organizationName || '',
        host: item.currentResponsibleName || 'Chưa phân công'
      });
    }
  };

  const refetchAfterTaskAction = async () => {
    await Promise.all([fetchAssignmentsProgress(), fetchCalendarEvents(), fetchChangeNotifs()]);
  };

  const handleAcceptSelf = async (item: AssignmentProgressItem) => {
    try {
      if (item.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.acceptInvitation(item.participantId || item.itemId);
        toast.success('Đã chấp nhận thư mời.');
      } else {
        await departmentReceptionTasksApi.acceptRequestSelf(item.logisticsItemId || item.itemId);
        toast.success('Đã tự nhận đơn yêu cầu.');
      }
      await refetchAfterTaskAction();
    } catch (e: any) {
      toast.error(e.response?.data?.message || e.response?.data?.title || e.message || 'Thao tác thất bại');
    }
  };

  const handleRejectOrDecline = async (item: AssignmentProgressItem) => {
    const reason = window.prompt(item.itemType === 'INVITATION' ? 'Nhập lý do từ chối thư mời:' : 'Nhập lý do từ chối đơn yêu cầu:');
    if (!reason?.trim()) return;
    try {
      if (item.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.declineInvitation(item.participantId || item.itemId, reason.trim());
        toast.success('Đã từ chối thư mời.');
      } else if (item.canDecline) {
        await departmentReceptionTasksApi.declineAssignment(item.logisticsItemId || item.itemId, reason.trim());
        toast.success('Đã từ chối nhiệm vụ.');
      } else {
        await departmentReceptionTasksApi.rejectRequest(item.logisticsItemId || item.itemId, reason.trim());
        toast.success('Đã từ chối đơn yêu cầu.');
      }
      await refetchAfterTaskAction();
    } catch (e: any) {
      toast.error(e.response?.data?.message || e.response?.data?.title || e.message || 'Thao tác thất bại');
    }
  };

  const handleAssignTask = async (item: AssignmentProgressItem) => {
    setAssigningTaskItem(item);
  };

  const normalizeEventDate = () => {
    const value = activePopoverEvent?.date || activeEventDetail?.date;
    if (!value) return '';
    if (/^\d{4}-\d{2}-\d{2}$/.test(value)) return value;
    const parts = String(value).split(/[-/]/);
    if (parts.length === 3 && parts[2]?.length === 4) {
      return `${parts[2]}-${parts[1].padStart(2, '0')}-${parts[0].padStart(2, '0')}`;
    }
    return '';
  };

  const extractTimeRange = () => {
    const start = activeEventDetail?.startTime || activePopoverEvent?.time?.split('-')[0]?.trim() || '';
    const end = activeEventDetail?.endTime || activePopoverEvent?.time?.split('-')[1]?.trim() || '';
    return {
      start: start.match(/\d{2}:\d{2}/)?.[0] || '',
      end: end.match(/\d{2}:\d{2}/)?.[0] || ''
    };
  };

  // Đoàn khách diễn ra nhiều ngày (so ngày của usageStartAt/usageEndAt theo giờ VN) → ô đề xuất
  // giờ cần cả ngày lẫn giờ (datetime-local); trong 1 ngày → chỉ cần giờ (time), như trước.
  const usageStartDatePart = toVietnamDateTimeLocalInput(activeEventDetail?.usageStartAt || undefined).slice(0, 10) || undefined;
  const usageEndDatePart = toVietnamDateTimeLocalInput(activeEventDetail?.usageEndAt || undefined).slice(0, 10) || undefined;
  const isMultiDay = !!(usageStartDatePart && usageEndDatePart && usageStartDatePart !== usageEndDatePart);

  // "Chốt": số lượng Host đã CHẤP NHẬN đề xuất — hiển thị số này ở mọi chỗ chỉ có 1 ô "Số lượng"
  // (không tách 3 cột dự kiến/đề xuất/chốt như card Host); số dự kiến gốc (activeEventDetail.quantity)
  // không đổi, chỉ dùng để tính toán (vd ép "đề xuất mới phải nhỏ hơn").
  const finalQuantityDisplay = activeEventDetail?.proposalResponse === 'ACCEPTED' && activeEventDetail?.proposedQuantity != null
    ? activeEventDetail.proposedQuantity : activeEventDetail?.quantity;
  const quantityTooHigh = proposalQuantity.trim() !== '' && activeEventDetail?.quantity != null && Number(proposalQuantity) >= activeEventDetail.quantity;

  const handleOpenProposal = () => {
    if (isMultiDay) {
      setProposalStartTime(toVietnamDateTimeLocalInput(activeEventDetail?.proposedUsageStartAt || activeEventDetail?.usageStartAt || undefined));
      setProposalEndTime(toVietnamDateTimeLocalInput(activeEventDetail?.proposedUsageEndAt || activeEventDetail?.usageEndAt || undefined));
    } else {
      const current = extractTimeRange();
      setProposalStartTime(current.start);
      setProposalEndTime(current.end);
    }
    // Không seed từ quantity gốc — số lượng đề xuất phải NHỎ HƠN số lượng dự kiến, để trống mặc
    // định, chỉ khôi phục đề xuất dở dang nếu có.
    setProposalQuantity(activeEventDetail?.proposedQuantity != null ? String(activeEventDetail.proposedQuantity) : '');
    setProposalContent(activeEventDetail?.proposedDescription || '');
    setIsProposing(true);
  };

  const buildProposalDateTime = (time: string) => {
    const date = normalizeEventDate();
    return date && time ? `${date}T${time}:00` : null;
  };

  const handleSelectAssignee = async (staff: any) => {
    if (!assigningTaskItem) return;
    const staffId = staff.id || staff.userId;
    const staffName = staff.name || staff.fullName || 'Nhân sự';
    if (assigningTaskItem.itemType !== 'INVITATION') {
      await openLogisticsAssignPreview({
        logisticsItemId: assigningTaskItem.logisticsItemId || assigningTaskItem.itemId,
        staffId,
        staffName,
        title: assigningTaskItem.title,
        delegationName: (assigningTaskItem as any).delegationName,
      });
      return;
    }
    await openInvitationAssignPreview({
      participantId: assigningTaskItem.participantId || assigningTaskItem.itemId,
      staffId,
      staffName,
      title: assigningTaskItem.title,
      delegationName: (assigningTaskItem as any).delegationName,
    });
  };

  // TEMP DEV TEST: Department contribution shortcut.
  // Shows an extra action (next to the unchanged eye icon) that opens the Contribution Page,
  // only for Department roles on rows that carry a visitInstanceId, and only in dev builds.
  // Does NOT touch the eye icon's onClick/route/detail flow, nor backend allowedActions.
  // Remove this shortcut when the OPEN_CONTRIBUTION allowedAction is implemented by backend.
  const isDepartmentRole = !!isDeptLeader || !!isDeptStaff;
  const canOpenContribution = (row: AssignmentProgressItem) =>
    import.meta.env.DEV && isDepartmentRole && !!row.visitInstanceId;

  const renderAssignmentsProgressPanel = () => (
    <div className="space-y-5">
      {focusVisitRequestId && (
        <div className="flex items-center justify-between gap-3 rounded-2xl border border-blue-200 bg-blue-50/80 px-4 py-3">
          <p className="text-sm font-bold text-[#004c91]">
            Đang lọc theo đơn/thư vừa chọn từ thông báo ({assignmentItems.length} mục).
          </p>
          <button
            type="button"
            onClick={clearFocusFilter}
            className="px-3 py-1.5 rounded-lg border border-blue-200 bg-white text-[#004c91] text-[11px] font-black hover:bg-blue-100 transition-colors"
          >
            Xem tất cả
          </button>
        </div>
      )}
      {attentionItems.length > 0 && (
        <div className="rounded-2xl border border-orange-200 bg-orange-50/80 p-4 shadow-3xs">
          <div className="flex items-center gap-2 text-[#f37021] font-black text-sm mb-3">
            <AlertCircle className="w-4 h-4" />
            <span>Đơn yêu cầu đang làm / Cần chú ý</span>
          </div>
          <div className="space-y-2">
            {attentionItems.slice(0, 5).map(item => (
              <div key={`${item.itemType}_${item.itemId}`} className="flex items-center justify-between gap-3 bg-white/80 border border-orange-100 rounded-xl px-3 py-2">
                <div className="min-w-0">
                  <p className="text-xs font-black text-slate-850 truncate">{item.delegationName} - {item.title}</p>
                  <p className="text-[11px] text-orange-700 font-semibold">{item.attentionReason || item.statusLabel}</p>
                </div>
                <button
                  type="button"
                  onClick={() => openAssignmentDetail(item)}
                  className="px-3 py-1.5 rounded-lg border border-orange-200 bg-white text-[#f37021] text-[11px] font-black hover:bg-orange-100 transition-colors"
                >
                  Xem chi tiết
                </button>
              </div>
            ))}
          </div>
        </div>
      )}
      <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden">
        <div className="bg-[#005594] px-4 py-3.5 flex flex-wrap lg:flex-nowrap items-center gap-3 w-full">
          <input
            value={assignmentSearch}
            onChange={e => setAssignmentSearch(e.target.value)}
            placeholder="Tìm kiếm nhiệm vụ..."
            className="flex-1 min-w-[200px] w-full lg:w-auto px-4 py-2 bg-white/10 border border-white/15 rounded-xl text-xs font-semibold text-white placeholder:text-white/70 outline-none focus:bg-white/20 focus:border-white/30 transition-all shadow-inner"
          />
          <div className="flex flex-wrap items-center gap-2.5 shrink-0 ml-auto">
            <select value={assignmentItemType} onChange={e => setAssignmentItemType(e.target.value)} className="px-3 py-2 bg-white rounded-xl text-xs font-bold text-slate-800 outline-none shadow-sm cursor-pointer hover:bg-slate-50">
              <option value="ALL">Tất cả loại</option>
              <option value="INVITATION">Thư mời</option>
              <option value="REQUEST">Đơn yêu cầu</option>
            </select>
            <select value={assignmentStatus} onChange={e => setAssignmentStatus(e.target.value)} className="px-3 py-2 bg-white rounded-xl text-xs font-bold text-slate-800 outline-none shadow-sm cursor-pointer hover:bg-slate-50">
              <option value="ALL">Tất cả trạng thái</option>
              <option value="REQUESTED">Chưa phân công</option>
              <option value="ASSIGNED">Đã giao</option>
              <option value="ACCEPTED">Chấp nhận</option>
              <option value="REJECTED">Từ chối</option>
              <option value="CHANGE_PROPOSED">Đang đề xuất</option>
              <option value="IN_PROGRESS">Trong tiến trình</option>
              <option value="DONE">Hoàn thành</option>
              <option value="CANCELLED">Đã hủy</option>
            </select>
            <select value={assignmentOwnerScope} onChange={e => setAssignmentOwnerScope(e.target.value)} className="px-3 py-2 bg-white rounded-xl text-xs font-bold text-slate-800 outline-none shadow-sm cursor-pointer hover:bg-slate-50">
              <option value="DEPARTMENT">Văn phòng</option>
              <option value="ME">Tôi</option>
            </select>
            <div className="flex items-center gap-1.5 bg-white px-3 py-1.5 rounded-xl shadow-sm text-xs border border-slate-200">
              <input type="date" value={assignmentFromDate} onChange={e => setAssignmentFromDate(e.target.value)} className="bg-transparent font-bold text-slate-800 outline-none cursor-pointer" />
              <span className="text-slate-400 font-bold">-</span>
              <input type="date" value={assignmentToDate} onChange={e => setAssignmentToDate(e.target.value)} className="bg-transparent font-bold text-slate-800 outline-none cursor-pointer" />
            </div>
            <button
              type="button"
              onClick={() => setAssignmentSortDirection(v => v === 'ASC' ? 'DESC' : 'ASC')}
              className="px-4 py-2 bg-white text-[#004c91] hover:bg-blue-50 rounded-xl text-xs font-black shadow-sm transition-colors shrink-0 cursor-pointer"
            >
              {assignmentSortDirection === 'DESC' ? 'Mới nhất' : 'Cũ nhất'}
            </button>
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[980px] text-left border-x border-b border-slate-100 rounded-b-2xl overflow-hidden">
            <thead className="bg-[#005594] text-white text-[11px] uppercase font-black">
              <tr>
                <th className="px-4 py-4 w-[60px] text-center">STT</th>
                <th className="px-7 py-4">Đoàn khách</th>
                <th className="px-5 py-4">Nhiệm vụ được giao</th>
                <th className="px-5 py-4">Thời gian</th>
                <th className="px-5 py-4">Người phụ trách</th>
                <th className="px-5 py-4 w-[170px]">Trạng thái</th>
                <th className="px-5 py-4 text-center">Hành động</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {assignmentItems.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-10 text-center text-sm font-semibold text-slate-400">Không có dữ liệu phù hợp</td>
                </tr>
              )}
              {assignmentItems.map((item, index) => (
                <tr key={`${item.itemType}_${item.itemId}`} className="hover:bg-slate-50/80 transition-colors">
                  <td className="px-4 py-5 text-center text-xs font-extrabold text-slate-500">{index + 1}</td>
                  <td className="px-7 py-5">
                    <p className="text-sm font-black text-slate-900 line-clamp-2">{item.delegationName}</p>
                    <p className="text-[11px] text-slate-450 font-semibold">{item.itemType === 'INVITATION' ? 'Thư mời' : 'Đơn yêu cầu'} {item.requestCode ? `• ${item.requestCode}` : ''}</p>
                  </td>
                  <td className="px-5 py-5 max-w-[280px]">
                    <p className="text-sm font-bold text-slate-800 line-clamp-2" title={item.title}>{item.title}</p>
                  </td>
                  <td className="px-5 py-5 text-xs text-slate-600 font-semibold whitespace-nowrap">
                    {(() => {
                      const sd = item.startAt ? toVietnamCalendarDate(item.startAt) : null;
                      const ed = item.endAt ? toVietnamCalendarDate(item.endAt) : null;
                      if (!sd) return '—';
                      const fmt = (d: Date) => `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')} ${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}/${d.getFullYear()}`;
                      return <><span className="block">{fmt(sd)}</span>{ed && <span className="block text-slate-400">→ {fmt(ed)}</span>}</>;
                    })()}
                  </td>
                  <td className="px-5 py-5 text-sm text-[#004c91] text-center">
                    {item.currentResponsibleName ? (
                      <div>
                        <p className="font-black">{item.currentResponsibleName}</p>
                        {item.currentResponsibleRole && <p className="text-[11px] font-semibold text-slate-500">{item.currentResponsibleRole}</p>}
                      </div>
                    ) : (item.uiStatus === 'DECLINED' || item.uiStatus === 'REJECTED') ? (
                      <div>
                        <p className="font-black text-rose-600">{item.latestDeclinedByName || (item.isActedByCurrentUser ? user?.name : 'Trưởng phòng')}</p>
                        <p className="text-[11px] font-semibold text-rose-500">{item.latestDeclinedByName ? 'Nhân viên' : 'Trưởng phòng'}</p>
                      </div>
                    ) : (
                      item.uiStatus === 'REQUESTED'
                        ? <span className="text-slate-400 font-semibold text-xs">Chưa phân công</span>
                        : <span className="text-rose-400 font-semibold text-xs">—</span>
                    )}
                    {canShowChangeResponsible(item) && (
                      <button
                        type="button"
                        onClick={() => handleAssignTask(item)}
                        className="mt-1 text-[11px] font-semibold text-blue-600 underline underline-offset-2 hover:text-[#004c91]"
                      >
                        Phân công người phụ trách
                      </button>
                    )}
                  </td>
                  <td className="px-5 py-5 w-[170px]">
                    <span className={`px-2.5 py-1 rounded-full border text-[11px] font-black ${getStatusClass(item.uiStatus)}`}>
                      {item.uiStatus === 'DECLINED' || item.uiStatus === 'REJECTED' ? 'Từ chối' : item.statusLabel}
                    </span>
                    {(item.uiStatus === 'DECLINED' || item.uiStatus === 'REJECTED') && item.latestDeclinedByName && (
                      <p className="text-[10px] text-rose-500 mt-1">
                        Từ chối bởi: {item.latestDeclinedByName}{item.latestDeclinedAt ? ` • ${item.latestDeclinedAt}` : ''}
                      </p>
                    )}
                    {(item.uiStatus === 'DECLINED' || item.uiStatus === 'REJECTED') && item.latestDeclineReason && (
                      <p className="text-[10px] text-rose-400 mt-0.5 line-clamp-2">Lý do: {item.latestDeclineReason}</p>
                    )}
                  </td>
                  <td className="px-5 py-5 text-center">
                    {item.uiStatus === 'CANCELLED' ? (
                      <span className="text-[11px] font-bold text-slate-500">
                        Đơn đã hủy vì đoàn khách đã hủy{item.cancelReason ? `: ${item.cancelReason}` : ''}
                      </span>
                    ) : (
                      <div className="flex items-center justify-center gap-2">
                        {/* TEMP DEV TEST: Department contribution shortcut.
                            Keep the eye icon unchanged.
                            Remove this shortcut when OPEN_CONTRIBUTION allowedAction is implemented by backend. */}
                        {canOpenContribution(item) && (
                          <button
                            type="button"
                            title="Đóng góp kết quả chuyến thăm"
                            aria-label="Đóng góp kết quả chuyến thăm"
                            onClick={(e) => {
                              e.stopPropagation();
                              navigate(`/dashboard/visit/contribution/${item.visitInstanceId}`);
                            }}
                            className="inline-flex items-center justify-center w-9 h-9 rounded-full text-[#f37021] hover:bg-orange-50 transition-colors"
                          >
                            <FileText className="w-5 h-5" />
                          </button>
                        )}

                        {/* Giữ nguyên icon mắt hiện tại */}
                        <button
                          type="button"
                          onClick={() => openAssignmentDetail(item)}
                          className="inline-flex items-center justify-center w-9 h-9 rounded-full text-slate-400 hover:text-[#004c91] hover:bg-blue-50 transition-colors"
                          title="Xem chi tiết"
                        >
                          <Eye className="w-5 h-5" />
                        </button>
                      </div>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex flex-wrap items-center justify-between gap-3 px-6 py-4 border-t border-slate-100 text-xs font-bold text-slate-500">
          <div className="flex items-center gap-3">
            <span className="text-sm text-slate-500 font-semibold">Hiển thị:</span>
            <select
              value={assignmentPageSize}
              onChange={e => {
                setAssignmentPageSize(Number(e.target.value));
                setAssignmentPage(1);
              }}
              className="px-2 py-1 bg-slate-50 border border-slate-200 rounded text-sm font-bold text-slate-700 outline-none hover:bg-slate-100"
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
              <option value={50}>50</option>
            </select>
            <span>{assignmentLoading ? 'Đang tải...' : `${assignmentTotal} mục`}</span>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => setAssignmentPage(page => Math.max(1, page - 1))}
              disabled={assignmentPage <= 1}
              className="px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-black text-[#004c91] hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Trước
            </button>
            <span className="px-3 py-2 rounded-xl bg-[#004c91] text-white">
              {assignmentPage} / {Math.max(1, Math.ceil(assignmentTotal / assignmentPageSize))}
            </span>
            <button
              type="button"
              onClick={() => setAssignmentPage(page => Math.min(Math.max(1, Math.ceil(assignmentTotal / assignmentPageSize)), page + 1))}
              disabled={assignmentPage >= Math.max(1, Math.ceil(assignmentTotal / assignmentPageSize))}
              className="px-3 py-2 bg-white border border-slate-200 rounded-xl text-xs font-black text-[#004c91] hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed"
            >
              Sau
            </button>
          </div>
        </div>
      </div>
    </div>
  );

  return (
    <div className="space-y-6">
      {assigningTaskItem && (
        <div className="fixed inset-0 z-[80] bg-slate-900/35 flex items-center justify-center p-4">
          <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden">
            <div className="px-5 py-4 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 className="text-base font-black text-[#004c91]">Chọn người phụ trách</h3>
                <p className="text-xs text-slate-500 mt-0.5 line-clamp-1">{assigningTaskItem.title}</p>
              </div>
              <button
                type="button"
                onClick={() => setAssigningTaskItem(null)}
                className="w-8 h-8 rounded-full hover:bg-slate-100 flex items-center justify-center text-slate-400"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <div className="max-h-[360px] overflow-y-auto py-2">
              {filteredCandidates.length === 0 ? (
                <div className="px-5 py-8 text-center text-sm font-semibold text-slate-400">Không có nhân sự phù hợp</div>
              ) : filteredCandidates.map((staff) => {
                const staffConflict = getCandidateConflict(staff.id || staff.userId);
                return (
                  <button
                    key={staff.id || staff.userId}
                    type="button"
                    disabled={!!staffConflict}
                    onClick={() => {
                      if (staffConflict) {
                        toast.error(`Nhân sự ${staff.name} đã bị trùng thời gian (${staffConflict.time} - ${staffConflict.title})!`);
                        return;
                      }
                      handleSelectAssignee(staff);
                    }}
                    className={`w-full px-5 py-3 text-left transition-colors flex items-center justify-between gap-3 ${
                      staffConflict ? 'bg-red-50/50 hover:bg-red-50 cursor-not-allowed border-l-4 border-red-500' : 'hover:bg-blue-50 cursor-pointer'
                    }`}
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-black text-slate-800 truncate">{staff.name}</p>
                      <p className="text-xs font-medium text-slate-500 truncate">{staff.email}</p>
                      {staffConflict && (
                        <p className="text-[11px] font-bold text-red-600 mt-1 flex items-center gap-1">
                          <AlertCircle className="w-3 h-3 text-red-500 shrink-0 inline" />
                          Bị trùng thời gian ({staffConflict.time} - {staffConflict.title})
                        </p>
                      )}
                    </div>
                    {!staffConflict && <ChevronRight className="w-4 h-4 text-slate-300" />}
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      )}


      <div className={viewMode === 'calendar' ? 'bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden font-sans' : 'font-sans'}>

        {/* Shared Header Bar */}
        {viewMode === 'calendar' && (
          <header className="p-4 sm:p-6 pb-4 flex flex-wrap items-center gap-4">
            {/* Google-Calendar-style toolbar button group */}
            <div className="bg-slate-100 p-0.5 rounded-xl border border-slate-200 flex items-center gap-1">
              <button
                onClick={handleResetToAugust2026}
                className="px-4 py-2 text-xs font-bold text-slate-700 bg-white shadow-xs hover:bg-slate-50 border border-slate-250/60 rounded-lg transition-all"
              >
                Hôm nay
              </button>
              <div className="h-4 w-px bg-slate-200 mx-1"></div>
              <button
                onClick={handlePrev}
                className="p-2 text-slate-600 hover:bg-white rounded-lg hover:text-slate-800 hover:shadow-3xs transition-all active:scale-95"
                title="Trước"
              >
                <ChevronLeft className="w-4 h-4" />
              </button>
              <button
                onClick={handleNext}
                className="p-2 text-slate-600 hover:bg-white rounded-lg hover:text-slate-800 hover:shadow-3xs transition-all active:scale-95"
                title="Sau"
              >
                <ChevronRight className="w-4 h-4" />
              </button>
            </div>

            {/* Month & Year Dropdown Trigger with Mini Calendar Popover */}
            <div className="relative">
              <button
                onClick={() => {
                  setMiniMonth(currentMonth);
                  setMiniYear(currentYear);
                  setShowMiniCalendar(!showMiniCalendar);
                  setShowDisplayDropdown(false);
                }}
                className="flex items-center justify-between w-[155px] px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-700 hover:bg-slate-50 transition-colors shadow-3xs"
              >
                <span className="text-slate-800 select-none">
                  {displayMode === 'Ngày' && selectedCellDate ? `Ngày ${selectedCellDate.split('-').reverse().join('/')}` :
                    displayMode === 'Tuần' && selectedCellDate ? `Tuần ${(() => {
                      const d = parseDateKey(selectedCellDate);
                      const startYear = new Date(d.getFullYear(), 0, 1);
                      const days = Math.floor((d.getTime() - startYear.getTime()) / (24 * 60 * 60 * 1000));
                      return Math.ceil((d.getDay() + 1 + days) / 7);
                    })()}` :
                      displayMode === 'Năm' ? `Năm ${currentYear}` :
                        `Tháng ${currentMonth + 1}, ${currentYear}`}
                </span>
                <ChevronDown className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
              </button>

              {showMiniCalendar && (
                <>
                  <div className="fixed inset-0 z-25" onClick={() => setShowMiniCalendar(false)} />
                  <div className="absolute left-0 top-full mt-2 w-[280px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 p-4 animate-fade-in-quick text-slate-800">
                    <div className="flex items-center justify-between mb-3.5">
                      <span className="text-xs font-extrabold text-slate-700">
                        Tháng {miniMonth + 1} năm {miniYear}
                      </span>
                      <div className="flex items-center gap-1">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            if (miniMonth === 0) { setMiniMonth(11); setMiniYear(y => y - 1); }
                            else setMiniMonth(m => m - 1);
                          }}
                          className="p-1 text-slate-550 hover:bg-slate-50 border border-transparent hover:border-slate-200 rounded-lg hover:shadow-3xs transition-all active:scale-95"
                        >
                          <ChevronLeft className="w-3.5 h-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            if (miniMonth === 11) { setMiniMonth(0); setMiniYear(y => y + 1); }
                            else setMiniMonth(m => m + 1);
                          }}
                          className="p-1 text-slate-550 hover:bg-slate-50 border border-transparent hover:border-slate-200 rounded-lg hover:shadow-3xs transition-all active:scale-95"
                        >
                          <ChevronRight className="w-3.5 h-3.5" />
                        </button>
                      </div>
                    </div>
                    <div className="grid grid-cols-7 text-center text-[10px] font-black text-slate-400 mb-2">
                      <div>CN</div><div>T2</div><div>T3</div><div>T4</div><div>T5</div><div>T6</div><div>T7</div>
                    </div>
                    <div className="grid grid-cols-7 text-center gap-y-1 text-xs">
                      {miniDaysGrid.map((cell, idx) => {
                        const mStr = String(cell.month + 1).padStart(2, '0');
                        const dStr = String(cell.day).padStart(2, '0');
                        const cellDateStr = `${cell.year}-${mStr}-${dStr}`;
                        const isSelected = selectedCellDate === cellDateStr;
                        return (
                          <button
                            key={idx}
                            type="button"
                            onClick={() => {
                              setCurrentMonth(cell.month);
                              setCurrentYear(cell.year);
                              setSelectedCellDate(cellDateStr);
                              const ev = events.find(e => e.date === cellDateStr);
                              if (ev) setActivePopoverEvent(ev);
                              else setActivePopoverEvent(null);
                              setShowMiniCalendar(false);
                            }}
                            className={`w-7 h-7 rounded-full flex items-center justify-center font-bold transition-all mx-auto select-none ${isSelected
                              ? 'bg-[#f37021] text-white shadow-sm font-extrabold scale-105'
                              : cell.isCurrentMonth
                                ? 'text-slate-800 hover:bg-slate-100'
                                : 'text-slate-300 hover:bg-slate-50'
                              }`}
                          >
                            {cell.day}
                          </button>
                        );
                      })}
                    </div>
                  </div>
                </>
              )}
            </div>

            {/* Display Mode Dropdown "Hiển thị: " */}
            <div className="relative">
              <button
                onClick={() => {
                  setShowDisplayDropdown(!showDisplayDropdown);
                  setShowMiniCalendar(false);
                  setShowTypeDropdown(false);
                }}
                className="flex items-center justify-between w-[150px] px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-700 hover:bg-slate-50 transition-colors shadow-3xs"
              >
                <span className="text-slate-800 font-extrabold select-none">Hiển thị: {displayMode}</span>
                <ChevronDown className="w-3.5 h-3.5 text-slate-400 flex-shrink-0" />
              </button>
              {showDisplayDropdown && (
                <>
                  <div className="fixed inset-0 z-25" onClick={() => setShowDisplayDropdown(false)} />
                  <div className="absolute left-0 top-full mt-2 w-[150px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 py-2 animate-fade-in-quick text-slate-800">
                    {(['Ngày', 'Tuần', 'Tháng', 'Năm'] as const).map((mode) => (
                      <button
                        key={mode}
                        type="button"
                        onClick={() => { setDisplayMode(mode); setShowDisplayDropdown(false); }}
                        className={`w-full text-left px-5 py-2.5 text-xs font-bold transition-colors block ${displayMode === mode ? 'bg-slate-50 text-[#004c91]' : 'text-slate-700 hover:bg-slate-50'}`}
                      >
                        {mode}
                      </button>
                    ))}
                  </div>
                </>
              )}
            </div>

            {/* Calendar Type Dropdown */}
            <div className="relative">
              <button
                onClick={() => {
                  setShowTypeDropdown(!showTypeDropdown);
                  setShowDisplayDropdown(false);
                  setShowMiniCalendar(false);
                }}
                className="flex items-center justify-between gap-2 px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-[#004c91] hover:bg-slate-50 transition-colors shadow-3xs"
              >
                <span className="select-none text-left truncate">{calendarType}</span>
                <ChevronDown className="w-3.5 h-3.5 text-[#004c91]/75 flex-shrink-0" />
              </button>
              {showTypeDropdown && (
                <>
                  <div className="fixed inset-0 z-25" onClick={() => setShowTypeDropdown(false)} />
                  <div className="absolute left-0 top-full mt-2 w-[160px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 py-2 animate-fade-in-quick text-slate-800">
                    {((isStudent || isVisitor) ? ['Lịch của tôi'] : ['Trong văn phòng', 'Lịch của tôi']).map((type) => (
                      <button
                        key={type}
                        type="button"
                        onClick={() => {
                          setCalendarType(type as 'Trong văn phòng' | 'Lịch của tôi');
                          setShowTypeDropdown(false);
                        }}
                        className={`w-full text-left px-5 py-2.5 text-xs font-bold transition-colors block ${calendarType === type ? 'bg-slate-50 text-[#f37021]' : 'text-slate-700 hover:bg-slate-50'}`}
                      >
                        {type}
                      </button>
                    ))}
                  </div>
                </>
              )}
            </div>

            {/* Chú thích màu sắc ngang hàng bên phải */}
            <div className="ml-auto flex flex-wrap items-center gap-4 text-xs font-medium text-slate-600">
              {calendarType === 'Trong văn phòng' ? (
                <>
                  <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-amber-400 inline-block" />Cần xử lý</span>
                  <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-emerald-500 inline-block" />Đã có người phụ trách</span>
                  <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-[#004c91] inline-block" />Lịch của tôi</span>
                  <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-slate-400 inline-block" /><span className="line-through text-slate-500">Hủy</span></span>
                </>
              ) : (
                <>
                  <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-[#004c91] inline-block" />Đơn phụ trách</span>
                  <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-purple-500 inline-block" />Lịch cá nhân</span>
                  <span className="flex items-center gap-1.5"><span className="w-3 h-3 rounded-full bg-slate-400 inline-block" /><span className="line-through text-slate-500">Hủy</span></span>
                </>
              )}
            </div>
          </header>
        )}

        {viewMode === 'assignments' && renderAssignmentsProgressPanel()}

        {/* Grid of Calendar (Full Width) */}
        {viewMode === 'calendar' && (
          <div className="relative">
            <div className="w-full">

              {/* Calendar Container */}
              <div className="w-full flex flex-col border-t border-slate-200">

                {/* 1. MONTH VIEW */}
                {displayMode === 'Tháng' && (
                  <>
                    {/* Days of the week header */}
                    <div className="grid grid-cols-7 bg-[#004c91] border-b border-[#002f63] text-center text-xs font-extrabold text-white uppercase tracking-wider py-4">
                      <div>Thứ Hai</div>
                      <div>Thứ Ba</div>
                      <div>Thứ Tư</div>
                      <div>Thứ Năm</div>
                      <div>Thứ Sáu</div>
                      <div>Thứ Bảy</div>
                      <div>Chủ Nhật</div>
                    </div>

                    {/* Grid of Days */}
                    <div className="grid grid-cols-7 grid-rows-5 flex-grow min-h-[920px] divide-x divide-y divide-slate-300 border-l border-r border-b border-slate-300 bg-slate-50/20">
                      {daysGrid.map((cell, idx) => {
                        const dayEvents = filteredEvents.filter(e => e.date === cell.dateString);
                        const isSelected = selectedCellDate === cell.dateString;
                        const isPastDay = cell.dateString < todayStr;
                        return (
                          <div
                            key={idx}
                            onClick={() => {
                              setSelectedCellDate(cell.dateString);
                              const dayEvs = filteredEvents.filter(e => e.date === cell.dateString);
                              if (dayEvs.length > 0) {
                                setDisplayMode('Ngày');
                              }
                            }}
                            className={`h-[175px] max-h-[175px] overflow-hidden p-2 flex flex-col justify-between transition-colors group relative cursor-pointer ${isSelected
                                ? 'bg-orange-50 ring-2 ring-inset ring-[#f37021] z-10 shadow-sm'
                                : cell.isCurrent
                                  ? 'bg-white hover:bg-orange-50/80 text-slate-800'
                                  : 'bg-slate-50/30 hover:bg-orange-50/30 text-slate-350'
                              }`}
                          >
                            {cell.isCurrent ? (
                              <>
                                {/* Header of Date cell */}
                                <div className="flex justify-between items-center mb-1">
                                  <span className={`text-xs font-extrabold px-1.5 py-0.5 rounded-md ${cell.dateString === todayStr && cell.isCurrent
                                      ? 'bg-red-500 text-white shadow-xs'
                                      : isSelected
                                        ? 'bg-[#f37021] text-white'
                                        : 'text-slate-700'
                                    }`}>
                                    {cell.day}
                                  </span>

                                  <button
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      handleOpenAddModal(cell.dateString);
                                    }}
                                    className="opacity-0 group-hover:opacity-100 text-[#f37021] hover:text-[#004c91] transition-opacity p-0.5 hover:bg-orange-100 rounded-md cursor-pointer"
                                    title="Add Logistics Event"
                                  >
                                    <Plus className="w-3.5 h-3.5" />
                                  </button>
                                </div>

                                {/* Event cards space */}
                                <div className="flex-grow space-y-1 overflow-y-auto no-scrollbar pt-1">
                                  {dayEvents.map(ev => {
                                    const isHighlighted = activePopoverEvent?.id === ev.id;
                                    const hasChanges = getEventChangeNotifs(ev).length > 0;
                                    return (
                                      <div
                                        key={ev.id}
                                        id={`event-card-${ev.id}`}
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          setSelectedCellDate(cell.dateString);
                                          setActivePopoverEvent(ev);
                                        }}
                                        className={`relative px-2 py-1.5 rounded-lg border text-[10px] font-normal leading-tight cursor-pointer transition-all truncate selection:bg-transparent ${hasChanges ? 'pr-5' : ''} ${ev.color} ${ev.hoverColor} ${isHighlighted ? 'ring-2 ring-orange-500/10 border-orange-400 shadow-sm' : ''
                                          }`}
                                      >
                                        <span className="inline-block w-1.5 h-1.5 rounded-full mr-1.5 bg-current" />
                                        <span className={ev.status === 'CANCELLED' ? 'line-through' : ''}>{ev.title}</span>
                                        {hasChanges && (
                                          <span className="absolute top-1 right-1 flex h-2 w-2" title="Đơn này có thay đổi mới">
                                            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
                                            <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500" />
                                          </span>
                                        )}
                                      </div>
                                    );
                                  })}
                                </div>
                              </>
                            ) : null}
                            {/* Lớp mờ phủ lên các ngày trong quá khứ */}
                            {cell.isCurrent && isPastDay && (
                              <div className="absolute inset-0 bg-slate-300/45 pointer-events-none z-10" aria-hidden="true" />
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </>
                )}

                {/* 2. WEEK VIEW */}
                {displayMode === 'Tuần' && (
                  <>
                    <div className="grid grid-cols-7 bg-[#004c91] border-b border-[#002f63] text-center text-xs font-extrabold text-white uppercase tracking-wider py-4">
                      <div>Thứ Hai</div>
                      <div>Thứ Ba</div>
                      <div>Thứ Tư</div>
                      <div>Thứ Năm</div>
                      <div>Thứ Sáu</div>
                      <div>Thứ Bảy</div>
                      <div>Chủ Nhật</div>
                    </div>

                    <div className="grid grid-cols-7 flex-grow min-h-[160px] pb-2 divide-x divide-slate-100 bg-slate-50/20">
                      {currentWeekDays.map((cell, idx) => {
                        const dayEvents = filteredEvents.filter(e => e.date === cell.dateString);
                        const isSelected = selectedCellDate === cell.dateString;
                        const isPastDay = cell.dateString < todayStr;
                        return (
                          <div
                            key={idx}
                            onClick={() => {
                              setSelectedCellDate(cell.dateString);
                              const dayEvs = filteredEvents.filter(e => e.date === cell.dateString);
                              if (dayEvs.length > 0) {
                                setDisplayMode('Ngày');
                              }
                            }}
                            className={`p-3.5 flex flex-col justify-between transition-colors group relative cursor-pointer ${isSelected
                                ? 'bg-orange-50 ring-2 ring-inset ring-[#f37021] z-10 shadow-sm'
                                : cell.isCurrent
                                  ? 'bg-white hover:bg-orange-50 text-slate-800'
                                  : 'bg-slate-50/30 hover:bg-orange-50/60 text-slate-350'
                              }`}
                          >
                            <div className="flex justify-between items-center mb-2">
                              <span className={`text-xs font-extrabold px-2 py-1 rounded-md ${cell.dateString === todayStr && cell.isCurrent
                                  ? 'bg-red-500 text-white'
                                  : isSelected
                                    ? 'bg-[#f37021] text-white shadow-xs'
                                    : 'text-slate-700 bg-slate-100'
                                }`}>
                                {cell.day}
                              </span>
                              <button
                                onClick={(e) => {
                                  e.stopPropagation();
                                  handleOpenAddModal(cell.dateString);
                                }}
                                className="opacity-0 group-hover:opacity-100 text-[#f37021] hover:text-[#004c91] transition-opacity p-0.5 hover:bg-orange-100 rounded-md"
                                title="Thêm công việc"
                              >
                                <Plus className="w-3.5 h-3.5" />
                              </button>
                            </div>

                            <div className="flex-grow space-y-1.5 overflow-y-auto no-scrollbar pt-1">
                              {dayEvents.map(ev => {
                                const isHighlighted = activePopoverEvent?.id === ev.id;
                                const hasChanges = getEventChangeNotifs(ev).length > 0;
                                return (
                                  <div
                                    key={ev.id}
                                    onClick={(e) => {
                                      e.stopPropagation();
                                      setSelectedCellDate(cell.dateString);
                                      setActivePopoverEvent(ev);
                                    }}
                                    className={`relative px-2 py-2 rounded-lg border text-[10px] font-normal leading-tight cursor-pointer transition-all ${hasChanges ? 'pr-5' : ''} ${ev.color} ${ev.hoverColor} ${isHighlighted ? 'ring-2 ring-[#f37021]/30 border-[#f37021] shadow-sm scale-[1.01]' : ''
                                      }`}
                                  >
                                    <span className="inline-block w-1.5 h-1.5 rounded-full mr-1.5 bg-current" />
                                    <span className={ev.status === 'CANCELLED' ? 'line-through' : ''}>{ev.title}</span>
                                    {hasChanges && (
                                      <span className="absolute top-1 right-1 flex h-2 w-2" title="Đơn này có thay đổi mới">
                                        <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
                                        <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500" />
                                      </span>
                                    )}
                                  </div>
                                );
                              })}
                            </div>
                            {/* Lớp mờ phủ lên các ngày trong quá khứ */}
                            {isPastDay && (
                              <div className="absolute inset-0 bg-slate-300/45 pointer-events-none z-10" aria-hidden="true" />
                            )}
                          </div>
                        );
                      })}
                    </div>
                  </>
                )}

                {/* 3. DAY VIEW */}
                {displayMode === 'Ngày' && (
                  <div className="p-6 flex flex-col flex-grow min-h-[640px]">
                    {/* Quay lại button */}
                    <div className="mb-4">
                      <button
                        type="button"
                        onClick={() => setDisplayMode('Tháng')}
                        className="flex items-center gap-2 px-3.5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-[11px] font-black rounded-xl transition-all shadow-3xs border border-slate-200/60"
                      >
                        <ChevronLeft className="w-3.5 h-3.5 text-slate-600" />
                        <span>Quay lại lịch tháng</span>
                      </button>
                    </div>

                    <div className="flex justify-between items-center pb-4 border-b border-slate-100 mb-5">
                      <div>
                        <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider">
                          Lịch trình chi tiết ngày {(() => {
                            if (!selectedCellDate) return 'chưa chọn';
                            const parts = selectedCellDate.split('-');
                            return `${parts[2]}/${parts[1]}/${parts[0]}`;
                          })()}
                        </h3>
                        <p className="text-xs text-slate-500 mt-1 font-medium">Báo cáo hậu cần nội bộ trực tiếp cho điều phối viên</p>
                      </div>

                      <button
                        onClick={() => selectedCellDate && handleOpenAddModal(selectedCellDate)}
                        className="flex items-center gap-1.5 px-3.5 py-2 bg-[#f37021] text-white text-xs font-black rounded-lg hover:opacity-90 active:scale-95 transition-all shadow-sm"
                      >
                        <Plus className="w-3.5 h-3.5" />
                        <span>Thêm sự kiện</span>
                      </button>
                    </div>

                    {(() => {
                      const dayEvents = filteredEvents.filter(e => e.date === selectedCellDate);
                      if (dayEvents.length === 0) {
                        return (
                          <div className="flex flex-col items-center justify-center py-24 text-center flex-grow">
                            <div className="w-16 h-16 bg-slate-50 border border-slate-150 rounded-full flex items-center justify-center text-slate-350 mb-4 shadow-3xs">
                              <CalendarIcon className="w-7 h-7" />
                            </div>
                            <h4 className="text-xs font-black text-slate-700">Không có sự kiện hậu cần nào</h4>
                            <p className="text-[11px] text-slate-400 mt-1 max-w-xs font-medium">
                              Ngày này hiện chưa có chương trình đón tiếp hay cuộc họp quốc tế nào được thiết lập.
                            </p>
                          </div>
                        );
                      }

                      return (
                        <div className="space-y-4 flex-grow overflow-y-auto no-scrollbar max-h-[500px] pr-1">
                          {dayEvents.map((ev) => {
                            const isHighlighted = activePopoverEvent?.id === ev.id;
                            const hasChanges = getEventChangeNotifs(ev).length > 0;
                            return (
                              <div
                                key={ev.id}
                                onClick={() => setActivePopoverEvent(ev)}
                                className={`p-4 rounded-xl border transition-all cursor-pointer relative ${ev.color} ${ev.hoverColor} ${isHighlighted ? 'ring-2 ring-[#f37021] border-[#f37021] scale-[1.002]' : 'border-slate-100'
                                  }`}
                              >
                                <div className="flex items-start justify-between gap-3">
                                  <div className="flex items-center gap-2">
                                    <span className="w-2.5 h-2.5 rounded-full bg-current" />
                                    <span className="text-[10px] font-black uppercase tracking-wider opacity-90">{ev.category}</span>
                                    {hasChanges && (
                                      <span className="relative flex h-2 w-2" title="Đơn này có thay đổi mới">
                                        <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
                                        <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500" />
                                      </span>
                                    )}
                                  </div>
                                  <span className="text-[11px] font-bold opacity-80">{ev.time}</span>
                                </div>

                                <h4 className={`text-sm font-medium mt-2 leading-snug ${ev.status === 'CANCELLED' ? 'line-through' : ''}`}>{ev.title}</h4>

                                <div className="mt-3.5 grid grid-cols-1 md:grid-cols-2 gap-2 text-[11px] font-medium opacity-90 border-t border-current/10 pt-2.5">
                                  <div className="flex items-center gap-1.5">
                                    <MapPin className="w-3.5 h-3.5 shrink-0 text-[#f37021]" />
                                    <span className="truncate">{ev.location}</span>
                                  </div>
                                  <div className="flex items-center gap-1.5">
                                    <Users className="w-3.5 h-3.5 shrink-0 text-[#004c91]" />
                                    <span className="truncate">{ev.guests}</span>
                                  </div>
                                </div>
                              </div>
                            );
                          })}
                        </div>
                      );
                    })()}
                  </div>
                )}

                {/* 4. YEAR VIEW */}
                {displayMode === 'Năm' && (
                  <div className="p-6 overflow-y-auto no-scrollbar max-h-[720px]">
                    <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider mb-6 text-center">
                      Tổng quan danh mục sự kiện năm {currentYear}
                    </h3>

                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                      {monthNames.map((mName, mIdx) => {
                        const mDays = getDaysForMonth(currentYear, mIdx);
                        return (
                          <div key={mIdx} className="bg-slate-50/50 p-3 rounded-2xl border border-slate-150/80 hover:border-orange-200 transition-colors">
                            <h4 className="text-xs font-black text-[#004c91] text-center mb-2.5">{mName}</h4>

                            {/* Mon-Sun labels representation */}
                            <div className="grid grid-cols-7 text-center text-[9px] font-black text-slate-400 mb-1">
                              <div>T2</div><div>T3</div><div>T4</div><div>T5</div><div>T6</div><div>T7</div><div>CN</div>
                            </div>

                            <div className="grid grid-cols-7 text-center gap-0.5 text-[10px]">
                              {mDays.map((cell, cIdx) => {
                                const mStr = String(cell.month + 1).padStart(2, '0');
                                const dStr = String(cell.day).padStart(2, '0');
                                const cellDateStr = `${currentYear}-${mStr}-${dStr}`;
                                const isSelected = selectedCellDate === cellDateStr;
                                const hasEvents = filteredEvents.some(e => e.date === cellDateStr);

                                return (
                                  <button
                                    key={cIdx}
                                    type="button"
                                    onClick={() => {
                                      setCurrentMonth(mIdx);
                                      setSelectedCellDate(cellDateStr);
                                      setDisplayMode('Tháng');
                                    }}
                                    className={`relative w-5 h-5 rounded-full flex items-center justify-center font-bold transition-all mx-auto ${isSelected
                                        ? 'bg-[#f37021] text-white font-black'
                                        : hasEvents
                                          ? 'bg-orange-100 text-orange-700 hover:bg-orange-200'
                                          : cell.isCurrent
                                            ? 'text-slate-700 hover:bg-slate-200'
                                            : 'text-slate-350 hover:bg-slate-100/50'
                                      }`}
                                  >
                                    {cell.day}
                                    {hasEvents && (
                                      <span className={`absolute -top-0.5 -right-0.5 w-1.5 h-1.5 rounded-full ${isSelected ? 'bg-white' : 'bg-red-500'} border border-white`} />
                                    )}
                                  </button>
                                );
                              })}
                            </div>
                          </div>
                        );
                      })}
                    </div>
                  </div>
                )}

              </div>

              {/* Left / Right Panel Side Card statistics & active popover tooltip */}
              <div className="hidden">

                {/* Popover Display panel (Active Popover Tooltip showcase) */}
                {activePopoverEvent ? (
                  <div className="bg-white rounded-2xl border-2 border-slate-200 shadow-lg overflow-hidden relative transition-all duration-300 animate-fade-in-quick">

                    {/* Decorative Festive top banner card header for Festive category */}
                    {activePopoverEvent.category === 'Lời mời tham gia' ? (
                      <div className="bg-gradient-to-r from-blue-700 to-[#004c91] p-4 text-white relative">
                        <div className="absolute top-0 right-0 p-8 bg-[radial-gradient(#ffffff_1px,transparent_1px)] opacity-10 pointer-events-none" style={{ backgroundSize: '12px 12px' }}></div>
                        <div className="flex justify-between items-start">
                          <div className="flex items-center gap-2">
                            <span className="bg-blue-800 text-blue-200 border border-blue-300/30 text-[9px] font-black uppercase px-2 py-0.5 rounded-full tracking-widest shadow-inner">
                              ★ VIP Đón tiếp ★
                            </span>
                            <Sparkles className="w-4 h-4 text-blue-250 animate-pulse" />
                          </div>

                          {/* Clean action icons at the top-right corner of the tooltip */}
                          <div className="flex items-center gap-1.5 relative z-10 bg-black/10 p-1 rounded-lg border border-white/10">
                            <button
                              onClick={() => handleDeleteEvent(activePopoverEvent.id)}
                              className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                              title="Trash bin"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                            <button className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors" title="More options">
                              <MoreVertical className="w-3.5 h-3.5" />
                            </button>
                            <button
                              onClick={() => setActivePopoverEvent(null)}
                              className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                              title="Close"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </div>

                        <div className="mt-4">
                          <span className="text-[10px] uppercase font-bold tracking-widest text-amber-200">Tiêu điểm Phái đoàn Khách mời</span>
                          <h4 className="text-[17px] font-black text-white mt-1 leading-snug drop-shadow-xs">
                            {activePopoverEvent.title}
                          </h4>
                        </div>
                      </div>
                    ) : (
                      <div className="bg-gradient-to-r from-[#004c91] to-blue-700 p-4 text-white relative">
                        <div className="flex justify-between items-start">
                          <div className="flex items-center gap-2">
                            <span className="bg-blue-900 border border-blue-500/30 text-[9px] font-bold uppercase px-2.5 py-0.5 rounded-full tracking-widest">
                              Mục {activePopoverEvent.category}
                            </span>
                          </div>

                          {/* Action icons */}
                          <div className="flex items-center gap-1.5 relative z-10 bg-black/10 p-1 rounded-lg border border-white/10">
                            <button
                              onClick={() => handleDeleteEvent(activePopoverEvent.id)}
                              className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                              title="Xóa sự kiện"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                            <button className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors" title="Tùy chọn khác">
                              <MoreVertical className="w-3.5 h-3.5" />
                            </button>
                            <button
                              onClick={() => setActivePopoverEvent(null)}
                              className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded transition-colors"
                              title="Đóng"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          </div>
                        </div>

                        <div className="mt-4">
                          <span className="text-[10px] uppercase font-bold tracking-widest text-blue-200">Sự kiện Hậu cần PEMS</span>
                          <h4 className="text-[17px] font-black text-white mt-1 leading-snug">
                            {activePopoverEvent.title}
                          </h4>
                        </div>
                      </div>
                    )}

                    {/* Event Details Content Area */}
                    <div className="p-5 space-y-3.5 text-slate-800 text-xs max-h-[580px] overflow-y-auto no-scrollbar">

                      {/* VIP Level Badge Row (if any) */}
                      {activePopoverEvent.vipLevel && (
                        <div className="flex gap-3 pb-3 border-b border-slate-100">
                          <div className="w-8 h-8 rounded-lg bg-red-50 text-red-600 flex items-center justify-center shrink-0">
                            <Sparkles className="w-4 h-4" />
                          </div>
                          <div>
                            <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Cấp độ tiếp đón ngoại giao</span>
                            <span className={`inline-block text-[9px] font-black uppercase px-2 py-0.5 mt-1 rounded ${activePopoverEvent.vipLevel === 'VVIP'
                                ? 'bg-red-600 text-white animate-pulse'
                                : activePopoverEvent.vipLevel === 'VIP'
                                  ? 'bg-amber-550 bg-[#f37021] text-white'
                                  : 'bg-slate-100 text-slate-650'
                              }`}>
                              {activePopoverEvent.vipLevel} CLASS / GUEST
                            </span>
                          </div>
                        </div>
                      )}

                      {/* Purpose / Work info */}
                      {activePopoverEvent.purpose && (
                        <div className="flex gap-3 pb-3 border-b border-slate-100">
                          <div className="w-8 h-8 rounded-lg bg-amber-50 text-amber-700 flex items-center justify-center shrink-0">
                            <FileText className="w-4 h-4" />
                          </div>
                          <div>
                            <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Mục đích đón tiếp & Nội dung làm việc</span>
                            <p className="font-bold text-slate-700 mt-0.5 leading-relaxed">{activePopoverEvent.purpose}</p>
                          </div>
                        </div>
                      )}

                      {/* Time field */}
                      <div className="flex gap-3 pb-3 border-b border-slate-100">
                        <div className="w-8 h-8 rounded-lg bg-orange-50 text-[#f37021] flex items-center justify-center shrink-0">
                          <Clock className="w-4 h-4" />
                        </div>
                        <div>
                          <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Ngày & Giờ diễn ra</span>
                          <p className="font-bold text-slate-700 mt-0.5 leading-relaxed">
                            {(() => {
                              const d = parseDateKey(activePopoverEvent.date);
                              const weekdays = ['Chủ Nhật', 'Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy'];
                              return `${weekdays[d.getDay() || 0]}, ngày ${d.getDate()} tháng ${d.getMonth() + 1} năm ${d.getFullYear()}`;
                            })()}
                          </p>
                          <p className="font-bold text-[#f37021] mt-0.5">{activePopoverEvent.time}</p>
                        </div>
                      </div>

                      {/* Location field */}
                      <div className="flex gap-3 pb-3 border-b border-slate-100">
                        <div className="w-8 h-8 rounded-lg bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
                          <MapPin className="w-4 h-4" />
                        </div>
                        <div>
                          <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Địa điểm & Vị trí tổ chức</span>
                          <p className="font-bold text-slate-700 mt-0.5 leading-normal">{activePopoverEvent.location}</p>
                        </div>
                      </div>

                      {/* Vehicle scheduling details */}
                      {activePopoverEvent.carBooking && (
                        <div className="flex gap-3 pb-3 border-b border-slate-100">
                          <div className="w-8 h-8 rounded-lg bg-cyan-50 text-cyan-700 flex items-center justify-center shrink-0">
                            <TrendingUp className="w-4 h-4" />
                          </div>
                          <div>
                            <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Xe công vụ & Đưa đón</span>
                            <p className="font-semibold text-slate-650 mt-1 leading-relaxed">{activePopoverEvent.carBooking}</p>
                          </div>
                        </div>
                      )}

                      {/* Banner / Welcoming Text */}
                      {activePopoverEvent.bannerText && (
                        <div className="flex gap-3 pb-3 border-b border-slate-100">
                          <div className="w-8 h-8 rounded-lg bg-red-550/10 bg-red-50 text-red-650 flex items-center justify-center shrink-0">
                            <Bell className="w-4 h-4" />
                          </div>
                          <div className="flex-1 min-w-0">
                            <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Khẩu hiệu chào mừng trên màn hình LED</span>
                            <div className="bg-slate-900 text-yellow-300 font-mono text-[9px] p-2 mt-1 rounded border border-slate-950 leading-snug break-words">
                              {activePopoverEvent.bannerText}
                            </div>
                          </div>
                        </div>
                      )}

                      {/* Host field */}
                      <div className="flex gap-3 pb-3 border-b border-slate-100">
                        <div className="w-8 h-8 rounded-lg bg-purple-50 text-purple-600 flex items-center justify-center shrink-0">
                          <Bookmark className="w-4 h-4" />
                        </div>
                        <div>
                          <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Bộ phận FPTU chủ trì / Host</span>
                          <p className="font-bold text-slate-700 mt-0.5 leading-normal">{activePopoverEvent.host}</p>
                        </div>
                      </div>

                      {/* Contact Person Details */}
                      {activePopoverEvent.contactPerson && (
                        <div className="flex gap-3 pb-3 border-b border-slate-100">
                          <div className="w-8 h-8 rounded-lg bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0">
                            <Users className="w-4 h-4" />
                          </div>
                          <div>
                            <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Cán bộ điều phối liên hệ</span>
                            <p className="font-bold text-emerald-700 mt-0.5">{activePopoverEvent.contactPerson}</p>
                          </div>
                        </div>
                      )}

                      {/* Hotel list information */}
                      {activePopoverEvent.hotelInfo && (
                        <div className="flex gap-3 pb-3 border-b border-slate-100">
                          <div className="w-8 h-8 rounded-lg bg-pink-50 text-pink-600 flex items-center justify-center shrink-0">
                            <MapPin className="w-4 h-4" />
                          </div>
                          <div>
                            <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Khách sạn lưu trú đoàn khách</span>
                            <p className="font-semibold text-slate-650 mt-1 leading-relaxed">{activePopoverEvent.hotelInfo}</p>
                          </div>
                        </div>
                      )}

                      {/* Guests field */}
                      <div className="flex gap-3 pb-3 border-b border-slate-100">
                        <div className="w-8 h-8 rounded-lg bg-sky-50 text-sky-600 flex items-center justify-center shrink-0">
                          <Users className="w-4 h-4" />
                        </div>
                        <div>
                          <span className="text-[10px] font-extrabold text-slate-400 uppercase tracking-wider block">Chi tiết đoàn khách đối tác</span>
                          <p className="font-bold text-slate-700 mt-0.5 leading-relaxed">{activePopoverEvent.guests}</p>
                        </div>
                      </div>

                      {/* Logistics Checklist */}
                      {activePopoverEvent.checklist && activePopoverEvent.checklist.length > 0 && (
                        <div className="space-y-2.5 pt-1">
                          <span className="text-[10px] font-extrabold text-[#f37021] uppercase tracking-widest block">
                            ✔ Checklist nhiệm vụ hậu cần:
                          </span>
                          <ul className="space-y-1.5 bg-slate-50 p-3 rounded-xl border border-slate-200/60 font-medium text-slate-650">
                            {activePopoverEvent.checklist.map((item, idx) => (
                              <li key={idx} className="flex items-start gap-2">
                                <CheckSquare className="w-3.5 h-3.5 text-emerald-500 shrink-0 mt-0.5" />
                                <span>{item}</span>
                              </li>
                            ))}
                          </ul>
                        </div>
                      )}

                    </div>
                  </div>
                ) : (
                  <div className="bg-slate-50 rounded-2xl border-2 border-dashed border-slate-200 p-6 text-center text-slate-400">
                    <Info className="w-8 h-8 mx-auto text-slate-350 mb-2" />
                    <p className="text-xs font-bold">Vui lòng chọn bất kỳ sự kiện nào trên lịch để rà soát chi tiết hậu cần & thao tác.</p>
                  </div>
                )}

                {/* Dynamic Month/Year Events list list sidebar (Lịch chi tiết toàn bộ sự kiện của tháng) */}
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5 space-y-4">
                  <div>
                    <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider">
                      Các mốc sự kiện trong {monthNames[currentMonth]} năm {currentYear}
                    </h3>
                    <p className="text-[10px] text-slate-400 mt-0.5 font-semibold">Tự động tập hợp dựa trên lịch học & phái đoàn đã khởi tạo</p>
                  </div>

                  <div className="space-y-2.5 max-h-[360px] overflow-y-auto no-scrollbar pr-0.5">
                    {eventsInCurrentMonthAndYear.length === 0 ? (
                      <div className="text-center py-6 text-slate-400 text-xs italic">
                        Không có chương trình hay đoàn khách tiếp đón nào được đăng ký trong tháng này.
                      </div>
                    ) : (
                      eventsInCurrentMonthAndYear.map((ev) => {
                        const isSelected = selectedCellDate === ev.date && activePopoverEvent?.id === ev.id;
                        const isTodayHighlight = ev.date === todayStr;
                        const parts = ev.date.split('-');
                        const displayDayNum = parts[2];

                        return (
                          <div
                            key={ev.id}
                            onClick={() => {
                              setSelectedCellDate(ev.date);
                              setActivePopoverEvent(ev);
                            }}
                            className={`p-3 rounded-xl border text-xs cursor-pointer transition-all ${isSelected
                                ? 'bg-orange-50/90 border-[#f37021] ring-1 ring-[#f37021] text-slate-800'
                                : 'bg-slate-50 hover:bg-orange-50/40 hover:border-orange-200 text-slate-700 border-slate-100'
                              }`}
                          >
                            <div className="flex justify-between items-center gap-1.5 mb-2 leading-none">
                              <span className={`text-[9px] font-black uppercase tracking-wider px-2 py-0.5 rounded ${ev.category === 'Lời mời tham gia'
                                  ? 'bg-blue-100 text-blue-800'
                                  : ev.category === 'Lời mời tham gia'
                                    ? 'bg-emerald-100 text-emerald-800'
                                    : 'bg-orange-100 text-orange-800'
                                }`}>
                                {ev.category}
                              </span>
                              <span className="text-[10px] font-bold text-[#f37021]">
                                {ev.time}
                              </span>
                            </div>

                            <h4 className="font-extrabold text-[#004c91] text-[11px] mb-1.5 leading-snug line-clamp-2">
                              {ev.title}
                            </h4>

                            <div className="flex items-center justify-between text-[10px] font-extrabold text-slate-450 border-t border-slate-100 pt-1.5 mt-1.5">
                              <span className="text-slate-500 font-extrabold">Ngày {displayDayNum} {monthNames[currentMonth]}</span>
                              <span className="text-slate-400 font-medium truncate max-w-[110px]">{ev.location}</span>
                            </div>
                          </div>
                        );
                      })
                    )}
                  </div>

                  <div className="p-3 bg-[#004c91]/5 rounded-xl flex items-center justify-between gap-3 text-[11px] leading-relaxed font-bold text-slate-650 border border-[#004c91]/10">
                    <span>Tổng số sự kiện cần rà soát:</span>
                    <span className="font-black text-[#004c91] bg-white border border-[#004c91]/20 px-2 py-0.5 rounded-md shadow-3xs">
                      {eventsInCurrentMonthAndYear.length} sự kiện
                    </span>
                  </div>
                </div>

              </div>
            </div>
          </div>
        )}

        {/* Add event modal */}
        {showAddFormModal && (
          <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
            <div className="bg-white rounded-2xl max-w-3xl w-full border border-slate-200 shadow-2xl overflow-hidden animate-fade-in-quick">

              <div className="bg-[#004c91] px-5 py-4 text-white flex justify-between items-center">
                <h3 className="font-black text-sm flex items-center gap-2">
                  <CalendarIcon className="w-4 h-4 text-[#f37021]" />
                  Lên Lịch Công Tác ({selectedCellDate})
                </h3>
                <button
                  onClick={() => setShowAddFormModal(false)}
                  className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded-full transition-colors"
                >
                  <X className="w-4 h-4" />
                </button>
              </div>

              <form onSubmit={handleAddEventSubmit} className="p-6 space-y-4 text-xs text-slate-800">
                <div className="space-y-4">
                  <div className="grid grid-cols-2 gap-3">
                    <div>
                      <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                        Tiêu đề sự kiện *
                      </label>
                      <input
                        type="text"
                        required
                        placeholder="VD: Họp định kỳ"
                        value={newTitle}
                        onChange={e => setNewTitle(e.target.value)}
                        className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                      />
                    </div>
                    <div>
                      <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                        Khung giờ
                      </label>
                      <div className="flex items-center gap-2">
                        <input
                          type="time"
                          required
                          value={newStartTime}
                          onChange={e => setNewStartTime(e.target.value)}
                          className="flex-1 text-xs px-3 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                        />
                        <span className="text-slate-400 font-bold text-xs shrink-0">—</span>
                        <input
                          type="time"
                          required
                          value={newEndTime}
                          min={newStartTime}
                          onChange={e => setNewEndTime(e.target.value)}
                          className="flex-1 text-xs px-3 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                        />
                      </div>
                    </div>
                  </div>

                  <div>
                    <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                      Địa điểm tổ chức
                    </label>
                    <input
                      type="text"
                      value={newLocation}
                      onChange={e => setNewLocation(e.target.value)}
                      className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                      Nội dung
                    </label>
                    <textarea
                      rows={5}
                      value={newContent}
                      onChange={e => setNewContent(e.target.value)}
                      className="w-full text-xs px-3.5 py-2 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none resize-none font-sans bg-slate-50/20"
                    />
                  </div>
                </div>

                <div className="flex justify-end gap-2.5 pt-4 border-t border-slate-100">
                  <button
                    type="button"
                    onClick={() => setShowAddFormModal(false)}
                    className="py-2.5 px-4 bg-slate-150 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors cursor-pointer"
                  >
                    Đóng
                  </button>
                  <button
                    type="submit"
                    className="py-2.5 px-7 bg-[#f37021] text-white font-black rounded-xl hover:opacity-90 active:scale-98 transition-all cursor-pointer shadow-3xs"
                  >
                    Xác nhận lưu
                  </button>
                </div>

              </form>
            </div>
          </div>
        )}

        {/* Wide Horizontal Table Modal representing Giai đoạn 1: Trước tiếp khách */}
        {activePopoverEvent && (
          <>
            {/* Modal này bọc ngoài TaskHandoverModal (biên bản, id #task-handover-modal) bằng các lớp
                overflow-y-auto/max-h-[70vh]/flex — chỉ hiện visibility qua CSS in của TaskHandoverModal
                không đủ, vì overflow/max-height của lớp cha vẫn cắt nội dung khi in. Reset riêng 3 lớp
                cha này (không đụng visibility — đã đúng ở CSS in bên trong). */}
            <style type="text/css" media="print">
              {`
                /* position:static rơi về đúng vị trí trong luồng tài liệu — nếu trang có nội dung
                   ẩn khác nằm TRƯỚC modal này (vd bảng "Phân công và tiến độ"), phần đó vẫn chiếm
                   chỗ dù invisible, đẩy biên bản xuống dưới thành khoảng trắng lớn đầu trang. Ép
                   absolute + top:0 để ghim hẳn lên đầu trang in, giống #task-handover-modal bên trong. */
                #event-modal-backdrop {
                  position: absolute !important;
                  left: 0 !important;
                  top: 0 !important;
                  /* class gốc "inset-0" gán luôn right/bottom: 0 — không reset nốt 2 cạnh này thì
                     khung vẫn bị ép đúng 1 màn hình cao, nội dung dư ra bị cắt dù overflow:visible. */
                  right: auto !important;
                  bottom: auto !important;
                  width: 100% !important;
                  height: auto !important;
                  margin: 0 !important;
                  padding: 0 !important;
                }
                #event-modal-backdrop, #event-modal-card, #event-modal-body {
                  overflow: visible !important;
                  max-height: none !important;
                  height: auto !important;
                  display: block !important;
                }
              `}
            </style>
          <div id="event-modal-backdrop" className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4 overflow-y-auto">
            <div id="event-modal-card" className="bg-white rounded-2xl max-w-5xl w-full border border-slate-200 shadow-2xl overflow-hidden animate-fade-in-quick flex flex-col my-8">

              {/* Modal Title Banner */}
              <div className={`${activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'accepted' ? 'bg-[#f37021]' : 'bg-[#004c91]'} px-6 py-5 text-white flex justify-between items-center relative shadow-sm border-b border-white/10`}>
                <div className="flex items-center gap-3">
                  <div className="p-2.5 rounded-xl bg-white/10 border border-white/20 flex items-center justify-center">
                    {activePopoverEvent.category === 'Lời mời tham gia' || (activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending') ? <Info className="w-5 h-5 text-white" /> : <FileText className="w-5 h-5 text-white" />}
                  </div>
                  <div>
                    <h3 className={`font-extrabold tracking-tight text-white leading-tight font-sans ${activePopoverEvent.category === 'Lời mời tham gia' || (activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending') ? 'text-xl md:text-3xl' : 'text-base md:text-lg'}`}>
                      {activePopoverEvent.category === 'Lời mời tham gia' ? 'Chi tiết thư mời' : activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending' ? 'Thông tin chi tiết' : activePopoverEvent.title}
                    </h3>
                    <p className={`text-white/80 mt-1 ${activePopoverEvent.category === 'Lời mời tham gia' ? 'text-sm font-medium' : 'text-[11px] mt-0.5'}`}>
                      {activePopoverEvent.category === 'Lời mời tham gia'
                        ? 'Thông tin sự kiện'
                        : activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'pending'
                          ? 'Nhiệm vụ được giao'
                          : activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'accepted'
                            ? 'Hồ sơ pháp lý: Biên bản bàn giao kỹ thuật & Nghiệm thu xe điện đối ngoại'
                            : 'Bảng chi tiết thông tin và phương án cơ sở vật chất đón tiếp phái đoàn'}
                    </p>
                  </div>
                </div>

                <button
                  onClick={() => setActivePopoverEvent(null)}
                  className="text-white/85 hover:text-white p-2 hover:bg-white/10 rounded-full transition-all flex items-center justify-center shadow-3xs"
                >
                  <X className="w-5 h-5" />
                </button>
              </div>

              {/* Modal Contents in a clean wide Horizontal Table layout */}
              <div id="event-modal-body" className="p-6 md:p-8 space-y-4 overflow-y-auto max-h-[70vh] no-scrollbar bg-slate-50/50">
                {/* Thay đổi mới (thông báo chưa đọc gắn với đơn/thư mời này) */}
                {(() => {
                  const changes = getEventChangeNotifs(activePopoverEvent);
                  if (changes.length === 0) return null;
                  return (
                    <div className="bg-red-50/70 border border-red-200 rounded-2xl overflow-hidden">
                      <div className="px-5 py-3 flex items-center gap-2.5 border-b border-red-100 bg-red-50">
                        <span className="relative flex h-2.5 w-2.5 shrink-0">
                          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
                          <span className="relative inline-flex h-2.5 w-2.5 rounded-full bg-red-500" />
                        </span>
                        <span className="text-sm font-bold text-red-700">Thay đổi mới ({changes.length})</span>
                        <span className="text-[11px] text-red-400 ml-auto hidden sm:block">Bấm vào từng thay đổi để mở đúng chỗ cần xem</span>
                      </div>
                      <div className="divide-y divide-red-100">
                        {changes.map(n => (
                          <button
                            key={n.notificationId}
                            onClick={() => handleChangeNotifClick(n)}
                            className="w-full text-left px-5 py-3 hover:bg-red-100/50 transition-colors cursor-pointer"
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
                  );
                })()}

                {/* Xem chi tiết đoàn đón khách */}
                <div className="w-full">
                  <button
                    onClick={() => setShowDetailSection(!showDetailSection)}
                    className="w-full flex items-center justify-between px-5 py-3.5 bg-orange-50 hover:bg-orange-100 text-[#f37021] font-black rounded-xl transition-colors border border-orange-200"
                  >
                    <span className="flex items-center gap-2 text-sm uppercase tracking-wider">
                      <Users className="w-5 h-5" /> Xem chi tiết đoàn đón khách
                    </span>
                    {showDetailSection ? <ChevronUp className="w-5 h-5" /> : <ChevronDown className="w-5 h-5" />}
                  </button>

                  {showDetailSection && (
                    <div className="mt-4 bg-white border border-orange-100 rounded-2xl shadow-sm overflow-hidden animate-fade-in-quick text-sm">

                      {/* 1. Thông tin người tạo */}
                      <div className="p-5 border-b border-orange-100">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">1</span>
                          Thông tin người tạo
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Chi tiết về người liên hệ, đơn vị phụ trách đăng ký lịch</p>
                        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 bg-slate-50 p-4 rounded-xl text-xs">
                          <div>
                            <p className="text-slate-500 mb-1">Họ và tên</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.registrantFullName || 'N/A'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Email</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.registrantEmail || 'N/A'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Đơn vị công tác</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.registrantOrganization || 'N/A'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Chức danh</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.registrantJobTitle || 'N/A'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Số điện thoại (SĐT)</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.registrantPhone || 'N/A'}</p>
                          </div>
                        </div>
                      </div>

                      {/* 2. Thông tin đoàn khách */}
                      <div className="p-5 border-b border-orange-100">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">2</span>
                          Thông tin đoàn khách
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Tên cơ quan, thời gian, cơ sở hoạt động và mục đích đối ngoại</p>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 bg-slate-50 p-4 rounded-xl text-xs mb-4">
                          <div>
                            <p className="text-slate-500 mb-1">Tên đoàn</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.delegationName || 'N/A'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Cơ sở đón tiếp</p>
                            <p className="font-bold text-slate-800">{activePopoverEvent?.location || 'Hòa Lạc'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Ngày bắt đầu</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.date || 'N/A'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1">Thời gian</p>
                            <p className="font-bold text-slate-800">{activeEventDetail?.startTime} - {activeEventDetail?.endTime}</p>
                          </div>
                        </div>
                        <div className="space-y-3 bg-slate-50 p-4 rounded-xl text-xs">
                          <div>
                            <p className="text-slate-500 mb-1 font-bold">Mục đích thăm</p>
                            <p className="text-slate-800">{activeEventDetail?.purpose || 'Không có'}</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-1 font-bold">Nội dung làm việc</p>
                            <p className="text-slate-800">{activeEventDetail?.workingContent || 'Không có'}</p>
                          </div>
                        </div>
                      </div>

                      {/* 3. Setup */}
                      <div className="p-5 border-b border-orange-100">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">3</span>
                          Setup
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Tiêu chí bố trí tham quan, chương trình chi tiết & thành phần tham gia</p>
                        <div className="bg-slate-50 p-4 rounded-xl text-xs space-y-4">
                          <div>
                            <p className="text-slate-500 mb-1">Loại hình tham quan</p>
                            <p className="font-bold text-slate-800">Đón tiếp đoàn khách quốc tế và sự kiện</p>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-2 font-bold">Agenda chi tiết</p>
                            <table className="w-full text-left bg-white border border-slate-200 rounded-lg overflow-hidden">
                              <thead className="bg-[#004c91] text-white">
                                <tr>
                                  <th className="p-2 w-1/4">Khung Giờ</th>
                                  <th className="p-2">Khung nội dung chi tiết đón tiếp & tham quan dự kiến</th>
                                </tr>
                              </thead>
                              <tbody>
                                <tr className="border-b"><td className="p-2 font-bold">18:00 - 18:15</td><td className="p-2">Tập trung phái đoàn, đón tiếp xã giao sảnh Alpha, chụp hình lưu niệm check-in.</td></tr>
                                <tr className="border-b"><td className="p-2 font-bold">18:15 - 19:30</td><td className="p-2">Làm việc trao đổi học thuật, thảo luận chi tiết hợp tác hành chính tại phòng họp VIP sảnh Alpha.</td></tr>
                                <tr><td className="p-2 font-bold">19:30 - 22:00</td><td className="p-2">Campus Tour: Di chuyển bằng xe điện tham quan khu phát triển công nghệ cao, Thư viện số và chào tạm biệt đoàn.</td></tr>
                              </tbody>
                            </table>
                          </div>
                          <div>
                            <p className="text-slate-500 mb-2 font-bold">Thành phần tham gia</p>
                            <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Host</p><p className="font-bold">Nguyễn Văn A</p></div>
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Người hỗ trợ bên IC</p><p className="font-bold">Nguyễn Văn B</p></div>
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Người thuộc phòng ban khác</p><p className="font-bold">Nguyễn Văn C</p></div>
                              <div className="bg-white p-2 rounded border"><p className="text-[10px] text-slate-400">Sinh viên hỗ trợ</p><p className="font-bold">Nguyễn Văn D</p></div>
                            </div>
                          </div>
                        </div>
                      </div>

                      {/* 4. Detail setup */}
                      <div className="p-5 bg-orange-50/50">
                        <h4 className="font-bold text-[#004c91] mb-1 flex items-center gap-2">
                          <span className="w-6 h-6 rounded-full bg-orange-100 text-[#f37021] flex items-center justify-center text-xs">4</span>
                          Detail setup
                        </h4>
                        <p className="text-xs text-slate-500 mb-4">Yêu cầu kỹ thuật về khẩu hiệu trình chiếu LED và công tác chuẩn bị đón tiếp Campus Tour</p>
                        <div className="space-y-4">
                          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm text-xs">
                            <h5 className="font-bold text-slate-800 border-b pb-2 mb-2 text-[13px]">Mục 1: Trình chiếu khẩu hiệu LED</h5>
                            <p className="mb-1"><span className="font-bold text-[#0aa14f]">Có sử dụng</span> <span className="text-slate-500">(Hiển thị chạy tự động dọc theo màn hình LED lớn sảnh chính đón khách)</span></p>
                            <p className="bg-slate-100 p-2 rounded text-[#f37021] font-bold text-center border border-slate-200 mt-2">"FPT UNIVERSITY LUNAR NEW YEAR EVE CELEBRATION FOR INTERNATIONALS"</p>
                          </div>

                          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm text-xs">
                            <h5 className="font-bold text-slate-800 border-b pb-2 mb-3 text-[13px]">Mục 2: Chuẩn bị cho Campus Tour</h5>
                            <div className="space-y-3">
                              <div>
                                <p className="font-bold text-[#004c91] mb-1">Phần 1: Người dẫn</p>
                                <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Bố trí 02 Đại sứ sinh viên xuất sắc hướng dẫn dẫn đoàn và thuyết minh lưu loát bằng tiếng Anh/Việt.</p>
                              </div>
                              <div>
                                <p className="font-bold text-[#004c91] mb-1">Phần 2: Xe điện</p>
                                <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Chuẩn bị sẵn 01 xe điện sạc đầy pin 100%, bảo dưỡng lốp, lau dọn khu vực ghế tươm tất.</p>
                              </div>
                              <div>
                                <p className="font-bold text-[#004c91] mb-1">Phần 3: Người lái</p>
                                <p className="text-slate-700 bg-slate-50 p-2 rounded border border-slate-100">Cử cán bộ lái xe điện chuyên trách túc trực, trang phục lịch thiệp, an toàn.</p>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>

                    </div>
                  )}
                </div>


                {(activePopoverEvent.category === 'Lời mời tham gia' || activePopoverEvent.itemType === 'INVITATION') && (
                  <div className="bg-white rounded-2xl p-6 md:p-8 font-sans w-full space-y-6 relative overflow-visible">

                    {/* BENTO GRID (Người gửi, Thời gian gửi, Đoàn khách, Thời gian diễn ra) */}
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">

                      <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                        <div className="flex items-center gap-2 text-gray-400 mb-2">
                          <User className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Người gửi</span>
                        </div>
                        <div className="text-sm font-black text-[#004c91]">{activePopoverEvent.host}</div>
                      </div>

                      <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                        <div className="flex items-center gap-2 text-gray-400 mb-2">
                          <Clock className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian gửi</span>
                        </div>
                        <div className="text-sm font-black text-[#004c91]">{formatDateTimeDisplay(activeEventDetail?.requestedAt)}</div>
                      </div>

                      <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default flex flex-col justify-center">
                        <div className="flex items-center gap-2 text-gray-400 mb-2">
                          <Users className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Đoàn khách</span>
                        </div>
                        <div className="text-base font-black text-[#004c91] border-l-4 border-[#f37021] pl-3 py-1 bg-transparent leading-none flex items-center">
                          {activePopoverEvent.guests}
                        </div>
                      </div>

                      <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default relative overflow-hidden flex flex-col justify-center">
                        <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                          <Calendar className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian diễn ra</span>
                        </div>
                        <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center flex-wrap gap-2 sm:gap-3">
                          <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[0]?.trim()}</span>
                          <ChevronRight className="w-4 h-4 text-gray-400" />
                          <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[1]?.trim()}</span>
                          <span className="text-[#004c91] font-bold ml-1">{activePopoverEvent.date?.split('-').reverse().join('-')}</span>
                        </div>
                      </div>

                    </div>

                    <div className="flex flex-col gap-3 pt-4 transition-all cursor-default relative z-10">
                      <div className="flex items-center gap-2 text-gray-400">
                        <FileText className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung</span>
                      </div>
                      <div className="text-[15px] font-medium text-gray-700 leading-relaxed transition-all relative">
                        {typeof activePopoverEvent.purpose === 'string' && activePopoverEvent.purpose.split('\n').map((line, idx) => (
                          <p key={idx} className={idx > 0 && line.startsWith('Vui lòng') ? 'mt-4 font-bold text-gray-900 border-l-2 border-[#004c91] pl-3 py-1 bg-blue-50/50' : 'mb-2'}>
                            {line}
                          </p>
                        ))}
                      </div>
                    </div>

                    {invitationStatus === 'rejecting' && (
                      <div className="animate-fade-in-quick pt-4">
                        <label className="block text-[11px] font-bold text-gray-700 uppercase tracking-wider mb-2">Lý do từ chối</label>
                        <textarea
                          rows={3}
                          className="w-full text-sm p-4 border border-red-200 rounded-xl focus:border-red-500 focus:ring-1 focus:ring-red-200 outline-none resize-none"
                          placeholder="Nhập lý do không thể tham gia..."
                          value={rejectReason}
                          onChange={(e) => setRejectReason(e.target.value)}
                          autoFocus
                        />
                        <div className="flex justify-end gap-3 mt-3">
                          <button
                            onClick={() => {
                              setInvitationStatus('pending');
                              setRejectReason('');
                            }}
                            className="px-5 py-2 rounded-xl text-gray-500 hover:bg-gray-100 font-bold text-xs"
                          >
                            Hủy
                          </button>
                          <button
                            onClick={async () => {
                              try {
                                if (activePopoverEvent?.rawId) {
                                  await departmentReceptionTasksApi.declineInvitation(activePopoverEvent.rawId, rejectReason);
                                  toast.success('Đã gửi phản hồi từ chối');
                                  setInvitationStatus('rejected');
                                  const now = toVietnamCalendarDate(new Date())!;
                                  const timeStr = `${String(now.getUTCDate()).padStart(2, '0')}/${String(now.getUTCMonth() + 1).padStart(2, '0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2, '0')}:${String(now.getUTCMinutes()).padStart(2, '0')}`;
                                  setRejectSignature({ name: (user?.name || 'Khách') + (isDeptLeader ? ' - Trưởng phòng' : ' - Nhân viên'), time: timeStr });
                                  await fetchCalendarEvents();
                                }
                              } catch (e) { console.error(e); toast.error('Gửi phản hồi thất bại'); }
                            }}
                            disabled={!rejectReason.trim()}
                            className="px-5 py-2 rounded-xl bg-red-600 text-white hover:bg-red-700 font-bold text-xs disabled:opacity-50"
                          >
                            Gửi phản hồi
                          </button>
                        </div>
                      </div>
                    )}

                    {invitationStatus === 'rejected' && (
                      <div className="animate-fade-in-quick pt-4">
                        <div className="p-4 rounded-2xl border border-red-200 bg-red-50 flex flex-col gap-3 relative">
                          <div className="flex items-start gap-3">
                            <Info className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                            <div>
                              <span className="text-red-800 font-bold text-sm block mb-1">Đã từ chối tham gia</span>
                              <span className="text-red-600/80 text-xs italic">"{rejectReason}"</span>
                            </div>
                          </div>
                          {rejectSignature && (
                            <div className="bg-red-100/50 px-3 py-1.5 rounded-lg inline-block self-start sm:ml-8">
                              <span className="text-red-800 text-[11px] font-medium flex flex-col sm:flex-row sm:items-center sm:gap-1">
                                <span>bởi: <span className="font-bold">{rejectSignature.name}</span></span>
                                <span className="hidden sm:inline">-</span>
                                <span>{rejectSignature.time}</span>
                              </span>
                            </div>
                          )}
                        </div>
                      </div>
                    )}

                    {invitationStatus === 'accepted' && acceptSignature && (
                      <div className="animate-fade-in-quick pt-4">
                        <div className="p-5 rounded-2xl border border-[#004c91] bg-blue-50/50 flex flex-col gap-3">
                          <div className="flex items-center gap-2">
                            <div className="w-6 h-6 rounded-full bg-[#004c91] flex items-center justify-center">
                              <CheckSquare className="w-3.5 h-3.5 text-white" />
                            </div>
                            <span className="text-[#004c91] font-black text-sm">Đã xác nhận tham gia</span>
                          </div>
                          <div className="border-t border-blue-100 pt-3">
                            <div className="flex flex-col">
                              <span className="text-xs text-slate-500 font-medium">Xác nhận bởi: <span className="font-extrabold text-[#004c91]">{acceptSignature.name}</span></span>
                              <span className="text-[10px] text-slate-400 font-mono mt-0.5">{acceptSignature.time}</span>
                            </div>
                          </div>
                        </div>
                      </div>
                    )}

                    {invitationStatus === 'assigned' && (
                      <div className="animate-fade-in-quick pt-4">
                        <div className="p-4 rounded-2xl border border-blue-200 bg-blue-50 flex items-start gap-3 relative mb-2">
                          <Info className="w-5 h-5 text-blue-600 shrink-0 mt-0.5" />
                          <div className="flex-1">
                            <span className="text-blue-800 font-bold text-sm block mb-1">Đã ủy quyền nhiệm vụ</span>
                            <span className="text-blue-600/80 text-xs font-medium block">Người phụ trách hiện tại: <span className="font-extrabold">{assignedPerson}</span></span>
                          </div>
                        </div>
                      </div>
                    )}

                    {/* Hiển thị nút Từ chối / Xác nhận tham gia cho lời mời đang chờ phản hồi. */}
                    {invitationStatus === 'pending' && (
                      <div className="flex gap-4 pt-6 mt-6 border-t border-gray-100 flex-col relative z-10 w-full animate-fade-in-quick">
                        {isDeptLeader && leaderSelfConflict && (
                          <div className="mb-2 p-3 rounded-xl bg-red-50 border border-red-200 text-red-700 text-xs font-bold flex items-start gap-2 shadow-xs">
                            <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
                            <div>
                              <p className="font-black">Thư/đơn này đã trùng thời gian của bạn ({leaderSelfConflict.time}). Hãy phân công cho nhân sự!</p>
                              <p className="text-[11px] font-normal text-red-600 mt-0.5">Trùng với: {leaderSelfConflict.title}</p>
                            </div>
                          </div>
                        )}

                        <div className="flex flex-col sm:flex-row gap-4 w-full">
                          <button
                            onClick={() => setInvitationStatus('rejecting')}
                            disabled={!!assignedPerson}
                            className={`flex-1 py-4 px-6 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${(!!assignedPerson)
                                ? 'border-gray-200 text-gray-400 bg-gray-50 cursor-not-allowed'
                                : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg active:scale-[0.98] cursor-pointer'
                              }`}>
                            Từ chối
                          </button>
                          <button
                            onClick={async () => {
                              if (isDeptLeader && leaderSelfConflict) {
                                toast.error(`Thư/đơn này đã trùng thời gian của bạn (${leaderSelfConflict.time}). Hãy phân công cho nhân sự!`);
                                return;
                              }
                              try {
                                if (activePopoverEvent?.rawId) {
                                  await departmentReceptionTasksApi.acceptInvitation(activePopoverEvent.rawId);
                                  toast.success('Xác nhận tham gia thành công');
                                  const now = toVietnamCalendarDate(new Date())!;
                                  const timeStr = `${String(now.getUTCDate()).padStart(2, '0')}/${String(now.getUTCMonth() + 1).padStart(2, '0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2, '0')}:${String(now.getUTCMinutes()).padStart(2, '0')}`;
                                  setAcceptSignature({ name: user?.name || 'Khách', time: timeStr });
                                  setInvitationStatus('accepted');
                                  await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
                                }
                              } catch (e) { console.error(e); toast.error('Xác nhận thất bại'); }
                            }}
                            disabled={!!assignedPerson || (isDeptLeader && !!leaderSelfConflict)}
                            className={`flex-1 py-4 px-6 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${(!!assignedPerson || (isDeptLeader && !!leaderSelfConflict))
                                ? 'bg-gray-100 text-gray-400 border border-gray-200 cursor-not-allowed'
                                : 'bg-[#004c91] text-white hover:bg-[#003b73] shadow-lg shadow-[#004c91]/20 active:scale-[0.98] border border-blue-600 cursor-pointer'
                              }`}>
                            Xác nhận tham gia
                          </button>
                        </div>

                        {isDeptLeader && (
                          <div className="w-full relative mt-2">
                            <button
                              onClick={() => setShowAssignDropdown(!showAssignDropdown)}
                              disabled={isDeptStaff && deptPreliminaryStatus !== 'accepted'}
                              className={`w-full py-3.5 px-6 rounded-2xl bg-slate-100 text-slate-700 font-black uppercase tracking-wider transition-all duration-300 outline-none text-xs text-center flex items-center justify-center gap-2 ${isDeptStaff && deptPreliminaryStatus !== 'accepted' ? 'opacity-50 cursor-not-allowed border-dashed' : 'hover:bg-slate-200 border border-slate-200'
                                }`}>
                              <User className="w-4 h-4" />
                              {assignedPerson ? `Đã giao: ${assignedPerson}` : 'Ủy quyền / Đổi người phụ trách'}
                            </button>
                            {showAssignDropdown && (
                              <div className="absolute top-full left-0 right-0 mt-2 bg-white border border-slate-200 rounded-xl shadow-[0_8px_30px_-4px_rgba(0,0,0,0.1)] z-50 overflow-hidden">
                                <div className="py-2">
                                  {filteredCandidates.length === 0 ? (
                                    <div className="px-4 py-3 text-xs text-slate-400 text-center font-medium">Không có nhân sự phòng ban</div>
                                  ) : filteredCandidates.map((staff) => {
                                    const staffConflict = getCandidateConflict(staff.id || staff.userId);
                                    return (
                                      <button
                                        key={staff.id || staff.userId}
                                        disabled={!!staffConflict}
                                        className={`w-full px-4 py-3 text-left border-b border-slate-50 last:border-0 transition-colors group flex items-start justify-between ${
                                          staffConflict ? 'bg-red-50/50 hover:bg-red-50 cursor-not-allowed border-l-4 border-red-500' : 'hover:bg-slate-50 cursor-pointer'
                                        }`}
                                        onClick={async () => {
                                          if (staffConflict) {
                                            toast.error(`Nhân sự ${staff.name} đã bị trùng thời gian (${staffConflict.time} - ${staffConflict.title})!`);
                                            return;
                                          }
                                          if (activePopoverEvent?.rawId) {
                                            await openInvitationAssignPreview({
                                              participantId: activePopoverEvent.rawId,
                                              staffId: staff.id || staff.userId,
                                              staffName: staff.name || staff.fullName || 'Nhân sự',
                                              title: activePopoverEvent?.fullTitle || activePopoverEvent?.title,
                                              delegationName: activePopoverEvent?.delegationName,
                                            });
                                          }
                                        }}
                                      >
                                        <div>
                                          <span className="block text-sm font-bold text-slate-800 group-hover:text-[#004c91]">{staff.name}</span>
                                          <span className="block text-xs font-medium text-slate-500 mt-0.5">{staff.email}</span>
                                          {staffConflict && (
                                            <span className="block text-[11px] font-bold text-red-600 mt-1 flex items-center gap-1">
                                              <AlertCircle className="w-3 h-3 text-red-500 shrink-0 inline" />
                                              Trùng thời gian ({staffConflict.time} - {staffConflict.title})
                                            </span>
                                          )}
                                        </div>
                                        {assignedPerson === staff.name && (
                                          <CheckSquare className="w-4 h-4 text-[#004c91]" />
                                        )}
                                      </button>
                                    );
                                  })}
                                </div>
                              </div>
                            )}
                          </div>
                        )}

                      </div>
                    )}

                  </div>
                )}

                {(activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' || activePopoverEvent.itemType === 'REQUEST') && (
                  <div className="bg-white rounded-2xl p-6 md:p-8 font-sans w-full space-y-6 relative overflow-visible">
                    {!isProposing ? (
                      <>
                        {/* BENTO GRID (Người gửi, Thời gian gửi, Đoàn khách, Tiêu đề/Số lượng, Thời gian sử dụng) */}
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">

                          <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                            <div className="flex items-center gap-2 text-gray-400 mb-2">
                              <User className="w-4 h-4" />
                              <span className="text-[11px] font-bold uppercase tracking-wider">Người gửi</span>
                            </div>
                            <div className="text-sm font-black text-[#004c91]">{activePopoverEvent.host}</div>
                          </div>

                          <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                            <div className="flex items-center gap-2 text-gray-400 mb-2">
                              <Clock className="w-4 h-4" />
                              <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian gửi</span>
                            </div>
                            <div className="text-sm font-black text-[#004c91]">{formatDateTimeDisplay(activeEventDetail?.requestedAt)}</div>
                          </div>

                          {activePopoverEvent.guests && (
                            <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default flex flex-col justify-center">
                              <div className="flex items-center gap-2 text-gray-400 mb-2">
                                <Users className="w-4 h-4" />
                                <span className="text-[11px] font-bold uppercase tracking-wider">Đoàn khách</span>
                              </div>
                              <div className="text-base font-black text-[#004c91] border-l-4 border-[#f37021] pl-3 py-1 bg-transparent leading-none flex items-center uppercase">
                                {activePopoverEvent.guests}
                              </div>
                            </div>
                          )}

                          {((activeEventDetail?.title || activePopoverEvent?.title) || activeEventDetail?.quantity != null) && (
                            <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default flex items-center gap-8">
                              {(activeEventDetail?.title || activePopoverEvent?.title) && (
                                <div>
                                  <div className="flex items-center gap-2 text-gray-400 mb-2">
                                    <FileText className="w-4 h-4" />
                                    <span className="text-[11px] font-bold uppercase tracking-wider">Tiêu đề</span>
                                  </div>
                                  <div className="text-sm font-black text-[#004c91]">{activeEventDetail?.title || activePopoverEvent?.title}</div>
                                </div>
                              )}
                              {activeEventDetail?.quantity != null && (
                                <div>
                                  <div className="flex items-center gap-2 text-gray-400 mb-2">
                                    <FileText className="w-4 h-4" />
                                    <span className="text-[11px] font-bold uppercase tracking-wider">Số lượng</span>
                                  </div>
                                  <div className="text-sm font-black text-[#004c91]">{finalQuantityDisplay}</div>
                                </div>
                              )}
                            </div>
                          )}

                          <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default relative overflow-hidden flex flex-col justify-center">
                            <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                              <Calendar className="w-4 h-4" />
                              <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng</span>
                            </div>
                            <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center flex-wrap gap-2 sm:gap-3">
                              {isMultiDay ? (
                                <>
                                  <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{formatDateTimeDisplay(activeEventDetail?.usageStartAt)}</span>
                                  <ChevronRight className="w-4 h-4 text-gray-400" />
                                  <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{formatDateTimeDisplay(activeEventDetail?.usageEndAt)}</span>
                                </>
                              ) : (
                                <>
                                  <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[0]?.trim()}</span>
                                  <ChevronRight className="w-4 h-4 text-gray-400" />
                                  <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[1]?.trim()}</span>
                                  <span className="text-sm text-[#004c91] ml-2 font-black">{activePopoverEvent.date.split('-').reverse().join('-')}</span>
                                </>
                              )}
                            </div>
                            <div className="absolute right-0 top-1/2 -translate-y-1/2 opacity-[0.02] pointer-events-none scale-150 mr-4">
                              <Calendar className="w-24 h-24 text-gray-900" />
                            </div>
                          </div>

                        </div>

                        <div className="flex flex-col gap-3 pt-4 transition-all cursor-default relative z-10">
                          <div className="flex items-center gap-2 text-gray-400">
                            <FileText className="w-4 h-4" />
                            <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc</span>
                          </div>
                          <div className="p-6 bg-[#f8fafc] rounded-2xl text-[15px] font-medium text-gray-700 leading-relaxed border border-gray-200 transition-all relative">
                            {typeof activePopoverEvent.purpose === 'string' && activePopoverEvent.purpose.split('\n').map((line, idx) => (
                              <p key={idx} className={idx > 0 && line.startsWith('*') ? 'mt-4 font-bold text-gray-900 border-l-2 border-[#004c91] pl-3 py-1 bg-blue-50/50' : 'mb-2'}>
                                {line}
                              </p>
                            ))}
                          </div>
                        </div>

                        {(requestStatus === 'pending' || requestStatus === 'awaiting-reassign') && !proposalSubmitted && (
                          <div className="flex justify-end pt-2">
                            <button
                              onClick={handleOpenProposal}
                              disabled={!!assignedPerson}
                              className={`px-5 py-2.5 rounded-xl border border-orange-200 text-[#f37021] bg-orange-50 font-bold text-xs flex items-center gap-2 transition-colors ${(!!assignedPerson) ? 'opacity-50 cursor-not-allowed' : 'hover:bg-orange-100'}`}>
                              <Edit2 className="w-4 h-4" />
                              Đề xuất thay đổi
                            </button>
                          </div>
                        )}

                        {proposalSubmitted && (
                          <div className="mt-4 animate-fade-in-quick">
                            <div className="bg-[#de703b] text-white rounded-2xl p-5 flex flex-col items-center justify-center text-center shadow-md border border-[#c9602c]">
                              <div className="flex items-center gap-2.5 mb-2.5">
                                <Clock className="w-5 h-5" />
                                <span className="font-extrabold text-sm uppercase tracking-wider">Chờ xác nhận (Đề xuất thay đổi)</span>
                              </div>
                              <div className="bg-black/15 px-4 py-1.5 rounded-full inline-block">
                                <span className="text-white/95 text-xs font-medium">
                                  bởi: {activeEventDetail?.proposedByName || user?.name || 'Người xử lý'}
                                  {activeEventDetail?.proposedByRole ? ` - ${activeEventDetail.proposedByRole}` : ''}
                                  {' - '}
                                  {activeEventDetail?.proposedAt ? formatDateTime(activeEventDetail.proposedAt) : formatVietnamDateTime(new Date())}
                                </span>
                                <div className="mt-2 space-y-1 text-white/95 text-xs">
                                  {activeEventDetail?.proposedQuantity != null && (
                                    <p>Đề xuất số lượng: {activeEventDetail.proposedQuantity}</p>
                                  )}
                                  {activeEventDetail?.proposedUsageStartAt && activeEventDetail?.proposedUsageEndAt && (
                                    <p>Đề xuất giờ: {formatDateTime(activeEventDetail.proposedUsageStartAt)} - {formatDateTime(activeEventDetail.proposedUsageEndAt)}</p>
                                  )}
                                  {activeEventDetail?.proposedDescription && (
                                    <p>Nội dung đề xuất: {activeEventDetail.proposedDescription}</p>
                                  )}
                                  <p>Lý do: {activeEventDetail?.proposalNote || proposalNote}</p>
                                </div>
                              </div>
                            </div>
                          </div>
                        )}
                      </>
                    ) : (
                      /* Đang đề xuất thay đổi: tách 2 khối — trái = đề xuất mượn của Host (đối chiếu), phải = đề xuất thay đổi của mình */
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 items-start">

                        <div className="rounded-xl border border-gray-200 bg-gray-50/60 p-4">
                          <h4 className="text-[11px] font-black uppercase tracking-wider text-gray-500 flex items-center gap-1.5 pb-2 mb-1 border-b border-gray-200">
                            <User className="w-3.5 h-3.5" /> Đề xuất mượn của Host
                          </h4>
                          <InfoLine icon={User} label="Người gửi" value={activePopoverEvent.host} />
                          <InfoLine icon={Clock} label="Thời gian gửi" value={formatDateTimeDisplay(activeEventDetail?.requestedAt)} />
                          <InfoLine icon={Users} label="Đoàn khách" value={activePopoverEvent.guests} emphasize />
                          <InfoLine icon={FileText} label="Tiêu đề" value={activeEventDetail?.title || activePopoverEvent?.title} />
                          <InfoLine icon={FileText} label="Số lượng" value={finalQuantityDisplay} />
                          <InfoLine
                            icon={Calendar}
                            label="Thời gian sử dụng"
                            value={isMultiDay
                              ? `${formatDateTimeDisplay(activeEventDetail?.usageStartAt)} - ${formatDateTimeDisplay(activeEventDetail?.usageEndAt)}`
                              : `${activePopoverEvent.time || ''}${activePopoverEvent.date ? ` · ${activePopoverEvent.date.split('-').reverse().join('-')}` : ''}`}
                          />
                          {typeof activePopoverEvent.purpose === 'string' && activePopoverEvent.purpose.trim() && (
                            <div className="flex items-start gap-2 py-1 pt-2 mt-1 border-t border-gray-200">
                              <FileText className="w-3.5 h-3.5 text-gray-400 mt-0.5 shrink-0" />
                              <div className="min-w-0 flex-1">
                                <span className="text-[10px] font-bold uppercase tracking-wider text-gray-400 block leading-none mb-1">Nội dung chi tiết công việc</span>
                                {activePopoverEvent.purpose.split('\n').map((line: string, idx: number) => (
                                  <p key={idx} className={`text-sm text-gray-700 leading-relaxed ${idx > 0 && line.startsWith('*') ? 'mt-2 font-bold text-gray-900' : ''}`}>
                                    {line}
                                  </p>
                                ))}
                              </div>
                            </div>
                          )}
                        </div>

                        <div className="rounded-xl border border-orange-200 bg-orange-50/40 p-4">
                          <h4 className="text-[11px] font-black uppercase tracking-wider text-[#de703b] flex items-center gap-1.5 pb-2 mb-1 border-b border-orange-200">
                            <Edit2 className="w-3.5 h-3.5" /> Đề xuất thay đổi của tôi
                          </h4>

                          <div className="flex flex-col gap-2.5 animate-fade-in-quick">
                            <div>
                              <label className="text-[10px] font-bold uppercase tracking-wider text-[#de703b]/80 block mb-1">Số lượng mới</label>
                              <input
                                type="number"
                                min={1}
                                max={activeEventDetail?.quantity != null ? activeEventDetail.quantity - 1 : undefined}
                                step={1}
                                value={proposalQuantity}
                                onChange={(e) => setProposalQuantity(e.target.value)}
                                className={`w-full text-sm px-3 py-2 border rounded-lg outline-none bg-white font-bold text-slate-800 ${quantityTooHigh ? 'border-red-400 focus:border-red-500 ring-1 ring-red-200' : 'border-orange-200 focus:border-orange-500 focus:ring-1 focus:ring-orange-200'}`}
                              />
                              {quantityTooHigh && (
                                <p className="mt-1 text-[11px] font-semibold text-red-600">Số lượng đề xuất phải nhỏ hơn số lượng dự kiến ({activeEventDetail.quantity}).</p>
                              )}
                            </div>

                            <div>
                              <label className="text-[10px] font-bold uppercase tracking-wider text-[#de703b]/80 block mb-1">Thời gian sử dụng mới</label>
                              <div className="flex items-center gap-2">
                                <input
                                  type={isMultiDay ? 'datetime-local' : 'time'}
                                  value={proposalStartTime}
                                  onChange={(e) => setProposalStartTime(e.target.value)}
                                  className="w-full text-sm px-3 py-2 border border-orange-200 rounded-lg focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none bg-white font-bold text-slate-800"
                                />
                                <span className="text-[#de703b] font-black shrink-0">-</span>
                                <input
                                  type={isMultiDay ? 'datetime-local' : 'time'}
                                  value={proposalEndTime}
                                  onChange={(e) => setProposalEndTime(e.target.value)}
                                  className="w-full text-sm px-3 py-2 border border-orange-200 rounded-lg focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none bg-white font-bold text-slate-800"
                                />
                              </div>
                            </div>

                            <div>
                              <label className="text-[10px] font-bold uppercase tracking-wider text-[#de703b]/80 block mb-1">Nội dung chi tiết công việc (đề xuất)</label>
                              <textarea
                                ref={(el) => {
                                  if (el) {
                                    el.style.height = 'auto';
                                    el.style.height = `${Math.max(38, el.scrollHeight)}px`;
                                  }
                                }}
                                rows={1}
                                className="w-full text-sm px-3 py-2 border border-orange-200 rounded-lg focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none resize-none overflow-hidden bg-white font-medium text-slate-800 placeholder:font-normal placeholder:text-gray-400"
                                placeholder="Nhập nội dung công việc đề xuất (nếu có thay đổi)..."
                                value={proposalContent}
                                onChange={(e) => {
                                  setProposalContent(e.target.value);
                                  e.target.style.height = 'auto';
                                  e.target.style.height = `${Math.max(38, e.target.scrollHeight)}px`;
                                }}
                              />
                            </div>

                            <div>
                              <label className="text-[10px] font-bold uppercase tracking-wider text-[#de703b]/80 block mb-1">Lý do đề xuất *</label>
                              <textarea
                                ref={(el) => {
                                  if (el) {
                                    el.style.height = 'auto';
                                    el.style.height = `${Math.max(38, el.scrollHeight)}px`;
                                  }
                                }}
                                rows={1}
                                className="w-full text-sm px-3 py-2 border border-orange-200 rounded-lg focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none resize-none overflow-hidden bg-white font-medium text-slate-800 placeholder:font-normal placeholder:text-gray-400"
                                placeholder="Lý do đề xuất thay đổi..."
                                value={proposalNote}
                                onChange={(e) => {
                                  setProposalNote(e.target.value);
                                  e.target.style.height = 'auto';
                                  e.target.style.height = `${Math.max(38, e.target.scrollHeight)}px`;
                                }}
                                autoFocus
                              />
                            </div>

                            <div className="flex justify-end gap-2 pt-1">
                              <button
                                onClick={() => {
                                  setIsProposing(false);
                                  setProposalContent('');
                                  setProposalNote('');
                                  setProposalStartTime('');
                                  setProposalEndTime('');
                                  setProposalQuantity('');
                                }}
                                className="px-4 py-2 rounded-lg text-gray-500 hover:bg-gray-100 font-bold text-xs"
                              >
                                Hủy
                              </button>
                              <button
                                onClick={async () => {
                                  if (proposalSubmitting) return; // chặn double-submit khi bấm liên tục
                                  try {
                                    if (activePopoverEvent?.rawId) {
                                      if (proposalStartTime && proposalEndTime && proposalStartTime >= proposalEndTime) {
                                        toast.error('Thời gian kết thúc phải sau thời gian bắt đầu');
                                        return;
                                      }
                                      if (!proposalNote.trim()) {
                                        toast.error('Vui lòng nhập lý do đề xuất.');
                                        return;
                                      }
                                      const qty = proposalQuantity.trim() ? Number(proposalQuantity) : null;
                                      if (qty != null && (!Number.isInteger(qty) || qty < 1)) {
                                        toast.error('Số lượng đề xuất phải là số nguyên ≥ 1');
                                        return;
                                      }
                                      if (qty != null && activeEventDetail?.quantity != null && qty >= activeEventDetail.quantity) {
                                        toast.error(`Số lượng đề xuất phải nhỏ hơn số lượng dự kiến (${activeEventDetail.quantity})`);
                                        return;
                                      }
                                      setProposalSubmitting(true);
                                      await departmentReceptionTasksApi.proposeChange(activePopoverEvent.rawId, {
                                        proposedQuantity: qty,
                                        proposedUsageStartAt: isMultiDay ? `${proposalStartTime}:00` : buildProposalDateTime(proposalStartTime),
                                        proposedUsageEndAt: isMultiDay ? `${proposalEndTime}:00` : buildProposalDateTime(proposalEndTime),
                                        proposedDescription: proposalContent.trim() || undefined,
                                        proposalNote: proposalNote.trim(),
                                      });
                                      toast.success('Đã gửi đề xuất thay đổi');
                                      setIsProposing(false);
                                      setProposalSubmitted(true);
                                      setProposalContent('');
                                      setProposalStartTime('');
                                      setProposalEndTime('');
                                      setProposalQuantity('');
                                      await refetchAfterTaskAction();
                                      const detail = await departmentReceptionTasksApi.getRequestDetail(activePopoverEvent.rawId);
                                      setActiveEventDetail(detail);
                                    }
                                  } catch (e: any) {
                                    toast.error(e.response?.data?.message || e.response?.data?.title || e.message || 'Gửi đề xuất thất bại');
                                  } finally {
                                    setProposalSubmitting(false);
                                  }
                                }}
                                disabled={proposalSubmitting || !proposalNote.trim() || !proposalStartTime || !proposalEndTime || quantityTooHigh}
                                className="px-4 py-2 rounded-lg bg-[#de703b] text-white hover:bg-[#c9602c] font-bold text-xs disabled:opacity-50"
                              >
                                {proposalSubmitting ? 'Đang gửi...' : 'Gửi đề xuất'}
                              </button>
                            </div>
                          </div>
                        </div>

                      </div>
                    )}

                    {requestStatus === 'rejecting' && (
                      <div className="mt-4 pt-4 border-t border-red-100 animate-fade-in-quick">
                        <div className="flex items-center gap-2 text-red-600 mb-2">
                          <Info className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Lý do từ chối</span>
                        </div>
                        <textarea
                          rows={3}
                          className="w-full text-sm p-4 border border-red-200 rounded-xl focus:border-red-500 focus:ring-1 focus:ring-red-200 outline-none resize-none"
                          placeholder="Nhập lý do không thể tiếp nhận..."
                          value={requestRejectReason}
                          onChange={(e) => setRequestRejectReason(e.target.value)}
                          autoFocus
                        />
                        <div className="flex justify-end gap-3 mt-3">
                          <button
                            onClick={() => {
                              setRequestStatus('pending');
                              setRequestRejectReason('');
                            }}
                            className="px-5 py-2 rounded-xl text-gray-500 hover:bg-gray-100 font-bold text-xs"
                          >
                            Hủy
                          </button>
                          <button
                            onClick={async () => {
                              try {
                                if (activePopoverEvent?.rawId) {
                                  await departmentReceptionTasksApi.rejectRequest(activePopoverEvent.rawId, requestRejectReason);
                                  toast.success('Đã từ chối nhiệm vụ');
                                  setRequestStatus('rejected');
                                  const now = toVietnamCalendarDate(new Date())!;
                                  const timeStr = `${String(now.getUTCDate()).padStart(2, '0')}/${String(now.getUTCMonth() + 1).padStart(2, '0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2, '0')}:${String(now.getUTCMinutes()).padStart(2, '0')}`;
                                  setRequestRejectSignature({ name: (user?.name || 'Khách') + (isDeptLeader ? ' - Trưởng phòng' : ' - Nhân viên'), time: timeStr });
                                  await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
                                }
                              } catch (e) { console.error(e); toast.error('Từ chối thất bại'); }
                            }}
                            disabled={!requestRejectReason.trim()}
                            className="px-5 py-2 rounded-xl bg-red-600 text-white hover:bg-red-700 font-bold text-xs disabled:opacity-50"
                          >
                            Gửi phản hồi
                          </button>
                        </div>
                      </div>
                    )}

                    {requestStatus === 'rejected' && (
                      <div className="animate-fade-in-quick pt-4">
                        <div className="p-4 rounded-2xl border border-red-200 bg-red-50 flex flex-col gap-3 relative">
                          <div className="flex items-start gap-3">
                            <Info className="w-5 h-5 text-red-600 shrink-0 mt-0.5" />
                            <div>
                              <span className="text-red-800 font-bold text-sm block mb-1">Đã từ chối nhiệm vụ</span>
                              <span className="text-red-600/80 text-xs italic">"{requestRejectReason}"</span>
                            </div>
                          </div>
                          {requestRejectSignature && (
                            <div className="bg-red-100/50 px-3 py-1.5 rounded-lg inline-block self-start sm:ml-8">
                              <span className="text-red-800 text-[11px] font-medium flex flex-col sm:flex-row sm:items-center sm:gap-1">
                                <span>bởi: <span className="font-bold">{requestRejectSignature.name}</span></span>
                                <span className="hidden sm:inline">-</span>
                                <span>{requestRejectSignature.time}</span>
                              </span>
                            </div>
                          )}
                        </div>
                      </div>
                    )}

                    {(requestStatus === 'pending' || requestStatus === 'accepted' || requestStatus === 'assigned' || requestStatus === 'awaiting-reassign') && !isProposing && !proposalSubmitted && (
                      <div className="flex gap-4 pt-6 mt-6 border-t border-gray-100 flex-col relative z-10 w-full animate-fade-in-quick">
                        {isDeptLeader && leaderSelfConflict && requestStatus === 'pending' && (
                          <div className="mb-2 p-3 rounded-xl bg-red-50 border border-red-200 text-red-700 text-xs font-bold flex items-start gap-2 shadow-xs">
                            <AlertCircle className="w-4 h-4 text-red-600 shrink-0 mt-0.5" />
                            <div>
                              <p className="font-black">Đơn/thư này đã trùng thời gian của bạn ({leaderSelfConflict.time}). Hãy phân công cho nhân sự!</p>
                              <p className="text-[11px] font-normal text-red-600 mt-0.5">Trùng với: {leaderSelfConflict.title}</p>
                            </div>
                          </div>
                        )}

                        {/* Nút Xác nhận/Từ chối hiện cho cả REQUESTED (pending) */}
                        {requestStatus === 'pending' && (
                          <div className="flex flex-col sm:flex-row gap-4 w-full">
                            <button
                              onClick={() => setRequestStatus('rejecting')}
                              disabled={!!assignedPerson}
                              className={`flex-1 py-4 px-6 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${(!!assignedPerson)
                                  ? 'border-gray-200 text-gray-400 bg-gray-50 cursor-not-allowed'
                                  : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg active:scale-[0.98] cursor-pointer'
                                }`}>
                              Từ chối
                            </button>
                            <button
                              onClick={async () => {
                                if (isDeptLeader && leaderSelfConflict) {
                                  toast.error(`Đơn/thư này đã trùng thời gian của bạn (${leaderSelfConflict.time}). Hãy phân công cho nhân sự!`);
                                  return;
                                }
                                try {
                                  if (activePopoverEvent?.rawId) {
                                    await departmentReceptionTasksApi.confirmRequest(activePopoverEvent.rawId);
                                    toast.success('Xác nhận nhiệm vụ thành công');
                                    const now = toVietnamCalendarDate(new Date())!;
                                    const timeStr = `${String(now.getUTCDate()).padStart(2, '0')}/${String(now.getUTCMonth() + 1).padStart(2, '0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2, '0')}:${String(now.getUTCMinutes()).padStart(2, '0')}`;
                                    setRequestAcceptSignature({ name: user?.name || 'Khách', time: timeStr });
                                    setRequestStatus('accepted');
                                    await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
                                  }
                                } catch (e) { console.error(e); toast.error('Xác nhận thất bại'); }
                              }}
                              disabled={!!assignedPerson || (isDeptLeader && !!leaderSelfConflict)}
                              className={`flex-1 py-4 px-6 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${(!!assignedPerson || (isDeptLeader && !!leaderSelfConflict))
                                  ? 'bg-gray-100 text-gray-400 border border-gray-200 cursor-not-allowed'
                                  : 'bg-[#004c91] text-white hover:bg-[#003b73] shadow-lg shadow-[#004c91]/20 active:scale-[0.98] border border-blue-600 cursor-pointer'
                                }`}>
                              Xác nhận nhiệm vụ
                            </button>
                          </div>
                        )}

                        {requestStatus === 'accepted' && (
                          <div className="p-4 rounded-2xl border border-green-200 bg-green-50 flex items-start gap-3 relative">
                            <CheckSquare className="w-5 h-5 text-green-600 shrink-0 mt-0.5" />
                            <div className="flex-1">
                              <span className="text-green-800 font-bold text-sm block mb-2">Đã xác nhận nhiệm vụ</span>
                              {requestAcceptSignature && (
                                <div className="bg-green-100/50 px-3 py-1.5 rounded-lg inline-block w-full sm:w-auto mb-2">
                                  <span className="text-green-800 text-[11px] font-medium flex flex-col sm:flex-row sm:items-center sm:gap-1">
                                    <span>bởi: <span className="font-bold">{requestAcceptSignature.name}</span></span>
                                    <span className="hidden sm:inline">-</span>
                                    <span>{requestAcceptSignature.time}</span>
                                  </span>
                                </div>
                              )}
                              <span className="text-green-600/80 text-xs font-medium block">Bên dưới là biên bản bàn giao &amp; nghiệm thu.</span>
                            </div>
                          </div>
                        )}

                        {requestStatus === 'assigned' && (
                          <div className="p-4 rounded-2xl border border-blue-200 bg-blue-50 flex items-start gap-3 relative mb-2">
                            <Info className="w-5 h-5 text-blue-600 shrink-0 mt-0.5" />
                            <div className="flex-1">
                              <span className="text-blue-800 font-bold text-sm block mb-1">Đã phân công người phụ trách</span>
                              <span className="text-blue-600/80 text-xs font-medium block">Đang chờ nhân viên xác nhận: <span className="font-extrabold">{assignedPerson}</span></span>
                            </div>
                          </div>
                        )}

                        {/* DEPT LEADER: Assign / Reassign button */}
                        {isDeptLeader && requestStatus === 'pending' && (

                          <div className="w-full relative mt-2">
                            <button
                              onClick={() => setShowAssignDropdown(!showAssignDropdown)}
                              className="w-full py-3.5 px-6 rounded-2xl bg-slate-100 text-slate-700 font-black uppercase tracking-wider transition-all duration-300 outline-none text-xs text-center flex items-center justify-center gap-2 hover:bg-slate-200 border border-slate-200">
                              <User className="w-4 h-4" />
                              Phân công người phụ trách
                            </button>
                            {showAssignDropdown && (
                              <div className="absolute bottom-full mb-2 left-0 right-0 bg-white border border-slate-200 rounded-xl shadow-[0_-8px_30px_-4px_rgba(0,0,0,0.1)] z-50 overflow-hidden">
                                <div className="py-2">
                                  {filteredCandidates.length === 0 ? (
                                    <div className="px-4 py-3 text-xs text-slate-400 text-center font-medium">Không có nhân sự phòng ban</div>
                                  ) : filteredCandidates.map((staff) => {
                                    const staffConflict = getCandidateConflict(staff.id || staff.userId);
                                    return (
                                      <button
                                        key={staff.id || staff.userId}
                                        disabled={!!staffConflict}
                                        className={`w-full px-4 py-3 text-left border-b border-slate-50 last:border-0 transition-colors group flex items-start justify-between ${
                                          staffConflict ? 'bg-red-50/50 hover:bg-red-50 cursor-not-allowed border-l-4 border-red-500' : 'hover:bg-slate-50 cursor-pointer'
                                        }`}
                                        onClick={async () => {
                                          if (staffConflict) {
                                            toast.error(`Nhân sự ${staff.name} đã bị trùng thời gian (${staffConflict.time} - ${staffConflict.title})!`);
                                            return;
                                          }
                                          if (activePopoverEvent?.rawId) {
                                            await openLogisticsAssignPreview({
                                              logisticsItemId: activePopoverEvent.rawId,
                                              staffId: staff.id || staff.userId,
                                              staffName: staff.name || staff.fullName || 'Nhân sự',
                                              title: activePopoverEvent?.fullTitle || activePopoverEvent?.title,
                                              delegationName: activePopoverEvent?.delegationName,
                                            });
                                          }
                                        }}
                                      >
                                        <div>
                                          <span className="block text-sm font-bold text-slate-800 group-hover:text-[#004c91]">{staff.name}</span>
                                          <span className="block text-xs font-medium text-slate-500 mt-0.5">{staff.email}</span>
                                          {staffConflict && (
                                            <span className="block text-[11px] font-bold text-red-600 mt-1 flex items-center gap-1">
                                              <AlertCircle className="w-3 h-3 text-red-500 shrink-0 inline" />
                                              Trùng thời gian ({staffConflict.time} - {staffConflict.title})
                                            </span>
                                          )}
                                        </div>
                                        {assignedPerson === staff.name && (
                                          <CheckSquare className="w-4 h-4 text-[#004c91]" />
                                        )}
                                      </button>
                                    );
                                  })}
                                </div>
                              </div>
                            )}
                          </div>
                        )}

                        {/* ASSIGNED STAFF: Accept / Decline buttons */}
                        {isDeptStaff && requestStatus === 'assigned' && activeEventDetail?.assigneeId === user?.userId && (
                          <div className="flex flex-col sm:flex-row gap-3 w-full mt-2">
                            <button
                              onClick={async () => {
                                try {
                                  await departmentReceptionTasksApi.declineAssignment(activePopoverEvent.rawId, requestRejectReason || 'Không thể thực hiện nhiệm vụ này');
                                  toast.success('Đã từ chối nhiệm vụ');
                                  setRequestStatus('rejected');
                                  setRequestRejectReason(requestRejectReason || 'Không thể thực hiện nhiệm vụ này');
                                  const now = toVietnamCalendarDate(new Date())!;
                                  const timeStr = `${String(now.getUTCDate()).padStart(2, '0')}/${String(now.getUTCMonth() + 1).padStart(2, '0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2, '0')}:${String(now.getUTCMinutes()).padStart(2, '0')}`;
                                  setRequestRejectSignature({ name: user?.name || 'Nhân viên', time: timeStr });
                                  setAssignedPerson(null);
                                  await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
                                } catch (e: any) { toast.error(e.response?.data?.message || 'Từ chối thất bại'); }
                              }}
                              className="flex-1 py-3.5 px-5 rounded-2xl border-2 border-red-400 text-red-600 font-black uppercase tracking-wider text-xs hover:bg-red-50 transition-all"
                            >
                              Từ chối nhiệm vụ
                            </button>
                            <button
                              onClick={async () => {
                                try {
                                  await departmentReceptionTasksApi.acceptAssignment(activePopoverEvent.rawId);
                                  toast.success('Đã xác nhận nhiệm vụ thành công');
                                  setRequestStatus('accepted');
                                  const now = toVietnamCalendarDate(new Date())!;
                                  const timeStr = `${String(now.getUTCDate()).padStart(2, '0')}/${String(now.getUTCMonth() + 1).padStart(2, '0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2, '0')}:${String(now.getUTCMinutes()).padStart(2, '0')}`;
                                  setRequestAcceptSignature({ name: user?.name || 'Nhân viên', time: timeStr });
                                  await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
                                } catch (e: any) { toast.error(e.response?.data?.message || 'Xác nhận thất bại'); }
                              }}
                              className="flex-1 py-3.5 px-5 rounded-2xl bg-[#004c91] text-white font-black uppercase tracking-wider text-xs hover:bg-[#003b73] shadow-lg transition-all"
                            >
                              Xác nhận nhận việc
                            </button>
                          </div>
                        )}


                      </div>
                    )}

                  </div>
                )}

                {(activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' || activePopoverEvent.itemType === 'REQUEST') && requestStatus === 'accepted' && activeEventDetail && (() => {
                  // Dùng chung TaskHandoverModal với Dept Staff (StaffLeaderTaskModal) thay vì layout
                  // "Safuri" cũ hardcode tên/ngày giả — data thật + mọi fix (checklist, in PDF...) tự
                  // đồng bộ. Dept Leader chỉ xem (không ký) khi việc đã giao cho staff khác.
                  const toPascalSig = (sig: any) => sig?.name ? { Name: sig.name, SignedAt: sig.signedAt } : null;
                  const isAssignedToOther = activeEventDetail.assigneeId && activeEventDetail.assigneeId !== currentUserId;
                  const readOnlyHandover = isDeptLeader && isAssignedToOther;
                  // "Chốt": số lượng đã được Host CHẤP NHẬN đề xuất thay proposedQuantity cho
                  // quantity gốc — biên bản bàn giao phải dùng số này, không phải số dự kiến ban đầu.
                  const finalQuantity = activeEventDetail.proposalResponse === 'ACCEPTED' && activeEventDetail.proposedQuantity != null
                    ? activeEventDetail.proposedQuantity : activeEventDetail.quantity;
                  const handoverDto = {
                    LogisticsItemId: activePopoverEvent.rawId,
                    VisitInstanceId: activeEventDetail.visitInstanceId,
                    Title: activeEventDetail.title,
                    Quantity: finalQuantity,
                    Description: activeEventDetail.description,
                    ItemType: activeEventDetail.itemType,
                    UsageEndTime: activeEventDetail.endTime,
                    UsageDate: activeEventDetail.date,
                    DelegationName: activeEventDetail.delegationName,
                    SenderName: activeEventDetail.senderName,
                    AssigneeName: activeEventDetail.assigneeName,
                    BorrowNote: activeEventDetail.borrowNote,
                    ReturnNote: activeEventDetail.returnNote,
                    ChecklistJson: activeEventDetail.checklistJson,
                    BorrowProviderSignature: toPascalSig(activeEventDetail.borrowProviderSignature),
                    BorrowBorrowerSignature: toPascalSig(activeEventDetail.borrowBorrowerSignature),
                    ReturnBorrowerSignature: toPascalSig(activeEventDetail.returnBorrowerSignature),
                    ReturnProviderSignature: toPascalSig(activeEventDetail.returnProviderSignature),
                  };
                  return (
                    <TaskHandoverModal
                      inline
                      detailData={handoverDto}
                      onSuccess={refreshActiveEventDetail}
                      readOnly={readOnlyHandover}
                    />
                  );
                })()}

                {activePopoverEvent.category === 'Lịch của tôi' && activePopoverEvent.itemType !== 'INVITATION' && activePopoverEvent.itemType !== 'REQUEST' && (
                  <div className="bg-white rounded-2xl p-6 md:p-8 font-sans w-full space-y-6 relative overflow-visible">

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">

                      <div className="p-4 bg-purple-50/50 rounded-2xl border border-purple-100 cursor-default">
                        <div className="flex items-center gap-2 text-purple-600 mb-2">
                          <User className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Chủ tọa</span>
                        </div>
                        <div className="text-sm font-black text-purple-900">{activePopoverEvent.host}</div>
                      </div>

                      <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                        <div className="flex items-center gap-2 text-gray-400 mb-2">
                          <MapPin className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Địa điểm</span>
                        </div>
                        <div className="text-sm font-black text-slate-800">{activePopoverEvent.location || 'Chưa cập nhật'}</div>
                      </div>

                      <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default relative overflow-hidden flex flex-col justify-center">
                        <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                          <Calendar className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian</span>
                        </div>
                        <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center flex-wrap gap-2 sm:gap-3">
                          <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-purple-700">{activePopoverEvent.time?.split('-')[0]?.trim()}</span>
                          <ChevronRight className="w-4 h-4 text-gray-400" />
                          <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-purple-700">{activePopoverEvent.time?.split('-')[1]?.trim()}</span>
                          <span className="text-purple-800 font-bold ml-1">{activePopoverEvent.date?.split('-').reverse().join('-')}</span>
                        </div>
                      </div>
                    </div>

                    <div className="flex flex-col gap-3 pt-4 transition-all cursor-default relative z-10">
                      <div className="flex items-center gap-2 text-gray-400">
                        <FileText className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết</span>
                      </div>
                      <div className="text-[15px] font-medium text-gray-700 leading-relaxed transition-all relative">
                        {typeof activePopoverEvent.purpose === 'string' && activePopoverEvent.purpose.split('\n').map((line: string, idx: number) => (
                          <p key={idx} className={'mb-2'}>
                            {line}
                          </p>
                        ))}
                      </div>
                    </div>
                  </div>
                )}

              </div>

              {/* Footer controls inside modal */}
              <div className="bg-slate-50 px-6 py-4 flex justify-end items-center border-t border-slate-200 rounded-b-2xl">
                <div className="flex items-center gap-2">
                  <button
                    onClick={() => setActivePopoverEvent(null)}
                    className="px-5 py-2.5 bg-[#004c91] text-white hover:opacity-90 text-[11px] font-bold rounded-xl transition-colors shadow-3xs"
                  >
                    {activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' && requestStatus === 'accepted' ? 'Đóng biên bản bàn giao & nghiệm thu' : 'Đóng bảng chi tiết'}
                  </button>
                </div>
              </div>

            </div>
          </div>
          </>
        )}

        {/* Editable email preview before assigning a logistics task. */}
        <EmailPreviewModal
          open={assignPreview.open}
          loading={assignPreview.loading}
          sending={assignPreview.sending}
          error={assignPreview.error}
          subject={assignPreview.subject}
          body={assignPreview.body}
          isActionTemplate={assignPreview.isActionTemplate}
          systemActionDescription={assignPreview.systemActionDescription}
          lockedActionBlockHtml={assignPreview.lockedActionBlockHtml}
          canSend
          sendLabel="Gán với nội dung này"
          pushToast={(type, msg) => { if (type === 'error') toast.error(msg); else if (type === 'success') toast.success(msg); else toast(msg); }}
          onSubjectChange={(v) => setAssignPreview((s) => ({ ...s, subject: v }))}
          onBodyChange={(v) => setAssignPreview((s) => ({ ...s, body: v }))}
          onClose={closeAssignPreview}
          onRestore={reloadAssignPreview}
          onSend={confirmLogisticsAssign}
        />

        <NotificationDetailModal item={changeNotifDetail} onClose={() => setChangeNotifDetail(null)} />

      </div>
    </div>
  );
}
