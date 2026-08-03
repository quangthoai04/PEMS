import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, within } from '@testing-library/react';

/**
 * Two regressions are pinned here:
 *  1. the "Thêm cơ sở" ceiling was hardcoded to 10 while only 5 campuses accept registrations, so
 *     the form advertised campuses that could never be chosen;
 *  2. the guest / support lists were loose input grids with no ordinal, unlike v1's table.
 * Both are driven by live backend data, so retiring or adding a campus must change the form with
 * no code change — the mocked campus list below is what "the backend said", nothing more.
 */

const CAMPUSES = [
  { campusCode: 'HN', campusName: 'Hòa Lạc' },
  { campusCode: 'HCM', campusName: 'TP. Hồ Chí Minh' },
  { campusCode: 'DN', campusName: 'Đà Nẵng' },
];

const campusesMock = vi.fn();
vi.mock('../hooks/useRegistrationCampuses', () => ({
  useRegistrationCampuses: () => campusesMock(),
}));

vi.mock('../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ user: null }),
}));

vi.mock('../api/visitRequestApi', () => ({
  visitRequestApi: {
    getCreateHostCandidates: vi.fn().mockResolvedValue([]),
    initiate: vi.fn(), resendOtp: vi.fn(), recoverOtp: vi.fn(),
  },
}));

vi.mock('../api/visitRequestV2Api', () => ({
  createVisitRequestV2: vi.fn(),
  initiateVisitRequestV2: vi.fn(),
  verifyAndCreateVisitRequestV2: vi.fn(),
}));

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    // `t` is called both as t(key, {opts}) and as t(key, 'fallbackString'); only the object form
    // carries the count/max interpolation this test reads.
    t: (key: string, opts?: unknown) => {
      if (opts && typeof opts === 'object' && 'max' in opts) {
        const { count, max } = opts as unknown as { count: number; max: number };
        return `${key}:${count}/${max}`;
      }
      return key;
    },
    i18n: { language: 'vi' },
  }),
}));

import { VisitRequestFormV2 } from '../components/v2/VisitRequestFormV2';

const campusSelects = () =>
  screen.getAllByRole('combobox').filter(el =>
    el.tagName === 'SELECT'
    && Array.from((el as HTMLSelectElement).options).some(o => CAMPUSES.some(c => c.campusCode === o.value)));

const addCampusButton = () => screen.getByTestId('v2-add-campus');

describe('VisitRequestFormV2 — campus limit and member tables', () => {
  beforeEach(() => {
    campusesMock.mockReset();
    campusesMock.mockReturnValue({ campuses: CAMPUSES, loading: false });
  });

  it('caps campuses at the number the backend actually offers, not a hardcoded 10', () => {
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);
    // Label carries count/max — 1 of 3 here, because three campuses were returned.
    expect(addCampusButton().textContent).toContain('1/3');
    expect(addCampusButton()).not.toBeDisabled();
  });

  it('reflects a different campus list with no code change', () => {
    campusesMock.mockReturnValue({
      campuses: [{ campusCode: 'HN', campusName: 'Hòa Lạc' }],
      loading: false,
    });
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    expect(addCampusButton().textContent).toContain('1/1');
    // A single campus is already the whole list — there is nothing left to add.
    expect(addCampusButton()).toBeDisabled();
  });

  it('disables adding once every campus is taken, and re-enables after a removal', () => {
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    fireEvent.click(addCampusButton());
    fireEvent.click(addCampusButton());
    expect(addCampusButton()).toBeDisabled();
    expect(addCampusButton().textContent).toContain('3/3');

    // Removing an untouched card frees a slot again.
    fireEvent.click(screen.getAllByLabelText('visitRequestV2:card.removeCampus')[0]);
    expect(addCampusButton()).not.toBeDisabled();
    expect(addCampusButton().textContent).toContain('2/3');
  });

  it('never offers a campus already chosen on another card', () => {
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);
    fireEvent.click(addCampusButton());

    const [first, second] = campusSelects();
    fireEvent.change(first, { target: { value: 'HN' } });

    const secondOptions = Array.from((second as HTMLSelectElement).options).map(o => o.value);
    expect(secondOptions).not.toContain('HN');
    expect(secondOptions).toContain('HCM');
    expect(secondOptions).toContain('DN');

    // The card holding the selection still lists its own campus, or it could not stay selected.
    const firstOptions = Array.from((first as HTMLSelectElement).options).map(o => o.value);
    expect(firstOptions).toContain('HN');
  });

  it('renders guests and support members as tables with the v1 column set', () => {
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    // A card opens with one guest row and NO support row, and an empty support list is drawn as a
    // placeholder rather than a headerless table (commit f172e531). Add one so there is a table to
    // check — the column set is the assertion, and it only exists once the section has a row.
    expect(screen.getByText('visitRequestV2:person.noSupportTeam')).toBeInTheDocument();
    fireEvent.click(screen.getAllByText('visitRequestV2:card.addSupport')[0]);

    for (const kind of ['visitors', 'supportTeam']) {
      const table = screen.getByTestId(`v2-${kind}-table`);
      const headers = within(table).getAllByRole('columnheader').map(h => h.textContent);
      expect(headers).toEqual([
        'visitRequestV2:person.stt',
        'visitRequestV2:person.fullName',
        'visitRequestV2:person.jobTitle',
        'visitRequestV2:person.organization',
        'visitRequestV2:person.nationality',
        'visitRequestV2:person.actions',
      ]);
    }
  });

  it('renumbers the ordinal column after a row is removed', () => {
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    fireEvent.click(screen.getByText('visitRequestV2:card.addVisitor'));
    fireEvent.click(screen.getByText('visitRequestV2:card.addVisitor'));

    const table = screen.getByTestId('v2-visitors-table');
    const ordinals = () => within(table).getAllByRole('row').slice(1)
      .map(r => within(r).getAllByRole('cell')[0].textContent);
    expect(ordinals()).toEqual(['1', '2', '3']);

    // Remove the FIRST row — the remaining rows must be renumbered, not keep 2 and 3.
    // The desktop table labels this button per section (person.removeGuest / person.removeSupport);
    // card.removeRow survives only on the stacked mobile variant, which is not the table queried here.
    fireEvent.click(within(table).getAllByLabelText('visitRequestV2:person.removeGuest')[0]);
    expect(ordinals()).toEqual(['1', '2']);
  });

  it('keeps each campus card on its own member table', () => {
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);
    fireEvent.click(addCampusButton());

    // Two cards → two independent visitor tables, never one shared list.
    expect(screen.getAllByTestId('v2-visitors-table')).toHaveLength(2);
  });
});
