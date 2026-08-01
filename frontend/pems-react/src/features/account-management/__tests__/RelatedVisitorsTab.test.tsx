import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

vi.mock('../api/accountManagementApi', () => ({
  accountManagementApi: {
    getRelatedVisitors: vi.fn(),
    getRelatedVisitorNationalities: vi.fn(),
    getRelatedVisitorDetails: vi.fn(),
  },
}));

import { accountManagementApi } from '../api/accountManagementApi';
import { RelatedVisitorsTab } from '../components/RelatedVisitorsTab';

const getRelatedVisitors = accountManagementApi.getRelatedVisitors as unknown as ReturnType<typeof vi.fn>;
const getNationalities = accountManagementApi.getRelatedVisitorNationalities as unknown as ReturnType<typeof vi.fn>;

function visitorPage(overrides: Record<string, unknown> = {}) {
  return {
    items: [
      {
        userId: '1', fullName: 'Yamada Taro', email: 'taro@example.com', phone: null,
        nationality: 'Nhật Bản', roleCode: 'VISITOR', status: 'ACTIVE', createdVia: null,
        createdAt: '2026-01-05T00:00:00Z', lastLoginAt: null, relatedRequestCount: 2,
        lastRelatedRequestAt: null, latestPlannedStartAt: null,
        canViewDetails: true, canManageStatus: false, canUpdateRole: false, canResetPassword: false,
      },
    ],
    page: 1, pageSize: 10, totalItems: 25, totalPages: 3,
    ...overrides,
  };
}

/** The params of the most recent related-visitors request. */
const lastListParams = () => getRelatedVisitors.mock.calls.at(-1)?.[0];

beforeEach(() => {
  getRelatedVisitors.mockReset();
  getNationalities.mockReset();
  getRelatedVisitors.mockResolvedValue(visitorPage());
  getNationalities.mockResolvedValue({ items: ['Hàn Quốc', 'Nhật Bản', 'Pháp'] });
});

describe('RelatedVisitorsTab — account type filter', () => {
  it('offers exactly two modes and no "Tất cả tài khoản"', async () => {
    render(<RelatedVisitorsTab accountTypeFilter="VISITOR" onAccountTypeChange={vi.fn()} />);

    const select = await screen.findByLabelText('Loại tài khoản');
    const options = within(select).getAllByRole('option');

    expect(options.map((o) => o.textContent)).toEqual(['Tài khoản nội bộ', 'Tài khoản khách']);
    expect(screen.queryByRole('option', { name: 'Tất cả tài khoản' })).toBeNull();
  });

  it('reports the switch back to internal accounts to its parent', async () => {
    const onAccountTypeChange = vi.fn();
    render(<RelatedVisitorsTab accountTypeFilter="VISITOR" onAccountTypeChange={onAccountTypeChange} />);

    await userEvent.selectOptions(await screen.findByLabelText('Loại tài khoản'), 'INTERNAL');

    expect(onAccountTypeChange).toHaveBeenCalledWith('INTERNAL');
  });
});

describe('RelatedVisitorsTab — nationality options', () => {
  it('reads the options from the nationality endpoint, not from a page of the visitor list', async () => {
    render(<RelatedVisitorsTab />);

    await waitFor(() => expect(getNationalities).toHaveBeenCalledTimes(1));

    // The old workaround requested 100 rows purely to scrape distinct nationalities off them,
    // which hid every nationality beyond that page. No list call may serve that purpose again.
    for (const [params] of getRelatedVisitors.mock.calls) {
      expect(params.pageSize).toBe(10);
    }

    await userEvent.click(screen.getByLabelText('Quốc tịch'));
    for (const name of ['Hàn Quốc', 'Nhật Bản', 'Pháp']) {
      expect(screen.getByRole('button', { name })).toBeInTheDocument();
    }
  });

  it('sends the chosen nationality and returns to page 1', async () => {
    render(<RelatedVisitorsTab />);
    await waitFor(() => expect(getRelatedVisitors).toHaveBeenCalled());

    await userEvent.click(await screen.findByRole('button', { name: '2' }));
    await waitFor(() => expect(lastListParams().page).toBe(2));

    await userEvent.click(screen.getByLabelText('Quốc tịch'));
    await userEvent.click(await screen.findByRole('button', { name: 'Nhật Bản' }));

    await waitFor(() => expect(lastListParams().nationality).toBe('Nhật Bản'));
    expect(lastListParams().page).toBe(1);
  });

  it('sends no nationality param for "Tất cả quốc tịch" — never the label itself', async () => {
    render(<RelatedVisitorsTab />);

    await userEvent.click(await screen.findByLabelText('Quốc tịch'));
    await userEvent.click(await screen.findByRole('button', { name: 'Nhật Bản' }));
    await waitFor(() => expect(lastListParams().nationality).toBe('Nhật Bản'));

    await userEvent.click(screen.getByLabelText('Quốc tịch'));
    await userEvent.click(await screen.findByRole('button', { name: 'Tất cả quốc tịch' }));

    await waitFor(() => expect(lastListParams().nationality).toBeUndefined());
  });

  it('keeps the visitor table working when the nationality request fails, and offers a retry', async () => {
    getNationalities.mockRejectedValueOnce(new Error('boom'));
    render(<RelatedVisitorsTab />);

    // The table is unaffected by the dropdown's failure.
    expect(await screen.findByText('Yamada Taro')).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText('Quốc tịch'));
    const retry = await screen.findByRole('button', { name: 'Thử lại' });

    getNationalities.mockResolvedValueOnce({ items: ['Singapore'] });
    await userEvent.click(retry);

    expect(await screen.findByRole('button', { name: 'Singapore' })).toBeInTheDocument();
  });

  it('shows an empty-state instead of inventing a country list', async () => {
    getNationalities.mockResolvedValue({ items: [] });
    render(<RelatedVisitorsTab />);

    await userEvent.click(await screen.findByLabelText('Quốc tịch'));

    expect(await screen.findByText('Chưa có dữ liệu quốc tịch.')).toBeInTheDocument();
    // "Tất cả quốc tịch" is the cleared state, not a country — it stays.
    expect(screen.getByRole('button', { name: 'Tất cả quốc tịch' })).toBeInTheDocument();
  });
});

describe('RelatedVisitorsTab — read-only + own pagination', () => {
  it('pages off the visitor API totals and exposes no management action', async () => {
    render(<RelatedVisitorsTab />);

    // 25 items / pageSize 10 → the Visitor response's own totalPages, not the internal list's.
    await waitFor(() => expect(screen.getByRole('button', { name: '3' })).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: '4' })).toBeNull();

    expect(screen.queryByText('Tạo tài khoản mới')).toBeNull();
    expect(screen.queryByTitle('Khóa tài khoản')).toBeNull();
    expect(screen.queryByTitle('Chỉnh sửa vai trò')).toBeNull();
    expect(screen.getByTitle('Xem tài khoản')).toBeInTheDocument();
  });
});
