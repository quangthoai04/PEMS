import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

/**
 * Pins the backend-pagination fix (PEMS_FIX_NOTIFICATION_PAGINATION): each filter tab must send the
 * right `categories`/`isActionRequired`/`isRead` params for the server to filter BEFORE it counts and
 * paginates — never a client-side `.filter()` over an already-paginated page (removed from
 * `NotificationsPage.tsx`'s fetch effect). Also pins the page-index clamp: a stale `currentPage` that
 * no longer fits the newly-filtered set's `totalPages` must fall back into range on its own, not
 * request an out-of-range page and render blank.
 */

const getNotificationsMock = vi.fn();
const navigateMock = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof import('react-router-dom')>('react-router-dom');
  return { ...actual, useNavigate: () => navigateMock };
});

vi.mock('../../../features/notifications/api/notificationsApi', () => ({
  notificationsApi: {
    getNotifications: (...args: unknown[]) => getNotificationsMock(...args),
    getUnreadCount: vi.fn().mockResolvedValue({ unreadCount: 0 }),
    markAsRead: vi.fn().mockResolvedValue({ success: true }),
    markAllAsRead: vi.fn().mockResolvedValue({ updatedCount: 0 }),
  },
}));

vi.mock('../../../shared/hooks/useAuth', () => ({
  useAuth: () => ({ user: { userId: '1', roleCode: 'STAFF', subRole: 'LEADER' } }),
}));

vi.mock('../../../features/notifications/context/NotificationsContext', () => ({
  useNotifications: () => ({
    markAsRead: vi.fn(),
    markAllAsRead: vi.fn(),
    unreadCount: 0,
    pendingFeedback: [],
    fetchPendingFeedback: vi.fn(),
  }),
}));

vi.mock('../../../features/feedbacks/components/HostFeedbackModal', () => ({ HostFeedbackModal: () => null }));
vi.mock('../../../features/feedbacks/components/VisitFeedbackModal', () => ({ VisitFeedbackModal: () => null }));
vi.mock('../../../features/feedbacks/components/VisitorFeedbackDetailModal', () => ({ VisitorFeedbackDetailModal: () => null }));
vi.mock('../../../features/notifications/components/NotificationDetailModal', () => ({ NotificationDetailModal: () => null }));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
    i18n: { language: 'vi' },
  }),
}));

import { NotificationsPage } from '../NotificationsPage';

const emptyPage = (page: number, pageSize: number) =>
  ({ items: [], page, pageSize, totalItems: 0, totalPages: 0 });

const renderPage = () => render(<MemoryRouter><NotificationsPage /></MemoryRouter>);

const clickFilter = (key: string) => fireEvent.click(screen.getByText(`notifications:filters.${key}`));

beforeEach(() => {
  getNotificationsMock.mockReset();
  navigateMock.mockReset();
  getNotificationsMock.mockResolvedValue(emptyPage(1, 10));
});

describe('NotificationsPage — filter tabs send the right params (backend filters before paginating)', () => {
  it('"Đoàn khách" (multi-category) sends categories=VISIT,REMINDER, never a client-side post-filter', async () => {
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalled());
    getNotificationsMock.mockClear();

    clickFilter('visit');

    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(
      expect.objectContaining({ categories: ['VISIT', 'REMINDER'], page: 1, pageSize: 10 }),
    ));
    // Neither isRead nor isActionRequired belongs on a plain category tab.
    const params = getNotificationsMock.mock.calls.at(-1)?.[0];
    expect(params.isRead).toBeUndefined();
    expect(params.isActionRequired).toBeUndefined();
  });

  it('"Hậu cần" (multi-category) sends categories=LOGISTICS,HANDOVER', async () => {
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalled());
    getNotificationsMock.mockClear();

    clickFilter('logistics');

    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(
      expect.objectContaining({ categories: ['LOGISTICS', 'HANDOVER'] }),
    ));
  });

  it('"Hệ thống" (multi-category) sends categories=SYSTEM,ACCOUNT,GENERAL', async () => {
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalled());
    getNotificationsMock.mockClear();

    clickFilter('system');

    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(
      expect.objectContaining({ categories: ['SYSTEM', 'ACCOUNT', 'GENERAL'] }),
    ));
  });

  it('"Cần hành động" sends isActionRequired=true, no category param', async () => {
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalled());
    getNotificationsMock.mockClear();

    clickFilter('actionRequired');

    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(
      expect.objectContaining({ isActionRequired: true }),
    ));
    const params = getNotificationsMock.mock.calls.at(-1)?.[0];
    expect(params.categories).toBeUndefined();
    expect(params.isRead).toBeUndefined();
  });

  it('"Chưa đọc" sends isRead=false, no category param', async () => {
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalled());
    getNotificationsMock.mockClear();

    clickFilter('unread');

    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(
      expect.objectContaining({ isRead: false }),
    ));
    const params = getNotificationsMock.mock.calls.at(-1)?.[0];
    expect(params.categories).toBeUndefined();
    expect(params.isActionRequired).toBeUndefined();
  });
});

describe('NotificationsPage — page index resets correctly', () => {
  it('changing the filter resets currentPage back to 1', async () => {
    // Enough pages that a "Next" click is possible.
    getNotificationsMock.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 50, totalPages: 5 });
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalled());

    fireEvent.click(screen.getByTestId('notifications-page-next'));
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(expect.objectContaining({ page: 2 })));

    getNotificationsMock.mockClear();
    clickFilter('visit');
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(expect.objectContaining({ page: 1 })));
  });

  it('changing the page size resets currentPage back to 1', async () => {
    getNotificationsMock.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 50, totalPages: 5 });
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalled());

    fireEvent.click(screen.getByTestId('notifications-page-next'));
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(expect.objectContaining({ page: 2 })));

    getNotificationsMock.mockClear();
    fireEvent.change(screen.getByTestId('notifications-page-size'), { target: { value: '20' } });
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(expect.objectContaining({ page: 1, pageSize: 20 })));
  });

  it('the Next button is disabled once the current page matches the known totalPages — never lets the UI request a page past the end', async () => {
    getNotificationsMock.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 12, totalPages: 2 });
    renderPage();
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(expect.objectContaining({ page: 1 })));

    fireEvent.click(screen.getByTestId('notifications-page-next'));
    await waitFor(() => expect(getNotificationsMock).toHaveBeenCalledWith(expect.objectContaining({ page: 2 })));

    expect(screen.getByTestId('notifications-page-next')).toBeDisabled();
  });
});
