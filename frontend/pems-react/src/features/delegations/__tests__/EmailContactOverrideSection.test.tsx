/**
 * The reply-contact panel of the compose modal.
 *
 * The defect it replaces: the preview endpoint rendered a dashed "hệ thống điền đầu mối…" stand-in INTO
 * the body it returned, that body went into the rich-text editor, and the host sent it back as authored
 * content — after which the backend appended the real contact card underneath. So the message carried a
 * placeholder AND a card, and the host had approved neither arrangement.
 *
 * The rules asserted below all follow from the repair: the block is rendered outside the editor, it is
 * never part of what the modal sends, and every decision about it (which modes exist, whether it may be
 * hidden, whether a chosen colleague is allowed) is answered by the server on each change.
 */
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

const previewEmailContact = vi.fn();
const searchEmailContactCandidates = vi.fn();

vi.mock('../api/delegationsApi', () => ({
  delegationsApi: {
    previewEmailContact: (...a: unknown[]) => previewEmailContact(...a),
    searchEmailContactCandidates: (...a: unknown[]) => searchEmailContactCandidates(...a),
  },
}));

import { EmailContactOverrideSection } from '../components/EmailContactOverrideSection';
import type { EmailContactContext, EmailContactPreviewResult } from '../types/delegations.types';

const CONTEXT: EmailContactContext = {
  templateCode: 'LOGISTICS_REQUEST_TO_DEPARTMENT',
  visitInstanceId: 42,
};

/** OPTIONAL: the block may be changed AND switched off for one message. */
const OPTIONAL: EmailContactPreviewResult = {
  supported: true,
  requirement: 'OPTIONAL',
  mode: 'TEMPLATE_DEFAULT',
  source: 'CAMPUS_DEFAULT',
  lockedContactBlockHtml: '<table><tbody><tr><td>Đầu mối cơ sở</td><td>co.so@pems.test</td></tr></tbody></table>',
  contactDisplayName: 'Cơ sở Hà Nội',
  contactEmail: 'co.so@pems.test',
  contactPhone: '0900000001',
  replyToDisplay: 'co.so@pems.test',
  hidden: false,
  canOverride: true,
  canHide: true,
  availableModes: ['TEMPLATE_DEFAULT', 'SYSTEM_USER', 'MANUAL'],
  availableReplyToModes: ['POLICY_DEFAULT', 'CONTACT', 'SENDER', 'NONE'],
};

/** REQUIRED: the wording tells the reader to make contact, so the block cannot be hidden. */
const REQUIRED: EmailContactPreviewResult = {
  ...OPTIONAL, requirement: 'REQUIRED', canHide: false, source: 'HOST',
  contactDisplayName: 'Trần Thị Hà',
};

/** A template that can never carry the block — a credential-bearing mail. */
const UNSUPPORTED: EmailContactPreviewResult = {
  supported: false,
  requirement: 'NONE',
  mode: 'TEMPLATE_DEFAULT',
  source: null,
  lockedContactBlockHtml: null,
  hidden: false,
  canOverride: false,
  canHide: false,
  availableModes: [],
  availableReplyToModes: [],
};

function renderPanel(
  initial: EmailContactPreviewResult | null,
  onChange = vi.fn(),
  context: EmailContactContext | null = CONTEXT,
) {
  render(
    <EmailContactOverrideSection context={context} initial={initial} onChange={onChange} />,
  );
  return onChange;
}

beforeEach(() => {
  previewEmailContact.mockReset();
  searchEmailContactCandidates.mockReset();
  searchEmailContactCandidates.mockResolvedValue([]);
});

describe('the panel decides nothing the server has not said', () => {
  it('shows the resolved contact and the block read-only', () => {
    renderPanel(OPTIONAL);

    expect(screen.getByTestId('contact-panel')).toBeTruthy();
    expect(screen.getByText(/Cơ sở Hà Nội/)).toBeTruthy();
    // The block is rendered as markup, outside any editor.
    expect(screen.getByTestId('contact-block').innerHTML).toContain('co.so@pems.test');
  });

  it('hides itself entirely for a template that cannot carry the block', () => {
    renderPanel(UNSUPPORTED);

    expect(screen.queryByTestId('contact-panel')).toBeNull();
    expect(screen.queryByTestId('contact-change')).toBeNull();
  });

  /**
   * A preview with no real message behind it — the "xem mẫu" links on each panel header. There is no
   * visit to resolve a Host from and nothing to send, so offering a contact editor would invite a
   * decision that has nowhere to go.
   */
  it('hides itself when there is no message context', () => {
    renderPanel(OPTIONAL, vi.fn(), null);

    expect(screen.queryByTestId('contact-panel')).toBeNull();
  });

  it('offers "không hiển thị" only where the server said it may', () => {
    const { unmount } = render(
      <EmailContactOverrideSection context={CONTEXT} initial={OPTIONAL} onChange={vi.fn()} />,
    );
    fireEvent.click(screen.getByTestId('contact-change'));
    expect(screen.queryByTestId('contact-hide')).toBeTruthy();
    unmount();

    render(<EmailContactOverrideSection context={CONTEXT} initial={REQUIRED} onChange={vi.fn()} />);
    fireEvent.click(screen.getByTestId('contact-change'));
    expect(screen.queryByTestId('contact-hide')).toBeNull();
  });
});

