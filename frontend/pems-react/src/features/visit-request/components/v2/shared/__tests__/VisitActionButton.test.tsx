import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { VisitActionButton } from '../VisitActionButton';
import type { VisitActionCapability } from '../../../../api/visitRequestV2Api';

const cutoffCapability = (overrides: Partial<VisitActionCapability> = {}): VisitActionCapability => ({
  code: 'SUBMIT_SAFE_EDIT',
  scope: 'INSTANCE',
  enabled: false,
  disabledReasonCode: 'VISIT_MUTATION_CUTOFF_REACHED',
  cutoffAt: '2026-08-18T03:00:00',
  plannedStartAt: '2026-08-19T09:00:00',
  campusName: 'FPTU Hà Nội',
  requiredLeadHours: 6,
  ...overrides,
});

describe('VisitActionButton', () => {
  it('renders a plain, clickable button when the action is granted', () => {
    const onClick = vi.fn();
    render(
      <VisitActionButton granted capability={undefined} onClick={onClick} data-testid="quick-edit">
        Sửa nhanh
      </VisitActionButton>,
    );

    const button = screen.getByTestId('quick-edit');
    expect(button).not.toHaveAttribute('aria-disabled');
    fireEvent.click(button);
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  // ── E1 — no permission at all: hidden, not a greyed-out button ──────────────────────────────────

  it('renders nothing when refused for a reason other than the cutoff (no permission at all)', () => {
    const { container } = render(
      <VisitActionButton
        granted={false}
        capability={{ ...cutoffCapability(), disabledReasonCode: 'VISIT_MUTATION_RELATION_NOT_ALLOWED' }}
        onClick={vi.fn()}
        data-testid="quick-edit"
      >
        Sửa nhanh
      </VisitActionButton>,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when the backend sent no capability at all', () => {
    const { container } = render(
      <VisitActionButton granted={false} capability={undefined} onClick={vi.fn()} data-testid="quick-edit">
        Sửa nhanh
      </VisitActionButton>,
    );
    expect(container).toBeEmptyDOMElement();
  });

  // ── E2/E3 — cutoff refusal: a real disabled control with a SHORT tooltip, not a long paragraph ──

  it('renders a disabled-but-focusable control with a short tooltip on a cutoff refusal — no long paragraph', () => {
    const onClick = vi.fn();
    render(
      <VisitActionButton granted={false} capability={cutoffCapability()} onClick={onClick} data-testid="quick-edit">
        Sửa nhanh
      </VisitActionButton>,
    );

    const button = screen.getByTestId('quick-edit-disabled');
    // aria-disabled, NEVER the native `disabled` attribute — a truly disabled button cannot receive
    // keyboard focus at all, which would make the tooltip unreachable by keyboard (§E3).
    expect(button).toHaveAttribute('aria-disabled', 'true');
    expect(button).not.toHaveAttribute('disabled');
    fireEvent.click(button);
    expect(onClick).not.toHaveBeenCalled();

    const reason = screen.getByTestId('quick-edit-reason');
    expect(reason).toHaveTextContent(/at least 6 hours/i);
    expect(reason).toHaveTextContent(/Deadline:/i);
    // The old always-visible paragraph repeated the campus name and the start time — both dropped:
    // the campus is already named by the card this button sits in, and the start time sits elsewhere
    // on the same card (§E2).
    expect(reason).not.toHaveTextContent(/Unavailable for/i);
    expect(reason).not.toHaveTextContent(/Starts at/i);
  });

  it('is reachable by keyboard focus, so the tooltip is not mouse-hover-only', () => {
    render(
      <VisitActionButton granted={false} capability={cutoffCapability()} onClick={vi.fn()} data-testid="quick-edit">
        Sửa nhanh
      </VisitActionButton>,
    );

    const button = screen.getByTestId('quick-edit-disabled');
    button.focus();
    expect(button).toHaveFocus();
    expect(button).toHaveAttribute('aria-describedby', 'quick-edit-reason');
  });
});
