import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

const getPublicPartners = vi.fn();
const getPublicPartnerCountries = vi.fn();
const getPublicPartnerTypes = vi.fn();
vi.mock('../../features/public-partners/api/publicPartnersApi', () => ({
  publicPartnersApi: {
    getPublicPartners: (...a: unknown[]) => getPublicPartners(...a),
    getPublicPartnerCountries: (...a: unknown[]) => getPublicPartnerCountries(...a),
    getPublicPartnerTypes: (...a: unknown[]) => getPublicPartnerTypes(...a),
  },
}));

vi.mock('../../shared/features/VisitEntrySurfaces', () => ({ VisitEntrySurfaces: () => null }));
vi.mock('../../shared/features/useVisitEntryCta', () => ({
  useVisitEntryCta: () => ({ trigger: vi.fn() }),
}));

import { PartnersPage } from '../PartnersPage';
import i18n from '../../shared/i18n/config';

describe('PartnersPage ?search= deep link', () => {
  beforeEach(async () => {
    getPublicPartners.mockReset().mockResolvedValue({ items: [], totalCount: 0 });
    getPublicPartnerCountries.mockReset().mockResolvedValue([]);
    getPublicPartnerTypes.mockReset().mockResolvedValue([]);
    await act(async () => { await i18n.changeLanguage('en'); });
  });

  it('hydrates the search box from the URL and filters the list by it', async () => {
    // This is where the search popup's "view more related partners" lands — sending the visitor to a
    // URL whose parameter the page ignored would show them the unfiltered list instead.
    render(
      <MemoryRouter initialEntries={['/partners?search=acme%20corp']}>
        <PartnersPage />
      </MemoryRouter>,
    );

    expect(await screen.findByDisplayValue('acme corp')).toBeInTheDocument();
    await waitFor(() =>
      expect(getPublicPartners).toHaveBeenCalledWith(expect.objectContaining({ search: 'acme corp' })),
    );
  });

  it('sends no search filter when the URL carries none', async () => {
    render(
      <MemoryRouter initialEntries={['/partners']}>
        <PartnersPage />
      </MemoryRouter>,
    );

    await waitFor(() => expect(getPublicPartners).toHaveBeenCalled());
    // The list call for the grid (not the pageSize:1 metrics probe) asks for no search term.
    const listCalls = getPublicPartners.mock.calls
      .map((c) => c[0])
      .filter((p) => p && p.pageSize !== 1);
    expect(listCalls.length).toBeGreaterThan(0);
    expect(listCalls.every((p) => p.search === undefined)).toBe(true);
  });
});
