import { useState, useEffect, useCallback } from 'react';
import { notificationsApi } from '../api/notificationsApi';
import { NotificationItem } from '../types/notification.types';

export function useNotifications() {
  const [unreadCount, setUnreadCount] = useState(0);
  const [notifications, setNotifications] = useState<NotificationItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [popoverOpen, setPopoverOpen] = useState(false);

  const fetchUnreadCount = useCallback(async () => {
    try {
      const data = await notificationsApi.getUnreadCount();
      setUnreadCount(data.unreadCount);
    } catch (error) {
      console.error('Failed to fetch unread count', error);
    }
  }, []);

  const fetchNotifications = useCallback(async () => {
    try {
      setLoading(true);
      const data = await notificationsApi.getNotifications({ page: 1, pageSize: 10 });
      setNotifications(data.items);
    } catch (error) {
      console.error('Failed to fetch notifications', error);
    } finally {
      setLoading(false);
    }
  }, []);

  const markAsRead = async (id: number) => {
    try {
      setNotifications(prev => prev.map(n => n.notificationId === id ? { ...n, isRead: true } : n));
      const target = notifications.find(n => n.notificationId === id);
      if (target && !target.isRead) {
        setUnreadCount(prev => Math.max(0, prev - 1));
      }
      
      await notificationsApi.markAsRead(id);
    } catch (error) {
      console.error('Failed to mark as read', error);
    }
  };

  const markAllAsRead = async () => {
    try {
      setNotifications(prev => prev.map(n => ({ ...n, isRead: true })));
      setUnreadCount(0);
      await notificationsApi.markAllAsRead();
    } catch (error) {
      console.error('Failed to mark all as read', error);
    }
  };

  useEffect(() => {
    fetchUnreadCount();
    
    // Polling every 60 seconds
    const interval = setInterval(fetchUnreadCount, 60000);
    return () => clearInterval(interval);
  }, [fetchUnreadCount]);

  useEffect(() => {
    if (popoverOpen) {
      fetchNotifications();
    }
  }, [popoverOpen, fetchNotifications]);

  return {
    unreadCount,
    items: notifications,
    loading,
    fetchNotifications,
    markAsRead,
    markAllAsRead,
  };
}
