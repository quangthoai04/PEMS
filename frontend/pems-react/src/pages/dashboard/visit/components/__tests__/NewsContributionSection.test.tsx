/**
 * NewsContributionSection message-priority dedup (redesign spec §18/19).
 *
 * Before this change the section could stack `newsNotRequired` text, `!mediaConsentAllowed`
 * text, AND VisitNewsPostList's own generic "chưa có bài tin" empty-state simultaneously — all
 * saying the same thing three different ways. The fix picks ONE priority reason
 * (`!mediaConsentAllowed` > `newsNotRequired` > none) and suppresses the generic empty-state via
 * `VisitNewsPostList`'s new `hideEmptyState` prop — but ONLY while the list is actually empty.
 * The critical regression this guards: a blocking reason must never hide existing news posts.
 */
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import type { NewsContributionStatus, VisitNews, VisitNewsList } from '../../../../../features/delegations/types/delegations.types';

const listNews = vi.fn();

vi.mock('../../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    visitNews: {
      list: (...a: unknown[]) => listNews(...a),
    },
  },
}));

const navigate = vi.fn();
vi.mock('react-router-dom', () => ({
  useNavigate: () => navigate,
  useLocation: () => ({ pathname: '/dashboard/visit/1/contribution', search: '' }),
}));

import { NewsContributionSection } from '../NewsContributionSection';

const baseData: NewsContributionStatus = {
  hasNews: false,
  newsId: null,
  status: 'NONE',
  title: null,
  description: null,
  createdByName: null,
  updatedAt: null,
  rejectionReason: null,
  newsNotRequired: false,
  mediaConsentAllowed: true,
  canCurrentUserCreate: true,
  canCurrentUserEdit: false,
};

const post = (overrides: Partial<VisitNews> = {}): VisitNews => ({
  newsId: 1,
  visitInstanceId: 1,
  title: 'Bài viết mẫu',
  summary: 'Tóm tắt bài viết',
  body: null,
  status: 'PENDING_REVIEW',
  isPublished: false,
  authorUserId: 9,
  authorName: 'Nguyễn Văn A',
  submittedAt: '2026-08-01T00:00:00Z',
  publishedAt: null,
  reviewNote: null,
  rowVersion: 1,
  canEdit: true,
  canApprove: false,
  canReject: false,
  ...overrides,
});

const mockList = (items: VisitNews[], canCreate = true): VisitNewsList => ({
  visitInstanceId: 1,
  canView: true,
  canCreate,
  items,
});

function renderSection(data: Partial<NewsContributionStatus>, items: VisitNews[] = []) {
  listNews.mockResolvedValue(mockList(items));
  return render(
    <NewsContributionSection
      visitInstanceId="1"
      data={{ ...baseData, ...data }}
      canView
      instanceStatus="AFTER_VISIT"
      onChanged={() => {}}
    />
  );
}

describe('NewsContributionSection — message-priority dedup', () => {
  it('mediaConsentAllowed=false, empty list: only the consent notice renders', async () => {
    renderSection({ mediaConsentAllowed: false });

    expect(await screen.findByText(/khách không đồng ý truyền thông/i)).toBeInTheDocument();
    expect(screen.queryByText(/không yêu cầu bài tin tức/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/chưa có bài tin tức nào/i)).not.toBeInTheDocument();
  });

  it('newsNotRequired=true (consent allowed), empty list: only that notice renders', async () => {
    renderSection({ newsNotRequired: true, mediaConsentAllowed: true });

    expect(await screen.findByText(/không yêu cầu bài tin tức/i)).toBeInTheDocument();
    expect(screen.queryByText(/khách không đồng ý truyền thông/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/chưa có bài tin tức nào/i)).not.toBeInTheDocument();
  });

  it('no blocking flag, empty list: only the generic empty-state text renders', async () => {
    renderSection({});

    expect(await screen.findByText(/chưa có bài tin tức nào/i)).toBeInTheDocument();
    expect(screen.queryByText(/khách không đồng ý truyền thông/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/không yêu cầu bài tin tức/i)).not.toBeInTheDocument();
  });

  it('no blocking flag, list has items: posts render normally, no notices', async () => {
    renderSection({ hasNews: true }, [post()]);

    expect(await screen.findByText('Bài viết mẫu')).toBeInTheDocument();
    expect(screen.queryByText(/chưa có bài tin tức nào/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/khách không đồng ý truyền thông/i)).not.toBeInTheDocument();
  });

  it('CRITICAL REGRESSION: mediaConsentAllowed=false with existing posts — notice shows AND posts still render in full', async () => {
    renderSection(
      { mediaConsentAllowed: false, hasNews: true },
      [
        post({ newsId: 1, title: 'Bài đã duyệt', status: 'PUBLISHED', canEdit: false }),
        post({ newsId: 2, title: 'Bài chờ duyệt', status: 'PENDING_REVIEW', canEdit: true, canApprove: true }),
      ]
    );

    // Blocking notice still shown.
    expect(screen.getByText(/khách không đồng ý truyền thông/i)).toBeInTheDocument();

    // Existing posts are NOT hidden by the notice — await the async list fetch to actually settle.
    expect(await screen.findByText('Bài đã duyệt')).toBeInTheDocument();
    expect(screen.getByText('Bài chờ duyệt')).toBeInTheDocument();

    // Generic empty-state must not render (list isn't empty).
    expect(screen.queryByText(/chưa có bài tin tức nào/i)).not.toBeInTheDocument();

    // Per-post actions still follow that post's own flags, untouched by the notice.
    expect(screen.getByRole('button', { name: /sửa/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /duyệt bài/i })).toBeInTheDocument();
  });
});
