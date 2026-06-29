import httpClient from '../../../shared/api/httpClient';
import { NotificationItem, UnreadNotificationCountResponse } from '../types/notification.types';

type PaginatedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
};

export const notificationsApi = {
  getNotifications: async (params?: { page?: number; pageSize?: number; isRead?: boolean }) => {
    const response = await httpClient.get<PaginatedResult<NotificationItem>>('/notifications', { params });
    return response.data;
  },

  getUnreadCount: async () => {
    const response = await httpClient.get<UnreadNotificationCountResponse>('/notifications/unread-count');
    return response.data;
  },

  markAsRead: async (notificationId: number) => {
    const response = await httpClient.patch<{ success: boolean }>(`/notifications/${notificationId}/read`);
    return response.data;
  },

  markAllAsRead: async () => {
    const response = await httpClient.patch<{ updatedCount: number }>('/notifications/mark-all-read');
    return response.data;
  },
};
