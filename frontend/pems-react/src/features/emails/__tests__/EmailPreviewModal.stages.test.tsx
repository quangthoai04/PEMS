/**
 * The three stages read three different things, and the send carries what the stage promised.
 *
 * <b>What this pins.</b> VIEW used to render the EDITABLE body — a bare template body with a hole where
 * the action area belongs — and paste the buttons into that hole in the browser. So the stage most sends
 * leave from showed a message assembled by rules no recipient's mail ever passes through. VIEW now shows
 * the backend's assembled copy, EDIT keeps the editable one, and FINAL_PREVIEW shows what the backend
 * signed. Those three are deliberately given DIFFERENT text here, so a stage reading the wrong field
 * fails loudly instead of looking plausible.
 *
 * The editor is the real one, with real Quill: the claim that the action node survives a trip through
 * EDIT is a claim about Quill's blot registry, and a mocked editor would assert nothing about it.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import { EmailPreviewModal, type EmailPreviewSendPayload } from '../../delegations/components/EmailPreviewModal';
import { SYSTEM_ACTION_NODE } from '../utils/systemActionNode';
import { delegationsApi } from '../../delegations/api/delegationsApi';

vi.mock('../../delegations/api/delegationsApi', () => ({
  delegationsApi: { buildFinalEmailPreview: vi.fn() },
}));

const EDITABLE = `<p>NOI-DUNG-SOAN</p>${SYSTEM_ACTION_NODE}<p>KY-TEN</p>`;
const ASSEMBLED = '<div>MAU-EMAIL-DAY-DU<p>NOI-DUNG-SOAN</p><span>NUT-BAM</span></div>';
const FINAL = '<div>KET-QUA-CUOI<span>NUT-BAM</span></div>';

/** The modal with the parent state it is controlled by, so an edit actually reaches it. */
function Harness({ onSend = vi.fn() }: { onSend?: (p: EmailPreviewSendPayload) => void }) {
  const [subject, setSubject] = useState('Chủ đề');
  const [body, setBody] = useState(EDITABLE);

  return (
    <EmailPreviewModal
      open
      loading={false}
      sending={false}
      error={null}
      subject={subject}
      body={body}
      initialFinalPreviewHtml={ASSEMBLED}
      isActionTemplate
      lockedActionBlockHtml='<div><span>NUT-BAM</span></div>'
      systemActionDescription="Nút do hệ thống gắn."
      replyToEmail="nguoi.gui@fpt.edu.vn"
      recipient={{ name: 'Nguyễn Văn Bình', email: 'binh@fpt.edu.vn' }}
      runtimeEditable
      previewToken="tok-prepare"
      canSend
      sendLabel="Gửi với nội dung này"
      onSubjectChange={setSubject}
      onBodyChange={setBody}
      onClose={vi.fn()}
      onRestore={vi.fn()}
      onSend={onSend}
    />
  );
}

const enterEdit = async () => {
  fireEvent.click(await screen.findByRole('button', { name: /Chỉnh sửa/ }));
  await screen.findByRole('toolbar', { name: /Định dạng nội dung email/ });
};

