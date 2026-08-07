/**
 * G6.3 — the compose modal on its real path: EmailComposeModal → emailsApi (preview, then send).
 *
 * These assert the payload that actually leaves the component, because the defect this replaces was
 * invisible in the UI: the screen could show a CC while the request stamped every recipient 'TO'.
 * Rendering alone would not have caught it; only the payload does.
 *
 * The path changed when drafts were removed — the payload used to go to `emailDraftsApi.createDraft`
 * and be sent by id — but the rule being protected did not: a CC the screen collected must leave as a
 * CC. The draft-restore and autosave suites that used to sit here are gone with the feature they
 * described; what remains is what still has to be true.
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

// Auto-confirm: these tests are about the payload, not about the confirmation dialog.
vi.mock('../../../components/modals/ConfirmModal', () => ({
  ConfirmModal: ({ isOpen, onConfirm }: { isOpen: boolean; onConfirm: () => void }) =>
    isOpen ? <button type="button" onClick={onConfirm}>__confirm__</button> : null,
}));
vi.mock('../../../shared/auth/authStorage', () => ({ authStorage: { getAccessToken: () => 'test-token' } }));

// The rich-text editor is not what these tests are about; a plain textarea keeps them fast and stable.
vi.mock('react-quill-new', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string, d: unknown, s: string) => void }) => (
    <textarea aria-label="body" value={value} onChange={e => onChange(e.target.value, undefined, 'user')} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { EmailComposeModal } from '../components/EmailComposeModal';

const flushLimit = async () =>
  await waitFor(() => expect(getRecipientLimits).toHaveBeenCalled());

const renderModal = (props: Record<string, unknown> = {}) =>
  render(<EmailComposeModal open onClose={vi.fn()} pushToast={vi.fn()} {...props} />);

/** Types an address into a named group and commits it as a chip. */
const addRecipient = (group: 'Đến' | 'CC' | 'BCC', email: string) => {
  const field = screen.getByLabelText(group);
  fireEvent.change(field, { target: { value: email } });
  fireEvent.keyDown(field, { key: 'Enter' });
};

const openGroup = (label: 'Thêm CC' | 'Thêm BCC') =>
  fireEvent.click(screen.getByRole('button', { name: label }));

beforeEach(() => {
  vi.clearAllMocks();
  getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
  getEmailTemplateList.mockResolvedValue({ data: { items: [] } });
  previewEmail.mockResolvedValue({
    data: { subject: 'Chủ đề', body: '<p>x</p>', isHtml: true, to: [], cc: [], bcc: [], attachments: [] },
  });
  sendEmail.mockResolvedValue({ data: { sentEmailId: 9, status: 'SENT', success: true, message: 'ok' } });
});

describe('recipient groups', () => {
  it('shows TO always and reveals CC/BCC on demand', async () => {
    renderModal();
    await flushLimit();

    expect(screen.getByLabelText('Đến')).toBeInTheDocument();
    expect(screen.queryByLabelText('CC')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('BCC')).not.toBeInTheDocument();

    openGroup('Thêm CC');
    openGroup('Thêm BCC');
    expect(screen.getByLabelText('CC')).toBeInTheDocument();
    expect(screen.getByLabelText('BCC')).toBeInTheDocument();
  });

  it('keeps CC/BCC addresses when the field is collapsed and reopened', async () => {
    renderModal();
    await flushLimit();

    openGroup('Thêm CC');
    addRecipient('CC', 'copy@fpt.vn');
    expect(screen.getByTestId('chip-CC')).toHaveTextContent('copy@fpt.vn');

    fireEvent.click(screen.getByRole('button', { name: 'Thu gọn CC' }));
    expect(screen.queryByLabelText('CC')).not.toBeInTheDocument();

    openGroup('Thêm CC');
    expect(screen.getByTestId('chip-CC')).toHaveTextContent('copy@fpt.vn');
  });

  it('refuses an address already present in another group', async () => {
    renderModal();
    await flushLimit();

    addRecipient('Đến', 'same@fpt.vn');
    openGroup('Thêm CC');
    addRecipient('CC', 'SAME@fpt.vn');

    expect(screen.getByRole('alert')).toHaveTextContent('chỉ được thuộc một mục');
    expect(screen.queryByTestId('chip-CC')).not.toBeInTheDocument();
  });
});

