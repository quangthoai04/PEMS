import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({ getVisitRequestHistory: vi.fn() }));

import VisitHistoryTimeline from '../components/VisitHistoryTimeline';
import { getVisitRequestHistory, type VisitHistoryEntry } from '../api/visitRequestV2Api';

// jsdom's navigator.language is en-US → i18n initializes in EN; assertions use the EN strings.

const entry = (over: Partial<VisitHistoryEntry> = {}): VisitHistoryEntry => ({
  at: '2026-07-20T09:30:00',
  eventCode: 'INSTANCE_CONTENT_CREATED',
  visitInstanceId: 10,
  campusName: 'FPT University Hà Nội',
  actorName: 'Kim Min Jae',
  formRevision: 1,
  approvalRevision: 1,
  amendmentNo: null,
  statusCode: null,
  sourceType: 'CREATE',
  reason: null,
  maskedEmail: null,
  fromStatus: null,
  toStatus: null,
  ...over,
});

const withEntries = (...entries: VisitHistoryEntry[]) =>
  vi.mocked(getVisitRequestHistory).mockResolvedValue({
    visitRequestId: 1, requestCode: 'VR-1', entries,
  });

describe('VisitHistoryTimeline', () => {
  beforeEach(() => vi.clearAllMocks());

  it('never renders the audit fragments the backend used to glue into titles', async () => {
    withEntries(
      entry({ sourceType: 'CREATE', approvalRevision: 1 }),
      entry({ eventCode: 'INSTANCE_REJECTED', statusCode: 'REJECTED', reason: 'Trùng lịch' }),
    );
    render(<VisitHistoryTimeline visitRequestId={1} />);
    await screen.findByTestId('visit-history-timeline');

    const text = screen.getByTestId('visit-history-timeline').textContent ?? '';
    expect(text).not.toContain('source=');
    expect(text).not.toContain('approvalRevision=');
    expect(text).not.toContain('CREATE');
    expect(text).not.toContain('REJECTED');
    expect(text).not.toContain('→');
  });

  it('names the actor and the campus so multi-campus rows are distinguishable', async () => {
    withEntries(
      entry({ campusName: 'FPT University Hà Nội' }),
      entry({ campusName: 'FPT University HCM', visitInstanceId: 11 }),
    );
    render(<VisitHistoryTimeline visitRequestId={1} />);
    await screen.findByTestId('visit-history-timeline');

    expect(screen.getByText('Content for FPT University Hà Nội was created — version 1.')).toBeInTheDocument();
    expect(screen.getByText('Content for FPT University HCM was created — version 1.')).toBeInTheDocument();
  });

  it('phrases a rejection with its actor and surfaces the reason separately', async () => {
    withEntries(entry({
      eventCode: 'INSTANCE_REJECTED', actorName: 'IC Staff Leader Hà Nội',
      statusCode: 'REJECTED', reason: 'Trùng lịch sự kiện của cơ sở.',
    }));
    render(<VisitHistoryTimeline visitRequestId={1} />);
    await screen.findByTestId('visit-history-timeline');

    expect(screen.getByText('IC Staff Leader Hà Nội declined to host at FPT University Hà Nội.')).toBeInTheDocument();
    expect(screen.getByText('Reason: Trùng lịch sự kiện của cơ sở.')).toBeInTheDocument();
  });

  it('marks a submitted proposal as not yet in force', async () => {
    withEntries(entry({ eventCode: 'AMENDMENT_SUBMITTED', amendmentNo: 3 }));
    render(<VisitHistoryTimeline visitRequestId={1} />);
    await screen.findByTestId('visit-history-timeline');

    expect(screen.getByText('Kim Min Jae proposed change #3 for FPT University Hà Nội.')).toBeInTheDocument();
    expect(screen.getByText('Not the content in force')).toBeInTheDocument();
  });

  it('falls back to a neutral sentence for an event code it does not know', async () => {
    withEntries(entry({ eventCode: 'SOMETHING_NEW_ENTIRELY' }));
    render(<VisitHistoryTimeline visitRequestId={1} />);
    await screen.findByTestId('visit-history-timeline');

    expect(screen.getByText('Another change was recorded.')).toBeInTheDocument();
    expect(screen.queryByText(/SOMETHING_NEW_ENTIRELY/)).not.toBeInTheDocument();
  });

  it('stands in for a missing actor rather than printing an empty name', async () => {
    withEntries(entry({ eventCode: 'INSTANCE_REJECTED', actorName: null }));
    render(<VisitHistoryTimeline visitRequestId={1} />);
    await screen.findByTestId('visit-history-timeline');

    expect(screen.getByText('A user declined to host at FPT University Hà Nội.')).toBeInTheDocument();
  });

  it('offers a retry when the history cannot be loaded', async () => {
    vi.mocked(getVisitRequestHistory).mockRejectedValueOnce(new Error('network'));
    render(<VisitHistoryTimeline visitRequestId={1} />);

    const retry = await screen.findByTestId('history-retry');
    withEntries(entry());
    fireEvent.click(retry);

    await waitFor(() => expect(screen.getByTestId('visit-history-timeline')).toBeInTheDocument());
  });

  it('states an empty history instead of rendering an empty list', async () => {
    withEntries();
    render(<VisitHistoryTimeline visitRequestId={1} />);

    expect(await screen.findByText('No changes have been recorded yet.')).toBeInTheDocument();
  });
});