describe('EmailPreviewModal — what each stage shows', () => {
  beforeEach(() => {
    vi.mocked(delegationsApi.buildFinalEmailPreview).mockReset();
    vi.mocked(delegationsApi.buildFinalEmailPreview).mockResolvedValue({
      subject: 'Chủ đề',
      finalPreviewHtml: FINAL,
      finalPreviewToken: 'tok-final',
      expiresAt: '2026-08-05T12:00:00+07:00',
    } as any);
  });

  it('VIEW shows the assembled message', async () => {
    render(<Harness />);

    const view = await screen.findByTestId('view-body');
    expect(view.innerHTML).toContain('MAU-EMAIL-DAY-DU');
    expect(view.innerHTML).toContain('NUT-BAM');
  });

  it('EDIT opens on the editable body, not on the assembled message', async () => {
    const { container } = render(<Harness />);
    await enterEdit();

    const editor = container.querySelector('.ql-editor');
    expect(editor).toBeTruthy();
    expect(editor!.textContent).toContain('NOI-DUNG-SOAN');
    // The shell belongs to the message, never to the thing being typed into: an author who could edit
    // the branded wrapper could delete it, and the send would put it back.
    expect(editor!.textContent).not.toContain('MAU-EMAIL-DAY-DU');
  });

  it('FINAL_PREVIEW shows what the backend signed', async () => {
    render(<Harness />);
    await enterEdit();

    fireEvent.click(screen.getByRole('button', { name: /Xem trước kết quả/ }));

    const final = await screen.findByTestId('final-preview-body');
    expect(final.innerHTML).toContain('KET-QUA-CUOI');
    // Not the first preview's copy: this stage exists to show the effect of the edit.
    expect(final.innerHTML).not.toContain('MAU-EMAIL-DAY-DU');
  });

  /**
   * The action node survives the round trip through real Quill.
   *
   * If the blot were unregistered — or registered against the wrong copy of Quill, which compiles and
   * throws nothing — the editor would drop the node on load and the payload below would carry no
   * position at all. The send would then append the buttons at the end of whatever the author wrote,
   * silently, which is the defect the movable node was built to end.
   */
  it('keeps the action node through EDIT and hands it back at its position', async () => {
    render(<Harness />);
    await enterEdit();

    fireEvent.click(screen.getByRole('button', { name: /Xem trước kết quả/ }));

    await waitFor(() => expect(delegationsApi.buildFinalEmailPreview).toHaveBeenCalled());

    const payload = vi.mocked(delegationsApi.buildFinalEmailPreview).mock.calls[0][0];
    const sent = payload.editableBodyHtml ?? '';

    expect(sent).toContain('data-system-block');
    expect(sent.indexOf('NOI-DUNG-SOAN')).toBeLessThan(sent.indexOf('data-system-block'));
    expect(sent.indexOf('data-system-block')).toBeLessThan(sent.indexOf('KY-TEN'));
    expect(payload.previewToken).toBe('tok-prepare');
  });
});

