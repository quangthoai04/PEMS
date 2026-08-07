/**
 * G6.5 — the reply path as the user actually reaches it.
 *
 * `ReplyComposer.test.tsx` proves the envelope in isolation; a component nobody can open would still pass
 * every one of those tests. This drives the real page: the server says the viewer may reply, the button
 * appears, the composer opens on the right message, and the request that leaves carries the right thing.
 *
 * The email being answered deliberately has CC and BCC rows — the shape the detail endpoint returns to a
 * SENDER, who is the one viewer allowed to see blind copies. That is the exact situation where a
 * "reply all" convenience would leak them, so it is the situation the test uses.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const getEmailDetail = vi.fn();
const replyEmail = vi.fn();
const replyAllEmail = vi.fn();
const getRecipientLimits = vi.fn();

vi.mock('../api/emailsApi', () => ({
  emailsApi: {
    getEmailDetail: (...a: unknown[]) => getEmailDetail(...a),
    replyEmail: (...a: unknown[]) => replyEmail(...a),
    replyAllEmail: (...a: unknown[]) => replyAllEmail(...a),
    getRecipientLimits: (...a: unknown[]) => getRecipientLimits(...a),
    markCompleted: vi.fn(),
  },
}));
vi.mock('react-router-dom', () => ({
  useNavigate: () => vi.fn(),
  useParams: () => ({ sourceType: 'sent', id: '77' }),
}));
vi.mock('react-hot-toast', () => ({ toast: { error: vi.fn(), success: vi.fn() } }));
vi.mock('../../../shared/utils/vietnamTime', () => ({ formatVietnamDateTime: (v: string) => v }));
vi.mock('react-quill-new', () => ({
  default: ({ value, onChange, readOnly }: { value: string; onChange: (v: string, d: unknown, s: string) => void; readOnly?: boolean }) => (
    <textarea aria-label="reply-body" value={value} readOnly={readOnly}
      onChange={e => onChange(e.target.value, undefined, 'user')} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { SentEmailDetail } from '../../../pages/dashboard/emails/SentEmailDetail';

const BCC_ADDRESS = 'bi-mat@fpt.edu.vn';

const detail = (overrides: Record<string, unknown> = {}) => ({
  data: {
    sentEmailId: 77,
    subject: 'Thư mời',
    status: 'SENT',
    bodySnapshot: '<p>nội dung</p>',
    canReply: true,
    sender: { userId: 5, fullName: 'Người Gửi', email: 'sender@fpt.edu.vn' },
    recipients: [
      { recipientEmail: 'to@fpt.edu.vn', recipientName: 'Tới', recipientType: 'TO', deliveryStatus: 'SENT' },
      { recipientEmail: 'cc@fpt.edu.vn', recipientName: 'Cc', recipientType: 'CC', deliveryStatus: 'SENT' },
      { recipientEmail: BCC_ADDRESS, recipientName: 'Ẩn', recipientType: 'BCC', deliveryStatus: 'SENT' },
    ],
    ...overrides,
  },
});

const openReply = async () => {
  fireEvent.click(await screen.findByRole('button', { name: 'Phản hồi' }));
  return screen.findByLabelText('reply-body');
};

beforeEach(() => {
  vi.clearAllMocks();
  getEmailDetail.mockResolvedValue(detail());
  getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
  replyEmail.mockResolvedValue({ data: { success: true } });
  replyAllEmail.mockResolvedValue({ data: { success: true } });
});

describe('SentEmailDetail — reply is reachable', () => {
  it('offers the reply button when the server says this viewer may reply', async () => {
    render(<SentEmailDetail />);
    expect(await screen.findByRole('button', { name: 'Phản hồi' })).toBeInTheDocument();
  });

  it('hides the reply button when the server says otherwise', async () => {
    getEmailDetail.mockResolvedValue(detail({ canReply: false }));
    render(<SentEmailDetail />);

    await screen.findByText('Thư mời');
    expect(screen.queryByRole('button', { name: 'Phản hồi' })).not.toBeInTheDocument();
  });

  it('opens the composer on the message being viewed, addressed to its sender', async () => {
    render(<SentEmailDetail />);
    await openReply();

    // The TO shown is the address the server will resolve from originalEmailId, not one the page picked.
    const to = screen.getByText('Tới:').parentElement as HTMLElement;
    expect(to).toHaveTextContent('sender@fpt.edu.vn');
    expect(to).toHaveTextContent('hệ thống xác định');
  });
});

describe('SentEmailDetail — a reply inherits nothing from the original', () => {
  it('leaves CC and BCC empty even though the original had both', async () => {
    render(<SentEmailDetail />);
    await openReply();

    // The addresses are on screen in the header of the message being read; what matters is that none of
    // them was pulled into the reply's own fields.
    expect(screen.queryAllByTestId('chip-CC')).toHaveLength(0);
    expect(screen.queryAllByTestId('chip-BCC')).toHaveLength(0);
    expect(screen.queryByLabelText('CC')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('BCC')).not.toBeInTheDocument();
  });

  it('never puts the original blind copy into the request', async () => {
    render(<SentEmailDetail />);
    const body = await openReply();
    fireEvent.change(body, { target: { value: 'Cảm ơn anh' } });
    fireEvent.click(screen.getByRole('button', { name: /^Gửi$/ }));

    await waitFor(() => expect(replyEmail).toHaveBeenCalledTimes(1));
    const sent = JSON.stringify(replyEmail.mock.calls[0][0]);
    expect(sent).not.toContain(BCC_ADDRESS);
    expect(sent).not.toContain('cc@fpt.edu.vn');
    expect(sent).not.toContain('to@fpt.edu.vn');
    expect(replyEmail.mock.calls[0][0]).toMatchObject({ originalEmailId: 77, cc: [], bcc: [] });
  });

  it('reloads the message and closes the composer after a successful reply', async () => {
    render(<SentEmailDetail />);
    const body = await openReply();
    fireEvent.change(body, { target: { value: 'Cảm ơn anh' } });

    getEmailDetail.mockClear();
    fireEvent.click(screen.getByRole('button', { name: /^Gửi$/ }));

    await waitFor(() => expect(screen.queryByLabelText('reply-body')).not.toBeInTheDocument());
    expect(getEmailDetail).toHaveBeenCalled();
  });

  it('keeps the composer open with its content when the reply is refused', async () => {
    replyEmail.mockRejectedValue({ response: { data: { message: 'Không thể phản hồi email hệ thống tự động.' } } });

    render(<SentEmailDetail />);
    const body = await openReply();
    fireEvent.change(body, { target: { value: 'Cảm ơn anh' } });
    fireEvent.click(screen.getByRole('button', { name: /^Gửi$/ }));

    await screen.findByText('Không thể phản hồi email hệ thống tự động.');
    expect(screen.getByLabelText('reply-body')).toHaveValue('Cảm ơn anh');
  });
});

/**
 * Reply All (G11-H).
 *
 * The client sends a MODE, never a recipient list. That is the whole safety property: if the client
 * assembled "everyone who was on the original", it would be naming addresses it read from the detail
 * response — and a client that can name recipients can name one who was on BCC.
 */
