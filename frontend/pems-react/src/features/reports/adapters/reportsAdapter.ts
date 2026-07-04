/**
 * Map status DB → label tiếng Việt / màu badge + các formatter dùng chung cho HO report.
 */

const INSTANCE_STATUS_LABELS: Record<string, string> = {
  WAITING_REQUEST_APPROVAL: 'Chờ duyệt',
  WAITING_HOST_ASSIGNMENT: 'Chờ gán host',
  ASSIGNED: 'Đã gán host',
  BEFORE_VISIT: 'Trước tiếp khách',
  DURING_VISIT: 'Đang tiếp',
  AFTER_VISIT: 'Sau tiếp khách',
  CLOSED: 'Đã đóng',
  CANCELLED: 'Đã hủy',
};

const REQUEST_STATUS_LABELS: Record<string, string> = {
  PENDING_APPROVAL: 'Chờ duyệt',
  APPROVED: 'Đã duyệt',
  REJECTED: 'Từ chối',
  CANCELLED: 'Đã hủy',
};

/** Tailwind classes cho badge theo status (text + bg nhẹ). */
const STATUS_BADGE_CLASSES: Record<string, string> = {
  PENDING_APPROVAL: 'bg-amber-50 text-amber-700 border-amber-200',
  APPROVED: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  REJECTED: 'bg-red-50 text-red-600 border-red-200',
  CANCELLED: 'bg-slate-100 text-slate-500 border-slate-200',
  WAITING_REQUEST_APPROVAL: 'bg-amber-50 text-amber-700 border-amber-200',
  WAITING_HOST_ASSIGNMENT: 'bg-orange-50 text-orange-600 border-orange-200',
  ASSIGNED: 'bg-blue-50 text-blue-700 border-blue-200',
  BEFORE_VISIT: 'bg-sky-50 text-sky-700 border-sky-200',
  DURING_VISIT: 'bg-indigo-50 text-indigo-700 border-indigo-200',
  AFTER_VISIT: 'bg-violet-50 text-violet-700 border-violet-200',
  CLOSED: 'bg-emerald-50 text-emerald-700 border-emerald-200',
};

const BLOCKER_LABELS: Record<string, string> = {
  PLANNED_END_NOT_REACHED: 'Chưa tới giờ kết thúc',
  LOGISTICS_OPEN: 'Hậu cần còn mở',
  HANDOVER_SIGNATURE_MISSING: 'Bàn giao thiếu chữ ký',
  ACTION_ITEMS_OPEN: 'Việc biên bản còn mở',
  NEWS_MISSING: 'Thiếu bài tin tức',
};

export const reportsAdapter = {
  instanceStatusLabel: (status: string): string => INSTANCE_STATUS_LABELS[status] ?? status,

  requestStatusLabel: (status: string): string => REQUEST_STATUS_LABELS[status] ?? status,

  statusBadgeClass: (status: string): string =>
    STATUS_BADGE_CLASSES[status] ?? 'bg-slate-100 text-slate-500 border-slate-200',

  blockerLabel: (blocker: string): string => BLOCKER_LABELS[blocker] ?? blocker,

  /** 12345 → "12.345" (vi-VN). */
  formatNumber: (value: number): string => value.toLocaleString('vi-VN'),

  /** 4.333 → "4.3"; null → "—". */
  formatRating: (value: number | null | undefined): string =>
    value == null ? '—' : value.toFixed(1),

  /** ISO string → dd/MM/yyyy. */
  formatDate: (iso: string | null | undefined): string => {
    if (!iso) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
  },

  /** ISO string → dd/MM/yyyy HH:mm. */
  formatDateTime: (iso: string | null | undefined): string => {
    if (!iso) return '—';
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleString('vi-VN', {
      day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
    });
  },

  /** 52.4 giờ → "2 ngày 4 giờ"; < 1 giờ → "dưới 1 giờ". */
  formatWaitingHours: (hours: number): string => {
    if (hours < 1) return 'dưới 1 giờ';
    const totalHours = Math.floor(hours);
    const days = Math.floor(totalHours / 24);
    const rest = totalHours % 24;
    if (days === 0) return `${rest} giờ`;
    if (rest === 0) return `${days} ngày`;
    return `${days} ngày ${rest} giờ`;
  },

  /** 78.4 → "78%". */
  formatPercent: (value: number | null | undefined): string =>
    value == null ? '—' : `${Math.round(value)}%`,
};
