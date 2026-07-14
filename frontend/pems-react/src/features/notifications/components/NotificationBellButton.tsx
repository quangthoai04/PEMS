import React, { useState, useRef, useEffect } from 'react';
import { Bell, Star } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useNotifications } from '../context/NotificationsContext';
import { useAuth } from '../../../shared/hooks/useAuth';
import { NotificationItem } from '../types/notification.types';
import { VisitFeedbackModal } from '../../feedbacks/components/VisitFeedbackModal';
import { HostFeedbackModal } from '../../feedbacks/components/HostFeedbackModal';
import { VisitorFeedbackDetailModal } from '../../feedbacks/components/VisitorFeedbackDetailModal';
import { NotificationDetailModal } from './NotificationDetailModal';
import type { AuthUser } from '../../authentication/types/authentication.types';
import { formatVietnamRelative } from '../../../shared/utils/vietnamTime';

export function timeAgo(dateStr: string): string {
  // API trả ISO +07:00 (giờ Việt Nam) — parse offset-aware, tuyệt đối không nối 'Z'.
  return formatVietnamRelative(dateStr);
}

export function getNotificationLink(item: NotificationItem, user: AuthUser | null): string | undefined {
  // Tin tức / đối tác: mọi thông báo (duyệt, từ chối, chờ duyệt) đều đưa về trang quản lý
  // lọc đúng 1 bản ghi — có nút "Xem tất cả" để thoát. Rewrite theo relatedType/relatedId
  // để xử lý luôn notification cũ trỏ trang chi tiết hoặc không có targetUrl.
  const relatedType = item.relatedType?.toUpperCase();
  if (relatedType === 'NEWS' && item.relatedId) {
    return `/dashboard/news?newsId=${item.relatedId}`;
  }
  if (relatedType === 'PARTNER' && item.relatedId) {
    return `/dashboard/partners?partnerId=${item.relatedId}`;
  }

  let link = item.targetUrl || undefined;

  if (link && user) {
    const isDeptStaff = user.roleCode?.toUpperCase() === 'DEPARTMENT' && user.subRole?.toUpperCase() !== 'LEADER';
    const isDeptLeader = user.roleCode?.toUpperCase() === 'DEPARTMENT' && user.subRole?.toUpperCase() === 'LEADER';
    if (isDeptStaff) {
      if (link.includes('/tasks/')) {
        const parts = link.split('/tasks/');
        return `/dashboard?taskId=${parts[1]}&itemType=REQUEST`;
      }
      if (link.includes('/invitations/')) {
        const parts = link.split('/invitations/');
        return `/dashboard?taskId=${parts[1]}&itemType=INVITATION`;
      }
    }

    // Dept Leader: đơn/thư mời được giao xem tại tab "Phân công và tiến độ" thay vì
    // trang chi tiết đứng riêng (TaskDetail/TaskInvitationDetail) — lọc còn đúng 1 dòng.
    if (isDeptLeader && (link.includes('/tasks/') || link.includes('/invitations/')) && item.visitRequestId) {
      return `/dashboard/visit?tab=assignments-progress&visitRequestId=${item.visitRequestId}`;
    }

    // Notification cũ (tạo trước khi ActionUrl bắt đầu kèm id) còn lưu targetUrl trơ trụi
    // "/dashboard/visit" — không định danh được đơn nào. Vá lại bằng visitRequestId sẵn có
    // trên chính notification, áp dụng mọi role vì list không lọc gì thì vô nghĩa.
    if (link === '/dashboard/visit' && item.visitRequestId) {
      return `/dashboard/visit?visitRequestId=${item.visitRequestId}`;
    }

    const isProcessDetailLink = /\/dashboard\/visit\/(process|reception-detail|ho-detail)\//.test(link);

    // Visitor & HO không bao giờ là Host theo thiết kế hệ thống — trang Host Operation
    // không dành cho họ. Notification cũ có thể còn trỏ vào /process|/reception-detail|
    // /ho-detail từ trước khi route này được đổi — luôn rewrite về trang Quản lý tiếp khách
    // lọc đúng đơn, tính theo dữ liệu hiện tại của notification thay vì tin URL đã lưu sẵn.
    const isViewOnlyRole = ['VISITOR', 'HO'].includes(user.roleCode?.toUpperCase() || '');
    if (isViewOnlyRole && isProcessDetailLink && item.visitRequestId) {
      return `/dashboard/visit?visitRequestId=${item.visitRequestId}`;
    }

    // "Bạn được mời tham gia đoàn" (participant invitation) — người nhận có thể là
    // Student/IC Staff KHÔNG phải Host của đoàn này (IC Staff đôi khi là Host đoàn khác,
    // nên không thể chặn theo role tĩnh). Trang Host Operation chỉ dành cho đúng Host của
    // đoàn, nên loại notification này luôn rewrite về trang danh sách "Quản lý tiếp khách"
    // lọc đúng đơn, bất kể role.
    if (item.actionType === 'OPEN_VISIT_INVITATION' && isProcessDetailLink && item.visitRequestId) {
      return `/dashboard/visit?visitRequestId=${item.visitRequestId}`;
    }
  }

  return link;
}