describe('payload', () => {
  /** Drives the real send: preview → confirm → handleSend. */
  const send = async () => {
    fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'Chủ đề' } });
    fireEvent.click(screen.getByTestId('preview-email'));
    fireEvent.click(await screen.findByTestId('confirm-send'));
    fireEvent.click(await screen.findByRole('button', { name: '__confirm__' }));
  };

  it('preserves each recipient type instead of stamping everything TO', async () => {
    renderModal();
    await flushLimit();

    addRecipient('Đến', 'to@fpt.vn');
    openGroup('Thêm CC');
    addRecipient('CC', 'cc@fpt.vn');
    openGroup('Thêm BCC');
    addRecipient('BCC', 'bcc@fpt.vn');
    await send();

    await waitFor(() => expect(sendEmail).toHaveBeenCalled());

    const payload = sendEmail.mock.calls.at(-1)![0] as any;
    expect(payload.to.map((r: any) => r.email)).toEqual(['to@fpt.vn']);
    expect(payload.cc.map((r: any) => r.email)).toEqual(['cc@fpt.vn']);
    expect(payload.bcc.map((r: any) => r.email)).toEqual(['bcc@fpt.vn']);
  });

  it('previews the same envelope it would send', async () => {
    renderModal();
    await flushLimit();

    addRecipient('Đến', 'to@fpt.vn');
    openGroup('Thêm CC');
    addRecipient('CC', 'cc@fpt.vn');
    fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'Chủ đề' } });
    fireEvent.click(screen.getByTestId('preview-email'));

    await waitFor(() => expect(previewEmail).toHaveBeenCalled());
    const payload = previewEmail.mock.calls.at(-1)![0] as any;
    // A preview built from a different mapping could show an envelope the send would not produce.
    expect(payload.to.map((r: any) => r.email)).toEqual(['to@fpt.vn']);
    expect(payload.cc.map((r: any) => r.email)).toEqual(['cc@fpt.vn']);
  });

  it('carries an idempotency key on the send', async () => {
    renderModal();
    await flushLimit();
    addRecipient('Đến', 'to@fpt.vn');
    await send();

    await waitFor(() => expect(sendEmail).toHaveBeenCalled());
    const key = sendEmail.mock.calls.at(-1)![1];
    // With no DRAFT → SENT claim left, this header is the double-click protection.
    expect(typeof key).toBe('string');
    expect((key as string).length).toBeGreaterThan(0);
  });

  it('keeps the composed content when the server rejects the envelope', async () => {
    sendEmail.mockRejectedValue({
      response: { data: { errorCode: 'EMAIL_RECIPIENT_INVALID', message: "Địa chỉ email không hợp lệ ở mục CC: 'x'." } },
    });

    renderModal();
    await flushLimit();
    addRecipient('Đến', 'to@fpt.vn');
    openGroup('Thêm CC');
    addRecipient('CC', 'cc@fpt.vn');
    await send();

    // The rejection is shown on the CC field, and nothing the sender typed is lost. There is no draft
    // behind this screen any more, so losing it here would lose it for good.
    expect(await screen.findByText(/không hợp lệ ở mục CC/)).toBeInTheDocument();
    expect(screen.getByTestId('chip-TO')).toHaveTextContent('to@fpt.vn');
    expect(screen.getByTestId('chip-CC')).toHaveTextContent('cc@fpt.vn');
    expect(screen.getByDisplayValue('Chủ đề')).toBeInTheDocument();
  });

  it('shows an unattributable server error at form level rather than guessing a field', async () => {
    sendEmail.mockRejectedValue({ response: { data: { message: 'Lỗi hệ thống.' } } });

    renderModal();
    await flushLimit();
    addRecipient('Đến', 'to@fpt.vn');
    await send();

    expect(await screen.findByText('Lỗi hệ thống.')).toBeInTheDocument();
  });

  it('does not start a second send while one is in flight', async () => {
    let release: (v: unknown) => void = () => {};
    sendEmail.mockReturnValue(new Promise(resolve => { release = resolve; }));

    renderModal();
    await flushLimit();
    addRecipient('Đến', 'to@fpt.vn');
    await send();

    await waitFor(() => expect(sendEmail).toHaveBeenCalledTimes(1));

    // A second confirm while the first request is still open must not issue another one.
    const confirmAgain = screen.queryByRole('button', { name: '__confirm__' });
    if (confirmAgain) fireEvent.click(confirmAgain);
    expect(sendEmail).toHaveBeenCalledTimes(1);

    release({ data: { sentEmailId: 9, status: 'SENT', success: true, message: 'ok' } });
  });
});
