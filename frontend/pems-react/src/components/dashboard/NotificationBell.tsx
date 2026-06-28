import React, { useState, useRef, useEffect } from 'react';
import { Bell } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { useNotifications } from '../../features/notifications/hooks/useNotifications';
import { NotificationItem } from '../../features/notifications/api/notificationsApi';

function timeAgo(dateStr: string): string {
  const date = new Date(dateStr.endsWith('Z') ? dateStr : dateStr + 'Z');
  const diffMs = Date.now() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  if (diffMin < 1) return 'Vừa xong';
  if (diffMin < 60) return `${diffMin} phút trước`;
  const diffHour = Math.floor(diffMin / 60);
  if (diffHour < 24) return `${diffHour} giờ trước`;
  const diffDay = Math.floor(diffHour / 24);
  if (diffDay < 7) return `${diffDay} ngày trước`;
  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

function getNotificationLink(item: NotificationItem): string | undefined {
  if (item.relatedType === 'NEWS' && item.relatedId) {
    return `/dashboard/news/${item.relatedId}`;
  }
  return undefined;
}

export function NotificationBell() {
  const [isOpen, setIsOpen] = useState(false);
  const popoverRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

  const { items, unreadCount, loading, fetchNotifications, markAsRead, markAllAsRead } = useNotifications();

  // Fetch on mount to show unread badge
  useEffect(() => {
    fetchNotifications();
  }, [fetchNotifications]);

  // Re-fetch when bell is opened
  useEffect(() => {
    if (isOpen) fetchNotifications();
  }, [isOpen, fetchNotifications]);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (popoverRef.current && !popoverRef.current.contains(event.target as Node)) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleItemClick = async (item: NotificationItem) => {
    if (!item.isRead) await markAsRead(item.notificationId);
    setIsOpen(false);
    const link = getNotificationLink(item);
    if (link) navigate(link);
  };

  const handleMarkAllAsRead = async () => {
    await markAllAsRead();
  };

  return (
    <div className="relative" ref={popoverRef}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="p-2 text-gray-600 hover:bg-gray-100 rounded-full transition-colors relative"
        aria-label="Thông báo"
      >
        <Bell className="w-6 h-6" />
        {unreadCount > 0 && (
          <span className="absolute top-1 right-1.5 flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white">
            {unreadCount > 9 ? '9+' : unreadCount}
          </span>
        )}
      </button>

      <AnimatePresence>
        {isOpen && (
          <motion.div
            initial={{ opacity: 0, y: 10, scale: 0.95 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 10, scale: 0.95 }}
            transition={{ duration: 0.2 }}
            className="absolute right-0 mt-2 w-80 sm:w-96 bg-white rounded-xl shadow-xl border border-gray-100 z-50 overflow-hidden"
          >
            <div className="flex items-center justify-between px-4 py-3 border-b border-gray-100 bg-gray-50/50">
              <h3 className="font-semibold text-gray-800">Thông báo</h3>
              {unreadCount > 0 && (
                <button
                  onClick={handleMarkAllAsRead}
                  className="text-xs font-medium text-[#004c91] hover:underline"
                >
                  Đánh dấu đã đọc
                </button>
              )}
            </div>

            <div className="max-h-[70vh] overflow-y-auto">
              {loading && items.length === 0 ? (
                <div className="flex items-center justify-center gap-2 py-8 text-gray-400">
                  <div className="w-4 h-4 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                  <span className="text-sm">Đang tải...</span>
                </div>
              ) : items.length === 0 ? (
                <div className="p-6 text-center text-gray-500">
                  <p className="text-sm">Không có thông báo nào</p>
                </div>
              ) : (
                <div className="flex flex-col divide-y divide-gray-50">
                  {items.map((item: NotificationItem) => (
                    <button
                      key={item.notificationId}
                      onClick={() => handleItemClick(item)}
                      className={`flex flex-col gap-1 p-4 text-left transition-colors hover:bg-gray-50 w-full ${
                        !item.isRead ? 'bg-blue-50/40' : ''
                      }`}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <span className={`text-sm font-medium ${!item.isRead ? 'text-gray-900' : 'text-gray-600'}`}>
                          {!item.isRead && (
                            <span className="inline-block w-1.5 h-1.5 bg-blue-500 rounded-full mr-1.5 mb-0.5 align-middle" />
                          )}
                          {item.title}
                        </span>
                        <span className="text-[10px] text-gray-400 shrink-0 mt-0.5 whitespace-nowrap">
                          {timeAgo(item.createdAt)}
                        </span>
                      </div>
                      {item.message && (
                        <p className="text-xs text-gray-500 line-clamp-2 pl-0">
                          {item.message}
                        </p>
                      )}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
