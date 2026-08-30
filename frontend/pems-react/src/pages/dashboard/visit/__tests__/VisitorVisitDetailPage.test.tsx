/**
 * VisitorVisitDetailPage — human-readable visit type/language, agenda responsible person, and
 * published-news visibility regression.
 *
 * Covers 3 fixes on the Visitor reception detail screen:
 *  1. `requestSummary.visitType`/`workingLanguage` and the hero badge used to render the raw
 *     backend enum (WORKSHOP/EN/...); they must now go through the visitorVisitDetail i18n
 *     namespace (`visitType.*`/`workingLanguage.*`), matching the canonical VI wording already
 *     established by RequestInfoReadOnly.VISIT_TYPE_LABELS.
 *  2. The agenda timeline did not show who is responsible for each item even though the backend
 *     DTO (`AgendaItemDto.ResponsibleName`) and the FE type (`VisitAgendaItem.responsibleName`)
 *     already carried it end-to-end — only the presentation was missing. `templateResponsibleRoleLabel`
 *     is a template role HINT, never a person's name, and must never be shown as one.
 *  3. `VisitorPublicNewsSection` was already wired to `detail.publicNews` — this only asserts the
 *     empty/non-empty rendering contract (no section header with zero items; full card + click-through
 *     when at least one PUBLISHED item is present) since the backend filtering itself is proven correct
 *     by GetVisitProcessDetailQueryHandler's own `n.Status == NewsConstants.Status.Published` filter.
 */
import React from 'react';
import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import i18n from '../../../../shared/i18n/config';
import type {
  VisitProcessDetail,
  VisitAgendaItem,
  VisitorPublicNewsListItem,
} from '../../../../features/delegations/types/delegations.types';

const navigateMock = vi.fn();
vi.mock('react-router-dom', () => ({
  useNavigate: () => navigateMock,
}));

import { VisitorVisitDetailPage } from '../VisitorVisitDetailPage';

const HOST = {
  userId: 1,
  fullName: 'Nguyen Van Host',
  email: 'host@fptu.edu.vn',
  phone: '0900000000',
  departmentName: 'IC Office',
  statusLabel: 'Đã được phân công',
};

const AGENDA_BASE: VisitAgendaItem = {
  agendaId: 1,
  title: 'Đón đoàn tại sảnh',
  startTime: '2026-09-01T09:00:00',
  endTime: '2026-09-01T09:30:00',
  description: null,
  location: 'Alpha Building - FPTU Hà Nội',
  responsibleName: null,
  templateResponsibleRoleLabel: null,
};

function buildDetail(overrides: Partial<VisitProcessDetail> = {}): VisitProcessDetail {
  return {
    visitRequestId: 9001,
    visitInstanceId: 501,
    delegationName: 'SeoulTech Robotics Collaboration Delegation',
    instanceStatus: 'BEFORE_VISIT',
    plannedStartAt: '2026-09-01T09:00:00',
    plannedEndAt: '2026-09-01T11:00:00',
    campusName: 'FPTU Hà Nội',
    hostUserId: 1,
    hostName: HOST.fullName,
    relation: 'OPERATIONAL_CONTACT',
    canEditBefore: false,
    agenda: [AGENDA_BASE],
    host: HOST,
    participants: [],
    notifications: [],
    publicNews: [],
    ...overrides,
  } as VisitProcessDetail;
}

const PERM = {
  visitInstanceId: 501,
  visitRequestId: 9001,
  requestStatus: 'APPROVED',
  instanceStatus: 'BEFORE_VISIT',
  relation: 'OPERATIONAL_CONTACT',
  hostAssigned: true,
} as any;

const NEWS_ITEM: VisitorPublicNewsListItem = {
  newsId: 42,
  title: 'FPTU welcomes the SeoulTech delegation',
  summary: 'A short summary of the visit.',
  thumbnailUrl: 'https://example.com/thumb.jpg',
  publishedAt: '2026-08-26T10:00:00',
  authorName: 'IC Office',
};

afterEach(async () => {
  await act(async () => { await i18n.changeLanguage('en'); });
  navigateMock.mockClear();
});

