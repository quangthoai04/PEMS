import React from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

const mockAuth = vi.fn();
vi.mock('../../../shared/hooks/useAuth', () => ({ useAuth: () => mockAuth() }));
vi.mock('../../../shared/hooks/useAuthenticatedImage', () => ({
  useAuthenticatedImage: () => null,
}));

import { Sidebar } from '../Sidebar';
import {
  canAccessDashboardRoute,
  getVisibleSidebarItems,
  DASHBOARD_ROUTE_POLICIES,
} from '../../../shared/auth/dashboardRouteAccess';
import { ALL_EFFECTIVE_ROLES, type EffectiveRole } from '../../../shared/auth/resolveEffectiveRole';

function renderSidebarAs(effectiveRole: EffectiveRole | null) {
  mockAuth.mockReturnValue({
    logout: vi.fn(),
    user: {
      userId: 'u-1',
      fullName: 'Nguyen Van A',
      email: 'a@fpt.edu.vn',
      roleCode: 'HO',
      subRole: null,
      campusName: 'HCM',
      avatarUrl: null,
      mustChangePassword: false,
      mustSetPassword: false,
      effectiveRole: effectiveRole ?? '',
      status: 'ACTIVE',
    },
    effectiveRole,
  });

  return render(
    <MemoryRouter>
      <Sidebar />
    </MemoryRouter>,
  );
}

/** Hrefs of the nav links the Sidebar actually rendered. */
function renderedNavPaths(): string[] {
  return Array.from(document.querySelectorAll('nav a'))
    .map((a) => a.getAttribute('href') ?? '')
    .filter(Boolean);
}

beforeEach(() => {
  mockAuth.mockReset();
  localStorage.clear();
});

describe('Sidebar renders exactly what the route policy allows', () => {
  it.each(ALL_EFFECTIVE_ROLES)('%s: rendered menu == getVisibleSidebarItems', (role) => {
    renderSidebarAs(role);
    const expected = getVisibleSidebarItems(role).map((i) => i.path);
    expect(renderedNavPaths()).toEqual(expected);
  });

  it.each(ALL_EFFECTIVE_ROLES)('%s: every rendered link is a route it may enter', (role) => {
    renderSidebarAs(role);
    for (const href of renderedNavPaths()) {
      const policy = DASHBOARD_ROUTE_POLICIES.find((p) => p.path === href);
      expect(policy, `menu link ${href} has no route policy`).toBeDefined();
      expect(
        canAccessDashboardRoute(role, policy!.key),
        `${role} sees ${href} but the guard would refuse it`,
      ).toBe(true);
    }
  });

  it('renders no menu at all for an account with no resolvable role', () => {
    renderSidebarAs(null);
    expect(renderedNavPaths()).toHaveLength(0);
  });
});

describe('Sidebar hides what the policy denies', () => {
  it('shows the campus entry to HO only', () => {
    for (const role of ALL_EFFECTIVE_ROLES) {
      const { unmount } = renderSidebarAs(role);
      const hasCampus = renderedNavPaths().includes('/dashboard/campus');
      expect(hasCampus, `${role} campus menu`).toBe(role === 'HO');
      unmount();
    }
  });

  it('shows ADMIN no business menu — only the system console', () => {
    renderSidebarAs('ADMIN');
    const paths = renderedNavPaths();
    for (const businessPath of [
      '/dashboard/campus',
      '/dashboard/visit',
      '/dashboard/gallery',
      '/dashboard/news',
      '/dashboard/email',
      '/dashboard/partners',
      '/dashboard/faq',
      '/dashboard/reports',
      '/dashboard/documents',
    ]) {
      expect(paths).not.toContain(businessPath);
    }
    expect(paths).toContain('/dashboard/admin/security');
    expect(paths).toContain('/dashboard/apis');
  });

  it('shows Gallery to Staff Leader but not to plain Staff', () => {
    renderSidebarAs('STAFF_LEADER');
    expect(renderedNavPaths()).toContain('/dashboard/gallery');
    screen.getByText('Quản lý Gallery');
  });

  it('does not show Gallery to plain Staff', () => {
    renderSidebarAs('STAFF');
    expect(renderedNavPaths()).not.toContain('/dashboard/gallery');
  });

  it('routes the Department Lead to /dashboard/my-department, with no id in the URL', () => {
    renderSidebarAs('DEPARTMENT_LEAD');
    const paths = renderedNavPaths();
    expect(paths).toContain('/dashboard/my-department');
    // The old menu built /dashboard/departments/{id} from a stored id with a '1' fallback.
    expect(paths.some((p) => p.startsWith('/dashboard/departments/'))).toBe(false);
  });
});

describe('Sidebar does not read authorization from localStorage', () => {
  it('ignores a forged currentUser when building the menu', () => {
    localStorage.setItem('currentUser', JSON.stringify({ role: 'HO', subRole: null }));
    renderSidebarAs('STUDENT');

    const paths = renderedNavPaths();
    expect(paths).not.toContain('/dashboard/campus');
    expect(paths).not.toContain('/dashboard/faq');
    expect(paths).toEqual(getVisibleSidebarItems('STUDENT').map((i) => i.path));
  });
});
