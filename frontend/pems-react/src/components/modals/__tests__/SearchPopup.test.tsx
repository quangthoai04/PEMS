import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import type { SearchInformationResult } from '../../../features/public-search/types/publicSearch.types';

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({ useNavigate: () => navigateMock }));

const searchMock = vi.fn();
vi.mock('../../../features/public-search/api/publicSearchApi', () => ({
  publicSearchApi: { search: (...args: unknown[]) => searchMock(...args) },
}));

// These three modules powered the removed suggestion chips. Importing them here is deliberate: if
// SearchPopup ever calls them again, these spies catch it — asserting on the absence of a network
// call, not merely on the absence of chips in the DOM.
const getActiveCampuses = vi.fn();
const getPublicPartnerTypes = vi.fn();
const getFaqTypeCounts = vi.fn();
vi.mock('../../../features/authentication/api/authenticationApi', () => ({
  authenticationApi: { getActiveCampuses: (...a: unknown[]) => getActiveCampuses(...a) },
}));
vi.mock('../../../features/public-partners/api/publicPartnersApi', () => ({
  publicPartnersApi: { getPublicPartnerTypes: (...a: unknown[]) => getPublicPartnerTypes(...a) },
}));
vi.mock('../../../features/public-faq/api/publicFaqApi', () => ({
  publicFaqApi: { getFaqTypeCounts: (...a: unknown[]) => getFaqTypeCounts(...a) },
}));

import { SearchPopup } from '../SearchPopup';
import i18n from '../../../shared/i18n/config';

const emptyResult: SearchInformationResult = {
  news: [], partners: [], galleries: [], faqs: [],
  hasMore: { news: false, partners: false, galleries: false, faqs: false },
  totalCount: 0,
};

function resultWith(overrides: Partial<SearchInformationResult>): SearchInformationResult {
  const merged = { ...emptyResult, ...overrides };
  return {
    ...merged,
    totalCount:
      merged.news.length + merged.partners.length + merged.galleries.length + merged.faqs.length,
  };
}

const newsHit = { newsId: 12, title: 'FPTU opening ceremony', summary: 'A summary', publishedAt: '2026-08-05T00:00:00Z' };
const partnerHit = { partnerId: 7, name: 'Acme Corporation', descriptionPreview: 'Equipment supplier', country: 'Vietnam', publicSlug: 'acme-corporation' };
const galleryHit = {
  galleryItemId: 88, title: 'Central Library', descriptionPreview: 'Reading space',
  campusCode: 'HN', campusName: 'FPTU Hanoi', areaId: 3, areaName: 'Study Area',
  locationId: 12, locationName: 'Library', mediaKind: 'IMAGE', thumbnailUrl: '/api/public/visit-fptu/media/100/content',
};
const faqHit = { faqId: 5, question: 'How do I register a visit?', answerPreview: 'Open the form.', faqType: 'VISIT_REQUEST', faqTypeLabel: 'Visit Registration' };

/** Advances past the 350 ms debounce and lets the resolved promise flush. */
async function runDebounce() {
  await act(async () => {
    vi.advanceTimersByTime(400);
  });
}

/**
 * Result rows are queried by accessible name, not by text: keyword highlighting wraps the matched
 * part in <mark>, which splits the title across elements and makes getByText('FPTU opening ceremony')
 * miss. The accessible name concatenates the descendants, so it sees the row as a reader does — and a
 * queryBy* absence check is only trustworthy if it would have found the row when present.
 */
const findRow = (name: RegExp) => screen.findByRole('button', { name });
const queryRow = (name: RegExp) => screen.queryByRole('button', { name });