describe('Visit type — human-readable, not the raw enum', () => {
  beforeEach(async () => { await act(async () => { await i18n.changeLanguage('vi'); }); });

  it('renders WORKSHOP as the Vietnamese label, never the raw enum', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({ requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', visitType: 'WORKSHOP' } as any })}
      />,
    );
    expect(screen.queryByText('WORKSHOP')).toBeNull();
    expect(screen.getAllByText('Hội thảo').length).toBeGreaterThan(0);
  });

  it('renders MEETING as the Vietnamese label', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({ requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', visitType: 'MEETING' } as any })}
      />,
    );
    expect(screen.queryByText('MEETING')).toBeNull();
    expect(screen.getAllByText('Họp trao đổi').length).toBeGreaterThan(0);
  });

  it('OTHER uses the guest-entered free text when present', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({
          requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', visitType: 'OTHER', visitTypeOther: 'Lễ trao học bổng' } as any,
        })}
      />,
    );
    expect(screen.queryByText('OTHER')).toBeNull();
    expect(screen.getAllByText('Lễ trao học bổng').length).toBeGreaterThan(0);
  });

  it('OTHER falls back to "Khác" when no free text was entered', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({
          requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', visitType: 'OTHER', visitTypeOther: null } as any,
        })}
      />,
    );
    expect(screen.getAllByText('Khác').length).toBeGreaterThan(0);
  });
});

describe('Working language — human-readable, locale-aware', () => {
  it('renders EN as "Tiếng Anh" / VI as "Tiếng Việt" on the Vietnamese locale', async () => {
    await act(async () => { await i18n.changeLanguage('vi'); });
    const { unmount } = render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({ requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', workingLanguage: 'EN' } as any })}
      />,
    );
    expect(screen.queryByText(/^EN$/)).toBeNull();
    expect(screen.getByText('Tiếng Anh')).toBeInTheDocument();
    unmount();

    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({ requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', workingLanguage: 'VI' } as any })}
      />,
    );
    expect(screen.getByText('Tiếng Việt')).toBeInTheDocument();
  });

  it('renders "English"/"Vietnamese" on the English locale', async () => {
    await act(async () => { await i18n.changeLanguage('en'); });
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({ requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', workingLanguage: 'EN' } as any })}
      />,
    );
    expect(screen.queryByText(/^EN$/)).toBeNull();
    expect(screen.getByText('English')).toBeInTheDocument();
  });
});

describe('Operational contact — organization partner badge', () => {
  beforeEach(async () => { await act(async () => { await i18n.changeLanguage('vi'); }); });

  it('shows the badge with its own wording when isOrganizationInSystem is true', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({
          requestSummary: {
            delegationName: 'x', visitScope: 'SINGLE_CAMPUS',
            operationalContactOrganization: 'ĐH Bách Khoa', operationalContactIsOrganizationInSystem: true,
          } as any,
        })}
      />,
    );
    expect(screen.getByText('✓ Tổ chức đã có trong hệ thống')).toBeInTheDocument();
    expect(screen.getByText('ĐH Bách Khoa')).toBeInTheDocument();
  });

  it('shows no badge when isOrganizationInSystem is false/undefined', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({
          requestSummary: { delegationName: 'x', visitScope: 'SINGLE_CAMPUS', operationalContactOrganization: 'ĐH Bách Khoa' } as any,
        })}
      />,
    );
    expect(screen.queryByText('✓ Tổ chức đã có trong hệ thống')).toBeNull();
    expect(screen.getByText('ĐH Bách Khoa')).toBeInTheDocument();
  });
});

