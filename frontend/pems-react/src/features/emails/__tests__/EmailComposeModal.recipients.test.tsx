/**
 * G6.3 — the compose modal on its real path: EmailComposeModal → emailDraftsApi.
 *
 * These assert the payload that actually leaves the component, because the defect this replaces was
 * invisible in the UI: the screen could show a CC while the request stamped every recipient 'TO'.
 * Rendering alone would not have caught it; only the payload does.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor, within } from '@testing-library/react';

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

// Auto-confirm: these tests are about the payload, not about the confirmation dialog.
vi.mock('../../../components/modals/ConfirmModal', () => ({
  ConfirmModal: ({ isOpen, onConfirm }: { isOpen: boolean; onConfirm: () => void }) =>
    isOpen ? <button type="button" onClick={onConfirm}>__confirm__</button> : null,
}));
vi.mock('../../../shared/auth/authStorage', () => ({ authStorage: { getToken: () => 'test-token' } }));

// The rich-text editor is not what these tests are about; a plain textarea keeps them fast and stable.
vi.mock('react-quill-new', () => ({
  default: ({ value, onChange }: { value: string; onChange: (v: string) => void }) => (
    <textarea aria-label="body" value={value} onChange={e => onChange(e.target.value)} />
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
  createDraft.mockResolvedValue({ emailDraftId: 7, recipients: [], attachments: [] });
  updateDraft.mockResolvedValue({ emailDraftId: 7, recipients: [], attachments: [] });
  sendDraft.mockResolvedValue({ emailDraftId: 7, sentEmailId: 9, status: 'SENT', success: true, draftStatus: 'SENT', message: 'ok' });
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
  /**
   * Drives the real send: preview → confirm → handleSend. Asserting here rather than on the debounced
   * autosave keeps the test deterministic (autosave fires after 1200ms, which a default waitFor would
   * race) and exercises the create/update/send path the requirement is actually about.
   */
  const send = async () => {
    fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'Chủ đề' } });
    fireEvent.click(screen.getByRole('button', { name: /Xem trước/ }));
    fireEvent.click(await screen.findByRole('button', { name: 'Xác nhận gửi' }));
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

    await waitFor(() => expect(createDraft).toHaveBeenCalled());

    const payload = createDraft.mock.calls.at(-1)![0] as any;
    const byType = Object.fromEntries(payload.recipients.map((r: any) => [r.email, r.recipientType]));
    expect(byType).toEqual({
      'to@fpt.vn': 'TO',
      'cc@fpt.vn': 'CC',
      'bcc@fpt.vn': 'BCC',
    });
    await waitFor(() => expect(sendDraft).toHaveBeenCalledWith(7));
  });

  it('gives recipients a stable display order across the three groups', async () => {
    renderModal();
    await flushLimit();

    addRecipient('Đến', 'a@fpt.vn');
    openGroup('Thêm BCC');
    addRecipient('BCC', 'b@fpt.vn');
    await send();

    await waitFor(() => expect(createDraft).toHaveBeenCalled());
    const payload = createDraft.mock.calls.at(-1)![0] as any;
    expect(payload.recipients.map((r: any) => r.displayOrder)).toEqual([0, 1]);
  });

  it('keeps the composed content when the server rejects the envelope', async () => {
    createDraft.mockRejectedValue({
      response: { data: { errorCode: 'EMAIL_RECIPIENT_INVALID', message: "Địa chỉ email không hợp lệ ở mục CC: 'x'." } },
    });

    renderModal();
    await flushLimit();
    addRecipient('Đến', 'to@fpt.vn');
    openGroup('Thêm CC');
    addRecipient('CC', 'cc@fpt.vn');
    await send();

    // The rejection is shown on the CC field, and nothing the sender typed is lost.
    expect(await screen.findByText(/không hợp lệ ở mục CC/)).toBeInTheDocument();
    expect(screen.getByTestId('chip-TO')).toHaveTextContent('to@fpt.vn');
    expect(screen.getByTestId('chip-CC')).toHaveTextContent('cc@fpt.vn');
    expect(screen.getByDisplayValue('Chủ đề')).toBeInTheDocument();
  });

  it('shows an unattributable server error at form level rather than guessing a field', async () => {
    createDraft.mockRejectedValue({ response: { data: { message: 'Lỗi hệ thống.' } } });

    renderModal();
    await flushLimit();
    addRecipient('Đến', 'to@fpt.vn');
    await send();

    expect(await screen.findByText('Lỗi hệ thống.')).toBeInTheDocument();
  });

  it('does not start a second send while one is in flight', async () => {
    let release: (v: unknown) => void = () => {};
    createDraft.mockReturnValue(new Promise(resolve => { release = resolve; }));

    renderModal();
    await flushLimit();
    addRecipient('Đến', 'to@fpt.vn');
    await send();

    await waitFor(() => expect(createDraft).toHaveBeenCalledTimes(1));

    // A second confirm while the first request is still open must not issue another one.
    const confirmAgain = screen.queryByRole('button', { name: '__confirm__' });
    if (confirmAgain) fireEvent.click(confirmAgain);
    expect(createDraft).toHaveBeenCalledTimes(1);

    release({ emailDraftId: 7, recipients: [], attachments: [] });
  });
});

