/**
 * VisitNewsPostList shared-component regression (redesign spec, correction round 3).
 *
 * The Contribution page redesign added two opt-in props to VisitNewsPostList — `compact` and
 * `hideEmptyState` — both default `false` specifically so the OTHER consumer, VisitNewsSection
 * (the "Sau tiếp khách" tab), keeps its current rendering behavior and styling unchanged when it
 * doesn't pass them. This asserts that contract narrowly: only the specific class fragment that
 * distinguishes "default" from "compact" sizing, and presence of the generic empty-state text —
 * not a full Tailwind class snapshot, so an unrelated future spacing tweak won't fail this test.
 */
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

const listNews = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    visitNews: {
      list: (...a: unknown[]) => listNews(...a),
    },
  },
}));

vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
  useLocation: () => ({ pathname: '/dashboard/visit/process/1', search: '' }),
}));

import { VisitNewsSection } from '../VisitNewsSection';

describe('VisitNewsSection — default VisitNewsPostList behavior unchanged', () => {
  it('renders the generic (non-compact) empty state when the news list is empty', async () => {
    listNews.mockResolvedValue({ visitInstanceId: 1, canView: true, canCreate: true, items: [] });

    render(<VisitNewsSection visitInstanceId={1} />);

    const emptyText = await screen.findByText(/chưa có bài tin tức nào cho chuyến thăm này/i);
    expect(emptyText).toBeInTheDocument();

    // Non-compact empty-state icon carries the default w-10 sizing (compact would be w-8).
    const icon = emptyText.parentElement?.querySelector('svg');
    expect(icon?.getAttribute('class') || '').toContain('w-10');
  });
});
