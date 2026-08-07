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
const REPORT_NAME = 'PEMS_Schedule_Report_VR-9001.pdf';

/** The message the setup-progress prepare endpoint hands the composer. */
const PREPARED = {
  subject: 'Cập nhật công tác chuẩn bị',
  bodyHtml: '<p>noi dung</p>',
  recipients: [
    { email: 'guest@partner.example', name: 'Guest', recipientType: 'TO' as const, displayOrder: 0 },
    { email: 'ic.staff@fpt.edu.vn', name: 'IC', recipientType: 'CC' as const, displayOrder: 1 },
  ],
};

/** The Schedule Report, as the flow now hands it over: an attachment, not merely an id. */
const REPORT_ATTACHMENT = {
  fileId: REPORT_FILE_ID,
  name: REPORT_NAME,
  mimeType: 'application/pdf',
  size: null,
};

beforeEach(() => {
  vi.clearAllMocks();
  getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
  getEmailTemplateList.mockResolvedValue({ data: { items: [] } });
  // Echoes the ATTACHMENT list back, so the preview reflects what was actually posted rather than a
  // constant — that count is what several tests below are about. The body stays a fixed server-side
  // string on purpose: the preview must show what the SERVER returned, and a mock that echoed the
  // caller's body would make a preview rendering the local one look correct.
  previewEmail.mockImplementation((payload: any) => Promise.resolve({
    data: {
      subject: payload.subject,
      body: '<p>noi dung</p>',
      isHtml: true,
      to: ['Guest <guest@partner.example>'],
      cc: ['IC <ic.staff@fpt.edu.vn>'],
      bcc: [] as string[],
      attachments: (payload.attachments ?? []).map((a: any) => a.displayName ?? `tệp #${a.fileId}`),
    },
  }));
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
      initialAttachments={[REPORT_ATTACHMENT]}
      onSend={onSend}
      {...overrides}
    />,
  );
  return { ...utils, onSend };
}

/** Removes an attachment through the UI, confirmation and all. */
async function removeTheReport() {
  fireEvent.click(await screen.findByTestId('attachment-remove'));
  await screen.findByTestId('confirm-dialog');
  fireEvent.click(screen.getByText('__confirm__'));
}

/** The file ids in the most recent send payload. */
function sentFileIds(onSend: ReturnType<typeof vi.fn>): number[] {
  const payload = onSend.mock.calls.at(-1)![0];
  return payload.attachments.map((a: { fileId: number }) => a.fileId);
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

  /**
   * A body the SYNC wrote is not something the author typed.
   *
   * The overwrite warning exists to protect the author's own words, so it must be armed by their edits
   * and disarmed by a rebuild — otherwise the second sync of a session asks permission to overwrite text
   * the first sync produced, which the author never wrote and has no reason to be asked about. The
   * generated baseline has to move with each rebuild for that to hold.
   *
   * This is the same distinction the editor now makes with Quill's `source`: an application write is not
   * an edit. Pinned here at the composer level too, because the two are enforced independently — the
   * editor decides what counts as a keystroke, this decides what counts as the text to compare against.
   */
  it('treats a synced body as the new baseline rather than as the Host\'s writing', async () => {
    const onRefreshRequiredAttachment = vi.fn()
      .mockResolvedValueOnce({ fileId: 901, name: 'report.pdf', bodyHtml: '<p>fresh</p>', warnings: [] })
      .mockResolvedValueOnce({ fileId: 902, name: 'report.pdf', bodyHtml: '<p>fresher</p>', warnings: [] });
    renderSetupProgress({ onRefreshRequiredAttachment });

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));
    await waitFor(() => expect(onRefreshRequiredAttachment).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toContain('fresh'));

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    // Straight through: no "sẽ bị thay thế" over words the author never wrote.
    await waitFor(() => expect(onRefreshRequiredAttachment).toHaveBeenCalledTimes(2));
    expect(screen.queryByTestId('confirm-dialog')).not.toBeInTheDocument();
  });
});