describe('draft restore', () => {
  it('puts each stored recipient back in the group its recipient_type names, with the display name', async () => {
    getDraft.mockResolvedValue({
      emailDraftId: 42,
      subject: 'Đã lưu',
      bodyContent: '<p>xin chào</p>',
      bodyFormat: 'HTML',
      status: 'DRAFT',
      recipients: [
        { emailDraftRecipientId: 1, recipientEmail: 'to@fpt.vn', recipientName: 'Người Nhận', recipientType: 'TO', displayOrder: 0 },
        { emailDraftRecipientId: 2, recipientEmail: 'cc@fpt.vn', recipientName: null, recipientType: 'CC', displayOrder: 1 },
        { emailDraftRecipientId: 3, recipientEmail: 'bcc@fpt.vn', recipientName: null, recipientType: 'BCC', displayOrder: 2 },
      ],
      attachments: [],
    });

    renderModal({ initialDraftId: 42 });
    await waitFor(() => expect(getDraft).toHaveBeenCalledWith(42));

    // Groups holding data are revealed, not left hidden behind a toggle.
    await waitFor(() => expect(screen.getByTestId('chip-CC')).toBeInTheDocument());
    expect(screen.getByTestId('chip-TO')).toHaveTextContent('Người Nhận <to@fpt.vn>');
    expect(screen.getByTestId('chip-BCC')).toHaveTextContent('bcc@fpt.vn');
    expect(screen.getByDisplayValue('Đã lưu')).toBeInTheDocument();
  });

  describe('a recipient type that is none of TO/CC/BCC', () => {
    const corruptDraft = {
      emailDraftId: 43, subject: 'Nháp hỏng', bodyContent: '<p>x</p>', bodyFormat: 'HTML', status: 'DRAFT',
      recipients: [
        { emailDraftRecipientId: 1, recipientEmail: 'ok@fpt.vn', recipientType: 'TO', displayOrder: 0 },
        { emailDraftRecipientId: 2, recipientEmail: 'weird@fpt.vn', recipientType: 'WAT', displayOrder: 1 },
      ],
      attachments: [],
    };

    it('does not place it in TO, CC or BCC', async () => {
      getDraft.mockResolvedValue(corruptDraft);
      renderModal({ initialDraftId: 43 });
      await screen.findByTestId('draft-blocked');

      // The classifiable row is still shown; the unclassifiable one is in no group at all.
      expect(screen.getByTestId('chip-TO')).toHaveTextContent('ok@fpt.vn');
      expect(screen.queryByText(/weird@fpt\.vn/)).not.toBeInTheDocument();
      expect(screen.queryByTestId('chip-CC')).not.toBeInTheDocument();
      expect(screen.queryByTestId('chip-BCC')).not.toBeInTheDocument();
    });

    it('reports it as a draft-level fault naming the offending type', async () => {
      getDraft.mockResolvedValue(corruptDraft);
      renderModal({ initialDraftId: 43 });

      const blocked = await screen.findByTestId('draft-blocked');
      expect(blocked).toHaveTextContent('không hợp lệ');
      expect(blocked).toHaveTextContent('WAT');
    });

    it('refuses to preview or send it', async () => {
      getDraft.mockResolvedValue(corruptDraft);
      renderModal({ initialDraftId: 43 });
      await screen.findByTestId('draft-blocked');

      const preview = screen.getByRole('button', { name: /Xem trước/ });
      expect(preview).toBeDisabled();

      fireEvent.click(preview);
      expect(screen.queryByRole('button', { name: 'Xác nhận gửi' })).not.toBeInTheDocument();
      expect(sendDraft).not.toHaveBeenCalled();
    });

    it('never writes the draft back, so the rows it could not classify are not deleted', async () => {
      vi.useFakeTimers();
      try {
        getDraft.mockResolvedValue(corruptDraft);
        renderModal({ initialDraftId: 43 });
        await vi.waitFor(() => expect(getDraft).toHaveBeenCalled());

        // Edit something, then run past the autosave debounce.
        fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'sửa' } });
        await vi.advanceTimersByTimeAsync(5000);

        expect(createDraft).not.toHaveBeenCalled();
        expect(updateDraft).not.toHaveBeenCalled();
      } finally {
        vi.useRealTimers();
      }
    });
  });
});

