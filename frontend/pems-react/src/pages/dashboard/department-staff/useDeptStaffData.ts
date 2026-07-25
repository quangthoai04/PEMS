/**
 * Hook tổng hợp data cho Department Staff Dashboard
 * Lấy: lịch calendar (chỉ đơn/thư được phân công cho staff đó)
 *       + danh sách nhiệm vụ được giao (assignments progress)
 *       + danh sách cần chú ý (attention items)
 */

import { useState, useCallback, useMemo } from 'react';
import { departmentReceptionTasksApi } from '../../../features/department-reception-tasks/api/departmentReceptionTasksApi';
import { toVietnamCalendarDate } from '../../../shared/utils/vietnamTime';

// ── Shared Types ──────────────────────────────────────────────────────────────

export type CalendarItem = {
  id: string;
  rawId: number | string;
  itemType: 'INVITATION' | 'REQUEST' | 'PERSONAL';
  visitRequestId?: number;
  visitInstanceId?: number;
  status: string;
  isProcessed: boolean;
  title: string;
  delegationName?: string;
  date: string; // YYYY-MM-DD
  time: string;
  color: string;
  hoverColor: string;
  location: string;
  host: string;
  purpose?: string;
  cancelReason?: string;
};

export type AssignedTask = {
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
  rawStatus: string;
  uiStatus: string;
  statusLabel: string;
  startAt: string;
  endAt: string;
  canAccept: boolean;
  canDecline: boolean;
  canProposeChange: boolean;
  canSignBorrow: boolean;
  canSignReturn: boolean;
  latestDeclineReason?: string;
  latestDeclinedByName?: string;
  latestDeclinedAt?: string;
  needsAttention: boolean;
  attentionReason?: string;
  cancelReason?: string;
  time?: string;
  host?: string;
  location?: string;
  purpose?: string;
  status?: string;
  date?: string;
  rawId?: number | string;
};

export type TaskStatusFilter =
  | 'ALL'
  | 'ACCEPTED'
  | 'DECLINED'
  | 'REJECTED'
  | 'CHANGE_PROPOSED'
  | 'IN_PROGRESS'
  | 'DONE'
  | 'CANCELLED';

