import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({ useNavigate: () => navigateMock }));

// The CTA now only has V2 modal or toast.error on error/disabled. Surfaces `mode` on the DOM so
// tests can tell a public shell apart from an authenticated one without reaching into props.
vi.mock('../../features/visit-request/components/v2/VisitRequestV2Modal', () => ({
  VisitRequestV2Modal: ({ isOpen, mode }: { isOpen: boolean; mode: string }) =>
    isOpen ? <div data-testid="v2-modal" data-mode={mode} /> : null,
}));

// react-hot-toast: capture the error/loading calls the entry CTA makes.
const toastError = vi.fn();
const toastLoading = vi.fn();
const toastDismiss = vi.fn();
vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { error: (...a: unknown[]) => toastError(...a), loading: (...a: unknown[]) => toastLoading(...a), dismiss: (...a: unknown[]) => toastDismiss(...a) }),
}));

const retryMock = vi.fn();
const capabilityMock = vi.fn();
vi.mock('../../shared/features/perCampusV2Capability', () => ({
  usePerCampusV2Capability: () => capabilityMock(),
}));

// Auth state the CTA resolves its mode from. Defaults to a settled, signed-out visitor; individual
// tests override this to cover the signed-in and still-bootstrapping cases (bug: this CTA used to
// hard-code 'public' regardless of who was signed in).
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

import { FinalCtaSection } from './FinalCtaSection';

describe('FinalCtaSection v2 cutover', () => {
  beforeEach(() => {
    navigateMock.mockClear();
    capabilityMock.mockReset();
    toastError.mockClear();
    toastLoading.mockClear();
    retryMock.mockClear();
    authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
  });

  it('opens the v2 form in a modal — and does NOT navigate away — when the capability is enabled', () => {
    capabilityMock.mockReturnValue({ status: 'ready', enabled: true, readEnabled: true, writeEnabled: true, retry: retryMock });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(screen.getByTestId('v2-modal')).toBeInTheDocument();
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('surfaces a disabled error when the capability is disabled (flags OFF)', () => {
    capabilityMock.mockReturnValue({ status: 'ready', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(navigateMock).not.toHaveBeenCalled();
    expect(screen.queryByTestId('v2-modal')).toBeNull();
    expect(toastError).toHaveBeenCalledTimes(1);
  });

  it('surfaces an error with retry (NOT a silent v1 fallback) when the capability errored', () => {
    // Behaviour change (owner-requested): a fetch failure must not downgrade users to v1. It shows
    // an error toast with a Retry, and never opens the v1 popup or routes to v2.
    capabilityMock.mockReturnValue({ status: 'error', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(screen.queryByTestId('v2-modal')).toBeNull();
    expect(toastError).toHaveBeenCalledTimes(1);
  });

  it('disables the CTA while the capability is still resolving', () => {
    capabilityMock.mockReturnValue({ status: 'loading', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });
    render(<FinalCtaSection />);

    expect(screen.getByRole('button')).toBeDisabled();
  });

  describe('entry mode follows sign-in state (homepage bug fix)', () => {
    beforeEach(() => {
      capabilityMock.mockReturnValue({ status: 'ready', enabled: true, readEnabled: true, writeEnabled: true, retry: retryMock });
    });

    it('opens the PUBLIC shell for a signed-out visitor', () => {
      authState = { user: null, isAuthenticated: false, isReady: true, effectiveRole: null };
      render(<FinalCtaSection />);

      fireEvent.click(screen.getByRole('button'));

      expect(screen.getByTestId('v2-modal')).toHaveAttribute('data-mode', 'public');
    });

    it('opens the AUTHENTICATED shell for a signed-in visitor — same as the dashboard, no OTP', () => {
      authState = { user: { userId: '42' }, isAuthenticated: true, isReady: true, effectiveRole: 'VISITOR' };
      render(<FinalCtaSection />);

      fireEvent.click(screen.getByRole('button'));

      expect(screen.getByTestId('v2-modal')).toHaveAttribute('data-mode', 'authenticated');
    });

    it.each(['STAFF', 'STAFF_LEADER'])('opens the AUTHENTICATED shell for allowed role %s', (role) => {
      authState = { user: { userId: '43' }, isAuthenticated: true, isReady: true, effectiveRole: role };
      render(<FinalCtaSection />);

      fireEvent.click(screen.getByRole('button'));

      expect(screen.getByTestId('v2-modal')).toHaveAttribute('data-mode', 'authenticated');
      expect(navigateMock).not.toHaveBeenCalled();
    });

    it.each(['ADMIN', 'HO', 'DEPARTMENT_LEAD', 'DEPARTMENT', 'STUDENT'])(
      'FORBIDDEN role %s — never the form, never public fallback, routed to /403',
      (role) => {
        authState = { user: { userId: '44' }, isAuthenticated: true, isReady: true, effectiveRole: role };
        render(<FinalCtaSection />);

        fireEvent.click(screen.getByRole('button'));

        expect(screen.queryByTestId('v2-modal')).toBeNull();
        expect(navigateMock).toHaveBeenCalledWith('/403');
      },
    );

    it('signed-in but unmappable account (effectiveRole null) → /invalid-account, not the form', () => {
      authState = { user: { userId: '45' }, isAuthenticated: true, isReady: true, effectiveRole: null };
      render(<FinalCtaSection />);

      fireEvent.click(screen.getByRole('button'));

      expect(screen.queryByTestId('v2-modal')).toBeNull();
      expect(navigateMock).toHaveBeenCalledWith('/invalid-account');
    });

    it('does NOT open the public shell while auth is still bootstrapping', () => {
      authState = { user: null, isAuthenticated: false, isReady: false, effectiveRole: null };
      render(<FinalCtaSection />);

      fireEvent.click(screen.getByRole('button'));

      expect(screen.queryByTestId('v2-modal')).toBeNull();
    });

    it('disables the CTA while auth is still bootstrapping', () => {
      authState = { user: null, isAuthenticated: false, isReady: false, effectiveRole: null };
      render(<FinalCtaSection />);

      expect(screen.getByRole('button')).toBeDisabled();
    });
  });
});
