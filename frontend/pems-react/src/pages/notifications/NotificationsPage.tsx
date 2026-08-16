import React, { useEffect, useState } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../shared/hooks/useAuth';
import { useNotifications } from '../../features/notifications/context/NotificationsContext';
import { notificationsApi } from '../../features/notifications/api/notificationsApi';
import { NotificationFilterBar } from '../../features/notifications/components/NotificationFilterBar';
import { getNotificationLink } from '../../features/notifications/components/NotificationBellButton';
import { HostFeedbackModal } from '../../features/feedbacks/components/HostFeedbackModal';
import { VisitFeedbackModal } from '../../features/feedbacks/components/VisitFeedbackModal';
import { VisitorFeedbackDetailModal } from '../../features/feedbacks/components/VisitorFeedbackDetailModal';
import { NotificationDetailModal } from '../../features/notifications/components/NotificationDetailModal';
import type { NotificationItem } from '../../features/notifications/types/notification.types';
import {
  NOTIFICATION_FILTER_CATEGORY_MAP,
  getVisibleNotificationFilters,
  type NotificationFilterKey,
} from '../../features/notifications/constants/notificationFilters';
import { resolveNotificationPresentation } from '../../features/notifications/utils/resolveNotificationPresentation';
import type { UiLanguage } from '../../shared/utils/vietnamTime';

type PaginatedResult<T> = { items: T[]; page: number; pageSize: number; totalItems: number; totalPages: number };

/** Item ảo (client-side) đại diện cho một lời mời đánh giá đoàn (pendingFeedback), trộn chung
 * dòng thời gian với notification thật để trang /notifications hiển thị đầy đủ, không bỏ sót. */
type FeedbackInviteItem = {
  kind: 'feedback-invite';
  key: string;
  visitInstanceId: number;
  delegationName: string;
  campusName?: string | null;
  createdAt: string;
};

type DisplayItem = (NotificationItem & { kind: 'notification' }) | FeedbackInviteItem;

