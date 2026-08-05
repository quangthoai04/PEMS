import { describe, it, expect } from 'vitest';
import {
  canAccessDashboardRoute,
  getDefaultDashboardRoute,
  getVisibleSidebarItems,
  getDashboardRoutePolicy,
  DASHBOARD_ROUTE_POLICIES,
  type DashboardRouteKey,
} from '../dashboardRouteAccess';
import { ALL_EFFECTIVE_ROLES, type EffectiveRole } from '../resolveEffectiveRole';

describe('dashboardRouteAccess — fail-closed', () => {
  it('denies when the role is null or undefined', () => {
    expect(canAccessDashboardRoute(null, 'CAMPUS_LIST')).toBe(false);
    expect(canAccessDashboardRoute(undefined, 'DASHBOARD_HOME')).toBe(false);
  });

  it('denies an unknown route key instead of defaulting to allow', () => {
    // Route keys can arrive from runtime data that TypeScript never checked.
    const bogus = 'NOT_A_ROUTE' as DashboardRouteKey;
    for (const role of ALL_EFFECTIVE_ROLES) {
      expect(canAccessDashboardRoute(role, bogus)).toBe(false);
    }
  });

  it('declares a non-empty allowedRoles for every policy', () => {
    for (const policy of DASHBOARD_ROUTE_POLICIES) {
      expect(policy.allowedRoles.length).toBeGreaterThan(0);
    }
  });

  it('has no duplicate route keys', () => {
    const keys = DASHBOARD_ROUTE_POLICIES.map((p) => p.key);
    expect(new Set(keys).size).toBe(keys.length);
  });
});

describe('dashboardRouteAccess — Campus is HO-only', () => {
  // The original bug: an ADMIN could open /dashboard/campus by typing the URL.
  it('allows only HO on the campus routes', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      const expected = role === 'HO';
      expect(canAccessDashboardRoute(role, 'CAMPUS_LIST')).toBe(expected);
      expect(canAccessDashboardRoute(role, 'CAMPUS_DETAIL')).toBe(expected);
    }
  });

  it('refuses ADMIN explicitly', () => {
    expect(canAccessDashboardRoute('ADMIN', 'CAMPUS_LIST')).toBe(false);
    expect(canAccessDashboardRoute('ADMIN', 'CAMPUS_DETAIL')).toBe(false);
  });
});

describe('dashboardRouteAccess — ADMIN is not a business superuser', () => {
  const businessRoutes: DashboardRouteKey[] = [
    'CAMPUS_LIST', 'FAQ_LIST', 'GALLERY', 'GALLERY_LOCATIONS', 'VISIT_LIST',
    'VISIT_DETAIL', 'VISIT_PROCESS', 'VISIT_PHOTOS', 'DOCUMENTS', 'MINUTES',
    'FEEDBACK', 'REPORTS', 'PARTNER_LIST', 'NEWS_LIST', 'EMAIL_LIST',
    'DEPARTMENT_LIST', 'MY_DEPARTMENT', 'POST_VISIT_TASKS',
  ];

  it.each(businessRoutes)('denies ADMIN on %s', (routeKey) => {
    expect(canAccessDashboardRoute('ADMIN', routeKey)).toBe(false);
  });

  it('still allows ADMIN its own console', () => {
    for (const key of ['ADMIN_SESSIONS', 'ADMIN_SECURITY', 'ADMIN_AUDIT_LOGS', 'API_MANAGEMENT'] as const) {
      expect(canAccessDashboardRoute('ADMIN', key)).toBe(true);
    }
    // Account management is a system-administration duty, so ADMIN keeps it.
    expect(canAccessDashboardRoute('ADMIN', 'ACCOUNT_LIST')).toBe(true);
  });
});

