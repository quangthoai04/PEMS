/**
 * The shared email editor (V4 §5–§7), against a REAL Quill.
 *
 * These deliberately do not mock `react-quill-new`. Every pre-existing editor test in this project does,
 * and that is precisely how two defects reached this branch unnoticed: the action node being dropped, and
 * every alignment arriving at the recipient as a `ql-align-center` class no mail client has a rule for.
 * An editor test that mocks the editor tests the mock.
 */
import React, { useState } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, fireEvent, waitFor } from '@testing-library/react';
// @ts-ignore - react-quill-new ships without bundled types in this project
import { Quill } from 'react-quill-new';
import {
  EmailRichTextEditor, type EmailRichTextEditorHandle,
} from '../components/EmailRichTextEditor';
import { isSameEmailHtml } from '../utils/emailHtmlCanonicalizer';
import { EMAIL_FONTS, EMAIL_SIZES } from '../utils/emailEditorFormats';
import { SYSTEM_ACTION_NODE, countSystemActionNodes } from '../utils/systemActionNode';
import { SPACE_RUN_WARNING, cleanInlineStyle, hasSpaceRun, normalizeSpaceRuns } from '../utils/emailEditorPaste';
import { COMPOSE_CAPABILITIES, TEMPLATE_CAPABILITIES, capabilitiesFor } from '../utils/emailEditorCapabilities';

/**
 * Renders the editor the way a host screen actually uses it: STATEFUL.
 *
 * A static `value` with a spy `onChange` is not a simplification here, it is a different component — the
 * editor is controlled, so an unchanging prop makes React re-render with the original document and Quill
 * reload it, reverting every edit a moment after it happens. Tests written that way pass or fail on
 * timing rather than on behaviour.
 */
function setup(props: Partial<React.ComponentProps<typeof EmailRichTextEditor>> = {}) {
  const onNotice = vi.fn();
  const seen: string[] = [];

  function Host() {
    const [html, setHtml] = useState(props.value ?? '<p>xin chào</p>');
    return (
      <EmailRichTextEditor
        mode="TEMPLATE"
        onNotice={onNotice}
        data-testid="editor"
        {...props}
        value={html}
        onChange={(next) => { seen.push(next); setHtml(next); }}
      />
    );
  }

  const utils = render(<Host />);
  const root = utils.container.querySelector('.ql-editor') as HTMLElement;

  return {
    ...utils,
    onNotice,
    root,
    /**
     * The document as it stands now, read from the live editor rather than from a spy's argument.
     * `.ql-editor`'s innerHTML IS Quill's document — no lookup needed, and it stays correct across the
     * re-renders a controlled component goes through.
     */
    html: () => root.innerHTML,
    /** Every value the editor has emitted, i.e. what the host screen would have stored. */
    emitted: () => seen,
  };
}

/**
 * Puts QUILL's own selection over the whole document, which is what an inline format applies to.
 * A DOM Range is not enough: Quill tracks its selection separately, and a format applied while its
 * selection is collapsed lands on the cursor rather than on the text.
 */
function quillOf(container: HTMLElement) {
  return (Quill as any).find(container.querySelector('.ql-container') as HTMLElement);
}

function selectAll(container: HTMLElement) {
  const q = quillOf(container);
  q.setSelection(0, q.getLength(), 'user');
  return q;
}

describe('it mounts a real editor', () => {
  it('renders the Quill surface and the toolbar', () => {
    const { container } = setup();

    expect(container.querySelector('.ql-editor')).toBeTruthy();
    expect(screen.getByRole('toolbar', { name: /Định dạng nội dung email/ })).toBeTruthy();
  });

  it('shows the document it was given', () => {
    const { root } = setup({ value: '<p>Kính gửi anh Nam</p>' });
    expect(root.textContent).toContain('Kính gửi anh Nam');
  });
});

// ── §6.1 the toolbar exists, in full ────────────────────────────────────────

describe('the toolbar offers what V4 §6.1 lists', () => {
  it.each([
    'Hoàn tác', 'Làm lại', 'Đậm', 'Nghiêng', 'Gạch chân', 'Gạch ngang',
    'Căn trái', 'Căn giữa', 'Căn phải',
    'Danh sách đánh số', 'Danh sách gạch đầu dòng',
    'Giảm thụt lề', 'Tăng thụt lề',
    'Chèn liên kết', 'Chèn đường kẻ ngang', 'Xóa định dạng', 'Toàn màn hình',
  ])('has a %s control', (label) => {
    setup();
    expect(screen.getByRole('button', { name: label })).toBeTruthy();
  });

  it('offers the font and size ladders, and nothing outside them', () => {
    setup();

    const fonts = screen.getByLabelText('Phông chữ') as HTMLSelectElement;
    const sizes = screen.getByLabelText('Cỡ chữ') as HTMLSelectElement;

    // The empty first option is the "unset" placeholder, not a value.
    expect(Array.from(fonts.options).slice(1).map((o) => o.value)).toEqual([...EMAIL_FONTS]);
    expect(Array.from(sizes.options).slice(1).map((o) => o.value)).toEqual([...EMAIL_SIZES]);
  });

  /**
   * The formatting controls do NOT depend on the mode.
   *
   * What separates TEMPLATE from COMPOSE is authority over variables and the action block — who may place
   * a placeholder, who may create an action area. It is not "may I centre this line", and hiding half the
   * toolbar behind a mode is how the two screens drifted apart in the first place: the same wording came
   * out differently depending on where it was typed.
   */
  it.each([
    'Hoàn tác', 'Làm lại', 'Đậm', 'Nghiêng', 'Gạch chân', 'Gạch ngang',
    'Căn trái', 'Căn giữa', 'Căn phải',
    'Danh sách đánh số', 'Danh sách gạch đầu dòng',
    'Giảm thụt lề', 'Tăng thụt lề',
    'Chèn liên kết', 'Chèn bảng', 'Chèn đường kẻ ngang', 'Xóa định dạng', 'Toàn màn hình',
  ])('offers %s in COMPOSE too', (label) => {
    setup({ mode: 'COMPOSE' });
    expect(screen.getByRole('button', { name: label })).toBeTruthy();
  });

  it('offers the same font and size ladders in COMPOSE', () => {
    setup({ mode: 'COMPOSE' });

    const fonts = screen.getByLabelText('Phông chữ') as HTMLSelectElement;
    const sizes = screen.getByLabelText('Cỡ chữ') as HTMLSelectElement;

    expect(Array.from(fonts.options).slice(1).map((o) => o.value)).toEqual([...EMAIL_FONTS]);
    expect(Array.from(sizes.options).slice(1).map((o) => o.value)).toEqual([...EMAIL_SIZES]);
  });

  it('hides the image button when the host provides no uploader', () => {
    setup();
    expect(screen.queryByRole('button', { name: 'Chèn ảnh' })).toBeNull();
  });

  it('shows the image button when it can actually upload', () => {
    setup({ onUploadImage: vi.fn() });
    expect(screen.getByRole('button', { name: 'Chèn ảnh' })).toBeTruthy();
  });
});

// ── §6.2 / §6.3 / §7.2 the output an email can carry ────────────────────────

describe('formatting produces inline CSS, not classes', () => {
  it('aligns with a style attribute', async () => {
    const { container, html } = setup({ value: '<p>giữa</p>' });
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Căn giữa' }));

    await waitFor(() => expect(html()).toContain('text-align: center'));
    // The default Quill behaviour, which no mail client can render.
    expect(html()).not.toContain('ql-align');
  });

  it('indents with margin-left, stepping through the fixed ladder', async () => {
    const { container, html } = setup({ value: '<p>thụt lề</p>' });
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Tăng thụt lề' }));

    await waitFor(() => expect(html()).toContain('margin-left: 16px'));
    expect(html()).not.toContain('ql-indent');
  });

  it('sets a size from the ladder as a style', async () => {
    const { container, html } = setup({ value: '<p>cỡ chữ</p>' });
    selectAll(container);

    fireEvent.change(screen.getByLabelText('Cỡ chữ'), { target: { value: '18px' } });

    await waitFor(() => expect(html()).toContain('font-size: 18px'));
  });

  it('sets a font from the whitelist as a style', async () => {
    const { container, html } = setup({ value: '<p>phông</p>' });
    selectAll(container);

    fireEvent.change(screen.getByLabelText('Phông chữ'), { target: { value: 'Georgia' } });

    await waitFor(() => expect(html()).toContain('font-family: Georgia'));
  });

  it('inserts a divider, which Quill drops without a blot for it', async () => {
    const { html } = setup({ value: '<p>trên</p>' });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn đường kẻ ngang' }));

    await waitFor(() => expect(html()).toContain('<hr'));
  });
});

// ── §11 / §12 the toolbar applies to the operator's selection, every time ───

/**
 * Every formatting control, applied to a selection the operator made and then LOST focus on — which is
 * what actually happens: a colour input, a font list and a table dialog all take focus away from the
 * editor before their handler runs.
 *
 * <b>The defect these pin.</b> Two formats in a row worked and the third silently did nothing. Measured
 * on quill 2.0.3: after a format the host stores the new html, a controlled editor re-renders, and
 * Quill's selection comes back COLLAPSED at index 0. `q.focus()` restores the range Quill last saw —
 * the collapsed one — so every later format landed on a cursor. An operator selecting a heading and
 * setting size, then font, then colour, got a heading with no colour and no explanation.
 */
