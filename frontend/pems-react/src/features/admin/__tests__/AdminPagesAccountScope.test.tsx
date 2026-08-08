import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

vi.mock('../api/adminApi', () => ({
  adminApi: {
    getSessions: vi.fn(),
    getSecurityEvents: vi.fn(),
    revokeSession: vi.fn(),
    revokeUserSessions: vi.fn(),
  },
}));

import { adminApi } from '../api/adminApi';
import { SessionManagement } from '../../../pages/dashboard/admin/SessionManagement';
import { SecurityMonitoring } from '../../../pages/dashboard/admin/SecurityMonitoring';

const api = adminApi as unknown as Record<string, ReturnType<typeof vi.fn>>;

const EMPTY_PAGE = { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 };

function renderAt(path: string, ui: React.ReactElement) {
  return render(<MemoryRouter initialEntries={[path]}>{ui}</MemoryRouter>);
}

beforeEach(() => {
  Object.values(api).forEach((fn) => fn.mockReset());
  api.getSessions.mockResolvedValue(EMPTY_PAGE);
  api.getSecurityEvents.mockResolvedValue(EMPTY_PAGE);
});

/**
 * The account detail drawer opens these two pages with `?keyword=<email>` so ADMIN lands on one
 * account's history instead of the whole system's. What matters is that the scope is BOTH applied
 * (the request carries it) and VISIBLE (the search box shows it) — a filter the operator cannot see
 * is a filter they cannot undo.
 */
describe('Admin pages open scoped to one account when given ?keyword=', () => {
  it('Phiên đăng nhập: seeds the search box and asks the API for that account only', async () => {
    renderAt('/dashboard/admin/sessions?keyword=duy%40fpt.edu.vn', <SessionManagement />);

    expect(await screen.findByLabelText('Tìm theo email hoặc họ tên')).toHaveValue('duy@fpt.edu.vn');
    await waitFor(() => expect(api.getSessions).toHaveBeenCalledWith(
      expect.objectContaining({ keyword: 'duy@fpt.edu.vn' }),
    ));
  });

  it('Phiên đăng nhập: the scope can be cleared, because it lives in the visible box', async () => {
    renderAt('/dashboard/admin/sessions?keyword=duy%40fpt.edu.vn', <SessionManagement />);

    await userEvent.clear(await screen.findByLabelText('Tìm theo email hoặc họ tên'));

    await waitFor(() => expect(api.getSessions).toHaveBeenLastCalledWith(
      expect.not.objectContaining({ keyword: expect.anything() }),
    ));
  });

  it('Bảo mật: seeds the search box and asks the API for that account only', async () => {
    renderAt('/dashboard/admin/security?keyword=duy%40fpt.edu.vn', <SecurityMonitoring />);

    expect(await screen.findByLabelText('Tìm theo email / họ tên')).toHaveValue('duy@fpt.edu.vn');
    await waitFor(() => expect(api.getSecurityEvents).toHaveBeenCalledWith(
      expect.objectContaining({ keyword: 'duy@fpt.edu.vn' }),
    ));
  });

  it('opens unscoped when no keyword is given', async () => {
    renderAt('/dashboard/admin/sessions', <SessionManagement />);

    expect(await screen.findByLabelText('Tìm theo email hoặc họ tên')).toHaveValue('');
    await waitFor(() => expect(api.getSessions).toHaveBeenCalledWith(
      expect.not.objectContaining({ keyword: expect.anything() }),
    ));
  });
});
