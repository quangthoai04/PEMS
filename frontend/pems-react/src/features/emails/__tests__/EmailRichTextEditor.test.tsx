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

// ── §9 the action block, through the shared editor ──────────────────────────

describe('the system action block', () => {
  it('is offered in TEMPLATE mode and withheld in COMPOSE', () => {
    const { unmount } = setup({ mode: 'TEMPLATE' });
    expect(screen.getByRole('button', { name: 'Chèn khối nút phản hồi' })).toBeTruthy();
    unmount();

    setup({ mode: 'COMPOSE' });
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

  it('refuses a second one rather than minting one token into two buttons', () => {
    const { onNotice } = setup({ value: `<p>a</p>${SYSTEM_ACTION_NODE}` });

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
