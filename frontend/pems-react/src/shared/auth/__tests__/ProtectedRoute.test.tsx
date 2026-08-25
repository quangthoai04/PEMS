import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Routes, Route } from 'react-router-dom';

// The guard reads auth state through useAuth; stub it so each case can pose as a role
// without standing up the whole AuthProvider + API bootstrap.
const mockAuth = vi.fn();
vi.mock('../../hooks/useAuth', () => ({
  useAuth: () => mockAuth(),
}));

import { ProtectedRoute } from '../ProtectedRoute';
import { RouteAccessGuard } from '../RouteAccessGuard';
import { resolveEffectiveRole, type EffectiveRole } from '../resolveEffectiveRole';
import { VISIT_REQUEST_V2_CREATE_ROLES } from '../visitRequestV2Access';
import type { AuthUser } from '../../../features/authentication/types/authentication.types';
import type { DashboardRouteKey } from '../dashboardRouteAccess';

function makeUser(roleCode: string, subRole: string | null = null): AuthUser {
  return {
    userId: 'u-1',
    fullName: 'Test User',
    email: 'test@fpt.edu.vn',
    roleCode,
    subRole,
    mustChangePassword: false,
    mustSetPassword: false,
    effectiveRole: '',
    status: 'ACTIVE',
  };
}

/** Poses as a signed-in user whose role is derived exactly the way AuthContext derives it. */
function signedInAs(user: AuthUser | null, overrides: Record<string, unknown> = {}) {
  mockAuth.mockReturnValue({
    user,
    isAuthenticated: !!user,
    isLoading: false,
    isReady: true,
    effectiveRole: resolveEffectiveRole(user),
    hasRole: (roles: string[]) => !!user && roles.includes(user.roleCode),
    hasEffectiveRole: (roles: string[]) => {
      const r = resolveEffectiveRole(user);
      return !!r && roles.includes(r);
    },
    loginPortal: 'INTERNAL',
    ...overrides,
  });
}

