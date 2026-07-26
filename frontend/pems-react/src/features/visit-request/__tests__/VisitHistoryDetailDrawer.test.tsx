import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

vi.mock('../api/visitRequestV2Api', () => ({
  getVisitRequestHistory: vi.fn(),
  getVisitHistoryDetail: vi.fn(),
}));

import VisitHistoryTimeline from '../components/VisitHistoryTimeline';
import {
  getVisitRequestHistory,
  getVisitHistoryDetail,
  type VisitHistoryDetail,
  type VisitHistoryEntry,
} from '../api/visitRequestV2Api';

// jsdom reports en-US, so i18n initialises in EN and the assertions use the EN strings.

const entry = (over: Partial<VisitHistoryEntry> = {}): VisitHistoryEntry => ({
  at: '2026-07-20T09:30:00',
  eventCode: 'INSTANCE_SAFE_EDIT_APPLIED',
  eventId: 'IREV:100',
  visitInstanceId: 10,
  campusName: 'FPT University Hà Nội',
  actorName: 'Kim Min Jae',
  formRevision: 3,
  approvalRevision: 1,
  amendmentNo: null,
  statusCode: null,
  sourceType: 'SAFE_EDIT',
  reason: null,
  maskedEmail: null,
  fromStatus: null,
  toStatus: null,
  ...over,
});

const detail = (over: Partial<VisitHistoryDetail> = {}): VisitHistoryDetail => ({
  eventId: 'IREV:100',
  eventCode: 'INSTANCE_SAFE_EDIT_APPLIED',
  occurredAt: '2026-07-20T09:30:00',
  actorName: 'Kim Min Jae',
  campusId: 1,
  campusName: 'FPT University Hà Nội',
  reason: null,
  beforeRevision: 2,
  afterRevision: 3,
  fieldChanges: [],
  collectionChanges: [],
  ...over,
});

const renderTimeline = (entries: VisitHistoryEntry[]) => {
  vi.mocked(getVisitRequestHistory).mockResolvedValue({
    visitRequestId: 1, requestCode: 'VR-1', entries,
  });
  return render(<VisitHistoryTimeline visitRequestId={1} />);
};

describe('history detail — the eye button', () => {
  beforeEach(() => vi.clearAllMocks());

  it('is offered for an event that has a diff', async () => {
    renderTimeline([entry()]);
    expect(await screen.findByTestId('history-detail-open-IREV:100')).toBeInTheDocument();
  });

  it('is NOT offered for an event whose line already says everything', async () => {
    // A campus decision carries its outcome and note inline; an eye button there would open a
    // drawer that repeats what the reader just read.
    renderTimeline([entry({ eventCode: 'INSTANCE_APPROVED', eventId: null })]);
    await screen.findByTestId('visit-history-timeline');
    expect(screen.queryByRole('button', { name: /view change details/i })).toBeNull();
  });

  it('carries an accessible name that says WHICH event it opens', async () => {
    // A timeline of ten identical "View change details" buttons is unusable with a screen reader.
    renderTimeline([entry()]);
    const button = await screen.findByTestId('history-detail-open-IREV:100');
    expect(button).toHaveAccessibleName(/view change details:/i);
    expect(button).toHaveAttribute('title');
  });
});