interface NotificationBellButtonProps {
  variant?: 'header-desktop' | 'header-mobile' | 'dashboard';
  onNavigate?: () => void;
}

export function NotificationBellButton({ variant = 'dashboard', onNavigate }: NotificationBellButtonProps) {
  const { t } = useTranslation(['notifications']);
  const [isOpen, setIsOpen] = useState(false);
  const popoverRef = useRef<HTMLDivElement>(null);
  const navigate = useNavigate();
  const { user } = useAuth();

  const { items, unreadCount, pendingFeedback, loading, fetchNotifications, fetchPendingFeedback, markAsRead, markAllAsRead } =
    useNotifications();

  const [feedbackModalInstanceId, setFeedbackModalInstanceId] = useState<number | string | null>(null);
  const [hostFeedbackVisitInstanceId, setHostFeedbackVisitInstanceId] = useState<number | null>(null);
  const [visitorFeedbackVisitInstanceId, setVisitorFeedbackVisitInstanceId] = useState<number | null>(null);
  const [detailModalItem, setDetailModalItem] = useState<NotificationItem | null>(null);

  // Re-fetch when bell is opened (danh sách/pending feedback vẫn có thể đã cũ từ lần poll trước).
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

    if (item.actionType === 'OPEN_HOST_FEEDBACK_MODAL' && item.visitInstanceId) {
      setIsOpen(false);
      setHostFeedbackVisitInstanceId(item.visitInstanceId);
      return;
    }

    if (item.actionType === 'OPEN_VISITOR_FEEDBACK_MODAL' && item.visitInstanceId) {
      setIsOpen(false);
      setVisitorFeedbackVisitInstanceId(item.visitInstanceId);
      return;
    }

    // Tính link trước rồi mới quyết định modal: notification NEWS/PARTNER cũ không có
    // targetUrl (canOpen=false) vẫn điều hướng được nhờ rewrite theo relatedType/relatedId.
    const link = getNotificationLink(item, user);
    setIsOpen(false);
    if (!link) {
      setDetailModalItem(item);
      return;
    }
    navigate(link);
    onNavigate?.();
  };

  const handleMarkAllAsRead = async () => {
    await markAllAsRead();
  };

  return (
    <div className="relative" ref={popoverRef}>
      <button
        onClick={() => setIsOpen(!isOpen)}
        className="p-2 text-gray-600 hover:bg-gray-100 rounded-full transition-colors relative"
        aria-label={t('notifications:bell.ariaLabel')}
        title={t('notifications:bell.tooltip')}
        data-variant={variant}
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
              <h3 className="font-semibold text-gray-800">{t('notifications:dropdown.title')}</h3>
              {unreadCount > 0 && (
                <button
                  onClick={handleMarkAllAsRead}
                  className="text-xs font-medium text-[#004c91] hover:underline"
                >
                  {t('notifications:dropdown.markAllRead')}
                </button>
              )}
            </div>

            <div className="max-h-[70vh] overflow-y-auto">
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
                        <span className="block text-sm font-semibold text-gray-900">
                          {t('notifications:dropdown.pendingFeedbackTitle')}
                        </span>
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
                  <span className="text-sm">{t('notifications:dropdown.loading')}</span>
                </div>
              ) : items.length === 0 ? (
                pendingFeedback.length === 0 ? (
                  <div className="p-6 text-center text-gray-500">
                    <p className="text-sm">{t('notifications:dropdown.empty')}</p>
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

            <div className="border-t border-gray-100 bg-gray-50/50 px-4 py-2 text-center">
              <button
                onClick={() => {
                  setIsOpen(false);
                  navigate('/notifications');
                  onNavigate?.();
                }}
                className="text-xs font-medium text-[#004c91] hover:underline"
              >
                {t('notifications:dropdown.viewAll')}
              </button>
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

      <HostFeedbackModal
        open={hostFeedbackVisitInstanceId !== null}
        visitInstanceId={hostFeedbackVisitInstanceId}
        onClose={() => setHostFeedbackVisitInstanceId(null)}
      />

      <VisitorFeedbackDetailModal
        open={visitorFeedbackVisitInstanceId !== null}
        visitInstanceId={visitorFeedbackVisitInstanceId}
        onClose={() => setVisitorFeedbackVisitInstanceId(null)}
      />

      <NotificationDetailModal item={detailModalItem} onClose={() => setDetailModalItem(null)} />
    </div>
  );
}
