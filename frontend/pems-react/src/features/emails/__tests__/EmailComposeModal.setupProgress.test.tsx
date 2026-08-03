/**
 * The opt-in extensions the setup-progress flow needs from the shared composer.
 *
 * Each is asserted on the payload or the DOM rather than on the prop being passed, because the risk
 * they carry is silent: a locked attachment that can still be deleted looks fine until an email goes
 * out without its report, and a send that quietly falls back to the generic endpoint looks fine until
 * a replaced host mails the guest.
 *
 * The last test in the file is the one that guards everyone else: with none of the new props supplied,
 * the composer must behave exactly as the email-management screens have always seen it.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const createDraft = vi.fn();
const updateDraft = vi.fn();
const sendDraft = vi.fn();
const getDraft = vi.fn();
const discardDraft = vi.fn();
const getRecipientLimits = vi.fn();
const getEmailTemplateList = vi.fn();

vi.mock('../api/emailDraftsApi', () => ({
  emailDraftsApi: {
    createDraft: (...a: unknown[]) => createDraft(...a),
    updateDraft: (...a: unknown[]) => updateDraft(...a),
    sendDraft: (...a: unknown[]) => sendDraft(...a),
    getDraft: (...a: unknown[]) => getDraft(...a),
    discardDraft: (...a: unknown[]) => discardDraft(...a),
  },
}));

vi.mock('../api/emailsApi', () => ({
  emailsApi: {
    getRecipientLimits: (...a: unknown[]) => getRecipientLimits(...a),
    getEmailTemplateList: (...a: unknown[]) => getEmailTemplateList(...a),
  },
}));

vi.mock('../../../shared/api/filesApi', () => ({ filesApi: { upload: vi.fn(), download: vi.fn() } }));

// Renders the wording as well as the button: what the confirmation SAYS is the subject of the
// overwrite tests below, not merely that some dialog appeared.
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
vi.mock('../../../shared/auth/authStorage', () => ({ authStorage: { getToken: () => 'test-token' } }));

vi.mock('react-quill-new', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
    <textarea aria-label="body" value={value} onChange={e => onChange(e.target.value)} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { EmailComposeModal } from '../components/EmailComposeModal';
import i18n from '../../../shared/i18n/config';

const REPORT_FILE_ID = 900;

const STORED_DRAFT = {
  emailDraftId: 77,
  subject: 'Cập nhật công tác chuẩn bị',
  bodyContent: '<p>noi dung</p>',
  recipients: [
    { emailDraftRecipientId: 1, recipientEmail: 'guest@partner.example', recipientName: 'Guest', recipientType: 'TO', displayOrder: 0 },
    { emailDraftRecipientId: 2, recipientEmail: 'ic.staff@fpt.edu.vn', recipientName: 'IC', recipientType: 'CC', displayOrder: 1 },
  ],
  attachments: [
    { emailDraftAttachmentId: 1, fileId: REPORT_FILE_ID, attachmentType: 'ATTACHMENT', displayName: 'PEMS_Schedule_Report_VR-10.pdf', fileSize: 2048, mimeType: 'application/pdf', displayOrder: 0 },
    { emailDraftAttachmentId: 2, fileId: 901, attachmentType: 'ATTACHMENT', displayName: 'ghi-chu.pdf', fileSize: 512, mimeType: 'application/pdf', displayOrder: 1 },
  ],
};

const setupProps = {
  open: true as const,
  onClose: vi.fn(),
  pushToast: vi.fn(),
  initialDraftId: 77,
  lockedTemplate: true,
  lockedAttachmentFileIds: [REPORT_FILE_ID],
  contextTitle: 'Gửi cập nhật chuẩn bị',
};

const renderSetupComposer = (extra: Record<string, unknown> = {}) =>
  render(<EmailComposeModal {...setupProps} {...extra} />);

/** Waits for the stored draft to be hydrated into the form. */
const hydrated = async () =>
  await waitFor(() => expect(screen.getByDisplayValue('Cập nhật công tác chuẩn bị')).toBeTruthy());