describe('the toolbar formats what is selected', () => {
  /** Selects "xin" and then blurs, the way clicking any toolbar control does. */
  function selectAndBlur(container: HTMLElement) {
    const q = quillOf(container);
    q.setSelection(0, 3, 'user');
    q.blur();
    return q;
  }

  it('applies three formats in a row to the SAME selection', async () => {
    const { container, html } = setup({ value: '<p>xin chào</p>' });
    selectAndBlur(container);

    fireEvent.change(screen.getByLabelText('Cỡ chữ'), { target: { value: '18px' } });
    await waitFor(() => expect(html()).toContain('font-size: 18px'));

    fireEvent.change(screen.getByLabelText('Phông chữ'), { target: { value: 'Georgia' } });
    await waitFor(() => expect(html()).toContain('font-family: Georgia'));

    // The one that used to be dropped.
    fireEvent.change(screen.getByLabelText('Màu chữ'), { target: { value: '#ff0000' } });
    await waitFor(() => expect(html()).toContain('color: rgb(255, 0, 0)'));

    // …all three on the words, not on the rest of the line.
    expect(html()).toContain('chào');
  });

  it.each([
    ['Đậm', '<strong>'],
    ['Nghiêng', '<em>'],
    ['Gạch chân', '<u>'],
    ['Gạch ngang', '<s>'],
  ])('applies %s after the editor has lost focus', async (label, tag) => {
    const { container, html } = setup({ value: '<p>xin chào</p>' });
    selectAndBlur(container);

    fireEvent.click(screen.getByRole('button', { name: label }));

    await waitFor(() => expect(html()).toContain(tag));
  });

  it('applies a background colour after a colour has already been set', async () => {
    const { container, html } = setup({ value: '<p>xin chào</p>' });
    selectAndBlur(container);

    fireEvent.change(screen.getByLabelText('Màu chữ'), { target: { value: '#ff0000' } });
    await waitFor(() => expect(html()).toContain('color: rgb(255, 0, 0)'));

    fireEvent.change(screen.getByLabelText('Màu nền'), { target: { value: '#ffff00' } });

    await waitFor(() => expect(html()).toContain('background-color: rgb(255, 255, 0)'));
  });

  it.each([
    ['Căn giữa', 'text-align: center'],
    ['Căn phải', 'text-align: right'],
    ['Tăng thụt lề', 'margin-left: 16px'],
  ])('applies %s to the line the caret was on', async (label, style) => {
    const { container, html } = setup({ value: '<p>xin chào</p><p>dòng hai</p>' });
    const q = quillOf(container);
    q.setSelection(1, 0, 'user');
    q.blur();

    fireEvent.click(screen.getByRole('button', { name: label }));

    await waitFor(() => expect(html()).toContain(style));
    // The FIRST line, which is where the caret was — not whichever line Quill defaulted to.
    expect(html().indexOf(style)).toBeLessThan(html().indexOf('dòng hai'));
  });

  it.each([
    ['Danh sách gạch đầu dòng', '<ul>'],
    ['Danh sách đánh số', '<ol>'],
  ])('turns the caret line into a %s', async (label, tag) => {
    const { container, emitted } = setup({ value: '<p>một</p><p>hai</p>' });
    const q = quillOf(container);
    q.setSelection(1, 0, 'user');
    q.blur();

    fireEvent.click(screen.getByRole('button', { name: label }));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain(tag));
    expect(emitted().at(-1)).toContain('một');
  });

  it('clears formatting from the selection it was given', async () => {
    const { container, html } = setup({ value: '<p><strong>đậm</strong> thường</p>' });
    const q = quillOf(container);
    q.setSelection(0, 3, 'user');
    q.blur();

    fireEvent.click(screen.getByRole('button', { name: 'Xóa định dạng' }));

    await waitFor(() => expect(html()).not.toContain('<strong>'));
    expect(html()).toContain('đậm');
  });

  it('puts a link on the selected words after the prompt has taken focus', async () => {
    const prompt = vi.spyOn(window, 'prompt').mockReturnValue('https://pems.fpt.edu.vn/x');
    const { container, html } = setup({ value: '<p>xin chào</p>' });
    selectAndBlur(container);

    fireEvent.click(screen.getByRole('button', { name: 'Chèn liên kết' }));

    await waitFor(() => expect(html()).toContain('href="https://pems.fpt.edu.vn/x"'));
    // On the words, not on the whole line.
    expect(html()).toContain('chào');
    prompt.mockRestore();
  });
});

// ── §40 a controlled editor survives the screen around it re-rendering ──────

/**
 * The host screen re-renders for its own reasons — a keystroke in the subject, a toast, a fetch landing.
 * None of that is an edit to the body, and none of it may reload the document.
 *
 * <b>What this pins.</b> react-quill-new compares the value it was handed against what the editor holds,
 * on every render, and re-runs `setContents` when they differ. Any lasting difference — a style attribute
 * we re-spelled, a trailing blank block Quill's parse drops — makes that comparison answer "different"
 * forever: the whole document is rebuilt on every render, the caret is discarded, and live DOM nodes are
 * replaced under whatever was holding one. It is invisible until an operator loses a selection mid-format
 * or a fan spins up, which is exactly why it is pinned here rather than left to be noticed.
 */
describe('stability across an unrelated re-render', () => {
  /** Renders the editor beside a counter the test can bump without touching the body. */
  function setupWithSibling(value: string) {
    const seen: string[] = [];

    function Host() {
      const [html, setHtml] = useState(value);
      const [tick, setTick] = useState(0);
      return (
        <>
          <button type="button" data-testid="bump" onClick={() => setTick(tick + 1)}>{tick}</button>
          <EmailRichTextEditor
            mode="TEMPLATE"
            variables={[{ name: 'fullName', label: 'Họ tên' }]}
            systemBlocks={[{ name: 'actionBlock', label: 'Khu vực nút thao tác' }]}
            value={html}
            onChange={(next) => { seen.push(next); setHtml(next); }}
          />
        </>
      );
    }

    const utils = render(<Host />);
    return { ...utils, emitted: () => seen };
  }

  it.each([
    ['plain text', '<p>xin chào</p>'],
    ['a multi-declaration style', '<p style="color:#334155;font-size:14px;line-height:1.65">Kính gửi,</p>'],
    ['a variable', '<p>Kính gửi {{fullName}}</p>'],
    ['a system block', '<p>a</p>{{actionBlock}}<p>b</p>'],
    ['a table', '<table role="presentation" style="border-collapse:collapse"><tbody><tr><td>ô</td></tr></tbody></table><p>sau bảng</p>'],
    ['a divider', '<p>a</p><hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0"><p>b</p>'],
    ['a list', '<ul><li>Một</li><li>Hai</li></ul>'],
  ])('keeps the document and the caret through a re-render with %s', async (_label, value) => {
    const utils = setupWithSibling(value);
    const q = quillOf(utils.container);

    q.setSelection(1, 0, 'user');
    const before = utils.container.querySelector('.ql-editor')!.firstElementChild;

    fireEvent.click(screen.getByTestId('bump'));
    await waitFor(() => expect(screen.getByTestId('bump').textContent).toBe('1'));
    fireEvent.click(screen.getByTestId('bump'));
    await waitFor(() => expect(screen.getByTestId('bump').textContent).toBe('2'));

    // The same nodes, not replacements: a rebuild is what discards the caret and detaches selections.
    expect(utils.container.querySelector('.ql-editor')!.firstElementChild).toBe(before);
    expect(q.getSelection()).toEqual({ index: 1, length: 0 });
    // …and nothing was reported to the host as a change, because nobody edited anything.
    expect(utils.emitted()).toHaveLength(0);
  });
});

// ── §9 the action block, through the shared editor ──────────────────────────

