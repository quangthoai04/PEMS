import httpClient from '../../../shared/api/httpClient';

export interface NotificationItem {
  notificationId: number;
  notificationType: string;
  title: string;
  message?: string;
  relatedType?: string;
  relatedId?: number;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationsResponse {
  items: NotificationItem[];
  unreadCount: number;
}

export const notificationsApi = {
  getNotifications: (pageSize = 20) =>
    httpClient.get<NotificationsResponse>('/public/notifications', { params: { pageSize } }),

  markAsRead: (notificationId: number) =>
    httpClient.patch(`/public/notifications/${notificationId}/read`),

  markAllAsRead: () =>
    httpClient.patch('/public/notifications/read-all'),
};