/**
 * The Schedule Report as a DEFAULT attachment.
 *
 * The bug these replace was silent in exactly the way that matters. The flow passed the report's id as
 * `lockedAttachmentFileIds` and nothing else — which names an id without attaching anything — so the
 * composer opened holding a lock on a file it was not carrying: the strip said "Chưa có tệp đính kèm",
 * the payload went out with `attachments: []`, and the send was then refused by the backend for omitting
 * the report the screen had just claimed was mandatory. Every test here is written against the payload or
 * the DOM rather than against the prop, because the prop was never the thing that was wrong.
 */
describe('EmailComposeModal — the Schedule Report is attached by default and removable', () => {
  it('opens carrying the report, not merely knowing its id', async () => {
    renderSetupProgress();

    const chip = await screen.findByTestId('attachment');
    expect(chip).toHaveTextContent(REPORT_NAME);
    expect(screen.queryByText(/Chưa có tệp đính kèm/)).not.toBeInTheDocument();
  });

  it('sends the report when the Host leaves it alone', async () => {
    const { onSend } = renderSetupProgress();
    await screen.findByTestId('attachment');

    await previewAndSend();

    await waitFor(() => expect(onSend).toHaveBeenCalledTimes(1));
    expect(sentFileIds(onSend)).toEqual([REPORT_FILE_ID]);
    expect(onSend.mock.calls[0][0].attachments[0].attachmentType).toBe('ATTACHMENT');
  });

  it('lets the Host remove the report', async () => {
    renderSetupProgress();
    await screen.findByTestId('attachment');

    await removeTheReport();

    // Gone from the strip, and no "Bắt buộc" badge ever stood in for a delete button.
    await waitFor(() => expect(screen.queryByTestId('attachment')).not.toBeInTheDocument());
    expect(screen.queryByTestId('locked-attachment')).not.toBeInTheDocument();
  });

  it('previews and sends with no attachments once the report is removed', async () => {
    const { onSend } = renderSetupProgress();
    await screen.findByTestId('attachment');
    await removeTheReport();
    await waitFor(() => expect(screen.queryByTestId('attachment')).not.toBeInTheDocument());

    await previewAndSend();

    // The preview is what the server would send, and it agrees: nothing attached.
    expect(previewEmail.mock.calls.at(-1)![0].attachments).toEqual([]);
    await waitFor(() => expect(onSend).toHaveBeenCalledTimes(1));
    expect(sentFileIds(onSend)).toEqual([]);
  });

  it('never re-adds the report at send time just because it was prepared', async () => {
    const { onSend } = renderSetupProgress();
    await screen.findByTestId('attachment');
    await removeTheReport();

    await previewAndSend();

    // The payload comes from the attachment STATE. Rebuilding it from the prepare response — which
    // still names a report — is how a removed file comes back without anyone asking for it.
    await waitFor(() => expect(onSend).toHaveBeenCalled());
    expect(sentFileIds(onSend)).not.toContain(REPORT_FILE_ID);
  });

  it('opens with nothing attached when the backend could not produce a report', async () => {
    const { onSend } = renderSetupProgress({
      initialAttachments: undefined,
      notices: ['Không thể tạo Báo cáo Lịch trình do kho tệp hiện không khả dụng.'],
    });

    // The composer opens, says why, and is sendable — the whole point of the report being optional.
    expect(await screen.findByTestId('compose-notices'))
      .toHaveTextContent('Không thể tạo Báo cáo Lịch trình');
    expect(screen.queryByTestId('attachment')).not.toBeInTheDocument();

    await previewAndSend();
    await waitFor(() => expect(onSend).toHaveBeenCalledTimes(1));
    expect(sentFileIds(onSend)).toEqual([]);
  });

  it('does not fabricate an attachment with a zero id or an empty name', async () => {
    renderSetupProgress({ initialAttachments: undefined });
    await waitFor(() => expect(screen.getByLabelText('body')).toBeInTheDocument());

    expect(screen.queryByTestId('attachment')).not.toBeInTheDocument();
    expect(screen.getByText(/Chưa có tệp đính kèm/)).toBeInTheDocument();
  });

  it('leaves callers that pass no attachments exactly as they were', async () => {
    render(
      <EmailComposeModal
        open
        onClose={vi.fn()}
        pushToast={vi.fn()}
        initialSubject="Xin chào"
        initialBodyHtml="<p>thân gửi</p>"
        initialRecipients="someone@fpt.edu.vn"
      />,
    );

    await waitFor(() => expect(screen.getByLabelText('body')).toBeInTheDocument());
    expect(screen.getByText(/Chưa có tệp đính kèm/)).toBeInTheDocument();
    // No sync control either: it is offered only to a caller that supplied a rebuild.
    expect(screen.queryByTestId('refresh-required-attachment')).not.toBeInTheDocument();
  });

  it('does not carry one session\'s attachments into the next', async () => {
    const { rerender } = render(
      <EmailComposeModal open onClose={vi.fn()} pushToast={vi.fn()}
        initialSubject="A" initialBodyHtml="<p>a</p>" initialAttachments={[REPORT_ATTACHMENT]} />,
    );
    await screen.findByTestId('attachment');

    // Close, then open again on a message that has no report of its own.
    rerender(
      <EmailComposeModal open={false} onClose={vi.fn()} pushToast={vi.fn()}
        initialSubject="B" initialBodyHtml="<p>b</p>" />,
    );
    rerender(
      <EmailComposeModal open onClose={vi.fn()} pushToast={vi.fn()}
        initialSubject="B" initialBodyHtml="<p>b</p>" />,
    );

    await waitFor(() => expect(screen.queryByTestId('attachment')).not.toBeInTheDocument());
  });
});