describe('hydration guard', () => {
  /**
   * The failure this prevents: the composer mounts empty, the autosave debounce fires before
   * `getDraft` resolves, and the empty form is PUT over the draft being restored. "Reopen my draft"
   * would erase it.
   */
  it('does not autosave while the draft is still loading', async () => {
    vi.useFakeTimers();
    try {
      let resolveDraft: (v: unknown) => void = () => {};
      getDraft.mockReturnValue(new Promise(resolve => { resolveDraft = resolve; }));

      renderModal({ initialDraftId: 55 });
      await vi.waitFor(() => expect(getDraft).toHaveBeenCalledWith(55));

      // Type into the still-empty form and run well past the 1200ms debounce.
      fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'x' } });
      await vi.advanceTimersByTimeAsync(5000);

      expect(createDraft).not.toHaveBeenCalled();
      expect(updateDraft).not.toHaveBeenCalled();

      resolveDraft({
        emailDraftId: 55, subject: 'Nội dung thật', bodyContent: '<p>b</p>', bodyFormat: 'HTML',
        status: 'DRAFT', recipients: [], attachments: [],
      });
    } finally {
      vi.useRealTimers();
    }
  });

  it('enables autosave only after hydration succeeds', async () => {
    vi.useFakeTimers();
    try {
      getDraft.mockResolvedValue({
        emailDraftId: 55, subject: 'Nội dung thật', bodyContent: '<p>b</p>', bodyFormat: 'HTML',
        status: 'DRAFT',
        recipients: [{ emailDraftRecipientId: 1, recipientEmail: 'to@fpt.vn', recipientType: 'TO', displayOrder: 0 }],
        attachments: [],
      });

      renderModal({ initialDraftId: 55 });
      await vi.waitFor(() => expect(screen.getByTestId('chip-TO')).toBeInTheDocument());

      fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'đã sửa' } });
      await vi.advanceTimersByTimeAsync(1500);

      await vi.waitFor(() => expect(updateDraft).toHaveBeenCalled());
      const [draftId, payload] = updateDraft.mock.calls.at(-1)! as [number, any];
      expect(draftId).toBe(55);
      expect(payload.subject).toBe('đã sửa');
      expect(createDraft).not.toHaveBeenCalled();   // never forks a second draft
    } finally {
      vi.useRealTimers();
    }
  });

  it('creates nothing and updates nothing when the draft cannot be loaded', async () => {
    vi.useFakeTimers();
    try {
      getDraft.mockRejectedValue(new Error('boom'));

      renderModal({ initialDraftId: 55 });
      await vi.waitFor(() => expect(getDraft).toHaveBeenCalled());

      fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'gõ tiếp' } });
      await vi.advanceTimersByTimeAsync(5000);

      expect(createDraft).not.toHaveBeenCalled();
      expect(updateDraft).not.toHaveBeenCalled();
    } finally {
      vi.useRealTimers();
    }
  });

  it('says the draft could not be loaded instead of presenting a working empty composer', async () => {
    getDraft.mockRejectedValue(new Error('boom'));

    renderModal({ initialDraftId: 55 });

    expect(await screen.findByText(/Không tải được email nháp/)).toBeInTheDocument();
  });
});

