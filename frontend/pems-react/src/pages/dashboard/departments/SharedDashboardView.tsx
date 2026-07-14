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
  Edit2,
  Download
} from 'lucide-react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import toast, { Toaster } from 'react-hot-toast';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import { delegationsApi } from '../../../features/delegations/api/delegationsApi';
import { notificationsApi } from '../../../features/notifications/api/notificationsApi';
import { useNotifications } from '../../../features/notifications/context/NotificationsContext';
import { getNotificationLink, timeAgo } from '../../../features/notifications/components/NotificationBellButton';
import { NotificationDetailModal } from '../../../features/notifications/components/NotificationDetailModal';
import type { NotificationItem } from '../../../features/notifications/types/notification.types';
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
  priority?: string | null;     // LOW | MEDIUM | HIGH | URGENT (REQUEST items only)
  dueAt?: string | null;
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
  const getEventChangeNotifs = React.useCallback((ev: any): NotificationItem[] => {
    if (!ev || ev.itemType === 'PERSONAL') return [];
    return changeNotifs.filter(n => {
      if (n.isRead) return false;
      const rt = (n.relatedType || '').toUpperCase();
      const sameInstance = n.visitInstanceId != null && ev.visitInstanceId != null
        && String(n.visitInstanceId) === String(ev.visitInstanceId);
      const sameRequest = n.visitRequestId != null && ev.visitRequestId != null
        && String(n.visitRequestId) === String(ev.visitRequestId);
      if (ev.itemType === 'REQUEST') {
        // Đích danh đơn hậu cần (VD: host phản hồi đề xuất) hoặc biên bản bàn giao của cùng visit.
        if (rt === 'LOGISTICS_ITEM') return n.relatedId != null && String(n.relatedId) === String(ev.rawId);
        if (rt === 'LOGISTICS_HANDOVER') return sameInstance;
        return false;
      }
      // INVITATION: thay đổi mức người tham gia hoặc mức đoàn khách/visit.
      // Notification cũ có thể chỉ lưu related_id mà bỏ trống cột visit_instance_id/visit_request_id.
      if (rt === 'VISIT_PARTICIPANT') return n.relatedId != null && String(n.relatedId) === String(ev.rawId);
      if (rt === 'VISIT_INSTANCE') return sameInstance || sameRequest
        || (n.relatedId != null && ev.visitInstanceId != null && String(n.relatedId) === String(ev.visitInstanceId));
      if (rt === 'VISIT_REQUEST') return sameInstance || sameRequest
        || (n.relatedId != null && ev.visitRequestId != null && String(n.relatedId) === String(ev.visitRequestId));
      if (rt === 'LOGISTICS_ITEM' || rt === 'LOGISTICS_HANDOVER') return false;
      return sameInstance || sameRequest;
    });
  }, [changeNotifs]);

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
  const [assignmentPriority, setAssignmentPriority] = useState('ALL');
  const [assignmentSortBy, setAssignmentSortBy] = useState<'PRIORITY' | 'DATE'>('PRIORITY');
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
  const [rejectSignature, setRejectSignature] = useState<{name: string, time: string} | null>(null);
  const [acceptSignature, setAcceptSignature] = useState<{name: string, time: string} | null>(null);
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
          dueAt: '—',
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
  const [requestAcceptSignature, setRequestAcceptSignature] = useState<{name: string, time: string} | null>(null);
  const [requestRejectReason, setRequestRejectReason] = useState('');
  const [requestRejectSignature, setRequestRejectSignature] = useState<{name: string, time: string} | null>(null);
  const [isProposing, setIsProposing] = useState(false);
  const [proposalNote, setProposalNote] = useState('');
  const [proposalStartTime, setProposalStartTime] = useState('');
  const [proposalEndTime, setProposalEndTime] = useState('');
  const [proposalSubmitted, setProposalSubmitted] = useState(false);

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
    setSafuriBG1Signed(null);
    setSafuriBG2Signed(null);
    setSafuriNT1Signed(null);
    setSafuriNT2Signed(null);
  }, [activePopoverEvent?.id]);

  // States for interactive handover & acceptance of Safuri event
  const [safuriBG1Signed, setSafuriBG1Signed] = useState<string | null>(null);
  const [safuriBG2Signed, setSafuriBG2Signed] = useState<string | null>(null);
  const [safuriNT1Signed, setSafuriNT1Signed] = useState<string | null>(null);
  const [safuriNT2Signed, setSafuriNT2Signed] = useState<string | null>(null);

  const [safuriBG1Note, setSafuriBG1Note] = useState('Xe sạc đầy pin 100%, có trang bị 10 ô mang thương hiệu FPT.');
  const [safuriBG2Note, setSafuriBG2Note] = useState('Đã kiểm tra xe vận hành êm ái, đầy đủ ô dù.');
  const [safuriNT1Note, setSafuriNT1Note] = useState('Đã nhận lại chìa khóa, xe sạch sẽ.');
  const [safuriNT2Note, setSafuriNT2Note] = useState('Xe trả nguyên trạng, hoàn tất phiên bàn giao.');

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
            setAcceptSignature({ name: detail.responderName || detail.senderName, time: detail.actionTime });
          } else if (detail.status === 'REJECTED' || detail.status === 'DECLINED') {
            setInvitationStatus('rejected');
            setRejectReason(detail.rejectReason || '');
            setRejectSignature({ name: detail.responderName || detail.senderName, time: detail.actionTime });
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
          setSafuriBG1Signed(toHandoverSignatureText(detail.borrowProviderSignature));
          setSafuriBG2Signed(toHandoverSignatureText(detail.borrowBorrowerSignature));
          setSafuriNT1Signed(toHandoverSignatureText(detail.returnProviderSignature));
          setSafuriNT2Signed(toHandoverSignatureText(detail.returnBorrowerSignature));
          if (detail.borrowNote) {
            const notes = parseHandoverNotes(detail.borrowNote);
            setSafuriBG1Note(notes.provider || detail.borrowNote);
            setSafuriBG2Note(notes.borrower || detail.borrowNote);
          }
          if (detail.returnNote) {
            const notes = parseHandoverNotes(detail.returnNote);
            setSafuriNT1Note(notes.provider || detail.returnNote);
            setSafuriNT2Note(notes.borrower || detail.returnNote);
          }

          if (detail.status === 'CANCELLED') {
             setRequestStatus('rejected');
             setRequestRejectReason(`Đơn yêu cầu / thư mời đã bị hủy do đoàn khách hủy.${detail.cancelReason ? ` Lý do: ${detail.cancelReason}` : ''}`);
          } else if (detail.status === 'ASSIGNED') {
             setAssignedPerson(detail.assigneeName);
             setRequestStatus('assigned');
             setRequestAcceptSignature({ name: detail.responderName || detail.assigneeName || detail.senderName, time: detail.actionTime });
          } else if (detail.status === 'ACCEPTED' || detail.status === 'IN_PROGRESS' || detail.status === 'DONE') {
             setAssignedPerson(detail.assigneeName);
             setRequestStatus('accepted');
             setRequestAcceptSignature({ name: detail.responderName || detail.assigneeName || detail.senderName, time: detail.actionTime });
          } else if (detail.status === 'REJECTED' || detail.status === 'DECLINED') {
             setRequestStatus('rejected');
             setRequestRejectReason(detail.rejectReason || '');
             setRequestRejectSignature({ name: detail.responderName || detail.senderName, time: detail.actionTime });
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
             setRequestRejectSignature({ name: detail.responderName || detail.senderName, time: detail.actionTime });
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
           let isProcessed = false;
           const itemStatus = item.itemStatus || item.status;
           const relatedId = item.relatedUserId != null ? String(item.relatedUserId) : null;
           const isMine = relatedId != null && [user?.id, user?.userId, user?.account, user?.user_id]
             .some(v => v != null && String(v) === relatedId);

           if (item.itemType === 'INVITATION') {
              isProcessed = itemStatus !== 'REQUESTED';
              cat = 'Lời mời tham gia';
           } else if (item.itemType === 'REQUEST') {
              isProcessed = itemStatus !== 'REQUESTED';
              cat = 'Đơn yêu cầu mượn đồ';
           } else {
              cat = 'Lịch của tôi';
           }

           if (itemStatus === 'CANCELLED') {
              // Đã hủy: giữ màu theo loại đơn (không tô xám), chữ gạch ngang khi render.
              if (item.itemType === 'INVITATION') {
                 col = 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100';
                 hCol = 'border-emerald-500';
              } else if (item.itemType === 'REQUEST') {
                 col = 'bg-orange-50 text-orange-700 border-orange-300 hover:bg-orange-100';
                 hCol = 'border-orange-500';
              } else {
                 col = 'bg-purple-100 text-purple-800 border-purple-400 hover:bg-purple-200';
                 hCol = 'border-purple-600';
              }
           } else if (isProcessed) {
              if (item.status !== 'ASSIGNED') {
                 cat = 'Lịch của tôi';
              }
              if (isDeptLeader && isMine) {
                 // Đơn Dept Leader phụ trách (đã chấp nhận) → màu tím như "Tôi".
                 col = 'bg-purple-100 text-purple-800 border-purple-400 hover:bg-purple-200';
                 hCol = 'border-purple-600';
              } else {
                 col = 'bg-blue-50 text-blue-700 border-blue-300 hover:bg-blue-100';
                 hCol = 'border-blue-500';
              }
           } else if (item.itemType === 'INVITATION') {
              col = 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100';
              hCol = 'border-emerald-500';
           } else if (item.itemType === 'REQUEST') {
              col = 'bg-orange-50 text-orange-700 border-orange-300 hover:bg-orange-100';
              hCol = 'border-orange-500';
           } else {
              col = 'bg-purple-100 text-purple-800 border-purple-400 hover:bg-purple-200';
              hCol = 'border-purple-600';
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
    } catch(e) { console.error(e); }
  }, [currentYear, isDeptLeader, user?.id, user?.userId, user?.account, user?.user_id]);

  const fetchCandidates = React.useCallback(async () => {
    try {
      if (isDeptLeader || isDeptStaff) {
        const res = await departmentReceptionTasksApi.getAssigneeCandidates();
        if (res) setCandidates(res);
      }
    } catch(e) { console.error(e); }
  }, [isDeptLeader, isDeptStaff]);

  const fetchAssignmentsProgress = React.useCallback(async () => {
    if (!(isDeptLeader || isDeptStaff)) return;
    setAssignmentLoading(true);
    try {
      const params: Record<string, any> = {
        search: assignmentSearch || undefined,
        itemType: assignmentItemType,
        status: assignmentStatus,
        priority: assignmentPriority !== 'ALL' ? assignmentPriority : undefined,
        ownerScope: assignmentOwnerScope,
        fromDate: assignmentFromDate || undefined,
        toDate: assignmentToDate || undefined,
        sortBy: assignmentSortBy === 'PRIORITY' ? 'priority' : 'date',
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
    assignmentPriority,
    assignmentSortBy,
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
    assignmentPriority,
    assignmentSortBy,
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
  const [calendarType, setCalendarType] = useState<'Trong văn phòng' | 'Lịch của tôi'>((isStudent || isVisitor) ? 'Lịch của tôi' : 'Trong văn phòng');
  const [showTypeDropdown, setShowTypeDropdown] = useState(false);

  // Filter events based on type
  const filteredEvents = useMemo(() => {
    if (calendarType === 'Lịch của tôi') {
      if (isStudent || isVisitor) return events;
      return events.filter(e => {
        if (e.category === 'Lịch của tôi') return true;
        const eid = String(e.relatedUserId);
        return eid === String(user?.id) || eid === String(user?.userId) || eid === String(user?.account) || eid === String(user?.user_id);
      });
    }
    return events; // "Trong văn phòng" shows all
  }, [events, calendarType, isStudent, isVisitor]);

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
    } catch(err: any) {
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

  const toHandoverSignatureText = (signature?: { name?: string; signedAt?: string } | null) => {
    if (!signature?.signedAt) return null;
    return `${signature.name || 'Người ký'} - ${formatDateTime(signature.signedAt)}`;
  };

  const parseHandoverNotes = (note?: string) => {
    const result: { borrower?: string; provider?: string } = {};
    if (!note) return result;
    note.split('\n').forEach((line) => {
      if (line.startsWith('Bên nhận:')) result.borrower = line.replace('Bên nhận:', '').trim();
      if (line.startsWith('Bên giao:')) result.provider = line.replace('Bên giao:', '').trim();
    });
    return result;
  };

  const handleSignHandover = async (
    handoverType: 'BORROW' | 'RETURN',
    signerSide: 'BORROWER' | 'PROVIDER',
    note: string,
    setSigned: React.Dispatch<React.SetStateAction<string | null>>,
    successMessage: string
  ) => {
    if (!activePopoverEvent?.rawId) return;
    try {
      const result = await departmentReceptionTasksApi.signHandover(activePopoverEvent.rawId, handoverType, signerSide, note);
      setSigned(`${result.signedByName || user?.name || 'Người ký'} - ${formatDateTime(result.signedAt)}`);
      toast.success(successMessage);
      const detail = await departmentReceptionTasksApi.getRequestDetail(activePopoverEvent.rawId);
      setActiveEventDetail(detail);
      if (result.status === 'IN_PROGRESS' || detail.status === 'IN_PROGRESS') setRequestStatus('accepted');
      if (result.status === 'DONE' || detail.status === 'DONE') setRequestStatus('accepted');
      await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
    } catch (e: any) {
      toast.error(e.response?.data?.message || e.response?.data?.title || e.message || 'Ký biên bản thất bại');
    }
  };

  const getPriorityClass = (priority?: string | null) => {
    switch ((priority || '').toUpperCase()) {
      case 'URGENT': return 'bg-red-50 text-red-700 border-red-200';
      case 'HIGH': return 'bg-amber-50 text-amber-700 border-amber-200';
      case 'LOW': return 'bg-slate-50 text-slate-500 border-slate-200';
      default: return 'bg-sky-50 text-sky-700 border-sky-200'; // MEDIUM
    }
  };
  const getPriorityLabel = (priority?: string | null) => {
    switch ((priority || '').toUpperCase()) {
      case 'URGENT': return 'Khẩn cấp';
      case 'HIGH': return 'Cao';
      case 'LOW': return 'Thấp';
      default: return 'Trung bình';
    }
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

  const handleOpenProposal = () => {
    const current = extractTimeRange();
    setProposalStartTime(current.start);
    setProposalEndTime(current.end);
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
        <div className="flex items-center gap-3 px-6 pt-6 pb-5">
          <div className="w-12 h-12 rounded-2xl bg-blue-50 text-[#004c91] flex items-center justify-center">
            <CheckSquare className="w-6 h-6" />
          </div>
          <h3 className="text-xl md:text-2xl font-black text-[#004c91] uppercase tracking-tight">Nhiệm vụ điều phối & thư mời tham gia</h3>
        </div>

        <div className="bg-[#005594] rounded-t-2xl px-6 py-5 flex flex-wrap items-center justify-between gap-4">
          <input
            value={assignmentSearch}
            onChange={e => setAssignmentSearch(e.target.value)}
            placeholder="Tìm kiếm nhiệm vụ..."
            className="w-full lg:w-[420px] px-4 py-3 bg-white/10 border border-white/10 rounded-xl text-sm font-semibold text-white placeholder:text-white/70 outline-none focus:bg-white/15"
          />
          <div className="flex flex-wrap items-center gap-3">
            <select value={assignmentItemType} onChange={e => setAssignmentItemType(e.target.value)} className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
              <option value="ALL">Tất cả loại</option>
              <option value="INVITATION">Thư mời</option>
              <option value="REQUEST">Đơn yêu cầu</option>
            </select>
            <select value={assignmentStatus} onChange={e => setAssignmentStatus(e.target.value)} className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
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
            <select value={assignmentPriority} onChange={e => setAssignmentPriority(e.target.value)} className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
              <option value="ALL">Mọi mức ưu tiên</option>
              <option value="URGENT">Khẩn cấp</option>
              <option value="HIGH">Cao</option>
              <option value="MEDIUM">Trung bình</option>
              <option value="LOW">Thấp</option>
            </select>
            <select value={assignmentOwnerScope} onChange={e => setAssignmentOwnerScope(e.target.value)} className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
              <option value="DEPARTMENT">Văn phòng</option>
              <option value="ME">Tôi</option>
            </select>
            <div className="flex items-center gap-2">
              <input type="date" value={assignmentFromDate} onChange={e => setAssignmentFromDate(e.target.value)} className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800" />
              <span className="text-white font-black">-</span>
              <input type="date" value={assignmentToDate} onChange={e => setAssignmentToDate(e.target.value)} className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800" />
            </div>
            <select value={assignmentSortBy} onChange={e => setAssignmentSortBy(e.target.value as 'PRIORITY' | 'DATE')} className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-bold text-slate-800">
              <option value="PRIORITY">Sắp xếp: Ưu tiên</option>
              <option value="DATE">Sắp xếp: Thời gian</option>
            </select>
            {assignmentSortBy === 'DATE' && (
              <button
                type="button"
                onClick={() => setAssignmentSortDirection(v => v === 'ASC' ? 'DESC' : 'ASC')}
                className="px-3 py-2.5 bg-white border border-white/20 rounded-xl text-sm font-black text-[#004c91] hover:bg-blue-50"
              >
                {assignmentSortDirection === 'DESC' ? 'Đơn mới nhất' : 'Đơn cũ nhất'}
              </button>
            )}
          </div>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full min-w-[980px] text-left border-x border-b border-slate-100 rounded-b-2xl overflow-hidden">
            <thead className="bg-[#005594] text-white text-[11px] uppercase font-black">
              <tr>
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
                  <td colSpan={6} className="px-4 py-10 text-center text-sm font-semibold text-slate-400">Không có dữ liệu phù hợp</td>
                </tr>
              )}
              {assignmentItems.map(item => (
                <tr key={`${item.itemType}_${item.itemId}`} className="hover:bg-slate-50/80 transition-colors">
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
                      const fmt = (d: Date) => `${String(d.getHours()).padStart(2,'0')}:${String(d.getMinutes()).padStart(2,'0')} ${String(d.getDate()).padStart(2,'0')}/${String(d.getMonth()+1).padStart(2,'0')}/${d.getFullYear()}`;
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
                    {item.priority && (
                      <span className={`mt-1 inline-flex px-2.5 py-1 rounded-full border text-[10px] font-black ${getPriorityClass(item.priority)}`}>
                        {getPriorityLabel(item.priority)}
                      </span>
                    )}
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
      <Toaster position="top-right" />

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
              {candidates.length === 0 ? (
                <div className="px-5 py-8 text-center text-sm font-semibold text-slate-400">Không có nhân sự phù hợp</div>
              ) : candidates.map((staff) => (
                <button
                  key={staff.id || staff.userId}
                  type="button"
                  onClick={() => handleSelectAssignee(staff)}
                  className="w-full px-5 py-3 text-left hover:bg-blue-50 transition-colors flex items-center justify-between gap-3"
                >
                  <div className="min-w-0">
                    <p className="text-sm font-black text-slate-800 truncate">{staff.name}</p>
                    <p className="text-xs font-medium text-slate-500 truncate">{staff.email}</p>
                  </div>
                  <ChevronRight className="w-4 h-4 text-slate-300" />
                </button>
              ))}
            </div>
          </div>
        </div>
      )}


    <div className={viewMode === 'calendar' ? 'bg-white rounded-3xl border border-slate-200/85 shadow-md p-4 sm:p-6 md:p-8 font-sans' : 'font-sans'}>
      
      {/* Shared Header Bar */}
      {viewMode === 'calendar' && (
      <header className="pb-5 border-b border-slate-100 flex flex-wrap items-center justify-between gap-4 mb-6">
        <div>
          <span className="text-[10px] font-bold text-[#f37021] uppercase tracking-widest block mb-0.5">FPT University • PEMS v3.0</span>
          <h1 className="text-xl md:text-2xl font-black text-[#004c91] tracking-tight">
            Lịch chung & theo dõi sự kiện
          </h1>
        </div>

        {/* Legend */}
        <div className="flex flex-wrap items-center gap-4 text-xs font-bold text-slate-600">
          <span className="flex items-center gap-2"><div className="w-3 h-3 rounded-full bg-emerald-50 border-2 border-emerald-400"></div>Thư mời</span>
          <span className="flex items-center gap-2"><div className="w-3 h-3 rounded-full bg-orange-50 border-2 border-orange-400"></div>Đơn yêu cầu</span>
          <span className="flex items-center gap-2"><div className="w-3 h-3 rounded-full bg-blue-50 border-2 border-blue-400"></div>Đã xử lý</span>
          <span className="flex items-center gap-2"><span className="line-through text-slate-500">Bị hủy</span></span>
          <span className="flex items-center gap-2"><div className="w-3 h-3 rounded-sm bg-slate-300/60 border border-slate-300"></div>Ngày đã qua</span>
          <span className="flex items-center gap-2"><div className="w-3 h-3 rounded-full bg-purple-200 border-2 border-purple-500"></div>Tôi</span>
        </div>

        {/* Google-Calendar-style toolbar button group */}
        {viewMode === 'calendar' && (
        <div className="flex items-center gap-4 flex-wrap">
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
                {/* Overlay backdrop to close clicking outside */}
                <div 
                  className="fixed inset-0 z-25" 
                  onClick={() => setShowMiniCalendar(false)} 
                />
                
                {/* Popover Card */}
                <div className="absolute right-0 top-full mt-2 w-[280px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 p-4 animate-fade-in-quick text-slate-800">
                  
                  {/* Miniature header */}
                  <div className="flex items-center justify-between mb-3.5">
                    <span className="text-xs font-extrabold text-slate-700">
                      Tháng {miniMonth + 1} năm {miniYear}
                    </span>
                    <div className="flex items-center gap-1">
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          if (miniMonth === 0) {
                            setMiniMonth(11);
                            setMiniYear(y => y - 1);
                          } else {
                            setMiniMonth(m => m - 1);
                          }
                        }}
                        className="p-1 text-slate-550 hover:bg-slate-50 border border-transparent hover:border-slate-200 rounded-lg hover:shadow-3xs transition-all active:scale-95"
                      >
                        <ChevronLeft className="w-3.5 h-3.5" />
                      </button>
                      <button
                        type="button"
                        onClick={(e) => {
                          e.stopPropagation();
                          if (miniMonth === 11) {
                            setMiniMonth(0);
                            setMiniYear(y => y + 1);
                          } else {
                            setMiniMonth(m => m + 1);
                          }
                        }}
                        className="p-1 text-slate-550 hover:bg-slate-50 border border-transparent hover:border-slate-200 rounded-lg hover:shadow-3xs transition-all active:scale-95"
                      >
                        <ChevronRight className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  </div>

                  {/* Week days labels */}
                  <div className="grid grid-cols-7 text-center text-[10px] font-black text-slate-400 mb-2">
                    <div>CN</div>
                    <div>T2</div>
                    <div>T3</div>
                    <div>T4</div>
                    <div>T5</div>
                    <div>T6</div>
                    <div>T7</div>
                  </div>

                  {/* Days grid */}
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
                            
                            // Find matching event or clear
                            const ev = events.find(e => e.date === cellDateStr);
                            if (ev) {
                              setActivePopoverEvent(ev);
                            } else {
                              setActivePopoverEvent(null);
                            }
                            setShowMiniCalendar(false);
                          }}
                          className={`w-7 h-7 rounded-full flex items-center justify-center font-bold transition-all mx-auto select-none ${
                            isSelected
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

          {/* Display Mode Dropdown "Hiển thị: " -> Ngày, Tuần, Tháng, Năm */}
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
                <div 
                  className="fixed inset-0 z-25" 
                  onClick={() => setShowDisplayDropdown(false)} 
                />
                <div className="absolute right-0 top-full mt-2 w-[150px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 py-2 animate-fade-in-quick text-slate-800">
                  {(['Ngày', 'Tuần', 'Tháng', 'Năm'] as const).map((mode) => (
                    <button
                      key={mode}
                      type="button"
                      onClick={() => {
                        setDisplayMode(mode);
                        setShowDisplayDropdown(false);
                      }}
                      className={`w-full text-left px-5 py-2.5 text-xs font-bold transition-colors block ${
                        displayMode === mode 
                          ? 'bg-slate-50 text-[#004c91]' 
                          : 'text-slate-700 hover:bg-slate-50'
                      }`}
                    >
                      {mode}
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>

          {/* New Calendar Type Dropdown "Loại lịch: " -> Trong văn phòng, Lịch của tôi */}
          <div className="relative">
            <button
              onClick={() => {
                setShowTypeDropdown(!showTypeDropdown);
                setShowDisplayDropdown(false);
                setShowMiniCalendar(false);
              }}
              className="flex items-center justify-between w-[240px] px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-[#004c91] hover:bg-slate-50 transition-colors shadow-3xs"
            >
              <span className="select-none text-left truncate">Loại lịch: {calendarType}</span>
              <ChevronDown className="w-3.5 h-3.5 text-[#004c91]/75 flex-shrink-0 ml-1" />
            </button>

            {showTypeDropdown && (
              <>
                <div 
                  className="fixed inset-0 z-25" 
                  onClick={() => setShowTypeDropdown(false)} 
                />
                <div className="absolute right-0 top-full mt-2 w-[240px] bg-white border border-slate-200 rounded-2xl shadow-xl z-30 py-2 animate-fade-in-quick text-slate-800">
                  {((isStudent || isVisitor) ? ['Lịch của tôi'] : ['Trong văn phòng', 'Lịch của tôi']).map((type) => (
                    <button
                      key={type}
                      type="button"
                      onClick={() => {
                        setCalendarType(type as 'Trong văn phòng' | 'Lịch của tôi');
                        setShowTypeDropdown(false);
                      }}
                      className={`w-full text-left px-5 py-2.5 text-xs font-bold transition-colors block ${
                        calendarType === type 
                          ? 'bg-slate-50 text-[#f37021]' 
                          : 'text-slate-700 hover:bg-slate-50'
                      }`}
                    >
                      {type}
                    </button>
                  ))}
                </div>
              </>
            )}
          </div>
        </div>
        )}
      </header>
      )}

      {viewMode === 'assignments' && renderAssignmentsProgressPanel()}

      {/* Grid of Calendar (Full Width) */}
      {viewMode === 'calendar' && (
      <div className="relative">
        <div className="w-full">
          
           {/* Calendar Container */}
          <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden flex flex-col">
            
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
                <div className="grid grid-cols-7 grid-rows-5 flex-grow min-h-[850px] divide-x divide-y divide-slate-200 border-l border-r border-b border-slate-200 bg-slate-50/20">
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
                        className={`h-[160px] max-h-[160px] overflow-hidden p-2 flex flex-col justify-between transition-colors group relative cursor-pointer ${
                          isSelected
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
                              <span className={`text-xs font-extrabold px-1.5 py-0.5 rounded-md ${
                                cell.dateString === todayStr && cell.isCurrent
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
                                    className={`relative px-2 py-1.5 rounded-lg border text-[10px] font-normal leading-tight cursor-pointer transition-all truncate selection:bg-transparent ${hasChanges ? 'pr-5' : ''} ${ev.color} ${ev.hoverColor} ${
                                      isHighlighted ? 'ring-2 ring-orange-500/10 border-orange-400 shadow-sm' : ''
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
                        className={`p-3.5 flex flex-col justify-between transition-colors group relative cursor-pointer ${
                          isSelected
                            ? 'bg-orange-50 ring-2 ring-inset ring-[#f37021] z-10 shadow-sm'
                            : cell.isCurrent
                              ? 'bg-white hover:bg-orange-50 text-slate-800'
                              : 'bg-slate-50/30 hover:bg-orange-50/60 text-slate-350'
                        }`}
                      >
                        <div className="flex justify-between items-center mb-2">
                          <span className={`text-xs font-extrabold px-2 py-1 rounded-md ${
                            cell.dateString === todayStr && cell.isCurrent
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
                                className={`relative px-2 py-2 rounded-lg border text-[10px] font-normal leading-tight cursor-pointer transition-all ${hasChanges ? 'pr-5' : ''} ${ev.color} ${ev.hoverColor} ${
                                  isHighlighted ? 'ring-2 ring-[#f37021]/30 border-[#f37021] shadow-sm scale-[1.01]' : ''
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
                            className={`p-4 rounded-xl border transition-all cursor-pointer relative ${ev.color} ${ev.hoverColor} ${
                              isHighlighted ? 'ring-2 ring-[#f37021] border-[#f37021] scale-[1.002]' : 'border-slate-100'
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
                                className={`relative w-5 h-5 rounded-full flex items-center justify-center font-bold transition-all mx-auto ${
                                  isSelected
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
                        <span className={`inline-block text-[9px] font-black uppercase px-2 py-0.5 mt-1 rounded ${
                          activePopoverEvent.vipLevel === 'VVIP' 
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
                        className={`p-3 rounded-xl border text-xs cursor-pointer transition-all ${
                          isSelected
                            ? 'bg-orange-50/90 border-[#f37021] ring-1 ring-[#f37021] text-slate-800'
                            : 'bg-slate-50 hover:bg-orange-50/40 hover:border-orange-200 text-slate-700 border-slate-100'
                        }`}
                      >
                        <div className="flex justify-between items-center gap-1.5 mb-2 leading-none">
                          <span className={`text-[9px] font-black uppercase tracking-wider px-2 py-0.5 rounded ${
                            ev.category === 'Lời mời tham gia' 
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
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-50 p-4 overflow-y-auto">
          <div className="bg-white rounded-2xl max-w-5xl w-full border border-slate-200 shadow-2xl overflow-hidden animate-fade-in-quick flex flex-col my-8">
            
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
            <div className="p-6 md:p-8 space-y-4 overflow-y-auto max-h-[70vh] no-scrollbar bg-slate-50/50">
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
                            } catch(e) { console.error(e); toast.error('Gửi phản hồi thất bại'); }
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
                    
                    <div className="flex flex-col sm:flex-row gap-4 w-full">
                      <button 
                        onClick={() => setInvitationStatus('rejecting')}
                        disabled={!!assignedPerson}
                        className={`flex-1 py-4 px-6 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                          (!!assignedPerson)
                            ? 'border-gray-200 text-gray-400 bg-gray-50 cursor-not-allowed' 
                            : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg active:scale-[0.98] cursor-pointer'
                        }`}>
                        Từ chối
                      </button>
                      <button 
                        onClick={async () => {
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
                          } catch(e) { console.error(e); toast.error('Xác nhận thất bại'); }
                        }}
                        disabled={!!assignedPerson}
                        className={`flex-1 py-4 px-6 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                          (!!assignedPerson)
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
                           className={`w-full py-3.5 px-6 rounded-2xl bg-slate-100 text-slate-700 font-black uppercase tracking-wider transition-all duration-300 outline-none text-xs text-center flex items-center justify-center gap-2 ${
                             isDeptStaff && deptPreliminaryStatus !== 'accepted' ? 'opacity-50 cursor-not-allowed border-dashed' : 'hover:bg-slate-200 border border-slate-200'
                           }`}>
                           <User className="w-4 h-4" />
                           {assignedPerson ? `Đã giao: ${assignedPerson}` : 'Ủy quyền / Đổi người phụ trách'}
                         </button>
                         {showAssignDropdown && (
                           <div className="absolute top-full left-0 right-0 mt-2 bg-white border border-slate-200 rounded-xl shadow-[0_8px_30px_-4px_rgba(0,0,0,0.1)] z-50 overflow-hidden">
                             <div className="py-2">
                               {candidates.map((staff) => (
                                 <button
                                   key={staff.id || staff.userId}
                                   className="w-full px-4 py-3 text-left hover:bg-slate-50 border-b border-slate-50 last:border-0 transition-colors group flex items-start justify-between"
                                   onClick={async () => {
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
                                    </div>
                                    {assignedPerson === staff.name && (
                                      <CheckSquare className="w-4 h-4 text-[#004c91]" />
                                    )}
                                 </button>
                               ))}
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
                  
                  {/* BENTO GRID (Người gửi, Thời gian gửi, Đoàn khách, Thời gian sử dụng) */}
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

                    <div className="col-span-1 sm:col-span-2 p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default relative overflow-hidden flex flex-col justify-center">
                      <div className="flex items-center gap-2 text-gray-400 mb-2 relative z-10">
                        <Calendar className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng</span>
                      </div>
                      <div className="text-[15px] font-bold text-gray-800 relative z-10 flex items-center flex-wrap gap-2 sm:gap-3">
                         <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[0]?.trim()}</span>
                         <ChevronRight className="w-4 h-4 text-gray-400" />
                         <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activePopoverEvent.time?.split('-')[1]?.trim()}</span>
                         <span className="text-sm text-[#004c91] ml-2 font-black">{activePopoverEvent.date.split('-').reverse().join('-')}</span>
                      </div>
                      <div className="absolute right-0 top-1/2 -translate-y-1/2 opacity-[0.02] pointer-events-none scale-150 mr-4">
                        <Calendar className="w-24 h-24 text-gray-900" />
                      </div>
                    </div>

                    {/* Mức ưu tiên + Hạn phản hồi (visit_logistics_items.priority / due_at) */}
                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <AlertCircle className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Mức ưu tiên</span>
                      </div>
                      <span className={`inline-flex px-2.5 py-1 rounded-full border text-[11px] font-black ${getPriorityClass(activeEventDetail?.priority)}`}>
                        {getPriorityLabel(activeEventDetail?.priority)}
                      </span>
                    </div>

                    <div className="p-4 bg-gray-50/80 rounded-2xl border border-gray-100 cursor-default">
                      <div className="flex items-center gap-2 text-gray-400 mb-2">
                        <Clock className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Hạn phản hồi</span>
                      </div>
                      <div className="text-sm font-black text-[#004c91]">
                        {activeEventDetail?.dueAt ? formatDateTime(activeEventDetail.dueAt) : 'Chưa có'}
                      </div>
                    </div>

                    {isProposing && !proposalSubmitted && (
                      <div className="col-span-1 sm:col-span-2 p-4 bg-orange-50/50 rounded-2xl border border-orange-200 cursor-default relative overflow-hidden flex flex-col justify-center animate-fade-in-quick mt-[-4px]">
                        <div className="flex items-center gap-2 text-[#de703b] mb-2 relative z-10">
                          <Calendar className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian sử dụng (Đề xuất)</span>
                        </div>
                        <div className="grid grid-cols-1 sm:grid-cols-[1fr_auto_1fr] items-center gap-3">
                          <input
                            type="time"
                            value={proposalStartTime}
                            onChange={(e) => setProposalStartTime(e.target.value)}
                            className="w-full text-sm p-3.5 border border-orange-200 rounded-xl focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none bg-white font-bold text-slate-800"
                          />
                          <span className="text-center text-[#de703b] font-black">-</span>
                          <input
                            type="time"
                            value={proposalEndTime}
                            onChange={(e) => setProposalEndTime(e.target.value)}
                            className="w-full text-sm p-3.5 border border-orange-200 rounded-xl focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none bg-white font-bold text-slate-800"
                          />
                        </div>
                      </div>
                    )}

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

                  {(requestStatus === 'pending' || requestStatus === 'awaiting-reassign') && !isProposing && !proposalSubmitted && (
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

                  {isProposing && (
                    <div className="flex flex-col gap-3 transition-all cursor-default relative z-10 animate-fade-in-quick mt-2">
                       <div className="flex items-center gap-2 text-[#de703b] mt-2">
                           <FileText className="w-4 h-4" />
                           <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc (Đề xuất)</span>
                       </div>
                       <textarea
                         rows={4}
                         className="w-full text-sm p-5 border border-orange-200 rounded-2xl focus:border-orange-500 focus:ring-1 focus:ring-orange-200 outline-none resize-none bg-orange-50/50 font-medium text-slate-800 placeholder:font-normal placeholder:text-gray-400"
                         placeholder="Nhập đề xuất nội dung..."
                         value={proposalNote}
                         onChange={(e) => setProposalNote(e.target.value)}
                         autoFocus
                       />
                       <div className="flex justify-end gap-3 mt-1">
                         <button
                           onClick={() => {
                             setIsProposing(false);
                             setProposalNote('');
                             setProposalStartTime('');
                             setProposalEndTime('');
                           }}
                           className="px-5 py-2.5 rounded-xl text-gray-500 hover:bg-gray-100 font-bold text-xs"
                         >
                           Hủy
                         </button>
                         <button
                           onClick={async () => {
                             try {
                               if (activePopoverEvent?.rawId) {
                                 if (proposalStartTime && proposalEndTime && proposalStartTime >= proposalEndTime) {
                                   toast.error('Giờ kết thúc phải sau giờ bắt đầu');
                                   return;
                                 }
                                 if (!proposalNote.trim()) {
                                   toast.error('Vui lòng nhập lý do/ghi chú đề xuất.');
                                   return;
                                 }
                                 await departmentReceptionTasksApi.proposeChange(activePopoverEvent.rawId, {
                                   proposedUsageStartAt: buildProposalDateTime(proposalStartTime),
                                   proposedUsageEndAt: buildProposalDateTime(proposalEndTime),
                                   proposalNote: proposalNote.trim(),
                                 });
                                 toast.success('Đã gửi đề xuất thay đổi');
                                 setIsProposing(false);
                                 setProposalSubmitted(true);
                                 setProposalStartTime('');
                                 setProposalEndTime('');
                                 await refetchAfterTaskAction();
                                 const detail = await departmentReceptionTasksApi.getRequestDetail(activePopoverEvent.rawId);
                                 setActiveEventDetail(detail);
                               }
                             } catch (e: any) {
                               toast.error(e.response?.data?.message || e.response?.data?.title || e.message || 'Gửi đề xuất thất bại');
                             }
                           }}
                           disabled={!proposalNote.trim() || !proposalStartTime || !proposalEndTime}
                           className="px-5 py-2.5 rounded-xl bg-[#de703b] text-white hover:bg-[#c9602c] font-bold text-xs disabled:opacity-50"
                         >
                           Gửi đề xuất
                         </button>
                       </div>
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
                               {activeEventDetail?.proposedUsageStartAt && activeEventDetail?.proposedUsageEndAt && (
                                 <p>Đề xuất giờ: {formatDateTime(activeEventDetail.proposedUsageStartAt)} - {formatDateTime(activeEventDetail.proposedUsageEndAt)}</p>
                               )}
                               <p>Ghi chú: {activeEventDetail?.proposedDescription || proposalNote}</p>
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
                            } catch(e) { console.error(e); toast.error('Từ chối thất bại'); }
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
                      {/* Nút Xác nhận/Từ chối hiện cho cả REQUESTED (pending) */}
                      {requestStatus === 'pending' && (
                        <div className="flex flex-col sm:flex-row gap-4 w-full">
                          <button 
                            onClick={() => setRequestStatus('rejecting')}
                            disabled={!!assignedPerson}
                            className={`flex-1 py-4 px-6 rounded-2xl border-2 font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                              (!!assignedPerson)
                                ? 'border-gray-200 text-gray-400 bg-gray-50 cursor-not-allowed' 
                                : 'border-[#f37021] text-[#f37021] hover:bg-[#f37021] hover:text-white hover:shadow-lg active:scale-[0.98] cursor-pointer'
                            }`}>
                            Từ chối
                          </button>
                          <button 
                            onClick={async () => {
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
                              } catch(e) { console.error(e); toast.error('Xác nhận thất bại'); }
                            }}
                            disabled={!!assignedPerson}
                            className={`flex-1 py-4 px-6 rounded-2xl font-black uppercase tracking-wider transition-all duration-300 outline-none text-sm text-center ${
                              (!!assignedPerson)
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
                                {candidates.map((staff) => (
                                  <button
                                    key={staff.id || staff.userId}
                                    className="w-full px-4 py-3 text-left hover:bg-slate-50 border-b border-slate-50 last:border-0 transition-colors group flex items-start justify-between"
                                    onClick={async () => {
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
                                    </div>
                                    {assignedPerson === staff.name && (
                                      <CheckSquare className="w-4 h-4 text-[#004c91]" />
                                    )}
                                  </button>
                                ))}
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
                                  const timeStr = `${String(now.getUTCDate()).padStart(2,'0')}/${String(now.getUTCMonth()+1).padStart(2,'0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2,'0')}:${String(now.getUTCMinutes()).padStart(2,'0')}`;
                                  setRequestRejectSignature({ name: user?.name || 'Nhân viên', time: timeStr });
                                  setAssignedPerson(null);
                                  await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
                                } catch(e: any) { toast.error(e.response?.data?.message || 'Từ chối thất bại'); }
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
                                  const timeStr = `${String(now.getUTCDate()).padStart(2,'0')}/${String(now.getUTCMonth()+1).padStart(2,'0')}/${now.getUTCFullYear()}, ${String(now.getUTCHours()).padStart(2,'0')}:${String(now.getUTCMinutes()).padStart(2,'0')}`;
                                  setRequestAcceptSignature({ name: user?.name || 'Nhân viên', time: timeStr });
                                  await Promise.all([fetchCalendarEvents(), fetchAssignmentsProgress()]);
                                } catch(e: any) { toast.error(e.response?.data?.message || 'Xác nhận thất bại'); }
                              }}
                              className="flex-1 py-3.5 px-5 rounded-2xl bg-[#004c91] text-white font-black uppercase tracking-wider text-xs hover:bg-[#003b73] shadow-lg transition-all"
                            >
                              Xác nhận nhận việc
                            </button>
                          </div>
                        )}

                        {/* Assignment History */}
                        {activeEventDetail?.assignmentHistory?.length > 0 && (
                          <div className="mt-4 pt-4 border-t border-slate-100">
                            <div className="flex items-center gap-2 text-slate-400 mb-3">
                              <Clock className="w-4 h-4" />
                              <span className="text-[11px] font-bold uppercase tracking-wider">Lịch sử phân công</span>
                            </div>
                            <div className="flex flex-col gap-2">
                              {activeEventDetail.assignmentHistory.map((att: any) => (
                                <div key={att.attemptId} className={`flex items-start gap-3 p-3 rounded-xl border ${
                                  att.status === 'DECLINED' || att.status === 'REJECTED' ? 'bg-red-50 border-red-100' :
                                  att.status === 'ACCEPTED' ? 'bg-green-50 border-green-100' :
                                  'bg-slate-50 border-slate-100'
                                }`}>
                                  <div className={`w-2 h-2 rounded-full mt-1.5 shrink-0 ${
                                    att.status === 'DECLINED' || att.status === 'REJECTED' ? 'bg-red-400' :
                                    att.status === 'ACCEPTED' ? 'bg-green-500' :
                                    'bg-amber-400'
                                  }`} />
                                  <div className="flex-1 min-w-0">
                                    <span className="block text-sm font-bold text-slate-800">{att.assigneeName}</span>
                                    <span className={`text-[11px] font-semibold ${
                                      att.status === 'DECLINED' || att.status === 'REJECTED' ? 'text-red-600' :
                                      att.status === 'ACCEPTED' ? 'text-green-600' :
                                      'text-amber-600'
                                    }`}>
                                      {att.status === 'DECLINED' || att.status === 'REJECTED' ? 'Đã từ chối' : att.status === 'ACCEPTED' ? 'Đã nhận' : 'Đang chờ phản hồi'}
                                    </span>
                                    <span className="block text-[10px] text-slate-400 font-mono mt-0.5">{att.assignedAt}</span>
                                    {att.responseNote && (
                                      <span className="block text-xs text-red-600 italic mt-1">Lý do: "{att.responseNote}"</span>
                                    )}
                                  </div>
                                </div>
                              ))}
                            </div>
                          </div>
                        )}
                    </div>
                  )}

                </div>
              )}

              {(activePopoverEvent.category === 'Đơn yêu cầu mượn đồ' || activePopoverEvent.itemType === 'REQUEST') && requestStatus === 'accepted' && (
                <>
                  <style type="text/css" media="print">
                    {`
                      body * {
                        visibility: hidden;
                      }
                      #safuri-handover-layout, #safuri-handover-layout * {
                        visibility: visible;
                      }
                      #safuri-handover-layout {
                        position: absolute;
                        left: 0;
                        top: 0;
                        width: 100%;
                        margin: 0;
                        padding: 0;
                        overflow: visible !important;
                        border: none !important;
                        box-shadow: none !important;
                      }
                    `}
                  </style>
                  {/* Safuri Event Layout */}
                  <div id="safuri-handover-layout" className="bg-white rounded-2xl p-6 md:p-10 font-sans w-full space-y-6 relative overflow-hidden print:max-w-none">
                    
                    <button 
                      type="button"
                      onClick={() => window.print()}
                      className="absolute top-6 right-6 z-20 flex items-center gap-1.5 text-xs font-bold text-[#004c91] bg-blue-50 hover:bg-blue-100 px-3 py-1.5 rounded-lg transition-colors outline-none print:hidden"
                    >
                      <Download className="w-4 h-4" /> Tải PDF
                    </button>

                    {/* Draft decorative watermark stamp */}
                    <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 text-slate-100/15 text-5xl sm:text-7xl font-sans font-black tracking-widest uppercase pointer-events-none select-none -rotate-12">
                      FPT UNIVERSITY
                    </div>

                  {/* National Emblem Text & FPTU Header */}
                  <div className="flex flex-col sm:flex-row justify-between border-b border-slate-150 pb-5 text-xs gap-4 text-slate-550 relative z-10">
                    <div className="text-left space-y-1">
                      <p className="font-extrabold text-slate-900 text-xs sm:text-sm uppercase tracking-wide">TRƯỜNG ĐẠI HỌC FPT HÒA LẠC</p>
                      <p className="font-bold text-[11px] text-slate-550">Tổ Quản Lý Thiết Bị & Xe Điện Nội Khu</p>
                      <p className="text-[10px] text-slate-450 font-mono">Số văn bản: FPTU/BGNT-XD/2026-088</p>
                    </div>
                    <div className="text-left sm:text-right space-y-1">
                      <p className="font-extrabold text-slate-900 uppercase text-[11px] tracking-wider">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p>
                      <p className="font-black text-[11px] text-[#f37021]">Độc lập - Tự do - Hạnh phúc</p>
                      <div className="w-24 sm:w-32 h-[1px] bg-slate-250 sm:ml-auto mt-1" />
                    </div>
                  </div>

                  {/* Official Document Title */}
                  <div className="text-center space-y-1 mb-8 relative z-10 pt-2">
                    <h4 className="text-xl sm:text-2xl font-bold uppercase tracking-wide">
                      BIÊN BẢN BÀN GIAO VÀ NGHIỆM THU
                    </h4>
                    <p className="text-lg font-bold uppercase">
                      TÀI SẢN / TRANG THIẾT BỊ
                    </p>
                  </div>

                  {/* Core Minutes Information */}
                  <div className="space-y-3 text-[15px] leading-relaxed mb-6 font-sans relative z-10">
                    <p>
                      Hôm nay, lúc: <b>08:00</b> giờ, ngày <b>08/08/2026</b>, tại: <b>Trường Đại học FPT Hòa Lạc</b>.
                    </p>
                    <p>Chúng tôi gồm:</p>
                    <div className="space-y-2 pl-4">
                      <div className="flex flex-wrap gap-x-8 gap-y-2">
                        <p className="flex-1 min-w-[250px]">Người bàn giao: <b>Đại diện Tổ Quản Lý Thiết Bị</b></p>
                        <p className="flex-1 min-w-[200px]">Bộ phận: <b>Tổ Quản Lý Thiết Bị</b></p>
                      </div>
                      <div className="flex flex-wrap gap-x-8 gap-y-2">
                        <p className="flex-1 min-w-[250px]">Người nhận bàn giao: <b>Đại diện Ban Đào tạo & CTSV</b></p>
                        <p className="flex-1 min-w-[200px]">Bộ phận: <b>Ban Đào tạo & CTSV</b></p>
                      </div>
                      <p>Lý do bàn giao: <b>Đón tiếp phái đoàn đối tác thương mại Safuri</b></p>
                      <p>Thời gian hẹn trả tài sản: <b>16:30, 08/08/2026</b></p>
                    </div>
                  </div>

                  <p className="font-bold text-[15px] mb-2 relative z-10">Cùng bàn giao tài sản với tình trạng sau:</p>
                  <div className="overflow-x-auto mb-6 relative z-10">
                    <table className="w-full border-collapse border border-slate-500 text-[14px]">
                      <thead>
                        <tr className="bg-slate-50">
                          <th className="border border-slate-500 p-2 text-center w-12">STT</th>
                          <th className="border border-slate-500 p-2 text-center">Nội dung</th>
                          <th className="border border-slate-500 p-2 text-center w-24">Số Lượng</th>
                          <th className="border border-slate-500 p-2 text-center">Tình Trạng bàn giao</th>
                          <th className="border border-slate-500 p-2 text-center">Tình Trạng nhận</th>
                          <th className="border border-slate-500 p-2 text-center">Ghi chú</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr>
                          <td className="border border-slate-500 p-2 text-center">1</td>
                          <td className="border border-slate-500 p-2 font-semibold">Xe điện FPTU-EV-09 (8 ghế)</td>
                          <td className="border border-slate-500 p-2 text-center">1</td>
                          <td className="border border-slate-500 p-2 text-center">
                            {safuriBG1Note || 'Đã sạc đầy 100%, 10 ô dù'}
                          </td>
                          <td className="border border-slate-500 p-2 text-center">
                            {safuriBG2Signed ? (safuriBG2Note || 'Đã xác nhận') : ''}
                          </td>
                          <td className="border border-slate-500 p-2"></td>
                        </tr>
                      </tbody>
                    </table>
                  </div>

                  <div className="space-y-1 text-[14px] mb-8 relative z-10">
                    <p className="font-bold">Quy định khi sử dụng tài sản:</p>
                    <ul className="list-disc pl-8 space-y-1">
                      <li>Người mượn tài sản phải tuân thủ đúng mục đích sử dụng, không tự ý chuyển giao cho người khác.</li>
                      <li>Khi có vấn đề xảy ra (bị hỏng hoặc không nguyên hiện trạng ban đầu), <b>người mượn tài sản</b> sẽ phải chịu hoàn toàn trách nhiệm chi trả chi phí sửa chữa/đền bù.</li>
                      <li>An toàn trong quá trình sử dụng tài sản sẽ do <b>người mượn tài sản</b> chịu hoàn toàn trách nhiệm.</li>
                      <li>Ghi chú khác: ....................................................................................................................</li>
                    </ul>
                    <p className="mt-4">
                      Tôi là <b>Đại diện Ban Đào tạo & CTSV</b>, đã đọc hiểu và cam kết thực hiện đúng quy định sử dụng.
                    </p>
                  </div>

                  {/* Gray horizontal divider with Handover text */}
                  <div className="relative my-7">
                    <div className="absolute inset-0 flex items-center" aria-hidden="true">
                      <div className="w-full border-t border-slate-350"></div>
                    </div>
                    <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                      <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-3xs uppercase text-[11px] tracking-widest">BÀN GIAO</span>
                    </div>
                  </div>

                  {/* Handover Signatures with Notes on the SAME row */}
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-slate-50/70 p-4.5 rounded-2xl border border-slate-200">
                    
                    {/* Block Bên Giao */}
                    <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4">
                      <div>
                        <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">
                          Ghi chú Bên Giao
                        </label>
                        <textarea
                          rows={2}
                          value={safuriBG1Note}
                          onChange={e => setSafuriBG1Note(e.target.value)}
                          className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#f37021] outline-none resize-none font-sans bg-slate-50/30 focus:ring-1 focus:ring-orange-200"
                          disabled={!!safuriBG1Signed}
                          placeholder="Nhập ý kiến Bên Giao đầu giờ..."
                        />
                      </div>

                      {/* Horizontal Signature Box */}
                      <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriBG1Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-white hover:border-[#004c91]/40'}`}>
                        {safuriBG1Signed ? (
                          <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                              <div className="text-left">
                                <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                                <p className="text-[11px] font-extrabold text-slate-805 leading-snug truncate max-w-[170px]">{safuriBG1Signed.split(' - ')[0]}</p>
                                <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriBG1Signed.split(' - ')[1]}</p>
                              </div>
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-row items-center justify-between gap-3 w-full">
                            <div className="flex items-center gap-2">
                              <FileText className="w-4 h-4 text-[#f37021]/80 shrink-0" />
                              <div className="text-left">
                                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                <span className="text-[9px] text-slate-450">Nhấp để hoàn tất BG1</span>
                              </div>
                            </div>
                            <button
                              type="button"
                              onClick={() => handleSignHandover('BORROW', 'PROVIDER', safuriBG1Note, setSafuriBG1Signed, 'Đã ký bàn giao. Đơn yêu cầu đã chuyển sang đang xử lý.')}
                              className="py-2 px-3 bg-orange-50 hover:bg-orange-100 hover:text-[#f37021] text-slate-705 font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-3xs shrink-0"
                            >
                              <FileText className="w-3.5 h-3.5" />
                              <span>Ký xác nhận (BG1)</span>
                            </button>
                          </div>
                        )}
                      </div>
                    </div>

                    {/* Block Bên Nhận */}
                    <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4">
                      <div>
                        <label className="block text-[10px] font-black text-[#f37021] uppercase tracking-wider mb-1.5">
                          Ghi chú Bên Nhận
                        </label>
                        {safuriBG2Signed ? (
                          <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                            {safuriBG2Note || 'Đã xác nhận nhận tài sản.'}
                          </div>
                        ) : (
                          <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-400 italic">
                            Chưa ký nhận.
                          </div>
                        )}
                      </div>

                      {/* Horizontal Signature Box */}
                      <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriBG2Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                        {safuriBG2Signed ? (
                          <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                            <div className="flex items-center gap-2.5">
                              <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                              <div className="text-left">
                                <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT BÀN GIAO</span>
                                <p className="text-[11px] font-extrabold text-slate-850 leading-snug truncate max-w-[170px]">{safuriBG2Signed.split(' - ')[0]}</p>
                                <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriBG2Signed.split(' - ')[1]}</p>
                              </div>
                            </div>
                          </div>
                        ) : (
                          <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                            <div className="flex items-center gap-2">
                              <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                              <div className="text-left">
                                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                <span className="text-[9px] text-slate-450">Chờ Host ký nhận</span>
                              </div>
                            </div>
                          </div>
                        )}
                      </div>
                    </div>
                  </div>

                  {/* Toggle Acceptance Row when both are signed */}
                  {safuriBG1Signed && safuriBG2Signed ? (
                    <div className="animate-fade-in-quick space-y-6 pt-2 font-sans">
                      {/* Gray horizontal divider with Nghiệm thu text */}
                      <div className="relative my-7">
                        <div className="absolute inset-0 flex items-center" aria-hidden="true">
                          <div className="w-full border-t border-slate-350"></div>
                        </div>
                        <div className="relative flex justify-center text-xs uppercase font-extrabold tracking-widest">
                          <span className="bg-white text-slate-900 font-black px-4 py-1.5 rounded-full border border-slate-200 shadow-3xs uppercase text-[11px] tracking-widest">NGHIỆM THU</span>
                        </div>
                      </div>

                      {/* Acceptance signatures with Notes on the SAME row */}
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-5 bg-[#f8fbfe] p-4.5 rounded-2xl border border-blue-200/50">
                        
                        {/* Block Bên Giao Nghiệm Thu */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4 print:shadow-none">
                          <div>
                            <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">
                              Ghi chú Nghiệm thu (Bên Giao)
                            </label>
                            {safuriNT1Signed ? (
                              <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-600 italic">
                                {safuriNT1Note || 'Đã bàn giao trả tài sản.'}
                              </div>
                            ) : (
                              <div className="w-full text-xs p-3 border border-slate-200 rounded-xl bg-slate-50 min-h-[64px] text-slate-400 italic">
                                Chưa ký trả.
                              </div>
                            )}
                          </div>

                          {/* Horizontal Signature Box */}
                          <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriNT1Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-slate-50'}`}>
                            {safuriNT1Signed ? (
                              <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                                <div className="flex items-center gap-2.5">
                                  <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                    <p className="text-[11px] font-extrabold text-slate-805 leading-snug truncate max-w-[170px]">{safuriNT1Signed.split(' - ')[0]}</p>
                                    <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriNT1Signed.split(' - ')[1]}</p>
                                  </div>
                                </div>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Giao</span>
                                    <span className="text-[9px] text-slate-450">Chờ Host ký trả</span>
                                  </div>
                                </div>
                              </div>
                            )}
                          </div>
                        </div>

                        {/* Block Bên Nhận Nghiệm Thu */}
                        <div className="bg-white rounded-xl p-4 border border-slate-200 shadow-3xs flex flex-col justify-between gap-4 print:shadow-none">
                          <div>
                            <label className="block text-[10px] font-black text-[#004c91] uppercase tracking-wider mb-1.5">
                              Ghi chú Nghiệm thu (Bên Nhận)
                            </label>
                            <textarea
                              rows={2}
                              value={safuriNT2Note}
                              onChange={e => setSafuriNT2Note(e.target.value)}
                              className="w-full text-xs p-2.5 border border-slate-250 rounded-xl focus:border-[#004c91] outline-none resize-none font-sans bg-slate-50/30 focus:ring-1 focus:ring-blue-200"
                              disabled={!!safuriNT2Signed || !safuriNT1Signed}
                              placeholder={safuriNT1Signed ? "Nhận xét tình trạng bàn giao trả..." : "Chờ Host (Bên Giao) ký trả trước..."}
                            />
                          </div>

                          {/* Horizontal Signature Box */}
                          <div className={`border-2 rounded-xl p-3 relative group shadow-3xs transition-colors ${safuriNT2Signed ? 'border-solid border-emerald-500 bg-emerald-50/20' : 'border-dashed border-slate-250 bg-white hover:border-[#004c91]/40'}`}>
                            {safuriNT2Signed ? (
                              <div className="flex flex-row items-center justify-between gap-4 animate-fade-in-quick w-full">
                                <div className="flex items-center gap-2.5">
                                  <div className="w-8 h-8 rounded-full bg-emerald-50 text-emerald-700 flex items-center justify-center font-bold text-xs border border-emerald-200 shadow-3xs shrink-0">✓</div>
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black uppercase text-emerald-800 tracking-wider font-sans block leading-none mb-0.5">ĐÃ KÝ DUYỆT NGHIỆM THU</span>
                                    <p className="text-[11px] font-extrabold text-slate-850 leading-snug truncate max-w-[170px]">{safuriNT2Signed.split(' - ')[0]}</p>
                                    <p className="text-[9px] text-slate-500 font-mono mt-0.5 leading-none">{safuriNT2Signed.split(' - ')[1]}</p>
                                  </div>
                                </div>
                              </div>
                            ) : safuriNT1Signed ? (
                              <div className="flex flex-row items-center justify-between gap-3 w-full">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-[#004c91]/80 shrink-0" />
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                    <span className="text-[9px] text-slate-450">Nhấp để hoàn tất NT2</span>
                                  </div>
                                </div>
                                <button
                                  type="button"
                                  onClick={() => handleSignHandover('RETURN', 'PROVIDER', safuriNT2Note, setSafuriNT2Signed, 'Đã ký nghiệm thu. Đơn yêu cầu đã hoàn thành.')}
                                  className="py-2 px-3 bg-blue-50 hover:bg-blue-100 hover:text-[#004c91] text-slate-705 font-extrabold text-[11px] rounded-xl border border-slate-200 transition-all flex items-center gap-1.5 cursor-pointer shadow-3xs shrink-0"
                                >
                                  <FileText className="w-3.5 h-3.5" />
                                  <span>Ký Nghiệm thu (NT2)</span>
                                </button>
                              </div>
                            ) : (
                              <div className="flex flex-row items-center justify-between gap-3 w-full opacity-60">
                                <div className="flex items-center gap-2">
                                  <FileText className="w-4 h-4 text-slate-400 shrink-0" />
                                  <div className="text-left font-sans">
                                    <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest block leading-none">Chữ ký Bên Nhận</span>
                                    <span className="text-[9px] text-slate-450">Chờ bên giao ký trước</span>
                                  </div>
                                </div>
                              </div>
                            )}
                          </div>
                        </div>

                      </div>
                    </div>
                  ) : (
                    <div className="bg-amber-50/85 rounded-2xl p-4.5 text-center text-xs text-amber-900 border border-amber-200 animate-pulse relative z-10 flex items-center justify-center gap-2 font-sans">
                      <span className="w-2 h-2 rounded-full bg-amber-500" />
                      <span className="font-semibold text-amber-950">Tiến trình an toàn: Vui lòng ký đầy đủ 2 ô "Bàn giao" đợt 1 bên trên để tự động mở khóa hồ sơ "Nghiệm thu bồi hoàn" đợt 2 sau khi hoàn tất hành trình di chuyển đoàn Safuri.</span>
                    </div>
                  )}
                </div>
                </>
              )}

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