describe('what the modal sends', () => {
  /**
   * The whole point of the repair. Whatever the panel displays, the payload is structured data — the
   * block's HTML is never part of it, so a message can never carry two of them.
   */
  it('reports no override until the sender changes something', () => {
    const onChange = renderPanel(OPTIONAL);

    expect(onChange).toHaveBeenCalledWith({ contactOverride: null, blocked: false });
  });

  it('reports the chosen colleague as an id and nothing else', async () => {
    searchEmailContactCandidates.mockResolvedValue([
      { userId: 7, fullName: 'Nguyễn Văn B', email: 'b@pems.test', hasEmail: true },
    ]);
    previewEmailContact.mockResolvedValue({
      ...OPTIONAL, mode: 'SYSTEM_USER', contactDisplayName: 'Nguyễn Văn B',
      lockedContactBlockHtml: '<table><tbody><tr><td>b@pems.test</td></tr></tbody></table>',
    });

    const onChange = renderPanel(OPTIONAL);

    fireEvent.click(screen.getByTestId('contact-change'));
    fireEvent.click(screen.getByLabelText?.('Chọn người trong hệ thống') ?? screen.getByDisplayValue('SYSTEM_USER'));
    fireEvent.change(screen.getByTestId('contact-search'), { target: { value: 'Nguyễn' } });

    await waitFor(() => expect(screen.getByText('Nguyễn Văn B')).toBeTruthy());
    fireEvent.click(screen.getByText('Nguyễn Văn B'));
    fireEvent.click(screen.getByTestId('contact-apply'));

    await waitFor(() => expect(previewEmailContact).toHaveBeenCalled());

    const sent = previewEmailContact.mock.calls[0][1];
    expect(sent.mode).toBe('SYSTEM_USER');
    expect(sent.userId).toBe(7);
    // Never the identity fields — those come from the chosen person's own record.
    expect(sent.displayName).toBeUndefined();
    expect(sent.email).toBeUndefined();

    await waitFor(() =>
      expect(onChange).toHaveBeenLastCalledWith({
        contactOverride: expect.objectContaining({ mode: 'SYSTEM_USER', userId: 7 }),
        blocked: false,
      }));
  });

  it('never puts the block html into the payload', async () => {
    previewEmailContact.mockResolvedValue({ ...OPTIONAL, hidden: true, lockedContactBlockHtml: null });
    const onChange = renderPanel(OPTIONAL);

    fireEvent.click(screen.getByTestId('contact-change'));
    fireEvent.click(screen.getByTestId('contact-hide'));
    fireEvent.click(screen.getByTestId('contact-apply'));

    await waitFor(() => expect(previewEmailContact).toHaveBeenCalled());

    const payload = JSON.stringify(onChange.mock.calls.at(-1)?.[0]);
    expect(payload).not.toContain('<table');
    expect(payload).not.toContain('lockedContactBlockHtml');
  });
});

describe('errors keep the sender where they were', () => {
  /** A REQUIRED template with nobody to name. The send must be stopped, not attempted and refused. */
  it('blocks the send while the panel is in error', () => {
    const onChange = renderPanel({
      ...REQUIRED,
      lockedContactBlockHtml: null,
      contactDisplayName: null,
      errorCode: 'EMAIL_CONTACT_REQUIRED_BUT_NOT_FOUND',
      errorMessage: 'Không tìm được đầu mối khả dụng.',
    });

    expect(screen.getByTestId('contact-error').textContent).toContain('Không tìm được đầu mối');
    expect(onChange).toHaveBeenCalledWith({ contactOverride: null, blocked: true });
  });

  it('keeps the manual form filled in when the server refuses it', async () => {
    previewEmailContact.mockResolvedValue({
      ...OPTIONAL,
      errorCode: 'EMAIL_CONTACT_OVERRIDE_INVALID',
      errorMessage: 'Email của đầu mối liên hệ không hợp lệ.',
    });

    renderPanel(OPTIONAL);

    fireEvent.click(screen.getByTestId('contact-change'));
    fireEvent.click(screen.getByDisplayValue('MANUAL'));
    fireEvent.change(screen.getByTestId('contact-manual-name'), { target: { value: 'Lê Thị Bếp' } });
    fireEvent.change(screen.getByTestId('contact-manual-role'), { target: { value: 'Điều phối' } });
    fireEvent.change(screen.getByTestId('contact-manual-email'), { target: { value: 'sai-dinh-dang' } });
    fireEvent.change(screen.getByTestId('contact-manual-reason'), { target: { value: 'Nhà thầu ngoài' } });
    fireEvent.click(screen.getByTestId('contact-apply'));

    await waitFor(() =>
      expect(screen.getByTestId('contact-form-error').textContent).toContain('không hợp lệ'));

    // Still open, still filled in — the sender fixes one field rather than retyping the form.
    expect((screen.getByTestId('contact-manual-name') as HTMLInputElement).value).toBe('Lê Thị Bếp');
    expect((screen.getByTestId('contact-manual-reason') as HTMLInputElement).value).toBe('Nhà thầu ngoài');
  });

  /**
   * Client-side validation exists for the user's benefit and must not become a second, disagreeing rule:
   * an incomplete manual form is reported without a round trip, and nothing is committed.
   */
  it('reports an incomplete manual form without calling the server', async () => {
    renderPanel(OPTIONAL);

    fireEvent.click(screen.getByTestId('contact-change'));
    fireEvent.click(screen.getByDisplayValue('MANUAL'));
    fireEvent.change(screen.getByTestId('contact-manual-name'), { target: { value: 'Lê Thị Bếp' } });
    fireEvent.click(screen.getByTestId('contact-apply'));

    await waitFor(() => expect(screen.getByTestId('contact-form-error')).toBeTruthy());
    expect(previewEmailContact).not.toHaveBeenCalled();
  });
});
