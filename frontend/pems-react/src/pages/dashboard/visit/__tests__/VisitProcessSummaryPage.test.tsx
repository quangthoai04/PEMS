/**
 * VisitProcessSummaryPage — regression coverage for the verified-gap remediation:
 *   - instance status color now covers all 9 reachable VisitInstanceStatus values (not 6/9)
 *   - the relation banner names HOST/HO/STAFF_LEADER correctly (was a 2-way HO-vs-else ternary)
 *   - participant status renders a translated label (was the raw enum), with a color for every value
 *   - logistics status renders a translated label and visually separates REJECTED/DECLINED from
 *     REQUESTED/IN_PROGRESS (was: only DONE had a distinct color, everything else read as "pending")
 *   - Minutes/Media reuse the real Contribution sections (forced isReadOnly) instead of a hardcoded
 *     "Chưa có dữ liệu được tạo." placeholder
 *   - News shows the real title/status/description when present instead of the same placeholder
 *   - Timeline renders the real VisitHistoryTimeline instead of the false
 *     "Tính năng Timeline đang được phát triển" claim
 *   - Feedback now renders real per-instance feedback (all 4 feedback types, server-authorized
 *     identically to every other section) instead of a static placeholder
 *
 * MinutesContributionSection / MediaContributionSection / VisitHistoryTimeline are mocked at the
 * module boundary, same convention VisitContributionPage.test.tsx already uses for the first two —
 * each has its own test coverage elsewhere; what this file measures is that VisitProcessSummaryPage
 * passes them the right data and forces them read-only.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

const getSummary = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitInstanceSummary: (...a: unknown[]) => getSummary(...a),
  },
}));

vi.mock('../../../../shared/hooks/useAuth', () => ({
  useAuth: () => ({ effectiveRole: 'STAFF_LEADER' }),
}));

vi.mock('react-router-dom', () => ({
  useParams: () => ({ visitInstanceId: '501' }),
  useNavigate: () => vi.fn(),
  useLocation: () => ({ state: null }),
}));

vi.mock('../components/MinutesContributionSection', () => ({
  MinutesContributionSection: (props: { data: { hasMinutes: boolean }; isReadOnly?: boolean }) => (
    <div data-testid="minutes-section" data-readonly={String(!!props.isReadOnly)} data-has-minutes={String(props.data.hasMinutes)} />
  ),
}));
vi.mock('../components/MediaContributionSection', () => ({
  MediaContributionSection: (props: { data: { uploadedCount: number }; isReadOnly?: boolean }) => (
    <div data-testid="media-section" data-readonly={String(!!props.isReadOnly)} data-uploaded-count={String(props.data.uploadedCount)} />
  ),
}));
vi.mock('../../../../features/visit-request/components/VisitHistoryTimeline', () => ({
  default: (props: { visitRequestId: number }) => (
    <div data-testid="visit-history-timeline-stub" data-visit-request-id={String(props.visitRequestId)} />
  ),
}));

import { VisitProcessSummaryPage } from '../VisitProcessSummaryPage';
import type { ProcessSummaryPage, ProcessSummaryPermission, VisitParticipantListItem } from '../../../../features/delegations/types/delegations.types';

// Typed against the real interfaces (not inferred from the object literal below) so Partial<>
// overrides in individual tests can legally set ANY field the interface declares — including
// optional ones like minutesSummary.content or newsSummary.newsId that don't appear in this base
// fixture — instead of TypeScript narrowing each field to only the shape seeded here.
const BASE_PERMISSIONS: ProcessSummaryPermission = {
  canViewSummaryPage: true,
  relation: 'STAFF_LEADER',
  canViewRequestSummary: true,
  canViewAgendaSummary: true,
  canViewParticipantSummary: true,
  canViewLogisticsSummary: true,
  canViewMinutesSummary: true,
  canViewMediaSummary: true,
  canViewNewsSummary: true,
  canViewFeedbackSummary: true,
  canViewTimeline: true,
  isReadOnly: true,
  instanceStatus: 'BEFORE_VISIT',
  campusName: 'FPTU Hà Nội',
  delegationName: 'Đoàn ABC',
  hostName: 'Trần Cảnh',
  plannedStartAt: '2026-09-01T09:00:00',
  plannedEndAt: '2026-09-01T11:00:00',
};

const BASE_PAGE: ProcessSummaryPage = {
  visitRequestId: 9001,
  permissions: BASE_PERMISSIONS,
  requestSummary: null,
  agendaSummary: [],
  participantSummary: [],
  logisticsSummary: [],
  minutesSummary: { hasMinutes: false, status: 'NOT_STARTED', canCurrentUserTakeLock: false, canCurrentUserEdit: false },
  mediaSummary: { items: [], requiredMinimumCount: 1, uploadedCount: 0, isRequirementSatisfied: false, canCurrentUserUpload: false },
  newsSummary: { hasNews: false, status: 'NOT_STARTED', newsNotRequired: false, mediaConsentAllowed: true, canCurrentUserCreate: false, canCurrentUserEdit: false },
  feedbackSummary: [],
};

function mockPage(overrides: Partial<Omit<ProcessSummaryPage, 'permissions'>> & { permissions?: Partial<ProcessSummaryPermission> } = {}) {
  return {
    ...BASE_PAGE,
    ...overrides,
    permissions: { ...BASE_PERMISSIONS, ...overrides.permissions },
  };
}

beforeEach(() => vi.clearAllMocks());

// Every SectionCard except "Thông tin yêu cầu" / "Lịch trình làm việc (Agenda)" starts collapsed
// (VisitProcessSummaryPage's own expandedSections initial state) — its body is not in the DOM at
// all until the header is clicked (SectionCard renders the body via `{isExpanded && (...)}`).
async function expandSection(title: string) {
  await userEvent.click(screen.getByText(title));
}

describe('VisitProcessSummaryPage — instance status color covers all 9 reachable statuses', () => {
  it.each([
    'WAITING_CONTACT_CONFIRMATION',
    'WAITING_REQUEST_APPROVAL',
    'ASSIGNED',
    'BEFORE_VISIT',
    'DURING_VISIT',
    'AFTER_VISIT',
    'CLOSED',
    'CANCELLED',
    'REJECTED',
  ])('%s does not fall back to the generic gray badge', async (instanceStatus) => {
    getSummary.mockResolvedValue(mockPage({ permissions: { instanceStatus } }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Đoàn ABC')).toBeInTheDocument());
    const badge = screen.getByText('Đoàn ABC').parentElement!.querySelector('span.rounded-lg')!;
    expect(badge.className).not.toContain('bg-gray-100 text-gray-800 border-gray-200');
  });
});

describe('VisitProcessSummaryPage — relation banner names the real relation', () => {
  it.each([
    ['HOST', 'Host'],
    ['HO', 'Head Office'],
    ['STAFF_LEADER', 'Staff Leader'],
  ])('relation=%s reads "%s" in the banner sentence, not always "Staff Leader"', async (relation, expectedWord) => {
    getSummary.mockResolvedValue(mockPage({ permissions: { relation } }));
    render(<VisitProcessSummaryPage />);

    // The sentence is split across text nodes by JSX interpolation ("dành cho " / {word} / " giám
    // sát...") — getByText only matches within one node, so read the containing <p>'s full text.
    const strong = await screen.findByText('Báo cáo tổng hợp (Chỉ đọc)');
    const banner = strong.closest('p')!;
    expect(banner.textContent).toContain(`dành cho ${expectedWord} giám sát`);
    // The raw badge (READ-ONLY (relation)) must still show the literal backend value, unchanged —
    // also split across nodes ("READ-ONLY (" / relation / ")").
    const badge = screen.getByText(/READ-ONLY/).closest('span')!;
    expect(badge.textContent).toContain(`READ-ONLY (${relation})`);
  });
});

describe('VisitProcessSummaryPage — participant status is translated, every reachable value has a color', () => {
  it.each([
    ['ACCEPTED', 'Đã chấp nhận'],
    ['DECLINED', 'Đã từ chối'],
    ['INVITED', 'Đã mời'],
    ['ASSIGNED', 'Đã phân công'],
    ['REMOVED', 'Đã gỡ'],
  ])('%s renders as "%s", not the raw enum', async (status, expectedLabel) => {
    getSummary.mockResolvedValue(mockPage({
      participantSummary: [{
        participantId: 1, userId: 1, email: 'a@example.com', roleCode: 'STAFF',
        fullName: 'Nguyễn Văn A', isHost: false, participantRole: 'IC_SUPPORT',
        status: status as VisitParticipantListItem['status'],
      }],
    }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Thành phần tham gia')).toBeInTheDocument());
    await expandSection('Thành phần tham gia');

    await waitFor(() => expect(screen.getByText('Nguyễn Văn A')).toBeInTheDocument());
    expect(screen.getByText(expectedLabel)).toBeInTheDocument();
    expect(screen.queryByText(status)).not.toBeInTheDocument();
  });
});

describe('VisitProcessSummaryPage — logistics status separates refused from in-progress', () => {
  it('REJECTED and IN_PROGRESS get visually distinct colors, not the same amber "pending" class', async () => {
    getSummary.mockResolvedValue(mockPage({
      logisticsSummary: [
        { logisticsItemId: 1, title: 'Phòng họp A', status: 'IN_PROGRESS', itemType: 'ROOM', departmentName: 'Facilities' },
        { logisticsItemId: 2, title: 'Xe đưa đón', status: 'REJECTED', itemType: 'TRANSPORT', departmentName: 'Facilities' },
      ],
    }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Hậu cần & Chuẩn bị')).toBeInTheDocument());
    await expandSection('Hậu cần & Chuẩn bị');

    await waitFor(() => expect(screen.getByText('Đang xử lý')).toBeInTheDocument());
    const inProgressBadge = screen.getByText('Đang xử lý');
    const rejectedBadge = screen.getByText('Từ chối');
    expect(inProgressBadge.className).not.toEqual(rejectedBadge.className);
    expect(rejectedBadge.className).toContain('rose');
    expect(inProgressBadge.className).not.toContain('rose');
  });
});

describe('VisitProcessSummaryPage — Minutes/Media reuse the real Contribution sections, forced read-only', () => {
  it('passes real backend data through and forces isReadOnly on both', async () => {
    getSummary.mockResolvedValue(mockPage({
      minutesSummary: { hasMinutes: true, status: 'COMPLETED', content: 'Nội dung biên bản thật', canCurrentUserTakeLock: false, canCurrentUserEdit: false },
      mediaSummary: { items: [], requiredMinimumCount: 1, uploadedCount: 3, isRequirementSatisfied: true, canCurrentUserUpload: false },
    }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Biên bản làm việc')).toBeInTheDocument());
    await expandSection('Biên bản làm việc');
    await expandSection('Hình ảnh / Media');

    await waitFor(() => expect(screen.getByTestId('minutes-section')).toBeInTheDocument());
    expect(screen.getByTestId('minutes-section')).toHaveAttribute('data-readonly', 'true');
    expect(screen.getByTestId('minutes-section')).toHaveAttribute('data-has-minutes', 'true');
    expect(screen.getByTestId('media-section')).toHaveAttribute('data-readonly', 'true');
    expect(screen.getByTestId('media-section')).toHaveAttribute('data-uploaded-count', '3');
  });

  it('no longer shows the old "Chưa có dữ liệu được tạo." placeholder when real minutes exist', async () => {
    getSummary.mockResolvedValue(mockPage({
      minutesSummary: { hasMinutes: true, status: 'COMPLETED', content: 'Nội dung thật', canCurrentUserTakeLock: false, canCurrentUserEdit: false },
    }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Biên bản làm việc')).toBeInTheDocument());
    await expandSection('Biên bản làm việc');

    await waitFor(() => expect(screen.getByTestId('minutes-section')).toBeInTheDocument());
    expect(screen.queryByText('Chưa có dữ liệu được tạo.')).not.toBeInTheDocument();
  });
});

describe('VisitProcessSummaryPage — News shows real content instead of the generic placeholder', () => {
  it('renders the real title/status/description when hasNews is true', async () => {
    getSummary.mockResolvedValue(mockPage({
      newsSummary: {
        hasNews: true, newsId: 77, status: 'PUBLISHED', title: 'Đoàn ABC thăm FPTU',
        description: 'Buổi làm việc thành công tốt đẹp.', createdByName: 'Trần Cảnh',
        newsNotRequired: false, mediaConsentAllowed: true, canCurrentUserCreate: false, canCurrentUserEdit: false,
      },
    }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Tin tức & Bài viết')).toBeInTheDocument());
    await expandSection('Tin tức & Bài viết');

    await waitFor(() => expect(screen.getByText('Đoàn ABC thăm FPTU')).toBeInTheDocument());
    expect(screen.getByText('Buổi làm việc thành công tốt đẹp.')).toBeInTheDocument();
    // 'Trần Cảnh' also appears in the always-visible header "Host:" badge, so scope to the News
    // section's own "Người tạo:" line rather than matching the name alone.
    expect(screen.getByText(/Người tạo:.*Trần Cảnh/)).toBeInTheDocument();
    expect(screen.queryByText('Chưa có dữ liệu được tạo.')).not.toBeInTheDocument();
  });

  it('shows a real empty state (not the fake placeholder) when hasNews is false', async () => {
    getSummary.mockResolvedValue(mockPage());
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Tin tức & Bài viết')).toBeInTheDocument());
    await expandSection('Tin tức & Bài viết');

    await waitFor(() => expect(screen.getByText('Chưa có bài tin tức nào cho chuyến thăm này.')).toBeInTheDocument());
  });
});

describe('VisitProcessSummaryPage — Timeline is real, the false "under development" claim is gone', () => {
  it('renders VisitHistoryTimeline with the real visitRequestId, not the old placeholder', async () => {
    getSummary.mockResolvedValue(mockPage({ visitRequestId: 12345 }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Lịch sử cập nhật (Timeline)')).toBeInTheDocument());
    await expandSection('Lịch sử cập nhật (Timeline)');

    await waitFor(() => expect(screen.getByTestId('visit-history-timeline-stub')).toBeInTheDocument());
    expect(screen.getByTestId('visit-history-timeline-stub')).toHaveAttribute('data-visit-request-id', '12345');
    expect(screen.queryByText('Tính năng Timeline đang được phát triển.')).not.toBeInTheDocument();
  });
});

describe('VisitProcessSummaryPage — Feedback renders real per-instance data (HO requirement)', () => {
  it('renders rating/comment/submitter/target from the real feedback DTO, using the shared type label — not a raw enum', async () => {
    getSummary.mockResolvedValue(mockPage({
      permissions: { relation: 'HO', instanceStatus: 'CLOSED' },
      feedbackSummary: [
        {
          feedbackId: 1, feedbackType: 'VISITOR_OVERALL', submitterRole: 'VISITOR',
          submitterNameSnapshot: 'Nguyễn Văn Khách', targetType: 'VISIT_INSTANCE',
          targetNameSnapshot: 'Toàn bộ đoàn khách', rating: 5, comment: 'Chuyến thăm rất tốt.',
          submittedAt: '2026-09-02T10:00:00',
        },
        {
          feedbackId: 2, feedbackType: 'HOST_PARTICIPANT', submitterRole: 'HOST',
          // Deliberately different from BASE_PERMISSIONS.hostName ('Trần Cảnh', shown in the
          // always-visible header badge) so this assertion can't accidentally match that instead.
          submitterNameSnapshot: 'Lê Văn Host', targetType: 'VISIT_PARTICIPANT',
          targetNameSnapshot: 'Phạm Thị D', rating: 4, comment: null,
          submittedAt: '2026-09-02T10:05:00',
        },
      ],
    }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Đánh giá chất lượng (Feedback)')).toBeInTheDocument());
    await expandSection('Đánh giá chất lượng (Feedback)');

    // VISITOR_OVERALL row: real target/submitter names, translated type label, real comment.
    await waitFor(() => expect(screen.getByText('Toàn bộ đoàn khách')).toBeInTheDocument());
    expect(screen.getByText(/Khách đánh giá chuyến thăm/)).toBeInTheDocument();
    expect(screen.getByText(/Nguyễn Văn Khách/)).toBeInTheDocument();
    expect(screen.getByText('Chuyến thăm rất tốt.')).toBeInTheDocument();

    // HOST_PARTICIPANT row: real target/submitter names, translated type label.
    expect(screen.getByText('Phạm Thị D')).toBeInTheDocument();
    expect(screen.getByText(/Host đánh giá bên tham gia/)).toBeInTheDocument();
    expect(screen.getByText(/Lê Văn Host/)).toBeInTheDocument();

    // Raw enum values never leak into the DOM.
    expect(screen.queryByText('VISITOR_OVERALL')).not.toBeInTheDocument();
    expect(screen.queryByText('HOST_PARTICIPANT')).not.toBeInTheDocument();

    // Read-only: the shared star rating renders disabled radio buttons, no click handler wired.
    const stars = screen.getAllByRole('radio');
    expect(stars.length).toBeGreaterThan(0);
    stars.forEach((star) => expect(star).toBeDisabled());
  });

  it('shows a genuine "no feedback yet" empty state (not the old fake placeholder) once the instance is past AFTER_VISIT', async () => {
    getSummary.mockResolvedValue(mockPage({ permissions: { instanceStatus: 'CLOSED' }, feedbackSummary: [] }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Đánh giá chất lượng (Feedback)')).toBeInTheDocument());
    await expandSection('Đánh giá chất lượng (Feedback)');

    await waitFor(() => expect(screen.getByText('Chưa có đánh giá nào cho chuyến thăm này.')).toBeInTheDocument());
    expect(screen.queryByText('Tính năng đang phát triển')).not.toBeInTheDocument();
  });

  it('keeps the "not yet eligible" message when the instance has not reached AFTER_VISIT/CLOSED', async () => {
    getSummary.mockResolvedValue(mockPage({ permissions: { instanceStatus: 'DURING_VISIT' }, feedbackSummary: [] }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Đánh giá chất lượng (Feedback)')).toBeInTheDocument());
    await expandSection('Đánh giá chất lượng (Feedback)');

    await waitFor(() =>
      expect(screen.getByText('Feedback chỉ khả dụng sau khi chuyến thăm hoàn tất.')).toBeInTheDocument(),
    );
  });

  it('never renders sibling-instance feedback — the page only ever has the one instance\'s array to render from', async () => {
    // Frontend-side proof that isolation is a backend guarantee, not a client-side filter: the
    // component has no visitInstanceId-vs-item comparison anywhere, it just maps data.feedbackSummary
    // as given — so a leak could only originate server-side (covered by the backend integration
    // tests: instance A never receives instance B's rows in the first place).
    getSummary.mockResolvedValue(mockPage({
      feedbackSummary: [{
        feedbackId: 9, feedbackType: 'VISITOR_OVERALL', submitterRole: 'VISITOR',
        submitterNameSnapshot: 'Khách A', targetType: 'VISIT_INSTANCE', targetNameSnapshot: 'Toàn bộ đoàn khách',
        rating: 5, comment: null, submittedAt: '2026-09-02T10:00:00',
      }],
    }));
    render(<VisitProcessSummaryPage />);

    await waitFor(() => expect(screen.getByText('Đánh giá chất lượng (Feedback)')).toBeInTheDocument());
    await expandSection('Đánh giá chất lượng (Feedback)');

    // 'Khách A' sits next to static label text in the same <p> ("Khách đánh giá: Khách A"), so it's
    // not an isolated text node — match with a regex (substring) instead of an exact string.
    await waitFor(() => expect(screen.getByText(/Khách A/)).toBeInTheDocument());
    expect(screen.getAllByRole('radio').length).toBe(5); // exactly one item's worth of stars (1 group of 5)
  });
});
