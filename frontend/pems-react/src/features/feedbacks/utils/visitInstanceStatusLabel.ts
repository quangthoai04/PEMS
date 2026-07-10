const VISIT_INSTANCE_STATUS_LABELS_VI: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ duyệt',
  ASSIGNED: 'Đã phân công host',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Sau tiếp khách',
  CLOSED: 'Đã đóng đoàn',
  REJECTED: 'Đã từ chối',
  CANCELLED: 'Đã hủy',
};

export function visitInstanceStatusLabelVi(status: string): string {
  return VISIT_INSTANCE_STATUS_LABELS_VI[status] || status;
}
