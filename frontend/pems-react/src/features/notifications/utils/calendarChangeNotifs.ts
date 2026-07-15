import type { NotificationItem } from '../types/notification.types';

/** Thông tin tối thiểu của 1 sự kiện trên bảng lịch để đối chiếu với thông báo. */
export type CalendarChangeTarget = {
  itemType?: string; // 'INVITATION' | 'REQUEST' | 'VISIT' | 'PERSONAL'
  rawId?: number | string; // participantId (INVITATION) | logisticsItemId (REQUEST)
  visitRequestId?: number | string | null;
  visitInstanceId?: number | string | null;
};

/**
 * Lọc các thông báo CHƯA ĐỌC gắn với đúng 1 đơn/thư mời trên bảng lịch — dùng cho
 * chấm đỏ nháy trên chip lịch và danh sách "Thay đổi mới" trong modal chi tiết
 * (dùng chung Dept Leader / Dept Staff).
 */
export function matchCalendarChangeNotifs(
  notifs: NotificationItem[],
  ev: CalendarChangeTarget | null | undefined,
): NotificationItem[] {
  if (!ev || ev.itemType === 'PERSONAL') return [];
  return notifs.filter(n => {
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
    if (ev.itemType === 'VISIT') {
      // Lịch của Staff Leader / IC Staff hiện cả đoàn khách (visit instance): MỌI thay đổi
      // gắn với đúng visit đó (kể cả hậu cần, người tham gia) đều tính là thay đổi của đơn.
      if (sameInstance || sameRequest) return true;
      if (rt === 'VISIT_INSTANCE') return n.relatedId != null && ev.visitInstanceId != null
        && String(n.relatedId) === String(ev.visitInstanceId);
      if (rt === 'VISIT_REQUEST') return n.relatedId != null && ev.visitRequestId != null
        && String(n.relatedId) === String(ev.visitRequestId);
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
}
