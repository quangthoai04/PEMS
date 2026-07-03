import React, { useState, useRef, useEffect, useCallback } from 'react';
import { Bell, Star } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import toast from 'react-hot-toast';
import { useNotifications } from '../../features/notifications/hooks/useNotifications';
import { NotificationItem } from '../../features/notifications/types/notification.types';
import { visitFeedbackApi } from '../../features/feedbacks/api/visitFeedbackApi';
import type { PendingFeedbackItem } from '../../features/feedbacks/types/visitFeedback.types';
import { VisitFeedbackModal } from '../../features/feedbacks/components/VisitFeedbackModal';

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
  return item.targetUrl || undefined;
}

export function NotificationBell() {
  const [isOpen, setIsOpen] = useState(false);
  const popoverRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();

  const { items, unreadCount, loading, fetchNotifications, markAsRead, markAllAsRead } = useNotifications();

  // Nhắc "Bạn hãy đánh giá đoàn" — query động từ backend (không insert vào notifications
  // nên không bao giờ duplicate; tự biến mất sau khi user gửi feedback).
  const [pendingFeedback, setPendingFeedback] = useState<PendingFeedbackItem[]>([]);
  const [feedbackModalInstanceId, setFeedbackModalInstanceId] = useState<number | string | null>(null);

  const fetchPendingFeedback = useCallback(async () => {
    try {
      const res = await visitFeedbackApi.getMyPending();
      setPendingFeedback((res.items || []).filter((x) => !x.alreadySubmitted));
    } catch {
      setPendingFeedback([]);
    }
  }, []);

  // Fetch on mount to show unread badge
  useEffect(() => {
    fetchNotifications();
    fetchPendingFeedback();
  }, [fetchNotifications, fetchPendingFeedback]);

  // Re-fetch when bell is opened
  useEffect(() => {
    if (isOpen) {
      fetchNotifications();
      fetchPendingFeedback();
    }
  }, [isOpen, fetchNotifications, fetchPendingFeedback]);

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

    if (!item.canOpen || !item.targetUrl) {
      toast.error(item.disabledReason || "Không thể mở nội dung này.", {
        icon: '⚠️',
        style: {
          borderRadius: '10px',
          background: '#333',
          color: '#fff',
        },
      });
      return;
    }

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
        {(unreadCount + pendingFeedback.length) > 0 && (
          <span className="absolute top-1 right-1.5 flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white">
            {(unreadCount + pendingFeedback.length) > 9 ? '9+' : unreadCount + pendingFeedback.length}
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
              {/* Nhắc đánh giá đoàn — hàng ưu tiên trên cùng, bấm vào mở trang đánh giá */}
              {pendingFeedback.length > 0 && (
                <div className="flex flex-col divide-y divide-orange-100/60 border-b border-orange-100 bg-orange-50/40">
                  {pendingFeedback.map((p) => (
                    <button
                      key={`fb-${p.visitInstanceId}`}
                      onClick={() => {
                        setIsOpen(false);
                        setFeedbackModalInstanceId(p.visitInstanceId!);
                      }}
                      className="flex w-full items-start gap-2.5 p-4 text-left transition-colors hover:bg-orange-50"
                    >
                      <Star className="mt-0.5 h-4 w-4 shrink-0 text-[#F37021]" />
                      <span className="min-w-0">
                        <span className="block text-sm font-semibold text-gray-900">Bạn hãy đánh giá đoàn</span>
                        <span className="block truncate text-xs text-gray-500">
                          {p.delegationName}{p.campusName ? ` • ${p.campusName}` : ''}
                        </span>
                      </span>
                    </button>
                  ))}
                </div>
              )}
              {loading && items.length === 0 ? (
                <div className="flex items-center justify-center gap-2 py-8 text-gray-400">
                  <div className="w-4 h-4 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                  <span className="text-sm">Đang tải...</span>
                </div>
              ) : items.length === 0 ? (
                pendingFeedback.length === 0 ? (
                  <div className="p-6 text-center text-gray-500">
                    <p className="text-sm">Không có thông báo nào</p>
                  </div>
                ) : null
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

      <VisitFeedbackModal 
        open={feedbackModalInstanceId !== null} 
        visitInstanceId={feedbackModalInstanceId} 
        onClose={() => setFeedbackModalInstanceId(null)} 
        onSubmitted={() => {
          fetchPendingFeedback();
          setFeedbackModalInstanceId(null);
        }} 
      />
    </div>
  );
}
