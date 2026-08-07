/**
 * The composer after drafts were removed, and the opt-in extensions the setup-progress flow needs
 * from it.
 *
 * Each is asserted on the payload or the DOM rather than on the prop being passed, because the risk
 * they carry is silent: a locked attachment that can still be deleted looks fine until an email goes
 * out without its report, and a send that quietly falls back to the generic endpoint looks fine until
 * a replaced host mails the guest.
 *
 * The behaviours the removal of drafts made load-bearing are pinned here too: a provider failure must
 * leave the message on screen (there is no draft to recover it from), closing a dirty composer must
 * ask (nothing is saved), and every attempt of one composer session must carry ONE idempotency key
 * (the DRAFT → SENT claim is gone, so the key is the only double-click protection left).
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const sendEmail = vi.fn();
const previewEmail = vi.fn();
const getRecipientLimits = vi.fn();
const getEmailTemplateList = vi.fn();

vi.mock('../api/emailsApi', () => ({
  emailsApi: {
    sendEmail: (...a: unknown[]) => sendEmail(...a),
    previewEmail: (...a: unknown[]) => previewEmail(...a),
    getRecipientLimits: (...a: unknown[]) => getRecipientLimits(...a),
    getEmailTemplateList: (...a: unknown[]) => getEmailTemplateList(...a),
  },
}));

vi.mock('../../../shared/api/filesApi', () => ({ filesApi: { upload: vi.fn(), download: vi.fn() } }));

// Renders the wording as well as the button: what the confirmation SAYS is the subject of the
// overwrite and close tests below, not merely that some dialog appeared.
vi.mock('../../../components/modals/ConfirmModal', () => ({
  ConfirmModal: ({ isOpen, onConfirm, onClose, title, message }: {
    isOpen: boolean; onConfirm: () => void; onClose: () => void; title?: string; message?: string;
  }) =>
    isOpen ? (
      <div data-testid="confirm-dialog">
        <p data-testid="confirm-title">{title}</p>
        <p data-testid="confirm-message">{message}</p>
        <button type="button" onClick={onConfirm}>__confirm__</button>
        <button type="button" onClick={onClose}>__cancel__</button>
      </div>
    ) : null,
}));
vi.mock('../../../shared/auth/authStorage', () => ({ authStorage: { getAccessToken: () => 'test-token' } }));

vi.mock('react-quill-new', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string, d: unknown, s: string) => void }) => (
    <textarea aria-label="body" value={value} onChange={e => onChange(e.target.value, undefined, 'user')} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { EmailComposeModal } from '../components/EmailComposeModal';

const REPORT_FILE_ID = 900;

/** The message the setup-progress prepare endpoint hands the composer. */
const PREPARED = {
  subject: 'Cập nhật công tác chuẩn bị',
  bodyHtml: '<p>noi dung</p>',
  recipients: [
    { email: 'guest@partner.example', name: 'Guest', recipientType: 'TO' as const, displayOrder: 0 },
    { email: 'ic.staff@fpt.edu.vn', name: 'IC', recipientType: 'CC' as const, displayOrder: 1 },
  ],
};

const PREVIEW_OK = {
  data: {
    subject: PREPARED.subject,
    body: '<p>noi dung</p>',
    isHtml: true,
    to: ['Guest <guest@partner.example>'],
    cc: ['IC <ic.staff@fpt.edu.vn>'],
    bcc: [] as string[],
    attachments: [] as string[],
  },
};

beforeEach(() => {
  vi.clearAllMocks();
  getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
  getEmailTemplateList.mockResolvedValue({ data: { items: [] } });
  previewEmail.mockResolvedValue(PREVIEW_OK);
  sendEmail.mockResolvedValue({ data: { success: true, status: 'SENT', message: 'ok', sentEmailId: 5 } });
});

/** Opens the composer as the setup-progress flow does. */
function renderSetupProgress(overrides: Record<string, unknown> = {}) {
  const onSend = vi.fn().mockResolvedValue({ success: true, message: 'ok' });
  const utils = render(
    <EmailComposeModal
      open
      onClose={vi.fn()}
      pushToast={vi.fn()}
      contextTitle="Gửi cập nhật chuẩn bị"
      initialSubject={PREPARED.subject}
      initialBodyHtml={PREPARED.bodyHtml}
      initialEnvelope={PREPARED.recipients}
      relatedType="VISIT_INSTANCE"
      relatedId={42}
      lockedTemplate
      lockedAttachmentFileIds={[REPORT_FILE_ID]}
      onSend={onSend}
      {...overrides}
    />,
  );
  return { ...utils, onSend };
}