/**
 * Autosave coverage, driven by fake timers rather than by waiting out the real 1200ms debounce.
 * Kept separate from the send-path tests so both the create and update payloads are asserted.
 */
describe('autosave payload', () => {
  it('creates the draft with each recipient type and a continuous display order', async () => {
    vi.useFakeTimers();
    try {
      renderModal();
      await vi.waitFor(() => expect(getRecipientLimits).toHaveBeenCalled());

      addRecipient('Đến', 'to@fpt.vn');
      openGroup('Thêm CC');
      addRecipient('CC', 'cc@fpt.vn');
      openGroup('Thêm BCC');
      addRecipient('BCC', 'bcc@fpt.vn');

      await vi.advanceTimersByTimeAsync(1500);
      await vi.waitFor(() => expect(createDraft).toHaveBeenCalled());

      const payload = createDraft.mock.calls.at(-1)![0] as any;
      expect(payload.recipients).toEqual([
        { email: 'to@fpt.vn', name: null, recipientType: 'TO', displayOrder: 0 },
        { email: 'cc@fpt.vn', name: null, recipientType: 'CC', displayOrder: 1 },
        { email: 'bcc@fpt.vn', name: null, recipientType: 'BCC', displayOrder: 2 },
      ]);
    } finally {
      vi.useRealTimers();
    }
  });

  it('updates an existing draft without collapsing the groups', async () => {
    vi.useFakeTimers();
    try {
      renderModal();
      await vi.waitFor(() => expect(getRecipientLimits).toHaveBeenCalled());

      addRecipient('Đến', 'to@fpt.vn');
      await vi.advanceTimersByTimeAsync(1500);
      await vi.waitFor(() => expect(createDraft).toHaveBeenCalled());

      // Second edit goes to update, not create, and keeps the new group.
      openGroup('Thêm BCC');
      addRecipient('BCC', 'bcc@fpt.vn');
      await vi.advanceTimersByTimeAsync(1500);
      await vi.waitFor(() => expect(updateDraft).toHaveBeenCalled());

      const [draftId, payload] = updateDraft.mock.calls.at(-1)! as [number, any];
      expect(draftId).toBe(7);
      expect(payload.recipients.map((r: any) => r.recipientType)).toEqual(['TO', 'BCC']);
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('recipient limit', () => {
  it('shows the ceiling the server reported, not a hard-coded one', async () => {
    getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 3 } });
    renderModal();
    await waitFor(() => expect(screen.getByTestId('recipient-counter')).toHaveTextContent('0/3'));
  });

  it('says the limit is unknown when the request fails, and still keeps the draft usable', async () => {
    getRecipientLimits.mockRejectedValue(new Error('network'));
    renderModal();

    await waitFor(() =>
      expect(screen.getByTestId('recipient-counter')).toHaveTextContent('chưa tải được giới hạn'));

    addRecipient('Đến', 'a@fpt.vn');
    expect(screen.getByTestId('chip-TO')).toBeInTheDocument();   // draft not lost
  });

  it('treats a non-positive configured limit as unusable instead of rendering 0', async () => {
    getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 0 } });
    renderModal();
    await waitFor(() =>
      expect(screen.getByTestId('recipient-counter')).toHaveTextContent('chưa tải được giới hạn'));
    expect(screen.getByTestId('recipient-counter')).not.toHaveTextContent('/0');
  });

  it('blocks the send when the total exceeds the served ceiling', async () => {
    getRecipientLimits.mockResolvedValue({ data: { maxRecipients: 1 } });
    renderModal();
    await flushLimit();

    addRecipient('Đến', 'a@fpt.vn');
    openGroup('Thêm CC');
    addRecipient('CC', 'b@fpt.vn');
    fireEvent.change(screen.getByPlaceholderText('Tiêu đề email…'), { target: { value: 'x' } });

    createDraft.mockClear();
    sendDraft.mockClear();
    fireEvent.click(screen.getByRole('button', { name: /Xem trước/ }));

    expect(await screen.findByText(/vượt quá giới hạn/)).toBeInTheDocument();
    expect(sendDraft).not.toHaveBeenCalled();
  });
});
