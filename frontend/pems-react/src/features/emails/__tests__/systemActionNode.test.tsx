/**
 * The action block is shown where it will actually appear (V4 §9.1, §10.1).
 *
 * The prepared body used to have its action area cut out and returned separately, so the modal could only
 * print the buttons in a panel underneath the message. That made one thing impossible to see: whether a
 * sentence introducing the buttons — "chọn một phương án bên dưới" — actually pointed at them. It usually
 * did not, because the send appended the real block after the signature.
 *
 * The body now carries an inert `<div data-system-block="action">` at the template's chosen position, and
 * the read-only stage draws the (disabled, tokenless) block over it.
 */
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { EmailPreviewModal } from '../../delegations/components/EmailPreviewModal';
import {
  SYSTEM_ACTION_NODE,
  countSystemActionNodes,
  hasSystemActionNode,
  renderSystemActionNode,
} from '../utils/systemActionNode';

describe('systemActionNode', () => {
  it.each([
    '<div data-system-block="action"></div>',
    "<div data-system-block='action'></div>",
    '<div class="x" data-system-block="action"></div>',
    '<div data-system-block="action" class="x"></div>',
    '<div data-system-block="action">   </div>',
    '<div  DATA-SYSTEM-BLOCK = "action" ></div >',
  ])('recognises the node however the editor respelled it: %s', (node) => {
    expect(hasSystemActionNode(node)).toBe(true);
  });

  it('does not mistake an ordinary empty div for the node', () => {
    expect(hasSystemActionNode('<div></div><div class="spacer"></div>')).toBe(false);
    expect(hasSystemActionNode('<p>nội dung</p>')).toBe(false);
    expect(hasSystemActionNode('')).toBe(false);
    expect(hasSystemActionNode(null)).toBe(false);
  });

  it('counts nodes, so a duplicate can be reported rather than rendered twice', () => {
    expect(countSystemActionNodes(SYSTEM_ACTION_NODE + SYSTEM_ACTION_NODE)).toBe(2);
    expect(countSystemActionNodes('<p>x</p>')).toBe(0);
  });

  it('draws the block at the node position, not at the end', () => {
    const html = renderSystemActionNode(
      `<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`,
      '<div>BUTTONS</div>',
    );

    expect(html.indexOf('INTRO')).toBeLessThan(html.indexOf('BUTTONS'));
    expect(html.indexOf('BUTTONS')).toBeLessThan(html.indexOf('SIGNATURE'));
    expect(html).not.toContain('data-system-block');
  });

  it('leaves a body with no node untouched', () => {
    const body = '<p>không có khối hành động</p>';
    expect(renderSystemActionNode(body, '<div>BUTTONS</div>')).toBe(body);
  });

  it('inserts block markup containing $ verbatim', () => {
    // An action URL may carry a query string; `$&` in a replacement would splice the match back in.
    const block = '<a href="https://x.test/a?b=$1&c=$&">Đồng ý</a>';
    expect(renderSystemActionNode(SYSTEM_ACTION_NODE, block)).toBe(block);
  });

  it('is safe to call repeatedly — the regex carries no lastIndex between calls', () => {
    // A /g regex reused across calls silently skips every other match if lastIndex is not reset.
    expect(hasSystemActionNode(SYSTEM_ACTION_NODE)).toBe(true);
    expect(hasSystemActionNode(SYSTEM_ACTION_NODE)).toBe(true);
    expect(hasSystemActionNode(SYSTEM_ACTION_NODE)).toBe(true);
  });
});

