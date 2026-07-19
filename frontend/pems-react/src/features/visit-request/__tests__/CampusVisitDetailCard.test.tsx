import { describe, expect, it } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CampusVisitDetailCard } from '../components/v2/CampusVisitDetailCard';
import { campusFixture } from './fixtures';

// jsdom's navigator.language is en-US → the i18n config initializes in EN; assertions
// below use the EN strings (plus structural roles/aria, which are language-free).

describe('CampusVisitDetailCard', () => {
  it('renders THIS campus snapshot: name, status, content, host, decision, revision', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    expect(screen.getByText('FPTU Hà Nội')).toBeInTheDocument();
    expect(screen.getByText('Approved')).toBeInTheDocument();
    expect(screen.getByText('Đoàn ĐH ABC')).toBeInTheDocument();
    expect(screen.getByText('Trao đổi hợp tác')).toBeInTheDocument();
    expect(screen.getByText('Nội dung làm việc HN')).toBeInTheDocument();
    expect(screen.getByText('Host Hà Nội')).toBeInTheDocument();
    expect(screen.getByText(/Leader HN/)).toBeInTheDocument();
    expect(screen.getByText('Content v2 · Approval v1')).toBeInTheDocument();
    // Masked-scope guarantee: the card shows ONLY what it was given (no sibling data).
    expect(screen.queryByText(/HCM/)).not.toBeInTheDocument();
  });

  it('people list is collapsed by default and toggles with an accessible button', () => {
    render(<CampusVisitDetailCard campus={campusFixture()} />);

    const toggle = screen.getByRole('button', { name: /Delegation list \(1 guests, 0 support\)/ });
    const region = document.getElementById('cvd-people-10')!;
    // jsdom loads no Tailwind CSS, so visibility is asserted via the `hidden` class the
    // component toggles (the aria-expanded state is the accessible contract).
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(region).toHaveClass('hidden');

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(region).not.toHaveClass('hidden');
    expect(screen.getByText('Khách Một')).toBeInTheDocument();

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(region).toHaveClass('hidden');
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