/** Walks the composer from the editor through preview to a confirmed send. */
async function previewAndSend() {
  fireEvent.click(screen.getByTestId('preview-email'));
  await screen.findByTestId('compose-preview');
  fireEvent.click(screen.getByTestId('confirm-send'));
  await screen.findByTestId('confirm-dialog');
  fireEvent.click(screen.getByText('__confirm__'));
}

describe('EmailComposeModal — the setup-progress extensions', () => {
  it('seeds the three groups from the backend envelope, keeping CC as CC', async () => {
    renderSetupProgress();

    // A flat recipient string could not have carried the group, so a server-chosen CC used to arrive
    // as a primary recipient. The chips are asserted per group, which is the distinction at issue.
    await waitFor(() => expect(screen.getByTestId('chip-TO')).toHaveTextContent('guest@partner.example'));
    expect(screen.getByTestId('chip-CC')).toHaveTextContent('ic.staff@fpt.edu.vn');

    fireEvent.click(screen.getByTestId('preview-email'));
    await screen.findByTestId('compose-preview');

    const payload = previewEmail.mock.calls[0][0];
    expect(payload.to.map((r: { email: string }) => r.email)).toEqual(['guest@partner.example']);
    expect(payload.cc.map((r: { email: string }) => r.email)).toEqual(['ic.staff@fpt.edu.vn']);
  });

  it('sends through the caller endpoint rather than the generic one', async () => {
    const { onSend } = renderSetupProgress();
    await previewAndSend();

    await waitFor(() => expect(onSend).toHaveBeenCalledTimes(1));
    // A fallback to the generic route would send a message whose host and stage nobody re-checked.
    expect(sendEmail).not.toHaveBeenCalled();
  });

  it('passes the whole message, not an id, to the caller endpoint', async () => {
    const { onSend } = renderSetupProgress();
    await previewAndSend();

    await waitFor(() => expect(onSend).toHaveBeenCalled());
    const [payload, key] = onSend.mock.calls[0];

    expect(payload.subject).toBe(PREPARED.subject);
    expect(payload.bodyHtml).toContain('noi dung');
    expect(payload.recipients).toHaveLength(2);
    expect(typeof key).toBe('string');
    expect(key.length).toBeGreaterThan(0);
  });

  it('refuses to let the mandatory report be removed', async () => {
    renderSetupProgress();
    // The locked attachment renders without a remove control; a report that can be deleted is a
    // message that goes out contradicting its own body.
    await waitFor(() => expect(screen.queryByTestId('attachment')).not.toBeInTheDocument());
  });

  it('warns before a sync overwrites what the Host has typed', async () => {
    const onRefreshRequiredAttachment = vi.fn().mockResolvedValue({
      fileId: 901, name: 'report.pdf', bodyHtml: '<p>fresh</p>',
    });
    renderSetupProgress({ onRefreshRequiredAttachment });

    // Edit the body first, so there is something of the author's to lose.
    fireEvent.change(screen.getByLabelText('body'), { target: { value: '<p>Host wrote this</p>' } });

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    const message = await screen.findByTestId('confirm-message');
    expect(message.textContent).toContain('sẽ bị thay thế');
    expect(onRefreshRequiredAttachment).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText('__confirm__'));
    await waitFor(() => expect(onRefreshRequiredAttachment).toHaveBeenCalledTimes(1));
  });

  it('syncs without asking when the body is still the generated text', async () => {
    const onRefreshRequiredAttachment = vi.fn().mockResolvedValue({
      fileId: 901, name: 'report.pdf', bodyHtml: '<p>fresh</p>',
    });
    renderSetupProgress({ onRefreshRequiredAttachment });

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    await waitFor(() => expect(onRefreshRequiredAttachment).toHaveBeenCalledTimes(1));
    expect(screen.queryByTestId('confirm-dialog')).not.toBeInTheDocument();
  });
});

