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
  const renderModal = async (body: string) => {
    return render(
      <EmailPreviewModal
        open
        loading={false}
        sending={false}
        error={null}
        subject="Chủ đề"
        body={body}
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

  it('draws the buttons inside the message, between the words that introduce them', async () => {
    await renderModal(`<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`);

    const view = await screen.findByTestId('view-body');
    const text = view.innerHTML;

    expect(text.indexOf('INTRO')).toBeLessThan(text.indexOf('NUT-PHAN-HOI'));
    expect(text.indexOf('NUT-PHAN-HOI')).toBeLessThan(text.indexOf('SIGNATURE'));
  });

  it('shows the buttons exactly once — inline, not also in the panel below', async () => {
    const { container } = await renderModal(`<p>INTRO</p>${SYSTEM_ACTION_NODE}<p>SIGNATURE</p>`);

    await screen.findByTestId('view-body');

    expect(container.innerHTML.match(/NUT-PHAN-HOI/g) ?? []).toHaveLength(1);
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

  it('falls back to the panel when the body carries no node', async () => {
    const { container } = await renderModal('<p>INTRO</p><p>SIGNATURE</p>');

    const view = await screen.findByTestId('view-body');

    // Not in the message…
    expect(view.innerHTML).not.toContain('NUT-PHAN-HOI');
    // …but still shown, so the sender knows which buttons the recipient gets.
    expect(container.innerHTML).toContain('NUT-PHAN-HOI');
  });
});
