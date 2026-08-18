/**
 * StaffDashboardCalendar — bảng lịch dashboard cho Staff Leader (STAFF+LEADER) và
 * Staff thường (STAFF+STAFF), data thật từ GET /api/dashboard/staff/calendar.
 *
 * Hai loại lịch:
 *   • Lịch văn phòng — toàn bộ yêu cầu đến thăm thuộc campus của user.
 *   • Lịch của tôi   — chỉ yêu cầu tham quan mà user là host.
 *
 * Click event → modal chi tiết (StaffVisitDetailModal). Staff Leader có thể
 * Chấp nhận & gán host / Từ chối theo allowedActions backend trả; gán host chỉ
 * cần chọn host (không gửi email, không có bước chấp nhận/từ chối — Staff được
 * gán mặc nhiên là host và tự vào "Setup đoàn khách" khi cần).
 * Mỗi ngày có nút + để tạo lịch cá nhân (dùng chung API personal-events với dashboard
 * Department Leader).
 */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  Calendar as CalendarIcon, ChevronLeft, ChevronRight, ChevronDown,
  Loader2, AlertCircle, RefreshCw, Briefcase, User as UserIcon, X, Plus,
} from 'lucide-react';
import toast from 'react-hot-toast';
import {
  staffCalendarApi,
  type StaffCalendarItem,
  type StaffCalendarPersonalEvent,
  type StaffCalendarDetail,
} from '../../../../features/dashboard/api/staffCalendarApi';
import { delegationsApi } from '../../../../features/delegations/api/delegationsApi';
import { departmentReceptionTasksApi } from '../../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import { notificationsApi } from '../../../../features/notifications/api/notificationsApi';
import { useNotifications } from '../../../../features/notifications/context/NotificationsContext';
import { getNotificationLink } from '../../../../features/notifications/components/NotificationBellButton';
import { NotificationDetailModal } from '../../../../features/notifications/components/NotificationDetailModal';
import { HostFeedbackModal } from '../../../../features/feedbacks/components/HostFeedbackModal';
import { VisitorFeedbackDetailModal } from '../../../../features/feedbacks/components/VisitorFeedbackDetailModal';
import type { NotificationItem } from '../../../../features/notifications/types/notification.types';
import { matchCalendarChangeNotifs } from '../../../../features/notifications/utils/calendarChangeNotifs';
import { useAuth } from '../../../../shared/hooks/useAuth';
import { useNavigate } from 'react-router-dom';
import { AssignHostModal } from '../../../../components/modals/AssignHostModal';
import {
  isInstanceVersionConflict,
  INSTANCE_VERSION_CONFLICT_MESSAGE,
} from '../../../../features/visit-request/utils/decisionConflict';
import { StaffVisitDetailModal } from './StaffVisitDetailModal';
import { formatVietnamTime, toVietnamCalendarDate } from '../../../../shared/utils/vietnamTime';

type DisplayMode = 'Ngày' | 'Tuần' | 'Tháng' | 'Năm';
type CalendarType = 'office' | 'mine';

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

const WEEKDAYS = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];
const MONTH_NAMES = [
  'Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6',
  'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12',
];

/** Legend đúng nhóm nghiệp vụ yêu cầu đến thăm (không dùng "thư mời"/"đơn yêu cầu").
 *  Đơn bị hủy hiển thị bằng gạch ngang chữ;
 *  "Lịch cá nhân" (tím) chỉ hiện ở "Lịch của tôi" — ở lịch văn phòng nó gộp màu xanh dương với MINE. */
const LEGEND: { key: string; label: string; dot: string }[] = [
  { key: 'NEEDS_ACTION', label: 'Cần xử lý', dot: 'bg-amber-400' },
  { key: 'PROCESSED', label: 'Đã có người phụ trách', dot: 'bg-emerald-500' },
  { key: 'MINE', label: 'Đơn phụ trách', dot: 'bg-[#004c91]' },
  { key: 'PERSONAL', label: 'Lịch cá nhân', dot: 'bg-purple-400' },
  { key: 'CANCELLED', label: 'Hủy', dot: 'bg-slate-400' },
];

const PILL_CLASS: Record<string, string> = {
  NEEDS_ACTION: 'bg-amber-50 text-amber-800 border-amber-300 hover:bg-amber-100',
  MINE: 'bg-blue-50 text-[#004c91] border-blue-300 hover:bg-blue-100',
  PROCESSED: 'bg-emerald-50 text-emerald-700 border-emerald-300 hover:bg-emerald-100',
  CANCELLED_OR_EXPIRED: 'bg-slate-100 text-slate-500 border-slate-200 hover:bg-slate-200',
  NEUTRAL: 'bg-slate-50 text-slate-600 border-slate-200 hover:bg-slate-100',
  PERSONAL: 'bg-purple-100 text-purple-800 border-purple-400 hover:bg-purple-200',
};

const pad2 = (n: number) => String(n).padStart(2, '0');
const toDateKey = (d: Date) => `${d.getUTCFullYear()}-${pad2(d.getUTCMonth() + 1)}-${pad2(d.getUTCDate())}`;
const addDays = (d: Date, days: number) => {
  const r = new Date(d);
  r.setUTCDate(r.getUTCDate() + days);
  return r;
};
/** Thứ Hai đầu tuần của một ngày bất kỳ (nhận UTC carrier Date). */
const startOfWeek = (d: Date) => {
  const r = new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth(), d.getUTCDate()));
  const day = r.getUTCDay(); // 0=CN
  return addDays(r, day === 0 ? -6 : 1 - day);
};
// Giờ Việt Nam cố định — pill giờ không đổi theo timezone browser.
const fmtTime = (value: string) => formatVietnamTime(value);
/** Parse business datetime → Date re-based để mọi getter local trả phần giờ VN. */
const toVnDate = (value: string) => toVietnamCalendarDate(value) ?? new Date(NaN);

