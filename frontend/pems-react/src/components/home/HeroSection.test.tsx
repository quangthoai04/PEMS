import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

/**
 * Homepage bug fix (CanhIter3FixBug §2-§8): the Hero's primary CTA used to hard-code
 * `useVisitEntryCta('public')`, so a signed-in Visitor/Staff/Staff Leader landing on the (still
 * public-looking) homepage got the anonymous OTP form instead of the same authenticated,
 * self-registration form the dashboard opens for them. The CTA's mode must follow sign-in state,
 * not which page it is rendered on.
 */

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({ useNavigate: () => navigateMock }));

vi.mock('./LazyGlobeShowcase', () => ({ LazyGlobeShowcase: () => null }));

vi.mock('../../features/visit-request/components/v2/VisitRequestV2Modal', () => ({
  VisitRequestV2Modal: ({ isOpen, mode }: { isOpen: boolean; mode: string }) =>
    isOpen ? <div data-testid="v2-modal" data-mode={mode} /> : null,
}));

const retryMock = vi.fn();
const capabilityMock = vi.fn();
vi.mock('../../shared/features/perCampusV2Capability', () => ({
  usePerCampusV2Capability: () => capabilityMock(),
}));

type AuthState = {
  user: { userId: string } | null;
  isAuthenticated: boolean;
  isReady: boolean;
  effectiveRole: string | null;
};
let authState: AuthState = {
  user: null, isAuthenticated: false, isReady: true, effectiveRole: null,
};
vi.mock('../../shared/auth/AuthContext', () => ({
  useAuthContext: () => authState,
}));

import { HeroSection } from './HeroSection';

const primaryCta = () => screen.getAllByRole('button')[0];

describe('HeroSection — homepage primary CTA follows sign-in state', () => {
  beforeEach(() => {
    capabilityMock.mockReturnValue({ status: 'ready', enabled: true, readEnabled: true, writeEnabled: true, retry: retryMock });
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
    navigateMock.mockClear();
  });

  it('signed-out visitor → public form (editable registrant, OTP)', () => {
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
    render(<HeroSection />);

    fireEvent.click(primaryCta());

    expect(screen.getByTestId('v2-modal')).toHaveAttribute('data-mode', 'public');
  });

  it('authenticated Visitor → authenticated form, same as the dashboard (no OTP)', () => {
    authState = { user: { userId: '101' }, isAuthenticated: true, isReady: true, effectiveRole: 'VISITOR' };
    render(<HeroSection />);

    fireEvent.click(primaryCta());

    expect(screen.getByTestId('v2-modal')).toHaveAttribute('data-mode', 'authenticated');
  });

  it('authenticated Staff → authenticated form (profile auto-load, no OTP)', () => {
    authState = { user: { userId: '202' }, isAuthenticated: true, isReady: true, effectiveRole: 'STAFF' };
    render(<HeroSection />);

    fireEvent.click(primaryCta());

    expect(screen.getByTestId('v2-modal')).toHaveAttribute('data-mode', 'authenticated');
  });

  it('authenticated Staff Leader → authenticated form (profile auto-load, no OTP)', () => {
    authState = { user: { userId: '303' }, isAuthenticated: true, isReady: true, effectiveRole: 'STAFF_LEADER' };
    render(<HeroSection />);

    fireEvent.click(primaryCta());

    expect(screen.getByTestId('v2-modal')).toHaveAttribute('data-mode', 'authenticated');
  });

  it.each(['ADMIN', 'HO', 'DEPARTMENT_LEAD', 'DEPARTMENT', 'STUDENT'])(
    'authenticated FORBIDDEN role %s → never the form, never public fallback, routed to /403',
    (role) => {
      authState = { user: { userId: '404' }, isAuthenticated: true, isReady: true, effectiveRole: role };
      render(<HeroSection />);

      fireEvent.click(primaryCta());

      expect(screen.queryByTestId('v2-modal')).toBeNull();
      expect(navigateMock).toHaveBeenCalledWith('/403');
    },
  );

  it('does not guess public while auth is still bootstrapping, and disables the CTA', () => {
    authState = { user: null, isAuthenticated: false, isReady: false, effectiveRole: null };
    render(<HeroSection />);

    expect(primaryCta()).toBeDisabled();

    fireEvent.click(primaryCta());
    expect(screen.queryByTestId('v2-modal')).toBeNull();
    expect(navigateMock).not.toHaveBeenCalled();
  });
});
