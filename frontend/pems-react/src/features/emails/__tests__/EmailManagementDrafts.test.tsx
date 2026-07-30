/**
 * The wiring between the drafts list and the composer, in the screen that owns both.
 *
 * DraftsPanel's own tests prove it refetches when `refreshToken` changes; these prove EmailManagement
 * actually changes it after a send, and that it hands the chosen draft's id to the composer. Without
 * this, "the list refreshes after sending" would rest on reading the JSX rather than on running it.
 *
 * DraftsPanel and EmailComposeModal are replaced by stubs that expose their props, so the test is
 * about the wiring and not about either component's internals.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const getEmailList = vi.fn();
vi.mock('../../../features/emails/api/emailsApi', () => ({
  emailsApi: {
    getEmailList: (...a: unknown[]) => getEmailList(...a),
    getEmailTemplateList: vi.fn().mockResolvedValue({ data: { items: [] } }),
  },
}));

let lastPanelProps: { onOpenDraft: (id: number) => void; refreshToken?: number } | null = null;
vi.mock('../../../features/emails/components/DraftsPanel', () => ({
  DraftsPanel: (props: { onOpenDraft: (id: number) => void; refreshToken?: number }) => {
    lastPanelProps = props;
    return (
      <div data-testid="drafts-panel" data-refresh={String(props.refreshToken ?? 0)}>
        <button type="button" onClick={() => props.onOpenDraft(42)}>__open-draft-42__</button>
      </div>
    );
  },
}));

let lastModalProps: { initialDraftId?: number | null; onSent?: () => void; onClose?: () => void } | null = null;
vi.mock('../../../features/emails/components/EmailComposeModal', () => ({
  EmailComposeModal: (props: { initialDraftId?: number | null; onSent?: () => void; onClose?: () => void }) => {
    lastModalProps = props;
    return (
      <>
        <button type="button" onClick={() => props.onSent?.()}>__report-sent__</button>
        {/* Discarding a draft closes the composer through the same onClose the ✕ uses. */}
        <button type="button" onClick={() => props.onClose?.()}>__report-closed__</button>
      </>
    );
  },
}));

vi.mock('./TemplateManagement', () => ({ TemplateManagement: () => <div /> }));
vi.mock('../../../shared/utils/vietnamTime', () => ({
  formatVietnamDateTime: (v: string) => v, formatVietnamTime: (v: string) => v,
}));
vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
  useLocation: () => ({ pathname: '/dashboard/emails', search: '' }),
  useSearchParams: () => [new URLSearchParams(), vi.fn()],
}));

import { EmailManagement } from '../../../pages/dashboard/emails/EmailManagement';

/** The mailbox select, found by the options it owns rather than by its current value. */
const mailboxSelect = () =>
  screen.getAllByRole('combobox').find(el => el.querySelector('option[value="drafts"]')) as HTMLSelectElement;

const setMailbox = (value: string) => fireEvent.change(mailboxSelect(), { target: { value } });
const selectDrafts = () => setMailbox('drafts');

beforeEach(() => {
  vi.clearAllMocks();
  lastPanelProps = null;
  lastModalProps = null;
  localStorage.setItem('currentUser', JSON.stringify({ role: 'STAFF' }));
  getEmailList.mockResolvedValue({ data: { items: [], totalCount: 0 } });
});

describe('EmailManagement — drafts wiring', () => {
  it('shows the drafts list when the "Nháp" filter is chosen', async () => {
    render(<EmailManagement />);
    selectDrafts();
    expect(await screen.findByTestId('drafts-panel')).toBeInTheDocument();
  });

  it('does not fetch sent mail for the drafts filter', async () => {
    render(<EmailManagement />);
    await waitFor(() => expect(getEmailList).toHaveBeenCalled());

    getEmailList.mockClear();
    selectDrafts();
    await waitFor(() => expect(screen.getByTestId('drafts-panel')).toBeInTheDocument());
    expect(getEmailList).not.toHaveBeenCalled();
  });

  it('opens the composer on the chosen draft', async () => {
    render(<EmailManagement />);
    selectDrafts();

    fireEvent.click(await screen.findByRole('button', { name: '__open-draft-42__' }));
    await waitFor(() => expect(lastModalProps?.initialDraftId).toBe(42));
  });

  it('leaves the drafts view for Sent after a send, and comes back to a refreshed list', async () => {
    render(<EmailManagement />);
    selectDrafts();
    await screen.findByTestId('drafts-panel');

    const before = Number(screen.getByTestId('drafts-panel').getAttribute('data-refresh'));

    fireEvent.click(screen.getByRole('button', { name: '__report-sent__' }));

    // Sending moves the user to "Đã gửi", so the drafts panel unmounts — the sent draft is no longer
    // a draft and the query excludes it by status.
    await waitFor(() => expect(screen.queryByTestId('drafts-panel')).not.toBeInTheDocument());
    expect(mailboxSelect().value).toBe('sent');

    // Returning to "Nháp" mounts the panel with a bumped token, so it refetches rather than showing a
    // cached list that still contains the draft just sent.
    selectDrafts();
    const panel = await screen.findByTestId('drafts-panel');
    expect(Number(panel.getAttribute('data-refresh'))).toBeGreaterThan(before);
  });

  it('refreshes the drafts list in place when the composer closes after a discard', async () => {
    render(<EmailManagement />);
    selectDrafts();
    await screen.findByTestId('drafts-panel');

    fireEvent.click(screen.getByRole('button', { name: '__open-draft-42__' }));
    await waitFor(() => expect(lastModalProps?.initialDraftId).toBe(42));

    const before = Number(screen.getByTestId('drafts-panel').getAttribute('data-refresh'));

    // Discard closes the composer without changing the mailbox, so the panel stays mounted and has to
    // refetch on the spot — otherwise the discarded draft would linger in the list.
    fireEvent.click(screen.getByRole('button', { name: '__report-closed__' }));

    await waitFor(() =>
      expect(Number(screen.getByTestId('drafts-panel').getAttribute('data-refresh'))).toBeGreaterThan(before));
    expect(mailboxSelect().value).toBe('drafts');
    expect(lastModalProps?.initialDraftId).toBeNull();
  });

  it('clears the draft id after a send so a later compose starts empty', async () => {
    render(<EmailManagement />);
    selectDrafts();

    fireEvent.click(await screen.findByRole('button', { name: '__open-draft-42__' }));
    await waitFor(() => expect(lastModalProps?.initialDraftId).toBe(42));

    fireEvent.click(screen.getByRole('button', { name: '__report-sent__' }));
    await waitFor(() => expect(lastModalProps?.initialDraftId).toBeNull());
  });
});
