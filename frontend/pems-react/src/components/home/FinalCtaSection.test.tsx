import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({ useNavigate: () => navigateMock }));

// Stub the heavy v1 form popup so this test stays light and only asserts open/closed.
vi.mock('../modals/VisitingFormPopup', () => ({
  VisitingFormPopup: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="v1-popup" /> : null,
}));

const capabilityMock = vi.fn();
vi.mock('../../shared/features/perCampusV2Capability', () => ({
  usePerCampusV2Capability: () => capabilityMock(),
}));

import { FinalCtaSection } from './FinalCtaSection';

describe('FinalCtaSection v2 cutover', () => {
  beforeEach(() => {
    navigateMock.mockClear();
    capabilityMock.mockReset();
  });

  it('routes to the v2 registration page when the capability is enabled', () => {
    capabilityMock.mockReturnValue({ status: 'ready', enabled: true, readEnabled: true, writeEnabled: true });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(navigateMock).toHaveBeenCalledWith('/visit-registration/v2');
    expect(screen.queryByTestId('v1-popup')).toBeNull();
  });

  it('opens the v1 popup when the capability is disabled (flags OFF)', () => {
    capabilityMock.mockReturnValue({ status: 'ready', enabled: false, readEnabled: false, writeEnabled: false });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(navigateMock).not.toHaveBeenCalled();
    expect(screen.getByTestId('v1-popup')).toBeInTheDocument();
  });

  it('falls back to the v1 popup when the capability errored (fail-safe)', () => {
    capabilityMock.mockReturnValue({ status: 'error', enabled: false, readEnabled: false, writeEnabled: false });
    render(<FinalCtaSection />);

    fireEvent.click(screen.getByRole('button'));

    expect(navigateMock).not.toHaveBeenCalled();
    expect(screen.getByTestId('v1-popup')).toBeInTheDocument();
  });

  it('disables the CTA while the capability is still resolving', () => {
    capabilityMock.mockReturnValue({ status: 'loading', enabled: false, readEnabled: false, writeEnabled: false });
    render(<FinalCtaSection />);

    expect(screen.getByRole('button')).toBeDisabled();
  });
});
