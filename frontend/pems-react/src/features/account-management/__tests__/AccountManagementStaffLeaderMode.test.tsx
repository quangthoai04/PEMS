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

const INTERNAL_SUBTITLE =
  'Quản lý tài khoản của nhân sự phòng IC, trưởng phòng của các phòng ban khác và sinh viên trong cơ sở';
const VISITOR_SUBTITLE = 'Tất cả tài khoản của khách đã từng đến thăm cơ sở';

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

  it('shows the internal subtitle by default and swaps it when the mode changes', async () => {
    renderPage();

    expect(await screen.findByText(INTERNAL_SUBTITLE)).toBeInTheDocument();
    expect(screen.queryByText(VISITOR_SUBTITLE)).toBeNull();

    await userEvent.selectOptions(accountTypeSelect(), 'VISITOR');

    expect(await screen.findByText(VISITOR_SUBTITLE)).toBeInTheDocument();
    expect(screen.queryByText(INTERNAL_SUBTITLE)).toBeNull();

    await userEvent.selectOptions(accountTypeSelect(), 'INTERNAL');
    expect(await screen.findByText(INTERNAL_SUBTITLE)).toBeInTheDocument();
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