// ── Calendar colour logic for staff ──────────────────────────────────────────
// Xanh dương  = staff này đã xử lý (chấp nhận / đang thực hiện / hoàn thành)
// Xanh lá     = thư mời chưa xử lý
// Cam          = đơn yêu cầu chưa xử lý
// Đã hủy      = giữ màu theo loại, chữ gạch ngang khi render (không tô xám)
// Ngày quá khứ được phủ lớp xám ở lưới lịch, không tô xám từng item.
function mapCalendarItem(item: any, idx: number): CalendarItem {
  const itemStatus = item.itemStatus || item.status;
  const rawId = item.itemId || item.logisticsItemId || item.participantId || item.id;
  const isProcessed = itemStatus !== 'REQUESTED' && itemStatus !== 'ASSIGNED';

  let col = '';
  let hCol = '';

  if (itemStatus === 'CANCELLED') {
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
    // Staff đã xử lý → xanh dương
    col = 'bg-blue-50 text-blue-700 border-blue-300 hover:bg-blue-100';
    hCol = 'border-blue-500';
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

  // Re-based: local getters bên dưới trả đúng phần giờ Việt Nam trên mọi browser.
  const sd = toVietnamCalendarDate(item.startAt) ?? new Date(NaN);
  const ed = toVietnamCalendarDate(item.endAt) ?? new Date(NaN);
  const dateStr = `${sd.getUTCFullYear()}-${String(sd.getUTCMonth() + 1).padStart(2, '0')}-${String(sd.getUTCDate()).padStart(2, '0')}`;
  const timeStr = `${String(sd.getUTCHours()).padStart(2, '0')}:${String(sd.getUTCMinutes()).padStart(2, '0')} - ${String(ed.getUTCHours()).padStart(2, '0')}:${String(ed.getUTCMinutes()).padStart(2, '0')}`;

  return {
    id: `${rawId}_${idx}`,
    rawId,
    itemType: item.itemType,
    visitRequestId: item.visitRequestId,
    visitInstanceId: item.visitInstanceId,
    status: itemStatus,
    isProcessed,
    title: itemStatus === 'CANCELLED' ? `${item.title} (đã hủy)` : item.title,
    delegationName: item.delegationName,
    date: dateStr,
    time: timeStr,
    color: col,
    hoverColor: hCol,
    location: item.campusName || 'Hòa Lạc',
    host: item.senderName || 'Hệ thống',
    purpose: itemStatus === 'CANCELLED'
      ? `Đã bị hủy.${item.cancelReason ? ` Lý do: ${item.cancelReason}` : ''}`
      : item.title || '',
    cancelReason: item.cancelReason,
  };
}

// ─────────────────────────────────────────────────────────────────────────────

export type AssignmentsFilter = {
  search: string;
  itemType: string;
  status: TaskStatusFilter;
  fromDate: string;
  toDate: string;
  sortDirection: 'ASC' | 'DESC';
  page: number;
  pageSize: number;
};

export const DEFAULT_FILTER: AssignmentsFilter = {
  search: '',
  itemType: 'ALL',
  status: 'ALL',
  fromDate: '',
  toDate: '',
  sortDirection: 'ASC',
  page: 1,
  pageSize: 10,
};

export function useDeptStaffData(year: number) {
  // ── Calendar ──────────────────────────────────────────────────────────────
  const [calendarItems, setCalendarItems] = useState<CalendarItem[]>([]);
  const [calendarLoading, setCalendarLoading] = useState(false);

  const fetchCalendar = useCallback(async () => {
    setCalendarLoading(true);
    try {
      const res = await departmentReceptionTasksApi.getCalendar(String(year));
      const list = res?.data || res || [];
      if (Array.isArray(list)) {
        // Đơn/thư mời đã từ chối không hiện trên bảng lịch của staff nữa.
        const visible = list.filter((it: any) => {
          const st = it.itemStatus || it.status;
          return st !== 'DECLINED' && st !== 'REJECTED';
        });
        setCalendarItems(visible.map(mapCalendarItem));
      }
    } catch (e) {
      console.error('Fetch calendar error:', e);
    } finally {
      setCalendarLoading(false);
    }
  }, [year]);

  // ── Assigned tasks ────────────────────────────────────────────────────────
  const [tasks, setTasks] = useState<AssignedTask[]>([]);
  const [totalTasks, setTotalTasks] = useState(0);
  const [tasksLoading, setTasksLoading] = useState(false);
  const [attentionItems, setAttentionItems] = useState<AssignedTask[]>([]);

  const fetchTasks = useCallback(async (filter: AssignmentsFilter) => {
    setTasksLoading(true);
    try {
      const params: Record<string, any> = {
        search: filter.search || undefined,
        itemType: filter.itemType,
        status: filter.status === 'ALL' ? undefined : filter.status,
        statusGroup: 'STAFF_HISTORY',
        ownerScope: 'ME',  // Staff chỉ thấy nhiệm vụ của mình
        fromDate: filter.fromDate || `${year}-01-01`,
        toDate: filter.toDate || `${year}-12-31`,
        sortBy: 'date',
        sortDirection: filter.sortDirection,
        page: filter.page,
        pageSize: filter.pageSize,
      };
      const res = await departmentReceptionTasksApi.getAssignmentsProgress(params);
      setTasks(res?.items || []);
      setTotalTasks(res?.totalItems || 0);

      const pendingAssignments = await departmentReceptionTasksApi.getAssignmentsProgress({
        itemType: 'ALL',
        status: 'ASSIGNED',
        ownerScope: 'ME',
        fromDate: `${year}-01-01`,
        toDate: `${year}-12-31`,
        sortBy: 'date',
        sortDirection: 'ASC',
        page: 1,
        pageSize: 100,
      });
      const pendingItems = (pendingAssignments?.items || []).filter((item: AssignedTask) =>
        item.uiStatus === 'ASSIGNED'
        && item.rawStatus !== 'DECLINED'
        && item.rawStatus !== 'REJECTED'
        && (item.canAccept || item.canDecline),
      );
      setAttentionItems(pendingItems);
    } catch (e) {
      console.error('Fetch tasks error:', e);
    } finally {
      setTasksLoading(false);
    }
  }, [year]);

  // ── Pending items (ASSIGNED status, for top banner) ───────────────────────
  const pendingItems = useMemo(
    () => calendarItems.filter(
      (e) => e.status === 'ASSIGNED' && !e.isProcessed,
    ),
    [calendarItems],
  );

  return {
    calendarItems,
    calendarLoading,
    fetchCalendar,
    tasks,
    totalTasks,
    tasksLoading,
    attentionItems,
    fetchTasks,
    pendingItems,
  };
}
