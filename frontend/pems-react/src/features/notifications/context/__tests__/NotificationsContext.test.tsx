/**
 * `NotificationsProvider` wraps the whole app (main.tsx) — its poll/fetch effect runs on EVERY
 * page, including the public homepage, whenever `isAuthenticated` is true. `isAuthenticated` is
 * seeded OPTIMISTICALLY and synchronously from whatever `pems_user` sits in localStorage (by
 * design — a genuinely valid session must not flash logged-out on a hard refresh), which is true
 * even for a stale/revoked session left over from a previous visit. Firing an authenticated
 * request on that alone 401'd on a guest's very first paint and surfaced a false "session expired"
 * toast. `isReady` is the flag AuthContext already built for exactly this (it only flips once
 * bootstrap has verified or cleared the stored session) — this pins that the provider now waits
 * for it too, instead of acting on the optimistic value alone.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, waitFor } from '@testing-library/react';
import { NotificationsProvider } from '../NotificationsContext';

const getUnreadCountMock = vi.fn();
const getNotificationsMock = vi.fn();
const getMyPendingMock = vi.fn();

vi.mock('../../api/notificationsApi', () => ({
  notificationsApi: {
    getUnreadCount: (...a: unknown[]) => getUnreadCountMock(...a),
    getNotifications: (...a: unknown[]) => getNotificationsMock(...a),
    markAsRead: vi.fn(),
    markAllAsRead: vi.fn(),
  },
}));
vi.mock('../../../feedbacks/api/visitFeedbackApi', () => ({
  visitFeedbackApi: { getMyPending: (...a: unknown[]) => getMyPendingMock(...a) },
}));

let authState: { isAuthenticated: boolean; isReady: boolean } = { isAuthenticated: false, isReady: true };
vi.mock('../../../../shared/hooks/useAuth', () => ({
  useAuth: () => authState,
}));

const renderProvider = () => render(<NotificationsProvider>{null}</NotificationsProvider>);

beforeEach(() => {
  vi.clearAllMocks();
  getUnreadCountMock.mockResolvedValue({ unreadCount: 0 });
  getNotificationsMock.mockResolvedValue({ items: [] });
  getMyPendingMock.mockResolvedValue({ items: [] });
});

describe('NotificationsProvider — waits for isReady, not just isAuthenticated', () => {
  it('does NOT fetch while bootstrap has not settled, even if the optimistic user looks authenticated', async () => {
    // Exactly the public-homepage-with-a-stale-token shape: AuthContext seeded `user` from
    // localStorage (isAuthenticated=true) but bootstrap has not yet verified it (isReady=false).
    authState = { isAuthenticated: true, isReady: false };
    renderProvider();

    await new Promise((r) => setTimeout(r, 50));
    expect(getUnreadCountMock).not.toHaveBeenCalled();
    expect(getNotificationsMock).not.toHaveBeenCalled();
  });

  it('fetches once isReady flips true with a genuinely authenticated user', async () => {
    authState = { isAuthenticated: true, isReady: true };
    renderProvider();

    await waitFor(() => expect(getUnreadCountMock).toHaveBeenCalledTimes(1));
    expect(getNotificationsMock).toHaveBeenCalledTimes(1);
  });

  it('never fetches for a guest (isReady true, isAuthenticated false) — the ordinary logged-out case', async () => {
    authState = { isAuthenticated: false, isReady: true };
    renderProvider();

    await new Promise((r) => setTimeout(r, 50));
    expect(getUnreadCountMock).not.toHaveBeenCalled();
  });
});
