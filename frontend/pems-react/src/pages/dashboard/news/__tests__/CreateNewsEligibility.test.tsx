import React from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

// ── Everything the page needs but this test is not about ──────────────────────
// The subject here is ONE thing: where the "may I write news for this visit" verdict comes from.

const httpGet = vi.fn();
const httpPost = vi.fn();
vi.mock('../../../../shared/api/httpClient', () => ({
  default: { get: (...a: unknown[]) => httpGet(...a), post: (...a: unknown[]) => httpPost(...a) },
}));

vi.mock('../../../../shared/hooks/useAuth', () => ({
  useAuth: () => ({ user: { roleCode: 'STUDENT', subRole: null } }),
}));

vi.mock('react-quill-new', () => ({
  default: ({ value }: { value?: string }) => <div data-testid="quill">{value}</div>,
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

vi.mock('react-hot-toast', () => ({
  default: { success: vi.fn(), error: vi.fn() },
}));

vi.mock('../components/SectionImagesEditor', () => ({
  SectionImagesEditor: () => <div data-testid="section-images" />,
}));
vi.mock('../components/BilingualColumns', () => ({
  BilingualColumns: ({ children }: { children?: React.ReactNode }) => <div>{children}</div>,
  LanguageColumnLabel: () => <span />,
}));
vi.mock('../components/useBilingualTranslate', () => ({
  useBilingualTranslate: () => ({ translating: false, retranslateNow: vi.fn() }),
}));
vi.mock('../components/CollapsibleSection', () => ({
  CollapsibleSection: ({ title, disabled, children }: {
    title: React.ReactNode; disabled?: boolean; children: React.ReactNode;
  }) => (
    <section data-testid="collapsible" data-disabled={String(!!disabled)}>
      <h2>{title}</h2>
      {children}
    </section>
  ),
}));
vi.mock('../components/AutoGrowInput', () => ({
  AutoGrowInput: (p: Record<string, unknown>) => <input {...p} />,
  AutoGrowTextarea: (p: Record<string, unknown>) => <textarea {...p} />,
}));
vi.mock('../components/VisitInstancePhotoPicker', () => ({
  VisitInstancePhotoPicker: () => <div data-testid="photo-picker" />,
}));
vi.mock('../components/SmartImage', () => ({ SmartImage: () => <img alt="" /> }));

import { CreateNews } from '../CreateNews';

const PRESET_ID = 3006;

const eligibleItem = {
  visitInstanceId: PRESET_ID,
  visitTitle: 'Đoàn Kyoto tại FPTU Hà Nội',
  campusName: 'FPTU Hà Nội',
  plannedStartAt: '2026-09-01T09:00:00',
  plannedEndAt: '2026-09-01T11:00:00',
  status: 'AFTER_VISIT',
  hasNews: false,
  canSelect: true,
};

/** The list plus the backend's verdict for the ONE campus the page was opened for. */
const respond = (items: unknown[], requested: unknown) => {
  httpGet.mockResolvedValue({ data: { items, requested } });
};

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={[`/dashboard/news/create?visitInstanceId=${PRESET_ID}`]}>
      <CreateNews />
    </MemoryRouter>,
  );

const formLocked = () =>
  screen.getAllByTestId('collapsible').every(s => s.getAttribute('data-disabled') === 'true');

describe('CreateNews — canonical visit-news eligibility (V16)', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('asks the backend about the preset campus specifically', async () => {
    respond([eligibleItem], { visitInstanceId: PRESET_ID, canCreate: true, reasonCode: null, hasNews: false, existingNewsId: null, canEditExisting: false });

    renderPage();

    await waitFor(() => expect(httpGet).toHaveBeenCalled());
    expect(httpGet).toHaveBeenCalledWith('/news/eligible-visit-instances', {
      params: { includeAlreadyHasNews: true, visitInstanceId: String(PRESET_ID) },
    });
  });

  it('lets the form open when the backend says the preset campus is eligible', async () => {
    respond([eligibleItem], { visitInstanceId: PRESET_ID, canCreate: true, reasonCode: null, hasNews: false, existingNewsId: null, canEditExisting: false });

    renderPage();

    expect(await screen.findByText('Đoàn Kyoto tại FPTU Hà Nội')).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    await waitFor(() => expect(formLocked()).toBe(false));
  });

  it.each([
    ['NEWS_VISIT_NOT_IN_WRITING_WINDOW', 'This visit has not reached the stage where news can be written (After visit onwards).'],
    ['NEWS_VISIT_NOT_REQUIRED', 'This visit has been confirmed as not requiring a news article.'],
    ['NEWS_VISIT_MEDIA_CONSENT_DENIED', 'News cannot be created because the guests did not agree to media coverage.'],
    ['NEWS_VISIT_PARTICIPANT_ROLE_NOT_ALLOWED', 'Your participation role is not allowed to write news for this visit.'],
    ['NEWS_VISIT_NOT_IN_SCOPE', 'You are not part of this visit, so you cannot write news for it.'],
  ])('states the single true cause for %s', async (reasonCode, expected) => {
    respond([], { visitInstanceId: PRESET_ID, canCreate: false, reasonCode, hasNews: false, existingNewsId: null, canEditExisting: false });

    renderPage();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(expected);
    // The old sentence named three causes at once, so most of it was wrong every time.
    expect(alert.textContent).not.toContain('hoặc bạn không phải Host');
    await waitFor(() => expect(formLocked()).toBe(true));
  });

  it('points an author who already wrote one at their existing article instead of a duplicate', async () => {
    respond(
      [{ ...eligibleItem, hasNews: true, canSelect: false }],
      {
        visitInstanceId: PRESET_ID, canCreate: false,
        reasonCode: 'NEWS_ALREADY_EXISTS_FOR_VISIT_INSTANCE',
        hasNews: true, existingNewsId: 987, canEditExisting: true,
      },
    );

    renderPage();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('You already have a news article for this visit.');
    expect(screen.getByRole('button', { name: 'Open my existing article' })).toBeInTheDocument();
    await waitFor(() => expect(formLocked()).toBe(true));
  });

  it('takes the verdict from the backend, not from whether the campus is in the list', async () => {
    // The list is where the page USED to look: a campus missing from it was declared ineligible.
    // The backend says otherwise here, and the backend is the authority.
    respond([], { visitInstanceId: PRESET_ID, canCreate: true, reasonCode: null, hasNews: false, existingNewsId: null, canEditExisting: false });

    renderPage();

    await waitFor(() => expect(httpGet).toHaveBeenCalled());
    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });

  it('says the visit could not be loaded when the query itself fails', async () => {
    httpGet.mockRejectedValue(new Error('network'));

    renderPage();

    expect(await screen.findByRole('alert'))
      .toHaveTextContent('Unable to load the visit information. Please try again.');
  });
});
