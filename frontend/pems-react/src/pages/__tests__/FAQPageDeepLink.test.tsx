import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { PublicFaqItem } from '../../features/public-faq/types/publicFaq.types';

const getPublicFaqs = vi.fn();
const getFaqTypeCounts = vi.fn();
const getPublicFaqDetail = vi.fn();
vi.mock('../../features/public-faq/api/publicFaqApi', () => ({
  publicFaqApi: {
    getPublicFaqs: (...a: unknown[]) => getPublicFaqs(...a),
    getFaqTypeCounts: (...a: unknown[]) => getFaqTypeCounts(...a),
    getPublicFaqDetail: (...a: unknown[]) => getPublicFaqDetail(...a),
  },
}));

// The visit-registration CTA is a whole feature of its own (capability probe + modal); stub it so
// these tests exercise the deep link and nothing else.
vi.mock('../../shared/features/VisitEntrySurfaces', () => ({ VisitEntrySurfaces: () => null }));
vi.mock('../../shared/features/useVisitEntryCta', () => ({
  useVisitEntryCta: () => ({ trigger: vi.fn() }),
}));

import { FAQPage } from '../FAQPage';
import i18n from '../../shared/i18n/config';

const faq = (faqId: number, question: string, faqType = 'VISIT_REQUEST'): PublicFaqItem => ({
  faqId,
  faqType,
  faqTypeLabel: 'Visit Registration',
  question,
  answer: `Answer for ${question}`,
  displayOrder: faqId,
  createdAt: '2026-01-01T00:00:00Z',
});

// The linked FAQ is deliberately NOT in the list page the component loads — that is the case the
// deep link exists for (a search hit that lives on page 3 of the paginated list).
const listFaq = faq(1, 'Listed question');
const linkedFaq = faq(42, 'Deep linked question', 'LOGISTICS_RESOURCE');

function renderAt(path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <FAQPage />
    </MemoryRouter>,
  );
}

describe('FAQPage deep link (/faq?faqId=)', () => {
  beforeEach(async () => {
    getPublicFaqs.mockReset();
    getFaqTypeCounts.mockReset();
    getPublicFaqDetail.mockReset();
    getPublicFaqs.mockResolvedValue({
      items: [listFaq], page: 1, pageSize: 10, totalItems: 1, totalPages: 1,
      hasNextPage: false, hasPreviousPage: false,
    });
    getFaqTypeCounts.mockResolvedValue([
      { value: 'VISIT_REQUEST', label: 'Visit Registration', count: 1 },
      { value: 'LOGISTICS_RESOURCE', label: 'Logistics & Resources', count: 1 },
    ]);
    getPublicFaqDetail.mockResolvedValue(linkedFaq);
    Element.prototype.scrollIntoView = vi.fn();
    await act(async () => { await i18n.changeLanguage('en'); });
  });

  it('does not fetch a single FAQ when there is no faqId', async () => {
    renderAt('/faq');
    await waitFor(() => expect(getPublicFaqs).toHaveBeenCalled());

    expect(getPublicFaqDetail).not.toHaveBeenCalled();
  });

  it('fetches, shows and opens the linked FAQ even though it is not on the loaded page', async () => {
    renderAt('/faq?faqId=42');

    expect(await screen.findByText('Deep linked question')).toBeInTheDocument();
    expect(getPublicFaqDetail).toHaveBeenCalledWith(42, expect.stringMatching(/^en/));
    // Opened, i.e. its answer is rendered — not merely present and collapsed.
    expect(await screen.findByText('Answer for Deep linked question')).toBeInTheDocument();
  });

  it('requests the linked FAQ in the current site language', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    renderAt('/faq?faqId=42');

    await waitFor(() => expect(getPublicFaqDetail).toHaveBeenCalled());
    expect(getPublicFaqDetail).toHaveBeenCalledWith(42, expect.stringMatching(/^vi/));
  });

  it('scrolls the linked FAQ into view once it has rendered', async () => {
    renderAt('/faq?faqId=42');
    await screen.findByText('Deep linked question');

    await waitFor(() => expect(Element.prototype.scrollIntoView).toHaveBeenCalled());
  });

  it('keeps the page usable and explains when the linked FAQ is no longer public', async () => {
    getPublicFaqDetail.mockRejectedValue(new Error('404'));
    renderAt('/faq?faqId=42');

    expect(await screen.findByText(/no longer publicly available/i)).toBeInTheDocument();
    // The rest of the FAQ page still works. (getAllBy: the list FAQ also appears under "Suggested".)
    expect(screen.getAllByText('Listed question').length).toBeGreaterThan(0);
  });

  it('drops the linked FAQ once the visitor filters by a topic themselves', async () => {
    renderAt('/faq?faqId=42');
    expect(await screen.findByText('Deep linked question')).toBeInTheDocument();

    // Any manual filtering means they have moved on — the pinned FAQ must not linger or re-open.
    fireEvent.click(screen.getAllByRole('button', { name: /All topics|All/i })[0]);

    await waitFor(() => expect(screen.queryByText('Deep linked question')).toBeNull());
  });

  it('drops the linked FAQ once the visitor types their own search', async () => {
    renderAt('/faq?faqId=42');
    expect(await screen.findByText('Deep linked question')).toBeInTheDocument();

    fireEvent.change(screen.getByPlaceholderText(/Enter keywords/i), { target: { value: 'something else' } });

    await waitFor(() => expect(screen.queryByText('Deep linked question')).toBeNull());
  });
});
