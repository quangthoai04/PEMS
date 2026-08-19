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
import { formatVietnamRelative, type UiLanguage } from '../../../shared/utils/vietnamTime';
import { resolveNotificationPresentation } from '../utils/resolveNotificationPresentation';
import { resolveNotificationDestination } from '../utils/resolveNotificationDestination';

export function timeAgo(dateStr: string): string {
  // API trả ISO +07:00 (giờ Việt Nam) — parse offset-aware, tuyệt đối không nối 'Z'.
  return formatVietnamRelative(dateStr);
}

/** Item ảo (client-side) đại diện cho một lời mời đánh giá đoàn (pendingFeedback), trộn chung
 * dòng thời gian với notification thật thay vì ghim cứng ở đầu — sắp theo thời gian thực. */
type FeedbackInviteItem = {
  kind: 'feedback-invite';
  key: string;
  visitInstanceId: number;
  delegationName: string;
  campusName?: string | null;
  createdAt: string;
};

type DisplayItem = (NotificationItem & { kind: 'notification' }) | FeedbackInviteItem;

interface NotificationBellButtonProps {
  variant?: 'header-desktop' | 'header-mobile' | 'dashboard';
  onNavigate?: () => void;
}

export function NotificationBellButton({ variant = 'dashboard', onNavigate }: NotificationBellButtonProps) {
  const { t, i18n } = useTranslation(['notifications']);
  const language = i18n.language as UiLanguage;
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

  // Lời mời đánh giá đoàn trộn vào cùng danh sách theo mốc thời gian thực (plannedStartAt),
  // sắp xếp mới nhất lên đầu — không còn ghim cố định ở trên cùng bất kể thời gian.
  const feedbackInviteItems: FeedbackInviteItem[] = pendingFeedback.map((p) => ({
    kind: 'feedback-invite',
    key: `fb-${p.visitInstanceId}`,
    visitInstanceId: p.visitInstanceId,
    delegationName: p.delegationName,
    campusName: p.campusName,
    createdAt: p.plannedStartAt || new Date().toISOString(),
  }));
  const notificationDisplayItems: DisplayItem[] = items.map((it) => ({ ...it, kind: 'notification' as const }));
  const displayItems: DisplayItem[] = [...feedbackInviteItems, ...notificationDisplayItems].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  );

  const handleItemClick = async (item: DisplayItem) => {
    if (item.kind === 'feedback-invite') {
      setIsOpen(false);
      setFeedbackModalInstanceId(item.visitInstanceId);
      return;
    }

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
    const link = resolveNotificationDestination(item, user);
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

            <div className="max-h-[70dvh] overflow-y-auto">
              {loading && displayItems.length === 0 ? (
                <div className="flex items-center justify-center gap-2 py-8 text-gray-400">
                  <div className="w-4 h-4 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
                  <span className="text-sm">{t('notifications:dropdown.loading')}</span>
                </div>
              ) : displayItems.length === 0 ? (
                <div className="p-6 text-center text-gray-500">
                  <p className="text-sm">{t('notifications:dropdown.empty')}</p>
                </div>
              ) : (
                <div className="flex flex-col divide-y divide-gray-50">
                  {displayItems.map((item) =>
                    item.kind === 'feedback-invite' ? (
                      <button
                        key={item.key}
                        onClick={() => handleItemClick(item)}
                        className="flex w-full items-start gap-2.5 p-4 text-left transition-colors hover:bg-orange-50 bg-orange-50/40"
                      >
                        <Star className="mt-0.5 h-4 w-4 shrink-0 text-[#F37021]" />
                        <span className="min-w-0">
                          <span className="block text-sm font-semibold text-gray-900">
                            {t('notifications:dropdown.pendingFeedbackTitle')}
                          </span>
                          <span className="block truncate text-xs text-gray-500">
                            {item.delegationName}{item.campusName ? ` • ${item.campusName}` : ''}
                          </span>
                        </span>
                      </button>
                    ) : (() => {
                      const resolved = resolveNotificationPresentation(
                        { metadataJson: item.metadataJson, createdAt: item.createdAt, legacyTitle: item.title, legacyMessage: item.message },
                        language,
                        t,
                      );
                      return (
                        <button
                          key={item.notificationId}
                          onClick={() => handleItemClick(item)}
                          className={`flex flex-col gap-1 p-4 text-left transition-colors hover:bg-gray-50 w-full ${
                            !item.isRead ? 'bg-blue-50/40' : ''
                          }`}
                        >
                          <div className="flex items-start justify-between gap-2">
                            <span className={`text-sm ${!item.isRead ? 'font-medium text-gray-900' : 'font-normal text-gray-600'}`}>
                              {!item.isRead && (
                                <span className="inline-block w-1.5 h-1.5 bg-blue-500 rounded-full mr-1.5 mb-0.5 align-middle" />
                              )}
                              {resolved.title}
                            </span>
                            <span className="text-[10px] text-gray-400 shrink-0 mt-0.5 whitespace-nowrap">
                              {resolved.relativeTime}
                            </span>
                          </div>
                          {resolved.message && (
                            <p className="text-xs text-gray-500 line-clamp-2 pl-0">
                              {resolved.message}
                            </p>
                          )}
                        </button>
                      );
                    })()
                  )}
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