describe('the system action block', () => {
  /**
   * The button offers what the CONTRACT allows, and nothing when it allows nothing.
   *
   * TEMPLATE and COMPOSE insert two different things under one button — a `{{placeholder}}` the renderer
   * substitutes, and a position node inside content already rendered. See `emailEditorTemplateBlocks.ts`;
   * the suite below the variables covers the template half in full.
   */
  it('is offered in TEMPLATE when the contract has a block, and withheld in COMPOSE', () => {
    const blocks = [{ name: 'actionBlock', label: 'Khu vực nút thao tác' }];

    const { unmount } = setup({ mode: 'TEMPLATE', systemBlocks: blocks });
    expect(screen.getByRole('button', { name: 'Chèn khối hệ thống' })).toBeTruthy();
    unmount();

    // A template whose send path attaches no block offers no button: a placeholder saved here would be
    // refused by the renderer, so it must not be insertable in the first place.
    const bare = setup({ mode: 'TEMPLATE' });
    expect(screen.queryByRole('button', { name: 'Chèn khối hệ thống' })).toBeNull();
    bare.unmount();

    setup({ mode: 'COMPOSE', systemBlocks: blocks });
    expect(screen.queryByRole('button', { name: 'Chèn khối hệ thống' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeNull();
  });

  it('survives the editor and comes back in canonical form', async () => {
    const { emitted, html } = setup({ value: `<p>a</p>${SYSTEM_ACTION_NODE}<p>b</p>` });

    // Mounting alone round-trips the document through Quill — the node must still be there afterwards.
    expect(html()).toContain('data-system-block');

    // …and what the host would store is the CANONICAL node, exactly once: no editor class, no label.
    if (emitted().length > 0) {
      expect(countSystemActionNodes(emitted().at(-1)!)).toBe(1);
      expect(emitted().at(-1)!).not.toContain('pems-system-action-block');
    }
  });

  it('refuses a second position node rather than minting one token into two buttons', () => {
    // The COMPOSE half, reached the way a runtime-edit flow reaches it: the capability is granted.
    const { onNotice } = setup({
      mode: 'COMPOSE',
      capabilities: { allowSystemBlockInsert: true },
      value: `<p>a</p>${SYSTEM_ACTION_NODE}`,
    });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn khối nút phản hồi' }));

    expect(onNotice).toHaveBeenCalledWith(expect.stringContaining('một khối nút phản hồi'));
  });
});

// ── §8.1 variables ──────────────────────────────────────────────────────────

describe('variable insertion', () => {
  const variables = [
    { name: 'senderName', label: 'Họ tên người gửi' },
    { name: 'delegationName', label: 'Tên đoàn' },
  ];

  it('is offered in TEMPLATE mode only — COMPOSE text is already substituted', () => {
    const { unmount } = setup({ mode: 'TEMPLATE', variables });
    expect(screen.getByRole('button', { name: 'Chèn biến' })).toBeTruthy();
    unmount();

    setup({ mode: 'COMPOSE', variables });
    expect(screen.queryByRole('button', { name: 'Chèn biến' })).toBeNull();
  });

  /**
   * The chip is the whole point of §8.1: a label on screen, a placeholder on the wire. Asserting the
   * editor DOM contains `{{senderName}}` would be asserting the feature is absent.
   */
  it('shows a chip in the editor and stores the placeholder', async () => {
    const { html, emitted } = setup({ mode: 'TEMPLATE', variables, value: '<p>Kính gửi </p>' });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn biến' }));
    fireEvent.click(screen.getByRole('button', { name: /Họ tên người gửi/ }));

    // On screen: the friendly label, as an object.
    await waitFor(() => expect(html()).toContain('data-variable="senderName"'));
    expect(html()).toContain('Họ tên người gửi');

    // On the wire: the placeholder the renderer substitutes, and no trace of the label.
    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('{{senderName}}'));
    expect(emitted().at(-1)).not.toContain('Họ tên người gửi');
    expect(emitted().at(-1)).not.toContain('pems-variable-chip');
  });

  it('inserts a table the mail client can render, and stores it bare', async () => {
    const { html, emitted } = setup({ mode: 'TEMPLATE', value: '<p>trên</p>' });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn bảng' }));
    fireEvent.click(await screen.findByTestId('table-dialog-apply'));

    await waitFor(() => expect(html()).toContain('<table'));
    // §17 — inline CSS, no stylesheet exists in mail.
    expect(html()).toContain('border-collapse:collapse');
    expect(html()).toContain('role="presentation"');

    // Stored without the editor's wrapper.
    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('<table'));
    expect(emitted().at(-1)).not.toContain('data-email-table');
  });
});

// ── §5 / §6 / §7 system blocks inside a TEMPLATE ────────────────────────────

/**
 * A system block is not a variable, and a template does not hold the COMPOSE node.
 *
 * <b>What was wrong, measured before the fix.</b> A stored body carrying `{{actionBlock}}` rendered as
 * `<span class="pems-variable-chip" data-variable="actionBlock">actionBlock</span>` — an ordinary data
 * chip, labelled with the raw name, indistinguishable from `{{fullName}}`. And "Chèn khối nút phản hồi"
 * wrote `<div data-system-block="action"></div>` INTO THE TEMPLATE: markup the runtime renderer never
 * looks at, because what it substitutes is `{{actionBlock}}`. Saving that produced a template whose
 * buttons simply do not exist in the delivered mail, with nothing on screen saying so.
 */
describe('system blocks in a template', () => {
  const blocks = [
    { name: 'actionBlock', label: 'Khu vực nút thao tác' },
    { name: 'setupSummaryBlock', label: 'Bảng thông tin chuẩn bị' },
  ];
  const variables = [{ name: 'fullName', label: 'Họ tên' }];

  const blockNodes = (container: HTMLElement) =>
    Array.from(container.querySelectorAll('.ql-editor [data-template-block]')) as HTMLElement[];

  it('shows a stored placeholder as a protected object, not as a variable chip', () => {
    const { container } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: blocks,
      value: '<p>Trước</p>{{actionBlock}}<p>Sau</p>',
    });

    const [node] = blockNodes(container);
    expect(node).toBeTruthy();
    expect(node.getAttribute('data-template-block')).toBe('actionBlock');
    expect(node.textContent).toContain('Khu vực nút thao tác');
    // Not a data variable — that is the whole point.
    expect(container.querySelector('.ql-editor [data-variable="actionBlock"]')).toBeNull();
    // And not editable inside: the backend owns what goes there.
    expect(node.getAttribute('contenteditable')).toBe('false');
  });

  it('stores it back as the placeholder the renderer substitutes', async () => {
    const { container, emitted } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: blocks,
      value: '<p>Trước</p>{{actionBlock}}<p>Sau</p>',
    });

    const q = quillOf(container);
    await act(async () => { q.insertText(0, 'x', 'user'); });

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('{{actionBlock}}'));
    const out = emitted().at(-1) ?? '';
    // Never the editor's furniture, and never the COMPOSE node.
    expect(out).not.toContain('pems-template-block');
    expect(out).not.toContain('data-template-block');
    expect(out).not.toContain('data-system-block');
    expect(out).not.toContain('Khu vực nút thao tác');
    expect(out).toContain('Trước');
    expect(out).toContain('Sau');
  });

  it('keeps both blocks apart, and keeps ordinary variables as chips', () => {
    const { container } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: blocks,
      value: '<p>{{fullName}}</p>{{actionBlock}}{{setupSummaryBlock}}',
    });

    expect(blockNodes(container).map((n) => n.getAttribute('data-template-block')))
      .toEqual(['actionBlock', 'setupSummaryBlock']);
    expect(container.querySelector('.ql-editor [data-variable="fullName"]')).toBeTruthy();
  });

  it('inserts the PLACEHOLDER, never the compose node', async () => {
    const { container, emitted } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: [blocks[0]], value: '<p>Trước</p>',
    });

    // One allowed block: the button inserts it directly rather than opening a list.
    fireEvent.click(screen.getByRole('button', { name: 'Chèn khối hệ thống' }));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('{{actionBlock}}'));
    expect(emitted().at(-1)).not.toContain('data-system-block');
    expect(blockNodes(container)).toHaveLength(1);
  });

  it('offers a choice when the contract allows more than one', async () => {
    const { emitted } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: blocks, value: '<p>Trước</p>',
    });

    fireEvent.click(screen.getByRole('button', { name: 'Chèn khối hệ thống' }));
    fireEvent.click(await screen.findByRole('button', { name: /Bảng thông tin chuẩn bị/ }));

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('{{setupSummaryBlock}}'));
    expect(emitted().at(-1)).not.toContain('{{actionBlock}}');
  });

  it('refuses a second copy of the same block', async () => {
    const { onNotice, emitted } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: [blocks[0]],
      value: '<p>Trước</p>{{actionBlock}}',
    });
    const before = emitted().length;

    fireEvent.click(screen.getByRole('button', { name: 'Chèn khối hệ thống' }));

    expect(onNotice).toHaveBeenCalledWith(expect.stringContaining('Khu vực nút thao tác'));
    expect(emitted().length).toBe(before);
  });

  it('survives load → edit → save → reload unchanged', async () => {
    const stored = '<p>Kính gửi {{fullName}},</p>{{setupSummaryBlock}}<p>Trân trọng.</p>{{actionBlock}}';
    const { emitted } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: blocks, value: stored,
    });

    await waitFor(() => expect(document.querySelector('.ql-editor')).toBeTruthy());
    expect(isSameEmailHtml(emitted().at(-1) ?? stored, stored)).toBe(true);
  });

  /** A block written into a template that may not carry it is still an object, not silent text. */
  it('shows a block the contract does not list, rather than turning it into a variable', () => {
    const { container } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: [blocks[0]],
      value: '<p>a</p>{{setupSummaryBlock}}',
    });

    expect(blockNodes(container)).toHaveLength(1);
    expect(container.querySelector('.ql-editor [data-variable="setupSummaryBlock"]')).toBeNull();
  });

  /** A misspelling is NOT a block: it must stay a variable so the contract check can name it. */
  it('leaves a mistyped block name as an ordinary variable', () => {
    const { container } = setup({
      mode: 'TEMPLATE', variables, systemBlocks: blocks, value: '<p>{{actionBlok}}</p>',
    });

    expect(blockNodes(container)).toHaveLength(0);
    expect(container.querySelector('.ql-editor [data-variable="actionBlok"]')).toBeTruthy();
  });

  /** COMPOSE is the other representation, and must not be given the template one. */
  it('leaves a placeholder alone in COMPOSE, where a block is a position node', () => {
    const { container } = setup({
      mode: 'COMPOSE', value: '<p>Trước</p>{{actionBlock}}<p>Sau</p>',
    });

    expect(container.querySelectorAll('.ql-editor [data-template-block]')).toHaveLength(0);
  });
});

// ── §38 what the host would SAVE carries no editor furniture ────────────────

/**
 * The save-payload contract, on one document carrying every feature at once.
 *
 * Each of these has its own test above; this one exists because the failure they guard against is not
 * "one conversion is wrong" but "one conversion was forgotten" — a chip class, a wrapper div, a
 * `contenteditable` flag or a guard character reaching the database, the renderer and then a recipient.
 * Asserted on what the editor emits, which is exactly what the screen puts in the payload.
 */
describe('the save payload', () => {
  const EVERYTHING = [
    '<p style="text-align: center;"><span style="font-size: 18px; color: rgb(255, 0, 0);">Kính gửi {{fullName}}</span></p>',
    '<hr style="border:none;border-top:1px solid #e2e8f0;margin:20px 0">',
    '<ul><li>Một</li><li>Hai</li></ul>',
    '<table role="presentation" style="border-collapse:collapse"><tbody><tr>',
    '<td style="border:1px solid #dbe4ee">{{delegationName}}</td></tr></tbody></table>',
    '<p>Trân trọng, {{senderName}}</p>',
    '{{actionBlock}}',
  ].join('');

  it('is canonical content and nothing else', async () => {
    const { container, emitted } = setup({
      mode: 'TEMPLATE',
      variables: [
        { name: 'fullName', label: 'Họ tên' },
        { name: 'delegationName', label: 'Tên đoàn' },
        { name: 'senderName', label: 'Họ tên người gửi' },
      ],
      systemBlocks: [{ name: 'actionBlock', label: 'Khu vực nút thao tác' }],
      value: EVERYTHING,
    });

    const q = quillOf(container);
    await act(async () => { q.insertText(0, 'x', 'user'); });
    await waitFor(() => expect(emitted().length).toBeGreaterThan(0));
    const payload = emitted().at(-1) ?? '';

    // Editor furniture — every spelling of it.
    for (const forbidden of [
      'pems-variable-chip', 'data-variable', 'data-label',
      'pems-template-block', 'data-template-block',
      'pems-email-table', 'data-email-table', 'data-selected',
      'contenteditable', 'ql-ui', 'data-list',
      'Khu vực nút thao tác', 'Họ tên',        // labels shown on screen, never sent
      '﻿', '​',                      // Quill's guard characters
    ]) {
      expect(payload).not.toContain(forbidden);
    }

    // …and everything that IS content.
    for (const kept of [
      '{{fullName}}', '{{delegationName}}', '{{senderName}}', '{{actionBlock}}',
      '<hr', '<ul>', '<li>', '<table', 'border:1px solid #dbe4ee',
      'text-align: center', 'font-size: 18px', 'color: rgb(255, 0, 0)',
    ]) {
      expect(payload).toContain(kept);
    }
  });
});