describe('EmailComposeModal — what the removal of drafts made load-bearing', () => {
  it('keeps the message on screen when the send fails', async () => {
    const onSend = vi.fn().mockRejectedValue({ response: { data: { message: 'SMTP down' } } });
    renderSetupProgress({ onSend });

    await previewAndSend();

    // The modal is still up, back on the editor, with every word the Host wrote. There is no draft to
    // recover this from — closing here would destroy the message.
    await waitFor(() => expect(screen.getByLabelText('body')).toBeInTheDocument());
    expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toContain('noi dung');
    expect(screen.getByDisplayValue(PREPARED.subject)).toBeInTheDocument();
  });

  it('reuses one idempotency key across a retry of the same session', async () => {
    const onSend = vi.fn()
      .mockRejectedValueOnce({ response: { data: { message: 'SMTP down' } } })
      .mockResolvedValueOnce({ success: true });
    renderSetupProgress({ onSend });

    await previewAndSend();
    await waitFor(() => expect(onSend).toHaveBeenCalledTimes(1));

    await previewAndSend();
    await waitFor(() => expect(onSend).toHaveBeenCalledTimes(2));

    // Same key both times: the server recognises the retry as the same message rather than sending a
    // second one. A key minted per attempt would defeat the reservation entirely.
    expect(onSend.mock.calls[0][1]).toBe(onSend.mock.calls[1][1]);
  });

  it('asks before closing once the message has been touched', async () => {
    const onClose = vi.fn();
    renderSetupProgress({ onClose });

    fireEvent.change(screen.getByLabelText('body'), { target: { value: '<p>edited</p>' } });
    fireEvent.click(screen.getByLabelText('Đóng'));

    const title = await screen.findByTestId('confirm-title');
    expect(title.textContent).toContain('Đóng email đang soạn');
    // Not closed yet — "Tiếp tục chỉnh sửa" has to be a real option.
    expect(onClose).not.toHaveBeenCalled();

    fireEvent.click(screen.getByText('__cancel__'));
    expect(onClose).not.toHaveBeenCalled();
  });

  it('closes without a question when nothing has been touched', async () => {
    const onClose = vi.fn();
    renderSetupProgress({ onClose });
    await waitFor(() => expect(screen.getByLabelText('body')).toBeInTheDocument());

    fireEvent.click(screen.getByLabelText('Đóng'));

    // A confirmation that appears when there is nothing to confirm is one that stops being read.
    expect(screen.queryByTestId('confirm-dialog')).not.toBeInTheDocument();
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('offers no way to save a draft', async () => {
    renderSetupProgress();
    await waitFor(() => expect(screen.getByLabelText('body')).toBeInTheDocument());

    expect(screen.queryByText(/Lưu nháp/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Huỷ nháp/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Đã lưu nháp/i)).not.toBeInTheDocument();
  });
});

describe('EmailComposeModal — the generic composer', () => {
  /** With none of the setup-progress props, the modal behaves as the mailbox screen has always seen it. */
  function renderPlain(overrides: Record<string, unknown> = {}) {
    return render(
      <EmailComposeModal
        open
        onClose={vi.fn()}
        pushToast={vi.fn()}
        initialSubject="Xin chào"
        initialBodyHtml="<p>thân gửi</p>"
        initialRecipients="someone@fpt.edu.vn"
        {...overrides}
      />,
    );
  }

  it('previews against the backend before offering to send', async () => {
    renderPlain();

    fireEvent.click(screen.getByTestId('preview-email'));

    await screen.findByTestId('compose-preview');
    expect(previewEmail).toHaveBeenCalledTimes(1);
    // The preview shows the body the SERVER returned, not the local one — the two differ whenever the
    // backend sanitiser removes something.
    expect(screen.getByTestId('compose-preview-body').innerHTML).toContain('noi dung');
  });

  it('sends through the generic endpoint with an idempotency key', async () => {
    renderPlain();
    await previewAndSend();

    await waitFor(() => expect(sendEmail).toHaveBeenCalledTimes(1));
    const [payload, key] = sendEmail.mock.calls[0];
    expect(payload.subject).toBe('Xin chào');
    expect(payload.to[0].email).toBe('someone@fpt.edu.vn');
    expect(typeof key).toBe('string');
  });

  it('does not send when the preview was refused', async () => {
    previewEmail.mockRejectedValueOnce({ response: { data: { message: 'Tệp không đọc được' } } });
    renderPlain();

    fireEvent.click(screen.getByTestId('preview-email'));

    // No preview step means no confirm step means no send: a refusal here is the refusal the send
    // would have given.
    await waitFor(() => expect(previewEmail).toHaveBeenCalled());
    expect(screen.queryByTestId('compose-preview')).not.toBeInTheDocument();
    expect(sendEmail).not.toHaveBeenCalled();
  });
});
