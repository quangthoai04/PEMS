import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { VisitChangeBadges } from '../../delegations/components/VisitChangeBadges';
import type { VisitListChangeSummary } from '../../delegations/types/delegations.types';

// jsdom reports en-US, so i18n initialises in EN and the assertions use the EN strings.

const summary = (over: Partial<VisitListChangeSummary> = {}): VisitListChangeSummary => ({
  hasUnreadChanges: true,
  unreadChangeCount: 1,
  latestEventCode: 'CONTENT_UPDATED',
  latestChangedAt: '2026-07-25T10:00:00',
  pendingAmendmentCount: 0,
  requiresViewerAction: false,
  campusIndicators: [],
  ...over,
});

describe('VisitChangeBadges', () => {
  it('renders nothing when the row has no change summary', () => {
    const { container } = render(<VisitChangeBadges summary={null} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders nothing when there is a summary but nothing in it', () => {
    // A quiet row must stay quiet: an empty badge strip still costs a line of layout.
    const { container } = render(
      <VisitChangeBadges summary={summary({ hasUnreadChanges: false, unreadChangeCount: 0, latestEventCode: null })} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('names the kind of change rather than showing a bare count', () => {
    render(<VisitChangeBadges summary={summary({ latestEventCode: 'HOST_CHANGED' })} />);
    expect(screen.getByTestId('change-badge-latest')).toHaveTextContent('Reception owner changed');
  });

  it('leads with the approval badge when a change needs a decision', () => {
    render(<VisitChangeBadges summary={summary({ pendingAmendmentCount: 2, requiresViewerAction: true })} />);
    const badges = screen.getByTestId('visit-change-badges');
    expect(badges.textContent).toMatch(/^2 changes to approve/);
  });

  it('omits the count badge when a single change is already described', () => {
    // "1 unseen change" next to a badge naming that one change says nothing new.
    render(<VisitChangeBadges summary={summary({ unreadChangeCount: 1 })} />);
    expect(screen.queryByTestId('change-badge-count')).toBeNull();
  });

  it('shows the count once several changes are stacked up', () => {
    render(<VisitChangeBadges summary={summary({ unreadChangeCount: 3 })} />);
    expect(screen.getByTestId('change-badge-count')).toHaveTextContent('3 unseen changes');
  });

  it('falls back to neutral wording for an event code it has not been taught', () => {
    // A future backend code must never surface as a raw enum in front of a user.
    render(<VisitChangeBadges summary={summary({ latestEventCode: 'SOMETHING_NEW' })} />);
    const badge = screen.getByTestId('change-badge-latest');
    expect(badge).toHaveTextContent('Recently updated');
    expect(badge).not.toHaveTextContent('SOMETHING_NEW');
  });
});
