export type NotificationItem = {
  notificationId: number;
  title: string;
  message: string | null;
  notificationType: string;
  category: string;
  priority: string;
  isActionRequired: boolean;
  relatedType: string | null;
  relatedId: number | null;
  visitRequestId: number | null;
  visitInstanceId: number | null;
  campusId: number | null;
  actionType: string | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
  timeAgoText: string;
  targetUrl: string | null;
  canOpen: boolean;
  disabledReason: string | null;
};

export type UnreadNotificationCountResponse = {
  unreadCount: number;
};