beforeEach(() => {
  // These assertions are written against the Vietnamese UI. Pin the language, because i18n falls back
  // to `navigator.language` — which is en-US under jsdom — and the attachment strip is translated now
  // (it used to hard-code Vietnamese, which hid the difference).
  void i18n.changeLanguage('vi');
  vi.clearAllMocks();
  getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 50 } });
  getEmailTemplateList.mockResolvedValue({ data: { items: [{ emailTemplateId: 5, name: 'Mẫu bất kỳ' }] } });
  getDraft.mockResolvedValue(STORED_DRAFT);
  updateDraft.mockResolvedValue({ emailDraftId: 77, recipients: [], attachments: [] });
  sendDraft.mockResolvedValue({ emailDraftId: 77, sentEmailId: 9, status: 'SENT', success: true, draftStatus: 'SENT', message: 'ok' });
});

describe('setup-progress composer', () => {
  it('opens on the stored draft with its recipients back in the groups they came from', async () => {
    renderSetupComposer();
    await hydrated();

    // Chips may render "Name <address>", so match on the address appearing at all rather than on a
    // node whose whole text is the address.
    expect(screen.getByText((_, el) => el?.textContent === 'Guest <guest@partner.example>')).toBeTruthy();
    expect(screen.getByText((_, el) => el?.textContent === 'IC <ic.staff@fpt.edu.vn>')).toBeTruthy();
  });

  it('hides the template picker but leaves subject and body editable', async () => {
    renderSetupComposer();
    await hydrated();

    expect(screen.queryByLabelText(/Chọn mẫu email/)).toBeNull();

    const subject = screen.getByDisplayValue('Cập nhật công tác chuẩn bị');
    fireEvent.change(subject, { target: { value: 'Tiêu đề Host tự sửa' } });
    expect(screen.getByDisplayValue('Tiêu đề Host tự sửa')).toBeTruthy();
  });

  it('marks the report attachment mandatory and gives it no delete control', async () => {
    renderSetupComposer();
    await hydrated();

    const locked = screen.getByTestId('locked-attachment');
    expect(locked.textContent).toContain('PEMS_Schedule_Report_VR-10.pdf');
    expect(locked.textContent).toContain('Bắt buộc');
    // Names the DELETE control rather than "any button": view and download are legitimately present
    // on every attachment now, so counting buttons would no longer say anything about removability.
    expect(screen.queryByTestId('locked-attachment-remove')).toBeNull();
    // Being mandatory must not cost the Host the ability to check what is being sent.
    expect((screen.getByTestId('locked-attachment-view') as HTMLButtonElement).disabled).toBe(false);

    // The Host's own file keeps its delete button — the lock is one file, not the whole list.
    const ordinary = screen.getByTestId('attachment');
    expect(ordinary.textContent).toContain('ghi-chu.pdf');
    expect(screen.getByTestId('attachment-remove')).toBeTruthy();
  });

  it('shows the backend warnings above the form', async () => {
    renderSetupComposer({ notices: ['Chưa có địa chỉ email nào của phía khách.'] });
    await hydrated();

    expect(screen.getByTestId('compose-notices').textContent)
      .toContain('Chưa có địa chỉ email nào của phía khách.');
  });

  it('sends through the caller endpoint, not the generic draft send', async () => {
    const sendOverride = vi.fn().mockResolvedValue({ success: true });
    renderSetupComposer({ sendDraftOverride: sendOverride });
    await hydrated();

    fireEvent.click(screen.getByRole('button', { name: /Xem trước/ }));
    await waitFor(() => expect(screen.getByRole('button', { name: /Xác nhận gửi/ })).toBeTruthy());
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận gửi/ }));
    fireEvent.click(await screen.findByText('__confirm__'));

    await waitFor(() => expect(sendOverride).toHaveBeenCalledWith(77));
    // The whole point of the dedicated endpoint is that the generic one is NOT reached.
    expect(sendDraft).not.toHaveBeenCalled();
  });

  it('shows the mandatory attachment in the preview too', async () => {
    renderSetupComposer();
    await hydrated();

    fireEvent.click(screen.getByRole('button', { name: /Xem trước/ }));

    const previewLocked = await screen.findByTestId('preview-locked-attachment');
    expect(previewLocked.textContent).toContain('Bắt buộc');
    expect(screen.getByTestId('preview-TO').textContent).toContain('guest@partner.example');
    expect(screen.getByTestId('preview-CC').textContent).toContain('ic.staff@fpt.edu.vn');
  });

  it('moves the lock onto the regenerated report so the replacement is the protected one', async () => {
    const refresh = vi.fn().mockResolvedValue({ fileId: 950, name: 'PEMS_Schedule_Report_VR-10_new.pdf' });
    // Body matches what was generated, so this sync is the no-warning path.
    renderSetupComposer({ onRefreshRequiredAttachment: refresh, initialBodyHtml: STORED_DRAFT.bodyContent });
    await hydrated();

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    await waitFor(() => expect(refresh).toHaveBeenCalled());
    await waitFor(() => {
      const locked = screen.getByTestId('locked-attachment');
      expect(locked.textContent).toContain('PEMS_Schedule_Report_VR-10_new.pdf');
      expect(screen.queryByTestId('locked-attachment-remove')).toBeNull();
    });

    // The Host's own attachment survives a report refresh; only the mandatory one is replaced.
    expect(screen.getByTestId('attachment').textContent).toContain('ghi-chu.pdf');
  });
});