/**
 * "Đồng bộ dữ liệu mới nhất" against an attachment list the Host has been editing.
 *
 * The rule is one sentence — replace what this composer generated, keep what the author added — and every
 * case below is a way of getting it wrong: duplicating the report, deleting the author's files with it, or
 * leaving a report from an older snapshot on screen after a sync that could not produce a new one.
 */
describe('EmailComposeModal — syncing the generated attachment', () => {
  const CUSTOM = { fileId: 500, name: 'danh-sach-khach.xlsx', size: 1024, mimeType: null };

  /** Puts a file the AUTHOR added into the list, through the upload path they would use. */
  async function attachCustomFile() {
    const { filesApi } = await import('../../../shared/api/filesApi');
    (filesApi.upload as any).mockResolvedValue({
      fileId: CUSTOM.fileId, originalFilename: CUSTOM.name, fileSize: CUSTOM.size, mimeType: 'application/octet-stream',
    });
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    fireEvent.change(input, { target: { files: [new File(['x'], CUSTOM.name)] } });
    await screen.findByText(CUSTOM.name);
  }

  function attachedNames(): string[] {
    return screen.queryAllByTestId('attachment-name').map(el => el.textContent ?? '');
  }

  it('replaces the report rather than adding a second one', async () => {
    const onRefreshRequiredAttachment = vi.fn().mockResolvedValue({
      fileId: 901, name: 'report-901.pdf', bodyHtml: '<p>fresh</p>', warnings: [],
    });
    const { onSend } = renderSetupProgress({ onRefreshRequiredAttachment });
    await screen.findByTestId('attachment');

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));
    await waitFor(() => expect(attachedNames()).toEqual(['report-901.pdf']));

    await previewAndSend();
    await waitFor(() => expect(onSend).toHaveBeenCalled());
    expect(sentFileIds(onSend)).toEqual([901]);
  });

  it('keeps the files the author added', async () => {
    const onRefreshRequiredAttachment = vi.fn().mockResolvedValue({
      fileId: 901, name: 'report-901.pdf', bodyHtml: '<p>fresh</p>', warnings: [],
    });
    const { onSend } = renderSetupProgress({ onRefreshRequiredAttachment });
    await screen.findByTestId('attachment');
    await attachCustomFile();

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));
    await waitFor(() => expect(attachedNames()).toEqual(['report-901.pdf', CUSTOM.name]));

    await previewAndSend();
    await waitFor(() => expect(onSend).toHaveBeenCalled());
    expect(sentFileIds(onSend)).toEqual([901, CUSTOM.fileId]);
  });

  it('brings the report back when the Host asks for a fresh one after removing it', async () => {
    const onRefreshRequiredAttachment = vi.fn().mockResolvedValue({
      fileId: 901, name: 'report-901.pdf', bodyHtml: '<p>fresh</p>', warnings: [],
    });
    renderSetupProgress({ onRefreshRequiredAttachment });
    await screen.findByTestId('attachment');
    await removeTheReport();
    await waitFor(() => expect(screen.queryByTestId('attachment')).not.toBeInTheDocument());

    // Pressing "đồng bộ" is an explicit request for a current snapshot, so it produces one. Removing it
    // again is one click; sending an update with no schedule after asking for one is not recoverable.
    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    await waitFor(() => expect(attachedNames()).toEqual(['report-901.pdf']));
  });

  it('leaves exactly one report after repeated syncs', async () => {
    const onRefreshRequiredAttachment = vi.fn()
      .mockResolvedValueOnce({ fileId: 901, name: 'report-901.pdf', bodyHtml: '<p>v2</p>', warnings: [] })
      .mockResolvedValueOnce({ fileId: 902, name: 'report-902.pdf', bodyHtml: '<p>v3</p>', warnings: [] });
    const { onSend } = renderSetupProgress({ onRefreshRequiredAttachment });
    await screen.findByTestId('attachment');

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));
    await waitFor(() => expect(attachedNames()).toEqual(['report-901.pdf']));
    fireEvent.click(screen.getByTestId('refresh-required-attachment'));
    await waitFor(() => expect(attachedNames()).toEqual(['report-902.pdf']));

    await previewAndSend();
    await waitFor(() => expect(onSend).toHaveBeenCalled());
    expect(sentFileIds(onSend)).toEqual([902]);
  });

  it('drops the stale report when the rebuild produced no new one', async () => {
    const warning = 'Kết nối Google Drive cần được xác thực lại. Báo cáo Lịch trình chưa được đính kèm.';
    const onRefreshRequiredAttachment = vi.fn().mockResolvedValue({
      fileId: null, name: null, bodyHtml: '<p>fresh</p>', warnings: [warning],
    });
    const { onSend } = renderSetupProgress({ onRefreshRequiredAttachment });
    await screen.findByTestId('attachment');
    await attachCustomFile();

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    // The body was rebuilt, so the old PDF describes an older moment: keeping it would present it as
    // the current snapshot. The author's own file is untouched, and the composer says what happened.
    await waitFor(() => expect(attachedNames()).toEqual([CUSTOM.name]));
    expect(await screen.findByTestId('compose-notices')).toHaveTextContent('xác thực lại');
    expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toContain('fresh');

    // And it is still a sendable message.
    await previewAndSend();
    await waitFor(() => expect(onSend).toHaveBeenCalled());
    expect(sentFileIds(onSend)).toEqual([CUSTOM.fileId]);
  });

  it('clears a stale warning once a sync succeeds', async () => {
    const onRefreshRequiredAttachment = vi.fn().mockResolvedValue({
      fileId: 901, name: 'report-901.pdf', bodyHtml: '<p>fresh</p>', warnings: [],
    });
    renderSetupProgress({
      onRefreshRequiredAttachment,
      initialAttachments: undefined,
      notices: ['Không thể tạo Báo cáo Lịch trình do kho tệp hiện không khả dụng.'],
    });
    await screen.findByTestId('compose-notices');

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    // A "báo cáo chưa được tạo" panel sitting above a composer that has just generated one is its own
    // kind of wrong.
    await waitFor(() => expect(attachedNames()).toEqual(['report-901.pdf']));
    expect(screen.queryByTestId('compose-notices')).not.toBeInTheDocument();
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
