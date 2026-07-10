/** Mirror backend PEMS.Application.Notifications.Common.NotificationConstants. */
export const NotificationCategories = {
  Visit: 'VISIT',
  Invitation: 'INVITATION',
  Reminder: 'REMINDER',
  Feedback: 'FEEDBACK',
  Logistics: 'LOGISTICS',
  Handover: 'HANDOVER',
  News: 'NEWS',
  Partner: 'PARTNER',
  Account: 'ACCOUNT',
  System: 'SYSTEM',
  General: 'GENERAL',
} as const;

export const NotificationActionTypes = {
  OpenVisitDetail: 'OPEN_VISIT_DETAIL',
  OpenVisitInvitation: 'OPEN_VISIT_INVITATION',
  OpenHostFeedbackModal: 'OPEN_HOST_FEEDBACK_MODAL',
  OpenLogisticsDetail: 'OPEN_LOGISTICS_DETAIL',
  OpenHandoverDetail: 'OPEN_HANDOVER_DETAIL',
  OpenNewsDetail: 'OPEN_NEWS_DETAIL',
  OpenPartnerDetail: 'OPEN_PARTNER_DETAIL',
  OpenAccountDetail: 'OPEN_ACCOUNT_DETAIL',
  OpenNotificationPage: 'OPEN_NOTIFICATION_PAGE',
} as const;

/** 10 filter theo spec (mục 3 CLAUDE_PROMPT): Tất cả/Chưa đọc/Cần hành động + 7 category. */
export type NotificationFilterKey =
  | 'all'
  | 'unread'
  | 'actionRequired'
  | 'visit'
  | 'invitation'
  | 'feedback'
  | 'logistics'
  | 'news'
  | 'partner'
  | 'system';

export const NOTIFICATION_FILTER_KEYS: NotificationFilterKey[] = [
  'all', 'unread', 'actionRequired', 'visit', 'invitation', 'feedback', 'logistics', 'news', 'partner', 'system',
];

/** Mỗi nhãn filter map sang 1..N category backend thật ("Đoàn khách" gộp VISIT+REMINDER, "Hệ thống" gộp SYSTEM+ACCOUNT+GENERAL). */
export const NOTIFICATION_FILTER_CATEGORY_MAP: Partial<Record<NotificationFilterKey, string[]>> = {
  visit: [NotificationCategories.Visit, NotificationCategories.Reminder],
  invitation: [NotificationCategories.Invitation],
  feedback: [NotificationCategories.Feedback],
  logistics: [NotificationCategories.Logistics, NotificationCategories.Handover],
  news: [NotificationCategories.News],
  partner: [NotificationCategories.Partner],
  system: [NotificationCategories.System, NotificationCategories.Account, NotificationCategories.General],
};

/**
 * Tab category nào thực sự có thể xuất hiện với từng role, dựa theo audit toàn bộ nơi backend
 * tạo notification (chỉ nơi nào thật sự set recipient theo role đó mới được liệt kê ở đây).
 * 'all'/'unread'/'actionRequired' luôn hiện cho mọi role, không khai báo trong bảng này.
 *
 * VISITOR: chỉ nhận category VISIT (trạng thái đơn) và FEEDBACK (mời đánh giá chuyến thăm).
 * Các role nội bộ (STAFF/DEPARTMENT/HO/ADMIN) có thể nhận đủ 7 category nên giữ nguyên toàn bộ.
 */
const ROLE_CATEGORY_FILTERS: Partial<Record<string, NotificationFilterKey[]>> = {
  VISITOR: ['visit', 'feedback'],
};

const ALL_CATEGORY_FILTERS: NotificationFilterKey[] = [
  'visit', 'invitation', 'feedback', 'logistics', 'news', 'partner', 'system',
];

/** Trả về danh sách filter key nên hiển thị cho role hiện tại (luôn có all/unread/actionRequired trước). */
export function getVisibleNotificationFilters(roleCode: string | null | undefined): NotificationFilterKey[] {
  const categoryFilters = (roleCode && ROLE_CATEGORY_FILTERS[roleCode.toUpperCase()]) || ALL_CATEGORY_FILTERS;
  return ['all', 'unread', 'actionRequired', ...categoryFilters];
}