/**
 * Syncing rebuilds the body as well as the PDF, because the two are renderings of one snapshot. That
 * makes the button destructive to anything the Host has typed, so these cover the one rule that must
 * hold: it never overwrites without asking, and asking must be honest about what is lost.
 */
describe('syncing from the latest setup data', () => {
  const freshReport = {
    fileId: 950,
    name: 'PEMS_Schedule_Report_VR-10_new.pdf',
    bodyHtml: '<p>bang du lieu moi</p>',
  };

  /** Opens on a draft whose body is exactly what the backend generated — nothing to lose yet. */
  const renderUnedited = (refresh: ReturnType<typeof vi.fn>) =>
    renderSetupComposer({
      onRefreshRequiredAttachment: refresh,
      initialBodyHtml: STORED_DRAFT.bodyContent,
    });

  it('rebuilds the body and the attachment together, without asking, when nothing was edited', async () => {
    const refresh = vi.fn().mockResolvedValue(freshReport);
    renderUnedited(refresh);
    await hydrated();

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    await waitFor(() => expect(refresh).toHaveBeenCalled());
    expect(screen.queryByTestId('confirm-dialog')).toBeNull();
    await waitFor(() =>
      expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toBe('<p>bang du lieu moi</p>'));
    await waitFor(() =>
      expect(screen.getByTestId('locked-attachment').textContent).toContain('PEMS_Schedule_Report_VR-10_new.pdf'));
  });

  it('warns before overwriting a body the host has edited, and does nothing until confirmed', async () => {
    const refresh = vi.fn().mockResolvedValue(freshReport);
    renderUnedited(refresh);
    await hydrated();

    fireEvent.change(screen.getByLabelText('body'), { target: { value: '<p>Host tu viet them</p>' } });
    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    expect(screen.getByTestId('confirm-dialog')).toBeTruthy();
    // The warning has to name the consequence; "bạn có chắc không" would not tell the Host that the
    // paragraphs they just wrote are what disappears.
    expect(screen.getByTestId('confirm-title').textContent).toContain('ghi đè');
    expect(screen.getByTestId('confirm-message').textContent).toContain('bạn tự sửa');
    // …and reassure that addressing is not touched, because that is the other thing a Host would fear.
    expect(screen.getByTestId('confirm-message').textContent).toContain('người nhận được giữ nguyên');

    // Nothing has happened yet: no request, and the edit is still in the editor.
    expect(refresh).not.toHaveBeenCalled();
    expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toBe('<p>Host tu viet them</p>');
  });

  it('replaces the edited body once the host confirms', async () => {
    const refresh = vi.fn().mockResolvedValue(freshReport);
    renderUnedited(refresh);
    await hydrated();

    fireEvent.change(screen.getByLabelText('body'), { target: { value: '<p>Host tu viet them</p>' } });
    fireEvent.click(screen.getByTestId('refresh-required-attachment'));
    fireEvent.click(screen.getByText('__confirm__'));

    await waitFor(() => expect(refresh).toHaveBeenCalled());
    await waitFor(() =>
      expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toBe('<p>bang du lieu moi</p>'));
  });

  it('keeps the edit and sends no request when the host cancels', async () => {
    const refresh = vi.fn().mockResolvedValue(freshReport);
    renderUnedited(refresh);
    await hydrated();

    fireEvent.change(screen.getByLabelText('body'), { target: { value: '<p>Host tu viet them</p>' } });
    fireEvent.click(screen.getByTestId('refresh-required-attachment'));
    fireEvent.click(screen.getByText('__cancel__'));

    await waitFor(() => expect(screen.queryByTestId('confirm-dialog')).toBeNull());
    expect(refresh).not.toHaveBeenCalled();
    expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toBe('<p>Host tu viet them</p>');
    // The old report is still the locked one, since nothing was regenerated.
    expect(screen.getByTestId('locked-attachment').textContent).toContain('PEMS_Schedule_Report_VR-10.pdf');
  });

  it('warns on a reopened draft, whose content cannot be proved unedited', async () => {
    const refresh = vi.fn().mockResolvedValue(freshReport);
    // Prepare returns an empty body when it re-opens an existing draft: nothing records whether that
    // draft was edited in an earlier session, so the composer must assume it was.
    renderSetupComposer({ onRefreshRequiredAttachment: refresh, initialBodyHtml: '' });
    await hydrated();

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    expect(screen.getByTestId('confirm-dialog')).toBeTruthy();
    expect(refresh).not.toHaveBeenCalled();
  });

  it('leaves the body alone when the backend returns none, so other callers keep the old behaviour', async () => {
    const refresh = vi.fn().mockResolvedValue({ fileId: 950, name: 'chi-doi-tep.pdf' });
    renderUnedited(refresh);
    await hydrated();

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    await waitFor(() => expect(refresh).toHaveBeenCalled());
    await waitFor(() =>
      expect(screen.getByTestId('locked-attachment').textContent).toContain('chi-doi-tep.pdf'));
    expect((screen.getByLabelText('body') as HTMLTextAreaElement).value).toBe(STORED_DRAFT.bodyContent);
  });
});

describe('the generic composer is unchanged', () => {
  it('keeps the template picker, deletable attachments and the generic send when no new prop is passed', async () => {
    render(<EmailComposeModal open onClose={vi.fn()} pushToast={vi.fn()} initialDraftId={77} />);
    await hydrated();

    expect(screen.getByText('Không dùng mẫu / Soạn thủ công')).toBeTruthy();
    expect(screen.queryByTestId('locked-attachment')).toBeNull();
    expect(screen.queryByTestId('refresh-required-attachment')).toBeNull();
    expect(screen.getAllByTestId('attachment')).toHaveLength(2);

    fireEvent.click(screen.getByRole('button', { name: /Xem trước/ }));
    await waitFor(() => expect(screen.getByRole('button', { name: /Xác nhận gửi/ })).toBeTruthy());
    fireEvent.click(screen.getByRole('button', { name: /Xác nhận gửi/ }));
    fireEvent.click(await screen.findByText('__confirm__'));

    await waitFor(() => expect(sendDraft).toHaveBeenCalledWith(77));
  });
});
