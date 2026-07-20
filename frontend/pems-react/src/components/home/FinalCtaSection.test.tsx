import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({ useNavigate: () => navigateMock }));

// Stub the heavy v1 form popup so this test stays light and only asserts open/closed.
vi.mock('../modals/VisitingFormPopup', () => ({
  VisitingFormPopup: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="v1-popup" /> : null,
}));

// react-hot-toast: capture the error/loading calls the entry CTA makes.
const toastError = vi.fn();
const toastLoading = vi.fn();
const toastDismiss = vi.fn();
vi.mock('react-hot-toast', () => ({
  default: Object.assign(vi.fn(), { error: (...a: unknown[]) => toastError(...a), loading: (...a: unknown[]) => toastLoading(...a), dismiss: (...a: unknown[]) => toastDismiss(...a) }),
}));

const retryMock = vi.fn();
const capabilityMock = vi.fn();
vi.mock('../../shared/features/perCampusV2Capability', () => ({
  usePerCampusV2Capability: () => capabilityMock(),
}));

import { FinalCtaSection } from './FinalCtaSection';

describe('FinalCtaSection v2 cutover', () => {
  beforeEach(() => {
    navigateMock.mockClear();
    capabilityMock.mockReset();
    toastError.mockClear();
    toastLoading.mockClear();
    retryMock.mockClear();
  });

  it('routes to the v2 registration page when the capability is enabled', () => {
    capabilityMock.mockReturnValue({ status: 'ready', enabled: true, readEnabled: true, writeEnabled: true, retry: retryMock });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(navigateMock).toHaveBeenCalledWith('/visit-registration/v2');
    expect(screen.queryByTestId('v1-popup')).toBeNull();
  });

  it('opens the v1 popup when the capability is disabled (flags OFF)', () => {
    capabilityMock.mockReturnValue({ status: 'ready', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(navigateMock).not.toHaveBeenCalled();
    expect(screen.getByTestId('v1-popup')).toBeInTheDocument();
  });

  it('surfaces an error with retry (NOT a silent v1 fallback) when the capability errored', () => {
    // Behaviour change (owner-requested): a fetch failure must not downgrade users to v1. It shows
    // an error toast with a Retry, and never opens the v1 popup or routes to v2.
    capabilityMock.mockReturnValue({ status: 'error', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(navigateMock).not.toHaveBeenCalled();
    expect(screen.queryByTestId('v1-popup')).toBeNull();
    expect(toastError).toHaveBeenCalledTimes(1);
  });

  it('disables the CTA while the capability is still resolving', () => {
    capabilityMock.mockReturnValue({ status: 'loading', enabled: false, readEnabled: false, writeEnabled: false, retry: retryMock });
    render(<FinalCtaSection />);

    expect(screen.getByRole('button')).toBeDisabled();
  });
});