// ── §16 lists reach storage in their canonical shape ────────────────────────

/**
 * Quill draws a list as `<ol><li data-list="bullet"><span class="ql-ui">` — its own CSS-dependent
 * spelling, with a marker element that means nothing outside the editor. None of that may be stored: a
 * mail client has no `ql-*` stylesheet, so a bulleted list saved that way arrives numbered.
 *
 * It already does not reach storage, because react-quill-new hands this component Quill's SEMANTIC html.
 * That is a property of the stack rather than of anything written here, which is exactly why it is worth
 * a test: a future change of that default would silently start saving editor markup.
 */
describe('list canonicalisation', () => {
  it.each([
    ['bullet', '<ul><li>Một</li><li>Hai</li></ul>', '<ul>'],
    ['ordered', '<ol><li>Một</li><li>Hai</li></ol>', '<ol>'],
  ])('stores a %s list as plain markup', async (_kind, stored, tag) => {
    const { container, emitted } = setup({ mode: 'TEMPLATE', value: stored });

    const q = quillOf(container);
    await act(async () => { q.insertText(0, 'x', 'user'); });

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('<li>'));
    const out = emitted().at(-1) ?? '';
    expect(out).toContain(tag);
    expect(out).not.toContain('ql-ui');
    expect(out).not.toContain('data-list');
    expect(out).toContain('Hai');
  });
});

// ── §4 / §7 two variables side by side ──────────────────────────────────────

/**
 * A caret between two adjacent variables — the difference between a template that can say
 * `{{senderName}} / {{senderRole}}` and one that cannot.
 *
 * <b>What was wrong.</b> The chip element carried `contenteditable="false"`, which reads as the obvious
 * way to say "this is an object". Quill 2 renders an inline embed as a guard character, a non-editable
 * content node, and a second guard — the two guards being the only caret positions immediately before and
 * after the object, and the only thing standing between two chips that touch. Marking the outer element
 * non-editable put those guards in a non-editable subtree, so no caret could reach them: two variables
 * inserted one after another became a wall, and the separator had to be typed BEFORE the second variable
 * or not at all.
 *
 * <b>What is asserted here, and what cannot be.</b> Clicking down between two inline boxes is a hit-test
 * against a rendered layout, and jsdom has no layout — so the click itself is not simulable, in this or
 * any other test in this project. What IS asserted is the two things that decide whether the click can
 * work: the guards exist and are inside an editable subtree, and text placed at the position between the
 * chips comes back out in stored content with both placeholders intact and no guard character in it.
 */
describe('two variables side by side', () => {
  const variables = [
    { name: 'senderName', label: 'Họ tên người gửi' },
    { name: 'senderRole', label: 'Vai trò người gửi' },
  ];

  const GUARD = '﻿';
  const chips = (container: HTMLElement) =>
    Array.from(container.querySelectorAll('.ql-editor [data-variable]')) as HTMLElement[];

  it('leaves the caret positions around a chip reachable', () => {
    const { container } = setup({
      mode: 'TEMPLATE', variables, value: '<p>{{senderName}}{{senderRole}}</p>',
    });

    const [first, second] = chips(container);
    expect(first).toBeTruthy();
    expect(second).toBeTruthy();

    for (const chip of [first, second]) {
      // The label is the untouchable part — not the element around it.
      expect(chip.getAttribute('contenteditable')).toBeNull();
      expect(chip.querySelector('span[contenteditable="false"]')).toBeTruthy();

      // A guard at each end, and nothing above them saying "not editable".
      const guards = Array.from(chip.childNodes).filter(
        (n) => n.nodeType === Node.TEXT_NODE && n.textContent === GUARD,
      );
      expect(guards).toHaveLength(2);
      expect(chip.closest('[contenteditable="false"]')).toBeNull();
    }
  });

  it('takes a character typed between them, and stores both placeholders around it', async () => {
    const { container, emitted } = setup({
      mode: 'TEMPLATE', variables, value: '<p>{{senderName}}{{senderRole}}</p>',
    });

    // Index 1 is exactly between the two embeds: each is one unit long.
    const q = quillOf(container);
    await act(async () => {
      q.insertText(1, '/', 'user');
    });

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('{{senderName}}'));
    const out = emitted().at(-1) ?? '';
    expect(out).toContain('{{senderName}}/{{senderRole}}');
    // Editor furniture never reaches stored content — §13.
    expect(out).not.toContain(GUARD);
    expect(out).not.toContain('​');
    expect(out).not.toContain('pems-variable-chip');
  });

  /**
   * The same with spaces around the separator — asserted by MEANING rather than by spelling.
   *
   * Quill writes a space at the edge of a text run as `&nbsp;`, here as everywhere else in this editor
   * (see the change-attribution suite, which says the same thing about ordinary typing). That is one
   * whitespace character, not a run, and both the canonicaliser and every mail client read it as a
   * space — so what is asserted is that the document MEANS `{{senderName}} / {{senderRole}}`. Matching
   * the entity literally would be pinning Quill's spelling, and rewriting it on the way out would be the
   * whitespace-editing V4 §7.4 refuses to do.
   */
  it('takes a spaced separator between them', async () => {
    const { container, emitted } = setup({
      mode: 'TEMPLATE', variables, value: '<p>{{senderName}}{{senderRole}}</p>',
    });

    const q = quillOf(container);
    await act(async () => {
      q.insertText(1, ' / ', 'user');
    });

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('/'));
    const out = emitted().at(-1) ?? '';
    expect(isSameEmailHtml(out, '<p>{{senderName}} / {{senderRole}}</p>')).toBe(true);
    expect(out).not.toContain(GUARD);
  });

  it('stores two touching variables with nothing at all between them', async () => {
    const { container, emitted } = setup({
      mode: 'TEMPLATE', variables, value: '<p>x{{senderName}}{{senderRole}}</p>',
    });

    // A real edit somewhere else, so the host is handed the document as it now stands.
    const q = quillOf(container);
    await act(async () => {
      q.insertText(0, 'y', 'user');
    });

    await waitFor(() => expect(emitted().at(-1) ?? '').toContain('{{senderRole}}'));
    expect(emitted().at(-1)).toContain('{{senderName}}{{senderRole}}');
  });

  it('deletes a whole variable rather than half of one', async () => {
    const { container, emitted, html } = setup({
      mode: 'TEMPLATE', variables, value: '<p>{{senderName}}{{senderRole}}</p>',
    });

    const q = quillOf(container);
    await act(async () => {
      q.deleteText(0, 1, 'user');            // backspace over the first chip
    });

    await waitFor(() => expect(html()).not.toContain('data-variable="senderName"'));
    const out = emitted().at(-1) ?? '';
    expect(out).toContain('{{senderRole}}');
    // Not `{{senderNam` or a stray brace: an embed goes whole or not at all.
    expect(out).not.toContain('senderName');
    expect(out).not.toContain('{{}}');
  });

  it('inserts two variables in a row through the handle, then takes a separator between them', async () => {
    const ref = React.createRef<EmailRichTextEditorHandle>();
    const seen: string[] = [];

    function Host() {
      const [html, setHtml] = useState('<p></p>');
      return (
        <EmailRichTextEditor
          ref={ref}
          mode="TEMPLATE"
          variables={variables}
          value={html}
          onChange={(next) => { seen.push(next); setHtml(next); }}
        />
      );
    }

    const { container } = render(<Host />);

    await act(async () => { ref.current!.insertVariable(variables[0]); });
    await act(async () => { ref.current!.insertVariable(variables[1]); });

    await waitFor(() => expect(seen.at(-1) ?? '').toContain('{{senderRole}}'));

    const q = quillOf(container);
    await act(async () => {
      q.insertText(1, ' - ', 'user');
    });

    await waitFor(() => expect(seen.at(-1) ?? '').toContain('-'));
    // By meaning, for the reason given above: Quill spells a boundary space as `&nbsp;`.
    expect(isSameEmailHtml(seen.at(-1) ?? '', '<p>{{senderName}} - {{senderRole}}</p>')).toBe(true);
  });

  it('survives save and reload with the text between the variables intact', async () => {
    const stored = '<p>{{senderName}} / {{senderRole}}</p>';
    const seen: string[] = [];

    function Host() {
      const [html, setHtml] = useState(stored);
      return (
        <EmailRichTextEditor
          mode="TEMPLATE"
          variables={variables}
          value={html}
          onChange={(next) => { seen.push(next); setHtml(next); }}
        />
      );
    }

    const { container } = render(<Host />);
    await waitFor(() => expect(container.querySelector('.ql-editor')).toBeTruthy());

    // Both variables are still objects on screen…
    expect(container.querySelectorAll('.ql-editor [data-variable]')).toHaveLength(2);
    // …and what a save would write means the same as what was loaded.
    expect(isSameEmailHtml(seen.at(-1) ?? stored, stored)).toBe(true);
  });
});

// ── §7.3 table editing ──────────────────────────────────────────────────────

/**
 * The table UX, driven end to end: click the node, open the dialog, change it, apply.
 *
 * The node is atomic (see `emailEditorTable.ts`), so none of this can be done by typing — which makes
 * these the only tests that cover editing a table at all. They run against a real Quill for the reason
 * the rest of this file does: a mocked editor has no blot to click and no document to replace.
 */
