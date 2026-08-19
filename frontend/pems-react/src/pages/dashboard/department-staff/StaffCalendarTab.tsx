/**
 * StaffCalendarTab – Tab lịch của Department Staff
 * Chỉ hiển thị đơn/thư được Leader phân công cho staff đó.
 * Không có bộ lọc "Loại lịch" (lọc tôi / phòng ban).
 */
import React, { useState, useMemo, useEffect } from 'react';
import {
  ChevronLeft, ChevronRight, ChevronDown, X, Clock, MapPin, User, AlertCircle, FileText, Plus, Calendar as CalendarIcon,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { useNavigate } from 'react-router-dom';
import { SubmittedVisitRequestDetailModal } from '../../../components/modals/SubmittedVisitRequestDetailModal';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import { LogisticsWorkContent } from '../../../features/department-reception-tasks/components/LogisticsWorkContent';
import { notificationsApi } from '../../../features/notifications/api/notificationsApi';
import { useNotifications } from '../../../features/notifications/context/NotificationsContext';
import { getNotificationLink } from '../../../features/notifications/components/NotificationBellButton';
import { NotificationDetailModal } from '../../../features/notifications/components/NotificationDetailModal';
import { HostFeedbackModal } from '../../../features/feedbacks/components/HostFeedbackModal';
import { VisitorFeedbackDetailModal } from '../../../features/feedbacks/components/VisitorFeedbackDetailModal';
import type { NotificationItem } from '../../../features/notifications/types/notification.types';
import { matchCalendarChangeNotifs } from '../../../features/notifications/utils/calendarChangeNotifs';
import { useAuth } from '../../../shared/hooks/useAuth';
import type { CalendarItem } from './useDeptStaffData';
import { StaffLeaderTaskModal } from './StaffLeaderTaskModal';
import { toVietnamCalendarDate } from '../../../shared/utils/vietnamTime';

interface Props {
  year: number;
  onYearChange: (year: number) => void;
  calendarItems: CalendarItem[];
  calendarLoading: boolean;
  onRefresh: () => void;
}

const WEEKDAYS = ['Thứ Hai', 'Thứ Ba', 'Thứ Tư', 'Thứ Năm', 'Thứ Sáu', 'Thứ Bảy', 'Chủ Nhật'];
const MONTHS = ['Tháng 1','Tháng 2','Tháng 3','Tháng 4','Tháng 5','Tháng 6','Tháng 7','Tháng 8','Tháng 9','Tháng 10','Tháng 11','Tháng 12'];

function todayStr() {
  // "Hôm nay" theo lịch Việt Nam — ô today không lệch ngày ở browser nước ngoài.
  const t = toVietnamCalendarDate(new Date())!;
  return `${t.getUTCFullYear()}-${String(t.getUTCMonth()+1).padStart(2,'0')}-${String(t.getUTCDate()).padStart(2,'0')}`;
}

function getDaysForYearMonth(year: number, month: number) {
  const firstDayRaw = new Date(Date.UTC(year, month, 1)).getUTCDay();
  const firstDayIndex = firstDayRaw === 0 ? 6 : firstDayRaw - 1;
  const totalDays = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
  const prevMonthTotalDays = new Date(Date.UTC(year, month, 0)).getUTCDate();
  const days = [];

  for (let i = 0; i < firstDayIndex; i++) {
    const d = prevMonthTotalDays - firstDayIndex + 1 + i;
    const pm = month === 0 ? 11 : month - 1;
    const py = month === 0 ? year - 1 : year;
    days.push({ day: d, month: pm, year: py, isCurrentMonth: false });
  }
  for (let i = 1; i <= totalDays; i++) {
    days.push({ day: i, month, year, isCurrentMonth: true });
  }
  const targetLen = days.length > 35 ? 42 : 35;
  const remaining = targetLen - days.length;
  for (let i = 1; i <= remaining; i++) {
    const nm = month === 11 ? 0 : month + 1;
    const ny = month === 11 ? year + 1 : year;
    days.push({ day: i, month: nm, year: ny, isCurrentMonth: false });
  }
  return days;
}

/** Parse key "YYYY-MM-DD" thành Date theo PHẦN lịch (không phải instant UTC midnight —
 *  new Date('YYYY-MM-DD') sẽ lùi 1 ngày ở browser múi giờ âm). */
function parseDateKey(s: string): Date {
  const [y, m, d] = s.split('-').map(Number);
  return new Date(y, (m || 1) - 1, d || 1);
}

function buildDaysGrid(year: number, month: number) {
  const firstRaw = new Date(year, month, 1).getDay();
  const firstIdx = firstRaw === 0 ? 6 : firstRaw - 1;
  const total = new Date(year, month + 1, 0).getDate();
  const prevTotal = new Date(year, month, 0).getDate();
  const days: { day: number; month: number; year: number; isCurrent: boolean; dateStr: string }[] = [];

  for (let i = 0; i < firstIdx; i++) {
    const d = prevTotal - firstIdx + 1 + i;
    const m = month === 0 ? 11 : month - 1;
    const y = month === 0 ? year - 1 : year;
    days.push({ day: d, month: m, year: y, isCurrent: false, dateStr: `${y}-${String(m+1).padStart(2,'0')}-${String(d).padStart(2,'0')}` });
  }
  for (let i = 1; i <= total; i++) {
    days.push({ day: i, month, year, isCurrent: true, dateStr: `${year}-${String(month+1).padStart(2,'0')}-${String(i).padStart(2,'0')}` });
  }
  const rem = 35 - days.length;
  for (let i = 1; i <= rem; i++) {
    const m = month === 11 ? 0 : month + 1;
    const y = month === 11 ? year + 1 : year;
    days.push({ day: i, month: m, year: y, isCurrent: false, dateStr: `${y}-${String(m+1).padStart(2,'0')}-${String(i).padStart(2,'0')}` });
  }
  return days;
}

/**
 * The Staff-Leader task modal only handles assigned work items (an INVITATION or a REQUEST); a
 * PERSONAL calendar event has no task workflow. This narrows CalendarItem so a PERSONAL event is
 * never passed to <StaffLeaderTaskModal/> — the narrowed item is structurally a StaffLeaderTaskModalItem.
 */
const isTaskModalItem = (
  item: CalendarItem | null,
): item is CalendarItem & { itemType: 'INVITATION' | 'REQUEST' } =>
  !!item && item.itemType !== 'PERSONAL';

export function StaffCalendarTab({ year, onYearChange, calendarItems, calendarLoading, onRefresh }: Props) {
  const today = todayStr();
  const now = toVietnamCalendarDate(new Date())!;
  const [month, setMonth] = useState(now.getUTCMonth());
  const [selectedDate, setSelectedDate] = useState<string | null>(today);
  const [displayMode, setDisplayMode] = useState<'Ngày' | 'Tuần' | 'Tháng' | 'Năm'>('Tháng');
  const [showDisplayDd, setShowDisplayDd] = useState(false);
  const [calendarType, setCalendarType] = useState<'office' | 'mine'>('mine');
  const [showTypeDd, setShowTypeDd] = useState(false);
  const [activeEvent, setActiveEvent] = useState<CalendarItem | null>(null);
  const [eventDetail, setEventDetail] = useState<any>(null);
  const [actionLoading, setActionLoading] = useState(false);
  const [rejectReason, setRejectReason] = useState('');
  const [showRejectInput, setShowRejectInput] = useState(false);
  const [proposalNote, setProposalNote] = useState('');
  const [proposalStart, setProposalStart] = useState('');
  const [proposalEnd, setProposalEnd] = useState('');
  const [showProposalInput, setShowProposalInput] = useState(false);
  const [handoverNote, setHandoverNote] = useState('');
  const [submittedVisitRequestId, setSubmittedVisitRequestId] = useState<number | null>(null);

  // Personal Event state for Staff
  const [showAddModal, setShowAddModal] = useState(false);
  const [newTitle, setNewTitle] = useState('');
  const [newContent, setNewContent] = useState('');
  const [newStartTime, setNewStartTime] = useState('08:00');
  const [newEndTime, setNewEndTime] = useState('09:00');
  const [addLoading, setAddLoading] = useState(false);

  const handleCreatePersonalEvent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newTitle.trim() || !selectedDate) return;
    if (selectedDate < today) {
      toast.error('Không thể tạo lịch trong quá khứ. Vui lòng chọn ngày từ hôm nay trở đi.');
      return;
    }

    const st = newStartTime || '08:00';
    const et = newEndTime || '09:00';
    const newTime = `${st} - ${et}`;

    const conflictingEvent = calendarItems.find(ev => {
      const s = ev.status;
      if (s === 'CANCELLED' || s === 'DECLINED' || s === 'REJECTED') return false;
      const parts = ev.time ? ev.time.split('-').map(x => x.trim()) : [];
      if (parts.length === 2 && parts[0].includes(':') && parts[1].includes(':')) {
        const [h1, m1] = parts[0].split(':').map(Number);
        const [h2, m2] = parts[1].split(':').map(Number);
        const [nh1, nm1] = st.split(':').map(Number);
        const [nh2, nm2] = et.split(':').map(Number);
        const start1 = nh1 * 60 + nm1;
        const end1 = nh2 * 60 + nm2;
        const start2 = h1 * 60 + m1;
        const end2 = h2 * 60 + m2;
        return ev.date === selectedDate && start1 < end2 && end1 > start2;
      }
      return false;
    });

    if (conflictingEvent) {
      toast.error(`Thời gian tạo lịch cá nhân bị trùng với đơn/thư hoặc lịch khác trong ngày (${conflictingEvent.time}: ${conflictingEvent.title}). Vui lòng chọn khung giờ khác!`);
      return;
    }

    setAddLoading(true);
    try {
      await departmentReceptionTasksApi.createPersonalEvent(
        newTitle.trim(),
        newContent.trim(),
        selectedDate,
        st,
        et
      );
      toast.success('Đã lưu lịch cá nhân');
      onRefresh();
      setShowAddModal(false);
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'Lỗi khi lưu lịch cá nhân');
    } finally {
      setAddLoading(false);
    }
  };

  // Xem / Sửa / Xóa lịch cá nhân
  const [personalEventModal, setPersonalEventModal] = useState<{
    open: boolean; eventId?: number | string; title: string; description: string;
    date: string; startTime: string; endTime: string; submitting: boolean; error?: string | null; isEditing?: boolean;
  }>({ open: false, title: '', description: '', date: '', startTime: '', endTime: '', submitting: false });

  const openPersonalEventModal = (ev: CalendarItem) => {
    const rawId = ev.rawId || ev.id;
    const timeParts = (ev.time || '09:00 - 10:00').split(' - ');
    const startTimeStr = timeParts[0]?.trim() || '09:00';
    const endTimeStr = timeParts[1]?.trim() || '10:00';
    setPersonalEventModal({
      open: true,
      eventId: rawId,
      title: ev.title.replace(' (đã hủy)', ''),
      description: (ev as any).description || (ev as any).purpose || '',
      date: ev.date,
      startTime: startTimeStr,
      endTime: endTimeStr,
      submitting: false,
      error: null,
      isEditing: false,
    });
  };

  const submitUpdatePersonalEvent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!personalEventModal.eventId) return;
    setPersonalEventModal((s) => ({ ...s, submitting: true, error: null }));
    try {
      await departmentReceptionTasksApi.updatePersonalEvent(
        personalEventModal.eventId,
        personalEventModal.title.trim(),
        personalEventModal.description.trim(),
        personalEventModal.date,
        personalEventModal.startTime,
        personalEventModal.endTime
      );
      toast.success('Đã cập nhật lịch cá nhân.');
      setPersonalEventModal((s) => ({ ...s, open: false, submitting: false }));
      onRefresh();
    } catch (err: any) {
      setPersonalEventModal((s) => ({
        ...s, submitting: false,
        error: err?.response?.data?.message || err?.response?.data?.title || 'Lỗi khi cập nhật lịch cá nhân.',
      }));
    }
  };

  const handleDeletePersonalEvent = async () => {
    if (!personalEventModal.eventId) return;
    if (!window.confirm('Bạn có chắc chắn muốn xóa lịch cá nhân này?')) return;
    setPersonalEventModal((s) => ({ ...s, submitting: true, error: null }));
    try {
      await departmentReceptionTasksApi.deletePersonalEvent(personalEventModal.eventId);
      toast.success('Đã xóa lịch cá nhân.');
      setPersonalEventModal((s) => ({ ...s, open: false, submitting: false }));
      onRefresh();
    } catch (err: any) {
      setPersonalEventModal((s) => ({
        ...s, submitting: false,
        error: err?.response?.data?.message || err?.response?.data?.title || 'Lỗi khi xóa lịch cá nhân.',
      }));
    }
  };

  const navigate = useNavigate();
  const { user: authUser } = useAuth();
  const { markAsRead: markNotificationRead } = useNotifications();
  // Thông báo chưa đọc gắn với đơn/thư mời — chấm đỏ nháy + "Thay đổi mới" (giống Dept Leader).
  const [changeNotifs, setChangeNotifs] = useState<NotificationItem[]>([]);
  const [changeNotifDetail, setChangeNotifDetail] = useState<NotificationItem | null>(null);
  // Feedback nhận được (Visitor đánh giá / Host chấm điểm) phải mở modal xem nội dung — không
  // điều hướng sang trang setup đoàn (xem SubmitVisitFeedbackCommandHandler.cs, cùng pattern
  // NotificationsPage.tsx / NotificationBellButton.tsx).
  const [hostFeedbackVisitInstanceId, setHostFeedbackVisitInstanceId] = useState<number | null>(null);
  const [visitorFeedbackVisitInstanceId, setVisitorFeedbackVisitInstanceId] = useState<number | null>(null);

  useEffect(() => {
    (async () => {
      try {
        const res = await notificationsApi.getNotifications({ page: 1, pageSize: 50, isRead: false });
        setChangeNotifs(res?.items || []);
      } catch (e) { console.error(e); }
    })();
  }, [calendarItems]);

  const getEventChangeNotifs = (ev: CalendarItem | null): NotificationItem[] =>
    matchCalendarChangeNotifs(changeNotifs, ev);

  /** Bấm 1 thay đổi: đánh dấu đã đọc rồi trỏ tới đúng chỗ như thông báo. */
  const handleChangeNotifClick = async (n: NotificationItem) => {
    try { await markNotificationRead(n.notificationId); } catch { /* ignore */ }
    setChangeNotifs(prev => prev.filter(x => x.notificationId !== n.notificationId));
    if (n.actionType === 'OPEN_HOST_FEEDBACK_MODAL' && n.visitInstanceId) {
      setHostFeedbackVisitInstanceId(n.visitInstanceId);
      return;
    }
    if (n.actionType === 'OPEN_VISITOR_FEEDBACK_MODAL' && n.visitInstanceId) {
      setVisitorFeedbackVisitInstanceId(n.visitInstanceId);
      return;
    }
    const link = getNotificationLink(n, authUser);
    if (link) {
      setActiveEvent(null);
      navigate(link);
    } else {
      setChangeNotifDetail(n);
    }
  };

  const daysGrid = useMemo(() => buildDaysGrid(year, month), [year, month]);

  const shiftView = (dir: -1 | 1) => {
    const base = selectedDate ? parseDateKey(selectedDate) : new Date(year, month, 1);
    if (displayMode === 'Ngày') {
      base.setDate(base.getDate() + dir);
      const next = `${base.getFullYear()}-${String(base.getMonth()+1).padStart(2,'0')}-${String(base.getDate()).padStart(2,'0')}`;
      setSelectedDate(next);
      setMonth(base.getMonth());
      onYearChange(base.getFullYear());
    } else if (displayMode === 'Tuần') {
      base.setDate(base.getDate() + dir * 7);
      const next = `${base.getFullYear()}-${String(base.getMonth()+1).padStart(2,'0')}-${String(base.getDate()).padStart(2,'0')}`;
      setSelectedDate(next);
      setMonth(base.getMonth());
      onYearChange(base.getFullYear());
    } else if (displayMode === 'Năm') {
      onYearChange(year + dir);
    } else if (month === 0 && dir === -1) {
      setMonth(11);
      onYearChange(year - 1);
    } else if (month === 11 && dir === 1) {
      setMonth(0);
      onYearChange(year + 1);
    } else {
      setMonth(m => m + dir);
    }
    setActiveEvent(null);
  };

  const prevMonth = () => shiftView(-1);
  const nextMonth = () => shiftView(1);

  const weekDays = useMemo(() => {
    const base = selectedDate ? parseDateKey(selectedDate) : new Date(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
    const day = base.getDay();
    const mondayOffset = day === 0 ? -6 : 1 - day;
    const monday = new Date(base);
    monday.setDate(base.getDate() + mondayOffset);
    return Array.from({ length: 7 }, (_, i) => {
      const d = new Date(monday);
      d.setDate(monday.getDate() + i);
      const dateStr = `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,'0')}-${String(d.getDate()).padStart(2,'0')}`;
      return { dateStr, label: WEEKDAYS[i], day: d.getDate(), month: d.getMonth(), year: d.getFullYear() };
    });
  }, [selectedDate]);

  const viewTitle = useMemo(() => {
    if (displayMode === 'Năm') return `Năm ${year}`;
    if (displayMode === 'Ngày' && selectedDate) return `Ngày ${selectedDate.split('-').reverse().join('/')}`;
    if (displayMode === 'Tuần') {
      const start = weekDays[0]?.dateStr.split('-').reverse().join('/');
      const end = weekDays[6]?.dateStr.split('-').reverse().join('/');
      return `Tuần ${start} - ${end}`;
    }
    return `${MONTHS[month]} ${year}`;
  }, [displayMode, month, selectedDate, weekDays, year]);

  const renderEventPill = (ev: CalendarItem, dateStr: string) => {
    const hasChanges = getEventChangeNotifs(ev).length > 0;
    return (
      <div key={ev.id} onClick={e => {
        e.stopPropagation();
        setSelectedDate(dateStr);
        if (ev.itemType === 'PERSONAL') {
          openPersonalEventModal(ev);
        } else {
          setActiveEvent(ev);
        }
      }}
        className={`relative px-1.5 py-1 rounded-md border text-[9px] font-normal leading-tight cursor-pointer truncate ${hasChanges ? 'pr-4' : ''} ${ev.color} ${activeEvent?.id === ev.id ? 'ring-2 ring-[#f37021]/50' : ''}`}>
        <span className="inline-block w-1 h-1 rounded-full mr-1 bg-current" />
        <span className={ev.status === 'CANCELLED' ? 'line-through' : ''}>{ev.title}</span>
        {hasChanges && (
          <span className="absolute top-1 right-1 flex h-2 w-2" title="Đơn này có thay đổi mới">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
            <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500" />
          </span>
        )}
      </div>
    );
  };

  const renderEmpty = (label = 'Không có lịch được giao') => (
    <div className="flex items-center justify-center min-h-[220px] text-sm text-slate-400 font-normal">{label}</div>
  );

  const displayItems = useMemo(() => {
    const authUserId = (authUser as any)?.id ?? authUser?.userId ?? (authUser as any)?.account;
    return calendarItems
      .filter(item => {
        if (calendarType === 'office') return true;
        const relatedId = item.relatedUserId != null ? String(item.relatedUserId) : null;
        const isMine = relatedId != null && authUserId != null && String(authUserId) === relatedId;
        return isMine || item.itemType === 'PERSONAL' || item.color.includes('amber');
      })
      .map(item => {
        if (item.itemType === 'PERSONAL') {
          if (calendarType === 'mine') {
            return {
              ...item,
              color: 'bg-purple-100 text-purple-800 border-purple-400 hover:bg-purple-200',
              hoverColor: 'border-purple-600',
            };
          } else {
            return {
              ...item,
              color: 'bg-blue-50 text-[#004c91] border-blue-300 hover:bg-blue-100',
              hoverColor: 'border-blue-500',
            };
          }
        }
        return item;
      });
  }, [calendarItems, calendarType, authUser]);

  const yearMonths = useMemo(() => Array.from({ length: 12 }, (_, m) => ({
    month: m,
    items: displayItems.filter(e => {
      const d = parseDateKey(e.date); // e.date đã là ngày lịch Việt Nam (mapCalendarItem)
      return d.getFullYear() === year && d.getMonth() === m;
    }),
  })), [displayItems, year]);

  // Fetch detail when event selected
  useEffect(() => {
    if (!activeEvent?.rawId) { setEventDetail(null); return; }
    (async () => {
      try {
        if (activeEvent.itemType === 'INVITATION') {
          const d = await departmentReceptionTasksApi.getInvitationDetail(activeEvent.rawId);
          setEventDetail(d);
        } else {
          const d = await departmentReceptionTasksApi.getRequestDetail(activeEvent.rawId);
          setEventDetail(d);
        }
      } catch { setEventDetail(null); }
    })();
    setRejectReason('');
    setShowRejectInput(false);
    setProposalNote('');
    setProposalStart('');
    setProposalEnd('');
    setShowProposalInput(false);
    setHandoverNote('');
  }, [activeEvent?.rawId, activeEvent?.itemType]);

  const handleAccept = async () => {
    if (!activeEvent) return;
    setActionLoading(true);
    try {
      if (activeEvent.itemType === 'INVITATION') {
        // Thư mời của chính mình, không phải dòng đang hiển thị trên ô lịch.
        await departmentReceptionTasksApi.acceptInvitation(activeEvent.currentUserParticipantId || activeEvent.rawId);
        toast.success('Đã chấp nhận thư mời');
      } else {
        await departmentReceptionTasksApi.acceptAssignment(activeEvent.rawId);
        toast.success('Đã chấp nhận nhiệm vụ');
      }
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Thao tác thất bại');
    } finally { setActionLoading(false); }
  };

  const handleDecline = async () => {
    if (!activeEvent || !rejectReason.trim()) { toast.error('Vui lòng nhập lý do từ chối'); return; }
    setActionLoading(true);
    try {
      if (activeEvent.itemType === 'INVITATION') {
        await departmentReceptionTasksApi.declineInvitation(
          activeEvent.currentUserParticipantId || activeEvent.rawId, rejectReason.trim());
        toast.success('Đã từ chối thư mời');
      } else {
        await departmentReceptionTasksApi.declineAssignment(activeEvent.rawId, rejectReason.trim());
        toast.success('Đã từ chối nhiệm vụ');
      }
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Thao tác thất bại');
    } finally { setActionLoading(false); }
  };

  const handlePropose = async () => {
    if (!activeEvent || !proposalNote.trim()) { toast.error('Vui lòng nhập nội dung đề xuất'); return; }
    if (!proposalStart || !proposalEnd) { toast.error('Vui lòng chọn giờ bắt đầu và giờ kết thúc'); return; }
    if (proposalEnd <= proposalStart) {
      toast.error('Giờ kết thúc phải sau giờ bắt đầu');
      return;
    }
    setActionLoading(true);
    try {
      await departmentReceptionTasksApi.proposeChange(activeEvent.rawId, {
        proposedUsageStartAt: `${activeEvent.date}T${proposalStart}:00`,
        proposedUsageEndAt: `${activeEvent.date}T${proposalEnd}:00`,
        proposalNote: proposalNote.trim(),
        proposedDescription: proposalNote.trim(),
      });
      toast.success('Đã gửi đề xuất thay đổi');
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Gửi đề xuất thất bại');
    } finally { setActionLoading(false); }
  };

  const handleSignHandover = async (handoverType: 'BORROW' | 'RETURN') => {
    if (!activeEvent) return;
    setActionLoading(true);
    try {
      await departmentReceptionTasksApi.signHandover(
        activeEvent.rawId,
        handoverType,
        handoverType === 'BORROW' ? 'PROVIDER' : 'BORROWER',
        handoverNote.trim(),
      );
      toast.success(handoverType === 'BORROW' ? 'Đã ký bàn giao' : 'Đã ký nghiệm thu');
      onRefresh();
      setActiveEvent(null);
    } catch (e: any) {
      toast.error(e?.response?.data?.message || 'Ký biên bản thất bại');
    } finally {
      setActionLoading(false);
    }
  };

  const openVisitDetail = () => {
    if (activeEvent?.visitRequestId) {
      setSubmittedVisitRequestId(Number(activeEvent.visitRequestId));
      return;
    }
    toast.error('Không tìm thấy lịch đoàn tiếp khách');
  };

  const getStatusBadge = (status: string) => {
    const map: Record<string, string> = {
      ASSIGNED: 'bg-orange-100 text-orange-700 border-orange-200',
      ACCEPTED: 'bg-emerald-100 text-emerald-700 border-emerald-200',
      DECLINED: 'bg-rose-100 text-rose-700 border-rose-200',
      REJECTED: 'bg-rose-100 text-rose-700 border-rose-200',
      IN_PROGRESS: 'bg-cyan-100 text-cyan-700 border-cyan-200',
      DONE: 'bg-slate-100 text-slate-600 border-slate-200',
      CANCELLED: 'bg-gray-100 text-gray-500 border-gray-200',
      CHANGE_PROPOSED: 'bg-amber-100 text-amber-700 border-amber-200',
    };
    return map[status] || 'bg-slate-100 text-slate-600 border-slate-200';
  };

  const statusLabel = (s: string) => {
    const m: Record<string, string> = {
      ASSIGNED: 'Mới được giao', ACCEPTED: 'Đã chấp nhận', DECLINED: 'Đã từ chối', REJECTED: 'Đã từ chối',
      IN_PROGRESS: 'Đang thực hiện', DONE: 'Hoàn thành', CANCELLED: 'Đã hủy',
      CHANGE_PROPOSED: 'Đang đề xuất', REQUESTED: 'Chờ phân công',
    };
    return m[s] || s;
  };

  const canAct = activeEvent && (activeEvent.status === 'ASSIGNED');
  const canProposeRequest = activeEvent?.itemType === 'REQUEST' && activeEvent.status !== 'CANCELLED' && activeEvent.status !== 'DECLINED' && activeEvent.status !== 'REJECTED' && activeEvent.status !== 'DONE' && activeEvent.status !== 'CHANGE_PROPOSED';
  const canSignBorrow = activeEvent?.itemType === 'REQUEST' && activeEvent.status === 'ACCEPTED';
  const canSignReturn = activeEvent?.itemType === 'REQUEST' && activeEvent.status === 'IN_PROGRESS';

  return (
    <div className="space-y-0">
      {/* Toolbar + chú thích gọn trên cùng 1 dòng */}
      <div className="flex items-center gap-3 flex-wrap p-4 md:p-5 pb-4">
        <div className="bg-slate-100 p-0.5 rounded-xl border border-slate-200 flex items-center gap-1">
          <button onClick={() => { setMonth(now.getUTCMonth()); onYearChange(now.getUTCFullYear()); }} className="px-4 py-2 text-xs font-bold text-slate-700 bg-white shadow-sm hover:bg-slate-50 border border-slate-200/60 rounded-lg">Hôm nay</button>
          <div className="h-4 w-px bg-slate-200 mx-1" />
          <button onClick={prevMonth} className="p-2 text-slate-600 hover:bg-white rounded-lg transition-all"><ChevronLeft className="w-4 h-4" /></button>
          <button onClick={nextMonth} className="p-2 text-slate-600 hover:bg-white rounded-lg transition-all"><ChevronRight className="w-4 h-4" /></button>
        </div>
        <div className="relative">
          <button onClick={() => setShowDisplayDd(v => !v)} className="flex items-center gap-2 px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-slate-700 hover:bg-slate-50 shadow-sm">
            Hiển thị: {displayMode} <ChevronDown className="w-3.5 h-3.5 text-slate-400" />
          </button>
          {showDisplayDd && (
            <><div className="fixed inset-0 z-20" onClick={() => setShowDisplayDd(false)} />
            <div className="absolute left-0 top-full mt-2 w-32 max-w-[calc(100vw-2rem)] bg-white border border-slate-200 rounded-xl shadow-xl z-30 py-1">
              {(['Ngày', 'Tuần', 'Tháng', 'Năm'] as const).map(m => (
                <button key={m} onClick={() => { setDisplayMode(m); setShowDisplayDd(false); }}
                  className={`w-full text-left px-4 py-2.5 text-xs font-bold transition-colors ${displayMode === m ? 'bg-blue-50 text-[#004c91]' : 'text-slate-700 hover:bg-slate-50'}`}>{m}</button>
              ))}
            </div></>
          )}
        </div>
        <div className="relative">
          <button onClick={() => setShowTypeDd(v => !v)} className="flex items-center gap-2 px-4 py-2 bg-white border border-slate-200 rounded-xl text-xs font-bold text-[#004c91] hover:bg-slate-50 shadow-sm">
            {calendarType === 'office' ? 'Trong văn phòng' : 'Lịch của tôi'} <ChevronDown className="w-3.5 h-3.5 text-[#004c91]/75" />
          </button>
          {showTypeDd && (
            <><div className="fixed inset-0 z-20" onClick={() => setShowTypeDd(false)} />
            <div className="absolute left-0 top-full mt-2 w-40 max-w-[calc(100vw-2rem)] bg-white border border-slate-200 rounded-xl shadow-xl z-30 py-1">
              <button onClick={() => { setCalendarType('office'); setShowTypeDd(false); }}
                className={`w-full text-left px-4 py-2.5 text-xs font-bold transition-colors ${calendarType === 'office' ? 'bg-blue-50 text-[#004c91]' : 'text-slate-700 hover:bg-slate-50'}`}>Trong văn phòng</button>
              <button onClick={() => { setCalendarType('mine'); setShowTypeDd(false); }}
                className={`w-full text-left px-4 py-2.5 text-xs font-bold transition-colors ${calendarType === 'mine' ? 'bg-blue-50 text-[#004c91]' : 'text-slate-700 hover:bg-slate-50'}`}>Lịch của tôi</button>
            </div></>
          )}
        </div>
        <span className="text-base font-extrabold text-[#004c91]">{viewTitle}</span>
        {/* Chú thích rút gọn */}
        <div className="ml-auto flex flex-wrap items-center gap-4 text-xs font-normal text-slate-600">
          {calendarType === 'office' ? (
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
      </div>

      <div className="flex gap-4">
        {/* Calendar grid */}
        <div className="flex-1 bg-white border-t border-slate-200 overflow-hidden">
          <div className="grid grid-cols-7 bg-[#004c91] text-center text-xs font-extrabold text-white uppercase tracking-wider py-4">
            {WEEKDAYS.map(d => <div key={d}>{d}</div>)}
          </div>
          {calendarLoading ? (
            <div className="flex items-center justify-center h-64 text-sm text-slate-400 font-normal">Đang tải lịch...</div>
          ) : displayMode === 'Tháng' ? (
            <div className="grid grid-cols-7 grid-rows-5 min-h-[850px] divide-x divide-y divide-slate-300 border-b border-slate-300 bg-slate-50/20">
              {daysGrid.map((cell, idx) => {
                const dayEvs = displayItems.filter(e => e.date === cell.dateStr);
                const isSelected = selectedDate === cell.dateStr;
                const isToday = cell.dateStr === today;
                const isPastDay = cell.dateStr < today;
                return (
                  <div key={idx}
                    onClick={() => {
                      setSelectedDate(cell.dateStr);
                      // Bấm vào ô ngày có lịch → mở thẳng view Ngày (giống Dept Leader).
                      if (dayEvs.length > 0) setDisplayMode('Ngày');
                      else setActiveEvent(null);
                    }}
                    className={`relative h-[165px] max-h-[165px] overflow-hidden p-2 flex flex-col cursor-pointer transition-colors group ${
                      isSelected ? 'bg-orange-50 ring-2 ring-inset ring-[#f37021] z-10' : cell.isCurrent ? 'bg-white hover:bg-orange-50/40' : 'bg-slate-50/30 hover:bg-slate-50'
                    }`}>
                    {cell.isCurrent && (
                      <>
                        <div className="flex items-center justify-between mb-1">
                          <span className={`text-xs font-extrabold px-1.5 py-0.5 rounded-md ${isToday ? 'bg-red-500 text-white' : isSelected ? 'bg-[#f37021] text-white' : 'text-slate-700'}`}>{cell.day}</span>
                          {!isPastDay && (
                            <button
                              type="button"
                              onClick={(e) => {
                                e.stopPropagation();
                                setSelectedDate(cell.dateStr);
                                setNewTitle('');
                                setNewContent('');
                                setNewStartTime('08:00');
                                setNewEndTime('09:00');
                                setShowAddModal(true);
                              }}
                              className="opacity-0 group-hover:opacity-100 text-[#f37021] hover:text-[#004c91] transition-opacity p-0.5 hover:bg-orange-100 rounded-md cursor-pointer"
                              title="Tạo lịch cá nhân"
                            >
                              <Plus className="w-3.5 h-3.5" />
                            </button>
                          )}
                        </div>
                        <div className="space-y-0.5 overflow-y-auto flex-1 no-scrollbar">
                          {dayEvs.map(ev => renderEventPill(ev, cell.dateStr))}
                        </div>
                      </>
                    )}
                    {/* Lớp xám phủ lên các ngày trong quá khứ */}
                    {cell.isCurrent && isPastDay && (
                      <div className="absolute inset-0 bg-slate-300/45 pointer-events-none z-10" aria-hidden="true" />
                    )}
                  </div>
                );
              })}
            </div>
          ) : displayMode === 'Tuần' ? (
            <div className="grid grid-cols-7 min-h-[520px] divide-x divide-slate-200 bg-white">
              {weekDays.map(day => {
                const dayEvs = displayItems.filter(e => e.date === day.dateStr);
                const isToday = day.dateStr === today;
                const isPastDay = day.dateStr < today;
                return (
                  <div key={day.dateStr} onClick={() => setSelectedDate(day.dateStr)} className={`relative p-3 ${selectedDate === day.dateStr ? 'bg-orange-50' : ''}`}>
                    <div className="text-center mb-3">
                      <p className="text-[10px] font-black uppercase text-slate-400">{day.label}</p>
                      <span className={`inline-flex mt-1 w-8 h-8 items-center justify-center rounded-full text-sm font-black ${isToday ? 'bg-red-500 text-white' : selectedDate === day.dateStr ? 'bg-[#f37021] text-white' : 'bg-slate-100 text-slate-700'}`}>{day.day}</span>
                    </div>
                    <div className="space-y-1">{dayEvs.length ? dayEvs.map(ev => renderEventPill(ev, day.dateStr)) : <p className="text-[11px] text-slate-300 text-center font-normal pt-4">Trống</p>}</div>
                    {/* Lớp xám phủ lên các ngày trong quá khứ */}
                    {isPastDay && (
                      <div className="absolute inset-0 bg-slate-300/45 pointer-events-none z-10" aria-hidden="true" />
                    )}
                  </div>
                );
              })}
            </div>
          ) : displayMode === 'Ngày' ? (
            <div className="p-5 min-h-[520px] bg-white">
              <div className="mb-4">
                <button
                  type="button"
                  onClick={() => setDisplayMode('Tháng')}
                  className="flex items-center gap-2 px-3.5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-[11px] font-bold rounded-xl transition-all border border-slate-200/60"
                >
                  <ChevronLeft className="w-3.5 h-3.5 text-slate-600" />
                  <span>Quay lại lịch tháng</span>
                </button>
              </div>
              {(() => {
                const dayEvs = displayItems.filter(e => e.date === selectedDate);
                if (!dayEvs.length) return renderEmpty();
                return <div className="space-y-2 max-w-3xl mx-auto">{dayEvs.map(ev => {
                  const hasChanges = getEventChangeNotifs(ev).length > 0;
                  return (
                    <div key={ev.id} onClick={() => setActiveEvent(ev)} className={`px-4 py-3 rounded-xl border text-sm font-normal cursor-pointer ${ev.color}`}>
                      <div className="flex items-center justify-between gap-3">
                        <span className="truncate flex items-center gap-2">
                          <span className={`truncate ${ev.status === 'CANCELLED' ? 'line-through' : ''}`} title={ev.title}>{ev.title}</span>
                          {hasChanges && (
                            <span className="relative flex h-2 w-2 shrink-0" title="Đơn này có thay đổi mới">
                              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
                              <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500" />
                            </span>
                          )}
                        </span>
                        <span className="text-xs shrink-0">{ev.time}</span>
                      </div>
                      {ev.delegationName && <p className="text-xs opacity-80 mt-1">{ev.delegationName}</p>}
                    </div>
                  );
                })}</div>;
              })()}
            </div>
          ) : (
            <div className="p-6 overflow-y-auto no-scrollbar max-h-[720px] bg-white">
              <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider mb-6 text-center">
                Tổng quan danh mục sự kiện năm {year}
              </h3>

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                {MONTHS.map((mName, mIdx) => {
                  const mDays = getDaysForYearMonth(year, mIdx);
                  const isCurrentMonthThisYear = mIdx === (new Date().getMonth()) && year === (new Date().getFullYear());
                  return (
                    <div
                      key={mName}
                      className={`p-3.5 rounded-2xl border transition-colors ${
                        isCurrentMonthThisYear ? 'border-orange-300 bg-orange-50/20' : 'bg-slate-50/50 border-slate-150/80 hover:border-orange-200'
                      }`}
                    >
                      <h4 className="text-xs font-black text-[#004c91] text-center mb-2.5">{mName}</h4>

                      <div className="grid grid-cols-7 text-center text-[9px] font-black text-slate-400 mb-1">
                        <div>T2</div><div>T3</div><div>T4</div><div>T5</div><div>T6</div><div>T7</div><div>CN</div>
                      </div>

                      <div className="grid grid-cols-7 text-center gap-0.5 text-[10px]">
                        {mDays.map((cell, cIdx) => {
                          const cellKey = `${cell.year}-${String(cell.month + 1).padStart(2, '0')}-${String(cell.day).padStart(2, '0')}`;
                          const cellEvents = displayItems.filter(e => e.date === cellKey);
                          const hasEvents = cellEvents.length > 0;
                          const isTodayCell = cellKey === today;

                          return (
                            <button
                              key={cIdx}
                              type="button"
                              onClick={() => {
                                setMonth(cell.month);
                                setSelectedDate(cellKey);
                                setDisplayMode(hasEvents ? 'Ngày' : 'Tháng');
                              }}
                              className={`relative w-5.5 h-5.5 rounded-full flex items-center justify-center font-bold transition-all mx-auto cursor-pointer ${
                                isTodayCell
                                  ? 'bg-[#004c91] text-white font-black'
                                  : hasEvents
                                  ? 'bg-orange-100 text-orange-700 hover:bg-orange-200 font-black'
                                  : cell.isCurrentMonth
                                  ? 'text-slate-700 hover:bg-slate-200'
                                  : 'text-slate-350 hover:bg-slate-100/50'
                              }`}
                              title={hasEvents ? `${cellKey}: ${cellEvents.length} lịch` : undefined}
                            >
                              {cell.day}
                              {hasEvents && (
                                <span className={`absolute -top-0.5 -right-0.5 w-1.5 h-1.5 rounded-full ${isTodayCell ? 'bg-white' : 'bg-red-500'} border border-white`} />
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

        <StaffLeaderTaskModal
          item={isTaskModalItem(activeEvent) ? activeEvent : null}
          onClose={() => setActiveEvent(null)}
          onRefresh={onRefresh}
          changeNotifs={isTaskModalItem(activeEvent) ? getEventChangeNotifs(activeEvent) : []}
          onChangeNotifClick={handleChangeNotifClick}
        />

        {/* Event detail modal */}
        {false && activeEvent && (
          <div className="fixed inset-0 z-50 bg-slate-900/45 flex items-center justify-center p-4" onClick={() => setActiveEvent(null)}>
          {/* Overlay is a flat p-4 (2rem vertical gutter). */}
          <div className="w-full max-w-4xl max-h-[calc(100dvh-2rem)] bg-white rounded-2xl border border-slate-200 shadow-2xl flex flex-col overflow-hidden" onClick={e => e.stopPropagation()}>
            <div className="bg-[#004c91] px-6 py-5 text-white flex items-start justify-between gap-4 shadow-sm">
              <div className="flex-1 min-w-0">
                <span className="text-[11px] font-black uppercase tracking-wider text-white/75 inline-block mb-2">
                  {activeEvent.itemType === 'INVITATION' ? 'Chi tiết thư mời' : 'Thông tin đơn yêu cầu'}
                </span>
                <p className="text-xl md:text-3xl font-extrabold tracking-tight text-white leading-tight">{activeEvent.title}</p>
                {activeEvent.delegationName && <p className="text-sm text-white/80 font-normal mt-1">{activeEvent.delegationName}</p>}
              </div>
              <button onClick={() => setActiveEvent(null)} className="p-2 hover:bg-white/10 rounded-full text-white flex-shrink-0"><X className="w-5 h-5" /></button>
            </div>

            <div className="p-6 md:p-8 space-y-5 flex-1 overflow-y-auto bg-slate-50/50">
              {(activeEvent.visitRequestId || activeEvent.visitInstanceId) && (
                <button
                  onClick={openVisitDetail}
                  className="w-full md:w-auto py-3 px-5 rounded-xl border border-blue-100 bg-white text-[#004c91] text-sm font-black hover:bg-blue-50 flex items-center justify-center gap-2 shadow-sm"
                >
                  <FileText className="w-4 h-4" />
                  Xem chi tiết đoàn tiếp khách
                </button>
              )}

              <div className="bg-white rounded-2xl border border-slate-200/85 p-5 md:p-6 space-y-5">
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="p-4 bg-blue-50/60 rounded-2xl border border-blue-100">
                    <div className="flex items-center gap-2 text-[#004c91] mb-2">
                      <User className="w-4 h-4" />
                      <span className="text-[11px] font-bold uppercase tracking-wider">Người giao</span>
                    </div>
                    <p className="text-sm font-normal text-[#004c91]">{activeEvent.host || 'Hệ thống'}</p>
                  </div>
                  <div className="p-4 bg-orange-50/60 rounded-2xl border border-orange-100">
                    <div className="flex items-center gap-2 text-[#f37021] mb-2">
                      <MapPin className="w-4 h-4" />
                      <span className="text-[11px] font-bold uppercase tracking-wider">Địa điểm</span>
                    </div>
                    <p className="text-sm font-normal text-slate-800">{activeEvent.location || 'Chưa cập nhật'}</p>
                  </div>
                  <div className="sm:col-span-2 p-4 bg-slate-50 rounded-2xl border border-slate-100">
                    <div className="flex items-center gap-2 text-slate-500 mb-2">
                      <Clock className="w-4 h-4" />
                      <span className="text-[11px] font-bold uppercase tracking-wider">Thời gian</span>
                    </div>
                    <div className="text-[15px] font-normal text-gray-800 flex items-center flex-wrap gap-2">
                      <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activeEvent.time?.split('-')[0]?.trim()}</span>
                      <ChevronRight className="w-4 h-4 text-gray-400" />
                      <span className="px-3 py-1 bg-white rounded-lg border border-gray-200 shadow-sm text-[#004c91]">{activeEvent.time?.split('-')[1]?.trim()}</span>
                      <span className="text-[#004c91] font-normal ml-1">{activeEvent.date?.split('-').reverse().join('-')}</span>
                    </div>
                  </div>
                </div>
                <div className="flex items-center justify-between gap-3">
                  <span className={`text-[11px] font-black px-3 py-1 rounded-full border ${getStatusBadge(activeEvent.status)}`}>{statusLabel(activeEvent.status)}</span>
                  <span className="text-[11px] font-normal text-slate-400">{activeEvent.itemType === 'INVITATION' ? 'Thư mời tham gia' : 'Đơn yêu cầu hỗ trợ'}</span>
                </div>
                {/* A REQUEST's detail comes from the request detail's `description`. `activeEvent.purpose`
                    is derived from the calendar feed, which carries no description — its value is the
                    item TITLE — so reading it here printed the request's own name under this heading.
                    An invitation has no such detail field and keeps using `purpose`. */}
                {(() => {
                  const isRequest = activeEvent.itemType === 'REQUEST';
                  // An invitation has no request-detail description and keeps using `purpose`.
                  if (!isRequest) {
                    return activeEvent.purpose ? (
                      <div className="space-y-2">
                        <div className="flex items-center gap-2 text-slate-400">
                          <FileText className="w-4 h-4" />
                          <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết</span>
                        </div>
                        <p className="whitespace-pre-wrap break-words text-sm font-normal leading-relaxed text-slate-700">
                          {activeEvent.purpose}
                        </p>
                      </div>
                    ) : null;
                  }
                  return (
                    <div className="space-y-2">
                      <div className="flex items-center gap-2 text-slate-400">
                        <FileText className="w-4 h-4" />
                        <span className="text-[11px] font-bold uppercase tracking-wider">Nội dung chi tiết công việc</span>
                      </div>
                      <LogisticsWorkContent detail={eventDetail} className="text-sm font-normal text-slate-700" />
                    </div>
                  );
                })()}
              </div>

              {/* Decline reason from detail */}
              {eventDetail?.rejectReason && (
                <div className="bg-rose-50 rounded-xl p-3 border border-rose-100 flex gap-2">
                  <AlertCircle className="w-4 h-4 text-rose-500 flex-shrink-0 mt-0.5" />
                  <p className="text-[11px] text-rose-700 font-normal">{eventDetail.rejectReason}</p>
                </div>
              )}

              {/* Actions – only when status is ASSIGNED */}
              {canAct && (
                <div className="pt-2 space-y-2">
                  <button onClick={handleAccept} disabled={actionLoading}
                    className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                    ✓ Chấp nhận
                  </button>
                  {!showRejectInput ? (
                    <button onClick={() => setShowRejectInput(true)} disabled={actionLoading}
                      className="w-full py-2.5 bg-white border border-rose-200 text-rose-600 text-sm font-black rounded-xl hover:bg-rose-50 transition-colors">
                      ✕ Từ chối
                    </button>
                  ) : (
                    <div className="space-y-2">
                      <textarea rows={2} value={rejectReason} onChange={e => setRejectReason(e.target.value)}
                        placeholder="Lý do từ chối..."
                        className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-rose-400 resize-none" />
                      <div className="flex gap-2">
                        <button onClick={handleDecline} disabled={actionLoading || !rejectReason.trim()}
                          className="flex-1 py-2 bg-rose-600 hover:bg-rose-700 text-white text-xs font-black rounded-xl disabled:opacity-50">Xác nhận từ chối</button>
                        <button onClick={() => setShowRejectInput(false)} className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
                      </div>
                    </div>
                  )}
                  {activeEvent.itemType === 'REQUEST' && (
                    !showProposalInput ? (
                      <button onClick={() => setShowProposalInput(true)} disabled={actionLoading}
                        className="w-full py-2.5 bg-white border border-amber-200 text-amber-700 text-sm font-black rounded-xl hover:bg-amber-50 transition-colors">
                        Đề xuất thay đổi
                      </button>
                    ) : (
                      <div className="space-y-2">
                        <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                          <div>
                            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian bắt đầu mới</label>
                            <input type="time" value={proposalStart} onChange={e => setProposalStart(e.target.value)}
                              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                          </div>
                          <div>
                            <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian kết thúc mới</label>
                            <input type="time" value={proposalEnd} onChange={e => setProposalEnd(e.target.value)}
                              className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                          </div>
                        </div>
                        <textarea rows={3} value={proposalNote} onChange={e => setProposalNote(e.target.value)}
                          placeholder="Nội dung/lý do đề xuất..."
                          className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400 resize-none" />
                        <div className="flex gap-2">
                          <button onClick={handlePropose} disabled={actionLoading || !proposalNote.trim() || !proposalStart || !proposalEnd}
                            className="flex-1 py-2 bg-amber-600 hover:bg-amber-700 text-white text-xs font-black rounded-xl disabled:opacity-50">Gửi đề xuất</button>
                          <button onClick={() => setShowProposalInput(false)} className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
                        </div>
                      </div>
                    )
                  )}
                </div>
              )}
              {!canAct && canProposeRequest && (
                <div className="pt-2 space-y-2">
                  {!showProposalInput ? (
                    <button onClick={() => setShowProposalInput(true)} disabled={actionLoading}
                      className="w-full py-2.5 bg-white border border-amber-200 text-amber-700 text-sm font-black rounded-xl hover:bg-amber-50 transition-colors">
                      Đề xuất thay đổi
                    </button>
                  ) : (
                    <div className="space-y-2">
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                        <div>
                          <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian bắt đầu mới</label>
                          <input type="time" value={proposalStart} onChange={e => setProposalStart(e.target.value)}
                            className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                        </div>
                        <div>
                          <label className="block text-[11px] font-black text-slate-500 uppercase tracking-wide mb-1">Thời gian kết thúc mới</label>
                          <input type="time" value={proposalEnd} onChange={e => setProposalEnd(e.target.value)}
                            className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400" />
                        </div>
                      </div>
                      <textarea rows={3} value={proposalNote} onChange={e => setProposalNote(e.target.value)}
                        placeholder="Nội dung/lý do đề xuất..."
                        className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-amber-400 resize-none" />
                      <div className="flex gap-2">
                        <button onClick={handlePropose} disabled={actionLoading || !proposalNote.trim() || !proposalStart || !proposalEnd}
                          className="flex-1 py-2 bg-amber-600 hover:bg-amber-700 text-white text-xs font-black rounded-xl disabled:opacity-50">Gửi đề xuất</button>
                        <button onClick={() => setShowProposalInput(false)} className="px-3 py-2 text-xs font-bold text-slate-600 bg-slate-100 rounded-xl hover:bg-slate-200">Hủy</button>
                      </div>
                    </div>
                  )}
                </div>
              )}
              {(canSignBorrow || canSignReturn) && (
                <div className="pt-3 space-y-2 border-t border-slate-100">
                  <textarea rows={2} value={handoverNote} onChange={e => setHandoverNote(e.target.value)}
                    placeholder={canSignBorrow ? 'Ghi chú bàn giao...' : 'Ghi chú nghiệm thu...'}
                    className="w-full px-3 py-2 text-xs border border-slate-200 rounded-xl outline-none focus:border-blue-400 resize-none" />
                  {canSignBorrow && (
                    <button onClick={() => handleSignHandover('BORROW')} disabled={actionLoading}
                      className="w-full py-2.5 bg-orange-600 hover:bg-orange-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                      Ký bàn giao
                    </button>
                  )}
                  {canSignReturn && (
                    <button onClick={() => handleSignHandover('RETURN')} disabled={actionLoading}
                      className="w-full py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-black rounded-xl transition-colors disabled:opacity-50">
                      Ký nghiệm thu
                    </button>
                  )}
                </div>
              )}
            </div>
          </div>
          </div>
        )}
      </div>
      <SubmittedVisitRequestDetailModal
        isOpen={submittedVisitRequestId != null}
        visitRequestId={submittedVisitRequestId}
        onClose={() => setSubmittedVisitRequestId(null)}
        hideOperationalContact
      />
      <NotificationDetailModal item={changeNotifDetail} onClose={() => setChangeNotifDetail(null)} />

      <HostFeedbackModal
        open={hostFeedbackVisitInstanceId !== null}
        visitInstanceId={hostFeedbackVisitInstanceId}
        onClose={() => setHostFeedbackVisitInstanceId(null)}
      />

      <VisitorFeedbackDetailModal
        open={visitorFeedbackVisitInstanceId !== null}
        visitInstanceId={visitorFeedbackVisitInstanceId}
        onClose={() => setVisitorFeedbackVisitInstanceId(null)}
      />

      {/* Personal Event Creation Modal for Staff */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 flex items-center justify-center p-4" onClick={() => setShowAddModal(false)}>
          <div className="w-full max-w-md bg-white rounded-2xl shadow-2xl border border-slate-200 overflow-hidden" onClick={e => e.stopPropagation()}>
            <div className="px-5 py-3.5 bg-[#004c91] text-white flex items-center justify-between">
              <h3 className="font-black text-sm flex items-center gap-2">
                <CalendarIcon className="w-4 h-4 text-[#f37021]" />
                Lên Lịch Cá Nhân ({selectedDate})
              </h3>
              <button
                type="button"
                onClick={() => setShowAddModal(false)}
                className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded-full transition-colors"
              >
                <X className="w-4 h-4" />
              </button>
            </div>

            <form onSubmit={handleCreatePersonalEvent} className="p-5 space-y-4 text-xs text-slate-800">
              <div className="space-y-3">
                <div>
                  <label className="block text-[10px] font-extrabold text-slate-400 uppercase tracking-wider mb-1">
                    Tiêu đề sự kiện *
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="VD: Họp nội bộ / Nhiệm vụ riêng"
                    value={newTitle}
                    onChange={e => setNewTitle(e.target.value)}
                    className="w-full text-xs px-3.5 py-2 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none"
                  />
                </div>

                <div>
                  <label className="block text-[10px] font-extrabold text-slate-400 uppercase tracking-wider mb-1">
                    Khung giờ *
                  </label>
                  <div className="flex items-center gap-2">
                    <input
                      type="time"
                      required
                      value={newStartTime}
                      onChange={e => setNewStartTime(e.target.value)}
                      className="flex-1 text-xs px-3 py-2 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none"
                    />
                    <span className="text-slate-400 font-bold text-xs shrink-0">—</span>
                    <input
                      type="time"
                      required
                      value={newEndTime}
                      min={newStartTime}
                      onChange={e => setNewEndTime(e.target.value)}
                      className="flex-1 text-xs px-3 py-2 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-[10px] font-extrabold text-slate-400 uppercase tracking-wider mb-1">
                    Nội dung / Ghi chú
                  </label>
                  <textarea
                    rows={4}
                    value={newContent}
                    onChange={e => setNewContent(e.target.value)}
                    placeholder="Mô tả nội dung công việc..."
                    className="w-full text-xs px-3.5 py-2 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none resize-none"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="py-2 px-4 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors cursor-pointer"
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  disabled={addLoading || !newTitle.trim()}
                  className="py-2 px-6 bg-[#f37021] hover:bg-[#e05f10] text-white font-black rounded-xl transition-all cursor-pointer shadow-xs disabled:opacity-50"
                >
                  {addLoading ? 'Đang lưu...' : 'Xác nhận lưu'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ── Modal xem/sửa/xóa lịch cá nhân ── */}
      {personalEventModal.open && (
        <div className="fixed inset-0 z-[120] flex items-center justify-center bg-slate-900/40 backdrop-blur-xs p-4">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden animate-in fade-in zoom-in-95 duration-150">
            <div className="px-6 py-4 bg-gradient-to-r from-purple-600 to-indigo-600 text-white flex items-center justify-between">
              <h3 className="font-black text-sm flex items-center gap-2">
                <CalendarIcon className="w-4 h-4 text-purple-200" />
                {personalEventModal.isEditing ? 'Chỉnh sửa lịch cá nhân' : 'Chi tiết lịch cá nhân'}
              </h3>
              <button
                type="button"
                onClick={() => setPersonalEventModal((s) => ({ ...s, open: false }))}
                className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded-full transition-colors cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <form onSubmit={submitUpdatePersonalEvent} className="p-6 space-y-4 text-xs text-slate-800">
              <div>
                <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                  Tiêu đề sự kiện *
                </label>
                <input
                  type="text"
                  required
                  disabled={!personalEventModal.isEditing || personalEventModal.submitting}
                  placeholder="VD: Họp định kỳ"
                  value={personalEventModal.title}
                  onChange={(e) => setPersonalEventModal((s) => ({ ...s, title: e.target.value }))}
                  className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-purple-500 outline-none bg-slate-50/20 disabled:bg-slate-100 disabled:text-slate-700"
                />
              </div>
              <div className="grid grid-cols-3 gap-2">
                <div>
                  <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">Ngày *</label>
                  <input
                    type="date"
                    required
                    disabled={!personalEventModal.isEditing || personalEventModal.submitting}
                    value={personalEventModal.date}
                    onChange={(e) => setPersonalEventModal((s) => ({ ...s, date: e.target.value }))}
                    className="w-full text-xs px-2.5 py-2.5 border border-slate-200 rounded-xl focus:border-purple-500 outline-none bg-slate-50/20 disabled:bg-slate-100 disabled:text-slate-700"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">Bắt đầu *</label>
                  <input
                    type="time"
                    required
                    disabled={!personalEventModal.isEditing || personalEventModal.submitting}
                    value={personalEventModal.startTime}
                    onChange={(e) => setPersonalEventModal((s) => ({ ...s, startTime: e.target.value }))}
                    className="w-full text-xs px-2.5 py-2.5 border border-slate-200 rounded-xl focus:border-purple-500 outline-none bg-slate-50/20 disabled:bg-slate-100 disabled:text-slate-700"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">Kết thúc *</label>
                  <input
                    type="time"
                    required
                    disabled={!personalEventModal.isEditing || personalEventModal.submitting}
                    value={personalEventModal.endTime}
                    min={personalEventModal.startTime}
                    onChange={(e) => setPersonalEventModal((s) => ({ ...s, endTime: e.target.value }))}
                    className="w-full text-xs px-2.5 py-2.5 border border-slate-200 rounded-xl focus:border-purple-500 outline-none bg-slate-50/20 disabled:bg-slate-100 disabled:text-slate-700"
                  />
                </div>
              </div>
              <div>
                <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">Nội dung</label>
                <textarea
                  rows={3}
                  disabled={!personalEventModal.isEditing || personalEventModal.submitting}
                  value={personalEventModal.description}
                  onChange={(e) => setPersonalEventModal((s) => ({ ...s, description: e.target.value }))}
                  className="w-full text-xs px-3.5 py-2 border border-slate-200 rounded-xl focus:border-purple-500 outline-none resize-none font-sans bg-slate-50/20 disabled:bg-slate-100 disabled:text-slate-700"
                />
              </div>
              {personalEventModal.error && <p className="text-red-500 text-xs font-normal">{personalEventModal.error}</p>}
              <div className="flex justify-between items-center pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={handleDeletePersonalEvent}
                  disabled={personalEventModal.submitting}
                  className="px-3.5 py-2 text-xs font-bold text-red-600 bg-red-50 hover:bg-red-100 rounded-xl transition-colors cursor-pointer"
                >
                  Xóa lịch
                </button>
                <div className="flex gap-2">
                  {!personalEventModal.isEditing ? (
                    <button
                      type="button"
                      onClick={() => setPersonalEventModal((s) => ({ ...s, isEditing: true }))}
                      className="px-4 py-2 text-xs font-bold text-white bg-purple-600 hover:bg-purple-700 rounded-xl transition-colors cursor-pointer"
                    >
                      Chỉnh sửa
                    </button>
                  ) : (
                    <>
                      <button
                        type="button"
                        onClick={() => setPersonalEventModal((s) => ({ ...s, isEditing: false }))}
                        className="px-3 py-2 text-xs font-bold text-slate-600 hover:bg-slate-100 rounded-xl transition-colors cursor-pointer"
                      >
                        Hủy
                      </button>
                      <button
                        type="submit"
                        disabled={personalEventModal.submitting}
                        className="px-4 py-2 text-xs font-bold text-white bg-purple-600 hover:bg-purple-700 rounded-xl transition-colors cursor-pointer disabled:opacity-50"
                      >
                        {personalEventModal.submitting ? 'Đang lưu...' : 'Lưu thay đổi'}
                      </button>
                    </>
                  )}
                </div>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
