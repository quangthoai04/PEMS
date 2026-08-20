/**
 * Patch 6 — /api/campuses/filter-options is HO-only server-side (RoleAccessPolicy.
 * CanAccessCampusManagement). Before this patch, VisitRequestManagement called
 * `useCampusFilterOptions()` unconditionally, so every non-HO viewer (Visitor, Staff, Staff
 * Leader, Department, Student) fired that request on every mount and got a silently-swallowed
 * 403 — visible only in the network tab / console.
 *
 * This suite uses the REAL `useCampusFilterOptions` hook (unlike the page's other test files,
 * which mock the hook away entirely) so it can assert on the underlying API call itself: the
 * HO-only endpoint must be called for HO and ONLY for HO; every other role must use the public
 * `/campuses/active` fallback instead.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

const listMock = vi.fn();
const invitationsMock = vi.fn();
const getFilterOptionsMock = vi.fn();
const getActiveCampusesMock = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitRequestManagementList: (...args: unknown[]) => listMock(...args),
    getMyInvitations: (...args: unknown[]) => invitationsMock(...args),
    getHostCandidates: vi.fn().mockResolvedValue([]),
    visitInvitations: { getMyInvitations: (...a: unknown[]) => invitationsMock(...a) },
  },
}));

vi.mock('../../../../features/feedbacks/api/visitFeedbackApi', () => ({
  visitFeedbackApi: { getMyPending: vi.fn().mockResolvedValue({ items: [] }) },
}));

vi.mock('../../../../shared/features/perCampusV2Capability', () => ({
  usePerCampusV2Capability: () => ({ status: 'ready', enabled: true, retry: vi.fn() }),
}));

// Deliberately NOT mocked away — this is the point of the suite: exercise the real hook against
// mocked API layers so the enabled-gating logic itself is what's under test.
vi.mock('../../../../features/campus-management/api/campusManagementApi', () => ({
  campusManagementApi: { getFilterOptions: (...args: unknown[]) => getFilterOptionsMock(...args) },
}));
vi.mock('../../../../features/authentication/api/authenticationApi', () => ({
  authenticationApi: { getActiveCampuses: (...args: unknown[]) => getActiveCampusesMock(...args) },
}));

let currentUser: { userId: string; roleCode: string; subRole?: string } =
  { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' };
vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: currentUser }),
}));

import { VisitRequestManagement } from '../VisitRequestManagement';

const renderList = () => {
  listMock.mockResolvedValue({ items: [], totalItems: 0 });
  invitationsMock.mockResolvedValue([]);
  getFilterOptionsMock.mockResolvedValue({ campuses: [], cities: [], statuses: [] });
  getActiveCampusesMock.mockResolvedValue([]);
  return render(<MemoryRouter><VisitRequestManagement /></MemoryRouter>);
};

beforeEach(() => {
  vi.clearAllMocks();
});

describe('campus filter source is chosen by role, never the HO-only endpoint for a non-HO viewer', () => {
  it.each([
    ['Visitor', { userId: '10', roleCode: 'VISITOR' }],
    ['Staff (regular)', { userId: '77', roleCode: 'STAFF', subRole: 'STAFF' }],
    ['Staff Leader', { userId: '77', roleCode: 'STAFF', subRole: 'LEADER' }],
    ['Department', { userId: '40', roleCode: 'DEPARTMENT', subRole: 'STAFF' }],
    ['Student', { userId: '60', roleCode: 'STUDENT' }],
  ])('%s: zero requests to the HO-only filter-options endpoint, falls back to the public active-campuses list', async (_label, user) => {
    currentUser = user;
    renderList();

    // The campus-source effect runs independently of the list fetch (it depends only on the
    // role, never on `listMock` resolving) — wait on IT directly rather than on an unrelated
    // part of the page settling first.
    await waitFor(() => expect(getActiveCampusesMock).toHaveBeenCalled());

    expect(getFilterOptionsMock).not.toHaveBeenCalled();
    // C-12: not a retry/render loop — exactly one request for the one mount.
    expect(getActiveCampusesMock).toHaveBeenCalledTimes(1);
  });

  it('HO: the campus dropdown DOES use the HO-only filter-options endpoint', async () => {
    currentUser = { userId: '1', roleCode: 'HO' };
    renderList();

    await waitFor(() => expect(getFilterOptionsMock).toHaveBeenCalled());

    expect(getActiveCampusesMock).not.toHaveBeenCalled();
    // C-12: not a retry/render loop — exactly one request for the one mount.
    expect(getFilterOptionsMock).toHaveBeenCalledTimes(1);
  });
});
