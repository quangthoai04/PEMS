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

vi.mock('../../../components/modals/ConfirmModal', () => ({
  ConfirmModal: ({ isOpen, onConfirm }: { isOpen: boolean; onConfirm: () => void }) =>
    isOpen ? <button type="button" onClick={onConfirm}>__confirm__</button> : null,
}));
vi.mock('../../../shared/auth/authStorage', () => ({ authStorage: { getToken: () => 'test-token' } }));

vi.mock('react-quill-new', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
    <textarea aria-label="body" value={value} onChange={e => onChange(e.target.value)} />
  ),
}));
vi.mock('react-quill-new/dist/quill.snow.css', () => ({}));

import { EmailComposeModal } from '../components/EmailComposeModal';

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
    expect(locked.querySelector('button')).toBeNull();

    // The Host's own file keeps its delete button — the lock is one file, not the whole list.
    const ordinary = screen.getByTestId('attachment');
    expect(ordinary.textContent).toContain('ghi-chu.pdf');
    expect(ordinary.querySelector('button')).not.toBeNull();
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
    renderSetupComposer({ onRefreshRequiredAttachment: refresh });
    await hydrated();

    fireEvent.click(screen.getByTestId('refresh-required-attachment'));

    await waitFor(() => expect(refresh).toHaveBeenCalled());
    await waitFor(() => {
      const locked = screen.getByTestId('locked-attachment');
      expect(locked.textContent).toContain('PEMS_Schedule_Report_VR-10_new.pdf');
      expect(locked.querySelector('button')).toBeNull();
    });

    // The Host's own attachment survives a report refresh; only the mandatory one is replaced.
    expect(screen.getByTestId('attachment').textContent).toContain('ghi-chu.pdf');
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
