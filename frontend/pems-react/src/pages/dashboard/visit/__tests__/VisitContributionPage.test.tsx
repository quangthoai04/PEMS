/**
 * VisitContributionPage — layout redesign regression.
 *
 * The page used to list read-only reference sections (Thông tin yêu cầu / Lịch trình / Thành
 * phần tham gia / Hậu cần) BEFORE the actual contribution workspace (Biên bản / Ảnh đoàn khách /
 * Tin tức), forcing users to scroll past everything they didn't come for. The redesign moves the
 * contribution stack to right under the header, in fixed order Biên bản → Ảnh đoàn khách → Tin
 * tức, full width (no 3-column grid), with the read-only groups demoted underneath. This asserts
 * the new DOM order and confirms permission gating and empty-state text weren't disturbed by the
 * reorder — no `perm.*`/`data.*` semantics are touched by this redesign.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';

const getContribution = vi.fn();

vi.mock('../../../../features/delegations/api/delegationsApi', () => ({
  delegationsApi: {
    getVisitInstanceContribution: (...a: unknown[]) => getContribution(...a),
  },
}));

vi.mock('../../../../shared/auth/AuthContext', () => ({
  useAuthContext: () => ({ effectiveRole: 'HOST' }),
}));

vi.mock('react-router-dom', () => ({
  useParams: () => ({ visitInstanceId: '501' }),
  useNavigate: () => vi.fn(),
  useLocation: () => ({ state: null }),
}));

// The 3 real contribution subcomponents each fetch their own data (MinutesCard/VisitPhotoPanel/
// VisitNewsPostList) — irrelevant to what this file measures (page-level ordering/gating), and
// their own behavior is covered by NewsContributionSection.test.tsx and the components themselves.
vi.mock('../components/MinutesContributionSection', () => ({
  MinutesContributionSection: ({ canView }: { canView: boolean }) =>
    canView ? <div data-testid="minutes-section">Biên bản (stub)</div> : null,
}));
vi.mock('../components/MediaContributionSection', () => ({
  MediaContributionSection: ({ canView }: { canView: boolean }) =>
    canView ? <div data-testid="media-section">Ảnh đoàn khách (stub)</div> : null,
}));
vi.mock('../components/NewsContributionSection', () => ({
  NewsContributionSection: ({ canView }: { canView: boolean }) =>
    canView ? <div data-testid="news-section">Tin tức (stub)</div> : null,
}));

import { VisitContributionPage } from '../VisitContributionPage';

const FULL_PERMISSIONS = {
  canViewContributionPage: true,
  relation: 'HOST',
  canViewRequestSummary: true,
  canViewAgendaSummary: true,
  canViewParticipantSummary: true,
  canViewLogisticsSummary: true,
  canViewRelatedLogisticsOnly: false,
  canViewFullLogisticsSummary: true,
  canViewMinutes: true,
  canEditMinutes: true,
  canViewMedia: true,
  canUploadMedia: true,
  canViewNews: true,
  canCreateNews: true,
  canEditNews: true,
  isReadOnly: false,
};

const SUMMARY = {
  visitRequestId: 9001,
  visitInstanceId: 501,
  delegationName: 'University of Queensland Partnership Review',
  requestStatus: 'APPROVED',
  instanceStatus: 'AFTER_VISIT',
  plannedStartAt: '2026-07-30T09:00:00',
  plannedEndAt: '2026-07-30T11:00:00',
  campusName: 'FPTU Hà Nội',
  hostName: 'Trần Cảnh',
  guestCount: 1,
  request: {
    delegationName: 'University of Queensland Partnership Review',
    registrantOrganization: 'University of Queensland',
    visitType: 'MEETING',
    visitTypeOther: null as string | null,
    purpose: 'Hợp tác đào tạo',
    workingLanguage: 'EN',
  },
  agenda: [] as Array<{
    agendaId: number;
    title: string;
    startTime: string;
    endTime?: string | null;
    location?: string | null;
    responsibleName?: string | null;
    templateResponsibleRoleLabel?: string | null;
  }>,
  participants: [],
  logistics: [],
};

const PARTICIPANTS = [
  { participantId: 1, fullName: 'Trần Cảnh', participantRole: 'IC_HOST', isHost: true, status: 'ASSIGNED' },
  { participantId: 2, fullName: 'Nguyễn Văn B', participantRole: 'DEPT_SUPPORT', isHost: false, status: 'ACCEPTED' },
  { participantId: 3, fullName: 'Department Lead Đào tạo HN', participantRole: 'DEPT_SUPPORT', isHost: false, status: 'DECLINED' },
  { participantId: 4, fullName: 'Phạm Thị D', participantRole: 'STUDENT', isHost: false, status: 'INVITED' },
  { participantId: 5, fullName: 'Lê Văn E', participantRole: 'IC_SUPPORT', isHost: false, status: 'REMOVED' },
];

const WORKSPACE = {
  minutes: { hasMinutes: true, status: 'DRAFT', canCurrentUserTakeLock: false, canCurrentUserEdit: true },
  media: { items: [], requiredMinimumCount: 3, uploadedCount: 0, isRequirementSatisfied: false, canCurrentUserUpload: true },
  news: { hasNews: false, status: 'NONE', newsNotRequired: false, mediaConsentAllowed: true, canCurrentUserCreate: true, canCurrentUserEdit: false },
};

const mockPage = (overrides: {
  permissions?: Partial<typeof FULL_PERMISSIONS>;
  summary?: Partial<typeof SUMMARY>;
} = {}) => ({
  permissions: { ...FULL_PERMISSIONS, ...overrides.permissions },
  summary: { ...SUMMARY, ...overrides.summary },
  workspace: WORKSPACE,
});

beforeEach(() => {
  vi.clearAllMocks();
});

describe('VisitContributionPage — contribution stack promoted above reference info', () => {
  it('renders Biên bản → Ảnh đoàn khách → Tin tức, all before Thông tin yêu cầu', async () => {
    getContribution.mockResolvedValue(mockPage());
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByTestId('minutes-section')).toBeInTheDocument());

    // compareDocumentPosition tells us A comes before B in DOM order.
    const contributionHeading = screen.getByText('Đóng góp của bạn');
    const minutes = screen.getByTestId('minutes-section');
    const media = screen.getByTestId('media-section');
    const news = screen.getByTestId('news-section');
    const infoHeading = screen.getByText('Thông tin chuyến thăm');
    const requestInfo = screen.getByText('Thông tin yêu cầu');

    const before = (a: Element, b: Element) =>
      !!(a.compareDocumentPosition(b) & Node.DOCUMENT_POSITION_FOLLOWING);

    expect(before(contributionHeading, minutes)).toBe(true);
    expect(before(minutes, media)).toBe(true);
    expect(before(media, news)).toBe(true);
    expect(before(news, infoHeading)).toBe(true);
    expect(before(infoHeading, requestInfo)).toBe(true);
  });

  it('gates each contribution module on its own canView flag, unaffected by the reorder', async () => {
    getContribution.mockResolvedValue(mockPage({ permissions: { canViewMedia: false } }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByTestId('minutes-section')).toBeInTheDocument());
    expect(screen.queryByTestId('media-section')).not.toBeInTheDocument();
    expect(screen.getByTestId('news-section')).toBeInTheDocument();
  });

  it('shows the closed/read-only notice as a compact chip when isReadOnly is true', async () => {
    getContribution.mockResolvedValue(mockPage({ permissions: { isReadOnly: true } }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByTestId('minutes-section')).toBeInTheDocument());
    expect(screen.getByText(/chuyến thăm đã đóng\/hủy/i)).toBeInTheDocument();
  });

  it('hides the closed/read-only notice when the instance is not read-only', async () => {
    getContribution.mockResolvedValue(mockPage());
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByTestId('minutes-section')).toBeInTheDocument());
    expect(screen.queryByText(/chuyến thăm đã đóng\/hủy/i)).not.toBeInTheDocument();
  });

  it('shows a single "no data" line per empty read-only section (no duplication)', async () => {
    getContribution.mockResolvedValue(mockPage());
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Lịch trình')).toBeInTheDocument());
    // Agenda + participants + logistics are all empty in SUMMARY — each renders its own single
    // "Chưa có dữ liệu cho phần này." line; there must be exactly 3 (one per empty section).
    expect(screen.getAllByText('Chưa có dữ liệu cho phần này.')).toHaveLength(3);
  });

  it('renders visitType/workingLanguage as friendly labels, never the raw enum code', async () => {
    getContribution.mockResolvedValue(mockPage());
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Loại chuyến thăm')).toBeInTheDocument());
    // MEETING → "Họp trao đổi", EN → "Tiếng Anh" — canonical labels reused from RequestInfoReadOnly.
    expect(screen.getByText('Họp trao đổi')).toBeInTheDocument();
    expect(screen.getByText('Tiếng Anh')).toBeInTheDocument();
    expect(screen.queryByText('MEETING')).not.toBeInTheDocument();
    expect(screen.queryByText('EN')).not.toBeInTheDocument();
  });

  it('maps WORKSHOP visitType and VI workingLanguage to friendly labels', async () => {
    getContribution.mockResolvedValue(mockPage({
      summary: { request: { ...SUMMARY.request, visitType: 'WORKSHOP', workingLanguage: 'VI' } },
    }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Loại chuyến thăm')).toBeInTheDocument());
    expect(screen.getByText('Hội thảo')).toBeInTheDocument();
    expect(screen.getByText('Tiếng Việt')).toBeInTheDocument();
    expect(screen.queryByText('WORKSHOP')).not.toBeInTheDocument();
    expect(screen.queryByText('VI')).not.toBeInTheDocument();
  });

  it('renders visitTypeOther verbatim when visitType is OTHER and it is set', async () => {
    getContribution.mockResolvedValue(mockPage({
      summary: { request: { ...SUMMARY.request, visitType: 'OTHER', visitTypeOther: 'Trao đổi song phương' } },
    }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Loại chuyến thăm')).toBeInTheDocument());
    expect(screen.getByText('Trao đổi song phương')).toBeInTheDocument();
    expect(screen.queryByText('OTHER')).not.toBeInTheDocument();
  });

  it('falls back to "Khác" when visitType is OTHER and visitTypeOther is blank', async () => {
    getContribution.mockResolvedValue(mockPage({
      summary: { request: { ...SUMMARY.request, visitType: 'OTHER', visitTypeOther: null } },
    }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Loại chuyến thăm')).toBeInTheDocument());
    expect(screen.getByText('Khác')).toBeInTheDocument();
  });

  it('shows only ACCEPTED/ASSIGNED participants, hiding DECLINED/REMOVED/INVITED', async () => {
    getContribution.mockResolvedValue(mockPage({ summary: { participants: PARTICIPANTS } }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Thành phần tham gia')).toBeInTheDocument());

    expect(screen.getByText('Trần Cảnh')).toBeInTheDocument(); // ASSIGNED
    expect(screen.getByText('Nguyễn Văn B')).toBeInTheDocument(); // ACCEPTED

    expect(screen.queryByText('Department Lead Đào tạo HN')).not.toBeInTheDocument(); // DECLINED
    expect(screen.queryByText('Phạm Thị D')).not.toBeInTheDocument(); // INVITED
    expect(screen.queryByText('Lê Văn E')).not.toBeInTheDocument(); // REMOVED

    // Badge count reflects the filtered (visible) list, not the raw 5.
    expect(screen.getByText('2 người')).toBeInTheDocument();
  });

  it('renders responsibleName as the agenda item\'s responsible person', async () => {
    getContribution.mockResolvedValue(mockPage({
      summary: {
        agenda: [{
          agendaId: 1,
          title: 'Đón đoàn',
          startTime: '2026-07-30T09:00:00',
          endTime: '2026-07-30T09:30:00',
          location: 'Alpha Building',
          responsibleName: 'IC Staff Hà Nội',
          templateResponsibleRoleLabel: null,
        }],
      },
    }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Đón đoàn')).toBeInTheDocument());
    expect(screen.getByText(/Người phụ trách:/)).toBeInTheDocument();
    expect(screen.getByText('IC Staff Hà Nội')).toBeInTheDocument();
  });

  it('shows "Chưa phân công" when responsibleName is null', async () => {
    getContribution.mockResolvedValue(mockPage({
      summary: {
        agenda: [{
          agendaId: 2,
          title: 'Phiên làm việc chuyên môn',
          startTime: '2026-07-30T09:30:00',
          endTime: '2026-07-30T11:30:00',
          location: 'Alpha Building',
          responsibleName: null,
          templateResponsibleRoleLabel: null,
        }],
      },
    }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Phiên làm việc chuyên môn')).toBeInTheDocument());
    expect(screen.getByText('Chưa phân công')).toBeInTheDocument();
  });

  it('never renders templateResponsibleRoleLabel as if it were the responsible person\'s name', async () => {
    getContribution.mockResolvedValue(mockPage({
      summary: {
        agenda: [{
          agendaId: 3,
          title: 'Tiệc chiêu đãi',
          startTime: '2026-07-30T12:00:00',
          endTime: null,
          location: null,
          responsibleName: null,
          templateResponsibleRoleLabel: 'Staff Leader',
        }],
      },
    }));
    render(<VisitContributionPage />);

    await waitFor(() => expect(screen.getByText('Tiệc chiêu đãi')).toBeInTheDocument());
    // "Staff Leader" is only a template role hint — it must never render as the responsible person.
    expect(screen.queryByText('Staff Leader')).not.toBeInTheDocument();
    expect(screen.getByText('Chưa phân công')).toBeInTheDocument();
  });
});
