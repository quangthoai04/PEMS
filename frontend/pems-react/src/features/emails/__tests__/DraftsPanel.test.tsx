/**
 * The "Nháp" list: it must call the drafts collection endpoint (not the sent-mail one) and hand the
 * chosen draft's id back to the caller, which is what makes the restore path real rather than a prop
 * nobody passes.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const listDrafts = vi.fn();
vi.mock('../api/emailDraftsApi', () => ({
  emailDraftsApi: { listDrafts: (...a: unknown[]) => listDrafts(...a) },
}));
vi.mock('../../../shared/utils/vietnamTime', () => ({ formatVietnamTime: (v: string) => `t:${v}` }));

import { DraftsPanel } from '../components/DraftsPanel';

const row = (over: Record<string, unknown> = {}) => ({
  emailDraftId: 11, subject: 'Nháp một', updatedAt: '2026-07-05T09:00:00', recipientCount: 2,
  attachmentCount: 0, ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  listDrafts.mockResolvedValue({ items: [row()], page: 1, pageSize: 50, totalCount: 1 });
});

describe('DraftsPanel', () => {
  it('loads from the drafts collection endpoint', async () => {
    render(<DraftsPanel onOpenDraft={vi.fn()} />);
    await waitFor(() => expect(listDrafts).toHaveBeenCalled());
    expect(await screen.findByTestId('drafts-list')).toBeInTheDocument();
  });

  it('shows a loading state first', () => {
    render(<DraftsPanel onOpenDraft={vi.fn()} />);
    expect(screen.getByRole('status')).toHaveTextContent('Đang tải');
  });

  it('shows an empty state when there are no drafts', async () => {
    listDrafts.mockResolvedValue({ items: [], page: 1, pageSize: 50, totalCount: 0 });
    render(<DraftsPanel onOpenDraft={vi.fn()} />);
    expect(await screen.findByTestId('drafts-empty')).toBeInTheDocument();
  });

  it('shows an error state with a retry that refetches', async () => {
    listDrafts.mockRejectedValueOnce(new Error('network'));
    render(<DraftsPanel onOpenDraft={vi.fn()} />);

    expect(await screen.findByRole('alert')).toHaveTextContent('Không tải được');

    listDrafts.mockResolvedValue({ items: [row()], page: 1, pageSize: 50, totalCount: 1 });
    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(await screen.findByTestId('drafts-list')).toBeInTheDocument();
  });

  it('hands the draft id to the caller when a row is chosen', async () => {
    const onOpenDraft = vi.fn();
    render(<DraftsPanel onOpenDraft={onOpenDraft} />);

    fireEvent.click(await screen.findByRole('button', { name: /Nháp một/ }));
    expect(onOpenDraft).toHaveBeenCalledWith(11);
  });

  it('labels a draft with no subject rather than rendering a blank row', async () => {
    listDrafts.mockResolvedValue({ items: [row({ subject: '   ' })], page: 1, pageSize: 50, totalCount: 1 });
    render(<DraftsPanel onOpenDraft={vi.fn()} />);
    expect(await screen.findByText('(Không có tiêu đề)')).toBeInTheDocument();
  });

  it('refetches when the parent signals a send or discard', async () => {
    const { rerender } = render(<DraftsPanel onOpenDraft={vi.fn()} refreshToken={0} />);
    await waitFor(() => expect(listDrafts).toHaveBeenCalledTimes(1));

    rerender(<DraftsPanel onOpenDraft={vi.fn()} refreshToken={1} />);
    await waitFor(() => expect(listDrafts).toHaveBeenCalledTimes(2));
  });
});