describe('SentEmailDetail — reply all', () => {
  const openReplyAll = async () => {
    fireEvent.click(await screen.findByTestId('reply-all'));
    return screen.findByLabelText('reply-body');
  };

  it('offers reply all to a viewer who may reply', async () => {
    render(<SentEmailDetail />);
    expect(await screen.findByTestId('reply-all')).toBeInTheDocument();
  });

  it('offers no reply all when the server says this viewer may not reply', async () => {
    getEmailDetail.mockResolvedValue(detail({ canReply: false }));

    render(<SentEmailDetail />);
    await screen.findByText('Thư mời');

    expect(screen.queryByTestId('reply-all')).not.toBeInTheDocument();
  });

  it('posts to the reply-all route, not the reply route', async () => {
    render(<SentEmailDetail />);
    const body = await openReplyAll();
    fireEvent.change(body, { target: { value: '<p>Chúng tôi xác nhận.</p>' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/ }));

    await waitFor(() => expect(replyAllEmail).toHaveBeenCalledTimes(1));
    expect(replyEmail).not.toHaveBeenCalled();
  });

  it('sends no recipient list of its own — only the parent id and this author’s copies', async () => {
    render(<SentEmailDetail />);
    const body = await openReplyAll();
    fireEvent.change(body, { target: { value: '<p>Xác nhận.</p>' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/ }));

    await waitFor(() => expect(replyAllEmail).toHaveBeenCalledTimes(1));

    const payload = replyAllEmail.mock.calls[0][0];
    expect(payload).toMatchObject({ originalEmailId: 77, cc: [], bcc: [] });
    // No `to`: the server resolves it. A client-supplied TO is a redirected reply.
    expect(payload).not.toHaveProperty('to');
  });

  it('never carries the original blind copy into the request', async () => {
    render(<SentEmailDetail />);
    const body = await openReplyAll();
    fireEvent.change(body, { target: { value: '<p>Xác nhận.</p>' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/ }));

    await waitFor(() => expect(replyAllEmail).toHaveBeenCalledTimes(1));
    expect(JSON.stringify(replyAllEmail.mock.calls[0][0])).not.toContain(BCC_ADDRESS);
  });

  it('carries an idempotency key so a retried reply all is not a second message', async () => {
    render(<SentEmailDetail />);
    const body = await openReplyAll();
    fireEvent.change(body, { target: { value: '<p>Xác nhận.</p>' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/ }));

    await waitFor(() => expect(replyAllEmail).toHaveBeenCalledTimes(1));

    const key = replyAllEmail.mock.calls[0][1];
    expect(typeof key).toBe('string');
    expect((key as string).length).toBeGreaterThanOrEqual(8);
  });

  it('reply and reply all use different keys for the same message', async () => {
    render(<SentEmailDetail />);

    const bodyAll = await openReplyAll();
    fireEvent.change(bodyAll, { target: { value: '<p>Một.</p>' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/ }));
    await waitFor(() => expect(replyAllEmail).toHaveBeenCalledTimes(1));

    const bodyOne = await openReply();
    fireEvent.change(bodyOne, { target: { value: '<p>Hai.</p>' } });
    fireEvent.click(screen.getByRole('button', { name: /Gửi/ }));
    await waitFor(() => expect(replyEmail).toHaveBeenCalledTimes(1));

    // They send to different people, so sharing a key would let one be mistaken for the other.
    expect(replyAllEmail.mock.calls[0][1]).not.toBe(replyEmail.mock.calls[0][1]);
  });
});
