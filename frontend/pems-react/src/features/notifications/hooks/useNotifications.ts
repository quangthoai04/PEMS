import { useState, useCallback } from 'react';
import { notificationsApi, NotificationItem } from '../api/notificationsApi';

export function useNotifications() {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [loading, setLoading] = useState(false);

  const fetchNotifications = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await notificationsApi.getNotifications();
      setItems(data.items ?? []);
      setUnreadCount(data.unreadCount ?? 0);
    } catch {
      // silently fail — bell just shows stale state
    } finally {
      setLoading(false);
    }
  }, []);

  const markAsRead = useCallback(async (notificationId: number) => {
    setItems((prev: NotificationItem[]) =>
      prev.map((n: NotificationItem) => n.notificationId === notificationId ? { ...n, isRead: true } : n)
    );
    setUnreadCount((prev: number) => Math.max(0, prev - 1));
    try {
      await notificationsApi.markAsRead(notificationId);
    } catch {
      // UI already updated optimistically
    }
  }, []);

  const markAllAsRead = useCallback(async () => {
    setItems((prev: NotificationItem[]) => prev.map((n: NotificationItem) => ({ ...n, isRead: true })));
    setUnreadCount(0);
    try {
      await notificationsApi.markAllAsRead();
    } catch {
      // UI already updated optimistically
    }
  }, []);

  return { items, unreadCount, loading, fetchNotifications, markAsRead, markAllAsRead };
}