describe('EmailPreviewModal VIEW stage', () => {
  // Statically imported, and deliberately NOT behind vi.resetModules() + a dynamic import. That
  // combination made these two tests fail roughly one run in four: resetting the registry mid-suite while
  // the modal pulls in a real Quill lets the component under test and the helpers it calls resolve to
  // different copies of the same module, so the node the test wrote was not the node the component looked
  // for. Nothing here needs a mock — VIEW touches no auth and no editor.
  const renderModal = async (body: string, initialFinalPreviewHtml?: string) => {
    return render(
      <EmailPreviewModal
        open
        loading={false}
        sending={false}
        error={null}
        subject="Chủ đề"
        body={body}
        initialFinalPreviewHtml={initialFinalPreviewHtml}
        isActionTemplate
        lockedActionBlockHtml='<div><span>NUT-PHAN-HOI</span></div>'
        canSend={false}
        sendLabel="Gửi"
        onSubjectChange={vi.fn()}
        onBodyChange={vi.fn()}
        onClose={vi.fn()}
        onSend={vi.fn()}
        onRestore={vi.fn()}
      />,
    );
  };

  /**
   * What the backend assembled, shown as-is.
   *
   * The editable body it is built from is passed too, and deliberately says something different: if the
   * screen were still composing its own view out of `body`, these assertions would find the editable
   * text instead of the assembled message and say so.
   */
  it('shows the backend-assembled message, not one it builds out of the editable body', async () => {
    await renderModal(
      `<p>BAN-SOAN-THAO</p>${SYSTEM_ACTION_NODE}`,
      '<div>MAU-EMAIL<p>INTRO</p><span>NUT-PHAN-HOI</span><p>SIGNATURE</p></div>',
    );

    const view = await screen.findByTestId('view-body');

    expect(view.innerHTML).toContain('MAU-EMAIL');
    expect(view.innerHTML).not.toContain('BAN-SOAN-THAO');
  });

  it('draws the buttons inside the message, between the words that introduce them', async () => {
    await renderModal(
      `<p>x</p>${SYSTEM_ACTION_NODE}`,
      '<div><p>INTRO</p><span>NUT-PHAN-HOI</span><p>SIGNATURE</p></div>',
    );

    const view = await screen.findByTestId('view-body');
    const text = view.innerHTML;

    expect(text.indexOf('INTRO')).toBeLessThan(text.indexOf('NUT-PHAN-HOI'));
    expect(text.indexOf('NUT-PHAN-HOI')).toBeLessThan(text.indexOf('SIGNATURE'));
  });

  /**
   * No technical panel in VIEW — not the caption, and not a second copy of the buttons.
   *
   * The panel described something already on screen, and sitting under the message it read as another
   * set of buttons appended to the end. Both halves are asserted because removing only the duplicate
   * markup while leaving the caption would still tell a sender their message carries a separate
   * "system section" that no recipient will see.
   */
  it('shows no separate system-action section', async () => {
    const { container } = await renderModal(
      `<p>x</p>${SYSTEM_ACTION_NODE}`,
      '<div><p>INTRO</p><span>NUT-PHAN-HOI</span><p>SIGNATURE</p></div>',
    );

    await screen.findByTestId('view-body');

    expect(container.innerHTML.match(/NUT-PHAN-HOI/g) ?? []).toHaveLength(1);
    expect(screen.queryByText(/Nút phản hồi hệ thống/i)).toBeNull();
  });

  /**
   * A response prepared before the assembled field existed still renders a message rather than a blank
   * panel — the buttons drawn into the node, as the screen used to do it.
   */
  it('falls back to composing from the body when no assembled message arrives', async () => {
    await renderModal(`<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`);

    const view = await screen.findByTestId('view-body');
    const text = view.innerHTML;

    expect(text.indexOf('INTRO')).toBeLessThan(text.indexOf('NUT-PHAN-HOI'));
    expect(text.indexOf('NUT-PHAN-HOI')).toBeLessThan(text.indexOf('SIGNATURE'));
  });

  /**
   * The send modal's EDIT stage is the COMPOSE half of the shared editor (V4 §5.1, §18.3).
   *
   * It used to build its own five-button ReactQuill, separately from the template screen's. Two editors
   * meant two answers to "may I centre this?", and the one that mattered was whichever the recipient's
   * mail client rendered.
   */
  it('opens the shared editor, in COMPOSE mode, when the sender chooses to edit', async () => {
    render(
      <EmailPreviewModal
        open
        loading={false}
        sending={false}
        error={null}
        subject="Chủ đề"
        body={`<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`}
        isActionTemplate
        lockedActionBlockHtml='<div><span>NUT-PHAN-HOI</span></div>'
        runtimeEditable
        previewToken="tok"
        canSend
        sendLabel="Gửi"
        onSubjectChange={vi.fn()}
        onBodyChange={vi.fn()}
        onClose={vi.fn()}
        onSend={vi.fn()}
        onRestore={vi.fn()}
      />,
    );

    fireEvent.click(await screen.findByRole('button', { name: /Chỉnh sửa/ }));

    // The shared toolbar, not the old five buttons.
    expect(await screen.findByRole('toolbar', { name: /Định dạng nội dung email/ })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Căn giữa' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Tăng thụt lề' })).toBeTruthy();

    // COMPOSE withholds both: the text is already substituted, and the flow — not the sender — decides
    // whether this message has an action area at all.
    expect(screen.queryByRole('button', { name: 'Chèn biến' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeNull();
  });

  /**
   * The panel belongs to EDIT, where there is something it can explain.
   *
   * In EDIT the action area is an object the author may move but not reword, and that rule is invisible
   * from the object itself. In the read-only stages the buttons are simply where the recipient will find
   * them, so the panel repeated what was already visible — and, printed under the message, looked like a
   * second set of buttons.
   */
  it('explains the locked buttons in EDIT, and only there', async () => {
    render(
      <EmailPreviewModal
        open
        loading={false}
        sending={false}
        error={null}
        subject="Chủ đề"
        body={`<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`}
        initialFinalPreviewHtml="<div><p>INTRO</p><span>NUT-PHAN-HOI</span></div>"
        isActionTemplate
        systemActionDescription="Nút Chấp nhận/Từ chối do hệ thống gắn."
        lockedActionBlockHtml='<div><span>NUT-PHAN-HOI</span></div>'
        runtimeEditable
        previewToken="tok"
        canSend
        sendLabel="Gửi"
        onSubjectChange={vi.fn()}
        onBodyChange={vi.fn()}
        onClose={vi.fn()}
        onSend={vi.fn()}
        onRestore={vi.fn()}
      />,
    );

    expect(screen.queryByText(/Nút phản hồi hệ thống/i)).toBeNull();

    fireEvent.click(await screen.findByRole('button', { name: /Chỉnh sửa/ }));

    expect(await screen.findByText(/Nút phản hồi hệ thống/i)).toBeTruthy();
    expect(screen.getByText(/Nút Chấp nhận\/Từ chối do hệ thống gắn/)).toBeTruthy();
  });

  /**
   * A body with no node cannot show the author where the buttons will land, so EDIT keeps the one copy
   * that tells them which buttons the message carries.
   */
  it('keeps a copy of the buttons in EDIT when the body carries no node', async () => {
    const { container } = render(
      <EmailPreviewModal
        open
        loading={false}
        sending={false}
        error={null}
        subject="Chủ đề"
        body="<p>INTRO</p><p>SIGNATURE</p>"
        initialFinalPreviewHtml="<div><p>INTRO</p></div>"
        isActionTemplate
        lockedActionBlockHtml='<div><span>NUT-PHAN-HOI</span></div>'
        runtimeEditable
        previewToken="tok"
        canSend
        sendLabel="Gửi"
        onSubjectChange={vi.fn()}
        onBodyChange={vi.fn()}
        onClose={vi.fn()}
        onSend={vi.fn()}
        onRestore={vi.fn()}
      />,
    );

    fireEvent.click(await screen.findByRole('button', { name: /Chỉnh sửa/ }));
    await screen.findByText(/Nút phản hồi hệ thống/i);

    expect(container.innerHTML).toContain('NUT-PHAN-HOI');
  });
});
