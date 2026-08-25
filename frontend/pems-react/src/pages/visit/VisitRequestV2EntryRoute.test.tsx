import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';

/**
 * Direct-route authorization (CanhIter3FixBug follow-up): `/visit-registration/v2` used to be
 * hard-coded `mode="public"` regardless of who was signed in — a Visitor/Staff/Staff Leader typing
 * the URL got the anonymous OTP form instead of the authenticated one the dashboard/homepage CTA
 * already open for them, and an Admin/HO/Department/Student account got no gate at all. This route
 * resolver is the single place that now decides all three branches, mirroring the backend's own
 * actor guard (CreateVisitRequestV2CommandHandler: Visitor/Staff/Staff Leader only).
 */

vi.mock('react-router-dom', () => ({
  Navigate: ({ to }: { to: string }) => <div data-testid="navigate" data-to={to} />,
}));

vi.mock('./VisitRequestV2Page', () => ({
  default: ({ mode }: { mode: string }) => <div data-testid="visit-form" data-mode={mode} />,
}));

type AuthState = {
  isAuthenticated: boolean;
  isLoading: boolean;
  isReady: boolean;
  effectiveRole: string | null;
};
let authState: AuthState = { isAuthenticated: false, isLoading: false, isReady: true, effectiveRole: null };
vi.mock('../../shared/auth/AuthContext', () => ({
  useAuthContext: () => authState,
}));

import VisitRequestV2EntryRoute from './VisitRequestV2EntryRoute';

describe('VisitRequestV2EntryRoute — /visit-registration/v2 auth-aware entry', () => {
  beforeEach(() => {
    authState = { isAuthenticated: false, isLoading: false, isReady: true, effectiveRole: null };
  });

  it('anonymous → the public OTP form, unchanged', () => {
    authState = { isAuthenticated: false, isLoading: false, isReady: true, effectiveRole: null };
    render(<VisitRequestV2EntryRoute />);

    expect(screen.getByTestId('visit-form')).toHaveAttribute('data-mode', 'public');
  });

  it.each(['VISITOR', 'STAFF', 'STAFF_LEADER'])(
    'authenticated allowed role %s → the authenticated self-registration form',
    (role) => {
      authState = { isAuthenticated: true, isLoading: false, isReady: true, effectiveRole: role };
      render(<VisitRequestV2EntryRoute />);

      expect(screen.getByTestId('visit-form')).toHaveAttribute('data-mode', 'authenticated');
    },
  );

  it.each(['ADMIN', 'HO', 'DEPARTMENT_LEAD', 'DEPARTMENT', 'STUDENT'])(
    'authenticated FORBIDDEN role %s → routed to /403, never the form, never a public fallback',
    (role) => {
      authState = { isAuthenticated: true, isLoading: false, isReady: true, effectiveRole: role };
      render(<VisitRequestV2EntryRoute />);

      expect(screen.queryByTestId('visit-form')).toBeNull();
      expect(screen.getByTestId('navigate')).toHaveAttribute('data-to', '/403');
    },
  );

  it('authenticated but unmappable account (effectiveRole null) → /invalid-account', () => {
    authState = { isAuthenticated: true, isLoading: false, isReady: true, effectiveRole: null };
    render(<VisitRequestV2EntryRoute />);

    expect(screen.queryByTestId('visit-form')).toBeNull();
    expect(screen.getByTestId('navigate')).toHaveAttribute('data-to', '/invalid-account');
  });

  it('auth still bootstrapping (isReady false) → loading shell, no form, no navigate — never flashes public', () => {
    authState = { isAuthenticated: false, isLoading: true, isReady: false, effectiveRole: null };
    render(<VisitRequestV2EntryRoute />);

    expect(screen.queryByTestId('visit-form')).toBeNull();
    expect(screen.queryByTestId('navigate')).toBeNull();
    expect(screen.getByText('Đang tải...')).toBeInTheDocument();
  });

  it('auth still bootstrapping (isLoading true, isReady already true) → still waits', () => {
    authState = { isAuthenticated: false, isLoading: true, isReady: true, effectiveRole: null };
    render(<VisitRequestV2EntryRoute />);

    expect(screen.queryByTestId('visit-form')).toBeNull();
    expect(screen.getByText('Đang tải...')).toBeInTheDocument();
  });
});
