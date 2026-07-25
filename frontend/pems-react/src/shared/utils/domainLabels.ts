/**
 * Central Vietnamese labels for domain enums/codes shown to business users.
 *
 * Rule: never render a raw enum/code (DRAFT, PRESENT, COVERAGE_GENERAL, "Visit Instance"…) in the UI.
 * Every user-facing status/type/coverage goes through a formatter here. An unrecognised value renders a
 * neutral "Chưa xác định" (and is logged in development so the gap is caught) — never the raw token, and
 * never a crash. These labels are DISPLAY only; the underlying code sent to the backend never changes.
 */

const UNKNOWN_LABEL = 'Chưa xác định';

/** Resolve a code against a map; unknown → neutral label + a dev-only telemetry log (never the raw code). */
function labelFrom(map: Record<string, string>, value: unknown, domain: string): string {
  if (typeof value !== 'string' || value.trim() === '') return UNKNOWN_LABEL;
  const key = value.trim().toUpperCase();
  const hit = map[key];
  if (hit !== undefined) return hit;
  if (import.meta.env?.DEV) {
    // eslint-disable-next-line no-console
    console.warn(`[domainLabels] unmapped ${domain} value: ${value}`);
  }
  return UNKNOWN_LABEL;
}

// ── Minutes ──────────────────────────────────────────────────────────────────
const MINUTES_STATUS: Record<string, string> = {
  DRAFT: 'Bản nháp',
  SAVED: 'Đã lưu',
  PUBLISHED: 'Đã xuất bản',
  ARCHIVED: 'Đã lưu trữ',
};
export const formatMinutesStatus = (v: unknown) => labelFrom(MINUTES_STATUS, v, 'minutesStatus');

// ── Attendance ───────────────────────────────────────────────────────────────
const ATTENDANCE_STATUS: Record<string, string> = {
  PRESENT: 'Có mặt',
  ABSENT: 'Vắng mặt',
  EXCUSED: 'Vắng có phép',
};
export const formatAttendanceStatus = (v: unknown) => labelFrom(ATTENDANCE_STATUS, v, 'attendanceStatus');

// ── Documents ────────────────────────────────────────────────────────────────
const DOCUMENT_TYPE: Record<string, string> = {
  GENERAL: 'Tài liệu chung',
  VISIT: 'Theo đoàn tiếp khách',
  PARTNER: 'Đối tác',
  MINUTES: 'Biên bản',
  NEWS: 'Tin tức',
  LOGISTICS: 'Hậu cần',
  REPORT: 'Báo cáo',
};
export const formatDocumentType = (v: unknown) => labelFrom(DOCUMENT_TYPE, v, 'documentType');

const DOCUMENT_STATUS: Record<string, string> = {
  DRAFT: 'Bản nháp',
  PUBLISHED: 'Đã xuất bản',
  ARCHIVED: 'Đã lưu trữ',
  HIDDEN: 'Đã ẩn',
};
export const formatDocumentStatus = (v: unknown) => labelFrom(DOCUMENT_STATUS, v, 'documentStatus');

const COVERAGE_TYPE: Record<string, string> = {
  COVERAGE_GENERAL: 'Phạm vi chung',
  GENERAL: 'Phạm vi chung',
  COVERAGE_VISIT: 'Theo đoàn tiếp khách',
  VISIT: 'Theo đoàn tiếp khách',
};
export const formatCoverageType = (v: unknown) => labelFrom(COVERAGE_TYPE, v, 'coverageType');

// ── News ─────────────────────────────────────────────────────────────────────
const NEWS_STATUS: Record<string, string> = {
  DRAFT: 'Bản nháp',
  PENDING_REVIEW: 'Chờ duyệt',
  PUBLISHED: 'Đã xuất bản',
  REJECTED: 'Bị từ chối',
  HIDDEN: 'Đã ẩn',
  ARCHIVED: 'Đã lưu trữ',
};
export const formatNewsStatus = (v: unknown) => labelFrom(NEWS_STATUS, v, 'newsStatus');

// ── Invitation ───────────────────────────────────────────────────────────────
const INVITATION_STATUS: Record<string, string> = {
  INVITED: 'Đã mời',
  PENDING: 'Chờ phản hồi',
  ACCEPTED: 'Đã chấp nhận',
  DECLINED: 'Đã từ chối',
  ASSIGNED: 'Đã phân công',
  REMOVED: 'Đã gỡ',
  CANCELLED: 'Đã hủy',
};
export const formatInvitationStatus = (v: unknown) => labelFrom(INVITATION_STATUS, v, 'invitationStatus');

// ── Logistics ────────────────────────────────────────────────────────────────
const LOGISTICS_STATUS: Record<string, string> = {
  PLANNED: 'Đã lên kế hoạch',
  REQUESTED: 'Đã yêu cầu',
  APPROVED: 'Đã duyệt',
  REJECTED: 'Bị từ chối',
  IN_PROGRESS: 'Đang thực hiện',
  HANDED_OVER: 'Đã bàn giao',
  RETURNED: 'Đã trả',
  COMPLETED: 'Hoàn tất',
  CANCELLED: 'Đã hủy',
};
export const formatLogisticsStatus = (v: unknown) => labelFrom(LOGISTICS_STATUS, v, 'logisticsStatus');

// ── Visit instance status ────────────────────────────────────────────────────
const VISIT_STATUS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ duyệt yêu cầu',
  PENDING_APPROVAL: 'Chờ duyệt',
  APPROVED: 'Đã duyệt',
  PARTIALLY_APPROVED: 'Duyệt một phần',
  ASSIGNED: 'Đã phân công',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp khách',
  AFTER_VISIT: 'Sau tiếp khách',
  CLOSED: 'Đã đóng',
  CANCELLED: 'Đã hủy',
  REJECTED: 'Bị từ chối',
};
export const formatVisitStatus = (v: unknown) => labelFrom(VISIT_STATUS, v, 'visitStatus');

// ── Action items ─────────────────────────────────────────────────────────────
const ACTION_ITEM_STATUS: Record<string, string> = {
  TODO: 'Cần làm',
  OPEN: 'Đang mở',
  IN_PROGRESS: 'Đang thực hiện',
  DONE: 'Hoàn tất',
  COMPLETED: 'Hoàn tất',
  CANCELLED: 'Đã hủy',
};
export const formatActionItemStatus = (v: unknown) => labelFrom(ACTION_ITEM_STATUS, v, 'actionItemStatus');

// ── Audit action ─────────────────────────────────────────────────────────────
const AUDIT_ACTION: Record<string, string> = {
  UPLOAD_VISIT_PHOTOS: 'Tải ảnh đoàn khách',
  CREATE: 'Tạo mới',
  UPDATE: 'Cập nhật',
  DELETE: 'Xóa',
  APPROVE: 'Duyệt',
  REJECT: 'Từ chối',
};
export const formatAuditAction = (v: unknown) => labelFrom(AUDIT_ACTION, v, 'auditAction');

export const UNKNOWN_DOMAIN_LABEL = UNKNOWN_LABEL;