describe('dashboardRouteAccess — Leader vs Staff are distinguished', () => {
  it('gives Gallery to Staff Leader but not plain Staff', () => {
    expect(canAccessDashboardRoute('STAFF_LEADER', 'GALLERY')).toBe(true);
    expect(canAccessDashboardRoute('STAFF', 'GALLERY')).toBe(false);
    expect(canAccessDashboardRoute('STAFF_LEADER', 'GALLERY_LOCATIONS')).toBe(true);
    expect(canAccessDashboardRoute('STAFF', 'GALLERY_LOCATIONS')).toBe(false);
  });

  it('gives Account management to Staff Leader but not plain Staff', () => {
    expect(canAccessDashboardRoute('STAFF_LEADER', 'ACCOUNT_LIST')).toBe(true);
    expect(canAccessDashboardRoute('STAFF', 'ACCOUNT_LIST')).toBe(false);
  });

  it('gives My Department to Department Lead but not Department staff', () => {
    expect(canAccessDashboardRoute('DEPARTMENT_LEAD', 'MY_DEPARTMENT')).toBe(true);
    expect(canAccessDashboardRoute('DEPARTMENT', 'MY_DEPARTMENT')).toBe(false);
  });

  it('gives department master data to Staff Leader only', () => {
    expect(canAccessDashboardRoute('STAFF_LEADER', 'DEPARTMENT_LIST')).toBe(true);
    expect(canAccessDashboardRoute('DEPARTMENT_LEAD', 'DEPARTMENT_LIST')).toBe(false);
    expect(canAccessDashboardRoute('DEPARTMENT', 'DEPARTMENT_LIST')).toBe(false);
  });
});

describe('dashboardRouteAccess — module gates from the permission matrix', () => {
  it('FAQ is HO-only', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      expect(canAccessDashboardRoute(role, 'FAQ_LIST')).toBe(role === 'HO');
    }
  });

  it('the admin console is ADMIN-only', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      expect(canAccessDashboardRoute(role, 'ADMIN_SECURITY')).toBe(role === 'ADMIN');
      expect(canAccessDashboardRoute(role, 'API_MANAGEMENT')).toBe(role === 'ADMIN');
    }
  });

  it('reports exclude Staff, Student and Visitor', () => {
    expect(canAccessDashboardRoute('HO', 'REPORTS')).toBe(true);
    expect(canAccessDashboardRoute('STAFF_LEADER', 'REPORTS')).toBe(true);
    expect(canAccessDashboardRoute('DEPARTMENT_LEAD', 'REPORTS')).toBe(true);
    expect(canAccessDashboardRoute('DEPARTMENT', 'REPORTS')).toBe(true);
    expect(canAccessDashboardRoute('STAFF', 'REPORTS')).toBe(false);
    expect(canAccessDashboardRoute('STUDENT', 'REPORTS')).toBe(false);
    expect(canAccessDashboardRoute('VISITOR', 'REPORTS')).toBe(false);
  });

  it('closes the visit workspace to ADMIN and to Department staff', () => {
    // ADMIN: no business access at all. Department staff: the list screen is not theirs —
    // they reach visits through their invitations/tasks, which is what the old app did too.
    for (const role of ALL_EFFECTIVE_ROLES) {
      const expected = role !== 'ADMIN' && role !== 'DEPARTMENT';
      expect(canAccessDashboardRoute(role, 'VISIT_LIST')).toBe(expected);
    }
  });

  it('still lets Department staff reach the visit work assigned to them', () => {
    for (const key of ['VISIT_INVITATION', 'VISIT_DETAIL', 'VISIT_PROCESS', 'POST_VISIT_TASKS'] as const) {
      expect(canAccessDashboardRoute('DEPARTMENT', key)).toBe(true);
    }
  });

  it('keeps Department staff out of the email workspace', () => {
    expect(canAccessDashboardRoute('DEPARTMENT', 'EMAIL_LIST')).toBe(false);
    expect(canAccessDashboardRoute('DEPARTMENT_LEAD', 'EMAIL_LIST')).toBe(true);
  });

  it('keeps HO on documents, minutes and feedback', () => {
    // Matrix §5.7/§5.9/§5.13 say otherwise; HO uses these screens in practice.
    for (const key of ['DOCUMENTS', 'MINUTES', 'FEEDBACK'] as const) {
      expect(canAccessDashboardRoute('HO', key)).toBe(true);
    }
  });

  it('profile is open to every valid role', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      expect(canAccessDashboardRoute(role, 'PROFILE')).toBe(true);
    }
  });
});