export function NotificationsPage() {
  const { t, i18n } = useTranslation(['notifications']);
  const language = i18n.language as UiLanguage;
  const navigate = useNavigate();
  const { user } = useAuth();
  const { markAsRead, markAllAsRead, unreadCount, pendingFeedback, fetchPendingFeedback } = useNotifications();

  const visibleFilters = getVisibleNotificationFilters(user?.roleCode);
  const [filter, setFilter] = useState<NotificationFilterKey>('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [result, setResult] = useState<PaginatedResult<NotificationItem> | null>(null);
  const [loading, setLoading] = useState(false);
  const [hostFeedbackVisitInstanceId, setHostFeedbackVisitInstanceId] = useState<number | null>(null);
  const [visitorFeedbackVisitInstanceId, setVisitorFeedbackVisitInstanceId] = useState<number | null>(null);
  const [feedbackModalInstanceId, setFeedbackModalInstanceId] = useState<number | string | null>(null);
  const [detailModalItem, setDetailModalItem] = useState<NotificationItem | null>(null);

  // Nếu filter đang chọn không còn hợp lệ với role hiện tại (ví dụ đổi tài khoản), rơi về 'all'.
  useEffect(() => {
    if (!visibleFilters.includes(filter)) {
      setFilter('all');
      setCurrentPage(1);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visibleFilters.join(',')]);

  const handleFilterChange = (next: NotificationFilterKey) => {
    setFilter(next);
    setCurrentPage(1);
  };

  useEffect(() => {
    let cancelled = false;
    setLoading(true);

    const params: { page: number; pageSize: number; isRead?: boolean; isActionRequired?: boolean; category?: string } = {
      page: currentPage,
      pageSize,
    };
    if (filter === 'unread') params.isRead = false;
    else if (filter === 'actionRequired') params.isActionRequired = true;
    else if (filter !== 'all') {
      const categories = NOTIFICATION_FILTER_CATEGORY_MAP[filter];
      if (categories?.length === 1) params.category = categories[0];
    }

    notificationsApi.getNotifications(params)
      .then((data) => {
        if (cancelled) return;
        // Lớp filter client-side dự phòng cho nhãn multi-category (visit/system) hoặc nếu
        // backend bỏ qua param category lạ — đảm bảo danh sách hiển thị luôn đúng.
        if (filter !== 'all' && filter !== 'unread' && filter !== 'actionRequired') {
          const categories = NOTIFICATION_FILTER_CATEGORY_MAP[filter];
          if (categories) {
            data.items = data.items.filter((it) => categories.includes(it.category));
          }
        }
        setResult(data);
      })
      .catch(() => setResult(null))
      .finally(() => { if (!cancelled) setLoading(false); });

    return () => { cancelled = true; };
  }, [filter, currentPage, pageSize]);

  // Lời mời đánh giá đoàn (pendingFeedback) được trộn vào cùng danh sách theo mốc thời gian
  // planned start, để "Tất cả" và tab "Feedback" không bỏ sót lời mời này. Chỉ hiện ở trang 1
  // khi không lọc theo "Chưa đọc"/"Cần hành động" (2 filter này chỉ có ý nghĩa với notification thật).
  const showFeedbackInvites = currentPage === 1 && (filter === 'all' || filter === 'feedback');
  const feedbackInviteItems: FeedbackInviteItem[] = showFeedbackInvites
    ? pendingFeedback.map((p) => ({
        kind: 'feedback-invite',
        key: `fb-${p.visitInstanceId}`,
        visitInstanceId: p.visitInstanceId,
        delegationName: p.delegationName,
        campusName: p.campusName,
        createdAt: p.plannedStartAt || new Date().toISOString(),
      }))
    : [];

  const notificationDisplayItems: DisplayItem[] = (result?.items || []).map((it) => ({ ...it, kind: 'notification' as const }));
  const displayItems: DisplayItem[] = [...feedbackInviteItems, ...notificationDisplayItems].sort(
    (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
  );

  const handleItemClick = async (item: DisplayItem) => {
    if (item.kind === 'feedback-invite') {
      setFeedbackModalInstanceId(item.visitInstanceId);
      return;
    }

    if (!item.isRead) await markAsRead(item.notificationId);

    if (item.actionType === 'OPEN_HOST_FEEDBACK_MODAL' && item.visitInstanceId) {
      setHostFeedbackVisitInstanceId(item.visitInstanceId);
      return;
    }
    if (item.actionType === 'OPEN_VISITOR_FEEDBACK_MODAL' && item.visitInstanceId) {
      setVisitorFeedbackVisitInstanceId(item.visitInstanceId);
      return;
    }
    // Tính link trước rồi mới quyết định modal: notification NEWS/PARTNER cũ không có
    // targetUrl (canOpen=false) vẫn điều hướng được nhờ rewrite theo relatedType/relatedId.
    const link = getNotificationLink(item, user);
    if (!link) {
      setDetailModalItem(item);
      return;
    }
    navigate(link);
  };

  const handleMarkAllAsRead = async () => {
    await markAllAsRead();
  };

  return (
    <div className="bg-slate-50 min-h-screen pt-20 pb-16">
      <div className="w-full max-w-5xl mx-auto px-4 sm:px-6 pt-8">
        <div className="flex flex-col md:flex-row justify-between items-start md:items-center gap-4 mb-4">
          <div>
            <h1 className="text-3xl font-bold text-[#004c91] tracking-tight">{t('notifications:page.title')}</h1>
            <p className="text-gray-500 mt-1 font-medium">{t('notifications:page.subtitle')}</p>
          </div>
          {unreadCount > 0 && (
            <button
              onClick={handleMarkAllAsRead}
              className="px-4 py-2 bg-[#004c91] text-white text-sm font-bold rounded-xl hover:opacity-90 transition-colors"
            >
              {t('notifications:actions.markAllRead')}
            </button>
          )}
        </div>

        <div className="mb-4">
          <NotificationFilterBar value={filter} onChange={handleFilterChange} />
        </div>

        <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
          {loading ? (
            <div className="flex items-center justify-center gap-2 py-16 text-gray-400">
              <div className="w-5 h-5 border-2 border-[#004c91] border-t-transparent rounded-full animate-spin" />
              <span className="text-sm">{t('notifications:dropdown.loading')}</span>
            </div>
          ) : displayItems.length === 0 ? (
            <div className="py-16 text-center text-gray-500">
              <p className="text-sm font-medium">{t('notifications:empty.title')}</p>
              <p className="text-xs mt-1">{t('notifications:empty.description')}</p>
            </div>
          ) : (
            <div className="flex flex-col divide-y divide-gray-50">
              {displayItems.map((item) =>
                item.kind === 'feedback-invite' ? (
                  <button
                    key={item.key}
                    onClick={() => handleItemClick(item)}
                    className="flex flex-col gap-1 p-4 text-left transition-colors hover:bg-orange-50 w-full bg-orange-50/40"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <span className="text-sm font-medium text-gray-900">
                        <span className="inline-block w-1.5 h-1.5 bg-[#F37021] rounded-full mr-1.5 mb-0.5 align-middle" />
                        {t('notifications:dropdown.pendingFeedbackTitle')}
                        <span className="ml-2 inline-block px-1.5 py-0.5 bg-orange-100 text-orange-700 text-[10px] font-bold rounded">
                          {t('notifications:filters.actionRequired')}
                        </span>
                      </span>
                    </div>
                    <p className="text-xs text-gray-500 line-clamp-2">
                      {item.delegationName}{item.campusName ? ` • ${item.campusName}` : ''}
                    </p>
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
                        <span className={`text-sm font-medium ${!item.isRead ? 'text-gray-900' : 'text-gray-600'}`}>
                          {!item.isRead && (
                            <span className="inline-block w-1.5 h-1.5 bg-blue-500 rounded-full mr-1.5 mb-0.5 align-middle" />
                          )}
                          {resolved.title}
                          {item.isActionRequired && (
                            <span className="ml-2 inline-block px-1.5 py-0.5 bg-orange-100 text-orange-700 text-[10px] font-bold rounded">
                              {t('notifications:filters.actionRequired')}
                            </span>
                          )}
                        </span>
                        <span className="text-[10px] text-gray-400 shrink-0 mt-0.5 whitespace-nowrap">
                          {resolved.relativeTime}
                        </span>
                      </div>
                      {resolved.message && <p className="text-xs text-gray-500 line-clamp-2">{resolved.message}</p>}
                    </button>
                  );
                })()
              )}
            </div>
          )}
        </div>

        <div className="p-4 mt-4 border border-slate-200 rounded-2xl flex flex-col md:flex-row items-center justify-between gap-4 bg-slate-50">
          <div className="flex items-center gap-2 text-sm text-slate-500 font-medium">
            <span>{t('notifications:pagination.showing')}</span>
            <select
              value={pageSize}
              onChange={(e) => { setPageSize(Number(e.target.value)); setCurrentPage(1); }}
              className="px-2 py-1 bg-white border border-slate-200 rounded-lg outline-none cursor-pointer focus:border-[#004c91] transition-colors"
            >
              <option value={5}>5</option>
              <option value={10}>10</option>
              <option value={20}>20</option>
            </select>
            <span>{t('notifications:pagination.perPage')}</span>
          </div>

          <div className="flex items-center gap-2">
            <button
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              disabled={currentPage === 1}
              className="cursor-pointer p-1 text-slate-500 hover:bg-slate-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronLeft className="w-5 h-5" />
            </button>
            <div className="text-sm font-bold text-slate-700">
              {t('notifications:pagination.page')} {currentPage} {t('notifications:pagination.of')} {Math.max(1, result?.totalPages || 1)}
            </div>
            <button
              onClick={() => setCurrentPage((p) => p + 1)}
              disabled={currentPage >= (result?.totalPages || 1)}
              className="cursor-pointer p-1 text-slate-500 hover:bg-slate-200 rounded-lg disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              <ChevronRight className="w-5 h-5" />
            </button>
          </div>
        </div>
      </div>

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
