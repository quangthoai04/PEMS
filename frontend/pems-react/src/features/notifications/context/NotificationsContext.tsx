import React, { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { useAuth } from '../../../shared/hooks/useAuth';
import { notificationsApi } from '../api/notificationsApi';
import { visitFeedbackApi } from '../../feedbacks/api/visitFeedbackApi';
import type { NotificationItem } from '../types/notification.types';
import type { PendingFeedbackItem } from '../../feedbacks/types/visitFeedback.types';

interface NotificationsContextValue {
  unreadCount: number;
  items: NotificationItem[];
  pendingFeedback: PendingFeedbackItem[];
  loading: boolean;
  fetchNotifications: () => Promise<void>;
  fetchPendingFeedback: () => Promise<void>;
  markAsRead: (id: number) => Promise<void>;
  markAllAsRead: () => Promise<void>;
}

const NotificationsContext = createContext<NotificationsContextValue | undefined>(undefined);
const POLL_MS = 60000;

/**
 * Nguồn dữ liệu notification dùng chung cho MỌI bell instance (Header desktop/mobile,
 * DashboardLayout mobile/desktop) — tránh mỗi bell tự fetch/poll riêng, gây gọi API trùng và
 * unread count lệch nhau giữa các nơi.
 */
export function NotificationsProvider({ children }: { children: React.ReactNode }) {
  const { isAuthenticated } = useAuth();
  const [unreadCount, setUnreadCount] = useState(0);
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [pendingFeedback, setPendingFeedback] = useState<PendingFeedbackItem[]>([]);
  const [loading, setLoading] = useState(false);

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
      setItems(data.items);
    } catch (error) {
      console.error('Failed to fetch notifications', error);
    } finally {
      setLoading(false);
    }
  }, []);

  const fetchPendingFeedback = useCallback(async () => {
    try {
      const res = await visitFeedbackApi.getMyPending();
      setPendingFeedback((res.items || []).filter((x) => !x.alreadySubmitted));
    } catch {
      setPendingFeedback([]);
    }
  }, []);

  const markAsRead = useCallback(async (id: number) => {
    setItems((prev) => {
      const target = prev.find((n) => n.notificationId === id);
      if (target && !target.isRead) {
        setUnreadCount((c) => Math.max(0, c - 1));
      }
      return prev.map((n) => (n.notificationId === id ? { ...n, isRead: true } : n));
    });
    try {
      await notificationsApi.markAsRead(id);
    } catch (error) {
      console.error('Failed to mark as read', error);
    }
  }, []);

  const markAllAsRead = useCallback(async () => {
    setItems((prev) => prev.map((n) => ({ ...n, isRead: true })));
    setUnreadCount(0);
    try {
      await notificationsApi.markAllAsRead();
    } catch (error) {
      console.error('Failed to mark all as read', error);
    }
  }, []);

  useEffect(() => {
    if (!isAuthenticated) {
      setUnreadCount(0);
      setItems([]);
      setPendingFeedback([]);
      return;
    }
    fetchUnreadCount();
    fetchNotifications();
    fetchPendingFeedback();
    const interval = setInterval(fetchUnreadCount, POLL_MS);
    return () => clearInterval(interval);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAuthenticated]);

  const value: NotificationsContextValue = {
    unreadCount,
    items,
    pendingFeedback,
    loading,
    fetchNotifications,
    fetchPendingFeedback,
    markAsRead,
    markAllAsRead,
  };

  return <NotificationsContext.Provider value={value}>{children}</NotificationsContext.Provider>;
}

/** Cùng tên `useNotifications` như hook cũ — nơi gọi chỉ cần đổi import path. */
export function useNotifications(): NotificationsContextValue {
  const ctx = useContext(NotificationsContext);
  if (!ctx) throw new Error('useNotifications must be used within a <NotificationsProvider>.');
  return ctx;
}
