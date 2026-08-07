/**
 * G6.5 — the reply envelope.
 *
 * The rule these tests exist to hold is negative: a reply must not inherit anything from the message it
 * answers, and its BCC must be only what this author typed now. A blind copy restored into a reply would
 * disclose, to every recipient of the new message, who was quietly included on the old one.
 *
 * They also pin the payload shape to `ReplytoEmailCommand`: `originalEmailId`, `body`, `cc`, `bcc` — and
 * no `to`, because the server resolves the addressee from the original message and a client-supplied TO
 * would let a reply be redirected away from the thread.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';

const replyEmail = vi.fn();
const getRecipientLimits = vi.fn();

vi.mock('../api/emailsApi', () => ({
  emailsApi: {
    replyEmail: (...a: unknown[]) => replyEmail(...a),
    getRecipientLimits: (...a: unknown[]) => getRecipientLimits(...a),
  },
}));

vi.mock('react-quill-new', () => ({
  default: ({ value, onChange, readOnly }: { value: string; onChange: (v: string, d: unknown, s: string) => void; readOnly?: boolean }) => (
    <textarea aria-label="reply-body" value={value} readOnly={readOnly}
      onChange={e => onChange(e.target.value, undefined, 'user')} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { ReplyComposer } from '../components/ReplyComposer';

const TO = { email: 'sender@fpt.edu.vn', name: 'Người Gửi' };

const setup = (props: Partial<React.ComponentProps<typeof ReplyComposer>> = {}) => {
  const onReplied = vi.fn();
  const onCancel = vi.fn();
  const view = render(
    <ReplyComposer originalEmailId={77} resolvedTo={TO} onCancel={onCancel} onReplied={onReplied} {...props} />,
  );
  return { ...view, onReplied, onCancel };
};

const typeBody = (text = 'Nội dung phản hồi') =>
  fireEvent.change(screen.getByLabelText('reply-body'), { target: { value: text } });

/** Reveals a group's field and adds one address through the real chip input. */
const addRecipient = async (group: 'CC' | 'BCC', email: string) => {
  const toggle = screen.queryByRole('button', { name: `Thêm ${group}` });
  if (toggle) fireEvent.click(toggle);
  const field = await screen.findByLabelText(group);
  fireEvent.change(field, { target: { value: email } });
  fireEvent.keyDown(field, { key: 'Enter' });
};

const send = () => fireEvent.click(screen.getByRole('button', { name: /^Gửi$/ }));

const payload = () => replyEmail.mock.calls[0][0];

beforeEach(() => {
  vi.clearAllMocks();
  getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
  replyEmail.mockResolvedValue({ data: { success: true } });
});

describe('ReplyComposer — envelope', () => {
  it('shows the resolved TO read-only and never posts it', async () => {
    setup();
    await waitFor(() => expect(getRecipientLimits).toHaveBeenCalled());

    expect(screen.getByText(/sender@fpt\.edu\.vn/)).toBeInTheDocument();
    // No editable TO field exists at all — there is nothing to type an addressee into.
    expect(screen.queryByLabelText('Đến')).not.toBeInTheDocument();

    typeBody();
    send();

    await waitFor(() => expect(replyEmail).toHaveBeenCalledTimes(1));
    expect(payload()).not.toHaveProperty('to');
    expect(payload()).not.toHaveProperty('To');
  });

  it('posts only originalEmailId, body, cc and bcc', async () => {
    setup();
    typeBody('<p>Xin chào</p>');
    await addRecipient('CC', 'cc@fpt.edu.vn');
    await addRecipient('BCC', 'bcc@fpt.edu.vn');
    send();

    await waitFor(() => expect(replyEmail).toHaveBeenCalledTimes(1));
    expect(Object.keys(payload()).sort()).toEqual(['bcc', 'body', 'cc', 'originalEmailId']);
    expect(payload().originalEmailId).toBe(77);
    expect(payload().cc).toEqual([{ email: 'cc@fpt.edu.vn', name: undefined }]);
    expect(payload().bcc).toEqual([{ email: 'bcc@fpt.edu.vn', name: undefined }]);
  });

  it('starts with empty CC and BCC — nothing is carried over from the original message', async () => {
    setup();
    await waitFor(() => expect(getRecipientLimits).toHaveBeenCalled());

    // Both fields start hidden precisely because there is nothing to prefill them with.
    expect(screen.queryByLabelText('CC')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('BCC')).not.toBeInTheDocument();
    expect(screen.queryAllByTestId('chip-CC')).toHaveLength(0);
    expect(screen.queryAllByTestId('chip-BCC')).toHaveLength(0);

    typeBody();
    send();
    await waitFor(() => expect(replyEmail).toHaveBeenCalledTimes(1));
    expect(payload().cc).toEqual([]);
    expect(payload().bcc).toEqual([]);
  });

  it('refuses a CC that duplicates the person being replied to', async () => {
    setup();
    typeBody();
    await addRecipient('CC', TO.email);

    // The chip input refuses it at the field, so it never reaches the envelope.
    expect(await screen.findByRole('alert')).toHaveTextContent(/đã có ở mục khác/i);
    expect(screen.queryAllByTestId('chip-CC')).toHaveLength(0);

    send();
    await waitFor(() => expect(replyEmail).toHaveBeenCalledTimes(1));
    expect(payload().cc).toEqual([]);
  });

  it('counts the server-resolved TO against the recipient limit', async () => {
    getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 2 } });
    setup();
    await screen.findByText('1/2 người nhận');       // the TO already occupies one place

    typeBody();
    await addRecipient('CC', 'a@fpt.edu.vn');
    await screen.findByText('2/2 người nhận');
    await addRecipient('BCC', 'b@fpt.edu.vn');       // 3 with the TO — over the ceiling

    send();
    await waitFor(() =>
      expect(screen.getByText(/Tổng số người nhận \(3\) vượt quá giới hạn cho phép \(2\)/)).toBeInTheDocument());
    expect(replyEmail).not.toHaveBeenCalled();
  });

  it('says so rather than inventing a ceiling when the limit cannot be fetched', async () => {
    getRecipientLimits.mockRejectedValue(new Error('offline'));
    setup();

    await screen.findByText(/Chưa lấy được giới hạn người nhận/);
    typeBody();
    send();
    // Not knowing the ceiling must not block a legitimate reply; the server still enforces it.
    await waitFor(() => expect(replyEmail).toHaveBeenCalledTimes(1));
  });
});

