import { describe, expect, it } from 'vitest';
import { render, screen, within, fireEvent } from '@testing-library/react';
import { PersonListTable, type PersonRow } from '../components/v2/shared/PersonListTable';
import { VisitStatusBadge } from '../components/v2/shared/VisitStatusBadge';
import { VisitSectionCard } from '../components/v2/shared/VisitSectionCard';
import {
  isKnownVisitStatus,
  visitStatusI18nKey,
  visitStatusTone,
} from '../components/v2/shared/visitStatus';

// jsdom's navigator.language is en-US → i18n initializes in EN; assertions use the EN strings.

const people = (...names: string[]): PersonRow[] =>
  names.map((fullName, i) => ({
    id: i + 100,
    fullName,
    jobTitle: 'Giảng viên',
    organization: 'ĐH ABC',
    nationality: 'VN',
  }));

const tableCells = (testId: string) =>
  within(within(screen.getByTestId(testId)).getByRole('table'))
    .getAllByRole('cell')
    .map(c => c.textContent);

describe('PersonListTable', () => {
  it('numbers rows from the render position, not from the data', () => {
    // Nothing in the payload carries an ordinal — it is a reading aid for THIS list.
    render(
      <PersonListTable data-testid="t" title="Khách" rows={people('A', 'B', 'C')} emptyMessage="none" />,
    );

    const cells = tableCells('t');
    expect(cells[0]).toBe('1');
    expect(cells[5]).toBe('2');
    expect(cells[10]).toBe('3');
  });

  it('renumbers without gaps when a row is removed', () => {
    const { rerender } = render(
      <PersonListTable data-testid="t" title="Khách" rows={people('A', 'B', 'C')} emptyMessage="none" />,
    );
    expect(tableCells('t').filter((_, i) => i % 5 === 0)).toEqual(['1', '2', '3']);

    // Drop the middle person: a stored ordinal would leave "1, 3" and read as missing data.
    rerender(
      <PersonListTable data-testid="t" title="Khách" rows={people('A', 'C')} emptyMessage="none" />,
    );
    const cells = tableCells('t');
    expect(cells.filter((_, i) => i % 5 === 0)).toEqual(['1', '2']);
    expect(cells[1]).toBe('A');
    expect(cells[6]).toBe('C');
  });

  it('carries every field into the narrow layout', () => {
    render(<PersonListTable data-testid="t" title="Khách" rows={people('A')} emptyMessage="none" />);

    const cards = screen.getByTestId('t').querySelector('ul') as HTMLElement;
    expect(within(cards).getByText('A')).toBeInTheDocument();
    expect(within(cards).getByText('Giảng viên')).toBeInTheDocument();
    expect(within(cards).getByText('ĐH ABC')).toBeInTheDocument();
    expect(within(cards).getByText('VN')).toBeInTheDocument();
  });

  it('shows a placeholder for a person whose optional fields are blank', () => {
    render(
      <PersonListTable
        data-testid="t"
        title="Khách"
        rows={[{ id: 1, fullName: 'Chỉ có tên', jobTitle: null, organization: '', nationality: undefined }]}
        emptyMessage="none"
      />,
    );

    expect(tableCells('t')).toEqual(['1', 'Chỉ có tên', '—', '—', '—']);
  });

  it('states an empty list instead of rendering an empty table', () => {
    render(<PersonListTable data-testid="t" title="Khách" rows={[]} emptyMessage="Chưa có ai." />);

    expect(screen.getByText('Chưa có ai.')).toBeInTheDocument();
    expect(within(screen.getByTestId('t')).queryByRole('table')).toBeNull();
  });
});

describe('VisitStatusBadge', () => {
  it('renders the campus lifecycle in plain language', () => {
    render(<VisitStatusBadge kind="instance" status="WAITING_REQUEST_APPROVAL" data-testid="b" />);
    expect(screen.getByTestId('b')).toHaveTextContent('Awaiting Staff Leader');
    expect(screen.getByTestId('b')).not.toHaveTextContent('WAITING_REQUEST_APPROVAL');
  });

  it('renders the aggregate request status from its own vocabulary', () => {
    render(<VisitStatusBadge kind="request" status="PARTIALLY_APPROVED" data-testid="b" />);
    expect(screen.getByTestId('b')).toHaveTextContent('Partially approved');
  });

  it.each([
    ['a value from the OTHER enum', 'instance', 'PARTIALLY_APPROVED'],
    ['an unknown value', 'request', 'SOMETHING_NEW'],
    ['an empty value', 'instance', ''],
    ['a null value', 'instance', null],
  ])('falls back to the neutral label for %s', (_label, kind, status) => {
    render(<VisitStatusBadge kind={kind as 'instance' | 'request'} status={status} data-testid="b" />);

    expect(screen.getByTestId('b')).toHaveTextContent('Unknown');
    expect(screen.getByTestId('b').textContent).not.toContain('_');
  });

  it('matches statuses case- and whitespace-insensitively', () => {
    render(<VisitStatusBadge kind="instance" status="  during_visit " data-testid="b" />);
    expect(screen.getByTestId('b')).toHaveTextContent('Visit in progress');
  });
});

describe('visitStatus helpers', () => {
  it('keeps the two enums separate', () => {
    // ASSIGNED exists only on a campus instance; APPROVED only on the aggregate request.
    expect(isKnownVisitStatus('instance', 'ASSIGNED')).toBe(true);
    expect(isKnownVisitStatus('request', 'ASSIGNED')).toBe(false);
    expect(isKnownVisitStatus('request', 'APPROVED')).toBe(true);
    expect(isKnownVisitStatus('instance', 'APPROVED')).toBe(false);
  });

  it('routes an unknown value to the shared unknown key and a neutral tone', () => {
    expect(visitStatusI18nKey('instance', 'NOPE')).toBe('visitRequestV2:status.unknown');
    expect(visitStatusTone('instance', 'NOPE')).toBe('neutral');
  });
});

describe('VisitSectionCard', () => {
  it('exposes the collapse state to assistive tech and hides the body when closed', () => {
    render(
      <VisitSectionCard step={1} title="Người đăng ký" data-testid="s">
        <p>nội dung</p>
      </VisitSectionCard>,
    );

    const toggle = screen.getByRole('button', { name: /Người đăng ký/ });
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(screen.getByText('nội dung')).toBeInTheDocument();

    fireEvent.click(toggle);
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
  });

  it('offers no toggle at all when the section is not collapsible', () => {
    render(
      <VisitSectionCard title="Không thu gọn" collapsible={false} data-testid="s">
        <p>luôn hiện</p>
      </VisitSectionCard>,
    );

    expect(screen.queryByRole('button')).toBeNull();
    expect(screen.getByText('luôn hiện')).toBeInTheDocument();
  });
});
