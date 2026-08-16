import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

vi.mock('../../../features/visit-request/api/visitRequestV2Api', () => ({
  getOperationalContactInvitationInfo: vi.fn(),
  acceptOperationalContactInvitation: vi.fn(),
  declineOperationalContactInvitation: vi.fn(),
  publicAcceptOperationalContactInvitation: vi.fn(),
  publicDeclineOperationalContactInvitation: vi.fn(),
}));

const navigate = vi.fn();
vi.mock('react-router-dom', () => ({
  useNavigate: () => navigate,
  useParams: () => ({ token: 'raw-token' }),
}));

vi.mock('../../../shared/hooks/useAuth', () => ({
  useAuth: () => ({ isAuthenticated: false, user: null }),
}));

import VisitContactInvitationPage from '../VisitContactInvitationPage';
import {
  getOperationalContactInvitationInfo,
  publicAcceptOperationalContactInvitation,
  publicDeclineOperationalContactInvitation,
} from '../../../features/visit-request/api/visitRequestV2Api';

/**
 * A live INITIAL_CONFIRMATION invitation as the anonymous confirmation-info endpoint answers it.
 * `intendedAction` is overridden per test — it is the whole subject here.
 */
const invitation = {
  status: 'PENDING',
  actionable: true,
  kind: 'INITIAL_CONFIRMATION',
  maskedEmail: 'o***@example.com',
  requestCode: 'VR-2026-0001',
  campusName: 'FPTU Hà Nội',
  delegationName: 'Đoàn khách ABC',
  plannedStartAt: '2026-09-01T09:00:00',
  plannedEndAt: '2026-09-01T11:00:00',
  expiresAt: '2026-08-12T09:00:00',
  requiresGoogleLoginEmailMatch: false,
  intendedAction: 'ACCEPT' as string | null | undefined,
};

const info = (over: Partial<typeof invitation>) => ({ ...invitation, ...over });

const load = (over: Partial<typeof invitation>) =>
  vi.mocked(getOperationalContactInvitationInfo).mockResolvedValue(
    info(over) as unknown as Awaited<ReturnType<typeof getOperationalContactInvitationInfo>>,
  );

/**
 * Renders and waits for the anonymous GET to settle, so assertions never race the loader.
 *
 * Asserts the ENGLISH strings: `src/test/setup.ts` documents that jsdom reports
 * `navigator.language = en-US`, so every test in this suite renders EN by default (no VI
 * override here) — matching how `VisitContactInvitationPage.tsx` actually renders under test.
 */
const renderLoaded = async () => {
  render(<VisitContactInvitationPage kind="claim" />);
  await waitFor(() => expect(screen.queryByText('Loading invitation…')).toBeNull());
};

const acceptCta = () => screen.queryByRole('button', { name: /Accept the contact role/i });
const declineCta = () => screen.queryByRole('button', { name: /Confirm decline/i });
/** The old two-button page also offered a plain "Decline" toggle next to accept — it no longer
 * exists (single mutationAction-driven button now), so this must always resolve to null. */
const declineToggle = () => screen.queryByRole('button', { name: /^Decline$/i });

/**
 * An invitation email carries ONE link per answer and each token is minted for exactly one of them,
 * so the page must offer exactly the action the reader's own link can perform. Showing both is not a
 * cosmetic slip: pressing the wrong one spends the visit's single confirmation attempt on a refusal
 * ("liên kết không hợp lệ") for an answer the reader was entitled to give through the other link.
 */
describe('VisitContactInvitationPage — the link decides which action is offered', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(publicAcceptOperationalContactInvitation).mockResolvedValue(
      { message: 'ok' } as unknown as Awaited<ReturnType<typeof publicAcceptOperationalContactInvitation>>,
    );
    vi.mocked(publicDeclineOperationalContactInvitation).mockResolvedValue(
      { message: 'ok' } as unknown as Awaited<ReturnType<typeof publicDeclineOperationalContactInvitation>>,
    );
  });

  it('an ACCEPT link offers accept only, and posts accept exactly once', async () => {
    load({ intendedAction: 'ACCEPT' });
    await renderLoaded();

    expect(acceptCta()).not.toBeNull();
    expect(declineCta()).toBeNull();
    expect(declineToggle()).toBeNull();

    acceptCta()!.click();
    await waitFor(() => expect(publicAcceptOperationalContactInvitation).toHaveBeenCalledTimes(1));
    expect(publicDeclineOperationalContactInvitation).not.toHaveBeenCalled();
  });

  it('a DECLINE link offers decline only, and posts decline exactly once', async () => {
    load({ intendedAction: 'DECLINE' });
    await renderLoaded();

    expect(declineCta()).not.toBeNull();
    expect(acceptCta()).toBeNull();

    declineCta()!.click();
    await waitFor(() => expect(publicDeclineOperationalContactInvitation).toHaveBeenCalledTimes(1));
    expect(publicAcceptOperationalContactInvitation).not.toHaveBeenCalled();
  });

  it('a settled invitation (actionable=false) offers no mutation at all', async () => {
    load({ status: 'APPLIED', actionable: false, intendedAction: 'ACCEPT' });
    await renderLoaded();

    expect(acceptCta()).toBeNull();
    expect(declineCta()).toBeNull();
    expect(declineToggle()).toBeNull();
  });

  // The fail-safe. A field the server did not send must never be read as "both are allowed" — the
  // absent case has to land on the same silence as a settled invitation, not on a guess.
  it.each([
    ['null', null],
    ['undefined', undefined],
  ])('a token whose intendedAction is %s offers no mutation and says why', async (_label, action) => {
    load({ intendedAction: action });
    await renderLoaded();

    expect(acceptCta()).toBeNull();
    expect(declineCta()).toBeNull();
    expect(declineToggle()).toBeNull();
    expect(screen.getByRole('alert').textContent).toMatch(/cannot perform any action right now/i);
  });
});