/** Mounts a guarded page and reports where the router ended up. */
function renderGuarded(routeKey: DashboardRouteKey, onMount?: () => void) {
  function Page() {
    // Stands in for a real screen's data fetch: if this runs, the page mounted.
    React.useEffect(() => { onMount?.(); }, []);
    return <div>CAMPUS PAGE</div>;
  }

  return render(
    <MemoryRouter initialEntries={['/dashboard/campus']}>
      <Routes>
        <Route
          path="/dashboard/campus"
          element={<RouteAccessGuard routeKey={routeKey}><Page /></RouteAccessGuard>}
        />
        <Route path="/403" element={<div>FORBIDDEN PAGE</div>} />
        <Route path="/invalid-account" element={<div>INVALID ACCOUNT PAGE</div>} />
        <Route path="/change-password" element={<div>CHANGE PASSWORD PAGE</div>} />
        <Route path="/" element={<div>LANDING PAGE</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

/** Mounts a page guarded by an ad-hoc `effectiveRoles` list — the mechanism `/visit/create-v2`
 * (and other non-dashboard routes) use directly, without a DashboardRouteKey. */
function renderEffectiveRolesGuarded(effectiveRoles: EffectiveRole[]) {
  function Page() {
    return <div>VISIT CREATE PAGE</div>;
  }

  return render(
    <MemoryRouter initialEntries={['/visit/create-v2']}>
      <Routes>
        <Route
          path="/visit/create-v2"
          element={<ProtectedRoute effectiveRoles={effectiveRoles}><Page /></ProtectedRoute>}
        />
        <Route path="/403" element={<div>FORBIDDEN PAGE</div>} />
        <Route path="/invalid-account" element={<div>INVALID ACCOUNT PAGE</div>} />
        <Route path="/change-password" element={<div>CHANGE PASSWORD PAGE</div>} />
        <Route path="/" element={<div>LANDING PAGE</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  mockAuth.mockReset();
  localStorage.clear();
});

describe('RouteAccessGuard — the original /dashboard/campus bug', () => {
  it('lets HO into the campus screen', () => {
    signedInAs(makeUser('HO'));
    renderGuarded('CAMPUS_LIST');
    expect(screen.getByText('CAMPUS PAGE')).toBeTruthy();
  });

  it('sends ADMIN to /403 instead of rendering the campus screen', () => {
    signedInAs(makeUser('ADMIN'));
    renderGuarded('CAMPUS_LIST');
    expect(screen.getByText('FORBIDDEN PAGE')).toBeTruthy();
    expect(screen.queryByText('CAMPUS PAGE')).toBeNull();
  });

  it('does not mount the page — so it cannot fire its API call — when denied', () => {
    const onMount = vi.fn();
    signedInAs(makeUser('ADMIN'));
    renderGuarded('CAMPUS_LIST', onMount);
    expect(onMount).not.toHaveBeenCalled();
  });

  it('mounts the page when allowed', () => {
    const onMount = vi.fn();
    signedInAs(makeUser('HO'));
    renderGuarded('CAMPUS_LIST', onMount);
    expect(onMount).toHaveBeenCalledTimes(1);
  });

  it.each([
    ['ADMIN', null],
    ['STAFF', 'LEADER'],
    ['STAFF', 'STAFF'],
    ['DEPARTMENT', 'LEADER'],
    ['DEPARTMENT', 'STAFF'],
    ['STUDENT', null],
    ['VISITOR', null],
  ] as const)('denies %s/%s', (roleCode, subRole) => {
    signedInAs(makeUser(roleCode, subRole));
    renderGuarded('CAMPUS_LIST');
    expect(screen.getByText('FORBIDDEN PAGE')).toBeTruthy();
  });
});

describe('RouteAccessGuard — localStorage cannot change the verdict', () => {
  it('a HO written into localStorage does not let an ADMIN into campus', () => {
    localStorage.setItem('currentUser', JSON.stringify({ role: 'HO', subRole: null }));
    localStorage.setItem('pems_user', JSON.stringify(makeUser('HO')));
    signedInAs(makeUser('ADMIN')); // what the backend actually returned

    renderGuarded('CAMPUS_LIST');

    expect(screen.getByText('FORBIDDEN PAGE')).toBeTruthy();
    expect(screen.queryByText('CAMPUS PAGE')).toBeNull();
  });

  it('an ADMIN written into localStorage does not lock a real HO out', () => {
    localStorage.setItem('currentUser', JSON.stringify({ role: 'ADMIN', subRole: null }));
    signedInAs(makeUser('HO'));

    renderGuarded('CAMPUS_LIST');

    expect(screen.getByText('CAMPUS PAGE')).toBeTruthy();
  });

  it('a forged LEADER sub-role in localStorage does not promote a plain Staff', () => {
    localStorage.setItem('currentUser', JSON.stringify({ role: 'STAFF', subRole: 'LEADER' }));
    signedInAs(makeUser('STAFF', 'STAFF'));

    renderGuarded('GALLERY'); // Staff Leader only
    expect(screen.getByText('FORBIDDEN PAGE')).toBeTruthy();
  });
});

describe('RouteAccessGuard — account and session states', () => {
  it('waits instead of deciding while auth is still bootstrapping', () => {
    signedInAs(makeUser('HO'), { isReady: false, isLoading: true });
    renderGuarded('CAMPUS_LIST');
    // Neither the page nor a redirect — a refresh must not bounce the user.
    expect(screen.queryByText('CAMPUS PAGE')).toBeNull();
    expect(screen.queryByText('FORBIDDEN PAGE')).toBeNull();
    expect(screen.getByText('Đang tải...')).toBeTruthy();
  });

  it('sends an unauthenticated visitor to the landing page', () => {
    signedInAs(null);
    renderGuarded('CAMPUS_LIST');
    expect(screen.getByText('LANDING PAGE')).toBeTruthy();
  });

  it('sends an account with no resolvable role to /invalid-account, not /403', () => {
    // STAFF with no sub-role: a misconfigured account, not a permission problem.
    signedInAs(makeUser('STAFF', null));
    renderGuarded('CAMPUS_LIST');
    expect(screen.getByText('INVALID ACCOUNT PAGE')).toBeTruthy();
  });

  it('forces a pending password change before anything else', () => {
    const user = { ...makeUser('HO'), mustChangePassword: true };
    signedInAs(user);
    renderGuarded('CAMPUS_LIST');
    expect(screen.getByText('CHANGE PASSWORD PAGE')).toBeTruthy();
  });
});

describe('RouteAccessGuard — deep links across modules', () => {
  it.each([
    ['ADMIN', 'GALLERY', false],
    ['ADMIN', 'VISIT_LIST', false],
    ['ADMIN', 'ADMIN_SECURITY', true],
    ['HO', 'ADMIN_SECURITY', false],
    ['HO', 'CAMPUS_LIST', true],
    ['STAFF', 'CAMPUS_LIST', false],
    ['DEPARTMENT', 'GALLERY', false],
    ['STUDENT', 'ACCOUNT_LIST', false],
    ['VISITOR', 'REPORTS', false],
  ] as const)('%s deep-linking to %s -> allowed=%s', (roleCode, routeKey, allowed) => {
    const subRole =
      roleCode === 'STAFF' ? 'STAFF' : roleCode === 'DEPARTMENT' ? 'STAFF' : null;
    signedInAs(makeUser(roleCode, subRole));
    renderGuarded(routeKey as DashboardRouteKey);

    if (allowed) {
      expect(screen.getByText('CAMPUS PAGE')).toBeTruthy();
    } else {
      expect(screen.getByText('FORBIDDEN PAGE')).toBeTruthy();
      expect(screen.queryByText('CAMPUS PAGE')).toBeNull();
    }
  });
});

describe('ProtectedRoute — /visit/create-v2 root-cause fix: `effectiveRoles` gates a non-dashboard route', () => {
  // Before this fix the route was a bare <ProtectedRoute> with no role check at all — any
  // authenticated account (Admin, HO, Department, Student…) could type this URL and reach the
  // authenticated create form. `effectiveRoles={VISIT_REQUEST_V2_CREATE_ROLES}` closes that gap.
  it.each(VISIT_REQUEST_V2_CREATE_ROLES.map((r) => [r] as const))('allows %s', (role) => {
    const roleCode = role === 'STAFF_LEADER' ? 'STAFF' : role;
    const subRole = role === 'STAFF_LEADER' ? 'LEADER' : role === 'STAFF' ? 'STAFF' : null;
    signedInAs(makeUser(roleCode, subRole));
    renderEffectiveRolesGuarded([...VISIT_REQUEST_V2_CREATE_ROLES]);
    expect(screen.getByText('VISIT CREATE PAGE')).toBeTruthy();
  });

  it.each([
    ['ADMIN', null],
    ['HO', null],
    ['DEPARTMENT', 'LEADER'],
    ['DEPARTMENT', 'STAFF'],
    ['STUDENT', null],
  ] as const)('denies %s/%s — /403, not the form', (roleCode, subRole) => {
    signedInAs(makeUser(roleCode, subRole));
    renderEffectiveRolesGuarded([...VISIT_REQUEST_V2_CREATE_ROLES]);
    expect(screen.getByText('FORBIDDEN PAGE')).toBeTruthy();
    expect(screen.queryByText('VISIT CREATE PAGE')).toBeNull();
  });

  it('sends an unauthenticated visitor to the landing page, not the form (ProtectedRoute\'s own convention for this route)', () => {
    signedInAs(null);
    renderEffectiveRolesGuarded([...VISIT_REQUEST_V2_CREATE_ROLES]);
    expect(screen.getByText('LANDING PAGE')).toBeTruthy();
  });

  it('waits instead of deciding while auth is still bootstrapping', () => {
    signedInAs(makeUser('VISITOR'), { isReady: false, isLoading: true });
    renderEffectiveRolesGuarded([...VISIT_REQUEST_V2_CREATE_ROLES]);
    expect(screen.queryByText('VISIT CREATE PAGE')).toBeNull();
    expect(screen.queryByText('FORBIDDEN PAGE')).toBeNull();
    expect(screen.getByText('Đang tải...')).toBeTruthy();
  });
});