export function StaffDashboardCalendar({ isStaffLeader }: { user?: any; isStaffLeader?: boolean }) {
  // "Hôm nay" theo lịch Việt Nam (ô today không lệch ngày ở browser nước ngoài).
  const today = toVietnamCalendarDate(new Date())!;
  const todayKey = toDateKey(today);
  const todayStr = todayKey;

  // ── Điều hướng lịch ── (chế độ hiển thị gồm cả Năm — không tách bộ lọc riêng)
  const [displayMode, setDisplayMode] = useState<DisplayMode>('Tháng');
  // View Ngày có thể mở từ lưới Tháng hoặc Tuần — nút "Quay lại" trả về đúng nơi xuất phát.
  const [dayViewReturnMode, setDayViewReturnMode] = useState<'Tháng' | 'Tuần'>('Tháng');
  const [anchorDate, setAnchorDate] = useState<Date>(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate())));
  // Staff Leader mặc định xem lịch văn phòng; Staff thường mặc định xem lịch của tôi.
  const [calendarType, setCalendarType] = useState<CalendarType>(isStaffLeader ? 'office' : 'mine');
  const [showModeDropdown, setShowModeDropdown] = useState(false);

  // ── Data ──
  const [items, setItems] = useState<StaffCalendarItem[]>([]);
  const [personalEvents, setPersonalEvents] = useState<StaffCalendarPersonalEvent[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // ── Modal chi tiết + action ──
  const [detailInstanceId, setDetailInstanceId] = useState<number | null>(null);
  const [detailRefreshKey, setDetailRefreshKey] = useState(0);

  // Từ chối yêu cầu (Staff Leader — campus-reject).
  const [reject, setReject] = useState<{
    open: boolean; detail: StaffCalendarDetail | null; text: string; submitting: boolean; error: string | null;
  }>({ open: false, detail: null, text: '', submitting: false, error: null });

  // Gán host: chỉ 1 bước chọn host (không email, không accept/decline).
  const [assign, setAssign] = useState<{ open: boolean; detail: StaffCalendarDetail | null }>({ open: false, detail: null });
  // "Tôi là người phụ trách": Staff Leader tự nhận host, submit ngay không cần mở modal chọn.
  const [selfHostSubmittingId, setSelfHostSubmittingId] = useState<number | null>(null);

  // Tạo lịch cá nhân (nút + trên mỗi ngày).
  const [addEvent, setAddEvent] = useState<{
    open: boolean; date: string; title: string; description: string;
    startTime: string; endTime: string; submitting: boolean; error: string | null;
  }>({ open: false, date: todayStr, title: '', description: '', startTime: '09:00', endTime: '10:00', submitting: false, error: null });

  // Xem / Sửa / Xóa lịch cá nhân
  const [personalEventModal, setPersonalEventModal] = useState<{
    open: boolean; eventId?: number | string; title: string; description: string;
    date: string; startTime: string; endTime: string; submitting: boolean; error?: string | null; isEditing?: boolean;
  }>({ open: false, title: '', description: '', date: '', startTime: '', endTime: '', submitting: false });

  const openPersonalEventModal = (ev: StaffCalendarPersonalEvent) => {
    const rawId = ev.calendarEventId || (ev as any).id;
    const sd = toVnDate(ev.startAt);
    const ed = toVnDate(ev.endAt);
    const dateStr = toDateKey(sd);
    const startTimeStr = `${pad2(sd.getUTCHours())}:${pad2(sd.getUTCMinutes())}`;
    const endTimeStr = `${pad2(ed.getUTCHours())}:${pad2(ed.getUTCMinutes())}`;
    setPersonalEventModal({
      open: true,
      eventId: rawId,
      title: ev.title || '',
      description: ev.description || '',
      date: dateStr,
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
      await fetchCalendar();
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
      await fetchCalendar();
    } catch (err: any) {
      setPersonalEventModal((s) => ({
        ...s, submitting: false,
        error: err?.response?.data?.message || err?.response?.data?.title || 'Lỗi khi xóa lịch cá nhân.',
      }));
    }
  };

  // ── Thông báo chưa đọc gắn với đơn tham quan — chấm đỏ nháy + "Thay đổi mới" (giống Dept Leader) ──
  const navigate = useNavigate();
  const { user: authUser } = useAuth();
  const { markAsRead: markNotificationRead } = useNotifications();
  const [changeNotifs, setChangeNotifs] = useState<NotificationItem[]>([]);
  const [changeNotifDetail, setChangeNotifDetail] = useState<NotificationItem | null>(null);
  // Feedback nhận được (Visitor đánh giá / Host chấm điểm) phải mở modal xem nội dung — không
  // điều hướng sang trang setup đoàn (xem SubmitVisitFeedbackCommandHandler.cs, cùng pattern
  // NotificationsPage.tsx / NotificationBellButton.tsx).
  const [hostFeedbackVisitInstanceId, setHostFeedbackVisitInstanceId] = useState<number | null>(null);
  const [visitorFeedbackVisitInstanceId, setVisitorFeedbackVisitInstanceId] = useState<number | null>(null);

  const fetchChangeNotifs = useCallback(async () => {
    try {
      const res = await notificationsApi.getNotifications({ page: 1, pageSize: 50, isRead: false });
      setChangeNotifs(res?.items || []);
    } catch (e) { console.error(e); }
  }, []);

  const getVisitChangeNotifs = useCallback((item: StaffCalendarItem): NotificationItem[] =>
    matchCalendarChangeNotifs(changeNotifs, {
      itemType: 'VISIT',
      rawId: item.visitInstanceId,
      visitRequestId: item.visitRequestId,
      visitInstanceId: item.visitInstanceId,
    }), [changeNotifs]);

  /** Bấm 1 thay đổi trong modal: đánh dấu đã đọc rồi trỏ tới đúng chỗ như thông báo. */
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
      setDetailInstanceId(null);
      navigate(link);
    } else {
      setChangeNotifDetail(n);
    }
  };

  // ── Khoảng ngày hiển thị (và fetch) theo chế độ xem ──
  const { gridStart, gridEnd, monthCells } = useMemo(() => {
    if (displayMode === 'Ngày') {
      const d = new Date(anchorDate);
      return { gridStart: d, gridEnd: d, monthCells: [] as Date[] };
    }
    if (displayMode === 'Tuần') {
      const start = startOfWeek(anchorDate);
      return { gridStart: start, gridEnd: addDays(start, 6), monthCells: [] as Date[] };
    }
    if (displayMode === 'Năm') {
      return {
        gridStart: new Date(Date.UTC(anchorDate.getUTCFullYear(), 0, 1)),
        gridEnd: new Date(Date.UTC(anchorDate.getUTCFullYear(), 11, 31)),
        monthCells: [] as Date[],
      };
    }
    // Tháng: lưới Monday-first phủ trọn tháng của anchorDate.
    const firstOfMonth = new Date(Date.UTC(anchorDate.getUTCFullYear(), anchorDate.getUTCMonth(), 1));
    const lastOfMonth = new Date(Date.UTC(anchorDate.getUTCFullYear(), anchorDate.getUTCMonth() + 1, 0));
    const start = startOfWeek(firstOfMonth);
    const totalDays = Math.ceil(((lastOfMonth.getTime() - start.getTime()) / 86400000 + 1) / 7) * 7;
    const cells: Date[] = [];
    for (let i = 0; i < totalDays; i++) cells.push(addDays(start, i));
    return { gridStart: start, gridEnd: cells[cells.length - 1], monthCells: cells };
  }, [displayMode, anchorDate]);

  const fromStr = toDateKey(gridStart);
  const toStr = toDateKey(gridEnd);

  const fetchCalendar = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = displayMode === 'Năm'
        ? await staffCalendarApi.getCalendar({ viewMode: calendarType, from: fromStr, to: toStr, year: anchorDate.getUTCFullYear() })
        : await staffCalendarApi.getCalendar({ viewMode: calendarType, from: fromStr, to: toStr });
      setItems(res?.items || []);
      setPersonalEvents(res?.personalEvents || []);
    } catch (e: any) {
      setError('Không thể tải lịch yêu cầu đến thăm. Vui lòng thử lại.');
      setItems([]);
      setPersonalEvents([]);
    } finally {
      setLoading(false);
    }
  }, [calendarType, fromStr, toStr, displayMode, anchorDate]);

  useEffect(() => {
    fetchCalendar();
    fetchChangeNotifs();
  }, [fetchCalendar, fetchChangeNotifs]);

  // ── Gộp yêu cầu đến thăm + lịch cá nhân thành 1 danh sách pill hiển thị chung ──
  type CalendarPill =
    | { kind: 'visit'; key: string; startAt: string; endAt: string; item: StaffCalendarItem }
    | { kind: 'personal'; key: string; startAt: string; endAt: string; event: StaffCalendarPersonalEvent };

  const allPills = useMemo<CalendarPill[]>(() => [
    ...items.map((item): CalendarPill => ({ kind: 'visit', key: `v_${item.visitInstanceId}`, startAt: item.plannedStartAt, endAt: item.plannedEndAt, item })),
    ...personalEvents.map((event): CalendarPill => ({ kind: 'personal', key: `p_${event.calendarEventId}`, startAt: event.startAt, endAt: event.endAt, event })),
  ], [items, personalEvents]);

  // ── Gom theo ngày (một yêu cầu kéo dài nhiều ngày sẽ hiện ở mọi ngày nó phủ) ──
  const eventsByDay = useMemo(() => {
    const map: Record<string, CalendarPill[]> = {};
    const rangeStart = new Date(Date.UTC(gridStart.getUTCFullYear(), gridStart.getUTCMonth(), gridStart.getUTCDate()));
    const rangeEnd = new Date(Date.UTC(gridEnd.getUTCFullYear(), gridEnd.getUTCMonth(), gridEnd.getUTCDate()));
    for (const pill of allPills) {
      const s = toVnDate(pill.startAt);
      const e = toVnDate(pill.endAt);
      let d = new Date(Math.max(rangeStart.getTime(), new Date(Date.UTC(s.getUTCFullYear(), s.getUTCMonth(), s.getUTCDate())).getTime()));
      const end = new Date(Math.min(rangeEnd.getTime(), new Date(Date.UTC(e.getUTCFullYear(), e.getUTCMonth(), e.getUTCDate())).getTime()));
      while (d <= end) {
        const key = toDateKey(d);
        (map[key] = map[key] || []).push(pill);
        d = addDays(d, 1);
      }
    }
    Object.values(map).forEach((list) =>
      list.sort((a, b) => new Date(a.startAt).getTime() - new Date(b.startAt).getTime()));
    return map;
  }, [allPills, gridStart, gridEnd]);

  const yearMonths = useMemo(() => Array.from({ length: 12 }, (_, m) => {
    const monthPills = allPills.filter((p) => {
      const d = toVnDate(p.startAt);
      return d.getUTCFullYear() === anchorDate.getUTCFullYear() && d.getUTCMonth() === m;
    });
    return { month: m, pills: monthPills };
  }), [allPills, anchorDate]);

  // ── Điều hướng ──
  const goPrev = () => {
    if (displayMode === 'Năm') setAnchorDate((d) => new Date(Date.UTC(d.getUTCFullYear() - 1, d.getUTCMonth(), 1)));
    else if (displayMode === 'Tháng') setAnchorDate((d) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() - 1, 1)));
    else setAnchorDate((d) => addDays(d, displayMode === 'Tuần' ? -7 : -1));
  };
  const goNext = () => {
    if (displayMode === 'Năm') setAnchorDate((d) => new Date(Date.UTC(d.getUTCFullYear() + 1, d.getUTCMonth(), 1)));
    else if (displayMode === 'Tháng') setAnchorDate((d) => new Date(Date.UTC(d.getUTCFullYear(), d.getUTCMonth() + 1, 1)));
    else setAnchorDate((d) => addDays(d, displayMode === 'Tuần' ? 7 : 1));
  };
  const goToday = () => setAnchorDate(new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), today.getUTCDate())));

  const headerLabel = displayMode === 'Năm'
    ? `Năm ${anchorDate.getUTCFullYear()}`
    : displayMode === 'Tháng'
      ? `${MONTH_NAMES[anchorDate.getUTCMonth()]} ${anchorDate.getUTCFullYear()}`
      : displayMode === 'Tuần'
        ? `${pad2(gridStart.getUTCDate())}/${pad2(gridStart.getUTCMonth() + 1)} – ${pad2(gridEnd.getUTCDate())}/${pad2(gridEnd.getUTCMonth() + 1)}/${gridEnd.getUTCFullYear()}`
        : `${pad2(anchorDate.getUTCDate())}/${pad2(anchorDate.getUTCMonth() + 1)}/${anchorDate.getUTCFullYear()}`;

  // ── Refresh sau action (không reload trang) ──
  const refreshAfterAction = useCallback(async () => {
    setDetailRefreshKey((k) => k + 1);
    await Promise.all([fetchCalendar(), fetchChangeNotifs()]);
  }, [fetchCalendar, fetchChangeNotifs]);

  // ── Flow từ chối (Staff Leader) — reject theo campus instance (campus-independent approval) ──
  const submitReject = async () => {
    if (!reject.detail || !reject.text.trim()) return;
    setReject((s) => ({ ...s, submitting: true, error: null }));
    try {
      await delegationsApi.rejectCampusInstance(
        reject.detail.visitRequestId, reject.detail.visitInstanceId, reject.text.trim(),
        reject.detail.rowVersion);
      toast.success('Đã từ chối tiếp nhận tại cơ sở này.');
      setReject({ open: false, detail: null, text: '', submitting: false, error: null });
      await refreshAfterAction();
    } catch (e: any) {
      // Phiên bản cũ: đóng hộp thoại và tải lại thay vì để người duyệt bấm lại — nội dung họ vừa
      // đọc không còn là nội dung sẽ bị từ chối.
      if (isInstanceVersionConflict(e)) {
        setReject({ open: false, detail: null, text: '', submitting: false, error: null });
        toast.error(INSTANCE_VERSION_CONFLICT_MESSAGE);
        await refreshAfterAction();
        return;
      }
      setReject((s) => ({
        ...s, submitting: false,
        error: e?.response?.data?.message || e?.response?.data?.title || 'Từ chối thất bại. Vui lòng thử lại.',
      }));
    }
  };

  // ── Flow gán host: chỉ chọn host rồi gọi API luôn (không email, không accept/decline) ──
  const handleHostAssigned = async () => {
    toast.success('Đã gán người phụ trách.');
    setAssign({ open: false, detail: null });
    await refreshAfterAction();
  };

  // ── Flow "Tôi là người phụ trách": tự nhận host, submit ngay (không mở modal chọn) ──
  const submitSelfHost = async (detail: StaffCalendarDetail) => {
    setSelfHostSubmittingId(detail.visitInstanceId);
    try {
      const candidates = await delegationsApi.getHostCandidates(detail.visitInstanceId);
      const selfOption = candidates.find((c) => c.isStaffLeaderSelfHostOption);
      if (!selfOption) {
        toast.error('Không tìm thấy lựa chọn tự làm người phụ trách cho cơ sở này.');
        return;
      }
      await delegationsApi.approveCampusInstance(
        detail.visitRequestId, detail.visitInstanceId, selfOption.userId, undefined, detail.rowVersion);
      toast.success('Bạn đã trở thành người phụ trách đoàn khách này.');
      await refreshAfterAction();
    } catch (e: any) {
      if (isInstanceVersionConflict(e)) {
        toast.error(INSTANCE_VERSION_CONFLICT_MESSAGE);
        await refreshAfterAction();
        return;
      }
      toast.error(e?.response?.data?.message || e?.response?.data?.title || 'Không thể tự nhận làm người phụ trách. Vui lòng thử lại.');
    } finally {
      setSelfHostSubmittingId(null);
    }
  };

  // ── Tạo lịch cá nhân ──
  const openAddEvent = (dateStr: string) => {
    if (dateStr < todayStr) {
      toast.error('Không thể tạo lịch trong quá khứ. Vui lòng chọn ngày từ hôm nay trở đi.');
      return;
    }
    setAddEvent({ open: true, date: dateStr, title: '', description: '', startTime: '09:00', endTime: '10:00', submitting: false, error: null });
  };

  const submitAddEvent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!addEvent.title.trim()) return;
    setAddEvent((s) => ({ ...s, submitting: true, error: null }));
    try {
      await departmentReceptionTasksApi.createPersonalEvent(
        addEvent.title.trim(), addEvent.description, addEvent.date, addEvent.startTime, addEvent.endTime,
      );
      toast.success('Đã lưu lịch cá nhân.');
      setAddEvent((s) => ({ ...s, open: false, submitting: false }));
      await fetchCalendar();
    } catch (err: any) {
      setAddEvent((s) => ({
        ...s, submitting: false,
        error: err?.response?.data?.message || err?.response?.data?.title || 'Lỗi khi lưu lịch cá nhân.',
      }));
    }
  };

  // ── Render 1 pill (yêu cầu đến thăm hoặc lịch cá nhân) ── (`key` khai báo trong props type
  // vì project thiếu @types/react đầy đủ nên JSX không tự loại trừ key khỏi props check)
  const EventPill = ({ pill, full = false }: { pill: CalendarPill; full?: boolean; key?: string | number }) => {
    if (pill.kind === 'personal') {
      const ev = pill.event;
      const personalClass = calendarType === 'mine' ? PILL_CLASS.PERSONAL : PILL_CLASS.MINE;
      return (
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            openPersonalEventModal(ev);
          }}
          title={`${ev.title} — Lịch cá nhân (bấm để xem/sửa/xóa)`}
          className={`w-full text-left border rounded-lg transition-colors cursor-pointer ${personalClass} ${full ? 'px-3 py-2' : 'px-1.5 py-0.5'}`}
        >
          {full ? (
            <>
              <p className="text-xs font-normal truncate">{ev.title}</p>
              <p className="text-[11px] font-normal opacity-80 mt-0.5">
                {fmtTime(ev.startAt)} – {fmtTime(ev.endAt)} · Lịch cá nhân
              </p>
            </>
          ) : (
            <p className="text-[10px] font-normal truncate leading-4">{fmtTime(ev.startAt)} {ev.title}</p>
          )}
        </button>
      );
    }
    const item = pill.item;
    const hasChanges = getVisitChangeNotifs(item).length > 0;
    return (
      <button
        type="button"
        onClick={(e) => { e.stopPropagation(); setDetailInstanceId(item.visitInstanceId); }}
        title={`${item.title} — ${item.displayStatus}${hasChanges ? ' — Có thay đổi mới' : ''}`}
        className={`relative w-full text-left border rounded-lg transition-colors cursor-pointer ${hasChanges ? 'pr-5' : ''} ${PILL_CLASS[item.colorType] || PILL_CLASS.NEUTRAL} ${full ? 'px-3 py-2' : 'px-1.5 py-0.5'}`}
      >
        {full ? (
          <>
            <p className={`text-xs font-normal truncate ${item.isCancelled ? 'line-through' : ''}`}>{item.title}</p>
            <p className="text-[11px] font-normal opacity-80 mt-0.5">
              {fmtTime(item.plannedStartAt)} – {fmtTime(item.plannedEndAt)} · {item.displayStatus}
              {item.currentHostName ? ` · Người phụ trách: ${item.currentHostName}` : ''}
            </p>
          </>
        ) : (
          <p className={`text-[10px] font-normal truncate leading-4 ${item.isCancelled ? 'line-through' : ''}`}>
            {fmtTime(item.plannedStartAt)} {item.title}
          </p>
        )}
        {hasChanges && (
          <span className="absolute top-1 right-1 flex h-2 w-2" title="Đơn này có thay đổi mới">
            <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-red-400 opacity-75" />
            <span className="relative inline-flex h-2 w-2 rounded-full bg-red-500" />
          </span>
        )}
      </button>
    );
  };

  const DayAddButton = ({ dateStr }: { dateStr: string }) => (
    <button
      type="button"
      onClick={(e) => { e.stopPropagation(); openAddEvent(dateStr); }}
      className="opacity-0 group-hover:opacity-100 text-[#f37021] hover:text-[#004c91] transition-opacity p-0.5 hover:bg-orange-100 rounded-md cursor-pointer shrink-0"
      title="Thêm lịch cá nhân"
    >
      <Plus className="w-3.5 h-3.5" />
    </button>
  );

  const renderDayColumn = (date: Date, compactHeader = false) => {
    const key = toDateKey(date);
    const dayEvents = eventsByDay[key] || [];
    const isToday = key === todayKey;
    const isPast = key < todayKey;
    return (
      <div
        key={key}
        onClick={() => {
          // Bấm vào 1 ô ngày trong tuần → mở thẳng view Ngày (nút quay lại trả về Tuần).
          if (dayEvents.length > 0) {
            setAnchorDate(new Date(date));
            setDayViewReturnMode('Tuần');
            setDisplayMode('Ngày');
          }
        }}
        className={`group relative flex-1 min-w-0 rounded-xl border p-2 ${dayEvents.length > 0 ? 'cursor-pointer' : ''} ${isToday ? 'border-[#004c91]/50 bg-blue-50/40' : 'border-slate-200 bg-white'}`}>
        <div className="flex items-center justify-between mb-2">
          <p className={`text-xs font-bold ${isToday ? 'text-[#004c91]' : 'text-slate-500'}`}>
            {compactHeader
              ? `${WEEKDAYS[(date.getUTCDay() + 6) % 7]} ${pad2(date.getUTCDate())}/${pad2(date.getUTCMonth() + 1)}`
              : `${pad2(date.getUTCDate())}/${pad2(date.getUTCMonth() + 1)}`}
          </p>
          <DayAddButton dateStr={key} />
        </div>
        <div className="space-y-1.5">
          {dayEvents.length === 0 ? (
            <p className="text-[11px] text-slate-400 italic">Trống</p>
          ) : (
            dayEvents.map((p) => <EventPill key={`${p.key}_${key}`} pill={p} full />)
          )}
        </div>
        {/* Lớp xám phủ lên các ngày trong quá khứ */}
        {isPast && (
          <div className="absolute inset-0 bg-slate-300/45 pointer-events-none z-10 rounded-xl" aria-hidden="true" />
        )}
      </div>
    );
  };

  // ── Chế độ Năm: lưới 12 tháng, mỗi ô đếm số yêu cầu đến thăm trong tháng đó ──
  const yearMonthCounts = useMemo(() => {
    const counts = Array(12).fill(0);
    for (const pill of allPills) {
      const d = toVnDate(pill.startAt);
      if (d.getUTCFullYear() === anchorDate.getUTCFullYear()) counts[d.getUTCMonth()] += 1;
    }
    return counts;
  }, [allPills, anchorDate]);

  return (
    <div className="space-y-4">
      {/* ── Toolbar ── */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-sm px-4 py-3 flex flex-wrap items-center justify-between gap-3">
        {/* Bên trái: Điều hướng thời gian + Bộ lọc (cạnh nút sang trái sang phải) */}
        <div className="flex items-center gap-3 flex-wrap">
          {/* Điều hướng */}
          <div className="flex items-center gap-1">
            <button onClick={goToday} className="mr-1.5 text-xs font-bold text-[#004c91] bg-blue-50 hover:bg-blue-100 px-3 py-1.5 rounded-lg transition-colors cursor-pointer">
              Hôm nay
            </button>
            <button onClick={goPrev} className="w-8 h-8 rounded-lg hover:bg-slate-100 flex items-center justify-center text-slate-500 hover:text-[#004c91] transition-colors cursor-pointer">
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="text-sm font-extrabold text-slate-700 min-w-[130px] text-center">{headerLabel}</span>
            <button onClick={goNext} className="w-8 h-8 rounded-lg hover:bg-slate-100 flex items-center justify-center text-slate-500 hover:text-[#004c91] transition-colors cursor-pointer">
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>

          {/* Bộ lọc (chuyển sang trái cạnh nút điều hướng) */}
          <div className="flex items-center gap-2">
            {/* Loại lịch */}
            <div className="flex rounded-xl border border-slate-200 overflow-hidden">
              <button
                onClick={() => setCalendarType('office')}
                className={`px-3 py-1.5 text-xs font-bold flex items-center gap-1.5 transition-colors cursor-pointer ${calendarType === 'office' ? 'bg-[#004c91] text-white' : 'bg-white text-slate-600 hover:bg-slate-50'}`}
              >
                <Briefcase className="w-3.5 h-3.5" /> Lịch văn phòng
              </button>
              <button
                onClick={() => setCalendarType('mine')}
                className={`px-3 py-1.5 text-xs font-bold flex items-center gap-1.5 transition-colors cursor-pointer ${calendarType === 'mine' ? 'bg-[#004c91] text-white' : 'bg-white text-slate-600 hover:bg-slate-50'}`}
              >
                <UserIcon className="w-3.5 h-3.5" /> Lịch của tôi
              </button>
            </div>

            {/* Chế độ hiển thị */}
            <div className="relative">
              <button
                onClick={() => setShowModeDropdown((v) => !v)}
                className="px-3 py-1.5 text-xs font-bold text-slate-600 bg-white border border-slate-200 rounded-xl hover:bg-slate-50 flex items-center gap-1.5 transition-colors cursor-pointer"
              >
                {displayMode} <ChevronDown className="w-3.5 h-3.5" />
              </button>
              {showModeDropdown && (
                <div className="absolute left-0 top-full mt-1 z-20 bg-white border border-slate-200 rounded-xl shadow-lg overflow-hidden min-w-[110px]">
                  {(['Ngày', 'Tuần', 'Tháng', 'Năm'] as DisplayMode[]).map((m) => (
                    <button
                      key={m}
                      onClick={() => { setDisplayMode(m); setShowModeDropdown(false); }}
                      className={`w-full text-left px-3.5 py-2 text-xs font-bold transition-colors cursor-pointer ${displayMode === m ? 'bg-blue-50 text-[#004c91]' : 'text-slate-600 hover:bg-slate-50'}`}
                    >
                      {m}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Bên phải: Chú thích (Legend) nằm bên phải phần lọc */}
        <div className="flex items-center gap-x-4 gap-y-1.5 flex-wrap ml-auto">
          {LEGEND.filter((l) =>
            (l.key !== 'NEEDS_ACTION' || (isStaffLeader && calendarType === 'office'))
            && (l.key !== 'PROCESSED' || calendarType === 'office')
            && (l.key !== 'PERSONAL' || calendarType === 'mine'),
          ).map((l) => (
            <span key={l.key} className="flex items-center gap-1.5 text-[11px] font-normal text-slate-600">
              <span className={`w-2.5 h-2.5 rounded-full ${l.dot}`} />
              <span className={l.key === 'CANCELLED' ? 'line-through text-slate-500 font-normal' : ''}>
                {l.key === 'MINE' && calendarType === 'office' ? 'Lịch của tôi' : l.label}
              </span>
            </span>
          ))}
          {loading && <Loader2 className="w-4 h-4 animate-spin text-[#004c91]" />}
        </div>
      </div>

      {/* ── Bảng lịch ── */}
      <div className="bg-white border border-slate-200 rounded-2xl shadow-sm overflow-hidden">

        {error ? (
          <div className="py-14 text-center">
            <AlertCircle className="w-8 h-8 mx-auto mb-2 text-red-400" />
            <p className="text-sm font-normal text-red-600 mb-3">{error}</p>
            <button
              onClick={fetchCalendar}
              className="text-xs font-bold text-[#004c91] bg-blue-50 hover:bg-blue-100 px-4 py-2 rounded-xl inline-flex items-center gap-1.5 transition-colors cursor-pointer"
            >
              <RefreshCw className="w-3.5 h-3.5" /> Thử lại
            </button>
          </div>
        ) : displayMode === 'Tháng' ? (
          <div className="p-3">
            <div className="grid grid-cols-7 mb-1">
              {WEEKDAYS.map((wd) => (
                <div key={wd} className="text-center text-[10px] font-extrabold text-slate-400 uppercase tracking-widest py-1.5">{wd}</div>
              ))}
            </div>
            <div className="grid grid-cols-7 gap-1">
              {monthCells.map((date) => {
                const key = toDateKey(date);
                const inMonth = date.getUTCMonth() === anchorDate.getUTCMonth();
                const isToday = key === todayKey;
                const isPast = key < todayKey;
                const dayEvents = eventsByDay[key] || [];
                const visible = dayEvents.slice(0, 5);
                const more = dayEvents.length - visible.length;
                return (
                  <div
                    key={key}
                    onClick={() => {
                      // Bấm vào ô ngày có lịch → mở thẳng view Ngày (giống Dept Leader).
                      if (inMonth && dayEvents.length > 0) {
                        setAnchorDate(new Date(date));
                        setDayViewReturnMode('Tháng');
                        setDisplayMode('Ngày');
                      }
                    }}
                    className={`group relative min-h-[160px] rounded-lg border p-1 flex flex-col gap-0.5 transition-colors ${inMonth && dayEvents.length > 0 ? 'cursor-pointer' : ''}
                      ${isToday ? 'border-[#004c91]/80 ring-1 ring-[#004c91]/40 bg-blue-50/50' : 'border-slate-300'}
                      ${!inMonth ? 'bg-slate-50/60 border-slate-200' : 'bg-white'}`}
                  >
                    {/* Chỉ hiển thị ngày thuộc tháng đang xem — ô ngoài tháng để trống (giống Dept Leader). */}
                    {inMonth && (
                      <>
                        <div className="flex items-center justify-between">
                          <span className={`text-[11px] font-bold px-1
                            ${isToday ? 'text-white bg-[#004c91] rounded-md px-1.5 py-0.5' : isPast ? 'text-slate-400' : 'text-slate-600'}`}>
                            {date.getUTCDate()}
                          </span>
                          <DayAddButton dateStr={key} />
                        </div>
                        {visible.map((p) => <EventPill key={`${p.key}_${key}`} pill={p} />)}
                        {more > 0 && (
                          <button
                            type="button"
                            onClick={(e) => { e.stopPropagation(); setAnchorDate(new Date(date)); setDayViewReturnMode('Tháng'); setDisplayMode('Ngày'); }}
                            className="text-[10px] font-bold text-[#004c91] hover:underline text-left px-1 cursor-pointer"
                          >
                            +{more} khác
                          </button>
                        )}
                        {/* Lớp xám phủ lên các ngày trong quá khứ */}
                        {isPast && !isToday && (
                          <div className="absolute inset-0 bg-slate-300/45 pointer-events-none z-10 rounded-lg" aria-hidden="true" />
                        )}
                      </>
                    )}
                  </div>
                );
              })}
            </div>
            {!loading && items.length === 0 && (
              <p className="text-center text-sm text-slate-400 font-normal py-4">
                Không có yêu cầu đến thăm trong khoảng thời gian này.
              </p>
            )}
          </div>
        ) : displayMode === 'Tuần' ? (
          <div className="p-3">
            <div className="grid grid-cols-2 md:grid-cols-4 xl:grid-cols-7 gap-2">
              {Array.from({ length: 7 }, (_, i) => addDays(gridStart, i)).map((d) => renderDayColumn(d, true))}
            </div>
            {!loading && items.length === 0 && (
              <p className="text-center text-sm text-slate-400 font-normal py-4">
                Không có yêu cầu đến thăm trong khoảng thời gian này.
              </p>
            )}
          </div>
        ) : displayMode === 'Năm' ? (
          <div className="p-6 overflow-y-auto no-scrollbar max-h-[720px] bg-white">
            <h3 className="text-sm font-extrabold text-[#004c91] uppercase tracking-wider mb-6 text-center">
              Tổng quan danh mục sự kiện năm {anchorDate.getUTCFullYear()}
            </h3>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {MONTH_NAMES.map((mName, mIdx) => {
                const mDays = getDaysForYearMonth(anchorDate.getUTCFullYear(), mIdx);
                const isCurrentMonthThisYear = mIdx === today.getUTCMonth() && anchorDate.getUTCFullYear() === today.getUTCFullYear();
                return (
                  <div
                    key={mName}
                    className={`p-3.5 rounded-2xl border transition-colors ${
                      isCurrentMonthThisYear ? 'border-orange-300 bg-orange-50/20' : 'bg-slate-50/50 border-slate-150/80 hover:border-orange-200'
                    }`}
                  >
                    <h4 className="text-xs font-black text-[#004c91] text-center mb-2.5">{mName}</h4>

                    <div className="grid grid-cols-7 text-center text-[9px] font-black text-slate-400 mb-1">
                      {WEEKDAYS.map((w) => (
                        <div key={w}>{w}</div>
                      ))}
                    </div>

                    <div className="grid grid-cols-7 text-center gap-0.5 text-[10px]">
                      {mDays.map((cell, cIdx) => {
                        const cellKey = `${cell.year}-${String(cell.month + 1).padStart(2, '0')}-${String(cell.day).padStart(2, '0')}`;
                        const cellEvents = eventsByDay[cellKey] || [];
                        const hasEvents = cellEvents.length > 0;
                        const isTodayCell = cellKey === todayKey;

                        return (
                          <button
                            key={cIdx}
                            type="button"
                            onClick={() => {
                              setAnchorDate(new Date(Date.UTC(cell.year, cell.month, cell.day)));
                              setDayViewReturnMode('Tháng');
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
        ) : (
          <div className="p-4">
            <div className="flex items-center justify-between mb-3">
              <button
                type="button"
                onClick={() => setDisplayMode(dayViewReturnMode)}
                className="flex items-center gap-2 px-3.5 py-2 bg-slate-100 hover:bg-slate-200 text-slate-700 text-[11px] font-bold rounded-xl transition-all border border-slate-200/60 cursor-pointer"
              >
                <ChevronLeft className="w-3.5 h-3.5 text-slate-600" />
                <span>{dayViewReturnMode === 'Tuần' ? 'Quay lại lịch tuần' : 'Quay lại lịch tháng'}</span>
              </button>
              <button
                type="button"
                onClick={() => openAddEvent(toDateKey(anchorDate))}
                className="flex items-center gap-1.5 px-3.5 py-2 bg-[#f37021] text-white text-xs font-black rounded-lg hover:opacity-90 active:scale-95 transition-all shadow-sm cursor-pointer"
              >
                <Plus className="w-3.5 h-3.5" />
                <span>Thêm lịch cá nhân</span>
              </button>
            </div>
            {(eventsByDay[toDateKey(anchorDate)] || []).length === 0 && !loading ? (
              <p className="text-center text-sm text-slate-400 font-normal py-8">
                Không có yêu cầu đến thăm trong khoảng thời gian này.
              </p>
            ) : (
              <div className="space-y-2 max-w-2xl mx-auto">
                {(eventsByDay[toDateKey(anchorDate)] || []).map((p) => (
                  <EventPill key={p.key} pill={p} full />
                ))}
              </div>
            )}
          </div>
        )}
      </div>

      {/* ── Modal chi tiết yêu cầu tham quan ── */}
      <StaffVisitDetailModal
        isOpen={detailInstanceId !== null}
        visitInstanceId={detailInstanceId}
        refreshKey={detailRefreshKey}
        onClose={() => setDetailInstanceId(null)}
        onSelfHost={isStaffLeader ? (d) => { void submitSelfHost(d); } : undefined}
        onAssignHost={isStaffLeader ? (d) => setAssign({ open: true, detail: d }) : undefined}
        onReject={isStaffLeader ? (d) => setReject({ open: true, detail: d, text: '', submitting: false, error: null }) : undefined}
        selfHostSubmitting={selfHostSubmittingId !== null && selfHostSubmittingId === detailInstanceId}
        changeNotifs={(() => {
          const openItem = items.find((i) => i.visitInstanceId === detailInstanceId);
          return openItem ? getVisitChangeNotifs(openItem) : [];
        })()}
        onChangeNotifClick={handleChangeNotifClick}
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

      {/* ── Duyệt & gán host trong một bước (campus-independent approval, không gửi email) ── */}
      {assign.open && assign.detail && (
        <AssignHostModal
          isOpen={assign.open}
          visitRequestId={assign.detail.visitRequestId}
          visitInstanceId={assign.detail.visitInstanceId}
          delegationName={assign.detail.delegationName}
          currentHostUserId={assign.detail.currentHostUserId}
          customTitle="Duyệt & gán host"
          // Phiên bản campus đúng như modal chi tiết vừa hiển thị cho người duyệt.
          expectedInstanceRowVersion={assign.detail.rowVersion}
          onClose={() => setAssign({ open: false, detail: null })}
          onConfirmed={() => { void handleHostAssigned(); }}
          // Chỉ tải lại dữ liệu; người duyệt phải tự mở lại và đọc bản mới trước khi quyết định.
          onReloadRequested={() => { void refreshAfterAction(); }}
        />
      )}

      {/* ── Modal từ chối yêu cầu ── */}
      {reject.open && reject.detail && (
        <div className="fixed inset-0 z-[110] flex items-center justify-center bg-slate-900/40 backdrop-blur-sm p-4">
          <div className="bg-white rounded-3xl shadow-2xl w-full max-w-md overflow-hidden">
            <div className="px-6 py-4 bg-red-600 flex items-center justify-between">
              <h3 className="text-lg font-bold text-white flex items-center gap-2">
                <AlertCircle className="w-5 h-5 bg-white/20 rounded-full p-0.5" /> Từ chối yêu cầu đến thăm
              </h3>
              <button
                type="button"
                disabled={reject.submitting}
                onClick={() => setReject({ open: false, detail: null, text: '', submitting: false, error: null })}
                className="text-white/85 hover:text-white hover:bg-white/10 rounded-full p-1.5 cursor-pointer"
              >
                <X className="w-5 h-5" />
              </button>
            </div>
            <div className="p-6">
              <p className="text-sm text-gray-700 mb-3">
                Vui lòng nhập lý do từ chối yêu cầu của đoàn{' '}
                <span className="font-bold text-[#004c91]">{reject.detail.delegationName || reject.detail.requestCode}</span>:
              </p>
              <textarea
                value={reject.text}
                onChange={(e) => setReject((s) => ({ ...s, text: e.target.value }))}
                placeholder="Nhập lý do chi tiết..."
                disabled={reject.submitting}
                className="w-full px-4 py-3 rounded-2xl border border-gray-200 focus:border-red-500 focus:ring-4 focus:ring-red-500/10 outline-none transition-all text-sm min-h-[110px] resize-none bg-gray-50/50 focus:bg-white"
              />
              {reject.error && <p className="text-red-500 text-sm mt-2">{reject.error}</p>}
            </div>
            <div className="px-6 py-4 bg-gray-50 flex items-center justify-end gap-3 border-t border-gray-100">
              <button
                type="button"
                disabled={reject.submitting}
                onClick={() => setReject({ open: false, detail: null, text: '', submitting: false, error: null })}
                className="px-5 py-2 rounded-xl font-bold text-gray-600 hover:bg-gray-200 transition-colors outline-none text-sm cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={!reject.text.trim() || reject.submitting}
                onClick={submitReject}
                className="px-6 py-2 rounded-xl font-bold text-white bg-red-600 hover:bg-red-700 shadow-sm transition-all outline-none text-sm cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {reject.submitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* ── Modal tạo lịch cá nhân (nút + trên mỗi ngày) ── */}
      {addEvent.open && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center z-[110] p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full border border-slate-200 shadow-2xl overflow-hidden animate-fade-in-quick">
            <div className="bg-[#004c91] px-5 py-4 text-white flex justify-between items-center">
              <h3 className="font-black text-sm flex items-center gap-2">
                <CalendarIcon className="w-4 h-4 text-[#f37021]" />
                Lên lịch cá nhân ({addEvent.date})
              </h3>
              <button
                type="button"
                onClick={() => setAddEvent((s) => ({ ...s, open: false }))}
                className="text-white/80 hover:text-white p-1 hover:bg-white/10 rounded-full transition-colors cursor-pointer"
              >
                <X className="w-4 h-4" />
              </button>
            </div>
            <form onSubmit={submitAddEvent} className="p-6 space-y-4 text-xs text-slate-800">
              <div>
                <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">
                  Tiêu đề sự kiện *
                </label>
                <input
                  type="text"
                  required
                  placeholder="VD: Họp định kỳ"
                  value={addEvent.title}
                  onChange={(e) => setAddEvent((s) => ({ ...s, title: e.target.value }))}
                  className="w-full text-xs px-3.5 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">Bắt đầu</label>
                  <input
                    type="time"
                    required
                    value={addEvent.startTime}
                    onChange={(e) => setAddEvent((s) => ({ ...s, startTime: e.target.value }))}
                    className="w-full text-xs px-3 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                  />
                </div>
                <div>
                  <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">Kết thúc</label>
                  <input
                    type="time"
                    required
                    value={addEvent.endTime}
                    min={addEvent.startTime}
                    onChange={(e) => setAddEvent((s) => ({ ...s, endTime: e.target.value }))}
                    className="w-full text-xs px-3 py-2.5 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none bg-slate-50/20"
                  />
                </div>
              </div>
              <div>
                <label className="block text-[10px] font-extrabold text-slate-450 uppercase tracking-wider mb-1">Nội dung</label>
                <textarea
                  rows={4}
                  value={addEvent.description}
                  onChange={(e) => setAddEvent((s) => ({ ...s, description: e.target.value }))}
                  className="w-full text-xs px-3.5 py-2 border border-slate-200 rounded-xl focus:border-[#f37021] outline-none resize-none font-sans bg-slate-50/20"
                />
              </div>
              {addEvent.error && <p className="text-red-500 text-xs">{addEvent.error}</p>}
              <div className="flex justify-end gap-2.5 pt-2 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setAddEvent((s) => ({ ...s, open: false }))}
                  className="py-2.5 px-4 bg-slate-150 hover:bg-slate-200 text-slate-700 font-bold rounded-xl transition-colors cursor-pointer"
                >
                  Đóng
                </button>
                <button
                  type="submit"
                  disabled={!addEvent.title.trim() || addEvent.submitting}
                  className="py-2.5 px-7 bg-[#f37021] text-white font-black rounded-xl hover:opacity-90 active:scale-98 transition-all cursor-pointer shadow-3xs disabled:opacity-50"
                >
                  {addEvent.submitting ? 'Đang lưu...' : 'Xác nhận lưu'}
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