describe('EmailPreviewModal — sending', () => {
  beforeEach(() => {
    vi.mocked(delegationsApi.buildFinalEmailPreview).mockReset();
    vi.mocked(delegationsApi.buildFinalEmailPreview).mockResolvedValue({
      subject: 'Chủ đề',
      finalPreviewHtml: FINAL,
      finalPreviewToken: 'tok-final',
      expiresAt: '2026-08-05T12:00:00+07:00',
    } as any);
  });

  /**
   * A sender who changes nothing sends from VIEW, without being marched through the other two stages.
   *
   * The payload is empty by design: there is nothing of the sender's to approve, so there is nothing to
   * bind, and the backend renders the template — the same template VIEW's assembled copy was built from.
   */
  it('sends straight from VIEW, carrying no approved content', async () => {
    const onSend = vi.fn();
    render(<Harness onSend={onSend} />);

    fireEvent.click(await screen.findByRole('button', { name: /Gửi với nội dung này/ }));

    expect(onSend).toHaveBeenCalledWith({});
  });

  it('sends the approved content and its token once the sender has edited', async () => {
    const onSend = vi.fn();
    render(<Harness onSend={onSend} />);
    await enterEdit();

    fireEvent.click(screen.getByRole('button', { name: /Xem trước kết quả/ }));
    await screen.findByTestId('final-preview-body');

    fireEvent.click(screen.getByRole('button', { name: /Gửi với nội dung này/ }));

    expect(onSend).toHaveBeenCalledTimes(1);
    const payload = onSend.mock.calls[0][0] as EmailPreviewSendPayload;
    expect(payload.approvedContent?.finalPreviewToken).toBe('tok-final');
    expect(payload.approvedContent?.bodyHtml).toContain('NOI-DUNG-SOAN');
  });

  /**
   * A second edit cannot be sent on the first approval: it has to be approved again, and what goes is
   * the NEW token over the NEW words.
   *
   * The backend would refuse the stale one anyway — its content hash would not match — but catching it
   * here keeps the sender's words on screen instead of trading them for an error they cannot act on.
   * EDIT offers no send button at all, so the only route from a changed message to a send runs back
   * through the final preview; this walks that route and checks which approval came out of it.
   */
  it('will not send a second edit on the first approval', async () => {
    const onSend = vi.fn();
    const { container } = render(<Harness onSend={onSend} />);
    await enterEdit();

    fireEvent.click(screen.getByRole('button', { name: /Xem trước kết quả/ }));
    await screen.findByTestId('final-preview-body');

    fireEvent.click(screen.getByRole('button', { name: /Quay lại chỉnh sửa/ }));
    await screen.findByRole('toolbar', { name: /Định dạng nội dung email/ });

    // A real change through the real editor, not a prop poke.
    const editor = container.querySelector('.ql-editor') as HTMLElement;
    await act(async () => {
      editor.innerHTML = `<p>DOI-Y-ROI</p>${SYSTEM_ACTION_NODE}`;
      editor.dispatchEvent(new Event('input', { bubbles: true }));
    });

    // The changed message has to be approved again — and this time the backend signs a different token.
    vi.mocked(delegationsApi.buildFinalEmailPreview).mockResolvedValue({
      subject: 'Chủ đề',
      finalPreviewHtml: '<div>KET-QUA-CUOI-LAN-HAI</div>',
      finalPreviewToken: 'tok-final-2',
      expiresAt: '2026-08-05T12:00:00+07:00',
    } as any);

    fireEvent.click(screen.getByRole('button', { name: /Xem trước kết quả/ }));
    await screen.findByText(/KET-QUA-CUOI-LAN-HAI/);

    fireEvent.click(screen.getByRole('button', { name: /Gửi với nội dung này/ }));

    const payload = onSend.mock.calls[0][0] as EmailPreviewSendPayload;
    expect(payload.approvedContent?.finalPreviewToken).toBe('tok-final-2');
    expect(payload.approvedContent?.bodyHtml).toContain('DOI-Y-ROI');
    expect(payload.approvedContent?.bodyHtml).not.toContain('KY-TEN');
  });

  /**
   * Abandoning an edit returns to the template, not to a half-approved version of it.
   *
   * "Hủy thay đổi" lands on VIEW, and a send from VIEW carries no approved content at all — so the
   * abandoned words cannot reach a recipient by any route.
   */
  it('sends the template again after an edit is abandoned', async () => {
    const onSend = vi.fn();
    const { container } = render(<Harness onSend={onSend} />);
    await enterEdit();

    const editor = container.querySelector('.ql-editor') as HTMLElement;
    await act(async () => {
      editor.innerHTML = `<p>BO-DI</p>${SYSTEM_ACTION_NODE}`;
      editor.dispatchEvent(new Event('input', { bubbles: true }));
    });

    fireEvent.click(screen.getByRole('button', { name: /Hủy thay đổi/ }));
    fireEvent.click(await screen.findByRole('button', { name: /Gửi với nội dung này/ }));

    expect(onSend).toHaveBeenCalledWith({});
  });
});

describe('EmailPreviewModal — what stays put across the stages', () => {
  beforeEach(() => {
    vi.mocked(delegationsApi.buildFinalEmailPreview).mockReset();
    vi.mocked(delegationsApi.buildFinalEmailPreview).mockResolvedValue({
      subject: 'Chủ đề',
      finalPreviewHtml: FINAL,
      finalPreviewToken: 'tok-final',
      expiresAt: '2026-08-05T12:00:00+07:00',
    } as any);
  });

  /**
   * Recipient and Reply-To are facts about the send, not about the text, so they are shown in every
   * stage and editable in none. A sender who has to leave a stage to check who a message goes to will
   * eventually not check.
   */
  it('keeps the recipient and the reply address visible in all three stages', async () => {
    render(<Harness />);

    const present = () => {
      expect(screen.getByText('Nguyễn Văn Bình')).toBeTruthy();
      expect(screen.getByText('nguoi.gui@fpt.edu.vn')).toBeTruthy();
    };

    present();

    await enterEdit();
    present();

    fireEvent.click(screen.getByRole('button', { name: /Xem trước kết quả/ }));
    await screen.findByTestId('final-preview-body');
    present();
  });
});