describe('Agenda — responsible person', () => {
  beforeEach(async () => { await act(async () => { await i18n.changeLanguage('vi'); }); });

  it('shows the real assigned person from responsibleName', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({ agenda: [{ ...AGENDA_BASE, responsibleName: 'IC Staff Hà Nội' }] })}
      />,
    );
    expect(screen.getByText('IC Staff Hà Nội')).toBeInTheDocument();
  });

  it('shows "Chưa phân công" when responsibleName is null', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({ agenda: [{ ...AGENDA_BASE, responsibleName: null }] })}
      />,
    );
    expect(screen.getByText('Chưa phân công')).toBeInTheDocument();
  });

  it('never displays templateResponsibleRoleLabel as if it were the responsible person', () => {
    render(
      <VisitorVisitDetailPage
        perm={PERM}
        detail={buildDetail({
          agenda: [{ ...AGENDA_BASE, responsibleName: null, templateResponsibleRoleLabel: 'IC Host' }],
        })}
      />,
    );
    expect(screen.getByText('Chưa phân công')).toBeInTheDocument();
    expect(screen.queryByText('IC Host')).toBeNull();
  });
});

const NEWS_ITEM_2: VisitorPublicNewsListItem = {
  newsId: 43,
  title: 'A second published article',
  summary: 'Another short summary.',
  thumbnailUrl: null,
  publishedAt: '2026-08-27T09:00:00',
  authorName: 'Staff Leader',
};

const EMPTY_TEXT_VI = 'Chưa có bài tin nào được công khai cho chuyến thăm này.';

describe('Published news — section always renders (with empty state when there is nothing to show)', () => {
  beforeEach(async () => { await act(async () => { await i18n.changeLanguage('vi'); }); });

  it('case 1: publicNews = [] still renders the section title and shows the empty state, no fake card', () => {
    render(<VisitorVisitDetailPage perm={PERM} detail={buildDetail({ publicNews: [] })} />);
    expect(screen.getByText('Bản tin chuyến thăm')).toBeInTheDocument();
    expect(screen.getByText(EMPTY_TEXT_VI)).toBeInTheDocument();
    expect(screen.queryByRole('article')).toBeNull();
  });

  it('case 2: publicNews = undefined is handled defensively — section renders, empty state shown, no crash', () => {
    const detail = buildDetail();
    delete (detail as any).publicNews;
    expect(() => render(<VisitorVisitDetailPage perm={PERM} detail={detail} />)).not.toThrow();
    expect(screen.getByText('Bản tin chuyến thăm')).toBeInTheDocument();
    expect(screen.getByText(EMPTY_TEXT_VI)).toBeInTheDocument();
  });

  it('case 3: one published item — renders it, hides the empty state', () => {
    render(<VisitorVisitDetailPage perm={PERM} detail={buildDetail({ publicNews: [NEWS_ITEM] })} />);
    expect(screen.getByText('Bản tin chuyến thăm')).toBeInTheDocument();
    expect(screen.getByText(NEWS_ITEM.title)).toBeInTheDocument();
    expect(screen.getByText(NEWS_ITEM.summary!)).toBeInTheDocument();
    expect(screen.queryByText(EMPTY_TEXT_VI)).toBeNull();
  });

  it('case 4: multiple items — renders the full list, hides the empty state', () => {
    render(<VisitorVisitDetailPage perm={PERM} detail={buildDetail({ publicNews: [NEWS_ITEM, NEWS_ITEM_2] })} />);
    expect(screen.getByText(NEWS_ITEM.title)).toBeInTheDocument();
    expect(screen.getByText(NEWS_ITEM_2.title)).toBeInTheDocument();
    expect(screen.queryByText(EMPTY_TEXT_VI)).toBeNull();
  });

  it('case 5: clicking a news card still navigates to the article (no regression)', () => {
    render(<VisitorVisitDetailPage perm={PERM} detail={buildDetail({ publicNews: [NEWS_ITEM] })} />);
    fireEvent.click(screen.getByText('Xem chi tiết'));
    expect(navigateMock).toHaveBeenCalledWith(
      `/news/${NEWS_ITEM.newsId}`,
      expect.objectContaining({ state: expect.objectContaining({ returnTo: expect.any(String) }) }),
    );
  });

  it('renders the English empty state on the English locale', async () => {
    await act(async () => { await i18n.changeLanguage('en'); });
    render(<VisitorVisitDetailPage perm={PERM} detail={buildDetail({ publicNews: [] })} />);
    expect(screen.getByText('Visit news')).toBeInTheDocument();
    expect(screen.getByText('No news articles have been published for this visit yet.')).toBeInTheDocument();
  });
});
