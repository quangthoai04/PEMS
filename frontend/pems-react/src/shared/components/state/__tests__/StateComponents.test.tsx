import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LoadingState } from '../LoadingState';
import { EmptyState } from '../EmptyState';
import { ErrorState } from '../ErrorState';
import { StaleDataBanner } from '../StaleDataBanner';
import { VISIT_FORM_DETAIL_MISSING } from '../../../api/normalizeApiError';

const httpError = (status: number, data?: unknown) =>
  Object.assign(new Error(`Request failed with status code ${status}`), {
    isAxiosError: true,
    code: 'ERR_BAD_RESPONSE',
    response: { status, data, headers: {} },
    config: {},
  });

const networkError = () =>
  Object.assign(new Error('Network Error'), { isAxiosError: true, code: 'ERR_NETWORK', request: {} });

describe('LoadingState', () => {
  it('announces itself as a live status and never reads as empty', () => {
    render(<LoadingState />);
    const status = screen.getByRole('status');
    expect(status).toHaveTextContent(/loading/i);
  });
});

describe('EmptyState', () => {
  it('shows the business-meaning title for a genuinely empty result', () => {
    render(<EmptyState title="No minutes yet" />);
    expect(screen.getByText('No minutes yet')).toBeInTheDocument();
  });
});

describe('ErrorState', () => {
  it('renders a permission-specific title for a 403 — not "no data"', () => {
    render(<ErrorState error={httpError(403, { message: 'nope' })} />);
    expect(screen.getByRole('alert')).toHaveTextContent(/permission/i);
  });

  it('renders the Pure V2 "details not set up" title for the form-detail-missing 409', () => {
    render(<ErrorState error={httpError(409, { errorCode: VISIT_FORM_DETAIL_MISSING })} />);
    expect(screen.getByRole('alert')).toHaveTextContent(/not fully set up/i);
  });

  it('renders a network-specific title for a dropped connection', () => {
    render(<ErrorState error={networkError()} />);
    expect(screen.getByRole('alert')).toHaveTextContent(/reach the server/i);
  });

  it('surfaces the backend error code for support', () => {
    render(<ErrorState error={httpError(409, { errorCode: 'SOME_CODE', message: 'conflict' })} />);
    expect(screen.getByRole('alert')).toHaveTextContent('SOME_CODE');
  });

  it('calls onRetry when the retry button is clicked', async () => {
    const onRetry = vi.fn();
    render(<ErrorState error={httpError(500)} onRetry={onRetry} />);
    await userEvent.click(screen.getByRole('button', { name: /try again/i }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('hides the retry button when no handler is given', () => {
    render(<ErrorState error={httpError(500)} />);
    expect(screen.queryByRole('button', { name: /try again/i })).not.toBeInTheDocument();
  });
});

describe('StaleDataBanner', () => {
  it('states the data may be out of date and offers a retry', async () => {
    const onRetry = vi.fn();
    render(<StaleDataBanner onRetry={onRetry} />);
    expect(screen.getByRole('status')).toHaveTextContent(/out of date/i);
    await userEvent.click(screen.getByRole('button', { name: /try again/i }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });
});