describe('ReplyComposer — submission', () => {
  it('sends once even if the button is pressed repeatedly', async () => {
    let release: (v: unknown) => void = () => {};
    replyEmail.mockImplementation(() => new Promise(resolve => { release = resolve; }));

    setup();
    typeBody();

    // Matches both labels, so the repeat presses land on the same control the user would press again.
    const submit = () => screen.getByRole('button', { name: /gửi/i });
    fireEvent.click(submit());
    fireEvent.click(submit());
    fireEvent.click(submit());

    expect(replyEmail).toHaveBeenCalledTimes(1);
    expect(submit()).toBeDisabled();
    expect(submit()).toHaveTextContent('Đang gửi');

    release({ data: { success: true } });
    await waitFor(() => expect(screen.queryByRole('button', { name: /Đang gửi/ })).not.toBeInTheDocument());
  });

  it('keeps the body, CC and BCC when the server refuses', async () => {
    replyEmail.mockRejectedValue({
      response: { data: { errorCode: 'EMAIL_RECIPIENT_INVALID', message: "Địa chỉ email không hợp lệ ở mục CC: 'x'." } },
    });

    setup();
    typeBody('Bản nháp cần giữ lại');
    await addRecipient('CC', 'cc@fpt.edu.vn');
    await addRecipient('BCC', 'bcc@fpt.edu.vn');
    send();

    await waitFor(() => expect(screen.getByText(/không hợp lệ ở mục CC/)).toBeInTheDocument());

    // Nothing was cleared: the reply is still there to correct and resend.
    expect(screen.getByLabelText('reply-body')).toHaveValue('Bản nháp cần giữ lại');
    expect(within(screen.getByTestId('chip-CC')).getByText('cc@fpt.edu.vn')).toBeInTheDocument();
    expect(within(screen.getByTestId('chip-BCC')).getByText('bcc@fpt.edu.vn')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Gửi$/ })).toBeEnabled();
  });

  it('reports an unattributable refusal at form level rather than on a field', async () => {
    replyEmail.mockRejectedValue({ response: { data: { message: 'Không thể phản hồi email hệ thống tự động.' } } });

    setup();
    typeBody();
    send();

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Không thể phản hồi email hệ thống tự động.');
  });

  it('does not send an empty body', async () => {
    setup();
    await waitFor(() => expect(getRecipientLimits).toHaveBeenCalled());

    expect(screen.getByRole('button', { name: /^Gửi$/ })).toBeDisabled();
    fireEvent.change(screen.getByLabelText('reply-body'), { target: { value: '<p><br></p>' } });
    expect(screen.getByRole('button', { name: /^Gửi$/ })).toBeDisabled();
    expect(replyEmail).not.toHaveBeenCalled();
  });

  it('locks the fields while the reply is in flight', async () => {
    let release: (v: unknown) => void = () => {};
    replyEmail.mockImplementation(() => new Promise(resolve => { release = resolve; }));

    setup();
    typeBody();
    await addRecipient('CC', 'cc@fpt.edu.vn');
    send();

    expect(screen.getByLabelText('reply-body')).toHaveAttribute('readonly');
    expect(screen.getByLabelText('CC')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Hủy' })).toBeDisabled();

    release({ data: { success: true } });
    await waitFor(() => expect(screen.getByLabelText('CC')).toBeEnabled());
  });

  it('reports success to the page only after the server accepted', async () => {
    const { onReplied } = setup();
    typeBody();
    expect(onReplied).not.toHaveBeenCalled();

    send();
    await waitFor(() => expect(onReplied).toHaveBeenCalledTimes(1));
  });
});
