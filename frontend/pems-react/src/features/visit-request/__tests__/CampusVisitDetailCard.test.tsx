import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { CampusVisitDetailCard } from '../components/v2/CampusVisitDetailCard';
import { campusFixture } from './fixtures';

// jsdom's navigator.language is en-US → the i18n config initializes in EN; assertions
// below use the EN strings (plus structural roles/aria, which are language-free).

describe('CampusVisitDetailCard', () => {
  it('renders THIS campus snapshot: name, status, content, host, decision, revision', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    expect(screen.getByText('FPTU Hà Nội')).toBeInTheDocument();
    // The host moved out of the decision grid into its own block; the decision grid keeps who
    // decided and when, which is a different question from who is running the campus.
    expect(screen.getByTestId(`reception-host-${'10'}-current-name`)).toBeInTheDocument();
    expect(screen.getByText('Đoàn ĐH ABC')).toBeInTheDocument();
    expect(screen.getByText('Trao đổi hợp tác')).toBeInTheDocument();
    expect(screen.getByText('Nội dung làm việc HN')).toBeInTheDocument();
    expect(screen.getByText('Host Hà Nội')).toBeInTheDocument();
    expect(screen.getByText(/Leader HN/)).toBeInTheDocument();
    // The fixture is ASSIGNED with a recorded decision → the content is in force and approved.
    expect(screen.getByText('Content version 2 is in force')).toBeInTheDocument();
    expect(screen.getByText('Approved at round 1')).toBeInTheDocument();
    // Masked-scope guarantee: the card shows ONLY what it was given (no sibling data).
    expect(screen.queryByText(/HCM/)).not.toBeInTheDocument();
  });

  it('does not claim approval on a campus nobody has decided yet', () => {
    // approvalRevision is 1 from the moment the request is created, so the old unconditional
    // "Approval v1" told a waiting visitor their campus had already approved.
    render(<CampusVisitDetailCard campus={campusFixture({
      instanceStatus: 'WAITING_REQUEST_APPROVAL', decidedAt: null, decidedByName: null,
      formRevision: 1, approvalRevision: 1,
    })} />);

    expect(screen.getByText('Approval status: not approved yet')).toBeInTheDocument();
    expect(screen.getByText('Current content: version 1')).toBeInTheDocument();
    expect(screen.queryByText(/Approved at round/)).not.toBeInTheDocument();
  });

  it('never renders a raw enum, even for a status the UI has not been taught', () => {
    render(<CampusVisitDetailCard campus={campusFixture({ instanceStatus: 'SOME_FUTURE_STATE' })} />);

    expect(screen.queryByText('SOME_FUTURE_STATE')).not.toBeInTheDocument();
    expect(screen.getByTestId('campus-status-10')).toHaveTextContent('Unknown');
  });

  it('shows the delegation up front, numbered, without a toggle to find it behind', () => {
    // The people list used to start collapsed. "Who is coming" is the reason this card gets
    // opened at all, so it is now on the page from the first render.
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    // Both layouts are in the DOM (CSS picks one), so assertions are scoped to the table.
    const table = within(screen.getByTestId('campus-visitors-10')).getByRole('table');
    const cells = within(table).getAllByRole('cell').map(c => c.textContent);
    // The ordinal is derived from the row position, never stored.
    expect(cells.slice(0, 5)).toEqual(['1', 'Khách Một', 'GV', 'ĐH ABC', 'VN']);
    expect(within(screen.getByTestId('campus-visitors-10')).getByText('1 people')).toBeInTheDocument();
  });

  it('keeps every field for a person on the narrow layout too', () => {
    // The mobile cards are always rendered (CSS decides which layout is visible), so dropping a
    // field to shorten them would silently lose data on a phone.
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    const visitors = screen.getByTestId('campus-visitors-10');
    const mobileCards = visitors.querySelector('ul');
    expect(mobileCards).not.toBeNull();
    expect(within(mobileCards as HTMLElement).getByText('Khách Một')).toBeInTheDocument();
    expect(within(mobileCards as HTMLElement).getByText('GV')).toBeInTheDocument();
    expect(within(mobileCards as HTMLElement).getByText('ĐH ABC')).toBeInTheDocument();
    expect(within(mobileCards as HTMLElement).getByText('VN')).toBeInTheDocument();
  });

  it('states an empty support list instead of hiding the section', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    const support = screen.getByTestId('campus-support-10');
    expect(within(support).getByText('No accompanying support staff.')).toBeInTheDocument();
  });

  it('shows the pending-amendment badge only when an active amendment exists', () => {
    const { rerender } = render(<CampusVisitDetailCard campus={campusFixture()} />);
    expect(screen.queryByText(/Amendment #/)).not.toBeInTheDocument();

    rerender(
      <CampusVisitDetailCard
        campus={campusFixture({
          activeAmendment: { amendmentId: 9, amendmentNo: 2, status: 'PENDING', requestedAt: '2026-07-21T08:00:00', changedFieldCount: 3 },
        })}
      />,
    );
    expect(screen.getByText('Amendment #2 pending')).toBeInTheDocument();
  });

  it('renders visitTypeOther text when the type is OTHER', () => {
    render(
      <CampusVisitDetailCard campus={campusFixture({ visitType: 'OTHER', visitTypeOther: 'Thăm phòng lab' })} />,
    );
    expect(screen.getByText('Thăm phòng lab')).toBeInTheDocument();
  });
});