describe('table editing', () => {
  const variables = [
    { name: 'senderName', label: 'Họ tên người gửi' },
    { name: 'delegationName', label: 'Tên đoàn' },
  ];

  const STORED = '<table role="presentation" width="100%" cellpadding="0" cellspacing="0"'
    + ' style="border-collapse:collapse;width:100%;margin:16px 0"><tbody>'
    + '<tr><th style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top;background:#f8fafc;font-weight:600;text-align:left">Hạng mục</th>'
    + '<th style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top;background:#f8fafc;font-weight:600;text-align:left">Số lượng</th></tr>'
    + '<tr><td style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top">Ghế</td>'
    + '<td style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top">20</td></tr>'
    + '</tbody></table>';

  /** Opens the dialog on the table already in the document, the way an author does: by clicking it. */
  async function openDialog(utils: ReturnType<typeof setup>) {
    const node = utils.container.querySelector('.pems-email-table') as HTMLElement;
    expect(node).toBeTruthy();
    fireEvent.click(node);
    fireEvent.click(screen.getByRole('button', { name: 'Chỉnh sửa bảng' }));
    return screen.findByTestId('table-dialog-apply');
  }

  const cell = (row: number, col: number) =>
    screen.getByLabelText(`Ô hàng ${row} cột ${col}`) as HTMLTextAreaElement;

  it('reads the cells out of the document, chips and all', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED, variables });
    await openDialog(utils);

    expect(cell(1, 1).value).toBe('Hạng mục');
    expect(cell(2, 2).value).toBe('20');
  });

  it('writes an edited cell back and leaves the untouched ones exactly as they were', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const apply = await openDialog(utils);

    fireEvent.change(cell(2, 2), { target: { value: '25' } });
    fireEvent.click(apply);

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('>25<'));
    const out = utils.emitted().at(-1) ?? '';
    expect(out).toContain('Hạng mục');
    expect(out).toContain('Ghế');
    expect(out).not.toContain('>20<');
    // Structure and the inline CSS a mail client needs, both intact.
    expect((out.match(/<tr>/g) ?? []).length).toBe(2);
    expect(out).toContain('border:1px solid #dbe4ee');
    expect(out).toContain('padding:8px 10px');
  });

  /**
   * Opening the dialog and applying it unchanged must not report the document as edited.
   *
   * This is the reason `applyTableEdit` patches the original markup instead of regenerating a table
   * from the model: a regenerated table is a different string even when it is the same table, and the
   * screen would offer to save a template nobody had touched.
   */
  it('does not dirty the document when nothing was changed', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const before = utils.emitted().length;

    const apply = await openDialog(utils);
    fireEvent.click(apply);

    await waitFor(() => expect(screen.queryByTestId('table-dialog-apply')).toBeNull());
    expect(utils.emitted().length).toBe(before);
    expect(isSameEmailHtml(utils.emitted().at(-1) ?? STORED, STORED)).toBe(true);
  });

  it('adds rows and columns, and the new cells carry the styling of the old', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const apply = await openDialog(utils);

    fireEvent.click(screen.getByRole('button', { name: 'Thêm hàng' }));
    fireEvent.click(screen.getByRole('button', { name: 'Thêm cột' }));
    fireEvent.change(cell(3, 3), { target: { value: 'mới' } });
    fireEvent.click(apply);

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('mới'));
    const out = utils.emitted().at(-1) ?? '';
    expect((out.match(/<tr>/g) ?? []).length).toBe(3);
    // A cloned cell keeps the styling of the one it came from — 3×3 cells, all still bordered. Without
    // this a widened table came out with the new column invisible in mail.
    expect((out.match(/border:1px solid #dbe4ee/g) ?? []).length).toBe(9);
  });

  it('removes a row, and takes only that row', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const apply = await openDialog(utils);

    fireEvent.click(screen.getByRole('button', { name: 'Xóa hàng 2' }));
    fireEvent.click(apply);

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').not.toContain('Ghế'));
    const out = utils.emitted().at(-1) ?? '';
    expect((out.match(/<tr>/g) ?? []).length).toBe(1);
    expect(out).toContain('Hạng mục');
    expect(out).toContain('Số lượng');
  });

  it('removes a column, and takes only that column', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const apply = await openDialog(utils);

    fireEvent.click(screen.getByRole('button', { name: 'Xóa cột 2' }));
    fireEvent.click(apply);

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').not.toContain('Số lượng'));
    const out = utils.emitted().at(-1) ?? '';
    expect((out.match(/<tr>/g) ?? []).length).toBe(2);
    expect(out).toContain('Hạng mục');
    expect(out).toContain('Ghế');
    expect(out).not.toContain('>20<');
  });

  it('will not delete the last row or the last column', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: '<table><tbody><tr><td>một</td></tr></tbody></table>' });
    await openDialog(utils);

    expect(screen.getByRole('button', { name: 'Xóa hàng 1' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Xóa cột 1' })).toBeDisabled();
  });

  it('turns the heading row off, keeping the cells', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const apply = await openDialog(utils);

    fireEvent.click(screen.getByRole('checkbox', { name: 'Hàng đầu là tiêu đề' }));
    fireEvent.click(apply);

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').not.toContain('<th'));
    expect(utils.emitted().at(-1)).toContain('Hạng mục');
  });

  it('applies alignment and a width preset in a form mail can use', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const apply = await openDialog(utils);

    fireEvent.change(screen.getByLabelText('Căn lề bảng'), { target: { value: 'center' } });
    fireEvent.change(screen.getByLabelText('Độ rộng bảng'), { target: { value: '50%' } });
    fireEvent.click(apply);

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('align="center"'));
    const out = utils.emitted().at(-1) ?? '';
    // Outlook honours the attribute; everything else honours the margin. Both, or it drifts left there.
    expect(out).toContain('margin:16px auto');
    expect(out).toContain('width="50%"');
    expect(out).toContain('width:50%');
  });

  it('puts a variable into a cell as a placeholder, not as the label', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED, variables });
    const apply = await openDialog(utils);

    fireEvent.focus(cell(2, 2));
    fireEvent.change(screen.getByLabelText('Chèn biến vào ô đang chọn'), { target: { value: 'senderName' } });
    fireEvent.click(apply);

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('{{senderName}}'));
    // On the wire it is the placeholder; on screen it is a chip.
    expect(utils.emitted().at(-1)).not.toContain('Họ tên người gửi');
    await waitFor(() => expect(utils.html()).toContain('data-variable="senderName"'));
  });

  it('offers no variables in COMPOSE, where a placeholder would ship unresolved', async () => {
    const utils = setup({ mode: 'COMPOSE', value: STORED, variables });
    await openDialog(utils);

    expect(screen.queryByLabelText('Chèn biến vào ô đang chọn')).toBeNull();
  });

  it('refuses to open a nested table rather than flattening it', async () => {
    const nested = '<table><tbody><tr><td><table><tbody><tr><td>trong</td></tr></tbody></table></td></tr></tbody></table>';
    const utils = setup({ mode: 'TEMPLATE', value: nested });

    const node = utils.container.querySelector('.pems-email-table') as HTMLElement;
    fireEvent.click(node);
    fireEvent.click(screen.getByRole('button', { name: 'Chỉnh sửa bảng' }));

    expect(screen.queryByTestId('table-dialog-apply')).toBeNull();
    expect(utils.onNotice).toHaveBeenCalledWith(expect.stringContaining('bảng lồng nhau'));
  });

  it('cannot be opened until a table is selected', () => {
    setup({ mode: 'TEMPLATE', value: '<p>không có bảng</p>' });

    expect(screen.getByRole('button', { name: 'Chỉnh sửa bảng' })).toBeDisabled();
  });

  /**
   * §5 — the table as an object the author can see they have selected, and can type after.
   *
   * The node is atomic by design, which is not the complaint: the complaint is that an author who
   * inserted one had no way to tell WHICH table "Chỉnh sửa bảng" would open, and — when the table was
   * the last thing in the document — nowhere to put the caret afterwards, so the editor read as stuck.
   */
  describe('selecting a table, and writing after one', () => {
    const TWO = `${STORED}<p>giữa</p>${STORED}`;

    it('marks the clicked table, and only that one', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: TWO });
      const nodes = Array.from(
        utils.container.querySelectorAll('.pems-email-table'),
      ) as HTMLElement[];
      expect(nodes).toHaveLength(2);

      fireEvent.click(nodes[1]);

      await waitFor(() => expect(nodes[1].getAttribute('data-selected')).toBe('true'));
      expect(nodes[0].getAttribute('data-selected')).toBeNull();

      fireEvent.click(nodes[0]);
      await waitFor(() => expect(nodes[0].getAttribute('data-selected')).toBe('true'));
      expect(nodes[1].getAttribute('data-selected')).toBeNull();
    });

    it('drops the mark when the click lands outside any table', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: TWO });
      const node = utils.container.querySelector('.pems-email-table') as HTMLElement;

      fireEvent.click(node);
      await waitFor(() => expect(node.getAttribute('data-selected')).toBe('true'));

      fireEvent.click(utils.root);

      await waitFor(() => expect(node.getAttribute('data-selected')).toBeNull());
      expect(screen.getByRole('button', { name: 'Chỉnh sửa bảng' })).toBeDisabled();
    });

    /**
     * The selected mark is editor furniture, and furniture must not become an edit. It is written onto
     * the wrapper element, which `nodesToTables` removes on the way to stored content — so selecting a
     * table cannot make the screen above offer to save one nobody touched.
     */
    it('is not an edit, and never reaches stored content', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: STORED });
      const before = utils.emitted().length;
      const node = utils.container.querySelector('.pems-email-table') as HTMLElement;

      fireEvent.click(node);

      await waitFor(() => expect(node.getAttribute('data-selected')).toBe('true'));
      expect(utils.emitted().length).toBe(before);
      for (const html of utils.emitted()) expect(html).not.toContain('data-selected');
    });

    /**
     * The caret goes AFTER the table, so the next thing typed continues below it rather than jumping to
     * the top of the document — which is where it used to go, because the toolbar click had blurred the
     * editor and a blurred editor answers 0 when asked where the caret is.
     *
     * A table inserted at the very END of a body is the one case this cannot fix: the last line is then
     * the object itself, so the document has no position after it, and neither Quill nor the html it is
     * given can be made to hold an empty one (see the notes above `dropTrailingBlank`). The author's
     * closing sentence has to be written before the table is added, or after it by pressing Enter first.
     */
    it('puts the caret after a table, ready for the next sentence', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: '<p>trên</p><p>dưới</p>' });

      // The caret sits at the end of the first line, where an author would put the table.
      const q = quillOf(utils.container);
      q.setSelection(5, 0, 'user');

      fireEvent.click(screen.getByRole('button', { name: 'Chèn bảng' }));
      fireEvent.click(await screen.findByTestId('table-dialog-apply'));
      await waitFor(() => expect(utils.html()).toContain('<table'));

      await act(async () => {
        q.insertText(q.getSelection()?.index ?? 0, 'sau bảng', 'user');
      });

      await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('sau'));
      const out = utils.emitted().at(-1) ?? '';
      expect(out.indexOf('sau')).toBeGreaterThan(out.indexOf('</table>'));
      expect(out.indexOf('trên')).toBeLessThan(out.indexOf('<table'));
    });

    it('leaves the table it has just inserted selected, ready to edit', async () => {
      setup({ mode: 'TEMPLATE', value: '<p>trên</p>' });

      fireEvent.click(screen.getByRole('button', { name: 'Chèn bảng' }));
      fireEvent.click(await screen.findByTestId('table-dialog-apply'));

      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Chỉnh sửa bảng' })).not.toBeDisabled());
    });

    it('keeps the table selected after an edit, so a second row is one click away', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: STORED });
      const apply = await openDialog(utils);

      fireEvent.change(cell(2, 2), { target: { value: '25' } });
      fireEvent.click(apply);

      await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('>25<'));
      // The element the state was holding has been replaced; the selection followed the replacement.
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Chỉnh sửa bảng' })).not.toBeDisabled());
      const node = utils.container.querySelector('.pems-email-table') as HTMLElement;
      expect(node.getAttribute('data-selected')).toBe('true');
    });

    /**
     * The stale-node case, and why the selection is held as a POSITION as well as an element.
     *
     * A controlled Quill rebuilds its whole document whenever the value is re-fed, so the element a
     * click selected is detached moments later — and "Chỉnh sửa bảng" was then pointing at markup no
     * longer in the document, which resolves an index in a document that node is not part of. The
     * position survives the rebuild, so the selection is re-resolved against whatever the editor now
     * holds; when the table is genuinely gone, the button goes back to being disabled.
     */
    it('refuses to edit a table that has left the document, and says why', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: STORED });
      const node = utils.container.querySelector('.pems-email-table') as HTMLElement;
      fireEvent.click(node);
      await waitFor(() => expect(node.getAttribute('data-selected')).toBe('true'));

      const q = quillOf(utils.container);
      await act(async () => {
        q.setText('bảng đã đi\n', 'user');
      });

      fireEvent.click(screen.getByRole('button', { name: 'Chỉnh sửa bảng' }));

      // No dialog on a table that is not there, and no silent no-op either.
      expect(screen.queryByTestId('table-dialog-apply')).toBeNull();
      expect(utils.onNotice).toHaveBeenCalledWith(expect.stringContaining('Vui lòng chọn lại bảng'));
      await waitFor(() =>
        expect(screen.getByRole('button', { name: 'Chỉnh sửa bảng' })).toBeDisabled());
    });
  });

  // ── §6 the dialog's own controls ──────────────────────────────────────────

  describe('the table dialog', () => {
    it('will not offer a variable until a cell has been chosen', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: STORED, variables });
      await openDialog(utils);

      const picker = screen.getByLabelText('Chèn biến vào ô đang chọn') as HTMLSelectElement;
      // Plainly unavailable, rather than available and silently doing nothing.
      expect(picker).toBeDisabled();
      expect(picker.options[0].text).toBe('Chọn một ô trước');

      fireEvent.focus(cell(1, 1));

      await waitFor(() => expect(picker).not.toBeDisabled());
      expect(screen.getByTestId('table-variable-target').textContent).toBe('Ô hàng 1 cột 1');
    });

    it('keeps what was typed when a row is added', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: STORED });
      const apply = await openDialog(utils);

      fireEvent.change(cell(2, 2), { target: { value: 'giữ lại' } });
      fireEvent.click(screen.getByRole('button', { name: 'Thêm hàng' }));

      expect(cell(2, 2).value).toBe('giữ lại');
      fireEvent.click(apply);
      await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('giữ lại'));
    });

    it('keeps what was typed when a column is added', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: STORED });
      const apply = await openDialog(utils);

      fireEvent.change(cell(2, 1), { target: { value: 'vẫn đây' } });
      fireEvent.click(screen.getByRole('button', { name: 'Thêm cột' }));

      expect(cell(2, 1).value).toBe('vẫn đây');
      fireEvent.click(apply);
      await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('vẫn đây'));
    });

    it('inserts the variable into the cell that was chosen, at the caret', async () => {
      const utils = setup({ mode: 'TEMPLATE', value: STORED, variables });
      const apply = await openDialog(utils);

      const target = cell(2, 1);
      fireEvent.focus(target);
      target.setSelectionRange(0, 0);
      fireEvent.select(target);

      fireEvent.change(screen.getByLabelText('Chèn biến vào ô đang chọn'), { target: { value: 'senderName' } });

      // In THAT cell, at its head — not appended to whichever cell rendered last.
      await waitFor(() => expect(cell(2, 1).value).toBe('{{senderName}}Ghế'));
      expect(cell(2, 2).value).toBe('20');

      fireEvent.click(apply);
      await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('{{senderName}}'));
    });
  });

  it('leaves the document alone when the dialog is cancelled', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    const before = utils.emitted().length;
    await openDialog(utils);

    fireEvent.change(cell(1, 1), { target: { value: 'không lưu' } });
    fireEvent.click(screen.getByRole('button', { name: 'Hủy' }));

    await waitFor(() => expect(screen.queryByTestId('table-dialog-apply')).toBeNull());
    expect(utils.emitted().length).toBe(before);
    expect(utils.html()).toContain('Hạng mục');
    expect(utils.html()).not.toContain('không lưu');
  });
});

