export type NotificationItem = {
  notificationId: number;
  title: string;
  message: string | null;
  notificationType: string;
  relatedType: string | null;
  relatedId: number | null;
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
