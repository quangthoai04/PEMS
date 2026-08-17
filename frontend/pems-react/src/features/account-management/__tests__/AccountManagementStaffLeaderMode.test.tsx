import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

vi.mock('../api/accountManagementApi', () => ({
  accountManagementApi: {
    getAccounts: vi.fn(),
    getStatistics: vi.fn(),
    getCampusDepartments: vi.fn(),
    getActiveCampuses: vi.fn(),
    getRelatedVisitors: vi.fn(),
    getRelatedVisitorNationalities: vi.fn(),
    getRelatedVisitorDetails: vi.fn(),
    getRoleAssignmentOptions: vi.fn(),
    getStaffLeaderAvailability: vi.fn(),
    getHoCampusCheck: vi.fn(),
    resendEmailConfirmation: vi.fn(),
  },
}));

import { accountManagementApi } from '../api/accountManagementApi';
import { AccountManagement } from '../../../pages/dashboard/accounts/AccountManagement';

const api = accountManagementApi as unknown as Record<string, ReturnType<typeof vi.fn>>;

// Neither mode shows a subtitle any more (the internal one was dropped as unnecessary chrome, and
// Visitor mode never had its own — commit bc1e7340 replaced that whole surface with
// <RelatedVisitorsTab>, which brings its own filter bar). So presence is checked via markers that
// are unique to each surface instead of copy that can be edited or removed independently:
// the internal stat tiles only render in internal mode, and the nationality filter only in Visitor.
const INTERNAL_ONLY_MARKER = 'Tổng số tài khoản';
const VISITOR_ONLY_FILTER = 'Quốc tịch';

function signInAsStaffLeader() {
  localStorage.setItem('currentUser', JSON.stringify({
    role: 'STAFF', subRole: 'LEADER', campus: 'Quy Nhơn',
  }));
}

function renderPage() {
  return render(<MemoryRouter><AccountManagement /></MemoryRouter>);
}

beforeEach(() => {
  Object.values(api).forEach((fn) => fn.mockReset());
  api.getAccounts.mockResolvedValue({ items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 });
  api.getStatistics.mockResolvedValue({ totalAccounts: 4, activeAccounts: 3, inactiveAccounts: 1, lockedAccounts: 0 });
  api.getCampusDepartments.mockResolvedValue([]);
  api.getActiveCampuses.mockResolvedValue([]);
  api.getRelatedVisitors.mockResolvedValue({ items: [], page: 1, pageSize: 10, totalItems: 0, totalPages: 0 });
  api.getRelatedVisitorNationalities.mockResolvedValue({ items: ['Nhật Bản'] });
  signInAsStaffLeader();
});

/** The account-type <select> currently on screen (the page's own, or the Visitor tab's). */
const accountTypeSelect = () => screen.getByLabelText('Loại tài khoản');

describe('AccountManagement — Staff Leader account type filter', () => {
  it('opens on "Tài khoản nội bộ" and offers only the two real modes', async () => {
    renderPage();

    const select = await screen.findByLabelText('Loại tài khoản');
    expect(select).toHaveValue('INTERNAL');
    expect(within(select).getAllByRole('option').map((o) => o.textContent))
      .toEqual(['Tài khoản nội bộ', 'Tài khoản khách']);
    expect(screen.queryByRole('option', { name: 'Tất cả tài khoản' })).toBeNull();
  });

  it('opens on the internal view and swaps the whole surface when the mode changes', async () => {
    renderPage();

    expect(await screen.findByText(INTERNAL_ONLY_MARKER)).toBeInTheDocument();
    expect(screen.queryByLabelText(VISITOR_ONLY_FILTER)).toBeNull();

    await userEvent.selectOptions(accountTypeSelect(), 'VISITOR');

    // The internal surface is gone, not merely re-captioned, and the Visitor tab is mounted.
    expect(await screen.findByLabelText(VISITOR_ONLY_FILTER)).toBeInTheDocument();
    expect(screen.queryByText(INTERNAL_ONLY_MARKER)).toBeNull();

    await userEvent.selectOptions(accountTypeSelect(), 'INTERNAL');
    expect(await screen.findByText(INTERNAL_ONLY_MARKER)).toBeInTheDocument();
    expect(screen.queryByLabelText(VISITOR_ONLY_FILTER)).toBeNull();
  });
});

describe('AccountManagement — Staff Leader API gating', () => {
  it('calls only the internal account list in internal mode', async () => {
    renderPage();

    await waitFor(() => expect(api.getAccounts).toHaveBeenCalled());
    expect(api.getRelatedVisitors).not.toHaveBeenCalled();
    expect(api.getRelatedVisitorNationalities).not.toHaveBeenCalled();
  });

  it('stops the internal list and calls only the Visitor endpoints in Visitor mode', async () => {
    renderPage();
    await waitFor(() => expect(api.getAccounts).toHaveBeenCalled());
    const internalCallsBefore = api.getAccounts.mock.calls.length;

    await userEvent.selectOptions(accountTypeSelect(), 'VISITOR');

    await waitFor(() => expect(api.getRelatedVisitors).toHaveBeenCalled());
    await waitFor(() => expect(api.getRelatedVisitorNationalities).toHaveBeenCalled());
    // The internal list must not keep running underneath the Visitor tab.
    expect(api.getAccounts.mock.calls.length).toBe(internalCallsBefore);
  });
});

describe('AccountManagement — Staff Leader Visitor mode is read-only', () => {
  it('hides the create button and the internal statistics cards', async () => {
    renderPage();
    expect(await screen.findByText('Tạo tài khoản mới')).toBeInTheDocument();
    expect(screen.getByText('Tổng số tài khoản')).toBeInTheDocument();

    await userEvent.selectOptions(accountTypeSelect(), 'VISITOR');

    await waitFor(() => expect(screen.queryByText('Tạo tài khoản mới')).toBeNull());
    // The counters are internal-account totals — over a Visitor list they would read as Visitor
    // figures, so they go away rather than being relabelled.
    expect(screen.queryByText('Tổng số tài khoản')).toBeNull();
  });

  it('drops the role filter in Visitor mode and brings it back on return', async () => {
    renderPage();
    expect(await screen.findByRole('option', { name: 'Tất cả Vai trò' })).toBeInTheDocument();

    await userEvent.selectOptions(accountTypeSelect(), 'VISITOR');
    await waitFor(() => expect(screen.queryByRole('option', { name: 'Tất cả Vai trò' })).toBeNull());

    await userEvent.selectOptions(accountTypeSelect(), 'INTERNAL');
    expect(await screen.findByRole('option', { name: 'Tất cả Vai trò' })).toBeInTheDocument();
  });
});