// ── §14.5 links ─────────────────────────────────────────────────────────────

describe('links', () => {
  it('refuses a scheme an email may not carry', () => {
    const prompt = vi.spyOn(window, 'prompt').mockReturnValue('javascript:alert(1)');
    const { container, onNotice } = setup();
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Chèn liên kết' }));

    expect(onNotice).toHaveBeenCalledWith(expect.stringContaining('http, https, mailto hoặc tel'));
    prompt.mockRestore();
  });

  it('accepts an ordinary https link', async () => {
    const prompt = vi.spyOn(window, 'prompt').mockReturnValue('https://pems.fpt.edu.vn/x');
    const { container, html, onNotice } = setup({ value: '<p>bấm đây</p>' });
    selectAll(container);

    fireEvent.click(screen.getByRole('button', { name: 'Chèn liên kết' }));

    await waitFor(() => expect(html()).toContain('https://pems.fpt.edu.vn/x'));
    expect(onNotice).not.toHaveBeenCalled();
    prompt.mockRestore();
  });
});

// ── §5.4 capabilities ───────────────────────────────────────────────────────

describe('capabilities', () => {
  it('never permits raw HTML in either mode', () => {
    expect(TEMPLATE_CAPABILITIES.allowRawHtml).toBe(false);
    expect(COMPOSE_CAPABILITIES.allowRawHtml).toBe(false);
    expect(capabilitiesFor('COMPOSE').allowRawHtml).toBe(false);
  });

  it('lets COMPOSE move the action block but not create or delete one', () => {
    expect(COMPOSE_CAPABILITIES.allowSystemBlockMove).toBe(true);
    expect(COMPOSE_CAPABILITIES.allowSystemBlockInsert).toBe(false);
    expect(COMPOSE_CAPABILITIES.allowSystemBlockDelete).toBe(false);
  });

  it('lets a caller override where its flow genuinely differs', () => {
    setup({ mode: 'COMPOSE', capabilities: { allowSystemBlockInsert: true } });
    expect(screen.getByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeTruthy();
  });
});

// ── where an inserted variable LANDS (moved here from the template screen) ──

describe('insertVariable, through the imperative handle', () => {
  const variables = [{ name: 'fullName', label: 'Họ tên' }];

  /** Renders with a ref, so a host screen's sidebar can be simulated. */
  function setupWithRef(value: string) {
    const ref = React.createRef<EmailRichTextEditorHandle>();
    const seen: string[] = [];

    function Host() {
      const [html, setHtml] = useState(value);
      return (
        <EmailRichTextEditor
          ref={ref}
          mode="TEMPLATE"
          variables={variables}
          value={html}
          onChange={(next) => { seen.push(next); setHtml(next); }}
        />
      );
    }

    const utils = render(<Host />);
    const root = utils.container.querySelector('.ql-editor') as HTMLElement;
    return { ref, root, container: utils.container, html: () => root.innerHTML };
  }

  it('lands at the remembered caret after the editor has lost focus', async () => {
    const { ref, container, html } = setupWithRef('<p>Chào bạn.</p>');

    // Caret after "Chà" (index 3), then the sidebar chip steals the focus — the exact sequence that
    // used to defeat a DOM-focus check and send the variable to the end of the document.
    const q = quillOf(container);
    q.setSelection(3, 0, 'user');
    q.blur();

    ref.current!.insertVariable(variables[0]);

    await waitFor(() => expect(html()).toContain('data-variable="fullName"'));
    // Inside the sentence, not after it.
    expect(html().indexOf('data-variable')).toBeLessThan(html().indexOf('o bạn.'));
  });

  it('replaces a selected run rather than adding to it', async () => {
    const { ref, container, html } = setupWithRef('<p>Chào bạn.</p>');

    quillOf(container).setSelection(0, 4, 'user');   // "Chào"

    ref.current!.insertVariable(variables[0]);

    await waitFor(() => expect(html()).toContain('data-variable="fullName"'));
    expect(html()).not.toContain('Chào');
    expect(html()).toContain('bạn.');
  });

  it('falls back to the head of the document, never the tail', async () => {
    const { ref, html } = setupWithRef('<p>Chào bạn.</p>');

    // Nothing has been focused: no caret was ever reported.
    ref.current!.insertVariable(variables[0]);

    await waitFor(() => expect(html()).toContain('data-variable="fullName"'));
    expect(html().indexOf('data-variable')).toBeLessThan(html().indexOf('Chào'));
  });

  it('reports whether a live editor is attached', () => {
    const { ref } = setupWithRef('<p>x</p>');
    expect(ref.current!.isReady()).toBe(true);
  });
});

// ── §15.3 load → edit → save → reload ───────────────────────────────────────

describe('the round trip a template makes', () => {
  /**
   * Stored html → editor → stored html must be semantically identical, or every save writes a diff
   * nobody asked for and every open reports unsaved changes.
   */
  it.each([
    '<p>Kính gửi <strong>{{recipientName}}</strong>,</p><p>Trân trọng,</p>',
    `<p>Trước</p>${SYSTEM_ACTION_NODE}<p>Sau</p>`,
    '<p style="text-align:center">Giữa</p><p style="margin-left:32px">Thụt</p>',
    '<ul><li>Một</li><li>Hai</li></ul>',
    '<table role="presentation" style="border-collapse:collapse"><tbody>'
      + '<tr><td style="border:1px solid #dbe4ee">Đoàn</td>'
      + '<td style="border:1px solid #dbe4ee">{{delegationName}}</td></tr></tbody></table>',
  ])('survives load → edit → save → reload: %s', async (stored) => {
    const seen: string[] = [];

    function Host() {
      const [html, setHtml] = useState(stored);
      return (
        <EmailRichTextEditor
          mode="TEMPLATE"
          variables={[{ name: 'recipientName', label: 'Người nhận' }, { name: 'delegationName', label: 'Tên đoàn' }]}
          value={html}
          onChange={(next) => { seen.push(next); setHtml(next); }}
        />
      );
    }

    render(<Host />);

    // Whatever the editor emitted on load must MEAN the same as what was stored — that is what makes
    // "opening a template is not an edit" true on the screen above it.
    await waitFor(() => expect(document.querySelector('.ql-editor')).toBeTruthy());
    const emitted = seen.at(-1) ?? stored;

    expect(isSameEmailHtml(stored, emitted)).toBe(true);
  });
});

// ── §7 / §14.8 paste and whitespace, as pure functions ──────────────────────

describe('paste cleanup', () => {
  it.each([
    'position:absolute', 'z-index:99', 'transform:scale(2)', 'animation:x 1s',
    'display:none', 'visibility:hidden', 'opacity:0', 'font-size:0',
  ])('drops %s', (decl) => {
    expect(cleanInlineStyle(decl)).toBeNull();
  });

  it('keeps the formatting a sender actually wanted', () => {
    const kept = cleanInlineStyle('color:#374151;position:absolute;text-align:center');
    expect(kept).toContain('color');
    expect(kept).toContain('text-align');
    expect(kept).not.toContain('position');
  });

  it('normalises runs of spaces rather than turning them into nbsp', () => {
    expect(normalizeSpaceRuns('Số điện thoại:     0901234567')).toBe('Số điện thoại: 0901234567');
    expect(hasSpaceRun('a   b')).toBe(true);
    expect(hasSpaceRun('a b')).toBe(false);
  });

  /**
   * The blind spot that made the typed-run warning dead code.
   *
   * Quill returns a typed run as `&nbsp;` — `<p>a&nbsp;&nbsp;&nbsp;b</p>` — so a check against the
   * ASCII space alone never matched anything a person typed, and the warning could only ever appear
   * on paste, where the fragment is read before Quill touches it.
   */
  it('sees a run of NON-BREAKING spaces, which is how a typed run comes back', () => {
    expect(hasSpaceRun('a   b')).toBe(true);
    expect(hasSpaceRun('a   b')).toBe(true);   // mixed, as Quill actually emits them
    expect(hasSpaceRun('a b')).toBe(false);          // one is a normal character
  });
});

/**
 * V4 §7.4 — the space-run warning, on the way the runs actually get there.
 *
 * It used to fire on paste only. Typing is the likelier route: pasting from Word brings a table,
 * while a person lining two columns up by hand reaches for the space bar — and HTML collapses those
 * runs, so what they carefully aligned arrives as one ragged line.
 *
 * The editor WARNS and leaves the text alone. Deleting characters out from under someone mid-sentence
 * is worse than the problem, and `&nbsp;` would be worse still: it holds in the composer and then
 * refuses to wrap on a phone.
 */

/**
 * V4 §7.4 — the space-run warning, on the way the runs actually get there.
 *
 * It used to fire on paste only. Typing is the likelier route: pasting from Word brings a table,
 * while a person lining two columns up by hand reaches for the space bar — and HTML collapses those
 * runs, so what they carefully aligned arrives as one ragged line.
 *
 * The editor WARNS and leaves the text alone. Deleting characters out from under someone mid-sentence
 * is worse than the problem, and `&nbsp;` would be worse still: it holds in the composer and then
 * refuses to wrap on a phone.
 *
 * Typed through Quill's own text API rather than by assigning innerHTML. Quill owns its document; a
 * DOM write goes in behind its back, and its observer may or may not have caught up by the assertion —
 * which is how a test ends up measuring timing instead of behaviour.
 */
describe('EmailRichTextEditor space runs', () => {
  /** Types at the end of the document, as a person would. */
  const type = async (container: HTMLElement, text: string) => {
    const q = quillOf(container);
    await act(async () => {
      q.insertText(q.getLength() - 1, text, 'user');
    });
  };

  const warningsOf = (onNotice: ReturnType<typeof vi.fn>) =>
    onNotice.mock.calls.filter(([m]: [string]) => m === SPACE_RUN_WARNING);

  it('warns when a sender TYPES a run of spaces', async () => {
    const { container, onNotice } = setup({ value: '<p>Số điện thoại:</p>' });

    await type(container, '     0901234567');

    expect(warningsOf(onNotice)).toHaveLength(1);
  });

  it('leaves the typed text exactly as written', async () => {
    const { container, root } = setup({ value: '<p>Cột một</p>' });

    await type(container, '     Cột hai');

    // Warned, not rewritten: the sender still has their words, and the canonicalizer collapses the
    // run before anything is compared or sent.
    expect(root.textContent).toContain('Cột một     Cột hai');
  });

  it('says it once, not once per keystroke', async () => {
    const { container, onNotice } = setup({ value: '<p>a</p>' });

    await type(container, '   b');
    await type(container, 'c');
    await type(container, 'd');

    expect(warningsOf(onNotice)).toHaveLength(1);
  });

  it('warns again after the runs are removed and a new one is made', async () => {
    const { container, onNotice } = setup({ value: '<p>a</p>' });
    const q = quillOf(container);

    await type(container, '   b');
    expect(warningsOf(onNotice)).toHaveLength(1);

    // The sender fixes it — the whole document goes back to single spacing, and the flag re-arms.
    await act(async () => {
      q.setText('a b\n', 'user');
    });

    await type(container, '   c');

    expect(warningsOf(onNotice)).toHaveLength(2);
  });

  it('says nothing about ordinary single-spaced prose', async () => {
    const { container, onNotice } = setup({ value: '<p>Kính gửi anh Bình,</p>' });

    await type(container, ' nhờ anh hỗ trợ đón đoàn khách.');

    expect(warningsOf(onNotice)).toHaveLength(0);
  });

  /**
   * Two spaces are ordinary typing — after a full stop, or a stray double-tap. Three is somebody
   * building a column. `hasSpaceRun` draws the line at three deliberately: warning on two would make
   * the message noise, and a message people learn to dismiss protects nothing.
   */
  it('does not warn about a double space', async () => {
    const { container, onNotice } = setup({ value: '<p>Xong.</p>' });

    await type(container, '  Cảm ơn anh.');

    expect(warningsOf(onNotice)).toHaveLength(0);
  });
});

/**
 * Only a person's edit counts as an edit.
 *
 * <b>What this protects.</b> The editor is CONTROLLED: every render feeds Quill the current `value` via
 * `setContents()`, and Quill answers that with a change event of its own. `react-quill-new` does not tell
 * that echo apart from a keystroke, so it used to reach the host as `onChange` — and because Quill's
 * reparse is never byte-identical to what was fed in, the emitted html became the next `value`, which
 * reparsed to a third spelling, and so on. Two notations for the same content traded places forever,
 * pinning a CPU core the moment an editor opened on any template with a multi-declaration inline style.
 *
 * The fix is to filter on Quill's own `source`, and it is filtering on the ONE signal that cannot mistake
 * an echo for an edit. It is also invisible: nothing on screen shows whether a change was attributed to
 * `'user'` or `'api'`, so a future contributor "simplifying" the handler back to `onChange(html)` would
 * see every test still pass and every screen still work — until an operator's fan spun up. Hence these.
 *
 * Written against the REAL Quill, like the rest of this file: `source` is Quill's own concept, and a
 * mocked editor asserting it would only be asserting the mock.
 */
describe('EmailRichTextEditor change attribution', () => {
  it('reports a change the person typed', async () => {
    const { container, emitted } = setup({ value: '<p>xin chào</p>' });
    const before = emitted().length;
    const q = quillOf(container);

    await act(async () => {
      q.insertText(q.getLength() - 1, ' anh Nam', 'user');
    });

    expect(emitted().length).toBeGreaterThan(before);
    // Asserted on the words rather than on the spacing: Quill writes a typed space as `&nbsp;`, so
    // matching "anh Nam" literally would be testing its entity spelling, not that the edit was reported.
    expect(emitted().at(-1)).toContain('Nam');
  });

  it('stays silent for a programmatic change', async () => {
    const { container, emitted } = setup({ value: '<p>xin chào</p>' });
    const before = emitted().length;
    const q = quillOf(container);

    // Exactly what a controlled re-feed looks like from Quill's side, and what the composer's
    // "Đồng bộ dữ liệu mới nhất" does when it replaces the body: content written BY the application.
    // The document really does change — it simply was not the author who changed it.
    await act(async () => {
      q.insertText(q.getLength() - 1, ' (tự động)', 'api');
    });

    expect(q.getText()).toContain('(tự động)');
    expect(emitted().length).toBe(before);
  });

  it('stays silent for a silent change', async () => {
    const { container, emitted } = setup({ value: '<p>xin chào</p>' });
    const before = emitted().length;
    const q = quillOf(container);

    await act(async () => {
      q.insertText(q.getLength() - 1, ' im lặng', 'silent');
    });

    expect(emitted().length).toBe(before);
  });

  /**
   * The symptom as an operator met it: opening a document emitted nothing at all, so no host screen was
   * ever handed a "change" it had not been given by a person. A count that grows here is the loop.
   */
  it('emits nothing merely from opening a document', async () => {
    const { emitted } = setup({
      value: '<p style="color:#334155;font-size:14px;line-height:1.65">Kính gửi Quý vị,</p>',
    });

    await waitFor(() => expect(document.querySelector('.ql-editor')).toBeTruthy());
    expect(emitted()).toHaveLength(0);
  });
});

// ── the focus a person has just given to something else ─────────────────────

/**
 * Losing the selection must cost nothing — least of all the focus somebody just placed elsewhere.
 *
 * <b>The defect these pin.</b> The selection handler refreshed the toolbar with `quill.getFormat()`,
 * called with no arguments. That signature is a trap: its index defaults to `getSelection(true)`, and the
 * `true` is a FOCUS flag — Quill runs `root.focus()` and restores its last range whenever it does not
 * already hold focus (core/quill.js `getFormat`/`getSelection`/`focus`). Quill reports the null range
 * from a document-level `selectionchange` listener behind a timer, so it arrives about a millisecond
 * AFTER the click that moved focus away, and the refresh tore focus back out of whatever had just been
 * clicked.
 *
 * One line, two faces, which is why both are pinned here rather than in the screens that showed them: a
 * caret placed in the template screen's subject input jumped back into the body a frame later, and every
 * cell of the table dialog refused a keystroke. Neither was an input bug or a dialog bug.
 *
 * The null report is driven through Quill's own selection object, which is the same call `quill.blur()`
 * makes — but with the `'user'` source a real focus change carries, so these exercise the path production
 * takes rather than an easier one.
 */
describe('a lost selection never steals focus back', () => {
  /** Reports the loss of selection exactly as Quill does when focus moves out of the editor. */
  function reportSelectionLost(container: HTMLElement) {
    quillOf(container).selection.setRange(null, 'user');
  }

  /**
   * The template screen's shape in miniature: the editor, and a plain text input beside it.
   *
   * A second field is the whole point — the defect is invisible without somewhere else for focus to be.
   */
  function setupBesideAnInput(value = '<p>Chào bạn.</p>') {
    function Host() {
      const [html, setHtml] = useState(value);
      const [subject, setSubject] = useState('Hello world');
      return (
        <div>
          <input
            aria-label="Tiêu đề"
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
          />
          <EmailRichTextEditor mode="TEMPLATE" value={html} onChange={setHtml} />
        </div>
      );
    }

    const utils = render(<Host />);
    return {
      container: utils.container,
      subject: () => screen.getByLabelText('Tiêu đề') as HTMLInputElement,
    };
  }

  it('leaves focus on the field the operator moved to', async () => {
    const { container, subject } = setupBesideAnInput();

    // The body was being written in a moment ago, so Quill has a range to restore — which is what made
    // the theft possible. Without this the editor has nothing to go back to and the bug hides.
    const q = quillOf(container);
    act(() => { q.setSelection(3, 0, 'user'); });

    act(() => { subject().focus(); });
    await act(async () => { reportSelectionLost(container); });

    expect(document.activeElement).toBe(subject());
    expect(document.activeElement).not.toBe(container.querySelector('.ql-editor'));
  });

  it('keeps the caret where it was clicked, mid-text, and types there', async () => {
    const { container, subject } = setupBesideAnInput();

    const q = quillOf(container);
    act(() => { q.setSelection(3, 0, 'user'); });

    // "Hello |world" — the click lands between the two words, not at either end.
    const input = subject();
    act(() => {
      input.focus();
      input.setSelectionRange(6, 6);
    });

    await act(async () => { reportSelectionLost(container); });

    expect(document.activeElement).toBe(input);
    expect(input.selectionStart).toBe(6);

    // And the field still takes text at that caret rather than at an end it was pushed to.
    fireEvent.change(input, { target: { value: 'Hello PEMS world' } });
    expect(subject().value).toBe('Hello PEMS world');
  });

  /**
   * The toolbar must still follow a REAL selection — the guard is meant to drop the null report, not to
   * stop the editor reading formats at all. Without this a "fix" that returns from the handler
   * unconditionally would pass every test above and quietly freeze the toolbar.
   */
  it('still refreshes the toolbar for a selection a person made', async () => {
    const { container } = setup({ value: '<p><strong>Kính gửi</strong> Quý vị,</p>' });

    await act(async () => { quillOf(container).setSelection(0, 4, 'user'); });

    await waitFor(() => expect(
      screen.getByRole('button', { name: 'Đậm' }).getAttribute('aria-pressed'),
    ).toBe('true'));
  });
});

/**
 * The table dialog, driven the way an operator drives it: click a cell, type in it, move to the next.
 *
 * Every one of these failed before the selection handler stopped grabbing focus — clicking a cell blurs
 * Quill, and the blur report pulled focus straight back out of the textarea, so the dialog looked frozen.
 * They live beside a REAL editor rather than rendering `EmailTableDialog` on its own, because a dialog
 * rendered alone has no Quill to take the focus and would pass while the screen stayed broken.
 */
describe('table dialog cells accept editing', () => {
  const STORED = '<table role="presentation" width="100%" cellpadding="0" cellspacing="0"'
    + ' style="border-collapse:collapse;width:100%;margin:16px 0"><tbody>'
    + '<tr><td style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top">Đoàn khách</td>'
    + '<td style="border:1px solid #dbe4ee;padding:8px 10px;vertical-align:top">20</td></tr>'
    + '</tbody></table>';

  const cell = (row: number, col: number) =>
    screen.getByLabelText(`Ô hàng ${row} cột ${col}`) as HTMLTextAreaElement;

  /** Opens the dialog by clicking the table, the way an author does. */
  async function openDialog(utils: ReturnType<typeof setup>) {
    const node = utils.container.querySelector('.pems-email-table') as HTMLElement;
    fireEvent.click(node);
    fireEvent.click(screen.getByRole('button', { name: 'Chỉnh sửa bảng' }));
    await screen.findByTestId('table-dialog-apply');
  }

  /**
   * Clicks into a cell, with the state that makes the click dangerous.
   *
   * <b>The precondition is the test.</b> Quill only reports a lost selection when it HAD one — `update()`
   * compares against the previous range and stays quiet when both are null (core/selection.js). An author
   * reaching this dialog has been writing in the body, so Quill is holding a caret; a test that skips that
   * setup gets no event at all, exercises nothing, and passes against the very defect it was written for.
   * That is not hypothetical — the first draft of these did exactly that. So the caret is established
   * first, and the report is then asserted to have actually happened.
   */
  async function focusCell(utils: ReturnType<typeof setup>, row: number, col: number) {
    const q = quillOf(utils.container);
    act(() => { q.setSelection(0, 0, 'user'); });
    expect(q.getSelection()).not.toBeNull();

    const target = cell(row, col);
    act(() => { target.focus(); });

    let reported = 0;
    const count = (range: unknown) => { if (range === null) reported += 1; };
    q.on('selection-change', count);
    await act(async () => { q.selection.setRange(null, 'user'); });
    q.off('selection-change', count);

    expect(reported).toBeGreaterThan(0);
    return target;
  }

  it('keeps focus in the cell that was clicked', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    await openDialog(utils);

    const target = await focusCell(utils, 1, 1);

    expect(document.activeElement).toBe(target);
  });

  it('takes a keystroke in the focused cell', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    await openDialog(utils);

    const target = await focusCell(utils, 1, 1);
    fireEvent.change(target, { target: { value: 'ABC' } });

    expect(cell(1, 1).value).toBe('ABC');
    expect(document.activeElement).toBe(cell(1, 1));
  });

  it('edits one cell without disturbing the other', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    await openDialog(utils);

    fireEvent.change(await focusCell(utils, 1, 1), { target: { value: 'A' } });
    fireEvent.change(await focusCell(utils, 1, 2), { target: { value: 'B' } });

    expect(cell(1, 1).value).toBe('A');
    expect(cell(1, 2).value).toBe('B');
  });

  /** Text goes in where the caret is, not appended to whichever end the focus was thrown to. */
  it('inserts at a caret placed in the middle of a cell', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    await openDialog(utils);

    const target = await focusCell(utils, 1, 1);
    expect(target.value).toBe('Đoàn khách');

    act(() => { target.setSelectionRange(5, 5); });          // "Đoàn |khách"
    fireEvent.change(target, { target: { value: 'Đoàn FPT khách' } });

    expect(cell(1, 1).value).toBe('Đoàn FPT khách');
  });

  /** And the edit survives to the document — the dialog is not a scratchpad. */
  it('applies an edit made after the blur report', async () => {
    const utils = setup({ mode: 'TEMPLATE', value: STORED });
    await openDialog(utils);

    fireEvent.change(await focusCell(utils, 1, 2), { target: { value: '25' } });
    fireEvent.click(screen.getByTestId('table-dialog-apply'));

    await waitFor(() => expect(utils.emitted().at(-1) ?? '').toContain('>25<'));
    expect(utils.emitted().at(-1) ?? '').toContain('Đoàn khách');
  });
});