describe('SearchPopup', () => {
  beforeEach(async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    navigateMock.mockClear();
    searchMock.mockReset();
    searchMock.mockResolvedValue(emptyResult);
    getActiveCampuses.mockClear();
    getPublicPartnerTypes.mockClear();
    getFaqTypeCounts.mockClear();
    await act(async () => { await i18n.changeLanguage('en'); });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  // ── Removed surfaces ───────────────────────────────────────────────────────────────

  it('makes no network call at all when it opens', async () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);
    await runDebounce();

    expect(searchMock).not.toHaveBeenCalled();
    expect(getActiveCampuses).not.toHaveBeenCalled();
    expect(getPublicPartnerTypes).not.toHaveBeenCalled();
    expect(getFaqTypeCounts).not.toHaveBeenCalled();
  });

  it('shows the initial hint instead of popular suggestions', () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    expect(screen.getByText(/Enter a keyword to search News, Partners, Gallery and FAQs/i)).toBeInTheDocument();
    expect(screen.queryByText(/Popular Keywords/i)).toBeNull();
  });

  it('keeps the five campus contacts reachable, as dial and compose links', () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    expect(screen.getByText(/Contact information/i)).toBeInTheDocument();
    for (const email of [
      'tuyensinhhanoi@fpt.edu.vn', 'tuyensinhhcm@fpt.edu.vn', 'tuyensinhdanang@fpt.edu.vn',
      'tuyensinhcantho@fpt.edu.vn', 'tuyensinhquynhon@fpt.edu.vn',
    ]) {
      expect(screen.getByText(email).closest('a')).toHaveAttribute('href', `mailto:${email}`);
    }
    // Hotlines dial rather than sitting there as plain text.
    expect(screen.getByText('(024) 7300 5588').closest('a')).toHaveAttribute('href', 'tel:02473005588');
  });

  it('still shows the contacts alongside results, below them', async () => {
    searchMock.mockResolvedValue(resultWith({ news: [newsHit] }));
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();
    await findRow(/FPTU opening ceremony/);

    expect(screen.getByText(/Contact information/i)).toBeInTheDocument();
  });

  it('renders no Campus section even when every other section has hits', async () => {
    searchMock.mockResolvedValue(resultWith({
      news: [newsHit], partners: [partnerHit], galleries: [galleryHit], faqs: [faqHit],
    }));
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();

    expect(await screen.findByText('News')).toBeInTheDocument();
    expect(screen.getByText('Gallery')).toBeInTheDocument();
    expect(screen.queryByText(/Campuses/i)).toBeNull();
  });

  // ── Query behaviour ────────────────────────────────────────────────────────────────

  it('does not call the API for an empty or whitespace-only keyword', async () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);
    const input = screen.getByLabelText('Search');

    fireEvent.change(input, { target: { value: '   ' } });
    await runDebounce();

    expect(searchMock).not.toHaveBeenCalled();
  });

  it('debounces typing into a single request', async () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);
    const input = screen.getByLabelText('Search');

    fireEvent.change(input, { target: { value: 'f' } });
    act(() => { vi.advanceTimersByTime(100); });
    fireEvent.change(input, { target: { value: 'fp' } });
    act(() => { vi.advanceTimersByTime(100); });
    fireEvent.change(input, { target: { value: 'fptu' } });

    expect(searchMock).not.toHaveBeenCalled(); // still inside the debounce window
    await runDebounce();

    expect(searchMock).toHaveBeenCalledTimes(1);
    expect(searchMock.mock.calls[0][0]).toMatchObject({ keyword: 'fptu', limit: 5, languageCode: 'en' });
  });

  it('searches immediately on Enter', async () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);
    const input = screen.getByLabelText('Search');

    fireEvent.change(input, { target: { value: 'fptu' } });
    fireEvent.keyDown(input, { key: 'Enter' });
    await act(async () => {});

    expect(searchMock).toHaveBeenCalledTimes(1);
  });

  it('clearing the input drops the results and asks for nothing more', async () => {
    searchMock.mockResolvedValue(resultWith({ news: [newsHit] }));
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();
    expect(await findRow(/FPTU opening ceremony/)).toBeInTheDocument();

    fireEvent.click(screen.getByLabelText('Clear keyword'));
    await runDebounce();

    expect(queryRow(/FPTU opening ceremony/)).toBeNull();
    expect(screen.getByText(/Enter a keyword to search/i)).toBeInTheDocument();
  });

  // ── States ─────────────────────────────────────────────────────────────────────────

  it('shows a skeleton while loading, not a bare spinner', async () => {
    searchMock.mockImplementation(() => new Promise(() => {})); // never settles
    const { container } = render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();

    expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThanOrEqual(4);
  });

  it('shows the empty state with the keyword', async () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'zzzz' } });
    await runDebounce();

    expect(await screen.findByText(/No results found for "zzzz"/i)).toBeInTheDocument();
    expect(screen.getByText(/Try a shorter or different keyword/i)).toBeInTheDocument();
  });

  it('shows the empty state in Vietnamese when the site is Vietnamese', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Tìm kiếm'), { target: { value: 'zzzz' } });
    await runDebounce();

    expect(await screen.findByText(/Không tìm thấy nội dung phù hợp với "zzzz"/i)).toBeInTheDocument();
    expect(searchMock.mock.calls[0][0]).toMatchObject({ languageCode: 'vi' });
  });

  it('offers a retry after a failure and re-requests when it is used', async () => {
    searchMock.mockRejectedValueOnce(new Error('network'));
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();

    expect(await screen.findByText(/Search is temporarily unavailable/i)).toBeInTheDocument();

    searchMock.mockResolvedValue(resultWith({ news: [newsHit] }));
    fireEvent.click(screen.getByRole('button', { name: /Try again/i }));
    await runDebounce();

    expect(await findRow(/FPTU opening ceremony/)).toBeInTheDocument();
  });

  // ── Result links ───────────────────────────────────────────────────────────────────

  it.each([
    ['News', /FPTU opening ceremony/, '/news/12', { news: [newsHit] }],
    ['Partner', /Acme Corporation/, '/partners/acme-corporation', { partners: [partnerHit] }],
    ['FAQ', /How do I register a visit/, '/faq?faqId=5', { faqs: [faqHit] }],
    ['Gallery', /Central Library/, '/visit-fptu/hn?locationId=12&itemId=88', { galleries: [galleryHit] }],
  ])('clicking a %s result navigates to its own content', async (_label, text, expectedUrl, payload) => {
    const onClose = vi.fn();
    searchMock.mockResolvedValue(resultWith(payload as Partial<SearchInformationResult>));
    render(<SearchPopup isOpen onClose={onClose} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();

    fireEvent.click(await findRow(text));

    expect(navigateMock).toHaveBeenCalledWith(expectedUrl);
    expect(onClose).toHaveBeenCalled();
  });

  it('falls back to the partner id when it has no public slug', async () => {
    searchMock.mockResolvedValue(resultWith({ partners: [{ ...partnerHit, publicSlug: null }] }));
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'acme' } });
    await runDebounce();
    fireEvent.click(await findRow(/Acme Corporation/));

    expect(navigateMock).toHaveBeenCalledWith('/partners/7');
  });

  // ── Partner "view more" CTA ────────────────────────────────────────────────────────

  it('hides the partner CTA when there are partner hits but no more of them', async () => {
    searchMock.mockResolvedValue(resultWith({ partners: [partnerHit] }));
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'acme' } });
    await runDebounce();
    await findRow(/Acme Corporation/);

    expect(queryRow(/View more related partners/i)).toBeNull();
  });

  it('hides the partner CTA when only other sections matched', async () => {
    // The old bug: the CTA sat in the result container's footer, so a news-only search still
    // offered "view more related partners".
    searchMock.mockResolvedValue(resultWith({ news: [newsHit], faqs: [faqHit], galleries: [galleryHit] }));
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();
    await findRow(/FPTU opening ceremony/);

    expect(queryRow(/View more related partners/i)).toBeNull();
  });

  it('shows the partner CTA only when more partners exist, and links to the filtered list', async () => {
    searchMock.mockResolvedValue({
      ...resultWith({ partners: [partnerHit] }),
      hasMore: { news: false, partners: true, galleries: false, faqs: false },
    });
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'acme corp' } });
    await runDebounce();

    fireEvent.click(await screen.findByText(/View more related partners/i));

    expect(navigateMock).toHaveBeenCalledWith('/partners?search=acme%20corp');
  });

  // ── Language ───────────────────────────────────────────────────────────────────────

  it('refetches in the new language and does not let the old response overwrite it', async () => {
    let resolveVi: (v: SearchInformationResult) => void = () => {};
    searchMock.mockImplementationOnce(
      () => new Promise<SearchInformationResult>((res) => { resolveVi = res; }),
    );

    await act(async () => { await i18n.changeLanguage('vi'); });
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Tìm kiếm'), { target: { value: 'robotics' } });
    await runDebounce();
    expect(searchMock).toHaveBeenCalledTimes(1);

    // Switch to EN while the VI request is still in flight.
    searchMock.mockResolvedValue(resultWith({ news: [{ ...newsHit, title: 'Robotics Centre' }] }));
    await act(async () => { await i18n.changeLanguage('en'); });
    await runDebounce();

    // The stale VI response lands late and must be ignored.
    await act(async () => {
      resolveVi(resultWith({ news: [{ ...newsHit, newsId: 99, title: 'Trung tâm Robotics' }] }));
    });

    expect(await findRow(/Robotics Centre/)).toBeInTheDocument();
    expect(queryRow(/Trung tâm Robotics/)).toBeNull();
    expect(searchMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ keyword: 'robotics', languageCode: 'en' }),
      expect.anything(),
    );
  });

  it('sends the bare language code, never a regional tag', async () => {
    await act(async () => { await i18n.changeLanguage('en-US'); });
    render(<SearchPopup isOpen onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();

    expect(searchMock.mock.calls[0][0]).toMatchObject({ languageCode: 'en' });
  });

  it('formats dates for the active locale', async () => {
    searchMock.mockResolvedValue(resultWith({ news: [newsHit] }));
    const { unmount } = render(<SearchPopup isOpen onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText('Search'), { target: { value: 'fptu' } });
    await runDebounce();
    expect(await screen.findByText('Aug 5, 2026')).toBeInTheDocument();
    unmount();

    await act(async () => { await i18n.changeLanguage('vi'); });
    render(<SearchPopup isOpen onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText('Tìm kiếm'), { target: { value: 'fptu' } });
    await runDebounce();
    expect(await screen.findByText('05/08/2026')).toBeInTheDocument();
  });

  // ── Accessibility / shell ──────────────────────────────────────────────────────────

  it('focuses the input on open and labels its close button', async () => {
    render(<SearchPopup isOpen onClose={vi.fn()} />);
    const closeButton = screen.getByLabelText('Close search');

    expect(closeButton).toHaveAttribute('aria-label', 'Close search');
    await waitFor(() => expect(screen.getByLabelText('Search')).toHaveFocus());
  });

  it('closes on Escape', () => {
    const onClose = vi.fn();
    render(<SearchPopup isOpen onClose={onClose} />);

    fireEvent.keyDown(document, { key: 'Escape' });

    expect(onClose).toHaveBeenCalled();
  });

  it('renders nothing when closed', () => {
    render(<SearchPopup isOpen={false} onClose={vi.fn()} />);

    expect(screen.queryByLabelText('Search')).toBeNull();
  });
});