describe('history detail — the drawer', () => {
  beforeEach(() => vi.clearAllMocks());

  const open = async () => {
    renderTimeline([entry()]);
    fireEvent.click(await screen.findByTestId('history-detail-open-IREV:100'));
    return screen.findByTestId('history-detail-drawer');
  };

  it('shows field before/after in a table', async () => {
    vi.mocked(getVisitHistoryDetail).mockResolvedValue(detail({
      fieldChanges: [{
        fieldCode: 'NoteToFptu',
        labelKey: 'visitRequestV2:historyDetail.field.noteToFptu',
        beforeValue: 'Ghi chú cũ',
        afterValue: 'Ghi chú mới',
      }],
    }));
    await open();

    const table = await screen.findByTestId('history-detail-fields');
    expect(table).toHaveTextContent('Note to the campus');
    expect(table).toHaveTextContent('Ghi chú cũ');
    expect(table).toHaveTextContent('Ghi chú mới');
  });

  it('renders member joins and departures, not a JSON blob', async () => {
    vi.mocked(getVisitHistoryDetail).mockResolvedValue(detail({
      collectionChanges: [
        {
          collectionCode: 'VISITORS', changeType: 'ADDED', itemKey: 'Khách Hai',
          before: null, after: { fullName: 'Khách Hai', jobTitle: 'GV' },
        },
        {
          collectionCode: 'VISITORS', changeType: 'REMOVED', itemKey: 'Khách Một',
          before: { fullName: 'Khách Một' }, after: null,
        },
      ],
    }));
    await open();

    const list = await screen.findByTestId('history-detail-collections');
    expect(list).toHaveTextContent('Added');
    expect(list).toHaveTextContent('Khách Hai');
    expect(list).toHaveTextContent('Removed');
    expect(list).toHaveTextContent('Khách Một');
  });

  it('shows only the member fields that actually moved on an UPDATE', async () => {
    vi.mocked(getVisitHistoryDetail).mockResolvedValue(detail({
      collectionChanges: [{
        collectionCode: 'VISITORS', changeType: 'UPDATED', itemKey: 'Khách Một',
        before: { fullName: 'Khách Một', jobTitle: 'GV', organization: 'ĐH ABC' },
        after: { fullName: 'Khách Một', jobTitle: 'Trưởng khoa', organization: 'ĐH ABC' },
      }],
    }));
    await open();

    const list = await screen.findByTestId('history-detail-collections');
    expect(list).toHaveTextContent('Trưởng khoa');
    // The organisation did not change, so it is not listed as if it had.
    expect(list).not.toHaveTextContent('ĐH ABC');
  });

  it('never renders raw snapshot JSON', async () => {
    vi.mocked(getVisitHistoryDetail).mockResolvedValue(detail({
      fieldChanges: [{
        fieldCode: 'Purpose',
        labelKey: 'visitRequestV2:historyDetail.field.purpose',
        beforeValue: 'A', afterValue: 'B',
      }],
    }));
    const drawer = await open();
    await screen.findByTestId('history-detail-fields');

    const text = drawer.textContent ?? '';
    expect(text).not.toMatch(/[{}]/);
    expect(text).not.toContain('snapshotJson');
  });

  it('says so plainly when the event recorded no detail', async () => {
    vi.mocked(getVisitHistoryDetail).mockResolvedValue(detail());
    await open();
    expect(await screen.findByTestId('history-detail-empty')).toBeInTheDocument();
  });

  it('reports a load failure instead of showing an empty drawer', async () => {
    vi.mocked(getVisitHistoryDetail).mockRejectedValue(new Error('boom'));
    await open();
    expect(await screen.findByRole('alert')).toHaveTextContent(/could not load/i);
  });

  it('closes on Escape and on the close button', async () => {
    vi.mocked(getVisitHistoryDetail).mockResolvedValue(detail());
    await open();

    fireEvent.keyDown(document, { key: 'Escape' });
    await waitFor(() => expect(screen.queryByTestId('history-detail-drawer')).toBeNull());

    fireEvent.click(await screen.findByTestId('history-detail-open-IREV:100'));
    fireEvent.click(await screen.findByTestId('history-detail-close'));
    await waitFor(() => expect(screen.queryByTestId('history-detail-drawer')).toBeNull());
  });

  it('requests the detail for the event that was clicked', async () => {
    vi.mocked(getVisitHistoryDetail).mockResolvedValue(detail());
    renderTimeline([entry(), entry({ eventId: 'AMDS:55', eventCode: 'AMENDMENT_SUBMITTED', at: '2026-07-21T08:00:00' })]);

    fireEvent.click(await screen.findByTestId('history-detail-open-AMDS:55'));
    await waitFor(() => expect(getVisitHistoryDetail).toHaveBeenCalledWith(1, 'AMDS:55'));
  });
});
