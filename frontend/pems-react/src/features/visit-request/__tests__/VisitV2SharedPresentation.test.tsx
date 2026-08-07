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
import viVisitRequestV2 from '../../../shared/i18n/locales/vi/visitRequestV2.json';
import enVisitRequestV2 from '../../../shared/i18n/locales/en/visitRequestV2.json';

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

  // The confirmation gate has a value on BOTH enums, and neither was in the vocabulary: a request
  // sitting at PENDING_CONTACT_CONFIRMATION rendered "Unknown" on the detail screen, directly above
  // a campus card that rendered "Unknown" for WAITING_CONTACT_CONFIRMATION.
  it('names the request-level confirmation gate instead of falling back to Unknown', () => {
    render(<VisitStatusBadge kind="request" status="PENDING_CONTACT_CONFIRMATION" data-testid="b" />);

    expect(screen.getByTestId('b')).toHaveTextContent('Awaiting delegation contact confirmation');
    expect(screen.getByTestId('b')).not.toHaveTextContent('Unknown');
    expect(screen.getByTestId('b').textContent).not.toContain('_');
  });

  it('names the campus-level confirmation gate too', () => {
    render(<VisitStatusBadge kind="instance" status="WAITING_CONTACT_CONFIRMATION" data-testid="b" />);

    expect(screen.getByTestId('b')).toHaveTextContent('Awaiting delegation contact confirmation');
    expect(screen.getByTestId('b')).not.toHaveTextContent('Unknown');
  });

  it.each([
    ['a value from the OTHER enum', 'instance', 'PARTIALLY_APPROVED'],
    ['an unknown value', 'request', 'SOMETHING_NEW'],
    ['an empty value', 'instance', ''],
    ['a null value', 'instance', null],
    // The two gate values stay in their own enum: the request one is not a campus state and the
    // campus one is not a request state, so each is still Unknown on the other side.
    ['the request gate value on the instance enum', 'instance', 'PENDING_CONTACT_CONFIRMATION'],
    ['the instance gate value on the request enum', 'request', 'WAITING_CONTACT_CONFIRMATION'],
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
    // …and the confirmation gate is two DIFFERENT values, one per enum, not one shared value.
    expect(isKnownVisitStatus('request', 'PENDING_CONTACT_CONFIRMATION')).toBe(true);
    expect(isKnownVisitStatus('instance', 'PENDING_CONTACT_CONFIRMATION')).toBe(false);
    expect(isKnownVisitStatus('instance', 'WAITING_CONTACT_CONFIRMATION')).toBe(true);
    expect(isKnownVisitStatus('request', 'WAITING_CONTACT_CONFIRMATION')).toBe(false);
  });

  it('tones the confirmation gate as waiting, on both enums', () => {
    expect(visitStatusTone('request', 'PENDING_CONTACT_CONFIRMATION')).toBe('waiting');
    expect(visitStatusTone('instance', 'WAITING_CONTACT_CONFIRMATION')).toBe('waiting');
    expect(visitStatusI18nKey('request', 'PENDING_CONTACT_CONFIRMATION'))
      .toBe('visitRequestV2:status.request.PENDING_CONTACT_CONFIRMATION');
  });

  it('routes an unknown value to the shared unknown key and a neutral tone', () => {
    expect(visitStatusI18nKey('instance', 'NOPE')).toBe('visitRequestV2:status.unknown');
    expect(visitStatusTone('instance', 'NOPE')).toBe('neutral');
  });

  // The badge renders in ONE language per run (jsdom picks EN), so the other locale can only be
  // checked at its source. A key present in one file and missing in the other falls back silently:
  // the reader gets Vietnamese in an English UI, or "Unknown" again.
  it.each([
    ['vi', viVisitRequestV2, 'Chờ đầu mối đoàn khách xác nhận'],
    ['en', enVisitRequestV2, 'Awaiting delegation contact confirmation'],
  ])('labels the confirmation gate in %s, on both enums', (_lang, bundle, expected) => {
    const status = (bundle as { status: { request: Record<string, string>; instance: Record<string, string> } }).status;

    expect(status.request.PENDING_CONTACT_CONFIRMATION).toBe(expected);
    expect(status.instance.WAITING_CONTACT_CONFIRMATION).toBe(expected);
    // Each value belongs to exactly one enum — a stray copy is how the two vocabularies drift.
    expect(status.instance.PENDING_CONTACT_CONFIRMATION).toBeUndefined();
    expect(status.request.WAITING_CONTACT_CONFIRMATION).toBeUndefined();
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