describe('getDefaultDashboardRoute', () => {
  it('sends Student and Visitor to the visit workspace, not /dashboard', () => {
    expect(getDefaultDashboardRoute('STUDENT')).toBe('/dashboard/visit');
    expect(getDefaultDashboardRoute('VISITOR')).toBe('/dashboard/visit');
  });

  it('sends the other roles to /dashboard', () => {
    for (const role of ['ADMIN', 'HO', 'STAFF_LEADER', 'STAFF', 'DEPARTMENT_LEAD', 'DEPARTMENT'] as const) {
      expect(getDefaultDashboardRoute(role)).toBe('/dashboard');
    }
  });

  it('sends an unresolvable account to /invalid-account', () => {
    expect(getDefaultDashboardRoute(null)).toBe('/invalid-account');
  });

  it('never returns a route the role cannot enter — otherwise 403 loops', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      const destination = getDefaultDashboardRoute(role);
      const policy = DASHBOARD_ROUTE_POLICIES.find((p) => p.path === destination);
      expect(policy, `no policy for default route ${destination} of ${role}`).toBeDefined();
      expect(canAccessDashboardRoute(role, policy!.key)).toBe(true);
    }
  });
});

describe('getVisibleSidebarItems', () => {
  it('returns nothing for an unresolvable account', () => {
    expect(getVisibleSidebarItems(null)).toHaveLength(0);
  });

  it('shows ADMIN only System Administration entries', () => {
    const keys = getVisibleSidebarItems('ADMIN').map((i) => i.key);
    expect(keys).toEqual([
      'DASHBOARD_HOME',
      'ACCOUNT_LIST',
      'ADMIN_SESSIONS',
      'ADMIN_SECURITY',
      'API_MANAGEMENT',
      'ADMIN_AUDIT_LOGS',
    ]);
  });

  it('never shows a campus entry to anyone but HO', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      const hasCampus = getVisibleSidebarItems(role).some((i) => i.key === 'CAMPUS_LIST');
      expect(hasCampus).toBe(role === 'HO');
    }
  });

  it('gives every visible item a label and a path', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      for (const item of getVisibleSidebarItems(role)) {
        expect(item.sidebarLabel, `${item.key} has no label`).toBeTruthy();
        expect(item.path.startsWith('/dashboard')).toBe(true);
        // A menu entry must be a concrete URL, never a pattern with a parameter.
        expect(item.path).not.toContain(':');
      }
    }
  });
});

describe('sidebar / route-guard parity', () => {
  // The property the whole refactor exists to guarantee:
  // a visible menu item is always enterable, and a hidden one is always a 403.
  //
  // The reverse (enterable => visible) holds except where a policy opts out via
  // hideInSidebarForRoles. That is presentation only — DASHBOARD_HOME is reachable by
  // Student/Visitor but shows no menu entry, because the app redirects them onward and
  // the button would just jump to the entry below it. It never widens access.
  it('menu visible  <=>  canAccessDashboardRoute is true', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      const visible = new Set(getVisibleSidebarItems(role).map((i) => i.key));
      for (const policy of DASHBOARD_ROUTE_POLICIES) {
        const allowed = canAccessDashboardRoute(role, policy.key);
        if (visible.has(policy.key)) {
          expect(allowed, `${role} sees ${policy.key} in the menu but is denied the route`).toBe(true);
        }
        const hidden = policy.hideInSidebarForRoles?.includes(role) ?? false;
        if (policy.showInSidebar && allowed && !hidden) {
          expect(
            visible.has(policy.key),
            `${role} may enter ${policy.key} but it is missing from the menu`,
          ).toBe(true);
        }
      }
    }
  });
});

describe('role × route matrix is fully specified', () => {
  it('produces a decision for every role and every route', () => {
    const matrix: Record<string, Record<string, boolean>> = {};
    for (const role of ALL_EFFECTIVE_ROLES) {
      matrix[role] = {};
      for (const policy of DASHBOARD_ROUTE_POLICIES) {
        const decision = canAccessDashboardRoute(role as EffectiveRole, policy.key);
        expect(typeof decision).toBe('boolean');
        matrix[role][policy.key] = decision;
      }
    }
    expect(Object.keys(matrix)).toHaveLength(8);
    expect(Object.keys(matrix.ADMIN)).toHaveLength(DASHBOARD_ROUTE_POLICIES.length);
  });

  it('exposes a policy for each declared key', () => {
    for (const policy of DASHBOARD_ROUTE_POLICIES) {
      expect(getDashboardRoutePolicy(policy.key)).toBe(policy);
    }
  });
});
