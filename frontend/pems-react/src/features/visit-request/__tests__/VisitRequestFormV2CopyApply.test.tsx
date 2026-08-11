import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';

/**
 * Pins a real regression: `useFieldArray.update()`/`.replace()` (used by the "Sao chép nội dung
 * từ" / "Áp dụng cho cơ sở khác" actions) patch the underlying RHF form values correctly, but
 * react-hook-form deliberately skips resyncing `register()`-bound inputs and any NESTED
 * `useFieldArray` (visitors/supportTeam, each its own field array scoped inside a campus card)
 * on `update`/`replace` — only `Controller`-bound fields re-render on their own. Without a forced
 * remount, the copy/apply looks like a no-op: the data is there in `form.getValues()` (which is
 * all the older hook-level tests checked) but the screen keeps showing the pre-copy content.
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

const addCampusButton = () => screen.getByTestId('v2-add-campus');

const campusSelects = () =>
  screen.getAllByRole('combobox').filter(el =>
    el.tagName === 'SELECT'
    && Array.from((el as HTMLSelectElement).options).some(o => CAMPUSES.some(c => c.campusCode === o.value)));

const visitTypeSelects = () =>
  screen.getAllByRole('combobox').filter(el =>
    el.tagName === 'SELECT'
    && Array.from((el as HTMLSelectElement).options).some(o => o.value === 'MEETING'));

const delegationInputs = () => screen.getAllByTestId('campus-delegation-input');

describe('VisitRequestFormV2 — copy / apply-to-all actually reach the screen', () => {
  beforeEach(() => {
    campusesMock.mockReset();
    campusesMock.mockReturnValue({ campuses: CAMPUSES, loading: false });
  });

  // Renders 2 full campus cards and fires several sequential events each — under a full-suite
  // run (many files/workers competing for CPU) that can outrun the 5s default timeout.
  it('"Sao chép nội dung từ" fills the register()-bound select AND the nested visitor rows on the target card', async () => {
    const { container } = render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    // Each visitor row renders TWICE in the DOM (a desktop table row + a mobile card, toggled by
    // CSS, both present in jsdom) — scope to the desktop table per campus card so the same
    // `data-testid` doesn't collide across that responsive duplication.
    const visitorFullName = (cardIndex: number, rowIndex: number) =>
      within(screen.getAllByTestId('v2-visitors-table')[cardIndex]).getByTestId(`visitors-${rowIndex}-fullName`);

    // Card 1: pick a campus (so its copy-source label is stable), a distinguishable visit type
    // (register()-bound <select>, NOT a Controller), and two visitor rows (a nested field array).
    fireEvent.change(campusSelects()[0], { target: { value: 'HN' } });
    fireEvent.change(visitTypeSelects()[0], { target: { value: 'MEETING' } });
    fireEvent.change(delegationInputs()[0], { target: { value: 'Đoàn A' } });

    fireEvent.click(screen.getAllByRole('button', { name: /card\.addVisitor/ })[0]);
    fireEvent.change(visitorFullName(0, 0), { target: { value: 'Khách 1' } });
    fireEvent.change(visitorFullName(0, 1), { target: { value: 'Khách 2' } });

    // Card 2: a fresh, empty card — starts with exactly one blank visitor row.
    fireEvent.click(addCampusButton());
    expect(() => visitorFullName(1, 1)).toThrow(); // card 2 has no second row yet

    const copySelect = container.querySelector('select[id^="copy-src-"]') as HTMLSelectElement;
    expect(copySelect).toBeTruthy();
    fireEvent.change(copySelect, { target: { value: '0' } });

    await waitFor(() => {
      expect(delegationInputs()[1]).toHaveValue('Đoàn A');
      expect(visitTypeSelects()[1]).toHaveValue('MEETING');
      expect(visitorFullName(1, 0)).toHaveValue('Khách 1');
      expect(visitorFullName(1, 1)).toHaveValue('Khách 2'); // card 2 grew a second row from the copy
    });

    // Campus + schedule are identity, never overwritten by a content copy.
    expect(campusSelects()[1]).toHaveValue('');
  });

  it('"Áp dụng cho cơ sở khác" (confirmed) reaches every other card on screen, not just form state', async () => {
    render(<VisitRequestFormV2 mode="public" onSuccess={vi.fn()} />);

    fireEvent.change(campusSelects()[0], { target: { value: 'HN' } });
    fireEvent.change(visitTypeSelects()[0], { target: { value: 'MEETING' } });
    fireEvent.change(delegationInputs()[0], { target: { value: 'Đoàn A' } });

    fireEvent.click(addCampusButton());
    fireEvent.click(addCampusButton());

    // "Áp dụng cho cơ sở khác" on card 1 pushes its content to every other card.
    fireEvent.click(screen.getAllByRole('button', { name: 'visitRequestV2:card.applyToAll' })[0]);
    fireEvent.click(screen.getByRole('button', { name: 'visitRequestV2:applyAll.confirm' }));

    await waitFor(() => {
      const inputs = delegationInputs();
      expect(inputs[1]).toHaveValue('Đoàn A');
      expect(inputs[2]).toHaveValue('Đoàn A');
      const selects = visitTypeSelects();
      expect(selects[1]).toHaveValue('MEETING');
      expect(selects[2]).toHaveValue('MEETING');
    });
  });
});
