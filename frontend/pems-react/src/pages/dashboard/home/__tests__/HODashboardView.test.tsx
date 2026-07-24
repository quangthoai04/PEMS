import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// The overview is a single all-or-nothing endpoint; mock it so we can drive the loading / error / retry
// branches without a backend.
vi.mock('../../../../shared/api/httpClient', () => ({
  default: { get: vi.fn() },
}));

import { HODashboardView } from '../HODashboardView';
import httpClient from '../../../../shared/api/httpClient';

const mockGet = vi.mocked(httpClient.get);

const serverError = () =>
  Object.assign(new Error('Request failed with status code 500'), {
    isAxiosError: true,
    code: 'ERR_BAD_RESPONSE',
    response: { status: 500, data: {}, headers: {} },
    config: {},
  });

describe('HODashboardView — a failed overview never becomes an infinite spinner', () => {
  beforeEach(() => vi.clearAllMocks());

  it('shows a finite loading indicator while the overview is in flight', () => {
    mockGet.mockReturnValue(new Promise(() => {})); // never settles
    render(<HODashboardView />);
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('lands in an error state with retry when the overview fails (not a stuck spinner)', async () => {
    mockGet.mockRejectedValue(serverError());
    render(<HODashboardView />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/system encountered an error/i);
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
    // The loading spinner must be gone — the failure is surfaced, not hidden behind an endless load.
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('re-fetches the overview when retry is clicked', async () => {
    mockGet.mockRejectedValue(serverError());
    render(<HODashboardView />);
    await screen.findByRole('alert');
    expect(mockGet).toHaveBeenCalledTimes(1);

    await userEvent.click(screen.getByRole('button', { name: /try again/i }));
    expect(mockGet).toHaveBeenCalledTimes(2);
  });
});
