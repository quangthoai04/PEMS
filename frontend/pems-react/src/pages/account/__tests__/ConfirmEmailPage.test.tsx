import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import ConfirmEmailPage from '../ConfirmEmailPage';

const post = vi.fn();
vi.mock('../../../shared/api/httpClient', () => ({
  default: { post: (...args: unknown[]) => post(...args) },
}));
vi.mock('../../../shared/api/normalizeApiError', () => ({
  normalizeApiError: () => ({ category: 'network', message: 'net', isVisitFormDetailMissing: false, cause: null }),
}));

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <ConfirmEmailPage />
    </MemoryRouter>,
  );
}

describe('ConfirmEmailPage', () => {
  beforeEach(() => post.mockReset());

  it('confirms a valid token and posts it exactly once (never a GET)', async () => {
    post.mockResolvedValue({ data: { success: true, status: 'CONFIRMED', message: 'ok' } });
    renderAt('/confirm-email?token=abc');
    expect(await screen.findByTestId('confirm-success')).toBeTruthy();
    expect(post).toHaveBeenCalledWith('/public/account-confirmations/confirm', { token: 'abc' });
    expect(post).toHaveBeenCalledTimes(1);
  });

  it('treats an already-confirmed replay as success', async () => {
    post.mockResolvedValue({ data: { success: true, status: 'ALREADY_CONFIRMED', message: 'ok' } });
    renderAt('/confirm-email?token=abc');
    expect(await screen.findByTestId('confirm-success')).toBeTruthy();
  });

  it('shows the expired state', async () => {
    post.mockResolvedValue({ data: { success: false, status: 'EXPIRED', message: 'het han' } });
    renderAt('/confirm-email?token=abc');
    expect(await screen.findByTestId('confirm-expired')).toBeTruthy();
  });

  it('shows the invalid state', async () => {
    post.mockResolvedValue({ data: { success: false, status: 'INVALID', message: 'invalid' } });
    renderAt('/confirm-email?token=abc');
    expect(await screen.findByTestId('confirm-invalid')).toBeTruthy();
  });

  it('shows invalid immediately when the token is missing and never posts', async () => {
    renderAt('/confirm-email');
    expect(await screen.findByTestId('confirm-invalid')).toBeTruthy();
    expect(post).not.toHaveBeenCalled();
  });

  it('never confirms with a GET (the only network call is the POST)', async () => {
    post.mockResolvedValue({ data: { success: true, status: 'CONFIRMED', message: 'ok' } });
    renderAt('/confirm-email?token=abc');
    await screen.findByTestId('confirm-success');
    // Every call went to the POST confirm endpoint — a state change must never happen via GET.
    for (const call of post.mock.calls) {
      expect(call[0]).toBe('/public/account-confirmations/confirm');
    }
  });
});
